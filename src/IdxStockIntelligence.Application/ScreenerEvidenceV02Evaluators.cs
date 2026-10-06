using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public enum EvidenceClaim
{
    StableIdentity = 0,
    SecurityType = 1,
    Currency = 2,
    ListingCoverage = 3,
    Delisting = 4,
    BoardRegime = 5,
    BoardChange = 6,
    ExchangeRuleVersion = 7,
    MechanismException = 8,
    Suspension = 9,
    Reopening = 10,
    ScheduledSession = 11,
    CompletedSession = 12,
    CorporateAction = 13,
    SourcePriceConvention = 14,
    GenuinePriceObservation = 15,
    PriceComparability = 16,
    TradingStatus = 17
}

public sealed record SourceAdmissionResult(bool Admitted, string? Reason);

public static class ScreenerSourceAdmission
{
    private static int HighestPermittedTier(EvidenceClaim claim) => claim switch
    {
        EvidenceClaim.Delisting => 0,
        EvidenceClaim.Suspension => 0,
        EvidenceClaim.Reopening => 0,
        EvidenceClaim.StableIdentity => 1,
        EvidenceClaim.SecurityType => 1,
        EvidenceClaim.Currency => 1,
        EvidenceClaim.ListingCoverage => 1,
        EvidenceClaim.BoardRegime => 1,
        EvidenceClaim.BoardChange => 1,
        EvidenceClaim.ExchangeRuleVersion => 1,
        EvidenceClaim.MechanismException => 1,
        EvidenceClaim.ScheduledSession => 1,
        EvidenceClaim.CompletedSession => 1,
        EvidenceClaim.CorporateAction => 1,
        EvidenceClaim.SourcePriceConvention => 2,
        EvidenceClaim.GenuinePriceObservation => 2,
        EvidenceClaim.PriceComparability => 3,
        EvidenceClaim.TradingStatus => 1,
        _ => -1
    };

    public static SourceAdmissionResult Admit(EvidenceClaim claim, SourceAuthorityTier tier)
    {
        if (tier == SourceAuthorityTier.T5Secondary)
        {
            return new(false, ScreenerEvidenceReasons.EvidenceSourceInadmissible);
        }

        var highest = HighestPermittedTier(claim);
        if (highest < 0 || (int)tier > highest)
        {
            return new(false, ScreenerEvidenceReasons.EvidenceSourceInadmissible);
        }

        return new(true, null);
    }
}

public enum EvidenceClass
{
    ContinuingState = 0,
    PointObservation = 1,
    VersionedRule = 2,
    SessionFact = 3,
    SourceConvention = 4
}

public static class ScreenerEvidenceValidity
{
    public static bool Visible(EvidenceChronology chronology, DateTimeOffset cutoff)
    {
        ArgumentNullException.ThrowIfNull(chronology);
        if (chronology.KnownAt is not { } known || known > cutoff)
        {
            return false;
        }

        if (chronology.RetrievedAt is { } retrieved && retrieved > cutoff)
        {
            return false;
        }

        if (chronology.PublicationAt is { } published && published > cutoff)
        {
            return false;
        }

        return true;
    }

    public static EvidenceQuality Evaluate(
        EvidenceClass evidenceClass,
        EffectiveInterval scope,
        DateOnly evaluationDate,
        EvidenceChronology chronology,
        DateTimeOffset cutoff)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(chronology);

        if (!Visible(chronology, cutoff))
        {
            return EvidenceQuality.Unknown;
        }

        return evidenceClass switch
        {
            EvidenceClass.SessionFact => scope.From == evaluationDate && scope.To == evaluationDate
                ? EvidenceQuality.Verified
                : EvidenceQuality.Unknown,
            EvidenceClass.PointObservation => PointObservation(scope, evaluationDate),
            EvidenceClass.VersionedRule => scope.Contains(evaluationDate) ? EvidenceQuality.Verified : EvidenceQuality.Unknown,
            EvidenceClass.ContinuingState => Interval(scope, evaluationDate),
            EvidenceClass.SourceConvention => Interval(scope, evaluationDate),
            _ => EvidenceQuality.Unknown
        };
    }

    private static EvidenceQuality PointObservation(EffectiveInterval scope, DateOnly evaluationDate)
    {
        if (scope.To is not { } to)
        {
            return EvidenceQuality.Unknown;
        }

        if (evaluationDate < scope.From)
        {
            return EvidenceQuality.Unknown;
        }

        return evaluationDate <= to ? EvidenceQuality.Verified : EvidenceQuality.Stale;
    }

    private static EvidenceQuality Interval(EffectiveInterval scope, DateOnly evaluationDate)
    {
        if (evaluationDate < scope.From)
        {
            return EvidenceQuality.Unknown;
        }

        return scope.To is { } to && evaluationDate > to ? EvidenceQuality.Stale : EvidenceQuality.Verified;
    }
}

