using Xunit;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Tests;

public sealed class PilotTests
{
    private static readonly DateTimeOffset Known = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);
    private static readonly InstrumentId Stock = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly InstrumentId Index = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static DailyBar Bar(InstrumentId id, DateOnly date, decimal close = 100m, long volume = 1000) =>
        new(id,date,close,close+1,close-1,close,volume,
            new("fixture",Guid.NewGuid(),Known,Known,new('f',64)),close/2,"SHARE_COUNT_CORROBORATED","SPLIT_ADJUSTED","UNKNOWN");
    private static SessionProof Proof(DateOnly date, ExchangeDayStatus status = ExchangeDayStatus.ObservedTrading) =>
        new(date,status,"https://independent.example/dated-evidence",Known);

    [Fact]
    public void SameContentThenCorrectionThenReversionAreAppendOnlyAndAsOfSafe()
    {
        var date = new DateOnly(2026,9,25);
        var original = Bar(Stock,date);
        var changed = Bar(Stock,date,101);
        var store = new DailyBarRevisionStore();
        var first = store.Ingest(original,Known,PilotValidation.ContentHash(original),Guid.NewGuid());
        Assert.Equal(IngestionDisposition.DuplicateIgnored,store.Ingest(original,Known.AddHours(1),PilotValidation.ContentHash(original),Guid.NewGuid()).Disposition);
        store.Ingest(changed,Known.AddHours(2),PilotValidation.ContentHash(changed),Guid.NewGuid());
        Assert.Equal(IngestionDisposition.RevisionAppended,store.Ingest(original,Known.AddHours(3),PilotValidation.ContentHash(original),Guid.NewGuid()).Disposition);
        Assert.Equal(3,store.History(Stock,date).Count);
        Assert.Equal(first.Revision,store.AsOf(Stock,date,Known.AddHours(1)));
        Assert.Equal(101m,store.AsOf(Stock,date,Known.AddHours(2))!.Bar.Close);
        Assert.Equal(100m,store.AsOf(Stock,date,Known.AddHours(3))!.Bar.Close);
        Assert.Throws<ArgumentException>(() => store.Ingest(changed,Known.AddHours(2),PilotValidation.ContentHash(changed),Guid.NewGuid()));
        Assert.Equal(50m,original.AdjustedClose);
        Assert.NotEqual(original.Close,original.AdjustedClose);
    }

    [Fact]
    public void ClosedUnknownFutureMissingZeroAndPreListingNeverCreateBars()
    {
        var date = new DateOnly(2026,8,25);
        var instrument = new Instrument(Stock,"Fixture",new DateOnly(2020,1,1));
        var padding = Bar(Stock,date,100,0);
        Assert.Equal("CLOSED",PilotValidation.Validate(instrument,date,padding,Proof(date,ExchangeDayStatus.AnnouncedClosed),Known).Status);
        Assert.Null(PilotValidation.Validate(instrument,date,padding,Proof(date,ExchangeDayStatus.AnnouncedClosed),Known).Bar);
        Assert.Equal("SESSION_UNCONFIRMED",PilotValidation.Validate(instrument,date,padding,null,Known).Status);
        Assert.Equal("SESSION_UNCONFIRMED",PilotValidation.Validate(instrument,date,padding,Proof(date) with { KnownAt=Known.AddDays(1) },Known).Status);
        Assert.Equal("MISSING",PilotValidation.Validate(instrument,date,null,Proof(date),Known).Status);
        Assert.Equal("SOURCE_ERROR",PilotValidation.Validate(instrument,date,null,Proof(date),Known,true).Status);
        Assert.Equal("UNKNOWN",PilotValidation.Validate(instrument,date,padding,Proof(date),Known).Status);
        var unknownListing = new Instrument(Stock,"Fixture",null);
        Assert.Equal("UNKNOWN",PilotValidation.Validate(unknownListing,date,null,Proof(date),Known).Status);
        var futureListing = new Instrument(Stock,"Fixture",date.AddDays(1));
        Assert.Equal("PRE_LISTING",PilotValidation.Validate(futureListing,date,null,Proof(date),Known).Status);
    }

    private static (List<DailyBarRevision> Bars,List<SessionProof> Proofs) Series()
    {
        var bars = new List<DailyBarRevision>();
        var proofs = new List<SessionProof>();
        var date = new DateOnly(2026,1,5);
        while (proofs.Count < 60)
        {
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                proofs.Add(Proof(date));
                foreach (var id in new[] { Stock,Index })
                {
                    var bar = Bar(id,date);
                    bars.Add(new(1,bar,Known,PilotValidation.ContentHash(bar),Guid.NewGuid()));
                }
            }
            date = date.AddDays(1);
        }
        return (bars,proofs);
    }

    [Fact]
    public void FeaturesUseValidSessionsAndExplicitWarmupWithIndependentGoldenValues()
    {
        var (bars,proofs) = Series();
        var holiday = proofs[20];
        proofs[20] = holiday with { Status=ExchangeDayStatus.AnnouncedClosed };
        // Even a huge padded provider bar on the holiday cannot change a feature.
        bars.RemoveAll(r => r.Bar.SessionDate == holiday.Date);
        var padded = Bar(Stock,holiday.Date,9000);
        bars.Add(new(1,padded,Known,PilotValidation.ContentHash(padded),Guid.NewGuid()));
        var result = PilotFeatures.Calculate(bars,Stock,Index,proofs,Known);
        Assert.Equal(59,result.ConsecutiveSessions);
        Assert.Equal("AVAILABLE_PILOT",result.Status);
        Assert.Equal(100m,result.Ema20);
        Assert.Equal(100m,result.Ema50);
        Assert.Equal(2m,result.Atr14);
        Assert.Equal(101m,result.PriorHigh20);
        Assert.Equal(99m,result.PriorLow20);
        Assert.Equal(1m,result.VolumeRatio20);
        Assert.Equal(0m,result.RelativePerformance20);
        var warmup = PilotFeatures.Calculate(bars,Stock,Index,proofs.Take(10).ToArray(),Known);
        Assert.Null(warmup.Ema20);
        Assert.Null(warmup.Atr14);
    }

    [Fact]
    public void MissingUnknownOrFutureSessionBreaksContinuityAndBenchmarkMustAlign()
    {
        var (bars,proofs) = Series();
        bars.RemoveAll(r => r.Bar.InstrumentId == Index && r.Bar.SessionDate == proofs[^10].Date);
        Assert.Null(PilotFeatures.Calculate(bars,Stock,Index,proofs,Known).RelativePerformance20);
        proofs.RemoveAt(50);
        var result = PilotFeatures.Calculate(bars,Stock,Index,proofs,Known);
        Assert.Equal(9,result.ConsecutiveSessions);
        Assert.Null(result.Ema20);
        Assert.Null(result.RelativePerformance20);
        Assert.Equal("SESSION_UNCONFIRMED",PilotFeatures.Calculate(bars,Stock,Index,proofs,Known.AddSeconds(-1)).Status);
        bars.RemoveAll(r => r.Bar.InstrumentId == Stock && r.Bar.SessionDate == proofs[^1].Date);
        Assert.Equal("STALE",PilotFeatures.Calculate(bars,Stock,Index,proofs,Known).Status);
    }

    [Fact]
    public void PriorWindowsExcludeCurrentBarAndVolumeBasisChangesDisableRatio()
    {
        var (bars,proofs) = Series();
        var latest = Bar(Stock,proofs[^1].Date,200,2000);
        bars.Add(new(2,latest,Known,PilotValidation.ContentHash(latest),Guid.NewGuid()));
        var result = PilotFeatures.Calculate(bars,Stock,Index,proofs,Known);
        Assert.Equal(101m,result.PriorHigh20);
        Assert.Equal(99m,result.PriorLow20);
        Assert.Equal(2m,result.VolumeRatio20);
        Assert.Equal(1m,result.RelativePerformance20);
        Assert.Equal(100m + (2m / 21m) * 100m,result.Ema20);
        Assert.Equal(100m + (2m / 51m) * 100m,result.Ema50);
        Assert.Equal(2m + (1m / 14m) * 99m,result.Atr14);
        var changed = new DailyBar(Stock,latest.SessionDate,200,201,199,200,2000,latest.Source,100,"UNKNOWN","UNKNOWN","UNKNOWN");
        bars.Add(new(3,changed,Known,PilotValidation.ContentHash(changed),Guid.NewGuid()));
        Assert.Null(PilotFeatures.Calculate(bars,Stock,Index,proofs,Known).VolumeRatio20);
    }
}
