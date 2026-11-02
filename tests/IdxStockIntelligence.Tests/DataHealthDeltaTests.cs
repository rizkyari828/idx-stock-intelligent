using System.Text.Json;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

/// <summary>
/// Focused acceptance for the factual daily delta over the two newest canonical sessions. Fixtures are
/// synthetic rows; no database, provider call or session-status inference is involved.
/// </summary>
public sealed class DataHealthDeltaTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Previous = new(2026, 10, 22);
    private static readonly DateOnly Newest = new(2026, 10, 23);
    private static readonly Guid A = Instrument(1);
    private static readonly Guid B = Instrument(2);
    private static readonly Guid C = Instrument(3);
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] DeltaKeys =
        ["since", "previousSession", "newSession", "newSessions", "changes", "coverageDifferences", "flat",
            "zeroVolume", "quality", "degraded", "note"];
    private static readonly string[] ChangeKeys =
        ["instrumentId", "symbol", "status", "previousSession", "previousClose", "newSession", "newClose",
            "change", "changePercent", "reason"];
    private static readonly string[] CoverageKeys = ["instrumentId", "symbol", "presentIn"];
    private static readonly DateOnly[] SingleNewest = [Newest];
    private static readonly DateOnly[] TwoNewest = [Newest, Previous];

    private static Guid Instrument(int index) =>
        Guid.Parse("00000000-0000-4000-8000-" + index.ToString("D12", System.Globalization.CultureInfo.InvariantCulture));

    private static DataHealthBar Bar(Guid id, string symbol, DateOnly day, decimal close, long volume = 1_000,
        string quality = "VALID", bool flat = false) =>
        new(id, symbol, day, flat ? close : close - 1m, flat ? close : close + 1m, flat ? close : close - 1m,
            close, volume, quality);

    private static DataHealthDelta Build(params DataHealthBar[] bars) =>
        DataHealthReport.Build(Now, 11, bars).Delta;

    [Fact]
    public void TwoSessionsCompareCloseChangeInAbsoluteAndPercentTerms()
    {
        var delta = Build(Bar(A, "A.JK", Previous, 100m), Bar(A, "A.JK", Newest, 105m),
            Bar(B, "B.JK", Previous, 50m), Bar(B, "B.JK", Newest, 45m));

        Assert.Equal(Previous, delta.PreviousSession);
        Assert.Equal(Newest, delta.NewSession);
        Assert.Equal(SingleNewest, delta.NewSessions);
        Assert.Equal(2, delta.Changes.Count);
        var first = delta.Changes[0];
        Assert.Equal(DataHealthDelta.Compared, first.Status);
        Assert.Equal(5m, first.Change);
        Assert.Equal(5m, first.ChangePercent);
        Assert.Equal(Previous, first.PreviousSession);
        Assert.Equal(Newest, first.NewSession);
        Assert.Null(first.Reason);
        var second = delta.Changes[1];
        Assert.Equal(-5m, second.Change);
        Assert.Equal(-10m, second.ChangePercent);
        Assert.Empty(delta.CoverageDifferences);
    }

    [Fact]
    public void AnInstrumentMissingFromEitherSessionIsExplicitAndNeverUnchanged()
    {
        var delta = Build(Bar(A, "A.JK", Previous, 100m), Bar(B, "B.JK", Previous, 50m),
            Bar(A, "A.JK", Newest, 105m), Bar(C, "C.JK", Newest, 20m));

        Assert.Equal(3, delta.Changes.Count);
        var changed = delta.Changes.Single(change => change.Symbol == "A.JK");
        Assert.Equal(DataHealthDelta.Compared, changed.Status);
        var previousOnly = delta.Changes.Single(change => change.Symbol == "B.JK");
        Assert.Equal(DataHealthDelta.Missing, previousOnly.Status);
        Assert.Equal("MISSING_NEW_SESSION", previousOnly.Reason);
        Assert.Equal(50m, previousOnly.PreviousClose);
        Assert.Null(previousOnly.NewClose);
        Assert.Null(previousOnly.Change);
        Assert.Null(previousOnly.ChangePercent);
        Assert.Null(previousOnly.NewSession);
        var newOnly = delta.Changes.Single(change => change.Symbol == "C.JK");
        Assert.Equal(DataHealthDelta.Missing, newOnly.Status);
        Assert.Equal("MISSING_PREVIOUS_SESSION", newOnly.Reason);
        Assert.Null(newOnly.PreviousClose);
        Assert.Equal(20m, newOnly.NewClose);
        Assert.Equal(2, delta.CoverageDifferences.Count);
        Assert.Equal(DataHealthDelta.PreviousOnly,
            delta.CoverageDifferences.Single(item => item.Symbol == "B.JK").PresentIn);
        Assert.Equal(DataHealthDelta.NewOnly,
            delta.CoverageDifferences.Single(item => item.Symbol == "C.JK").PresentIn);
    }

    [Fact]
    public void FlatAndZeroVolumeRowsAreCountedOnlyAsObservations()
    {
        var delta = Build(Bar(A, "A.JK", Previous, 100m), Bar(B, "B.JK", Previous, 50m),
            Bar(A, "A.JK", Newest, 100m, volume: 500, flat: true), Bar(B, "B.JK", Newest, 50m, volume: 0));

        Assert.Equal(1, delta.Flat);
        Assert.Equal(1, delta.ZeroVolume);
        Assert.Contains("do not prove", delta.Note, StringComparison.Ordinal);
        Assert.Contains("observation", delta.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmptyStoreReportsAnExplicitEmptyDelta()
    {
        var delta = Build();

        Assert.Null(delta.PreviousSession);
        Assert.Null(delta.NewSession);
        Assert.Empty(delta.NewSessions);
        Assert.Empty(delta.Changes);
        Assert.Empty(delta.CoverageDifferences);
        Assert.False(delta.Degraded);
        Assert.Contains("No canonical session is retained", delta.Note, StringComparison.Ordinal);
        Assert.All(DataHealthReport.QualityStatuses, status => Assert.Equal(0, delta.Quality[status]));
    }

    [Fact]
    public void OneSessionCannotBeCompared()
    {
        var delta = Build(Bar(A, "A.JK", Newest, 100m));

        Assert.Null(delta.PreviousSession);
        Assert.Equal(Newest, delta.NewSession);
        Assert.Empty(delta.Changes);
        Assert.Contains("needs two sessions", delta.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroPreviousCloseKeepsTheAbsoluteChangeAndLeavesPercentUnavailable()
    {
        var delta = Build(Bar(A, "A.JK", Previous, 0m), Bar(A, "A.JK", Newest, 5m));

        var change = Assert.Single(delta.Changes);
        Assert.Equal(DataHealthDelta.Compared, change.Status);
        Assert.Equal(5m, change.Change);
        Assert.Null(change.ChangePercent);
        Assert.Equal("PREVIOUS_CLOSE_ZERO", change.Reason);
    }

    [Fact]
    public void SinceOnlyWidensTheListedNewSessions()
    {
        var rows = new[]
        {
            Bar(A, "A.JK", new DateOnly(2026, 10, 20), 10m), Bar(A, "A.JK", Previous, 11m),
            Bar(A, "A.JK", Newest, 12m)
        };
        var report = DataHealthReport.Build(Now, 11, rows, since: new DateOnly(2026, 10, 20));
        var delta = report.Delta;

        Assert.Equal(TwoNewest, delta.NewSessions);
        Assert.Equal(Previous, delta.PreviousSession);
        Assert.Equal(1m, Assert.Single(delta.Changes).Change);
        Assert.Equal(SingleNewest, DataHealthReport.Build(Now, 11, rows).Delta.NewSessions);
    }

    [Fact]
    public void DegradedReflectsOnlyTheNewestComparedSession()
    {
        var cleanNew = DataHealthReport.Build(Now, 11,
            [Bar(A, "A.JK", Previous, 100m, quality: "DEGRADED"), Bar(A, "A.JK", Newest, 105m)]);
        Assert.True(cleanNew.Degraded);
        Assert.False(cleanNew.Delta.Degraded);

        var degradedNew = DataHealthReport.Build(Now, 11,
            [Bar(A, "A.JK", Previous, 100m), Bar(A, "A.JK", Newest, 105m, quality: "DEGRADED")]);
        Assert.True(degradedNew.Delta.Degraded);
        Assert.Equal(1, degradedNew.Delta.Quality["DEGRADED"]);
    }

    [Fact]
    public void SerializedDeltaShapeIsPinned()
    {
        var delta = Build(Bar(A, "A.JK", Previous, 100m), Bar(B, "B.JK", Previous, 50m),
            Bar(A, "A.JK", Newest, 105m));

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(delta, WebJson));
        var root = document.RootElement;
        Assert.Equal(DeltaKeys, root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(ChangeKeys,
            root.GetProperty("changes")[0].EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(CoverageKeys,
            root.GetProperty("coverageDifferences")[0].EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("MISSING", root.GetProperty("changes")[1].GetProperty("status").GetString());
        Assert.Equal("MISSING_NEW_SESSION", root.GetProperty("changes")[1].GetProperty("reason").GetString());
    }
}
