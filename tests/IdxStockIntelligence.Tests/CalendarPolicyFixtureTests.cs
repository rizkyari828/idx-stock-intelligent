using System.Text.Json;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class CalendarPolicyFixtureTests
{
    [Fact]
    public void SharedCalendarFixtureMatchesCompletedSessionPolicyAndExplicitWeekendOpening()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "calendar-policy.json")));
        var clock = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        foreach (var row in json.RootElement.EnumerateArray())
        {
            var day = DateOnly.Parse(row.GetProperty("date").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            var status = row.GetProperty("status").GetString();
            var proof = status is null ? null : new SessionProof(day, Enum.Parse<ExchangeDayStatus>(status), "https://reference.example/synthetic", clock.AddDays(-1));
            Assert.Equal(row.GetProperty("reason").GetString(), CompletedSessionPolicy.Reason(day, clock, new(19, 0), proof));
            if (proof is not null)
            {
                var calendar = new ExchangeCalendarEvidence(); calendar.Record(new(day, proof.Status, proof.Reference));
                Assert.Equal(proof.Status, calendar.StatusOn(day));
            }
        }
    }

}
