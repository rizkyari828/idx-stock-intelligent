using IdxStockIntelligence.Application;
using Xunit;

namespace IdxStockIntelligence.Tests;

// E2 acceptance: exact power-of-ten normalization and metric/unit/period applicability.
// Scenario mapping: T03–T07 (number/missingness), T28 (scale), T42 (numeric bounds).
public sealed class FundamentalNumbersTests
{
    private static EvidenceDecimal Number(string literal) => new(literal);

    [Theory]
    [InlineData("1000", 0, "1000")]
    [InlineData("1000", 3, "1000000")]
    [InlineData("1000", -3, "1")]
    [InlineData("1.5", 2, "150")]
    [InlineData("1.5", -2, "0.015")]
    [InlineData("0.0000000000000000000000000001", 0, "0.0000000000000000000000000001")]
    public void T28PowerOfTenScalingIsExact(string source, int power, string expected)
    {
        Assert.True(FundamentalNumbers.TryNormalize(Number(source), power, FundamentalSign.IDENTITY, out var value, out var diagnostic), diagnostic);
        Assert.Equal(Number(expected).Value, value.Value);
    }

    [Fact]
    public void T42MaximumCoefficientIsAcceptedAndBeyondIsRejected()
    {
        Assert.True(FundamentalNumbers.TryNormalize(new EvidenceDecimal("79228162514264337593543950335"), 0,
            FundamentalSign.IDENTITY, out var max, out _));
        Assert.Equal(decimal.MaxValue, max.Value);

        var beyond = new EvidenceDecimal("79228162514264337593543950335");
        Assert.False(FundamentalNumbers.TryNormalize(beyond, 1, FundamentalSign.IDENTITY, out _, out var diagnostic));
        Assert.Equal(FundamentalNumbers.Numeric, diagnostic);
    }

    [Fact]
    public void T42NonZeroBeyondTwentyEightPlacesIsRejected()
    {
        Assert.False(FundamentalNumbers.TryNormalize(Number("0.0000000000000000000000000001"), -1,
            FundamentalSign.IDENTITY, out _, out var diagnostic));
        Assert.Equal(FundamentalNumbers.Numeric, diagnostic);

        // Trailing zeros beyond 28 places reduce exactly and remain admissible.
        Assert.True(FundamentalNumbers.TryNormalize(Number("2.5000000000000000000000000000"), -1,
            FundamentalSign.IDENTITY, out var reduced, out _));
        Assert.Equal(0.25m, reduced.Value);
    }

    [Fact]
    public void T03ZeroNegativeAndSignTransformAreRetainedExactly()
    {
        Assert.True(FundamentalNumbers.TryNormalize(Number("-0.00"), 0, FundamentalSign.IDENTITY, out var negativeZero, out _));
        Assert.Equal(0m, negativeZero.Value);

        Assert.True(FundamentalNumbers.TryNormalize(Number("12.5"), 0, FundamentalSign.NEGATE, out var negated, out _));
        Assert.Equal(-12.5m, negated.Value);

        Assert.True(FundamentalNumbers.TryNormalize(Number("-12.5"), 0, FundamentalSign.NEGATE, out var doubleNegated, out _));
        Assert.Equal(12.5m, doubleNegated.Value);
    }

    [Fact]
    public void T05ScaleOutsideTheDeclaredRangeIsMalformed()
    {
        foreach (var power in new[] { 29, -29, 100, -100 })
        {
            Assert.False(FundamentalNumbers.TryNormalize(Number("1"), power, FundamentalSign.IDENTITY, out _, out var diagnostic));
            Assert.Equal(FundamentalNumbers.Malformed, diagnostic);
        }
    }

    [Fact]
    public void T06ApplicabilityRejectsIncompatibleUnitAndMetric()
    {
        Assert.Null(FundamentalApplicability.Validate("Revenue", "MONETARY", "QUARTER"));
        Assert.Equal(FundamentalNumbers.Incomparable, FundamentalApplicability.Validate("Revenue", "PER_SHARE", "QUARTER"));
        Assert.Equal(FundamentalNumbers.Incomparable, FundamentalApplicability.Validate("EarningsPerShare", "MONETARY", "QUARTER"));
        Assert.Null(FundamentalApplicability.Validate("EarningsPerShare", "PER_SHARE", "ANNUAL"));
        Assert.Equal(FundamentalNumbers.Incomparable, FundamentalApplicability.Validate("SharesOutstanding", "MONETARY", "INSTANT"));
        Assert.Null(FundamentalApplicability.Validate("SharesOutstanding", "SHARES", "INSTANT"));
    }

    [Fact]
    public void T07PeriodKindMustMatchTheMetricExpectation()
    {
        Assert.Equal(FundamentalNumbers.Incomparable, FundamentalApplicability.Validate("NetIncome", "MONETARY", "INSTANT"));
        Assert.Null(FundamentalApplicability.Validate("NetIncome", "MONETARY", "YTD"));
        Assert.Equal(FundamentalNumbers.Incomparable, FundamentalApplicability.Validate("TotalEquity", "MONETARY", "QUARTER"));
        Assert.Null(FundamentalApplicability.Validate("TotalEquity", "MONETARY", "INSTANT"));
        Assert.Equal(FundamentalNumbers.Incomparable, FundamentalApplicability.Validate("CashDividend", "MONETARY", "OTHER"));
    }

    [Theory]
    [InlineData("FreeCashFlow", "REPORTED", FundamentalNumbers.Unsupported)]
    [InlineData("FreeCashFlow", "DERIVED", null)]
    [InlineData("DividendPerShare", "DERIVED", FundamentalNumbers.Unsupported)]
    [InlineData("DividendPerShare", "REPORTED", null)]
    [InlineData("DilutionObservation", "REPORTED", FundamentalNumbers.Unsupported)]
    [InlineData("Revenue", "REPORTED", null)]
    public void T43FactKindGuardsStaySeparationOfConcerns(string metric, string kind, string? expected)
    {
        Assert.Equal(expected, FundamentalApplicability.ValidateFactKind(metric, kind));
    }

    [Fact]
    public void T42DomainCheckAgreesWithEvidenceDecimal()
    {
        Assert.True(FundamentalNumbers.IsWithinDomain(new EvidenceDecimal("79228162514264337593543950335")));
        Assert.True(FundamentalNumbers.IsWithinDomain(new EvidenceDecimal("-0.0000000000000000000000000001")));
        Assert.True(FundamentalNumbers.IsWithinDomain(new EvidenceDecimal("0")));
    }
}
