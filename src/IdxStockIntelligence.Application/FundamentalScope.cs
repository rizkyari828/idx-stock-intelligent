using System.Globalization;
using System.Text.Json;

namespace IdxStockIntelligence.Application;

// E4: typed financial scope and identity from §18.4 / §19.1 / §20. Parses the nested
// objects the E1 decoder only shape-checked, and exposes exact comparability instead of
// inferring equivalence from labels.
public enum FundamentalPeriodType { INSTANT, DURATION }
public enum FundamentalPeriodKind { INSTANT, QUARTER, YTD, ANNUAL, TTM, OTHER }

public sealed record FundamentalPeriod(FundamentalPeriodType Type, DateOnly? StartDate, DateOnly EndDate,
    string? FiscalYear, DateOnly? FiscalStart, DateOnly? FiscalEnd, int? FiscalQuarter,
    FundamentalPeriodKind Kind, bool Cumulative);

public sealed record FundamentalUnit(string Kind, string? CurrencyCode);

public sealed record FundamentalShareBasis(string CountBasis, string CalculationBasis, string QuantityBasis,
    EntityKey? ShareClass);

public sealed record FundamentalAccounting(string StatementFamily, string Consolidation, EntityKey? Group,
    string Attribution, string Operations, string AuditStatus, string StatementScope,
    IReadOnlyList<KeyValuePair<string, string>> Dimensions);

public static class FundamentalScope
{
    public const string Malformed = FundamentalNumbers.Malformed;
    public const string Incomparable = FundamentalNumbers.Incomparable;
    public const string Unsupported = FundamentalNumbers.Unsupported;

    public static FundamentalPeriod? ParsePeriod(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var type = Token(element, "periodType") is "INSTANT" ? FundamentalPeriodType.INSTANT : FundamentalPeriodType.DURATION;
        var kind = Token(element, "periodKind") switch
        {
            "INSTANT" => FundamentalPeriodKind.INSTANT,
            "QUARTER" => FundamentalPeriodKind.QUARTER,
            "YTD" => FundamentalPeriodKind.YTD,
            "ANNUAL" => FundamentalPeriodKind.ANNUAL,
            "TTM" => FundamentalPeriodKind.TTM,
            _ => FundamentalPeriodKind.OTHER,
        };
        var start = Date(element, "startDate");
        var end = Date(element, "endDate");
        if (end is null) return null;
        return new FundamentalPeriod(type, start, end.Value, Text(element, "fiscalYear"),
            Date(element, "fiscalStart"), Date(element, "fiscalEnd"), Int(element, "fiscalQuarter"),
            kind, Bool(element, "cumulative") ?? false);
    }

    /// <summary>Exact comparability of two economic periods; unknown never equals known.</summary>
    public static string? Compare(FundamentalPeriod left, FundamentalPeriod right)
    {
        if (left.Type != right.Type) return Incomparable;
        if (left.Type == FundamentalPeriodType.INSTANT)
            return left.EndDate == right.EndDate ? null : Incomparable;
        if (left.Kind != right.Kind) return Incomparable;
        if (left.StartDate != right.StartDate || left.EndDate != right.EndDate) return Incomparable;
        return null;
    }

    /// <summary>Coverage-set rules: no duplicate, overlap or gap inside one fiscal year.</summary>
    public static string? ValidateCoverage(IReadOnlyList<FundamentalPeriod> periods)
    {
        var quarters = periods.Where(p => p.Kind == FundamentalPeriodKind.QUARTER)
            .OrderBy(p => p.StartDate).ToList();
        for (var index = 1; index < quarters.Count; index++)
        {
            if (quarters[index].StartDate == quarters[index - 1].StartDate) return Incomparable;
            if (quarters[index].StartDate <= quarters[index - 1].EndDate) return Incomparable;
        }
        return null;
    }

    /// <summary>ANNUAL must span the evidenced fiscal year exactly; YTD must start there.</summary>
    public static string? ValidateFiscalSpan(FundamentalPeriod period)
    {
        if (period.Kind == FundamentalPeriodKind.ANNUAL)
        {
            if (period.FiscalStart is null || period.FiscalEnd is null) return Incomparable;
            if (period.StartDate != period.FiscalStart || period.EndDate != period.FiscalEnd) return Incomparable;
        }
        if (period.Kind == FundamentalPeriodKind.YTD && period.FiscalStart is not null &&
            period.StartDate != period.FiscalStart) return Incomparable;
        if (period.FiscalQuarter is not null && period.Kind is not (FundamentalPeriodKind.QUARTER or FundamentalPeriodKind.YTD))
            return Incomparable;
        if (period.FiscalStart is not null && period.FiscalEnd is not null && period.FiscalStart > period.FiscalEnd)
            return Incomparable;
        return null;
    }

