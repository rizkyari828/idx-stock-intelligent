BEGIN;
SELECT pg_advisory_xact_lock(20360928);
CREATE TABLE IF NOT EXISTS pilot_schema_version (version integer PRIMARY KEY);
DO $$
DECLARE constraint_name text;
BEGIN
IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 2) THEN
    ALTER TABLE instrument ALTER COLUMN listed_on DROP NOT NULL;
    FOR constraint_name IN SELECT conname FROM pg_constraint
        WHERE conrelid = 'daily_bar_revision'::regclass AND contype = 'u'
        AND pg_get_constraintdef(oid) LIKE '%canonical_content_sha256%'
    LOOP
        EXECUTE format('ALTER TABLE daily_bar_revision DROP CONSTRAINT %I', constraint_name);
    END LOOP;
    ALTER TABLE daily_bar_revision
        ADD COLUMN adjusted_close numeric CHECK (adjusted_close > 0),
        ADD COLUMN volume_unit text NOT NULL DEFAULT 'UNKNOWN',
        ADD COLUMN volume_basis text NOT NULL DEFAULT 'UNKNOWN',
        ADD COLUMN market_segment text NOT NULL DEFAULT 'UNKNOWN',
        ADD COLUMN retrieved_at timestamptz,
        ADD COLUMN session_reference text,
        ADD COLUMN session_known_at timestamptz,
        ADD CHECK (retrieved_at IS NULL OR known_at >= retrieved_at),
        ADD CHECK (session_known_at IS NULL OR known_at >= session_known_at);
    ALTER TABLE daily_bar_revision
        ALTER COLUMN open TYPE numeric, ALTER COLUMN high TYPE numeric,
        ALTER COLUMN low TYPE numeric, ALTER COLUMN close TYPE numeric;
    INSERT INTO pilot_schema_version VALUES (2);
END IF;
END $$;
CREATE TABLE IF NOT EXISTS raw_fetch_observation (
    ingestion_run_id uuid NOT NULL REFERENCES ingestion_run,
    raw_artifact_id uuid NOT NULL REFERENCES raw_artifact,
    fetched_at timestamptz NOT NULL,
    manifest jsonb NOT NULL,
    PRIMARY KEY (ingestion_run_id, raw_artifact_id, fetched_at)
);
CREATE OR REPLACE FUNCTION reject_evidence_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Evidence is append-only'; END $$;
DROP TRIGGER IF EXISTS immutable_daily_bar ON daily_bar_revision;
CREATE TRIGGER immutable_daily_bar BEFORE UPDATE OR DELETE ON daily_bar_revision
    FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
DROP TRIGGER IF EXISTS immutable_raw_artifact ON raw_artifact;
CREATE TRIGGER immutable_raw_artifact BEFORE UPDATE OR DELETE ON raw_artifact
    FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
DROP TRIGGER IF EXISTS immutable_fetch ON raw_fetch_observation;
CREATE TRIGGER immutable_fetch BEFORE UPDATE OR DELETE ON raw_fetch_observation
    FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
COMMIT;
