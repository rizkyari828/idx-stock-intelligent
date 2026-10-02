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
Click a holding for costs, price provenance and feature states. Use the Thesis
journal for current mandate and immutable version history. Transactions provides
a Correct action that prefills a new event and supplies `supersedes`; the original
is preserved. Use a new external reference for each correction.

## API

All routes return product contracts; enum values use explicit uppercase names.
Money accepts JSON decimal numbers or decimal strings (UI sends strings).

| Method | Route | Result |
|---|---|---|
| POST | /api/portfolios | Create account; name, allowNegativeCash=false |
| GET | /api/portfolios/{id} | Cash, open holdings, subtotal/nullable totals, coverage, date/cutoff |
| POST | /api/portfolios/{id}/events | Append or canonical duplicate; event + duplicate flag |
| GET | /api/portfolios/{id}/events | Immutable history plus cutoff-aware `correctedBy`, ordered trade date, ledger sequence, UUID |
| POST | /api/portfolios/{id}/reconciliation/preview | Read-only decimal comparison of expected snapshot against ledger replay |
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


## Production portfolio milestone — 2026-10-01

The approved `design/portfolio-prototype/` remains a standalone reference. The
production React app now owns its own tokens/styles, shared primitives and real
API state. It has no runtime dependency on the prototype and contains no fixture
ledger or comparison arithmetic. Future research navigation stays disabled.

Audit before implementation: the existing `main.tsx`, `PortfolioTable`,
`ImportPanel` and `view.ts` already supported portfolio selection/creation,
registration, appending events/theses and JSON/CSV exchange. `PortfolioDatabase`
already provided pooled cancellable SQL, replay bounds, per-portfolio write
locks and historical enrichment; `PortfolioLedger` owned accounting and
correction semantics. These paths are reused. The only new route is reconciliation
preview. No database migration, persistence table, accounting engine, dependency
or authentication was added.

### Reconciliation V0.1

`POST /api/portfolios/{portfolioId}/reconciliation/preview` accepts a JSON body:

```json
{
  "through": "2026-09-30",
  "cutoff": "2026-10-01T02:00:00Z",
  "expectedCash": "1000000",
  "holdings": [
    { "instrumentId": null, "symbol": "ANTM", "shares": "1300", "averageCost": "3450" }
  ]
}
```

Dates are required and follow existing `ProductQuery` validation: through is an
economic/trade-date boundary (1900..Jakarta today), cutoff is an independent
recording-time boundary (1900..now). The UI labels cutoff WIB/UTC+7 and sends UTC.
A blank context cutoff means now at request time; Now changes only the knowledge
cutoff and preserves the selected through date; the response's exact cutoff
remains visible. Context edits clear reconciliation results and require Apply
before a new comparison. Snapshot edits cancel outstanding comparison requests
and clear their results. Snapshots are not stored.

Stable ID or effective symbol is required per holding. Symbols normalize trim/
uppercase and resolve using `instrument_history` effective on through; this
matches the existing effective-only security-master model, which is not a
bitemporal registry. Both fields, if supplied, must resolve to the same EQUITY.
Ambiguous symbols, mismatched identities and duplicate resolved identities are
400 errors. Unknown single IDs/symbols remain `UNKNOWN_INSTRUMENT` result rows.
Maximum 200 expected holdings; positive integral shares <=10^9; average cost
nonnegative <=10^12 or null (unknown); expected cash within ±10^12. Negative
expected cash is supported for the existing negative-cash account setting.

Replay reads at most 10,000 events and retains the existing 200-open-holding
ceiling. The preview runs in one repeatable-read, **READ ONLY** transaction,
uses positional SQL parameters and cancellation tokens, and writes no ledger,
correction, thesis, snapshot or operation records. It never reads prices for
comparison. Portfolio absence at cutoff is a 404. Other errors use the existing
400/503 conventions and request body limit of 64 KiB.

Response fields: `through`, `knowledgeCutoff`, `status`, `ledgerCash`,
`expectedCash`, `cashDifference`, `expectedHoldings`, `ledgerHoldings`, `matched`,
`review`, `missing`, `unknown`, `rows`. Every row carries stable identity/display
symbol, ledger/expected shares, share difference, ledger/expected average cost,
average-cost difference and status. Ordering is symbol then identity. Differences
are **ledger minus expected**. Missing-side values and differences are null,
never zero. Decimal comparisons do not round or use a tolerance.

