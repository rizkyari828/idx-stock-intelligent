using System.Text.Json;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record OutcomeRequest(int HorizonSessions)
{
    public static readonly int[] Horizons = [1, 5, 10, 20];
    public static OutcomeRequest Parse(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object || body.EnumerateObject().Count() != 1
            || !body.TryGetProperty("horizonSessions", out var h) || h.ValueKind != JsonValueKind.Number || !h.TryGetInt32(out var n) || !Horizons.Contains(n))
            throw new ScreenerException(400, "OUTCOME_REQUEST_INVALID");
        return new(n);
    }
}

public sealed record OutcomeCalendarDay(DateOnly Date, string Classification, SessionProof? Proof);
public sealed record OutcomeHorizon(DateOnly? Date, string State, string? Reason,
    IReadOnlyList<OutcomeCalendarDay> Calendar, bool NonProspective = false);
public sealed record OutcomeCell(Guid InstrumentId, int HorizonSessions, string State, string? Reason,
    DateOnly? AnchorMarketDate, decimal? AnchorClose, DateOnly? HorizonMarketDate, decimal? HorizonClose,
    decimal? PriceReturnPct, bool Materialized = false, bool NewlyMaterialized = false,
    DateTimeOffset? OutcomeKnownAt = null, DateTimeOffset? RecordedAt = null)
{
    public string Resolution => Terminal ? "TERMINAL" : "UNRESOLVED";
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Terminal => State is "AVAILABLE" or "ANCHOR_UNAVAILABLE" or "DATA_UNAVAILABLE" or "BASIS_UNCERTAIN";
}
public sealed record OutcomeSummary(int Cells, int Available, int TerminalUnavailable, int Unresolved,
    int Materialized, int NewlyMaterialized);
public sealed record OutcomeResponse(Guid RunId, int? HorizonSessions, string OutcomePolicyId, int SchemaVersion,
    DateTimeOffset AssessedAt, int NewlyMaterializedCount, OutcomeSummary Summary, IReadOnlyList<OutcomeCell> Cells)
{
    public static OutcomeResponse From(Guid runId, int? horizon, DateTimeOffset at, IReadOnlyList<OutcomeCell> cells)
    {
        var ordered = cells.OrderBy(c => c.InstrumentId).ThenBy(c => c.HorizonSessions).ToArray();
        var created = ordered.Count(c => c.NewlyMaterialized);
        return new(runId, horizon, OutcomeEvaluator.PolicyId, 1, at, created,
            new(ordered.Length, ordered.Count(c => c.State == "AVAILABLE"), ordered.Count(c => c.Terminal && c.State != "AVAILABLE"),
                ordered.Count(c => !c.Terminal), ordered.Count(c => c.Materialized), created), ordered);
    }
}
public sealed record OutcomeInputs(ScreenerDatabaseEvidence CapturedDatabase, SelectedScreenerReferences CapturedReferences,
    ScreenerDatabaseEvidence ForwardDatabase, SelectedScreenerReferences ForwardReferences);
public sealed record OutcomeManifest(int SchemaVersion, string OutcomePolicyId, int CaptureSchemaVersion,
    string CapturePolicyId, string CaptureInputHash, string CaptureSelectedDigest, Guid InstrumentId,
    int HorizonSessions, DateTimeOffset EvaluationCutoff, DecisionBarLink? Anchor, DecisionBarLink? Endpoint,
    DecisionListingLink? Listing, IReadOnlyList<OutcomeCalendarDay> Calendar,
    IReadOnlyList<DecisionReferenceLink> Instruments, IReadOnlyList<InstrumentSessionProof> InstrumentSessions,
    IReadOnlyList<DecisionReferenceArchive> Archives, string? TerminalCondition);

