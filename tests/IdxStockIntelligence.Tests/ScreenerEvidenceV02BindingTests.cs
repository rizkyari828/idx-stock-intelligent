using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

internal static class EvidenceBindingFixture
{
    internal static readonly Guid Instrument = Guid.Parse("10000000-0000-4000-8000-000000000001");
    internal static readonly Guid Exchange = Guid.Parse("20000000-0000-4000-8000-000000000001");
    internal static readonly Guid Reference = Guid.Parse("30000000-0000-4000-8000-000000000001");
    internal static readonly DateOnly Day = new(2026, 9, 30);
    internal static readonly DateTimeOffset Known = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    internal static EvidenceBar Bar => new(new("100.0"), new("110"), new("90"), new("105"), 100,
        null, "UNKNOWN", "UNKNOWN", "UNKNOWN");
    internal static ConventionValue Convention => new("source-a", "eod", "ohlc", "1", EvidencePriceKind.STOCK_RAW,
        EvidenceContinuity.RAW_AS_TRADED, EvidenceZeroMeaning.GENUINE_NO_EXECUTION,
        EvidenceSyntheticIdentification.DOCUMENTED_NON_SYNTHETIC, "retained-document");
    internal static PriceValue Price => new("regular", Bar, 10, ScreenerEvidenceBinding.BarHash(Bar), Reference, Reference,
        EvidenceMarker.NO, EvidenceMarker.NO, EvidenceMarker.NO, EvidenceZeroVolumeSemantics.NOT_APPLICABLE, EvidenceZeroProof.NONE);
    internal static IEnumerable<(EvidenceClaim Claim, ScreenerEvidenceValue Value)> Values()
    {
        yield return (EvidenceClaim.StableIdentity, new IdentityValue("TEST"));
        yield return (EvidenceClaim.SecurityType, new SecurityTypeValue(EvidenceSecurityType.ORDINARY));
        yield return (EvidenceClaim.Currency, new CurrencyValue("IDR"));
        yield return (EvidenceClaim.ListingCoverage, new ListingValue(Day));
        yield return (EvidenceClaim.Delisting, new DelistingValue(Day));
        yield return (EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.MAIN));
        yield return (EvidenceClaim.BoardChange, new BoardChangeValue("change-1", new(EvidenceBoard.MAIN), new(EvidenceBoard.DEVELOPMENT), Day));
        yield return (EvidenceClaim.ExchangeRuleVersion, new RuleValue("rule-1", "1", EvidenceMechanism.CONTINUOUS));
        yield return (EvidenceClaim.MechanismException, new ExceptionValue("exception-1", "declared-exception", EvidenceMechanism.CALL_AUCTION));
        yield return (EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice-1"));
        yield return (EvidenceClaim.Reopening, new ReopeningValue("reopening-1", EvidenceStatus.TRADING, Day, Reference));
        yield return (EvidenceClaim.ScheduledSession, new ScheduledSessionValue("regular", EvidenceSchedule.OPEN));
        yield return (EvidenceClaim.CompletedSession, new CompletedSessionValue("regular", EvidenceCompletion.COMPLETED, Known.AddHours(-1)));
        yield return (EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.TRADING, "regular"));
        yield return (EvidenceClaim.CorporateAction, new ActionEventValue(EvidenceActionValueKind.EVENT, "event-1", EvidenceActionType.SPLIT,
            Day, new(new("2"), new("1")), null));
        yield return (EvidenceClaim.CorporateAction, new ActionCoverageValue(EvidenceActionValueKind.COVERAGE, "coverage-1", EvidenceCoverage.COMPLETE, [Reference]));
        yield return (EvidenceClaim.SourcePriceConvention, Convention);
        yield return (EvidenceClaim.GenuinePriceObservation, Price);
    }
    internal static EvidenceClass[] Classes(EvidenceClaim claim) => claim switch
    {
        EvidenceClaim.StableIdentity or EvidenceClaim.SecurityType or EvidenceClaim.Currency or EvidenceClaim.ListingCoverage
            or EvidenceClaim.Delisting or EvidenceClaim.BoardRegime or EvidenceClaim.Suspension => [EvidenceClass.ContinuingState, EvidenceClass.PointObservation],
        EvidenceClaim.BoardChange or EvidenceClaim.Reopening or EvidenceClaim.MechanismException => [EvidenceClass.ContinuingState],
        EvidenceClaim.ScheduledSession or EvidenceClaim.CompletedSession or EvidenceClaim.TradingStatus or EvidenceClaim.GenuinePriceObservation => [EvidenceClass.SessionFact],
        EvidenceClaim.ExchangeRuleVersion => [EvidenceClass.VersionedRule],
        EvidenceClaim.SourcePriceConvention => [EvidenceClass.SourceConvention],
        EvidenceClaim.CorporateAction => [EvidenceClass.PointObservation],
        _ => []
    };
    internal static ScreenerScopeKind Scope(EvidenceClaim claim) => claim is EvidenceClaim.CompletedSession or EvidenceClaim.ScheduledSession
        ? ScreenerScopeKind.EXCHANGE : ScreenerScopeKind.INSTRUMENT;
    internal static ScreenerEvidenceRecord Envelope(EvidenceClaim claim, long revision = 1, long? supersedes = null,
        Guid? id = null, string source = "source-a", DateTimeOffset? known = null, ScreenerScopeKind? scope = null,
        DateOnly? through = null, SourceAuthorityTier tier = SourceAuthorityTier.T1Governing)
    {
        var kind = scope ?? Scope(claim);
        return new(id ?? Guid.NewGuid(), kind == ScreenerScopeKind.EXCHANGE ? Exchange : Instrument, claim,
            ScreenerEvidenceV02.PolicyId, 1, ScreenerEvidenceRevisionSeries.Canonical(source, "test-" + claim), revision, supersedes,
            tier, Day, through ?? Day, null, Known.AddHours(-2), Known.AddHours(-1), known ?? Known, source,
            "retained-reference", claim == EvidenceClaim.GenuinePriceObservation ? Reference : null, "{}");
    }
    internal static ScreenerEvidenceRecord Bound(EvidenceClaim claim, ScreenerEvidenceValue value, ScreenerEvidenceRecord? envelope = null) =>
        ScreenerEvidenceBinding.Bind(envelope ?? Envelope(claim), Classes(claim)[0], (envelope?.ScopeKind ?? Scope(claim)), Exchange,
            new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, value));
}

