namespace IdxStockIntelligence.Application;

public static class ProductQuery
{
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Jakarta").DateTime);
    public static DateOnly Date(DateOnly? value)
    {
        var day = value ?? Today;
        if (day > Today || day < new DateOnly(1900, 1, 1)) throw new ArgumentException("through must be 1900-01-01..today (Jakarta).");
        return day;
    }
    public static DateTimeOffset Cutoff(DateTimeOffset? value)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = value ?? now;
        if (cutoff > now || cutoff < new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero))
            throw new ArgumentException("cutoff must be 1900-01-01..now.");
        return cutoff.ToUniversalTime();
    }
}
