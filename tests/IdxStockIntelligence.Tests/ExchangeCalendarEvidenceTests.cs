using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ExchangeCalendarEvidenceTests
{
    [Fact]
    public void ExplicitTradingHolidayWeekendAndUnknownStaySeparate()
    {
        var calendar = new ExchangeCalendarEvidence();
        calendar.Record(new ExchangeDayEvidence(
            new DateOnly(2026, 1, 2), ExchangeDayStatus.ObservedTrading, "fixture:trading-day"));
        calendar.Record(new ExchangeDayEvidence(
            new DateOnly(2026, 1, 1), ExchangeDayStatus.AnnouncedClosed, "fixture:holiday-notice"));

        Assert.Equal(ExchangeDayStatus.AnnouncedClosed, calendar.StatusOn(new DateOnly(2026, 1, 1)));
        Assert.Equal(ExchangeDayStatus.ObservedTrading, calendar.StatusOn(new DateOnly(2026, 1, 2)));
        Assert.Equal(ExchangeDayStatus.Weekend, calendar.StatusOn(new DateOnly(2026, 1, 3)));
        Assert.Equal(ExchangeDayStatus.Unknown, calendar.StatusOn(new DateOnly(2026, 1, 5)));
    }

    [Fact]
    public void DuplicateEvidenceIsIdempotentAndResultsAreChronological()
    {
        var calendar = new ExchangeCalendarEvidence();
        var later = new ExchangeDayEvidence(
            new DateOnly(2026, 1, 2), ExchangeDayStatus.ObservedTrading, "fixture:trading-day");
        var earlier = new ExchangeDayEvidence(
            new DateOnly(2026, 1, 1), ExchangeDayStatus.AnnouncedClosed, "fixture:holiday-notice");

        Assert.True(calendar.Record(later));
        Assert.True(calendar.Record(earlier));
        Assert.False(calendar.Record(later));
        Assert.Equal([earlier, later], calendar.Chronological());
    }

    [Fact]
    public void ConflictingEvidenceRequiresRevisionPolicy()
    {
        var calendar = new ExchangeCalendarEvidence();
        var date = new DateOnly(2026, 1, 2);
        calendar.Record(new ExchangeDayEvidence(date, ExchangeDayStatus.ObservedTrading, "fixture:a"));

        Assert.Throws<InvalidOperationException>(() => calendar.Record(
            new ExchangeDayEvidence(date, ExchangeDayStatus.AnnouncedClosed, "fixture:b")));
    }
}