public sealed class ScreenerEvidenceV02BindingTests
{
    public static IEnumerable<object[]> Cases => EvidenceBindingFixture.Values().Select(v => new object[] { v.Claim, v.Value });
    public static IEnumerable<object[]> ClassCases => EvidenceBindingFixture.Values().GroupBy(v => v.Claim).Select(g => g.First())
        .SelectMany(v => Enum.GetValues<EvidenceClass>().Select(cls => new object[] { v.Claim, v.Value, cls }));
    public static IEnumerable<object[]> ScopeCases => EvidenceBindingFixture.Values().GroupBy(v => v.Claim).Select(g => g.First())
        .SelectMany(v => Enum.GetValues<ScreenerScopeKind>().Select(scope => new object[] { v.Claim, v.Value, scope }));

    [Theory, MemberData(nameof(Cases))]
    public void EveryClaimVariantHasStrictDeterministicTypedRoundTrip(EvidenceClaim claim, ScreenerEvidenceValue value)
    {
        var payload = new ScreenerEvidencePayload(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, value);
        var json = ScreenerEvidenceBinding.Encode(claim, payload);
        var decoded = ScreenerEvidenceBinding.Decode(claim, 1, json);
        Assert.Equal(value.GetType(), decoded.Value!.GetType());
        Assert.Equal(json, ScreenerEvidenceBinding.Encode(claim, decoded));
        Assert.Equal(json, ScreenerEvidenceJson.Canonicalize(json));
        Assert.Equal(EvidenceOperation.ASSERT, decoded.Operation);
        Assert.Equal(EvidenceCompleteness.FULL, decoded.Completeness);
        Assert.True(EvidenceBindingFixture.Bound(claim, value).IsBound);
    }

