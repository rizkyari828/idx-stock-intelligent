using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace IdxStockIntelligence.Tests;

// Standard discovery; test_screener_evidence.py owns/migrates/drops the loopback database.
// Each test owns a separate schema with real LIKE types/checks/indexes, without production triggers.
public sealed class ResearchDatabaseTests
{
    public static bool DatabaseConfigured => ScreenerDatabaseTests.DatabaseConfigured;
    private static readonly DateTimeOffset Captured = new(2026, 9, 30, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Known = Captured.AddDays(10);
    private static readonly DateTimeOffset Now = Captured.AddDays(15);
    private static Guid Id(int n, char prefix = '1') => Guid.Parse(prefix + "0000000-0000-4000-8000-" + n.ToString("D12", CultureInfo.InvariantCulture));
    private static ResearchQuery Query() => new(new(2026, 9, 30), new(2026, 10, 15), Now);
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    private sealed class Fixture(NpgsqlDataSource source, string connection, string schema) : IAsyncDisposable
    {
        public NpgsqlDataSource Source { get; } = source;
        public ResearchService Service { get; } = new(source, new FixedClock());
        public static async Task<Fixture> Create(CancellationToken ct)
        {
            var config = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDX_SCREENER_TEST_CONNECTION"));
            if (config.Host != "127.0.0.1" || config.Database is null || !config.Database.StartsWith("idx_screener_test_", StringComparison.Ordinal)
                || !config.Database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                throw new InvalidOperationException("Owned loopback disposable database required.");
            var schema = "research_" + Guid.NewGuid().ToString("N");
            var original = config.ConnectionString;
            await using (var c = new NpgsqlConnection(original))
            {
                await c.OpenAsync(ct);
                await using var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", c) { CommandTimeout = 15 };
                await create.ExecuteNonQueryAsync(ct);
            }
            config.SearchPath = schema; config.ApplicationName = schema;
            var fixture = new Fixture(NpgsqlDataSource.Create(config.ConnectionString), original, schema);
            try
            {
                await fixture.Sql("""
                    CREATE TABLE decision_snapshot_run (LIKE public.decision_snapshot_run INCLUDING ALL);
                    CREATE TABLE decision_snapshot_row (LIKE public.decision_snapshot_row INCLUDING ALL);
                    CREATE TABLE decision_snapshot_outcome (LIKE public.decision_snapshot_outcome INCLUDING ALL);
                    """, ct);
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public async Task Sql(string sql, CancellationToken ct, params object[] values)
        {
            await using var c = await Source.OpenConnectionAsync(ct);
            await using var command = new NpgsqlCommand(sql, c) { CommandTimeout = 15 };
            foreach (var value in values)
                if (value is DBNull) command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = value });
                else command.Parameters.AddWithValue(value);
            await command.ExecuteNonQueryAsync(ct);
        }
        public async Task<string> Scalar(string sql, CancellationToken ct, params object[] values)
        {
            await using var c = await Source.OpenConnectionAsync(ct);
            await using var command = new NpgsqlCommand(sql, c) { CommandTimeout = 15 };
            foreach (var value in values) command.Parameters.AddWithValue(value);
            return Convert.ToString(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture)!;
        }
        public Task Seed(int runs, int rows, CancellationToken ct, int start = 1) => Sql("""
            WITH captured AS (INSERT INTO decision_snapshot_run(run_id,request_id,schema_version,capture_kind,captured_at,knowledge_cutoff,
                recorded_at,through,history_anchor,target_session,policy_id,universe,universe_snapshot_id,portfolio_id,
                input_hash,selected_digest,status,row_count,request_intent,result,evidence_manifest)
            SELECT ('20000000-0000-4000-8000-'||lpad(r::text,12,'0'))::uuid,
                ('30000000-0000-4000-8000-'||lpad(r::text,12,'0'))::uuid,1,'PROSPECTIVE_CAPTURE',$3,$3,$3,
                '2026-09-30','2026-08-24','2026-09-30','screener-v0.1.0','PILOT','retained-universe',NULL,
                repeat('a',64),repeat('b',64),'COMPLETE',$2,
                '{"through":null,"portfolioId":null}'::jsonb,
                '{"reasons":[],"summary":{},"rankedCandidateIds":[],"allViewIds":[],"shortlistIds":[],"heldIds":[],
                  "marketContext":{"trend":"UNKNOWN","volatility":"UNKNOWN","marketDate":"2026-09-30","reasons":["retained-context"]}}'::jsonb,
                jsonb_build_object('schemaVersion',1,'evaluatedInstrumentIds',coalesce((SELECT jsonb_agg(
                    ('10000000-0000-4000-8000-'||lpad(i::text,12,'0'))::uuid) FROM generate_series(1,$2) i),'[]'::jsonb),
                    'request',jsonb_build_object('through','2026-09-30','cutoff',$3,'historyAnchor','2026-08-24'))
            FROM generate_series($4,$4+$1-1) r RETURNING run_id)
            INSERT INTO decision_snapshot_row(run_id,instrument_id,symbol,configured,held,eligibility,setup,setup_evaluated,market_date,close,result)
            SELECT captured.run_id,
                ('10000000-0000-4000-8000-'||lpad(i::text,12,'0'))::uuid,'OLD',true,false,'ELIGIBLE','NONE',true,
                '2026-09-30',100,'{"eligibilityReasons":[],"setupReasons":[],"episode":null,"fieldStates":{},"provenance":{}}'::jsonb
            FROM captured CROSS JOIN generate_series(1,$2) i;
            """, ct, runs, rows, Captured, start);
        public Task Outcome(int instrument, string? value, string state, CancellationToken ct, int horizon = 5, int run = 1)
            => Sql("""
                INSERT INTO decision_snapshot_outcome(run_id,instrument_id,horizon_sessions,schema_version,outcome_policy_id,
                    anchor_market_date,anchor_close,horizon_market_date,horizon_close,price_return_pct,outcome_state,reason,
                    outcome_known_at,recorded_at,evidence_manifest)
                SELECT s.run_id,s.instrument_id,$3,1,'outcome-v0.1.0',s.market_date,s.close,'2026-10-08',
                    CASE WHEN $4='AVAILABLE' THEN 110 ELSE NULL END,$5::numeric,$4,
                    CASE $4 WHEN 'AVAILABLE' THEN NULL WHEN 'ANCHOR_UNAVAILABLE' THEN 'CAPTURED_PRICE_BASIS_UNVERIFIED'
                        WHEN 'DATA_UNAVAILABLE' THEN 'SUSPENDED_AT_HORIZON' ELSE 'PRICE_CONVENTION_UNSUPPORTED' END,$6,$6,
                    '{"schemaVersion":1,"outcomePolicyId":"outcome-v0.1.0","captureSchemaVersion":1,"capturePolicyId":"screener-v0.1.0",
                      "captureInputHash":"","captureSelectedDigest":"","instrumentId":"","horizonSessions":5,"evaluationCutoff":"",
                      "anchor":null,"endpoint":null,"listing":null,"calendar":[],"instruments":[],"instrumentSessions":[],"archives":[],"terminalCondition":null}'::jsonb
                FROM decision_snapshot_row s WHERE s.run_id=$1 AND s.instrument_id=$2;
                """, ct, Id(run, '2'), Id(instrument), horizon, state, value ?? (object)DBNull.Value, Known);
        public async Task DropChecks(string table, CancellationToken ct)
        {
            Assert.True(table is "decision_snapshot_run" or "decision_snapshot_row" or "decision_snapshot_outcome");
            await Sql($"""
                DO $owned$ DECLARE c record; BEGIN
                FOR c IN SELECT conname FROM pg_constraint WHERE conrelid='{table}'::regclass AND contype='c'
                LOOP EXECUTE format('ALTER TABLE {table} DROP CONSTRAINT %I',c.conname); END LOOP;
                END $owned$;
                """, ct);
        }
        public async Task<string> Fingerprint(CancellationToken ct)
        {
            var hashes = new List<string>();
            foreach (var table in new[] { "decision_snapshot_run", "decision_snapshot_row", "decision_snapshot_outcome" })
                hashes.Add(await Scalar($"SELECT count(*)||':'||md5(coalesce(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text),'')) FROM {table} t", ct));
            return string.Join('/', hashes);
        }
        public async ValueTask DisposeAsync()
        {
            await Source.DisposeAsync();
            await using var c = new NpgsqlConnection(connection); await c.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", c) { CommandTimeout = 15 };
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task EmptyAndBlockedUnresolvedPopulationsRemainValid()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct);
        var empty = await f.Service.ReadAsync(Query(), ct);
        Assert.Equal(0, empty.Summary.Counts.N); Assert.Null(empty.Summary.Metrics.AvailableCoverage);
        await f.Seed(1, 10, ct);
        await f.Sql("UPDATE decision_snapshot_run SET status='BLOCKED'; UPDATE decision_snapshot_row SET eligibility='DATA_BLOCKED',setup_evaluated=false", ct);
        var before = await f.Fingerprint(ct); var result = await f.Service.ReadAsync(Query(), ct);
        Assert.Equal(new ResearchCounts(10, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0), result.Summary.Counts);
        Assert.Equal(new ResearchMetrics(null, null, null, null, null, 0), result.Summary.Metrics);
        Assert.All(result.Observations, r => { Assert.Null(r.Outcome); Assert.Equal("NO_COMMITTED_OUTCOME_AS_OF_CUTOFF", r.ResearchReason); });
        Assert.Equal(before, await f.Fingerprint(ct));
    }

    [Fact(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task StoredArithmeticFixtureAndLeftJoinPreserveAllObservations()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 8, ct);
        string[] values = ["-10", "0", "5", "15"];
        for (var i = 0; i < values.Length; i++) await f.Outcome(i + 1, values[i], "AVAILABLE", ct);
        await f.Outcome(5, null, "ANCHOR_UNAVAILABLE", ct); await f.Outcome(6, null, "DATA_UNAVAILABLE", ct);
        await f.Outcome(7, null, "BASIS_UNCERTAIN", ct);
        var before = await f.Fingerprint(ct); var result = await f.Service.ReadAsync(Query(), ct);
        Assert.Equal(new ResearchCounts(8, 4, 3, 7, 1, 1, 1, 1, 2, 1, 1), result.Summary.Counts);
        Assert.Equal(new ResearchMetrics(2.5m, 2.5m, -10m, 15m, 50m, 50m), result.Summary.Metrics);
        Assert.Equal(8, result.Observations.Count); Assert.Equal(before, await f.Fingerprint(ct));
    }

    [Theory(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("equal", 1)][InlineData("known-after", 0)][InlineData("recorded-after", 0)]
    public async Task BothOutcomeClocksAreInclusiveAndInsideLeftJoin(string boundary, int a)
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 2, ct);
        await f.Outcome(1, "10", "AVAILABLE", ct); await f.DropChecks("decision_snapshot_outcome", ct);
        await f.Sql("UPDATE decision_snapshot_outcome SET outcome_known_at=$1,recorded_at=$2", ct,
            boundary == "known-after" ? Known.AddMilliseconds(1) : Known,
            boundary == "recorded-after" ? Known.AddMilliseconds(1) : Known);
        var result = await f.Service.ReadAsync(Query() with { Cutoff = Known }, ct);
        Assert.Equal((2, a, 2 - a), (result.Summary.Counts.N, result.Summary.Counts.A, result.Summary.Counts.U));
        if (a == 0) Assert.All(result.Observations, r => Assert.Null(r.Outcome));
    }

    [Theory(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("equal", 1)][InlineData("captured-after", 0)][InlineData("knowledge-after", 0)][InlineData("recorded-after", 0)]
    public async Task EveryCaptureClockIsCutoffBound(string boundary, int n)
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 1, ct);
        await f.DropChecks("decision_snapshot_run", ct);
        await f.Sql("UPDATE decision_snapshot_run SET captured_at=$1,knowledge_cutoff=$2,recorded_at=$3", ct,
            boundary == "captured-after" ? Captured.AddMilliseconds(1) : Captured,
            boundary == "knowledge-after" ? Captured.AddMilliseconds(1) : Captured,
            boundary == "recorded-after" ? Captured.AddMilliseconds(1) : Captured);
        Assert.Equal(n, (await f.Service.ReadAsync(Query() with { Cutoff = Captured }, ct)).Summary.Counts.N);
    }

