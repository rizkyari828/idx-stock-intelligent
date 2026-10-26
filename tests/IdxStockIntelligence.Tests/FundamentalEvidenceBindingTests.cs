using System.Text.Json;
using System.Text.Json.Nodes;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

// E1 acceptance: envelope/E01 plus FILING01, FACT01 and CONV01 shape and family rules.
// Scenario mapping: T01 (binding round-trip), T23 (admission), T24 (series identity),
// T32 (statement-only scope), T36 (closed shape/canonical-value rules) and T43
// (unsupported branches). No parser, table, source or formula is exercised.
public sealed class FundamentalEvidenceBindingTests
{
    private static readonly string Sha = new('a', 64);
    private static readonly string Uuid = "11111111-2222-3333-4444-555555555555";

    private static JsonObject Period() => new()
    {
        ["periodType"] = "DURATION",
        ["startDate"] = "2026-01-01",
        ["endDate"] = "2026-03-31",
        ["fiscalYear"] = "2026",
        ["fiscalStart"] = "2026-01-01",
        ["fiscalEnd"] = "2026-12-31",
        ["fiscalQuarter"] = 1,
        ["periodKind"] = "QUARTER",
        ["cumulative"] = false,
    };

    private static JsonObject Artifact() => new()
    {
        ["artifactId"] = Uuid,
        ["sha256"] = Sha,
        ["byteLength"] = 1024,
        ["locator"] = "archive/filing.xbrl",
    };

    private static JsonObject Ref() => new() { ["evidenceId"] = Uuid, ["revisionNumber"] = 1 };

    private static JsonObject Admission(string claim = "FinancialFiling") => new()
    {
        ["decisionId"] = Uuid,
        ["evidence"] = Artifact(),
        ["reviewedAt"] = "2026-10-01T00:00:00Z",
        ["route"] = "official-filing",
        ["allowedClaims"] = new JsonArray(claim),
        ["authorityTier"] = "T1",
        ["acquisitionAllowed"] = true,
        ["retentionAllowed"] = true,
        ["replayAllowed"] = true,
        ["validFrom"] = "2026-09-01T00:00:00Z",
        ["validTo"] = null,
    };

    private static JsonObject Receipt() => new()
    {
        ["ingestionRunId"] = Uuid,
        ["artifactId"] = Uuid,
        ["retrievedAt"] = "2026-10-01T00:00:00Z",
    };

    private static JsonObject Format() => new()
    {
        ["kind"] = "XBRL",
        ["schemaId"] = "idx-xbrl",
        ["schemaVersion"] = "1.0",
        ["taxonomyFamily"] = "idx-ifrs",
        ["taxonomyVersion"] = "2026",
    };

    private static JsonObject Filing(string claim = "FinancialFiling") => new()
    {
        ["evidenceId"] = Uuid,
        ["policyId"] = "fundamental-evidence-v0.1.0",
        ["schemaVersion"] = 1,
        ["payloadSchemaVersion"] = 1,
        ["claim"] = claim,
        ["scopeKind"] = "REPORTING_ENTITY",
        ["subject"] = new JsonObject { ["namespace"] = "issuer", ["code"] = "ABCD" },
        ["sourceId"] = "official-filing",
        ["sourceReference"] = "doc-2026-q1",
        ["assertionKey"] = "ABCD-2026Q1",
        ["revisionSeriesId"] = "official-filing\u001Ffundamental-evidence-v0.1.0/FinancialFiling/ABCD-2026Q1",
        ["revisionNumber"] = 1,
        ["supersedesRevisionNumber"] = null,
        ["lineageEvidence"] = null,
        ["authorityTier"] = "T1",
        ["publishedAt"] = null,
        ["publicationDate"] = null,
        ["publicationEvidence"] = null,
        ["receipt"] = Receipt(),
        ["knownAt"] = "2026-10-02T00:00:00Z",
        ["recordedAt"] = "2026-10-02T00:00:01Z",
        ["admission"] = Admission(claim),
        ["payload"] = new JsonObject
        {
            ["operation"] = "ASSERT",
            ["completeness"] = "FULL",
            ["value"] = new JsonObject
            {
                ["documentKey"] = "ABCD-2026Q1",
                ["artifact"] = Artifact(),
                ["reportingName"] = "PT Contoso Tbk",
                ["legalEntity"] = null,
                ["statementFamily"] = "GENERAL_CORPORATE",
                ["format"] = Format(),
                ["coveredPeriods"] = new JsonArray(Period()),
                ["documentRole"] = "ORIGINAL",
                ["revisionCoverage"] = "OBSERVED_ONLY",
            },
        },
        ["payloadSha256"] = Sha,
    };

