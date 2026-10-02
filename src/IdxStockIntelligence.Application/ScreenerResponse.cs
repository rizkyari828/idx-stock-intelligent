using System.Globalization;
using System.Text.RegularExpressions;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed class ScreenerException(int statusCode, string code) : Exception(code)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public sealed record ScreenerQuery(DateOnly Through, DateTimeOffset Cutoff, Guid? PortfolioId,
    string View, string Setup, string Eligibility, int Offset, int Limit, string? InputHash)
{
    private static readonly string[] Keys = ["through", "cutoff", "universe", "portfolioId", "view", "setup", "eligibility", "offset", "limit", "inputHash"];
    private static readonly Regex Timestamp = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant);

    // The route supplies one clock reading; defaults and both future checks use that same instant.
    public static ScreenerQuery Resolve(IReadOnlyDictionary<string, string?> values, DateTimeOffset now)
    {
        if (values.Keys.Any(k => !Keys.Contains(k, StringComparer.Ordinal))) throw new ArgumentException("Unknown Screener parameter.");
        string? Get(string key) => values.GetValueOrDefault(key);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, "Asia/Jakarta").DateTime);
        var through = today;
        if (Get("through") is { } date && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out through))
            throw new ArgumentException("through requires YYYY-MM-DD.");
        if (through < ScreenerReadRequest.Anchor || through > ScreenerReadRequest.Horizon || through > today)
            throw new ArgumentException("through is outside the Screener horizon or after Jakarta today.");
        var cutoff = now;
        if (Get("cutoff") is { } at && !string.IsNullOrWhiteSpace(at) && (!Timestamp.IsMatch(at) || !DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out cutoff))) throw new ArgumentException("cutoff requires an RFC3339 timestamp with offset.");
        if (cutoff > now || cutoff < new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero))
            throw new ArgumentException("cutoff must be 1900-01-01..now.");
        string Choice(string key, string fallback, params string[] allowed)
        {
            var value = Get(key) ?? fallback;
            if (!allowed.Contains(value, StringComparer.Ordinal)) throw new ArgumentException("Invalid " + key + ".");
            return value;
        }
        _ = Choice("universe", "PILOT", "PILOT");
        Guid? portfolio = null;
        if (Get("portfolioId") is { } id)
        {
            if (!Guid.TryParseExact(id, "D", out var parsed) || parsed == Guid.Empty) throw new ArgumentException("portfolioId requires a UUID.");
            portfolio = parsed;
        }
        int Page(string key, int fallback, int min, int max)
        {
            if (Get(key) is not { } text) return fallback;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
                throw new ArgumentException("Invalid " + key + ".");
            return value;
        }
        var hash = Get("inputHash");
        if (hash is not null && !ScreenerReferences.IsHash(hash)) throw new ArgumentException("inputHash requires lowercase SHA-256.");
        return new(through, cutoff.ToUniversalTime(), portfolio, Choice("view", "shortlist", "shortlist", "all"),
            Choice("setup", "ALL", "ALL", "NONE", "WATCH", "CONFIRMED", "FAILED"),
            Choice("eligibility", "ALL", "ALL", "ELIGIBLE", "INELIGIBLE", "DATA_BLOCKED"),
            Page("offset", 0, 0, 10000), Page("limit", 20, 1, 100), hash);
    }
}

public sealed record ScreenerFieldDto(string Availability, string? Reason);
public sealed record ScreenerRevisionDto(DateOnly SessionDate, long Revision, DateTimeOffset KnownAt,
    DateTimeOffset? RetrievedAt, string ContentHash, string CanonicalQuality, string Source);
public sealed record ScreenerProvenanceDto(string? PriceBasis, string? Source, string? Currency, string? VolumeUnit,
    string? VolumeBasis, string? MarketSegment, long? Revision, DateTimeOffset? KnownAt, DateTimeOffset? RetrievedAt,
    string? ContentHash, DateOnly? PriceSequenceStart, DateOnly? EmaSeedStart, DateOnly? AtrSeedStart,
    int ConsecutiveSessions, IReadOnlyList<string> ReferenceSnapshotIds, string? CanonicalQuality,
    ScreenerRevisionDto? CurrentEvidence);
