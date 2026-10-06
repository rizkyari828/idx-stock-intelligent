using IdxStockIntelligence.Application;
using Npgsql;
using NpgsqlTypes;

namespace IdxStockIntelligence.Infrastructure;

public enum EvidenceWriteDisposition
{
    Inserted = 0,
    DuplicateIgnored = 1
}

public sealed record EvidenceWriteResult(EvidenceWriteDisposition Disposition, Guid EvidenceId);

public static class ScreenerEvidenceV02Store
{
    public const int CommandTimeoutSeconds = 15;

    private const string SelectList = "evidence_id, subject_id, claim, policy_id, schema_version, " +
        "revision_series_id, revision_number, supersedes_revision_number, authority_tier, effective_from, effective_to, " +
        "effective_at, published_at, retrieved_at, known_at, recorded_at, source_id, source_reference, raw_artifact_id, payload";

    public static async Task<EvidenceWriteResult> AppendAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        ScreenerEvidenceRecord record,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(record);
        ct.ThrowIfCancellationRequested();

        var byIdentity = await ReadExactAsync(connection, transaction, record.EvidenceId, ct).ConfigureAwait(false);
        if (byIdentity is not null)
        {
            return ResolveExisting(byIdentity, record);
        }

        if (record.RevisionSeriesId is { } series)
        {
            var bySeries = await ReadSeriesRevisionAsync(connection, transaction, series, record.RevisionNumber, ct).ConfigureAwait(false);
            if (bySeries is not null)
            {
                return ResolveExisting(bySeries, record);
            }
        }

        var inserted = await InsertAsync(connection, transaction, record, ct).ConfigureAwait(false);
        if (inserted == 1)
        {
            return new(EvidenceWriteDisposition.Inserted, record.EvidenceId);
        }

        // A concurrent writer won a uniqueness race; resolve deterministically against the retained row.
        var raced = await ReadExactAsync(connection, transaction, record.EvidenceId, ct).ConfigureAwait(false);
        if (raced is null && record.RevisionSeriesId is { } racedSeries)
        {
            raced = await ReadSeriesRevisionAsync(connection, transaction, racedSeries, record.RevisionNumber, ct).ConfigureAwait(false);
        }

        return raced is null
            ? throw new InvalidOperationException("Screener evidence record was not inserted and no retained record was found.")
            : ResolveExisting(raced, record);
    }

    public static async Task<ScreenerEvidenceRecord?> ReadExactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid evidenceId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var command = new NpgsqlCommand(
            "SELECT " + SelectList + " FROM screener_evidence_record WHERE evidence_id = $1;",
            connection,
            transaction) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = evidenceId });
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    public static async Task<IReadOnlyList<ScreenerEvidenceRecord>> ReadSeriesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string revisionSeriesId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(revisionSeriesId);
        await using var command = new NpgsqlCommand(
            "SELECT " + SelectList + " FROM screener_evidence_record WHERE revision_series_id = $1 ORDER BY revision_number ASC;",
            connection,
            transaction) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = revisionSeriesId });
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var records = new List<ScreenerEvidenceRecord>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            records.Add(Read(reader));
        }

        return records;
    }

    private static async Task<ScreenerEvidenceRecord?> ReadSeriesRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string revisionSeriesId,
        long revisionNumber,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT " + SelectList + " FROM screener_evidence_record WHERE revision_series_id = $1 AND revision_number = $2;",
            connection,
            transaction) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = revisionSeriesId });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint, Value = revisionNumber });
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    private static async Task<int> InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        ScreenerEvidenceRecord record,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO screener_evidence_record(
                evidence_id, subject_id, claim, policy_id, schema_version,
                revision_series_id, revision_number, supersedes_revision_number,
                authority_tier, effective_from, effective_to,
                effective_at, published_at, retrieved_at, known_at,
                source_id, source_reference, raw_artifact_id, payload, payload_sha256)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)
            ON CONFLICT DO NOTHING;
            """, connection, transaction) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = record.EvidenceId });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = record.SubjectId });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = record.Claim.ToString() });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = record.PolicyId });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Smallint, Value = (short)record.SchemaVersion });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)record.RevisionSeriesId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint, Value = record.RevisionNumber });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint, Value = (object?)record.SupersedesRevisionNumber ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Smallint, Value = (short)((int)record.AuthorityTier + 1) });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Date, Value = record.EffectiveFrom });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Date, Value = (object?)record.EffectiveTo ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = (object?)record.EffectiveAt ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = (object?)record.PublishedAt ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = (object?)record.RetrievedAt ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = record.KnownAt });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = record.SourceId });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)record.SourceReference ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)record.RawArtifactId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = record.Payload });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = record.PayloadSha256 });
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static EvidenceWriteResult ResolveExisting(ScreenerEvidenceRecord existing, ScreenerEvidenceRecord incoming)
    {
        return ContentEquals(existing, incoming)
            ? new(EvidenceWriteDisposition.DuplicateIgnored, existing.EvidenceId)
            : throw new InvalidOperationException("Screener evidence identity conflict: the same immutable identity already exists with different content.");
    }

    private static bool ContentEquals(ScreenerEvidenceRecord left, ScreenerEvidenceRecord right) =>
        left.SubjectId == right.SubjectId
        && left.Claim == right.Claim
        && left.PolicyId == right.PolicyId
        && left.SchemaVersion == right.SchemaVersion
        && left.RevisionSeriesId == right.RevisionSeriesId
        && left.RevisionNumber == right.RevisionNumber
        && left.SupersedesRevisionNumber == right.SupersedesRevisionNumber
        && left.AuthorityTier == right.AuthorityTier
        && left.EffectiveFrom == right.EffectiveFrom
        && left.EffectiveTo == right.EffectiveTo
        && left.EffectiveAt == right.EffectiveAt
        && left.PublishedAt == right.PublishedAt
        && left.RetrievedAt == right.RetrievedAt
        && left.KnownAt == right.KnownAt
        && left.SourceId == right.SourceId
        && left.SourceReference == right.SourceReference
        && left.RawArtifactId == right.RawArtifactId
        && left.PayloadSha256 == right.PayloadSha256;

    private static ScreenerEvidenceRecord Read(NpgsqlDataReader reader)
    {
        var record = new ScreenerEvidenceRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            Enum.Parse<EvidenceClaim>(reader.GetString(2)),
            reader.GetString(3),
            reader.GetInt16(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetInt64(6),
            reader.IsDBNull(7) ? null : reader.GetInt64(7),
            (SourceAuthorityTier)(reader.GetInt16(8) - 1),
            reader.GetFieldValue<DateOnly>(9),
            reader.IsDBNull(10) ? null : reader.GetFieldValue<DateOnly>(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
            reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
            reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
            reader.GetFieldValue<DateTimeOffset>(14),
            reader.GetString(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetGuid(18),
            reader.GetString(19));
        return record with { RecordedAt = reader.GetFieldValue<DateTimeOffset>(15) };
    }
}
