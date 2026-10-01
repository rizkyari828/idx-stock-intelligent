using System.Data;
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

    public async Task<IReadOnlyList<PortfolioEvent>> EventsAsync(Guid id, DateTimeOffset cutoff, int offset, int limit, CancellationToken ct)
    {
        ValidatePage(offset, limit);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await ReadPortfolioAsync(connection, id, false, ct);
        await using var command = Command(connection, $"SELECT {EventColumns} FROM portfolio_event WHERE portfolio_id=$1 AND known_at<=$2 ORDER BY trade_date,event_order,event_id OFFSET $3 LIMIT $4", id, cutoff, offset, limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<PortfolioEvent>();
        while (await reader.ReadAsync(ct)) rows.Add(ReadEvent(reader));
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

    public async Task<IReadOnlyList<object>> InstrumentsAsync(int offset, int limit, CancellationToken ct)
    {
        ValidatePage(offset, limit);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = Command(connection, """
            SELECT i.instrument_id,i.issuer_name,i.instrument_type,coalesce(h.symbol,i.issuer_name)
            FROM instrument i LEFT JOIN LATERAL (SELECT symbol FROM instrument_history
                WHERE instrument_id=i.instrument_id AND valid_from <= $1 AND (valid_to IS NULL OR valid_to >= $1)
                ORDER BY valid_from DESC LIMIT 1) h ON true
            ORDER BY coalesce(h.symbol,i.issuer_name) COLLATE "C",i.instrument_id OFFSET $2 LIMIT $3
            """, ProductQuery.Today, offset, limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<object>();
        while (await reader.ReadAsync(ct)) rows.Add(new { id = reader.GetGuid(0), name = reader.GetString(1),
            type = reader.GetString(2), symbol = reader.GetString(3) });
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
        await using (var command = Command(connection, "SELECT instrument_id FROM instrument_history WHERE symbol=$1 AND valid_to IS NULL LIMIT 1", symbol))
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
