using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record ScreenerPriceField(string SourceId, string Endpoint, string Field, string Version)
{
    public bool Matches(ConventionValue value) => value.PriceSourceId == SourceId && value.Endpoint == Endpoint
        && value.Field == Field && value.Version == Version;
}
public sealed record ScreenerReadinessBenchmark(Guid SubjectId, ScreenerPriceField PriceField);
public sealed record ScreenerReadinessRequest(Guid SubjectId, string SessionId, DateOnly EvaluationDate,
    DateOnly HistoryFrom, DateTimeOffset Cutoff, ScreenerPriceField PriceField,
    ScreenerReadinessBenchmark? Benchmark = null, string PolicyId = ScreenerEvidenceV02.PolicyId,
    int SchemaVersion = ScreenerEvidenceV02.SchemaVersion)
{
    public const int MaximumCalendarDays = 330;
    public IEnumerable<DateOnly> Dates => Enumerable.Range(0, EvaluationDate.DayNumber - HistoryFrom.DayNumber + 1).Select(HistoryFrom.AddDays);
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(PriceField);
        if (Benchmark is not null) ArgumentNullException.ThrowIfNull(Benchmark.PriceField);
        if (SubjectId == Guid.Empty || string.IsNullOrWhiteSpace(SessionId) || SessionId.Length > 200
            || SessionId != SessionId.Trim() || SessionId.Any(char.IsControl)
            || HistoryFrom > EvaluationDate || EvaluationDate.DayNumber - HistoryFrom.DayNumber >= MaximumCalendarDays
            || !ScreenerEvidenceV02.Supports(PolicyId, SchemaVersion)
            || Benchmark is { } b && (b.SubjectId == Guid.Empty || b.SubjectId == SubjectId))
            throw new ArgumentException("Invalid V0.2 readiness request.");
        foreach (var field in new[] { PriceField, Benchmark?.PriceField }.Where(f => f is not null))
            if (new[] { field!.SourceId, field.Endpoint, field.Field, field.Version }.Any(s => string.IsNullOrWhiteSpace(s)
                || s.Length > 200 || s != s.Trim() || s.Any(char.IsControl)))
                throw new ArgumentException("An exact retained price-field binding is required.");
    }
}
public sealed record ScreenerReadinessBarBinding(Guid SubjectId, DateOnly Date, PriceValue Price,
    ScreenerEvidenceProvenance Evidence, SourceReference? ObservationSource = null);
public sealed record ScreenerEvidenceReadinessResult(ScreenerReadinessRequest Request, MarketEligibilityResult MarketEligibility,
    TradingStatusResult TradingStatus, PriceComparabilityResult PriceComparability, PriceComparabilityResult HistoryComparability,
    IReadOnlyDictionary<TechnicalFeature, FeatureState> CoreFeatures,
    IReadOnlyDictionary<OptionalFeature, FeatureState> OptionalFeatures, bool DataReady, bool CanEvaluateSetup,
    IReadOnlyList<string> Diagnostics, IReadOnlyList<ScreenerEvidenceProvenance> References,
    IReadOnlyList<ScreenerReadinessBarBinding> Bars, IReadOnlyList<ScreenerReadinessBarBinding> ActiveBars, string? FailureReason = null);

// Composition only: no feature values, signals or TECHNICAL_EVALUATED state are produced here.
public static class ScreenerEvidenceReadiness
{
    public static readonly EvidenceClaim[] MarketClaims = [EvidenceClaim.StableIdentity, EvidenceClaim.SecurityType,
        EvidenceClaim.ListingCoverage, EvidenceClaim.Delisting, EvidenceClaim.BoardRegime, EvidenceClaim.BoardChange,
        EvidenceClaim.ExchangeRuleVersion, EvidenceClaim.MechanismException, EvidenceClaim.Suspension,
        EvidenceClaim.Reopening, EvidenceClaim.TradingStatus, EvidenceClaim.CompletedSession];
    public static readonly EvidenceClaim[] HistoryClaims = [EvidenceClaim.StableIdentity, EvidenceClaim.Currency,
        EvidenceClaim.ScheduledSession, EvidenceClaim.CompletedSession, EvidenceClaim.SourcePriceConvention,
        EvidenceClaim.CorporateAction, EvidenceClaim.GenuinePriceObservation];
    private static readonly TechnicalFeature[] Core = [TechnicalFeature.Ema20, TechnicalFeature.Ema50,
        TechnicalFeature.Atr14, TechnicalFeature.PriorHigh20, TechnicalFeature.PriorLow20, TechnicalFeature.DistanceToHighPercent];

