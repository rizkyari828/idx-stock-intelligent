using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record ScreenerTechnicalCandidateProjection(string CandidatePolicyId, int SchemaVersion,
    Guid SourceCaptureId, string SourceInputHash, string SourceResultHash, int CaptureSchemaVersion,
    Guid SubjectId, string TechnicalPolicyId, int TechnicalSchemaVersion, string SessionId,
    DateOnly EvaluationDate, DateTimeOffset Cutoff, bool Candidate, SetupStatus Setup, IReadOnlyList<string> Reasons);
public sealed record ScreenerTechnicalCandidateDecision(Guid DecisionId, string ReplayIdentity,
    ScreenerTechnicalCandidateProjection Projection, DateTimeOffset? RecordedAt = null);

public static class ScreenerEvidenceTechnicalCandidates
{
    public const string PolicyId = "screener-technical-candidate-v0.2.0";
    public const int SchemaVersion = 1;
    public const int MaximumBytes = 8192;

    // Descriptive technical qualification only. Discovery membership/rank and investment decisions are separate.
    public static ScreenerTechnicalCandidateDecision Promote(ScreenerTechnicalCapture capture, CancellationToken ct = default)
    {
        var binding = capture.Projection.Result.Request;
        if (binding.PolicyId != ScreenerEvidenceV02.PolicyId || binding.SchemaVersion != ScreenerEvidenceV02.SchemaVersion
            || capture.Projection.CaptureSchemaVersion != ScreenerEvidenceTechnicalCapture.SchemaVersion)
            throw new EvidenceBindingException("TECHNICAL_CANDIDATE_POLICY_UNSUPPORTED");
        ScreenerEvidenceTechnicalCapture.Validate(capture, ct);
        var result = capture.Projection.Result; var request = result.Request;
        if (!Enum.IsDefined(result.Setup.Status)) throw new EvidenceBindingException("TECHNICAL_CAPTURE_MALFORMED");
        var eligibility = capture.Projection.ReadinessHistory.MaxBy(d => d.Date)!.MarketEligibility.Status;
        var projection = new ScreenerTechnicalCandidateProjection(PolicyId, SchemaVersion,
            capture.CaptureId, capture.InputHash, capture.ResultHash, capture.Projection.CaptureSchemaVersion,
            request.SubjectId, request.PolicyId, request.SchemaVersion, request.SessionId, request.EvaluationDate,
            request.Cutoff.ToUniversalTime(), ScreenerOrdering.QualifiesTechnically(eligibility, result.Setup),
            result.Setup.Status, ScreenerEvidenceReasons.Canonical(result.Reasons.Concat(result.Setup.Reasons)));
        if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(projection, ScreenerReferences.JsonOptions)) > MaximumBytes)
            throw new EvidenceBindingException("TECHNICAL_CANDIDATE_BOUND_EXCEEDED");
        var identity = ScreenerReferences.Hash(new { CandidatePolicyId = PolicyId, SchemaVersion,
            capture.CaptureId, capture.InputHash }, ct);
        return new(ScreenerEvidenceTechnicalCapture.Id(identity), ScreenerReferences.Hash(projection, ct), projection);
    }
}
