using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class OutcomeTrackingTests
{
    private static readonly Guid Stock = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid Index = Guid.Parse("76237e96-232f-5085-9b13-dcb7104222bc");
    private static readonly DateOnly Base = new(2026, 9, 4); // Friday
    private static readonly DateTimeOffset Capture = new(2026, 9, 4, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 10, 10, 13, 0, 0, TimeSpan.Zero);
    private static SessionProof Proof(DateOnly date, DateTimeOffset? at = null, ExchangeDayStatus status = ExchangeDayStatus.ObservedTrading)
        => new(date, status, "https://reference.example/session", at ?? Later.AddMinutes(-1), date == Base ? Capture.AddMinutes(-2) : null);
    private static ScreenerBarEvidence Bar(DateOnly day, decimal close = 100, string source = "synthetic")
    {
        var at = day == Base ? Capture.AddMinutes(-1) : Later.AddMinutes(-1);
        var daily = new DailyBar(new(Stock), day, close, close, close, close, 100,
            new(source, Stock, at, at, new('a', 64)), null, "SHARES", "RAW_AS_TRADED", "REGULAR");
        return new(Stock, day, 1, at, PilotValidation.ContentHash(daily), Stock, Stock, source, new('a', 64), at, at,
            "https://reference.example/session", at, "VALID", close.ToString(System.Globalization.CultureInfo.InvariantCulture),
            close.ToString(System.Globalization.CultureInfo.InvariantCulture), close.ToString(System.Globalization.CultureInfo.InvariantCulture),
            close.ToString(System.Globalization.CultureInfo.InvariantCulture), 100, null, "SHARES", "RAW_AS_TRADED", "REGULAR");
    }
    private static InstrumentSnapshot Reference(DateTimeOffset at, params ScreenerBarEvidence[] bars)
    {
        var source = new ScreenerSourceEvidence("proof", "synthetic", "https://reference.example/clearance", null, at, at);
        var r = new InstrumentSnapshot("ref-" + at.ToUnixTimeSeconds(), Stock, at, [source], "",
            [new(ScreenerReadRequest.Anchor, null, "SYN", "Synthetic", "ORDINARY", "IDR", "MAIN", ["proof"])],
            [new(ScreenerReadRequest.Anchor, ScreenerReadRequest.Horizon, "TRADING", "CONTINUOUS", ["proof"])],
            [new(ScreenerReadRequest.Anchor, ScreenerReadRequest.Horizon, "synthetic", "STOCK_RAW", "RAW_AS_TRADED", "CLEARED", bars.Select(b => b.ContentHash).ToArray(), ["proof"])], []);
        return Seal(r);
    }
    private static InstrumentSnapshot Seal(InstrumentSnapshot r) => r with { ContentHash = ScreenerReferences.SnapshotHash(r) };
    internal static (DecisionSnapshotHeader H, DecisionSnapshotRow Row, OutcomeInputs Inputs) Fixture(decimal end = 110, decimal anchorClose = 100)
    {
        var start = Bar(Base, anchorClose); var endpoint = Bar(Base.AddDays(3), end);
        var old = Reference(Capture.AddMinutes(-1), start); var future = Reference(Later.AddMinutes(-1), start, endpoint);
        var u = new UniverseSnapshot("pilot", Capture.AddMinutes(-1), "PILOT", [Stock], Index, old.Evidence, "");
        u = u with { ContentHash = ScreenerReferences.SnapshotHash(u) };
        var captured = new SelectedScreenerReferences([u], [old], [Proof(Base, Capture.AddMinutes(-1))], []);
        var db = new ScreenerDatabaseEvidence([start], []);
        var request = new ScreenerReadRequest([Stock], Index, ScreenerReadRequest.Anchor, Base, Capture);
        var evaluated = ScreenerService.Evaluate(request, db, captured, null, default);
        var row = DecisionSnapshotProjection.Row(ScreenerPresentation.Row(evaluated.Result.Rows.Single(), captured, Capture), false, null, null);
        var h = new DecisionSnapshotHeader(Guid.NewGuid(), Guid.NewGuid(), 1, "PROSPECTIVE_CAPTURE", Capture, Capture, Capture,
            Base, Base, ScreenerReadRequest.Anchor, ScreenerReadRequest.PolicyId, "PILOT", "pilot", null, evaluated.InputHash, evaluated.SelectedDigest, "BLOCKED", 1);
        return (h, row, new(db, captured, new([endpoint], []), new([], [future], [Proof(endpoint.SessionDate)], [])));
    }
    private static OutcomeCell Evaluate((DecisionSnapshotHeader H, DecisionSnapshotRow Row, OutcomeInputs Inputs) f,
        OutcomeInputs? inputs = null, DecisionSnapshotRow? row = null)
    {
        var i = inputs ?? f.Inputs;
        return OutcomeEvaluator.Evaluate(f.H, row ?? f.Row, 1, OutcomeEvaluator.ResolveHorizon(f.H, 1, i.ForwardReferences.Sessions, Later), i, Later);
    }

    [Theory]
    [InlineData(1, 3)][InlineData(5, 7)][InlineData(10, 14)][InlineData(20, 28)]
    public void AnchorExcludedAndExactExchangeOrdinalAcrossWeekends(int horizon, int civilDays)
    {
        var f = Fixture();
        var proofs = Enumerable.Range(0, 35).Select(n => Base.AddDays(n)).Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Select(d => Proof(d)).ToArray();
        Assert.Equal(Base.AddDays(civilDays), OutcomeEvaluator.ResolveHorizon(f.H, horizon, proofs, Later, TestContext.Current.CancellationToken).Date);
    }
    [Fact]
    public void HolidaySkippedOnlyWithProofAndBarsCannotManufactureSessions()
    {
        var f = Fixture(); var monday = Base.AddDays(3);
        var missing = OutcomeEvaluator.ResolveHorizon(f.H, 1, [], Later, TestContext.Current.CancellationToken);
        Assert.Null(missing.Date); Assert.Equal("SESSION_UNAVAILABLE", missing.State);
        var known = OutcomeEvaluator.ResolveHorizon(f.H, 1, [Proof(monday, status: ExchangeDayStatus.AnnouncedClosed), Proof(monday.AddDays(1))], Later, TestContext.Current.CancellationToken);
        Assert.Equal(monday.AddDays(1), known.Date);
        Assert.False(OutcomeEvaluator.Evaluate(f.H, f.Row, 1, missing, f.Inputs, Later, TestContext.Current.CancellationToken).Terminal);
    }
    [Fact]
    public void FutureAndConflictingSessionProofsExcludedAndBeforeHorizonPending()
    {
        var f = Fixture();
        Assert.Equal("PENDING", OutcomeEvaluator.ResolveHorizon(f.H, 1, f.Inputs.ForwardReferences.Sessions, Capture.AddDays(1), TestContext.Current.CancellationToken).State);
        Assert.Null(OutcomeEvaluator.ResolveHorizon(f.H, 1, [Proof(Base.AddDays(3), Later.AddDays(1))], Later, TestContext.Current.CancellationToken).Date);
        Assert.Equal("SESSION_UNAVAILABLE", OutcomeEvaluator.ResolveHorizon(f.H, 1,
            [Proof(Base.AddDays(3)), Proof(Base.AddDays(3), status: ExchangeDayStatus.AnnouncedClosed)], Later, TestContext.Current.CancellationToken).State);
    }
    [Theory]
    [InlineData(100, 0)][InlineData(110, 10)][InlineData(90, -10)]
    public void ExactPriceReturnIncludingRealZeroAndNoBenchmarkDependency(int close, int expected)
    {
        var f = Fixture(close); var cell = Evaluate(f);
        Assert.Equal("AVAILABLE", cell.State); Assert.Equal(expected, cell.PriceReturnPct);
        Assert.Equal("TERMINAL", cell.Resolution); Assert.False(cell.Materialized); Assert.Null(cell.OutcomeKnownAt);
    }
    [Fact]
    public void LateBarAndBasisEvidenceRemainRetryableUntilActuallyProved()
    {
        var f = Fixture();
        var missingBar = f.Inputs with { ForwardDatabase = new([], []) };
        var first = Evaluate(f, missingBar);
        Assert.Equal("UNRESOLVED", first.State); Assert.Equal("HORIZON_BAR_MISSING", first.Reason);
        var noBasis = f.Inputs with { ForwardReferences = f.Inputs.ForwardReferences with
            { Instruments = [Seal(f.Inputs.ForwardReferences.Instruments[0] with { Prices = [] })] } };
        Assert.False(Evaluate(f, noBasis).Terminal);
        Assert.Equal("AVAILABLE", Evaluate(f).State);
    }
    [Theory]
    [InlineData("UNKNOWN")][InlineData("UNRESOLVED")]
    public void IncompleteActionCoverageNeverInventsTerminalCorporateAction(string coverage)
    {
        var f = Fixture(); var r = f.Inputs.ForwardReferences.Instruments[0];
        var i = f.Inputs with { ForwardReferences = f.Inputs.ForwardReferences with
            { Instruments = [Seal(r with { Prices = [r.Prices[0] with { EventCoverage = coverage }] })] } };
        Assert.Equal("UNRESOLVED", Evaluate(f, i).State);
    }
    [Theory]
    [InlineData("DATA_BLOCKED", "NONE", false, false)]
    [InlineData("ELIGIBLE", "NONE", true, false)]
    [InlineData("ELIGIBLE", "WATCH", true, false)]
    [InlineData("ELIGIBLE", "CONFIRMED", true, false)]
    [InlineData("INELIGIBLE", "FAILED", true, true)]
    public void AllPopulationStatesIncludingHeldOutsideAreIndependent(string eligibility, string setup, bool evaluated, bool held)
    {
        var f = Fixture();
        Assert.Equal("AVAILABLE", Evaluate(f, row: f.Row with { Eligibility = eligibility, Setup = setup,
            SetupEvaluated = evaluated, Held = held, Configured = !held }).State);
    }
    [Fact]
    public void CapturedBasisCannotBeRepairedWithLaterKnowledgeAndStaleAnchorNotCarried()
    {
        var f = Fixture();
        var invalid = f.Inputs with { CapturedReferences = f.Inputs.CapturedReferences with { Instruments = [] } };
        Assert.Equal("ANCHOR_UNAVAILABLE", Evaluate(f, invalid).State);
        Assert.Equal("ANCHOR_UNAVAILABLE", Evaluate(f, row: f.Row with { MarketDate = Base.AddDays(-1) }).State);
        Assert.Equal("ANCHOR_UNAVAILABLE", Evaluate(f, row: f.Row with { Close = null }).State);
    }
    [Theory]
    [InlineData("SUSPENSION", "SUSPENDED_AT_HORIZON")][InlineData("NO_TRADE", "NO_TRADE_AT_HORIZON")]
    public void PositiveEndpointStatusIsTerminalButUnknownAndZeroVolumeAreNot(string status, string reason)
    {
        var f = Fixture(); var r = f.Inputs.ForwardReferences.Instruments[0];
        var i = f.Inputs with { ForwardDatabase = new([], []), ForwardReferences = f.Inputs.ForwardReferences with
            { Instruments = [Seal(r with { Trading = [r.Trading[0] with { Status = status }] })] } };
        Assert.Equal(reason, Evaluate(f, i).Reason); Assert.Equal("DATA_UNAVAILABLE", Evaluate(f, i).State);
        Assert.False(Evaluate(f, f.Inputs with { ForwardDatabase = new([f.Inputs.ForwardDatabase.Bars[0] with { Volume = 0 }], []) }).Terminal);
        Assert.False(Evaluate(f, f.Inputs with { ForwardReferences = f.Inputs.ForwardReferences with { Instruments = [Seal(r with { Trading = [] })] } }).Terminal);
    }
    [Fact]
    public void PositivelyVerifiedSourceChangeIsTerminalUnsupportedComparability()
    {
        var f = Fixture(); var bar = Bar(Base.AddDays(3), 110, "other-source"); var r = f.Inputs.ForwardReferences.Instruments[0];
        r = Seal(r with { Prices = [r.Prices[0] with { SourceId = "other-source", ContentHashes = [bar.ContentHash] }] });
        var i = f.Inputs with { ForwardDatabase = new([bar], []), ForwardReferences = f.Inputs.ForwardReferences with { Instruments = [r] } };
        Assert.Equal("BASIS_UNCERTAIN", Evaluate(f, i).State);
    }
    [Fact]
    public void DecimalDivisionAndRepeatedCaptureRemainDistinct()
    {
        var f = Fixture(101); Assert.Equal(1m, Evaluate(f).PriceReturnPct);
        Assert.NotEqual(f.H.RunId, (f.H with { RunId = Guid.NewGuid() }).RunId);
        Assert.Equal(Evaluate(f), Evaluate(f));
    }
    [Fact]
    public void RepeatingDecimalIsRetainedWithoutTwoPlaceQuantization()
    {
        var cell = Evaluate(Fixture(10, 3));
        Assert.Equal("AVAILABLE", cell.State);
        Assert.Equal(233.33333333333333333333333333m, cell.PriceReturnPct);
    }
    [Fact]
    public void FutureEndpointKnowledgeAndConflictingStatusCannotFinalize()
    {
        var f = Fixture(); var b = f.Inputs.ForwardDatabase.Bars[0];
        Assert.False(Evaluate(f, f.Inputs with { ForwardDatabase = new([b with { RetrievedAt = Later.AddMinutes(1) }], []) }).Terminal);
        var proof = new InstrumentSessionProof(new(Stock), Base.AddDays(3), MarketSessionStatus.Suspension,
            "https://reference.example/status", Later.AddMinutes(-1));
        Assert.Equal("UNRESOLVED", Evaluate(f, f.Inputs with { ForwardReferences = f.Inputs.ForwardReferences with { InstrumentSessions = [proof] } }).State);
    }
    [Fact]
    public void PostDelistingRequiresVerifiedEconomicBoundaryAndDoesNotSlide()
    {
        var f = Fixture(); var date = Base.AddDays(3);
        var boundary = new InstrumentBoundaryEvidence(Stock, "SYN", "Synthetic", Base.AddDays(-10), Base,
            null, "VERIFIED", "synthetic", "https://reference.example/listing", "proof", Later.AddMinutes(-1),
            Later.AddMinutes(-1), "VERIFIED", "1", "Synthetic only");
        var listing = new ScreenerListingEvidence(Stock, boundary.KnownAt, new('a', 64), new EvidenceResult<InstrumentBoundaryEvidence>(boundary, null));
        var i = f.Inputs with { ForwardDatabase = new([], [listing]) };
        Assert.Equal("POST_DELISTING", Evaluate(f, i).Reason);
        Assert.Equal(date, Evaluate(f, i).HorizonMarketDate);
        Assert.False(Evaluate(f, f.Inputs with { ForwardDatabase = new([Bar(date.AddDays(1))], []) }).Terminal);
    }
    [Fact]
    public void PreviouslyCompletedForwardSessionCannotBecomeProspective()
    {
        var f = Fixture(); var h = f.H with { Through = Base.AddDays(3), CapturedAt = Capture.AddDays(3) };
        var proof = Proof(Base.AddDays(3)) with { CompletedAt = Capture.AddDays(3).AddMinutes(-1) };
        var horizon = OutcomeEvaluator.ResolveHorizon(h, 1, [proof], Later, TestContext.Current.CancellationToken);
        Assert.True(horizon.NonProspective);
        Assert.Equal("ANCHOR_UNAVAILABLE", OutcomeEvaluator.Evaluate(h, f.Row, 1, horizon, f.Inputs, Later, TestContext.Current.CancellationToken).State);
    }
    [Theory]
    [InlineData("{}")][InlineData("{\"horizonSessions\":2}")][InlineData("{\"horizonSessions\":1,\"knownAt\":null}")]
    [InlineData("{\"horizonSessions\":1,\"horizonSessions\":1}")][InlineData("{\"horizonSessions\":\"1\"}")]
    public void StrictIntentDoesNotAcceptResultsOrClocks(string json)
    {
        using var body = JsonDocument.Parse(json); Assert.Throws<ScreenerException>(() => OutcomeRequest.Parse(body.RootElement));
    }
}
