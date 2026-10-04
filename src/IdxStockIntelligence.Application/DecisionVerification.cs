using System.Collections;
using System.Globalization;

namespace IdxStockIntelligence.Application;

public enum DecisionVerificationState
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("MATCH")] Match,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("INPUT_NOT_AVAILABLE")] InputNotAvailable,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("POLICY_VERSION_UNAVAILABLE")] PolicyVersionUnavailable,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("DIFFERENT_RESULT")] DifferentResult
}
public sealed record DecisionDifference(string Field, string? Stored, string? Recomputed);
public sealed record DecisionVerificationResult(Guid RunId, DecisionVerificationState State, DateTimeOffset VerifiedAt,
    DateTimeOffset CapturedAt, string PolicyId, string StoredInputHash, string? RecomputedInputHash,
    string StoredSelectedDigest, string? RecomputedSelectedDigest, IReadOnlyList<DecisionDifference> Differences,
    bool DifferencesTruncated, string? Detail);

public class RetainedInputUnavailableException(string reason) : Exception(reason);

public static class DecisionVerification
{
    private static void Require(bool condition, string reason) { if (!condition) throw new RetainedInputUnavailableException(reason); }
    public static void ValidateManifest(DecisionSnapshotHeader h, DecisionSnapshotManifest m)
    {
        Require(m.SchemaVersion == 1 && m.Request is not null && m.Request.BoundsReason() is null
            && m.Request.InstrumentIds.Distinct().Count() == m.Request.InstrumentIds.Count
            && m.Request.Through == h.Through && m.Request.HistoryAnchor == h.HistoryAnchor && m.Request.Cutoff == h.KnowledgeCutoff
            && h.CapturedAt == h.KnowledgeCutoff && h.RecordedAt >= h.CapturedAt && h.CaptureKind == "PROSPECTIVE_CAPTURE" && h.Universe == "PILOT", "MANIFEST_CHRONOLOGY_INVALID");
        Require(m.Bars is not null && m.Listings is not null && m.ConfiguredIds is not null && m.HeldIds is not null
            && m.EvaluatedInstrumentIds is not null && m.Universes is not null && m.Instruments is not null
            && m.Sessions is not null && m.InstrumentSessions is not null && m.Archives is not null, "MANIFEST_INVALID");
        var r = m.Request!;
        var ids = r.InstrumentIds.Append(r.BenchmarkId).ToHashSet();
        Require(m.Bars!.All(b => b is not null) && m.Listings!.All(l => l is not null), "MANIFEST_EVIDENCE_INVALID");
        Require(m.Bars!.Count <= ScreenerReadRequest.MaximumRows && m.Listings!.Count <= ids.Count
            && m.Bars.Select(b => (b.InstrumentId, b.SessionDate)).Distinct().Count() == m.Bars.Count
            && m.Listings.Select(l => l.InstrumentId).Distinct().Count() == m.Listings.Count
            && m.Bars.All(b => b is not null && ids.Contains(b.InstrumentId) && b.SessionDate >= r.HistoryAnchor && b.SessionDate <= r.Through
                && b.KnownAt <= r.Cutoff && b.RevisionNumber > 0 && b.RawArtifactId != Guid.Empty && ScreenerReferences.IsHash(b.ContentHash))
            && m.Listings.All(l => l is not null && ids.Contains(l.InstrumentId) && l.KnownAt <= r.Cutoff && ScreenerReferences.IsHash(l.ContentHash)), "MANIFEST_EVIDENCE_INVALID");
        Require(m.Universes!.Count + m.Instruments!.Count + m.Sessions!.Count + m.InstrumentSessions!.Count
            <= ScreenerReferences.MaximumRecords, "MANIFEST_REFERENCE_BOUND_EXCEEDED");
        foreach (var set in new[] { m.ConfiguredIds!, m.HeldIds!, m.EvaluatedInstrumentIds! })
            Require(set.Count <= 210 && set.Distinct().Count() == set.Count && set.All(id => id != Guid.Empty), "MANIFEST_POPULATION_INVALID");
        Require(m.ConfiguredIds!.Count <= 10 && m.HeldIds!.Count <= 200
            && (m.Portfolio?.PortfolioId == h.PortfolioId) && (h.PortfolioId is not null || m.Portfolio is null), "MANIFEST_PORTFOLIO_INVALID");
        if (m.Portfolio is { } p)
            Require(p.EventIds is not null && p.ThesisIds is not null && p.EventIds.Count <= PortfolioLedger.MaximumEvents
                && p.ThesisIds.Count <= PortfolioExchange.MaxTheses && p.EventIds.Distinct().Count() == p.EventIds.Count
                && p.ThesisIds.Distinct().Count() == p.ThesisIds.Count && p.EventIds.Concat(p.ThesisIds).All(id => id != Guid.Empty), "MANIFEST_PORTFOLIO_INVALID");
    }

