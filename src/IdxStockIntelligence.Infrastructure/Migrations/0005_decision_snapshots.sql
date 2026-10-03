BEGIN;
SELECT pg_advisory_xact_lock(20360928);
-- Refresh this narrow guard on controlled rerun; legacy restored IDs alone cannot authorize an INSERT.
CREATE OR REPLACE FUNCTION decision_snapshot_same_transaction() RETURNS trigger LANGUAGE plpgsql AS $fn$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM decision_snapshot_run WHERE run_id=NEW.run_id AND originating_xid=pg_current_xact_id() AND xmin=pg_current_xact_id()::xid) THEN
        RAISE EXCEPTION 'Rows must be inserted in run creation transaction';
    END IF;
    RETURN NEW;
END $fn$;
DO $migration$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 5) THEN
    IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 4) THEN
        RAISE EXCEPTION 'Decision snapshots require schema version 4';
    END IF;
    CREATE TABLE decision_snapshot_run (
        run_id uuid PRIMARY KEY CHECK (run_id <> '00000000-0000-0000-0000-000000000000'),
        request_id uuid NOT NULL UNIQUE CHECK (request_id <> '00000000-0000-0000-0000-000000000000'),
        schema_version smallint NOT NULL CHECK (schema_version = 1),
        capture_kind text NOT NULL CHECK (capture_kind = 'PROSPECTIVE_CAPTURE'),
        captured_at timestamptz NOT NULL,
        knowledge_cutoff timestamptz NOT NULL CHECK (knowledge_cutoff = captured_at),
        recorded_at timestamptz NOT NULL DEFAULT clock_timestamp() CHECK (recorded_at >= captured_at),
        through date NOT NULL CHECK (through BETWEEN '2026-08-24' AND '2027-08-24'
            AND through = (captured_at AT TIME ZONE 'Asia/Jakarta')::date),
        history_anchor date NOT NULL CHECK (history_anchor = '2026-08-24'),
        target_session date CHECK (target_session BETWEEN history_anchor AND through),
        policy_id text NOT NULL CHECK (policy_id = 'screener-v0.1.0'),
        universe text NOT NULL CHECK (universe = 'PILOT'),
        universe_snapshot_id text,
        portfolio_id uuid REFERENCES portfolio(portfolio_id) ON DELETE RESTRICT,
        input_hash text NOT NULL CHECK (input_hash ~ '^[0-9a-f]{64}$'),
        selected_digest text NOT NULL CHECK (selected_digest ~ '^[0-9a-f]{64}$'),
        status text NOT NULL CHECK (status IN ('COMPLETE','PARTIAL','BLOCKED')),
        row_count smallint NOT NULL CHECK (row_count BETWEEN 0 AND 210),
        request_intent jsonb NOT NULL CHECK ((jsonb_typeof(request_intent) = 'object'
            AND request_intent ?& ARRAY['through','portfolioId']
            AND request_intent - ARRAY['through','portfolioId'] = '{}') IS TRUE),
        result jsonb NOT NULL CHECK ((jsonb_typeof(result) = 'object'
            AND result ?& ARRAY['reasons','summary','rankedCandidateIds','allViewIds','shortlistIds','heldIds','marketContext']
            AND octet_length(result::text) <= 32768) IS TRUE),
        evidence_manifest jsonb NOT NULL CHECK ((jsonb_typeof(evidence_manifest) = 'object'
            AND evidence_manifest->>'schemaVersion' = '1'
            AND jsonb_typeof(evidence_manifest->'evaluatedInstrumentIds') = 'array'
            AND octet_length(evidence_manifest::text) <= 33554432) IS TRUE),
        originating_xid xid8 NOT NULL DEFAULT pg_current_xact_id()
    );
    CREATE TABLE decision_snapshot_row (
        run_id uuid NOT NULL REFERENCES decision_snapshot_run(run_id) ON DELETE RESTRICT,
        instrument_id uuid NOT NULL CHECK (instrument_id <> '00000000-0000-0000-0000-000000000000'),
        symbol text,
        configured boolean NOT NULL,
        held boolean NOT NULL,
        discovery_rank integer CHECK (discovery_rank > 0),
        eligibility text NOT NULL CHECK (eligibility IN ('ELIGIBLE','INELIGIBLE','DATA_BLOCKED')),
        setup text NOT NULL CHECK (setup IN ('NONE','WATCH','CONFIRMED','FAILED')),
        setup_evaluated boolean NOT NULL,
        episode_id text,
        market_date date,
        close numeric CHECK (close > 0),
        shares numeric CHECK (shares >= 0),
        invested_cost numeric CHECK (invested_cost >= 0),
        average_cost numeric CHECK (average_cost > 0),
        mandate text CHECK (mandate IN ('INVEST','FAST_SWING','LONG_SWING')),
        thesis_version_id uuid,
        thesis_version integer CHECK (thesis_version > 0),
        thesis_active boolean,
        result jsonb NOT NULL CHECK ((jsonb_typeof(result) = 'object'
            AND result ?& ARRAY['eligibilityReasons','setupReasons','episode','fieldStates','provenance']
            AND octet_length(result::text) <= 8192) IS TRUE),
        PRIMARY KEY (run_id,instrument_id),
        CHECK (episode_id IS NOT DISTINCT FROM result#>>'{episode,id}'),
        CHECK ((thesis_version_id IS NULL AND thesis_version IS NULL AND thesis_active IS NULL)
            OR (thesis_version_id IS NOT NULL AND thesis_version IS NOT NULL AND thesis_active IS NOT NULL)),
        CHECK (mandate IS NULL OR (held AND thesis_active IS TRUE)),
        CHECK (held OR (thesis_version_id IS NULL AND mandate IS NULL)),
        CHECK (discovery_rank IS NULL OR (configured AND eligibility='ELIGIBLE' AND setup_evaluated AND setup IN ('WATCH','CONFIRMED')))
    );
    CREATE INDEX decision_snapshot_recent_idx ON decision_snapshot_run(captured_at DESC,run_id DESC);
    CREATE INDEX decision_snapshot_portfolio_idx ON decision_snapshot_run(portfolio_id,captured_at DESC,run_id DESC);
    CREATE INDEX decision_snapshot_instrument_idx ON decision_snapshot_row(instrument_id,run_id);
    CREATE UNIQUE INDEX decision_snapshot_rank_idx ON decision_snapshot_row(run_id,discovery_rank) WHERE discovery_rank IS NOT NULL;

    CREATE FUNCTION decision_snapshot_record_clock() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        NEW.originating_xid := pg_current_xact_id();
        NEW.recorded_at := clock_timestamp();
        RETURN NEW;
    END $fn$;
    CREATE FUNCTION decision_snapshot_complete() RETURNS trigger LANGUAGE plpgsql AS $fn$
    DECLARE actual integer; declared integer;
    BEGIN
        SELECT count(*) INTO actual FROM decision_snapshot_row WHERE run_id=NEW.run_id;
        SELECT count(*) INTO declared FROM jsonb_array_elements_text(NEW.evidence_manifest->'evaluatedInstrumentIds');
        IF actual <> NEW.row_count OR declared <> actual OR EXISTS (
            SELECT value::uuid FROM jsonb_array_elements_text(NEW.evidence_manifest->'evaluatedInstrumentIds')
            EXCEPT SELECT instrument_id FROM decision_snapshot_row WHERE run_id=NEW.run_id
        ) OR EXISTS (
            SELECT instrument_id FROM decision_snapshot_row WHERE run_id=NEW.run_id
            EXCEPT SELECT value::uuid FROM jsonb_array_elements_text(NEW.evidence_manifest->'evaluatedInstrumentIds')
        ) THEN RAISE EXCEPTION 'Incomplete decision population'; END IF;
        IF (NEW.evidence_manifest#>>'{request,through}')::date IS DISTINCT FROM NEW.through
            OR (NEW.evidence_manifest#>>'{request,cutoff}')::timestamptz IS DISTINCT FROM NEW.knowledge_cutoff
            OR (NEW.evidence_manifest#>>'{request,historyAnchor}')::date IS DISTINCT FROM NEW.history_anchor
            OR (NEW.request_intent->>'portfolioId')::uuid IS DISTINCT FROM NEW.portfolio_id
            OR (NEW.request_intent->>'through' IS NOT NULL AND (NEW.request_intent->>'through')::date <> NEW.through) THEN
            RAISE EXCEPTION 'Decision chronology mismatch';
        END IF;
        IF EXISTS (SELECT 1 FROM decision_snapshot_row WHERE run_id=NEW.run_id AND episode_id IS NOT NULL AND
            ((result#>>'{episode,startDate}') IS NULL
             OR (result#>>'{episode,startDate}')::date NOT BETWEEN NEW.history_anchor AND NEW.through
             OR (result#>>'{episode,confirmationDate}')::date NOT BETWEEN (result#>>'{episode,startDate}')::date AND NEW.through
             OR (result#>>'{episode,endDate}')::date NOT BETWEEN (result#>>'{episode,startDate}')::date AND NEW.through)) THEN
            RAISE EXCEPTION 'Decision episode chronology mismatch';
        END IF;
        IF EXISTS (SELECT 1 FROM decision_snapshot_row WHERE run_id=NEW.run_id AND
            (market_date > NEW.through OR
             (NEW.portfolio_id IS NULL AND (held OR shares IS NOT NULL OR invested_cost IS NOT NULL OR average_cost IS NOT NULL OR thesis_version_id IS NOT NULL OR mandate IS NOT NULL)) OR
             (NEW.portfolio_id IS NOT NULL AND (shares IS NULL OR invested_cost IS NULL OR held IS DISTINCT FROM (shares>0)
                OR (shares=0 AND (invested_cost<>0 OR average_cost IS NOT NULL)) OR (shares>0 AND average_cost IS NULL))))) THEN
            RAISE EXCEPTION 'Decision row context mismatch';
        END IF;
        -- Arrays are ordered outputs, not canonical-hash sets. Preserve their membership and rank order.
        IF EXISTS (
            SELECT value::uuid FROM jsonb_array_elements_text(NEW.result->'heldIds')
            EXCEPT SELECT instrument_id FROM decision_snapshot_row WHERE run_id=NEW.run_id AND held
        ) OR EXISTS (
            SELECT instrument_id FROM decision_snapshot_row WHERE run_id=NEW.run_id AND held
            EXCEPT SELECT value::uuid FROM jsonb_array_elements_text(NEW.result->'heldIds')
        ) OR jsonb_array_length(NEW.result->'heldIds') <> (SELECT count(*) FROM decision_snapshot_row WHERE run_id=NEW.run_id AND held)
        OR EXISTS (
            SELECT value::uuid FROM jsonb_array_elements_text(NEW.result->'allViewIds')
            EXCEPT SELECT instrument_id FROM decision_snapshot_row WHERE run_id=NEW.run_id AND configured
        ) OR EXISTS (
            SELECT instrument_id FROM decision_snapshot_row WHERE run_id=NEW.run_id AND configured
            EXCEPT SELECT value::uuid FROM jsonb_array_elements_text(NEW.result->'allViewIds')
        ) OR jsonb_array_length(NEW.result->'allViewIds') <> (SELECT count(*) FROM decision_snapshot_row WHERE run_id=NEW.run_id AND configured)
        OR jsonb_array_length(NEW.result->'rankedCandidateIds') <> (SELECT count(*) FROM decision_snapshot_row WHERE run_id=NEW.run_id AND discovery_rank IS NOT NULL)
        OR EXISTS (SELECT 1 FROM jsonb_array_elements_text(NEW.result->'rankedCandidateIds') WITH ORDINALITY a(value,rank)
            WHERE NOT EXISTS (SELECT 1 FROM decision_snapshot_row WHERE run_id=NEW.run_id AND instrument_id=a.value::uuid AND discovery_rank=a.rank))
        OR NEW.result->'shortlistIds' IS DISTINCT FROM (SELECT coalesce(jsonb_agg(value ORDER BY rank),'[]')
            FROM jsonb_array_elements(NEW.result->'rankedCandidateIds') WITH ORDINALITY a(value,rank) WHERE rank<=20)
        THEN RAISE EXCEPTION 'Decision membership/order mismatch'; END IF;
        IF NEW.evidence_manifest->'heldIds' IS DISTINCT FROM NEW.result->'heldIds'
            OR EXISTS (SELECT value::uuid FROM jsonb_array_elements_text(NEW.evidence_manifest->'configuredIds')
                EXCEPT SELECT instrument_id FROM decision_snapshot_row WHERE run_id=NEW.run_id AND configured)
            OR EXISTS (SELECT instrument_id FROM decision_snapshot_row WHERE run_id=NEW.run_id AND configured
                EXCEPT SELECT value::uuid FROM jsonb_array_elements_text(NEW.evidence_manifest->'configuredIds'))
            OR jsonb_array_length(NEW.evidence_manifest->'configuredIds') <> (SELECT count(*) FROM decision_snapshot_row WHERE run_id=NEW.run_id AND configured)
            THEN RAISE EXCEPTION 'Decision manifest mismatch'; END IF;
        RETURN NEW;
    END $fn$;
    CREATE TRIGGER decision_snapshot_clock BEFORE INSERT ON decision_snapshot_run FOR EACH ROW EXECUTE FUNCTION decision_snapshot_record_clock();
    CREATE TRIGGER decision_snapshot_row_transaction BEFORE INSERT ON decision_snapshot_row FOR EACH ROW EXECUTE FUNCTION decision_snapshot_same_transaction();
    CREATE CONSTRAINT TRIGGER decision_snapshot_population AFTER INSERT ON decision_snapshot_run
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION decision_snapshot_complete();
    CREATE TRIGGER immutable_decision_run BEFORE UPDATE OR DELETE ON decision_snapshot_run FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_decision_row BEFORE UPDATE OR DELETE ON decision_snapshot_row FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_decision_run_truncate BEFORE TRUNCATE ON decision_snapshot_run FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_decision_row_truncate BEFORE TRUNCATE ON decision_snapshot_row FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
    INSERT INTO pilot_schema_version VALUES (5);
END IF;
END $migration$;
COMMIT;
