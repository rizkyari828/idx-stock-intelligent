using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public static class OutcomeV02Store
{
    private static readonly JsonSerializerOptions Json = new(DecisionSnapshotService.JsonOptions) { RespectNullableAnnotations = true };
    private static NpgsqlCommand Command(string sql, NpgsqlConnection c, NpgsqlTransaction? t) => new(sql, c, t) { CommandTimeout = 15 };
    private static T Decode<T>(string json)
    {
        OutcomeV02.Require(Encoding.UTF8.GetByteCount(json) <= 2 * 1024 * 1024, "OUTCOME_PROJECTION_BOUND_EXCEEDED");
        try { return JsonSerializer.Deserialize<T>(json, Json) ?? throw new EvidenceBindingException("OUTCOME_PROJECTION_INVALID"); }
        catch (JsonException) { throw new EvidenceBindingException("OUTCOME_PROJECTION_INVALID"); }
    }
    private static async Task<DateTimeOffset> Clock(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    { await using var cmd = Command("SELECT clock_timestamp()", c, t); return new((DateTime)(await cmd.ExecuteScalarAsync(ct))!, TimeSpan.Zero); }
    public static async Task<OutcomeV02Enrollment?> ReadEnrollmentAsync(NpgsqlConnection c, NpgsqlTransaction? t, Guid id, CancellationToken ct)
    {
        await using var cmd = Command("SELECT enrollment_identity,binding_hash,projection,candidate_decision_id,capture_id,outcome_policy_id,schema_version,enrollment_known_at,enrollment_recorded_at,enrollment_deadline FROM outcome_v02_enrollment WHERE enrollment_id=$1", c, t);
        cmd.Parameters.AddWithValue(id); await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        var result = new OutcomeV02Enrollment(id, r.GetString(0), r.GetString(1), Decode<OutcomeV02EnrollmentProjection>(r.GetString(2)));
        OutcomeV02.Validate(result); var p = result.Projection;
        OutcomeV02.Require(p.CandidateDecisionId == r.GetGuid(3) && p.CaptureId == r.GetGuid(4) && p.OutcomePolicyId == r.GetString(5)
            && p.SchemaVersion == r.GetInt16(6) && p.EnrollmentKnownAt == r.GetFieldValue<DateTimeOffset>(7)
            && p.EnrollmentRecordedAt == r.GetFieldValue<DateTimeOffset>(8) && p.EnrollmentDeadline == r.GetFieldValue<DateTimeOffset>(9), "OUTCOME_ENROLLMENT_INTEGRITY_CONFLICT");
        return result;
    }
    public static async Task<OutcomeV02Observation?> ReadObservationAsync(NpgsqlConnection c, NpgsqlTransaction? t, Guid id, int horizon, CancellationToken ct)
    {
        await using var cmd = Command("SELECT observation_id,observation_identity,result_hash,projection,outcome_policy_id,schema_version,outcome_known_at,recorded_at FROM outcome_v02_observation WHERE enrollment_id=$1 AND horizon_sessions=$2", c, t);
        cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(horizon); await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        var result = new OutcomeV02Observation(r.GetGuid(0), r.GetString(1), r.GetString(2), Decode<OutcomeV02ObservationProjection>(r.GetString(3)));
        OutcomeV02.Validate(result); var p = result.Projection;
        OutcomeV02.Require(p.EnrollmentId == id && p.HorizonSessions == horizon && p.OutcomePolicyId == r.GetString(4) && p.SchemaVersion == r.GetInt16(5)
            && p.OutcomeKnownAt == r.GetFieldValue<DateTimeOffset>(6) && p.RecordedAt == r.GetFieldValue<DateTimeOffset>(7), "OUTCOME_OBSERVATION_INTEGRITY_CONFLICT");
        return result;
    }
    private static async Task<(ScreenerTechnicalCandidateDecision Candidate, ScreenerTechnicalCapture Capture)> Source(NpgsqlConnection c, NpgsqlTransaction t, Guid id, CancellationToken ct)
    {
        ScreenerTechnicalCandidateDecision candidate;
        await using (var cmd = Command("SELECT candidate_policy_id,schema_version,replay_identity,recorded_at,projection,capture_id FROM screener_technical_candidate WHERE decision_id=$1", c, t))
        {
            cmd.Parameters.AddWithValue(id); await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) throw new EvidenceBindingException("OUTCOME_CANDIDATE_NOT_FOUND");
            OutcomeV02.Require(r.GetString(0) == ScreenerEvidenceTechnicalCandidates.PolicyId && r.GetInt16(1) == 1, "OUTCOME_SOURCE_POLICY_UNSUPPORTED");
            var body = r.GetString(4); OutcomeV02.Require(Encoding.UTF8.GetByteCount(body) <= 8192, "OUTCOME_CANDIDATE_PROJECTION_BOUND_EXCEEDED");
            var projection = Decode<ScreenerTechnicalCandidateProjection>(body);
            OutcomeV02.Require(projection.SourceCaptureId == r.GetGuid(5), "OUTCOME_CANDIDATE_INTEGRITY_CONFLICT");
            candidate = new(id, r.GetString(2), projection, r.GetFieldValue<DateTimeOffset>(3));
        }
        var capture = await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(c, t, candidate.Projection.SourceCaptureId, ct)
            ?? throw new EvidenceBindingException("OUTCOME_CAPTURE_NOT_FOUND");
        return (candidate, capture); // No Promote call or live qualification.
    }
    private static async Task<OutcomeV02Evidence> Evidence(NpgsqlConnection c, NpgsqlTransaction t,
        IEnumerable<ScreenerEvidenceRecord> initial, string archiveRoot, CancellationToken ct)
    {
        var rows = initial.ToDictionary(r => r.EvidenceId); var queue = new Queue<ScreenerEvidenceRecord>(rows.Values); var seen = new HashSet<Guid>();
        var artifacts = new Dictionary<Guid, OutcomeV02Artifact>(); var root = Path.GetFullPath(archiveRoot) + Path.DirectorySeparatorChar; long total = 0;
        while (queue.TryDequeue(out var row))
        {
            ct.ThrowIfCancellationRequested();
            if (!seen.Add(row.EvidenceId)) continue;
            OutcomeV02.Require(rows.Count <= ScreenerEvidenceAsOf.MaximumRecords, ScreenerEvidenceAsOf.BoundExceeded);
            var payload = ScreenerEvidenceBinding.Decode(row);
            await ScreenerEvidenceV02Store.ValidateReferencesAsync(c, t, row, payload, ct);
            async Task Exact(Guid id)
            {
                if (!rows.TryGetValue(id, out var premise))
                { premise = await ScreenerEvidenceV02Store.ReadExactAsync(c, t, id, ct) ?? throw new EvidenceBindingException("OUTCOME_EXACT_EVIDENCE_MISSING"); rows.Add(id, premise); }
                queue.Enqueue(premise);
            }
            switch (payload.Value)
            {
                case PriceValue p: await Exact(p.ConventionEvidenceId); await Exact(p.CompletedSessionEvidenceId); break;
                case ReopeningValue r: await Exact(r.SuspensionEvidenceId); break;
                case ActionCoverageValue a: foreach (var id in a.EventEvidenceIds) await Exact(id); break;
            }
            if (payload.Operation == EvidenceOperation.CANCEL)
            {
                await using var cmd = Command("SELECT " + ScreenerEvidenceV02Store.SelectList + " FROM screener_evidence_record WHERE revision_series_id=$1 AND revision_number=$2", c, t);
                cmd.Parameters.AddWithValue(row.RevisionSeriesId!); cmd.Parameters.AddWithValue(row.SupersedesRevisionNumber!.Value);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct)) throw new EvidenceBindingException("OUTCOME_EXACT_EVIDENCE_MISSING");
                var original = ScreenerEvidenceV02Store.Read(r); rows.TryAdd(original.EvidenceId, original); queue.Enqueue(original);
            }
            if (row.RawArtifactId is not { } rawId || artifacts.ContainsKey(rawId)) continue;
            OutcomeV02Artifact artifact; string uri;
            await using (var cmd = Command("SELECT source_id,content_sha256,byte_length,fetched_at,local_uri FROM raw_artifact WHERE raw_artifact_id=$1", c, t))
            {
                cmd.Parameters.AddWithValue(rawId); await using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct)) throw new EvidenceBindingException("OUTCOME_RAW_ARTIFACT_MISSING");
                artifact = new(rawId, r.GetString(0), r.GetString(1), r.GetInt64(2), r.GetFieldValue<DateTimeOffset>(3)); uri = r.GetString(4);
            }
            OutcomeV02.Require(artifact.ByteLength >= 0 && (total += artifact.ByteLength) <= ScreenerReferences.MaximumBytes
                && ScreenerReferences.IsHash(artifact.ContentHash), "OUTCOME_ARCHIVE_BOUND_INVALID");
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            { OutcomeV02.Require(parsed.IsFile && string.IsNullOrEmpty(parsed.Host), "OUTCOME_ARCHIVE_IDENTITY_INVALID"); uri = parsed.LocalPath; }
            var path = Path.GetFullPath(Path.IsPathRooted(uri) ? uri : Path.Combine(root, uri));
            OutcomeV02.Require(path.StartsWith(root, StringComparison.Ordinal), "OUTCOME_ARCHIVE_IDENTITY_INVALID");
            // Retained archive identity cannot escape the configured root through a symlink.
            for (var info = new FileInfo(path) as FileSystemInfo; info is not null && info.FullName.StartsWith(root, StringComparison.Ordinal);
                info = Directory.GetParent(info.FullName))
                OutcomeV02.Require(info.LinkTarget is null, "OUTCOME_ARCHIVE_IDENTITY_INVALID");
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
                OutcomeV02.Require(stream.Length == artifact.ByteLength, "OUTCOME_ARCHIVE_LENGTH_MISMATCH");
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[8192]; long length = 0; int read;
                while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                { length += read; OutcomeV02.Require(length <= artifact.ByteLength, "OUTCOME_ARCHIVE_LENGTH_MISMATCH"); hasher.AppendData(buffer, 0, read); }
                OutcomeV02.Require(length == artifact.ByteLength && Convert.ToHexStringLower(hasher.GetHashAndReset()) == artifact.ContentHash, "OUTCOME_ARCHIVE_HASH_MISMATCH");
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) { throw new EvidenceBindingException("OUTCOME_ARCHIVE_MISSING"); }
            artifacts.Add(rawId, artifact);
        }
        var result = new OutcomeV02Evidence(rows.Values.OrderBy(r => r.EvidenceId.ToString("D"), StringComparer.Ordinal).ToArray(), artifacts.Values.OrderBy(a => a.ArtifactId.ToString("D"), StringComparer.Ordinal).ToArray());
        OutcomeV02.Authenticate(result); return result;
    }
    private static async Task<OutcomeV02Evidence> CaptureEvidence(NpgsqlConnection c, NpgsqlTransaction t, ScreenerTechnicalCapture capture, string root, CancellationToken ct)
    {
        var rows = new List<ScreenerEvidenceRecord>();
        foreach (var link in capture.Projection.Result.References)
        {
            var row = await ScreenerEvidenceV02Store.ReadExactAsync(c, t, link.EvidenceId, ct);
            OutcomeV02.Require(row is not null && ScreenerEvidenceAsOf.Provenance(row) == link, "OUTCOME_CAPTURE_EVIDENCE_UNAVAILABLE"); rows.Add(row!);
        }
        return await Evidence(c, t, rows, root, ct);
    }
    public static async Task<(OutcomeV02Enrollment Enrollment, bool Created)> EnrollAsync(NpgsqlConnection c, Guid candidateId, string archiveRoot, CancellationToken ct)
    {
        OutcomeV02.Require(candidateId != Guid.Empty, "OUTCOME_CANDIDATE_ID_INVALID");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(60)); ct = deadline.Token;
        for (var attempt = 0; ; attempt++)
        {
            await using var t = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            try
            {
                var known = await Clock(c, t, ct); var identity = OutcomeV02.EnrollmentIdentity(candidateId); var id = ScreenerEvidenceTechnicalCapture.Id(identity);
                var existing = await ReadEnrollmentAsync(c, t, id, ct); var source = await Source(c, t, candidateId, ct);
                var evidence = await CaptureEvidence(c, t, source.Capture, archiveRoot, ct);
                var result = OutcomeV02.Enroll(source.Candidate, source.Capture, evidence,
                    existing?.Projection.EnrollmentKnownAt ?? known, existing?.Projection.EnrollmentRecordedAt ?? await Clock(c, t, ct));
                if (existing is not null)
                {
                    OutcomeV02.Require(existing.BindingHash == result.BindingHash && existing.EnrollmentIdentity == result.EnrollmentIdentity, "OUTCOME_ENROLLMENT_INTEGRITY_CONFLICT");
                    await t.CommitAsync(ct); return (existing, false);
                }
                await using var insert = Command("""
                    INSERT INTO outcome_v02_enrollment(enrollment_id,enrollment_identity,binding_hash,candidate_decision_id,capture_id,
                        outcome_policy_id,schema_version,enrollment_known_at,enrollment_recorded_at,enrollment_deadline,projection)
                    VALUES ($1,$2,$3,$4,$5,'outcome-v0.2.0',1,$6,$7,$8,$9)
                    """, c, t);
                insert.Parameters.AddWithValue(result.EnrollmentId); insert.Parameters.AddWithValue(result.EnrollmentIdentity); insert.Parameters.AddWithValue(result.BindingHash);
                insert.Parameters.AddWithValue(candidateId); insert.Parameters.AddWithValue(source.Capture.CaptureId);
                insert.Parameters.AddWithValue(result.Projection.EnrollmentKnownAt); insert.Parameters.AddWithValue(result.Projection.EnrollmentRecordedAt);
                insert.Parameters.AddWithValue(result.Projection.EnrollmentDeadline); insert.Parameters.AddWithValue(JsonSerializer.Serialize(result.Projection, Json));
                await insert.ExecuteNonQueryAsync(ct); OutcomeV02.CheckDeadline(await Clock(c, t, ct), result.Projection.EnrollmentDeadline);
                await t.CommitAsync(ct); return (result, true);
            }
            catch (PostgresException e) when (attempt == 0 && e.SqlState is "23505" or "40001") { await t.RollbackAsync(ct); }
        }
    }
    public static async Task<OutcomeV02Assessment> AssessAsync(NpgsqlConnection c, Guid enrollmentId, int horizon, string archiveRoot, CancellationToken ct)
    {
        OutcomeV02.Require(enrollmentId != Guid.Empty && OutcomeRequest.Horizons.Contains(horizon), "OUTCOME_OBSERVATION_REQUEST_INVALID");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(60)); ct = deadline.Token;
        for (var attempt = 0; ; attempt++)
        {
            await using var t = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            try
            {
                var saved = await ReadObservationAsync(c, t, enrollmentId, horizon, ct);
                if (saved is not null) { await t.CommitAsync(ct); return new(saved.Projection.State, saved.Projection.Reason, saved); }
                var known = await Clock(c, t, ct);
                var enrollment = await ReadEnrollmentAsync(c, t, enrollmentId, ct) ?? throw new EvidenceBindingException("OUTCOME_ENROLLMENT_NOT_FOUND"); var p = enrollment.Projection;
                var source = await Source(c, t, p.CandidateDecisionId, ct); var captured = await CaptureEvidence(c, t, source.Capture, archiveRoot, ct);
                var authenticated = OutcomeV02.Enroll(source.Candidate, source.Capture, captured, p.EnrollmentKnownAt, p.EnrollmentRecordedAt);
                OutcomeV02.Require(enrollment.BindingHash == authenticated.BindingHash, "OUTCOME_ENROLLMENT_INTEGRITY_CONFLICT");
                var through = OutcomeEvaluator.Through(known); if (through > ScreenerReadRequest.Horizon) through = ScreenerReadRequest.Horizon;
                var rows = new List<ScreenerEvidenceRecord>();
                await using (var cmd = Command("SELECT " + ScreenerEvidenceV02Store.SelectList + " " + """
                    FROM screener_evidence_record WHERE (subject_id=$1 OR (scope_kind='EXCHANGE' AND subject_id=$2))
                        AND known_at <= $3 AND (retrieved_at IS NULL OR retrieved_at <= $3) AND (published_at IS NULL OR published_at <= $3)
                        AND effective_from <= $4 AND (effective_to IS NULL OR effective_to >= $5)
                    ORDER BY evidence_id LIMIT 513
                    """, c, t))
                {
                    cmd.Parameters.AddWithValue(p.InstrumentId); cmd.Parameters.AddWithValue(p.ExchangeId); cmd.Parameters.AddWithValue(known);
                    cmd.Parameters.AddWithValue(through); cmd.Parameters.AddWithValue(p.EvaluationDate);
                    await using var reader = await cmd.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) rows.Add(ScreenerEvidenceV02Store.Read(reader));
                }
                var evidence = await Evidence(c, t, rows, archiveRoot, ct); var result = OutcomeV02.Assess(enrollment, horizon, evidence, known, await Clock(c, t, ct));
                if (result.Observation is { } observation)
                {
                    OutcomeV02.Validate(observation); var o = observation.Projection;
                    await using var insert = Command("""
                        INSERT INTO outcome_v02_observation(enrollment_id,horizon_sessions,observation_id,observation_identity,result_hash,
                            outcome_policy_id,schema_version,outcome_known_at,recorded_at,projection)
                        VALUES ($1,$2,$3,$4,$5,'outcome-v0.2.0',1,$6,$7,$8)
                        """, c, t);
                    insert.Parameters.AddWithValue(enrollmentId); insert.Parameters.AddWithValue(horizon); insert.Parameters.AddWithValue(observation.ObservationId);
                    insert.Parameters.AddWithValue(observation.ObservationIdentity); insert.Parameters.AddWithValue(observation.ResultHash);
                    insert.Parameters.AddWithValue(o.OutcomeKnownAt); insert.Parameters.AddWithValue(o.RecordedAt); insert.Parameters.AddWithValue(JsonSerializer.Serialize(o, Json));
                    await insert.ExecuteNonQueryAsync(ct);
                }
                await t.CommitAsync(ct); return result;
            }
            catch (PostgresException e) when (attempt == 0 && e.SqlState is "23505" or "40001") { await t.RollbackAsync(ct); }
        }
    }
}
