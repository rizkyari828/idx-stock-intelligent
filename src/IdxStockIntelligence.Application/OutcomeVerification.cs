using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public enum OutcomeVerificationState
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("MATCH")] Match,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("INPUT_NOT_AVAILABLE")] InputNotAvailable,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("POLICY_VERSION_UNAVAILABLE")] PolicyVersionUnavailable,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("DIFFERENT_RESULT")] DifferentResult
}

public sealed record OutcomeVerificationProjection(Guid RunId, Guid InstrumentId, int HorizonSessions,
    string OutcomePolicyId, int SchemaVersion, DateOnly? AnchorMarketDate, decimal? AnchorClose,
    DateOnly? HorizonMarketDate, decimal? HorizonClose, string TerminalState, string? TerminalReason,
    decimal? PriceReturnPct, DateTimeOffset OutcomeKnownAt, DateTimeOffset RecordedAt);
public sealed record OutcomeDifference(string Field, string? StoredValue, string? RecomputedValue, string Reason);
public sealed record OutcomeVerificationResult(Guid RunId, Guid InstrumentId, int HorizonSessions,
    OutcomeVerificationState State, DateTimeOffset VerifiedAt, string OutcomePolicyId, int SchemaVersion,
    DateTimeOffset OutcomeKnownAt, DateTimeOffset RecordedAt, OutcomeVerificationProjection? ReplayedProjection,
    IReadOnlyList<OutcomeDifference> Differences, bool DifferencesTruncated, string? Detail);

public static class OutcomeVerification
{
    public const int MaximumDifferences = 100;
    private static void Require(bool condition, string reason)
    { if (!condition) throw new RetainedInputUnavailableException(reason); }
    private static bool Same(object a, object b, CancellationToken ct)
        => ScreenerReferences.Hash(a, ct) == ScreenerReferences.Hash(b, ct);

    public static void ValidateRequest(Guid run, Guid instrument, int horizon, JsonElement body)
    {
        if (run == Guid.Empty || instrument == Guid.Empty || !OutcomeRequest.Horizons.Contains(horizon)
            || body.ValueKind != JsonValueKind.Object || body.EnumerateObject().Any())
            throw new ScreenerException(400, "OUTCOME_VERIFICATION_REQUEST_INVALID");
    }

    public static Func<DecisionSnapshotHeader, DecisionSnapshotRow, int, OutcomeHorizon, OutcomeInputs,
        DateTimeOffset, CancellationToken, OutcomeCell>? ResolvePolicy(string policy, int schema,
        string capturePolicy, int captureSchema, string captureKind, string universe)
        => (policy, schema, capturePolicy, captureSchema, captureKind, universe) switch
        {
            ("outcome-v0.1.0", 1, "screener-v0.1.0", 1, "PROSPECTIVE_CAPTURE", "PILOT") => OutcomeEvaluator.Evaluate,
            _ => null
        };

    public static OutcomeVerificationResult Unavailable(Guid run, Guid instrument, int horizon,
        string policy, int schema, DateTimeOffset knownAt, DateTimeOffset recordedAt,
        DateTimeOffset verifiedAt, OutcomeVerificationState state, string detail)
        => new(run, instrument, horizon, state, verifiedAt, policy, schema, knownAt, recordedAt, null, [], false, detail);

    public static void ValidateArchives(IReadOnlyList<DecisionReferenceArchive> archives)
    {
        string[] kinds = ["screener-reference", "sessions", "instrument-sessions"];
        Require(archives is not null && archives.Count == 3 && archives.All(a => a is not null)
            && archives.Select(a => a.Kind).Distinct(StringComparer.Ordinal).Count() == 3
            && archives.All(a => kinds.Contains(a.Kind, StringComparer.Ordinal)), "REFERENCE_ARCHIVE_LINK_INVALID");
        long bytes = 0;
        foreach (var a in archives!)
            Require(a.Present ? ScreenerReferences.IsHash(a.ContentSha256) && a.ByteLength >= 0
                && (bytes += a.ByteLength) <= ScreenerReferences.MaximumBytes
                : a.ContentSha256 is null && a.ByteLength == 0, "REFERENCE_ARCHIVE_LINK_INVALID");
    }

