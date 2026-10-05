using System.Globalization;

namespace IdxStockIntelligence.Application;

#pragma warning disable CA1707 // Frozen Research vocabulary, consistent with existing public exchange enums.
public enum ResearchCohort { ALL, DATA_BLOCKED, INELIGIBLE, NOT_EVALUATED, NONE, WATCH, CONFIRMED, FAILED }
public enum ResearchGrouping { NONE, MARKET_TREND, MARKET_VOLATILITY, HELD_CONTEXT, MEMBERSHIP }
#pragma warning restore CA1707

public sealed record ResearchQuery(DateOnly CaptureFrom, DateOnly CaptureTo, DateTimeOffset Cutoff,
    Guid? PortfolioId = null, int HorizonSessions = 5, ResearchCohort Cohort = ResearchCohort.ALL,
    ResearchGrouping GroupBy = ResearchGrouping.NONE, Guid? InstrumentId = null, string? EpisodeId = null)
{
    public ResearchQuery Normalize(DateTimeOffset now)
    {
        if (CaptureFrom < ScreenerReadRequest.Anchor || CaptureTo > ScreenerReadRequest.Horizon
            || CaptureFrom > CaptureTo || CaptureTo.DayNumber - CaptureFrom.DayNumber >= 366
            || CaptureTo > OutcomeEvaluator.Through(now) || Cutoff > now
            || Cutoff < new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero)
            || PortfolioId == Guid.Empty || InstrumentId == Guid.Empty
            || !OutcomeRequest.Horizons.Contains(HorizonSessions) || !Enum.IsDefined(Cohort) || !Enum.IsDefined(GroupBy)
            || InstrumentId is not null && EpisodeId is not null
            || EpisodeId is not null && !ResearchEvaluation.ValidEpisodeKey(EpisodeId))
            throw new ArgumentException("RESEARCH_QUERY_INVALID");
        return this with { Cutoff = Cutoff.ToUniversalTime() };
    }

    // Semantic projection only; transport parsing, wire serialization and dataset hashing are later work.
    public ResearchQueryProjection Canonical() => new(CaptureFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        CaptureTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Cutoff.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        PortfolioId?.ToString("D"), HorizonSessions, Cohort.ToString(), GroupBy.ToString(), InstrumentId?.ToString("D"), EpisodeId);

    public static ResearchCohort ParseCohort(string value) => Parse<ResearchCohort>(value);
    public static ResearchGrouping ParseGrouping(string value) => Parse<ResearchGrouping>(value);
    private static T Parse<T>(string value) where T : struct, Enum
        => Enum.GetNames<T>().Contains(value, StringComparer.Ordinal) ? Enum.Parse<T>(value)
            : throw new ArgumentException("RESEARCH_QUERY_INVALID");
}

public sealed record ResearchQueryProjection(string CaptureFrom, string CaptureTo, string Cutoff, string? PortfolioId,
    int HorizonSessions, string Cohort, string GroupBy, string? InstrumentId, string? EpisodeId);
public sealed record ResearchRun(DecisionSnapshotHeader Header, string MarketTrend, string MarketVolatility,
    DateOnly? MarketContextDate, IReadOnlyList<string> MarketContextReasons)
{
    public static ResearchRun From(DecisionSnapshotHeader header, DecisionSnapshotResult result)
        => new(header, result.MarketContext.Trend, result.MarketContext.Volatility,
            result.MarketContext.MarketDate, result.MarketContext.Reasons.ToArray());
}

// Numeric inputs are decimal only. A future reader must prove exact PostgreSQL numeric
// representability BEFORE constructing these records; no raw-number parsing or rounding occurs here.
public sealed record ResearchObservation(Guid RunId, Guid InstrumentId, string? Symbol, bool Configured, bool Held,
    string Eligibility, string Setup, bool SetupEvaluated, IReadOnlyList<string> EligibilityReasons,
    IReadOnlyList<string> SetupReasons, ScreenerEpisode? Episode, DateOnly? MarketDate, decimal? Close)
{
    public static ResearchObservation From(Guid runId, DecisionSnapshotRow row)
    {
        ResearchEvaluation.Require(row.EpisodeId == row.Result.Episode?.Id);
        return new(runId, row.InstrumentId, row.Symbol, row.Configured, row.Held, row.Eligibility, row.Setup,
            row.SetupEvaluated, row.Result.EligibilityReasons.ToArray(), row.Result.SetupReasons.ToArray(),
            row.Result.Episode, row.MarketDate, row.Close);
    }
}
public sealed record ResearchOutcome(Guid RunId, Guid InstrumentId, int HorizonSessions, string OutcomePolicyId,
    int SchemaVersion, DateOnly? AnchorMarketDate, decimal? AnchorClose, DateOnly HorizonMarketDate,
    decimal? HorizonClose, string State, string? Reason, decimal? PriceReturnPct,
    DateTimeOffset OutcomeKnownAt, DateTimeOffset RecordedAt);
