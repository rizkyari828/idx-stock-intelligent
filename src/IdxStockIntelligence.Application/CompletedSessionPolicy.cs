using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public static class CompletedSessionPolicy
{
    public static string Reason(DateOnly day, DateTimeOffset collectionAt, TimeOnly cutoff, SessionProof? proof)
    {
        var jakarta=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(collectionAt,"Asia/Jakarta");
        var today=DateOnly.FromDateTime(jakarta.DateTime);
        if (day>today) return "FUTURE_DATE";
        if (day==today && TimeOnly.FromDateTime(jakarta.DateTime)<cutoff) return "SAFE_EOD_CUTOFF_NOT_REACHED";
        if (proof is null) return ExchangeCalendarEvidence.Classify(day) == ExchangeDayStatus.Weekend
            ? "CLOSED_BY_CALENDAR" : "SESSION_PROOF_REQUIRED";
        if (proof.Date!=day || !Uri.TryCreate(proof.Reference,UriKind.Absolute,out var uri) || uri.Scheme!="https"
            || uri.Host.Length==0 || uri.UserInfo.Length!=0 || uri.Host.Equals("eodhd.com",StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".eodhd.com",StringComparison.OrdinalIgnoreCase) || proof.KnownAt>collectionAt)
            return "INDEPENDENT_ALREADY_KNOWN_PROOF_REQUIRED";
        if (proof.Status is ExchangeDayStatus.AnnouncedClosed or ExchangeDayStatus.ExceptionalClosure) return "KNOWN_CLOSED";
        if (proof.Status!=ExchangeDayStatus.ObservedTrading)
            return "SESSION_PROOF_REQUIRED";
        if (day<today) return "ELIGIBLE_PRIOR_COMPLETED_SESSION";
        if (proof.CompletedAt is null || proof.CompletedAt>proof.KnownAt
            || DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(proof.CompletedAt.Value,"Asia/Jakarta").DateTime)!=day)
            return "COMPLETED_SESSION_EVIDENCE_REQUIRED";
        return "ELIGIBLE_SAME_DAY_COMPLETED_SESSION";
    }
}
