using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using IdxStockIntelligence.Application;
using IdxStockIntelligence.Domain;

namespace IdxStockIntelligence.Infrastructure;

// Native psql avoids a new driver for this single local CLI. Replace when hosting a service.
public static class PilotDatabase
{
    public static string DatabaseName
    {
        get
        {
            var name = Environment.GetEnvironmentVariable("IDX_PILOT_DATABASE") ?? "idx_stock_intelligence";
            if (name.Length is < 1 or > 63 || !name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
                throw new ArgumentException("Invalid local pilot database name.");
            return name;
        }
    }
    public static string SqlLiteral(string value) => "'" + value.Replace("'", "''") + "'";

    public static string Execute(string sql)
    {
        var info = new ProcessStartInfo("docker") { RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in new[] { "compose", "exec", "-T", "postgres", "psql", "-X", "-q", "-A", "-t",
            "-v", "ON_ERROR_STOP=1", "-U", "idx_stock", "-d", DatabaseName }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start psql.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.StandardInput.Write(sql);
        process.StandardInput.Close();
        if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new InvalidOperationException("Database timed out."); }
        if (process.ExitCode != 0) throw new InvalidOperationException("Database operation failed: " + error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult().Trim();
    }

    public static void EnsureSchema()
    {
        if (Execute("SELECT to_regclass('public.source') IS NOT NULL;") == "f")
            Execute(File.ReadAllText("src/IdxStockIntelligence.Infrastructure/Migrations/0001_phase0_foundation.sql"));
        Execute(File.ReadAllText("src/IdxStockIntelligence.Infrastructure/Migrations/0002_prospective_pilot.sql"));
        Execute(File.ReadAllText("src/IdxStockIntelligence.Infrastructure/Migrations/0003_pilot_observation.sql"));
    }

    public static void Persist(object payload)
    {
        var json = SqlLiteral(JsonSerializer.Serialize(payload));
        // ponytail: serialize pilot ingestion; per-instrument locks if the fixed panel expands.
        Execute($$"""
            BEGIN;
            LOCK TABLE daily_bar_revision IN EXCLUSIVE MODE;
            CREATE TEMP TABLE pilot_input AS SELECT {{json}}::jsonb AS data;
            INSERT INTO source VALUES ('eodhd','EODHD private zero-cost pilot','UNKNOWN',
                'https://eodhd.com/financial-apis/quick-start-with-our-financial-data-apis',now()) ON CONFLICT DO NOTHING;
            INSERT INTO ingestion_run
                SELECT (data->>'run_id')::uuid,'eodhd',(data->>'started_at')::timestamptz,
                    (data->>'known_at')::timestamptz,'DEGRADED','zero-cost-pilot-1',data->'parameters',NULL
                FROM pilot_input ON CONFLICT DO NOTHING;
            INSERT INTO instrument (instrument_id,issuer_name,listed_on)
                SELECT (i->>'id')::uuid,i->>'name',(i->>'listed_on')::date
                FROM pilot_input,jsonb_array_elements(data->'instruments') i ON CONFLICT DO NOTHING;
            INSERT INTO instrument_listing_evidence
                SELECT (i->>'id')::uuid,(i->'listing_evidence'->>'known_at')::timestamptz,i->'listing_evidence'
                FROM pilot_input,jsonb_array_elements(data->'instruments') i
                WHERE i ? 'listing_evidence' ON CONFLICT DO NOTHING;
            INSERT INTO raw_artifact
                SELECT (a->>'id')::uuid,(data->>'run_id')::uuid,'eodhd',a->'manifest'->>'requested_uri',
                    a->'manifest'->'request_parameters',(a->'manifest'->>'fetched_at_utc')::timestamptz,
                    a->>'local_uri',a->'manifest'->'artifact'->>'content_sha256',
                    (a->'manifest'->'artifact'->>'byte_length')::bigint,a->'manifest'->>'parser_version'
                FROM pilot_input,jsonb_array_elements(data->'artifacts') a ON CONFLICT DO NOTHING;
            INSERT INTO raw_fetch_observation
                SELECT (data->>'run_id')::uuid,(a->>'id')::uuid,
                    (a->'manifest'->>'fetched_at_utc')::timestamptz,a->'manifest'
                FROM pilot_input,jsonb_array_elements(data->'artifacts') a ON CONFLICT DO NOTHING;
            DO $block$
            DECLARE item jsonb; input jsonb; previous daily_bar_revision%ROWTYPE;
            BEGIN
                SELECT data INTO input FROM pilot_input;
                FOR item IN SELECT * FROM jsonb_array_elements(input->'bars') LOOP
                    SELECT * INTO previous FROM daily_bar_revision
                        WHERE instrument_id=(item->>'instrument_id')::uuid AND session_date=(item->>'date')::date
                        ORDER BY revision_number DESC LIMIT 1;
                    IF previous.canonical_content_sha256 IS DISTINCT FROM item->>'hash' THEN
                        IF (item->>'retrieved_at')::timestamptz < previous.retrieved_at THEN
                            CONTINUE; -- Reprocessing an older archive is not a new provider reversion.
                        END IF;
                        IF (input->>'known_at')::timestamptz < previous.known_at THEN
                            RAISE EXCEPTION 'Revision knowledge cannot regress';
                        END IF;
                        INSERT INTO daily_bar_revision VALUES (
                            (item->>'instrument_id')::uuid,(item->>'date')::date,coalesce(previous.revision_number,0)+1,
                            (input->>'known_at')::timestamptz,(input->>'run_id')::uuid,(item->>'artifact_id')::uuid,
                            item->>'hash',(item->>'open')::numeric,(item->>'high')::numeric,(item->>'low')::numeric,
                            (item->>'close')::numeric,(item->>'volume')::bigint,'DEGRADED',
                            (item->>'adjusted_close')::numeric,item->>'volume_unit',item->>'volume_basis',item->>'market_segment',
                            (item->>'retrieved_at')::timestamptz,item->>'session_reference',(item->>'session_known_at')::timestamptz);
                    END IF;
                END LOOP;
            END $block$;
            COMMIT;
            """);
    }

    public static IReadOnlyList<DailyBarRevision> ReadRevisions()
    {
        var json = Execute("SELECT coalesce(jsonb_agg(to_jsonb(r)||jsonb_build_object('raw_hash',a.content_sha256,'source_id',a.source_id,'fetched_at',coalesce(r.retrieved_at,a.fetched_at))),'[]') FROM pilot_revision_evidence r JOIN raw_artifact a USING(raw_artifact_id);");
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray().Select(r =>
        {
            var fetched = r.GetProperty("fetched_at").GetDateTimeOffset();
            var source = new SourceReference(r.GetProperty("source_id").GetString()!, r.GetProperty("raw_artifact_id").GetGuid(), fetched, fetched, r.GetProperty("raw_hash").GetString()!);
            var bar = new DailyBar(new(r.GetProperty("instrument_id").GetGuid()), DateOnly.Parse(r.GetProperty("session_date").GetString()!, CultureInfo.InvariantCulture),
                r.GetProperty("open").GetDecimal(), r.GetProperty("high").GetDecimal(), r.GetProperty("low").GetDecimal(),
                r.GetProperty("close").GetDecimal(), r.GetProperty("volume").GetInt64(), source,
                r.GetProperty("adjusted_close").ValueKind == JsonValueKind.Null ? null : r.GetProperty("adjusted_close").GetDecimal(),
                r.GetProperty("volume_unit").GetString()!,r.GetProperty("volume_basis").GetString()!,r.GetProperty("market_segment").GetString()!);
            return new DailyBarRevision(r.GetProperty("revision_number").GetInt64(),bar,r.GetProperty("known_at").GetDateTimeOffset(),
                r.GetProperty("canonical_content_sha256").GetString()!,r.GetProperty("ingestion_run_id").GetGuid(),r.GetProperty("first_seen_at").GetDateTimeOffset());
        }).ToArray();
    }
}
