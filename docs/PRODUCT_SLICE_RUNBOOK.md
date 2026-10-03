# Portfolio API/UI vertical slice — local runbook

Requires .NET 10, PostgreSQL 17 (existing compose service), Python 3.12+ for
acceptance and Node 22.12+ for current Vite. Run from repository root unless
indicated. No provider token is needed. Keep connection strings outside Git.

Current product status: **Decision Verification V0.1 implemented**, alongside
**Decision Snapshot History UI V0.1**,
**Stocks + Stock Detail V0.1** and prospective Decision Snapshot persistence, plus
**Screener Milestone 6 data-readiness/evidence complete**. The latest section records
the read-only Decision History verification. Stocks is a local registry/history surface. The
production sidebar opens the read-only PILOT Screener independently of a portfolio.
The Screener M6 section below records the read-only operational audit plus the
authoritative-source review, the one substantiated PILOT configured-membership
universe snapshot, and the honest BLOCKED candidate result. Implementation and
evidence-review completion does not certify real candidate readiness. Earlier dated
sections retain their milestone results and scope at the time.

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

## Screener V0.1 — Milestone 1 completed, 2026-10-02

Implemented only the shared feature chronology/relative-strength milestone against
the unchanged [frozen contract](SCREENER_V0_1_CONTRACT.md). Starting repository state
was clean at `7fcff5316c395648f3e9fca7e6628f44c259d431` (`main`, already one commit
ahead of origin). No commit or push was performed for this milestone.

`PilotFeatures.Calculate` now requires an inclusive `DateOnly through` immediately
before `DateTimeOffset cutoff`. It filters both canonical bar dates and session-proof
dates internally before calendar/revision resolution. Existing known-at/source-
availability filters remain in effect. Future input cannot supply a historical
market date, advance its feature window, alter seeds or break its continuity.
There is no default/inferred through value or old-signature overload.

| Caller updated | Explicit market boundary / compatibility |
|---|---|
| Worker `Program.cs` (only production caller) | `last`, parsed and validated from the requested batch's `to`. Stored history and evidence may extend beyond that batch; the shared boundary excludes them. Same eligible inputs evaluated at the intended batch end retain the existing calculations. |
| `scripts/OfflineScale/Program.cs` (six call sites) | A fixed `through` equal to the planned fixture's 250th exchange date, independent of the supplied/revised history. Scoped/full, missing/alignment and correction/replay checks use the same date. This is an opt-in synthetic checker, not FullIdx activation. |
| Existing `PilotTests.cs` (nine call sites) | The fixture's explicit evaluation date; the 10-observation warmup case uses its tenth proof date, and the weekend case its planned Monday. Existing expected feature/legacy values were preserved. |

The legacy `RelativePerformance20` remains
`(stockEnd/stockStart)/(indexEnd/indexStart)-1`, including its prior positive-index-
volume rule. It was neither renamed nor converted to pp. EMA20/EMA50, True Range,
Wilder ATR14, prior ranges and volume ratio retain their existing implementations.

New programmatic fields `PilotFeatureResult.Rs20Pp` and `.Rs60Pp` calculate
`100 * ((stockEnd/stockStart-1) - (indexEnd/indexStart-1))` over exactly 21/61
observations on matching exchange dates. Every interior benchmark observation must
exist; no nearest date, forward fill or skipped gap is permitted. Index volume is
irrelevant to these new price returns. Fixtures verify **5 pp** for stock 100→110 /
IHSG 100→105, and **10 pp** for stock 100→120 / IHSG 100→110, while retaining the
distinct legacy ratio.

`Rs20PpState` / `Rs60PpState` reuse the existing `FeatureState` model. Adequate aligned
history is AVAILABLE; insufficient continuous stock observations are WARMUP with
`INSUFFICIENT_SESSIONS`; an absent/misaligned benchmark is UNAVAILABLE with
`BENCHMARK_MISSING`. Missing/current-gap/unconfirmed-session results remain
UNAVAILABLE with the existing result's reason. Decimal overflow of an optional new
RS field yields UNAVAILABLE/`NUMERIC_OUT_OF_RANGE` without breaking legacy features.
Every unavailable/warmup RS scalar stays null. Horizons are independent: RS20 may
be available while RS60 is warmup or blocked by an older benchmark gap.

The new RS values/states are intentionally excluded from legacy pilot JSON via
`JsonIgnore`: existing report fields and offline report-hash serialization retain
their previous shape. Future Screener DTOs must project the named numeric values
and states explicitly. No archived output was rewritten and no Screener API was added.

Added `PilotFeatureChronologyTests.cs` with **24** discovered cases covering through-
stable EMA20/EMA50/ATR14 and both RS horizons; future bars/proofs/oversized inputs;
future-only price dates; future conflicting proof records excluded before resolution;
repeated results; legacy formula/report compatibility; exact pp fixtures; 20/21/60/61
warmup boundaries; missing interior/end benchmark observations; absent/date-shifted
IHSG; stock missing/unknown/zero-volume continuity breaks; zero-volume index prices;
late stock/benchmark revisions and source availability; and optional RS overflow.
All existing .NET regression expectations remain intact apart from explicit dates.

| Check | Actual Milestone 1 result |
|---|---|
| Canonical `rtk proxy dotnet build` | PASS, zero warnings/errors |
| Canonical `rtk proxy dotnet test` | **74/74 passed**, zero skipped (50 prior + 24 new cases) |
| `rtk proxy dotnet build scripts/OfflineScale/OfflineScale.csproj` | PASS, zero warnings/errors; all auxiliary callers compile |
| Standard Python discovery with Python3.13 and collector `PYTHONPATH` | 61 discovered: **60 passed, 1 skipped**, no failures |
| `git diff --check` | PASS |
| Disposable HTTP/browser acceptance, restore, database opt-in fingerprint and scale execution | **NOT RUN**: Docker socket access was denied in the sandbox; the outside-sandbox readiness retry confirmed the Docker daemon was unavailable |
| Frontend tests/build | Not rerun; no frontend/API changes in this milestone |

The Python skip is the existing `test_production_database_fingerprint_unchanged`
gate (`IDX_EXPERIMENT_VERIFY_DB=1`, reason `Opt-in local PostgreSQL fingerprint`).
It is not a new skip or a database-verification pass. No fresh operational database
fingerprint is claimed. Tests use in-memory/temporary fixtures; Worker ingestion and
provider collection were not run. No database writes, migrations, reference snapshots,
Screener evaluator, API, UI, portfolio action or threshold change was introduced.
Collector FullIdx rejection guards and pilot configuration are unchanged; standard
Python tests covering FullIdx rejection passed.

Remaining milestones: bounded quality-preserving as-of reads and dated reference
evidence; actual price/status/basis readiness gates; eligibility and episode logic;
transparent sort/held union; read-only API; production screen; disposable acceptance
when Docker is available. The shared primitive does not attest corporate-action,
identity/status or volume-basis clearance and does not by itself establish live
candidate readiness. Follow the frozen contract before adding these later pieces.

## Screener V0.1 — Milestone 2 completed, 2026-10-02

Implemented only the evidence-read foundation against the unchanged
[frozen contract](SCREENER_V0_1_CONTRACT.md). Starting state was clean `main` at
`d124385e23e80c1440332e7c37d910a3d6b76517`, already two commits ahead of origin.
Milestone 1's calculator, callers, legacy report shape and RS formulas are unchanged.
No commit or push was performed.

### Bounded canonical reads

`ScreenerEvidenceDatabase.ReadAsync(connection, transaction, request, ct)` accepts
the caller's existing Npgsql connection/transaction. A parameterized Npgsql batch
reads bars and canonical listing evidence in one round trip, without the CLI reader,
per-bar queries, registry joins or application-side historical revision selection.
Milestone 4 must supply its one read-only repeatable-read transaction; the disposable
tests exercise that exact transaction mode. This reader opens no independent view
and issues no writes.

`ScreenerReadRequest` requires the fixed **2026-08-24** anchor and through within
**2026-08-24..2027-08-24**, inclusive (366 civil dates). Explicit errors reject a
different anchor/out-of-scope date or more than **211** selected stable IDs including
the benchmark. Bars are limited to **80,000 selected rows**, with SQL limit+1 and
an unavailable overflow result, never truncation. The batch timeout is **15 seconds**;
tokens reach database execution, result reads, file reads and hashing. A blocked
database-read cancellation test verifies prompt termination.

SQL filters requested IDs, anchored dates, `known_at`, raw-artifact fetch time,
recorded retrieval and session knowledge against inclusive through/cutoff **before**
`DISTINCT ON (instrument_id, session_date)` ordered by `known_at DESC,
revision_number DESC`. Stocks and benchmark use the same query. A→B→A evidence and
equal-knowledge revision ordering are preserved. Missing provenance is retained
as missing, rather than synthesized or used to prefer an older bar.

`ScreenerBarEvidence` retains selected ID/date/revision/knowledge/hash, run/artifact
identities, source/raw hash/fetch/retrieval, session reference/knowledge, exact
canonical quality, OHLC/adjusted-close strings, volume and unit/basis/segment.
Validation occurs **after selection** through `Validate()`. REJECTED/UNKNOWN quality,
incomplete provenance, inconsistent OHLC or unrepresentable decimals yield typed
unavailable reasons and retain the selected row. Excess fractional precision is
also rejected instead of allowing decimal parsing to round. No cleaner-revision
fallback. DEGRADED/STALE quality is retained independently of later clearance and
freshness decisions; no source-quality promotion occurs.

Listing evidence is selected from `instrument_listing_evidence` by stable ID and
cutoff and resolved with `InstrumentBoundaries.AsOf`. Original legacy assertions
retain their missing retrieval limitation and stable-ID name placeholder; no live
issuer name, type, symbol or listing boundary is borrowed from the mutable registry.
Listing/identity reconciliation and sequence eligibility belong to Milestone 3.

### Local reference schema 1 and effective resolution

The only new real reference input is `pilot/screener-reference.json`:

```json
{"schemaVersion":1,"universes":[],"instruments":[]}
```

This is intentionally valid, empty and **not ready evidence**. Missing files likewise
supply no assertions; absent universe returns `UNIVERSE_NOT_KNOWN` and absent
instrument snapshot `REFERENCE_NOT_KNOWN`. No fallback to `pilot/universe.json`,
`instrument` or `instrument_history`. No real verification snapshots were generated.

The typed camelCase schema requires these fields (nullable values remain explicit):

| Record | Fields |
| --- | --- |
| Universe snapshot | `snapshotId`, `knownAt`, `universeId=PILOT`, `memberIds` (max 10), `benchmarkId`, `evidence`, `contentHash` |
| Instrument full snapshot | `snapshotId`, `instrumentId`, `knownAt`, `evidence`, `contentHash`, `identities`, `trading`, `prices`, `volumes` |
| Source evidence | `id`, `source`, `reference` (HTTPS), `publishedAt` (nullable), `retrievedAt`, `knownAt` |
| Identity interval | `from`, `through` (nullable), `symbol`, `displayName` (nullable), `classification`, `currency`, `board`, `evidenceIds` |
| Trading interval | `from`, explicit `through`, `status`, `mechanism`, `evidenceIds` |
| Price interval | `from`, explicit `through`, `sourceId`, `convention`, `continuity`, `eventCoverage`, `contentHashes`, `evidenceIds` |
| Volume interval | `from`, explicit `through`, `sourceId`, `unit`, `basis`, `rawPriceCompatible`, `currency`, `marketSegment`, `contentHashes`, `evidenceIds` |

Snapshots are retained append-only assertions: actual local `knownAt` cannot precede
source knowledge/retrieval, and publication never substitutes for capture. Select
the latest full snapshot known at cutoff, then an inclusive effective interval.
Back-effective facts known later never leak. New incomplete/UNKNOWN coverage replaces
old coverage in full; it is never merged to improve readiness. Same-time competing
snapshots return conflict. V1 conservatively requires non-overlapping intervals within
each fact collection; overlap invalidates the selected instrument snapshot, including
overlap outside the evaluated day. Distinct snapshots at later knowledge times are
the replacement mechanism, not edits to old assertions.

Unknown/duplicate JSON fields, missing required fields/arrays, duplicate snapshot or
member IDs, empty/malformed UUIDs, unsupported schema/vocabulary, invalid ranges,
unseen evidence IDs, invalid lowercase SHA-256 or changed snapshot content fail closed.
`contentHash` must equal `ScreenerReferences.SnapshotHash(snapshot)`: SHA-256 of canonical
JSON excluding the root `contentHash`, with ordinal property/collection ordering and
UTC instants. It detects accidental mutation, not the truth of an external assertion.
Never update the hash/knowledge of an old assertion to manufacture historical readiness.

Identity classes are ORDINARY/INDEX/UNSUPPORTED/UNKNOWN. The schema records supported,
unsupported and unknown boards/mechanisms without deciding final eligibility.
Price continuity requires RAW_AS_TRADED, source convention STOCK_RAW or INDEX_LEVEL,
specific selected canonical content hashes and valid canonical price provenance.
Ordinary-share price use requires IDR, positive observed stock volume and CLEARED
unit-changing-event coverage; unresolved splits/consolidations/reorganizations remain
unavailable. Verified raw cash-dividend movements are retained; no total-return math.
Index event coverage can be NOT_APPLICABLE: verified identity, index-level convention,
continuity and valid prices remain required, but stock boards/status/share quantities
and positive index volume do not gate index price clearance.

Volume clearance is independent: explicit source, matching revision hashes, compatible
raw-price quantity basis, IDR, known segment and matching verified share metadata are
required. UNKNOWN equality is not verification. LOTS can be represented but are not
converted or cleared for these share-based fields. SPLIT_ADJUSTED is usable only with
specific independent raw-price compatibility evidence, never merely because strings
match. Missing/failed volume clearance need not erase cleared positive-volume prices.
No adjustment factor, adjusted-close substitution or guessed ×100 is introduced.

`ScreenerReferenceFiles.LoadAsync(ct)` copies exactly the three fixed local inputs
once: new reference, existing `pilot/sessions.json`, `pilot/instrument-sessions.json`.
They share **4 MiB / 10,000 evidence-record** bounds (including nested assertions,
membership IDs and revision-hash entries); overflow returns explicit unavailable.
There is no remote fetch, HTTP upload or supplied filesystem path.

Calendar resolution reuses `SessionProof`, `InstrumentSessionProof`,
`ExchangeCalendarEvidence.Classify` and `CompletedSessionPolicy` (19:00 WIB).
Through/cutoff filter precedes latest dated assertion selection. An unambiguous later
assertion replaces the earlier assertion for that date; incompatible same-time
assertions block chronology. Closures/weekends remain distinct from unknown weekdays;
same-day open evidence needs completion. Missing bars and zero volume never generate
closure/no-trade assertions. Existing operational files and Worker admission are not
rewritten. Before operational session corrections are appended, separately verify
legacy Worker ingestion, whose pre-existing single-record-per-date parser is unchanged.

