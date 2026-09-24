using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Application;

public sealed record DailyBarRevision(
    long RevisionNumber,
    DailyBar Bar,
    DateTimeOffset KnownAt,
    string ContentSha256,
    Guid IngestionRunId);

public enum IngestionDisposition
{
    Inserted,
    RevisionAppended,
    DuplicateIgnored
}

public sealed record IngestionResult(IngestionDisposition Disposition, DailyBarRevision Revision);

public sealed class DailyBarRevisionStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(InstrumentId InstrumentId, DateOnly SessionDate), List<DailyBarRevision>> _revisions = [];

    public IngestionResult Ingest(
        DailyBar bar,
        DateTimeOffset knownAt,
        string canonicalContentSha256,
        Guid ingestionRunId)
    {
        ArgumentNullException.ThrowIfNull(bar);

        if (knownAt < bar.Source.AvailableAt)
        {
            throw new ArgumentException("Canonical knowledge cannot predate source availability.", nameof(knownAt));
        }

        if (canonicalContentSha256.Length != 64 || !canonicalContentSha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Canonical content requires a SHA-256 hex digest.", nameof(canonicalContentSha256));
        }

        if (ingestionRunId == Guid.Empty)
        {
            throw new ArgumentException("Ingestion run identity cannot be empty.", nameof(ingestionRunId));
        }

        var normalizedHash = canonicalContentSha256.ToLowerInvariant();
        var key = (bar.InstrumentId, bar.SessionDate);

        lock (_gate)
        {
            if (!_revisions.TryGetValue(key, out var revisions))
            {
                revisions = [];
                _revisions.Add(key, revisions);
            }

            var duplicate = revisions.FirstOrDefault(item => item.ContentSha256 == normalizedHash);
            if (duplicate is not null)
            {
                return new IngestionResult(IngestionDisposition.DuplicateIgnored, duplicate);
            }

            var revision = new DailyBarRevision(
                revisions.Count + 1L,
                bar,
                knownAt,
                normalizedHash,
                ingestionRunId);

            revisions.Add(revision);
            return new IngestionResult(
                revisions.Count == 1 ? IngestionDisposition.Inserted : IngestionDisposition.RevisionAppended,
                revision);
        }
    }

    public DailyBarRevision? AsOf(InstrumentId instrumentId, DateOnly sessionDate, DateTimeOffset cutoff)
    {
        lock (_gate)
        {
            return _revisions.TryGetValue((instrumentId, sessionDate), out var revisions)
                ? revisions
                    .Where(item => item.KnownAt <= cutoff)
                    .OrderByDescending(item => item.KnownAt)
                    .ThenByDescending(item => item.RevisionNumber)
                    .FirstOrDefault()
                : null;
        }
    }

    public IReadOnlyList<DailyBarRevision> History(InstrumentId instrumentId, DateOnly sessionDate)
    {
        lock (_gate)
        {
            return _revisions.TryGetValue((instrumentId, sessionDate), out var revisions)
                ? revisions.ToArray()
                : [];
        }
    }
}
