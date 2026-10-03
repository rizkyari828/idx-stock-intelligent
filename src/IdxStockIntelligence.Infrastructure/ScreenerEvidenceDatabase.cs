using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public static class ScreenerEvidenceDatabase
{
    private static readonly JsonSerializerOptions ListingOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    // ponytail: fixed 211-ID/366-date/80,000-selected-row pilot; measure and review bounds before expansion.
    public static async Task<EvidenceResult<ScreenerDatabaseEvidence>> ReadAsync(NpgsqlConnection connection,
        NpgsqlTransaction? transaction, ScreenerReadRequest request, CancellationToken ct, DecisionSnapshotManifest? retained = null)
    {
        ct.ThrowIfCancellationRequested();
        if (request.BoundsReason() is { } reason) return new(null, reason);
        var command = new NpgsqlBatchCommand(retained is null ? """
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
            """ : """
            SELECT r.instrument_id,r.session_date,r.revision_number,r.known_at,r.canonical_content_sha256,
                r.ingestion_run_id,r.raw_artifact_id,a.source_id,a.content_sha256,a.fetched_at,r.retrieved_at,
                r.session_reference,r.session_known_at,r.quality_status,r.open::text,r.high::text,r.low::text,
                r.close::text,r.volume,r.adjusted_close::text,r.volume_unit,r.volume_basis,r.market_segment
            FROM jsonb_to_recordset($1::jsonb) AS k("instrumentId" uuid,"sessionDate" date,"revisionNumber" bigint)
            JOIN daily_bar_revision r ON (r.instrument_id,r.session_date,r.revision_number)=(k."instrumentId",k."sessionDate",k."revisionNumber")
            JOIN raw_artifact a USING(raw_artifact_id)
            ORDER BY r.instrument_id,r.session_date LIMIT 80001;
            """);
        var ids = request.InstrumentIds.Append(request.BenchmarkId).Distinct().ToArray();
        if (retained is null)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = ids });
            command.Parameters.Add(new NpgsqlParameter { Value = request.HistoryAnchor });
            command.Parameters.Add(new NpgsqlParameter { Value = request.Through });
            command.Parameters.Add(new NpgsqlParameter { Value = request.Cutoff.ToUniversalTime() });
        }
        else command.Parameters.Add(new NpgsqlParameter { Value = JsonSerializer.Serialize(retained.Bars, DecisionSnapshotService.JsonOptions) });
        var listingCommand = new NpgsqlBatchCommand(retained is null ? """
            SELECT DISTINCT ON (instrument_id) instrument_id,known_at,evidence::text
            FROM instrument_listing_evidence WHERE instrument_id=ANY($1) AND known_at <= $2
            ORDER BY instrument_id,known_at DESC;
            """ : """
            SELECT l.instrument_id,l.known_at,l.evidence::text
            FROM jsonb_to_recordset($1::jsonb) AS k("instrumentId" uuid,"knownAt" timestamptz)
            JOIN instrument_listing_evidence l ON (l.instrument_id,l.known_at)=(k."instrumentId",k."knownAt")
            ORDER BY l.instrument_id;
            """);
        if (retained is null)
        {
            listingCommand.Parameters.Add(new NpgsqlParameter { Value = ids });
            listingCommand.Parameters.Add(new NpgsqlParameter { Value = request.Cutoff.ToUniversalTime() });
        }
        else listingCommand.Parameters.Add(new NpgsqlParameter { Value = JsonSerializer.Serialize(retained.Listings, DecisionSnapshotService.JsonOptions) });
        await using var batch = new NpgsqlBatch(connection, transaction) { Timeout = 15 };
        batch.BatchCommands.Add(command);
        batch.BatchCommands.Add(listingCommand);
        await using var reader = await batch.ExecuteReaderAsync(ct);
        var bars = new List<ScreenerBarEvidence>();
        while (await reader.ReadAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            if (bars.Count == ScreenerReadRequest.MaximumRows) return new(null, "BAR_BOUND_EXCEEDED");
            bars.Add(ReadBar(reader));
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

    internal static ScreenerBarEvidence ReadBar(NpgsqlDataReader reader, int offset = 0) => new(reader.GetGuid(offset + 0), reader.GetFieldValue<DateOnly>(offset + 1), reader.GetInt64(offset + 2),
                reader.GetFieldValue<DateTimeOffset>(offset + 3), reader.GetString(offset + 4), reader.GetGuid(offset + 5), reader.GetGuid(offset + 6),
                reader.GetString(offset + 7), reader.GetString(offset + 8), reader.GetFieldValue<DateTimeOffset>(offset + 9),
                reader.IsDBNull(offset + 10) ? null : reader.GetFieldValue<DateTimeOffset>(offset + 10),
                reader.IsDBNull(offset + 11) ? null : reader.GetString(offset + 11), reader.IsDBNull(offset + 12) ? null : reader.GetFieldValue<DateTimeOffset>(offset + 12),
                reader.GetString(offset + 13), reader.GetString(offset + 14), reader.GetString(offset + 15), reader.GetString(offset + 16), reader.GetString(offset + 17),
                reader.GetInt64(offset + 18), reader.IsDBNull(offset + 19) ? null : reader.GetString(offset + 19), reader.GetString(offset + 20), reader.GetString(offset + 21), reader.GetString(offset + 22));
}
