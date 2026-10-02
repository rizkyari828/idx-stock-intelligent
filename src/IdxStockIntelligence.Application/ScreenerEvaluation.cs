using System.Globalization;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record ScreenerEligibility(EligibilityStatus Status, IReadOnlyList<string> Reasons)
{
    private static readonly string[] Priority = ["IDENTITY_CONFLICT", "REFERENCE_NOT_KNOWN", "TYPE_UNKNOWN",
        "UNSUPPORTED_TYPE", "PRE_LISTING", "POST_DELISTING", "UNSUPPORTED_BOARD", "SUSPENDED", "NO_TRADE",
        "LISTING_UNKNOWN", "BOARD_UNKNOWN", "STATUS_UNKNOWN", "SESSION_UNCONFIRMED", "PRICE_BASIS_UNVERIFIED",
        "CANONICAL_INVALID", "NUMERIC_OUT_OF_RANGE", "MISSING_CURRENT_BAR", "STALE", "INSUFFICIENT_HISTORY",
        "ZERO_VOLUME_UNEXPLAINED"];

    public static ScreenerEligibility Decide(IEnumerable<string> reasons)
    {
        var ordered = reasons.Distinct(StringComparer.Ordinal).OrderBy(r =>
        { var i = Array.IndexOf(Priority, r); return i < 0 ? int.MaxValue : i; }).ThenBy(r => r, StringComparer.Ordinal).ToArray();
        var status = ordered.Any(r => Priority.Take(3).Contains(r)) ? EligibilityStatus.DataBlocked
            : ordered.Any(r => Priority.Skip(3).Take(6).Contains(r)) ? EligibilityStatus.Ineligible
            : ordered.Length > 0 ? EligibilityStatus.DataBlocked : EligibilityStatus.Eligible;
        return new(status, ordered);
    }
}

public sealed record ScreenerEpisode(string Id, DateOnly StartDate, DateOnly? ConfirmationDate, DateOnly? EndDate,
    string? EndReason, int AgeSessions, int? ConfirmedAgeSessions, decimal? TriggerPrice, decimal? WatchThreshold);
public sealed record ScreenerSetup(SetupStatus Status, bool Evaluated, IReadOnlyList<string> Reasons, ScreenerEpisode? Episode)
{
    public static ScreenerSetup Empty { get; } = new(SetupStatus.None, false, ["NOT_EVALUATED"], null);
}

public static class ScreenerEpisodes
{
    // Prepared price evidence only: no feature mathematics, database or request clock here.
    public static ScreenerSetup Advance(Guid id, DateOnly date, ScreenerSetup previous,
        EligibilityStatus eligibility, decimal? close, decimal? priorHigh20)
    {
        if (eligibility != EligibilityStatus.Eligible || close is null || priorHigh20 is null)
        {
            var ended = previous.Episode is { EndDate: null } episode
                ? episode with { EndDate = date, EndReason = eligibility == EligibilityStatus.Ineligible ? "INELIGIBLE" : "DATA_INTERRUPTED", WatchThreshold = null }
                : null;
            return new(SetupStatus.None, false, ["NOT_EVALUATED"], ended);
        }
        if (close <= 0 || priorHigh20 <= 0 || id == Guid.Empty) throw new ArgumentException("Valid prepared price evidence required.");
        var active = previous.Episode is { EndDate: null } ? previous.Episode : null;
        if (active is not null && date <= active.StartDate) throw new ArgumentException("Sessions must advance in date order.");
        ScreenerSetup End(string reason, SetupStatus state, bool advanceAge) => new(state, true, [reason], active! with
        {
            EndDate = date, EndReason = reason, WatchThreshold = null,
            AgeSessions = active!.AgeSessions + (advanceAge ? 1 : 0),
            ConfirmedAgeSessions = active.ConfirmedAgeSessions + (advanceAge ? 1 : 0)
        });
        if (previous.Status == SetupStatus.Watch && active is not null)
        {
            if (active.AgeSessions >= 5) return End("WATCH_EXPIRED", SetupStatus.None, false);
            if (close > priorHigh20) return new(SetupStatus.Confirmed, true, ["CLOSE_ABOVE_PRIOR_HIGH"], active with
            { AgeSessions = active.AgeSessions + 1, ConfirmationDate = date, ConfirmedAgeSessions = 1, TriggerPrice = priorHigh20, WatchThreshold = null });
            if (close >= 0.98m * priorHigh20) return new(SetupStatus.Watch, true, ["WITHIN_WATCH_BAND"], active with
            { AgeSessions = active.AgeSessions + 1, WatchThreshold = priorHigh20 });
            return End("WATCH_LEFT_BAND", SetupStatus.None, true);
        }
        if (previous.Status == SetupStatus.Confirmed && active is not null)
        {
            if (active.ConfirmedAgeSessions >= 20) return End("CONFIRMED_EXPIRED", SetupStatus.None, false);
            if (close < active.TriggerPrice) return End("CLOSE_BELOW_TRIGGER", SetupStatus.Failed, true);
            return new(SetupStatus.Confirmed, true, ["AT_OR_ABOVE_TRIGGER"], active with
            { AgeSessions = active.AgeSessions + 1, ConfirmedAgeSessions = active.ConfirmedAgeSessions + 1 });
        }
        var confirmed = close > priorHigh20;
        if (!confirmed && close < 0.98m * priorHigh20) return new(SetupStatus.None, true, ["NO_SETUP"], null);
        var key = $"{ScreenerReadRequest.PolicyId}/{id.ToString("D").ToLowerInvariant()}/{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        return new(confirmed ? SetupStatus.Confirmed : SetupStatus.Watch, true,
            [confirmed ? "CLOSE_ABOVE_PRIOR_HIGH" : "WITHIN_WATCH_BAND"],
            new(key, date, confirmed ? date : null, null, null, 1, confirmed ? 1 : null,
                confirmed ? priorHigh20 : null, confirmed ? null : priorHigh20));
    }
}

