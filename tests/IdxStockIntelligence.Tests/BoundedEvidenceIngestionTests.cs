using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

// Disposable synthetic adapter only: not an authoritative market source or installed operational feed.
internal static class IngestionFixture
{
    internal static readonly Guid Instrument = Guid.Parse("10000000-0000-4000-8000-000000000001");
    internal static readonly Guid Exchange = Guid.Parse("20000000-0000-4000-8000-000000000001");
    internal static readonly DateOnly Day = new(2026, 9, 15);
    internal static readonly DateTimeOffset Earlier = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    internal static UniverseSnapshot Universe()
    {
        var u = new UniverseSnapshot("owned-fixture", Earlier, "PILOT", [Instrument], Guid.Parse("30000000-0000-4000-8000-000000000001"),
            [new("fixture", "owned-test", "https://synthetic.example/universe", null, Earlier, Earlier)], "");
        return u with { ContentHash = ScreenerReferences.SnapshotHash(u) };
    }
    internal static byte[] Bytes(string currency = "IDR", int revision = 1) => Encoding.UTF8.GetBytes($$"""{"currency":"{{currency}}","revision":{{revision}}}""");
    internal static BoundedEvidenceArtifact Artifact(byte[] bytes) => new("owned-test", "https://synthetic.example/currency", "owned-test-1",
        "application/json", DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddMilliseconds(-500), null, BoundedEvidenceIngestion.ContentHash(bytes));
    internal static ScreenerEvidenceRecord Row(BoundedEvidenceArtifact a, Guid raw, EvidenceClaim claim = EvidenceClaim.Currency,
        ScreenerEvidenceValue? value = null, Guid? subject = null, SourceAuthorityTier authority = SourceAuthorityTier.T2AdmittedReference,
        long revision = 1, string reference = "currency", EvidenceClass cls = EvidenceClass.PointObservation, ScreenerScopeKind scope = ScreenerScopeKind.INSTRUMENT)
    {
        var payload = ScreenerEvidenceBinding.Encode(claim, new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, value ?? new CurrencyValue("IDR")));
        ScreenerEvidenceRecord Create(Guid id) => new(id, subject ?? Instrument, claim, ScreenerEvidenceV02.PolicyId, 1,
            ScreenerEvidenceRevisionSeries.Canonical(a.SourceId, reference), revision, revision == 1 ? null : revision - 1, authority,
            Day, Day, null, a.PublishedAt, a.ReceivedAt, a.KnownAt, a.SourceId, reference, raw, payload, cls, 1, scope, Exchange);
        var prototype = Create(Instrument); return Create(BoundedEvidenceIngestion.EvidenceIdentity(raw, prototype));
    }
    internal static BoundedEvidenceSource Source() => new("owned-test", "owned-test-1", "application/json",
        [new(EvidenceClaim.Currency, EvidenceClass.PointObservation, SourceAuthorityTier.T2AdmittedReference, ScreenerScopeKind.INSTRUMENT)],
        (bytes, artifact, raw, ct) =>
        {
            ct.ThrowIfCancellationRequested(); using var doc = JsonDocument.Parse(bytes);
            return [Row(artifact, raw, value: new CurrencyValue(doc.RootElement.GetProperty("currency").GetString()!), revision: doc.RootElement.GetProperty("revision").GetInt32())];
        });
}

