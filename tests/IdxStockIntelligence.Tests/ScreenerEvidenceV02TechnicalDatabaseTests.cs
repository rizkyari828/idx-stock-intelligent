using System.Data;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceV02TechnicalDatabaseTests
{
    public static bool DatabaseConfigured => ScreenerEvidenceV02DatabaseTests.DatabaseConfigured;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("ready")]
    [InlineData("warmup")]
    [InlineData("synthetic")]
    [InlineData("split-warmup")]
    [InlineData("split-ready")]
    [InlineData("benchmark")]
    [InlineData("benchmark-missing")]
    [InlineData("genuine-zero")]
    [InlineData("unknown-quantity")]
    [InlineData("repeat")]
    [InlineData("later-cutoff")]
    [InlineData("historical-exclusion")]
    public async Task RetainedEvidenceExecutesWithoutWritesOrV01AdmissionChanges(string scenario)
    {
        var f = new TechnicalFixture(scenario == "warmup" ? 19 : 61, scenario != "unknown-quantity", scenario == "genuine-zero");
        if (scenario == "synthetic") f.Evidence.Replace(EvidenceClaim.GenuinePriceObservation,
            v => ((PriceValue)v) with { SyntheticOrCarryForward = EvidenceMarker.YES });
        if (scenario.StartsWith("split", StringComparison.Ordinal)) f.Split(scenario == "split-ready" ? 50 : 20);
        if (scenario == "benchmark") f.Request = f.Evidence.WithBenchmark(61);
        if (scenario == "benchmark-missing") f.Request = f.Request with { Benchmark = new(Guid.NewGuid(), f.Request.PriceField) };
        if (scenario == "historical-exclusion")
        {
            var date = f.Evidence.Dates[^2];
            f.Evidence.Rows.RemoveAll(r => r.Claim == EvidenceClaim.TradingStatus && r.EffectiveFrom == date);
            f.Evidence.Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.SUSPENDED, f.Request.SessionId), date);
        }
        await using var c = await ScreenerEvidenceV02DatabaseTests.Fixture(Token);
        await ScreenerEvidenceV02ReadinessDatabaseTests.Populate(c, f.Evidence);
        if (scenario == "later-cutoff") await AppendCorrection(c, f);
        var before = await ScreenerEvidenceV02ReadinessDatabaseTests.Fingerprint(c);
        var actual = await ScreenerEvidenceTechnicalService.EvaluateAsync(c, null, f.Request, Token);
        var expected = f.Execute();
        Assert.Equal(expected.TechnicalEvaluated, actual.TechnicalEvaluated);
        Assert.Equal(TechnicalFixture.Stable(expected.Fields), TechnicalFixture.Stable(actual.Fields));
        Assert.Equal(TechnicalFixture.Stable(expected.Setup), TechnicalFixture.Stable(actual.Setup));
        Assert.Equal(scenario is not ("warmup" or "synthetic" or "split-warmup"), actual.TechnicalEvaluated);
        if (actual.TechnicalEvaluated)
        {
            Assert.Equal(scenario == "split-ready" ? 50 : 61, actual.StockBindings.Count);
            Assert.Equal(scenario == "benchmark" ? Availability.AVAILABLE : Availability.UNAVAILABLE, actual.Fields["rs20Pp"].Availability);
        }
        if (scenario == "genuine-zero")
        {
            Assert.Equal(0m, actual.Fields["monetaryLiquidity20Idr"].Value);
            Assert.NotEqual(Availability.AVAILABLE, actual.Fields["volumeRatio20"].Availability);
            var legacy = TechnicalFixture.Legacy(f.Input().Current);
            Assert.Equal("VOLUME_BASIS_UNVERIFIED", legacy["monetaryLiquidity20Idr"].UnavailableReason);
        }
        if (scenario == "unknown-quantity") Assert.Null(actual.Fields["monetaryLiquidity20Idr"].Value);
        if (scenario == "repeat") Assert.Equal(TechnicalFixture.Stable(actual), TechnicalFixture.Stable(
            await ScreenerEvidenceTechnicalService.EvaluateAsync(c, null, f.Request, Token)));
        if (scenario == "later-cutoff")
        {
            var later = await ScreenerEvidenceTechnicalService.EvaluateAsync(c, null, f.Request with { Cutoff = f.Request.Cutoff.AddDays(1) }, Token);
            Assert.True(later.TechnicalEvaluated);
            Assert.NotEqual(actual.Fields["ema20"].Value, later.Fields["ema20"].Value);
            Assert.NotEqual(actual.ReplayIdentity, later.ReplayIdentity);
            Assert.Equal(TechnicalFixture.Stable(actual), TechnicalFixture.Stable(
                await ScreenerEvidenceTechnicalService.EvaluateAsync(c, null, f.Request, Token)));
        }
        Assert.Equal(before, await ScreenerEvidenceV02ReadinessDatabaseTests.Fingerprint(c));
    }

    private static Task<EvidenceWriteResult> AppendCorrection(NpgsqlConnection c, TechnicalFixture f)
    {
        var original = f.Evidence.Rows.Single(r => r.Claim == EvidenceClaim.GenuinePriceObservation && r.EffectiveFrom == f.Request.EvaluationDate);
        var price = (PriceValue)ScreenerEvidenceBinding.Decode(original).Value!;
        var bar = price.Bar with { High = new(500m), Close = new(400m) };
        price = price with { Bar = bar, BarRevision = price.BarRevision + 1, BarContentHash = ScreenerEvidenceBinding.BarHash(bar) };
        var correction = new ScreenerEvidenceRecord(Guid.NewGuid(), original.SubjectId, original.Claim, original.PolicyId,
            original.SchemaVersion, original.RevisionSeriesId, original.RevisionNumber + 1, original.RevisionNumber,
            original.AuthorityTier, original.EffectiveFrom, original.EffectiveTo, original.EffectiveAt, original.PublishedAt,
            original.RetrievedAt, f.Request.Cutoff.AddDays(1), original.SourceId, original.SourceReference,
            original.RawArtifactId, original.Payload, original.EvidenceClass, original.PayloadSchemaVersion,
            original.ScopeKind, original.ScopeExchangeId);
        correction = ScreenerEvidenceBinding.Bind(correction, correction.EvidenceClass!.Value, correction.ScopeKind!.Value,
            correction.ScopeExchangeId!.Value, new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, price));
        return ScreenerEvidenceV02Store.AppendAsync(c, null, correction, Token);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ConcurrentAppendDoesNotChangeTechnicalExecutionInSharedReadOnlySnapshot()
    {
        await using var c = ScreenerEvidenceV02DatabaseTests.OwnedConnection(); await c.OpenAsync(Token);
        await using var writer = ScreenerEvidenceV02DatabaseTests.OwnedConnection(); await writer.OpenAsync(Token);
        var schema = "technical_test_" + Guid.NewGuid().ToString("N");
        await ScreenerEvidenceV02ReadinessDatabaseTests.Sql(c, "CREATE SCHEMA " + schema + "; CREATE TABLE " + schema
            + ".screener_evidence_record (LIKE public.screener_evidence_record INCLUDING ALL); SET search_path TO " + schema + ",public;");
        try
        {
            await ScreenerEvidenceV02ReadinessDatabaseTests.Sql(writer, "SET search_path TO " + schema + ",public;");
            var f = new TechnicalFixture(); await ScreenerEvidenceV02ReadinessDatabaseTests.Populate(c, f.Evidence);
            var request = f.Request with { Cutoff = f.Request.Cutoff.AddDays(1) };
            await using (var t = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, Token))
            {
                await using (var ro = new NpgsqlCommand("SET TRANSACTION READ ONLY", c, t) { CommandTimeout = 15 }) await ro.ExecuteNonQueryAsync(Token);
                var before = await ScreenerEvidenceTechnicalService.EvaluateAsync(c, t, request, Token);
                Assert.True(before.TechnicalEvaluated);
                await ScreenerEvidenceV02Store.AppendAsync(writer, null, AsOfFixture.Row(EvidenceClaim.BoardRegime,
                    new BoardValue(EvidenceBoard.DEVELOPMENT), series: "concurrent-technical", known: request.Cutoff), Token);
                var after = await ScreenerEvidenceTechnicalService.EvaluateAsync(c, t, request, Token);
                Assert.Equal(TechnicalFixture.Stable(before), TechnicalFixture.Stable(after));
                await t.CommitAsync(Token);
                var newer = await ScreenerEvidenceTechnicalService.EvaluateAsync(c, null, request, Token);
                Assert.False(newer.TechnicalEvaluated);
                Assert.Contains(ScreenerEvidenceReasons.EvidenceConflict, newer.Reasons);
            }
        }
        finally { await ScreenerEvidenceV02ReadinessDatabaseTests.Sql(c, "SET search_path TO public; DROP SCHEMA " + schema + " CASCADE;"); }
    }
}