    // Byte authentication is pure; the service copies files once and parses only these bytes.
    public static void AuthenticateArchive(DecisionReferenceArchive link, ReadOnlySpan<byte> bytes)
    {
        Require(link.Present && link.Kind is "screener-reference" or "sessions" or "instrument-sessions"
            && bytes.Length <= ScreenerReferences.MaximumBytes && ScreenerReferences.IsHash(link.ContentSha256), "REFERENCE_ARCHIVE_LINK_INVALID");
        Require(link.ByteLength == bytes.Length, "REFERENCE_ARCHIVE_LENGTH_MISMATCH");
        Require(Convert.ToHexStringLower(SHA256.HashData(bytes)) == link.ContentSha256, "REFERENCE_ARCHIVE_HASH_MISMATCH");
    }

    public static void ValidateManifest(OutcomeVerificationProjection stored, DecisionSnapshotHeader h,
        DecisionSnapshotRow row, DecisionSnapshotManifest capture, OutcomeManifest m)
    {
        DecisionVerification.ValidateManifest(h, capture);
        Require(h.RunId == stored.RunId && row.InstrumentId == stored.InstrumentId
            && h.RowCount is > 0 and <= 210 && capture.EvaluatedInstrumentIds.Count == h.RowCount
            && capture.EvaluatedInstrumentIds.Contains(row.InstrumentId) && h.TargetSession is not null
            && row.Result is not null && row.Result.Provenance is not null && row.Result.FieldStates is not null,
            "CAPTURE_LINK_INVALID");
        Require(m is not null && m.SchemaVersion == 1 && m.OutcomePolicyId == stored.OutcomePolicyId
            && m.InstrumentId == stored.InstrumentId && m.HorizonSessions == stored.HorizonSessions
            && OutcomeRequest.Horizons.Contains(m.HorizonSessions)
            && m.CaptureSchemaVersion == h.SchemaVersion && m.CapturePolicyId == h.PolicyId
            && ScreenerReferences.IsHash(m.CaptureInputHash) && m.CaptureInputHash == h.InputHash
            && ScreenerReferences.IsHash(m.CaptureSelectedDigest) && m.CaptureSelectedDigest == h.SelectedDigest,
            "OUTCOME_MANIFEST_IDENTITY_INVALID");
        Require(m!.EvaluationCutoff == stored.OutcomeKnownAt && stored.RecordedAt >= stored.OutcomeKnownAt
            && stored.OutcomeKnownAt >= h.CapturedAt, "OUTCOME_MANIFEST_CHRONOLOGY_INVALID");
        Require(m.Calendar is { Count: >= 2 and <= 366 } && m.Calendar.All(d => d is not null)
            && m.Instruments is not null && m.InstrumentSessions is not null
            && m.Instruments.Count + m.InstrumentSessions.Count <= ScreenerReferences.MaximumRecords
            && (m.TerminalCondition is null || m.TerminalCondition.Length is > 0 and <= 128), "OUTCOME_MANIFEST_INVALID");
        // Required linkage follows the manifest's original assertion, not possibly altered output scalars.
        Require(m.TerminalCondition is not null || m.Anchor is not null && m.Endpoint is not null, "OUTCOME_REQUIRED_LINK_MISSING");
        Require(m.TerminalCondition != "POST_DELISTING" || m.Listing is not null, "OUTCOME_REQUIRED_LINK_MISSING");
        var calendar = m.Calendar!;
        Require(calendar[0].Date == h.TargetSession && calendar[0].Classification == "ANCHOR"
            && calendar[^1].Classification == "ObservedTrading"
            && calendar[^1].Date <= OutcomeEvaluator.Through(stored.OutcomeKnownAt)
            && calendar[^1].Date <= ScreenerReadRequest.Horizon
            && calendar.Skip(1).Count(d => d.Classification == "ObservedTrading") == m.HorizonSessions,
            "OUTCOME_CALENDAR_INVALID");
        for (var i = 1; i < calendar.Count; i++)
            Require(calendar[i].Date == calendar[i - 1].Date.AddDays(1)
                && calendar[i].Classification is "ObservedTrading" or "Weekend" or "AnnouncedClosed" or "ExceptionalClosure"
                && (calendar[i].Proof is null || calendar[i].Proof!.Date == calendar[i].Date), "OUTCOME_CALENDAR_INVALID");
        foreach (var b in new[] { m.Anchor, m.Endpoint }.OfType<DecisionBarLink>())
            Require(b.InstrumentId == stored.InstrumentId && b.RevisionNumber > 0 && b.RawArtifactId != Guid.Empty
                && ScreenerReferences.IsHash(b.ContentHash) && b.KnownAt <= stored.OutcomeKnownAt
                && b.SessionDate >= h.HistoryAnchor && b.SessionDate <= calendar[^1].Date, "OUTCOME_CANONICAL_LINK_INVALID");
        Require(m.Anchor is null || capture.Bars.Contains(m.Anchor), "CAPTURE_ANCHOR_LINK_INVALID");
        Require(m.Endpoint is null || m.Endpoint.SessionDate == calendar[^1].Date, "OUTCOME_ENDPOINT_LINK_INVALID");
        Require(m.Listing is null || m.Listing.InstrumentId == stored.InstrumentId
            && m.Listing.KnownAt <= stored.OutcomeKnownAt && ScreenerReferences.IsHash(m.Listing.ContentHash), "OUTCOME_LISTING_LINK_INVALID");
        Require(m.Instruments!.All(r => r is not null && !string.IsNullOrWhiteSpace(r.SnapshotId)
                && r.SnapshotId.Length <= 2000 && r.KnownAt <= stored.OutcomeKnownAt && ScreenerReferences.IsHash(r.ContentHash))
            && m.Instruments!.Distinct().Count() == m.Instruments!.Count
            && m.InstrumentSessions!.All(p => p is not null && p.Instrument.Value == stored.InstrumentId
                && p.Date >= h.HistoryAnchor && p.Date <= calendar[^1].Date && p.KnownAt <= stored.OutcomeKnownAt)
            && m.InstrumentSessions!.Distinct().Count() == m.InstrumentSessions!.Count, "OUTCOME_REFERENCE_LINK_INVALID");
        ValidateArchives(capture.Archives);
        ValidateArchives(m.Archives);
    }

