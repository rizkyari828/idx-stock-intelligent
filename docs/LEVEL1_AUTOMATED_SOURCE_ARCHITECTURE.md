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

---

## Slice 5A.2g — corporate-action source confirmation (2026-10-08 WIB)

**Decision: `CORPORATE_ACTION_COVERAGE_PARTIAL`.** No admitted free, permitted,
unattended, reproducible Indonesian corporate-action route establishes complete
coverage for the five required categories. Positive individual events are
obtainable from several official/reviewed sources, but no source proves a closed
all-type, all-window, amendment/cancellation coverage basis, and no source grants
permitted unattended automation. Independent permission/automation verdict on the
operational axis: **`CORPORATE_ACTION_AUTOMATION_OR_PERMISSION_BLOCKED`** (IDX
website automation prohibited and system-to-system paid per Slice 5A.2e; KSEI
public pages have no login/payment but no granted automation permission; IDX
`/primary` routes are discovery-only and must not be scraped). Both must be
resolved before `priceComparability` can ever reach `CLEARED`. This section is
additive and appends to this file alone; it does not amend the frozen contract,
the source ledger, the external source review, or prior findings.

Baseline: `main`, HEAD `6d04eb1a731df4483dcefba85be100c12c15ec12`, clean worktree,
16 ahead / 0 behind locally recorded `origin/main`; no fetch. Slice 5A.2f remains
the EODHD price decision; this milestone does not finish EODHD semantics.

### Bounded verification of current source state

Ordinary attributed public GETs on this review date (no login, no WAF/CAPTCHA
bypass, no internal `/primary` call):

