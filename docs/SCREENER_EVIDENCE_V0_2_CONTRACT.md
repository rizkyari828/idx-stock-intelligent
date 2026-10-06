# Screener Evidence V0.2 — frozen normative contract

Design frozen: **2026-10-06, Asia/Jakarta**. Reviewed baseline: `main` at
`9c1232d1a821031346dde9b0df50fc35b6d1fea2`, clean working tree.

Policy/schema identity: **`screener-evidence-v0.2.0`**.

This document is the canonical normative contract for Bounded Personal Screener
Evidence V0.2. It freezes evidence admission, validity, chronological selection,
the eligibility/readiness funnel, and deterministic conflict precedence. It
supersedes the accepted V0.2 correction reports as the normative source. It is
**documentation only**: it authorizes no implementation, migration, adapter,
provider integration, formula change, or V0.1 behavior change.

V0.1 (`screener-v0.1.0`) remains unchanged and authoritative for its own path.
Where this contract and V0.1 disagree, V0.1 governs V0.1 artifacts and this
contract governs `screener-evidence-v0.2.0` evaluation only.

Terms: **FROZEN** = a chosen requirement. **DEFERRED** = outside V0.2.
**UNKNOWN**, **PARTIAL**, **STALE**, **CONFLICTING**, **UNRESOLVED** are preserved
states, never converted to a value, zero, or confidence score.

## 1. Scope and inherited invariants

V0.2 freezes the HYBRID evidence model and the `YES_WITH_ADVISORY_ONLY_LIMITS`
posture. It inherits, without reinterpretation, the V0.1 non-negotiable data
principles: missing means `UNKNOWN`, not zero; stale values retain their original
source date; corrections append revisions; replay uses only information available
at its cutoff; candles are never fabricated; pre-listing absence is not a data gap;
suspension is not a flat tradable candle; holiday, no-trade, suspension, and
missing-provider-row are distinct; provider units/segments/adjustments/revisions
must be explicit; deterministic decisions precede LLM explanation.

V0.2 preserves existing V0.1 formulas, seeds, and thresholds unchanged (EMA20/50,
Wilder ATR14, priorHigh20/priorLow20, distanceToHighPercent, RS20/RS60, WATCH/
CONFIRMED episode rules, volatility flag). It changes only evidence admission,
validity, gating, and conflict precedence — the subjects of this contract.

## 2. Evidence quality model

Per-dimension quality is exactly:

- `VERIFIED`
- `PARTIAL`
- `UNKNOWN`
- `CONFLICTING`
- `STALE`

The factual value and its evidence quality are separate fields. `VERIFIED` applies
only to the specific claim and effective scope; it never means "the whole
instrument is verified". Every admitted fact retains: source identity, effective
scope, publication/release time when available, retrieval time, local
known/admitted time, derivation references, and revision relationships where
applicable.

## 3. Provider-neutral source admission

### 3.1 Authority tiers

- **T1 GOVERNING** — the legally operative exchange/regulator act, or the
  issuer/registrar/central-depository record for the exact claim.
- **T2 ADMITTED_REFERENCE** — a contract-authorized reference source explicitly
  admitted for the claim's scope.
- **T3 PROVIDER_OBSERVATION** — an admitted market-data source's own observation of
  its feed, within its documented endpoint/field/version scope.
- **T4 DERIVATION** — deterministic local computation from admitted T1–T3 facts;
  never used for facts requiring external authority.
- **T5 SECONDARY** — secondary disclosure; may locate or corroborate, never the
  sole basis for `VERIFIED`.

No vendor is hardcoded into financial meaning. Any example provider name is
illustrative only.

### 3.2 Admission matrix

For every dimension: acceptable authority class, minimum required fields,
effective scope, chronology requirements, whether a single positive observation
suffices, whether continuing coverage is required, whether deterministic
derivation is allowed, and the resulting evidence state when requirements are
incomplete.

