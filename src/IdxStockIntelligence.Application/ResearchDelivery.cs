using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IdxStockIntelligence.Application;

// Transport-only delivery vocabulary. Semantic normalization stays in ResearchQuery/
// ResearchEvaluation; this type only adds the frozen delivery parameters.
public sealed record ResearchDeliveryRequest(ResearchQuery Query, bool ExplicitCutoff, int Limit,
    ResearchCursor? Cursor, string? DatasetId, string Format)
{
    public bool Export => string.Equals(Format, ResearchDelivery.ExportFormat, StringComparison.Ordinal);
}

public sealed record ResearchCursorWire(DateTimeOffset CapturedAt, Guid RunId, Guid InstrumentId, string Context);

// Opaque keyset continuation for the frozen canonical order. The context is the datasetId,
// so a cursor cannot be replayed against another query, cutoff or ordering domain.
public sealed record ResearchCursor(DateTimeOffset CapturedAt, Guid RunId, Guid InstrumentId, string Context)
{
    public static ResearchCursor Decode(string encoded)
    {
        if (encoded.Length is 0 or > ResearchDelivery.MaximumCursorLength
            || encoded.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))) throw Invalid();
        try
        {
            var base64 = encoded.Replace('-', '+').Replace('_', '/');
            using var json = JsonDocument.Parse(Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '=')));
            if (json.RootElement.ValueKind != JsonValueKind.Object || json.RootElement.EnumerateObject().Count() != 4
                || json.RootElement.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != 4)
                throw Invalid();
            var wire = json.RootElement.Deserialize<ResearchCursorWire>(ScreenerReferences.JsonOptions);
            if (wire is null || wire.RunId == Guid.Empty || wire.InstrumentId == Guid.Empty || string.IsNullOrEmpty(wire.Context)
                || wire.CapturedAt < new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero)) throw Invalid();
            return new(wire.CapturedAt.ToUniversalTime(), wire.RunId, wire.InstrumentId, wire.Context);
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException or ArgumentException)
        { throw Invalid(); }
    }
    public string Encode() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(
        new ResearchCursorWire(CapturedAt, RunId, InstrumentId, Context), ScreenerReferences.JsonOptions))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static ScreenerException Invalid() => new(400, "RESEARCH_QUERY_INVALID");
}

public sealed record ResearchIdentityQuery(string CaptureFrom, string CaptureTo, string Cutoff, string Context,
    string? PortfolioId, int HorizonSessions, string Cohort, string GroupBy, string? InstrumentId, string? EpisodeId);
public sealed record ResearchIdentityText(int Ordinal, string Text);
public sealed record ResearchIdentityCount(int Ordinal, string Cohort, int N);
public sealed record ResearchIdentityEpisode(string Id, string StartDate, int AgeSessions, string? ConfirmationDate,
    string? EndDate, string? EndReason, int? ConfirmedAgeSessions, string? TriggerPrice, string? WatchThreshold);
public sealed record ResearchIdentityOutcome(string RunId, string InstrumentId, int HorizonSessions,
    string OutcomePolicyId, int SchemaVersion, string? AnchorMarketDate, string? AnchorClose, string HorizonMarketDate,
    string? HorizonClose, string State, string? Reason, string? PriceReturnPct, string OutcomeKnownAt, string RecordedAt);
public sealed record ResearchIdentityObservation(int Ordinal, string RunId, string InstrumentId, string? Symbol,
    bool Configured, bool Held, string Eligibility, string Setup, bool SetupEvaluated,
    IReadOnlyList<ResearchIdentityText> EligibilityReasons, IReadOnlyList<ResearchIdentityText> SetupReasons,
    ResearchIdentityEpisode? Episode, string? MarketDate, string? Close, string Cohort, string Partition,
    string Resolution, string? ResearchReason, ResearchIdentityOutcome? Outcome);
public sealed record ResearchIdentityRun(int Ordinal, string RunId, string CapturedAt, string KnowledgeCutoff,
    string RecordedAt, string Through, string? TargetSession, string HistoryAnchor, string PolicyId, int SchemaVersion,
    string CaptureKind, string Universe, string? UniverseSnapshotId, string? PortfolioId, string Status, int RowCount,
    string InputHash, string SelectedDigest, string MarketTrend, string MarketVolatility, string? MarketContextDate,
    IReadOnlyList<ResearchIdentityText> MarketContextReasons);
