using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

#pragma warning disable CA1707
public enum ReconciliationStatus { MATCH, REVIEW, MISSING_FROM_LEDGER, MISSING_FROM_EXPECTED, UNKNOWN_INSTRUMENT }
#pragma warning restore CA1707
public sealed record ExpectedHolding(Guid? InstrumentId, string? Symbol, decimal Shares, decimal? AverageCost);
public sealed record ReconciliationInput(DateOnly Through, DateTimeOffset Cutoff, decimal ExpectedCash,
    IReadOnlyList<ExpectedHolding> Holdings);
public sealed record ReconciliationRow(Guid? InstrumentId, string Symbol, decimal? LedgerShares,
    decimal? ExpectedShares, decimal? ShareDifference, decimal? LedgerAverageCost,
    decimal? ExpectedAverageCost, decimal? AverageCostDifference, ReconciliationStatus Status);
public sealed record ReconciliationPreview(DateOnly Through, DateTimeOffset KnowledgeCutoff,
    ReconciliationStatus Status, decimal LedgerCash, decimal ExpectedCash, decimal CashDifference,
    int ExpectedHoldings, int LedgerHoldings, int Matched, int Review, int Missing, int Unknown,
    IReadOnlyList<ReconciliationRow> Rows);

public static class PortfolioReconciliation
{
    public static void Validate(ReconciliationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ProductQuery.Date(input.Through);
        ProductQuery.Cutoff(input.Cutoff);
        if (input.ExpectedCash is < -1_000_000_000_000m or > 1_000_000_000_000m || input.Holdings is null
            || input.Holdings.Count > PortfolioLedger.MaximumOpenHoldings)
            throw new ArgumentException("Expected cash must be bounded by 10^12; at most 200 holdings.");
        foreach (var h in input.Holdings)
            if (h is null || h.InstrumentId == Guid.Empty || h.InstrumentId is null && string.IsNullOrWhiteSpace(h.Symbol)
                || h.Symbol?.Length > 50 || h.Shares <= 0 || h.Shares > 1_000_000_000m
                || h.Shares != decimal.Truncate(h.Shares) || h.AverageCost is < 0 or > 1_000_000_000_000m)
                throw new ArgumentException("Each holding needs an identity/symbol, positive integral shares and a bounded nonnegative average cost or null.");
    }

    public static ReconciliationPreview Compare(LedgerProjection ledger, ReconciliationInput input,
        IReadOnlyList<InstrumentReference> references, CancellationToken ct = default)
    {
        Validate(input);
        ct.ThrowIfCancellationRequested();
        var positions = ledger.Positions.Where(p => p.Shares > 0).ToDictionary(p => p.InstrumentId);
        var seen = new HashSet<Guid>();
        var unknown = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<ReconciliationRow>();
        string Display(InstrumentReference i) => i.Symbols.LastOrDefault(s => s.ValidFrom <= input.Through
            && (s.ValidTo is null || s.ValidTo >= input.Through))?.Symbol ?? i.IssuerName;
        foreach (var h in input.Holdings)
        {
            ct.ThrowIfCancellationRequested();
            var symbol = string.IsNullOrWhiteSpace(h.Symbol) ? null : h.Symbol.Trim().ToUpperInvariant();
            var byId = h.InstrumentId is { } id ? references.SingleOrDefault(i => i.Id == id && i.Type == "EQUITY") : null;
            var matches = string.IsNullOrEmpty(symbol) ? [] : references.Where(i => i.Type == "EQUITY"
                && i.Symbols.Any(s => s.Symbol == symbol && s.ValidFrom <= input.Through
                    && (s.ValidTo is null || s.ValidTo >= input.Through))).ToArray();
            if (matches.Length > 1) throw new ArgumentException("Symbol resolves to multiple identities; supply an unambiguous stable ID.");
            var bySymbol = matches.SingleOrDefault();
            if (h.InstrumentId is not null && !string.IsNullOrEmpty(symbol) && (byId is null || bySymbol?.Id != byId.Id))
                throw new ArgumentException("Instrument ID and effective symbol must resolve to the same equity identity.");
            var resolved = h.InstrumentId is not null ? byId : bySymbol;
            if (resolved is null)
            {
                if (!unknown.Add(h.InstrumentId?.ToString() ?? symbol!)) throw new ArgumentException("Duplicate expected identity/symbol.");
                rows.Add(new(h.InstrumentId, symbol ?? h.InstrumentId!.Value.ToString(), null, h.Shares, null,
                    null, h.AverageCost, null, ReconciliationStatus.UNKNOWN_INSTRUMENT));
                continue;
            }
            if (!seen.Add(resolved.Id)) throw new ArgumentException("Duplicate expected instrument identity.");
            positions.Remove(resolved.Id, out var p);
            var status = p is null ? ReconciliationStatus.MISSING_FROM_LEDGER
                : p.Shares == h.Shares && h.AverageCost is not null && p.AverageCost == h.AverageCost
                    ? ReconciliationStatus.MATCH : ReconciliationStatus.REVIEW;
            rows.Add(new(resolved.Id, Display(resolved), p?.Shares, h.Shares, p?.Shares - h.Shares,
                p?.AverageCost, h.AverageCost, p?.AverageCost - h.AverageCost, status));
        }
        foreach (var p in positions.Values)
        {
            ct.ThrowIfCancellationRequested();
            var reference = references.Single(i => i.Id == p.InstrumentId);
            rows.Add(new(p.InstrumentId, Display(reference), p.Shares, null, null, p.AverageCost,
                null, null, ReconciliationStatus.MISSING_FROM_EXPECTED));
        }
        var ordered = rows.OrderBy(r => r.Symbol, StringComparer.Ordinal).ThenBy(r => r.InstrumentId).ToArray();
        var matched = rows.Count(r => r.Status == ReconciliationStatus.MATCH);
        var review = rows.Count - matched;
        var missing = rows.Count(r => r.Status is ReconciliationStatus.MISSING_FROM_LEDGER or ReconciliationStatus.MISSING_FROM_EXPECTED);
        var cashDifference = ledger.Cash - input.ExpectedCash;
        return new(input.Through, input.Cutoff.ToUniversalTime(), review == 0 && cashDifference == 0
            ? ReconciliationStatus.MATCH : ReconciliationStatus.REVIEW, ledger.Cash, input.ExpectedCash,
            cashDifference, input.Holdings.Count, ledger.Positions.Count(p => p.Shares > 0), matched, review,
            missing, rows.Count(r => r.Status == ReconciliationStatus.UNKNOWN_INSTRUMENT), ordered);
    }
}