### Selected-input digest and actual verification

`ScreenerReferences.Select` exposes only the chosen knowledge-visible snapshots and
anchored, through-bounded latest session/status assertions for selected IDs. Competing
selected assertions are retained for conflict provenance. `SelectedDigest(..., ct)`
hashes resolved policy/anchor/through/cutoff/IDs, selected bar revision tuples, selected
canonical listing facts/hashes and the selected reference bundle. Properties/arrays
are ordered invariantly; dates are ISO and instants UTC. Input ordering and equivalent
timestamp offsets do not alter the digest. Future-only references, bars and proofs
cannot change an old-cutoff digest. Paths, connection settings and unselected history
are absent. This is an **internal market/reference digest**, not the final public
`inputHash`: portfolio ledger/thesis facts and API input pinning are Milestone 4 work.

Added **64 .NET cases**: 55 in-memory chronology/schema/clearance/calendar/digest/bounds
checks and 9 opt-in real PostgreSQL cases. The SQL tests use connection-local TEMP
copies of the actual schema in a harness-owned `idx_screener_test_*` database.
They verify stock/index A→B→A, late/future-source exclusion, invalid latest and decimal
overflow without fallback, revision ties, requested-ID/date bounds, listing chronology,
future append digest stability, read-only repeatable-read compatibility and cancellation
while waiting on an owned table lock. Every existing 74-case regression remains.

| Check | Actual Milestone 2 result |
| --- | --- |
| Canonical `rtk proxy dotnet build` | PASS, zero warnings/errors |
| Plain canonical `rtk proxy dotnet test` | 138 discovered: **129 passed, 9 skipped**, zero failures; skips are the new explicit disposable-DB opt-in gate |
| `rtk proxy python3.13 -m unittest discover -s scripts -p test_screener_evidence.py -v` | **1/1 passed**; invokes canonical `rtk proxy dotnet test` with owned DB context: **138/138 passed, zero skipped** |
| Collector standard discovery with `IDX_EXPERIMENT_VERIFY_DB=1`, Python3.13 and collector PYTHONPATH | **61/61 passed**, zero skipped; includes existing DB fingerprint and FullIdx rejection checks |
| `git diff --check` | PASS |
| Frontend/browser/exchange acceptance and archived restore | Not rerun for this foundation; no API/UI/exchange/Worker behavior changes |

Docker was available after the required outside-sandbox readiness retry (server
29.8.1). The final owned DB run passed and removed its disposable database. Before/
after fingerprints matched for canonical bars, raw artifacts/fetches/runs, registry/
symbol history/listing evidence/calendar, portfolio headers/events/theses, all local
pilot JSON inputs and authoritative operation files. The only intentional real
evidence-file addition is the empty schema-1 reference. No operational tables or
existing reference/soak files were written. No provider calls, collector changes,
new dependencies, database migrations/indexes or FullIdx activation.

Remaining: Milestone 3 eligibility/continuity, per-feature evidence dependencies,
episode transitions, transparent ordering and held union; Milestone 4 one request
transaction/read-only endpoint and final portfolio-inclusive digest/pinning; production
UI and full disposable acceptance/restore. Real identity/status/price-volume attestations
and enough retained history remain external readiness gaps. This milestone does not
produce candidates or certify operational stocks as ready.

## Screener V0.1 — Milestone 3 completed, 2026-10-02

Implemented the pure application evaluation core against the unchanged
[frozen contract](SCREENER_V0_1_CONTRACT.md). Starting state was clean `main` at
`c5083f48a47e0f86b453a4908bcf59273beb3500`, already three commits ahead of origin.
No commit or push was performed. The canonical contract, M2 readers/resolvers/digest,
reference files, collector, Worker entry point, API and frontend are unchanged.

### Application inputs and result

`ScreenerEvaluator.Evaluate(request, database, selectedReferences, portfolioHistory, ct)`
accepts M2's bounded, selected evidence and optional immutable portfolio history. It
does not open a database connection, load a file, use a current clock, generate IDs,
fetch a provider or write anything. Explicit `through`/`cutoff` and the fixed anchor
govern every replay. Missing evidence returns blocked rows rather than fabricated
successful empty results; cancellation propagates.

| File | Responsibility |
| --- | --- |
| `ScreenerEvaluation.cs` | Application records, ordered eligibility classification, prepared-evidence episode transitions, canonical ordering, cap and summaries |
| `ScreenerEvaluator.cs` | Calendar-aligned replay, M2 evidence integration, independently cleared index context, held union and historical mandate selection |
| `ScreenerFeatures.cs` | Internal availability/dependency integration around shared arithmetic; independently certified volume and IDR value proxies |
| `PilotFeatures.cs` | Extract existing TR, smoothing, volume-ratio and pp-return arithmetic into internal helpers for reuse; legacy public signature, calculations, stock/index admission and report shape preserved |
| `ScreenerEvaluationTests.cs` | 42 pure transition/precedence/order/cap/model cases |
| `ScreenerEvaluatorTests.cs` | 78 integrated evidence/replay/feature/held/chronology/bounds cases |

Paths in the table are under `src/IdxStockIntelligence.Application/` or
`tests/IdxStockIntelligence.Tests/`, respectively. No new dependency, framework,
repository layer, migration, index, result table or episode persistence was added.

The complete in-memory result contains dated rows, ordered eligibility reasons,
setup/evaluation reasons, episodes, per-field `FeatureState`s, row quality,
reference/seed/canonical provenance, shared IHSG context, nullable summary counts,
and separate ranked-candidate/all-view/shortlist/held ID lists. These are application
models, not the final HTTP DTO. Raw close/volume retain their actual observation
date; current indicators become null when the current sequence is unavailable.
Provenance retains both the last valid observed row and the selected current evidence,
including a rejected/unrepresentable current revision. Canonical quality is never
promoted to match computed quality.

### Eligibility and availability

Reuse the domain `Eligible`/`Ineligible`/`DataBlocked` states. Ordered precedence is:

1. Unresolved/conflicting identity, reference or membership, then unknown class:
   DATA_BLOCKED (`IDENTITY_CONFLICT`, `REFERENCE_NOT_KNOWN`, `TYPE_UNKNOWN`).
2. Proven non-ordinary type, pre-/post-listing, unsupported board/mechanism,
   suspension or no-trade: INELIGIBLE, in the contract's exclusion reason order.
3. Otherwise listing/board/status/calendar/basis/canonical/current/history gaps:
   DATA_BLOCKED, in the frozen reason order. Keep additional specific M2 errors and
   all established causes rather than returning only the primary code.
4. Verified ordinary shares on MAIN/DEVELOPMENT, continuous trading and at least
   21 uninterrupted completed valid observations: ELIGIBLE.

Use M2 reference/calendar/status resolution and existing listing boundary semantics.
There is no registry/suffix inference or cleaner-revision fallback. A listing assertion
without retained retrieval remains `LISTING_UNKNOWN`; its missing knowledge provenance
is not synthesized. Conflicting reference/session status cannot certify `noTrade`.
An unexplained zero stock volume blocks observation use. Proven pre-listing,
post-delisting, suspension and no-trade absence is distinct from a missing provider
row; it does not create a missing-current/history reason for a known exclusion.

Known exchange closures/weekends are skipped. Unknown weekdays, invalid/missing bars,
unsupported/unknown required facts and uncleared price segments break the sequence.
The next cleared segment starts a new seed and must rebuild 21 observations before
setup evaluation. Unknown/uncompleted trailing sessions never make an older close
current. Actual observed dates remain visible.

Optional EMA50, RS60/RS20, benchmark/context, volume ratio, ATR and liquidity gaps
do not become eligibility/setup gates. Price and volume certification remain
independent and revision-specific. Unknown volume metadata equality is not clearance;
no guessed conversion or adjusted-close substitution is used. The prior-20 IDR
close×share proxy excludes the current observation; daily value uses current only.
Optional arithmetic overflow affects that field alone. Index EMA/TR/Wilder ATR and
returns use cleared index prices with no share-volume, stock-board or stock-status
gate. IHSG trend and the inclusive ATR% >=2 volatility flag remain independent.

Field readiness is AVAILABLE/WARMUP/UNAVAILABLE; unsupported values are null with
reasons. Row COMPLETE/PARTIAL/BLOCKED is separate from eligibility, setup and source
quality. A price-eligible CONFIRMED row can be PARTIAL. Every blocked/ineligible row
has NONE, `Evaluated=false` and `NOT_EVALUATED`, distinct from evaluated `NO_SETUP`.

### Episodes

`ScreenerEpisodes.Advance` consumes only prepared close, prior high and evaluability.
It does not calculate features. Close > priorHigh20 directly confirms; the inclusive
98%..100% band starts WATCH; lower closes produce evaluated NONE. Current intraday
high alone cannot create confirmation or failure.

WATCH retains one ID while its rolling threshold changes. Sessions 1–5 can confirm
in that same episode, freezing the current prior high and confirmation date.
The would-be sixth session expires first, without confirmation/restart on that bar.
Leaving the band ends the watch. CONFIRMED retains its original trigger/date despite
higher rolling highs. Close equal to trigger stays confirmed; a lower close fails
only during confirmed sessions 1–20. The would-be 21st expires before a failure test.
Neither exit nor expiry restarts on its ending session. FAILED is terminal for its
failure session; the next evaluable session applies fresh NONE rules.

Blocked/ineligible days or a broken sequence terminate an active episode with
`DATA_INTERRUPTED`/`INELIGIBLE`; no pause, resurrection or inferred price failure.
Expiry/interruption retains the last evaluated age. Ordinary watch exit/failure
counts its evaluated ending session. Confirmation age starts at 1 independently
of total episode age. Closed dates advance neither counter.

Episode IDs are exactly `screener-v0.1.0/{lowercase UUID}/YYYY-MM-DD`, from the start
session. Start/confirmation/end dates, reason, both ages, frozen trigger and current
watch threshold are explicit. No random/request-clock identity or repeated daily
confirmation event exists. Revision-aware replay can revise attributes at a later
cutoff without rewriting stored evidence.

### Ordering, held positions and counts

Discovery requires configured, eligible, evaluated WATCH/CONFIRMED. Sort CONFIRMED
before WATCH, then available RS60 pp descending, RS20 pp descending, prior-20 IDR
proxy descending, uppercase ordinal symbol (missing last), then lowercase UUID
ordinal. Available negatives precede nulls; non-available values cannot improve rank.
No rounding or composite score. All-view adds FAILED then NONE under the same keys.

Rank the complete candidate list before taking its first **20**. Preserve every
candidate and configured row, plus omitted confirmed/watch counts. A 28-candidate
service-level ordering fixture proves the cap and ranks without changing the real
max-10 universe/reference bound. No HTTP filter or page is implemented here.

`PortfolioLedger.Project` supplies positive open holdings using the same through
and cutoff and its existing correction/accounting rules. Closed/future positions
and thesis-only ownership are not held. All held IDs survive eligibility, setup,
membership and cap restrictions, including outside-universe/missing-label rows.
Membership/held union produces one logical row per stable ID, with configured/held
flags and a nullable canonical discovery rank. Held ordering is supported symbol
then UUID; no current registry label fallback.

Mandates follow the existing known-at/latest-version/active thesis semantics;
FAST_SWING/LONG_SWING/INVEST or null. An inactive latest version cannot resurrect an
older active mandate. Mandates do not alter features, episodes or rank. Replay uses
the existing 10,000-event/200-open-holding bounds and 2,000-thesis archive ceiling,
with no per-instrument database calls. M4 must read the bounded portfolio/evidence
inputs together in its request transaction.

Known-universe configured/category/evaluated/candidate/shortlist/omission counts
are deterministic; configured equals eligible+ineligible+dataBlocked. History,
stale and unsupported diagnostics can overlap. Held/held-outside counts are separate.
Unknown membership leaves configured/category counts and held-outside membership
count null, while the number of reconstructed held positions remains known.
Complete known exclusions can produce COMPLETE with zero candidates; blocked held
coverage still prevents that complete state. Unresolved universe/calendar produces
BLOCKED. No action, recommendation, buy/sell signal, confidence or AI score exists.

### Actual verification

Added **120 discovered .NET cases** (42 pure, 78 integrated), on top of the M2
138-case baseline. Fixtures are in-memory only; no operational reference attestations
or market/portfolio fixtures were written.

| Check | Actual Milestone 3 result |
| --- | --- |
| Canonical `rtk proxy dotnet build` | PASS, zero warnings/errors |
| Plain canonical `rtk proxy dotnet test` | 258 discovered: **249 passed, 9 skipped**, zero failures |
| Existing disposable Screener evidence harness via standard Python discovery | **1/1 passed**; canonical opt-in .NET discovery **258/258 passed**, zero skipped |
| Collector standard discovery with `IDX_EXPERIMENT_VERIFY_DB=1`, Python3.13 and collector PYTHONPATH | **61/61 passed**, zero skipped, including DB fingerprint and FullIdx rejection |
| `git diff --check` | PASS |
| Frontend/browser/exchange acceptance and archived restore | Not rerun; no fresh pass claimed |

Plain discovery's nine skips are the existing M2 disposable PostgreSQL opt-in gate,
not new M3 skips. Docker was available (29.8.1) after the outside-sandbox socket
readiness check. The final disposable run exercised the existing nine real SQL cases
and all new application cases, removed its owned database and verified unchanged
fingerprints of 11 operational tables, pilot JSON files and authoritative operation
files. Provider calls **0**; FullIdx remains **DISABLED**. No new migrations, collector
universe changes, operational writes, API route or UI/navigation changes occurred.

Remaining M4+: one read-only repeatable-read request transaction with bounded
portfolio/evidence reads, GET/HTTP DTO/error mapping, final portfolio-inclusive
input digest and pinning, display filters/paging; then production UI and broader
disposable browser/restore acceptance before release. The empty real reference file
and short/uncleared actual history still prevent a claim of real candidate readiness.

## Screener V0.1 — Milestone 4 completed, 2026-10-02

Implemented the read-only HTTP milestone against the unchanged
[frozen contract](SCREENER_V0_1_CONTRACT.md). Starting state was clean `main` at
`7e49dbdfe1b52e8e66fa8a531b1d4d78b56ce15d`, already four commits ahead of origin.
No commit or push was performed. M1 feature arithmetic/report compatibility, M2
evidence selection/clearance, M3 eligibility/episodes/order/status and all actual
pilot references remain unchanged. M2's canonical hashing gains only a public
cancellation-token overload for the final hash.

### GET /api/screener

`ScreenerQuery.Resolve` receives one request clock reading. The through default,
cutoff default and both future checks use that same instant. The endpoint accepts
only these parameters; unknown and repeated keys are 400 errors.

