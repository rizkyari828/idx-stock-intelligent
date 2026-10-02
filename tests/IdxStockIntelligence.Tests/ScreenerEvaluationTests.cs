using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvaluationTests
{
    private static readonly Guid Id = Guid.Parse("abcdef01-0000-4000-8000-000000000001");
    private static readonly DateOnly Start = new(2026, 9, 21);
    private static ScreenerSetup Step(ScreenerSetup previous, int day, decimal close = 99m, decimal high = 100m,
        EligibilityStatus status = EligibilityStatus.Eligible) => ScreenerEpisodes.Advance(Id, Start.AddDays(day), previous, status, close, high);
    private static ScreenerSetup Run(int count, bool confirmed = false)
    {
        var value = ScreenerSetup.Empty;
        for (var i = 0; i < count; i++) value = Step(value, i, confirmed ? 101 : 99);
        return value;
    }

    [Theory]
    [InlineData("97.99", SetupStatus.None)]
    [InlineData("98.00", SetupStatus.Watch)]
    [InlineData("100.00", SetupStatus.Watch)]
    [InlineData("100.01", SetupStatus.Confirmed)]
    [InlineData("99", SetupStatus.Watch)]
    [InlineData("97", SetupStatus.None)]
    public void ExactCloseBoundaries(string price, SetupStatus expected)
    {
        var result = Step(ScreenerSetup.Empty, 0, decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(expected, result.Status); Assert.True(result.Evaluated);
        Assert.Equal(expected == SetupStatus.None ? "NO_SETUP" : expected == SetupStatus.Watch ? "WITHIN_WATCH_BAND" : "CLOSE_ABOVE_PRIOR_HIGH", Assert.Single(result.Reasons));
        if (expected == SetupStatus.None) Assert.Null(result.Episode);
        else { Assert.Equal(1, result.Episode!.AgeSessions); Assert.Equal(expected == SetupStatus.Confirmed ? 1 : (int?)null, result.Episode.ConfirmedAgeSessions); }
    }

    [Fact]
    public void WatchThresholdRollsWithoutResetAndConfirmationFreezesSameEpisode()
    {
        var first = Run(1);
        var second = Step(first, 1, 100, 102);
        Assert.Equal(first.Episode!.Id, second.Episode!.Id);
        Assert.Equal(2, second.Episode.AgeSessions); Assert.Equal(102m, second.Episode.WatchThreshold);
        Assert.Null(second.Episode.TriggerPrice); Assert.Null(second.Episode.ConfirmationDate);
        var confirm = Step(second, 2, 106, 105);
        Assert.Equal(SetupStatus.Confirmed, confirm.Status); Assert.Equal(first.Episode.Id, confirm.Episode!.Id);
        Assert.Equal(3, confirm.Episode.AgeSessions); Assert.Equal(1, confirm.Episode.ConfirmedAgeSessions);
        Assert.Equal(Start.AddDays(2), confirm.Episode.ConfirmationDate); Assert.Equal(105m, confirm.Episode.TriggerPrice);
        Assert.Null(confirm.Episode.WatchThreshold);
        var later = Step(confirm, 3, 110, 108);
        Assert.Equal(confirm.Episode.Id, later.Episode!.Id); Assert.Equal(confirm.Episode.TriggerPrice, later.Episode.TriggerPrice);
        Assert.Equal(confirm.Episode.ConfirmationDate, later.Episode.ConfirmationDate); Assert.Equal(2, later.Episode.ConfirmedAgeSessions);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(101)]
    public void WatchSixthSessionExpiresBeforeAnyPriceTest(int close)
    {
        var fifth = Run(5);
        Assert.Equal(SetupStatus.Watch, fifth.Status); Assert.Equal(5, fifth.Episode!.AgeSessions);
        var expired = Step(fifth, 5, close);
        Assert.Equal(SetupStatus.None, expired.Status); Assert.True(expired.Evaluated);
        Assert.Equal("WATCH_EXPIRED", Assert.Single(expired.Reasons)); Assert.Equal(5, expired.Episode!.AgeSessions);
        Assert.Equal(Start.AddDays(5), expired.Episode.EndDate); Assert.Equal("WATCH_EXPIRED", expired.Episode.EndReason);
        var next = Step(expired, 6, 101);
        Assert.Equal(SetupStatus.Confirmed, next.Status); Assert.NotEqual(fifth.Episode.Id, next.Episode!.Id);
        Assert.Equal(1, next.Episode.AgeSessions);
    }

    [Fact]
    public void WatchLeavesBandEndsAndCannotRestartOnExitSession()
    {
        var first = Run(1); var exit = Step(first, 1, 97);
        Assert.Equal(SetupStatus.None, exit.Status); Assert.Equal("WATCH_LEFT_BAND", Assert.Single(exit.Reasons));
        Assert.Equal(first.Episode!.Id, exit.Episode!.Id); Assert.Equal(2, exit.Episode.AgeSessions);
        Assert.Equal(Start.AddDays(1), exit.Episode.EndDate);
        Assert.NotEqual(exit.Episode.Id, Step(exit, 2).Episode!.Id);
    }

    [Theory]
    [InlineData("100", SetupStatus.Confirmed)]
    [InlineData("99.99", SetupStatus.Failed)]
    public void ConfirmedEqualityAndFailureUseFrozenTrigger(string close, SetupStatus expected)
    {
        var first = Run(1, true); var result = Step(first, 1, decimal.Parse(close, System.Globalization.CultureInfo.InvariantCulture), 500);
        Assert.Equal(expected, result.Status); Assert.Equal(100m, result.Episode!.TriggerPrice);
        Assert.Equal(first.Episode!.Id, result.Episode.Id); Assert.Equal(2, result.Episode.AgeSessions);
        Assert.Equal(2, result.Episode.ConfirmedAgeSessions);
        if (expected == SetupStatus.Failed) { Assert.Equal("CLOSE_BELOW_TRIGGER", result.Episode.EndReason); Assert.Equal(Start.AddDays(1), result.Episode.EndDate); }
        else Assert.Null(result.Episode.EndDate);
    }

    [Theory]
    [InlineData(97, SetupStatus.None)]
    [InlineData(99, SetupStatus.Watch)]
    [InlineData(101, SetupStatus.Confirmed)]
    public void FailedIsOneSessionThenUsesFreshNoneRules(int close, SetupStatus expected)
    {
        var failed = Step(Run(1, true), 1, 99);
        Assert.Equal(SetupStatus.Failed, failed.Status);
        var next = Step(failed, 2, close);
        Assert.Equal(expected, next.Status);
        if (next.Episode is not null) { Assert.NotEqual(failed.Episode!.Id, next.Episode.Id); Assert.Equal(1, next.Episode.AgeSessions); }
        else Assert.Null(next.Episode);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(200)]
    public void ConfirmedTwentyFirstSessionExpiresBeforeFailureOrRestart(int close)
    {
        var twentieth = Run(20, true);
        Assert.Equal(SetupStatus.Confirmed, twentieth.Status); Assert.Equal(20, twentieth.Episode!.ConfirmedAgeSessions);
        var expired = Step(twentieth, 20, close);
        Assert.Equal(SetupStatus.None, expired.Status); Assert.Equal("CONFIRMED_EXPIRED", Assert.Single(expired.Reasons));
        Assert.Equal(20, expired.Episode!.AgeSessions); Assert.Equal(20, expired.Episode.ConfirmedAgeSessions);
        Assert.Equal(Start.AddDays(20), expired.Episode.EndDate);
        Assert.NotEqual(expired.Episode.Id, Step(expired, 21, 101).Episode!.Id);
    }

    [Theory]
    [InlineData(false, EligibilityStatus.DataBlocked, "DATA_INTERRUPTED")]
    [InlineData(true, EligibilityStatus.DataBlocked, "DATA_INTERRUPTED")]
    [InlineData(false, EligibilityStatus.Ineligible, "INELIGIBLE")]
    [InlineData(true, EligibilityStatus.Ineligible, "INELIGIBLE")]
    public void BlockedOrIneligibleEndsActiveEpisodeWithoutAdvancingAge(bool confirmed, EligibilityStatus eligibility, string reason)
    {
        var active = Run(3, confirmed);
        var interrupted = Step(active, 3, 1, 100, eligibility);
        Assert.False(interrupted.Evaluated); Assert.Equal(SetupStatus.None, interrupted.Status);
        Assert.Equal("NOT_EVALUATED", Assert.Single(interrupted.Reasons)); Assert.Equal(reason, interrupted.Episode!.EndReason);
        Assert.Equal(3, interrupted.Episode.AgeSessions); Assert.Equal(active.Episode!.ConfirmedAgeSessions, interrupted.Episode.ConfirmedAgeSessions);
        var next = Step(interrupted, 4, confirmed ? 101 : 99);
        Assert.NotEqual(active.Episode.Id, next.Episode!.Id); Assert.Equal(1, next.Episode.AgeSessions);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void MissingPreparedCloseOrHighInterruptsEvenIfCallerSaysEligible(bool confirmed, bool missingClose)
    {
        var active = Run(2, confirmed);
        var result = ScreenerEpisodes.Advance(Id, Start.AddDays(2), active, EligibilityStatus.Eligible,
            missingClose ? null : 101, missingClose ? 100 : null);
        Assert.False(result.Evaluated); Assert.Equal("DATA_INTERRUPTED", result.Episode!.EndReason);
        Assert.Equal(2, result.Episode.AgeSessions);
    }

    [Fact]
    public void EpisodeIdentityIsOnlyPolicyStableIdAndStartDate()
    {
        var a = Run(1, true); var b = Run(1, true);
        Assert.Equal(a.Episode, b.Episode); Assert.Equal(a.Reasons, b.Reasons); Assert.Equal("screener-v0.1.0/abcdef01-0000-4000-8000-000000000001/2026-09-21", a.Episode!.Id);
        Assert.NotEqual(a.Episode.Id, Step(ScreenerSetup.Empty, 1, 101).Episode!.Id);
        Assert.DoesNotContain(typeof(ScreenerEpisodes).GetMethods(), m => m.GetParameters().Any(p => p.ParameterType == typeof(DateTimeOffset)));
    }

    [Fact]
    public void PrecedenceRetainsAllEstablishedReasonsInFrozenOrder()
    {
        var result = ScreenerEligibility.Decide(["STALE", "INSUFFICIENT_HISTORY", "NO_TRADE", "TYPE_UNKNOWN", "IDENTITY_CONFLICT", "NO_TRADE"]);
        Assert.Equal(EligibilityStatus.DataBlocked, result.Status);
        Assert.Equal(["IDENTITY_CONFLICT", "TYPE_UNKNOWN", "NO_TRADE", "STALE", "INSUFFICIENT_HISTORY"], result.Reasons);
        Assert.Equal(EligibilityStatus.Ineligible, ScreenerEligibility.Decide(["MISSING_CURRENT_BAR", "UNSUPPORTED_TYPE"]).Status);
    }

    private static ScreenerRow Row(int id, SetupStatus setup = SetupStatus.Confirmed, decimal? rs60 = null,
        decimal? rs20 = null, decimal? liquidity = null, string? symbol = "SYN", bool held = false) => new(
        Guid.Parse($"00000000-0000-4000-8000-{id:000000000000}"), symbol, "Synthetic", true, held, null, null,
        new(EligibilityStatus.Eligible, []), new(setup, true, [], null), Start, 100, 100, false, false, "TRADING", "NEUTRAL",
        ScreenerQuality.Complete, [], new Dictionary<string, FeatureState>
        {
            ["rs60Pp"] = new(rs60 is null ? Availability.UNAVAILABLE : Availability.AVAILABLE, rs60, null),
            ["rs20Pp"] = new(rs20 is null ? Availability.UNAVAILABLE : Availability.AVAILABLE, rs20, null),
            ["monetaryLiquidity20Idr"] = new(liquidity is null ? Availability.UNAVAILABLE : Availability.AVAILABLE, liquidity, null)
        }, new(null, Start, Start, Start, 61, "synthetic"));
    private static ScreenerResult Order(params ScreenerRow[] rows) => ScreenerOrdering.Assemble(Start, Start,
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero), "synthetic", true, true,
        new(Id, Start, "NEUTRAL", "NORMAL", new Dictionary<string, FeatureState>(), [], new(null, Start, Start, Start, 61, "synthetic")),
        rows, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("state")]
    [InlineData("rs60")]
    [InlineData("negative60")]
    [InlineData("rs20")]
    [InlineData("negative20")]
    [InlineData("liquidity")]
    [InlineData("symbol")]
    [InlineData("missing-symbol")]
    [InlineData("uuid")]
    [InlineData("unrounded")]
    public void EachLexicographicOrderingKeyIsExplicit(string key)
    {
        var a = Row(1); var b = Row(2);
        (a, b) = key switch
        {
            "state" => (Row(1, SetupStatus.Watch, 100), Row(2, rs60: -1)),
            "rs60" => (Row(1, rs60: 1, rs20: 100), Row(2, rs60: 2)),
            "negative60" => (Row(1), Row(2, rs60: -100)),
            "rs20" => (Row(1, rs60: 1, rs20: 1, liquidity: 1000), Row(2, rs60: 1, rs20: 2)),
            "negative20" => (Row(1), Row(2, rs20: -100)),
            "liquidity" => (Row(1, liquidity: 100), Row(2, liquidity: 101)),
            "symbol" => (Row(1, symbol: "Z"), Row(2, symbol: "a")),
            "missing-symbol" => (Row(1, symbol: null), Row(2, symbol: "Z")),
            "uuid" => (Row(2, symbol: "abc"), Row(1, symbol: "ABC")),
            "unrounded" => (Row(1, rs60: 1.00001m), Row(2, rs60: 1.00002m)),
            _ => throw new ArgumentException(key)
        };
        var result = Order(a, b);
        Assert.Equal(b.InstrumentId, result.RankedCandidateIds[0]);
        Assert.Equal(result.RankedCandidateIds, Order(b, a).RankedCandidateIds);
    }

    [Fact]
    public void CapComesAfterRanksAllRowsRemainAndHeldNeverConsumesCapacity()
    {
        var rows = Enumerable.Range(1, 28).Select(i => Row(i, i <= 24 ? SetupStatus.Confirmed : SetupStatus.Watch,
            rs60: -i, held: i == 28)).Append(Row(29, SetupStatus.None, held: true) with { Configured = false }).ToArray();
        var result = Order(rows.Reverse().ToArray());
        Assert.Equal(28, result.RankedCandidateIds.Count); Assert.Equal(20, result.ShortlistIds.Count); Assert.Equal(28, result.AllViewIds.Count);
        Assert.Equal(29, result.Rows.Count); Assert.Equal(28, result.Summary.Candidates);
        Assert.Equal(4, result.Summary.OmittedConfirmed); Assert.Equal(4, result.Summary.OmittedWatch);
        Assert.Equal(2, result.HeldIds.Count); Assert.Equal(1, result.Summary.HeldOutsideUniverse);
        Assert.Equal(28, Assert.Single(result.Rows, r => r.InstrumentId == rows[27].InstrumentId).DiscoveryRank);
        Assert.Null(Assert.Single(result.Rows, r => r.InstrumentId == rows[28].InstrumentId).DiscoveryRank);
        Assert.Equal(result.ShortlistIds, result.RankedCandidateIds.Take(20));
    }

    [Fact]
    public void AllViewStateOrderIncludesFailedNoneBlockedAndIneligible()
    {
        var rows = new[] { Row(1, SetupStatus.None), Row(2, SetupStatus.Failed), Row(3, SetupStatus.Watch), Row(4),
            Row(5, SetupStatus.None) with { Eligibility = new(EligibilityStatus.DataBlocked, ["MISSING_CURRENT_BAR"]), Setup = ScreenerSetup.Empty },
            Row(6, SetupStatus.None) with { Eligibility = new(EligibilityStatus.Ineligible, ["UNSUPPORTED_TYPE"]), Setup = ScreenerSetup.Empty } };
        var result = Order(rows);
        Assert.Equal(new List<int> { 4, 3, 2, 1, 5, 6 }.Select(i => Row(i).InstrumentId), result.AllViewIds);
        Assert.Equal(2, result.RankedCandidateIds.Count);
        Assert.Equal(result.Summary.Configured, result.Summary.Eligible + result.Summary.Ineligible + result.Summary.DataBlocked);
    }

    [Fact]
    public void ModelsHaveNoRecommendationActionOrScoreFields()
    {
        var banned = new[] { "recommendation", "buysignal", "sellsignal", "action", "confidencescore", "aiscore" };
        foreach (var type in new[] { typeof(ScreenerRow), typeof(ScreenerResult), typeof(ScreenerSetup), typeof(ScreenerEpisode), typeof(ScreenerSummary), typeof(ScreenerMarketContext) })
            Assert.DoesNotContain(type.GetProperties(), p => banned.Contains(p.Name.ToLowerInvariant()));
    }

    [Fact]
    public void UnavailableEvidenceCannotImproveRankEvenIfPreparedInputContainsAValue()
    {
        var unavailable = Row(1) with { Fields = new Dictionary<string, FeatureState>
        { ["rs60Pp"] = new(Availability.UNAVAILABLE, 1000, "BENCHMARK_MISSING") } };
        var available = Row(2, rs60: -1);
        Assert.Equal(available.InstrumentId, Order(unavailable, available).RankedCandidateIds[0]);
        Assert.Null(unavailable.Rs60Pp);
    }

    [Fact]
    public void ConfirmationOnFifthWatchSessionGetsItsOwnFullTwentySessionPhase()
    {
        var confirmed = Step(Run(4), 4, 101);
        Assert.Equal(5, confirmed.Episode!.AgeSessions); Assert.Equal(1, confirmed.Episode.ConfirmedAgeSessions);
        for (var i = 5; i < 24; i++) confirmed = Step(confirmed, i, 101, 1000);
        Assert.Equal(SetupStatus.Confirmed, confirmed.Status); Assert.Equal(20, confirmed.Episode!.ConfirmedAgeSessions);
        Assert.Equal(24, confirmed.Episode.AgeSessions); Assert.Equal(100m, confirmed.Episode.TriggerPrice);
        var expired = Step(confirmed, 24, 99);
        Assert.Equal("CONFIRMED_EXPIRED", expired.Episode!.EndReason); Assert.Equal(24, expired.Episode.AgeSessions);
        Assert.Equal(20, expired.Episode.ConfirmedAgeSessions);
    }
}