// RevisionSeriesId identifies the logical source record / claim revision series.
// It is NOT provider/source identity: two records published by the same provider
// have different series identities and never supersede one another.
public sealed record EvidenceCandidate<T>(
    T Value,
    EffectiveInterval Scope,
    SourceAuthorityTier Tier,
    EvidenceChronology Chronology,
    long RevisionNumber,
    int Specificity = 0,
    string? RevisionSeriesId = null,
    long? SupersedesRevisionNumber = null);

public sealed record EvidenceResolution<T>(T? Selected, bool Conflicting, IReadOnlyList<T> Candidates)
{
    public bool Resolved => !Conflicting && Selected is not null;
}

public static class ScreenerEvidenceResolver
{
    public static EvidenceResolution<T> Resolve<T>(
        IEnumerable<EvidenceCandidate<T>> candidates,
        DateOnly evaluationDate,
        DateTimeOffset cutoff)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var applicable = candidates
            .Where(c => c.Scope.Contains(evaluationDate) && ScreenerEvidenceValidity.Visible(c.Chronology, cutoff))
            .ToArray();
        if (applicable.Length == 0)
        {
            return new(default, false, []);
        }

        // Authority and scope specificity dominate. Chronology is not a tie-break;
        // a later record supersedes an earlier one only through an admitted relationship.
        var topTier = applicable.Min(c => (int)c.Tier);
        var topSpecificity = applicable.Where(c => (int)c.Tier == topTier).Max(c => c.Specificity);
        var group = applicable
            .Where(c => (int)c.Tier == topTier && c.Specificity == topSpecificity)
            .ToArray();

        var survivors = group.Where(c => !group.Any(other => !ReferenceEquals(c, other) && Supersedes(other, c))).ToArray();
        if (survivors.Length == 0)
        {
            return new(default, true, group.Select(c => c.Value).ToArray());
        }

        if (survivors.Select(c => c.Value).Distinct().Count() > 1)
        {
            return new(default, true, survivors.Select(c => c.Value).ToArray());
        }

        var winner = survivors
            .OrderByDescending(c => c.Chronology.KnownAt)
            .ThenByDescending(c => c.RevisionNumber)
            .ThenBy(c => c.RevisionSeriesId, StringComparer.Ordinal)
            .First();
        return new(winner.Value, false, applicable.Select(c => c.Value).ToArray());
    }

    private static bool Supersedes<T>(EvidenceCandidate<T> candidate, EvidenceCandidate<T> other)
    {
        // Supersession requires the same logical revision series. Provider/source
        // identity and bare revision-number coincidence are never sufficient.
        if (candidate.RevisionSeriesId is not { } series || series != other.RevisionSeriesId)
        {
            return false;
        }

        if (candidate.SupersedesRevisionNumber is { } superseded && superseded == other.RevisionNumber)
        {
            return true;
        }

        return candidate.RevisionNumber > other.RevisionNumber;
    }
}

public sealed record TradingStatusResult
{
    public TradingStatusResult(TradingStatus status, EvidenceQuality quality, IReadOnlyList<string>? reasons = null)
    {
        Status = status;
        Quality = quality;
        Reasons = ScreenerEvidenceReasons.Canonical(reasons ?? []);
    }

