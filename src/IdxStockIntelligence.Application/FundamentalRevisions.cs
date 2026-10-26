using System.Text.Json;

namespace IdxStockIntelligence.Application;

// E5: revision, cancellation, reinstatement, point-in-time visibility and premise
// closure from §19.2–19.3 and §21. Pure functions over retained metadata: no reader,
// no database and no retroactive knowledge.
public sealed record FundamentalRevisionEntry(Guid EvidenceId, string RevisionSeriesId, long RevisionNumber,
    long? SupersedesRevisionNumber, FundamentalOperation Operation, DateTimeOffset KnownAt,
    DateTimeOffset? RetrievedAt, DateTimeOffset? PublishedAt, string AuthorityTier, Guid? PremiseEvidenceId);

public sealed record FundamentalPremise(Guid EvidenceId, string Role, IReadOnlyList<Guid> DependsOn);

public static class FundamentalRevisions
{
    public const string Malformed = FundamentalNumbers.Malformed;
    public const string Chronology = "FUNDAMENTAL_CHRONOLOGY_INVALID";
    public const string Conflict = "FUNDAMENTAL_CONFLICTING";
    public const string PremiseInvalid = "FUNDAMENTAL_PREMISE_INVALID";
    public const int MaxPremiseNodes = 256;
    public const int MaxPremiseEdges = 16;

    /// <summary>Ordinals are consecutive, every revision names its predecessor, and a cancel is never first.</summary>
    public static IReadOnlyList<string> ValidateSeries(IReadOnlyList<FundamentalRevisionEntry> entries)
    {
        var diagnostics = new List<string>();
        if (entries.Count == 0) return [Malformed];
        var ordered = entries.OrderBy(e => e.RevisionNumber).ToList();
        if (ordered.Select(e => e.RevisionSeriesId).Distinct(StringComparer.Ordinal).Count() != 1)
            diagnostics.Add(Malformed);
        for (var index = 0; index < ordered.Count; index++)
        {
            var entry = ordered[index];
            if (entry.RevisionNumber != index + 1) diagnostics.Add(Malformed);
            var expected = index == 0 ? (long?)null : index;
            if (entry.SupersedesRevisionNumber != expected) diagnostics.Add(Malformed);
            if (entry.Operation == FundamentalOperation.CANCEL)
            {
                if (entry.RevisionNumber == 1) diagnostics.Add(Malformed);
                if (entry.PremiseEvidenceId is null) diagnostics.Add(Malformed);
            }
        }
        return diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>PIT visibility: retained knowledge, retrieval and evidenced publication all respect the cutoff.</summary>
    public static bool VisibleAt(FundamentalRevisionEntry entry, DateTimeOffset cutoff) =>
        entry.KnownAt <= cutoff &&
        (entry.RetrievedAt is null || entry.RetrievedAt <= cutoff) &&
        (entry.PublishedAt is null || entry.PublishedAt <= cutoff);

    /// <summary>
    /// Selects the governing revision at a cutoff. No latest-wins shortcut, no fallback to
    /// an older revision when a visible cancel exists, and equal-authority conflicts are
    /// reported instead of resolved by recency.
    /// </summary>
    public static (FundamentalRevisionEntry? Governing, string? Diagnostic) ResolveAt(
        IReadOnlyList<FundamentalRevisionEntry> entries, DateTimeOffset cutoff)
    {
        var visible = entries.Where(e => VisibleAt(e, cutoff)).OrderBy(e => e.RevisionNumber).ToList();
        if (visible.Count == 0) return (null, null);
        var head = visible[^1];
        if (head.Operation == FundamentalOperation.CANCEL) return (null, null);

        var conflicting = visible.Where(e => e.Operation == FundamentalOperation.ASSERT &&
            e.AuthorityTier == head.AuthorityTier && e.RevisionNumber != head.RevisionNumber &&
            e.KnownAt == head.KnownAt).ToList();
        return conflicting.Count > 0 ? (null, Conflict) : (head, null);
    }

    /// <summary>A premise corrected later is invisible earlier; an unreadmitted premise never resurrects.</summary>
    public static (FundamentalRevisionEntry? Governing, string? Diagnostic) ResolveWithCorrection(
        Guid premiseEvidenceId, IReadOnlyList<FundamentalRevisionEntry> corrections, DateTimeOffset cutoff)
    {
        var visibleCorrections = corrections.Where(e => VisibleAt(e, cutoff)).ToList();
        if (visibleCorrections.Count == 0) return (null, PremiseInvalid);
        var head = visibleCorrections.OrderBy(e => e.RevisionNumber).Last();
        if (head.Operation == FundamentalOperation.CANCEL) return (null, null);
        return head.PremiseEvidenceId == premiseEvidenceId ? (head, null) : (null, PremiseInvalid);
    }

    /// <summary>Bounded, acyclic premise closure with unique roles and no inactive premise.</summary>
    public static IReadOnlyList<string> ValidateClosure(IReadOnlyList<FundamentalPremise> premises)
    {
        var diagnostics = new List<string>();
        if (premises.Count > MaxPremiseNodes) return ["FUNDAMENTAL_BOUND_EXCEEDED"];
        var ids = premises.Select(p => p.EvidenceId).ToHashSet();
        if (ids.Count != premises.Count) diagnostics.Add(Malformed);
        if (premises.Select(p => p.Role).Distinct(StringComparer.Ordinal).Count() != premises.Count)
            diagnostics.Add(Malformed);
        var edges = 0;
        foreach (var premise in premises)
        {
            edges += premise.DependsOn.Count;
            if (premise.DependsOn.Any(dependency => !ids.Contains(dependency))) diagnostics.Add(PremiseInvalid);
            if (premise.DependsOn.Contains(premise.EvidenceId)) diagnostics.Add(PremiseInvalid);
        }
        if (edges > MaxPremiseEdges) diagnostics.Add("FUNDAMENTAL_BOUND_EXCEEDED");

        var byId = new Dictionary<Guid, FundamentalPremise>();
        foreach (var premise in premises) byId.TryAdd(premise.EvidenceId, premise);
        var state = new Dictionary<Guid, int>();
        bool Visit(Guid node)
        {
            if (!byId.TryGetValue(node, out var current)) return true; // unresolved dependency already flagged
            if (state.TryGetValue(node, out var mark)) return mark != 1;
            state[node] = 1;
            foreach (var dependency in current.DependsOn)
                if (!Visit(dependency)) return false;
            state[node] = 2;
            return true;
        }
        if (premises.Any(p => !Visit(p.EvidenceId))) diagnostics.Add(PremiseInvalid);
        return diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Reinstatement stays invisible before its own knownAt, then governs from that instant.</summary>
    public static FundamentalRevisionEntry? ReinstatedFrom(IReadOnlyList<FundamentalRevisionEntry> entries,
        DateTimeOffset cutoff) =>
        entries.Where(e => e.Operation == FundamentalOperation.ASSERT && VisibleAt(e, cutoff))
            .OrderBy(e => e.RevisionNumber).LastOrDefault();
}
