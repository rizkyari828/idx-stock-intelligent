using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IdxStockIntelligence.Application;

// E1: strict envelope and three typed bindings from Fundamental Evidence Binding V0.1
// §§18.1–18.7 (policy fundamental-evidence-v0.1.0). Shape and family rules only:
// periods, dimensions, mapped concepts and admission semantics stay in E4.
#pragma warning disable CA1707
public enum FundamentalClaim { FinancialFiling, FundamentalFact, FundamentalConvention }
public enum FundamentalOperation { ASSERT, CANCEL }
public enum FundamentalCompleteness { FULL, PARTIAL }
#pragma warning restore CA1707

public sealed record EntityKey(string Namespace, string Code);

public abstract record FundamentalClaimValue;

public sealed record FundamentalFilingValue(string DocumentKey, JsonElement Artifact, string ReportingName,
    EntityKey? LegalEntity, string StatementFamily, JsonElement Format, IReadOnlyList<JsonElement> CoveredPeriods,
    string DocumentRole, string RevisionCoverage) : FundamentalClaimValue;

public sealed record FundamentalFactValue(string Metric, string FactKind, string Applicability,
    string? InapplicabilityReason, EvidenceDecimal? Amount, JsonElement Unit, JsonElement Period,
    JsonElement Accounting, JsonElement? ShareBasis, string DefinitionId, JsonElement? SourceConcept,
    JsonElement? Filing, JsonElement Convention, JsonElement? ComparativeOf, JsonElement? Derivation)
    : FundamentalClaimValue;

public sealed record FundamentalConventionValue(string ConventionId, string Version, string Kind,
    IReadOnlyList<JsonElement> Support, JsonElement? Mapping, JsonElement? EntitySecurity, JsonElement? Formula)
    : FundamentalClaimValue;

public sealed record FundamentalPayload(FundamentalOperation Operation, FundamentalCompleteness Completeness,
    FundamentalClaimValue? Value);

public sealed record FundamentalEvidence(Guid EvidenceId, string PolicyId, int SchemaVersion, int PayloadSchemaVersion,
    FundamentalClaim Claim, string ScopeKind, EntityKey Subject, string SourceId, string SourceReference,
    string AssertionKey, string RevisionSeriesId, long RevisionNumber, long? SupersedesRevisionNumber,
    JsonElement? LineageEvidence, string AuthorityTier, DateTimeOffset? PublishedAt, DateOnly? PublicationDate,
    JsonElement? PublicationEvidence, JsonElement? Receipt, DateTimeOffset KnownAt, DateTimeOffset RecordedAt,
    JsonElement Admission, FundamentalPayload Payload, string PayloadSha256);

public sealed record FundamentalDecodeResult(FundamentalEvidence? Evidence, IReadOnlyList<string> Diagnostics)
{
    public bool IsValid => Evidence is not null && Diagnostics.Count == 0;
}

/// <summary>Strict decoder for the frozen V0.1 fundamental envelope. Rejects rather than repairs.</summary>
public static class FundamentalEvidenceCodec
{
    public const string PolicyId = "fundamental-evidence-v0.1.0";
    public const int EnvelopeSchemaVersion = 1;
    public const int PayloadSchemaVersion = 1;
    public const int MaxRecordBytes = 131072;
    public const int MaxDepth = 16;
    public const int MaxArrayItems = 256;

    private const string Malformed = "FUNDAMENTAL_MALFORMED";
    private const string Unsupported = "FUNDAMENTAL_BINDING_UNSUPPORTED";
    private const string BoundExceeded = "FUNDAMENTAL_BOUND_EXCEEDED";
    private const string Numeric = "FUNDAMENTAL_NUMERIC_UNREPRESENTABLE";
    private const string Chronology = "FUNDAMENTAL_CHRONOLOGY_INVALID";

    private static readonly HashSet<string> Metrics = new(StringComparer.Ordinal)
    {
        "Revenue", "OperatingProfit", "NetIncome", "EarningsPerShare", "OperatingCashFlow", "CapitalExpenditure",
        "FreeCashFlow", "TotalAssets", "TotalLiabilities", "TotalEquity", "CashAndCashEquivalents", "ShortTermDebt",
        "LongTermDebt", "TotalDebt", "SharesOutstanding", "CashDividend", "DividendPerShare", "OtherReportedConcept",
    };

    private static readonly string[] EnvelopeFields =
    [
        "evidenceId", "policyId", "schemaVersion", "payloadSchemaVersion", "claim", "scopeKind", "subject", "sourceId",
        "sourceReference", "assertionKey", "revisionSeriesId", "revisionNumber", "supersedesRevisionNumber",
        "lineageEvidence", "authorityTier", "publishedAt", "publicationDate", "publicationEvidence", "receipt",
        "knownAt", "recordedAt", "admission", "payload", "payloadSha256",
    ];

