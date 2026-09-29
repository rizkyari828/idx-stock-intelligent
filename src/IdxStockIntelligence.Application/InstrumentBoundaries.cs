using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public enum InstrumentBoundaryState { PreListing, ExpectedSession, PostDelisting, UnknownBoundary }

public sealed record InstrumentBoundaryEvidence(Guid InstrumentId, string Symbol, string IssuerName,
    DateOnly? ListedFrom, DateOnly? DelistedAt, DateOnly? FirstTradingDate, string Status,
    string Source, string Reference, string PublicationReference, DateTimeOffset? RetrievedAt,
    DateTimeOffset KnownAt, string Confidence, string SourceVersion, string Notes);

public static class InstrumentBoundaries
{
    public static void Validate(InstrumentBoundaryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        _ = new InstrumentId(evidence.InstrumentId);
        if (evidence.Status is not ("VERIFIED" or "PARTIAL" or "UNKNOWN")
            || new[] { evidence.Symbol, evidence.IssuerName, evidence.Source, evidence.PublicationReference,
                evidence.Confidence, evidence.SourceVersion }.Any(string.IsNullOrWhiteSpace)
            || !Uri.TryCreate(evidence.Reference, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || evidence.RetrievedAt > evidence.KnownAt
            || evidence.DelistedAt < evidence.ListedFrom || evidence.FirstTradingDate < evidence.ListedFrom
            || evidence.FirstTradingDate > evidence.DelistedAt
            || evidence.Status == "VERIFIED" && evidence.ListedFrom is null && evidence.DelistedAt is null)
            throw new ArgumentException("Invalid boundary reference evidence.", nameof(evidence));
    }

    public static InstrumentBoundaryEvidence? AsOf(IEnumerable<InstrumentBoundaryEvidence> history, InstrumentId id, DateTimeOffset cutoff)
    {
        var visible = history.Where(e => e.InstrumentId == id.Value && e.KnownAt <= cutoff).ToArray();
        foreach (var evidence in visible) Validate(evidence);
        if (visible.GroupBy(e => e.KnownAt).Any(g => g.Distinct().Count() > 1))
            throw new ArgumentException("Conflicting evidence requires a distinct PARTIAL revision; never choose silently.", nameof(history));
        return visible.MaxBy(e => e.KnownAt);
    }

    public static Instrument Resolve(Instrument fallback, IEnumerable<InstrumentBoundaryEvidence> history, DateTimeOffset cutoff)
    {
        var evidence = AsOf(history, fallback.Id, cutoff);
        return evidence is null ? new(fallback.Id, fallback.IssuerName, null) :
            new(fallback.Id, evidence.IssuerName, evidence.Status == "VERIFIED" ? evidence.ListedFrom : null,
                evidence.Status == "VERIFIED" ? evidence.DelistedAt : null);
    }

    public static InstrumentBoundaryState Classify(Instrument instrument, DateOnly date) =>
        instrument.ListedOn is not null && date < instrument.ListedOn ? InstrumentBoundaryState.PreListing :
        instrument.DelistedOn is not null && date > instrument.DelistedOn ? InstrumentBoundaryState.PostDelisting :
        instrument.ListedOn is null ? InstrumentBoundaryState.UnknownBoundary : InstrumentBoundaryState.ExpectedSession;
}