public sealed record ResearchCell(ResearchObservation Observation, ResearchCohort Cohort, string Partition,
    ResearchOutcome? Outcome)
{
    public string Resolution => Outcome is null ? "UNRESOLVED" : "TERMINAL";
    public string? ResearchReason => Outcome is null ? "NO_COMMITTED_OUTCOME_AS_OF_CUTOFF" : null;
}
public sealed record ResearchCounts(int N, int A, int T, int R, int U, int TAnchor, int TData, int TBasis, int P, int Z, int M);
public sealed record ResearchMetrics(decimal? Median, decimal? Mean, decimal? Minimum, decimal? Maximum,
    decimal? PositiveProportion, decimal? AvailableCoverage);
public sealed record ResearchDiversity(int Runs, int Instruments, int TargetSessions, int NullTargetObservations,
    int EpisodeLinkedObservations, int NoEpisodeObservations, int EpisodeKeys, int ExtraEpisodeCaptures,
    int AdditionalSessionCaptures);
public sealed record ResearchWarning(string Code, string Text);
public sealed record ResearchSummary(ResearchCounts Counts, ResearchMetrics Metrics, ResearchDiversity Diversity,
    ResearchDiversity AvailableDiversity, IReadOnlyList<ResearchWarning> Warnings);
public sealed record ResearchCohortCount(ResearchCohort Cohort, int N);
public sealed record ResearchGroupSummary(ResearchCohort Cohort, string Partition, ResearchSummary Summary);
public sealed record ResearchResult(ResearchQuery Query, IReadOnlyList<ResearchRun> BaseRuns,
    int BaseObservationCount, int FilteredOutObservationCount, int EmptyRunCount,
    IReadOnlyList<ResearchCohortCount> BaseCohortCounts, ResearchSummary Summary,
    IReadOnlyList<ResearchGroupSummary> Cohorts, IReadOnlyList<ResearchCell> Observations)
{
    public string PolicyId { get; } = ResearchEvaluation.PolicyId;
    public int SchemaVersion { get; } = 1;
}

public static class ResearchEvaluation
{
    public const string PolicyId = "research-evaluation-v0.1.0";
    public const int MaximumRuns = 1000;
    public const int MaximumObservations = 10000;
    private static readonly ResearchCohort[] Primary = Enum.GetValues<ResearchCohort>().Where(c => c != ResearchCohort.ALL).ToArray();
    internal static void Require(bool valid, string reason = "RESEARCH_INPUT_INVALID")
    { if (!valid) throw new InvalidOperationException(reason); }
    public static string? DecimalText(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture);

