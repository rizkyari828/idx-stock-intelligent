using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public static class ScreenerEvidenceV02
{
    public const string PolicyId = "screener-evidence-v0.2.0";
    public const int SchemaVersion = 1;

    public static bool Supports(string? policyId, int schemaVersion) =>
        policyId == PolicyId && schemaVersion == SchemaVersion;
}

public enum EvidenceQuality
{
    Verified = 0,
    Partial = 1,
    Unknown = 2,
    Conflicting = 3,
    Stale = 4
}

public enum SourceAuthorityTier
{
    T1Governing = 0,
    T2AdmittedReference = 1,
    T3ProviderObservation = 2,
    T4Derivation = 3,
    T5Secondary = 4
}

public enum PriceComparability
{
    Cleared = 0,
    KnownBreak = 1,
    Unresolved = 2
}

public enum TradingStatus
{
    Trading = 0,
    Suspended = 1,
    ReopeningUnconfirmed = 2,
    Unknown = 3
}

public enum ScreenerFunnelStage
{
    MasterUniverse = 0,
    MarketEligible = 1,
    DataReady = 2,
    TechnicalEvaluated = 3,
    DecisionCandidate = 4
}

public sealed record EffectiveInterval
{
    public EffectiveInterval(DateOnly from, DateOnly? to = null)
    {
        if (to is not null && to < from)
        {
            throw new ArgumentException("Effective interval end cannot precede its start.", nameof(to));
        }

        From = from;
        To = to;
    }

    public DateOnly From { get; }
    public DateOnly? To { get; }

    public bool Contains(DateOnly date) => date >= From && (To is null || date <= To);
}

public sealed record EvidenceChronology
{
    public EvidenceChronology(
        DateTimeOffset? effectiveAt = null,
        DateTimeOffset? publicationAt = null,
        DateTimeOffset? retrievedAt = null,
        DateTimeOffset? knownAt = null,
        DateTimeOffset? recordedAt = null)
    {
        if (knownAt is not null && retrievedAt is not null && knownAt < retrievedAt)
        {
            throw new ArgumentException("Known/admitted time cannot precede retrieval time.", nameof(knownAt));
        }

        EffectiveAt = effectiveAt;
        PublicationAt = publicationAt;
        RetrievedAt = retrievedAt;
        KnownAt = knownAt;
        RecordedAt = recordedAt;
    }

    public DateTimeOffset? EffectiveAt { get; }
    public DateTimeOffset? PublicationAt { get; }
    public DateTimeOffset? RetrievedAt { get; }
    public DateTimeOffset? KnownAt { get; }
    public DateTimeOffset? RecordedAt { get; }

    public bool KnownAdmitted => KnownAt is not null;
}

public sealed record EvidenceFact<T>
{
    public EvidenceFact(
        T value,
        EvidenceQuality quality,
        SourceAuthorityTier tier,
        string? sourceId,
        EvidenceChronology chronology,
        EffectiveInterval? scope = null)
    {
        ArgumentNullException.ThrowIfNull(chronology);

        if (quality == EvidenceQuality.Unknown && value is not null)
        {
            throw new ArgumentException("Missing evidence is UNKNOWN with no value.", nameof(value));
        }

        if (quality == EvidenceQuality.Verified)
        {
            if (value is null)
            {
                throw new ArgumentException("A VERIFIED fact requires a value.", nameof(value));
            }

            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("A VERIFIED fact requires a source identity.", nameof(sourceId));
            }

            if (scope is null)
            {
                throw new ArgumentException("A VERIFIED fact requires an effective scope.", nameof(scope));
            }

            if (chronology.KnownAt is null)
            {
                throw new ArgumentException("A VERIFIED fact requires a known/admitted time.", nameof(chronology));
            }
        }

        Value = value;
        Quality = quality;
        Tier = tier;
        SourceId = sourceId;
        Chronology = chronology;
        Scope = scope;
    }

    public T Value { get; }
    public EvidenceQuality Quality { get; }
    public SourceAuthorityTier Tier { get; }
    public string? SourceId { get; }
    public EvidenceChronology Chronology { get; }
    public EffectiveInterval? Scope { get; }
}

public sealed record TradingStatusEvidence
{
    public TradingStatusEvidence(
        TradingStatus status,
        EvidenceQuality quality,
        SourceAuthorityTier tier,
        string sourceId,
        EvidenceChronology chronology,
        EffectiveInterval scope)
    {
        ArgumentNullException.ThrowIfNull(chronology);
        ArgumentNullException.ThrowIfNull(scope);

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Trading status evidence requires a source identity.", nameof(sourceId));
        }

