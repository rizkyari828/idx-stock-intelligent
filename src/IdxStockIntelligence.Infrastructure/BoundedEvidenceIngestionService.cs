using System.Data;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

// No production adapter is installed by default. Explicit reviewed source configuration is required.
public sealed class BoundedEvidenceIngestionService(string archiveRoot, IReadOnlyList<BoundedEvidenceSource> sources)
{
    private static NpgsqlCommand Command(string sql, NpgsqlConnection c, NpgsqlTransaction? t = null) => new(sql, c, t) { CommandTimeout = 15 };
    private static string ArchivePath(string root, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative)); var directory = new DirectoryInfo(Path.GetDirectoryName(path)!);
        while (directory is not null)
        {
            BoundedEvidenceIngestion.Require(directory.LinkTarget is null, "INGESTION_ARCHIVE_INVALID"); directory = directory.Parent;
        }
        BoundedEvidenceIngestion.Require(new FileInfo(path).LinkTarget is null, "INGESTION_ARCHIVE_INVALID"); return path;
    }
    public async Task<BoundedEvidenceIngestionResult> ImportAsync(NpgsqlConnection connection, UniverseSnapshot universe,
        Guid exchange, BoundedEvidenceArtifact artifact, Stream input, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(60)); var token = deadline.Token;
        BoundedEvidenceIngestionResult Result(string status, Guid? id, int inserted, int duplicates, params string[] reasons)
            => new(universe.SnapshotId, artifact.SourceId, status, id, id is null ? 0 : 1, id is null ? 1 : 0, inserted, duplicates,
                reasons.Contains("INGESTION_SOURCE_INADMISSIBLE") || reasons.Contains("INGESTION_OUTSIDE_UNIVERSE") || reasons.Contains("INGESTION_UNSUPPORTED_BINDING") ? 1 : 0,
                reasons.Contains("INGESTION_PARSE_FAILED") ? 1 : 0,
                reasons.Contains("ADMITTED_SOURCE_ADAPTER_MISSING") || reasons.Contains("INGESTION_SERVICE_UNAVAILABLE") || reasons.Contains("INGESTION_SOURCE_NOT_APPROVED") ? 1 : 0, reasons);
        var matches = sources.Where(s => s.SourceId == artifact.SourceId && s.ParserVersion == artifact.ParserVersion && s.ContentType == artifact.ContentType).ToArray();
        if (matches.Length != 1) return Result(BoundedEvidenceIngestion.SourceUnavailable, null, 0, 0, "ADMITTED_SOURCE_ADAPTER_MISSING");
        Guid? retained = null;
        try
        {
            using var memory = new MemoryStream(); var buffer = new byte[81920]; int n;
            while ((n = await input.ReadAsync(buffer, token)) > 0)
            {
                BoundedEvidenceIngestion.Require(memory.Length + n <= BoundedEvidenceIngestion.MaximumArtifactBytes, "INGESTION_BOUND_EXCEEDED");
                await memory.WriteAsync(buffer.AsMemory(0, n), token);
            }
            var bytes = memory.ToArray(); var rawId = BoundedEvidenceIngestion.Validate(universe, exchange, artifact, bytes, DateTimeOffset.UtcNow);
            await using (var admission = Command("SELECT terms_status FROM source WHERE source_id=$1", connection))
            {
                admission.Parameters.AddWithValue(artifact.SourceId);
                BoundedEvidenceIngestion.Require((string?)await admission.ExecuteScalarAsync(token) == "ALLOWED", "INGESTION_SOURCE_NOT_APPROVED");
            }
            var root = Path.GetFullPath(archiveRoot);
            // Check ancestors before the shared archiver writes, then authenticate the exact content-addressed destination.
            _ = ArchivePath(root, artifact.ContentSha256[..2] + "/" + artifact.ContentSha256 + ".bin");
            var archived = await new RawArtifactArchiver(root).ArchiveAsync(new MemoryStream(bytes, writable: false), ".bin", token);
            var path = ArchivePath(root, archived.RelativePath);
            await using var retainedStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            BoundedEvidenceIngestion.Require(retainedStream.Length == bytes.Length, "INGESTION_HASH_MISMATCH");
            var storedBytes = new byte[bytes.Length]; await retainedStream.ReadExactlyAsync(storedBytes, token);
            BoundedEvidenceIngestion.Require(retainedStream.ReadByte() == -1, "INGESTION_HASH_MISMATCH");
            BoundedEvidenceIngestion.Require(storedBytes.Length == bytes.Length && BoundedEvidenceIngestion.ContentHash(storedBytes) == artifact.ContentSha256, "INGESTION_HASH_MISMATCH");
            var parameters = JsonSerializer.Serialize(new { artifact.ContentType, artifact.KnownAt, artifact.PublishedAt }, ScreenerReferences.JsonOptions);
            await using (var rawTransaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token))
            {
                await using var run = Command("""
                    INSERT INTO ingestion_run(ingestion_run_id,source_id,started_at,completed_at,status,collector_version,request_parameters)
                    VALUES ($1,$2,$3,$3,'SUCCEEDED',$4,$5::jsonb) ON CONFLICT DO NOTHING;
                    """, connection, rawTransaction);
                run.Parameters.AddWithValue(rawId); run.Parameters.AddWithValue(artifact.SourceId); run.Parameters.AddWithValue(DateTimeOffset.UtcNow);
                run.Parameters.AddWithValue(artifact.ParserVersion); run.Parameters.AddWithValue(parameters); await run.ExecuteNonQueryAsync(token);
                await using var insert = Command("""
                    INSERT INTO raw_artifact(raw_artifact_id,ingestion_run_id,source_id,original_uri,request_parameters,fetched_at,local_uri,content_sha256,byte_length,parser_version)
                    VALUES ($1,$1,$2,$6,$5::jsonb,$3,$7,$8,$9,$4) ON CONFLICT DO NOTHING;
                    """, connection, rawTransaction);
                insert.Parameters.AddWithValue(rawId); insert.Parameters.AddWithValue(artifact.SourceId); insert.Parameters.AddWithValue(artifact.ReceivedAt);
                insert.Parameters.AddWithValue(artifact.ParserVersion); insert.Parameters.AddWithValue(parameters); insert.Parameters.AddWithValue(artifact.SourceReference);
                insert.Parameters.AddWithValue(path); insert.Parameters.AddWithValue(artifact.ContentSha256); insert.Parameters.AddWithValue((long)bytes.Length);
                await insert.ExecuteNonQueryAsync(token);
                await using var read = Command("SELECT raw_artifact_id,original_uri,fetched_at,local_uri,byte_length,parser_version,request_parameters=$3::jsonb FROM raw_artifact WHERE source_id=$1 AND content_sha256=$2", connection, rawTransaction);
                read.Parameters.AddWithValue(artifact.SourceId); read.Parameters.AddWithValue(artifact.ContentSha256); read.Parameters.AddWithValue(parameters);
                await using (var r = await read.ExecuteReaderAsync(token))
                    BoundedEvidenceIngestion.Require(await r.ReadAsync(token) && r.GetGuid(0) == rawId && r.GetString(1) == artifact.SourceReference
                        && r.GetFieldValue<DateTimeOffset>(2) == artifact.ReceivedAt && r.GetString(3) == path && r.GetInt64(4) == bytes.Length
                        && r.GetString(5) == artifact.ParserVersion && r.GetBoolean(6), "INGESTION_ARTIFACT_IDENTITY_CONFLICT");
                await rawTransaction.CommitAsync(token); retained = rawId;
            }
            // Parse only authenticated retained bytes. Parser/row failure leaves raw provenance but no partial semantic batch.
            IReadOnlyList<ScreenerEvidenceRecord> normalized;
            try { normalized = matches[0].Normalize(storedBytes, artifact, rawId, token); }
            catch (Exception e) when (e is JsonException or ArgumentException or KeyNotFoundException or FormatException or OverflowException or InvalidOperationException)
            { return Result("REJECTED", retained, 0, 0, "INGESTION_PARSE_FAILED"); }
            if (normalized is null) return Result("REJECTED", retained, 0, 0, "INGESTION_PARSE_FAILED");
            var rows = BoundedEvidenceIngestion.ValidateRows(universe, exchange, matches[0], artifact, rawId, normalized);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var inserted = 0; var duplicates = 0;
            var premises = rows.ToDictionary(r => r.EvidenceId);
            foreach (var row in rows)
            {
                if (row.SupersedesRevisionNumber is { } revision)
                {
                    await using var previous = Command("SELECT " + ScreenerEvidenceV02Store.SelectList + " FROM screener_evidence_record WHERE revision_series_id=$1 AND revision_number=$2", connection, transaction);
                    previous.Parameters.AddWithValue(row.RevisionSeriesId!); previous.Parameters.AddWithValue(revision);
                    ScreenerEvidenceRecord old;
                    await using (var reader = await previous.ExecuteReaderAsync(token))
                    {
                        BoundedEvidenceIngestion.Require(await reader.ReadAsync(token), "INGESTION_LINEAGE_INVALID"); old = ScreenerEvidenceV02Store.Read(reader);
                    }
                    foreach (var value in new[] { ScreenerEvidenceBinding.Decode(old).Value, ScreenerEvidenceBinding.Decode(row).Value }.OfType<PriceValue>())
                    {
                        var reference = await ScreenerEvidenceV02Store.ReadExactAsync(connection, transaction, value.ConventionEvidenceId, token);
                        BoundedEvidenceIngestion.Require(reference is not null, "INGESTION_LINEAGE_INVALID"); premises[value.ConventionEvidenceId] = reference!;
                    }
                    BoundedEvidenceIngestion.ValidateCorrection(row, old, premises);
                }
                var result = await ScreenerEvidenceV02Store.AppendAsync(connection, transaction, row, token);
                if (result.Disposition == EvidenceWriteDisposition.Inserted) inserted++; else duplicates++;
            }
            await transaction.CommitAsync(token); return Result("ACCEPTED", rawId, inserted, duplicates);
        }
        catch (EvidenceBindingException e) { return Result("REJECTED", retained, 0, 0, e.Reason switch
            {
                "INGESTION_UNIVERSE_INVALID" or "INGESTION_BOUND_EXCEEDED" or "INGESTION_PROVENANCE_INVALID" or "INGESTION_CHRONOLOGY_INVALID"
                or "INGESTION_HASH_MISMATCH" or "INGESTION_SOURCE_INADMISSIBLE" or "INGESTION_SOURCE_NOT_APPROVED" or "INGESTION_ARCHIVE_INVALID" or "INGESTION_ARTIFACT_IDENTITY_CONFLICT"
                or "INGESTION_LINEAGE_INVALID" or "INGESTION_IDENTITY_INVALID" or "INGESTION_DUPLICATE_IDENTITY" or "INGESTION_OUTSIDE_UNIVERSE" => e.Reason,
                "PERSISTED_EVIDENCE_UNBOUND" or "PERSISTED_EVIDENCE_BINDING_UNSUPPORTED" or "PERSISTED_EVIDENCE_DERIVED_CLAIM" => "INGESTION_UNSUPPORTED_BINDING",
                "PERSISTED_EVIDENCE_MALFORMED" => "INGESTION_PARSE_FAILED",
                "PERSISTED_EVIDENCE_BOUND_EXCEEDED" => "INGESTION_BOUND_EXCEEDED",
                _ => "INGESTION_INPUT_INVALID"
            }); }
        catch (JsonException) { return Result("REJECTED", retained, 0, 0, "INGESTION_PARSE_FAILED"); }
        catch (ArgumentException) { return Result("REJECTED", retained, 0, 0, "INGESTION_PARSE_FAILED"); }
        catch (InvalidOperationException) { return Result("REJECTED", retained, 0, 0, "INGESTION_IDENTITY_CONFLICT"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Result("REJECTED", retained, 0, 0, "INGESTION_SERVICE_UNAVAILABLE"); }
        catch (Exception e) when (e is NpgsqlException or IOException or UnauthorizedAccessException or TimeoutException)
        { ct.ThrowIfCancellationRequested(); return Result("REJECTED", retained, 0, 0, "INGESTION_SERVICE_UNAVAILABLE"); }
    }
}