    // References are selected by the frozen selector from authenticated copies, never live files.
    // All database inputs are the exact retained keys. Integrity precedes result comparison.
    public static OutcomeVerificationResult Verify(OutcomeVerificationProjection stored, DecisionSnapshotHeader h,
        DecisionSnapshotRow row, DecisionSnapshotManifest capture, OutcomeManifest manifest, OutcomeInputs inputs,
        IReadOnlyList<SessionProof> retainedHorizonProofs, DateTimeOffset verifiedAt, CancellationToken ct = default)
    {
        var replay = ResolvePolicy(stored.OutcomePolicyId, stored.SchemaVersion, h.PolicyId, h.SchemaVersion, h.CaptureKind, h.Universe);
        OutcomeVerificationResult Fail(OutcomeVerificationState state, string detail) => Unavailable(stored.RunId,
            stored.InstrumentId, stored.HorizonSessions, stored.OutcomePolicyId, stored.SchemaVersion,
            stored.OutcomeKnownAt, stored.RecordedAt, verifiedAt, state, detail);
        if (replay is null) return Fail(OutcomeVerificationState.PolicyVersionUnavailable, "POLICY_OR_SCHEMA_UNSUPPORTED");
        try
        {
            ValidateManifest(stored, h, row, capture, manifest);
            var old = inputs.CapturedReferences; var forward = inputs.ForwardReferences;
            Require(Same(capture.Universes, old.Universes.Select(i => new DecisionReferenceLink(i.SnapshotId, i.KnownAt, i.ContentHash)).ToArray(), ct)
                && Same(capture.Instruments, old.Instruments.Select(i => new DecisionReferenceLink(i.SnapshotId, i.KnownAt, i.ContentHash)).ToArray(), ct)
                && Same(capture.Sessions, old.Sessions, ct) && Same(capture.InstrumentSessions, old.InstrumentSessions, ct)
                && Same(manifest.Instruments, forward.Instruments.Select(i => new DecisionReferenceLink(i.SnapshotId, i.KnownAt, i.ContentHash)).ToArray(), ct)
                && forward.Instruments.All(i => i.InstrumentId == row.InstrumentId)
                && Same(manifest.InstrumentSessions, forward.InstrumentSessions, ct), "REFERENCE_SELECTION_MISMATCH");
            DecisionVerification.Authenticate(capture, inputs.CapturedDatabase, ct);
            Require(ScreenerReferences.SelectedDigest(capture.Request, inputs.CapturedDatabase, old, ct) == h.SelectedDigest,
                "CAPTURE_SELECTED_DIGEST_MISMATCH");
            var exactForward = capture with
            {
                Request = new([row.InstrumentId], capture.Request.BenchmarkId, h.HistoryAnchor, manifest.Calendar[^1].Date, stored.OutcomeKnownAt),
                Bars = manifest.Endpoint is null ? [] : [manifest.Endpoint],
                Listings = manifest.Listing is null ? [] : [manifest.Listing]
            };
            DecisionVerification.Authenticate(exactForward, inputs.ForwardDatabase, ct);
            var anchor = OutcomeEvaluator.Anchor(row, inputs);
            var anchorLink = anchor is null ? null : new DecisionBarLink(anchor.InstrumentId, anchor.SessionDate,
                anchor.RevisionNumber, anchor.KnownAt, anchor.ContentHash, anchor.RawArtifactId);
            Require(anchorLink == manifest.Anchor, "CAPTURE_ANCHOR_LINK_INVALID");
            var baseProof = ScreenerSessions.Resolve(old.Sessions, h.TargetSession!.Value, h.Through, h.KnowledgeCutoff).Proof;
            Require(baseProof == manifest.Calendar[0].Proof, "CAPTURE_SESSION_LINK_INVALID");
            var horizon = OutcomeEvaluator.ResolveHorizon(h, stored.HorizonSessions, retainedHorizonProofs, stored.OutcomeKnownAt, ct);
            // Compare ordered typed days. The reference hash deliberately treats arrays as sets.
            Require(horizon.Date is not null && horizon.Calendar.SequenceEqual(manifest.Calendar.Skip(1)), "HORIZON_SESSION_LINK_INVALID");
            var cell = replay(h, row, stored.HorizonSessions, horizon, inputs, stored.OutcomeKnownAt, ct);
            var projection = new OutcomeVerificationProjection(h.RunId, cell.InstrumentId, cell.HorizonSessions,
                OutcomeEvaluator.PolicyId, 1, cell.AnchorMarketDate, cell.AnchorClose, cell.HorizonMarketDate,
                cell.HorizonClose, cell.State, cell.Reason, cell.PriceReturnPct, manifest.EvaluationCutoff, stored.RecordedAt);
            return Compare(stored, projection, manifest.TerminalCondition, verifiedAt);
        }
        catch (RetainedInputUnavailableException e) { return Fail(OutcomeVerificationState.InputNotAvailable, e.Message); }
    }

