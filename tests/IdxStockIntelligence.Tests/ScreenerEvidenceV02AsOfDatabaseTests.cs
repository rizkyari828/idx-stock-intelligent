using System.Data;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceV02AsOfDatabaseTests
{
    public static bool DatabaseConfigured => ScreenerEvidenceV02DatabaseTests.DatabaseConfigured;
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Task<NpgsqlConnection> Fixture() => ScreenerEvidenceV02DatabaseTests.Fixture(Token);
    private static Task<EvidenceWriteResult> Append(NpgsqlConnection c, ScreenerEvidenceRecord row) => ScreenerEvidenceV02Store.AppendAsync(c, null, row, Token);
    private static Task<ScreenerEvidenceAsOfResult> Read(NpgsqlConnection c, EvidenceClaim claim,
        DateTimeOffset? cutoff = null, DateOnly? date = null) => ScreenerEvidenceAsOfReader.ReadAsync(c, null, AsOfFixture.Request(claim, cutoff, date), Token);
    private static async Task Sql(NpgsqlConnection c, string sql)
    {
        await using var command = new NpgsqlCommand(sql, c) { CommandTimeout = 15 };
        await command.ExecuteNonQueryAsync(Token);
    }
    private static async Task InsertPhysical(NpgsqlConnection c, ScreenerEvidenceRecord row, string? hash = null)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO screener_evidence_record(evidence_id,subject_id,claim,policy_id,schema_version,
                revision_series_id,revision_number,supersedes_revision_number,authority_tier,effective_from,effective_to,
                published_at,retrieved_at,known_at,source_id,source_reference,raw_artifact_id,payload,payload_sha256,
                evidence_class,payload_schema_version,scope_kind,scope_exchange_id)
            VALUES ($1,$2,$3,$4,1,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22);
            """, c) { CommandTimeout = 15 };
        command.Parameters.AddWithValue(row.EvidenceId); command.Parameters.AddWithValue(row.SubjectId);
        command.Parameters.AddWithValue(row.Claim.ToString()); command.Parameters.AddWithValue(row.PolicyId);
        void Add(NpgsqlDbType type, object? value) => command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = type, Value = value ?? DBNull.Value });
        Add(NpgsqlDbType.Text, row.RevisionSeriesId); Add(NpgsqlDbType.Bigint, row.RevisionNumber); Add(NpgsqlDbType.Bigint, row.SupersedesRevisionNumber);
        Add(NpgsqlDbType.Smallint, (short)((int)row.AuthorityTier + 1)); Add(NpgsqlDbType.Date, row.EffectiveFrom); Add(NpgsqlDbType.Date, row.EffectiveTo);
        Add(NpgsqlDbType.TimestampTz, row.PublishedAt); Add(NpgsqlDbType.TimestampTz, row.RetrievedAt); Add(NpgsqlDbType.TimestampTz, row.KnownAt);
        Add(NpgsqlDbType.Text, row.SourceId); Add(NpgsqlDbType.Text, row.SourceReference); Add(NpgsqlDbType.Uuid, row.RawArtifactId);
        Add(NpgsqlDbType.Text, row.Payload); Add(NpgsqlDbType.Text, hash ?? row.PayloadSha256);
        Add(NpgsqlDbType.Text, row.EvidenceClass is { } cls ? ScreenerEvidenceBinding.ClassToken(cls) : null);
        Add(NpgsqlDbType.Integer, row.PayloadSchemaVersion); Add(NpgsqlDbType.Text, row.ScopeKind?.ToString()); Add(NpgsqlDbType.Uuid, row.ScopeExchangeId);
        await command.ExecuteNonQueryAsync(Token);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task BoundAndUnboundRowsStayDistinctAndReadsWriteNothing()
    {
        await using var c = await Fixture();
        Assert.Equal(EvidenceQuality.Unknown, (await Read(c, EvidenceClaim.StableIdentity)).Quality);
        var legacy = EvidenceBindingFixture.Envelope(EvidenceClaim.StableIdentity);
        await ScreenerEvidenceV02Store.AppendMechanicalAsync(c, null, legacy, Token);
        var unavailable = await Read(c, legacy.Claim);
        Assert.Equal(EvidenceQuality.Unknown, unavailable.Quality); Assert.Contains("PERSISTED_EVIDENCE_UNBOUND", unavailable.Reasons);
        var bound = AsOfFixture.Row(legacy.Claim, new IdentityValue("BOUND")); await Append(c, bound);
        var result = await Read(c, bound.Claim);
        Assert.Equal(EvidenceQuality.Verified, result.Quality); Assert.Equal(bound.EvidenceId, Assert.Single(result.Facts).Selected!.EvidenceId);
        Assert.Equal("BOUND", Assert.IsType<IdentityValue>(result.Facts[0].Value).SourceCode);
        await using var count = new NpgsqlCommand("SELECT count(*)::int FROM screener_evidence_record;", c);
        Assert.Equal(2, (int)(await count.ExecuteScalarAsync(Token))!);
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("future-known")]
    [InlineData("future-retrieval")]
    [InlineData("historical-learned-later")]
    [InlineData("future-publication")]
    [InlineData("recorded-later")]
    public async Task RetainedChronologyControlsVisibility(string scenario)
    {
        await using var c = await Fixture();
        var old = AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("ORIGINAL")); await Append(c, old);
        var future = AsOfFixture.Row(old.Claim, new IdentityValue("LATER"), revision: 2,
            known: scenario == "future-publication" ? old.KnownAt : old.KnownAt.AddDays(2),
            from: scenario == "historical-learned-later" ? old.EffectiveFrom.AddYears(-1) : null);
        if (scenario == "future-publication")
            future = new(future.EvidenceId, future.SubjectId, future.Claim, future.PolicyId, 1, future.RevisionSeriesId,
                2, 1, future.AuthorityTier, future.EffectiveFrom, future.EffectiveTo, null, old.KnownAt.AddDays(2),
                future.RetrievedAt, future.KnownAt, future.SourceId, future.SourceReference, null, future.Payload,
                future.EvidenceClass, 1, future.ScopeKind, future.ScopeExchangeId);
        if (scenario == "future-retrieval")
            future = new(future.EvidenceId, future.SubjectId, future.Claim, future.PolicyId, 1, future.RevisionSeriesId,
                2, 1, future.AuthorityTier, future.EffectiveFrom, future.EffectiveTo, null, future.PublishedAt,
                old.KnownAt.AddDays(1), future.KnownAt, future.SourceId, future.SourceReference, null, future.Payload,
                future.EvidenceClass, 1, future.ScopeKind, future.ScopeExchangeId);
        if (scenario != "recorded-later") await Append(c, future);
        var result = await Read(c, old.Claim);
        Assert.Equal(old.EvidenceId, Assert.Single(result.Facts).Selected!.EvidenceId);
        Assert.True(result.Facts[0].Selected!.Chronology.RecordedAt > result.Request.Cutoff);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task RevisionsAndCancellationPreserveEarlierReplayAndIndependentSeries()
    {
        await using var c = await Fixture();
        var rev10 = AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("TEN"), revision: 10);
        var rev11 = AsOfFixture.Row(rev10.Claim, new IdentityValue("ELEVEN"), revision: 11, known: rev10.KnownAt.AddDays(1));
        await Append(c, rev10); await Append(c, rev11);
        Assert.Equal(rev10.EvidenceId, Assert.Single((await Read(c, rev10.Claim)).Facts).Selected!.EvidenceId);
        Assert.Equal(rev11.EvidenceId, Assert.Single((await Read(c, rev10.Claim, rev11.KnownAt)).Facts).Selected!.EvidenceId);
        Assert.NotNull(await ScreenerEvidenceV02Store.ReadExactAsync(c, null, rev10.EvidenceId, Token));
        var cancel = AsOfFixture.Cancel(rev11, rev11.KnownAt.AddDays(1)); await Append(c, cancel);
        var unrelated = AsOfFixture.Row(rev10.Claim, new IdentityValue("INDEPENDENT"), series: "independent"); await Append(c, unrelated);
        Assert.Equal(EvidenceQuality.Conflicting, (await Read(c, rev10.Claim, rev11.KnownAt)).Quality);
        // Cancellation removes exactly rev11; rev10 must not be resurrected as a fallback for the cancelled lineage.
        var after = await Read(c, rev10.Claim, cancel.KnownAt);
        Assert.Equal(unrelated.EvidenceId, Assert.Single(after.Facts).Selected!.EvidenceId);
        Assert.Contains(ScreenerEvidenceReasons.EvidenceCancelled, after.Reasons);
        Assert.NotNull(await ScreenerEvidenceV02Store.ReadExactAsync(c, null, rev11.EvidenceId, Token));
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData(SourceAuthorityTier.T2AdmittedReference, EvidenceBoard.DEVELOPMENT, EvidenceQuality.Verified)]
    [InlineData(SourceAuthorityTier.T1Governing, EvidenceBoard.DEVELOPMENT, EvidenceQuality.Conflicting)]
    [InlineData(SourceAuthorityTier.T1Governing, EvidenceBoard.MAIN, EvidenceQuality.Verified)]
    public async Task AuthorityAndTypedConflictIgnoreRetrievalRecency(SourceAuthorityTier tier, EvidenceBoard board, EvidenceQuality expected)
    {
        await using var c = await Fixture();
        var main = AsOfFixture.Row(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.MAIN), series: "main");
        var other = AsOfFixture.Row(main.Claim, new BoardValue(board), series: "other", tier: tier, known: main.KnownAt.AddDays(1));
        await Append(c, main); await Append(c, other);
        var result = await Read(c, main.Claim, other.KnownAt);
        Assert.Equal(expected, result.Quality);
        if (expected == EvidenceQuality.Conflicting) Assert.Null(Assert.Single(result.Facts).Value);
        else Assert.Equal(EvidenceBoard.MAIN, Assert.IsType<BoardValue>(Assert.Single(result.Facts).Value).BoardCode);
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData(EvidenceClass.PointObservation, -2, EvidenceQuality.Stale)]
    [InlineData(EvidenceClass.ContinuingState, -2, EvidenceQuality.Stale)]
    [InlineData(EvidenceClass.PointObservation, 1, EvidenceQuality.Unknown)]
    [InlineData(EvidenceClass.ContinuingState, 1, EvidenceQuality.Unknown)]
    [InlineData(EvidenceClass.PointObservation, 0, EvidenceQuality.Verified)]
    public async Task EffectiveScopeNeverCarriesPointsForward(EvidenceClass cls, int offset, EvidenceQuality expected)
    {
        await using var c = await Fixture();
        var row = AsOfFixture.Row(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.MAIN), cls: cls,
            from: EvidenceBindingFixture.Day.AddDays(offset), to: EvidenceBindingFixture.Day.AddDays(offset)); await Append(c, row);
        var result = await Read(c, row.Claim); Assert.Equal(expected, result.Quality);
        if (expected != EvidenceQuality.Verified) Assert.DoesNotContain(result.Facts, f => f.Value is not null);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ExchangeScopeRequiresRetainedIdentityAndInstrumentSpecificityWins()
    {
        await using var c = await Fixture();
        var market = AsOfFixture.Row(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.SUSPENDED, "regular"), kind: ScreenerScopeKind.EXCHANGE);
        await Append(c, market);
        Assert.Equal(EvidenceQuality.Unknown, (await Read(c, market.Claim)).Quality);
        var identity = AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("LOCAL")); await Append(c, identity);
        Assert.Equal(market.EvidenceId, Assert.Single((await Read(c, market.Claim)).Facts).Selected!.EvidenceId);
        var instrument = AsOfFixture.Row(market.Claim, new TradingStatusValue(EvidenceStatus.TRADING, "regular"), series: "specific"); await Append(c, instrument);
        Assert.Equal(instrument.EvidenceId, Assert.Single((await Read(c, market.Claim)).Facts).Selected!.EvidenceId);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ContinuingSuspensionAndLaterReopeningUseOriginalKnowledge()
    {
        await using var c = await Fixture();
        var suspension = AsOfFixture.Row(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice")); await Append(c, suspension);
        var date = suspension.EffectiveFrom.AddDays(1);
        var reopening = AsOfFixture.Row(EvidenceClaim.Reopening, new ReopeningValue("reopen", EvidenceStatus.TRADING, date, suspension.EvidenceId),
            from: date, known: suspension.KnownAt.AddDays(2)); await Append(c, reopening);
        async Task<TradingStatusResult> At(DateTimeOffset cutoff)
        {
            await using var t = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, Token);
            await using (var ro = new NpgsqlCommand("SET TRANSACTION READ ONLY", c, t)) await ro.ExecuteNonQueryAsync(Token);
            var s = await ScreenerEvidenceAsOfReader.ReadAsync(c, t, AsOfFixture.Request(suspension.Claim, cutoff, date), Token);
            var r = await ScreenerEvidenceAsOfReader.ReadAsync(c, t, AsOfFixture.Request(reopening.Claim, cutoff, date), Token);
            var status = await ScreenerEvidenceAsOfReader.ReadAsync(c, t, AsOfFixture.Request(EvidenceClaim.TradingStatus, cutoff, date), Token);
            Assert.Equal(EvidenceQuality.Verified, s.Quality);
            await t.CommitAsync(Token);
            return ScreenerEvidenceAsOf.TradingStatus(s, r, status);
        }
        Assert.Equal(TradingStatus.Suspended, (await At(suspension.KnownAt)).Status);
        Assert.Equal(TradingStatus.Trading, (await At(reopening.KnownAt)).Status);
        Assert.Equal(TradingStatus.Suspended, (await At(suspension.KnownAt)).Status);
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData(EvidenceClaim.ScheduledSession)]
    [InlineData(EvidenceClaim.CompletedSession)]
    public async Task SessionsDoNotEstablishTradingStatus(EvidenceClaim claim)
    {
        await using var c = await Fixture();
        ScreenerEvidenceValue value = claim == EvidenceClaim.ScheduledSession ? new ScheduledSessionValue("regular", EvidenceSchedule.OPEN)
            : new CompletedSessionValue("regular", EvidenceCompletion.COMPLETED, EvidenceBindingFixture.Known.AddHours(-1));
        var row = AsOfFixture.Row(claim, value); await Append(c, row);
        var own = await ScreenerEvidenceAsOfReader.ReadAsync(c, null, AsOfFixture.Request(claim, scope: ScreenerScopeKind.EXCHANGE), Token);
        Assert.Equal(EvidenceQuality.Verified, own.Quality);
        Assert.Equal(EvidenceQuality.Unknown, (await Read(c, EvidenceClaim.TradingStatus)).Quality);
        if (claim == EvidenceClaim.ScheduledSession)
            Assert.Equal(EvidenceQuality.Unknown, (await ScreenerEvidenceAsOfReader.ReadAsync(c, null,
                AsOfFixture.Request(EvidenceClaim.CompletedSession, scope: ScreenerScopeKind.EXCHANGE), Token)).Quality);
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData(EvidenceClaim.StableIdentity)]
    [InlineData(EvidenceClaim.CompletedSession)]
    [InlineData(EvidenceClaim.CorporateAction)]
    public async Task IncompleteAndUnprovedFactsRemainPartial(EvidenceClaim claim)
    {
        await using var c = await Fixture();
        ScreenerEvidenceValue value = claim switch
        {
            EvidenceClaim.StableIdentity => new IdentityValue("PARTIAL"),
            EvidenceClaim.CompletedSession => new CompletedSessionValue("regular", EvidenceCompletion.UNPROVED, null),
            _ => new ActionCoverageValue(EvidenceActionValueKind.COVERAGE, "partial", EvidenceCoverage.PARTIAL, [])
        };
        var row = AsOfFixture.Row(claim, value, completeness: claim == EvidenceClaim.StableIdentity ? EvidenceCompleteness.PARTIAL : EvidenceCompleteness.FULL);
        await Append(c, row);
        var result = await ScreenerEvidenceAsOfReader.ReadAsync(c, null, AsOfFixture.Request(claim,
            scope: EvidenceBindingFixture.Scope(claim)), Token);
        Assert.Equal(EvidenceQuality.Partial, result.Quality); Assert.Equal(EvidenceQuality.Partial, Assert.Single(result.Facts).Quality);
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("lower-later", TradingStatus.Suspended, EvidenceQuality.Verified)]
    [InlineData("lower-conflict", TradingStatus.Suspended, EvidenceQuality.Verified)]
    [InlineData("same-tier-conflict", TradingStatus.Unknown, EvidenceQuality.Conflicting)]
    [InlineData("expired-higher", TradingStatus.Trading, EvidenceQuality.Verified)]
    public async Task ApplicableStatusAuthorityPrecedesRecencyAndConflicts(string scenario, TradingStatus expected, EvidenceQuality quality)
    {
        await using var c = await Fixture();
        var expired = scenario == "expired-higher";
        if (scenario != "same-tier-conflict")
            await Append(c, AsOfFixture.Row(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "authoritative"),
                from: expired ? EvidenceBindingFixture.Day.AddDays(-1) : null, to: expired ? EvidenceBindingFixture.Day.AddDays(-1) : null));
        var tier = scenario == "same-tier-conflict" ? SourceAuthorityTier.T1Governing : SourceAuthorityTier.T2AdmittedReference;
        var trade = AsOfFixture.Row(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.TRADING, "regular"),
            tier: tier, known: EvidenceBindingFixture.Known.AddDays(1)); await Append(c, trade);
        if (scenario is "same-tier-conflict" or "lower-conflict")
            await Append(c, AsOfFixture.Row(trade.Claim, new TradingStatusValue(EvidenceStatus.SUSPENDED, "regular"),
                tier: tier, known: trade.KnownAt, series: "independent"));
        await using var t = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, Token);
        await using (var ro = new NpgsqlCommand("SET TRANSACTION READ ONLY", c, t)) await ro.ExecuteNonQueryAsync(Token);
        var s = await ScreenerEvidenceAsOfReader.ReadAsync(c, t, AsOfFixture.Request(EvidenceClaim.Suspension, trade.KnownAt), Token);
        var r = await ScreenerEvidenceAsOfReader.ReadAsync(c, t, AsOfFixture.Request(EvidenceClaim.Reopening, trade.KnownAt), Token);
        var status = await ScreenerEvidenceAsOfReader.ReadAsync(c, t, AsOfFixture.Request(trade.Claim, trade.KnownAt), Token);
        var result = ScreenerEvidenceAsOf.TradingStatus(s, r, status);
        Assert.Equal(expected, result.Status); Assert.Equal(quality, result.Quality);
        await t.CommitAsync(Token);
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("malformed", "PERSISTED_EVIDENCE_MALFORMED")]
    [InlineData("hash", "PERSISTED_EVIDENCE_MALFORMED")]
    [InlineData("version", "PERSISTED_EVIDENCE_BINDING_UNSUPPORTED")]
    public async Task CorruptBoundEvidenceFailsWithoutFallback(string defect, string reason)
    {
        await using var c = await Fixture();
        var good = AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("GOOD")); await Append(c, good);
        var next = AsOfFixture.Row(good.Claim, new IdentityValue("BAD"), revision: 2);
        await InsertPhysical(c, defect == "malformed" ? AsOfFixture.Physical(next, "{}", 1)
            : defect == "version" ? AsOfFixture.Physical(next, "{}", 2) : next, defect == "hash" ? new string('a', 64) : null);
        var result = await Read(c, good.Claim); Assert.Equal(reason, result.FailureReason); Assert.Empty(result.Facts);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task DerivedLegacyComparabilityCannotEstablishAnyBaseFact()
    {
        await using var c = await Fixture();
        var legacy = EvidenceBindingFixture.Envelope(EvidenceClaim.PriceComparability);
        await ScreenerEvidenceV02Store.AppendMechanicalAsync(c, null, legacy, Token);
        var result = await Read(c, legacy.Claim);
        Assert.Equal(EvidenceQuality.Unknown, result.Quality); Assert.Empty(result.Facts); Assert.Contains("PERSISTED_EVIDENCE_UNBOUND", result.Reasons);
        await InsertPhysical(c, new(Guid.NewGuid(), legacy.SubjectId, legacy.Claim, legacy.PolicyId, 1, null, 1, null,
            legacy.AuthorityTier, legacy.EffectiveFrom, legacy.EffectiveTo, null, legacy.PublishedAt, legacy.RetrievedAt,
            legacy.KnownAt, legacy.SourceId, null, null, "{}", EvidenceClass.SessionFact, 1,
            ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange));
        Assert.Equal("PERSISTED_EVIDENCE_DERIVED_CLAIM", (await Read(c, legacy.Claim)).FailureReason);
    }

    [Theory(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData(EvidenceMarker.NO, EvidenceQuality.Verified, EvidenceCompleteness.FULL)]
    [InlineData(EvidenceMarker.YES, EvidenceQuality.Unknown, EvidenceCompleteness.FULL)]
    [InlineData(EvidenceMarker.UNPROVED, EvidenceQuality.Unknown, EvidenceCompleteness.FULL)]
    [InlineData(EvidenceMarker.NO, EvidenceQuality.Partial, EvidenceCompleteness.PARTIAL)]
    public async Task PriceReferencesStayExactAndDoNotProveTrading(EvidenceMarker marker, EvidenceQuality expected, EvidenceCompleteness completion)
    {
        await using var c = await Fixture();
        await Sql(c, "CREATE TEMP TABLE raw_artifact (LIKE public.raw_artifact INCLUDING ALL);");
        var convention = AsOfFixture.Row(EvidenceClaim.SourcePriceConvention, EvidenceBindingFixture.Convention);
        var session = AsOfFixture.Row(EvidenceClaim.CompletedSession, new CompletedSessionValue("regular", EvidenceCompletion.COMPLETED, EvidenceBindingFixture.Known.AddHours(-1)),
            completeness: completion);
        await Append(c, convention); await Append(c, session);
        await using (var artifact = new NpgsqlCommand("""
            INSERT INTO raw_artifact(raw_artifact_id,ingestion_run_id,source_id,original_uri,request_parameters,fetched_at,
                local_uri,content_sha256,byte_length,parser_version)
            VALUES ($1,$2,'source-a','synthetic','{}',$3,'synthetic',repeat('a',64),100,'synthetic');
            """, c))
        {
            artifact.Parameters.AddWithValue(EvidenceBindingFixture.Reference); artifact.Parameters.AddWithValue(Guid.NewGuid());
            artifact.Parameters.AddWithValue(EvidenceBindingFixture.Known.AddHours(-1)); await artifact.ExecuteNonQueryAsync(Token);
        }
        var value = EvidenceBindingFixture.Price with { ConventionEvidenceId = convention.EvidenceId, CompletedSessionEvidenceId = session.EvidenceId, Placeholder = marker };
        var price = AsOfFixture.Row(EvidenceClaim.GenuinePriceObservation, value); await Append(c, price);
        var result = await Read(c, price.Claim); Assert.Equal(expected, result.Quality);
        Assert.Contains(result.References, p => p.EvidenceId == convention.EvidenceId);
        Assert.Contains(result.References, p => p.EvidenceId == session.EvidenceId);
        Assert.Equal(EvidenceQuality.Unknown, (await Read(c, EvidenceClaim.TradingStatus)).Quality);
        var correction = AsOfFixture.Row(EvidenceClaim.SourcePriceConvention, EvidenceBindingFixture.Convention with { PriceKind = EvidencePriceKind.UNPROVED },
            revision: 2, known: EvidenceBindingFixture.Known.AddDays(1)); await Append(c, correction);
        Assert.Equal(expected, (await Read(c, price.Claim, correction.KnownAt)).Quality);
        var missing = AsOfFixture.Row(price.Claim, value with { CompletedSessionEvidenceId = Guid.NewGuid() }, revision: 2);
        await InsertPhysical(c, missing);
        Assert.Equal(ScreenerEvidenceAsOf.InputUnavailable, (await Read(c, price.Claim)).FailureReason);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task CandidateLimitIsAnExplicitFailure()
    {
        await using var c = await Fixture();
        for (var i = 0; i <= ScreenerEvidenceAsOf.MaximumRecords; i++)
            await InsertPhysical(c, AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("LOCAL"), series: "bounded-" + i));
        var result = await Read(c, EvidenceClaim.StableIdentity);
        Assert.Equal(ScreenerEvidenceAsOf.BoundExceeded, result.FailureReason); Assert.Empty(result.Facts);
    }

    [Fact(Skip = "Owned disposable PostgreSQL only", SkipUnless = nameof(DatabaseConfigured))]
    public async Task RepeatableReadDoesNotMixConcurrentAppendsOrBlockTheWriter()
    {
        await using var c = ScreenerEvidenceV02DatabaseTests.OwnedConnection(); await c.OpenAsync(Token);
        await using var writer = ScreenerEvidenceV02DatabaseTests.OwnedConnection(); await writer.OpenAsync(Token);
        var schema = "asof_test_" + Guid.NewGuid().ToString("N");
        await Sql(c, "CREATE SCHEMA " + schema + "; CREATE TABLE " + schema + ".screener_evidence_record (LIKE public.screener_evidence_record INCLUDING ALL); SET search_path TO " + schema + ", public;");
        try
        {
            await Sql(writer, "SET search_path TO " + schema + ", public;");
            var first = AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("FIRST")); await Append(writer, first);
            var second = AsOfFixture.Row(first.Claim, new IdentityValue("SECOND"), revision: 2, known: first.KnownAt.AddHours(1));
            var request = AsOfFixture.Request(first.Claim, second.KnownAt);
            await using (var t = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, Token))
            {
                await using (var ro = new NpgsqlCommand("SET TRANSACTION READ ONLY", c, t)) await ro.ExecuteNonQueryAsync(Token);
                Assert.Equal(first.EvidenceId, Assert.Single((await ScreenerEvidenceAsOfReader.ReadAsync(c, t, request, Token)).Facts).Selected!.EvidenceId);
                await Append(writer, second);
                Assert.Equal(first.EvidenceId, Assert.Single((await ScreenerEvidenceAsOfReader.ReadAsync(c, t, request, Token)).Facts).Selected!.EvidenceId);
                await t.CommitAsync(Token);
            }
            Assert.Equal(second.EvidenceId, Assert.Single((await ScreenerEvidenceAsOfReader.ReadAsync(c, null, request, Token)).Facts).Selected!.EvidenceId);
        }
        finally { await Sql(c, "SET search_path TO public; DROP SCHEMA " + schema + " CASCADE;"); }
    }
}
