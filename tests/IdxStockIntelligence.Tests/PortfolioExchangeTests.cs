using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class PortfolioExchangeTests
{
    private static readonly Guid PortfolioId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid StockId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static PortfolioDocument Fixture()
    {
        var deposit = PortfolioLedger.Canonicalize(PortfolioId, new(Guid.NewGuid(), PortfolioEventType.CASH_DEPOSIT, null,
            new(2026, 9, 1), CashAmount: 1000000), Start.AddDays(1), 10);
        var buy = PortfolioLedger.Canonicalize(PortfolioId, new(Guid.NewGuid(), PortfolioEventType.BUY, StockId,
            new(2026, 9, 10), 13, QuantityUnit.LOTS, 100, 1300, ExternalReference: "synthetic"), Start.AddDays(10), 20);
        var correction = buy with { Id = Guid.NewGuid(), Order = 30, KnownAt = Start.AddDays(19), Price = 110,
            ExternalReference = "synthetic-correction", Supersedes = buy.Id };
        var first = PortfolioLedger.NextThesis(PortfolioId, StockId, new(Mandate.FAST_SWING, "Synthetic first"), null, Start.AddDays(11));
        var second = PortfolioLedger.NextThesis(PortfolioId, StockId, new(Mandate.INVEST, "Synthetic change"), first, Start.AddDays(24));
        return new(1, new(PortfolioId, "SYNTHETIC TEST ONLY", false, Start), [deposit, buy, correction], [first, second],
            [new(StockId, "SYNTHETIC equity", "EQUITY", null, null, [new("SYNTHETIC.JK", new(2000, 1, 1), null)])]);
    }

    [Fact]
    public void JsonExportIsStableLosslessAndIndependentOfCollectionOrdering()
    {
        var document = Fixture();
        var exported = PortfolioExchange.Serialize(document);
        var reversed = document with { Events = document.Events.Reverse().ToArray(), Theses = document.Theses.Reverse().ToArray() };
        Assert.Equal(exported, PortfolioExchange.Serialize(reversed));
        var parsed = PortfolioExchange.Parse(exported);
        PortfolioExchange.Validate(parsed, Now, TestContext.Current.CancellationToken);
        Assert.Equal(exported, PortfolioExchange.Serialize(parsed));
        Assert.Equal(PortfolioLedger.Project(document.Events, Now, new(2026, 9, 30)).Cash,
            PortfolioLedger.Project(parsed.Events, Now, new(2026, 9, 30)).Cash);
        Assert.Equal(document.Events.Select(e => e.Id), parsed.Events.Select(e => e.Id));
        Assert.Equal(document.Theses.Select(t => t.Id), parsed.Theses.Select(t => t.Id));
        Assert.DoesNotContain("connectionString", exported, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw_artifact", exported, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TradeDateAndRecordedKnowledgeRemainSeparateAcrossRestore()
    {
        var document = PortfolioExchange.Parse(PortfolioExchange.Serialize(Fixture()));
        Assert.Empty(PortfolioLedger.Project(document.Events, Start.AddDays(9), new(2026, 9, 30)).Positions);
        Assert.Empty(PortfolioLedger.Project(document.Events, Now, new(2026, 9, 9)).Positions);
        Assert.Equal(101, Assert.Single(PortfolioLedger.Project(document.Events, Start.AddDays(14), new(2026, 9, 30)).Positions).AverageCost);
        Assert.Equal(111, Assert.Single(PortfolioLedger.Project(document.Events, Now, new(2026, 9, 30)).Positions).AverageCost);
        Assert.Equal(Mandate.FAST_SWING, document.Theses.Where(t => t.KnownAt < Start.AddDays(24)).MaxBy(t => t.Version)!.Mandate);
        Assert.Equal(Mandate.INVEST, document.Theses.MaxBy(t => t.Version)!.Mandate);
    }

    [Fact]
    public void UnsupportedIncompleteDuplicateJsonAndBoundsFailExplicitly()
    {
        var document = Fixture();
        Assert.Throws<ArgumentException>(() => PortfolioExchange.Parse(PortfolioExchange.Serialize(document).Replace("\"schemaVersion\":1", "\"schemaVersion\":2")));
        Assert.Throws<ArgumentException>(() => PortfolioExchange.Parse("{\"schemaVersion\":1,\"schemaVersion\":1}"));
        Assert.Throws<JsonException>(() => PortfolioExchange.Parse("{\"schemaVersion\":1}"));
        Assert.Throws<ArgumentException>(() => PortfolioExchange.Parse("[]"));
        Assert.Throws<ArgumentException>(() => PortfolioExchange.Parse("{\"schemaVersion\":\"1\"}"));
        Assert.Throws<ArgumentException>(() => PortfolioExchange.Validate(document with { Events = [null!] }, Now, TestContext.Current.CancellationToken));
        var overlapping = document.Instruments[0] with { Symbols = [new("SYNTHETIC.JK", new(2000, 1, 1), null), new("SYNTHETIC.JK", new(2020, 1, 1), null)] };
        Assert.Throws<ArgumentException>(() => PortfolioExchange.Validate(document with { Instruments = [overlapping] }, Now, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => PortfolioExchange.CheckSize(new string('x', PortfolioExchange.MaxFileBytes + 1)));
        Assert.Throws<ArgumentException>(() => PortfolioExchange.Validate(document with { Theses = Enumerable.Repeat(document.Theses[0], PortfolioExchange.MaxTheses + 1).ToArray() }, Now, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => PortfolioExchange.Validate(document with { Events = [document.Events[0], document.Events[1] with { Unit = QuantityUnit.LOTS }] }, Now, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void InvalidPastCashCannotBeHiddenByLaterBackdatedDeposit()
    {
        var document = Fixture();
        var buy = document.Events[1] with { Order = 1 };
        var laterDeposit = document.Events[0] with { KnownAt = buy.KnownAt.AddDays(1), Order = 2 };
        Assert.Throws<ArgumentException>(() => PortfolioExchange.Validate(document with { Events = [buy, laterDeposit], Theses = [] }, Now, TestContext.Current.CancellationToken));
        // Atomic equal-timestamp batch has one knowledge boundary.
        PortfolioExchange.Validate(document with { Events = [buy, laterDeposit with { KnownAt = buy.KnownAt }], Theses = [] }, Now, TestContext.Current.CancellationToken);
    }

    private const string Header = "trade_date,type,instrument_id,symbol,quantity,unit,price,fees,cash_amount,external_reference,note\n";
    [Fact]
    public void CsvUsesExistingCanonicalizerExplicitUnitsAndRepeatableIds()
    {
        var doc = Fixture();
        var csv = Header + $"2026-09-10,BUY,{StockId},,13,LOTS,100,1300,0,synthetic-csv,\"comma, quote \"\" and newline\ntext\"\n";
        var row = Assert.Single(PortfolioExchange.ParseCsv(csv, PortfolioId, doc.Instruments, Now, 0, TestContext.Current.CancellationToken));
        Assert.Null(row.Error); Assert.Equal(1300, row.Event!.Quantity); Assert.Equal(QuantityUnit.SHARES, row.Event.Unit);
        Assert.Equal("comma, quote \" and newline\ntext", row.Event.Note);
        Assert.Equal(row.Event.Id, Assert.Single(PortfolioExchange.ParseCsv(csv, PortfolioId, doc.Instruments, Now.AddHours(1), 100, TestContext.Current.CancellationToken)).Event!.Id);
        Assert.NotEqual(row.Event.Id, PortfolioExchange.CsvEventId(Guid.NewGuid(), "synthetic-csv"));
        Assert.NotNull(Assert.Single(PortfolioExchange.ParseCsv(csv.Replace(",LOTS,", ",0,"), PortfolioId, doc.Instruments, Now, 0, TestContext.Current.CancellationToken)).Error);
        var unknown = Assert.Single(PortfolioExchange.ParseCsv(csv.Replace(StockId.ToString(), Guid.NewGuid().ToString()), PortfolioId, doc.Instruments, Now, 0, TestContext.Current.CancellationToken));
        Assert.StartsWith("UNKNOWN_INSTRUMENT:", unknown.Error);
        Assert.NotNull(Assert.Single(PortfolioExchange.ParseCsv(Header + "2026-09-10,BUY,\"unterminated", PortfolioId, doc.Instruments, Now, 0, TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public void CsvFormulaSafeExportIsConvenienceOnlyAndRejectsCorrections()
    {
        var doc = Fixture();
        Assert.Throws<ArgumentException>(() => PortfolioExchange.CsvExport(doc));
        foreach (var note in new[] { "=SUM(A1)", "+cmd", "-cmd", "@cmd", "  =cmd", "\tcmd", "\rcmd", "\ncmd" })
        {
            var buy = doc.Events[1] with { Note = note, ExternalReference = "=ref" };
            var csv = PortfolioExchange.CsvExport(doc with { Events = [doc.Events[0], buy], Theses = [] });
            Assert.Contains("\"'" + note.Replace("\"", "\"\"") + "\"", csv);
            Assert.Contains("\"'=ref\"", csv);
        }
    }
}
