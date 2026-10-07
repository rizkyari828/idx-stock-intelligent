BEGIN;
SELECT pg_advisory_xact_lock(20360928);
DO $migration$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 10) THEN
    IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 9) THEN
        RAISE EXCEPTION 'Technical candidates require schema version 9';
    END IF;
    CREATE TABLE screener_technical_candidate (
        decision_id uuid PRIMARY KEY CHECK (decision_id <> '00000000-0000-0000-0000-000000000000'),
        capture_id uuid NOT NULL REFERENCES screener_technical_capture(capture_id) ON DELETE RESTRICT,
        candidate_policy_id text NOT NULL CHECK (candidate_policy_id = 'screener-technical-candidate-v0.2.0'),
        schema_version smallint NOT NULL CHECK (schema_version = 1),
        replay_identity text NOT NULL CHECK (replay_identity ~ '^[0-9a-f]{64}$'),
        recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
        projection text NOT NULL CHECK (octet_length(projection) <= 8192),
        UNIQUE(capture_id,candidate_policy_id,schema_version),
        CHECK ((jsonb_typeof(projection::jsonb) = 'object'
            AND projection::jsonb->>'candidatePolicyId' = candidate_policy_id
            AND (projection::jsonb->>'schemaVersion')::int = schema_version
            AND (projection::jsonb->>'sourceCaptureId')::uuid = capture_id
            AND projection::jsonb->>'sourceInputHash' ~ '^[0-9a-f]{64}$'
            AND projection::jsonb->>'sourceResultHash' ~ '^[0-9a-f]{64}$'
            AND (projection::jsonb->>'captureSchemaVersion')::int = 1
            AND projection::jsonb->>'technicalPolicyId' = 'screener-evidence-v0.2.0'
            AND (projection::jsonb->>'technicalSchemaVersion')::int = 1
            AND projection::jsonb->>'setup' IN ('None','Watch','Confirmed','Failed')
            AND (projection::jsonb->>'candidate')::boolean = (projection::jsonb->>'setup' IN ('Watch','Confirmed'))
            AND jsonb_typeof(projection::jsonb->'reasons') = 'array') IS TRUE)
    );
    CREATE TRIGGER immutable_screener_technical_candidate BEFORE UPDATE OR DELETE ON screener_technical_candidate
        FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_screener_technical_candidate_truncate BEFORE TRUNCATE ON screener_technical_candidate
        FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
    INSERT INTO pilot_schema_version VALUES (10);
END IF;
END $migration$;
COMMIT;
