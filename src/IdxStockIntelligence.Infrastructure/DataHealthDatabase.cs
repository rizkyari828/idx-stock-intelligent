using System.Data;
using System.Globalization;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

/// <summary>Read-only aggregate of the latest canonical bar per instrument and session.</summary>
public sealed class DataHealthDatabase(NpgsqlDataSource dataSource)
{
    private const string Sql = """
        SELECT latest.instrument_id, latest.session_date,
               coalesce(history.symbol, listing.symbol, instrument.issuer_name),
               latest.open, latest.high, latest.low, latest.close, latest.volume, latest.quality_status
        FROM (
            SELECT DISTINCT ON (instrument_id, session_date) *
            FROM daily_bar_revision
            ORDER BY instrument_id, session_date, known_at DESC, revision_number DESC
        ) AS latest
        JOIN instrument USING (instrument_id)
        LEFT JOIN LATERAL (
            SELECT symbol FROM instrument_history
            WHERE instrument_id = latest.instrument_id AND valid_from <= CURRENT_DATE
              AND (valid_to IS NULL OR valid_to >= CURRENT_DATE)
            ORDER BY valid_from DESC LIMIT 1
        ) AS history ON true
        LEFT JOIN LATERAL (
            SELECT nullif(trim(evidence->>'symbol'), '') AS symbol FROM instrument_listing_evidence
            WHERE instrument_id = latest.instrument_id
            ORDER BY known_at DESC LIMIT 1
        ) AS listing ON true
        ORDER BY latest.session_date DESC,
                 coalesce(history.symbol, listing.symbol, instrument.issuer_name)
        """;

    public async Task<DataHealthReport> ReadAsync(CancellationToken ct)
    {
        var bars = new List<DataHealthBar>();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction) { CommandTimeout = 15 })
            await readOnly.ExecuteNonQueryAsync(ct);
        await using (var command = new NpgsqlCommand(Sql, connection, transaction) { CommandTimeout = 30 })
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                ct.ThrowIfCancellationRequested();
                bars.Add(new(reader.GetGuid(0), reader.GetString(2), reader.GetFieldValue<DateOnly>(1),
                    reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6),
                    reader.GetInt64(7), reader.GetString(8)));
            }
        }
        await transaction.CommitAsync(ct);
        return DataHealthReport.Build(DateTimeOffset.UtcNow, ExpectedPanel(), bars,
            DataHealthCalendar.Load(Path.GetFullPath("pilot/holidays-2026.json")));
    }

    private static int ExpectedPanel()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath("pilot/universe.json")));
        var count = document.RootElement.GetProperty("instruments").GetArrayLength();
        if (count == 0) throw new InvalidOperationException("DATA_HEALTH_PANEL_EMPTY");
        return count;
    }
}

/// <summary>The cited closure fixture, if present. A missing file means no date is known closed.</summary>
public static class DataHealthCalendar
{
    public static IReadOnlyDictionary<DateOnly, DataHealthHoliday> Load(string path)
    {
        if (!File.Exists(path)) return new Dictionary<DateOnly, DataHealthHoliday>();
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("holidays", out var entries) || entries.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("DATA_HEALTH_HOLIDAY_FIXTURE_INVALID");
        var holidays = new Dictionary<DateOnly, DataHealthHoliday>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("date", out var date)
                || !DateOnly.TryParseExact(date.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day))
                throw new InvalidOperationException("DATA_HEALTH_HOLIDAY_FIXTURE_INVALID");
            var name = entry.TryGetProperty("name", out var value) ? value.GetString() ?? "" : "";
            var kind = entry.TryGetProperty("kind", out var kindValue) ? kindValue.GetString() ?? "" : "";
            holidays[day] = new(day, name, kind);
        }
        return holidays;
    }
}
