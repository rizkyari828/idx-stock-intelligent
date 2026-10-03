using System.Data;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public sealed record ScreenerEvaluationRead(ScreenerReadRequest Request, ScreenerDatabaseEvidence Database,
    SelectedScreenerReferences References, ScreenerPortfolioHistory? History, LedgerProjection? Projection,
    ScreenerResult Result, string SelectedDigest, string InputHash);

public sealed class ScreenerService(NpgsqlDataSource dataSource)
{
    // Stable IHSG identity from the frozen contract, used only to describe missing universe evidence.
    private static readonly Guid Benchmark = Guid.Parse("76237e96-232f-5085-9b13-dcb7104222bc");

    public async Task<ScreenerResponse> ReadAsync(ScreenerQuery query, CancellationToken ct)
    {
        var copied = await ScreenerReferenceFiles.LoadAsync(ct);
        if (!copied.Available) throw new ScreenerException(503, copied.Reason!);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction) { CommandTimeout = 15 })
                await readOnly.ExecuteNonQueryAsync(ct);
            var evaluated = await EvaluateAsync(connection, transaction, copied.Value!, query, ct);
            var response = ScreenerPresentation.Map(evaluated.Result, query, evaluated.InputHash, evaluated.References, ct);
            await transaction.CommitAsync(ct);
            return response;
        }
        catch (KeyNotFoundException) { throw new ScreenerException(404, "PORTFOLIO_NOT_FOUND"); }
        catch (Exception error) when (error is NpgsqlException or TimeoutException or InvalidOperationException or ArgumentException or OverflowException)
        { ct.ThrowIfCancellationRequested(); throw new ScreenerException(503, "SCREENER_UNAVAILABLE"); }
    }

    public static async Task<ScreenerEvaluationRead> EvaluateAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ScreenerReferenceBundle bundle, ScreenerQuery query, CancellationToken ct)
    {
        var universe = ScreenerReferences.Universe(bundle.Reference, query.Cutoff);
        var history = query.PortfolioId is { } id
            ? await PortfolioDatabase.ScreenerHistoryAsync(connection, id, query.Cutoff, ct) : null;
        ct.ThrowIfCancellationRequested();
        var projection = history is null ? null : PortfolioLedger.Project(history.Events, query.Cutoff,
            query.Through, history.Portfolio.AllowNegativeCash);
        ct.ThrowIfCancellationRequested();
        var held = projection?.Positions.Where(p => p.Shares > 0).Select(p => p.InstrumentId).ToArray() ?? [];
        var request = new ScreenerReadRequest((universe.Value?.MemberIds ?? []).Concat(held).Distinct().ToArray(),
            universe.Value?.BenchmarkId ?? Benchmark, ScreenerReadRequest.Anchor, query.Through, query.Cutoff);
        if (request.BoundsReason() is { } reason) throw new ScreenerException(503, reason);
        var database = await ScreenerEvidenceDatabase.ReadAsync(connection, transaction, request, ct);
        if (!database.Available) throw new ScreenerException(503, database.Reason!);
        var references = ScreenerReferences.Select(bundle.Reference, bundle.Sessions, bundle.InstrumentSessions, request, ct);
        return Evaluate(request, database.Value!, references, history, ct);
    }

    // Capture, GET and retained-input verification share the same analytical core.
    public static ScreenerEvaluationRead Evaluate(ScreenerReadRequest request, ScreenerDatabaseEvidence database,
        SelectedScreenerReferences references, ScreenerPortfolioHistory? history, CancellationToken ct)
    {
        var projection = history is null ? null : PortfolioLedger.Project(history.Events, request.Cutoff,
            request.Through, history.Portfolio.AllowNegativeCash);
        var result = ScreenerEvaluator.Evaluate(request, database, references, history, ct);
        var digest = ScreenerReferences.SelectedDigest(request, database, references, ct);
        var hash = ScreenerPresentation.InputHash(digest, request, history, projection, ct);
        return new(request, database, references, history, projection, result, digest, hash);
    }
}
