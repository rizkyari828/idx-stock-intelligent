# Screener V0.1 — frozen implementation contract

Review completed: **2026-10-02, Asia/Jakarta**. Decision: **CONDITIONAL GO**.
Reviewed commit: `f36e539b0cdf508590879fa960b6f634af9aaba9` on `main`
(`feat: complete portfolio reconciliation onboarding`). This is a documentation-only
review. It supersedes the temporary review checkpoint. No Screener implementation,
threshold optimization, provider collection, FullIdx activation or push is authorized
by this document alone.

This document is the canonical contract for the first descriptive Screener. References
to existing files describe the reviewed baseline; requirements describe work still to
be implemented. **FROZEN** means a chosen requirement. **UNVALIDATED PARAMETER** means
a fixed starting value without evidence of predictive or economic value. **DEFERRED**
means outside V0.1. **OPEN QUESTION** is reserved for missing external evidence, with
a specified fail-closed treatment; it is not permission to invent an implementation
choice or silently substitute a value.

## A. Decision

**CONDITIONAL GO to implement a bounded, local, deterministic, descriptive pilot
Screener. Not ready to claim an analyzable all-IDX universe or validated strategy.**

The portfolio, canonical revisions, application layers and disposable harness are
adequate foundations. The first deliverable may correctly show zero eligible stocks.
Useful live candidates require evidence and continuous history that are currently
missing. Synthetic acceptance proves behavior, never market-data readiness.

The three continuation issues are resolved as follows:

| Issue | Disposition | Frozen decision |
|---|---|---|
| Existing relative performance differs from return difference | Small required implementation change | Preserve the legacy `RelativePerformance20` meaning for existing pilot consumers. Add explicitly named `rs20Pp` and `rs60Pp` under the Screener feature version. Never relabel or multiply the legacy ratio and call it the new RS. Reuse the selected bars and smoothing implementation. |
| `PilotFeatures.Calculate` lacks `through` | Small required shared implementation change; release blocker if omitted | Require an explicit market boundary in the shared calculator, filter both bars and proofs internally, and update every caller, including Worker and tests. Filtering solely in the HTTP caller is insufficient. |
| Registry effective history lacks knowledge history | Documented limitation plus small required reference-input change | Use dated, retained pilot reference snapshots for Screener identity, membership and safety evidence. Do not read mutable registry labels/classification as historical truth. Missing snapshots block the affected evaluation. Full knowledge-dated registry redesign is deferred. |

There is **no prerequisite database redesign**. There is also no waiver of chronology:
an old cutoff with no reference evidence must return unavailable, not today's identity.
The code changes above and the acceptance matrix are release conditions. External
data gaps are blockers to the affected values/candidates, not blockers to building
the honest blocked/partial-data screen.

## B. Current repository readiness

### Baseline evidence and verification limits

The interrupted review and continuation inspected source at the same commit.
On October 2, `rtk proxy dotnet build` passed with zero warnings/errors and canonical
`rtk proxy dotnet test` passed **50/50**, zero skipped. The read-only soak report returned
**1/10** unique sessions, last qualifying market date September 30, with FullIdx
`NOT ENABLED`.

During the preceding October 1 review, opt-in standard Python discovery passed
**61/61**, frontend tests **14/14**, and the Node 22 production build passed. These
are checks at this commit, not newly rerun October 2. The runbook records the prior
milestone's **8/8** disposable HTTP/PostgreSQL/Chrome acceptance and **14** offline
restore groups, with unchanged operational fingerprints. October 2 attempts to
repeat those two database-dependent checks failed before verification because the
Docker daemon was unavailable, including outside the sandbox. They are **not fresh
passes**. Recheck them with Docker available before the implementation is released.

The October 1 read-only aggregate audit found **76 canonical revisions, 11 instruments,
7 distinct market dates, August 24–September 30, 2026**; earliest canonical knowledge
was September 28 at `08:43:50.071463Z`. All observed bar quality was `DEGRADED`, market
segment `UNKNOWN`, volume basis `SPLIT_ADJUSTED`; volume units were
`SHARE_COUNT_CORROBORATED` or `UNKNOWN`. The operational registry had 11 `UNKNOWN`
types, zero symbol-history rows and 18 listing-evidence rows. These are dated audit
observations, not assertions about a currently reachable database. No raw prices or
portfolio contents are needed in this contract.

Calendar files contain seven observed sessions and two closures. September 28 has
no open-session proof in that set, so September 29–30 do not form a 20/50/60-session
continuous history with earlier bars. `pilot/instrument-sessions.json` is empty.
Published source notes with earlier soak figures are historical observations; the
soak report is authoritative for current progress.

### Source map and disposition

| Area / inspected source | Exists now | V0.1 disposition |
|---|---|---|
| `Infrastructure/Migrations/0001_phase0_foundation.sql` through `0004_portfolio.sql` under `src/IdxStockIntelligence.Infrastructure/` | Stable IDs; immutable canonical bar revisions, raw observations, listing evidence, portfolio events and theses | REUSE. No Screener tables. |
| `src/IdxStockIntelligence.Application/DailyBarRevisions.cs` | Known-at revision selection, duplicate handling, late corrections and A→B→A revisions | REUSE chronology; carry quality/provenance into the bounded read. |
| `src/IdxStockIntelligence.Application/PilotFeatures.cs` | SMA-seeded EMA20/50, Wilder ATR14, prior 20 highs/lows, volume ratio, multiplicative relative performance | CHANGE boundary and field readiness; add the precisely defined missing outputs. No competing ATR engine. |
| `src/IdxStockIntelligence.Application/InstrumentBoundaries.cs`, `AvailabilityClassifier.cs`, `PilotValidation.cs`, `ExchangeCalendarEvidence.cs`, `CompletedSessionPolicy.cs` | Knowledge-dated listing evidence, explicit calendar/session evidence, pre-listing distinction, completed-session controls | REUSE rules; do not infer missing status from missing bars. |
| `src/IdxStockIntelligence.Infrastructure/PilotDatabase.cs`, `BoundaryReference.cs`; Worker `Program.cs` | Canonical persistence, evidence import, CLI reporting | CLI `ReadRevisions` reads all history and loses some quality detail: do not call it from HTTP. New bounded driver query needed. |
| `src/IdxStockIntelligence.Infrastructure/PortfolioDatabase.cs`, `Application/ProductQuery.cs` | Npgsql, cancellation, 15-second command timeout, bounded ledger replay, through/cutoff validation | REUSE patterns. One read transaction must cover the Screener's entire database view. |
| `instrument` / `instrument_symbol_history` | Effective identity and symbol intervals; registry supports EQUITY/INDEX/UNKNOWN | NEEDS DESIGN CHANGE at the Screener input boundary: no ordinary-share proof, board history or identity known-at. Registry registration is not listing/status evidence. |
| `src/IdxStockIntelligence.Api/Program.cs` | Local-only API, existing error conventions and portfolio endpoints | NEW read-only Screener route; no run-write endpoint. |
| `frontend/src/main.tsx`, `ui.tsx`, `PortfolioTable.tsx`, `style.css`, `api.ts`, `view.ts` | Production React shell, dates, abortable requests, badges/notices/dialogs/table scrolling; Screener navigation currently disabled | REUSE visual language; NEW small screen. Prototype HTML/CSS is not production code. |
| `pilot/universe.json`, Python `idx_stock_collector/pilot.py` | Fixed ten equity candidates plus `JKSE.INDX`, 16-unit daily ceiling, hard FullIdx rejection | REUSE unchanged. Screener cannot expand collection. |
| `tests/IdxStockIntelligence.Tests`, Python standard discovery, `scripts/test_product_slice.py`, `test_portfolio_exchange.py`, `check_pilot_restore.py` | Runnable tests and owned disposable database/browser checks | EXTEND existing harnesses, do not invent a runner. |

There is no existing monetary-liquidity implementation, Screener service, episode
state machine, RS60 field or Screener UI. Domain state names alone are not those
implementations. No Redis, service split, feature database, generic strategy DSL,
new UI framework, authentication or additional dependency is justified.

## C. Frozen V0.1 scope

**FROZEN:** configured pilot membership → identity/status/data eligibility → ordinary
shares → one close-confirmed prior-high breakout → shortlist **union all held
positions**. Enrichment, risk/mandate evaluation and explanations follow in later
milestones. The long-term all-listed funnel remains the direction; this release
does not advertise ten candidates as “all IDX stocks.”

The benchmark is the stable ID for `JKSE.INDX`
`76237e96-232f-5085-9b13-dcb7104222bc`, never a discovery stock. The configured pilot
contains BBCA, BBRI, ANTM, RAJA, VKTR, ENRG, PTRO, DSSA, GOTO and LPIN in the existing
provider namespace. Membership and ticker text must still pass the dated-reference
contract in K. Held instruments outside that set are evaluated from local evidence
when available and always returned. No screen request fetches data from a provider.

One feature/policy ID, `screener-v0.1.0`, freezes these definitions and starting
parameters. Any semantic, seed, gate, threshold, episode, history-anchor or ordering
change requires a new policy ID and acceptance changes. Do not add user-adjustable
strategy knobs or switchable formulas in V0.1.

## D. Exact feature definitions

### Shared conventions

**FROZEN:** `through` is an inclusive Jakarta market date; `cutoff` is an inclusive
UTC knowledge instant, resolved once. K specifies evidence and revision selection.
`T` is the last independently confirmed completed exchange session through that
date. Closed days do not create bars. An unclassified weekday between the last
confirmed session and `through`, or an uncompleted requested open session, makes
current-session evaluation unavailable; do not silently call an older bar current.

A valid stock observation has finite, positive, internally consistent raw OHLC,
positive volume, supported provenance and verified price-unit continuity. Explicit
no-trade, suspension, missing required rows, unknown weekdays, invalid bars and
unresolved price-basis events **break** the price sequence. Known exchange closures
are skipped. A new sequence starts with the first valid observation after the break;
no filling, carrying forward, bridging gaps, or constructing suspension bars.

