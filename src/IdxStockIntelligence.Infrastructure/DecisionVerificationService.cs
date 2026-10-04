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
        try { DecisionVerification.ValidateManifest(h, m); }
        catch (RetainedInputUnavailableException e) { throw new MissingInput(e.Message); }
    }

    internal static void Authenticate(DecisionSnapshotManifest m, ScreenerDatabaseEvidence evidence, CancellationToken ct)
    {
        try { DecisionVerification.Authenticate(m, evidence, ct); }
        catch (RetainedInputUnavailableException e) { throw new MissingInput(e.Message); }
    }

    internal static async Task<ScreenerReferenceBundle> ArchivesAsync(IReadOnlyList<DecisionReferenceArchive> archives, CancellationToken ct, bool serviceFailures = false, Dictionary<string, byte[]>? copies = null)
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
                if (copies?.TryGetValue(hash, out var retainedBytes) == true)
                {
                    Require(retainedBytes.LongLength == a.ByteLength, "REFERENCE_ARCHIVE_LENGTH_MISMATCH");
                    files.Add(retainedBytes); continue;
                }
                // Hash-only fixed internal root. No path from HTTP or the retained manifest.
                await using var stream = new FileStream(Path.Combine("data/raw/decision-reference", hash[..2], hash + ".json"),
                    FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
                Require(stream.Length == a.ByteLength, "REFERENCE_ARCHIVE_LENGTH_MISMATCH");
                var bytes = new byte[(int)a.ByteLength];
                await stream.ReadExactlyAsync(bytes, ct);
                Require(await stream.ReadAsync(new byte[1], ct) == 0, "REFERENCE_ARCHIVE_LENGTH_MISMATCH");
                try { OutcomeVerification.AuthenticateArchive(a, bytes); }
                catch (RetainedInputUnavailableException e) { throw new MissingInput(e.Message); }
                files.Add(bytes);
                copies?.Add(hash, bytes);
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or EndOfStreamException)
            { throw new MissingInput("REFERENCE_ARCHIVE_UNAVAILABLE"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
            {
                if (serviceFailures) throw new ScreenerException(503, "OUTCOME_VERIFICATION_UNAVAILABLE");
                throw new MissingInput("REFERENCE_ARCHIVE_UNAVAILABLE");
            }
        }
        var parsed = ScreenerReferenceFiles.Parse(files[0], files[1], files[2], ct);
        Require(parsed.Available, "RETAINED_REFERENCE_INVALID");
        return parsed.Value!;
    }
}
