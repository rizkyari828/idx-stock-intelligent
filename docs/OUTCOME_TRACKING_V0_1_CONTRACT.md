# Outcome Tracking V0.1 — frozen design contract

Design frozen: **2026-10-03, Asia/Jakarta**. Decision: **GO for subsequent bounded
implementation; design only in this milestone**. Reviewed clean `main` at
`720f46c25993fa05237e1b20b2fc02a03166fdf2`, eight commits ahead of `origin/main`.
Outcome policy **`outcome-v0.1.0`**, storage/manifest schema **1**.

This document is the canonical implementation contract for atomic factual forward
price observations. It adds no implementation and changes neither
`SCREENER_V0_1_CONTRACT.md` nor `DECISION_SNAPSHOT_V0_1_CONTRACT.md`.
FROZEN requirements below are implementation requirements, not claims that an
outcome table, service, scheduler, verifier or UI already exists.

## 1. Purpose, evidence reviewed and boundaries

Answer: after this exact prospective captured observation, what comparable market
close became observable at +1, +5, +10 and +20 confirmed exchange sessions?
A captured close is a descriptive reference, never an executable entry/fill.
No recommendation, action, winner/loser, success/failure, hit rate, alpha, strategy
ranking, threshold optimization, backtest or aggregate performance metric.

The reviewed implementation supplies the foundations:

| Foundation | Reviewed source / reuse |
|---|---|
| Immutable full population, capture clocks, row close/date/provenance and exact manifest | `Application/DecisionSnapshots.cs`, `Infrastructure/DecisionSnapshotService.cs`, migration `0005_decision_snapshots.sql` under `src/IdxStockIntelligence.*` |
| Exact retained-key authentication, archive length/SHA checks, policy binding and typed comparison | `Infrastructure/DecisionVerificationService.cs`, `Application/DecisionVerification.cs` |
| Revision chronology and bounded temporal selection | `Application/DailyBarRevisions.cs`, `Application/ScreenerEvidence.cs`, `Infrastructure/ScreenerEvidenceDatabase.cs` |
| Independent sourced sessions and completed-session handling | `Application/ExchangeCalendarEvidence.cs`, `CompletedSessionPolicy.cs`, `ScreenerSessions` in `ScreenerEvidence.cs`, `PilotValidation.cs` |
| Knowledge-dated full instrument snapshots and revision-specific price clearance | `Application/ScreenerReferences.cs`, `Infrastructure/ScreenerReferenceFiles.cs` |
| Decimal arithmetic and descriptive return conventions | `Application/ScreenerFeatures.cs`, `PilotFeatures.cs`; frozen Screener D/K/O |
| Canonical positive OHLC, source linkage, append-only market/portfolio records | `Domain/Phase0Model.cs`; migrations `0001` through `0005` |

Paths in the table abbreviate their existing layer prefixes. There is no typed
corporate-action adjustment/entitlement engine or total-return ledger. The current
price intervals certify raw unit continuity/event coverage; they do not supply
split factors, merger conversions, cash entitlement or dividend reinvestment.
Reuse that evidence boundary; do not build an action engine to release V0.1.

Frozen Screener O describes a later **episode-level** evaluation using confirmation
close and potentially IHSG-relative returns. This milestone stores **capture-row
market observations**, including non-confirmed rows. It neither implements nor
redefines that episode study. Confirmation close is not substituted for the row
close; episodes remain correlated context. Snapshot contract 13 explicitly permits
separate immutable row-linked outcomes with their own policy and evidence cutoff.

## 2. Observation identity and population

One immutable result per **`(runId, instrumentId, horizonSessions)`**, where the
horizon is exactly 1, 5, 10 or 20. Composite FK to the existing captured row.
No new episode key, synthetic capture or benchmark population row. Use only
schema-1 `PROSPECTIVE_CAPTURE` / PILOT snapshots within existing capacity bounds.

The write always evaluates the entire immutable run population, maximum 210 rows,
without shortlist, pagination, rank, current universe or current holdings filters.
Include ELIGIBLE, INELIGIBLE, DATA_BLOCKED; NONE, WATCH, CONFIRMED, FAILED and
setupEvaluated=false; configured unheld and all held outside-universe rows. These
facts remain in the original snapshot and are joined, not copied as mutable labels.
Eligibility/setup never decide outcome admission. Valid price evidence does.

Repeated captures of the same episode/date/input hash remain distinct observations.
Episode ID permits later correlation analysis only. Do not combine them or count
them as independent trades. No outcome enters Screener features, ranks or decisions.

## 3. Anchor: use the captured observation, never a future replacement

**FROZEN:** the numerical anchor is exactly the stored row close, if authenticated
and comparable at capture. `anchorMarketDate` and `anchorClose` preserve the row's
actual stored date/value, including nulls and an unusable older observation. Storing
those factual values does not certify them. An unavailable outcome has no return.

The run's immutable `targetSession` is the **session-count base** for all its rows.
It is not an alternative price source. A calculable anchor must have row.marketDate
== targetSession, positive exactly representable close, an authenticated captured
observation revision whose close matches the row, and captured knowledge-visible
price clearance. Require independently completed target proof and no unclassified
or uncompleted exchange day from that target through the captured through date.
Use the copied capture bundle and original cutoff, not current registry/labels.

Authenticate the observation link from row provenance plus the captured manifest.
`CurrentEvidence` can identify a different invalid target revision; it is **not**
permission to replace the observation. If it contradicts the observation at the
same target date, fail closed. An absent fieldStates.close entry means the existing
DTO omitted an AVAILABLE field; it alone is not price-basis certification.