Price features use **raw, as-traded OHLC**, all in the same currency and unit. Do not
mix adjusted close with raw high/low. Known unit-changing events (split, consolidation,
rights/reorganization with unverified comparability) break the sequence; restart on
a subsequently cleared segment. Unknown event coverage is not clearance. Ordinary
cash dividends may remain in a verified raw-price sequence: returns are price
returns and the ex-dividend move is included, not total returns. This limitation is
displayed. A verified index price sequence uses index points and does not require
stock-like volume, share units, board or corporate-action clearance; it does require
verified index identity, source convention and complete valid index OHLC.

The deterministic pilot history anchor is **2026-08-24**, the first retained pilot
market date observed in this audit. Read from that anchor through `T`; never move
the seed backward or forward with the requested date. `through` is supported from
the anchor through **2027-08-24**, inclusive (366 civil dates). Later dates require
an explicit versioned capacity/anchor decision, not silent truncation. Dates before
the anchor are rejected as outside V0.1 history. This is a bounded pilot engineering
limit, not a trading parameter. The earliest available valid suffix within that
range supplies the seed. Pre-listing time does not count as a missing history gap.
All reported seed/start dates are part of provenance. Indicators are these seeded
pilot indicators, not a claim to reproduce a vendor's unbounded historical EMA.

For a current valid uninterrupted sequence, index observations `0..t` ending at `T`.
`C`, `H`, `L`, `V` denote its close, high, low and volume. Window membership must also
be consecutive in the independently known exchange calendar; `N+1` scattered bars
do not satisfy an N-session return.

Calculate in checked .NET `decimal`; select/validate database numeric values before
mapping to decimal. Unrepresentable canonical numeric values yield
`NUMERIC_OUT_OF_RANGE`, not overflow, clamp or zero. Do not round intermediate
values, eligibility comparisons, trigger comparisons or sort keys. Use the recurrence
evaluation order stated below. Serialize decimals as JSON numbers using the existing
API convention. UI rounding is display only: price/ATR to two decimals, percentages
and pp to two, ratios to two, IDR proxy to whole IDR; preserve full decimal text in
details. Never re-sort rounded values in JavaScript.

Every field has availability `AVAILABLE`, `WARMUP` or `UNAVAILABLE` and a reason.
Non-available numerical fields are JSON `null`. WARMUP means otherwise valid but
short history; unknown/invalid evidence is UNAVAILABLE. Known constant windows may
legitimately produce zero ATR or zero relative performance. Missing is never zero.
Optional-field failure does not erase unrelated valid features.

### Formulas and dependencies

| Field | Exact definition, current inclusion and warmup | Additional null/quality rules |
|---|---|---|
| `ema20` | At observation 19 seed `sum(C[0..19])/20`. Thereafter `E = E + (2/21)*(C-E)` in date order through current `t`. Minimum 20 observations. | Restart after a price-sequence break. |
| `ema50` | At observation 49 seed `sum(C[0..49])/50`. Thereafter `E = E + (2/51)*(C-E)`. Minimum 50 observations. | Same seed/break policy, no “close as seed” shortcut. |
| True range | For `i >= 1`, `TR[i] = max(H[i]-L[i], abs(H[i]-C[i-1]), abs(L[i]-C[i-1]))`. | The first bar alone supplies no TR. No phantom previous close. |
| `atr14` | Seed at observation 14: arithmetic mean of `TR[1..14]`. Thereafter `A = A + (1/14)*(TR-A)`. Includes current TR, requires **15 bars**. | **Wilder/RMA only**, matching the existing recurrence; no rolling-SMA ATR variant. |
| `atrPercent` | `100 * atr14 / C[t]`. | Close is the contemporaneous positive raw close; ATR must be available. |
| `priorHigh20` | `max(H[t-20..t-1])`; **excludes current high and close**. Requires 20 preceding observations plus current valid observation = 21. | Rolling extrema never use the breakout bar. |
| `priorLow20` | `min(L[t-20..t-1])`; excludes current, same 21-bar requirement. | Descriptive range boundary, not a promised fill/automatic stop. |
| `volumeRatio20` | `V[t] / (sum(V[t-20..t-1])/20)`. Current is numerator only; minimum 21 observations. | All 21 need verified comparable share units, adjustment basis and segment. A positive denominator is required. Zero/unknown stock volume breaks the sequence. UNKNOWN strings comparing equal do not verify comparability. |
| `dailyValueProxyIdr` | `C[t] * V[t]`, current only, minimum one valid observation. | IDR/share × actual same-basis shares; not an exchange traded-value field or a regular-market turnover claim. See basis gate below. |
| `monetaryLiquidity20Idr` | Arithmetic mean of the **previous 20** `dailyValueProxyIdr` observations, `sum(C[i]*V[i], i=t-20..t-1)/20`, excludes current. Minimum 21 observations under the shared current-row convention. | All 20 monetary observations require verified IDR and matching raw-price/share-quantity basis, units and consistent known segment. No partial mean, median substitute, lot conversion or index value. |
| `rs20Pp` | `100 * ((C[t]/C[t-20]-1) - (I[T]/I[T-20]-1))`; 21 aligned observations. | Simple price-return difference in **percentage points**, not RSI or ratio strength. |
| `rs60Pp` | Same formula with 60; 61 aligned observations. | All stock and IHSG sessions in the interval must align exactly, not just endpoints. |
| `changePercent` | `100 * (C[t]/C[t-1]-1)`; two consecutive valid observations. | Descriptive only; no bridging suspension/gap. |
| `distanceToHighPercent` | `100 * (C[t]/priorHigh20 - 1)`; positive means above rolling prior high. | Null without valid current close and prior high. This is distinct from the episode's frozen trigger. |
| `trend` | POSITIVE if `C > ema20 > ema50`; NEGATIVE if `C < ema20 < ema50`; NEUTRAL otherwise. | UNKNOWN if any required value is missing. Strict inequalities; equality is NEUTRAL. Descriptive. |
| `stale` | Boolean comparison of last usable observed stock date with resolved `T`: true when earlier, false when equal. | Null if no usable observation or no reliable target calendar; return reason, never silently false. Closed weekend after Friday does not make Friday stale. |
| `noTrade` | True only for affirmative, effective, knowledge-visible NO_TRADE evidence at `T`; false for affirmative trading evidence plus valid positive-volume current bar. | Otherwise null. Suspension is a separate `tradingStatus`, not no-trade. Zero volume or an absent row alone never proves no-trade. |

IHSG missing a required session makes the affected RS unavailable, not zero and not
last-observation-filled. A 21-session benchmark window can support RS20 when RS60 is
still warmup. Benchmark volume is irrelevant to RS, trend and ATR. The existing
collector's zero-volume rejection can prevent index rows reaching canonical history;
Screener must report that gap. Broadening index admission requires separately verified
source/index evidence, not a screen-side fabricated index bar.

The current pilot's legacy formula is `(Cend/Cstart)/(Iend/Istart)-1`. For stock
100→110 and IHSG 100→105 it is approximately `0.047619`, whereas the new value is
**5 pp**. Both definitions must have explicit distinct names and tests; the legacy
field is never part of the Screener DTO or ordering. Existing archived pilot reports
are not rewritten. Only one shared EMA/TR/ATR implementation should remain.

Provider documentation describes raw OHLC, adjusted close with split/dividend
adjustment, and split-adjusted volume. Therefore raw close multiplied by that volume
is not automatically contemporaneous traded value. Do not repair this with adjusted
close, a guessed factor or ×100. Leave volume ratio/liquidity unavailable until the
specific selected revisions' units/basis are cleared. Even cleared `C*V` remains a
close-price proxy, not the sum of actual trade values. Sources: repository
[semantics audit](EODHD_SEMANTICS_RIGHTS.md) and
[provider field documentation](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes).

## E. Eligibility contract

**FROZEN:** eligibility answers whether this instrument can be evaluated for the
price-breakout setup at `T`; it does not judge investment merit. Compute a stable
ordered reason list. Primary precedence below avoids calling a proven warrant a
data-blocked ordinary share merely because it also lacks prices.

1. **DATA_BLOCKED** for unresolved stable identity, conflicting authoritative
   reference evidence, unknown universe membership or unknown security classification.
   Reason priority: `IDENTITY_CONFLICT`, `REFERENCE_NOT_KNOWN`, `TYPE_UNKNOWN`.
2. Once identity is established, **INELIGIBLE** for proven non-ordinary instruments
   (index, warrant, right, fund, preferred), known pre-listing/post-delisting date,
   unsupported listing board, confirmed suspension or confirmed no-trade at `T`.
   Reason priority: `UNSUPPORTED_TYPE`, `PRE_LISTING`, `POST_DELISTING`,
   `UNSUPPORTED_BOARD`, `SUSPENDED`, `NO_TRADE`. A verified listing day and delisting
   day are inclusive listing boundaries under the existing boundary convention;
   separate suspension/no-trade evidence can still exclude those dates.
3. Otherwise **DATA_BLOCKED** for unknown listing boundary, board, status or calendar;
   price-basis uncertainty; missing/invalid current data; stale price; insufficient
   continuous price history. Ordered codes: `LISTING_UNKNOWN`, `BOARD_UNKNOWN`,
   `STATUS_UNKNOWN`, `SESSION_UNCONFIRMED`, `PRICE_BASIS_UNVERIFIED`,
   `CANONICAL_INVALID`, `NUMERIC_OUT_OF_RANGE`, `MISSING_CURRENT_BAR`, `STALE`,
   `INSUFFICIENT_HISTORY`. Include all established reasons, not only the first.
4. **ELIGIBLE** if none applies and there are at least **21** continuous valid
   observations for the current prior-high comparison. Price-volume zero without
   no-trade evidence is `CANONICAL_INVALID`/`ZERO_VOLUME_UNEXPLAINED`, DATA_BLOCKED.

