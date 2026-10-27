using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

/// <summary>
/// Personal, non-canonical projection of the thirteen existing technical features for one symbol. It
/// prepares a calendar-continuous personal session series, then forwards it to
/// <see cref="TechnicalFeatures.Project"/> unchanged: no formula, period, admission, cache or persistence
/// is added here. Every response states its personal provenance, and no value it carries is canonical,
/// VERIFIED or DATA_READY.
/// </summary>
public static class PersonalTechnicalFeatures
{
    public const string Source = "PERSONAL_FILE_STORE";
    public const string SessionProof = "UNPROVEN_PERSONAL";

    /// <summary>
    /// A personal capture retains no volume unit, basis or market segment, so the volume-derived features
    /// stay unavailable with the same reason the canonical reader uses. They are never zero or guessed.
    /// </summary>
    public const string VolumeReason = "VOLUME_BASIS_UNVERIFIED";

    /// <summary>Every feature of an empty run reports this; a personal run that is not continuous projects nothing.</summary>
    public const string NoContinuousRun = "NO_CONTINUOUS_PERSONAL_RUN";

    public const string MissingCapture = "PERSONAL_CAPTURE_MISSING";

    /// <summary>One bounded read window; the trailing run never spans more calendar days than this.</summary>
    public const int WindowDays = PersonalBarsQuery.MaximumRangeDays;

    /// <summary>The fixed panel's benchmark member; the personal store carries one row per panel member.</summary>
    public const string BenchmarkSymbol = "JKSE.INDX";

    private static readonly IReadOnlyDictionary<DateOnly, DailyBar> NoBenchmark =
        new Dictionary<DateOnly, DailyBar>();