| Capture condition | Anchor treatment after the horizon resolves |
|---|---|
| Missing close/date or no captured observation | ANCHOR_UNAVAILABLE / CAPTURED_CLOSE_MISSING |
| Row date earlier than target or proven stale | ANCHOR_UNAVAILABLE / STALE_ANCHOR; preserve original date/value, never relabel or carry forward |
| Unknown target | No resolvable session base: dynamic SESSION_UNAVAILABLE; no invented date |
| Target exists but original capture completion/calendar boundary is unproved | ANCHOR_UNAVAILABLE / CAPTURE_SESSION_UNCONFIRMED after an exact horizon resolves; new proof does not repair capture readiness |
| Raw close exists but captured identity/currency/price clearance is absent/uncertain | ANCHOR_UNAVAILABLE / CAPTURED_PRICE_BASIS_UNVERIFIED |
| Invalid capture observation or explicit suspension/no-trade at anchor | ANCHOR_UNAVAILABLE with precise captured evidence reason |
| DATA_BLOCKED for warmup/optional features or an unrelated setup gate, but anchor proof valid | Anchor may be valid; no 20/50/60-session feature requirement |
| Held-only/outside universe or INELIGIBLE | Same anchor checks; membership/shortlist is irrelevant |

No future bar, later correction, later basis attestation or verification MATCH
repairs an anchor that lacked required proof at capture. Later attestation can prove
forward comparability only for an already valid captured anchor. This conservative
choice prevents retrospective admission of an originally unverified observation.
Capture BLOCKED and snapshot verification MATCH remain unchanged and independent.

## 4. Exact exchange-session horizons

Starting strictly **after targetSession**, traverse civil dates in chronological
order through min(Jakarta date of outcomeKnownAt, 2027-08-24). Exclude the base
session; do not traverse future dates to infer whether the horizon is complete.
Count only independent `ObservedTrading` proofs
accepted by the existing `ScreenerSessions.Resolve` / `CompletedSessionPolicy` at
the outcome knowledge boundary. The Nth completed exchange session is the exact
horizon date. Same-day acceptance retains the existing Jakarta 19:00 safeguard
and valid completedAt proof; prior-date acceptance retains the existing sourced
prior-completed-session rule. Do not add stricter fictitious clocks to old proofs.

Skip announced/exceptional closures proved by sourced retained evidence. Weekend
classification uses the existing Saturday/Sunday calendar rule; explicit sourced
exceptional trading can override it. Persist the selected proof or the versioned
weekend classification for **each traversed civil date**, including closures, so
session ordinals and missing dates can be replayed. Bars never prove a session.
Friday -> Monday is +1 only if Monday is confirmed trading; if Monday is a proved
closure, Tuesday may be +1. Never silently assume a weekday holiday or open day.

An unknown/conflicting elapsed weekday before the Nth date makes the ordinal
SESSION_UNAVAILABLE, not a shorter horizon. A current session awaiting its completion
cutoff/proof is PENDING if the prior prefix is complete; an unresolved older gap is
SESSION_UNAVAILABLE. Future announced-open dates are not completed sessions.
If the complete known prefix contains fewer than N sessions and no past gap,
PENDING. No partial substitution, projected date or synthetic session.

Every counted session must genuinely be after the capture observation: if a later
calendar correction introduces a completed session at/before capturedAt, do not
skip it to obtain a favorable ordinal. ANCHOR_UNAVAILABLE /
NONPROSPECTIVE_SESSION_BASE once the ordinal is proved. Dates after captured through
are necessarily prospective; a same-date exceptional session requires retained
completion evidence strictly later than capturedAt. Unknown ordering fails closed.

Counting is exchange-global, not instrument-tradable. Suspension/no-trade does not
pause the clock. No slide to the next instrument bar. All rows share the same exact
horizon date for this run/horizon, even if their captured prices are unusable.

V0.1 preserves the existing evidence window **2026-08-24..2027-08-24 inclusive**.
Scan at most 366 civil dates and never read beyond that window to complete a horizon.
If N cannot be proved inside the window after it ends, return dynamic
SESSION_UNAVAILABLE / HORIZON_OUT_OF_SCOPE. A separate versioned capacity review is
needed for later horizons; do not silently extend Screener bounds. Late captures
near the end may therefore retain unresolved horizons permanently in V0.1.

## 5. Knowledge, late computation and evidence selection

Four distinct clocks/dates:

- `anchorMarketDate`: unchanged captured row date, nullable; targetSession is joined
  separately as the counting base. An old date is never called a current anchor.
- `horizonMarketDate`: proved Nth subsequent exchange session, null while unresolved.
- `outcomeKnownAt`: actual server/database UTC evaluation knowledge cutoff, resolved
  once with `clock_timestamp()` inside repeatable read before market reads.
- `outcomeRecordedAt`: actual database insertion clock, >= outcomeKnownAt, not a claim
  of exact commit time. The operation returns success only after commit.

outcomeKnownAt is when this complete **derived assessment** was made with the retained
inputs, not the earliest hypothetical time anyone could have computed it. Manifest
input knownAt/retrievedAt/completedAt retains the actual source chronology. No caller
clock, horizon backdating or max(inputTime) substituted for the actual evaluation
clock. Preserve capturedAt and all original source timestamps independently.

Late computation is permitted. An Oct 15 evaluation of an exactly proved Oct 12
+5 endpoint selects retained evidence visible on Oct 15 and records Oct 15 clocks.
It describes that earlier market endpoint with later knowledge; it is not an outcome
known/stored on Oct 12. No price date after the horizon is used in the return.

Select the latest visible horizon revision per instrument/date by knownAt DESC,
revisionNumber DESC, with inclusive known/source retrieval/session knowledge filters
**before** revision selection. Only committed evidence visible in this transaction
qualifies. Validate selected contents/quality/provenance/hash **after** selection.
Invalid selected evidence does not fall back to an older revision or another date.
Capture anchor uses its exact original revision, even if corrected since capture.
Select latest full instrument reference snapshot known by outcomeKnownAt, then
its effective intervals; no coverage borrowed from older full snapshots.

