using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

// E3 acceptance: canonical payload bytes, hash binding and literal-preserving clocks.
// Scenario mapping: T36 (canonical bytes/write identity) and T44 (clock precision).
public sealed class FundamentalCanonicalTests
{
    private static JsonElement Payload(string amount, string knownAtToken)
    {
        var node = new JsonObject
        {
            ["operation"] = "ASSERT",
            ["completeness"] = "FULL",
            ["value"] = new JsonObject
            {
                ["amount"] = amount,
                ["knownAt"] = knownAtToken,
            },
        };
        return JsonSerializer.SerializeToElement(node);
    }

    [Fact]
    public void T36PropertyOrderDoesNotChangeCanonicalBytes()
    {
        var first = JsonDocument.Parse("""{"b":1,"a":{"y":2,"x":3}}""").RootElement;
        var second = JsonDocument.Parse("""{"a":{"x":3,"y":2},"b":1}""").RootElement;
        Assert.Equal(FundamentalCanonical.CanonicalPayload(first), FundamentalCanonical.CanonicalPayload(second));
        Assert.Equal(FundamentalCanonical.PayloadHash(first), FundamentalCanonical.PayloadHash(second));
        Assert.Equal("""{"a":{"x":3,"y":2},"b":1}""", FundamentalCanonical.CanonicalPayload(first));
    }

    [Fact]
    public void T36ArrayOrderIsPreserved()
    {
        var first = JsonDocument.Parse("""{"a":[1,2]}""").RootElement;
        var second = JsonDocument.Parse("""{"a":[2,1]}""").RootElement;
        Assert.NotEqual(FundamentalCanonical.PayloadHash(first), FundamentalCanonical.PayloadHash(second));
    }

    [Fact]
    public void T36EquivalentDecimalsKeepDistinctPayloadHashes()
    {
        var one = Payload("1.0", "2026-10-02T00:00:00Z");
        var oneHundredth = Payload("1.00", "2026-10-02T00:00:00Z");
        Assert.Equal(new EvidenceDecimal("1.0").Value, new EvidenceDecimal("1.00").Value);
        Assert.NotEqual(FundamentalCanonical.PayloadHash(one), FundamentalCanonical.PayloadHash(oneHundredth));
    }

    [Fact]
    public void T44InstantLiteralSpellingSurvivesCanonicalisation()
    {
        var payload = Payload("1", "2026-10-02T00:00:00.0000001Z");
        Assert.Contains("0000001Z", FundamentalCanonical.CanonicalPayload(payload));

        var micro = DateTimeOffset.Parse("2026-10-02T00:00:00.0000000Z", CultureInfo.InvariantCulture);
        var tick = DateTimeOffset.Parse("2026-10-02T00:00:00.0000001Z", CultureInfo.InvariantCulture);
        Assert.True(tick > micro);
        Assert.Equal(1, (tick - micro).Ticks);
    }

    [Fact]
    public void T36DeclaredHashMustMatchCanonicalPayload()
    {
        var payload = Payload("1", "2026-10-02T00:00:00Z");
        var hash = FundamentalCanonical.PayloadHash(payload);
        Assert.True(FundamentalCanonical.Matches(payload, hash));
        Assert.False(FundamentalCanonical.Matches(payload, new string('a', 64)));
    }

