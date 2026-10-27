using System.Text.Json;
using IdxStockIntelligence.Application;

namespace IdxStockIntelligence.Infrastructure;

/// <summary>
/// Read-only personal technical-feature read. It reads the bounded personal session window through the
/// existing <see cref="PersonalBarFileStore"/> (highest retained revision per session, exactly as the bars
/// endpoint does), takes the cited exchange closures from the retained 2026 holiday fixture, and delegates
/// every value to <see cref="PersonalTechnicalFeatures"/>. It owns no formula, database, cache or write.
/// </summary>
public sealed class PersonalTechnicalFeatureService(PersonalBarFileStore store, string holidayFixturePath)
{
    public async Task<PersonalTechnicalFeaturesReport> ReadAsync(PersonalTechnicalQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        // One bounded window ending at through is enough for every guarded window (the longest needs 61).
        var from = query.Through.AddDays(-(PersonalTechnicalFeatures.WindowDays - 1));
        var bars = await store.ReadAsync(new PersonalBarsQuery(from, query.Through, false), ct);
        return PersonalTechnicalFeatures.Report(query, bars.Sessions, Closures(), ct);
    }

    // A cited closure is the retained holiday fixture the Data Health read already uses; nothing is inferred.
    private HashSet<DateOnly> Closures()
    {
        if (!File.Exists(holidayFixturePath)) throw Unavailable();
        try { return DataHealthCalendar.Load(holidayFixturePath).Keys.ToHashSet(); }
        catch (Exception error) when (error is InvalidOperationException or JsonException or IOException
        or UnauthorizedAccessException or ArgumentException)
        { throw Unavailable(); }
    }

    private static PersonalTechnicalFeaturesException Unavailable() =>
        new(503, "PERSONAL_TECHNICAL_CALENDAR_UNAVAILABLE",
            "The cited exchange closure calendar is unavailable, so personal continuity cannot be established.");
}
