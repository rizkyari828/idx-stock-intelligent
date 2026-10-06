using Xunit;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceV02EvaluatorTests
{
    private static readonly DateTimeOffset Cutoff = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static DateOnly Day(int month, int day) => new(2026, month, day);

    private static DateTimeOffset Instant(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static DailyBar Bar(long volume = 1000, decimal close = 105m)
    {
        var source = new SourceReference("fixture", Guid.NewGuid(), Cutoff, Cutoff, new string('a', 64));
        return new DailyBar(
            new InstrumentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Day(1, 2),
            100m,
            110m,
            95m,
            close,
            volume,
            source);
    }

    private static TradingStatusEvidence Status(
        TradingStatus status,
        EvidenceQuality quality,
        EffectiveInterval scope,
        DateTimeOffset known,
        SourceAuthorityTier tier = SourceAuthorityTier.T1Governing,
        string sourceId = "src") =>
        new(status, quality, tier, sourceId, new EvidenceChronology(knownAt: known), scope);

    private static PriceObservationCandidate Candidate(
        bool sourceConvention = true,
        bool sessionCompleted = true,
        bool synthetic = false,
        bool placeholder = false,
        bool substituted = false,
        long volume = 1000,
        ZeroVolumeSemantics zero = ZeroVolumeSemantics.NotApplicable,
        long revision = 1) =>
        new(
            Bar(volume),
            revision,
            Cutoff.AddHours(-1),
            null,
            new string('b', 64),
            sourceConvention,
            sessionCompleted,
            synthetic,
            placeholder,
            substituted,
            zero);

    private static MarketEvidenceFacts Facts(
        TradingStatusResult? status = null,
        bool identityConflict = false,
        bool securityTypeKnown = true,
        bool securityTypeUnsupported = false,
        bool listingKnown = true,
        bool preListing = false,
        bool postDelisting = false,
        bool boardKnown = true,
        bool boardUnsupported = false,
        bool boardConflict = false,
        bool mechanismKnown = true,
        bool mechanismUnresolved = false,
        bool targetSessionCompleted = true) =>
        new(
            true,
            identityConflict,
            securityTypeKnown,
            securityTypeUnsupported,
            listingKnown,
            preListing,
            postDelisting,
            boardKnown,
            boardUnsupported,
            boardConflict,
            mechanismKnown,
            mechanismUnresolved,
            status ?? new TradingStatusResult(TradingStatus.Trading, EvidenceQuality.Verified, [ScreenerEvidenceReasons.StatusTrading]),
            targetSessionCompleted);

    private static FeatureState Available() => new(Availability.AVAILABLE, 1m, null);

    private static FeatureState Warmup() => new(Availability.WARMUP, null, ScreenerEvidenceReasons.InsufficientHistory);

    private static MarketEligibilityResult EligibleMarket() => new(EligibilityStatus.Eligible);

    private static PriceComparabilityRequest ComparabilityRequest(
        bool identity = true,
        bool currency = true,
        bool rawConvention = true,
        bool session = true,
        bool actionCoverage = true,
        bool revisionBinding = true,
        IReadOnlyList<CorporateActionKind>? breaks = null) =>
        new(identity, currency, rawConvention, session, actionCoverage, revisionBinding, breaks ?? []);

    [Theory]
    [InlineData(EvidenceClaim.Suspension, SourceAuthorityTier.T1Governing)]
    [InlineData(EvidenceClaim.Reopening, SourceAuthorityTier.T1Governing)]
    [InlineData(EvidenceClaim.StableIdentity, SourceAuthorityTier.T1Governing)]
    [InlineData(EvidenceClaim.BoardRegime, SourceAuthorityTier.T2AdmittedReference)]
    [InlineData(EvidenceClaim.CompletedSession, SourceAuthorityTier.T2AdmittedReference)]
    [InlineData(EvidenceClaim.PriceComparability, SourceAuthorityTier.T4Derivation)]
    public void SourceAdmissionAdmitsSufficientAuthority(EvidenceClaim claim, SourceAuthorityTier tier)
    {
        var result = ScreenerSourceAdmission.Admit(claim, tier);
        Assert.True(result.Admitted);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData(EvidenceClaim.Suspension, SourceAuthorityTier.T2AdmittedReference)]
    [InlineData(EvidenceClaim.Suspension, SourceAuthorityTier.T3ProviderObservation)]
    [InlineData(EvidenceClaim.Reopening, SourceAuthorityTier.T3ProviderObservation)]
    [InlineData(EvidenceClaim.StableIdentity, SourceAuthorityTier.T4Derivation)]
    [InlineData(EvidenceClaim.MechanismException, SourceAuthorityTier.T4Derivation)]
    [InlineData(EvidenceClaim.Suspension, SourceAuthorityTier.T5Secondary)]
    [InlineData(EvidenceClaim.PriceComparability, SourceAuthorityTier.T5Secondary)]
    public void SourceAdmissionRejectsInsufficientOrInadmissibleAuthority(EvidenceClaim claim, SourceAuthorityTier tier)
    {
        var result = ScreenerSourceAdmission.Admit(claim, tier);
        Assert.False(result.Admitted);
        Assert.Equal(ScreenerEvidenceReasons.EvidenceSourceInadmissible, result.Reason);
    }

    [Fact]
    public void ValidityExcludesEvidenceNotYetKnownAtCutoff()
    {
        var scope = new EffectiveInterval(Day(1, 1));
        var learnedLater = new EvidenceChronology(knownAt: Cutoff.AddDays(1));

        var quality = ScreenerEvidenceValidity.Evaluate(EvidenceClass.ContinuingState, scope, Day(5, 1), learnedLater, Cutoff);

        Assert.Equal(EvidenceQuality.Unknown, quality);
    }

    [Fact]
    public void ValidityEffectiveInPastButLearnedLaterStaysInvisible()
    {
        var scope = new EffectiveInterval(Day(1, 1), Day(12, 31));
        var learnedLater = new EvidenceChronology(knownAt: Cutoff.AddDays(10));

        var quality = ScreenerEvidenceValidity.Evaluate(EvidenceClass.PointObservation, scope, Day(5, 1), learnedLater, Cutoff);

        Assert.Equal(EvidenceQuality.Unknown, quality);
    }

    [Fact]
    public void ContinuingStateDoesNotBecomeStaleWithAge()
    {
        var scope = new EffectiveInterval(new DateOnly(2020, 1, 1));
        var chronology = new EvidenceChronology(knownAt: new DateTimeOffset(new DateOnly(2020, 1, 2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        var quality = ScreenerEvidenceValidity.Evaluate(EvidenceClass.ContinuingState, scope, Day(5, 1), chronology, Cutoff);

        Assert.Equal(EvidenceQuality.Verified, quality);
    }

    [Fact]
    public void PointObservationDoesNotCarryForward()
    {
        var finite = new EffectiveInterval(Day(1, 1), Day(1, 31));
        var openEnded = new EffectiveInterval(Day(1, 1));
        var chronology = new EvidenceChronology(knownAt: Instant(Day(1, 1)));

        Assert.Equal(EvidenceQuality.Stale, ScreenerEvidenceValidity.Evaluate(EvidenceClass.PointObservation, finite, Day(2, 1), chronology, Cutoff));
        Assert.Equal(EvidenceQuality.Unknown, ScreenerEvidenceValidity.Evaluate(EvidenceClass.PointObservation, openEnded, Day(5, 1), chronology, Cutoff));
    }

    [Fact]
    public void VersionedRuleIsValidOnlyInsideItsInterval()
    {
        var scope = new EffectiveInterval(Day(1, 1), Day(6, 30));
        var chronology = new EvidenceChronology(knownAt: Instant(Day(1, 1)));

        Assert.Equal(EvidenceQuality.Verified, ScreenerEvidenceValidity.Evaluate(EvidenceClass.VersionedRule, scope, Day(3, 1), chronology, Cutoff));
        Assert.Equal(EvidenceQuality.Unknown, ScreenerEvidenceValidity.Evaluate(EvidenceClass.VersionedRule, scope, Day(8, 1), chronology, Cutoff));
    }

    [Fact]
    public void SessionFactIsScopedToItsExactDate()
    {
        var scope = new EffectiveInterval(Day(3, 4), Day(3, 4));
        var chronology = new EvidenceChronology(knownAt: Instant(Day(3, 4)));

        Assert.Equal(EvidenceQuality.Verified, ScreenerEvidenceValidity.Evaluate(EvidenceClass.SessionFact, scope, Day(3, 4), chronology, Cutoff));
        Assert.Equal(EvidenceQuality.Unknown, ScreenerEvidenceValidity.Evaluate(EvidenceClass.SessionFact, scope, Day(3, 5), chronology, Cutoff));
    }

    [Fact]
    public void ResolverPrefersHigherAuthorityTierWhenBothVisible()
    {
        var scope = new EffectiveInterval(Day(1, 1));
        var chronology = new EvidenceChronology(knownAt: Instant(Day(1, 1)));
        var governing = new EvidenceCandidate<string>("governing", scope, SourceAuthorityTier.T1Governing, chronology, 1);
        var provider = new EvidenceCandidate<string>("provider", scope, SourceAuthorityTier.T3ProviderObservation, chronology, 1);

        var result = ScreenerEvidenceResolver.Resolve([provider, governing], Day(2, 1), Cutoff);

        Assert.Equal("governing", result.Selected);
        Assert.False(result.Conflicting);
    }

    [Fact]
    public void ResolverAppliesExplicitCorrectionOnlyAtItsKnownCutoff()
    {
        var scope = new EffectiveInterval(Day(1, 1));
        var original = new EvidenceCandidate<string>(
            "original", scope, SourceAuthorityTier.T2AdmittedReference, new EvidenceChronology(knownAt: Instant(Day(1, 2))), 1, SourceId: "src");
        var correction = new EvidenceCandidate<string>(
            "correction", scope, SourceAuthorityTier.T2AdmittedReference, new EvidenceChronology(knownAt: Instant(Day(3, 2))), 2,
            SourceId: "src", SupersedesRevisionNumber: 1);

        var earlier = ScreenerEvidenceResolver.Resolve([original, correction], Day(4, 1), Instant(Day(2, 1)));
        var later = ScreenerEvidenceResolver.Resolve([original, correction], Day(4, 1), Instant(Day(4, 1)));

        Assert.Equal("original", earlier.Selected);
        Assert.Equal("correction", later.Selected);
    }

    [Fact]
    public void ResolverReportsConflictWhenLaterEvidenceIsNotACorrection()
    {
        var scope = new EffectiveInterval(Day(1, 1));
        var original = new EvidenceCandidate<string>(
            "original", scope, SourceAuthorityTier.T2AdmittedReference, new EvidenceChronology(knownAt: Instant(Day(1, 2))), 1, SourceId: "src-a");
        var merelyNewer = new EvidenceCandidate<string>(
            "merelyNewer", scope, SourceAuthorityTier.T2AdmittedReference, new EvidenceChronology(knownAt: Instant(Day(4, 2))), 1, SourceId: "src-b");

        var result = ScreenerEvidenceResolver.Resolve([original, merelyNewer], Day(5, 1), Instant(Day(6, 1)));

        Assert.True(result.Conflicting);
        Assert.Null(result.Selected);
    }

    [Fact]
    public void ResolverNeverLetsLaterLowerAuthorityOverrideHigherAuthority()
    {
        var scope = new EffectiveInterval(Day(1, 1));
        var governing = new EvidenceCandidate<string>(
            "governing", scope, SourceAuthorityTier.T1Governing, new EvidenceChronology(knownAt: Instant(Day(1, 2))), 1, SourceId: "g");
        var providerLater = new EvidenceCandidate<string>(
            "provider", scope, SourceAuthorityTier.T3ProviderObservation, new EvidenceChronology(knownAt: Instant(Day(5, 2))), 1, SourceId: "p");

        var result = ScreenerEvidenceResolver.Resolve([governing, providerLater], Day(6, 1), Instant(Day(7, 1)));

        Assert.Equal("governing", result.Selected);
        Assert.False(result.Conflicting);
    }

    [Fact]
    public void ResolverReportsUnresolvedSameTierConflict()
    {
        var scope = new EffectiveInterval(Day(1, 1));
        var chronology = new EvidenceChronology(knownAt: Instant(Day(1, 1)));
        var first = new EvidenceCandidate<string>("one", scope, SourceAuthorityTier.T1Governing, chronology, 1);
        var second = new EvidenceCandidate<string>("two", scope, SourceAuthorityTier.T1Governing, chronology, 1);

        var result = ScreenerEvidenceResolver.Resolve([first, second], Day(2, 1), Cutoff);

        Assert.True(result.Conflicting);
        Assert.Null(result.Selected);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void ResolverIsDeterministicRegardlessOfInputOrder()
    {
        var scope = new EffectiveInterval(Day(1, 1));
        var chronology = new EvidenceChronology(knownAt: Instant(Day(1, 1)));
        var governing = new EvidenceCandidate<string>("governing", scope, SourceAuthorityTier.T1Governing, chronology, 1);
        var provider = new EvidenceCandidate<string>("provider", scope, SourceAuthorityTier.T3ProviderObservation, chronology, 1);

        var forward = ScreenerEvidenceResolver.Resolve([governing, provider], Day(2, 1), Cutoff);
        var reversed = ScreenerEvidenceResolver.Resolve([provider, governing], Day(2, 1), Cutoff);

        Assert.Equal(forward.Selected, reversed.Selected);
    }

    [Fact]
    public void AuthoritativeTradingEvidenceEstablishesTrading()
    {
        var evidence = new[]
        {
            Status(TradingStatus.Trading, EvidenceQuality.Verified, new EffectiveInterval(Day(1, 1)), Instant(Day(1, 2)))
        };

        var result = ScreenerTradingStatusEvaluator.Evaluate(evidence, Day(5, 1), Cutoff);

        Assert.Equal(TradingStatus.Trading, result.Status);
        Assert.Equal(EvidenceQuality.Verified, result.Quality);
        Assert.Contains(ScreenerEvidenceReasons.StatusTrading, result.Reasons);
    }

    [Fact]
    public void ContinuingSuspensionEstablishesSuspended()
    {
        var evidence = new[]
        {
            Status(TradingStatus.Suspended, EvidenceQuality.Verified, new EffectiveInterval(Day(1, 1)), Instant(Day(1, 2)))
        };

        var result = ScreenerTradingStatusEvaluator.Evaluate(evidence, Day(5, 1), Cutoff);

        Assert.Equal(TradingStatus.Suspended, result.Status);
        Assert.Contains(ScreenerEvidenceReasons.Suspended, result.Reasons);
    }

    [Fact]
    public void ReopeningLearnedLateAppliesOnlyAfterItsKnownTime()
    {
        var evidence = new[]
        {
            Status(TradingStatus.Suspended, EvidenceQuality.Verified, new EffectiveInterval(Day(1, 1)), Instant(Day(1, 2))),
            Status(TradingStatus.Trading, EvidenceQuality.Verified, new EffectiveInterval(Day(3, 1)), Instant(Day(3, 2)))
        };

        var before = ScreenerTradingStatusEvaluator.Evaluate(evidence, Day(4, 1), Instant(Day(2, 1)));
        var after = ScreenerTradingStatusEvaluator.Evaluate(evidence, Day(4, 1), Instant(Day(4, 1)));

        Assert.Equal(TradingStatus.Suspended, before.Status);
        Assert.Equal(TradingStatus.Trading, after.Status);
    }

    [Fact]
    public void PriorTradingWithNoLaterCoverageIsUnknown()
    {
        var evidence = new[]
        {
            Status(TradingStatus.Trading, EvidenceQuality.Verified, new EffectiveInterval(Day(1, 1), Day(1, 31)), Instant(Day(1, 2)))
        };

        var result = ScreenerTradingStatusEvaluator.Evaluate(evidence, Day(2, 5), Cutoff);

        Assert.Equal(TradingStatus.Unknown, result.Status);
        Assert.Contains(ScreenerEvidenceReasons.StatusUnknown, result.Reasons);
    }

    [Fact]
    public void NoStatusEvidenceDoesNotProveTrading()
    {
        var result = ScreenerTradingStatusEvaluator.Evaluate([], Day(2, 5), Cutoff);

        Assert.Equal(TradingStatus.Unknown, result.Status);
        Assert.Equal(EvidenceQuality.Unknown, result.Quality);
    }

    [Fact]
    public void PartialStatusReportsCoveragePartial()
    {
        var evidence = new[]
        {
            Status(TradingStatus.Trading, EvidenceQuality.Partial, new EffectiveInterval(Day(1, 1)), Instant(Day(1, 2)), SourceAuthorityTier.T2AdmittedReference)
        };

        var result = ScreenerTradingStatusEvaluator.Evaluate(evidence, Day(5, 1), Cutoff);

        Assert.Equal(TradingStatus.Trading, result.Status);
        Assert.Contains(ScreenerEvidenceReasons.StatusCoveragePartial, result.Reasons);
    }

    [Fact]
    public void ConflictingStatusEvidenceIsConflicting()
    {
        var evidence = new[]
        {
            Status(TradingStatus.Suspended, EvidenceQuality.Verified, new EffectiveInterval(Day(1, 1)), Instant(Day(1, 2)), sourceId: "a"),
            Status(TradingStatus.Trading, EvidenceQuality.Verified, new EffectiveInterval(Day(1, 1)), Instant(Day(1, 2)), sourceId: "b")
        };

        var result = ScreenerTradingStatusEvaluator.Evaluate(evidence, Day(5, 1), Cutoff);

        Assert.Equal(EvidenceQuality.Conflicting, result.Quality);
        Assert.Contains(ScreenerEvidenceReasons.StatusConflict, result.Reasons);
    }

    [Fact]
    public void TradingStatusIgnoresLaterLowerAuthorityObservation()
    {
        var evidence = new[]
        {
            Status(TradingStatus.Suspended, EvidenceQuality.Verified, new EffectiveInterval(Day(1, 1)), Instant(Day(1, 2)), SourceAuthorityTier.T1Governing),
            Status(TradingStatus.Trading, EvidenceQuality.Partial, new EffectiveInterval(Day(3, 1)), Instant(Day(3, 2)), SourceAuthorityTier.T3ProviderObservation)
        };

        var result = ScreenerTradingStatusEvaluator.Evaluate(evidence, Day(4, 1), Instant(Day(5, 1)));

        Assert.Equal(TradingStatus.Suspended, result.Status);
    }

    [Fact]
    public void GenuineObservationAdmittedWhenAllConditionsMet()
    {
        var result = ScreenerPriceAdmission.Admit(Candidate(volume: 1000));

        Assert.True(result.Admitted);
        Assert.NotNull(result.Observation);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void PositiveVolumeAloneDoesNotAdmitWithoutSourceConvention()
    {
        var result = ScreenerPriceAdmission.Admit(Candidate(sourceConvention: false, volume: 1000));

        Assert.False(result.Admitted);
        Assert.Equal(ScreenerEvidenceReasons.PriceBasisUnverified, result.Reason);
    }

    [Fact]
    public void ZeroVolumeExplicitlyGenuineIsAdmitted()
    {
        var result = ScreenerPriceAdmission.Admit(Candidate(volume: 0, zero: ZeroVolumeSemantics.ExplicitlyGenuine));

        Assert.True(result.Admitted);
    }

    [Fact]
    public void AmbiguousZeroVolumeFailsClosed()
    {
        var result = ScreenerPriceAdmission.Admit(Candidate(volume: 0, zero: ZeroVolumeSemantics.Ambiguous));

        Assert.False(result.Admitted);
        Assert.Equal(ScreenerEvidenceReasons.ZeroVolumeUnexplained, result.Reason);
    }

    [Fact]
    public void SyntheticCarryForwardIsRejected()
    {
        var result = ScreenerPriceAdmission.Admit(Candidate(synthetic: true));

        Assert.False(result.Admitted);
        Assert.Equal(ScreenerEvidenceReasons.PriceSyntheticCarryForward, result.Reason);
    }

    [Fact]
    public void PlaceholderIsRejected()
    {
        var result = ScreenerPriceAdmission.Admit(Candidate(placeholder: true));

        Assert.False(result.Admitted);
        Assert.Equal(ScreenerEvidenceReasons.PricePlaceholder, result.Reason);
    }

    [Fact]
    public void SubstitutedPriceIsRejected()
    {
        var result = ScreenerPriceAdmission.Admit(Candidate(substituted: true));

        Assert.False(result.Admitted);
        Assert.Equal(ScreenerEvidenceReasons.PriceSubstituted, result.Reason);
    }

    [Fact]
    public void UnconfirmedSessionIsRejected()
    {
        var result = ScreenerPriceAdmission.Admit(Candidate(sessionCompleted: false));

        Assert.False(result.Admitted);
        Assert.Equal(ScreenerEvidenceReasons.SessionUnconfirmed, result.Reason);
    }

    [Fact]
    public void AllComparabilityPremisesProduceCleared()
    {
        var result = ScreenerPriceComparabilityEvaluator.Evaluate(ComparabilityRequest());

        Assert.Equal(PriceComparability.Cleared, result.State);
        Assert.Contains(ScreenerEvidenceReasons.PriceCleared, result.Reasons);
    }

    [Fact]
    public void PartialActionCoverageIsUnresolved()
    {
        var result = ScreenerPriceComparabilityEvaluator.Evaluate(ComparabilityRequest(actionCoverage: false));

        Assert.Equal(PriceComparability.Unresolved, result.State);
        Assert.Contains(ScreenerEvidenceReasons.CorporateActionCoveragePartial, result.Reasons);
    }

    [Theory]
    [InlineData(CorporateActionKind.Split)]
    [InlineData(CorporateActionKind.ReverseSplit)]
    [InlineData(CorporateActionKind.RightsOrShareEvent)]
    [InlineData(CorporateActionKind.Conversion)]
    [InlineData(CorporateActionKind.MergerOrReorganization)]
    public void KnownBreakEventProducesKnownBreak(CorporateActionKind kind)
    {
        var result = ScreenerPriceComparabilityEvaluator.Evaluate(ComparabilityRequest(breaks: [kind]));

        Assert.Equal(PriceComparability.KnownBreak, result.State);
        Assert.Contains(ScreenerEvidenceReasons.PriceKnownBreak, result.Reasons);
    }

    [Fact]
    public void MissingSourceConventionIsUnresolved()
    {
        var result = ScreenerPriceComparabilityEvaluator.Evaluate(ComparabilityRequest(rawConvention: false));

        Assert.Equal(PriceComparability.Unresolved, result.State);
        Assert.Contains(ScreenerEvidenceReasons.PriceBasisUnverified, result.Reasons);
    }

    [Fact]
    public void MissingRevisionBindingIsUnresolved()
    {
        var result = ScreenerPriceComparabilityEvaluator.Evaluate(ComparabilityRequest(revisionBinding: false));

        Assert.Equal(PriceComparability.Unresolved, result.State);
        Assert.Contains(ScreenerEvidenceReasons.PriceClearanceIncomplete, result.Reasons);
    }

    [Fact]
    public void CleanMarketFactsAreEligible()
    {
        var result = ScreenerMarketEligibilityEvaluator.Evaluate(Facts());

        Assert.Equal(EligibilityStatus.Eligible, result.Status);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void KnownSuspensionIsIneligible()
    {
        var facts = Facts(status: new TradingStatusResult(TradingStatus.Suspended, EvidenceQuality.Verified, [ScreenerEvidenceReasons.Suspended]));

        var result = ScreenerMarketEligibilityEvaluator.Evaluate(facts);

        Assert.Equal(EligibilityStatus.Ineligible, result.Status);
        Assert.Contains(ScreenerEvidenceReasons.Suspended, result.Reasons);
    }

    [Fact]
    public void PartialStatusIsDataBlocked()
    {
        var facts = Facts(status: new TradingStatusResult(TradingStatus.Trading, EvidenceQuality.Partial, [ScreenerEvidenceReasons.StatusCoveragePartial]));

        var result = ScreenerMarketEligibilityEvaluator.Evaluate(facts);

        Assert.Equal(EligibilityStatus.DataBlocked, result.Status);
        Assert.Contains(ScreenerEvidenceReasons.StatusCoveragePartial, result.Reasons);
    }

    [Fact]
    public void ConflictingBoardIsDataBlocked()
    {
        var result = ScreenerMarketEligibilityEvaluator.Evaluate(Facts(boardConflict: true));

        Assert.Equal(EligibilityStatus.DataBlocked, result.Status);
        Assert.Contains(ScreenerEvidenceReasons.BoardConflict, result.Reasons);
    }

    [Fact]
    public void PreListingIsIneligible()
    {
        var result = ScreenerMarketEligibilityEvaluator.Evaluate(Facts(preListing: true));

        Assert.Equal(EligibilityStatus.Ineligible, result.Status);
        Assert.Contains(ScreenerEvidenceReasons.PreListing, result.Reasons);
    }

    [Fact]
    public void UnsupportedBoardIsIneligible()
    {
        var result = ScreenerMarketEligibilityEvaluator.Evaluate(Facts(boardUnsupported: true));

        Assert.Equal(EligibilityStatus.Ineligible, result.Status);
        Assert.Contains(ScreenerEvidenceReasons.UnsupportedBoard, result.Reasons);
    }

    [Fact]
    public void TechnicalWarmupDoesNotMakeMarketIneligible()
    {
        var result = ScreenerMarketEligibilityEvaluator.Evaluate(Facts());

        Assert.Equal(EligibilityStatus.Eligible, result.Status);
    }

    [Fact]
    public void RequiredWarmupFeatureKeepsDataNotReady()
    {
        var result = ScreenerDataReadiness.Evaluate(
            EligibleMarket(),
            new PriceComparabilityResult(PriceComparability.Cleared),
            [Available(), Warmup()]);

        Assert.False(result.DataReady);
        Assert.False(result.CanEvaluateSetup);
        Assert.False(result.EvaluationAllowed);
    }

    [Fact]
    public void AllRequiredCoreInputsReadyAllowsEvaluationButDoesNotAssertExecution()
    {
        var result = ScreenerDataReadiness.Evaluate(
            EligibleMarket(),
            new PriceComparabilityResult(PriceComparability.Cleared),
            [Available(), Available()]);

        Assert.True(result.DataReady);
        Assert.True(result.CanEvaluateSetup);
        Assert.True(result.EvaluationAllowed);
        Assert.Null(typeof(ScreenerReadinessResult).GetProperty("TechnicalEvaluated"));
        Assert.Null(typeof(ScreenerReadinessResult).GetProperty("SetupEvaluated"));
    }

    [Fact]
    public void MarketFactsValidWithUnresolvedPriceBasisStayEligibleButNotDataReady()
    {
        var market = ScreenerMarketEligibilityEvaluator.Evaluate(Facts());
        var readiness = ScreenerDataReadiness.Evaluate(
            market,
            ScreenerPriceComparabilityEvaluator.Evaluate(ComparabilityRequest(rawConvention: false)),
            [Available()]);

        Assert.Equal(EligibilityStatus.Eligible, market.Status);
        Assert.False(readiness.DataReady);
        Assert.Contains(ScreenerEvidenceReasons.DataNotReady, readiness.Reasons);
    }

    [Fact]
    public void BlockedMarketNeverAllowsSetupEvenWithValidDiagnostics()
    {
        var result = ScreenerDataReadiness.Evaluate(
            new MarketEligibilityResult(EligibilityStatus.DataBlocked, [ScreenerEvidenceReasons.StatusUnknown]),
            new PriceComparabilityResult(PriceComparability.Cleared),
            [Available()]);

        Assert.False(result.DataReady);
        Assert.False(result.CanEvaluateSetup);
        Assert.Contains(ScreenerEvidenceReasons.MarketNotEligible, result.Reasons);
    }

    [Fact]
    public void UnresolvedComparabilityBlocksDataReady()
    {
        var result = ScreenerDataReadiness.Evaluate(
            EligibleMarket(),
            ScreenerPriceComparabilityEvaluator.Evaluate(ComparabilityRequest(actionCoverage: false)),
            [Available()]);

        Assert.False(result.DataReady);
        Assert.False(result.CanEvaluateSetup);
        Assert.Contains(ScreenerEvidenceReasons.DataNotReady, result.Reasons);
    }

    [Fact]
    public void OptionalBenchmarkMissingDoesNotBlockSetup()
    {
        var readiness = ScreenerDataReadiness.Evaluate(
            EligibleMarket(),
            new PriceComparabilityResult(PriceComparability.Cleared),
            [Available()]);
        var context = ScreenerOptionalFeatures.Evaluate(
            OptionalFeature.MarketContext,
            new OptionalFeatureInputs(true, true, true, BenchmarkAvailable: false, false, true));

        Assert.True(readiness.CanEvaluateSetup);
        Assert.Equal(Availability.UNAVAILABLE, context.Availability);
        Assert.Equal(ScreenerEvidenceReasons.BenchmarkMissing, context.UnavailableReason);
    }

    [Fact]
    public void KnownBreakForcesRestart()
    {
        var state = ScreenerTechnicalReadiness.Evaluate(
            TechnicalFeature.Ema20,
            new TechnicalWindowInputs(PriceComparability.KnownBreak, 100));

        Assert.Equal(Availability.UNAVAILABLE, state.Availability);
        Assert.Equal(ScreenerEvidenceReasons.PriceKnownBreak, state.UnavailableReason);
    }

    [Fact]
    public void InsufficientClearedHistoryIsWarmup()
    {
        var state = ScreenerTechnicalReadiness.Evaluate(
            TechnicalFeature.Ema20,
            new TechnicalWindowInputs(PriceComparability.Cleared, 10));

        Assert.Equal(Availability.WARMUP, state.Availability);
        Assert.Equal(ScreenerEvidenceReasons.InsufficientHistory, state.UnavailableReason);
    }

    [Fact]
    public void RelativeFeatureWithoutBenchmarkIsUnavailable()
    {
        var state = ScreenerTechnicalReadiness.Evaluate(
            TechnicalFeature.Rs20,
            new TechnicalWindowInputs(PriceComparability.Cleared, 100, BenchmarkAvailable: false));

        Assert.Equal(Availability.UNAVAILABLE, state.Availability);
        Assert.Equal(ScreenerEvidenceReasons.BenchmarkMissing, state.UnavailableReason);
    }

    [Fact]
    public void SufficientClearedHistoryIsAvailable()
    {
        var state = ScreenerTechnicalReadiness.Evaluate(
            TechnicalFeature.Rs20,
            new TechnicalWindowInputs(PriceComparability.Cleared, 30, BenchmarkAvailable: true, ConsecutiveClearedBenchmarkObservations: 30));

        Assert.Equal(Availability.AVAILABLE, state.Availability);
    }

    [Fact]
    public void UnknownVolumeBasisMakesVolumeFeaturesUnavailable()
    {
        var state = ScreenerOptionalFeatures.Evaluate(
            OptionalFeature.RelativeVolume,
            new OptionalFeatureInputs(false, true, true, true, true, true));

        Assert.Equal(Availability.UNAVAILABLE, state.Availability);
        Assert.Equal(ScreenerEvidenceReasons.VolumeBasisUnverified, state.UnavailableReason);
    }
}
