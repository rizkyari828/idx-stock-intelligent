using System.Data;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

/// <summary>
/// Read-only projection of the thirteen existing technical features for one instrument and date. It
/// prepares a calendar-continuous canonical bar series and calls the public Application facade; it owns
/// no formula, admission, cache, persistence or provider access.
/// </summary>
public sealed class TechnicalFeatureService(NpgsqlDataSource dataSource)
{
    // Canonical rows alone do not clear volume units/basis/segment; certification stays with the Screener.
    private const string VolumeReason = "VOLUME_BASIS_UNVERIFIED";
    private const string MissingCurrentBar = "MISSING_CURRENT_BAR";

    public async Task<TechnicalFeaturesReport> ReadAsync(Guid id, DateOnly through, DateTimeOffset cutoff, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new ArgumentException("A stable instrument ID is required.");
        if (through < ScreenerReadRequest.Anchor || through > ScreenerReadRequest.Horizon)
            throw new ArgumentException("through is outside the frozen feature horizon.");
        var reference = await ScreenerReferenceFiles.LoadAsync(ct);
        if (!reference.Available) throw new ScreenerException(503, reference.Reason!);
        var bundle = reference.Value!;
        // The frozen universe names the benchmark; without it no index classification or RS alignment is known.
        var benchmark = ScreenerReferences.Universe(bundle.Reference, cutoff).Value?.BenchmarkId;
        var index = benchmark == id;
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction) { CommandTimeout = 15 })
            await readOnly.ExecuteNonQueryAsync(ct);
        var identity = await StockDatabase.RegistryAsync(connection, transaction, id, ProductQuery.Today, ct);
        var ids = benchmark is { } other && other != id ? new[] { id, other } : new[] { id };
        var evidence = await ReadBarsAsync(connection, transaction, ids, through, cutoff, ct);
        await transaction.CommitAsync(ct);
        var stock = Valid(evidence.Where(bar => bar.InstrumentId == id));
        var aligned = benchmark is { } benchmarkId
            ? Valid(evidence.Where(bar => bar.InstrumentId == benchmarkId))
            : new Dictionary<DateOnly, DailyBar>();
        var series = TechnicalFeatures.Series(bundle.Sessions, stock, through, cutoff, ct);
        var fields = TechnicalFeatures.Project(series.Bars, aligned, index,
            series.Bars.Count == 0 ? MissingCurrentBar : null, VolumeReason, VolumeReason, ct);
        return TechnicalFeatures.Report(identity, through, cutoff, index, series, fields);
    }

    // Only the already-selected visible revision per session is decoded; an invalid revision stays a gap.
    private static Dictionary<DateOnly, DailyBar> Valid(IEnumerable<ScreenerBarEvidence> evidence)
    {
        var bars = new Dictionary<DateOnly, DailyBar>();
        foreach (var row in evidence)
            if (row.Validate() is { Available: true, Value: { } revision }) bars[row.SessionDate] = revision.Bar;
        return bars;
    }

    private static async Task<IReadOnlyList<ScreenerBarEvidence>> ReadBarsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid[] ids, DateOnly through, DateTimeOffset cutoff, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            WITH selected AS (
                SELECT DISTINCT ON (r.instrument_id, r.session_date) r.*,a.source_id,a.content_sha256 AS raw_hash,a.fetched_at
                FROM daily_bar_revision r JOIN raw_artifact a USING(raw_artifact_id)
                WHERE r.instrument_id = ANY($1) AND r.session_date >= $2 AND r.session_date <= $3
                    AND r.known_at <= $4 AND a.fetched_at <= $4
                    AND (r.retrieved_at IS NULL OR r.retrieved_at <= $4)
                    AND (r.session_known_at IS NULL OR r.session_known_at <= $4)
                ORDER BY r.instrument_id, r.session_date, r.known_at DESC, r.revision_number DESC
            )
            SELECT instrument_id,session_date,revision_number,known_at,canonical_content_sha256,
                ingestion_run_id,raw_artifact_id,source_id,raw_hash,fetched_at,retrieved_at,
                session_reference,session_known_at,quality_status,open::text,high::text,low::text,
                close::text,volume,adjusted_close::text,volume_unit,volume_basis,market_segment
            FROM selected ORDER BY instrument_id,session_date
            """, connection, transaction) { CommandTimeout = 15 };
        command.Parameters.AddWithValue(ids);
        command.Parameters.AddWithValue(ScreenerReadRequest.Anchor);
        command.Parameters.AddWithValue(through);
        command.Parameters.AddWithValue(cutoff);
        var rows = new List<ScreenerBarEvidence>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(ScreenerEvidenceDatabase.ReadBar(reader));
        return rows;
    }
}
