BEGIN;

CREATE TABLE source (
    source_id text PRIMARY KEY,
    display_name text NOT NULL,
    terms_status text NOT NULL CHECK (terms_status IN ('UNKNOWN', 'ALLOWED', 'DISALLOWED')),
    terms_evidence_uri text,
    reviewed_at timestamptz
);

CREATE TABLE ingestion_run (
    ingestion_run_id uuid PRIMARY KEY,
    source_id text NOT NULL REFERENCES source(source_id),
    started_at timestamptz NOT NULL,
    completed_at timestamptz,
    status text NOT NULL CHECK (status IN ('STARTED', 'SUCCEEDED', 'DEGRADED', 'FAILED')),
    collector_version text NOT NULL,
    request_parameters jsonb NOT NULL,
    error_code text,
    CHECK (completed_at IS NULL OR completed_at >= started_at)
);

CREATE TABLE raw_artifact (
    raw_artifact_id uuid PRIMARY KEY,
    ingestion_run_id uuid NOT NULL REFERENCES ingestion_run(ingestion_run_id),
    source_id text NOT NULL REFERENCES source(source_id),
    original_uri text NOT NULL,
    request_parameters jsonb NOT NULL,
    fetched_at timestamptz NOT NULL,
    local_uri text NOT NULL,
    content_sha256 char(64) NOT NULL,
    byte_length bigint NOT NULL CHECK (byte_length >= 0),
    parser_version text NOT NULL,
    UNIQUE (source_id, content_sha256)
);

CREATE TABLE instrument (
    instrument_id uuid PRIMARY KEY,
    issuer_name text NOT NULL,
    listed_on date NOT NULL,
    delisted_on date,
    CHECK (delisted_on IS NULL OR delisted_on >= listed_on)
);

CREATE TABLE instrument_history (
    instrument_id uuid NOT NULL REFERENCES instrument(instrument_id),
    symbol text NOT NULL,
    valid_from date NOT NULL,
    valid_to date,
    PRIMARY KEY (instrument_id, valid_from),
    CHECK (valid_to IS NULL OR valid_to >= valid_from)
);

CREATE TABLE market_session (
    session_date date PRIMARY KEY,
    status text NOT NULL CHECK (status IN ('TRADING', 'HOLIDAY', 'SUSPENSION', 'NO_TRADE')),
    evidence_raw_artifact_id uuid REFERENCES raw_artifact(raw_artifact_id)
);

CREATE TABLE daily_bar_revision (
    instrument_id uuid NOT NULL REFERENCES instrument(instrument_id),
    session_date date NOT NULL,
    revision_number bigint NOT NULL CHECK (revision_number > 0),
    known_at timestamptz NOT NULL,
    ingestion_run_id uuid NOT NULL REFERENCES ingestion_run(ingestion_run_id),
    raw_artifact_id uuid NOT NULL REFERENCES raw_artifact(raw_artifact_id),
    canonical_content_sha256 char(64) NOT NULL,
    open numeric(20, 6) NOT NULL CHECK (open > 0),
    high numeric(20, 6) NOT NULL CHECK (high > 0),
    low numeric(20, 6) NOT NULL CHECK (low > 0),
    close numeric(20, 6) NOT NULL CHECK (close > 0),
    volume bigint NOT NULL CHECK (volume >= 0),
    quality_status text NOT NULL CHECK (quality_status IN ('UNKNOWN', 'VALID', 'REJECTED', 'DEGRADED', 'STALE')),
    PRIMARY KEY (instrument_id, session_date, revision_number),
    UNIQUE (instrument_id, session_date, canonical_content_sha256),
    CHECK (high >= open AND high >= close AND high >= low),
    CHECK (low <= open AND low <= close)
);

CREATE INDEX daily_bar_revision_as_of_idx
    ON daily_bar_revision (instrument_id, session_date, known_at DESC, revision_number DESC);

COMMIT;
