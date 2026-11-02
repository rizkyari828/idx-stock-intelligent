using System.Text.Json;
using IdxStockIntelligence.Application;

namespace IdxStockIntelligence.Infrastructure;

/// <summary>
/// Read-only descriptive screening over the retained pilot panel. It reads the bounded personal session window
/// once through the existing <see cref="PersonalBarFileStore"/>, projects every panel symbol through the existing
/// <see cref="PersonalTechnicalFeatures"/> arithmetic, and hands the rows to <see cref="PersonalScreening"/>. It
/// owns no formula, admission, database, cache, persistence or provider access.
/// </summary>
public sealed class PersonalScreeningService(PersonalBarFileStore store, string holidayFixturePath,
    string panelFixturePath)
{
    public async Task<PersonalScreeningReport> ReadAsync(PersonalScreeningQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var from = query.Through.AddDays(-(PersonalTechnicalFeatures.WindowDays - 1));
        var bars = await store.ReadAsync(new PersonalBarsQuery(from, query.Through, false), ct);
        var closures = Closures();
        var panel = Panel();
        var entries = new List<PersonalScreeningEntry>(panel.Count);
        foreach (var instrument in panel)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var report = PersonalTechnicalFeatures.Report(
                    new PersonalTechnicalQuery(instrument.Symbol, query.Through, query.Cutoff),
                    bars.Sessions, closures, ct);
                entries.Add(new(instrument, report, null));
            }
            catch (PersonalTechnicalFeaturesException error)
                when (error.Code == PersonalTechnicalFeatures.MissingCapture)
            {
                // One absent symbol is a visible row, never a failed panel and never an empty value.
                entries.Add(new(instrument, null, error.Code));
            }
        }
        return PersonalScreening.Build(query, entries);
    }

    // The cited closure fixture is the one the Data Health and personal technical reads already use.
    private HashSet<DateOnly> Closures()
    {
        if (!File.Exists(holidayFixturePath)) throw Unavailable("PERSONAL_SCREENING_CALENDAR_UNAVAILABLE");
        try { return DataHealthCalendar.Load(holidayFixturePath).Keys.ToHashSet(); }
        catch (Exception error) when (error is InvalidOperationException or JsonException or IOException
        or UnauthorizedAccessException or ArgumentException)
        { throw Unavailable("PERSONAL_SCREENING_CALENDAR_UNAVAILABLE"); }
    }

    // Symbols and names come only from the retained panel fixture; no request can add a panel member.
    private List<PersonalPanelInstrument> Panel()
    {
        if (!File.Exists(panelFixturePath)) throw Unavailable("PERSONAL_SCREENING_PANEL_UNAVAILABLE");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(panelFixturePath));
            if (!document.RootElement.TryGetProperty("instruments", out var instruments)
                || instruments.ValueKind != JsonValueKind.Array)
                throw Unavailable("PERSONAL_SCREENING_PANEL_UNAVAILABLE");
            var panel = new List<PersonalPanelInstrument>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in instruments.EnumerateArray())
            {
                var symbol = entry.TryGetProperty("symbol", out var symbolValue) && symbolValue.ValueKind == JsonValueKind.String
                    ? symbolValue.GetString()?.Trim().ToUpperInvariant() : null;
                var name = entry.TryGetProperty("name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String
                    ? nameValue.GetString()?.Trim() : null;
                if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(name) || !seen.Add(symbol))
                    throw Unavailable("PERSONAL_SCREENING_PANEL_UNAVAILABLE");
                panel.Add(new(symbol, name));
            }
            return panel.Count > 0 ? panel : throw Unavailable("PERSONAL_SCREENING_PANEL_UNAVAILABLE");
        }
        catch (JsonException) { throw Unavailable("PERSONAL_SCREENING_PANEL_UNAVAILABLE"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw Unavailable("PERSONAL_SCREENING_PANEL_UNAVAILABLE"); }
    }

    private static PersonalScreeningException Unavailable(string code) =>
        new(503, code, code == "PERSONAL_SCREENING_CALENDAR_UNAVAILABLE"
            ? "The cited exchange closure calendar is unavailable, so personal continuity cannot be established."
            : "The retained pilot panel is unavailable.");
}
