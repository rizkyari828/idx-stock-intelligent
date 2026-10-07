using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

internal sealed class TechnicalFixture
{
    internal ReadinessFixture Evidence { get; }
    internal ScreenerReadinessRequest Request { get; set; }
    internal TechnicalFixture(int count = 61, bool quantity = true, bool zero = false)
    {
        Evidence = new(count); Request = Evidence.Request;
        foreach (var day in Evidence.Dates.SkipLast(1))
            Evidence.Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.TRADING, Request.SessionId), day);
        if (zero)
        {
            Evidence.Replace(EvidenceClaim.SourcePriceConvention, v => ((ConventionValue)v) with { ZeroVolumeMeaning = EvidenceZeroMeaning.GENUINE_NO_EXECUTION });
            foreach (var id in Evidence.Conventions.Keys.ToArray())
                Evidence.Conventions[id] = Evidence.Conventions[id] with { ZeroVolumeMeaning = EvidenceZeroMeaning.GENUINE_NO_EXECUTION };
        }
        foreach (var row in Evidence.Rows.Where(r => r.Claim == EvidenceClaim.GenuinePriceObservation).ToArray())
        {
            var p = (PriceValue)ScreenerEvidenceBinding.Decode(row).Value!;
            var i = Array.IndexOf(Evidence.Dates, row.EffectiveFrom); var close = 100m + i;
            var bar = p.Bar with { Open = new(close - 1), Close = new(close), High = new(close + 2), Low = new(close - 3),
                Volume = zero ? 0 : 100 + i, VolumeUnit = quantity ? "SHARES" : "UNKNOWN", VolumeBasis = quantity ? "RAW_AS_TRADED" : "UNKNOWN",
                MarketSegment = quantity ? "REGULAR" : "UNKNOWN" };
            p = p with { Bar = bar, BarContentHash = ScreenerEvidenceBinding.BarHash(bar),
                ZeroVolumeSemantics = zero ? EvidenceZeroVolumeSemantics.EXPLICITLY_GENUINE : EvidenceZeroVolumeSemantics.NOT_APPLICABLE,
                ZeroVolumeProof = zero ? EvidenceZeroProof.DOCUMENTED_CONVENTION : EvidenceZeroProof.NONE };
            Evidence.Rows.Remove(row); Evidence.Rows.Add(ScreenerEvidenceBinding.Bind(row, row.EvidenceClass!.Value, row.ScopeKind!.Value,
                row.ScopeExchangeId!.Value, new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, p)));
        }
    }
    internal (ScreenerEvidenceReadinessResult Current, IReadOnlyList<ScreenerEvidenceReadinessResult> History) Input()
    {
        var reads = Evidence.Reads(Request);
        var retained = Evidence.Rows.ToDictionary(r => r.EvidenceId);
        var raw = new Dictionary<Guid, SourceReference> { [EvidenceBindingFixture.Reference] = new("source-a", EvidenceBindingFixture.Reference,
            EvidenceBindingFixture.Known.AddHours(-1), EvidenceBindingFixture.Known, new string('a', 64)) };
        foreach (var date in Evidence.Dates)
            foreach (var claim in ScreenerEvidenceReadiness.MarketClaims)
                if (!reads.Any(r => r.Request.SubjectId == Request.SubjectId && r.Request.Claim == claim && r.Request.EvaluationDate == date))
                    reads.Add(ScreenerEvidenceAsOf.Resolve(new(Request.SubjectId, claim, date, Request.Cutoff),
                        Evidence.Rows.Where(r => r.Claim == claim && (r.SubjectId == Request.SubjectId || r.ScopeKind == ScreenerScopeKind.EXCHANGE)).ToArray(),
                        retained, raw, EvidenceBindingFixture.Exchange));
        var current = ScreenerEvidenceReadiness.Compose(Request, reads, Evidence.Conventions);
        var dates = current.ActiveBars.Where(b => b.SubjectId == Request.SubjectId).Select(b => b.Date).Order().ToArray();
        var history = dates.Select(date => ScreenerEvidenceReadiness.Compose(Request with { EvaluationDate = date },
            reads.Where(r => r.Request.EvaluationDate <= date).ToArray(), Evidence.Conventions)).ToArray();
        return (current, history);
    }
    internal ScreenerEvidenceTechnicalResult Execute()
    { var input = Input(); return ScreenerEvidenceTechnical.Execute(input.Current, input.History, TestContext.Current.CancellationToken); }
    internal void Split(int after)
    {
        var day = Evidence.Dates[^(after + 1)];
        var action = Evidence.Add(EvidenceClaim.CorporateAction, new ActionEventValue(EvidenceActionValueKind.EVENT,
            "split", EvidenceActionType.SPLIT, day, null, null), day);
        Evidence.Replace(EvidenceClaim.CorporateAction, v => v is ActionCoverageValue c ? c with { EventEvidenceIds = [action.EvidenceId] } : v);
    }
    internal static DailyBar Bar(ScreenerReadinessBarBinding b) => new(new(b.SubjectId), b.Date, b.Price.Bar.Open.Value,
        b.Price.Bar.High.Value, b.Price.Bar.Low.Value, b.Price.Bar.Close.Value, b.Price.Bar.Volume, b.ObservationSource!,
        b.Price.Bar.AdjustedClose?.Value, b.Price.Bar.VolumeUnit, b.Price.Bar.VolumeBasis, b.Price.Bar.MarketSegment);
    internal static IReadOnlyDictionary<string, FeatureState> Legacy(ScreenerEvidenceReadinessResult ready)
    {
        var stock = ready.ActiveBars.Where(b => b.SubjectId == ready.Request.SubjectId).OrderBy(b => b.Date).ToArray();
        var raw = stock.Select(b => new ScreenerBarEvidence(b.SubjectId, b.Date, b.Price.BarRevision,
            b.Evidence.Chronology.KnownAt!.Value, b.Price.BarContentHash, EvidenceBindingFixture.Reference, EvidenceBindingFixture.Reference,
            b.Evidence.SourceId, b.ObservationSource!.ContentSha256, b.ObservationSource.FetchedAt, b.ObservationSource.FetchedAt,
            "https://example.test/session", b.Evidence.Chronology.KnownAt, "DEGRADED",
            b.Price.Bar.Open.Literal, b.Price.Bar.High.Literal, b.Price.Bar.Low.Literal, b.Price.Bar.Close.Literal,
            b.Price.Bar.Volume, b.Price.Bar.AdjustedClose?.Literal, b.Price.Bar.VolumeUnit, b.Price.Bar.VolumeBasis, b.Price.Bar.MarketSegment)).ToArray();
        var reference = new InstrumentSnapshot("synthetic", ready.Request.SubjectId, ready.Request.Cutoff, [], new string('a', 64),
            [new(ready.Request.HistoryFrom, ready.Request.EvaluationDate, "TEST", "Synthetic", "ORDINARY", "IDR", "MAIN", [])], [], [],
            [new(ready.Request.HistoryFrom, ready.Request.EvaluationDate, "source-a", "SHARES", "RAW_AS_TRADED", true, "IDR", "REGULAR",
                raw.Select(b => b.ContentHash).ToArray(), [])]);
        var calculator = typeof(ScreenerEvaluator).Assembly.GetType("IdxStockIntelligence.Application.ScreenerFeatures")!.GetMethod("Calculate")!;
        return (IReadOnlyDictionary<string, FeatureState>)calculator.Invoke(null, [stock.Select(Bar).ToArray(),
            ready.ActiveBars.Where(b => b.SubjectId != ready.Request.SubjectId).Select(Bar).ToDictionary(b => b.SessionDate),
            raw, reference, null, false, TestContext.Current.CancellationToken])!;
    }
    internal static string Stable(object value) => JsonSerializer.Serialize(value);
}

