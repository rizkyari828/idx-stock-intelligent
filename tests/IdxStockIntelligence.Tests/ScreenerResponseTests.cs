using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerResponseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 18, 1, 2, TimeSpan.Zero);
    private static readonly Guid Index = Guid.Parse("76237e96-232f-5085-9b13-dcb7104222bc");
    private static readonly string Hash = new('a', 64);
    private static readonly SelectedScreenerReferences References = new([], [], [], []);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    private static ScreenerQuery Query(params (string Key, string? Value)[] pairs) =>
        ScreenerQuery.Resolve(pairs.ToDictionary(p => p.Key, p => p.Value), Now);
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-4000-8000-{n.ToString("D12", CultureInfo.InvariantCulture)}");

    [Fact]
    public void DefaultsUseOneJakartaClockAndBlankCutoffIsNow()
    {
        var query = Query();
        Assert.Equal(new DateOnly(2026, 10, 3), query.Through); Assert.Equal(Now, query.Cutoff);
        Assert.Equal(("shortlist", "ALL", "ALL", 0, 20), (query.View, query.Setup, query.Eligibility, query.Offset, query.Limit));
        Assert.Null(query.PortfolioId); Assert.Null(query.InputHash);
        Assert.Equal(query, Query(("cutoff", ""))); Assert.Equal(query, Query(("cutoff", " ")));
    }

    [Theory]
    [InlineData("through", "2026-08-23")]
    [InlineData("through", "2027-08-25")]
    [InlineData("through", "2026-10-04")]
    [InlineData("through", "2026-9-30")]
    [InlineData("through", "2026-99-30")]
    [InlineData("through", "")]
    [InlineData("cutoff", "1899-12-31T23:59:59Z")]
    [InlineData("cutoff", "2026-10-02T18:01:03Z")]
    [InlineData("cutoff", "2026-09-30T12:00:00")]
    [InlineData("cutoff", "2026-09-30")]
    [InlineData("cutoff", "2026-09-30 12:00:00Z")]
    [InlineData("universe", "FullIdx")]
    [InlineData("universe", "AAA,BBB")]
    [InlineData("universe", "")]
    [InlineData("portfolioId", "bad")]
    [InlineData("portfolioId", "00000000-0000-0000-0000-000000000000")]
    [InlineData("portfolioId", "")]
    [InlineData("view", "ALL")]
    [InlineData("setup", "BUY")]
    [InlineData("eligibility", "Unknown")]
    [InlineData("offset", "-1")]
    [InlineData("offset", "10001")]
    [InlineData("offset", "1.0")]
    [InlineData("offset", "2147483648")]
    [InlineData("limit", "0")]
    [InlineData("limit", "101")]
    [InlineData("limit", "")]
    [InlineData("inputHash", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("inputHash", "a")]
    [InlineData("sort", "symbol")]
    [InlineData("excludeHeld", "true")]
    [InlineData("path", "/tmp/input")]
    public void InvalidQueryIsRejected(string key, string value) => Assert.Throws<ArgumentException>(() => Query((key, value)));

    [Fact]
    public void ExplicitBoundariesOffsetNormalizationAndPageEdgesAreAccepted()
    {
        var query = Query(("through", "2026-08-24"), ("cutoff", "2026-09-30T19:00:00+07:00"), ("portfolioId", Id(1).ToString()),
            ("view", "all"), ("setup", "CONFIRMED"), ("eligibility", "ELIGIBLE"), ("offset", "10000"), ("limit", "100"), ("inputHash", Hash));
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero), query.Cutoff);
        Assert.Equal(Id(1), query.PortfolioId); Assert.Equal(Hash, query.InputHash);
        var end = ScreenerQuery.Resolve(new Dictionary<string, string?> { ["through"] = "2027-08-24" }, Now.AddYears(1));
        Assert.Equal(ScreenerReadRequest.Horizon, end.Through);
        Assert.Equal(new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero), Query(("cutoff", "1900-01-01T00:00:00Z")).Cutoff);
    }

    private static Dictionary<string, FeatureState> Fields() => new(StringComparer.Ordinal)
    {
        ["close"] = new(Availability.AVAILABLE, 100, null), ["volume"] = new(Availability.AVAILABLE, 100, null),
        ["changePercent"] = new(Availability.AVAILABLE, 0, null), ["ema20"] = new(Availability.AVAILABLE, 100, null),
        ["ema50"] = new(Availability.WARMUP, null, "INSUFFICIENT_SESSIONS"), ["priorHigh20"] = new(Availability.AVAILABLE, 100, null),
        ["priorLow20"] = new(Availability.AVAILABLE, 100, null), ["distanceToHighPercent"] = new(Availability.AVAILABLE, 0, null),
        ["volumeRatio20"] = new(Availability.UNAVAILABLE, null, "VOLUME_BASIS_UNVERIFIED"),
        ["dailyValueProxyIdr"] = new(Availability.UNAVAILABLE, null, "VOLUME_BASIS_UNVERIFIED"),
        ["monetaryLiquidity20Idr"] = new(Availability.UNAVAILABLE, null, "VOLUME_BASIS_UNVERIFIED"),
        ["atr14"] = new(Availability.AVAILABLE, 0, null), ["atrPercent"] = new(Availability.AVAILABLE, 0, null),
        ["rs20Pp"] = new(Availability.AVAILABLE, 0, null), ["rs60Pp"] = new(Availability.WARMUP, null, "INSUFFICIENT_SESSIONS"),
        ["trend"] = new(Availability.UNAVAILABLE, null, "INSUFFICIENT_SESSIONS")
    };
    private static ScreenerRow Row(int n, bool held = false, bool configured = true, SetupStatus setup = SetupStatus.Confirmed,
        EligibilityStatus eligibility = EligibilityStatus.Eligible) => new(Id(n), "SYN" + n.ToString("D3", CultureInfo.InvariantCulture),
        "Synthetic", configured, held, null, null, new(eligibility, []), new(setup, eligibility == EligibilityStatus.Eligible, [], null),
        new(2026, 9, 30), 100, 100, false, false, "TRADING", "UNKNOWN", ScreenerQuality.Partial, ["INSUFFICIENT_SESSIONS"],
        Fields(), new(null, ScreenerReadRequest.Anchor, ScreenerReadRequest.Anchor, ScreenerReadRequest.Anchor, 21, null));
    private static ScreenerResult Result(params ScreenerRow[] rows) => ScreenerOrdering.Assemble(new(2026, 9, 30), new(2026, 9, 30), Now,
        "synthetic", true, true, new(Index, null, "UNKNOWN", "UNKNOWN", new Dictionary<string, FeatureState>
        { ["close"] = new(Availability.UNAVAILABLE, null, "BENCHMARK_MISSING"), ["ema20"] = new(Availability.UNAVAILABLE, null, "BENCHMARK_MISSING"),
            ["ema50"] = new(Availability.UNAVAILABLE, null, "BENCHMARK_MISSING"), ["atr14"] = new(Availability.UNAVAILABLE, null, "BENCHMARK_MISSING"),
            ["atrPercent"] = new(Availability.UNAVAILABLE, null, "BENCHMARK_MISSING") }, ["BENCHMARK_MISSING"], new(null, null, null, null, 0, null)),
        rows, TestContext.Current.CancellationToken);
    private static ScreenerResponse Map(ScreenerResult result, ScreenerQuery query) =>
        ScreenerPresentation.Map(result, query, Hash, References, TestContext.Current.CancellationToken);

    [Fact]
    public void ShortlistIsCappedBeforeFilteringAndNeverRefills()
    {
        var rows = Enumerable.Range(1, 25).Select(n => Row(n, setup: n <= 20 ? SetupStatus.Confirmed : SetupStatus.Watch)).ToArray();
        var result = Result(rows);
        var filtered = Map(result, Query(("setup", "WATCH")));
        Assert.Empty(filtered.DiscoveryIds); Assert.Equal(0, filtered.Page.Total);
        Assert.Equal(25, filtered.Summary.Candidates); Assert.Equal(5, filtered.Summary.OmittedWatch);
        var all = Map(result, Query(("view", "all"), ("setup", "WATCH")));
        Assert.Equal(5, all.Page.Total); Assert.Equal(21, all.Rows[0].DiscoveryRank); Assert.Equal(Hash, all.InputHash);
    }

    [Theory]
    [InlineData("ALL", "ALL")]
    [InlineData("NONE", "DATA_BLOCKED")]
    [InlineData("FAILED", "INELIGIBLE")]
    public void PagingAndFiltersNeverHideHeldRowsOrChangeRanks(string setup, string eligibility)
    {
        var result = Result(Row(1, true), Row(2), Row(3), Row(4, true, false, SetupStatus.None, EligibilityStatus.DataBlocked),
            Row(5, true, true, SetupStatus.Failed, EligibilityStatus.Ineligible));
        var response = Map(result, Query(("view", "all"), ("setup", setup), ("eligibility", eligibility), ("offset", "100"), ("limit", "1")));
        Assert.Empty(response.DiscoveryIds); Assert.Equal(result.HeldIds, response.HeldIds); Assert.Equal(3, response.Rows.Count);
        Assert.Equal(1, Assert.Single(response.Rows, r => r.InstrumentId == Id(1)).DiscoveryRank);
        Assert.Null(Assert.Single(response.Rows, r => r.InstrumentId == Id(4)).DiscoveryRank);
        Assert.Equal(result.Summary, response.Summary);
        var first = Map(result, Query(("limit", "1")));
        Assert.Single(first.DiscoveryIds); Assert.Equal(3, first.Rows.Count); Assert.Equal(3, first.Rows.Select(r => r.InstrumentId).Distinct().Count());
    }

    [Fact]
    public void AllViewPagesIncludeEveryConfiguredStateWithoutDuplicates()
    {
        var result = Result(Row(1), Row(2, setup: SetupStatus.Watch), Row(3, setup: SetupStatus.Failed),
            Row(4, setup: SetupStatus.None), Row(5, eligibility: EligibilityStatus.DataBlocked), Row(6, eligibility: EligibilityStatus.Ineligible));
        var ids = Enumerable.Range(0, 6).SelectMany(n => Map(result, Query(("view", "all"), ("offset", n.ToString(CultureInfo.InvariantCulture)), ("limit", "1"))).DiscoveryIds).ToArray();
        Assert.Equal(result.AllViewIds, ids); Assert.Equal(6, ids.Distinct().Count());
    }

    [Fact]
    public void JsonPreservesKeysNullsRealZerosReasonsAndStructuralAbsence()
    {
        var response = Map(Result(Row(1)), Query());
        using var json = JsonSerializer.SerializeToDocument(response, JsonOptions);
        var row = json.RootElement.GetProperty("rows")[0];
        Assert.Equal(JsonValueKind.Null, row.GetProperty("ema50").ValueKind);
        Assert.Equal(0, row.GetProperty("atr14").GetDecimal()); Assert.Equal(0, row.GetProperty("rs20Pp").GetDecimal());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("mandate").ValueKind); Assert.Equal(JsonValueKind.Null, row.GetProperty("episode").ValueKind);
        var states = row.GetProperty("fieldStates");
        Assert.Equal("WARMUP", states.GetProperty("ema50").GetProperty("availability").GetString());
        Assert.Equal("INSUFFICIENT_SESSIONS", states.GetProperty("ema50").GetProperty("reason").GetString());
        Assert.False(states.TryGetProperty("mandate", out _)); Assert.False(states.TryGetProperty("atr14", out _));
        Assert.False(row.TryGetProperty("relativePerformance20", out _));
        foreach (var key in new[] { "recommendation", "action", "score", "confidence" }) Assert.DoesNotContain(key, json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var context = json.RootElement.GetProperty("marketContext");
        Assert.Equal(JsonValueKind.Null, context.GetProperty("close").ValueKind); Assert.Equal("UNKNOWN", context.GetProperty("volatility").GetString());
        Assert.Equal("FIXED_PILOT_KNOWN_INPUTS", response.ReplayScope);
    }

    [Fact]
    public void ChangedPinIs409AndCancellationPropagates()
    {
        var result = Result(Row(1));
        Assert.Equal("INPUT_CHANGED", Assert.Throws<ScreenerException>(() => Map(result, Query(("inputHash", new string('b', 64))))).Code);
        Assert.Equal(Hash, Map(result, Query(("inputHash", Hash))).InputHash);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => ScreenerPresentation.Map(result, Query(), Hash, References, cancel.Token));
    }

    private static ScreenerPortfolioHistory History() => new(new(Id(100), "Synthetic", true, Now.AddDays(-10)),
        [new(Id(101), Id(100), PortfolioEventType.BUY, Id(1), new(2026, 9, 30), Now.AddDays(-1), 1, 100, QuantityUnit.SHARES, 10, 0, 0, null, "USER", null, null)],
        [new(Id(102), Id(100), Id(1), 1, Mandate.FAST_SWING, "Synthetic", Now.AddDays(-1), null, null, true)]);
    private static string Digest(ScreenerPortfolioHistory? history, string market = "market")
    {
        var request = new ScreenerReadRequest([Id(1)], Index, ScreenerReadRequest.Anchor, new(2026, 9, 30), Now);
        var projection = history is null ? null : PortfolioLedger.Project(history.Events, request.Cutoff, request.Through, true);
        return ScreenerPresentation.InputHash(market, request, history, projection, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("header")]
    [InlineData("ledger")]
    [InlineData("correction")]
    [InlineData("thesis")]
    [InlineData("market")]
    public void EverySelectedPortfolioOrMarketChangeChangesHash(string change)
    {
        var history = History(); var before = Digest(history);
        var changed = change switch
        {
            "header" => history with { Portfolio = history.Portfolio with { AllowNegativeCash = false } },
            "ledger" => history with { Events = [history.Events[0] with { Quantity = 200 }] },
            "correction" => history with { Events = [history.Events[0], history.Events[0] with { Id = Id(103), Supersedes = Id(101), Order = 2, Quantity = 300 }] },
            "thesis" => history with { Theses = [history.Theses[0] with { Active = false }] },
            _ => history
        };
        Assert.NotEqual(before, Digest(changed, change == "market" ? "other" : "market"));
        Assert.NotEqual(before, Digest(null));
    }

    [Fact]
    public void FutureFactsUnheldThesesOrderingOffsetsAndDecimalScaleCannotChangeHash()
    {
        var history = History(); var before = Digest(history);
        var expanded = history with
        {
            Portfolio = history.Portfolio with { CreatedAt = history.Portfolio.CreatedAt.ToOffset(TimeSpan.FromHours(7)) },
            Events = [history.Events[0] with { KnownAt = history.Events[0].KnownAt.ToOffset(TimeSpan.FromHours(7)), Price = 10.00m },
                history.Events[0] with { Id = Id(103), KnownAt = Now.AddDays(1), Order = 2 }],
            Theses = [history.Theses[0] with { KnownAt = history.Theses[0].KnownAt.ToOffset(TimeSpan.FromHours(7)) },
                history.Theses[0] with { Id = Id(104), Version = 2, KnownAt = Now.AddDays(1) },
                history.Theses[0] with { Id = Id(105), InstrumentId = Id(2) }]
        };
        Assert.Equal(before, Digest(expanded));
        Assert.Equal(before, Digest(expanded with { Events = expanded.Events.Reverse().ToArray(), Theses = expanded.Theses.Reverse().ToArray() }));
    }
}