| Parameter | Accepted values / default |
| --- | --- |
| `through` | Exact `YYYY-MM-DD`; default Jakarta today. Inclusive **2026-08-24..2027-08-24**, and not after Jakarta today. No clamping when today itself exceeds the horizon. |
| `cutoff` | RFC3339 with explicit `Z` or `±HH:MM` offset, seconds and optional .NET-representable fractional seconds (up to seven digits). Blank/omitted resolves once to now. Inclusive 1900..request now; returned as UTC. |
| `universe` | `PILOT` only, default PILOT. FullIdx and arbitrary lists return 400. |
| `portfolioId` | Optional nonempty UUID. Omitted means discovery only. Missing/not created by cutoff returns 404 `PORTFOLIO_NOT_FOUND`. |
| `view` | `shortlist` (default) or `all`. |
| `setup` | `ALL` (default), `NONE`, `WATCH`, `CONFIRMED`, `FAILED`. |
| `eligibility` | `ALL` (default), `ELIGIBLE`, `INELIGIBLE`, `DATA_BLOCKED`. |
| `offset` | Integer 0..10000, default 0. |
| `limit` | Integer 1..100, default 20. |
| `inputHash` | Optional exact lowercase SHA-256 returned by an earlier request. Changed inputs return 409 `INPUT_CHANGED`. |

Example initial request and pinned second page:

```text
GET /api/screener?through=2026-09-30&view=all&limit=20
GET /api/screener?through=2026-09-30&view=all&offset=20&limit=20&cutoff=<URL-encoded returned cutoff>&inputHash=<returned hash>
```

Keep the first response's through and cutoff when changing filters/pages. A supplied
cutoff is never replaced with a newer now. Invalid query returns 400 `INVALID_QUERY`;
database/timeouts/internal portfolio bounds return sanitized 503
`SCREENER_UNAVAILABLE`. M2 reference/bound errors retain stable codes such as
`REFERENCE_MALFORMED` and `REFERENCE_BOUND_EXCEEDED`. Incomplete valid evidence
returns HTTP 200 with the computed status, including an empty valid reference bundle.
No body, server path, custom sort, held-exclusion control or run/job endpoint is added.

### One database view and copied reference bundle

`ScreenerService.ReadAsync` copies the three fixed local evidence files once using
M2's loader, opens one pooled connection and begins one **REPEATABLE READ, READ ONLY**
transaction. Portfolio header, knowledge-visible event history and thesis versions,
selected canonical/listing evidence and benchmark are all read on that connection
within the same transaction. Existing portfolio decoders and ledger projection are
reused; there is no per-holding query, registry/symbol-history fallback, independent
portfolio view or second evidence transaction.

Positive ledger holdings determine the bounded configured/held/benchmark union.
M2 reads the selected revisions, M2 resolves the copied evidence, and M3 evaluates
the result. The service computes the final hash, verifies any pin, maps the response,
then completes the read transaction. A file replacement during the request cannot
replace the already copied bundle. A concurrent database commit cannot change later
reads in the established request snapshot.

Existing bounds remain: 10 configured stocks, 200 open holdings, 211 distinct IDs
including benchmark, 366 anchored dates, 80,000 selected bars, 10,000 events and
2,000 knowledge-visible thesis versions per portfolio. SQL uses limit+1 for bounded
histories/evidence; overflow fails explicitly rather than truncating. The three
files share 4 MiB / 10,000 reference-record bounds. All database commands use a
15-second timeout. Request cancellation reaches file/database reads, evidence
selection, replay, canonical hashing and mapping; it propagates rather than returning
an empty success. Existing ledger projection stays bounded and is checked for
cancellation before and after; its accounting implementation is unchanged.

### Final inputHash and pinning

The final SHA-256 incorporates M2's selected-input digest: policy, anchor, through,
cutoff, selected IDs, stock/benchmark revision tuples, listing facts/hashes,
universe/instrument snapshots and calendar/instrument-session evidence. With a
portfolio, it also includes the immutable portfolio header/settings, knowledge-visible
ledger facts and correction lineage used by replay, held share state and
knowledge-visible thesis versions for held IDs. With no portfolio, those inputs
remain absent. Decimal portfolio/share values use invariant round-trip strings;
instants normalize to UTC and canonical object/collection ordering is deterministic.

View, setup/eligibility filters, offset and limit do not affect evaluation identity.
Future-only facts, reference snapshots and session replacements cannot alter a pinned
old-cutoff response. A newly visible selected revision, universe/reference, ledger
fact or thesis changes the hash and makes the old pin return 409. No latest-file hash,
filesystem path, connection details or independent wall clock is included. This
continues to describe `FIXED_PILOT_KNOWN_INPUTS`; it does not recover an unretained
actual historical decision or a historical all-listed universe.

### Response, status and paging

`ScreenerResponse.cs` defines explicit camelCase HTTP DTOs and a presentation mapper.
Top-level context includes policy, through/target/cutoff/anchor, selected universe ID,
replay scope, final hash, M3 status/reasons/summary, shared IHSG context, page metadata,
ordered discovery/held IDs and one deduplicated row collection. Every row consistently
exposes identity/membership/mandate/rank; eligibility and setup reasons/evaluation;
episode attributes; dated close, price/volume/IDR-proxy/ATR/RS pp features; freshness,
trading status, data quality/reasons, sparse field states and provenance.

Unavailable numbers/tri-state flags remain null or UNKNOWN with availability/reasons;
legitimate ATR/RS zeros stay zero. Structural null mandate/episode/rank needs no
feature reason. Canonical DEGRADED/REJECTED quality is preserved separately from
computed COMPLETE/PARTIAL/BLOCKED. Flat provenance describes the actual retained
observation and seeds/reference IDs; `currentEvidence` independently retains the
selected current revision even when rejected. A stale close keeps its actual date;
current indicators stay unavailable. No legacy relative-performance, recommendation,
action, score or confidence field is projected.

Status/summary come directly from M3 before any display filter: unknown universe
keeps category counts null; incomplete/no evaluated coverage is BLOCKED; optional
gaps can produce PARTIAL with an eligible setup; complete affirmative exclusions
can produce COMPLETE with zero candidates. Configured/category sums, overlapping
diagnostics, pre-cap candidates and omissions remain unchanged.

Shortlist starts with M3's ranked top 20, then filters, then pages. Filters never
refill from lower ranks. All-view starts with all configured rows, including NONE,
FAILED, blocked/ineligible and candidates below the cap. `page.total` counts the
filtered list before paging. Discovery ranks and whole-result summary are unchanged.
Every positive holding is exposed independently of view/filter/offset/limit,
including outside-universe, blocked and ineligible holdings. A held discovery row
appears once; held rows consume no page capacity. Closed holdings and thesis-only
ownership do not become held. Existing correction chronology and latest-version/
active thesis semantics determine holdings/mandates at cutoff.

### Actual verification

Added **47 .NET cases** in `ScreenerResponseTests.cs`: exact/default/boundary query
validation and offset normalization; cap-before-filter/no refill; all-view deterministic
paging; held preservation/deduplication/ranks/counts; JSON nulls/reasons/real zeros;
pinning/cancellation; portfolio-aware hash changes and ordering/offset/decimal-scale/
future-fact invariance. Existing M1–M3 and database cases remain in standard discovery.

Extended the existing unittest/PostgreSQL/HTTP harness with
`scripts/test_screener_http.py`; common environment/fingerprint helpers are reused
from `test_screener_evidence.py`. **10 HTTP cases** exercise the full route through
validation, copied reference resolution, bounded SQL, M3, final hash and DTO.
They cover empty/malformed evidence and 400/404/409/503 mappings, filtered/paged held
union, historical unavailability, future facts, selected-input changes, stock/index
A→B→A and rejected-current provenance, correction/closed-position/inactive-thesis
chronology, 201 holdings/10,001 events/2,001 thesis overflow, reference byte overflow,
concurrent canonical/portfolio commits plus file replacement, client disconnect and
the real 15-second command timeout. All fixtures use owned temporary databases and
private temporary API/reference directories, removed by cleanup.

| Check | Actual Milestone 4 result |
| --- | --- |
| Canonical `rtk proxy dotnet build` | PASS, zero warnings/errors |
| Plain canonical `rtk proxy dotnet test` | 305 discovered: **296 passed, 9 expected M2 DB opt-in skips**, zero failures |
| Standard disposable discovery: `rtk proxy python3.13 -m unittest discover -s scripts -p 'test_screener_*.py' -v` | **11/11 passed**: existing SQL wrapper + 10 HTTP cases; canonical opt-in .NET discovery **305/305 passed**, zero skipped |
| Collector standard discovery with `IDX_EXPERIMENT_VERIFY_DB=1` and collector PYTHONPATH | **61/61 passed**, zero skipped, including operational DB fingerprint and FullIdx rejection |
| `git diff --check` | PASS |
| Frontend/browser/product exchange and archived restore | Not rerun; no fresh pass claimed |

Docker was available (29.8.1) after the outside-sandbox readiness check. Disposable
checks verified unchanged before/after fingerprints of all 11 operational canonical/
raw/registry/listing/calendar/portfolio tables, pilot JSON files and authoritative
operation/soak files. Provider calls **0**. FullIdx remains **DISABLED** and rejected.
No operational writes, migration/index, result/episode persistence, provider/collector
change, new dependency, threshold optimization or Worker/StockDetail/snapshot change.
Only GET is added; React page, navigation, API wrapper and table remain unimplemented.

Remaining M5+: production React Screener within the existing shell/design, accessible
states/filters/held exposure and browser acceptance; broader release/restore checks
as applicable. Real retained identity/status/price-volume clearance and adequate
history remain external readiness gaps. The unchanged empty real reference file
still yields honest BLOCKED coverage rather than claiming actual candidates are ready.

## Screener V0.1 — Milestone 5 completed, 2026-10-02

The production React application now contains one enabled **Screener** navigation
entry and a descriptive comparison page. It reuses the existing shell, theme tokens,
PageHeader, Metric, Badge, Notice, table scrolling and native Dialog. The standalone
design prototype is unchanged. M4 at `006254d` is the committed baseline; all M5
changes remain uncommitted. The frozen contract and M1–M4 backend are unchanged.

### Files and production page

| File | Change |
| --- | --- |
| `frontend/src/ScreenerPage.tsx` | Context, summary, IHSG context, discovery and held sections, filters/paging, notices and row details |
| `frontend/src/screener.ts` | Literal DTO types, GET wrapper, display formatting and one cancellable request owner with generation/hash/cutoff guards |
| `frontend/src/api.ts` | Preserve HTTP status/code in an Error subclass; existing message callers keep their behavior |
| `frontend/src/main.tsx` | Route Screener before portfolio availability gates; reuse the current portfolio/open dialog state, hide unrelated portfolio context on Screener |
| `frontend/src/ui.tsx` | Reuse/test the actual navigation; optional loading label and keyboard traversal of native summaries in dialogs |
| `frontend/src/style.css` | Screener grids, wrapping facts, numeric alignment, sticky symbol cells and narrow-screen table scrolling |
| `frontend/src/screener.test.tsx` | 36 new native Node/React render and request lifecycle checks |
| `frontend/package.json`, `frontend/tsconfig.test.json` | Include the new checks in the existing frontend test command; no dependency change |
| `scripts/check_product_ui.mjs` | Extend the existing Chrome/CDP entry point with a Screener mode and consistent screenshot scroll position |
| `scripts/check_screener_ui.mjs` | Screener assertions using the same browser/runtime helpers and real disposable HTTP responses |
| `scripts/test_screener_http.py` | Serve the built frontend, add opt-in Chrome case and owned temporary mutation/reference fixtures |
| `docs/PRODUCT_SLICE_RUNBOOK.md` | This completion record, operation and recovery guidance |

### Context, pinning and errors

Through is a market date, bounded by the frozen 2026-08-24..2027-08-24 horizon and
Jakarta today. Known by is a WIB datetime-local input; an explicit value uses the
existing UTC conversion. Blank omits cutoff on a fresh request, allowing the server
to resolve now once. The response displays policy, PILOT, applied through, resolved
target session and resolved known-by time. Its normalized cutoff and inputHash are
retained verbatim for view/filter/page requests. No frontend indicator, ranking,
eligibility, setup or quality calculation is performed.

Editing through/cutoff aborts and clears rows, details and pin immediately, resets
offset, and requires **Apply context**. **Now** intentionally clears the known-by
input and evaluates. Portfolio is optional: choose **Discovery only** or the open
workspace portfolio. Its selector starts a fresh context; opening another portfolio
reuses the existing workspace dialog/state and remounts the Screener with a fresh
today/now context. No reconciliation draft or portfolio result is used as Screener
evidence. There is no duplicate portfolio-list loader.

AbortController plus an increasing request generation reject both late success and
late error responses, including a transport that ignores cancellation. Same-context
filter/page loads replace discovery with its own busy state while retaining all
held rows from the established pin. New contexts and errors clear the displayed
result. No result is written to localStorage, IndexedDB or a database.

HTTP 409 `INPUT_CHANGED` clears every displayed row and disables traversal. The
notice says **Underlying Screener inputs changed. Refresh the evaluation.** Explicit
**Refresh evaluation** discards the pin, resets offset to zero and evaluates the
current context. Blank cutoff resolves a fresh now; an explicit historical cutoff
remains selected. HTTP 400 shows context/filter validation guidance, 404 reports an
unavailable portfolio under the context, and 503/network failures offer retry.
Errors never become empty success and private server details are not displayed.

### Presentation and coverage

The header says **Descriptive breakout screening; not an order.** Summary metrics
are Configured, Eligible, Data Blocked, Candidates and Held. The disclosure exposes
confirmed/watch, evaluated/ineligible, history/stale/unsupported, shortlist/omitted
and outside-universe held counts. Diagnostics explicitly may overlap. Null counts
stay **Unavailable**, and API zero remains zero.

IHSG is separate: trend POSITIVE/NEUTRAL/NEGATIVE/UNKNOWN and volatility
NORMAL/ELEVATED/UNKNOWN retain the supplied values. Market date, close, EMA20/50,
ATR14/%, reasons, availability and provenance are discoverable. UNKNOWN is never
converted to neutral or normal.

Default discovery columns, in order: Symbol/Held, Setup, Close IDR/observed date,
Distance to prior high %, RS60 pp, RS20 pp, Prior-20 Value Proxy IDR, Data State.
The backend's ID list controls order; a Map only resolves the shared returned rows.
No local sorting, gate, cap refill or ranking is added. Prices, percentages and pp
use two decimals; IDR proxies use whole IDR. Details show unrounded returned numeric
values. Stale observations keep their actual old dates. Historical labels never
fall back to the mutable instrument registry.

