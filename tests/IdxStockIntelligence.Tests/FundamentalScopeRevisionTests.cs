using System.Text.Json;
using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

// E4/E5 acceptance: typed scope identity, period comparability, revision/PIT selection
// and premise closure. Scenario mapping: T08–T16, T26, T41, T43 (E4) and T17–T22, T29,
// T30, T35, T37–T40 (E5).
public sealed class FundamentalScopeRevisionTests
{
    private static readonly DateTimeOffset Cutoff = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid A = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid B = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid C = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string Series = "source\u001Ffundamental-evidence-v0.1.0/FundamentalFact/key";

    private static JsonElement Period(string type, string? start, string end, string kind, bool cumulative)
    {
        var node = new Dictionary<string, object?>
        {
            ["periodType"] = type,
            ["startDate"] = start,
            ["endDate"] = end,
            ["fiscalYear"] = "2026",
            ["fiscalStart"] = "2026-01-01",
            ["fiscalEnd"] = "2026-12-31",
            ["fiscalQuarter"] = kind == "QUARTER" ? 1 : null,
            ["periodKind"] = kind,
            ["cumulative"] = cumulative,
        };
        return JsonSerializer.SerializeToElement(node);
    }

    private static FundamentalRevisionEntry Entry(Guid id, long revision, long? supersedes,
        FundamentalOperation operation, DateTimeOffset knownAt, string tier = "T1", Guid? premise = null) =>
        new(id, Series, revision, supersedes, operation, knownAt, knownAt.AddMinutes(-5), null, tier, premise);

    [Fact]
    public void T08InstantAndDurationAreNeverComparable()
    {
        var instant = FundamentalScope.ParsePeriod(Period("INSTANT", null, "2026-03-31", "INSTANT", false))!;
        var duration = FundamentalScope.ParsePeriod(Period("DURATION", "2026-01-01", "2026-03-31", "QUARTER", false))!;
        Assert.Equal(FundamentalScope.Incomparable, FundamentalScope.Compare(instant, duration));
        Assert.Equal(FundamentalNumbers.Incomparable, FundamentalScope.ValidateFiscalSpan(
            FundamentalScope.ParsePeriod(Period("DURATION", "2026-01-01", "2026-03-31", "ANNUAL", true))!));
    }

    [Fact]
    public void T09QuarterAndYtdRemainDistinct()
    {
        var quarter = FundamentalScope.ParsePeriod(Period("DURATION", "2026-01-01", "2026-03-31", "QUARTER", false))!;
        var ytd = FundamentalScope.ParsePeriod(Period("DURATION", "2026-01-01", "2026-06-30", "YTD", true))!;
        Assert.Equal(FundamentalScope.Incomparable, FundamentalScope.Compare(quarter, ytd));
        Assert.Null(FundamentalScope.ValidateFiscalSpan(ytd));
    }

    [Fact]
    public void T11CoverageRejectsDuplicatesAndOverlaps()
    {
        var first = FundamentalScope.ParsePeriod(Period("DURATION", "2026-01-01", "2026-03-31", "QUARTER", false))!;
        var duplicate = FundamentalScope.ParsePeriod(Period("DURATION", "2026-01-01", "2026-03-31", "QUARTER", false))!;
        var overlapping = FundamentalScope.ParsePeriod(Period("DURATION", "2026-03-01", "2026-06-30", "QUARTER", false))!;
        Assert.Equal(FundamentalScope.Incomparable, FundamentalScope.ValidateCoverage([first, duplicate]));
        Assert.Equal(FundamentalScope.Incomparable, FundamentalScope.ValidateCoverage([first, overlapping]));
        Assert.Null(FundamentalScope.ValidateCoverage([first]));
    }

    [Fact]
    public void T13ConsolidatedAndStandaloneScopesStaySeparate()
    {
        var consolidated = new FundamentalAccounting("GENERAL_CORPORATE", "CONSOLIDATED",
            new EntityKey("issuer", "ABCD"), "TOTAL", "ALL", "AUDITED", "PRIMARY_STATEMENTS", []);
        var standalone = consolidated with { Consolidation = "STANDALONE", Group = null };
        Assert.Equal(FundamentalScope.Incomparable, FundamentalScope.CompareAccounting(consolidated, standalone));
        Assert.Null(FundamentalScope.CompareAccounting(consolidated, consolidated));
    }

