using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record Position(Guid InstrumentId, decimal Shares, decimal InvestedCost, decimal RealizedPnl,
    decimal CashImpact, DateOnly LatestEventDate)
{
    public decimal? AverageCost => Shares == 0 ? null : InvestedCost / Shares;
}
public sealed record LedgerProjection(decimal Cash, IReadOnlyList<Position> Positions);

public static class PortfolioLedger
{
    public const int MaximumEvents = 10000;
    public const int MaximumOpenHoldings = 200;
    public static PortfolioEvent Canonicalize(Guid portfolioId, EventInput input, DateTimeOffset knownAt, long order)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (portfolioId == Guid.Empty || input.EventId == Guid.Empty || !Enum.IsDefined(input.Type) || !Enum.IsDefined(input.Unit))
            throw new ArgumentException("Valid portfolio/event identity, event type and unit required.");
        if (string.IsNullOrWhiteSpace(input.Source) || input.Source.Length > 100 || input.Note?.Length > 4000
            || input.ExternalReference?.Length > 200 || input.ExternalReference is not null && string.IsNullOrWhiteSpace(input.ExternalReference))
            throw new ArgumentException("Source/reference/note invalid or too long.");
        if (input.TradeDate < new DateOnly(1900, 1, 1) || input.TradeDate > DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(knownAt, "Asia/Jakarta").DateTime))
            throw new ArgumentException("Trade date must be 1900-01-01..today (Jakarta).");
        if (new[] { input.Quantity, input.Price, input.Fees, input.CashAmount }.Any(v => v < 0 || v > 1_000_000_000_000m))
            throw new ArgumentException("Amounts must be nonnegative and bounded by 10^12.");
        var trade = input.Type is PortfolioEventType.BUY or PortfolioEventType.SELL;
        var shares = checked(input.Quantity * (input.Unit == QuantityUnit.LOTS ? 100 : 1));
        if (trade && (input.InstrumentId is null || input.InstrumentId == Guid.Empty || shares <= 0
            || shares > 1_000_000_000 || shares != decimal.Truncate(shares) || input.Price <= 0 || input.CashAmount != 0))
            throw new ArgumentException("Equity trades require integral shares, positive price and no cash amount.");
        if (!trade && (input.InstrumentId is not null || input.Quantity != 0 || input.Price != 0
            || input.CashAmount <= 0 || input.Unit != QuantityUnit.SHARES))
            throw new ArgumentException("Cash events require only cash amount/fees, with no instrument or quantity.");
        if (input.Type is PortfolioEventType.CASH_DEPOSIT && input.Fees > input.CashAmount)
            throw new ArgumentException("Deposit fees exceed deposit.");
        return new(input.EventId, portfolioId, input.Type, input.InstrumentId, input.TradeDate, knownAt, order,
            shares, QuantityUnit.SHARES, input.Price, input.Fees, input.CashAmount, input.ExternalReference,
            input.Source.Trim(), input.Note, input.Supersedes);
    }

    public static bool SameFact(PortfolioEvent a, PortfolioEvent b) =>
        a with { Id = b.Id, KnownAt = b.KnownAt, Order = b.Order } == b;

    public static void ValidateAppend(IReadOnlyList<PortfolioEvent> history, PortfolioEvent item)
    {
        if (history.Count >= MaximumEvents) throw new InvalidOperationException("Ledger limit reached; archive/checkpoint support required.");
        if (item.Supersedes is { } target)
        {
            var original = history.SingleOrDefault(e => e.Id == target);
            if (original is null || original.PortfolioId != item.PortfolioId || original.InstrumentId != item.InstrumentId
                || history.Any(e => e.Supersedes == target) || item.Id == target || original.KnownAt > item.KnownAt)
                throw new ArgumentException("Correction must replace the current event for the same portfolio/instrument.");
        }
    }

    public static LedgerProjection Project(IEnumerable<PortfolioEvent> events, DateTimeOffset cutoff, DateOnly through,
        bool allowNegativeCash = false)
    {
        var history = events.Where(e => e.KnownAt <= cutoff).ToArray();
        if (history.Length > MaximumEvents) throw new InvalidOperationException("Ledger exceeds supported bound.");
        var replaced = history.Where(e => e.Supersedes is not null).Select(e => e.Supersedes!.Value).ToHashSet();
        var byId = history.ToDictionary(e => e.Id);
        long EconomicOrder(PortfolioEvent item)
        {
            var seen = new HashSet<Guid>();
            while (item.Supersedes is { } target)
            {
                if (!seen.Add(item.Id) || !byId.TryGetValue(target, out var prior))
                    throw new ArgumentException("Invalid correction chain.");
                item = prior;
            }
            return item.Order;
        }
        var positions = new Dictionary<Guid, Position>();
        decimal cash = 0;
        foreach (var e in history.Where(e => !replaced.Contains(e.Id) && e.TradeDate <= through)
            .OrderBy(e => e.TradeDate).ThenBy(EconomicOrder).ThenBy(e => e.Id))
        {
            if (e.Unit != QuantityUnit.SHARES) throw new ArgumentException("Ledger quantities must be SHARES.");
            decimal impact;
            if (e.Type is PortfolioEventType.CASH_DEPOSIT or PortfolioEventType.CASH_WITHDRAWAL)
                impact = (e.Type == PortfolioEventType.CASH_DEPOSIT ? e.CashAmount : -e.CashAmount) - e.Fees;
            else
            {
                var id = e.InstrumentId ?? throw new ArgumentException("Trade instrument required.");
                var p = positions.GetValueOrDefault(id) ?? new(id, 0, 0, 0, 0, e.TradeDate);
                if (e.Type == PortfolioEventType.BUY)
                {
                    impact = -(e.Quantity * e.Price + e.Fees);
                    p = p with { Shares = p.Shares + e.Quantity, InvestedCost = p.InvestedCost - impact };
                }
                else
                {
                    if (e.Quantity > p.Shares) throw new ArgumentException("SELL exceeds shares held; short positions unsupported.");
                    var removedCost = e.Quantity == p.Shares ? p.InvestedCost : p.InvestedCost * e.Quantity / p.Shares;
                    impact = e.Quantity * e.Price - e.Fees;
                    p = p with { Shares = p.Shares - e.Quantity, InvestedCost = p.InvestedCost - removedCost,
                        RealizedPnl = p.RealizedPnl + impact - removedCost };
                }
                positions[id] = p with { CashImpact = p.CashImpact + impact, LatestEventDate = e.TradeDate };
            }
            cash += impact;
            if (!allowNegativeCash && cash < 0) throw new ArgumentException("Insufficient cash at event date.");
        }
        if (positions.Values.Count(p => p.Shares > 0) > MaximumOpenHoldings)
            throw new InvalidOperationException("V0.1 supports at most 200 open holdings per portfolio.");
        return new(cash, positions.Values.OrderBy(p => p.InstrumentId).ToArray());
    }

    public static ThesisVersion NextThesis(Guid portfolioId, Guid instrumentId, ThesisInput input,
        ThesisVersion? previous, DateTimeOffset knownAt)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(input.Mandate) || string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 8000
            || input.InvalidationNote?.Length > 4000 || portfolioId == Guid.Empty || instrumentId == Guid.Empty)
            throw new ArgumentException("Valid mandate, thesis and identities required.");
        return new(Guid.NewGuid(), portfolioId, instrumentId, (previous?.Version ?? 0) + 1,
            input.Mandate, input.Text.Trim(), knownAt, previous?.Id, input.InvalidationNote, input.Active);
    }
}
