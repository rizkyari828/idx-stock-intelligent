using System.Text.Json;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

internal sealed record OutcomeV02ReplayFixture(OutcomeV02Observation Observation, OutcomeV02Enrollment Enrollment,
    ScreenerTechnicalCandidateDecision Candidate, ScreenerTechnicalCapture Capture, OutcomeV02Evidence Captured, OutcomeV02Evidence Forward)
{
    internal static OutcomeV02ReplayFixture Create(string state = "AVAILABLE", int horizon = 1, bool closure = false, bool friday = false)
    {
        var f = new OutcomeV02Fixture(zero: state == "ANCHOR_UNAVAILABLE", date: friday ? new(2026, 10, 9) : null);
        var source = f.Source(); var captured = f.Evidence; var e = f.Enrollment(); f.Future(closure ? 2 : horizon, status: state != "DATA_UNAVAILABLE");
        var d = f.Request.EvaluationDate.AddDays(1);
        if (state == "DATA_UNAVAILABLE") f.Add(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice"), d, known: f.After(1));
        if (state == "BASIS_UNCERTAIN") f.Add(EvidenceClaim.CorporateAction, new ActionEventValue(EvidenceActionValueKind.EVENT, "split", EvidenceActionType.SPLIT, d, null, null), d, known: f.After(1));
        if (closure)
        {
            f.Rows.RemoveAll(r => r.EffectiveFrom == d && r.Claim is EvidenceClaim.CompletedSession or EvidenceClaim.GenuinePriceObservation or EvidenceClaim.TradingStatus);
            f.Add(EvidenceClaim.ScheduledSession, new ScheduledSessionValue(f.Request.SessionId, EvidenceSchedule.CLOSED), d);
        }
        var forward = f.ForwardEvidence; var o = OutcomeV02.Assess(e, horizon, forward, f.After(), f.After()).Observation!;
        Assert.NotNull(o); Assert.Equal(state, o.Projection.State);
        return new(o, e, source.Candidate, source.Capture, captured, forward);
    }
    internal OutcomeV02VerificationResult Verify() => OutcomeV02Verification.Verify(Observation, Enrollment, Candidate, Capture, Captured, Forward,
        Observation.Projection.RecordedAt.AddYears(1));
    internal OutcomeV02ReplayFixture Change(OutcomeV02ObservationProjection projection) => this with
    { Observation = Observation with { Projection = projection, ResultHash = ScreenerReferences.Hash(projection) } };
}
public sealed class OutcomeV02VerificationTests
{
    [Theory]
    [InlineData("AVAILABLE", 1)] [InlineData("AVAILABLE", 5)] [InlineData("AVAILABLE", 10)] [InlineData("AVAILABLE", 20)]
    [InlineData("ANCHOR_UNAVAILABLE", 1)] [InlineData("DATA_UNAVAILABLE", 1)] [InlineData("BASIS_UNCERTAIN", 1)]
    public void ExactCommittedTerminalReplayMatchesWithOriginalChronology(string state, int horizon)
    {
        var f = OutcomeV02ReplayFixture.Create(state, horizon); var result = f.Verify();
        Assert.Equal(OutcomeVerificationState.Match, result.State); Assert.Empty(result.Differences);
        Assert.Equal(f.Observation.Projection.OutcomeKnownAt, result.ReplayedProjection!.OutcomeKnownAt);
        Assert.Equal(f.Observation.Projection.RecordedAt, result.ReplayedProjection.RecordedAt);
        Assert.True(result.VerifiedAt > result.RecordedAt);
    }
    [Theory]
    [InlineData("return")] [InlineData("anchor")] [InlineData("endpoint")] [InlineData("date")] [InlineData("state")]
    public void CompleteAuthenticatedReplayDetectsDifferentTypedProjection(string field)
    {
        var f = OutcomeV02ReplayFixture.Create(); var p = f.Observation.Projection;
        p = field switch { "return" => p with { PriceReturnPct = p.PriceReturnPct + 1m }, "anchor" => p with { AnchorClose = p.AnchorClose + 1m },
            "endpoint" => p with { HorizonClose = p.HorizonClose + 1m }, "date" => p with { HorizonMarketDate = p.HorizonMarketDate.AddDays(1) },
            _ => p with { State = "BASIS_UNCERTAIN", Reason = "UNIT_CHANGING_EVENT", PriceReturnPct = null } };
        var result = f.Change(p).Verify(); Assert.Equal(OutcomeVerificationState.DifferentResult, result.State);
        Assert.NotEmpty(result.Differences); Assert.True(result.Differences.Count <= 100);
        Assert.Equal(result.Differences.OrderBy(d => d.Field, StringComparer.Ordinal), result.Differences);
    }
    [Fact]
    public void ChangedTerminalReasonDiffersRatherThanBorrowingNewFacts()
    {
        var f = OutcomeV02ReplayFixture.Create("BASIS_UNCERTAIN");
        Assert.Equal(OutcomeVerificationState.DifferentResult, f.Change(f.Observation.Projection with { Reason = "CAPITAL_ACTION_UNSUPPORTED" }).Verify().State);
    }
    [Theory]
    [InlineData("outcome-policy")] [InlineData("outcome-schema")] [InlineData("candidate-policy")] [InlineData("candidate-schema")]
    [InlineData("capture-policy")] [InlineData("capture-schema")]
    public void UnsupportedExactPolicyPrecedesMissingInput(string defect)
    {
        var f = OutcomeV02ReplayFixture.Create() with { Forward = new([], []) };
        if (defect == "outcome-policy") f = f with { Observation = f.Observation with { Projection = f.Observation.Projection with { OutcomePolicyId = "current" } } };
        if (defect == "outcome-schema") f = f with { Observation = f.Observation with { Projection = f.Observation.Projection with { SchemaVersion = 2 } } };
        if (defect == "candidate-policy") f = f with { Candidate = f.Candidate with { Projection = f.Candidate.Projection with { CandidatePolicyId = "current" } } };
        if (defect == "candidate-schema") f = f with { Candidate = f.Candidate with { Projection = f.Candidate.Projection with { SchemaVersion = 2 } } };
        if (defect == "capture-policy") f = f with { Capture = f.Capture with { Projection = f.Capture.Projection with { Result = f.Capture.Projection.Result with
            { Request = f.Capture.Projection.Result.Request with { PolicyId = "current" } } } } };
        if (defect == "capture-schema") f = f with { Capture = f.Capture with { Projection = f.Capture.Projection with { CaptureSchemaVersion = 2 } } };
        Assert.Equal(OutcomeVerificationState.PolicyVersionUnavailable, f.Verify().State);
    }
    [Theory]
    [InlineData("candidate-hash")] [InlineData("capture-hash")] [InlineData("enrollment-hash")] [InlineData("observation-hash")]
    [InlineData("anchor")] [InlineData("forward")] [InlineData("candidate-false")] [InlineData("session")]
    [InlineData("chronology")] [InlineData("manifest")] [InlineData("artifact")]
    public void MissingOrCorruptExactInputFailsClosed(string defect)
    {
        var f = OutcomeV02ReplayFixture.Create();
        f = defect switch
        {
            "candidate-hash" => f with { Candidate = f.Candidate with { ReplayIdentity = new string('b', 64) } },
            "capture-hash" => f with { Capture = f.Capture with { ResultHash = new string('b', 64) } },
            "enrollment-hash" => f with { Enrollment = f.Enrollment with { BindingHash = new string('b', 64) } },
            "observation-hash" => f with { Observation = f.Observation with { ResultHash = new string('b', 64) } },
            "anchor" => f with { Enrollment = f.Enrollment with { Projection = f.Enrollment.Projection with { AnchorReason = "invented" } } },
            "forward" => f with { Forward = f.Forward with { Records = f.Forward.Records.Skip(1).ToArray() } },
            "candidate-false" => f with { Candidate = f.Candidate with { Projection = f.Candidate.Projection with { Candidate = false } } },
            "session" => f with { Enrollment = f.Enrollment with { Projection = f.Enrollment.Projection with { SessionId = "other" } } },
            "chronology" => f with { Enrollment = f.Enrollment with { Projection = f.Enrollment.Projection with { EnrollmentKnownAt = f.Enrollment.Projection.EnrollmentDeadline } } },
            "manifest" => f.Change(f.Observation.Projection with { Manifest = f.Observation.Projection.Manifest with { Evidence = [] } }),
            _ => f with { Forward = f.Forward with { Artifacts = [] } }
        };
        Assert.Equal(OutcomeVerificationState.InputNotAvailable, f.Verify().State);
    }
    [Theory]
    [InlineData(true, false)] [InlineData(false, true)]
    public void FrozenClosuresAndWeekendsReplayExactly(bool closure, bool friday)
    { Assert.Equal(OutcomeVerificationState.Match, OutcomeV02ReplayFixture.Create(closure: closure, friday: friday).Verify().State); }
    [Fact]
    public void UnboundLaterEvidenceCannotEnterTheRetainedSet()
    {
        var f = OutcomeV02ReplayFixture.Create(); var before = f.Verify();
        var later = AsOfFixture.Row(EvidenceClaim.Currency, new CurrencyValue("USD"), known: f.Observation.Projection.OutcomeKnownAt.AddDays(1));
        Assert.Equal(OutcomeVerificationState.Match, before.State);
        // Supplying even a valid extra row is rejected, rather than silently changing selection.
        Assert.Equal(OutcomeVerificationState.InputNotAvailable, (f with { Forward = f.Forward with { Records = [.. f.Forward.Records, later] } }).Verify().State);
        Assert.Equal(before.State, f.Verify().State);
        var replacement = OutcomeV02ReplayFixture.Create();
        Assert.Equal(OutcomeVerificationState.InputNotAvailable, (f with { Candidate = replacement.Candidate }).Verify().State);
    }
    [Theory]
    [InlineData("{}", 1, true)] [InlineData("{\"cutoff\":null}", 1, false)] [InlineData("[]", 1, false)] [InlineData("{}", 2, false)]
    public void RequestIsExactAndBounded(string body, int horizon, bool valid)
    {
        using var json = JsonDocument.Parse(body);
        if (valid) OutcomeV02Verification.ValidateRequest(Guid.NewGuid(), horizon, json.RootElement);
        else Assert.Equal(400, Assert.Throws<ScreenerException>(() => OutcomeV02Verification.ValidateRequest(Guid.NewGuid(), horizon, json.RootElement)).StatusCode);
    }
}