Persist exact selections and explicit absences. A later import with an older
claimed knownAt must not enter verification of an already committed outcome.
Later corrections, A->B->A revisions and replaced live files never rewrite it.

## 6. Minimum metrics and decimal arithmetic

Store only captured anchor date/close, exact horizon date, nullable authenticated
horizon close, price-return percentage, factual availability/reason, clocks and
linkage. No absolute-change column: it is derivable. No MFE/MAE, intrahorizon high/
low path, total return, execution cost, dividend cash, benchmark or relative metric.

For AVAILABLE only, in checked .NET decimal and this order:

`priceReturnPct = 100m * (horizonClose / anchorClose - 1m)`.

Endpoints must be positive, exact CLR decimals. Use the existing numeric boundary
validation; reject unrepresentable canonical numbers rather than parse-and-round,
clamp or zero-fill. No explicit intermediate/final quantization. CLR decimal
division has its native representable precision (up to 28 fractional digits); the
policy freezes that arithmetic, not arbitrary-precision rational arithmetic.
Store unconstrained PostgreSQL numeric with the exact resulting CLR decimal value,
not numeric(p,2) and not float/double. JSON numbers follow current API conventions;
UI rounding, if later implemented, is display only. Overflow gives UNRESOLVED /
NUMERIC_OUT_OF_RANGE; a correctable numerical input is not terminal absence.
Unchanged prices yield genuine **0**, not missing. A zero close
is invalid, not a -100% substitute. Unavailable priceReturnPct is always null.
A valid observed horizon close can remain visible when comparability is unavailable;
it is labelled an observation, not a calculable return.

## 7. Raw price / corporate-action comparability

Require the captured anchor's own verified `STOCK_RAW` / `RAW_AS_TRADED` proof.
For the forward interval, the latest full instrument snapshot at outcomeKnownAt
must establish ordinary IDR identity and a **single** price-clearance interval
covering targetSession..horizonMarketDate, source matching both endpoints,
continuity RAW_AS_TRADED, EventCoverage CLEARED and contentHashes including both
exact endpoint revisions (the original anchor hash and selected horizon hash).
Reuse `ScreenerReferences.Price` for endpoint checks; this whole-span check is the
small required additional outcome rule. Matching two isolated endpoint labels is
not proof that an intervening unit change did not occur.

A single whole-span interval is a deliberate conservative ceiling: separately
cleared segments, even with matching text labels, do not prove cross-segment
comparability. Missing whole-span clearance remains UNRESOLVED /
PRICE_COMPARABILITY_UNVERIFIED and may be retried when proof arrives.
No need for every intermediate instrument bar; retained calendar completeness and
whole-span continuity/event coverage are required. Endpoint-to-endpoint return is
not a continuous tradable price path, indicator calculation or MFE/MAE.

| Authoritatively established event / uncertainty within the span | V0.1 treatment |
|---|---|
| Split or reverse split | BASIS_UNCERTAIN / UNIT_CHANGING_EVENT; no inferred factor |
| Rights issue, bonus shares, stock dividend | BASIS_UNCERTAIN / CAPITAL_ACTION_UNSUPPORTED; no unit/entitlement adjustment |
| Merger, conversion, reorganized security identity | BASIS_UNCERTAIN / SECURITY_CONVERSION_UNSUPPORTED; no substitute instrument |
| Positively established incompatible currency/source/unit convention | Terminal BASIS_UNCERTAIN / PRICE_CONVENTION_UNSUPPORTED |
| Missing event coverage, UNKNOWN/UNRESOLVED, missing whole-span clearance or unmatched endpoint hash | Dynamic UNRESOLVED / PRICE_COMPARABILITY_UNVERIFIED; retryable |
| Ordinary cash dividend with verified unchanged raw units and CLEARED coverage | PRICE RETURN only: exclude dividend cash, include ex-dividend price move; no total shareholder return claim |

Known unit-changing events cannot be certified away by equality of metadata or an
unsupported CLEARED assertion. The underlying source evidence must substantiate the
interval. Terminal BASIS_UNCERTAIN requires positive authoritative evidence of the
unsupported event/incompatibility, its effective date/span and exact source linkage.
A generic EventCoverage UNRESOLVED/UNKNOWN flag, source URL, missing interval or
failed clearance check does not prove an event. If existing retained typed evidence
cannot establish the positive condition, leave UNRESOLVED; do not infer action
facts from symbols, price jumps or free text, or add an action classifier here.
Do not fetch actions, use adjustedClose, fabricate volume or build conversions.

Equity endpoints retain existing positive-volume observed-trading validation and
independent suspension/no-trade distinctions. Full volume-unit/value-proxy clearance
is not an extra return dependency; its failure does not erase an otherwise cleared
raw price. Index semantics remain independent: valid index OHLC does not require
ordinary-share positive volume/tradability. No benchmark row/return is computed here.

## 8. Resolution, states and terminal evidence

Keep outcome availability separate from eligibility, features and verification.
A cell has derived **resolution = UNRESOLVED or TERMINAL**. Materialization is a
separate per-cell flag: a terminal read-only preview is not yet a committed row.
The stored state vocabulary itself determines terminal resolution; no mutable
resolution/status column or PENDING placeholder is added.

