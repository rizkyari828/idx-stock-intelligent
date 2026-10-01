using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;
using Xunit;
namespace IdxStockIntelligence.Tests;
public sealed class ReconciliationTests
{
    private static readonly Guid PortfolioId = Guid.NewGuid();
    private static readonly Guid Equity = Guid.NewGuid();
    private static readonly Guid Outside = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 9, 20);
    private static readonly DateTimeOffset At = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
    private static readonly InstrumentReference[] References = [new(Equity, "Equity", "EQUITY", null, null,
        [new("OLD", new(2000, 1, 1), new(2026, 9, 19)), new("TEST", Day, null)]),
        new(Outside, "Outside", "EQUITY", null, null, [new("OUTSIDE", new(2000, 1, 1), null)])];
    private static readonly LedgerProjection Ledger = new(1000, [new(Equity, 100, 10000, 0, -10000, Day)]);
    private static ReconciliationInput Input(decimal cash = 1000, decimal shares = 100, decimal? average = 100) =>
        new(Day, At, cash, [new(Equity, "TEST", shares, average)]);
    [Theory]
    [InlineData(1000, 100, 100, ReconciliationStatus.MATCH)]
    [InlineData(999, 100, 100, ReconciliationStatus.REVIEW)]
    [InlineData(1000, 99, 100, ReconciliationStatus.REVIEW)]
    [InlineData(1000, 100, 99, ReconciliationStatus.REVIEW)]
    public void ExactCashSharesAndCostComparisons(decimal cash, decimal shares, decimal average, ReconciliationStatus status)
    {
        var result = PortfolioReconciliation.Compare(Ledger, Input(cash, shares, average), References, TestContext.Current.CancellationToken);
        Assert.Equal(status, result.Status);
        Assert.Equal(1000 - cash, result.CashDifference);
        Assert.Equal(100 - shares, result.Rows[0].ShareDifference);
        Assert.Equal(100 - average, result.Rows[0].AverageCostDifference);
    }
    [Fact]
    public void MissingSourcesAndUnknownAreDistinctAndNeverZero()
    {
        var request = Input() with { Holdings = [new(null, "OUTSIDE", 10, 5), new(null, "UNKNOWN", 10, null)] };
        var result = PortfolioReconciliation.Compare(Ledger, request, References, TestContext.Current.CancellationToken);
        Assert.Equal(3, result.Review); Assert.Equal(2, result.Missing); Assert.Equal(1, result.Unknown);
        var expectedOnly = result.Rows.Single(r => r.Symbol == "OUTSIDE");
        Assert.Equal(ReconciliationStatus.MISSING_FROM_LEDGER, expectedOnly.Status);
        Assert.Null(expectedOnly.LedgerShares); Assert.Null(expectedOnly.ShareDifference);
        var ledgerOnly = result.Rows.Single(r => r.Symbol == "TEST");
        Assert.Equal(ReconciliationStatus.MISSING_FROM_EXPECTED, ledgerOnly.Status);
        Assert.Null(ledgerOnly.ExpectedShares); Assert.Null(ledgerOnly.AverageCostDifference);
        Assert.Equal(ReconciliationStatus.UNKNOWN_INSTRUMENT, result.Rows.Single(r => r.Symbol == "UNKNOWN").Status);
    }
    [Fact]
    public void IdentityResolutionUsesEffectiveSymbolAndRejectsMismatchAndDuplicates()
    {
        Assert.Equal(ReconciliationStatus.MATCH, PortfolioReconciliation.Compare(Ledger,
            Input() with { Holdings = [new(null, " test ", 100, 100)] }, References, TestContext.Current.CancellationToken).Status);
        Assert.Equal(ReconciliationStatus.UNKNOWN_INSTRUMENT, PortfolioReconciliation.Compare(new(0, []),
            new(Day, At, 0, [new(null, "OLD", 100, 100)]), References, TestContext.Current.CancellationToken).Rows[0].Status);
        Assert.Throws<ArgumentException>(() => PortfolioReconciliation.Compare(Ledger,
            Input() with { Holdings = [new(Equity, "OUTSIDE", 100, 100)] }, References, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => PortfolioReconciliation.Compare(Ledger,
            Input() with { Holdings = [new(Equity, null, 100, 100), new(null, "TEST", 100, 100)] }, References, TestContext.Current.CancellationToken));
        Assert.Equal(ReconciliationStatus.UNKNOWN_INSTRUMENT, PortfolioReconciliation.Compare(new(0, []),
            new(Day, At, 0, [new(Guid.NewGuid(), null, 100, 100)]), References, TestContext.Current.CancellationToken).Rows[0].Status);
    }
    [Fact]
    public void ThroughAndCutoffRemainIndependentAcrossImmutableCorrection()
    {
        var deposit = PortfolioLedger.Canonicalize(PortfolioId, new(Guid.NewGuid(), PortfolioEventType.CASH_DEPOSIT,
            null, Day.AddDays(-1), CashAmount: 20000), At, 1);
        var buy = PortfolioLedger.Canonicalize(PortfolioId, new(Guid.NewGuid(), PortfolioEventType.BUY,
            Equity, Day, 100, Price: 100), At.AddHours(1), 2);
        var correction = buy with { Id = Guid.NewGuid(), Price = 110, KnownAt = At.AddDays(1), Order = 3, Supersedes = buy.Id };
        PortfolioLedger.ValidateAppend([deposit, buy], correction);
        PortfolioEvent[] history = [deposit, buy, correction];
        var before = new ReconciliationInput(Day, At.AddHours(2), 10000, [new(Equity, null, 100, 100)]);
        var after = before with { Cutoff = At.AddDays(2), ExpectedCash = 9000, Holdings = [new(Equity, null, 100, 110)] };
        foreach (var request in new[] { before, after })
            Assert.Equal(ReconciliationStatus.MATCH, PortfolioReconciliation.Compare(
                PortfolioLedger.Project(history, request.Cutoff, request.Through), request, References, TestContext.Current.CancellationToken).Status);
        var earlier = after with { Through = Day.AddDays(-1), ExpectedCash = 20000, Holdings = [] };
        Assert.Equal(ReconciliationStatus.MATCH, PortfolioReconciliation.Compare(
            PortfolioLedger.Project(history, earlier.Cutoff, earlier.Through), earlier, References, TestContext.Current.CancellationToken).Status);
        Assert.Equal(100, buy.Price); Assert.Equal(3, history.Length);
    }
    [Fact]
    public void UnpricedHoldingStillMatchesAndTinyCostDifferenceIsNotRoundedAway()
    {
        Assert.Equal(ReconciliationStatus.MATCH, PortfolioReconciliation.Compare(Ledger, Input(), References, TestContext.Current.CancellationToken).Status);
        var tiny = PortfolioReconciliation.Compare(Ledger, Input(average:100.000000000000001m), References, TestContext.Current.CancellationToken);
        Assert.Equal(ReconciliationStatus.REVIEW, tiny.Rows[0].Status); Assert.NotEqual(0, tiny.Rows[0].AverageCostDifference);
        Assert.Equal(ReconciliationStatus.REVIEW, PortfolioReconciliation.Compare(Ledger, Input(average:null), References, TestContext.Current.CancellationToken).Rows[0].Status);
    }
    [Fact]
    public async Task ValidationAndCancellationBoundPreviewBeforeDatabaseAccess()
    {
        Assert.Throws<ArgumentException>(() => PortfolioReconciliation.Validate(Input(shares:0.1m)));
        Assert.Throws<ArgumentException>(() => PortfolioReconciliation.Validate(Input(cash:decimal.MinValue)));
        Assert.Throws<ArgumentException>(() => PortfolioReconciliation.Validate(Input() with { Holdings = Enumerable.Repeat(Input().Holdings[0], 201).ToArray() }));
        Assert.Throws<ArgumentException>(() => PortfolioReconciliation.Validate(Input() with { Through = ProductQuery.Today.AddDays(1) }));
        using var cancel = new CancellationTokenSource();cancel.Cancel();
        await using var source = Npgsql.NpgsqlDataSource.Create("Host=127.0.0.1;Database=synthetic;Username=synthetic;Timeout=1");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PortfolioDatabase(source).ReconcileAsync(PortfolioId, Input(), cancel.Token));
    }
}
