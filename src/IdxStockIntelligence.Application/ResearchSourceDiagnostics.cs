namespace IdxStockIntelligence.Application;

public sealed record ResearchFeatureDiagnostic(SourceFeature Feature, IReadOnlyList<SourceCapability> RequiredCapabilities,
    bool UsagePermitted, string Reason);
public sealed record ResearchSourceDiagnosticReport(string PolicyId, string UsagePolicyId, DeploymentProfile Profile,
    AnalysisMode Mode, DateTimeOffset ExecutionAt, string Classification, string Provenance,
    bool MarketDataRead, bool EvidenceGateSatisfied, bool CandidatePromotionPerformed,
    IReadOnlyList<ResearchFeatureDiagnostic> Features, IReadOnlyList<string> Limitations);

public static class ResearchSourceDiagnostics
{
    public const string PolicyId = "research-source-diagnostics-v0.1.0";

    public static AnalysisMode ParseMode(IReadOnlyDictionary<string, string?> query)
    {
        if (query.Keys.Any(k => k != "mode")) throw new ArgumentException("RESEARCH_DIAGNOSTICS_QUERY_INVALID");
        return query.GetValueOrDefault("mode") switch
        {
            null when !query.ContainsKey("mode") => AnalysisMode.Research,
            "RESEARCH" => AnalysisMode.Research,
            "VERIFIED" => AnalysisMode.Verified,
            _ => throw new ArgumentException("RESEARCH_DIAGNOSTICS_QUERY_INVALID")
        };
    }

    public static ResearchSourceDiagnosticReport Read(string? trustedConfiguration, AnalysisMode mode, DateTimeOffset executionAt)
    {
        if (!Enum.IsDefined(mode) || executionAt.Offset != TimeSpan.Zero || executionAt == default)
            throw new ArgumentException("RESEARCH_DIAGNOSTICS_QUERY_INVALID");
        var config = trustedConfiguration is null ? SourceUsagePolicy.Disabled() : SourceUsagePolicy.Parse(trustedConfiguration);
        var features = Enum.GetValues<SourceFeature>().Select(feature =>
        {
            var required = SourceUsagePolicy.RequiredCapabilities(feature);
            var selections = config.Selections.Where(s => required.Contains(s.Capability)).ToArray();
            // Configuration describes intent only; no market input has been inspected or admitted here.
            var decision = SourceUsagePolicy.Evaluate(config, feature, mode, selections, executionAt, evidenceGateSatisfied: false);
            return new ResearchFeatureDiagnostic(feature, required, decision.Permitted, decision.Reason);
        }).ToArray();
        return new(PolicyId, SourceUsagePolicy.PolicyId, config.Profile, mode, executionAt,
            "INDICATIVE_DIAGNOSTICS_ONLY", "TRUSTED_LOCAL_CONFIGURATION_ONLY", false, false, false, features,
            ["No market facts, prices, indicators, strategy results or recommendations were calculated.",
             "Usage permission is separate from capability, input quality and frozen Verified readiness.",
             "Permission records are trusted operator-reviewed inputs; this endpoint does not authenticate legal grants.",
             "Existing Research V0.1 datasets, membership and export bytes are unchanged."]);
    }
}
