using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Infrastructure;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class PersonalBarsTests
{
    private static readonly Dictionary<string, string?> NoBounds = [];
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] SessionKeys = ["sessionDate", "sessionProof", "capturedAt", "rows"];
    private static readonly string[] RowKeys =
        ["symbol", "open", "high", "low", "close", "adjustedClose", "volume", "qualityStatus"];

    [Fact]
    public async Task DefaultReadKeepsExactDecimalStringsAndUnprovenProof()
    {
        using var store = new Store();
        store.Write("2026-10-26", 1, Document("2026-10-26", close: "100.50"));
        var response = await new PersonalBarFileStore(store.Root)
            .ReadAsync(PersonalBarsQuery.Resolve(NoBounds), CancellationToken.None);

        Assert.Equal("PERSONAL_FILE_STORE", response.Source);
        Assert.Equal(DateTimeKind.Utc, response.GeneratedAt.Kind);
        var session = Assert.Single(response.Sessions);
        Assert.Equal(new DateOnly(2026, 10, 26), session.SessionDate);
        Assert.Equal("UNPROVEN_PERSONAL", session.SessionProof);
        Assert.Equal("2026-10-26T11:00:00+00:00", session.CapturedAt);
        var row = Assert.Single(session.Rows);
        Assert.Equal("BBCA.JK", row.Symbol);
        Assert.Equal("100.25", row.Open);
        Assert.Equal("101.5", row.High);
        Assert.Equal("99.75", row.Low);
        Assert.Equal("100.50", row.Close);
        Assert.Equal("100.50", row.AdjustedClose);
        Assert.Equal(1234567, row.Volume);
        Assert.Equal("UNPROVEN_PERSONAL", row.QualityStatus);
    }

    [Fact]
    public async Task SerializedShapeUsesStringPricesAndNumericVolume()
    {
        using var store = new Store();
        store.Write("2026-10-26", 1, Document("2026-10-26"));
        var response = await new PersonalBarFileStore(store.Root)
            .ReadAsync(PersonalBarsQuery.Resolve(NoBounds), CancellationToken.None);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, WebJson));
        var root = json.RootElement;
        Assert.Equal("PERSONAL_FILE_STORE", root.GetProperty("source").GetString());
        Assert.EndsWith("Z", root.GetProperty("generatedAt").GetString(), StringComparison.Ordinal);
        var session = root.GetProperty("sessions")[0];
        Assert.Equal(SessionKeys, session.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("2026-10-26", session.GetProperty("sessionDate").GetString());
        Assert.Equal("UNPROVEN_PERSONAL", session.GetProperty("sessionProof").GetString());
        var row = session.GetProperty("rows")[0];
        Assert.Equal(RowKeys, row.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(JsonValueKind.String, row.GetProperty("close").ValueKind);
        Assert.Equal("100.5", row.GetProperty("close").GetString());
        Assert.Equal("100.5", row.GetProperty("adjustedClose").GetString());
        Assert.Equal(JsonValueKind.Number, row.GetProperty("volume").ValueKind);
        Assert.Equal(1234567, row.GetProperty("volume").GetInt64());
        Assert.Equal("UNPROVEN_PERSONAL", row.GetProperty("qualityStatus").GetString());
    }

    [Fact]
    public async Task HighestRetainedRevisionIsReadWithoutMixing()
    {
        using var store = new Store();
        store.Write("2026-10-26", 1, Document("2026-10-26", close: "100.5"))
            .Write("2026-10-26", 2, Document("2026-10-26", close: "101.25"))
            .Write("2026-10-26", 3, Document("2026-10-26", close: "102.25"));

        var response = await new PersonalBarFileStore(store.Root)
            .ReadAsync(PersonalBarsQuery.Resolve(NoBounds), CancellationToken.None);
        var session = Assert.Single(response.Sessions);
        var row = Assert.Single(session.Rows);
        Assert.Equal("102.25", row.Close);
        Assert.Equal("102.25", row.AdjustedClose);
    }

    [Fact]
    public async Task ProvenProofIsAlsoSurfacedVerbatim()
    {
        using var store = new Store();
        store.Write("2026-10-26", 1,
            Document("2026-10-26", proof: "PROVEN", quality: "PROVEN"));

        var session = Assert.Single((await new PersonalBarFileStore(store.Root)
            .ReadAsync(PersonalBarsQuery.Resolve(NoBounds), CancellationToken.None)).Sessions);
        Assert.Equal("PROVEN", session.SessionProof);
        Assert.Equal("PROVEN", Assert.Single(session.Rows).QualityStatus);
    }

    [Fact]
    public async Task MissingDirectoryIsAnExplicitEmptyResult()
    {
        var missing = Path.Combine(Path.GetTempPath(), "idx-personal-missing-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(missing));
        var response = await new PersonalBarFileStore(missing)
            .ReadAsync(PersonalBarsQuery.Resolve(NoBounds), CancellationToken.None);
        Assert.Equal("PERSONAL_FILE_STORE", response.Source);
        Assert.Empty(response.Sessions);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"schema_version":1,"session_date":"2026-10-26","captured_at":"2026-10-26T11:00:00+00:00","session_proof":"UNPROVEN_PERSONAL","source_id":"eodhd","rows":[{"symbol":"BBCA.JK"}]}""")]
    [InlineData("""{"schema_version":2,"session_date":"2026-10-26","captured_at":"2026-10-26T11:00:00+00:00","session_proof":"UNPROVEN_PERSONAL","source_id":"eodhd","rows":[]}""")]
    [InlineData("""{"schema_version":1,"session_date":"2026-10-26","captured_at":"2026-10-26T11:00:00+00:00","session_proof":"UNKNOWN","source_id":"eodhd","rows":[]}""")]
    [InlineData("""{"schema_version":1,"session_date":"2026-10-26","captured_at":"2026-10-26T11:00:00+00:00","session_proof":"UNPROVEN_PERSONAL","source_id":"eodhd","rows":[{"symbol":"BBCA.JK","open":"100.25","high":"101.5","low":"99.75","close":"100.5","adjusted_close":"100.5","volume":"1234567","quality_status":"UNPROVEN_PERSONAL"}]}""")]
    public async Task MalformedFileFailsTheWholeRequest(string content)
    {
        using var store = new Store();
        store.Write("2026-10-26", 1, content);
        var error = await Assert.ThrowsAsync<PersonalBarsException>(() => new PersonalBarFileStore(store.Root)
            .ReadAsync(PersonalBarsQuery.Resolve(NoBounds), CancellationToken.None));
        Assert.Equal(500, error.StatusCode);
        Assert.Equal("PERSONAL_BARS_CORRUPT", error.Code);
        Assert.Contains("2026-10-26", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionDateMustMatchTheFileName()
    {
        using var store = new Store();
        store.Write("2026-10-26", 1, Document("2026-10-26", sessionDate: "2026-10-25"));
        var error = await Assert.ThrowsAsync<PersonalBarsException>(() => new PersonalBarFileStore(store.Root)
            .ReadAsync(PersonalBarsQuery.Resolve(NoBounds), CancellationToken.None));
        Assert.Equal("PERSONAL_BARS_CORRUPT", error.Code);
    }

    [Fact]
    public void ReversedRangeIsRejected()
    {
        var error = Assert.Throws<PersonalBarsException>(() => PersonalBarsQuery.Resolve(Bounds("2026-10-27", "2026-10-26")));
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("PERSONAL_BARS_RANGE_INVALID", error.Code);
    }

    [Fact]
    public void AbsurdRangeIsRejected()
    {
        var error = Assert.Throws<PersonalBarsException>(() => PersonalBarsQuery.Resolve(Bounds("2024-01-01", "2026-10-26")));
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("PERSONAL_BARS_RANGE_INVALID", error.Code);
        Assert.Contains("366", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OneSidedAndUnknownAndMalformedBoundsAreRejected()
    {
        Assert.Equal("PERSONAL_BARS_RANGE_INVALID",
            Assert.Throws<PersonalBarsException>(() => PersonalBarsQuery.Resolve(Bounds("2026-10-26", null))).Code);
        Assert.Equal("PERSONAL_BARS_QUERY_INVALID",
            Assert.Throws<PersonalBarsException>(() => PersonalBarsQuery.Resolve(
                new Dictionary<string, string?> { ["limit"] = "7" })).Code);
        Assert.Equal("PERSONAL_BARS_QUERY_INVALID",
            Assert.Throws<PersonalBarsException>(() => PersonalBarsQuery.Resolve(Bounds("2026-9-26", "2026-10-26"))).Code);
    }

    [Fact]
    public async Task DefaultSelectsTheNewestSevenSessionsInAscendingOrder()
    {
        using var store = new Store();
        for (var day = 1; day <= 9; day++)
        {
            var session = $"2026-09-{day:D2}";
            store.Write(session, 1, Document(session));
        }
        var response = await new PersonalBarFileStore(store.Root)
            .ReadAsync(PersonalBarsQuery.Resolve(NoBounds), CancellationToken.None);
        Assert.Equal(Enumerable.Range(3, 7).Select(day => new DateOnly(2026, 9, day)).ToArray(),
            response.Sessions.Select(item => item.SessionDate).ToArray());
    }

    [Fact]
    public async Task ExplicitRangeReturnsOnlySessionsInsideIt()
    {
        using var store = new Store();
        foreach (var session in new[] { "2026-10-23", "2026-10-26", "2026-10-27" })
            store.Write(session, 1, Document(session));

        var response = await new PersonalBarFileStore(store.Root)
            .ReadAsync(PersonalBarsQuery.Resolve(Bounds("2026-10-24", "2026-10-27")), CancellationToken.None);
        Assert.Equal(new[] { new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 27) },
            response.Sessions.Select(item => item.SessionDate).ToArray());
    }

    private static Dictionary<string, string?> Bounds(string? from, string? to)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (from is not null) values["from"] = from;
        if (to is not null) values["to"] = to;
        return values;
    }

    private static string Document(string session, string close = "100.5",
        string proof = "UNPROVEN_PERSONAL", string quality = "UNPROVEN_PERSONAL",
        long volume = 1234567, string? sessionDate = null, int schemaVersion = 1)
    {
        var declared = sessionDate ?? session;
        return $$"""
            {
              "schema_version": {{schemaVersion}},
              "session_date": "{{declared}}",
              "captured_at": "2026-10-26T11:00:00+00:00",
              "session_proof": "{{proof}}",
              "source_id": "eodhd",
              "rows": [
                {
                  "symbol": "BBCA.JK",
                  "instrument_id": "6f0f6f2e-6a4b-4a4e-9d38-2f6f6c1a9b01",
                  "open": "100.25",
                  "high": "101.5",
                  "low": "99.75",
                  "close": "{{close}}",
                  "adjusted_close": "{{close}}",
                  "volume": {{volume}},
                  "content_sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "artifact_sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                  "fetched_at": "2026-10-26T10:30:00+00:00",
                  "quality_status": "{{quality}}"
                }
              ]
            }
            """;
    }

    private sealed class Store : IDisposable
    {
        public Store() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "idx-personal-" + Guid.NewGuid().ToString("N"));

        public Store Write(string session, int revision, string content)
        {
            var directory = Path.Combine(Root, session[..4]);
            Directory.CreateDirectory(directory);
            var name = revision == 1 ? session + ".json" : $"{session}.r{revision}.json";
            File.WriteAllText(Path.Combine(directory, name), content);
            return this;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
