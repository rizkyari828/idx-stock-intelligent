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

public static class DecisionVerification
{
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
