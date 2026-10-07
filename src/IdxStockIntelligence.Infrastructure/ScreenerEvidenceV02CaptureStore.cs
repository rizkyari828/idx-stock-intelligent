using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public sealed record ScreenerTechnicalCaptureWriteResult(ScreenerTechnicalCapture Capture, bool Created);

public static class ScreenerEvidenceTechnicalCaptureStore
{
    // Reuse the snapshot codec/hash conventions; text retains exact decimal payload spelling.
    private static readonly JsonSerializerOptions Options = new(DecisionSnapshotService.JsonOptions) { RespectNullableAnnotations = true };
    private static ScreenerTechnicalCaptureProjection Decode(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > ScreenerEvidenceTechnicalCapture.MaximumBytes)
            throw new EvidenceBindingException("TECHNICAL_CAPTURE_BOUND_EXCEEDED");
        try { return JsonSerializer.Deserialize<ScreenerTechnicalCaptureProjection>(json, Options)
            ?? throw new EvidenceBindingException("TECHNICAL_CAPTURE_MALFORMED"); }
        catch (JsonException) { throw new EvidenceBindingException("TECHNICAL_CAPTURE_MALFORMED"); }
    }

    public static async Task<ScreenerTechnicalCaptureWriteResult> CaptureAsync(NpgsqlConnection connection,
        ScreenerReadinessRequest request, CancellationToken ct)
    {
        request.Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        ScreenerTechnicalCapture capture;
        await using (var snapshot = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, deadline.Token))
        {
            await using (var ro = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, snapshot) { CommandTimeout = 15 })
                await ro.ExecuteNonQueryAsync(deadline.Token);
            var input = await ScreenerEvidenceReadinessService.ReadForTechnicalAsync(connection, snapshot, request, deadline.Token);
            capture = ScreenerEvidenceTechnicalCapture.Create(input.Current, input.History, deadline.Token);
            await snapshot.CommitAsync(deadline.Token);
        }
        // All evidence was bound in the read-only snapshot. The write only appends that frozen output;
        // it never reselects evidence between the evaluation and INSERT.
        return await AppendAsync(connection, capture, deadline.Token);
    }

    public static async Task<ScreenerTechnicalCaptureWriteResult> AppendAsync(NpgsqlConnection connection,
        ScreenerTechnicalCapture capture, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60)); ct = deadline.Token;
        // Freeze caller-owned collections before the first asynchronous boundary.
        var json = JsonSerializer.Serialize(capture.Projection, Options);
        capture = capture with { Projection = Decode(json), RecordedAt = null };
        ScreenerEvidenceTechnicalCapture.Validate(capture, ct);
        var request = capture.Projection.Result.Request;
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        foreach (var reference in capture.Projection.Result.References)
        {
            var row = await ScreenerEvidenceV02Store.ReadExactAsync(connection, transaction, reference.EvidenceId, ct);
            if (row is null || ScreenerEvidenceAsOf.Provenance(row) != reference)
                throw new EvidenceBindingException("TECHNICAL_CAPTURE_INPUT_UNAVAILABLE");
            ScreenerEvidenceBinding.Decode(row);
        }
        await using var insert = new NpgsqlCommand("""
            INSERT INTO screener_technical_capture(capture_id,input_hash,result_hash,subject_id,policy_id,schema_version,
                capture_schema_version,session_id,evaluation_date,knowledge_cutoff,projection)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11) ON CONFLICT DO NOTHING
            """, connection, transaction) { CommandTimeout = 15 };
        insert.Parameters.AddWithValue(capture.CaptureId); insert.Parameters.AddWithValue(capture.InputHash);
        insert.Parameters.AddWithValue(capture.ResultHash); insert.Parameters.AddWithValue(request.SubjectId);
        insert.Parameters.AddWithValue(request.PolicyId); insert.Parameters.AddWithValue((short)request.SchemaVersion);
        insert.Parameters.AddWithValue((short)capture.Projection.CaptureSchemaVersion); insert.Parameters.AddWithValue(request.SessionId);
        insert.Parameters.AddWithValue(request.EvaluationDate);
        insert.Parameters.AddWithValue(JsonSerializer.Serialize(request.Cutoff, Options)[1..^1]);
        insert.Parameters.AddWithValue(json);
        var created = await insert.ExecuteNonQueryAsync(ct) == 1;
        var stored = await ReadAsync(connection, transaction, capture.CaptureId, ct);
        if (stored is null || stored.InputHash != capture.InputHash || stored.ResultHash != capture.ResultHash)
            throw new InvalidOperationException("Technical capture integrity conflict: immutable identity has different content.");
        await transaction.CommitAsync(ct);
        return new(stored, created);
    }

    public static async Task<ScreenerTechnicalCapture?> ReadAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        Guid captureId, CancellationToken ct)
    {
        if (captureId == Guid.Empty) throw new ArgumentException("A capture identity is required.");
        await using var command = new NpgsqlCommand("""
            SELECT input_hash,result_hash,recorded_at,projection,subject_id,policy_id,schema_version,capture_schema_version,
                session_id,evaluation_date,knowledge_cutoff FROM screener_technical_capture WHERE capture_id=$1
            """, connection, transaction) { CommandTimeout = 15 };
        command.Parameters.AddWithValue(captureId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var capture = new ScreenerTechnicalCapture(captureId, reader.GetString(0), reader.GetString(1),
            Decode(reader.GetString(3)), reader.GetFieldValue<DateTimeOffset>(2));
        ScreenerEvidenceTechnicalCapture.Validate(capture, ct);
        var request = capture.Projection.Result.Request;
        if (request.SubjectId != reader.GetGuid(4) || request.PolicyId != reader.GetString(5) || request.SchemaVersion != reader.GetInt16(6)
            || capture.Projection.CaptureSchemaVersion != reader.GetInt16(7) || request.SessionId != reader.GetString(8)
            || request.EvaluationDate != reader.GetFieldValue<DateOnly>(9)
            || request.Cutoff != DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
            throw new EvidenceBindingException("TECHNICAL_CAPTURE_MALFORMED");
        return capture;
    }
}