| Status | Meaning |
| --- | --- |
| MATCH | Same stable identity, exact shares and known equal average cost. Overall MATCH also requires exact cash and no nonmatching rows. |
| REVIEW | Share or average-cost difference, or unknown expected average cost. Overall REVIEW includes cash and all nonmatching states. |
| MISSING_FROM_LEDGER | Resolved expected equity has no open ledger position. |
| MISSING_FROM_EXPECTED | Open ledger position is absent from the snapshot. |
| UNKNOWN_INSTRUMENT | Single supplied identity/symbol does not resolve to an equity. |

`review` counts all non-MATCH rows, including missing/unknown; `missing` counts
both missing-source statuses, and `unknown` counts unresolved rows. Price quality
and availability never affect these states. The UI uses named badges, distinct
filled/outlined missing statuses, numeric alignment, quiet zeros, visible
nonzero differences and All/Needs review filters. No repair action exists.

### Corrections and thesis versions

Transactions shows cutoff-aware ACTIVE/SUPERSEDED labels and inspectable event
IDs, external references and lineage. `correctedBy` is additive history metadata,
resolved against all events at cutoff even when a child is on another page.
It is not part of the portable JSON archive. Correct opens a native dialog with
immutable original ID, recorded time, instrument and lineage plus prefilled
economic fields. Confirm submits the existing events endpoint with a new UUID,
new external reference and `supersedes`. Cancel/Escape closes without writes.
The backend revalidates current correction eligibility under its existing lock;
it never updates/deletes an original. History before correction recording still
shows and reconstructs the original. History after recording uses its successor.

Thesis journal shows current version, mandate, active/inactive state, time, text,
invalidation note and disclosures for prior immutable versions. Create New
Version sends the existing thesis endpoint with `active: true`. Invalidate
requires an explicit reason and appends `active: false`, preserving current
text/mandate. Neither changes quantities. Historical cutoff reads still use the
backend history. Pages retain the existing 100-row paging and bounds. Journal
selection currently covers open holdings; closed-position journal browsing is
not added. A fixed historical cutoff remains fixed after a write; use Now to
inspect newly recorded facts.

### Valuation and exchange UI

The .NET portfolio projection now exposes open-position `investedCost`,
whole-ledger `realizedPnl` (including closed positions), nullable complete
`unrealizedPnl` and nullable per-holding `unrealizedPercent`. These are derived
from the existing replay/valuation, not new accounting. An unrepresentable
percentage returns null without breaking valuation. Complete portfolio/equity
and unrealized totals remain unavailable when pricing is incomplete. Priced
subtotal, coverage and every unpriced holding stay visible, with original source
dates/provenance. Formatting is display-only; input decimals remain strings.

Exchange keeps the original contracts and atomic backend revalidation. The
styled stepper/file/preview/review/Confirm flow distinguishes canonical JSON
from generic CSV onboarding. Confirm sends the **exact successful preview
payload**, never rebuilt current inputs. File/format/mode/portfolio changes clear
that payload, cancel obsolete requests and discard late responses. Invalid and
conflicting previews disable confirmation. UTF-8 and file-size checks remain
local; accounting, identity and import validation remain authoritative in .NET.
Exports are real downloads through existing routes; CSV correction-history
refusal remains unchanged. No file path is submitted or returned.

### Browser acceptance and shared UI

Production primitives in `frontend/src/ui.tsx`: icon, badge/status badge, page
header, notice, metric, instrument cell, loading/empty state and native dialog.
Feature components retain practical table/timeline/stepper markup. The shell
uses the reference navigation, surface hierarchy, semantic dark/light tokens,
spacing/radii, compact fields and tabular financial numerals. There is no generic
component framework.

The browser check exercises the actual built React application and disposable
PostgreSQL API, including MATCH/REVIEW, input/result invalidation, identity errors,
immutable correction submit/cancel, historical replay, thesis version/invalidation,
valid/invalid JSON and CSV preview gates, exact import payload and file/format/
mode/portfolio invalidation. It checks all production surfaces at 1440, 1366,
1280, 1024, 768 and 390 px; tables own horizontal scrolling, reconciliation keeps
symbols visible, navigation becomes a rail/menu, and dialogs stay within bounds.
Keyboard Tab wraps within dialogs, Escape/Cancel restore focus, controls retain
visible focus, tables remain semantic and reduced motion is respected. Status
text/icons accompany colors. Screenshots are saved outside the repository and
were visually inspected. This is a targeted Chrome pass; Safari/Firefox and a
screen-reader audit remain unverified.

