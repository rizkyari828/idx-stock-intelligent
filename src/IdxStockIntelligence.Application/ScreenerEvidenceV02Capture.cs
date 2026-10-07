using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record ScreenerTechnicalCaptureBoundary(DateOnly Date, MarketEligibilityResult MarketEligibility,
    TradingStatusResult TradingStatus, PriceComparabilityResult PriceComparability, PriceComparabilityResult HistoryComparability,
    IReadOnlyDictionary<TechnicalFeature, FeatureState> CoreFeatures, IReadOnlyDictionary<OptionalFeature, FeatureState> OptionalFeatures,
    bool DataReady, bool CanEvaluateSetup, IReadOnlyList<string> Diagnostics, IReadOnlyList<Guid> EvidenceIds);
public sealed record ScreenerTechnicalCaptureProjection(int CaptureSchemaVersion, ScreenerEvidenceTechnicalResult Result,
    IReadOnlyList<ScreenerTechnicalCaptureBoundary> ReadinessHistory);
public sealed record ScreenerTechnicalCapture(Guid CaptureId, string InputHash, string ResultHash,
    ScreenerTechnicalCaptureProjection Projection, DateTimeOffset? RecordedAt = null);

public static class ScreenerEvidenceTechnicalCapture
{
    public const int SchemaVersion = 1;
    public const int MaximumBytes = 32 * 1024 * 1024;
    private static readonly string[] CoreNames = ["ema20", "ema50", "atr14", "priorHigh20", "priorLow20", "distanceToHighPercent"];
    private static readonly HashSet<string> FieldNames = [.. CoreNames, "atrPercent", "changePercent", "dailyValueProxyIdr",
        "monetaryLiquidity20Idr", "volumeRatio20", "actualTradedValue", "rs20Pp", "rs60Pp"];
    private static readonly HashSet<string> BenchmarkNames = ["ema20", "ema50", "atr14", "atrPercent"];

    // Only actual execution can cross this boundary; readiness flags alone never create a capture.
    public static ScreenerTechnicalCapture Create(ScreenerEvidenceReadinessResult readiness,
        IReadOnlyList<ScreenerEvidenceReadinessResult> history, CancellationToken ct = default)
    {
        readiness = readiness with { Request = readiness.Request with { Cutoff = readiness.Request.Cutoff.ToUniversalTime() } };
        history = history.Select(r => r with { Request = r.Request with { Cutoff = r.Request.Cutoff.ToUniversalTime() } }).ToArray();
        var result = ScreenerEvidenceTechnical.Execute(readiness, history, ct);
        if (!result.TechnicalEvaluated) throw new EvidenceBindingException("TECHNICAL_CAPTURE_NOT_EVALUATED");
        var boundaries = history.OrderBy(r => r.Request.EvaluationDate).Select(r => new ScreenerTechnicalCaptureBoundary(
            r.Request.EvaluationDate, r.MarketEligibility, r.TradingStatus, r.PriceComparability, r.HistoryComparability,
            r.CoreFeatures, r.OptionalFeatures, r.DataReady, r.CanEvaluateSetup, ScreenerEvidenceReasons.Canonical(r.Diagnostics),
            r.References.Select(e => e.EvidenceId).OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray())).ToArray();
        var projection = new ScreenerTechnicalCaptureProjection(SchemaVersion, result, boundaries);
        var inputHash = InputHash(projection, ct);
        var capture = new ScreenerTechnicalCapture(Id(inputHash), inputHash, ScreenerReferences.Hash(projection, ct), projection);
        Validate(capture, ct);
        return capture;
    }

    public static string InputHash(ScreenerTechnicalCaptureProjection projection, CancellationToken ct = default) =>
        ScreenerReferences.Hash(new { projection.CaptureSchemaVersion,
            Request = projection.Result.Request with { Cutoff = projection.Result.Request.Cutoff.ToUniversalTime() },
            projection.Result.StockBindings, projection.Result.BenchmarkBindings, projection.Result.References,
            projection.ReadinessHistory }, ct);
    public static Guid Id(string inputHash) => ScreenerReferences.IsHash(inputHash)
        ? Guid.ParseExact(inputHash[..32], "N") : throw new EvidenceBindingException("TECHNICAL_CAPTURE_MALFORMED");

