using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ResearchDeliveryTests
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
            "original-universe", portfolio, new('a', 64), new('b', 64), "COMPLETE", count),
        "UNKNOWN", "UNKNOWN", Target, ["original-context"]);
    private static ResearchObservation Row(int id = 1, int run = 100) => new(Id(run), Id(id), "OLD", true, false,
        "ELIGIBLE", "NONE", true, [], [], null, Target, 100m);
    private static ResearchOutcome Outcome(ResearchObservation row, decimal? value = 10, string state = "AVAILABLE") => new(
        row.RunId, row.InstrumentId, 5, "outcome-v0.1.0", 1, row.MarketDate, row.Close, new(2026, 10, 8),
        state == "AVAILABLE" ? 110 : null, state, state switch { "AVAILABLE" => null,
            "ANCHOR_UNAVAILABLE" => "CAPTURED_PRICE_BASIS_UNVERIFIED", "DATA_UNAVAILABLE" => "SUSPENDED_AT_HORIZON",
            _ => "PRICE_CONVENTION_UNSUPPORTED" }, state == "AVAILABLE" ? value : null, Known, Known.AddSeconds(1));
    private static ResearchResult Evaluate(ResearchRun[] runs, ResearchObservation[] rows, ResearchOutcome[]? outcomes = null,
        ResearchQuery? query = null)
        => ResearchEvaluation.Evaluate(query ?? Query(), runs, rows, outcomes ?? [], Now, TestContext.Current.CancellationToken);
    private static ResearchResult Returns(params decimal[] values)
    {
        var rows = values.Select((_, i) => Row(i + 1)).ToArray();
        return Evaluate([Run(rows.Length)], rows, rows.Select((r, i) => Outcome(r, values[i])).ToArray());
    }
    private static string IdOf(ResearchResult result)
        => ResearchDelivery.DatasetId(ResearchDelivery.Preimage(result), TestContext.Current.CancellationToken);
    private static ResearchDeliveryRequest Request(ResearchQuery? query = null, int limit = ResearchDelivery.DefaultLimit,
        ResearchCursor? cursor = null, string? datasetId = null, bool export = false)
        => new(query ?? Query(), true, limit, cursor, datasetId,
            export ? ResearchDelivery.ExportFormat : ResearchDelivery.PageFormat);
    private static Dictionary<string, string?> Q(params (string Key, string? Value)[] values)
        => values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
    private static string BadQuery(IReadOnlyDictionary<string, string?> values)
        => Assert.Throws<ScreenerException>(() => ResearchDelivery.Parse(values, Now)).Code;
    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Fact]
    public void OmittedDefaultsAndExplicitDefaultsNormalizeIdentically()
    {
        var omitted = ResearchDelivery.Parse(Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15")), Now);
        var explicitDefaults = ResearchDelivery.Parse(Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"),
            ("horizonSessions", "5"), ("cohort", "ALL"), ("groupBy", "NONE")), Now);
        Assert.Equal(omitted.Query.Canonical(), explicitDefaults.Query.Canonical());
        Assert.False(omitted.ExplicitCutoff); Assert.Equal(Now, omitted.Query.Cutoff);
        Assert.Equal(ResearchDelivery.DefaultLimit, omitted.Limit); Assert.Equal("PAGE", omitted.Format);
        Assert.Null(omitted.DatasetId); Assert.Null(omitted.Cursor); Assert.False(omitted.Export);
        Assert.False(explicitDefaults.ExplicitCutoff);
    }

    [Theory]
    [InlineData("2026-10-15T13:00:00Z", "2026-10-15T20:00:00+07:00")]
    [InlineData("2026-10-15T13:00:00.0000000Z", "2026-10-15T06:00:00-07:00")]
    public void EquivalentOffsetsAndFractionalFormsNormalizeIdentically(string utc, string offset)
    {
        var left = ResearchDelivery.Parse(Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cutoff", utc)), Now);
        var right = ResearchDelivery.Parse(Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cutoff", offset)), Now);
        Assert.Equal(left.Query.Canonical(), right.Query.Canonical());
        Assert.True(left.ExplicitCutoff);
    }

    [Fact]
    public void StrictQueryVocabularyRejectsUnfrozenInputs()
    {
        var validCursor = new ResearchCursor(Now, Id(100), Id(1), new string('c', 64)).Encode();
        var bad = new[]
        {
            Q(("captureFrom", "2026-10-01")),
            Q(("captureTo", "2026-10-15")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("sort", "asc")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("policy", "latest")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("outcomeState", "AVAILABLE")),
            Q(("captureFrom", ""), ("captureTo", "2026-10-15")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cohort", "")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("portfolioId", "null")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("portfolioId", "00000000-0000-0000-0000-000000000000")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("instrumentId", "not-a-uuid")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("horizonSessions", "0")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("horizonSessions", "2")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("horizonSessions", "05x")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cohort", "all")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("groupBy", "MARKET_TREND,VOLATILITY")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("limit", "0")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("limit", "-1")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("limit", "101")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("limit", "10.0")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cutoff", "2026-10-15T13:00:00")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cutoff", "2026-10-15 13:00:00Z")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cutoff", "2026-10-16T13:00:00Z")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cutoff", "1899-12-31T23:59:59Z")),
            Q(("captureFrom", "2026-8-1"), ("captureTo", "2026-10-15")),
            Q(("captureFrom", "2026-10-02"), ("captureTo", "2026-10-01")),
            Q(("captureFrom", "2026-08-23"), ("captureTo", "2026-10-15")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-16")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("instrumentId", Id(1).ToString("D")), ("episodeId", "screener-v0.1.0/" + Id(1).ToString("D") + "/2026-09-30")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("episodeId", "bogus")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("format", "CSV")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("format", "export_json")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("format", "JSON")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("datasetId", "A" + new string('a', 63))),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("datasetId", new string('a', 63))),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cursor", "not-a-cursor")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cursor", validCursor)),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("cutoff", "2026-10-15T13:00:00Z"), ("cursor", validCursor)),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("format", "EXPORT_JSON")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("format", "EXPORT_JSON"), ("cutoff", "2026-10-15T13:00:00Z")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("format", "EXPORT_JSON"), ("cutoff", "2026-10-15T13:00:00Z"),
                ("datasetId", new string('a', 64)), ("limit", "50")),
            Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("format", "EXPORT_JSON"), ("cutoff", "2026-10-15T13:00:00Z"),
                ("datasetId", new string('a', 64)), ("cursor", validCursor))
        };
        foreach (var values in bad) Assert.Equal("RESEARCH_QUERY_INVALID", BadQuery(values));
    }

    [Fact]
    public void PageLimitsAndPinnedDeliveryParametersParse()
    {
        foreach (var limit in new[] { "1", "50", "100" })
            Assert.Equal(int.Parse(limit, CultureInfo.InvariantCulture),
                ResearchDelivery.Parse(Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"), ("limit", limit)), Now).Limit);
        var cursor = new ResearchCursor(Now, Id(100), Id(1), new string('c', 64)).Encode();
        var pinned = ResearchDelivery.Parse(Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"),
            ("cutoff", "2026-10-15T13:00:00Z"), ("datasetId", new string('a', 64)), ("cursor", cursor)), Now);
        Assert.NotNull(pinned.Cursor); Assert.Equal(new string('a', 64), pinned.DatasetId); Assert.True(pinned.ExplicitCutoff);
        var export = ResearchDelivery.Parse(Q(("captureFrom", "2026-10-01"), ("captureTo", "2026-10-15"),
            ("cutoff", "2026-10-15T13:00:00Z"), ("datasetId", new string('a', 64)), ("format", "EXPORT_JSON")), Now);
        Assert.True(export.Export);
    }

    [Fact]
    public void DatasetIdIsStableAcrossPagesLimitsAndFormats()
    {
        var result = Returns(1, 2, 3);
        var preimage = ResearchDelivery.Preimage(result);
        var datasetId = ResearchDelivery.DatasetId(preimage, TestContext.Current.CancellationToken);
        var one = ResearchDelivery.Page(result, Request(limit: 1), preimage, datasetId, Now);
        var hundred = ResearchDelivery.Page(result, Request(limit: 100), preimage, datasetId, Now);
        Assert.Equal(datasetId, one.Dataset.DatasetId);
        Assert.Equal(datasetId, hundred.Dataset.DatasetId);
        Assert.Equal(one.Summary, hundred.Summary);
        Assert.Equal(one.Dataset, hundred.Dataset);
        using var export = JsonDocument.Parse(ResearchDelivery.ExportBytes(result, preimage, datasetId));
        Assert.Equal(datasetId, export.RootElement.GetProperty("datasetId").GetString());
        Assert.Equal(datasetId, OutputHash(export.RootElement.GetProperty("preimage")));
        Assert.Equal(datasetId, IdOf(result));
    }

    private static string OutputHash(JsonElement preimage)
        => ResearchDelivery.DatasetId(preimage.Deserialize<ResearchIdentityPreimage>(ScreenerReferences.JsonOptions)!,
            TestContext.Current.CancellationToken);

    [Fact]
    public void DatasetIdChangesOnlyWithSelectedAnalyticalTruth()
    {
        var baseline = Returns(10, 20);
        var baselineId = IdOf(baseline);
        Assert.Equal(baselineId, IdOf(Returns(10, 20)));
        Assert.NotEqual(baselineId, IdOf(Returns(10, 21)));
        Assert.NotEqual(baselineId, IdOf(Returns(10, 20, 30)));
        var rows = new[] { Row(1), Row(2) };
        var outcomes = new[] { Outcome(rows[0], 10), Outcome(rows[1], 20) };
        var resolved = Evaluate([Run(2)], rows, outcomes);
        Assert.Equal(baselineId, IdOf(resolved));
        Assert.NotEqual(baselineId, IdOf(Evaluate([Run(2)], rows, [outcomes[0]])));
        Assert.NotEqual(IdOf(Evaluate([Run(1)], [Row()], [Outcome(Row(), state: "DATA_UNAVAILABLE")])),
            IdOf(Evaluate([Run(1)], [Row()], [Outcome(Row(), state: "BASIS_UNCERTAIN")])));
        var suspended = Outcome(Row(), state: "DATA_UNAVAILABLE");
        Assert.NotEqual(IdOf(Evaluate([Run(1)], [Row()], [suspended])),
            IdOf(Evaluate([Run(1)], [Row()], [suspended with { Reason = "NO_TRADE_AT_HORIZON" }])));
        Assert.NotEqual(IdOf(Evaluate([Run(1)], [Row()])), IdOf(Evaluate([Run(1)], [Row() with { Close = 101 }])));
        Assert.NotEqual(IdOf(Evaluate([Run(1)], [Row()])), IdOf(Evaluate([Run(2)], [Row(), Row(2)])));
        Assert.NotEqual(IdOf(Evaluate([Run(1)], [Row()])), IdOf(Evaluate([Run(1)], [Row()], query: Query() with { Cohort = ResearchCohort.WATCH })));
        Assert.NotEqual(IdOf(Evaluate([Run(1)], [Row()])), IdOf(Evaluate([Run(1)], [Row()], query: Query() with { Cutoff = Now.AddMinutes(-1) })));
        Assert.NotEqual(IdOf(Evaluate([Run(1)], [Row()], query: Query() with { GroupBy = ResearchGrouping.MARKET_TREND })),
            IdOf(Evaluate([Run(1)], [Row()], query: Query() with { GroupBy = ResearchGrouping.HELD_CONTEXT })));
    }

    [Fact]
    public void DatasetIdIgnoresIrrelevantFutureAndOrderingInsensitiveEvidence()
    {
        var rows = new[] { Row(1), Row(2) };
        var outcomes = new[] { Outcome(rows[0], 10), Outcome(rows[1], 20) };
        var baseline = Evaluate([Run(2)], rows, outcomes);
        var later = Evaluate([Run(2)], rows, [outcomes[0], outcomes[1], Outcome(rows[0], 999) with { HorizonSessions = 20 }]);
        Assert.Equal(IdOf(baseline), IdOf(later));
        var outside = Run(0, 900) with { Header = Run(0, 900).Header with { CapturedAt = Now.AddDays(30),
            KnowledgeCutoff = Now.AddDays(30), RecordedAt = Now.AddDays(30),
            Through = OutcomeEvaluator.Through(Now.AddDays(30)) } };
        Assert.Equal(IdOf(baseline), IdOf(Evaluate([Run(2), outside], rows, outcomes)));
        Assert.Equal(IdOf(baseline), IdOf(Evaluate([Run(2)], rows.Reverse().ToArray(), outcomes.Reverse().ToArray())));
    }

    [Fact]
    public void OrderedReasonArraysAndExplicitNullsAreIdentityBearing()
    {
        var ordered = Row() with { EligibilityReasons = ["first", "second"], SetupReasons = ["only"] };
        var reversed = Row() with { EligibilityReasons = ["second", "first"], SetupReasons = ["only"] };
        Assert.NotEqual(IdOf(Evaluate([Run(1)], [ordered])), IdOf(Evaluate([Run(1)], [reversed])));
        Assert.Equal(IdOf(Evaluate([Run(1)], [ordered])), IdOf(Evaluate([Run(1)], [ordered with { EligibilityReasons = ["first", "second"] }])));
        var zero = Evaluate([Run(1)], [Row()], [Outcome(Row(), 0)]);
        var unresolved = Evaluate([Run(1)], [Row()]);
        Assert.NotEqual(IdOf(zero), IdOf(unresolved));
        var preimage = ResearchDelivery.Preimage(unresolved);
        Assert.Null(preimage.Observations[0].Outcome);
        Assert.Equal("NO_COMMITTED_OUTCOME_AS_OF_CUTOFF", preimage.Observations[0].ResearchReason);
        Assert.Equal("UNRESOLVED", preimage.Observations[0].Resolution);
    }

    [Fact]
    public void FirstPageAndContinuationCarryNoDuplicateSkippedOrReorderedRows()
    {
        var result = Returns(1, 2, 3, 4, 5);
        var preimage = ResearchDelivery.Preimage(result);
        var datasetId = ResearchDelivery.DatasetId(preimage, TestContext.Current.CancellationToken);
        var first = ResearchDelivery.Page(result, Request(limit: 2), preimage, datasetId, Now);
        int[] firstOrdinals = [0, 1];
        Assert.Equal(firstOrdinals, first.Observations.Select(o => o.Ordinal));
        Assert.NotNull(first.Page.NextCursor);
        var second = ResearchDelivery.Page(result,
            Request(limit: 2, cursor: ResearchCursor.Decode(first.Page.NextCursor!)), preimage, datasetId, Now);
        int[] secondOrdinals = [2, 3];
        Assert.Equal(secondOrdinals, second.Observations.Select(o => o.Ordinal));
        var third = ResearchDelivery.Page(result,
            Request(limit: 2, cursor: ResearchCursor.Decode(second.Page.NextCursor!)), preimage, datasetId, Now);
        int[] thirdOrdinals = [4];
        Assert.Equal(thirdOrdinals, third.Observations.Select(o => o.Ordinal));
        Assert.Null(third.Page.NextCursor);
        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(first.Summary, third.Summary);
        Assert.Equal(first.Dataset.DatasetId, third.Dataset.DatasetId);
        Assert.Equal(result.Summary.Counts.N, third.Page.Total);
        Assert.Equal(5, first.Page.Total);
        Assert.Equal(first.Observations.Concat(second.Observations).Concat(third.Observations)
            .Select(o => o.InstrumentId), result.Observations.Select(c => c.Observation.InstrumentId.ToString("D")));
    }

    [Fact]
    public void CursorIsOpaqueBoundedAndBoundToItsDataset()
    {
        var result = Returns(1, 2, 3);
        var preimage = ResearchDelivery.Preimage(result);
        var datasetId = ResearchDelivery.DatasetId(preimage, TestContext.Current.CancellationToken);
        var encoded = ResearchDelivery.Page(result, Request(limit: 2), preimage, datasetId, Now).Page.NextCursor!;
        Assert.True(encoded.Length <= ResearchDelivery.MaximumCursorLength);
        Assert.All(encoded, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        Assert.Throws<ScreenerException>(() => ResearchCursor.Decode(new string('A', ResearchDelivery.MaximumCursorLength + 1)));
        Assert.Throws<ScreenerException>(() => ResearchCursor.Decode(encoded[..^2]));
        Assert.Throws<ScreenerException>(() => ResearchCursor.Decode(encoded + "!"));
        Assert.Throws<ScreenerException>(() => ResearchCursor.Decode("+++///"));
        var extra = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"capturedAt\":\"2026-10-01T00:00:00+00:00\",\"runId\":\""
            + Id(1).ToString("D") + "\",\"instrumentId\":\"" + Id(2).ToString("D") + "\",\"context\":\"x\",\"extra\":1}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Throws<ScreenerException>(() => ResearchCursor.Decode(extra));
        var cursor = ResearchCursor.Decode(encoded);
        var foreign = new string('f', 64);
        Assert.Throws<ScreenerException>(() =>
            ResearchDelivery.Page(result, Request(cursor: cursor), preimage, foreign, Now));
        var missing = new ResearchCursor(cursor.CapturedAt, Id(999), cursor.InstrumentId, datasetId);
        Assert.Throws<ScreenerException>(() =>
            ResearchDelivery.Page(result, Request(cursor: missing), preimage, datasetId, Now));
        Assert.Throws<ScreenerException>(() => ResearchDelivery.Page(result,
            Request(cursor: new ResearchCursor(cursor.CapturedAt, cursor.RunId, cursor.InstrumentId, datasetId) with { CapturedAt = cursor.CapturedAt.AddSeconds(1) }),
            preimage, datasetId, Now));
        var other = Returns(1, 2, 3, 4);
        var otherPreimage = ResearchDelivery.Preimage(other);
        var otherId = ResearchDelivery.DatasetId(otherPreimage, TestContext.Current.CancellationToken);
        Assert.Throws<ScreenerException>(() => ResearchDelivery.Page(other,
            Request(cursor: cursor), otherPreimage, otherId, Now));
    }

    [Fact]
    public void ExportIsLosslessDeterministicAndCoversTheWholePopulation()
    {
        decimal[] values = [0.0000000000000000000000000001m, -10.25m, 0m, 100m, 7922816251426433759354395033m];
        var result = Returns(values);
        var preimage = ResearchDelivery.Preimage(result);
        var datasetId = ResearchDelivery.DatasetId(preimage, TestContext.Current.CancellationToken);
        var first = ResearchDelivery.ExportBytes(result, preimage, datasetId);
        var second = ResearchDelivery.ExportBytes(result, preimage, datasetId);
        Assert.Equal(first, second);
        Assert.Equal(Hex(first), Hex(second));
        Assert.True(first.Length <= ResearchDelivery.MaximumExportBytes);
        using var document = JsonDocument.Parse(first);
        var root = document.RootElement;
        Assert.Equal(datasetId, root.GetProperty("datasetId").GetString());
        Assert.Equal(datasetId, ResearchDelivery.DatasetId(
            root.GetProperty("preimage").Deserialize<ResearchIdentityPreimage>(ScreenerReferences.JsonOptions)!,
                TestContext.Current.CancellationToken));
        var observations = root.GetProperty("preimage").GetProperty("observations");
        Assert.Equal(5, observations.GetArrayLength());
        var texts = observations.EnumerateArray().Select(o => o.GetProperty("outcome").GetProperty("priceReturnPct").GetString()).ToArray();
        Assert.Equal(values.Select(v => ResearchEvaluation.DecimalText(v)).ToArray(), texts);
        Assert.All(observations.EnumerateArray(), o => Assert.Equal("TERMINAL", o.GetProperty("resolution").GetString()));
        var counts = root.GetProperty("summary").GetProperty("counts");
        Assert.Equal(5, counts.GetProperty("n").GetInt32());
        Assert.Equal(5, counts.GetProperty("a").GetInt32());
        Assert.Equal(0, counts.GetProperty("u").GetInt32());
        Assert.Equal(counts.GetProperty("n").GetInt32(),
            counts.GetProperty("a").GetInt32() + counts.GetProperty("t").GetInt32() + counts.GetProperty("u").GetInt32());
        Assert.Equal("0", ResearchEvaluation.DecimalText(0m));
        Assert.Contains("\"availableCoverage\":\"100\"", Encoding.UTF8.GetString(first), StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAndPagePreserveUnresolvedCellsAndExcludePrivateFields()
    {
        var rows = new[] { Row(1), Row(2) };
        var result = Evaluate([Run(2)], rows, [Outcome(rows[0], -10.25m)]);
        var preimage = ResearchDelivery.Preimage(result);
        var datasetId = ResearchDelivery.DatasetId(preimage, TestContext.Current.CancellationToken);
        using var export = JsonDocument.Parse(ResearchDelivery.ExportBytes(result, preimage, datasetId));
        var observations = export.RootElement.GetProperty("preimage").GetProperty("observations");
        var unresolved = observations.EnumerateArray().Single(o => o.GetProperty("resolution").GetString() == "UNRESOLVED");
        Assert.Equal("NO_COMMITTED_OUTCOME_AS_OF_CUTOFF", unresolved.GetProperty("researchReason").GetString());
        Assert.Equal(JsonValueKind.Null, unresolved.GetProperty("outcome").ValueKind);
        Assert.Equal("100", unresolved.GetProperty("close").GetString());
        Assert.Equal("2026-09-30", unresolved.GetProperty("marketDate").GetString());
        Assert.Contains(observations.EnumerateArray(), o => o.GetProperty("resolution").GetString() == "TERMINAL");
        using var page = JsonDocument.Parse(ResearchDelivery.PageBytes(ResearchDelivery.Page(result,
            Request(limit: 1), preimage, datasetId, Now)));
        Assert.Equal(2, page.RootElement.GetProperty("page").GetProperty("total").GetInt32());
        Assert.Single(page.RootElement.GetProperty("observations").EnumerateArray());
        Assert.Equal(datasetId, page.RootElement.GetProperty("dataset").GetProperty("datasetId").GetString());
        var json = Encoding.UTF8.GetString(ResearchDelivery.ExportBytes(result, preimage, datasetId));
        foreach (var forbidden in new[] { "shares", "investedCost", "averageCost", "mandate", "thesis", "portfolioName",
            "notes", "archivePath", "credential", "select ", "private" })
            Assert.DoesNotContain(forbidden, json, StringComparison.Ordinal);
    }

    [Fact]
    public void PageAndExportBoundsFailClosedWithoutTruncating()
    {
        var pageResult = Evaluate([Run(1)], [Row() with { Symbol = new string('x', ResearchDelivery.MaximumPageBytes + 4096) }]);
        var pagePreimage = ResearchDelivery.Preimage(pageResult);
        var pageId = ResearchDelivery.DatasetId(pagePreimage, TestContext.Current.CancellationToken);
        var pageError = Assert.Throws<ScreenerException>(() => ResearchDelivery.PageBytes(
            ResearchDelivery.Page(pageResult, Request(), pagePreimage, pageId, Now)));
        Assert.Equal("RESEARCH_BOUND_EXCEEDED", pageError.Code); Assert.Equal(503, pageError.StatusCode);
        var exportResult = Evaluate([Run(1)], [Row() with { Symbol = new string('x', ResearchDelivery.MaximumExportBytes + 4096) }]);
        var exportPreimage = ResearchDelivery.Preimage(exportResult);
        var exportError = Assert.Throws<ScreenerException>(() =>
            ResearchDelivery.ExportBytes(exportResult, exportPreimage, ResearchDelivery.DatasetId(exportPreimage, TestContext.Current.CancellationToken)));
        Assert.Equal("RESEARCH_BOUND_EXCEEDED", exportError.Code); Assert.Equal(503, exportError.StatusCode);
    }

    [Fact]
    public void EmptyAndAllUnresolvedDatasetsRemainDeliverable()
    {
        var empty = Evaluate([Run(0)], []);
        var emptyPreimage = ResearchDelivery.Preimage(empty);
        var emptyId = ResearchDelivery.DatasetId(emptyPreimage, TestContext.Current.CancellationToken);
        Assert.Equal(0, empty.Summary.Counts.N);
        var emptyPage = ResearchDelivery.Page(empty, Request(), emptyPreimage, emptyId, Now);
        Assert.Empty(emptyPage.Observations); Assert.Null(emptyPage.Page.NextCursor);
        Assert.Null(emptyPage.Summary.Metrics.Mean);
        Assert.Equal(emptyId, IdOf(Evaluate([Run(0)], [])));
        using var export = JsonDocument.Parse(ResearchDelivery.ExportBytes(empty, emptyPreimage, emptyId));
        Assert.Empty(export.RootElement.GetProperty("preimage").GetProperty("observations").EnumerateArray());
        var unresolved = Evaluate([Run(2)], [Row(1), Row(2)]);
        var unresolvedPage = ResearchDelivery.Page(unresolved, Request(), ResearchDelivery.Preimage(unresolved),
            IdOf(unresolved), Now);
        Assert.Equal(2, unresolvedPage.Observations.Count);
        Assert.All(unresolvedPage.Observations, o => Assert.Equal("UNRESOLVED", o.Resolution));
        Assert.Equal(2, unresolvedPage.Summary.Counts.U);
        Assert.Equal("0", unresolvedPage.Summary.Metrics.AvailableCoverage);
    }
}
