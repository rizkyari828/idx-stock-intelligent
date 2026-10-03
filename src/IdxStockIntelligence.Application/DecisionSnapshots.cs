using System.Globalization;
using System.Text.Json;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record DecisionSnapshotIntent(DateOnly? Through, Guid? PortfolioId);
public sealed record DecisionSnapshotRequest(Guid RequestId, DateOnly? Through, Guid? PortfolioId)
{
    public DecisionSnapshotIntent Intent => new(Through, PortfolioId);
    public static DecisionSnapshotRequest Parse(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object) throw Invalid();
        var keys = body.EnumerateObject().Select(p => p.Name).ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length || keys.Any(k => k is not ("requestId" or "through" or "portfolioId"))
            || !body.TryGetProperty("requestId", out var id)) throw Invalid();
        var request = Uuid(id);
        DateOnly? through = null;
        if (body.TryGetProperty("through", out var date) && date.ValueKind != JsonValueKind.Null)
        {
            if (date.ValueKind != JsonValueKind.String || !DateOnly.TryParseExact(date.GetString(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) throw Invalid();
            through = parsed;
        }
        Guid? portfolio = body.TryGetProperty("portfolioId", out var p) && p.ValueKind != JsonValueKind.Null ? Uuid(p) : null;
        return new(request, through, portfolio);
    }
    private static Guid Uuid(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || !Guid.TryParseExact(value.GetString(), "D", out var id) || id == Guid.Empty) throw Invalid();
        return id;
    }
    private static ScreenerException Invalid() => new(400, "SNAPSHOT_REQUEST_INVALID");
    public ScreenerQuery Resolve(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, "Asia/Jakarta").DateTime);
        if (Through is { } date && date != today) throw new ScreenerException(400, "PROSPECTIVE_THROUGH_REQUIRED");
        try
        {
            return ScreenerQuery.Resolve(new Dictionary<string, string?>
            {
                ["through"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["portfolioId"] = PortfolioId?.ToString("D"), ["view"] = "all", ["limit"] = "100"
            }.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value), now);
        }
        catch (ArgumentException) { throw Invalid(); }
    }
}

public sealed record DecisionSnapshotHeader(Guid RunId, Guid RequestId, int SchemaVersion, string CaptureKind,
    DateTimeOffset CapturedAt, DateTimeOffset KnowledgeCutoff, DateTimeOffset RecordedAt, DateOnly Through,
    DateOnly? TargetSession, DateOnly HistoryAnchor, string PolicyId, string Universe, string? UniverseSnapshotId,
    Guid? PortfolioId, string InputHash, string SelectedDigest, string Status, int RowCount);
public sealed record DecisionSnapshotResult(IReadOnlyList<string> Reasons, ScreenerSummary Summary,
    IReadOnlyList<Guid> RankedCandidateIds, IReadOnlyList<Guid> AllViewIds, IReadOnlyList<Guid> ShortlistIds,
    IReadOnlyList<Guid> HeldIds, ScreenerContextDto MarketContext);
public sealed record DecisionSnapshotRowResult(IReadOnlyList<string> EligibilityReasons, IReadOnlyList<string> SetupReasons,
    ScreenerEpisode? Episode, decimal? PriorHigh20, decimal? DistanceToHighPercent, decimal? Ema20, decimal? Ema50,
    decimal? Atr14, decimal? AtrPercent, decimal? VolumeRatio20, decimal? Rs20Pp, decimal? Rs60Pp, string Trend,
    decimal? MonetaryLiquidity20Idr, bool? Stale, bool? NoTrade, string? TradingStatus, string DataQuality,
    IReadOnlyList<string> DataReasons, IReadOnlyDictionary<string, ScreenerFieldDto> FieldStates, ScreenerProvenanceDto Provenance);
public sealed record DecisionSnapshotRow(Guid InstrumentId, string? Symbol, bool Configured, bool Held, int? DiscoveryRank,
    string Eligibility, string Setup, bool SetupEvaluated, string? EpisodeId, DateOnly? MarketDate, decimal? Close,
    decimal? Shares, decimal? InvestedCost, decimal? AverageCost, string? Mandate, Guid? ThesisVersionId,
    int? ThesisVersion, bool? ThesisActive, DecisionSnapshotRowResult Result);
public sealed record DecisionSnapshotRun(DecisionSnapshotHeader Header, DecisionSnapshotResult Result, IReadOnlyList<DecisionSnapshotRow> Rows);
public sealed record DecisionSnapshotHistoryItem(DecisionSnapshotHeader Header, DecisionSnapshotRow Row);
public sealed record DecisionSnapshotPage<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record DecisionReferenceArchive(string Kind, bool Present, string? ContentSha256, long ByteLength);
public sealed record DecisionBarLink(Guid InstrumentId, DateOnly SessionDate, long RevisionNumber,
    DateTimeOffset KnownAt, string ContentHash, Guid RawArtifactId);