To include this check in standard disposable acceptance:

```bash
# Chrome is installed at the standard macOS path by default; override elsewhere.
export IDX_TEST_BROWSER=1
# Optional: IDX_TEST_CHROME=/path/to/chrome
# Optional: IDX_TEST_NODE=/path/to/node-22
# Optional: IDX_TEST_UI_OUTPUT=/tmp/idx-product-ui-previews
python3 -m unittest discover -s scripts -p test_portfolio_exchange.py -v
```

The harness starts/stops its own isolated headless Chrome/profile and disposable
API/database. It fingerprints operational security-master and portfolio tables
as well as canonical revisions and authoritative operation files. Never point
`check_product_ui.mjs` at the operational API: it deliberately creates synthetic
corrections/theses/imports in the owned fixture.

Remaining scope limits are unchanged: local IDR equity accounting, bounded
replay/imports, no corporate actions/FX/shorts, no broker-specific parsing, no
reconciliation persistence, no public hosting/auth and no Screener/AI scoring.


### Executed verification — 2026-10-01

| Check | Actual result |
| --- | --- |
| `dotnet build --no-restore` | PASS, zero warnings/errors |
| Standard `dotnet test --no-restore` | 50/50 passed, zero skipped |
| Python standard discovery with `IDX_EXPERIMENT_VERIFY_DB=1` | 61/61 passed |
| Frontend `npm test` | 3/3 native Node tests passed |
| Frontend `npm run build` | TypeScript + Vite production build passed |
| Standard disposable exchange acceptance with `IDX_TEST_BROWSER=1` | 7/7 passed: original 5 regressions, read-only reconciliation, real production React/Chrome |
| Final targeted browser reruns after visual/state refinements | PASS at all six widths; no external requests/runtime exceptions |
| Existing offline `check_pilot_restore.py` | 14 groups passed, zero provider requests |
| Operational loopback smoke | Existing account's actual snapshot returns MATCH; no synthetic writes |

Operational API was restarted with the built milestone at
http://127.0.0.1:5080. Before/after fingerprints of canonical revisions,
instruments, effective symbol history, portfolio headers, events and theses
are identical. Counts remain 76 canonical revisions, 11 instrument identities,
zero symbol-history rows, one pre-existing portfolio, zero events and zero
theses. Authoritative operation/soak files remain unchanged under acceptance.
EODHD billable units 0; soak 1/10; FullIdx DISABLED. Disposable fixture databases
and their isolated Chrome profiles are cleaned up. No market datasets, personal
exports or screenshots are added to Git.

The workspace includes implementation and tests plus the preserved approved
prototype. No commit or push was performed for this milestone. Next: owner-led
private reconciliation against an actual broker snapshot and focused usability
feedback. Screener remains a separate, unstarted milestone.


## Reconciliation Snapshot CSV

On Reconcile, **Upload Snapshot CSV** reads a current broker holdings snapshot
entirely in the browser. Review validated rows and consolidated holdings, then
**Apply Snapshot** to replace the expected holdings editor. Manual edits remain
available. Press **Compare Portfolio** separately to use the existing read-only
`POST /api/portfolios/{portfolioId}/reconciliation/preview` endpoint. Selecting
another file, applying or editing clears previous results; Cancel retains the
current editor. Pending local previews must be applied or cancelled before comparison.

This is not transaction import: no CSV content, filename or local path is uploaded,
and no ledger events, thesis versions or broker snapshots are written. No backend
contract or schema changes are needed.

Supported headers (any order, no duplicate or unknown headers):
`source_account,symbol,lots,shares,average_cost,mandate`. `symbol` and at least one
of `shares`/`lots` must appear. Optional columns may be omitted. Symbols are trimmed,
uppercased and limited to 50 characters. Shares are positive whole numbers up to
1,000,000,000. Optional lots are positive whole numbers; one lot is 100 shares.
When both are present they must agree. Average cost is a nonnegative plain decimal
up to 1e12; blank means unknown/null. Decimal inputs support at most 28 digits and
28 fractional places; repeating weighted averages round to 28 significant digits.
Scientific notation and localized number separators are not supported.

`source_account` and `mandate` are preview metadata only. Mandate accepts
FAST_SWING, LONG_SWING or INVEST; unknown values block Apply. These values never
change ledger identity or mandate/thesis history; manage thesis separately in
Thesis journal. Different informational mandates can coexist in a consolidated group.