    public TradingStatus Status { get; }
    public EvidenceQuality Quality { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public static class ScreenerTradingStatusEvaluator
{
    public static TradingStatusResult Evaluate(
        IEnumerable<TradingStatusEvidence> evidence,
        DateOnly evaluationDate,
        DateTimeOffset cutoff)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var applicable = evidence
            .Where(e => e.Scope.Contains(evaluationDate) && ScreenerEvidenceValidity.Visible(e.Chronology, cutoff))
            .ToArray();
        if (applicable.Length == 0)
        {
            return new(TradingStatus.Unknown, EvidenceQuality.Unknown, [ScreenerEvidenceReasons.StatusUnknown]);
        }

        // Authority dominates recency: a later lower-authority observation never
        // overrides applicable higher-authority evidence. Within the highest
        // authority tier, the latest effective state transition applies.
        var topTier = applicable.Min(e => (int)e.Tier);
        var tierGroup = applicable.Where(e => (int)e.Tier == topTier).ToArray();
        var latestEffective = tierGroup.Max(e => e.Scope.From);
        var latest = tierGroup.Where(e => e.Scope.From == latestEffective).ToArray();
        if (latest.Select(e => e.Status).Distinct().Count() > 1)
        {
            return new(TradingStatus.Unknown, EvidenceQuality.Conflicting, [ScreenerEvidenceReasons.StatusConflict]);
        }

        var chosen = latest
            .OrderBy(e => (int)e.Quality)
            .ThenBy(e => e.SourceId, StringComparer.Ordinal)
            .First();

        var reasons = new List<string>();
        switch (chosen.Status)
        {
            case TradingStatus.Trading:
                reasons.Add(ScreenerEvidenceReasons.StatusTrading);
                break;
            case TradingStatus.Suspended:
                reasons.Add(ScreenerEvidenceReasons.Suspended);
                break;
            case TradingStatus.ReopeningUnconfirmed:
                reasons.Add(ScreenerEvidenceReasons.ReopeningUnconfirmed);
                break;
            default:
                reasons.Add(ScreenerEvidenceReasons.StatusUnknown);
                break;
        }

        switch (chosen.Quality)
        {
            case EvidenceQuality.Partial:
                reasons.Add(ScreenerEvidenceReasons.StatusCoveragePartial);
                break;
            case EvidenceQuality.Stale:
                reasons.Add(ScreenerEvidenceReasons.StatusStale);
                break;
            case EvidenceQuality.Conflicting:
                reasons.Add(ScreenerEvidenceReasons.StatusConflict);
                break;
            default:
                break;
        }

        return new(chosen.Status, chosen.Quality, reasons);
    }
}

public enum ZeroVolumeSemantics
{
    NotApplicable = 0,
    ExplicitlyGenuine = 1,
    Ambiguous = 2
}

public sealed record PriceObservationCandidate(
    DailyBar Bar,
    long RevisionNumber,
    DateTimeOffset KnownAt,
    DateTimeOffset? RetrievedAt,
    string ContentHash,
    bool SourceConventionAdmitted,
    bool SessionCompleted,
    bool SyntheticOrCarryForward,
    bool Placeholder,
    bool Substituted,
    ZeroVolumeSemantics ZeroVolumeSemantics);

public sealed record PriceAdmissionResult
{
    public PriceAdmissionResult(GenuinePriceObservation? observation, string? reason)
    {
        Observation = observation;
        Reason = reason;
    }

    public GenuinePriceObservation? Observation { get; }
    public string? Reason { get; }
    public bool Admitted => Observation is not null && Reason is null;
}

public static class ScreenerPriceAdmission
{
    public static PriceAdmissionResult Admit(PriceObservationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!candidate.SourceConventionAdmitted)
        {
            return new(null, ScreenerEvidenceReasons.PriceBasisUnverified);
        }

        if (!candidate.SessionCompleted)
        {
            return new(null, ScreenerEvidenceReasons.SessionUnconfirmed);
        }

        if (candidate.SyntheticOrCarryForward)
        {
            return new(null, ScreenerEvidenceReasons.PriceSyntheticCarryForward);
        }

        if (candidate.Placeholder)
        {
            return new(null, ScreenerEvidenceReasons.PricePlaceholder);
        }

        if (candidate.Substituted)
        {
            return new(null, ScreenerEvidenceReasons.PriceSubstituted);
        }

        if (candidate.Bar.Volume == 0 && candidate.ZeroVolumeSemantics != ZeroVolumeSemantics.ExplicitlyGenuine)
        {
            return new(null, ScreenerEvidenceReasons.ZeroVolumeUnexplained);
        }

        if (!ValidProvenance(candidate))
        {
            return new(null, ScreenerEvidenceReasons.PriceInvariantInvalid);
        }