Supported boards for the initial ordinary-share scope are verified IDX **MAIN** and
**DEVELOPMENT**. ACCELERATION, NEW_ECONOMY, special-monitoring/call-auction regimes
and any other affirmatively identified unsupported trading regime are INELIGIBLE
in this release; unknown is DATA_BLOCKED. This is an explicit scope limit, not a
claim those shares cannot be traded or are intrinsically unsuitable. Board and
trading mechanism are separate facts; Main-board identity alone does not override
an unsupported current mechanism. A `.JK` suffix or registry `EQUITY` is insufficient.

Missing EMA50, RS60, benchmark, optional volume semantics or monetary liquidity
does **not** block an otherwise evaluable price breakout. They yield field-level
warmup/unavailable and `PARTIAL` quality. No minimum volume ratio, monetary liquidity,
ATR% band, relative-strength cutoff or EMA trend is a V0.1 eligibility/setup gate.
This avoids silently introducing extra strategies. Current positive stock volume
is an observation-validity check, not a volume-surprise threshold.

An ineligible/blocked instrument has `setup = NONE`, `setupEvaluated = false` and
`setupReasons = [NOT_EVALUATED]`; NONE must not be presented as an evaluated lack of
setup in that case. Preserve independently supported observations and their dates.
Do not report stale indicators as current; current feature fields are null when the
current price sequence is unavailable. Last observed close/date may remain visible.
The benchmark is counted separately and never included in candidate counts.

## F. Setup state machine and episode behavior

**FROZEN:** compare unrounded raw close with the previous 20-session high. A
confirmation requires **`Close > priorHigh20`**, with no tick/percentage buffer.
Equality is not confirmation. WATCH means `0.98 * priorHigh20 <= Close <= priorHigh20`.
An intraday high above the threshold without a confirming close is WATCH only when
that close meets the proximity rule; otherwise NONE. It is not FAILED without a
previous confirmed episode.

Evaluate one ordered bar per independently completed session, starting at the fixed
anchor. Each day must satisfy that day's eligibility under the request's single
cutoff. A WATCH episode lasts at most **5** evaluated exchange sessions including
its start. A CONFIRMED phase lasts at most **20** evaluated exchange sessions including
its confirmation. These durations are unvalidated starting parameters. Closed days
do not advance counters. A gap/blocked/ineligible day terminates an active episode
as `DATA_INTERRUPTED`/`INELIGIBLE`; it never pauses the clock and later resurrects it.

Transition order on each valid evaluable session:

| Incoming state | Condition | Output / episode operation |
|---|---|---|
| NONE | Close strictly above current prior high | CONFIRMED; create episode and freeze today's prior high as trigger. WATCH is not a prerequisite. |
| NONE | Close in WATCH band | WATCH; create episode. |
| NONE | Otherwise | NONE, evaluated with `NO_SETUP`. |
| WATCH | This would be session 6 of WATCH | NONE, `WATCH_EXPIRED`; close episode before any new test on this bar. |
| WATCH | Within sessions 1–5, close above today's prior high | CONFIRMED in same episode; confirmation age 1; freeze today's prior high. |
| WATCH | Within band relative to today's prior high | WATCH, same episode. |
| WATCH | Otherwise | NONE, `WATCH_LEFT_BAND`; end unconfirmed episode. |
| CONFIRMED | This would be session 21 after confirmation | NONE, `CONFIRMED_EXPIRED`; close episode before any new price test on this bar. |
| CONFIRMED | Sessions 1–20 and close strictly below frozen trigger | FAILED, `CLOSE_BELOW_TRIGGER`; terminal failure recorded for this session only. |
| CONFIRMED | Close at/above frozen trigger | CONFIRMED, same episode, original trigger/date unchanged. |
| FAILED | Next valid evaluable session | Start from NONE and apply the NONE rules; may create a new WATCH/CONFIRMED episode with a new ID. |
| Any | Blocked/ineligible session or continuity break | NONE, not evaluated, active episode ended with interruption reason. No price failure is inferred. |

After WATCH exit/expiry or CONFIRMED expiry, **no new episode starts on that same
session**. The next evaluable session may start one. FAILED is distinct: the failure
bar ends the episode, the following evaluable bar can start a new one. These rules
settle boundary-day priority; for example a close below trigger on session 21 is
expiry, not an in-window failure. A close equal to the frozen trigger during the
confirmed phase remains CONFIRMED.

WATCH uses the freshly recomputed rolling prior high each session; a new rolling
high does not reset WATCH's ID or expiry. Only confirmation freezes a trigger.
Subsequent new highs do not reset an active CONFIRMED episode or emit another event.
Breakout distance continues to use the rolling high and must not be mislabeled
distance to the frozen trigger. `triggerPrice` is null before confirmation;
`watchThreshold` is today's rolling high when WATCH.

Episode key is the tuple `(policyId, instrumentId, episodeStartSession)`, serialized
as `policyId/instrumentId/YYYY-MM-DD`; it contains no request time. Return start,
confirmation and end dates where applicable, trigger and reason. Across revisions
the same key may acquire revised attributes, so the reproducible observation key
also includes cutoff and input digest. No persistent episode table is necessary.

`ageSessions` counts evaluable sessions since episode start, including the current
session; `confirmedAgeSessions` is null until confirmation, then starts at 1.
An episode ending on an expiry/interruption row retains its last evaluated age;
report the end reason/date rather than pretending an interrupted bar was evaluated.
An ordinary NONE row without an episode carries `episode=null`.

Price basis for confirmation, trigger, distance and failure is the same raw-price
sequence. Failure reference is the frozen trigger, **not average purchase cost** and
not a guaranteed executable stop. EMA20/50 and ATR are context; volume ratio is
descriptive; liquidity and RS are ordering evidence. EXTENDED, risk modifiers,
stop updates and automatic actions are **DEFERRED**. CONFIRMED never means BUY.

## G. Frozen definitions and research parameters

Values below are fixed before observing outcomes. No profitability claim is made.
Changing an inactive candidate into an active rule also requires a new policy version.
E/S/O mean affects eligibility / setup / ordering. An indirect effect is stated.

| Name | Class | V0.1 value / unit | Rationale and effects | Validation / version |
|---|---|---|---|---|
| EMA periods | FROZEN DEFINITION | 20, 50 valid observations | Selected small descriptive feature set; E no, S no, O no | Arithmetic specified; predictive value unvalidated; policy ID |
| ATR | FROZEN DEFINITION | 14 TRs, Wilder/RMA, percent of raw close | Reuse existing single smoothing convention; E/S/O no | Formula tests, not economic validation; policy ID |
| Prior range lookback | UNVALIDATED STARTING PARAMETER | 20 previous completed valid sessions | Reuses existing window; small auditable baseline, no search for best window; E minimum history, S yes, O via setup | Unvalidated; policy ID |
| Volume baseline | FROZEN DEFINITION | Mean of prior 20 volumes, excludes current | Explicit meaning of `volumeRatio20`; E/S/O no | Semantic basis still must be verified; policy ID |
| Liquidity baseline | FROZEN DEFINITION | Mean of prior 20 close×volume proxies, IDR | Avoid letting breakout day's volume dominate historical liquidity; E/S no, O yes | Proxy only, no execution-capacity claim; policy ID |
| RS horizons and unit | FROZEN DEFINITION | 20 and 60 sessions; simple return difference, pp | Interpretable, aligned comparison; E/S no, O yes | Formula tests; no predictive validation; policy ID |
| WATCH proximity | UNVALIDATED STARTING PARAMETER | 2% below rolling high, inclusive | Small visible near-threshold band; E no, S yes, O via state | Unvalidated; policy ID |
| WATCH lifetime | UNVALIDATED STARTING PARAMETER | 5 sessions including start | Bounds unresolved watch episodes; E no, S yes, O via state | Unvalidated; policy ID |
| CONFIRMED lifetime | UNVALIDATED STARTING PARAMETER | 20 sessions including confirmation | Prevents indefinitely active discovery events; E no, S yes, O via state | Unvalidated; policy ID |
| Confirmation comparison | FROZEN DEFINITION | Strict `>`; zero added buffer | One close-confirmed breakout definition; S yes | Exact equality tested; policy ID |
| Failure comparison | UNVALIDATED STARTING PARAMETER | Strict `<` frozen trigger; 0% buffer | Simplest failure observation; E no, S yes, O via state | Unvalidated; policy ID |
| Market volatility threshold | UNVALIDATED STARTING PARAMETER | IHSG ATR% >= 2.00% | Simple separate flag; no stock eligibility/setup/sort effect | Unvalidated, especially for index scale; policy ID |
| Minimum monetary liquidity | FUTURE RESEARCH CANDIDATE | **Disabled**, no number | No supported execution-capacity threshold yet; E/S/O gate no | Deferred; new policy required to activate |
| Minimum volume ratio | FUTURE RESEARCH CANDIDATE | **Disabled**, no number | No second confirmation condition; E/S/O gate no | Deferred |
| ATR% bounds / RS cutoffs / EMA gates | FUTURE RESEARCH CANDIDATE | **Disabled**, no values | Do not turn context into undocumented selection rules | Deferred |
| Shortlist cap | FROZEN DEFINITION | 20 candidates | Display capacity, not an economic threshold; never hides held review | Policy ID; all-candidate view remains available |
| History/read limits | FROZEN DEFINITION | Anchor and bounds in D/M | Reproducible seeds and bounded pilot cost, not tuned lookback | Policy ID; reject excess rather than truncate |

The board/type scope is a frozen conservative product boundary. Universe changes,
source evidence and bar revisions have separate reference/input identities; they
cannot quietly change the strategy policy. No thresholds were optimized in this review.

## H. Market-context contract

**FROZEN:** one independently evaluated IHSG context accompanies all rows.

- `trend = POSITIVE` when `Iclose > Iema20 > Iema50`; NEGATIVE when all inequalities
  reverse; NEUTRAL for other fully available combinations, including equality;
  UNKNOWN if close/EMA20/EMA50 or session alignment is unavailable.