public sealed record ScreenerRowDto(Guid InstrumentId, string? Symbol, string? DisplayName, bool Configured, bool Held,
    Mandate? Mandate, int? DiscoveryRank, string Eligibility, IReadOnlyList<string> EligibilityReasons,
    string Setup, bool SetupEvaluated, IReadOnlyList<string> SetupReasons, ScreenerEpisode? Episode,
    DateOnly? MarketDate, decimal? Close, decimal? ChangePercent, decimal? Ema20, decimal? Ema50, string Trend,
    decimal? PriorHigh20, decimal? PriorLow20, decimal? DistanceToHighPercent, long? Volume, decimal? VolumeRatio20,
    decimal? DailyValueProxyIdr, decimal? MonetaryLiquidity20Idr, decimal? Atr14, decimal? AtrPercent,
    decimal? Rs20Pp, decimal? Rs60Pp, bool? Stale, bool? NoTrade, string? TradingStatus, string DataQuality,
    IReadOnlyList<string> DataReasons, IReadOnlyDictionary<string, ScreenerFieldDto> FieldStates, ScreenerProvenanceDto Provenance);
public sealed record ScreenerContextDto(Guid InstrumentId, DateOnly? MarketDate, string Trend, string Volatility,
    decimal? Close, decimal? Ema20, decimal? Ema50, decimal? Atr14, decimal? AtrPercent, IReadOnlyList<string> Reasons,
    IReadOnlyDictionary<string, ScreenerFieldDto> FieldStates, ScreenerProvenanceDto Provenance);
public sealed record ScreenerPage(string View, string Setup, string Eligibility, int Offset, int Limit, int Total);
public sealed record ScreenerResponse(string PolicyId, DateOnly Through, DateOnly? TargetSession, DateTimeOffset Cutoff,
    DateOnly HistoryAnchor, string Universe, string? UniverseSnapshotId, string ReplayScope, string InputHash,
    string Status, IReadOnlyList<string> Reasons, ScreenerSummary Summary, ScreenerContextDto MarketContext,
    ScreenerPage Page, IReadOnlyList<Guid> DiscoveryIds, IReadOnlyList<Guid> HeldIds, IReadOnlyList<ScreenerRowDto> Rows);

public static class ScreenerPresentation
{
    public static string EligibilityName(EligibilityStatus status) => status switch
    { EligibilityStatus.Eligible => "ELIGIBLE", EligibilityStatus.Ineligible => "INELIGIBLE", _ => "DATA_BLOCKED" };
    private static decimal? Value(IReadOnlyDictionary<string, FeatureState> fields, string name) =>
        fields.GetValueOrDefault(name) is { Availability: Availability.AVAILABLE } state ? state.Value : null;
    private static Dictionary<string, ScreenerFieldDto> States(IReadOnlyDictionary<string, FeatureState> fields) =>
        fields.Where(p => p.Value.Availability != Availability.AVAILABLE).ToDictionary(p => p.Key,
            p => new ScreenerFieldDto(p.Value.Availability.ToString(), p.Value.UnavailableReason), StringComparer.Ordinal);

    private static ScreenerProvenanceDto Provenance(ScreenerProvenance p, SelectedScreenerReferences references, DateTimeOffset cutoff)
    {
        var raw = p.Observation;
        var selected = raw is null ? null : ScreenerReferences.Instrument(new(1, [], references.Instruments), raw.InstrumentId, cutoff).Value;
        var identity = raw is null || selected is null ? null : ScreenerReferences.Identity(selected, raw.SessionDate).Value;
        var price = raw is null || selected is null ? null : ScreenerReferences.Price(selected, raw).Value;
        var current = p.CurrentEvidence;
        return new(price?.Continuity, raw?.SourceId, identity?.Currency, raw?.VolumeUnit, raw?.VolumeBasis, raw?.MarketSegment,
            raw?.RevisionNumber, raw?.KnownAt.ToUniversalTime(), raw?.RetrievedAt?.ToUniversalTime(), raw?.ContentHash,
            p.PriceSequenceStart, p.EmaSeedStart, p.AtrSeedStart, p.ConsecutiveSessions,
            p.ReferenceSnapshotId is { } snapshot ? [snapshot] : [], raw?.CanonicalQuality,
            current is null ? null : new(current.SessionDate, current.RevisionNumber, current.KnownAt.ToUniversalTime(),
                current.RetrievedAt?.ToUniversalTime(), current.ContentHash, current.CanonicalQuality, current.SourceId));
    }