    public static void Authenticate(DecisionSnapshotManifest m, ScreenerDatabaseEvidence evidence, CancellationToken ct)
    {
        Require(evidence.Bars.Count == m.Bars.Count, "CANONICAL_REVISION_MISSING");
        var links = m.Bars.ToDictionary(b => (b.InstrumentId, b.SessionDate, b.RevisionNumber));
        foreach (var b in evidence.Bars)
        {
            ct.ThrowIfCancellationRequested();
            Require(links.TryGetValue((b.InstrumentId, b.SessionDate, b.RevisionNumber), out var link)
                && link.KnownAt == b.KnownAt && link.ContentHash == b.ContentHash && link.RawArtifactId == b.RawArtifactId
                && b.FetchedAt <= m.Request.Cutoff && (b.RetrievedAt is null || b.RetrievedAt <= m.Request.Cutoff)
                && (b.SessionKnownAt is null || b.SessionKnownAt <= m.Request.Cutoff) && b.ContentHashMatches(), "CANONICAL_REVISION_INTEGRITY_FAILED");
        }
        Require(evidence.Listings.Count == m.Listings.Count, "LISTING_INPUT_MISSING");
        var listings = m.Listings.ToDictionary(l => l.InstrumentId);
        Require(evidence.Listings.All(l => listings.TryGetValue(l.InstrumentId, out var link)
            && link.KnownAt == l.KnownAt && link.ContentHash == l.ContentHash), "LISTING_INPUT_INTEGRITY_FAILED");
    }

    public const int MaximumDifferences = 100;
    public static DecisionVerificationResult Unavailable(DecisionSnapshotHeader h, DecisionVerificationState state, string detail)
        => new(h.RunId, state, DateTimeOffset.UtcNow, h.CapturedAt, h.PolicyId, h.InputHash, null,
            h.SelectedDigest, null, [], false, detail);

    public static DecisionVerificationResult Compare(DecisionSnapshotRun stored, DecisionSnapshotResult result,
        IReadOnlyList<DecisionSnapshotRow> rows, string status, DateOnly? targetSession, string? universeSnapshotId,
        string inputHash, string selectedDigest, CancellationToken ct = default)
    {
        var differences = new List<DecisionDifference>();
        var truncated = false;
        void Difference(string field, object? a, object? b)
        {
            if (differences.Count == MaximumDifferences) { truncated = true; return; }
            static string? Text(object? value)
            {
                var text = value switch { null => null, string s => s, DateTimeOffset t => t.ToUniversalTime().ToString("O"),
                    IEnumerable => "[collection]", IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => "[object]" };
                return text is { Length: > 160 } ? text[..160] + "…" : text;
            }
            differences.Add(new(field.Length > 240 ? field[..240] + "…" : field, Text(a), Text(b)));
        }
        // Only schema-defined typed projections reach this comparison. Dictionaries are maps;
        // arrays retain contract order. Decimal equality and UTC instant equality use CLR values.
        void Values(string path, object? a, object? b)
        {
            ct.ThrowIfCancellationRequested();
            if (truncated || Equals(a, b)) return;
            if (a is null || b is null) { Difference(path, a, b); return; }
            if (a is IDictionary da && b is IDictionary db)
            {
                foreach (var key in da.Keys.Cast<string>().Concat(db.Keys.Cast<string>()).Distinct().Order(StringComparer.Ordinal))
                    if (!da.Contains(key) || !db.Contains(key)) Difference(path + "." + key, da.Contains(key) ? da[key] : null, db.Contains(key) ? db[key] : null);
                    else Values(path + "." + key, da[key], db[key]);
            }
            else if (a is not string && a is IEnumerable ea && b is IEnumerable eb)
            {
                var aa = ea.Cast<object?>().ToArray(); var bb = eb.Cast<object?>().ToArray();
                if (aa.Length != bb.Length) Difference(path + ".count", aa.Length, bb.Length);
                for (var n = 0; n < Math.Min(aa.Length, bb.Length) && !truncated; n++) Values(path + "[" + n + "]", aa[n], bb[n]);
            }
            else if (a.GetType() != b.GetType() || a is string || a.GetType().IsValueType) Difference(path, a, b);
            else foreach (var p in a.GetType().GetProperties().OrderBy(p => p.Name, StringComparer.Ordinal))
                Values(path + "." + char.ToLowerInvariant(p.Name[0]) + p.Name[1..], p.GetValue(a), p.GetValue(b));
        }
        var h = stored.Header;
        Values("inputHash", h.InputHash, inputHash); Values("selectedDigest", h.SelectedDigest, selectedDigest);
        Values("status", h.Status, status); Values("targetSession", h.TargetSession, targetSession);
        Values("universeSnapshotId", h.UniverseSnapshotId, universeSnapshotId); Values("rowCount", h.RowCount, rows.Count);
        Values("result", stored.Result, result);
        Values("population", stored.Rows.Select(r => r.InstrumentId).ToArray(), rows.Select(r => r.InstrumentId).ToArray());
        var replay = rows.ToDictionary(r => r.InstrumentId);
        foreach (var row in stored.Rows)
        {
            if (replay.Remove(row.InstrumentId, out var other)) Values("rows." + row.InstrumentId, row, other);
            else Difference("rows." + row.InstrumentId, "captured", "missing in replay");
        }
        foreach (var id in replay.Keys.Order()) Difference("rows." + id, "not captured", "extra in replay");
        return new(h.RunId, differences.Count == 0 ? DecisionVerificationState.Match : DecisionVerificationState.DifferentResult,
            DateTimeOffset.UtcNow, h.CapturedAt, h.PolicyId, h.InputHash, inputHash, h.SelectedDigest, selectedDigest,
            differences, truncated, null);
    }
}
