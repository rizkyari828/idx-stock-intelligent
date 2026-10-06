using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

internal static class AsOfFixture
{
    internal static ScreenerEvidenceRecord Row(EvidenceClaim claim, ScreenerEvidenceValue value, long revision = 1,
        string? series = null, SourceAuthorityTier tier = SourceAuthorityTier.T1Governing, DateTimeOffset? known = null,
        DateOnly? from = null, DateOnly? to = null, EvidenceClass? cls = null,
        ScreenerScopeKind? kind = null, string source = "source-a", EvidenceCompleteness completeness = EvidenceCompleteness.FULL)
    {
        var date = from ?? EvidenceBindingFixture.Day;
        var scope = kind ?? EvidenceBindingFixture.Scope(claim);
        var evidenceClass = cls ?? EvidenceBindingFixture.Classes(claim)[0];
        var envelope = new ScreenerEvidenceRecord(Guid.NewGuid(), scope == ScreenerScopeKind.EXCHANGE ? EvidenceBindingFixture.Exchange : EvidenceBindingFixture.Instrument,
            claim, ScreenerEvidenceV02.PolicyId, 1, ScreenerEvidenceRevisionSeries.Canonical(source, series ?? "asof-" + claim), revision,
            revision > 1 ? revision - 1 : null, tier, date,
            evidenceClass is EvidenceClass.PointObservation or EvidenceClass.SessionFact ? to ?? date : to,
            null, EvidenceBindingFixture.Known.AddHours(-2), EvidenceBindingFixture.Known.AddHours(-1), known ?? EvidenceBindingFixture.Known,
            source, "retained", claim == EvidenceClaim.GenuinePriceObservation ? EvidenceBindingFixture.Reference : null, "{}");
        return ScreenerEvidenceBinding.Bind(envelope, evidenceClass, scope, EvidenceBindingFixture.Exchange,
            new(EvidenceOperation.ASSERT, completeness, value));
    }
    internal static ScreenerEvidenceAsOfRequest Request(EvidenceClaim claim, DateTimeOffset? cutoff = null,
        DateOnly? date = null, ScreenerScopeKind scope = ScreenerScopeKind.INSTRUMENT) =>
        new(scope == ScreenerScopeKind.EXCHANGE ? EvidenceBindingFixture.Exchange : EvidenceBindingFixture.Instrument,
            claim, date ?? EvidenceBindingFixture.Day, cutoff ?? EvidenceBindingFixture.Known, scope);
    internal static ScreenerEvidenceRecord Cancel(ScreenerEvidenceRecord original, DateTimeOffset known) =>
        new(Guid.NewGuid(), original.SubjectId, original.Claim, original.PolicyId, original.SchemaVersion,
            original.RevisionSeriesId, original.RevisionNumber + 1, original.RevisionNumber, original.AuthorityTier,
            original.EffectiveFrom, original.EffectiveTo, null, original.PublishedAt, original.RetrievedAt, known,
            original.SourceId, original.SourceReference, original.RawArtifactId,
            ScreenerEvidenceBinding.Encode(original.Claim, new(EvidenceOperation.CANCEL, EvidenceCompleteness.FULL, null)),
            original.EvidenceClass, 1, original.ScopeKind, original.ScopeExchangeId);
    internal static ScreenerEvidenceRecord Physical(ScreenerEvidenceRecord row, string json, int? version) =>
        new(row.EvidenceId, row.SubjectId, row.Claim, row.PolicyId, row.SchemaVersion, row.RevisionSeriesId,
            row.RevisionNumber, row.SupersedesRevisionNumber, row.AuthorityTier, row.EffectiveFrom, row.EffectiveTo,
            row.EffectiveAt, row.PublishedAt, row.RetrievedAt, row.KnownAt, row.SourceId, row.SourceReference,
            row.RawArtifactId, json, version is null ? null : row.EvidenceClass, version,
            version is null ? null : row.ScopeKind, version is null ? null : row.ScopeExchangeId);
}

public sealed class ScreenerEvidenceV02AsOfTests
{
    [Fact]
    public void MissingAndUnboundAreUnknownWithoutInventingValues()
    {
        var request = AsOfFixture.Request(EvidenceClaim.StableIdentity);
        Assert.Equal(EvidenceQuality.Unknown, ScreenerEvidenceAsOf.Resolve(request, []).Quality);
        var unbound = EvidenceBindingFixture.Envelope(EvidenceClaim.StableIdentity);
        var result = ScreenerEvidenceAsOf.Resolve(request, [unbound]);
        Assert.Equal(EvidenceQuality.Unknown, result.Quality); Assert.Empty(result.Facts);
        Assert.Contains("PERSISTED_EVIDENCE_UNBOUND", result.Reasons);
    }

