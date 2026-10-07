using System.Data;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public sealed class OutcomeV02VerificationService(NpgsqlDataSource dataSource, string archiveRoot)
{
    private static NpgsqlCommand Command(string sql, NpgsqlConnection c, NpgsqlTransaction t) => new(sql, c, t) { CommandTimeout = 15 };
    private static async Task<OutcomeV02Evidence> Exact(NpgsqlConnection c, NpgsqlTransaction t,
        IReadOnlyList<ScreenerEvidenceProvenance> references, IReadOnlyList<OutcomeV02Artifact> artifacts, string root, CancellationToken ct)
    {
        OutcomeV02.Require(references.Count <= ScreenerEvidenceAsOf.MaximumRecords && references.Select(r => r.EvidenceId).Distinct().Count() == references.Count,
            "RETAINED_INPUT_INVALID");
        var rows = new List<ScreenerEvidenceRecord>();
        foreach (var reference in references)
        {
            var row = await ScreenerEvidenceV02Store.ReadExactAsync(c, t, reference.EvidenceId, ct);
            OutcomeV02.Require(row is not null && ScreenerEvidenceAsOf.Provenance(row) == reference, "RETAINED_INPUT_INVALID"); rows.Add(row!);
        }
        // Validate the closed set before shared authentication can traverse any premise.
        OutcomeV02Verification.Closed(new(rows, artifacts));
        var evidence = await OutcomeV02Store.Evidence(c, t, rows, root, ct);
        OutcomeV02.Require(ScreenerReferences.Hash(evidence.Artifacts) == ScreenerReferences.Hash(artifacts), "RETAINED_INPUT_INVALID");
        return evidence;
    }
    public async Task<OutcomeV02VerificationResult> VerifyAsync(Guid enrollmentId, int horizon, CancellationToken ct)
    {
        if (enrollmentId == Guid.Empty || !OutcomeRequest.Horizons.Contains(horizon))
            throw new ScreenerException(400, "OUTCOME_VERIFICATION_REQUEST_INVALID");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(60)); var token = deadline.Token;
        var verifiedAt = DateTimeOffset.UtcNow;
        try
        {
            await using var c = await dataSource.OpenConnectionAsync(token);
            await using var t = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            await using (var readOnly = Command("SET TRANSACTION READ ONLY", c, t)) await readOnly.ExecuteNonQueryAsync(token);
            string policy; int schema; Guid observation; DateTimeOffset known, recorded;
            bool unsupported, missing;
            await using (var header = Command("""
                SELECT o.observation_id,o.outcome_policy_id,o.schema_version,o.outcome_known_at,o.recorded_at,
                    e.outcome_policy_id,e.schema_version,d.candidate_policy_id,d.schema_version,
                    c.policy_id,c.schema_version,c.capture_schema_version
                FROM outcome_v02_observation o LEFT JOIN outcome_v02_enrollment e USING(enrollment_id)
                LEFT JOIN screener_technical_candidate d ON d.decision_id=e.candidate_decision_id
                LEFT JOIN screener_technical_capture c ON c.capture_id=e.capture_id
                WHERE o.enrollment_id=$1 AND o.horizon_sessions=$2
                """, c, t))
            {
                header.Parameters.AddWithValue(enrollmentId); header.Parameters.AddWithValue(horizon);
                await using var reader = await header.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new ScreenerException(404, "OUTCOME_NOT_FOUND");
                observation = reader.GetGuid(0); policy = reader.GetString(1); schema = reader.GetInt16(2);
                known = reader.GetFieldValue<DateTimeOffset>(3); recorded = reader.GetFieldValue<DateTimeOffset>(4);
                missing = Enumerable.Range(5, 7).Any(reader.IsDBNull);
                unsupported = policy != OutcomeV02.PolicyId || schema != 1
                    || !reader.IsDBNull(5) && (reader.GetString(5) != OutcomeV02.PolicyId || reader.GetInt16(6) != 1)
                    || !reader.IsDBNull(7) && (reader.GetString(7) != ScreenerEvidenceTechnicalCandidates.PolicyId || reader.GetInt16(8) != 1)
                    || !reader.IsDBNull(9) && (reader.GetString(9) != ScreenerEvidenceV02.PolicyId || reader.GetInt16(10) != 1 || reader.GetInt16(11) != 1);
            }
            OutcomeV02VerificationResult Fail(OutcomeVerificationState state, string detail) => OutcomeV02Verification.Unavailable(enrollmentId,
                observation, horizon, policy, schema, known, recorded, verifiedAt, state, detail);
            if (unsupported) return Fail(OutcomeVerificationState.PolicyVersionUnavailable, "POLICY_OR_SCHEMA_UNSUPPORTED");
            if (missing) return Fail(OutcomeVerificationState.InputNotAvailable, "RETAINED_INPUT_INVALID");
            try
            {
                var stored = await OutcomeV02Store.ReadObservationAsync(c, t, enrollmentId, horizon, token);
                var enrollment = await OutcomeV02Store.ReadEnrollmentAsync(c, t, enrollmentId, token);
                OutcomeV02.Require(stored is not null && enrollment is not null, "RETAINED_INPUT_INVALID");
                var source = await OutcomeV02Store.Source(c, t, enrollment!.Projection.CandidateDecisionId, token);
                var captured = await Exact(c, t, source.Capture.Projection.Result.References, enrollment.Projection.Artifacts, archiveRoot, token);
                var forward = await Exact(c, t, stored!.Projection.Manifest.Evidence, stored.Projection.Manifest.Artifacts, archiveRoot, token);
                var result = OutcomeV02Verification.Verify(stored, enrollment, source.Candidate, source.Capture, captured, forward, verifiedAt);
                await t.CommitAsync(token); return result;
            }
            catch (EvidenceBindingException) { return Fail(OutcomeVerificationState.InputNotAvailable, "RETAINED_INPUT_INVALID"); }
            catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException or OverflowException or NullReferenceException)
            { return Fail(OutcomeVerificationState.InputNotAvailable, "RETAINED_INPUT_INVALID"); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ScreenerException(503, "OUTCOME_VERIFICATION_UNAVAILABLE"); }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException or IOException or UnauthorizedAccessException)
        { ct.ThrowIfCancellationRequested(); throw new ScreenerException(503, "OUTCOME_VERIFICATION_UNAVAILABLE"); }
    }
}
