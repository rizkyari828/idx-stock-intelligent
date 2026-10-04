using System.Data;
using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public sealed class OutcomeVerificationService(NpgsqlDataSource dataSource)
{
    private static NpgsqlCommand Command(string sql, NpgsqlConnection c, NpgsqlTransaction t)
        => new(sql, c, t) { CommandTimeout = 15 };
    private static T Decode<T>(string json, int bound)
    {
        if (Encoding.UTF8.GetByteCount(json) > bound) throw new RetainedInputUnavailableException("RETAINED_INPUT_BOUND_EXCEEDED");
        return JsonSerializer.Deserialize<T>(json, DecisionSnapshotService.JsonOptions)
            ?? throw new RetainedInputUnavailableException("RETAINED_INPUT_INVALID");
    }

    public async Task<OutcomeVerificationResult> VerifyAsync(Guid run, Guid instrument, int horizon, CancellationToken ct)
    {
        if (run == Guid.Empty || instrument == Guid.Empty || !OutcomeRequest.Horizons.Contains(horizon))
            throw new ScreenerException(400, "OUTCOME_VERIFICATION_REQUEST_INVALID");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60)); var token = deadline.Token;
        var verifiedAt = DateTimeOffset.UtcNow;
        try
        {
            await using var c = await dataSource.OpenConnectionAsync(token);
            await using var t = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            await using (var ro = Command("SET TRANSACTION READ ONLY", c, t)) await ro.ExecuteNonQueryAsync(token);
            string capturePolicy, captureKind, universe; int captureSchema;
            // Resolve existence and policy from scalar headers before decoding any retained evidence.
            await using (var header = Command("""
                SELECT policy_id,schema_version,capture_kind,universe,
                    EXISTS(SELECT FROM decision_snapshot_row WHERE run_id=$1 AND instrument_id=$2)
                FROM decision_snapshot_run WHERE run_id=$1
                """, c, t))
            {
                header.Parameters.AddWithValue(run); header.Parameters.AddWithValue(instrument);
                await using var reader = await header.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new ScreenerException(404, "SNAPSHOT_NOT_FOUND");
                if (!reader.GetBoolean(4)) throw new ScreenerException(404, "SNAPSHOT_ROW_NOT_FOUND");
                capturePolicy = reader.GetString(0); captureSchema = reader.GetInt16(1);
                captureKind = reader.GetString(2); universe = reader.GetString(3);
            }
            string policy, projectionJson, manifestJson; int schema; DateTimeOffset knownAt, recordedAt;
            await using (var outcome = Command("""
                SELECT outcome_policy_id,schema_version,outcome_known_at,recorded_at,
                    jsonb_build_object('runId',run_id,'instrumentId',instrument_id,'horizonSessions',horizon_sessions,
                        'outcomePolicyId',outcome_policy_id,'schemaVersion',schema_version,
                        'anchorMarketDate',anchor_market_date,'anchorClose',anchor_close,
                        'horizonMarketDate',horizon_market_date,'horizonClose',horizon_close,
                        'terminalState',outcome_state,'terminalReason',reason,'priceReturnPct',price_return_pct,
                        'outcomeKnownAt',outcome_known_at,'recordedAt',recorded_at)::text,evidence_manifest::text
                FROM decision_snapshot_outcome WHERE run_id=$1 AND instrument_id=$2 AND horizon_sessions=$3
                """, c, t))
            {
                outcome.Parameters.AddWithValue(run); outcome.Parameters.AddWithValue(instrument); outcome.Parameters.AddWithValue(horizon);
                await using var reader = await outcome.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new ScreenerException(404, "OUTCOME_NOT_FOUND");
                policy = reader.GetString(0); schema = reader.GetInt16(1);
                knownAt = reader.GetFieldValue<DateTimeOffset>(2); recordedAt = reader.GetFieldValue<DateTimeOffset>(3);
                projectionJson = reader.GetString(4); manifestJson = reader.GetString(5);
            }
            OutcomeVerificationResult Unavailable(OutcomeVerificationState state, string detail)
                => OutcomeVerification.Unavailable(run, instrument, horizon, policy, schema, knownAt, recordedAt, verifiedAt, state, detail);
            if (OutcomeVerification.ResolvePolicy(policy, schema, capturePolicy, captureSchema, captureKind, universe) is null)
                return Unavailable(OutcomeVerificationState.PolicyVersionUnavailable, "POLICY_OR_SCHEMA_UNSUPPORTED");
            try
            {
                var stored = Decode<OutcomeVerificationProjection>(projectionJson, 2 * 1024 * 1024);
                var manifest = Decode<OutcomeManifest>(manifestJson, 65536);
                DecisionSnapshotHeader h; DecisionSnapshotRow row; DecisionSnapshotManifest capture;
                await using (var parent = Command($"""
                    SELECT {DecisionSnapshotService.HeaderSql}::text,{DecisionSnapshotService.RowSql}::text,r.evidence_manifest::text
                    FROM decision_snapshot_run r JOIN decision_snapshot_row s USING(run_id)
                    WHERE r.run_id=$1 AND s.instrument_id=$2
                    """, c, t))
                {
                    parent.Parameters.AddWithValue(run); parent.Parameters.AddWithValue(instrument);
                    await using var reader = await parent.ExecuteReaderAsync(token);
                    if (!await reader.ReadAsync(token)) throw new RetainedInputUnavailableException("CAPTURE_LINK_INVALID");
                    h = Decode<DecisionSnapshotHeader>(reader.GetString(0), 2 * 1024 * 1024);
                    row = Decode<DecisionSnapshotRow>(reader.GetString(1), 2 * 1024 * 1024);
                    capture = Decode<DecisionSnapshotManifest>(reader.GetString(2), 32 * 1024 * 1024);
                }
                OutcomeVerification.ValidateManifest(stored, h, row, capture, manifest);
                var copies = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                var old = await DecisionVerificationService.ArchivesAsync(capture.Archives, token, serviceFailures: true, copies);
                var forward = await DecisionVerificationService.ArchivesAsync(manifest.Archives, token, serviceFailures: true, copies);
                var oldRefs = ScreenerReferences.Select(old.Reference, old.Sessions, old.InstrumentSessions, capture.Request, token);
                var request = new ScreenerReadRequest([instrument], capture.Request.BenchmarkId,
                    h.HistoryAnchor, manifest.Calendar[^1].Date, knownAt);
                var refs = ScreenerReferences.Select(forward.Reference, forward.Sessions, forward.InstrumentSessions, request, token);
                refs = refs with { Instruments = refs.Instruments.Where(i => i.InstrumentId == instrument).ToArray(),
                    InstrumentSessions = refs.InstrumentSessions.Where(p => p.Instrument.Value == instrument).ToArray() };
                var captured = await ScreenerEvidenceDatabase.ReadAsync(c, t, capture.Request, token, capture);
                var forwardManifest = capture with { Request = request,
                    Bars = manifest.Endpoint is null ? [] : [manifest.Endpoint],
                    Listings = manifest.Listing is null ? [] : [manifest.Listing] };
                var endpoint = await ScreenerEvidenceDatabase.ReadAsync(c, t, request, token, forwardManifest);
                if (!captured.Available || !endpoint.Available) throw new RetainedInputUnavailableException("RETAINED_DATABASE_EVIDENCE_UNAVAILABLE");
                var result = OutcomeVerification.Verify(stored, h, row, capture, manifest,
                    new(captured.Value!, oldRefs, endpoint.Value!, refs), forward.Sessions, verifiedAt, token);
                await t.CommitAsync(token);
                return result;
            }
            catch (RetainedInputUnavailableException e) { return Unavailable(OutcomeVerificationState.InputNotAvailable, e.Message); }
            catch (DecisionVerificationService.MissingInput e) { return Unavailable(OutcomeVerificationState.InputNotAvailable, e.Message); }
            catch (Exception e) when (e is JsonException or ArgumentException or FormatException or OverflowException)
            { return Unavailable(OutcomeVerificationState.InputNotAvailable, "RETAINED_INPUT_INVALID"); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ScreenerException(503, "OUTCOME_VERIFICATION_UNAVAILABLE"); }
        catch (Exception e) when (e is NpgsqlException or TimeoutException or InvalidOperationException or IOException or UnauthorizedAccessException)
        { ct.ThrowIfCancellationRequested(); throw new ScreenerException(503, "OUTCOME_VERIFICATION_UNAVAILABLE"); }
    }
}
