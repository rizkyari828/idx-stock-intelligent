using System.Data;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public sealed class StockDatabase(NpgsqlDataSource dataSource)
{
    public async Task<StockHistory> HistoryAsync(Guid id, DateOnly through, DateTimeOffset cutoff, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 120) throw new ArgumentException("History limit must be 1..120.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction) { CommandTimeout = 15 })
            await readOnly.ExecuteNonQueryAsync(ct);
        var registryAsOf = ProductQuery.Today;
        StockIdentity registry;
        await using (var command = new NpgsqlCommand("""
            SELECT i.issuer_name,i.instrument_type,coalesce(h.symbol,l.symbol,i.issuer_name),
                CASE WHEN h.symbol IS NOT NULL THEN 'REGISTRY_HISTORY' WHEN l.symbol IS NOT NULL THEN 'RETAINED_LISTING' ELSE 'UNAVAILABLE' END
            FROM instrument i LEFT JOIN LATERAL (SELECT symbol FROM instrument_history WHERE instrument_id=i.instrument_id
                AND valid_from <= $2 AND (valid_to IS NULL OR valid_to >= $2) ORDER BY valid_from DESC LIMIT 1) h ON true
            LEFT JOIN LATERAL (SELECT nullif(trim(evidence->>'symbol'),'') AS symbol FROM instrument_listing_evidence
                WHERE instrument_id=i.instrument_id AND known_at <= $3 ORDER BY known_at DESC LIMIT 1) l ON true
            WHERE i.instrument_id=$1
            """, connection, transaction) { CommandTimeout = 15 })
        {
            command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(registryAsOf); command.Parameters.AddWithValue(DateTimeOffset.UtcNow);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new KeyNotFoundException("Instrument not found.");
            registry = new(id, reader.GetString(2), reader.GetString(0), reader.GetString(1), reader.GetString(3) == "REGISTRY_HISTORY", "UNKNOWN", reader.GetString(3));
        }
        await using var bars = new NpgsqlCommand("""
            WITH selected AS (
                SELECT DISTINCT ON (r.session_date) r.*,a.source_id,a.content_sha256 AS raw_hash,a.fetched_at
                FROM daily_bar_revision r JOIN raw_artifact a USING(raw_artifact_id)
                WHERE r.instrument_id=$1 AND r.session_date <= $2 AND r.known_at <= $3 AND a.fetched_at <= $3
                    AND (r.retrieved_at IS NULL OR r.retrieved_at <= $3)
                    AND (r.session_known_at IS NULL OR r.session_known_at <= $3)
                ORDER BY r.session_date DESC,r.known_at DESC,r.revision_number DESC
            )
            SELECT instrument_id,session_date,revision_number,known_at,canonical_content_sha256,
                ingestion_run_id,raw_artifact_id,source_id,raw_hash,fetched_at,retrieved_at,
                session_reference,session_known_at,quality_status,open::text,high::text,low::text,
                close::text,volume,adjusted_close::text,volume_unit,volume_basis,market_segment
            FROM selected ORDER BY session_date DESC LIMIT $4
            """, connection, transaction) { CommandTimeout = 15 };
        bars.Parameters.AddWithValue(id); bars.Parameters.AddWithValue(through);
        bars.Parameters.AddWithValue(cutoff); bars.Parameters.AddWithValue(limit);
        var rows = new List<StockHistoryRow>();
        await using (var reader = await bars.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) rows.Add(StockHistoryRow.From(ScreenerEvidenceDatabase.ReadBar(reader)));
        rows.Reverse();
        await transaction.CommitAsync(ct);
        return new(registry, registryAsOf, through, cutoff, limit, "MARKET_DATE_ASCENDING", rows);
    }
}