    [Fact]
    public void T14AttributionAndDimensionsMustAgree()
    {
        var total = new FundamentalAccounting("BANK", "CONSOLIDATED", new EntityKey("issuer", "ABCD"),
            "TOTAL", "ALL", "AUDITED", "PRIMARY_STATEMENTS", []);
        var parent = total with { Attribution = "PARENT_OWNERS" };
        var dimensioned = total with { Dimensions = [new("segment", "retail")] };
        Assert.Equal(FundamentalScope.Incomparable, FundamentalScope.CompareAccounting(total, parent));
        Assert.Equal(FundamentalScope.Incomparable, FundamentalScope.CompareAccounting(total, dimensioned));
        Assert.Equal(FundamentalScope.Incomparable, FundamentalScope.RequireKnown("UNKNOWN"));
        Assert.Null(FundamentalScope.RequireKnown("AUDITED"));
    }

    [Fact]
    public void T26IdentityRequiresEveryScopeComponent()
    {
        var period = FundamentalScope.ParsePeriod(Period("DURATION", "2026-01-01", "2026-03-31", "QUARTER", false))!;
        var unit = FundamentalScope.ParseUnit(JsonSerializer.SerializeToElement(
            new Dictionary<string, object?> { ["kind"] = "MONETARY", ["currencyCode"] = "IDR" }))!;
        var accounting = new FundamentalAccounting("GENERAL_CORPORATE", "CONSOLIDATED", new EntityKey("issuer", "ABCD"),
            "TOTAL", "ALL", "AUDITED", "PRIMARY_STATEMENTS", []);
        var identity = FundamentalIdentity.From(new EntityKey("issuer", "ABCD"), "Revenue", "revenue-declared",
            period, unit, accounting, "REPORTED")!;
        var same = identity with { };
        var otherUnit = identity with { UnitKind = "SHARES", CurrencyCode = null };
        Assert.True(identity.ComparableWith(same));
        Assert.False(identity.ComparableWith(otherUnit));
        Assert.Null(FundamentalIdentity.From(new EntityKey("issuer", "ABCD"), "Revenue", "d", null, unit, accounting, "REPORTED"));
    }

    [Fact]
    public void T41UnknownScopeBlocksComparisonInsteadOfGuessing()
    {
        var unknown = new FundamentalAccounting("UNKNOWN", "UNKNOWN", null, "UNKNOWN", "UNKNOWN", "UNKNOWN",
            "PRIMARY_STATEMENTS", []);
        var known = unknown with { Consolidation = "CONSOLIDATED", Group = new EntityKey("issuer", "ABCD") };
        Assert.Equal(FundamentalScope.Incomparable, FundamentalScope.CompareAccounting(unknown, known));
        Assert.Equal(FundamentalScope.Incomparable, FundamentalScope.RequireKnown(unknown.Consolidation));
    }

    [Fact]
    public void T19AndT37SeriesOrdinalsAndCancellationAreEnforced()
    {
        var first = Entry(A, 1, null, FundamentalOperation.ASSERT, Cutoff.AddDays(-3));
        var second = Entry(B, 2, 1, FundamentalOperation.ASSERT, Cutoff.AddDays(-2));
        Assert.Empty(FundamentalRevisions.ValidateSeries([first, second]));

        var gap = Entry(C, 3, 1, FundamentalOperation.ASSERT, Cutoff.AddDays(-1));
        Assert.Contains(FundamentalRevisions.Malformed, FundamentalRevisions.ValidateSeries([first, gap]));

        var cancelAsFirst = Entry(A, 1, null, FundamentalOperation.CANCEL, Cutoff.AddDays(-3), premise: B);
        Assert.Contains(FundamentalRevisions.Malformed, FundamentalRevisions.ValidateSeries([cancelAsFirst]));

        var cancelWithoutLineage = Entry(B, 2, 1, FundamentalOperation.CANCEL, Cutoff.AddDays(-2));
        Assert.Contains(FundamentalRevisions.Malformed, FundamentalRevisions.ValidateSeries([first, cancelWithoutLineage]));
    }

