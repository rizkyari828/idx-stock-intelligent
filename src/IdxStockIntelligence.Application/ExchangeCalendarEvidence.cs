namespace IdxStockIntelligence.Application;

public enum ExchangeDayStatus
{
    Unknown,
    Weekend,
    AnnouncedClosed,
    ObservedTrading
}

public sealed record ExchangeDayEvidence(
    DateOnly Date,
    ExchangeDayStatus Status,
    string SourceReference);

public sealed class ExchangeCalendarEvidence
{
    private readonly SortedDictionary<DateOnly, ExchangeDayEvidence> _days = [];

    public bool Record(ExchangeDayEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (string.IsNullOrWhiteSpace(evidence.SourceReference))
        {
            throw new ArgumentException("Calendar evidence requires a source reference.", nameof(evidence));
        }

        if (evidence.Status is not (ExchangeDayStatus.AnnouncedClosed or ExchangeDayStatus.ObservedTrading))
        {
            throw new ArgumentException("Only sourced closure or trading evidence can be recorded.", nameof(evidence));
        }

        if (IsWeekend(evidence.Date) && evidence.Status == ExchangeDayStatus.ObservedTrading)
        {
            throw new ArgumentException("Weekend trading needs a separate exceptional-session policy.", nameof(evidence));
        }

        if (_days.TryGetValue(evidence.Date, out var existing))
        {
            if (existing == evidence)
            {
                return false;
            }

            throw new InvalidOperationException("Conflicting calendar evidence requires a revision policy.");
        }

        _days.Add(evidence.Date, evidence);
        return true;
    }

    public ExchangeDayStatus StatusOn(DateOnly date)
    {
        if (_days.TryGetValue(date, out var evidence))
        {
            return evidence.Status;
        }

        return IsWeekend(date) ? ExchangeDayStatus.Weekend : ExchangeDayStatus.Unknown;
    }

    public IReadOnlyList<ExchangeDayEvidence> Chronological() => _days.Values.ToArray();

    private static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
