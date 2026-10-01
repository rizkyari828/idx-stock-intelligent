namespace IdxStockIntelligence.Domain;

// Preserve the documented API/SQL vocabulary.
#pragma warning disable CA1707
public enum PortfolioEventType { BUY, SELL, CASH_DEPOSIT, CASH_WITHDRAWAL }
public enum QuantityUnit { SHARES, LOTS }
public enum Mandate { INVEST, FAST_SWING, LONG_SWING }
#pragma warning restore CA1707
public sealed record Portfolio(Guid Id, string Name, bool AllowNegativeCash, DateTimeOffset CreatedAt);

// Input is converted once; ledger quantities always carry SHARES.
public sealed record EventInput(Guid EventId, PortfolioEventType Type, Guid? InstrumentId,
    DateOnly TradeDate, decimal Quantity = 0, QuantityUnit Unit = QuantityUnit.SHARES,
    decimal Price = 0, decimal Fees = 0, decimal CashAmount = 0,
    string? ExternalReference = null, string Source = "USER", string? Note = null, Guid? Supersedes = null);

public sealed record PortfolioEvent(Guid Id, Guid PortfolioId, PortfolioEventType Type, Guid? InstrumentId,
    DateOnly TradeDate, DateTimeOffset KnownAt, long Order, decimal Quantity, QuantityUnit Unit,
    decimal Price, decimal Fees, decimal CashAmount, string? ExternalReference, string Source,
    string? Note, Guid? Supersedes);

public sealed record ThesisInput(Mandate Mandate, string Text, string? InvalidationNote = null, bool Active = true);
public sealed record ThesisVersion(Guid Id, Guid PortfolioId, Guid InstrumentId, int Version,
    Mandate Mandate, string Text, DateTimeOffset KnownAt, Guid? Supersedes,
    string? InvalidationNote, bool Active);
