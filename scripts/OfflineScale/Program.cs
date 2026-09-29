using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;

// Opt-in, synthetic data only. Production worker/panel configuration is never touched.
if (args.Length is < 2 or > 3 || (args.Length == 3 && args[2] != "verify-restore")
    || !int.TryParse(args[0], out var count) || count is < 1 or > 924
    || !PilotDatabase.DatabaseName.StartsWith("idx_scale_", StringComparison.Ordinal))
    throw new ArgumentException("Requires 1..924 equities, output path and an owned idx_scale_ database.");
var output = Path.GetFullPath(args[1]);
if (!output.StartsWith(Path.GetFullPath("data/collector-output/scale") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
    throw new ArgumentException("Report must remain in ignored scale storage.");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var timings = new Dictionary<string, double>();
T Measure<T>(string name, Func<T> action)
{
    Console.Error.WriteLine("Starting " + name);
    var timer = Stopwatch.StartNew(); var result = action();
    timings[name] = timer.Elapsed.TotalSeconds;
    Console.Error.WriteLine($"Completed {name}: {timings[name]:F3}s"); return result;
}
void Check(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
Guid Identity(string text) => new(SHA256.HashData(Encoding.UTF8.GetBytes(text))[..16]);
var start = new DateOnly(2025, 1, 6);
var dates = Enumerable.Range(0, 400).Select(start.AddDays)
    .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Take(250).ToArray();
// Synthetic knowledge clocks, deliberately distinct from dates. No real calendar claim.
var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
var t1 = t0.AddDays(1); var t2 = t1.AddDays(1);
var proofs = dates.Select(d => new SessionProof(d, ExchangeDayStatus.ObservedTrading,
    "https://synthetic.example/completed-session", t0.AddHours(-1), t0.AddHours(-2))).ToArray();
var instruments = Enumerable.Range(0, count + 1).Select(i => new Instrument(
    new(Identity($"offline-scale-{i}")), i == count ? "SYNTHETIC_BENCHMARK" : $"SYN{i:D4}", start)).ToArray();
var benchmark = instruments[^1].Id;
PilotFeatureResult[] Scoped(IReadOnlyList<DailyBarRevision> history, DateTimeOffset cutoff)
{
    var groups = history.GroupBy(r => r.Bar.InstrumentId).ToDictionary(g => g.Key, g => g.ToArray());
    return instruments.Take(count).Select(i => PilotFeatures.Calculate(groups[i.Id].Concat(groups[benchmark]),
        i.Id, benchmark, proofs, cutoff)).ToArray();
}
if (args.Length == 3)
{
    using var expected = JsonDocument.Parse(File.ReadAllText(output));
    var restored = Measure("restore_read_features", () => {
        var history = PilotDatabase.ReadRevisions();
        Check(Digest(JsonSerializer.SerializeToUtf8Bytes(Scoped(history,t1))) == expected.RootElement.GetProperty("latest_feature_hash").GetString(), "Restored latest features differ");
        return Scoped(history,t0);
    });
    Check(Digest(JsonSerializer.SerializeToUtf8Bytes(restored)) == expected.RootElement.GetProperty("feature_hash").GetString(), "Restored prior features differ");
    Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS", timings, provider_requests = 0 }));
    return;
}
var rawRoot = Path.Combine("data/raw/scale", PilotDatabase.DatabaseName);
var archiver = new RawArtifactArchiver(rawRoot);
var bars = new List<DailyBar>(); var artifacts = new List<object>();
var generated = Measure("generate_archive_validate", () =>
{
    for (var i = 0; i <= count; i++)
    {
        var rows = dates.Select((d, j) => new { date = d, open = 100m + i * 10m + j * (i % 7 + 1) / 10m,
            close = 101m + i * 10m + j * (i % 7 + 1) / 10m,
            high = 103m + i * 10m + j * (i % 7 + 1) / 10m,
            low = 98m + i * 10m + j * (i % 7 + 1) / 10m,
            adjusted_close = 101m + i * 10m + j * (i % 7 + 1) / 10m,
            volume = 10000L + i * 100L + j * 10L }).ToArray();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(rows);
        var archived = archiver.ArchiveAsync(new MemoryStream(bytes), ".json").GetAwaiter().GetResult();
        Check(archived.ContentSha256 == Digest(bytes), "Raw archive hash mismatch");
        artifacts.Add(new { id = archived.ArtifactId, local_uri = Path.GetFullPath(Path.Combine(rawRoot, archived.RelativePath)),
            manifest = new { requested_uri = "https://synthetic.example/" + instruments[i].IssuerName,
                request_parameters = new { synthetic = true }, fetched_at_utc = t0, parser_version = "synthetic-scale-1",
                artifact = new { content_sha256 = archived.ContentSha256, byte_length = archived.ByteLength } } });
        var source = new SourceReference("eodhd", archived.ArtifactId, t0, t0, archived.ContentSha256);
        foreach (var (row, j) in rows.Select((r, j) => (r, j)))
        {
            var bar = new DailyBar(instruments[i].Id, row.date, row.open, row.close + 2m, row.open - 2m,
                row.close, row.volume, source, row.close, "SYNTHETIC_SHARES", "SYNTHETIC_UNADJUSTED", "SYNTHETIC");
            Check(PilotValidation.Validate(instruments[i], row.date, bar, proofs[j], t0).Status == "AVAILABLE", "Admission failed");
            bars.Add(bar);
        }
    }
    return bars.Count;
});
PilotDatabase.EnsureSchema();
object Payload(IEnumerable<DailyBar> input, DateTimeOffset known) => new {
    run_id = Guid.NewGuid(), started_at = known, known_at = known,
    parameters = new { mode = "OFFLINE_SYNTHETIC_SCALE", provider_requests = 0 },
    instruments = instruments.Select(i => new { id = i.Id.Value, name = i.IssuerName, listed_on = i.ListedOn }), artifacts,
    bars = input.Select(b => new { instrument_id = b.InstrumentId.Value, date = b.SessionDate,
        hash = PilotValidation.ContentHash(b), artifact_id = b.Source.RawArtifactId, open = b.Open, high = b.High,
        low = b.Low, close = b.Close, volume = b.Volume, adjusted_close = b.AdjustedClose,
        volume_unit = b.VolumeUnit, volume_basis = b.VolumeBasis, market_segment = b.MarketSegment,
        retrieved_at = b.Source.FetchedAt, session_reference = proofs[0].Reference, session_known_at = proofs[0].KnownAt }) };
void Persist(IEnumerable<DailyBar> input, DateTimeOffset known)
{
    // Bound JSON/psql memory per transaction; use the unchanged real ingestion SQL.
    foreach (var chunk in input.Chunk(5000)) PilotDatabase.Persist(Payload(chunk, known));
}
Measure("insert", () => { Persist(bars, t0); return 0; });
var revisions = Measure("read_resolve", PilotDatabase.ReadRevisions);
Check(revisions.Count == generated, "Initial row count");
var canonical = Measure("canonical_resolution", () => revisions.Where(r => r.KnownAt <= t0 && r.Bar.Source.AvailableAt <= t0)
    .GroupBy(r => (r.Bar.InstrumentId,r.Bar.SessionDate)).Select(g => g.OrderByDescending(r => r.KnownAt)
        .ThenByDescending(r => r.RevisionNumber).First()).ToArray());
Check(canonical.Length == generated, "Canonical resolution lost rows");
PilotFeatureResult[] Features(IReadOnlyList<DailyBarRevision> history, DateTimeOffset cutoff) => instruments.Take(count)
    .Select(i => PilotFeatures.Calculate(history, i.Id, benchmark, proofs, cutoff)).ToArray();
var features = Measure("features_full_input", () => Features(revisions, t0));
Check(features.All(f => f.Status == "AVAILABLE_PILOT" && f.ConsecutiveSessions == 250
    && f.Ema20 != null && f.Ema50 != null && f.Atr14 != null && f.PriorHigh20 != null
    && f.PriorLow20 != null && f.VolumeRatio20 != null && f.RelativePerformance20 != null), "Feature warm-up/alignment");
// Same public engine on relevant own/benchmark histories: exposes repeated global resolution overhead.
var grouped = revisions.GroupBy(r => r.Bar.InstrumentId).ToDictionary(g => g.Key, g => g.ToArray());
var repeated = Measure("features_scoped_rerun", () => Scoped(revisions, t0));
Check(features.SequenceEqual(repeated), "Rerun/scoped feature mismatch");
foreach (var i in new[] { 0, count / 2, count - 1 }.Distinct())
    Check(features[i] == PilotFeatures.Calculate(grouped[instruments[i].Id].Concat(grouped[benchmark]),
        instruments[i].Id, benchmark, proofs, t0), "Cross-symbol contamination");
var missingDate = dates[^10];
var missing = PilotFeatures.Calculate(grouped[instruments[0].Id].Where(r => r.Bar.SessionDate != missingDate)
    .Concat(grouped[benchmark]), instruments[0].Id, benchmark, proofs, t0);
Check(missing.Status == "WARMUP" && missing.ConsecutiveSessions == 9, "Missing session was bridged");
Check(PilotValidation.Validate(instruments[0], missingDate, null, proofs[^10], t0).Status == "MISSING", "Missing outcome");
var unaligned = PilotFeatures.Calculate(grouped[instruments[0].Id].Concat(grouped[benchmark].Where(r => r.Bar.SessionDate != dates[^5])),
    instruments[0].Id, benchmark, proofs, t0);
Check(unaligned.RelativePerformance20 is null, "Missing benchmark alignment was fabricated");
var affected = instruments.Take(Math.Max(1, count / 100)).Select(i => i.Id).ToHashSet();
var originals = bars.Where(b => affected.Contains(b.InstrumentId) && b.SessionDate == dates[^15]).ToArray();
var corrections = originals.Select(b =>
{
    var bytes = JsonSerializer.SerializeToUtf8Bytes(new { date = b.SessionDate, open = b.Open, high = b.High + 1m,
        low = b.Low, close = b.Close + 1m, volume = b.Volume, adjusted_close = b.AdjustedClose, instrument = b.InstrumentId.Value });
    var archived = archiver.ArchiveAsync(new MemoryStream(bytes), ".json").GetAwaiter().GetResult();
    artifacts.Add(new { id = archived.ArtifactId, local_uri = Path.GetFullPath(Path.Combine(rawRoot, archived.RelativePath)),
        manifest = new { requested_uri = "https://synthetic.example/correction", request_parameters = new { synthetic = true },
            fetched_at_utc = t1, parser_version = "synthetic-scale-1",
            artifact = new { content_sha256 = archived.ContentSha256, byte_length = archived.ByteLength } } });
    return new DailyBar(b.InstrumentId, b.SessionDate, b.Open, b.High + 1m, b.Low, b.Close + 1m, b.Volume,
        new SourceReference(b.Source.SourceId, archived.ArtifactId, t1, t1, archived.ContentSha256),
        b.AdjustedClose, b.VolumeUnit, b.VolumeBasis, b.MarketSegment);
}).ToArray();
Measure("corrections", () => { Persist(corrections, t1); return 0; });
var corrected = PilotDatabase.ReadRevisions();
Check(corrected.Count == generated + corrections.Length, "Correction must append exactly once");
Measure("stale_and_duplicate", () => { Persist(originals, t2); Persist(corrections, t2); return 0; });
Check(PilotDatabase.ReadRevisions().Count == corrected.Count, "Stale/duplicate appended a revision");
var prior = Measure("asof_features", () => Scoped(corrected, t0));
Check(prior.SequenceEqual(features), "As-of leaked future correction");
var latest = Measure("latest_features", () => Scoped(corrected, t1));
var changed = latest.Select((f, i) => f != features[i]).ToArray();
Check(changed.Select((c, i) => c == affected.Contains(instruments[i].Id)).All(x => x), "Correction contamination/recalculation");
var correctedGroups = corrected.GroupBy(r => r.Bar.InstrumentId).ToDictionary(g => g.Key,g => g.ToArray());
Measure("affected_features_only", () => {
    foreach (var i in Enumerable.Range(0,count).Where(i => affected.Contains(instruments[i].Id)))
        Check(latest[i] == PilotFeatures.Calculate(correctedGroups[instruments[i].Id].Concat(correctedGroups[benchmark]),
            instruments[i].Id,benchmark,proofs,t1), "Bounded recalculation differs");
    return 0;
});
Measure("asof_price_queries", () => {
    foreach (var b in originals)
        foreach (var clock in new[] { t0,t1 })
        {
            var close = decimal.Parse(PilotDatabase.Execute($"SELECT close FROM daily_bar_revision WHERE instrument_id='{b.InstrumentId.Value}' AND session_date='{b.SessionDate:yyyy-MM-dd}' AND known_at<='{clock:O}' ORDER BY known_at DESC,revision_number DESC LIMIT 1;"),System.Globalization.CultureInfo.InvariantCulture);
            Check(close == b.Close + (clock == t0 ? 0m : 1m), "Database replay price differs");
        }
    return 0;
});
var store = new DailyBarRevisionStore();
foreach (var b in originals) store.Ingest(b, t0, PilotValidation.ContentHash(b), Identity("initial"));
foreach (var b in corrections) store.Ingest(b, t1, PilotValidation.ContentHash(b), Identity("corrected"));
foreach (var b in originals)
{
    Check(store.Ingest(b, t2, PilotValidation.ContentHash(b), Identity("stale")).Disposition == IngestionDisposition.StaleEvidenceIgnored, "Memory stale guard");
    Check(store.AsOf(b.InstrumentId, b.SessionDate, t0)!.Bar.Close == b.Close, "Prior price");
    Check(store.AsOf(b.InstrumentId, b.SessionDate, t1)!.Bar.Close == b.Close + 1m, "Latest price");
}
var queryPlan = PilotDatabase.Execute($"EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM daily_bar_revision WHERE instrument_id='{instruments[0].Id.Value}' AND session_date='{dates[^15]:yyyy-MM-dd}' AND known_at<='{t0:O}' ORDER BY known_at DESC,revision_number DESC LIMIT 1;");
var wholePlan = PilotDatabase.Execute("EXPLAIN (ANALYZE, BUFFERS) SELECT count(*) FROM pilot_revision_evidence;");
var mutationRejected = false;
try { PilotDatabase.Execute("UPDATE daily_bar_revision SET close=close WHERE revision_number=2;"); }
catch (InvalidOperationException) { mutationRejected = true; }
Check(mutationRejected, "Mutation unexpectedly allowed");
var databaseBytes = long.Parse(PilotDatabase.Execute("SELECT pg_database_size(current_database());"), System.Globalization.CultureInfo.InvariantCulture);
var report = new { status = "PASS", synthetic = true, equities = count, benchmark = 1, sessions = 250,
    generated_rows = generated, revision_rows = corrected.Count, corrections = corrections.Length,
    database_bytes = databaseBytes, timings, query_plan = queryPlan, whole_scan_plan = wholePlan,
    feature_hash = Digest(JsonSerializer.SerializeToUtf8Bytes(features)),
    latest_feature_hash = Digest(JsonSerializer.SerializeToUtf8Bytes(latest)),
    missing_session = missing.Status, chronology = "PASS", provider_requests = 0, billable_units = 0,
    FullIdx = "DISABLED", soak_eligible = false };
File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(report));
