using System.Globalization;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record EvidenceResult<T>(T? Value, string? Reason) where T : class
{
    public bool Available => Value is not null && Reason is null;
}

public sealed record ScreenerReadRequest(IReadOnlyList<Guid> InstrumentIds, Guid BenchmarkId,
    DateOnly HistoryAnchor, DateOnly Through, DateTimeOffset Cutoff)
{
    public static readonly DateOnly Anchor = new(2026, 8, 24);
    public static readonly DateOnly Horizon = new(2027, 8, 24);
    public const int MaximumIds = 211;
    public const int MaximumRows = 80000;
    public const string PolicyId = "screener-v0.1.0";

    public string? BoundsReason() => HistoryAnchor != Anchor || Through < Anchor || Through > Horizon
        || Through.DayNumber - HistoryAnchor.DayNumber >= 366 ? "HISTORY_OUT_OF_SCOPE" :
        InstrumentIds is null || InstrumentIds.Count > MaximumIds || BenchmarkId == Guid.Empty || InstrumentIds.Any(id => id == Guid.Empty)
        || InstrumentIds.Append(BenchmarkId).Distinct().Count() > MaximumIds ? "INSTRUMENT_BOUND_EXCEEDED" : null;
}

// Raw numeric strings keep an unrepresentable selected revision visible instead of losing it during decoding.
public sealed record ScreenerBarEvidence(Guid InstrumentId, DateOnly SessionDate, long RevisionNumber,
    DateTimeOffset KnownAt, string ContentHash, Guid IngestionRunId, Guid RawArtifactId,
    string SourceId, string RawHash, DateTimeOffset FetchedAt, DateTimeOffset? RetrievedAt,
    string? SessionReference, DateTimeOffset? SessionKnownAt, string CanonicalQuality,
    string Open, string High, string Low, string Close, long Volume, string? AdjustedClose,
    string VolumeUnit, string VolumeBasis, string MarketSegment)
{
    // Authenticate the stored content even when admission/quality validation deliberately rejects it.
    public bool ContentHashMatches()
    {
        try
        {
            string Number(string text) => ExactDecimal(text).ToString("G29", CultureInfo.InvariantCulture);
            return ContentHash == PilotValidation.ContentHash([SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Number(Open), Number(High), Number(Low), Number(Close), Volume.ToString(CultureInfo.InvariantCulture),
                AdjustedClose is null ? null : Number(AdjustedClose), VolumeUnit, VolumeBasis, MarketSegment]);
        }
        catch (Exception e) when (e is FormatException or OverflowException) { return false; }
    }

    public EvidenceResult<DailyBarRevision> Validate()
    {
        if (CanonicalQuality is not ("VALID" or "DEGRADED" or "STALE")) return new(null, "CANONICAL_QUALITY_UNAVAILABLE");
        if (RetrievedAt is null || SessionKnownAt is null || string.IsNullOrWhiteSpace(SessionReference)
            || FetchedAt > RetrievedAt || RetrievedAt > KnownAt || SessionKnownAt > KnownAt
            || RawArtifactId == Guid.Empty || IngestionRunId == Guid.Empty || RevisionNumber <= 0)
            return new(null, "CANONICAL_PROVENANCE_UNAVAILABLE");
        try
        {
            var source = new SourceReference(SourceId, RawArtifactId, RetrievedAt.Value, RetrievedAt.Value, RawHash);
            var bar = new DailyBar(new(InstrumentId), SessionDate, ExactDecimal(Open), ExactDecimal(High),
                ExactDecimal(Low), ExactDecimal(Close), Volume, source,
                AdjustedClose is null ? null : ExactDecimal(AdjustedClose), VolumeUnit, VolumeBasis, MarketSegment);
            if (!ScreenerReferences.IsHash(ContentHash)) return new(null, "CANONICAL_INVALID");
            return new(new(RevisionNumber, bar, KnownAt, ContentHash, IngestionRunId), null);
        }
        catch (OverflowException) { return new(null, "NUMERIC_OUT_OF_RANGE"); }
        catch (FormatException) { return new(null, "CANONICAL_INVALID"); }
        catch (ArgumentException) { return new(null, "CANONICAL_INVALID"); }
    }

    private static decimal ExactDecimal(string text)
    {
        var value = decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        // decimal.Parse rounds excessive fractional precision: reject that change rather than alter canonical prices.
        static string Normalize(string s)
        {
            var parts = s.TrimStart('+').Split('.');
            var integer = parts[0].TrimStart('0');
            if (integer.Length == 0) integer = "0";
            var fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : "";
            return integer + (fraction.Length == 0 ? "" : "." + fraction);
        }
        if (Normalize(text) != Normalize(value.ToString("0.############################", CultureInfo.InvariantCulture)))
            throw new OverflowException("Canonical numeric is not exactly representable.");
        return value;
    }
}

