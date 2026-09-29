# Data-driven instrument boundaries — September 29, 2026

This hardening used **zero EODHD requests and zero billable units**. FullIdx remains
disabled, the panel is unchanged and prospective soak remains **0/10**. Reference
research changes metadata, not price history or exchange-session proof.

## Existing model and minimal extension

`instrument` supplies the stable UUID, issuer and legacy nullable listing/delisting
fields. `instrument_history` describes symbol validity, not when a listing fact
became known. Existing append-only `instrument_listing_evidence` supplies the
equivalent metadata history: `(instrument_id, known_at)` identifies an immutable
JSON evidence revision. No parallel table or new migration was needed.

`InstrumentBoundaryEvidence` retains listing, delisting and distinct first-trading
dates, issuer/symbol, status, confidence, source, publication reference, HTTPS URL,
retrieved_at, known_at, source_version and notes. Null delisting means unestablished,
not independently verified continuing listing. First trading is never inferred
from listing, IPO/allotment dates or the earliest provider bar.

The worker resolves boundary history as of its knowledge cutoff. Legacy reference
observations keep their original known_at; their unrecorded retrieved_at remains
null. Timeless `instrument.listed_on` is not a substitute for visible evidence.
New imports require an actual retrieval timestamp and cannot claim future knowledge.

## Reviewed pilot references

