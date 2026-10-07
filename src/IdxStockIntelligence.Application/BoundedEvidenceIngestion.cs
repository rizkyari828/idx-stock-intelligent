using System.Security.Cryptography;

namespace IdxStockIntelligence.Application;

public sealed record BoundedEvidenceMapping(EvidenceClaim Claim, EvidenceClass Class, SourceAuthorityTier Authority, ScreenerScopeKind Scope);
// Trusted operational adapter configuration, never a caller-supplied authority assertion.
// Python adapters parse provider formats; this boundary accepts only their provider-neutral typed output.
public sealed record BoundedEvidenceSource(string SourceId, string ParserVersion, string ContentType,
    IReadOnlyList<BoundedEvidenceMapping> Mappings,
    Func<ReadOnlyMemory<byte>, BoundedEvidenceArtifact, Guid, CancellationToken, IReadOnlyList<ScreenerEvidenceRecord>> Normalize);
public sealed record BoundedEvidenceArtifact(string SourceId, string SourceReference, string ParserVersion,
    string ContentType, DateTimeOffset ReceivedAt, DateTimeOffset KnownAt, DateTimeOffset? PublishedAt, string ContentSha256);
public sealed record BoundedEvidenceIngestionResult(string UniverseSnapshotId, string SourceId, string Status,
    Guid? ArtifactId, int ArtifactsAccepted, int ArtifactsRejected, int EvidenceAppended, int EvidenceAlreadyPresent,
    int UnsupportedRows, int ParseFailures, int SourceFailures, IReadOnlyList<string> Diagnostics)
{
    public int ArtifactsDiscovered { get; } = 1;
}

