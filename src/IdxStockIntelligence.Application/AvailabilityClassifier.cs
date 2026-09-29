using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public static class AvailabilityClassifier
{
    public static DataAvailabilityStatus Classify(
        Instrument instrument,
        MarketSession session,
        bool providerRowPresent)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(session);

        var boundary = InstrumentBoundaries.Classify(instrument,session.Date);
        if (boundary == InstrumentBoundaryState.PreListing)
        {
            return DataAvailabilityStatus.PreListing;
        }

        if (boundary == InstrumentBoundaryState.PostDelisting)
        {
            return DataAvailabilityStatus.PostDelisting;
        }

        return session.Status switch
        {
            MarketSessionStatus.Holiday => DataAvailabilityStatus.Holiday,
            MarketSessionStatus.Suspension => DataAvailabilityStatus.Suspension,
            MarketSessionStatus.NoTrade => DataAvailabilityStatus.NoTrade,
            MarketSessionStatus.Trading when providerRowPresent => DataAvailabilityStatus.Available,
            MarketSessionStatus.Trading when instrument.ListedOn is null => DataAvailabilityStatus.Unknown,
            MarketSessionStatus.Trading => DataAvailabilityStatus.MissingProviderRow,
            _ => DataAvailabilityStatus.Unknown
        };
    }
}
