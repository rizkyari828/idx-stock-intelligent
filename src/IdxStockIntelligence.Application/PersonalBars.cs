using System.Globalization;

namespace IdxStockIntelligence.Application;

/// <summary>One personal capture row, with prices retained as the stored decimal strings.</summary>
public sealed record PersonalBarRow(string Symbol, string Open, string High, string Low, string Close,
    string AdjustedClose, long Volume, string QualityStatus);

/// <summary>One personal session read from its highest retained revision.</summary>
public sealed record PersonalBarSession(DateOnly SessionDate, string SessionProof, string CapturedAt,
    IReadOnlyList<PersonalBarRow> Rows);

/// <summary>Read-only personal file-store response. It never substitutes for canonical evidence.</summary>
public sealed record PersonalBarsResponse(string Source, DateTime GeneratedAt, IReadOnlyList<PersonalBarSession> Sessions)
{
    public const string SourceName = "PERSONAL_FILE_STORE";
}

public sealed class PersonalBarsException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

/// <summary>
/// Bounded query for the personal file store. Both bounds are optional together; omitting them
/// selects the newest seven retained session files. No clock or database is involved.
/// </summary>
public sealed record PersonalBarsQuery(DateOnly? From, DateOnly? To, bool NewestOnly)
{
    public const int NewestSessions = 7;
    public const int MaximumRangeDays = 366;
    private static readonly string[] Keys = ["from", "to"];

    public static PersonalBarsQuery Resolve(IReadOnlyDictionary<string, string?> values)
    {
        if (values.Keys.Any(key => !Keys.Contains(key, StringComparer.Ordinal)))
            throw Invalid("Unknown personal bars parameter.");
        var hasFrom = values.TryGetValue("from", out var fromText);
        var hasTo = values.TryGetValue("to", out var toText);
        if (hasFrom != hasTo)
            throw Range("from and to must be supplied together, or omitted for the newest seven sessions.");
        if (!hasFrom) return new(null, null, true);
        var from = ParseDate("from", fromText!);
        var to = ParseDate("to", toText!);
        if (from > to) throw Range("from must be on or before to.");
        if (to.DayNumber - from.DayNumber + 1 > MaximumRangeDays)
            throw Range($"from/to range must not exceed {MaximumRangeDays} days.");
        return new(from, to, false);
    }

    private static DateOnly ParseDate(string name, string? value)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw Invalid(name + " requires YYYY-MM-DD.");
        return date;
    }

    private static PersonalBarsException Invalid(string message) =>
        new(400, "PERSONAL_BARS_QUERY_INVALID", message);

    private static PersonalBarsException Range(string message) =>
        new(400, "PERSONAL_BARS_RANGE_INVALID", message);
}
