using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace IdxStockIntelligence.Application;

public static class ScreenerEvidenceBinding
{
    public const int PayloadVersion = 1;
    public const int MaximumBytes = 64 * 1024;
    private const string Malformed = "PERSISTED_EVIDENCE_MALFORMED";
    private const string Unsupported = "PERSISTED_EVIDENCE_BINDING_UNSUPPORTED";
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false, RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16,
            RespectNullableAnnotations = true, NumberHandling = JsonNumberHandling.Strict
        };
        options.Converters.Add(new StrictEnumConverterFactory());
        options.Converters.Add(new StrictGuidConverter());
        options.Converters.Add(new StrictDateConverter());
        options.Converters.Add(new StrictInstantConverter());
        return options;
    }

    public static string ClassToken(EvidenceClass value) => value switch
    {
        EvidenceClass.ContinuingState => "CONTINUING_EFFECTIVE_STATE",
        EvidenceClass.PointObservation => "POINT_OBSERVATION",
        EvidenceClass.VersionedRule => "VERSIONED_RULE",
        EvidenceClass.SessionFact => "SESSION_FACT",
        EvidenceClass.SourceConvention => "SOURCE_CONVENTION",
        _ => throw new EvidenceBindingException(Unsupported)
    };
    public static EvidenceClass ParseClass(string token) => token switch
    {
        "CONTINUING_EFFECTIVE_STATE" => EvidenceClass.ContinuingState,
        "POINT_OBSERVATION" => EvidenceClass.PointObservation,
        "VERSIONED_RULE" => EvidenceClass.VersionedRule,
        "SESSION_FACT" => EvidenceClass.SessionFact,
        "SOURCE_CONVENTION" => EvidenceClass.SourceConvention,
        _ => throw new EvidenceBindingException(Unsupported)
    };
    public static int Specificity(ScreenerScopeKind scope) => scope switch
    {
        ScreenerScopeKind.INSTRUMENT => 1, ScreenerScopeKind.EXCHANGE => 0,
        _ => throw new EvidenceBindingException(Malformed)
    };

    public static string BarHash(EvidenceBar bar) => ScreenerEvidenceJson.Sha256(
        ScreenerEvidenceJson.Canonicalize(JsonSerializer.Serialize(bar, Options)));

    public static string Encode(EvidenceClaim claim, ScreenerEvidencePayload payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { payload.Operation, payload.Completeness, Value = (object?)payload.Value }, Options);
            var decoded = Decode(claim, PayloadVersion, json);
            if (payload.Value?.GetType() != decoded.Value?.GetType()) throw new EvidenceBindingException(Malformed);
            return ScreenerEvidenceJson.Canonicalize(json);
        }
        catch (JsonException) { throw new EvidenceBindingException(Malformed); }
    }

    public static ScreenerEvidencePayload Decode(EvidenceClaim claim, int? version, string json)
    {
        if (version is null) throw new EvidenceBindingException("PERSISTED_EVIDENCE_UNBOUND");
        if (version != PayloadVersion) throw new EvidenceBindingException(Unsupported);
        if (claim == EvidenceClaim.PriceComparability) throw new EvidenceBindingException("PERSISTED_EVIDENCE_DERIVED_CLAIM");
        if (!Enum.IsDefined(claim)) throw new EvidenceBindingException(Unsupported);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = int.MaxValue });
            while (reader.Read())
                if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject && reader.CurrentDepth >= 16)
                    throw new EvidenceBindingException("PERSISTED_EVIDENCE_BOUND_EXCEEDED");
            using var tree = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            CheckTree(tree.RootElement);
            if (Encoding.UTF8.GetByteCount(ScreenerEvidenceJson.Canonicalize(json)) > MaximumBytes)
                throw new EvidenceBindingException("PERSISTED_EVIDENCE_BOUND_EXCEEDED");
            var root = tree.RootElement;
            Require(root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Count() == 3
                && root.TryGetProperty("operation", out _) && root.TryGetProperty("completeness", out _)
                && root.TryGetProperty("value", out _));
            var operation = root.GetProperty("operation").Deserialize<EvidenceOperation>(Options);
            var completeness = root.GetProperty("completeness").Deserialize<EvidenceCompleteness>(Options);
            var value = root.GetProperty("value");
            if (operation == EvidenceOperation.CANCEL)
            {
                Require(value.ValueKind == JsonValueKind.Null && completeness == EvidenceCompleteness.FULL);
                return new(operation, completeness, null);
            }
            Require(value.ValueKind == JsonValueKind.Object);
            var type = ValueType(claim, value);
            var typed = value.Deserialize(type, Options) as ScreenerEvidenceValue;
            Require(typed is not null);
            ValidateValue(typed!);
            return new(operation, completeness, typed);
        }
        catch (JsonException) { throw new EvidenceBindingException(Malformed); }
        catch (InvalidOperationException) { throw new EvidenceBindingException(Malformed); }
        catch (FormatException) { throw new EvidenceBindingException(Malformed); }
    }

    private static Type ValueType(EvidenceClaim claim, JsonElement value) => claim switch
    {
        EvidenceClaim.StableIdentity => typeof(IdentityValue), EvidenceClaim.SecurityType => typeof(SecurityTypeValue),
        EvidenceClaim.Currency => typeof(CurrencyValue), EvidenceClaim.ListingCoverage => typeof(ListingValue),
        EvidenceClaim.Delisting => typeof(DelistingValue), EvidenceClaim.BoardRegime => typeof(BoardValue),
        EvidenceClaim.BoardChange => typeof(BoardChangeValue), EvidenceClaim.ExchangeRuleVersion => typeof(RuleValue),
        EvidenceClaim.MechanismException => typeof(ExceptionValue), EvidenceClaim.Suspension => typeof(SuspensionValue),
        EvidenceClaim.Reopening => typeof(ReopeningValue), EvidenceClaim.ScheduledSession => typeof(ScheduledSessionValue),
        EvidenceClaim.CompletedSession => typeof(CompletedSessionValue), EvidenceClaim.TradingStatus => typeof(TradingStatusValue),
        EvidenceClaim.SourcePriceConvention => typeof(ConventionValue), EvidenceClaim.GenuinePriceObservation => typeof(PriceValue),
        EvidenceClaim.CorporateAction when value.TryGetProperty("kind", out var kind) => kind.GetString() switch
        {
            "EVENT" => typeof(ActionEventValue), "COVERAGE" => typeof(ActionCoverageValue),
            _ => throw new EvidenceBindingException(Malformed)
        },
        _ => throw new EvidenceBindingException(Malformed)
    };

    private static void CheckTree(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in node.EnumerateObject()) { Require(names.Add(p.Name)); CheckTree(p.Value); }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            if (node.GetArrayLength() > 256) throw new EvidenceBindingException("PERSISTED_EVIDENCE_BOUND_EXCEEDED");
            foreach (var child in node.EnumerateArray()) CheckTree(child);
        }
        else if (node.ValueKind == JsonValueKind.String)
        {
            var text = node.GetString()!;
            Require(text.Length is > 0 and <= 200 && text.Trim() == text && !text.Any(char.IsControl));
        }
    }

    private static void ValidateValue(ScreenerEvidenceValue value)
    {
        switch (value)
        {
            case CurrencyValue c: Require(Regex.IsMatch(c.CurrencyCode, @"\A[A-Z]{3}\z", RegexOptions.CultureInvariant)); break;
            case BoardValue b: Other(b.BoardCode == EvidenceBoard.OTHER, b.OtherCode); break;
            case BoardChangeValue b:
                Require(b.FromBoard is not null && b.ToBoard is not null);
                ValidateValue(b.FromBoard!); ValidateValue(b.ToBoard!); break;
            case RuleValue r: Other(r.Mechanism == EvidenceMechanism.OTHER, r.OtherMechanism); break;
            case ExceptionValue e: Other(e.Mechanism == EvidenceMechanism.OTHER, e.OtherMechanism); break;
            case SuspensionValue s: Require(s.Status == EvidenceStatus.SUSPENDED); break;
            case ReopeningValue r: Require(r.Status == EvidenceStatus.TRADING && r.SuspensionEvidenceId != Guid.Empty); break;
            case CompletedSessionValue s: Require((s.Completion == EvidenceCompletion.COMPLETED) == (s.CompletedAt is not null)); break;
            case ActionEventValue e:
                Require(e.Kind == EvidenceActionValueKind.EVENT && (e.Ratio is null || e.Ratio.NewUnits.Value > 0 && e.Ratio.OldUnits.Value > 0)); break;
            case ActionCoverageValue c:
                Require(c.Kind == EvidenceActionValueKind.COVERAGE && c.EventEvidenceIds is not null
                    && c.EventEvidenceIds.All(id => id != Guid.Empty)
                    && c.EventEvidenceIds.SequenceEqual(c.EventEvidenceIds.Distinct().OrderBy(id => id.ToString("D"), StringComparer.Ordinal))); break;
            case PriceValue p:
                Require(p.Bar is not null && p.BarRevision > 0 && p.ConventionEvidenceId != Guid.Empty && p.CompletedSessionEvidenceId != Guid.Empty);
                var bar = p.Bar!;
                Require(bar.Open.Value > 0 && bar.High.Value > 0 && bar.Low.Value > 0 && bar.Close.Value > 0 && bar.Volume >= 0
                    && bar.High.Value >= bar.Open.Value && bar.High.Value >= bar.Close.Value && bar.High.Value >= bar.Low.Value
                    && bar.Low.Value <= bar.Open.Value && bar.Low.Value <= bar.Close.Value && (bar.AdjustedClose is null || bar.AdjustedClose.Value.Value > 0)
                    && p.BarContentHash == BarHash(bar));
                Require(bar.Volume == 0
                    ? p.ZeroVolumeSemantics == EvidenceZeroVolumeSemantics.AMBIGUOUS && p.ZeroVolumeProof == EvidenceZeroProof.NONE
                        || p.ZeroVolumeSemantics == EvidenceZeroVolumeSemantics.EXPLICITLY_GENUINE && p.ZeroVolumeProof != EvidenceZeroProof.NONE
                    : p.ZeroVolumeSemantics == EvidenceZeroVolumeSemantics.NOT_APPLICABLE && p.ZeroVolumeProof == EvidenceZeroProof.NONE);
                break;
        }
    }
    private static void Other(bool other, string? code) => Require(other ? !string.IsNullOrEmpty(code) : code is null);
    private static void Require(bool condition) { if (!condition) throw new EvidenceBindingException(Malformed); }

    public static ScreenerEvidenceRecord Bind(ScreenerEvidenceRecord envelope, EvidenceClass evidenceClass,
        ScreenerScopeKind scopeKind, Guid exchangeId, ScreenerEvidencePayload payload)
    {
        var record = new ScreenerEvidenceRecord(envelope.EvidenceId, envelope.SubjectId, envelope.Claim, envelope.PolicyId,
            envelope.SchemaVersion, envelope.RevisionSeriesId, envelope.RevisionNumber, envelope.SupersedesRevisionNumber,
            envelope.AuthorityTier, envelope.EffectiveFrom, envelope.EffectiveTo, envelope.EffectiveAt, envelope.PublishedAt,
            envelope.RetrievedAt, envelope.KnownAt, envelope.SourceId, envelope.SourceReference, envelope.RawArtifactId,
            Encode(envelope.Claim, payload), evidenceClass, PayloadVersion, scopeKind, exchangeId);
        Decode(record);
        return record;
    }

    public static ScreenerEvidencePayload Decode(ScreenerEvidenceRecord record)
    {
        if (!ScreenerEvidenceV02.Supports(record.PolicyId, record.SchemaVersion)) throw new EvidenceBindingException(Unsupported);
        var payload = Decode(record.Claim, record.PayloadSchemaVersion, record.Payload);
        Require(record.Payload == ScreenerEvidenceJson.Canonicalize(record.Payload)
            && record.PayloadSha256 == ScreenerEvidenceJson.Sha256(record.Payload));
        var cls = record.EvidenceClass;
        var allowed = record.Claim switch
        {
            EvidenceClaim.StableIdentity or EvidenceClaim.SecurityType or EvidenceClaim.Currency or EvidenceClaim.ListingCoverage
                or EvidenceClaim.Delisting or EvidenceClaim.BoardRegime or EvidenceClaim.Suspension => cls is EvidenceClass.ContinuingState or EvidenceClass.PointObservation,
            EvidenceClaim.BoardChange or EvidenceClaim.MechanismException or EvidenceClaim.Reopening => cls == EvidenceClass.ContinuingState,
            EvidenceClaim.ExchangeRuleVersion => cls == EvidenceClass.VersionedRule,
            EvidenceClaim.ScheduledSession or EvidenceClaim.CompletedSession or EvidenceClaim.GenuinePriceObservation or EvidenceClaim.TradingStatus => cls == EvidenceClass.SessionFact,
            EvidenceClaim.SourcePriceConvention => cls == EvidenceClass.SourceConvention,
            EvidenceClaim.CorporateAction => cls == EvidenceClass.PointObservation,
            _ => false
        };
        Require(allowed);
        Require(Enum.IsDefined(record.AuthorityTier));
        var exchangeAllowed = record.Claim is EvidenceClaim.ExchangeRuleVersion or EvidenceClaim.MechanismException
            or EvidenceClaim.Suspension or EvidenceClaim.Reopening or EvidenceClaim.ScheduledSession or EvidenceClaim.CompletedSession
            or EvidenceClaim.SourcePriceConvention or EvidenceClaim.TradingStatus;
        Require(record.ScopeKind != ScreenerScopeKind.EXCHANGE || exchangeAllowed);
        Require(record.Claim is not (EvidenceClaim.ScheduledSession or EvidenceClaim.CompletedSession) || record.ScopeKind == ScreenerScopeKind.EXCHANGE);
        if (cls == EvidenceClass.PointObservation) Require(record.EffectiveTo is not null);
        if (cls == EvidenceClass.SessionFact) Require(record.EffectiveTo == record.EffectiveFrom);
        if (record.Claim == EvidenceClaim.TradingStatus) Require(ScreenerSourceAdmission.Admit(record.Claim, record.AuthorityTier).Admitted);
        if (record.Claim == EvidenceClaim.GenuinePriceObservation) Require(record.RawArtifactId is not null && record.RetrievedAt is not null);
        if (payload.Operation == EvidenceOperation.CANCEL)
            Require(record.RevisionSeriesId is not null && record.SupersedesRevisionNumber is { } rev && rev < record.RevisionNumber);
        else switch (payload.Value)
        {
            case BoardChangeValue b: Require(b.ChangeDate == record.EffectiveFrom); break;
            case ReopeningValue r: Require(r.ReopeningDate == record.EffectiveFrom); break;
            case ListingValue l when cls == EvidenceClass.ContinuingState: Require(l.ListingDate == record.EffectiveFrom); break;
            case DelistingValue d when cls == EvidenceClass.ContinuingState: Require(d.DelistingDate == record.EffectiveFrom); break;
            case CompletedSessionValue s: Require(s.CompletedAt is null || s.CompletedAt <= record.KnownAt); break;
            case ActionEventValue e: Require(e.EffectiveDate == record.EffectiveFrom && record.EffectiveTo == record.EffectiveFrom); break;
        }
        return payload;
    }

    private sealed class StrictEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsEnum;
        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(StrictEnumConverter<>).MakeGenericType(type))!;
    }
    private sealed class StrictEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String && Enum.TryParse<T>(reader.GetString(), false, out var value)
            && Enum.IsDefined(value) && reader.GetString() == Enum.GetName(value) ? value : throw new JsonException();
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteStringValue(Enum.GetName(value) ?? throw new JsonException());
    }
    private sealed class StrictGuidConverter : JsonConverter<Guid>
    {
        public override Guid Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            return Guid.TryParseExact(text, "D", out var id) && id != Guid.Empty && text == id.ToString("D") ? id : throw new JsonException();
        }
        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString("D"));
    }
    private sealed class StrictDateConverter : JsonConverter<DateOnly>
    {
        public override DateOnly Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String && DateOnly.TryParseExact(reader.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) && reader.GetString() == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ? date : throw new JsonException();
        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
    private sealed class StrictInstantConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            return text is not null && Regex.IsMatch(text, @"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?Z\z", RegexOptions.CultureInvariant)
                && DateTimeOffset.TryParseExact(text, ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"], CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : throw new JsonException();
        }
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            if (value.Offset != TimeSpan.Zero) throw new JsonException();
            writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
        }
    }
}
