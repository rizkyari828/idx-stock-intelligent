using System.Globalization;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ResearchEvaluationTests
{
    private static readonly DateTimeOffset Captured = new(2026, 10, 3, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Known = Captured.AddDays(7);
    private static readonly DateTimeOffset Now = Captured.AddDays(12);
    private static readonly DateOnly Target = new(2026, 9, 30);
    private static Guid Id(int n) => Guid.Parse("10000000-0000-4000-8000-" + n.ToString("D12", CultureInfo.InvariantCulture));
    private static ResearchQuery Query() => new(new(2026, 10, 1), new(2026, 10, 15), Now);
    private static ResearchRun Run(int count, int id = 100, Guid? portfolio = null) => new(
        new(Id(id), Id(id + 1000), 1, "PROSPECTIVE_CAPTURE", Captured, Captured, Captured.AddSeconds(1),
            OutcomeEvaluator.Through(Captured), Target, ScreenerReadRequest.Anchor, "screener-v0.1.0", "PILOT",
            "original-universe", portfolio, new('a', 64), new('b', 64), "COMPLETE", count), "UNKNOWN", "UNKNOWN", Target, ["original-context"]);
    private static ResearchObservation Row(int id = 1, int run = 100) => new(Id(run), Id(id), "OLD", true, false,
        "ELIGIBLE", "NONE", true, [], [], null, Target, 100m);
    private static ResearchOutcome Outcome(ResearchObservation row, decimal? value = 10, string state = "AVAILABLE") => new(
        row.RunId, row.InstrumentId, 5, "outcome-v0.1.0", 1, row.MarketDate, row.Close, new(2026, 10, 8),
        state == "AVAILABLE" ? 110 : null, state, state switch { "AVAILABLE" => null,
            "ANCHOR_UNAVAILABLE" => "CAPTURED_PRICE_BASIS_UNVERIFIED", "DATA_UNAVAILABLE" => "SUSPENDED_AT_HORIZON",
            _ => "PRICE_CONVENTION_UNSUPPORTED" }, state == "AVAILABLE" ? value : null, Known, Known.AddSeconds(1));
    private static ResearchResult Evaluate(ResearchRun[] runs, ResearchObservation[] rows, ResearchOutcome[]? outcomes = null, ResearchQuery? query = null)
        => ResearchEvaluation.Evaluate(query ?? Query(), runs, rows, outcomes ?? [], Now, TestContext.Current.CancellationToken);
    private static ResearchResult Returns(params decimal[] values)
    {
        var rows = values.Select((_, i) => Row(i + 1)).ToArray();
        return Evaluate([Run(rows.Length)], rows, rows.Select((r, i) => Outcome(r, values[i])).ToArray());
    }
    private static ResearchObservation WithEpisode(ResearchObservation row, string setup = "WATCH") => row with
    {
        Setup = setup,
        Episode = new($"screener-v0.1.0/{row.InstrumentId:D}/2026-09-30", Target,
            setup == "WATCH" ? null : Target, setup == "FAILED" ? Target : null,
            setup == "FAILED" ? "CLOSE_BELOW_TRIGGER" : null, 1, setup == "WATCH" ? null : 1,
            setup == "WATCH" ? null : 100, setup == "WATCH" ? 100 : null)
    };
    private static string Json(object value) => JsonSerializer.Serialize(value);

    [Theory]
    [InlineData(1)][InlineData(5)][InlineData(10)][InlineData(20)]
    public void QueryDefaultsOffsetsAndFixedHorizonsNormalize(int horizon)
    {
        var query = Query() with { HorizonSessions = horizon };
        var offset = query with { Cutoff = query.Cutoff.ToOffset(TimeSpan.FromHours(7)) };
        Assert.Equal(query.Normalize(Now).Canonical(), offset.Normalize(Now).Canonical());
        Assert.Equal("ALL", query.Canonical().Cohort); Assert.Equal("NONE", query.Canonical().GroupBy);
        Assert.Equal(Query(), Query() with { HorizonSessions = 5, Cohort = ResearchCohort.ALL, GroupBy = ResearchGrouping.NONE });
        var uuid = Guid.Parse("ABCDEF12-ABCD-4321-ABCD-ABCDEF123456");
        Assert.Equal("abcdef12-abcd-4321-abcd-abcdef123456", (query with { InstrumentId = uuid }).Canonical().InstrumentId);
        Assert.EndsWith("+00:00", offset.Canonical().Cutoff);
    }
    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(21)]
    public void InvalidHorizonRejected(int n) => Assert.Throws<ArgumentException>(() => (Query() with { HorizonSessions = n }).Normalize(Now));
    [Fact]
    public void QueryDatesCutoffAndNonzeroIdsAreBounded()
    {
        foreach (var q in new[] { Query() with { CaptureTo = new(2026, 9, 30) },
            Query() with { CaptureFrom = new(2026, 8, 23) }, Query() with { CaptureTo = new(2027, 8, 25) },
            Query() with { CaptureTo = new(2026, 10, 16) }, Query() with { Cutoff = Now.AddTicks(1) },
            Query() with { Cutoff = DateTimeOffset.MinValue }, Query() with { PortfolioId = Guid.Empty },
            Query() with { InstrumentId = Guid.Empty }, Query() with { Cohort = (ResearchCohort)99 },
            Query() with { GroupBy = (ResearchGrouping)99 } })
            Assert.Throws<ArgumentException>(() => q.Normalize(Now));
        var later = new DateTimeOffset(2027, 8, 24, 13, 0, 0, TimeSpan.Zero);
        var full = new ResearchQuery(ScreenerReadRequest.Anchor, ScreenerReadRequest.Horizon, later);
        Assert.Equal(365, full.Normalize(later).CaptureTo.DayNumber - full.CaptureFrom.DayNumber); // 366 inclusive civil dates.
        Assert.Throws<ArgumentException>(() => (full with { CaptureTo = full.CaptureTo.AddDays(1) }).Normalize(later.AddDays(1)));
    }
    [Theory]
    [InlineData("")][InlineData(" ")][InlineData("null")][InlineData("all")][InlineData("1")][InlineData("[\"ALL\"]")][InlineData("UNKNOWN")]
    public void ExactEnumTokensRejectTransportLikeAlternatives(string value)
    {
        Assert.Throws<ArgumentException>(() => ResearchQuery.ParseCohort(value));
        Assert.Throws<ArgumentException>(() => ResearchQuery.ParseGrouping(value));
    }
    [Fact]
    public void EveryFrozenEnumTokenParsesExactly()
    {
        foreach (var c in Enum.GetValues<ResearchCohort>()) Assert.Equal(c, ResearchQuery.ParseCohort(c.ToString()));
        foreach (var g in Enum.GetValues<ResearchGrouping>()) Assert.Equal(g, ResearchQuery.ParseGrouping(g.ToString()));
    }
    [Fact]
    public void EpisodeDrilldownRequiresExactBoundedRetainedKey()
    {
        var key = WithEpisode(Row()).Episode!.Id;
        Assert.Equal(key, (Query() with { EpisodeId = key }).Normalize(Now).EpisodeId);
        Assert.Throws<ArgumentException>(() => (Query() with { EpisodeId = key, InstrumentId = Id(1) }).Normalize(Now));
        foreach (var bad in new[] { "", "null", new string('x', 128), new string('x', 129),
            key.Replace("screener-v0.1.0", "latest"), key.Replace("2026-09-30", "2026-08-23"),
            "screener-v0.1.0/00000000-0000-0000-0000-000000000000/2026-09-30" })
            Assert.Throws<ArgumentException>(() => (Query() with { EpisodeId = bad }).Normalize(Now));
    }
    [Theory]
    [InlineData("DATA_BLOCKED", "NONE", false, ResearchCohort.DATA_BLOCKED)]
    [InlineData("INELIGIBLE", "NONE", false, ResearchCohort.INELIGIBLE)]
    [InlineData("ELIGIBLE", "NONE", false, ResearchCohort.NOT_EVALUATED)]
    [InlineData("ELIGIBLE", "NONE", true, ResearchCohort.NONE)]
    [InlineData("ELIGIBLE", "WATCH", true, ResearchCohort.WATCH)]
    [InlineData("ELIGIBLE", "CONFIRMED", true, ResearchCohort.CONFIRMED)]
    [InlineData("ELIGIBLE", "FAILED", true, ResearchCohort.FAILED)]
    public void ExclusiveFirstMatchCohorts(string eligibility, string setup, bool evaluated, ResearchCohort cohort)
        => Assert.Equal(cohort, ResearchEvaluation.Classify(Row() with { Eligibility = eligibility, Setup = setup, SetupEvaluated = evaluated }));
    [Theory]
    [InlineData("DATA_BLOCKED", "NONE", true)][InlineData("INELIGIBLE", "WATCH", true)]
    [InlineData("ELIGIBLE", "WATCH", false)][InlineData("unknown", "NONE", false)][InlineData("ELIGIBLE", "BUY", true)]
    public void ImpossibleCohortInputsFail(string eligibility, string setup, bool evaluated)
        => Assert.Throws<InvalidOperationException>(() => ResearchEvaluation.Classify(Row() with { Eligibility = eligibility, Setup = setup, SetupEvaluated = evaluated }));
    [Fact]
    public void AllCohortsAreExhaustiveAndReconcileEvenBeforeFiltering()
    {
        ResearchObservation[] rows = [Row(1) with { Eligibility = "DATA_BLOCKED", SetupEvaluated = false },
            Row(2) with { Eligibility = "INELIGIBLE", SetupEvaluated = false }, Row(3) with { SetupEvaluated = false }, Row(4),
            WithEpisode(Row(5)), WithEpisode(Row(6), "CONFIRMED"), WithEpisode(Row(7), "FAILED")];
        var result = Evaluate([Run(7)], rows);
        Assert.Equal(7, result.Summary.Counts.N); Assert.Equal(7, result.Cohorts.Sum(g => g.Summary.Counts.N));
        Assert.All(result.Cohorts, g => Assert.Equal(1, g.Summary.Counts.N));
        var filtered = Evaluate([Run(7)], rows, query: Query() with { Cohort = ResearchCohort.DATA_BLOCKED });
        Assert.Equal(7, filtered.BaseObservationCount); Assert.Equal(6, filtered.FilteredOutObservationCount);
        Assert.Equal(1, filtered.Summary.Counts.U); Assert.All(filtered.BaseCohortCounts, c => Assert.Equal(1, c.N));
    }
    [Theory]
    [InlineData(ResearchGrouping.MARKET_TREND, "POSITIVE")][InlineData(ResearchGrouping.MARKET_TREND, "NEUTRAL")]
    [InlineData(ResearchGrouping.MARKET_TREND, "NEGATIVE")][InlineData(ResearchGrouping.MARKET_TREND, "UNKNOWN")]
    [InlineData(ResearchGrouping.MARKET_VOLATILITY, "NORMAL")][InlineData(ResearchGrouping.MARKET_VOLATILITY, "ELEVATED")]
    [InlineData(ResearchGrouping.MARKET_VOLATILITY, "UNKNOWN")]
    public void CapturedMarketPartitionsIncludeUnknown(ResearchGrouping group, string bucket)
    {
        var run = Run(1) with { MarketTrend = group == ResearchGrouping.MARKET_TREND ? bucket : "UNKNOWN",
            MarketVolatility = group == ResearchGrouping.MARKET_VOLATILITY ? bucket : "UNKNOWN" };
        var result = Evaluate([run], [Row()], query: Query() with { GroupBy = group });
        Assert.Equal(bucket, Assert.Single(result.Observations).Partition);
        Assert.Equal(7 * ResearchEvaluation.Partitions(group).Count, result.Cohorts.Count);
        Assert.Equal(1, result.Cohorts.Sum(g => g.Summary.Counts.N));
    }
    [Fact]
    public void HeldAndMembershipAreCapturedContextNotCurrentState()
    {
        var row = Row(); var portfolio = Run(1, portfolio: Id(999)); var noContext = Run(1);
        Assert.Equal("NO_PORTFOLIO_CONTEXT", ResearchEvaluation.Partition(ResearchGrouping.HELD_CONTEXT, noContext, row));
        Assert.Equal("NOT_HELD", ResearchEvaluation.Partition(ResearchGrouping.HELD_CONTEXT, portfolio, row));
        Assert.Equal("HELD", ResearchEvaluation.Partition(ResearchGrouping.HELD_CONTEXT, portfolio, row with { Held = true }));
        Assert.Equal("CONFIGURED", ResearchEvaluation.Partition(ResearchGrouping.MEMBERSHIP, portfolio, row));
        Assert.Equal("OUTSIDE_CONFIGURED", ResearchEvaluation.Partition(ResearchGrouping.MEMBERSHIP, portfolio, row with { Configured = false }));
        Assert.Equal("MEMBERSHIP_UNKNOWN", ResearchEvaluation.Partition(ResearchGrouping.MEMBERSHIP,
            portfolio with { Header = portfolio.Header with { UniverseSnapshotId = null } }, row with { Configured = false }));
        var result = Evaluate([portfolio], [row with { Held = true }], query: Query() with { PortfolioId = Id(999), GroupBy = ResearchGrouping.HELD_CONTEXT });
        Assert.Equal(1, result.Summary.Counts.N); Assert.Equal("HELD", Assert.Single(result.Observations).Partition);
        Assert.Equal(0, Evaluate([portfolio], [row]).Summary.Counts.N);
    }
    [Fact]
    public void RequiredEightObservationArithmeticFixture()
    {
        var rows = Enumerable.Range(1, 8).Select(n => Row(n)).ToArray();
        var outcomes = new[] { Outcome(rows[0], -10), Outcome(rows[1], 0), Outcome(rows[2], 5), Outcome(rows[3], 15),
            Outcome(rows[4], state: "ANCHOR_UNAVAILABLE"), Outcome(rows[5], state: "DATA_UNAVAILABLE"), Outcome(rows[6], state: "BASIS_UNCERTAIN") };
        var result = Evaluate([Run(8)], rows, outcomes);
        Assert.Equal(new ResearchCounts(8, 4, 3, 7, 1, 1, 1, 1, 2, 1, 1), result.Summary.Counts);
        Assert.Equal(new ResearchMetrics(2.5m, 2.5m, -10m, 15m, 50m, 50m), result.Summary.Metrics);
        Assert.Equal("UNRESOLVED", result.Observations[^1].Resolution); Assert.Null(result.Observations[^1].Outcome);
        Assert.Equal("NO_COMMITTED_OUTCOME_AS_OF_CUTOFF", result.Observations[^1].ResearchReason);
    }
    [Theory]
    [InlineData("positive")][InlineData("negative")][InlineData("mixed")][InlineData("zeros")][InlineData("one")][InlineData("duplicates")]
    public void MeanMedianExtremaAndSignsUseExactDecimals(string scenario)
    {
        decimal[] values = scenario switch { "positive" => [1, 3, 5], "negative" => [-5, -3, -1],
            "mixed" => [-10, 0, 5, 15], "zeros" => [0, 0], "one" => [7], _ => [-1, -1, 1, 1] };
        var result = Returns(values); var sorted = values.Order().ToArray(); var a = values.Length;
        Assert.Equal(values.Sum() / a, result.Summary.Metrics.Mean);
        Assert.Equal(a % 2 == 1 ? sorted[a / 2] : (sorted[a / 2 - 1] + sorted[a / 2]) / 2m, result.Summary.Metrics.Median);
        Assert.Equal(values.Min(), result.Summary.Metrics.Minimum); Assert.Equal(values.Max(), result.Summary.Metrics.Maximum);
        Assert.Equal(100m * values.Count(v => v > 0) / a, result.Summary.Metrics.PositiveProportion);
        Assert.Equal(a, result.Summary.Counts.P + result.Summary.Counts.Z + result.Summary.Counts.M);
    }
    [Fact]
    public void TinySignsNativeDivisionAndCanonicalDecimalTextArePreserved()
    {
        const decimal tiny = 0.0000000000000000000000000001m;
        var result = Returns(tiny, -tiny, 0);
        Assert.Equal((1, 1, 1), (result.Summary.Counts.P, result.Summary.Counts.Z, result.Summary.Counts.M));
        Assert.Equal(100m / 3m, result.Summary.Metrics.PositiveProportion);
        Assert.Equal("0", ResearchEvaluation.DecimalText(new decimal(0, 0, 0, true, 28)));
        Assert.Equal(ResearchEvaluation.DecimalText(1m), ResearchEvaluation.DecimalText(1.000m));
        Assert.Equal(tiny, decimal.Parse(ResearchEvaluation.DecimalText(tiny)!, NumberStyles.Float, CultureInfo.InvariantCulture));
        Assert.Null(ResearchEvaluation.DecimalText(null));
        Assert.Equal(tiny, result.Observations[0].Outcome!.PriceReturnPct);
    }
    [Fact]
    public void CheckedArithmeticNeverDropsOverflowingReturns()
    {
        Assert.Throws<OverflowException>(() => Returns(decimal.MaxValue, 1)); // Mean sum.
        Assert.Throws<OverflowException>(() => Returns(-decimal.MaxValue, 50000000000000000000000000000m,
            50000000000000000000000000000m, 50000000000000000000000000000m)); // Sum fits; even median overflows.
    }
    [Fact]
    public void EmptyAndEntirelyUnresolvedPopulationsHaveExplicitNullMetrics()
    {
        var empty = Evaluate([Run(0)], []);
        Assert.Equal(1, empty.EmptyRunCount); Assert.Equal(new ResearchCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), empty.Summary.Counts);
        Assert.Equal(new ResearchMetrics(null, null, null, null, null, null), empty.Summary.Metrics);
        var missing = Evaluate([Run(1)], [Row()]);
        Assert.Equal((1, 0, 1), (missing.Summary.Counts.N, missing.Summary.Counts.R, missing.Summary.Counts.U));
        Assert.Equal(new ResearchMetrics(null, null, null, null, null, 0), missing.Summary.Metrics);
        Assert.Contains(missing.Summary.Warnings, w => w.Text == "No committed AVAILABLE outcomes at this cutoff.");
        var terminal = Evaluate([Run(1)], [Row()], [Outcome(Row(), state: "DATA_UNAVAILABLE")]);
        Assert.Equal((1, 1, 0), (terminal.Summary.Counts.N, terminal.Summary.Counts.T, terminal.Summary.Counts.U));
        Assert.Null(terminal.Summary.Metrics.Mean);
    }
    [Fact]
    public void CanonicalOrderingAndAggregationDoNotDependOnInputEnumeration()
    {
        var runs = new[] { Run(2, 102), Run(2, 101), Run(2, 100) };
        runs[0] = runs[0] with { Header = runs[0].Header with { CapturedAt = Captured.AddDays(-1), KnowledgeCutoff = Captured.AddDays(-1),
            RecordedAt = Captured.AddDays(-1).AddSeconds(1), Through = OutcomeEvaluator.Through(Captured.AddDays(-1)) } };
        var rows = runs.SelectMany(r => new[] { Row(2) with { RunId = r.Header.RunId }, Row(1) with { RunId = r.Header.RunId } }).ToArray();
        var outcomes = rows.Select(r => Outcome(r)).ToArray(); var result = Evaluate(runs, rows, outcomes);
        Assert.Equal(Json(result), Json(Evaluate(runs.Reverse().ToArray(), rows.Reverse().ToArray(), outcomes.Reverse().ToArray())));
        Assert.Equal(new[] { Id(102), Id(102), Id(100), Id(100), Id(101), Id(101) }, result.Observations.Select(c => c.Observation.RunId));
        Assert.Equal(new[] { Id(1), Id(2), Id(1), Id(2), Id(1), Id(2) }, result.Observations.Select(c => c.Observation.InstrumentId));
    }
    [Fact]
    public void RepeatedEpisodesAndSessionsAreCountedWithoutDeduplication()
    {
        var runs = new[] { Run(1, 100), Run(1, 101), Run(1, 102) };
        var rows = new[] { WithEpisode(Row()), WithEpisode(Row(run: 101), "CONFIRMED"), WithEpisode(Row(run: 102), "FAILED") };
        var result = Evaluate(runs, rows, rows.Select(r => Outcome(r, 10)).ToArray());
        Assert.Equal(3, result.Summary.Counts.N);
        Assert.Equal(new ResearchDiversity(3, 1, 1, 0, 3, 0, 1, 2, 2), result.Summary.Diversity);
        Assert.Equal(1, result.Summary.AvailableDiversity.EpisodeKeys);
        Assert.Equal(3, result.Cohorts.Sum(g => g.Summary.Diversity.EpisodeKeys));
        Assert.Contains(result.Summary.Warnings, w => w.Code == "REPEATED_EPISODES");
        var drilldown = Evaluate(runs, rows, query: Query() with { EpisodeId = rows[0].Episode!.Id });
        Assert.Equal(3, drilldown.Summary.Counts.N);
    }
    [Fact]
    public void NullTargetsAndNoEpisodeObservationsRemainDistinct()
    {
        var runs = new[] { Run(1), Run(1, 101) };
        runs[1] = runs[1] with { Header = runs[1].Header with { TargetSession = null } };
        var result = Evaluate(runs, [Row(), Row(run: 101)]);
        Assert.Equal(new ResearchDiversity(2, 1, 1, 1, 0, 2, 0, 0, 0), result.Summary.Diversity);
    }
    [Fact]
    public void CutoffUsesBothClocksAndNeverExposesFutureOutcomeFields()
    {
        var row = Row(); var future = Outcome(row) with { RecordedAt = Now.AddTicks(1) };
        var result = Evaluate([Run(1)], [row], [future]);
        Assert.Null(Assert.Single(result.Observations).Outcome); Assert.Equal(1, result.Summary.Counts.U);
        Assert.Equal(Json(Evaluate([Run(1)], [row])), Json(result));
        Assert.Equal(1, Evaluate([Run(1)], [row], [future with { RecordedAt = Now, OutcomeKnownAt = Now }]).Summary.Counts.A);
        var capture = Run(1) with { Header = Run(1).Header with { RecordedAt = Now.AddTicks(1) } };
        Assert.Equal(0, Evaluate([capture], [row]).Summary.Counts.N);
        Assert.Equal(1, Evaluate([capture with { Header = capture.Header with { RecordedAt = Now } }], [row]).Summary.Counts.N);
    }
    [Fact]
    public void CaptureDatesUseJakartaAndLaterCapturesOtherHorizonsAreIsolated()
    {
        var run = Run(1); var at = new DateTimeOffset(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);
        run = run with { Header = run.Header with { CapturedAt = at, KnowledgeCutoff = at, RecordedAt = at.AddSeconds(1) } };
        var q = Query() with { CaptureFrom = new(2026, 10, 3), CaptureTo = new(2026, 10, 3) };
        var old = Evaluate([run], [Row()], [Outcome(Row())], q);
        var later = Run(1, 101) with { Header = Run(1, 101).Header with { CapturedAt = Now.AddDays(1), KnowledgeCutoff = Now.AddDays(1),
            RecordedAt = Now.AddDays(1), Through = OutcomeEvaluator.Through(Now.AddDays(1)) } };
        Assert.Equal(Json(old), Json(Evaluate([later, run], [Row(run: 101), Row()],
            [Outcome(Row()), Outcome(Row()) with { HorizonSessions = 20, PriceReturnPct = 999 }], q)));
    }
    [Theory]
    [InlineData("capture-policy")][InlineData("capture-schema")][InlineData("outcome-policy")][InlineData("outcome-schema")]
    public void UnsupportedBindingCannotBeHiddenByCapturedFilters(string changed)
    {
        var run = Run(1); var outcome = Outcome(Row());
        if (changed == "capture-policy") run = run with { Header = run.Header with { PolicyId = "latest" } };
        if (changed == "capture-schema") run = run with { Header = run.Header with { SchemaVersion = 2 } };
        if (changed == "outcome-policy") outcome = outcome with { OutcomePolicyId = "latest" };
        if (changed == "outcome-schema") outcome = outcome with { SchemaVersion = 2 };
        Assert.Equal("RESEARCH_POLICY_VERSION_UNAVAILABLE", Assert.Throws<InvalidOperationException>(() =>
            Evaluate([run], [Row()], [outcome], Query() with { InstrumentId = Id(999) })).Message);
    }
    [Fact]
    public void DuplicateIncompleteAndMalformedTypedInputsFailClosed()
    {
        Assert.Throws<InvalidOperationException>(() => Evaluate([Run(2)], [Row(), Row()]));
        Assert.Throws<InvalidOperationException>(() => Evaluate([Run(2)], [Row()]));
        Assert.Throws<InvalidOperationException>(() => Evaluate([Run(1), Run(1)], [Row()]));
        Assert.Throws<InvalidOperationException>(() => Evaluate([Run(1)], [Row()], [Outcome(Row()), Outcome(Row())]));
        Assert.Throws<InvalidOperationException>(() => Evaluate([Run(1)], [WithEpisode(Row()) with { Episode = null }]));
        Assert.Throws<InvalidOperationException>(() => Evaluate([Run(1)], [WithEpisode(Row()) with { InstrumentId = Id(2) }]));
        foreach (var bad in new[] { Outcome(Row()) with { PriceReturnPct = null }, Outcome(Row()) with { State = "PENDING" },
            Outcome(Row()) with { Reason = "unexpected" }, Outcome(Row()) with { AnchorClose = 999 },
            Outcome(Row()) with { RecordedAt = Captured } })
            Assert.Throws<InvalidOperationException>(() => Evaluate([Run(1)], [Row()], [bad]));
        var missing = Row() with { MarketDate = null, Close = null };
        Assert.Equal(1, Evaluate([Run(1)], [missing], [Outcome(missing, state: "ANCHOR_UNAVAILABLE")]).Summary.Counts.TAnchor);
    }
    [Fact]
    public void BaseBoundsApplyBeforeSelectiveFilters()
    {
        var runs = Enumerable.Range(100, 1001).Select(id => Run(0, id)).ToArray();
        Assert.Throws<InvalidOperationException>(() => Evaluate(runs, [], query: Query() with { InstrumentId = Id(9999) }));
        Assert.Equal(1000, Evaluate(runs.Take(1000).ToArray(), []).EmptyRunCount);
        var bounded = Enumerable.Range(100, 50).Select(id => Run(200, id)).ToArray();
        var accepted = bounded.SelectMany(r => Enumerable.Range(1, 200).Select(i => Row(i) with { RunId = r.Header.RunId })).ToArray();
        Assert.Equal(10000, Evaluate(bounded, accepted).Summary.Counts.N);
        var crowded = Enumerable.Range(100, 50).Select(id => Run(210, id)).ToArray();
        var rows = crowded.SelectMany(r => Enumerable.Range(1, 210).Select(i => Row(i) with { RunId = r.Header.RunId })).ToArray();
        Assert.Equal("RESEARCH_BOUND_EXCEEDED", Assert.Throws<InvalidOperationException>(() =>
            Evaluate(crowded, rows, query: Query() with { Cohort = ResearchCohort.FAILED })).Message);
    }
    [Theory]
    [InlineData(1, "VERY_SMALL_SAMPLE")][InlineData(4, "VERY_SMALL_SAMPLE")][InlineData(5, "SMALL_SAMPLE")]
    [InlineData(19, "SMALL_SAMPLE")][InlineData(20, "LIMITED_SAMPLE")][InlineData(29, "LIMITED_SAMPLE")][InlineData(30, "DESCRIPTIVE_SAMPLE")]
    public void SampleWarningsAreFactualOnly(int a, string code)
    {
        var result = Returns(Enumerable.Repeat(1m, a).ToArray());
        Assert.Contains(result.Summary.Warnings, w => w.Code == code);
        Assert.DoesNotContain(result.Summary.Warnings, w => w.Text.Contains("not implemented", StringComparison.Ordinal));
    }
    [Fact]
    public void AnalyticalModelsHaveNoVerificationOrWinRateSelectionFields()
    {
        foreach (var type in new[] { typeof(ResearchQuery), typeof(ResearchObservation), typeof(ResearchOutcome), typeof(ResearchMetrics) })
            Assert.DoesNotContain(type.GetProperties(), p => p.Name.Contains("Verification", StringComparison.OrdinalIgnoreCase)
                || p.Name.Equals("winRate", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, Evaluate([Run(1)], [Row() with { Eligibility = "DATA_BLOCKED", SetupEvaluated = false }], [Outcome(Row())]).Summary.Counts.A);
    }
    [Fact]
    public void CancellationStopsPureWork()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => ResearchEvaluation.Evaluate(Query(), [Run(1)], [Row()], [], Now, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ResearchEvaluation.Evaluate(Query(), [], [], [], Now, cancellation.Token));
    }

    [Fact]
    public void InstrumentDrilldownKeepsBaseAccountingAndStoredValues()
    {
        var row = Row();
        var outcome = Outcome(row, 0.0000000000000000000000000001m) with { HorizonClose = 123.456m };
        var result = Evaluate([Run(2)], [row, Row(2) with { Configured = false }], [outcome], Query() with { InstrumentId = row.InstrumentId });
        Assert.Equal((2, 1, 1), (result.BaseObservationCount, result.FilteredOutObservationCount, result.Summary.Counts.N));
        Assert.Equal(outcome, Assert.Single(result.Observations).Outcome); // Stored return is consumed, never recalculated from prices.
    }

    [Fact]
    public void EndedEpisodesRemainLinkedForNoneAndBlockedCaptures()
    {
        var ended = WithEpisode(Row(), "FAILED") with { Setup = "NONE" };
        var blocked = ended with { RunId = Id(101), Eligibility = "DATA_BLOCKED", SetupEvaluated = false };
        var result = Evaluate([Run(1), Run(1, 101)], [ended, blocked]);
        Assert.Equal(new[] { ResearchCohort.NONE, ResearchCohort.DATA_BLOCKED }, result.Observations.Select(c => c.Cohort));
        Assert.Equal(1, result.Summary.Diversity.EpisodeKeys); Assert.Equal(2, result.Summary.Diversity.EpisodeLinkedObservations);
    }

    [Fact]
    public void ProjectionCopiesOrderedCapturedReasonsAndOmitsPrivateFields()
    {
        string[] eligibility = ["second", "first"]; string[] setup = ["captured-reason"];
        var provenance = new ScreenerProvenanceDto(null, null, null, null, null, null, null, null, null, null,
            null, null, null, 0, [], null, null);
        var detail = new DecisionSnapshotRowResult(eligibility, setup, null, null, null, null, null, null, null,
            null, null, null, "UNKNOWN", null, null, null, null, "BLOCKED", [], new Dictionary<string, ScreenerFieldDto>(), provenance);
        var captured = new DecisionSnapshotRow(Id(1), "OLD", false, true, null, "DATA_BLOCKED", "NONE", false,
            null, Target, 100m, 999m, 123m, 456m, "PRIVATE", Id(77), 2, true, detail);
        var projected = ResearchObservation.From(Id(100), captured);
        eligibility[0] = "changed"; setup[0] = "changed";
        Assert.Equal(["second", "first"], projected.EligibilityReasons);
        Assert.Equal(["captured-reason"], projected.SetupReasons);
        Assert.False(projected.Configured); Assert.True(projected.Held); Assert.Equal("OLD", projected.Symbol);
        foreach (var field in new[] { "Shares", "InvestedCost", "AverageCost", "Mandate", "ThesisVersionId", "DiscoveryRank" })
            Assert.Null(typeof(ResearchObservation).GetProperty(field));
        Assert.Throws<InvalidOperationException>(() => ResearchObservation.From(Id(100), captured with { EpisodeId = "lost-link" }));
    }

    [Fact]
    public void CanonicalNumbersAndWarningsAreCultureIndependent()
    {
        var original = CultureInfo.CurrentCulture;
        var expected = Json(Returns(0.1m, 0, -0.2m));
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("id-ID");
            Assert.Equal(expected, Json(Returns(0.1m, 0, -0.2m)));
            Assert.Equal("0.1", ResearchEvaluation.DecimalText(0.1m));
            Assert.Equal("2026-10-01", Query().Canonical().CaptureFrom);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
