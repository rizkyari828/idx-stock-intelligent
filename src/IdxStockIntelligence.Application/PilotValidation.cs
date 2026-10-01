using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record SessionProof(DateOnly Date, ExchangeDayStatus Status, string Reference, DateTimeOffset KnownAt,
    DateTimeOffset? CompletedAt = null);
public sealed record PilotObservation(string Status, DailyBar? Bar, string? Reason = null);
public sealed record InstrumentSessionProof(InstrumentId Instrument, DateOnly Date,
    MarketSessionStatus Status, string Reference, DateTimeOffset KnownAt);

public static class PilotValidation
{
    public static PilotObservation Validate(Instrument instrument, DateOnly date, DailyBar? row,
        SessionProof? proof, DateTimeOffset knownAt, bool sourceError = false, InstrumentSessionProof? instrumentProof = null)
    {
        var boundary = InstrumentBoundaries.Classify(instrument,date);
        if (boundary == InstrumentBoundaryState.PreListing)
            return new("PRE_LISTING", null);
        if (boundary == InstrumentBoundaryState.PostDelisting)
            return new("POST_DELISTING", null);
        if (proof is null && ExchangeCalendarEvidence.Classify(date) == ExchangeDayStatus.Weekend)
            return new("CLOSED", null, "CLOSED_BY_CALENDAR");
        if (proof is null || proof.KnownAt > knownAt || string.IsNullOrWhiteSpace(proof.Reference))
            return new("SESSION_UNCONFIRMED", null);
        if (proof.Date != date)
            throw new ArgumentException("Session proof date does not match observation.", nameof(proof));
        if (proof.Status is ExchangeDayStatus.AnnouncedClosed or ExchangeDayStatus.ExceptionalClosure)
            return new("CLOSED", null);
        if (proof.Status != ExchangeDayStatus.ObservedTrading)
            return new("SESSION_UNCONFIRMED", null);
        if (instrumentProof is not null)
        {
            if (instrumentProof.Instrument != instrument.Id || instrumentProof.Date != date || string.IsNullOrWhiteSpace(instrumentProof.Reference))
                throw new ArgumentException("Invalid instrument-session proof.", nameof(instrumentProof));
            if (instrumentProof.KnownAt <= knownAt && instrumentProof.Status is MarketSessionStatus.Suspension or MarketSessionStatus.NoTrade)
                return new(instrumentProof.Status == MarketSessionStatus.Suspension ? "SUSPENDED" : "NO_TRADE", null);
        }
        if (sourceError) return new("SOURCE_ERROR", null);
        if (row is null) return new(instrument.ListedOn is null ? "UNKNOWN" : "MISSING", null,
            instrument.ListedOn is null ? "Listing boundary unknown." : null);
        if (row.InstrumentId != instrument.Id || row.SessionDate != date || row.Source.AvailableAt > knownAt)
            throw new ArgumentException("Row identity/date/availability does not match observation.", nameof(row));
        if (row.Volume == 0) return new("UNKNOWN", null, "Zero volume does not establish tradable/no-trade/suspension status.");
        return new("AVAILABLE", row, row.MarketSegment == "UNKNOWN" ? "Market segment unresolved; pilot evidence only." : null);
    }

    public static string ContentHash(DailyBar bar)
    {
        var content = JsonSerializer.Serialize(new[] { bar.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bar.Open.ToString("G29", CultureInfo.InvariantCulture), bar.High.ToString("G29", CultureInfo.InvariantCulture),
            bar.Low.ToString("G29", CultureInfo.InvariantCulture), bar.Close.ToString("G29", CultureInfo.InvariantCulture),
            bar.Volume.ToString(CultureInfo.InvariantCulture), bar.AdjustedClose?.ToString("G29", CultureInfo.InvariantCulture),
            bar.VolumeUnit, bar.VolumeBasis, bar.MarketSegment });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