    internal static bool ValidEpisodeKey(string key)
    {
        if (key.Length > 128) return false;
        var p = key.Split('/');
        return p.Length == 3 && p[0] == ScreenerReadRequest.PolicyId
            && Guid.TryParseExact(p[1], "D", out var id) && id != Guid.Empty && p[1] == id.ToString("D")
            && DateOnly.TryParseExact(p[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && date >= ScreenerReadRequest.Anchor && date <= ScreenerReadRequest.Horizon;
    }

    public static ResearchCohort Classify(ResearchObservation row)
    {
        Require(row.Eligibility is "ELIGIBLE" or "DATA_BLOCKED" or "INELIGIBLE"
            && row.Setup is "NONE" or "WATCH" or "CONFIRMED" or "FAILED"
            && (row.SetupEvaluated ? row.Eligibility == "ELIGIBLE" : row.Setup == "NONE"));
        if (row.Eligibility == "DATA_BLOCKED") return ResearchCohort.DATA_BLOCKED;
        if (row.Eligibility == "INELIGIBLE") return ResearchCohort.INELIGIBLE;
        if (!row.SetupEvaluated) return ResearchCohort.NOT_EVALUATED;
        return row.Setup switch { "NONE" => ResearchCohort.NONE, "WATCH" => ResearchCohort.WATCH,
            "CONFIRMED" => ResearchCohort.CONFIRMED, _ => ResearchCohort.FAILED };
    }

    public static IReadOnlyList<string> Partitions(ResearchGrouping grouping) => grouping switch
    {
        ResearchGrouping.NONE => ["NONE"],
        ResearchGrouping.MARKET_TREND => ["POSITIVE", "NEUTRAL", "NEGATIVE", "UNKNOWN"],
        ResearchGrouping.MARKET_VOLATILITY => ["NORMAL", "ELEVATED", "UNKNOWN"],
        ResearchGrouping.HELD_CONTEXT => ["HELD", "NOT_HELD", "NO_PORTFOLIO_CONTEXT"],
        ResearchGrouping.MEMBERSHIP => ["CONFIGURED", "OUTSIDE_CONFIGURED", "MEMBERSHIP_UNKNOWN"],
        _ => throw new ArgumentException("RESEARCH_QUERY_INVALID")
    };
    public static string Partition(ResearchGrouping grouping, ResearchRun run, ResearchObservation row) => grouping switch
    {
        ResearchGrouping.NONE => "NONE",
        ResearchGrouping.MARKET_TREND => run.MarketTrend,
        ResearchGrouping.MARKET_VOLATILITY => run.MarketVolatility,
        ResearchGrouping.HELD_CONTEXT => run.Header.PortfolioId is null ? "NO_PORTFOLIO_CONTEXT" : row.Held ? "HELD" : "NOT_HELD",
        ResearchGrouping.MEMBERSHIP => row.Configured ? "CONFIGURED" : run.Header.UniverseSnapshotId is null ? "MEMBERSHIP_UNKNOWN" : "OUTSIDE_CONFIGURED",
        _ => throw new ArgumentException("RESEARCH_QUERY_INVALID")
    };

    private static void Validate(ResearchRun run, ResearchObservation row)
    {
        var h = run.Header;
        Require(row.InstrumentId != Guid.Empty && row.RunId == h.RunId && (h.PortfolioId is not null || !row.Held)
            && (row.MarketDate is null || row.MarketDate <= h.Through) && (row.Close is null || row.Close > 0)
            && row.EligibilityReasons is not null && row.SetupReasons is not null
            && row.EligibilityReasons.All(s => !string.IsNullOrWhiteSpace(s)) && row.SetupReasons.All(s => !string.IsNullOrWhiteSpace(s)));
        Classify(row);
        var e = row.Episode;
        Require(e is not null || row.Setup is "NONE");
        if (e is null) return;
        Require(ValidEpisodeKey(e.Id) && e.Id == h.PolicyId + "/" + row.InstrumentId.ToString("D") + "/" + e.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            && e.StartDate <= h.Through && e.AgeSessions > 0
            && (e.ConfirmationDate is null || e.ConfirmationDate >= e.StartDate && e.ConfirmationDate <= h.Through)
            && (e.EndDate is null ? e.EndReason is null : !string.IsNullOrWhiteSpace(e.EndReason)
                && e.EndDate >= (e.ConfirmationDate ?? e.StartDate) && e.EndDate <= h.Through)
            && (e.ConfirmationDate is null ? e.ConfirmedAgeSessions is null && e.TriggerPrice is null
                : e.ConfirmedAgeSessions > 0 && e.TriggerPrice > 0)
            && (row.Setup switch { "WATCH" => e.EndDate is null && e.ConfirmationDate is null && e.WatchThreshold > 0,
                "CONFIRMED" => e.EndDate is null && e.ConfirmationDate is not null,
                "FAILED" => e.EndDate is not null && e.ConfirmationDate is not null, _ => e.EndDate is not null }));
    }

    private static void Validate(ResearchOutcome outcome, ResearchRun run, ResearchObservation row)
    {
        Require(outcome.SchemaVersion == 1 && outcome.OutcomePolicyId == "outcome-v0.1.0", "RESEARCH_POLICY_VERSION_UNAVAILABLE");
        Require(run.Header.TargetSession is not null && outcome.AnchorMarketDate == row.MarketDate && outcome.AnchorClose == row.Close
            && outcome.HorizonMarketDate > run.Header.TargetSession && outcome.HorizonMarketDate <= OutcomeEvaluator.Through(outcome.OutcomeKnownAt)
            && outcome.HorizonMarketDate <= ScreenerReadRequest.Horizon
            && outcome.OutcomeKnownAt >= run.Header.CapturedAt && outcome.RecordedAt >= outcome.OutcomeKnownAt
            && (outcome.HorizonClose is null || outcome.HorizonClose > 0)
            && (outcome.State == "AVAILABLE" ? outcome.PriceReturnPct is not null && outcome.Reason is null
                && outcome.AnchorClose > 0 && outcome.HorizonClose > 0 && outcome.AnchorMarketDate == run.Header.TargetSession
                : outcome.State is "ANCHOR_UNAVAILABLE" or "DATA_UNAVAILABLE" or "BASIS_UNCERTAIN"
                    && outcome.PriceReturnPct is null && !string.IsNullOrWhiteSpace(outcome.Reason) && outcome.Reason.Length <= 128));
    }

    public static ResearchResult Evaluate(ResearchQuery query, IReadOnlyList<ResearchRun> runs,
        IReadOnlyList<ResearchObservation> observations, IReadOnlyList<ResearchOutcome> outcomes,
        DateTimeOffset now, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        query = query.Normalize(now);
        var selectedRuns = runs.Where(r => r.Header.PortfolioId == query.PortfolioId
            && OutcomeEvaluator.Through(r.Header.CapturedAt) >= query.CaptureFrom && OutcomeEvaluator.Through(r.Header.CapturedAt) <= query.CaptureTo
            && r.Header.CapturedAt <= query.Cutoff && r.Header.KnowledgeCutoff <= query.Cutoff && r.Header.RecordedAt <= query.Cutoff)
            .Take(MaximumRuns + 1).OrderBy(r => r.Header.CapturedAt).ThenBy(r => r.Header.RunId.ToString("D"), StringComparer.Ordinal).ToArray();
        Require(selectedRuns.Length <= MaximumRuns, "RESEARCH_BOUND_EXCEEDED");
        Require(selectedRuns.Select(r => r.Header.RunId).Distinct().Count() == selectedRuns.Length);
        var byRun = selectedRuns.ToDictionary(r => r.Header.RunId);
        foreach (var run in selectedRuns)
        {
            ct.ThrowIfCancellationRequested(); var h = run.Header;
            Require(h.SchemaVersion == 1 && h.PolicyId == "screener-v0.1.0" && h.CaptureKind == "PROSPECTIVE_CAPTURE"
                && h.Universe == "PILOT", "RESEARCH_POLICY_VERSION_UNAVAILABLE");
            Require(h.RunId != Guid.Empty && h.PortfolioId != Guid.Empty && h.CapturedAt == h.KnowledgeCutoff && h.RecordedAt >= h.CapturedAt
                && h.HistoryAnchor == ScreenerReadRequest.Anchor && h.Through == OutcomeEvaluator.Through(h.CapturedAt)
                && (h.TargetSession is null || h.TargetSession >= h.HistoryAnchor && h.TargetSession <= h.Through)
                && h.Status is "COMPLETE" or "PARTIAL" or "BLOCKED" && h.RowCount is >= 0 and <= 210
                && ScreenerReferences.IsHash(h.InputHash) && ScreenerReferences.IsHash(h.SelectedDigest)
                && Partitions(ResearchGrouping.MARKET_TREND).Contains(run.MarketTrend)
                && Partitions(ResearchGrouping.MARKET_VOLATILITY).Contains(run.MarketVolatility)
                && (run.MarketContextDate is null || run.MarketContextDate <= h.Through)
                && run.MarketContextReasons is not null && run.MarketContextReasons.All(s => !string.IsNullOrWhiteSpace(s)));
        }
        var rows = observations.Where(r => byRun.ContainsKey(r.RunId)).Take(MaximumObservations + 1)
            .OrderBy(r => byRun[r.RunId].Header.CapturedAt).ThenBy(r => r.RunId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(r => r.InstrumentId.ToString("D"), StringComparer.Ordinal).ToArray();
        Require(rows.Length <= MaximumObservations, "RESEARCH_BOUND_EXCEEDED");
        Require(rows.Select(r => (r.RunId, r.InstrumentId)).Distinct().Count() == rows.Length);
        var rowCounts = rows.GroupBy(r => r.RunId).ToDictionary(g => g.Key, g => g.Count());
        Require(selectedRuns.All(r => rowCounts.GetValueOrDefault(r.Header.RunId) == r.Header.RowCount));
        var byRow = rows.ToDictionary(r => (r.RunId, r.InstrumentId));
        foreach (var row in rows) { ct.ThrowIfCancellationRequested(); Validate(byRun[row.RunId], row); }
        var visible = outcomes.Where(o => o.HorizonSessions == query.HorizonSessions && byRow.ContainsKey((o.RunId, o.InstrumentId))
            && o.OutcomeKnownAt <= query.Cutoff && o.RecordedAt <= query.Cutoff).ToArray();
        Require(visible.Select(o => (o.RunId, o.InstrumentId)).Distinct().Count() == visible.Length);
        foreach (var outcome in visible)
        { ct.ThrowIfCancellationRequested(); Validate(outcome, byRun[outcome.RunId], byRow[(outcome.RunId, outcome.InstrumentId)]); }
        var committed = visible.ToDictionary(o => (o.RunId, o.InstrumentId));
        var cells = rows.Select(row => new ResearchCell(row with { EligibilityReasons = row.EligibilityReasons.ToArray(), SetupReasons = row.SetupReasons.ToArray() },
                Classify(row), Partition(query.GroupBy, byRun[row.RunId], row), committed.GetValueOrDefault((row.RunId, row.InstrumentId))))
            .Where(c => (query.Cohort == ResearchCohort.ALL || c.Cohort == query.Cohort)
                && (query.InstrumentId is null || c.Observation.InstrumentId == query.InstrumentId)
                && (query.EpisodeId is null || c.Observation.Episode?.Id == query.EpisodeId)).ToArray();
        var summary = Summarize(cells, byRun, ct);
        var groups = Primary.SelectMany(cohort => Partitions(query.GroupBy).Select(partition => new ResearchGroupSummary(cohort, partition,
            Summarize(cells.Where(c => c.Cohort == cohort && c.Partition == partition).ToArray(), byRun, ct)))).ToArray();
        Require(groups.Sum(g => g.Summary.Counts.N) == summary.Counts.N);
        return new(query, selectedRuns.Select(r => r with { MarketContextReasons = r.MarketContextReasons.ToArray() }).ToArray(), rows.Length,
            rows.Length - cells.Length, selectedRuns.Count(r => r.Header.RowCount == 0),
            Primary.Select(c => new ResearchCohortCount(c, rows.Count(r => Classify(r) == c))).ToArray(), summary, groups, cells);
    }

    private static ResearchSummary Summarize(ResearchCell[] cells, IReadOnlyDictionary<Guid, ResearchRun> runs, CancellationToken ct)
    {
        var available = cells.Where(c => c.Outcome?.State == "AVAILABLE").ToArray();
        var values = available.Select(c => c.Outcome!.PriceReturnPct!.Value).ToArray();
        var anchor = cells.Count(c => c.Outcome?.State == "ANCHOR_UNAVAILABLE");
        var data = cells.Count(c => c.Outcome?.State == "DATA_UNAVAILABLE");
        var basis = cells.Count(c => c.Outcome?.State == "BASIS_UNCERTAIN");
        var a = values.Length; var t = anchor + data + basis; var n = cells.Length;
        var counts = new ResearchCounts(n, a, t, a + t, n - a - t, anchor, data, basis,
            values.Count(x => x > 0), values.Count(x => x == 0), values.Count(x => x < 0));
        Require(counts.N == counts.A + counts.T + counts.U && counts.N == counts.R + counts.U
            && counts.R == counts.A + counts.T && counts.T == counts.TAnchor + counts.TData + counts.TBasis
            && counts.A == counts.P + counts.Z + counts.M);
        decimal sum = 0;
        foreach (var value in values) { ct.ThrowIfCancellationRequested(); sum = checked(sum + value); }
        var sorted = values.Order().ToArray();
        var metrics = new ResearchMetrics(a == 0 ? null : a % 2 == 1 ? sorted[a / 2] : checked(sorted[a / 2 - 1] + sorted[a / 2]) / 2m,
            a == 0 ? null : sum / a, a == 0 ? null : sorted[0], a == 0 ? null : sorted[^1],
            a == 0 ? null : 100m * counts.P / a, n == 0 ? null : 100m * a / n);
        var diversity = Diversity(cells, runs);
        return new(counts, metrics, diversity, Diversity(available, runs), Warnings(counts, metrics, diversity));
    }

    private static ResearchDiversity Diversity(ResearchCell[] cells, IReadOnlyDictionary<Guid, ResearchRun> runs)
    {
        var linked = cells.Where(c => c.Observation.Episode is not null).ToArray();
        var episodes = linked.Select(c => (runs[c.Observation.RunId].Header.PolicyId, c.Observation.InstrumentId, c.Observation.Episode!.Id)).Distinct().Count();
        var dated = cells.Where(c => runs[c.Observation.RunId].Header.TargetSession is not null).ToArray();
        return new(cells.Select(c => c.Observation.RunId).Distinct().Count(), cells.Select(c => c.Observation.InstrumentId).Distinct().Count(),
            dated.Select(c => runs[c.Observation.RunId].Header.TargetSession).Distinct().Count(), cells.Length - dated.Length,
            linked.Length, cells.Length - linked.Length, episodes, linked.Length - episodes,
            dated.Length - dated.Select(c => (c.Observation.InstrumentId, runs[c.Observation.RunId].Header.TargetSession)).Distinct().Count());
    }

    private static ResearchWarning[] Warnings(ResearchCounts c, ResearchMetrics metrics, ResearchDiversity d)
    {
        var warnings = new List<ResearchWarning>
        {
            new("PROSPECTIVE_OBSERVATIONS", "Prospective observations, not trades. Exploratory price-return summaries do not establish predictive performance."),
            new("DEPENDENCE", "Captures, instruments and forward windows may be correlated."),
            new("PRICE_ONLY", "Price return excludes dividend cash and execution costs; it is not total return or realized portfolio performance.")
        };
        var sample = c.N == 0 ? new ResearchWarning("NO_OBSERVATIONS", "No captured observations selected.")
            : c.A == 0 ? new("NO_AVAILABLE_OUTCOMES", "No committed AVAILABLE outcomes at this cutoff.")
            : c.A < 5 ? new("VERY_SMALL_SAMPLE", FormattableString.Invariant($"Very small observed return sample (A={c.A})."))
            : c.A < 20 ? new("SMALL_SAMPLE", FormattableString.Invariant($"Small observed return sample (A={c.A})."))
            : c.A < 30 ? new("LIMITED_SAMPLE", FormattableString.Invariant($"Limited observed return sample (A={c.A})."))
            : new("DESCRIPTIVE_SAMPLE", "Descriptive sample; independence and predictive validity are not established.");
        warnings.Add(sample);
        if (d.ExtraEpisodeCaptures > 0) warnings.Add(new("REPEATED_EPISODES", FormattableString.Invariant($"{d.EpisodeLinkedObservations} episode-linked observations represent {d.EpisodeKeys} distinct captured episode keys; repeated captures receive repeated weight.")));
        if (c.U > 0) warnings.Add(new("UNRESOLVED", FormattableString.Invariant($"{c.U} of {c.N} observations have no committed outcome visible at this cutoff; market maturity, evidence gaps and unrecorded assessment are not distinguished.")));
        if (c.A < c.N) warnings.Add(new("SELECTIVE_AVAILABILITY", FormattableString.Invariant($"Returns describe {c.A} of {c.N} observations ({DecimalText(metrics.AvailableCoverage)}% AVAILABLE). Missing outcomes may be selective.")));
        // The obsolete frozen notice that Outcome Verification is unimplemented needs a
        // separately reviewed factual correction. Do not emit a false notice or invent verdicts.
        return warnings.ToArray();
    }
}
