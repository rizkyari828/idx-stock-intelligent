using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdxStockIntelligence.Domain;
using Microsoft.VisualBasic.FileIO;

namespace IdxStockIntelligence.Application;

#pragma warning disable CA1707 // Public exchange vocabulary.
public enum ImportMode { CREATE_NEW, RESTORE_EXISTING_EMPTY }
public enum ImportStatus { READY, IMPORTED, ALREADY_PRESENT, CONFLICT, INVALID }
#pragma warning restore CA1707
public sealed record SymbolReference(string Symbol, DateOnly ValidFrom, DateOnly? ValidTo);
public sealed record InstrumentReference(Guid Id, string IssuerName, string Type, DateOnly? ListedOn,
    DateOnly? DelistedOn, IReadOnlyList<SymbolReference> Symbols);
public sealed record PortfolioDocument(int SchemaVersion, Portfolio Portfolio, IReadOnlyList<PortfolioEvent> Events,
    IReadOnlyList<ThesisVersion> Theses, IReadOnlyList<InstrumentReference> Instruments);
public sealed record ImportRequest(string Format, string Content, ImportMode Mode, Guid? PortfolioId = null);
public sealed record ImportRow(int Row, PortfolioEvent? Event, string? Error, bool Duplicate = false);
public sealed record ImportPreview(ImportStatus Status, Guid? PortfolioId, int RowsRead, int ValidRows, int InvalidRows,
    int Duplicates, int EstimatedResultingEvents, IReadOnlyList<string> UnknownInstruments,
    IReadOnlyList<string> Errors, IReadOnlyList<ImportRow> Rows)
{
    public bool CanImport => Status is ImportStatus.READY or ImportStatus.ALREADY_PRESENT;
}
public sealed record ImportResult(ImportStatus Status, Guid? PortfolioId, int EventsAdded, int ThesesAdded,
    IReadOnlyList<string> Errors);

