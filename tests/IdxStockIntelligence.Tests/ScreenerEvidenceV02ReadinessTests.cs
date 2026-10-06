using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

internal sealed class ReadinessFixture
{
    internal List<ScreenerEvidenceRecord> Rows { get; } = [];
    internal ScreenerReadinessRequest Request { get; }
    internal Dictionary<Guid, ConventionValue> Conventions { get; } = [];
    internal DateOnly[] Dates { get; }
    internal ReadinessFixture(int count = 50)
    {
        Dates = Enumerable.Range(0, 160).Select(i => EvidenceBindingFixture.Day.AddDays(-i))
            .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Take(count).Reverse().ToArray();
        var cv = EvidenceBindingFixture.Convention;
        Request = new(EvidenceBindingFixture.Instrument, "regular", EvidenceBindingFixture.Day, Dates[0],
            EvidenceBindingFixture.Known, new(cv.PriceSourceId, cv.Endpoint, cv.Field, cv.Version));
        Add(EvidenceClaim.StableIdentity, new IdentityValue("TEST"));
        Add(EvidenceClaim.SecurityType, new SecurityTypeValue(EvidenceSecurityType.ORDINARY));
        Add(EvidenceClaim.Currency, new CurrencyValue("IDR"));
        Add(EvidenceClaim.ListingCoverage, new ListingValue(Dates[0]));
        Add(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.MAIN));
        Add(EvidenceClaim.ExchangeRuleVersion, new RuleValue("mechanism", "1", EvidenceMechanism.CONTINUOUS));
        Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.TRADING, "regular"), EvidenceBindingFixture.Day);
        Add(EvidenceClaim.CorporateAction, new ActionCoverageValue(EvidenceActionValueKind.COVERAGE, "coverage", EvidenceCoverage.COMPLETE, []),
            Dates[0], EvidenceBindingFixture.Day);
        var convention = Add(EvidenceClaim.SourcePriceConvention, cv); Conventions.Add(convention.EvidenceId, cv);
        foreach (var day in Dates)
        {
            var session = Add(EvidenceClaim.CompletedSession, new CompletedSessionValue("regular", EvidenceCompletion.COMPLETED,
                EvidenceBindingFixture.Known.AddHours(-1)), day);
            Add(EvidenceClaim.GenuinePriceObservation, EvidenceBindingFixture.Price with
            { ConventionEvidenceId = convention.EvidenceId, CompletedSessionEvidenceId = session.EvidenceId }, day);
        }
    }
    internal ScreenerEvidenceRecord Add(EvidenceClaim claim, ScreenerEvidenceValue value, DateOnly? from = null, DateOnly? to = null,
        string? series = null, EvidenceCompleteness completeness = EvidenceCompleteness.FULL, DateTimeOffset? known = null)
    {
        var row = AsOfFixture.Row(claim, value, from: from ?? Dates[0], to: to,
            series: series ?? claim + "-" + Rows.Count, completeness: completeness, known: known);
        Rows.Add(row); return row;
    }
    internal void Replace(EvidenceClaim claim, Func<ScreenerEvidenceValue, ScreenerEvidenceValue> replace,
        EvidenceCompleteness completeness = EvidenceCompleteness.FULL)
    {
        foreach (var row in Rows.Where(r => r.Claim == claim).ToArray())
        {
            Rows.Remove(row);
            Rows.Add(ScreenerEvidenceBinding.Bind(row, row.EvidenceClass!.Value, row.ScopeKind!.Value, row.ScopeExchangeId!.Value,
                new(EvidenceOperation.ASSERT, completeness, replace(ScreenerEvidenceBinding.Decode(row).Value!))));
        }
    }
    internal List<ScreenerEvidenceAsOfResult> Reads(ScreenerReadinessRequest? request = null)
    {
        request ??= Request;
        var retained = Rows.ToDictionary(r => r.EvidenceId);
        var raw = new Dictionary<Guid, SourceReference> { [EvidenceBindingFixture.Reference] = new("source-a", EvidenceBindingFixture.Reference,
            EvidenceBindingFixture.Known.AddHours(-1), EvidenceBindingFixture.Known, new string('a', 64)) };
        var reads = new Dictionary<(Guid, EvidenceClaim, DateOnly), ScreenerEvidenceAsOfResult>();
        void Read(Guid subject, EvidenceClaim claim, DateOnly date) => reads.TryAdd((subject, claim, date), ScreenerEvidenceAsOf.Resolve(
            new(subject, claim, date, request.Cutoff), Rows.Where(r => r.Claim == claim && (r.SubjectId == subject
                || r.ScopeKind == ScreenerScopeKind.EXCHANGE)).ToArray(), retained, raw, EvidenceBindingFixture.Exchange));
        foreach (var claim in ScreenerEvidenceReadiness.MarketClaims) Read(request.SubjectId, claim, request.EvaluationDate);
        foreach (var subject in new[] { request.SubjectId, request.Benchmark?.SubjectId }.Where(s => s.HasValue).Select(s => s!.Value))
            foreach (var day in request.Dates)
                foreach (var claim in ScreenerEvidenceReadiness.HistoryClaims) Read(subject, claim, day);
        return reads.Values.ToList();
    }
    internal ScreenerReadinessRequest WithBenchmark(int observations)
    {
        var subject = Guid.NewGuid(); var conventionId = Guid.NewGuid();
        foreach (var original in Rows.Where(r => r.ScopeKind == ScreenerScopeKind.INSTRUMENT
            && ScreenerEvidenceReadiness.HistoryClaims.Contains(r.Claim)).ToArray())
        {
            if (original.Claim == EvidenceClaim.GenuinePriceObservation && !Dates.TakeLast(observations).Contains(original.EffectiveFrom)) continue;
            var value = ScreenerEvidenceBinding.Decode(original).Value!;
            if (value is ConventionValue cv) { value = cv with { PriceKind = EvidencePriceKind.INDEX_LEVEL }; Conventions.Add(conventionId, (ConventionValue)value); }
            if (value is PriceValue price) value = price with { ConventionEvidenceId = conventionId };
            var id = original.Claim == EvidenceClaim.SourcePriceConvention ? conventionId : Guid.NewGuid();
            var envelope = new ScreenerEvidenceRecord(id, subject, original.Claim, original.PolicyId, 1,
                ScreenerEvidenceRevisionSeries.Canonical(original.SourceId, "benchmark-" + id), 1, null, original.AuthorityTier,
                original.EffectiveFrom, original.EffectiveTo, null, original.PublishedAt, original.RetrievedAt, original.KnownAt,
                original.SourceId, original.SourceReference, original.RawArtifactId, "{}");
            Rows.Add(ScreenerEvidenceBinding.Bind(envelope, original.EvidenceClass!.Value, ScreenerScopeKind.INSTRUMENT,
                EvidenceBindingFixture.Exchange, new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, value)));
        }
        return Request with { Benchmark = new(subject, Request.PriceField) };
    }
    internal ScreenerEvidenceReadinessResult Evaluate(ScreenerReadinessRequest? request = null) =>
        ScreenerEvidenceReadiness.Compose(request ?? Request, Reads(request), Conventions);
}