        return new(
            new GenuinePriceObservation(
                candidate.Bar,
                candidate.RevisionNumber,
                candidate.KnownAt,
                candidate.RetrievedAt,
                candidate.ContentHash),
            null);
    }

    private static bool ValidProvenance(PriceObservationCandidate candidate)
    {
        if (candidate.RevisionNumber <= 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(candidate.ContentHash)
            || candidate.ContentHash.Length != 64
            || !candidate.ContentHash.All(Uri.IsHexDigit))
        {
            return false;
        }

        var retrieved = candidate.RetrievedAt;
        return retrieved is null || candidate.KnownAt >= retrieved.Value;
    }
}

public enum CorporateActionKind
{
    Split = 0,
    ReverseSplit = 1,
    RightsOrShareEvent = 2,
    BonusOrShareDistribution = 3,
    Conversion = 4,
    MergerOrReorganization = 5
}

public sealed record PriceComparabilityRequest(
    bool IdentityContinuity,
    bool CurrencyUnitContinuity,
    bool RawPriceConventionContinuity,
    bool SessionContinuity,
    bool CorporateActionCoverageComplete,
    bool RevisionBinding,
    IReadOnlyList<CorporateActionKind> KnownBreakEvents);

public static class ScreenerPriceComparabilityEvaluator
{
    public static PriceComparabilityResult Evaluate(PriceComparabilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.KnownBreakEvents.Count > 0)
        {
            return new(PriceComparability.KnownBreak, [ScreenerEvidenceReasons.PriceKnownBreak]);
        }

        var reasons = new List<string>();
        if (!request.IdentityContinuity)
        {
            reasons.Add(ScreenerEvidenceReasons.IdentityConflict);
        }

        if (!request.CurrencyUnitContinuity)
        {
            reasons.Add(ScreenerEvidenceReasons.PriceBasisUnverified);
        }

        if (!request.RawPriceConventionContinuity)
        {
            reasons.Add(ScreenerEvidenceReasons.PriceBasisUnverified);
        }

        if (!request.SessionContinuity)
        {
            reasons.Add(ScreenerEvidenceReasons.SessionUnavailable);
        }

        if (!request.CorporateActionCoverageComplete)
        {
            reasons.Add(ScreenerEvidenceReasons.CorporateActionCoveragePartial);
        }

        if (!request.RevisionBinding)
        {
            reasons.Add(ScreenerEvidenceReasons.PriceClearanceIncomplete);
        }

        return reasons.Count > 0
            ? new(PriceComparability.Unresolved, reasons)
            : new(PriceComparability.Cleared, [ScreenerEvidenceReasons.PriceCleared]);
    }
}

public sealed record MarketEvidenceFacts(
    bool IdentityVerified,
    bool IdentityConflict,
    bool SecurityTypeKnown,
    bool SecurityTypeUnsupported,
    bool ListingKnown,
    bool PreListing,
    bool PostDelisting,
    bool BoardKnown,
    bool BoardUnsupported,
    bool BoardConflict,
    bool MechanismKnown,
    bool MechanismUnresolved,
    TradingStatusResult TradingStatus,
    bool TargetSessionCompleted);

public static class ScreenerMarketEligibilityEvaluator
{
    public static MarketEligibilityResult Evaluate(MarketEvidenceFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.IdentityConflict)
        {
            return Blocked(ScreenerEvidenceReasons.IdentityConflict);
        }

        if (!facts.IdentityVerified)
        {
            return Blocked(ScreenerEvidenceReasons.ReferenceNotKnown);
        }

        if (facts.SecurityTypeUnsupported)
        {
            return Ineligible(ScreenerEvidenceReasons.UnsupportedType);
        }

        if (!facts.SecurityTypeKnown)
        {
            return Blocked(ScreenerEvidenceReasons.TypeUnknown);
        }

        if (facts.PreListing)
        {
            return Ineligible(ScreenerEvidenceReasons.PreListing);
        }

        if (facts.PostDelisting)
        {
            return Ineligible(ScreenerEvidenceReasons.PostDelisting);
        }

        if (!facts.ListingKnown)
        {
            return Blocked(ScreenerEvidenceReasons.ListingUnknown);
        }

        if (facts.BoardConflict)
        {
            return Blocked(ScreenerEvidenceReasons.BoardConflict);
        }