Shortlist is default; All instruments, ALL/NONE/WATCH/CONFIRMED/FAILED setup and
ALL/ELIGIBLE/INELIGIBLE/DATA_BLOCKED eligibility controls fetch the backend. Filters
and views reset offset; Previous/Next use returned offset/limit/total. Production
default limit is **20**. Held rows are never locally filtered or paged away.

**All Held Positions** uses `heldIds` and the same row objects as discovery. A shared
instrument may appear in both tables but the API summary is displayed without
double counting. Outside-universe, ineligible, blocked and noncandidate holdings
remain visible. Mandate is neutral FAST_SWING/LONG_SWING/INVEST/Unassigned metadata.
A selected empty portfolio has a factual no-positive-holdings message; discovery
still operates. Discovery only has no held section.

COMPLETE is neutral; zero candidates explicitly says **No candidates met this
descriptive setup**, even if All instruments still contains excluded rows. PARTIAL
has a persistent coverage notice and actual reasons while evaluable setups remain
visible. BLOCKED explains unavailable required evidence/history and cannot be
mistaken for an empty successful screen. `setupEvaluated=false` renders **Not
evaluated**, never No setup; evaluated NONE is **NONE / No setup**. Canonical
DEGRADED and selected REJECTED quality remain explicit beside evaluation quality.

The instrument dialog exposes stable/historical identity, configured/held/mandate/
rank, eligibility and setup reasons, all nine episode fields, every technical
feature, field availability/reasons, observation and selected-current provenance,
source/revision/known/retrieved times, canonical quality, sequence/seed starts and
reference snapshot IDs. Null stays unavailable; legitimate zero stays numeric.
No action, recommendation, confidence, urgency or order controls are introduced.

### Browser, accessibility and regression results

The new browser case uses the existing Chrome/CDP stack, owned disposable database
and private API working/reference directory. It covers discovery without portfolio,
shared/outside held rows, real filters, pinned pages, reverse-release of two actual
HTTP responses whose cancellation is deliberately ignored, and a selected revision
that causes an actual 409 followed by explicit refresh. Empty/malformed temporary
references exercise BLOCKED/503; known exclusions exercise COMPLETE with zero
candidates; optional-feature warmup exercises PARTIAL with a valid confirmed setup.

The max-ten PILOT fixture normally cannot fill a twenty-row page. A **test-only
browser fetch shim** requests limit 2 from the real API to exercise Previous/Next.
The unmodified production default-20 request is separately asserted and captured.
No fixture values or shim enter the production frontend or operational files.

All six widths **1440, 1366, 1280, 1024, 768, 390** pass body-overflow and last-column
reachability assertions. Screenshots at every width were inspected, along with
light/dark views, mobile dialog top/bottom, horizontally scrolled tables and the
409/BLOCKED/COMPLETE states. Numeric alignment, controls, held rows, wrapped hashes,
full data-state columns and dialog actions remain reachable. Keyboard checks cover
context/filter/paging controls, dialog Tab containment, Escape and returned focus;
labels, scoped headers/captions, busy labels, status/alert notices, text state badges,
visible focus and existing reduced-motion/theme behavior are retained.

| Check | Actual M5 result |
| --- | --- |
| Canonical `rtk proxy dotnet build` | PASS, **0 warnings, 0 errors** |
| Plain canonical `rtk proxy dotnet test` | **305 discovered: 296 passed, 9 expected database opt-in skips**, 0 failures |
| Owned disposable canonical .NET discovery via evidence wrapper | **305/305 passed**, 0 skipped |
| Existing Screener SQL/HTTP discovery | **11/11 existing cases passed**; the added browser case initially opt-in skipped, then **1/1 passed** in targeted enabled discovery |
| Collector discovery with `IDX_EXPERIMENT_VERIFY_DB=1` | **61/61 passed**, 0 skipped; ordinary discovery also passed with its 1 expected DB fingerprint skip |
| `npm test` | **50/50 passed**, 0 skipped: 14 existing + 36 new |
| `npm run build` | PASS using Node **24.19.0**, existing dependencies only |
| Existing disposable portfolio/exchange/Chrome discovery | **8/8 passed**, including actual reconciliation, draft/registration, transactions, thesis, exchange, responsive and keyboard flows |
| Established offline `check_pilot_restore.py` | **14/14 groups passed**, 0 provider requests |
| `git diff --check`; frozen/backend/reference comparison to HEAD | PASS; frozen contract, backend, migrations, existing .NET tests, collector and pilot files unchanged |

Reproduction uses the existing commands and installed Node 22.12+; build first:

```bash
rtk proxy dotnet build
rtk proxy dotnet test
cd frontend
npm test
npm run build
cd ..
IDX_EXPERIMENT_VERIFY_DB=1 PYTHONPATH=collectors/python/src python3.13 -m unittest discover -s collectors/python/tests -v
IDX_TEST_BROWSER=1 IDX_TEST_NODE="$(command -v node)" python3.13 -m unittest discover -s scripts -p 'test_screener_*.py' -v
IDX_TEST_BROWSER=1 IDX_TEST_NODE="$(command -v node)" python3.13 -m unittest discover -s scripts -p test_portfolio_exchange.py -v
python3.13 scripts/check_pilot_restore.py
```

### Recovery, safety and remaining readiness

Screener has no persisted results to restore. Preserve the source-controlled
`pilot/screener-reference.json` together with `pilot/sessions.json` and
`pilot/instrument-sessions.json`, using their exact dated versions alongside the
database backup. Existing pilot JSON fingerprint coverage already includes the new
Screener reference. Raw re-ingestion still cannot reproduce original canonical
knowledge timestamps; exact historical recovery requires the established database
backup, retained raw provenance and the matching reference files. No restore or
portfolio-export semantics were changed to persist a transient response.

Docker **29.8.1** was available. Existing disposable harness cleanup completed.
Before/after checks preserved **all 11 operational canonical/raw/registry/listing/
calendar/portfolio tables** and **14 pilot/reference/operation files**; the same
checks also surrounded the offline restore invocation. This includes the new empty
Screener reference and authoritative operation/soak state. Browsing made **0 API
writes and 0 external requests**; provider calls **0**. FullIdx remains **DISABLED**
and rejected. No migration, result persistence, threshold change, backend semantic
change, new dependency, AI, chart or Stock Detail was added. No commit or push.

Implementation and fixture acceptance are complete. Real retained knowledge-dated
membership/identity/trading evidence, price continuity/event clearance, independently
certified volume basis and sufficient 20/50/60 history remain readiness limitations.
The real reference remains empty and real BLOCKED/WARMUP/Unavailable is expected.
Do not populate it from tests, shorten warmup, infer equity from current registry,
or enable FullIdx to manufacture candidates. Larger universes, richer Stock Detail/
charts, alternative setups and portfolio decision workflows require separately
authorized post-V0.1 work; no recommendation or rule promotion is implied here.

## Screener V0.1 — Milestone 6 data-readiness/evidence, 2026-10-02

**Pass 1 (read-only operational audit) is accepted and preserved below.** Starting
state was clean `main` at `39268c0` (`feat: complete screener v0.1 production UI`);
M1–M5 are committed and there was **no pre-existing M6 diff** to continue. M6 is the
data-readiness/evidence milestone required by contract sections Q/W-step-7. No
Screener code, frozen policy, migration, dependency or provider call was made and no
operational table was written. **Pass 2** (authoritative-source review, below) added
exactly one substantiated PILOT universe snapshot to `pilot/screener-reference.json`;
the `instruments` array remains empty and unknown evidence stays unknown.

### Read-only operational evidence audit

The audit queried the operational PostgreSQL database inside `BEGIN READ ONLY`
transactions only.

| Observation | Actual value |
| --- | --- |
| Canonical bar revisions | **76** rows; **11** instrument IDs (10 configured pilot stock candidates + `JKSE.INDX` benchmark); **7** dates 2026-08-24..2026-09-30 |
| Canonical quality | `DEGRADED` for all 76 rows; zero `VALID` |
| Volume basis / segment | `SPLIT_ADJUSTED` / `UNKNOWN` for all rows |
| Volume unit | `SHARE_COUNT_CORROBORATED` 70, `UNKNOWN` 6 |
| Source | single `eodhd`; `terms_status = UNKNOWN` |
| Raw artifacts | 34 |
| `instrument` types | 11 × `UNKNOWN` (no `EQUITY`/`INDEX` label) |
| `instrument_history` | 0 rows |
| `instrument_listing_evidence` | 18 rows / 11 IDs |
| `market_session` | 0 rows |
| `pilot/instrument-sessions.json` | `[]` (no status or no-trade evidence) |
| Portfolio | 1 header, 0 events, 0 theses (no held IDs) |

Each of the ten configured stock candidates has exactly **7** observed dates; `JKSE.INDX` has **6**
(missing 2026-09-29). No instrument reaches the 15-observation ATR floor, the
21-observation setup minimum, the 50-observation EMA50 window or the 61-observation
RS60 window. Session evidence currently proves five 2026-09 dates plus 2026-08-24/26;
2026-09-28 has no open-session proof, so the recent suffix is not a continuous
20/50/60-session sequence. `instrument-sessions.json` is empty, so no affirmative
no-trade/suspension evidence exists.

Listing evidence (reused by M2/M3 through `InstrumentBoundaries.AsOf`) carries
VERIFIED listing dates for BBCA, BBRI, ANTM, DSSA, ENRG, GOTO, RAJA (2003-01-22),
VKTR (2023-06-19) and PTRO (1990-05-21; a later `known_at` VERIFIED assertion
supersedes its earlier PARTIAL candidate). LPIN remains year-only `PARTIAL` with
`listed_from` null; `JKSE.INDX` is `NOT_APPLICABLE`. These are **listing boundaries
only**, not classification, board, mechanism or basis clearance.

### Pass 2 — authoritative-source evidence review

Pass 2 looked for primary sources per candidate. Pages were fetched directly; search
snippets were **not** used as evidence. KSEI (the Indonesian central securities
depository) was reachable and authoritative. `idx.co.id` and `e-ipo.co.id` returned
**HTTP 403** to the fetcher and `eodhd.com/exchange/INDX` returned **404**, so those
facts remain unresolved rather than guessed. No fact below is inferred from ticker
text, the mutable registry, or common knowledge.

| # | Source (URL) | As-of / publication | Retrieved | Exact fact supported | Limitation | Scope (current vs interval) |
| --- | --- | --- | --- | --- | --- | --- |
| S1 | `https://web.ksei.co.id/Download/StatisEfek20260930.txt.zip` (title: Master File Efek 30 September 2026) | 2026-09-30 | 2026-10-02 | KSEI registered-securities master as of 2026-09-30: all ten candidates appear with `Type=EQUITY`, `Status=ACTIVE`, `Stock Exchange=IDX`, `Currency=IDR`, an ISIN and (six) listing dates | Broad `EQUITY` class only; no ordinary/preferred split, no board/mechanism, no price/volume basis or segment; single dated snapshot | Dated current-at-2026-09-30 observation, not a historical interval |
| S2 | `https://web.ksei.co.id/services/registered-securities/shares/lc/<SYMBOL>` (ten pages, title: Efek Terdaftar — issuer) | page "As of 1 Oct 2026" | 2026-10-02 | Every candidate's type is `Saham Biasa` (ordinary shares), `IDR`, status `Active`, exchange `IDX`, with ISIN and a dated corporate-action list | Current classification only (no effective-dated interval); no board/mechanism/segment; corporate-action list completeness not guaranteed | Current classification observation; the evaluated Aug–Sep 2026 window is not covered by an effective interval |
| S3 | `https://www.gotocompany.com/news/press/goto-tercatat-di-papan-utama-bei` (issuer primary) | 2022-04-11 | 2026-10-02 | GOTO listed and traded on the IDX Main Board (`Papan Utama`) under code GOTO from 2022-04-11; first listing under OJK multiple-voting-share rules (`saham Seri A`) | Listing-day board only; no current board/mechanism; MVS is a nuance for ordinary-share treatment | Historical listing-day fact |
| S4 | `https://eodhd.com/financial-apis/api-for-historical-data-and-volumes` (retained, `docs/EODHD_SEMANTICS_RIGHTS.md`) | documented contract; retained 2026-09-29 | 2026-10-02 (retained) | Provider feeds raw as-traded OHLC plus a separate adjusted close, and split-adjusted volume | Provider documentation, not an exchange tape; does not establish the JK regular-market segment | Provider semantics, not instrument-dated |
| S5 | `https://rdis.idx.co.id/en/events/mengenai-satuan-lot-apa-itu-lot` (retained) | IDX education | retained | IDX convention: 1 lot = 100 shares | Defines lots, not the provider's transformation or segment | Convention, not instrument-dated |

Unavailable during this pass (no fact taken): IDX company-profile page/API (403),
`idx.co.id` (403), `e-ipo.co.id` (403), `eodhd.com/exchange/INDX` (404).

**Current classification.** KSEI per-security pages show `Saham Biasa` for all ten —
BBCA, BBRI, ANTM, RAJA, VKTR, ENRG, PTRO, DSSA, GOTO, LPIN — with `IDR`, `Active`,
`IDX` and ISINs `ID1000109507`, `ID1000118201`, `ID1000106602`, `ID1000094105`,
`ID1000190309`, `ID1000098304`, `ID1000122401`, `ID1000113400`, `ID1000166903`,
`ID1000086408`. This is a **current** observation and is not an effective-dated
interval reaching 2026-08-24..2026-09-30.

**Corporate actions listed for the window.** BBCA cash dividend (cum 2026-09-01);
DSSA proxy voting (cum 2026-08-11); GOTO proxy voting (cum 2026-09-21); none for
BBRI, ANTM, ENRG, PTRO, RAJA, VKTR, LPIN. ENRG (`2 : 1 ENRG-R`, cum 2026-10-02) and
VKTR (`35 : 12 VKTR-R`, cum 2026-10-06) are **after** the window. No in-window
unit-changing event was listed for any candidate; the RAJA 1:5, DSSA 1:25 and PTRO
1:10 conversions are before it. Cash dividends may remain in a verified raw-price
sequence and proxy voting is not price-affecting, but a KSEI list is not certified
complete coverage, so price continuity is still not "cleared".

### Universe snapshot decision

The frozen contract defines a universe snapshot as **configured experiment
membership**, not historical exchange membership. The retained `pilot/universe.json`
is the authoritative definition of that configuration and is public and
commit-pinnable, so it **is** sufficient evidence for configured membership. Exactly
one substantiated snapshot was created:

- `snapshotId` = `pilot-universe-2026-10-02`; `knownAt` = `2026-10-02T07:31:15.1843500Z`
  (actual capture, not backdated); `universeId` = `PILOT`; 10 `memberIds`;
  `benchmarkId` = `76237e96-232f-5085-9b13-dcb7104222bc` (`JKSE.INDX`).