| State | Meaning | Resolution / persisted? |
|---|---|---|
| PENDING | Complete known prefix has not yet reached N completed future sessions | UNRESOLVED / never persisted |
| SESSION_UNAVAILABLE | Base/elapsed session sequence is unproved, conflicting or outside capacity | UNRESOLVED / never persisted |
| UNRESOLVED | Horizon may be proved, but forward bar, status, identity, numerical or comparability evidence is incomplete/retryable | UNRESOLVED / never persisted |
| ANCHOR_UNAVAILABLE | Original captured observation lacked required value/clearance under capture-time knowledge | TERMINAL / only once exact horizon is proved |
| DATA_UNAVAILABLE | Positive authoritative evidence establishes an unusable required endpoint under V0.1 | TERMINAL / only once exact horizon is proved |
| BASIS_UNCERTAIN | Positive authoritative evidence establishes unsupported price comparability under V0.1 | TERMINAL / only once exact horizon is proved |
| AVAILABLE | Exact endpoints, sessions and comparability support the frozen price return | TERMINAL / may be persisted |

Precedence: unresolved session ordinal first; then original anchor validity; then
positive terminal endpoint evidence; then positive terminal basis evidence; then
incomplete endpoint/comparability evidence (UNRESOLVED); otherwise AVAILABLE.
Terminal unavailability can be proved without a horizon bar when an authenticated
source establishes the disqualifying condition. Conflicting or ambiguous authoritative
facts do not prove a terminal condition: leave unresolved, unless an independent
unambiguous terminal condition already decides the outcome. No fallback to an older
cleaner revision or reference snapshot.

Persist one primary stable reason (<=128 characters), null for AVAILABLE. Retain
exact supporting evidence in the manifest. Missing forward evidence uses dynamic
reasons such as HORIZON_BAR_MISSING, SESSION_UNCONFIRMED, IDENTITY_UNVERIFIED,
STATUS_UNKNOWN, CANONICAL_QUALITY_UNAVAILABLE, NUMERIC_OUT_OF_RANGE and
PRICE_COMPARABILITY_UNVERIFIED; these are **not terminal evidence**. Unknown quality,
a rejected/invalid selected bar, zero equity volume alone or possible later correction
remain retryable. No age, deadline, retry count or missing bar finalizes absence.
Service/integrity errors abort the operation rather than create factual missingness.

### Terminal DATA_UNAVAILABLE evidence gate

The whitelist is affirmative, knowledge-visible, effective evidence that establishes:

- **SUSPENDED_AT_HORIZON / NO_TRADE_AT_HORIZON:** independently sourced instrument
  session proof or unambiguous authoritative trading interval covers the exact
  completed horizon session and certifies the unusable endpoint; no missing-bar or
  zero-volume inference. Select dated replacements/conflicts under existing rules.
- **POST_DELISTING:** canonical authoritative listing-boundary evidence establishes
  the instrument was already delisted at the required endpoint, using existing
  effective-date semantics. No last-close, proceeds or successor substitution.
- **SPECIAL_REGIME_UNSUPPORTED / ENDPOINT_CLASSIFICATION_UNSUPPORTED:** authoritative
  effective identity/trading intervals positively establish a V0.1-unsupported
  endpoint class/currency/mechanism, rather than UNKNOWN or missing coverage.

Retain the exact proof/record/snapshot identity, effective date/span, knownAt,
content hash where available and source evidence IDs. Positive evidence must be
unambiguous and valid at this evaluation cutoff. A subsequent authoritative correction
may challenge a committed result but cannot overwrite it. No additional terminal
absence rule is inferred. If the current typed inputs cannot prove one of these
conditions, leave UNRESOLVED, even indefinitely. Prefer false incompleteness over
falsely finalized missingness.

### Terminal BASIS_UNCERTAIN evidence gate

Require positive authoritative, effective-dated evidence of a split/reverse split,
rights issue, bonus shares, stock dividend, merger/conversion or incompatible unit/
source/currency convention that V0.1 does not support comparing across the span.
The manifest links the established condition to its authoritative retained evidence;
merely incomplete corporate-action/event coverage cannot satisfy this gate.
Section 7 defines the conservative price policy. If positive event facts cannot be
established from retained typed inputs, no terminal basis row is materialized.

Suspension/no-trade at the endpoint does not mean zero return. A mid-span suspension
without unit change need not invalidate valid endpoints: the return describes prices
only, not ability to trade. Unknown delisting/status/mechanism remains UNRESOLVED,
not a fabricated exclusion. Newly listed and held instruments keep the same global
ordinal; missing history never shortens +20 or slides to another bar.

## 9. Terminal-only materialization and retryable missingness

**Append an immutable row only when that cell is TERMINAL and its exact horizon
is proved.** No mutable placeholders. Horizon occurrence by itself is insufficient.
A missing forward bar, basis/status proof or incomplete action evidence remains
unmaterialized and retryable; later valid evidence may resolve it to AVAILABLE or
a positively proved terminal unavailable state, with actual later knowledge clocks.
Original capture-time anchor knowledge is fixed and cannot be repaired this way.

A whole-run/horizon assessment returns every captured row, but may contain existing
terminal rows, newly terminal rows and unresolved unmaterialized rows. Insert only
the newly terminal subset, **atomically in one transaction**. Zero new terminal rows
means no outcome/archive write. Existing terminal rows remain unchanged; unresolved
cells are not omitted or forced into DATA_UNAVAILABLE/BASIS_UNCERTAIN for completeness.

Once committed, AVAILABLE and terminal unavailable rows are equally immutable.
Later imports/corrections never overwrite them. A later correction dataset remains
a separately reviewed milestone; it is not a retry of a terminal key. Verification
replays the exact committed assessment and never substitutes new evidence.

No manual outcome count, selected-row promotion, terminalization timeout or
retrospective soak qualification. Prospective research must distinguish unresolved
from terminal unavailable, not encode collection latency as a finalized market fact.

