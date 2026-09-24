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

        if (session.Date < instrument.ListedOn)
        {
            return DataAvailabilityStatus.PreListing;
        }

        if (instrument.DelistedOn is not null && session.Date > instrument.DelistedOn)
        {
            return DataAvailabilityStatus.PostDelisting;
        }

        return session.Status switch
        {
            MarketSessionStatus.Holiday => DataAvailabilityStatus.Holiday,
            MarketSessionStatus.Suspension => DataAvailabilityStatus.Suspension,
            MarketSessionStatus.NoTrade => DataAvailabilityStatus.NoTrade,
            MarketSessionStatus.Trading when providerRowPresent => DataAvailabilityStatus.Available,
            MarketSessionStatus.Trading => DataAvailabilityStatus.MissingProviderRow,
            _ => DataAvailabilityStatus.Unknown
        };
    }
}
