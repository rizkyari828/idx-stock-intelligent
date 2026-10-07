using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceV02CaptureDatabaseTests
{
    public static bool DatabaseConfigured => ScreenerEvidenceV02DatabaseTests.DatabaseConfigured;
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Task Sql(NpgsqlConnection c, string sql) => ScreenerEvidenceV02ReadinessDatabaseTests.Sql(c, sql);
    internal static async Task<NpgsqlConnection> Fixture(TechnicalFixture f)
    {
        var c = await ScreenerEvidenceV02DatabaseTests.Fixture(Token);
        try
        {
            await Sql(c, """
                CREATE TEMP TABLE screener_technical_capture (LIKE public.screener_technical_capture INCLUDING ALL);
                CREATE TRIGGER immutable_capture BEFORE UPDATE OR DELETE ON screener_technical_capture
                    FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
                CREATE TRIGGER immutable_capture_truncate BEFORE TRUNCATE ON screener_technical_capture
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
                """);
            await ScreenerEvidenceV02ReadinessDatabaseTests.Populate(c, f.Evidence);
            return c;
        }
        catch { await c.DisposeAsync(); throw; }
    }
    internal static async Task<string> V01Fingerprint(NpgsqlConnection c)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT md5(coalesce((SELECT string_agg(to_jsonb(t)::text,',' ORDER BY run_id) FROM decision_snapshot_run t),'')
                ||coalesce((SELECT string_agg(to_jsonb(t)::text,',' ORDER BY run_id,instrument_id) FROM decision_snapshot_row t),'')
                ||coalesce((SELECT string_agg(to_jsonb(t)::text,',' ORDER BY run_id,instrument_id,horizon_sessions) FROM decision_snapshot_outcome t),''))
            """, c) { CommandTimeout = 15 };
        return (string)(await cmd.ExecuteScalarAsync(Token))!;
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("ready")]
    [InlineData("benchmark")]
    [InlineData("genuine-zero")]
    [InlineData("unknown-quantity")]
    [InlineData("precise-cutoff")]
    [InlineData("optional-warmup")]
    [InlineData("historical-exclusion")]
    public async Task RetainedEvidenceExecutionCaptureAndReadbackMatchExactly(string scenario)
    {
        var f = new TechnicalFixture(scenario == "optional-warmup" ? 50 : 61,
            quantity: scenario != "unknown-quantity", zero: scenario == "genuine-zero");
        if (scenario is "benchmark" or "optional-warmup") f.Request = f.Evidence.WithBenchmark(scenario == "benchmark" ? 61 : 50);
        if (scenario == "precise-cutoff") f.Request = f.Request with { Cutoff = f.Request.Cutoff.AddTicks(7) };
        if (scenario == "historical-exclusion")
        {
            var date = f.Evidence.Dates[^2]; f.Evidence.Rows.RemoveAll(r => r.Claim == EvidenceClaim.TradingStatus && r.EffectiveFrom == date);
            f.Evidence.Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.SUSPENDED, f.Request.SessionId), date);
        }
        await using var c = await Fixture(f);
        var before = await ScreenerEvidenceV02ReadinessDatabaseTests.Fingerprint(c); var v01 = await V01Fingerprint(c);
        var executed = scenario == "ready" ? await ScreenerEvidenceTechnicalService.EvaluateAsync(c, null, f.Request, Token) : f.Execute();
        Assert.True(executed.TechnicalEvaluated);
        var write = await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token);
        Assert.True(write.Created); Assert.NotNull(write.Capture.RecordedAt);
        var stored = await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(c, null, write.Capture.CaptureId, Token);
        Assert.NotNull(stored); var r = stored.Projection.Result;
        Assert.Equal(write.Capture.ResultHash, stored.ResultHash); Assert.Equal(write.Capture.InputHash, stored.InputHash);
        Assert.Equal(f.Request.Cutoff, r.Request.Cutoff);
        Assert.Equal(ScreenerReferences.Hash(executed.Fields), ScreenerReferences.Hash(r.Fields));
        Assert.Equal(ScreenerReferences.Hash(executed.BenchmarkFields), ScreenerReferences.Hash(r.BenchmarkFields));
        Assert.Equal(ScreenerReferences.Hash(executed.Setup), ScreenerReferences.Hash(r.Setup));
        if (scenario == "ready") Assert.Equal(ScreenerReferences.Hash(executed), ScreenerReferences.Hash(r));
        Assert.Equal(executed.StockBindings.Select(b => b.Price), r.StockBindings.Select(b => b.Price));
        Assert.Equal(executed.StockBindings.Select(b => b.Evidence.EvidenceId), r.StockBindings.Select(b => b.Evidence.EvidenceId));
        Assert.Equal(executed.BenchmarkBindings.Select(b => b.Price), r.BenchmarkBindings.Select(b => b.Price));
        Assert.Null(r.Fields["actualTradedValue"].Value); Assert.NotNull(r.Fields["actualTradedValue"].UnavailableReason);
        if (scenario == "genuine-zero")
        { Assert.Equal(0m, r.Fields["monetaryLiquidity20Idr"].Value); Assert.Equal(Availability.AVAILABLE, r.Fields["monetaryLiquidity20Idr"].Availability); Assert.Null(r.Fields["volumeRatio20"].Value); }
        if (scenario == "unknown-quantity")
        { Assert.Null(r.Fields["monetaryLiquidity20Idr"].Value); Assert.Equal("VOLUME_BASIS_UNVERIFIED", r.Fields["monetaryLiquidity20Idr"].UnavailableReason); }
        if (scenario == "optional-warmup")
        { Assert.Equal(Availability.WARMUP, r.Fields["rs60Pp"].Availability); Assert.Null(r.Fields["rs60Pp"].Value); }
        Assert.Equal(before, await ScreenerEvidenceV02ReadinessDatabaseTests.Fingerprint(c)); Assert.Equal(v01, await V01Fingerprint(c));
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("readiness-only")]
    [InlineData("warmup")]
    public async Task IncompleteExecutionCannotWriteCapture(string scenario)
    {
        var f = new TechnicalFixture(scenario == "warmup" ? 19 : 61); await using var c = await Fixture(f);
        var input = f.Input();
        var ex = await Assert.ThrowsAsync<EvidenceBindingException>(async () =>
        {
            if (scenario == "readiness-only")
                await ScreenerEvidenceTechnicalCaptureStore.AppendAsync(c, ScreenerEvidenceTechnicalCapture.Create(input.Current, [], Token), Token);
            else await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token);
        });
        Assert.Equal("TECHNICAL_CAPTURE_NOT_EVALUATED", ex.Reason);
        await using var count = new NpgsqlCommand("SELECT count(*) FROM screener_technical_capture", c);
        Assert.Equal(0L, await count.ExecuteScalarAsync(Token));
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task IdenticalRetryNoOpsButSameIdentityChangedResultFailsAndLaterCutoffIsDistinct()
    {
        var f = new TechnicalFixture(); await using var c = await Fixture(f);
        var first = await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token);
        var reordered = first.Capture.Projection with { ReadinessHistory = first.Capture.Projection.ReadinessHistory.Reverse().ToArray(),
            Result = first.Capture.Projection.Result with { StockBindings = first.Capture.Projection.Result.StockBindings.Reverse().ToArray(),
                References = first.Capture.Projection.Result.References.Reverse().ToArray(),
                Fields = first.Capture.Projection.Result.Fields.Reverse().ToDictionary(x => x.Key, x => x.Value) } };
        var retry = await ScreenerEvidenceTechnicalCaptureStore.AppendAsync(c,
            first.Capture with { Projection = reordered, RecordedAt = DateTimeOffset.UtcNow.AddYears(1) }, Token);
        Assert.False(retry.Created); Assert.Equal(first.Capture.CaptureId, retry.Capture.CaptureId); Assert.Equal(first.Capture.RecordedAt, retry.Capture.RecordedAt);
        var r = first.Capture.Projection.Result; var fields = r.Fields.ToDictionary(x => x.Key, x => x.Value);
        fields["ema20"] = fields["ema20"] with { Value = fields["ema20"].Value + 1m };
        var changed = first.Capture.Projection with { Result = r with { Fields = fields } };
        var conflict = first.Capture with { Projection = changed, ResultHash = ScreenerReferences.Hash(changed) };
        Assert.Equal(first.Capture.InputHash, ScreenerEvidenceTechnicalCapture.InputHash(changed, Token));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ScreenerEvidenceTechnicalCaptureStore.AppendAsync(c, conflict, Token));
        Assert.Contains("integrity conflict", error.Message);
        var later = await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request with { Cutoff = f.Request.Cutoff.AddDays(1) }, Token);
        Assert.True(later.Created); Assert.NotEqual(first.Capture.CaptureId, later.Capture.CaptureId);
        Assert.Equal(first.Capture.ResultHash, (await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(c, null, first.Capture.CaptureId, Token))!.ResultHash);
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("UPDATE screener_technical_capture SET result_hash=repeat('a',64)")]
    [InlineData("DELETE FROM screener_technical_capture")]
    [InlineData("TRUNCATE screener_technical_capture")]
    public async Task CommittedCaptureRejectsMutation(string sql)
    {
        var f = new TechnicalFixture(); await using var c = await Fixture(f);
        // Exercise the triggers installed by migration 0009 itself, not only fixture copies.
        await Sql(c, "DROP TABLE pg_temp.screener_technical_capture;");
        var capture = (await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token)).Capture;
        await Assert.ThrowsAsync<PostgresException>(() => Sql(c, sql));
        Assert.Equal(capture.ResultHash, (await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(c, null, capture.CaptureId, Token))!.ResultHash);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ReadbackIgnoresLaterEvidenceAndDoesNotRequireOrRecomputeSourceEvidence()
    {
        var f = new TechnicalFixture(); await using var c = await Fixture(f);
        var first = await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token);
        await ScreenerEvidenceV02Store.AppendAsync(c, null, AsOfFixture.Row(EvidenceClaim.BoardRegime,
            new BoardValue(EvidenceBoard.DEVELOPMENT), series: "later-capture-board", known: f.Request.Cutoff.AddDays(1)), Token);
        // A missing source table would prevent readiness/technical execution. Stored output readback needs neither.
        await Sql(c, "ALTER TABLE pg_temp.screener_evidence_record RENAME TO evidence_hidden_for_capture_read;");
        var read = await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(c, null, first.Capture.CaptureId, Token);
        Assert.Equal(ScreenerReferences.Hash(first.Capture), ScreenerReferences.Hash(read!));
        Assert.Null(await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(c, null, Guid.NewGuid(), Token));
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ExactUnretainedReferencesCannotBePersisted()
    {
        await using var c = await Fixture(new TechnicalFixture());
        var missing = ScreenerEvidenceV02CaptureTests.Capture(new TechnicalFixture());
        var ex = await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceTechnicalCaptureStore.AppendAsync(c, missing, Token));
        Assert.Equal("TECHNICAL_CAPTURE_INPUT_UNAVAILABLE", ex.Reason);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task AdditiveMigrationRerunPreservesCapturesEvidenceAndV01Tables()
    {
        var f = new TechnicalFixture(); await using var c = await Fixture(f);
        var capture = (await ScreenerEvidenceTechnicalCaptureStore.CaptureAsync(c, f.Request, Token)).Capture;
        var v01 = await V01Fingerprint(c); var evidence = await ScreenerEvidenceV02ReadinessDatabaseTests.Fingerprint(c);
        var migration = await File.ReadAllTextAsync(Path.Combine(ScreenerEvidenceV02BindingDatabaseTests.Root(),
            "src/IdxStockIntelligence.Infrastructure/Migrations/0009_screener_technical_capture_v02.sql"), Token);
        await Sql(c, migration); await Sql(c, migration);
        Assert.Equal(v01, await V01Fingerprint(c)); Assert.Equal(evidence, await ScreenerEvidenceV02ReadinessDatabaseTests.Fingerprint(c));
        Assert.Equal(capture.ResultHash, (await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(c, null, capture.CaptureId, Token))!.ResultHash);
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM pg_trigger WHERE tgrelid='public.screener_technical_capture'::regclass AND NOT tgisinternal", c);
        Assert.Equal(2L, await cmd.ExecuteScalarAsync(Token));
    }
}
