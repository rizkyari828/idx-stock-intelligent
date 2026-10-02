using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

// Prepared, evidence-cleared suffixes only; the public evaluator enforces through/cutoff before this integration.
internal static class ScreenerFeatures
{
    private static readonly string[] Names = ["changePercent", "ema20", "ema50", "atr14", "atrPercent", "priorHigh20",
        "priorLow20", "distanceToHighPercent", "volumeRatio20", "dailyValueProxyIdr", "monetaryLiquidity20Idr", "rs20Pp", "rs60Pp"];

    public static IReadOnlyDictionary<string, FeatureState> Calculate(IReadOnlyList<DailyBar> series,
        IReadOnlyDictionary<DateOnly, DailyBar> benchmark, IReadOnlyList<ScreenerBarEvidence> evidence,
        InstrumentSnapshot? reference, string? currentReason, bool index, CancellationToken ct)
    {
        var fields = new Dictionary<string, FeatureState>(StringComparer.Ordinal);
        foreach (var name in Names)
        { ct.ThrowIfCancellationRequested(); fields[name] = new(Availability.UNAVAILABLE, null, currentReason ?? "MISSING_CURRENT_BAR"); }
        if (currentReason is not null || series.Count == 0) return fields;
        var close = series[^1].Close;
        void Field(string name, int count, Func<decimal?> calculate, string? reason = null)
        {
            ct.ThrowIfCancellationRequested();
            if (series.Count < count) { fields[name] = new(Availability.WARMUP, null, "INSUFFICIENT_SESSIONS"); return; }
            if (reason is not null) { fields[name] = new(Availability.UNAVAILABLE, null, reason); return; }
            try { fields[name] = new(Availability.AVAILABLE, calculate(), null); }
            catch (OverflowException) { fields[name] = new(Availability.UNAVAILABLE, null, "NUMERIC_OUT_OF_RANGE"); }
        }
        var closes = series.Select(b => b.Close).ToArray();
        Field("ema20", 20, () => PilotFeatures.Smooth(closes, 20, 2m / 21m));
        Field("ema50", 50, () => PilotFeatures.Smooth(closes, 50, 2m / 51m));
        Field("atr14", 15, () => PilotFeatures.Smooth(PilotFeatures.TrueRanges(series), 14, 1m / 14m));
        Field("atrPercent", 15, () => 100m * fields["atr14"].Value / close, fields["atr14"].UnavailableReason);
        Field("priorHigh20", 21, () => series.SkipLast(1).TakeLast(20).Max(b => b.High));
        Field("priorLow20", 21, () => series.SkipLast(1).TakeLast(20).Min(b => b.Low));
        Field("distanceToHighPercent", 21, () => 100m * (close / fields["priorHigh20"].Value - 1m));
        Field("changePercent", 2, () => 100m * (close / series[^2].Close - 1m));
        if (index) return fields; // Index share-volume and stock-relative fields are not applicable.
        var byDate = evidence.ToDictionary(b => b.SessionDate);
        bool Cleared(DailyBar bar) => reference is not null && byDate.TryGetValue(bar.SessionDate, out var raw)
            && ScreenerReferences.Volume(reference, raw).Available;
        var window = series.TakeLast(21).ToArray();
        var currentVolumeReason = Cleared(series[^1]) ? null : "VOLUME_BASIS_UNVERIFIED";
        var windowVolumeReason = window.All(Cleared) && window.All(b => b.VolumeUnit == window[^1].VolumeUnit
            && b.VolumeBasis == window[^1].VolumeBasis && b.MarketSegment == window[^1].MarketSegment)
            ? null : "VOLUME_BASIS_UNVERIFIED";
        Field("dailyValueProxyIdr", 1, () => checked(close * series[^1].Volume), currentVolumeReason);
        Field("volumeRatio20", 21, () => PilotFeatures.VolumeRatio(series), windowVolumeReason);
        Field("monetaryLiquidity20Idr", 21, () => window.SkipLast(1).Average(b => checked(b.Close * b.Volume)), windowVolumeReason);
        fields["rs20Pp"] = PilotFeatures.RelativeStrength(series, benchmark, 20);
        fields["rs60Pp"] = PilotFeatures.RelativeStrength(series, benchmark, 60);
        return fields;
    }

    public static string Trend(decimal? close, IReadOnlyDictionary<string, FeatureState> fields)
    {
        var ema20 = fields.GetValueOrDefault("ema20")?.Value;
        var ema50 = fields.GetValueOrDefault("ema50")?.Value;
        return close is null || ema20 is null || ema50 is null ? "UNKNOWN"
            : close > ema20 && ema20 > ema50 ? "POSITIVE" : close < ema20 && ema20 < ema50 ? "NEGATIVE" : "NEUTRAL";
    }
}