| Source | Response | Bearing |
|---|---|---|
| [IDX corporate-action page](https://www.idx.id/id/perusahaan-tercatat/aksi-korporasi/) | 200 HTML, 406,277 B | Lists Stock Split, HMETD, Tanpa HMETD, **ESOP/MSOP**, Saham Bonus, IPO, Partial Delisting, Waran, Dividen Saham, Gabung Usaha, Reverse Stock, Kurang Modal, Konversi Saham. ESOP/MSOP is absent from the `idx-bei` 15-item `CA_TYPES`, confirming that wrapper enumeration is not an exhaustive authoritative universe. |
| [KSEI rights/HMETD index](https://web.ksei.co.id/publications/corporate-action-schedules/rights-distribution) | 200 HTML, 33,843 B | Public category index; individual dated notices are separate PDFs. |
| [KSEI merger/split/reverse index](https://web.ksei.co.id/publications/corporate-action-schedules/masr) | 200 HTML, 33,906 B | Public category index; earlier finding of a final schedule referencing an earlier announcement, separating conversion/suspension/delisting dates. |
| [KSEI bonus index](https://web.ksei.co.id/publications/corporate-action-schedules/share-bonus?setLocale=en-US) | 200 HTML, 33,827 B | Earlier direct fetch returned 500; index reachable now, no completeness statement. |
| [KSEI current announcements](https://www.ksei.co.id/en/publication/announcement) and [action calendar](https://www.ksei.co.id/en/service-support/schedule/schedule-of-corporate-actions) | 200 | Public dated references/categories; pagination only, no exhaustive historical/cancellation export or feed. |
| [KSEI downloads](https://web.ksei.co.id/data/download-data-and-user-guide?setLocale=en-US) | 200 HTML, 87,922 B | Annual combined/category XLS downloads; combined 2026 export exceeds the 4 MiB/512-row ingestion bound (prior finding). |
| [IDX disclosures](https://www.idx.id/id/perusahaan-tercatat/keterbukaan-informasi/) and [XBRL](https://www.idx.id/id/perusahaan-tercatat/xbrl/) | 200 | Product/taxonomy relevance; no complete public revision/feed or automation licence. |

`GetIssuedHistory`, `GetAllAnnouncement` and other IDX `/primary` routes were
assessed in the [external source review](EXTERNAL_SOURCE_INTELLIGENCE_REVIEW.md)
from pinned repository code only; they were not called here and are not admitted.

### Corporate-action coverage matrix

`CONFIRMED_FIELD` = directly observed in an inspected official/reviewed artifact
for at least one event. `PARTIAL` = present in some notices but not systematically
across the type/window. `UNRESOLVED` = no authenticated universal source. No row is
a completeness assertion.

| Action type | Positive-event source(s) | Confirmed fields | Partial / unresolved fields | Automation | Permission | Historical / PIT / revision limitation |
|---|---|---|---|---|---|---|
| Stock split | IDX action page; KSEI merger/split/reverse index + final schedule PDFs; issuer originals (e.g. BCA 2021 split PDF) | Instrument identity; announced ratio and nominal split; some market-effective dates; KSEI publication/reference | Universal ex/effective semantics; all amendment/cancellation records; complete window | Index/PDF manual; no permitted feed | IDX website prohibited / system-to-system paid; KSEI automation permission unknown | Mutable pages; no immutable event chain; publication timezone not authenticated |
| Reverse stock split | IDX action page; KSEI merger/split/reverse category | Instrument identity; category presence; some ratio/schedule via PDFs | Same as split; no exhaustive catalogue | Same | Same | Same |
| Cash dividend | KSEI AUTO dividend notice PDF (reference + separate ex/record/payment dates); IDX action page (Dividen Saham) | Instrument identity; reference/publication date; ex/record/payment dates for the observed event | Universal coverage; amendment/cancellation; consistency of date semantics across issuers | Manual PDF; no feed | KSEI unknown; IDX blocked/paid | Frozen policy keeps cash dividends price-only; still needs no-break coverage for absence claims |
| Rights issue / HMETD | KSEI rights/HMETD index + dated rights notice PDFs (security identity, segment-specific schedule); IDX action page (HMETD/Tanpa HMETD) | Instrument identity; exercise terms/ratio in notices; some cum/record/effective/payment dates | Universal ex-date/theoretical-price semantics; all amendments; complete window | Manual PDF; no feed | Same | Announcement time, cum/ex/record/effective distinctions not universally authenticated |
| Bonus shares / stock dividend | IDX action page (Saham Bonus); KSEI bonus category; prior bonus guide | Instrument identity; category/ratio clues | Bonus direct fetch returned 500 earlier; no complete coverage | Manual; C-BEST reports need participant access | KSEI unknown; IDX blocked/paid | No native revision lineage |

Field-level summary: **instrument identity** is the strongest confirmed field
(KSEI master/detail, IDX disclosures). **announcement/publication date** is visible
on KSEI notices but lacks an authenticated timezone/first-public definition.
**cum/ex/record/effective/distribution** dates appear in some notices but are not
proven universal or consistently defined across types and issuers. **ratios /
amounts / exercise prices** appear in notices but are not systematically
retained/authenticated. **amendments, cancellations and corrections** are the
weakest: no source exposes a complete, immutable revision chain. `GetIssuedHistory`
supplies `JumlahSaham`/`JumlahSahamSetelahTindakan` and `TanggalPencatatan`, but
`TanggalPencatatan` is a listing-date clue, not a universal ex/effective date, and
failed categories are recorded as empty (silence, not absence).

### Automation, permission and source roles

| Source | Role assigned | Basis |
|---|---|---|
| KSEI corporate-action publications (indices + notice PDFs) | **Reconciliation / manual research only** (positive events `CONDITIONALLY_ADMISSIBLE`) | Public no-login PDFs; automation permission not granted; no publisher-complete coverage or cancellation feed; XLS combined export exceeds ingestion bounds |
| IDX official disclosures / aksi-korporasi / `GetIssuedHistory` / `GetAllAnnouncement` | **Discovery only** | Endpoint/category intelligence; unattended website automation prohibited; system-to-system route is a paid licence; wrapper enumeration not exhaustive and failures-to-empty |
| Issuer originals (e.g. BCA chronology/split PDF) | **Reconciliation** | Positive corroboration where a reviewed issuer route is allowed; not a complete coverage basis |
| EODHD splits/dividends endpoint | **Discovery only / corroboration** | Documented T3 API but cannot certify rights/bonus/merger/no-break completeness |
| Any complete permitted unattended corporate-action feed | **Blocked / not admissible (none found)** | No source grants the required rights and coverage |

Technical accessibility, public visibility and operational permission are
**separate claims**: reachable official PDFs and internal `/primary` routes do not
constitute an automation grant.

### Historical and PIT findings

- **Prospective EOD operation:** event acquisition can be planned (shared indices,
  dated notices), but no permitted unattended feed exists; prospective coverage is
  therefore also blocked, not merely historical.
- **Historical reconstruction:** individual dated events are reconstructable from
  retained originals; a closed all-window coverage basis is not proven.
- **Publication vs first-seen:** KSEI publication/reference dates exist; timezone,
  first-public meaning and original linkage are unproven. Local knownAt stays
  separate and cannot be backdated.
- **Effective-date handling:** cum/ex/record/effective/distribution distinctions are
  not universally authenticated; `TanggalPencatatan` is not an ex-date.
- **Revision/correction history:** no immutable correction/cancellation chain;
  mutable pages and same-URL replacement mean a later fetch cannot prove the prior
  version; missing revisions cannot be reconstructed.
- **Immutable evidence / deterministic replay:** retain original bytes and hashes,
  but hashes prove identity only. A missing event, an unsupported category or a
  failure-to-empty leaves the window `PARTIAL`/`UNRESOLVED`; never `CLEARED`.

### Price-adjustment and indicator consequences

Frozen semantics are unchanged; this identifies required treatment, not new rules.

| Action | Required explicit treatment | Downstream risk if untreated |
|---|---|---|
| Stock split, reverse split, bonus shares / stock dividend | Capital discontinuity: mark the affected segment `PRICE_KNOWN_BREAK`; restart/warmup per the frozen split-in-seed rule | Wrong returns, EMA20/50, momentum, ATR, priorHigh/Low and distance-to-high across the break |
| Cash dividend | Retain frozen price-only treatment; do not invent an adjustment | Adjusted-close use would alter raw technicals; cash dividends alone do not create a capital-break claim |
| Rights issue / HMETD | Must be evaluated per event: **split-like adjustment is not universally valid**; requires authenticated cum/ex/effective terms and a defensible adjustment basis, else `PRICE_KNOWN_BREAK`/`UNRESOLVED` | Theoretical ex-rights price distortion of returns/indicators; over- or under-adjustment |
| Volume basis | EODHD documents split-adjusted volume, which is **not** the required raw `RAW_AS_TRADED` quantity basis | Volume/liquidity features remain `UNAVAILABLE`; never convert adjusted volume to raw shares |

Because coverage is partial, `priceComparability` cannot be set `CLEARED` for any
window containing an unproven action; affected dependencies are `UNRESOLVED` and
`DATA_READY` is not reached (`CORPORATE_ACTION_COVERAGE_PARTIAL`).

### Level-1 blocker impact

Corporate-action completeness remains a **distinct mandatory Level-1 dependency**.
It is not subsumed by TradingStatus or session completion, and those must **not** be
declared the sole remaining blockers. The corporate-action gap independently blocks
`priceComparability = CLEARED` and therefore `DATA_READY` for any segment crossing
an unproven capital event. No complete Level-1 blocker-matrix re-run is performed
here; that follows the EODHD-semantics finish.

### Remaining EODHD questions carried forward

Unchanged from Slice 5A.2f; this milestone does not resolve them:

1. **JK genuine-observation/date semantics** for `/api/eod/BBCA.JK` and pilot JK
   equities (`period=d`): source of OHLC; what local trading date `date` identifies;
   carry-forward / previous-close substitution / synthetic / placeholder /
   estimate behavior including weekends, holidays and no-execution sessions; what
   zero volume means.
2. **Exact raw currency/unit continuity:** whether raw OHLC are unconverted IDR per
   ordinary share, and which dated symbol/ISIN/exchange metadata bind that
   currency/unit/scale to the fields across historical code/currency/unit changes.

EODHD splits/dividends remains corroboration only; it cannot certify complete
corporate-action coverage.

### Validation and activity

Documentation only; append to this file alone. No build/full test suite required.
Fresh public first-party GETs were bounded to official/reviewed pages; **no** IDX
`/primary` endpoint, authenticated Stockbit endpoint, KSEI participant service or
EODHD market-data call was made. Provider units **0**, paid use **0**, accounts
**0**, credentials **0**, login **0**, WAF/CAPTCHA bypass **0**. No code, adapter,
migration, source/permission registry, operational DB, evidence, Daily Runner,
FullIdx or soak change. `git diff --check` clean; only this documentation file
changes; commit message `docs: confirm corporate action evidence route`; no push.

---

## Source selection architecture delta review (2026-10-08 WIB)

**Decisions.** Prior-Astra research completeness: **COMPLETE_WITH_OPEN_EXTERNAL_BLOCKERS**.
Provider abstraction: **EXISTING_SUFFICIENT**. Source selection:
**POLICY_CLARIFICATION_NEEDED** (clarified below; no code change). EODHD:
**CONDITIONAL**. This review authorizes no production code, migration, registry
activation, adapter, provider call or paid action.

Baseline: `main`, HEAD `685e1af285d9d4eec139b1163e8c41be3449a522`, clean worktree,
17 ahead / 0 behind locally recorded `origin/main`; no fetch. This section appends
to this file alone and does not amend frozen contracts or prior decisions.

### Prior-Astra completeness check

[External source intelligence review](EXTERNAL_SOURCE_INTELLIGENCE_REVIEW.md) is
present, complete (12 sections, no truncation/placeholders), internally consistent,
and its recommendations/unresolved items are classified. It covered the eight
repositories, IDX endpoint intelligence, permission, KSEI, EODHD, Stockbit,
corporate actions, TradingStatus/session, official XBRL/XLSX, free-first
architecture, automation/manual, provenance/PIT, cost/scale and next
investigations. Execution closure is verified from commit `6d04eb1`, which changed
only that documentation file (874 insertions) with a clean subsequent worktree and
no production/DB change. The original task prompt and separate task report are not
committed repository artifacts, so exact prompt text and the reported anonymous
HTTP counts cannot be independently re-verified; no missing execution evidence is
invented. Slice 5A.2g later refined corporate-action coverage (`PARTIAL`,
permission/automation blocked); that refinement is preserved. **No material
documentation gap.**

### Provider-abstraction capability matrix

Evidence is from inspected production code, not documentation intention.

| Capability | Classification | Repository evidence |
|---|---|---|
| Multiple source registration | ALREADY_IMPLEMENTED | `source` table with `terms_status ∈ {UNKNOWN,ALLOWED,DISALLOWED}` (`src/IdxStockIntelligence.Infrastructure/Migrations/0001_phase0_foundation.sql`); `ingestion_run`/`raw_artifact` carry `source_id`. Production registers only `eodhd` (`UNKNOWN`) in `PilotDatabase`. |
| Source-specific acquisition | CONTRACTED_BUT_NOT_IMPLEMENTED | `BoundedEvidenceSource` adapter record (`src/IdxStockIntelligence.Application/BoundedEvidenceIngestion.cs`); `BoundedEvidenceIngestionService` is constructed only in tests; service comment: "No production adapter is installed by default." |
| Provider-specific normalization | CONTRACTED_BUT_NOT_IMPLEMENTED | Boundary accepts provider-neutral typed rows via `Normalize`; Python parses provider formats (`collectors/python/src/idx_stock_collector/contract.py`, `pilot.py`, `eodhd_experimental.py`); no production adapter registered. |
| Immutable original evidence | ALREADY_IMPLEMENTED | `RawArtifactArchiver` content-addressed SHA-256 with atomic move; `raw_artifact` retains bytes/hash/length/URI/parser version. |
| Source provenance | ALREADY_IMPLEMENTED | `raw_artifact` (`original_uri`, `fetched_at`, `parser_version`) + evidence `source_id`, `source_reference`, `raw_artifact_id`; `ingestion_run`. |
| Source revision handling | ALREADY_IMPLEMENTED | `RevisionSeriesId == ScreenerEvidenceRevisionSeries.Canonical(SourceId, SourceReference)`; append-only supersession; DB update/delete triggers in `0007_screener_evidence_v02.sql`. |
| Typed evidence binding | ALREADY_IMPLEMENTED | `ScreenerEvidenceBinding.Decode`/payload codecs; `ScreenerEvidenceV02Store` re-verifies payload SHA-256. |
| Data-family-specific source admission | ALREADY_IMPLEMENTED | `ScreenerSourceAdmission.Admit` per claim + `terms_status = 'ALLOWED'` check in `BoundedEvidenceIngestionService`. |
| Explicit provider selection | ALREADY_IMPLEMENTED (resolution) | `ScreenerEvidenceResolver.Resolve` selects by authority tier, then scope specificity; chronology only a tie-break. No declarative primary/reconciliation role label exists; see policy below. |
| Provider replacement | CONTRACTED_BUT_NOT_IMPLEMENTED | Requires a reviewed adapter tuple + source row + mappings + admission; no automatic switch exists. |
| Source reconciliation | ALREADY_IMPLEMENTED | Multiple candidates resolved; disagreeing survivors yield `CONFLICTING` rather than a convenient winner. |
| Incompatible-source detection | ALREADY_IMPLEMENTED | Source-namespaced `SourcePriceConvention` identity (`[PriceSourceId, Endpoint, Field, Version]`) and the comparability evaluator prevent silent basis mixing. |
| Source failure handling | ALREADY_IMPLEMENTED | Readiness fails closed (`DATA_BLOCKED`/`UNKNOWN`), no invented value, no silent provider fallback. |
| Data-quality / readiness enforcement | ALREADY_IMPLEMENTED | `ScreenerEvidenceReadiness.Compose` + evaluators; `ScreenerEvidenceReadinessService` read-only snapshot. |
| Historical reproducibility | ALREADY_IMPLEMENTED | `ScreenerEvidenceV02AsOfReader` stored-only, `RepeatableRead`, cutoff/knownAt visibility; 512-record bound (`MaximumRecords` in `ScreenerEvidenceV02AsOf`, enforced by `LIMIT 513` detection in the reader). |

Deferred and not required: automatic failover/switch engine, provider-selection UI,
generic plugin framework, new orchestration or schema.

### Data-family source-selection policy

Admission states reuse existing tokens; `NONE/BLOCKED` stays valid. One admitted
source may serve several claims only where the contract permits. This is the
minimum policy; it introduces no new class, table or service.

| Family | Candidate source | Admission state | Role | Independent blocker |
|---|---|---|---|---|
| Security identity | KSEI master/detail + IDX acts | CONDITIONALLY_ADMISSIBLE | Primary candidate | Exact local-ID/ISIN/code interval; permissions |
| Security type | KSEI detail + issuer evidence | CONDITIONALLY_ADMISSIBLE | Primary candidate | Ordinary-equity mapping and interval |
| Listing/delisting history | IDX acts + KSEI/issuer originals | NEEDS_CONFIRMATION | Primary candidate + reconciliation | Operative listing act; complete PIT coverage |
| TradingStatus | IDX exact-session status | NOT_OPERATIONALLY_ACCEPTABLE (5A.2e) | None admitted | Permission + completeness |
| Board/mechanism | IDX dated roster + rules/exceptions | NEEDS_CONFIRMATION | Primary candidate | Dated membership, rule version, exceptions |
| Trading calendar | IDX annual schedule + amendments | Not admitted automated | Primary candidate (manual) | Unattended permission; amendment coverage |
| Exceptional closures | Governing IDX notices/checkpoint | Not admitted automated | Primary candidate | No complete free source (5A.2g) |
| Session completion | Independent IDX final artifact | SOURCE_NOT_AVAILABLE | None admitted | No authenticated clock/artifact |
| EOD OHLC | EODHD Free API (T3) | CONDITIONAL | Conditional primary (pilot only) | Two JK semantics premises (5A.2f) |
| Raw traded volume | None (EODHD volume is split-adjusted) | NOT_ADMISSIBLE for raw basis | None | Feature-local; volume features UNAVAILABLE |
| Corporate actions | KSEI/IDX disclosures/issuer originals | CORPORATE_ACTION_COVERAGE_PARTIAL + AUTOMATION_OR_PERMISSION_BLOCKED | Reconciliation / discovery | Coverage + permission (5A.2g) |
| Benchmark/index | EODHD `JKSE.INDX` (T3), later permitted IDX index | CONDITIONAL / optional | Optional | Independent identity/convention admission |
| Fundamentals (Phase 2) | Official issuer/IDX filings → XBRL/XLSX | DEFERRED | Future primary concept | Permitted unattended acquisition |
| Optional market/flow | IDX foreign/nonregular aggregates | DISCOVERY_ONLY | Optional context | No admitted units/scope/route |

### Provider-switching safety

A switch is a **new admitted source**, never an in-place continuation. Existing
contracts already require the following; this review clarifies them, and adds no
new gate:

- **Price basis:** raw/as-traded vs adjusted vs split-adjusted may not be spliced;
  a new provider needs its own `SourcePriceConvention` identity and admission.
- **Currency/unit/scale:** IDR per-share and unit continuity must be re-bound to the
  new source; earlier proof does not transfer.
- **Identity:** instrument/ISIN/ticker and effective-date continuity must be
  re-mapped; no suffix or name guessing.
- **Observation semantics:** genuine/non-synthetic/no-substitution, trading-date
  meaning, session applicability, finality and revision chronology are source-specific.
- **Provenance:** original source, artifact, retrieval/knownAt, revision and
  normalization version are retained; replay uses retained inputs, never live fallback.
- **Indicator continuity:** a provider change inside an indicator window is a
  potential `PRICE_KNOWN_BREAK`; rebuild/restart the affected segment rather than
  silently continue EMA20/50, ATR14, momentum, priorHigh/Low or relative strength.
- **Failure handling:** on failure, keep evidence and leave the claim unavailable;
  never invent an observation, silently substitute another source, or bypass readiness.

### EODHD continuation decision

**CONDITIONAL.** Repository evidence: EODHD is the only registered source
(`terms_status = UNKNOWN`); Slice 5A.2f keeps the free bounded EOD interface a
conditional T3 candidate and finds `EODHD_JK_PRICE_NOT_YET_ADMISSIBLE`; the
documented free plan is 20 units/day with local ceiling 16 and private
noncommercial storage allowed. Rationale: it remains the most plausible free
automated price route and fits the existing Python collector and evidence
boundaries, but its two mandatory JK premises are unproven, so it cannot be
prioritized or admitted; it should not be rejected because no better free
alternative is demonstrated. Remaining verification: (1) genuine-observation/date
semantics; (2) exact raw IDR per-share currency/unit continuity. Cost: zero within
the 16/20 units for the 11-symbol panel; no paid activation. EODHD remains an
optional price source for the bounded pilot, not an application-wide dependency.

### Zero-cost feasibility

| Scenario | Feasibility | Limiting factors |
|---|---|---|
| A — zero-cost pilot universe | Transport feasible; Level-1 readiness still blocked | EODHD free 20/day (11-unit panel); control/calendar/completion/action gates open |
| B — zero-cost full/broader IDX | Not feasible | EODHD bulk 100 units exceeds free allowance; whole-exchange is not a free entitlement; other free routes permission/coverage-blocked |
| C — optional future paid | Architecturally accommodatable; not justified now | No paid capability demonstrated to close exact mandatory gaps; no purchase authorized |

Pilot transport feasibility is not full-IDX feasibility and is not Level-1 readiness.

### Level-1 blocker preservation

Unchanged independent mandatory blockers: IDX automated control-evidence permission
(5A.2e); TradingStatus; board/mechanism; calendar/exceptional closures; session
completion; corporate-action completeness and automation permission (5A.2g); EODHD
JK semantics; price comparability. Optional/feature-local evidence: raw volume
(volume/liquidity features `UNAVAILABLE`), benchmark (RS `UNAVAILABLE`), optional
market/flow context. TradingStatus/session control is **not** the sole remaining
blocker, and corporate actions remain independently mandatory where price
comparability depends on complete action evidence. No new readiness state and no
full blocker-matrix re-run are introduced here.

### Required versus deferred changes

- **Required now:** none in code. This documentation clarification only.
- **Required before any future adapter activation:** a reviewed
  `BoundedEvidenceSource` adapter, an `ALLOWED` source row with evidence, and
  claim/tier mappings; then normal admission.
- **Optional/deferred:** declarative provider-role configuration, additional
  reconciliation sources, any failover engine (not desired).

### Validation and activity

Documentation only; append to this file alone. No production code, adapter,
migration, source/permission registry, operational DB, evidence, Daily Runner,
FullIdx or soak change. No provider/network call was made in this milestone
(provider units **0**, paid use **0**, credentials **0**); the repository was
inspected read-only. `git diff --check` clean; commit message
`docs: clarify level1 source selection policy`; no push.

---

## EODHD semantics final determination (2026-10-08 WIB)

**Decision: EODHD_JK_PRICE_NOT_YET_ADMISSIBLE is confirmed and final for the
current evidence.** Both mandatory JK semantic families remain **UNRESOLVED**:
they require an endpoint-specific provider confirmation that public EODHD
documentation does not supply. EODHD stays **CONDITIONAL** (bounded T3 pilot price
candidate), is **not ready for adapter implementation**, and no paid upgrade is
justified because payment does not resolve undocumented semantics. This closes the
bounded EODHD investigation for the currently identified questions; no further
broad search will be started without a new evidence path.

Baseline: `main`, HEAD `056065a9c2f712fd1f44abe6cb62c9763f70d5db`, clean worktree,
18 ahead / 0 behind locally recorded `origin/main`; no fetch. This section appends
to this file alone and supersedes no frozen contract. It continues Slice 5A.2f and
reuses [EODHD semantics and rights](EODHD_SEMANTICS_RIGHTS.md) and the
[data-spike empirical findings](DATA_SPIKE_RESULT.md).

### Bounded re-check performed

Only first-party EODHD documentation already used by Slice 5A.2f was re-read:
the [EOD endpoint specification](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes),
[data sources and partners](https://eodhd.com/financial-apis/our-data-sources-and-data-partners)
and [covered tickers](https://eodhd.com/financial-apis/covered-tickers-eodhd)
(each HTTP 200, public, no login). These describe raw as-traded OHLC,
split/dividend-adjusted `adjusted_close`, split-adjusted volume, one row per
trading day and nullable ISIN in the current ticker list. They contain **no**
Jakarta/JK-specific statement on synthetic/placeholder/estimation, carry-forward,
no-execution or zero-volume behavior, **no** JK timezone/date mapping, and **no**
endpoint-field currency/unit binding. No market-data API request, account action,
paid activation or support message was made.

### Final semantics matrix

Evidence types: **PD** provider documentation; **RE** retained empirical
repository evidence; **INF** engineering inference. Statuses reuse existing tokens.

| Item | Claim | Evidence | Type | Status | Operational consequence | Missing evidence |
|---|---|---|---|---|---|---|
| OHLC origin | `open/high/low/close` are raw, unadjusted, as-traded | EOD spec | PD | PROVEN (documentation) | Preserve raw fields for the frozen technical path | none |
| Genuine observation | Every JK row is a genuine non-synthetic observation | EOD spec, OpenAPI | PD | STILL_UNCONFIRMED | Blocks `GenuinePriceObservation` admission | JK non-synthetic/no-substitution statement |
| Trading date | Daily `date` identifies the local trading date | EOD spec | PD | PROVEN (YYYY-MM-DD) but JK timezone mapping STILL_UNCONFIRMED | Session binding uncertain | JK date/timezone mapping statement |
| Weekend behavior | JK weekends omitted | generic one-row-per-trading-day | PD | NOT_DOCUMENTED (JK) | Independent calendar still required | JK exception contract |
| Holiday behavior | JK holidays omitted | generic wording | PD | NOT_DOCUMENTED (JK) | Closure handling relies on independent proof | JK holiday contract |
| Suspension behavior | Suspended sessions omitted or marked | none | — | NOT_DOCUMENTED | Cannot distinguish suspension from absence | JK suspension representation |
| No-execution behavior | No-execution sessions omitted or zero row | none | — | NOT_DOCUMENTED | `NO_TRADE` vs missing row unresolved | JK no-execution convention |
| Zero-volume meaning | JK `volume = 0` means genuine no execution | EOD spec | PD | NOT_DOCUMENTED; ambiguous zero fails admission | Zero rows not admitted | Zero-volume convention/flag |
| Carry-forward | Prior close is not carried forward as a new row | data spike | RE | CONTRADICTED for tested rows (ANTM carry-forward zero-volume rows on Aug 17/25; GOTO padding) | Cannot assume row absence/presence is genuine | JK carry-forward/fill policy |
| Synthetic/placeholder | No synthetic/placeholder/estimated rows | EOD spec, OpenAPI | PD | STILL_UNCONFIRMED | Blocks admission | JK synthetic/placeholder declaration |
| Raw currency | Raw OHLC are unconverted IDR | listing-currency metadata | PD | STILL_UNCONFIRMED (endpoint field binding) | Currency/total-return interpretation at risk | Endpoint-field currency statement |
| Per-share unit | Raw OHLC are IDR per ordinary share | listing metadata | PD | STILL_UNCONFIRMED | Price unit for indicators unbound | Per-share unit/scale binding |
| Price scale | Historical scale continuity | EOD spec | PD | NOT_DOCUMENTED | Cross-period numeric continuity unproven | Scale/continuity statement |
| Historical instrument binding | Rows bind to dated ISIN/identity | ticker list | PD | PARTIAL (nullable ISIN, current list); no historical continuity | Ticker-change continuity unproven | Dated identity/ISIN mapping |
| Historical currency/unit continuity | Continuity across currency/code/unit changes | listing metadata | PD | NOT_DOCUMENTED | Long-window comparability unproven | Dated metadata continuity statement |

### Confirmed versus unresolved

Confirmed (reuse, no new work): documented raw/adjusted field split; Free
20 units/day within a conservative 330-day window; private noncommercial
storage/analysis granted; split-adjusted volume (so raw `RAW_AS_TRADED` volume is
**not** available); `.JK` is a Jakarta catalogue namespace (not a MIC/class); a
JK symbol is recognized and `JKSE.INDX` identity/recent closes were sampled;
existing parser/admission controls reject known closures and treat ambiguous zero
as UNKNOWN (**VERIFIED for the tested rows only**).

Unresolved (**provider confirmation required**): Family A — JK genuineness,
carry-forward/synthetic/placeholder, weekend/holiday/suspension/no-execution/zero
semantics and the JK date mapping. Family B — endpoint-field IDR per-share
currency/unit/scale binding and dated historical identity/currency continuity.

### Exact missing provider confirmations (draft only; no message sent)

1. **Family A — JK observation/date semantics.** For Free `/api/eod/BBCA.JK` and
   the other pilot JK equities with `period=d`: what source produces OHLC, and what
   local trading date does `date` identify? Can rows be carried forward,
   previous-close substitutions, synthetic, placeholders or estimated, including
   weekends, holidays, suspension and no-execution/inactive sessions? State the
   conditions and the fields/conventions that positively distinguish genuine
   observations, and what exactly `volume = 0` means.
2. **Family B — raw currency/unit continuity.** For the same JK EOD endpoint, are
   raw OHLC unconverted IDR per ordinary share? Which dated symbol/ISIN/exchange
   metadata and convention bind that currency, unit and scale to the fields,
   including historical code, currency or unit changes?

These are endpoint-specific statements; generic raw-OHLC documentation cannot
substitute for them, and an inference may not be promoted to a provider fact.

### Price-correctness consequences

`GenuinePriceObservation` and `SourcePriceConvention` remain **unadmitted**; the
two families are the only universal price premises still open. Consequences:
trading-date/session binding is unsafe without the JK date mapping; `priceComparability`
cannot reach `CLEARED`; EMA20/50, ATR14, momentum, priorHigh/Low and relative
strength cannot be computed from admitted observations; historical reproducibility
is bounded to retained bytes plus local revision ordinals (never provider
correction IDs); split/action handling keeps the frozen break semantics. Optional
raw-volume/liquidity and benchmark limitations stay **feature-local** and are
**not** promoted to universal price-readiness blockers. No new price-adjustment
algorithm is introduced, and EODHD corporate-action fields are never treated as
proof of complete action coverage.

### EODHD continuation decision

**CONDITIONAL** (unchanged). Confirmed capability: the only documented free bounded
EOD candidate that fits the existing Python collector and provider-neutral evidence
boundaries. Remaining gaps: the two families above. Operational limits: two
unproven premises, split-adjusted volume, unknown JK upstream/segment, no native
revision IDs. Pilot usefulness: a conditional price lead, not a complete stack.
Free-tier implication: zero monetary cost at 11 units/panel within 16 local / 20
Free. Provider confirmation: **required** (draft inquiry above; not sent). Adapter
implementation: **not ready** until the two premises close and normal scoped
admission passes. Paid upgrade: **not justified** — payment does not resolve
undocumented semantics. Source registry admission is unchanged (only `eodhd`,
`UNKNOWN`).

### Level-1 blocker-matrix handoff

- **A. Resolved EODHD claims:** raw vs adjusted field split; entitlement/private
  storage; split-adjusted volume (raw volume unavailable); symbol namespace and
  basic discovery.
- **B. Unresolved EODHD claims:** JK genuineness/date/no-trade/carry-forward/
  synthetic/zero semantics (Family A); IDR per-share currency/unit/scale and dated
  historical continuity (Family B).
- **C. Claims requiring official confirmation:** the two endpoint-specific
  statements above.
- **D. EODHD-specific admission consequences:** `GenuinePriceObservation` and
  `SourcePriceConvention` unadmitted; `priceComparability` `UNRESOLVED`; volume/
  liquidity features `UNAVAILABLE`.
- **E. Independent blockers outside EODHD (preserved):** IDX automated
  control-evidence permission; TradingStatus; board/mechanism; calendar/exceptional
  closures; session completion; corporate-action completeness and automation
  permission; price-comparability dependencies on admitted actions.

No full Level-1 blocker-matrix re-run is performed here; no `DATA_READY` is
declared, and TradingStatus/session control is **not** the sole remaining blocker.

### Validation and activity

Documentation only; append to this file alone. No production code, adapter,
migration, source/permission registry, operational DB, evidence, Daily Runner,
FullIdx or soak change. Fresh network activity was limited to three first-party
EODHD documentation GETs (all public, no login); market-data API calls **0**,
provider units **0**, paid use **0**, accounts **0**, credentials **0**, support
messages **0**, WAF/CAPTCHA bypass **0**. `git diff --check` clean; commit message
`docs: finalize eodhd semantics assessment`; no push.

---

## Level-1 blocker matrix reconciliation (2026-10-08 WIB)

**Outcome: Level-1 remains BLOCKED.** No admitted complete Level-1 source stack
exists. The remaining blockers are **external** (source permission, authoritative
evidence routes, provider semantics), not internal development gaps: the
provider-neutral architecture and readiness gates are already sufficient. This
re-run adds no new readiness enum, no requirement, and no frozen-contract change;
it consolidates accepted evidence into one dependency map. The operational
decision itself is deferred to the next milestone.

Baseline: `main`, HEAD `107ac55b24ceebc423a2e1fc3c4dff428fe0437b`, clean worktree,
19 ahead / 0 behind locally recorded `origin/main`; no fetch. Appends to this file
alone. Sources consolidated: this document (5A.2e–5A.2g, delta review, EODHD final),
[source ledger](LEVEL1_SCREENER_SOURCE_STACK.md),
[V0.2 contract](SCREENER_EVIDENCE_V0_2_CONTRACT.md) and
[persisted binding](SCREENER_EVIDENCE_V0_2_PERSISTED_BINDING.md).

### Frozen mandatory requirements (contract §§3–13)

Per-instrument/market claims: stable identity, security type, currency, listing
coverage, delisting, board/regime, board change, exchange rule version, mechanism
exception, suspension, reopening, scheduled session, completed session, corporate
action (event + coverage), source price convention, genuine price observation;
derived: price comparability (T4 only, never a base claim). Hard eligibility
dependencies are identity, listing, board/mechanism, status and session; price
authenticity gates price; optional features are feature-local. Incomplete mandatory
evidence fails closed (`UNKNOWN`/`PARTIAL`/`DATA_BLOCKED`), never a green result.

### Complete Level-1 blocker matrix

Statuses reuse existing tokens. "Admission" = current operational admission (all
are not admitted). "Root" = independent root blocker; "Derived" = consequence.

| # | Requirement | Mandatory? | Candidate source | Evidence status | Admission | Exact blocker | Kind | Required resolution |
|---|---|---|---|---|---|---|---|---|
| 1 | Stable identity | Yes | KSEI master/detail + IDX/issuer | CONDITIONALLY_ADMISSIBLE | Not admitted | Exact local-ID/ISIN/code interval; permission | Root | Permitted automated route + dated interval |
| 2 | Security type | Yes | KSEI detail + issuer | CONDITIONALLY_ADMISSIBLE | Not admitted | Ordinary-equity classification/interval | Root | Authoritative type interval |
| 3 | Listing/delisting | Yes | IDX acts + issuer/KSEI | NEEDS_CONFIRMATION | Not admitted | Operative listing/delisting act; PIT coverage | Root | Authoritative dated acts |
| 4 | TradingStatus | Yes | IDX exact-session | NOT_OPERATIONALLY_ACCEPTABLE | Not admitted | Permission + complete transitions/checkpoint | Root | 5A.2e authorized route + completeness |
| 5 | Board/mechanism | Yes | IDX roster + rules/exceptions | NEEDS_CONFIRMATION | Not admitted | Dated membership, rule version, exceptions | Root | Authoritative dated mapping |
| 6 | Trading calendar | Yes | IDX annual + amendments | Not admitted automated | Not admitted | Unattended permission; amendment coverage | Root | Permitted calendar route |
| 7 | Exceptional closures | Yes | Governing IDX notices/checkpoint | Not admitted automated | Not admitted | No complete free source | Root | Authoritative closure stream |
| 8 | Session completion | Yes | Independent IDX artifact | SOURCE_NOT_AVAILABLE | Not admitted | No authenticated completion artifact/clock | Root | Affirmative `completedAt` evidence |
| 9 | EOD OHLC (raw) | Yes | EODHD Free API (T3) | CONDITIONAL | Not admitted | Family A + Family B | Root | Provider confirmation |
| 10 | Genuine price observation | Yes | EODHD | Not admitted | Not admitted | Rows not distinguishable from synthetic/carry-forward; date/session | Derived ← 9 | Close 9 |
| 11 | Price currency/unit convention | Yes | EODHD | Not admitted | Not admitted | IDR per-share field/scale binding | Derived ← 9 | Close 9 (Family B) |
| 12 | Historical instrument continuity | Yes | KSEI/IDX + EODHD | PARTIAL | Not admitted | Dated identity/ISIN/currency/scale continuity | Root/shared | Dated metadata continuity |
| 13 | Corporate Action | Yes | KSEI/IDX/issuer | CORPORATE_ACTION_COVERAGE_PARTIAL + AUTOMATION_OR_PERMISSION_BLOCKED | Not admitted | Coverage + permission | Root | Complete all-type window + permission (5A.2g) |
| 14 | Price comparability | Yes (derived T4) | Derived from 1/9/11/12/13 | UNRESOLVED | n/a | Missing action coverage; unbound convention | Derived ← 13 (+9/11/12) | Clear parents |
| 15 | Historical/session coverage | Yes | Derived from 4/6/7/8 | Unproven | n/a | Control/session evidence absent | Derived ← 4/6/7/8 | Clear parents |
| 16 | Source provenance | Yes | Ingestion framework | Implemented | Capability exists | No admitted rows | Internal (satisfied) | Admit evidence |
| 17 | Revision / PIT correctness | Yes | As-of reader + lineage | Implemented | Capability exists | No admitted rows | Internal (satisfied) | Admit evidence |
| 18 | Required source permissions | Yes | `source` registry | `eodhd` = UNKNOWN only | Not admitted | No ALLOWED source with evidence | Root (shared) | Permitted source registration |
| 19 | Required evidence admission | Yes | — | None admitted | Not admitted | No admitted claim rows | Derived ← 1–13 | Admit roots |
| 20 | Final Level-1 readiness | Yes | Derived | DATA_BLOCKED | Not ready | Multiple mandatory gaps | Derived ← all roots | Clear roots |

Feature-specific rows (optional, contract §12): raw traded volume — **UNAVAILABLE**
(EODHD volume split-adjusted, not `RAW_AS_TRADED`); relative volume/monetary
liquidity proxy — **UNAVAILABLE**; benchmark — **CONDITIONAL** (EODHD `JKSE.INDX`,
unadmitted); `rs20Pp`/`rs60Pp` — **UNAVAILABLE**; foreign/broker flow and sector
rotation — **DEFERRED**. None of these is a universal mandatory blocker.

### Independent root blockers vs derived outcomes

Root (external) blockers: **R1** IDX automated control-evidence permission;
**R2** TradingStatus completeness; **R3** board/mechanism dated evidence;
**R4** calendar/exceptional closures; **R5** session completion; **R6** EODHD
Family A; **R7** EODHD Family B; **R8** corporate-action coverage + permission;
**R9** KSEI/IDX/issuer identity/listing/type permission and coverage; **R10**
source-permission registry (no `ALLOWED` source).

Derived (never counted as separate roots): genuine price observation ← R6+R7;
currency/unit convention ← R7; historical continuity ← R9+R7; price comparability
`UNRESOLVED` ← R8 (+R6/R7/R9); historical/session coverage ← R2+R4+R5;
`MARKET_ELIGIBLE` blocked ← R2/R3/R9; `DATA_READY` blocked ← all mandatory roots.
`DATA_BLOCKED` is the final readiness result, not an independent source gap.

### Current source-admission matrix

| Source | Technical | Automation | Permission | Semantics | Coverage | Registry | Admitted claims |
|---|---|---|---|---|---|---|---|
| EODHD | Yes | Yes (documented) | ALLOWED_JUSTIFIED (private) but registry UNKNOWN | Family A/B unresolved | Bounded pilot | `eodhd`/UNKNOWN | 0 |
| IDX official/public | Yes | Prohibited (website) / paid (system-to-system) | Blocked | Partial | Incomplete | Absent | 0 |
| KSEI | Yes (PDF/XLS) | Permission unresolved | Unknown | Partial | Incomplete | Absent | 0 |
| Issuer disclosures | Yes | Unclear | Issuer-specific | Positive events | Per-issuer | Absent | 0 |
| Stockbit | Yes | Terms require consent | Not suitable | Rich but no provenance | n/a | Absent | 0 |

Configured ≠ admitted; public access ≠ automation permission; price availability ≠
genuine observation; historical data ≠ PIT-correct; partial ≠ complete; fetch ≠
completed session; candidate ≠ production adapter.

### Root blocker prioritization (by resolution dependency)

| Blocker | Exact missing element | External vs internal | Shared by | Changes feasibility? | Monetary cost | Narrower pilot? |
|---|---|---|---|---|---|---|
| R1 IDX permission | Authorized free machine route or terms decision | External (IDX) | 4, (5,6,7) | Yes, mandatory | Requested free; paid not justified | No |
| R2 TradingStatus | Positive bootstrap + complete transitions | External (IDX) | 15 | Yes, mandatory | — | No |
| R3 Board/mechanism | Dated roster + rule versions | External (IDX) | eligible scope | Yes, mandatory | — | No |
| R4 Calendar/closures | Authoritative dated/amended schedule + exceptions | External (IDX) | 15, session | Yes, mandatory | — | No |
| R5 Session completion | Affirmative `completedAt` artifact | External (IDX) | 15 | Yes, mandatory | — | No |
| R6/R7 EODHD semantics | Endpoint-specific provider statements | External (EODHD) | 9,10,11,12,14 | Yes for price | Zero (free account) | Yes (conditional price) |
| R8 Corporate action | Complete all-type window + permission | External (KSEI/IDX/issuer) | 13,14 | Yes, mandatory | — | No |
| R9 Identity/listing/type | Permitted dated authoritative records | External | 1,2,3,12 | Yes, mandatory | — | No |
| R10 Registry | An `ALLOWED` source with evidence | Internal process on external grant | all | Enables admission | — | No |

Internal development alone resolves **none** of the mandatory roots; the missing
elements are external permission/authority/semantics. Repository work could only
build adapters *after* admission (deferred and not authorized here).

### Historical versus prospective, pilot versus full-IDX

- **Historical reconstruction:** blocked — control/session/action history unproven,
  and current metadata cannot reconstruct historical PIT states.
- **Prospective EOD:** blocked — the same permission/semantic gaps apply daily.
- **Pilot-universe:** EODHD transport feasible; Level-1 readiness still blocked.
- **Broader/full-IDX:** not feasible — bulk quota (100 units) exceeds Free, and
  identity/status/action permission+coverage gaps remain.
A future prospective warmup is conceivable only after all forward sources are
admitted; it has not started and cannot currently run.

### Decision-question answers

- **Q1** No admitted complete Level-1 operational source stack exists.
- **Q2** No — TradingStatus/session control is one of several mandatory root blockers.
- **Q3** Yes — corporate-action coverage independently prevents `CLEARED`
  comparability where required.
- **Q4** Yes — EODHD remains independently blocked by Families A and B.
- **Q5** No — internal development cannot resolve the external permission/authority/
  semantics roots.
- **Q6** No — the existing architecture is sufficient; no redesign is required.
- **Q7** Portfolio ledger/thesis facts, read-only evidence storage/inspection,
  diagnostic feature calculation and research storage remain usable independently
  of Level-1 readiness; `setupEvaluated`/candidate promotion stay blocked.
- **Q8** No — a bounded prospective Level-1 pilot is not currently admissible.
- **Q9** No — a paid provider does not necessarily resolve undocumented semantics
  or publisher permission.
- **Q10** The smallest justified next decision is the **Level-1 Operational
  Decision** (classify what may safely operate, what stays blocked, and whether
  source pursuit continues or pauses pending external permission).

### Prepared operational options (not activated)

- **A —** Maintain strict BLOCKED status until mandatory source evidence is admitted.
- **B —** Continue independent research/portfolio/evidence capabilities without
  claiming `DATA_READY`.
- **C —** Bounded prospective pilot only if every applicable frozen requirement can
  actually be satisfied (not currently).
- **D —** Future licensed/paid assessment only if it could close an exact mandatory
  blocker (not demonstrated). No option is activated; no contract is weakened.

### Validation and activity

Documentation only; append to this file alone. No production code, adapter,
migration, source/permission registry, operational DB, evidence, Daily Runner,
FullIdx or soak change; no provider/network call in this milestone (provider units
**0**, paid use **0**). Frozen contracts and persisted binding unchanged. Read-only
repository inspection only. `git diff --check` clean; commit message
`docs: reconcile level1 operational blockers`; no push.

---

## Level-1 operational disposition (2026-10-08 WIB)

**Decision: OPTIONS A + B, selected together. OPTION C is not currently
admissible; OPTION D is not justified.** Keep Level-1 operational readiness
strictly BLOCKED (A) while continuing the independent, already-implemented
research/portfolio/evidence capabilities that do not consume blocked Level-1
evidence (B). A bounded prospective Level-1 pilot (C) cannot operate because
mandatory control/action/price premises are unmet; licensed/paid investigation (D)
is not justified because no paid capability is demonstrated to close an exact
mandatory blocker. No frozen contract, source admission or readiness rule changes.

Baseline: `main`, HEAD `7b04fbb03f8726e8d5af5fff8ee5aca30637da08`, clean worktree,
20 ahead / 0 behind locally recorded `origin/main`; no fetch. This section consumes
the blocker matrix above; it does not re-run source investigations.

### Capability classification

Code readiness and operational data readiness are assessed separately; no
implemented feature is assumed to have admissible market evidence.

| Capability | Class | Basis / limit |
|---|---|---|
| Portfolio ledger (append, replay, correction, idempotency) | SAFE_TO_CONTINUE | Ledger facts come from user/broker records and need no market observation (`PORTFOLIO_LEDGER.md`) |
| Position reconciliation (ledger-derived shares/cash) | SAFE_TO_CONTINUE | Deterministic ledger replay; no external evidence |
| Investment thesis tracking | SAFE_TO_CONTINUE | Thesis versions independent of prices; no mandate change on price moves |
| Evidence storage/archive (immutable originals, provenance) | SAFE_TO_CONTINUE | Framework implemented; only already-authorized bytes may be retained |
| Stored-only as-of readers / historical evidence inspection | SAFE_TO_CONTINUE | `ScreenerEvidenceV02AsOfReader` and Research V0.1 read retained data only |
| Read-only canonical market enrichment (retained bars) | SAFE_TO_CONTINUE (retained-only) | `MarketState` returns STALE/UNKNOWN explicitly; never contacts a provider |
| Portfolio valuation | CONDITIONAL | Works only from retained canonical bars; unpriced holdings keep quantity/cost with null valuation and PARTIAL coverage |
| Diagnostic technical feature calculation | CONDITIONAL | Contract §14 permits diagnostic facts/features; `setupEvaluated=false`; no candidate promotion |
| Outcome V0.1/V0.2 verification on retained evidence | CONDITIONAL | Existing machinery retained; no new V0.2 candidates exist to enroll |
| Source-reconciliation framework | CONDITIONAL | Architecture exists; no admitted source to reconcile |
| Screener `MARKET_ELIGIBLE` / `DATA_READY` / `setupEvaluated` | BLOCKED | Mandatory control/action/price premises unmet |
| Candidate generation / promotion / `TECHNICAL_EVALUATED` | BLOCKED | Requires `DATA_READY` |
| New V0.2 evidence admission for mandatory claims | BLOCKED | No `ALLOWED` source and no admitted adapter |
| Daily ingestion / Daily Runner / automatic bootstrap | BLOCKED | Requires admitted sources and a reviewed adapter |
| FullIdx operation | BLOCKED | Disabled; free entitlement cannot support bulk |
| Optional volume/liquidity, benchmark/RS, foreign/broker flow, sector rotation | DEFERRED | Feature-local; missing optional evidence must not block stock-only readiness |
| Phase-2 fundamentals implementation | DEFERRED | Design may proceed; production ingestion not authorized |

### Root-blocker reconciliation

Root blockers remain R1–R9 (IDX control-evidence permission; TradingStatus
completeness; board/mechanism; calendar/closures; session completion; EODHD
Families A/B; corporate-action coverage + permission; KSEI/IDX/issuer
identity/listing/type permission and coverage). Two earlier classifications are
corrected as classification hygiene only:

- **R10 (no `ALLOWED` source) is an admission state, not an independent external
  root blocker.** It is the registry consequence of missing permission/evidence and
  is cleared by the same external grants.
- **Missing production adapters are deferred internal implementation work**, not
  external blockers. They become implementable only after admission; they are not
  the reason sources are blocked.

The conclusion stands: internal development alone cannot resolve missing
authoritative source evidence. Derived readiness failures (`GenuinePriceObservation`,
`SourcePriceConvention`, `priceComparability`, historical/session coverage,
`MARKET_ELIGIBLE`, final `DATA_BLOCKED`) are consequences of the roots, not new roots.

### Decision answers

1. **What can operate now:** SAFE_TO_CONTINUE capabilities plus CONDITIONAL
   retained-evidence functions within their explicit STALE/PARTIAL/UNAVAILABLE
   semantics.
2. **What must stay blocked:** screener readiness/evaluation, candidate promotion,
   new evidence admission, ingestion/Daily Runner, FullIdx.
3. **Can the screener produce `DATA_READY`:** No.
4. **Can the prospective EOD pilot begin:** No — mandatory control/action/price
   premises are unmet.
5. **Can FullIdx begin:** No.
6. **Should source investigation continue:** Yes, but only as targeted external
   inquiries (see the unblock strategy section), not repeated internal discovery.
7. **Should production collectors be implemented:** No — no admitted source, and
   speculative collectors are prohibited.
8. **Is paid-provider evaluation justified:** No — no paid capability is shown to
   close an exact mandatory blocker.
9. **Is architecture redesign necessary:** No — the provider-neutral architecture
   is sufficient.
10. **Can independent development continue:** Yes, within SAFE_TO_CONTINUE and
    bounded design scope (see the backlog section).

### Reconsideration conditions

Reconsider the Level-1 disposition only when a documented, permitted, admissible
route closes a mandatory root (IDX control evidence, corporate-action completeness,
or the two EODHD JK premises), with the exact evidence admitted through the
existing framework. No deadline or automatic retry is created.

### Validation

Documentation only; append to this file alone. No code, adapter, migration,
registry, DB, evidence, Daily Runner, FullIdx or soak change; provider/network use
**0**. `git diff --check` clean; commit message
`docs: decide level1 operational disposition`; no push.

---

## External source unblock strategy (2026-10-08 WIB)

**Purpose:** the smallest external actions that could materially improve Level-1
source feasibility. This is a targeted inquiry plan, not another provider survey.
**No message was sent, no account created, no paid service activated, and no
credential handled.** Permission, automation and paid routes are separate claims;
paying a provider never substitutes for missing correctness requirements.

Baseline: `main`, HEAD `9bc146d` (this bundle's Task 1 commit), clean worktree.
This section appends to this file alone.

### External dependency matrix

| Dependency | Owner | Exact missing evidence | Public docs sufficient? | Written confirmation? | Alternative permitted source | Paid route demonstrated? | If no response | Reopen trigger |
|---|---|---|---|---|---|---|---|---|
| IDX control evidence (TradingStatus, board/mechanism, calendar/closures, session completion) | PT BEI data services/licensing | Authorized machine route; status dictionary; positive bootstrap + complete transitions; completion clock | No | Yes | None free shown | Licensed system-to-system exists but not shown to include status/session | Level-1 stays BLOCKED | Documented permitted machine route or explicit terms decision |
| KSEI corporate action (automation, coverage, revisions) | KSEI | Automation permission; complete all-type window; correction/cancellation lineage; effective-date semantics | No | Yes | IDX/issuer originals (partial) | None demonstrated | `priceComparability` stays `UNRESOLVED` | Documented permission + coverage basis |
| EODHD JK semantics (Families A/B) | EODHD support | Endpoint-specific genuine/date/no-trade/zero statement; IDR per-share unit and historical continuity | No | Yes | None free demonstrated | Not justified | Price admission blocked | Endpoint-specific provider answer |
| Issuer/IDX filing retrieval (Phase 2) | Issuers / IDX | Authorized structured filing discovery/retrieval; publication and revision semantics | No | Yes | Issuer IR archives (per-issuer) | None demonstrated | Phase-2 production waits | Permitted filing route + revision semantics |

### Prioritized unblock actions

1. **EODHD support inquiry** — lowest cost, could conditionally unblock the price
   family; no account upgrade requested.
2. **IDX control-evidence licensing inquiry** — highest impact; asks only whether a
   permitted route exists and whether it covers status/calendar/completion.
3. **KSEI corporate-action permission/coverage inquiry** — required for
   comparability; asks for permission, coverage and revision semantics.
4. **Issuer/filing route** — Phase-2 only; deferred until Phase-2 design and
   authorization.

### Inquiry drafts (prepared only; none sent)

**IDX (data services/licensing).** "For a private, noncommercial personal system,
does IDX offer a zero-cost or licensed machine-readable route (file, feed or API)
covering: (a) positive per-security trading status per session and suspension/
reopening transitions with a completeness/checkpoint guarantee; (b) the operative
trading calendar and exceptional closures with amendments; (c) an authenticated
session-completion value (`completedAt`); and (d) dated board/mechanism and
listing coverage? Please state the permitted automation scope, recurring retrieval,
retention of raw data, historical depth, revision monitoring and any quota or
commercial conditions. If no such route exists, an explicit answer is requested."

**KSEI.** "May KSEI's public corporate-action publications (rights/HMETD, merger/
split/reverse, bonus, dividends) be retrieved by an unattended personal
noncommercial process? Please state the permitted automation scope, retention of
raw files, historical depth and revision policy, and whether a complete
all-type, all-market coverage basis (including amendments and cancellations) and
effective-date definitions are published. If unattended retrieval is not
permitted, an explicit answer is requested."

**EODHD (technical support).** Reuse the two endpoint-specific questions already
recorded in the EODHD final determination above (JK genuine-observation/date
semantics; exact raw IDR per-share currency/unit continuity). No account upgrade,
paid feature or credential is requested.

### Stop / resume policy

- **Pause** repeated IDX endpoint discovery and any provider survey without new
  evidence.
- **Resume** IDX evaluation only when a permitted machine route or explicit terms
  decision is documented; resume corporate-action evaluation only when permission
  and a complete coverage basis are available; resume EODHD semantics only on an
  endpoint-specific provider answer.
- No deadlines, automatic retries or polling loops. A negative or absent response
  is a terminal research result, and Level-1 remains BLOCKED.

### Expected effect

A positive EODHD answer could conditionally admit the price family (still subject
to scoped admission). A positive IDX/KSEI answer could unblock the control and
action families. Any single answer closes at most its own family; readiness still
requires every mandatory root to clear.

### Validation

Documentation only; append to this file alone. No external contact, account, paid
activation, credential or network provider call (units **0**, paid **0**).
`git diff --check` clean; commit message
`docs: define external source unblock strategy`; no push.
