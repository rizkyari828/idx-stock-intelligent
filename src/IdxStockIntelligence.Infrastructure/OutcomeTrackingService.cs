using System.Data;
using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;
using NpgsqlTypes;

namespace IdxStockIntelligence.Infrastructure;

public sealed class OutcomeTrackingService(NpgsqlDataSource dataSource)
{
    private static NpgsqlCommand Command(string sql, NpgsqlConnection c, NpgsqlTransaction? t = null) => new(sql, c, t) { CommandTimeout = 15 };
    private static T Decode<T>(string json) => JsonSerializer.Deserialize<T>(json, DecisionSnapshotService.JsonOptions)
        ?? throw new ScreenerException(503, "OUTCOME_UNAVAILABLE");
    private static void Require(bool condition) { if (!condition) throw new ScreenerException(503, "OUTCOME_UNAVAILABLE"); }

    private static async Task<IReadOnlyList<OutcomeCell>> StoredAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid run, CancellationToken ct)
    {
        await using var cmd = Command("""
            SELECT jsonb_build_object('instrumentId',instrument_id,'horizonSessions',horizon_sessions,
                'state',outcome_state,'reason',reason,'anchorMarketDate',anchor_market_date,'anchorClose',anchor_close,
                'horizonMarketDate',horizon_market_date,'horizonClose',horizon_close,'priceReturnPct',price_return_pct,
                'materialized',true,'newlyMaterialized',false,'outcomeKnownAt',outcome_known_at,'recordedAt',recorded_at)::text,
                schema_version,outcome_policy_id FROM decision_snapshot_outcome WHERE run_id=$1
            ORDER BY instrument_id,horizon_sessions LIMIT 841;
            """, c, t);
        cmd.Parameters.AddWithValue(run);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var cells = new List<OutcomeCell>();
        while (await reader.ReadAsync(ct))
        {
            Require(cells.Count < 840);
            // The table fixes V0.1 versions; unsupported storage must never be silently reinterpreted.
            Require(reader.GetInt16(1) == 1 && reader.GetString(2) == OutcomeEvaluator.PolicyId);
            var cell = Decode<OutcomeCell>(reader.GetString(0)); Require(cell.Terminal);
            cells.Add(cell);
        }
        return cells;
    }

    public Task<OutcomeResponse> EvaluateAsync(Guid run, int horizon, CancellationToken ct)
    {
        if (run == Guid.Empty || !OutcomeRequest.Horizons.Contains(horizon)) throw new ScreenerException(400, "OUTCOME_REQUEST_INVALID");
        return ExecuteAsync(run, [horizon], null, true, ct);
    }
    public Task<OutcomeResponse> ReadAsync(Guid run, Guid? instrument, CancellationToken ct)
    {
        if (run == Guid.Empty || instrument == Guid.Empty) throw new ScreenerException(400, "OUTCOME_REQUEST_INVALID");
        return ExecuteAsync(run, OutcomeRequest.Horizons, instrument, false, ct);
    }

    private async Task<OutcomeResponse> ExecuteAsync(Guid run, int[] horizons, Guid? instrument, bool write, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(token);
            // A fast stored-only view recovers terminal results without loading live files.
            await using (var quick = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token))
            {
                var snapshot = await DecisionSnapshotService.ReadRunAsync(connection, quick, run, token)
                    ?? throw new ScreenerException(404, "SNAPSHOT_NOT_FOUND");
                if (instrument is { } id && !snapshot.Rows.Any(r => r.InstrumentId == id)) throw new ScreenerException(404, "SNAPSHOT_ROW_NOT_FOUND");
                var existing = (await StoredAsync(connection, quick, run, token)).Where(c => horizons.Contains(c.HorizonSessions)
                    && (instrument is null || c.InstrumentId == instrument)).ToArray();
                var count = snapshot.Rows.Count(r => instrument is null || r.InstrumentId == instrument) * horizons.Length;
                await quick.CommitAsync(token);
                if (existing.Length == count) return OutcomeResponse.From(run, horizons.Length == 1 ? horizons[0] : null, DateTimeOffset.UtcNow, existing);
                if (!OutcomeEvaluator.Supported(snapshot.Header)) throw new ScreenerException(409, "OUTCOME_POLICY_VERSION_UNAVAILABLE");
            }
            var copied = await ScreenerReferenceFiles.CopyAsync(token);
            if (!copied.Available) throw new ScreenerException(503, "OUTCOME_UNAVAILABLE");
            var locked = false;
            // This is a lock key only, not an evidence hash. Collisions merely serialize unrelated bounded operations.
            var lockIntent = "outcome/" + run.ToString("D") + "/" + horizons[0];
            try
            {
                if (write)
                {
                    await using var acquire = Command("SELECT pg_advisory_lock(hashtextextended($1,0))", connection);
                    acquire.Parameters.AddWithValue(lockIntent); locked = true; await acquire.ExecuteNonQueryAsync(token);
                }
                for (var attempt = 0; ; attempt++)
                {
                    try { return await AssessAsync(connection, run, horizons, instrument, write, copied.Value!, token); }
                    catch (PostgresException e) when (attempt == 0 && e.SqlState is "23505" or "40001")
                    { token.ThrowIfCancellationRequested(); } // AssessAsync disposed/rolled back its entire new subset; re-read a fresh view.
                }
            }
            finally
            {
                if (locked)
                {
                    try
                    {
                        await using var release = Command("SELECT pg_advisory_unlock(hashtextextended($1,0))", connection);
                        release.Parameters.AddWithValue(lockIntent); await release.ExecuteNonQueryAsync(CancellationToken.None);
                    }
                    catch { NpgsqlConnection.ClearPool(connection); } // Disposal discards this lease, releasing any surviving session lock.
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ScreenerException(503, "OUTCOME_UNAVAILABLE"); }
        catch (DecisionVerificationService.MissingInput) { throw new ScreenerException(503, "OUTCOME_UNAVAILABLE"); }
        catch (ScreenerException e) when (e.StatusCode == 503) { throw new ScreenerException(503, "OUTCOME_UNAVAILABLE"); }
        catch (Exception e) when (e is NpgsqlException or TimeoutException or JsonException or InvalidOperationException or ArgumentException or OverflowException)
        { ct.ThrowIfCancellationRequested(); throw new ScreenerException(503, "OUTCOME_UNAVAILABLE"); }
    }

    private static async Task<OutcomeResponse> AssessAsync(NpgsqlConnection connection, Guid run, int[] horizons,
        Guid? instrument, bool write, ScreenerReferenceCopy copied, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!write) { await using var ro = Command("SET TRANSACTION READ ONLY", connection, transaction); await ro.ExecuteNonQueryAsync(ct); }
        await using var clock = Command("SELECT clock_timestamp()", connection, transaction);
        var at = new DateTimeOffset((DateTime)(await clock.ExecuteScalarAsync(ct))!, TimeSpan.Zero);
        var capture = await DecisionSnapshotService.ReadRunAsync(connection, transaction, run, ct)
            ?? throw new ScreenerException(404, "SNAPSHOT_NOT_FOUND");
        var rows = capture.Rows.Where(r => instrument is null || r.InstrumentId == instrument).OrderBy(r => r.InstrumentId).ToArray();
        var stored = (await StoredAsync(connection, transaction, run, ct)).Where(c => horizons.Contains(c.HorizonSessions)).ToArray();
        var existing = stored.ToDictionary(c => (c.InstrumentId, c.HorizonSessions));
        if (rows.All(r => horizons.All(n => existing.ContainsKey((r.InstrumentId, n)))))
        {
            await transaction.CommitAsync(ct);
            return OutcomeResponse.From(run, horizons.Length == 1 ? horizons[0] : null, at, stored.Where(c => instrument is null || c.InstrumentId == instrument).ToArray());
        }
        if (!OutcomeEvaluator.Supported(capture.Header)) throw new ScreenerException(409, "OUTCOME_POLICY_VERSION_UNAVAILABLE");
        var manifest = await DecisionVerificationService.ManifestAsync(connection, transaction, run, ct);
        DecisionVerificationService.ValidateManifest(capture.Header, manifest);
        Require(capture.Rows.Select(r => r.InstrumentId).Order().SequenceEqual(manifest.EvaluatedInstrumentIds.Order()) && capture.Rows.Count == capture.Header.RowCount);
        var original = await DecisionVerificationService.ArchivesAsync(manifest.Archives, ct);
        var oldRefs = ScreenerReferences.Select(original.Reference, original.Sessions, original.InstrumentSessions, manifest.Request, ct);
        Require(ScreenerReferences.Hash(manifest.Instruments) == ScreenerReferences.Hash(oldRefs.Instruments.Select(i => new DecisionReferenceLink(i.SnapshotId, i.KnownAt, i.ContentHash)).ToArray())
            && ScreenerReferences.Hash(manifest.Sessions) == ScreenerReferences.Hash(oldRefs.Sessions)
            && ScreenerReferences.Hash(manifest.InstrumentSessions) == ScreenerReferences.Hash(oldRefs.InstrumentSessions));
        var captured = await ScreenerEvidenceDatabase.ReadAsync(connection, transaction, manifest.Request, ct, manifest);
        Require(captured.Available); DecisionVerificationService.Authenticate(manifest, captured.Value!, ct);
        var cells = new List<OutcomeCell>();
        var pending = new List<(OutcomeCell Cell, OutcomeManifest Manifest)>();
        foreach (var n in horizons)
        {
            var horizon = OutcomeEvaluator.ResolveHorizon(capture.Header, n, copied.Bundle.Sessions, at, ct);
            var fixedDates = stored.Where(c => c.HorizonSessions == n).Select(c => c.HorizonMarketDate).Distinct().ToArray();
            if (fixedDates.Length > 0 && (fixedDates.Length != 1 || horizon.Date is not null && horizon.Date != fixedDates[0]))
                horizon = horizon with { Date = null, State = "UNRESOLVED", Reason = "HORIZON_ALIGNMENT_CONFLICT" };
            var missing = rows.Where(r => !existing.ContainsKey((r.InstrumentId, n))).ToArray();
            var forward = new ScreenerDatabaseEvidence([], []);
            var refs = new SelectedScreenerReferences([], [], [], []);
            if (horizon.Date is { } end && missing.Length > 0)
            {
                var request = new ScreenerReadRequest(missing.Select(r => r.InstrumentId).ToArray(), manifest.Request.BenchmarkId,
                    ScreenerReadRequest.Anchor, end, at);
                refs = ScreenerReferences.Select(copied.Bundle.Reference, copied.Bundle.Sessions, copied.Bundle.InstrumentSessions, request, ct);
                var read = await ScreenerEvidenceDatabase.ReadAsync(connection, transaction, request, ct);
                Require(read.Available); forward = read.Value!;
                foreach (var b in forward.Bars)
                    Require(b.ContentHashMatches() || b.Validate().Reason == "NUMERIC_OUT_OF_RANGE");
            }
            var inputs = new OutcomeInputs(captured.Value!, oldRefs, forward, refs);
            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                if (existing.TryGetValue((row.InstrumentId, n), out var saved)) { cells.Add(saved); continue; }
                var cell = OutcomeEvaluator.Evaluate(capture.Header, row, n, horizon, inputs, at, ct);
                cells.Add(cell);
                if (!write || !cell.Terminal) continue;
                var anchor = OutcomeEvaluator.Anchor(row, inputs); var endpoint = OutcomeEvaluator.Endpoint(row, horizon, inputs);
                DecisionBarLink? Link(ScreenerBarEvidence? b) => b is null ? null : new(b.InstrumentId,b.SessionDate,b.RevisionNumber,b.KnownAt,b.ContentHash,b.RawArtifactId);
                var listing = forward.Listings.SingleOrDefault(l => l.InstrumentId == row.InstrumentId);
                var calendar = new[] { new OutcomeCalendarDay(capture.Header.TargetSession!.Value, "ANCHOR",
                    ScreenerSessions.Resolve(oldRefs.Sessions, capture.Header.TargetSession.Value, capture.Header.Through, capture.Header.KnowledgeCutoff).Proof) }
                    .Concat(horizon.Calendar).ToArray();
                pending.Add((cell, new(1, OutcomeEvaluator.PolicyId, capture.Header.SchemaVersion, capture.Header.PolicyId,
                    capture.Header.InputHash, capture.Header.SelectedDigest, row.InstrumentId, n, at, Link(anchor), Link(endpoint),
                    listing is null ? null : new(listing.InstrumentId, listing.KnownAt, listing.ContentHash), calendar,
                    refs.Instruments.Where(i => i.InstrumentId == row.InstrumentId).Select(i => new DecisionReferenceLink(i.SnapshotId,i.KnownAt,i.ContentHash)).ToArray(),
                    refs.InstrumentSessions.Where(s => s.Instrument.Value == row.InstrumentId).ToArray(), [], cell.Reason)));
            }
        }
        if (pending.Count > 0)
        {
            var archives = await DecisionSnapshotService.ArchiveAsync(copied, ct);
            var payload = pending.Select(p => new { run_id = run, instrument_id = p.Cell.InstrumentId, horizon_sessions = p.Cell.HorizonSessions,
                schema_version = 1, outcome_policy_id = OutcomeEvaluator.PolicyId, anchor_market_date = p.Cell.AnchorMarketDate,
                anchor_close = p.Cell.AnchorClose, horizon_market_date = p.Cell.HorizonMarketDate, horizon_close = p.Cell.HorizonClose,
                price_return_pct = p.Cell.PriceReturnPct, outcome_state = p.Cell.State, reason = p.Cell.Reason, outcome_known_at = at,
                evidence_manifest = p.Manifest with { Archives = archives } }).ToArray();
            long bytes = 0;
            foreach (var p in payload)
            {
                var length = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(p.evidence_manifest, DecisionSnapshotService.JsonOptions));
                Require(length <= 65536 && (bytes += length) <= 32 * 1024 * 1024);
            }
            await using var insert = Command("""
                INSERT INTO decision_snapshot_outcome(run_id,instrument_id,horizon_sessions,schema_version,outcome_policy_id,
                    anchor_market_date,anchor_close,horizon_market_date,horizon_close,price_return_pct,outcome_state,reason,outcome_known_at,evidence_manifest)
                SELECT run_id,instrument_id,horizon_sessions,schema_version,outcome_policy_id,anchor_market_date,anchor_close,
                    horizon_market_date,horizon_close,price_return_pct,outcome_state,reason,outcome_known_at,evidence_manifest
                FROM jsonb_to_recordset($1) AS x(run_id uuid,instrument_id uuid,horizon_sessions smallint,schema_version smallint,
                    outcome_policy_id text,anchor_market_date date,anchor_close numeric,horizon_market_date date,horizon_close numeric,
                    price_return_pct numeric,outcome_state text,reason text,outcome_known_at timestamptz,evidence_manifest jsonb);
                """, connection, transaction);
            insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(payload, DecisionSnapshotService.JsonOptions) });
            await insert.ExecuteNonQueryAsync(ct);
            var inserted = (await StoredAsync(connection, transaction, run, ct)).ToDictionary(c => (c.InstrumentId, c.HorizonSessions));
            var newKeys = pending.Select(p => (p.Cell.InstrumentId, p.Cell.HorizonSessions)).ToHashSet();
            cells = cells.Select(c => newKeys.Contains((c.InstrumentId, c.HorizonSessions))
                ? inserted[(c.InstrumentId, c.HorizonSessions)] with { NewlyMaterialized = true } : c).ToList();
        }
        await transaction.CommitAsync(ct);
        return OutcomeResponse.From(run, horizons.Length == 1 ? horizons[0] : null, at, cells);
    }
}
