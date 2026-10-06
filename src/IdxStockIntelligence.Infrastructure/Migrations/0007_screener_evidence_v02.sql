BEGIN;
SELECT pg_advisory_xact_lock(20360928);
DO $migration$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 7) THEN
    IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 6) THEN
        RAISE EXCEPTION 'Screener evidence V0.2 requires schema version 6';
    END IF;
    CREATE TABLE screener_evidence_record (
        evidence_id uuid PRIMARY KEY CHECK (evidence_id <> '00000000-0000-0000-0000-000000000000'),
        subject_id uuid NOT NULL CHECK (subject_id <> '00000000-0000-0000-0000-000000000000'),
        claim text NOT NULL CHECK (claim IN ('StableIdentity','SecurityType','Currency','ListingCoverage',
            'Delisting','BoardRegime','BoardChange','ExchangeRuleVersion','MechanismException','Suspension',
            'Reopening','ScheduledSession','CompletedSession','CorporateAction','SourcePriceConvention',
            'GenuinePriceObservation','PriceComparability')),
        policy_id text NOT NULL CHECK (policy_id = 'screener-evidence-v0.2.0'),
        schema_version smallint NOT NULL CHECK (schema_version = 1),
        revision_series_id text CHECK (length(revision_series_id) BETWEEN 1 AND 200),
        revision_number bigint NOT NULL CHECK (revision_number > 0),
        supersedes_revision_number bigint CHECK (supersedes_revision_number > 0),
        authority_tier smallint NOT NULL CHECK (authority_tier BETWEEN 1 AND 5),
        effective_from date NOT NULL,
        effective_to date CHECK (effective_to IS NULL OR effective_to >= effective_from),
        effective_at timestamptz,
        published_at timestamptz,
        retrieved_at timestamptz,
        known_at timestamptz NOT NULL,
        recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
        source_id text NOT NULL CHECK (length(source_id) BETWEEN 1 AND 200),
        source_reference text,
        raw_artifact_id uuid REFERENCES raw_artifact(raw_artifact_id) ON DELETE RESTRICT,
        payload text NOT NULL CHECK (jsonb_typeof(payload::jsonb) = 'object'),
        payload_sha256 char(64) NOT NULL CHECK (payload_sha256 ~ '^[0-9a-f]{64}$'),
        CHECK (retrieved_at IS NULL OR known_at >= retrieved_at),
        CHECK (supersedes_revision_number IS NULL OR revision_series_id IS NOT NULL)
    );
    CREATE UNIQUE INDEX screener_evidence_series_revision_idx
        ON screener_evidence_record(revision_series_id, revision_number) WHERE revision_series_id IS NOT NULL;
    CREATE INDEX screener_evidence_subject_claim_idx
        ON screener_evidence_record(subject_id, claim, known_at DESC, revision_number DESC);
    CREATE TRIGGER immutable_screener_evidence BEFORE UPDATE OR DELETE ON screener_evidence_record
        FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_screener_evidence_truncate BEFORE TRUNCATE ON screener_evidence_record
        FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
    INSERT INTO pilot_schema_version VALUES (7);
END IF;
END $migration$;
COMMIT;