public sealed class ScreenerEvidenceV02TechnicalCompatibilityTests
{
    [Theory]
    [InlineData(50)]
    [InlineData(61)]
    public void GenuineZeroComputesMonetaryZeroWhileV01GateAndRelativeVolumeStayUnchanged(int count)
    {
        var f = new TechnicalFixture(count, zero: true); var input = f.Input();
        var result = ScreenerEvidenceTechnical.Execute(input.Current, input.History, TestContext.Current.CancellationToken);
        Assert.True(result.TechnicalEvaluated);
        Assert.Equal(Availability.AVAILABLE, result.Fields["monetaryLiquidity20Idr"].Availability);
        Assert.Equal(0m, result.Fields["monetaryLiquidity20Idr"].Value);
        Assert.Equal(0m, result.Fields["dailyValueProxyIdr"].Value);
        Assert.NotEqual(Availability.AVAILABLE, result.Fields["volumeRatio20"].Availability);
        Assert.Null(result.Fields["volumeRatio20"].Value);
        var legacy = TechnicalFixture.Legacy(input.Current);
        Assert.Equal(Availability.UNAVAILABLE, legacy["monetaryLiquidity20Idr"].Availability);
        Assert.Equal("VOLUME_BASIS_UNVERIFIED", legacy["monetaryLiquidity20Idr"].UnavailableReason);
        Assert.Null(legacy["monetaryLiquidity20Idr"].Value);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownQuantityIsNeverCoercedToZero(bool zero)
    {
        var result = new TechnicalFixture(quantity: false, zero: zero).Execute();
        Assert.True(result.TechnicalEvaluated);
        Assert.Null(result.Fields["monetaryLiquidity20Idr"].Value);
        Assert.Null(result.Fields["dailyValueProxyIdr"].Value);
        Assert.Equal("VOLUME_BASIS_UNVERIFIED", result.Fields["monetaryLiquidity20Idr"].UnavailableReason);
    }
    [Fact]
    public void UnadmittedZeroCannotBecomeTechnicalInput()
    {
        var f = new TechnicalFixture(zero: true);
        f.Evidence.Replace(EvidenceClaim.GenuinePriceObservation, v => ((PriceValue)v) with
        { ZeroVolumeSemantics = EvidenceZeroVolumeSemantics.AMBIGUOUS, ZeroVolumeProof = EvidenceZeroProof.NONE });
        Assert.False(f.Execute().TechnicalEvaluated);
    }
    [Theory]
    [InlineData(50)]
    [InlineData(61)]
    public void PositiveCleanFeaturesHaveExactV01Parity(int count)
    {
        var f = new TechnicalFixture(count); f.Request = f.Evidence.WithBenchmark(count);
        var input = f.Input(); var legacy = TechnicalFixture.Legacy(input.Current);
        var result = ScreenerEvidenceTechnical.Execute(input.Current, input.History, TestContext.Current.CancellationToken);
        Assert.True(result.TechnicalEvaluated);
        foreach (var name in new[] { "ema20", "ema50", "atr14", "atrPercent", "priorHigh20", "priorLow20",
            "distanceToHighPercent", "changePercent", "volumeRatio20", "dailyValueProxyIdr", "monetaryLiquidity20Idr", "rs20Pp" })
            Assert.Equal(legacy[name], result.Fields[name]);
        Assert.Equal(legacy["rs60Pp"].Value, result.Fields["rs60Pp"].Value);
        Assert.Equal(legacy["rs60Pp"].Availability, result.Fields["rs60Pp"].Availability);
        var window = result.StockBindings.SkipLast(1).TakeLast(20);
        Assert.Equal(window.Average(b => b.Price.Bar.Close.Value * b.Price.Bar.Volume), result.Fields["monetaryLiquidity20Idr"].Value);
    }
}

public sealed class ScreenerEvidenceV02TechnicalTests
{
    [Theory]
    [InlineData("warmup")]
    [InlineData("status")]
    [InlineData("synthetic")]
    [InlineData("coverage")]
    public void FailedReadinessNeverExecutes(string defect)
    {
        var f = new TechnicalFixture(defect == "warmup" ? 19 : 50);
        if (defect == "status") f.Evidence.Rows.RemoveAll(r => r.Claim == EvidenceClaim.TradingStatus);
        if (defect == "synthetic") f.Evidence.Replace(EvidenceClaim.GenuinePriceObservation, v => ((PriceValue)v) with { SyntheticOrCarryForward = EvidenceMarker.YES });
        if (defect == "coverage") f.Evidence.Replace(EvidenceClaim.CorporateAction, v => ((ActionCoverageValue)v) with { Coverage = EvidenceCoverage.PARTIAL });
        var input = f.Input(); var result = ScreenerEvidenceTechnical.Execute(input.Current, [], TestContext.Current.CancellationToken);
        Assert.False(result.TechnicalEvaluated); Assert.False(result.Setup.Evaluated); Assert.Empty(result.Fields);
        Assert.Contains("NOT_EVALUATED", result.Reasons);
    }
    [Fact]
    public void ArithmeticFailureCannotClaimExecutionDespiteReadiness()
    {
        var f = new TechnicalFixture(50);
        f.Evidence.Replace(EvidenceClaim.GenuinePriceObservation, v =>
        {
            var p = (PriceValue)v;
            var bar = p.Bar with { Open = new(1m), Low = new(1m), Close = new(decimal.MaxValue / 2m), High = new(decimal.MaxValue) };
            return p with { Bar = bar, BarContentHash = ScreenerEvidenceBinding.BarHash(bar) };
        });
        var input = f.Input(); Assert.True(input.Current.DataReady);
        var result = ScreenerEvidenceTechnical.Execute(input.Current, input.History, TestContext.Current.CancellationToken);
        Assert.False(result.TechnicalEvaluated); Assert.Empty(result.Fields);
        Assert.Contains("NUMERIC_OUT_OF_RANGE", result.Reasons);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionalArithmeticFailureDoesNotInvalidateStockExecution(bool benchmark)
    {
        var f = new TechnicalFixture(50);
        if (benchmark) f.Request = f.Evidence.WithBenchmark(50);
        foreach (var original in f.Evidence.Rows.Where(r => r.Claim == EvidenceClaim.GenuinePriceObservation
            && (benchmark ? r.SubjectId != f.Request.SubjectId : r.SubjectId == f.Request.SubjectId)).ToArray())
        {
            var p = (PriceValue)ScreenerEvidenceBinding.Decode(original).Value!;
            var close = benchmark ? decimal.MaxValue / 2m : 100000000000000000000m;
            var bar = p.Bar with { Open = new(close), High = new(close + 2m), Low = new(close - 3m), Close = new(close), Volume = long.MaxValue };
            p = p with { Bar = bar, BarContentHash = ScreenerEvidenceBinding.BarHash(bar) };
            f.Evidence.Rows.Remove(original);
            f.Evidence.Rows.Add(ScreenerEvidenceBinding.Bind(original, original.EvidenceClass!.Value, original.ScopeKind!.Value,
                original.ScopeExchangeId!.Value, new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, p)));
        }
        var result = f.Execute(); Assert.True(result.TechnicalEvaluated); Assert.True(result.Setup.Evaluated);
        var state = benchmark ? result.BenchmarkFields["ema50"] : result.Fields["monetaryLiquidity20Idr"];
        Assert.Equal(Availability.UNAVAILABLE, state.Availability); Assert.Null(state.Value);
        Assert.Equal("NUMERIC_OUT_OF_RANGE", state.UnavailableReason);
        Assert.Contains("NUMERIC_OUT_OF_RANGE", result.Reasons);
    }

