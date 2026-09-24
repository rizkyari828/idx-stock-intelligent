using Xunit;
using System.Text;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;

namespace IdxStockIntelligence.Tests;

public sealed class Phase0InvariantTests
{
    private static readonly DateTimeOffset FirstKnownAt = new(2026, 1, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DailyBarRejectsInvalidOhlc()
    {
        var action = () => CreateBar(close: 105m, high: 104m);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void DuplicateIngestionIsIdempotent()
    {
        var store = new DailyBarRevisionStore();
        var bar = CreateBar();
        var runId = Guid.NewGuid();
        var digest = Digest('a');

        var first = store.Ingest(bar, FirstKnownAt, digest, runId);
        var duplicate = store.Ingest(bar, FirstKnownAt.AddMinutes(5), digest, Guid.NewGuid());

        Assert.Equal(IngestionDisposition.Inserted, first.Disposition);
        Assert.Equal(IngestionDisposition.DuplicateIgnored, duplicate.Disposition);
        Assert.Single(store.History(bar.InstrumentId, bar.SessionDate));
        Assert.Equal(first.Revision, duplicate.Revision);
    }

    [Fact]
    public void CorrectionAppendsAndNeverOverwritesEvidence()
    {
        var store = new DailyBarRevisionStore();
        var original = CreateBar(close: 105m);
        var corrected = CreateBar(close: 106m);

        store.Ingest(original, FirstKnownAt, Digest('a'), Guid.NewGuid());
        var result = store.Ingest(corrected, FirstKnownAt.AddDays(1), Digest('b'), Guid.NewGuid());

        var history = store.History(original.InstrumentId, original.SessionDate);
        Assert.Equal(IngestionDisposition.RevisionAppended, result.Disposition);
        Assert.Equal(2, history.Count);
        Assert.Equal(105m, history[0].Bar.Close);
        Assert.Equal(106m, history[1].Bar.Close);
    }

    [Fact]
    public void AsOfCannotSeeFutureRevision()
    {
        var store = new DailyBarRevisionStore();
        var original = CreateBar(close: 105m);
        var corrected = CreateBar(close: 106m);

        store.Ingest(original, FirstKnownAt, Digest('a'), Guid.NewGuid());
        store.Ingest(corrected, FirstKnownAt.AddDays(1), Digest('b'), Guid.NewGuid());

        var beforeAnyKnowledge = store.AsOf(original.InstrumentId, original.SessionDate, FirstKnownAt.AddTicks(-1));
        var beforeCorrection = store.AsOf(original.InstrumentId, original.SessionDate, FirstKnownAt.AddHours(2));
        var afterCorrection = store.AsOf(original.InstrumentId, original.SessionDate, FirstKnownAt.AddDays(2));

        Assert.Null(beforeAnyKnowledge);
        Assert.Equal(105m, beforeCorrection?.Bar.Close);
        Assert.Equal(106m, afterCorrection?.Bar.Close);
    }

    [Fact]
    public void PreIpoAbsenceIsNotAProviderGap()
    {
        var instrument = new Instrument(InstrumentId.New(), "Example Tbk", new DateOnly(2025, 1, 10));
        var session = new MarketSession(new DateOnly(2025, 1, 9), MarketSessionStatus.Trading);

        var status = AvailabilityClassifier.Classify(instrument, session, providerRowPresent: false);

        Assert.Equal(DataAvailabilityStatus.PreListing, status);
    }

    [Fact]
    public void SuspensionDiffersFromMissingProviderRow()
    {
        var instrument = new Instrument(InstrumentId.New(), "Example Tbk", new DateOnly(2020, 1, 1));
        var date = new DateOnly(2026, 1, 2);

        var suspension = AvailabilityClassifier.Classify(
            instrument,
            new MarketSession(date, MarketSessionStatus.Suspension),
            providerRowPresent: false);
        var missing = AvailabilityClassifier.Classify(
            instrument,
            new MarketSession(date, MarketSessionStatus.Trading),
            providerRowPresent: false);

        Assert.Equal(DataAvailabilityStatus.Suspension, suspension);
        Assert.Equal(DataAvailabilityStatus.MissingProviderRow, missing);
        Assert.NotEqual(suspension, missing);
    }

    [Fact]
    public async Task RawArtifactArchiveIsContentAddressedAndIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"idx-stock-tests-{Guid.NewGuid():N}");
        try
        {
            var archiver = new RawArtifactArchiver(root);
            var payload = Encoding.UTF8.GetBytes("fixture-only");

            var first = await archiver.ArchiveAsync(new MemoryStream(payload), ".json", TestContext.Current.CancellationToken);
            var second = await archiver.ArchiveAsync(new MemoryStream(payload), ".json", TestContext.Current.CancellationToken);

            Assert.Equal(first.ContentSha256, second.ContentSha256);
            Assert.Equal(first.RelativePath, second.RelativePath);
            Assert.Equal(payload.Length, first.ByteLength);
            Assert.Single(Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static DailyBar CreateBar(decimal close = 105m, decimal high = 110m)
    {
        var source = new SourceReference(
            "fixture",
            Guid.NewGuid(),
            FirstKnownAt,
            FirstKnownAt,
            Digest('f'));

        return new DailyBar(
            new InstrumentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new DateOnly(2026, 1, 2),
            open: 100m,
            high,
            low: 95m,
            close,
            volume: 1_000,
            source);
    }

    private static string Digest(char value) => new(value, 64);
}