        if (facts.MechanismUnresolved)
        {
            return Blocked(ScreenerEvidenceReasons.MechanismUnresolved);
        }

        if (!facts.BoardKnown)
        {
            return Blocked(ScreenerEvidenceReasons.BoardUnknown);
        }

        if (facts.BoardUnsupported)
        {
            return Ineligible(ScreenerEvidenceReasons.UnsupportedBoard);
        }

        if (!facts.MechanismKnown)
        {
            return Blocked(ScreenerEvidenceReasons.MechanismUnresolved);
        }

        if (facts.TradingStatus.Status == TradingStatus.Suspended)
        {
            return Ineligible(ScreenerEvidenceReasons.Suspended);
        }

        if (facts.TradingStatus.Status != TradingStatus.Trading
            || facts.TradingStatus.Quality != EvidenceQuality.Verified)
        {
            return Blocked(StatusReason(facts.TradingStatus));
        }

        if (!facts.TargetSessionCompleted)
        {
            return Blocked(ScreenerEvidenceReasons.SessionUnconfirmed);
        }

        return new(EligibilityStatus.Eligible);
    }

    private static string StatusReason(TradingStatusResult status) => status.Quality switch
    {
        EvidenceQuality.Partial => ScreenerEvidenceReasons.StatusCoveragePartial,
        EvidenceQuality.Stale => ScreenerEvidenceReasons.StatusStale,
        EvidenceQuality.Conflicting => ScreenerEvidenceReasons.StatusConflict,
        _ => status.Status == TradingStatus.ReopeningUnconfirmed
            ? ScreenerEvidenceReasons.ReopeningUnconfirmed
            : ScreenerEvidenceReasons.StatusUnknown
    };

    private static MarketEligibilityResult Blocked(string reason) =>
        new(EligibilityStatus.DataBlocked, [reason]);

    private static MarketEligibilityResult Ineligible(string reason) =>
        new(EligibilityStatus.Ineligible, [reason]);
}

public sealed record ScreenerReadinessResult(
    EligibilityStatus MarketEligibility,
    bool DataReady,
    bool CanEvaluateSetup,
    bool EvaluationAllowed,
    IReadOnlyList<string> Reasons);

public static class ScreenerDataReadiness
{
    public static ScreenerReadinessResult Evaluate(
        MarketEligibilityResult market,
        PriceComparabilityResult comparability,
        IReadOnlyList<FeatureState> requiredCoreFeatures)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(comparability);
        ArgumentNullException.ThrowIfNull(requiredCoreFeatures);

        var marketEligible = market.Status == EligibilityStatus.Eligible;
        var comparabilityCleared = comparability.State == PriceComparability.Cleared;
        var coreReady = requiredCoreFeatures.Count > 0
            && requiredCoreFeatures.All(f => f.Availability == Availability.AVAILABLE);
        var dataReady = marketEligible && comparabilityCleared && coreReady;

        // Readiness means the setup may be evaluated; it never asserts that the setup
        // actually executed. TECHNICAL_EVALUATED belongs to the future executor.
        var canEvaluateSetup = ScreenerFunnel.SetupEvaluationAllowed(marketEligible, dataReady) && coreReady;
        var evaluationAllowed = canEvaluateSetup;

        var reasons = new List<string>();
        if (!marketEligible)
        {
            reasons.Add(ScreenerEvidenceReasons.MarketNotEligible);
            reasons.AddRange(market.Reasons);
        }

        if (!comparabilityCleared)
        {
            reasons.Add(ScreenerEvidenceReasons.DataNotReady);
            reasons.AddRange(comparability.Reasons);
        }

        foreach (var feature in requiredCoreFeatures)
        {
            if (feature.Availability != Availability.AVAILABLE)
            {
                reasons.Add(feature.UnavailableReason ?? ScreenerEvidenceReasons.InsufficientHistory);
            }
        }

        return new(market.Status, dataReady, canEvaluateSetup, evaluationAllowed, ScreenerEvidenceReasons.Canonical(reasons));
    }
}

public enum TechnicalFeature
{
    Ema20 = 0,
    Ema50 = 1,
    Atr14 = 2,
    PriorHigh20 = 3,
    PriorLow20 = 4,
    DistanceToHighPercent = 5,
    Rs20 = 6,
    Rs60 = 7
}

