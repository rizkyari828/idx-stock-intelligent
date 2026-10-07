using System.Globalization;
using System.Text.Json;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record ScreenerEvidenceAsOfRequest(Guid SubjectId, EvidenceClaim Claim, DateOnly EvaluationDate,
    DateTimeOffset Cutoff, ScreenerScopeKind ScopeKind = ScreenerScopeKind.INSTRUMENT)
{
    public EffectiveInterval Scope => new(EvaluationDate, EvaluationDate);
    public void Validate()
    {
        if (SubjectId == Guid.Empty || !Enum.IsDefined(Claim) || !Enum.IsDefined(ScopeKind))
            throw new ArgumentException("Invalid retained evidence request.");
    }
}

public sealed record ScreenerEvidenceProvenance(Guid EvidenceId, long RevisionNumber, string? RevisionSeriesId,
    long? SupersedesRevisionNumber, EvidenceClass EvidenceClass, ScreenerScopeKind ScopeKind, Guid ExchangeId,
    EffectiveInterval Scope, SourceAuthorityTier Authority, string SourceId, string? SourceReference,
    Guid? RawArtifactId, int PayloadVersion, string PayloadHash, EvidenceChronology Chronology);
public sealed record ScreenerEvidenceAsOfCandidate(ScreenerEvidenceValue Value, EvidenceQuality Quality, ScreenerEvidenceProvenance Provenance);
public sealed record ScreenerEvidenceAsOfFact(string LogicalKey, ScreenerEvidenceValue? Value, EvidenceQuality Quality,
    ScreenerEvidenceProvenance? Selected, IReadOnlyList<ScreenerEvidenceAsOfCandidate> Candidates,
    SourceReference? ObservationSource = null);
public sealed record ScreenerEvidenceAsOfDiagnostic(Guid? EvidenceId, string Reason);
public sealed record ScreenerEvidenceAsOfResult(ScreenerEvidenceAsOfRequest Request, Guid? ExchangeId, EvidenceQuality Quality,
    IReadOnlyList<ScreenerEvidenceAsOfFact> Facts, IReadOnlyList<string> Reasons,
    IReadOnlyList<ScreenerEvidenceAsOfDiagnostic> Diagnostics, IReadOnlyList<ScreenerEvidenceProvenance> References,
    string? FailureReason = null, IReadOnlyList<Guid>? ExaminedEvidenceIds = null);

public static class ScreenerEvidenceAsOf
{
    // Personal-use ceiling applies to the entire retained set, including exact premises. Never truncate.
    public const int MaximumRecords = 512;
    public const string BoundExceeded = "PERSISTED_EVIDENCE_BOUND_EXCEEDED";
    public const string InputUnavailable = "PERSISTED_EVIDENCE_INPUT_UNAVAILABLE";
    public static EvidenceChronology Chronology(ScreenerEvidenceRecord r) => new(r.EffectiveAt, r.PublishedAt, r.RetrievedAt, r.KnownAt, r.RecordedAt);
    public static ScreenerEvidenceProvenance Provenance(ScreenerEvidenceRecord r) => new(r.EvidenceId, r.RevisionNumber,
        r.RevisionSeriesId, r.SupersedesRevisionNumber, r.EvidenceClass!.Value, r.ScopeKind!.Value, r.ScopeExchangeId!.Value,
        new(r.EffectiveFrom, r.EffectiveTo), r.AuthorityTier, r.SourceId, r.SourceReference, r.RawArtifactId,
        r.PayloadSchemaVersion!.Value, r.PayloadSha256, Chronology(r));
    public static ScreenerEvidenceAsOfResult Failure(ScreenerEvidenceAsOfRequest request, string reason, Guid? evidenceId = null) =>
        new(request, null, EvidenceQuality.Unknown, [], [reason], [new(evidenceId, reason)], [], reason);

