BEGIN;
SELECT pg_advisory_xact_lock(20360928);
DO $migration$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version=11) THEN
    IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version=10) THEN
        RAISE EXCEPTION 'Candidate outcomes require schema version 10';
    END IF;
    CREATE TABLE outcome_v02_enrollment (
        enrollment_id uuid PRIMARY KEY,
        enrollment_identity text NOT NULL UNIQUE CHECK (enrollment_identity ~ '^[0-9a-f]{64}$'),
        binding_hash text NOT NULL CHECK (binding_hash ~ '^[0-9a-f]{64}$'),
        candidate_decision_id uuid NOT NULL REFERENCES screener_technical_candidate(decision_id) ON DELETE RESTRICT,
        capture_id uuid NOT NULL REFERENCES screener_technical_capture(capture_id) ON DELETE RESTRICT,
        outcome_policy_id text NOT NULL CHECK (outcome_policy_id='outcome-v0.2.0'),
        schema_version smallint NOT NULL CHECK (schema_version=1),
        enrollment_known_at timestamptz NOT NULL,
        enrollment_recorded_at timestamptz NOT NULL CHECK (enrollment_recorded_at>=enrollment_known_at),
        enrollment_deadline timestamptz NOT NULL CHECK (enrollment_deadline>enrollment_recorded_at),
        projection text NOT NULL CHECK (octet_length(projection)<=65536 AND jsonb_typeof(projection::jsonb)='object'),
        UNIQUE(candidate_decision_id,outcome_policy_id,schema_version)
    );
    CREATE FUNCTION outcome_v02_enrollment_validate() RETURNS trigger LANGUAGE plpgsql AS $fn$
    DECLARE d screener_technical_candidate; c screener_technical_capture; p jsonb; q jsonb;
    BEGIN
        SELECT * INTO d FROM screener_technical_candidate WHERE decision_id=NEW.candidate_decision_id;
        SELECT * INTO c FROM screener_technical_capture WHERE capture_id=NEW.capture_id;
        p:=NEW.projection::jsonb; q:=d.projection::jsonb;
        IF (d.capture_id=NEW.capture_id AND d.candidate_policy_id='screener-technical-candidate-v0.2.0' AND d.schema_version=1
            AND (q->>'candidate')::boolean AND c.policy_id='screener-evidence-v0.2.0' AND c.schema_version=1 AND c.capture_schema_version=1
            AND c.knowledge_cutoff::timestamptz<=c.recorded_at AND c.recorded_at<=d.recorded_at
            AND d.recorded_at<=NEW.enrollment_known_at AND NEW.enrollment_recorded_at<=clock_timestamp()
            AND clock_timestamp()<NEW.enrollment_deadline
            AND NEW.enrollment_deadline=((c.evaluation_date+1)::timestamp AT TIME ZONE 'Asia/Jakarta')
            AND c.evaluation_date BETWEEN '2026-08-24' AND '2027-08-24'
            AND p->>'outcomePolicyId'=NEW.outcome_policy_id AND (p->>'schemaVersion')::int=1
            AND (p->>'candidateDecisionId')::uuid=NEW.candidate_decision_id AND (p->>'captureId')::uuid=NEW.capture_id
            AND p->>'candidatePolicyId'=d.candidate_policy_id AND (p->>'candidateSchemaVersion')::int=d.schema_version
            AND p->>'candidateReplayIdentity'=d.replay_identity AND p->>'captureInputHash'=c.input_hash AND p->>'captureResultHash'=c.result_hash
            AND p->>'technicalPolicyId'=c.policy_id AND (p->>'technicalSchemaVersion')::int=c.schema_version AND (p->>'captureSchemaVersion')::int=1
            AND (p->>'candidateRecordedAt')::timestamptz=d.recorded_at AND (p->>'captureRecordedAt')::timestamptz=c.recorded_at
            AND p->>'captureReplayIdentity'=c.projection::jsonb#>>'{result,replayIdentity}'
            AND (p->>'instrumentId')::uuid=c.subject_id AND p->>'sessionId'=c.session_id AND (p->>'evaluationDate')::date=c.evaluation_date
            AND p->>'originalCutoff'=c.knowledge_cutoff
            AND (p->>'enrollmentKnownAt')::timestamptz=NEW.enrollment_known_at
            AND (p->>'enrollmentRecordedAt')::timestamptz=NEW.enrollment_recorded_at
            AND (p->>'enrollmentDeadline')::timestamptz=NEW.enrollment_deadline
            AND jsonb_typeof(p->'captureEvidenceIds')='array' AND jsonb_typeof(p->'artifacts')='array'
            AND p->'anchor'= (SELECT b FROM jsonb_array_elements(c.projection::jsonb#>'{result,stockBindings}') b
                WHERE (b->>'date')::date=c.evaluation_date AND (b->>'subjectId')::uuid=c.subject_id)
            AND p->>'exchangeId'=p#>>'{anchor,evidence,exchangeId}') IS NOT TRUE THEN
            RAISE EXCEPTION 'Outcome enrollment source/clock/anchor integrity conflict';
        END IF;
        RETURN NEW;
    END $fn$;
    CREATE TRIGGER outcome_v02_enrollment_guard BEFORE INSERT ON outcome_v02_enrollment FOR EACH ROW EXECUTE FUNCTION outcome_v02_enrollment_validate();
    CREATE TRIGGER immutable_outcome_v02_enrollment BEFORE UPDATE OR DELETE ON outcome_v02_enrollment FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_outcome_v02_enrollment_truncate BEFORE TRUNCATE ON outcome_v02_enrollment FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TABLE outcome_v02_observation (
        enrollment_id uuid NOT NULL REFERENCES outcome_v02_enrollment(enrollment_id) ON DELETE RESTRICT,
        horizon_sessions smallint NOT NULL CHECK (horizon_sessions IN (1,5,10,20)),
        observation_id uuid NOT NULL UNIQUE,
        observation_identity text NOT NULL UNIQUE CHECK (observation_identity ~ '^[0-9a-f]{64}$'),
        result_hash text NOT NULL CHECK (result_hash ~ '^[0-9a-f]{64}$'),
        outcome_policy_id text NOT NULL CHECK (outcome_policy_id='outcome-v0.2.0'),
        schema_version smallint NOT NULL CHECK (schema_version=1),
        outcome_known_at timestamptz NOT NULL,
        recorded_at timestamptz NOT NULL CHECK (recorded_at>=outcome_known_at),
        projection text NOT NULL CHECK (octet_length(projection)<=2097152 AND jsonb_typeof(projection::jsonb)='object'),
        PRIMARY KEY(enrollment_id,horizon_sessions)
    );
    CREATE FUNCTION outcome_v02_observation_validate() RETURNS trigger LANGUAGE plpgsql AS $fn$
    DECLARE e outcome_v02_enrollment; p jsonb; m jsonb; anchor_day date; end_day date;
    BEGIN
        SELECT * INTO e FROM outcome_v02_enrollment WHERE enrollment_id=NEW.enrollment_id;
        p:=NEW.projection::jsonb; m:=p->'manifest'; anchor_day:=(e.projection::jsonb->>'evaluationDate')::date; end_day:=(p->>'horizonMarketDate')::date;
        IF (e.enrollment_recorded_at<=NEW.outcome_known_at AND NEW.recorded_at<=clock_timestamp()
            AND p->>'outcomePolicyId'=NEW.outcome_policy_id AND (p->>'schemaVersion')::int=1
            AND (p->>'enrollmentId')::uuid=NEW.enrollment_id AND (p->>'horizonSessions')::int=NEW.horizon_sessions
            AND (p->>'outcomeKnownAt')::timestamptz=NEW.outcome_known_at AND (p->>'recordedAt')::timestamptz=NEW.recorded_at
            AND p->>'instrumentId'=e.projection::jsonb->>'instrumentId' AND (p->>'anchorMarketDate')::date=anchor_day
            AND (p->>'anchorClose')::numeric=(e.projection::jsonb#>>'{anchor,price,bar,close}')::numeric
            AND end_day>anchor_day AND end_day<='2027-08-24' AND end_day<=(NEW.outcome_known_at AT TIME ZONE 'Asia/Jakarta')::date
            AND p->>'state' IN ('AVAILABLE','ANCHOR_UNAVAILABLE','DATA_UNAVAILABLE','BASIS_UNCERTAIN')
            AND ((p->>'state'='AVAILABLE' AND p->>'reason' IS NULL AND p->>'priceReturnPct' IS NOT NULL AND (p->>'horizonClose')::numeric>0)
                OR (p->>'state'<>'AVAILABLE' AND length(p->>'reason') BETWEEN 1 AND 128 AND p->>'priceReturnPct' IS NULL))
            AND (m->>'schemaVersion')::int=1 AND m->>'outcomePolicyId'=NEW.outcome_policy_id
            AND (m->>'enrollmentId')::uuid=NEW.enrollment_id AND m->>'enrollmentIdentity'=e.enrollment_identity
            AND m->>'enrollmentBindingHash'=e.binding_hash AND (m->>'horizonSessions')::int=NEW.horizon_sessions
            AND (m->>'evaluationCutoff')::timestamptz=NEW.outcome_known_at AND m->>'terminalCondition' IS NOT DISTINCT FROM p->>'reason'
            AND octet_length(m::text)<=65536 AND jsonb_typeof(m->'evidence')='array' AND jsonb_typeof(m->'artifacts')='array'
            AND jsonb_array_length(m->'calendar')=end_day-anchor_day+1
            AND (SELECT count(*) FROM jsonb_array_elements(m->'calendar') d WHERE d->>'classification'='ObservedTrading')=NEW.horizon_sessions
            AND NOT EXISTS(SELECT FROM jsonb_array_elements(m->'calendar') WITH ORDINALITY x(d,n)
                WHERE (d->>'date')::date<>anchor_day+(n::int-1) OR d->>'classification' NOT IN ('ANCHOR','ObservedTrading','Weekend','AnnouncedClosed')
                    OR (n=1 AND d->>'classification'<>'ANCHOR') OR (n>1 AND d->>'classification'='ANCHOR'))) IS NOT TRUE THEN
            RAISE EXCEPTION 'Outcome observation source/session/result integrity conflict';
        END IF;
        RETURN NEW;
    END $fn$;
    CREATE TRIGGER outcome_v02_observation_guard BEFORE INSERT ON outcome_v02_observation FOR EACH ROW EXECUTE FUNCTION outcome_v02_observation_validate();
    CREATE TRIGGER immutable_outcome_v02_observation BEFORE UPDATE OR DELETE ON outcome_v02_observation FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_outcome_v02_observation_truncate BEFORE TRUNCATE ON outcome_v02_observation FOR EACH STATEMENT EXECUTE FUNCTION reject_evidence_mutation();
    INSERT INTO pilot_schema_version VALUES(11);
END IF;
END $migration$;
COMMIT;