public sealed record DecisionListingLink(Guid InstrumentId, DateTimeOffset KnownAt, string ContentHash);
public sealed record DecisionReferenceLink(string SnapshotId, DateTimeOffset KnownAt, string ContentHash);
public sealed record DecisionPortfolioLink(Guid PortfolioId, IReadOnlyList<Guid> EventIds, IReadOnlyList<Guid> ThesisIds);
public sealed record DecisionSnapshotManifest(int SchemaVersion, ScreenerReadRequest Request,
    IReadOnlyList<Guid> ConfiguredIds, IReadOnlyList<Guid> HeldIds, IReadOnlyList<Guid> EvaluatedInstrumentIds,
    IReadOnlyList<DecisionBarLink> Bars, IReadOnlyList<DecisionListingLink> Listings,
    IReadOnlyList<DecisionReferenceLink> Universes, IReadOnlyList<DecisionReferenceLink> Instruments,
    IReadOnlyList<SessionProof> Sessions, IReadOnlyList<InstrumentSessionProof> InstrumentSessions,
    DecisionPortfolioLink? Portfolio, IReadOnlyList<DecisionReferenceArchive> Archives);

public static class DecisionSnapshotProjection
{
    private static readonly HashSet<string> Fields = ["close", "priorHigh20", "distanceToHighPercent", "ema20", "ema50",
        "atr14", "atrPercent", "volumeRatio20", "rs20Pp", "rs60Pp", "trend", "monetaryLiquidity20Idr", "tradingStatus"];
    public static DecisionSnapshotRow Row(ScreenerRowDto row, bool hasPortfolio, Position? position, ThesisVersion? thesis)
    {
        var heldThesis = row.Held ? thesis : null;
        return new(row.InstrumentId, row.Symbol, row.Configured, row.Held, row.DiscoveryRank, row.Eligibility,
            row.Setup, row.SetupEvaluated, row.Episode?.Id, row.MarketDate, row.Close,
            hasPortfolio ? position?.Shares ?? 0 : null, hasPortfolio ? position?.InvestedCost ?? 0 : null,
            hasPortfolio ? position?.AverageCost : null, row.Mandate?.ToString(), heldThesis?.Id,
            heldThesis?.Version, heldThesis?.Active,
            new(row.EligibilityReasons, row.SetupReasons, row.Episode, row.PriorHigh20, row.DistanceToHighPercent,
                row.Ema20, row.Ema50, row.Atr14, row.AtrPercent, row.VolumeRatio20, row.Rs20Pp, row.Rs60Pp, row.Trend,
                row.MonetaryLiquidity20Idr, row.Stale, row.NoTrade, row.TradingStatus, row.DataQuality, row.DataReasons,
                row.FieldStates.Where(p => Fields.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value), row.Provenance));
    }
}

public sealed record DecisionSnapshotCursor(DateTimeOffset CapturedAt, Guid RunId, string Context);
public sealed record DecisionSnapshotListQuery(int Limit, Guid? PortfolioId, DecisionSnapshotCursor? Cursor, string Context)
{
    public static DecisionSnapshotListQuery Parse(IReadOnlyDictionary<string, string?> values, Guid? instrumentId = null)
    {
        try
        {
            if (values.Keys.Any(k => k is not ("limit" or "cursor" or "portfolioId")) || instrumentId is not null && values.ContainsKey("portfolioId")) throw new ArgumentException("Invalid snapshot query.");
            var limit = 20;
            if (values.TryGetValue("limit", out var n) && (!int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 100)) throw new ArgumentException("Invalid snapshot query.");
            Guid? portfolio = null;
            if (values.TryGetValue("portfolioId", out var id))
            {
                if (!Guid.TryParseExact(id, "D", out var parsed) || parsed == Guid.Empty) throw new ArgumentException("Invalid snapshot query.");
                portfolio = parsed;
            }
            var context = instrumentId is { } stock ? "instrument/" + stock.ToString("D") : "recent/" + (portfolio?.ToString("D") ?? "all");
            DecisionSnapshotCursor? cursor = null;
            if (values.TryGetValue("cursor", out var encoded))
            {
                if (string.IsNullOrEmpty(encoded) || encoded.Length > 512 || encoded.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))) throw new ArgumentException("Invalid snapshot query.");
                var base64 = encoded.Replace('-', '+').Replace('_', '/');
                using var json = JsonDocument.Parse(Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '=')));
                if (json.RootElement.ValueKind != JsonValueKind.Object || json.RootElement.EnumerateObject().Count() != 3
                    || json.RootElement.EnumerateObject().Select(p => p.Name).Distinct().Count() != 3) throw new ArgumentException("Invalid snapshot query.");
                cursor = json.RootElement.Deserialize<DecisionSnapshotCursor>(ScreenerReferences.JsonOptions);
                if (cursor is null || cursor.Context != context || cursor.RunId == Guid.Empty
                    || cursor.CapturedAt < new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero)) throw new ArgumentException("Invalid snapshot query.");
            }
            return new(limit, portfolio, cursor, context);
        }
        catch (Exception e) when (e is ArgumentException or JsonException or FormatException or InvalidOperationException)
        { throw new ScreenerException(400, "SNAPSHOT_QUERY_INVALID"); }
    }
    public string Encode(DecisionSnapshotHeader header) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(
        new DecisionSnapshotCursor(header.CapturedAt, header.RunId, Context), ScreenerReferences.JsonOptions)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
