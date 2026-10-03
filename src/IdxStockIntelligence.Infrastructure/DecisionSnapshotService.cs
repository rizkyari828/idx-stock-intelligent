using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdxStockIntelligence.Application;
using Npgsql;
using NpgsqlTypes;

namespace IdxStockIntelligence.Infrastructure;

public sealed class DecisionSnapshotService(NpgsqlDataSource dataSource)
{
    public static JsonSerializerOptions JsonOptions { get; } = new(ScreenerReferences.JsonOptions)
    { Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    private const string ArchiveRoot = "data/raw/decision-reference";
    private const string HeaderSql = """
        jsonb_build_object('runId',r.run_id,'requestId',r.request_id,'schemaVersion',r.schema_version,
            'captureKind',r.capture_kind,'capturedAt',r.captured_at,'knowledgeCutoff',r.knowledge_cutoff,
            'recordedAt',r.recorded_at,'through',r.through,'targetSession',r.target_session,'historyAnchor',r.history_anchor,
            'policyId',r.policy_id,'universe',r.universe,'universeSnapshotId',r.universe_snapshot_id,
            'portfolioId',r.portfolio_id,'inputHash',r.input_hash,'selectedDigest',r.selected_digest,'status',r.status,'rowCount',r.row_count)
        """;
    private const string RowSql = """
        jsonb_build_object('instrumentId',s.instrument_id,'symbol',s.symbol,'configured',s.configured,'held',s.held,
            'discoveryRank',s.discovery_rank,'eligibility',s.eligibility,'setup',s.setup,'setupEvaluated',s.setup_evaluated,
            'episodeId',s.episode_id,'marketDate',s.market_date,'close',s.close,'shares',s.shares,'investedCost',s.invested_cost,
            'averageCost',s.average_cost,'mandate',s.mandate,'thesisVersionId',s.thesis_version_id,'thesisVersion',s.thesis_version,
            'thesisActive',s.thesis_active,'result',s.result)
        """;

    private static byte[] Bytes<T>(T value, int limit)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if (bytes.Length > limit) throw new ScreenerException(503, "SNAPSHOT_BOUND_EXCEEDED");
        return bytes;
    }
    private static T Decode<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new ScreenerException(503, "SNAPSHOT_UNAVAILABLE");
    private static NpgsqlCommand Command(string sql, NpgsqlConnection connection, NpgsqlTransaction? transaction = null)
        => new(sql, connection, transaction) { CommandTimeout = 15 };
    private static void Add(NpgsqlCommand command, object? value, NpgsqlDbType type)
        => command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = type, Value = value ?? DBNull.Value });

    public static async Task<IReadOnlyList<DecisionReferenceArchive>> ArchiveAsync(ScreenerReferenceCopy copied, CancellationToken ct)
    {
        var kinds = new[] { "screener-reference", "sessions", "instrument-sessions" };
        var retained = new List<DecisionReferenceArchive>();
        try
        {
            for (var i = 0; i < kinds.Length; i++)
            {
                if (copied.Files[i] is not { } bytes) { retained.Add(new(kinds[i], false, null, 0)); continue; }
                await using var input = new MemoryStream(bytes, writable: false);
                var archived = await new RawArtifactArchiver(ArchiveRoot).ArchiveAsync(input, ".json", ct);
                retained.Add(new(kinds[i], true, archived.ContentSha256, archived.ByteLength));
            }
            await VerifyArchivesAsync(retained, ct);
            return retained;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
        { throw new ScreenerException(503, "SNAPSHOT_ARCHIVE_UNAVAILABLE"); }
    }

    private static async Task VerifyArchivesAsync(IReadOnlyList<DecisionReferenceArchive> archives, CancellationToken ct)
    {
        try
        {
            foreach (var archive in archives.Where(a => a.Present))
            {
                if (!ScreenerReferences.IsHash(archive.ContentSha256)) throw new ScreenerException(503, "SNAPSHOT_ARCHIVE_UNAVAILABLE");
                var hash = archive.ContentSha256!;
                await using var existing = new FileStream(Path.Combine(ArchiveRoot, hash[..2], hash + ".json"), FileMode.Open,
                    FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (existing.Length != archive.ByteLength || !string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(existing, ct)), hash, StringComparison.OrdinalIgnoreCase))
                    throw new ScreenerException(503, "SNAPSHOT_ARCHIVE_UNAVAILABLE");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
        { throw new ScreenerException(503, "SNAPSHOT_ARCHIVE_UNAVAILABLE"); }
    }

    public async Task<(DecisionSnapshotRun Run, bool Created)> CaptureAsync(DecisionSnapshotRequest request, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        try
        {
            if (await FindAsync(request.RequestId, true, token) is { } existing) return (Retry(existing, request), false);
            var copied = await ScreenerReferenceFiles.CopyAsync(token);
            if (!copied.Available) throw new ScreenerException(503, copied.Reason!);
            var archives = await ArchiveAsync(copied.Value!, token);
            try
            {
                await using var connection = await dataSource.OpenConnectionAsync(token);
                await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
                await using var clock = Command("SELECT clock_timestamp()", connection, transaction);
                var captured = (DateTime) (await clock.ExecuteScalarAsync(token))!;
                var now = new DateTimeOffset(captured, TimeSpan.Zero);
                if (await ReadAsync(connection, transaction, request.RequestId, true, token) is { } raced)
                {
                    var original = Retry(raced, request);
                    await transaction.CommitAsync(token);
                    return (original, false);
                }
                var query = request.Resolve(now);
                var evaluated = await ScreenerService.EvaluateAsync(connection, transaction, copied.Value!.Bundle, query, token);
                var result = evaluated.Result;
                var presentation = ScreenerPresentation.Map(result, query, evaluated.InputHash, evaluated.References, token);
                var positions = evaluated.Projection?.Positions.ToDictionary(p => p.InstrumentId);
                var theses = evaluated.History?.Theses.GroupBy(t => t.InstrumentId).ToDictionary(g => g.Key, g => g.MaxBy(t => t.Version)!);
                var rows = result.Rows.OrderBy(r => r.InstrumentId).Select(r => DecisionSnapshotProjection.Row(
                    ScreenerPresentation.Row(r, evaluated.References, query.Cutoff), evaluated.History is not null,
                    positions?.GetValueOrDefault(r.InstrumentId), theses?.GetValueOrDefault(r.InstrumentId))).ToArray();
                if (rows.Length > 210) throw new ScreenerException(503, "SNAPSHOT_BOUND_EXCEEDED");
                var output = new DecisionSnapshotResult(result.Reasons, result.Summary, result.RankedCandidateIds,
                    result.AllViewIds, result.ShortlistIds, result.HeldIds, presentation.MarketContext);
                var manifest = new DecisionSnapshotManifest(1, evaluated.Request,
                    result.Rows.Where(r => r.Configured).Select(r => r.InstrumentId).Order().ToArray(), result.HeldIds,
                    rows.Select(r => r.InstrumentId).ToArray(),
                    evaluated.Database.Bars.Select(b => new DecisionBarLink(b.InstrumentId, b.SessionDate, b.RevisionNumber, b.KnownAt, b.ContentHash, b.RawArtifactId)).ToArray(),
                    evaluated.Database.Listings.Select(l => new DecisionListingLink(l.InstrumentId, l.KnownAt, l.ContentHash)).ToArray(),
                    evaluated.References.Universes.Select(r => new DecisionReferenceLink(r.SnapshotId, r.KnownAt, r.ContentHash)).ToArray(),
                    evaluated.References.Instruments.Select(r => new DecisionReferenceLink(r.SnapshotId, r.KnownAt, r.ContentHash)).ToArray(),
                    evaluated.References.Sessions, evaluated.References.InstrumentSessions,
                    evaluated.History is { } history ? new(history.Portfolio.Id, history.Events.Select(e => e.Id).ToArray(), history.Theses.Select(t => t.Id).ToArray()) : null, archives);
                var outputJson = Bytes(output, 32768);
                var manifestJson = Bytes(manifest, 32 * 1024 * 1024);
                foreach (var row in rows) { token.ThrowIfCancellationRequested(); Bytes(row.Result, 8192); }
                var runId = Guid.NewGuid();
                await using var insert = Command("""
                    INSERT INTO decision_snapshot_run(run_id,request_id,schema_version,capture_kind,captured_at,knowledge_cutoff,
                        through,history_anchor,target_session,policy_id,universe,universe_snapshot_id,portfolio_id,input_hash,
                        selected_digest,status,row_count,request_intent,result,evidence_manifest)
                    VALUES($1,$2,1,'PROSPECTIVE_CAPTURE',$3,$3,$4,'2026-08-24',$5,'screener-v0.1.0','PILOT',$6,$7,$8,$9,$10,$11,$12,$13,$14)
                    RETURNING recorded_at;
                    """, connection, transaction);
                Add(insert, runId, NpgsqlDbType.Uuid); Add(insert, request.RequestId, NpgsqlDbType.Uuid);
                Add(insert, now, NpgsqlDbType.TimestampTz); Add(insert, query.Through, NpgsqlDbType.Date);
                Add(insert, result.TargetSession, NpgsqlDbType.Date); Add(insert, result.UniverseSnapshotId, NpgsqlDbType.Text);
                Add(insert, request.PortfolioId, NpgsqlDbType.Uuid); Add(insert, evaluated.InputHash, NpgsqlDbType.Text);
                Add(insert, evaluated.SelectedDigest, NpgsqlDbType.Text); Add(insert, presentation.Status, NpgsqlDbType.Text);
                Add(insert, (short)rows.Length, NpgsqlDbType.Smallint); Add(insert, JsonSerializer.Serialize(request.Intent, JsonOptions), NpgsqlDbType.Jsonb);
                Add(insert, System.Text.Encoding.UTF8.GetString(outputJson), NpgsqlDbType.Jsonb);
                Add(insert, System.Text.Encoding.UTF8.GetString(manifestJson), NpgsqlDbType.Jsonb);
                var recorded = new DateTimeOffset((DateTime)(await insert.ExecuteScalarAsync(token))!, TimeSpan.Zero);
                await InsertRowsAsync(connection, transaction, runId, rows, token);
                var run = new DecisionSnapshotRun(new(runId, request.RequestId, 1, "PROSPECTIVE_CAPTURE", now, now, recorded,
                    query.Through, result.TargetSession, ScreenerReadRequest.Anchor, ScreenerReadRequest.PolicyId, "PILOT",
                    result.UniverseSnapshotId, request.PortfolioId, evaluated.InputHash, evaluated.SelectedDigest, presentation.Status, rows.Length), output, rows);
                Bytes(run, 2 * 1024 * 1024);
                await VerifyArchivesAsync(archives, token);
                await transaction.CommitAsync(token);
                return (run, true);
            }
            catch (PostgresException e) when (e.SqlState == "40001" || e.SqlState == "23505" && e.ConstraintName == "decision_snapshot_run_request_id_key")
            {
                // Fresh transaction after disposal/rollback; never read an aborted repeatable-read snapshot.
                var winner = await FindAsync(request.RequestId, true, token);
                if (winner is null) throw new ScreenerException(503, "SNAPSHOT_UNAVAILABLE");
                return (Retry(winner, request), false);
            }
        }
        catch (KeyNotFoundException) { throw new ScreenerException(404, "PORTFOLIO_NOT_FOUND"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ScreenerException(503, "SNAPSHOT_UNAVAILABLE"); }
        catch (Exception e) when (e is NpgsqlException or TimeoutException or InvalidOperationException or ArgumentException or OverflowException or JsonException)
        { ct.ThrowIfCancellationRequested(); throw new ScreenerException(503, "SNAPSHOT_UNAVAILABLE"); }
    }

    private static async Task InsertRowsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id,
        IReadOnlyList<DecisionSnapshotRow> rows, CancellationToken ct)
    {
        var payload = rows.Select(r => new { run_id = id, instrument_id = r.InstrumentId, symbol = r.Symbol, configured = r.Configured,
            held = r.Held, discovery_rank = r.DiscoveryRank, eligibility = r.Eligibility, setup = r.Setup, setup_evaluated = r.SetupEvaluated,
            episode_id = r.EpisodeId, market_date = r.MarketDate, close = r.Close, shares = r.Shares, invested_cost = r.InvestedCost,
            average_cost = r.AverageCost, mandate = r.Mandate, thesis_version_id = r.ThesisVersionId, thesis_version = r.ThesisVersion,
            thesis_active = r.ThesisActive, result = r.Result });
        await using var command = Command("""
            INSERT INTO decision_snapshot_row SELECT * FROM jsonb_to_recordset($1) AS x(run_id uuid,instrument_id uuid,symbol text,
                configured boolean,held boolean,discovery_rank integer,eligibility text,setup text,setup_evaluated boolean,
                episode_id text,market_date date,close numeric,shares numeric,invested_cost numeric,average_cost numeric,
                mandate text,thesis_version_id uuid,thesis_version integer,thesis_active boolean,result jsonb);
            """, connection, transaction);
        Add(command, JsonSerializer.Serialize(payload, JsonOptions), NpgsqlDbType.Jsonb);
        await command.ExecuteNonQueryAsync(ct);
    }

    private sealed record Stored(DecisionSnapshotRun Run, DecisionSnapshotIntent Intent);
    private static DecisionSnapshotRun Retry(Stored existing, DecisionSnapshotRequest request)
    {
        if (existing.Intent != request.Intent) throw new ScreenerException(409, "REQUEST_ID_CONFLICT");
        return existing.Run;
    }
    private async Task<Stored?> FindAsync(Guid id, bool byRequest, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var readOnly = Command("SET TRANSACTION READ ONLY", connection, transaction)) await readOnly.ExecuteNonQueryAsync(ct);
        var stored = await ReadAsync(connection, transaction, id, byRequest, ct);
        await transaction.CommitAsync(ct);
        return stored;
    }
    private static async Task<Stored?> ReadAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, bool byRequest, CancellationToken ct)
    {
        await using var command = Command($"""
            SELECT {HeaderSql}::text,r.result::text,r.request_intent::text,
                (SELECT coalesce(jsonb_agg({RowSql} ORDER BY s.instrument_id),'[]') FROM decision_snapshot_row s WHERE s.run_id=r.run_id)::text
            FROM decision_snapshot_run r WHERE {(byRequest ? "r.request_id" : "r.run_id")}=$1;
            """, connection, transaction);
        Add(command, id, NpgsqlDbType.Uuid);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var run = new DecisionSnapshotRun(Decode<DecisionSnapshotHeader>(reader.GetString(0)), Decode<DecisionSnapshotResult>(reader.GetString(1)),
            Decode<DecisionSnapshotRow[]>(reader.GetString(3)));
        Bytes(run, 2 * 1024 * 1024);
        return new(run, Decode<DecisionSnapshotIntent>(reader.GetString(2)));
    }
    public async Task<DecisionSnapshotRun> GetAsync(Guid id, CancellationToken ct)
    {
        try { return (await FindAsync(id, false, ct))?.Run ?? throw new ScreenerException(404, "SNAPSHOT_NOT_FOUND"); }
        catch (Exception e) when (e is NpgsqlException or TimeoutException or JsonException or InvalidOperationException)
        { ct.ThrowIfCancellationRequested(); throw new ScreenerException(503, "SNAPSHOT_UNAVAILABLE"); }
    }
    public async Task<object> ListAsync(DecisionSnapshotListQuery query, Guid? instrument, CancellationToken ct)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = Command($"""
                SELECT {HeaderSql}::text{(instrument is null ? "" : $",{RowSql}::text")}
                FROM decision_snapshot_run r {(instrument is null ? "" : "JOIN decision_snapshot_row s ON s.run_id=r.run_id AND s.instrument_id=$5")}
                WHERE ($1::uuid IS NULL OR r.portfolio_id=$1)
                    AND ($2::timestamptz IS NULL OR (r.captured_at,r.run_id)<($2,$3))
                ORDER BY r.captured_at DESC,r.run_id DESC LIMIT $4;
                """, connection);
            Add(command, query.PortfolioId, NpgsqlDbType.Uuid); Add(command, query.Cursor?.CapturedAt, NpgsqlDbType.TimestampTz);
            Add(command, query.Cursor?.RunId, NpgsqlDbType.Uuid); Add(command, query.Limit + 1, NpgsqlDbType.Integer);
            if (instrument is { } id) Add(command, id, NpgsqlDbType.Uuid);
            var headers = new List<DecisionSnapshotHeader>(); var history = new List<DecisionSnapshotHistoryItem>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var h = Decode<DecisionSnapshotHeader>(reader.GetString(0));
                headers.Add(h);
                if (instrument is not null) history.Add(new(h, Decode<DecisionSnapshotRow>(reader.GetString(1))));
            }
            var next = headers.Count > query.Limit ? query.Encode(headers[query.Limit - 1]) : null;
            object page = instrument is null ? new DecisionSnapshotPage<DecisionSnapshotHeader>(headers.Take(query.Limit).ToArray(), next)
                : new DecisionSnapshotPage<DecisionSnapshotHistoryItem>(history.Take(query.Limit).ToArray(), next);
            Bytes(page, 2 * 1024 * 1024);
            return page;
        }
        catch (Exception e) when (e is NpgsqlException or TimeoutException or JsonException or InvalidOperationException)
        { ct.ThrowIfCancellationRequested(); throw new ScreenerException(503, "SNAPSHOT_UNAVAILABLE"); }
    }
}
