using System.Security.Cryptography;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class OutcomeV02DatabaseTests
{
    public static bool DatabaseConfigured => ScreenerEvidenceV02DatabaseTests.DatabaseConfigured;
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Task Sql(NpgsqlConnection c, string sql) => ScreenerEvidenceV02ReadinessDatabaseTests.Sql(c, sql);
    private static string Json(object value) => JsonSerializer.Serialize(value, DecisionSnapshotService.JsonOptions);
    private static async Task<long> Count(NpgsqlConnection c, string table)
    { await using var cmd = new NpgsqlCommand("SELECT count(*) FROM " + table, c); return (long)(await cmd.ExecuteScalarAsync(Token))!; }
    private sealed class Fixture(NpgsqlConnection connection, OutcomeV02Fixture facts, string root) : IAsyncDisposable
    {
        internal NpgsqlConnection Connection { get; } = connection;
        internal OutcomeV02Fixture Facts { get; } = facts;
        internal string Root { get; } = root;
        internal string Archive => Path.Combine(Root, "retained.bin");
        internal static async Task<Fixture> Create(bool historical = false, bool candidate = true, bool zero = false)
        {
            var c = await ScreenerEvidenceV02DatabaseTests.Fixture(Token);
            var root = Path.Combine(Path.GetTempPath(), "idx-outcome-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); var bytes = "synthetic raw"u8.ToArray();
            await using var clock = new NpgsqlCommand("SELECT clock_timestamp()", c);
            var now = new DateTimeOffset((DateTime)(await clock.ExecuteScalarAsync(Token))!, TimeSpan.Zero);
            var known = historical ? now.AddDays(-30) : now.AddMinutes(-5);
            var f = new Fixture(c, new(candidate, zero, OutcomeEvaluator.Through(known), known, Convert.ToHexStringLower(SHA256.HashData(bytes))), root);
            try
            {
                await File.WriteAllBytesAsync(f.Archive, bytes, Token);
                await Sql(c, "CREATE TEMP TABLE raw_artifact (LIKE public.raw_artifact INCLUDING ALL);");
                await using var raw = new NpgsqlCommand("""
                    INSERT INTO raw_artifact(raw_artifact_id,ingestion_run_id,source_id,original_uri,request_parameters,fetched_at,local_uri,content_sha256,byte_length,parser_version)
                    VALUES($1,$2,'source-a','synthetic','{}',$3,$4,$5,13,'synthetic')
                    """, c);
                raw.Parameters.AddWithValue(f.Facts.Artifact.ArtifactId); raw.Parameters.AddWithValue(Guid.NewGuid());
                raw.Parameters.AddWithValue(f.Facts.Artifact.RetrievedAt); raw.Parameters.AddWithValue(new Uri(f.Archive).AbsoluteUri);
                raw.Parameters.AddWithValue(f.Facts.Artifact.ContentHash); await raw.ExecuteNonQueryAsync(Token);
                if (historical)
                    await Sql(c, """
                        CREATE TEMP TABLE screener_technical_capture (LIKE public.screener_technical_capture INCLUDING ALL);
                        CREATE TEMP TABLE screener_technical_candidate (LIKE public.screener_technical_candidate INCLUDING ALL);
                        CREATE TEMP TABLE outcome_v02_enrollment (LIKE public.outcome_v02_enrollment INCLUDING ALL);
                        CREATE TEMP TABLE outcome_v02_observation (LIKE public.outcome_v02_observation INCLUDING ALL);
                        CREATE TRIGGER outcome_guard BEFORE INSERT ON outcome_v02_observation FOR EACH ROW EXECUTE FUNCTION outcome_v02_observation_validate();
                        CREATE TRIGGER immutable_outcome BEFORE UPDATE OR DELETE ON outcome_v02_observation FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
                        CREATE TRIGGER immutable_outcome_truncate BEFORE TRUNCATE ON outcome_v02_observation FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
                        """);
                await f.Append(f.Facts.Rows);
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        internal async Task Append(IEnumerable<ScreenerEvidenceRecord> rows)
        {
            foreach (var row in rows.OrderBy(r => r.Claim == EvidenceClaim.GenuinePriceObservation ? 2
                : ScreenerEvidenceBinding.Decode(r).Value is ActionCoverageValue or ReopeningValue ? 1 : 0))
                await ScreenerEvidenceV02Store.AppendAsync(Connection, null, row, Token);
        }
        internal async Task<(ScreenerTechnicalCandidateDecision Candidate, ScreenerTechnicalCapture Capture)> Source(bool historical = false, bool later = false)
        {
            var q = later ? Facts.Request with { Cutoff = Facts.Request.Cutoff.AddTicks(1) } : Facts.Request;
            var capture = (await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(Connection, q, Token)).Capture;
            if (historical)
            {
                // Trusted disposable historical fixture only; production commands never accept backdated clocks.
                await using var cmd = new NpgsqlCommand("UPDATE screener_technical_capture SET recorded_at=$1 WHERE capture_id=$2", Connection);
                cmd.Parameters.AddWithValue(Facts.Known.AddSeconds(1)); cmd.Parameters.AddWithValue(capture.CaptureId); await cmd.ExecuteNonQueryAsync(Token);
                capture = (await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(Connection, null, capture.CaptureId, Token))!;
            }
            var candidate = (await ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(Connection, capture.CaptureId, Token)).Decision;
            if (historical)
            {
                await using var cmd = new NpgsqlCommand("UPDATE screener_technical_candidate SET recorded_at=$1 WHERE decision_id=$2", Connection);
                cmd.Parameters.AddWithValue(Facts.Known.AddSeconds(2)); cmd.Parameters.AddWithValue(candidate.DecisionId); await cmd.ExecuteNonQueryAsync(Token);
                candidate = candidate with { RecordedAt = Facts.Known.AddSeconds(2) };
            }
            return (candidate, capture);
        }
        internal async Task<OutcomeV02Enrollment> HistoricalEnrollment()
        {
            var s = await Source(true); var exact = new List<ScreenerEvidenceRecord>();
            foreach (var link in s.Capture.Projection.Result.References)
                exact.Add((await ScreenerEvidenceV02Store.ReadExactAsync(Connection, null, link.EvidenceId, Token))!);
            var e = OutcomeV02.Enroll(s.Candidate, s.Capture, new(exact, [Facts.Artifact]), Facts.Known.AddSeconds(3), Facts.Known.AddSeconds(4));
            await using var insert = new NpgsqlCommand("""
                INSERT INTO outcome_v02_enrollment(enrollment_id,enrollment_identity,binding_hash,candidate_decision_id,capture_id,outcome_policy_id,schema_version,
                    enrollment_known_at,enrollment_recorded_at,enrollment_deadline,projection)
                VALUES($1,$2,$3,$4,$5,'outcome-v0.2.0',1,$6,$7,$8,$9)
                """, Connection);
            insert.Parameters.AddWithValue(e.EnrollmentId); insert.Parameters.AddWithValue(e.EnrollmentIdentity); insert.Parameters.AddWithValue(e.BindingHash);
            insert.Parameters.AddWithValue(s.Candidate.DecisionId); insert.Parameters.AddWithValue(s.Capture.CaptureId);
            insert.Parameters.AddWithValue(e.Projection.EnrollmentKnownAt); insert.Parameters.AddWithValue(e.Projection.EnrollmentRecordedAt);
            insert.Parameters.AddWithValue(e.Projection.EnrollmentDeadline); insert.Parameters.AddWithValue(Json(e.Projection)); await insert.ExecuteNonQueryAsync(Token);
            return e;
        }
        internal async Task Future(int n, bool status = true)
        { var before = Facts.Rows.Count; Facts.Future(n, status: status); await Append(Facts.Rows.Skip(before)); }
        public async ValueTask DisposeAsync()
        { await Connection.DisposeAsync(); Directory.Delete(Root, true); }
    }
    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ExplicitEnrollmentRetriesDistinctCandidatesForeignKeysAndMigrationAreAdditive()
    {
        await using var f = await Fixture.Create(); var c = f.Connection;
        var before = await ScreenerEvidenceV02CaptureDatabaseTests.V01Fingerprint(c); var count = await Count(c, "outcome_v02_enrollment");
        var s = await f.Source(); Assert.Equal(count, await Count(c, "outcome_v02_enrollment"));
        var first = await OutcomeV02Store.EnrollAsync(c, s.Candidate.DecisionId, f.Root, Token); Assert.True(first.Created);
        Assert.Equal(s.Capture.CaptureId, first.Enrollment.Projection.CaptureId); Assert.Equal(s.Candidate.RecordedAt, first.Enrollment.Projection.CandidateRecordedAt);
        var retry = await OutcomeV02Store.EnrollAsync(c, s.Candidate.DecisionId, f.Root, Token);
        Assert.False(retry.Created); Assert.Equal(Json(first.Enrollment), Json(retry.Enrollment));
        var later = await f.Source(later: true); var second = await OutcomeV02Store.EnrollAsync(c, later.Candidate.DecisionId, f.Root, Token);
        Assert.NotEqual(first.Enrollment.EnrollmentId, second.Enrollment.EnrollmentId);
        var migration = await File.ReadAllTextAsync(Path.Combine(ScreenerEvidenceV02BindingDatabaseTests.Root(), "src/IdxStockIntelligence.Infrastructure/Migrations/0011_outcome_v02.sql"), Token);
        await Sql(c, migration); await Sql(c, migration);
        Assert.Equal(before, await ScreenerEvidenceV02CaptureDatabaseTests.V01Fingerprint(c));
        await using var foreign = new NpgsqlCommand("SELECT count(*) FROM pg_constraint WHERE contype='f' AND conrelid IN ('public.outcome_v02_enrollment'::regclass,'public.outcome_v02_observation'::regclass)", c);
        Assert.Equal(3L, await foreign.ExecuteScalarAsync(Token));
        foreach (var sql in new[] { $"UPDATE outcome_v02_enrollment SET binding_hash=repeat('a',64) WHERE enrollment_id='{first.Enrollment.EnrollmentId:D}'",
            $"DELETE FROM outcome_v02_enrollment WHERE enrollment_id='{first.Enrollment.EnrollmentId:D}'", "TRUNCATE outcome_v02_enrollment" })
            await Assert.ThrowsAsync<PostgresException>(() => Sql(c, sql));
        Assert.Equal(first.Enrollment.BindingHash, (await OutcomeV02Store.ReadEnrollmentAsync(c, null, first.Enrollment.EnrollmentId, Token))!.BindingHash);
    }
    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("non-candidate")] [InlineData("missing")] [InlineData("expired")]
    [InlineData("archive-missing")] [InlineData("archive-changed")]
    public async Task InvalidNewEnrollmentWritesNothing(string defect)
    {
        await using var f = await Fixture.Create(historical: defect == "expired", candidate: defect != "non-candidate");
        var s = await f.Source(); var before = await Count(f.Connection, "outcome_v02_enrollment");
        if (defect == "archive-missing") File.Delete(f.Archive);
        if (defect == "archive-changed") await File.WriteAllBytesAsync(f.Archive, "different raw"u8.ToArray(), Token);
        var ex = await Assert.ThrowsAsync<EvidenceBindingException>(() => OutcomeV02Store.EnrollAsync(f.Connection,
            defect == "missing" ? Guid.NewGuid() : s.Candidate.DecisionId, f.Root, Token));
        Assert.NotEmpty(ex.Reason); Assert.Equal(before, await Count(f.Connection, "outcome_v02_enrollment"));
    }
    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task IncrementalTerminalRowsPreserveOldResultsAndResearchPopulation()
    {
        await using var f = await Fixture.Create(historical: true); var c = f.Connection;
        var v01 = await ScreenerEvidenceV02CaptureDatabaseTests.V01Fingerprint(c); var e = await f.HistoricalEnrollment();
        // A matching committed enrollment remains authentic on a retry after the strict deadline.
        var retryEnrollment = await OutcomeV02Store.EnrollAsync(c, e.Projection.CandidateDecisionId, f.Root, Token);
        Assert.False(retryEnrollment.Created); Assert.Equal(e.BindingHash, retryEnrollment.Enrollment.BindingHash);
        Assert.Null((await OutcomeV02Store.AssessAsync(c, e.EnrollmentId, 1, f.Root, Token)).Observation);
        await f.Future(1);
        var first = await OutcomeV02Store.AssessAsync(c, e.EnrollmentId, 1, f.Root, Token); Assert.Equal("AVAILABLE", first.State);
        Assert.Null((await OutcomeV02Store.AssessAsync(c, e.EnrollmentId, 5, f.Root, Token)).Observation); Assert.Equal(1L, await Count(c, "outcome_v02_observation"));
        await f.Future(5);
        var fifth = await OutcomeV02Store.AssessAsync(c, e.EnrollmentId, 5, f.Root, Token); Assert.Equal("AVAILABLE", fifth.State);
        Assert.Equal(2L, await Count(c, "outcome_v02_observation"));
        var retry = await OutcomeV02Store.AssessAsync(c, e.EnrollmentId, 1, f.Root, Token); Assert.Equal(Json(first), Json(retry));
        File.Delete(f.Archive); // A terminal read/retry does not rerun selection or repair the original result.
        Assert.Equal(Json(first), Json(await OutcomeV02Store.AssessAsync(c, e.EnrollmentId, 1, f.Root, Token)));
        foreach (var sql in new[] { "UPDATE outcome_v02_observation SET result_hash=repeat('a',64)", "DELETE FROM outcome_v02_observation", "TRUNCATE outcome_v02_observation" })
            await Assert.ThrowsAsync<PostgresException>(() => Sql(c, sql));
        var schema = "outcome_research_" + Guid.NewGuid().ToString("N");
        await Sql(c, $"CREATE SCHEMA {schema}; CREATE TABLE {schema}.outcome_v02_enrollment AS TABLE pg_temp.outcome_v02_enrollment; CREATE TABLE {schema}.outcome_v02_observation AS TABLE pg_temp.outcome_v02_observation;");
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDX_SCREENER_TEST_CONNECTION")) { SearchPath = schema + ",public" };
        await using var dataSource = NpgsqlDataSource.Create(connection.ConnectionString);
        var now = DateTimeOffset.UtcNow;
        var research = await new ResearchService(dataSource).ReadAsync(new(new(2026, 8, 24), OutcomeEvaluator.Through(now), now), Token);
        Assert.Equal(0, research.Summary.Counts.N); Assert.Equal(v01, await ScreenerEvidenceV02CaptureDatabaseTests.V01Fingerprint(c));
    }
    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("anchor", "ANCHOR_UNAVAILABLE")] [InlineData("suspension", "DATA_UNAVAILABLE")] [InlineData("split", "BASIS_UNCERTAIN")]
    public async Task PositiveRetainedEvidencePersistsUnavailableStates(string scenario, string expected)
    {
        await using var f = await Fixture.Create(historical: true, zero: scenario == "anchor"); var e = await f.HistoricalEnrollment();
        await f.Future(1, status: scenario != "suspension"); var d = f.Facts.Request.EvaluationDate.AddDays(1); var before = f.Facts.Rows.Count;
        if (scenario == "suspension") f.Facts.Add(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "positive"), d, known: f.Facts.After(1));
        if (scenario == "split") f.Facts.Add(EvidenceClaim.CorporateAction, new ActionEventValue(EvidenceActionValueKind.EVENT, "split", EvidenceActionType.SPLIT, d, null, null), d, known: f.Facts.After(1));
        await f.Append(f.Facts.Rows.Skip(before));
        var result = await OutcomeV02Store.AssessAsync(f.Connection, e.EnrollmentId, 1, f.Root, Token);
        Assert.Equal(expected, result.State); Assert.NotNull(result.Observation); Assert.Null(result.Observation.Projection.PriceReturnPct);
        Assert.Equal(1L, await Count(f.Connection, "outcome_v02_observation"));
    }
    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task CorruptedBindingAndObservationHashFailWithoutFallback()
    {
        await using var f = await Fixture.Create(historical: true); var e = await f.HistoricalEnrollment(); var c = f.Connection;
        await Sql(c, "UPDATE outcome_v02_enrollment SET binding_hash=repeat('b',64);");
        await Assert.ThrowsAsync<EvidenceBindingException>(() => OutcomeV02Store.EnrollAsync(c, e.Projection.CandidateDecisionId, f.Root, Token));
        await using (var restore = new NpgsqlCommand("UPDATE outcome_v02_enrollment SET binding_hash=$1", c))
        { restore.Parameters.AddWithValue(e.BindingHash); await restore.ExecuteNonQueryAsync(Token); }
        await f.Future(1); await OutcomeV02Store.AssessAsync(c, e.EnrollmentId, 1, f.Root, Token);
        // Fixture corruption of a copy leaves migration-installed mutation guards intact.
        await Sql(c, "ALTER TABLE pg_temp.outcome_v02_observation RENAME TO authentic_observations; CREATE TEMP TABLE outcome_v02_observation AS TABLE authentic_observations; UPDATE outcome_v02_observation SET result_hash=repeat('b',64);");
        await Assert.ThrowsAsync<EvidenceBindingException>(() => OutcomeV02Store.AssessAsync(c, e.EnrollmentId, 1, f.Root, Token));
        Assert.Equal(1L, await Count(c, "outcome_v02_observation"));
    }
}