- `volatility = ELEVATED` when IHSG `atrPercent >= 2.00`; NORMAL below 2.00;
  UNKNOWN without valid index ATR% and current index close. A flat verified index
  series can have ATR%=0 and NORMAL. Trend and volatility are independent axes.
- Use the same EMA/TR/ATR formulas, seed policy, through and cutoff as stocks,
  with index-specific price validation and no index-volume gate. Include index
  market date, numeric EMA/ATR context, availability and reason codes.
- Missing/stale IHSG produces UNKNOWN context and unavailable affected RS fields;
  it does not cancel an otherwise valid stock breakout or generate a portfolio action.

**DEFERRED:** breadth, percent above EMA, advance/decline, sector/group context.
Ten hand-selected stocks with uneven history cannot stand in for exchange breadth.

## I. Transparent sorting and shortlist

**FROZEN:** qualifying discovery candidates are configured ordinary shares with
`eligibility = ELIGIBLE`, `setupEvaluated = true`, and WATCH or CONFIRMED. The order is:

1. CONFIRMED before WATCH.
2. `rs60Pp` descending, available before null.
3. `rs20Pp` descending, available before null.
4. `monetaryLiquidity20Idr` descending, available before null.
5. Effective, knowledge-supported symbol in uppercase ordinal ascending order,
   missing symbol last; then stable UUID in lowercase canonical text ordinal order.

A known negative RS sorts ahead of missing RS, not behind an invented zero. No
composite score, normalization, confidence rank or hidden secondary condition.
The rank is a position in this explicit list, not a forecast.

The default shortlist is the first **20** candidates, confirmed and watch combined.
**Not every CONFIRMED candidate is guaranteed a default slot** if there are more
than 20; show omitted total/confirmed/watch counts and an “All candidates” route
through the same endpoint's `view=all&setup=CONFIRMED` or WATCH filters. Every
configured instrument remains inspectable in `view=all`, including NONE, FAILED,
INELIGIBLE and DATA_BLOCKED. All-view setup ordering is CONFIRMED, WATCH, FAILED,
NONE followed by the same numeric and identity keys. Do not sort by freshness or
quality behind the user's back.

Compute full-universe counts and canonical order first; cap shortlist second;
apply display filters third; page last. A filter never refills the top 20 from
lower ranks. For `view=all`, there is no shortlist cap before filtering/paging.
Held rows are outside every display filter, cap and page, as specified in J/M.
Counts state whether they refer to configured instruments, eligible discoveries,
filtered results or held positions; overlapping quality reasons are not summed as
mutually exclusive categories.

## J. Held-position behavior

Project the selected portfolio's immutable ledger using the **same through and
cutoff**, including correction visibility, and take positive open share positions
by stable instrument ID. Retain all such IDs even outside configured membership,
without market data, on unsupported boards/types, while suspended/delisted, with
NONE/FAILED or while DATA_BLOCKED. A closed position is not held merely because it
has an old thesis. Read the applicable thesis using existing known-at/active-version
semantics; expose FAST_SWING, LONG_SWING, INVEST or null/unassigned.

Each held row exposes the same factual features, eligibility and reasons, setup
and evaluation flag, market context, dated data quality, mandate and `held=true`.
It also has `configured` and nullable `discoveryRank`. Return one row per stable ID,
even if simultaneously shortlisted and held. The UI may reference it in two groups
but must not add it twice to counts of distinct instruments.

Do not add HOLD, REVIEW, REDUCE, EXIT, thesis-health judgment, stop changes, position
sizing or recommendation fields. An absent evaluation is not HOLD. Portfolio mandate
does not alter discovery features or rank. Average buy price is not technical support;
a failed FAST_SWING never silently becomes INVEST. No portfolio ledger writes occur.

## K. Replay and point-in-time contract

### Two independent boundaries

`through` limits market dates everywhere: stock bars, index bars, calendar proofs,
status evidence, feature windows and episode transitions. `cutoff` limits knowledge:
canonical `known_at`, source retrieval/availability, session evidence known-at,
listing evidence, reference snapshots, ledger and thesis versions. No “now” lookup
may influence a request whose cutoff was already resolved. Select the latest eligible
canonical revision per `(instrument_id, session_date)` ordered by `known_at DESC,
revision_number DESC`. Apply temporal eligibility **before** latest-revision selection;
validate its contents **after** selection. An invalid latest visible revision blocks
the date; do not fall back to an older cleaner revision. Apply the identical rule to
IHSG. A→B→A revisions remain distinct evidence. Validate `retrieved_at`, session
known-at and other available source bounds, not only the bar's nominal date.

Resolve listing evidence using `InstrumentBoundaries.AsOf` and existing
pre-/post-listing semantics. Reject contradictory same-priority evidence. Effective
dates answer when a fact applies; knowledge dates answer when it could be used.
A September effective symbol entered in October cannot be assumed known in September.

### Small reference input, not a registry redesign

**FROZEN required addition during implementation:** one versioned, local, typed
`pilot/screener-reference.json`, following the existing pilot evidence-file approach.
It is an evidence input, not a strategy DSL, database or UI write API. A missing
file is valid unavailable evidence and yields a blocked response. Never generate
“verified” contents from the current registry automatically.

Its schema version 1 contains two append-only arrays:

- `universes`: snapshots with `snapshotId`, actual `knownAt`, `universeId=PILOT`,
  the fixed stable-ID member list, benchmark ID, evidence references and content hash.
  These are configured experiment membership, **not historical exchange membership**.
  Select the latest snapshot known by cutoff. No snapshot means unknown universe,
  not today's membership retroactively. Do not backdate initial capture to listing
  dates or the history anchor.
- `instruments`: full per-instrument reference snapshots with `snapshotId`, stable
  ID, actual `knownAt`, source references and hash. Each snapshot contains effective
  identity intervals (symbol, display name, ordinary/index/unsupported class,
  currency, board), trading-status/mechanism intervals, and price/volume-basis
  clearance evidence. Select the latest snapshot for the ID known by cutoff, then
  the interval containing the evaluated market date. Intervals are inclusive dates;
  end may be null for identity, but status/basis coverage must have an explicit
  verified end. Overlapping contradictory intervals invalidate that snapshot.
  A newer full snapshot replaces the older snapshot's coverage at its cutoff;
  do not fill a newly unknown interval using an older snapshot.

Facts require source/reference, source publication date when available, retrieval
instant and known-at no earlier than retrieval. Store canonical content hashes of
the bar revisions covered by basis clearance, so a new/revised bar never inherits
clearance accidentally. An index price-convention clearance is distinct from stock
share/volume clearance. Volume clearance states shares vs lots, price/quantity
adjustment compatibility, IDR and the known segment; equality of UNKNOWN metadata
is insufficient. Price clearance certifies the selected segment's unit continuity
and event coverage; it is not a self-declared quality override. Listing dates retain
the existing canonical listing-evidence source; conflicts with a snapshot block use.

Existing `pilot/sessions.json` and `pilot/instrument-sessions.json` remain session
evidence inputs. Load their bytes once per request, filter knowledge/effective dates,
and retain old assertions; append corrections with later known-at rather than
overwriting old history. If overlapping assertions conflict without an unambiguous
dated replacement, block the affected interval. Recognize verified index observations
without applying ordinary-share status/volume requirements to them.

The initial file can be empty. Populating it with substantiated evidence is an
external data-readiness task, not permission to manufacture attestations in tests
or operational storage. Retain released snapshots and source references in version
control/local restore inputs; hashes detect accidental mutation. Tests must prove a
later appended snapshot cannot affect an earlier cutoff. Full automated reference
ingestion and a general bitemporal registry are **DEFERRED** until dynamic universe
or operational volume warrants them. Mutable registry data can continue serving
portfolio registration; Screener neither modifies it nor uses it for hidden fallback.
Without knowledge-supported labels, held rows show their stable ID and null labels.

### Reproducibility, provenance and limitations

Use one read-only repeatable-read database transaction, one copied reference bundle
and a single resolved cutoff for the request. Make the cancellable query return only
selected instruments and anchored dates, with canonical quality and provenance.
Do not use the unbounded CLI reader, perform one query per bar, or compute from
uncommitted mixed snapshots. The selected source set is limited as in M.

Return policy ID, through, target session, cutoff, history anchor, effective universe
snapshot ID, selected reference IDs, seed dates and an input SHA-256. Hash a stable
ordered serialization of **selected** revisions `(ID,date,revision,knownAt,contentHash)`,
selected calendar/listing/status/reference evidence, selected ledger/thesis facts,
and resolved request/policy inputs. Decimal strings in that hash use invariant
round-trip formatting; dates ISO; timestamps UTC; arrays sorted by stable keys. Do
not hash the entire latest file or future rows: appending future-only evidence must
not change an old cutoff's input digest. Store no raw paths or credentials in the API.

For identical explicit inputs, retained evidence and policy, response values/order
and digest repeat. A later cutoff may legitimately select corrections, change
eligibility or rewrite a computed episode's attributes; this is a reconstructed
view, not a stored historical decision. Changing offset/filter changes presentation,
not the evaluation input digest. Paging pins cutoff and input digest (M).

**Explicit September 15 answer: no, the repository cannot recover a real Screener
decision made on 2026-09-15 using only knowledge available then.** Screener did not
exist, the audited bar knowledge begins September 28, and dated identity/status/
membership evidence for that cutoff is absent. The implemented endpoint can
reproducibly answer “unavailable at that cutoff”; it cannot reconstruct information
never retained. Using October knowledge to describe September 15 is a later
reconstruction and must be labeled with that October cutoff. It is not a prospective
September decision. No imported source publication date may replace actual local
knowledge time merely to make a backtest work.

Prospective fixed-pilot replay becomes trustworthy only from retained, dated inputs
and adequate history. Strict historical **all-listed universe** reproduction remains
unsupported. Portfolio held IDs/mandates have their own existing chronology; identity
labels remain unavailable if the reference cutoff lacks them. These limitations
are visible in `replayScope` and reason codes, not hidden in developer notes.