    private static readonly string[] PayloadFields = ["operation", "completeness", "value"];
    private static readonly string[] RefFields = ["evidenceId", "revisionNumber"];
    private static readonly string[] ArtifactFields = ["artifactId", "sha256", "byteLength", "locator"];
    private static readonly string[] EntityKeyFields = ["namespace", "code"];
    private static readonly string[] ReceiptFields = ["ingestionRunId", "artifactId", "retrievedAt"];
    private static readonly string[] AdmissionFields =
    [
        "decisionId", "evidence", "reviewedAt", "route", "allowedClaims", "authorityTier", "acquisitionAllowed",
        "retentionAllowed", "replayAllowed", "validFrom", "validTo",
    ];
    private static readonly string[] FilingFields =
    [
        "documentKey", "artifact", "reportingName", "legalEntity", "statementFamily", "format", "coveredPeriods",
        "documentRole", "revisionCoverage",
    ];
    private static readonly string[] FactFields =
    [
        "metric", "factKind", "applicability", "inapplicabilityReason", "amount", "unit", "period", "accounting",
        "shareBasis", "definitionId", "sourceConcept", "filing", "convention", "comparativeOf", "derivation",
    ];
    private static readonly string[] ConventionFields =
        ["conventionId", "version", "kind", "support", "mapping", "entitySecurity", "formula"];

    private static readonly Regex InstantPattern = new(@"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z\z", RegexOptions.CultureInvariant);
    private static readonly Regex HashPattern = new(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant);

