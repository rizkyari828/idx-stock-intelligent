using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public static class ScreenerEvidenceDatabase
{
    private static readonly JsonSerializerOptions ListingOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    // ponytail: fixed 211-ID/366-date/80,000-selected-row pilot; measure and review bounds before expansion.
    public static async Task<EvidenceResult<ScreenerDatabaseEvidence>> ReadAsync(NpgsqlConnection connection,
        NpgsqlTransaction? transaction, ScreenerReadRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.BoundsReason() is { } reason) return new(null, reason);
        var command = new NpgsqlBatchCommand("""
            WITH selected AS (
                SELECT DISTINCT ON (r.instrument_id,r.session_date) r.*,a.source_id,
                    a.content_sha256 AS raw_hash,a.fetched_at
                FROM daily_bar_revision r JOIN raw_artifact a USING(raw_artifact_id)
                WHERE r.instrument_id = ANY($1) AND r.session_date BETWEEN $2 AND $3
                    AND r.known_at <= $4 AND a.fetched_at <= $4
                    AND (r.retrieved_at IS NULL OR r.retrieved_at <= $4)
                    AND (r.session_known_at IS NULL OR r.session_known_at <= $4)
                ORDER BY r.instrument_id,r.session_date,r.known_at DESC,r.revision_number DESC
            )
            SELECT instrument_id,session_date,revision_number,known_at,canonical_content_sha256,
                ingestion_run_id,raw_artifact_id,source_id,raw_hash,fetched_at,retrieved_at,
                session_reference,session_known_at,quality_status,open::text,high::text,low::text,
                close::text,volume,adjusted_close::text,volume_unit,volume_basis,market_segment
            FROM selected ORDER BY instrument_id,session_date LIMIT 80001;
            """);
        var ids = request.InstrumentIds.Append(request.BenchmarkId).Distinct().ToArray();
        command.Parameters.Add(new NpgsqlParameter { Value = ids });
        command.Parameters.Add(new NpgsqlParameter { Value = request.HistoryAnchor });
        command.Parameters.Add(new NpgsqlParameter { Value = request.Through });
        command.Parameters.Add(new NpgsqlParameter { Value = request.Cutoff.ToUniversalTime() });
        var listingCommand = new NpgsqlBatchCommand("""
            SELECT DISTINCT ON (instrument_id) instrument_id,known_at,evidence::text
            FROM instrument_listing_evidence WHERE instrument_id=ANY($1) AND known_at <= $2
            ORDER BY instrument_id,known_at DESC;
            """);
        listingCommand.Parameters.Add(new NpgsqlParameter { Value = ids });
        listingCommand.Parameters.Add(new NpgsqlParameter { Value = request.Cutoff.ToUniversalTime() });
        await using var batch = new NpgsqlBatch(connection, transaction) { Timeout = 15 };
        batch.BatchCommands.Add(command);
        batch.BatchCommands.Add(listingCommand);
        await using var reader = await batch.ExecuteReaderAsync(ct);
        var bars = new List<ScreenerBarEvidence>();
        while (await reader.ReadAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            if (bars.Count == ScreenerReadRequest.MaximumRows) return new(null, "BAR_BOUND_EXCEEDED");
            bars.Add(new(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetInt64(2),
                reader.GetFieldValue<DateTimeOffset>(3), reader.GetString(4), reader.GetGuid(5), reader.GetGuid(6),
                reader.GetString(7), reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9),
                reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
                reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
                reader.GetString(13), reader.GetString(14), reader.GetString(15), reader.GetString(16), reader.GetString(17),
                reader.GetInt64(18), reader.IsDBNull(19) ? null : reader.GetString(19), reader.GetString(20), reader.GetString(21), reader.GetString(22)));
        }
        await reader.NextResultAsync(ct);
        var listings = new List<ScreenerListingEvidence>();
        while (await reader.ReadAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            var id = reader.GetGuid(0);
            var known = reader.GetFieldValue<DateTimeOffset>(1);
            using var json = JsonDocument.Parse(reader.GetString(2));
            var data = json.RootElement;
            EvidenceResult<InstrumentBoundaryEvidence> resolution;
            try
            {
                InstrumentBoundaryEvidence? boundary;
                if (data.TryGetProperty("listed_from", out _))
                    boundary = data.Deserialize<InstrumentBoundaryEvidence>(ListingOptions);
                else
                {
                    // Legacy canonical assertions lack retrieval/name. Retain that limitation; never join the live registry.
                    var status = data.GetProperty("status").GetString();
                    boundary = new(id, data.GetProperty("symbol").GetString()!, id.ToString("D"),
                        data.GetProperty("listed_on").ValueKind == JsonValueKind.Null ? null :
                            data.GetProperty("listed_on").Deserialize<DateOnly>(), null, null,
                        status == "VERIFIED" ? "VERIFIED" : "UNKNOWN", "legacy-reference-register",
                        data.GetProperty("reference").GetString()!, "legacy listing register", null, known,
                        data.GetProperty("confidence").GetString()!, "legacy-1", "Original retrieval timestamp was not recorded.");
                }
                if (boundary is null || boundary.InstrumentId != id || boundary.KnownAt != known)
                    resolution = new(null, "LISTING_CONFLICT");
                else resolution = new(InstrumentBoundaries.AsOf([boundary], new(id), request.Cutoff), null);
            }
            catch (Exception error) when (error is ArgumentException or JsonException or InvalidOperationException or KeyNotFoundException)
            { resolution = new(null, "LISTING_INVALID"); }
            listings.Add(new(id, known, ScreenerReferences.Hash(data), resolution));
        }
        return new(new(bars, listings), null);
    }
}
