using System.Data;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceV02ReadinessDatabaseTests
{
    public static bool DatabaseConfigured => ScreenerEvidenceV02DatabaseTests.DatabaseConfigured;
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    internal static async Task Sql(NpgsqlConnection c, string sql)
    {
        await using var command = new NpgsqlCommand(sql, c) { CommandTimeout = 15 };
        await command.ExecuteNonQueryAsync(Token);
    }
    internal static async Task Populate(NpgsqlConnection c, ReadinessFixture fixture)
    {
        await Sql(c, "CREATE TEMP TABLE raw_artifact (LIKE public.raw_artifact INCLUDING ALL);");
        await using (var raw = new NpgsqlCommand("""
            INSERT INTO raw_artifact(raw_artifact_id,ingestion_run_id,source_id,original_uri,request_parameters,fetched_at,
                local_uri,content_sha256,byte_length,parser_version)
            VALUES ($1,$2,'source-a','synthetic','{}',$3,'synthetic',repeat('a',64),100,'synthetic');
            """, c))
        {
            raw.Parameters.AddWithValue(EvidenceBindingFixture.Reference); raw.Parameters.AddWithValue(Guid.NewGuid());
            raw.Parameters.AddWithValue(EvidenceBindingFixture.Known.AddHours(-1)); await raw.ExecuteNonQueryAsync(Token);
        }
        // Exact premises precede referencing rows, including event coverage.
        foreach (var row in fixture.Rows.OrderBy(r => r.Claim == EvidenceClaim.GenuinePriceObservation ? 2
            : ScreenerEvidenceBinding.Decode(r).Value is ActionCoverageValue or ReopeningValue ? 1 : 0))
            await ScreenerEvidenceV02Store.AppendAsync(c, null, row, Token);
    }
    internal static async Task<string> Fingerprint(NpgsqlConnection c)
    {
        await using var command = new NpgsqlCommand("SELECT md5(string_agg(to_jsonb(t)::text,',' ORDER BY evidence_id)) FROM screener_evidence_record t;", c);
        return (string)(await command.ExecuteScalarAsync(Token))!;
    }
    private static string Stable(ScreenerEvidenceReadinessResult result) => JsonSerializer.Serialize(result);

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("eligible")]
    [InlineData("history")]
    [InlineData("suspended")]
    [InlineData("reopen-early")]
    [InlineData("reopen-later")]
    [InlineData("board-conflict")]
    [InlineData("unbound")]
    [InlineData("synthetic")]
    [InlineData("partial-coverage")]
    [InlineData("split-warmup")]
    [InlineData("split-ready")]
    [InlineData("benchmark-missing")]
    [InlineData("repeat")]
    [InlineData("unsupported")]
    [InlineData("aggregate-overflow")]
    public async Task StoredEvidenceComposesReadinessWithoutWrites(string scenario)
    {
        var f = new ReadinessFixture(scenario is "eligible" or "suspended" or "reopen-early" or "reopen-later" or "board-conflict" or "unbound" or "unsupported" or "aggregate-overflow" ? 1
            : scenario.StartsWith("split", StringComparison.Ordinal) ? 61 : 50);
        var request = f.Request;
        if (scenario is "suspended" or "reopen-early" or "reopen-later")
        {
            f.Rows.RemoveAll(r => r.Claim == EvidenceClaim.TradingStatus);
            var s = f.Add(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice"), request.EvaluationDate.AddDays(-1));
            if (scenario.StartsWith("reopen", StringComparison.Ordinal))
            {
                f.Add(EvidenceClaim.Reopening, new ReopeningValue("reopen", EvidenceStatus.TRADING, request.EvaluationDate, s.EvidenceId),
                    request.EvaluationDate, known: request.Cutoff.AddDays(1));
                if (scenario == "reopen-later") request = request with { Cutoff = request.Cutoff.AddDays(1) };
            }
        }
        if (scenario == "board-conflict") f.Add(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.DEVELOPMENT));
        if (scenario == "unsupported") f.Replace(EvidenceClaim.SecurityType, _ => new SecurityTypeValue(EvidenceSecurityType.OTHER));
        if (scenario == "unbound")
        {
            var board = f.Rows.Single(r => r.Claim == EvidenceClaim.BoardRegime); f.Rows.Remove(board);
        }
        if (scenario == "synthetic") f.Replace(EvidenceClaim.GenuinePriceObservation, v => ((PriceValue)v) with { SyntheticOrCarryForward = EvidenceMarker.YES });
        if (scenario == "partial-coverage") f.Replace(EvidenceClaim.CorporateAction, v => ((ActionCoverageValue)v) with { Coverage = EvidenceCoverage.PARTIAL });
        if (scenario.StartsWith("split", StringComparison.Ordinal))
        {
            var day = f.Dates[scenario == "split-ready" ? 10 : 40];
            var action = f.Add(EvidenceClaim.CorporateAction, new ActionEventValue(EvidenceActionValueKind.EVENT, "split", EvidenceActionType.SPLIT, day, null, null), day);
            f.Replace(EvidenceClaim.CorporateAction, v => v is ActionCoverageValue coverage ? coverage with { EventEvidenceIds = [action.EvidenceId] } : v);
        }
        if (scenario == "benchmark-missing") request = request with { Benchmark = new(Guid.NewGuid(), request.PriceField) };
        if (scenario == "aggregate-overflow")
        {
            for (var i = 0; i < 300; i++) f.Add(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.MAIN));
            for (var i = 0; i < 240; i++) f.Add(EvidenceClaim.Currency, new CurrencyValue("IDR"));
        }
        await using var c = await ScreenerEvidenceV02DatabaseTests.Fixture(Token);
        await Populate(c, f);
        if (scenario == "unbound") await ScreenerEvidenceV02Store.AppendMechanicalAsync(c, null, EvidenceBindingFixture.Envelope(EvidenceClaim.BoardRegime), Token);
        var before = await Fingerprint(c);
        var result = await ScreenerEvidenceReadinessService.EvaluateAsync(c, null, request, Token);
        Assert.Equal(before, await Fingerprint(c));
        var market = scenario is "suspended" or "reopen-early" or "unsupported" ? EligibilityStatus.Ineligible
            : scenario is "board-conflict" or "unbound" or "aggregate-overflow" ? EligibilityStatus.DataBlocked : EligibilityStatus.Eligible;
        Assert.Equal(market, result.MarketEligibility.Status);
        Assert.Equal(scenario is "history" or "split-ready" or "benchmark-missing" or "repeat", result.DataReady);
        Assert.Equal(result.DataReady, result.CanEvaluateSetup);
        Assert.All(result.CoreFeatures.Values, v => Assert.Null(v.Value));
        if (scenario == "synthetic") { Assert.Empty(result.Bars); Assert.Contains(ScreenerEvidenceReasons.PriceSyntheticCarryForward, result.Diagnostics); }
        if (scenario == "partial-coverage") Assert.Equal(PriceComparability.Unresolved, result.PriceComparability.State);
        if (scenario.StartsWith("split", StringComparison.Ordinal))
        {
            Assert.Equal(PriceComparability.KnownBreak, result.HistoryComparability.State);
            Assert.Equal(scenario == "split-ready" ? Availability.AVAILABLE : Availability.WARMUP, result.CoreFeatures[TechnicalFeature.Ema50].Availability);
            Assert.Equal(scenario == "split-ready" ? 50 : 20, result.ActiveBars.Count);
        }
        if (scenario == "benchmark-missing") Assert.Equal(Availability.UNAVAILABLE, result.OptionalFeatures[OptionalFeature.Rs20].Availability);
        if (scenario == "aggregate-overflow") Assert.Equal(ScreenerEvidenceAsOf.BoundExceeded, result.FailureReason);
        if (scenario == "repeat") Assert.Equal(Stable(result), Stable(await ScreenerEvidenceReadinessService.EvaluateAsync(c, null, request, Token)));
        if (scenario == "reopen-later")
        {
            var earlier = await ScreenerEvidenceReadinessService.EvaluateAsync(c, null, f.Request, Token);
            Assert.Equal(EligibilityStatus.Ineligible, earlier.MarketEligibility.Status);
        }
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task SharedSnapshotExcludesConcurrentAppendAcrossAllClaimsAndDoesNotBlockWriter()
    {
        await using var c = ScreenerEvidenceV02DatabaseTests.OwnedConnection(); await c.OpenAsync(Token);
        await using var writer = ScreenerEvidenceV02DatabaseTests.OwnedConnection(); await writer.OpenAsync(Token);
        var schema = "readiness_test_" + Guid.NewGuid().ToString("N");
        await Sql(c, "CREATE SCHEMA " + schema + "; CREATE TABLE " + schema + ".screener_evidence_record (LIKE public.screener_evidence_record INCLUDING ALL); SET search_path TO " + schema + ",public;");
        try
        {
            await Sql(writer, "SET search_path TO " + schema + ",public;");
            var f = new ReadinessFixture(1); await Populate(c, f);
            var request = f.Request with { Cutoff = f.Request.Cutoff.AddDays(1) };
            await using (var t = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, Token))
            {
                await using (var ro = new NpgsqlCommand("SET TRANSACTION READ ONLY", c, t)) await ro.ExecuteNonQueryAsync(Token);
                var before = await ScreenerEvidenceReadinessService.EvaluateAsync(c, t, request, Token);
                Assert.Equal(EligibilityStatus.Eligible, before.MarketEligibility.Status);
                var appended = AsOfFixture.Row(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.DEVELOPMENT), series: "concurrent", known: request.Cutoff);
                await ScreenerEvidenceV02Store.AppendAsync(writer, null, appended, Token);
                var after = await ScreenerEvidenceReadinessService.EvaluateAsync(c, t, request, Token);
                Assert.Equal(Stable(before), Stable(after));
                await t.CommitAsync(Token);
            }
            Assert.Equal(EligibilityStatus.DataBlocked, (await ScreenerEvidenceReadinessService.EvaluateAsync(c, null, request, Token)).MarketEligibility.Status);
        }
        finally { await Sql(c, "SET search_path TO public; DROP SCHEMA " + schema + " CASCADE;"); }
    }
}