| Dimension | Min authority | Minimum required fields | Effective scope | Single obs sufficient | Continuing coverage | Derivation | Incomplete → state |
|---|---|---|---|---|---|---|---|
| stable instrument identity | T1/T2 | stable local ID, source code, identity interval, source ref | identity interval (code ↔ stable ID) | Yes for a closed interval | Only open-ended current | Mapping from admitted stable code only | `UNKNOWN` (`REFERENCE_NOT_KNOWN`) |
| security type (ordinary/index/other) | T1/T2 | type classification, effective interval, source | per identity interval | Yes if explicitly authoritative | No | **No** (never from suffix/registry label) | `UNKNOWN` (`TYPE_UNKNOWN`) |
| currency | T1/T2 | currency code, effective interval | per identity interval | Yes | No | Yes from admitted metadata only (never from magnitude) | `UNKNOWN` |
| listing coverage | T1, or T2 reviewed issuer doc | effective listing date, source, retrieval/known time | per identity | Yes for the listing fact | No | **No** (never from earliest bar) | `UNKNOWN`/`PARTIAL` (`LISTING_UNKNOWN`) |
| delisting | T1 | effective delisting date, source | per identity | Yes | No | **No** (inactivity ≠ delisting) | absent ⇒ `UNKNOWN` continuing, not "still listed" |
| board/regime | T1/T2 | board code, effective interval, source | per identity | Yes | Yes (effective until change) | **No** | `UNKNOWN` (`BOARD_UNKNOWN`) |
| board change | T1/T2 | from/to board, effective date, source | per identity | Yes | No | **No** | `UNKNOWN` |
| exchange rule version | T1/T2 | rule version, effective interval | market/exchange scope | Yes per version | Versioned | **No** | `UNKNOWN` |
| mechanism exception | T1/T2 | exception type, instrument/market scope, effective interval | declared scope | Yes | Yes (effective until terminated) | **No** | `UNKNOWN` (`MECHANISM_UNRESOLVED`) |
| suspension | T1 | effective start, scope | instrument/scope | Yes | Yes (open-ended until termination) | **No** (never from absent/zero bars) | `UNKNOWN` |
| reopening | T1 | effective reopening date, scope | same scope as suspension | Yes | No | **No** | `REOPENING_UNCONFIRMED` (suspension persists) |
| scheduled session | T1 calendar, or T2 with independent open/closure proof | date, status, source, known_at | exchange + date | Yes per date | Per date | Weekend default only; never weekday-open | `SESSION_UNCONFIRMED` |
| completed session | T1/T2 independent proof | date, completion proof, `completed_at`, source, `known_at` | exchange + date | Yes per date | Per date | **No** (a bar never proves a session) | `SESSION_UNCONFIRMED`/`SESSION_UNAVAILABLE` |
| corporate action | T1 (issuer/exchange/registrar) or T2 | action type, effective/ex date, ratio/basis, source | event + instrument | Yes for the event | Coverage claim requires completeness | **No** (never invent adjustment factors) | `UNKNOWN`; partial coverage blocks `CLEARED` |
| source price convention | T2/T3 documentation + local version | endpoint/field/version and documented semantics | exact provider/endpoint/field/version | Yes for the documented convention | Versioned | **No** | `PRICE_BASIS_UNVERIFIED`/`VOLUME_BASIS_UNVERIFIED` |
| genuine price observation | T3 (direct observation) | source ID, instrument ID, session/date ID, actual OHLC, retained provenance/revision, valid invariants | exact source + session + instrument | Yes if all §6 conditions hold | No | **No** (synthetic/carry-forward forbidden) | not admitted |
| price-comparability clearance | T2/T3 convention + T1 actions + T4 | continuity, currency/unit, raw convention, session continuity, action coverage, revision binding | exact segment/window + selected revisions | **No** (requires coverage) | Yes for the window | Only deterministic | `UNRESOLVED` |

## 4. Evidence validity and freshness

Validity follows evidence semantics and effective scope. **There is no global
"valid for N days" rule.** A fresh retrieval timestamp never extends substantive
validity.

### 4.1 Continuing effective state
Suspension, reopening, delisting, board transfer, explicit mechanism exception.
Continues only according to its defined effective scope. A continuing suspension
does **not** become `STALE` merely because the notice is old. Transitions:
`VERIFIED` while the effective scope covers the evaluated point; `STALE` only when
its own scope is exhausted/superseded without a covering successor; `UNKNOWN` if
start/scope is unproved; `CONFLICTING` if incompatible effective states overlap the
same scope/time with no precedence winner. Termination requires affirmative
terminating evidence in the same scope.

### 4.2 Point observation
Current roster/status snapshot, provider instrument metadata. Proves only the
stated point/scope unless source semantics establish continuation. A fresh
retrieval does not extend it. Transitions: `VERIFIED` for the point/scope from
`known_at` forward; `STALE` when the evaluated scope is later than the proven scope
with no continuation; `PARTIAL` for subset coverage; `UNKNOWN` if none visible at
cutoff; `CONFLICTING` for irreconcilable same-scope/time points with no precedence.

