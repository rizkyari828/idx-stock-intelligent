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
is local only and uses same-origin JSON writes with a 64 KiB body limit (24 MiB HTTP envelope only for portfolio imports; decoded file content is capped at 8 MiB). It has no authentication or
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
| GET | /api/portfolios/{id}/export?format=JSON | One lossless portfolio archive; JSON default |
| GET | /api/portfolios/{id}/export?format=CSV | Generic transactions only; refuses correction history |
| POST | /api/portfolio-imports/preview | Validate JSON/CSV, read-only preview |
| POST | /api/portfolio-imports | Revalidate and atomically commit explicit import |

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

## Portfolio exchange V0.1

Use **Export Portfolio (lossless JSON)** for backup and restore. Save the file
privately, outside Git; `data/portfolio/` and `data/broker/` are ignored. Export
contains `schemaVersion: 1`, the original `portfolio` header/settings,
`events`, `theses`, and exactly the traded `instruments` and their effective
symbol histories. It preserves stable IDs, trade dates, UTC recorded timestamps,
SHARES, amounts, fees, sources, external references, notes, correction links,
thesis versions/mandates, active status and invalidation metadata. The existing
ledger does not retain original LOTS input: 13 LOTS exports as 1,300 SHARES.
No market history, raw provider artifacts, credentials or connection settings
are exported. Outside-universe identities travel with the archive.

Ordering is deterministic: events by ledger order, theses by instrument/version,
references by UUID and symbol history by effective start/symbol. Exported `order`
is the per-portfolio ordinal 1..N, preserving economic tie order; the database's
global identity sequence is storage-local and is regenerated. No volatile
export timestamp is included. Recorded timestamps must have exact PostgreSQL
microsecond precision; imports reject higher precision rather than rounding.
Version 1 requires its defined fields and rejects unknown/duplicate JSON fields,
missing records, numeric enum values and future schema versions. Add a single
explicit version conversion if a real V2 is needed; no migration framework exists.

JSON mode **CREATE_NEW** requires the original portfolio UUID to be absent.
It creates that identity without cloning or remapping. **RESTORE_EXISTING_EMPTY**
requires an existing original header with identical name/settings/creation time
and no events/theses. A complete exact match returns `ALREADY_PRESENT` in either
mode. Changed facts, reused event/thesis IDs, occupied targets and differing
instrument metadata return `CONFLICT`; nothing is overwritten or merged.
Missing instrument references can be inserted, but existing canonical identities
and symbol histories are never changed by import. Overlapping symbol identities
are rejected. The restore creates no bars or prices: unpriced holdings stay
`NO_CURRENT_MARKET_PRICE`, with unavailable valuation totals.

In the UI: select JSON or CSV → select a UTF-8 file → **Preview** → review the
summary/normalized rows → **Confirm Import**. File selection and preview write
nothing. Editing the format/mode/file or opening a different portfolio clears
confirmation. Commit validates again under the same portfolio lock used by
ledger writes and uses one transaction for header, references, events and theses.
Concurrent identical imports yield one `IMPORTED` and subsequent
`ALREADY_PRESENT` results. Failures roll back the entire batch.

Both POST routes take a JSON envelope, never a path or uploaded filename:

```json
{
  "format": "JSON",
  "content": "{\"schemaVersion\":1,...}",
  "mode": "CREATE_NEW",
  "portfolioId": null
}
```

`content` is the file text, including its complete document (the ellipsis above
is illustrative). JSON identity comes from the header; optional `portfolioId`
must match it. Results use `IMPORTED`, `ALREADY_PRESENT`, `CONFLICT` or `INVALID`;
preview additionally uses `READY` and `canImport`. Malformed outer HTTP envelopes
return 400, unavailable services 503. A preview reports rows read, valid/invalid
rows, duplicates, unknown instruments, row errors, normalized SHARES and estimated
resulting ledger events. Invalid/conflicting content never becomes a partial import.

### Generic CSV transactions V0.1

CSV is a convenience onboarding format, not a broker export or lossless backup.
Header order is exact; all eleven columns are required:

```csv
trade_date,type,instrument_id,symbol,quantity,unit,price,fees,cash_amount,external_reference,note
2026-09-01,CASH_DEPOSIT,,,0,SHARES,0,0,1000000,example-deposit,Illustrative only
2026-09-10,BUY,81808734-9a0d-5fa6-aa94-ea723dcb414f,,13,LOTS,100,1300,0,example-buy,Illustrative only
```

Types: BUY, SELL, CASH_DEPOSIT, CASH_WITHDRAWAL. Trade rows need a registered
stable ID or uniquely resolving effective symbol on the trade date. If both are
supplied they must agree. Cash rows require blank instrument/symbol and explicit
SHARES with zero quantity/price; use `cash_amount` for the deposit/withdrawal.
All numeric columns are plain nonnegative invariant decimals; use 0 for
inapplicable values. Quantity/unit is explicit LOTS or SHARES; 1 LOT = 100 SHARES,
and resulting shares must be integral. Quoted commas, escaped quotes and multiline
notes are supported. Unknown/ambiguous instruments, malformed records and missing
units are row errors. Blank notes are allowed.

Every row requires a nonblank `external_reference`. CSV source is `GENERIC_CSV`;
event UUIDs derive deterministically from portfolio UUID/source/reference.
Repeated identical facts are duplicates; changed content for the same reference
is a conflict. Recorded times are assigned at commit and never backdated to the
trade date. CSV uses the existing accounting/correction model, with no second
engine. Its envelope uses `format: "CSV"`, `mode: "CREATE_NEW"` and the open
existing `portfolioId`: append only new deterministic transaction identities.
`RESTORE_EXISTING_EMPTY` is JSON-only. CSV cannot restore knowledge/thesis history.

