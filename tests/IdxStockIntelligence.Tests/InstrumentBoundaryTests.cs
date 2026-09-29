using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class InstrumentBoundaryTests
{
    private static readonly InstrumentId Id=InstrumentId.New();
    private static readonly DateTimeOffset Learned=new(2026,9,29,0,0,0,TimeSpan.Zero);
    private static readonly DateOnly Listing=new(2024,2,5);
    private static InstrumentBoundaryEvidence Evidence(string symbol="ARBITRARY") => new(Id.Value,symbol,"Synthetic issuer",
        Listing,new DateOnly(2025,2,5),Listing.AddDays(1),"VERIFIED","synthetic reference","https://reference.example/listing",
        "Synthetic dated listing notice",Learned,Learned,"SYNTHETIC","fixture-1","Not real market evidence.");

    [Fact]
    public void BoundariesComposeWithSessionsWithoutDependingOnTicker()
    {
        foreach (var symbol in new[] { "ARBITRARY", "ANOTHER" })
        {
            var instrument=InstrumentBoundaries.Resolve(new(Id,"Synthetic",null),[Evidence(symbol)],Learned);
            Assert.Equal(InstrumentBoundaryState.PreListing,InstrumentBoundaries.Classify(instrument,Listing.AddDays(-1)));
            Assert.Equal(DataAvailabilityStatus.PreListing,AvailabilityClassifier.Classify(instrument,new(Listing.AddDays(-1),MarketSessionStatus.Trading),false));
            Assert.Equal(InstrumentBoundaryState.ExpectedSession,InstrumentBoundaries.Classify(instrument,Listing));
            Assert.Equal(DataAvailabilityStatus.MissingProviderRow,AvailabilityClassifier.Classify(instrument,new(Listing,MarketSessionStatus.Trading),false));
            Assert.Equal(DataAvailabilityStatus.Holiday,AvailabilityClassifier.Classify(instrument,new(Listing,MarketSessionStatus.Holiday),false));
            var after=new DateOnly(2025,2,6);
            Assert.Equal(InstrumentBoundaryState.PostDelisting,InstrumentBoundaries.Classify(instrument,after));
            Assert.Equal("POST_DELISTING",PilotValidation.Validate(instrument,after,null,null,Learned).Status);
            Assert.Equal(InstrumentBoundaryState.ExpectedSession,InstrumentBoundaries.Classify(instrument,after.AddDays(-1)));
            Assert.Equal("SESSION_UNCONFIRMED",PilotValidation.Validate(instrument,Listing,null,null,Learned).Status);
        }
    }

    [Fact]
    public void UnknownAndPartialNeverSupplyAnExpectedMissingBoundary()
    {
        var fallback=new Instrument(Id,"Synthetic",null);
        foreach (var status in new[] { "UNKNOWN","PARTIAL" })
        {
            var instrument=InstrumentBoundaries.Resolve(fallback,[Evidence() with { Status=status }],Learned);
            Assert.Equal(InstrumentBoundaryState.UnknownBoundary,InstrumentBoundaries.Classify(instrument,Listing));
            Assert.Equal(DataAvailabilityStatus.Unknown,AvailabilityClassifier.Classify(instrument,new(Listing,MarketSessionStatus.Trading),false));
        }
        Assert.Equal(InstrumentBoundaryState.UnknownBoundary,InstrumentBoundaries.Classify(
            InstrumentBoundaries.Resolve(fallback,[Evidence()],Learned.AddTicks(-1)),Listing));
    }

    [Fact]
    public void CorrectionsAreVisibleOnlyAfterActualKnowledgeAndConflictIsExplicit()
    {
        var original=Evidence();
        var correction=original with { ListedFrom=Listing.AddDays(-1),FirstTradingDate=Listing.AddDays(1),
            RetrievedAt=Learned.AddDays(1),KnownAt=Learned.AddDays(1),SourceVersion="fixture-2" };
        var history=new[] { original,correction };
        var fallback=new Instrument(Id,"Synthetic",null);
        Assert.Equal(InstrumentBoundaryState.PreListing,InstrumentBoundaries.Classify(
            InstrumentBoundaries.Resolve(fallback,history,Learned),Listing.AddDays(-1)));
        Assert.Equal(InstrumentBoundaryState.ExpectedSession,InstrumentBoundaries.Classify(
            InstrumentBoundaries.Resolve(fallback,history,Learned.AddDays(1)),Listing.AddDays(-1)));
        Assert.Equal(original,InstrumentBoundaries.AsOf(history,Id,Learned));
        Assert.Throws<ArgumentException>(() => InstrumentBoundaries.AsOf(
            [original,original with { ListedFrom=Listing.AddDays(-2) }],Id,Learned));
        Assert.Null(InstrumentBoundaries.Resolve(fallback,[original,correction with { Status="PARTIAL",Notes="Conflicting references retained." }],
            Learned.AddDays(1)).ListedOn);
    }

    [Fact]
    public void InvalidChronologyAndInventedPrecisionAreRejected()
    {
        Assert.Throws<ArgumentException>(() => InstrumentBoundaries.Validate(Evidence() with { RetrievedAt=Learned.AddSeconds(1) }));
        Assert.Throws<ArgumentException>(() => InstrumentBoundaries.Validate(Evidence() with { DelistedAt=Listing.AddDays(-1) }));
        Assert.Throws<ArgumentException>(() => InstrumentBoundaries.Validate(Evidence() with { FirstTradingDate=Listing.AddDays(-1) }));
        Assert.Throws<ArgumentException>(() => InstrumentBoundaries.Validate(Evidence() with { ListedFrom=null,DelistedAt=null }));
        Assert.Throws<ArgumentException>(() => InstrumentBoundaries.Validate(Evidence() with { Reference="AI answer" }));
    }
}
