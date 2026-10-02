using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdxStockIntelligence.Application;

public sealed record ScreenerReferenceDocument(int SchemaVersion, IReadOnlyList<UniverseSnapshot> Universes,
    IReadOnlyList<InstrumentSnapshot> Instruments);
public sealed record ScreenerSourceEvidence(string Id, string Source, string Reference, DateTimeOffset? PublishedAt,
    DateTimeOffset RetrievedAt, DateTimeOffset KnownAt);
public sealed record UniverseSnapshot(string SnapshotId, DateTimeOffset KnownAt, string UniverseId,
    IReadOnlyList<Guid> MemberIds, Guid BenchmarkId, IReadOnlyList<ScreenerSourceEvidence> Evidence, string ContentHash);
public sealed record InstrumentSnapshot(string SnapshotId, Guid InstrumentId, DateTimeOffset KnownAt,
    IReadOnlyList<ScreenerSourceEvidence> Evidence, string ContentHash,
    IReadOnlyList<ScreenerIdentityInterval> Identities, IReadOnlyList<ScreenerTradingInterval> Trading,
    IReadOnlyList<ScreenerPriceInterval> Prices, IReadOnlyList<ScreenerVolumeInterval> Volumes);
public sealed record ScreenerIdentityInterval(DateOnly From, DateOnly? Through, string? Symbol, string? DisplayName,
    string Classification, string Currency, string Board, IReadOnlyList<string> EvidenceIds);
public sealed record ScreenerTradingInterval(DateOnly From, DateOnly Through, string Status, string Mechanism,
    IReadOnlyList<string> EvidenceIds);
public sealed record ScreenerPriceInterval(DateOnly From, DateOnly Through, string SourceId, string Convention,
    string Continuity, string EventCoverage, IReadOnlyList<string> ContentHashes, IReadOnlyList<string> EvidenceIds);
public sealed record ScreenerVolumeInterval(DateOnly From, DateOnly Through, string SourceId, string Unit, string Basis,
    bool RawPriceCompatible, string Currency, string MarketSegment,
    IReadOnlyList<string> ContentHashes, IReadOnlyList<string> EvidenceIds);

