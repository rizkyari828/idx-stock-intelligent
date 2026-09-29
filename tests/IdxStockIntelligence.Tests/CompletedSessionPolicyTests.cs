using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class CompletedSessionPolicyTests
{
    [Fact]
    public void ExplicitJakartaClockCompletionAndConfiguredCutoffControlEligibility()
    {
        var day=new DateOnly(2026,9,29);
        var proof=new SessionProof(day,ExchangeDayStatus.ObservedTrading,"https://reference.example/closing-report",
            new(2026,9,29,10,0,0,TimeSpan.Zero),new(2026,9,29,9,15,0,TimeSpan.Zero));
        var cutoff=new TimeOnly(19,0);
        DateTimeOffset Clock(int hour,int minute=0) => new(2026,9,29,hour,minute,0,TimeSpan.Zero);
        Assert.Equal("ELIGIBLE_PRIOR_COMPLETED_SESSION",CompletedSessionPolicy.Reason(day,Clock(18),cutoff,proof));
        Assert.Equal("SAFE_EOD_CUTOFF_NOT_REACHED",CompletedSessionPolicy.Reason(day,Clock(11,59),cutoff,proof));
        Assert.Equal("ELIGIBLE_SAME_DAY_COMPLETED_SESSION",CompletedSessionPolicy.Reason(day,Clock(12),cutoff,proof));
        Assert.Equal("SESSION_PROOF_REQUIRED",CompletedSessionPolicy.Reason(day,Clock(12,30),cutoff,null));
        Assert.Equal("KNOWN_CLOSED",CompletedSessionPolicy.Reason(day,Clock(12,30),cutoff,proof with { Status=ExchangeDayStatus.AnnouncedClosed }));
        Assert.Equal("ELIGIBLE_SAME_DAY_COMPLETED_SESSION",CompletedSessionPolicy.Reason(day,Clock(12,30),cutoff,proof));
        Assert.Equal("FUTURE_DATE",CompletedSessionPolicy.Reason(day.AddDays(1),Clock(12,30),cutoff,null));
        Assert.Equal("SAFE_EOD_CUTOFF_NOT_REACHED",CompletedSessionPolicy.Reason(day,Clock(18).AddDays(-1),cutoff,proof));
        Assert.Equal("SAFE_EOD_CUTOFF_NOT_REACHED",CompletedSessionPolicy.Reason(day,Clock(12,30),new(20,0),proof));
        Assert.Equal("ELIGIBLE_SAME_DAY_COMPLETED_SESSION",CompletedSessionPolicy.Reason(day,Clock(13),new(20,0),proof));
        Assert.Equal("COMPLETED_SESSION_EVIDENCE_REQUIRED",CompletedSessionPolicy.Reason(day,Clock(12),cutoff,proof with { CompletedAt=null }));
        Assert.Equal("COMPLETED_SESSION_EVIDENCE_REQUIRED",CompletedSessionPolicy.Reason(day,Clock(12),cutoff,proof with { CompletedAt=Clock(11) }));
        Assert.Equal("INDEPENDENT_ALREADY_KNOWN_PROOF_REQUIRED",CompletedSessionPolicy.Reason(day,Clock(12),cutoff,proof with { KnownAt=Clock(13) }));
        Assert.Equal("INDEPENDENT_ALREADY_KNOWN_PROOF_REQUIRED",CompletedSessionPolicy.Reason(day,Clock(12),cutoff,proof with { Reference="https://eodhd.com/row" }));
    }
}