CSV exports canonical SHARES and quote every value; potentially executable
spreadsheet strings beginning with `=`, `+`, `-`, `@` (also after whitespace) or
leading tab/CR/LF receive an apostrophe. This intentionally changes affected
notes/references. CSV omits original source, IDs/timestamps and thesis metadata;
reimport is not a substitute for JSON restoration. Export refuses any correction
history rather than flattening it silently. Use JSON for exact recovery.

### Historical reconstruction and limits

`through=YYYY-MM-DD` selects economic trade dates; `cutoff=ISO8601` selects only
facts already recorded. For a Sep 20 trade recorded Sep 22, cutoff Sep 21 excludes
it even if through includes Sep 20. Before a correction's recorded time the
original fact remains visible; afterwards the immutable superseding fact applies.
Theses and mandate changes use the same knowledge cutoff. An inactive latest
version leaves no active thesis; prior versions remain queryable. Import validates
each recorded boundary, preventing later backdated events from concealing an
invalid historical balance. Equal-timestamp records form one atomic boundary.

Limits fail explicitly, never truncate: 8 MiB UTF-8 file content, 24 MiB HTTP JSON
envelope, 10,000 events/CSV records/resulting ledger events, 2,000 thesis records,
1,000 instrument references/CSV registry lookup, 10,000 symbol-history records,
200 open holdings, JSON depth 32. Amount/field bounds are the existing ledger
contract. Validation honors cancellation and parameterized SQL; server filenames,
paths, shell commands and SQL are not accepted from files/HTTP. Prefix replay is
O(n²) within the 10,000-event ceiling; large archives can be slow. Add checkpoints
only if this bounded personal workflow actually requires them. No arbitrary merge,
broker-specific parser, authentication or public hosting is included.

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
python3 -m unittest discover -s scripts -p test_portfolio_exchange.py -v
# Includes the original two product regression tests plus CSV, clean restore and HTTP security/bounds.
# The original suite alone remains available as test_product_slice.py.
# Existing archived pilot restore/replay check, zero provider calls:
python3 scripts/check_pilot_restore.py
```

The local environment for this implementation selected old Python/Node by default.
Validation used installed `python3.13`; for Node it used a temporary runtime:
`npm exec --package=node@22 -- npm test` and the equivalent `npm run build`.
Acceptance can set `IDX_TEST_NODE` to the supported Node executable. These
runtime paths and secrets are not embedded in source or fixture data.

## Initial slice acceptance — 2026-10-01

.NET build: zero warnings/errors; standard `dotnet test`: **34/34** passed.
Python standard discovery with the opt-in database fingerprint: **61/61** passed.
Frontend: TypeScript check + Vite production build and **1/1** native Node test
passed. PostgreSQL/HTTP/React acceptance: **2/2** passed. Existing offline pilot
restore/replay: **14** groups passed. Migration 0004 was applied and reapplied in
the disposable database; immutable triggers and concurrent import idempotency
were exercised. Production canonical history and authoritative operation ledger
fingerprints remained unchanged. EODHD billable units **0**; soak **1/10**;
FullIdx **DISABLED**. The API was stopped and synthetic databases dropped after
acceptance. This describes the initial slice acceptance; migration 0004 was subsequently applied to the operational database during the separately verified activation milestone.

Known V0.1 limits: IDR equities, four event types, no FX/short/corporate-action
accounting, 10,000 ledger events, 200 open holdings, bounded per-holding market
reads, no technical-feature projection, effective-only symbol metadata, and UI
verification by production build plus real-component rendering rather than an
interactive browser test. No authentication or public hosting is included.
## Verified exchange result — 2026-10-01

.NET standard discovery: **40/40**; frontend native tests: **2/2**, TypeScript and
production build passed. Python offline discovery with database fingerprint:
**61/61**. Combined disposable PostgreSQL/HTTP acceptance: **5/5** (the two
original regressions plus CSV, full JSON restore and HTTP security/bounds). Archived pilot restore:
**14** groups, zero provider requests; generated check summaries stay inside the
owned test directory, outside the operational root ledger.

Clean restore reproduces five events, three thesis versions (including
inactivation), original identities and correction links, cash **886,350**, pilot
**1,000 SHARES / average cost 111 / realized P&L 2,400**, and outside-universe
**100 SHARES / average cost 50.5** with no price. **17** independent historical
queries and thesis cutoff histories compare exactly before/after restore.
Preview, invalid imports, metadata/identity conflicts, repeated imports,
four concurrent retries and forced mid-transaction failure were exercised.
No generated exports or personal transactions are committed.

Operational canonical revisions remain **76** with unchanged canonical/security
master/ledger fingerprints. One pre-existing empty portfolio was observed and
preserved; operational events/theses remain **0/0**. EODHD units **0**, soak
**1/10**, FullIdx **DISABLED**. UI verification is build, confirmation logic and
actual holdings-component rendering; no interactive browser acceptance is claimed.
Ready for bounded generic CSV onboarding or canonical JSON restore on loopback.
Next milestone: small thesis/transaction editing improvements and private first
portfolio reconciliation; broker-specific formats need an actual documented
sample. Use **MEDIUM** reasoning.