public sealed record TechnicalWindowInputs(
    PriceComparability Comparability,
    int ConsecutiveClearedObservations,
    bool BenchmarkAvailable = true,
    int ConsecutiveClearedBenchmarkObservations = 0);

public static class ScreenerTechnicalReadiness
{
    public static FeatureState Evaluate(TechnicalFeature feature, TechnicalWindowInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Comparability != PriceComparability.Cleared)
        {
            return new(
                Availability.UNAVAILABLE,
                null,
                inputs.Comparability == PriceComparability.KnownBreak
                    ? ScreenerEvidenceReasons.PriceKnownBreak
                    : ScreenerEvidenceReasons.PriceUnresolved);
        }

        var required = RequiredObservations(feature);
        if (IsRelative(feature))
        {
            if (!inputs.BenchmarkAvailable)
            {
                return new(Availability.UNAVAILABLE, null, ScreenerEvidenceReasons.BenchmarkMissing);
            }

            if (inputs.ConsecutiveClearedBenchmarkObservations < required)
            {
                return new(Availability.WARMUP, null, ScreenerEvidenceReasons.InsufficientHistory);
            }
        }

        return inputs.ConsecutiveClearedObservations < required
            ? new(Availability.WARMUP, null, ScreenerEvidenceReasons.InsufficientHistory)
            : new(Availability.AVAILABLE, null, null);
    }

    private static bool IsRelative(TechnicalFeature feature) =>
        feature is TechnicalFeature.Rs20 or TechnicalFeature.Rs60;

    private static int RequiredObservations(TechnicalFeature feature) => feature switch
    {
        TechnicalFeature.Ema20 => 20,
        TechnicalFeature.Ema50 => 50,
        TechnicalFeature.Atr14 => 15,
        TechnicalFeature.PriorHigh20 => 21,
        TechnicalFeature.PriorLow20 => 21,
        TechnicalFeature.DistanceToHighPercent => 21,
        TechnicalFeature.Rs20 => 21,
        TechnicalFeature.Rs60 => 61,
        _ => int.MaxValue
    };
}

public enum OptionalFeature
{
    RelativeVolume = 0,
    MonetaryLiquidityProxy = 1,
    ActualTradedValue = 2,
    Rs20 = 3,
    Rs60 = 4,
    MarketContext = 5
}

public sealed record OptionalFeatureInputs(
    bool ComparableQuantitySemantics,
    bool CompatiblePriceQuantityCurrency,
    bool TradedValueSemanticsAdmitted,
    bool BenchmarkAvailable,
    bool BenchmarkHistorySufficient,
    bool StockHistorySufficient);

public static class ScreenerOptionalFeatures
{
    public static FeatureState Evaluate(OptionalFeature feature, OptionalFeatureInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        return feature switch
        {
            OptionalFeature.RelativeVolume => inputs.ComparableQuantitySemantics
                ? History(inputs.StockHistorySufficient)
                : new(Availability.UNAVAILABLE, null, ScreenerEvidenceReasons.VolumeBasisUnverified),
            OptionalFeature.MonetaryLiquidityProxy => inputs.CompatiblePriceQuantityCurrency
                ? History(inputs.StockHistorySufficient)
                : new(Availability.UNAVAILABLE, null, ScreenerEvidenceReasons.VolumeBasisUnverified),
            OptionalFeature.ActualTradedValue => inputs.TradedValueSemanticsAdmitted
                ? History(inputs.StockHistorySufficient)
                : new(Availability.UNAVAILABLE, null, ScreenerEvidenceReasons.PriceBasisUnverified),
            OptionalFeature.Rs20 or OptionalFeature.Rs60 or OptionalFeature.MarketContext =>
                inputs.BenchmarkAvailable
                    ? History(inputs.BenchmarkHistorySufficient)
                    : new(Availability.UNAVAILABLE, null, ScreenerEvidenceReasons.BenchmarkMissing),
            _ => new(Availability.UNAVAILABLE, null, ScreenerEvidenceReasons.EvidenceUnknown)
        };
    }

    private static FeatureState History(bool sufficient) => sufficient
        ? new(Availability.AVAILABLE, null, null)
        : new(Availability.WARMUP, null, ScreenerEvidenceReasons.InsufficientHistory);
}