public static class PortfolioExchange
{
    public const int MaxFileBytes = 8 * 1024 * 1024;
    public const int MaxTheses = 2000;
    public const int MaxInstruments = 1000;
    public const int MaxSymbols = 10000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static void CheckSize(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (Encoding.UTF8.GetByteCount(content) > MaxFileBytes) throw new ArgumentException("File exceeds 8 MiB.");
    }
    public static PortfolioDocument Parse(string content)
    {
        CheckSize(content);
        // Reject duplicate JSON property names rather than silently accepting the last value.
        using var tree = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicateProperties(tree.RootElement);
        if (tree.RootElement.ValueKind != JsonValueKind.Object || !tree.RootElement.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
            throw new ArgumentException("Unsupported schemaVersion; only 1 is supported.");
        return JsonSerializer.Deserialize<PortfolioDocument>(content, JsonOptions)
            ?? throw new ArgumentException("Portfolio document required.");
    }
    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new ArgumentException("Duplicate JSON property: " + property.Name);
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) RejectDuplicateProperties(child);
    }

    public static PortfolioDocument Ordered(PortfolioDocument document) => document with
    {
        Events = document.Events.OrderBy(e => e.Order).ThenBy(e => e.Id)
            .Select((e, index) => e with { Order = index + 1, KnownAt = e.KnownAt.ToUniversalTime() }).ToArray(),
        Theses = document.Theses.OrderBy(t => t.InstrumentId).ThenBy(t => t.Version)
            .Select(t => t with { KnownAt = t.KnownAt.ToUniversalTime() }).ToArray(),
        Instruments = document.Instruments.OrderBy(i => i.Id).Select(i => i with
        { Symbols = i.Symbols.OrderBy(s => s.ValidFrom).ThenBy(s => s.Symbol, StringComparer.Ordinal).ToArray() }).ToArray(),
        Portfolio = document.Portfolio with { CreatedAt = document.Portfolio.CreatedAt.ToUniversalTime() }
    };
    public static string Serialize(PortfolioDocument document)
    {
        var json = JsonSerializer.Serialize(Ordered(document), JsonOptions);
        CheckSize(json);
        return json;
    }

    private static void Timestamp(DateTimeOffset at, DateTimeOffset now)
    {
        if (at < new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero) || at > now || at.Ticks % 10 != 0)
            throw new ArgumentException("Timestamps must be already known, >=1900 and exactly PostgreSQL microsecond precision.");
    }

    public static void Validate(PortfolioDocument document, DateTimeOffset now, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != 1) throw new ArgumentException("Unsupported schemaVersion; only 1 is supported.");
        var p = document.Portfolio ?? throw new ArgumentException("Portfolio header required.");
        if (p.Id == Guid.Empty || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 200 || p.Name != p.Name.Trim())
            throw new ArgumentException("Invalid portfolio identity/name.");
        Timestamp(p.CreatedAt, now);
        if (document.Events is null || document.Theses is null || document.Instruments is null
            || document.Events.Count > PortfolioLedger.MaximumEvents || document.Theses.Count > MaxTheses
            || document.Instruments.Count > MaxInstruments) throw new ArgumentException("Document collections missing or over bounds.");
        var references = new Dictionary<Guid, InstrumentReference>();
        var symbols = 0;
        foreach (var i in document.Instruments)
        {
            ct.ThrowIfCancellationRequested();
            if (i is null || i.Id == Guid.Empty || !references.TryAdd(i.Id, i) || string.IsNullOrWhiteSpace(i.IssuerName)
                || i.IssuerName.Length > 200 || i.Type is not ("EQUITY" or "INDEX" or "UNKNOWN") || i.Symbols is null
                || i.DelistedOn < i.ListedOn) throw new ArgumentException("Invalid or duplicate instrument reference.");
            symbols += i.Symbols.Count;
            if (symbols > MaxSymbols) throw new ArgumentException("Symbol history exceeds 10,000 rows.");
            var starts = new HashSet<DateOnly>();
            foreach (var s in i.Symbols)
                if (s is null || string.IsNullOrWhiteSpace(s.Symbol) || s.Symbol.Length > 50 || s.ValidTo < s.ValidFrom
                    || !starts.Add(s.ValidFrom)) throw new ArgumentException("Invalid symbol history.");
        }
        // Validate reference intervals within the incoming document before any database mutation.
        foreach (var group in references.Values.SelectMany(i => i.Symbols.Select(s => (i.Id, Symbol: s))).GroupBy(r => r.Symbol.Symbol))
        {
            DateOnly? end = null;
            foreach (var r in group.OrderBy(r => r.Symbol.ValidFrom))
            {
                if (end is not null && r.Symbol.ValidFrom <= end) throw new ArgumentException("Overlapping symbol identity/history: " + group.Key);
                end = r.Symbol.ValidTo ?? DateOnly.MaxValue;
            }
        }
        if (document.Events.Any(e => e is null)) throw new ArgumentException("Null event record.");
        var history = new List<PortfolioEvent>();
        var eventIds = new HashSet<Guid>();
        var importIds = new HashSet<(string, string)>();
        var corrected = new HashSet<Guid>();
        long previousOrder = 0;
        var previousKnown = p.CreatedAt;
        foreach (var e in document.Events.OrderBy(e => e.Order))
        {
            ct.ThrowIfCancellationRequested();
            if (e is null || e.PortfolioId != p.Id || e.Order <= previousOrder || e.KnownAt < previousKnown
                || e.Unit != QuantityUnit.SHARES || !eventIds.Add(e.Id)
                || e.ExternalReference is not null && !importIds.Add((e.Source, e.ExternalReference)))
                throw new ArgumentException("Invalid event identity/order/chronology or duplicate import reference.");
            Timestamp(e.KnownAt, now);
            var canonical = PortfolioLedger.Canonicalize(p.Id, new(e.Id, e.Type, e.InstrumentId, e.TradeDate,
                e.Quantity, e.Unit, e.Price, e.Fees, e.CashAmount, e.ExternalReference, e.Source, e.Note, e.Supersedes), e.KnownAt, e.Order);
            if (canonical != e) throw new ArgumentException("Event fields must already be canonical.");
            if (e.InstrumentId is { } instrument && (!references.TryGetValue(instrument, out var reference) || reference.Type != "EQUITY"))
                throw new ArgumentException("Trade requires an included EQUITY reference.");
            if (e.Supersedes is { } target)
            {
                var prior = history.SingleOrDefault(item => item.Id == target);
                if (prior is null || prior.InstrumentId != e.InstrumentId || !corrected.Add(target))
                    throw new ArgumentException("Invalid correction chain.");
            }
            history.Add(e);
            // ponytail: O(n²) prefix replay up to 10,000 events; reuse incremental projection if imports become large/frequent.
            // Validate every recorded prefix: a later backdated deposit/correction cannot hide invalid past cash or shares.
            previousOrder = e.Order; previousKnown = e.KnownAt;
        }
        foreach (var cutoff in history.Select(e => e.KnownAt).Distinct().Order())
        {
            ct.ThrowIfCancellationRequested();
            // Equal timestamps are one atomic knowledge boundary (e.g. one CSV batch).
            PortfolioLedger.Project(history, cutoff, ProductQuery.Today, p.AllowNegativeCash);
        }
        if (document.Theses.Any(t => t is null)) throw new ArgumentException("Null thesis record.");
        var thesisIds = new HashSet<Guid>();
        foreach (var group in document.Theses.GroupBy(t => t.InstrumentId))
        {
            ThesisVersion? prior = null;
            foreach (var t in group.OrderBy(t => t.Version))
            {
                ct.ThrowIfCancellationRequested();
                if (t.Id == Guid.Empty || !thesisIds.Add(t.Id) || t.PortfolioId != p.Id
                    || t.Version != (prior?.Version ?? 0) + 1 || t.Supersedes != prior?.Id
                    || t.KnownAt < (prior?.KnownAt ?? p.CreatedAt)
                    || !history.Any(e => e.InstrumentId == t.InstrumentId && e.KnownAt <= t.KnownAt))
                    throw new ArgumentException("Invalid thesis identity/version/knowledge chain.");
                Timestamp(t.KnownAt, now);
                var validated = PortfolioLedger.NextThesis(p.Id, t.InstrumentId,
                    new(t.Mandate, t.Text, t.InvalidationNote, t.Active), prior, t.KnownAt);
                if (validated.Text != t.Text) throw new ArgumentException("Thesis text must already be canonical.");
                prior = t;
            }
        }
        var used = history.Where(e => e.InstrumentId is not null).Select(e => e.InstrumentId!.Value).ToHashSet();
        if (!used.SetEquals(references.Keys)) throw new ArgumentException("Instrument references must match ledger instruments exactly.");
    }

    public static Guid CsvEventId(Guid portfolio, string reference) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(portfolio + "\nGENERIC_CSV\n" + reference)).AsSpan(0, 16), bigEndian: true);
    private static readonly string[] CsvHeader = ["trade_date", "type", "instrument_id", "symbol", "quantity", "unit", "price", "fees", "cash_amount", "external_reference", "note"];

    public static IReadOnlyList<ImportRow> ParseCsv(string content, Guid portfolioId, IReadOnlyList<InstrumentReference> instruments,
        DateTimeOffset knownAt, long startOrder, CancellationToken ct = default)
    {
        CheckSize(content);
        using var parser = new TextFieldParser(new StringReader(content))
        { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        var header = parser.ReadFields() ?? throw new ArgumentException("CSV header required.");
        if (!header.SequenceEqual(CsvHeader)) throw new ArgumentException("CSV header must match the documented V0.1 columns exactly.");
        var rows = new List<ImportRow>();
        var ids = instruments.ToDictionary(i => i.Id);
        while (!parser.EndOfData)
        {
            ct.ThrowIfCancellationRequested();
            if (rows.Count >= PortfolioLedger.MaximumEvents) throw new ArgumentException("CSV exceeds 10,000 rows.");
            var number = rows.Count + 1;
            try
            {
                var f = parser.ReadFields()!;
                if (f.Length != CsvHeader.Length) throw new ArgumentException("Wrong field count.");
                var type = Enum.Parse<PortfolioEventType>(f[1], ignoreCase: false);
                var unit = Enum.Parse<QuantityUnit>(f[5], ignoreCase: false);
                if (f[1] != type.ToString() || f[5] != unit.ToString()) throw new ArgumentException("Explicit event type and LOTS/SHARES names required.");
                var trade = type is PortfolioEventType.BUY or PortfolioEventType.SELL;
                Guid? instrument = null;
                if (trade)
                {
                    if (f[2].Length > 0)
                    {
                        instrument = Guid.Parse(f[2]);
                        if (!ids.TryGetValue(instrument.Value, out var info) || info.Type != "EQUITY"
                            || f[3].Length > 0 && !info.Symbols.Any(s => s.Symbol == f[3] && s.ValidFrom <= DateOnly.ParseExact(f[0], "yyyy-MM-dd", CultureInfo.InvariantCulture)
                                && (s.ValidTo is null || s.ValidTo >= DateOnly.ParseExact(f[0], "yyyy-MM-dd", CultureInfo.InvariantCulture))))
                            throw new KeyNotFoundException("Unknown instrument or conflicting symbol: " + f[2]);
                    }
                    else
                    {
                        var day = DateOnly.ParseExact(f[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                        var matches = instruments.Where(i => i.Type == "EQUITY" && i.Symbols.Any(s => s.Symbol == f[3] && s.ValidFrom <= day && (s.ValidTo is null || s.ValidTo >= day))).ToArray();
                        if (matches.Length != 1) throw new KeyNotFoundException("Unknown or ambiguous instrument: " + f[3]);
                        instrument = matches[0].Id;
                    }
                }
                else if (f[2].Length > 0 || f[3].Length > 0) throw new ArgumentException("Cash rows cannot contain instrument metadata.");
                if (string.IsNullOrWhiteSpace(f[9])) throw new ArgumentException("external_reference is required for repeatable CSV imports.");
                decimal Amount(int index) => decimal.Parse(f[index], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                var input = new EventInput(CsvEventId(portfolioId, f[9]), type, instrument,
                    DateOnly.ParseExact(f[0], "yyyy-MM-dd", CultureInfo.InvariantCulture), Amount(4), unit,
                    Amount(6), Amount(7), Amount(8), f[9], "GENERIC_CSV", f[10].Length == 0 ? null : f[10]);
                rows.Add(new(number, PortfolioLedger.Canonicalize(portfolioId, input, knownAt, startOrder + number), null));
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or KeyNotFoundException or MalformedLineException)
            {
                rows.Add(new(number, null, ex is KeyNotFoundException ? "UNKNOWN_INSTRUMENT: " + ex.Message : ex.Message));
            }
        }
        if (rows.Count == 0) throw new ArgumentException("CSV has no transaction rows.");
        return rows;
    }

    private static string CsvText(string value)
    {
        // Spreadsheet safety is a lossy convenience export; canonical JSON retains exact notes/references.
        if (value.AsSpan().TrimStart().Length > 0 && "=+-@".Contains(value.AsSpan().TrimStart()[0])
            || value.StartsWith('\t') || value.StartsWith('\r') || value.StartsWith('\n')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    public static string CsvExport(PortfolioDocument document)
    {
        var builder = new StringBuilder(string.Join(',', CsvHeader) + "\r\n");
        foreach (var e in Ordered(document).Events)
        {
            if (e.Supersedes is not null) throw new ArgumentException("CSV cannot preserve correction history; use JSON export.");
            string Amount(decimal value) => value.ToString(CultureInfo.InvariantCulture);
            builder.AppendLine(string.Join(',', new[] { e.TradeDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), e.Type.ToString(),
                e.InstrumentId?.ToString() ?? "", "", Amount(e.Quantity), "SHARES", Amount(e.Price), Amount(e.Fees), Amount(e.CashAmount),
                e.ExternalReference ?? e.Id.ToString(), e.Note ?? "" }.Select(CsvText)));
        }
        var csv = builder.ToString(); CheckSize(csv); return csv;
    }
}