- One evidence record cites the pinned public configuration
  `https://raw.githubusercontent.com/rizkyari828/idx-stock-intelligent/7c501136ba924f6dc265fed4af2872de5c2fe06a/pilot/universe.json`
  (`publishedAt` 2026-09-28T09:24:41Z; retrieved/known 2026-10-02).
- `contentHash` = `d7a3266cd9aaa360e8ea6c373866c117f8fc3a13f4db2489cede297b1149aa8f`,
  verified by the application's own `ScreenerReferences.Parse`/`SnapshotHash`.

`pilot/universe.json`, the configured ceiling and FullIdx are unchanged. Cutoffs at
or after `knownAt` now see a known PILOT universe; earlier cutoffs still return
`UNIVERSE_NOT_KNOWN`.

### Instrument snapshot decision

**No instrument reference snapshot was created or updated.** The substantiated
classification/currency/status facts are current observations (KSEI as of
2026-09-30/2026-10-01); applying them to the Aug–Sep window would require backdating
capture to a listing date, which the contract forbids. Board, mechanism and an
explicit-end trading-status interval are unsubstantiated, and price/volume clearance
cannot be supplied (sections G/H below). A partial snapshot would yield no eligibility
and risk implying more than is known, so unknown stays unknown and `instruments`
remains `[]`.

### Clearance result

The reference schema needs, per instrument, a `knownAt`-visible full snapshot with an
identity interval (`ORDINARY`/`INDEX`, `IDR`, supported board), a trading interval, a
price interval (`RAW_AS_TRADED` + `STOCK_RAW`/`INDEX_LEVEL` + `CLEARED`/
`NOT_APPLICABLE` + matching `sourceId` + selected content hashes) and a volume interval
(non-`UNKNOWN` unit/basis/segment). The substantiated evidence still does not supply
those knowledge-dated attestations for the evaluated dates:

- Ordinary classification: current-only (KSEI `Saham Biasa`), no effective interval.
- Board/mechanism/status: only GOTO's 2022 listing-day Main Board; current board and
  mechanism `UNKNOWN`; KSEI `Active` is current-only with no explicit-end interval.
- Price basis: no `RAW_AS_TRADED`/`CLEARED` event-coverage attestation and no
  selected-hash clearance.
- Volume basis: every selected bar has `market_segment = UNKNOWN`, so volume
  clearance cannot be satisfied for any instrument under the frozen rule.

No instrument can be promoted to `ELIGIBLE` without fabricating evidence. The honest
real outcome remains BLOCKED/WARMUP/Unavailable, never a candidate list.

### Current live Screener result

`GET /api/screener?through=2026-09-30&view=all` (default cutoff = now, API run from
the repository root, read-only, then stopped):

- `status` = **BLOCKED**; `reasons` = `["INCOMPLETE_COVERAGE","RAW_PRICE_RETURNS"]`;
  `universeSnapshotId` = `pilot-universe-2026-10-02`; `targetSession` = `2026-09-30`.
- `summary`: configured 10, eligible 0, ineligible 0, dataBlocked 10, evaluated 0,
  candidates 0, confirmed 0, watch 0, shortlisted 0, insufficientHistory 10, stale 0,
  unsupported 0, held 0, heldOutsideUniverse 0.
- `marketContext`: trend/volatility `UNKNOWN`/`UNKNOWN`, reason `REFERENCE_NOT_KNOWN`.
- 10 rows, all `DATA_BLOCKED` (`REFERENCE_NOT_KNOWN`, `STATUS_UNKNOWN`,
  `INSUFFICIENT_HISTORY`, some `LISTING_UNKNOWN`), `setup=NONE`, `NOT_EVALUATED`.
- `inputHash` = `c4491e4f78d629dc631eab80c3de107e2f307fec3394fac98a5b747b74ab368b`.

### Final M6 report (A–Q)

**A. Files changed.** `pilot/screener-reference.json` (one PILOT universe snapshot;
`instruments` still empty) and `docs/PRODUCT_SLICE_RUNBOOK.md` (this record). No source
code, test, migration, frozen-contract, collector, frontend, or operational file
changed; `bin/`/`obj/` build output is gitignored.

**B. Authoritative sources reviewed.** S1–S5 above, all directly fetched or retained
with URLs/titles/as-of dates; HTTP-failure sources are recorded as unresolved.

**C. Universe snapshot status.** Created: `pilot-universe-2026-10-02`, actual
`knownAt` 2026-10-02, 10 configured member IDs, `JKSE.INDX` benchmark, evidence pinned
to the public configuration commit `7c50113`, verified `contentHash`
`d7a3266c…aa8f`. Configured experiment membership only.

**D. Instrument reference snapshots added/updated.** None (rationale above). No
instrument fact is asserted beyond the current, sourced observations in E.

**E. Identity/classification readiness.** Current `Saham Biasa` (ordinary) + `IDR` +
`ACTIVE` + ISIN substantiated for all ten (KSEI, 2026-09-30/10-01). Effective-dated
interval for the Aug–Sep window: **not substantiated**. GOTO carries multiple-voting
Series A shares.

**F. Board/mechanism/status readiness.** Board: only GOTO Main Board at listing
(2022-04-11); current board `UNKNOWN` for all (IDX 403). Mechanism: `UNKNOWN` for all.
Trading status: KSEI `Active` current-only; no explicit-end interval, so not usable as
a frozen trading interval.

**G. Price-basis readiness.** Not cleared. No `RAW_AS_TRADED`/`CLEARED` attestation;
all 76 canonical bars are `DEGRADED`. In-window corporate actions are limited to a
BBCA cash dividend and non-price proxy votes; no in-window split/consolidation was
listed, but absence of a listed event is not certified completeness.

**H. Volume-basis readiness.** Not cleared. Every bar is `volume_basis=SPLIT_ADJUSTED`,
`market_segment=UNKNOWN`, unit `SHARE_COUNT_CORROBORATED` (70) or `UNKNOWN` (6). KSEI
does not state volume basis/segment; the provider documents split-adjusted volume but
no JK segment. The rule that `UNKNOWN` metadata is not verification is unchanged.

**I. Per-instrument history/readiness matrix.** All ten configured candidates have 7
bars (2026-08-24..2026-09-30); `JKSE.INDX` has 6 (missing 2026-09-29). No instrument
reaches 21 continuous observations, so all are `DATA_BLOCKED`
(`REFERENCE_NOT_KNOWN` + `STATUS_UNKNOWN` + `INSUFFICIENT_HISTORY`; some
`LISTING_UNKNOWN`). Listing evidence: BBCA 2000-05-31, BBRI 2003-11-10, ANTM
1997-11-27, RAJA 2003-01-22, VKTR 2023-06-19, ENRG 2004-06-07, PTRO 1990-05-21, DSSA
2009-12-10, GOTO 2022-04-11 are retained as verified; LPIN remains year-only partial.
No candidate is `ELIGIBLE`.

**J. IHSG readiness.** `JKSE.INDX` has 6 bars with an interior gap at 2026-09-29 and no
instrument snapshot, so `marketContext` is `UNKNOWN`/`UNKNOWN` with
`REFERENCE_NOT_KNOWN`; RS20/RS60 are unavailable. Missing index context does not veto
an otherwise valid stock breakout.

**K. Current live Screener result.** See the section above: `BLOCKED`,
`INCOMPLETE_COVERAGE`/`RAW_PRICE_RETURNS`, configured 10 / dataBlocked 10 / candidates
0, `inputHash` `c4491e4f…368b`, known `universeSnapshotId`.

**L. Soak status.** `pilot/soak.json` is `after_market_date=2026-09-28`,
`required_completed_runs=10` → **1/10**; FullIdx remains **DISABLED**; provider calls
**0**.

**M. Evidence that remains unresolved.** Current-and-historical board/mechanism for
all; classification as an effective-dated interval for 2026-08-24..2026-09-30; KSEI
corporate-action completeness; volume basis/segment; `RAW_AS_TRADED` continuity;
exact LPIN listing day; GOTO MVS/current board; IDX and e-IPO sources (403);
post-cancellation retention rights.

**N. Exact test results.** `rtk proxy dotnet test`: **305 total, 296 passed, 9 skipped
(opt-in disposable PostgreSQL), 0 failed**. `rtk proxy python3.13 -m unittest discover
-s scripts -p 'test_screener_*.py' -v`: **12 tests, 11 passed, 1 skipped** (opt-in
browser), with the embedded owned-DB .NET discovery **305/305 passed, 0 skipped** and
operational database/reference/operation fingerprints unchanged before/after. Release
builds of `IdxStockIntelligence.Application` and `IdxStockIntelligence.Api`: **0
warnings, 0 errors**. A temporary harness confirmed `ScreenerReferences.Parse` accepts
the new reference and `Universe` selects the snapshot. Not rerun: frontend `npm
test`/`npm run build`, portfolio exchange, offline restore, collector discovery.

**O. Operational fingerprint/provider/FullIdx safety.** The operational database was
read-only throughout; counts are unchanged (76 bars, 1 portfolio, 0 events, 0 theses,
18 listing rows) and the disposable harness asserts unchanged fingerprints. The only
API interaction was a read-only `GET`; provider calls **0**; FullIdx **DISABLED**; soak
**1/10**. No migration, index, dependency, code or frozen-policy change.

**P. Next recommended data-readiness action.** Obtain knowledge-dated, effective-dated
authoritative evidence for the pilot window: IDX board/mechanism and trading status
via an accessible official route, and provider/segment confirmation of volume basis
and `RAW_AS_TRADED` continuity tied to selected revision hashes; separately resolve
LPIN's exact listing day and GOTO's MVS/current board. Because the Aug–Sep window
predates capture, the practical path is prospective retention (continue the soak) plus
a new reference snapshot once dated evidence exists. Do not backdate or infer.

**Q. Git status.** Uncommitted, unpushed:
`M docs/PRODUCT_SLICE_RUNBOOK.md`, `M pilot/screener-reference.json`. `git diff
--check` is clean.

### Verification and operational safety

All database work was read-only (`BEGIN READ ONLY`); the operational database, the
`pilot/` inputs and authoritative operation/soak state are unchanged. Provider calls
were **0** and FullIdx remains **DISABLED**. This milestone produces no candidate, no
migration, no code and no recommendation; the only new retained evidence is the
substantiated configured-membership universe snapshot. Real Screener candidate
readiness is still not established.

### Local project-launch path correction (2026-10-03)

`dotnet run --project src/IdxStockIntelligence.Api` previously launched with the API
project as its working/content directory, missing repository `pilot/` references
and `frontend/dist`. The API project now sets the native `RunWorkingDirectory` to
the repository root. Use the same command from the repository root with the existing
private `IDX_DATABASE_CONNECTION`; no `IDX_UI_ROOT` override is required for the
repository's built frontend. Direct DLL launches still use their caller's working
directory, including disposable acceptance fixtures. Screener reads the three fixed
reference files there; it does not fall back to mutable `pilot/universe.json`.

The reported service-error banner/503 was not reproduced: port 5080 was initially
not listening. Before correction, both requested dates returned HTTP 200 BLOCKED
but the project launch missed the retained universe snapshot. After correction,
real read-only requests for Sep 30 and Oct 2 both return HTTP 200 BLOCKED with
configured **10**, dataBlocked **10**, candidates **0**. Oct 2 retains
`through=2026-10-02`, separately reports `targetSession=2026-09-30`, and reports
`SESSION_UNCONFIRMED`; Sep 30 reports `INCOMPLETE_COVERAGE`. No completion or
instrument evidence is invented. The production parser/hash validation accepts the
unchanged M6 reference snapshot.

One disposable HTTP regression checks the normal project launch against the retained
references and default frontend root; it failed before the correction and passes
after it. Verification: `dotnet build --no-restore` **0 warnings/errors**;
`rtk proxy dotnet test` **305 total, 296 passed, 9 opt-in database skips**;
`rtk proxy python3.13 -m unittest discover -s scripts -p test_screener_http.py -v`
**12 tests, 11 passed, 1 opt-in browser skip**. A separate temporary headless Chrome
check of the real 5080 React page (Oct 2, blank Known by, Discovery only) renders
BLOCKED without the service-error banner and makes only local GET requests.
Frontend code/tests/build and unrelated suites were not changed or rerun.

All operational database/portfolio, retained reference/operation-ledger and frozen
contract fingerprints remain unchanged. Provider calls **0**; FullIdx remains
**DISABLED**; no migration, dependency, evidence-policy or contract change.

### Benchmark admission correction and Oct 2 soak disposition (2026-10-03)

The Oct 2 DAILY operation `ff959f1e-b462-4db0-94bb-8f1254d0ec58` fetched all
eleven rows, but the shared pilot validator applied stock zero-volume/tradability
rules to `JKSE.INDX`. Its zero volume made the run DEGRADED (10 accepted rows),
soak_eligible=false. This conflicts with the frozen contract's index price semantics
(D: index prices do not require stock-like volume) and the retained source review's
verified JKSE identity/close evidence (`docs/EODHD_SEMANTICS_RIGHTS.md`).

The worker now supplies the fixed configured benchmark ID to the shared validator.
Only that benchmark may have an AVAILABLE observed price with zero volume;
stock suspension/no-trade requirements remain unchanged. Benchmark tradability is
reported NOT_APPLICABLE, not TRADED. Actual volume and UNKNOWN volume metadata
remain preserved; no index volume, session, price clearance or Screener eligibility
is manufactured. Soak qualification predicates and Screener policy are unchanged.

Retained Oct 2 evidence re-evaluates to 11 accepted rows/SUCCEEDED in a disposable
database without refetching. Both DAILY-in-owned-DB and OFFLINE_REPLAY remain
non-qualifying. The original operational DEGRADED report is preserved: the frozen
contract provides no retrospective qualification procedure. **Soak stays 1/10**
(only Sep 30); operational Oct 2 benchmark admission was not replayed or promoted.

Verification: focused PilotTests **10/10**; build **0 warnings/errors**; standard
.NET discovery **308 total, 299 passed, 9 opt-in database skips**; disposable
`python3.13 -m unittest discover -s scripts -p test_pilot_validation.py -v`
**1 test passed**, covering DAILY and OFFLINE_REPLAY. Operational/reference/ledger
fingerprints and all eleven retained payload hashes are unchanged; original
retrieval/knownAt timestamps and frozen contract are preserved. Provider requests
and units **0**; FullIdx remains **NOT ENABLED**. No dependency or migration added.

## Stocks + Stock Detail V0.1 (2026-10-03)

Stocks now opens without a selected portfolio. It searches locally retained symbol
or issuer names, orders by display symbol then stable UUID, and pages 20 rows at a
time. It includes the locally retained benchmark; it does not discover or collect
FullIdx. UNKNOWN type, unavailable price, original market date, stale observations
and canonical quality remain visible. Effective registry symbol history takes
priority. Where it is absent, the latest retained listing symbol known now is a
**current display identity only**, labelled `RETAINED_LISTING`; otherwise the
issuer name is displayed with `UNAVAILABLE` symbol source. This never infers type,
board/status, currency or historical Screener eligibility.

