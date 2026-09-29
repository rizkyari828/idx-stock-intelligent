using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;

if (args is ["pilot-state"])
{
    try
    {
        var latest = PilotDatabase.ReadRevisions().GroupBy(r => (r.Bar.InstrumentId, r.Bar.SessionDate)).Select(g => g.MaxBy(r => r.RevisionNumber)!);
        Console.WriteLine(JsonSerializer.Serialize(new { status = "KNOWN", database = PilotDatabase.DatabaseName,
            revisions = latest.Select(r => new { instrument_id = r.Bar.InstrumentId.Value, date = r.Bar.SessionDate,
                content_sha256 = r.ContentSha256, retrieved_at = r.Bar.Source.FetchedAt }) }));
    }
    catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or JsonException)
    {
        Console.WriteLine("{\"status\":\"UNKNOWN\",\"revisions\":[]}");
        Environment.ExitCode = 1;
    }
    return;
}

if (args.Length != 2 || args[0] != "pilot")
{
    Console.WriteLine("Manual pilot: dotnet run --project src/IdxStockIntelligence.Worker -- pilot <ignored-batch.json>");
    return;
}
var operationId = Guid.NewGuid();
var operationStarted = DateTimeOffset.UtcNow;
var output = Path.Combine("data", "collector-output", "pilot", operationId + ".summary.json");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
try
{
    // ponytail: serialize one local worker; database locks retain cross-process append safety.
    using var workerLock = new FileStream(Path.Combine(Path.GetDirectoryName(output)!, "worker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    using var batch = JsonDocument.Parse(File.ReadAllText(args[1]));
    using var universe = JsonDocument.Parse(File.ReadAllText("pilot/universe.json"));
    using var sessions = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("IDX_PILOT_SESSIONS") ?? "pilot/sessions.json"));
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
            || proof.Status is not (ExchangeDayStatus.ObservedTrading or ExchangeDayStatus.AnnouncedClosed or ExchangeDayStatus.ExceptionalClosure))
            throw new ArgumentException("Independent, already-known session evidence required.");
        calendar.Record(new(proof.Date, proof.Status, proof.Reference));
    }
    var proofByDate = proofs.ToDictionary(p => p.Date);
    using var instrumentSessions = JsonDocument.Parse(File.ReadAllText("pilot/instrument-sessions.json"));
    var instrumentProofs = instrumentSessions.RootElement.EnumerateArray().Select(p => new InstrumentSessionProof(
        new(p.GetProperty("instrument_id").GetGuid()), DateOnly.Parse(p.GetProperty("date").GetString()!, CultureInfo.InvariantCulture),
        Enum.Parse<MarketSessionStatus>(p.GetProperty("status").GetString()!),p.GetProperty("reference").GetString()!,p.GetProperty("known_at").GetDateTimeOffset())).ToArray();
    foreach (var proof in instrumentProofs)
    {
        var uri = new Uri(proof.Reference);
        if (uri.Scheme != "https" || uri.Host.EndsWith("eodhd.com", StringComparison.OrdinalIgnoreCase) || proof.KnownAt > now
            || proof.Status is not (MarketSessionStatus.NoTrade or MarketSessionStatus.Suspension))
            throw new ArgumentException("Independent instrument state evidence required.");
    }
    var instrumentByDate = instrumentProofs.ToDictionary(p => (p.Instrument,p.Date));
    var instruments = universe.RootElement.GetProperty("instruments").EnumerateArray().ToDictionary(i => i.GetProperty("symbol").GetString()!);
    if (instruments.Count != 11 || !instruments.TryGetValue("JKSE.INDX", out var benchmarkConfig)) throw new ArgumentException("Fixed pilot panel required.");
    var entries = batch.RootElement.GetProperty("entries").EnumerateArray().ToArray();
    if (entries.Length != 11 || entries.Select(e => e.GetProperty("symbol").GetString()).Distinct().Count() != 11)
        throw new ArgumentException("Every instrument needs an explicit outcome.");
    var bars = new List<object>();
    var accepted = new List<DailyBar>();
    var artifacts = new List<object>();
    var observations = new List<object>();
    var accountedOutcomes=0;
    foreach (var entry in entries)
    {
        var symbol = entry.GetProperty("symbol").GetString()!;
        var config = instruments[symbol];
        var id = new InstrumentId(config.GetProperty("id").GetGuid());
        if (entry.GetProperty("instrument_id").GetGuid() != id.Value) throw new ArgumentException("Identity mismatch.");
        var listing = config.GetProperty("listing_evidence");
        if (listing.GetProperty("known_at").GetDateTimeOffset() > now || listing.GetProperty("listed_on").GetRawText() != config.GetProperty("listed_on").GetRawText()
            || listing.GetProperty("symbol").GetString() != symbol)
            throw new ArgumentException("Invalid listing evidence chronology/identity.");
        var instrument = new Instrument(id, config.GetProperty("name").GetString()!,
            listing.GetProperty("status").GetString() != "VERIFIED" ? null : DateOnly.Parse(config.GetProperty("listed_on").GetString()!, CultureInfo.InvariantCulture));
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
                entry.GetProperty("status").GetString() != "AVAILABLE", instrumentByDate.GetValueOrDefault((id,date)));
            var exchangeState = proofByDate.GetValueOrDefault(date)?.Status switch
            {
                ExchangeDayStatus.ObservedTrading => "EXCHANGE_OPEN",
                ExchangeDayStatus.AnnouncedClosed => "EXCHANGE_HOLIDAY",
                ExchangeDayStatus.ExceptionalClosure => "EXCEPTIONAL_CLOSURE",
                _ => "UNKNOWN"
            };
            var instrumentState = result.Status switch { "AVAILABLE" => "TRADED", "SUSPENDED" => "SUSPENDED", "NO_TRADE" => "NO_TRADE", "MISSING" => "MISSING_DATA", _ => "UNKNOWN" };
            observations.Add(new { symbol, date, result.Status, result.Reason, exchange_state=exchangeState, instrument_state=instrumentState });
            if (result.Status is "AVAILABLE" or "NO_TRADE" or "SUSPENDED") accountedOutcomes++;
            if (result.Bar is not { } bar) continue;
            accepted.Add(bar);
            var proof = proofByDate[date];
            bars.Add(new { instrument_id = id.Value, date, hash = PilotValidation.ContentHash(bar), artifact_id = source!.RawArtifactId,
                open = bar.Open, high = bar.High, low = bar.Low, close = bar.Close, volume = bar.Volume, adjusted_close = bar.AdjustedClose,
                volume_unit = bar.VolumeUnit, volume_basis = bar.VolumeBasis, market_segment = bar.MarketSegment,
                retrieved_at = source.FetchedAt, session_reference = proof.Reference, session_known_at = proof.KnownAt });
        }
    }
    PilotDatabase.EnsureSchema();
    var before = PilotDatabase.ReadRevisions();
    var latest = before.GroupBy(r => (r.Bar.InstrumentId,r.Bar.SessionDate)).ToDictionary(g => g.Key,g => g.MaxBy(r => r.RevisionNumber)!);
    var staleEvidence = accepted.Count(b => latest.TryGetValue((b.InstrumentId,b.SessionDate),out var old)
        && b.Source.FetchedAt<old.Bar.Source.FetchedAt && PilotValidation.ContentHash(b)!=old.ContentSha256);
    var runId = batch.RootElement.GetProperty("run_id").GetGuid();
    PilotDatabase.Persist(new { run_id = runId, started_at = batch.RootElement.GetProperty("started_at").GetDateTimeOffset(), known_at = now,
        parameters = new { from = first, to = last, mode = "ZERO_COST_PILOT", entries = entries.Select(e => new { symbol = e.GetProperty("symbol").GetString(), status = e.GetProperty("status").GetString() }) },
        instruments = instruments.Values.ToArray(), artifacts, bars });
    var revisions = PilotDatabase.ReadRevisions();
    var existing = before.Select(b => (b.Bar.InstrumentId,b.Bar.SessionDate,b.RevisionNumber)).ToHashSet();
    var added = revisions.Where(r => !existing.Contains((r.Bar.InstrumentId,r.Bar.SessionDate,r.RevisionNumber))).ToArray();
    var benchmark = new InstrumentId(benchmarkConfig.GetProperty("id").GetGuid());
    var expected = (last.DayNumber-first.DayNumber+1)*11;
    var fullRequests = entries.All(e => e.GetProperty("status").GetString() == "AVAILABLE"
        && DateOnly.Parse(e.GetProperty("manifest").GetProperty("request_parameters").GetProperty("from").GetString()!,CultureInfo.InvariantCulture)<=first
        && DateOnly.Parse(e.GetProperty("manifest").GetProperty("request_parameters").GetProperty("to").GetString()!,CultureInfo.InvariantCulture)>=last);
    var runStatus = accountedOutcomes == expected && fullRequests && staleEvidence==0 ? "SUCCEEDED" : "DEGRADED";
    using var soak = JsonDocument.Parse(File.ReadAllText("pilot/soak.json"));
    var soakEligible = runStatus == "SUCCEEDED" && first == last && first > DateOnly.Parse(soak.RootElement.GetProperty("after_market_date").GetString()!,CultureInfo.InvariantCulture)
        && batch.RootElement.TryGetProperty("mode",out var mode) && mode.GetString()=="DAILY"
        && batch.RootElement.GetProperty("reserved_units").GetInt32()==11 && entries.All(e => !e.TryGetProperty("cached",out var cached) || !cached.GetBoolean())
        && entries.All(e => e.GetProperty("manifest").GetProperty("fetched_at_utc").GetDateTimeOffset()>=proofByDate[first].KnownAt)
        && PilotDatabase.DatabaseName=="idx_stock_intelligence";
    var summary = new { operation_id=operationId, run_id = runId, started_at=operationStarted, completed_at=DateTimeOffset.UtcNow,
        known_at = now, mode = "ZERO_COST_PILOT", run_status=runStatus, market_date=first==last ? (DateOnly?)first : null, soak_eligible=soakEligible,
        database=PilotDatabase.DatabaseName,
        canonical_state=revisions.GroupBy(r => (r.Bar.InstrumentId,r.Bar.SessionDate)).Select(g => g.MaxBy(r => r.RevisionNumber)!)
            .Where(r => r.Bar.SessionDate>=first && r.Bar.SessionDate<=last).Select(r => new { instrument_id=r.Bar.InstrumentId.Value,
                date=r.Bar.SessionDate,content_sha256=r.ContentSha256,retrieved_at=r.Bar.Source.FetchedAt }),
        session_evidence=sessions.RootElement,instrument_evidence=universe.RootElement,
        requested_instruments=entries.Select(e => e.GetProperty("symbol").GetString()), accepted_rows = bars.Count,
        canonical_additions=added.Count(r => r.RevisionNumber==1), corrections=added.Count(r => r.RevisionNumber>1), revisions_added = added.Length,
        stale_evidence_ignored=staleEvidence,
        rejected_evidence=entries.Sum(e => e.GetProperty("rows").GetArrayLength())-bars.Count, unavailable_observations=expected-bars.Count,
        warnings=runStatus=="SUCCEEDED" ? new[] { "PILOT_SEMANTICS_DEGRADED" } : new[] { "INCOMPLETE_OR_UNCONFIRMED", "PILOT_SEMANTICS_DEGRADED" }, observations,
        features = instruments.Select(i => new { symbol = i.Key, result = PilotFeatures.Calculate(revisions,
            new InstrumentId(i.Value.GetProperty("id").GetGuid()), benchmark, proofs, now) }) };
    File.WriteAllText(output, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(new { summary_path=output }));
    Environment.ExitCode=runStatus=="SUCCEEDED" ? 0 : 2;
}
catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or KeyNotFoundException or JsonException or FormatException or OverflowException)
{
    Console.Error.WriteLine("STOP: pilot validation/database failure; no fabricated bars. " + error.GetType().Name);
    File.WriteAllText(output,JsonSerializer.Serialize(new { operation_id=operationId,started_at=operationStarted,completed_at=DateTimeOffset.UtcNow,
        run_status="FAILED",error_code=error.GetType().Name,soak_eligible=false,canonical_additions=(int?)null,corrections=(int?)null }));
    Console.WriteLine(JsonSerializer.Serialize(new { summary_path=output }));
    Environment.ExitCode = 1;
}