    [Theory]
    [InlineData("known")]
    [InlineData("published")]
    [InlineData("recorded")]
    public void ChronologyNeverBackdatesKnowledgeOrUsesRecordingAsKnowledge(string clock)
    {
        var old = AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("OLD"), from: EvidenceBindingFixture.Day.AddYears(-1));
        var later = AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("NEW"), revision: 2,
            from: old.EffectiveFrom, known: clock == "known" ? EvidenceBindingFixture.Known.AddDays(1) : null);
        if (clock == "published") later = new(later.EvidenceId, later.SubjectId, later.Claim, later.PolicyId, 1,
            later.RevisionSeriesId, 2, 1, later.AuthorityTier, later.EffectiveFrom, null, null,
            EvidenceBindingFixture.Known.AddDays(1), later.RetrievedAt, later.KnownAt, later.SourceId, later.SourceReference,
            null, later.Payload, later.EvidenceClass, 1, later.ScopeKind, later.ScopeExchangeId);
        if (clock == "recorded") old = old with { RecordedAt = EvidenceBindingFixture.Known.AddYears(1) };
        var result = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(EvidenceClaim.StableIdentity), clock == "recorded" ? [old] : [old, later]);
        Assert.Equal("OLD", Assert.IsType<IdentityValue>(Assert.Single(result.Facts).Value).SourceCode);
    }

    [Fact]
    public void RevisionCancellationAndIndependentSeriesReplayExactly()
    {
        var a = AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("A"), revision: 10);
        var b = AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("B"), revision: 11, known: EvidenceBindingFixture.Known.AddDays(1));
        Assert.Equal(a.EvidenceId, Assert.Single(ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(a.Claim), [a, b]).Facts).Selected!.EvidenceId);
        Assert.Equal(b.EvidenceId, Assert.Single(ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(a.Claim, b.KnownAt), [a, b]).Facts).Selected!.EvidenceId);
        var cancel = AsOfFixture.Cancel(a, b.KnownAt);
        var unrelated = AsOfFixture.Row(a.Claim, new IdentityValue("OTHER"), series: "unrelated");
        var before = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(a.Claim), [a, cancel]);
        Assert.Equal(EvidenceQuality.Verified, before.Quality);
        var after = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(a.Claim, cancel.KnownAt), [a, cancel]);
        Assert.Equal(EvidenceQuality.Unknown, after.Quality); Assert.Contains(ScreenerEvidenceReasons.EvidenceCancelled, after.Reasons);
        Assert.Equal(unrelated.EvidenceId, Assert.Single(ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(a.Claim, cancel.KnownAt), [a, unrelated, cancel]).Facts).Selected!.EvidenceId);
        Assert.Equal(ScreenerEvidenceAsOf.InputUnavailable, ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(a.Claim, cancel.KnownAt), [cancel]).FailureReason);
    }

    [Fact]
    public void AuthoritySpecificityAndTypedEqualityDoNotUseRecency()
    {
        var main = AsOfFixture.Row(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.MAIN), series: "main");
        var lower = AsOfFixture.Row(main.Claim, new BoardValue(EvidenceBoard.DEVELOPMENT), series: "lower", tier: SourceAuthorityTier.T2AdmittedReference,
            known: main.KnownAt.AddDays(1));
        var request = AsOfFixture.Request(main.Claim, lower.KnownAt);
        Assert.Equal(main.EvidenceId, Assert.Single(ScreenerEvidenceAsOf.Resolve(request, [main, lower]).Facts).Selected!.EvidenceId);
        var equalTier = AsOfFixture.Row(main.Claim, new BoardValue(EvidenceBoard.DEVELOPMENT), series: "other");
        Assert.Equal(EvidenceQuality.Conflicting, ScreenerEvidenceAsOf.Resolve(request, [main, equalTier]).Quality);
        var partial = AsOfFixture.Row(main.Claim, new BoardValue(EvidenceBoard.DEVELOPMENT), series: "partial", completeness: EvidenceCompleteness.PARTIAL);
        Assert.Equal(EvidenceQuality.Conflicting, ScreenerEvidenceAsOf.Resolve(request, [main, partial]).Quality);
        var point = AsOfFixture.Row(main.Claim, new BoardValue(EvidenceBoard.MAIN), series: "point", cls: EvidenceClass.PointObservation);
        Assert.Equal(EvidenceQuality.Verified, ScreenerEvidenceAsOf.Resolve(request, [main, point]).Quality);
        var instrument = AsOfFixture.Row(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.TRADING, "regular"));
        var exchange = AsOfFixture.Row(instrument.Claim, new TradingStatusValue(EvidenceStatus.SUSPENDED, "regular"), kind: ScreenerScopeKind.EXCHANGE, series: "exchange");
        Assert.Equal(instrument.EvidenceId, Assert.Single(ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(instrument.Claim),
            [instrument, exchange], establishedExchange: EvidenceBindingFixture.Exchange).Facts).Selected!.EvidenceId);
        Assert.Empty(ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(instrument.Claim), [exchange]).Facts);
    }

    [Theory]
    [InlineData(EvidenceClass.ContinuingState, 1, EvidenceQuality.Unknown)]
    [InlineData(EvidenceClass.ContinuingState, -2, EvidenceQuality.Stale)]
    [InlineData(EvidenceClass.PointObservation, -2, EvidenceQuality.Stale)]
    [InlineData(EvidenceClass.PointObservation, 1, EvidenceQuality.Unknown)]
    public void ValidityPrecedesCompleteness(EvidenceClass cls, int fromOffset, EvidenceQuality expected)
    {
        var r = AsOfFixture.Row(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.MAIN), cls: cls,
            from: EvidenceBindingFixture.Day.AddDays(fromOffset), to: EvidenceBindingFixture.Day.AddDays(fromOffset), completeness: EvidenceCompleteness.PARTIAL);
        var result = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(r.Claim), [r]);
        Assert.Equal(expected, result.Quality); Assert.DoesNotContain(result.Facts, f => f.Value is not null);
    }

    [Theory]
    [InlineData(1, "{}", "PERSISTED_EVIDENCE_MALFORMED")]
    [InlineData(2, "{}", "PERSISTED_EVIDENCE_BINDING_UNSUPPORTED")]
    public void MalformedOrUnsupportedCandidateNeverFallsBack(int version, string payload, string reason)
    {
        var good = AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("GOOD"));
        var bad = AsOfFixture.Physical(AsOfFixture.Row(good.Claim, new IdentityValue("BAD"), revision: 2), payload, version);
        var result = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(good.Claim), [good, bad]);
        Assert.Equal(reason, result.FailureReason); Assert.Empty(result.Facts);
    }

    [Fact]
    public void IndependentEventsAreASetAndBoundsNeverTruncate()
    {
        var a = AsOfFixture.Row(EvidenceClaim.CorporateAction, new ActionEventValue(EvidenceActionValueKind.EVENT, "split",
            EvidenceActionType.SPLIT, EvidenceBindingFixture.Day, null, null), series: "split");
        var b = AsOfFixture.Row(a.Claim, new ActionEventValue(EvidenceActionValueKind.EVENT, "bonus",
            EvidenceActionType.BONUS_OR_SHARE_DISTRIBUTION, EvidenceBindingFixture.Day, null, null), series: "bonus");
        Assert.Equal(2, ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(a.Claim), [a, b]).Facts.Count);
        var many = Enumerable.Range(0, ScreenerEvidenceAsOf.MaximumRecords + 1)
            .Select(i => AsOfFixture.Row(EvidenceClaim.StableIdentity, new IdentityValue("TEST"), series: "row-" + i)).ToArray();
        Assert.Equal(EvidenceQuality.Verified, ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(EvidenceClaim.StableIdentity), many[..^1]).Quality);
        Assert.Equal(ScreenerEvidenceAsOf.BoundExceeded, ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(EvidenceClaim.StableIdentity), many).FailureReason);
    }

    [Fact]
    public void PartialAndUnprovedAssertionsNeverBecomeVerified()
    {
        var partial = AsOfFixture.Row(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.MAIN), completeness: EvidenceCompleteness.PARTIAL);
        Assert.Equal(EvidenceQuality.Partial, ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(partial.Claim), [partial]).Quality);
        var unproved = AsOfFixture.Row(EvidenceClaim.CompletedSession, new CompletedSessionValue("regular", EvidenceCompletion.UNPROVED, null));
        Assert.Equal(EvidenceQuality.Partial, ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(unproved.Claim,
            scope: ScreenerScopeKind.EXCHANGE), [unproved]).Quality);
    }

    [Fact]
    public void StatusAuthorityAndExpiryPrecedeRecencyAndLowerTierConflicts()
    {
        var suspended = AsOfFixture.Row(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "high"));
        var lowTrading = AsOfFixture.Row(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.TRADING, "regular"),
            tier: SourceAuthorityTier.T2AdmittedReference, known: suspended.KnownAt.AddDays(1));
        var lowSuspended = AsOfFixture.Row(lowTrading.Claim, new TradingStatusValue(EvidenceStatus.SUSPENDED, "regular"),
            tier: SourceAuthorityTier.T2AdmittedReference, series: "other", known: lowTrading.KnownAt);
        var s = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(suspended.Claim, lowTrading.KnownAt), [suspended]);
        var r = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(EvidenceClaim.Reopening, lowTrading.KnownAt), []);
        var t = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(lowTrading.Claim, lowTrading.KnownAt), [lowTrading, lowSuspended]);
        Assert.Equal(EvidenceQuality.Conflicting, t.Quality);
        Assert.Equal(TradingStatus.Suspended, ScreenerEvidenceAsOf.TradingStatus(s, r, t).Status);
        var expired = AsOfFixture.Row(suspended.Claim, new SuspensionValue(EvidenceStatus.SUSPENDED, "expired"),
            from: EvidenceBindingFixture.Day.AddDays(-1), to: EvidenceBindingFixture.Day.AddDays(-1));
        s = ScreenerEvidenceAsOf.Resolve(s.Request, [expired]);
        t = ScreenerEvidenceAsOf.Resolve(t.Request, [lowTrading]);
        Assert.Equal(TradingStatus.Trading, ScreenerEvidenceAsOf.TradingStatus(s, r, t).Status);
    }

    [Fact]
    public void StatusTransitionsUseRetainedFactsAndNeverSessionsOrPrices()
    {
        var suspended = AsOfFixture.Row(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice"));
        var reopen = AsOfFixture.Row(EvidenceClaim.Reopening, new ReopeningValue("reopen", EvidenceStatus.TRADING,
            EvidenceBindingFixture.Day.AddDays(1), suspended.EvidenceId), from: EvidenceBindingFixture.Day.AddDays(1), known: suspended.KnownAt.AddDays(2));
        TradingStatusResult At(DateTimeOffset cutoff)
        {
            var s = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(suspended.Claim, cutoff, reopen.EffectiveFrom), [suspended]);
            var r = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(reopen.Claim, cutoff, reopen.EffectiveFrom), [reopen],
                new[] { suspended, reopen }.ToDictionary(x => x.EvidenceId));
            var t = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(EvidenceClaim.TradingStatus, cutoff, reopen.EffectiveFrom), []);
            return ScreenerEvidenceAsOf.TradingStatus(s, r, t);
        }
        Assert.Equal(TradingStatus.Suspended, At(suspended.KnownAt).Status);
        Assert.Equal(TradingStatus.Trading, At(reopen.KnownAt).Status);
        var s0 = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(EvidenceClaim.Suspension), []);
        var r0 = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(EvidenceClaim.Reopening), []);
        var t0 = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(EvidenceClaim.TradingStatus), []);
        Assert.Equal(TradingStatus.Unknown, ScreenerEvidenceAsOf.TradingStatus(s0, r0, t0).Status);
        var bounded = AsOfFixture.Row(suspended.Claim, new SuspensionValue(EvidenceStatus.SUSPENDED, "bounded"), to: EvidenceBindingFixture.Day);
        var expired = ScreenerEvidenceAsOf.Resolve(AsOfFixture.Request(bounded.Claim, date: EvidenceBindingFixture.Day.AddDays(1)), [bounded]);
        Assert.Equal(TradingStatus.ReopeningUnconfirmed, ScreenerEvidenceAsOf.TradingStatus(expired,
            r0 with { Request = r0.Request with { EvaluationDate = expired.Request.EvaluationDate } },
            t0 with { Request = t0.Request with { EvaluationDate = expired.Request.EvaluationDate } }).Status);
    }
}