        if (quality == EvidenceQuality.Verified
            && tier is not (SourceAuthorityTier.T1Governing or SourceAuthorityTier.T2AdmittedReference))
        {
            throw new ArgumentException("VERIFIED trading status requires an authoritative (T1/T2) source.", nameof(tier));
        }

        Status = status;
        Quality = quality;
        Tier = tier;
        SourceId = sourceId;
        Chronology = chronology;
        Scope = scope;
    }

    public TradingStatus Status { get; }
    public EvidenceQuality Quality { get; }
    public SourceAuthorityTier Tier { get; }
    public string SourceId { get; }
    public EvidenceChronology Chronology { get; }
    public EffectiveInterval Scope { get; }
}

public sealed record GenuinePriceObservation
{
    public GenuinePriceObservation(
        DailyBar bar,
        long revisionNumber,
        DateTimeOffset knownAt,
        DateTimeOffset? retrievedAt,
        string contentHash)
    {
        ArgumentNullException.ThrowIfNull(bar);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revisionNumber);

        if (retrievedAt is not null && knownAt < retrievedAt)
        {
            throw new ArgumentException("Known/admitted time cannot precede retrieval time.", nameof(knownAt));
        }

        if (string.IsNullOrWhiteSpace(contentHash) || contentHash.Length != 64 || !contentHash.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("A SHA-256 hex digest is required.", nameof(contentHash));
        }

        Bar = bar;
        RevisionNumber = revisionNumber;
        KnownAt = knownAt;
        RetrievedAt = retrievedAt;
        ContentHash = contentHash.ToLowerInvariant();
    }

    public DailyBar Bar { get; }
    public long RevisionNumber { get; }
    public DateTimeOffset KnownAt { get; }
    public DateTimeOffset? RetrievedAt { get; }
    public string ContentHash { get; }
}

public sealed record PriceComparabilityResult
{
    public PriceComparabilityResult(PriceComparability state, IReadOnlyList<string>? reasons = null)
    {
        reasons ??= [];
        if (state == PriceComparability.Cleared && reasons.Any(ScreenerEvidenceReasons.ClearedBlocking.Contains))
        {
            throw new ArgumentException("CLEARED cannot carry an unresolved/known-break reason.", nameof(reasons));
        }

        State = state;
        Reasons = ScreenerEvidenceReasons.Canonical(reasons);
    }

