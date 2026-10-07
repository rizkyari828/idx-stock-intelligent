using System.Globalization;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record ScreenerEvidenceTechnicalResult(ScreenerReadinessRequest Request, bool TechnicalEvaluated,
    ScreenerSetup Setup, IReadOnlyDictionary<string, FeatureState> Fields,
    IReadOnlyDictionary<string, FeatureState> BenchmarkFields, string Trend, string BenchmarkTrend, string BenchmarkVolatility,
    IReadOnlyList<string> Reasons, IReadOnlyList<ScreenerReadinessBarBinding> StockBindings,
    IReadOnlyList<ScreenerReadinessBarBinding> BenchmarkBindings, IReadOnlyList<ScreenerEvidenceProvenance> References,
    string ReplayIdentity);

public static class ScreenerEvidenceTechnical
{
    private static readonly IReadOnlyDictionary<TechnicalFeature, string> Core = new Dictionary<TechnicalFeature, string>
    {
        [TechnicalFeature.Ema20] = "ema20", [TechnicalFeature.Ema50] = "ema50", [TechnicalFeature.Atr14] = "atr14",
        [TechnicalFeature.PriorHigh20] = "priorHigh20", [TechnicalFeature.PriorLow20] = "priorLow20",
        [TechnicalFeature.DistanceToHighPercent] = "distanceToHighPercent"
    };
    private static readonly IReadOnlyDictionary<OptionalFeature, string[]> Optional = new Dictionary<OptionalFeature, string[]>
    {
        [OptionalFeature.RelativeVolume] = ["volumeRatio20"],
        [OptionalFeature.MonetaryLiquidityProxy] = ["dailyValueProxyIdr", "monetaryLiquidity20Idr"],
        [OptionalFeature.ActualTradedValue] = ["actualTradedValue"],
        [OptionalFeature.Rs20] = ["rs20Pp"], [OptionalFeature.Rs60] = ["rs60Pp"]
    };
    private static bool Gate(ScreenerEvidenceReadinessResult r) => r.FailureReason is null && r.DataReady && r.CanEvaluateSetup
        && r.MarketEligibility.Status == EligibilityStatus.Eligible && r.PriceComparability.State == PriceComparability.Cleared
        && Core.Keys.All(f => r.CoreFeatures.TryGetValue(f, out var state) && state.Availability == Availability.AVAILABLE);

    public static ScreenerEvidenceTechnicalResult NotEvaluated(ScreenerEvidenceReadinessResult readiness, string? reason = null) =>
        new(readiness.Request, false, ScreenerSetup.Empty, new Dictionary<string, FeatureState>(), new Dictionary<string, FeatureState>(),
            "UNKNOWN", "UNKNOWN", "UNKNOWN", ScreenerEvidenceReasons.Canonical(readiness.Diagnostics.Concat(
                new[] { ScreenerEvidenceReasons.NotEvaluated, reason ?? ScreenerEvidenceReasons.DataNotReady })), [], [], readiness.References.OrderBy(r => r.EvidenceId.ToString("D"), StringComparer.Ordinal).ToArray(),
            ScreenerReferences.Hash(new { readiness.Request, readiness.References, reason }));