## L. Persistence and migration decision

**FROZEN: option A — pure computed endpoint. No Screener result/run/episode tables,
no database migration for V0.1.** Reuse canonical bars, listing evidence and portfolio
history. The small retained reference input in K supplies missing pilot facts without
retrofitting every registry writer/export/restore path. It does not claim to repair
the registry's historical knowledge.

No new database index is justified before measuring the bounded query against the
existing `(instrument_id,session_date,revision_number)` key and pilot size. An actual
query-plan problem can justify a focused index migration later; do not pre-create it.
Document the history/reference size ceiling in the implementation with a `ponytail:`
comment and the upgrade path. Do not silently widen limits or truncate evidence.

**DEFERRED:** immutable decision snapshots/outcome tracking. Before prospective
evaluation in O, introduce a separately reviewed evidence-retention milestone that
saves all candidates/rejections, policy/inputs and episodes. A computed screen alone
does not prove what the user saw at a past instant. No speculative snapshot schema
is frozen here. Until that milestone, make no prospective performance claims.

## M. API contract

**FROZEN:** one read-only route, `GET /api/screener`. No POST/run IDs/background jobs.
Existing loopback/security/error conventions apply; local-only is not permission to
fetch arbitrary URLs or accept server filesystem paths.

| Parameter | Contract |
|---|---|
| `through` | Optional ISO date, default Jakarta today; limited to D's anchor/horizon and not future relative to request clock. |
| `cutoff` | Optional RFC3339 timestamp with offset; blank/omitted resolves once to now, not future and not before 1900. Return normalized UTC. |
| `universe` | Optional, only `PILOT`, default PILOT. `FullIdx` or arbitrary symbol lists return 400. |
| `portfolioId` | Optional UUID. Omitted means discovery only with `heldIds=[]`; a selected portfolio adds every held ID. Missing portfolio returns 404; portfolio created after cutoff is not visible at that cutoff. |
| `view` | `shortlist` (default) or `all`. |
| `setup` | `ALL` (default), NONE, WATCH, CONFIRMED or FAILED. Filters discovery/all-universe list only. |
| `eligibility` | `ALL` (default), ELIGIBLE, INELIGIBLE or DATA_BLOCKED. Filters list only. |
| `offset`, `limit` | Defaults 0/20; offset 0..10000; limit 1..100, integers. Out-of-range is 400, not silently clamped. |
| `inputHash` | Optional lowercase SHA-256 from the first page; if supplied and evaluation inputs differ return 409 `INPUT_CHANGED`. Subsequent pages must send resolved cutoff and this hash. |

No customizable sorting or held exclusion parameter. The response contains one
deduplicated row collection plus ordered ID lists for the paged list and all held
positions. Rank and summary are computed before display filtering. Held IDs sort by
knowledge-supported symbol then stable ID and do not consume page capacity. API
numeric fields use explicit `%`, pp or IDR names; counts are integers, flags boolean
or null where unknown. A nullable field's reason is exposed in `fieldStates`.

Bounds: at most 10 configured stocks plus one benchmark; at most 200 held IDs;
at most 211 distinct selected IDs total. Reject oversize input/reference configuration,
do not call it FullIdx or silently drop held IDs. At most 366 civil dates and **80,000**
selected bar rows, after as-of selection, with limit+1 overflow detection. Keep the
existing 10,000-event portfolio bound, command timeout 15 seconds, cancellation on
all database reads and calculation loops. Reference files together are limited to
4 MiB and 10,000 evidence records; exceeding a bound yields explicit unavailable
service response, never partial truncation. No historical revision scan is loaded
into application memory merely to choose one row per date; select as-of in SQL.

HTTP 200 covers COMPLETE, PARTIAL and BLOCKED evaluations. Incomplete source data is
not a server exception. COMPLETE means every configured row has a known eligibility
decision and every applicable requested feature/context is available; known exclusions
do not require stock indicators. PARTIAL means at least one instrument was evaluated
but required/optional coverage elsewhere is incomplete. BLOCKED means no configured
stock's setup could be evaluated, or universe/calendar is unresolved. No-candidate
COMPLETE (all known exclusions or evaluated NONE) is distinct from BLOCKED: if every
configured instrument is affirmatively INELIGIBLE and evidence is complete, report
COMPLETE even though evaluated count is zero. Invalid query is 400, absent portfolio
404, changed pinned input 409; database/timeouts/bounds/malformed reference bundle
503 with a stable error code and no leaked connection details. Client cancellation
stops work and must not be converted into a fabricated empty successful response.

Counts: `configured = eligible + ineligible + dataBlocked` when universe is known;
`evaluated` counts configured stocks with `setupEvaluated=true`; `candidates` counts
configured WATCH/CONFIRMED before cap. `insufficientHistory`, `stale`, `unsupported`
are overlapping diagnostic subsets. `held` and `heldOutsideUniverse` are separate.
Unknown universe yields null configured/category counts, not zero membership. Every
known configured instrument can be retrieved in `view=all`; none is silently discarded.

Example request:

```http
GET /api/screener?through=2026-09-30&cutoff=2026-10-02T03%3A00%3A00Z&universe=PILOT&view=all&offset=0&limit=20
```

The following **synthetic, shortened-universe fixture** illustrates the complete
shape, not an assertion about actual September market values or reference clearance.
UUIDs/hashes are fixture identifiers. Additional rows have the same schema.

```json
{
  "policyId": "screener-v0.1.0",
  "through": "2026-09-30",
  "targetSession": "2026-09-30",
  "cutoff": "2026-10-02T03:00:00Z",
  "historyAnchor": "2026-08-24",
  "universe": "PILOT",
  "universeSnapshotId": "fixture-universe-1",
  "replayScope": "FIXED_PILOT_KNOWN_INPUTS",
  "inputHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "status": "PARTIAL",
  "reasons": ["OPTIONAL_FEATURES_UNAVAILABLE", "RAW_PRICE_RETURNS"],
  "summary": {
    "configured": 1, "eligible": 1, "ineligible": 0, "dataBlocked": 0,
    "evaluated": 1, "insufficientHistory": 0, "stale": 0, "unsupported": 0,
    "candidates": 1, "confirmed": 1, "watch": 0,
    "shortlisted": 1, "omittedConfirmed": 0, "omittedWatch": 0,
    "held": 0, "heldOutsideUniverse": 0
  },
  "marketContext": {
    "instrumentId": "76237e96-232f-5085-9b13-dcb7104222bc",
    "marketDate": null, "trend": "UNKNOWN", "volatility": "UNKNOWN",
    "close": null, "ema20": null, "ema50": null, "atr14": null,
    "atrPercent": null, "reasons": ["BENCHMARK_MISSING"]
  },
  "page": {"view": "all", "setup": "ALL", "eligibility": "ALL", "offset": 0, "limit": 20, "total": 1},
  "discoveryIds": ["00000000-0000-4000-8000-000000000001"],
  "heldIds": [],
  "rows": [{
    "instrumentId": "00000000-0000-4000-8000-000000000001",
    "symbol": "FIXTURE", "displayName": "Synthetic ordinary share",
    "configured": true, "held": false, "mandate": null, "discoveryRank": 1,
    "eligibility": "ELIGIBLE", "eligibilityReasons": [],
    "setup": "CONFIRMED", "setupEvaluated": true,
    "setupReasons": ["CLOSE_ABOVE_PRIOR_HIGH"],
    "episode": {
      "id": "screener-v0.1.0/00000000-0000-4000-8000-000000000001/2026-09-30",
      "startDate": "2026-09-30", "confirmationDate": "2026-09-30",
      "endDate": null, "endReason": null, "ageSessions": 1,
      "confirmedAgeSessions": 1, "triggerPrice": 110, "watchThreshold": null
    },
    "marketDate": "2026-09-30", "close": 111, "changePercent": 0.9090909090909091,
    "ema20": 102.5, "ema50": null, "trend": "UNKNOWN",
    "priorHigh20": 110, "priorLow20": 90, "distanceToHighPercent": 0.9090909090909091,
    "volume": 10000, "volumeRatio20": null, "dailyValueProxyIdr": null,
    "monetaryLiquidity20Idr": null, "atr14": 2, "atrPercent": 1.8018018018018018,
    "rs20Pp": null, "rs60Pp": null, "stale": false, "noTrade": false,
    "tradingStatus": "TRADING", "dataQuality": "PARTIAL",
    "dataReasons": ["EMA50_WARMUP", "VOLUME_BASIS_UNVERIFIED", "BENCHMARK_MISSING"],
    "fieldStates": {
      "ema50": {"availability": "WARMUP", "reason": "INSUFFICIENT_SESSIONS"},
      "volumeRatio20": {"availability": "UNAVAILABLE", "reason": "VOLUME_BASIS_UNVERIFIED"},
      "dailyValueProxyIdr": {"availability": "UNAVAILABLE", "reason": "VOLUME_BASIS_UNVERIFIED"},
      "monetaryLiquidity20Idr": {"availability": "UNAVAILABLE", "reason": "VOLUME_BASIS_UNVERIFIED"},
      "rs20Pp": {"availability": "UNAVAILABLE", "reason": "BENCHMARK_MISSING"},
      "rs60Pp": {"availability": "WARMUP", "reason": "INSUFFICIENT_SESSIONS"}
    },
    "provenance": {
      "priceBasis": "RAW_AS_TRADED", "source": "SYNTHETIC",
      "currency": "IDR", "volumeUnit": "UNKNOWN",
      "volumeBasis": "UNKNOWN", "marketSegment": "UNKNOWN",
      "revision": 1, "knownAt": "2026-09-30T13:00:00Z",
      "retrievedAt": "2026-09-30T12:59:00Z",
      "contentHash": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
      "priceSequenceStart": "2026-08-24", "emaSeedStart": "2026-08-24",
      "atrSeedStart": "2026-08-24", "consecutiveSessions": 26,
      "referenceSnapshotIds": ["fixture-identity-1", "fixture-clearance-1"],
      "canonicalQuality": "DEGRADED"
    }
  }]
}
```

