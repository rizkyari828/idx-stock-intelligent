using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class DataHealthTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 10, 23);
    private static readonly string[] ExpectedObservations = ["FLAT_OHLC", "ZERO_VOLUME"];

    private static Guid Instrument(int index) =>
        Guid.Parse("00000000-0000-4000-8000-" + index.ToString("D12", System.Globalization.CultureInfo.InvariantCulture));

    private static DataHealthBar Bar(int index, DateOnly day, string quality = "VALID",
        decimal open = 100m, decimal high = 101m, decimal low = 99m, decimal close = 100.5m, long volume = 1_000)
        => new(Instrument(index), $"SYM{index}.JK", day, open, high, low, close, volume, quality);

    private static DataHealthBar[] Panel(DateOnly day, int count = 11, string quality = "VALID") =>
        Enumerable.Range(1, count).Select(index => Bar(index, day, quality)).ToArray();

    [Fact]
    public void EmptyCanonicalStoreIsNotDegradedAndHasNoSessions()
    {
        var report = DataHealthReport.Build(Now, 11, []);
        Assert.Null(report.LastSession);
        Assert.Empty(report.Sessions);
        Assert.Empty(report.Gaps);
        Assert.Empty(report.Anomalies);
        Assert.False(report.Degraded);
        Assert.All(DataHealthReport.QualityStatuses, status => Assert.Equal(0, report.Quality[status]));
    }

    [Fact]
    public void OneCompleteSessionReportsCoverageAndLastSession()
    {
        var report = DataHealthReport.Build(Now, 11, Panel(Day));
        Assert.Equal(Day, report.LastSession);
        var session = Assert.Single(report.Sessions);
        Assert.Equal(Day, session.Date);
        Assert.Equal(11, session.Bars);
        Assert.Equal(11, session.Instruments);
        Assert.Equal(0, session.CoverageGap);
        Assert.Equal(11, session.Quality["VALID"]);
        Assert.Equal(11, report.Quality["VALID"]);
        Assert.Empty(report.Gaps);
        Assert.Empty(report.Anomalies);
        Assert.False(report.Degraded);
    }

    [Fact]
    public void MissingPanelMembersAreCoverageGapsOnly()
    {
        var report = DataHealthReport.Build(Now, 11, Panel(Day, count: 9));
        var session = Assert.Single(report.Sessions);
        Assert.Equal(9, session.Bars);
        Assert.Equal(9, session.Instruments);
        Assert.Equal(2, session.CoverageGap);
        Assert.False(report.Degraded);
    }

    [Fact]
    public void FlatAndZeroVolumeRowsAreObservationsNotSessionProof()
    {
        var flat = Bar(1, Day, open: 100m, high: 100m, low: 100m, close: 100m, volume: 500);
        var zero = Bar(2, Day, volume: 0);
        var report = DataHealthReport.Build(Now, 11, [flat, zero]);
        Assert.Equal(ExpectedObservations, report.Anomalies.Select(item => item.Observation).ToArray());
        Assert.Equal(1, report.Sessions[0].Flat);
        Assert.Equal(1, report.Sessions[0].ZeroVolume);
        Assert.Contains("do not prove", DataHealthReport.ObservationNote, StringComparison.Ordinal);
    }

    [Fact]
    public void WeekdayGapsCarryHolidayMarkersOnlyFromTheFixture()
    {
        var holidays = new Dictionary<DateOnly, DataHealthHoliday>
        {
            [new(2026, 8, 17)] = new(new(2026, 8, 17), "Proklamasi Kemerdekaan RI", "national_holiday")
        };
        var bars = Panel(new DateOnly(2026, 8, 14)).Concat(Panel(new DateOnly(2026, 8, 18))).ToArray();
        var report = DataHealthReport.Build(Now, 11, bars, holidays);
        var gap = Assert.Single(report.Gaps);
        Assert.Equal(new DateOnly(2026, 8, 17), gap.Date);
        Assert.True(gap.KnownHoliday);
        Assert.Equal("Proklamasi Kemerdekaan RI", gap.HolidayName);
        Assert.Equal("national_holiday", gap.HolidayKind);
    }

    [Fact]
    public void NonHolidayWeekdayGapIsMarkedUnknownNotAssumedClosed()
    {
        var bars = Panel(new DateOnly(2026, 10, 22)).Concat(Panel(new DateOnly(2026, 10, 26))).ToArray();
        var report = DataHealthReport.Build(Now, 11, bars, new Dictionary<DateOnly, DataHealthHoliday>());
        var gap = Assert.Single(report.Gaps);
        Assert.Equal(new DateOnly(2026, 10, 23), gap.Date);
        Assert.False(gap.KnownHoliday);
        Assert.Null(gap.HolidayName);
    }

    [Fact]
    public void NonValidQualitySetsTheDegradedFlag()
    {
        var report = DataHealthReport.Build(Now, 11, Panel(Day, quality: "DEGRADED"));
        Assert.True(report.Degraded);
        Assert.Equal(11, report.Quality["DEGRADED"]);
        Assert.Equal(0, report.Quality["VALID"]);
        Assert.Equal(11, report.Sessions[0].Quality["DEGRADED"]);
    }

    [Fact]
    public void DuplicateCanonicalRowsFailClosed()
    {
        var rows = new[] { Bar(1, Day), Bar(1, Day, close: 101m) };
        Assert.Throws<InvalidOperationException>(() => DataHealthReport.Build(Now, 11, rows));
    }

    [Fact]
    public void HolidayFixtureLoadsDatesNamesAndMissingFileIsEmpty()
    {
        using var temporary = new TemporaryDirectory();
        var path = Path.Combine(temporary.Path, "holidays.json");
        File.WriteAllText(path, """
            {"year":2026,"holidays":[{"date":"2026-08-17","name":"Proklamasi Kemerdekaan RI","kind":"national_holiday"}]}
            """);
        var holidays = DataHealthCalendar.Load(path);
        Assert.True(holidays.ContainsKey(new DateOnly(2026, 8, 17)));
        Assert.Equal("national_holiday", holidays[new DateOnly(2026, 8, 17)].Kind);
        Assert.Empty(DataHealthCalendar.Load(Path.Combine(temporary.Path, "absent.json")));
        File.WriteAllText(path, """{"year":2026,"holidays":[{"date":"17-08-2026"}]}""");
        Assert.Throws<InvalidOperationException>(() => DataHealthCalendar.Load(path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "idx-health-" + Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
        }
    }
}