| Sample | Status | Retained evidence | Effective boundary | Source |
|---|---|---|---|---|
| RAJA | VERIFIED | 2003-01-22 listing on Surabaya Stock Exchange | 2003-01-22 | [Issuer 2024 sustainability report, pp.32–33](https://cms.raja.co.id/uploads/SR_2024_13a2b61733.pdf) |
| VKTR | VERIFIED | 2023-06-19 listing; offering/allotment/distribution are separate | 2023-06-19 | [IDX e-IPO listing-date field](https://www.e-ipo.co.id/en/ipo/237/vktr-pt-vktr-teknologi-mobilitas-tbk) |
| PTRO | PARTIAL | Candidate 1990-05-21 in indexed issuer report; direct document access rejected | UNKNOWN | [Issuer 2011 annual report](https://www.petrosea.com/wp-content/uploads/2019/03/PETROSEA_AR-2011.pdf) |
| LPIN | PARTIAL | Indexed issuer text establishes year 1990 only; exact date null | UNKNOWN | [Issuer 2018 interim report](https://www.multiprimasejahtera.net/upload/PDF_INV_ENG_182277666_1533179211.pdf) |

LPIN's secondary exact-date claim is retained only in notes, not a canonical field.
No distinct first-trading or delisting date was established for these samples.
The JSON records contain their actual September 29 UTC review/knowledge timestamps;
publication year is not a backdated knowledge time. Two previously unknown boundaries
are now verified; PTRO and LPIN remain unresolved. The older `pilot/universe.json`
register is preserved as earlier evidence, not rewritten to imply earlier knowledge.

## Reference input and import

The versioned `pilot/instrument-boundaries.json` is a reference-data array, not a
source-code ticker dictionary. Each record uses the fields listed above. It may
contain arbitrary instrument identities; no classification branch depends on ticker.

```sh
dotnet run --project src/IdxStockIntelligence.Worker --no-restore -- \
  import-boundaries pilot/instrument-boundaries.json
```

The deterministic batch importer validates identifiers, required provenance,
chronology and date ordering, then appends within one PostgreSQL transaction.
Identical imports are idempotent. Changing an existing identity/known_at payload
fails; corrections require a new knowledge timestamp. Existing database triggers
also reject UPDATE/DELETE. Importing metadata makes no HTTP request and creates no bar.
The pilot worker reads this file by default (`IDX_PILOT_BOUNDARIES` may select a
different reference file), combines it with persisted history and includes that
evidence in its immutable operation summary. Valid metadata may persist before a
later price-ingestion failure; it is a separate reference-data transaction.

For a correction, append a new record with actual retrieved_at/known_at and the
new source/version. As-of selection chooses only the latest visible observation.
Earlier cutoffs still see the old record. Conflicting evidence must be recorded in
a new PARTIAL revision with competing references in notes, rather than silently
choosing a date. A latest PARTIAL/UNKNOWN observation supplies no verified boundary,
even if older evidence was VERIFIED. Equal-time conflicting records are rejected.
Exact temporal recovery still requires a database backup; re-importing evidence
cannot reconstruct missing original canonical bar knowledge timestamps.

## Availability and sessions

| Boundary/session facts | Result |
|---|---|
| Date before verified listed_from, even with open-session proof | PRE_LISTING; never provider missing |
| Date after verified delisted_at, even with open-session proof | POST_DELISTING; never provider missing |
| Date on listing or inclusive final-listed date | EXPECTED_SESSION boundary path, subject to calendar |
| Insufficient boundary evidence | UNKNOWN_BOUNDARY; absent row remains UNKNOWN |
| Listed instrument and independently known closed session | No expected bar; CLOSED/holiday |
| Listed instrument and independently known open session | Validate observed bar, or classify absent row as missing |
| Unknown/future session proof | SESSION_UNCONFIRMED; no canonical admission |

Boundary eligibility is separate from actual observed availability. A positive,
valid provider observation may be admitted for an independently confirmed open
session while its listing boundary remains UNKNOWN. That observation does not
establish listing metadata, expected earlier history or exchange-calendar facts.
Distinct first_trading_date is preserved metadata; it is not automatically equated
with listing or used to manufacture no-trade/session proof.

The existing versioned `pilot/sessions.json` and instrument-session register remain
the generic proof mechanism: sourced completed-session evidence, actual known_at,
then local dry-run, then authorized collection. No September 29 proof was added.
The collector rejects today's Jakarta date even after close; September 29 collection
requires a later Jakarta date and reviewed independent completion evidence.

## Narrow future collector contract and AI role

Future `SecurityMasterCollector` should emit normalized reference records using the
same evidence fields, plus external_code, optional ISIN and instrument_type when
the source establishes them. Identity mapping must resolve a stable instrument_id;
symbol changes/reuse belong in existing symbol history rather than new identities
selected solely by ticker. Feed records through validation/import, not directly
into canonical availability. No collector/provider integration was implemented.

AI may locate sources, propose extracted dates and highlight conflicts. It cannot
establish VERIFIED status from its answer. A source reviewer must check traceable
evidence; deterministic validation checks structure/chronology, not the truth of a
web claim. No AI dependency or paid model call exists in the runtime path.

The architecture supports batch reference imports for roughly 900 identities
without per-symbol code, while retaining UNKNOWN and correction/as-of semantics.
This is a design assessment, not a full-universe performance benchmark. The current
pilot loads history in memory and uses an exclusive import lock; benchmark and
consider indexed cutoff queries before high-volume concurrent reference ingestion.
Market collection remains fixed-panel and guarded: FullIdx is **NOT ENABLED**.

## Validation and remaining gates

**20 .NET tests, 26 Python tests and 13 offline restore groups pass.** Arbitrary
synthetic tickers cover pre/listing/post, closed/unknown sessions, unknown/partial
boundaries, correction visibility and conflict/chronology rejection. Disposable
database checks cover repeat import, same-time overwrite rejection and appended
metadata corrections with earlier/later cutoff queries; canonical bars are unchanged.
Build has zero warnings/errors. No raw market data, secrets or runtime evidence
are tracked by this change.

Remaining gates include PTRO/LPIN primary exact-date review, independent completed
future sessions and ten actual eligible soak dates, calendar/identity chronology,
market-segment/adjustment/corporate-action semantics, rights and any separately
authorized full-universe operating budget. Historical 2022-present remains
BLOCKED_BY_ENTITLEMENT. Tonight's September 29 live collection is **NO** under
the existing prior-date rule; prepare evidence after completion and collect no
earlier than September 30 after reviewing proof and normal quota headroom.
