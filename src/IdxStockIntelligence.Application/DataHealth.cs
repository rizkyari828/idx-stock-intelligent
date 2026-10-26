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

public sealed record DataHealthReport(DateTimeOffset GeneratedAt, DateOnly? LastSession, int ExpectedPanel,
    IReadOnlyList<DataHealthSession> Sessions, IReadOnlyList<DataHealthGap> Gaps,
    IReadOnlyList<DataHealthAnomaly> Anomalies, IReadOnlyDictionary<string, int> Quality, bool Degraded)
{
    public const string ObservationNote =
        "Flat OHLC and zero-volume rows are observations about retained data only; they do not prove no trade, suspension or a completed session.";

    public static readonly string[] QualityStatuses = ["UNKNOWN", "VALID", "REJECTED", "DEGRADED", "STALE"];

    /// <summary>Pure aggregation over canonical rows; no clock other than the caller's generatedAt.</summary>
    public static DataHealthReport Build(DateTimeOffset generatedAt, int expectedPanel,
        IEnumerable<DataHealthBar> bars, IReadOnlyDictionary<DateOnly, DataHealthHoliday>? holidays = null)
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
            sessions, gaps, anomalies, quality, degraded);
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