    public static ScreenerEvidenceAsOfResult Resolve(ScreenerEvidenceAsOfRequest request,
        IReadOnlyList<ScreenerEvidenceRecord> records, IReadOnlyDictionary<Guid, ScreenerEvidenceRecord>? premises = null,
        IReadOnlyDictionary<Guid, SourceReference>? rawSources = null, Guid? establishedExchange = null)
    {
        request.Validate();
        premises ??= records.ToDictionary(r => r.EvidenceId);
        if (records.Select(r => r.EvidenceId).Concat(premises.Keys).Distinct().Take(MaximumRecords + 1).Count() > MaximumRecords)
            return Failure(request, BoundExceeded);
        var diagnostics = new List<ScreenerEvidenceAsOfDiagnostic>();
        var decoded = new List<(ScreenerEvidenceRecord Row, ScreenerEvidencePayload Payload)>();
        Guid? current = null;
        try
        {
            foreach (var r in records.OrderBy(r => r.EvidenceId.ToString("D"), StringComparer.Ordinal))
            {
                current = r.EvidenceId;
                if (r.Claim != request.Claim || !ScreenerEvidenceValidity.Visible(Chronology(r), request.Cutoff)) continue;
                if (!r.IsBound) { diagnostics.Add(new(r.EvidenceId, "PERSISTED_EVIDENCE_UNBOUND")); continue; }
                var p = ScreenerEvidenceBinding.Decode(r);
                var applicableSubject = request.ScopeKind == ScreenerScopeKind.EXCHANGE
                    ? r.ScopeKind == ScreenerScopeKind.EXCHANGE && r.SubjectId == request.SubjectId && r.ScopeExchangeId == request.SubjectId
                    : r.ScopeKind == ScreenerScopeKind.INSTRUMENT && r.SubjectId == request.SubjectId
                        || r.ScopeKind == ScreenerScopeKind.EXCHANGE && r.ScopeExchangeId == establishedExchange;
                if (!applicableSubject) continue;
                if (!ScreenerSourceAdmission.Admit(r.Claim, r.AuthorityTier).Admitted)
                { diagnostics.Add(new(r.EvidenceId, ScreenerEvidenceReasons.EvidenceSourceInadmissible)); continue; }
                decoded.Add((r, p));
            }
            var cancelled = new HashSet<Guid>();
            foreach (var (r, p) in decoded.Where(x => x.Payload.Operation == EvidenceOperation.CANCEL))
            {
                current = r.EvidenceId;
                var target = premises.Values.SingleOrDefault(t => t.RevisionSeriesId == r.RevisionSeriesId
                    && t.RevisionNumber == r.SupersedesRevisionNumber);
                Require(target is not null && target.Claim == r.Claim && target.SubjectId == r.SubjectId
                    && target.ScopeKind == r.ScopeKind && target.ScopeExchangeId == r.ScopeExchangeId
                    && target.EvidenceClass == r.EvidenceClass && target.SourceId == r.SourceId
                    && ScreenerEvidenceBinding.Decode(target).Operation == EvidenceOperation.ASSERT
                    && ScreenerEvidenceValidity.Visible(Chronology(target), r.KnownAt));
                if (r.AuthorityTier <= target!.AuthorityTier && new EffectiveInterval(r.EffectiveFrom, r.EffectiveTo).Contains(request.EvaluationDate))
                {
                    var targetValue = ScreenerEvidenceBinding.Decode(target).Value!;
                    var targetKey = Key(target, targetValue, premises);
                    // Cancel this exact logical lineage without resurrecting an older superseded revision.
                    foreach (var (ancestor, assertion) in decoded.Where(x => x.Payload.Operation == EvidenceOperation.ASSERT))
                        if (ancestor.RevisionSeriesId == target.RevisionSeriesId && ancestor.RevisionNumber <= target.RevisionNumber
                            && ancestor.Claim == target.Claim && ancestor.SubjectId == target.SubjectId
                            && ancestor.ScopeKind == target.ScopeKind && ancestor.ScopeExchangeId == target.ScopeExchangeId
                            && ancestor.EvidenceClass == target.EvidenceClass && Key(ancestor, assertion.Value!, premises) == targetKey)
                            cancelled.Add(ancestor.EvidenceId);
                    diagnostics.Add(new(r.EvidenceId, ScreenerEvidenceReasons.EvidenceCancelled));
                }
            }
            var candidates = new List<Candidate>();
            foreach (var (r, p) in decoded.Where(x => x.Payload.Operation == EvidenceOperation.ASSERT && !cancelled.Contains(x.Row.EvidenceId)))
            {
                current = r.EvidenceId;
                var quality = ScreenerEvidenceValidity.Evaluate(r.EvidenceClass!.Value, new(r.EffectiveFrom, r.EffectiveTo),
                    request.EvaluationDate, Chronology(r), request.Cutoff);
                if (quality == EvidenceQuality.Unknown) { diagnostics.Add(new(r.EvidenceId, ScreenerEvidenceReasons.EvidenceScopeMismatch)); continue; }
                if (quality == EvidenceQuality.Stale) diagnostics.Add(new(r.EvidenceId, ScreenerEvidenceReasons.EvidenceStale));
                else if (p.Completeness == EvidenceCompleteness.PARTIAL) quality = EvidenceQuality.Partial;
                else if (p.Value is CompletedSessionValue { Completion: EvidenceCompletion.UNPROVED }) quality = EvidenceQuality.Partial;
                else if (p.Value is ActionCoverageValue { Coverage: EvidenceCoverage.PARTIAL }) quality = EvidenceQuality.Partial;
                if (quality != EvidenceQuality.Stale && p.Value is PriceValue price)
                    quality = PriceQuality(r, price, premises, rawSources, request.Cutoff, diagnostics, quality);
                candidates.Add(new(r, p.Value!, quality, Key(r, p.Value!, premises), premises));
            }
            var facts = new List<ScreenerEvidenceAsOfFact>();
            foreach (var group in candidates.GroupBy(c => c.Key).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var applicable = group.Where(c => c.Quality is EvidenceQuality.Verified or EvidenceQuality.Partial or EvidenceQuality.Unknown)
                    .Where(c => new EffectiveInterval(c.Row.EffectiveFrom, c.Row.EffectiveTo).Contains(request.EvaluationDate)).ToArray();
                var resolution = ScreenerEvidenceResolver.Resolve(applicable.Select(c => new EvidenceCandidate<Candidate>(c,
                    new(c.Row.EffectiveFrom, c.Row.EffectiveTo), c.Row.AuthorityTier, Chronology(c.Row), c.Row.RevisionNumber,
                    ScreenerEvidenceBinding.Specificity(c.Row.ScopeKind!.Value), c.Row.RevisionSeriesId, c.Row.SupersedesRevisionNumber)),
                    request.EvaluationDate, request.Cutoff);
                var chosen = resolution.Selected;
                // Scope-expired facts remain diagnostic only. No value is selected from them.
                var quality = resolution.Conflicting ? EvidenceQuality.Conflicting : chosen?.Quality
                    ?? (group.Any(c => c.Quality == EvidenceQuality.Stale) ? EvidenceQuality.Stale : EvidenceQuality.Unknown);
                var retained = resolution.Candidates.Count == 0 ? group.ToArray() : resolution.Candidates.ToArray();
                if (resolution.Conflicting) diagnostics.Add(new(null, ScreenerEvidenceReasons.EvidenceConflict));
                if (quality == EvidenceQuality.Partial) diagnostics.Add(new(chosen?.Row.EvidenceId, request.Claim switch
                {
                    EvidenceClaim.Suspension or EvidenceClaim.Reopening or EvidenceClaim.TradingStatus => ScreenerEvidenceReasons.StatusCoveragePartial,
                    EvidenceClaim.CorporateAction => ScreenerEvidenceReasons.CorporateActionCoveragePartial,
                    EvidenceClaim.CompletedSession => ScreenerEvidenceReasons.SessionUnconfirmed,
                    _ => ScreenerEvidenceReasons.EvidenceUnknown
                }));
                facts.Add(new(group.Key, chosen is { Quality: EvidenceQuality.Verified or EvidenceQuality.Partial } ? chosen.Value : null,
                    quality, chosen is { Quality: EvidenceQuality.Verified or EvidenceQuality.Partial } ? Provenance(chosen.Row) : null,
                    retained.OrderBy(c => c.Row.EvidenceId.ToString("D"), StringComparer.Ordinal)
                        .Select(c => new ScreenerEvidenceAsOfCandidate(c.Value, c.Quality, Provenance(c.Row))).ToArray(),
                    chosen is { Quality: EvidenceQuality.Verified, Value: PriceValue } && rawSources is not null
                        ? new SourceReference(chosen.Row.SourceId, chosen.Row.RawArtifactId!.Value, chosen.Row.RetrievedAt!.Value,
                            chosen.Row.KnownAt, rawSources[chosen.Row.RawArtifactId.Value].ContentSha256) : null));
            }
            var overall = facts.Any(f => f.Quality == EvidenceQuality.Conflicting) ? EvidenceQuality.Conflicting
                : facts.Any(f => f.Quality == EvidenceQuality.Unknown) ? EvidenceQuality.Unknown
                : facts.Any(f => f.Quality == EvidenceQuality.Partial) ? EvidenceQuality.Partial
                : facts.Any(f => f.Quality == EvidenceQuality.Verified) ? EvidenceQuality.Verified
                : facts.Any(f => f.Quality == EvidenceQuality.Stale) ? EvidenceQuality.Stale : EvidenceQuality.Unknown;
            if (facts.Count == 0) diagnostics.Add(new(null, ScreenerEvidenceReasons.EvidenceUnknown));
            return new(request, establishedExchange, overall, facts, ScreenerEvidenceReasons.Canonical(diagnostics.Select(d => d.Reason)),
                diagnostics.Distinct().OrderBy(d => d.EvidenceId?.ToString("D"), StringComparer.Ordinal).ThenBy(d => d.Reason, StringComparer.Ordinal).ToArray(),
                premises.Values.Where(r => r.IsBound).OrderBy(r => r.EvidenceId.ToString("D"), StringComparer.Ordinal).Select(Provenance).ToArray(),
                ExaminedEvidenceIds: records.Select(r => r.EvidenceId).Concat(premises.Keys).Distinct().Order().ToArray());
        }
        catch (EvidenceBindingException e) { return Failure(request, e.Reason, current); }
    }

