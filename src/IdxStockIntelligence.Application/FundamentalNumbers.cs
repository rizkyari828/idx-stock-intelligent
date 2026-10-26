using System.Globalization;
using System.Numerics;

namespace IdxStockIntelligence.Application;

// E2: family applicability and exact power-of-ten normalization from §18.2 (N01) and
// §18.4 (unit/period metric compatibility). Reuses EvidenceDecimal unchanged; adds no
// wider numeric domain, FX guess or rounding.
public enum FundamentalSign { IDENTITY, NEGATE }

public static class FundamentalNumbers
{
    public const string Malformed = "FUNDAMENTAL_MALFORMED";
    public const string Numeric = "FUNDAMENTAL_NUMERIC_UNREPRESENTABLE";
    public const string Incomparable = "FUNDAMENTAL_INCOMPARABLE";
    public const string Unsupported = "FUNDAMENTAL_BINDING_UNSUPPORTED";

    private static readonly BigInteger MaxCoefficient =
        BigInteger.Parse("79228162514264337593543950335", CultureInfo.InvariantCulture);

    /// <summary>Exact sourceLiteral × 10^scalePower × sign; rejects inexact or unrepresentable results.</summary>
    public static bool TryNormalize(EvidenceDecimal source, int scalePower, FundamentalSign sign,
        out EvidenceDecimal normalized, out string diagnostic)
    {
        normalized = default;
        if (scalePower is < -28 or > 28) { diagnostic = Malformed; return false; }

        var bits = decimal.GetBits(source.Value);
        var coefficient = (BigInteger)(uint)bits[0] | (BigInteger)(uint)bits[1] << 32 | (BigInteger)(uint)bits[2] << 64;
        if (bits[3] < 0) coefficient = -coefficient;
        var scale = (bits[3] >> 16) & 255;

        var targetScale = scale - scalePower;
        if (targetScale < 0)
        {
            coefficient *= BigInteger.Pow(10, -targetScale);
            targetScale = 0;
        }
        if (sign == FundamentalSign.NEGATE) coefficient = -coefficient;

        while (targetScale > 28)
        {
            if (coefficient % 10 != 0) { diagnostic = Numeric; return false; }
            coefficient /= 10;
            targetScale--;
        }
        if (BigInteger.Abs(coefficient) > MaxCoefficient) { diagnostic = Numeric; return false; }

        var magnitude = BigInteger.Abs(coefficient).ToString(CultureInfo.InvariantCulture);
        var literal = targetScale == 0
            ? magnitude
            : magnitude.Length <= targetScale
                ? "0." + new string('0', targetScale - magnitude.Length) + magnitude
                : magnitude[..^targetScale] + "." + magnitude[^targetScale..];
        if (coefficient.Sign < 0 && literal.TrimStart('0', '.').Length > 0) literal = "-" + literal;
        if (literal.Length > 200) { diagnostic = Numeric; return false; }
        try { normalized = new EvidenceDecimal(literal); }
        catch (EvidenceBindingException) { diagnostic = Numeric; return false; }
        diagnostic = string.Empty;
        return true;
    }

    /// <summary>EvidenceDecimal's documented domain, checked rather than assumed.</summary>
    public static bool IsWithinDomain(EvidenceDecimal value)
    {
        var bits = decimal.GetBits(value.Value);
        var scale = (bits[3] >> 16) & 255;
        var coefficient = (BigInteger)(uint)bits[0] | (BigInteger)(uint)bits[1] << 32 | (BigInteger)(uint)bits[2] << 64;
        return scale <= 28 && BigInteger.Abs(coefficient) <= MaxCoefficient;
    }
}

/// <summary>Metric ↔ unit ↔ period compatibility from §18.4; unknown tokens never satisfy a rule.</summary>
public static class FundamentalApplicability
{
    private static readonly HashSet<string> DurationOnly = new(StringComparer.Ordinal)
    {
        "Revenue", "OperatingProfit", "NetIncome", "EarningsPerShare", "OperatingCashFlow",
        "CapitalExpenditure", "FreeCashFlow", "CashDividend", "DividendPerShare",
    };

    private static readonly HashSet<string> InstantOnly = new(StringComparer.Ordinal)
    {
        "TotalAssets", "TotalLiabilities", "TotalEquity", "CashAndCashEquivalents",
        "ShortTermDebt", "LongTermDebt", "TotalDebt",
    };

    private static readonly Dictionary<string, string> RequiredUnit = new(StringComparer.Ordinal)
    {
        ["EarningsPerShare"] = "PER_SHARE",
        ["DividendPerShare"] = "PER_SHARE",
        ["SharesOutstanding"] = "SHARES",
    };

    /// <summary>Returns a diagnostic token, or null when the combination is admissible.</summary>
    public static string? Validate(string metric, string unitKind, string periodKind)
    {
        if (RequiredUnit.TryGetValue(metric, out var required))
        {
            if (unitKind != required) return FundamentalNumbers.Incomparable;
        }
        else if (unitKind is not ("MONETARY" or "SHARES"))
        {
            return FundamentalNumbers.Incomparable;
        }

        if (DurationOnly.Contains(metric) && periodKind is not ("QUARTER" or "YTD" or "ANNUAL"))
            return FundamentalNumbers.Incomparable;
        if (InstantOnly.Contains(metric) && periodKind != "INSTANT") return FundamentalNumbers.Incomparable;
        return null;
    }

    /// <summary>Metric expectations outside unit/period: DERIVED-only, REPORTED-only, excluded.</summary>
    public static string? ValidateFactKind(string metric, string factKind) => metric switch
    {
        "FreeCashFlow" when factKind != "DERIVED" => FundamentalNumbers.Unsupported,
        "DividendPerShare" when factKind != "REPORTED" => FundamentalNumbers.Unsupported,
        "DilutionObservation" => FundamentalNumbers.Unsupported,
        _ => null,
    };
}
