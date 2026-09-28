using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record PilotFeatureResult(DateOnly? MarketDate, string Status, int ConsecutiveSessions,
    decimal? Ema20, decimal? Ema50, decimal? Atr14, decimal? PriorHigh20, decimal? PriorLow20,
    decimal? VolumeRatio20, decimal? RelativePerformance20);

public static class PilotFeatures
{
    public static PilotFeatureResult Calculate(IEnumerable<DailyBarRevision> revisions,
        InstrumentId instrument, InstrumentId benchmark, IReadOnlyList<SessionProof> proofs, DateTimeOffset cutoff)
    {
        var calendar = proofs.Where(p => p.KnownAt <= cutoff).ToDictionary(p => p.Date);
        var all = revisions.Where(r => r.KnownAt <= cutoff && r.Bar.Source.AvailableAt <= cutoff)
            .GroupBy(r => (r.Bar.InstrumentId, r.Bar.SessionDate))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.KnownAt).ThenByDescending(r => r.RevisionNumber).First().Bar);
        var dates = calendar.Values.Where(p => p.Status == ExchangeDayStatus.ObservedTrading)
            .Select(p => p.Date).Order().ToArray();
        if (dates.Length == 0) return Empty(null, "SESSION_UNCONFIRMED");
        var own = all.Values.Where(b => b.InstrumentId == instrument).OrderBy(b => b.SessionDate).ToArray();
        if (own.Length == 0) return Empty(null, "MISSING");
        var series = new List<DailyBar>();
        for (var date = own[0].SessionDate; date <= dates[^1]; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (calendar.TryGetValue(date, out var proof) && proof.Status is ExchangeDayStatus.AnnouncedClosed or ExchangeDayStatus.ExceptionalClosure) continue;
            if (proof?.Status == ExchangeDayStatus.ObservedTrading && all.TryGetValue((instrument, date), out var bar) && bar.Volume > 0)
                series.Add(bar);
            else series.Clear(); // Unknown weekdays/missing bars break continuity; never bridge them.
        }
        if (series.Count == 0) return Empty(own[^1].SessionDate, own[^1].SessionDate < dates[^1] ? "STALE" : "UNKNOWN");
        var closes = series.Select(b => b.Close).ToArray();
        var trueRanges = series.Skip(1).Select((b, i) => Math.Max(b.High - b.Low,
            Math.Max(Math.Abs(b.High - series[i].Close), Math.Abs(b.Low - series[i].Close)))).ToArray();
        decimal? relative = null;
        if (series.Count >= 21)
        {
            var aligned = series.TakeLast(21).Select(b => all.GetValueOrDefault((benchmark, b.SessionDate))).ToArray();
            if (aligned.All(b => b is not null && b.Volume > 0))
                relative = (series[^1].Close / series[^21].Close) / (aligned[^1]!.Close / aligned[0]!.Close) - 1m;
        }
        var previous = series.SkipLast(1).TakeLast(20).ToArray();
        var sameVolumeBasis = previous.Length == 20 && previous.All(b =>
            b.VolumeUnit == series[^1].VolumeUnit && b.VolumeBasis == series[^1].VolumeBasis && b.MarketSegment == series[^1].MarketSegment);
        decimal? volumeRatio = sameVolumeBasis && previous.Sum(b => (decimal)b.Volume) > 0
            ? series[^1].Volume / previous.Average(b => (decimal)b.Volume) : null;
        return new(series[^1].SessionDate, series.Count >= 50 ? "AVAILABLE_PILOT" : "WARMUP", series.Count,
            Smooth(closes, 20, 2m / 21m), Smooth(closes, 50, 2m / 51m), Smooth(trueRanges, 14, 1m / 14m),
            previous.Length == 20 ? previous.Max(b => b.High) : null,
            previous.Length == 20 ? previous.Min(b => b.Low) : null, volumeRatio, relative);
    }

    private static decimal? Smooth(decimal[] values, int period, decimal alpha)
    {
        if (values.Length < period) return null;
        var value = values.Take(period).Average();
        foreach (var next in values.Skip(period)) value += alpha * (next - value);
        return value;
    }

    private static PilotFeatureResult Empty(DateOnly? date, string status) => new(date, status, 0, null, null, null, null, null, null, null);
}