    [Fact]
    public void ReadyInputActuallyExecutesWithOptionalBenchmarkAbsent()
    {
        var f = new TechnicalFixture(); var input = f.Input();
        Assert.All(input.Current.CoreFeatures.Values, v => Assert.Null(v.Value));
        var result = ScreenerEvidenceTechnical.Execute(input.Current, input.History, TestContext.Current.CancellationToken);
        Assert.True(result.TechnicalEvaluated); Assert.True(result.Setup.Evaluated);
        Assert.Equal(Availability.AVAILABLE, result.Fields["ema50"].Availability);
        Assert.Equal("BENCHMARK_MISSING", result.Fields["rs20Pp"].UnavailableReason);
        Assert.Null(result.Fields["actualTradedValue"].Value);
        Assert.StartsWith(f.Request.PolicyId + "/", result.Setup.Episode?.Id ?? f.Request.PolicyId + "/");
    }
    [Fact]
    public void ExactSharedSeedAndRecurrenceValuesAreProduced()
    {
        var result = new TechnicalFixture(50).Execute();
        Assert.True(result.TechnicalEvaluated);
        Assert.Equal(149m - 9.5m, result.Fields["ema20"].Value);
        Assert.Equal(124.5m, result.Fields["ema50"].Value);
        Assert.Equal(5m, result.Fields["atr14"].Value);
        Assert.Equal(150m, result.Fields["priorHigh20"].Value);
        Assert.Equal(126m, result.Fields["priorLow20"].Value);
        Assert.Equal(100m * (149m / 150m - 1m), result.Fields["distanceToHighPercent"].Value);
    }
    [Fact]
    public void KnownBreakExecutesOnlyCleanPostBreakSegment()
    {
        var f = new TechnicalFixture(61); f.Split(50); var input = f.Input();
        var result = ScreenerEvidenceTechnical.Execute(input.Current, input.History, TestContext.Current.CancellationToken);
        Assert.True(result.TechnicalEvaluated); Assert.Equal(50, result.StockBindings.Count);
        var expected = TechnicalFixture.Legacy(input.Current);
        Assert.Equal(expected["ema50"], result.Fields["ema50"]); Assert.Equal(expected["atr14"], result.Fields["atr14"]);
        f = new(61); f.Split(20); Assert.False(f.Execute().TechnicalEvaluated);
    }
    [Fact]
    public void OrderingDoesNotChangeTypedOrSerializedResult()
    {
        var f = new TechnicalFixture(); var input = f.Input();
        var a = ScreenerEvidenceTechnical.Execute(input.Current, input.History, TestContext.Current.CancellationToken);
        var b = ScreenerEvidenceTechnical.Execute(input.Current with { ActiveBars = input.Current.ActiveBars.Reverse().ToArray(), References = input.Current.References.Reverse().ToArray() },
            input.History.Reverse().Select(h => h with { ActiveBars = h.ActiveBars.Reverse().ToArray(), References = h.References.Reverse().ToArray() }).ToArray(), TestContext.Current.CancellationToken);
        Assert.True(a.TechnicalEvaluated); Assert.True(b.TechnicalEvaluated);
        Assert.Equal(TechnicalFixture.Stable(a), TechnicalFixture.Stable(b));
    }
    [Theory]
    [InlineData("bar")]
    [InlineData("source")]
    [InlineData("premise")]
    [InlineData("history")]
    [InlineData("cutoff")]
    [InlineData("policy")]
    public void ReplayMismatchFailsWithoutPartialExecution(string defect)
    {
        var f = new TechnicalFixture(50); var input = f.Input(); var ready = input.Current; var history = input.History;
        if (defect == "bar") ready = ready with { ActiveBars = ready.ActiveBars.Skip(1).ToArray() };
        if (defect == "source") ready = ready with { ActiveBars = ready.ActiveBars.Select(b => b with { ObservationSource = null }).ToArray() };
        if (defect == "premise") ready = ready with { References = ready.References.Where(r => r.EvidenceId != ready.ActiveBars[0].Price.ConventionEvidenceId).ToArray() };
        if (defect == "history") history = [];
        if (defect == "cutoff") ready = ready with { Request = ready.Request with { Cutoff = ready.Request.Cutoff.AddDays(1) } };
        if (defect == "policy") ready = ready with { Request = ready.Request with { PolicyId = "current" } };
        var result = ScreenerEvidenceTechnical.Execute(ready, history, TestContext.Current.CancellationToken);
        Assert.False(result.TechnicalEvaluated); Assert.Empty(result.Fields); Assert.Contains("NOT_EVALUATED", result.Reasons);
    }
    [Fact]
    public void HistoricalMarketExclusionInterruptsEpisodeUsingRetainedStatus()
    {
        var f = new TechnicalFixture(61);
        f.Evidence.Replace(EvidenceClaim.GenuinePriceObservation, v =>
        {
            var p = (PriceValue)v; var bar = p.Bar with { High = new(p.Bar.Close.Value) };
            return p with { Bar = bar, BarContentHash = ScreenerEvidenceBinding.BarHash(bar) };
        });
        var day = f.Evidence.Dates[^2];
        f.Evidence.Rows.RemoveAll(r => r.Claim == EvidenceClaim.TradingStatus && r.EffectiveFrom == day);
        f.Evidence.Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.SUSPENDED, f.Request.SessionId), day);
        var result = f.Execute(); Assert.True(result.TechnicalEvaluated);
        Assert.Equal(f.Request.EvaluationDate, result.Setup.Episode!.StartDate);
        Assert.Equal(1, result.Setup.Episode.AgeSessions);
    }
}