### 4.3 Versioned rule
Trading-mechanism or board rule. Applies per effective version interval.
`VERIFIED` within interval; `UNKNOWN` if no version covers the evaluated time;
`CONFLICTING` for overlapping contradictory versions; `STALE` when superseded
without a covering version.

### 4.4 Session fact
Applies only to the specified exchange/session/date. `VERIFIED` for that date with
completion proof; `PARTIAL` if completion unproved; `UNKNOWN` if unsourced. A
passed session fact does not expire; `STALE` is not meaningful for it.

### 4.5 Source convention
Raw OHLC semantics, adjustment behavior, quantity semantics. Applies only to the
documented provider/endpoint/field/version scope. `VERIFIED` within scope;
`UNKNOWN` if field/version differs; `CONFLICTING` if behavior changed without a
version boundary; `STALE` when deprecated/superseded without a covering version.

## 5. Chronology and point-in-time safety

Keep separate clocks: **effective time**, **publication/release time**,
**retrieval time**, **known/admitted time**, **recorded time**. `known_at` is never
earlier than retrieval. No future evidence may affect an earlier cutoff. Later
corrections apply only at later valid knowledge cutoffs. A September-effective fact
entered in October cannot be assumed known in September. Recording timestamps are
not database commit timestamps.

## 6. Genuine price observation

Positive volume is **not** a universal requirement for price admission. An
observation is admitted only if **all** hold:

1. the record came from an admitted market-data source within scope;
2. instrument identity is unambiguous;
3. session/date identity is unambiguous, with independently supported completed
   session;
4. OHLC fields are actual provider observations, not system-computed;
5. provenance/revision is retained;
6. OHLC invariants are valid (finite, positive raw prices where required,
   internally consistent);
7. the value is not a carried-forward synthetic record;
8. the value is not a placeholder/sentinel;
9. no older price has been silently substituted (exact selected revision binding);
10. the source price convention for the field/version is documented.

Volume-unit evidence is **not** required to validate price when price authenticity
is independently proven.

**Zero volume.** `volume = 0` may be admitted **only** when provider/source
semantics positively distinguish a genuine zero-volume observation from a
synthetic/carry-forward bar (for example an explicit no-trade/zero-quantity flag,
or a documented convention stating zero means "no execution", combined with an
independent completed-session proof). Where the provider cannot distinguish the
two, the observation fails closed and is not admitted.

**Synthetic / carry-forward rejection.** Detected via source semantics, source
revision identity, and/or an explicit provider flag — never volume magnitude alone.
A rejected record never becomes an admitted observation.

## 7. Trading status

`tradingStatus` is governed **only** by admitted authoritative status evidence.

- Exact authoritative target-session status evidence whose effective scope covers
  `T` may establish `STATUS_TRADING` / target-session eligibility.
- A genuine price observation is a **separate fact**:
  `genuinePriceObservation = VERIFIED` / `TRADED_OBSERVED_AT_T` for that
  instrument/session. It may corroborate trading activity but **cannot** prove:
  uninterrupted trading status, absence of an unknown suspension, mechanism
  continuity, or next-session tradability.
- A genuine bar does **not** prove trading status.
- "No suspension notice found" does **not** imply normal trading.
- An earlier `TRADING` observation plus incomplete subsequent event coverage does
  **not** prove current `TRADING`.
- An effective continuing suspension persists according to its effective scope and
  establishes `SUSPENDED` / `INELIGIBLE` for that scope.
- Reopening requires affirmative evidence; an open-ended suspension terminates only
  on affirmative reopening. A stated end passed without affirmative reopening yields
  `REOPENING_UNCONFIRMED`.
- `PARTIAL`, `UNKNOWN`, `STALE`, or `CONFLICTING` required status blocks
  `MARKET_ELIGIBLE`; therefore `setupEvaluated = false`.

## 8. Session handling

Scheduled and completed sessions are distinct. A completed session requires
independent completion proof with `completed_at`; a bar never proves a session.
Unknown weekdays break continuity; known closures are skipped. Missing/zero-volume/
inconsistent rows cannot manufacture a session. Distinguish pre-listing absence,
holiday, no-trade, suspension, and missing-provider-row.

## 9. Board and mechanism

