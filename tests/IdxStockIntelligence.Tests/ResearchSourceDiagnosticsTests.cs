using System.Text.Json;
using System.Text.Json.Serialization;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ResearchSourceDiagnosticsTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) } };

    [Fact]
    public void DefaultReportHasNoMarketFactsAdmissionOrPromotion()
    {
        var report = ResearchSourceDiagnostics.Read(null, AnalysisMode.Research, SourceUsagePolicyTests.Now);
        Assert.Equal(4, report.Features.Count); Assert.All(report.Features, f => Assert.Equal("FEATURE_DISABLED", f.Reason));
        Assert.False(report.MarketDataRead); Assert.False(report.EvidenceGateSatisfied); Assert.False(report.CandidatePromotionPerformed);
        Assert.Equal("INDICATIVE_DIAGNOSTICS_ONLY", report.Classification);
        Assert.Equal("TRUSTED_LOCAL_CONFIGURATION_ONLY", report.Provenance);
        Assert.Equal(SourceUsagePolicyTests.Now, report.ExecutionAt);
    }

    [Fact]
    public void GrantedConfigurationStillCannotManufactureVerifiedReadinessOrLeakReviewReferences()
    {
        var config = SourceUsagePolicyTests.Fixture(); var source = config.Sources[0];
        config = config with { Sources = [source with { Usage = source.Usage! with { TermsReference = "PRIVATE-REVIEW-SENTINEL" } }] };
        var json = JsonSerializer.Serialize(config, Options);
        var research = ResearchSourceDiagnostics.Read(json, AnalysisMode.Research, SourceUsagePolicyTests.Now);
        Assert.True(research.Features.Single(f => f.Feature == SourceFeature.IndicativeTechnicals).UsagePermitted);
        Assert.Equal("MODE_UNSUPPORTED", research.Features.Single(f => f.Feature == SourceFeature.VerifiedScreening).Reason);
        var verified = ResearchSourceDiagnostics.Read(json, AnalysisMode.Verified, SourceUsagePolicyTests.Now);
        Assert.All(verified.Features, f => { Assert.False(f.UsagePermitted); Assert.Equal("EVIDENCE_NOT_ADMITTED", f.Reason); });
        Assert.DoesNotContain("PRIVATE-REVIEW-SENTINEL", JsonSerializer.Serialize(research, Options));
        Assert.DoesNotContain(source.ScopeId, JsonSerializer.Serialize(research, Options));
    }

    [Theory]
    [InlineData("permission", "EXPLICIT_PERMISSION")] [InlineData("evidenceGateSatisfied", "true")]
    [InlineData("sourceId", "other-source")] [InlineData("mode", "0")]
    [InlineData("mode", "")] [InlineData("mode", "RESEARCH,VERIFIED")]
    public void ClientCannotSetRightsSourcesOrAdmission(string key, string value)
        => Assert.Equal("RESEARCH_DIAGNOSTICS_QUERY_INVALID", Assert.Throws<ArgumentException>(() =>
            ResearchSourceDiagnostics.ParseMode(new Dictionary<string, string?> { [key] = value })).Message);

    [Fact]
    public void UnknownConfigurationFailsWithoutFallbackAndModeOnlySelectsDiagnostics()
    {
        Assert.Equal(AnalysisMode.Research, ResearchSourceDiagnostics.ParseMode(new Dictionary<string, string?>()));
        Assert.Equal(AnalysisMode.Verified, ResearchSourceDiagnostics.ParseMode(new Dictionary<string, string?> { ["mode"] = "VERIFIED" }));
        Assert.Equal("SOURCE_USAGE_CONFIG_INVALID", Assert.Throws<ArgumentException>(() =>
            ResearchSourceDiagnostics.Read("{}", AnalysisMode.Research, SourceUsagePolicyTests.Now)).Message);
    }
}