`fieldStates` is a sparse map: every non-available numerical/tri-state field must
have an entry; a present supported value without an entry means AVAILABLE. Null
mandate, episode or rank has the structural meaning unassigned/no episode/not a
candidate and needs no feature reason. If multiple causes apply to a feature, use
the first in this order: identity/canonical invalid, missing current observation,
insufficient stock history, price/volume basis, benchmark gap; include remaining
causes in row data reasons. `dataQuality` is COMPLETE/PARTIAL/BLOCKED and never
overwrites `provenance.canonicalQuality`; `DEGRADED` admission is not magically
promoted by a computed result. Available pilot price fields require independent
clearance of their particular dependencies even with DEGRADED source quality.

`marketDate` is the actual observed close date, including when stale. At a gap return
null current indicators, explicit stale/unknown state and the last observed close
with that date; never relabel it as T. For non-evaluated rows, episode may be the
episode terminated on T with its end reason, otherwise null. Return all state,
feature and provenance keys consistently even when values are null. Market context
is shared once at top level. No recommendation, action, score or AI-confidence key
exists in the response.

## N. UI contract

**FROZEN:** enable one Screener entry in the existing React sidebar. Use the current
production shell, `PageHeader`, `Metric`, `Badge`, `Notice`, `Dialog`/native details,
formatting conventions and accessible table scroll container. Extend a shared
component only where needed (for example its loading label); do not fork the design
system or transplant `design/portfolio-prototype` as another application.

The page works without an open portfolio for discovery. Selecting a portfolio adds
a persistent “All held positions” section, independently of the candidate page and
filters. Reuse the applied through/known-by context, with WIB input converted to the
API's explicit offset/UTC. Show the resolved target session, actual cutoff, fixed
pilot label and “Descriptive setup; not an order” text. Changing context aborts the
old request, clears mismatched results and resets paging; never display a prior
request's rows under new dates. Keep portfolio reconciliation draft state separate.

Summary: configured, eligible, data blocked and held counts; an expandable breakdown
for ineligible/insufficient/stale/unsupported, candidate counts and omissions. Display
IHSG trend and volatility separately, including UNKNOWN reasons. A persistent
coverage notice states when optional RS/liquidity is missing and which evidence
prevents setup evaluation. Do not celebrate an empty blocked run as “no opportunities.”

| Default table columns, in order | Detail/expanded fields |
|---|---|
| Symbol with Held marker; Setup; Close with observed date; Distance to prior high %; RS60 pp; RS20 pp; Prior-20 value proxy (IDR); Data state | Stable ID/name; mandate; eligibility and every reason; setup evaluated flag and episode start/confirmation/expiry/failure/trigger; change %; EMA20/EMA50 and descriptive trend; ATR14/ATR%; prior high/low; raw volume and verified unit/basis; volume ratio; daily value proxy; field availability; current vs frozen trigger; selected revision, cutoff, source and reference identifiers |

Use explicit “pp” on RS, “%” on price distances, “proxy” on monetary liquidity and
IDR as unit. Null renders “Unavailable” or an em dash with accessible reason; it
must not render zero, a green badge, or “normal.” Dates and quality are always
discoverable without a hover-only interaction. NONE with not-evaluated flag is
displayed “Not evaluated,” not “No setup.” Use text/icon plus color for state.

Minimum controls: Shortlist / All instruments, setup and eligibility selects, next/
previous paging. CONFIRMED and WATCH counts may link to the corresponding all-view
filter. The held section stays visible when any filter yields no candidates. Sorting
is the fixed order explained beside the table; no sortable header suggesting a
different canonical rank. No parameter sliders, Buy buttons or export/run-persistence
workflow in this milestone.

Loading uses an accessible busy state with a Screener label. Empty complete results
say no candidates met this descriptive rule. Partial results show coverage and
reasons. BLOCKED results say what evidence/history is missing and allow inspection
of known configured/held rows. Database errors show retry without pretending data
is empty; a 409 asks the page to refresh its pinned context. A stale observed close
keeps its date and badge; derived current indicators remain unavailable.

At 1440, 1366, 1280 and 1024 px, preserve readable numeric alignment and table
headers; at 768 and 390 px use the existing horizontal scroll pattern. Keep symbol,
setup, dated price and data state easy to identify; do not delete quality/held facts
to fit. No body-wide overflow, clipped dialogs or controls. Verify keyboard traversal,
visible focus, screen-reader labels, light/dark states, accessible details and aborted
request races in the existing browser harness. Stock Detail stays reserved.

## O. Stock Detail boundary and later evaluation

Screener provides rapid comparison, bounded evidence, transparent rank and small row
details. **DEFERRED to Stock Detail:** full price charts, individual raw artifacts,
complete revision timelines, corporate-action investigation, filings/news/events,
fundamentals, ownership/broker information, thesis editing, position/risk planning,
alternative strategies and richer historical episode exploration. Do not design a
terminal or add a charts dependency to show this table.

### Evaluation milestone after V0.1

Purpose: disprove bad rules and detect data/selection artifacts, not manufacture
alpha. Freeze policy and reference inputs before collecting outcomes. Retain the
full configured population, exclusions with reasons, candidates below the cap and
every held row; evaluating only the displayed top 20 creates selection bias. Retain
prospective result/evidence bundles in the separate persistence milestone before
claiming what was known at decision time.

The independent event unit is an **episode**, not a daily row. WATCH starts/ends and
CONFIRMED/FAILED boundaries follow F exactly. One watch that confirms contributes
one confirmed episode; ten subsequent CONFIRMED rows are not ten trades or lessons.
Record unconfirmed watch expiry/exit and interruptions separately from price failures.
Revision-aware reconstruction is a separate dataset from saved prospective decisions.

Initial descriptive observations use confirmation close as a clearly labeled
non-executable reference: raw-price returns and IHSG-relative price-return differences
at **1, 5, 10 and 20 subsequent exchange sessions**. Record missing horizons, suspension,
delisting and action-basis uncertainty as unavailable/censored; no substitution with
the next available close, no survivor-only removal. A forward no-trade session still
occupies its scheduled horizon and may make the observation unavailable. These
horizons are unvalidated research choices, versioned with the evaluation policy, and
are not additional V0.1 entry/exit rules. Forward outcomes must not enter Screener
inputs, ranks or threshold selection during the initial frozen evaluation.

If an executable FAST_SWING policy is later assessed, earliest assumed entry is
the next exchange session **after the signal was actually available**: both the
signal close and its local data availability matter. A late next-day retrieval
cannot assume that day's already-passed open. Explicitly model tradability, auction/
price-limit constraints, lots, fees/taxes, slippage, spread/liquidity and actual
entry availability. No same-close entry, guaranteed stop fill, forced exit at an
untraded price or silent mandate change. Raw dividend-affected descriptive returns
are not net total-return performance. Costs and execution assumptions require their
own verified contract before any economic claims. No optimizer is part of this work.

## P. Test and acceptance matrix

Use the existing standard .NET discovery and established frontend/Python harnesses.
Fixtures live only in test memory or the harness-owned disposable database, never
operational prices/holdings/reference attestations. The matrix is required behavior,
not a request for one test framework/file per cell. Parameterized cases may share
small deterministic fixtures. All numerical comparisons use unrounded decimal
values/declared recurrences; UI rounding tests are separate.

For formula checks with repeating decimal coefficients, compare the mathematical
expected value within absolute `1e-24` for the small fixtures below, rather than
demanding infinite-precision rational arithmetic from .NET decimal. For example,
the Wilder update approaching 3 may differ in its final stored decimal places.
This test tolerance is **not** a production comparison buffer: triggers, eligibility,
trend and sort always use the actual unrounded decimal results.

