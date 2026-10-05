using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdxStockIntelligence.Application;
using Npgsql;
using NpgsqlTypes;

namespace IdxStockIntelligence.Infrastructure;

public sealed class ResearchService(NpgsqlDataSource dataSource, TimeProvider? clock = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(DecisionSnapshotService.JsonOptions)
    { Converters = { new ExactNumericConverter() } };
    private static NpgsqlCommand Command(string sql, NpgsqlConnection c, NpgsqlTransaction t)
        => new(sql, c, t) { CommandTimeout = 15 };
    private static void Require(bool valid, string code = "RESEARCH_UNAVAILABLE")
    { if (!valid) throw new InvalidOperationException(code); }
    private static T Decode<T>(string json, int bound)
    {
        Require(Encoding.UTF8.GetByteCount(json) <= bound, "RESEARCH_BOUND_EXCEEDED");
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException("RESEARCH_UNAVAILABLE");
    }

    // Like ScreenerBarEvidence's strict numeric boundary: decimal.Parse can round;
    // compare the original PostgreSQL value with a lossless, plain decimal rendering.
    private static decimal ExactNumeric(string text)
    {
        var value = decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        static string Normalize(string s)
        {
            var negative = s.StartsWith('-');
            var parts = s.TrimStart('+', '-').Split('.');
            var integer = parts[0].TrimStart('0');
            if (integer.Length == 0) integer = "0";
            var fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : "";
            return (negative && (integer != "0" || fraction.Length > 0) ? "-" : "")
                + integer + (fraction.Length == 0 ? "" : "." + fraction);
        }
        Require(Normalize(text) == Normalize(value.ToString("0.############################", CultureInfo.InvariantCulture)));
        return value;
    }
    private sealed class ExactNumericConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String) return ExactNumeric(reader.GetString()!);
            if (reader.TokenType != JsonTokenType.Number) throw new JsonException();
            using var json = JsonDocument.ParseValue(ref reader);
            return ExactNumeric(json.RootElement.GetRawText());
        }
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
            => throw new NotSupportedException("Read-only numeric materialization.");
    }

    public async Task<ResearchResult> ReadAsync(ResearchQuery query, CancellationToken ct)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        query = query.Normalize(now);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60)); var token = deadline.Token;
        try
        {
            await using var c = await dataSource.OpenConnectionAsync(token);
            await using var t = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            await using (var ro = Command("SET TRANSACTION READ ONLY", c, t)) await ro.ExecuteNonQueryAsync(token);
            var runs = new List<ResearchRun>();
            var declared = new Dictionary<Guid, Guid[]>();
            long materializedBytes = 0;
            T Materialize<T>(string json, int bound)
            {
                materializedBytes = checked(materializedBytes + Encoding.UTF8.GetByteCount(json));
                Require(materializedBytes <= 32 * 1024 * 1024, "RESEARCH_BOUND_EXCEEDED");
                return Decode<T>(json, bound);
            }
            // Jakarta civil dates become UTC instant bounds; indexed captured_at is not cast.
            var from = new DateTimeOffset(query.CaptureFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
            var until = new DateTimeOffset(query.CaptureTo.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
            await using (var command = Command($"""
                SELECT r.policy_id,r.schema_version,r.capture_kind,r.universe,
                    jsonb_build_object('header',{DecisionSnapshotService.HeaderSql},
                        'marketTrend',r.result->'marketContext'->'trend',
                        'marketVolatility',r.result->'marketContext'->'volatility',
                        'marketContextDate',r.result->'marketContext'->'marketDate',
                        'marketContextReasons',r.result->'marketContext'->'reasons')::text,
                    (r.evidence_manifest->'evaluatedInstrumentIds')::text,
                    (r.result->'marketContext' ?& ARRAY['trend','volatility','marketDate','reasons'])
                FROM decision_snapshot_run r
                WHERE r.captured_at >= $1 AND r.captured_at < $2
                    AND r.captured_at <= $3 AND r.knowledge_cutoff <= $3 AND r.recorded_at <= $3
                    AND {(query.PortfolioId is null ? "r.portfolio_id IS NULL" : "r.portfolio_id=$4")}
                ORDER BY r.captured_at,r.run_id LIMIT 1001
                """, c, t))
            {
                command.Parameters.AddWithValue(from); command.Parameters.AddWithValue(until); command.Parameters.AddWithValue(query.Cutoff);
                if (query.PortfolioId is { } portfolio) command.Parameters.AddWithValue(portfolio);
                await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    Require(runs.Count < ResearchEvaluation.MaximumRuns, "RESEARCH_BOUND_EXCEEDED");
                    Require(reader.GetString(0) == "screener-v0.1.0" && reader.GetInt16(1) == 1
                        && reader.GetString(2) == "PROSPECTIVE_CAPTURE" && reader.GetString(3) == "PILOT", "RESEARCH_POLICY_VERSION_UNAVAILABLE");
                    Require(!reader.IsDBNull(6) && reader.GetBoolean(6));
                    var run = Materialize<ResearchRun>(reader.GetString(4), 2 * 1024 * 1024);
                    Require(!reader.IsDBNull(5));
                    var ids = Materialize<Guid[]>(reader.GetString(5), 32768);
                    Require(ids.Length == run.Header.RowCount && ids.Distinct().Count() == ids.Length && ids.All(id => id != Guid.Empty));
                    declared.Add(run.Header.RunId, ids); runs.Add(run);
                }
            }
            var rows = new List<ResearchObservation>(); var outcomes = new List<ResearchOutcome>();
            if (runs.Count > 0)
            {
                // No observation drilldown in SQL: all base rows are bounded/validated first.
                // Both right-side clocks stay in ON, so missing/later outcomes leave rows intact.
                await using var command = Command("""
                    SELECT jsonb_build_object('runId',s.run_id,'instrumentId',s.instrument_id,'symbol',s.symbol,
                        'configured',s.configured,'held',s.held,'eligibility',s.eligibility,'setup',s.setup,
                        'setupEvaluated',s.setup_evaluated,'eligibilityReasons',s.result->'eligibilityReasons',
                        'setupReasons',s.result->'setupReasons','episode',s.result->'episode',
                        'marketDate',s.market_date,'close',s.close::text)::text,s.episode_id,
                        s.result ?& ARRAY['eligibilityReasons','setupReasons','episode'],
                        o.outcome_policy_id,o.schema_version,
                        CASE WHEN o.run_id IS NULL THEN NULL ELSE jsonb_build_object('runId',o.run_id,
                            'instrumentId',o.instrument_id,'horizonSessions',o.horizon_sessions,
                            'outcomePolicyId',o.outcome_policy_id,'schemaVersion',o.schema_version,
                            'anchorMarketDate',o.anchor_market_date,'anchorClose',o.anchor_close::text,
                            'horizonMarketDate',o.horizon_market_date,'horizonClose',o.horizon_close::text,
                            'state',o.outcome_state,'reason',o.reason,'priceReturnPct',o.price_return_pct::text,
                            'outcomeKnownAt',o.outcome_known_at,'recordedAt',o.recorded_at)::text END
                    FROM decision_snapshot_row s
                    LEFT JOIN decision_snapshot_outcome o ON o.run_id=s.run_id AND o.instrument_id=s.instrument_id
                        AND o.horizon_sessions=$2 AND o.outcome_known_at <= $3 AND o.recorded_at <= $3
                    WHERE s.run_id=ANY($1)
                    ORDER BY s.run_id,s.instrument_id LIMIT 10001
                    """, c, t);
                command.Parameters.AddWithValue(runs.Select(r => r.Header.RunId).ToArray());
                command.Parameters.AddWithValue(query.HorizonSessions); command.Parameters.AddWithValue(query.Cutoff);
                await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    Require(rows.Count < ResearchEvaluation.MaximumObservations, "RESEARCH_BOUND_EXCEEDED");
                    if (!reader.IsDBNull(3)) Require(reader.GetString(3) == "outcome-v0.1.0" && reader.GetInt16(4) == 1,
                        "RESEARCH_POLICY_VERSION_UNAVAILABLE");
                    Require(!reader.IsDBNull(2) && reader.GetBoolean(2));
                    var row = Materialize<ResearchObservation>(reader.GetString(0), 2 * 1024 * 1024);
                    Require((reader.IsDBNull(1) ? null : reader.GetString(1)) == row.Episode?.Id);
                    rows.Add(row);
                    if (!reader.IsDBNull(5))
                    {
                        var outcome = Materialize<ResearchOutcome>(reader.GetString(5), 2 * 1024 * 1024);
                        // Validate the frozen stored vocabulary, not the underlying market evidence.
                        Require(outcome.State switch
                        {
                            "AVAILABLE" => outcome.Reason is null,
                            "ANCHOR_UNAVAILABLE" => outcome.Reason is "CAPTURED_CLOSE_MISSING" or "STALE_ANCHOR"
                                or "CAPTURED_OBSERVATION_INVALID" or "CAPTURE_SESSION_UNCONFIRMED"
                                or "CAPTURED_PRICE_BASIS_UNVERIFIED" or "CAPTURED_STATUS_UNVERIFIED" or "NONPROSPECTIVE_SESSION_BASE",
                            "DATA_UNAVAILABLE" => outcome.Reason is "SUSPENDED_AT_HORIZON" or "NO_TRADE_AT_HORIZON"
                                or "POST_DELISTING" or "SPECIAL_REGIME_UNSUPPORTED" or "ENDPOINT_CLASSIFICATION_UNSUPPORTED",
                            "BASIS_UNCERTAIN" => outcome.Reason is "UNIT_CHANGING_EVENT" or "CAPITAL_ACTION_UNSUPPORTED"
                                or "SECURITY_CONVERSION_UNSUPPORTED" or "PRICE_CONVENTION_UNSUPPORTED",
                            _ => false
                        });
                        outcomes.Add(outcome);
                    }
                }
            }
            var byRun = rows.GroupBy(r => r.RunId).ToDictionary(g => g.Key, g => g.Select(r => r.InstrumentId).ToHashSet());
            foreach (var run in runs)
                Require(byRun.GetValueOrDefault(run.Header.RunId, []).SetEquals(declared[run.Header.RunId]));
            var result = ResearchEvaluation.Evaluate(query, runs, rows, outcomes, now, token);
            await t.CommitAsync(token);
            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new InvalidOperationException("RESEARCH_UNAVAILABLE"); }
        catch (InvalidOperationException e) when (e.Message is "RESEARCH_BOUND_EXCEEDED" or "RESEARCH_POLICY_VERSION_UNAVAILABLE")
        { throw; }
        catch (Exception e) when (e is NpgsqlException or TimeoutException or InvalidOperationException or ArgumentException
            or OverflowException or JsonException or FormatException or KeyNotFoundException or InvalidCastException)
        { ct.ThrowIfCancellationRequested(); throw new InvalidOperationException("RESEARCH_UNAVAILABLE"); }
    }
}