    public static ScreenerEvidenceReadinessResult Compose(ScreenerReadinessRequest request,
        IReadOnlyList<ScreenerEvidenceAsOfResult> reads, IReadOnlyDictionary<Guid, ConventionValue> exactConventions, string? failure = null)
    {
        request.Validate();
        if (reads.Any(r => r.Request.Cutoff != request.Cutoff || r.Request.ScopeKind != ScreenerScopeKind.INSTRUMENT
            || r.Request.EvaluationDate < request.HistoryFrom || r.Request.EvaluationDate > request.EvaluationDate
            || r.Request.SubjectId != request.SubjectId && r.Request.SubjectId != request.Benchmark?.SubjectId)
            || reads.GroupBy(r => (r.Request.SubjectId, r.Request.Claim, r.Request.EvaluationDate)).Any(g => g.Count() != 1))
            throw new ArgumentException("Readiness requires one consistent request boundary.");
        var references = reads.SelectMany(r => r.References).GroupBy(r => r.EvidenceId)
            .Select(g => g.First()).OrderBy(r => r.EvidenceId.ToString("D"), StringComparer.Ordinal).ToArray();
        if (reads.SelectMany(r => r.References).GroupBy(r => r.EvidenceId).Any(g => g.Any(r => r != g.First())))
            throw new ArgumentException("One retained identity cannot describe different evidence states.");
        // Count unbound rows and diagnostic-only inputs as well as replay references.
        var ids = references.Select(r => r.EvidenceId).Concat(reads.SelectMany(r => r.ExaminedEvidenceIds ?? [])).Concat(reads.SelectMany(r => r.Diagnostics)
            .Where(d => d.EvidenceId.HasValue).Select(d => d.EvidenceId!.Value)).Distinct().ToArray();
        if (ids.Length > ScreenerEvidenceAsOf.MaximumRecords) failure = ScreenerEvidenceAsOf.BoundExceeded;
        var lookup = reads.ToDictionary(r => (r.Request.SubjectId, r.Request.Claim, r.Request.EvaluationDate));
        ScreenerEvidenceAsOfResult Read(Guid subject, EvidenceClaim claim, DateOnly date) =>
            lookup.GetValueOrDefault((subject, claim, date)) ?? ScreenerEvidenceAsOf.Resolve(new(subject, claim, date, request.Cutoff), []);
        var target = MarketClaims.ToDictionary(c => c, c => Read(request.SubjectId, c, request.EvaluationDate));
        var sessionStatus = target[EvidenceClaim.TradingStatus] with { Facts = target[EvidenceClaim.TradingStatus].Facts
            .Where(f => f.Value is TradingStatusValue s && s.SessionId == request.SessionId
                || f.Candidates.Any(c => c.Value is TradingStatusValue v && v.SessionId == request.SessionId)).ToArray() };
        var status = ScreenerEvidenceAsOf.TradingStatus(target[EvidenceClaim.Suspension], target[EvidenceClaim.Reopening], sessionStatus);
        // An incomplete suspension cannot positively exclude a subject.
        if (status.Quality != EvidenceQuality.Verified && status.Status == TradingStatus.Suspended)
            status = new(TradingStatus.Unknown, status.Quality, status.Reasons);
        var board = Combine(target[EvidenceClaim.BoardRegime], target[EvidenceClaim.BoardChange], v => v switch
        { BoardValue b => b, BoardChangeValue b => b.ToBoard, _ => null });
        var mechanism = Combine(target[EvidenceClaim.ExchangeRuleVersion], target[EvidenceClaim.MechanismException], v => v switch
        { RuleValue r => r.Mechanism, ExceptionValue e => e.Mechanism, _ => (EvidenceMechanism?)null });
        var identity = Single<IdentityValue>(target[EvidenceClaim.StableIdentity]);
        var type = Single<SecurityTypeValue>(target[EvidenceClaim.SecurityType]);
        var listing = Single<ListingValue>(target[EvidenceClaim.ListingCoverage]);
        var delisting = Single<DelistingValue>(target[EvidenceClaim.Delisting]);
        var session = Session(target[EvidenceClaim.CompletedSession], request.SessionId);
        var facts = new MarketEvidenceFacts(identity is not null, target[EvidenceClaim.StableIdentity].Quality == EvidenceQuality.Conflicting,
            type is not null, type is { Classification: not EvidenceSecurityType.ORDINARY }, listing is not null,
            listing is not null && listing.ListingDate > request.EvaluationDate,
            delisting is not null && delisting.DelistingDate <= request.EvaluationDate,
            board.Quality == EvidenceQuality.Verified, board.Quality == EvidenceQuality.Verified
                && board.Value?.BoardCode is not (EvidenceBoard.MAIN or EvidenceBoard.DEVELOPMENT)
                || mechanism.Quality == EvidenceQuality.Verified && mechanism.Value != EvidenceMechanism.CONTINUOUS,
            board.Quality == EvidenceQuality.Conflicting, mechanism.Quality == EvidenceQuality.Verified,
            mechanism.Quality != EvidenceQuality.Verified, status, session);
        var marketFailure = target.Values.Any(r => r.FailureReason is not null);
        var market = failure is not null || marketFailure
            ? new MarketEligibilityResult(EligibilityStatus.DataBlocked, [failure ?? ScreenerEvidenceAsOf.InputUnavailable])
            : ScreenerMarketEligibilityEvaluator.Evaluate(facts);
        var stock = History(request.SubjectId, request.PriceField, request, Read, exactConventions);
        var benchmark = request.Benchmark is { } bm ? History(bm.SubjectId, bm.PriceField, request, Read, exactConventions) : null;
        var comparability = failure is null ? stock.Active : new PriceComparabilityResult(PriceComparability.Unresolved, [failure]);
        var core = Core.ToDictionary(f => f, f => ScreenerTechnicalReadiness.Evaluate(f, new(comparability.State, stock.Count)));
        var alignedCount = benchmark is null ? 0 : stock.ActiveDates.Reverse().Zip(benchmark.ActiveDates.Reverse())
            .TakeWhile(pair => pair.First == pair.Second).Count();
        var benchmarkReady = benchmark is not null && benchmark.Active.State == PriceComparability.Cleared && benchmark.Count > 0;
        var optional = new Dictionary<OptionalFeature, FeatureState>();
        foreach (var feature in Enum.GetValues<OptionalFeature>())
        {
            var required = feature == OptionalFeature.Rs60 ? 61 : 21;
            if (feature is OptionalFeature.Rs20 or OptionalFeature.Rs60)
                optional[feature] = ScreenerTechnicalReadiness.Evaluate(feature == OptionalFeature.Rs20 ? TechnicalFeature.Rs20 : TechnicalFeature.Rs60,
                    new(comparability.State, stock.Count, benchmarkReady && alignedCount > 0, alignedCount));
            else optional[feature] = ScreenerOptionalFeatures.Evaluate(feature, new(stock.QuantityComparable,
                stock.QuantityComparable && stock.MonetaryCompatible, false, benchmarkReady,
                feature == OptionalFeature.MarketContext ? benchmarkReady && new[] { TechnicalFeature.Ema20, TechnicalFeature.Ema50, TechnicalFeature.Atr14 }
                    .All(f => ScreenerTechnicalReadiness.Evaluate(f, new(benchmark!.Active.State, benchmark.Count)).Availability == Availability.AVAILABLE)
                    : benchmark?.Count >= required,
                stock.Count >= required && (feature != OptionalFeature.RelativeVolume || stock.Bars.TakeLast(21).All(b => b.Price.Bar.Volume > 0))));
        }
        var readiness = ScreenerDataReadiness.Evaluate(market, comparability, core.Values.ToArray());
        var diagnostics = ScreenerEvidenceReasons.Canonical(readiness.Reasons.Concat(market.Reasons).Concat(stock.Reasons)
            .Concat(optional.Values.Where(v => v.UnavailableReason is not null).Select(v => v.UnavailableReason!))
            .Concat(reads.SelectMany(r => r.Reasons)).Concat(failure is null ? [] : new[] { failure }));
        return new(request, market, status, comparability, stock.Whole, core, optional, readiness.DataReady,
            readiness.CanEvaluateSetup, diagnostics, references, stock.Bars.Concat(benchmark?.Bars ?? []).OrderBy(b => b.SubjectId)
                .ThenBy(b => b.Date).ToArray(), stock.Bars.Where(b => stock.ActiveDates.Contains(b.Date))
                .Concat(benchmark?.Bars.Where(b => benchmark.ActiveDates.Contains(b.Date)) ?? [])
                .OrderBy(b => b.SubjectId).ThenBy(b => b.Date).ToArray(), failure);
    }