| Area | Concrete fixture / check | Required result |
|---|---|---|
| EMA20 | 19 then 20 closes; `1..20`, then 21 | Null/WARMUP at 19; seed 10.5; next EMA 11.5. |
| EMA50 | 49 then 50 closes; `1..50`, then 51 | Null/WARMUP at 49; seed 25.5; next EMA 26.5. |
| EMA seed/through | Add future bars/proofs to a through-bounded request; append a later cutoff-only revision | Same historical seed/result at unchanged cutoff. Both entry-point and shared-calculator tests. |
| True range | H=105,L=103,previous C=100; then H=97,L=95,previous C=100; and H=104,L=98,previous C=100 | TR 5, 5, 6 respectively; no first-bar artificial TR. |
| ATR14 warmup/smoothing | 14 bars, then 15 with 14 TRs=2; next TR=16 | Null at 14 bars, ATR=2 at 15, next ATR=3 by Wilder recurrence. No SMA replacement. |
| ATR% / zero | ATR=3,C=100; also a verified flat positive-price/positive-volume series | 3%; flat series ATR=0 and ATR%=0, not null. Invalid/nonpositive denominator unavailable. |
| Prior extrema | Previous 20 highs have max120/lows min80; current high999/low1 | priorHigh=120/priorLow=80, unaffected by current extremes. 19 prior bars is WARMUP. |
| Volume ratio | Previous 20 volumes=100, current 200, all cleared; then one missing/zero/unknown-basis observation | Ratio 2; affected invalid cases null, not divide-by-zero or partial baseline. Current volume excluded from mean. |
| Monetary liquidity | Previous 20 C=10,V=100; current C=100,V=10000, all cleared | Prior mean 1000 IDR; current proxy 1000000 IDR. Split-adjusted incompatibility, UNKNOWN segment or unknown currency makes affected proxy unavailable. |
| RS20 | 21 consecutive aligned bars, stock 100→110, IHSG 100→105 | 5 pp; assert unequal to legacy multiplicative 0.047619… ratio. |
| RS60 | 61 aligned bars, stock 100→120, IHSG 100→110 | 10 pp; 60 stock observations WARMUP. No use of a 60-calendar-day shortcut. |
| Benchmark alignment | Remove an interior IHSG date while endpoints remain; index has valid zero/unknown volume | RS affected window unavailable for missing date; valid index price does not require positive volume. Never substitute closest dates. |
| Missing IHSG | Valid stock 21+ observations, absent index | Stock can be ELIGIBLE/CONFIRMED; RS null, market context UNKNOWN, PARTIAL coverage. |
| Calendar/no-trade | Friday followed by proved closure; unknown weekday; independently confirmed stock NO_TRADE; zero volume without proof | Closure skipped, unknown weekday breaks continuity, noTrade true only with proof, unexplained zero DATA_BLOCKED with noTrade null. |
| Stale | Target Tuesday with last valid stock Monday; Friday target resolved on closed weekend; no observations | true, false, null respectively, with original observed dates. |
| Corporate-action gate | Unverified split across lookback, revised volume rebasing, cleared post-event segment | No bridging or raw/adjusted mixing; reset price warmup; new revision needs matching clearance; volume failures need not erase cleared price feature. |
| NONE/WATCH boundaries | priorHigh 100; closes 97.99,98,100 | NONE, WATCH, WATCH. Equality never confirms. |
| Confirmation / intraday | H101,C100.01 against 100; H101,C99; H101,C97 | CONFIRMED, WATCH, NONE. Intraday rejection alone is not FAILED. |
| Failure boundary | Confirmed trigger 100, later C100 then 99.99 | Still same CONFIRMED at equality; FAILED below, terminal date/reason retained. |
| WATCH expiry | Five evaluable near-threshold sessions, then sixth above priorHigh | Sixth is WATCH_EXPIRED/NONE, no same-session restart; next valid bar can start new episode. |
| CONFIRMED expiry | 20 confirmed-phase sessions then session 21 below trigger | Expiry/NONE on 21, not an in-window FAILED event; next valid session can create a new episode. |
| Repeated/new triggers | Higher rolling highs within WATCH and CONFIRMED; multiple closes above threshold | WATCH threshold updates without age reset; confirmation freezes trigger; no daily new confirmation ID. |
| FAILED reset / interruption | Day after failure meets WATCH/direct-confirm rule; gap while active | New episode next valid session; interruption ends old episode without invented price failure or resurrection. |
| Security eligibility | Known warrant/index, pre-IPO, post-delisting, unsupported board/regime | INELIGIBLE with reason; pre-IPO time is not a provider gap. |
| Unknown identity/status | Missing or contradictory reference; generic EQUITY registration only; blank status; insufficient history | DATA_BLOCKED, never ordinary-share inference from suffix; insufficient 21 history separately reported. |
| Precedence | Known unsupported type plus missing prices; unknown/conflicting identity plus plausible bars | Proven unsupported type INELIGIBLE; unresolved identity DATA_BLOCKED. No valid setup on either. |
| Canonical validation | Latest visible malformed/decimal-overflow bar over older valid bar | Unavailable with exact error, no fallback to older revision, no exception returning fabricated empty success. |
| Ordering/nulls | Tied states/RS/liquidity; known negative RS vs null; identical symbols | Fixed lexicographic order, available negative ahead of missing, final stable-ID tie-break. No round-before-sort. |
| Cap/filter/page | More than 20 fixture candidates via service-level fixture (without expanding production universe); mixed WATCH/CONFIRMED | Correct capped order/omitted counts; all-view retrieves all; filtering after cap doesn't refill; page boundaries no duplicates. Production universe still max 10. |
| Held union | Outside-universe held; blocked/FAILED held; held also shortlisted; filters exclude its state | Every held ID returned, one row per ID, no cap/filter loss or double distinct count. Closed holdings absent. |
| Portfolio chronology | Later event correction and later mandate change | Earlier cutoff retains original shares/mandate; through excludes future-dated trades. No live registry override. |
| Null DTO contract | Missing features/benchmark/identity; no active episode | Null values with reasons, UNKNOWN context; no numeric zero substitutions, recommendation/action/score/confidence fields. |
| Query validation | Bad date/cutoff/UUID/enums/page; FullIdx; overflow configured/held/reference bounds | 400/404/503 as specified; no truncation, expansion or provider call. |
| Replay | Repeat same resolved inputs; append a future bar/status/reference/ledger item | Identical deterministic values/order/inputHash. Future-only evidence not in digest. |
| Revision cutoff | Price and benchmark A→B→A corrections at distinct known-at times | Each cutoff selects correct revision; later cutoff may revise setup and RS; old cutoff unchanged. |
| Identity chronology | Back-effective symbol captured after cutoff; later full reference snapshot clears/deletes old coverage | No future label/classification leak; absent evidence blocks; older cutoff uses retained old snapshot. |
| Calendar chronology | Future-known open/closure proof for old date; incompatible same-time assertions | Unknown at old cutoff; usable only when known; ambiguity blocks, not guessed trading. |
| September 15 | Through 2026-09-15 and historical cutoff, no retained knowledge | Explicit unavailable result, no retrospective September 28 observations or current universe labels. |
| Input pin / concurrency | Insert revision between separate requests; change file while one request is running | One request sees its copied bundle/transaction consistently; pinned input mismatch409; no mixed pages. |
| Bounds/cancellation | Selected IDs/date limits, cancellation before/during read/calculation | Bounded SQL and memory, prompt cancellation, no unbounded CLI reader; respect 15-second DB timeout. |
| UI | Busy/complete-empty/blocked/partial/stale/error; 390 through 1440 px; keyboard and request race | Honest states, dated values, held visibility, accessible scroll/details, no stale response replacing new context. |
| Operational safety | Fingerprint canonical/portfolio tables and operational/soak files before/after disposable acceptance/restore | Unchanged; zero provider requests; FullIdx still rejected; all owned fixtures cleaned up, no production synthetic writes. |

Run canonical `rtk proxy dotnet test`, Python standard discovery with the opt-in
fingerprint where Docker is available, frontend tests/build and the disposable
acceptance/restore harnesses. An unavailable Docker daemon is a recorded environmental
block, never a passing test. Do not use an executable demo runner as the primary
substitute for standard discovery. The implementation must add its coverage to the
existing suites and report actual new totals, not promise the current 50/61/14/8 counts.

## Q. Data feasibility gaps

Readiness below concerns **real available pilot data**, distinct from whether the
formula can be implemented and tested now. The seven audited dates are not enough
for any requested 20/50/60-session feature. Missing semantics cannot be repaired by
a successful build or synthetic fixture.

| Capability | Readiness | Missing/uncertain evidence | Minimal V0.1 treatment / impact |
|---|---|---|---|
| EMA20 / EMA50 | BLOCKED | 20/50 continuous certified observations, price continuity/action coverage | Null/WARMUP or UNAVAILABLE. EMA absence alone not a setup gate once 21 valid prices exist. |
| ATR14 / ATR% | BLOCKED | 15 continuous safe price observations; current sequence much shorter | Null with seed/history/basis reason. Does not independently block price breakout. |
| Prior high/low20 | BLOCKED | 21 continuous safe observations plus current status/identity | Blocks setup evaluation; no shorter fallback window. |
| Volume ratio20 | BLOCKED | 21 observations; units/basis/segment comparability not broadly verified | Null optional feature; sampled share-scale corroboration for two stocks is not whole-universe proof. |
| Monetary liquidity | BLOCKED | Contemporaneous volume/price basis, IDR and segment; required history | Null optional sort field; no false turnover precision. |
| RS20 / RS60 | BLOCKED | 21/61 aligned stock and benchmark dates; price basis | Null independent fields; use rest of transparent ordering. Existing relative ratio is not a ready substitute. |
| IHSG trend / volatility | BLOCKED | 50 /15 valid continuous index observations and completed dates | UNKNOWN independently, no stock setup veto. Index volume semantics do not fix missing prices. |
| Stale flag | READY WITH LIMITATION | Current completed-session proof/calendar gaps can make target unknown | Known dates permit factual comparison; unknown target/no bar yields null. Read-only implementation needed. |
| No-trade / suspension | BLOCKED | Empty instrument-session evidence file, no comprehensive effective dated status proof | Do not infer from absence or zero. A positive price alone is not full status clearance. |
| Ordinary identity / board / mechanism | BLOCKED | Generic UNKNOWN registry in audit; no knowledge timeline or verified ordinary/board coverage | Dated snapshots or DATA_BLOCKED. No suffix/type guessing. |
| Listing boundary | READY WITH LIMITATION | Existing append-only evidence; LPIN exact date remains partial; initial trading differs from listing | Reuse verified boundary assertions; partial bounds stay unknown. Not all historical sessions become gaps. |
| Bar revision as-of | READY | Revision/knowledge/raw provenance exists | Add shared through enforcement and bounded quality-preserving query. Ready mechanism does not imply older observations exist. |
| Full historical cutoff reproduction | BLOCKED for actual September 15 decision | No retained observations then; identity/membership/status knowledge absent | Reproduce an unavailable response; prospective fixed-pilot replay only after evidence retention. Never claim historical all-IDX replay. |
| Portfolio held/mandate reconstruction | READY WITH LIMITATION | Existing tested ledger/thesis chronology; labels lack historical knowledge | Reuse shares/mandates and preserve held IDs; reference-gate names, no disappearing holdings. |
| Breadth, total-return/action-adjusted research, full-universe identity | DEFER | Coverage and semantics not established | No fields/gates pretending readiness. |

**OPEN QUESTION — external evidence only:** which dated authoritative sources establish
each pilot instrument's ordinary-share class, board/mechanism, trading status, complete
unit-changing-event coverage, share-volume basis/segment and actual entitled history?
The decision is frozen: missing proof leaves the field/row unavailable. There is no
implementation discretion to assert VERIFIED or lower the history requirement.

