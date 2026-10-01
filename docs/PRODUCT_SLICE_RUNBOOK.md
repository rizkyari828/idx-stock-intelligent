# Portfolio API/UI vertical slice — local runbook

Requires .NET 10, PostgreSQL 17 (existing compose service), Python 3.12+ for
acceptance and Node 22.12+ for current Vite. Run from repository root unless
indicated. No provider token is needed. Keep connection strings outside Git.

## Schema and startup

Apply migrations using the existing local CLI, once the target database exists.
For a fresh database apply 0001, 0002, 0003, then 0004. For the existing pilot
database apply 0004 only; 0001 is not rerunnable. 0004 checks schema version and
uses the existing migration lock. The API checks no migrations at request time.

```bash
# Explicit target; synthetic fixtures must never be loaded into this database.
export IDX_PRODUCT_DATABASE=idx_stock_intelligence
# Existing initialized database:
docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U idx_stock \
  -d "$IDX_PRODUCT_DATABASE" < src/IdxStockIntelligence.Infrastructure/Migrations/0004_portfolio.sql

cd frontend
npm ci
npm test
npm run build
cd ..

# Set this privately using your local PostgreSQL password; never commit it.
# IDX_DATABASE_CONNECTION='Host=127.0.0.1;Port=5432;Database=...;Username=idx_stock;Password=...'
export IDX_DATABASE_CONNECTION
export IDX_UI_ROOT="$PWD/frontend/dist"
dotnet run --project src/IdxStockIntelligence.Api
```

Open http://127.0.0.1:5080. For frontend development run `npm run dev` in
`frontend` and open the Vite loopback URL; `/api` proxies to 5080. The application
is local only and uses same-origin JSON writes with a 64 KiB body limit. It has no authentication or
remote deployment support. CLI ingestion keeps its Docker/psql mechanism; the
API uses pooled Npgsql with positional parameters, cancellation and 15s commands, following
the [Npgsql driver guidance](https://www.npgsql.org/doc/basic-usage.html).

Create a named portfolio (negative cash false by default), deposit cash, register
or select an EQUITY by its stable ID, then record a BUY/SELL. Existing pilot
instruments may lack symbol/type metadata: use their existing UUID from the
security master/pilot reference when registering. New outside-universe equities
need no collector changes. Registry is paginated; UI reads available pages.
The schema preserves UNKNOWN rather than guessing every existing instrument is
equity. Use SHARES or LOTS; a successful response always carries SHARES.
Click a holding for costs, price provenance, feature states and thesis history.
Appending a mandate/thesis explicitly creates a version. Event history shows IDs
for correction entry; supply the ID as `supersedes` and a new import reference.

## API

All routes return product contracts; enum values use explicit uppercase names.
Money accepts JSON decimal numbers or decimal strings (UI sends strings).

| Method | Route | Result |
|---|---|---|
| POST | /api/portfolios | Create account; name, allowNegativeCash=false |
| GET | /api/portfolios/{id} | Cash, open holdings, subtotal/nullable totals, coverage, date/cutoff |
| POST | /api/portfolios/{id}/events | Append or canonical duplicate; event + duplicate flag |
| GET | /api/portfolios/{id}/events | Immutable history ordered trade date, ledger sequence, UUID |
| GET | /api/portfolios/{id}/holdings | Bounded holdings page plus whole-account coverage |
| POST | /api/portfolios/{id}/holdings/{instrumentId}/theses | Append thesis version |
| GET | /api/portfolios/{id}/holdings/{instrumentId}/theses | History, descending version |
| GET | /api/instruments/{id}/market-state | Latest canonical market state as-of |
| POST | /api/instruments | Register stable identity/display metadata; no provider dependency |
| GET | /api/instruments | Bounded registry ordered symbol then stable ID |

Reads accept `through=YYYY-MM-DD` (portfolio/holdings/market state) and
`cutoff=ISO8601` (also histories), default Jakarta today/UTC now. Both reject future
values and dates before 1900. History queries filter by cutoff; no open-ended
provider scan. Pages use offset 0..10000, limit 1..200 (default 100), explicit
ordering. V0.1 replay is bounded to 10,000 events and enrichment to 200 open
holdings; exceeding these returns an explicit error, never truncated balances.
JSON errors distinguish FAILED execution from data quality; invalid input 400,
missing identity 404, database identity collision 409, unavailable service 503.

Example trade body (use your account's registered stable instrument ID):

```json
{
  "eventId": "99999999-9999-4999-8999-999999999999",
  "type": "BUY", "instrumentId": "81808734-9a0d-5fa6-aa94-ea723dcb414f",
  "tradeDate": "2026-09-30", "quantity": "13", "unit": "LOTS",
  "price": "100", "fees": "1300", "externalReference": "synthetic-example-only",
  "source": "USER", "note": "Illustrative only"
}
```

## Reproduce synthetic acceptance

The fixture has one pilot-ID holding with a synthetic degraded stale price and
one outside-universe holding with no price. It covers share/lot buys, partial
sale, fees, import retry, correction, as-of reads, mandate history, cash, incomplete
coverage, concurrent retry and atomic invalid-event rejection. It renders the
actual React holdings component from the HTTP response. No browser automation
is claimed. Synthetic price provenance lives only in a generated disposable
`idx_product_test_*` database, dropped on cleanup. No user's holdings are included.
It starts its API on an available loopback port, verifies an empty registry, and
fingerprints production canonical history and operation ledgers before/after.

```bash
dotnet build IdxStockIntelligence.slnx
dotnet test
PYTHONPATH=collectors/python/src python3 -m unittest discover -s collectors/python/tests -v
# Acceptance uses its own loopback port; requires frontend npm test/build above.
python3 -m unittest discover -s scripts -p test_product_slice.py -v
# Existing archived pilot restore/replay check, zero provider calls:
python3 scripts/check_pilot_restore.py
```

The local environment for this implementation selected old Python/Node by default.
Validation used installed `python3.13`; for Node it used a temporary runtime:
`npm exec --package=node@22 -- npm test` and the equivalent `npm run build`.
Acceptance can set `IDX_TEST_NODE` to the supported Node executable. These
runtime paths and secrets are not embedded in source or fixture data.

## Verified implementation result — 2026-10-01

.NET build: zero warnings/errors; standard `dotnet test`: **34/34** passed.
Python standard discovery with the opt-in database fingerprint: **61/61** passed.
Frontend: TypeScript check + Vite production build and **1/1** native Node test
passed. PostgreSQL/HTTP/React acceptance: **2/2** passed. Existing offline pilot
restore/replay: **14** groups passed. Migration 0004 was applied and reapplied in
the disposable database; immutable triggers and concurrent import idempotency
were exercised. Production canonical history and authoritative operation ledger
fingerprints remained unchanged. EODHD billable units **0**; soak **1/10**;
FullIdx **DISABLED**. The API was stopped and synthetic databases dropped after
acceptance. The product migration has not been applied to the operational database.

Known V0.1 limits: IDR equities, four event types, no FX/short/corporate-action
accounting, 10,000 ledger events, 200 open holdings, bounded per-holding market
reads, no technical-feature projection, effective-only symbol metadata, and UI
verification by production build plus real-component rendering rather than an
interactive browser test. No authentication or public hosting is included.
Next milestone: bounded broker-record import/export and portfolio restore/as-of
acceptance, then improve thesis/transaction editing ergonomics; descriptive
screener comes later. Use MEDIUM reasoning for the next product milestone.