    public static void Validate(ScreenerTechnicalCapture capture, CancellationToken ct = default)
    {
        static void Require(bool valid)
        { if (!valid) throw new EvidenceBindingException("TECHNICAL_CAPTURE_MALFORMED"); }
        var p = capture.Projection; var r = p.Result; r.Request.Validate();
        Require(p.CaptureSchemaVersion == SchemaVersion && r.TechnicalEvaluated && r.Setup.Evaluated
            && ScreenerReferences.IsHash(r.ReplayIdentity) && p.ReadinessHistory.Count is >= 50 and <= 330
            && r.References.Count <= ScreenerEvidenceAsOf.MaximumRecords
            && r.References.Select(e => e.EvidenceId).Distinct().Count() == r.References.Count
            && r.References.All(e => e.EvidenceId != Guid.Empty && e.RevisionNumber > 0 && e.PayloadVersion == 1
                && ScreenerReferences.IsHash(e.PayloadHash) && ScreenerEvidenceValidity.Visible(e.Chronology, r.Request.Cutoff)));
        var ids = r.References.Select(e => e.EvidenceId).ToHashSet();
        Require(p.ReadinessHistory.Select(d => d.Date).Distinct().Count() == p.ReadinessHistory.Count
            && p.ReadinessHistory.All(d => d.EvidenceIds.Count <= ScreenerEvidenceAsOf.MaximumRecords
                && d.EvidenceIds.Distinct().Count() == d.EvidenceIds.Count && d.EvidenceIds.All(ids.Contains))
            && p.ReadinessHistory.Select(d => d.Date).Order().SequenceEqual(r.StockBindings.Select(b => b.Date).Order()));
        var last = p.ReadinessHistory.MaxBy(d => d.Date)!;
        Require(last.Date == r.Request.EvaluationDate && last.DataReady && last.CanEvaluateSetup
            && last.MarketEligibility.Status == EligibilityStatus.Eligible && last.PriceComparability.State == PriceComparability.Cleared
            && last.CoreFeatures.Count == 6 && last.CoreFeatures.Values.All(f => f.Availability == Availability.AVAILABLE)
            && Enum.GetValues<OptionalFeature>().All(last.OptionalFeatures.ContainsKey));
        Require(CoreNames.All(k => r.Fields.TryGetValue(k, out var f) && f.Availability == Availability.AVAILABLE && f.Value is not null)
            && FieldNames.SetEquals(r.Fields.Keys) && BenchmarkNames.SetEquals(r.BenchmarkFields.Keys)
            && r.Fields.Values.Concat(r.BenchmarkFields.Values).All(f => Enum.IsDefined(f.Availability)
                && (f.Availability == Availability.AVAILABLE ? f.Value is not null && f.UnavailableReason is null
                    : f.Value is null && !string.IsNullOrWhiteSpace(f.UnavailableReason)))
            && r.Reasons.SequenceEqual(ScreenerEvidenceReasons.Canonical(r.Reasons)));
        var references = r.References.ToDictionary(e => e.EvidenceId);
        foreach (var group in new[] { r.StockBindings, r.BenchmarkBindings })
        {
            Require(group.Count <= 330 && group.Select(b => b.Date).Distinct().Count() == group.Count);
            foreach (var b in group)
                Require(b.SubjectId == (ReferenceEquals(group, r.StockBindings) ? r.Request.SubjectId : r.Request.Benchmark?.SubjectId)
                    && b.Date >= r.Request.HistoryFrom && b.Date <= r.Request.EvaluationDate
                    && references.TryGetValue(b.Evidence.EvidenceId, out var e) && e == b.Evidence
                    && ids.Contains(b.Price.ConventionEvidenceId) && ids.Contains(b.Price.CompletedSessionEvidenceId)
                    && b.Price.SessionId == r.Request.SessionId && b.ObservationSource is not null
                    && b.Price.BarContentHash == ScreenerEvidenceBinding.BarHash(b.Price.Bar)
                    && b.Evidence.PayloadHash == ScreenerEvidenceJson.Sha256(ScreenerEvidenceBinding.Encode(EvidenceClaim.GenuinePriceObservation,
                        new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, b.Price))));
        }
        Require(r.ReplayIdentity == ScreenerReferences.Hash(new { request = r.Request, stock = r.StockBindings,
            benchmark = r.BenchmarkBindings, retained = r.References,
            history = p.ReadinessHistory.Select(d => new { EvaluationDate = d.Date, d.MarketEligibility, d.DataReady, d.CanEvaluateSetup }) }, ct));
        Require(capture.InputHash == InputHash(p, ct) && capture.CaptureId == Id(capture.InputHash)
            && capture.ResultHash == ScreenerReferences.Hash(p, ct));
    }
}
