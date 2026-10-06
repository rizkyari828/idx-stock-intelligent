using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

// Opt-in only, against the disposable DB owned by scripts/test_screener_evidence.py.
public sealed class ScreenerEvidenceV02DatabaseTests
{
    public static bool DatabaseConfigured => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDX_SCREENER_TEST_CONNECTION"));

    private static readonly Guid Subject = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly DateOnly Day = new(2026, 9, 15);
    private static readonly DateTimeOffset Known = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    internal static NpgsqlConnection OwnedConnection()
    {
        var config = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDX_SCREENER_TEST_CONNECTION"));
        if (config.Host != "127.0.0.1" || config.Database is null || !config.Database.StartsWith("idx_screener_test_", StringComparison.Ordinal)
            || !config.Database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) throw new InvalidOperationException("Owned loopback disposable database required.");
        return new NpgsqlConnection(config.ConnectionString);
    }

    internal static async Task<NpgsqlConnection> Fixture(CancellationToken ct)
    {
        var connection = OwnedConnection();
        try
        {
            await connection.OpenAsync(ct);
            await using var create = new NpgsqlCommand("""
                CREATE TEMP TABLE screener_evidence_record (LIKE public.screener_evidence_record INCLUDING ALL);
                CREATE TRIGGER immutable_screener_evidence BEFORE UPDATE OR DELETE ON screener_evidence_record
                    FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
                CREATE TRIGGER immutable_screener_evidence_truncate BEFORE TRUNCATE ON screener_evidence_record
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
                """, connection) { CommandTimeout = 15 };
            await create.ExecuteNonQueryAsync(ct);
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static ScreenerEvidenceRecord Record(
        Guid? evidenceId = null,
        string? seriesReference = "record-1",
        long revision = 1,
        long? supersedes = null,
        string source = "provider-a",
        string payload = "{\"symbol\":\"TEST\"}",
        DateOnly? effectiveTo = null,
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? retrievedAt = null,
        Guid? rawArtifactId = null,
        EvidenceClaim claim = EvidenceClaim.StableIdentity)
    {
        var revisionSeriesId = seriesReference is null
            ? null
            : ScreenerEvidenceRevisionSeries.Canonical(source, seriesReference);
        return new(
            evidenceId ?? Guid.NewGuid(),
            Subject,
            claim,
            ScreenerEvidenceV02.PolicyId,
            ScreenerEvidenceV02.SchemaVersion,
            revisionSeriesId,
            revision,
            supersedes,
            SourceAuthorityTier.T1Governing,
            Day,
            effectiveTo,
            Known.AddHours(-2),
            publishedAt,
            retrievedAt,
            Known,
            source,
            "https://reference.example/evidence",
            rawArtifactId,
            payload);
    }

    private static async Task<int> CountAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT count(*)::int FROM screener_evidence_record;", connection) { CommandTimeout = 15 };
        return (int)(await command.ExecuteScalarAsync(ct))!;
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task AppendRoundTripsAllChronologyFields()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var record = Record(publishedAt: Known.AddHours(-3), retrievedAt: Known.AddHours(-1));

        var result = await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, record, ct);
        Assert.Equal(EvidenceWriteDisposition.Inserted, result.Disposition);

        var stored = await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, record.EvidenceId, ct);
        Assert.NotNull(stored);
        Assert.Equal(record.EffectiveFrom, stored!.EffectiveFrom);
        Assert.Equal(record.EffectiveAt, stored.EffectiveAt);
        Assert.Equal(record.PublishedAt, stored.PublishedAt);
        Assert.Equal(record.RetrievedAt, stored.RetrievedAt);
        Assert.Equal(record.KnownAt, stored.KnownAt);
        Assert.NotNull(stored.RecordedAt);
        Assert.Equal(record.PayloadSha256, stored.PayloadSha256);
        Assert.Equal(SourceAuthorityTier.T1Governing, stored.AuthorityTier);
        Assert.Equal(record.Payload, stored.Payload);
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task NullableClocksAndOpenEndedScopeRemainExplicit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var record = Record(effectiveTo: null, publishedAt: null, retrievedAt: null);

        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, record, ct);
        var stored = await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, record.EvidenceId, ct);

        Assert.NotNull(stored);
        Assert.Null(stored!.EffectiveTo);
        Assert.Null(stored.PublishedAt);
        Assert.Null(stored.RetrievedAt);
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task KnownBeforeRetrievedIsRejectedInCodeAndDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.Throws<ArgumentException>(() => Record(retrievedAt: Known.AddHours(1)));

        await using var connection = await Fixture(ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO screener_evidence_record(evidence_id, subject_id, claim, policy_id, schema_version,
                revision_series_id, revision_number, authority_tier, effective_from, known_at, retrieved_at,
                source_id, payload, payload_sha256)
            VALUES ($1,$2,'StableIdentity','screener-evidence-v0.2.0',1,'s',1,1,$3,$4,$5,'p','{}',repeat('a',64));
            """, connection) { CommandTimeout = 15 };
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(Subject);
        command.Parameters.AddWithValue(Day);
        command.Parameters.AddWithValue(Known);
        command.Parameters.AddWithValue(Known.AddHours(1));

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(ct));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task SameSeriesRetainsBothRevisionsAndHistoricalRevision()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var rev10 = Record(seriesReference: "record-1", revision: 10);
        var rev11 = Record(seriesReference: "record-1", revision: 11, supersedes: 10);

        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, rev10, ct);
        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, rev11, ct);

        var history = await ScreenerEvidenceV02Store.ReadSeriesAsync(connection, null, rev10.RevisionSeriesId!, ct);
        Assert.Equal(new long[] { 10, 11 }, history.Select(r => r.RevisionNumber));

        var original = await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, rev10.EvidenceId, ct);
        Assert.NotNull(original);
        Assert.Equal(10, original!.RevisionNumber);
        Assert.Null(original.SupersedesRevisionNumber);
        Assert.Equal(10, rev11.SupersedesRevisionNumber);
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task SameProviderDifferentSeriesRemainIndependent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var seriesA = Record(seriesReference: "record-a", revision: 10, source: "provider-a");
        var seriesB = Record(seriesReference: "record-b", revision: 11, source: "provider-a");

        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, seriesA, ct);
        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, seriesB, ct);

        Assert.Single(await ScreenerEvidenceV02Store.ReadSeriesAsync(connection, null, seriesA.RevisionSeriesId!, ct));
        Assert.Single(await ScreenerEvidenceV02Store.ReadSeriesAsync(connection, null, seriesB.RevisionSeriesId!, ct));
        Assert.Equal(2, await CountAsync(connection, ct));
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task DifferentSourcesWithSameNativeReferenceStayIndependent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var sourceA = Record(seriesReference: "123", revision: 1, source: "SOURCE_A");
        var sourceB = Record(seriesReference: "123", revision: 1, source: "SOURCE_B");

        Assert.NotEqual(sourceA.RevisionSeriesId, sourceB.RevisionSeriesId);

        Assert.Equal(EvidenceWriteDisposition.Inserted, (await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, sourceA, ct)).Disposition);
        Assert.Equal(EvidenceWriteDisposition.Inserted, (await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, sourceB, ct)).Disposition);
        Assert.Equal(2, await CountAsync(connection, ct));
        Assert.Single(await ScreenerEvidenceV02Store.ReadSeriesAsync(connection, null, sourceA.RevisionSeriesId!, ct));
        Assert.Single(await ScreenerEvidenceV02Store.ReadSeriesAsync(connection, null, sourceB.RevisionSeriesId!, ct));
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task NullSeriesRecordsRemainIndependent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var first = Record(seriesReference: null, revision: 1, payload: "{\"symbol\":\"ONE\"}");
        var second = Record(seriesReference: null, revision: 1, payload: "{\"symbol\":\"TWO\"}");

        Assert.Equal(EvidenceWriteDisposition.Inserted, (await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, first, ct)).Disposition);
        Assert.Equal(EvidenceWriteDisposition.Inserted, (await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, second, ct)).Disposition);
        Assert.Equal(2, await CountAsync(connection, ct));
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ExactDuplicateRetryIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var record = Record();

        Assert.Equal(EvidenceWriteDisposition.Inserted, (await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, record, ct)).Disposition);
        Assert.Equal(EvidenceWriteDisposition.DuplicateIgnored, (await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, record, ct)).Disposition);
        Assert.Equal(1, await CountAsync(connection, ct));
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task SameIdentityWithDifferentContentFailsExplicitly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var id = Guid.NewGuid();
        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, Record(evidenceId: id), ct);

        var changedPayload = Record(evidenceId: id, payload: "{\"symbol\":\"CHANGED\"}");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, changedPayload, ct));

        var seriesOriginal = Record(seriesReference: "record-x", revision: 5, payload: "{\"v\":1}");
        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, seriesOriginal, ct);
        var seriesConflicting = Record(seriesReference: "record-x", revision: 5, payload: "{\"v\":2}");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, seriesConflicting, ct));
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task SeriesHistoryOrderIsDeterministicRegardlessOfAppendOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);

        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, Record(seriesReference: "record-ordered", revision: 11, supersedes: 10), ct);
        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, Record(seriesReference: "record-ordered", revision: 10), ct);

        var history = await ScreenerEvidenceV02Store.ReadSeriesAsync(
            connection, null, ScreenerEvidenceRevisionSeries.Canonical("provider-a", "record-ordered"), ct);
        Assert.Equal(new long[] { 10, 11 }, history.Select(r => r.RevisionNumber));
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task RollbackLeavesNoDurableEvidence()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var record = Record();

        await using (var transaction = await connection.BeginTransactionAsync(ct))
        {
            Assert.Equal(EvidenceWriteDisposition.Inserted, (await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, transaction, record, ct)).Disposition);
            await transaction.RollbackAsync(ct);
        }

        Assert.Null(await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, record.EvidenceId, ct));
        Assert.Equal(0, await CountAsync(connection, ct));
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task EvidenceRecordsAreImmutableUnderUpdateAndDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var record = Record();
        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, record, ct);

        await using var update = new NpgsqlCommand("UPDATE screener_evidence_record SET source_id='x' WHERE evidence_id=$1;", connection) { CommandTimeout = 15 };
        update.Parameters.AddWithValue(record.EvidenceId);
        await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync(ct));

        await using var delete = new NpgsqlCommand("DELETE FROM screener_evidence_record WHERE evidence_id=$1;", connection) { CommandTimeout = 15 };
        delete.Parameters.AddWithValue(record.EvidenceId);
        await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync(ct));
    }

    [Fact(Skip = "Opt-in disposable PostgreSQL evidence test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task V01SchemaAndTablesRemainUnaffected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);

        await using (var version = new NpgsqlCommand("SELECT max(version)::int FROM pilot_schema_version;", connection) { CommandTimeout = 15 })
        {
            Assert.Equal(8, (int)(await version.ExecuteScalarAsync(ct))!);
        }

        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, Record(), ct);

        await using var v01 = new NpgsqlCommand("SELECT count(*)::int FROM daily_bar_revision;", connection) { CommandTimeout = 15 };
        Assert.Equal(0, (int)(await v01.ExecuteScalarAsync(ct))!);
    }
}