public static class ScreenerReferences
{
    // ponytail: retained local evidence <=4 MiB/10,000 records; review ingestion/storage only when this pilot ceiling matters.
    public const int MaximumBytes = 4 * 1024 * 1024;
    public const int MaximumRecords = 10000;
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false, RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32
    };
    public static bool IsHash(string? hash) => hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static EvidenceResult<ScreenerReferenceDocument> Parse(ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (bytes.Length > MaximumBytes) return new(null, "REFERENCE_BOUND_EXCEEDED");
        try
        {
            using var tree = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
            CheckProperties(tree.RootElement, ct);
            var doc = tree.RootElement.Deserialize<ScreenerReferenceDocument>(JsonOptions);
            if (doc is null || doc.SchemaVersion != 1) return new(null, "REFERENCE_SCHEMA_UNSUPPORTED");
            var count = Validate(doc, ct);
            return count > MaximumRecords ? new(null, "REFERENCE_BOUND_EXCEEDED") : new(doc, null);
        }
        catch (JsonException) { return new(null, "REFERENCE_MALFORMED"); }
        catch (ArgumentException) { return new(null, "REFERENCE_MALFORMED"); }
    }

    private static void CheckProperties(JsonElement node, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in node.EnumerateObject())
            {
                if (!names.Add(p.Name)) throw new ArgumentException("Duplicate property.");
                CheckProperties(p.Value, ct);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var child in node.EnumerateArray()) CheckProperties(child, ct);
    }

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition)
    { if (!condition) throw new ArgumentException("Invalid reference evidence."); }
    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2000;
    private static bool Date(DateOnly date) => date >= new DateOnly(1900, 1, 1);
    private static bool Time(DateTimeOffset at) => at >= new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static bool One(string value, params string[] allowed) => allowed.Contains(value, StringComparer.Ordinal);
    private static void Range(DateOnly from, DateOnly? through, IReadOnlyList<string> evidenceIds, HashSet<string> sources)
    {
        Require(Date(from) && (through is null || Date(through.Value) && through >= from)
            && evidenceIds is { Count: > 0 } && evidenceIds.Distinct().Count() == evidenceIds.Count
            && evidenceIds.All(sources.Contains));
    }

    private static HashSet<string> Sources(IReadOnlyList<ScreenerSourceEvidence> evidence, DateTimeOffset knownAt)
    {
        Require(Time(knownAt) && evidence is { Count: > 0 });
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in evidence)
            Require(e is not null && Text(e.Id) && ids.Add(e.Id) && Text(e.Source) && Text(e.Reference)
                && Uri.TryCreate(e.Reference, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0
                && Time(e.RetrievedAt) && Time(e.KnownAt) && e.RetrievedAt <= e.KnownAt && e.KnownAt <= knownAt
                && (e.PublishedAt is null || Time(e.PublishedAt.Value) && e.PublishedAt <= e.RetrievedAt));
        return ids;
    }

    public static int Validate(ScreenerReferenceDocument doc, CancellationToken ct = default)
    {
        Require(doc.SchemaVersion == 1 && doc.Universes is not null && doc.Instruments is not null);
        var snapshots = new HashSet<string>(StringComparer.Ordinal);
        var count = doc.Universes.Count + doc.Instruments.Count;
        foreach (var u in doc.Universes)
        {
            ct.ThrowIfCancellationRequested();
            Require(u is not null && Text(u.SnapshotId) && snapshots.Add(u.SnapshotId) && u.UniverseId == "PILOT"
                && u.MemberIds is { Count: <= 10 } && u.MemberIds.All(id => id != Guid.Empty)
                && u.MemberIds.Distinct().Count() == u.MemberIds.Count && u.BenchmarkId != Guid.Empty
                && !u.MemberIds.Contains(u.BenchmarkId) && IsHash(u.ContentHash));
            _ = Sources(u.Evidence, u.KnownAt);
            count += u.Evidence.Count + u.MemberIds.Count;
            Require(u.ContentHash == Hash(u, true, ct));
        }
        foreach (var i in doc.Instruments)
        {
            ct.ThrowIfCancellationRequested();
            Require(i is not null && Text(i.SnapshotId) && snapshots.Add(i.SnapshotId) && i.InstrumentId != Guid.Empty
                && IsHash(i.ContentHash) && i.Identities is not null && i.Trading is not null && i.Prices is not null && i.Volumes is not null);
            var sources = Sources(i.Evidence, i.KnownAt);
            count += i.Evidence.Count + i.Identities.Count + i.Trading.Count + i.Prices.Count + i.Volumes.Count;
            foreach (var f in i.Identities)
            {
                ct.ThrowIfCancellationRequested();
                Require(f is not null);
                Range(f.From, f.Through, f.EvidenceIds, sources);
                Require((f.Symbol is null || Text(f.Symbol)) && (f.DisplayName is null || Text(f.DisplayName))
                    && One(f.Classification, "ORDINARY", "INDEX", "UNSUPPORTED", "UNKNOWN")
                    && One(f.Currency, "IDR", "OTHER", "NOT_APPLICABLE", "UNKNOWN")
                    && One(f.Board, "MAIN", "DEVELOPMENT", "ACCELERATION", "NEW_ECONOMY", "SPECIAL_MONITORING", "OTHER", "NOT_APPLICABLE", "UNKNOWN"));
            }
            foreach (var f in i.Trading)
            {
                ct.ThrowIfCancellationRequested();
                Require(f is not null);
                Range(f.From, f.Through, f.EvidenceIds, sources);
                Require(One(f.Status, "TRADING", "SUSPENSION", "NO_TRADE", "UNKNOWN")
                    && One(f.Mechanism, "CONTINUOUS", "CALL_AUCTION", "OTHER", "NOT_APPLICABLE", "UNKNOWN"));
            }
            foreach (var f in i.Prices)
            {
                ct.ThrowIfCancellationRequested();
                Require(f is not null);
                Range(f.From, f.Through, f.EvidenceIds, sources);
                Require(Text(f.SourceId) && One(f.Convention, "STOCK_RAW", "INDEX_LEVEL", "UNKNOWN")
                    && One(f.Continuity, "RAW_AS_TRADED", "UNKNOWN") && One(f.EventCoverage, "CLEARED", "UNRESOLVED", "NOT_APPLICABLE", "UNKNOWN"));
                CheckHashes(f.ContentHashes); count += f.ContentHashes.Count;
            }
            foreach (var f in i.Volumes)
            {
                ct.ThrowIfCancellationRequested();
                Require(f is not null);
                Range(f.From, f.Through, f.EvidenceIds, sources);
                Require(Text(f.SourceId) && One(f.Unit, "SHARES", "LOTS", "SHARE_COUNT_CORROBORATED", "UNKNOWN")
                    && One(f.Basis, "RAW_AS_TRADED", "SPLIT_ADJUSTED", "UNKNOWN")
                    && One(f.Currency, "IDR", "OTHER", "UNKNOWN")
                    && One(f.MarketSegment, "REGULAR", "ALL_MARKETS", "OTHER", "UNKNOWN"));
                CheckHashes(f.ContentHashes); count += f.ContentHashes.Count;
            }
            Require(i.ContentHash == Hash(i, true, ct));
        }
        return count;
    }
    private static void CheckHashes(IReadOnlyList<string> hashes) =>
        Require(hashes is not null && hashes.All(IsHash) && hashes.Distinct().Count() == hashes.Count);

    public static EvidenceResult<UniverseSnapshot> Universe(ScreenerReferenceDocument doc, DateTimeOffset cutoff)
    {
        var visible = doc.Universes.Where(s => s.KnownAt <= cutoff).ToArray();
        if (visible.Length == 0) return new(null, "UNIVERSE_NOT_KNOWN");
        var latestKnown = visible.Max(v => v.KnownAt);
        var latest = visible.Where(s => s.KnownAt == latestKnown).ToArray();
        return latest.Length == 1 ? new(latest[0], null) : new(null, "UNIVERSE_CONFLICT");
    }
    public static EvidenceResult<InstrumentSnapshot> Instrument(ScreenerReferenceDocument doc, Guid id, DateTimeOffset cutoff)
    {
        var visible = doc.Instruments.Where(s => s.InstrumentId == id && s.KnownAt <= cutoff).ToArray();
        if (visible.Length == 0) return new(null, "REFERENCE_NOT_KNOWN");
        var latestKnown = visible.Max(v => v.KnownAt);
        var latest = visible.Where(s => s.KnownAt == latestKnown).ToArray();
        if (latest.Length != 1) return new(null, "REFERENCE_CONFLICT");
        var snapshot = latest[0];
        return Conflict(snapshot.Identities, f => f.From, f => f.Through)
            || Conflict(snapshot.Trading, f => f.From, f => f.Through)
            || Conflict(snapshot.Prices, f => f.From, f => f.Through)
            || Conflict(snapshot.Volumes, f => f.From, f => f.Through)
            ? new(null, "REFERENCE_INTERVAL_CONFLICT") : new(snapshot, null);
    }

    public static SelectedScreenerReferences Select(ScreenerReferenceDocument doc, IReadOnlyList<SessionProof> sessions,
        IReadOnlyList<InstrumentSessionProof> statuses, ScreenerReadRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var ids = request.InstrumentIds.Append(request.BenchmarkId).Distinct().ToHashSet();
        var instruments = new List<InstrumentSnapshot>();
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var visible = doc.Instruments.Where(s => s.InstrumentId == id && s.KnownAt <= request.Cutoff).ToArray();
            if (visible.Length > 0)
            {
                var latestKnown = visible.Max(v => v.KnownAt);
                instruments.AddRange(visible.Where(s => s.KnownAt == latestKnown));
            }
        }
        var selectedSessions = sessions.Where(s => s.Date >= request.HistoryAnchor && s.Date <= request.Through && s.KnownAt <= request.Cutoff)
            .GroupBy(s => s.Date).SelectMany(g =>
            { var at = g.Max(p => p.KnownAt); return g.Where(s => s.KnownAt == at); }).Distinct().ToArray();
        var selectedStatuses = statuses.Where(s => ids.Contains(s.Instrument.Value) && s.Date >= request.HistoryAnchor
                && s.Date <= request.Through && s.KnownAt <= request.Cutoff).GroupBy(s => (s.Instrument, s.Date))
            .SelectMany(g => { var at = g.Max(p => p.KnownAt); return g.Where(s => s.KnownAt == at); }).Distinct().ToArray();
        var universes = doc.Universes.Where(s => s.KnownAt <= request.Cutoff).ToArray();
        var universeKnown = universes.Length == 0 ? DateTimeOffset.MinValue : universes.Max(v => v.KnownAt);
        return new(universes.Where(s => s.KnownAt == universeKnown).ToArray(),
            instruments, selectedSessions, selectedStatuses);
    }

    private static bool Conflict<T>(IReadOnlyList<T> facts, Func<T, DateOnly> from, Func<T, DateOnly?> through)
    {
        var ordered = facts.OrderBy(from).ToArray();
        for (var n = 1; n < ordered.Length; n++)
            if (through(ordered[n - 1]) is null || through(ordered[n - 1]) >= from(ordered[n])) return true;
        return false;
    }

    private static EvidenceResult<T> Interval<T>(IReadOnlyList<T> facts, Func<T, DateOnly> from,
        Func<T, DateOnly?> through, DateOnly day) where T : class
    {
        // Full snapshots never borrow coverage from earlier snapshots. Ambiguous overlap fails closed.
        if (Conflict(facts, from, through)) return new(null, "REFERENCE_INTERVAL_CONFLICT");
        var ordered = facts.OrderBy(from).ToArray();
        var fact = ordered.FirstOrDefault(f => from(f) <= day && (through(f) is null || through(f) >= day));
        return new(fact, fact is null ? "REFERENCE_INTERVAL_MISSING" : null);
    }
    public static EvidenceResult<ScreenerIdentityInterval> Identity(InstrumentSnapshot snapshot, DateOnly day) =>
        Interval(snapshot.Identities, f => f.From, f => f.Through, day);
    public static EvidenceResult<ScreenerTradingInterval> Trading(InstrumentSnapshot snapshot, DateOnly day) =>
        Interval(snapshot.Trading, f => f.From, f => f.Through, day);

    public static EvidenceResult<ScreenerPriceInterval> Price(InstrumentSnapshot snapshot, ScreenerBarEvidence bar)
    {
        var identity = Identity(snapshot, bar.SessionDate);
        var price = Interval(snapshot.Prices, f => f.From, f => f.Through, bar.SessionDate);
        if (snapshot.InstrumentId != bar.InstrumentId || !identity.Available || identity.Value!.Classification is not ("ORDINARY" or "INDEX"))
            return new(null, identity.Reason ?? "IDENTITY_UNVERIFIED");
        if (!price.Available) return price;
        var f = price.Value!;
        var index = identity.Value.Classification == "INDEX";
        return f.Continuity == "RAW_AS_TRADED" && (index ? f.EventCoverage is "CLEARED" or "NOT_APPLICABLE" : f.EventCoverage == "CLEARED")
            && (index || identity.Value.Currency == "IDR" && bar.Volume > 0) && f.SourceId == bar.SourceId
            && f.Convention == (index ? "INDEX_LEVEL" : "STOCK_RAW")
            && f.ContentHashes.Contains(bar.ContentHash) && bar.Validate().Available ? price : new(null, "PRICE_BASIS_UNVERIFIED");
    }
    public static EvidenceResult<ScreenerVolumeInterval> Volume(InstrumentSnapshot snapshot, ScreenerBarEvidence bar)
    {
        var volume = Interval(snapshot.Volumes, f => f.From, f => f.Through, bar.SessionDate);
        if (!volume.Available) return volume;
        var identity = Identity(snapshot, bar.SessionDate);
        var f = volume.Value!;
        return snapshot.InstrumentId == bar.InstrumentId && identity.Available && identity.Value!.Classification == "ORDINARY"
            && identity.Value.Currency == "IDR" && f.Currency == "IDR" && f.RawPriceCompatible
            && f.Unit is "SHARES" or "SHARE_COUNT_CORROBORATED" && f.Basis != "UNKNOWN" && f.MarketSegment != "UNKNOWN"
            && f.SourceId == bar.SourceId && f.Unit == bar.VolumeUnit && f.Basis == bar.VolumeBasis
            && f.MarketSegment == bar.MarketSegment && f.ContentHashes.Contains(bar.ContentHash)
            && bar.Volume > 0 && bar.Validate().Available ? volume : new(null, "VOLUME_BASIS_UNVERIFIED");
    }

    public static string SnapshotHash(object snapshot) => Hash(snapshot, omitContentHash: true);
    public static string Hash(object value, bool omitContentHash = false) => Hash(value, omitContentHash, default);
    public static string Hash(object value, CancellationToken ct) => Hash(value, false, ct);
    private static string Hash(object value, bool omitContentHash, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var json = JsonSerializer.SerializeToDocument(value, JsonOptions);
        return Convert.ToHexStringLower(SHA256.HashData(CanonicalBytes(json.RootElement, omitContentHash, ct)));
    }
    private static byte[] CanonicalBytes(JsonElement element, bool omitContentHash, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(element, writer, omitContentHash, ct);
        return stream.ToArray();
    }
    private static void Write(JsonElement e, Utf8JsonWriter writer, bool omitContentHash, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (e.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var p in e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (omitContentHash && p.Name == "contentHash") continue;
                writer.WritePropertyName(p.Name);
                if (p.Name is "knownAt" or "retrievedAt" or "publishedAt" or "completedAt" or "known_at" or "retrieved_at" && p.Value.ValueKind == JsonValueKind.String)
                    writer.WriteStringValue(p.Value.GetDateTimeOffset().ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                else Write(p.Value, writer, false, ct);
            }
            writer.WriteEndObject();
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var child in e.EnumerateArray().Select(v => CanonicalBytes(v, false, ct))
                .OrderBy(v => System.Text.Encoding.UTF8.GetString(v), StringComparer.Ordinal))
            { ct.ThrowIfCancellationRequested(); writer.WriteRawValue(child); }
            writer.WriteEndArray();
        }
        else e.WriteTo(writer);
    }

    public static string SelectedDigest(ScreenerReadRequest request, ScreenerDatabaseEvidence database,
        SelectedScreenerReferences references, CancellationToken ct) => Hash(new
        {
            policyId = ScreenerReadRequest.PolicyId, request.HistoryAnchor, request.Through, cutoff = request.Cutoff.ToUniversalTime(),
            ids = request.InstrumentIds.Append(request.BenchmarkId).Distinct().ToArray(), request.BenchmarkId,
            bars = database.Bars.Select(b => new { b.InstrumentId, b.SessionDate, b.RevisionNumber, b.KnownAt, b.ContentHash }),
            listings = database.Listings, references.Universes, references.Instruments,
            references.Sessions, references.InstrumentSessions
        }, false, ct);
}
