using System.Globalization;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Xunit;

namespace IdxStockIntelligence.Tests;

/// <summary>
/// Focused acceptance for the personal technical-feature read: the existing thirteen-feature facade over
/// a calendar-continuous personal session run. Fixtures are disposable synthetic personal captures; no
/// provider call, database, canonical bar or real market value is involved.
/// </summary>
public sealed class PersonalTechnicalFeaturesTests
{
    private const string Symbol = "BBCA.JK";
    private const string CapturedAt = "2026-10-26T11:00:00+00:00";
    private static readonly DateOnly Today = new(2026, 10, 27);
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 8, 3);
    // The two cited 2026 closures inside the synthetic window: Proklamasi and Maulid Nabi.
    private static readonly DateOnly[] CitedClosures = [new(2026, 8, 17), new(2026, 8, 25)];
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] ReportKeys =
        ["source", "sessionProof", "symbol", "through", "cutoff", "marketDate", "runStart", "sessions", "warmupNote", "features"];
    private static readonly string[] ValueKeys = ["availability", "value", "reason", "minimumSessions"];
    private static readonly string[] AvailableOnTwentyTwoSessions =
        ["changePercent", "ema20", "atr14", "atrPercent", "priorHigh20", "priorLow20", "distanceToHighPercent"];

    [Fact]
    public async Task CitedClosuresDoNotBreakAContinuousPersonalRun()
    {
        using var store = new Store().Capture(22);
        var through = new DateOnly(2026, 9, 3);
        var report = await Read(store, through);

        Assert.Equal(22, report.Sessions);
        Assert.Equal(Start, report.RunStart);
        Assert.Equal(through, report.MarketDate);
        Assert.Equal(through, report.Through);
        Assert.Equal(AvailableOnTwentyTwoSessions,
            report.Features.Where(feature => feature.Value.Availability == "AVAILABLE").Select(feature => feature.Key).ToArray());
        foreach (var name in AvailableOnTwentyTwoSessions)
        {
            Assert.NotNull(report.Features[name].Value);
            Assert.Null(report.Features[name].Reason);
        }
        // 08-17 and 08-25 carry no capture and are still sessions the run crosses without a gap.
        Assert.False(File.Exists(Path.Combine(store.Root, "2026", "2026-08-17.json")));
        Assert.False(File.Exists(Path.Combine(store.Root, "2026", "2026-08-25.json")));
    }

    [Fact]
    public async Task AnUncitedWeekdayGapEndsTheRunAtTheGap()
    {
        using var store = new Store().Capture(22);
        store.Remove(new DateOnly(2026, 9, 1)); // A normal Tuesday, not a cited closure.

        var report = await Read(store, new DateOnly(2026, 9, 3));

        Assert.Equal(2, report.Sessions);
        Assert.Equal(new DateOnly(2026, 9, 2), report.RunStart);
        Assert.Equal(new DateOnly(2026, 9, 3), report.MarketDate);
        Assert.Equal("AVAILABLE", report.Features["changePercent"].Availability);
        Assert.Equal("WARMUP", report.Features["ema20"].Availability);
    }

    [Fact]
    public async Task AMissingSessionAtThroughProjectsNothingAndNeverZero()
    {
        using var store = new Store().Capture(22);
        store.Remove(new DateOnly(2026, 9, 3));

        var report = await Read(store, new DateOnly(2026, 9, 3));

        Assert.Equal(0, report.Sessions);
        Assert.Null(report.MarketDate);
        Assert.Null(report.RunStart);
        Assert.All(report.Features, feature =>
        {
            Assert.Equal("UNAVAILABLE", feature.Value.Availability);
            Assert.Equal("NO_CONTINUOUS_PERSONAL_RUN", feature.Value.Reason);
            Assert.Null(feature.Value.Value);
        });
        Assert.Contains("nothing is projected", report.WarmupNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneSessionShortOfAWindowStaysWarmupWithoutAValue()
    {
        using var store = new Store().Capture(20);
        var report = await Read(store, new DateOnly(2026, 9, 1));

        Assert.Equal(20, report.Sessions);
        Assert.Equal("AVAILABLE", report.Features["ema20"].Availability);
        var short_ = report.Features["priorHigh20"];
        Assert.Equal("WARMUP", short_.Availability);
        Assert.Equal("INSUFFICIENT_SESSIONS", short_.Reason);
        Assert.Equal(21, short_.MinimumSessions);
        Assert.Null(short_.Value);
    }

    [Fact]
    public async Task EveryProjectionStatesPersonalProvenanceAndHidesNonAvailableValues()
    {
        using var store = new Store().Capture(22);
        var report = await Read(store, new DateOnly(2026, 9, 3));

        Assert.Equal("PERSONAL_FILE_STORE", report.Source);
        Assert.Equal("UNPROVEN_PERSONAL", report.SessionProof);
        Assert.Equal("BBCA.JK", report.Symbol);
        Assert.Contains("never canonical, verified or DATA_READY", report.WarmupNote, StringComparison.Ordinal);
        Assert.Equal(13, report.Features.Count);
        Assert.All(report.Features, feature =>
        {
            Assert.Contains(feature.Key, TechnicalFeatures.Names);
            Assert.True(feature.Value.MinimumSessions > 0);
            if (feature.Value.Availability != "AVAILABLE") Assert.Null(feature.Value.Value);
        });
        // A single-symbol personal store aligns no benchmark and retains no volume basis, so those five are
        // unavailable with an explicit reason instead of a fabricated relative return or value.
        Assert.Equal("BENCHMARK_MISSING", report.Features["rs20Pp"].Reason);
        Assert.Equal("WARMUP", report.Features["rs60Pp"].Availability);
        foreach (var name in new[] { "volumeRatio20", "dailyValueProxyIdr", "monetaryLiquidity20Idr" })
        {
            Assert.Equal("UNAVAILABLE", report.Features[name].Availability);
            Assert.Equal("VOLUME_BASIS_UNVERIFIED", report.Features[name].Reason);
            Assert.Null(report.Features[name].Value);
        }
    }

    [Fact]
    public async Task SerializedShapeIsTheSameThirteenKeyFeaturesObjectAndANullIsNeverZero()
    {
        using var store = new Store().Capture(22);
        var report = await Read(store, new DateOnly(2026, 9, 3));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(report, WebJson));
        var root = json.RootElement;
        Assert.Equal(ReportKeys, root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("PERSONAL_FILE_STORE", root.GetProperty("source").GetString());
        Assert.Equal("UNPROVEN_PERSONAL", root.GetProperty("sessionProof").GetString());
        Assert.Equal("2026-09-03", root.GetProperty("through").GetString());
        Assert.Equal("2026-09-03", root.GetProperty("marketDate").GetString());
        Assert.Equal("2026-08-03", root.GetProperty("runStart").GetString());
        Assert.Equal(22, root.GetProperty("sessions").GetInt32());
        var features = root.GetProperty("features");
        Assert.Equal(13, features.EnumerateObject().Count());
        foreach (var feature in features.EnumerateObject())
        {
            Assert.Equal(ValueKeys, feature.Value.EnumerateObject().Select(property => property.Name).ToArray());
            if (feature.Value.GetProperty("availability").GetString() == "AVAILABLE") continue;
            Assert.Equal(JsonValueKind.Null, feature.Value.GetProperty("value").ValueKind);
        }
    }

    [Fact]
    public async Task HighestRetainedRevisionIsProjectedWithoutMixing()
    {
        using var store = new Store().Write(new DateOnly(2026, 9, 28), 100m)
            .Write(new DateOnly(2026, 9, 29), 100m, revision: 1)
            .Write(new DateOnly(2026, 9, 29), 105m, revision: 2);
        var last = new DateOnly(2026, 9, 29);

        var report = await Read(store, last);

        Assert.Equal(2, report.Sessions);
        Assert.Equal(5m, report.Features["changePercent"].Value);
    }

    [Fact]
    public async Task UnknownSymbolEmptyStoreAndFutureOnlyCaptureAreExplicit404()
    {
        using var store = new Store().Capture(3);
        foreach (var symbol in new[] { "BBCA", "BBRI.JK", "BBCA.JKX" })
        {
            var error = await Assert.ThrowsAsync<PersonalTechnicalFeaturesException>(() =>
                Read(store, new DateOnly(2026, 8, 5), symbol));
            Assert.Equal(404, error.StatusCode);
            Assert.Equal("PERSONAL_CAPTURE_MISSING", error.Code);
        }
        using var empty = new Store();
        Assert.Equal("PERSONAL_CAPTURE_MISSING",
            (await Assert.ThrowsAsync<PersonalTechnicalFeaturesException>(() => Read(empty, new DateOnly(2026, 8, 5)))).Code);
        // A capture after through says nothing about through; it is not a value and not a zero.
        Assert.Equal("PERSONAL_CAPTURE_MISSING",
            (await Assert.ThrowsAsync<PersonalTechnicalFeaturesException>(() => Read(store, new DateOnly(2026, 8, 2)))).Code);
    }

    [Fact]
    public async Task SymbolMatchingIsTrimmedUppercasedAndNeverFuzzy()
    {
        using var store = new Store().Capture(3);
        Assert.Equal(Symbol, (await Read(store, new DateOnly(2026, 8, 5), " bbca.jk ")).Symbol);
    }

    [Fact]
    public async Task ACaptureRecordedAfterTheCutoffIsNotVisible()
    {
        using var store = new Store().Capture(3);
        var early = new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero);
        Assert.Equal("PERSONAL_CAPTURE_MISSING",
            (await Assert.ThrowsAsync<PersonalTechnicalFeaturesException>(() => Read(store, new DateOnly(2026, 8, 5), cutoff: early))).Code);
        var later = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(3, (await Read(store, new DateOnly(2026, 8, 5), cutoff: later)).Sessions);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task AnInvalidPersonalPriceFailsClosedInsteadOfBecomingAValue(string close)
    {
        using var store = new Store().Capture(2);
        store.Write(new DateOnly(2026, 8, 5), 100m, closeText: close);

        var error = await Assert.ThrowsAsync<PersonalBarsException>(() => Read(store, new DateOnly(2026, 8, 5)));

        Assert.Equal(500, error.StatusCode);
        Assert.Equal("PERSONAL_BARS_CORRUPT", error.Code);
    }

    [Fact]
    public async Task AMissingCitedCalendarFailsClosedInsteadOfShorteningTheRun()
    {
        using var store = new Store().Capture(22);
        var service = new PersonalTechnicalFeatureService(new PersonalBarFileStore(store.Root),
            Path.Combine(store.Root, "absent-holidays.json"));

        var error = await Assert.ThrowsAsync<PersonalTechnicalFeaturesException>(() =>
            service.ReadAsync(Query(new DateOnly(2026, 9, 3)), CancellationToken.None));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("PERSONAL_TECHNICAL_CALENDAR_UNAVAILABLE", error.Code);
    }

    [Fact]
    public void QueryRejectsUnknownParametersBadBoundsAndUnmatchableSymbols()
    {
        Assert.Equal("PERSONAL_TECHNICAL_QUERY_INVALID", Invalid(Bounds(("through", "2026-09-03"), ("limit", "1"))).Code);
        Assert.Equal("PERSONAL_TECHNICAL_QUERY_INVALID", Invalid(Bounds(("through", "2026-9-3"))).Code);
        Assert.Equal("PERSONAL_TECHNICAL_QUERY_INVALID", Invalid(Bounds(("through", "2026-10-28"))).Code);
        Assert.Equal("PERSONAL_TECHNICAL_QUERY_INVALID", Invalid(Bounds(("cutoff", "yesterday"))).Code);
        Assert.Equal("PERSONAL_TECHNICAL_QUERY_INVALID", Invalid(Bounds(("cutoff", "2026-10-28T00:00:00+00:00"))).Code);
        Assert.Equal("PERSONAL_TECHNICAL_QUERY_INVALID", Invalid(Bounds(), "BBCA JK").Code);
        Assert.Equal("PERSONAL_TECHNICAL_QUERY_INVALID", Invalid(Bounds(), new string('A', 33)).Code);
        Assert.Equal(new DateOnly(2026, 10, 27), PersonalTechnicalFeatures.Query("bbca.jk", Bounds(), Today, Now).Through);
    }

    [Fact]
    public void TheShippedCalendarIsTheCitedTwentyTwoClosureFixture()
    {
        var calendar = DataHealthCalendar.Load("holidays-2026.json");
        Assert.Equal(22, calendar.Count);
        Assert.All(CitedClosures, day => Assert.True(calendar.ContainsKey(day)));
    }

    private static PersonalTechnicalFeaturesException Invalid(Dictionary<string, string?> values, string symbol = Symbol) =>
        Assert.Throws<PersonalTechnicalFeaturesException>(() => PersonalTechnicalFeatures.Query(symbol, values, Today, Now));

    private static Dictionary<string, string?> Bounds(params (string Key, string Value)[] values) =>
        values.ToDictionary(value => value.Key, value => (string?)value.Value, StringComparer.Ordinal);

    private static PersonalTechnicalQuery Query(DateOnly through, string symbol = Symbol,
        DateTimeOffset? cutoff = null) =>
        PersonalTechnicalFeatures.Query(symbol,
            Bounds(("through", through.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ("cutoff", (cutoff ?? Now).ToString("O", CultureInfo.InvariantCulture))), Today, Now);

    private static Task<PersonalTechnicalFeaturesReport> Read(Store store, DateOnly through,
        string symbol = Symbol, DateTimeOffset? cutoff = null, string? holidayPath = null) =>
        new PersonalTechnicalFeatureService(new PersonalBarFileStore(store.Root), holidayPath ?? store.Holidays)
            .ReadAsync(Query(through, symbol, cutoff), CancellationToken.None);

    private static DateOnly[] Sessions(int count)
    {
        var sessions = new List<DateOnly>(count);
        for (var day = Start; sessions.Count < count; day = day.AddDays(1))
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !CitedClosures.Contains(day))
                sessions.Add(day);
        return sessions.ToArray();
    }

    [Fact]
    public async Task BenchmarkRowsUnlockRelativeStrength()
    {
        using var store = new Store().CaptureWithBenchmark(22);
        var report = await Read(store, new DateOnly(2026, 9, 3));

        // 22 sessions satisfy the 21-point RS20 window; RS60 still needs 61.
        Assert.Equal("AVAILABLE", report.Features["rs20Pp"].Availability);
        Assert.NotNull(report.Features["rs20Pp"].Value);
        Assert.Equal("WARMUP", report.Features["rs60Pp"].Availability);
        Assert.Null(report.Features["rs60Pp"].Value);
    }

    [Fact]
    public async Task AMissingBenchmarkRowIsReportedNotFabricated()
    {
        using var store = new Store().CaptureWithBenchmark(22);
        store.RemoveBenchmarkRow(new DateOnly(2026, 9, 2)); // inside the 21-point RS20 window

        var report = await Read(store, new DateOnly(2026, 9, 3));

        Assert.Equal("UNAVAILABLE", report.Features["rs20Pp"].Availability);
        Assert.Equal("BENCHMARK_MISSING", report.Features["rs20Pp"].Reason);
        Assert.Null(report.Features["rs20Pp"].Value);
    }

    private sealed class Store : IDisposable
    {
        public Store()
        {
            Directory.CreateDirectory(Root);
            // The cited closures are a fixture here, so a test never infers them from provider data.
            File.WriteAllText(Holidays, """
                {"year":2026,"holidays":[
                  {"date":"2026-08-17","name":"Proklamasi Kemerdekaan RI","kind":"national_holiday"},
                  {"date":"2026-08-25","name":"Maulid Nabi Muhammad SAW","kind":"national_holiday"}]}
                """);
        }

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "idx-personal-technical-" + Guid.NewGuid().ToString("N"));
        public string Holidays => Path.Combine(Path.GetDirectoryName(Root)!, Path.GetFileName(Root) + "-holidays.json");

        public Store CaptureWithBenchmark(int sessions, decimal first = 100m, decimal step = 0.5m,
            decimal benchmarkFirst = 5000m, decimal benchmarkStep = 2m)
        {
            var dates = Sessions(sessions);
            for (var index = 0; index < dates.Length; index++)
                WriteWithBenchmark(dates[index], first + step * index, benchmarkFirst + benchmarkStep * index);
            return this;
        }

        public Store WriteWithBenchmark(DateOnly session, decimal close, decimal benchmarkClose)
        {
            var directory = Path.Combine(Root, session.Year.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, session.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".json"),
                DocumentWithBenchmark(session, close, benchmarkClose));
            return this;
        }

        public Store RemoveBenchmarkRow(DateOnly session)
        {
            var directory = Path.Combine(Root, session.Year.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(directory, session.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".json"),
                DocumentWithoutBenchmark(session, 100m + 0.5m * Sessions(1)[0].DayNumber));
            return this;
        }

        private static string DocumentWithBenchmark(DateOnly session, decimal close, decimal benchmarkClose) => $$"""
            {
              "schema_version": 1,
              "session_date": "{{session.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}}",
              "captured_at": "{{CapturedAt}}",
              "session_proof": "UNPROVEN_PERSONAL",
              "source_id": "eodhd",
              "rows": [
                {{Row(session, close, Symbol, 1234567)}},
                {{Row(session, benchmarkClose, PersonalTechnicalFeatures.BenchmarkSymbol, 999999)}}
              ]
            }
            """;

        private static string DocumentWithoutBenchmark(DateOnly session, decimal close) => $$"""
            {
              "schema_version": 1,
              "session_date": "{{session.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}}",
              "captured_at": "{{CapturedAt}}",
              "session_proof": "UNPROVEN_PERSONAL",
              "source_id": "eodhd",
              "rows": [
                {{Row(session, close, Symbol, 1234567)}}
              ]
            }
            """;

        private static string Row(DateOnly session, decimal close, string symbol, long volume) => $$"""
            {
              "symbol": "{{symbol}}",
              "instrument_id": "6f0f6f2e-6a4b-4a4e-9d38-2f6f6c1a9b01",
              "open": "{{Price(close - 0.5m)}}",
              "high": "{{Price(close + 1m)}}",
              "low": "{{Price(close - 1m)}}",
              "close": "{{Price(close)}}",
              "adjusted_close": "{{Price(close)}}",
              "volume": {{volume}},
              "content_sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "artifact_sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
              "fetched_at": "2026-10-26T10:30:00+00:00",
              "quality_status": "UNPROVEN_PERSONAL"
            }
            """;

        public Store Capture(int sessions, decimal first = 100m, decimal step = 0.5m)
        {
            var dates = Sessions(sessions);
            for (var index = 0; index < dates.Length; index++) Write(dates[index], first + step * index);
            return this;
        }

        public Store Write(DateOnly session, decimal close, int revision = 1, string symbol = Symbol,
            string capturedAt = CapturedAt, long volume = 1234567, string? closeText = null)
        {
            var directory = Path.Combine(Root, session.Year.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);
            var name = revision == 1 ? session.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".json"
                : $"{session.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.r{revision}.json";
            File.WriteAllText(Path.Combine(directory, name), Document(session, close, symbol, capturedAt, volume, closeText));
            return this;
        }

        public Store Remove(DateOnly session)
        {
            File.Delete(Path.Combine(Root, session.Year.ToString(CultureInfo.InvariantCulture),
                session.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".json"));
            return this;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
            if (File.Exists(Holidays)) File.Delete(Holidays);
        }

        private static string Document(DateOnly session, decimal close, string symbol, string capturedAt,
            long volume, string? closeText) => $$"""
            {
              "schema_version": 1,
              "session_date": "{{session.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}}",
              "captured_at": "{{capturedAt}}",
              "session_proof": "UNPROVEN_PERSONAL",
              "source_id": "eodhd",
              "rows": [
                {
                  "symbol": "{{symbol}}",
                  "instrument_id": "6f0f6f2e-6a4b-4a4e-9d38-2f6f6c1a9b01",
                  "open": "{{Price(close - 0.5m)}}",
                  "high": "{{Price(close + 1m)}}",
                  "low": "{{Price(close - 1m)}}",
                  "close": "{{closeText ?? Price(close)}}",
                  "adjusted_close": "{{Price(close)}}",
                  "volume": {{volume}},
                  "content_sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "artifact_sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                  "fetched_at": "2026-10-26T10:30:00+00:00",
                  "quality_status": "UNPROVEN_PERSONAL"
                }
              ]
            }
            """;

        private static string Price(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
