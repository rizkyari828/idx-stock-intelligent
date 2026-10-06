using System.Data;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public static class ScreenerEvidenceReadinessService
{
    public static async Task<ScreenerEvidenceReadinessResult> EvaluateAsync(NpgsqlConnection connection,
        NpgsqlTransaction? transaction, ScreenerReadinessRequest request, CancellationToken ct)
    {
        request.Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            if (transaction is null)
            {
                await using var owned = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, deadline.Token);
                await using (var ro = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, owned) { CommandTimeout = 15 })
                    await ro.ExecuteNonQueryAsync(deadline.Token);
                var result = await Snapshot(connection, owned, request, deadline.Token);
                await owned.CommitAsync(deadline.Token);
                return result;
            }
            // Every reader call validates the caller's shared READ ONLY / REPEATABLE READ transaction.
            return await Snapshot(connection, transaction, request, deadline.Token);
        }
        catch (EvidenceBindingException e) { return ScreenerEvidenceReadiness.Compose(request, [], new Dictionary<Guid, ConventionValue>(), e.Reason); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return ScreenerEvidenceReadiness.Compose(request, [], new Dictionary<Guid, ConventionValue>(), "PERSISTED_EVIDENCE_READ_UNAVAILABLE"); }
        catch (Exception e) when (e is NpgsqlException or TimeoutException)
        { ct.ThrowIfCancellationRequested(); return ScreenerEvidenceReadiness.Compose(request, [], new Dictionary<Guid, ConventionValue>(), "PERSISTED_EVIDENCE_READ_UNAVAILABLE"); }
    }

    private static async Task<ScreenerEvidenceReadinessResult> Snapshot(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ScreenerReadinessRequest request, CancellationToken ct)
    {
        var reads = new Dictionary<(Guid, EvidenceClaim, DateOnly), ScreenerEvidenceAsOfResult>();
        var examined = new HashSet<Guid>();
        var conventions = new Dictionary<Guid, ConventionValue>();
        async Task<bool> Read(Guid subject, EvidenceClaim claim, DateOnly date)
        {
            if (reads.TryGetValue((subject, claim, date), out var existing)) return existing.FailureReason is null;
            var result = await ScreenerEvidenceAsOfReader.ReadAsync(connection, transaction,
                new(subject, claim, date, request.Cutoff), ct);
            if (result.FailureReason == ScreenerEvidenceAsOf.BoundExceeded) throw new EvidenceBindingException(result.FailureReason);
            examined.UnionWith(result.ExaminedEvidenceIds ?? []);
            if (examined.Count > ScreenerEvidenceAsOf.MaximumRecords) throw new EvidenceBindingException(ScreenerEvidenceAsOf.BoundExceeded);
            reads.Add((subject, claim, date), result);
            foreach (var reference in result.References.Where(r => r.PayloadVersion == 1))
            {
                // Only exact convention IDs selected by admitted prices are decoded here; no new semantic selection.
                if (!result.Facts.Any(f => f.Quality == EvidenceQuality.Verified && f.Value is PriceValue p && p.ConventionEvidenceId == reference.EvidenceId)
                    || conventions.ContainsKey(reference.EvidenceId)) continue;
                var row = await ScreenerEvidenceV02Store.ReadExactAsync(connection, transaction, reference.EvidenceId, ct);
                if (row is null || ScreenerEvidenceBinding.Decode(row).Value is not ConventionValue value)
                    throw new EvidenceBindingException(ScreenerEvidenceAsOf.InputUnavailable);
                conventions.Add(reference.EvidenceId, value);
            }
            return result.FailureReason is null;
        }
        foreach (var claim in ScreenerEvidenceReadiness.MarketClaims)
            if (!await Read(request.SubjectId, claim, request.EvaluationDate))
                return ScreenerEvidenceReadiness.Compose(request, reads.Values.ToArray(), conventions);
        foreach (var subject in new[] { request.SubjectId, request.Benchmark?.SubjectId }.Where(id => id.HasValue).Select(id => id!.Value))
        {
            var failed = false;
            foreach (var day in request.Dates)
            {
                if (failed) break;
                foreach (var claim in ScreenerEvidenceReadiness.HistoryClaims)
                    if (!await Read(subject, claim, day)) { failed = true; break; }
            }
        }
        return ScreenerEvidenceReadiness.Compose(request, reads.Values.ToArray(), conventions);
    }
}
