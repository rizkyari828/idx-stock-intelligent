using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ProductSliceTests
{
    private static readonly Guid PortfolioId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid InstrumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset At = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 9, 30);
    private static PortfolioEvent Event(PortfolioEventType type, long order, decimal quantity = 0, decimal price = 0,
        decimal fees = 0, decimal cash = 0, QuantityUnit unit = QuantityUnit.SHARES) =>
        PortfolioLedger.Canonicalize(PortfolioId, new(Guid.NewGuid(), type,
            type is PortfolioEventType.BUY or PortfolioEventType.SELL ? InstrumentId : null,
            Day, quantity, unit, price, fees, cash), At.AddSeconds(order), order);
    private static PortfolioEvent Deposit => Event(PortfolioEventType.CASH_DEPOSIT, 1, cash: 1000000);

    [Fact]
    public void LotsWeightedAveragePartialSaleAndFeesReproduceCashAndRealizedPnl()
    {
        var lots = Event(PortfolioEventType.BUY, 2, 13, 100, 1300, unit: QuantityUnit.LOTS);
        Assert.Equal(1300, lots.Quantity);
        Assert.Equal(QuantityUnit.SHARES, lots.Unit);
        var shares = Event(PortfolioEventType.BUY, 3, 100, 115, 100);
        var sell = Event(PortfolioEventType.SELL, 4, 400, 120, 400);
        var projection = PortfolioLedger.Project([sell, lots, Deposit, shares], At.AddDays(1), Day);
        var position = Assert.Single(projection.Positions);
        // (131300 + 11600) / 1400 = 102.071428... weighted average.
        Assert.Equal(1000, position.Shares);
        Assert.InRange(Math.Abs(142900m * 1000 / 1400 - position.InvestedCost), 0, 0.00000000000000000001m);
        Assert.Equal(47600m - 142900m * 400 / 1400, position.RealizedPnl);
        Assert.Equal(1000000m - 142900m + 47600m, projection.Cash);
        Assert.Equal(-142900m + 47600m, position.CashImpact);
    }

    [Fact]
    public void FullCloseAndReopenResetAverageCostWithoutLosingRealizedProfit()
    {
        var projection = PortfolioLedger.Project([Deposit, Event(PortfolioEventType.BUY, 2, 100, 100, 100),
            Event(PortfolioEventType.SELL, 3, 100, 120, 100)], At.AddDays(1), Day);
        var closed = Assert.Single(projection.Positions);
        Assert.Equal(0, closed.InvestedCost); Assert.Null(closed.AverageCost); Assert.Equal(1800, closed.RealizedPnl);
        var reopened = PortfolioLedger.Project([Deposit, Event(PortfolioEventType.BUY, 2, 100, 100, 100),
            Event(PortfolioEventType.SELL, 3, 100, 120, 100), Event(PortfolioEventType.BUY, 4, 20, 80)], At.AddDays(1), Day);
        Assert.Equal(80, Assert.Single(reopened.Positions).AverageCost);
        Assert.Equal(1800, Assert.Single(reopened.Positions).RealizedPnl);
    }

    [Fact]
    public void CorrectionHistoryRespectsKnowledgeCutoffAndReplacesRatherThanAdds()
    {
        var buy = Event(PortfolioEventType.BUY, 2, 1300, 100);
        var correction = buy with { Id = Guid.NewGuid(), KnownAt = At.AddHours(1), Order = 3, Price = 110, Supersedes = buy.Id };
        PortfolioLedger.ValidateAppend([Deposit, buy], correction);
        Assert.Equal(100, Assert.Single(PortfolioLedger.Project([Deposit, buy, correction], At.AddMinutes(1), Day).Positions).AverageCost);
        Assert.Equal(110, Assert.Single(PortfolioLedger.Project([Deposit, buy, correction], At.AddHours(2), Day).Positions).AverageCost);
        Assert.Throws<ArgumentException>(() => PortfolioLedger.ValidateAppend([Deposit, buy, correction], correction with { Id = Guid.NewGuid() }));
        Assert.True(PortfolioLedger.SameFact(buy, buy with { Id = Guid.NewGuid(), KnownAt = At.AddHours(1), Order = 8 }));
        Assert.False(PortfolioLedger.SameFact(buy, buy with { Price = 110 }));
    }

    [Fact]
    public void CorrectedBuyKeepsOriginalEconomicOrderBeforeSameDaySale()
    {
        var buy = Event(PortfolioEventType.BUY, 2, 100, 100);
        var sell = Event(PortfolioEventType.SELL, 3, 40, 120);
        var corrected = buy with { Id = Guid.NewGuid(), Price = 110, Supersedes = buy.Id, Order = 4, KnownAt = At.AddHours(1) };
        var view = PortfolioLedger.Project([Deposit, buy, sell, corrected], At.AddDays(1), Day);
        Assert.Equal(60, Assert.Single(view.Positions).Shares);
        Assert.Equal(110, Assert.Single(view.Positions).AverageCost);
        Assert.Equal(400, Assert.Single(view.Positions).RealizedPnl);
    }

    [Fact]
    public void CashWithdrawalNegativeCashAndOversellingAreExplicit()
    {
        Assert.Equal(79900, PortfolioLedger.Project([Event(PortfolioEventType.CASH_DEPOSIT, 1, cash: 100000),
            Event(PortfolioEventType.CASH_WITHDRAWAL, 2, fees: 100, cash: 20000)], At.AddDays(1), Day).Cash);
        Assert.Throws<ArgumentException>(() => PortfolioLedger.Project([Event(PortfolioEventType.BUY, 1, 10, 100)], At.AddDays(1), Day));
        Assert.Equal(-1000, PortfolioLedger.Project([Event(PortfolioEventType.BUY, 1, 10, 100)], At.AddDays(1), Day, true).Cash);
        Assert.Throws<ArgumentException>(() => PortfolioLedger.Project([Deposit, Event(PortfolioEventType.SELL, 2, 1, 100)], At.AddDays(1), Day));
        Assert.Throws<ArgumentException>(() => Event(PortfolioEventType.BUY, 2, 0.1m, 100));
    }

    [Fact]
    public void ThesisMandateChangeCreatesLinkedHistoryAndExplicitDeactivation()
    {
        var first = PortfolioLedger.NextThesis(PortfolioId, InstrumentId, new(Mandate.FAST_SWING, "Synthetic swing"), null, At);
        var second = PortfolioLedger.NextThesis(PortfolioId, InstrumentId, new(Mandate.INVEST, "Explicit new thesis"), first, At.AddHours(1));
        var third = PortfolioLedger.NextThesis(PortfolioId, InstrumentId, new(Mandate.INVEST, "Invalidated", "Synthetic invalidation", false), second, At.AddHours(2));
        Assert.Equal(Mandate.FAST_SWING, first.Mandate); Assert.Equal(1, first.Version);
        Assert.Equal(first.Id, second.Supersedes); Assert.Equal(2, second.Version); Assert.Equal(3, third.Version); Assert.False(third.Active);
    }

    private static MarketState Market(Guid id, string symbol, decimal? price, MarketQuality quality = MarketQuality.DEGRADED) =>
        new(id, symbol, "EQUITY", price is null ? null : Day.AddDays(-1), price is null ? null : At,
            price is null ? null : At, At.AddDays(1), price is null ? null : 1, price is null ? Freshness.UNKNOWN : Freshness.STALE,
            Completeness.UNKNOWN, quality, "SYNTHETIC", "RAW_CLOSE", price,
            ProductValuation.UnprojectedFeatures(), price is null ? "NO_CURRENT_MARKET_PRICE" : null);

    [Fact]
    public void OutsideUniverseHoldingRemainsPresentAndMakesTotalsIncomplete()
    {
        var outside = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var projection = new LedgerProjection(500, [new(InstrumentId, 100, 10000, 0, -10000, Day), new(outside, 50, 3000, 0, -3000, Day)]);
        var view = ProductValuation.Assemble(new(PortfolioId, "SYNTHETIC", false, At), projection,
            new Dictionary<Guid, MarketState> { [InstrumentId] = Market(InstrumentId, "BBCA", 120), [outside] = Market(outside, "XYZ", null) },
            new Dictionary<Guid, ThesisVersion>(), Day, At.AddDays(1));
        Assert.Equal(2, view.HoldingCount); Assert.Equal(1, view.PricedHoldings); Assert.Equal(Completeness.PARTIAL, view.ValuationCoverage);
        Assert.Null(view.TotalMarketValue); Assert.Null(view.TotalEquity); Assert.Equal(12000, view.PricedMarketValue);
        var missing = view.Holdings.Single(h => h.Position.InstrumentId == outside);
        Assert.Equal(50, missing.Position.Shares); Assert.Equal(60, missing.Position.AverageCost); Assert.Null(missing.Valuation.MarketValue);
        Assert.Equal("NO_CURRENT_MARKET_PRICE", missing.Valuation.UnavailableReason);
        Assert.Equal(OperationStatus.SUCCESS, view.Operation); Assert.Equal(MarketQuality.DEGRADED, view.Holdings[0].Market.Quality);
        Assert.Equal(Freshness.STALE, view.Holdings[0].Market.Freshness);
        Assert.Equal(Availability.UNAVAILABLE, view.Holdings[0].Market.Features["ATR14"].Availability);
        Assert.Equal("BBCA", view.Holdings[0].Market.DisplaySymbol);
        Assert.Equal("XYZ", view.Holdings[1].Market.DisplaySymbol);
    }

    [Fact]
    public void RejectedAndFuturePricesDoNotValueHoldings()
    {
        var position = new Position(InstrumentId, 100, 10000, 0, 0, Day);
        Assert.Equal(Availability.UNAVAILABLE, ProductValuation.Value(position, Market(InstrumentId, "BBCA", 120, MarketQuality.REJECTED), Day).Availability);
        Assert.Equal("PRICE_AFTER_VALUATION_DATE", ProductValuation.Value(position, Market(InstrumentId, "BBCA", 120) with { MarketDate = Day.AddDays(1) }, Day).UnavailableReason);
        Assert.Equal("PRICE_AFTER_KNOWLEDGE_CUTOFF", ProductValuation.Value(position, Market(InstrumentId, "BBCA", 120) with { KnownAt = At.AddDays(2) }, Day).UnavailableReason);
    }

    [Fact]
    public void WarmupReadinessDoesNotChangeSuccessfulOperationOrPriceValuation()
    {
        var market = Market(InstrumentId, "SYNTHETIC", 120) with
        {
            Features = new Dictionary<string, FeatureState> { ["ATR14"] = new(Availability.WARMUP, null, "INSUFFICIENT_SESSIONS") }
        };
        var view = ProductValuation.Assemble(new(PortfolioId, "SYNTHETIC", false, At),
            new(500, [new(InstrumentId, 100, 10000, 0, 0, Day)]),
            new Dictionary<Guid, MarketState> { [InstrumentId] = market }, new Dictionary<Guid, ThesisVersion>(), Day, At.AddDays(1));
        Assert.Equal(OperationStatus.SUCCESS, view.Operation);
        Assert.Equal(Availability.WARMUP, view.Holdings[0].Market.Features["ATR14"].Availability);
        Assert.Equal(Availability.AVAILABLE, view.Holdings[0].Valuation.Availability);
    }

    [Fact]
    public async Task AdapterHonorsCancellationBeforeOpeningConnection()
    {
        await using var source = Npgsql.NpgsqlDataSource.Create("Host=127.0.0.1;Database=synthetic_cancellation;Username=synthetic;Timeout=1");
        var db = new PortfolioDatabase(source);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.MarketAsync(InstrumentId, Day, At, cancel.Token));
    }

    [Fact]
    public void AdapterAndQueriesEnforceBounds()
    {
        Assert.Throws<ArgumentException>(() => PortfolioDatabase.ValidatePage(-1, 10));
        Assert.Throws<ArgumentException>(() => PortfolioDatabase.ValidatePage(0, 201));
        Assert.Throws<ArgumentException>(() => ProductQuery.Date(ProductQuery.Today.AddDays(1)));
        Assert.Throws<ArgumentException>(() => ProductQuery.Cutoff(DateTimeOffset.UtcNow.AddDays(1)));
    }
}
