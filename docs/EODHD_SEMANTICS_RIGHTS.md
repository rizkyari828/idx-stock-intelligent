# EODHD semantics and rights closure — 2026-09-29

Scope: private non-commercial Phase 0 research, the existing ten equities plus
JKSE.INDX. **Zero EODHD API requests and zero billable units** were used. Public
documentation was reviewed; no credentials were loaded, no market endpoint called,
no purchase made and no canonical bar changed. FullIdx remains disabled; soak is
**0/10**. The [current matrix](DATA_SOURCE_MATRIX.md#semantics-and-rights-closure--2026-09-29)
separates documented contracts from sampled empirical behavior.

## Reference / retention closure follow-up — 2026-09-29

Research used **zero EODHD API endpoints / zero billable units**. No token or account
response was loaded. Public issuer reports and provider documentation only were
reviewed. No price history, soak count or FullIdx setting changed.

### Evidence register and distinct events

New reference observations have actual `retrieved_at` and `known_at`
**2026-09-29T04:39:49.352803+00:00**, not the historical event/publication dates.
The earlier 03:09:27.403168Z observations remain unchanged.

| Ticker / issuer | Event | Effective date | Status / assessment | Source and reviewed location | Notes |
|---|---|---|---|---|---|
| PTRO / Petrosea Tbk | Initial exchange listing | 1990-05-21 | VERIFIED / CONFIRMED | [Issuer Annual Report 2024](https://petrosea.com/wp-content/uploads/2025/03/PTRO_Annual-Report-2024.pdf), printed p.110, PDF page 56, Share Listing Chronology | Direct public download succeeded (14,066,172 bytes); PDFKit extraction and rendered table reviewed after browser size-limit rejection. Explicit initial Jakarta/Surabaya listing, not merely an indexed snippet. |
| PTRO / Petrosea Tbk | IPO effective statement | 1990-05-21 | VERIFIED / CONFIRMED for this event only | Same report, financial note 1b, printed financial p.8, PDF page 174 | Separate offering authorization. Offering subscription-period dates not established. Equal reported dates do not establish first trading. |
| PTRO / Petrosea Tbk | Initial first trading | UNKNOWN | UNKNOWN / INSUFFICIENT | No separately verified first-trading document | `first_trading_date` remains null. |
| LPIN / Multi Prima Sejahtera Tbk | Initial exchange listing | 1990; exact day UNKNOWN | PARTIAL / INSUFFICIENT for exact day | [Issuer Annual Report 2024](https://www.multiprimasejahtera.net/upload/PDF_INV_ENG_714713373_1747031276.pdf), printed pp.27–29, PDF pages 14–15; financial note 1b p.9, PDF page 82 | Direct report text reviewed. Listing-year evidence strengthened; no invented January 1 boundary. Exact candidate 1990-02-05 remains secondary only. |
| LPIN / Multi Prima Sejahtera Tbk | Initial IPO / first trading | 1990 IPO year; exact day / initial trading UNKNOWN | PARTIAL / INSUFFICIENT | Same chronology / note 1b | The documented 2002-05-30 start of scripless trading (financial p.10, PDF page 83) is a later conversion event, not the initial first-trading date. |

LPIN's 1990-02-05 claim remains recorded but unaccepted: examples include
[Ajaib's asset page](https://ajaib.co.id/saham/aset/LPIN) and
[a 2025 PNJ thesis table](https://repository.pnj.ac.id/31499/1/Halaman%20Identitas%20Skripsi.pdf)
attributing its processed table to IDX without the underlying dated exchange record.
Generic portal IPO/listing labels are not proof of event equivalence. The issuer's
year-only statement neither confirms nor contradicts that day: **INSUFFICIENT**,
not CONFLICTING. PTRO's old candidate agrees with the directly reviewed chronology:
**CONFIRMED** for listing only. No incompatible same-event dates were established.
All candidate histories survive; a later authoritative contradiction must append
new knowledge and preserve prior as-of results.

### Post-cancellation retention: UNKNOWN

Official public pages reviewed September 29: [Terms and Conditions](https://eodhd.com/financial-apis/terms-conditions),
PERSONAL AND COMMERCIAL USE OF INFORMATION, ACCESS TO THE SERVICES §4 and
TERMINATION; [Commercial vs Personal licensing](https://eodhd.com/financial-apis/commercial-vs-personal-license-use),
Commercial Usage Terms / FAQ; [Pricing](https://eodhd.com/pricing), cancellation FAQ.
Current private storage/analysis is allowed; cancellation and contract termination
are described. These pages contain neither an explicit continuing right to retain
previously downloaded ordinary EOD data after cancellation nor an explicit deletion
requirement for that data. The licensing guidance distinguishes personal/business
use, and the pricing FAQ explains unsubscribe controls, not stored-data survival.

Classification: **C — no clear statement; UNKNOWN**, not VERIFIED or PROHIBITED.
Vendor-specific Marketplace terms cannot be applied to the ordinary EOD feed.
Written provider clarification must distinguish cancellation/downgrade from breach
termination, duration, raw/normalized/derived storage and continued private use.
No provider message was sent. Current private-use permission remains scoped; it
cannot justify a one-month download-and-cancel archive.

### Phase 0 impact and validation

- PTRO listing: **CLOSED**; separate first-trading enrichment remains **FUTURE ITEM**.
- LPIN exact listing: **COMPLETENESS LIMITATION**, remains open. Existing
  `UNKNOWN_BOUNDARY` behavior continues; separate first-trading evidence is a future item.
- Post-cancellation retention: **FUTURE ITEM** for the continuing Free pilot; a rights
  blocker for any cancellation-based permanent archive. It is not a newly found
  canonical-price correctness defect or approval to retain data after termination.

Local validation: first import grew reference history **16 → 18**; identical
repeat kept **18**; canonical price revisions stayed **55**. Read-only as-of queries
at 03:09:27.403168Z and 04:39:49.352803Z confirmed earlier effective boundaries
remain null for both, then PTRO becomes 1990-05-21 and LPIN remains null. Existing
offline tests additionally verified correction append, same-time overwrite
rejection, idempotence and replay in owned disposable databases.

Only two reference observations were appended using the existing generic importer:
PTRO VERIFIED listing and LPIN PARTIAL reviewed-year provenance. No source code or
migration change. First-trading/delisting dates remain null. Normal local database
validation and complete offline suites are recorded below; references improve
listing coverage to nine verified equities plus one PARTIAL (LPIN). The prior
knowledge cutoff still yields PTRO PARTIAL and LPIN PARTIAL.

## Price and adjustment contract

The [official EOD specification](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes)
documents raw, as-traded OHLC, split/dividend-adjusted adjusted_close and split-adjusted
volume. Thus corporate actions do not themselves restate OHLC under this contract;
corrections are a separate possibility. Adjusted closes are retrospectively
recomputed after dividends and are not stable historical identifiers. Field-level
semantics are VERIFIED as documentation, not proof of exact JK action calculations.

Existing experiments establish the seven fields, valid OHLC bounds, separate raw
and adjusted closes, and repeat-request/archive stability in the sampled window.
They did not span and independently reconcile a known split/dividend adjustment.
Actual JK corporate-action correctness remains PARTIAL. Retain both close fields,
original observations and their knowledge times; an adjusted rebase is not by itself
evidence of an erroneous original trade price. A provider recommendation to refresh
history does not authorize unbounded retrieval or overwrite as-of revisions.

## Volume: four distinct claims

1. Observed numerical scale: BBCA 89,447,400 and ANTM 67,544,100 on September 25
   matched the reviewed reference's 894,474 and 675,441 explicitly labelled lots,
   multiplied by its stated 100 shares/lot. These two comparisons are VERIFIED
   observations, not a universal JK unit proof. See [preserved empirical evidence](DATA_SPIKE_RESULT.md#1-jk-volume-explicit-units-two-dated-comparisons).
2. IDX convention: [IDX's public education reference](https://rdis.idx.co.id/en/events/mengenai-satuan-lot-apa-itu-lot)
   establishes 100 shares per lot. This defines lots, not EODHD transformations.
3. Provider contract: EOD volume is split-adjusted. Share-scale corroboration does
   not establish original pre-split quantities or a regular-market-only segment.
4. Upstream and processing: the JK feed, aggregation/segment scope and independence
   from the reference publisher remain UNKNOWN. [Source disclosure](https://eodhd.com/financial-apis/our-data-sources-and-data-partners)
   names other exchange contracts and market-maker sources, but no JK-specific chain.

Overall JK volume semantics: **PARTIAL**. Persist the provider integer exactly;
introduce no automatic ×100/÷100 conversion. Keep sampled scale, documented split
basis and UNKNOWN market segment distinct. No stock unit is assigned to index volume.

## Session behavior and current controls

The [existing adjacent-date experiment](DATA_SPIKE_RESULT.md#2-holiday-rows-adjacent-dates-and-reproducibility)
established repeatable ANTM carry-forward zero-volume rows on August 17/25, GOTO
padding on those closures and BBCA omission. Flat positive-volume GOTO observations
also occur. This is VERIFIED for the tested rows only, not a universal synthetic-row
specification. The provider's generic one-row-per-trading-day description cannot
override independently proved closure dates.

Parser retains all raw evidence; .NET admission rejects known closures, does not
promote unknown sessions and treats zero volume as UNKNOWN absent independent
instrument no-trade/suspension proof. Presence/absence cannot create a calendar.
[Same-day eligibility](SAME_DAY_EOD.md) additionally requires configured cutoff and
completed/open proof before retrieval. Existing controls are sufficient for this
fixed-panel pilot's conservative admission; no correctness defect requiring code
changes was found. They do not certify full calendar or provider completion coverage.

The EOD documentation advertises exchange updates roughly 2–3 hours after close,
with some index updates later. **19:00 WIB is a local safety policy, not a JK/IHSG
publication SLA**; missing same-day data still produces an explicit degraded outcome.

## Splits and dividends

[Official event documentation](https://eodhd.com/financial-apis/api-splits-dividends)
includes Free with one year of history, at one unit per symbol/endpoint. Dividend
date is ex-date; JSON distinguishes declaration, record and payment dates when
available, currency, split-adjusted value and unadjustedValue. Split date is ex-split;
the ratio is new shares over old shares. Some ratios represent broader reorganizations,
not a clean split. Optional dates and ambiguous empty responses require validation.

Documented endpoint/date/ratio semantics: VERIFIED. JK event completeness and
reconciliation: UNKNOWN/unperformed. **Corporate-action safety gate: PARTIAL**;
2022-present event access is BLOCKED on current Free history. These endpoints do
not establish complete rights-issue, merger, spin-off, ticker or entitlement history.
Require issuer/authorized reference events, currency/basis reconciliation and as-of
revision checks before treating action-adjusted output as validated research input.

## Symbol discovery and entitlement

`.JK` is EODHD's exchange namespace for its Jakarta catalogue, not a MIC or security
class. [Ticker-list documentation](https://eodhd.com/financial-apis/covered-tickers-eodhd)
describes a current/recent-active list, asset-type filters and Code/Name/Country/
Exchange/Currency/Type/nullable ISIN. Its delisted wording says included inactive
tickers; the [specific delisted guide](https://eodhd.com/financial-apis/delisted-stock-companies-data-2)
explicitly says `delisted=1` returns only inactive tickers. Keep the two populations
separate, preserve response parameters and reconcile them rather than assuming a
combined list. Inactivity is not a verified legal delisting date.

The existing JK inactive sample included a warrant-style code labelled Common Stock.
Neither all JK rows nor a type filter defines the canonical ordinary-share universe.
Future FullIdx needs independently verified security class, stable identity/ISIN,
listing/delisting and symbol-history boundaries, board/segment/status, population
reconciliation and lawful reference access. Current discovery is PARTIAL; old SCBD
prices remain BLOCKED by the observed Free warning, not proved absent at EODHD.
JKSE.INDX identity and sampled recent closes remain VERIFIED from actual discovery.

Current [EOD specification](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes),
[pricing](https://eodhd.com/pricing) and [limits](https://eodhd.com/financial-apis/api-limits)
support Free 20 units/day and past-year history. Exact rolling-date boundary remains
unspecified; project uses a conservative 330-day request limit. Splits/dividends
share the documented Free one-year limit. Exchange lists and [news](https://eodhd.com/financial-apis/stock-market-financial-news-api)
are documented available; news ordinarily costs five units and is not fetched here.
News's own deeper-history entitlement is not independently established.

Known account capabilities supplied for this review are EOD, splits, dividends,
exchange list and news; fundamentals, Technical API, calendar and tick remain
disabled. Intraday, screener, real-time/extended feeds, economic/macro and other
paid/Marketplace features are not approved or assumed enabled. Pricing is not a
fresh account observation; no endpoint probes were performed. Local indicator
calculation does not require buying the provider's Technical API.

## Scoped rights

[Terms, personal/commercial section](https://eodhd.com/financial-apis/terms-conditions)
expressly allow nonprofessional private storage, manipulation and analysis for
personal investment. Current entitled retrieval and local research: **VERIFIED
within that stated scope**. Normalization, indicator calculation and retaining
private derived research results are applications of that grant, an interpretation
rather than individually named promises. No separate perpetual derived-data license
was found. Post-cancellation raw/normalized/derived retention remains UNKNOWN.

Sharing accounts, redistribution/resale and providing/displaying original or
repackaged data to others are prohibited under personal terms. Public/commercial
use is OUT OF SCOPE and needs separate licensing; private use cannot be on behalf
of others. [Commercial guidance](https://eodhd.com/financial-apis/commercial-vs-personal-license-use)
supports that distinction. No JK-specific upstream grant was established. This
records the provider text, not a legal determination or an exchange-feed license.
The terms' generic 100,000-request clause is not this Free account's quota; use
plan-specific limits. General no-warranty wording and source disclosures prohibit
equating provider data with an unquestioned exchange tape.

## Extra calls: documentation versus account behavior

[Limits, Extra API Calls section](https://eodhd.com/financial-apis/api-limits)
documents an overflow balance used after the ordinary daily allowance, non-expiring
and cumulative when purchased. It does not increase the ordinary daily limit.
Documented buffer behavior: VERIFIED. Quota purchase supplies no established
subscription-feature or history upgrade: this conclusion follows the separate
plan restrictions, and the old SCBD request was
refused while the earlier account had an extra balance. No disabled feature is
authorized by an unused balance. No direct statement granting deeper Free history
or unlocking disabled endpoints was found.

Empirical overflow consumption remains **UNKNOWN**: the earlier test never met
its authorized preconditions and made no overflow probes. An unchanged extra balance
below the ordinary allowance does not verify its runtime consumption. No purchase,
quota exhaustion or extra-call test is authorized by this closure task.

## Deterministic quality policy

EODHD remains the broad-screening candidate/primary for this fixed-panel pilot,
not an exchange source of truth or production-approved feed. Preserve exact bytes,
stable identity, request bounds, units/basis/status and retrieval/knowledge chronology.
Validate schema, dates, duplicates, OHLC bounds and independently known sessions
before admission. Missing/unknown remains UNKNOWN, never zero or silently filled.

Require separately sourced reconciliation for session conflicts, suspicious volume
or unit/basis changes, action-like discontinuities, unexpected close/adjusted ratios,
and large historical corrections. No new numeric threshold was invented: until an
approved deterministic threshold exists, unexplained anomalies remain flagged for
review and do not clear quality gates. Retain competing observations/references;
append corrections without overwriting history. Do not silently substitute providers.
Positive-volume bounds-valid anomalies can still exist in pilot canonical storage
with DEGRADED quality; this document does not claim a new production quarantine engine.

## Offline validation and readiness

Latest reference follow-up: **21 .NET tests, 28 Python tests and all 14 offline
restore/replay check groups passed**; zero provider requests. Restore evidence is
ignored at `data/collector-output/pilot/idx_pilot_check_5313f6458cb84a4fa6631aadc601dbc0/result.json`.
No source code, price revision or completed-session proof changed; soak **0/10**.

Earlier semantics-only review (before the reference follow-up): documentation-only changes. Existing parser, workflow and completed-session tests:
**15 passed**. Five existing semantic artifacts passed exact SHA-256/byte-length
verification (230,234 bytes total); no raw data or account response was printed.
Local dry-run still reports 55 canonical bars and NOT_ELIGIBLE before cutoff with
UNKNOWN_SESSION. No September 29 proof was added. Soak remains **0/10**.

Readiness now: **NO**. After 19:00, readiness still requires independently reviewed
completed/open September 29 proof with actual timestamps, an eligible local dry-run,
healthy PostgreSQL, ordinary quota headroom and subsequent normal payload validation.
FullIdx, complete historical/action reconciliation, source-chain confirmation,
post-cancellation retention and ten actual soak sessions remain uncleared. No
purchase, live collection or 2022-present experiment starts automatically.
