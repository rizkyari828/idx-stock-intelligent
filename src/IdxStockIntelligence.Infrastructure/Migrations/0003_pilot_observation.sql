BEGIN;
CREATE TABLE IF NOT EXISTS instrument_listing_evidence (
    instrument_id uuid NOT NULL REFERENCES instrument,
    known_at timestamptz NOT NULL,
    evidence jsonb NOT NULL,
    PRIMARY KEY (instrument_id, known_at)
);
DROP TRIGGER IF EXISTS immutable_listing ON instrument_listing_evidence;
CREATE TRIGGER immutable_listing BEFORE UPDATE OR DELETE ON instrument_listing_evidence
    FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
CREATE OR REPLACE VIEW pilot_revision_evidence AS
    SELECT r.*, min(known_at) OVER (PARTITION BY instrument_id, session_date) AS first_seen_at
    FROM daily_bar_revision r;
COMMIT;
