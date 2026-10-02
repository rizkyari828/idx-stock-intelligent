using System.Text;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using IdxStockIntelligence.Infrastructure;
using Npgsql;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class ScreenerEvidenceTests
{
    private static readonly Guid Stock = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid Index = Guid.Parse("10000000-0000-4000-8000-000000000002");
    private static readonly DateOnly Day = new(2026, 9, 15);
    private static readonly DateTimeOffset Known = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly string HashA = new('a', 64);
    private static readonly string HashB = new('b', 64);
    private static readonly string HashC = new('c', 64);
    private static ScreenerReadRequest Request(DateTimeOffset? cutoff = null) => new([Stock], Index, ScreenerReadRequest.Anchor, Day, cutoff ?? Known);
    private static ScreenerSourceEvidence Source() => new("source-1", "synthetic", "https://reference.example/synthetic",
        Known.AddDays(-1), Known.AddMinutes(-1), Known);
    private static UniverseSnapshot Universe(string id = "universe-A", DateTimeOffset? at = null)
    {
        var snapshot = new UniverseSnapshot(id, at ?? Known, "PILOT", [Stock], Index, [Source()], "");
        return snapshot with { ContentHash = ScreenerReferences.SnapshotHash(snapshot) };
    }
    private static InstrumentSnapshot Instrument(Guid? id = null, string snapshotId = "instrument-A", DateTimeOffset? at = null)
    {
        var identity = new ScreenerIdentityInterval(ScreenerReadRequest.Anchor, null, "OLD", "Synthetic", "ORDINARY", "IDR", "MAIN", ["source-1"]);
        var trading = new ScreenerTradingInterval(ScreenerReadRequest.Anchor, ScreenerReadRequest.Horizon, "TRADING", "CONTINUOUS", ["source-1"]);
        var price = new ScreenerPriceInterval(ScreenerReadRequest.Anchor, ScreenerReadRequest.Horizon, "synthetic", "STOCK_RAW", "RAW_AS_TRADED", "CLEARED", [HashA], ["source-1"]);
        var volume = new ScreenerVolumeInterval(ScreenerReadRequest.Anchor, ScreenerReadRequest.Horizon, "synthetic", "SHARES", "RAW_AS_TRADED", true, "IDR", "REGULAR", [HashA], ["source-1"]);
        return Seal(new(snapshotId, id ?? Stock, at ?? Known, [Source()], "", [identity], [trading], [price], [volume]));
    }
    private static InstrumentSnapshot Seal(InstrumentSnapshot snapshot) => snapshot with { ContentHash = ScreenerReferences.SnapshotHash(snapshot) };
    private static ScreenerReferenceDocument Doc(params InstrumentSnapshot[] instruments) => new(1, [Universe()], instruments);
    private static byte[] Bytes(ScreenerReferenceDocument doc) => JsonSerializer.SerializeToUtf8Bytes(doc, ScreenerReferences.JsonOptions);
    private static ScreenerBarEvidence Bar(Guid? id = null) => new(id ?? Stock, Day, 1, Known, HashA,
        Guid.Parse("20000000-0000-4000-8000-000000000001"), Guid.Parse("30000000-0000-4000-8000-000000000001"),
        "synthetic", HashC, Known.AddMinutes(-1), Known.AddMinutes(-1), "https://reference.example/session", Known,
        "DEGRADED", "100", "101", "99", "100", 100, null, "SHARES", "RAW_AS_TRADED", "REGULAR");
    private static SessionProof Proof(ExchangeDayStatus status = ExchangeDayStatus.ObservedTrading, DateTimeOffset? known = null) =>
        new(Day, status, "https://reference.example/session", known ?? Known);

    private static string Digest(ScreenerReadRequest request, ScreenerDatabaseEvidence database, SelectedScreenerReferences references) =>
        ScreenerReferences.SelectedDigest(request, database, references, TestContext.Current.CancellationToken);
    private static EvidenceResult<ScreenerReferenceDocument> Parse(byte[] bytes) =>
        ScreenerReferences.Parse(bytes, TestContext.Current.CancellationToken);
    private static EvidenceResult<ScreenerReferenceBundle> ParseBundle(byte[] reference, byte[] sessions, byte[] statuses) =>
        ScreenerReferenceFiles.Parse(reference, sessions, statuses, TestContext.Current.CancellationToken);

    [Fact]
    public void EmptyOrMissingReferenceIsValidUnavailableEvidence()
    {
        var parsed = ParseBundle([], [], []);
        Assert.True(parsed.Available);
        Assert.Equal("UNIVERSE_NOT_KNOWN", ScreenerReferences.Universe(parsed.Value!.Reference, Known).Reason);
        Assert.Equal("REFERENCE_NOT_KNOWN", ScreenerReferences.Instrument(parsed.Value.Reference, Stock, Known).Reason);
        Assert.Empty(ScreenerReferences.Select(parsed.Value.Reference, [], [], Request(), TestContext.Current.CancellationToken).Instruments);
    }

    [Fact]
    public void UniverseKnowledgeSelectsLatestAndNeverUsesCurrentConfiguration()
    {
        var a = Universe(); var b = Universe("universe-B", Known.AddDays(5));
        var doc = new ScreenerReferenceDocument(1, [b, a], []);
        Assert.Equal(a, ScreenerReferences.Universe(doc, Known).Value);
        Assert.Equal(b, ScreenerReferences.Universe(doc, Known.AddDays(5)).Value);
        Assert.False(ScreenerReferences.Universe(doc, Known.AddTicks(-1)).Available);
        Assert.Equal("UNIVERSE_CONFLICT", ScreenerReferences.Universe(doc with { Universes = [a, Universe("tie")] }, Known).Reason);
    }

    [Fact]
    public void BackEffectiveIdentityDoesNotLeakAndFullSnapshotNeverBorrowsOldCoverage()
    {
        var a = Instrument();
        var b = Seal(Instrument(snapshotId: "instrument-B", at: Known.AddDays(5)) with
        { Identities = [a.Identities[0] with { From = new(2026, 9, 1), Symbol = "NEW" }], Prices = [], Volumes = [], Trading = [] });
        var doc = Doc(b, a);
        var old = ScreenerReferences.Instrument(doc, Stock, Known).Value!;
        Assert.Equal("OLD", ScreenerReferences.Identity(old, Day).Value!.Symbol);
        var later = ScreenerReferences.Instrument(doc, Stock, Known.AddDays(5)).Value!;
        Assert.Equal("NEW", ScreenerReferences.Identity(later, Day).Value!.Symbol);
        Assert.False(ScreenerReferences.Price(later, Bar()).Available);
        Assert.False(ScreenerReferences.Trading(later, Day).Available);
        var unknown = Seal(b with { Identities = [b.Identities[0] with { Classification = "UNKNOWN", Board = "UNKNOWN", Symbol = null }] });
        Assert.Equal("UNKNOWN", ScreenerReferences.Identity(ScreenerReferences.Instrument(Doc(a, unknown), Stock, Known.AddDays(5)).Value!, Day).Value!.Classification);
        Assert.False(ScreenerReferences.Price(unknown, Bar()).Available);
        Assert.False(ScreenerReferences.Instrument(doc, Stock, Known.AddTicks(-1)).Available);
    }

    [Fact]
    public void IdentityIntervalsUseInclusiveEffectiveBoundaries()
    {
        var snapshot = Instrument();
        snapshot = Seal(snapshot with { Identities = [snapshot.Identities[0] with { Through = Day }, snapshot.Identities[0] with { From = Day.AddDays(1), Symbol = "NEW" }] });
        Assert.Equal("OLD", ScreenerReferences.Identity(snapshot, Day).Value!.Symbol);
        Assert.Equal("NEW", ScreenerReferences.Identity(snapshot, Day.AddDays(1)).Value!.Symbol);
        Assert.False(ScreenerReferences.Identity(snapshot, ScreenerReadRequest.Anchor.AddDays(-1)).Available);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("board")]
    [InlineData("mechanism")]
    [InlineData("status")]
    [InlineData("price")]
    [InlineData("volume")]
    public void ContradictoryIntervalsInvalidateSelectedSnapshot(string kind)
    {
        var s = Instrument();
        s = kind switch
        {
            "identity" => s with { Identities = [s.Identities[0], s.Identities[0] with { Symbol = "OTHER" }] },
            "board" => s with { Identities = [s.Identities[0], s.Identities[0] with { Board = "DEVELOPMENT" }] },
            "mechanism" => s with { Trading = [s.Trading[0], s.Trading[0] with { Mechanism = "CALL_AUCTION" }] },
            "status" => s with { Trading = [s.Trading[0], s.Trading[0] with { Status = "SUSPENSION" }] },
            "price" => s with { Prices = [s.Prices[0], s.Prices[0] with { Continuity = "UNKNOWN" }] },
            _ => s with { Volumes = [s.Volumes[0], s.Volumes[0] with { Unit = "LOTS" }] }
        };
        s = Seal(s);
        Assert.True(Parse(Bytes(Doc(s))).Available); // Structural parsing retains conflicting evidence.
        Assert.Equal("REFERENCE_INTERVAL_CONFLICT", ScreenerReferences.Instrument(Doc(s), Stock, Known).Reason);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("mutation")]
    [InlineData("duplicateSnapshot")]
    [InlineData("duplicateMember")]
    [InlineData("benchmarkMember")]
    [InlineData("emptyId")]
    [InlineData("tooManyMembers")]
    [InlineData("badRange")]
    [InlineData("badEnum")]
    [InlineData("unknownSource")]
    [InlineData("retrievalAfterKnowledge")]
    [InlineData("sourceKnownLater")]
    [InlineData("nullArray")]
    public void MalformedEvidenceFailsClosed(string kind)
    {
        var s = Instrument(); var u = Universe(); var doc = Doc(s);
        doc = kind switch
        {
            "hash" => doc with { Instruments = [s with { ContentHash = "xyz" }] },
            "mutation" => doc with { Instruments = [s with { Identities = [s.Identities[0] with { Symbol = "MUTATED" }] }] },
            "duplicateSnapshot" => doc with { Instruments = [s, s] },
            "duplicateMember" => doc with { Universes = [u with { MemberIds = [Stock, Stock] }] },
            "benchmarkMember" => doc with { Universes = [u with { BenchmarkId = Stock }] },
            "emptyId" => doc with { Instruments = [s with { InstrumentId = Guid.Empty }] },
            "tooManyMembers" => doc with { Universes = [u with { MemberIds = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray() }] },
            "badRange" => doc with { Instruments = [Seal(s with { Prices = [s.Prices[0] with { Through = s.Prices[0].From.AddDays(-1) }] })] },
            "badEnum" => doc with { Instruments = [Seal(s with { Identities = [s.Identities[0] with { Classification = "EQUITY" }] })] },
            "unknownSource" => doc with { Instruments = [Seal(s with { Identities = [s.Identities[0] with { EvidenceIds = ["unseen"] }] })] },
            "retrievalAfterKnowledge" => doc with { Instruments = [Seal(s with { Evidence = [Source() with { RetrievedAt = Known.AddDays(1) }] })] },
            "sourceKnownLater" => doc with { Instruments = [Seal(s with { Evidence = [Source() with { KnownAt = Known.AddDays(1) }] })] },
            _ => doc with { Instruments = null! }
        };
        Assert.Equal("REFERENCE_MALFORMED", Parse(Bytes(doc)).Reason);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"universes\":[],\"instruments\":[]}", "REFERENCE_SCHEMA_UNSUPPORTED")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"universes\":[],\"instruments\":[]}", "REFERENCE_MALFORMED")]
    [InlineData("{\"schemaVersion\":1,\"universes\":[],\"instruments\":[],\"extra\":1}", "REFERENCE_MALFORMED")]
    [InlineData("{\"schemaVersion\":1}", "REFERENCE_MALFORMED")]
    public void StrictJsonRejectsUnsupportedOrIncompleteDocuments(string json, string reason) =>
        Assert.Equal(reason, Parse(Encoding.UTF8.GetBytes(json)).Reason);

    [Theory]
    [InlineData("2026-99-15")]
    [InlineData("not-a-uuid")]
    public void MalformedDateAndUuidAreRejected(string bad)
    {
        var json = Encoding.UTF8.GetString(Bytes(Doc(Instrument())));
        json = bad == "not-a-uuid" ? json.Replace(Stock.ToString(), bad, StringComparison.Ordinal) :
            json.Replace("2026-08-24", bad, StringComparison.Ordinal);
        Assert.Equal("REFERENCE_MALFORMED", Parse(Encoding.UTF8.GetBytes(json)).Reason);
    }

    [Fact]
    public void ReferenceByteAndRecordBoundsAreCombinedAndNeverTruncated()
    {
        Assert.Equal("REFERENCE_BOUND_EXCEEDED", ParseBundle(new byte[ScreenerReferences.MaximumBytes], "[]"u8.ToArray(), []).Reason);
        var row = "{\"date\":\"2026-09-15\",\"status\":\"ObservedTrading\",\"known_at\":\"2026-09-30T12:00:00Z\",\"reference\":\"https://reference.example/session\"}";
        var rows = Encoding.UTF8.GetBytes("[" + string.Join(',', Enumerable.Repeat(row, 10001)) + "]");
        Assert.Equal("REFERENCE_BOUND_EXCEEDED", ParseBundle([], rows, []).Reason);
    }

    [Fact]
    public void CanonicalQualityAndPriceVolumeClearanceAreIndependent()
    {
        var bar = Bar(); var snapshot = Instrument();
        Assert.True(bar.Validate().Available); Assert.Equal("DEGRADED", bar.CanonicalQuality);
        Assert.True(ScreenerReferences.Price(snapshot, bar).Available);
        Assert.True(ScreenerReferences.Volume(snapshot, bar).Available);
        Assert.False(ScreenerReferences.Price(snapshot with { Prices = [] }, bar).Available);
        Assert.False(ScreenerReferences.Volume(snapshot with { Volumes = [] }, bar).Available);
        Assert.True(ScreenerReferences.Price(snapshot with { Volumes = [] }, bar).Available);
        Assert.False(ScreenerReferences.Price(snapshot, bar with { ContentHash = HashB }).Available);
        Assert.False(ScreenerReferences.Volume(snapshot, bar with { ContentHash = HashB }).Available);
        Assert.True(ScreenerReferences.Price(Seal(snapshot with { Prices = [snapshot.Prices[0] with { ContentHashes = [HashB] }] }), bar with { ContentHash = HashB }).Available);
        Assert.False(ScreenerReferences.Price(snapshot, bar with { CanonicalQuality = "REJECTED" }).Available);
        Assert.False(ScreenerReferences.Price(snapshot, bar with { Volume = 0 }).Available);
    }

    [Theory]
    [InlineData("event")]
    [InlineData("continuity")]
    [InlineData("source")]
    [InlineData("convention")]
    [InlineData("currency")]
    public void UnresolvedPriceContinuityFailsClosed(string kind)
    {
        var s = Instrument(); var p = s.Prices[0];
        p = kind switch { "event" => p with { EventCoverage = "UNRESOLVED" }, "continuity" => p with { Continuity = "UNKNOWN" },
            "source" => p with { SourceId = "other" }, _ => p with { Convention = "INDEX_LEVEL" } };
        if (kind == "currency") s = s with { Identities = [s.Identities[0] with { Currency = "UNKNOWN" }] };
        Assert.False(ScreenerReferences.Price(Seal(s with { Prices = [p] }), Bar()).Available);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("basis")]
    [InlineData("segment")]
    [InlineData("currency")]
    [InlineData("incompatible")]
    public void UnknownOrIncompatibleVolumeDoesNotCertifyPriceTimesQuantity(string kind)
    {
        var s = Instrument(); var v = s.Volumes[0]; var b = Bar();
        v = kind switch { "unit" => v with { Unit = "UNKNOWN" }, "basis" => v with { Basis = "UNKNOWN" },
            "segment" => v with { MarketSegment = "UNKNOWN" }, "currency" => v with { Currency = "UNKNOWN" },
            _ => v with { RawPriceCompatible = false } };
        b = b with { VolumeUnit = v.Unit, VolumeBasis = v.Basis, MarketSegment = v.MarketSegment };
        s = Seal(s with { Volumes = [v] });
        Assert.False(ScreenerReferences.Volume(s, b).Available);
        Assert.True(ScreenerReferences.Price(s, b).Available);
        Assert.Equal(100, b.Volume); // No guessed unit conversion.
    }

    [Fact]
    public void IndexPricesNeedNoStockStatusBoardOrVolume()
    {
        var s = Instrument(Index);
        s = Seal(s with { Identities = [s.Identities[0] with { Classification = "INDEX", Board = "NOT_APPLICABLE", Currency = "NOT_APPLICABLE" }],
            Prices = [s.Prices[0] with { Convention = "INDEX_LEVEL", EventCoverage = "NOT_APPLICABLE" }], Volumes = [], Trading = [] });
        var b = Bar(Index) with { Volume = 0, VolumeUnit = "UNKNOWN", VolumeBasis = "UNKNOWN", MarketSegment = "UNKNOWN" };
        Assert.True(b.Validate().Available);
        Assert.True(ScreenerReferences.Price(s, b).Available);
        Assert.False(ScreenerReferences.Volume(s, b).Available);
        Assert.Equal("INDEX", ScreenerReferences.Identity(s, Day).Value!.Classification);
    }

    [Theory]
    [InlineData("79228162514264337593543950336", "NUMERIC_OUT_OF_RANGE")]
    [InlineData("100.00000000000000000000000000001", "NUMERIC_OUT_OF_RANGE")]
    [InlineData("0", "CANONICAL_INVALID")]
    [InlineData("NaN", "CANONICAL_INVALID")]
    public void InvalidSelectedNumericStaysVisibleWithTypedReason(string close, string reason)
    {
        var selected = Bar() with { Close = close };
        Assert.Equal(close, selected.Close);
        Assert.Equal(reason, selected.Validate().Reason);
        Assert.Null(selected.Validate().Value);
        Assert.Equal(1, selected.RevisionNumber);
    }

    [Fact]
    public void SourceAndSessionProvenanceCannotBeInvented()
    {
        Assert.Equal("CANONICAL_PROVENANCE_UNAVAILABLE", (Bar() with { RetrievedAt = null }).Validate().Reason);
        Assert.Equal("CANONICAL_PROVENANCE_UNAVAILABLE", (Bar() with { SessionKnownAt = null }).Validate().Reason);
        Assert.Equal("CANONICAL_PROVENANCE_UNAVAILABLE", (Bar() with { FetchedAt = Known.AddDays(1) }).Validate().Reason);
        Assert.Equal("CANONICAL_QUALITY_UNAVAILABLE", (Bar() with { CanonicalQuality = "UNKNOWN" }).Validate().Reason);
    }

    [Fact]
    public void CalendarReusesClosureWeekendUnknownAndDatedReplacementSemantics()
    {
        Assert.Equal(ExchangeDayStatus.AnnouncedClosed, ScreenerSessions.Resolve([Proof(ExchangeDayStatus.AnnouncedClosed)], Day, Day, Known).Status);
        Assert.Equal(ExchangeDayStatus.Unknown, ScreenerSessions.Resolve([], Day, Day, Known).Status);
        Assert.Equal(ExchangeDayStatus.Weekend, ScreenerSessions.Resolve([], new(2026, 9, 19), new(2026, 9, 19), Known).Status);
        Assert.Equal(ExchangeDayStatus.Unknown, ScreenerSessions.Resolve([Proof(known: Known.AddDays(1))], Day, Day, Known).Status);
        Assert.Equal("SESSION_CONFLICT", ScreenerSessions.Resolve([Proof(), Proof(ExchangeDayStatus.AnnouncedClosed)], Day, Day, Known).Reason);
        Assert.Equal(ExchangeDayStatus.AnnouncedClosed, ScreenerSessions.Resolve([Proof(), Proof(ExchangeDayStatus.AnnouncedClosed, Known.AddDays(1))], Day, Day, Known.AddDays(1)).Status);
        Assert.Equal("AFTER_THROUGH", ScreenerSessions.Resolve([Proof()], Day, Day.AddDays(-1), Known).Reason);
    }

    [Fact]
    public void InstrumentSessionConflictsNeverBecomeNoTradeFromBarAbsence()
    {
        var a = new InstrumentSessionProof(new(Stock), Day, MarketSessionStatus.Suspension, "https://reference.example/status", Known);
        var b = a with { Status = MarketSessionStatus.NoTrade };
        Assert.Equal("INSTRUMENT_SESSION_CONFLICT", ScreenerSessions.ResolveInstrument([a, b], Stock, Day, Day, Known).Reason);
        Assert.Equal("STATUS_UNKNOWN", ScreenerSessions.ResolveInstrument([b with { KnownAt = Known.AddDays(1) }], Stock, Day, Day, Known).Reason);
        Assert.Equal(MarketSessionStatus.Suspension, ScreenerSessions.ResolveInstrument([a], Stock, Day, Day, Known).Value!.Status);
    }

    [Fact]
    public void SameDaySessionRequiresExistingCompletionPolicy()
    {
        var day = new DateOnly(2026, 9, 30);
        var proof = Proof() with { Date = day };
        Assert.Equal("COMPLETED_SESSION_EVIDENCE_REQUIRED", ScreenerSessions.Resolve([proof], day, day, Known).Reason);
        Assert.Null(ScreenerSessions.Resolve([proof with { CompletedAt = Known.AddMinutes(-1) }], day, day, Known).Reason);
    }

    [Fact]
    public void SelectedDigestIsStableUnderOrderingOffsetsAndFutureOnlyEvidence()
    {
        var snapshot = Instrument(); var doc = Doc(snapshot);
        var future = Instrument(snapshotId: "future", at: Known.AddDays(5));
        var expanded = doc with { Universes = [Universe("future-universe", Known.AddDays(5)), doc.Universes[0]], Instruments = [future, snapshot] };
        var request = Request(); var bars = new ScreenerDatabaseEvidence([Bar(), Bar(Index)], []);
        var selected = ScreenerReferences.Select(doc, [Proof()], [], request, TestContext.Current.CancellationToken);
        var digest = Digest(request, bars, selected);
        Assert.Equal(digest, Digest(request, bars, selected));
        Assert.Equal(digest, Digest(request, bars, ScreenerReferences.Select(expanded,
            [Proof(known: Known.AddDays(5)), Proof(), Proof() with { Date = Day.AddDays(1) }], [], request, TestContext.Current.CancellationToken)));
        Assert.Equal(digest, Digest(request with { Cutoff = Known.ToOffset(TimeSpan.FromHours(7)) },
            bars with { Bars = bars.Bars.Reverse().ToArray() }, selected));
        Assert.NotEqual(digest, Digest(request, bars with { Bars = [Bar() with { RevisionNumber = 2, ContentHash = HashB }, Bar(Index)] }, selected));
        Assert.DoesNotContain("path", JsonSerializer.Serialize(selected), StringComparison.OrdinalIgnoreCase);
        var reordered = Seal(snapshot with { Evidence = snapshot.Evidence.Reverse().ToArray() });
        Assert.Equal(snapshot.ContentHash, reordered.ContentHash);
    }

    [Fact]
    public void SnapshotHashNormalizesCollectionsAndUtcInstants()
    {
        var s = Instrument();
        s = Seal(s with { Prices = [s.Prices[0] with { ContentHashes = [HashA, HashB] }],
            Evidence = [Source(), Source() with { Id = "source-2" }] });
        var reversed = s with { Prices = [s.Prices[0] with { ContentHashes = [HashB, HashA] }],
            Evidence = s.Evidence.Reverse().Select(e => e with { KnownAt = e.KnownAt.ToOffset(TimeSpan.FromHours(7)) }).ToArray(),
            KnownAt = s.KnownAt.ToOffset(TimeSpan.FromHours(7)) };
        Assert.Equal(s.ContentHash, ScreenerReferences.SnapshotHash(reversed));
        Assert.True(Parse(Bytes(Doc(reversed))).Available);
    }

    [Theory]
    [InlineData("2026-08-23")]
    [InlineData("2027-08-25")]
    public async Task UnsupportedDatesFailBeforeDatabaseAccess(string date)
    {
        var request = Request() with { Through = DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture) };
        var result = await ScreenerEvidenceDatabase.ReadAsync(null!, null, request, TestContext.Current.CancellationToken);
        Assert.Equal("HISTORY_OUT_OF_SCOPE", result.Reason);
    }

    [Fact]
    public async Task AnchorIdBoundsAndCancellationAreEnforcedBeforeAccess()
    {
        Assert.Equal("HISTORY_OUT_OF_SCOPE", (Request() with { HistoryAnchor = Day }).BoundsReason());
        Assert.Null((Request() with { Through = ScreenerReadRequest.Horizon }).BoundsReason());
        Assert.Equal("INSTRUMENT_BOUND_EXCEEDED", (Request() with { InstrumentIds = Enumerable.Range(0, 211).Select(_ => Guid.NewGuid()).ToArray() }).BoundsReason());
        Assert.Equal("INSTRUMENT_BOUND_EXCEEDED", (Request() with { InstrumentIds = [Guid.Empty] }).BoundsReason());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ScreenerEvidenceDatabase.ReadAsync(null!, null, Request(), cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ScreenerReferenceFiles.LoadAsync(cancel.Token));
        Assert.Throws<OperationCanceledException>(() => ScreenerReferences.Parse(Bytes(Doc()), cancel.Token));
        Assert.Throws<OperationCanceledException>(() => ScreenerReferences.SelectedDigest(Request(), new([], []), new([], [], [], []), cancel.Token));
    }
}