public static class OutcomeEvaluator
{
    public const string PolicyId = "outcome-v0.1.0";
    public static bool Supported(DecisionSnapshotHeader h) => h.SchemaVersion == 1 && h.PolicyId == ScreenerReadRequest.PolicyId
        && h.CaptureKind == "PROSPECTIVE_CAPTURE" && h.Universe == "PILOT";
    public static DateOnly Through(DateTimeOffset at)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(at, "Asia/Jakarta").DateTime);
    private static bool Closed(ExchangeDayStatus status) => status is ExchangeDayStatus.Weekend
        or ExchangeDayStatus.AnnouncedClosed or ExchangeDayStatus.ExceptionalClosure;

    public static OutcomeHorizon ResolveHorizon(DecisionSnapshotHeader h, int n, IReadOnlyList<SessionProof> proofs,
        DateTimeOffset at, CancellationToken ct = default)
    {
        if (!OutcomeRequest.Horizons.Contains(n)) throw new ScreenerException(400, "OUTCOME_REQUEST_INVALID");
        if (h.TargetSession is not { } start) return new(null, "SESSION_UNAVAILABLE", "CAPTURE_TARGET_UNKNOWN", []);
        var today = Through(at);
        var through = today < ScreenerReadRequest.Horizon ? today : ScreenerReadRequest.Horizon;
        var days = new List<OutcomeCalendarDay>();
        var count = 0; var nonProspective = false;
        // ponytail: <=366 civil dates per bounded PILOT horizon; extend only under a reviewed capacity/policy change.
        for (var d = start.AddDays(1); d <= through; d = d.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();
            var session = ScreenerSessions.Resolve(proofs, d, through, at);
            days.Add(new(d, session.Status.ToString(), session.Proof));
            if (Closed(session.Status) && session.Reason is null) continue;
            if (session.Status != ExchangeDayStatus.ObservedTrading || session.Reason is not null)
                return new(null, d == today && session.Reason != "SESSION_CONFLICT" ? "PENDING" : "SESSION_UNAVAILABLE",
                    session.Reason ?? "SESSION_UNCONFIRMED", days);
            if (d <= h.Through)
            {
                if (d < Through(h.CapturedAt) || session.Proof?.CompletedAt <= h.CapturedAt) nonProspective = true;
                else if (session.Proof?.CompletedAt is null)
                    return new(null, "SESSION_UNAVAILABLE", "HORIZON_CHRONOLOGY_UNPROVED", days);
            }
            if (++count == n) return new(d, "RESOLVED", null, days, nonProspective);
        }
        return new(null, today > ScreenerReadRequest.Horizon ? "SESSION_UNAVAILABLE" : "PENDING",
            today > ScreenerReadRequest.Horizon ? "HORIZON_OUT_OF_SCOPE" : "HORIZON_NOT_REACHED", days);
    }

    public static ScreenerBarEvidence? Anchor(DecisionSnapshotRow row, OutcomeInputs inputs) =>
        inputs.CapturedDatabase.Bars.SingleOrDefault(b => b.InstrumentId == row.InstrumentId && b.SessionDate == row.MarketDate
            && b.RevisionNumber == row.Result.Provenance.Revision && b.ContentHash == row.Result.Provenance.ContentHash);
    public static ScreenerBarEvidence? Endpoint(DecisionSnapshotRow row, OutcomeHorizon horizon, OutcomeInputs inputs) =>
        inputs.ForwardDatabase.Bars.SingleOrDefault(b => b.InstrumentId == row.InstrumentId && b.SessionDate == horizon.Date);

    private static string? CaptureProblem(DecisionSnapshotHeader h, DecisionSnapshotRow row, OutcomeInputs inputs, CancellationToken ct)
    {
        if (row.Close is null || row.MarketDate is null) return "CAPTURED_CLOSE_MISSING";
        if (row.MarketDate != h.TargetSession || row.Result.Stale == true) return "STALE_ANCHOR";
        var raw = Anchor(row, inputs);
        if (raw is null || row.Close <= 0 || raw.KnownAt > h.KnowledgeCutoff || raw.FetchedAt > h.KnowledgeCutoff
            || raw.RetrievedAt > h.KnowledgeCutoff || raw.SessionKnownAt > h.KnowledgeCutoff) return "CAPTURED_OBSERVATION_INVALID";
        var observed = raw.Validate();
        if (!observed.Available || observed.Value!.Bar.Close != row.Close) return "CAPTURED_OBSERVATION_INVALID";
        var current = row.Result.Provenance.CurrentEvidence;
        if (current?.SessionDate == row.MarketDate && (current.Revision != raw.RevisionNumber || current.ContentHash != raw.ContentHash))
            return "CAPTURED_OBSERVATION_INVALID";
        if (row.Result.FieldStates.TryGetValue("close", out var field) && field.Availability != "AVAILABLE") return "CAPTURED_CLOSE_MISSING";
        for (var day = h.TargetSession!.Value; day <= h.Through; day = day.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();
            var session = ScreenerSessions.Resolve(inputs.CapturedReferences.Sessions, day, h.Through, h.KnowledgeCutoff);
            if (session.Reason is not null || (day == h.TargetSession ? session.Status != ExchangeDayStatus.ObservedTrading : !Closed(session.Status)))
                return "CAPTURE_SESSION_UNCONFIRMED";
        }
        var reference = ScreenerReferences.Instrument(new(1, [], inputs.CapturedReferences.Instruments), row.InstrumentId, h.KnowledgeCutoff);
        if (!reference.Available || !ScreenerReferences.Price(reference.Value!, raw).Available
            || ScreenerReferences.Identity(reference.Value!, raw.SessionDate).Value?.Classification != "ORDINARY")
            return "CAPTURED_PRICE_BASIS_UNVERIFIED";
        var status = Trading(reference.Value, inputs.CapturedReferences.InstrumentSessions, row.InstrumentId, raw.SessionDate, h.Through, h.KnowledgeCutoff);
        return status.Reason is not null || status.Status != "TRADING" || status.Mechanism != "CONTINUOUS" ? "CAPTURED_STATUS_UNVERIFIED" : null;
    }

    private static (string? Status, string? Mechanism, string? Reason) Trading(InstrumentSnapshot? reference,
        IReadOnlyList<InstrumentSessionProof> proofs, Guid id, DateOnly date, DateOnly through, DateTimeOffset at)
    {
        var trading = reference is null ? null : ScreenerReferences.Trading(reference, date);
        var status = trading?.Value?.Status;
        var proof = ScreenerSessions.ResolveInstrument(proofs, id, date, through, at);
        if (proof.Reason == "INSTRUMENT_SESSION_CONFLICT") return (null, null, proof.Reason);
        if (proof.Available)
        {
            var explicitStatus = proof.Value!.Status == MarketSessionStatus.Suspension ? "SUSPENSION" : "NO_TRADE";
            if (status is not null and not "UNKNOWN" && status != explicitStatus) return (null, null, "INSTRUMENT_SESSION_CONFLICT");
            status = explicitStatus;
        }
        return (status, trading?.Value?.Mechanism, null);
    }

    public static OutcomeCell Evaluate(DecisionSnapshotHeader h, DecisionSnapshotRow row, int n, OutcomeHorizon horizon,
        OutcomeInputs inputs, DateTimeOffset at, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        OutcomeCell Cell(string state, string? reason, decimal? close = null, decimal? priceReturn = null) =>
            new(row.InstrumentId, n, state, reason, row.MarketDate, row.Close, horizon.Date, close, priceReturn);
        if (horizon.Date is not { } date) return Cell(horizon.State, horizon.Reason);
        if (horizon.NonProspective) return Cell("ANCHOR_UNAVAILABLE", "NONPROSPECTIVE_SESSION_BASE");
        var anchorProblem = CaptureProblem(h, row, inputs, ct);
        if (anchorProblem is not null) return Cell("ANCHOR_UNAVAILABLE", anchorProblem);
        var raw = Endpoint(row, horizon, inputs);
        if (raw is not null && (raw.KnownAt > at || raw.FetchedAt > at || raw.RetrievedAt > at || raw.SessionKnownAt > at))
            return Cell("UNRESOLVED", "HORIZON_EVIDENCE_NOT_YET_KNOWN");
        var observation = raw?.Validate();
        var close = observation?.Value?.Bar.Close;
        var reference = ScreenerReferences.Instrument(new(1, [], inputs.ForwardReferences.Instruments), row.InstrumentId, at);
        if (reference.Reason?.Contains("CONFLICT", StringComparison.Ordinal) == true) return Cell("UNRESOLVED", reference.Reason, close);
        var listing = inputs.ForwardDatabase.Listings.SingleOrDefault(l => l.InstrumentId == row.InstrumentId);
        if (listing?.Resolution.Reason?.Contains("CONFLICT", StringComparison.Ordinal) == true) return Cell("UNRESOLVED", listing.Resolution.Reason, close);
        if (listing?.Resolution.Value is { Status: "VERIFIED", RetrievedAt: not null } boundary
            && boundary.RetrievedAt <= boundary.KnownAt && boundary.KnownAt <= at && boundary.DelistedAt < date)
            return Cell("DATA_UNAVAILABLE", "POST_DELISTING", close);
        var trading = Trading(reference.Value, inputs.ForwardReferences.InstrumentSessions, row.InstrumentId, date, date, at);
        if (trading.Reason is not null) return Cell("UNRESOLVED", trading.Reason, close);
        if (trading.Status is "SUSPENSION" or "NO_TRADE")
            return Cell("DATA_UNAVAILABLE", trading.Status == "SUSPENSION" ? "SUSPENDED_AT_HORIZON" : "NO_TRADE_AT_HORIZON", close);
        var identity = reference.Available ? ScreenerReferences.Identity(reference.Value!, date).Value : null;
        if (identity is not null && (identity.Classification is "INDEX" or "UNSUPPORTED" || identity.Currency is "OTHER" or "NOT_APPLICABLE"))
            return Cell("DATA_UNAVAILABLE", "ENDPOINT_CLASSIFICATION_UNSUPPORTED", close);
        if (trading.Mechanism is "CALL_AUCTION" or "OTHER" or "NOT_APPLICABLE") return Cell("DATA_UNAVAILABLE", "SPECIAL_REGIME_UNSUPPORTED", close);
        // The existing typed action model has coverage flags, not action facts: UNKNOWN/UNRESOLVED is never terminal proof.
        if (raw is not null && observation?.Available == true && reference.Available
            && ScreenerReferences.Price(reference.Value!, raw).Available && raw.SourceId != Anchor(row, inputs)!.SourceId)
            return Cell("BASIS_UNCERTAIN", "PRICE_CONVENTION_UNSUPPORTED", close);
        if (!reference.Available || identity?.Classification != "ORDINARY" || identity.Currency != "IDR") return Cell("UNRESOLVED", "IDENTITY_UNVERIFIED", close);
        if (trading.Status != "TRADING" || trading.Mechanism != "CONTINUOUS") return Cell("UNRESOLVED", "STATUS_UNKNOWN", close);
        if (raw is null) return Cell("UNRESOLVED", "HORIZON_BAR_MISSING");
        if (observation?.Available != true) return Cell("UNRESOLVED", observation?.Reason ?? "CANONICAL_INVALID");
        if (raw.Volume <= 0) return Cell("UNRESOLVED", "ZERO_VOLUME_UNEXPLAINED", close);
        var anchor = Anchor(row, inputs)!;
        var span = reference.Value!.Prices.SingleOrDefault(p => p.From <= h.TargetSession && p.Through >= date);
        if (!ScreenerReferences.Price(reference.Value, raw).Available || span is null || span.Continuity != "RAW_AS_TRADED"
            || span.EventCoverage != "CLEARED" || span.Convention != "STOCK_RAW" || span.SourceId != raw.SourceId
            || anchor.SourceId != raw.SourceId || !span.ContentHashes.Contains(anchor.ContentHash) || !span.ContentHashes.Contains(raw.ContentHash))
            return Cell("UNRESOLVED", "PRICE_COMPARABILITY_UNVERIFIED", close);
        // Require known ordinary IDR identity throughout the span, not merely two matching endpoint labels.
        var day = h.TargetSession!.Value;
        while (day <= date)
        {
            ct.ThrowIfCancellationRequested();
            var fact = ScreenerReferences.Identity(reference.Value, day).Value;
            if (fact?.Classification != "ORDINARY" || fact.Currency != "IDR") return Cell("UNRESOLVED", "PRICE_COMPARABILITY_UNVERIFIED", close);
            day = day.AddDays(1);
        }
        try { return Cell("AVAILABLE", null, close, checked(100m * (close!.Value / row.Close!.Value - 1m))); }
        catch (OverflowException) { return Cell("UNRESOLVED", "NUMERIC_OUT_OF_RANGE", close); }
    }
}
