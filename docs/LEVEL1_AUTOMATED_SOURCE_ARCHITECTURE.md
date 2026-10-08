# Automated Level-1 source acquisition architecture V0.1

Reviewed **2026-10-08, Asia/Jakarta**. Architecture/source-strategy decision only.
Initial `main`: `494f23e5a2bae8ec97dd3674717bfd22e8809be7`, clean,
12 ahead / 0 behind locally recorded `origin/main`; no fetch.

## Decision and boundary

**C — FREE_FULLY_AUTOMATED_LEVEL1_BLOCKED_ON_EXACT_CLAIMS.** Select architecture
C below, a **conditional free hybrid with reconciliation**, as the target design.
It is not a working, admitted source stack. Zero mandatory monetary cost and
fully automatic acquisition of every mandatory claim are **not yet established**.
Paid-source necessity is **not demonstrated**. No purchase or paid integration is
recommended. **Manual operational data handling: NONE**, including bootstrap.

This decision replaces manual acquisition as the proposed *future operational*
route; it does not rewrite the findings in [the source ledger](LEVEL1_SCREENER_SOURCE_STACK.md).
Research may inspect downloaded originals. Production may not require the user
to find, download, upload, type, reconcile or confirm market facts.
Daily Runner, adapters and soak remain gated. FullIdx remains disabled; the
retained ten-equity plus separate benchmark boundary is unchanged. Source
readability is not source admission. No source/permission registry changed.

Preserve [frozen V0.2](SCREENER_EVIDENCE_V0_2_CONTRACT.md) and its
[persisted binding](SCREENER_EVIDENCE_V0_2_PERSISTED_BINDING.md), including
**KEEP_AFFIRMATIVE_TRADING_STATUS_REQUIRED** from Slice 5A.2d. No future leakage,
synthetic bars, silent fallback, latest-row selection, price-derived status,
weekday-derived OPEN/completion, or action absence inferred from silence.
Genuine zero-volume semantics remain valid when independently proved. Optional
volume/RS/liquidity failures remain local to their features.

## Research evidence and access ledger

All sources below were opened on 8 October 2026, except explicitly identified
prior retained findings. Search snippets only located sources. No private key,
market-data API, account, purchase, provider message or access-control bypass was
used. Public read/download activity is research, not permission for scheduled use.
HTTP metadata describes a response, not economic publication or completion time.

