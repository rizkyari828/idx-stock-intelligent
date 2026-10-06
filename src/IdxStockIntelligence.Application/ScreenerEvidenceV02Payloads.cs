using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace IdxStockIntelligence.Application;

// Public tokens deliberately match the frozen provider-neutral wire vocabulary.
#pragma warning disable CA1707
public enum ScreenerScopeKind { INSTRUMENT, EXCHANGE }
public enum EvidenceOperation { ASSERT, CANCEL }
public enum EvidenceCompleteness { FULL, PARTIAL }
public enum EvidenceSecurityType { ORDINARY, INDEX, OTHER }
public enum EvidenceBoard { MAIN, DEVELOPMENT, ACCELERATION, NEW_ECONOMY, SPECIAL_MONITORING, OTHER }
public enum EvidenceMechanism { CONTINUOUS, CALL_AUCTION, OTHER }
public enum EvidenceStatus { TRADING, SUSPENDED }
public enum EvidenceSchedule { OPEN, CLOSED }
public enum EvidenceCompletion { COMPLETED, UNPROVED }
public enum EvidenceActionValueKind { EVENT, COVERAGE }
public enum EvidenceActionType { SPLIT, REVERSE_SPLIT, RIGHTS_OR_SHARE_EVENT, BONUS_OR_SHARE_DISTRIBUTION, CONVERSION, MERGER_OR_REORGANIZATION }
public enum EvidenceCoverage { COMPLETE, PARTIAL }
public enum EvidencePriceKind { STOCK_RAW, INDEX_LEVEL, UNPROVED }
public enum EvidenceContinuity { RAW_AS_TRADED, UNPROVED }
public enum EvidenceZeroMeaning { GENUINE_NO_EXECUTION, EXPLICIT_GENUINE_FLAG, UNPROVED }
public enum EvidenceSyntheticIdentification { EXPLICIT_MARKER, DOCUMENTED_NON_SYNTHETIC, UNPROVED }
public enum EvidenceMarker { YES, NO, UNPROVED }
public enum EvidenceZeroVolumeSemantics { NOT_APPLICABLE, EXPLICITLY_GENUINE, AMBIGUOUS }
public enum EvidenceZeroProof { EXPLICIT_SOURCE_FLAG, DOCUMENTED_CONVENTION, NONE }

#pragma warning restore CA1707

public sealed class EvidenceBindingException(string reason) : ArgumentException(reason)
{
    public string Reason { get; } = reason;
}

