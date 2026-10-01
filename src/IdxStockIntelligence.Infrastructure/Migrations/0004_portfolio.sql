BEGIN;
SELECT pg_advisory_xact_lock(20360928);
DO $$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pilot_schema_version WHERE version = 4) THEN
    ALTER TABLE instrument ADD COLUMN instrument_type text NOT NULL DEFAULT 'UNKNOWN'
        CHECK (instrument_type IN ('EQUITY','INDEX','UNKNOWN'));
    CREATE TABLE portfolio (
        portfolio_id uuid PRIMARY KEY,
        name text NOT NULL CHECK (length(trim(name)) BETWEEN 1 AND 200),
        allow_negative_cash boolean NOT NULL DEFAULT false,
        created_at timestamptz NOT NULL DEFAULT clock_timestamp()
    );
    CREATE TABLE portfolio_event (
        event_id uuid PRIMARY KEY,
        portfolio_id uuid NOT NULL REFERENCES portfolio,
        instrument_id uuid REFERENCES instrument,
        event_order bigint GENERATED ALWAYS AS IDENTITY UNIQUE,
        event_type text NOT NULL CHECK (event_type IN ('BUY','SELL','CASH_DEPOSIT','CASH_WITHDRAWAL')),
        trade_date date NOT NULL,
        known_at timestamptz NOT NULL,
        quantity numeric NOT NULL CHECK (quantity >= 0 AND quantity = trunc(quantity) AND quantity <= 1000000000),
        quantity_unit text NOT NULL CHECK (quantity_unit = 'SHARES'),
        price numeric NOT NULL CHECK (price BETWEEN 0 AND 1000000000000),
        fees numeric NOT NULL CHECK (fees BETWEEN 0 AND 1000000000000),
        cash_amount numeric NOT NULL CHECK (cash_amount BETWEEN 0 AND 1000000000000),
        external_reference text CHECK (length(trim(external_reference)) BETWEEN 1 AND 200),
        source text NOT NULL CHECK (length(trim(source)) BETWEEN 1 AND 100),
        note text CHECK (length(note) <= 4000),
        supersedes uuid UNIQUE,
        UNIQUE (portfolio_id, event_id),
        FOREIGN KEY (portfolio_id, supersedes) REFERENCES portfolio_event(portfolio_id,event_id),
        CHECK (supersedes IS NULL OR supersedes <> event_id),
        CHECK ((event_type IN ('BUY','SELL') AND instrument_id IS NOT NULL AND quantity > 0 AND price > 0 AND cash_amount = 0)
            OR (event_type IN ('CASH_DEPOSIT','CASH_WITHDRAWAL') AND instrument_id IS NULL AND quantity = 0 AND price = 0 AND cash_amount > 0)),
        CHECK (event_type <> 'CASH_DEPOSIT' OR fees <= cash_amount)
    );
    CREATE UNIQUE INDEX portfolio_event_import_idx ON portfolio_event(portfolio_id,source,external_reference)
        WHERE external_reference IS NOT NULL;
    CREATE INDEX portfolio_event_history_idx ON portfolio_event(portfolio_id,known_at,event_order);
    CREATE TABLE thesis_version (
        thesis_id uuid PRIMARY KEY,
        portfolio_id uuid NOT NULL REFERENCES portfolio,
        instrument_id uuid NOT NULL REFERENCES instrument,
        version integer NOT NULL CHECK (version > 0),
        mandate text NOT NULL CHECK (mandate IN ('INVEST','FAST_SWING','LONG_SWING')),
        thesis_text text NOT NULL CHECK (length(trim(thesis_text)) BETWEEN 1 AND 8000),
        known_at timestamptz NOT NULL,
        supersedes uuid UNIQUE REFERENCES thesis_version,
        invalidation_note text CHECK (length(invalidation_note) <= 4000),
        active boolean NOT NULL,
        UNIQUE (portfolio_id,instrument_id,version)
    );
    CREATE INDEX thesis_version_history_idx ON thesis_version(portfolio_id,instrument_id,known_at DESC,version DESC);
    CREATE TRIGGER immutable_portfolio BEFORE UPDATE OR DELETE ON portfolio
        FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_portfolio_event BEFORE UPDATE OR DELETE ON portfolio_event
        FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    CREATE TRIGGER immutable_thesis BEFORE UPDATE OR DELETE ON thesis_version
        FOR EACH ROW EXECUTE FUNCTION reject_evidence_mutation();
    INSERT INTO pilot_schema_version VALUES (4);
END IF;
END $$;
COMMIT;