    public static FundamentalUnit? ParseUnit(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var kind = Token(element, "kind");
        if (kind is null) return null;
        return new FundamentalUnit(kind, Text(element, "currencyCode"));
    }

    public static FundamentalShareBasis? ParseShareBasis(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        return new FundamentalShareBasis(Token(element, "countBasis") ?? "UNKNOWN",
            Token(element, "calculationBasis") ?? "UNKNOWN", Token(element, "quantityBasis") ?? "UNKNOWN",
            EntityKey(element));
    }

    public static FundamentalAccounting? ParseAccounting(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var dimensions = new List<KeyValuePair<string, string>>();
        if (element.TryGetProperty("dimensions", out var items) && items.ValueKind == JsonValueKind.Array)
            foreach (var item in items.EnumerateArray())
            {
                var axis = Token(item, "axis");
                var member = Token(item, "member");
                if (axis is null || member is null) return null;
                dimensions.Add(new(axis, member));
            }
        return new FundamentalAccounting(Token(element, "statementFamily") ?? "UNKNOWN",
            Token(element, "consolidation") ?? "UNKNOWN", EntityKey(element),
            Token(element, "attribution") ?? "UNKNOWN", Token(element, "operations") ?? "UNKNOWN",
            Token(element, "auditStatus") ?? "UNKNOWN", Token(element, "statementScope") ?? "PRIMARY_STATEMENTS",
            dimensions);
    }

    /// <summary>Attribution and consolidation constraints that must agree before comparison.</summary>
    public static string? CompareAccounting(FundamentalAccounting left, FundamentalAccounting right)
    {
        if (left.Consolidation != right.Consolidation) return Incomparable;
        if (left.Consolidation == "CONSOLIDATED" && left.Group != right.Group) return Incomparable;
        if (left.Attribution != right.Attribution) return Incomparable;
        if (left.Operations != right.Operations) return Incomparable;
        if (left.StatementScope != right.StatementScope) return Incomparable;
        if (left.Dimensions.Count != right.Dimensions.Count ||
            left.Dimensions.Zip(right.Dimensions).Any(pair => pair.First.Key != pair.Second.Key || pair.First.Value != pair.Second.Value))
            return Incomparable;
        return null;
    }

    /// <summary>Unknown terms never establish equivalence; they block comparison instead.</summary>
    public static string? RequireKnown(string? value) => string.IsNullOrEmpty(value) || value == "UNKNOWN" ? Incomparable : null;

    private static EntityKey? EntityKey(JsonElement element) =>
        element.TryGetProperty("group", out var group) && group.ValueKind == JsonValueKind.Object &&
        group.TryGetProperty("namespace", out var ns) && group.TryGetProperty("code", out var code) &&
        ns.ValueKind == JsonValueKind.String && code.ValueKind == JsonValueKind.String
            ? new EntityKey(ns.GetString()!, code.GetString()!)
            : null;

    private static string? Token(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Text(JsonElement element, string name) => Token(element, name);

    private static DateOnly? Date(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed : null;

    private static int? Int(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed : null;

    private static bool? Bool(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
}

/// <summary>Exact comparison identity for a statement fact (§19.1 E-keys, scoped).</summary>
public sealed record FundamentalIdentity(EntityKey Subject, string Metric, string DefinitionId,
    FundamentalPeriodType? PeriodType, DateOnly? StartDate, DateOnly? EndDate, FundamentalPeriodKind? PeriodKind,
    string UnitKind, string? CurrencyCode, string Consolidation, string? GroupCode, string Attribution,
    string Operations, string StatementScope, string FactKind)
{
    public static FundamentalIdentity? From(EntityKey subject, string metric, string definitionId,
        FundamentalPeriod? period, FundamentalUnit? unit, FundamentalAccounting? accounting, string factKind)
    {
        if (period is null || unit is null || accounting is null) return null;
        return new FundamentalIdentity(subject, metric, definitionId, period.Type, period.StartDate, period.EndDate,
            period.Kind, unit.Kind, unit.CurrencyCode, accounting.Consolidation, accounting.Group?.Code,
            accounting.Attribution, accounting.Operations, accounting.StatementScope, factKind);
    }

    /// <summary>Equal identities describe one comparable assertion; anything unknown is not equal.</summary>
    public bool ComparableWith(FundamentalIdentity other) =>
        Subject == other.Subject && Metric == other.Metric && DefinitionId == other.DefinitionId &&
        PeriodType == other.PeriodType && StartDate == other.StartDate && EndDate == other.EndDate &&
        PeriodKind == other.PeriodKind && UnitKind == other.UnitKind && CurrencyCode == other.CurrencyCode &&
        Consolidation == other.Consolidation && GroupCode == other.GroupCode &&
        Attribution == other.Attribution && Operations == other.Operations &&
        StatementScope == other.StatementScope && FactKind == other.FactKind;
}
