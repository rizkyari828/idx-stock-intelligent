BEGIN;
SELECT pg_advisory_xact_lock(20360928);
DO $migration$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version=6) THEN
    IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version=5) THEN
        RAISE EXCEPTION 'Decision outcomes require schema version 5';
    END IF;
    CREATE TABLE decision_snapshot_outcome (
        run_id uuid NOT NULL,
        instrument_id uuid NOT NULL,
        horizon_sessions smallint NOT NULL CHECK (horizon_sessions IN (1,5,10,20)),
        schema_version smallint NOT NULL CHECK (schema_version=1),
        outcome_policy_id text NOT NULL CHECK (outcome_policy_id='outcome-v0.1.0'),
        anchor_market_date date,
        anchor_close numeric CHECK (anchor_close>0),
        horizon_market_date date NOT NULL CHECK (horizon_market_date BETWEEN '2026-08-24' AND '2027-08-24'),
        horizon_close numeric CHECK (horizon_close>0),
        price_return_pct numeric,
        outcome_state text NOT NULL CHECK (outcome_state IN ('AVAILABLE','ANCHOR_UNAVAILABLE','DATA_UNAVAILABLE','BASIS_UNCERTAIN')),
        reason text CHECK (length(reason) BETWEEN 1 AND 128),
        outcome_known_at timestamptz NOT NULL,
        recorded_at timestamptz NOT NULL DEFAULT clock_timestamp() CHECK (recorded_at>=outcome_known_at),
        evidence_manifest jsonb NOT NULL CHECK ((jsonb_typeof(evidence_manifest)='object'
            AND evidence_manifest->>'schemaVersion'='1' AND evidence_manifest->>'outcomePolicyId'='outcome-v0.1.0'
            AND evidence_manifest ?& ARRAY['captureSchemaVersion','capturePolicyId','captureInputHash','captureSelectedDigest',
                'instrumentId','horizonSessions','evaluationCutoff','anchor','endpoint','listing','calendar','instruments','instrumentSessions','archives','terminalCondition']
            AND octet_length(evidence_manifest::text)<=65536) IS TRUE),
        PRIMARY KEY (run_id,instrument_id,horizon_sessions),
        FOREIGN KEY (run_id,instrument_id) REFERENCES decision_snapshot_row(run_id,instrument_id) ON DELETE RESTRICT,
        CHECK ((outcome_state='AVAILABLE' AND anchor_close IS NOT NULL AND horizon_close IS NOT NULL
            AND price_return_pct IS NOT NULL AND reason IS NULL)
            OR (outcome_state<>'AVAILABLE' AND price_return_pct IS NULL AND reason IS NOT NULL))
    );
    CREATE FUNCTION decision_outcome_validate() RETURNS trigger LANGUAGE plpgsql AS $fn$
    DECLARE r decision_snapshot_run; s decision_snapshot_row; last_day date; sessions integer; link jsonb;
    BEGIN
        SELECT * INTO r FROM decision_snapshot_run WHERE run_id=NEW.run_id;
        SELECT * INTO s FROM decision_snapshot_row WHERE run_id=NEW.run_id AND instrument_id=NEW.instrument_id;
        IF r.run_id IS NULL OR s.instrument_id IS NULL OR r.target_session IS NULL
            OR NEW.anchor_market_date IS DISTINCT FROM s.market_date OR NEW.anchor_close IS DISTINCT FROM s.close
            OR NEW.horizon_market_date<=r.target_session OR NEW.outcome_known_at<r.captured_at
            OR NEW.horizon_market_date>(NEW.outcome_known_at AT TIME ZONE 'Asia/Jakarta')::date
            OR r.capture_kind<>'PROSPECTIVE_CAPTURE' OR r.schema_version<>1
            OR r.policy_id<>'screener-v0.1.0' OR r.universe<>'PILOT' THEN
            RAISE EXCEPTION 'Outcome capture/date/clock mismatch';
        END IF;
        IF (NEW.evidence_manifest->>'instrumentId')::uuid IS DISTINCT FROM NEW.instrument_id
            OR (NEW.evidence_manifest->>'horizonSessions')::integer IS DISTINCT FROM NEW.horizon_sessions
            OR (NEW.evidence_manifest->>'evaluationCutoff')::timestamptz IS DISTINCT FROM NEW.outcome_known_at
            OR NEW.evidence_manifest->>'captureInputHash' IS DISTINCT FROM r.input_hash
            OR NEW.evidence_manifest->>'captureSelectedDigest' IS DISTINCT FROM r.selected_digest
            OR (NEW.evidence_manifest->>'captureSchemaVersion')::integer IS DISTINCT FROM r.schema_version
            OR NEW.evidence_manifest->>'capturePolicyId' IS DISTINCT FROM r.policy_id
            OR jsonb_typeof(NEW.evidence_manifest->'calendar')<>'array'
            OR jsonb_typeof(NEW.evidence_manifest->'instruments')<>'array'
            OR jsonb_typeof(NEW.evidence_manifest->'archives')<>'array'
            OR NEW.evidence_manifest->>'terminalCondition' IS DISTINCT FROM NEW.reason THEN
            RAISE EXCEPTION 'Outcome manifest identity mismatch';
        END IF;
        SELECT max((d->>'date')::date),count(*) FILTER(WHERE d->>'classification'='ObservedTrading')
            INTO last_day,sessions FROM jsonb_array_elements(NEW.evidence_manifest->'calendar') d;
        IF last_day IS DISTINCT FROM NEW.horizon_market_date OR sessions<>NEW.horizon_sessions
            OR jsonb_array_length(NEW.evidence_manifest->'calendar')<>(NEW.horizon_market_date-r.target_session+1)
            OR EXISTS(SELECT FROM jsonb_array_elements(NEW.evidence_manifest->'calendar') WITH ORDINALITY AS x(d,n)
                WHERE (d->>'date')::date IS DISTINCT FROM r.target_session+(n::int-1)
                OR d->>'classification' NOT IN ('ANCHOR','ObservedTrading','Weekend','AnnouncedClosed','ExceptionalClosure')
                OR (n=1 AND d->>'classification'<>'ANCHOR')
                OR (n>1 AND d->>'classification'='ANCHOR')) THEN
            RAISE EXCEPTION 'Outcome session ordinal mismatch';
        END IF;
        FOREACH link IN ARRAY ARRAY[NEW.evidence_manifest->'anchor',NEW.evidence_manifest->'endpoint'] LOOP
            IF link<>'null'::jsonb AND NOT EXISTS(SELECT FROM daily_bar_revision b WHERE
                b.instrument_id=NEW.instrument_id AND b.instrument_id=(link->>'instrumentId')::uuid
                AND b.session_date=(link->>'sessionDate')::date AND b.revision_number=(link->>'revisionNumber')::integer
                AND b.known_at=(link->>'knownAt')::timestamptz AND b.known_at<=NEW.outcome_known_at
                AND b.canonical_content_sha256=link->>'contentHash' AND b.raw_artifact_id=(link->>'rawArtifactId')::uuid) THEN
                RAISE EXCEPTION 'Outcome canonical linkage mismatch';
            END IF;
        END LOOP;
        IF (NEW.evidence_manifest->'anchor'<>'null'::jsonb AND
            (NOT r.evidence_manifest->'bars' @> jsonb_build_array(NEW.evidence_manifest->'anchor')
             OR (NEW.evidence_manifest#>>'{anchor,sessionDate}')::date IS DISTINCT FROM NEW.anchor_market_date))
            OR (NEW.evidence_manifest->'endpoint'<>'null'::jsonb AND
                (NEW.evidence_manifest#>>'{endpoint,sessionDate}')::date IS DISTINCT FROM NEW.horizon_market_date)
            OR jsonb_array_length(NEW.evidence_manifest->'archives')<>3
            OR (SELECT count(DISTINCT a->>'kind') FROM jsonb_array_elements(NEW.evidence_manifest->'archives') a
                WHERE a->>'kind' IN ('screener-reference','sessions','instrument-sessions'))<>3 THEN
            RAISE EXCEPTION 'Outcome retained input linkage mismatch';
        END IF;
        IF NEW.outcome_state='DATA_UNAVAILABLE' AND
            (CASE WHEN NEW.reason='POST_DELISTING' THEN NEW.evidence_manifest->'listing'='null'::jsonb
            ELSE jsonb_array_length(NEW.evidence_manifest->'instruments')=0
                AND jsonb_array_length(NEW.evidence_manifest->'instrumentSessions')=0 END) THEN
            RAISE EXCEPTION 'Terminal endpoint requires authoritative linkage';
        END IF;
        IF NEW.outcome_state='BASIS_UNCERTAIN' AND (NEW.evidence_manifest->'endpoint'='null'::jsonb
            OR jsonb_array_length(NEW.evidence_manifest->'instruments')=0) THEN
            RAISE EXCEPTION 'Terminal comparability requires authoritative linkage';
        END IF;
        IF NEW.outcome_state='AVAILABLE' AND (NEW.anchor_market_date IS DISTINCT FROM r.target_session
            OR NEW.evidence_manifest->'anchor'='null'::jsonb OR NEW.evidence_manifest->'endpoint'='null'::jsonb) THEN
            RAISE EXCEPTION 'Available outcome requires exact endpoint linkage';
        END IF;
        IF NEW.outcome_state='DATA_UNAVAILABLE' AND NEW.reason NOT IN
            ('SUSPENDED_AT_HORIZON','NO_TRADE_AT_HORIZON','POST_DELISTING','SPECIAL_REGIME_UNSUPPORTED','ENDPOINT_CLASSIFICATION_UNSUPPORTED') THEN
            RAISE EXCEPTION 'No terminal endpoint evidence condition';
        END IF;
        IF NEW.outcome_state='BASIS_UNCERTAIN' AND NEW.reason NOT IN
            ('UNIT_CHANGING_EVENT','CAPITAL_ACTION_UNSUPPORTED','SECURITY_CONVERSION_UNSUPPORTED','PRICE_CONVENTION_UNSUPPORTED') THEN
            RAISE EXCEPTION 'No terminal comparability evidence condition';
        END IF;
        IF EXISTS(SELECT FROM decision_snapshot_outcome o WHERE o.run_id=NEW.run_id AND o.horizon_sessions=NEW.horizon_sessions
            AND o.horizon_market_date<>NEW.horizon_market_date) THEN RAISE EXCEPTION 'Outcome horizon alignment mismatch'; END IF;
        NEW.recorded_at:=clock_timestamp();
        RETURN NEW;
    END $fn$;
    CREATE TRIGGER decision_outcome_guard BEFORE INSERT ON decision_snapshot_outcome FOR EACH ROW EXECUTE FUNCTION decision_outcome_validate();
    CREATE TRIGGER immutable_decision_outcome BEFORE UPDATE OR DELETE ON decision_snapshot_outcome FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_decision_outcome_truncate BEFORE TRUNCATE ON decision_snapshot_outcome FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
    INSERT INTO pilot_schema_version VALUES(6);
END IF;
END $migration$;
COMMIT;