Board and trading mechanism are separate facts. Verified supported boards are a
frozen conservative product boundary; unsupported regimes are `INELIGIBLE`; unknown
is `DATA_BLOCKED`. Main-board identity alone does not override an unsupported or
unknown current mechanism. Conflicting board facts → `CONFLICTING` /
`MECHANISM_UNRESOLVED`, blocking the affected scope.

## 10. Price comparability

Frozen exactly: `CLEARED`, `KNOWN_BREAK`, `UNRESOLVED`.

- **CLEARED** applies to an exact calculation segment/window and exact selected
  revisions, requiring sufficient evidence for: instrument/identity continuity,
  currency/unit continuity, raw-price convention, required session continuity,
  applicable corporate-action continuity, and local bar revision binding. Partial
  action coverage **never** produces `CLEARED`.
- **KNOWN_BREAK** is a positively known incompatible event inside the required
  comparison/seed segment: split, reverse split, relevant rights/bonus/share event,
  conversion, merger/reorganization. No adjustment factors are invented.
- **UNRESOLVED** means required continuity/action/source evidence is insufficient or
  conflicting. A separately proven sub-window may still be `CLEARED`.

`KNOWN_BREAK` inside an active required segment resets/restarts affected
calculations. `UNRESOLVED` prevents affected dependencies from reaching
`DATA_READY`. A break never bridges.

## 11. Technical window semantics

A `KNOWN_BREAK` inside any active seed/recurrence segment requires restart and
warmup from a subsequently `CLEARED` segment. The actual seeded recurrence is used,
not merely the nominal latest N observations. No break is silently bridged.

| Feature | Window/seed | Break behavior |
|---|---|---|
| `ema20` | 20-bar SMA seed, then `E += (2/21)(C−E)` | Break in seed/active recurrence resets; `WARMUP` until 20 post-break cleared bars |
| `ema50` | 50-bar SMA seed, then `E += (2/51)(C−E)` | Same; `WARMUP` until 50 post-break cleared bars |
| `atr14` | 14 TRs (15 bars) seed, then Wilder `A += (1/14)(TR−A)` | Break in seed/recurrence resets; `WARMUP` until 15 post-break cleared bars |
| `priorHigh20` / `priorLow20` | previous 20 consecutive valid sessions | Break in window → unavailable until 20 consecutive post-break valid sessions |
| `distanceToHighPercent` | current close vs `priorHigh20` | `null` until `priorHigh20` available |
| `rs20Pp` | 21 aligned cleared stock + benchmark sessions | Break on either side → reset/warmup |
| `rs60Pp` | 61 aligned cleared stock + benchmark sessions | Break on either side → reset/warmup |

Reset/warmup is a data/feature readiness matter; it gates `DATA_READY` /
`TECHNICAL_EVALUATED` and therefore `setupEvaluated`, never `MARKET_ELIGIBLE`.

## 12. Optional feature boundaries

| Feature | Required evidence | Failure scope |
|---|---|---|
| relative volume | comparable quantity semantics across its window | feature-level `UNAVAILABLE` |
| monetary liquidity proxy | compatible raw price × quantity plus currency/unit evidence; explicitly a **proxy**, never actual traded value | feature-level `UNAVAILABLE` |
| actual traded value | admitted traded-value semantics | `UNAVAILABLE` until admitted |
| `rs20Pp`/`rs60Pp` | independently valid stock and benchmark windows with aligned sessions | missing benchmark affects RS/context only |
| market context | independent benchmark window | missing benchmark must not globally invalidate otherwise supported stock-only features |

Feature state is exactly `AVAILABLE`, `WARMUP`, `UNAVAILABLE`, each with an explicit
reason. Missing optional evidence must not globally `DATA_BLOCK` an otherwise valid
stock; only hard eligibility dependencies (identity, listing, board/mechanism,
status, session, price authenticity) may block.

## 13. Conflict, correction, and revision precedence

Deterministic precedence for the same claim/scope/time (first decisive rule wins):

1. effective scope match;
2. cutoff visibility (`known_at ≤ cutoff`, retrieval ≤ cutoff);
3. authority tier `T1 > T2 > T3 > T4 > T5` among already-visible claims;
4. effective specificity (instrument-specific over market-wide);
5. correction chronology (later effective `known_at` supersedes prospectively);
6. revision identity (latest `revision_number` within the same source/scope);
7. otherwise `CONFLICTING`.

A generic "newest row wins" is **never** used; later retrieval does not by itself
mean greater authority, and source authority does not override chronology.

Dispositions:

1. Two authoritative sources disagree → retain both; `CONFLICTING`; block only the
   affected dimension/window.
2. Authoritative source vs provider metadata → authoritative wins for the claim;
   provider metadata retained `PARTIAL` for its scope; if the provider is the only
   source, block.
3. Old fact vs later correction → correction applies only from its own `known_at`.
4. Original notice vs amendment → amendment applies only from its `known_at`;
   original retained; purely additive amendments combine only where scope is explicit.
5. Explicit cancellation → terminates the fact from cancellation `known_at`; prior
   interval retained; afterwards `UNKNOWN` unless another fact covers.
6. Current snapshot vs retained historical event → historical event remains for its
   effective interval; snapshot applies forward from its `known_at`; overlap
   conflict → `CONFLICTING`; the past is never erased.
7. External fact vs local deterministic derivation → external authority governs
   externally-governed facts; derivation governs arithmetic; contradiction →
   `CONFLICTING`, block affected dimension.
8. Duplicate identical evidence → idempotent, one canonical fact, no double count.
9. Newer bar revision replacing earlier canonical revision → supersedes only for
   cutoffs ≥ newer `known_at` and only when content differs; `A→B→A` remains distinct.

Corrections apply prospectively from their actual known time. Original facts,
amendments, cancellations, revisions, conflicts, and historical knowledge are
preserved. Immutable captures are never rewritten.

## 14. Universe and funnel

Frozen funnel:

```
MASTER_UNIVERSE
  → MARKET_ELIGIBLE
  → DATA_READY
  → TECHNICAL_EVALUATED
  → DECISION_CANDIDATE
```

`setupEvaluated = true` only after: (1) `MARKET_ELIGIBLE` is affirmatively proved;
and (2) `DATA_READY` is reached. Diagnostic feature calculation does not advance
the funnel. When required market eligibility is `PARTIAL`, `UNKNOWN`, `STALE`,
`CONFLICTING`, or otherwise unproved, independently valid diagnostic facts/features
may still be calculated and shown, but `setupEvaluated` remains `false`,
`TECHNICAL_EVALUATED` is not reached, and candidate promotion is blocked.

`FETCH_UNIVERSE` remains separate from `MASTER_UNIVERSE`. Preserve independent
monitoring unions for HELD positions, the personal WATCHLIST, and committed Outcome
obligations. Discovery exclusion does not hide held positions; parked instruments
remain historically retained; suspended instruments remain monitored for reopening;
reopening does not retroactively create missed candidates; recovered data becomes
usable only at later valid knowledge cutoffs.

## 15. States and machine-testable reason codes

**States.** `marketEligibility` ∈ {`ELIGIBLE`, `INELIGIBLE`, `DATA_BLOCKED`}.
`featureState` ∈ {`AVAILABLE`, `WARMUP`, `UNAVAILABLE`}. `evidenceQuality` ∈
{`VERIFIED`, `PARTIAL`, `UNKNOWN`, `CONFLICTING`, `STALE`}. `priceComparability` ∈
{`CLEARED`, `KNOWN_BREAK`, `UNRESOLVED`}. No combined confidence score exists.

**Reason codes (minimum set).**

- Identity/reference: `IDENTITY_CONFLICT`, `REFERENCE_NOT_KNOWN`, `TYPE_UNKNOWN`.
- Listing/board/mechanism: `PRE_LISTING`, `POST_DELISTING`, `LISTING_UNKNOWN`,
  `UNSUPPORTED_TYPE`, `UNSUPPORTED_BOARD`, `BOARD_UNKNOWN`, `BOARD_CONFLICT`,
  `MECHANISM_UNRESOLVED`, `EXCHANGE_RULE_UNKNOWN`.
- Session: `SESSION_UNCONFIRMED`, `SESSION_UNAVAILABLE`, `NO_TRADE`.
- Status: `STATUS_TRADING`, `STATUS_COVERAGE_PARTIAL`, `STATUS_UNKNOWN`,
  `STATUS_STALE`, `STATUS_CONFLICT`, `SUSPENDED`, `REOPENING_UNCONFIRMED`.
- Price observation: `PRICE_NOT_ADMITTED`, `PRICE_SYNTHETIC_CARRY_FORWARD`,
  `PRICE_PLACEHOLDER`, `PRICE_SUBSTITUTED`, `PRICE_INVARIANT_INVALID`,
  `PRICE_UNIDENTIFIED`, `TRADED_OBSERVED_AT_T`.