    public PriceComparability State { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed record MarketEligibilityResult
{
    public MarketEligibilityResult(EligibilityStatus status, IReadOnlyList<string>? reasons = null)
    {
        reasons ??= [];
        if (status == EligibilityStatus.Eligible && reasons.Count > 0)
        {
            throw new ArgumentException("ELIGIBLE market eligibility carries no blocking reason.", nameof(reasons));
        }

        Status = status;
        Reasons = ScreenerEvidenceReasons.Canonical(reasons);
    }

    public EligibilityStatus Status { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public static class ScreenerFunnel
{
    public static bool SetupEvaluationAllowed(bool marketEligibleProved, bool dataReady) =>
        marketEligibleProved && dataReady;
}

public static class ScreenerEvidenceReasons
{
    public const string IdentityConflict = "IDENTITY_CONFLICT";
    public const string ReferenceNotKnown = "REFERENCE_NOT_KNOWN";
    public const string TypeUnknown = "TYPE_UNKNOWN";
    public const string PreListing = "PRE_LISTING";
    public const string PostDelisting = "POST_DELISTING";
    public const string ListingUnknown = "LISTING_UNKNOWN";
    public const string UnsupportedType = "UNSUPPORTED_TYPE";
    public const string UnsupportedBoard = "UNSUPPORTED_BOARD";
    public const string BoardUnknown = "BOARD_UNKNOWN";
    public const string BoardConflict = "BOARD_CONFLICT";
    public const string MechanismUnresolved = "MECHANISM_UNRESOLVED";
    public const string ExchangeRuleUnknown = "EXCHANGE_RULE_UNKNOWN";
    public const string SessionUnconfirmed = "SESSION_UNCONFIRMED";
    public const string SessionUnavailable = "SESSION_UNAVAILABLE";
    public const string NoTrade = "NO_TRADE";
    public const string StatusTrading = "STATUS_TRADING";
    public const string StatusCoveragePartial = "STATUS_COVERAGE_PARTIAL";
    public const string StatusUnknown = "STATUS_UNKNOWN";
    public const string StatusStale = "STATUS_STALE";
    public const string StatusConflict = "STATUS_CONFLICT";
    public const string Suspended = "SUSPENDED";
    public const string ReopeningUnconfirmed = "REOPENING_UNCONFIRMED";
    public const string PriceNotAdmitted = "PRICE_NOT_ADMITTED";
    public const string PriceSyntheticCarryForward = "PRICE_SYNTHETIC_CARRY_FORWARD";
    public const string PricePlaceholder = "PRICE_PLACEHOLDER";
    public const string PriceSubstituted = "PRICE_SUBSTITUTED";
    public const string PriceInvariantInvalid = "PRICE_INVARIANT_INVALID";
    public const string PriceUnidentified = "PRICE_UNIDENTIFIED";
    public const string TradedObservedAtT = "TRADED_OBSERVED_AT_T";
    public const string PriceCleared = "PRICE_CLEARED";
    public const string PriceKnownBreak = "PRICE_KNOWN_BREAK";
    public const string PriceUnresolved = "PRICE_UNRESOLVED";
    public const string PriceBasisUnverified = "PRICE_BASIS_UNVERIFIED";
    public const string PriceClearanceIncomplete = "PRICE_CLEARANCE_INCOMPLETE";
    public const string CorporateActionCoveragePartial = "CORPORATE_ACTION_COVERAGE_PARTIAL";
    public const string VolumeBasisUnverified = "VOLUME_BASIS_UNVERIFIED";
    public const string BenchmarkMissing = "BENCHMARK_MISSING";
    public const string InsufficientHistory = "INSUFFICIENT_HISTORY";
    public const string ZeroVolumeUnexplained = "ZERO_VOLUME_UNEXPLAINED";
    public const string EvidenceSourceInadmissible = "EVIDENCE_SOURCE_INADMISSIBLE";
    public const string EvidenceScopeMismatch = "EVIDENCE_SCOPE_MISMATCH";
    public const string EvidenceConflict = "EVIDENCE_CONFLICT";
    public const string EvidenceStale = "EVIDENCE_STALE";
    public const string EvidenceUnknown = "EVIDENCE_UNKNOWN";
    public const string EvidenceSuperseded = "EVIDENCE_SUPERSEDED";
    public const string EvidenceCancelled = "EVIDENCE_CANCELLED";
    public const string NotEvaluated = "NOT_EVALUATED";
    public const string MarketNotEligible = "MARKET_NOT_ELIGIBLE";
    public const string DataNotReady = "DATA_NOT_READY";
    public const string CandidatePromotionBlocked = "CANDIDATE_PROMOTION_BLOCKED";

    internal static readonly string[] ClearedBlocking =
    [
        PriceKnownBreak,
        PriceUnresolved,
        PriceClearanceIncomplete,
        CorporateActionCoveragePartial
    ];

    public static IReadOnlyList<string> All { get; } =
    [
        IdentityConflict, ReferenceNotKnown, TypeUnknown, PreListing, PostDelisting, ListingUnknown,
        UnsupportedType, UnsupportedBoard, BoardUnknown, BoardConflict, MechanismUnresolved,
        ExchangeRuleUnknown, SessionUnconfirmed, SessionUnavailable, NoTrade, StatusTrading,
        StatusCoveragePartial, StatusUnknown, StatusStale, StatusConflict, Suspended,
        ReopeningUnconfirmed, PriceNotAdmitted, PriceSyntheticCarryForward, PricePlaceholder,
        PriceSubstituted, PriceInvariantInvalid, PriceUnidentified, TradedObservedAtT,
        PriceCleared, PriceKnownBreak, PriceUnresolved, PriceBasisUnverified,
        PriceClearanceIncomplete, CorporateActionCoveragePartial, VolumeBasisUnverified,
        BenchmarkMissing, InsufficientHistory, ZeroVolumeUnexplained, EvidenceSourceInadmissible,
        EvidenceScopeMismatch, EvidenceConflict, EvidenceStale, EvidenceUnknown,
        EvidenceSuperseded, EvidenceCancelled, NotEvaluated, MarketNotEligible, DataNotReady,
        CandidatePromotionBlocked
    ];

    public static IReadOnlyList<string> Canonical(IEnumerable<string> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);
        return reasons.Distinct(StringComparer.Ordinal).OrderBy(static r => r, StringComparer.Ordinal).ToArray();
    }
}