    public static ScreenerResponse Map(ScreenerResult result, ScreenerQuery query, string hash,
        SelectedScreenerReferences references, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (query.InputHash is not null && query.InputHash != hash) throw new ScreenerException(409, "INPUT_CHANGED");
        var byId = result.Rows.ToDictionary(r => r.InstrumentId);
        var filtered = (query.View == "shortlist" ? result.ShortlistIds : result.AllViewIds).Where(id =>
            (query.Setup == "ALL" || string.Equals(byId[id].Setup.Status.ToString(), query.Setup, StringComparison.OrdinalIgnoreCase))
            && (query.Eligibility == "ALL" || EligibilityName(byId[id].Eligibility.Status) == query.Eligibility)).ToArray();
        var discovery = filtered.Skip(query.Offset).Take(query.Limit).ToArray();
        var rows = new List<ScreenerRowDto>();
        foreach (var id in discovery.Concat(result.HeldIds).Distinct())
        {
            ct.ThrowIfCancellationRequested();
            var r = byId[id]; var f = r.Fields;
            var rowStates = States(f);
            if (r.TradingStatus is null) rowStates["tradingStatus"] = new("UNAVAILABLE", "STATUS_UNKNOWN");
            rows.Add(new(r.InstrumentId, r.Symbol, r.DisplayName, r.Configured, r.Held, r.Mandate, r.DiscoveryRank,
                EligibilityName(r.Eligibility.Status), r.Eligibility.Reasons, r.Setup.Status.ToString().ToUpperInvariant(),
                r.Setup.Evaluated, r.Setup.Reasons, r.Setup.Episode, r.MarketDate, r.Close, Value(f, "changePercent"),
                Value(f, "ema20"), Value(f, "ema50"), r.Trend, Value(f, "priorHigh20"), Value(f, "priorLow20"),
                Value(f, "distanceToHighPercent"), r.Volume, Value(f, "volumeRatio20"), Value(f, "dailyValueProxyIdr"),
                Value(f, "monetaryLiquidity20Idr"), Value(f, "atr14"), Value(f, "atrPercent"), Value(f, "rs20Pp"), Value(f, "rs60Pp"),
                r.Stale, r.NoTrade, r.TradingStatus, r.DataQuality.ToString().ToUpperInvariant(), r.DataReasons, rowStates,
                Provenance(r.Provenance, references, result.Cutoff)));
        }
        var context = result.MarketContext;
        var states = States(context.Fields);
        if (context.Trend == "UNKNOWN") states["trend"] = new("UNAVAILABLE", context.Reasons.Count > 0 ? context.Reasons[0] : "BENCHMARK_MISSING");
        if (context.Volatility == "UNKNOWN") states["volatility"] = new("UNAVAILABLE", context.Fields["atrPercent"].UnavailableReason);
        return new(result.PolicyId, result.Through, result.TargetSession, result.Cutoff, result.HistoryAnchor, "PILOT",
            result.UniverseSnapshotId, "FIXED_PILOT_KNOWN_INPUTS", hash, result.Status.ToString().ToUpperInvariant(), result.Reasons,
            result.Summary, new(context.InstrumentId, context.MarketDate, context.Trend, context.Volatility,
                Value(context.Fields, "close"), Value(context.Fields, "ema20"), Value(context.Fields, "ema50"), Value(context.Fields, "atr14"),
                Value(context.Fields, "atrPercent"), context.Reasons, states, Provenance(context.Provenance, references, result.Cutoff)),
            new(query.View, query.Setup, query.Eligibility, query.Offset, query.Limit, filtered.Length), discovery, result.HeldIds, rows);
    }

    public static string InputHash(string selectedDigest, ScreenerReadRequest request, ScreenerPortfolioHistory? history,
        LedgerProjection? projection, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        static string Number(decimal number) => number.ToString("G29", CultureInfo.InvariantCulture);
        var held = projection?.Positions.Where(p => p.Shares > 0).Select(p => new { p.InstrumentId, shares = Number(p.Shares) }).ToArray();
        var ids = held?.Select(p => p.InstrumentId).ToHashSet() ?? [];
        return ScreenerReferences.Hash(new
        {
            selectedDigest,
            portfolio = history is null ? null : new { history.Portfolio.Id, history.Portfolio.Name,
                history.Portfolio.AllowNegativeCash, createdAt = history.Portfolio.CreatedAt.ToUniversalTime() },
            events = history?.Events.Where(e => e.KnownAt <= request.Cutoff).Select(e => new
            {
                e.Id, e.PortfolioId, e.Type, e.InstrumentId, e.TradeDate, e.KnownAt, e.Order, e.Unit,
                quantity = Number(e.Quantity), price = Number(e.Price), fees = Number(e.Fees), cashAmount = Number(e.CashAmount),
                e.ExternalReference, e.Source, e.Note, e.Supersedes
            }).ToArray(),
            theses = history?.Theses.Where(t => t.KnownAt <= request.Cutoff && ids.Contains(t.InstrumentId)).ToArray(), held
        }, ct);
    }
}
