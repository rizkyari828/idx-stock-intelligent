using Xunit;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceV02Tests
{
    private static readonly DateTimeOffset Retrieved = new(2026, 1, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FrozenEvidenceQualitiesExist()
    {
        Assert.Equal(
            new[]
            {
                EvidenceQuality.Verified,
                EvidenceQuality.Partial,
                EvidenceQuality.Unknown,
                EvidenceQuality.Conflicting,
                EvidenceQuality.Stale
            },
            Enum.GetValues<EvidenceQuality>());
    }

    [Fact]
    public void FrozenMarketEligibilityReusesDomainStatus()
    {
        Assert.Equal(
            new[] { EligibilityStatus.Eligible, EligibilityStatus.Ineligible, EligibilityStatus.DataBlocked },
            Enum.GetValues<EligibilityStatus>());
    }

    [Fact]
    public void FrozenFeatureAvailabilityExists()
    {
        Assert.Equal(
            new[] { Availability.AVAILABLE, Availability.UNAVAILABLE, Availability.WARMUP },
            Enum.GetValues<Availability>());
    }

    [Fact]
    public void FrozenSourceAuthorityTiersExist()
    {
        Assert.Equal(
            new[]
            {
                SourceAuthorityTier.T1Governing,
                SourceAuthorityTier.T2AdmittedReference,
                SourceAuthorityTier.T3ProviderObservation,
                SourceAuthorityTier.T4Derivation,
                SourceAuthorityTier.T5Secondary
            },
            Enum.GetValues<SourceAuthorityTier>());
    }

    [Fact]
    public void FrozenPriceComparabilityStatesExist()
    {
        Assert.Equal(
            new[] { PriceComparability.Cleared, PriceComparability.KnownBreak, PriceComparability.Unresolved },
            Enum.GetValues<PriceComparability>());
    }

    [Fact]
    public void FrozenTradingStatusesExist()
    {
        Assert.Equal(
            new[] { TradingStatus.Trading, TradingStatus.Suspended, TradingStatus.ReopeningUnconfirmed, TradingStatus.Unknown },
            Enum.GetValues<TradingStatus>());
    }

    [Fact]
    public void PolicyIdentityIsExactAndDistinctFromV01()
    {
        Assert.Equal("screener-evidence-v0.2.0", ScreenerEvidenceV02.PolicyId);
        Assert.Equal(1, ScreenerEvidenceV02.SchemaVersion);
        Assert.True(ScreenerEvidenceV02.Supports("screener-evidence-v0.2.0", 1));
        Assert.False(ScreenerEvidenceV02.Supports("screener-v0.1.0", 1));
        Assert.False(ScreenerEvidenceV02.Supports("screener-evidence-v0.2.0", 2));
    }

    [Fact]
    public void EvidenceValueAndQualityAreIndependent()
    {
        var scope = new EffectiveInterval(new DateOnly(2026, 1, 1));
        var chronology = new EvidenceChronology(retrievedAt: Retrieved, knownAt: Retrieved.AddHours(1));
        var verified = new EvidenceFact<decimal>(100m, EvidenceQuality.Verified, SourceAuthorityTier.T1Governing, "src", chronology, scope);
        var stale = new EvidenceFact<decimal>(100m, EvidenceQuality.Stale, SourceAuthorityTier.T1Governing, "src", chronology, scope);

        Assert.Equal(verified.Value, stale.Value);
        Assert.NotEqual(verified.Quality, stale.Quality);
    }

    [Fact]
    public void UnknownEvidenceHasNoValue()
    {
        var unknown = new EvidenceFact<decimal?>(null, EvidenceQuality.Unknown, SourceAuthorityTier.T1Governing, null, new EvidenceChronology());
        Assert.False(unknown.Value.HasValue);
        Assert.Equal(EvidenceQuality.Unknown, unknown.Quality);

        Assert.Throws<ArgumentException>(() => new EvidenceFact<decimal?>(
            1m, EvidenceQuality.Unknown, SourceAuthorityTier.T1Governing, null, new EvidenceChronology()));
    }

    [Fact]
    public void VerifiedFactRequiresScopeAndKnownTime()
    {
        var chronology = new EvidenceChronology(retrievedAt: Retrieved, knownAt: Retrieved.AddHours(1));
        var scope = new EffectiveInterval(new DateOnly(2026, 1, 1));

        Assert.Throws<ArgumentException>(() => new EvidenceFact<decimal>(
            1m, EvidenceQuality.Verified, SourceAuthorityTier.T1Governing, "src", chronology));
        Assert.Throws<ArgumentException>(() => new EvidenceFact<decimal>(
            1m, EvidenceQuality.Verified, SourceAuthorityTier.T1Governing, "src", new EvidenceChronology(), scope));

        var verified = new EvidenceFact<decimal>(
            1m, EvidenceQuality.Verified, SourceAuthorityTier.T1Governing, "src", chronology, scope);
        Assert.Equal(EvidenceQuality.Verified, verified.Quality);
    }

    [Fact]
    public void ChronologyRepresentsUnknownClocksExplicitly()
    {
        var chronology = new EvidenceChronology();

        Assert.Null(chronology.EffectiveAt);
        Assert.Null(chronology.PublicationAt);
        Assert.Null(chronology.RetrievedAt);
        Assert.Null(chronology.KnownAt);
        Assert.Null(chronology.RecordedAt);
        Assert.False(chronology.KnownAdmitted);
    }

    [Fact]
    public void ChronologyRejectsKnownBeforeRetrieved()
    {
        Assert.Throws<ArgumentException>(() => new EvidenceChronology(
            retrievedAt: Retrieved, knownAt: Retrieved.AddMinutes(-1)));
    }

    [Fact]
    public void ChronologyAcceptsKnownAtOrAfterRetrieved()
    {
        var equal = new EvidenceChronology(retrievedAt: Retrieved, knownAt: Retrieved);
        var later = new EvidenceChronology(retrievedAt: Retrieved, knownAt: Retrieved.AddDays(1));

        Assert.True(equal.KnownAdmitted);
        Assert.True(later.KnownAdmitted);
    }

    [Fact]
    public void GenuinePriceObservationDoesNotCarryTradingStatus()
    {
        Assert.Null(typeof(GenuinePriceObservation).GetProperty("TradingStatus"));
        Assert.Null(typeof(GenuinePriceObservation).GetProperty("Status"));
        Assert.Equal("TRADED_OBSERVED_AT_T", ScreenerEvidenceReasons.TradedObservedAtT);
        Assert.Contains(ScreenerEvidenceReasons.TradedObservedAtT, ScreenerEvidenceReasons.All);

        var observation = new GenuinePriceObservation(Bar(), 1, Retrieved.AddHours(1), Retrieved, new string('b', 64));
        Assert.Equal(1, observation.RevisionNumber);
        Assert.Equal(105m, observation.Bar.Close);
    }

    [Fact]
    public void GenuinePriceObservationRejectsInvalidProvenance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenuinePriceObservation(Bar(), 0, Retrieved.AddHours(1), Retrieved, new string('b', 64)));
        Assert.Throws<ArgumentException>(() =>
            new GenuinePriceObservation(Bar(), 1, Retrieved, Retrieved.AddHours(1), new string('b', 64)));
        Assert.Throws<ArgumentException>(() =>
            new GenuinePriceObservation(Bar(), 1, Retrieved.AddHours(1), Retrieved, "not-a-hash"));
    }

    [Fact]
    public void VerifiedTradingStatusRequiresAuthoritativeTier()
    {
        var scope = new EffectiveInterval(new DateOnly(2026, 1, 1));
        var chronology = new EvidenceChronology(retrievedAt: Retrieved, knownAt: Retrieved.AddHours(1));

        Assert.Throws<ArgumentException>(() => new TradingStatusEvidence(
            TradingStatus.Trading, EvidenceQuality.Verified, SourceAuthorityTier.T3ProviderObservation, "src", chronology, scope));

        var authorized = new TradingStatusEvidence(
            TradingStatus.Trading, EvidenceQuality.Verified, SourceAuthorityTier.T1Governing, "src", chronology, scope);
        Assert.Equal(TradingStatus.Trading, authorized.Status);
    }

    [Fact]
    public void ClearedComparabilityCannotCarryPartialActionReason()
    {
        Assert.Throws<ArgumentException>(() => new PriceComparabilityResult(
            PriceComparability.Cleared, [ScreenerEvidenceReasons.CorporateActionCoveragePartial]));

        var cleared = new PriceComparabilityResult(PriceComparability.Cleared);
        Assert.Equal(PriceComparability.Cleared, cleared.State);
        Assert.Empty(cleared.Reasons);
    }

    [Fact]
    public void EligibleMarketEligibilityCarriesNoReason()
    {
        var eligible = new MarketEligibilityResult(EligibilityStatus.Eligible);
        Assert.Equal(EligibilityStatus.Eligible, eligible.Status);
        Assert.Empty(eligible.Reasons);

        Assert.Throws<ArgumentException>(() => new MarketEligibilityResult(
            EligibilityStatus.Eligible, [ScreenerEvidenceReasons.StatusUnknown]));

        var blocked = new MarketEligibilityResult(EligibilityStatus.DataBlocked, [ScreenerEvidenceReasons.StatusUnknown]);
        Assert.Single(blocked.Reasons);
    }

    [Fact]
    public void SetupEvaluationRequiresMarketEligibleAndDataReady()
    {
        Assert.False(ScreenerFunnel.SetupEvaluationAllowed(marketEligibleProved: false, dataReady: false));
        Assert.False(ScreenerFunnel.SetupEvaluationAllowed(marketEligibleProved: false, dataReady: true));
        Assert.False(ScreenerFunnel.SetupEvaluationAllowed(marketEligibleProved: true, dataReady: false));
        Assert.True(ScreenerFunnel.SetupEvaluationAllowed(marketEligibleProved: true, dataReady: true));
    }

    [Fact]
    public void ReasonCodeRepresentationIsDeterministic()
    {
        Assert.Equal(
            ScreenerEvidenceReasons.All.Count,
            ScreenerEvidenceReasons.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ScreenerEvidenceReasons.All, reason => Assert.Equal(reason, reason.ToUpperInvariant()));

        var canonical = ScreenerEvidenceReasons.Canonical(
        [
            ScreenerEvidenceReasons.StatusUnknown,
            ScreenerEvidenceReasons.EvidenceConflict,
            ScreenerEvidenceReasons.StatusUnknown
        ]);
        Assert.Equal(
            new[] { ScreenerEvidenceReasons.EvidenceConflict, ScreenerEvidenceReasons.StatusUnknown },
            canonical);
    }

    [Fact]
    public void V01IdentitiesRemainUnchanged()
    {
        Assert.Equal("screener-v0.1.0", ScreenerReadRequest.PolicyId);
        Assert.Equal("outcome-v0.1.0", OutcomeEvaluator.PolicyId);
        Assert.Equal("research-evaluation-v0.1.0", ResearchEvaluation.PolicyId);
    }

    private static DailyBar Bar()
    {
        var source = new SourceReference("fixture", Guid.NewGuid(), Retrieved, Retrieved, new string('a', 64));
        return new DailyBar(
            new InstrumentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new DateOnly(2026, 1, 2),
            open: 100m,
            high: 110m,
            low: 95m,
            close: 105m,
            volume: 1_000,
            source);
    }
}
