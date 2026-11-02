namespace IdxStockIntelligence.Application;

/// <summary>One latest canonical revision row, already reduced by (instrument, session).</summary>
public sealed record DataHealthBar(Guid InstrumentId, string Symbol, DateOnly SessionDate,
    decimal Open, decimal High, decimal Low, decimal Close, long Volume, string QualityStatus);

public sealed record DataHealthHoliday(DateOnly Date, string Name, string Kind);

public sealed record DataHealthSession(DateOnly Date, int Bars, int Instruments, int CoverageGap,
    IReadOnlyDictionary<string, int> Quality, int Flat, int ZeroVolume);

/// <summary>A weekday between retained sessions without rows. KnownHoliday comes only from the cited fixture.</summary>
public sealed record DataHealthGap(DateOnly Date, bool KnownHoliday, string? HolidayName, string? HolidayKind);

/// <summary>An observation about a stored row; never evidence of a session, trade or suspension.</summary>
public sealed record DataHealthAnomaly(DateOnly Date, Guid InstrumentId, string Symbol,
    string Observation, string QualityStatus);

/// <summary>
/// One close comparison between the two newest retained sessions. A missing side stays explicit and null;
/// it is never treated as unchanged and no percentage is divided by a missing close.
/// </summary>
public sealed record DataHealthDeltaChange(Guid InstrumentId, string Symbol, string Status,
    DateOnly? PreviousSession, decimal? PreviousClose, DateOnly? NewSession, decimal? NewClose,
    decimal? Change, decimal? ChangePercent, string? Reason);

/// <summary>An instrument present in exactly one of the two compared sessions.</summary>
public sealed record DataHealthDeltaCoverage(Guid InstrumentId, string Symbol, string PresentIn);

/// <summary>
/// Factual delta between the two newest canonical sessions. Flat/zero-volume counts are observations about
/// retained rows, not evidence about trading status.
/// </summary>
public sealed record DataHealthDelta(DateOnly? Since, DateOnly? PreviousSession, DateOnly? NewSession,
    IReadOnlyList<DateOnly> NewSessions, IReadOnlyList<DataHealthDeltaChange> Changes,
    IReadOnlyList<DataHealthDeltaCoverage> CoverageDifferences, int Flat, int ZeroVolume,
    IReadOnlyDictionary<string, int> Quality, bool Degraded, string Note)
{
    public const string Compared = "COMPARED";
    public const string Missing = "MISSING";
    public const string PreviousOnly = "PREVIOUS_ONLY";
    public const string NewOnly = "NEW_ONLY";

    public const string NoteText =
        "Facts only: a close change is not a cause, signal, breakout or recommendation. Flat and zero-volume " +
        "counts are observations about retained rows; they do not prove no trade, suspension, a holiday or " +
        "a completed session. A missing instrument is not unchanged and no percentage is divided by a " +
        "missing close.";
}