Duplicate normalized symbols consolidate by summing shares (also capped at
1,000,000,000). If every contributing row supplies average cost, the expected cost
is the share-weighted mean; otherwise it is null. Preview lists contributing CSV
row numbers, accounts and mandates and counts duplicate groups. Stable identity,
unknown symbols and ambiguous effective aliases remain authoritative .NET checks
at Compare; distinct aliases are not merged locally.

Expected cash stays explicit and unchanged; holdings never imply a cash balance.
Limits: UTF-8 only (optional BOM), 1 MiB per file, at most 200 resulting holdings.
Comma-delimited quoted fields, doubled quotes, embedded newlines and CRLF/LF are
supported. Malformed CSV, wrong headers and invalid rows block Apply with row
numbers. Blank data rows are invalid; a final line ending is allowed.

Verification: frontend snapshot contract tests in `npm test`; the opt-in real
browser regression (`IDX_TEST_BROWSER=1` with standard Python acceptance discovery)
checks replacement/editing, invalidation, unchanged cash, local-only parsing and
Apply, exact reconciliation request, unchanged exported ledger/thesis history,
no transaction import calls, and desktop/mobile preview layout.


Snapshot enhancement regression result (2026-10-01): 8 frontend tests and Node 22
production build passed; 50 .NET tests passed; Python discovery passed 60 tests
with 1 opt-in evidence test skipped; 7 disposable HTTP/PostgreSQL/browser tests
passed; 14 offline restore groups passed with zero provider requests. Snapshot
preview screenshots at 1440 and 390 px were inspected. Existing browser checks
also covered 1366, 1280, 1024 and 768 px. Production database and operational ledger
fingerprints remained unchanged during disposable acceptance.


## Reconciliation Draft

Reconcile automatically saves applied snapshot inputs and later cash/holding edits
in browser `localStorage`, scoped to `idx.reconcile.draft.v1:{portfolioId}`.
The versioned schema contains `version: 1`, `portfolioId`, `expectedCash`,
`holdings`, `through` and `cutoff` (the applied context). Each holding contains
`instrumentId`, `symbol`, `shares`, `averageCost`, plus optional source-account and
mandate lists for display only. Unapplied CSV previews are not saved. Blank cutoff
continues to mean now; applied context is restored at startup/portfolio switch.

No reconciliation result, ledger/market state, backend balance, file contents,
filename, filesystem path or secret is saved. Refresh or navigating away/back
restores the selected portfolio's inputs; **Compare Portfolio** always fetches a
new result from .NET. Cash and holdings remain editable. Malformed/unsupported
or invalid-date drafts are ignored; unavailable/full browser storage reports a
save error while retaining current inputs. Local draft storage is bounded to
1 MiB and 200 holdings. Symbol/identity edits discard associated CSV metadata.

**Clear Draft** removes only the selected portfolio's key, clears expected
cash/holdings, local file preview and current comparison result, and leaves the
applied date/cutoff and other portfolios' drafts alone. It does not touch the
ledger or registry. Empty cash and empty holdings leave no saved draft.
Browser storage is origin-specific; clearing browser data removes drafts.

## Bulk Instrument Registration

The snapshot preview/applied editor checks normalized symbols against the
paginated existing `GET /api/instruments?through=YYYY-MM-DD`. The optional
`through` parameter selects effective symbols at the snapshot date (default
remains Jakarta today). `hasEffectiveSymbol` distinguishes an effective symbol
from the existing issuer-name fallback, preventing a display name from being
mistaken for a registered ticker. Non-EQUITY or ambiguous matches are conflicts
and are never automatically converted or overwritten.

**Review Missing Instruments** opens a compact review showing symbol, EQUITY,
display name and source accounts. A separate **Register N Instruments** action is
the explicit confirmation. No automatic registration occurs on upload, Apply,
refresh or registry checks. The symbol serves as the minimum valid display name;
no company legal name, exchange, sector or provider ID is invented. Registration
uses the existing client `crypto.randomUUID()` convention and
`POST /api/instruments` contract (`id,name,symbol,type,validFrom`). `validFrom`
is visibly the selected snapshot date, not a claimed IPO/listing date.

