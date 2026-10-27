using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

// The facade must forward availability exactly; no test here computes an indicator.
public sealed class TechnicalFeaturesTests
{
    private static readonly DateTimeOffset Known = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Stock = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid Index = Guid.Parse("10000000-0000-4000-8000-000000000002");
    private static readonly string[] IndexNotApplicable =
        ["volumeRatio20", "dailyValueProxyIdr", "monetaryLiquidity20Idr", "rs20Pp", "rs60Pp"];

    private static DateOnly[] Dates(int count)
    {
        var dates = new List<DateOnly>();
        for (var day = ScreenerReadRequest.Anchor; dates.Count < count; day = day.AddDays(1))
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) dates.Add(day);
        return [.. dates];
    }

    private static DailyBar Bar(Guid id, DateOnly date, decimal close = 100m, long volume = 1_000) =>
        new(new(id), date, close, close + 1m, close - 1m, close, volume,
            new("synthetic", Guid.NewGuid(), Known, Known, new string('a', 64)),
            close / 2m, "SHARE_COUNT_CORROBORATED", "SPLIT_ADJUSTED", "REGULAR");

    private static IReadOnlyList<DailyBar> Bars(int count, Guid? id = null)
    {
        var dates = Dates(count);
        return [.. dates.Select((date, i) => Bar(id ?? Stock, date, 100m + i))];
    }

    private static Dictionary<DateOnly, DailyBar> Benchmark(int count) =>
        Dates(count).ToDictionary(date => date, date => Bar(Index, date, 500m));

    private static IReadOnlyDictionary<string, FeatureState> Project(IReadOnlyList<DailyBar> series, bool index = false,
        string? currentReason = null, string? volumeReason = null) => TechnicalFeatures.Project(series,
        Benchmark(series.Count), index, currentReason, volumeReason, volumeReason,
        TestContext.Current.CancellationToken);

    [Fact]
    public void ProjectExposesExactlyTheThirteenNamedFeatures()
    {
        var fields = Project(Bars(3));
        Assert.Equal(TechnicalFeatures.Names.Count, fields.Count);
        Assert.Equal(TechnicalFeatures.Names.ToHashSet(StringComparer.Ordinal), fields.Keys.ToHashSet(StringComparer.Ordinal));
        Assert.Equal(fields.Count, TechnicalFeatures.MinimumSessions.Count);
        Assert.Equal(TechnicalFeatures.Names.ToHashSet(StringComparer.Ordinal),
            TechnicalFeatures.MinimumSessions.Keys.ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryFeatureIsAvailableExactlyAtItsReportedWindowAndWarmupOneSessionShort()
    {
        foreach (var name in TechnicalFeatures.Names)
        {
            var window = TechnicalFeatures.MinimumSessions[name];
            var shortSeries = Project(Bars(window - 1));
            if (window > 1)
            {
                Assert.Equal(Availability.WARMUP, shortSeries[name].Availability);
                Assert.Equal("INSUFFICIENT_SESSIONS", shortSeries[name].UnavailableReason);
                Assert.Null(shortSeries[name].Value);
            }
            var met = Project(Bars(window))[name];
            Assert.Equal(Availability.AVAILABLE, met.Availability);
            Assert.Null(met.UnavailableReason);
            Assert.NotNull(met.Value);
        }
    }

    [Fact]
    public void WarmupNeverEmitsANumberAndUnavailableReasonIsPassedThrough()
    {
        var warmup = Project(Bars(19));
        Assert.Equal(Availability.WARMUP, warmup["ema20"].Availability);
        Assert.Null(warmup["ema20"].Value);
        var unavailable = Project(Bars(70), currentReason: "NO_CONFIRMED_SESSION");
        Assert.All(unavailable.Values, state =>
        {
            Assert.Equal(Availability.UNAVAILABLE, state.Availability);
            Assert.Equal("NO_CONFIRMED_SESSION", state.UnavailableReason);
            Assert.Null(state.Value);
        });
    }

    [Fact]
    public void VolumeReasonsAreForwardedWithoutInventingALiquidityNumber()
    {
        var fields = Project(Bars(70), volumeReason: "VOLUME_BASIS_UNVERIFIED");
        foreach (var name in new[] { "volumeRatio20", "dailyValueProxyIdr", "monetaryLiquidity20Idr" })
        {
            Assert.Equal(Availability.UNAVAILABLE, fields[name].Availability);
            Assert.Equal("VOLUME_BASIS_UNVERIFIED", fields[name].UnavailableReason);
            Assert.Null(fields[name].Value);
        }
        Assert.Equal(Availability.AVAILABLE, fields["ema20"].Availability);
        Assert.Equal(Availability.AVAILABLE, fields["rs60Pp"].Availability);
    }

    [Fact]
    public void IndexOmitsShareVolumeAndStockRelativeFieldsButKeepsPriceFeatures()
    {
        var fields = Project(Bars(70), index: true);
        foreach (var name in IndexNotApplicable)
        {
            Assert.Equal(Availability.UNAVAILABLE, fields[name].Availability);
            Assert.Equal(TechnicalFeatures.NotApplicableToIndex, fields[name].UnavailableReason);
            Assert.Null(fields[name].Value);
        }
        foreach (var name in new[] { "changePercent", "ema20", "ema50", "atr14", "atrPercent", "priorHigh20",
                     "priorLow20", "distanceToHighPercent" })
        {
            Assert.Equal(Availability.AVAILABLE, fields[name].Availability);
            Assert.NotNull(fields[name].Value);
        }
    }

    [Fact]
    public void ReportCarriesAvailabilityWindowAndMarketDateWithoutAWarmupValue()
    {
        var dates = Dates(3);
        var series = TechnicalFeatures.Series(dates.Select(date => Proof(date)).ToArray(),
            dates.ToDictionary(date => date, date => Bar(Stock, date)), dates[2], Cutoff,
            TestContext.Current.CancellationToken);
        Assert.Equal(dates[2], series.MarketDate);
        Assert.Equal(3, series.Bars.Count);
        var identity = new StockIdentity(Stock, "SYN", "Synthetic", "UNKNOWN", true);
        var fields = TechnicalFeatures.Project(series.Bars, new Dictionary<DateOnly, DailyBar>(), false, null, null, null,
            TestContext.Current.CancellationToken);
        var report = TechnicalFeatures.Report(identity, dates[2], Cutoff, false, series, fields);
        Assert.Equal(("SYN", dates[2], 3, false), (report.Symbol, report.MarketDate, report.Sessions, report.Index));
        Assert.Equal(TechnicalFeatures.Names.Count, report.Features.Count);
        Assert.Equal(50, report.Features["ema50"].MinimumSessions);
        Assert.Equal("WARMUP", report.Features["ema50"].Availability);
        Assert.Null(report.Features["ema50"].Value);
        Assert.Equal("INSUFFICIENT_SESSIONS", report.Features["ema50"].Reason);
        Assert.Equal("AVAILABLE", report.Features["changePercent"].Availability);
    }

    private static SessionProof Proof(DateOnly date, ExchangeDayStatus status = ExchangeDayStatus.ObservedTrading) =>
        new(date, status, "https://independent.example/dated-evidence", Known);

    private static Dictionary<DateOnly, DailyBar> Calendar(params DateOnly[] dates) =>
        dates.ToDictionary(date => date, date => Bar(Stock, date));

    [Fact]
    public void SeriesKeepsTheTrailingRunAndAnUnprovenWeekdayEndsIt()
    {
        var dates = Dates(22); // 2026-08-24 .. 2026-09-22, weekdays only
        var calendar = Calendar(dates[0], dates[1], dates[2], dates[21]);
        var proofs = new[] { Proof(dates[0]), Proof(dates[1]), Proof(dates[2]), Proof(dates[21]) };
        var series = TechnicalFeatures.Series(proofs, calendar, dates[21], Cutoff,
            TestContext.Current.CancellationToken);
        // Every weekday between the two clusters is unproven, so only the newest run survives.
        Assert.Equal(dates[21], series.MarketDate);
        Assert.Single(series.Bars);
        Assert.Equal(dates[21], series.Bars[0].SessionDate);
    }

    [Fact]
    public void SeriesTreatsACitedClosureAsContinuousAndAWeekendAsNotStale()
    {
        var dates = Dates(5); // Mon..Fri
        var calendar = Calendar([.. dates]);
        var proofs = new[] { Proof(dates[0]), Proof(dates[1], ExchangeDayStatus.AnnouncedClosed), Proof(dates[2]),
            Proof(dates[3]), Proof(dates[4]) };
        var series = TechnicalFeatures.Series(proofs, calendar, dates[4].AddDays(2), Cutoff,
            TestContext.Current.CancellationToken); // Sunday
        Assert.Equal(dates[4], series.MarketDate);
        Assert.Equal([dates[0], dates[2], dates[3], dates[4]], series.Bars.Select(bar => bar.SessionDate));
    }

    [Fact]
    public void SeriesDoesNotUseASessionThatWasNotKnownAtTheCutoff()
    {
        var dates = Dates(2);
        var calendar = Calendar(dates[0], dates[1]);
        var proofs = new[] { Proof(dates[0]), Proof(dates[1]) };
        // The proof is known on 2026-09-28; an earlier cutoff cannot confirm either session.
        var series = TechnicalFeatures.Series(proofs, calendar, dates[1],
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);
        Assert.Null(series.MarketDate);
        Assert.Empty(series.Bars);
    }
}