Open `/stocks/{instrumentId}` directly, click a Stocks symbol, use the Stock Detail
link beside a Screener symbol, or use Open Stock Detail in the existing Portfolio
holding dialog. Existing symbol/holding dialogs remain available. UUID routes,
browser back and explicit invalid/missing-ID states are supported. Stock Detail
keeps current registry metadata separate from knowledge-dated evaluation evidence.
Currency is UNKNOWN when absent from the registry.

Through defaults to Jakarta today. Blank Known by resolves on the history API;
the returned exact UTC cutoff is displayed and reused for Screener and optional
portfolio reads. Draft edits cancel/invalidate old results and require Apply
context; Now resolves a fresh cutoff. Aborted/older requests cannot publish over a
newer context. Selected portfolio context is factual: held shares, cost, market
value/P&L when available, mandate and thesis version/status; otherwise Not held.
The Stocks portfolio picker only opens existing portfolios.

### Read APIs and data semantics

- `GET /api/instruments` retains its array and existing identity fields. Added
  optional `search` (literal case-insensitive substring, max 200 characters) and
  `cutoff`; existing `through`, `offset` (0..10000) and `limit` (1..200, default
  100) remain. Added `symbolSource`, `marketDate`, nullable `close`, and `quality`.
- `GET /api/instruments/{id}/history?through=YYYY-MM-DD&cutoff=<offset ISO8601>&limit=60`
  returns current display metadata/registry date, resolved through/cutoff, requested
  limit and `MARKET_DATE_ASCENDING` rows. Limit is 1..120; default 60. Unknown UUID
  returns 404; invalid dates/cutoffs/limits return 400 under existing query rules.
- History selects the latest **visible** revision per market date after checking
  canonical knownAt, raw fetchedAt, retrievedAt and session-knownAt against cutoff,
  and market date against through. It takes the latest bounded dates, then presents
  them ascending. Invalid selected revisions remain present with null OHLC/volume,
  an unavailable reason and unchanged raw evidence; no superseded-price fallback.
- List and detail reuse the same canonical evidence decoder/validation. History
  uses a read-only repeatable-read transaction, positional SQL, cancellation and
  15-second commands. No migration, new persistence or provider access is involved.
- The native SVG close chart uses market-date spacing and observed points, breaks
  at unavailable selected observations, and requires at least two usable prices.
  The table is authoritative; missing sessions are not synthesized. Observed
  prices/volume do not establish certified price comparability or volume basis.
- Technical values, availability/reasons, eligibility, setup/episode and benchmark
  context come from the existing PILOT Screener API/evaluator. No second indicator
  engine or state machine exists. WARMUP/UNAVAILABLE/DATA_BLOCKED stay intact;
  outside-scope instruments say Not evaluated by current Screener universe, not
  setup NONE. Genuine observed zero stays zero, including index volume; it is not
  converted into equity tradability or volume certification.
- Expand Data & Provenance for selected revision, quality, raw OHLC, source, hash,
  retrieval/knowledge/session clocks, volume metadata and Screener references.
  Missing historical identity/reference/basis remains UNKNOWN. A Screener or
  portfolio read failure is local to that section; retained history remains visible.

### Run and verify

Build the frontend with the existing Node 22.12+ workflow above, then start/restart
`dotnet run --project src/IdxStockIntelligence.Api` from repository root with the
existing private `IDX_DATABASE_CONNECTION`. Open http://127.0.0.1:5080/stocks.
No provider token is needed. No operational process was replaced during acceptance;
the real smoke used a temporary loopback API running the new build against the
operational database and cleaned up afterward.

Executed verification:

- `rtk proxy dotnet build`: **PASS, 0 warnings/errors**.
- `rtk proxy dotnet test`: **311 total, 302 passed, 9 expected database opt-in
  skips, 0 failed**. Three new .NET cases cover canonical invalid/zero preservation.
- `rtk proxy python3 -m unittest discover -s scripts -p test_screener_evidence.py -v`:
  **1 harness test passed**, enabling standard .NET discovery: **311/311 passed,
  0 skipped/failed**. Owned PostgreSQL database removed; operational fingerprints
  unchanged.
- In `frontend`, `npm test`: **60/60 passed, 0 skipped/failed**, including ten new
  Stocks cases; `npm run build`: **PASS**. The locked dependencies were reinstalled
  with `npm ci --ignore-scripts` to restore a missing local Vite native binding;
  package manifests/lockfile remain unchanged.
- `IDX_TEST_BROWSER=1 IDX_TEST_NODE=<Node22+ executable> python3 -m unittest discover
  -s scripts -p test_stocks_http.py -v`: **5/5 passed**, four focused HTTP cases plus
  real production Chrome acceptance. All required widths **1440/1366/1280/1024/768/390**
  pass for list, pilot detail, unknown/no-price detail and provenance. Search,
  stable routes/back, one-observation chart absence, stale history, WARMUP, context
  invalidation, held/unheld historical replay, Screener/Portfolio links, invalid
  and missing UUIDs, GET-only traffic and zero external requests are verified.
  The final chart/presentation change was followed by another successful focused
  browser case. Disposable APIs/databases and Chrome profiles were cleaned up.

### Real operational smoke and safety

Read-only `through=2026-10-03`, blank cutoff, exact resolved cutoff reused by Screener:

| Instrument | HTTP | Retained dates | Latest date | Latest observed close | Quality | Screener context |
|---|---|---:|---|---:|---|---|
| ANTM.JK | 200 | 8 | 2026-10-02 | 3140 | DEGRADED | BLOCKED / DATA_BLOCKED; setupEvaluated=false |
| BBCA.JK | 200 | 8 | 2026-10-02 | 6100 | DEGRADED | BLOCKED / DATA_BLOCKED; setupEvaluated=false |
| JKSE.INDX | 200 | 6 | 2026-09-30 | 6071.1382 | DEGRADED | BLOCKED benchmark context |

All three keep UNKNOWN registry type/currency and `RETAINED_LISTING` display symbol
source. Technical context is UNAVAILABLE (`REFERENCE_NOT_KNOWN`), not fabricated
WARMUP/zero. Latest dates remain older than through. The retained rejected Oct 2
benchmark has not been operationally replayed/admitted. A separate native Chrome
smoke of all three real detail pages passed, including ANTM at 390px: UNKNOWN,
STALE, reference-unavailable and DATA_BLOCKED states render without a service-error
banner, console exceptions, browser writes or external requests.

All **11 operational table fingerprints** and **15 pilot/reference/operation file
hashes**, including Screener references/session proofs, match the initial baseline.
Canonical rows remain **86**; portfolio/event/thesis data is unchanged. Frozen
contract SHA-256 remains
`93d2b02f221cb87ea8d25b32f6d0394ecc3a9ef9f067bb9c6fc559c1379fef26`.
Soak remains **1/10**, only **2026-09-30**, after_market_date **2026-09-28**.
Provider calls/units **0/0**, FullIdx **NOT ENABLED**. No new dependency, migration,
policy, recommendation/action field, commit or push.

Remaining limits: registry currency/type/effective symbol history remain incomplete;
retained display identities do not solve historical reference readiness. Technical/
setup context stays within the existing frozen PILOT scope/horizon. History is a
bounded retained-observation view, not complete exchange-session coverage or a
certified comparable-price series. Context is shared across existing read APIs;
there is no new combined cross-API transactional snapshot or persistence model.

## Prospective Decision Snapshot V0.1 — design only (2026-10-03)

`docs/DECISION_SNAPSHOT_V0_1_CONTRACT.md` freezes a proposed immutable run plus
configured/held-union rows, actual server cutoff/recording clocks, existing
Screener inputHash and retained selected-evidence linkage. Decision: conditional
GO for a separate implementation milestone after review and owner resolution of
the pre-existing pasted command block in the Screener contract. No schema, API,
UI or behavior is implemented here. The frozen Screener policy is unchanged;
capture will preserve BLOCKED/PARTIAL output and remain PILOT-bounded. The existing
soak 1/10 and disabled FullIdx gates are independent. Verification and forward
outcomes are deferred; no database/provider/collector/soak actions were performed.

## Prospective Decision Snapshot V0.1 — implemented (2026-10-03)

The separate implementation milestone started from clean `bcc9872` on `main`
(five commits ahead of `origin/main`). The preceding design-only entry describes
that earlier milestone; API/persistence are now implemented against the frozen
`docs/DECISION_SNAPSHOT_V0_1_CONTRACT.md`. Both frozen contracts remain unchanged.
There is no Decision Snapshot UI, outcome tracking or historical verifier.

### Migration and persistence

`src/IdxStockIntelligence.Infrastructure/Migrations/0005_decision_snapshots.sql`
adds immutable `decision_snapshot_run` and `decision_snapshot_row`, under the
existing transactional advisory-lock/version convention. Apply only 0005 to an
initialized version-4 operational database; do not reapply 0001. Disposable fresh
0001–0005, version-4 upgrade and controlled rerun all passed before operational
application. The version registry now contains **2, 4, 5**, following existing
registry conventions. A final controlled operational rerun installed the tested
restore guard without changing either snapshot table's contents.

One run stores the typed factual result, existing inputHash/selectedDigest and
selected-evidence manifest; rows cover the full configured population union
positive holdings before filtering, shortlist or paging. Benchmark context stays
on the run. The maximum is **210 rows**. There is no registry FK on row instrument
IDs: absent mutable registry coverage must still allow honest DATA_BLOCKED capture.
No accounting/canonical table, migration 0001–0004 or dependency was changed.

Database triggers reject UPDATE, DELETE and TRUNCATE of either snapshot table.
Rows must be inserted in the header's actual creation transaction; the guard checks
both originating transaction ID and native header `xmin`, including protection
against a transaction-ID coincidence after restore. Deferred checks reject
incomplete populations, inconsistent dates/episode identities/membership and
invalid portfolio projections. Full dump/restore and a restored legacy-ID collision
are covered. Seven indexes (including primary/unique indexes) and seven noninternal
triggers were verified.

### API and chronology

- `POST /api/screener/decision-snapshots`: JSON `requestId` UUID required;
  `through` and `portfolioId` optional. Unknown/duplicate keys and caller-supplied
  clocks, policy or results are rejected. First capture returns **201**; identical
  normalized intent with the same requestId returns the original run with **200**;
  conflicting intent returns **409**. Invalid/prohibited requests return **400**,
  missing requested portfolio **404**, and service/reference/archive/integrity
  failures **503**. Existing transport limits/content-type rules still apply.
- `GET /api/screener/decision-snapshots/{runId}` returns `{header,result,rows}`.
- `GET /api/screener/decision-snapshots` returns recent headers, optionally filtered
  by `portfolioId`.
- `GET /api/instruments/{instrumentId}/decision-snapshots` returns captured
  instrument history. Both list APIs return `{items,nextCursor}`, use descending
  `(capturedAt,runId)` keysets, default limit **20**, maximum **100**, and reject
  cursors used with a different filter/context.

Capture resolves one actual PostgreSQL clock: `capturedAt == knowledgeCutoff`.
Through is that instant's Jakarta date; an explicit through must equal it. An
earlier completed target session is allowed without backdating. `recordedAt` is
assigned on database insertion. Existing historical GET Screener replay remains
read-only and does not create prospective snapshots.

Capture reuses the existing evaluator, episode machine, portfolio replay and hashes
inside one caller-owned read/write REPEATABLE READ transaction. Selected canonical
revision keys/hashes/clocks/raw-artifact IDs, selected reference/session identities,
portfolio event/thesis IDs and explicit absence are retained. No raw provider body,
archive filesystem path, internal transaction ID or private thesis text is exposed
by public snapshot responses. Discovery-only holding fields are null; portfolio
captures preserve factual holdings/mandates/permitted thesis metadata, including
positive holdings outside PILOT and their not-evaluated setup semantics.

Exact copied reference bytes are hashed/archived using the existing archiver under
ignored `data/raw/decision-reference`. Evaluation uses the copied bundle, never a
second read of changing live files. Archive byte length/hash are verified before
capture and immediately before commit. Archive failure prevents persistence;
deterministic orphan archives after a failed transaction may remain. Do not delete
archives required by committed runs. Restore needs both PostgreSQL records and
their ignored reference/raw archives; the database dump alone is insufficient for
future evidence verification. Verification itself remains deferred.

Existing evidence bounds remain intact, including the stricter **combined 4 MiB**
reference-bundle bound. New serialization bounds are run result **32 KiB**, row
result **8 KiB**, public response **2 MiB**, evidence manifest **32 MiB**. Capture
has a **60-second** deadline and **15-second** command timeout. Concurrent identical
requests commit one run; the loser rolls back and recovers the winner in a fresh
transaction. Lost-response retries return stored output without re-evaluating live
references or applying a new capture clock.

### Executed verification

- `rtk proxy dotnet build`: **PASS, 0 warnings/errors**.
- `rtk proxy dotnet test --filter FullyQualifiedName~DecisionSnapshotTests`:
  **37/37 passed**, 0 skipped/failed.
- `rtk proxy dotnet test`: **348 total, 339 passed, 9 expected database opt-in
  skips, 0 failed**.
- `rtk proxy python3 -m unittest discover -s scripts -p test_screener_evidence.py -v`:
  **1/1 harness test passed**, enabling canonical .NET discovery:
  **348/348 passed, 0 skipped/failed**, also rerun against final migration 0005.
- `rtk proxy python3 -m unittest discover -s scripts -p 'test_screener_*.py' -v`:
  **13 discovered, 12 passed, 1 expected opt-in browser skip, 0 failed**;
  includes the disposable-enabled canonical .NET run above and existing Screener
  HTTP acceptance. No new browser suite was required or executed.
- `rtk proxy python3 -m unittest discover -s scripts -p test_decision_snapshots.py -v`:
  **19/19 passed, 0 skipped/failed**. Covers migrations/guards/restore,
  complete configured-held population, COMPLETE/PARTIAL/BLOCKED and setup states,
  missing registry/optional fields, clocks/corrections/future exclusion, keysets,
  strict transport, concurrency, failure rollback, changed live inputs and archive
  corruption/removal during evaluation.
- `git diff --check`: **PASS**. Frontend/collector suites were not rerun; their
  source, dependencies and behavior are unchanged. All owned disposable databases
  and temporary API processes were cleaned up.

### Real operational capture and safety

After disposable acceptance and migration 0005, exactly one discovery-only capture
used real retained PILOT evidence via a temporary loopback API; the normal port-5080
process was not replaced. POST returned **201**, identical request retry **200**,
run GET **200**. Existing GET Screener at the exact captured cutoff matched the
snapshot's inputHash and summary.

