using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceV02CandidateDatabaseTests
{
    public static bool DatabaseConfigured => ScreenerEvidenceV02DatabaseTests.DatabaseConfigured;
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Task Sql(NpgsqlConnection c, string sql) => ScreenerEvidenceV02ReadinessDatabaseTests.Sql(c, sql);
    private static async Task<NpgsqlConnection> Fixture(TechnicalFixture f)
    {
        var c = await ScreenerEvidenceV02CaptureDatabaseTests.Fixture(f);
        try
        {
            // Exercise real migration 0010 FK/triggers against real immutable captures in the owned disposable DB.
            await Sql(c, "DROP TABLE pg_temp.screener_technical_capture;");
            return c;
        }
        catch { await c.DisposeAsync(); throw; }
    }
    private static async Task<long> Count(NpgsqlConnection c)
    {
        await using var command = new NpgsqlCommand("SELECT count(*) FROM screener_technical_candidate", c) { CommandTimeout = 15 };
        return (long)(await command.ExecuteScalarAsync(Token))!;
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("candidate", true)]
    [InlineData("non-candidate", false)]
    [InlineData("zero", true)]
    [InlineData("unknown", true)]
    public async Task StoredCapturePromotesAndReadsExactDecisionWithoutChangingV01(string scenario, bool expected)
    {
        var f = ScreenerEvidenceV02CandidateTests.Boundary(scenario == "non-candidate" ? 150m : 161.01m,
            quantity: scenario != "unknown", zero: scenario == "zero");
        await using var c = await Fixture(f);
        var v01 = await ScreenerEvidenceV02CaptureDatabaseTests.V01Fingerprint(c);
        var evidence = await ScreenerEvidenceV02ReadinessDatabaseTests.Fingerprint(c);
        var capture = (await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token)).Capture;
        var first = await ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, capture.CaptureId, Token);
        Assert.True(first.Created); Assert.NotNull(first.Decision.RecordedAt); Assert.Equal(expected, first.Decision.Projection.Candidate);
        Assert.Equal(capture.CaptureId, first.Decision.Projection.SourceCaptureId);
        Assert.Equal(capture.ResultHash, first.Decision.Projection.SourceResultHash);
        Assert.Equal(ScreenerReferences.Hash(ScreenerEvidenceTechnicalCandidates.Promote(capture, Token).Projection), first.Decision.ReplayIdentity);
        var retry = await ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, capture.CaptureId, Token);
        Assert.False(retry.Created); Assert.Equal(ScreenerReferences.Hash(first.Decision), ScreenerReferences.Hash(retry.Decision));
        var read = await ScreenerEvidenceTechnicalCandidateStore.ReadAsync(c, null, first.Decision.DecisionId, Token);
        Assert.Equal(ScreenerReferences.Hash(first.Decision), ScreenerReferences.Hash(read!));
        Assert.Equal(evidence, await ScreenerEvidenceV02ReadinessDatabaseTests.Fingerprint(c));
        Assert.Equal(v01, await ScreenerEvidenceV02CaptureDatabaseTests.V01Fingerprint(c));
        Assert.Null(await ScreenerEvidenceTechnicalCandidateStore.ReadAsync(c, null, Guid.NewGuid(), Token));
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ReadinessAloneHasNoStoredCaptureAndCannotPromote()
    {
        var f = new TechnicalFixture(); await using var c = await Fixture(f);
        Assert.True(f.Input().Current.DataReady); var before = await Count(c);
        var error = await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, Guid.NewGuid(), Token));
        Assert.Equal("TECHNICAL_CANDIDATE_CAPTURE_NOT_FOUND", error.Reason); Assert.Equal(before, await Count(c));
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task LaterCaptureIsDistinctButLaterEvidenceCannotChangeOldDecisionOrRetry()
    {
        var f = ScreenerEvidenceV02CandidateTests.Boundary(161.01m); await using var c = await Fixture(f);
        var capture = (await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token)).Capture;
        var first = await ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, capture.CaptureId, Token);
        var later = (await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request with { Cutoff = f.Request.Cutoff.AddTicks(1) }, Token)).Capture;
        var second = await ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, later.CaptureId, Token);
        Assert.NotEqual(first.Decision.DecisionId, second.Decision.DecisionId); Assert.NotEqual(first.Decision.Projection.Cutoff, second.Decision.Projection.Cutoff);
        await ScreenerEvidenceV02Store.AppendAsync(c, null, AsOfFixture.Row(EvidenceClaim.BoardRegime,
            new BoardValue(EvidenceBoard.DEVELOPMENT), series: "later-candidate-board", known: f.Request.Cutoff.AddDays(1)), Token);
        // No current evidence table is available for reselection/recomputation; both retry and read still work.
        await Sql(c, "ALTER TABLE pg_temp.screener_evidence_record RENAME TO evidence_hidden_for_candidate;");
        var retry = await ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, capture.CaptureId, Token);
        Assert.False(retry.Created); Assert.Equal(ScreenerReferences.Hash(first.Decision), ScreenerReferences.Hash(retry.Decision));
        Assert.Equal(capture.ResultHash, (await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(c, null, capture.CaptureId, Token))!.ResultHash);
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("UPDATE screener_technical_candidate SET replay_identity=repeat('a',64) WHERE decision_id='")]
    [InlineData("DELETE FROM screener_technical_candidate WHERE decision_id='")]
    [InlineData("TRUNCATE screener_technical_candidate")]
    public async Task MigrationInstalledMutationGuardsRejectChanges(string sql)
    {
        var f = new TechnicalFixture(); await using var c = await Fixture(f);
        var capture = (await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token)).Capture;
        var decision = (await ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, capture.CaptureId, Token)).Decision;
        if (sql.EndsWith('\'')) sql += decision.DecisionId.ToString("D") + "'";
        await Assert.ThrowsAsync<PostgresException>(() => Sql(c, sql));
        Assert.Equal(decision.ReplayIdentity, (await ScreenerEvidenceTechnicalCandidateStore.ReadAsync(c, null, decision.DecisionId, Token))!.ReplayIdentity);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ExactCaptureForeignKeyAndRerunnableMigrationProtectV01()
    {
        var f = new TechnicalFixture(); await using var c = await Fixture(f);
        var capture = (await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token)).Capture;
        var decision = (await ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, capture.CaptureId, Token)).Decision;
        var missing = Guid.NewGuid(); var id = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<PostgresException>(() => Sql(c, $$"""
            INSERT INTO screener_technical_candidate(decision_id,capture_id,candidate_policy_id,schema_version,replay_identity,projection)
            SELECT '{{id:D}}','{{missing:D}}',candidate_policy_id,schema_version,replay_identity,
                jsonb_set(projection::jsonb,'{sourceCaptureId}',to_jsonb('{{missing:D}}'::text))::text
            FROM screener_technical_candidate WHERE decision_id='{{decision.DecisionId:D}}'
            """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        var before = await ScreenerEvidenceV02CaptureDatabaseTests.V01Fingerprint(c);
        var migration = await File.ReadAllTextAsync(Path.Combine(ScreenerEvidenceV02BindingDatabaseTests.Root(),
            "src/IdxStockIntelligence.Infrastructure/Migrations/0010_screener_technical_candidate_v02.sql"), Token);
        await Sql(c, migration); await Sql(c, migration);
        Assert.Equal(before, await ScreenerEvidenceV02CaptureDatabaseTests.V01Fingerprint(c));
        Assert.Equal(decision.ReplayIdentity, (await ScreenerEvidenceTechnicalCandidateStore.ReadAsync(c, null, decision.DecisionId, Token))!.ReplayIdentity);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ChangedImmutableIdentityIsAnExplicitConflictAndCorruptCaptureCannotPromote()
    {
        var f = new TechnicalFixture(); await using var c = await Fixture(f);
        var capture = (await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token)).Capture;
        // Disposable corruption fixture shadows production storage; no production mutation guards are disabled.
        await Sql(c, "CREATE TEMP TABLE screener_technical_candidate (LIKE public.screener_technical_candidate INCLUDING ALL);");
        var first = await ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, capture.CaptureId, Token);
        await Sql(c, "UPDATE screener_technical_candidate SET replay_identity=repeat('a',64);");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, capture.CaptureId, Token));
        Assert.Contains("integrity conflict", error.Message); Assert.Equal(1L, await Count(c));
        await Sql(c, $"""
            CREATE TEMP TABLE screener_technical_capture (LIKE public.screener_technical_capture INCLUDING ALL);
            INSERT INTO screener_technical_capture SELECT * FROM public.screener_technical_capture WHERE capture_id='{capture.CaptureId:D}';
            UPDATE screener_technical_capture SET result_hash=repeat('a',64);
            """);
        await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceTechnicalCandidateStore.PromoteAsync(c, capture.CaptureId, Token));
        Assert.Equal(1L, await Count(c)); Assert.NotNull(first.Decision);
    }
}