| ID | Actual source/document accessed | Fresh finding and operational disposition |
|---|---|---|
| I1 | [IDX usage terms](https://www.idx.id/id/syarat-penggunaan/) | Clause 6 permits attributed noncommercial use but prohibits scraping/crawling. Ordinary website automation is excluded; a separate permitted feed/export arrangement must be established. A successful public GET cannot override this. |
| I2 | [IDX suspension index](https://www.idx.id/id/berita/suspensi/), [holiday page](https://www.idx.id/id/berita/jadwal-libur-bursa/), [statistics index](https://www.idx.id/id/data-pasar/laporan-statistik/statistik/), [Pantau](https://www.idx.id/id/investor/pantau/) | Reader returned a suspension Loading shell and sparse holiday/statistics pages; Pantau supplies explanatory guidance. No free licensed JSON/feed/export with full status history established. Prior 5A.2c browser research proves that notices can exist despite reader limitations; neither outcome proves a complete feed. |
| K1 | [KSEI monthly master index](https://web.ksei.co.id/archive_download/master_securities) | Actual dated Jan–Sep 2026 ZIP links, including September 30. Shared monthly snapshots, not daily status. Earlier ledger's ZIP inspection remains historical evidence; not downloaded again here. Automatic retrieval/private immutable retention permission remains unresolved. |
| K2 | [KSEI BBCA detail](https://web.ksei.co.id/services/registered-securities/shares/lc/BBCA) | BBCA/ID1000109507, ordinary share, IDX, IDR and listing date visible. Registration Active is not exchange TRADING. Action table includes historical Active/Cancelled rows and a 2021 mandatory conversion; no original publication/revision chain or coverage assertion. Price table returned sparse older observations, not a demonstrated current bounded daily API. |
| K3 | [KSEI download index](https://web.ksei.co.id/data/download-data-and-user-guide?setLocale=en-US) | Public index explicitly links annual combined/action-category XLS downloads. One ordinary GET inspected actual links; three files were downloaded for research. They are real machine-retrievable files, not hypothetical endpoints. Exact findings below. |
| K4 | [KSEI current action calendar](https://www.ksei.co.id/en/service-support/schedule/schedule-of-corporate-actions) | Dated categories and cum/record/effective filters are visible. Broad issuer-name results include debt; require exact security mapping. No exhaustive all-type historical/cancellation or delivery-completeness declaration established. |
| K5 | [KSEI FAQ](https://web.ksei.co.id/faqs?setLocale=en-US) | Describes issuer-supplied action schedules and electronic dissemination, and account-holder access to C-BEST/ORCHiD reports. These participant facilities are not demonstrated free personal APIs; investor access is not equivalent to participant entitlement. |
| K6 | [KSEI disclaimer](https://web.ksei.co.id/disclaimer) | Fresh web and ordinary GET returned 500. Prior ledger inspected copyright/disclaimer but established no adequate recurring/immutable retained-use grant. Current failure does not prove prohibition; permission remains unresolved. |
| B1 | [BCA action index](https://www.bca.co.id/id/tentang-bca/tata-kelola/aksi-korporasi) | Public issuer disclosures, useful positive corroboration. No documented immutable event index, updated-since API or complete all-type interval export/retention permission established. Research-only until those gates close. |
| O1 | [OJK statistics notice](https://ojk.go.id/id/kanal/pasar-modal/data-dan-statistik/statistik-pasar-modal/default.aspx) | Weekly/monthly statistics moved to its data portal from August 2025. This notice is not a daily administrative-status, exceptional-closure or completion feed. No OJK dataset is assigned such a claim. |
| E1 | [EODHD EOD documentation](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes) | Documented per-symbol GET with bounded dates and JSON/CSV; raw OHLC, adjusted close and split-adjusted volume. Free own-key access advertises past-year history. No JK-specific synthetic/placeholder/no-execution convention or raw revision linkage established. T3 is acceptable for its own observations once exact premises are proved. |
| E2 | [EODHD limits](https://eodhd.com/financial-apis/api-limits), [terms](https://eodhd.com/financial-apis/terms-conditions) | Free 20 units/day, EOD 1/request, usage 0, bulk 100; qualifying private storage/manipulation/analysis permitted. Account required; registry/actual entitlement not checked. Preserve current local ceiling 16. No perpetual/post-termination assumption. |
| T1 | [Twelve Data market coverage](https://twelvedata.com/stocks/), [IDX page](https://twelvedata.com/exchanges/xidx), [pricing](https://twelvedata.com/pricing) | Indonesia listed at Pro+/Venture+; Basic international access is trial symbols. Does not demonstrate sustainable free BBCA/panel coverage; no subscription recommended. |
| A1 | [Alpha Vantage documentation](https://www.alphavantage.co/documentation/) | Free-key daily compact endpoint offers 100 observations; full history requires premium. Exact BBCA/IDX mapping, required authenticity and rights not established. A generic global endpoint is not proof of JK support. No account/data calls. |
| G1 | [GOAPI hub](https://goapi.io/hub/), [docs](https://goapi.io/docs/), [Swagger](https://goapi.io/swagger/) | Stock Market IDX advertises Free Trial, unlike other products marked Free. Docs rendered no usable schema in the reader. No sustainable zero-cost, retention or genuine-EOD path established. Free signup alone is insufficient. |
| Y1 | [Yahoo API terms](https://legal.yahoo.com/us/en/yahoo/terms/product-atos/apitnc/index.html) | Discovery succeeded but direct document access failed in this review. No supported permitted JK historical API/retention grant established. Undocumented chart endpoints/yfinance are not selected; no unverified legal conclusion or bypass proposed. |

K3 primary file inspections (not admitted market facts):

| Explicitly linked file | Response / retained identity | Inspection |
|---|---|---|
| [combined 2026](https://web.ksei.co.id/Download/schedule_ca_keseluruhan_2026.xls) | HTTP 200; 5,527,235 bytes; SHA-256 `1e1d15f5fc55e2a9b48714d723c0d4a84880dc4ccad227a1edfcab2d7b602362` | HTML table served as XLS: 9,516 data rows, 15 columns; title explicitly organized by recording-date year. Nine observed categories, including mandatory/voluntary conversion, right distribution, stock/mixed/cash dividends, interest, redemption and voting. This is not a verified exhaustive taxonomy. |
| [conversion 2026](https://web.ksei.co.id/Download/mconv_2026.xls) | HTTP 200; 269,914 bytes; SHA-256 `b6b953c36495855c2e55e40a3fe464d23c25c39e5f667fdb7ed6b30e9a00ed63` | HTML-as-XLS: 390 data rows, 15 columns. Reference number and letter date each absent in 385 rows. Debt amortizations are present; do not interpret every mandatory conversion as an equity split. |
| [merger 2026](https://web.ksei.co.id/Download/Merger_2026.xls) | HTTP 200; 25,088 bytes; SHA-256 `632182f3f105e083ed21f08ff48d6d067bfb397512320430a9c853692be2f564` | Binary XLS downloaded; cells unverified because bundled XLS reader unavailable. No dependency installed, no content/coverage claim inferred from its filename. |

The two parsed tables expose security code/name, action type, cum/record/distribution
dates, exercise/proceed instruments and ratios, description, reference and letter
date. They do not expose a dedicated immutable event ID, revision/predecessor,
cancellation or publication timestamp column. The detail page has some cancellation
labels, but that does not supply missing links/times in exports. Source field
meaning still needs a dictionary; distribution/record dates are not automatically
exchange ex/effective dates. Include adjacent recording years for effective-window
queries; never filter discovery solely by publication year or issuer name.

Combined export exceeded the initial 4 MiB research cap; a second, explicitly
bounded 32 MiB research retrieval retained the complete 5.53 MB response. Production
bounds were not changed. Both HTML files returned Last-Modified
`2026-10-08T01:00:08Z`; merger returned `2026-01-26T04:43:27Z`. These are transport
metadata, not authenticated event chronology or an update SLA. Replacing a yearly
URL cannot supply versions missed before local acquisition.

## Claim architecture: mandatory and optional

All candidate source assignments below are conditional on permission, scope and
typed admission; none upgrades an unresolved source to VERIFIED.

| Claim | Acquisition mode | Candidate authority/source and minimum admission boundary |
|---|---|---|
| A stable identity | Bootstrap snapshot + changes + monthly reconciliation | KSEI master/detail plus operative exchange/issuer mapping; exact local ID ↔ ISIN ↔ dated source code, never symbol suffix guessing. Point snapshot cannot invent past/continuing identity. |
| B ordinary security type | Snapshot + explicit changes | KSEI exact ordinary-share classification/issuer evidence; EQUITY alone insufficient. Recheck on identity change, not gratuitously every day. |
| C listing/delisting | Bootstrap + governing events + reconciliation | Operative IDX acts; reviewed issuer listing fact where binding permits. Listing coverage must remain applicable; missing delisting notice is not proof of continued listing. |
| D board/mechanism | Versioned rules + board/exception events + reconciliation | IDX board assignment/change, effective rules, monitoring/mechanism exceptions. Board does not prove mechanism or status; intraday scope unsupported by DateOnly binding blocks affected date. |
| E affirmative TradingStatus | Exact daily snapshot OR proved bootstrap + complete transitions; reconciled before evaluation | T1/T2 exact-session status; T1 suspension/reopening acts with original references. Missing initial affirmative state or transition coverage blocks. No activity substitute. |
| F exchange calendar | Annual snapshot + amendments + reconciliation | Operative IDX schedule, or admissible independent T2 proof under contract. KSEI service calendar only corroborates its declared scope. Unknown weekdays stay unknown. |
| G exceptional closures | Governing events + final daily reconciliation | Closure/reopening/change notices with explicit exchange/date scope. Annual schedule alone is insufficient. |
| H completed session | Independent daily session fact | IDX final session artifact or admitted independent authoritative report, exact date/session and genuine completedAt. Report date, scheduled close, HTTP date or price row cannot fill completedAt. |
| I genuine EOD prices | Bounded daily observation feed + correction overlap | EODHD `/api/eod/BBCA.JK` (pilot) once E1 gaps close; raw OHLC, documented genuineness/flags, source convention, exact completion and revision references. |
| J currency/price convention | Authoritative identity metadata + versioned provider convention | KSEI/issuer currency is not proof that a vendor field is rupees/rupiah per share. Require exact endpoint/field/unit and continuity bindings; never use face value as market-price convention. |
| K actions/comparability | Positive events + complete bounded snapshots/index reconciliation | KSEI K3/K4 + issuer/IDX originals, with an authoritative closed coverage basis for all frozen break types, amendments and cancellations. Derive comparability locally for exact selected revisions/window. |
| L quantity/volume basis (optional) | Versioned convention + observations | Exact provider unit/adjustment scope; otherwise relative volume unavailable. |
| M observation market segment (optional) | Versioned field metadata | Missing observation segment limits local features. Administrative mechanism/market applicability under D/E is still mandatory. |
| N benchmark/RS (optional) | Separately admitted aligned observation history | Optional JKSE.INDX with its own convention/completion/comparability; absence does not block stock-only Level 1. |
| O liquidity (optional) | Documented quantity/value observations | Actual traded value requires its own meaning. Price × quantity only a labelled proxy after unit compatibility; otherwise unavailable. |

## TradingStatus options and completeness

| Option | Correctness/PIT/completeness | Access/cost/fragility decision |
|---|---|---|
| A daily authoritative snapshot | Strong for its expressly covered date/scope; no extrapolation backwards or through intraday ambiguity. Must contain affirmative state and complete relevant market scope. | Simplest if a permitted zero-cost exact-session export exists. Not established. Daily download is not inherently wasteful for this volatile claim. |
| B initial state + transitions | Safe only with authoritative affirmative bootstrap and complete sequence/index through target cutoff, covering restrictions, suspension, resumption, corrections/cancellations. Apply frozen scope/lineage resolution, not generic last-row-wins. | Pure public notice search rejected: no closed delivery or recovery basis. Sequence numbers must cover a defined series, not merely look monotonic. |
| C bootstrap + transitions + reconciliation | Preferred target. Completeness checkpoint before promotion; missing sequence/page or disagreement closes the affected interval. New snapshot can prove its own scope, not the unknown interval since an older snapshot. | More resilient than B but not a cure for unproved coverage. Requires both permitted event and checkpoint delivery. Monthly/weekly status comparison alone cannot safely permit intervening daily candidates. |
| D documented authoritative consolidated daily eligibility assertion | Equivalent to A only when meaning covers required administrative scope and authority is admitted. | No such free feed established. Executions, Active registration, normal board, Remarks2 or positive volume rejected as substitutes. |

For a complete transition path require publisher-defined universe/event taxonomy,
checkpoint/watermark and pagination boundaries, gap recovery, correction channel,
latency policy and effective market/session scope. Retain index/checkpoint bytes
as well as notices. Traverse only documented permitted interfaces. Local record
counts detect anomalies; they do not prove exhaustive source coverage.

If a source supplies only snapshots, use them only for their explicit scope.
Do not synthesize a continuing TRADING payload: the frozen TradingStatus binding
is SESSION_FACT; authenticated Reopening has a continuing class and exact
suspension reference. Any planned transition-derived exact-session assertion must
have an admitted source coverage/meaning basis and a supported typed proof path;
T4 cannot manufacture administrative authority. A representation gap is a future
design gate, not permission to change the frozen codec here.

## Sessions and corporate actions

Calendar bootstrap must acquire the operative annual schedule and known amendments
automatically, including previous years if the seed crosses a year boundary.
Then acquire independent daily completion and exceptional changes. Recovery must
enumerate expected artifacts from an authoritative index, not manufacture a list
of completed weekdays. A final report may support completion meaning only after
its clock specification is authenticated. The prior October 2 statistics report
and its missing completedAt remain prior findings; this review does not reinterpret
the clock or redownload 50 PDFs manually.

For actions, the new K3 exports are a stronger *candidate discovery/reconciliation*
route than keyword search. They still lack an established contract-level complete
coverage basis. Require split, reverse split, rights/share events, bonus/stock
distribution, conversion, merger/reorganization, identity/basis changes and every
relevant amendment/cancellation. A missing event, unsupported category or missing
referenced original leaves PARTIAL/UNRESOLVED. Announcement time and all market-
effective dates remain separate. Cash dividends retain existing price-only policy.

**NEGATIVE_INFERENCE_NOT_SUPPORTED** for current public status/action evidence.
Two matching snapshots cannot detect an intermediate A→B→A change, nor an action
later removed from a mutable export. Positive events can establish a KNOWN_BREAK;
neither matching current totals nor an empty BBCA window establishes CLEARED.
Source-level authoritative completeness is necessary under the frozen binding;
local reconciliation alone cannot generate a FULL coverage assertion.

## Price decision

Keep EODHD as the preferred conditional T3 observation candidate; do not reject it
for being T3. Free personal documented API + bounded existing collector is a
better starting point than undocumented endpoints. Required remaining statement:
exact JK feed/field currency and unit, non-synthetic/non-placeholder/substitution
behavior (including no-trade/non-trading dates), zero-volume meaning and correction
identity rules. Generic raw OHLC documentation neither proves every JK bar genuine
nor proves it synthetic. Optional quantity ambiguity does not reject proven prices.

Use raw OHLC, not adjusted close, for the frozen technical path. Archive every
received original and source-document version. Local hash/retrieval histories
authenticate observations from acquisition onward, not the vendor's historical
publication time. Refetch a bounded overlap to detect changes; longer historical
reconciliation detects older corrections later but cannot guarantee detection of
transient revisions or replace a required source lineage. Do not assign native
revision IDs from row order, date alone or HTTP Last-Modified. Conflicting or
unlinked changes stay quarantined until mapped under existing correction rules.

Alternative free paths were considered: official IDX files (automation gate), KSEI
price table (no demonstrated complete current feed/convention), Alpha Vantage
(exact JK support unproved), Twelve Data (IDX paid tier/trial limitation), GOAPI
(trial, not proved sustainable free), Yahoo (no verified supported route). None
closes the observation gate today; this is not proof that all possible free APIs
are impossible. No paid shopping or automatic fallback is selected.

## Three architectures and qualitative comparison

**A — Strict official free:** official reference/status/calendar/closure/completion,
official raw prices, official action exports and their revisions. Avoids vendor
mapping, but actual permission and complete datasets remain unavailable.

**B — Hybrid free:** official daily administrative/session snapshots and complete
action coverage, with EODHD prices. Reduces price-acquisition burden; each daily
authoritative snapshot must independently prove its scope. No transition shortcuts.

**C — Free automated with reconciliation (selected target):** official bootstrap,
permitted reference/status/action transitions, checkpoint and snapshot comparison,
daily independent session facts, and EODHD observations. Static reference is not
refetched needlessly; volatile eligibility is closed before each evaluation.

| Dimension | A strict official | B hybrid snapshots | C hybrid reconciled |
|---|---|---|---|
| Correctness | Strong if exact official meanings delivered; currently blocked | Strong only with full daily premises; currently blocked | Strong with closed intervals; fail closed during gaps; currently blocked |
| PIT safety | Retained versions needed; public mutable pages insufficient | Vendor and official clocks kept separate | Explicit checkpoint/event/snapshot clocks; no retroactive repair |
| Authority | Governing for exact claims, not organization-wide | Official state; T3 own price observations | Same; reconciliation never upgrades T3/T4 authority |
| Missing-event risk | Daily snapshot misses intervening changes | Lower target-state risk, action-history risk remains | Detectable gaps if source checkpoints exist; periodic snapshots alone insufficient |
| Automation completeness | No permitted complete path established | Price interface documented; official gates open | KSEI exports promising; critical official gates open |
| Zero-cost sustainability | Public access is not an automation entitlement | Bounded EOD budget plausible | Same budget, shared exports reduce repeated work |
| Operational complexity | Multiple official formats | Simpler snapshot dependency graph | Moderate: checkpoint closure and reconciliation required |
| Maintenance burden | Website/PDF changes and terms | Official changes plus vendor conventions | More state tracking, fewer redundant reference fetches |
| Historical bootstrap | Archived originals/clocks/coverage unproved | Price range plausible; remaining history unproved | Same; prospective accumulation does not repair prior missing history |
| Revision handling | Need source originals and links | Capture vendor differences + official lineage | Retain all versions; reconcile disagreements, never overwrite |
| Permission risk | IDX crawling prohibited; other rights unresolved | EOD private scope clearer; official gates unchanged | Same; machine-readable KSEI exports do not automatically grant rights |
| Bounded personal fit | Potentially suitable, not established | Good transport/budget fit if state proof exists | Preferred balance after exact gates close; no FullIdx claim |

No numeric aggregate score and no paid architecture: escalation gate not met.

## Preferred architecture specification

- **REFERENCE SOURCE:** KSEI explicit master/detail snapshots plus authoritative
  IDX listing/board/mechanism acts; permitted retrieval and scope still required.
- **TRADING STATUS:** authoritative positive bootstrap + complete status events +
  pre-evaluation complete checkpoint; exact daily status snapshot may cover its
  own scope when event continuity is unavailable. No proved delivery route yet.
- **SESSION CALENDAR:** operative IDX annual snapshot + amendment/closure events;
  separately scoped KSEI calendar corroboration only.
- **SESSION COMPLETION:** independent official/admitted final daily artifact with
  authenticated completedAt; source/interface/clock gate open.
- **PRICE:** EODHD own-key Free bounded observations, conditional on exact JK proof.
- **CORPORATE ACTIONS:** KSEI explicit yearly/category exports + linked original
  notices/issuer corroboration + authoritative complete-window basis; gate open.
- **RECONCILIATION:** daily status/closure/action coverage checkpoints, weekly
  bounded corrections/reference comparison, monthly master comparison. Point
  snapshots never silently extend to intermediate dates.
- **BOOTSTRAP:** automatic bounded BBCA proof package below; no user downloads.
- **STEADY-STATE CADENCE:** proposed schedule below, enabled only after source gates.
- **FALLBACK BEHAVIOR:** retain last authentic evidence for its original scope;
  mandatory gap → DATA_BLOCKED, no setup/candidate; optional gap → local unavailable.
  Never switch provider silently or fill a missing day.
- **COST:** intended zero subscription/data cost, existing local compute/storage;
  no demonstrated complete free stack. One free EODHD account/key required.
- **MANUAL OPERATION:** **NONE**. One-time account/permission setup is distinct from
  recurring market-data handling. No manual mandatory-data bootstrap exception.

## Automatic historical bootstrap and 50-session warmup

Pilot subject: BBCA / ID1000109507, with retained local instrument/exchange binding.
Use prior candidate **2026-07-23…2026-10-02** as a bounded test request, not a claim
that it contains 50 proven exchange sessions. At present the package cannot run
to successful readiness because the source gates remain open.

1. Automatically retrieve entitled source terms/specifications, historical identity,
   type, listing, board/mechanism and affirmative status evidence. Current snapshots
   cannot fill July scopes. Resolve dictionaries by historical effective version.
2. Automatically enumerate operative calendar/amendment and independent completion
   artifacts for the range; authenticate completion clocks. Assert no weekday OPEN.
3. Obtain complete action/reference/currency continuity for the entire seed and
   any later recurrence, including earlier announcements effective inside it and
   adjoining recording years. Acquire event originals before coverage records.
4. After source gates, use one bounded EOD request for BBCA.JK history, with actual
   request dates, daily JSON and explicit order; preserve bytes and revisions.
   Existing collector allows only the fixed 11-symbol panel, not BBCA-only execution;
   its acquisition helpers are reusable but no current BBCA-only invocation is
   claimed. A future scoped collector entry must preserve the established budget.
5. Archive and append premises before dependents, setting knowledge to actual
   receipt/admission. A later-cutoff query can use acquired historical facts; it
   cannot create past system knowledge or fix old captures/Outcome deadlines.
6. Require **50 consecutive comparable, genuinely observed completed sessions** for
   EMA50's seed. Apply exact historical status/reference facts where readiness/
   episode replay requires them; current status does not cover historical days.
   If a known break occurs, restart after it. If a required date is unresolved,
   block rather than skip it. Extend within a bounded 330-day collector horizon,
   then report insufficient history if still short. No arbitrary forever-backfill.

Use per-date/per-subject reads so the union of records and exact premises respects
the 512-record reader limit; 50 dates × several claims can exceed it. Separate
artifact admissions do not waive that read bound. Existing migrations 0007/0008
would need separately authorized deployment if absent, not a new migration here.
If historical status/action proof does not exist, prospective automated accumulation
can eventually warm up only after all forward sources are complete; it is not an
automatic historical bootstrap success.

## Proposed cadence and request budget

Times are **Asia/Jakarta**, engineering choices, not discovered publisher SLAs.
No schedule is installed. Only documented permitted source modes may be enabled.
The monthly master index is actually monthly; one observed HTTP modified time is
not proof of daily publication. Existing safe EOD cutoff is 19:00; EODHD describes
typical updates 2–3 hours after close, not guaranteed JK readiness.

| Time / trigger | Proposed bounded operation |
|---|---|
| Daily 08:00 | Retrieve permitted schedule/status/change checkpoints including prior-day delayed notices; reconcile pending gaps. This is provisional pre-market evidence, not proof of status for the completed target day. |
| Daily 19:00 | Acquire final target-scope status/checkpoint, closure/completion, actions and original revisions; then eligible EOD observations and readiness in dependency order. Missing final artifacts remain pending/blocked. |
| Next day 08:00 | One bounded catch-up for missing final artifacts after a new provider quota day; use only original effective dates and new knownAt. Do not keep retrying indefinitely. |
| Event notification | If an authorized durable feed exists, process its identified event, then reconcile checkpoint. No undocumented polling loop or assumed RSS source. |
| Weekly Saturday 09:00 | Reconcile active technical recurrence price range and reference/action originals under quotas; no new bars inferred on closed days. |
| First five calendar days each month, 09:00 | At most one master-index check/day for a new dated monthly snapshot; retrieve only a newly linked version. Thereafter weekly check if delayed; incomplete expected release stays flagged. |
| New annual schedule / rule version | Retrieve once when indexed, retain predecessor and effective mapping; daily checkpoint discovers amendments. |

EOD account ceiling is **20/day**, existing stricter local ceiling **16/day**.
Normal fixed panel costs **11 EOD units**; at most **5** remain locally for recovery/
correction work, reduced by any other account usage. `/api/user` costs zero units
but still counts HTTP requests. Reset midnight UTC = 07:00 WIB. Reserve attempted
cost before requests, including failures; do not count welcome bonuses, buy units
or rotate accounts. Whole-exchange bulk costs 100 and cannot fit this free plan.
FullIdx is therefore neither implemented nor claimed feasible through this path.

For a future BBCA-only authorized proof, one bounded historical request is one
unit; required official evidence is shared where possible. Existing collector's
no-automatic-retry behavior is retained; catch-up above is future explicit bounded
orchestration, not a claim it already retries. Official source cadence/rate ceilings
require confirmation before implementation. Avoid full annual multi-megabyte
downloads twice daily unless source update semantics justify them; prefer an
authorized update index and conditional retrieval, which are **not yet proved**.

## Failures, degradation and health

Apply this matrix separately to KSEI reference/action, IDX administrative/session,
issuer notices and price-provider acquisition. No source gets a silent success path.

| Failure | Source-specific handling and claim consequence |
|---|---|
| Unavailable / timeout | Preserve received evidence; one bounded deferred attempt, then source outage. Block mandatory uncovered scope; optional features unavailable. |
| Delayed publication | Keep PENDING vs outage distinct using expected source release contract; passed local clock proves nothing. DATA_BLOCKED until exact proof arrives. |
| Malformed / HTML challenge / schema drift | Quarantine bytes where licensed, record parser/version failure; append no partial semantic batch. Never reinterpret a login page as empty data. |
| Missing instrument | Mapping/coverage gap, never delisting, suspension or synthetic bar. Reject ambiguous code/ISIN reuse. |
| Partial update / missing page | Do not advance coverage checkpoint or issue COMPLETE. KSEI counts alone are anomaly checks; IDX empty filter is not status. |
| Revision / correction | Retain original and new bytes; authenticate logical identity/predecessor under frozen rules. An unlinked change is unresolved, not newest-row truth. |
| Permission / 401 / 403 | Stop that source; retain permission failure separately; no browser/WAF workaround or undocumented fallback. No deletion or continued use beyond retained rights. |
| Rate limit / 402 / 429 | Honor allowance/Retry-After, defer within global cap; no paid escalation or unbounded retries. |

Current evaluation lacks required evidence → DATA_BLOCKED, setupEvaluated false,
no candidate. An authentic supported negative (e.g. suspension) instead yields
the frozen INELIGIBLE result. Independent valid diagnostic facts may remain visible.
Known breaks trigger restart/warmup; optional benchmark/volume failures never become
global blockers. Existing immutable results are not rewritten after recovery.

Minimum source-health record/log (design only): source and parser/spec/terms version,
last attempted/successful retrieval; sanitized request/window; HTTP/disposition;
latest artifact ID/hash/length; latest admitted effective date and knownAt;
authoritative expected vs actual counts when available (otherwise UNKNOWN), missing
instrument count; native checkpoint/coverage interval; revision/cancellation counts;
parser failures; stale/delayed publication; uncovered intervals; permission state;
account units reserved/remaining and archive bytes. Separate transport/permission/
parser failure from authentic market-negative evidence. HTTP 200 with no new
artifact is not newly admitted evidence; no event is not proof that a feed is alive.

## Existing code reuse and implementation limits

No graph exists; code and relevant contracts/runbook were inspected directly.

| Existing piece | Reuse / limitation |
|---|---|
| `Infrastructure/RawArtifactArchiver.cs` | Reuse content-addressed retention unchanged; importer re-authenticates bytes. Hash proves identity/integrity, not truth/authority. |
| `Infrastructure/BoundedEvidenceIngestionService.cs` | Reuse archive-before-parse, source permission check, explicit adapter tuple, raw commit then atomic semantic append. No acquisition/default adapter exists. |
| `Application/BoundedEvidenceIngestion.cs` | Reuse trusted mappings, chronology, universe, 4 MiB/512-row bounds and lineage checks; no source may appoint its own authority. |
| Existing `source` registry | Must independently be ALLOWED for precise permission scope; unchanged here. Does not replace claim authority checks. |
| `BoundedEvidenceSource.Normalize` adapter boundary | Future reviewed Python parsing/provider-neutral typed output; no installed production adapter to reuse. Keep provider logic out of domain. |
| `Application/ScreenerEvidenceV02Binding.cs` and payload codecs | Reuse strict typed versions/classes, exact references, equality and cancellation semantics unchanged. No generic JSON clearance. |
| `Infrastructure/ScreenerEvidenceV02Store.cs` | Reuse immutable append and exact premise authentication; no snapshot overwrite. |
| `Infrastructure/ScreenerEvidenceV02AsOfReader.cs` | Reuse stored-only cutoff/lineage reading; preserve 512-record union bound, no network recovery during replay. |
| `Infrastructure/ScreenerEvidenceV02ReadinessService.cs` and application evaluators | Reuse independent eligibility, completion, comparability and historical readiness; no importer bypass. |
| Existing technical/capture/candidate/Outcome V0.2 chain | Consume ordinarily admitted readiness; no recomputation or policy conversion of immutable artifacts. |
| `collectors/python/src/idx_stock_collector/pilot.py`, `budget.py`, `pilot_daily.py` | Reuse bounded date validation, redacted provenance, account preflight and cost reservation concepts/helpers. Current CLI is fixed-panel/manual-session V0.1 flow, not an automatic V0.2 BBCA bootstrap or runner. |
| `collectors/python/src/idx_stock_collector/ksei_master.py` | Existing research inspector only; not an admitted ordinary-share/status adapter. |

K3 combined export exceeds **both 4 MiB and 512 source rows**; monthly KSEI master
also exceeds source-row bound under the prior inspection. Filtering 9,516 rows to
BBCA before declaring a 512-row source is not an unchanged-framework solution.
Prefer genuine publisher-bounded exports with full provenance; if unavailable,
require a separately authorized bounded bulk-acquisition design review before
implementation. Do not split/relabel local extracts as original publisher bytes,
silently drop records or change bounds. Even conversion export fitting bounds
does not resolve semantic completeness/permission.

Security/operations: one local nonprofessional account token from ignored
environment/OS secret storage; never put query tokens in archive names, URLs,
logs or Git. Account activation/permission setup is one-time, not manual fact
entry. Source-isolate endpoints, allowlist destinations, verify TLS, stop unexpected
redirects/challenges, cap compressed and decompressed sizes and parser time,
disable executable spreadsheet/PDF content, and use concurrency one per source
(at most two unrelated sources) initially. Respect actual tighter published limits.
Record local parser/convention versions, hashes and clocks; no local AI truth step.

Archive growth must be budgeted without deleting evidence needed for replay.
For scale illustration only, one changed 5,527,235-byte combined export on each
of 250 acquisition days is about 1.29 GiB/year before backups; two changed versions
per day double it. Deduplicate identical bytes but preserve each legitimate receipt
identity separately. Current source/content uniqueness rejects conflicting metadata
for identical bytes; a later fetch cannot extend scope or be re-imported with fresh
clocks to simulate a new event. If acquisition observations need independent
persistence, resolve that in a future scoped design, not a migration here. Alert on
disk/permission limits, preserve originals and licensed backups; no silent eviction.

## Exactly three remaining blocker packages

These group related admission work; they do not hide unproved mandatory claims.
"Exhausted" below means the concrete public approaches checked, not a proof that
every possible free arrangement is impossible.

1. **Authoritative market/session control evidence (A–H, J metadata):** exact
   affirmative status, historical identity/listing/board/mechanism applicability,
   exchange schedule/exception coverage and actual completion clocks, via a
   permitted free automated route. Public IDX snapshots/notices/statistics,
   transition search, KSEI registration/calendar, OJK aggregate reports and
   reconciliation were evaluated. Access restrictions, missing state/clock and
   coverage meaning remain; free personal login/participant access does not prove
   a suitable entitlement. **One next action:** obtain an IDX response/specimen
   defining a zero-cost personal automated export/feed for BBCA and its dictionary,
   status/checkpoint recovery, calendar exceptions and final-session clock. Explicit
   "not available" answers also resolve this inquiry. No message sent.
2. **Genuine JK observation convention (I, J field semantics):** EODHD Free API is
   plausible; generic raw fields do not close JK padding/synthetic/placeholder,
   unit or correction semantics. Official files, KSEI table, Alpha Vantage,
   Twelve Data, GOAPI and Yahoo do not establish a better complete free route.
   **One next action:** obtain one endpoint-specific EODHD JK convention statement
   covering these behaviors and personal raw/derived/replay retention, without
   upgrading the account or buying data. No message sent.
3. **Complete action/continuity coverage (K):** KSEI annual/category exports,
   current calendar, historical detail cancellations, issuer notices, snapshots
   and reconciliation do not establish all-type effective-window completeness,
   correction lineage or permission. Filename/category/empty result is insufficient;
   combined export also exceeds current ingestion bounds. **One next action:**
   obtain KSEI's definition/permission and one bounded BBCA complete-window export
   or equivalent authoritative coverage mechanism, including cancellations and
   recording-year/effective-date semantics. No message sent.

## Paid escalation and conditional next sequence

Escalation checks 1–6 were evaluated: exact mandatory claims, free official paths,
appropriate third-party observations, multi-source composition, snapshot/event/
reconciliation alternatives and free-account possibilities. Check 7 remains
"unsafe/unproved with current evidence", not universal free impossibility.
**Check 8 fails: no paid provider has demonstrated closing an exact remaining claim.**
Earlier IDX Reference product existence is insufficient. Classification D and
vendor shopping are therefore unwarranted. If later justified, restrict the paid
capability to the exact missing administrative/completeness fact and preserve the
free observation stack where valid; this is an escalation rule, not a paid proposal.

Immediate work is the three targeted confirmations above, starting with the IDX
control-evidence package. No additional broad survey and no production adapter
is unblocked. Conditional sequence only after the mandatory source gates close:

1. Freeze source acquisition permissions/specifications and resolve bounded payload
   and typed status-proof compatibility; leave frozen financial semantics intact.
2. Reference/status acquisition, then calendar/closure/completion acquisition;
   each slice checks scope, corrections, missing delivery and authenticated bytes.
3. Price acquisition with convention/admission checks; action event/coverage
   acquisition with cancellation and missing-category tests.
4. Automatic BBCA historical bootstrap, reconciliation, and authentic 50-session
   readiness proof at an honest later cutoff; prove no manual production data work.
5. Only then Daily Runner and future real-session soak. Neither is authorized now.

## Validation and research activity

Documentation only: dedicated decision plus one appended runbook gate. Prior
source history and both frozen V0.2 documents untouched. No code, adapter, schema,
registry, FullIdx or soak changes. No database connection/query/mutation, operational
collector, runner, scheduler or archive import invoked. Historical operational
schema/soak counts in the ledger were not freshly measured in this review.

Fresh checks: primary pages directly accessed; KSEI published links followed,
complete combined/conversion table shapes inspected, three research file byte/hash
identities recorded. Binary merger cells remain unverified (missing XLS reader).
Initial sandbox network resolution failed; a permitted ordinary network request
succeeded, without source access-control evasion. KSEI disclaimer returned 500;
some public readers yielded shells/errors. No production viability based only on
search snippets or these failures. No hidden endpoint probing or account creation.

Research files stay outside Git under temporary storage. **Five HTTP 200** ordinary
research responses: one download-index GET, three file responses (combined first
stopped at 4 MiB+1), and the complete combined re-fetch. One disclaimer GET returned
HTTP 500. The first sandbox DNS failure reached no source. Public web
search/reader calls were additional and nonzero. Market-data
provider API calls/units **0/0**, paid use **0**. No credential values accessed.

Required documentation checks: intended-file scope, preserved historical runbook
prefix/source ledger/contracts, exact staged and unstaged diff, `git diff --check`.
No build/full tests required or claimed. Commit scope: this document and the
runbook gate only, message `docs: define automated level1 source architecture`;
no push. Commit/final status are reported after execution, not predicted here.

---

## Slice 5A.2e — automated IDX control-evidence route confirmation (2026-10-08 WIB)

**Decision: D — IDX_AUTOMATION_PERMISSION_BLOCKED.** Relevant IDX control-data
does exist on the public website (suspension notices, exchange trading-holiday
schedule, dated statistical publications) and the exchange operates a documented
system-to-system market-data programme, but the published terms prohibit
unattended website automation (web scraping/crawling) and the only permitted
automated delivery channel is a **paid, contractual data licence**. No free,
permitted, automated, authoritative IDX route or composition was found for any
mandatory Level-1 control claim. This section is additive; it does not rewrite
the prior research prefix, the [source ledger](LEVEL1_SCREENER_SOURCE_STACK.md) or
the frozen contracts.

This resumes the interrupted Slice 5A.2e. The actual repository was re-inspected
before work: `main` at `74d64065099c2e313a66a5a80d284aa4676ac1e4`, clean worktree,
13 ahead / 0 behind locally recorded `origin/main`, no staged/unstaged/untracked
changes — so the interrupted run had left **no partial files** to reuse and no
unrelated user work to protect. Nothing was reset, restored or deleted.

### Exact IDX routes inspected

All probes were ordinary, bounded, attributed browser-style GETs. No WAF,
CAPTCHA or rate-limit bypass; no login; no account creation; no aggressive
crawl. `www.idx.id` is the current official site; `www.idx.co.id` is also
official and sits behind a Cloudflare WAF that 403s non-browser clients.

| Route | Response | Bearing on control evidence |
|---|---|---|
| [IDX Data Services product page](https://www.idx.id/id/produk/layanan-data-bei/) | 200 HTML, 253,443 B | Documents the licensed **system-to-system** market-data programme (Real Time / Delayed / EoD / Log Data) and product catalogue. Documentation page, not a data feed. |
| [IDX website usage terms](https://www.idx.id/id/syarat-penggunaan/) | 200 HTML, 171,067 B | Noncommercial citation of website data permitted **with full source attribution and access date**; **web scrapping/crawling not permitted**; commercial redistribution requires prior written consent; terms may change without notice. |
| `www.idx.co.id/id/data-pasar/laporan-statistik/statistik/` | 403 | WAF blocks non-browser clients (no login/JS challenge solved). |
| `www.idx.id/id/data-pasar/laporan-statistik/statistik/` | 200 (SPA payload embeds dated `ds_YYMMDD.pdf`, `ws_*`, `ms_*` links) | Free data is **static PDF only**; no CSV/XLSX/JSON/feed; UI page. |
| `www.idx.id/id/data-pasar/laporan-statistik/digital-statistic` | 200 (SPA shell; 23 text lines, 0 media/API links) | UI shell; records loaded client-side only. |
| `www.idx.id/id/data-pasar/ringkasan-perdagangan/ringkasan-saham` | 200 (SPA shell; 20 text lines) | UI shell. |
| `www.idx.id/id/berita/suspensi/` | 200 (SPA shell; 17 text lines) | UI shell; no feed. |
| `www.idx.id/id/berita/jadwal-libur-bursa/` | 200 (SPA shell; 17 text lines) | UI shell; schedule not machine-retrievable as a documented feed. |
| `www.idx.id/robots.txt` | 404 | No robots exclusion file. |
| [www.idx.id/sitemap.xml](https://www.idx.id/sitemap.xml) | 200 XML, 464 URLs | No API/JSON/CSV/RSS/Atom path; only market-data pages, statistical reports, `news/suspension`, `news/trading-holiday`, `products/idx-data-services`. |
| [`data.idx.co.id`](https://data.idx.co.id/) (IDX Data portal, "Register and Subscribe to IDX Data") | 403 on `/`, `/robots.txt`, `/en/market-data` | WAF-challenged; not automation-reachable. Access/entitlement **PERMISSION_UNCLEAR**, no free tier demonstrated. |

### IDX system-to-system / data services

The product page states the service is **system to system** and offers Real Time,
Delayed, End of Day and Log Data. It is governed by a published
[IDX Data License Agreement (2026)](https://www.idx.id/media/xu0bddem/idx-data-license-agreement-2026.docx)
(77,855 B; SHA-256 `867b49bd7b261d5c8a77fb0aba94d6144313f49969e96ba3f19fb655820d56ae`),
the latest [General Terms of Use](https://www.idx.id/media/vaupc3md/20240215_final-general-term-of-use-version-0-3-2024-efektif-1-january-2024-2.pdf)
(1,881,521 B; SHA-256 `0f646e9c9eb3c415566c41b476836f5b8116d03350c127a8ccb05dc0dad98cd8`)
and a [catalogue/price list (2026)](https://www.idx.id/media/rzfl4wzy/20260513_idx-data-services-catalogue-pricelist-non-ab-2026.pdf)
(21,028,838 B; SHA-256 `57fb27132030e4383555eeb9a947b701beeb8365f46edd8d209bb4ef1b94401b`).
The agreement text is explicit: a Licensee must pay **Fees including a Security
Deposit**; the licence is non-exclusive, non-transferable, temporary and limited;
the fee schedule is published; direct connection requires a leased line via an
NSP (≥8.5 Mbps) or a registered Redistributor. **Automation is deliberately
intended for paying licensees.** This is therefore a **paid, contractual** route,
not a free public one. The 20 MB price list is published but its body text is
CID-encoded; this review did not extract exact figures and asserts no price.

| Product family | Free? | Access | Datasets documented | Administrative status? |
|---|---|---|---|---|
| IDX Market Data — Equity/Bond/Derivatives (Real Time, Delayed, EoD, Log/ITCH) | **Paid licence** | Agreement + Application Form + Fee + Deposit; NSP leased line or Redistributor | quotes, trade/depth, EoD equity (Basic/Professional/Indices/Indices Weight/Recapitulation), derivatives EoD, bond EoD/real-time, ITCH | no documented TradingStatus, suspension/resumption, calendar or session-completion product |
| IDX Data Reference | **Paid licence** | same | financial reports, corporate actions, other disclosure; IDXNet XML push (`idxnetPushFE0X3`) | listing status only (`statusEmiten=tercatat`), plus `dataVersion`, `correctionFrom`, `publishStatus`, `submitDateTime`; no suspension/status form shown |
| IDX Connection License (NSP) | Paid | licence | connectivity | n/a |
| IDX Index License | Paid | licence | index use | no |
| IDX Publication | Paid fee item/publication | licence | statistical publications reproduced from public reports | no |

Free **sample-data specimens** are downloadable to demonstrate the paid
products, but they are static specimens, not a live route, and using the data
requires a licence:

| Specimen | Identity | Content |
|---|---|---|
| [IDX Equity EoD Basic](https://www.idx.id/media/9651/idx-equity-eod-basic.zip) | 897,437 B; SHA-256 `aa570ccc74874fef121f81fb5d87bd9c92eccfff757e499e671197949f7f0cde` | fixed-width TEXT `STOCK QUOTATION`, one row per listed code: Code, Name, **Remarks**, Prev, Close, Change, %, Freq, Volume, Value and a trailing board-like digit; zero-volume rows included; **no explicit TRADING/SUSPENDED enum** |
| [IDX Equity EoD Recapitulation](https://www.idx.id/media/9654/idx-equity-eod-recapitulation.zip) | 75,542 B; SHA-256 `632a86526de15cc4ccb99e7c50d8578ee8ce91ac86f3f54209f861e987a83122` | aggregate `TRADING RECAPITULATION` |
| [IDX Data Reference sample](https://www.idx.id/media/3kynnbhi/20250206_idx-data-reference-sample_e0x3.txt) | 22,604 B; SHA-256 `69d43c810c859c6b7a9f46fba12713be59cc48408be2576b81bce80a94b870ef` | IDXNet XML push, form `E0X3` = corporate-action disclosure; no suspension/reopening/status message |

### Trading status, completeness, calendar, closure, completion, board

| Mandatory control claim | Free automated route found? | Finding |
|---|---|---|
| Affirmative TradingStatus (E) | **No** | No free automated affirmative status record. The free suspension page is a UI shell; the paid EoD quotation exposes only `Remarks` and a board-like digit, which prior frozen research already rejects as status substitutes. The paid programme's published product list does **not** document a TradingStatus/suspension/resumption product at all. |
| Status transitions / checkpoints (completeness) | **No** | No documented sequence ID, watermark, updated-since cursor, gap-recovery or publisher-complete status feed on any free route. Search silence on the suspension filter is not completeness. Remains blocked. |
| Exchange trading calendar (F) | **No automated route** | The annual/updated holiday schedule is published as a UI page; no documented free automated calendar file/feed. Automation of the page is scraping/crawling, which the terms prohibit. |
| Exceptional closures / amendments (G) | **No automated route** | Same permission basis; notices exist but no reconciled, versioned free automated closure feed. |
| Completed session (H) | **No** | The free daily statistics PDF carries a DATE only; no authenticated `completedAt` clock. The paid EoD product shows a DATE header, not a completion instant. No free automated completion artifact or clock was located. |
| Board / mechanism / reference (D) | **Paid only, partial** | Free site has no automated reference export. The paid EoD product exposes a board-like digit and `Remarks`; IDX Data Reference exposes `statusEmiten`/listing and disclosure. None is a free automated, dictionary-authenticated board/mechanism/reference feed. |

### Permission classification

| Candidate route | Classification | Basis |
|---|---|---|
| Public website data (statistics PDFs, suspension/holiday pages, SPA data) | **AUTOMATION_PROHIBITED** | Terms: web scrapping/crawling not permitted; noncommercial manual citation only with attribution + access date. |
| `data.idx.co.id` portal | **PERMISSION_UNCLEAR / ACCESS_BLOCKED** | WAF 403 to automation; no free tier demonstrated. |
| IDX licensed system-to-system data services | **AUTOMATION_ALLOWED** but **PAID + CONTRACTUAL** (agreement, fees, deposit, non-transferable) | Data License Agreement 2026 + General Terms + catalogue. |
| Redistributor-delivered products (e.g. RTI, IDX Solusi Teknologi Informasi, foreign vendors) | Paid; out of scope | Requires paid vendor subscription; not free. |

No free authenticated IDX account or self-service free API was demonstrated. The
only free account flow visible is the WAF-protected `data.idx.co.id` "Register and
Subscribe" portal, which does not evidence a zero-cost entitlement.

### Bounded automation feasibility and remaining blocker

Bounded unattended operation on a **free** IDX control route is **not feasible**:
the free data is UI/PDF-only and its automated retrieval is explicitly
prohibited, and the permitted automated channel is a paid licence. The mandatory
claims remain uncovered: no free affirmative TradingStatus, no completeness
mechanism, no free automated calendar/closure feed and no free authenticated
session-completion clock. **Daily Runner and adapters remain blocked.** The exact
remaining blocker is unchanged in substance: an authoritative, complete,
permitted, zero-cost automated IDX source for (1) affirmative exact-session
status with closed transition coverage, (2) the operative calendar/exceptional
closures and (3) a genuine session-completion clock. This review found the free
route closed by permission and the automated route closed by price.

### Paid-escalation status

**PAID_EVALUATION_NOT_YET_JUSTIFIED.** The frozen escalation gate requires a paid
provider *demonstrated* to close an exact remaining claim. IDX's paid programme is
real and automated, but its own published product catalogue and specimen data do
**not** demonstrate an affirmative TradingStatus / suspension-status /
exchange-calendar / session-completion product; the EoD specimens expose only
`Remarks`, a board-like digit, prices and a DATE. Paid necessity for the control
claims therefore remains unproved, exactly as the prior gate found. A low-cost
**inquiry** (not a purchase) to IDX licensing — whether a licensed product
provides administrative trading status, suspension/resumption, the operative
calendar and an authenticated session-completion clock, with retention terms — is
the only justified next action; no purchase, account or paid service was
activated.

### Validation and network activity

Documentation only. This section is appended; the prior architecture text, the
source ledger and all frozen contracts are unchanged. No code, adapter, migration,
source/permission registry, operational DB, evidence, Daily Runner, FullIdx or
soak change. Research bytes live outside Git under temporary storage;
no temporary research file was added to the repository. Ordinary HTTP probes:
`www.idx.id` 200s for the data-services page, terms, statistics index, daily
statistics, digital-statistic, ringkasan-saham, suspension, trading-holiday,
`robots.txt` (404) and `sitemap.xml`; `www.idx.co.id` statistics index 403;
`data.idx.co.id` 403 on three paths; four specimen/terms files downloaded
(`eod-basic.zip`, `eod-recap.zip`, `data-reference-sample.txt`, and the
docx/pdf terms + catalogue). Market-data provider API calls/units **0/0**; paid
use **0**; accounts created **0**; credentials accessed **0**; no WAF/CAPTCHA
bypass, no scraping loop, no paid activation. `git diff --check` clean; only this
document and the appended runbook note change; commit message
`docs: confirm automated idx control evidence route`; no push.

---

## Slice 5A.2f — EODHD JK price semantics confirmation (2026-10-08 WIB)

**Decision: C — EODHD_JK_PRICE_NOT_YET_ADMISSIBLE.** The documented Free EOD
interface is a feasible bounded acquisition candidate. Mandatory JK observation
authenticity and exact currency/price-unit binding remain unconfirmed. No
endpoint-specific contradiction establishes D; generic raw-price documentation
does not establish A/B. Quantity/segment uncertainty and absent native revision
IDs are not universal price blockers. The price-source gate remains closed, so
the runbook gate is unchanged. No production adapter or source admission is made.

Initial repository: `main`, `a19205548360e09d4630e4921d2251a31f9e761d`, clean,
14 ahead / 0 behind locally recorded `origin/main`; no fetch. Earlier architecture,
Slice 5A.2e, source ledger and frozen contracts remain unchanged. This milestone
does not revisit IDX control evidence, KSEI actions or paid alternatives.

### First-party sources actually inspected

Directly opened on this review date; search snippets were discovery only. These
were public documentation/page reads, not authenticated market-data requests.

| ID | Source | Evidence scope |
|---|---|---|
| P1 | [EOD endpoint documentation](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes) | Field dictionary, daily date, raw/adjusted distinction, bounded GET and examples. |
| P2 | [Official OpenAPI reference](https://eodhistoricaldata.github.io/EODHD-openapi/redoc.html) | `/eod/{ticker}` parameters and seven-field response specimen; no documented per-row authenticity/correction metadata. |
| P3 | [Pricing](https://eodhd.com/pricing) | $0 Free package, global EOD/indices, one-year history. Actual account entitlement not checked. |
| P4 | [API limits](https://eodhd.com/financial-apis/api-limits) | Daily/minute limits, endpoint costs, reset and usage counters. |
| P5 | [Terms](https://eodhd.com/financial-apis/terms-conditions) | Nonprofessional private storage/analysis grant, redistribution restrictions, termination and disclaimers. |
| P6 | [Data sources](https://eodhd.com/financial-apis/our-data-sources-and-data-partners) | Named exchange agreements and other CFD/market-maker EOD feeds; no JK-specific provenance assignment. |
| P7 | [Exchange/ticker dictionary](https://eodhd.com/financial-apis/exchanges-api-list-of-tickers-and-trading-hours) | Listing currency, code/ISIN fields and documented symbol-bounded lookup; not raw-field unit continuity. |
| P8 | [BBCA.JK page](https://eodhd.com/financial-summary/BBCA.JK), [BBNI.JK page](https://eodhd.com/financial-summary/BBNI.JK) | Vendor recognizes these symbols. BBCA displays `Rp`; neither inspected page establishes the required historical raw-field unit binding. |
| P9 | [Personal/commercial licence FAQ](https://eodhd.com/financial-apis/commercial-vs-personal-license-use) | Personal-plan scope and professional-use distinction; no form submitted. |

### Required semantic matrix

Verdicts describe the exact proposition stated, within inspected documentation;
NOT_DOCUMENTED is not a claim that no unpublished clarification could exist.

| Proposition | Verdict | Finding / admission consequence |
|---|---|---|
| `open`, `high`, `low`, `close` are unadjusted | PROVEN | P1 defines as-traded OHLC without adjustments. Use raw `close` for the frozen technical path. |
| `adjusted_close` includes splits and dividends | PROVEN | P1; separate from raw close. Other action-specific adjustment rules: NOT_DOCUMENTED. |
| Daily `date` means trading date | PROVEN | P1 dictionary uses YYYY-MM-DD trading date. JK timezone/exception mapping: STILL_UNCONFIRMED. |
| Every JK row is a genuine, non-synthetic observation | STILL_UNCONFIRMED | P1/P2 lack a JK guarantee or usable row flags for carry-forward, repeated prior-close substitution, placeholders or estimates. Matching OHLC/positive volume cannot prove this. |
| JK weekends/holidays are always omitted | STILL_UNCONFIRMED | Generic trading-day wording is not an explicit JK exception contract. No independent calendar/completion authority assigned to EODHD. |
| JK no-trade/inactive-session omission or row creation | NOT_DOCUMENTED | Cannot tell missing observation from provider padding or establish no-execution meaning. |
| JK `volume = 0` means genuine no execution | NOT_DOCUMENTED | Do not map zero to unknown, genuine or synthetic by assumption. Ambiguous zero rows fail admission; this does not universally reject nonzero prices. |
| JK volume unit is shares/lots/contracts | NOT_DOCUMENTED | No JK unit dictionary found. Generic volume is insufficient. |
| Volume is unadjusted transaction quantity | CONTRADICTED | P1 expressly describes split-adjusted volume. Never label it `RAW_AS_TRADED` or silently undo adjustments. |
| JK market segment is regular/all/consolidated | NOT_DOCUMENTED | Retain `UNKNOWN`; no invented segment or universal stock-only readiness gate. |
| Exact JK raw OHLC is dated IDR per share, unconverted | STILL_UNCONFIRMED | P7 listing-currency metadata and P8 currency display are narrower facts; P1/P2 do not bind this endpoint's unit/scale or historical continuity. |
| Raw historical OHLC correction behavior | NOT_DOCUMENTED | No immutable-history promise or JK correction protocol found; conservatively allow later changed observations. |
| Adjusted history can change | PROVEN | P1 describes recomputation after dividends; never use adjusted close as stable raw identity. |
| Native EOD row revision/update/correction ID | NOT_DOCUMENTED | P2 example has date/OHLC/adjusted close/volume only. No documented ETag/Last-Modified revision meaning. No live headers inspected. |
| All EOD feeds are direct exchange feeds | CONTRADICTED | P6 distinguishes contracted feeds from other CFD/market-maker feeds. This does not identify a particular JK row as synthetic or establish its origin. |

The [frozen observation rule](SCREENER_EVIDENCE_V0_2_CONTRACT.md#6-genuine-price-observation)
requires positive non-synthetic/non-placeholder/no-substitution proof and an
independent completed session. Repeated numbers, matching official prices or one
successful request cannot replace a source convention. No local filling, weekend
rows, estimates, adjusted-price substitution or volume-based authenticity rule.

### Retention and free automated acquisition

**ALLOWED_JUSTIFIED** for a qualifying nonprofessional user's private, noncommercial
storage/manipulation/analysis under P5, supported by P9's personal-plan scope.
This covers retained responses and personal derived analysis within that scope;
redistribution/account sharing/public display are restricted. No Free-specific
storage exception was found. Post-termination retained-use rights remain
**REMAINS_UNKNOWN**: no perpetual licence or deletion obligation is invented.
Actual account qualification/entitlement and source/permission registry admission
remain separate, unchanged gates. No account/credentials were created or inspected.

Candidate request shape, not executed: `GET https://eodhd.com/api/eod/BBCA.JK`
with local `api_token`, explicit `fmt=json`, `period=d`, `order=a`, bounded ISO
`from`/`to`. Preserve exact original bytes plus sanitized request parameters,
source identity, receipt/admission clocks, hash and length; never retain the token
in provenance. The documented API supports unattended requests; no manual
operational download/import is proposed. This establishes transport feasibility,
not genuine-price admission or actual entitlement.

P3/P4: Free account/key, 20 units/day, EOD 1/request, usage `/api/user` 0 units,
bulk 100 (excluded). Reset midnight GMT = 07:00 WIB; counter resets lazily.
Default minute ceiling is 1,000 requests, with actual response limits/Retry-After
controlling. Preserve local ceiling **16**, reservations before attempts, no
automatic retries or purchased/bonus-unit reliance. Terms' generic 100,000 figure
does not override the explicit Free allowance. No API counter was queried.

| Bounded acquisition scenario | Units / feasibility |
|---|---|
| Ten equities + `JKSE.INDX`, one request each | 11; fits 16 local / 20 Free, subject to real entitlement/availability. |
| Same day plus one explicit failed-symbol catch-up | 12; leaves at most 4 local units for correction reconciliation, less other account use. |
| Entire eleven-symbol panel retried the same quota day | 22; exceeds both ceilings. Defer bounded outstanding symbols across quota days; never buy units. |
| Initial bounded history for all eleven symbols | 11 if one window/symbol; replace that day's narrow request with the history window containing it, or schedule separately. A second full bootstrap plus daily panel costs 22 and does not fit. |
| Routine correction overlap | Widen each daily window within bounds for the same 11-unit cost, or rotate separate checks within remaining units. No guarantee of detecting all transient/provider corrections. |

P3's one-year entitlement can accommodate the existing conservative **330-calendar-
day** collector ceiling. One range/symbol can retrieve candidate history for
50+ sessions without one call per date. This is **candidate price-bootstrap
feasibility**, not proof of 50 consecutive comparable completed sessions, actual
BBCA row availability or post-break warmup. Independent session, currency/identity
and complete action-continuity premises remain required. Preserve original
acquisition bounds; no unbounded full-history/bulk call or new collector invoked.

### Retained revisions and feature boundaries

Existing `RawArtifactArchiver` hashes original bytes and records length; the pilot
retains fetch observations and appends local bar revisions on changed canonical
content. Source/reference + receipt chronology + content hash can preserve
**locally observed versions** without a vendor revision number. Keep source,
instrument, session/date, endpoint/parameters and exact bar/artifact bindings;
local revision ordinals are never provider correction IDs/publication times.
Changed range bytes alone do not prove every contained row changed. Retain old
bytes, append newly authenticated observations at honest knownAt, never overwrite
or backdate, and quarantine unresolved lineage rather than manufacture it.
Unchanged-byte refetches do not create extended semantic scope. Missed intermediate
versions cannot be reconstructed; raw corrections remain conservatively possible.
This reuses the model conceptually, not a completed V0.2 adapter/mapping.

Inspected `ScreenerEvidenceV02Readiness` and `ScreenerEvidenceV02Technical` agree
with frozen sections 11–12: EMA20, EMA50, ATR14, priorHigh20, priorLow20 and
distanceToHighPercent use price/session/comparability premises, not authenticated
volume units. ATR percent, close change percent and price trend likewise need no
quantity. EMA50 needs 50 cleared bars; ATR14 15 bars; prior extrema exclude the
current bar and need 20 previous valid sessions. RS20/RS60 need independently
admitted aligned benchmark prices, not volume, and remain optional.

Relative volume, monetary liquidity proxy and liquidity-dependent features remain
feature-level UNAVAILABLE without their required comparable quantity/basis/segment
proof. Current readiness requires supported share units and `RAW_AS_TRADED`
quantity basis; P1's split-adjusted volume is not that basis. Actual traded value
needs its own evidence. No mandatory volume/liquidity gate is added. A zero row
with ambiguous genuineness still fails the separate frozen price-admission rule.

EODHD remains **T3 observation authority only**. It cannot supply TradingStatus,
listing, board/mechanism, exchange completion or corporate-action completeness.
No `GenuinePriceObservation` or `SourcePriceConvention` is admitted from this review.
In particular, do not set synthetic/placeholder/substituted markers to NO or
syntheticIdentification to DOCUMENTED_NON_SYNTHETIC from generic raw-OHLC wording.
No source ID, convention version, exact evidence references or future mapping is
frozen as operationally usable while mandatory facts remain unproved.

### Exactly two mandatory unresolved premises and next actions

1. **JK genuine-observation/date semantics.** Obtain an endpoint-specific retained
   EODHD specification/answer, without sending it in this task: “For Free
   `/api/eod/BBCA.JK` and the other pilot JK equities with `period=d`, what source
   produces OHLC and what local trading date does `date` identify? Can rows be
   carried forward, previous-close substitutions, synthetic, placeholders or
   estimated, including weekends, holidays and no-execution/inactive sessions?
   State each condition and the fields/conventions that positively distinguish
   genuine observations; what exactly does zero volume mean?” This resolves the
   mandatory authenticity family, not optional quantity units by guesswork.
2. **Exact raw currency/unit continuity.** Obtain one retained field/metadata
   specification/answer: “For the same JK EOD endpoint, are raw OHLC unconverted
   IDR per ordinary share? Which dated symbol/ISIN/exchange metadata and convention
   bind that currency/unit/scale to the fields, including historical code, currency
   or unit changes?” Listing currency or price magnitude alone is insufficient.

No further general vendor survey is recommended. Native revision IDs, raw correction
protocol, optional volume/segment and post-termination rights remain explicit
limitations, not extra universal blockers for qualifying active-account price use.
**5A.3c — Automated EODHD JK Price Evidence Adapter remains conditional**, pending
these two answers and ordinary scoped admission. Daily Runner remains blocked by
the separate unchanged source gates. No support/licensing message was sent.

### Validation and activity

Documentation only; append to this file alone. No build/full test suite or live
probe needed: a price sample cannot settle undocumented semantics. Fresh public
first-party reads/searches were nonzero; authenticated/demo market-data API calls
**0**, provider units **0**, paid use **0**. No credential search/exposure, account
creation, paid activation, provider recovery, operational collector, DB connection/
query/mutation, source/permission registry change or production evidence import.
No IDX/KSEI research in this milestone. FullIdx/soak configuration unchanged;
historical operational counts were not freshly measured. Checks passed: exact
documentation diff/`git diff --check`, unchanged historical prefix, frozen binding,
source ledger and runbook, and matching **1,386 protected file hashes** before/after.
Only this documentation file changes; no push.
