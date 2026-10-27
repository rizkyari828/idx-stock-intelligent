using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using IdxStockIntelligence.Application;

namespace IdxStockIntelligence.Infrastructure;

/// <summary>
/// Read-only access to the personal, non-canonical bar store. It reads only the documented
/// data/personal/bars/&lt;YYYY&gt;/&lt;YYYY-MM-DD&gt;[.rN].json files: no database, canonical
/// reader, cache or write. Prices are returned as the stored strings and are never parsed
/// to a floating-point number.
/// </summary>
public sealed class PersonalBarFileStore
{
    public const string RelativeRoot = "data/personal/bars";
    private const int MaximumFileBytes = 4 * 1024 * 1024;
    private const int MaximumDocumentDepth = 8;
    private static readonly Regex SessionFile = new(
        @"^(?<date>\d{4}-\d{2}-\d{2})(?:\.r(?<revision>[0-9]+))?\.json$", RegexOptions.CultureInvariant);
    private readonly string _root;

    public PersonalBarFileStore(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Personal bar store root is required.", nameof(root));
        _root = Path.GetFullPath(root);
    }

    /// <summary>
    /// Reads every session under the store root once and keeps only its highest retained
    /// revision, so revisions of one session are never merged. A missing directory is an
    /// explicit empty result; an unreadable or malformed file fails the whole request.
    /// </summary>
    public async Task<PersonalBarsResponse> ReadAsync(PersonalBarsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var generatedAt = DateTime.UtcNow;
        if (!Directory.Exists(_root)) return new(PersonalBarsResponse.SourceName, generatedAt, []);
        try
        {
            var selected = EnumerateSessions()
                .GroupBy(file => file.SessionDate)
                .Select(group => group.MaxBy(file => file.Revision)!)
                .Where(file => query.NewestOnly
                    || (file.SessionDate >= query.From!.Value && file.SessionDate <= query.To!.Value))
                .OrderByDescending(file => file.SessionDate)
                .Take(query.NewestOnly ? PersonalBarsQuery.NewestSessions : PersonalBarsQuery.MaximumRangeDays)
                .OrderBy(file => file.SessionDate)
                .ToArray();
            var sessions = new List<PersonalBarSession>(selected.Length);
            foreach (var file in selected)
            {
                ct.ThrowIfCancellationRequested();
                sessions.Add(await ReadSessionAsync(file, ct));
            }
            return new(PersonalBarsResponse.SourceName, generatedAt, sessions);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new PersonalBarsException(503, "PERSONAL_BARS_UNAVAILABLE", "The personal bar store is unavailable.");
        }
    }

    private IEnumerable<PersonalBarFile> EnumerateSessions()
    {
        foreach (var directory in Directory.EnumerateDirectories(_root))
        {
            var yearName = Path.GetFileName(directory);
            // Only the documented per-year directories are part of the store.
            if (!int.TryParse(yearName, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                || year is < 1900 or > 9999) continue;
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                var name = Path.GetFileName(path);
                var match = SessionFile.Match(name);
                if (!match.Success)
                    throw Corrupt("Personal bar store contains an unrecognized file: " + name + ".");
                if (!DateOnly.TryParseExact(match.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date))
                    throw Corrupt("Personal bar store contains an invalid session file name: " + name + ".");
                if (date.Year != year)
                    throw Corrupt("Personal capture " + name + " disagrees with its year directory.");
                var revision = 1;
                if (match.Groups["revision"].Success
                    && (!int.TryParse(match.Groups["revision"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out revision)
                        || revision < 2))
                    throw Corrupt("Personal capture " + name + " has an invalid revision number.");
                yield return new PersonalBarFile(date, revision, path);
            }
        }
    }

    private static async Task<PersonalBarSession> ReadSessionAsync(PersonalBarFile file, CancellationToken ct)
    {
        var session = file.SessionDate;
        if (new FileInfo(file.Path).Length > MaximumFileBytes)
            throw Corrupt(session, "the file exceeds the bounded size.");
        var bytes = await File.ReadAllBytesAsync(file.Path, ct);
        if (bytes.Length > MaximumFileBytes) throw Corrupt(session, "the file exceeds the bounded size.");
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = MaximumDocumentDepth });
        }
        catch (JsonException)
        {
            throw Corrupt(session, "the JSON is malformed.");
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw Corrupt(session, "the document is not an object.");
            if (!root.TryGetProperty("schema_version", out var schema) || schema.ValueKind != JsonValueKind.Number
                || !schema.TryGetInt32(out var version) || version != 1)
                throw Corrupt(session, "schema_version is not 1.");
            var sessionDate = RequiredString(root, "session_date", session);
            if (!DateOnly.TryParseExact(sessionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var declared)
                || declared != session)
                throw Corrupt(session, "session_date disagrees with the file name.");
            var proof = RequiredString(root, "session_proof", session);
            if (proof is not ("UNPROVEN_PERSONAL" or "PROVEN"))
                throw Corrupt(session, "session_proof is not a retained personal proof.");
            var capturedAt = RequiredString(root, "captured_at", session);
            if (!root.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
                throw Corrupt(session, "rows is not an array.");
            var parsedRows = new List<PersonalBarRow>();
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) throw Corrupt(session, "a row is not an object.");
                var symbol = RequiredString(row, "symbol", session);
                var open = RequiredString(row, "open", session);
                var high = RequiredString(row, "high", session);
                var low = RequiredString(row, "low", session);
                var close = RequiredString(row, "close", session);
                var adjustedClose = RequiredString(row, "adjusted_close", session);
                if (!row.TryGetProperty("volume", out var volume) || volume.ValueKind != JsonValueKind.Number
                    || !volume.TryGetInt64(out var parsedVolume))
                    throw Corrupt(session, "a row volume is not an integer.");
                var quality = RequiredString(row, "quality_status", session);
                parsedRows.Add(new(symbol, open, high, low, close, adjustedClose, parsedVolume, quality));
            }
            return new(session, proof, capturedAt, parsedRows);
        }
    }

    private static string RequiredString(JsonElement element, string name, DateOnly session)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw Corrupt(session, name + " is missing or not a non-empty string.");
        return value.GetString()!;
    }

    private static PersonalBarsException Corrupt(DateOnly session, string detail) =>
        new(500, "PERSONAL_BARS_CORRUPT", $"Personal capture {session:yyyy-MM-dd} is invalid: {detail}");

    private static PersonalBarsException Corrupt(string detail) =>
        new(500, "PERSONAL_BARS_CORRUPT", "The personal bar store is invalid: " + detail);

    private sealed record PersonalBarFile(DateOnly SessionDate, int Revision, string Path);
}
