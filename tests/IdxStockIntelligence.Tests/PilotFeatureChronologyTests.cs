using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class PilotFeatureChronologyTests
{
    private static readonly DateTimeOffset Known = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);
    private static readonly InstrumentId Stock = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly InstrumentId Index = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly string[] LegacyReportFields = ["MarketDate", "Status", "ConsecutiveSessions", "Ema20",
        "Ema50", "Atr14", "PriorHigh20", "PriorLow20", "VolumeRatio20", "RelativePerformance20"];
    private static DailyBarRevision Revision(InstrumentId id, DateOnly date, decimal close, long volume = 1000,
        long revision = 1, DateTimeOffset? known = null, DateTimeOffset? available = null)
    {
        var sourceAt = available ?? Known;
        var bar = new DailyBar(id, date, close, close + 1, close - 1, close, volume,
            new("fixture", id.Value, sourceAt, sourceAt, new('f', 64)), close / 2);
        return new(revision, bar, known ?? Known, PilotValidation.ContentHash(bar), id.Value);
    }

    private static (DailyBarRevision[] Bars, SessionProof[] Proofs) Series(int count,
        decimal stockEnd = 110, decimal indexEnd = 105, long indexVolume = 1000)
    {
        var start = new DateOnly(2026, 1, 5);
        var days = Enumerable.Range(0, count * 2).Select(start.AddDays)
            .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Take(count).ToArray();
        var bars = days.SelectMany((date, i) => new[] {
            Revision(Stock, date, 100m + (stockEnd - 100m) * i / (count - 1)),
            Revision(Index, date, 100m + (indexEnd - 100m) * i / (count - 1), indexVolume)
        }).ToArray();
        var proofs = days.Select(d => new SessionProof(d, ExchangeDayStatus.ObservedTrading,
            "https://independent.example/fixture", Known)).ToArray();
        return (bars, proofs);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FutureBarsAndProofsCannotChangeAnyThroughBoundedFeature(bool futureBars, bool futureProofs)
    {
        var (bars, proofs) = Series(75);
        var through = proofs[60].Date;
        var historicalBars = bars.Where(r => r.Bar.SessionDate <= through).ToArray();
        var historicalProofs = proofs.Take(61).ToArray();
        var expected = PilotFeatures.Calculate(historicalBars, Stock, Index, historicalProofs, through, Known);
        var actual = PilotFeatures.Calculate(futureBars ? bars : historicalBars, Stock, Index,
            futureProofs ? proofs : historicalProofs, through, Known);

        Assert.NotNull(expected.Ema20);
        Assert.NotNull(expected.Ema50);
        Assert.NotNull(expected.Atr14);
        Assert.NotNull(expected.Rs20Pp);
        Assert.NotNull(expected.Rs60Pp);
        Assert.Equal(expected, actual);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(actual, PilotFeatures.Calculate(bars, Stock, Index, proofs, through, Known));
    }

    [Fact]
    public void FutureOnlyBarsCannotSupplyHistoricalMarketDateOrPreventMissingState()
    {
        var (bars, proofs) = Series(21);
        var through = proofs[^1].Date;
        var expected = PilotFeatures.Calculate([], Stock, Index, proofs, through, Known);
        var future = new[] { Revision(Stock, through.AddDays(1), 999), Revision(Index, through.AddDays(1), 999) };
        Assert.Equal(expected, PilotFeatures.Calculate(future, Stock, Index, proofs, through, Known));
        Assert.Null(expected.MarketDate);
        Assert.Equal("MISSING", expected.Status);
        Assert.Equal(Availability.UNAVAILABLE, expected.Rs20PpState.Availability);
        Assert.Null(expected.Rs20Pp);
        Assert.Equal("MISSING", expected.Rs20PpState.UnavailableReason);
    }

    [Fact]
    public void ThroughFiltersFutureEvidenceBeforeCalendarResolution()
    {
        var (bars, proofs) = Series(21);
        var through = proofs[^1].Date;
        var expected = PilotFeatures.Calculate(bars, Stock, Index, proofs, through, Known);
        var future = proofs[^1] with { Date = through.AddDays(1) };
        var excess = proofs.Concat([future, future with { Status = ExchangeDayStatus.AnnouncedClosed }]).ToArray();
        Assert.Equal(expected, PilotFeatures.Calculate(bars, Stock, Index, excess, through, Known));
    }

    [Fact]
    public void Rs20IsFivePercentagePointsWhileLegacyRetainsItsMultiplicativeRatioAndReportShape()
    {
        var (bars, proofs) = Series(21);
        var result = PilotFeatures.Calculate(bars, Stock, Index, proofs, proofs[^1].Date, Known);
        Assert.Equal(5m, result.Rs20Pp);
        Assert.Equal(new FeatureState(Availability.AVAILABLE, 5m, null), result.Rs20PpState);
        Assert.Equal(1.1m / 1.05m - 1m, result.RelativePerformance20);
        Assert.InRange(result.RelativePerformance20!.Value, 0.0476190476190476190476190475m, 0.0476190476190476190476190477m);
        Assert.NotEqual(result.RelativePerformance20 * 100m, result.Rs20Pp);
        using var report = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.Equal(LegacyReportFields,
            report.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(result.RelativePerformance20, report.RootElement.GetProperty("RelativePerformance20").GetDecimal());
    }

    [Fact]
    public void Rs60IsTenPercentagePointsAcrossSixtyExchangeSessionsNotCalendarDays()
    {
        var (bars, proofs) = Series(61, 120, 110);
        var result = PilotFeatures.Calculate(bars, Stock, Index, proofs, proofs[^1].Date, Known);
        Assert.True(proofs[^1].Date.DayNumber - proofs[0].Date.DayNumber > 60);
        Assert.Equal(10m, result.Rs60Pp);
        Assert.Equal(new FeatureState(Availability.AVAILABLE, 10m, null), result.Rs60PpState);
    }

    [Theory]
    [InlineData(20, Availability.WARMUP, Availability.WARMUP)]
    [InlineData(21, Availability.AVAILABLE, Availability.WARMUP)]
    [InlineData(60, Availability.AVAILABLE, Availability.WARMUP)]
    [InlineData(61, Availability.AVAILABLE, Availability.AVAILABLE)]
    public void EachRsHorizonHasItsOwnExactWarmup(int count, Availability rs20, Availability rs60)
    {
        var (bars, proofs) = Series(count);
        var result = PilotFeatures.Calculate(bars, Stock, Index, proofs, proofs[^1].Date, Known);
        foreach (var (state, expected) in new[] { (result.Rs20PpState, rs20), (result.Rs60PpState, rs60) })
        {
            Assert.Equal(expected, state.Availability);
            if (expected == Availability.WARMUP)
            {
                Assert.Null(state.Value);
                Assert.Equal("INSUFFICIENT_SESSIONS", state.UnavailableReason);
            }
            else Assert.NotNull(state.Value);
        }
    }

    [Theory]
    [InlineData(10, Availability.AVAILABLE)]
    [InlineData(40, Availability.UNAVAILABLE)]
    [InlineData(50, Availability.UNAVAILABLE)]
    [InlineData(60, Availability.UNAVAILABLE)]
    public void MissingBenchmarkDateBlocksOnlyAffectedAlignedIntervals(int missingIndex, Availability rs20)
    {
        var (bars, proofs) = Series(61);
        var missing = proofs[missingIndex].Date;
        var unaligned = bars.Where(r => r.Bar.InstrumentId != Index || r.Bar.SessionDate != missing).ToArray();
        var result = PilotFeatures.Calculate(unaligned, Stock, Index, proofs, proofs[^1].Date, Known);
        Assert.Equal(rs20, result.Rs20PpState.Availability);
        Assert.Equal(new FeatureState(Availability.UNAVAILABLE, null, "BENCHMARK_MISSING"), result.Rs60PpState);
        if (rs20 == Availability.UNAVAILABLE) Assert.Null(result.Rs20Pp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrDateShiftedIhsgCannotBeNearestDateSubstituted(bool shifted)
    {
        var (bars, proofs) = Series(61);
        var unaligned = bars.Where(r => r.Bar.InstrumentId == Stock).ToList();
        if (shifted)
            unaligned.AddRange(bars.Where(r => r.Bar.InstrumentId == Index)
                .Select(r => Revision(Index, r.Bar.SessionDate.AddDays(1), r.Bar.Close)));
        var result = PilotFeatures.Calculate(unaligned, Stock, Index, proofs, proofs[^1].Date, Known);
        Assert.Equal(new FeatureState(Availability.UNAVAILABLE, null, "BENCHMARK_MISSING"), result.Rs20PpState);
        Assert.Equal(new FeatureState(Availability.UNAVAILABLE, null, "BENCHMARK_MISSING"), result.Rs60PpState);
    }

    [Theory]
    [InlineData("bar")]
    [InlineData("proof")]
    [InlineData("zero-volume")]
    public void StockContinuityBreakCannotBeSkippedToManufactureSixtySessionReturn(string gap)
    {
        var (bars, proofs) = Series(61);
        var missing = proofs[25].Date;
        if (gap != "proof")
            bars = bars.Where(r => r.Bar.InstrumentId != Stock || r.Bar.SessionDate != missing).ToArray();
        if (gap == "zero-volume") bars = [.. bars, Revision(Stock, missing, 100, 0)];
        if (gap == "proof") proofs = proofs.Where(p => p.Date != missing).ToArray();
        var result = PilotFeatures.Calculate(bars, Stock, Index, proofs, proofs[^1].Date, Known);
        Assert.Equal(35, result.ConsecutiveSessions);
        Assert.Equal(Availability.AVAILABLE, result.Rs20PpState.Availability);
        Assert.Equal(new FeatureState(Availability.WARMUP, null, "INSUFFICIENT_SESSIONS"), result.Rs60PpState);
    }

    [Fact]
    public void IndexVolumeDoesNotGateNewPriceReturnsButLegacyVolumeRuleIsPreserved()
    {
        var (bars, proofs) = Series(21, indexVolume: 0);
        var result = PilotFeatures.Calculate(bars, Stock, Index, proofs, proofs[^1].Date, Known);
        Assert.Equal(5m, result.Rs20Pp);
        Assert.Equal(Availability.AVAILABLE, result.Rs20PpState.Availability);
        Assert.Null(result.RelativePerformance20);
    }

    [Theory]
    [InlineData(21)]
    [InlineData(61)]
    public void OptionalRsOverflowDoesNotBreakLegacyFeatures(int count)
    {
        var (bars, proofs) = Series(count);
        var first = bars.First(r => r.Bar.InstrumentId == Stock);
        const decimal tiny = 0.0000000000000000000000001m;
        var bar = new DailyBar(Stock, first.Bar.SessionDate, tiny, 1m, tiny, tiny, 1000, first.Bar.Source);
        bars = [.. bars.Where(r => r != first), first with { Bar = bar, ContentSha256 = PilotValidation.ContentHash(bar) }];
        var result = PilotFeatures.Calculate(bars, Stock, Index, proofs, proofs[^1].Date, Known);
        Assert.NotNull(result.Ema20);
        Assert.NotNull(result.Atr14);
        Assert.NotNull(result.RelativePerformance20);
        Assert.Equal(new FeatureState(Availability.UNAVAILABLE, null, "NUMERIC_OUT_OF_RANGE"),
            count == 21 ? result.Rs20PpState : result.Rs60PpState);
    }

    [Fact]
    public void LateStockAndBenchmarkRevisionsAndFutureKnownProofsCannotLeakPastCutoff()
    {
        var (bars, proofs) = Series(21);
        var through = proofs[^1].Date;
        var later = Known.AddDays(1);
        var expected = PilotFeatures.Calculate(bars, Stock, Index, proofs, through, Known);
        DailyBarRevision[] corrected = [.. bars,
            Revision(Stock, through, 200, revision: 2, known: later, available: later),
            Revision(Index, through, 150, revision: 2, known: later, available: later)];
        var excessProofs = proofs.Append(proofs[5] with { KnownAt = later, Status = ExchangeDayStatus.AnnouncedClosed }).ToArray();
        Assert.Equal(expected, PilotFeatures.Calculate(corrected, Stock, Index, excessProofs, through, Known));
        Assert.Equal(50m, PilotFeatures.Calculate(corrected, Stock, Index, proofs, through, later).Rs20Pp);
        // Even inconsistent oversized input cannot bypass the source-availability boundary.
        var notAvailable = corrected.Select(r => r.RevisionNumber == 2 ? r with { KnownAt = Known } : r).ToArray();
        Assert.Equal(expected, PilotFeatures.Calculate(notAvailable, Stock, Index, proofs, through, Known));
    }
}