public enum ScreenerQuality { Complete, Partial, Blocked }
public sealed record ScreenerProvenance(ScreenerBarEvidence? Observation, DateOnly? PriceSequenceStart,
    DateOnly? EmaSeedStart, DateOnly? AtrSeedStart, int ConsecutiveSessions, string? ReferenceSnapshotId)
{
    public ScreenerBarEvidence? CurrentEvidence { get; init; }
}
public sealed record ScreenerRow(Guid InstrumentId, string? Symbol, string? DisplayName, bool Configured, bool Held,
    Mandate? Mandate, int? DiscoveryRank, ScreenerEligibility Eligibility, ScreenerSetup Setup,
    DateOnly? MarketDate, decimal? Close, long? Volume, bool? Stale, bool? NoTrade, string? TradingStatus,
    string Trend, ScreenerQuality DataQuality, IReadOnlyList<string> DataReasons,
    IReadOnlyDictionary<string, FeatureState> Fields, ScreenerProvenance Provenance)
{
    public decimal? Rs60Pp => Fields.GetValueOrDefault("rs60Pp") is { Availability: Availability.AVAILABLE } state ? state.Value : null;
    public decimal? Rs20Pp => Fields.GetValueOrDefault("rs20Pp") is { Availability: Availability.AVAILABLE } state ? state.Value : null;
    public decimal? MonetaryLiquidity20Idr => Fields.GetValueOrDefault("monetaryLiquidity20Idr") is { Availability: Availability.AVAILABLE } state ? state.Value : null;
}
public sealed record ScreenerMarketContext(Guid InstrumentId, DateOnly? MarketDate, string Trend, string Volatility,
    IReadOnlyDictionary<string, FeatureState> Fields, IReadOnlyList<string> Reasons, ScreenerProvenance Provenance);
public sealed record ScreenerSummary(int? Configured, int? Eligible, int? Ineligible, int? DataBlocked, int? Evaluated,
    int? Candidates, int? Confirmed, int? Watch, int? Shortlisted, int? OmittedConfirmed, int? OmittedWatch,
    int? InsufficientHistory, int? Stale, int? Unsupported, int Held, int? HeldOutsideUniverse);
public sealed record ScreenerResult(DateOnly Through, DateOnly? TargetSession, DateTimeOffset Cutoff,
    string? UniverseSnapshotId, ScreenerQuality Status, IReadOnlyList<string> Reasons, ScreenerSummary Summary,
    ScreenerMarketContext MarketContext, IReadOnlyList<ScreenerRow> Rows, IReadOnlyList<Guid> RankedCandidateIds,
    IReadOnlyList<Guid> AllViewIds, IReadOnlyList<Guid> ShortlistIds, IReadOnlyList<Guid> HeldIds)
{
    public string PolicyId { get; } = ScreenerReadRequest.PolicyId;
    public DateOnly HistoryAnchor { get; } = ScreenerReadRequest.Anchor;
}
public sealed record ScreenerPortfolioHistory(Portfolio Portfolio, IReadOnlyList<PortfolioEvent> Events,
    IReadOnlyList<ThesisVersion> Theses);

public static class ScreenerOrdering
{
    private static string Id(Guid id) => id.ToString("D").ToLowerInvariant();
    private static int State(SetupStatus state) => state switch
    { SetupStatus.Confirmed => 0, SetupStatus.Watch => 1, SetupStatus.Failed => 2, _ => 3 };
    private static IOrderedEnumerable<ScreenerRow> Order(IEnumerable<ScreenerRow> rows) => rows.OrderBy(r => State(r.Setup.Status))
        .ThenByDescending(r => r.Rs60Pp).ThenByDescending(r => r.Rs20Pp).ThenByDescending(r => r.MonetaryLiquidity20Idr)
        .ThenBy(r => r.Symbol is null).ThenBy(r => r.Symbol?.ToUpperInvariant(), StringComparer.Ordinal)
        .ThenBy(r => Id(r.InstrumentId), StringComparer.Ordinal);

