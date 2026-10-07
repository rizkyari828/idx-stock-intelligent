using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceV02BindingDatabaseTests
{
    public static bool DatabaseConfigured => ScreenerEvidenceV02DatabaseTests.DatabaseConfigured;
    private static Task<NpgsqlConnection> Fixture(CancellationToken ct) => ScreenerEvidenceV02DatabaseTests.Fixture(ct);
    internal static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IdxStockIntelligence.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root required.");
    }
    private static async Task<int> Sql(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 15 };
        return await command.ExecuteNonQueryAsync(ct);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task MigrationPreservesPreBindingRowsAndIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = ScreenerEvidenceV02DatabaseTests.OwnedConnection();
        await connection.OpenAsync(ct);
        var schema = "binding_test_" + Guid.NewGuid().ToString("N");
        await Sql(connection, "CREATE SCHEMA " + schema + "; SET search_path TO " + schema + ";", ct);
        try
        {
            var migrations = Directory.GetFiles(Path.Combine(Root(), "src/IdxStockIntelligence.Infrastructure/Migrations"), "*.sql").Order(StringComparer.Ordinal).ToArray();
            foreach (var path in migrations.Where(p => string.CompareOrdinal(Path.GetFileName(p), "0008") < 0))
                await Sql(connection, await File.ReadAllTextAsync(path, ct), ct);
            var legacy = EvidenceBindingFixture.Envelope(EvidenceClaim.StableIdentity);
            await using (var insert = new NpgsqlCommand("""
                INSERT INTO screener_evidence_record(evidence_id, subject_id, claim, policy_id, schema_version,
                    revision_number, authority_tier, effective_from, known_at, source_id, payload, payload_sha256)
                VALUES ($1,$2,'StableIdentity','screener-evidence-v0.2.0',1,1,1,$3,$4,'source-a',$5,$6);
                """, connection) { CommandTimeout = 15 })
            {
                insert.Parameters.AddWithValue(legacy.EvidenceId); insert.Parameters.AddWithValue(legacy.SubjectId);
                insert.Parameters.AddWithValue(legacy.EffectiveFrom); insert.Parameters.AddWithValue(legacy.KnownAt);
                insert.Parameters.AddWithValue(legacy.Payload); insert.Parameters.AddWithValue(legacy.PayloadSha256);
                await insert.ExecuteNonQueryAsync(ct);
            }
            await using var fingerprint = new NpgsqlCommand("SELECT md5(to_jsonb(t)::text) FROM screener_evidence_record t;", connection) { CommandTimeout = 15 };
            var before = (string)(await fingerprint.ExecuteScalarAsync(ct))!;
            var migration = await File.ReadAllTextAsync(migrations.Single(p => Path.GetFileName(p).StartsWith("0008", StringComparison.Ordinal)), ct);
            await Sql(connection, migration, ct);
            await Sql(connection, migration, ct);
            var retained = await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, legacy.EvidenceId, ct);
            Assert.NotNull(retained); Assert.False(retained.IsBound); Assert.Null(retained.EvidenceClass);
            Assert.Null(retained.ScopeKind); Assert.Null(retained.ScopeExchangeId);
            Assert.Equal("PERSISTED_EVIDENCE_UNBOUND", Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Decode(retained)).Reason);
            fingerprint.CommandText = "SELECT md5((to_jsonb(t) - 'evidence_class' - 'payload_schema_version' - 'scope_kind' - 'scope_exchange_id')::text) FROM screener_evidence_record t;";
            Assert.Equal(before, (string)(await fingerprint.ExecuteScalarAsync(ct))!);
            fingerprint.CommandText = "SELECT max(version)::int FROM pilot_schema_version;";
            Assert.Equal(8, (int)(await fingerprint.ExecuteScalarAsync(ct))!);
        }
        finally { await Sql(connection, "SET search_path TO public; DROP SCHEMA " + schema + " CASCADE;", ct); }
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task BoundRowsRoundTripBindingsAndRetainImmutableRevisionIdempotency()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var original = EvidenceBindingFixture.Bound(EvidenceClaim.StableIdentity, new IdentityValue("ORIGINAL"));
        var correction = EvidenceBindingFixture.Bound(EvidenceClaim.StableIdentity, new IdentityValue("CORRECTION"),
            EvidenceBindingFixture.Envelope(EvidenceClaim.StableIdentity, revision: 2, supersedes: 1));
        Assert.Equal(EvidenceWriteDisposition.Inserted, (await ScreenerEvidenceV02Store.AppendAsync(connection, null, original, ct)).Disposition);
        Assert.Equal(EvidenceWriteDisposition.DuplicateIgnored, (await ScreenerEvidenceV02Store.AppendAsync(connection, null, original, ct)).Disposition);
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, correction, ct);
        var retained = await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, original.EvidenceId, ct);
        Assert.NotNull(retained); Assert.Equal(original.EvidenceClass, retained.EvidenceClass);
        Assert.Equal(1, retained.PayloadSchemaVersion); Assert.Equal(original.ScopeKind, retained.ScopeKind);
        Assert.Equal(original.ScopeExchangeId, retained.ScopeExchangeId); Assert.Equal(original.PayloadSha256, retained.PayloadSha256);
        Assert.True(ScreenerEvidenceEquality.FactualEquals(original, retained));
        Assert.Equal(new long[] { 1, 2 }, (await ScreenerEvidenceV02Store.ReadSeriesAsync(connection, null, original.RevisionSeriesId!, ct)).Select(r => r.RevisionNumber));
        await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, original, ct));
        await Assert.ThrowsAsync<PostgresException>(() => Sql(connection, "UPDATE screener_evidence_record SET payload='{}';", ct));
        await Assert.ThrowsAsync<PostgresException>(() => Sql(connection, "DELETE FROM screener_evidence_record;", ct));
        await Assert.ThrowsAsync<PostgresException>(() => Sql(connection, "TRUNCATE screener_evidence_record;", ct));
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("BAD_CLASS", 1, "INSTRUMENT", false)]
    [InlineData("SESSION_FACT", 1, "BAD_SCOPE", false)]
    [InlineData("SESSION_FACT", 0, "INSTRUMENT", false)]
    [InlineData("SESSION_FACT", -1, "INSTRUMENT", false)]
    [InlineData("SESSION_FACT", null, "INSTRUMENT", false)]
    [InlineData("SESSION_FACT", 1, "EXCHANGE", false)]
    [InlineData("SESSION_FACT", 2, "INSTRUMENT", true)]
    [InlineData(null, null, null, true)]
    [InlineData("SESSION_FACT", 1, "INSTRUMENT", true)]
    public async Task DatabaseBindingConstraintsAcceptOnlyCompleteOrUnboundEnvelopes(string? cls, int? version, string? scope, bool accepted)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO screener_evidence_record(evidence_id, subject_id, claim, policy_id, schema_version,
                revision_number, authority_tier, effective_from, effective_to, known_at, source_id, payload,
                payload_sha256, evidence_class, payload_schema_version, scope_kind, scope_exchange_id)
            VALUES ($1,$2,'TradingStatus','screener-evidence-v0.2.0',1,1,1,$3,$3,$4,'source-a','{}',$5,$6,$7,$8,$9);
            """, connection) { CommandTimeout = 15 };
        command.Parameters.AddWithValue(Guid.NewGuid()); command.Parameters.AddWithValue(EvidenceBindingFixture.Instrument);
        command.Parameters.AddWithValue(EvidenceBindingFixture.Day); command.Parameters.AddWithValue(EvidenceBindingFixture.Known);
        command.Parameters.AddWithValue(ScreenerEvidenceJson.Sha256("{}"));
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)cls ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Integer, Value = (object?)version ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)scope ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = cls is null && version is null && scope is null ? DBNull.Value : EvidenceBindingFixture.Exchange });
        if (accepted)
        {
            Assert.Equal(1, await command.ExecuteNonQueryAsync(ct));
            if (version == 2)
            {
                command.CommandText = "SELECT evidence_id FROM screener_evidence_record;";
                command.Parameters.Clear();
                var record = await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, (Guid)(await command.ExecuteScalarAsync(ct))!, ct);
                Assert.Equal("PERSISTED_EVIDENCE_BINDING_UNSUPPORTED", Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Decode(record!)).Reason);
            }
        }
        else Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(ct))).SqlState);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task SemanticPathRejectsGenericLegacyWhileTradingStatusRoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var legacy = EvidenceBindingFixture.Envelope(EvidenceClaim.StableIdentity);
        Assert.Equal("PERSISTED_EVIDENCE_UNBOUND", (await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceV02Store.AppendAsync(connection, null, legacy, ct))).Reason);
        var masquerading = new ScreenerEvidenceRecord(Guid.NewGuid(), legacy.SubjectId, legacy.Claim, legacy.PolicyId,
            legacy.SchemaVersion, null, 1, null, legacy.AuthorityTier, legacy.EffectiveFrom, legacy.EffectiveTo,
            legacy.EffectiveAt, legacy.PublishedAt, legacy.RetrievedAt, legacy.KnownAt, legacy.SourceId,
            legacy.SourceReference, null, "{\"symbol\":\"TEST\"}", EvidenceClass.ContinuingState, 1,
            ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange);
        Assert.Equal("PERSISTED_EVIDENCE_MALFORMED", (await Assert.ThrowsAsync<EvidenceBindingException>(() =>
            ScreenerEvidenceV02Store.AppendAsync(connection, null, masquerading, ct))).Reason);
        Assert.Null(await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, masquerading.EvidenceId, ct));
        await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, legacy, ct);
        foreach (var claim in Enum.GetValues<EvidenceClaim>())
        {
            var mechanical = EvidenceBindingFixture.Envelope(claim, source: "mechanical-" + claim);
            await ScreenerEvidenceV02Store.AppendMechanicalAsync(connection, null, mechanical, ct);
            var retained = await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, mechanical.EvidenceId, ct);
            Assert.NotNull(retained); Assert.False(retained.IsBound); Assert.Equal(claim, retained.Claim);
            Assert.Equal("PERSISTED_EVIDENCE_UNBOUND", Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Decode(retained)).Reason);
        }
        var bound = EvidenceBindingFixture.Bound(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.TRADING, "regular"));
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, bound, ct);
        Assert.IsType<TradingStatusValue>(ScreenerEvidenceBinding.Decode((await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, bound.EvidenceId, ct))!).Value);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task CancelRequiresRetainedAuthenticTargetAndNeverDeletesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var original = EvidenceBindingFixture.Bound(EvidenceClaim.StableIdentity, new IdentityValue("TEST"));
        var cancel = ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(EvidenceClaim.StableIdentity, revision: 2, supersedes: 1),
            EvidenceClass.ContinuingState, ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange,
            new(EvidenceOperation.CANCEL, EvidenceCompleteness.FULL, null));
        Assert.Equal("PERSISTED_EVIDENCE_INPUT_UNAVAILABLE", (await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceV02Store.AppendAsync(connection, null, cancel, ct))).Reason);
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, original, ct);
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, cancel, ct);
        Assert.NotNull(await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, original.EvidenceId, ct));
        Assert.Equal(2, (await ScreenerEvidenceV02Store.ReadSeriesAsync(connection, null, original.RevisionSeriesId!, ct)).Count);
        var wrong = ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(EvidenceClaim.Currency, revision: 3, supersedes: 1),
            EvidenceClass.ContinuingState, ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange,
            new(EvidenceOperation.CANCEL, EvidenceCompleteness.FULL, null));
        await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceV02Store.AppendAsync(connection, null, wrong, ct));
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ReopeningAndCoverageRequireExactOriginalVisiblePremises()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var suspension = EvidenceBindingFixture.Bound(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice"));
        var reopening = EvidenceBindingFixture.Bound(EvidenceClaim.Reopening,
            new ReopeningValue("reopen", EvidenceStatus.TRADING, EvidenceBindingFixture.Day, suspension.EvidenceId));
        await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceV02Store.AppendAsync(connection, null, reopening, ct));
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, suspension, ct);
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, reopening, ct);
        var action = EvidenceBindingFixture.Bound(EvidenceClaim.CorporateAction,
            new ActionEventValue(EvidenceActionValueKind.EVENT, "split", EvidenceActionType.SPLIT, EvidenceBindingFixture.Day, null, null));
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, action, ct);
        var coverage = EvidenceBindingFixture.Bound(EvidenceClaim.CorporateAction,
            new ActionCoverageValue(EvidenceActionValueKind.COVERAGE, "coverage", EvidenceCoverage.COMPLETE, [action.EvidenceId]),
            EvidenceBindingFixture.Envelope(EvidenceClaim.CorporateAction, revision: 2));
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, coverage, ct);
        var future = EvidenceBindingFixture.Bound(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "future"),
            EvidenceBindingFixture.Envelope(EvidenceClaim.Suspension, revision: 2, known: EvidenceBindingFixture.Known.AddDays(1)));
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, future, ct);
        var early = EvidenceBindingFixture.Bound(EvidenceClaim.Reopening,
            new ReopeningValue("early", EvidenceStatus.TRADING, EvidenceBindingFixture.Day, future.EvidenceId),
            EvidenceBindingFixture.Envelope(EvidenceClaim.Reopening, revision: 2));
        await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceV02Store.AppendAsync(connection, null, early, ct));
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData(EvidenceZeroProof.NONE, EvidenceZeroMeaning.UNPROVED, EvidenceCompletion.COMPLETED, true)]
    [InlineData(EvidenceZeroProof.DOCUMENTED_CONVENTION, EvidenceZeroMeaning.GENUINE_NO_EXECUTION, EvidenceCompletion.COMPLETED, true)]
    [InlineData(EvidenceZeroProof.EXPLICIT_SOURCE_FLAG, EvidenceZeroMeaning.EXPLICIT_GENUINE_FLAG, EvidenceCompletion.COMPLETED, true)]
    [InlineData(EvidenceZeroProof.DOCUMENTED_CONVENTION, EvidenceZeroMeaning.UNPROVED, EvidenceCompletion.COMPLETED, false)]
    [InlineData(EvidenceZeroProof.EXPLICIT_SOURCE_FLAG, EvidenceZeroMeaning.GENUINE_NO_EXECUTION, EvidenceCompletion.COMPLETED, false)]
    [InlineData(EvidenceZeroProof.DOCUMENTED_CONVENTION, EvidenceZeroMeaning.GENUINE_NO_EXECUTION, EvidenceCompletion.UNPROVED, false)]
    public async Task PriceRequiresExactPremisesAndRetainedRawMetadataWithoutRecovery(EvidenceZeroProof proof,
        EvidenceZeroMeaning meaning, EvidenceCompletion completion, bool accepted)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        await Sql(connection, "CREATE TEMP TABLE raw_artifact (LIKE public.raw_artifact INCLUDING ALL);", ct);
        var convention = EvidenceBindingFixture.Bound(EvidenceClaim.SourcePriceConvention, EvidenceBindingFixture.Convention with { ZeroVolumeMeaning = meaning });
        var session = EvidenceBindingFixture.Bound(EvidenceClaim.CompletedSession,
            new CompletedSessionValue("regular", completion, completion == EvidenceCompletion.COMPLETED ? EvidenceBindingFixture.Known.AddHours(-1) : null));
        var bar = EvidenceBindingFixture.Bar with { Volume = proof == EvidenceZeroProof.NONE ? 100 : 0 };
        var value = EvidenceBindingFixture.Price with { ConventionEvidenceId = convention.EvidenceId, CompletedSessionEvidenceId = session.EvidenceId,
            Bar = bar, BarContentHash = ScreenerEvidenceBinding.BarHash(bar), ZeroVolumeProof = proof,
            ZeroVolumeSemantics = proof == EvidenceZeroProof.NONE ? EvidenceZeroVolumeSemantics.NOT_APPLICABLE : EvidenceZeroVolumeSemantics.EXPLICITLY_GENUINE };
        var price = EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, value);
        await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceV02Store.AppendAsync(connection, null, price, ct));
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, convention, ct);
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, session, ct);
        await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceV02Store.AppendAsync(connection, null, price, ct));
        await using (var raw = new NpgsqlCommand("""
            INSERT INTO raw_artifact(raw_artifact_id, ingestion_run_id, source_id, original_uri, request_parameters,
                fetched_at, local_uri, content_sha256, byte_length, parser_version)
            VALUES ($1,$2,'source-a','synthetic','{}',$3,'synthetic',repeat('a',64),100,'synthetic');
            """, connection) { CommandTimeout = 15 })
        {
            raw.Parameters.AddWithValue(EvidenceBindingFixture.Reference); raw.Parameters.AddWithValue(Guid.NewGuid());
            raw.Parameters.AddWithValue(price.RetrievedAt!.Value); await raw.ExecuteNonQueryAsync(ct);
        }
        if (!accepted)
        {
            await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceV02Store.AppendAsync(connection, null, price, ct));
            Assert.Null(await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, price.EvidenceId, ct));
            return;
        }
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, price, ct);
        Assert.Equal(value, ScreenerEvidenceBinding.Decode((await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, price.EvidenceId, ct))!).Value);
        var misplaced = value with { SessionId = "other-session" };
        await Assert.ThrowsAsync<EvidenceBindingException>(() => ScreenerEvidenceV02Store.AppendAsync(connection, null,
            EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, misplaced,
                EvidenceBindingFixture.Envelope(EvidenceClaim.GenuinePriceObservation, revision: 2)), ct));
        var cancel = ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(EvidenceClaim.GenuinePriceObservation, revision: 2, supersedes: 1),
            EvidenceClass.SessionFact, ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange,
            new(EvidenceOperation.CANCEL, EvidenceCompleteness.FULL, null));
        await ScreenerEvidenceV02Store.AppendAsync(connection, null, cancel, ct);
        Assert.NotNull(await ScreenerEvidenceV02Store.ReadExactAsync(connection, null, price.EvidenceId, ct));
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task StoredHashIsAuthenticatedRatherThanReplacedByRecomputedHash()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await Fixture(ct);
        var id = Guid.NewGuid();
        await using var command = new NpgsqlCommand("""
            INSERT INTO screener_evidence_record(evidence_id,subject_id,claim,policy_id,schema_version,
                revision_number,authority_tier,effective_from,known_at,source_id,payload,payload_sha256)
            VALUES ($1,$2,'StableIdentity','screener-evidence-v0.2.0',1,1,1,$3,$4,'source-a','{}',repeat('a',64));
            """, connection) { CommandTimeout = 15 };
        command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(EvidenceBindingFixture.Instrument);
        command.Parameters.AddWithValue(EvidenceBindingFixture.Day); command.Parameters.AddWithValue(EvidenceBindingFixture.Known);
        await command.ExecuteNonQueryAsync(ct);
        Assert.Equal("PERSISTED_EVIDENCE_MALFORMED", (await Assert.ThrowsAsync<EvidenceBindingException>(() =>
            ScreenerEvidenceV02Store.ReadExactAsync(connection, null, id, ct))).Reason);
    }
}