public sealed class BoundedEvidenceIngestionTests
{
    [Fact]
    public void ContentAndEvidenceIdentitiesAreDeterministicAndParserNamespaced()
    {
        var b = IngestionFixture.Bytes(); var a = IngestionFixture.Artifact(b); var u = IngestionFixture.Universe(); var now = DateTimeOffset.UtcNow;
        var first = BoundedEvidenceIngestion.Validate(u, IngestionFixture.Exchange, a, b, now);
        Assert.Equal(first, BoundedEvidenceIngestion.Validate(u, IngestionFixture.Exchange, a, b, now));
        Assert.Equal(IngestionFixture.Row(a, first).EvidenceId, IngestionFixture.Row(a, first).EvidenceId);
        Assert.NotEqual(first, BoundedEvidenceIngestion.Validate(u, IngestionFixture.Exchange, a with { ParserVersion = "owned-test-2" }, b, now));
        Assert.Single(BoundedEvidenceIngestion.ValidateRows(u, IngestionFixture.Exchange, IngestionFixture.Source(), a, first, [IngestionFixture.Row(a, first)]));
    }
    [Theory]
    [InlineData("hash")] [InlineData("future")] [InlineData("backdated")] [InlineData("size")] [InlineData("empty")]
    [InlineData("universe")] [InlineData("provenance")] [InlineData("precision")]
    public void InvalidArtifactOrUniverseFailsClosed(string defect)
    {
        var b = IngestionFixture.Bytes(); var a = IngestionFixture.Artifact(b); var u = IngestionFixture.Universe();
        if (defect == "hash") a = a with { ContentSha256 = new string('b', 64) };
        if (defect == "future") a = a with { KnownAt = DateTimeOffset.UtcNow.AddYears(1) };
        if (defect == "backdated") a = a with { KnownAt = a.ReceivedAt.AddDays(-1) };
        if (defect == "size") b = new byte[BoundedEvidenceIngestion.MaximumArtifactBytes + 1];
        if (defect == "empty") b = [];
        if (defect == "universe") u = u with { UniverseId = "FullIdx" };
        if (defect == "provenance") a = a with { SourceReference = " " };
        if (defect == "precision") a = a with { ReceivedAt = a.ReceivedAt.AddTicks(1) };
        Assert.ThrowsAny<ArgumentException>(() => BoundedEvidenceIngestion.Validate(u, IngestionFixture.Exchange, a, b, DateTimeOffset.UtcNow));
    }
    [Theory]
    [InlineData("outside")] [InlineData("authority")] [InlineData("unbound")] [InlineData("chronology")]
    [InlineData("duplicate")] [InlineData("overflow")] [InlineData("empty-output")]
    public void NormalizedRowsCannotBypassUniverseAdmissionOrBounds(string defect)
    {
        var b = IngestionFixture.Bytes(); var a = IngestionFixture.Artifact(b); var raw = BoundedEvidenceIngestion.Validate(IngestionFixture.Universe(), IngestionFixture.Exchange, a, b, DateTimeOffset.UtcNow);
        var row = IngestionFixture.Row(a, raw, subject: defect == "outside" ? Guid.NewGuid() : null,
            authority: defect == "authority" ? SourceAuthorityTier.T3ProviderObservation : SourceAuthorityTier.T2AdmittedReference);
        if (defect == "chronology") row = IngestionFixture.Row(a with { ReceivedAt = a.ReceivedAt.AddDays(-1), KnownAt = a.KnownAt.AddDays(-1) }, raw);
        if (defect == "unbound") row = new(row.EvidenceId, row.SubjectId, row.Claim, row.PolicyId, row.SchemaVersion, row.RevisionSeriesId, row.RevisionNumber,
            row.SupersedesRevisionNumber, row.AuthorityTier, row.EffectiveFrom, row.EffectiveTo, row.EffectiveAt, row.PublishedAt, row.RetrievedAt, row.KnownAt,
            row.SourceId, row.SourceReference, raw, row.Payload);
        var rows = defect == "duplicate" ? new[] { row, row } : defect == "overflow" ? Enumerable.Repeat(row, 513).ToArray()
            : defect == "empty-output" ? [] : [row];
        Assert.ThrowsAny<ArgumentException>(() => BoundedEvidenceIngestion.ValidateRows(IngestionFixture.Universe(), IngestionFixture.Exchange, IngestionFixture.Source(), a, raw, rows));
    }
    [Fact]
    public void CorrectionRequiresSameCanonicalFactAndOriginalChronology()
    {
        var a = IngestionFixture.Artifact(IngestionFixture.Bytes()); var old = IngestionFixture.Row(a, Guid.NewGuid()) with { RecordedAt = a.KnownAt };
        var next = IngestionFixture.Row(a with { KnownAt = a.KnownAt.AddMinutes(1) }, Guid.NewGuid(), value: new CurrencyValue("USD"), revision: 2);
        BoundedEvidenceIngestion.ValidateCorrection(next, old, new Dictionary<Guid, ScreenerEvidenceRecord>());
        Assert.Throws<EvidenceBindingException>(() => BoundedEvidenceIngestion.ValidateCorrection(next,
            IngestionFixture.Row(a, Guid.NewGuid(), reference: "unrelated") with { RecordedAt = a.KnownAt }, new Dictionary<Guid, ScreenerEvidenceRecord>()));
    }
    [Fact]
    public void SourceBindingCannotProduceDerivedComparabilityOrBarInferredStatus()
    {
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Decode(EvidenceClaim.PriceComparability, 1, "{}"));
        var source = IngestionFixture.Source(); Assert.DoesNotContain(source.Mappings, m => m.Claim == EvidenceClaim.TradingStatus);
        var a = IngestionFixture.Artifact(IngestionFixture.Bytes());
        var row = IngestionFixture.Row(a, Guid.NewGuid(), EvidenceClaim.ScheduledSession, new ScheduledSessionValue("IDX-EOD", EvidenceSchedule.CLOSED),
            IngestionFixture.Exchange, reference: "closure", cls: EvidenceClass.SessionFact, scope: ScreenerScopeKind.EXCHANGE);
        Assert.Equal(EvidenceSchedule.CLOSED, ((ScheduledSessionValue)ScreenerEvidenceBinding.Decode(row).Value!).Schedule);
        Assert.NotEqual(EvidenceClaim.CompletedSession, row.Claim);
    }
    [Fact]
    public void PriceNumbersRemainExactAndUnknownMarkersNeverBecomeStatusOrZeroProof()
    {
        var a = IngestionFixture.Artifact(IngestionFixture.Bytes()); var raw = Guid.NewGuid();
        var bar = new EvidenceBar(new("100.25"), new("101.75"), new("99.50"), new("100.00"), 0, null, "UNKNOWN", "UNKNOWN", "UNKNOWN");
        var value = new PriceValue("IDX-EOD", bar, 1, ScreenerEvidenceBinding.BarHash(bar), Guid.NewGuid(), Guid.NewGuid(),
            EvidenceMarker.UNPROVED, EvidenceMarker.UNPROVED, EvidenceMarker.UNPROVED, EvidenceZeroVolumeSemantics.AMBIGUOUS, EvidenceZeroProof.NONE);
        var row = IngestionFixture.Row(a, raw, EvidenceClaim.GenuinePriceObservation, value, authority: SourceAuthorityTier.T3ProviderObservation, cls: EvidenceClass.SessionFact);
        var decoded = (PriceValue)ScreenerEvidenceBinding.Decode(row).Value!;
        Assert.Equal(100.00m, decoded.Bar.Close.Value); Assert.Equal(0, decoded.Bar.Volume); Assert.Equal("UNKNOWN", decoded.Bar.VolumeUnit);
        Assert.Equal(EvidenceMarker.UNPROVED, decoded.SyntheticOrCarryForward); Assert.Equal(EvidenceZeroProof.NONE, decoded.ZeroVolumeProof);
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Decode(EvidenceClaim.GenuinePriceObservation, 1, row.Payload.Replace("\"volume\":0,", "", StringComparison.Ordinal)));
        Assert.Equal(EvidenceClaim.GenuinePriceObservation, row.Claim);
    }
}