- Run: `3bc25cf2-901b-4c15-84b2-e265091f7f48`.
- Request: `04693f9f-ed49-4ad8-a964-f14fe5e01a71`.
- CapturedAt / knowledgeCutoff: `2026-10-03T04:13:45.712111+00:00`.
- RecordedAt: `2026-10-03T04:13:45.838421+00:00`.
- Through: **2026-10-03**; target session: **2026-10-02**; portfolio: **null**.
- Status: **BLOCKED**; configured/dataBlocked/rows: **10/10/10**;
  candidates/held: **0/0**. Missing evidence was not promoted or fabricated.
- InputHash: `6c32994d484472bb08361905e29bf0f86d2a29c558c94ad91bf0d0e7380f9177`.
- SelectedDigest: `38ba96fba61bd32c803d6b9c2cca2e1a713a8611dfbb691496e44c4c457ed377`.
- Manifest: **86 selected bar links**, **3 reference archives**;
  retained snapshot tables: **1 run, 10 rows**.

All **11 pre-existing operational table fingerprints** and **208 protected file
hashes** match the initial baseline, including pilot references/session proofs,
retained operation/summary reports and both frozen contracts. Canonical history
remains **86 rows**; portfolio economic/event/thesis data is unchanged. New snapshot
table fingerprints also match before/after the final migration rerun. The rejected
Oct 2 benchmark was not replayed, re-collected or retrospectively qualified.
Soak remains **1/10**, qualifying date **2026-09-30**, after_market_date
**2026-09-28**. Provider calls/units **0/0**; FullIdx **NOT ENABLED**.
No collector, Screener policy, session proof, recommendation/action field,
navigation/UI, outcome/verifier service, commit or push was introduced.

To load the new endpoints in the normal local application, restart
`dotnet run --project src/IdxStockIntelligence.Api` from repository root with the
existing private `IDX_DATABASE_CONNECTION`; no provider token is needed. Keep the
database and ignored evidence archives together in the established backup workflow.
Implementation is uncommitted and ready for review. Real evidence remains
insufficient for candidates; successful persistence does not change readiness.

## Decision Snapshot History UI V0.1 — completed (2026-10-03)

Started from clean `e700cc0`, `main` six commits ahead of `origin/main`, with the
Decision Snapshot persistence milestone already committed. The earlier persistence
section's uncommitted/no-UI statements describe that milestone at completion;
this separate milestone adds a read surface without changing its frozen model.

### Routes and retained facts

The enabled **Decision History** sidebar item opens `/decisions`; canonical detail
identity is `/decisions/{runId}`. The list uses the existing recent-header GET,
backend default **20**, original order and opaque keyset cursors. **Next page**
advances the cursor; **Reset to latest** restarts without an offset or an unbounded
history scan. Captured status, dates, policy, universe, portfolio context, row count,
abbreviated inputHash and stable run ID remain visible. Loading, empty and service
errors are distinct from a stored BLOCKED evaluation.

Detail reads only the stored run API. It shows separate **Captured at**, **Known by**,
**Through**, **Target market session** and **Recorded at**, exact UTC clocks, request
and run IDs, history anchor, policy/universe snapshot, portfolio ID, all persisted
rows, captured counts and benchmark context. Full hashes wrap in selectable
monospace areas. There is no live Screener request or hash recomputation to populate
capture facts; no capture/edit button, outcome result or verification badge.

Every returned instrument survives, including DATA_BLOCKED, INELIGIBLE and positive
holdings outside the configured universe. Real expansion buttons expose captured
eligibility/setup reasons, episode identity/dates/ages/trigger, full technical
values, availability and provenance, and factual holding/mandate/thesis metadata.
Not evaluated stays distinct from evaluated NONE. Null values remain unavailable;
genuine zero remains zero. Unknown membership is distinguished from known outside
scope. The table scrolls horizontally; the expanded panel fits the viewport.

**Open current Stock Detail** uses the stable instrument ID and explicitly warns
that the destination may contain newer information. Stock Detail has a secondary
**Decision history** section using the existing instrument endpoint with **limit=5**,
independent of its current through/cutoff and portfolio controls. It shows captured
clocks/session, eligibility/setup/close/status and links to the immutable run.
No current portfolio values or private thesis text replace captured context.

### Hosting, state and accessibility

The only C# change is the narrow React host fallback
`/decisions/{**path:nonfile}` to `index.html`. Direct list/detail URLs and refresh
work. API routes keep their JSON/error responses; static assets retain their content
types, missing `.js` files retain 404, and unrelated URLs remain 404. Snapshot DTOs,
capture transactions, hashes, schema, migrations and chronology are unchanged.

Existing shell navigation/back/forward conventions are reused. Read requests have
abort ownership plus generation guards so late responses cannot overwrite newer
route state. Invalid IDs, missing runs, service failures, retries and cursor reset
have explicit states. Semantic headings/tables, real links/buttons, aria-expanded,
accessible loading/errors, visible focus and existing reduced-motion styling are
preserved. Chrome verified keyboard Tab reachability of navigation and links,
Space expansion/collapse, changing aria-expanded, leaving controls without a trap,
and browser back/forward.

### Exact executed verification

Node **24.19.0** from the existing bundled runtime satisfies the Node 22.12+ build
requirement; no dependency was added or package/lockfile changed.

- In `frontend`, `npm test`: **76/76 passed, 0 failed/skipped**, including **16**
  new Decision History cases. Covers loading/empty/error, BLOCKED, order/cursors,
  clocks/hashes, full 25-row population, null/zero/availability, all setup states,
  outside held and unknown membership, portfolio metadata, stable links and stale
  completion/404/service handling.
- In `frontend`, `npm run build`: **PASS**.
- `rtk proxy dotnet build`: **PASS, 0 warnings/errors**.
- `rtk proxy dotnet test`: **348 total, 339 passed, 9 expected database opt-in
  skips, 0 failed**. Executed because the API host fallback changed.
- `IDX_TEST_BROWSER=1 IDX_TEST_NODE=<Node22+ executable> python3 -m unittest discover
  -s scripts -p test_decision_history_ui.py -v`: **2/2 passed**, comprising focused
  HTTP/direct-route/asset/error regression and production native Chrome acceptance.
  The owned fixture contains BLOCKED/discovery and PARTIAL/portfolio captures,
  WARMUP/unavailable fields, evaluated NONE/WATCH/CONFIRMED, DATA_BLOCKED and
  outside-universe held/not-evaluated rows. Cursor paging, expansion, direct/reload/
  back/forward, keyboard, current Stock Detail/history links and API-unavailable
  recovery passed at **1440/1366/1280/1024/768/390**. Snapshot table contents are
  unchanged across browser browsing; browser traffic is GET-only with zero external
  requests and zero runtime exceptions.
- Existing Stocks acceptance with browser enabled: **5/5 passed**, including
  four HTTP cases and native Chrome regression at all six required widths.
- Real retained-capture HTTP/Chrome smoke: **PASS**, including direct list/detail
  and refresh against the final non-file fallback, at **1440 and 390**.
- `git diff --check`: **PASS**. Full persistence, collector and broader browser
  suites were not rerun; this task did not change their implementations.

The initial browser check exposed the missing direct-route fallback; route-isolation
acceptance then exposed file-like URLs being captured by the unconstrained fallback.
Both were fixed at the host boundary and the final affected checks passed. Headless
keyboard checks use explicit focus emulation; production controls remain native
buttons and links. Desktop/mobile screenshots were inspected. All owned disposable
databases, temporary API processes and Chrome profiles were cleaned up.

### Real operational smoke and safety

Read only the existing run `3bc25cf2-901b-4c15-84b2-e265091f7f48`; no new capture.
`/decisions` displays it, its direct detail URL and refresh return the React page,
and run GET returns **HTTP 200**. All **10 persisted rows** render. Captured state
remains **BLOCKED**, through **2026-10-03**, target session **2026-10-02**, portfolio
**null**, configured/dataBlocked **10/10**, candidates **0**. BLOCKED has no
application-error banner. Discovery-only context, distinct chronology labels and
both exact hashes match the retained run:

- InputHash: `6c32994d484472bb08361905e29bf0f86d2a29c558c94ad91bf0d0e7380f9177`.
- SelectedDigest: `38ba96fba61bd32c803d6b9c2cca2e1a713a8611dfbb691496e44c4c457ed377`.

The smoke used a temporary loopback API against real retained data; the normal
port-5080 process was not replaced. Browser requests were **GET only**, with **0**
capture POSTs, writes, live Screener calls or external requests. Provider calls/units
are **0/0** throughout the milestone.

Before/after fingerprints cover **13 operational tables**, including both snapshot
tables, and **211 protected file hashes** (pilot/reference/session/operation/summary
files, reference archives and both frozen contracts). They match. Retained captures
remain **1 run / 10 rows**, canonical history **86 rows**, portfolio data unchanged,
schema versions **2, 4, 5**. Soak remains **1/10**, qualifying date **2026-09-30**,
after_market_date **2026-09-28**; FullIdx **NOT ENABLED**. No new migration,
persistence/policy change, provider collection, outcome tracking or verifier service.

Remaining limits: forward-only history cursors with reset; Stock Detail shows only
the latest five captures; no UI capture action, history export, outcome or verification
workflow. Real evidence remains insufficient for candidates. Captured hashes provide
linkage, not a verified badge. Restart the local API and reload the built frontend
to use the new routes; no operational data modification is required.


## Decision Verification V0.1 — completed (2026-10-03)

Started from clean `3625510` on `main`, seven commits ahead of `origin/main`.
The earlier sections' deferred-verifier statements describe their own milestones.
This separate milestone implements on-demand reproduction of an immutable capture;
both frozen contracts and migration 0005 remain unchanged. No verification history,
new table, migration, dependency, outcome tracking or provider recovery was added.

### States, endpoint and chronology

`POST /api/screener/decision-snapshots/{runId}/verify` accepts exactly an empty JSON
object `{}` and no query parameters. No policy, evidence, result, date, path or clock
override is accepted. Existing same-origin JSON and 64 KiB transport rules apply.
It performs computation in a **READ ONLY / REPEATABLE READ** transaction. POST does
not create a capture or persist a verification result.

| State | Meaning |
|---|---|
| MATCH | Exact retained inputs authenticate, original policy is available, both hashes and the complete typed analytical projection reproduce |
| INPUT_NOT_AVAILABLE | Required retained records/archive bytes are missing, invalid, corrupt or cannot be authenticated |
| POLICY_VERSION_UNAVAILABLE | No explicitly allowlisted policy/storage-projection implementation can execute this capture |
| DIFFERENT_RESULT | Inputs and implementation are available, but replay differs in a hash or typed captured result |

All four are successful **HTTP 200** outcomes. Malformed ID/body/query returns
**400 VERIFICATION_REQUEST_INVALID**; missing capture **404 SNAPSHOT_NOT_FOUND**;
database, deadline or invalid stored runtime projection failures return
**503 VERIFICATION_UNAVAILABLE**. Existing snapshot service bounds keep their
existing error codes. Caller cancellation propagates rather than becoming an
unavailable-evidence outcome.

The response contains runId/state, actual current UTC `verifiedAt`, original
`capturedAt`/policyId, stored and nullable recomputed inputHash/selectedDigest,
concise detail, and bounded differences plus `differencesTruncated`. It exposes no
archive paths, internal transaction IDs, manifests, private thesis text or provider
payloads. `verifiedAt` is this execution time, never a replacement capture clock.

### Exact retained evidence and shared replay

The resolver explicitly binds `(screener-v0.1.0, storage schema 1)` to the existing
shared evaluation core. Unknown policy/projection pairs do not fall back. Add future
frozen implementations explicitly; do not change behavior under an old policy ID.

Verification reads exact `(instrumentId, sessionDate, revisionNumber)` bar keys and
listing `(instrumentId, knownAt)` keys from the stored manifest. It checks retained
identities, hashes, clocks and rawArtifactId, and recomputes the existing canonical
content hash from price/volume content. An unchanged hash column cannot conceal
changed content. Admission/quality validation remains separate: an authenticated
selected REJECTED revision remains selected, without substitution. An exact missing
revision is unavailable even when another revision for its date exists.

The three fixed-kind reference archives are read from the existing internal,
content-addressed root. Expected byte length and SHA-256 are verified before parsing
those copied bytes with the existing parser. The archived bundle's original-cutoff
selection must agree with retained universe/instrument/session/status linkage.
Explicit missing-file markers and empty selections retain absence; live files are
never consulted to fill them. There is no arbitrary archive traversal or recovery.

For portfolio captures, only retained event/thesis IDs, the immutable portfolio
header and original chronology are read. Missing required IDs fail closed. Later
events, corrections and thesis versions never enter verification, even if supplied
with an old knowledge date. Discovery-only captures require no portfolio input.

Replay reuses `ScreenerEvaluator`, features/episodes, `PortfolioLedger.Project`,
`ScreenerReferences.SelectedDigest`, `ScreenerPresentation.InputHash` and the same
capture projection. It does not call today's GET Screener or create another engine.
Both hashes, typed run result/header analytical values, complete ordered population
and every typed row projection are compared. Dictionaries are maps; arrays retain
contract order; decimals and UTC instants use exact CLR equality. Display formatting
and JSON property order are not differences. No competing result hash was added.

Bounds remain 211 selected IDs including benchmark, 80,000 bars, existing anchored
horizon, 210 persisted rows, 10,000 portfolio events, 2,000 theses, 4 MiB/10,000-record
reference bundle and 32 MiB manifest. Commands use **15 seconds**, operation deadline
**60 seconds**, with cancellation. Diagnostics stop at **100** differences, explicitly
mark truncation, and bound field/value text to 241/161 characters including ellipsis.

### Decision Detail audit UI

A separate **Audit / Verification** section has an explicit **Verify retained
capture** button. Loading, all four outcomes, service errors/retry, full stored and
recomputed hashes, separate clocks and bounded diagnostics render independently.
No page-load verification occurs; navigating away aborts and ignores late results.
Reload discards the on-demand result. Captured tables/fields remain unchanged,
including when DIFFERENT_RESULT is reported. Stock Detail keeps its existing history
links and has no per-row verification controls.

MATCH means deterministic reproduction only. It does not certify external provider
claims, evidence readiness, a prediction, recommendation or trade outcome. MATCH
can legitimately reproduce a captured BLOCKED result with zero candidates.

### Exact executed verification

- `rtk proxy dotnet build`: **PASS, zero warnings/errors**.
- Focused standard .NET `DecisionVerificationTests`: **13 total, 12 passed,
  1 expected database opt-in skip, zero failures**. Covers policy/projection binding,
  typed round-trip, decimal scale/UTC/map equality, ordered reasons/ID arrays,
  population/per-row differences, truncation, cancellation and content integrity.