    [Theory, MemberData(nameof(ClassCases))]
    public void FrozenClassMatrixIsEnforced(EvidenceClaim claim, ScreenerEvidenceValue value, EvidenceClass cls)
    {
        var payload = new ScreenerEvidencePayload(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, value);
        if (EvidenceBindingFixture.Classes(claim).Contains(cls))
            Assert.Equal(cls, ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(claim), cls,
                EvidenceBindingFixture.Scope(claim), EvidenceBindingFixture.Exchange, payload).EvidenceClass);
        else Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(claim), cls,
                EvidenceBindingFixture.Scope(claim), EvidenceBindingFixture.Exchange, payload));
    }

    [Theory, MemberData(nameof(ScopeCases))]
    public void FrozenScopeMatrixIsEnforced(EvidenceClaim claim, ScreenerEvidenceValue value, ScreenerScopeKind scope)
    {
        var allowed = scope == ScreenerScopeKind.EXCHANGE
            ? claim is EvidenceClaim.ExchangeRuleVersion or EvidenceClaim.MechanismException or EvidenceClaim.Suspension
                or EvidenceClaim.Reopening or EvidenceClaim.ScheduledSession or EvidenceClaim.CompletedSession
                or EvidenceClaim.SourcePriceConvention or EvidenceClaim.TradingStatus
            : claim is not (EvidenceClaim.ScheduledSession or EvidenceClaim.CompletedSession);
        var payload = new ScreenerEvidencePayload(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, value);
        if (allowed) Assert.Equal(scope, ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(claim, scope: scope),
            EvidenceBindingFixture.Classes(claim)[0], scope, EvidenceBindingFixture.Exchange, payload).ScopeKind);
        else Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(claim, scope: scope),
            EvidenceBindingFixture.Classes(claim)[0], scope, EvidenceBindingFixture.Exchange, payload));
    }

    [Theory]
    [InlineData("{\"symbol\":\"TEST\"}")]
    [InlineData("{\"v\":1}")]
    [InlineData("{\"operation\":\"ASSERT\",\"completeness\":\"FULL\",\"value\":null}")]
    [InlineData("{\"operation\":\"assert\",\"completeness\":\"FULL\",\"value\":{\"sourceCode\":\"TEST\"}}")]
    [InlineData("{\"operation\":0,\"completeness\":\"FULL\",\"value\":{\"sourceCode\":\"TEST\"}}")]
    [InlineData("{\"operation\":\"ASSERT\",\"completeness\":\"FULL\",\"value\":{\"SourceCode\":\"TEST\"}}")]
    [InlineData("{\"operation\":\"ASSERT\",\"completeness\":\"FULL\",\"value\":{\"sourceCode\":null}}")]
    [InlineData("{\"operation\":\"ASSERT\",\"completeness\":\"FULL\",\"value\":{\"sourceCode\":1}}")]
    [InlineData("{\"operation\":\"ASSERT\",\"operation\":\"ASSERT\",\"completeness\":\"FULL\",\"value\":{\"sourceCode\":\"TEST\"}}")]
    [InlineData("{\"operation\":\"ASSERT\",\"completeness\":\"FULL\",\"value\":{\"sourceCode\":\"TEST\",\"sourceCode\":\"TEST\"}}")]
    [InlineData("{\"operation\":\"ASSERT\",\"completeness\":\"FULL\",\"value\":{\"sourceCode\":\"TEST\",\"sourceId\":\"replacement\"}}")]
    [InlineData("{\"operation\":\"ASSERT\",\"completeness\":\"FULL\",\"value\":{\"sourceCode\":\"TEST\"},\"knownAt\":\"2020-01-01T00:00:00Z\"}")]
    [InlineData("{\"operation\":\"CANCEL\",\"completeness\":\"PARTIAL\",\"value\":null}")]
    [InlineData("{\"operation\":\"CANCEL\",\"completeness\":\"FULL\",\"value\":{\"sourceCode\":\"TEST\"}}")]
    public void MalformedAndMechanicalPayloadsCannotBeSemanticEvidence(string json) => Assert.Equal("PERSISTED_EVIDENCE_MALFORMED",
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Decode(EvidenceClaim.StableIdentity, 1, json)).Reason);

    [Theory]
    [InlineData(null, "PERSISTED_EVIDENCE_UNBOUND")]
    [InlineData(2, "PERSISTED_EVIDENCE_BINDING_UNSUPPORTED")]
    [InlineData(0, "PERSISTED_EVIDENCE_BINDING_UNSUPPORTED")]
    public void VersionIsExplicitAndCheckedBeforePayload(int? version, string reason) => Assert.Equal(reason,
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Decode(EvidenceClaim.StableIdentity, version, "not json")).Reason);

    [Fact]
    public void DerivedClaimAndMismatchedTypedPayloadAreRejected()
    {
        Assert.Equal("PERSISTED_EVIDENCE_DERIVED_CLAIM", Assert.Throws<EvidenceBindingException>(() =>
            ScreenerEvidenceBinding.Encode(EvidenceClaim.PriceComparability, new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, new IdentityValue("TEST")))).Reason);
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Encode(EvidenceClaim.Currency,
            new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, new IdentityValue("TEST"))));
    }

    [Fact]
    public void CancelRequiresEarlierExactSeriesTargetAndNullValue()
    {
        var payload = new ScreenerEvidencePayload(EvidenceOperation.CANCEL, EvidenceCompleteness.FULL, null);
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(EvidenceClaim.StableIdentity),
            EvidenceClass.ContinuingState, ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange, payload));
        var bound = ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(EvidenceClaim.StableIdentity, revision: 2, supersedes: 1),
            EvidenceClass.ContinuingState, ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange, payload);
        Assert.Null(ScreenerEvidenceBinding.Decode(bound).Value);
        Assert.Equal(1, bound.SupersedesRevisionNumber);
    }

    [Theory]
    [InlineData(SourceAuthorityTier.T1Governing, true)]
    [InlineData(SourceAuthorityTier.T2AdmittedReference, true)]
    [InlineData(SourceAuthorityTier.T3ProviderObservation, false)]
    [InlineData(SourceAuthorityTier.T4Derivation, false)]
    [InlineData(SourceAuthorityTier.T5Secondary, false)]
    public void TradingStatusRequiresAuthoritativeAdmission(SourceAuthorityTier tier, bool allowed)
    {
        var envelope = EvidenceBindingFixture.Envelope(EvidenceClaim.TradingStatus, tier: tier);
        if (allowed) Assert.True(EvidenceBindingFixture.Bound(EvidenceClaim.TradingStatus, new TradingStatusValue(EvidenceStatus.SUSPENDED, "regular"), envelope).IsBound);
        else Assert.Throws<EvidenceBindingException>(() => EvidenceBindingFixture.Bound(EvidenceClaim.TradingStatus,
            new TradingStatusValue(EvidenceStatus.TRADING, "regular"), envelope));
    }

    [Fact]
    public void ConditionalValuesAndScopeProofAreValidated()
    {
        Assert.Equal("PERSISTED_EVIDENCE_BINDING_UNSUPPORTED", Assert.Throws<EvidenceBindingException>(() =>
            ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(EvidenceClaim.StableIdentity), (EvidenceClass)999,
                ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange,
                new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, new IdentityValue("TEST")))).Reason);
        foreach (var value in new ScreenerEvidenceValue[] { new BoardValue(EvidenceBoard.OTHER), new BoardValue(EvidenceBoard.MAIN, "shadow"),
            new BoardChangeValue("id", null!, new(EvidenceBoard.MAIN), EvidenceBindingFixture.Day) })
            Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Encode(value is BoardChangeValue ? EvidenceClaim.BoardChange : EvidenceClaim.BoardRegime,
                new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, value)));
        Assert.Throws<EvidenceBindingException>(() => EvidenceBindingFixture.Bound(EvidenceClaim.CompletedSession,
            new CompletedSessionValue("regular", EvidenceCompletion.COMPLETED, EvidenceBindingFixture.Known.AddTicks(1))));
        Assert.Throws<EvidenceBindingException>(() => EvidenceBindingFixture.Bound(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.TRADING, "notice")));
        Assert.Throws<EvidenceBindingException>(() => EvidenceBindingFixture.Bound(EvidenceClaim.Reopening,
            new ReopeningValue("id", EvidenceStatus.SUSPENDED, EvidenceBindingFixture.Day, EvidenceBindingFixture.Reference)));
    }

    [Theory]
    [InlineData("1e2")]
    [InlineData("01")]
    [InlineData("NaN")]
    [InlineData("1,000")]
    [InlineData("0.12345678901234567890123456789")]
    [InlineData("79228162514264337593543950336")]
    public void DecimalParsingDoesNotCoerceOrRound(string value) => Assert.Throws<EvidenceBindingException>(() => new EvidenceDecimal(value));

    [Fact]
    public void GenuinePriceRetainsMarkersHashesAndDecimalSpelling()
    {
        var price = EvidenceBindingFixture.Price;
        var zero = price with { Bar = price.Bar with { Volume = 0 }, ZeroVolumeSemantics = EvidenceZeroVolumeSemantics.AMBIGUOUS };
        zero = zero with { BarContentHash = ScreenerEvidenceBinding.BarHash(zero.Bar) };
        Assert.IsType<PriceValue>(ScreenerEvidenceBinding.Decode(EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, zero)).Value);
        Assert.Throws<EvidenceBindingException>(() => EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, zero with { ZeroVolumeSemantics = EvidenceZeroVolumeSemantics.EXPLICITLY_GENUINE }));
        Assert.Throws<EvidenceBindingException>(() => EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, price with { BarContentHash = new string('a', 64) }));
        Assert.Equal(new EvidenceDecimal("100"), new EvidenceDecimal("100.000"));
        Assert.Equal("100.0", price.Bar.Open.Literal);
    }

    [Fact]
    public void FactualEqualityIgnoresProvenanceButPreservesContradictions()
    {
        var a = EvidenceBindingFixture.Bound(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice-a"));
        var b = EvidenceBindingFixture.Bound(EvidenceClaim.Suspension, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice-b"),
            EvidenceBindingFixture.Envelope(EvidenceClaim.Suspension, source: "other-source"));
        Assert.True(ScreenerEvidenceEquality.FactualEquals(a, b));
        var point = ScreenerEvidenceBinding.Bind(EvidenceBindingFixture.Envelope(EvidenceClaim.Suspension), EvidenceClass.PointObservation,
            ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange,
            new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, new SuspensionValue(EvidenceStatus.SUSPENDED, "notice-a")));
        Assert.False(ScreenerEvidenceEquality.FactualEquals(a, point));
        Assert.True(ScreenerEvidenceEquality.FactualEquals(EvidenceBindingFixture.Bound(EvidenceClaim.SourcePriceConvention, EvidenceBindingFixture.Convention),
            EvidenceBindingFixture.Bound(EvidenceClaim.SourcePriceConvention, EvidenceBindingFixture.Convention with { DocumentationReference = "another-reference" })));
        Assert.False(ScreenerEvidenceEquality.FactualEquals(EvidenceBindingFixture.Bound(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.MAIN)),
            EvidenceBindingFixture.Bound(EvidenceClaim.BoardRegime, new BoardValue(EvidenceBoard.DEVELOPMENT))));
        Assert.Equal(1, ScreenerEvidenceBinding.Specificity(ScreenerScopeKind.INSTRUMENT));
        Assert.Equal(0, ScreenerEvidenceBinding.Specificity(ScreenerScopeKind.EXCHANGE));
    }

    [Fact]
    public void CorporateCoverageAndPriceEqualityUseExactTypedPremises()
    {
        var eventA = EvidenceBindingFixture.Bound(EvidenceClaim.CorporateAction,
            new ActionEventValue(EvidenceActionValueKind.EVENT, "split", EvidenceActionType.SPLIT, EvidenceBindingFixture.Day, new(new("2.0"), new("1")), "terms-a"));
        var eventB = EvidenceBindingFixture.Bound(EvidenceClaim.CorporateAction,
            new ActionEventValue(EvidenceActionValueKind.EVENT, "split", EvidenceActionType.SPLIT, EvidenceBindingFixture.Day, new(new("2"), new("1.00")), "terms-b"));
        var coverageA = EvidenceBindingFixture.Bound(EvidenceClaim.CorporateAction,
            new ActionCoverageValue(EvidenceActionValueKind.COVERAGE, "coverage", EvidenceCoverage.COMPLETE, [eventA.EvidenceId]));
        var coverageB = EvidenceBindingFixture.Bound(EvidenceClaim.CorporateAction,
            new ActionCoverageValue(EvidenceActionValueKind.COVERAGE, "coverage", EvidenceCoverage.COMPLETE, [eventB.EvidenceId]));
        var convention = EvidenceBindingFixture.Bound(EvidenceClaim.SourcePriceConvention, EvidenceBindingFixture.Convention);
        var session = EvidenceBindingFixture.Bound(EvidenceClaim.CompletedSession,
            new CompletedSessionValue("regular", EvidenceCompletion.COMPLETED, EvidenceBindingFixture.Known.AddHours(-1)));
        var premises = new[] { eventA, eventB, convention, session }.ToDictionary(r => r.EvidenceId);
        Assert.True(ScreenerEvidenceEquality.FactualEquals(coverageA, coverageB, premises));
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceEquality.FactualEquals(coverageA, coverageB));
        var price = EvidenceBindingFixture.Price with { ConventionEvidenceId = convention.EvidenceId, CompletedSessionEvidenceId = session.EvidenceId };
        var formatted = price with { Bar = price.Bar with { Open = new("100.000") } };
        formatted = formatted with { BarContentHash = ScreenerEvidenceBinding.BarHash(formatted.Bar) };
        Assert.True(ScreenerEvidenceEquality.FactualEquals(EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, price),
            EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, formatted), premises));
        premises[convention.EvidenceId] = eventA;
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceEquality.FactualEquals(EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, price),
            EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, formatted), premises));
        premises[convention.EvidenceId] = ScreenerEvidenceBinding.Bind(
            EvidenceBindingFixture.Envelope(EvidenceClaim.SourcePriceConvention, revision: 2, supersedes: 1, id: convention.EvidenceId),
            EvidenceClass.SourceConvention, ScreenerScopeKind.INSTRUMENT, EvidenceBindingFixture.Exchange,
            new(EvidenceOperation.CANCEL, EvidenceCompleteness.FULL, null));
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceEquality.FactualEquals(EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, price),
            EvidenceBindingFixture.Bound(EvidenceClaim.GenuinePriceObservation, formatted), premises));
    }

    [Fact]
    public void WrongPrimitiveFormatsAndBoundsFailExplicitly()
    {
        var json = ScreenerEvidenceBinding.Encode(EvidenceClaim.GenuinePriceObservation,
            new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, EvidenceBindingFixture.Price));
        foreach (var bad in new[] { json.Replace("\"volume\":100", "\"volume\":\"100\"", StringComparison.Ordinal),
            json.Replace("\"placeholder\":\"NO\"", "\"placeholder\":false", StringComparison.Ordinal),
            json.Replace("\"open\":\"100.0\"", "\"open\":100", StringComparison.Ordinal) })
            Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Decode(EvidenceClaim.GenuinePriceObservation, 1, bad));
        var completed = ScreenerEvidenceBinding.Encode(EvidenceClaim.CompletedSession,
            new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL, new CompletedSessionValue("regular", EvidenceCompletion.COMPLETED, EvidenceBindingFixture.Known)));
        Assert.Throws<EvidenceBindingException>(() => ScreenerEvidenceBinding.Decode(EvidenceClaim.CompletedSession, 1,
            completed.Replace("Z\"", "+00:00\"", StringComparison.Ordinal)));
        Assert.Equal("PERSISTED_EVIDENCE_BOUND_EXCEEDED", Assert.Throws<EvidenceBindingException>(() =>
            ScreenerEvidenceBinding.Decode(EvidenceClaim.StableIdentity, 1, new string('[', 17) + "0" + new string(']', 17))).Reason);
        var tooMany = Enumerable.Range(1, 257).Select(i => Guid.Parse($"30000000-0000-4000-8000-{i:000000000000}")).ToArray();
        Assert.Equal("PERSISTED_EVIDENCE_BOUND_EXCEEDED", Assert.Throws<EvidenceBindingException>(() =>
            ScreenerEvidenceBinding.Encode(EvidenceClaim.CorporateAction, new(EvidenceOperation.ASSERT, EvidenceCompleteness.FULL,
                new ActionCoverageValue(EvidenceActionValueKind.COVERAGE, "coverage", EvidenceCoverage.COMPLETE, tooMany)))).Reason);
    }
}