    private static void Require(bool condition) { if (!condition) throw new EvidenceBindingException(InputUnavailable); }
    private static ScreenerEvidencePayload Premise(Guid id, EvidenceClaim claim, ScreenerEvidenceRecord parent,
        IReadOnlyDictionary<Guid, ScreenerEvidenceRecord> premises, DateTimeOffset cutoff)
    {
        Require(premises.TryGetValue(id, out var r) && r.EvidenceId == id && r.Claim == claim
            && r.ScopeExchangeId == parent.ScopeExchangeId && ScreenerEvidenceValidity.Visible(Chronology(r), parent.KnownAt)
            && ScreenerEvidenceValidity.Visible(Chronology(r), cutoff));
        var p = ScreenerEvidenceBinding.Decode(r!);
        Require(p.Operation == EvidenceOperation.ASSERT);
        return p;
    }
    private static EvidenceQuality PriceQuality(ScreenerEvidenceRecord r, PriceValue p,
        IReadOnlyDictionary<Guid, ScreenerEvidenceRecord> premises, IReadOnlyDictionary<Guid, SourceReference>? raw,
        DateTimeOffset cutoff, List<ScreenerEvidenceAsOfDiagnostic> diagnostics, EvidenceQuality rootQuality)
    {
        var convention = Premise(p.ConventionEvidenceId, EvidenceClaim.SourcePriceConvention, r, premises, cutoff);
        var completed = Premise(p.CompletedSessionEvidenceId, EvidenceClaim.CompletedSession, r, premises, cutoff);
        var c = premises[p.ConventionEvidenceId]; var s = premises[p.CompletedSessionEvidenceId];
        Require(convention.Value is ConventionValue cv && cv.PriceSourceId == r.SourceId
            && (c.ScopeKind == ScreenerScopeKind.EXCHANGE || c.SubjectId == r.SubjectId)
            && new EffectiveInterval(c.EffectiveFrom, c.EffectiveTo).Contains(r.EffectiveFrom)
            && completed.Value is CompletedSessionValue sv && sv.SessionId == p.SessionId && s.EffectiveFrom == r.EffectiveFrom);
        Require(raw is not null && raw.TryGetValue(r.RawArtifactId!.Value, out var source)
            && source.RawArtifactId == r.RawArtifactId && source.SourceId == r.SourceId
            && source.FetchedAt == r.RetrievedAt);
        if (new[] { p.SyntheticOrCarryForward, p.Placeholder, p.Substituted }.Contains(EvidenceMarker.UNPROVED))
        { diagnostics.Add(new(r.EvidenceId, ScreenerEvidenceReasons.PriceNotAdmitted)); return EvidenceQuality.Unknown; }
        var cv2 = (ConventionValue)convention.Value!;
        var sourceAdmitted = convention.Completeness == EvidenceCompleteness.FULL
            && ScreenerSourceAdmission.Admit(c.Claim, c.AuthorityTier).Admitted
            && cv2.PriceKind is EvidencePriceKind.STOCK_RAW or EvidencePriceKind.INDEX_LEVEL && cv2.Continuity == EvidenceContinuity.RAW_AS_TRADED;
        var sessionCompleted = completed.Completeness == EvidenceCompleteness.FULL
            && ScreenerSourceAdmission.Admit(s.Claim, s.AuthorityTier).Admitted
            && ((CompletedSessionValue)completed.Value!).Completion == EvidenceCompletion.COMPLETED;
        var zero = p.ZeroVolumeSemantics == EvidenceZeroVolumeSemantics.EXPLICITLY_GENUINE && sessionCompleted && sourceAdmitted
            && (p.ZeroVolumeProof == EvidenceZeroProof.DOCUMENTED_CONVENTION ? cv2.ZeroVolumeMeaning == EvidenceZeroMeaning.GENUINE_NO_EXECUTION
                : p.ZeroVolumeProof == EvidenceZeroProof.EXPLICIT_SOURCE_FLAG && cv2.ZeroVolumeMeaning == EvidenceZeroMeaning.EXPLICIT_GENUINE_FLAG);
        var bar = new DailyBar(new(r.SubjectId), r.EffectiveFrom, p.Bar.Open.Value, p.Bar.High.Value, p.Bar.Low.Value,
            p.Bar.Close.Value, p.Bar.Volume, new SourceReference(r.SourceId, r.RawArtifactId!.Value, r.RetrievedAt!.Value,
                r.KnownAt, raw![r.RawArtifactId.Value].ContentSha256), p.Bar.AdjustedClose?.Value,
            p.Bar.VolumeUnit, p.Bar.VolumeBasis, p.Bar.MarketSegment);
        var admission = ScreenerPriceAdmission.Admit(new(bar, p.BarRevision, r.KnownAt, r.RetrievedAt, p.BarContentHash,
            sourceAdmitted, sessionCompleted, p.SyntheticOrCarryForward == EvidenceMarker.YES, p.Placeholder == EvidenceMarker.YES,
            p.Substituted == EvidenceMarker.YES, zero ? ZeroVolumeSemantics.ExplicitlyGenuine
                : p.Bar.Volume == 0 ? ZeroVolumeSemantics.Ambiguous : ZeroVolumeSemantics.NotApplicable));
        if (!admission.Admitted)
        {
            diagnostics.Add(new(r.EvidenceId, admission.Reason!));
            return convention.Completeness == EvidenceCompleteness.PARTIAL || completed.Completeness == EvidenceCompleteness.PARTIAL
                ? EvidenceQuality.Partial : EvidenceQuality.Unknown;
        }
        return rootQuality;
    }

