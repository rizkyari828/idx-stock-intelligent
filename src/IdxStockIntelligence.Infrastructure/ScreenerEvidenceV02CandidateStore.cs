using System.Data;
using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public sealed record ScreenerTechnicalCandidateWriteResult(ScreenerTechnicalCandidateDecision Decision, bool Created);

public static class ScreenerEvidenceTechnicalCandidateStore
{
    private static readonly JsonSerializerOptions Options = new(DecisionSnapshotService.JsonOptions) { RespectNullableAnnotations = true };

    public static async Task<ScreenerTechnicalCandidateWriteResult> PromoteAsync(NpgsqlConnection connection,
        Guid captureId, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60)); ct = deadline.Token;
        // The only input is an immutable stored capture. No readiness, evidence or technical execution queries.
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var capture = await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(connection, transaction, captureId, ct)
            ?? throw new EvidenceBindingException("TECHNICAL_CANDIDATE_CAPTURE_NOT_FOUND");
        var decision = ScreenerEvidenceTechnicalCandidates.Promote(capture, ct);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO screener_technical_candidate(decision_id,capture_id,candidate_policy_id,schema_version,replay_identity,projection)
            VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT DO NOTHING
            """, connection, transaction) { CommandTimeout = 15 };
        insert.Parameters.AddWithValue(decision.DecisionId); insert.Parameters.AddWithValue(captureId);
        insert.Parameters.AddWithValue(decision.Projection.CandidatePolicyId);
        insert.Parameters.AddWithValue((short)decision.Projection.SchemaVersion);
        insert.Parameters.AddWithValue(decision.ReplayIdentity);
        insert.Parameters.AddWithValue(JsonSerializer.Serialize(decision.Projection, Options));
        var created = await insert.ExecuteNonQueryAsync(ct) == 1;
        var stored = await ReadAsync(connection, transaction, decision.DecisionId, ct);
        if (stored is null || stored.ReplayIdentity != decision.ReplayIdentity)
            throw Conflict();
        await transaction.CommitAsync(ct);
        return new(stored, created);
    }

    public static async Task<ScreenerTechnicalCandidateDecision?> ReadAsync(NpgsqlConnection connection,
        NpgsqlTransaction? transaction, Guid decisionId, CancellationToken ct)
    {
        if (decisionId == Guid.Empty) throw new ArgumentException("A candidate decision identity is required.");
        Guid captureId; string policy; short schema; string replay; string json; DateTimeOffset recorded;
        await using (var command = new NpgsqlCommand("""
            SELECT capture_id,candidate_policy_id,schema_version,replay_identity,projection,recorded_at
            FROM screener_technical_candidate WHERE decision_id=$1
            """, connection, transaction) { CommandTimeout = 15 })
        {
            command.Parameters.AddWithValue(decisionId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            captureId = reader.GetGuid(0); policy = reader.GetString(1); schema = reader.GetInt16(2);
            replay = reader.GetString(3); json = reader.GetString(4); recorded = reader.GetFieldValue<DateTimeOffset>(5);
        }
        if (policy != ScreenerEvidenceTechnicalCandidates.PolicyId || schema != ScreenerEvidenceTechnicalCandidates.SchemaVersion)
            throw new EvidenceBindingException("TECHNICAL_CANDIDATE_POLICY_UNSUPPORTED");
        if (Encoding.UTF8.GetByteCount(json) > ScreenerEvidenceTechnicalCandidates.MaximumBytes)
            throw new EvidenceBindingException("TECHNICAL_CANDIDATE_BOUND_EXCEEDED");
        ScreenerTechnicalCandidateProjection projection;
        try { projection = JsonSerializer.Deserialize<ScreenerTechnicalCandidateProjection>(json, Options)
            ?? throw new EvidenceBindingException("TECHNICAL_CANDIDATE_MALFORMED"); }
        catch (JsonException) { throw new EvidenceBindingException("TECHNICAL_CANDIDATE_MALFORMED"); }
        var capture = await ScreenerEvidenceTechnicalCaptureStore.ReadAsync(connection, transaction, captureId, ct)
            ?? throw new EvidenceBindingException("TECHNICAL_CANDIDATE_CAPTURE_NOT_FOUND");
        var expected = ScreenerEvidenceTechnicalCandidates.Promote(capture, ct);
        if (expected.DecisionId != decisionId || expected.ReplayIdentity != replay
            || replay != ScreenerReferences.Hash(projection, ct)) throw Conflict();
        return new(decisionId, replay, projection, recorded);
    }

    private static InvalidOperationException Conflict() => new("Technical candidate integrity conflict: immutable identity has different content.");
}