- Price comparability: `PRICE_CLEARED`, `PRICE_KNOWN_BREAK`, `PRICE_UNRESOLVED`,
  `PRICE_BASIS_UNVERIFIED`, `PRICE_CLEARANCE_INCOMPLETE`,
  `CORPORATE_ACTION_COVERAGE_PARTIAL`.
- Evidence: `EVIDENCE_SOURCE_INADMISSIBLE`, `EVIDENCE_SCOPE_MISMATCH`,
  `EVIDENCE_CONFLICT`, `EVIDENCE_STALE`, `EVIDENCE_UNKNOWN`,
  `EVIDENCE_SUPERSEDED`, `EVIDENCE_CANCELLED`.
- Features/readiness: `VOLUME_BASIS_UNVERIFIED`, `BENCHMARK_MISSING`,
  `INSUFFICIENT_HISTORY`, `NUMERIC_OUT_OF_RANGE`, `CANONICAL_INVALID`,
  `MISSING_CURRENT_BAR`, `STALE`, `ZERO_VOLUME_UNEXPLAINED`.
- Funnel: `NOT_EVALUATED`, `MARKET_NOT_ELIGIBLE`, `DATA_NOT_READY`,
  `CANDIDATE_PROMOTION_BLOCKED`.

## 16. Data readiness, candidate promotion, and park/re-entry

Data readiness is reached only when `MARKET_ELIGIBLE` is proved and required price/
feature dependencies are `CLEARED`/available. Candidate promotion is strict:
unproved market eligibility, `UNRESOLVED`/`KNOWN_BREAK` price on the evaluated
segment, or insufficient required history keeps `setupEvaluated = false` and blocks
promotion. Thresholds are never lowered to increase candidates. Parked instruments
remain retained and re-enter only at a later valid knowledge cutoff; reopening never
creates retroactive candidates.

## 17. V0.1 compatibility

V0.1 (`screener-v0.1.0`) is unchanged. Existing V0.1 captures retain their original
evidence semantics. Existing Outcomes retain their original policy/input bindings
(`outcome-v0.1.0`, snapshot/verification schema 1). Research datasets are not
reinterpreted (`research-evaluation-v0.1.0`, schema 1). V0.2 cannot repair historical
V0.1 captures or Outcomes. V0.2 uses its own separate policy/schema identity,
`screener-evidence-v0.2.0`.

## 18. Scenario cross-check

These are consistency checks only; they do not redesign the contract.

| Scenario | Result |
|---|---|
| normal eligible stock | `MARKET_ELIGIBLE` proved, `DATA_READY` reached, `setupEvaluated = true`, candidate permitted |
| known suspension | `SUSPENDED` / `INELIGIBLE`, no setup, monitored for reopening |
| reopening learned late | earlier cutoff remains `SUSPENDED`; reopening applies only from its `known_at`; no retroactive candidate |
| stale status | `STATUS_STALE` blocks `MARKET_ELIGIBLE`; diagnostics may show; `setupEvaluated = false` |
| partial status | `STATUS_COVERAGE_PARTIAL` blocks `MARKET_ELIGIBLE`; diagnostics may show; `setupEvaluated = false` |
| conflicting board | `BOARD_CONFLICT`/`MECHANISM_UNRESOLVED`, `DATA_BLOCKED` for scope, no setup |
| split inside EMA seed | `PRICE_KNOWN_BREAK`; restart/warmup; `DATA_READY` not reached until post-break cleared segment meets the window |
| partial corporate-action coverage | `CORPORATE_ACTION_COVERAGE_PARTIAL`; never `CLEARED`; affected dependencies `UNRESOLVED`; no `DATA_READY` |
| zero-volume genuine observation | admitted only if source semantics distinguish it; establishes `TRADED_OBSERVED_AT_T`, not `tradingStatus` |
| synthetic/carry-forward bar | `PRICE_SYNTHETIC_CARRY_FORWARD`; never admitted |
| missing benchmark | stock-only setup may continue; RS/context `UNAVAILABLE`; `BENCHMARK_MISSING` |
| held suspended instrument | `SUSPENDED`/`INELIGIBLE`, no setup, still returned in held union |
| watchlist warmup | feature `WARMUP`; insufficient required history keeps `setupEvaluated = false` |
| source outage | evidence `UNKNOWN`; fail closed; no invented value |
| correction after immutable capture | original capture unchanged; correction applies only at later valid knowledge cutoffs |
