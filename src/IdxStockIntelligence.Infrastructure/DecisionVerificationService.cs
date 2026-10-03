using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Npgsql;

namespace IdxStockIntelligence.Infrastructure;

public sealed class DecisionVerificationService(NpgsqlDataSource dataSource)
{
    // Bind the frozen policy/projection pair to its implementation, not merely a supported flag.
    public static Func<ScreenerReadRequest, ScreenerDatabaseEvidence, SelectedScreenerReferences,
        ScreenerPortfolioHistory?, CancellationToken, ScreenerEvaluationRead>? ResolvePolicy(DecisionSnapshotHeader h)
        => (h.PolicyId, h.SchemaVersion) switch
        {
            ("screener-v0.1.0", 1) => ScreenerService.Evaluate,
            _ => null
        };
    internal sealed class MissingInput(string reason) : Exception(reason);
    private static void Require(bool condition, string reason) { if (!condition) throw new MissingInput(reason); }
    private static bool Same(object a, object b, CancellationToken ct) => ScreenerReferences.Hash(a, ct) == ScreenerReferences.Hash(b, ct);

    public async Task<DecisionVerificationResult> VerifyAsync(Guid id, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction) { CommandTimeout = 15 })
                await readOnly.ExecuteNonQueryAsync(token);
            var stored = await DecisionSnapshotService.ReadRunAsync(connection, transaction, id, token)
                ?? throw new ScreenerException(404, "SNAPSHOT_NOT_FOUND");
            var replay = ResolvePolicy(stored.Header);
            if (replay is null)
                return DecisionVerification.Unavailable(stored.Header, DecisionVerificationState.PolicyVersionUnavailable, "POLICY_OR_PROJECTION_UNSUPPORTED");
            try
            {
                var manifest = await ManifestAsync(connection, transaction, id, token);
                ValidateManifest(stored.Header, manifest);
                var bundle = await ArchivesAsync(manifest.Archives, token);
                var request = manifest.Request;
                var references = ScreenerReferences.Select(bundle.Reference, bundle.Sessions, bundle.InstrumentSessions, request, token);
                Require(Same(manifest.Universes, references.Universes.Select(r => new DecisionReferenceLink(r.SnapshotId, r.KnownAt, r.ContentHash)).ToArray(), token)
                    && Same(manifest.Instruments, references.Instruments.Select(r => new DecisionReferenceLink(r.SnapshotId, r.KnownAt, r.ContentHash)).ToArray(), token)
                    && Same(manifest.Sessions, references.Sessions, token) && Same(manifest.InstrumentSessions, references.InstrumentSessions, token), "REFERENCE_SELECTION_MISMATCH");
                var read = await ScreenerEvidenceDatabase.ReadAsync(connection, transaction, request, token, manifest);
                Require(read.Available, "RETAINED_DATABASE_EVIDENCE_UNAVAILABLE");
                Authenticate(manifest, read.Value!, token);
                ScreenerPortfolioHistory? history = null;
                if (manifest.Portfolio is { } link)
                {
                    try { history = await PortfolioDatabase.ScreenerHistoryAsync(connection, link.PortfolioId, request.Cutoff, token, link); }
                    catch (KeyNotFoundException) { throw new MissingInput("PORTFOLIO_INPUT_MISSING"); }
                    Require(Same(link.EventIds, history.Events.Select(e => e.Id).ToArray(), token)
                        && Same(link.ThesisIds, history.Theses.Select(t => t.Id).ToArray(), token), "PORTFOLIO_INPUT_MISSING");
                }
                var universe = ScreenerReferences.Universe(bundle.Reference, request.Cutoff);
                var configured = (universe.Value?.MemberIds ?? []).Order().ToArray();
                var held = history is null ? [] : PortfolioLedger.Project(history.Events, request.Cutoff, request.Through,
                    history.Portfolio.AllowNegativeCash).Positions.Where(p => p.Shares > 0).Select(p => p.InstrumentId).Order().ToArray();
                var population = configured.Concat(held).Distinct().Order().ToArray();
                Require(Same(configured, manifest.ConfiguredIds, token) && Same(held, manifest.HeldIds, token)
                    && Same(population, request.InstrumentIds, token) && Same(population, manifest.EvaluatedInstrumentIds, token), "MANIFEST_POPULATION_MISMATCH");
                var evaluated = replay(request, read.Value!, references, history, token);
                var query = new ScreenerQuery(request.Through, request.Cutoff, stored.Header.PortfolioId, "all", "ALL", "ALL", 0, 100, null);
                var (output, rows, status) = DecisionSnapshotService.Project(evaluated, query, token);
                var result = DecisionVerification.Compare(stored, output, rows, status, evaluated.Result.TargetSession,
                    evaluated.Result.UniverseSnapshotId, evaluated.InputHash, evaluated.SelectedDigest, token);
                await transaction.CommitAsync(token);
                return result;
            }
            catch (MissingInput e) { return DecisionVerification.Unavailable(stored.Header, DecisionVerificationState.InputNotAvailable, e.Message); }
            catch (Exception e) when (e is JsonException or ArgumentException or OverflowException or FormatException)
            { return DecisionVerification.Unavailable(stored.Header, DecisionVerificationState.InputNotAvailable, "RETAINED_INPUT_INVALID"); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ScreenerException(503, "VERIFICATION_UNAVAILABLE"); }
        catch (Exception e) when (e is NpgsqlException or TimeoutException or InvalidOperationException or JsonException)
        { ct.ThrowIfCancellationRequested(); throw new ScreenerException(503, "VERIFICATION_UNAVAILABLE"); }
    }

    internal static async Task<DecisionSnapshotManifest> ManifestAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT evidence_manifest::text FROM decision_snapshot_run WHERE run_id=$1", connection, transaction) { CommandTimeout = 15 };
        command.Parameters.AddWithValue(id);
        var json = (string?)await command.ExecuteScalarAsync(ct);
        Require(json is not null && System.Text.Encoding.UTF8.GetByteCount(json) <= 32 * 1024 * 1024, "MANIFEST_BOUND_OR_MISSING");
        return JsonSerializer.Deserialize<DecisionSnapshotManifest>(json!, DecisionSnapshotService.JsonOptions) ?? throw new MissingInput("MANIFEST_INVALID");
    }

    internal static void ValidateManifest(DecisionSnapshotHeader h, DecisionSnapshotManifest m)
    {
        Require(m.SchemaVersion == 1 && m.Request is not null && m.Request.BoundsReason() is null
            && m.Request.InstrumentIds.Distinct().Count() == m.Request.InstrumentIds.Count
            && m.Request.Through == h.Through && m.Request.HistoryAnchor == h.HistoryAnchor && m.Request.Cutoff == h.KnowledgeCutoff
            && h.CapturedAt == h.KnowledgeCutoff && h.RecordedAt >= h.CapturedAt && h.CaptureKind == "PROSPECTIVE_CAPTURE" && h.Universe == "PILOT", "MANIFEST_CHRONOLOGY_INVALID");
        Require(m.Bars is not null && m.Listings is not null && m.ConfiguredIds is not null && m.HeldIds is not null
            && m.EvaluatedInstrumentIds is not null && m.Universes is not null && m.Instruments is not null
            && m.Sessions is not null && m.InstrumentSessions is not null && m.Archives is not null, "MANIFEST_INVALID");
        var r = m.Request!;
        var ids = r.InstrumentIds.Append(r.BenchmarkId).ToHashSet();
        Require(m.Bars!.All(b => b is not null) && m.Listings!.All(l => l is not null), "MANIFEST_EVIDENCE_INVALID");
        Require(m.Bars!.Count <= ScreenerReadRequest.MaximumRows && m.Listings!.Count <= ids.Count
            && m.Bars.Select(b => (b.InstrumentId, b.SessionDate)).Distinct().Count() == m.Bars.Count
            && m.Listings.Select(l => l.InstrumentId).Distinct().Count() == m.Listings.Count
            && m.Bars.All(b => b is not null && ids.Contains(b.InstrumentId) && b.SessionDate >= r.HistoryAnchor && b.SessionDate <= r.Through
                && b.KnownAt <= r.Cutoff && b.RevisionNumber > 0 && b.RawArtifactId != Guid.Empty && ScreenerReferences.IsHash(b.ContentHash))
            && m.Listings.All(l => l is not null && ids.Contains(l.InstrumentId) && l.KnownAt <= r.Cutoff && ScreenerReferences.IsHash(l.ContentHash)), "MANIFEST_EVIDENCE_INVALID");
        Require(m.Universes!.Count + m.Instruments!.Count + m.Sessions!.Count + m.InstrumentSessions!.Count
            <= ScreenerReferences.MaximumRecords, "MANIFEST_REFERENCE_BOUND_EXCEEDED");
        foreach (var set in new[] { m.ConfiguredIds!, m.HeldIds!, m.EvaluatedInstrumentIds! })
            Require(set.Count <= 210 && set.Distinct().Count() == set.Count && set.All(id => id != Guid.Empty), "MANIFEST_POPULATION_INVALID");
        Require(m.ConfiguredIds!.Count <= 10 && m.HeldIds!.Count <= 200
            && (m.Portfolio?.PortfolioId == h.PortfolioId) && (h.PortfolioId is not null || m.Portfolio is null), "MANIFEST_PORTFOLIO_INVALID");
        if (m.Portfolio is { } p)
            Require(p.EventIds is not null && p.ThesisIds is not null && p.EventIds.Count <= PortfolioLedger.MaximumEvents
                && p.ThesisIds.Count <= PortfolioExchange.MaxTheses && p.EventIds.Distinct().Count() == p.EventIds.Count
                && p.ThesisIds.Distinct().Count() == p.ThesisIds.Count && p.EventIds.Concat(p.ThesisIds).All(id => id != Guid.Empty), "MANIFEST_PORTFOLIO_INVALID");
    }

    internal static void Authenticate(DecisionSnapshotManifest m, ScreenerDatabaseEvidence evidence, CancellationToken ct)
    {
        Require(evidence.Bars.Count == m.Bars.Count, "CANONICAL_REVISION_MISSING");
        var links = m.Bars.ToDictionary(b => (b.InstrumentId, b.SessionDate, b.RevisionNumber));
        foreach (var b in evidence.Bars)
        {
            ct.ThrowIfCancellationRequested();
            Require(links.TryGetValue((b.InstrumentId, b.SessionDate, b.RevisionNumber), out var link)
                && link.KnownAt == b.KnownAt && link.ContentHash == b.ContentHash && link.RawArtifactId == b.RawArtifactId
                && b.FetchedAt <= m.Request.Cutoff && (b.RetrievedAt is null || b.RetrievedAt <= m.Request.Cutoff)
                && (b.SessionKnownAt is null || b.SessionKnownAt <= m.Request.Cutoff) && b.ContentHashMatches(), "CANONICAL_REVISION_INTEGRITY_FAILED");
        }
        Require(evidence.Listings.Count == m.Listings.Count, "LISTING_INPUT_MISSING");
        var listings = m.Listings.ToDictionary(l => l.InstrumentId);
        Require(evidence.Listings.All(l => listings.TryGetValue(l.InstrumentId, out var link)
            && link.KnownAt == l.KnownAt && link.ContentHash == l.ContentHash), "LISTING_INPUT_INTEGRITY_FAILED");
    }

    internal static async Task<ScreenerReferenceBundle> ArchivesAsync(IReadOnlyList<DecisionReferenceArchive> archives, CancellationToken ct)
    {
        string[] kinds = ["screener-reference", "sessions", "instrument-sessions"];
        Require(archives.Count == 3 && archives.All(a => a is not null) && archives.Select(a => a.Kind).Distinct().Count() == 3
            && archives.All(a => kinds.Contains(a.Kind, StringComparer.Ordinal)), "REFERENCE_ARCHIVE_LINK_INVALID");
        var files = new List<byte[]>();
        long total = 0;
        foreach (var kind in kinds)
        {
            ct.ThrowIfCancellationRequested();
            var a = archives.Single(a => a.Kind == kind);
            if (!a.Present) { Require(a.ContentSha256 is null && a.ByteLength == 0, "REFERENCE_ABSENCE_INVALID"); files.Add([]); continue; }
            Require(ScreenerReferences.IsHash(a.ContentSha256) && a.ByteLength >= 0
                && (total += a.ByteLength) <= ScreenerReferences.MaximumBytes, "REFERENCE_ARCHIVE_LINK_INVALID");
            var hash = a.ContentSha256!;
            try
            {
                // Hash-only fixed internal root. No path from HTTP or the retained manifest.
                await using var stream = new FileStream(Path.Combine("data/raw/decision-reference", hash[..2], hash + ".json"),
                    FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
                Require(stream.Length == a.ByteLength, "REFERENCE_ARCHIVE_LENGTH_MISMATCH");
                var bytes = new byte[(int)a.ByteLength];
                await stream.ReadExactlyAsync(bytes, ct);
                Require(await stream.ReadAsync(new byte[1], ct) == 0 && Convert.ToHexStringLower(SHA256.HashData(bytes)) == hash, "REFERENCE_ARCHIVE_HASH_MISMATCH");
                files.Add(bytes);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
            { throw new MissingInput("REFERENCE_ARCHIVE_UNAVAILABLE"); }
        }
        var parsed = ScreenerReferenceFiles.Parse(files[0], files[1], files[2], ct);
        Require(parsed.Available, "RETAINED_REFERENCE_INVALID");
        return parsed.Value!;
    }
}
