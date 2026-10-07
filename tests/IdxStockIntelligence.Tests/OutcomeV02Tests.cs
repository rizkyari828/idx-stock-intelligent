using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

internal sealed class OutcomeV02Fixture
{
    internal List<ScreenerEvidenceRecord> Rows { get; } = [];
    internal ScreenerReadinessRequest Request { get; }
    internal OutcomeV02Evidence Evidence => new(Rows.ToArray(), [Artifact]);
    internal OutcomeV02Evidence ForwardEvidence => new(Rows.Where(r => r.EffectiveTo is null || r.EffectiveTo >= Request.EvaluationDate).ToArray(), [Artifact]);
    internal OutcomeV02Artifact Artifact { get; }
    internal DateTimeOffset Known { get; }
    internal OutcomeV02Fixture(bool candidate = true, bool zero = false, DateOnly? date = null, DateTimeOffset? known = null, string? hash = null)
    {
        var f = ScreenerEvidenceV02CandidateTests.Boundary(candidate ? 161.01m : 150m, zero: zero);
        var day = date ?? new DateOnly(2026, 10, 7); var shift = day.DayNumber - f.Request.EvaluationDate.DayNumber;
        var oldDates = f.Evidence.Dates; var newDates = new List<DateOnly> { day };
        for (var d = day.AddDays(-1); newDates.Count < oldDates.Length; d = d.AddDays(-1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) newDates.Add(d);
        newDates.Reverse(); var dates = oldDates.Zip(newDates).ToDictionary(x => x.First, x => x.Second);
        DateOnly Map(DateOnly d) => dates.GetValueOrDefault(d, d.AddDays(shift));
        Known = (known ?? new(day.ToDateTime(new TimeOnly(20, 0)), TimeSpan.FromHours(7))).ToUniversalTime();
        Request = f.Request with { EvaluationDate = day, HistoryFrom = Map(f.Request.HistoryFrom), Cutoff = Known };
        Artifact = new(EvidenceBindingFixture.Reference, "source-a", hash ?? new string('a', 64), 13, Known.AddHours(-1));
        foreach (var row in f.Evidence.Rows)
        {
            var payload = ScreenerEvidenceBinding.Decode(row); var start = Map(row.EffectiveFrom); var end = row.EffectiveTo is { } to ? Map(to) : (DateOnly?)null;
            var value = payload.Value switch
            {
                CompletedSessionValue c => c with { CompletedAt = start == day ? Known.AddMinutes(-1) : new DateTimeOffset(start.ToDateTime(new TimeOnly(19, 0)), TimeSpan.FromHours(7)).ToUniversalTime() },
                ListingValue l => l with { ListingDate = Map(l.ListingDate) },
                _ => payload.Value
            };
            var envelope = new ScreenerEvidenceRecord(row.EvidenceId, row.SubjectId, row.Claim, row.PolicyId, row.SchemaVersion,
                row.RevisionSeriesId, row.RevisionNumber, row.SupersedesRevisionNumber, row.AuthorityTier, start, end, null,
                null, Artifact.RetrievedAt, Known, row.SourceId, row.SourceReference, row.RawArtifactId, "{}");
            Rows.Add(ScreenerEvidenceBinding.Bind(envelope, row.EvidenceClass!.Value, row.ScopeKind!.Value, row.ScopeExchangeId!.Value, payload with { Value = value }));
        }
    }
    private ScreenerTechnicalCapture? capture;
    internal ScreenerTechnicalCapture Capture()
    {
        if (capture is not null) return capture;
        var dates = Rows.Where(r => r.Claim == EvidenceClaim.GenuinePriceObservation).Select(r => r.EffectiveFrom).Order().ToArray();
        var reads = new List<ScreenerEvidenceAsOfResult>(); var conventions = Rows.Where(r => r.Claim == EvidenceClaim.SourcePriceConvention)
            .ToDictionary(r => r.EvidenceId, r => (ConventionValue)ScreenerEvidenceBinding.Decode(r).Value!);
        foreach (var date in Request.Dates)
            foreach (var claim in ScreenerEvidenceReadiness.HistoryClaims.Concat(ScreenerEvidenceReadiness.MarketClaims).Distinct())
                reads.Add(Evidence.Read(Request.SubjectId, EvidenceBindingFixture.Exchange, Request.SessionId, claim, date, Known));
        var history = dates.Select(d => ScreenerEvidenceReadiness.Compose(Request with { EvaluationDate = d }, reads.Where(r => r.Request.EvaluationDate <= d).ToArray(), conventions)).ToArray();
        return capture = ScreenerEvidenceTechnicalCapture.Create(history[^1], history) with { RecordedAt = Known.AddSeconds(1) };
    }
    internal (ScreenerTechnicalCandidateDecision Candidate, ScreenerTechnicalCapture Capture) Source()
    { var capture = Capture(); return (ScreenerEvidenceTechnicalCandidates.Promote(capture) with { RecordedAt = Known.AddSeconds(2) }, capture); }
    internal OutcomeV02Enrollment Enrollment()
    { var s = Source(); return OutcomeV02.Enroll(s.Candidate, s.Capture, Evidence, Known.AddSeconds(3), Known.AddSeconds(4)); }
    internal void Add(EvidenceClaim claim, ScreenerEvidenceValue value, DateOnly from, DateOnly? to = null, DateTimeOffset? known = null)
    { Rows.Add(Row(claim, value, from, to, known ?? Known)); }
    private ScreenerEvidenceRecord Row(EvidenceClaim claim, ScreenerEvidenceValue value, DateOnly from, DateOnly? to, DateTimeOffset known)
    {
        known = known.ToUniversalTime();
        var template = AsOfFixture.Row(claim, value, from: from, to: to, series: "outcome-" + Guid.NewGuid().ToString("N"),
            known: known > EvidenceBindingFixture.Known ? known : EvidenceBindingFixture.Known);
        var envelope = new ScreenerEvidenceRecord(template.EvidenceId, template.SubjectId, claim, template.PolicyId, 1, template.RevisionSeriesId, 1, null,
            template.AuthorityTier, from, template.EffectiveTo, null, null, Artifact.RetrievedAt, known,
            template.SourceId, template.SourceReference, template.RawArtifactId, "{}");
        return ScreenerEvidenceBinding.Bind(envelope, template.EvidenceClass!.Value, template.ScopeKind!.Value, EvidenceBindingFixture.Exchange,
            new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, value));
    }
    internal void Future(int sessions, bool endpoint = true, bool coverage = true, bool status = true)
    {
        var day = Request.EvaluationDate; var count = 0;
        while (count < sessions)
        {
            day = day.AddDays(1); if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            var completed = new DateTimeOffset(day.ToDateTime(new TimeOnly(19, 0)), TimeSpan.FromHours(7)).ToUniversalTime();
            var row = Row(EvidenceClaim.CompletedSession, new CompletedSessionValue(Request.SessionId, EvidenceCompletion.COMPLETED, completed),
                day, null, completed.AddMinutes(1)); Rows.Add(row);
            if (status) Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.TRADING, Request.SessionId), day, known: completed.AddMinutes(1));
            count++;
            if (endpoint)
            {
                var anchor = (PriceValue)ScreenerEvidenceBinding.Decode(Rows.First(r => r.Claim == EvidenceClaim.GenuinePriceObservation)).Value!;
                var bar = anchor.Bar with { Open = new(165m), High = new(172m), Low = new(164m), Close = new(170m), Volume = 200 };
                var price = anchor with { Bar = bar, BarContentHash = ScreenerEvidenceBinding.BarHash(bar), CompletedSessionEvidenceId = row.EvidenceId,
                    ZeroVolumeSemantics = EvidenceZeroVolumeSemantics.NOT_APPLICABLE, ZeroVolumeProof = EvidenceZeroProof.NONE };
                var original = Row(EvidenceClaim.GenuinePriceObservation, price, day, null, completed.AddMinutes(2));
                // All fixture prices share one exact raw archive; retain its retrieval boundary.
                var envelope = new ScreenerEvidenceRecord(original.EvidenceId, original.SubjectId, original.Claim, original.PolicyId, 1, original.RevisionSeriesId, 1, null,
                    original.AuthorityTier, day, day, null, null, Artifact.RetrievedAt, original.KnownAt, original.SourceId, original.SourceReference, Artifact.ArtifactId, "{}");
                Rows.Add(ScreenerEvidenceBinding.Bind(envelope, EvidenceClass.SessionFact, ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange,
                    new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, price)));
            }
        }
        if (coverage) Add(EvidenceClaim.CorporateAction, new ActionCoverageValue(EvidenceActionValueKind.COVERAGE, "forward", EvidenceCoverage.COMPLETE, []), Request.EvaluationDate, day,
            new DateTimeOffset(day.ToDateTime(new TimeOnly(20, 0)), TimeSpan.FromHours(7)));
    }
    internal DateTimeOffset After(int days = 40) => new DateTimeOffset(Request.EvaluationDate.AddDays(days).ToDateTime(new TimeOnly(21, 0)), TimeSpan.FromHours(7)).ToUniversalTime();
}

