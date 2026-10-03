using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class DecisionSnapshotTests
{
    private static readonly Guid Id = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"requestId\":null}")]
    [InlineData("{\"requestId\":1}")]
    [InlineData("{\"requestId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"requestId\":\"10000000-0000-4000-8000-000000000001\",\"requestId\":\"10000000-0000-4000-8000-000000000001\"}")]
    [InlineData("{\"requestId\":\"10000000-0000-4000-8000-000000000001\",\"through\":\"2026-1-1\"}")]
    [InlineData("{\"requestId\":\"10000000-0000-4000-8000-000000000001\",\"portfolioId\":\"bad\"}")]
    public void InvalidBodiesAreRejected(string body)
    {
        using var json = JsonDocument.Parse(body);
        var error = Assert.Throws<ScreenerException>(() => DecisionSnapshotRequest.Parse(json.RootElement));
        Assert.Equal(400, error.StatusCode); Assert.Equal("SNAPSHOT_REQUEST_INVALID", error.Code);
    }

    [Theory]
    [InlineData("cutoff")][InlineData("capturedAt")][InlineData("recordedAt")][InlineData("knownAt")]
    [InlineData("policyId")][InlineData("result")][InlineData("expectedInputHash")][InlineData("universe")]
    public void CallerChronologyAndPolicyAreNeverAccepted(string key)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object> { ["requestId"] = Id, [key] = "2026-10-01" }));
        Assert.Equal("SNAPSHOT_REQUEST_INVALID", Assert.Throws<ScreenerException>(() => DecisionSnapshotRequest.Parse(json.RootElement)).Code);
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(1)]
    public void JakartaMidnightResolvesOneClock(int seconds)
    {
        var instant = Now.AddSeconds(seconds);
        var query = new DecisionSnapshotRequest(Id, null, null).Resolve(instant);
        Assert.Equal(new DateOnly(2026, 10, seconds < 0 ? 2 : 3), query.Through);
        Assert.Equal(instant, query.Cutoff);
        Assert.Equal(query, new DecisionSnapshotRequest(Id, query.Through, null).Resolve(instant));
    }

    [Theory]
    [InlineData("2026-10-02")][InlineData("2026-10-04")]
    public void PastAndFutureExplicitThroughCannotBeCaptured(string date)
    {
        Assert.Equal("PROSPECTIVE_THROUGH_REQUIRED", Assert.Throws<ScreenerException>(() =>
            new DecisionSnapshotRequest(Id, DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), null).Resolve(Now)).Code);
    }

    [Fact]
    public void NullAndOmittedIntentAreEqualButExplicitThroughIsDistinct()
    {
        using var omitted = JsonDocument.Parse(JsonSerializer.Serialize(new { requestId = Id }));
        using var explicitNull = JsonDocument.Parse(JsonSerializer.Serialize(new { requestId = Id, through = (string?)null, portfolioId = (Guid?)null }));
        Assert.Equal(DecisionSnapshotRequest.Parse(omitted.RootElement).Intent, DecisionSnapshotRequest.Parse(explicitNull.RootElement).Intent);
        Assert.NotEqual(new DecisionSnapshotIntent(null, null), new DecisionSnapshotIntent(new(2026, 10, 3), null));
    }

    [Theory]
    [InlineData(SetupStatus.None, true, EligibilityStatus.Eligible)]
    [InlineData(SetupStatus.None, false, EligibilityStatus.DataBlocked)]
    [InlineData(SetupStatus.None, false, EligibilityStatus.Ineligible)]
    [InlineData(SetupStatus.Watch, true, EligibilityStatus.Eligible)]
    [InlineData(SetupStatus.Confirmed, true, EligibilityStatus.Eligible)]
    [InlineData(SetupStatus.Failed, true, EligibilityStatus.Eligible)]
    public void ProjectionKeepsSetupAvailabilityAndGenuineZero(SetupStatus setup, bool evaluated, EligibilityStatus eligibility)
    {
        var row = Row(setup, evaluated, eligibility);
        var projected = DecisionSnapshotProjection.Row(ScreenerPresentation.Row(row, new([], [], [], []), Now), false, null, null);
        Assert.Equal(setup.ToString().ToUpperInvariant(), projected.Setup);
        Assert.Equal(evaluated, projected.SetupEvaluated); Assert.Equal(ScreenerPresentation.EligibilityName(eligibility), projected.Eligibility);
        Assert.Null(projected.Result.Ema50); Assert.Null(projected.Result.VolumeRatio20);
        Assert.Equal("WARMUP", projected.Result.FieldStates["ema50"].Availability);
        Assert.Equal(0m, projected.Result.Atr14); Assert.Equal(0m, projected.Result.Rs20Pp);
        Assert.Null(projected.Shares); Assert.Null(projected.AverageCost);
    }

    [Fact]
    public void OutsideHeldEpisodeAndFactualInactiveThesisArePreserved()
    {
        var episode = new ScreenerEpisode("existing-episode", new(2026, 10, 1), new(2026, 10, 2), null, null, 2, 1, 100, null);
        var row = Row(SetupStatus.Confirmed, true, EligibilityStatus.Eligible) with
            { Configured = false, Held = true, Setup = new(SetupStatus.Confirmed, true, [], episode) };
        var thesis = new ThesisVersion(Id, Id, Id, 2, Mandate.FAST_SWING, "private", Now, null, null, false);
        var projected = DecisionSnapshotProjection.Row(ScreenerPresentation.Row(row, new([], [], [], []), Now), true,
            new(Id, 100, 1000, 0, 0, new(2026, 10, 2)), thesis);
        Assert.False(projected.Configured); Assert.True(projected.Held); Assert.Null(projected.DiscoveryRank);
        Assert.Equal(episode, projected.Result.Episode); Assert.Equal(100m, projected.Shares); Assert.Equal(10m, projected.AverageCost);
        Assert.Null(projected.Mandate); Assert.Equal(2, projected.ThesisVersion); Assert.False(projected.ThesisActive);
        Assert.DoesNotContain("private", JsonSerializer.Serialize(projected));
    }

    private static ScreenerRow Row(SetupStatus setup, bool evaluated, EligibilityStatus eligibility) => new(Id, null, null, true, false,
        null, null, new(eligibility, ["original"]), new(setup, evaluated, ["original"], null), new(2026, 10, 2), 100, 100,
        false, false, "TRADING", "UNKNOWN", ScreenerQuality.Partial, ["original"], new Dictionary<string, FeatureState>
        {
            ["close"] = new(Availability.AVAILABLE, 100, null), ["ema50"] = new(Availability.WARMUP, null, "INSUFFICIENT_SESSIONS"),
            ["volumeRatio20"] = new(Availability.UNAVAILABLE, null, "VOLUME_BASIS_UNVERIFIED"),
            ["atr14"] = new(Availability.AVAILABLE, 0, null), ["rs20Pp"] = new(Availability.AVAILABLE, 0, null)
        }, new(null, null, null, null, 0, null));

    [Fact]
    public void KeysetCursorIsBoundToQueryContext()
    {
        var header = new DecisionSnapshotHeader(Id, Id, 1, "PROSPECTIVE_CAPTURE", Now, Now, Now, new(2026, 10, 3),
            null, ScreenerReadRequest.Anchor, ScreenerReadRequest.PolicyId, "PILOT", null, null, new('a',64), new('b',64), "BLOCKED", 0);
        var query = DecisionSnapshotListQuery.Parse(new Dictionary<string, string?>());
        var cursor = query.Encode(header);
        var values = new Dictionary<string, string?> { ["cursor"] = cursor };
        Assert.Equal(Id, DecisionSnapshotListQuery.Parse(values).Cursor?.RunId);
        Assert.Throws<ScreenerException>(() => DecisionSnapshotListQuery.Parse(values, Id));
        values["portfolioId"] = Id.ToString(); Assert.Throws<ScreenerException>(() => DecisionSnapshotListQuery.Parse(values));
    }

    [Theory]
    [InlineData("limit", "0")][InlineData("limit", "101")][InlineData("limit", "-1")]
    [InlineData("cursor", "bad!")][InlineData("portfolioId", "bad")][InlineData("offset", "0")]
    public void InvalidListBoundsAreRejected(string key, string value) => Assert.Equal("SNAPSHOT_QUERY_INVALID",
        Assert.Throws<ScreenerException>(() => DecisionSnapshotListQuery.Parse(new Dictionary<string, string?> { [key] = value })).Code);
}
