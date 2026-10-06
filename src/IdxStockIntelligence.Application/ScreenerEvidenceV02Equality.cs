namespace IdxStockIntelligence.Application;

public static class ScreenerEvidenceEquality
{
    // Premises are exact retained identities supplied by the caller; never latest-record lookups.
    public static bool FactualEquals(ScreenerEvidenceRecord left, ScreenerEvidenceRecord right,
        IReadOnlyDictionary<Guid, ScreenerEvidenceRecord>? premises = null)
    {
        var a = ScreenerEvidenceBinding.Decode(left);
        var b = ScreenerEvidenceBinding.Decode(right);
        if (left.Claim != right.Claim || left.EvidenceClass != right.EvidenceClass || left.SubjectId != right.SubjectId || left.ScopeKind != right.ScopeKind
            || left.ScopeExchangeId != right.ScopeExchangeId || a.Operation != b.Operation) return false;
        if (a.Operation == EvidenceOperation.CANCEL)
            return left.RevisionSeriesId == right.RevisionSeriesId && left.SupersedesRevisionNumber == right.SupersedesRevisionNumber;
        if (left.EvidenceClass == EvidenceClass.SessionFact || right.EvidenceClass == EvidenceClass.SessionFact
            || a.Value is ActionCoverageValue || b.Value is ActionCoverageValue)
            if (left.EffectiveFrom != right.EffectiveFrom || left.EffectiveTo != right.EffectiveTo) return false;
        return ValuesEqual(a.Value!, b.Value!, left, right, premises);
    }

    // The reader evaluates class validity first, then compares these same factual projections.
    public static bool FactualValueEquals(ScreenerEvidenceRecord left, ScreenerEvidenceRecord right,
        IReadOnlyDictionary<Guid, ScreenerEvidenceRecord>? premises = null)
    {
        var a = ScreenerEvidenceBinding.Decode(left);
        var b = ScreenerEvidenceBinding.Decode(right);
        return left.Claim == right.Claim && a.Operation == EvidenceOperation.ASSERT && b.Operation == EvidenceOperation.ASSERT
            && ValuesEqual(a.Value!, b.Value!, left, right, premises);
    }

    private static bool ValuesEqual(ScreenerEvidenceValue a, ScreenerEvidenceValue b, ScreenerEvidenceRecord left,
        ScreenerEvidenceRecord right, IReadOnlyDictionary<Guid, ScreenerEvidenceRecord>? premises) => (a, b) switch
    {
        (SuspensionValue x, SuspensionValue y) => x.Status == y.Status,
        (ConventionValue x, ConventionValue y) => x with { DocumentationReference = "" } == y with { DocumentationReference = "" },
        (ActionEventValue x, ActionEventValue y) => x with { TermsReference = null } == y with { TermsReference = null },
        (ActionCoverageValue x, ActionCoverageValue y) => x.CoverageId == y.CoverageId && x.Coverage == y.Coverage
            && Events(x.EventEvidenceIds, premises).SequenceEqual(Events(y.EventEvidenceIds, premises)),
        (PriceValue x, PriceValue y) => left.SourceId == right.SourceId && x.SessionId == y.SessionId && x.Bar == y.Bar
            && x.BarRevision == y.BarRevision && x.SyntheticOrCarryForward == y.SyntheticOrCarryForward
            && x.Placeholder == y.Placeholder && x.Substituted == y.Substituted && x.ZeroVolumeSemantics == y.ZeroVolumeSemantics
            && x.ZeroVolumeProof == y.ZeroVolumeProof
            && FactualEquals(Premise(x.ConventionEvidenceId, premises, EvidenceClaim.SourcePriceConvention), Premise(y.ConventionEvidenceId, premises, EvidenceClaim.SourcePriceConvention), premises)
            && FactualEquals(Premise(x.CompletedSessionEvidenceId, premises, EvidenceClaim.CompletedSession), Premise(y.CompletedSessionEvidenceId, premises, EvidenceClaim.CompletedSession), premises),
        _ => a.Equals(b)
    };
    private static IEnumerable<ActionEventValue> Events(IReadOnlyList<Guid> ids, IReadOnlyDictionary<Guid, ScreenerEvidenceRecord>? premises) =>
        ids.Select(id => ScreenerEvidenceBinding.Decode(Premise(id, premises, EvidenceClaim.CorporateAction)).Value is ActionEventValue value
            ? value with { TermsReference = null } : throw new EvidenceBindingException("PERSISTED_EVIDENCE_INPUT_UNAVAILABLE"))
        .Distinct().OrderBy(e => e.EventId, StringComparer.Ordinal).ThenBy(e => e.EffectiveDate).ThenBy(e => e.ActionType)
        .ThenBy(e => e.Ratio?.NewUnits.Value).ThenBy(e => e.Ratio?.OldUnits.Value);
    private static ScreenerEvidenceRecord Premise(Guid id, IReadOnlyDictionary<Guid, ScreenerEvidenceRecord>? premises, EvidenceClaim claim) =>
        premises is not null && premises.TryGetValue(id, out var value) && value.EvidenceId == id && value.Claim == claim
            && ScreenerEvidenceBinding.Decode(value).Operation == EvidenceOperation.ASSERT ? value
            : throw new EvidenceBindingException("PERSISTED_EVIDENCE_INPUT_UNAVAILABLE");
}
