using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class DecisionVerificationTests
{
    private static readonly Guid Stock = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid Index = Guid.Parse("76237e96-232f-5085-9b13-dcb7104222bc");
    private static readonly DateOnly Day = new(2026, 9, 15);
    private static readonly DateTimeOffset Known = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static DecisionSnapshotRun Run()
    {
        var u = new UniverseSnapshot("fixture", Known, "PILOT", [Stock], Index, [], "");
        var refs = new SelectedScreenerReferences([u with { ContentHash = ScreenerReferences.SnapshotHash(u) }], [], [], []);
        var request = new ScreenerReadRequest([Stock], Index, ScreenerReadRequest.Anchor, Day, Known);
        var evaluated = ScreenerService.Evaluate(request, new([], []), refs, null, default);
        var q = new ScreenerQuery(Day, Known, null, "all", "ALL", "ALL", 0, 100, null);
        var dto = ScreenerPresentation.Map(evaluated.Result, q, evaluated.InputHash, refs, default);
        var result = evaluated.Result;
        return new(new(Guid.NewGuid(), Guid.NewGuid(), 1, "PROSPECTIVE_CAPTURE", Known, Known, Known, Day,
            result.TargetSession, request.HistoryAnchor, ScreenerReadRequest.PolicyId, "PILOT", result.UniverseSnapshotId,
            null, evaluated.InputHash, evaluated.SelectedDigest, dto.Status, result.Rows.Count),
            new(result.Reasons, result.Summary, result.RankedCandidateIds, result.AllViewIds, result.ShortlistIds, result.HeldIds, dto.MarketContext),
            result.Rows.Select(r => DecisionSnapshotProjection.Row(ScreenerPresentation.Row(r, refs, Known), false, null, null)).ToArray());
    }
    private static DecisionVerificationResult Compare(DecisionSnapshotRun stored, DecisionSnapshotRun replay)
        => DecisionVerification.Compare(stored, replay.Result, replay.Rows, replay.Header.Status, replay.Header.TargetSession,
            replay.Header.UniverseSnapshotId, replay.Header.InputHash, replay.Header.SelectedDigest);

    [Fact]
    public void TypedRoundTripReproducesBlockedCaptureAndBothHashes()
    {
        var original = Run();
        var stored = JsonSerializer.Deserialize<DecisionSnapshotRun>(JsonSerializer.Serialize(original, DecisionSnapshotService.JsonOptions), DecisionSnapshotService.JsonOptions)!;
        var before = DateTimeOffset.UtcNow; var verified = Compare(stored, original);
        Assert.Equal(DecisionVerificationState.Match, verified.State); Assert.Empty(verified.Differences);
        Assert.Equal(stored.Header.InputHash, verified.RecomputedInputHash); Assert.Equal(stored.Header.SelectedDigest, verified.RecomputedSelectedDigest);
        Assert.True(verified.VerifiedAt >= before); Assert.Equal(Known, verified.CapturedAt);
    }

    [Theory]
    [InlineData("screener-v0.1.0", 1, true)]
    [InlineData("screener-v0.1.1", 1, false)]
    [InlineData("arbitrary", 1, false)]
    [InlineData("screener-v0.1.0", 2, false)]
    public void PolicyAndProjectionAreExplicitlyAllowlisted(string policy, int schema, bool available)
        => Assert.Equal(available, DecisionVerificationService.ResolvePolicy(Run().Header with { PolicyId = policy, SchemaVersion = schema }) is not null);

    [Fact]
    public void DecimalScaleUtcOffsetsAndDictionaryInsertionOrderAreNotDifferences()
    {
        var run = Run(); var row = run.Rows[0] with { Close = 100.0m, Result = run.Rows[0].Result with
            { Atr14 = 0.0m, FieldStates = new Dictionary<string, ScreenerFieldDto> { ["close"] = new("AVAILABLE", null), ["ema50"] = new("WARMUP", "gap") },
                Provenance = run.Rows[0].Result.Provenance with { KnownAt = Known } } };
        var stored = run with { Rows = [row] };
        var replay = stored with { Rows = [row with { Close = 100.00m, Result = row.Result with { Atr14 = 0m,
            FieldStates = new Dictionary<string, ScreenerFieldDto> { ["ema50"] = new("WARMUP", "gap"), ["close"] = new("AVAILABLE", null) },
            Provenance = row.Result.Provenance with { KnownAt = Known.ToOffset(TimeSpan.FromHours(7)) } } }] };
        Assert.Equal(DecisionVerificationState.Match, Compare(stored, replay).State);
    }

    [Fact]
    public void HashesAndEveryTypedAnalyticalFieldAreComparedWithoutMutation()
    {
        var stored = Run(); var before = JsonSerializer.Serialize(stored);
        var row = stored.Rows[0];
        var replay = stored with { Header = stored.Header with { InputHash = new('a', 64), SelectedDigest = new('b', 64), Status = "PARTIAL" },
            Rows = [row with { Eligibility = "ELIGIBLE", Setup = "WATCH", SetupEvaluated = true, Close = 0,
                Result = row.Result with { Rs20Pp = 0, Episode = new("episode", Day, null, null, null, 1, 0, 100, 99) } }] };
        var result = Compare(stored, replay);
        Assert.Equal(DecisionVerificationState.DifferentResult, result.State);
        foreach (var field in new[] { "inputHash", "selectedDigest", "status", ".eligibility", ".setup", ".setupEvaluated", ".close", ".result.rs20Pp", ".result.episode" })
            Assert.Contains(result.Differences, d => d.Field.Contains(field, StringComparison.Ordinal));
        Assert.Equal(before, JsonSerializer.Serialize(stored));
    }

    [Fact]
    public void OrderedArraysAndCompletePopulationMatterEvenWhenHashesMatch()
    {
        var stored = Run(); var second = stored.Rows[0] with { InstrumentId = Index };
        stored = stored with { Rows = [stored.Rows[0], second], Result = stored.Result with { AllViewIds = [Stock, Index], Reasons = ["one", "two"] } };
        var replay = stored with { Rows = [second, stored.Rows[0]], Result = stored.Result with { AllViewIds = [Index, Stock], Reasons = ["two", "one"] } };
        var result = Compare(stored, replay);
        Assert.Contains(result.Differences, d => d.Field.StartsWith("population", StringComparison.Ordinal));
        Assert.Contains(result.Differences, d => d.Field.StartsWith("result.allViewIds", StringComparison.Ordinal));
        Assert.Contains(result.Differences, d => d.Field.StartsWith("result.reasons", StringComparison.Ordinal));
        var missing = Compare(stored, replay with { Rows = [second] });
        Assert.Contains(missing.Differences, d => d.Field == "rows." + Stock && d.Recomputed == "missing in replay");
    }

    [Fact]
    public void DiagnosticsAreBoundedAndTruncationIsExplicit()
    {
        var stored = Run(); stored = stored with { Result = stored.Result with { Reasons = Enumerable.Range(0, 150).Select(n => "a" + n).ToArray() } };
        var replay = stored with { Result = stored.Result with { Reasons = Enumerable.Range(0, 150).Select(n => new string('b', 300) + n).ToArray() } };
        var result = Compare(stored, replay);
        Assert.Equal(100, result.Differences.Count); Assert.True(result.DifferencesTruncated);
        Assert.All(result.Differences, d => Assert.True(d.Recomputed!.Length <= 161));
    }

    [Fact]
    public void CancellationStopsComparison()
    {
        var run = Run(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => DecisionVerification.Compare(run, run.Result, run.Rows, run.Header.Status,
            run.Header.TargetSession, run.Header.UniverseSnapshotId, run.Header.InputHash, run.Header.SelectedDigest, cancel.Token));
    }

    [Theory]
    [InlineData("VALID")][InlineData("REJECTED")]
    public void ContentAuthenticationIsIndependentOfAdmissionQuality(string quality)
    {
        var source = new SourceReference("synthetic", Stock, Known, Known, new('a', 64));
        var daily = new DailyBar(new(Stock), Day, 100, 101, 99, 100, 0, source, null, "UNKNOWN", "UNKNOWN", "UNKNOWN");
        var bar = new ScreenerBarEvidence(Stock, Day, 1, Known, PilotValidation.ContentHash(daily), Stock, Stock, "synthetic", new('a', 64),
            Known, Known, "https://reference.example/session", Known, quality, "100.00", "101", "99", "100", 0, null, "UNKNOWN", "UNKNOWN", "UNKNOWN");
        Assert.True(bar.ContentHashMatches()); Assert.False((bar with { Close = "100.1" }).ContentHashMatches());
        Assert.False((bar with { Close = "100000000000000000000000000000000" }).ContentHashMatches());
        if (quality == "REJECTED") Assert.False(bar.Validate().Available);
    }

    public static bool DatabaseConfigured => ScreenerDatabaseTests.DatabaseConfigured;
    [Fact(Skip = "Opt-in owned PostgreSQL verification test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ExactReaderPreservesRetainedRevisionAndExplicitAbsence()
    {
        var config = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDX_SCREENER_TEST_CONNECTION"));
        Assert.Equal("127.0.0.1", config.Host); Assert.StartsWith("idx_screener_test_", config.Database!);
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(config.ConnectionString); await connection.OpenAsync(ct);
        await using (var create = new NpgsqlCommand("""
            CREATE TEMP TABLE raw_artifact (LIKE public.raw_artifact INCLUDING ALL);
            CREATE TEMP TABLE daily_bar_revision (LIKE public.daily_bar_revision INCLUDING ALL);
            CREATE TEMP TABLE instrument_listing_evidence (LIKE public.instrument_listing_evidence INCLUDING ALL);
            INSERT INTO raw_artifact VALUES ('10000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001',
                'synthetic','fixture:only','{}','2026-09-30T12:00:00Z','fixture:only',repeat('a',64),0,'synthetic');
            INSERT INTO daily_bar_revision(instrument_id,session_date,revision_number,known_at,ingestion_run_id,raw_artifact_id,
                canonical_content_sha256,open,high,low,close,volume,quality_status)
                VALUES ('10000000-0000-4000-8000-000000000001','2026-09-15',1,'2026-09-30T12:00:00Z',
                '10000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001',repeat('b',64),100,100,100,100,100,'REJECTED'),
                ('10000000-0000-4000-8000-000000000001','2026-09-15',2,'2026-09-30T12:00:00Z',
                '10000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001',repeat('c',64),200,200,200,200,100,'VALID');
            """, connection)) await create.ExecuteNonQueryAsync(ct);
        var request = new ScreenerReadRequest([Stock], Index, ScreenerReadRequest.Anchor, Day, Known);
        var manifest = new DecisionSnapshotManifest(1, request, [Stock], [], [Stock],
            [new(Stock, Day, 1, Known, new('b', 64), Stock)], [], [], [], [], [], null, []);
        var retained = await ScreenerEvidenceDatabase.ReadAsync(connection, null, request, ct, manifest);
        Assert.Equal(1, Assert.Single(retained.Value!.Bars).RevisionNumber); Assert.Equal("REJECTED", retained.Value.Bars[0].CanonicalQuality);
        var empty = await ScreenerEvidenceDatabase.ReadAsync(connection, null, request, ct, manifest with { Bars = [] });
        Assert.Empty(empty.Value!.Bars); // Today's rows do not fill retained absence.
    }
}
