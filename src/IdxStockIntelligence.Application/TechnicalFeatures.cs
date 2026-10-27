using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

/// <summary>
/// Public, read-only facade over the internal Screener feature arithmetic. It adds no formula, period
/// or smoothing: it forwards a prepared, calendar-continuous bar series to
/// <see cref="ScreenerTechnicalArithmetic"/> and exposes the thirteen feature states for product reads.
/// A WARMUP or UNAVAILABLE state never carries a value.
/// </summary>
public static class TechnicalFeatures
{
    public const string NotApplicableToIndex = "NOT_APPLICABLE_TO_INDEX";

    public static IReadOnlyList<string> Names { get; } =
    [
        "changePercent", "ema20", "ema50", "atr14", "atrPercent", "priorHigh20", "priorLow20",
        "distanceToHighPercent", "volumeRatio20", "dailyValueProxyIdr", "monetaryLiquidity20Idr", "rs20Pp", "rs60Pp"
    ];

    /// <summary>
    /// The window each feature guards itself with inside ScreenerTechnicalArithmetic, exposed only so a
    /// product can say "warmup — needs N sessions". It is not applied here: the arithmetic alone decides
    /// availability, and tests pin every entry to its own AVAILABLE/WARMUP boundary.
    /// </summary>
    public static IReadOnlyDictionary<string, int> MinimumSessions { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["changePercent"] = 2, ["ema20"] = 20, ["ema50"] = 50, ["atr14"] = 15, ["atrPercent"] = 15,
        ["priorHigh20"] = 21, ["priorLow20"] = 21, ["distanceToHighPercent"] = 21, ["volumeRatio20"] = 21,
        ["dailyValueProxyIdr"] = 1, ["monetaryLiquidity20Idr"] = 21, ["rs20Pp"] = 21, ["rs60Pp"] = 61
    };

    // The arithmetic deliberately stops before these fields for an index (see its early return) and leaves
    // its placeholder reason. Availability stays UNAVAILABLE; only the reason is made truthful.
    private static readonly string[] IndexNotApplicable =
        ["volumeRatio20", "dailyValueProxyIdr", "monetaryLiquidity20Idr", "rs20Pp", "rs60Pp"];

    public static IReadOnlyDictionary<string, FeatureState> Project(IReadOnlyList<DailyBar> series,
        IReadOnlyDictionary<DateOnly, DailyBar> benchmark, bool index, string? currentReason,
        string? currentVolumeReason, string? windowVolumeReason, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(benchmark);
        var fields = ScreenerTechnicalArithmetic.Calculate(series, benchmark, currentReason, index,
            currentVolumeReason, windowVolumeReason, ct);
        if (!index) return fields;
        var projection = new Dictionary<string, FeatureState>(fields, StringComparer.Ordinal);
        foreach (var name in IndexNotApplicable)
            projection[name] = new(Availability.UNAVAILABLE, null, NotApplicableToIndex);
        return projection;
    }

    /// <summary>
    /// The trailing run of consecutive exchange sessions ending at the newest validated bar at or before
    /// <paramref name="through"/>. Session membership is the retained calendar evidence, so an unconfirmed
    /// or missing session ends the run instead of being bridged; N scattered bars never satisfy an
    /// N-session window.
    /// </summary>
    public static TechnicalSeries Series(IReadOnlyList<SessionProof> proofs, IReadOnlyDictionary<DateOnly, DailyBar> bars,
        DateOnly through, DateTimeOffset cutoff, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proofs);
        ArgumentNullException.ThrowIfNull(bars);
        var run = new List<DailyBar>();
        DailyBar[] last = [];
        DateOnly? marketDate = null;
        for (var date = ScreenerReadRequest.Anchor; date <= through; date = date.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();
            var day = ScreenerSessions.Resolve(proofs, date, through, cutoff);
            if (day.Status is ExchangeDayStatus.Weekend or ExchangeDayStatus.AnnouncedClosed or ExchangeDayStatus.ExceptionalClosure)
                continue;
            // An unconfirmed/unfinished session is not a bar even when a row exists: it ends the run.
            if (day.Status == ExchangeDayStatus.ObservedTrading && day.Reason is null && bars.TryGetValue(date, out var bar))
            { run.Add(bar); last = [.. run]; marketDate = date; }
            else run.Clear();
        }
        return new(marketDate, last);
    }

    public static TechnicalFeaturesReport Report(StockIdentity identity, DateOnly through, DateTimeOffset cutoff,
        bool index, TechnicalSeries series, IReadOnlyDictionary<string, FeatureState> fields)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(fields);
        var features = Names.ToDictionary(name => name, name =>
        {
            var state = fields[name];
            return new TechnicalFeatureValue(state.Availability.ToString(), state.Value, state.UnavailableReason,
                MinimumSessions[name]);
        }, StringComparer.Ordinal);
        return new(identity.Id, identity.Symbol, identity.Name, through, cutoff, series.MarketDate, series.Bars.Count,
            index, features);
    }
}

public sealed record TechnicalSeries(DateOnly? MarketDate, IReadOnlyList<DailyBar> Bars);

public sealed record TechnicalFeatureValue(string Availability, decimal? Value, string? Reason, int MinimumSessions);

public sealed record TechnicalFeaturesReport(Guid InstrumentId, string Symbol, string Name, DateOnly Through,
    DateTimeOffset Cutoff, DateOnly? MarketDate, int Sessions, bool Index,
    IReadOnlyDictionary<string, TechnicalFeatureValue> Features);