Retention after provider cancellation remains unresolved in the existing rights
audit; do not infer permission, redistribute source data or broaden use. The existing
approved private pilot scope remains the only collection scope. Resolving commercial
or retention terms is a data/rights gate for expansion, not a new Screener feature.

## R. FullIdx expansion gate

**FROZEN: FullIdx stays DISABLED.** Screen queries, held positions and registry
registration must not enlarge the collector universe. Preserve Python's fixed-pilot
guards and the configured request ceiling. No provider calls were made for this review.

Expansion requires a separate explicit decision after evidence for all these gates:

1. Complete the existing **10 unique prospective session** soak, using its authoritative
   rules. Current 1/10 is not completion; retries/backfills/synthetic fixtures do not
   manufacture additional qualifying sessions. Show unattended scheduled results and
   account for each configured instrument/session without treating absence as success.
2. Demonstrate enough retained aligned history for the enabled features, including 61
   observations for RS60, with independently completed calendar proofs and reproducible
   revision/knowledge selection. Report coverage denominators and all exclusions.
3. Verify expanded membership, stable identity, ordinary-share class, board/mechanism,
   effective statuses, listing/delisting, action basis, benchmark and volume semantics.
   Resolve duplicates/symbol changes; a provider exchange catalogue is not ordinary-
   share eligibility. Listed is not analyzable.
4. Produce a request-cost/entitlement plan within the actual account grant, ceiling,
   source permissions and retention rights. No silent fallback or implicit subscription
   change. An uncertain right is unresolved, not consent to collect.
5. Before an expansion pilot begins, freeze a dated coverage/error/staleness acceptance
   target for that universe. This review does not invent “acceptable” percentages from
   seven sessions. Require zero silently omitted instruments, explicit reasons for every
   missing row, no unresolved unexplained ingestion failures in the qualifying soak,
   and benchmark coverage on every otherwise evaluable session.
6. Pass offline restore/replay, cancellation/capacity and operational-fingerprint checks
   at proposed scale. Inspect query cost rather than just raising V0.1's 10-ID limit.

**OPEN QUESTION:** expansion-scale numerical service targets and paid entitlement
are not approved here. Thus the expansion gate is not satisfied. They do not delay
implementation of the fixed pilot screen or authorize automatic promotion when soak
alone reaches 10/10.

## S. AI boundary

**FROZEN:** .NET calculates canonical features and eligibility/setup/order. PostgreSQL
and retained evidence establish facts; Python collects/parses. No LLM call belongs in
the endpoint, replay, reference verification or ranking path. The existing application
separation supports this boundary; there is no Screener AI dependency to preserve.

Future AI may explain a read-only deterministic evidence bundle, explicitly retaining
unavailable fields and provenance. It must not recompute indicators, invent missing
values, verify evidence by assertion, override eligibility/setup, reorder candidates,
present AI confidence as fact, change parameters, promote a policy or rewrite stored
historical decisions. AI explanation failure cannot change the deterministic result.

## T. Deferred features

**DEFERRED:** AI score/explanation implementation, BUY/SELL or portfolio actions,
automated stops, executable FAST_SWING policy, EXTENDED/risk modifiers, MFI, CMF,
EMA9, oscillator stacking, approximate weekly VWAP, broker/foreign/bandar flow or
accumulation, ownership reconstruction, fundamentals/valuation, themes/groups,
order book/intraday/HAKA/HAKI, broker-specific tools, prediction/ML/autonomous agents,
online learning/parameter optimization/promotion, breadth, Stock Detail, full registry
bitemporality, total-return adjustments, persistent decisions/outcomes and FullIdx.

No Redis/Kafka/message broker, microservice, Python API server, feature store, generic
strategy/plugin framework, authentication or public hosting is needed for this slice.
Do not add a new abstraction or dependency merely to make one calculation configurable.

## U. Risks and failure modes

| Risk | Required defense |
|---|---|
| Legacy RS presented as pp | Separate explicit field names, 100→110/100→105 fixture, policy ID. |
| Future bar/proof leakage through shared helper | Mandatory through argument and filtering in shared calculator plus bounded SQL; inspect every caller. |
| Today's identity/status leaking into historical runs | No mutable registry fallback; retained dated references, null/blocked when absent. |
| False continuity across missing sessions/events | Calendar-aligned sequence resets; evidence coverage gates; no forward fill. |
| Raw price × adjusted quantity presented as liquidity | Basis/units/segment clearance tied to selected revisions; null until verified; label proxy. |
| Every feature blocked because optional IHSG is missing | Separate feature availability and minimum price-setup eligibility; context UNKNOWN. |
| Weak optional data quietly improves rank | Available-before-null lexicographic ordering and visible coverage, no fabricated zero. |
| Daily confirmed rows inflate outcome sample | Stable episode lifecycle; no repeat-confirm event or daily independent return lesson. |
| Late revision changes reconstructed history | Pin cutoff/policy/input digest, retain source revisions; distinguish reconstruction from prospective decision. |
| EMA drift from request-dependent seeding | Fixed bounded anchor, explicit seed dates, no rolling request-start seed. |
| Page changes lose held rows or mix inputs | Separate held-ID list; all-held union; pinned hash/cutoff and cancellation. |
| Empty data reported as no setups | BLOCKED/NOT_EVALUATED distinct from evaluated NONE and complete empty results. |
| Pilot DEGRADED rows get labeled exchange-certified | Carry canonical quality; specific evidence gates, no wholesale promotion of source quality. |
| Manual reference file becomes unbounded platform | Fixed typed schema, fixed pilot and byte/record limits; no reference CRUD/DSL. |
| Synthetic readiness mistaken for production readiness | Dated evidence matrix and operational fingerprints; fixtures only in disposable stores. |
| Reference mutation destroys old replay | Retain snapshots/source inputs, hash selected facts, restore/replay tests; never rewrite old knowledge to pass a cutoff. |
| Bounded pilot horizon expires | Explicit unsupported-date response and new versioned review; no silently shifted seed. |

## V. Final implementation checklist

- [ ] Keep the implementation scoped to `screener-v0.1.0`, fixed pilot and one price setup.
- [ ] Add required through semantics to the shared feature entry point and all callers.
- [ ] Preserve legacy relative-performance semantics; add the distinct pp RS fields.
- [ ] Reuse one EMA/TR/Wilder engine; field-level warmup/null and basis checks match D.
- [ ] Add only the bounded quality-preserving Npgsql read and small pure Screener logic.
- [ ] Load/validate retained reference evidence with actual known-at; an empty file is honest.
- [ ] Preserve calendar/listing/status/benchmark chronology and fixed seed anchor.
- [ ] Implement eligibility precedence and every episode boundary/expiry/reset explicitly.
- [ ] Apply transparent ordering and cap, then filters/paging; union every held ID.
- [ ] Expose the exact read-only API, sparse availability reasons and input pinning.
- [ ] Keep no recommendation, hidden score, AI dependency, provider call or write side effect.
- [ ] Add the small screen using existing production components and responsive behavior.
- [ ] Run the concrete feature/state/replay/API/UI acceptance checks using disposable data.
- [ ] Re-run database-dependent baseline checks with Docker available before release.
- [ ] Verify operational history/portfolio/soak fingerprints unchanged and FullIdx rejection intact.
- [ ] Update runbook with actual tests, policy/reference identities and remaining real-data gaps.
- [ ] Do not claim production candidate readiness until the relevant Q evidence gates pass.

No production checkboxes were completed by this review; they define the next coding
milestone. Completing documentation is not equivalent to implementing these behaviors.

## W. Minimum implementation pieces and sequence

| Piece | Classification | Smallest required work |
|---|---|---|
| Canonical bars/listing/ledger/theses | REUSE | Existing immutable storage and as-of rules; no schema copy. |
| Shared feature engine | CHANGE | Required through, separate pp returns, remaining formulas and per-field evidence/readiness. Update Worker/tests at the same boundary. |
| Bounded query/reference loading | NEW | Npgsql selection with cancellation and read transaction, fixed typed local evidence snapshots; no generic repository framework. |
| Screener evaluator | NEW | Small application service/pure functions for eligibility, sequential episodes, ordering and held union. |
| API DTO/route | NEW | One GET and explicit values/reasons/provenance; reuse validation/security patterns. |
| React page | NEW / REUSE | One page in current navigation, shared components, details and existing context. |
| Tests/acceptance/docs | CHANGE | Extend current suites/harnesses, add targeted chronological fixtures and preserve operational fingerprints. |
| Migrations / result persistence / reference platform | DEFER | No migration or new result table in the initial slice. |

Implement in this order:

1. Freeze fixtures from P and implement shared through + RS semantics first. Confirm
   all existing pilot callers/tests retain explicit legacy meanings. This is the first
   concrete task; do not begin with UI mockups or data collection.
2. Implement bounded canonical/reference selection and evidence gating; prove old
   cutoffs, later corrections and missing snapshots before producing candidate rows.
3. Add pure eligibility, episode transitions and ordering/held union; execute the
   boundary and null tests. Resolve no ambiguity by adding a second setup variant.
4. Wire the GET endpoint/read transaction with counts, input pinning and cancellation.
5. Add the production screen, including honest empty/blocked/partial states. It must
   remain useful for review even while the operational pilot has zero eligible stocks.
6. Run full relevant suites, disposable acceptance/restore and fingerprint checks;
   document actual data readiness. No operational synthetic fixes to make a demo green.
7. Separately obtain missing evidence/history within existing approved collection
   policy. Decision snapshots and later episode-level evaluation require their own
   milestone. Do not tune thresholds or expand universe as a coding shortcut.

## X. Recommended implementation reasoning level

**HIGH.** The code is a small extension, but chronology, shared-call-site compatibility,
sequence resets, explicit nulls and episode boundaries interact. HIGH is appropriate
for implementing and reviewing those invariants against this fixed contract. MEDIUM
can handle a later isolated display-only change. EXTRA HIGH is not required for this
bounded slice and must not become an invitation to broaden the architecture or tune
the strategy.
