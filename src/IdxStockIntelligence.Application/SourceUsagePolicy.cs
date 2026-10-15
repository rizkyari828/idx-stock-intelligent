using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdxStockIntelligence.Application;

public enum DeploymentProfile { Personal, Saas }
public enum AnalysisMode { Research, Verified }
public enum SourcePermissionState { Unknown, ExplicitPermission, ExplicitProhibition, ConditionalPermission }
public enum SourceOperation {
    PersonalUse, CommercialUse, AutomatedAcquisition, HistoricalRetrieval, RecurringRetrieval,
    RetainOriginal, InternalAnalysis, ProcessDerived, Display, Export, Redistribute, SaasConsumption
}
public enum SourceCapability {
    Ohlc, Volume, InstrumentIdentity, TradingStatus, ScheduledSession, CompletedSession,
    CorporateAction, FundamentalFilings, FinancialHistory, Benchmark, FlowOwnership
}
public enum SourceFeature { PriceInspection, IndicativeTechnicals, VerifiedScreening, PriceExport }
public sealed record SourcePermissionRule(SourceOperation Operation, SourcePermissionState Permission);
public sealed record SourceUsageEvidence(string SourceId, string ScopeId, string DecisionId, string TermsReference, DateTimeOffset ReviewedAt,
    DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil, SourcePermissionRule[] Permissions);
public sealed record SourceCapabilityDeclaration(SourceCapability Capability, string EvidenceReference);
public sealed record SourceCatalogEntry(string SourceId, string ScopeId, bool Enabled,
    SourceCapabilityDeclaration[] Capabilities, SourceUsageEvidence? Usage, string[] UnresolvedRestrictions);
public sealed record SourceSelection(SourceCapability Capability, string SourceId, string ScopeId);
public sealed record SourceUsageConfiguration(int SchemaVersion, DeploymentProfile Profile,
    SourceFeature[] EnabledFeatures, SourceCatalogEntry[] Sources, SourceSelection[] Selections);
public sealed record SourceUseProvenance(SourceSelection Input, string CapabilityEvidenceReference,
    string PermissionDecisionId, DateTimeOffset ReviewedAt);
public sealed record SourceUsageDecision(bool Permitted, string Reason, IReadOnlyList<SourceUseProvenance> Provenance);

