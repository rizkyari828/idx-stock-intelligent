using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

// Onboarding acceptance: a brand-new provider is enabled by configuration and selection
// only — no production code change — while an undeclared or disabled source stays denied.
// Scenario mapping: capability-policy onboarding; no acquisition and no provider call.
public sealed class SourceOnboardingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 4, 0, 0, TimeSpan.Zero);
    private static string Configuration(string sourceId = "future-provider", bool enabled = true,
        string features = "\"PRICE_INSPECTION\"") => $$"""
    {
      "schemaVersion": 1,
      "profile": "PERSONAL",
      "enabledFeatures": [{{features}}],
      "sources": [
        {
          "sourceId": "{{sourceId}}",
          "scopeId": "future-route-v1",
          "enabled": {{(enabled ? "true" : "false")}},
          "capabilities": [
            {"capability": "OHLC", "evidenceReference": "future-ohlc-semantics-v1"},
            {"capability": "INSTRUMENT_IDENTITY", "evidenceReference": "future-identity-v1"}
          ],
          "usage": {
            "sourceId": "{{sourceId}}",
            "scopeId": "future-route-v1",
            "decisionId": "review-2026-10-26",
            "termsReference": "future-provider-terms",
            "reviewedAt": "2026-10-01T00:00:00Z",
            "validFrom": "2026-09-01T00:00:00Z",
            "validUntil": "2027-09-01T00:00:00Z",
            "permissions": [
              {"operation": "PERSONAL_USE", "permission": "EXPLICIT_PERMISSION"},
              {"operation": "AUTOMATED_ACQUISITION", "permission": "EXPLICIT_PERMISSION"},
              {"operation": "RETAIN_ORIGINAL", "permission": "EXPLICIT_PERMISSION"},
              {"operation": "INTERNAL_ANALYSIS", "permission": "EXPLICIT_PERMISSION"},
              {"operation": "DISPLAY", "permission": "EXPLICIT_PERMISSION"}
            ]
          },
          "unresolvedRestrictions": []
        }
      ],
      "selections": [
        {"capability": "OHLC", "sourceId": "{{sourceId}}", "scopeId": "future-route-v1"},
        {"capability": "INSTRUMENT_IDENTITY", "sourceId": "{{sourceId}}", "scopeId": "future-route-v1"}
      ]
    }
    """;

    private static SourceSelection[] Inputs(string sourceId = "future-provider") =>
    [
        new(SourceCapability.Ohlc, sourceId, "future-route-v1"),
        new(SourceCapability.InstrumentIdentity, sourceId, "future-route-v1"),
    ];

    [Fact]
    public void NewProviderIsEnabledByConfigurationAlone()
    {
        var config = SourceUsagePolicy.Parse(Configuration());
        var decision = SourceUsagePolicy.Evaluate(config, SourceFeature.PriceInspection, AnalysisMode.Research, Inputs(), Now);
        Assert.True(decision.Permitted, decision.Reason);
        Assert.Equal(2, decision.Provenance.Count);
        Assert.All(decision.Provenance, entry => Assert.Equal("future-provider", entry.Input.SourceId));
    }

    [Fact]
    public void DisabledProviderIsDeniedEvenWithFullPermissions()
    {
        var config = SourceUsagePolicy.Parse(Configuration(enabled: false));
        var decision = SourceUsagePolicy.Evaluate(config, SourceFeature.PriceInspection, AnalysisMode.Research, Inputs(), Now);
        Assert.False(decision.Permitted);
        Assert.Equal("SOURCE_DISABLED", decision.Reason);
    }

    [Fact]
    public void UndeclaredSelectionIsRejectedRatherThanSilentlySubstituted()
    {
        var config = SourceUsagePolicy.Parse(Configuration());
        var foreign = new[]
        {
            new SourceSelection(SourceCapability.Ohlc, "other-provider", "future-route-v1"),
            new SourceSelection(SourceCapability.InstrumentIdentity, "other-provider", "future-route-v1"),
        };
        var decision = SourceUsagePolicy.Evaluate(config, SourceFeature.PriceInspection, AnalysisMode.Research, foreign, Now);
        Assert.False(decision.Permitted);
        Assert.True(decision.Reason is "SOURCE_SELECTION_MISMATCH" or "SOURCE_USAGE_REQUEST_INVALID", decision.Reason);
    }

    [Fact]
    public void VerifiedScreeningNeedsTheExtraCapabilitiesNoSingleProviderCanSkip()
    {
        var config = SourceUsagePolicy.Parse(Configuration(features: "\"PRICE_INSPECTION\", \"VERIFIED_SCREENING\""));
        var decision = SourceUsagePolicy.Evaluate(config, SourceFeature.VerifiedScreening, AnalysisMode.Verified, Inputs(), Now);
        Assert.False(decision.Permitted);
        Assert.Equal("CAPABILITY_MISSING", decision.Reason);
    }

    [Fact]
    public void SaasProfileRemainsUnsupportedOnAPersonalGrant()
    {
        var config = SourceUsagePolicy.Parse(Configuration()) with { Profile = DeploymentProfile.Saas };
        var decision = SourceUsagePolicy.Evaluate(config, SourceFeature.PriceInspection, AnalysisMode.Research, Inputs(), Now);
        Assert.False(decision.Permitted);
    }
}
