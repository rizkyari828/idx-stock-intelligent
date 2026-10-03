namespace IdxStockIntelligence.Application;

public sealed record StockIdentity(Guid Id, string Symbol, string Name, string Type,
    bool HasEffectiveSymbol, string Currency = "UNKNOWN", string SymbolSource = "REGISTRY_HISTORY");
public sealed record StockHistoryRow(DateOnly Date, decimal? Open, decimal? High, decimal? Low,
    decimal? Close, long? Volume, string Availability, string? Reason, ScreenerBarEvidence Evidence)
{
    public static StockHistoryRow From(ScreenerBarEvidence evidence)
    {
        var validated = evidence.Validate();
        var bar = validated.Value?.Bar;
        return new(evidence.SessionDate, bar?.Open, bar?.High, bar?.Low, bar?.Close, bar?.Volume,
            validated.Available ? "AVAILABLE" : "UNAVAILABLE", validated.Reason, evidence);
    }
}
public sealed record StockHistory(StockIdentity Registry, DateOnly RegistryAsOf, DateOnly Through,
    DateTimeOffset Cutoff, int Limit, string Order, IReadOnlyList<StockHistoryRow> Rows);
