using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class OutcomeV02VerificationDatabaseTests
{
    public static bool DatabaseConfigured => ScreenerEvidenceV02DatabaseTests.DatabaseConfigured;
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Task Sql(NpgsqlConnection c, string sql) => ScreenerEvidenceV02ReadinessDatabaseTests.Sql(c, sql);
    private sealed class Fixture(OutcomeV02DatabaseTests.Fixture original, NpgsqlDataSource dataSource, string schema,
        OutcomeV02Enrollment enrollment, OutcomeV02Observation observation) : IAsyncDisposable
    {
        internal NpgsqlConnection Connection => original.Connection;
        internal string Root => original.Root;
        internal string Archive => original.Archive;
        internal OutcomeV02Observation Observation => observation;
        internal OutcomeV02Enrollment Enrollment => enrollment;
        internal OutcomeV02VerificationService Service { get; } = new(dataSource, original.Root);
        internal static async Task<Fixture> Create(string state = "AVAILABLE")
        {
            var f = await OutcomeV02DatabaseTests.Fixture.Create(historical: true, zero: state == "ANCHOR_UNAVAILABLE");
            try
            {
                var e = await f.HistoricalEnrollment(); await f.Future(1, status: state != "DATA_UNAVAILABLE");
                var d = f.Facts.Request.EvaluationDate.AddDays(1); var start = f.Facts.Rows.Count;
                if (state == "DATA_UNAVAILABLE") f.Facts.Add(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice"), d, known: f.Facts.After(1));
                if (state == "BASIS_UNCERTAIN") f.Facts.Add(EvidenceClaim.CorporateAction, new ActionEventValue(EvidenceActionValueKind.EVENT, "split", EvidenceActionType.SPLIT, d, null, null), d, known: f.Facts.After(1));
                await f.Append(f.Facts.Rows.Skip(start));
                var o = (await OutcomeV02Store.AssessAsync(f.Connection, e.EnrollmentId, 1, f.Root, Token)).Observation!; Assert.NotNull(o);
                var schema = "outcome_verify_" + Guid.NewGuid().ToString("N"); await Sql(f.Connection, "CREATE SCHEMA " + schema);
                foreach (var table in new[] { "screener_evidence_record", "raw_artifact", "screener_technical_capture", "screener_technical_candidate", "outcome_v02_enrollment", "outcome_v02_observation" })
                    await Sql(f.Connection, $"CREATE TABLE {schema}.{table} AS TABLE pg_temp.{table};");
                var config = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDX_SCREENER_TEST_CONNECTION")) { SearchPath = schema + ",public" };
                await Sql(f.Connection, "SET search_path=" + schema + ",public,pg_temp");
                return new(f, NpgsqlDataSource.Create(config.ConnectionString), schema, e, o);
            }
            catch { await f.DisposeAsync(); throw; }
        }
        internal Task<OutcomeV02VerificationResult> Verify() => Service.VerifyAsync(enrollment.EnrollmentId, 1, Token);
        internal async Task<string> Fingerprint()
        {
            var hashes = new List<string>();
            foreach (var table in new[] { "screener_evidence_record", "raw_artifact", "screener_technical_capture", "screener_technical_candidate", "outcome_v02_enrollment", "outcome_v02_observation" })
            {
                await using var cmd = new NpgsqlCommand($"SELECT count(*)||':'||coalesce(md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)),'') FROM {schema}.{table} t", Connection);
                hashes.Add((string)(await cmd.ExecuteScalarAsync(Token))!);
            }
            return string.Join(";", hashes);
        }
        public async ValueTask DisposeAsync()
        { await dataSource.DisposeAsync(); await original.DisposeAsync(); }
    }
    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("AVAILABLE")] [InlineData("ANCHOR_UNAVAILABLE")] [InlineData("DATA_UNAVAILABLE")] [InlineData("BASIS_UNCERTAIN")]
    public async Task ExactCommittedStatesVerifyReadOnlyAndIgnoreLaterEvidence(string state)
    {
        await using var f = await Fixture.Create(state); var before = await f.Fingerprint();
        var result = await f.Verify(); Assert.Equal(OutcomeVerificationState.Match, result.State);
        Assert.Equal(before, await f.Fingerprint()); Assert.Equal(state, result.ReplayedProjection!.State);
        // A later revision/current candidate exists but cannot be used by the exact-key verifier.
        await Sql(f.Connection, """
            INSERT INTO screener_evidence_record SELECT (jsonb_populate_record(NULL::screener_evidence_record,
                to_jsonb(r)||jsonb_build_object('evidence_id',gen_random_uuid(),'revision_number',revision_number+1,
                'supersedes_revision_number',revision_number,'known_at',known_at+interval '1 year'))).* FROM screener_evidence_record r LIMIT 1;
            INSERT INTO screener_technical_candidate SELECT (jsonb_populate_record(NULL::screener_technical_candidate,
                to_jsonb(d)||jsonb_build_object('decision_id',gen_random_uuid(),'replay_identity',repeat('b',64)))).* FROM screener_technical_candidate d;
            """);
        var later = await f.Fingerprint(); Assert.Equal(OutcomeVerificationState.Match, (await f.Verify()).State); Assert.Equal(later, await f.Fingerprint());
        Assert.Equal(404, (await Assert.ThrowsAsync<ScreenerException>(() => f.Service.VerifyAsync(f.Enrollment.EnrollmentId, 5, Token))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<ScreenerException>(() => f.Service.VerifyAsync(Guid.NewGuid(), 1, Token))).StatusCode);
    }
    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task CompleteProjectionMismatchDiffersAndHashOrIdentityCorruptionFailsClosed()
    {
        await using var f = await Fixture.Create(); var p = f.Observation.Projection with { PriceReturnPct = f.Observation.Projection.PriceReturnPct + 1m };
        await using (var cmd = new NpgsqlCommand("UPDATE outcome_v02_observation SET projection=$1,result_hash=$2", f.Connection))
        { cmd.Parameters.AddWithValue(JsonSerializer.Serialize(p, DecisionSnapshotService.JsonOptions)); cmd.Parameters.AddWithValue(ScreenerReferences.Hash(p)); await cmd.ExecuteNonQueryAsync(Token); }
        var before = await f.Fingerprint(); var result = await f.Verify(); Assert.Equal(OutcomeVerificationState.DifferentResult, result.State);
        Assert.Contains(result.Differences, d => d.Field == "priceReturnPct"); Assert.Equal(before, await f.Fingerprint());
        await Sql(f.Connection, "UPDATE outcome_v02_observation SET result_hash=repeat('b',64)");
        Assert.Equal(OutcomeVerificationState.InputNotAvailable, (await f.Verify()).State);
        await Sql(f.Connection, "UPDATE outcome_v02_observation SET outcome_policy_id='unknown',projection='malformed'");
        Assert.Equal(OutcomeVerificationState.PolicyVersionUnavailable, (await f.Verify()).State);
        await Sql(f.Connection, "UPDATE outcome_v02_observation SET outcome_policy_id='outcome-v0.2.0',schema_version=2");
        Assert.Equal(OutcomeVerificationState.PolicyVersionUnavailable, (await f.Verify()).State);
    }
    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ExactSourceAndArchiveFailuresNeverBorrowCompatibleReplacements()
    {
        await using var f = await Fixture.Create(); Assert.Equal(OutcomeVerificationState.Match, (await f.Verify()).State);
        await Sql(f.Connection, "CREATE TABLE saved_candidate AS TABLE screener_technical_candidate; CREATE TABLE saved_capture AS TABLE screener_technical_capture; CREATE TABLE saved_enrollment AS TABLE outcome_v02_enrollment; CREATE TABLE saved_evidence AS TABLE screener_evidence_record;");
        foreach (var sql in new[] { "DELETE FROM screener_technical_candidate", "UPDATE screener_technical_candidate SET replay_identity=repeat('b',64)",
            "UPDATE screener_technical_candidate SET projection=jsonb_set(projection::jsonb,'{candidate}','false')::text",
            "DELETE FROM screener_technical_capture", "UPDATE screener_technical_capture SET result_hash=repeat('b',64)", "DELETE FROM outcome_v02_enrollment",
            "UPDATE outcome_v02_enrollment SET binding_hash=repeat('b',64)", "DELETE FROM screener_evidence_record" })
        {
            await Sql(f.Connection, sql); var before = await f.Fingerprint(); Assert.Equal(OutcomeVerificationState.InputNotAvailable, (await f.Verify()).State);
            Assert.Equal(before, await f.Fingerprint());
            await Sql(f.Connection, "DELETE FROM screener_technical_candidate; INSERT INTO screener_technical_candidate TABLE saved_candidate; DELETE FROM screener_technical_capture; INSERT INTO screener_technical_capture TABLE saved_capture; DELETE FROM outcome_v02_enrollment; INSERT INTO outcome_v02_enrollment TABLE saved_enrollment; DELETE FROM screener_evidence_record; INSERT INTO screener_evidence_record TABLE saved_evidence;");
        }
        await Sql(f.Connection, "UPDATE screener_technical_candidate SET candidate_policy_id='unknown'; DELETE FROM screener_evidence_record;");
        Assert.Equal(OutcomeVerificationState.PolicyVersionUnavailable, (await f.Verify()).State);
        await Sql(f.Connection, "DELETE FROM screener_technical_candidate; INSERT INTO screener_technical_candidate TABLE saved_candidate; INSERT INTO screener_evidence_record TABLE saved_evidence;");
        await File.WriteAllBytesAsync(f.Archive, "different raw"u8.ToArray(), Token);
        Assert.Equal(OutcomeVerificationState.InputNotAvailable, (await f.Verify()).State);
        File.Delete(f.Archive); Assert.Equal(OutcomeVerificationState.InputNotAvailable, (await f.Verify()).State);
    }
}
