using System.Data;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public static class ScreenerEvidenceAsOfReader
{
    private static NpgsqlCommand Command(string sql, NpgsqlConnection connection, NpgsqlTransaction transaction) =>
        new(sql, connection, transaction) { CommandTimeout = ScreenerEvidenceV02Store.CommandTimeoutSeconds };

    public static async Task<ScreenerEvidenceAsOfResult> ReadAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        ScreenerEvidenceAsOfRequest request, CancellationToken ct)
    {
        request.Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            if (transaction is null)
            {
                await using var owned = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, deadline.Token);
                await using (var readOnly = Command("SET TRANSACTION READ ONLY", connection, owned))
                    await readOnly.ExecuteNonQueryAsync(deadline.Token);
                var result = await ReadSnapshotAsync(connection, owned, request, deadline.Token);
                await owned.CommitAsync(deadline.Token);
                return result;
            }
            if (transaction.Connection != connection || transaction.IsolationLevel != IsolationLevel.RepeatableRead)
                throw new ArgumentException("As-of evidence requires one repeatable-read snapshot.");
            await using (var readOnly = Command("SHOW transaction_read_only", connection, transaction))
                if ((string?)await readOnly.ExecuteScalarAsync(deadline.Token) != "on")
                    throw new ArgumentException("As-of evidence requires a read-only snapshot.");
            return await ReadSnapshotAsync(connection, transaction, request, deadline.Token);
        }
        catch (EvidenceBindingException e) { return ScreenerEvidenceAsOf.Failure(request, e.Reason); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return ScreenerEvidenceAsOf.Failure(request, "PERSISTED_EVIDENCE_READ_UNAVAILABLE"); }
        catch (Exception e) when (e is NpgsqlException or TimeoutException)
        { ct.ThrowIfCancellationRequested(); return ScreenerEvidenceAsOf.Failure(request, "PERSISTED_EVIDENCE_READ_UNAVAILABLE"); }
    }

    private static async Task<ScreenerEvidenceAsOfResult> ReadSnapshotAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ScreenerEvidenceAsOfRequest request, CancellationToken ct)
    {
        var retained = new Dictionary<Guid, ScreenerEvidenceRecord>();
        var raw = new Dictionary<Guid, SourceReference>();
        void Retain(ScreenerEvidenceRecord r)
        {
            retained[r.EvidenceId] = r;
            if (retained.Count > ScreenerEvidenceAsOf.MaximumRecords) throw new EvidenceBindingException(ScreenerEvidenceAsOf.BoundExceeded);
        }
        async Task<ScreenerEvidenceRecord[]> Load(EvidenceClaim claim, Guid? exchange)
        {
            // SQL bounds and coarse visibility only; no authority, recency or revision winner is selected here.
            await using var command = Command("SELECT " + ScreenerEvidenceV02Store.SelectList + " " + """
                FROM screener_evidence_record
                WHERE claim=$1 AND (subject_id=$2 OR ($3::uuid IS NOT NULL AND scope_kind='EXCHANGE' AND subject_id=$3))
                    AND known_at <= $4 AND (retrieved_at IS NULL OR retrieved_at <= $4)
                    AND (published_at IS NULL OR published_at <= $4)
                ORDER BY evidence_id LIMIT 513;
                """, connection, transaction);
            command.Parameters.AddWithValue(claim.ToString()); command.Parameters.AddWithValue(request.SubjectId);
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Uuid, Value = (object?)exchange ?? DBNull.Value });
            command.Parameters.AddWithValue(request.Cutoff);
            var rows = new List<ScreenerEvidenceRecord>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (rows.Count == ScreenerEvidenceAsOf.MaximumRecords) throw new EvidenceBindingException(ScreenerEvidenceAsOf.BoundExceeded);
                var row = ScreenerEvidenceV02Store.Read(reader); rows.Add(row); Retain(row);
            }
            return rows.ToArray();
        }
        Guid? exchange = request.ScopeKind == ScreenerScopeKind.EXCHANGE ? request.SubjectId : null;
        if (request.ScopeKind == ScreenerScopeKind.INSTRUMENT && request.Claim != EvidenceClaim.StableIdentity)
        {
            var identityRequest = request with { Claim = EvidenceClaim.StableIdentity };
            var identities = await Load(EvidenceClaim.StableIdentity, null);
            var identity = ScreenerEvidenceAsOf.Resolve(identityRequest, identities, retained);
            if (identity.FailureReason is not null) return ScreenerEvidenceAsOf.Failure(request, identity.FailureReason);
            var selected = identity.Facts.SingleOrDefault()?.Selected;
            if (identity.Quality == EvidenceQuality.Verified && selected is not null) exchange = selected.ExchangeId;
            if (identity.Quality == EvidenceQuality.Conflicting)
                return ScreenerEvidenceAsOf.Failure(request, ScreenerEvidenceReasons.IdentityConflict);
        }
        var rows = await Load(request.Claim, exchange);
        var queue = new Queue<ScreenerEvidenceRecord>(rows.Where(r => r.IsBound));
        var visited = new HashSet<Guid>();
        while (queue.TryDequeue(out var row))
        {
            ct.ThrowIfCancellationRequested();
            if (!visited.Add(row.EvidenceId)) continue;
            var payload = ScreenerEvidenceBinding.Decode(row);
            async Task Exact(Guid id)
            {
                if (!retained.TryGetValue(id, out var premise))
                {
                    premise = await ScreenerEvidenceV02Store.ReadExactAsync(connection, transaction, id, ct)
                        ?? throw new EvidenceBindingException(ScreenerEvidenceAsOf.InputUnavailable);
                    Retain(premise);
                }
                queue.Enqueue(premise);
            }
            if (payload.Operation == EvidenceOperation.CANCEL)
            {
                await using var target = Command("SELECT " + ScreenerEvidenceV02Store.SelectList +
                    " FROM screener_evidence_record WHERE revision_series_id=$1 AND revision_number=$2;", connection, transaction);
                target.Parameters.AddWithValue(row.RevisionSeriesId!); target.Parameters.AddWithValue(row.SupersedesRevisionNumber!.Value);
                await using var reader = await target.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) throw new EvidenceBindingException(ScreenerEvidenceAsOf.InputUnavailable);
                var original = ScreenerEvidenceV02Store.Read(reader); Retain(original); queue.Enqueue(original);
            }
            else switch (payload.Value)
            {
                case ReopeningValue r: await Exact(r.SuspensionEvidenceId); break;
                case ActionCoverageValue c:
                    foreach (var id in c.EventEvidenceIds) await Exact(id);
                    break;
                case PriceValue p: await Exact(p.ConventionEvidenceId); await Exact(p.CompletedSessionEvidenceId); break;
            }
            // Reuse the semantic write boundary's exact identity/scope/original-knowledge authentication, without appending.
            await ScreenerEvidenceV02Store.ValidateReferencesAsync(connection, transaction, row, payload, ct);
            if (row.Claim == EvidenceClaim.GenuinePriceObservation)
            {
                await using var artifact = Command("SELECT source_id,content_sha256,fetched_at FROM raw_artifact WHERE raw_artifact_id=$1;", connection, transaction);
                artifact.Parameters.AddWithValue(row.RawArtifactId!.Value);
                await using var reader = await artifact.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) throw new EvidenceBindingException(ScreenerEvidenceAsOf.InputUnavailable);
                raw[row.RawArtifactId.Value] = new(reader.GetString(0), row.RawArtifactId.Value,
                    reader.GetFieldValue<DateTimeOffset>(2), row.KnownAt, reader.GetString(1));
            }
        }
        return ScreenerEvidenceAsOf.Resolve(request, rows, retained, raw, exchange);
    }
}
