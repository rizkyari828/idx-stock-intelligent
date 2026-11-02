using System.Globalization;

namespace IdxStockIntelligence.Application;

/// <summary>
/// Descriptive screening over the pilot panel. It consumes the thirteen feature states that
/// <see cref="PersonalTechnicalFeatures"/> already projects for Phase B.1 and does nothing else: it orders rows
/// by one already-existing feature and reports each row's feature states. It adds no threshold, score,
/// recommendation, admission, session proof or persistence, and it never emits a readiness or verification
/// token. A row whose ordering feature is not AVAILABLE is not ranked; it is listed after the ranked rows with
/// the feature's own state and reason.
/// </summary>
public static class PersonalScreening
{
    public const string Source = PersonalTechnicalFeatures.Source;
    public const string SessionProof = PersonalTechnicalFeatures.SessionProof;
    public const string SurfaceName = "DESCRIPTIVE_DIAGNOSTICS";
    public const string PanelName = "PILOT";

    public const string Ranked = "RANKED";
    public const string NotRanked = "NOT_RANKED";
    public const string NoCapture = "NO_PERSONAL_CAPTURE";

    public const string Ascending = "ASC";
    public const string Descending = "DESC";
    public const string AllRows = "ALL";
    public const string AvailableRows = "AVAILABLE";

    /// <summary>
    /// The literal limits the surface must carry. None of these is a policy state; they describe what the
    /// personal, unproven capture cannot support.
    /// </summary>
    public static IReadOnlyList<string> Limitations { get; } =
    [
        "Personal/unverified data — not a canonical bar and not an exchange tape.",
        "Descriptive only — no tradability, eligibility or recommendation is claimed.",
        "No confirmed trading status is known for these personal sessions.",
        "No corporate-action coverage is retained.",
        "No guaranteed session completeness — a missing or uncited weekday gap ends a personal run."
    ];

    public const string RankingNote =
        "Only AVAILABLE feature values are ordered. A row whose ordering feature is WARMUP or UNAVAILABLE keeps " +
        "rank null, sorts after every ranked row, and shows that feature's own state and reason.";