    public static bool SameLogicalFact(ScreenerEvidenceRecord left, ScreenerEvidenceRecord right,
        IReadOnlyDictionary<Guid, ScreenerEvidenceRecord> premises)
    {
        var l = ScreenerEvidenceBinding.Decode(left); var r = ScreenerEvidenceBinding.Decode(right);
        return r.Operation == EvidenceOperation.CANCEL || l.Value is not null && r.Value is not null
            && Key(left, l.Value, premises) == Key(right, r.Value, premises);
    }

    private static string Key(ScreenerEvidenceRecord r, ScreenerEvidenceValue value, IReadOnlyDictionary<Guid, ScreenerEvidenceRecord> premises)
    {
        var extra = value switch
        {
            RuleValue v => new[] { v.RuleId }, ExceptionValue v => [v.ExceptionId], BoardChangeValue v => [v.ChangeId],
            SuspensionValue v => [v.NoticeId], ReopeningValue v => [v.ReopeningId],
            ScheduledSessionValue v => [v.SessionId], CompletedSessionValue v => [v.SessionId], TradingStatusValue v => [v.SessionId],
            ActionEventValue v => ["EVENT", v.EventId], ActionCoverageValue v => ["COVERAGE", v.CoverageId],
            ConventionValue v => [v.PriceSourceId, v.Endpoint, v.Field, v.Version],
            PriceValue v when premises.TryGetValue(v.ConventionEvidenceId, out var c) && ScreenerEvidenceBinding.Decode(c).Value is ConventionValue cv
                => [cv.PriceSourceId, cv.Endpoint, cv.Field, cv.Version, v.SessionId],
            _ => Array.Empty<string>()
        };
        return JsonSerializer.Serialize(new[] { r.Claim.ToString() }.Concat(extra));
    }