public sealed class ScreenerEvidenceV02ReadinessTests
{
    [Fact]
    public void AllHardFactsAndClearedHistoryPermitSetupButDoNotExecuteIt()
    {
        var result = new ReadinessFixture().Evaluate();
        Assert.Equal(EligibilityStatus.Eligible, result.MarketEligibility.Status);
        Assert.Equal(PriceComparability.Cleared, result.PriceComparability.State);
        Assert.True(result.DataReady); Assert.True(result.CanEvaluateSetup);
        Assert.All(result.CoreFeatures.Values, f => { Assert.Equal(Availability.AVAILABLE, f.Availability); Assert.Null(f.Value); });
        Assert.Equal(50, result.Bars.Count); Assert.NotEmpty(result.References);
        Assert.DoesNotContain("TECHNICAL_EVALUATED", result.Diagnostics);
    }
    [Theory]
    [InlineData("suspended", EligibilityStatus.Ineligible)]
    [InlineData("missing", EligibilityStatus.DataBlocked)]
    [InlineData("partial", EligibilityStatus.DataBlocked)]
    [InlineData("conflict", EligibilityStatus.DataBlocked)]
    public void StatusRequiresPositiveCompleteAuthoritativeFacts(string scenario, EligibilityStatus expected)
    {
        var f = new ReadinessFixture(1);
        f.Rows.RemoveAll(r => r.Claim == EvidenceClaim.TradingStatus);
        if (scenario == "suspended") f.Add(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice"));
        if (scenario == "partial") f.Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.TRADING, "regular"),
            EvidenceBindingFixture.Day, completeness: EvidenceCompleteness.PARTIAL);
        if (scenario == "conflict")
        {
            f.Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.TRADING, "regular"), EvidenceBindingFixture.Day);
            f.Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.SUSPENDED, "regular"), EvidenceBindingFixture.Day);
        }
        Assert.Equal(expected, f.Evaluate().MarketEligibility.Status);
    }
    [Theory]
    [InlineData(EvidenceClaim.BoardRegime)]
    [InlineData(EvidenceClaim.MechanismException)]
    public void ConflictingBoardOrMechanismBlocksMarket(EvidenceClaim claim)
    {
        var f = new ReadinessFixture(1);
        f.Add(claim, claim == EvidenceClaim.BoardRegime ? new BoardValue(EvidenceBoard.DEVELOPMENT)
            : new ExceptionValue("exception", "exact", EvidenceMechanism.CALL_AUCTION));
        Assert.Equal(EligibilityStatus.DataBlocked, f.Evaluate().MarketEligibility.Status);
    }
    [Theory]
    [InlineData("coverage", PriceComparability.Unresolved, Availability.UNAVAILABLE)]
    [InlineData("warmup", PriceComparability.Cleared, Availability.WARMUP)]
    [InlineData("synthetic", PriceComparability.Unresolved, Availability.UNAVAILABLE)]
    public void TechnicalFailuresNeverChangeMarketEligibility(string scenario, PriceComparability expected, Availability availability)
    {
        var f = new ReadinessFixture(scenario == "warmup" ? 19 : 50);
        if (scenario == "coverage") f.Replace(EvidenceClaim.CorporateAction, v => ((ActionCoverageValue)v) with { Coverage = EvidenceCoverage.PARTIAL });
        if (scenario == "synthetic") f.Replace(EvidenceClaim.GenuinePriceObservation, v => ((PriceValue)v) with { SyntheticOrCarryForward = EvidenceMarker.YES });
        var result = f.Evaluate();
        Assert.Equal(EligibilityStatus.Eligible, result.MarketEligibility.Status);
        Assert.Equal(expected, result.PriceComparability.State);
        Assert.Equal(availability, result.CoreFeatures[TechnicalFeature.Ema20].Availability);
        Assert.False(result.DataReady); Assert.False(result.CanEvaluateSetup);
        if (scenario == "synthetic") Assert.Empty(result.Bars);
    }
    [Theory]
    [InlineData(10, Availability.WARMUP, false)]
    [InlineData(50, Availability.AVAILABLE, true)]
    public void KnownBreakRestartsOnlyTheProvedPostBreakSegment(int subsequent, Availability expected, bool ready)
    {
        var f = new ReadinessFixture(61);
        var day = f.Dates[^(subsequent + 1)];
        var action = f.Add(EvidenceClaim.CorporateAction, new ActionEventValue(EvidenceActionValueKind.EVENT, "split", EvidenceActionType.SPLIT, day, null, null), day);
        f.Replace(EvidenceClaim.CorporateAction, v => v is ActionCoverageValue coverage ? coverage with { EventEvidenceIds = [action.EvidenceId] } : v);
        var result = f.Evaluate();
        Assert.Equal(PriceComparability.KnownBreak, result.HistoryComparability.State);
        Assert.Equal(PriceComparability.Cleared, result.PriceComparability.State);
        Assert.Equal(expected, result.CoreFeatures[TechnicalFeature.Ema50].Availability);
        Assert.Equal(ready, result.DataReady);
        Assert.Equal(subsequent, result.ActiveBars.Count);
        Assert.All(result.ActiveBars, b => Assert.True(b.Date > day));
    }
    [Fact]
    public void OptionalBenchmarkAndQuantityFailuresStayLocal()
    {
        var result = new ReadinessFixture().Evaluate();
        Assert.True(result.DataReady);
        Assert.Equal(Availability.UNAVAILABLE, result.OptionalFeatures[OptionalFeature.Rs20].Availability);
        Assert.Equal(ScreenerEvidenceReasons.BenchmarkMissing, result.OptionalFeatures[OptionalFeature.MarketContext].UnavailableReason);
        Assert.Equal(ScreenerEvidenceReasons.VolumeBasisUnverified, result.OptionalFeatures[OptionalFeature.RelativeVolume].UnavailableReason);
        Assert.Equal(Availability.UNAVAILABLE, result.OptionalFeatures[OptionalFeature.ActualTradedValue].Availability);
    }
    [Fact]
    public void DiagnosticsAndBindingsAreIndependentOfReadOrdering()
    {
        var f = new ReadinessFixture(); var reads = f.Reads();
        var a = ScreenerEvidenceReadiness.Compose(f.Request, reads, f.Conventions);
        reads.Reverse();
        var b = ScreenerEvidenceReadiness.Compose(f.Request, reads, f.Conventions);
        Assert.Equal(a.Diagnostics, b.Diagnostics); Assert.Equal(a.References, b.References); Assert.Equal(a.Bars, b.Bars);
        Assert.Equal(a.CoreFeatures, b.CoreFeatures); Assert.Equal(a.OptionalFeatures, b.OptionalFeatures);
    }
    [Fact]
    public void AggregateOverflowAndInvalidBoundariesFailClosed()
    {
        var f = new ReadinessFixture(1); var reads = f.Reads();
        reads[0] = reads[0] with { ExaminedEvidenceIds = Enumerable.Range(0, 513).Select(_ => Guid.NewGuid()).ToArray() };
        var result = ScreenerEvidenceReadiness.Compose(f.Request, reads, f.Conventions);
        Assert.Equal(ScreenerEvidenceAsOf.BoundExceeded, result.FailureReason); Assert.False(result.CanEvaluateSetup);
        Assert.Throws<ArgumentException>(() => f.Evaluate(f.Request with { PolicyId = "current" }));
        Assert.Throws<ArgumentNullException>(() => f.Evaluate(f.Request with { Benchmark = new(Guid.NewGuid(), null!) }));
        Assert.Throws<ArgumentException>(() => f.Evaluate(f.Request with { HistoryFrom = f.Request.EvaluationDate.AddDays(-330) }));
        reads = f.Reads(); reads[0] = reads[0] with { Request = reads[0].Request with { Cutoff = f.Request.Cutoff.AddDays(1) } };
        Assert.Throws<ArgumentException>(() => ScreenerEvidenceReadiness.Compose(f.Request, reads, f.Conventions));
    }
    [Theory]
    [InlineData(20, Availability.WARMUP)]
    [InlineData(61, Availability.AVAILABLE)]
    public void ExactAlignedBenchmarkControlsOnlyRelativeFeatures(int observations, Availability expected)
    {
        var f = new ReadinessFixture(61); var request = f.WithBenchmark(observations);
        var result = f.Evaluate(request);
        Assert.True(result.DataReady);
        Assert.Equal(expected, result.OptionalFeatures[OptionalFeature.Rs60].Availability);
        Assert.Equal(expected, result.OptionalFeatures[OptionalFeature.Rs20].Availability);
        Assert.Equal(expected, result.OptionalFeatures[OptionalFeature.MarketContext].Availability);
    }
    [Fact]
    public void MissingWeekdayAndConflictingCoverageCannotBeBridged()
    {
        var f = new ReadinessFixture();
        f.Rows.RemoveAll(r => r.Claim == EvidenceClaim.CompletedSession && r.EffectiveFrom == f.Dates[40]);
        var result = f.Evaluate();
        Assert.False(result.DataReady); Assert.Equal(Availability.WARMUP, result.CoreFeatures[TechnicalFeature.Ema50].Availability);
        f.Add(EvidenceClaim.CorporateAction, new ActionCoverageValue(EvidenceActionValueKind.COVERAGE, "partial", EvidenceCoverage.PARTIAL, []),
            f.Dates[0], EvidenceBindingFixture.Day);
        Assert.Equal(PriceComparability.Unresolved, f.Evaluate().PriceComparability.State);
    }
    [Fact]
    public void AdmittedShareQuantitiesEnableOptionalVolumeButNeverActualTradedValue()
    {
        var f = new ReadinessFixture();
        f.Replace(EvidenceClaim.GenuinePriceObservation, value =>
        {
            var p = (PriceValue)value;
            var bar = p.Bar with { VolumeUnit = "SHARES", VolumeBasis = "RAW_AS_TRADED", MarketSegment = "REGULAR" };
            return p with { Bar = bar, BarContentHash = ScreenerEvidenceBinding.BarHash(bar) };
        });
        var result = f.Evaluate();
        Assert.True(result.DataReady);
        Assert.Equal(Availability.AVAILABLE, result.OptionalFeatures[OptionalFeature.RelativeVolume].Availability);
        Assert.Equal(Availability.AVAILABLE, result.OptionalFeatures[OptionalFeature.MonetaryLiquidityProxy].Availability);
        Assert.Equal(Availability.UNAVAILABLE, result.OptionalFeatures[OptionalFeature.ActualTradedValue].Availability);
        f.Replace(EvidenceClaim.GenuinePriceObservation, value =>
        {
            var p = (PriceValue)value; var bar = p.Bar with { VolumeBasis = "SPLIT_ADJUSTED" };
            return p with { Bar = bar, BarContentHash = ScreenerEvidenceBinding.BarHash(bar) };
        });
        result = f.Evaluate();
        Assert.True(result.DataReady);
        Assert.Equal(Availability.UNAVAILABLE, result.OptionalFeatures[OptionalFeature.MonetaryLiquidityProxy].Availability);
    }
}
