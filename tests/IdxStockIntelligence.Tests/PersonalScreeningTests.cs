using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Xunit;

namespace IdxStockIntelligence.Tests;

/// <summary>
/// Focused acceptance for the descriptive personal screening surface: the existing Phase B.1 feature states,
/// ordered by one already-existing feature. Fixtures are synthetic and disposable; no provider call, database,
/// canonical bar or admission path is involved.
/// </summary>
public sealed class PersonalScreeningTests
{
    private const string Symbol = "BBCA.JK";
    private const string CapturedAt = "2026-10-26T11:00:00+00:00";
    private static readonly DateOnly Today = new(2026, 10, 27);
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Through = new(2026, 9, 3);
    private static readonly DateOnly Start = new(2026, 8, 3);
    private static readonly DateOnly[] CitedClosures = [new(2026, 8, 17), new(2026, 8, 25)];
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] RankedNotRankedNoCapture =
        [PersonalScreening.Ranked, PersonalScreening.NotRanked, PersonalScreening.NoCapture];
    private static readonly string[] DescendingOrder = ["B.JK", "C.JK", "A.JK", "D.JK"];
    private static readonly string[] AscendingOrder = ["A.JK", "C.JK", "B.JK", "D.JK"];
    private static readonly int?[] DescendingRanks = [1, 2, 3, null];
    private static readonly string[] FilteredOrder = ["C.JK", "A.JK"];
    private static readonly int?[] FilteredRanks = [1, 2];
    private static readonly string[] VolumeOrder = ["A.JK", PersonalTechnicalFeatures.BenchmarkSymbol];

    [Fact]
    public void WarmupAndNoCaptureRowsNeverSortBeforeAnAvailableValue()
    {
        var rows = Build([("A.JK", Report("A.JK", 2m)), ("B.JK", Warmup("B.JK")), ("C.JK", null)]);

        Assert.Equal(RankedNotRankedNoCapture, rows.Rows.Select(row => row.Status).ToArray());
        Assert.Equal(1, rows.Rows[0].Rank);
        Assert.Null(rows.Rows[1].Rank);
        Assert.Null(rows.Rows[1].SortValue);
        Assert.Equal("WARMUP", rows.Rows[1].SortAvailability);
        Assert.Equal("INSUFFICIENT_SESSIONS", rows.Rows[1].Reason);
        Assert.Null(rows.Rows[2].Rank);
        Assert.Equal(PersonalTechnicalFeatures.MissingCapture, rows.Rows[2].Reason);
        Assert.Empty(rows.Rows[2].Features);
    }

    [Fact]
    public void DirectionReordersOnlyAvailableValuesAndWarmupStaysLast()
    {
        var entries = new (string Symbol, PersonalTechnicalFeaturesReport? Report)[]
        {
            ("A.JK", Report("A.JK", 1m)), ("B.JK", Report("B.JK", 3m)), ("C.JK", Report("C.JK", 2m)),
            ("D.JK", Warmup("D.JK"))
        };
        Assert.Equal(DescendingOrder, Build(entries).Rows.Select(row => row.Symbol).ToArray());
        Assert.Equal(DescendingRanks, Build(entries).Rows.Select(row => row.Rank).ToArray());
        Assert.Equal(AscendingOrder,
            Build(entries, direction: PersonalScreening.Ascending).Rows.Select(row => row.Symbol).ToArray());
    }

    [Fact]
    public void AvailabilityFilterKeepsOnlyRankedRowsAndRenumbersThem()
    {
        var entries = new (string Symbol, PersonalTechnicalFeaturesReport? Report)[]
            { ("A.JK", Report("A.JK", 1m)), ("B.JK", Warmup("B.JK")), ("C.JK", Report("C.JK", 2m)) };

        var filtered = Build(entries, availability: PersonalScreening.AvailableRows);

        Assert.Equal(FilteredOrder, filtered.Rows.Select(row => row.Symbol).ToArray());
        Assert.Equal(FilteredRanks, filtered.Rows.Select(row => row.Rank).ToArray());
        Assert.All(filtered.Rows, row => Assert.Equal(PersonalScreening.Ranked, row.Status));
        Assert.Equal(3, Build(entries).Rows.Count);
    }

    [Fact]
    public void NoCaptureRowIsExplicitAndCarriesNoCounts()
    {
        var row = Build([("A.JK", Report("A.JK", 1m)), ("C.JK", null)])
            .Rows.Single(item => item.Symbol == "C.JK");

        Assert.Equal(PersonalScreening.NoCapture, row.Status);
        Assert.Equal(PersonalTechnicalFeatures.MissingCapture, row.Reason);
        Assert.Null(row.MarketDate);
        Assert.Null(row.RunStart);
        Assert.Equal(0, row.Sessions);
        Assert.Equal(0, row.AvailableCount + row.WarmupCount + row.UnavailableCount);
        Assert.Null(row.SortValue);
    }

    [Fact]
    public void IndexVolumeFeatureIsUnavailableNotZeroAndSortsLast()
    {
        var stock = Report("A.JK", 1m) with
        {
            Features = Features(1m, ("volumeRatio20", "UNAVAILABLE", null, "VOLUME_BASIS_UNVERIFIED"))
        };
        var index = Report(PersonalTechnicalFeatures.BenchmarkSymbol, 1m) with
        {
            Features = Features(1m, ("volumeRatio20", "UNAVAILABLE", null, "NOT_APPLICABLE_TO_INDEX"))
        };

        var report = Build([("A.JK", stock), (PersonalTechnicalFeatures.BenchmarkSymbol, index)],
            sort: "volumeRatio20");

        Assert.Equal(VolumeOrder, report.Rows.Select(row => row.Symbol).ToArray());
        var indexRow = report.Rows.Single(row => row.Index);
        Assert.Equal(PersonalScreening.NotRanked, indexRow.Status);
        Assert.Equal("UNAVAILABLE", indexRow.SortAvailability);
        Assert.Equal("NOT_APPLICABLE_TO_INDEX", indexRow.Reason);
        Assert.Null(indexRow.SortValue);
        Assert.All(report.Rows, row => Assert.Null(row.Rank));
        Assert.Equal("VOLUME_BASIS_UNVERIFIED", report.Rows.Single(row => !row.Index).Reason);
    }

    [Fact]
    public void NumberOfAvailableWarmupAndUnavailableFeaturesIsAlwaysReported()
    {
        var row = Assert.Single(Build([("A.JK", Report("A.JK", 1m))]).Rows);

        Assert.Equal(1, row.AvailableCount);
        Assert.Equal(1, row.WarmupCount);
        Assert.Equal(11, row.UnavailableCount);
        Assert.Equal(13, row.AvailableCount + row.WarmupCount + row.UnavailableCount);
        Assert.Equal(13, row.Features.Count);
    }

    [Fact]
    public void SerializedDiagnosticsNeverCarryReadinessOrVerificationTokens()
    {
        var report = Build([("A.JK", Report("A.JK", 1m)), ("B.JK", Warmup("B.JK")), ("C.JK", null)]);

        var json = JsonSerializer.Serialize(report, WebJson);

        Assert.DoesNotMatch(new Regex(@"\bVERIFIED\b"), json);
        Assert.DoesNotMatch(new Regex(@"\bMARKET_ELIGIBLE\b"), json);
        Assert.DoesNotMatch(new Regex(@"\bDATA_READY\b"), json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(PersonalScreening.SurfaceName, root.GetProperty("surface").GetString());
        Assert.Equal(PersonalTechnicalFeatures.Source, root.GetProperty("source").GetString());
        Assert.Equal(PersonalTechnicalFeatures.SessionProof, root.GetProperty("sessionProof").GetString());
        Assert.Equal(5, root.GetProperty("limitations").GetArrayLength());
        Assert.Equal(3, root.GetProperty("rows").GetArrayLength());
        Assert.Equal("INSUFFICIENT_SESSIONS", root.GetProperty("rows")[1].GetProperty("reason").GetString());
    }

    [Fact]
    public void QueryDefaultsAndRejectsUnknownControls()
    {
        var defaults = PersonalScreening.Query(Bounds(), Today, Now);
        Assert.Equal("changePercent", defaults.Sort);
        Assert.Equal(PersonalScreening.Descending, defaults.Direction);
        Assert.Equal(PersonalScreening.AllRows, defaults.Availability);
        Assert.Equal(Today, defaults.Through);

        Assert.Equal("PERSONAL_SCREENING_QUERY_INVALID", Invalid(Bounds(("sort", "recommendationScore"))).Code);
        Assert.Equal("PERSONAL_SCREENING_QUERY_INVALID", Invalid(Bounds(("direction", "sideways"))).Code);
        Assert.Equal("PERSONAL_SCREENING_QUERY_INVALID", Invalid(Bounds(("availability", "ELIGIBLE"))).Code);
        Assert.Equal("PERSONAL_SCREENING_QUERY_INVALID", Invalid(Bounds(("limit", "10"))).Code);
        Assert.Equal("PERSONAL_SCREENING_QUERY_INVALID", Invalid(Bounds(("through", "2026-9-3"))).Code);
        Assert.Equal("PERSONAL_SCREENING_QUERY_INVALID", Invalid(Bounds(("cutoff", "yesterday"))).Code);
        Assert.Equal("PERSONAL_SCREENING_QUERY_INVALID", Invalid(Bounds(("through", "2026-10-28"))).Code);
    }

    [Fact]
    public async Task ServiceProjectsTheRetainedPanelThroughThePersonalStore()
    {
        using var fixture = new Fixture().Capture(22);
        var service = new PersonalScreeningService(new PersonalBarFileStore(fixture.Root), fixture.Holidays,
            fixture.PanelPath);

        var report = await service.ReadAsync(Query(Through, sort: "changePercent"), CancellationToken.None);

        Assert.Equal(3, report.Rows.Count);
        Assert.All(report.Rows, row => Assert.Equal(PersonalScreening.Ranked, row.Status));
        Assert.Equal(PersonalTechnicalFeatures.Source, report.Source);
        Assert.Equal(PersonalTechnicalFeatures.SessionProof, report.SessionProof);
        var index = report.Rows.Single(row => row.Index);
        Assert.Equal(PersonalTechnicalFeatures.BenchmarkSymbol, index.Symbol);
        Assert.Equal("NOT_APPLICABLE_TO_INDEX", index.Features["volumeRatio20"].Reason);
        Assert.Equal(Through, index.MarketDate);

        // No personal row carries a volume basis, so an ordering by that feature ranks nothing and stays explicit.
        var volume = await service.ReadAsync(Query(Through, sort: "volumeRatio20"), CancellationToken.None);
        Assert.All(volume.Rows, row => Assert.Equal(PersonalScreening.NotRanked, row.Status));
        Assert.All(volume.Rows, row => Assert.Null(row.Rank));
        Assert.All(volume.Rows.Where(row => !row.Index),
            row => Assert.Equal("VOLUME_BASIS_UNVERIFIED", row.Reason));

        // ema50 is a 50-session window: 22 continuous sessions keep it in warmup rather than inventing a value.
        var warmup = await service.ReadAsync(Query(Through, sort: "ema50"), CancellationToken.None);
        Assert.All(warmup.Rows, row => Assert.Equal(PersonalScreening.NotRanked, row.Status));
        Assert.All(warmup.Rows, row => Assert.Equal("INSUFFICIENT_SESSIONS", row.Reason));
    }

    private static PersonalScreeningReport Build((string Symbol, PersonalTechnicalFeaturesReport? Report)[] entries,
        string sort = "changePercent", string direction = PersonalScreening.Descending,
        string availability = PersonalScreening.AllRows) =>
        PersonalScreening.Build(Query(Through, sort, direction, availability), entries.Select(entry =>
            new PersonalScreeningEntry(new(entry.Symbol, entry.Symbol + " name"), entry.Report,
                entry.Report is null ? PersonalTechnicalFeatures.MissingCapture : null)).ToArray());

    private static PersonalTechnicalFeaturesReport Report(string symbol, decimal change) => new(
        PersonalTechnicalFeatures.Source, PersonalTechnicalFeatures.SessionProof, symbol, Through, Now, Through,
        Start, 22, "Synthetic feature projection.", Features(change));

    private static PersonalTechnicalFeaturesReport Warmup(string symbol) => new(
        PersonalTechnicalFeatures.Source, PersonalTechnicalFeatures.SessionProof, symbol, Through, Now, Through,
        Start, 22, "Synthetic feature projection.",
        Features(null, ("changePercent", "WARMUP", null, "INSUFFICIENT_SESSIONS")));

    private static Dictionary<string, TechnicalFeatureValue> Features(decimal? change,
        params (string Name, string Availability, decimal? Value, string? Reason)[] overrides)
    {
        var features = TechnicalFeatures.Names.ToDictionary(name => name,
            name => new TechnicalFeatureValue("UNAVAILABLE", null, "NOT_PROJECTED",
                TechnicalFeatures.MinimumSessions[name]), StringComparer.Ordinal);
        features["changePercent"] = new(change is null ? "WARMUP" : "AVAILABLE", change, null,
            TechnicalFeatures.MinimumSessions["changePercent"]);
        features["ema50"] = new("WARMUP", null, "INSUFFICIENT_SESSIONS", TechnicalFeatures.MinimumSessions["ema50"]);
        foreach (var item in overrides)
            features[item.Name] = new(item.Availability, item.Value, item.Reason,
                TechnicalFeatures.MinimumSessions[item.Name]);
        return features;
    }

    private static PersonalScreeningQuery Query(DateOnly through,
        string sort = "changePercent", string direction = PersonalScreening.Descending,
        string availability = PersonalScreening.AllRows) =>
        PersonalScreening.Query(Bounds(("through", through.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("sort", sort), ("direction", direction), ("availability", availability)), Today, Now);

    private static PersonalScreeningException Invalid(Dictionary<string, string?> values) =>
        Assert.Throws<PersonalScreeningException>(() => PersonalScreening.Query(values, Today, Now));

    private static Dictionary<string, string?> Bounds(params (string Key, string Value)[] values) =>
        values.ToDictionary(value => value.Key, value => (string?)value.Value, StringComparer.Ordinal);

    private static DateOnly[] Sessions(int count)
    {
        var sessions = new List<DateOnly>(count);
        for (var day = Start; sessions.Count < count; day = day.AddDays(1))
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !CitedClosures.Contains(day))
                sessions.Add(day);
        return sessions.ToArray();
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Holidays, """
                {"year":2026,"holidays":[
                  {"date":"2026-08-17","name":"Proklamasi Kemerdekaan RI","kind":"national_holiday"},
                  {"date":"2026-08-25","name":"Maulid Nabi Muhammad SAW","kind":"national_holiday"}]}
                """);
            File.WriteAllText(PanelPath, """
                {"mode":"ZERO_COST_PILOT","benchmark":"JKSE.INDX","instruments":[
                  {"symbol":"BBCA.JK","name":"Bank Central Asia Tbk"},
                  {"symbol":"TLKM.JK","name":"Telkom Indonesia Tbk"},
                  {"symbol":"JKSE.INDX","name":"Jakarta Stock Exchange Composite Index"}]}
                """);
        }

        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "idx-personal-screening-" + Guid.NewGuid().ToString("N"));
        public string Holidays => Path.Combine(Path.GetDirectoryName(Root)!, Path.GetFileName(Root) + "-holidays.json");
        public string PanelPath => Path.Combine(Path.GetDirectoryName(Root)!, Path.GetFileName(Root) + "-panel.json");

        public Fixture Capture(int sessions, decimal first = 100m, decimal step = 0.5m)
        {
            var dates = Sessions(sessions);
            for (var index = 0; index < dates.Length; index++)
                Write(dates[index], (Symbol, first + step * index), ("TLKM.JK", 3000m + 2m * index),
                    (PersonalTechnicalFeatures.BenchmarkSymbol, 5000m + 3m * index));
            return this;
        }

        private void Write(DateOnly session, params (string Symbol, decimal Close)[] rows)
        {
            var directory = Path.Combine(Root, session.Year.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);
            var payload = string.Join(",\n", rows.Select(row => $$"""
                {
                  "symbol": "{{row.Symbol}}",
                  "open": "{{Price(row.Close - 0.5m)}}",
                  "high": "{{Price(row.Close + 1m)}}",
                  "low": "{{Price(row.Close - 1m)}}",
                  "close": "{{Price(row.Close)}}",
                  "adjusted_close": "{{Price(row.Close)}}",
                  "volume": 1234567,
                  "quality_status": "UNPROVEN_PERSONAL"
                }
                """));
            File.WriteAllText(Path.Combine(directory,
                    session.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".json"),
                $$"""
                {
                  "schema_version": 1,
                  "session_date": "{{session.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}}",
                  "captured_at": "{{CapturedAt}}",
                  "session_proof": "UNPROVEN_PERSONAL",
                  "source_id": "eodhd",
                  "rows": [{{payload}}]
                }
                """);
        }

        private static string Price(decimal value) => value.ToString(CultureInfo.InvariantCulture);

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
            if (File.Exists(Holidays)) File.Delete(Holidays);
            if (File.Exists(PanelPath)) File.Delete(PanelPath);
        }
    }
}
