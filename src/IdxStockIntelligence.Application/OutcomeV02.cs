using System.Text.Json;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record OutcomeV02Artifact(Guid ArtifactId, string SourceId, string ContentHash, long ByteLength, DateTimeOffset RetrievedAt);
public sealed record OutcomeV02Evidence(IReadOnlyList<ScreenerEvidenceRecord> Records, IReadOnlyList<OutcomeV02Artifact> Artifacts)
{
    public ScreenerEvidenceAsOfResult Read(Guid subject, Guid exchange, string session, EvidenceClaim claim, DateOnly date, DateTimeOffset cutoff)
    {
        var rows = Records.Where(r => r.Claim == claim && (r.SubjectId == subject || r.ScopeKind == ScreenerScopeKind.EXCHANGE && r.SubjectId == exchange)).ToArray();
        var raw = Artifacts.ToDictionary(a => a.ArtifactId, a => new SourceReference(a.SourceId, a.ArtifactId, a.RetrievedAt, a.RetrievedAt, a.ContentHash));
        var result = ScreenerEvidenceAsOf.Resolve(new(subject, claim, date, cutoff), rows, Records.ToDictionary(r => r.EvidenceId), raw, exchange);
        if (result.FailureReason is not null) throw new EvidenceBindingException(result.FailureReason);
        return result with { Facts = result.Facts.Where(f => f.Value switch
        { CompletedSessionValue s => s.SessionId == session, ScheduledSessionValue s => s.SessionId == session,
            TradingStatusValue s => s.SessionId == session, PriceValue p => p.SessionId == session, _ => true }).ToArray() };
    }
}
public sealed record OutcomeV02EnrollmentProjection(string OutcomePolicyId, int SchemaVersion, Guid CandidateDecisionId,
    string CandidatePolicyId, int CandidateSchemaVersion, string CandidateReplayIdentity, DateTimeOffset CandidateRecordedAt,
    Guid CaptureId, int CaptureSchemaVersion, string TechnicalPolicyId, int TechnicalSchemaVersion,
    string CaptureInputHash, string CaptureResultHash, string CaptureReplayIdentity, DateTimeOffset CaptureRecordedAt,
    Guid InstrumentId, Guid ExchangeId, string SessionId, DateOnly EvaluationDate, DateTimeOffset OriginalCutoff,
    ScreenerPriceField PriceField, ScreenerReadinessBarBinding Anchor, string? AnchorReason,
    IReadOnlyList<Guid> CaptureEvidenceIds, IReadOnlyList<OutcomeV02Artifact> Artifacts,
    DateTimeOffset EnrollmentKnownAt, DateTimeOffset EnrollmentRecordedAt, DateTimeOffset EnrollmentDeadline);
public sealed record OutcomeV02Enrollment(Guid EnrollmentId, string EnrollmentIdentity, string BindingHash, OutcomeV02EnrollmentProjection Projection);
public sealed record OutcomeV02CalendarDay(DateOnly Date, string Classification, IReadOnlyList<Guid> EvidenceIds);
public sealed record OutcomeV02Horizon(DateOnly? Date, string State, string? Reason, IReadOnlyList<OutcomeV02CalendarDay> Calendar);
public sealed record OutcomeV02Manifest(int SchemaVersion, string OutcomePolicyId, Guid EnrollmentId, string EnrollmentIdentity,
    string EnrollmentBindingHash, int HorizonSessions, DateTimeOffset EvaluationCutoff, IReadOnlyList<OutcomeV02CalendarDay> Calendar,
    IReadOnlyList<ScreenerEvidenceProvenance> Evidence, IReadOnlyList<OutcomeV02Artifact> Artifacts, Guid? EndpointEvidenceId, string? TerminalCondition);
public sealed record OutcomeV02ObservationProjection(string OutcomePolicyId, int SchemaVersion, Guid EnrollmentId, int HorizonSessions,
    Guid InstrumentId, DateOnly AnchorMarketDate, decimal AnchorClose, DateOnly HorizonMarketDate, decimal? HorizonClose,
    string State, string? Reason, decimal? PriceReturnPct, DateTimeOffset OutcomeKnownAt, DateTimeOffset RecordedAt, OutcomeV02Manifest Manifest);
