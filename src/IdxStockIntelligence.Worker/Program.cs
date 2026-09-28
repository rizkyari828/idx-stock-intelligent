using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;

if (args.Length != 2 || args[0] != "pilot")
{
    Console.WriteLine("Manual pilot: dotnet run --project src/IdxStockIntelligence.Worker -- pilot <ignored-batch.json>");
    return;
}
try
{
    using var batch = JsonDocument.Parse(File.ReadAllText(args[1]));
    using var universe = JsonDocument.Parse(File.ReadAllText("pilot/universe.json"));
    using var sessions = JsonDocument.Parse(File.ReadAllText("pilot/sessions.json"));
    var now = DateTimeOffset.UtcNow;
    var first = DateOnly.Parse(batch.RootElement.GetProperty("from").GetString()!, CultureInfo.InvariantCulture);
    var last = DateOnly.Parse(batch.RootElement.GetProperty("to").GetString()!, CultureInfo.InvariantCulture);
    var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, "Asia/Jakarta").DateTime);
    if (first > last || last >= today || first < today.AddDays(-330)) throw new ArgumentException("Invalid completed-date window.");
    var proofs = sessions.RootElement.EnumerateArray().Select(p => new SessionProof(
        DateOnly.Parse(p.GetProperty("date").GetString()!, CultureInfo.InvariantCulture), Enum.Parse<ExchangeDayStatus>(p.GetProperty("status").GetString()!),
        p.GetProperty("reference").GetString()!, p.GetProperty("known_at").GetDateTimeOffset())).ToArray();
    var calendar = new ExchangeCalendarEvidence();
    foreach (var proof in proofs)
    {
        var uri = new Uri(proof.Reference);
        if (uri.Scheme != "https" || uri.Host == "eodhd.com" || uri.Host.EndsWith(".eodhd.com", StringComparison.OrdinalIgnoreCase)
            || proof.KnownAt > now || proof.Date > today
            || proof.Status is not (ExchangeDayStatus.ObservedTrading or ExchangeDayStatus.AnnouncedClosed))
            throw new ArgumentException("Independent, already-known session evidence required.");
        calendar.Record(new(proof.Date, proof.Status, proof.Reference));
    }
    var proofByDate = proofs.ToDictionary(p => p.Date);
    var instruments = universe.RootElement.GetProperty("instruments").EnumerateArray().ToDictionary(i => i.GetProperty("symbol").GetString()!);
    if (instruments.Count != 11 || !instruments.TryGetValue("JKSE.INDX", out var benchmarkConfig)) throw new ArgumentException("Fixed pilot panel required.");
    var entries = batch.RootElement.GetProperty("entries").EnumerateArray().ToArray();
    if (entries.Length != 11 || entries.Select(e => e.GetProperty("symbol").GetString()).Distinct().Count() != 11)
        throw new ArgumentException("Every instrument needs an explicit outcome.");
    var bars = new List<object>();
    var artifacts = new List<object>();
    var observations = new List<object>();
    foreach (var entry in entries)
    {
        var symbol = entry.GetProperty("symbol").GetString()!;
        var config = instruments[symbol];
        var id = new InstrumentId(config.GetProperty("id").GetGuid());
        if (entry.GetProperty("instrument_id").GetGuid() != id.Value) throw new ArgumentException("Identity mismatch.");
        var instrument = new Instrument(id, config.GetProperty("name").GetString()!,
            config.GetProperty("listed_on").ValueKind == JsonValueKind.Null ? null : DateOnly.Parse(config.GetProperty("listed_on").GetString()!, CultureInfo.InvariantCulture));
        SourceReference? source = null;
        var rows = new Dictionary<DateOnly, DailyBar>();
        if (entry.TryGetProperty("manifest", out var manifest))
        {
            var uri = manifest.GetProperty("requested_uri").GetString()!;
            if (uri != "https://eodhd.com/api/eod/" + symbol || manifest.GetProperty("source_id").GetString() != "eodhd")
                throw new ArgumentException("Unexpected request provenance.");
            if (manifest.GetProperty("request_parameters").EnumerateObject().Any(p => p.Name is not ("from" or "to" or "fmt" or "period" or "order")))
                throw new ArgumentException("Only sanitized EOD parameters may be persisted.");
            var artifact = manifest.GetProperty("artifact");
            var digest = artifact.GetProperty("content_sha256").GetString()!;
            var root = Path.GetFullPath(entry.GetProperty("raw_root").GetString()!);
            var path = Path.GetFullPath(Path.Combine(root, artifact.GetProperty("relative_uri").GetString()!));
            if (!root.StartsWith(Path.GetFullPath("data/raw") + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException("Evidence must remain inside ignored raw storage.");
            var bytes = File.ReadAllBytes(path);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != digest || bytes.LongLength != artifact.GetProperty("byte_length").GetInt64())
                throw new ArgumentException("Evidence hash/length mismatch.");
            var artifactId = new Guid(Convert.FromHexString(digest)[..16]);
            var fetched = manifest.GetProperty("fetched_at_utc").GetDateTimeOffset();
            if (fetched > now) throw new ArgumentException("Future retrieval timestamp.");
            source = new SourceReference("eodhd", artifactId, fetched, fetched, digest);
            artifacts.Add(new { id = artifactId, manifest, local_uri = path });
            if (entry.GetProperty("status").GetString() == "AVAILABLE")
            {
                using var raw = JsonDocument.Parse(bytes);
                var rawRows = raw.RootElement.EnumerateArray().ToDictionary(r => DateOnly.Parse(r.GetProperty("date").GetString()!, CultureInfo.InvariantCulture));
                foreach (var row in entry.GetProperty("rows").EnumerateArray())
                {
                    var date = DateOnly.Parse(row.GetProperty("date").GetString()!, CultureInfo.InvariantCulture);
                    if (date < first || date > last) throw new ArgumentException("Row outside window.");
                    var original = rawRows[date];
                    decimal Number(string key)
                    {
                        var value = decimal.Parse(row.GetProperty(key).GetString()!, CultureInfo.InvariantCulture);
                        if (value != original.GetProperty(key).GetDecimal()) throw new ArgumentException("Normalization differs from evidence.");
                        return value;
                    }
                    var volume = row.GetProperty("volume").GetInt64();
                    if (volume != original.GetProperty("volume").GetInt64()) throw new ArgumentException("Volume differs from evidence.");
                    rows.Add(date, new DailyBar(id, date, Number("open"), Number("high"), Number("low"), Number("close"), volume, source,
                        Number("adjusted_close"), symbol == "JKSE.INDX" ? "UNKNOWN" : "SHARE_COUNT_CORROBORATED", "SPLIT_ADJUSTED", "UNKNOWN"));
                }
                if (!rows.Keys.Order().SequenceEqual(rawRows.Keys.Where(d => d >= first && d <= last).Order()))
                    throw new ArgumentException("Normalized batch omitted evidence rows.");
            }
        }
        else if (entry.GetProperty("status").GetString() == "AVAILABLE") throw new ArgumentException("Available response needs provenance.");
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var result = PilotValidation.Validate(instrument, date, rows.GetValueOrDefault(date), proofByDate.GetValueOrDefault(date), now,
                entry.GetProperty("status").GetString() != "AVAILABLE");
            observations.Add(new { symbol, date, result.Status, result.Reason });
            if (result.Bar is not { } bar) continue;
            var proof = proofByDate[date];
            bars.Add(new { instrument_id = id.Value, date, hash = PilotValidation.ContentHash(bar), artifact_id = source!.RawArtifactId,
                open = bar.Open, high = bar.High, low = bar.Low, close = bar.Close, volume = bar.Volume, adjusted_close = bar.AdjustedClose,
                volume_unit = bar.VolumeUnit, volume_basis = bar.VolumeBasis, market_segment = bar.MarketSegment,
                retrieved_at = source.FetchedAt, session_reference = proof.Reference, session_known_at = proof.KnownAt });
        }
    }
    PilotDatabase.EnsureSchema();
    var before = PilotDatabase.ReadRevisions().Count;
    var runId = batch.RootElement.GetProperty("run_id").GetGuid();
    PilotDatabase.Persist(new { run_id = runId, started_at = batch.RootElement.GetProperty("started_at").GetDateTimeOffset(), known_at = now,
        parameters = new { from = first, to = last, mode = "ZERO_COST_PILOT", entries = entries.Select(e => new { symbol = e.GetProperty("symbol").GetString(), status = e.GetProperty("status").GetString() }) },
        instruments = instruments.Values.ToArray(), artifacts, bars });
    var revisions = PilotDatabase.ReadRevisions();
    var benchmark = new InstrumentId(benchmarkConfig.GetProperty("id").GetGuid());
    var summary = new { run_id = runId, known_at = now, mode = "ZERO_COST_PILOT", accepted_rows = bars.Count,
        revisions_added = revisions.Count - before, observations,
        features = instruments.Select(i => new { symbol = i.Key, result = PilotFeatures.Calculate(revisions,
            new InstrumentId(i.Value.GetProperty("id").GetGuid()), benchmark, proofs, now) }) };
    var output = Path.Combine("data", "collector-output", "pilot", runId + ".summary.json");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{output}; accepted rows={bars.Count}; revisions added={revisions.Count - before}");
}
catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or KeyNotFoundException or JsonException or FormatException or OverflowException)
{
    Console.Error.WriteLine("STOP: pilot validation/database failure; no fabricated bars. " + error.GetType().Name);
    Environment.ExitCode = 1;
}
