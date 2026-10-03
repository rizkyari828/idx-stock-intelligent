using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Infrastructure;

public sealed record ScreenerReferenceBundle(ScreenerReferenceDocument Reference,
    IReadOnlyList<SessionProof> Sessions, IReadOnlyList<InstrumentSessionProof> InstrumentSessions);
public sealed record ScreenerReferenceCopy(ScreenerReferenceBundle Bundle, IReadOnlyList<byte[]?> Files);

public static class ScreenerReferenceFiles
{
    // Fixed local inputs only. No request path, URL fetch, registry lookup or assertion generation.
    public static async Task<EvidenceResult<ScreenerReferenceBundle>> LoadAsync(CancellationToken ct)
    {
        var copied = await CopyAsync(ct);
        return new(copied.Value?.Bundle, copied.Reason);
    }

    public static async Task<EvidenceResult<ScreenerReferenceCopy>> CopyAsync(CancellationToken ct)
    {
        var copied = new List<byte[]?>();
        var bytes = 0;
        try
        {
            foreach (var file in new[] { "pilot/screener-reference.json", "pilot/sessions.json", "pilot/instrument-sessions.json" })
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(file)) { copied.Add(null); continue; }
                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read,
                    8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
                using var memory = new MemoryStream();
                var buffer = new byte[8192];
                int count;
                while ((count = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    bytes += count;
                    if (bytes > ScreenerReferences.MaximumBytes) return new(null, "REFERENCE_BOUND_EXCEEDED");
                    memory.Write(buffer, 0, count);
                }
                copied.Add(memory.ToArray());
            }
            var parsed = Parse(copied[0] ?? [], copied[1] ?? [], copied[2] ?? [], ct);
            return new(parsed.Available ? new(parsed.Value!, copied) : null, parsed.Reason);
        }
        catch (IOException) { return new(null, "REFERENCE_FILES_UNAVAILABLE"); }
        catch (UnauthorizedAccessException) { return new(null, "REFERENCE_FILES_UNAVAILABLE"); }
    }

    public static EvidenceResult<ScreenerReferenceBundle> Parse(ReadOnlyMemory<byte> reference,
        ReadOnlyMemory<byte> sessions, ReadOnlyMemory<byte> statuses, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if ((long)reference.Length + sessions.Length + statuses.Length > ScreenerReferences.MaximumBytes)
            return new(null, "REFERENCE_BOUND_EXCEEDED");
        var parsed = ScreenerReferences.Parse(reference.IsEmpty
            ? Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"universes\":[],\"instruments\":[]}") : reference, ct);
        if (!parsed.Available) return new(null, parsed.Reason);
        try
        {
            using var sessionJson = JsonDocument.Parse(sessions.IsEmpty ? "[]"u8.ToArray() : sessions);
            using var statusJson = JsonDocument.Parse(statuses.IsEmpty ? "[]"u8.ToArray() : statuses);
            var proofs = new List<SessionProof>();
            var instrumentProofs = new List<InstrumentSessionProof>();
            var records = ScreenerReferences.Validate(parsed.Value!, ct);
            foreach (var row in sessionJson.RootElement.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                if (++records > ScreenerReferences.MaximumRecords) return new(null, "REFERENCE_BOUND_EXCEEDED");
                CheckRow(row);
                var status = row.GetProperty("status").GetString()!;
                if (status is not ("ObservedTrading" or "AnnouncedClosed" or "ExceptionalClosure")) throw new ArgumentException("Invalid session status.");
                var known = row.GetProperty("known_at").GetDateTimeOffset();
                var completed = row.TryGetProperty("completed_at", out var c) ? c.GetDateTimeOffset() : (DateTimeOffset?)null;
                if (completed > known) throw new ArgumentException("Completion predates knowledge.");
                proofs.Add(new(row.GetProperty("date").Deserialize<DateOnly>(), Enum.Parse<ExchangeDayStatus>(status),
                    row.GetProperty("reference").GetString()!, known, completed));
            }
            foreach (var row in statusJson.RootElement.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                if (++records > ScreenerReferences.MaximumRecords) return new(null, "REFERENCE_BOUND_EXCEEDED");
                CheckRow(row);
                var status = row.GetProperty("status").GetString()!;
                if (status is not ("Suspension" or "NoTrade")) throw new ArgumentException("Invalid instrument session status.");
                instrumentProofs.Add(new(new(row.GetProperty("instrument_id").GetGuid()), row.GetProperty("date").Deserialize<DateOnly>(),
                    Enum.Parse<MarketSessionStatus>(status), row.GetProperty("reference").GetString()!, row.GetProperty("known_at").GetDateTimeOffset()));
            }
            return new(new(parsed.Value!, proofs, instrumentProofs), null);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException)
        { return new(null, "REFERENCE_MALFORMED"); }
    }

    private static void CheckRow(JsonElement row)
    {
        var names = row.EnumerateObject().Select(p => p.Name).ToArray();
        var reference = row.GetProperty("reference").GetString();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length
            || !Uri.TryCreate(reference, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0
            || uri.Host.Equals("eodhd.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".eodhd.com", StringComparison.OrdinalIgnoreCase)
            || row.GetProperty("date").Deserialize<DateOnly>() < new DateOnly(1900, 1, 1)
            || row.GetProperty("known_at").GetDateTimeOffset() < new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero))
            throw new ArgumentException("Independent dated session reference required.");
    }
}