- Standard root `dotnet test`: **361 total, 351 passed, 10 expected database opt-in
  skips, zero failures**.
- Existing disposable standard-discovery harness `test_screener_evidence.py`:
  **1/1 passed**, with **361/361 .NET cases passed, zero skips/failures**, including
  the exact-key SQL reader preserving an invalid selected revision and absence.
- `test_decision_verification.py`, browser enabled: **13/13 passed, zero skips**;
  12 owned HTTP/database cases plus native Chrome. MATCH/blocked/portfolio,
  late old-knownAt imports, later portfolio corrections/theses/live file changes,
  missing/corrupt/length/hash/malformed archives, selected canonical content/key
  loss, listing/session identity, malformed manifest links, missing event/thesis,
  explicit absence, unsupported policy, typed DIFFERENT_RESULT/hash diagnostics,
  HTTP errors and snapshot immutability all passed. Final focused transport/runtime
  error regression also passed **1/1**, including invalid stored DTO -> 503.
- Frontend `npm test`: **83/83 passed, zero skips/failures**; seven new verification
  cases. Frontend production build **PASS**, using installed Node **24.19.0**.
- Existing Decision History HTTP/native Chrome regression: **2/2 passed**.
- Existing capture/persistence regression `test_decision_snapshots.py`: **19/19
  passed**, including transactions, concurrency, immutable guards and restore.
- Verification and existing History browser acceptance covered
  **1440/1366/1280/1024/768/390**, visible keyboard focus/action, observable progress,
  explicit-only POST, successful unavailable/different states, error/retry,
  unchanged captured table, reload and no external requests/runtime exceptions.
- `git diff --check`: **PASS**. Collector/provider and unrelated product suites
  were not rerun; their implementations are unchanged. All owned databases and
  temporary API/Chrome processes/profiles were cleaned up.

### Real operational verification and safety

The existing run `3bc25cf2-901b-4c15-84b2-e265091f7f48` returned **HTTP 200 / MATCH**,
with **zero differences** and no truncation. It remains the same **BLOCKED** capture,
through **2026-10-03**, target **2026-10-02**, discovery-only, **10 rows**. Original
capture clock is `2026-10-03T04:13:45.712111+00:00`; the final direct verification
clock was `2026-10-03T13:36:17.894539+00:00`. Stored and recomputed values agree:

- InputHash: `6c32994d484472bb08361905e29bf0f86d2a29c558c94ad91bf0d0e7380f9177`.
- SelectedDigest: `38ba96fba61bd32c803d6b9c2cca2e1a713a8611dfbb691496e44c4c457ed377`.

A temporary loopback API reused the real retained evidence; the normal port-5080
process was not replaced. Production Chrome verified explicit MATCH, loading,
service-error/retry, keyboard/focus, separate clocks and unchanged captured content
at all six widths. Only compute-only verify POSTs were issued; no capture POST,
live Screener evaluation, provider recovery or external browser requests.

Before/after fingerprints match **13 operational tables and 211 protected file
hashes**, including both snapshot tables, canonical/portfolio data, pilot references,
sessions, operation/summary reports, retained reference archives and both contracts.
Retained state is **1 run / 10 snapshot rows**, **86 canonical revisions**; schema
versions **2, 4, 5** unchanged. Provider calls/units **0/0**. FullIdx **NOT ENABLED**;
soak **1/10**, qualifying **2026-09-30**, after_market_date **2026-09-28** unchanged.
No operational evidence was changed to force MATCH.

Remaining limits: on-demand computation only, no persisted audit history or batch
verification, no missing-input recovery, and only the current explicitly bound
policy/projection pair. Backup/restore must preserve original canonical/portfolio
records and decision reference archives together. Real candidate readiness remains
blocked independently of reproducibility. Restart the normal local API with its
existing private connection string and reload the production frontend to use Verify.


## Outcome Tracking V0.1 — design only (2026-10-03)

`docs/OUTCOME_TRACKING_V0_1_CONTRACT.md` freezes `outcome-v0.1.0`: one immutable
terminal outcome per captured row and +1/+5/+10/+20 confirmed exchange-session
horizon, assessed across the full captured population. It uses the exact captured
close only with captured anchor proof,
whole-span raw-price comparability, actual later evaluation/recording clocks and
explicit missingness. Whole-run evaluation returns all cells but atomically inserts
only newly terminal outcomes. Missing forward bars/session/basis/status evidence
remains unresolved and retryable; unavailable is terminal only with original
capture-time anchor failure or positive authoritative disqualifying evidence.
Committed terminal results remain immutable; unresolved cells stay in the research
denominator and may resolve when retained evidence arrives. Benchmark-relative
returns, MFE/MAE,
total return, outcome UI, scheduling and verification are deferred. The real Oct 3
capture targeting Oct 2 has no retained future horizon yet; its visible closes
lack captured price-basis clearance. This milestone changes documentation only:
no migration/table/API/code, evidence write, provider call, soak change or FullIdx
activation. Both existing frozen contracts remain unchanged; soak remains 1/10.


## Outcome Tracking V0.1 — implementation (2026-10-03)

`outcome-v0.1.0` now supports on-demand assessment for the complete immutable
captured population at +1/+5/+10/+20 independently confirmed completed exchange
sessions strictly after targetSession. Weekends and sourced closures do not count;
bars cannot manufacture sessions or slide an endpoint. Captured price/date/revision
and capture-time clearance remain fixed. Later evidence can resolve forward gaps
but cannot repair the original anchor.

Apply `src/IdxStockIntelligence.Infrastructure/Migrations/0006_decision_snapshot_outcomes.sql`
after schema 5 using the existing controlled SQL migration procedure. It adds only
`decision_snapshot_outcome` and schema version 6; no source/portfolio/snapshot backfill.
Composite capture-row FK/uniqueness, terminal-state/date/clock/manifest linkage guards
and UPDATE/DELETE/TRUNCATE rejection enforce immutable terminal storage.

The pure evaluator reuses canonical numeric/session/reference validation. AVAILABLE
uses checked decimal `100m * (horizonClose / anchorClose - 1m)` without display rounding,
requires ordinary IDR identity, supported endpoint trading and one source-matched
whole-span STOCK_RAW / RAW_AS_TRADED / CLEARED interval containing both endpoint
hashes. Dividends are excluded cash flows. Unknown action flags, missing bars,
unknown status, missing calendar proof and absent comparability remain retryable.
Typed authoritative endpoint status or incompatible source evidence can establish
terminal DATA_UNAVAILABLE / BASIS_UNCERTAIN; missing capture-time proof establishes
ANCHOR_UNAVAILABLE only once the exact forward horizon is independently proved.
The existing reference model has no typed corporate-action event facts: generic
UNKNOWN/UNRESOLVED coverage cannot manufacture an action or terminal basis outcome.

Endpoints (same existing loopback/origin/body protections):

- POST `/api/screener/decision-snapshots/{runId}/outcomes/evaluate`, strict body
  `{"horizonSessions":1}` (1, 5, 10, 20 only). Returns 201 when any new terminal subset
  commits, 200 otherwise. Caller clocks, prices, results, paths and filters are rejected.
- GET `/api/screener/decision-snapshots/{runId}/outcomes`: full bounded four-horizon
  grid, ordered by instrument/horizon, up to 840 cells.
- GET `/api/screener/decision-snapshots/{runId}/rows/{instrumentId}/outcomes`: four
  cells for a captured row. Unknown runs/rows return 404.

Responses distinguish terminal/unresolved resolution and committed/unmaterialized
cells, with original recording/knowledge clocks only for committed cells. Read-only
terminal previews do not insert. Whole-run POSTs reuse existing terminal cells and
atomically insert only new terminal cells; PENDING/SESSION_UNAVAILABLE/UNRESOLVED
are never persisted. Existing complete terminal subsets can be recovered without
live files. A native run/horizon advisory lock precedes the repeatable-read assessment;
uniqueness/serialization failure retries at most once with a fresh view. Actual
DB evaluation/recording clocks, copied bounded reference bytes, SHA/length-verified
archives and exact original/horizon revision links are retained. Existing 15-second
commands, 60-second deadline, 211-ID/80,000-row and 4 MiB reference bounds apply;
per-cell manifests are capped at 64 KiB and operation manifests at 32 MiB.

Verification executed:

- `rtk proxy dotnet build`: PASS, zero warnings/errors.
- Focused Outcome discovery: **31/31 PASS**.
- Canonical root `rtk proxy dotnet test`: **382 passed, 10 expected DB opt-in skips**
  (392 total, zero failures).
- Existing disposable-enabled canonical discovery: **392/392 PASS**, wrapper 1/1.
- `scripts/test_outcome_tracking.py`: **11/11 PASS** via standard unittest discovery;
  includes delayed bar/session/basis, incremental/mixed subsets, actual later clocks,
  terminal anchor/status/basis, exact zero/positive/negative returns, concurrency,
  rollback, native lock timeout/recovery, immutable reads, later revision isolation, strict API/integrity and
  migration fresh/upgrade/rerun/populated dump/restore acceptance.
- Existing Decision Snapshot acceptance: **19/19 PASS**; truncate test now includes
  CASCADE to exercise its append-only guard with the added dependent table.
- Existing Decision Verification acceptance: **12 passed, 1 expected browser opt-in
  skip**. Frontend/browser suites were not rerun; no frontend code changed.
- `git diff --check`: PASS.

After disposable acceptance, migration 0006 was applied to the operational database.
Versions are **2, 4, 5, 6**. Real retained run
`3bc25cf2-901b-4c15-84b2-e265091f7f48` was assessed at
`2026-10-03T16:34:59.226584+00:00`: HTTP **200**, +1, **10 unresolved cells**,
state **PENDING**, reason **HORIZON_NOT_REACHED**, no proved horizon date,
**0 available / 0 terminal unavailable / 0 newly materialized**.
Operational outcome row count **0 before / 0 after**. No future session or anchor
clearance was fabricated. Decision Verification remains HTTP 200 **MATCH**, with
unchanged inputHash/selectedDigest and no differences.

Before/after fingerprints match **13 operational tables and 212 protected file
hashes**: original 1 run / 10 rows, 86 canonical revisions, portfolios, references,
sessions, operation reports, retained archives and all three frozen contracts.
Only the additive schema/table changed. Provider calls/units **0/0**; FullIdx remains
**NOT ENABLED**. Soak stays **1/10**, qualifying 2026-09-30, after_market_date
2026-09-28. No collection, scheduler, outcome verifier, UI, new dependency or
contract/setup-policy change. The temporary operational API was stopped; the normal
local API requires its usual restart to expose the new routes.

Remaining limits: on-demand current policy only, bounded PILOT horizon through
2027-08-24, no adjusted/total/benchmark return, MFE/MAE, scheduler or outcome UI/
verification. Real future data readiness and capture-time price clearance remain
independent limitations. Backup/restore must retain outcomes together with original
captures, exact canonical revisions/raw/listing records and reference archives.


### Outcome History UI V0.1 (2026-10-04)

Decision Detail now includes an additive **Outcomes** section for every captured
row across **+1 / +5 / +10 / +20 completed sessions**. Neutral cells distinguish
UNRESOLVED from AVAILABLE, ANCHOR_UNAVAILABLE, DATA_UNAVAILABLE and BASIS_UNCERTAIN;
reasons, preview/committed status, horizon dates/closes and available **Price return**
are visible. Genuine zero remains zero; missing returns remain unavailable. Price
return excludes cash dividends. Keyboard-expandable cell details show exact public
DTO facts and separate capture/target, read assessment, committed known/recorded
clocks. Internal manifests, archive paths and provider payloads are not exposed.

Opening a capture performs only outcome GETs. Explicit **Evaluate +N** uses the
existing run/horizon POST against retained evidence, then refreshes the run read.
Zero newly materialized outcomes is normal success. Busy controls, abort/generation
guards and a 65-second client deadline protect duplicate clicks and stale routes;
errors retain captured content. A failed refresh after a completed POST is identified
separately. Four-horizon counts summarize the full population, with no performance
analytics, frontend return calculation, live Stock lookup, scheduler or outcome
verification. Existing captured rows, Stock links and Decision Verification remain
available. No backend/API/DTO, migration, dependency or frozen-contract change.

Verification executed against the production frontend:

- `npm test` (bundled Node 24): **100/100 PASS**, zero failures/skips; includes
  17 Outcome checks for states/returns/clocks, explicit evaluation, errors/timeouts,
  duplicate clicks, stale routes and zero-new results with existing terminal records.
- `npm run build`: **PASS**, TypeScript and Vite (35 modules).
- Standard discovery `scripts/test_outcome_history_ui.py`, `IDX_TEST_BROWSER=1`:
  **1/1 PASS** on owned disposable PostgreSQL/HTTP/Chrome fixtures. Covers unresolved
  and mixed states, positive/negative/zero available returns, previews/committed
  cells, explicit +1/+5 and refresh, unchanged capture, keyboard/focus/details,
  read error/retry, back navigation and no automatic evaluation/external requests.
- Existing `scripts/test_decision_history_ui.py`, `IDX_TEST_BROWSER=1`:
  **2/2 PASS**, including production browser and direct routes/assets/API errors.
- Both browser flows passed **1440/1366/1280/1024/768/390**; the matrix scrolls
  horizontally while retaining all horizons. Desktop/mobile screenshots inspected.
- `git diff --check`: **PASS**. Backend .NET/HTTP and separate Stock suites were
  not rerun: no backend DTO or shared production component was changed.

Real retained run `3bc25cf2-901b-4c15-84b2-e265091f7f48`, target **2026-10-02**,
captured at `2026-10-03T04:13:45.712111+00:00`, passed production Chrome smoke on
Oct 4 Jakarta time. Before/after reads contained **10 rows × 4 horizons**, all
**40 UNRESOLVED**, 0 available/terminal unavailable/materialized. Exactly one
explicit **Evaluate +1** returned the normal zero-new notice; its ten cells remain
**PENDING / HORIZON_NOT_REACHED**. No +5/+10/+20 POST was performed. The refreshed
read cutoff was `2026-10-03T17:19:32.892441+00:00`; original capture chronology was
unchanged. Operational outcome rows: **0 before / 0 after**. Verification before
and after: **MATCH**. All **14 table fingerprints / 212 protected file SHA-256s**
match, including portfolios, canonical revisions, snapshots, session/reference
proofs, durable operations, archives and three frozen contracts. Temporary API and
Chrome were stopped. Provider calls/units **0/0**, FullIdx **NOT ENABLED**, soak
**1/10**, qualifying **2026-09-30**, after_market_date **2026-09-28** unchanged.

Remaining limits: on-demand evidence readiness, existing bounded PILOT policy,
price return only; no total/benchmark returns, analytics, scheduler, outcome
verification or full Stock Detail outcomes. Terminal previews may change until
explicit evaluation records them; the UI does not certify missing evidence.
