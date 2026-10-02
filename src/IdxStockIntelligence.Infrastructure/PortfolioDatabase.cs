using System.Data;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

// CLI persistence stays in PilotDatabase. HTTP requests use pooled, cancellable driver calls.
public sealed class PortfolioDatabase(NpgsqlDataSource dataSource)
{
    public static void ValidatePage(int offset, int limit)
    {
        if (offset is < 0 or > 10000 || limit is < 1 or > 200) throw new ArgumentException("offset 0..10000; limit 1..200.");
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 15 };
        foreach (var value in values) command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
        return command;
    }

    public async Task<Portfolio> CreateAsync(string name, bool allowNegativeCash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new ArgumentException("Portfolio name required; maximum 200 characters.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var id = Guid.NewGuid();
        await using var command = Command(connection,
            "INSERT INTO portfolio(portfolio_id,name,allow_negative_cash) VALUES ($1,$2,$3) RETURNING created_at",
            id, name.Trim(), allowNegativeCash);
        var at = (DateTime)(await command.ExecuteScalarAsync(ct))!;
        return new(id, name.Trim(), allowNegativeCash, new(at));
    }

    private static async Task<Portfolio> ReadPortfolioAsync(NpgsqlConnection connection, Guid id, bool locked, CancellationToken ct)
    {
        await using var command = Command(connection,
            "SELECT name,allow_negative_cash,created_at FROM portfolio WHERE portfolio_id=$1" + (locked ? " FOR UPDATE" : ""), id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new KeyNotFoundException("Portfolio not found.");
        return new(id, reader.GetString(0), reader.GetBoolean(1), reader.GetFieldValue<DateTimeOffset>(2));
    }

    private static PortfolioEvent ReadEvent(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1),
        Enum.Parse<PortfolioEventType>(r.GetString(2)), r.IsDBNull(3) ? null : r.GetGuid(3),
        r.GetFieldValue<DateOnly>(4), r.GetFieldValue<DateTimeOffset>(5), r.GetInt64(6), r.GetDecimal(7), QuantityUnit.SHARES,
        r.GetDecimal(8), r.GetDecimal(9), r.GetDecimal(10), r.IsDBNull(11) ? null : r.GetString(11), r.GetString(12),
        r.IsDBNull(13) ? null : r.GetString(13), r.IsDBNull(14) ? null : r.GetGuid(14));
    private const string EventColumns = "event_id,portfolio_id,event_type,instrument_id,trade_date,known_at,event_order,quantity,price,fees,cash_amount,external_reference,source,note,supersedes";

    private static async Task<List<PortfolioEvent>> HistoryAsync(NpgsqlConnection connection, Guid id, DateTimeOffset cutoff, CancellationToken ct)
    {
        // ponytail: replay <=10,000 events per portfolio; add audited checkpoints when this bound matters.
        await using var command = Command(connection,
            $"SELECT {EventColumns} FROM portfolio_event WHERE portfolio_id=$1 AND known_at<=$2 ORDER BY event_order LIMIT 10001", id, cutoff);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var events = new List<PortfolioEvent>();
        while (await reader.ReadAsync(ct)) events.Add(ReadEvent(reader));
        if (events.Count > PortfolioLedger.MaximumEvents) throw new InvalidOperationException("Portfolio history exceeds supported bound.");
        return events;
    }

    public async Task<(PortfolioEvent Event, bool Duplicate)> AppendAsync(Guid id, EventInput input, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        // Per-portfolio row lock serializes cash, imports, corrections and thesis versions.
        var portfolio = await ReadPortfolioAsync(connection, id, true, ct);
        var history = await HistoryAsync(connection, id, DateTimeOffset.MaxValue, ct);
        var clock = DateTimeOffset.UtcNow;
        var at = history.Count == 0 ? clock : clock > history[^1].KnownAt ? clock : history[^1].KnownAt.AddTicks(10);
        var item = PortfolioLedger.Canonicalize(id, input, at, (history.LastOrDefault()?.Order ?? 0) + 1);
        var existing = history.FirstOrDefault(e => e.Id == item.Id || item.ExternalReference is not null
            && e.Source == item.Source && e.ExternalReference == item.ExternalReference);
        if (existing is not null)
        {
            if (!PortfolioLedger.SameFact(item, existing)) throw new ArgumentException("Idempotency identity already exists with different content.");
            await transaction.CommitAsync(ct);
            return (existing, true);
        }
        PortfolioLedger.ValidateAppend(history, item);
        if (item.InstrumentId is { } instrument)
        {
            await using var instrumentCommand = Command(connection, "SELECT instrument_type FROM instrument WHERE instrument_id=$1", instrument);
            var type = await instrumentCommand.ExecuteScalarAsync(ct) as string;
            if (type != "EQUITY") throw new ArgumentException("V0.1 trades require a registered EQUITY instrument; no collector membership required.");
        }
        history.Add(item);
        PortfolioLedger.Project(history, at, ProductQuery.Today, portfolio.AllowNegativeCash);
        await using var command = Command(connection, $"""
            INSERT INTO portfolio_event(event_id,portfolio_id,event_type,instrument_id,trade_date,known_at,
                quantity,quantity_unit,price,fees,cash_amount,external_reference,source,note,supersedes)
            VALUES ($1,$2,$3,$4,$5,$6,$7,'SHARES',$8,$9,$10,$11,$12,$13,$14) RETURNING {EventColumns}
            """, item.Id, id, item.Type.ToString(), item.InstrumentId, item.TradeDate, item.KnownAt,
            item.Quantity, item.Price, item.Fees, item.CashAmount, item.ExternalReference, item.Source, item.Note, item.Supersedes);
        PortfolioEvent persisted;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            persisted = ReadEvent(reader);
        }
        await transaction.CommitAsync(ct);
        return (persisted, false);
    }

    public async Task<IReadOnlyList<object>> EventsAsync(Guid id, DateTimeOffset cutoff, int offset, int limit, CancellationToken ct)
    {
        ValidatePage(offset, limit);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await ReadPortfolioAsync(connection, id, false, ct);
        await using var command = Command(connection, $"""
            SELECT {EventColumns},(SELECT c.event_id FROM portfolio_event c
                WHERE c.supersedes=portfolio_event.event_id AND c.known_at<=$2) AS corrected_by
            FROM portfolio_event WHERE portfolio_id=$1 AND known_at<=$2
            ORDER BY trade_date,event_order,event_id OFFSET $3 LIMIT $4
            """, id, cutoff, offset, limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<object>();
        while (await reader.ReadAsync(ct))
        {
            var e = ReadEvent(reader);
            rows.Add(new { e.Id, e.PortfolioId, e.Type, e.InstrumentId, e.TradeDate, e.KnownAt, e.Order,
                e.Quantity, e.Unit, e.Price, e.Fees, e.CashAmount, e.ExternalReference, e.Source, e.Note,
                e.Supersedes, correctedBy = reader.IsDBNull(15) ? (Guid?)null : reader.GetGuid(15) });
        }
        return rows;
    }

    private static ThesisVersion ReadThesis(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetInt32(3),
        Enum.Parse<Mandate>(r.GetString(4)), r.GetString(5), r.GetFieldValue<DateTimeOffset>(6),
        r.IsDBNull(7) ? null : r.GetGuid(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetBoolean(9));
    private const string ThesisColumns = "thesis_id,portfolio_id,instrument_id,version,mandate,thesis_text,known_at,supersedes,invalidation_note,active";

    public async Task<IReadOnlyList<ThesisVersion>> ThesesAsync(Guid id, Guid instrument, DateTimeOffset cutoff, int offset, int limit, CancellationToken ct)
    {
        ValidatePage(offset, limit);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await ReadPortfolioAsync(connection, id, false, ct);
        return await ReadThesesAsync(connection, id, instrument, cutoff, offset, limit, ct);
    }
    private static async Task<List<ThesisVersion>> ReadThesesAsync(NpgsqlConnection connection, Guid id, Guid instrument,
        DateTimeOffset cutoff, int offset, int limit, CancellationToken ct)
    {
        await using var command = Command(connection, $"SELECT {ThesisColumns} FROM thesis_version WHERE portfolio_id=$1 AND instrument_id=$2 AND known_at<=$3 ORDER BY version DESC OFFSET $4 LIMIT $5", id, instrument, cutoff, offset, limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<ThesisVersion>();
        while (await reader.ReadAsync(ct)) rows.Add(ReadThesis(reader));
        return rows;
    }

    public async Task<ThesisVersion> AddThesisAsync(Guid id, Guid instrument, ThesisInput input, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await ReadPortfolioAsync(connection, id, true, ct);
        var history = await HistoryAsync(connection, id, DateTimeOffset.MaxValue, ct);
        if (!history.Any(e => e.InstrumentId == instrument)) throw new ArgumentException("Record an instrument event before adding a thesis.");
        var prior = (await ReadThesesAsync(connection, id, instrument, DateTimeOffset.MaxValue, 0, 1, ct)).FirstOrDefault();
        var at = DateTimeOffset.UtcNow;
        if (prior?.KnownAt >= at) at = prior.KnownAt.AddTicks(10);
        var thesis = PortfolioLedger.NextThesis(id, instrument, input, prior, at);
        await using var command = Command(connection, """
            INSERT INTO thesis_version(thesis_id,portfolio_id,instrument_id,version,mandate,thesis_text,known_at,supersedes,invalidation_note,active)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
            """, thesis.Id, id, instrument, thesis.Version, thesis.Mandate.ToString(), thesis.Text, thesis.KnownAt,
            thesis.Supersedes, thesis.InvalidationNote, thesis.Active);
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return thesis;
    }

    public async Task<MarketState> MarketAsync(Guid instrument, DateOnly through, DateTimeOffset cutoff, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await ReadMarketAsync(connection, instrument, through, cutoff, ct);
    }
    private static async Task<MarketState> ReadMarketAsync(NpgsqlConnection connection, Guid instrument, DateOnly through, DateTimeOffset cutoff, CancellationToken ct)
    {
        await using var command = Command(connection, """
            SELECT coalesce(h.symbol,i.issuer_name),i.instrument_type,r.session_date,coalesce(r.retrieved_at,a.fetched_at),r.known_at,
                r.revision_number,r.quality_status,a.source_id,r.close
            FROM instrument i
            LEFT JOIN LATERAL (SELECT symbol FROM instrument_history WHERE instrument_id=i.instrument_id
                AND valid_from <= $2 AND (valid_to IS NULL OR valid_to >= $2) ORDER BY valid_from DESC LIMIT 1) h ON true
            LEFT JOIN LATERAL (SELECT * FROM daily_bar_revision WHERE instrument_id=i.instrument_id
                AND session_date <= $2 AND known_at <= $3
                AND EXISTS (SELECT 1 FROM raw_artifact evidence WHERE evidence.raw_artifact_id=daily_bar_revision.raw_artifact_id
                    AND coalesce(retrieved_at,evidence.fetched_at) <= $3)
                ORDER BY session_date DESC,known_at DESC,revision_number DESC LIMIT 1) r ON true
            LEFT JOIN raw_artifact a USING(raw_artifact_id)
            WHERE i.instrument_id=$1
            """, instrument, through, cutoff);
        await using var r = await command.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new KeyNotFoundException("Instrument not found.");
        var symbol = r.GetString(0);
        var type = r.GetString(1);
        if (r.IsDBNull(2)) return new(instrument, symbol, type, null, null, null, cutoff, null, Freshness.UNKNOWN,
            Completeness.UNKNOWN, MarketQuality.UNKNOWN, null, "RAW_CLOSE", null, ProductValuation.UnprojectedFeatures(), "NO_CURRENT_MARKET_PRICE");
        var date = r.GetFieldValue<DateOnly>(2);
        var quality = r.GetString(6) switch { "VALID" => MarketQuality.VERIFIED, "DEGRADED" or "STALE" => MarketQuality.DEGRADED,
            "REJECTED" => MarketQuality.REJECTED, _ => MarketQuality.UNKNOWN };
        return new(instrument, symbol, type, date, r.IsDBNull(3) ? null : r.GetFieldValue<DateTimeOffset>(3),
            r.GetFieldValue<DateTimeOffset>(4), cutoff, r.GetInt64(5), date == through ? Freshness.CURRENT : Freshness.STALE,
            Completeness.UNKNOWN, quality, r.GetString(7), "RAW_CLOSE", r.GetDecimal(8), ProductValuation.UnprojectedFeatures(),
            quality is MarketQuality.UNKNOWN or MarketQuality.REJECTED ? "PRICE_QUALITY_UNAVAILABLE" : null);
    }

    public async Task<PortfolioView> ViewAsync(Guid id, DateOnly through, DateTimeOffset cutoff, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var portfolio = await ReadPortfolioAsync(connection, id, false, ct);
        if (portfolio.CreatedAt > cutoff) throw new KeyNotFoundException("Portfolio did not exist at cutoff.");
        var history = await HistoryAsync(connection, id, cutoff, ct);
        var projection = PortfolioLedger.Project(history, cutoff, through, portfolio.AllowNegativeCash);
        var markets = new Dictionary<Guid, MarketState>();
        var theses = new Dictionary<Guid, ThesisVersion>();
        // ponytail: bounded per-holding reads (<=200); batch enrichment if measured latency warrants it.
        foreach (var position in projection.Positions.Where(p => p.Shares > 0))
        {
            markets[position.InstrumentId] = await ReadMarketAsync(connection, position.InstrumentId, through, cutoff, ct);
            var thesis = (await ReadThesesAsync(connection, id, position.InstrumentId, cutoff, 0, 1, ct)).FirstOrDefault();
            if (thesis is not null) theses[position.InstrumentId] = thesis;
        }
        await transaction.CommitAsync(ct);
        return ProductValuation.Assemble(portfolio, projection, markets, theses, through, cutoff);
    }

    public async Task<ReconciliationPreview> ReconcileAsync(Guid id, ReconciliationInput input, CancellationToken ct)
    {
        PortfolioReconciliation.Validate(input);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var readOnly = Command(connection, "SET TRANSACTION READ ONLY"))
            await readOnly.ExecuteNonQueryAsync(ct);
        var portfolio = await ReadPortfolioAsync(connection, id, false, ct);
        if (portfolio.CreatedAt > input.Cutoff) throw new KeyNotFoundException("Portfolio did not exist at cutoff.");
        var projection = PortfolioLedger.Project(await HistoryAsync(connection, id, input.Cutoff, ct),
            input.Cutoff, input.Through, portfolio.AllowNegativeCash);
        var ids = projection.Positions.Where(p => p.Shares > 0).Select(p => p.InstrumentId)
            .Concat(input.Holdings.Where(h => h.InstrumentId is not null).Select(h => h.InstrumentId!.Value)).ToHashSet();
        var symbols = input.Holdings.Where(h => !string.IsNullOrWhiteSpace(h.Symbol))
            .Select(h => h.Symbol!.Trim().ToUpperInvariant()).Distinct().ToArray();
        await using (var lookup = Command(connection, """
            SELECT DISTINCT instrument_id FROM instrument_history
            WHERE symbol=ANY($1) AND valid_from<=$2 AND (valid_to IS NULL OR valid_to>=$2) LIMIT 1001
            """, symbols, input.Through))
        await using (var reader = await lookup.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                if (ids.Add(reader.GetGuid(0)) && ids.Count > PortfolioExchange.MaxInstruments)
                    throw new ArgumentException("Instrument lookup exceeds supported bound.");
        var result = PortfolioReconciliation.Compare(projection, input, await ReferencesAsync(connection, ids.ToArray(), ct), ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<PortfolioDocument> ExportAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var document = await ExportCoreAsync(connection, id, ct);
        PortfolioExchange.Serialize(document); // enforce portable file-size bound before sending anything
        await transaction.CommitAsync(ct);
        return document;
    }

    internal static async Task<ScreenerPortfolioHistory> ScreenerHistoryAsync(NpgsqlConnection connection,
        Guid id, DateTimeOffset cutoff, CancellationToken ct)
    {
        var portfolio = await ReadPortfolioAsync(connection, id, false, ct);
        if (portfolio.CreatedAt > cutoff) throw new KeyNotFoundException("Portfolio did not exist at cutoff.");
        return new(portfolio, await HistoryAsync(connection, id, cutoff, ct), await AllThesesAsync(connection, id, ct, cutoff));
    }

    private static async Task<List<ThesisVersion>> AllThesesAsync(NpgsqlConnection connection, Guid id, CancellationToken ct,
        DateTimeOffset? cutoff = null)
    {
        await using var command = Command(connection, $"SELECT {ThesisColumns} FROM thesis_version WHERE portfolio_id=$1 AND known_at<=$2 ORDER BY instrument_id,version LIMIT 2001", id, cutoff ?? DateTimeOffset.MaxValue);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<ThesisVersion>();
        while (await reader.ReadAsync(ct)) rows.Add(ReadThesis(reader));
        if (rows.Count > PortfolioExchange.MaxTheses) throw new ArgumentException("Export exceeds 2,000 thesis versions.");
        return rows;
    }

    private static async Task<List<InstrumentReference>> ReferencesAsync(NpgsqlConnection connection, Guid[]? ids, CancellationToken ct)
    {
        await using var command = Command(connection, """
            SELECT instrument_id,issuer_name,instrument_type,listed_on,delisted_on FROM instrument
            WHERE ($1::uuid[] IS NULL OR instrument_id=ANY($1)) ORDER BY instrument_id LIMIT 1001
            """, ids);
        var rows = new Dictionary<Guid, InstrumentReference>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                rows.Add(reader.GetGuid(0), new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3),
                    reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4), []));
        if (rows.Count > PortfolioExchange.MaxInstruments) throw new ArgumentException("Registry/export exceeds 1,000 instruments.");
        await using var symbolsCommand = Command(connection, "SELECT instrument_id,symbol,valid_from,valid_to FROM instrument_history WHERE instrument_id=ANY($1) ORDER BY instrument_id,valid_from LIMIT 10001", rows.Keys.ToArray());
        var histories = rows.Keys.ToDictionary(id => id, _ => new List<SymbolReference>());
        var count = 0;
        await using (var reader = await symbolsCommand.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                if (++count > PortfolioExchange.MaxSymbols) throw new ArgumentException("Symbol history exceeds 10,000 rows.");
                histories[reader.GetGuid(0)].Add(new(reader.GetString(1), reader.GetFieldValue<DateOnly>(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3)));
            }
        return rows.Values.Select(i => i with { Symbols = histories[i.Id] }).ToList();
    }

    private static async Task<PortfolioDocument> ExportCoreAsync(NpgsqlConnection connection, Guid id, CancellationToken ct)
    {
        var portfolio = await ReadPortfolioAsync(connection, id, false, ct);
        var history = await HistoryAsync(connection, id, DateTimeOffset.MaxValue, ct);
        var theses = await AllThesesAsync(connection, id, ct);
        var ids = history.Where(e => e.InstrumentId is not null).Select(e => e.InstrumentId!.Value).Distinct().ToArray();
        var references = await ReferencesAsync(connection, ids, ct);
        return PortfolioExchange.Ordered(new(1, portfolio, history, theses, references));
    }

    public async Task<ImportPreview> PreviewImportAsync(ImportRequest request, CancellationToken ct) =>
        (await ProcessImportAsync(request, false, ct)).Preview;
    public async Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken ct)
    {
        var result = await ProcessImportAsync(request, true, ct);
        return new(result.Preview.Status, result.Preview.PortfolioId, result.EventsAdded, result.ThesesAdded, result.Preview.Errors);
    }

    private async Task<(ImportPreview Preview, int EventsAdded, int ThesesAdded)> ProcessImportAsync(ImportRequest request, bool commit, CancellationToken ct)
    {
        Guid? id = request.PortfolioId;
        try
        {
            PortfolioExchange.CheckSize(request.Content);
            if (!Enum.IsDefined(request.Mode)) throw new ArgumentException("Unknown import mode.");
            PortfolioDocument? document = null;
            if (request.Format == "JSON")
            {
                document = PortfolioExchange.Parse(request.Content);
                PortfolioExchange.Validate(document, DateTimeOffset.UtcNow, ct);
                if (id is not null && id != document.Portfolio.Id) throw new ArgumentException("Restore cannot remap portfolio identity.");
                id = document.Portfolio.Id;
            }
            else if (request.Format != "CSV" || id is null || id == Guid.Empty || request.Mode != ImportMode.CREATE_NEW)
                throw new ArgumentException("Format must be JSON, or CSV with CREATE_NEW and an existing portfolio ID.");
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(commit ? IsolationLevel.ReadCommitted : IsolationLevel.RepeatableRead, ct);
            if (commit)
            {
                await using var gate = Command(connection, "SELECT pg_advisory_xact_lock(hashtextextended($1,0))", "portfolio-import:" + id);
                await gate.ExecuteNonQueryAsync(ct);
            }
            await using var existsCommand = Command(connection, "SELECT EXISTS(SELECT 1 FROM portfolio WHERE portfolio_id=$1)", id);
            var exists = (bool)(await existsCommand.ExecuteScalarAsync(ct))!;
            var portfolio = exists ? await ReadPortfolioAsync(connection, id!.Value, commit, ct) : document?.Portfolio;
            var history = exists ? await HistoryAsync(connection, id!.Value, DateTimeOffset.MaxValue, ct) : [];
            var theses = exists ? await AllThesesAsync(connection, id!.Value, ct) : [];
            List<PortfolioEvent> added;
            IReadOnlyList<ImportRow> rows;
            if (document is not null)
            {
                if (commit)
                    foreach (var symbol in document.Instruments.SelectMany(i => i.Symbols).Select(s => s.Symbol).Distinct().Order(StringComparer.Ordinal))
                    {
                        await using var gate = Command(connection, "SELECT pg_advisory_xact_lock(hashtext($1))", "instrument:" + symbol);
                        await gate.ExecuteNonQueryAsync(ct);
                    }
                var conflicts = await ReferenceConflictsAsync(connection, document.Instruments, ct);
                if (conflicts.Count > 0) return (Failure(ImportStatus.CONFLICT, id, conflicts), 0, 0);
                if (exists && PortfolioExchange.Serialize(await ExportCoreAsync(connection, id!.Value, ct)) == PortfolioExchange.Serialize(document))
                    return (new(ImportStatus.ALREADY_PRESENT, id, document.Events.Count, document.Events.Count, 0, document.Events.Count,
                        history.Count, [], [], []), 0, 0);
                if (exists && (request.Mode != ImportMode.RESTORE_EXISTING_EMPTY || history.Count != 0 || theses.Count != 0 || portfolio != document.Portfolio))
                    return (Failure(ImportStatus.CONFLICT, id, ["Target exists and is nonempty or its identity/settings/creation timestamp differ."]), 0, 0);
                if (!exists && request.Mode == ImportMode.RESTORE_EXISTING_EMPTY)
                    return (Failure(ImportStatus.CONFLICT, id, ["RESTORE_EXISTING_EMPTY requires the original empty portfolio header."]), 0, 0);
                added = document.Events.OrderBy(e => e.Order).ToList();
                rows = added.Select((e, index) => new ImportRow(index + 1, e, null)).ToArray();
            }
            else
            {
                if (!exists) return (Failure(ImportStatus.CONFLICT, id, ["CSV requires an existing portfolio."]), 0, 0);
                var references = await ReferencesAsync(connection, null, ct);
                var now = DateTimeOffset.UtcNow;
                if (history.LastOrDefault()?.KnownAt >= now) now = history[^1].KnownAt.AddTicks(10);
                rows = PortfolioExchange.ParseCsv(request.Content, id!.Value, references, now, history.LastOrDefault()?.Order ?? 0, ct);
                added = [];
                var classified = new List<ImportRow>();
                foreach (var row in rows)
                {
                    var e = row.Event;
                    if (e is null) { classified.Add(row); continue; }
                    var existing = history.Concat(added).FirstOrDefault(prior => prior.Id == e.Id
                        || prior.Source == e.Source && prior.ExternalReference == e.ExternalReference);
                    classified.Add(existing is null ? row : PortfolioLedger.SameFact(e, existing)
                        ? row with { Duplicate = true } : row with { Error = "Import reference conflicts with existing content." });
                    if (existing is null) added.Add(e);
                }
                rows = classified;
                var invalid = rows.Where(r => r.Error is not null).ToArray();
                if (invalid.Length > 0)
                {
                    var unknown = invalid.Where(r => r.Error!.StartsWith("UNKNOWN_INSTRUMENT:", StringComparison.Ordinal)).Select(r => r.Error!).ToArray();
                    var status = invalid.Any(r => r.Error!.Contains("conflicts with existing", StringComparison.Ordinal)) ? ImportStatus.CONFLICT : ImportStatus.INVALID;
                    return (new(status, id, rows.Count, rows.Count - invalid.Length, invalid.Length, rows.Count(r => r.Duplicate),
                        history.Count + added.Count, unknown, invalid.Select(r => "Row " + r.Row + ": " + r.Error).ToArray(), rows), 0, 0);
                }
                if (history.Count + added.Count > PortfolioLedger.MaximumEvents) throw new ArgumentException("Import would exceed 10,000 ledger events.");
                try { PortfolioLedger.Project(history.Concat(added), now, ProductQuery.Today, portfolio!.AllowNegativeCash); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
                {
                    return (new(ImportStatus.INVALID, id, rows.Count, 0, rows.Count, rows.Count(r => r.Duplicate),
                        history.Count + added.Count, [], [ex.Message], rows.Select(r => r with { Error = ex.Message }).ToArray()), 0, 0);
                }
            }
            // Global event/thesis identities cannot be reused in a different portfolio.
            await using (var collision = Command(connection, "SELECT EXISTS(SELECT 1 FROM portfolio_event WHERE event_id=ANY($1)) OR EXISTS(SELECT 1 FROM thesis_version WHERE thesis_id=ANY($2))",
                added.Select(e => e.Id).ToArray(), document?.Theses.Select(t => t.Id).ToArray() ?? []))
                if ((bool)(await collision.ExecuteScalarAsync(ct))!) return (Failure(ImportStatus.CONFLICT, id, ["Event/thesis identity already belongs to stored data."]), 0, 0);
            var disposition = added.Count == 0 && document is null ? ImportStatus.ALREADY_PRESENT : commit ? ImportStatus.IMPORTED : ImportStatus.READY;
            var preview = new ImportPreview(disposition, id, rows.Count, rows.Count, 0, rows.Count(r => r.Duplicate),
                history.Count + added.Count, [], [], rows);
            if (!commit || disposition == ImportStatus.ALREADY_PRESENT) return (preview, 0, 0);
            if (!exists)
            {
                await using var insert = Command(connection, "INSERT INTO portfolio(portfolio_id,name,allow_negative_cash,created_at) VALUES ($1,$2,$3,$4)",
                    portfolio!.Id, portfolio.Name, portfolio.AllowNegativeCash, portfolio.CreatedAt);
                await insert.ExecuteNonQueryAsync(ct);
            }
            if (document is not null)
            {
                await RestoreReferencesAsync(connection, document.Instruments, ct);
                var concurrentConflicts = await ReferenceConflictsAsync(connection, document.Instruments, ct);
                if (concurrentConflicts.Count > 0) return (Failure(ImportStatus.CONFLICT, id, concurrentConflicts), 0, 0);
            }
            foreach (var e in added)
            {
                ct.ThrowIfCancellationRequested();
                await using var insert = Command(connection, """
                    INSERT INTO portfolio_event(event_id,portfolio_id,event_type,instrument_id,trade_date,known_at,quantity,quantity_unit,
                        price,fees,cash_amount,external_reference,source,note,supersedes)
                    VALUES ($1,$2,$3,$4,$5,$6,$7,'SHARES',$8,$9,$10,$11,$12,$13,$14)
                    """, e.Id, id, e.Type.ToString(), e.InstrumentId, e.TradeDate, e.KnownAt, e.Quantity, e.Price, e.Fees,
                    e.CashAmount, e.ExternalReference, e.Source, e.Note, e.Supersedes);
                await insert.ExecuteNonQueryAsync(ct);
            }
            if (document is not null)
                foreach (var t in document.Theses.OrderBy(t => t.InstrumentId).ThenBy(t => t.Version))
                {
                    await using var insert = Command(connection, """
                        INSERT INTO thesis_version(thesis_id,portfolio_id,instrument_id,version,mandate,thesis_text,known_at,supersedes,invalidation_note,active)
                        VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
                        """, t.Id, id, t.InstrumentId, t.Version, t.Mandate.ToString(), t.Text, t.KnownAt, t.Supersedes, t.InvalidationNote, t.Active);
                    await insert.ExecuteNonQueryAsync(ct);
                }
            await transaction.CommitAsync(ct);
            return (preview, added.Count, document?.Theses.Count ?? 0);
        }
        catch (PostgresException ex) when (ex.SqlState is "23505" or "23503" or "23514" or "40001")
        { return (Failure(ImportStatus.CONFLICT, id, ["Database identity/constraint conflict; transaction rolled back."]), 0, 0); }
        catch (Exception ex) when (ex is ArgumentException or JsonException or FormatException or InvalidOperationException or OverflowException)
        { return (Failure(ImportStatus.INVALID, id, [ex is JsonException ? "Invalid or incomplete JSON document." : ex.Message]), 0, 0); }
    }

    private static ImportPreview Failure(ImportStatus status, Guid? id, IReadOnlyList<string> errors) => new(status, id, 0, 0, 1, 0, 0, [], errors, []);

    private static async Task<List<string>> ReferenceConflictsAsync(NpgsqlConnection connection, IReadOnlyList<InstrumentReference> references, CancellationToken ct)
    {
        var existing = (await ReferencesAsync(connection, references.Select(i => i.Id).ToArray(), ct)).ToDictionary(i => i.Id);
        var errors = new List<string>();
        foreach (var i in references)
            if (existing.TryGetValue(i.Id, out var prior) && (prior.Id != i.Id || prior.IssuerName != i.IssuerName || prior.Type != i.Type
                || prior.ListedOn != i.ListedOn || prior.DelistedOn != i.DelistedOn || !prior.Symbols.SequenceEqual(i.Symbols.OrderBy(s => s.ValidFrom))))
                errors.Add("Instrument metadata conflict: " + i.Id);
        var symbols = references.SelectMany(i => i.Symbols).Select(s => s.Symbol).Distinct().ToArray();
        await using var command = Command(connection, "SELECT instrument_id,symbol,valid_from,valid_to FROM instrument_history WHERE symbol=ANY($1) ORDER BY instrument_id,valid_from LIMIT 10001", (object)symbols);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var count = 0;
        while (await reader.ReadAsync(ct))
        {
            if (++count > PortfolioExchange.MaxSymbols) throw new ArgumentException("Referenced symbol lookup exceeds bound.");
            var id = reader.GetGuid(0); var symbol = reader.GetString(1); var from = reader.GetFieldValue<DateOnly>(2);
            var to = reader.IsDBNull(3) ? DateOnly.MaxValue : reader.GetFieldValue<DateOnly>(3);
            if (references.Any(i => i.Id != id && i.Symbols.Any(s => s.Symbol == symbol && s.ValidFrom <= to && (s.ValidTo ?? DateOnly.MaxValue) >= from)))
                errors.Add("Symbol identity conflict: " + symbol);
        }
        return errors;
    }
    private static async Task RestoreReferencesAsync(NpgsqlConnection connection, IReadOnlyList<InstrumentReference> references, CancellationToken ct)
    {
        foreach (var i in references)
        {
            await using var insert = Command(connection, "INSERT INTO instrument(instrument_id,issuer_name,instrument_type,listed_on,delisted_on) VALUES ($1,$2,$3,$4,$5) ON CONFLICT DO NOTHING",
                i.Id, i.IssuerName, i.Type, i.ListedOn, i.DelistedOn);
            var created = await insert.ExecuteNonQueryAsync(ct);
            if (created == 0) continue;
            foreach (var s in i.Symbols)
            {
                await using var symbol = Command(connection, "INSERT INTO instrument_history(instrument_id,symbol,valid_from,valid_to) VALUES ($1,$2,$3,$4)", i.Id, s.Symbol, s.ValidFrom, s.ValidTo);
                await symbol.ExecuteNonQueryAsync(ct);
            }
        }
    }

    public async Task<IReadOnlyList<object>> InstrumentsAsync(int offset, int limit, CancellationToken ct, DateOnly? through = null)
    {
        ValidatePage(offset, limit);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = Command(connection, """
            SELECT i.instrument_id,i.issuer_name,i.instrument_type,coalesce(h.symbol,i.issuer_name),h.symbol
            FROM instrument i LEFT JOIN LATERAL (SELECT symbol FROM instrument_history
                WHERE instrument_id=i.instrument_id AND valid_from <= $1 AND (valid_to IS NULL OR valid_to >= $1)
                ORDER BY valid_from DESC LIMIT 1) h ON true
            ORDER BY coalesce(h.symbol,i.issuer_name) COLLATE "C",i.instrument_id OFFSET $2 LIMIT $3
            """, ProductQuery.Date(through), offset, limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<object>();
        while (await reader.ReadAsync(ct)) rows.Add(new { id = reader.GetGuid(0), name = reader.GetString(1),
            type = reader.GetString(2), symbol = reader.GetString(3), hasEffectiveSymbol = !reader.IsDBNull(4) });
        return rows;
    }

    public async Task RegisterInstrumentAsync(Guid id, string name, string symbol, string type, DateOnly validFrom, CancellationToken ct)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Length > 200 || string.IsNullOrWhiteSpace(symbol)
            || symbol.Length > 50 || type is not ("EQUITY" or "INDEX" or "UNKNOWN")
            || validFrom > ProductQuery.Today || validFrom < new DateOnly(1900, 1, 1)) throw new ArgumentException("Valid stable ID, name, symbol and type required.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        symbol = symbol.Trim().ToUpperInvariant();
        await using (var command = Command(connection, "SELECT pg_advisory_xact_lock(hashtext($1))", "instrument:" + symbol))
            await command.ExecuteNonQueryAsync(ct);
        await using (var command = Command(connection, "SELECT instrument_id FROM instrument_history WHERE symbol=$1 AND (valid_to IS NULL OR valid_to >= $2) AND instrument_id <> $3 LIMIT 1", symbol, validFrom, id))
        {
            var owner = await command.ExecuteScalarAsync(ct);
            if (owner is Guid existing && existing != id) throw new ArgumentException("Symbol already has a stable instrument ID; use that identity.");
        }
        await using (var command = Command(connection, "INSERT INTO instrument(instrument_id,issuer_name,instrument_type) VALUES ($1,$2,$3) ON CONFLICT DO NOTHING", id, name.Trim(), type))
            await command.ExecuteNonQueryAsync(ct);
        await using (var command = Command(connection, "SELECT instrument_type FROM instrument WHERE instrument_id=$1 FOR UPDATE", id))
        {
            var current = (string)(await command.ExecuteScalarAsync(ct))!;
            if (current != "UNKNOWN" && current != type) throw new ArgumentException("Instrument type conflicts with registered identity.");
        }
        await using (var command = Command(connection, "UPDATE instrument SET instrument_type=$2 WHERE instrument_id=$1 AND instrument_type='UNKNOWN'", id, type))
            await command.ExecuteNonQueryAsync(ct);
        await using (var command = Command(connection, "SELECT symbol FROM instrument_history WHERE instrument_id=$1 ORDER BY valid_from DESC LIMIT 1", id))
        {
            var current = await command.ExecuteScalarAsync(ct) as string;
            if (current is not null && current != symbol) throw new ArgumentException("Symbol changes require explicit security-master history; this endpoint only registers metadata.");
            if (current is null)
            {
                await using var insert = Command(connection, "INSERT INTO instrument_history(instrument_id,symbol,valid_from) VALUES ($1,$2,$3)", id, symbol, validFrom);
                await insert.ExecuteNonQueryAsync(ct);
            }
        }
        await transaction.CommitAsync(ct);
    }
}