    private sealed class Candidate(ScreenerEvidenceRecord row, ScreenerEvidenceValue value, EvidenceQuality quality,
        string key, IReadOnlyDictionary<Guid, ScreenerEvidenceRecord> premises) : IEquatable<Candidate>
    {
        public ScreenerEvidenceRecord Row { get; } = row;
        public ScreenerEvidenceValue Value { get; } = value;
        public EvidenceQuality Quality { get; } = quality;
        public string Key { get; } = key;
        public bool Equals(Candidate? other) => other is not null && Row.ScopeExchangeId == other.Row.ScopeExchangeId
            && ScreenerEvidenceEquality.FactualValueEquals(Row, other.Row, premises);
        public override bool Equals(object? obj) => obj is Candidate other && Equals(other);
        // ponytail: equality scans at most 512 records; add projection hashes only if that ceiling grows.
        public override int GetHashCode() => 0;
    }

    public static TradingStatusResult TradingStatus(ScreenerEvidenceAsOfResult suspension, ScreenerEvidenceAsOfResult reopening,
        ScreenerEvidenceAsOfResult status)
    {
        if (suspension.Request.Claim != EvidenceClaim.Suspension || reopening.Request.Claim != EvidenceClaim.Reopening
            || status.Request.Claim != EvidenceClaim.TradingStatus || new[] { reopening, status }.Any(r => r.Request.SubjectId != suspension.Request.SubjectId
                || r.Request.ScopeKind != suspension.Request.ScopeKind || r.Request.EvaluationDate != suspension.Request.EvaluationDate
                || r.Request.Cutoff != suspension.Request.Cutoff))
            throw new ArgumentException("Status facts require one subject, date and cutoff.");
        if (new[] { suspension, reopening, status }.Any(r => r.FailureReason is not null))
            return new(Application.TradingStatus.Unknown, EvidenceQuality.Unknown, [InputUnavailable]);
        var facts = new[] { suspension, reopening, status }.SelectMany(r => r.Facts)
            .SelectMany(f => f.Selected is not null ? new[] { new ScreenerEvidenceAsOfCandidate(f.Value!, f.Quality, f.Selected) }
                : f.Quality == EvidenceQuality.Conflicting ? f.Candidates.Select(c => c with { Quality = EvidenceQuality.Conflicting }).ToArray()
                : f.Quality == EvidenceQuality.Stale ? f.Candidates.Where(c => c.Value is SuspensionValue
                    && c.Provenance.EvidenceClass == EvidenceClass.ContinuingState).ToArray() : []).ToArray();
        if (facts.Length == 0) return ScreenerTradingStatusEvaluator.Evaluate([], suspension.Request.EvaluationDate, suspension.Request.Cutoff);
        // Expired suspension explains absence of a confirmed reopening; it cannot override an applicable session status.
        if (facts.Any(f => f.Quality != EvidenceQuality.Stale)) facts = facts.Where(f => f.Quality != EvidenceQuality.Stale).ToArray();
        var tier = facts.Min(f => f.Provenance.Authority);
        var specificity = facts.Where(f => f.Provenance.Authority == tier).Max(f => ScreenerEvidenceBinding.Specificity(f.Provenance.ScopeKind));
        var preferred = facts.Where(f => f.Provenance.Authority == tier && ScreenerEvidenceBinding.Specificity(f.Provenance.ScopeKind) == specificity).ToArray();
        var latest = preferred.Max(f => f.Provenance.Scope.From);
        if (preferred.Any(f => f.Provenance.Scope.From == latest && f.Quality == EvidenceQuality.Conflicting))
            return new(Application.TradingStatus.Unknown, EvidenceQuality.Conflicting, [ScreenerEvidenceReasons.StatusConflict]);
        var values = preferred
            .Select(f => new TradingStatusEvidence(f.Quality == EvidenceQuality.Stale ? Application.TradingStatus.ReopeningUnconfirmed
                : f.Value is SuspensionValue or TradingStatusValue { Status: EvidenceStatus.SUSPENDED } ? Application.TradingStatus.Suspended
                : Application.TradingStatus.Trading, f.Quality, f.Provenance.Authority, f.Provenance.SourceId, f.Provenance.Chronology,
                f.Quality == EvidenceQuality.Stale ? new(f.Provenance.Scope.From) : f.Provenance.Scope));
        return ScreenerTradingStatusEvaluator.Evaluate(values, suspension.Request.EvaluationDate, suspension.Request.Cutoff);
    }
}