    public static OutcomeVerificationResult Compare(OutcomeVerificationProjection stored, OutcomeVerificationProjection replayed,
        string? terminalCondition, DateTimeOffset verifiedAt)
    {
        var differences = new List<OutcomeDifference>(); var truncated = false;
        void Difference(string field, object? a, object? b)
        {
            if (Equals(a, b)) return;
            if (differences.Count == MaximumDifferences) { truncated = true; return; }
            static string? Text(object? value, string field)
            {
                if (value is string code && field is "terminalReason" or "manifest.terminalCondition"
                    && code.Any(c => c is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '_')))
                    return "[invalid reason]";
                var text = value switch { null => null, string s => s, DateTimeOffset t => t.ToUniversalTime().ToString("O"),
                    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => "[object]" };
                return text is { Length: > 160 } ? text[..160] + "…" : text;
            }
            differences.Add(new(field, Text(a, field), Text(b, field), "TYPED_VALUE_DIFFERENT"));
        }
        Difference("anchorClose", stored.AnchorClose, replayed.AnchorClose);
        Difference("anchorMarketDate", stored.AnchorMarketDate, replayed.AnchorMarketDate);
        Difference("horizonClose", stored.HorizonClose, replayed.HorizonClose);
        Difference("horizonMarketDate", stored.HorizonMarketDate, replayed.HorizonMarketDate);
        Difference("horizonSessions", stored.HorizonSessions, replayed.HorizonSessions);
        Difference("instrumentId", stored.InstrumentId, replayed.InstrumentId);
        Difference("manifest.terminalCondition", terminalCondition, replayed.TerminalReason);
        Difference("outcomeKnownAt", stored.OutcomeKnownAt, replayed.OutcomeKnownAt);
        Difference("outcomePolicyId", stored.OutcomePolicyId, replayed.OutcomePolicyId);
        Difference("priceReturnPct", stored.PriceReturnPct, replayed.PriceReturnPct);
        Difference("recordedAt", stored.RecordedAt, replayed.RecordedAt);
        Difference("runId", stored.RunId, replayed.RunId);
        Difference("schemaVersion", stored.SchemaVersion, replayed.SchemaVersion);
        Difference("terminalReason", stored.TerminalReason, replayed.TerminalReason);
        Difference("terminalState", stored.TerminalState, replayed.TerminalState);
        return new(stored.RunId, stored.InstrumentId, stored.HorizonSessions,
            differences.Count == 0 ? OutcomeVerificationState.Match : OutcomeVerificationState.DifferentResult,
            verifiedAt, stored.OutcomePolicyId, stored.SchemaVersion, stored.OutcomeKnownAt, stored.RecordedAt,
            replayed, differences, truncated, null);
    }
}
