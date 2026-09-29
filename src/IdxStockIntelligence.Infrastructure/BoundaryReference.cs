using System.Text.Json;
using IdxStockIntelligence.Application;

namespace IdxStockIntelligence.Infrastructure;

public static class BoundaryReference
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static InstrumentBoundaryEvidence[] ReadFile(string path)
    {
        var records = JsonSerializer.Deserialize<InstrumentBoundaryEvidence[]>(File.ReadAllText(path),Options)
            ?? throw new ArgumentException("Boundary record array required.",nameof(path));
        foreach (var record in records)
        {
            InstrumentBoundaries.Validate(record);
            if (record.RetrievedAt is null || record.KnownAt > DateTimeOffset.UtcNow)
                throw new ArgumentException("New imports require actual retrieval and already-known timestamps.",nameof(path));
        }
        if (records.GroupBy(r => (r.InstrumentId,r.KnownAt)).Any(g => g.Distinct().Count()>1))
            throw new ArgumentException("Conflicting same-time imports.",nameof(path));
        return records;
    }

    public static void Import(IReadOnlyList<InstrumentBoundaryEvidence> records)
    {
        foreach (var record in records)
        {
            InstrumentBoundaries.Validate(record);
            if (record.RetrievedAt is null || record.KnownAt>DateTimeOffset.UtcNow)
                throw new ArgumentException("Imports require actual retrieval and knowledge times.",nameof(records));
        }
        if (records.GroupBy(r => (r.InstrumentId,r.KnownAt)).Any(g => g.Distinct().Count()>1))
            throw new ArgumentException("Conflicting same-time imports.",nameof(records));
        var json=PilotDatabase.SqlLiteral(JsonSerializer.Serialize(records,Options));
        PilotDatabase.EnsureSchema();
        // ponytail: serialize reference batches; use narrower locks if concurrent import throughput requires it.
        PilotDatabase.Execute($$"""
            BEGIN;
            LOCK TABLE instrument_listing_evidence IN EXCLUSIVE MODE;
            CREATE TEMP TABLE boundary_input AS SELECT value AS data FROM jsonb_array_elements({{json}}::jsonb);
            INSERT INTO instrument(instrument_id,issuer_name,listed_on)
                SELECT (data->>'instrument_id')::uuid,data->>'issuer_name',NULL FROM boundary_input ON CONFLICT DO NOTHING;
            DO $check$
            BEGIN
                IF EXISTS(SELECT 1 FROM boundary_input b JOIN instrument_listing_evidence e
                    ON e.instrument_id=(b.data->>'instrument_id')::uuid AND e.known_at=(b.data->>'known_at')::timestamptz
                    WHERE e.evidence IS DISTINCT FROM b.data) THEN
                    RAISE EXCEPTION 'Boundary knowledge cannot be overwritten';
                END IF;
            END $check$;
            INSERT INTO instrument_listing_evidence
                SELECT (data->>'instrument_id')::uuid,(data->>'known_at')::timestamptz,data FROM boundary_input
                ON CONFLICT DO NOTHING;
            COMMIT;
            """);
    }

    public static IReadOnlyList<InstrumentBoundaryEvidence> ReadHistory()
    {
        using var rows=JsonDocument.Parse(PilotDatabase.Execute("""
            SELECT coalesce(jsonb_agg(jsonb_build_object('instrument_id',e.instrument_id,'issuer_name',i.issuer_name,
                'known_at',e.known_at,'evidence',e.evidence)),'[]') FROM instrument_listing_evidence e JOIN instrument i USING(instrument_id);
            """));
        return rows.RootElement.EnumerateArray().Select(row =>
        {
            var evidence=row.GetProperty("evidence");
            if (evidence.TryGetProperty("listed_from",out _))
                return evidence.Deserialize<InstrumentBoundaryEvidence>(Options)!;
            // Preserve legacy evidence chronology; never invent its missing retrieval time.
            var status=evidence.GetProperty("status").GetString()!;
            return new InstrumentBoundaryEvidence(row.GetProperty("instrument_id").GetGuid(),evidence.GetProperty("symbol").GetString()!,
                row.GetProperty("issuer_name").GetString()!,evidence.GetProperty("listed_on").ValueKind==JsonValueKind.Null ? null :
                    DateOnly.Parse(evidence.GetProperty("listed_on").GetString()!,System.Globalization.CultureInfo.InvariantCulture),
                null,null,status=="VERIFIED" ? "VERIFIED" : "UNKNOWN","legacy-reference-register",
                evidence.GetProperty("reference").GetString()!,"legacy listing register",null,row.GetProperty("known_at").GetDateTimeOffset(),
                evidence.GetProperty("confidence").GetString()!,"legacy-1","Original retrieval timestamp was not recorded.");
        }).ToArray();
    }
}
