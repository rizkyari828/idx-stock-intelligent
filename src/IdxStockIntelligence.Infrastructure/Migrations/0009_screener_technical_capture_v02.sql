BEGIN;
SELECT pg_advisory_xact_lock(20360928);
DO $migration$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 9) THEN
    IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 8) THEN
        RAISE EXCEPTION 'Technical evaluation captures require schema version 8';
    END IF;
    CREATE TABLE screener_technical_capture (
        capture_id uuid PRIMARY KEY CHECK (capture_id <> '00000000-0000-0000-0000-000000000000'),
        input_hash text NOT NULL UNIQUE CHECK (input_hash ~ '^[0-9a-f]{64}$'),
        result_hash text NOT NULL CHECK (result_hash ~ '^[0-9a-f]{64}$'),
        subject_id uuid NOT NULL CHECK (subject_id <> '00000000-0000-0000-0000-000000000000'),
        policy_id text NOT NULL CHECK (policy_id = 'screener-evidence-v0.2.0'),
        schema_version smallint NOT NULL CHECK (schema_version = 1),
        capture_schema_version smallint NOT NULL CHECK (capture_schema_version = 1),
        session_id text NOT NULL CHECK (length(session_id) BETWEEN 1 AND 200),
        evaluation_date date NOT NULL,
        -- JSON/text preserves all seven .NET fractional digits; PostgreSQL timestamps keep six.
        knowledge_cutoff text NOT NULL,
        recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
        projection text NOT NULL CHECK (octet_length(projection) <= 33554432),
        CHECK ((jsonb_typeof(projection::jsonb) = 'object'
            AND (projection::jsonb->>'captureSchemaVersion')::int = capture_schema_version
            AND (projection::jsonb#>>'{result,technicalEvaluated}')::boolean IS TRUE
            AND (projection::jsonb#>>'{result,setup,evaluated}')::boolean IS TRUE
            AND (projection::jsonb#>>'{result,request,subjectId}')::uuid = subject_id
            AND projection::jsonb#>>'{result,request,policyId}' = policy_id
            AND (projection::jsonb#>>'{result,request,schemaVersion}')::int = schema_version
            AND projection::jsonb#>>'{result,request,sessionId}' = session_id
            AND (projection::jsonb#>>'{result,request,evaluationDate}')::date = evaluation_date
            AND projection::jsonb#>>'{result,request,cutoff}' = knowledge_cutoff
            AND jsonb_typeof(projection::jsonb->'readinessHistory') = 'array') IS TRUE)
    );
    CREATE TRIGGER immutable_screener_technical_capture BEFORE UPDATE OR DELETE ON screener_technical_capture
        FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_screener_technical_capture_truncate BEFORE TRUNCATE ON screener_technical_capture
        FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
    INSERT INTO pilot_schema_version VALUES (9);
END IF;
END $migration$;
COMMIT;