public sealed record ScreenerDatabaseEvidence(IReadOnlyList<ScreenerBarEvidence> Bars,
    IReadOnlyList<ScreenerListingEvidence> Listings);
public sealed record ScreenerListingEvidence(Guid InstrumentId, DateTimeOffset KnownAt, string ContentHash,
    EvidenceResult<InstrumentBoundaryEvidence> Resolution);

public sealed record SelectedScreenerReferences(IReadOnlyList<UniverseSnapshot> Universes,
    IReadOnlyList<InstrumentSnapshot> Instruments, IReadOnlyList<SessionProof> Sessions,
    IReadOnlyList<InstrumentSessionProof> InstrumentSessions);

public sealed record ScreenerSessionEvidence(ExchangeDayStatus Status, SessionProof? Proof, string? Reason);

public static class ScreenerSessions
{
    public static ScreenerSessionEvidence Resolve(IReadOnlyList<SessionProof> proofs, DateOnly date,
        DateOnly through, DateTimeOffset cutoff)
    {
        var visible = proofs.Where(p => p.Date == date && p.Date <= through && p.KnownAt <= cutoff).ToArray();
        var latestKnown = visible.Length == 0 ? DateTimeOffset.MinValue : visible.Max(v => v.KnownAt);
        var latest = visible.Where(p => p.KnownAt == latestKnown).ToArray();
        if (latest.Select(p => (p.Status, p.CompletedAt)).Distinct().Count() > 1)
            return new(ExchangeDayStatus.Unknown, null, "SESSION_CONFLICT");
        var proof = latest.OrderBy(p => p.Reference, StringComparer.Ordinal).FirstOrDefault();
        if (date > through) return new(ExchangeDayStatus.Unknown, null, "AFTER_THROUGH");
        var status = ExchangeCalendarEvidence.Classify(date, proof?.Status);
        if (proof is null) return new(status, null, status == ExchangeDayStatus.Unknown ? "SESSION_UNCONFIRMED" : null);
        var reason = CompletedSessionPolicy.Reason(date, cutoff, new(19, 0), proof);
        return reason.StartsWith("ELIGIBLE_", StringComparison.Ordinal) || reason == "KNOWN_CLOSED"
            ? new(status, proof, null) : new(ExchangeDayStatus.Unknown, proof, reason);
    }

    public static EvidenceResult<InstrumentSessionProof> ResolveInstrument(IReadOnlyList<InstrumentSessionProof> proofs,
        Guid id, DateOnly date, DateOnly through, DateTimeOffset cutoff)
    {
        var visible = proofs.Where(p => p.Instrument.Value == id && p.Date == date && p.Date <= through && p.KnownAt <= cutoff).ToArray();
        var latestKnown = visible.Length == 0 ? DateTimeOffset.MinValue : visible.Max(v => v.KnownAt);
        var latest = visible.Where(p => p.KnownAt == latestKnown).ToArray();
        return latest.Select(p => p.Status).Distinct().Count() > 1 ? new(null, "INSTRUMENT_SESSION_CONFLICT") :
            new(latest.OrderBy(p => p.Reference, StringComparer.Ordinal).FirstOrDefault(), latest.Length == 0 ? "STATUS_UNKNOWN" : null);
    }
}
