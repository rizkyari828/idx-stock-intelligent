# Research / Evaluation V0.1 — design freeze

Design frozen: **2026-10-04, Asia/Jakarta**. Decision: **GO for a subsequent,
bounded, descriptive implementation; documentation only now**. Evaluation policy
**`research-evaluation-v0.1.0`**, research projection/export schema **1**. This
document changes no Screener, Decision Snapshot or Outcome Tracking contract.

## 1. Purpose and reviewed baseline

Describe what was subsequently recorded after actual prospective observations,
with transparent population, chronology, missingness and dependence. This is
research infrastructure, not a trade simulator, recommendation, prediction,
strategy ranking, parameter optimizer or evidence of predictive validity.

Initial repository: clean `main`, **two commits ahead of `origin/main`**, HEAD
`a279ceb7a0a7deb51390505f94d1e59c082035cd` (`feat: add decision outcome history views`).
`git status`, `git log --oneline -12` and `git diff --check` were inspected; the
initial whitespace check passed. No in-repository Graphify graph was present;
the relevant source was inspected directly. The mandatory project guidance and
the following frozen contracts/runbook informed this design:

- [Screener](SCREENER_V0_1_CONTRACT.md), especially E–K and O.
- [Decision Snapshot](DECISION_SNAPSHOT_V0_1_CONTRACT.md), especially 3–7 and 13.
- [Outcome Tracking](OUTCOME_TRACKING_V0_1_CONTRACT.md), especially 2–9 and 15.
- [Product runbook](PRODUCT_SLICE_RUNBOOK.md), including capture, verification,
  Outcome Tracking and October 4 Outcome History implementation notes.

Source paths below are relative to the repository root. These are implementation
findings, not assertions that Research already exists:

| Source | Finding / design consequence |
|---|---|
| `src/IdxStockIntelligence.Application/DecisionSnapshots.cs`; `Infrastructure/DecisionSnapshotService.cs` under the corresponding project prefix | Full configured-plus-held population is projected from evaluator rows before presentation paging. Same-date/episode/hash captures can recur; request retry alone is idempotent. Captured/knowledge and recording clocks differ. |
| `src/IdxStockIntelligence.Application/ScreenerEvaluation.cs`, `ScreenerResponse.cs` | Episode identity includes policy/instrument/start date. NONE can retain an ended episode; not-evaluated NONE is distinct. Market trend/volatility and holding flags are retained. |
| `src/IdxStockIntelligence.Application/ScreenerReferences.cs` | Existing SHA-256 helper canonicalizes object keys and sorts arrays. Reuse with a deliberately normalized research projection; do not mistake it for an order-sensitive result checksum. |
| `src/IdxStockIntelligence.Application/OutcomeTracking.cs`; `src/IdxStockIntelligence.Infrastructure/OutcomeTrackingService.cs` | Four fixed horizons; only terminal rows persist. GET can assess missing cells using live retained files and return unmaterialized terminal previews. Its summary cannot serve as an as-of research summary. |
| `src/IdxStockIntelligence.Infrastructure/Migrations/0005_decision_snapshots.sql`, `0006_decision_snapshot_outcomes.sql` | Immutable captures and terminal outcomes; row composite identities; recording clocks are insertion clocks, not commit timestamps. No stored unresolved reason history or outcome-verification status. |
| `src/IdxStockIntelligence.Api/Program.cs`; `frontend/src/DecisionHistoryPage.tsx`, `OutcomePanel.tsx` | Existing loopback API, bounded reads, explicit verification/evaluation actions and outcome matrix. Reuse shell, notices, table scrolling, details and cancellation patterns; add no automatic evaluation. |

**Operational limitation, reported rather than freshly queried here:** the task
and latest runbook report one prospective run, ten captured rows, zero committed
terminal outcome rows, forty unresolved cells, soak 1/10 and FullIdx disabled.
This review did not open the operational database, run collection or evaluate
outcomes. Earlier build/test counts in the runbook are not fresh passes here.

## 2. Observation, population and horizon

**Atomic observation = `(runId, instrumentId)`**. A horizon cell adds
`horizonSessions IN (1,5,10,20)`. Four cells do not make four independent
observations. A capture row is neither a trade, entry, executable fill,
recommendation nor independent bet. FAILED describes a captured setup state,
not a failed trade or negative forward return.

Include every retained schema-1 `PROSPECTIVE_CAPTURE`, `PILOT`,
`screener-v0.1.0` run selected by the temporal/context rules below, including
COMPLETE, PARTIAL, BLOCKED and zero-row runs. Outcome binding is schema 1 /
`outcome-v0.1.0`. Unsupported versions in the requested base population fail
explicitly; do not silently discard them or mix policies. A future policy needs
an explicit new research binding and separate population.

Construct the base population from captured rows, never from the shortlist,
current registry, current universe, current holdings or an AVAILABLE inner join.
Retain configured unheld, all held-only/outside-scope, rejected, suspended,
delisted and failed-data rows. Missing current registry entries cannot remove a
row. No row is invented for a date or instrument that was never captured.
Manual capture timing and the hand-selected PILOT already create selection
effects: results describe captured observations, not all signals, all episodes
that occurred, all trading sessions or all IDX equities.

Preserve run capture/knowledge/recording clocks, through, nullable targetSession,
instrument ID/captured symbol, setup/evaluated flag/reasons, eligibility/reasons,
episode object/ID, configured/held flags, portfolio context, universe snapshot,
captured IHSG context and inputHash/selectedDigest. Preserve every cell's stored
state/reason, original anchor date/close, horizon date/close, priceReturnPct and
outcome knowledge/recording clocks. Do not recompute these from today's evidence.