    public static ScreenerEvidenceTechnicalResult Execute(ScreenerEvidenceReadinessResult readiness,
        IReadOnlyList<ScreenerEvidenceReadinessResult> history, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            if (!ScreenerEvidenceV02.Supports(readiness.Request.PolicyId, readiness.Request.SchemaVersion))
                return NotEvaluated(readiness, "PERSISTED_EVIDENCE_BINDING_UNSUPPORTED");
            readiness.Request.Validate();
            if (!Gate(readiness)) return NotEvaluated(readiness);
            var request = readiness.Request;
            Require(Enum.GetValues<OptionalFeature>().All(readiness.OptionalFeatures.ContainsKey));
            var stock = readiness.ActiveBars.Where(b => b.SubjectId == request.SubjectId).OrderBy(b => b.Date).ToArray();
            var benchmark = readiness.ActiveBars.Where(b => b.SubjectId == request.Benchmark?.SubjectId).OrderBy(b => b.Date).ToArray();
            Require(stock.Length >= 50 && stock.Select(b => b.Date).Distinct().Count() == stock.Length && stock[^1].Date == request.EvaluationDate
                && readiness.ActiveBars.Count == stock.Length + benchmark.Length
                && readiness.References.Count <= ScreenerEvidenceAsOf.MaximumRecords);
            var retained = readiness.References.OrderBy(r => r.EvidenceId.ToString("D"), StringComparer.Ordinal).ToArray();
            var references = retained.ToDictionary(r => r.EvidenceId);
            var stockBars = stock.Select(b => Bar(b, readiness, references)).ToArray();
            var benchmarkBars = benchmark.Select(b => Bar(b, readiness, references)).ToDictionary(b => b.SessionDate);
            var days = history.OrderBy(r => r.Request.EvaluationDate).ToArray();
            Require(days.Length == stock.Length && days.Select(r => r.Request.EvaluationDate).SequenceEqual(stock.Select(b => b.Date)));
            var setup = ScreenerSetup.Empty;
            IReadOnlyDictionary<string, FeatureState> fields = new Dictionary<string, FeatureState>();
            // ponytail: batch arithmetic replay is quadratic within the 330-day ceiling; incremental state only if that ceiling grows.
            for (var i = 0; i < days.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var day = days[i];
                Require(day.Request == request with { EvaluationDate = stock[i].Date }
                    && day.ActiveBars.Where(b => b.SubjectId == request.SubjectId).OrderBy(b => b.Date).SequenceEqual(stock.Take(i + 1)));
                if (i == days.Length - 1) Require(ScreenerReferences.Hash(day, ct) == ScreenerReferences.Hash(readiness, ct));
                if (!Gate(day))
                {
                    Require(day.FailureReason is null);
                    setup = ScreenerEpisodes.Advance(request.SubjectId, stock[i].Date, setup,
                        day.MarketEligibility.Status == EligibilityStatus.Ineligible ? EligibilityStatus.Ineligible : EligibilityStatus.DataBlocked,
                        null, null);
                    continue;
                }
                var admitted = Core.Values.ToHashSet(StringComparer.Ordinal);
                if (i == days.Length - 1)
                {
                    admitted.UnionWith(["atrPercent", "changePercent"]);
                    foreach (var (feature, names) in Optional)
                        if (readiness.OptionalFeatures[feature].Availability == Availability.AVAILABLE) admitted.UnionWith(names);
                }
                fields = ScreenerTechnicalArithmetic.Calculate(stockBars.Take(i + 1).ToArray(), benchmarkBars,
                    null, false, null, null, ct, admitted);
                Require(Core.Values.All(f => fields[f].Availability == Availability.AVAILABLE && fields[f].Value is not null),
                    fields.Values.Any(f => f.UnavailableReason == "NUMERIC_OUT_OF_RANGE") ? "NUMERIC_OUT_OF_RANGE" : "TECHNICAL_READINESS_MISMATCH");
                setup = ScreenerEpisodes.Advance(request.SubjectId, stock[i].Date, setup, EligibilityStatus.Eligible,
                    stockBars[i].Close, fields["priorHigh20"].Value);
                if (setup.Episode is { } episode)
                    setup = setup with { Episode = episode with { Id = string.Create(CultureInfo.InvariantCulture,
                        $"{request.PolicyId}/{request.SubjectId:D}/{episode.StartDate:yyyy-MM-dd}") } };
            }
            var values = new Dictionary<string, FeatureState>(fields, StringComparer.Ordinal);
            foreach (var (feature, names) in Optional)
                foreach (var name in names)
                {
                    var ready = readiness.OptionalFeatures[feature];
                    if (ready.Availability != Availability.AVAILABLE) values[name] = ready;
                    else Require(values.TryGetValue(name, out var state) && ValidOptionalResult(state),
                        values.GetValueOrDefault(name)?.UnavailableReason ?? "TECHNICAL_READINESS_MISMATCH");
                }
            var context = new Dictionary<string, FeatureState>(StringComparer.Ordinal);
            var contextState = readiness.OptionalFeatures[OptionalFeature.MarketContext];
            var contextNames = new[] { "ema20", "ema50", "atr14", "atrPercent" };
            if (contextState.Availability == Availability.AVAILABLE)
            {
                var computed = ScreenerTechnicalArithmetic.Calculate(benchmarkBars.Values.OrderBy(b => b.SessionDate).ToArray(),
                    new Dictionary<DateOnly, DailyBar>(), null, true, null, null, ct, contextNames.ToHashSet(StringComparer.Ordinal));
                foreach (var name in contextNames) context[name] = computed[name];
                Require(context.Values.All(ValidOptionalResult), "TECHNICAL_READINESS_MISMATCH");
            }
            else foreach (var name in contextNames) context[name] = contextState;
            Require(setup.Evaluated);
            return new(request, true, setup, values, context, ScreenerFeatures.Trend(stockBars[^1].Close, values),
                ScreenerFeatures.Trend(contextState.Availability == Availability.AVAILABLE ? benchmarkBars[request.EvaluationDate].Close : null, context),
                context["atrPercent"].Value is { } percent ? percent >= 2m ? "ELEVATED" : "NORMAL" : "UNKNOWN",
                ScreenerEvidenceReasons.Canonical(readiness.Diagnostics.Concat(setup.Reasons).Concat(values.Values.Concat(context.Values)
                    .Where(f => f.UnavailableReason is not null).Select(f => f.UnavailableReason!))), stock, benchmark, retained,
                ScreenerReferences.Hash(new { request, stock, benchmark, retained,
                    history = days.Select(d => new { d.Request.EvaluationDate, d.MarketEligibility, d.DataReady, d.CanEvaluateSetup }) }, ct));
        }
        catch (EvidenceBindingException e) { return NotEvaluated(readiness, e.Reason); }
        catch (ArgumentException) { return NotEvaluated(readiness, "TECHNICAL_REPLAY_BINDING_MISMATCH"); }
    }

    private static bool ValidOptionalResult(FeatureState state) =>
        state is { Availability: Availability.AVAILABLE, Value: not null }
            or { Availability: Availability.UNAVAILABLE, UnavailableReason: "NUMERIC_OUT_OF_RANGE" };

    private static void Require(bool valid, string reason = "TECHNICAL_REPLAY_BINDING_MISMATCH")
    { if (!valid) throw new EvidenceBindingException(reason); }
    private static DailyBar Bar(ScreenerReadinessBarBinding binding, ScreenerEvidenceReadinessResult readiness,
        Dictionary<Guid, ScreenerEvidenceProvenance> references)
    {
        var p = binding.Price; var e = binding.Evidence; var source = binding.ObservationSource; var request = readiness.Request;
        Require(readiness.Bars.Contains(binding) && references.TryGetValue(e.EvidenceId, out var selected) && selected == e
            && e.PayloadVersion == 1 && e.EvidenceClass == EvidenceClass.SessionFact
            && e.ScopeKind == ScreenerScopeKind.INSTRUMENT && e.Scope.From == binding.Date && e.Scope.To == binding.Date
            && binding.Date >= request.HistoryFrom && binding.Date <= request.EvaluationDate
            && p.SessionId == request.SessionId && source is not null && source.SourceId == e.SourceId
            && ScreenerReferences.IsHash(source.ContentSha256) && source.RawArtifactId == e.RawArtifactId && source.FetchedAt == e.Chronology.RetrievedAt
            && source.AvailableAt == e.Chronology.KnownAt && source.AvailableAt <= request.Cutoff
            && ScreenerEvidenceValidity.Visible(e.Chronology, request.Cutoff)
            && references.TryGetValue(p.ConventionEvidenceId, out var convention) && convention.ExchangeId == e.ExchangeId
            && convention.EvidenceClass == EvidenceClass.SourceConvention
            && references.TryGetValue(p.CompletedSessionEvidenceId, out var session) && session.ExchangeId == e.ExchangeId
            && session.Scope.From == binding.Date && session.EvidenceClass == EvidenceClass.SessionFact
            && p.SyntheticOrCarryForward == EvidenceMarker.NO && p.Placeholder == EvidenceMarker.NO && p.Substituted == EvidenceMarker.NO
            && p.BarContentHash == ScreenerEvidenceBinding.BarHash(p.Bar)
            && e.PayloadHash == ScreenerEvidenceJson.Sha256(ScreenerEvidenceBinding.Encode(EvidenceClaim.GenuinePriceObservation,
                new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, p))));
        var bar = p.Bar;
        return new(new(binding.SubjectId), binding.Date, bar.Open.Value, bar.High.Value, bar.Low.Value, bar.Close.Value,
            bar.Volume, source!, bar.AdjustedClose?.Value, bar.VolumeUnit, bar.VolumeBasis, bar.MarketSegment);
    }
}