    /// <summary>
    /// Resolves one request clock and the ordering controls. Every value is validated here; nothing is
    /// approximated. The through/cutoff rules match the personal technical-features read.
    /// </summary>
    public static PersonalScreeningQuery Query(IReadOnlyDictionary<string, string?> values, DateOnly today,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Keys.Any(key => key is not ("through" or "cutoff" or "sort" or "direction" or "availability")))
            throw Invalid("Unknown personal screening parameter.");
        if (values.Values.Any(value => value is null))
            throw Invalid("Personal screening parameters require one value each.");
        var sort = values.GetValueOrDefault("sort") ?? "changePercent";
        if (!TechnicalFeatures.Names.Contains(sort, StringComparer.Ordinal))
            throw Invalid("sort must be one of the thirteen reported feature names.");
        var direction = (values.GetValueOrDefault("direction") ?? Descending).Trim().ToUpperInvariant();
        if (direction is not (Ascending or Descending))
            throw Invalid("direction must be ASC or DESC.");
        var availability = (values.GetValueOrDefault("availability") ?? AllRows).Trim().ToUpperInvariant();
        if (availability is not (AllRows or AvailableRows))
            throw Invalid("availability must be ALL or AVAILABLE.");
        return new(Through(values.GetValueOrDefault("through"), today), Cutoff(values.GetValueOrDefault("cutoff"), now),
            sort, direction, availability);
    }

    /// <summary>
    /// Pure ordering over already-projected personal reports. A missing report is the explicit no-capture
    /// state, never an empty or complete-looking row.
    /// </summary>
    public static PersonalScreeningReport Build(PersonalScreeningQuery query, IReadOnlyList<PersonalScreeningEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(entries);
        var rows = entries.Select(entry => Row(query, entry)).ToArray();
        var ranked = rows.Where(row => row.Status == Ranked);
        var orderedRanked = (query.Direction == Ascending
                ? ranked.OrderBy(row => row.SortValue!.Value)
                : ranked.OrderByDescending(row => row.SortValue!.Value))
            .ThenBy(row => row.Symbol, StringComparer.Ordinal)
            .ToArray();
        // Unranked rows are ordered after every ranked row and never interleaved by a missing value.
        var unranked = rows.Where(row => row.Status != Ranked)
            .OrderBy(row => row.Status == NoCapture ? 1 : 0)
            .ThenBy(row => row.Symbol, StringComparer.Ordinal)
            .ToArray();
        var ordered = query.Availability == AvailableRows
            ? orderedRanked
            : [.. orderedRanked, .. unranked];
        var withRanks = ordered.Select((row, index) =>
            row.Status == Ranked ? row with { Rank = index + 1 } : row).ToArray();
        return new(Source, SessionProof, SurfaceName, PanelName, query.Through, query.Cutoff, query.Sort,
            query.Direction, query.Availability, Limitations, RankingNote, withRanks);
    }

    private static PersonalScreeningRow Row(PersonalScreeningQuery query, PersonalScreeningEntry entry)
    {
        var instrument = entry.Instrument;
        var index = instrument.Symbol == PersonalTechnicalFeatures.BenchmarkSymbol;
        var report = entry.Report;
        if (report is null)
            return new(instrument.Symbol, instrument.Name, index, null, null, 0, NoCapture, null, null, null,
                entry.UnavailableReason ?? PersonalTechnicalFeatures.MissingCapture, 0, 0, 0,
                new Dictionary<string, TechnicalFeatureValue>());
        if (!report.Features.TryGetValue(query.Sort, out var ordering))
            throw new InvalidOperationException("PERSONAL_SCREENING_FEATURE_MISSING");
        var available = ordering.Availability == nameof(Availability.AVAILABLE) && ordering.Value is not null;
        var counts = Counts(report.Features);
        return new(instrument.Symbol, instrument.Name, index, report.MarketDate, report.RunStart, report.Sessions,
            available ? Ranked : NotRanked, null, available ? ordering.Value : null, ordering.Availability,
            available ? null : ordering.Reason ?? ordering.Availability, counts.Available, counts.Warmup,
            counts.Unavailable, report.Features);
    }

    private static (int Available, int Warmup, int Unavailable) Counts(
        IReadOnlyDictionary<string, TechnicalFeatureValue> features) => (
        features.Values.Count(value => value.Availability == nameof(Availability.AVAILABLE)),
        features.Values.Count(value => value.Availability == nameof(Availability.WARMUP)),
        features.Values.Count(value => value.Availability == nameof(Availability.UNAVAILABLE)));

    private static DateOnly Through(string? text, DateOnly today)
    {
        if (text is null) return today;
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var through))
            throw Invalid("through requires YYYY-MM-DD.");
        return through <= today && through >= new DateOnly(1900, 1, 1) ? through
            : throw Invalid("through must be 1900-01-01..today (Jakarta).");
    }

    private static DateTimeOffset Cutoff(string? text, DateTimeOffset now)
    {
        if (text is null) return now.ToUniversalTime();
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var cutoff))
            throw Invalid("cutoff requires an RFC 3339 timestamp.");
        return cutoff <= now && cutoff >= new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero)
            ? cutoff.ToUniversalTime() : throw Invalid("cutoff must be 1900-01-01..now.");
    }

    private static PersonalScreeningException Invalid(string message) =>
        new(400, "PERSONAL_SCREENING_QUERY_INVALID", message);
}

/// <summary>One panel member: the retained pilot symbol and its display name. No canonical identity is claimed.</summary>
public sealed record PersonalPanelInstrument(string Symbol, string Name);

/// <summary>
/// One panel projection attempt. A null report plus a reason is the explicit missing-capture state; an
/// unhandled read failure never becomes a row.
/// </summary>
public sealed record PersonalScreeningEntry(PersonalPanelInstrument Instrument,
    PersonalTechnicalFeaturesReport? Report, string? UnavailableReason);

public sealed record PersonalScreeningQuery(DateOnly Through, DateTimeOffset Cutoff, string Sort, string Direction,
    string Availability);

public sealed record PersonalScreeningRow(string Symbol, string Name, bool Index, DateOnly? MarketDate,
    DateOnly? RunStart, int Sessions, string Status, int? Rank, decimal? SortValue, string? SortAvailability,
    string? Reason, int AvailableCount, int WarmupCount, int UnavailableCount,
    IReadOnlyDictionary<string, TechnicalFeatureValue> Features);

/// <summary>
/// A diagnostics report, never a readiness one: every row carries its own feature states and the report
/// states the source and proof it actually has.
/// </summary>
public sealed record PersonalScreeningReport(string Source, string SessionProof, string Surface, string Panel,
    DateOnly Through, DateTimeOffset Cutoff, string Sort, string Direction, string Availability,
    IReadOnlyList<string> Limitations, string Note, IReadOnlyList<PersonalScreeningRow> Rows);

public sealed class PersonalScreeningException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