    private static T? Single<T>(ScreenerEvidenceAsOfResult read) where T : ScreenerEvidenceValue =>
        read.FailureReason is null && read.Facts.Count == 1 && read.Facts[0].Quality == EvidenceQuality.Verified ? read.Facts[0].Value as T : null;
    private static bool Session(ScreenerEvidenceAsOfResult read, string session) => read.FailureReason is null
        && read.Facts.Any(f => f.Quality == EvidenceQuality.Verified && f.Value is CompletedSessionValue
            { Completion: EvidenceCompletion.COMPLETED } s && s.SessionId == session);

    public static (T? Value, EvidenceQuality Quality) Combine<T>(ScreenerEvidenceAsOfResult first,
        ScreenerEvidenceAsOfResult second, Func<ScreenerEvidenceValue, T?> project)
    {
        var all = new[] { first, second }.SelectMany(r => r.Facts).Where(f => f.Selected is not null || f.Quality == EvidenceQuality.Conflicting).ToArray();
        if (all.Length == 0) return (default, EvidenceQuality.Unknown);
        var candidates = all.SelectMany(f => f.Selected is { } p ? new[] { new ScreenerEvidenceAsOfCandidate(f.Value!, f.Quality, p) } : f.Candidates).ToArray();
        var tier = candidates.Min(c => c.Provenance.Authority);
        var specificity = candidates.Where(c => c.Provenance.Authority == tier).Max(c => ScreenerEvidenceBinding.Specificity(c.Provenance.ScopeKind));
        var preferred = candidates.Where(c => c.Provenance.Authority == tier && ScreenerEvidenceBinding.Specificity(c.Provenance.ScopeKind) == specificity).ToArray();
        if (all.Any(f => f.Quality == EvidenceQuality.Conflicting && f.Candidates.Any(c => preferred.Contains(c))))
            return (default, EvidenceQuality.Conflicting);
        var resolution = ScreenerEvidenceResolver.Resolve(preferred.Select(c => new EvidenceCandidate<T?>(project(c.Value), c.Provenance.Scope,
            c.Provenance.Authority, c.Provenance.Chronology, c.Provenance.RevisionNumber,
            ScreenerEvidenceBinding.Specificity(c.Provenance.ScopeKind), c.Provenance.RevisionSeriesId, c.Provenance.SupersedesRevisionNumber)),
            first.Request.EvaluationDate, first.Request.Cutoff);
        return (resolution.Selected, resolution.Conflicting ? EvidenceQuality.Conflicting
            : preferred.Any(c => c.Quality != EvidenceQuality.Verified) ? EvidenceQuality.Partial : EvidenceQuality.Verified);
    }

