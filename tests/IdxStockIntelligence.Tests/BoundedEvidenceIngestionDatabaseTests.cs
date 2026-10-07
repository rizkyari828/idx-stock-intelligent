using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class BoundedEvidenceIngestionDatabaseTests
{
    public static bool DatabaseConfigured => ScreenerEvidenceV02DatabaseTests.DatabaseConfigured;
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static async Task Sql(NpgsqlConnection c, string sql)
    { await using var cmd = new NpgsqlCommand(sql, c) { CommandTimeout = 15 }; await cmd.ExecuteNonQueryAsync(Token); }
    private static async Task<long> Count(NpgsqlConnection c, string table)
    { await using var cmd = new NpgsqlCommand("SELECT count(*) FROM " + table, c); return (long)(await cmd.ExecuteScalarAsync(Token))!; }
    private sealed class Fixture(NpgsqlConnection connection, string root) : IAsyncDisposable
    {
        internal NpgsqlConnection Connection => connection;
        internal string Root => root;
        internal static async Task<Fixture> Create()
        {
            var c = await ScreenerEvidenceV02DatabaseTests.Fixture(Token);
            await Sql(c, """
                CREATE TEMP TABLE source (LIKE public.source INCLUDING ALL);
                CREATE TEMP TABLE ingestion_run (LIKE public.ingestion_run INCLUDING ALL);
                CREATE TEMP TABLE raw_artifact (LIKE public.raw_artifact INCLUDING ALL);
                CREATE TEMP TABLE screener_technical_candidate (LIKE public.screener_technical_candidate INCLUDING ALL);
                CREATE TEMP TABLE outcome_v02_observation (LIKE public.outcome_v02_observation INCLUDING ALL);
                CREATE TRIGGER immutable_raw BEFORE UPDATE OR DELETE ON raw_artifact FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
                INSERT INTO source(source_id,display_name,terms_status) VALUES ('owned-test','DISPOSABLE SYNTHETIC SOURCE','ALLOWED');
                """);
            return new(c, Path.Combine(Directory.Exists("/private/tmp") ? "/private/tmp" : Path.GetTempPath(), "idx_ingestion_" + Guid.NewGuid().ToString("N")));
        }
        internal Task<BoundedEvidenceIngestionResult> Import(byte[] bytes, BoundedEvidenceArtifact a, BoundedEvidenceSource? source = null,
            UniverseSnapshot? universe = null) => new BoundedEvidenceIngestionService(root, [source ?? IngestionFixture.Source()])
            .ImportAsync(connection, universe ?? IngestionFixture.Universe(), IngestionFixture.Exchange, a, new MemoryStream(bytes), Token);
        public async ValueTask DisposeAsync()
        { await connection.DisposeAsync(); if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task RawFirstTypedAppendIsImmutableIdempotentAndPITVisible()
    {
        await using var f = await Fixture.Create(); var b = IngestionFixture.Bytes(); var a = IngestionFixture.Artifact(b);
        var first = await f.Import(b, a); Assert.True(first.Status == "ACCEPTED", string.Join(",", first.Diagnostics)); Assert.Equal(1, first.EvidenceAppended);
        var repeat = await f.Import(b, a); Assert.Equal(1, repeat.EvidenceAlreadyPresent); Assert.Equal(0, repeat.EvidenceAppended);
        Assert.Equal(first.ArtifactId, repeat.ArtifactId); Assert.Equal(1, await Count(f.Connection, "raw_artifact")); Assert.Equal(1, await Count(f.Connection, "screener_evidence_record"));
        await using var read = new NpgsqlCommand("SELECT content_sha256,byte_length,local_uri,parser_version FROM raw_artifact", f.Connection);
        await using (var reader = await read.ExecuteReaderAsync(Token))
        {
            Assert.True(await reader.ReadAsync(Token)); Assert.Equal(a.ContentSha256, reader.GetString(0)); Assert.Equal(b.Length, reader.GetInt64(1));
            Assert.Equal(b, await File.ReadAllBytesAsync(reader.GetString(2), Token)); Assert.Equal(a.ParserVersion, reader.GetString(3));
        }
        var at = DateTimeOffset.UtcNow;
        var visible = await ScreenerEvidenceAsOfReader.ReadAsync(f.Connection, null, new(IngestionFixture.Instrument, EvidenceClaim.Currency, IngestionFixture.Day, at), Token);
        Assert.Null(visible.FailureReason); Assert.Equal("IDR", ((CurrencyValue)Assert.Single(visible.Facts).Value!).CurrencyCode);
        var before = await ScreenerEvidenceAsOfReader.ReadAsync(f.Connection, null, new(IngestionFixture.Instrument, EvidenceClaim.Currency, IngestionFixture.Day, a.ReceivedAt.AddTicks(-1)), Token);
        Assert.Empty(before.Facts);
        var readiness = await ScreenerEvidenceReadinessService.EvaluateAsync(f.Connection, null,
            new(IngestionFixture.Instrument, "IDX-EOD", IngestionFixture.Day, IngestionFixture.Day, DateTimeOffset.UtcNow, new("owned-test", "eod", "close", "1")), Token);
        Assert.False(readiness.DataReady); Assert.Contains(readiness.References, r => r.EvidenceId == IngestionFixture.Row(a, first.ArtifactId!.Value).EvidenceId);
        Assert.Equal(0, await Count(f.Connection, "screener_technical_candidate")); Assert.Equal(0, await Count(f.Connection, "outcome_v02_observation"));
        await Assert.ThrowsAsync<PostgresException>(() => Sql(f.Connection, "UPDATE raw_artifact SET byte_length=0"));
    }
    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ValidCorrectionRetainsOriginalAndHistoricalReplay()
    {
        await using var f = await Fixture.Create(); var b = IngestionFixture.Bytes(); var a = IngestionFixture.Artifact(b);
        Assert.Equal("ACCEPTED", (await f.Import(b, a)).Status); var cutoff = DateTimeOffset.UtcNow;
        var corrected = IngestionFixture.Bytes("USD", 2); var second = IngestionFixture.Artifact(corrected) with { ReceivedAt = DateTimeOffset.UtcNow, KnownAt = DateTimeOffset.UtcNow };
        var result = await f.Import(corrected, second); Assert.Equal("ACCEPTED", result.Status); Assert.Equal(1, result.EvidenceAppended);
        Assert.Equal(2, await Count(f.Connection, "raw_artifact")); Assert.Equal(2, await Count(f.Connection, "screener_evidence_record"));
        var old = await ScreenerEvidenceAsOfReader.ReadAsync(f.Connection, null, new(IngestionFixture.Instrument, EvidenceClaim.Currency, IngestionFixture.Day, cutoff), Token);
        Assert.Equal("IDR", ((CurrencyValue)Assert.Single(old.Facts).Value!).CurrencyCode);
    }
    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ParseFailureAndPartialBatchFailureLeaveRawButNoAuthoritativeRows()
    {
        await using var f = await Fixture.Create(); var invalid = Encoding.UTF8.GetBytes("not json"); var a = IngestionFixture.Artifact(invalid);
        var result = await f.Import(invalid, a); Assert.Equal("REJECTED", result.Status); Assert.Equal(1, result.ParseFailures);
        Assert.Equal(1, await Count(f.Connection, "raw_artifact")); Assert.Equal(0, await Count(f.Connection, "screener_evidence_record"));
        var b = IngestionFixture.Bytes(); a = IngestionFixture.Artifact(b);
        var source = IngestionFixture.Source() with { Normalize = (_, artifact, raw, _) =>
            [IngestionFixture.Row(artifact, raw), IngestionFixture.Row(artifact, raw, value: new CurrencyValue("USD"), revision: 2)] };
        result = await f.Import(b, a, source); Assert.Equal("REJECTED", result.Status);
        // Revision 1's database recording time is after the batch's historical knownAt; correction cannot borrow it.
        Assert.Equal(0, await Count(f.Connection, "screener_evidence_record"));
        Assert.Equal(2, await Count(f.Connection, "raw_artifact"));
    }
    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task UnknownParserOutsideUniverseAndCorruptArchiveFailClosed()
    {
        await using var f = await Fixture.Create(); var b = IngestionFixture.Bytes(); var a = IngestionFixture.Artifact(b);
        var absent = await f.Import(b, a with { ParserVersion = "unknown" }); Assert.Equal(BoundedEvidenceIngestion.SourceUnavailable, absent.Status);
        Assert.Equal(0, await Count(f.Connection, "raw_artifact"));
        var source = IngestionFixture.Source() with { Normalize = (_, artifact, raw, _) => [IngestionFixture.Row(artifact, raw, subject: Guid.NewGuid())] };
        Assert.Equal("REJECTED", (await f.Import(b, a, source)).Status); Assert.Equal(0, await Count(f.Connection, "screener_evidence_record"));
        Assert.Equal("ACCEPTED", (await f.Import(b, a)).Status);
        var path = Directory.GetFiles(f.Root, "*.bin", SearchOption.AllDirectories).Single(); await File.WriteAllBytesAsync(path, "changed"u8.ToArray(), Token);
        Assert.Equal("REJECTED", (await f.Import(b, a)).Status); Assert.Equal("changed", await File.ReadAllTextAsync(path, Token));
        Assert.Equal(1, await Count(f.Connection, "screener_evidence_record"));
    }
    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task UnknownSourceDoesNotArchiveOrWriteAndWeakAuthorityCannotAppend()
    {
        await using var f = await Fixture.Create(); var b = IngestionFixture.Bytes(); var a = IngestionFixture.Artifact(b);
        var service = new BoundedEvidenceIngestionService(f.Root, []);
        var result = await service.ImportAsync(f.Connection, IngestionFixture.Universe(), IngestionFixture.Exchange, a, new MemoryStream(b), Token);
        Assert.Equal(BoundedEvidenceIngestion.SourceUnavailable, result.Status); Assert.False(Directory.Exists(f.Root));
        await Sql(f.Connection, "UPDATE source SET terms_status='UNKNOWN'");
        result = await f.Import(b, a); Assert.Equal("REJECTED", result.Status); Assert.Equal(1, result.SourceFailures);
        Assert.Equal(0, result.UnsupportedRows); Assert.Equal(0, await Count(f.Connection, "raw_artifact"));
    }
}
