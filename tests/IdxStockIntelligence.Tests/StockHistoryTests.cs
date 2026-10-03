using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class StockHistoryTests
{
    private static ScreenerBarEvidence Bar() => new(Guid.NewGuid(), new(2026, 9, 30), 1,
        new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new('a', 64), Guid.NewGuid(), Guid.NewGuid(),
        "synthetic", new('b', 64), new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero),
        new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero), "https://reference.example/session",
        new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero), "DEGRADED", "100", "101", "99", "100", 0,
        null, "UNKNOWN", "UNKNOWN", "UNKNOWN");

    [Fact]
    public void ValidIndexObservationPreservesActualZeroAndUnknownVolumeBasis()
    {
        var row = StockHistoryRow.From(Bar());
        Assert.Equal(100m, row.Close); Assert.Equal(0L, row.Volume);
        Assert.Equal("AVAILABLE", row.Availability); Assert.Equal("UNKNOWN", row.Evidence.VolumeBasis);
    }

    [Theory]
    [InlineData("REJECTED", "100")]
    [InlineData("DEGRADED", "NaN")]
    public void InvalidSelectedEvidenceRetainsRawProvenanceWithoutSubstitutingPrice(string quality, string close)
    {
        var evidence = Bar() with { CanonicalQuality = quality, Close = close, RevisionNumber = 2 };
        var row = StockHistoryRow.From(evidence);
        Assert.Null(row.Close); Assert.Null(row.Volume); Assert.Equal("UNAVAILABLE", row.Availability);
        Assert.Same(evidence, row.Evidence); Assert.Equal(2, row.Evidence.RevisionNumber);
        Assert.NotNull(row.Reason);
    }
}