    private sealed record HistoryResult(PriceComparabilityResult Active, PriceComparabilityResult Whole, int Count,
        IReadOnlyList<DateOnly> ActiveDates, IReadOnlyList<ScreenerReadinessBarBinding> Bars,
        bool QuantityComparable, bool MonetaryCompatible, IReadOnlyList<string> Reasons);
    private static HistoryResult History(Guid subject, ScreenerPriceField field, ScreenerReadinessRequest request,
        Func<Guid, EvidenceClaim, DateOnly, ScreenerEvidenceAsOfResult> read, IReadOnlyDictionary<Guid, ConventionValue> exactConventions)
    {
        var bars = new List<ScreenerReadinessBarBinding>(); var dates = new List<DateOnly>(); var reasons = new List<string>();
        var wholeBreaks = new List<CorporateActionKind>();
        var active = new PriceComparabilityResult(PriceComparability.Unresolved, [ScreenerEvidenceReasons.PriceUnresolved]);
        (string Code, Guid Exchange, string Currency)? previous = null;
        var wholeClear = true;
        foreach (var day in request.Dates)
        {
            var inputs = HistoryClaims.ToDictionary(c => c, c => read(subject, c, day));
            var actions = inputs[EvidenceClaim.CorporateAction];
            var events = actions.Facts.Where(f => f.Quality == EvidenceQuality.Verified).Select(f => f.Value).OfType<ActionEventValue>()
                .Select(e => (CorporateActionKind)(int)e.ActionType).ToArray();
            wholeBreaks.AddRange(events);
            var closed = inputs[EvidenceClaim.ScheduledSession].Facts.Any(f => f.Quality == EvidenceQuality.Verified
                && f.Value is ScheduledSessionValue { Schedule: EvidenceSchedule.CLOSED } s && s.SessionId == request.SessionId);
            var completed = Session(inputs[EvidenceClaim.CompletedSession], request.SessionId);
            var scheduledOpen = inputs[EvidenceClaim.ScheduledSession].Facts.Any(f => f.Quality == EvidenceQuality.Verified
                && f.Value is ScheduledSessionValue { Schedule: EvidenceSchedule.OPEN } s && s.SessionId == request.SessionId);
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var identity = Single<IdentityValue>(inputs[EvidenceClaim.StableIdentity]);
            var currency = Single<CurrencyValue>(inputs[EvidenceClaim.Currency]);
            var coverage = actions.Facts.Any(f => f.Quality == EvidenceQuality.Verified
                && f.Value is ActionCoverageValue { Coverage: EvidenceCoverage.COMPLETE })
                && !actions.Facts.Any(f => f.Quality == EvidenceQuality.Conflicting
                    || f.Value is ActionCoverageValue && f.Quality != EvidenceQuality.Verified
                    || f.Value is ActionCoverageValue { Coverage: EvidenceCoverage.PARTIAL });
            if (day != request.EvaluationDate && (closed && !completed || weekend && !completed && !scheduledOpen
                && inputs[EvidenceClaim.ScheduledSession].Quality != EvidenceQuality.Conflicting))
            {
                if (events.Length > 0 || !coverage || identity is null || currency is null
                    || previous is { } prior && (prior.Exchange != inputs[EvidenceClaim.StableIdentity].Facts[0].Selected!.ExchangeId
                        || prior.Currency != currency.CurrencyCode))
                {
                    wholeClear = false; dates.Clear(); previous = null;
                    reasons.Add(events.Length > 0 ? ScreenerEvidenceReasons.PriceKnownBreak : ScreenerEvidenceReasons.PriceUnresolved);
                }
                continue;
            }
            var prices = inputs[EvidenceClaim.GenuinePriceObservation].Facts.Where(f => f.Quality == EvidenceQuality.Verified && f.Value is PriceValue p
                && p.SessionId == request.SessionId && f.Selected is { } provenance && provenance.SourceId == field.SourceId).ToArray();
            // Match the selected observation's exact convention, not the newest domain convention.
            var selected = prices.Where(f => f.Value is PriceValue p && readsConvention(p)).ToArray();
            bool readsConvention(PriceValue price)
            {
                return exactConventions.TryGetValue(price.ConventionEvidenceId, out var cv)
                    && field.Matches(cv) && cv.Continuity == EvidenceContinuity.RAW_AS_TRADED
                    && cv.PriceKind == (subject == request.SubjectId ? EvidencePriceKind.STOCK_RAW : EvidencePriceKind.INDEX_LEVEL);
            }
            var binding = identity is not null && currency is not null ? (identity.SourceCode,
                inputs[EvidenceClaim.StableIdentity].Facts[0].Selected!.ExchangeId, currency.CurrencyCode) : ((string, Guid, string)?)null;
            active = ScreenerPriceComparabilityEvaluator.Evaluate(new(binding is not null && (previous is null || previous.Value.Exchange == binding.Value.Item2),
                binding is not null && (previous is null || previous.Value.Currency == binding.Value.Item3),
                selected.Length == 1, completed && !closed, coverage && actions.FailureReason is null,
                selected.Length == 1 && inputs.Values.All(r => r.FailureReason is null), events));
            if (active.State != PriceComparability.Cleared)
            { wholeClear = false; dates.Clear(); previous = null; reasons.AddRange(active.Reasons); continue; }
            var priceFact = selected[0]; var value = (PriceValue)priceFact.Value!;
            bars.Add(new(subject, day, value, priceFact.Selected!, priceFact.ObservationSource)); dates.Add(day); previous = binding;
        }
        var activeBars = bars.Where(b => dates.Contains(b.Date)).TakeLast(21).ToArray();
        var quantity = activeBars.Length > 0 && activeBars.All(b => b.Price.Bar.VolumeUnit != "UNKNOWN" && b.Price.Bar.VolumeBasis == "RAW_AS_TRADED"
            && b.Price.Bar.VolumeUnit is "SHARES" or "SHARE_COUNT_CORROBORATED"
            && b.Price.Bar.MarketSegment != "UNKNOWN" && b.Price.Bar.VolumeUnit == activeBars[^1].Price.Bar.VolumeUnit
            && b.Price.Bar.VolumeBasis == activeBars[^1].Price.Bar.VolumeBasis && b.Price.Bar.MarketSegment == activeBars[^1].Price.Bar.MarketSegment);
        var whole = wholeBreaks.Count > 0 ? ScreenerPriceComparabilityEvaluator.Evaluate(new(false, false, false, false, false, false, wholeBreaks))
            : wholeClear ? active : new PriceComparabilityResult(PriceComparability.Unresolved, reasons);
        return new(active, whole, dates.Count, dates, bars, quantity,
            quantity && previous?.Currency == "IDR", reasons);
    }
}
