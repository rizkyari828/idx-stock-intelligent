using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IdxStockIntelligence.Application;

public static class ScreenerEvidenceJson
{
    public static string Canonicalize(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            Write(document.RootElement, writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void Write(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    Write(item, writer);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}

public static class ScreenerEvidenceRevisionSeries
{
    // Namespaces a provider-native record reference by its provider/source identity so
    // that two unrelated source-native records cannot collide into one revision series.
    public const char NamespaceSeparator = '\u001f';

    public static string Canonical(string sourceId, string sourceReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
        if (sourceId.Contains(NamespaceSeparator) || sourceReference.Contains(NamespaceSeparator))
        {
            throw new ArgumentException("Source identity and reference cannot contain the revision-series namespace separator.", nameof(sourceReference));
        }

        return sourceId + NamespaceSeparator + sourceReference;
    }

    public static bool IsCanonicalFor(string revisionSeriesId, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(revisionSeriesId) || string.IsNullOrWhiteSpace(sourceId))
        {
            return false;
        }

        var prefix = sourceId + NamespaceSeparator;
        return revisionSeriesId.Length > prefix.Length && revisionSeriesId.StartsWith(prefix, StringComparison.Ordinal);
    }
}

public sealed record ScreenerEvidenceRecord
{
    public ScreenerEvidenceRecord(
        Guid evidenceId,
        Guid subjectId,
        EvidenceClaim claim,
        string policyId,
        int schemaVersion,
        string? revisionSeriesId,
        long revisionNumber,
        long? supersedesRevisionNumber,
        SourceAuthorityTier authorityTier,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        DateTimeOffset? effectiveAt,
        DateTimeOffset? publishedAt,
        DateTimeOffset? retrievedAt,
        DateTimeOffset knownAt,
        string sourceId,
        string? sourceReference,
        Guid? rawArtifactId,
        string payloadJson)
    {
        if (evidenceId == Guid.Empty)
        {
            throw new ArgumentException("Evidence identity cannot be empty.", nameof(evidenceId));
        }

        if (subjectId == Guid.Empty)
        {
            throw new ArgumentException("Subject identity cannot be empty.", nameof(subjectId));
        }

        if (policyId != ScreenerEvidenceV02.PolicyId)
        {
            throw new ArgumentException("Unsupported evidence policy identity.", nameof(policyId));
        }

        if (schemaVersion != ScreenerEvidenceV02.SchemaVersion)
        {
            throw new ArgumentException("Unsupported evidence schema version.", nameof(schemaVersion));
        }

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source identity is required.", nameof(sourceId));
        }

        if (revisionSeriesId is not null && !ScreenerEvidenceRevisionSeries.IsCanonicalFor(revisionSeriesId, sourceId))
        {
            throw new ArgumentException("Revision series identity must be the canonical namespaced identity for its source.", nameof(revisionSeriesId));
        }

        if (revisionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber), "Revision number must be positive.");
        }

        if (supersedesRevisionNumber is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(supersedesRevisionNumber), "Superseded revision must be positive.");
        }

        if (supersedesRevisionNumber is not null && revisionSeriesId is null)
        {
            throw new ArgumentException("Supersession requires a revision series identity.", nameof(supersedesRevisionNumber));
        }

        if (effectiveTo is { } to && to < effectiveFrom)
        {
            throw new ArgumentException("Effective interval end cannot precede its start.", nameof(effectiveTo));
        }

        if (retrievedAt is { } retrieved && knownAt < retrieved)
        {
            throw new ArgumentException("Known/admitted time cannot precede retrieval time.", nameof(knownAt));
        }

        if (rawArtifactId == Guid.Empty)
        {
            throw new ArgumentException("Raw artifact identity cannot be empty.", nameof(rawArtifactId));
        }

        string payload;
        try
        {
            payload = ScreenerEvidenceJson.Canonicalize(payloadJson);
        }
        catch (JsonException error)
        {
            throw new ArgumentException("Payload must be a JSON object.", nameof(payloadJson), error);
        }

        using (var document = JsonDocument.Parse(payload))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Payload must be a JSON object.", nameof(payloadJson));
            }
        }

        EvidenceId = evidenceId;
        SubjectId = subjectId;
        Claim = claim;
        PolicyId = policyId;
        SchemaVersion = schemaVersion;
        RevisionSeriesId = revisionSeriesId;
        RevisionNumber = revisionNumber;
        SupersedesRevisionNumber = supersedesRevisionNumber;
        AuthorityTier = authorityTier;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        EffectiveAt = effectiveAt;
        PublishedAt = publishedAt;
        RetrievedAt = retrievedAt;
        KnownAt = knownAt;
        SourceId = sourceId;
        SourceReference = sourceReference;
        RawArtifactId = rawArtifactId;
        Payload = payload;
        PayloadSha256 = ScreenerEvidenceJson.Sha256(payload);
    }

    public Guid EvidenceId { get; }
    public Guid SubjectId { get; }
    public EvidenceClaim Claim { get; }
    public string PolicyId { get; }
    public int SchemaVersion { get; }
    public string? RevisionSeriesId { get; }
    public long RevisionNumber { get; }
    public long? SupersedesRevisionNumber { get; }
    public SourceAuthorityTier AuthorityTier { get; }
    public DateOnly EffectiveFrom { get; }
    public DateOnly? EffectiveTo { get; }
    public DateTimeOffset? EffectiveAt { get; }
    public DateTimeOffset? PublishedAt { get; }
    public DateTimeOffset? RetrievedAt { get; }
    public DateTimeOffset KnownAt { get; }
    public string SourceId { get; }
    public string? SourceReference { get; }
    public Guid? RawArtifactId { get; }
    public string Payload { get; }
    public string PayloadSha256 { get; }

    public DateTimeOffset? RecordedAt { get; init; }
}