## 10. Proposed additive storage (not implemented)

One table **`decision_snapshot_outcome`**, no outcome header or mutable queue:

| Column | Frozen semantics |
|---|---|
| run_id uuid, instrument_id uuid, horizon_sessions smallint | Composite PK; horizon IN (1,5,10,20); FK to snapshot row ON DELETE RESTRICT |
| schema_version smallint, outcome_policy_id text | 1 and outcome-v0.1.0; explicit implementation allowlist |
| anchor_market_date date NULL, anchor_close numeric NULL | Exact copied captured row facts; positive when present, possibly unusable; equality guard to original row |
| horizon_market_date date NOT NULL | Proved exact Nth session after joined targetSession, within supported window |
| horizon_close numeric NULL | Valid authenticated selected endpoint close; positive when present |
| price_return_pct numeric NULL | Exact checked decimal result; only AVAILABLE is non-null |
| outcome_state text, reason text NULL | Only four materialized states from section 8; constrained state/null consistency |
| outcome_known_at timestamptz, recorded_at timestamptz | Actual DB cutoff/insertion clocks, recorded >= known >= capturedAt |
| evidence_manifest jsonb | Schema-1 bounded typed linkage, <=64 KiB UTF-8 per row; no raw provider payload or source-document blobs |

AVAILABLE requires positive matching-date anchor/endpoint, non-null return and null
reason. Others require null return, a nonblank policy reason and retained evidence
establishing terminality; observed prices may remain present. PENDING,
SESSION_UNAVAILABLE and UNRESOLVED are forbidden stored states. Validate each row's
FK, anchor equality, exact horizon proof, terminal evidence and clocks; reject
UPDATE/DELETE/TRUNCATE. Do not require count(outcomes) == run.rowCount: different
cells may resolve in later transactions and retain their own outcomeKnownAt.
Rows newly inserted by one operation share that operation's cutoff; committed
rows preserve their earlier clocks. Each subset is atomic. Follow existing trigger,
restore and transactional migration conventions.
Existing snapshot completeness/originating-transaction guards remain untouched;
outcomes are separate FK rows inserted later, never extra snapshot rows.

PK supports run/horizon lookup. Reuse snapshot-row instrument/run index plus FK
join for instrument history; add no speculative indexes or partitioning. Unresolved
selection is a bounded anti-join on (runId,instrumentId,horizonSessions), not a
mutable status table or a test for an entirely missing run/horizon set.
No schema SQL or migration 0006 is created during this design milestone.

## 11. Exact evidence linkage and retention

Reuse existing SHA-256, canonical bar content hashing, snapshot content hashes,
`DecisionBarLink`, `DecisionReferenceLink`, `SessionProof`, `InstrumentSessionProof`
and fixed-kind `DecisionReferenceArchive` conventions. No new result hash,
canonicalizer, arbitrary path or raw blob column. Typed values/order are verified,
not inferred from a canonicalizer that sorts arrays.

Per-row outcome manifest must identify:

1. Original snapshot schema/policy, inputHash/selectedDigest and exact captured
   observation link (instrument/date/revision/knownAt/contentHash/rawArtifactId),
   or explicit captured absence. Original anchor clearance/proofs come from the
   original snapshot manifest and its authenticated archives, not today's bundle.
2. Exact selected horizon bar link plus its existing raw/source identity and
   retrieved/known chronology, or explicit absence/selected invalid evidence. A
   missing bar selection remains empty in future replay even after later import.
3. Ordered civil-date classifications from target through Nth horizon, exact
   selected session proofs (date/status/reference/knownAt/completedAt) and explicit
   weekend rule; source references/dated replacements must be reproducible.
4. Selected latest-full instrument snapshot identity/hash, relevant effective
   identity/trading/whole-span price intervals and their existing evidence IDs;
   endpoint status proofs and canonical listing-boundary keys when used, with
   explicit missing/conflicting selection. Preserve all latest conflicting inputs
   rather than resolve ties silently. No borrowing old reference coverage.
5. Exact fixed-kind copied outcome reference bundle archive hashes/lengths and
   missing-file markers. Share existing content-addressed decision-reference archive
   storage and parser. No files from HTTP; no archives for benchmark-only data.

Use bounded copied bundle bytes, archive before database outcome inserts, then
persist links atomically with results. Identical archive bytes deduplicate; failure
leaves no outcome row. An unreferenced archive after rollback is permissible existing
archive behavior, not permission to prune retained evidence. Missing required original
archive/record, corruption or malformed/oversized live bundle is a service/input
integrity failure: abort without freezing a fabricated unavailable market outcome.
Ordinary missing forward coverage/absent endpoint in a valid selection is dynamic
UNRESOLVED, not evidence of terminal absence. A committed absent-bar link is allowed
only when independent authoritative terminal evidence establishes why that endpoint
is unusable (or the original anchor is terminally unavailable). No provider recovery
or new artifact store.

Retain all outcome rows, original captures, exact canonical revisions/raw linkage,
listing records and referenced archives. No TTL, pruning or overwrite. Backup/restore
must preserve all these together and test immutability/completeness/authentication.

## 12. Transaction, bounded operation and idempotency

For one run and one horizon:

1. Perform a bounded stored-row lookup. If every captured cell already has a
   terminal row (or the run is empty), return without live reference loading.
   Otherwise copy the three fixed files once; no caller-controlled path.
2. Serialize write evaluations for this run/horizon with a bounded native PostgreSQL
   advisory lock, acquired **before** establishing the repeatable-read view; release
   in finally, including cancellation/failure, on the same dedicated connection
   held through the outcome transaction; discard a connection if unlock fails.
   Acquisition uses the existing 15-second command/60-second operation bounds.
   This coordinates incremental subsets without a new table/dependency. Different run/horizon keys are independent.
   Retain composite uniqueness as the database duplicate guard.