public sealed class OutcomeV02Tests
{
    [Fact]
    public void ExactSourceEnrolsWithoutExecutingTechnicalPolicyAgain()
    {
        var f = new OutcomeV02Fixture(); var e = f.Enrollment(); var source = f.Source();
        Assert.Equal(source.Candidate.DecisionId, e.Projection.CandidateDecisionId); Assert.Equal(source.Capture.CaptureId, e.Projection.CaptureId);
        Assert.Equal(161.01m, e.Projection.Anchor.Price.Bar.Close.Value); Assert.Equal(f.Request.Cutoff, e.Projection.OriginalCutoff);
        Assert.Null(e.Projection.AnchorReason); OutcomeV02.Validate(e);
        Assert.Equal(e.BindingHash, f.Enrollment().BindingHash);
    }
    [Theory]
    [InlineData(-1, true)] [InlineData(0, false)] [InlineData(1, false)]
    public void MidnightBoundaryIsStrict(long ticks, bool allowed)
    {
        var f = new OutcomeV02Fixture(); var s = f.Source(); var at = OutcomeV02.Deadline(f.Request.EvaluationDate).AddTicks(ticks);
        if (allowed) Assert.NotNull(OutcomeV02.Enroll(s.Candidate, s.Capture, f.Evidence, at, at));
        else Assert.Equal("OUTCOME_ENROLLMENT_DEADLINE_EXCEEDED", Assert.Throws<EvidenceBindingException>(() => OutcomeV02.Enroll(s.Candidate, s.Capture, f.Evidence, at, at)).Reason);
    }
    [Fact]
    public void WeekendDoesNotExtendEnrollment()
    {
        var f = new OutcomeV02Fixture(date: new(2026, 10, 9)); var s = f.Source();
        Assert.Equal(DayOfWeek.Saturday, OutcomeEvaluator.Through(OutcomeV02.Deadline(f.Request.EvaluationDate)).DayOfWeek);
        Assert.Throws<EvidenceBindingException>(() => OutcomeV02.Enroll(s.Candidate, s.Capture, f.Evidence, f.After(1), f.After(1)));
    }
    [Theory]
    [InlineData("non-candidate")] [InlineData("policy")] [InlineData("schema")] [InlineData("hash")] [InlineData("capture")] [InlineData("clock")] [InlineData("evidence")]
    public void InvalidSourceOrChronologyCannotEnroll(string defect)
    {
        var f = new OutcomeV02Fixture(candidate: defect != "non-candidate"); var s = f.Source(); var candidate = s.Candidate; var capture = s.Capture;
        if (defect == "policy") candidate = candidate with { Projection = candidate.Projection with { CandidatePolicyId = "current" } };
        if (defect == "schema") candidate = candidate with { Projection = candidate.Projection with { SchemaVersion = 2 } };
        if (defect == "hash") candidate = candidate with { ReplayIdentity = new string('b', 64) };
        if (defect == "capture") capture = capture with { CaptureId = Guid.NewGuid() };
        if (defect == "clock") candidate = candidate with { RecordedAt = f.Known.AddDays(1) };
        var evidence = defect == "evidence" ? f.Evidence with { Records = f.Rows.Skip(1).ToArray() } : f.Evidence;
        Assert.Throws<EvidenceBindingException>(() => OutcomeV02.Enroll(candidate, capture, evidence, f.Known.AddSeconds(3), f.Known.AddSeconds(4)));
    }
    [Fact]
    public void ChangedBindingUnderSameIdentityFailsAuthentication()
    {
        var e = new OutcomeV02Fixture().Enrollment();
        Assert.Throws<EvidenceBindingException>(() => OutcomeV02.Validate(e with { Projection = e.Projection with { OriginalCutoff = e.Projection.OriginalCutoff.AddTicks(1) } }));
        Assert.NotEqual(e.EnrollmentIdentity, OutcomeV02.EnrollmentIdentity(Guid.NewGuid()));
    }
    [Theory]
    [InlineData(1)] [InlineData(5)] [InlineData(10)] [InlineData(20)]
    public void ExactHorizonsExcludeBaseAndWeekendsAndUseDecimalParity(int n)
    {
        var f = new OutcomeV02Fixture(); var e = f.Enrollment(); f.Future(n);
        var result = OutcomeV02.Assess(e, n, f.ForwardEvidence, f.After(), f.After()); var o = Assert.IsType<OutcomeV02Observation>(result.Observation);
        Assert.Equal("AVAILABLE", result.State); Assert.Equal(n, o.Projection.Manifest.Calendar.Count(d => d.Classification == "ObservedTrading"));
        Assert.Equal(checked(100m * (170m / 161.01m - 1m)), o.Projection.PriceReturnPct); Assert.Equal(f.Request.EvaluationDate, o.Projection.AnchorMarketDate);
        Assert.True(o.Projection.HorizonMarketDate > f.Request.EvaluationDate); OutcomeV02.Validate(o);
    }
    [Fact]
    public void ConfirmedClosureDoesNotCountOrSlideEndpoint()
    {
        var f = new OutcomeV02Fixture(); var e = f.Enrollment(); var day = f.Request.EvaluationDate.AddDays(1);
        f.Add(EvidenceClaim.ScheduledSession, new ScheduledSessionValue(f.Request.SessionId, EvidenceSchedule.CLOSED), day);
        Assert.Null(OutcomeV02.Horizon(e, 1, f.Evidence, f.After(1)).Date);
        Assert.Contains(OutcomeV02.Horizon(e, 1, f.Evidence, f.After(1)).Calendar, d => d.Classification == "AnnouncedClosed");
    }
    [Theory]
    [InlineData("pending")] [InlineData("missing-price")] [InlineData("coverage")] [InlineData("missing-session")]
    public void UnresolvedNeverCreatesAnObservation(string scenario)
    {
        var f = new OutcomeV02Fixture(); var e = f.Enrollment();
        if (scenario != "pending") f.Future(1, endpoint: scenario != "missing-price", coverage: scenario != "coverage");
        if (scenario == "missing-session") f.Rows.RemoveAll(r => r.Claim == EvidenceClaim.CompletedSession && r.EffectiveFrom > f.Request.EvaluationDate);
        if (scenario == "missing-session") f.Rows.RemoveAll(r => r.Claim == EvidenceClaim.GenuinePriceObservation && r.EffectiveFrom > f.Request.EvaluationDate);
        var at = scenario == "pending" ? f.Known.AddHours(1) : f.After(1);
        Assert.Null(OutcomeV02.Assess(e, 1, f.ForwardEvidence, at, at).Observation);
    }
    [Theory]
    [InlineData("anchor", "ANCHOR_UNAVAILABLE")] [InlineData("suspension", "DATA_UNAVAILABLE")] [InlineData("split", "BASIS_UNCERTAIN")]
    public void PositiveEvidenceReproducesUnavailableTerminalStates(string scenario, string state)
    {
        var f = new OutcomeV02Fixture(zero: scenario == "anchor"); var e = f.Enrollment(); f.Future(1); var date = f.Request.EvaluationDate.AddDays(1);
        if (scenario == "suspension")
        { f.Rows.RemoveAll(r => r.Claim == EvidenceClaim.TradingStatus && r.EffectiveFrom == date); f.Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.SUSPENDED, f.Request.SessionId), date, known: f.After(1)); }
        if (scenario == "split") f.Add(EvidenceClaim.CorporateAction, new ActionEventValue(EvidenceActionValueKind.EVENT, "split", EvidenceActionType.SPLIT, date, null, null), date, known: f.After(1));
        var result = OutcomeV02.Assess(e, 1, f.ForwardEvidence, f.After(1), f.After(1));
        Assert.Equal(state, result.State); Assert.NotNull(result.Observation); Assert.Null(result.Observation.Projection.PriceReturnPct);
    }
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(21)]
    public void UnsupportedHorizonRejected(int n)
    { var f = new OutcomeV02Fixture(); Assert.Throws<EvidenceBindingException>(() => OutcomeV02.Horizon(f.Enrollment(), n, f.Evidence, f.After())); }
}