    [Fact(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task JakartaDatesExactContextAndOneHorizonSelectOnlyCapturedFacts()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(3, 1, ct);
        await f.Sql("UPDATE decision_snapshot_run SET portfolio_id=CASE WHEN run_id=$2 THEN $1 ELSE $3 END WHERE run_id IN ($2,$4)", ct,
            Id(88), Id(2, '2'), Id(99), Id(3, '2'));
        var discovery = await f.Service.ReadAsync(Query(), ct); Assert.Equal(Id(1, '2'), Assert.Single(discovery.Observations).Observation.RunId);
        Assert.Equal(Id(2, '2'), Assert.Single((await f.Service.ReadAsync(Query() with { PortfolioId = Id(88) }, ct)).Observations).Observation.RunId);
        Assert.Equal(Id(3, '2'), Assert.Single((await f.Service.ReadAsync(Query() with { PortfolioId = Id(99) }, ct)).Observations).Observation.RunId);
        await f.Outcome(1, "10", "AVAILABLE", ct, 5); await f.Outcome(1, "99", "AVAILABLE", ct, 1);
        Assert.Equal(10m, (await f.Service.ReadAsync(Query(), ct)).Summary.Metrics.Mean);
        Assert.Equal(99m, (await f.Service.ReadAsync(Query() with { HorizonSessions = 1 }, ct)).Summary.Metrics.Mean);
        Assert.Equal(1, (await f.Service.ReadAsync(Query() with { HorizonSessions = 20 }, ct)).Summary.Counts.U);
        var midnight = new DateTimeOffset(2026, 9, 30, 17, 0, 0, TimeSpan.Zero);
        await f.Sql("UPDATE decision_snapshot_run SET captured_at=$1,knowledge_cutoff=$1,recorded_at=$1,through='2026-10-01' WHERE run_id=$2", ct, midnight, Id(1, '2'));
        Assert.Equal(0, (await f.Service.ReadAsync(Query() with { CaptureTo = new(2026, 9, 30) }, ct)).Summary.Counts.N);
        Assert.Equal(1, (await f.Service.ReadAsync(Query() with { CaptureFrom = new(2026, 10, 1), CaptureTo = new(2026, 10, 1) }, ct)).Summary.Counts.N);
    }

    [Theory(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("-0.0000000000000000000000000001")][InlineData("0.0000000000000000000000000001")]
    [InlineData("0.0000000000000000000000000000")][InlineData("-10.25")]
    [InlineData("79228162514264337593543950335")][InlineData("1.000000000000000000000000000000000")]
    public async Task ExactDatabaseNumericsRetainTheirValue(string value)
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 1, ct);
        await f.Outcome(1, value, "AVAILABLE", ct);
        var result = await f.Service.ReadAsync(Query(), ct);
        Assert.Equal(decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture),
            Assert.Single(result.Observations).Outcome!.PriceReturnPct);
    }

    [Theory(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("79228162514264337593543950336")][InlineData("0.00000000000000000000000000001")]
    [InlineData("1.12345678901234567890123456789")][InlineData("NaN")][InlineData("Infinity")]
    public async Task NonRepresentableDatabaseReturnFailsWithoutRounding(string value)
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 1, ct);
        await f.Outcome(1, value, "AVAILABLE", ct);
        Assert.Equal("RESEARCH_UNAVAILABLE", (await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ReadAsync(Query(), ct))).Message);
    }

    [Theory(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("capture-policy")][InlineData("capture-schema")][InlineData("capture-kind")][InlineData("capture-universe")]
    [InlineData("outcome-policy")][InlineData("outcome-schema")]
    public async Task UnsupportedBindingsCannotHideBehindDrilldowns(string binding)
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 1, ct);
        await f.Outcome(1, "10", "AVAILABLE", ct);
        var table = binding.StartsWith("capture", StringComparison.Ordinal) ? "decision_snapshot_run" : "decision_snapshot_outcome";
        await f.DropChecks(table, ct);
        var change = binding switch { "capture-policy" => "policy_id='latest'", "capture-schema" => "schema_version=2",
            "capture-kind" => "capture_kind='REPLAY'", "capture-universe" => "universe='FullIdx'",
            "outcome-policy" => "outcome_policy_id='latest'", _ => "schema_version=2" };
        await f.Sql($"UPDATE {table} SET {change}", ct);
        Assert.Equal("RESEARCH_POLICY_VERSION_UNAVAILABLE", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.ReadAsync(Query() with { InstrumentId = Id(999) }, ct))).Message);
    }

    [Theory(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("missing-row")][InlineData("manifest-id")][InlineData("row-reasons")][InlineData("market-context")]
    [InlineData("anchor")][InlineData("available-null")][InlineData("unavailable-return")][InlineData("episode-numeric")][InlineData("wrong-reason")]
    public async Task CorruptStoredStructureFailsClosed(string broken)
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 1, ct);
        await f.Outcome(1, "10", "AVAILABLE", ct); await f.DropChecks("decision_snapshot_row", ct); await f.DropChecks("decision_snapshot_outcome", ct);
        var sql = broken switch {
            "missing-row" => "DELETE FROM decision_snapshot_row",
            "manifest-id" => "UPDATE decision_snapshot_run SET evidence_manifest=jsonb_set(evidence_manifest,'{evaluatedInstrumentIds}','[\"10000000-0000-4000-8000-000000000099\"]')",
            "row-reasons" => "UPDATE decision_snapshot_row SET result=result-'setupReasons'",
            "market-context" => "UPDATE decision_snapshot_run SET result=jsonb_set(result,'{marketContext}', '{}')",
            "anchor" => "UPDATE decision_snapshot_outcome SET anchor_close=101",
            "available-null" => "UPDATE decision_snapshot_outcome SET price_return_pct=NULL",
            "unavailable-return" => "UPDATE decision_snapshot_outcome SET outcome_state='DATA_UNAVAILABLE',reason='SUSPENDED_AT_HORIZON'",
            "wrong-reason" => "UPDATE decision_snapshot_outcome SET outcome_state='DATA_UNAVAILABLE',price_return_pct=NULL,reason='BOGUS'",
            _ => "UPDATE decision_snapshot_row SET episode_id='screener-v0.1.0/10000000-0000-4000-8000-000000000001/2026-09-30',setup='WATCH',result=jsonb_set(result,'{episode}', '{\"id\":\"screener-v0.1.0/10000000-0000-4000-8000-000000000001/2026-09-30\",\"startDate\":\"2026-09-30\",\"ageSessions\":1,\"confirmationDate\":null,\"endDate\":null,\"endReason\":null,\"confirmedAgeSessions\":null,\"triggerPrice\":null,\"watchThreshold\":0.00000000000000000000000000001}')" };
        await f.Sql(sql, ct);
        Assert.Equal("RESEARCH_UNAVAILABLE", (await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ReadAsync(Query(), ct))).Message);
    }

    [Fact(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task RunAndRowCeilingsApplyBeforeNarrowFilters()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1000, 0, ct);
        Assert.Equal(1000, (await f.Service.ReadAsync(Query(), ct)).EmptyRunCount);
        await f.Seed(1, 0, ct, start: 1001);
        Assert.Equal("RESEARCH_BOUND_EXCEEDED", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.ReadAsync(Query() with { InstrumentId = Id(999) }, ct))).Message);
        await using var rows = await Fixture.Create(ct); await rows.Seed(50, 200, ct);
        var timer = Stopwatch.StartNew(); var accepted = await rows.Service.ReadAsync(Query(), ct);
        Assert.Equal(10000, accepted.Summary.Counts.N); Assert.True(timer.Elapsed < TimeSpan.FromSeconds(60));
        await rows.Seed(1, 1, ct, start: 51);
        Assert.Equal("RESEARCH_BOUND_EXCEEDED", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rows.Service.ReadAsync(Query() with { Cohort = ResearchCohort.FAILED }, ct))).Message);
        Assert.Throws<ArgumentException>(() => (Query() with { CaptureTo = new(2027, 8, 25) }).Normalize(Now.AddYears(1)));
    }

    [Fact(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task RepeatedEpisodeCapturesAndCurrentFactsCannotRewriteHistoricalResearch()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(2, 1, ct);
        await f.Sql("""
            UPDATE decision_snapshot_run SET status='PARTIAL';
            UPDATE decision_snapshot_row SET setup='WATCH',episode_id='screener-v0.1.0/10000000-0000-4000-8000-000000000001/2026-09-30',
                result=jsonb_set(result,'{episode}','{"id":"screener-v0.1.0/10000000-0000-4000-8000-000000000001/2026-09-30",
                    "startDate":"2026-09-30","ageSessions":1,"confirmationDate":null,"endDate":null,"endReason":null,
                    "confirmedAgeSessions":null,"triggerPrice":null,"watchThreshold":100}');
            """, ct);
        var before = await f.Service.ReadAsync(Query(), ct);
        Assert.Equal((2, 1, 1), (before.Summary.Counts.N, before.Summary.Diversity.EpisodeKeys, before.Summary.Diversity.ExtraEpisodeCaptures));
        // Shadow current/live tables with hostile values. They are deliberately unnecessary to the reader.
        await f.Sql("""
            CREATE TABLE instrument(symbol text); INSERT INTO instrument VALUES ('RENAMED');
            CREATE TABLE daily_bar_revision(close numeric); INSERT INTO daily_bar_revision VALUES (999999);
            CREATE TABLE market_session(proof text); INSERT INTO market_session VALUES ('later-session');
            CREATE TABLE instrument_listing_evidence(evidence text); INSERT INTO instrument_listing_evidence VALUES ('later-status');
            CREATE TABLE portfolio_event(value text); INSERT INTO portfolio_event VALUES ('changed-position');
            CREATE TABLE thesis_version(value text); INSERT INTO thesis_version VALUES ('changed-thesis');
            """, ct);
        await f.Seed(1, 1, ct, start: 3);
        await f.Sql("UPDATE decision_snapshot_run SET captured_at=$1,knowledge_cutoff=$1,recorded_at=$1,through='2026-10-16' WHERE run_id=$2", ct, Now.AddDays(1), Id(3, '2'));
        await f.Outcome(1, "999", "AVAILABLE", ct);
        await f.Sql("UPDATE decision_snapshot_outcome SET outcome_known_at=$1,recorded_at=$1", ct, Now.AddDays(1));
        Assert.Equal(Json(before), Json(await f.Service.ReadAsync(Query(), ct)));
        Assert.All(before.Observations, c => Assert.Equal("OLD", c.Observation.Symbol));
    }

    [Fact(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task TransactionIsReadOnlyAndRepeatableRead()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 1, ct);
        await f.Sql("""
            ALTER TABLE decision_snapshot_row RENAME TO stored_rows;
            CREATE VIEW decision_snapshot_row AS SELECT * FROM stored_rows
                WHERE current_setting('transaction_read_only')='on'
                    AND current_setting('transaction_isolation')='repeatable read';
            """, ct);
        Assert.Equal(1, (await f.Service.ReadAsync(Query(), ct)).Summary.Counts.N);
    }

    [Fact(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ConcurrentOutcomeCommitCannotMixDatabaseSnapshots()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 1, ct);
        await f.Outcome(1, "10", "AVAILABLE", ct);
        await f.Sql("CREATE TABLE pending_outcome AS SELECT * FROM decision_snapshot_outcome; DELETE FROM decision_snapshot_outcome", ct);
        await using var locker = await f.Source.OpenConnectionAsync(ct); await using var transaction = await locker.BeginTransactionAsync(ct);
        await using (var command = new NpgsqlCommand("LOCK TABLE decision_snapshot_row IN ACCESS EXCLUSIVE MODE", locker, transaction))
            await command.ExecuteNonQueryAsync(ct);
        var inFlight = f.Service.ReadAsync(Query(), ct);
        await WaitForRowRead(f, ct);
        await using (var insert = new NpgsqlCommand("INSERT INTO decision_snapshot_outcome SELECT * FROM pending_outcome", locker, transaction))
            await insert.ExecuteNonQueryAsync(ct); // Commit after the reader snapshot is established; no read of the locked row table.
        await transaction.CommitAsync(ct);
        Assert.Equal((0, 1), ((await inFlight).Summary.Counts.A, (await inFlight).Summary.Counts.U));
        var later = await f.Service.ReadAsync(Query(), ct);
        Assert.Equal((1, 0), (later.Summary.Counts.A, later.Summary.Counts.U));
    }
    private static async Task WaitForRowRead(Fixture f, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (await f.Scalar("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND application_name=current_setting('application_name') AND wait_event_type='Lock' AND query LIKE 'SELECT jsonb_build_object%'", timeout.Token) == "0")
            await Task.Delay(20, timeout.Token);
    }

    [Fact(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task WaitingReadHonorsCallerCancellationAndSanitizesDatabaseFailure()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 1, ct);
        await using var locker = await f.Source.OpenConnectionAsync(ct); await using var transaction = await locker.BeginTransactionAsync(ct);
        await using (var command = new NpgsqlCommand("LOCK TABLE decision_snapshot_row IN ACCESS EXCLUSIVE MODE", locker, transaction))
            await command.ExecuteNonQueryAsync(ct);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct); cancel.CancelAfter(TimeSpan.FromMilliseconds(150));
        var timer = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.ReadAsync(Query(), cancel.Token));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5)); await transaction.RollbackAsync(ct);
        await f.Sql("DROP TABLE decision_snapshot_outcome", ct);
        Assert.Equal("RESEARCH_UNAVAILABLE", (await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ReadAsync(Query(), ct))).Message);
    }

    [Fact(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task ExistingIndexesSupportRepresentativeSetBasedQueryShapes()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(50, 200, ct);
        await f.Sql("ANALYZE decision_snapshot_run; ANALYZE decision_snapshot_row; ANALYZE decision_snapshot_outcome", ct);
        var plan = await f.Scalar("""
            EXPLAIN (FORMAT JSON) SELECT s.run_id,s.instrument_id,o.price_return_pct FROM decision_snapshot_row s
            LEFT JOIN decision_snapshot_outcome o ON o.run_id=s.run_id AND o.instrument_id=s.instrument_id
                AND o.horizon_sessions=5 AND o.outcome_known_at<=$2 AND o.recorded_at<=$2
            WHERE s.run_id=ANY($1)
            """, ct, new[] { Id(1, '2') }, Now);
        Assert.Contains("Index", plan, StringComparison.Ordinal); Assert.Contains("decision_snapshot_row", plan, StringComparison.Ordinal);
        var captures = await f.Scalar("""
            EXPLAIN (FORMAT JSON) SELECT run_id FROM decision_snapshot_run WHERE portfolio_id IS NULL
                AND captured_at>=$1 AND captured_at<$2 AND captured_at<=$2 AND knowledge_cutoff<=$2 AND recorded_at<=$2
            ORDER BY captured_at,run_id LIMIT 1001
            """, ct, Captured, Now);
        Assert.Contains("decision_snapshot_run", captures, StringComparison.Ordinal);
    }

    [Theory(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    [InlineData("captured")][InlineData("anchor")][InlineData("horizon")]
    public async Task EveryAnalyticalPriceUsesTheStrictNumericBoundary(string price)
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 1, ct);
        await f.Outcome(1, "10", "AVAILABLE", ct);
        var sql = price switch { "captured" => "UPDATE decision_snapshot_row SET close=0.00000000000000000000000000001",
            "anchor" => "UPDATE decision_snapshot_outcome SET anchor_close=0.00000000000000000000000000001",
            _ => "UPDATE decision_snapshot_outcome SET horizon_close=0.00000000000000000000000000001" };
        await f.Sql(sql, ct);
        Assert.Equal("RESEARCH_UNAVAILABLE", (await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ReadAsync(Query(), ct))).Message);
    }

    [Fact(Skip = "Opt-in owned PostgreSQL Research test", SkipUnless = nameof(DatabaseConfigured))]
    public async Task OversizedRetainedProjectionFailsWithoutPartialSuccess()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await Fixture.Create(ct); await f.Seed(1, 1, ct);
        await f.Sql("UPDATE decision_snapshot_row SET symbol=repeat('x',3*1024*1024)", ct);
        Assert.Equal("RESEARCH_BOUND_EXCEEDED", (await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ReadAsync(Query(), ct))).Message);
    }
}