3. Open repeatable-read transaction; resolve one actual DB cutoff, load immutable
   run and recheck stored terminal rows. Reuse those rows; assess only missing keys.
   Authenticate captured linkage/archives, resolve calendar once, and select a bounded
   batch of exact endpoint inputs/references for missing cells at that cutoff.
4. Return the full population assessment. If no missing cell is terminal, commit
   no outcome/archive writes. Otherwise validate all newly terminal results and
   archive the copied evidence before inserting their complete subset atomically.
5. Commit the new subset and combine it with existing terminal rows and unresolved
   cells in the response. DB recording clocks are actual insertion times. Failure
   leaves no partial new subset; earlier committed rows survive unchanged.

Each operation uses one exact proved exchange-session ordinal. When terminal rows
already exist, its horizon date must agree with their retained run/horizon alignment.
A later conflicting calendar correction leaves remaining cells UNRESOLVED /
HORIZON_ALIGNMENT_CONFLICT; do not rewrite prior dates or silently combine different
endpoints. If this cannot be resolved from authoritative retained evidence, keep the
remaining cells unresolved for later reviewed handling.

Bounds: <=210 rows written, <=840 run read cells, <=211 selected IDs, <=80,000
selected canonical rows, <=366 traversed dates, reference bundle existing 4 MiB/
10,000-record limits, <=64 KiB manifest per outcome and <=32 MiB total operation
manifest bytes. 15-second commands, 60-second operation deadline, cancellation.
Endpoint-oriented selection should ordinarily read far fewer bars; do not use
unbounded CLI history or rerun indicators/state machines. No portfolio replay
or provider access is needed: anchor context is already immutable.

Composite uniqueness is the idempotency key; no new request UUID/table is necessary.
Existing terminal key -> original immutable row. Missing/unresolved key -> may be
assessed again. Newly terminal key -> insert once. No UPSERT/UPDATE. An incomplete
set is normal and may grow by newly terminal cells; it is not an integrity error.

Concurrent callers recheck existing keys after obtaining the run/horizon lock.
For uniqueness/serialization failure, roll back the entire new subset and retry
at most once in a fresh transaction/view, recovering winner rows and reassessing
only still-missing keys under actual new clocks. Then 503 if still unsuccessful.
Never return stale provisional values as a committed winner or discard other newly
terminal cells merely because one key collided. Duplicate protection also covers
clients bypassing the normal lock. Existing unsupported-version rows remain readable
and immutable; new evaluation requires an explicit capture/outcome policy binding.

## 13. Minimal future API (not implemented)

**Write:** `POST /api/screener/decision-snapshots/{runId}/outcomes/evaluate`.
Strict body **`{"horizonSessions": 1}`**, allowing only 1,5,10,20; no query parameters,
duplicate/unknown JSON properties, individual instrument selection or all-horizons
switch. Existing same-origin JSON/loopback and 64 KiB transport rules apply.
Reject caller prices, result/state/reason, target/date/cutoff/clocks/policy/path.
Server selects all captured rows. One horizon avoids mixed incremental commits and
bounds the operation; repeat up to four explicit operations when appropriate.

Response: runId, horizonSessions, policy/schema, actual assessedAt,
newlyMaterializedCount and all cells ordered by instrument UUID. Each cell contains
instrumentId, resolution (UNRESOLVED/TERMINAL), materialized flag, state/reason,
original anchor facts, exact nullable horizonMarketDate, nullable endpoint/return
and nullable committed outcomeKnownAt/recordedAt. Do not use one run-wide
materialized flag for a mixed result. No manifest, archive paths, raw
payload, private portfolio notes or connection details in public responses.

- 201: one or more newly terminal rows committed atomically, including later
  incremental resolution of a previously unresolved subset.
- 200: no new terminal rows; return existing terminal and unresolved cells together,
  or an empty run. Terminal unavailable outcomes are successful responses.
- 400 OUTCOME_REQUEST_INVALID: invalid ID/body/query/horizon.
- 404 SNAPSHOT_NOT_FOUND (or SNAPSHOT_ROW_NOT_FOUND for per-row read).
- 409 OUTCOME_POLICY_VERSION_UNAVAILABLE: new evaluation lacks an explicitly bound
  capture/storage/outcome implementation; do not reinterpret it under today's policy.
- 503 OUTCOME_UNAVAILABLE: DB/deadline/archive/integrity/service-bound failure;
  no partial write. Cancellation follows existing propagation conventions.

**Reads:** GET `/api/screener/decision-snapshots/{runId}/outcomes` and
GET `/api/screener/decision-snapshots/{runId}/rows/{instrumentId}/outcomes`.
No arbitrary query controls; at most 840/4 cells respectively, ordered UUID then
horizon ascending. These produce the complete population x four-horizon grid,
including unresolved cells; no missing rows silently removed.

Existing cells return stored results/clocks without consulting live market data.
For missing cells, a bounded **read-only** use of the same evaluator produces an
explicitly unmaterialized preview as of server assessedAt, using current retained
knowledge. Previews include unresolved PENDING/SESSION_UNAVAILABLE/UNRESOLVED and
terminal-but-unmaterialized AVAILABLE or positively established unavailable
assessments; resolution and materialized are independent. Committed
outcomeKnownAt/recordedAt remain null until insertion. These previews may change
and are not immutable research records. GET never records them; it never
changes a stored cell. This avoids pretending a ready but unattempted outcome is
PENDING and avoids adding a misleading missing-result state. Readers/export research
must distinguish terminal AVAILABLE, terminal unavailable and unresolved, and
separately distinguish unmaterialized previews from committed terminal results.
Complete stored results remain readable without live reference readiness;
unresolved-preview service failures return a sanitized 503 rather than a false assessment. Stock Detail UI/history
integration and all outcome UI are deferred.