    /// <summary>
    /// Normalizes the requested symbol with trim + invariant uppercase and resolves the single request
    /// clock. A symbol is matched exactly from then on; nothing here approximates a symbol.
    /// </summary>
    public static PersonalTechnicalQuery Query(string? symbol, IReadOnlyDictionary<string, string?> values,
        DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Keys.Any(key => key is not ("through" or "cutoff")))
            throw Invalid("Unknown personal technical-features parameter.");
        if (values.Values.Any(value => value is null))
            throw Invalid("Personal technical-features parameters require one value each.");
        return new(Symbol(symbol), Through(values.GetValueOrDefault("through"), today),
            Cutoff(values.GetValueOrDefault("cutoff"), now));
    }

    public static string Symbol(string? value)
    {
        var symbol = value?.Trim().ToUpperInvariant() ?? "";
        if (symbol.Length is 0 or > 32
            || symbol.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_')))
            throw Invalid("symbol must be 1..32 characters of A-Z, 0-9, '.', '-' or '_'.");
        return symbol;
    }

    /// <summary>
    /// Projects the thirteen features over the trailing calendar-continuous personal run ending at
    /// <c>through</c>. Continuity follows the canonical rule: a weekend or a cited closure is skipped
    /// without breaking the run, while a session with no visible capture for this exact symbol ends it.
    /// A capture whose recorded capture time is later than the cutoff is not visible at that cutoff.
    /// </summary>
    public static PersonalTechnicalFeaturesReport Report(PersonalTechnicalQuery query,
        IReadOnlyList<PersonalBarSession> sessions, IReadOnlySet<DateOnly> citedClosures, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(citedClosures);
        var visible = Visible(query, sessions, ct);
        if (visible.Count == 0)
            throw new PersonalTechnicalFeaturesException(404, MissingCapture,
                $"No personal capture of {query.Symbol} is visible through {query.Through:yyyy-MM-dd} at this cutoff.");
        var bars = new List<DailyBar>();
        var marketDate = (DateOnly?)null;
        var runStart = (DateOnly?)null;
        var earliest = query.Through.AddDays(-(WindowDays - 1));
        for (var date = query.Through; date >= earliest; date = date.AddDays(-1))
        {
            ct.ThrowIfCancellationRequested();
            // A weekend or a cited closure is not a session and does not break the run.
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || citedClosures.Contains(date)) continue;
            if (!visible.TryGetValue(date, out var capture)) break; // A missing session ends the run.
            bars.Add(Bar(capture, query.Symbol));
            marketDate ??= date;
            runStart = date;
            if (bars.Count >= WindowDays) break;
        }
        bars.Reverse();
        // The personal store holds every panel member per session, so the benchmark series is read from the
        // same captures. A missing benchmark row on any aligned date yields BENCHMARK_MISSING downstream,
        // never a fabricated relative return.
        var benchmark = Benchmark(query, sessions, bars.Select(bar => bar.SessionDate).ToHashSet(), ct);
        var isIndex = Normalize(query.Symbol) == BenchmarkSymbol;
        var fields = TechnicalFeatures.Project(bars, benchmark,
            index: isIndex, bars.Count == 0 ? NoContinuousRun : null, VolumeReason, VolumeReason, ct);
        var features = TechnicalFeatures.Names.ToDictionary(name => name, name =>
        {
            var state = fields[name];
            return new TechnicalFeatureValue(state.Availability.ToString(), state.Value, state.UnavailableReason,
                TechnicalFeatures.MinimumSessions[name]);
        }, StringComparer.Ordinal);
        return new(Source, SessionProof, query.Symbol, query.Through, query.Cutoff, marketDate, runStart, bars.Count,
            Note(bars.Count, marketDate, features), features);
    }

    /// <summary>
    /// Reads the benchmark series from the same personal sessions, honouring the request cutoff. Only the
    /// dates already in this symbol's run are needed; anything missing stays absent so the feature reports
    /// BENCHMARK_MISSING instead of guessing.
    /// </summary>
    private static IReadOnlyDictionary<DateOnly, DailyBar> Benchmark(PersonalTechnicalQuery query,
        IReadOnlyList<PersonalBarSession> sessions, HashSet<DateOnly> runDates, CancellationToken ct)
    {
        if (Normalize(query.Symbol) == BenchmarkSymbol || runDates.Count == 0) return NoBenchmark;
        var benchmark = new Dictionary<DateOnly, DailyBar>();
        foreach (var session in sessions)
        {
            ct.ThrowIfCancellationRequested();
            if (!runDates.Contains(session.SessionDate)) continue;
            var capturedAt = CapturedAt(session);
            if (capturedAt > query.Cutoff) continue;
            var row = session.Rows.FirstOrDefault(candidate => Normalize(candidate.Symbol) == BenchmarkSymbol);
            if (row is null) continue;
            benchmark[session.SessionDate] = Bar(new PersonalCapture(session.SessionDate, row, capturedAt), BenchmarkSymbol);
        }
        return benchmark;
    }

    private static Dictionary<DateOnly, PersonalCapture> Visible(PersonalTechnicalQuery query,
        IReadOnlyList<PersonalBarSession> sessions, CancellationToken ct)
    {
        var visible = new Dictionary<DateOnly, PersonalCapture>();
        foreach (var session in sessions)
        {
            ct.ThrowIfCancellationRequested();
            if (session.SessionDate > query.Through) continue;
            var capturedAt = CapturedAt(session);
            // A capture recorded after the cutoff is not yet known, even when its session is older.
            if (capturedAt > query.Cutoff) continue;
            var row = session.Rows.FirstOrDefault(candidate => Normalize(candidate.Symbol) == query.Symbol);
            if (row is null) continue;
            visible[session.SessionDate] = new(session.SessionDate, row, capturedAt);
        }
        return visible;
    }

    private static string Normalize(string symbol) => symbol.Trim().ToUpperInvariant();

    private static DateTimeOffset CapturedAt(PersonalBarSession session)
    {
        // The store validates that captured_at is a non-empty string; chronology needs its instant.
        if (DateTimeOffset.TryParse(session.CapturedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var captured))
            return captured;
        throw Corrupt(session.SessionDate, "captured_at is not a timestamp.");
    }

    private static DailyBar Bar(PersonalCapture capture, string symbol)
    {
        var day = capture.SessionDate;
        var (artifact, hash) = Address(capture);
        try
        {
            return new DailyBar(Identity(symbol), day, Price(capture.Row.Open, day, "open"),
                Price(capture.Row.High, day, "high"), Price(capture.Row.Low, day, "low"),
                Price(capture.Row.Close, day, "close"), capture.Row.Volume,
                new SourceReference(Source, artifact, capture.CapturedAt, capture.CapturedAt, hash));
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        {
            throw Corrupt(day, error.Message);
        }
    }

    private static decimal Price(string text, DateOnly session, string field)
    {
        // Prices are retained as exact decimal strings; a non-decimal capture is corruption, never a gap.
        if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value)) return value;
        throw Corrupt(session, field + " is not an exact decimal.");
    }

    /// <summary>
    /// The arithmetic never reads a bar's source, but a bar requires one. Both values are derived from the
    /// retained personal row itself: a content digest and a content-addressed artifact id. No canonical
    /// artifact, provenance or identity is claimed by them.
    /// </summary>
    private static (Guid Artifact, string Hash) Address(PersonalCapture capture)
    {
        var text = string.Join('|', capture.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Normalize(capture.Row.Symbol), capture.Row.Open, capture.Row.High, capture.Row.Low, capture.Row.Close,
            capture.Row.AdjustedClose, capture.Row.Volume.ToString(CultureInfo.InvariantCulture));
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return (new Guid(digest.AsSpan(0, 16)), Convert.ToHexString(digest).ToLowerInvariant());
    }

    /// <summary>A stable, obviously non-canonical instrument id; the arithmetic never reads it either.</summary>
    private static InstrumentId Identity(string symbol) =>
        new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(Source + "|" + symbol)).AsSpan(0, 16)));

    private static string Note(int sessions, DateOnly? marketDate,
        IReadOnlyDictionary<string, TechnicalFeatureValue> features)
    {
        const string provenance = "Personal capture is never canonical, verified or DATA_READY.";
        if (sessions == 0)
            return "No calendar-continuous personal session ends at this through date, so nothing is projected. " + provenance;
        var available = features.Values.Count(value => value.Availability == nameof(Availability.AVAILABLE));
        var warmup = features.Values.Count(value => value.Availability == nameof(Availability.WARMUP));
        return $"{available} of {features.Count} features AVAILABLE from {sessions} calendar-continuous " +
            $"UNPROVEN_PERSONAL session{(sessions == 1 ? "" : "s")} ending {marketDate!.Value:yyyy-MM-dd}; " +
            $"{warmup} still warming up. " + provenance;
    }

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

    private static PersonalTechnicalFeaturesException Invalid(string message) =>
        new(400, "PERSONAL_TECHNICAL_QUERY_INVALID", message);

    /// <summary>Named so a corrupt row is reported with the store's own corruption code.</summary>
    private static PersonalBarsException Corrupt(DateOnly session, string detail) =>
        new(500, "PERSONAL_BARS_CORRUPT", $"Personal capture {session:yyyy-MM-dd} is invalid: {detail}");

    private sealed record PersonalCapture(DateOnly SessionDate, PersonalBarRow Row, DateTimeOffset CapturedAt);
}

public sealed record PersonalTechnicalQuery(string Symbol, DateOnly Through, DateTimeOffset Cutoff);

/// <summary>
/// The personal inverse of a canonical feature report. It carries the run it projected and never a
/// certification: <c>sessionProof</c> stays UNPROVEN_PERSONAL and a non-available feature stays null.
/// </summary>
public sealed record PersonalTechnicalFeaturesReport(string Source, string SessionProof, string Symbol,
    DateOnly Through, DateTimeOffset Cutoff, DateOnly? MarketDate, DateOnly? RunStart, int Sessions,
    string WarmupNote, IReadOnlyDictionary<string, TechnicalFeatureValue> Features);

public sealed class PersonalTechnicalFeaturesException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
