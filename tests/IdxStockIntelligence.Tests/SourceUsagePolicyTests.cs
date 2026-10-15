using System.Text.Json;
using System.Text.Json.Serialization;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

// Authored permission/market-shape fixtures only; no source admission or real provider grant.
public sealed class SourceUsagePolicyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) } };
    internal static readonly DateTimeOffset Now = new(2026, 10, 15, 2, 0, 0, TimeSpan.Zero);
    internal static SourceUsageConfiguration Fixture() => new(1, DeploymentProfile.Personal,
        Enum.GetValues<SourceFeature>(),
        [new("synthetic-provider", "synthetic-route-window-v1", true,
            Enum.GetValues<SourceCapability>().Select(c => new SourceCapabilityDeclaration(c, "synthetic-semantics-" + c)).ToArray(),
            new("synthetic-provider", "synthetic-route-window-v1", "synthetic-permission-v1", "synthetic-owned-terms", Now.AddDays(-2), Now.AddDays(-1), Now.AddDays(1),
                Enum.GetValues<SourceOperation>().Select(o => new SourcePermissionRule(o, SourcePermissionState.ExplicitPermission)).ToArray()), [])],
        Enum.GetValues<SourceCapability>().Select(c => new SourceSelection(c, "synthetic-provider", "synthetic-route-window-v1")).ToArray());
    internal static SourceSelection[] Inputs(SourceUsageConfiguration config, SourceFeature feature = SourceFeature.PriceInspection)
        => config.Selections.Where(s => SourceUsagePolicy.RequiredCapabilities(feature).Contains(s.Capability)).ToArray();
    internal static SourceUsageConfiguration Permission(SourceUsageConfiguration config, SourceOperation operation, SourcePermissionState permission)
    {
        var source = config.Sources[0]; var usage = source.Usage!;
        return config with { Sources = [source with { Usage = usage with
            { Permissions = usage.Permissions.Select(p => p.Operation == operation ? p with { Permission = permission } : p).ToArray() } }] };
    }
    private static SourceUsageDecision Evaluate(SourceUsageConfiguration config, AnalysisMode mode = AnalysisMode.Research,
        SourceFeature feature = SourceFeature.PriceInspection, bool admitted = false, SourceOperation[]? extra = null)
        => SourceUsagePolicy.Evaluate(config, feature, mode, Inputs(config, feature), Now, admitted, extra);

    [Fact]
    public void DefaultConfigurationHasNoEnabledSourceOrFeature()
    {
        var disabled = SourceUsagePolicy.Disabled();
        Assert.True(SourceUsagePolicy.Valid(disabled)); Assert.Empty(disabled.Sources); Assert.Empty(disabled.Selections);
        Assert.Equal("FEATURE_DISABLED", Evaluate(disabled).Reason);
    }

    [Theory]
    [InlineData(SourcePermissionState.Unknown, "PERMISSION_UNKNOWN")]
    [InlineData(SourcePermissionState.ExplicitProhibition, "PERMISSION_PROHIBITED")]
    [InlineData(SourcePermissionState.ConditionalPermission, "PERMISSION_UNKNOWN")]
    public void ResearchCannotBypassMissingDeniedOrConditionalRights(SourcePermissionState permission, string reason)
    {
        var config = Permission(Fixture(), SourceOperation.InternalAnalysis, permission);
        foreach (var mode in Enum.GetValues<AnalysisMode>())
        {
            var decision = Evaluate(config, mode, admitted: true);
            Assert.False(decision.Permitted); Assert.Equal(reason, decision.Reason); Assert.Empty(decision.Provenance);
        }
    }

    [Fact]
    public void ProhibitedAutomationCannotBeEnabledByResearch()
    {
        var config = Permission(Fixture(), SourceOperation.AutomatedAcquisition, SourcePermissionState.ExplicitProhibition);
        Assert.True(Evaluate(config).Permitted); // A lawful retained-only read needs no new download grant.
        Assert.Equal("PERMISSION_PROHIBITED", Evaluate(config, extra: [SourceOperation.AutomatedAcquisition]).Reason);
        Assert.Equal("PERMISSION_PROHIBITED", Evaluate(config, extra: [SourceOperation.HistoricalRetrieval]).Reason);
        Assert.Equal("PERMISSION_PROHIBITED", Evaluate(config, extra: [SourceOperation.RecurringRetrieval]).Reason);
    }

    [Fact]
    public void PersonalGrantsDoNotAuthorizeCommercialOrSaasConsumption()
    {
        var config = Permission(Fixture(), SourceOperation.CommercialUse, SourcePermissionState.ExplicitProhibition);
        Assert.True(Evaluate(config).Permitted);
        Assert.Equal("PERMISSION_PROHIBITED", Evaluate(config with { Profile = DeploymentProfile.Saas }).Reason);
        var commercial = Permission(Fixture(), SourceOperation.SaasConsumption, SourcePermissionState.Unknown) with { Profile = DeploymentProfile.Saas };
        Assert.Equal("PERMISSION_UNKNOWN", Evaluate(commercial).Reason);
        Assert.Equal("UNSUPPORTED_PROFILE", Evaluate(Fixture() with { Profile = DeploymentProfile.Saas }).Reason);
    }

    [Fact]
    public void DisabledSourceOrFeatureFailsWithoutFallback()
    {
        var config = Fixture();
        Assert.Equal("SOURCE_DISABLED", Evaluate(config with { Sources = [config.Sources[0] with { Enabled = false }] }).Reason);
        Assert.Equal("FEATURE_DISABLED", Evaluate(config with { EnabledFeatures = [] }).Reason);
    }

    [Fact]
    public void SupportedCapabilitiesAndRequiredInputsAreBothNecessary()
    {
        var config = Fixture();
        Assert.Equal("CAPABILITY_MISSING", SourceUsagePolicy.Evaluate(config, SourceFeature.PriceInspection,
            AnalysisMode.Research, [], Now).Reason);
        Assert.Equal("CAPABILITY_UNSUPPORTED", Evaluate(config with { Sources = [config.Sources[0] with { Capabilities = [] }] }).Reason);
    }

    [Fact]
    public void SourceSwitchAndScopeChangeRequireExplicitBinding()
    {
        var config = Fixture(); var inputs = Inputs(config);
        Assert.Equal("SOURCE_SELECTION_MISMATCH", SourceUsagePolicy.Evaluate(config, SourceFeature.PriceInspection,
            AnalysisMode.Research, [inputs[0] with { SourceId = "another-provider" }, inputs[1]], Now).Reason);
        Assert.Equal("SOURCE_SELECTION_MISMATCH", SourceUsagePolicy.Evaluate(config, SourceFeature.PriceInspection,
            AnalysisMode.Research, [inputs[0] with { ScopeId = "another-window" }, inputs[1]], Now).Reason);
    }

    [Fact]
    public void PermissionNeverCreatesVerifiedAdmissionOrResearchPromotion()
    {
        var config = Fixture(); Assert.True(Evaluate(config).Permitted);
        Assert.Equal("EVIDENCE_NOT_ADMITTED", Evaluate(config, AnalysisMode.Verified).Reason);
        Assert.Equal("EVIDENCE_NOT_ADMITTED", Evaluate(config, AnalysisMode.Verified, SourceFeature.VerifiedScreening).Reason);
        Assert.Equal("MODE_UNSUPPORTED", Evaluate(config, AnalysisMode.Research, SourceFeature.VerifiedScreening, true).Reason);
        Assert.True(Evaluate(config, AnalysisMode.Verified, SourceFeature.VerifiedScreening, true).Permitted);
        Assert.False(ScreenerSourceAdmission.Admit(EvidenceClaim.TradingStatus, SourceAuthorityTier.T3ProviderObservation).Admitted);
    }

    [Fact]
    public void ExportAndDerivedProcessingHaveSeparatePermissions()
    {
        var config = Permission(Fixture(), SourceOperation.Export, SourcePermissionState.ExplicitProhibition);
        Assert.True(Evaluate(config).Permitted);
        Assert.Equal("EXPORT_NOT_PERMITTED", Evaluate(config, feature: SourceFeature.PriceExport).Reason);
        config = Permission(Fixture(), SourceOperation.ProcessDerived, SourcePermissionState.Unknown);
        Assert.True(Evaluate(config).Permitted);
        Assert.Equal("PERMISSION_UNKNOWN", Evaluate(config, feature: SourceFeature.IndicativeTechnicals).Reason);
    }

    [Fact]
    public void EveryDerivedInputRetainsSourceScopeAndDecisionProvenance()
    {
        var config = Fixture(); var second = config.Sources[0] with { SourceId = "synthetic-identity",
            Usage = config.Sources[0].Usage! with { SourceId = "synthetic-identity", DecisionId = "synthetic-permission-identity" } };
        config = config with { Sources = [config.Sources[0], second], Selections = config.Selections.Select(s =>
            s.Capability == SourceCapability.InstrumentIdentity ? s with { SourceId = second.SourceId } : s).ToArray() };
        var decision = Evaluate(config, feature: SourceFeature.IndicativeTechnicals);
        Assert.True(decision.Permitted); Assert.Equal(2, decision.Provenance.Count);
        Assert.Equal(2, decision.Provenance.Select(p => p.Input.SourceId).Distinct().Count());
        Assert.All(decision.Provenance, p =>
        {
            Assert.Equal("synthetic-route-window-v1", p.Input.ScopeId);
            Assert.StartsWith("synthetic-semantics-", p.CapabilityEvidenceReference);
            Assert.Equal(p.Input.SourceId == "synthetic-provider" ? "synthetic-permission-v1" : "synthetic-permission-identity", p.PermissionDecisionId);
            Assert.Equal(Now.AddDays(-2), p.ReviewedAt);
        });
        Assert.Equal("PERMISSION_PROHIBITED", Evaluate(config with { Sources = [config.Sources[0], second with
            { Usage = second.Usage! with { Permissions = [new(SourceOperation.ProcessDerived, SourcePermissionState.ExplicitProhibition)] } }] },
            feature: SourceFeature.IndicativeTechnicals).Reason);
    }

    [Fact]
    public void PresentRightsAreCheckedAtExecutionTimeWithoutHistoricalCutoffBackdating()
    {
        var config = Fixture(); var source = config.Sources[0];
        Assert.Equal("PERMISSION_EXPIRED", Evaluate(config with { Sources = [source with { Usage = source.Usage! with { ValidUntil = Now } }] }).Reason);
        Assert.True(SourceUsagePolicy.Evaluate(config, SourceFeature.PriceInspection, AnalysisMode.Research,
            Inputs(config), source.Usage!.ValidFrom!.Value).Permitted);
        Assert.False(SourceUsagePolicy.Evaluate(config, SourceFeature.PriceInspection, AnalysisMode.Research,
            Inputs(config), source.Usage.ValidFrom.Value.AddTicks(-1)).Permitted);
        var revoked = Permission(config, SourceOperation.InternalAnalysis, SourcePermissionState.ExplicitProhibition);
        Assert.Equal("PERMISSION_PROHIBITED", Evaluate(revoked).Reason);
        Assert.True(Evaluate(config).Permitted); // Historical input/decision was not mutated by the new policy snapshot.
    }

    [Theory]
    [InlineData("missing-evidence")] [InlineData("future-review")] [InlineData("unknown-start")]
    [InlineData("unknown-end")] [InlineData("restriction")] [InlineData("missing-operation")]
    public void IncompleteAuthorizationFailsClosed(string defect)
    {
        var config = Fixture(); var source = config.Sources[0]; var usage = source.Usage!;
        source = defect switch
        {
            "missing-evidence" => source with { Usage = null },
            "future-review" => source with { Usage = usage with { ReviewedAt = Now.AddTicks(1) } },
            "unknown-start" => source with { Usage = usage with { ValidFrom = null } },
            "unknown-end" => source with { Usage = usage with { ValidUntil = null } },
            "restriction" => source with { UnresolvedRestrictions = ["synthetic-unimplemented-quota-condition"] },
            _ => source with { Usage = usage with { Permissions = [] } }
        };
        Assert.Equal("PERMISSION_UNKNOWN", Evaluate(config with { Sources = [source] }).Reason);
    }

    [Theory]
    [InlineData("duplicate-source")] [InlineData("duplicate-family")] [InlineData("duplicate-permission")]
    [InlineData("invalid-enum")] [InlineData("invalid-scope")] [InlineData("invalid-interval")]
    [InlineData("grant-source")] [InlineData("grant-scope")]
    public void InvalidTrustedConfigurationCannotProduceAPermit(string defect)
    {
        var config = Fixture(); var source = config.Sources[0];
        config = defect switch
        {
            "duplicate-source" => config with { Sources = [source, source] },
            "duplicate-family" => config with { Selections = [config.Selections[0], config.Selections[0]] },
            "duplicate-permission" => config with { Sources = [source with { Usage = source.Usage! with
                { Permissions = [source.Usage.Permissions[0], source.Usage.Permissions[0]] } }] },
            "invalid-enum" => config with { Profile = (DeploymentProfile)99 },
            "invalid-scope" => config with { Selections = [config.Selections[0] with { ScopeId = "unregistered" }] },
            "grant-source" => config with { Sources = [source with { Usage = source.Usage! with { SourceId = "another-provider" } }] },
            "grant-scope" => config with { Sources = [source with { Usage = source.Usage! with { ScopeId = "another-window" } }] },
            _ => config with { Sources = [source with { Usage = source.Usage! with { ValidUntil = source.Usage.ValidFrom } }] }
        };
        Assert.False(SourceUsagePolicy.Valid(config)); Assert.Equal("SOURCE_USAGE_CONFIG_INVALID", Evaluate(config).Reason);
    }

    [Theory]
    [InlineData("{}")] [InlineData("null")] [InlineData("{\"schemaVersion\":1,\"schemaVersion\":2}")]
    [InlineData("{\"secret-unknown-field\":\"do-not-leak\"}")]
    public void MalformedJsonHasOnlySafeDiagnostics(string json)
    {
        var error = Assert.Throws<ArgumentException>(() => SourceUsagePolicy.Parse(json));
        Assert.Equal("SOURCE_USAGE_CONFIG_INVALID", error.Message);
    }

    [Fact]
    public void StrictConfigurationRoundTripsAndRejectsNestedDuplicatesNullsAndOversize()
    {
        var json = JsonSerializer.Serialize(Fixture(), JsonOptions);
        Assert.True(Evaluate(SourceUsagePolicy.Parse(json)).Permitted);
        foreach (var changed in new[] { json.Replace("\"enabled\":true", "\"enabled\":true,\"enabled\":false", StringComparison.Ordinal),
            json.Replace("\"PERSONAL\"", "7", StringComparison.Ordinal), json.Replace("\"sources\":[", "\"sources\":null,\"unused\":[", StringComparison.Ordinal),
            new string(' ', SourceUsagePolicy.MaximumBytes + 1) })
            Assert.Throws<ArgumentException>(() => SourceUsagePolicy.Parse(changed));
    }
}
