using System.Security.Cryptography;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;
using Xunit;

namespace IdxStockIntelligence.Tests;

public sealed class OutcomeVerificationTests
{
    private static readonly DateTimeOffset Original = new(2026, 10, 10, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Original.AddDays(1);
    private static DecisionBarLink Link(ScreenerBarEvidence b) => new(b.InstrumentId, b.SessionDate,
        b.RevisionNumber, b.KnownAt, b.ContentHash, b.RawArtifactId);
    private static readonly DecisionReferenceArchive[] Archives =
        [new("screener-reference", true, new('a', 64), 10), new("sessions", true, new('b', 64), 10), new("instrument-sessions", false, null, 0)];

    private static (OutcomeVerificationProjection Stored, DecisionSnapshotHeader Header, DecisionSnapshotRow Row,
        DecisionSnapshotManifest Capture, OutcomeManifest Manifest, OutcomeInputs Inputs) Fixture(string state = "AVAILABLE", decimal end = 110, decimal start = 100)
    {
        var f = OutcomeTrackingTests.Fixture(end, start); var inputs = f.Inputs;
        if (state == "ANCHOR_UNAVAILABLE")
        {
            var old = inputs.CapturedReferences.Instruments[0] with { Prices = [] };
            old = old with { ContentHash = ScreenerReferences.SnapshotHash(old) };
            inputs = inputs with { CapturedReferences = inputs.CapturedReferences with { Instruments = [old] } };
        }
        if (state is "DATA_UNAVAILABLE" or "BASIS_UNCERTAIN")
        {
            var r = inputs.ForwardReferences.Instruments[0];
            if (state == "DATA_UNAVAILABLE")
                r = r with { Trading = [r.Trading[0] with { Status = "SUSPENSION" }] };
            else
            {
                var b = inputs.ForwardDatabase.Bars[0] with { SourceId = "other-source" };
                inputs = inputs with { ForwardDatabase = new([b], []) };
                r = r with { Prices = [r.Prices[0] with { SourceId = "other-source", ContentHashes = [b.ContentHash] }] };
            }
            r = r with { ContentHash = ScreenerReferences.SnapshotHash(r) };
            inputs = inputs with { ForwardReferences = inputs.ForwardReferences with { Instruments = [r] } };
        }
        var c = inputs.CapturedReferences;
        var request = new ScreenerReadRequest([f.Row.InstrumentId], Guid.Parse("76237e96-232f-5085-9b13-dcb7104222bc"),
            f.H.HistoryAnchor, f.H.Through, f.H.KnowledgeCutoff);
        f.H = f.H with { SelectedDigest = ScreenerReferences.SelectedDigest(request, inputs.CapturedDatabase, c, default) };
        var capture = new DecisionSnapshotManifest(1, request, [f.Row.InstrumentId], [], [f.Row.InstrumentId],
            inputs.CapturedDatabase.Bars.Select(Link).ToArray(), [],
            c.Universes.Select(r => new DecisionReferenceLink(r.SnapshotId, r.KnownAt, r.ContentHash)).ToArray(),
            c.Instruments.Select(r => new DecisionReferenceLink(r.SnapshotId, r.KnownAt, r.ContentHash)).ToArray(),
            c.Sessions, c.InstrumentSessions, null, Archives);
        var horizon = OutcomeEvaluator.ResolveHorizon(f.H, 1, inputs.ForwardReferences.Sessions, Original);
        var cell = OutcomeEvaluator.Evaluate(f.H, f.Row, 1, horizon, inputs, Original);
        Assert.Equal(state, cell.State);
        var manifest = new OutcomeManifest(1, OutcomeEvaluator.PolicyId, 1, f.H.PolicyId, f.H.InputHash,
            f.H.SelectedDigest, f.Row.InstrumentId, 1, Original, Link(inputs.CapturedDatabase.Bars[0]),
            Link(inputs.ForwardDatabase.Bars[0]), null,
            new[] { new OutcomeCalendarDay(f.H.TargetSession!.Value, "ANCHOR", c.Sessions[0]) }.Concat(horizon.Calendar).ToArray(),
            inputs.ForwardReferences.Instruments.Select(r => new DecisionReferenceLink(r.SnapshotId, r.KnownAt, r.ContentHash)).ToArray(),
            inputs.ForwardReferences.InstrumentSessions, Archives, cell.Reason);
        var stored = new OutcomeVerificationProjection(f.H.RunId, cell.InstrumentId, 1, OutcomeEvaluator.PolicyId, 1,
            cell.AnchorMarketDate, cell.AnchorClose, cell.HorizonMarketDate, cell.HorizonClose, cell.State, cell.Reason,
            cell.PriceReturnPct, Original, Original.AddSeconds(1));
        return (stored, f.H, f.Row, capture, manifest, inputs);
    }
    private static OutcomeVerificationResult Verify(string state = "AVAILABLE")
    {
        var f = Fixture(state);
        return OutcomeVerification.Verify(f.Stored, f.Header, f.Row, f.Capture, f.Manifest, f.Inputs,
            f.Inputs.ForwardReferences.Sessions, Now, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("AVAILABLE")][InlineData("ANCHOR_UNAVAILABLE")][InlineData("DATA_UNAVAILABLE")][InlineData("BASIS_UNCERTAIN")]
    public void ExactTerminalReplayMatchesIndependentlyOfVerificationTime(string state)
    {
        var result = Verify(state);
        Assert.Equal(OutcomeVerificationState.Match, result.State);
        Assert.Equal(Now, result.VerifiedAt); Assert.True(result.VerifiedAt > result.RecordedAt);
        Assert.Equal(Original, result.OutcomeKnownAt); Assert.Empty(result.Differences);
        Assert.Equal(state, result.ReplayedProjection!.TerminalState);
    }
    [Theory]
    [InlineData(100, 100)][InlineData(90, 100)][InlineData(10, 3)]
    public void ReturnsUseExactDecimalSemantics(int close, int anchor)
    {
        var f = Fixture(end: close, start: anchor);
        var result = OutcomeVerification.Verify(f.Stored, f.Header, f.Row, f.Capture, f.Manifest, f.Inputs,
            f.Inputs.ForwardReferences.Sessions, Now, TestContext.Current.CancellationToken);
        Assert.Equal(OutcomeVerificationState.Match, result.State);
        Assert.Equal(checked(100m * ((decimal)close / anchor - 1m)), result.ReplayedProjection!.PriceReturnPct);
    }
    [Theory]
    [InlineData("unknown", 1)][InlineData("outcome-v0.1.0", 2)]
    public void UnsupportedBindingPrecedesMissingEvidence(string policy, int schema)
    {
        var f = Fixture();
        var result = OutcomeVerification.Verify(f.Stored with { OutcomePolicyId = policy, SchemaVersion = schema },
            f.Header, f.Row, null!, null!, null!, [], Now, TestContext.Current.CancellationToken);
        Assert.Equal(OutcomeVerificationState.PolicyVersionUnavailable, result.State);
        Assert.Null(result.ReplayedProjection);
        Assert.Null(OutcomeVerification.ResolvePolicy(OutcomeEvaluator.PolicyId, 1, "current", 1, "PROSPECTIVE_CAPTURE", "PILOT"));
    }
    [Theory]
    [InlineData("date")][InlineData("anchor")][InlineData("close")][InlineData("return")][InlineData("state")][InlineData("reason")]
    public void IntactEvidenceComparedAgainstAlteredStoredProjection(string field)
    {
        var f = Fixture(); var stored = field switch
        {
            "date" => f.Stored with { HorizonMarketDate = f.Stored.HorizonMarketDate!.Value.AddDays(1) },
            "anchor" => f.Stored with { AnchorClose = 101 },
            "close" => f.Stored with { HorizonClose = 111 },
            "return" => f.Stored with { PriceReturnPct = 11 },
            "state" => f.Stored with { TerminalState = "DATA_UNAVAILABLE" },
            _ => f.Stored with { TerminalReason = "SYNTHETIC_REASON" }
        };
        var result = OutcomeVerification.Verify(stored, f.Header, f.Row, f.Capture, f.Manifest, f.Inputs,
            f.Inputs.ForwardReferences.Sessions, Now, TestContext.Current.CancellationToken);
        Assert.Equal(OutcomeVerificationState.DifferentResult, result.State); Assert.Single(result.Differences);
        Assert.Equal("TYPED_VALUE_DIFFERENT", result.Differences[0].Reason);
        Assert.Equal(result, OutcomeVerification.Verify(stored, f.Header, f.Row, f.Capture, f.Manifest, f.Inputs,
            f.Inputs.ForwardReferences.Sessions, Now, TestContext.Current.CancellationToken) with { Differences = result.Differences });
    }
    [Theory]
    [InlineData("capture")][InlineData("endpoint")][InlineData("session")][InlineData("basis")][InlineData("status")]
    public void RequiredInputLossPrecedesAResultDifference(string missing)
    {
        var f = Fixture(); var inputs = f.Inputs; var proofs = inputs.ForwardReferences.Sessions;
        if (missing == "capture") inputs = inputs with { CapturedDatabase = new([], []) };
        if (missing == "endpoint") inputs = inputs with { ForwardDatabase = new([], []) };
        if (missing == "session") proofs = [];
        if (missing is "basis" or "status") inputs = inputs with { ForwardReferences = inputs.ForwardReferences with { Instruments = [] } };
        var result = OutcomeVerification.Verify(f.Stored with { PriceReturnPct = 999 }, f.Header, f.Row, f.Capture,
            f.Manifest, inputs, proofs, Now, TestContext.Current.CancellationToken);
        Assert.Equal(OutcomeVerificationState.InputNotAvailable, result.State);
        Assert.Null(result.ReplayedProjection); Assert.Empty(result.Differences);
    }
    [Fact]
    public void MalformedChronologyDuplicateLinksAndCalendarFailClosed()
    {
        var f = Fixture();
        foreach (var m in new[] { f.Manifest with { SchemaVersion = 2 }, f.Manifest with { EvaluationCutoff = Now },
            f.Manifest with { Instruments = f.Manifest.Instruments.Concat(f.Manifest.Instruments).ToArray() },
            f.Manifest with { Calendar = f.Manifest.Calendar.Reverse().ToArray() },
            f.Manifest with { CaptureSelectedDigest = new('f', 64) } })
            Assert.Equal(OutcomeVerificationState.InputNotAvailable,
                OutcomeVerification.Verify(f.Stored, f.Header, f.Row, f.Capture, m, f.Inputs, f.Inputs.ForwardReferences.Sessions, Now, TestContext.Current.CancellationToken).State);
    }
    [Fact]
    public void CanonicalSelfHashAndExactRevisionMustAuthenticate()
    {
        var f = Fixture(); var b = f.Inputs.ForwardDatabase.Bars[0];
        foreach (var corrupt in new[] { b with { Close = "111" }, b with { RevisionNumber = 2 }, b with { RawArtifactId = Guid.NewGuid() } })
            Assert.Equal(OutcomeVerificationState.InputNotAvailable, OutcomeVerification.Verify(f.Stored, f.Header, f.Row,
                f.Capture, f.Manifest, f.Inputs with { ForwardDatabase = new([corrupt], []) }, f.Inputs.ForwardReferences.Sessions, Now, TestContext.Current.CancellationToken).State);
    }
    [Fact]
    public void ArchiveAuthenticationUsesExactBytesAndLength()
    {
        byte[] bytes = "retained"u8.ToArray();
        var link = new DecisionReferenceArchive("sessions", true, Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length);
        OutcomeVerification.AuthenticateArchive(link, bytes);
        Assert.Throws<RetainedInputUnavailableException>(() => OutcomeVerification.AuthenticateArchive(link, "Retained"u8));
        Assert.Throws<RetainedInputUnavailableException>(() => OutcomeVerification.AuthenticateArchive(link with { ByteLength = 99 }, bytes));
        Assert.Throws<RetainedInputUnavailableException>(() => OutcomeVerification.ValidateArchives([link, link, link]));
    }
    [Theory]
    [InlineData("{\"policy\":\"latest\"}")][InlineData("[]")][InlineData("null")][InlineData("{\"knownAt\":null}")]
    public void NoCallerEvidenceOrChronologyOverride(string json)
    {
        using var body = JsonDocument.Parse(json);
        Assert.Throws<ScreenerException>(() => OutcomeVerification.ValidateRequest(Guid.NewGuid(), Guid.NewGuid(), 1, body.RootElement));
    }
    [Fact]
    public void CosmeticDecimalScaleAndTimestampOffsetAreNotDifferences()
    {
        var f = Fixture(); var stored = f.Stored with { AnchorClose = 100.00m, OutcomeKnownAt = Original.ToOffset(TimeSpan.FromHours(7)) };
        Assert.Equal(OutcomeVerificationState.Match, OutcomeVerification.Compare(stored, f.Stored, null, Now).State);
    }
    [Theory]
    [InlineData(1)][InlineData(5)][InlineData(10)][InlineData(20)]
    public void ReplayVerifiesEveryExactSessionHorizon(int n)
    {
        var f = Fixture();
        var proofs = Enumerable.Range(1, 35).Select(d => f.Header.TargetSession!.Value.AddDays(d))
            .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(d => new SessionProof(d, ExchangeDayStatus.ObservedTrading,
                "https://reference.example/session", Original.AddMinutes(-1), null)).ToArray();
        var horizon = OutcomeEvaluator.ResolveHorizon(f.Header, n, proofs, Original, TestContext.Current.CancellationToken);
        var b = f.Inputs.ForwardDatabase.Bars[0] with { SessionDate = horizon.Date!.Value };
        b = b with { ContentHash = PilotValidation.ContentHash(new DailyBar(new(b.InstrumentId), b.SessionDate,
            110m, 110m, 110m, 110m, b.Volume, new(b.SourceId, b.RawArtifactId, b.RetrievedAt!.Value,
                b.RetrievedAt.Value, b.RawHash), null, b.VolumeUnit, b.VolumeBasis, b.MarketSegment)) };
        var r = f.Inputs.ForwardReferences.Instruments[0];
        r = r with { Prices = [r.Prices[0] with { ContentHashes = [f.Manifest.Anchor!.ContentHash, b.ContentHash] }] };
        r = r with { ContentHash = ScreenerReferences.SnapshotHash(r) };
        var inputs = f.Inputs with { ForwardDatabase = new([b], []),
            ForwardReferences = f.Inputs.ForwardReferences with { Instruments = [r], Sessions = proofs } };
        var manifest = f.Manifest with { HorizonSessions = n, Endpoint = Link(b),
            Instruments = [new(r.SnapshotId, r.KnownAt, r.ContentHash)],
            Calendar = new[] { f.Manifest.Calendar[0] }.Concat(horizon.Calendar).ToArray() };
        var stored = f.Stored with { HorizonSessions = n, HorizonMarketDate = horizon.Date };
        Assert.Equal(OutcomeVerificationState.Match, OutcomeVerification.Verify(stored, f.Header, f.Row,
            f.Capture, manifest, inputs, proofs, Now, TestContext.Current.CancellationToken).State);
    }
    [Fact]
    public void InvalidStoredReasonCannotExposePrivatePaths()
    {
        var f = Fixture();
        var result = OutcomeVerification.Compare(f.Stored with { TerminalReason = "/private/tmp/private-payload" }, f.Stored, null, Now);
        Assert.Equal("[invalid reason]", Assert.Single(result.Differences).StoredValue);
    }
    [Fact]
    public void AStoragePermittedActionReasonDoesNotCreateFrozenPolicyEvidence()
    {
        var f = Fixture();
        var result = OutcomeVerification.Verify(f.Stored with { TerminalState = "BASIS_UNCERTAIN",
            TerminalReason = "UNIT_CHANGING_EVENT", PriceReturnPct = null }, f.Header, f.Row, f.Capture,
            f.Manifest with { TerminalCondition = "UNIT_CHANGING_EVENT" }, f.Inputs,
            f.Inputs.ForwardReferences.Sessions, Now, TestContext.Current.CancellationToken);
        Assert.Equal(OutcomeVerificationState.DifferentResult, result.State);
        Assert.Equal("AVAILABLE", result.ReplayedProjection!.TerminalState);
    }
}