    // Also accepts >20 test rows; production membership bounds remain in the reference/read boundary.
    public static ScreenerResult Assemble(DateOnly through, DateOnly? target, DateTimeOffset cutoff,
        string? universeSnapshotId, bool universeKnown, bool calendarKnown, ScreenerMarketContext context,
        IReadOnlyList<ScreenerRow> rows, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (rows.Select(r => r.InstrumentId).Distinct().Count() != rows.Count) throw new ArgumentException("Duplicate logical row.");
        var configured = rows.Where(r => r.Configured).ToArray();
        var candidates = Order(configured.Where(r => r.Eligibility.Status == EligibilityStatus.Eligible && r.Setup.Evaluated
            && r.Setup.Status is SetupStatus.Confirmed or SetupStatus.Watch)).ToArray();
        var ranks = candidates.Select((r, i) => (r.InstrumentId, Rank: i + 1)).ToDictionary(x => x.InstrumentId, x => x.Rank);
        var rankedRows = new List<ScreenerRow>();
        foreach (var row in rows)
        { ct.ThrowIfCancellationRequested(); rankedRows.Add(row with { DiscoveryRank = ranks.TryGetValue(row.InstrumentId, out var rank) ? rank : null }); }
        var shortlist = candidates.Take(20).ToArray();
        var omitted = candidates.Skip(20).ToArray();
        int? Count(Func<ScreenerRow, bool> predicate) => universeKnown ? configured.Count(predicate) : null;
        var summary = new ScreenerSummary(universeKnown ? configured.Length : null,
            Count(r => r.Eligibility.Status == EligibilityStatus.Eligible), Count(r => r.Eligibility.Status == EligibilityStatus.Ineligible),
            Count(r => r.Eligibility.Status == EligibilityStatus.DataBlocked), Count(r => r.Setup.Evaluated),
            universeKnown ? candidates.Length : null, Count(r => ranks.ContainsKey(r.InstrumentId) && r.Setup.Status == SetupStatus.Confirmed),
            Count(r => ranks.ContainsKey(r.InstrumentId) && r.Setup.Status == SetupStatus.Watch), universeKnown ? shortlist.Length : null,
            universeKnown ? omitted.Count(r => r.Setup.Status == SetupStatus.Confirmed) : null,
            universeKnown ? omitted.Count(r => r.Setup.Status == SetupStatus.Watch) : null,
            Count(r => r.Eligibility.Reasons.Contains("INSUFFICIENT_HISTORY")), Count(r => r.Stale == true),
            Count(r => r.Eligibility.Reasons.Any(s => s is "UNSUPPORTED_TYPE" or "UNSUPPORTED_BOARD")),
            rows.Count(r => r.Held), universeKnown ? rows.Count(r => r.Held && !r.Configured) : null);
        var completeExclusions = universeKnown && configured.All(r => r.Eligibility.Status == EligibilityStatus.Ineligible);
        var blocked = !universeKnown || !calendarKnown || !completeExclusions && !configured.Any(r => r.Setup.Evaluated);
        var incomplete = rows.Any(r => r.DataQuality != ScreenerQuality.Complete)
            || (!completeExclusions || rows.Any(r => r.Setup.Evaluated)) && context.Reasons.Count > 0;
        var status = blocked ? ScreenerQuality.Blocked : !incomplete ? ScreenerQuality.Complete : ScreenerQuality.Partial;
        var held = rankedRows.Where(r => r.Held).OrderBy(r => r.Symbol is null)
            .ThenBy(r => r.Symbol?.ToUpperInvariant(), StringComparer.Ordinal).ThenBy(r => Id(r.InstrumentId), StringComparer.Ordinal);
        return new(through, target, cutoff.ToUniversalTime(), universeSnapshotId, status,
            !universeKnown ? ["UNIVERSE_NOT_KNOWN", "RAW_PRICE_RETURNS"] : !calendarKnown ? ["SESSION_UNCONFIRMED", "RAW_PRICE_RETURNS"]
                : status == ScreenerQuality.Complete ? ["RAW_PRICE_RETURNS"] : ["INCOMPLETE_COVERAGE", "RAW_PRICE_RETURNS"],
            summary, context, rankedRows.OrderBy(r => Id(r.InstrumentId), StringComparer.Ordinal).ToArray(),
            candidates.Select(r => r.InstrumentId).ToArray(), Order(rankedRows.Where(r => r.Configured)).Select(r => r.InstrumentId).ToArray(),
            shortlist.Select(r => r.InstrumentId).ToArray(), held.Select(r => r.InstrumentId).ToArray());
    }
}