public sealed record ResearchIdentityPreimage(int ProjectionSchemaVersion, string EvaluationPolicyId,
    string CapturePolicyId, int CaptureSchemaVersion, string OutcomePolicyId, int OutcomeSchemaVersion,
    ResearchIdentityQuery Query, int BaseObservationCount, int FilteredOutObservationCount, int EmptyRunCount,
    IReadOnlyList<ResearchIdentityCount> BaseCohortCounts, IReadOnlyList<ResearchIdentityRun> Runs,
    IReadOnlyList<ResearchIdentityObservation> Observations);

public sealed record ResearchCountsProjection(int N, int A, int T, int R, int U, int TAnchor, int TData, int TBasis,
    int P, int Z, int M);
public sealed record ResearchMetricsProjection(string? Median, string? Mean, string? Minimum, string? Maximum,
    string? PositiveProportion, string? AvailableCoverage);
public sealed record ResearchDiversityProjection(int Runs, int Instruments, int TargetSessions,
    int NullTargetObservations, int EpisodeLinkedObservations, int NoEpisodeObservations, int EpisodeKeys,
    int ExtraEpisodeCaptures, int AdditionalSessionCaptures);
public sealed record ResearchSummaryProjection(ResearchCountsProjection Counts, ResearchMetricsProjection Metrics,
    ResearchDiversityProjection Diversity, ResearchDiversityProjection AvailableDiversity,
    IReadOnlyList<ResearchWarning> Warnings);
public sealed record ResearchGroupProjection(string Cohort, string Partition, ResearchSummaryProjection Summary);

public sealed record ResearchFreshnessProjection(string? LatestCapturedAt, string? LatestOutcomeKnownAt,
    string? LatestOutcomeRecordedAt, int UnresolvedCount, string GeneratedAt);
public sealed record ResearchDatasetProjection(int ProjectionSchemaVersion, string EvaluationPolicyId,
    string CapturePolicyId, int CaptureSchemaVersion, string OutcomePolicyId, int OutcomeSchemaVersion,
    string CaptureFrom, string CaptureTo, string Cutoff, string Context, string? PortfolioId, int HorizonSessions,
    string Cohort, string GroupBy, string? InstrumentId, string? EpisodeId, string DatasetId, int BaseRunCount,
    int BaseObservationCount, int SelectedObservationCount, int FilteredOutObservationCount, int EmptyRunCount,
    IReadOnlyList<ResearchIdentityCount> BaseCohortCounts, IReadOnlyList<ResearchIdentityRun> Runs,
    ResearchFreshnessProjection Freshness);
public sealed record ResearchPageInfo(int Limit, int Returned, int Total, string? NextCursor);
public sealed record ResearchPageResponse(ResearchDatasetProjection Dataset, ResearchSummaryProjection Summary,
    IReadOnlyList<ResearchGroupProjection> Cohorts, IReadOnlyList<ResearchIdentityObservation> Observations,
    ResearchPageInfo Page);
public sealed record ResearchExportEnvelope(string DatasetId, ResearchIdentityPreimage Preimage,
    ResearchSummaryProjection Summary, IReadOnlyList<ResearchGroupProjection> Cohorts);

public static class ResearchDelivery
{
    public const int DefaultLimit = 50;
    public const int MaximumLimit = 100;
    public const int MaximumCursorLength = 512;
    public const int MaximumPageBytes = 2 * 1024 * 1024;
    public const int MaximumExportBytes = 32 * 1024 * 1024;
    public const string PageFormat = "PAGE";
    public const string ExportFormat = "EXPORT_JSON";
    private static readonly string[] Keys = ["captureFrom", "captureTo", "cutoff", "portfolioId", "horizonSessions",
        "cohort", "groupBy", "instrumentId", "episodeId", "limit", "cursor", "datasetId", "format"];
    private static readonly Regex Timestamp = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions DeliveryJson = new(JsonSerializerDefaults.Web);