Confirmation rechecks the registry before sequential independent registrations.
Existing equities are skipped. Backend symbol locking/identity validation remains
authoritative; overlapping historical symbol ownership is also rejected by the
shared registration guard. A concurrent successful registration is detected by
refreshing the registry and reported as already registered. Other conflicts or
failures are shown per symbol. Successful independent registrations remain;
there is no cosmetic rollback or bulk endpoint. Retry only remaining missing
symbols after review. If the final registry refresh fails, reported successes
stay visible and a recheck is required before retrying. Context changes interrupt
pending work; completed identities remain registered and registry must be rechecked.

After registration the registry refreshes and stale comparison results clear.
Expected holdings and cash stay unchanged. Press **Compare Portfolio** again:
a newly registered EQUITY with no ledger position becomes **MISSING_FROM_LEDGER**
instead of **UNKNOWN_INSTRUMENT**. Registration never manufactures a MATCH.
Only canonical instrument registration is written; there are no portfolio events,
positions, cash events, thesis versions, persisted reconciliation snapshots,
prices/bars or provider writes. Snapshot mandates remain informational; Thesis
journal is the authoritative mandate workflow. No migrations or new endpoints.

Checks: frontend storage/reload/isolation, corrupt drafts, registry pagination,
conflicts, races, partial outcomes and refresh failures; disposable Chrome
acceptance covers reload/context restore, result absence, explicit review and
confirmation, unchanged cash/holdings/events/theses, unknown-to-missing status,
portfolio isolation and Clear Draft. All synthetic identity writes stay in the
owned disposable database; acceptance fingerprints the operational database
and operation/soak ledger before/after.


Draft/registration enhancement verification (2026-10-01): `dotnet build` passed
with zero warnings/errors; canonical `dotnet test` passed **50/50**; frontend
`npm test` passed **14/14** and the Node 22 production build passed; disposable
HTTP/PostgreSQL/Chrome acceptance passed **8/8**; offline restore passed **14**
groups with zero provider requests. The browser covered 1440, 1366, 1280, 1024,
768 and 390 px; mobile snapshot and registration-review screenshots were inspected.

Standard Python discovery ran 61 tests: **60 passed, 1 skipped**. The skip is the
pre-existing `test_production_database_fingerprint_unchanged` in
`collectors/python/tests/test_experiment_evidence.py`, gated by
`IDX_EXPERIMENT_VERIFY_DB=1` with reason **Opt-in local PostgreSQL fingerprint**.
This decorator existed before both reconciliation enhancements; it is not a new
skip. Discovery was rerun with that opt-in enabled: **61/61 passed, zero skipped**.
The opt-in test performs read-only operational database fingerprints around
filesystem-only synthetic evidence creation in a temporary directory.

## Screener V0.1 contract review — 2026-10-02

Final pre-implementation review is complete: **CONDITIONAL GO** for the fixed-pilot,
descriptive Screener. The canonical implementation contract is
[docs/SCREENER_V0_1_CONTRACT.md](SCREENER_V0_1_CONTRACT.md), reviewed against
`f36e539b0cdf508590879fa960b6f634af9aaba9`. No Screener code was implemented by this review.

Required small changes: explicit `through` enforcement in the shared feature
calculator; distinct RS20/RS60 percentage-point return differences without relabeling
legacy relative performance; bounded as-of reads and retained, knowledge-dated pilot
reference inputs. The effective-dated registry cannot establish past knowledge;
missing identity/status/basis evidence must remain blocked. Full registry history
redesign is deferred. **No database migration or Screener result persistence is
expected for V0.1.**

Actual candidate readiness remains blocked by short/discontinuous history and
unverified identity/status/price-volume basis coverage. The screen must preserve
all held positions and display unavailable evidence honestly. FullIdx remains
**DISABLED**; the read-only soak report still shows **1/10**. No threshold optimization.

Next milestone: implement the contract's shared through/RS corrections and
chronological fixtures, then the bounded descriptive service/API and existing-design
React screen. Recommended implementation reasoning: **HIGH**.

Continuation verification: .NET build passed with zero warnings/errors and standard
`dotnet test` passed **50/50**. Earlier checks at the same commit passed Python opt-in
**61/61**, frontend **14/14** and the production build. October 2 disposable acceptance
and offline restore reruns could not verify results because Docker was unavailable,
including outside the sandbox. Their previously recorded **8/8** and **14 groups**
remain historical baseline evidence, not fresh passes; rerun them before implementation
release. The completed contract incorporates the temporary checkpoint, which was removed.