public static class BoundedEvidenceIngestion
{
    public const int MaximumArtifactBytes = 4 * 1024 * 1024;
    public const int MaximumRows = ScreenerEvidenceAsOf.MaximumRecords;
    public const string SourceUnavailable = "BOUNDED_SOURCE_NOT_YET_OPERATIONALLY_AVAILABLE";
    public static void Require(bool condition, string reason)
    { if (!condition) throw new EvidenceBindingException(reason); }
    private static bool Text(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200
        && value.Trim() == value && !value.Any(char.IsControl);
    public static Guid Identity(object value) => new(Convert.FromHexString(ScreenerReferences.Hash(value)[..32]), bigEndian: true);
    public static Guid EvidenceIdentity(Guid artifact, ScreenerEvidenceRecord row) => Identity(new
        { artifact, row.SourceId, row.SourceReference, row.SubjectId, row.Claim, row.RevisionNumber, row.PayloadSha256 });
    public static string ContentHash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static Guid Validate(UniverseSnapshot universe, Guid exchange, BoundedEvidenceArtifact artifact,
        ReadOnlyMemory<byte> raw, DateTimeOffset now)
    {
        ScreenerReferences.Validate(new(1, [universe], []));
        Require(exchange != Guid.Empty && universe.MemberIds.Count > 0 && universe.KnownAt <= artifact.KnownAt, "INGESTION_UNIVERSE_INVALID");
        Require(raw.Length is > 0 and <= MaximumArtifactBytes, "INGESTION_BOUND_EXCEEDED");
        Require(Text(artifact.SourceId) && Text(artifact.SourceReference) && Text(artifact.ParserVersion) && Text(artifact.ContentType), "INGESTION_PROVENANCE_INVALID");
        Require(artifact.ReceivedAt.Offset == TimeSpan.Zero && artifact.KnownAt.Offset == TimeSpan.Zero
            && artifact.ReceivedAt.Ticks % 10 == 0 && artifact.KnownAt.Ticks % 10 == 0
            && artifact.ReceivedAt <= artifact.KnownAt && artifact.KnownAt <= now
            && (artifact.PublishedAt is null || artifact.PublishedAt.Value.Offset == TimeSpan.Zero
                && artifact.PublishedAt.Value.Ticks % 10 == 0 && artifact.PublishedAt <= artifact.KnownAt), "INGESTION_CHRONOLOGY_INVALID");
        Require(ScreenerReferences.IsHash(artifact.ContentSha256) && artifact.ContentSha256 == ContentHash(raw.Span), "INGESTION_HASH_MISMATCH");
        return Identity(new { artifact.SourceId, artifact.SourceReference, artifact.ContentSha256, artifact.ParserVersion });
    }
    public static IReadOnlyList<ScreenerEvidenceRecord> ValidateRows(UniverseSnapshot universe, Guid exchange,
        BoundedEvidenceSource source, BoundedEvidenceArtifact artifact, Guid rawId, IReadOnlyList<ScreenerEvidenceRecord> rows)
    {
        Require(rows.Count is > 0 and <= MaximumRows, "INGESTION_BOUND_EXCEEDED");
        var allowed = universe.MemberIds.Append(universe.BenchmarkId).ToHashSet();
        Require(rows.Select(r => r.EvidenceId).Distinct().Count() == rows.Count, "INGESTION_DUPLICATE_IDENTITY");
        foreach (var row in rows)
        {
            _ = ScreenerEvidenceBinding.Decode(row);
            Require(row.IsBound && row.SourceId == source.SourceId && row.RawArtifactId == rawId
                && row.RetrievedAt == artifact.ReceivedAt && row.KnownAt == artifact.KnownAt && row.PublishedAt == artifact.PublishedAt
                && Text(row.SourceReference!) && row.RecordedAt is null, "INGESTION_PROVENANCE_INVALID");
            Require(row.EffectiveAt is null || row.EffectiveAt.Value.Ticks % 10 == 0, "INGESTION_CHRONOLOGY_INVALID");
            Require(row.EvidenceId == EvidenceIdentity(rawId, row), "INGESTION_IDENTITY_INVALID");
            Require(row.ScopeExchangeId == exchange && (row.ScopeKind == ScreenerScopeKind.EXCHANGE
                ? row.SubjectId == exchange : allowed.Contains(row.SubjectId)), "INGESTION_OUTSIDE_UNIVERSE");
            Require(source.Mappings.Contains(new(row.Claim, row.EvidenceClass!.Value, row.AuthorityTier, row.ScopeKind!.Value))
                && ScreenerSourceAdmission.Admit(row.Claim, row.AuthorityTier).Admitted, "INGESTION_SOURCE_INADMISSIBLE");
            Require(row.RevisionSeriesId is not null && row.RevisionNumber > 0
                && row.RevisionSeriesId == ScreenerEvidenceRevisionSeries.Canonical(row.SourceId, row.SourceReference!)
                && (row.RevisionNumber == 1 ? row.SupersedesRevisionNumber is null : row.SupersedesRevisionNumber is > 0 && row.SupersedesRevisionNumber < row.RevisionNumber), "INGESTION_LINEAGE_INVALID");
        }
        // Prerequisites are appended before dependents; unsupported cycles/missing links fail at the semantic store boundary.
        return rows.OrderBy(r => r.Claim == EvidenceClaim.GenuinePriceObservation ? 3
            : r.Claim == EvidenceClaim.Reopening ? 2 : r.Claim == EvidenceClaim.CorporateAction
                && ScreenerEvidenceBinding.Decode(r).Value is ActionCoverageValue ? 1 : 0)
            .ThenBy(r => r.RevisionNumber).ThenBy(r => r.EvidenceId.ToString("D"), StringComparer.Ordinal).ToArray();
    }
    public static void ValidateCorrection(ScreenerEvidenceRecord row, ScreenerEvidenceRecord previous,
        IReadOnlyDictionary<Guid, ScreenerEvidenceRecord> premises)
    {
        Require(previous.RevisionNumber == row.SupersedesRevisionNumber && previous.RevisionSeriesId == row.RevisionSeriesId
            && previous.SourceId == row.SourceId && previous.SubjectId == row.SubjectId && previous.Claim == row.Claim
            && previous.EvidenceClass == row.EvidenceClass && previous.ScopeKind == row.ScopeKind && previous.ScopeExchangeId == row.ScopeExchangeId
            && (row.EvidenceClass is not (EvidenceClass.SessionFact or EvidenceClass.PointObservation)
                || previous.EffectiveFrom == row.EffectiveFrom && previous.EffectiveTo == row.EffectiveTo)
            && previous.KnownAt <= row.KnownAt && previous.RetrievedAt <= row.KnownAt && previous.RecordedAt <= row.KnownAt
            && ScreenerEvidenceAsOf.SameLogicalFact(previous, row, premises), "INGESTION_LINEAGE_INVALID");
    }
}