    public static ResearchDeliveryRequest Parse(IReadOnlyDictionary<string, string?> values, DateTimeOffset now)
    {
        if (values.Keys.Any(k => !Keys.Contains(k, StringComparer.Ordinal))) throw Invalid();
        if (values.Values.Any(string.IsNullOrEmpty)) throw Invalid();
        string? Get(string key) => values.GetValueOrDefault(key);
        var explicitCutoff = Get("cutoff") is not null;
        if (Get("captureFrom") is not { } fromText
            || !DateOnly.TryParseExact(fromText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            || Get("captureTo") is not { } toText
            || !DateOnly.TryParseExact(toText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)) throw Invalid();
        var cutoff = now;
        if (Get("cutoff") is { } cutoffText && (!Timestamp.IsMatch(cutoffText)
            || !DateTimeOffset.TryParse(cutoffText, CultureInfo.InvariantCulture, DateTimeStyles.None, out cutoff))) throw Invalid();
        Guid? portfolio = null;
        if (Get("portfolioId") is { } portfolioText)
        { if (!Guid.TryParseExact(portfolioText, "D", out var parsed) || parsed == Guid.Empty) throw Invalid(); portfolio = parsed; }
        var horizon = 5;
        if (Get("horizonSessions") is { } horizonText
            && !int.TryParse(horizonText, NumberStyles.None, CultureInfo.InvariantCulture, out horizon)) throw Invalid();
        var cohort = ResearchCohort.ALL;
        if (Get("cohort") is { } cohortText)
        { try { cohort = ResearchQuery.ParseCohort(cohortText); } catch (ArgumentException) { throw Invalid(); } }
        var groupBy = ResearchGrouping.NONE;
        if (Get("groupBy") is { } groupText)
        { try { groupBy = ResearchQuery.ParseGrouping(groupText); } catch (ArgumentException) { throw Invalid(); } }
        Guid? instrument = null;
        if (Get("instrumentId") is { } instrumentText)
        { if (!Guid.TryParseExact(instrumentText, "D", out var parsed) || parsed == Guid.Empty) throw Invalid(); instrument = parsed; }
        var limit = DefaultLimit;
        if (Get("limit") is { } limitText
            && (!int.TryParse(limitText, NumberStyles.None, CultureInfo.InvariantCulture, out limit)
                || limit is < 1 or > MaximumLimit)) throw Invalid();
        var cursor = Get("cursor") is { } cursorText ? ResearchCursor.Decode(cursorText) : null;
        string? datasetId = null;
        if (Get("datasetId") is { } pin)
        { if (!ScreenerReferences.IsHash(pin)) throw Invalid(); datasetId = pin; }
        var format = PageFormat;
        if (Get("format") is { } formatText)
        { if (formatText is not (PageFormat or ExportFormat)) throw Invalid(); format = formatText; }
        if (cursor is not null && (!explicitCutoff || datasetId is null)) throw Invalid();
        if (format == ExportFormat && (!explicitCutoff || datasetId is null || cursor is not null || Get("limit") is not null))
            throw Invalid();
        ResearchQuery query;
        try
        {
            query = new ResearchQuery(from, to, cutoff, portfolio, horizon, cohort, groupBy, instrument, Get("episodeId")).Normalize(now);
        }
        catch (ArgumentException) { throw Invalid(); }
        return new(query, explicitCutoff, limit, cursor, datasetId, format);
    }

    public static ResearchIdentityPreimage Preimage(ResearchResult result)
    {
        var query = result.Query.Canonical();
        var identityQuery = new ResearchIdentityQuery(query.CaptureFrom, query.CaptureTo, query.Cutoff,
            query.PortfolioId is null ? "DISCOVERY" : "PORTFOLIO", query.PortfolioId, query.HorizonSessions, query.Cohort,
            query.GroupBy, query.InstrumentId, query.EpisodeId);
        var runs = result.BaseRuns.Select(Run).ToArray();
        var observations = result.Observations.Select(Observation).ToArray();
        var cohorts = result.BaseCohortCounts
            .Select((count, ordinal) => new ResearchIdentityCount(ordinal, count.Cohort.ToString(), count.N)).ToArray();
        return new(1, ResearchEvaluation.PolicyId, ScreenerReadRequest.PolicyId, 1, OutcomeEvaluator.PolicyId, 1,
            identityQuery, result.BaseObservationCount, result.FilteredOutObservationCount, result.EmptyRunCount,
            cohorts, runs, observations);
    }

    public static string DatasetId(ResearchIdentityPreimage preimage, CancellationToken ct = default)
        => ScreenerReferences.Hash(preimage, ct);

    public static ResearchPageResponse Page(ResearchResult result, ResearchDeliveryRequest request,
        ResearchIdentityPreimage preimage, string datasetId, DateTimeOffset generatedAt)
    {
        var start = 0;
        if (request.Cursor is { } cursor)
        {
            if (!string.Equals(cursor.Context, datasetId, StringComparison.Ordinal)) throw Invalid();
            var index = IndexOf(result, cursor);
            if (index < 0 || index >= result.Observations.Count - 1) throw Invalid();
            start = index + 1;
        }
        var page = preimage.Observations.Skip(start).Take(request.Limit).ToArray();
        var next = start + page.Length < result.Observations.Count
            ? NextCursor(result, datasetId, result.Observations[start + page.Length - 1]) : null;
        var outcomes = result.Observations.Select(c => c.Outcome).OfType<ResearchOutcome>().ToArray();
        var dataset = new ResearchDatasetProjection(1, ResearchEvaluation.PolicyId, ScreenerReadRequest.PolicyId, 1,
            OutcomeEvaluator.PolicyId, 1, preimage.Query.CaptureFrom, preimage.Query.CaptureTo, preimage.Query.Cutoff,
            preimage.Query.Context, preimage.Query.PortfolioId, preimage.Query.HorizonSessions, preimage.Query.Cohort,
            preimage.Query.GroupBy, preimage.Query.InstrumentId, preimage.Query.EpisodeId, datasetId,
            result.BaseRuns.Count, result.BaseObservationCount, result.Summary.Counts.N,
            result.FilteredOutObservationCount, result.EmptyRunCount, preimage.BaseCohortCounts, preimage.Runs,
            new(result.BaseRuns.Count == 0 ? null : Stamp(result.BaseRuns.Max(r => r.Header.CapturedAt)),
                outcomes.Length == 0 ? null : outcomes.Max(o => Stamp(o.OutcomeKnownAt)),
                outcomes.Length == 0 ? null : outcomes.Max(o => Stamp(o.RecordedAt)), result.Summary.Counts.U,
                Stamp(generatedAt)));
        return new(dataset, Summary(result.Summary), Groups(result), page,
            new(request.Limit, page.Length, result.Summary.Counts.N, next));
    }

    public static byte[] PageBytes(ResearchPageResponse response)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, DeliveryJson);
        if (bytes.Length > MaximumPageBytes) throw Bounds();
        return bytes;
    }

    public static byte[] ExportBytes(ResearchResult result, ResearchIdentityPreimage preimage, string datasetId)
    {
        var envelope = new ResearchExportEnvelope(datasetId, preimage, Summary(result.Summary), Groups(result));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, DeliveryJson);
        if (bytes.Length > MaximumExportBytes) throw Bounds();
        return bytes;
    }

    private static int IndexOf(ResearchResult result, ResearchCursor cursor)
    {
        var runs = result.BaseRuns.ToDictionary(r => r.Header.RunId);
        for (var index = 0; index < result.Observations.Count; index++)
        {
            var observation = result.Observations[index].Observation;
            if (observation.RunId == cursor.RunId && observation.InstrumentId == cursor.InstrumentId
                && runs.TryGetValue(cursor.RunId, out var run)
                && run.Header.CapturedAt.ToUniversalTime() == cursor.CapturedAt) return index;
        }
        return -1;
    }

    private static string NextCursor(ResearchResult result, string datasetId, ResearchCell cell)
    {
        var run = result.BaseRuns.Single(r => r.Header.RunId == cell.Observation.RunId);
        return new ResearchCursor(run.Header.CapturedAt.ToUniversalTime(), cell.Observation.RunId,
            cell.Observation.InstrumentId, datasetId).Encode();
    }

    private static ResearchIdentityRun Run(ResearchRun run, int ordinal)
    {
        var header = run.Header;
        return new(ordinal, header.RunId.ToString("D"), Stamp(header.CapturedAt), Stamp(header.KnowledgeCutoff),
            Stamp(header.RecordedAt), Date(header.Through), header.TargetSession is { } target ? Date(target) : null,
            Date(header.HistoryAnchor), header.PolicyId, header.SchemaVersion, header.CaptureKind, header.Universe,
            header.UniverseSnapshotId, header.PortfolioId?.ToString("D"), header.Status, header.RowCount,
            header.InputHash, header.SelectedDigest, run.MarketTrend, run.MarketVolatility,
            run.MarketContextDate is { } date ? Date(date) : null,
            run.MarketContextReasons.Select((text, index) => new ResearchIdentityText(index, text)).ToArray());
    }

    private static ResearchIdentityObservation Observation(ResearchCell cell, int ordinal)
    {
        var observation = cell.Observation;
        return new(ordinal, observation.RunId.ToString("D"), observation.InstrumentId.ToString("D"),
            observation.Symbol, observation.Configured, observation.Held, observation.Eligibility, observation.Setup,
            observation.SetupEvaluated,
            observation.EligibilityReasons.Select((text, index) => new ResearchIdentityText(index, text)).ToArray(),
            observation.SetupReasons.Select((text, index) => new ResearchIdentityText(index, text)).ToArray(),
            Episode(observation.Episode), observation.MarketDate is { } day ? Date(day) : null,
            ResearchEvaluation.DecimalText(observation.Close), cell.Cohort.ToString(), cell.Partition, cell.Resolution,
            cell.ResearchReason, Outcome(cell.Outcome));
    }

    private static ResearchIdentityEpisode? Episode(ScreenerEpisode? episode) => episode is null ? null
        : new(episode.Id, Date(episode.StartDate), episode.AgeSessions,
            episode.ConfirmationDate is { } confirmation ? Date(confirmation) : null,
            episode.EndDate is { } end ? Date(end) : null, episode.EndReason, episode.ConfirmedAgeSessions,
            ResearchEvaluation.DecimalText(episode.TriggerPrice), ResearchEvaluation.DecimalText(episode.WatchThreshold));

    private static ResearchIdentityOutcome? Outcome(ResearchOutcome? outcome) => outcome is null ? null
        : new(outcome.RunId.ToString("D"), outcome.InstrumentId.ToString("D"), outcome.HorizonSessions,
            outcome.OutcomePolicyId, outcome.SchemaVersion,
            outcome.AnchorMarketDate is { } anchor ? Date(anchor) : null,
            ResearchEvaluation.DecimalText(outcome.AnchorClose), Date(outcome.HorizonMarketDate),
            ResearchEvaluation.DecimalText(outcome.HorizonClose), outcome.State, outcome.Reason,
            ResearchEvaluation.DecimalText(outcome.PriceReturnPct), Stamp(outcome.OutcomeKnownAt),
            Stamp(outcome.RecordedAt));

    private static ResearchSummaryProjection Summary(ResearchSummary summary) => new(
        new(summary.Counts.N, summary.Counts.A, summary.Counts.T, summary.Counts.R, summary.Counts.U,
            summary.Counts.TAnchor, summary.Counts.TData, summary.Counts.TBasis, summary.Counts.P, summary.Counts.Z,
            summary.Counts.M),
        new(ResearchEvaluation.DecimalText(summary.Metrics.Median), ResearchEvaluation.DecimalText(summary.Metrics.Mean),
            ResearchEvaluation.DecimalText(summary.Metrics.Minimum), ResearchEvaluation.DecimalText(summary.Metrics.Maximum),
            ResearchEvaluation.DecimalText(summary.Metrics.PositiveProportion),
            ResearchEvaluation.DecimalText(summary.Metrics.AvailableCoverage)),
        Diversity(summary.Diversity), Diversity(summary.AvailableDiversity), summary.Warnings);

    private static ResearchDiversityProjection Diversity(ResearchDiversity diversity) => new(diversity.Runs,
        diversity.Instruments, diversity.TargetSessions, diversity.NullTargetObservations,
        diversity.EpisodeLinkedObservations, diversity.NoEpisodeObservations, diversity.EpisodeKeys,
        diversity.ExtraEpisodeCaptures, diversity.AdditionalSessionCaptures);

    private static ResearchGroupProjection[] Groups(ResearchResult result) => result.Cohorts
        .Select(group => new ResearchGroupProjection(group.Cohort.ToString(), group.Partition, Summary(group.Summary))).ToArray();

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Stamp(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static ScreenerException Invalid() => new(400, "RESEARCH_QUERY_INVALID");
    private static ScreenerException Bounds() => new(503, "RESEARCH_BOUND_EXCEEDED");
}
