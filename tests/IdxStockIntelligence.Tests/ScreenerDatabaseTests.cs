using System.Data;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

// Standard discovery tests; opt in only against the disposable DB owned by test_screener_evidence.py.
public sealed class ScreenerDatabaseTests
{
    public static bool DatabaseConfigured => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDX_SCREENER_TEST_CONNECTION"));
    private static readonly Guid Stock = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid Index = Guid.Parse("10000000-0000-4000-8000-000000000002");
    private static readonly Guid Outside = Guid.Parse("10000000-0000-4000-8000-000000000003");
    private static readonly DateOnly Day = new(2026, 9, 15);
    private static readonly DateTimeOffset Known = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static NpgsqlConnection OwnedConnection()
    {
        var config = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDX_SCREENER_TEST_CONNECTION"));
        if (config.Host != "127.0.0.1" || config.Database is null || !config.Database.StartsWith("idx_screener_test_", StringComparison.Ordinal)
            || !config.Database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) throw new InvalidOperationException("Owned loopback disposable database required.");
        return new NpgsqlConnection(config.ConnectionString);
    }

    private static async Task<NpgsqlConnection> Fixture(CancellationToken ct)
    {
        var connection = OwnedConnection();
        try
        {
            await connection.OpenAsync(ct);
            // Copies the real schema's constraints/types. All fixture tables are connection-local and vanish on close.
            await using var create = new NpgsqlCommand("""
                CREATE TEMP TABLE raw_artifact (LIKE public.raw_artifact INCLUDING ALL);
                CREATE TEMP TABLE daily_bar_revision (LIKE public.daily_bar_revision INCLUDING ALL);
                CREATE TEMP TABLE instrument_listing_evidence (LIKE public.instrument_listing_evidence INCLUDING ALL);
                INSERT INTO raw_artifact VALUES ('30000000-0000-4000-8000-000000000001',
                    '20000000-0000-4000-8000-000000000001','synthetic','fixture:only','{}',
                    '2026-09-30T11:59:00Z','fixture:only',repeat('c',64),0,'synthetic');
                INSERT INTO raw_artifact VALUES ('30000000-0000-4000-8000-000000000002',
                    '20000000-0000-4000-8000-000000000001','synthetic','fixture:only','{}',
                    '2026-10-20T12:00:00Z','fixture:only',repeat('d',64),0,'synthetic');
                """, connection) { CommandTimeout = 15 };
            await create.ExecuteNonQueryAsync(ct);
            foreach (var id in new[] { Stock, Index, Outside })
            {
                await Append(connection, id, Day, 1, Known, "100", 'a', "DEGRADED", ct);
                await Append(connection, id, Day, 2, Known.AddDays(1), "110", 'b', "DEGRADED", ct);
                await Append(connection, id, Day, 3, Known.AddDays(2), "100", 'a', "DEGRADED", ct);
                await Append(connection, id, Day, 4, Known.AddDays(3), "120", 'e', "REJECTED", ct);
                await Append(connection, id, Day, 5, Known.AddDays(4), "1000000000000000000000000000000", 'f', "DEGRADED", ct);
                await Append(connection, id, Day, 6, Known.AddDays(4), "130", 'b', "DEGRADED", ct); // Same-known-at revision tie.
                await Append(connection, id, Day, 7, Known.AddDays(5), "140", 'e', "DEGRADED", ct, missingRetrieval: true);
                await Append(connection, id, Day, 8, Known.AddDays(6), "150", 'f', "DEGRADED", ct, futureSource: true);
                await Append(connection, id, Day.AddDays(1), 1, Known, "999", 'b', "DEGRADED", ct);
                await Append(connection, id, ScreenerReadRequest.Anchor.AddDays(-1), 1, Known, "999", 'b', "DEGRADED", ct);
            }
            await using var listing = new NpgsqlCommand("""
                INSERT INTO instrument_listing_evidence VALUES ($1,$2,$3::jsonb),($1,$4,$5::jsonb);
                """, connection);
            listing.Parameters.AddWithValue(Stock); listing.Parameters.AddWithValue(Known);
            listing.Parameters.AddWithValue("{\"symbol\":\"OLD\",\"listed_on\":\"2000-01-01\",\"status\":\"VERIFIED\",\"reference\":\"https://reference.example/listing\",\"confidence\":\"PRIMARY_SOURCE\"}");
            listing.Parameters.AddWithValue(Known.AddDays(5));
            listing.Parameters.AddWithValue("{\"symbol\":\"NEW\",\"listed_on\":null,\"status\":\"UNKNOWN\",\"reference\":\"https://reference.example/listing\",\"confidence\":\"UNKNOWN\"}");
            await listing.ExecuteNonQueryAsync(ct);
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static async Task Append(NpgsqlConnection connection, Guid id, DateOnly date, long revision,
        DateTimeOffset known, string close, char hash, string quality, CancellationToken ct,
        bool missingRetrieval = false, bool futureSource = false)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO daily_bar_revision(instrument_id,session_date,revision_number,known_at,
                ingestion_run_id,raw_artifact_id,canonical_content_sha256,open,high,low,close,volume,quality_status,
                volume_unit,volume_basis,market_segment,retrieved_at,session_reference,session_known_at)
            VALUES ($1,$2,$3,$4,'20000000-0000-4000-8000-000000000001',$5,$6,
                $7::numeric,$7::numeric,$7::numeric,$7::numeric,$8,$9,'UNKNOWN','UNKNOWN','UNKNOWN',$10,
                'https://reference.example/session',$4);
            """, connection) { CommandTimeout = 15 };
        command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(date);
        command.Parameters.AddWithValue(revision); command.Parameters.AddWithValue(known);
        command.Parameters.AddWithValue(Guid.Parse(futureSource ? "30000000-0000-4000-8000-000000000002" : "30000000-0000-4000-8000-000000000001"));
        command.Parameters.AddWithValue(new string(hash, 64)); command.Parameters.AddWithValue(close);
        command.Parameters.AddWithValue(id == Index ? 0L : 100L); command.Parameters.AddWithValue(quality);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.TimestampTz,
            Value = missingRetrieval ? DBNull.Value : known.AddMinutes(-1) });
        await command.ExecuteNonQueryAsync(ct);
    }
    private static ScreenerReadRequest Request(DateTimeOffset cutoff) => new([Stock], Index, ScreenerReadRequest.Anchor, Day, cutoff);
    private static async Task<ScreenerDatabaseEvidence> Read(NpgsqlConnection connection, ScreenerReadRequest request, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction)) await readOnly.ExecuteNonQueryAsync(ct);
        var result = await ScreenerEvidenceDatabase.ReadAsync(connection, transaction, request, ct);
        Assert.True(result.Available, result.Reason);
        await transaction.CommitAsync(ct);
        return result.Value!;
    }

    [Theory(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData(0, 1, "100", null)]
    [InlineData(1, 2, "110", null)]
    [InlineData(2, 3, "100", null)]
    [InlineData(3, 4, "120", "CANONICAL_QUALITY_UNAVAILABLE")]
    [InlineData(4, 6, "130", null)]
    [InlineData(5, 7, "140", "CANONICAL_PROVENANCE_UNAVAILABLE")]
    [InlineData(6, 7, "140", "CANONICAL_PROVENANCE_UNAVAILABLE")]
    public async Task SqlSelectsChronologyBeforeValidationEquallyForStockAndIndex(int day, long revision, string close, string? reason)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var rows = await Read(connection, Request(Known.AddDays(day)), ct);
        Assert.Equal(2, rows.Bars.Count); // Only requested stock and separately supplied benchmark.
        foreach (var id in new[] { Stock, Index })
        {
            var row = Assert.Single(rows.Bars, r => r.InstrumentId == id);
            Assert.Equal(Day, row.SessionDate); Assert.Equal(revision, row.RevisionNumber); Assert.Equal(close, row.Close);
            Assert.Equal(reason, row.Validate().Reason);
            Assert.Equal(id == Index ? 0 : 100, row.Volume);
            Assert.Equal("synthetic", row.SourceId); Assert.Equal(new string('c', 64), row.RawHash);
            Assert.Equal(Known.AddDays(revision == 6 ? 4 : revision == 7 ? 5 : day), row.KnownAt);
        }
        Assert.DoesNotContain(rows.Bars, r => r.InstrumentId == Outside || r.SessionDate > Day || r.SessionDate < ScreenerReadRequest.Anchor);
        Assert.Equal(day < 5 ? "OLD" : "NEW", Assert.Single(rows.Listings).Resolution.Value!.Symbol);
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task SqlSelectedFutureAppendCannotChangeEarlierDigestAndInvalidOverflowNeverFallsBack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        // Remove only the same-time tie in the owned TEMP table so its oversized predecessor is selected.
        await using (var remove = new NpgsqlCommand("DELETE FROM pg_temp.daily_bar_revision WHERE revision_number=6", connection)) await remove.ExecuteNonQueryAsync(ct);
        var overflow = await Read(connection, Request(Known.AddDays(4)), ct);
        Assert.All(overflow.Bars, r => { Assert.Equal(5, r.RevisionNumber); Assert.Equal("NUMERIC_OUT_OF_RANGE", r.Validate().Reason); });
        var request = Request(Known); var before = await Read(connection, request, ct);
        var references = new SelectedScreenerReferences([], [], [], []);
        var digest = ScreenerReferences.SelectedDigest(request, before, references, ct);
        await Append(connection, Stock, Day, 9, Known.AddDays(10), "200", 'b', "DEGRADED", ct);
        await Append(connection, Index, Day.AddDays(2), 1, Known, "200", 'b', "DEGRADED", ct);
        var after = await Read(connection, request, ct);
        Assert.Equal(digest, ScreenerReferences.SelectedDigest(request, after, references, ct));
        var correctedRequest = Request(Known.AddDays(1));
        Assert.NotEqual(digest, ScreenerReferences.SelectedDigest(correctedRequest, await Read(connection, correctedRequest, ct), references, ct));
        Assert.Empty((await Read(connection, Request(Known.AddTicks(-1)), ct)).Bars);
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task WaitingDatabaseReadHonorsCancellation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var locker = OwnedConnection(); await locker.OpenAsync(ct);
        await using var lockTransaction = await locker.BeginTransactionAsync(ct);
        await using (var command = new NpgsqlCommand("LOCK TABLE daily_bar_revision IN ACCESS EXCLUSIVE MODE", locker, lockTransaction))
            await command.ExecuteNonQueryAsync(ct);
        await using var reader = OwnedConnection(); await reader.OpenAsync(ct);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(150));
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ScreenerEvidenceDatabase.ReadAsync(reader, null, Request(Known), cancel.Token));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
        await lockTransaction.RollbackAsync(ct);
    }
}
