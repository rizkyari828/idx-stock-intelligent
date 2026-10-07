using System.Data;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public static class ScreenerEvidenceTechnicalService
{
    public static async Task<ScreenerEvidenceTechnicalResult> EvaluateAsync(NpgsqlConnection connection,
        NpgsqlTransaction? transaction, ScreenerReadinessRequest request, CancellationToken ct)
    {
        request.Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        async Task<ScreenerEvidenceTechnicalResult> Execute(NpgsqlTransaction snapshot)
        {
            var input = await ScreenerEvidenceReadinessService.ReadForTechnicalAsync(connection, snapshot, request, deadline.Token);
            return ScreenerEvidenceTechnical.Execute(input.Current, input.History, deadline.Token);
        }
        try
        {
            if (transaction is not null) return await Execute(transaction);
            await using var owned = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, deadline.Token);
            await using (var ro = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, owned) { CommandTimeout = 15 })
                await ro.ExecuteNonQueryAsync(deadline.Token);
            var result = await Execute(owned);
            await owned.CommitAsync(deadline.Token);
            return result;
        }
        catch (EvidenceBindingException e) { return Failure(e.Reason); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Failure("PERSISTED_EVIDENCE_READ_UNAVAILABLE"); }
        catch (Exception e) when (e is NpgsqlException or TimeoutException)
        { ct.ThrowIfCancellationRequested(); return Failure("PERSISTED_EVIDENCE_READ_UNAVAILABLE"); }
        ScreenerEvidenceTechnicalResult Failure(string reason) => ScreenerEvidenceTechnical.NotEvaluated(
            ScreenerEvidenceReadiness.Compose(request, [], new Dictionary<Guid, ConventionValue>(), reason), reason);
    }
}
