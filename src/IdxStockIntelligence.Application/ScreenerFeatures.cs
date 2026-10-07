using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

// Prepared, evidence-cleared suffixes only; the public evaluator enforces through/cutoff before this integration.
internal static class ScreenerFeatures
{
    public static IReadOnlyDictionary<string, FeatureState> Calculate(IReadOnlyList<DailyBar> series,
        IReadOnlyDictionary<DateOnly, DailyBar> benchmark, IReadOnlyList<ScreenerBarEvidence> evidence,
        InstrumentSnapshot? reference, string? currentReason, bool index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (currentReason is not null || series.Count == 0 || index)
            return ScreenerTechnicalArithmetic.Calculate(series, benchmark, currentReason, index, null, null, ct);
        var byDate = evidence.ToDictionary(b => b.SessionDate);
        bool Cleared(DailyBar bar) => reference is not null && byDate.TryGetValue(bar.SessionDate, out var raw)
            && ScreenerReferences.Volume(reference, raw).Available;
        var window = series.TakeLast(21).ToArray();
        var currentVolumeReason = Cleared(series[^1]) ? null : "VOLUME_BASIS_UNVERIFIED";
        var windowVolumeReason = window.All(Cleared) && window.All(b => b.VolumeUnit == window[^1].VolumeUnit
            && b.VolumeBasis == window[^1].VolumeBasis && b.MarketSegment == window[^1].MarketSegment)
            ? null : "VOLUME_BASIS_UNVERIFIED";
        return ScreenerTechnicalArithmetic.Calculate(series, benchmark, currentReason, index, currentVolumeReason, windowVolumeReason, ct);
    }

    public static string Trend(decimal? close, IReadOnlyDictionary<string, FeatureState> fields)
    {
        var ema20 = fields.GetValueOrDefault("ema20")?.Value;
        var ema50 = fields.GetValueOrDefault("ema50")?.Value;
        return close is null || ema20 is null || ema50 is null ? "UNKNOWN"
            : close > ema20 && ema20 > ema50 ? "POSITIVE" : close < ema20 && ema20 < ema50 ? "NEGATIVE" : "NEUTRAL";
    }
}
