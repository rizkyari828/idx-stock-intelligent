using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public enum OperationStatus { SUCCESS, FAILED }
public enum Freshness { CURRENT, STALE, UNKNOWN }
public enum Completeness { COMPLETE, PARTIAL, UNKNOWN }
public enum MarketQuality { VERIFIED, DEGRADED, REJECTED, UNKNOWN }
public enum Availability { AVAILABLE, UNAVAILABLE, WARMUP }
public sealed record FeatureState(Availability Availability, decimal? Value, string? UnavailableReason);
public sealed record MarketState(Guid InstrumentId, string DisplaySymbol, string InstrumentType,
    DateOnly? MarketDate, DateTimeOffset? RetrievedAt, DateTimeOffset? KnownAt, DateTimeOffset KnowledgeCutoff,
    long? Revision, Freshness Freshness, Completeness Completeness, MarketQuality Quality,
    string? Source, string PriceBasis, decimal? Price, IReadOnlyDictionary<string, FeatureState> Features,
    string? UnavailableReason);
public sealed record Valuation(DateOnly? Date, decimal? Price, int? PriceAgeDays, decimal? MarketValue,
    decimal? UnrealizedPnl, Availability Availability, MarketQuality Quality, string? UnavailableReason);
public sealed record Holding(Position Position, MarketState Market, Valuation Valuation, ThesisVersion? ActiveThesis);
public sealed record PortfolioView(Portfolio Portfolio, decimal Cash, bool NegativeCash, IReadOnlyList<Holding> Holdings,
    int PricedHoldings, int HoldingCount, Completeness ValuationCoverage, decimal PricedMarketValue,
    decimal? TotalMarketValue, decimal? TotalEquity, DateOnly Through, DateTimeOffset KnowledgeCutoff,
    OperationStatus Operation = OperationStatus.SUCCESS);

public static class ProductValuation
{
    public static Valuation Value(Position position, MarketState market, DateOnly through)
    {
        var reason = market.UnavailableReason;
        if (market.Price is null || market.MarketDate is null) reason ??= "NO_CURRENT_MARKET_PRICE";
        if (market.MarketDate > through) reason = "PRICE_AFTER_VALUATION_DATE";
        if (market.KnownAt > market.KnowledgeCutoff) reason = "PRICE_AFTER_KNOWLEDGE_CUTOFF";
        if (market.Quality is MarketQuality.REJECTED or MarketQuality.UNKNOWN) reason ??= "PRICE_QUALITY_UNAVAILABLE";
        if (reason is not null) return new(market.MarketDate, null, null, null, null, Availability.UNAVAILABLE, market.Quality, reason);
        var value = position.Shares * market.Price!.Value;
        return new(market.MarketDate, market.Price, through.DayNumber - market.MarketDate!.Value.DayNumber,
            value, value - position.InvestedCost, Availability.AVAILABLE, market.Quality, null);
    }

    public static PortfolioView Assemble(Portfolio portfolio, LedgerProjection projection,
        IReadOnlyDictionary<Guid, MarketState> markets, IReadOnlyDictionary<Guid, ThesisVersion> theses,
        DateOnly through, DateTimeOffset cutoff)
    {
        var holdings = projection.Positions.Where(p => p.Shares > 0).Select(p =>
        {
            var market = markets[p.InstrumentId];
            theses.TryGetValue(p.InstrumentId, out var thesis);
            return new Holding(p, market, Value(p, market, through), thesis?.Active == true ? thesis : null);
        }).OrderBy(h => h.Market.DisplaySymbol, StringComparer.Ordinal).ThenBy(h => h.Position.InstrumentId).ToArray();
        var priced = holdings.Count(h => h.Valuation.Availability == Availability.AVAILABLE);
        var subtotal = holdings.Sum(h => h.Valuation.MarketValue ?? 0);
        var complete = priced == holdings.Length;
        return new(portfolio, projection.Cash, projection.Cash < 0, holdings, priced, holdings.Length,
            complete ? Completeness.COMPLETE : Completeness.PARTIAL, subtotal, complete ? subtotal : null,
            complete ? projection.Cash + subtotal : null, through, cutoff);
    }

    private static readonly string[] FeatureNames = ["EMA20", "EMA50", "ATR14", "PRIOR_HIGH20", "PRIOR_LOW20", "VOLUME_RATIO20", "RELATIVE_PERFORMANCE20"];
    public static IReadOnlyDictionary<string, FeatureState> UnprojectedFeatures() =>
        FeatureNames.ToDictionary(name => name, _ => new FeatureState(Availability.UNAVAILABLE, null, "NOT_PROJECTED_IN_PORTFOLIO_SLICE"));
}