    public static FundamentalDecodeResult Decode(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > MaxRecordBytes) return Failure(BoundExceeded);
        JsonDocument document;
        try { document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { MaxDepth = MaxDepth, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow }); }
        catch (JsonException) { return Failure(Malformed); }
        using (document) return Decode(document.RootElement);
    }

    public static FundamentalDecodeResult Decode(JsonElement root)
    {
        var diagnostics = new List<string>();
        var ctx = new Context(diagnostics);
        if (root.ValueKind != JsonValueKind.Object || !Closed(root, "", EnvelopeFields, ctx)) return Failure(diagnostics);

        var evidenceId = Uuid(root, "evidenceId", "/evidenceId", ctx);
        var policyId = Code(root, "policyId", "/policyId", ctx);
        var schemaVersion = Int32(root, "schemaVersion", "/schemaVersion", ctx);
        var payloadSchemaVersion = Int32(root, "payloadSchemaVersion", "/payloadSchemaVersion", ctx);
        var claimToken = Token(root, "claim", "/claim", ["FinancialFiling", "FundamentalFact", "FundamentalConvention"], ctx);
        var scopeKind = Token(root, "scopeKind", "/scopeKind", ["REPORTING_ENTITY", "SOURCE"], ctx);
        var subject = EntityKeyValue(root, "subject", "/subject", ctx);
        var sourceId = Code(root, "sourceId", "/sourceId", ctx);
        var sourceReference = Code(root, "sourceReference", "/sourceReference", ctx);
        var assertionKey = Code(root, "assertionKey", "/assertionKey", ctx);
        var revisionSeriesId = RevisionSeries(root, "/revisionSeriesId", ctx);
        var revisionNumber = Int64(root, "revisionNumber", "/revisionNumber", ctx);
        var supersedes = NullableInt64(root, "supersedesRevisionNumber", "/supersedesRevisionNumber", ctx);
        var lineage = NullableElement(root, "lineageEvidence", "/lineageEvidence", ctx);
        var authorityTier = Token(root, "authorityTier", "/authorityTier", ["T1", "T2", "T3", "T4", "T5"], ctx);
        var publishedAt = NullableInstant(root, "publishedAt", "/publishedAt", ctx);
        var publicationDate = NullableDate(root, "publicationDate", "/publicationDate", ctx);
        var publicationEvidence = NullableElement(root, "publicationEvidence", "/publicationEvidence", ctx);
        var receipt = NullableElement(root, "receipt", "/receipt", ctx);
        var knownAt = Instant(root, "knownAt", "/knownAt", ctx);
        var recordedAt = Instant(root, "recordedAt", "/recordedAt", ctx);
        var admissionElement = Element(root, "admission", "/admission", ctx);
        var payloadElement = Element(root, "payload", "/payload", ctx);
        var payloadSha256 = Hash(root, "payloadSha256", "/payloadSha256", ctx);
        if (payloadSha256 is not null && payloadElement is not null &&
            !FundamentalCanonical.Matches(payloadElement.Value, payloadSha256))
            ctx.Add("/payloadSha256", FundamentalCanonical.Integrity);

        if (policyId is not null && policyId != PolicyId) ctx.Add("/policyId", Unsupported);
        if (schemaVersion is not null && schemaVersion != EnvelopeSchemaVersion) ctx.Add("/schemaVersion", Unsupported);
        if (payloadSchemaVersion is not null && payloadSchemaVersion != PayloadSchemaVersion) ctx.Add("/payloadSchemaVersion", Unsupported);
        if (knownAt is not null && recordedAt is not null && recordedAt < knownAt) ctx.Add("/recordedAt", Chronology);
        if (publishedAt is not null && publicationDate is not null) ctx.Add("/publicationDate", Malformed);
        if ((publishedAt is not null || publicationDate is not null) && publicationEvidence is null) ctx.Add("/publicationEvidence", Malformed);

        var claim = claimToken switch
        {
            "FinancialFiling" => (FundamentalClaim?)FundamentalClaim.FinancialFiling,
            "FundamentalFact" => FundamentalClaim.FundamentalFact,
            "FundamentalConvention" => FundamentalClaim.FundamentalConvention,
            _ => null,
        };

        if (claim is not null && scopeKind is not null)
        {
            var expected = claim == FundamentalClaim.FundamentalConvention ? "SOURCE" : "REPORTING_ENTITY";
            if (scopeKind != expected) ctx.Add("/scopeKind", Malformed);
        }
        if (claim == FundamentalClaim.FundamentalConvention && subject is not null && sourceId is not null &&
            (subject.Namespace != "source" || subject.Code != sourceId)) ctx.Add("/subject", Malformed);
        if (claim is not null && sourceId is not null && assertionKey is not null && revisionSeriesId is not null)
        {
            var expected = sourceId + "\u001F" + PolicyId + "/" + claim + "/" + assertionKey;
            if (revisionSeriesId != expected) ctx.Add("/revisionSeriesId", Malformed);
        }
        if (revisionNumber is not null && revisionNumber < 1) ctx.Add("/revisionNumber", Malformed);
        if (supersedes is not null && revisionNumber is not null && supersedes >= revisionNumber) ctx.Add("/supersedesRevisionNumber", Malformed);

        Guid? admissionDecision = null;
        string? admissionTier = null;
        string[]? admissionClaims = null;
        if (admissionElement is not null)
        {
            Closed(admissionElement.Value, "/admission", AdmissionFields, ctx);
            admissionDecision = Uuid(admissionElement.Value, "decisionId", "/admission/decisionId", ctx);
            Artifact(admissionElement.Value, "evidence", "/admission/evidence", ctx);
            var reviewedAt = Instant(admissionElement.Value, "reviewedAt", "/admission/reviewedAt", ctx);
            Code(admissionElement.Value, "route", "/admission/route", ctx);
            admissionClaims = Tokens(admissionElement.Value, "allowedClaims", "/admission/allowedClaims", ctx);
            admissionTier = Token(admissionElement.Value, "authorityTier", "/admission/authorityTier", ["T1", "T2", "T3", "T4", "T5"], ctx);
            Bool(admissionElement.Value, "acquisitionAllowed", "/admission/acquisitionAllowed", ctx);
            Bool(admissionElement.Value, "retentionAllowed", "/admission/retentionAllowed", ctx);
            Bool(admissionElement.Value, "replayAllowed", "/admission/replayAllowed", ctx);
            var validFrom = Instant(admissionElement.Value, "validFrom", "/admission/validFrom", ctx);
            var validTo = NullableInstant(admissionElement.Value, "validTo", "/admission/validTo", ctx);
            if (validFrom is not null && validTo is not null && validTo < validFrom) ctx.Add("/admission/validTo", Chronology);
            if (reviewedAt is not null && knownAt is not null && reviewedAt > knownAt) ctx.Add("/admission/reviewedAt", Chronology);
            if (validFrom is not null && knownAt is not null && knownAt < validFrom) ctx.Add("/knownAt", Chronology);
            if (validTo is not null && knownAt is not null && knownAt > validTo) ctx.Add("/knownAt", Chronology);
        }
        if (authorityTier is not null && admissionTier is not null && authorityTier != admissionTier) ctx.Add("/authorityTier", Malformed);
        if (claim is not null && admissionClaims is not null && !admissionClaims.Contains(claim.ToString())) ctx.Add("/admission/allowedClaims", Malformed);
        if (authorityTier == "T5") ctx.Add("/authorityTier", Malformed);

        FundamentalPayload? payload = null;
        if (payloadElement is not null && claim is not null)
            payload = Payload(payloadElement.Value, "/payload", claim.Value, ctx);

        if (receipt is not null)
        {
            Closed(receipt.Value, "/receipt", ReceiptFields, ctx);
            Uuid(receipt.Value, "ingestionRunId", "/receipt/ingestionRunId", ctx);
            Uuid(receipt.Value, "artifactId", "/receipt/artifactId", ctx);
            var retrievedAt = Instant(receipt.Value, "retrievedAt", "/receipt/retrievedAt", ctx);
            if (retrievedAt is not null && knownAt is not null && retrievedAt > knownAt) ctx.Add("/receipt/retrievedAt", Chronology);
        }
        if (lineage is not null) Artifact(lineage.Value, "", "/lineageEvidence", ctx);

        var requiresReceipt = claim is FundamentalClaim.FinancialFiling or FundamentalClaim.FundamentalConvention ||
            payload?.Value is FundamentalFactValue { FactKind: "REPORTED" };
        if (requiresReceipt && receipt is null) ctx.Add("/receipt", Malformed);
        if (payload?.Value is FundamentalFactValue { FactKind: "DERIVED" } && receipt is not null) ctx.Add("/receipt", Malformed);

        if (ctx.Diagnostics.Count > 0 || claim is null || payload is null || evidenceId is null || policyId is null ||
            schemaVersion is null || payloadSchemaVersion is null || scopeKind is null || subject is null ||
            sourceId is null || sourceReference is null || assertionKey is null || revisionSeriesId is null ||
            revisionNumber is null || authorityTier is null || knownAt is null || recordedAt is null ||
            admissionElement is null || payloadSha256 is null)
            return Failure(ctx.Diagnostics);

        return new FundamentalDecodeResult(new FundamentalEvidence(evidenceId.Value, policyId, schemaVersion.Value,
            payloadSchemaVersion.Value, claim.Value, scopeKind, subject, sourceId, sourceReference, assertionKey,
            revisionSeriesId, revisionNumber.Value, supersedes, lineage, authorityTier, publishedAt, publicationDate,
            publicationEvidence, receipt, knownAt.Value, recordedAt.Value, admissionElement.Value, payload,
            payloadSha256), ctx.Diagnostics);
    }

    private static FundamentalPayload? Payload(JsonElement element, string pointer, FundamentalClaim claim, Context ctx)
    {
        if (!Closed(element, pointer, PayloadFields, ctx)) return null;
        var operation = Token(element, "operation", pointer + "/operation", ["ASSERT", "CANCEL"], ctx);
        var completeness = Token(element, "completeness", pointer + "/completeness", ["FULL", "PARTIAL"], ctx);
        var value = Required(element, "value", pointer, ctx);
        if (operation is null || completeness is null || value is null) return null;

        var operationValue = operation == "ASSERT" ? FundamentalOperation.ASSERT : FundamentalOperation.CANCEL;
        var completenessValue = completeness == "FULL" ? FundamentalCompleteness.FULL : FundamentalCompleteness.PARTIAL;

        if (operationValue == FundamentalOperation.CANCEL)
        {
            if (value.Value.ValueKind != JsonValueKind.Null) ctx.Add(pointer + "/value", Malformed);
            if (completenessValue != FundamentalCompleteness.FULL) ctx.Add(pointer + "/completeness", Malformed);
            return new FundamentalPayload(operationValue, completenessValue, null);
        }
        if (value.Value.ValueKind != JsonValueKind.Object) { ctx.Add(pointer + "/value", Malformed); return null; }

        FundamentalClaimValue? claimValue = claim switch
        {
            FundamentalClaim.FinancialFiling => Filing(value.Value, pointer + "/value", ctx),
            FundamentalClaim.FundamentalFact => Fact(value.Value, pointer + "/value", ctx),
            _ => Convention(value.Value, pointer + "/value", ctx),
        };
        return claimValue is null ? null : new FundamentalPayload(operationValue, completenessValue, claimValue);
    }

    private static FundamentalFilingValue? Filing(JsonElement element, string pointer, Context ctx)
    {
        if (!Closed(element, pointer, FilingFields, ctx)) return null;
        var documentKey = Code(element, "documentKey", pointer + "/documentKey", ctx);
        Artifact(element, "artifact", pointer + "/artifact", ctx);
        var reportingName = Text(element, "reportingName", pointer + "/reportingName", ctx);
        var legalEntity = NullableEntityKey(element, "legalEntity", pointer + "/legalEntity", ctx);
        var statementFamily = Token(element, "statementFamily", pointer + "/statementFamily",
            ["GENERAL_CORPORATE", "BANK", "INSURANCE", "OTHER", "UNKNOWN"], ctx);
        var format = Required(element, "format", pointer, ctx);
        if (format is not null) Format(format.Value, pointer + "/format", ctx);
        var periods = Array(element, "coveredPeriods", pointer + "/coveredPeriods", 1, MaxArrayItems, ctx);
        var documentRole = Token(element, "documentRole", pointer + "/documentRole",
            ["ORIGINAL", "CORRECTION", "RESTATEMENT", "UNKNOWN"], ctx);
        var revisionCoverage = Token(element, "revisionCoverage", pointer + "/revisionCoverage", ["OBSERVED_ONLY"], ctx);
        if (documentKey is null || reportingName is null || statementFamily is null || format is null || periods is null ||
            documentRole is null || revisionCoverage is null) return null;
        foreach (var period in periods) Period(period, pointer + "/coveredPeriods", ctx);
        return new FundamentalFilingValue(documentKey, element.GetProperty("artifact"), reportingName, legalEntity,
            statementFamily, format.Value, periods, documentRole, revisionCoverage);
    }

    private static FundamentalFactValue? Fact(JsonElement element, string pointer, Context ctx)
    {
        if (!Closed(element, pointer, FactFields, ctx)) return null;
        var metric = Code(element, "metric", pointer + "/metric", ctx);
        var factKind = Token(element, "factKind", pointer + "/factKind", ["REPORTED", "DERIVED"], ctx);
        var applicability = Token(element, "applicability", pointer + "/applicability", ["APPLICABLE", "INAPPLICABLE"], ctx);
        var reason = NullableText(element, "inapplicabilityReason", pointer + "/inapplicabilityReason", ctx);
        var amount = NullableDecimal(element, "amount", pointer + "/amount", ctx);
        var unit = Required(element, "unit", pointer, ctx);
        var period = Required(element, "period", pointer, ctx);
        var accounting = Required(element, "accounting", pointer, ctx);
        var shareBasis = NullableElement(element, "shareBasis", pointer + "/shareBasis", ctx);
        var definitionId = Code(element, "definitionId", pointer + "/definitionId", ctx);
        var sourceConcept = NullableElement(element, "sourceConcept", pointer + "/sourceConcept", ctx);
        var filing = NullableElement(element, "filing", pointer + "/filing", ctx);
        var convention = Required(element, "convention", pointer, ctx);
        var comparative = NullableElement(element, "comparativeOf", pointer + "/comparativeOf", ctx);
        var derivation = NullableElement(element, "derivation", pointer + "/derivation", ctx);

        if (metric is not null && !Metrics.Contains(metric)) ctx.Add(pointer + "/metric", Unsupported);
        if (metric == "DilutionObservation") ctx.Add(pointer + "/metric", Unsupported);
        if (unit is not null) Unit(unit.Value, pointer + "/unit", ctx);
        if (period is not null) Period(period.Value, pointer + "/period", ctx);
        if (accounting is not null && accounting.Value.ValueKind != JsonValueKind.Object) ctx.Add(pointer + "/accounting", Malformed);
        if (shareBasis is not null) RequireObject(shareBasis.Value, pointer + "/shareBasis", ctx);
        if (sourceConcept is not null) RequireObject(sourceConcept.Value, pointer + "/sourceConcept", ctx);
        if (filing is not null) Ref(filing.Value, pointer + "/filing", ctx);
        if (convention is not null) Ref(convention.Value, pointer + "/convention", ctx);
        if (comparative is not null) Ref(comparative.Value, pointer + "/comparativeOf", ctx);
        if (derivation is not null) RequireObject(derivation.Value, pointer + "/derivation", ctx);

        if (applicability == "APPLICABLE" && amount is null) ctx.Add(pointer + "/amount", Malformed);
        if (applicability == "INAPPLICABLE" && amount is not null) ctx.Add(pointer + "/amount", Malformed);
        if (applicability == "INAPPLICABLE" && reason is null) ctx.Add(pointer + "/inapplicabilityReason", Malformed);
        if (applicability == "APPLICABLE" && reason is not null) ctx.Add(pointer + "/inapplicabilityReason", Malformed);
        if (factKind == "REPORTED")
        {
            if (sourceConcept is null) ctx.Add(pointer + "/sourceConcept", Malformed);
            if (filing is null) ctx.Add(pointer + "/filing", Malformed);
            if (derivation is not null) ctx.Add(pointer + "/derivation", Malformed);
        }
        if (factKind == "DERIVED")
        {
            if (sourceConcept is not null) ctx.Add(pointer + "/sourceConcept", Malformed);
            if (filing is not null) ctx.Add(pointer + "/filing", Malformed);
            if (derivation is null) ctx.Add(pointer + "/derivation", Malformed);
            if (applicability == "INAPPLICABLE") ctx.Add(pointer + "/applicability", Malformed);
        }
        if (metric == "FreeCashFlow" && factKind != "DERIVED") ctx.Add(pointer + "/factKind", Unsupported);
        if (metric == "DividendPerShare" && factKind != "REPORTED") ctx.Add(pointer + "/factKind", Unsupported);

        if (metric is null || factKind is null || applicability is null || unit is null || period is null ||
            accounting is null || definitionId is null || convention is null) return null;
        return new FundamentalFactValue(metric, factKind, applicability, reason, amount, unit.Value, period.Value,
            accounting.Value, shareBasis, definitionId, sourceConcept, filing, convention.Value, comparative, derivation);
    }

    private static FundamentalConventionValue? Convention(JsonElement element, string pointer, Context ctx)
    {
        if (!Closed(element, pointer, ConventionFields, ctx)) return null;
        var conventionId = Code(element, "conventionId", pointer + "/conventionId", ctx);
        var version = Code(element, "version", pointer + "/version", ctx);
        var kind = Token(element, "kind", pointer + "/kind", ["MAPPING", "ENTITY_SECURITY", "FORMULA_REFERENCE"], ctx);
        var support = Array(element, "support", pointer + "/support", 1, 16, ctx);
        var mapping = NullableElement(element, "mapping", pointer + "/mapping", ctx);
        var entitySecurity = NullableElement(element, "entitySecurity", pointer + "/entitySecurity", ctx);
        var formula = NullableElement(element, "formula", pointer + "/formula", ctx);
        if (support is not null) foreach (var artifact in support) Artifact(artifact, "", pointer + "/support", ctx);
        var branches = new[] { mapping is not null, entitySecurity is not null, formula is not null }.Count(x => x);
        if (branches != 1) ctx.Add(pointer, Malformed);
        if (kind is not null)
        {
            var expected = kind == "MAPPING" ? mapping : kind == "ENTITY_SECURITY" ? entitySecurity : formula;
            if (branches == 1 && expected is null) ctx.Add(pointer + "/" + kind, Malformed);
        }
        if (conventionId is null || version is null || kind is null || support is null) return null;
        return new FundamentalConventionValue(conventionId, version, kind, support, mapping, entitySecurity, formula);
    }

    private static void Ref(JsonElement element, string pointer, Context ctx)
    {
        if (!Closed(element, pointer, RefFields, ctx)) return;
        Uuid(element, "evidenceId", pointer + "/evidenceId", ctx);
        var revision = Int64(element, "revisionNumber", pointer + "/revisionNumber", ctx);
        if (revision is not null && revision < 1) ctx.Add(pointer + "/revisionNumber", Malformed);
    }

    private static void Artifact(JsonElement element, string name, string pointer, Context ctx)
    {
        if (name.Length > 0)
        {
            var value = Required(element, name, pointer, ctx);
            if (value is null) return;
            element = value.Value;
            pointer += "/" + name;
        }
        if (!Closed(element, pointer, ArtifactFields, ctx)) return;
        Uuid(element, "artifactId", pointer + "/artifactId", ctx);
        Hash(element, "sha256", pointer + "/sha256", ctx);
        var length = Int64(element, "byteLength", pointer + "/byteLength", ctx);
        if (length is not null && length < 0) ctx.Add(pointer + "/byteLength", Malformed);
        Text(element, "locator", pointer + "/locator", ctx);
    }

    private static EntityKey? EntityKeyValue(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        return EntityKeyShape(value.Value, pointer + "/" + name, ctx);
    }

    private static EntityKey? NullableEntityKey(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null || value.Value.ValueKind == JsonValueKind.Null) return null;
        return EntityKeyShape(value.Value, pointer, ctx);
    }

    private static EntityKey? EntityKeyShape(JsonElement element, string pointer, Context ctx)
    {
        if (!Closed(element, pointer, EntityKeyFields, ctx)) return null;
        var ns = Code(element, "namespace", pointer + "/namespace", ctx);
        var code = Code(element, "code", pointer + "/code", ctx);
        return ns is null || code is null ? null : new EntityKey(ns, code);
    }

    private static void Format(JsonElement element, string pointer, Context ctx)
    {
        if (!Closed(element, pointer, ["kind", "schemaId", "schemaVersion", "taxonomyFamily", "taxonomyVersion"], ctx)) return;
        var kind = Token(element, "kind", pointer + "/kind", ["XBRL", "XLSX", "OTHER"], ctx);
        Code(element, "schemaId", pointer + "/schemaId", ctx);
        Code(element, "schemaVersion", pointer + "/schemaVersion", ctx);
        var family = NullableCode(element, "taxonomyFamily", pointer + "/taxonomyFamily", ctx);
        var version = NullableCode(element, "taxonomyVersion", pointer + "/taxonomyVersion", ctx);
        if ((family is null) != (version is null)) ctx.Add(pointer, Malformed);
        if (kind == "XBRL" && family is null) ctx.Add(pointer + "/taxonomyFamily", Malformed);
    }

    private static void Unit(JsonElement element, string pointer, Context ctx)
    {
        if (!Closed(element, pointer, ["kind", "currencyCode"], ctx)) return;
        var kind = Token(element, "kind", pointer + "/kind", ["MONETARY", "SHARES", "PER_SHARE"], ctx);
        var currency = NullableCode(element, "currencyCode", pointer + "/currencyCode", ctx);
        if (currency is not null && !Regex.IsMatch(currency, @"\A[A-Z]{3}\z")) ctx.Add(pointer + "/currencyCode", Malformed);
        if ((kind is "MONETARY" or "PER_SHARE") && currency is null) ctx.Add(pointer + "/currencyCode", Malformed);
        if (kind == "SHARES" && currency is not null) ctx.Add(pointer + "/currencyCode", Malformed);
    }

    private static void Period(JsonElement element, string pointer, Context ctx)
    {
        if (!Closed(element, pointer,
            ["periodType", "startDate", "endDate", "fiscalYear", "fiscalStart", "fiscalEnd", "fiscalQuarter", "periodKind", "cumulative"], ctx)) return;
        var periodType = Token(element, "periodType", pointer + "/periodType", ["INSTANT", "DURATION"], ctx);
        var start = NullableDate(element, "startDate", pointer + "/startDate", ctx);
        var end = Date(element, "endDate", pointer + "/endDate", ctx);
        var kind = Token(element, "periodKind", pointer + "/periodKind", ["INSTANT", "QUARTER", "YTD", "ANNUAL", "TTM", "OTHER"], ctx);
        var cumulative = Bool(element, "cumulative", pointer + "/cumulative", ctx);
        if (periodType == "INSTANT" && start is not null) ctx.Add(pointer + "/startDate", Malformed);
        if (periodType == "DURATION" && start is null) ctx.Add(pointer + "/startDate", Malformed);
        if (periodType == "INSTANT" && kind is not null && kind != "INSTANT") ctx.Add(pointer + "/periodKind", Malformed);
        if (periodType == "DURATION" && kind == "INSTANT") ctx.Add(pointer + "/periodKind", Malformed);
        if (start is not null && end is not null && start > end) ctx.Add(pointer + "/startDate", Malformed);
        if (kind == "TTM") ctx.Add(pointer + "/periodKind", Unsupported);
        if (cumulative is true && kind is "INSTANT" or "QUARTER" or "TTM") ctx.Add(pointer + "/cumulative", Malformed);
        if (cumulative is false && kind == "YTD") ctx.Add(pointer + "/cumulative", Malformed);
        if (cumulative is false && kind == "ANNUAL") ctx.Add(pointer + "/cumulative", Malformed);
    }

    private static string? Code(JsonElement element, string name, string pointer, Context ctx, int max = 200)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        if (!TryCode(value.Value, max, out var text)) { ctx.Add(pointer, Malformed); return null; }
        return text;
    }

    private static string? RevisionSeries(JsonElement element, string pointer, Context ctx)
    {
        var value = Required(element, "revisionSeriesId", pointer, ctx);
        if (value is null) return null;
        if (value.Value.ValueKind != JsonValueKind.String) { ctx.Add(pointer, Malformed); return null; }
        var candidate = value.Value.GetString()!;
        if (candidate.Length is 0 or > 400 || candidate.Trim().Length != candidate.Length)
        { ctx.Add(pointer, Malformed); return null; }
        foreach (var character in candidate)
            if (char.IsControl(character) && character != '\u001F') { ctx.Add(pointer, Malformed); return null; }
        return candidate;
    }

    private static string? NullableCode(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null || value.Value.ValueKind == JsonValueKind.Null) return null;
        if (!TryCode(value.Value, 200, out var text)) { ctx.Add(pointer, Malformed); return null; }
        return text;
    }

    private static string? Text(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        if (!TryCode(value.Value, 2000, out var text)) { ctx.Add(pointer, Malformed); return null; }
        return text;
    }

    private static string? NullableText(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null || value.Value.ValueKind == JsonValueKind.Null) return null;
        if (!TryCode(value.Value, 2000, out var text)) { ctx.Add(pointer, Malformed); return null; }
        return text;
    }

    private static bool TryCode(JsonElement value, int max, out string text)
    {
        text = string.Empty;
        if (value.ValueKind != JsonValueKind.String) return false;
        var candidate = value.GetString()!;
        if (candidate.Length is 0 || candidate.Length > max) return false;
        if (candidate.Trim().Length != candidate.Length) return false;
        foreach (var character in candidate)
            if (char.IsControl(character) || character == '\u001F') return false;
        text = candidate;
        return true;
    }

    private static Guid? Uuid(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        var text = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (text is null || !string.Equals(text, text.ToLowerInvariant(), StringComparison.Ordinal) || !Guid.TryParseExact(text, "D", out var parsed) || parsed == Guid.Empty)
        { ctx.Add(pointer, Malformed); return null; }
        return parsed;
    }

    private static string? Hash(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        var text = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (text is null || !HashPattern.IsMatch(text)) { ctx.Add(pointer, Malformed); return null; }
        return text;
    }

    private static int? Int32(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        if (value.Value.ValueKind != JsonValueKind.Number || !value.Value.TryGetInt32(out var parsed) ||
            !int.TryParse(value.Value.GetRawText(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var _))
        { ctx.Add(pointer, Malformed); return null; }
        return parsed;
    }

    private static long? Int64(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        if (value.Value.ValueKind != JsonValueKind.Number || !value.Value.TryGetInt64(out var parsed))
        { ctx.Add(pointer, Malformed); return null; }
        return parsed;
    }

    private static long? NullableInt64(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null || value.Value.ValueKind == JsonValueKind.Null) return null;
        if (value.Value.ValueKind != JsonValueKind.Number || !value.Value.TryGetInt64(out var parsed))
        { ctx.Add(pointer, Malformed); return null; }
        return parsed;
    }

    private static bool? Bool(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        return value.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => Report<bool?>(ctx, pointer),
        };
    }

    private static T? Report<T>(Context ctx, string pointer)
    {
        ctx.Add(pointer, Malformed);
        return default;
    }

    private static string? Token(JsonElement element, string name, string pointer, string[] allowed, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        var text = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (text is null || !allowed.Contains(text, StringComparer.Ordinal)) { ctx.Add(pointer, Malformed); return null; }
        return text;
    }

    private static string[]? Tokens(JsonElement element, string name, string pointer, Context ctx)
    {
        var items = Array(element, name, pointer, 1, 16, ctx);
        if (items is null) return null;
        var tokens = new List<string>();
        foreach (var item in items)
        {
            var text = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (text is null) { ctx.Add(pointer, Malformed); return null; }
            tokens.Add(text);
        }
        return [.. tokens];
    }

    private static List<JsonElement>? Array(JsonElement element, string name, string pointer, int min, int max, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        if (value.Value.ValueKind != JsonValueKind.Array) { ctx.Add(pointer, Malformed); return null; }
        var items = value.Value.EnumerateArray().ToList();
        if (items.Count < min || items.Count > max) { ctx.Add(pointer, items.Count > max ? BoundExceeded : Malformed); return null; }
        return items;
    }

    private static DateTimeOffset? Instant(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        if (!TryInstant(value.Value, out var parsed)) { ctx.Add(pointer, Malformed); return null; }
        return parsed;
    }

    private static DateTimeOffset? NullableInstant(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null || value.Value.ValueKind == JsonValueKind.Null) return null;
        if (!TryInstant(value.Value, out var parsed)) { ctx.Add(pointer, Malformed); return null; }
        return parsed;
    }

    private static bool TryInstant(JsonElement value, out DateTimeOffset parsed)
    {
        parsed = default;
        if (value.ValueKind != JsonValueKind.String) return false;
        var text = value.GetString();
        if (text is null || !InstantPattern.IsMatch(text)) return false;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed);
    }

    private static DateOnly? Date(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null) return null;
        if (!TryDate(value.Value, out var parsed)) { ctx.Add(pointer, Malformed); return null; }
        return parsed;
    }

    private static DateOnly? NullableDate(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null || value.Value.ValueKind == JsonValueKind.Null) return null;
        if (!TryDate(value.Value, out var parsed)) { ctx.Add(pointer, Malformed); return null; }
        return parsed;
    }

    private static bool TryDate(JsonElement value, out DateOnly parsed)
    {
        parsed = default;
        if (value.ValueKind != JsonValueKind.String) return false;
        var text = value.GetString();
        return text is not null && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
    }

    private static EvidenceDecimal? NullableDecimal(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null || value.Value.ValueKind == JsonValueKind.Null) return null;
        if (value.Value.ValueKind != JsonValueKind.String) { ctx.Add(pointer, Malformed); return null; }
        try { return new EvidenceDecimal(value.Value.GetString()!); }
        catch (EvidenceBindingException) { ctx.Add(pointer, Numeric); return null; }
    }

    private static JsonElement? Required(JsonElement element, string name, string pointer, Context ctx)
    {
        if (!element.TryGetProperty(name, out var value)) { ctx.Add(pointer, Malformed); return null; }
        return value;
    }

    private static JsonElement? Element(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null || value.Value.ValueKind != JsonValueKind.Object) { if (value is not null) ctx.Add(pointer, Malformed); return null; }
        return value;
    }

    private static JsonElement? NullableElement(JsonElement element, string name, string pointer, Context ctx)
    {
        var value = Required(element, name, pointer, ctx);
        if (value is null || value.Value.ValueKind == JsonValueKind.Null) return null;
        return value;
    }

    private static void RequireObject(JsonElement element, string pointer, Context ctx)
    {
        if (element.ValueKind != JsonValueKind.Object) ctx.Add(pointer, Malformed);
    }

    private static bool Closed(JsonElement element, string pointer, string[] allowed, Context ctx)
    {
        if (element.ValueKind != JsonValueKind.Object) { ctx.Add(pointer, Malformed); return false; }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ok = true;
        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                ctx.Add(pointer + "/" + property.Name, Malformed);
                ok = false;
            }
        }
        return ok;
    }

    private static FundamentalDecodeResult Failure(string diagnostic) => new(null, [diagnostic]);

    private static FundamentalDecodeResult Failure(List<string> diagnostics) =>
        new(null, diagnostics.Count == 0 ? [Malformed] : diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    private sealed class Context(List<string> diagnostics)
    {
        public List<string> Diagnostics { get; } = diagnostics;
        public void Add(string pointer, string diagnostic) => Diagnostics.Add(diagnostic + (pointer.Length > 0 ? "@" + pointer : string.Empty));
    }
}