public sealed record DataHealthReport(DateTimeOffset GeneratedAt, DateOnly? LastSession, int ExpectedPanel,
    IReadOnlyList<DataHealthSession> Sessions, IReadOnlyList<DataHealthGap> Gaps,
    IReadOnlyList<DataHealthAnomaly> Anomalies, IReadOnlyDictionary<string, int> Quality, bool Degraded,
    DataHealthDelta Delta)
{
    public const string ObservationNote =
        "Flat OHLC and zero-volume rows are observations about retained data only; they do not prove no trade, suspension or a completed session.";

    public static readonly string[] QualityStatuses = ["UNKNOWN", "VALID", "REJECTED", "DEGRADED", "STALE"];

    /// <summary>Pure aggregation over canonical rows; no clock other than the caller's generatedAt.</summary>
    public static DataHealthReport Build(DateTimeOffset generatedAt, int expectedPanel,
        IEnumerable<DataHealthBar> bars, IReadOnlyDictionary<DateOnly, DataHealthHoliday>? holidays = null,
        DateOnly? since = null)
    {
        if (expectedPanel <= 0) throw new ArgumentException("Expected panel must be positive.");
        var rows = bars.ToArray();
        holidays ??= new Dictionary<DateOnly, DataHealthHoliday>();
        var seen = new HashSet<(Guid, DateOnly)>();
        foreach (var row in rows)
            if (!seen.Add((row.InstrumentId, row.SessionDate)))
                throw new InvalidOperationException("DATA_HEALTH_DUPLICATE_CANONICAL_ROW");
        var quality = DataHealthQualityCounts();
        foreach (var row in rows)
        {
            if (!quality.TryGetValue(row.QualityStatus, out var count))
                throw new InvalidOperationException("DATA_HEALTH_QUALITY_STATUS_UNKNOWN");
            quality[row.QualityStatus] = count + 1;
        }
        var degraded = rows.Any(row => !string.Equals(row.QualityStatus, "VALID", StringComparison.Ordinal));

        var sessions = rows.GroupBy(row => row.SessionDate)
            .OrderByDescending(group => group.Key)
            .Select(group =>
            {
                var statuses = DataHealthQualityCounts();
                foreach (var row in group) statuses[row.QualityStatus]++;
                var instruments = group.Select(row => row.InstrumentId).Distinct().Count();
                return new DataHealthSession(group.Key, group.Count(), instruments,
                    Math.Max(0, expectedPanel - instruments), statuses,
                    group.Count(IsFlat), group.Count(row => row.Volume == 0));
            }).ToArray();

        var anomalies = rows
            .SelectMany(row => Observations(row).Select(kind =>
                new DataHealthAnomaly(row.SessionDate, row.InstrumentId, row.Symbol, kind, row.QualityStatus)))
            .OrderBy(item => item.Date).ThenBy(item => item.Symbol, StringComparer.Ordinal)
            .ThenBy(item => item.Observation, StringComparer.Ordinal)
            .ToArray();

        var gaps = new List<DataHealthGap>();
        if (sessions.Length > 1)
        {
            var retained = sessions.Select(session => session.Date).ToHashSet();
            var first = retained.Min();
            var last = retained.Max();
            for (var day = first.AddDays(1); day < last; day = day.AddDays(1))
            {
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || retained.Contains(day)) continue;
                holidays.TryGetValue(day, out var holiday);
                gaps.Add(new(day, holiday is not null, holiday?.Name, holiday?.Kind));
            }
        }

        return new(generatedAt, sessions.Length == 0 ? null : sessions.Max(session => session.Date), expectedPanel,
            sessions, gaps, anomalies, quality, degraded, BuildDelta(sessions, rows, since));
    }

    /// <summary>
    /// Compares only the two newest retained sessions. The requested <paramref name="since"/> only widens the
    /// listed new-session dates; it never changes which pair is compared.
    /// </summary>
    private static DataHealthDelta BuildDelta(DataHealthSession[] sessions, DataHealthBar[] rows, DateOnly? since)
    {
        if (sessions.Length == 0)
            return new(since, null, null, [], [], [], 0, 0, DataHealthQualityCounts(), false,
                "No canonical session is retained, so no delta is computed.");
        var newest = sessions[0];
        var previous = sessions.Length > 1 ? sessions[1] : null;
        var effectiveSince = since ?? previous?.Date;
        var newSessions = effectiveSince is null
            ? Array.Empty<DateOnly>()
            : sessions.Where(session => session.Date > effectiveSince.Value).Select(session => session.Date).ToArray();
        var newDegraded = newest.Quality.Any(item => item.Key != "VALID" && item.Value > 0);
        if (previous is null)
            return new(since, null, newest.Date, newSessions, [], [], newest.Flat, newest.ZeroVolume, newest.Quality,
                newDegraded, "Only one canonical session is retained; a delta needs two sessions and no change is inferred.");

        var previousRows = rows.Where(row => row.SessionDate == previous.Date)
            .ToDictionary(row => row.InstrumentId);
        var newRows = rows.Where(row => row.SessionDate == newest.Date)
            .ToDictionary(row => row.InstrumentId);
        var changes = new List<DataHealthDeltaChange>();
        var coverage = new List<DataHealthDeltaCoverage>();
        foreach (var instrument in previousRows.Keys.Union(newRows.Keys))
        {
            var hasPrevious = previousRows.TryGetValue(instrument, out var before);
            var hasNew = newRows.TryGetValue(instrument, out var after);
            var symbol = hasNew ? after!.Symbol : before!.Symbol;
            if (hasPrevious && hasNew)
            {
                var change = after!.Close - before!.Close;
                changes.Add(new(instrument, symbol, DataHealthDelta.Compared, previous.Date, before.Close,
                    newest.Date, after.Close, change, before.Close == 0 ? null : change / before.Close * 100,
                    before.Close == 0 ? "PREVIOUS_CLOSE_ZERO" : null));
                continue;
            }
            changes.Add(new(instrument, symbol, DataHealthDelta.Missing,
                hasPrevious ? previous.Date : null, hasPrevious ? before!.Close : null,
                hasNew ? newest.Date : null, hasNew ? after!.Close : null, null, null,
                hasPrevious ? "MISSING_NEW_SESSION" : "MISSING_PREVIOUS_SESSION"));
            coverage.Add(new(instrument, symbol,
                hasPrevious ? DataHealthDelta.PreviousOnly : DataHealthDelta.NewOnly));
        }

        return new(since, previous.Date, newest.Date, newSessions,
            changes.OrderBy(change => change.Symbol, StringComparer.Ordinal)
                .ThenBy(change => change.InstrumentId).ToArray(),
            coverage.OrderBy(item => item.Symbol, StringComparer.Ordinal)
                .ThenBy(item => item.InstrumentId).ToArray(),
            newest.Flat, newest.ZeroVolume, newest.Quality, newDegraded, DataHealthDelta.NoteText);
    }

    private static Dictionary<string, int> DataHealthQualityCounts() =>
        QualityStatuses.ToDictionary(status => status, _ => 0, StringComparer.Ordinal);

    private static bool IsFlat(DataHealthBar row) =>
        row.Open == row.High && row.High == row.Low && row.Low == row.Close;

    private static IEnumerable<string> Observations(DataHealthBar row)
    {
        if (IsFlat(row)) yield return "FLAT_OHLC";
        if (row.Volume == 0) yield return "ZERO_VOLUME";
    }
}