public sealed record OutcomeV02Observation(Guid ObservationId, string ObservationIdentity, string ResultHash, OutcomeV02ObservationProjection Projection);
public sealed record OutcomeV02Assessment(string State, string? Reason, OutcomeV02Observation? Observation);

public static class OutcomeV02
{
    public const string PolicyId = "outcome-v0.2.0";
    public const int SchemaVersion = 1;
    public const int MaximumManifestBytes = 65536;
    public static DateTimeOffset Deadline(DateOnly date) => new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
    public static string EnrollmentIdentity(Guid candidate) => ScreenerReferences.Hash(new { candidateDecisionId = candidate, outcomePolicyId = PolicyId, outcomeSchemaVersion = SchemaVersion });
    public static string ObservationIdentity(Guid enrollment, int horizon) => ScreenerReferences.Hash(new { enrollmentId = enrollment, horizonSessions = horizon });
    public static void Require(bool valid, string reason)
    { if (!valid) throw new EvidenceBindingException(reason); }
    public static void CheckDeadline(DateTimeOffset now, DateTimeOffset deadline) => Require(now < deadline, "OUTCOME_ENROLLMENT_DEADLINE_EXCEEDED");
    private static ScreenerEvidenceProvenance[] References(OutcomeV02Evidence evidence) => evidence.Records
        .Select(ScreenerEvidenceAsOf.Provenance).OrderBy(r => r.EvidenceId.ToString("D"), StringComparer.Ordinal).ToArray();
    private static OutcomeV02Artifact[] Artifacts(OutcomeV02Evidence evidence) => evidence.Artifacts.OrderBy(a => a.ArtifactId.ToString("D"), StringComparer.Ordinal).ToArray();
    private static void Bound(object manifest) => Require(System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(manifest, ScreenerReferences.JsonOptions)) <= MaximumManifestBytes, "OUTCOME_MANIFEST_BOUND_EXCEEDED");
    public static void Authenticate(OutcomeV02Evidence evidence)
    {
        Require(evidence.Records.Count <= ScreenerEvidenceAsOf.MaximumRecords && evidence.Records.Select(r => r.EvidenceId).Distinct().Count() == evidence.Records.Count,
            "OUTCOME_EVIDENCE_BOUND_OR_IDENTITY_INVALID");
        Require(evidence.Artifacts.Select(a => a.ArtifactId).Distinct().Count() == evidence.Artifacts.Count
            && evidence.Artifacts.All(a => a.ArtifactId != Guid.Empty && ScreenerReferences.IsHash(a.ContentHash) && a.ByteLength >= 0), "OUTCOME_ARTIFACT_INVALID");
        foreach (var r in evidence.Records) ScreenerEvidenceBinding.Decode(r);
    }
    public static OutcomeV02Enrollment Enroll(ScreenerTechnicalCandidateDecision candidate, ScreenerTechnicalCapture capture,
        OutcomeV02Evidence evidence, DateTimeOffset knownAt, DateTimeOffset recordedAt)
    {
        var p = candidate.Projection; var r = capture.Projection.Result; var q = r.Request;
        Require(p.CandidatePolicyId == ScreenerEvidenceTechnicalCandidates.PolicyId && p.SchemaVersion == 1
            && p.TechnicalPolicyId == ScreenerEvidenceV02.PolicyId && p.TechnicalSchemaVersion == 1 && p.CaptureSchemaVersion == 1,
            "OUTCOME_SOURCE_POLICY_UNSUPPORTED");
        ScreenerEvidenceTechnicalCapture.Validate(capture);
        var candidateKey = ScreenerReferences.Hash(new { CandidatePolicyId = p.CandidatePolicyId, SchemaVersion = p.SchemaVersion, capture.CaptureId, capture.InputHash });
        Require(candidate.DecisionId == ScreenerEvidenceTechnicalCapture.Id(candidateKey) && candidate.ReplayIdentity == ScreenerReferences.Hash(p)
            && p.SourceCaptureId == capture.CaptureId && p.SourceInputHash == capture.InputHash && p.SourceResultHash == capture.ResultHash
            && p.SubjectId == q.SubjectId && p.SessionId == q.SessionId && p.EvaluationDate == q.EvaluationDate && p.Cutoff == q.Cutoff
            && p.Setup == r.Setup.Status && p.Reasons.SequenceEqual(ScreenerEvidenceReasons.Canonical(r.Reasons.Concat(r.Setup.Reasons))),
            "OUTCOME_CANDIDATE_INTEGRITY_CONFLICT");
        Require(p.Candidate, "OUTCOME_NON_CANDIDATE"); // Stored decision only; no technical execution/qualification here.
        Require(capture.RecordedAt is { } && candidate.RecordedAt is { } && q.Cutoff <= capture.RecordedAt
            && capture.RecordedAt <= candidate.RecordedAt && candidate.RecordedAt <= knownAt && knownAt <= recordedAt, "OUTCOME_ENROLLMENT_CHRONOLOGY_INVALID");
        Require(q.EvaluationDate >= ScreenerReadRequest.Anchor && q.EvaluationDate <= ScreenerReadRequest.Horizon, "OUTCOME_BASE_OUT_OF_SCOPE");
        CheckDeadline(knownAt, Deadline(q.EvaluationDate)); CheckDeadline(recordedAt, Deadline(q.EvaluationDate));
        Authenticate(evidence);
        var refs = References(evidence);
        Require(ScreenerReferences.Hash(refs) == ScreenerReferences.Hash(r.References), "OUTCOME_CAPTURE_EVIDENCE_UNAVAILABLE");
        var anchor = r.StockBindings.Single(b => b.Date == q.EvaluationDate && b.SubjectId == q.SubjectId);
        var exchange = anchor.Evidence.ExchangeId;
        var proof = evidence.Records.SingleOrDefault(e => e.EvidenceId == anchor.Price.CompletedSessionEvidenceId);
        Require(proof is not null && ScreenerEvidenceBinding.Decode(proof).Value is CompletedSessionValue { Completion: EvidenceCompletion.COMPLETED, CompletedAt: not null } completed
            && OutcomeEvaluator.Through(completed.CompletedAt.Value) == q.EvaluationDate && completed.CompletedAt <= proof.KnownAt && proof.KnownAt <= q.Cutoff,
            "OUTCOME_ANCHOR_COMPLETION_INVALID");
        var price = evidence.Read(q.SubjectId, exchange, q.SessionId, EvidenceClaim.GenuinePriceObservation, q.EvaluationDate, q.Cutoff);
        Require(price.Facts.Any(f => f.Quality == EvidenceQuality.Verified && f.Selected == anchor.Evidence && f.Value is PriceValue v
            && ScreenerReferences.Hash(v) == ScreenerReferences.Hash(anchor.Price) && f.ObservationSource == anchor.ObservationSource), "OUTCOME_ANCHOR_EVIDENCE_INVALID");
        ScreenerEvidenceAsOfResult Original(EvidenceClaim claim) => evidence.Read(q.SubjectId, exchange, q.SessionId, claim, q.EvaluationDate, q.Cutoff);
        var currency = Single<CurrencyValue>(Original(EvidenceClaim.Currency));
        var originalIdentity = Original(EvidenceClaim.StableIdentity); var listing = Single<ListingValue>(Original(EvidenceClaim.ListingCoverage));
        var status = ScreenerEvidenceAsOf.TradingStatus(Original(EvidenceClaim.Suspension), Original(EvidenceClaim.Reopening), Original(EvidenceClaim.TradingStatus));
        var mechanism = ScreenerEvidenceReadiness.Combine(Original(EvidenceClaim.ExchangeRuleVersion), Original(EvidenceClaim.MechanismException), v => v switch
        { RuleValue rule => rule.Mechanism, ExceptionValue exception => exception.Mechanism, _ => (EvidenceMechanism?)null });
        var convention = evidence.Records.Single(e => e.EvidenceId == anchor.Price.ConventionEvidenceId);
        var action = Original(EvidenceClaim.CorporateAction);
        Require(Single<IdentityValue>(originalIdentity) is not null && originalIdentity.Facts[0].Selected?.ExchangeId == exchange
            && Single<SecurityTypeValue>(Original(EvidenceClaim.SecurityType))?.Classification == EvidenceSecurityType.ORDINARY
            && currency is not null && listing is not null && listing.ListingDate <= q.EvaluationDate
            && !(Single<DelistingValue>(Original(EvidenceClaim.Delisting))?.DelistingDate <= q.EvaluationDate)
            && status.Quality == EvidenceQuality.Verified && status.Status == TradingStatus.Trading
            && mechanism.Quality == EvidenceQuality.Verified && mechanism.Value == EvidenceMechanism.CONTINUOUS
            && ScreenerEvidenceBinding.Decode(convention).Value is ConventionValue { PriceKind: EvidencePriceKind.STOCK_RAW, Continuity: EvidenceContinuity.RAW_AS_TRADED } cv
            && q.PriceField.Matches(cv) && action.Facts.Any(f => f.Quality == EvidenceQuality.Verified && f.Value is ActionCoverageValue { Coverage: EvidenceCoverage.COMPLETE })
            && !action.Facts.Any(f => f.Quality == EvidenceQuality.Conflicting || f.Value is ActionEventValue), "OUTCOME_CAPTURE_CONTEXT_INVALID");
        var reason = anchor.Price.Bar.Volume == 0 || currency?.CurrencyCode != "IDR" ? "CAPTURED_PRICE_BASIS_UNVERIFIED" : null;
        var projection = new OutcomeV02EnrollmentProjection(PolicyId, 1, candidate.DecisionId, p.CandidatePolicyId, p.SchemaVersion, candidate.ReplayIdentity,
            candidate.RecordedAt!.Value, capture.CaptureId, 1, q.PolicyId, q.SchemaVersion, capture.InputHash, capture.ResultHash, r.ReplayIdentity,
            capture.RecordedAt!.Value, q.SubjectId, exchange, q.SessionId, q.EvaluationDate, q.Cutoff, q.PriceField, anchor, reason,
            refs.Select(e => e.EvidenceId).ToArray(), Artifacts(evidence), knownAt, recordedAt, Deadline(q.EvaluationDate));
        Bound(projection); var identity = EnrollmentIdentity(candidate.DecisionId);
        return new(ScreenerEvidenceTechnicalCapture.Id(identity), identity, ScreenerReferences.Hash(projection), projection);
    }
    public static void Validate(OutcomeV02Enrollment enrollment)
    {
        var p = enrollment.Projection;
        Require(p.OutcomePolicyId == PolicyId && p.SchemaVersion == 1 && enrollment.EnrollmentIdentity == EnrollmentIdentity(p.CandidateDecisionId)
            && enrollment.EnrollmentId == ScreenerEvidenceTechnicalCapture.Id(enrollment.EnrollmentIdentity)
            && enrollment.BindingHash == ScreenerReferences.Hash(p), "OUTCOME_ENROLLMENT_INTEGRITY_CONFLICT");
        Require(p.OriginalCutoff <= p.CaptureRecordedAt && p.CaptureRecordedAt <= p.CandidateRecordedAt && p.CandidateRecordedAt <= p.EnrollmentKnownAt
            && p.EnrollmentKnownAt <= p.EnrollmentRecordedAt && p.EnrollmentRecordedAt < p.EnrollmentDeadline
            && p.EnrollmentDeadline == Deadline(p.EvaluationDate), "OUTCOME_ENROLLMENT_CHRONOLOGY_INVALID");
        Bound(p);
    }
    private static T? Single<T>(ScreenerEvidenceAsOfResult result) where T : ScreenerEvidenceValue =>
        result.Facts.Count == 1 && result.Facts[0].Quality == EvidenceQuality.Verified ? result.Facts[0].Value as T : null;
    public static OutcomeV02Horizon Horizon(OutcomeV02Enrollment enrollment, int n, OutcomeV02Evidence evidence, DateTimeOffset cutoff)
    {
        Validate(enrollment); Require(OutcomeRequest.Horizons.Contains(n), "OUTCOME_HORIZON_INVALID");
        var p = enrollment.Projection; var today = OutcomeEvaluator.Through(cutoff); var through = today < ScreenerReadRequest.Horizon ? today : ScreenerReadRequest.Horizon;
        var days = new List<OutcomeV02CalendarDay> { new(p.EvaluationDate, "ANCHOR", [p.Anchor.Price.CompletedSessionEvidenceId]) }; var count = 0;
        for (var d = p.EvaluationDate.AddDays(1); d <= through; d = d.AddDays(1))
        {
            var scheduled = evidence.Read(p.InstrumentId, p.ExchangeId, p.SessionId, EvidenceClaim.ScheduledSession, d, cutoff);
            var completed = evidence.Read(p.InstrumentId, p.ExchangeId, p.SessionId, EvidenceClaim.CompletedSession, d, cutoff);
            var schedule = Single<ScheduledSessionValue>(scheduled); var completion = Single<CompletedSessionValue>(completed);
            var ids = scheduled.Facts.Concat(completed.Facts).SelectMany(f => f.Candidates.Select(c => c.Provenance))
                .Select(r => r.EvidenceId).Distinct().OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray();
            var conflict = scheduled.Facts.Any(f => f.Quality == EvidenceQuality.Conflicting) || completed.Facts.Any(f => f.Quality == EvidenceQuality.Conflicting)
                || schedule?.Schedule == EvidenceSchedule.CLOSED && completion?.Completion == EvidenceCompletion.COMPLETED;
            var weekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (!conflict && (schedule?.Schedule == EvidenceSchedule.CLOSED || weekend && schedule?.Schedule != EvidenceSchedule.OPEN && completion?.Completion != EvidenceCompletion.COMPLETED))
            { days.Add(new(d, schedule?.Schedule == EvidenceSchedule.CLOSED ? "AnnouncedClosed" : "Weekend", ids)); continue; }
            var complete = !conflict && completion is { Completion: EvidenceCompletion.COMPLETED, CompletedAt: not null }
                && OutcomeEvaluator.Through(completion.CompletedAt.Value) == d && completion.CompletedAt > p.EnrollmentRecordedAt
                && completion.CompletedAt <= cutoff && completion.CompletedAt <= completed.Facts[0].Selected?.Chronology.KnownAt
                && (d != today || TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(cutoff, "Asia/Jakarta").DateTime) >= new TimeOnly(19, 0));
            days.Add(new(d, complete ? "ObservedTrading" : "Unknown", ids));
            if (!complete) return new(null, d == today && !conflict ? "PENDING" : "SESSION_UNAVAILABLE", conflict ? "SESSION_CONFLICT" : "SESSION_UNCONFIRMED", days);
            if (++count == n) return new(d, "RESOLVED", null, days);
        }
        return new(null, today > ScreenerReadRequest.Horizon ? "SESSION_UNAVAILABLE" : "PENDING",
            today > ScreenerReadRequest.Horizon ? "HORIZON_OUT_OF_SCOPE" : "HORIZON_NOT_REACHED", days);
    }
    public static OutcomeV02Assessment Assess(OutcomeV02Enrollment enrollment, int n, OutcomeV02Evidence evidence, DateTimeOffset knownAt, DateTimeOffset recordedAt)
    {
        Validate(enrollment); Authenticate(evidence);
        var p = enrollment.Projection;
        Require(p.EnrollmentRecordedAt <= knownAt && knownAt <= recordedAt, "OUTCOME_OBSERVATION_CHRONOLOGY_INVALID");
        var horizon = Horizon(enrollment, n, evidence, knownAt);
        if (horizon.Date is not { } date) return new(horizon.State, horizon.Reason, null);
        ScreenerEvidenceAsOfResult Read(EvidenceClaim claim, DateOnly? day = null) => evidence.Read(p.InstrumentId, p.ExchangeId, p.SessionId, claim, day ?? date, knownAt);
        var prices = Read(EvidenceClaim.GenuinePriceObservation);
        var endpoint = prices.Facts.Where(f => f.Quality == EvidenceQuality.Verified && f.Value is PriceValue v && f.Selected?.SourceId == p.PriceField.SourceId
            && evidence.Records.Any(r => r.EvidenceId == v.ConventionEvidenceId && ScreenerEvidenceBinding.Decode(r).Value is ConventionValue c && p.PriceField.Matches(c))).ToArray();
        var chosen = endpoint.Length == 1 ? endpoint[0] : null; var price = chosen?.Value as PriceValue;
        OutcomeV02Assessment Terminal(string state, string? reason, decimal? value = null)
        {
            var manifest = new OutcomeV02Manifest(1, PolicyId, enrollment.EnrollmentId, enrollment.EnrollmentIdentity, enrollment.BindingHash, n, knownAt,
                horizon.Calendar, References(evidence), Artifacts(evidence), chosen?.Selected?.EvidenceId, reason);
            Bound(manifest);
            var projection = new OutcomeV02ObservationProjection(PolicyId, 1, enrollment.EnrollmentId, n, p.InstrumentId, p.EvaluationDate,
                p.Anchor.Price.Bar.Close.Value, date, price?.Bar.Close.Value, state, reason, value, knownAt, recordedAt, manifest);
            var identity = ObservationIdentity(enrollment.EnrollmentId, n);
            return new(state, reason, new(ScreenerEvidenceTechnicalCapture.Id(identity), identity, ScreenerReferences.Hash(projection), projection));
        }
        if (p.AnchorReason is not null) return Terminal("ANCHOR_UNAVAILABLE", p.AnchorReason);
        var delisting = Single<DelistingValue>(Read(EvidenceClaim.Delisting));
        if (delisting?.DelistingDate < date) return Terminal("DATA_UNAVAILABLE", "POST_DELISTING");
        var status = ScreenerEvidenceAsOf.TradingStatus(Read(EvidenceClaim.Suspension), Read(EvidenceClaim.Reopening), Read(EvidenceClaim.TradingStatus));
        if (status.Quality == EvidenceQuality.Verified && status.Status == TradingStatus.Suspended) return Terminal("DATA_UNAVAILABLE", "SUSPENDED_AT_HORIZON");
        var type = Single<SecurityTypeValue>(Read(EvidenceClaim.SecurityType)); var currency = Single<CurrencyValue>(Read(EvidenceClaim.Currency));
        if (type is { Classification: not EvidenceSecurityType.ORDINARY } || currency is { CurrencyCode: not "IDR" })
            return Terminal("DATA_UNAVAILABLE", "ENDPOINT_CLASSIFICATION_UNSUPPORTED");
        var resolvedMechanism = ScreenerEvidenceReadiness.Combine(Read(EvidenceClaim.ExchangeRuleVersion), Read(EvidenceClaim.MechanismException), v => v switch
        { RuleValue r => r.Mechanism, ExceptionValue e => e.Mechanism, _ => (EvidenceMechanism?)null });
        var mechanism = resolvedMechanism.Quality == EvidenceQuality.Verified ? resolvedMechanism.Value : null;
        if (mechanism is EvidenceMechanism.CALL_AUCTION or EvidenceMechanism.OTHER) return Terminal("DATA_UNAVAILABLE", "SPECIAL_REGIME_UNSUPPORTED");
        var actions = new List<(DateOnly Date, ScreenerEvidenceAsOfResult Result)>(); var identities = true; var currencies = true; var conventions = true;
        for (var d = p.EvaluationDate; d <= date; d = d.AddDays(1))
        {
            actions.Add((d, Read(EvidenceClaim.CorporateAction, d)));
            var identity = Read(EvidenceClaim.StableIdentity, d);
            identities &= Single<IdentityValue>(identity) is not null && identity.Facts[0].Selected?.ExchangeId == p.ExchangeId
                && Single<SecurityTypeValue>(Read(EvidenceClaim.SecurityType, d))?.Classification == EvidenceSecurityType.ORDINARY;
            currencies &= Single<CurrencyValue>(Read(EvidenceClaim.Currency, d))?.CurrencyCode == "IDR";
            var matching = Read(EvidenceClaim.SourcePriceConvention, d).Facts.Where(f => f.Quality == EvidenceQuality.Verified && f.Value is ConventionValue c
                && p.PriceField.Matches(c) && c.PriceKind == EvidencePriceKind.STOCK_RAW && c.Continuity == EvidenceContinuity.RAW_AS_TRADED).ToArray();
            conventions &= matching.Length == 1;
        }
        var events = actions.SelectMany(a => a.Result.Facts).Where(f => f.Quality == EvidenceQuality.Verified).Select(f => f.Value).OfType<ActionEventValue>().ToArray();
        if (events.Any(e => e.ActionType is EvidenceActionType.SPLIT or EvidenceActionType.REVERSE_SPLIT)) return Terminal("BASIS_UNCERTAIN", "UNIT_CHANGING_EVENT");
        if (events.Any(e => e.ActionType is EvidenceActionType.RIGHTS_OR_SHARE_EVENT or EvidenceActionType.BONUS_OR_SHARE_DISTRIBUTION)) return Terminal("BASIS_UNCERTAIN", "CAPITAL_ACTION_UNSUPPORTED");
        if (events.Any(e => e.ActionType is EvidenceActionType.CONVERSION or EvidenceActionType.MERGER_OR_REORGANIZATION)) return Terminal("BASIS_UNCERTAIN", "SECURITY_CONVERSION_UNSUPPORTED");
        if (actions.Any(a => Single<CurrencyValue>(Read(EvidenceClaim.Currency, a.Date)) is { CurrencyCode: not "IDR" })) return Terminal("BASIS_UNCERTAIN", "PRICE_CONVENTION_UNSUPPORTED");
        if (actions.Any(a => Read(EvidenceClaim.SourcePriceConvention, a.Date).Facts.Any(f => f.Quality == EvidenceQuality.Verified
            && f.Value is ConventionValue cv && p.PriceField.Matches(cv) && cv.PriceKind == EvidencePriceKind.INDEX_LEVEL)))
            return Terminal("BASIS_UNCERTAIN", "PRICE_CONVENTION_UNSUPPORTED");
        var wholeCoverage = actions.All(a => a.Result.Facts.Any(f => f.Quality == EvidenceQuality.Verified && f.Value is ActionCoverageValue { Coverage: EvidenceCoverage.COMPLETE }
                && f.Selected is { } link && link.Scope.From <= p.EvaluationDate && link.Scope.To >= date)
            && !a.Result.Facts.Any(f => f.Quality == EvidenceQuality.Conflicting || f.Value is ActionCoverageValue && f.Quality != EvidenceQuality.Verified
                || f.Value is ActionCoverageValue { Coverage: EvidenceCoverage.PARTIAL }));
        var comparability = ScreenerPriceComparabilityEvaluator.Evaluate(new(identities, currencies, price is not null && conventions, true, wholeCoverage, price is not null, []));
        if (price is null) return new("UNRESOLVED", "HORIZON_BAR_MISSING", null);
        if (price.Bar.Volume <= 0) return new("UNRESOLVED", "ZERO_VOLUME_UNEXPLAINED", null);
        if (type?.Classification != EvidenceSecurityType.ORDINARY || status.Quality != EvidenceQuality.Verified || status.Status != TradingStatus.Trading || mechanism != EvidenceMechanism.CONTINUOUS)
            return new("UNRESOLVED", "STATUS_UNKNOWN", null);
        if (comparability.State != PriceComparability.Cleared) return new("UNRESOLVED", "PRICE_COMPARABILITY_UNVERIFIED", null);
        try { return Terminal("AVAILABLE", null, checked(100m * (price.Bar.Close.Value / p.Anchor.Price.Bar.Close.Value - 1m))); }
        catch (OverflowException) { return new("UNRESOLVED", "NUMERIC_OUT_OF_RANGE", null); }
    }
    public static void Validate(OutcomeV02Observation observation)
    {
        var p = observation.Projection;
        Require(p.OutcomePolicyId == PolicyId && p.SchemaVersion == 1 && OutcomeRequest.Horizons.Contains(p.HorizonSessions)
            && observation.ObservationIdentity == ObservationIdentity(p.EnrollmentId, p.HorizonSessions)
            && observation.ObservationId == ScreenerEvidenceTechnicalCapture.Id(observation.ObservationIdentity)
            && observation.ResultHash == ScreenerReferences.Hash(p) && p.RecordedAt >= p.OutcomeKnownAt
            && p.State is "AVAILABLE" or "ANCHOR_UNAVAILABLE" or "DATA_UNAVAILABLE" or "BASIS_UNCERTAIN"
            && (p.State == "AVAILABLE" ? p.Reason is null && p.PriceReturnPct is not null : p.Reason is { Length: > 0 and <= 128 } && p.PriceReturnPct is null), "OUTCOME_OBSERVATION_INTEGRITY_CONFLICT");
        Bound(p.Manifest);
    }
}