    private static JsonObject Fact(string claim = "FundamentalFact")
    {
        var root = Filing(claim);
        root["scopeKind"] = "REPORTING_ENTITY";
        root["authorityTier"] = "T2";
        root["admission"] = Admission(claim);
        ((JsonObject)root["admission"]!)["authorityTier"] = "T2";
        ((JsonObject)root["payload"]!)["value"] = new JsonObject
        {
            ["metric"] = "Revenue",
            ["factKind"] = "REPORTED",
            ["applicability"] = "APPLICABLE",
            ["inapplicabilityReason"] = null,
            ["amount"] = "1000.50",
            ["unit"] = new JsonObject { ["kind"] = "MONETARY", ["currencyCode"] = "IDR" },
            ["period"] = Period(),
            ["accounting"] = new JsonObject { ["statementFamily"] = "GENERAL_CORPORATE" },
            ["shareBasis"] = null,
            ["definitionId"] = "revenue-declared",
            ["sourceConcept"] = new JsonObject { ["namespace"] = "idx", ["code"] = "Revenue" },
            ["filing"] = Ref(),
            ["convention"] = Ref(),
            ["comparativeOf"] = null,
            ["derivation"] = null,
        };
        root["revisionSeriesId"] = "official-filing\u001Ffundamental-evidence-v0.1.0/FundamentalFact/ABCD-2026Q1";
        return root;
    }

    private static JsonObject Convention(string claim = "FundamentalConvention")
    {
        var root = Filing(claim);
        root["scopeKind"] = "SOURCE";
        root["subject"] = new JsonObject { ["namespace"] = "source", ["code"] = "official-filing" };
        root["assertionKey"] = "mapping-1";
        root["revisionSeriesId"] = "official-filing\u001Ffundamental-evidence-v0.1.0/FundamentalConvention/mapping-1";
        ((JsonObject)root["payload"]!)["value"] = new JsonObject
        {
            ["conventionId"] = "mapping-1",
            ["version"] = "1.0",
            ["kind"] = "MAPPING",
            ["support"] = new JsonArray(Artifact()),
            ["mapping"] = new JsonObject { ["nativeConcept"] = "Revenue" },
            ["entitySecurity"] = null,
            ["formula"] = null,
        };
        return root;
    }

