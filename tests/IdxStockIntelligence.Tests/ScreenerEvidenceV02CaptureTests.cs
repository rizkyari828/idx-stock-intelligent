using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceV02CaptureTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    internal static ScreenerTechnicalCapture Capture(TechnicalFixture f)
    { var input = f.Input(); return ScreenerEvidenceTechnicalCapture.Create(input.Current, input.History, Token); }

    [Theory]
    [InlineData("ready")]
    [InlineData("benchmark")]
    [InlineData("zero")]
    [InlineData("unknown")]
    public void CompletedExecutionRetainsExactProjectionAndOptionalStates(string scenario)
    {
        var f = new TechnicalFixture(quantity: scenario != "unknown", zero: scenario == "zero");
        if (scenario == "benchmark") f.Request = f.Evidence.WithBenchmark(61);
        var capture = Capture(f); var result = capture.Projection.Result;
        Assert.Equal(ScreenerReferences.Hash(f.Execute()), ScreenerReferences.Hash(result));
        Assert.Equal(ScreenerEvidenceV02.PolicyId, result.Request.PolicyId);
        Assert.Equal(1, result.Request.SchemaVersion); Assert.Equal(1, capture.Projection.CaptureSchemaVersion);
        Assert.Equal(61, capture.Projection.ReadinessHistory.Count);
        Assert.Equal(scenario == "benchmark" ? 61 : 0, result.BenchmarkBindings.Count);
        Assert.Null(result.Fields["actualTradedValue"].Value);
        Assert.Equal(Availability.UNAVAILABLE, result.Fields["actualTradedValue"].Availability);
        if (scenario == "zero")
        { Assert.Equal(0m, result.Fields["monetaryLiquidity20Idr"].Value); Assert.Null(result.Fields["volumeRatio20"].Value); }
        if (scenario == "unknown")
        { Assert.Null(result.Fields["monetaryLiquidity20Idr"].Value); Assert.Equal("VOLUME_BASIS_UNVERIFIED", result.Fields["monetaryLiquidity20Idr"].UnavailableReason); }
    }

    [Theory]
    [InlineData("readiness-only")]
    [InlineData("warmup")]
    [InlineData("failed-execution")]
    public void ReadinessDoesNotAuthorizeCaptureWithoutCompletedExecution(string scenario)
    {
        var f = new TechnicalFixture(scenario == "warmup" ? 19 : 61); var input = f.Input();
        Assert.Equal(scenario != "warmup", input.Current.DataReady);
        var history = scenario == "warmup" ? input.History : [];
        var ex = Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceTechnicalCapture.Create(input.Current, history, Token));
        Assert.Equal("TECHNICAL_CAPTURE_NOT_EVALUATED", ex.Reason);
    }

    [Fact]
    public void CanonicalIdentityAndHashIgnoreInputAndPropertyInsertionOrderAndCreationClock()
    {
        var f = new TechnicalFixture(); var first = Capture(f); var input = f.Input();
        ScreenerEvidenceReadinessResult Reorder(ScreenerEvidenceReadinessResult r) => r with
        { References = r.References.Reverse().ToArray(), Bars = r.Bars.Reverse().ToArray(), ActiveBars = r.ActiveBars.Reverse().ToArray(),
            CoreFeatures = r.CoreFeatures.Reverse().ToDictionary(x => x.Key, x => x.Value),
            OptionalFeatures = r.OptionalFeatures.Reverse().ToDictionary(x => x.Key, x => x.Value) };
        var second = ScreenerEvidenceTechnicalCapture.Create(Reorder(input.Current), input.History.Reverse().Select(Reorder).ToArray(), Token);
        Assert.Equal(first.CaptureId, second.CaptureId); Assert.Equal(first.InputHash, second.InputHash); Assert.Equal(first.ResultHash, second.ResultHash);
        var reorderedResult = second.Projection.Result with { Fields = second.Projection.Result.Fields.Reverse().ToDictionary(x => x.Key, x => x.Value) };
        Assert.Equal(first.ResultHash, ScreenerReferences.Hash(second.Projection with { Result = reorderedResult }));
        ScreenerEvidenceTechnicalCapture.Validate(second with { RecordedAt = DateTimeOffset.UtcNow.AddYears(1) }, Token);
    }

    [Fact]
    public void LaterCutoffCreatesNewIdentityEvenWithIdenticalTechnicalValues()
    {
        var f = new TechnicalFixture(); var first = Capture(f); f.Request = f.Request with { Cutoff = f.Request.Cutoff.AddDays(1) };
        var second = Capture(f); Assert.NotEqual(first.CaptureId, second.CaptureId); Assert.NotEqual(first.InputHash, second.InputHash);
        Assert.Equal(ScreenerReferences.Hash(first.Projection.Result.Fields), ScreenerReferences.Hash(second.Projection.Result.Fields));
    }

    [Fact]
    public void EquivalentCutoffOffsetsAreTheSameSemanticEvaluation()
    {
        var f = new TechnicalFixture(); var utc = Capture(f);
        f.Request = f.Request with { Cutoff = f.Request.Cutoff.ToOffset(TimeSpan.FromHours(7)) };
        var local = Capture(f); Assert.Equal(utc.CaptureId, local.CaptureId); Assert.Equal(utc.ResultHash, local.ResultHash);
        Assert.Equal(TimeSpan.Zero, local.Projection.Result.Request.Cutoff.Offset);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("hash")]
    [InlineData("binding")]
    [InlineData("execution")]
    public void LostOrChangedCaptureIntegrityFailsClosed(string defect)
    {
        var c = Capture(new TechnicalFixture()); var p = c.Projection; var r = p.Result;
        if (defect == "identity") c = c with { CaptureId = Guid.NewGuid() };
        if (defect == "hash") c = c with { ResultHash = new string('a', 64) };
        if (defect == "binding") c = c with { Projection = p with { Result = r with { References = r.References.Skip(1).ToArray() } } };
        if (defect == "execution") c = c with { Projection = p with { Result = r with { TechnicalEvaluated = false } } };
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceTechnicalCapture.Validate(c, Token));
    }
}
