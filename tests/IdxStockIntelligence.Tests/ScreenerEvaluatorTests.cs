using System.Globalization;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvaluatorTests
{
    private static readonly Guid Stock = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid Index = Guid.Parse("10000000-0000-4000-8000-000000000002");
    private static readonly Guid Outside = Guid.Parse("10000000-0000-4000-8000-000000000003");
    private static readonly DateTimeOffset Known = new(2026, 12, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Known.AddDays(1);
    private static readonly string RawHash = new('c', 64);
    private sealed record Inputs(ScreenerReadRequest Request, ScreenerDatabaseEvidence Database, SelectedScreenerReferences References);
    private static DateOnly[] Dates(int count)
    {
        var dates = new List<DateOnly>();
        for (var day = ScreenerReadRequest.Anchor; dates.Count < count; day = day.AddDays(1))
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) dates.Add(day);
        return dates.ToArray();
    }
    private static ScreenerBarEvidence Bar(Guid id, DateOnly day, decimal close, decimal high, decimal low, long volume = 100) => new(id, day, 1,
        Known, ScreenerReferences.Hash(new { id, day, close, high, low, volume }), Guid.Parse("20000000-0000-4000-8000-000000000001"),
        Guid.Parse("30000000-0000-4000-8000-000000000001"), "synthetic", RawHash, Known.AddMinutes(-1), Known.AddMinutes(-1),
        "https://reference.example/session", Known, "DEGRADED", close.ToString(CultureInfo.InvariantCulture), high.ToString(CultureInfo.InvariantCulture),
        low.ToString(CultureInfo.InvariantCulture), close.ToString(CultureInfo.InvariantCulture), volume, null, "SHARES", "RAW_AS_TRADED", "REGULAR");
    private static readonly ScreenerSourceEvidence Source = new("synthetic", "synthetic", "https://reference.example/synthetic", null, Known, Known);
    private static InstrumentSnapshot Reference(Guid id, IReadOnlyList<ScreenerBarEvidence> bars, bool index = false) => Seal(new("synthetic-" + id, id,
        Known, [Source], "", [new(ScreenerReadRequest.Anchor, null, index ? "SYNINDEX" : "SYN", "Synthetic", index ? "INDEX" : "ORDINARY",
            index ? "NOT_APPLICABLE" : "IDR", index ? "NOT_APPLICABLE" : "MAIN", ["synthetic"])],
        [new(ScreenerReadRequest.Anchor, ScreenerReadRequest.Horizon, "TRADING", "CONTINUOUS", ["synthetic"])],
        [new(ScreenerReadRequest.Anchor, ScreenerReadRequest.Horizon, "synthetic", index ? "INDEX_LEVEL" : "STOCK_RAW", "RAW_AS_TRADED",
            index ? "NOT_APPLICABLE" : "CLEARED", bars.Select(b => b.ContentHash).ToArray(), ["synthetic"])],
        [new(ScreenerReadRequest.Anchor, ScreenerReadRequest.Horizon, "synthetic", "SHARES", "RAW_AS_TRADED", true, "IDR", "REGULAR",
            bars.Select(b => b.ContentHash).ToArray(), ["synthetic"])]));
    private static InstrumentSnapshot Seal(InstrumentSnapshot snapshot) => snapshot with { ContentHash = ScreenerReferences.SnapshotHash(snapshot) };
    private static ScreenerListingEvidence Listing(Guid id, DateOnly? listed = null, DateOnly? delisted = null) => new(id, Known, RawHash,
        new(new(id, "SYN", "Synthetic", listed ?? ScreenerReadRequest.Anchor, delisted, null, "VERIFIED", "synthetic", "https://reference.example/listing",
            "synthetic", Known, Known, "VERIFIED", "1", "Synthetic only"), null));
    private static Inputs Fixture(int count = 21, Func<int, decimal>? close = null, Func<int, decimal>? high = null)
    {
        var dates = Dates(count);
        var stock = dates.Select((d, i) => Bar(Stock, d, close?.Invoke(i) ?? (i == count - 1 ? 101m : 90m), high?.Invoke(i) ?? (i == count - 1 ? 101m : 100m), 80)).ToArray();
        var index = dates.Select(d => Bar(Index, d, 100, 100, 100, 0)).ToArray();
        var universe = new UniverseSnapshot("synthetic-universe", Known, "PILOT", [Stock], Index, [Source], "");
        universe = universe with { ContentHash = ScreenerReferences.SnapshotHash(universe) };
        return new(new([Stock], Index, ScreenerReadRequest.Anchor, dates[^1], Known), new(stock.Concat(index).ToArray(), [Listing(Stock)]),
            new([universe], [Reference(Stock, stock), Reference(Index, index, true)],
                dates.Select(d => new SessionProof(d, ExchangeDayStatus.ObservedTrading, "https://reference.example/session", Known)).ToArray(), []));
    }
    private static Inputs StockReference(Inputs input, Func<InstrumentSnapshot, InstrumentSnapshot> change) => input with
    { References = input.References with { Instruments = input.References.Instruments.Select(s => s.InstrumentId == Stock ? Seal(change(s)) : s).ToArray() } };
    private static ScreenerResult Evaluate(Inputs input, ScreenerPortfolioHistory? portfolio = null) =>
        ScreenerEvaluator.Evaluate(input.Request, input.Database, input.References, portfolio, TestContext.Current.CancellationToken);
    private static ScreenerRow StockRow(Inputs input) => Assert.Single(Evaluate(input).Rows, r => r.InstrumentId == Stock);

    // Full typed results captured and compared byte-for-byte with the pre-extraction 0400bb2 assembly.
    [Theory]
    [InlineData(21, false, "ec290d8d59d644891a6f4089633b22f2e61142e37102a7ded584d1d7b8af59c4")]
    [InlineData(50, false, "41922339e10cb58c7b2936398433ce7df60a2d9c54bad4ff5cfe53f6bfc87734")]
    [InlineData(61, false, "e97a2cb48762b49717c18e21f0b43821cbe51de02effa71ce4e19e1d36603cc6")]
    [InlineData(61, true, "26bf0fed5ba17b00ee61ab2c522db1f33f2adf5894c716ca33b19dce9820fc17")]
    public void SharedArithmeticPreservesPreExtractionFullV01Results(int count, bool zero, string expectedHash)
    {
        var input = Fixture(count);
        if (zero) input = input with { Database = input.Database with { Bars = input.Database.Bars.Select(b =>
            b.InstrumentId == Stock ? b with { Volume = 0 } : b).ToArray() } };
        Assert.Equal(expectedHash, ScreenerReferences.Hash(Evaluate(input), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("unsupported-type", EligibilityStatus.Ineligible, "UNSUPPORTED_TYPE")]
    [InlineData("index-type", EligibilityStatus.Ineligible, "UNSUPPORTED_TYPE")]
    [InlineData("pre-listing", EligibilityStatus.Ineligible, "PRE_LISTING")]
    [InlineData("post-delisting", EligibilityStatus.Ineligible, "POST_DELISTING")]
    [InlineData("unsupported-board", EligibilityStatus.Ineligible, "UNSUPPORTED_BOARD")]
    [InlineData("auction", EligibilityStatus.Ineligible, "UNSUPPORTED_BOARD")]
    [InlineData("suspension", EligibilityStatus.Ineligible, "SUSPENDED")]
    [InlineData("no-trade", EligibilityStatus.Ineligible, "NO_TRADE")]
    [InlineData("missing-reference", EligibilityStatus.DataBlocked, "REFERENCE_NOT_KNOWN")]
    [InlineData("conflicting-reference", EligibilityStatus.DataBlocked, "IDENTITY_CONFLICT")]
    [InlineData("unknown-type", EligibilityStatus.DataBlocked, "TYPE_UNKNOWN")]
    [InlineData("unknown-board", EligibilityStatus.DataBlocked, "BOARD_UNKNOWN")]
    [InlineData("unknown-status", EligibilityStatus.DataBlocked, "STATUS_UNKNOWN")]
    [InlineData("unknown-mechanism", EligibilityStatus.DataBlocked, "STATUS_UNKNOWN")]
    [InlineData("missing-current", EligibilityStatus.DataBlocked, "MISSING_CURRENT_BAR")]
    [InlineData("stale", EligibilityStatus.DataBlocked, "STALE")]
    [InlineData("short-history", EligibilityStatus.DataBlocked, "INSUFFICIENT_HISTORY")]
    [InlineData("unknown-listing", EligibilityStatus.DataBlocked, "LISTING_UNKNOWN")]
    [InlineData("price-basis", EligibilityStatus.DataBlocked, "PRICE_BASIS_UNVERIFIED")]
    [InlineData("invalid-canonical", EligibilityStatus.DataBlocked, "CANONICAL_INVALID")]
    [InlineData("overflow", EligibilityStatus.DataBlocked, "NUMERIC_OUT_OF_RANGE")]
    [InlineData("zero-volume", EligibilityStatus.DataBlocked, "CANONICAL_INVALID")]
    [InlineData("unsupported-missing", EligibilityStatus.Ineligible, "UNSUPPORTED_TYPE")]
    [InlineData("listing-conflict", EligibilityStatus.DataBlocked, "IDENTITY_CONFLICT")]
    public void EligibilityAndNotEvaluatedReasonsAreIntegrated(string problem, EligibilityStatus expected, string reason)
    {
        var input = Fixture(problem == "short-history" ? 20 : 21);
        input = problem switch
        {
            "unsupported-type" or "unsupported-missing" => StockReference(input, s => s with { Identities = [s.Identities[0] with { Classification = "UNSUPPORTED" }] }),
            "index-type" => StockReference(input, s => s with { Identities = [s.Identities[0] with { Classification = "INDEX" }] }),
            "unsupported-board" => StockReference(input, s => s with { Identities = [s.Identities[0] with { Board = "ACCELERATION" }] }),
            "unknown-board" => StockReference(input, s => s with { Identities = [s.Identities[0] with { Board = "UNKNOWN" }] }),
            "unknown-type" => StockReference(input, s => s with { Identities = [s.Identities[0] with { Classification = "UNKNOWN" }] }),
            "auction" => StockReference(input, s => s with { Trading = [s.Trading[0] with { Mechanism = "CALL_AUCTION" }] }),
            "unknown-mechanism" => StockReference(input, s => s with { Trading = [s.Trading[0] with { Mechanism = "UNKNOWN" }] }),
            "unknown-status" => StockReference(input, s => s with { Trading = [s.Trading[0] with { Status = "UNKNOWN" }] }),
            "suspension" => StockReference(input, s => s with { Trading = [s.Trading[0] with { Status = "SUSPENSION" }] }),
            "no-trade" => StockReference(input, s => s with { Trading = [s.Trading[0] with { Status = "NO_TRADE" }] }),
            "price-basis" => StockReference(input, s => s with { Prices = [s.Prices[0] with { EventCoverage = "UNRESOLVED" }] }),
            _ => input
        };
        if (problem == "pre-listing") input = input with { Database = input.Database with { Listings = [Listing(Stock, input.Request.Through.AddDays(1))] } };
        if (problem == "post-delisting") input = input with { Database = input.Database with { Listings = [Listing(Stock, delisted: input.Request.Through.AddDays(-1))] } };
        if (problem == "unknown-listing") input = input with { Database = input.Database with { Listings = [] } };
        if (problem == "listing-conflict") input = input with { Database = input.Database with { Listings = [new(Stock, Known, RawHash, new(null, "LISTING_CONFLICT"))] } };
        if (problem == "missing-reference") input = input with { References = input.References with { Instruments = input.References.Instruments.Where(s => s.InstrumentId != Stock).ToArray() } };
        if (problem == "conflicting-reference") input = input with { References = input.References with { Instruments = input.References.Instruments.Append(Seal(input.References.Instruments[0] with { SnapshotId = "tie" })).ToArray() } };
        if (problem is "missing-current" or "stale" or "unsupported-missing") input = input with
        { Database = input.Database with { Bars = input.Database.Bars.Where(b => b.InstrumentId != Stock || b.SessionDate != input.Request.Through).ToArray() } };
        if (problem is "invalid-canonical" or "overflow" or "zero-volume") input = input with { Database = input.Database with
        { Bars = input.Database.Bars.Select(b => b.InstrumentId == Stock && b.SessionDate == input.Request.Through
            ? problem == "overflow" ? b with { Close = "79228162514264337593543950336" } : problem == "zero-volume" ? b with { Volume = 0 } : b with { CanonicalQuality = "REJECTED" } : b).ToArray() } };
        var row = StockRow(input);
        Assert.Equal(expected, row.Eligibility.Status); Assert.Contains(reason, row.Eligibility.Reasons);
        Assert.Equal(SetupStatus.None, row.Setup.Status); Assert.False(row.Setup.Evaluated);
        Assert.Equal("NOT_EVALUATED", Assert.Single(row.Setup.Reasons));
        if (problem == "no-trade") Assert.True(row.NoTrade);
        if (problem == "zero-volume") { Assert.Null(row.NoTrade); Assert.Contains("ZERO_VOLUME_UNEXPLAINED", row.Eligibility.Reasons); }
        if (problem is "stale" or "missing-current")
        { Assert.True(row.Stale); Assert.Equal(Dates(21)[^2], row.MarketDate); Assert.Equal(90m, row.Close); Assert.Null(row.Fields["ema20"].Value); }
        if (problem == "unsupported-missing") Assert.Equal("UNSUPPORTED_TYPE", row.Eligibility.Reasons[0]);
    }

    [Fact]
    public void OptionalFeaturesMissingDoNotGatePriceSetupOrPromoteCanonicalQuality()
    {
        var input = StockReference(Fixture(), s => s with { Volumes = [] });
        input = input with { Database = input.Database with { Bars = input.Database.Bars.Where(b => b.InstrumentId != Index).ToArray() } };
        var result = Evaluate(input); var row = Assert.Single(result.Rows);
        Assert.Equal(EligibilityStatus.Eligible, row.Eligibility.Status); Assert.Empty(row.Eligibility.Reasons);
        Assert.True(row.Setup.Evaluated); Assert.Equal(SetupStatus.Confirmed, row.Setup.Status);
        Assert.Equal(ScreenerQuality.Partial, row.DataQuality); Assert.Equal(ScreenerQuality.Partial, result.Status);
        Assert.Equal(Availability.WARMUP, row.Fields["ema50"].Availability);
        Assert.Equal(Availability.WARMUP, row.Fields["rs60Pp"].Availability);
        Assert.Equal("BENCHMARK_MISSING", row.Fields["rs20Pp"].UnavailableReason);
        Assert.Equal("VOLUME_BASIS_UNVERIFIED", row.Fields["monetaryLiquidity20Idr"].UnavailableReason);
        Assert.Equal("UNKNOWN", result.MarketContext.Trend); Assert.Equal("UNKNOWN", result.MarketContext.Volatility);
        Assert.Equal("DEGRADED", row.Provenance.Observation!.CanonicalQuality);
        Assert.Equal(1, result.Summary.Evaluated); Assert.Equal(1, result.Summary.Candidates);
    }

    [Theory]
    [InlineData("MAIN")]
    [InlineData("DEVELOPMENT")]
    public void SupportedBoardsEvaluateWith21Observations(string board)
    {
        var row = StockRow(StockReference(Fixture(), s => s with { Identities = [s.Identities[0] with { Board = board }] }));
        Assert.Equal(EligibilityStatus.Eligible, row.Eligibility.Status); Assert.Equal(21, row.Provenance.ConsecutiveSessions);
        Assert.Equal(100m, row.Fields["priorHigh20"].Value); Assert.Equal(80m, row.Fields["priorLow20"].Value);
    }

    [Theory]
    [InlineData("97.99", SetupStatus.None)]
    [InlineData("98", SetupStatus.Watch)]
    [InlineData("100", SetupStatus.Watch)]
    [InlineData("100.01", SetupStatus.Confirmed)]
    [InlineData("99", SetupStatus.Watch)]
    [InlineData("97", SetupStatus.None)]
    public void IntradayHighNeverCreatesFailureOrChangesCloseComparison(string close, SetupStatus expected)
    {
        var price = decimal.Parse(close, CultureInfo.InvariantCulture);
        var row = StockRow(Fixture(close: i => i == 20 ? price : 90, high: i => i == 20 ? 101 : 100));
        Assert.Equal(expected, row.Setup.Status); Assert.True(row.Setup.Evaluated);
        Assert.Equal(100m, row.Fields["priorHigh20"].Value);
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(false, "calendar")]
    [InlineData(true, "calendar")]
    [InlineData(false, "basis")]
    [InlineData(true, "basis")]
    [InlineData(false, "suspension")]
    [InlineData(true, "suspension")]
    public void ReplayInterruptsBothActiveStatesAndNeverResurrects(bool confirmed, string cause)
    {
        var input = Fixture(23, i => i < 20 ? 90 : confirmed ? 101 : 99, i => i < 20 ? 100 : 101);
        var interruption = Dates(23)[21];
        if (cause == "missing") input = input with { Database = input.Database with { Bars = input.Database.Bars.Where(b => b.InstrumentId != Stock || b.SessionDate != interruption).ToArray() } };
        if (cause == "calendar") input = input with { References = input.References with { Sessions = input.References.Sessions.Where(s => s.Date != interruption).ToArray() } };
        if (cause == "basis") input = StockReference(input, s => s with { Prices = [s.Prices[0] with
        { ContentHashes = input.Database.Bars.Where(b => b.InstrumentId == Stock && b.SessionDate != interruption).Select(b => b.ContentHash).ToArray() }] });
        if (cause == "suspension") input = StockReference(input, s => s with { Trading =
        [s.Trading[0] with { Through = interruption.AddDays(-1) }, s.Trading[0] with { From = interruption, Through = interruption, Status = "SUSPENSION" },
            s.Trading[0] with { From = interruption.AddDays(1) }] });
        var interrupted = StockRow(input with { Request = input.Request with { Through = interruption } });
        Assert.False(interrupted.Setup.Evaluated); Assert.Equal(SetupStatus.None, interrupted.Setup.Status);
        Assert.Equal(cause == "suspension" ? "INELIGIBLE" : "DATA_INTERRUPTED", interrupted.Setup.Episode!.EndReason);
        Assert.Equal(1, interrupted.Setup.Episode.AgeSessions); Assert.Equal(interruption, interrupted.Setup.Episode.EndDate);
        var next = StockRow(input);
        Assert.False(next.Setup.Evaluated); Assert.Null(next.Setup.Episode); Assert.Equal(1, next.Provenance.ConsecutiveSessions);
    }

    [Fact]
    public void ClosureAndWeekendDoNotAgeEpisodeOrMakeItStale()
    {
        var input = Fixture(25, i => i < 20 ? 90 : 99);
        var friday = input.Request.Through;
        Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);
        var closedMonday = friday.AddDays(3);
        input = input with { Request = input.Request with { Through = closedMonday }, References = input.References with
        { Sessions = input.References.Sessions.Append(new(closedMonday, ExchangeDayStatus.AnnouncedClosed, "https://reference.example/closure", Known)).ToArray() } };
        var result = Evaluate(input); var row = Assert.Single(result.Rows);
        Assert.Equal(friday, result.TargetSession); Assert.False(row.Stale); Assert.Equal(friday, row.MarketDate);
        Assert.Equal(5, row.Setup.Episode!.AgeSessions); Assert.Equal(SetupStatus.Watch, row.Setup.Status);
    }

    [Fact]
    public void UnknownWeekdayAfterTargetDoesNotReuseOldCurrentFeatures()
    {
        var input = Fixture(); input = input with { Request = input.Request with { Through = input.Request.Through.AddDays(1) } };
        var result = Evaluate(input); var row = Assert.Single(result.Rows);
        Assert.Equal(Dates(21)[^1], result.TargetSession); Assert.Equal(ScreenerQuality.Blocked, result.Status);
        Assert.False(row.Setup.Evaluated); Assert.Null(row.Stale); Assert.Null(row.Fields["priorHigh20"].Value);
        Assert.Equal(Dates(21)[^1], row.MarketDate); Assert.Equal("DATA_INTERRUPTED", row.Setup.Episode!.EndReason);
    }

    [Fact]
    public void UnknownUniverseHasNullCountsRetainsHeldStableIdAndNoRegistryLabel()
    {
        var input = Fixture(); input = input with { References = input.References with { Universes = [], Instruments = [] } };
        var result = Evaluate(input, Portfolio(Stock)); var row = Assert.Single(result.Rows);
        Assert.True(row.Held); Assert.False(row.Configured); Assert.Null(row.Symbol); Assert.Null(row.DisplayName);
        Assert.Null(result.Summary.Configured); Assert.Null(result.Summary.DataBlocked); Assert.Null(result.Summary.Candidates);
        Assert.Null(result.Summary.HeldOutsideUniverse); Assert.Equal(1, result.Summary.Held); Assert.Equal(ScreenerQuality.Blocked, result.Status);
    }

    [Fact]
    public void AllKnownExclusionsCanBeCompleteWithNoCandidatesOrIndicators()
    {
        var input = StockReference(Fixture(), s => s with { Identities = [s.Identities[0] with { Classification = "UNSUPPORTED" }] });
        input = input with { Database = input.Database with { Bars = [] } };
        var result = Evaluate(input);
        Assert.Equal(ScreenerQuality.Complete, result.Status); Assert.Equal(1, result.Summary.Ineligible);
        Assert.Equal(0, result.Summary.Candidates); Assert.Equal(0, result.Summary.Evaluated);
        Assert.Equal(ScreenerQuality.Complete, Assert.Single(result.Rows).DataQuality);
    }

    [Fact]
    public void SharedIndexFeaturesAcceptZeroVolumeAndFlatZeroAtr()
    {
        var result = Evaluate(Fixture(61, _ => 90));
        Assert.Equal("NEUTRAL", result.MarketContext.Trend); Assert.Equal("NORMAL", result.MarketContext.Volatility);
        Assert.Equal(0m, result.MarketContext.Fields["atr14"].Value); Assert.Equal(0m, result.MarketContext.Fields["atrPercent"].Value);
        Assert.Equal(100m, result.MarketContext.Fields["ema50"].Value); Assert.Empty(result.MarketContext.Reasons);
        Assert.Equal(0m, Assert.Single(result.Rows).Rs60Pp);
    }

    [Fact]
    public void ClearedValueProxyExcludesCurrentAndVolumeSurpriseIsOnlyDescriptive()
    {
        var input = Fixture(close: i => i == 20 ? 100 : 10, high: i => i == 20 ? 100 : 10);
        input = input with { Database = input.Database with { Bars = input.Database.Bars.Select(b => b.InstrumentId == Stock
            ? b with { Low = "10", Volume = b.SessionDate == input.Request.Through ? 10000 : 100 } : b).ToArray() } };
        var row = StockRow(input);
        Assert.Equal(1000m, row.MonetaryLiquidity20Idr); Assert.Equal(1000000m, row.Fields["dailyValueProxyIdr"].Value);
        Assert.Equal(100m, row.Fields["volumeRatio20"].Value); Assert.Equal(SetupStatus.Confirmed, row.Setup.Status);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("incompatible")]
    [InlineData("revised")]
    [InlineData("mixed")]
    public void VolumeClearanceIsRevisionSpecificAndIndependentOfPrice(string kind)
    {
        var input = Fixture();
        if (kind == "unknown") input = StockReference(input, s => s with { Volumes = [s.Volumes[0] with { MarketSegment = "UNKNOWN" }] });
        if (kind == "incompatible") input = StockReference(input, s => s with { Volumes = [s.Volumes[0] with { RawPriceCompatible = false }] });
        if (kind == "revised") input = StockReference(input, s => s with { Volumes = [s.Volumes[0] with { ContentHashes = s.Volumes[0].ContentHashes.SkipLast(1).ToArray() }] });
        if (kind == "mixed")
        {
            var t = input.Request.Through;
            input = StockReference(input, s => s with { Volumes = [s.Volumes[0] with { Through = t.AddDays(-1) }, s.Volumes[0] with { From = t, MarketSegment = "ALL_MARKETS" }] });
            input = input with { Database = input.Database with { Bars = input.Database.Bars.Select(b => b.InstrumentId == Stock && b.SessionDate == t ? b with { MarketSegment = "ALL_MARKETS" } : b).ToArray() } };
        }
        var row = StockRow(input);
        Assert.Equal(EligibilityStatus.Eligible, row.Eligibility.Status); Assert.Equal(SetupStatus.Confirmed, row.Setup.Status);
        Assert.Null(row.MonetaryLiquidity20Idr); Assert.Null(row.Fields["volumeRatio20"].Value);
        Assert.Equal("VOLUME_BASIS_UNVERIFIED", row.Fields["monetaryLiquidity20Idr"].UnavailableReason);
        if (kind == "mixed") Assert.NotNull(row.Fields["dailyValueProxyIdr"].Value);
    }

    private static ScreenerPortfolioHistory Portfolio(params Guid[] held)
    {
        var p = new Portfolio(Guid.Parse("40000000-0000-4000-8000-000000000001"), "Synthetic", true, Known.AddDays(-1));
        var events = held.Select((id, i) => PortfolioLedger.Canonicalize(p.Id, new(Guid.Parse($"50000000-0000-4000-8000-{i + 1:000000000000}"),
            PortfolioEventType.BUY, id, ScreenerReadRequest.Anchor, 100, Price: 10), Known, i + 1)).ToArray();
        return new(p, events, []);
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("none")]
    [InlineData("failed")]
    [InlineData("blocked")]
    [InlineData("ineligible")]
    [InlineData("outside")]
    [InlineData("missing-label")]
    public void EveryPositiveHeldPositionSurvivesDiscoveryAndHasOneLogicalRow(string kind)
    {
        var input = kind == "none" ? Fixture(close: _ => 90) : kind == "failed" ? Fixture(22, i => i == 20 ? 101 : 90, i => i == 20 ? 101 : 100) : Fixture();
        if (kind == "blocked") input = input with { Database = input.Database with { Bars = [] } };
        if (kind == "ineligible") input = StockReference(input, s => s with { Identities = [s.Identities[0] with { Classification = "UNSUPPORTED" }] });
        if (kind == "missing-label") input = StockReference(input, s => s with { Identities = [s.Identities[0] with { Symbol = null, DisplayName = null }] });
        var id = kind == "outside" ? Outside : Stock;
        var result = Evaluate(input, Portfolio(id)); var row = Assert.Single(result.Rows, r => r.InstrumentId == id);
        Assert.True(row.Held); Assert.Equal(id, Assert.Single(result.HeldIds)); Assert.Equal(1, result.Summary.Held);
        if (kind == "candidate") { Assert.Equal(1, row.DiscoveryRank); Assert.Equal(id, Assert.Single(result.ShortlistIds)); Assert.Single(result.Rows); }
        if (kind == "none") { Assert.Equal(SetupStatus.None, row.Setup.Status); Assert.True(row.Setup.Evaluated); Assert.Null(row.DiscoveryRank); }
        if (kind == "failed") { Assert.Equal(SetupStatus.Failed, row.Setup.Status); Assert.Null(row.DiscoveryRank); }
        if (kind == "outside")
        {
            Assert.False(row.Configured); Assert.Null(row.Symbol); Assert.Null(row.DisplayName); Assert.Equal(1, result.Summary.HeldOutsideUniverse);
            Assert.Null(row.Close); Assert.Null(row.Volume); Assert.Equal(Availability.UNAVAILABLE, row.Fields["close"].Availability);
            Assert.NotNull(row.Fields["volume"].UnavailableReason);
        }
        if (kind == "missing-label") { Assert.Null(row.Symbol); Assert.Null(row.DisplayName); Assert.Equal(EligibilityStatus.Eligible, row.Eligibility.Status); }
    }

    [Fact]
    public void ClosedPositionAndFutureDatedTradeAreNotHeldEvenWithOldThesis()
    {
        var input = Fixture(); var p = Portfolio(Stock, Outside);
        var sell = PortfolioLedger.Canonicalize(p.Portfolio.Id, new(Guid.Parse("50000000-0000-4000-8000-000000000010"), PortfolioEventType.SELL,
            Stock, input.Request.Through, 100, Price: 10), Known, 3);
        p = p with { Events = [p.Events[0], p.Events[1] with { TradeDate = input.Request.Through.AddDays(1) }, sell], Theses = [Thesis(p, Stock, 1, Mandate.INVEST, Known)] };
        var result = Evaluate(input, p);
        Assert.Empty(result.HeldIds); Assert.Equal(0, result.Summary.Held); Assert.False(Assert.Single(result.Rows).Held);
    }

    private static ThesisVersion Thesis(ScreenerPortfolioHistory p, Guid id, int version, Mandate mandate, DateTimeOffset at, bool active = true) =>
        new(Guid.Parse($"60000000-0000-4000-8000-{version:000000000000}"), p.Portfolio.Id, id, version, mandate, "Synthetic", at, null, null, active);

    [Fact]
    public void LateCorrectionAndMandateChangeRespectCutoffAndNeverAffectFeaturesOrRanks()
    {
        var input = Fixture(); var p = Portfolio(Stock, Outside);
        var correction = p.Events[1] with { Id = Guid.Parse("50000000-0000-4000-8000-000000000011"), KnownAt = Later, Quantity = 200, TradeDate = input.Request.Through.AddDays(1), Supersedes = p.Events[1].Id };
        p = p with { Events = p.Events.Append(correction).ToArray(), Theses = [Thesis(p, Stock, 1, Mandate.FAST_SWING, Known), Thesis(p, Stock, 2, Mandate.INVEST, Later)] };
        var old = Evaluate(input, p); var newer = Evaluate(input with { Request = input.Request with { Cutoff = Later } }, p);
        Assert.Contains(Outside, old.HeldIds); Assert.DoesNotContain(Outside, newer.HeldIds);
        var oldRow = Assert.Single(old.Rows, r => r.InstrumentId == Stock); var newRow = Assert.Single(newer.Rows, r => r.InstrumentId == Stock);
        Assert.Equal(Mandate.FAST_SWING, oldRow.Mandate); Assert.Equal(Mandate.INVEST, newRow.Mandate);
        Assert.Equal(oldRow.Setup.Episode, newRow.Setup.Episode); Assert.Equal(oldRow.Setup.Reasons, newRow.Setup.Reasons); Assert.Equal(oldRow.DiscoveryRank, newRow.DiscoveryRank);
        Assert.Equal(oldRow.Fields, newRow.Fields);
    }

    [Fact]
    public void LatestInactiveThesisDoesNotResurrectOlderActiveMandate()
    {
        var input = Fixture(); var p = Portfolio(Stock);
        p = p with { Theses = [Thesis(p, Stock, 1, Mandate.LONG_SWING, Known), Thesis(p, Stock, 2, Mandate.INVEST, Later, false)] };
        Assert.Equal(Mandate.LONG_SWING, Assert.Single(Evaluate(input, p).Rows).Mandate);
        Assert.Null(Assert.Single(Evaluate(input with { Request = input.Request with { Cutoff = Later } }, p).Rows).Mandate);
    }

    [Fact]
    public void FutureOnlyInputsAndOrderDoNotChangeExplicitHistoricalEvaluation()
    {
        var input = Fixture(); var p = Portfolio(Stock); var result = Evaluate(input, p);
        var futureBar = input.Database.Bars[0] with { SessionDate = input.Request.Through.AddDays(1), KnownAt = Later };
        var futureSnapshot = Seal(input.References.Instruments[0] with { SnapshotId = "future", KnownAt = Later,
            Identities = [input.References.Instruments[0].Identities[0] with { Symbol = "FUTURE" }] });
        var extra = input with { Database = input.Database with { Bars = input.Database.Bars.Append(futureBar).Reverse().ToArray() },
            References = input.References with { Instruments = input.References.Instruments.Append(futureSnapshot).Reverse().ToArray(),
                Sessions = input.References.Sessions.Append(new(input.Request.Through.AddDays(1), ExchangeDayStatus.ObservedTrading, "https://reference.example/future", Later)).Reverse().ToArray() } };
        p = p with { Events = p.Events.Append(p.Events[0] with { Id = Outside, InstrumentId = Outside, KnownAt = Later }).ToArray(),
            Theses = [Thesis(p, Stock, 1, Mandate.INVEST, Later)] };
        var repeated = Evaluate(extra, p);
        Assert.Equal(result.RankedCandidateIds, repeated.RankedCandidateIds); Assert.Equal(result.AllViewIds, repeated.AllViewIds);
        Assert.Equal(result.ShortlistIds, repeated.ShortlistIds); Assert.Equal(result.HeldIds, repeated.HeldIds);
        var a = Assert.Single(result.Rows); var b = Assert.Single(repeated.Rows);
        Assert.Equal(ScreenerReferences.Hash(a.Setup), ScreenerReferences.Hash(b.Setup)); Assert.Equal(a.Eligibility.Status, b.Eligibility.Status); Assert.Equal(a.Fields, b.Fields);
        Assert.Equal(a.Provenance, b.Provenance); Assert.Equal(a.Symbol, b.Symbol); Assert.Equal(a.Mandate, b.Mandate);
    }

    [Fact]
    public void CancellationAndBoundsAreExplicitAndDuplicateSelectedBarsAreRejected()
    {
        var input = Fixture(); using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => ScreenerEvaluator.Evaluate(input.Request, input.Database, input.References, null, canceled.Token));
        Assert.Throws<ArgumentException>(() => Evaluate(input with { Request = input.Request with { Through = ScreenerReadRequest.Horizon.AddDays(1) } }));
        Assert.Throws<ArgumentException>(() => Evaluate(input with { Database = input.Database with { Bars = input.Database.Bars.Append(input.Database.Bars[0]).ToArray() } }));
        var p = Portfolio(Stock); Assert.Throws<ArgumentException>(() => Evaluate(input, p with { Events = Enumerable.Repeat(p.Events[0], PortfolioLedger.MaximumEvents + 1).ToArray() }));
        Assert.Throws<KeyNotFoundException>(() => Evaluate(input, p with { Portfolio = p.Portfolio with { CreatedAt = Later } }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullReplayHonorsExpiryPriorityAndStartsOnlyOnFollowingSession(bool confirmed)
    {
        var expiry = confirmed ? 40 : 25;
        var input = Fixture(expiry + 2, i => i < 20 ? 90 : i == expiry ? confirmed ? 99 : 101 : i > expiry ? 102 : confirmed ? 101 : 99,
            i => i > expiry ? 102 : i >= 20 ? 101 : 100);
        var row = StockRow(input with { Request = input.Request with { Through = Dates(expiry + 2)[expiry] } });
        Assert.Equal(SetupStatus.None, row.Setup.Status); Assert.True(row.Setup.Evaluated);
        Assert.Equal(confirmed ? "CONFIRMED_EXPIRED" : "WATCH_EXPIRED", row.Setup.Episode!.EndReason);
        Assert.Equal(confirmed ? 20 : 5, row.Setup.Episode.AgeSessions);
        var next = StockRow(input);
        Assert.Equal(SetupStatus.Confirmed, next.Setup.Status); Assert.Equal(1, next.Setup.Episode!.AgeSessions);
        Assert.NotEqual(row.Setup.Episode.Id, next.Setup.Episode.Id); Assert.Equal(101m, next.Setup.Episode.TriggerPrice);
    }

    [Fact]
    public void BenchmarkInteriorGapBlocksReturnsWithoutVetoingStockSetup()
    {
        var input = Fixture(61);
        input = input with { Database = input.Database with { Bars = input.Database.Bars.Where(b => b.InstrumentId != Index || b.SessionDate != Dates(61)[50]).ToArray() } };
        var result = Evaluate(input); var row = Assert.Single(result.Rows);
        Assert.Equal(EligibilityStatus.Eligible, row.Eligibility.Status); Assert.Equal(SetupStatus.Confirmed, row.Setup.Status);
        Assert.Null(row.Rs20Pp); Assert.Null(row.Rs60Pp); Assert.Equal("BENCHMARK_MISSING", row.Fields["rs20Pp"].UnavailableReason);
        Assert.Equal("UNKNOWN", result.MarketContext.Trend); Assert.Equal(10, result.MarketContext.Provenance.ConsecutiveSessions);
    }

    [Fact]
    public void IndexVolatilityAtExactlyTwoPercentIsElevatedIndependentlyOfTrend()
    {
        var input = Fixture(61);
        var bars = input.Database.Bars.Select(b => b.InstrumentId == Index ? Bar(Index, b.SessionDate, 100, 101, 99, 0) : b).ToArray();
        input = input with { Database = input.Database with { Bars = bars }, References = input.References with
        { Instruments = [input.References.Instruments[0], Reference(Index, bars.Where(b => b.InstrumentId == Index).ToArray(), true)] } };
        var context = Evaluate(input).MarketContext;
        Assert.Equal("NEUTRAL", context.Trend); Assert.Equal("ELEVATED", context.Volatility);
        Assert.Equal(2m, context.Fields["atrPercent"].Value);
    }

    [Fact]
    public void OptionalArithmeticOverflowDoesNotErasePriorHighOrPriceSetup()
    {
        var input = Fixture(21, _ => 9000000000000000000000000000m, _ => 9000000000000000000000000000m);
        var row = StockRow(input);
        Assert.Equal(EligibilityStatus.Eligible, row.Eligibility.Status); Assert.Equal(SetupStatus.Watch, row.Setup.Status);
        Assert.Equal("NUMERIC_OUT_OF_RANGE", row.Fields["ema20"].UnavailableReason);
        Assert.Equal("NUMERIC_OUT_OF_RANGE", row.Fields["dailyValueProxyIdr"].UnavailableReason);
        Assert.NotNull(row.Fields["priorHigh20"].Value); Assert.Equal(ScreenerQuality.Partial, row.DataQuality);
    }

    [Fact]
    public void ListingWithoutRetainedRetrievalIsUnknownAndCannotCertifyEligibility()
    {
        var input = Fixture(); var listing = input.Database.Listings[0];
        input = input with { Database = input.Database with { Listings = [listing with
        { Resolution = new(listing.Resolution.Value! with { RetrievedAt = null }, null) }] } };
        var row = StockRow(input);
        Assert.Equal(EligibilityStatus.DataBlocked, row.Eligibility.Status); Assert.Contains("LISTING_UNKNOWN", row.Eligibility.Reasons);
    }

    [Fact]
    public void SpecificCanonicalFailureRemainsVisibleAlongsideFrozenPrimaryReason()
    {
        var input = Fixture(); input = input with { Database = input.Database with { Bars = input.Database.Bars.Select(b =>
            b.InstrumentId == Stock && b.SessionDate == input.Request.Through ? b with { CanonicalQuality = "REJECTED" } : b).ToArray() } };
        var row = StockRow(input);
        Assert.Contains("CANONICAL_INVALID", row.Eligibility.Reasons); Assert.Contains("CANONICAL_QUALITY_UNAVAILABLE", row.DataReasons);
        Assert.Equal("CANONICAL_INVALID", row.Fields["priorHigh20"].UnavailableReason);
        Assert.Equal("REJECTED", row.Provenance.CurrentEvidence!.CanonicalQuality);
        Assert.Equal(input.Request.Through, row.Provenance.CurrentEvidence.SessionDate);
        Assert.Equal(Dates(21)[^2], row.MarketDate);
    }

    [Fact]
    public void AllExcludedConfiguredRowsDoNotConcealBlockedHeldCoverage()
    {
        var input = StockReference(Fixture(), s => s with { Identities = [s.Identities[0] with { Classification = "UNSUPPORTED" }] });
        var result = Evaluate(input, Portfolio(Outside));
        Assert.Equal(ScreenerQuality.Partial, result.Status); Assert.Equal(1, result.Summary.Ineligible);
        Assert.Equal(ScreenerQuality.Blocked, Assert.Single(result.Rows, r => r.InstrumentId == Outside).DataQuality);
    }

    [Fact]
    public void ConflictingStatusCannotCertifyNoTradeOrInferFailure()
    {
        var input = Fixture(); input = input with { References = input.References with { InstrumentSessions =
            [new(new(Stock), input.Request.Through, MarketSessionStatus.NoTrade, "https://reference.example/no-trade", Known)] } };
        var row = StockRow(input);
        Assert.Equal(EligibilityStatus.DataBlocked, row.Eligibility.Status); Assert.Equal("IDENTITY_CONFLICT", row.Eligibility.Reasons[0]);
        Assert.Null(row.NoTrade); Assert.Null(row.TradingStatus); Assert.False(row.Setup.Evaluated);
    }

    [Fact]
    public void CanonicalFeatureReasonPrecedesUnknownStatusAndAllCausesRemainVisible()
    {
        var input = StockReference(Fixture(), s => s with { Trading = [s.Trading[0] with { Status = "UNKNOWN" }] });
        input = input with { Database = input.Database with { Bars = input.Database.Bars.Select(b => b.InstrumentId == Stock
            && b.SessionDate == input.Request.Through ? b with { RetrievedAt = null } : b).ToArray() } };
        var row = StockRow(input);
        Assert.Contains("STATUS_UNKNOWN", row.Eligibility.Reasons); Assert.Contains("CANONICAL_INVALID", row.Eligibility.Reasons);
        Assert.Equal("CANONICAL_INVALID", row.Fields["ema20"].UnavailableReason);
        Assert.Contains("CANONICAL_PROVENANCE_UNAVAILABLE", row.DataReasons);
    }

    [Theory]
    [InlineData("NO_TRADE", "NO_TRADE")]
    [InlineData("SUSPENSION", "SUSPENDED")]
    public void AffirmativeStatusExplainsZeroVolumeWithoutManufacturingUnexplainedInvalidity(string status, string reason)
    {
        var input = StockReference(Fixture(), s => s with { Trading = [s.Trading[0] with { Status = status }] });
        input = input with { Database = input.Database with { Bars = input.Database.Bars.Select(b => b.InstrumentId == Stock
            ? b with { Volume = 0 } : b).ToArray() } };
        var row = StockRow(input);
        Assert.Equal(EligibilityStatus.Ineligible, row.Eligibility.Status); Assert.Contains(reason, row.Eligibility.Reasons);
        Assert.DoesNotContain("ZERO_VOLUME_UNEXPLAINED", row.Eligibility.Reasons);
        Assert.DoesNotContain("CANONICAL_INVALID", row.Eligibility.Reasons);
        Assert.Equal(status == "NO_TRADE" ? true : (bool?)null, row.NoTrade);
    }

    [Theory]
    [InlineData("pre-listing", "PRE_LISTING")]
    [InlineData("no-trade", "NO_TRADE")]
    [InlineData("suspension", "SUSPENDED")]
    public void ExpectedAbsenceIsDistinctFromMissingProviderRow(string kind, string reason)
    {
        var input = Fixture();
        input = input with { Database = input.Database with { Bars = input.Database.Bars.Where(b => b.InstrumentId != Stock).ToArray() } };
        if (kind == "pre-listing") input = input with { Database = input.Database with { Listings = [Listing(Stock, input.Request.Through.AddDays(1))] } };
        else input = StockReference(input, s => s with { Trading = [s.Trading[0] with { Status = kind == "no-trade" ? "NO_TRADE" : "SUSPENSION" }] });
        var row = StockRow(input);
        Assert.Equal(EligibilityStatus.Ineligible, row.Eligibility.Status); Assert.Contains(reason, row.Eligibility.Reasons);
        Assert.DoesNotContain("MISSING_CURRENT_BAR", row.Eligibility.Reasons); Assert.DoesNotContain("INSUFFICIENT_HISTORY", row.Eligibility.Reasons);
    }
}