    private static FundamentalDecodeResult Decode(JsonNode node) =>
        FundamentalEvidenceCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(node));

    private static void AssertRejected(JsonNode node, string diagnostic)
    {
        var result = Decode(node);
        Assert.Null(result.Evidence);
        Assert.Contains(result.Diagnostics, d => d.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void T01ValidFilingBindsEnvelopeAndValue()
    {
        var result = Decode(Filing());
        Assert.True(result.IsValid, string.Join("; ", result.Diagnostics));
        var evidence = result.Evidence!;
        Assert.Equal(FundamentalClaim.FinancialFiling, evidence.Claim);
        Assert.Equal(1, evidence.RevisionNumber);
        Assert.Equal("ABCD-2026Q1", Assert.IsType<FundamentalFilingValue>(evidence.Payload.Value).DocumentKey);
        Assert.Equal("T1", evidence.AuthorityTier);
    }

    [Fact]
    public void T01ValidFactAndConventionDecode()
    {
        var fact = Decode(Fact());
        Assert.True(fact.IsValid, "fact: " + string.Join("; ", fact.Diagnostics));
        var convention = Decode(Convention());
        Assert.True(convention.IsValid, "convention: " + string.Join("; ", convention.Diagnostics));
    }

    [Fact]
    public void T36UnknownDuplicateAndMissingPropertiesAreRejected()
    {
        var unknown = Filing();
        unknown["unexpected"] = true;
        AssertRejected(unknown, "FUNDAMENTAL_MALFORMED");

        var missingNullable = Filing();
        missingNullable.Remove("lineageEvidence");
        AssertRejected(missingNullable, "FUNDAMENTAL_MALFORMED");

        var duplicate = JsonNode.Parse(JsonSerializer.Serialize(Filing()).Replace(
            "\"schemaVersion\":1,", "\"schemaVersion\":1,\"schemaVersion\":1,"))!;
        AssertRejected(duplicate, "FUNDAMENTAL_MALFORMED");
    }

    [Fact]
    public void T36NumericStringsAreStrictAndNegativeZeroIsAdmitted()
    {
        var number = Fact();
        ((JsonObject)((JsonObject)number["payload"]!)["value"]!)["amount"] = 1000.5;
        AssertRejected(number, "FUNDAMENTAL_MALFORMED");

        var negativeZero = Fact();
        ((JsonObject)((JsonObject)negativeZero["payload"]!)["value"]!)["amount"] = "-0.00";
        Assert.True(Decode(negativeZero).IsValid);

        var rounded = Fact();
        ((JsonObject)((JsonObject)rounded["payload"]!)["value"]!)["amount"] = "1e3";
        AssertRejected(rounded, "FUNDAMENTAL_NUMERIC_UNREPRESENTABLE");
    }

    [Fact]
    public void T23AdmissionMustCarryTheClaimTierAndReviewWindow()
    {
        var missing = Filing();
        missing.Remove("admission");
        AssertRejected(missing, "FUNDAMENTAL_MALFORMED");

        var wrongClaim = Filing();
        ((JsonObject)wrongClaim["admission"]!)["allowedClaims"] = new JsonArray("FundamentalFact");
        AssertRejected(wrongClaim, "FUNDAMENTAL_MALFORMED");

        var tierMismatch = Filing();
        ((JsonObject)tierMismatch["admission"]!)["authorityTier"] = "T2";
        AssertRejected(tierMismatch, "FUNDAMENTAL_MALFORMED");
    }

    [Fact]
    public void T24SeriesIdentityMustBeDerivedExactly()
    {
        var wrong = Filing();
        wrong["revisionSeriesId"] = "official-filing\u001Ffundamental-evidence-v0.1.0/FinancialFiling/other";
        AssertRejected(wrong, "FUNDAMENTAL_MALFORMED");

        var wrongPolicy = Filing();
        wrongPolicy["policyId"] = "fundamental-evidence-v0.2.0";
        AssertRejected(wrongPolicy, "FUNDAMENTAL_BINDING_UNSUPPORTED");
    }

    [Fact]
    public void T32StatementOnlyScopeRejectsDilutionAndUnsupportedKinds()
    {
        var dilution = Fact();
        ((JsonObject)((JsonObject)dilution["payload"]!)["value"]!)["metric"] = "DilutionObservation";
        AssertRejected(dilution, "FUNDAMENTAL_BINDING_UNSUPPORTED");

        var reportedFcf = Fact();
        ((JsonObject)((JsonObject)reportedFcf["payload"]!)["value"]!)["metric"] = "FreeCashFlow";
        AssertRejected(reportedFcf, "FUNDAMENTAL_BINDING_UNSUPPORTED");

        var derivedDividend = Fact();
        var derivedValue = (JsonObject)((JsonObject)derivedDividend["payload"]!)["value"]!;
        derivedValue["metric"] = "DividendPerShare";
        derivedValue["factKind"] = "DERIVED";
        AssertRejected(derivedDividend, "FUNDAMENTAL_BINDING_UNSUPPORTED");
    }

    [Fact]
    public void T43ConventionNeedsExactlyOneMatchingBranch()
    {
        var twoBranches = Convention();
        ((JsonObject)((JsonObject)twoBranches["payload"]!)["value"]!)["formula"] = new JsonObject { ["formulaId"] = "f" };
        AssertRejected(twoBranches, "FUNDAMENTAL_MALFORMED");

        var wrongKind = Convention();
        ((JsonObject)((JsonObject)wrongKind["payload"]!)["value"]!)["kind"] = "FORMULA_REFERENCE";
        AssertRejected(wrongKind, "FUNDAMENTAL_MALFORMED");
    }

    [Fact]
    public void T43ApplicabilityAndDerivationBranchesAreExclusive()
    {
        var nullAmount = Fact();
        ((JsonObject)((JsonObject)nullAmount["payload"]!)["value"]!)["amount"] = null;
        AssertRejected(nullAmount, "FUNDAMENTAL_MALFORMED");

        var reportedWithDerivation = Fact();
        ((JsonObject)((JsonObject)reportedWithDerivation["payload"]!)["value"]!)["derivation"] = new JsonObject { ["formulaId"] = "f" };
        AssertRejected(reportedWithDerivation, "FUNDAMENTAL_MALFORMED");
    }

    [Fact]
    public void T43RecordAndArrayBoundsRejectBeforeAdmission()
    {
        var oversized = Filing();
        ((JsonObject)oversized["payload"]!)["value"] = new JsonObject
        {
            ["documentKey"] = "ABCD-2026Q1",
            ["artifact"] = Artifact(),
            ["reportingName"] = new string('x', 200_000),
            ["legalEntity"] = null,
            ["statementFamily"] = "GENERAL_CORPORATE",
            ["format"] = Format(),
            ["coveredPeriods"] = new JsonArray(Period()),
            ["documentRole"] = "ORIGINAL",
            ["revisionCoverage"] = "OBSERVED_ONLY",
        };
        AssertRejected(oversized, "FUNDAMENTAL_BOUND_EXCEEDED");

        var tooManyPeriods = Filing();
        var periods = new JsonArray();
        for (var index = 0; index < 257; index++) periods.Add(Period());
        ((JsonObject)((JsonObject)tooManyPeriods["payload"]!)["value"]!)["coveredPeriods"] = periods;
        AssertRejected(tooManyPeriods, "FUNDAMENTAL_BOUND_EXCEEDED");
    }

    [Fact]
    public void ChronologyAndInstantShapeAreStrict()
    {
        var backwards = Filing();
        backwards["recordedAt"] = "2026-10-01T23:59:59Z";
        AssertRejected(backwards, "FUNDAMENTAL_CHRONOLOGY_INVALID");

        var offset = Filing();
        offset["knownAt"] = "2026-10-02T00:00:00+07:00";
        AssertRejected(offset, "FUNDAMENTAL_MALFORMED");

        var dateOnlyInstant = Filing();
        dateOnlyInstant["knownAt"] = "2026-10-02";
        AssertRejected(dateOnlyInstant, "FUNDAMENTAL_MALFORMED");
    }
}
