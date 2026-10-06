using IdxStockIntelligence.Application;
using Npgsql;
using NpgsqlTypes;
using System.Data;

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
        "effective_at, published_at, retrieved_at, known_at, recorded_at, source_id, source_reference, raw_artifact_id, payload, payload_sha256, evidence_class, payload_schema_version, scope_kind, scope_exchange_id";

    // Mechanical legacy round-trip only. Bound records must use the semantic append boundary.
    public static Task<EvidenceWriteResult> AppendMechanicalAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        ScreenerEvidenceRecord record, CancellationToken ct)
    {
        if (record.IsBound) throw new EvidenceBindingException("PERSISTED_EVIDENCE_MALFORMED");
        return AppendStoredAsync(connection, transaction, record, ct);
    }

    public static async Task<EvidenceWriteResult> AppendAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        ScreenerEvidenceRecord record, CancellationToken ct)
    {
        var payload = ScreenerEvidenceBinding.Decode(record);
        if (transaction is null)
        {
            await using var owned = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var result = await AppendAsync(connection, owned, record, ct);
            await owned.CommitAsync(ct);
            return result;
        }
        if (transaction.Connection != connection || transaction.IsolationLevel is not (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
            throw new ArgumentException("Semantic append requires one consistent transaction.");
        await ValidateReferencesAsync(connection, transaction, record, payload, ct);
        return await AppendStoredAsync(connection, transaction, record, ct);
    }

    private static async Task ValidateReferencesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ScreenerEvidenceRecord record, ScreenerEvidencePayload payload, CancellationToken ct)
    {
        const string missing = "PERSISTED_EVIDENCE_INPUT_UNAVAILABLE";
        static void Require(bool condition) { if (!condition) throw new EvidenceBindingException("PERSISTED_EVIDENCE_MALFORMED"); }
        ScreenerEvidencePayload Authenticate(ScreenerEvidenceRecord? target)
        {
            if (target is null) throw new EvidenceBindingException(missing);
            ScreenerEvidencePayload decoded;
            try { decoded = ScreenerEvidenceBinding.Decode(target); }
            catch (EvidenceBindingException) { throw new EvidenceBindingException(missing); }
            Require(decoded.Operation == EvidenceOperation.ASSERT && ScreenerEvidenceValidity.Visible(
                new EvidenceChronology(target.EffectiveAt, target.PublishedAt, target.RetrievedAt, target.KnownAt, target.RecordedAt), record.KnownAt));
            return decoded;
        }
        async Task<ScreenerEvidenceRecord> Exact(Guid id)
        {
            var target = await ReadExactAsync(connection, transaction, id, ct);
            Authenticate(target);
            return target!;
        }
        bool SameSubject(ScreenerEvidenceRecord target) => target.SubjectId == record.SubjectId && target.ScopeKind == record.ScopeKind
            && target.ScopeExchangeId == record.ScopeExchangeId;

        if (record.Claim == EvidenceClaim.GenuinePriceObservation)
        {
            await using var raw = new NpgsqlCommand("SELECT source_id, content_sha256, byte_length, fetched_at FROM raw_artifact WHERE raw_artifact_id=$1;", connection, transaction)
                { CommandTimeout = CommandTimeoutSeconds };
            raw.Parameters.AddWithValue(record.RawArtifactId!.Value);
            await using var reader = await raw.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new EvidenceBindingException(missing);
            Require(reader.GetString(0) == record.SourceId && ScreenerReferences.IsHash(reader.GetString(1))
                && reader.GetInt64(2) >= 0 && reader.GetFieldValue<DateTimeOffset>(3) == record.RetrievedAt);
        }
        if (payload.Operation == EvidenceOperation.CANCEL)
        {
            var target = await ReadSeriesRevisionAsync(connection, transaction, record.RevisionSeriesId!, record.SupersedesRevisionNumber!.Value, ct);
            Authenticate(target);
            Require(SameSubject(target!) && target!.Claim == record.Claim && target.EvidenceClass == record.EvidenceClass
                && target.SourceId == record.SourceId && target.RevisionSeriesId == record.RevisionSeriesId);
            return;
        }
        switch (payload.Value)
        {
            case ReopeningValue reopening:
                var suspension = await Exact(reopening.SuspensionEvidenceId);
                Require(suspension.Claim == EvidenceClaim.Suspension && SameSubject(suspension)
                    && suspension.EffectiveFrom <= reopening.ReopeningDate);
                break;
            case ActionCoverageValue coverage:
                foreach (var id in coverage.EventEvidenceIds)
                {
                    var action = await Exact(id);
                    Require(SameSubject(action) && ScreenerEvidenceBinding.Decode(action).Value is ActionEventValue
                        && action.EffectiveFrom >= record.EffectiveFrom && action.EffectiveFrom <= record.EffectiveTo);
                }
                break;
            case PriceValue price:
            {
                var convention = await Exact(price.ConventionEvidenceId);
                var session = await Exact(price.CompletedSessionEvidenceId);
                Require(ScreenerEvidenceBinding.Decode(convention).Value is ConventionValue cv && cv.PriceSourceId == record.SourceId
                    && convention.ScopeExchangeId == record.ScopeExchangeId
                    && (convention.ScopeKind == ScreenerScopeKind.EXCHANGE || convention.SubjectId == record.SubjectId)
                    && new EffectiveInterval(convention.EffectiveFrom, convention.EffectiveTo).Contains(record.EffectiveFrom));
                Require(ScreenerEvidenceBinding.Decode(session).Value is CompletedSessionValue sv && sv.SessionId == price.SessionId
                    && session.ScopeExchangeId == record.ScopeExchangeId && session.EffectiveFrom == record.EffectiveFrom);
                if (price.ZeroVolumeSemantics == EvidenceZeroVolumeSemantics.EXPLICITLY_GENUINE)
                {
                    var source = ScreenerEvidenceBinding.Decode(convention);
                    var completed = ScreenerEvidenceBinding.Decode(session);
                    var terms = (ConventionValue)source.Value!;
                    Require(source.Completeness == EvidenceCompleteness.FULL && completed.Completeness == EvidenceCompleteness.FULL
                        && ((CompletedSessionValue)completed.Value!).Completion == EvidenceCompletion.COMPLETED
                        && ScreenerSourceAdmission.Admit(convention.Claim, convention.AuthorityTier).Admitted
                        && ScreenerSourceAdmission.Admit(session.Claim, session.AuthorityTier).Admitted
                        && (price.ZeroVolumeProof == EvidenceZeroProof.DOCUMENTED_CONVENTION
                            ? terms.ZeroVolumeMeaning == EvidenceZeroMeaning.GENUINE_NO_EXECUTION
                            : terms.ZeroVolumeMeaning == EvidenceZeroMeaning.EXPLICIT_GENUINE_FLAG));
                }
                break;
            }
        }
    }

    private static async Task<EvidenceWriteResult> AppendStoredAsync(
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
                source_id, source_reference, raw_artifact_id, payload, payload_sha256,
                evidence_class, payload_schema_version, scope_kind, scope_exchange_id)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,$24)
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
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = record.EvidenceClass is { } cls ? ScreenerEvidenceBinding.ClassToken(cls) : DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Integer, Value = (object?)record.PayloadSchemaVersion ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = record.ScopeKind is { } scope ? scope.ToString() : DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)record.ScopeExchangeId ?? DBNull.Value });
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
        && left.PayloadSha256 == right.PayloadSha256
        && left.EvidenceClass == right.EvidenceClass && left.PayloadSchemaVersion == right.PayloadSchemaVersion
        && left.ScopeKind == right.ScopeKind && left.ScopeExchangeId == right.ScopeExchangeId;

    private static ScreenerScopeKind ParseScope(string value) =>
        Enum.TryParse<ScreenerScopeKind>(value, false, out var scope) && Enum.IsDefined(scope) && scope.ToString() == value
            ? scope : throw new EvidenceBindingException("PERSISTED_EVIDENCE_MALFORMED");

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
            reader.GetString(19),
            reader.IsDBNull(21) ? null : ScreenerEvidenceBinding.ParseClass(reader.GetString(21)),
            reader.IsDBNull(22) ? null : reader.GetInt32(22),
            reader.IsDBNull(23) ? null : ParseScope(reader.GetString(23)),
            reader.IsDBNull(24) ? null : reader.GetGuid(24));
        if (record.PayloadSha256 != reader.GetString(20) || record.Payload != reader.GetString(19))
            throw new EvidenceBindingException("PERSISTED_EVIDENCE_MALFORMED");
        return record with { RecordedAt = reader.GetFieldValue<DateTimeOffset>(15) };
    }
}
