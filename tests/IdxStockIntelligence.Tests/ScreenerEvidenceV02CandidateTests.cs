using System.Globalization;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceV02CandidateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    internal static TechnicalFixture Boundary(decimal close, bool reset = true, bool quantity = true, bool zero = false)
    {
        var f = new TechnicalFixture(quantity: quantity, zero: zero);
        if (reset)
        {
            var day = f.Evidence.Dates[^2];
            f.Evidence.Rows.RemoveAll(r => r.Claim == EvidenceClaim.TradingStatus && r.EffectiveFrom == day);
            f.Evidence.Add(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.SUSPENDED, f.Request.SessionId), day);
        }
        SetClose(f, f.Request.EvaluationDate, close);
        return f;
    }
    private static void SetClose(TechnicalFixture f, DateOnly date, decimal close)
    {
        var row = f.Evidence.Rows.Single(r => r.Claim == EvidenceClaim.GenuinePriceObservation && r.EffectiveFrom == date);
        var p = (PriceValue)ScreenerEvidenceBinding.Decode(row).Value!;
        var bar = p.Bar with { Open = new(close - 1m), Close = new(close), High = new(close + 2m), Low = new(close - 3m) };
        f.Evidence.Rows.Remove(row);
        f.Evidence.Rows.Add(ScreenerEvidenceBinding.Bind(row, row.EvidenceClass!.Value, row.ScopeKind!.Value, row.ScopeExchangeId!.Value,
            new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, p with { Bar = bar, BarContentHash = ScreenerEvidenceBinding.BarHash(bar) })));
    }

    [Theory]
    [InlineData("157.77", SetupStatus.None, false)]
    [InlineData("157.78", SetupStatus.Watch, true)]
    [InlineData("161", SetupStatus.Watch, true)]
    [InlineData("161.01", SetupStatus.Confirmed, true)]
    public void ExactFrozenBoundaryPromotesFromActualCapturedExecutionWithV01Parity(string literal, SetupStatus setup, bool candidate)
    {
        var close = decimal.Parse(literal, CultureInfo.InvariantCulture);
        var capture = ScreenerEvidenceV02CaptureTests.Capture(Boundary(close));
        var decision = ScreenerEvidenceTechnicalCandidates.Promote(capture, Token);
        var legacy = ScreenerEpisodes.Advance(capture.Projection.Result.Request.SubjectId, decision.Projection.EvaluationDate,
            ScreenerSetup.Empty, EligibilityStatus.Eligible, close, 161m);
        Assert.Equal(setup, legacy.Status); Assert.Equal(setup, decision.Projection.Setup);
        Assert.Equal(candidate, decision.Projection.Candidate);
        Assert.Equal(ScreenerOrdering.QualifiesTechnically(EligibilityStatus.Eligible, legacy), decision.Projection.Candidate);
        Assert.Equal(capture.CaptureId, decision.Projection.SourceCaptureId); Assert.Equal(capture.InputHash, decision.Projection.SourceInputHash);
        Assert.Equal(capture.ResultHash, decision.Projection.SourceResultHash); Assert.Equal(capture.Projection.Result.Request.Cutoff, decision.Projection.Cutoff);
        Assert.Equal(ScreenerEvidenceTechnicalCandidates.PolicyId, decision.Projection.CandidatePolicyId);
        Assert.Equal(1, decision.Projection.SchemaVersion); Assert.Equal(capture.Projection.Result.Request.PolicyId, decision.Projection.TechnicalPolicyId);
        Assert.Null(decision.RecordedAt); Assert.DoesNotContain("NOT_EVALUATED", decision.Projection.Reasons);
    }

    [Fact]
    public void FailedAndExpiredAreValidEvaluatedNonCandidates()
    {
        var failedFixture = Boundary(100m, reset: false);
        SetClose(failedFixture, failedFixture.Evidence.Dates[^2], 200m);
        var failed = ScreenerEvidenceV02CaptureTests.Capture(failedFixture);
        Assert.Equal(SetupStatus.Failed, failed.Projection.Result.Setup.Status);
        Assert.False(ScreenerEvidenceTechnicalCandidates.Promote(failed, Token).Projection.Candidate);
        var f = new TechnicalFixture(); var expired = ScreenerEvidenceV02CaptureTests.Capture(f);
        Assert.Contains("WATCH_EXPIRED", expired.Projection.Result.Setup.Reasons);
        Assert.False(ScreenerEvidenceTechnicalCandidates.Promote(expired, Token).Projection.Candidate);
    }

    [Theory]
    [InlineData("execution")]
    [InlineData("setup")]
    [InlineData("hash")]
    [InlineData("identity")]
    [InlineData("policy")]
    [InlineData("schema")]
    [InlineData("technical-schema")]
    [InlineData("unknown-setup")]
    public void UnexecutedOrInvalidCaptureCannotPromote(string defect)
    {
        var c = ScreenerEvidenceV02CaptureTests.Capture(new TechnicalFixture()); var r = c.Projection.Result;
        if (defect == "execution") c = c with { Projection = c.Projection with { Result = r with { TechnicalEvaluated = false } } };
        if (defect == "setup") c = c with { Projection = c.Projection with { Result = r with { Setup = ScreenerSetup.Empty } } };
        if (defect == "hash") c = c with { ResultHash = new string('a', 64) };
        if (defect == "identity") c = c with { CaptureId = Guid.NewGuid() };
        if (defect == "policy") c = c with { Projection = c.Projection with { Result = r with { Request = r.Request with { PolicyId = "unknown" } } } };
        if (defect == "schema") c = c with { Projection = c.Projection with { CaptureSchemaVersion = 2 } };
        if (defect == "technical-schema") c = c with { Projection = c.Projection with { Result = r with { Request = r.Request with { SchemaVersion = 2 } } } };
        if (defect == "unknown-setup")
        {
            var p = c.Projection with { Result = r with { Setup = r.Setup with { Status = (SetupStatus)100 } } };
            c = c with { Projection = p, ResultHash = ScreenerReferences.Hash(p) };
        }
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceTechnicalCandidates.Promote(c, Token));
    }

    [Theory]
    [InlineData("benchmark-missing")]
    [InlineData("optional-warmup")]
    [InlineData("genuine-zero")]
    [InlineData("unknown-quantity")]
    public void OptionalFeatureFailureDoesNotAddCandidateGatesOrCoerceValues(string scenario)
    {
        var f = Boundary(161.01m, quantity: scenario != "unknown-quantity", zero: scenario == "genuine-zero");
        if (scenario == "optional-warmup") f.Request = f.Evidence.WithBenchmark(50);
        var c = ScreenerEvidenceV02CaptureTests.Capture(f); var before = ScreenerReferences.Hash(c);
        Assert.True(ScreenerEvidenceTechnicalCandidates.Promote(c, Token).Projection.Candidate);
        var field = c.Projection.Result.Fields[scenario is "genuine-zero" or "unknown-quantity" ? "monetaryLiquidity20Idr" : "rs60Pp"];
        if (scenario == "genuine-zero") { Assert.Equal(0m, field.Value); Assert.Equal(Availability.AVAILABLE, field.Availability); }
        else { Assert.Null(field.Value); Assert.NotNull(field.UnavailableReason); }
        if (scenario == "unknown-quantity") Assert.Equal("VOLUME_BASIS_UNVERIFIED", field.UnavailableReason);
        Assert.Equal(before, ScreenerReferences.Hash(c));
    }

    [Fact]
    public void ReplayIsDeterministicOrderIndependentAndLaterCaptureIsDistinct()
    {
        var f = Boundary(161.01m); var c = ScreenerEvidenceV02CaptureTests.Capture(f);
        var first = ScreenerEvidenceTechnicalCandidates.Promote(c, Token);
        var r = c.Projection.Result;
        var reordered = c with { RecordedAt = DateTimeOffset.UtcNow.AddYears(1), Projection = c.Projection with
        { ReadinessHistory = c.Projection.ReadinessHistory.Reverse().ToArray(), Result = r with
        { StockBindings = r.StockBindings.Reverse().ToArray(), References = r.References.Reverse().ToArray(),
            Fields = r.Fields.Reverse().ToDictionary(x => x.Key, x => x.Value),
            Setup = r.Setup with { Reasons = r.Setup.Reasons.Reverse().ToArray() } } } };
        Assert.Equal(ScreenerReferences.Hash(first), ScreenerReferences.Hash(ScreenerEvidenceTechnicalCandidates.Promote(reordered, Token)));
        Assert.Equal(first.ReplayIdentity, ScreenerReferences.Hash(first.Projection));
        var input = f.Input();
        var canonicalCapture = ScreenerEvidenceTechnicalCapture.Create(input.Current with { Diagnostics = input.Current.Diagnostics.Reverse().ToArray(),
            References = input.Current.References.Reverse().ToArray() },
            input.History.Reverse().Select(h => h with { Diagnostics = h.Diagnostics.Reverse().ToArray() }).ToArray(), Token);
        Assert.Equal(first.ReplayIdentity, ScreenerEvidenceTechnicalCandidates.Promote(canonicalCapture, Token).ReplayIdentity);
        f.Request = f.Request with { Cutoff = f.Request.Cutoff.AddTicks(1) };
        var later = ScreenerEvidenceTechnicalCandidates.Promote(ScreenerEvidenceV02CaptureTests.Capture(f), Token);
        Assert.NotEqual(first.DecisionId, later.DecisionId); Assert.NotEqual(first.ReplayIdentity, later.ReplayIdentity);
    }

    [Theory]
    [InlineData(EligibilityStatus.Eligible, true, SetupStatus.Watch, true)]
    [InlineData(EligibilityStatus.Eligible, true, SetupStatus.Confirmed, true)]
    [InlineData(EligibilityStatus.Eligible, true, SetupStatus.None, false)]
    [InlineData(EligibilityStatus.Eligible, true, SetupStatus.Failed, false)]
    [InlineData(EligibilityStatus.Eligible, false, SetupStatus.Watch, false)]
    [InlineData(EligibilityStatus.DataBlocked, true, SetupStatus.Confirmed, false)]
    [InlineData(EligibilityStatus.Ineligible, true, SetupStatus.Watch, false)]
    public void SharedPredicateMatchesOriginalV01TruthTable(EligibilityStatus eligibility, bool evaluated, SetupStatus state, bool expected) =>
        Assert.Equal(expected, ScreenerOrdering.QualifiesTechnically(eligibility, new(state, evaluated, [], null)));
}
