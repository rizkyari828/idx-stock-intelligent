BEGIN;
SELECT pg_advisory_xact_lock(20360928);
DO $migration$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 8) THEN
    IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 7) THEN
        RAISE EXCEPTION 'Typed screener evidence requires schema version 7';
    END IF;
    ALTER TABLE screener_evidence_record
        ADD COLUMN evidence_class text,
        ADD COLUMN payload_schema_version integer,
        ADD COLUMN scope_kind text,
        ADD COLUMN scope_exchange_id uuid;
    ALTER TABLE screener_evidence_record DROP CONSTRAINT screener_evidence_record_claim_check;
    ALTER TABLE screener_evidence_record ADD CONSTRAINT screener_evidence_record_claim_check CHECK
        (claim IN ('StableIdentity','SecurityType','Currency','ListingCoverage','Delisting','BoardRegime',
        'BoardChange','ExchangeRuleVersion','MechanismException','Suspension','Reopening','ScheduledSession',
        'CompletedSession','CorporateAction','SourcePriceConvention','GenuinePriceObservation',
        'PriceComparability','TradingStatus'));
    ALTER TABLE screener_evidence_record ADD CONSTRAINT screener_evidence_binding_check CHECK (
        (evidence_class IS NULL AND payload_schema_version IS NULL AND scope_kind IS NULL AND scope_exchange_id IS NULL)
        OR (evidence_class IS NOT NULL AND payload_schema_version IS NOT NULL AND scope_kind IS NOT NULL
            AND scope_exchange_id IS NOT NULL
            AND evidence_class IN ('CONTINUING_EFFECTIVE_STATE','POINT_OBSERVATION','VERSIONED_RULE',
                'SESSION_FACT','SOURCE_CONVENTION')
            AND payload_schema_version > 0 AND scope_kind IN ('INSTRUMENT','EXCHANGE')
            AND scope_exchange_id <> '00000000-0000-0000-0000-000000000000'
            AND (scope_kind <> 'EXCHANGE' OR subject_id = scope_exchange_id)));
    INSERT INTO pilot_schema_version VALUES (8);
END IF;
END $migration$;
COMMIT;