The +N horizon is the stored Nth independently confirmed completed **exchange**
session strictly after targetSession. It is not N civil days or instrument bars.
Closures do not count; no-trade/suspension does not pause the exchange clock;
no endpoint sliding, same-close execution assumption, extrapolated horizon date
or replacement anchor. Existing outcome policy owns proof and comparability.
Research performs no session resolution or return recalculation.

## 3. Repeated captures, episodes and dependence

V0.1 exposes **observation mode only**, plus episode counts and raw row drilldown.
Do not collapse repeated run/instrument keys, even if targetSession, episodeId,
inputHash or prices match. A repeated capture changes observation weighting;
frequently captured instruments/episodes receive more weight in the raw mean.
This is disclosed, not repaired with invented independence or silent weights.

For each selected cohort/horizon and the overall population report:

- `N`: raw observation count.
- `L`: observations with non-null captured episodeId; `N - L`: no-episode count.
- `E`: distinct `(capturePolicyId, instrumentId, episodeId)` among those L rows.
- `L - E`: additional episode-linked captures beyond one per key.
- `E_A`: distinct such episode keys among AVAILABLE rows; and AVAILABLE rows
  lacking an episode ID, separately.
- Distinct instruments, run IDs and non-null target sessions, plus observations
  with null targetSession. Report these counts for AVAILABLE rows too.
- Additional captures of a non-null `(instrumentId, targetSession)` beyond the
  first, including rows without episodes; do not merge all null targets together.

These are diversity/dependence diagnostics, **not an effective sample size**.
The number of independent observations is **not established in V0.1**, including
when E equals N. Distinct episodes/instruments can share market shocks and
overlapping windows. An episode appearing in WATCH and CONFIRMED contributes
to both cohort-specific E values; the overall E is a distinct union, never the
sum of subgroup E. No episode IDs are synthesized for NONE/blocked rows.

An episode key is copied exactly; never rebuild it with later bars. Revisions
can change the attributes or even start identity seen by later captures.
Retain all captured variants and hashes. A single key can have different ages,
triggers and end facts; distinct keys can describe economically related events.
Neither E nor E_A certifies a stable event lineage.

**Episode-deduplicated return analysis is DEFERRED.** First WATCH estimates a
different question from first CONFIRMED. First retained capture can be late in
an episode; first inside a query window is not first prospectively observed;
choosing the first AVAILABLE would select on future missingness. NONE may
carry an expiry/interruption episode. These distinctions make a single generic
dedup switch misleading. V0.1 has no representative selection rule or mode.
A later contract must freeze event eligibility, global first-observation scope,
left truncation, portfolio duplicates, revisions and tie order before inspecting
returns. It must preserve original capture anchors and never prefer available
or favorable outcomes. The older Screener O confirmation-close/benchmark study
remains deferred; this contract neither implements it nor asserts episode IID.

+1/+5/+10/+20 share anchors and portions of their price windows. Successive
captures overlap too. Summaries stay separate by horizon; do not pool horizon
cells, average horizon means into a headline score, run paired/independent tests,
compound observations, or describe horizon differences as causal effects.
AVAILABLE subsets can differ by horizon, so even descriptive comparisons must
show each horizon's N/A/U counts beside its values.

## 4. Cohorts and permitted segmentation

Primary cohorts are a mutually exclusive, exhaustive partition of captured
rows. Freeze the following deterministic precedence; stop at the first match:

1. If eligibility == DATA_BLOCKED, assign DATA_BLOCKED.
2. Else if eligibility == INELIGIBLE, assign INELIGIBLE.
3. Else if setupEvaluated == false, assign NOT_EVALUATED.
4. Else if setup == NONE, assign NONE.
5. Else if setup == WATCH, assign WATCH.
6. Else if setup == CONFIRMED, assign CONFIRMED.
7. Else if setup == FAILED, assign FAILED.

Exactly one primary cohort is assigned to every selected captured observation.
DATA_BLOCKED and INELIGIBLE rows are not also NOT_EVALUATED when their setup was
not evaluated. Every selected observation must be classified, and the sum of
primary cohort N values must equal total selected N.

| Cohort key | Exact captured condition | Interpretation |
|---|---|---|
| DATA_BLOCKED | eligibility DATA_BLOCKED | Setup not evaluable; not a zero outcome. |
| INELIGIBLE | eligibility INELIGIBLE | Excluded from setup under captured scope/evidence. |
| NOT_EVALUATED | eligibility ELIGIBLE and setupEvaluated false | Retained unevaluated row; never evaluated NONE. |
| NONE | eligibility ELIGIBLE, setupEvaluated true, setup NONE | Evaluated NONE, including retained expiry/exit reasons. |
| WATCH | eligibility ELIGIBLE, setupEvaluated true, setup WATCH | Prospective WATCH observation. |
| CONFIRMED | eligibility ELIGIBLE, setupEvaluated true, setup CONFIRMED | Prospective CONFIRMED observation, not BUY. |
| FAILED | eligibility ELIGIBLE, setupEvaluated true, setup FAILED | Forward prices after a captured failed setup, not performance of a preceding position. |

The current engine normally emits not-evaluated rows with blocked/ineligible
eligibility. The explicit NOT_EVALUATED residual prevents a future compatible
retained row from being called evaluated NONE. Unknown enum values, impossible
evaluated blocked/ineligible states or inconsistent episode linkage are integrity
errors, not additional silently excluded cohorts.

Every cohort may have descriptive returns **if** its cells were committed as
AVAILABLE under Outcome policy. Eligibility is not an extra return gate.
NONE/INELIGIBLE/DATA_BLOCKED/FAILED are not randomized controls for WATCH or
CONFIRMED; comparing them does not estimate a strategy effect.

One optional secondary partition is allowed, never arbitrary combinations:

| `groupBy` | Fixed buckets in display order |
|---|---|
| NONE | No secondary split. |
| MARKET_TREND | POSITIVE, NEUTRAL, NEGATIVE, UNKNOWN from run.result.marketContext.trend. |
| MARKET_VOLATILITY | NORMAL, ELEVATED, UNKNOWN from captured volatility. |
| HELD_CONTEXT | HELD, NOT_HELD, NO_PORTFOLIO_CONTEXT. |
| MEMBERSHIP | CONFIGURED, OUTSIDE_CONFIGURED, MEMBERSHIP_UNKNOWN. |

Use only captured run context; do not calculate regimes from live/current/future
IHSG. Preserve captured context date/reasons. UNKNOWN stays its own bucket, even
if better evidence arrives. IHSG volatility retains its existing unvalidated
ATR% threshold; Research supplies no new regime boundaries. No cross-product of
trend and volatility, stock trend, sector, ranking, RS20/RS60, ATR%, distance to
high, volume ratio or liquidity buckets in V0.1. Continuous features may be
inspected in Decision Detail but are not research filter/group controls.

`HELD`/`NOT_HELD` apply only when the run has a portfolioId, using captured held.
No portfolio means NO_PORTFOLIO_CONTEXT even though held=false is stored.
Never reproject holdings or compare forward returns with average purchase cost.
For membership: configured=true means CONFIGURED; configured=false with a
non-null captured universeSnapshotId means OUTSIDE_CONFIGURED; otherwise
MEMBERSHIP_UNKNOWN. Held outside universe is an intersecting factual attribute,
not an additional primary row counted twice. Unknown membership is not outside.

Primary display order is the cohort table above; secondary order is fixed above.
At most seven primary by four secondary buckets = **28 cohort rows**, plus one
overall total. Include zero-count buckets for the selected partition. Distinct
counts are recomputed for totals, not summed. No return-based sorting, winner
badge, “best regime” or interactive numeric thresholds.

## 5. Knowledge cutoff and committed-only selection

One read-only REPEATABLE READ transaction covers run selection, rows, outcomes,
counts, identity and pagination. Resolve server time once. A supplied cutoff K
is an inclusive RFC3339 instant with explicit offset, normalized to UTC and no
later than that server time; omitted K becomes that time. Never resolve “now”
separately for different reads. Capture date filters use Asia/Jakarta capture
dates, not target session dates or forward endpoint dates.

K represents **application/domain knowledge chronology**, determined from the
persisted capture knowledge/recording timestamps, outcome knowledge/evaluation
timestamp (`outcomeKnownAt`) and outcome `recordedAt`. Inclusion is deterministic
for the same persisted records and normalized query; it does not reconstruct the
exact historical PostgreSQL transaction commit visibility or MVCC-visible state
at an arbitrary sub-transaction instant.

Selection order is frozen:

1. Select complete committed runs visible in this transaction, matching capture
   date range and exactly one context (discovery-only or one portfolio). Require
   capturedAt <= K, knowledgeCutoff <= K and snapshot recordedAt <= K.
   Validate policy/schema and run completeness; retain empty runs in metadata.
2. Read the complete bounded base row population. Only then apply the explicit
   cohort and optional instrument/episode drilldown. Report base, selected and
   filtered-out observation counts. Filters never redefine the base population.
3. For each selected row at the one requested horizon, LEFT JOIN committed
   terminal outcomes on the exact composite key. An outcome qualifies only if
   outcomeKnownAt <= K **and** outcome recordedAt <= K. Apply both conditions
   inside the join/subquery; a WHERE on the right side must not erase missing rows.
4. A cell with no qualifying record is **research UNRESOLVED**, reason
   `NO_COMMITTED_OUTCOME_AS_OF_CUTOFF`, with null outcome state/return/endpoint/
   knownAt/recordedAt. Retain original captured anchor facts. This is a derived
   research reason, not a new stored Outcome state or a conclusion about market
   absence. Do not reveal the existence, state, date or value of a later outcome.
5. Aggregate, hash the full selected projection, then page. No preview, provider
   lookup, mutable reference read, registry fallback or evaluation POST occurs.

Research UNRESOLVED includes not-yet-mature, missing-evidence and simply
not-yet-recorded cells. Existing PENDING/SESSION_UNAVAILABLE/UNRESOLVED reasons
are live assessments, not retained historical facts. Terminal previews, including
AVAILABLE previews, are also unresolved for this committed-only dataset. Their
rows remain in the denominator; previews cannot enter returns or resolved counts.
Do not call all these cells “waiting for the market” or invent a past preview.
Outcome History remains a separate current-assessment surface with its own
preview labels. This operational distinction implements Outcome contract 15's
requirement to account for unmaterialized observations without counting them as
immutable research evidence.

Example of chronology (synthetic): a +5 endpoint on October 12 first evaluated
October 15 is absent at an October 14 K. If evaluated at 10:00 but recorded at
10:01 on October 15, K=10:00:30 still excludes it. At a later qualifying K it
appears with its original October 12 endpoint and October 15 clocks. It never
becomes knowledge from October 12. No source-knownAt backdating or later bar
revision changes an included immutable outcome.

**Domain-chronology as-of is not a historical MVCC snapshot.** `recordedAt`
is not a database commit timestamp. A transaction inserted before K but committed
after an earlier read can appear on a later read of the same K; owner-controlled
restore/import can also alter retained membership. This design makes no claim
to reconstruct exact historical commit visibility. Dataset identity detects
such changes; pinned reads fail with 409 rather than silently change. An exact
past dataset is reproduced from its exported membership/projection, not cutoff
alone. Future exact commit-visibility tracking, if required, is outside V0.1;
no schema or implementation change is part of this clarification.

## 6. Denominators, coverage and availability bias

For **one cohort and one horizon**, using the full selected set before paging:

- `N = count(captured observations)`.
- `A = count(qualifying committed AVAILABLE outcomes)`.
- `T_anchor`, `T_data`, `T_basis` count the three qualifying terminal unavailable
  states; `T = T_anchor + T_data + T_basis`.
- `R = A + T` is resolved, meaning committed terminal, not market-calendar maturity.
- `U = N - R` is unresolved for research.
- Required reconciliation: **`N = A + T + U = R + U`**.
- **AVAILABLE coverage % = `100 * A / N`**, null if N=0.
- No single unlabeled “coverage” or “sample size”: display A/N and all five
  N/R/A/T/U counts alongside every return summary, including exports.

There is no “mature-only” denominator, imputation, zero filling, removal after
a waiting period, complete-case population rewrite or available-over-resolved
coverage substituted for A/N. Zero available with N>0 means 0% coverage and
null return metrics. N=0 means no captured observations and null coverage.
Terminal unavailability still counts as resolved and stays visible by state and
stored reason. Reason categories may not be recoded as negative returns.

Availability can be associated with suspension, liquidity, listing status,
corporate actions, data quality and operator materialization timing. Therefore
AVAILABLE-only summaries describe a selected observed subset; missingness is
not assumed random. Even 100% committed coverage does not eliminate PILOT,
capture-selection, eligibility or dependence bias, or certify source truth.
No inverse-probability weights or censoring/survival model in V0.1.

## 7. Allowed metrics and exact arithmetic

Let `x[1..A]` be the **stored** priceReturnPct values, sorted numerically
ascending, ties by canonical observation order from section 10. Each available
observation has equal weight; no per-episode, per-instrument, position-size,
market-cap or portfolio-value weighting. Units are percentage price return.

| Allowed metric | Formula / denominator |
|---|---|
| Median forward price return (primary) | For odd A, x[(A+1)/2]; for even A, (x[A/2] + x[A/2+1]) / 2; null when A=0. |
| Mean forward price return (secondary) | Sum all A stored x values in canonical observation order, then divide by A; null when A=0. |
| Minimum / maximum, in expanded summary | Minimum x[1]; maximum x[A]; null when A=0. These are observed extrema, not risk limits. |
| Positive-return proportion % | `100 * P / A`, where P=count(x > 0); null when A=0. |
| Sign counts | P=count(x > 0), Z=count(x == 0), M=count(x < 0); P+Z+M=A. |

Counts and AVAILABLE coverage use section 6. Genuine zero contributes to A and
Z, to the mean and median, and to the positive-proportion denominator but not P.
Compare unrounded decimals: a tiny positive displayed as 0.00% is still positive.
Display exact text on expansion so this is explainable. Never call P/A win rate,
hit rate, accuracy, success rate, likelihood of profit or a forecast.

Median is prominent to reduce headline sensitivity to extreme observed values;
mean and extrema plus accessible individual rows retain those tails. No
trimming, winsorizing, outlier deletion, annualization or return compounding.

Use checked .NET decimal for aggregation of the validated exact stored decimals.
Mean sum order is fixed; even median adds the two values then divides by 2;
percentages multiply integer numerator by 100m then divide. Native decimal
division precision applies; no intermediate/final quantization for analytical
values. Unrepresentable database numeric, arithmetic overflow, malformed
AVAILABLE/null state or failed reconciliation aborts with a sanitized error,
not a dropped observation, fabricated null market outcome or float fallback.

Research schema 1 transmits decimal values as invariant **G29 strings** (including
real zero as "0") or JSON null, with counts as JSON integers. This explicit
research/export convention preserves precision through JavaScript; it does not
change existing Outcome DTOs. Backend alone computes metrics. UI rounds for
display to two decimal places, with exact strings available in details/export;
display rounding never determines sign, membership, sorting or dataset identity.
No statistical package is needed.

**DEFERRED:** quartiles/P25/P75, variance/standard deviation, histograms/box
plots, episode-weighted summaries and matched cross-horizon summaries. Quartile
conventions and tiny-sample display add choices without serving the first task.
**PROHIBITED in V0.1:** confidence/credible/prediction intervals, error bars,
standard errors, bootstrapped intervals (including naive IID resampling),
p-values/significance tests, statistical-power or effective-N claims; Sharpe,
Sortino, CAGR, drawdown, profit factor, strategy equity curves and accuracy.

