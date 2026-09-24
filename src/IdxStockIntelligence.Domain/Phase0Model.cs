namespace IdxStockIntelligence.Domain;

public readonly record struct InstrumentId
{
    public InstrumentId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Instrument identity cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static InstrumentId New() => new(Guid.NewGuid());
}

public sealed record Instrument
{
    public Instrument(InstrumentId id, string issuerName, DateOnly listedOn, DateOnly? delistedOn = null)
    {
        if (string.IsNullOrWhiteSpace(issuerName))
        {
            throw new ArgumentException("Issuer name is required.", nameof(issuerName));
        }

        if (delistedOn is not null && delistedOn < listedOn)
        {
            throw new ArgumentException("Delisting cannot precede listing.", nameof(delistedOn));
        }

        Id = id;
        IssuerName = issuerName.Trim();
        ListedOn = listedOn;
        DelistedOn = delistedOn;
    }

    public InstrumentId Id { get; }
    public string IssuerName { get; }
    public DateOnly ListedOn { get; }
    public DateOnly? DelistedOn { get; }
}

public sealed record InstrumentSymbolHistory
{
    public InstrumentSymbolHistory(InstrumentId instrumentId, string symbol, DateOnly validFrom, DateOnly? validTo = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        if (validTo is not null && validTo < validFrom)
        {
            throw new ArgumentException("Symbol validity end cannot precede its start.", nameof(validTo));
        }

        InstrumentId = instrumentId;
        Symbol = symbol.Trim().ToUpperInvariant();
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    public InstrumentId InstrumentId { get; }
    public string Symbol { get; }
    public DateOnly ValidFrom { get; }
    public DateOnly? ValidTo { get; }
}

public enum MarketSessionStatus
{
    Trading,
    Holiday,
    Suspension,
    NoTrade
}

public enum DataAvailabilityStatus
{
    Unknown,
    Available,
    PreListing,
    PostDelisting,
    Holiday,
    Suspension,
    NoTrade,
    MissingProviderRow
}

public enum DataQualityStatus
{
    Unknown,
    Valid,
    Rejected,
    Degraded,
    Stale
}

public sealed record MarketSession(DateOnly Date, MarketSessionStatus Status, string? EvidenceReference = null);

public sealed record SourceReference
{
    public SourceReference(
        string sourceId,
        Guid rawArtifactId,
        DateTimeOffset fetchedAt,
        DateTimeOffset availableAt,
        string contentSha256)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source identity is required.", nameof(sourceId));
        }

        if (availableAt < fetchedAt)
        {
            throw new ArgumentException("Availability cannot precede fetch time.", nameof(availableAt));
        }

        if (contentSha256.Length != 64 || !contentSha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("A lowercase or uppercase SHA-256 hex digest is required.", nameof(contentSha256));
        }

        SourceId = sourceId;
        RawArtifactId = rawArtifactId;
        FetchedAt = fetchedAt;
        AvailableAt = availableAt;
        ContentSha256 = contentSha256.ToLowerInvariant();
    }

    public string SourceId { get; }
    public Guid RawArtifactId { get; }
    public DateTimeOffset FetchedAt { get; }
    public DateTimeOffset AvailableAt { get; }
    public string ContentSha256 { get; }
}

public sealed record DailyBar
{
    public DailyBar(
        InstrumentId instrumentId,
        DateOnly sessionDate,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        long volume,
        SourceReference source)
    {
        if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(open), "OHLC values must be positive; missing is not zero.");
        }

        if (high < open || high < close || high < low)
        {
            throw new ArgumentException("High must be at least open, close, and low.", nameof(high));
        }

        if (low > open || low > close)
        {
            throw new ArgumentException("Low must be no greater than open and close.", nameof(low));
        }

        if (volume < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume cannot be negative.");
        }

        InstrumentId = instrumentId;
        SessionDate = sessionDate;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        Source = source;
    }

    public InstrumentId InstrumentId { get; }
    public DateOnly SessionDate { get; }
    public decimal Open { get; }
    public decimal High { get; }
    public decimal Low { get; }
    public decimal Close { get; }
    public long Volume { get; }
    public SourceReference Source { get; }
}

public sealed record MarketObservation
{
    private MarketObservation(DataAvailabilityStatus availability, DailyBar? bar)
    {
        Availability = availability;
        Bar = bar;
    }

    public DataAvailabilityStatus Availability { get; }
    public DailyBar? Bar { get; }

    public static MarketObservation Available(DailyBar bar) => new(DataAvailabilityStatus.Available, bar);

    public static MarketObservation Unavailable(DataAvailabilityStatus availability)
    {
        if (availability == DataAvailabilityStatus.Available)
        {
            throw new ArgumentException("Available observations require a real bar.", nameof(availability));
        }

        return new MarketObservation(availability, null);
    }
}

public enum EligibilityStatus { Eligible, Ineligible, DataBlocked }
public enum SetupStatus { None, Watch, Confirmed, Failed }
public enum ThesisHealth { Intact, Challenged, Invalid, Unknown }
public enum DecisionAction { Hold, Review, Reduce, Exit }
public enum DecisionModifier { Trail, Extended, EventRisk, ExecutionConstrained }