// Usage eligibility only: never creates an admission, financial readiness state or provider activation.
public static class SourceUsagePolicy
{
    public const string PolicyId = "source-usage-v0.1.0";
    public const int MaximumBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false, RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false, MaxDepth = 12,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) }
    };
    public static SourceUsageConfiguration Disabled() => new(1, DeploymentProfile.Personal, [], [], []);

    public static SourceUsageConfiguration Parse(string json)
    {
        try
        {
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new ArgumentException("SOURCE_USAGE_CONFIG_INVALID");
            var config = JsonSerializer.Deserialize<SourceUsageConfiguration>(json, Options);
            if (config is null || !Valid(config)) throw new ArgumentException("SOURCE_USAGE_CONFIG_INVALID");
            return config;
        }
        catch (JsonException) { throw new ArgumentException("SOURCE_USAGE_CONFIG_INVALID"); }
    }

    private static bool Code(string? value) => value is { Length: > 0 and <= 128 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    private static bool Reference(string? value) => value is { Length: > 0 and <= 512 }
        && value.Trim() == value && !value.Any(char.IsControl);
    private static bool Utc(DateTimeOffset value) => value.Offset == TimeSpan.Zero && value != default;
    private static bool Unique<T>(T[]? values) where T : struct, Enum => values is not null
        && values.All(v => Enum.IsDefined(v)) && values.Distinct().Count() == values.Length;

    public static bool Valid(SourceUsageConfiguration config)
    {
        if (config.SchemaVersion != 1 || !Enum.IsDefined(config.Profile) || !Unique(config.EnabledFeatures)
            || config.EnabledFeatures.Length > 4 || config.Sources is null || config.Sources.Length > 32
            || config.Selections is null || config.Selections.Length > 11
            || config.Sources.Any(s => s is null) || config.Selections.Any(s => s is null)
            || config.Sources.Select(s => s.SourceId).Distinct(StringComparer.Ordinal).Count() != config.Sources.Length
            || config.Selections.Select(s => s.Capability).Distinct().Count() != config.Selections.Length) return false;
        foreach (var source in config.Sources)
        {
            if (!Code(source.SourceId) || !Code(source.ScopeId) || source.Capabilities is null || source.Capabilities.Length > 11
                || source.Capabilities.Any(c => c is null || !Enum.IsDefined(c.Capability) || !Reference(c.EvidenceReference))
                || source.Capabilities.Select(c => c.Capability).Distinct().Count() != source.Capabilities.Length
                || source.UnresolvedRestrictions is null || source.UnresolvedRestrictions.Length > 16
                || source.UnresolvedRestrictions.Any(r => !Reference(r))) return false;
            if (source.Usage is not { } usage) continue;
            if (usage.SourceId != source.SourceId || usage.ScopeId != source.ScopeId
                || !Code(usage.DecisionId) || !Reference(usage.TermsReference) || !Utc(usage.ReviewedAt)
                || (usage.ValidFrom is { } from && !Utc(from)) || (usage.ValidUntil is { } until && !Utc(until))
                || (usage.ValidFrom is { } start && usage.ValidUntil is { } end && end <= start)
                || usage.Permissions is null || usage.Permissions.Length > 12
                || usage.Permissions.Any(p => p is null || !Enum.IsDefined(p.Operation) || !Enum.IsDefined(p.Permission))
                || usage.Permissions.Select(p => p.Operation).Distinct().Count() != usage.Permissions.Length) return false;
        }
        return config.Selections.All(selection => Enum.IsDefined(selection.Capability)
            && Code(selection.SourceId) && Code(selection.ScopeId)
            && config.Sources.Any(s => s.SourceId == selection.SourceId && s.ScopeId == selection.ScopeId));
    }

    public static SourceCapability[] RequiredCapabilities(SourceFeature feature) => feature switch
    {
        SourceFeature.PriceInspection or SourceFeature.IndicativeTechnicals or SourceFeature.PriceExport
            => [SourceCapability.Ohlc, SourceCapability.InstrumentIdentity],
        SourceFeature.VerifiedScreening => [SourceCapability.Ohlc, SourceCapability.InstrumentIdentity,
            SourceCapability.TradingStatus, SourceCapability.ScheduledSession,
            SourceCapability.CompletedSession, SourceCapability.CorporateAction],
        _ => []
    };

    // Inputs must describe actual input provenance, supplied by the consuming service, never a grant from a browser.
    public static SourceUsageDecision Evaluate(SourceUsageConfiguration config, SourceFeature feature, AnalysisMode mode,
        IReadOnlyList<SourceSelection> inputs, DateTimeOffset executionAt, bool evidenceGateSatisfied = false,
        IReadOnlyList<SourceOperation>? additionalOperations = null)
    {
        static SourceUsageDecision Deny(string reason) => new(false, reason, []);
        if (!Valid(config)) return Deny("SOURCE_USAGE_CONFIG_INVALID");
        if (!Enum.IsDefined(feature) || !Enum.IsDefined(mode) || !Utc(executionAt) || inputs is null
            || inputs.Count > 11 || inputs.Any(i => i is null || !Enum.IsDefined(i.Capability) || !Code(i.SourceId) || !Code(i.ScopeId))
            || inputs.Select(i => i.Capability).Distinct().Count() != inputs.Count
            || additionalOperations is { Count: > 12 } || additionalOperations?.Any(o => !Enum.IsDefined(o)) == true)
            return Deny("SOURCE_USAGE_REQUEST_INVALID");
        if (!config.EnabledFeatures.Contains(feature)) return Deny("FEATURE_DISABLED");
        if (feature == SourceFeature.VerifiedScreening && mode != AnalysisMode.Verified) return Deny("MODE_UNSUPPORTED");
        if (RequiredCapabilities(feature).Any(c => !inputs.Any(i => i.Capability == c))) return Deny("CAPABILITY_MISSING");
        if (inputs.Any(i => !config.Selections.Contains(i))) return Deny("SOURCE_SELECTION_MISMATCH");
        var selected = inputs.Select(i => config.Sources.Single(s => s.SourceId == i.SourceId)).Distinct().ToArray();
        if (selected.Any(s => !s.Enabled)) return Deny("SOURCE_DISABLED");
        var operations = new List<SourceOperation> { SourceOperation.RetainOriginal, SourceOperation.InternalAnalysis, SourceOperation.Display };
        operations.Add(config.Profile == DeploymentProfile.Personal ? SourceOperation.PersonalUse : SourceOperation.CommercialUse);
        if (config.Profile == DeploymentProfile.Saas) operations.Add(SourceOperation.SaasConsumption);
        if (feature is SourceFeature.IndicativeTechnicals or SourceFeature.VerifiedScreening) operations.Add(SourceOperation.ProcessDerived);
        if (feature == SourceFeature.PriceExport) operations.Add(SourceOperation.Export);
        operations.AddRange(additionalOperations ?? []);
        if (operations.Contains(SourceOperation.HistoricalRetrieval) || operations.Contains(SourceOperation.RecurringRetrieval))
            operations.Add(SourceOperation.AutomatedAcquisition);
        var permissions = selected.SelectMany(s => operations.Distinct().Select(o =>
            s.Usage?.Permissions.SingleOrDefault(p => p.Operation == o)?.Permission ?? SourcePermissionState.Unknown)).ToArray();
        var export = feature == SourceFeature.PriceExport || operations.Contains(SourceOperation.Export);
        string PermissionFailure(string reason) => export ? "EXPORT_NOT_PERMITTED" : reason;
        if (permissions.Contains(SourcePermissionState.ExplicitProhibition)) return Deny(PermissionFailure("PERMISSION_PROHIBITED"));
        if (selected.Any(s => s.Usage?.ValidUntil is { } end && executionAt >= end)) return Deny(PermissionFailure("PERMISSION_EXPIRED"));
        if (permissions.Any(p => p != SourcePermissionState.ExplicitPermission)
            || selected.Any(s => s.UnresolvedRestrictions.Length > 0 || s.Usage is not { } u || u.ReviewedAt > executionAt
                || u.ValidFrom is null || executionAt < u.ValidFrom || u.ValidUntil is null))
            return Deny(PermissionFailure("PERMISSION_UNKNOWN"));
        // SaaS serving is not implemented; a commercial grant cannot enable a public deployment.
        if (config.Profile == DeploymentProfile.Saas) return Deny("UNSUPPORTED_PROFILE");
        if (inputs.Any(i => !config.Sources.Single(s => s.SourceId == i.SourceId).Capabilities.Any(c => c.Capability == i.Capability)))
            return Deny("CAPABILITY_UNSUPPORTED");
        if (mode == AnalysisMode.Verified && !evidenceGateSatisfied) return Deny("EVIDENCE_NOT_ADMITTED");
        var provenance = inputs.OrderBy(i => i.Capability).Select(i =>
        {
            var source = config.Sources.Single(s => s.SourceId == i.SourceId);
            return new SourceUseProvenance(i, source.Capabilities.Single(c => c.Capability == i.Capability).EvidenceReference,
                source.Usage!.DecisionId, source.Usage.ReviewedAt);
        }).ToArray();
        return new(true, "USAGE_PERMITTED", provenance);
    }
}