    [Fact]
    public void T36EnvelopeRejectsAMismatchedPayloadHash()
    {
        var fixture = JsonNode.Parse("""
        {
          "evidenceId": "11111111-2222-3333-4444-555555555555",
          "policyId": "fundamental-evidence-v0.1.0",
          "schemaVersion": 1,
          "payloadSchemaVersion": 1,
          "claim": "FinancialFiling",
          "scopeKind": "REPORTING_ENTITY",
          "subject": {"namespace": "issuer", "code": "ABCD"},
          "sourceId": "official-filing",
          "sourceReference": "doc-2026-q1",
          "assertionKey": "ABCD-2026Q1",
          "revisionSeriesId": "official-filing\u001Ffundamental-evidence-v0.1.0/FinancialFiling/ABCD-2026Q1",
          "revisionNumber": 1,
          "supersedesRevisionNumber": null,
          "lineageEvidence": null,
          "authorityTier": "T1",
          "publishedAt": null,
          "publicationDate": null,
          "publicationEvidence": null,
          "receipt": {"ingestionRunId": "11111111-2222-3333-4444-555555555555",
                      "artifactId": "11111111-2222-3333-4444-555555555555",
                      "retrievedAt": "2026-10-01T00:00:00Z"},
          "knownAt": "2026-10-02T00:00:00Z",
          "recordedAt": "2026-10-02T00:00:01Z",
          "admission": {
            "decisionId": "11111111-2222-3333-4444-555555555555",
            "evidence": {"artifactId": "11111111-2222-3333-4444-555555555555", "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "byteLength": 1, "locator": "a"},
            "reviewedAt": "2026-10-01T00:00:00Z",
            "route": "official-filing",
            "allowedClaims": ["FinancialFiling"],
            "authorityTier": "T1",
            "acquisitionAllowed": true,
            "retentionAllowed": true,
            "replayAllowed": true,
            "validFrom": "2026-09-01T00:00:00Z",
            "validTo": null
          },
          "payload": {"operation": "ASSERT", "completeness": "FULL", "value": {
            "documentKey": "ABCD-2026Q1",
            "artifact": {"artifactId": "11111111-2222-3333-4444-555555555555", "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "byteLength": 1, "locator": "a"},
            "reportingName": "PT Contoso Tbk",
            "legalEntity": null,
            "statementFamily": "GENERAL_CORPORATE",
            "format": {"kind": "XBRL", "schemaId": "idx", "schemaVersion": "1", "taxonomyFamily": "idx-ifrs", "taxonomyVersion": "2026"},
            "coveredPeriods": [{"periodType": "DURATION", "startDate": "2026-01-01", "endDate": "2026-03-31", "fiscalYear": "2026", "fiscalStart": "2026-01-01", "fiscalEnd": "2026-12-31", "fiscalQuarter": 1, "periodKind": "QUARTER", "cumulative": false}],
            "documentRole": "ORIGINAL",
            "revisionCoverage": "OBSERVED_ONLY"
          }},
          "payloadSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        }
        """)!.AsObject();

        var decoded = FundamentalEvidenceCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(fixture));
        Assert.Null(decoded.Evidence);
        Assert.Contains(decoded.Diagnostics, d => d.StartsWith(FundamentalCanonical.Integrity, StringComparison.Ordinal));

        var payload = JsonSerializer.SerializeToElement(fixture["payload"]);
        fixture["payloadSha256"] = FundamentalCanonical.PayloadHash(payload);
        Assert.True(FundamentalEvidenceCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(fixture)).IsValid);
    }

    [Fact]
    public void T36GoldenCanonicalBytesArePinned()
    {
        // A4: permanent golden bytes. The fixture is generated once from the canonical
        // writer and then pinned; any change to the writer's output fails this test.
        var payload = JsonSerializer.SerializeToElement(JsonNode.Parse("""
        {
          "operation": "ASSERT",
          "completeness": "FULL",
          "value": {
            "reportingName": "PT Contoso Tbk",
            "documentKey": "ABCD-2026Q1",
            "statementFamily": "GENERAL_CORPORATE",
            "legalEntity": null,
            "format": {"taxonomyVersion": "2026", "kind": "XBRL", "schemaId": "idx", "schemaVersion": "1", "taxonomyFamily": "idx-ifrs"},
            "coveredPeriods": [{"fiscalQuarter": 1, "periodKind": "QUARTER", "cumulative": false, "periodType": "DURATION", "startDate": "2026-01-01", "endDate": "2026-03-31", "fiscalYear": "2026", "fiscalStart": "2026-01-01", "fiscalEnd": "2026-12-31"}],
            "documentRole": "ORIGINAL",
            "revisionCoverage": "OBSERVED_ONLY",
            "artifact": {"locator": "a", "byteLength": 1, "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "artifactId": "11111111-2222-3333-4444-555555555555"}
          }
        }
        """)!);

        var canonical = FundamentalCanonical.CanonicalPayload(payload);
        var hash = FundamentalCanonical.PayloadHash(payload);

        var bytesPath = FixturePath("fundamental-canonical.json");
        var hashPath = FixturePath("fundamental-canonical.sha256");

        Assert.Equal(File.ReadAllText(bytesPath, new UTF8Encoding(false)), canonical);
        Assert.Equal(File.ReadAllText(hashPath, new UTF8Encoding(false)).Trim(), hash);

        // The pinned text must hash to its own declared value: the fixture is self-consistent.
        Assert.Equal(hash, ScreenerEvidenceJson.Sha256(canonical));
        Assert.DoesNotContain("\n", canonical);
    }

    private static string FixturePath(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "fixtures", name);
            if (File.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException($"fixture not found: {name}");
    }
}
