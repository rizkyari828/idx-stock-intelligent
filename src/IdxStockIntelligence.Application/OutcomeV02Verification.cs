using System.Text;
using System.Text.Json;

namespace IdxStockIntelligence.Application;

public sealed record OutcomeV02VerificationProjection(Guid EnrollmentId, Guid InstrumentId, int HorizonSessions,
    string OutcomePolicyId, int SchemaVersion, DateOnly AnchorMarketDate, decimal AnchorClose, DateOnly HorizonMarketDate,
    decimal? HorizonClose, string State, string? Reason, decimal? PriceReturnPct, DateTimeOffset OutcomeKnownAt, DateTimeOffset RecordedAt);
public sealed record OutcomeV02VerificationResult(Guid EnrollmentId, Guid ObservationId, int HorizonSessions,
    OutcomeVerificationState State, DateTimeOffset VerifiedAt, string OutcomePolicyId, int SchemaVersion,
    DateTimeOffset OutcomeKnownAt, DateTimeOffset RecordedAt, OutcomeV02VerificationProjection? ReplayedProjection,
    IReadOnlyList<OutcomeDifference> Differences, bool DifferencesTruncated, string? Detail);

public static class OutcomeV02Verification
{
    public static bool Supports(string outcome, int schema, string candidate, int candidateSchema,
        string technical, int technicalSchema, int captureSchema) => outcome == OutcomeV02.PolicyId && schema == 1
        && candidate == ScreenerEvidenceTechnicalCandidates.PolicyId && candidateSchema == 1
        && technical == ScreenerEvidenceV02.PolicyId && technicalSchema == 1 && captureSchema == 1;
    public static void ValidateRequest(Guid enrollment, int horizon, JsonElement body)
    {
        if (enrollment == Guid.Empty || !OutcomeRequest.Horizons.Contains(horizon)
            || body.ValueKind != JsonValueKind.Object || body.EnumerateObject().Any())
            throw new ScreenerException(400, "OUTCOME_VERIFICATION_REQUEST_INVALID");
    }
    public static OutcomeV02VerificationResult Unavailable(Guid enrollment, Guid observation, int horizon, string policy,
        int schema, DateTimeOffset known, DateTimeOffset recorded, DateTimeOffset verified, OutcomeVerificationState state, string detail)
        => new(enrollment, observation, horizon, state, verified, policy, schema, known, recorded, null, [], false, detail);
    private static void Require(bool condition) => OutcomeV02.Require(condition, "RETAINED_INPUT_INVALID");
    private static bool Same(object a, object b) => ScreenerReferences.Hash(a) == ScreenerReferences.Hash(b);
    private static bool Ordered(IEnumerable<Guid> ids)
    {
        var values = ids.ToArray();
        return values.All(id => id != Guid.Empty) && values.Distinct().Count() == values.Length
            && values.SequenceEqual(values.OrderBy(id => id.ToString("D"), StringComparer.Ordinal));
    }
    // A retained record's premise must already belong to the closed manifest. Missing links are never fetched as replacements.
    public static void Closed(OutcomeV02Evidence evidence)
    {
        OutcomeV02.Authenticate(evidence); var rows = evidence.Records.ToDictionary(r => r.EvidenceId);
        foreach (var r in evidence.Records)
        {
            var p = ScreenerEvidenceBinding.Decode(r);
            Guid[] premises = p.Value switch { PriceValue v => [v.ConventionEvidenceId, v.CompletedSessionEvidenceId],
                ReopeningValue v => [v.SuspensionEvidenceId], ActionCoverageValue v => v.EventEvidenceIds.ToArray(), _ => [] };
            Require(premises.All(rows.ContainsKey));
            if (p.Operation == EvidenceOperation.CANCEL)
                Require(rows.Values.Any(old => old.RevisionSeriesId == r.RevisionSeriesId && old.RevisionNumber == r.SupersedesRevisionNumber));
        }
        Require(evidence.Artifacts.Select(a => a.ArtifactId).ToHashSet().SetEquals(rows.Values.Where(r => r.RawArtifactId is not null).Select(r => r.RawArtifactId!.Value)));
    }
    public static void AuthenticateManifest(OutcomeV02Observation stored, OutcomeV02Enrollment enrollment, OutcomeV02Evidence forward)
    {
        OutcomeV02.Validate(stored); OutcomeV02.Validate(enrollment); var p = stored.Projection; var m = p.Manifest;
        Require(p.EnrollmentId == enrollment.EnrollmentId && p.InstrumentId == enrollment.Projection.InstrumentId
            && p.OutcomeKnownAt >= enrollment.Projection.EnrollmentRecordedAt && m.SchemaVersion == 1 && m.OutcomePolicyId == p.OutcomePolicyId
            && m.EnrollmentId == enrollment.EnrollmentId && m.EnrollmentIdentity == enrollment.EnrollmentIdentity
            && m.EnrollmentBindingHash == enrollment.BindingHash && m.HorizonSessions == p.HorizonSessions && m.EvaluationCutoff == p.OutcomeKnownAt
            && Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(m, ScreenerReferences.JsonOptions)) <= OutcomeV02.MaximumManifestBytes
            && Ordered(m.Evidence.Select(e => e.EvidenceId)) && Ordered(m.Artifacts.Select(a => a.ArtifactId))
            && m.Evidence.Count <= ScreenerEvidenceAsOf.MaximumRecords && m.Calendar.Count is >= 2 and <= 366
            && (m.TerminalCondition is null || m.TerminalCondition.Length is > 0 and <= 128)
            && (m.TerminalCondition is not null || m.EndpointEvidenceId is not null));
        var links = forward.Records.Select(ScreenerEvidenceAsOf.Provenance).OrderBy(r => r.EvidenceId.ToString("D"), StringComparer.Ordinal).ToArray();
        Require(m.Evidence.SequenceEqual(links) && Same(m.Artifacts, forward.Artifacts));
        Closed(forward); var ids = links.Select(r => r.EvidenceId).ToHashSet();
        Require(m.EndpointEvidenceId is null || ids.Contains(m.EndpointEvidenceId.Value));
        Require(m.Calendar[0].Date == enrollment.Projection.EvaluationDate && m.Calendar[0].Classification == "ANCHOR"
            && m.Calendar[0].EvidenceIds.SequenceEqual(new[] { enrollment.Projection.Anchor.Price.CompletedSessionEvidenceId }));
        for (var i = 1; i < m.Calendar.Count; i++)
            Require(m.Calendar[i].Date == m.Calendar[i - 1].Date.AddDays(1)
                && m.Calendar[i].Classification is "ObservedTrading" or "Weekend" or "AnnouncedClosed"
                && Ordered(m.Calendar[i].EvidenceIds) && m.Calendar[i].EvidenceIds.All(ids.Contains));
        Require(m.Calendar[^1].Classification == "ObservedTrading" && m.Calendar.Skip(1).Count(d => d.Classification == "ObservedTrading") == p.HorizonSessions
            && m.Calendar[^1].Date <= OutcomeEvaluator.Through(p.OutcomeKnownAt) && m.Calendar[^1].Date <= ScreenerReadRequest.Horizon);
    }
    private static OutcomeVerificationProjection Projection(OutcomeV02ObservationProjection p) => new(p.EnrollmentId,
        p.InstrumentId, p.HorizonSessions, p.OutcomePolicyId, p.SchemaVersion, p.AnchorMarketDate, p.AnchorClose,
        p.HorizonMarketDate, p.HorizonClose, p.State, p.Reason, p.PriceReturnPct, p.OutcomeKnownAt, p.RecordedAt);
    public static OutcomeV02VerificationResult Verify(OutcomeV02Observation stored, OutcomeV02Enrollment enrollment,
        ScreenerTechnicalCandidateDecision candidate, ScreenerTechnicalCapture capture, OutcomeV02Evidence captured,
        OutcomeV02Evidence forward, DateTimeOffset verifiedAt)
    {
        var p = stored.Projection; var e = enrollment.Projection; var c = candidate.Projection; var q = capture.Projection.Result.Request;
        OutcomeV02VerificationResult Fail(OutcomeVerificationState state, string detail) => Unavailable(p.EnrollmentId,
            stored.ObservationId, p.HorizonSessions, p.OutcomePolicyId, p.SchemaVersion, p.OutcomeKnownAt, p.RecordedAt, verifiedAt, state, detail);
        if (!Supports(p.OutcomePolicyId, p.SchemaVersion, c.CandidatePolicyId, c.SchemaVersion, q.PolicyId, q.SchemaVersion, capture.Projection.CaptureSchemaVersion)
            || c.TechnicalPolicyId != ScreenerEvidenceV02.PolicyId || c.TechnicalSchemaVersion != 1 || c.CaptureSchemaVersion != 1
            || !Supports(e.OutcomePolicyId, e.SchemaVersion, e.CandidatePolicyId, e.CandidateSchemaVersion, e.TechnicalPolicyId, e.TechnicalSchemaVersion, e.CaptureSchemaVersion))
            return Fail(OutcomeVerificationState.PolicyVersionUnavailable, "POLICY_OR_SCHEMA_UNSUPPORTED");
        try
        {
            Closed(captured); Require(Ordered(e.CaptureEvidenceIds) && Ordered(e.Artifacts.Select(a => a.ArtifactId)) && Same(e.Artifacts, captured.Artifacts));
            var authenticated = OutcomeV02.Enroll(candidate, capture, captured, e.EnrollmentKnownAt, e.EnrollmentRecordedAt);
            Require(authenticated.EnrollmentId == enrollment.EnrollmentId && authenticated.EnrollmentIdentity == enrollment.EnrollmentIdentity
                && authenticated.BindingHash == enrollment.BindingHash);
            AuthenticateManifest(stored, enrollment, forward);
            var replay = OutcomeV02.Assess(authenticated, p.HorizonSessions, forward, p.Manifest.EvaluationCutoff, p.RecordedAt);
            if (replay.Observation is not { } observation) return Fail(OutcomeVerificationState.InputNotAvailable, "REPLAY_INPUT_INCOMPLETE");
            var result = OutcomeVerification.Compare(Projection(p), Projection(observation.Projection), p.Manifest.TerminalCondition, verifiedAt);
            var differences = result.Differences.Select(d => d.Field == "runId" ? d with { Field = "enrollmentId" } : d).ToList();
            if (!p.Manifest.Calendar.Zip(observation.Projection.Manifest.Calendar).All(pair => pair.First.Date == pair.Second.Date
                && pair.First.Classification == pair.Second.Classification && pair.First.EvidenceIds.SequenceEqual(pair.Second.EvidenceIds))
                || p.Manifest.Calendar.Count != observation.Projection.Manifest.Calendar.Count)
                differences.Add(new("manifest.calendar", "retained calendar", "replayed calendar", "TYPED_VALUE_DIFFERENT"));
            if (p.Manifest.EndpointEvidenceId != observation.Projection.Manifest.EndpointEvidenceId)
                differences.Add(new("manifest.endpointEvidenceId", p.Manifest.EndpointEvidenceId?.ToString("D"), observation.Projection.Manifest.EndpointEvidenceId?.ToString("D"), "TYPED_VALUE_DIFFERENT"));
            return new(p.EnrollmentId, stored.ObservationId, p.HorizonSessions, differences.Count == 0 ? OutcomeVerificationState.Match : OutcomeVerificationState.DifferentResult,
                verifiedAt, p.OutcomePolicyId, p.SchemaVersion, p.OutcomeKnownAt, p.RecordedAt,
                new(observation.Projection.EnrollmentId, observation.Projection.InstrumentId, observation.Projection.HorizonSessions,
                    observation.Projection.OutcomePolicyId, observation.Projection.SchemaVersion, observation.Projection.AnchorMarketDate,
                    observation.Projection.AnchorClose, observation.Projection.HorizonMarketDate, observation.Projection.HorizonClose,
                    observation.Projection.State, observation.Projection.Reason, observation.Projection.PriceReturnPct,
                    observation.Projection.OutcomeKnownAt, observation.Projection.RecordedAt),
                differences.OrderBy(d => d.Field, StringComparer.Ordinal).Take(OutcomeVerification.MaximumDifferences).ToArray(),
                differences.Count > OutcomeVerification.MaximumDifferences, null);
        }
        catch (EvidenceBindingException) { return Fail(OutcomeVerificationState.InputNotAvailable, "RETAINED_INPUT_INVALID"); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException or NullReferenceException or OverflowException)
        { return Fail(OutcomeVerificationState.InputNotAvailable, "RETAINED_INPUT_INVALID"); }
    }
}