## 14. Future scheduling and verification

No scheduler now. A later daily operator scans **at most 20 oldest unresolved
runs per invocation**, stable capturedAt/runId order and optional context-bound
keyset cursor; missing per-cell (run,instrument,horizon) terminal keys, including
runs/horizons with some already committed rows. Resolve shared retained calendar
read-only, then invoke whole-run/one-horizon writes for proved ordinals, at most
80 operations from this page. Surface unresolved gaps/out-of-scope/unsupported
runs and continue bounded pages, rather than silently dropping them or polling
providers. Revisit unresolved cells as retained bars/session/basis/status evidence
arrives; never reassess committed AVAILABLE or terminal unavailable keys. Whole-run
responses include existing rows without recalculating them. No collector coupling,
mutable job queue, timeout-based finalization or all-history scan.

Future on-demand verification is **read-only**, separately version-bound to
(outcome-v0.1.0, schema 1). Authenticate exact original and outcome manifests/
archives, replay only selected keys and explicit absences, reuse original arithmetic,
session rules and typed comparison. Do not select today's latest evidence.

States: MATCH, INPUT_NOT_AVAILABLE, POLICY_VERSION_UNAVAILABLE, DIFFERENT_RESULT.
MATCH may reproduce a committed unavailable result; it certifies deterministic
reproduction, not external evidence truth or investment quality. Preserve stored
outcome and captured rows for all states. Bound diagnostics with existing verifier
conventions; no new hash/audit table or verifier implementation in this milestone.

## 15. Research missingness, scale and independent safety gates

Research denominator is the **entire captured population x requested horizons**,
joined to original setup/eligibility/capture date and correlation context. Include
terminal AVAILABLE, terminal unavailable, unresolved (including missing forward
bars/basis/status proof) and terminal-but-unmaterialized previews explicitly.
Do not inner-join AVAILABLE only, drop delisted rows, use today's
survivors, count correlated episode captures as independent trades, infer wins from
sign, or choose a later rerun of a terminal key to improve returns. Unresolved is
not failed, zero-return or unavailable-final; a later legitimate evidence arrival
may resolve it. Missingness and late-computation clocks are analytically meaningful.
Any later numerical subset must disclose its availability denominator; this milestone defines no aggregate performance UI.

10 instruments x 252 captures x 4 = **10,080 rows/year**; hypothetical 100 =
**100,800 rows/year**. Held union/repeated captures add rows. Planning at 2 KiB average
result+linkage gives roughly 20/200 MiB logical outcome storage yearly, excluding
indexes, original snapshots and shared raw/reference archives. Worst-case 64 KiB
manifest alone approaches 630/6,300 MiB; measure real byte lengths and archive reuse.
These are planning estimates, not measured capacity or authorization for 100 stocks.
Retain all, monitor storage, review bounds before expansion; no partitioning now.

No FullIdx enablement or collection-budget change. Soak and reference readiness
are independent: outcomes cannot increase soak, retroactively qualify Oct 2,
certify missing identity/action evidence or change Screener thresholds.

## 16. Real retained capture and original read-only design audit

Run **`3bc25cf2-901b-4c15-84b2-e265091f7f48`**:

- captured/knowledge `2026-10-03T04:13:45.712111+00:00`;
  recorded `2026-10-03T04:13:45.838421+00:00`;
  through 2026-10-03; target 2026-10-02; discovery-only; BLOCKED; ten rows.
- Read-only inspection found **10/10 captured closes dated Oct 2**, all DATA_BLOCKED,
  priceBasis/currency unavailable, no instrument reference snapshot in the captured
  manifest. Close field-state entries are omitted by the existing available-field
  DTO convention; the displayed close is not proof of basis readiness.
- Canonical history has **86 revisions**, latest economic date Oct 2. Retained session
  evidence ends Oct 2. None of +1/+5/+10/+20 is proved; the forty prospective cells
  are currently **PENDING/unresolved**, with no projected endpoint/return or stored
  outcome. Their anchor problem is separately identifiable; once an exact horizon
  resolves they would be ANCHOR_UNAVAILABLE under the captured-proof rule, unless
  the original retained evidence actually proves the missing clearance. New evidence
  cannot retroactively cure it. No outcome is written during this review.
- Initial audit: 13 existing table fingerprints; 211 protected file hashes covering
  pilot files, operation/summary reports, reference archives and both frozen contracts;
  schema versions 2,4,5. One run/ten rows; portfolio events/theses 0/0. Final checks
  matched **13/13 table fingerprints and 211/211 protected hashes**; schema stayed
  2,4,5 and no outcome table exists. `git diff --check` and document whitespace
  checks passed; no build or implementation suite was rerun for documentation.
- Soak **1/10**, qualifying **2026-09-30**, after_market_date **2026-09-28**;
  FullIdx **NOT ENABLED**. Oct 2 remains non-qualifying. Provider calls/units **0/0**.

No operational migration, capture, verification write, collection, provider call or
browser action is required for this design. Only this document and a short runbook
note change. No code/build/test pass is claimed for unimplemented outcomes.

## 17. Implementation acceptance plan

Use canonical root `dotnet test` and existing owned disposable DB/HTTP patterns;
no provider traffic or executable-runner substitute for standard discovery.

1. Session fixtures: Friday/Monday, explicit Monday holiday -> Tuesday, exceptional
   weekend open, anchor excluded, exact +5/+10/+20, late/calendar replacements,
   past unknown/conflicting gap, current uncompleted session and supported-window
   exhaustion. Bars alone never qualify. No partial/next-bar substitution.