// Retain decimal spelling for transport/bar hashes; equality is the exact decimal value.
[JsonConverter(typeof(EvidenceDecimalConverter))]
public readonly struct EvidenceDecimal : IEquatable<EvidenceDecimal>
{
    public EvidenceDecimal(string literal)
    {
        if (!Regex.IsMatch(literal, @"\A-?(0|[1-9][0-9]*)(\.[0-9]+)?\z", RegexOptions.CultureInvariant)
            || literal.Length > 200 || !decimal.TryParse(literal, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value)) throw new EvidenceBindingException("PERSISTED_EVIDENCE_MALFORMED");
        var fractional = literal.Contains('.') ? literal.Length - literal.IndexOf('.') - 1 : 0;
        var coefficient = BigInteger.Parse(literal.Replace(".", ""), CultureInfo.InvariantCulture);
        var bits = decimal.GetBits(value);
        var stored = (BigInteger)(uint)bits[0] | (BigInteger)(uint)bits[1] << 32 | (BigInteger)(uint)bits[2] << 64;
        if (bits[3] < 0) stored = -stored;
        if (coefficient * BigInteger.Pow(10, (bits[3] >> 16) & 255) != stored * BigInteger.Pow(10, fractional))
            throw new EvidenceBindingException("PERSISTED_EVIDENCE_MALFORMED");
        Literal = literal;
        Value = value;
    }
    public EvidenceDecimal(decimal value) : this(value.ToString(CultureInfo.InvariantCulture)) { }
    public string Literal { get; }
    public decimal Value { get; }
    public bool Equals(EvidenceDecimal other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is EvidenceDecimal other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(EvidenceDecimal left, EvidenceDecimal right) => left.Equals(right);
    public static bool operator !=(EvidenceDecimal left, EvidenceDecimal right) => !left.Equals(right);
}

public sealed class EvidenceDecimalConverter : JsonConverter<EvidenceDecimal>
{
    public override EvidenceDecimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? new(reader.GetString()!) : throw new JsonException();
    public override void Write(Utf8JsonWriter writer, EvidenceDecimal value, JsonSerializerOptions options)
    {
        if (value.Literal is null) throw new JsonException();
        writer.WriteStringValue(value.Literal);
    }
}

public abstract record ScreenerEvidenceValue;
public sealed record IdentityValue(string SourceCode) : ScreenerEvidenceValue;
public sealed record SecurityTypeValue(EvidenceSecurityType Classification) : ScreenerEvidenceValue;
public sealed record CurrencyValue(string CurrencyCode) : ScreenerEvidenceValue;
public sealed record ListingValue(DateOnly ListingDate) : ScreenerEvidenceValue;
public sealed record DelistingValue(DateOnly DelistingDate) : ScreenerEvidenceValue;
public sealed record BoardValue(EvidenceBoard BoardCode, string? OtherCode = null) : ScreenerEvidenceValue;
public sealed record BoardChangeValue(string ChangeId, BoardValue FromBoard, BoardValue ToBoard, DateOnly ChangeDate) : ScreenerEvidenceValue;
public sealed record RuleValue(string RuleId, string RuleVersion, EvidenceMechanism Mechanism, string? OtherMechanism = null) : ScreenerEvidenceValue;
public sealed record ExceptionValue(string ExceptionId, string ExceptionType, EvidenceMechanism Mechanism, string? OtherMechanism = null) : ScreenerEvidenceValue;
public sealed record SuspensionValue(EvidenceStatus Status, string NoticeId) : ScreenerEvidenceValue;
public sealed record ReopeningValue(string ReopeningId, EvidenceStatus Status, DateOnly ReopeningDate, Guid SuspensionEvidenceId) : ScreenerEvidenceValue;
public sealed record ScheduledSessionValue(string SessionId, EvidenceSchedule Schedule) : ScreenerEvidenceValue;
public sealed record CompletedSessionValue(string SessionId, EvidenceCompletion Completion, DateTimeOffset? CompletedAt) : ScreenerEvidenceValue;
public sealed record TradingStatusValue(EvidenceStatus Status, string SessionId) : ScreenerEvidenceValue;
public sealed record ActionRatio(EvidenceDecimal NewUnits, EvidenceDecimal OldUnits);
public sealed record ActionEventValue(EvidenceActionValueKind Kind, string EventId, EvidenceActionType ActionType,
    DateOnly EffectiveDate, ActionRatio? Ratio, string? TermsReference) : ScreenerEvidenceValue;
public sealed record ActionCoverageValue(EvidenceActionValueKind Kind, string CoverageId, EvidenceCoverage Coverage,
    IReadOnlyList<Guid> EventEvidenceIds) : ScreenerEvidenceValue;
public sealed record ConventionValue(string PriceSourceId, string Endpoint, string Field, string Version,
    EvidencePriceKind PriceKind, EvidenceContinuity Continuity, EvidenceZeroMeaning ZeroVolumeMeaning,
    EvidenceSyntheticIdentification SyntheticIdentification, string DocumentationReference) : ScreenerEvidenceValue;
public sealed record EvidenceBar(EvidenceDecimal Open, EvidenceDecimal High, EvidenceDecimal Low, EvidenceDecimal Close,
    long Volume, EvidenceDecimal? AdjustedClose, string VolumeUnit, string VolumeBasis, string MarketSegment);
public sealed record PriceValue(string SessionId, EvidenceBar Bar, long BarRevision, string BarContentHash,
    Guid ConventionEvidenceId, Guid CompletedSessionEvidenceId, EvidenceMarker SyntheticOrCarryForward,
    EvidenceMarker Placeholder, EvidenceMarker Substituted, EvidenceZeroVolumeSemantics ZeroVolumeSemantics,
    EvidenceZeroProof ZeroVolumeProof) : ScreenerEvidenceValue;
public sealed record ScreenerEvidencePayload(EvidenceOperation Operation, EvidenceCompleteness Completeness, ScreenerEvidenceValue? Value);