    [Fact]
    public void T17AndT18VisibilityRespectsEveryClock()
    {
        var late = Entry(A, 1, null, FundamentalOperation.ASSERT, Cutoff.AddDays(1));
        Assert.False(FundamentalRevisions.VisibleAt(late, Cutoff));
        Assert.Null(FundamentalRevisions.ResolveAt([late], Cutoff).Governing);

        var onCutoff = Entry(A, 1, null, FundamentalOperation.ASSERT, Cutoff);
        Assert.True(FundamentalRevisions.VisibleAt(onCutoff, Cutoff));
        Assert.Equal(A, FundamentalRevisions.ResolveAt([onCutoff], Cutoff).Governing!.EvidenceId);
    }

    [Fact]
    public void T20VisibleCancelDoesNotFallBackToAnOlderRevision()
    {
        var first = Entry(A, 1, null, FundamentalOperation.ASSERT, Cutoff.AddDays(-3));
        var cancel = Entry(B, 2, 1, FundamentalOperation.CANCEL, Cutoff.AddDays(-2), premise: A);
        Assert.Null(FundamentalRevisions.ResolveAt([first, cancel], Cutoff).Governing);
        Assert.Equal(A, FundamentalRevisions.ResolveAt([first, cancel], Cutoff.AddDays(-3).AddSeconds(1)).Governing!.EvidenceId);
    }

    [Fact]
    public void T21ReinstatementIsVisibleOnlyFromItsOwnKnownAt()
    {
        var first = Entry(A, 1, null, FundamentalOperation.ASSERT, Cutoff.AddDays(-4));
        var cancel = Entry(B, 2, 1, FundamentalOperation.CANCEL, Cutoff.AddDays(-3), premise: A);
        var reinstated = Entry(C, 3, 2, FundamentalOperation.ASSERT, Cutoff.AddDays(-1));
        Assert.Null(FundamentalRevisions.ResolveAt([first, cancel, reinstated], Cutoff.AddDays(-2)).Governing);
        Assert.Equal(C, FundamentalRevisions.ResolveAt([first, cancel, reinstated], Cutoff).Governing!.EvidenceId);
    }

    [Fact]
    public void T22EqualAuthorityConflictIsReportedNotResolvedByRecency()
    {
        var left = Entry(A, 1, null, FundamentalOperation.ASSERT, Cutoff.AddDays(-2));
        var right = new FundamentalRevisionEntry(B, Series, 2, 1, FundamentalOperation.ASSERT,
            Cutoff.AddDays(-2), null, null, "T1", null);
        var result = FundamentalRevisions.ResolveAt([left, right], Cutoff);
        Assert.Null(result.Governing);
        Assert.Equal(FundamentalRevisions.Conflict, result.Diagnostic);
    }

    [Fact]
    public void T29AndT30PremiseClosureIsBoundedAndAcyclic()
    {
        var closure = new[]
        {
            new FundamentalPremise(A, "revenue", []),
            new FundamentalPremise(B, "equity", []),
            new FundamentalPremise(C, "ratio", [A, B]),
        };
        Assert.Empty(FundamentalRevisions.ValidateClosure(closure));

        var missing = new FundamentalPremise(C, "revenue", [Guid.NewGuid()]);
        Assert.Contains(FundamentalRevisions.PremiseInvalid, FundamentalRevisions.ValidateClosure([missing]));

        var cyclic = new[] { new FundamentalPremise(A, "a", [B]), new FundamentalPremise(B, "b", [A]) };
        Assert.Contains(FundamentalRevisions.PremiseInvalid, FundamentalRevisions.ValidateClosure(cyclic));

        var tooMany = Enumerable.Range(0, 17).Select(index => new FundamentalPremise(Guid.NewGuid(),
            "role" + index, [A])).ToList();
        Assert.Contains("FUNDAMENTAL_BOUND_EXCEEDED", FundamentalRevisions.ValidateClosure(tooMany));
    }

    [Fact]
    public void T35CorrectionAfterTheCutoffStaysInvisible()
    {
        var correction = Entry(B, 2, 1, FundamentalOperation.ASSERT, Cutoff.AddDays(1), premise: A);
        var result = FundamentalRevisions.ResolveWithCorrection(A, [correction], Cutoff);
        Assert.Null(result.Governing);
        Assert.Equal(FundamentalRevisions.PremiseInvalid, result.Diagnostic);
    }
}