2. Chronology: actual UTC DB cutoff/insertion clocks; late first computation;
   knowledge/retrieval/source bounds inclusive; future evidence excluded; immutable
   original anchor revision; late old-knownAt imports absent from replay; capture
   boundary gap and nonprospective calendar correction fail closed.
3. Anchors/prices: missing date/close, stale row, original close/provenance mismatch,
   CurrentEvidence invalid revision, captured absent basis cannot later be repaired;
   valid DATA_BLOCKED warmup/optional-feature row still usable. Positive exact decimals,
   genuine zero return, invalid zero price, precision rejection, overflow, native
   repeating division stored exactly, display rounding never alters stored value.
4. Basis/actions: whole-span hash-covered RAW_AS_TRADED -> AVAILABLE; isolated
   endpoint segments insufficient; later full snapshot missing old anchor coverage
   never borrows it; split/reverse/rights/bonus/stock dividend/merger unavailable;
   ordinary cash dividend remains labelled price-only. No adjusted-price fallback.
5. State/missingness: PENDING/SESSION_UNAVAILABLE/UNRESOLVED never persisted; a
   proved horizon alone never finalizes missing forward evidence. Test affirmative
   terminal suspension/no-trade/delisting/special regime versus missing or conflicting
   status proof, late listing and mid-span suspension with comparable endpoints.
6. Benchmark: missing IHSG and zero index volume do not block stock outcomes;
   equity zero-volume remains unknown without independent status proof. No benchmark/
   relative fields, population row or requirements introduced in V0.1.
7. Population: ELIGIBLE/INELIGIBLE/DATA_BLOCKED, NONE/WATCH/CONFIRMED/FAILED and
   unevaluated setup, held outside universe, below shortlist, repeated same episode
   captures distinct; one result per row/horizon and full missingness grid.
8. Storage/concurrency: atomic newly terminal subsets and legitimate incremental
   population growth, per-batch cutoff/per-row clocks, alignment conflict guard,
   composite FK, immutable UPDATE/DELETE/TRUNCATE, duplicate/retry/lost response,
   simultaneous same/different horizons and rollback/recovery. A terminal result
   cannot be overwritten; an unresolved cell can later resolve. Disposable
   fresh/upgrade/rerun/restore migration checks retain exact source records/archives together.
9. API/read/linkage: strict request/HTTP states, only one server-derived horizon,
   no clocks/prices/results/instrument filtering; read-only flagged previews with
   null committed clocks; archived copied bytes, SHA/length bounds, explicit absence,
   missing/corrupt inputs abort writes. Stored reads survive unavailable live files.
10. Future verification fixtures: four states, exact keys/absence/typed comparison,
    unavailable MATCH and policy allowlist; initially test linkage, do not implement
    a verifier merely to satisfy a future fixture.
11. Delayed resolution: proved horizon but missing canonical bar -> first operation
    UNRESOLVED/no row; later exact valid bar -> AVAILABLE with later actual cutoff.
    Missing session proof -> unresolved, later proof -> correct ordinal/resolution.
    Missing basis/status/action coverage -> unresolved, later substantiated whole-span
    proof -> AVAILABLE; UNKNOWN/UNRESOLVED flags never establish a terminal event.
12. Terminal evidence: authoritative unsupported corporate-action proof -> terminal
    BASIS_UNCERTAIN with exact effective/source linkage; if current typed inputs
    cannot establish the event, UNRESOLVED instead. Capture-time missing anchor
    remains terminal even after later proof/value. Terminal unavailable and committed
    AVAILABLE survive later corrections without mutation or automatic reassessment.
13. Concurrent incremental resolution: simultaneous unresolved retries create no
    placeholder/duplicate; concurrent transition to terminal creates exactly one
    immutable row per key. Mixed existing/new/unresolved cells all appear in response;
    every new subset commits or rolls back together. Existing rows preserve old
    clocks, later cells use later clocks, missing cells remain scheduler candidates.
14. Safety: before/after canonical/snapshot/portfolio/session/reference/operation/
    soak fingerprints; only explicitly authorized new outcome rows/archive bytes
    may change during future writes; provider 0/0, FullIdx disabled, no Screener
    policy changes or original snapshot mutation. No operational acceptance write
    until separately requested.

## 18. Decisions, remaining limitations and implementation order

No unresolved owner/product choice blocks this contract. External session, identity,
price-clearance and event coverage remain readiness limitations handled by the
frozen states. Terminal-only permanence, strict captured anchor proof, single-span
clearance, capacity boundary and provisional read previews are explicit design limits.
Temporary forward evidence gaps remain retryable; no terminal absence is manufactured.

After review, implement in this order:

1. Minimal typed policy/manifest/assessment and pure session/anchor/return evaluator;
   focused invariant tests, reusing existing validators/arithmetic/archive helpers.
2. Separately reviewed additive migration 0006 and atomic immutable terminal-subset/
   uniqueness constraints, with disposable upgrade/restore/concurrency tests.
3. Bounded whole-run single-horizon service and read APIs, archive linkage and
   actual clocks; standard discovery/build and relevant disposable HTTP acceptance.
4. Read-only assessment of real retained capture; a real materialization requires
   separately authorized operation and proved future horizon, not fabricated fixtures.
5. Later milestones may add outcome UI, scheduling, outcome verification and properly
   reviewed episode/benchmark research. MFE/MAE, total return, execution, aggregate
   metrics, evidence correction datasets and scale expansion remain deferred.

Finalization revision: **2026-10-03**. The prior whole-population finalization rule
is replaced by terminal-only atomic subsets and retryable missing forward evidence.
Only this contract and the runbook note are authorized for the documentation
checkpoint commit. No implementation or push; no build/test suites are required
for this documentation revision.
