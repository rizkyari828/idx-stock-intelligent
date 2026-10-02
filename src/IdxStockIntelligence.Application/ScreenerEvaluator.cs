using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public static class ScreenerEvaluator
{
    private sealed record DayFacts(ScreenerIdentityInterval? Identity, string? TradingStatus, bool? NoTrade,
        DailyBar? Bar, IReadOnlyList<string> Reasons);

    public static ScreenerResult Evaluate(ScreenerReadRequest request, ScreenerDatabaseEvidence database,
        SelectedScreenerReferences references, ScreenerPortfolioHistory? portfolio, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.BoundsReason() is { } bound) throw new ArgumentException(bound);
        if (database.Bars.Count > ScreenerReadRequest.MaximumRows) throw new ArgumentException("BAR_BOUND_EXCEEDED");
        var doc = new ScreenerReferenceDocument(1, references.Universes, references.Instruments);
        var universe = ScreenerReferences.Universe(doc, request.Cutoff);
        if (universe.Available && (universe.Value!.MemberIds.Count > 10 || universe.Value.MemberIds.Distinct().Count() != universe.Value.MemberIds.Count
            || universe.Value.MemberIds.Contains(request.BenchmarkId) || universe.Value.BenchmarkId != request.BenchmarkId))
            throw new ArgumentException("UNIVERSE_BOUND_OR_IDENTITY_INVALID");
        var configured = universe.Value?.MemberIds.ToHashSet() ?? [];
        var held = new HashSet<Guid>();
        var mandates = new Dictionary<Guid, Mandate>();
        if (portfolio is not null)
        {
            if (portfolio.Portfolio.CreatedAt > request.Cutoff) throw new KeyNotFoundException("Portfolio not known at cutoff.");
            if (portfolio.Events.Count > PortfolioLedger.MaximumEvents || portfolio.Theses.Count > PortfolioExchange.MaxTheses)
                throw new ArgumentException("PORTFOLIO_HISTORY_BOUND_EXCEEDED");
            foreach (var item in portfolio.Events)
            { ct.ThrowIfCancellationRequested(); if (item.PortfolioId != portfolio.Portfolio.Id) throw new ArgumentException("Portfolio history identity mismatch."); }
            var projection = PortfolioLedger.Project(portfolio.Events, request.Cutoff, request.Through, portfolio.Portfolio.AllowNegativeCash);
            held.UnionWith(projection.Positions.Where(p => p.Shares > 0).Select(p => p.InstrumentId));
            foreach (var thesis in portfolio.Theses)
            { ct.ThrowIfCancellationRequested(); if (thesis.PortfolioId != portfolio.Portfolio.Id) throw new ArgumentException("Thesis identity mismatch."); }
            foreach (var group in portfolio.Theses.Where(t => t.KnownAt <= request.Cutoff && held.Contains(t.InstrumentId)).GroupBy(t => t.InstrumentId))
            {
                ct.ThrowIfCancellationRequested();
                var latest = group.OrderByDescending(t => t.Version).First();
                if (latest.Active) mandates[group.Key] = latest.Mandate;
            }
        }
        var ids = configured.Concat(held).Distinct().ToArray();
        if (ids.Append(request.BenchmarkId).Distinct().Count() > ScreenerReadRequest.MaximumIds)
            throw new ArgumentException("INSTRUMENT_BOUND_EXCEEDED");
        var calendar = new Dictionary<DateOnly, ScreenerSessionEvidence>();
        var sessionGroups = references.Sessions.GroupBy(s => s.Date).ToDictionary(g => g.Key, g => g.ToArray());
        var statusGroups = references.InstrumentSessions.GroupBy(s => (s.Instrument.Value, s.Date)).ToDictionary(g => g.Key, g => g.ToArray());
        DateOnly? target = null;
        DateOnly? lastUnclosed = null;
        for (var date = request.HistoryAnchor; date <= request.Through; date = date.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();
            var day = ScreenerSessions.Resolve(sessionGroups.GetValueOrDefault(date) ?? [], date, request.Through, request.Cutoff);
            calendar[date] = day;
            if (Closed(day.Status)) continue;
            lastUnclosed = date;
            if (day.Status == ExchangeDayStatus.ObservedTrading && day.Reason is null) target = date;
        }
        var calendarKnown = target is not null && lastUnclosed == target;
        // M2 already selects revisions. Reject duplicate dates rather than inventing a second selection/fallback rule.
        var visible = database.Bars.Where(b => b.SessionDate >= request.HistoryAnchor && b.SessionDate <= request.Through
            && b.KnownAt <= request.Cutoff && b.FetchedAt <= request.Cutoff
            && (b.RetrievedAt is null || b.RetrievedAt <= request.Cutoff) && (b.SessionKnownAt is null || b.SessionKnownAt <= request.Cutoff)).ToArray();
        if (visible.GroupBy(b => (b.InstrumentId, b.SessionDate)).Any(g => g.Count() > 1))
            throw new ArgumentException("Expected one selected revision per date.");
        var grouped = visible.GroupBy(b => b.InstrumentId).ToDictionary(g => g.Key, g => g.ToDictionary(b => b.SessionDate));
        var benchmarkReference = ScreenerReferences.Instrument(doc, request.BenchmarkId, request.Cutoff);
        var benchmarkBars = grouped.GetValueOrDefault(request.BenchmarkId) ?? [];
        var benchmark = new Dictionary<DateOnly, DailyBar>();
        var indexSeries = new List<DailyBar>();
        ScreenerBarEvidence? lastIndex = null;
        var indexReason = "BENCHMARK_MISSING";
        foreach (var (date, session) in calendar)
        {
            ct.ThrowIfCancellationRequested();
            if (Closed(session.Status)) continue;
            benchmarkBars.TryGetValue(date, out var raw);
            var validated = raw?.Validate();
            var identity = benchmarkReference.Available ? ScreenerReferences.Identity(benchmarkReference.Value!, date) : null;
            if (session.Status == ExchangeDayStatus.ObservedTrading && session.Reason is null && raw is not null && validated!.Available
                && identity?.Value?.Classification == "INDEX" && ScreenerReferences.Price(benchmarkReference.Value!, raw).Available)
            {
                var bar = validated.Value!.Bar;
                benchmark[date] = bar; indexSeries.Add(bar); lastIndex = raw; indexReason = "";
            }
            else
            {
                indexSeries.Clear();
                indexReason = session.Reason is not null || session.Status == ExchangeDayStatus.Unknown ? "SESSION_UNCONFIRMED"
                    : !benchmarkReference.Available || identity?.Value?.Classification != "INDEX" ? "REFERENCE_NOT_KNOWN"
                    : validated is { Available: false } ? validated.Reason! : raw is null ? "BENCHMARK_MISSING" : "PRICE_BASIS_UNVERIFIED";
            }
        }
        var indexFields = ScreenerFeatures.Calculate(indexSeries, benchmark, benchmarkBars.Values.ToArray(), benchmarkReference.Value,
            calendarKnown && indexSeries.Count > 0 ? null : indexReason, true, ct);
        var indexApplicable = new[] { "ema20", "ema50", "atr14", "atrPercent" };
        var contextFields = indexApplicable.ToDictionary(n => n, n => indexFields[n]);
        contextFields["close"] = calendarKnown && indexSeries.Count > 0
            ? new(Availability.AVAILABLE, indexSeries[^1].Close, null) : new(Availability.UNAVAILABLE, null, indexReason);
        var contextReasons = contextFields.Values.Where(f => f.Availability != Availability.AVAILABLE)
            .Select(f => f.UnavailableReason!).Distinct().Order(StringComparer.Ordinal).ToArray();
        var context = new ScreenerMarketContext(request.BenchmarkId, lastIndex?.SessionDate,
            ScreenerFeatures.Trend(contextFields["close"].Value, contextFields), contextFields["atrPercent"].Value is { } percent
                ? percent >= 2m ? "ELEVATED" : "NORMAL" : "UNKNOWN", contextFields, contextReasons,
            Provenance(lastIndex, indexSeries, benchmarkReference.Value, lastUnclosed is { } indexDate ? benchmarkBars.GetValueOrDefault(indexDate) : null));
        var rows = new List<ScreenerRow>();
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var reference = ScreenerReferences.Instrument(doc, id, request.Cutoff);
            var listings = database.Listings.Where(l => l.InstrumentId == id && l.KnownAt <= request.Cutoff).ToArray();
            var listing = listings.Length == 1 ? listings[0].Resolution : new EvidenceResult<InstrumentBoundaryEvidence>(null,
                listings.Length == 0 ? "LISTING_UNKNOWN" : "LISTING_CONFLICT");
            var bars = grouped.GetValueOrDefault(id) ?? [];
            var series = new List<DailyBar>();
            var setup = ScreenerSetup.Empty;
            var eligibility = ScreenerEligibility.Decide(["SESSION_UNCONFIRMED"]);
            DayFacts? current = null;
            ScreenerBarEvidence? lastObserved = null;
            decimal? lastClose = null;
            DateOnly? lastUsable = null;
            foreach (var (date, session) in calendar)
            {
                ct.ThrowIfCancellationRequested();
                if (Closed(session.Status)) continue;
                bars.TryGetValue(date, out var raw);
                current = Facts(id, date, session, reference, listing, raw, statusGroups.GetValueOrDefault((id, date)) ?? [], request, universe.Available);
                var observed = raw?.Validate();
                if (observed?.Available == true) { lastObserved = raw; lastClose = observed.Value!.Bar.Close; }
                if (current.Reasons.Count == 0 && current.Bar is not null)
                { series.Add(current.Bar); lastUsable = date; }
                else series.Clear();
                var reasons = current.Reasons.ToList();
                var preliminary = ScreenerEligibility.Decide(reasons);
                if (preliminary.Status != EligibilityStatus.Ineligible)
                {
                    if (lastUsable is not null && lastUsable < date && session.Status == ExchangeDayStatus.ObservedTrading) reasons.Add("STALE");
                    if (series.Count < 21) reasons.Add("INSUFFICIENT_HISTORY");
                }
                eligibility = ScreenerEligibility.Decide(reasons);
                var high = series.Count >= 21 ? series.SkipLast(1).TakeLast(20).Max(b => b.High) : (decimal?)null;
                setup = ScreenerEpisodes.Advance(id, date, setup, eligibility.Status, current.Bar?.Close, high);
            }
            var currentReason = series.Count > 0 && calendarKnown ? null : FeatureReason(eligibility.Reasons, calendarKnown);
            var fields = new Dictionary<string, FeatureState>(ScreenerFeatures.Calculate(series, benchmark, bars.Values.ToArray(),
                reference.Value, currentReason, false, ct), StringComparer.Ordinal);
            fields["close"] = lastClose is null ? new(Availability.UNAVAILABLE, null, currentReason ?? "MISSING_CURRENT_BAR")
                : new(Availability.AVAILABLE, lastClose, null);
            fields["volume"] = lastObserved is null ? new(Availability.UNAVAILABLE, null, currentReason ?? "MISSING_CURRENT_BAR")
                : new(Availability.AVAILABLE, lastObserved.Volume, null);
            var trend = ScreenerFeatures.Trend(currentReason is null ? series[^1].Close : null, fields);
            bool? stale = calendarKnown && lastUsable is not null ? lastUsable < target : null;
            bool? noTrade = calendarKnown ? current?.NoTrade : null;
            if (stale is null) fields["stale"] = new(Availability.UNAVAILABLE, null, calendarKnown ? currentReason ?? "MISSING_CURRENT_BAR" : "SESSION_UNCONFIRMED");
            if (noTrade is null) fields["noTrade"] = new(Availability.UNAVAILABLE, null, !calendarKnown ? "SESSION_UNCONFIRMED"
                : current?.TradingStatus == "SUSPENSION" ? "SUSPENDED" : current?.TradingStatus == "TRADING"
                    ? currentReason ?? "MISSING_CURRENT_BAR" : "STATUS_UNKNOWN");
            if (trend == "UNKNOWN") fields["trend"] = new(Availability.UNAVAILABLE, null,
                currentReason ?? fields["ema20"].UnavailableReason ?? fields["ema50"].UnavailableReason ?? "INSUFFICIENT_SESSIONS");
            var dataReasons = eligibility.Reasons.Concat(fields.Values.Where(f => f.Availability != Availability.AVAILABLE)
                .Select(f => f.UnavailableReason!)).Distinct().Order(StringComparer.Ordinal).ToArray();
            var quality = eligibility.Status == EligibilityStatus.Ineligible ? ScreenerQuality.Complete
                : !setup.Evaluated ? ScreenerQuality.Blocked : dataReasons.Length > 0 ? ScreenerQuality.Partial : ScreenerQuality.Complete;
            rows.Add(new(id, current?.Identity?.Symbol?.ToUpperInvariant(), current?.Identity?.DisplayName,
                configured.Contains(id), held.Contains(id), mandates.TryGetValue(id, out var mandate) ? mandate : null, null,
                eligibility, setup, lastObserved?.SessionDate, lastClose, lastObserved?.Volume, stale, noTrade,
                current?.TradingStatus, trend, quality, dataReasons, fields,
                Provenance(lastObserved, series, reference.Value, lastUnclosed is { } stockDate ? bars.GetValueOrDefault(stockDate) : null)));
        }
        var result = ScreenerOrdering.Assemble(request.Through, target, request.Cutoff, universe.Value?.SnapshotId,
            universe.Available, calendarKnown, context, rows, ct);
        return !universe.Available && universe.Reason != "UNIVERSE_NOT_KNOWN"
            ? result with { Reasons = result.Reasons.Append(universe.Reason!).ToArray() } : result;
    }

    private static bool Closed(ExchangeDayStatus status) => status is ExchangeDayStatus.Weekend
        or ExchangeDayStatus.AnnouncedClosed or ExchangeDayStatus.ExceptionalClosure;
    private static ScreenerProvenance Provenance(ScreenerBarEvidence? raw, List<DailyBar> series, InstrumentSnapshot? reference,
        ScreenerBarEvidence? current) =>
        new(raw, series.Count > 0 ? series[0].SessionDate : null, series.Count >= 20 ? series[0].SessionDate : null,
            series.Count >= 15 ? series[0].SessionDate : null, series.Count, reference?.SnapshotId) { CurrentEvidence = current };

    private static readonly string[] FeatureReasons = ["IDENTITY_CONFLICT", "REFERENCE_NOT_KNOWN", "TYPE_UNKNOWN",
        "CANONICAL_INVALID", "NUMERIC_OUT_OF_RANGE", "MISSING_CURRENT_BAR", "INSUFFICIENT_HISTORY", "PRICE_BASIS_UNVERIFIED"];
    private static string FeatureReason(IReadOnlyList<string> reasons, bool calendarKnown) =>
        FeatureReasons.FirstOrDefault(reasons.Contains) ?? (!calendarKnown ? "SESSION_UNCONFIRMED" : reasons.Count > 0 ? reasons[0] : "MISSING_CURRENT_BAR");

    private static DayFacts Facts(Guid id, DateOnly date, ScreenerSessionEvidence session,
        EvidenceResult<InstrumentSnapshot> reference, EvidenceResult<InstrumentBoundaryEvidence> listing, ScreenerBarEvidence? raw,
        IReadOnlyList<InstrumentSessionProof> statuses, ScreenerReadRequest request, bool universeKnown)
    {
        var reasons = new List<string>();
        if (!universeKnown) reasons.Add("REFERENCE_NOT_KNOWN");
        var identity = reference.Available ? ScreenerReferences.Identity(reference.Value!, date) : null;
        if (!reference.Available || identity?.Available != true)
        {
            reasons.Add((reference.Reason ?? identity?.Reason ?? "").Contains("CONFLICT", StringComparison.Ordinal) ? "IDENTITY_CONFLICT" : "REFERENCE_NOT_KNOWN");
            if ((reference.Reason ?? identity?.Reason) is { } referenceReason) reasons.Add(referenceReason);
        }
        var fact = identity?.Value;
        if (fact?.Classification == "UNKNOWN") reasons.Add("TYPE_UNKNOWN");
        else if (fact is not null && fact.Classification != "ORDINARY") reasons.Add("UNSUPPORTED_TYPE");
        if (fact?.Board == "UNKNOWN") reasons.Add("BOARD_UNKNOWN");
        else if (fact is not null && fact.Classification == "ORDINARY" && fact.Board is not ("MAIN" or "DEVELOPMENT")) reasons.Add("UNSUPPORTED_BOARD");
        if (listing.Reason?.Contains("CONFLICT", StringComparison.Ordinal) == true
            || listing.Value is { } boundary && boundary.InstrumentId != id) reasons.Add("IDENTITY_CONFLICT");
        else if (listing.Available && listing.Value!.Status == "VERIFIED" && listing.Value.RetrievedAt is not null)
        {
            var b = listing.Value;
            var state = InstrumentBoundaries.Classify(new(new(id), id.ToString("D"), b.ListedFrom, b.DelistedAt), date);
            if (state == InstrumentBoundaryState.PreListing) reasons.Add("PRE_LISTING");
            else if (state == InstrumentBoundaryState.PostDelisting) reasons.Add("POST_DELISTING");
            else if (state == InstrumentBoundaryState.UnknownBoundary) reasons.Add("LISTING_UNKNOWN");
        }
        else
        {
            reasons.Add("LISTING_UNKNOWN");
            if (listing.Reason is { } listingReason) reasons.Add(listingReason);
        }
        var trading = reference.Available ? ScreenerReferences.Trading(reference.Value!, date) : null;
        var status = trading?.Value?.Status;
        var instrumentSession = ScreenerSessions.ResolveInstrument(statuses, id, date, request.Through, request.Cutoff);
        var statusConflict = instrumentSession.Reason == "INSTRUMENT_SESSION_CONFLICT";
        if (instrumentSession.Available)
        {
            var explicitStatus = instrumentSession.Value!.Status == MarketSessionStatus.Suspension ? "SUSPENSION" : "NO_TRADE";
            if (status is not null && status != "UNKNOWN" && status != explicitStatus) statusConflict = true;
            status = explicitStatus;
        }
        if (statusConflict)
        {
            reasons.Add("IDENTITY_CONFLICT");
            if (instrumentSession.Reason is { } conflictReason) reasons.Add(conflictReason);
            status = null;
        }
        if (status == "SUSPENSION") reasons.Add("SUSPENDED");
        else if (status == "NO_TRADE") reasons.Add("NO_TRADE");
        else if (status != "TRADING") reasons.Add("STATUS_UNKNOWN");
        var mechanism = trading?.Value?.Mechanism;
        if (mechanism is null or "UNKNOWN") reasons.Add("STATUS_UNKNOWN");
        else if (mechanism != "CONTINUOUS") reasons.Add("UNSUPPORTED_BOARD");
        if (session.Status != ExchangeDayStatus.ObservedTrading || session.Reason is not null)
        {
            reasons.Add("SESSION_UNCONFIRMED");
            if (session.Reason is { } sessionReason) reasons.Add(sessionReason);
        }
        var validated = raw?.Validate();
        var expectedAbsence = reasons.Any(r => r is "PRE_LISTING" or "POST_DELISTING" or "SUSPENDED" or "NO_TRADE");
        if (raw is null)
        { if (!expectedAbsence) reasons.Add("MISSING_CURRENT_BAR"); }
        else if (validated?.Available != true)
        {
            reasons.Add(validated?.Reason == "NUMERIC_OUT_OF_RANGE" ? "NUMERIC_OUT_OF_RANGE" : "CANONICAL_INVALID");
            if (validated?.Reason is { } validationReason) reasons.Add(validationReason);
        }
        else if (raw.Volume <= 0 && status is not ("NO_TRADE" or "SUSPENSION"))
        { reasons.Add("CANONICAL_INVALID"); reasons.Add("ZERO_VOLUME_UNEXPLAINED"); }
        if (raw is { Volume: > 0 } && validated?.Available == true && reference.Available && fact?.Classification == "ORDINARY"
            && !ScreenerReferences.Price(reference.Value!, raw).Available) reasons.Add("PRICE_BASIS_UNVERIFIED");
        var bar = validated?.Value?.Bar;
        return new(fact, status, status == "NO_TRADE" ? true : status == "TRADING" && bar?.Volume > 0 ? false : null,
            bar, reasons.Distinct().ToArray());
    }
}