These are descriptive contract choices, not claims that inference is impossible
forever. Repeated captures, clustered instruments/market sessions, overlapping
horizons, non-normal tails and exploratory selection require a separately frozen
dependence-aware estimand and design before inference. Raw N or E>=30 is no
waiver. NIST explains why dependence undermines standard uncertainty estimates
in [Consequences of Non-Randomness](https://www.itl.nist.gov/div898/handbook/eda/section2/eda251.htm).
ASA emphasizes transparent reporting and rejects threshold-only interpretation
in its [2016 p-value statement](https://www.amstat.org/asa/files/pdfs/p-valuestatement.pdf).
Those principles motivate this conservative exclusion; they do not prescribe
our product thresholds or constitute financial validation.

## 8. Small samples, warnings and honest current rendering

Keep descriptive numbers visible whenever A>0, together with N/R/A/T/U, E/E_A
and diversity counts. Never make a small sample disappear. Freeze these
**display cautions, not inferential thresholds**, using A in each cohort/horizon:

| Condition | Display |
|---|---|
| N=0 | “No captured observations selected.” Coverage and return metrics are null. |
| N>0, A=0 | “No committed AVAILABLE outcomes at this cutoff.” Return metrics are null; show T and U separately. |
| 1<=A<5 | “Very small observed return sample (A=<count>).” |
| 5<=A<20 | “Small observed return sample (A=<count>).” |
| 20<=A<30 | “Limited observed return sample (A=<count>).” |
| A>=30 | “Descriptive sample; independence and predictive validity are not established.” |

The 5/20/30 bands are frozen presentation choices, not a power calculation,
normality claim, sufficient evidence standard or promotion gate. Never label
GOOD/BAD, validated, reliable, production-ready or statistically significant.
E_A and available no-episode counts remain next to A; a large A with one episode
must not visually suggest diverse evidence. These are factual per-cohort/horizon
coverage messages and sample warnings only. V0.1 has no research or strategy
maturity classification, status field or promotion state.

Required additional factual notices:

- Always: “Prospective observations, not trades. Exploratory price-return
  summaries do not establish predictive performance.”
- Always: “Captures, instruments and forward windows may be correlated.”
  When L>E: “L episode-linked observations represent E distinct captured episode
  keys; repeated captures receive repeated weight.” Show actual L/E counts.
- U>0: “U of N observations have no committed outcome visible at this cutoff;
  market maturity, evidence gaps and unrecorded assessment are not distinguished.”
- A<N: “Returns describe A of N observations (<coverage>% AVAILABLE). Missing
  outcomes may be selective.” Show T and its three-state breakdown as well as U.
- Always: “Price return excludes dividend cash and execution costs; it is not
  total return or realized portfolio performance.”
- Always: “Outcome verification is not implemented; committed results have not
  been certified by an outcome replay verifier.”

For the **reported current population**, each horizon displays N=10, R=0, A=0,
T=0, U=10, AVAILABLE coverage 0%; median/mean/min/max/positive proportion are
null. Factual copy: “No committed AVAILABLE outcomes at this cutoff.”
Across the four panels there are **40 unresolved cells and only 10 raw
observations**. Latest included outcomeKnownAt is null. Do not invent episode
counts, future horizon dates or operational return values. Read E and context
from captures during future implementation. Data collection alone cannot cure
the original capture-time anchor-clearance limitation documented in Outcome
contract 16; once horizons are proved these may become terminal unavailable.

## 9. Bounded query and proposed API

**Future GET `/api/research/outcomes`**, read-only, local-only. No endpoint is
implemented in this milestone. Use strict allowlisted, single-occurrence query
keys, positional SQL, existing error envelopes and cancellation conventions.

| Parameter | Frozen semantics |
|---|---|
| captureFrom, captureTo | Required ISO dates, inclusive Asia/Jakarta capture dates, from<=to, at most 366 civil dates, within 2026-08-24..2027-08-24 and not later than Jakarta server today. Date end translates to exclusive next local midnight. K may be earlier than the range; then only cutoff-visible rows qualify. |
| cutoff | Optional inclusive RFC3339 instant with offset, 1900-01-01..server now; omission only (not blank) resolves once. Responses return UTC and subsequent requests reuse it. |
| portfolioId | Omitted means discovery-only captures (`portfolio_id IS NULL`); otherwise exactly this nonzero UUID's captures. No mixed/all-portfolios mode. Valid absent context returns empty, without consulting today's portfolio state. |
| horizonSessions | Exactly one of 1,5,10,20; default 5. No pooled/all-horizons return metric. |
| cohort | ALL by default, or one of the seven primary cohort keys. No separate ambiguous `setup=NONE` filter. |
| groupBy | NONE by default or one secondary partition from section 4. |
| instrumentId OR episodeId | Optional, mutually exclusive exact drilldown; no lists/search/wildcards. UUID must be nonzero canonical D form; episode key must match `screener-v0.1.0/<UUID>/<ISO date>` within the existing policy window, max 128 characters. Retained-key equality only; no reconstruction. |
| limit | Integer 1..100; default 50; paged observation cells only. |
| cursor | Opaque context-bound keyset, max 512 characters; requires explicit cutoff and datasetId. |
| datasetId | Optional lowercase SHA-256 pin on first request, required after it for paging/export. Mismatch -> 409. |
| format | PAGE (default) or EXPORT_JSON; export requires explicit cutoff/datasetId and rejects cursor/limit. |

No outcome-availability, return-sign, current listing, rank, arbitrary run list,
feature threshold, result sort, policy override, SQL-like expression or FullIdx
filter. Cohort is the supported setup/eligibility breakdown; finer eligibility
reasons remain row context. Market regimes and held/membership are fixed splits,
not extra independently combinable filters. This small filter set still permits
exploratory selection: do not describe filtered comparisons as validation.

Base bounds apply **before cohort/instrument/episode filters** so drilldown cannot
hide excessive scanning. Maximum **1,000 selected runs** (including empty runs),
**10,000 raw base observations**, and therefore 10,000 cells at the selected
horizon. Enforce limit+1 overflow detection without loading all-history market
bars or per-row manifests. At most 28 cohort summaries plus total. Use existing
15-second SQL command timeout and a 60-second overall request deadline, covering
hashing and serialization; propagate client cancellation. PAGE response <=2 MiB,
full research JSON export <=32 MiB, measured UTF-8 before successful response.
Reject overflow, never truncate rows/cohorts, estimate totals or summarize a page
as a population. These are local engineering ceilings, not claims of measured
throughput or authority to expand collection. Review capacity before raising them.

Canonical observation order is `(capturedAt UTC ascending, runId lowercase D
ordinal ascending, instrumentId lowercase D ordinal ascending)`; all cells share
the requested horizon. Cursor carries the last key and datasetId; revalidate
against normalized context on each request. Summaries and datasetId always cover
the whole selected population, not page contents. Each page recomputes the bounded
read; no server cursor store, cache, new table or migration is required.

Response sections:

- `dataset`: schema/evaluation/capture/outcome versions; normalized query and
  resolved cutoff; datasetId; complete selected base run-ID manifest with original
  clocks/inputHash/selectedDigest/rowCount; base/selected/filtered-out counts and
  empty-run count. No current registry labels or evidence manifests.
- `freshness`: latest included base capturedAt; latest included outcomeKnownAt
  and outcome recordedAt among selected qualifying cells (null if none);
  unresolved count; generatedAt separately marked response time, not knowledge.
- `summary` and `cohorts`: denominator counts, state breakdown, coverage, allowed
  metrics, sign/episode/diversity counts and warning codes/text. Base primary
  cohort counts remain available even after a cohort/drilldown filter.
- `observations`: bounded research projection from sections 2/5, with captured
  context, episode and all nullable committed outcome facts, plus cohort/split
  keys and research resolution. Original source state is null for unresolved.
- `page`: limit, returned count, total selected N and nextCursor.

200 includes empty, wholly unresolved and wholly terminal-unavailable datasets.
400 `RESEARCH_QUERY_INVALID` covers malformed/unknown/duplicate/conflicting keys,
invalid dates/IDs/enums/bounds and unsupported alternate scope requests.
409 `RESEARCH_DATASET_CHANGED` means a supplied pin no longer matches; restart
with explicit refreshed context, never silently fall back. 409
`RESEARCH_POLICY_VERSION_UNAVAILABLE` covers retained unsupported policy/schema.
503 `RESEARCH_BOUND_EXCEEDED` covers runtime population/byte bounds; 503
`RESEARCH_UNAVAILABLE` covers database/deadline/typed-integrity/numeric failures.
Errors contain no SQL, private notes, credentials or filesystem paths. No partial
successful summaries, synthetic unavailable rows on service error, or operational
POST triggered to make the query complete.

## 10. Dataset identity and export

Define datasetId as lowercase SHA-256 of a versioned **research projection**,
using `ScreenerReferences.Hash` with `omitContentHash=false`. This is dataset
identity, not a replacement for capture inputHash, selectedDigest or verification.
Persist none of it in the database in V0.1.

The hash preimage contains:

1. Research projection/schema and evaluation policy; explicitly bound capture
   policy/schema and outcome policy/schema; frozen committed-only inclusion,
   cohort, grouping, ordering and arithmetic rules identified by that evaluation
   policy version.
2. Full normalized semantic query: UTC cutoff, Jakarta date range, explicit
   discovery/portfolio context, horizon, cohort, groupBy and nullable drilldown.
3. All selected base run IDs, their original capture/knowledge/recording dates,
   policy/universe/context/status/rowCount and unchanged inputHash/selectedDigest;
   base primary counts and selected/filtered-out/empty-run counts.
4. The complete selected observation projection, before paging: each composite
   key and all returned captured/context/episode/cohort fields, each qualified
   outcome's key/policy/schema/values/state/reason/clocks, or explicit research
   unresolved marker with null outcome fields. Hash values, not just outcome keys.

Normalize UUIDs to lowercase D, dates to ISO, every timestamp to UTC round-trip
`O` text, every decimal to invariant G29 text, enums exactly, explicit nulls
distinct from empty arrays/strings and zero. Normalize omitted query defaults
before hashing. Strings otherwise retain exact captured content. Give ordered
records and reason arrays explicit zero-based `ordinal` properties in the hash
projection so the existing helper's array sorting cannot discard meaningful
ordering. Hash the full named projection object; no concatenated ambiguous keys,
unordered-set replacement of reasons or new generic canonicalization library.

Exclude request/generated time, limit/cursor/format, supplied datasetId, UI
rounding, transport metadata and derived summaries (already fixed by projection
and policy). Same projection in another page/format or equivalent UTC offsets
has the same identity. Cutoff/query changes change identity even when numerical
returns happen to match. A qualifying new outcome changes identity; later-only
outcomes must not change the same historical K dataset. Every returned decimal
and order must be reproducible from the full projection and frozen policy.

**Read-only full JSON export is included in the future minimum scope; CSV is
deferred.** EXPORT_JSON returns one deterministic, ordered, lossless envelope
containing the complete hash preimage, datasetId, full selected observation cells,
summary/coverage/warnings and capture/outcome version metadata. Store each run
context once and each observation once in the envelope's preimage, referenced by
its stable key; do not duplicate the full row set in another export field. Use fixed
property ordering and UTF-8 without volatile export timestamps for repeatable bytes. No
page cursor or partial export; identical dataset/policy must yield identical
export bytes. A client download saves a file only on explicit user action; the
server creates no stored research dataset or archive side effect.

Keep symbols/IDs, factual captured held/context flags and selected hashes;
omit private thesis text and thesis metadata, event notes, portfolio name,
shares/cost/average cost, manifests, archive paths, credentials, raw payloads and
feature arrays. Original captured detail remains in the existing authorized
Decision Detail. Hashes may reflect private source chronology without revealing
its text; they are provenance, not anonymous identifiers. Export remains inside
the approved private pilot use; this contract grants no redistribution rights.

The export reproduces this **selected analytical dataset**, including missingness;
it is not a backup of all evidence and cannot prove external prices by itself.
Retain ordinary database/reference backups for later outcome replay verification.
An exported small drilldown need not reconstruct excluded observations' returns;
it includes base accounting and exact selected membership. A changed database
cannot silently rewrite an already saved export. Read-time pin mismatch is an
honest limit of cutoff-only regeneration, not a reason to backdate or write rows.

## 11. Proposed dashboard and visualization decisions

Add one later Research page to the existing production shell. State “PILOT /
prospective captured observations / committed outcomes only.” Apply capture
range, known-by instant, explicit discovery/one-portfolio context and one horizon.
Show all normalized filters with datasetId, latest capture/outcome clocks and
unresolved count. The four horizon choices are tabs/separate requests sharing an
explicit cutoff; each retains its own datasetId and denominators.

Primary table: **Cohort | Observations N | Resolved R | Available A | Terminal
unavailable T | Unresolved U | AVAILABLE coverage A/N (%) | Median price return
| Mean price return | Positive proportion P/A (%)**. Show E/E_A and available
no-episode count beside sample details. Expand for sign counts, extrema, distinct
instruments/sessions/runs and terminal state/reason details. Keep one overall row,
fixed cohort order and explicit secondary bucket labels; no leaderboards.

The paged observation table is the required distribution inspection: show exact
returns, dates, states and capture links, including all tail observations and
unresolved rows. **Histogram, box plot and dedicated chart-ready distribution
payload are deferred** along with quartiles, rather than choosing bins after
seeing returns. No normal curve or misleading uncertainty bands. **Time-series
views, rolling averages, smooth performance lines and equity curves are deferred.**
Capture/target/endpoint dates remain available for factual row inspection/export.

Use existing notices/details and accessible horizontal table scroll, keyboard
focus, text state labels, busy/error/empty treatment and request cancellation.
Changing draft context invalidates old results; late responses cannot paint over
new filters. Show service errors as errors, never empty evidence. An export is
the full pinned query, not the visible page. No price/action colors or badges
suggesting recommendation, no slider, optimization control or auto-evaluation.

## 12. Verification, parameter changes and AI boundary

**Committed outcomes may be included without an Outcome Verification dependency.**
The existing verifier is Decision Verification; MATCH reproduces the capture,
not the forward return. No persisted outcome-verification field exists today.
Expose the dataset-level limitation from section 8; invent no per-row MATCH,
UNVERIFIED or verification date and make no auto-verifier calls. Research checks
typed storage consistency but is not a new evidence replay engine. An integrity
failure aborts the query, rather than improving a summary by removing bad rows.

Recommend Outcome Verification V0.1 before building Research interpretation,
then Research typed query/aggregation, read API/identity/export, Dashboard and
later explanatory AI. This is implementation sequencing only, not a new
verification-dependent inclusion rule. It gives the integrity chain:
Decision Snapshot -> Decision Verification -> Outcome Tracking -> Outcome
Verification -> Research Evaluation. A later verification-dependent research gate
needs a versioned contract defining verdict chronology, denominator treatment
and mismatches; it cannot quietly reinterpret these datasets or delete outcomes.

Evaluation data and future proposals are separate. Freeze Screener lookback,
WATCH band/lifetime, confirmed lifetime, failure threshold and regime semantics
under their existing policy. No parameter search, return-driven bucket boundary,
automatic optimization, policy promotion or “accuracy” metric is allowed here.
A hypothesis suggested by these data is exploratory. A change needs a new
Screener policy ID, a separately recorded proposal and evaluation specification
before new prospective captures. Do not reuse the inspected sample as validation;
future holdout collection must account for overlap with development windows
under its own frozen separation rule. V0.1 does not silently run a walk-forward
optimizer or retroactively recapture history.

No benchmark-relative or excess returns from current live IHSG. Such research
requires its own prospective benchmark outcome evidence contract. Captured IHSG
regime labels are context only. No subtraction of hypothetical fees/slippage to
call these strategy returns. Executable entries/exits, lots, tradability,
fees/taxes/dividends, realized P&L and net total return require a separate trade
simulation/accounting contract.

AI remains optional explanatory-only future work, with no implementation now.
Any future explanation must quote deterministic cohort/horizon/cutoff, A/N,
metrics, missingness and dependence caveats. It may not invent absent values,
select trades, rank policies, suppress warnings, change the dataset, optimize
parameters or promote rules. Unavailable AI leaves all research results intact.

## 13. Future acceptance plan

These are frozen implementation checks, **not tests implemented or executed by
this design review**. Use standard root `rtk proxy dotnet test`, existing owned
disposable PostgreSQL/HTTP harnesses and frontend discovery/build/browser checks.
No operational fixtures, new testing framework or executable-runner substitute.

| Check | Required evidence |
|---|---|
| Denominators | Synthetic N=8: AVAILABLE values [-10,0,5,15], one of each unavailable terminal state, one unresolved -> A=4,T=3,R=7,U=1, coverage=50%, mean=median=2.5%, positive=50%, min=-10,max=15,P=2,Z=1,M=1. All state counts reconcile. |
| Zero/empty/fully missing | A real zero remains available. N=0 -> null coverage/returns and factual no-observations copy; N>0,A=0 -> null returns and “No committed AVAILABLE outcomes at this cutoff,” with T/U shown separately. Current reported shape: N=10,A=0,T=0,R=0,U=10 and coverage=0% per horizon; forty cells, ten observations. No maturity state. |
| Precision/formulas | Odd/even median, sorted ties, canonical sum order, repeating decimal division, exact G29 round-trip, zero/negative-zero normalization, tiny positive displayed as 0.00 still P, no JS aggregation; out-of-range/overflow fails explicitly without dropping a row. |
| Repeated captures | Monday/Tuesday/Wednesday same episode -> N=3,L=3,E=1,L-E=2; three unresolved still count. Repeated same-session NONE rows trigger same-session diagnostic even with E=0. No representative chosen from AVAILABLE rows. |
| Episodes and horizons | WATCH -> CONFIRMED -> FAILED/ended NONE retains original keys/attributes. Changed episode lineage not silently merged; null episode separate. E across cohorts is distinct union, not sum. Four horizons are never independent-N or pooled-return observations. |
| Cohort partition | Exercise the exact first-match precedence for all seven cohorts, including DATA_BLOCKED/INELIGIBLE with setupEvaluated=false remaining solely in their eligibility cohort. Assert exactly one cohort per observation, all selected observations classified, and sum of cohort N equals total selected N. Evaluated NONE versus not-evaluated NONE, FAILED and empty buckets remain explicit; impossible retained states fail. |
| Held and membership | Configured-and-held counted once; known held outside universe retained; unknown universe stays MEMBERSHIP_UNKNOWN; discovery held=false -> NO_PORTFOLIO_CONTEXT; no portfolio mixing or live holdings replay. |
| As-of chronology | Outcome knownAt<=K but recordedAt>K excluded; capturedAt<=K but snapshot recordedAt>K excluded. Boundary equality included. Same persisted chronology and normalized query produce deterministic inclusion. Later qualifying cutoff exposes original stored outcome; earlier cutoff exposes none of its fields. Jakarta midnight/date ranges and equivalent UTC offsets covered. No exact historical MVCC/commit-visibility claim. |
| No preview leakage | A live AVAILABLE terminal preview without a qualifying committed row remains research unresolved. No live evidence/Outcome read evaluator calls; changing files, calendar proofs, current registry or future bar revisions does not change the pinned dataset. |
| Survivorship/selection | Delisted/suspended/missing registry IDs, blocked rows, below-shortlist rows and empty runs persist in base accounting. Cohort/instrument filters show base/selected/filtered counts; no AVAILABLE or current-listing population filter. |
| Commit visibility limit | Hold transaction across an earlier read with insertion clock<=K, then commit: later membership changes hash; pinned page/export ->409. Simulated owner restore/import mismatch also cannot silently change a pinned dataset. Do not claim historical commit-time reconstruction. |
| Context/buckets | Captured UNKNOWN stays UNKNOWN after later IHSG updates. Only one allowed secondary partition; no optimized buckets, regime recomputation or filter combinations beyond this contract. |
| Samples/copy | A=0,1,4,5,19,20,29,30 boundaries; large A/one E_A still shows dependence; T/U warnings independent. Sample warnings are not research maturity or strategy-validity states. No maturity classification, inference, recommendation, ranking, win/hit/accuracy or optimization output fields/UI labels. |
| Identity/export | Same values with different decimal scale, timestamp offset, query defaults, page/limit/format -> same hash; new visible value/state/query/cutoff -> different hash. Future-only append at same K unchanged. Ordered reasons with ordinals affect identity; explicit null differs from zero. Full export deterministic and lossless, including missingness, no private text/paths. |
| Bounds/API | Exact 366 dates/1000 runs/10000 rows/28 buckets/100 page rows pass; +1 exceeds proper limit. Empty runs count against run cap; filters cannot bypass base cap. Unknown/duplicate params, malformed IDs/cursor, future K/date, mixed filters, FullIdx, oversize bytes, timeout/cancellation return specified errors. |
| Pagination/concurrency | Stable ascending keysets, full-query summaries independent of page, context/cutoff/hash binding, no duplicate/skipped page rows, consistent read-only transaction; query change invalidates cursor. No silent refresh on 409. |
| UI | Empty/all unresolved/all unavailable/mixed/tiny-tail samples; exact values accessible, coverage always adjacent, state text/keyboard/focus and horizontal scroll at 1440/1366/1280/1024/768/390; stale requests aborted; GET-only traffic and explicit full pinned export. |
| Safety | Operational tables/files unchanged by Research GET/export; no capture/outcome writes, live evidence reads/fetches, provider calls, migrations, policy changes, soak qualification or FullIdx enablement. |

## 14. Persistence, limits and implementation order

**No new persistence or migration.** Compute bounded queries from existing
Decision Snapshot plus committed Outcome Tracking rows. .NET owns aggregation;
PostgreSQL supplies immutable records. No materialized research table, background
job, scheduler, cache service, Redis/Kafka, statistical package, provider access,
AI call or Python analysis engine is justified by this scope.

No unresolved product choice blocks this design. Known external/operational
limits remain: sparse prospective evidence, original anchor proof, future session
and whole-span comparability, selection bias, unverified outcomes and unmeasured
query cost. Missingness stays explicit, overflow fails closed, and no number of
captures upgrades these limits automatically. A statistically independent sample
count, event-representative analysis and precise historical commit visibility
remain unanswered capabilities, explicitly outside V0.1.

After this contract is frozen, recommend these separately requested milestones:

1. Outcome Verification V0.1: verify immutable Outcome Tracking evidence before
   building analytical interpretation, following its separately reviewed contract.
   This task does not implement the verifier or add a research inclusion gate.
2. Research typed query / deterministic aggregation: implement the smallest
   query/projection and pure aggregation rules, with denominator/precision/correlation
   fixtures; reuse existing hashing.
3. Research read API / dataset identity / export: add one bounded stored-only
   read transaction and identity/pinning/export path; prove cutoff, late commit,
   population, timeout and byte limits in disposable
   HTTP/database tests. No schema change or calls to preview evaluators.
4. Research Dashboard: add the existing-shell table, warnings, horizon controls,
   row details and explicit JSON export; verify empty/sparse data and accessible
   browser behavior.
5. Later explanatory AI: optional explanations of deterministic facts and caveats,
   under section 12's unchanged boundary and a separately requested milestone.

Run appropriate canonical discovery/build and disposable acceptance for each
implementation milestone; report actual results and unchanged operational
fingerprints. A read-only production check must not materialize outcomes to make
a screenshot interesting. Any later episode, inference, benchmark, feature-bucket,
simulation or policy proposal needs its own review.

Research design and later implementation do not depend on soak completion.
**FullIdx remains DISABLED, soak remains 1/10, `screener-v0.1.0` and
`outcome-v0.1.0` remain unchanged.** This milestone edits only this contract and
a runbook note. No code, API/UI, migration, data, dependency or outcome/capture
change is part of this task. Finalization stages and commits only these two
documentation files; no push.
