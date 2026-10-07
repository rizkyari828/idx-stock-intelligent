# Outcome V0.2 — candidate-source contract

Semantic freeze: **2026-10-07, Asia/Jakarta**. Reviewed baseline: `main` at
`5008c46ee044f493ff1dfa34b6e57cefa7bccc5b`, clean, 3 ahead / 0 behind origin/main.

**FROZEN: `outcome-v0.2.0`, schema 1.** This additive policy measures explicitly
enrolled technical candidates. It is documentation only: no storage, migration,
evaluator, verifier, Research, API, UI, acquisition or operational change is made
by this milestone. Subsequent implementation must use this contract.

## 1. Foundations and version boundary

Read alongside [Outcome V0.1](OUTCOME_TRACKING_V0_1_CONTRACT.md),
[Outcome Verification V0.1](OUTCOME_VERIFICATION_V0_1_CONTRACT.md),
[Research V0.1](RESEARCH_EVALUATION_V0_1_CONTRACT.md),
[Screener Evidence V0.2](SCREENER_EVIDENCE_V0_2_CONTRACT.md) and its
[persisted binding](SCREENER_EVIDENCE_V0_2_PERSISTED_BINDING.md).

Outcome V0.1 evaluates the entire immutable prospective V0.1 snapshot population,
regardless of eligibility/setup, using `(runId, instrumentId, horizonSessions)`.
Its schema, FK, anchor provenance and verifier require a schema-1
`screener-v0.1.0` / `PROSPECTIVE_CAPTURE` / PILOT parent. Research V0.1 uses that
captured-row population. A candidate-only population with explicit enrollment and
a different parent changes policy meaning; adding a nullable FK cannot resolve it.

V0.2 therefore has its own policy and source lifecycle. V0.1 contracts, tables,
FKs, manifests, hashes, verification and Research inclusion remain unchanged.
No conversion, backfill, synthetic V0.1 snapshot, polymorphic reinterpretation,
or extension of V0.1 policy resolution is permitted.

## 2. Source population and explicit enrollment

The only supported source is a committed immutable decision under
**`screener-technical-candidate-v0.2.0`, schema 1**, with `candidate=true`, linked
to its exact committed **`screener-evidence-v0.2.0`, schema 1**, technical capture
with **capture schema 1**. Authenticate stored candidate/capture identities,
projection hashes and their matching subject/session/date/cutoff. Unsupported
versions fail explicitly; there is no current/latest/compatible policy fallback.

`candidate=false` is a factual evaluated non-candidate, neither missing nor failed
by implication. It is ineligible for this population. Candidate eligibility does
not depend on rank, portfolio, current registry membership or current evidence.
Enrollment consumes the stored decision and capture; it does not rerun technical
arithmetic, setup progression, readiness or candidate qualification against live
facts. Authentication of stored projections is not a new technical evaluation.

**Candidate persistence does not enroll or create any Outcome.** A separate
explicit command selects one exact candidate UUID for enrollment. Its caller
supplies no price, cutoff, clock, expected result, replacement capture or evidence
identity. Server authentication derives the entire source binding. No batch
selection of latest candidates, automatic enrollment, scheduler or public route
is authorized here.

Enrollment commits a prospective obligation for all four horizons. It inserts
one enrollment, no terminal observations or persisted pending placeholders.
A candidate never enrolled creates no Outcome obligation. Enrollment has no
mutable cancellation, retargeting or removal based on later performance.

## 3. Deterministic identity and immutable binding

The semantic enrollment key is exactly:

`(candidateDecisionId, outcomePolicyId = outcome-v0.2.0, outcomeSchemaVersion = 1)`.

Use existing canonical JSON/SHA-256 rules on the object with these three named
properties, in canonical order, UUID in lowercase `D` form. Its full SHA-256 is
`enrollmentIdentity`; `enrollmentId` is the UUID parsed from its first 32 hex
characters, following the existing capture/candidate convention. Check the full
hash and complete immutable binding on UUID/unique-key conflicts; truncated-hash
collision never permits reuse of different content. Clocks are not key inputs.

Retain at least the following enrollment projection and manifest bindings:

| Binding | Required immutable facts |
|---|---|
| Outcome | Enrollment ID/full identity, policy/schema and manifest schema 1 |
| Candidate | Decision UUID, policy/schema, full replay/projection hash, candidate=true and original recordedAt |
| Capture | Capture UUID, capture schema, technical policy/schema, input/result hashes, technical replay identity and original recordedAt |
| Subject | Stable instrument UUID and exchange UUID authenticated from capture evidence; no symbol-only binding |
| Original boundary | Session ID, evaluation date and full-precision original capture/candidate cutoff |
| Anchor | Exact evaluation-date stock binding, price evidence identity/revision/payload hash, bar revision/content hash, raw artifact identity/hash/length, source/endpoint/field/version, raw close/date and exact premise references |
| Anchor assessment | Capture-time Outcome anchor usability and primary reason, supported by retained facts rather than an authority boolean |
| Enrollment | Server enrollmentKnownAt, enrollmentRecordedAt and computed deadline |

The immutable enrollment projection, including clocks and manifest references,
has `bindingHash` under the existing canonical hash convention, excluding the
bindingHash field itself. This authenticates
binding content; it is distinct from the semantic key hash. Exact source proofs
may be referenced rather than duplicated from the capture. Never fabricate an
absent reference or silently use an equivalent-looking capture/revision/archive.

Different valid capture/cutoff creates a different candidate and enrollment, even
for the same instrument/date/episode. An altered capture/cutoff under the same
candidate UUID is an integrity conflict, not a new interpretation of that key.

## 4. Prospective deadline and chronology

Let `E` be the candidate's authenticated original evaluation date. Define:

**`enrollmentDeadline = E.AddDays(1) at 00:00:00 Asia/Jakarta`, converted to UTC.**

New enrollment requires all of:

`originalCutoff <= captureRecordedAt <= candidateRecordedAt <= enrollmentKnownAt`

`enrollmentKnownAt <= enrollmentRecordedAt < enrollmentDeadline`.

Resolve enrollmentKnownAt once from the actual database clock inside the bounded
enrollment transaction, before source authentication. Resolve enrollmentRecordedAt
from the actual insertion clock; do not accept caller clocks or reuse historical
candidate/capture time as the enrollment time. Verify the clock/deadline constraints
at insertion and immediately before committing; crossing the deadline before
commit dispatch aborts the new enrollment. Return success only after commit.

The target completion proof must already support a completed session on E at the
original cutoff, with completedAt on E in Jakarta and no later than its knownAt or
cutoff. The capture's price/session/source premises must have been known at their
referencing record's admission boundary, as required by the persisted binding.
Missing, contradictory or impossible chronology fails closed without enrollment.

This is deliberately a **conservative civil-session boundary**, not an invented
exchange opening time. Existing retained facts supply session date and completion
time, not an opening instant. Every counted future horizon has date > E; closing
enrollment before the next civil date starts prevents selection after +1 prices
become observable, even if completion proof, bar ingestion or local knowledge is
delayed. Do not use absence of a +1 bar/proof, or its later knownAt, as permission
to enroll. Do not extend the deadline through a weekend/holiday or wait until a
future scheduled session closes. This intentionally rejects otherwise early
weekend enrollments and next-morning enrollments.

Exactly-at-deadline and later new enrollments are rejected, including historical
candidates whose +1 is not locally materialized. No enrollment/outcome row is
written on rejection. An authenticated retry of an already committed matching
enrollment may return that original enrollment after the deadline; it creates
nothing and preserves original clocks. A retry never repairs a lost/corrupt source.

These are application/domain chronology rules, not exact PostgreSQL transaction
commit-visibility reconstruction. recordedAt is not a commit timestamp. No claim
about historical sub-transaction MVCC visibility or exact exchange opening hours
is made. The enrollment operation selects no future return information.

## 5. Horizon base and exchange calendar

**Horizon base = E, the original candidate evaluation session**, not enrollment
date/time. Observation key is **`(enrollmentId, horizonSessions)`**, with exactly
1, 5, 10 or 20. The base session is excluded. All four obligations exist logically
from enrollment; each terminal row may materialize independently later.

Reuse V0.1's exchange-global ordinal, weekend/closure semantics, no endpoint
sliding and capacity: traverse civil dates strictly after E through
min(Jakarta date of outcomeKnownAt, 2027-08-24), within
**2026-08-24..2027-08-24**, at most 366 civil dates. Enrollment outside that
supported base-date window is rejected. Late-window enrollments may leave some
horizons unresolved permanently; no silent capacity extension.

For V0.2, authenticate the typed ScheduledSession/CompletedSession facts for the
exact capture exchange and session ID using frozen V0.2 admission/conflict rules.
Count only independently proved COMPLETED exchange dates; OPEN alone, instrument
TRADING, a bar or positive volume never proves completion. Same-day materialization
retains the Jakarta 19:00 safeguard and completedAt proof. V0.2 typed completion
requires completedAt, including prior dates; do not synthesize a missing timestamp
from V0.1's older prior-date shortcut.

Sourced CLOSED dates are skipped. Saturday/Sunday default is the existing versioned
weekend rule; sourced exceptional trading may override it. Contradictory calendar
facts fail closed. Unknown elapsed weekdays make the ordinal SESSION_UNAVAILABLE;
a complete prefix awaiting current-session completion/fewer than N sessions is
PENDING. Exchange sessions count even when this instrument is suspended/no-trade.
Every counted completion must be strictly after enrollmentRecordedAt; contradictory
retained ordering never permits skipping a session to improve the ordinal.

Retain an ordered entry for E and every traversed civil date, with classification,
selected/conflicting exact proof references or explicit weekend rule. No projected
date, manufactured session or later endpoint replacement.

## 6. Capture anchor authentication

The anchor is the unique stock binding at E in
`capture.Projection.Result.StockBindings`, for the candidate subject. Use its
**raw `PriceValue.Bar.Close`** and actual date, never adjustedClose, another stock
binding, benchmark, live canonical bar or enrollment-date price.

Authenticate the capture's existing input/result/replay hashes; matching candidate
source hashes; price evidence UUID, evidence revision/series, payload hash and
bar revision/hash; original chronology; exchange/session/date; raw source linkage;
exact convention and completed-session premises; and required capture identity,
currency, status, mechanism and comparability evidence. Evidence-record revision
is not bar revision. Re-encode/authenticate the retained bar with the frozen V0.2
bar hash, not V0.1's different canonical bar hash. No lossy V0.1 conversion.

The numerical convention is shared with Outcome V0.1: positive exact raw close,
ordinary IDR equity, RAW_AS_TRADED/STOCK_RAW, supported continuous TRADING context,
independent completion and original capture-time price comparability. Preserve
Outcome V0.1's conservative **positive equity volume** requirement for the Outcome
anchor and forward endpoint. Quantity-unit/liquidity-feature clearance is not an
additional return requirement.

This is an explicit source mismatch: V0.2 captures can admit positively proven
genuine zero-volume prices or a currency outside the Outcome IDR convention.
Such an authentic candidate may enroll; its original anchor is unusable for this
Outcome convention, retained as **CAPTURED_PRICE_BASIS_UNVERIFIED**. Once a horizon
is proved it yields ANCHOR_UNAVAILABLE, preserving the factual raw close/date.
Do not invalidate/rewrite the candidate or promote this price to an AVAILABLE
Outcome. Missing close/date/stale/unevaluated stock bindings cannot occur in a
compliant current technical capture; do not invent those fixtures as valid sources.

Distinguish authentic original unusability from loss of exact evidence. Missing or
corrupt required capture/anchor references prevent enrollment; after enrollment
they prevent a trustworthy assessment/verification. They never manufacture
ANCHOR_UNAVAILABLE. A later corrected anchor or new clearance cannot repair the
original anchor assessment.

## 7. Forward evidence and exact price-return calculation

Resolve outcomeKnownAt once from the actual DB clock inside one bounded REPEATABLE
READ assessment before market reads. Require
`enrollmentRecordedAt <= outcomeKnownAt <= outcomeRecordedAt`; insertion records
the actual server clock. Late assessment is allowed and retains its actual later
knowledge boundary, never the horizon date as a fabricated knowledge time.

Forward selection uses frozen V0.2 authority/scope/correction/revision precedence
at outcomeKnownAt and the original bound price field, subject, exchange and session
ID. Select the exact horizon-date price; validate the selected revision after
selection. Invalid/conflicting evidence never falls back to an older cleaner
revision or another date/source. Persist the complete selected/conflicting input
set and explicit absences used by this assessment. Replay later uses that closed
set, never another live as-of query.

For AVAILABLE, derive exact target-to-horizon comparability from typed V0.2 facts:
ordinary stable instrument/exchange and IDR currency throughout the span; matching
raw source/endpoint/field/version conventions; exact original anchor and selected
endpoint revision bindings; complete exchange-session sequence; and at least one
authoritative COMPLETE action-coverage assertion covering the **whole inclusive
E..horizon interval**, with all exact event premises authenticated. Conflicting or
partial coverage prevents clearance. Separate partial/adjacent coverage intervals
cannot be stitched into whole-span clearance. No intermediate instrument bars,
indicator warmup or benchmark are required for an endpoint return.

Derive comparability using frozen V0.2 premises/evaluators, not a stored CLEARED
boolean or a fabricated V0.1 InstrumentSnapshot. Positive known unit-changing
events cannot be cleared by identical metadata. Preserve the original anchor
revision even when forward evidence has a correction to that date.

For AVAILABLE only, use exactly:

`checked(100m * (horizonClose / anchorClose - 1m))`.

Use exact CLR decimals and native decimal division, no explicit rounding/tolerance,
float, numeric(p,2), clamping, zero-fill or adjusted price. Overflow/unrepresentable
selected input remains UNRESOLVED / NUMERIC_OUT_OF_RANGE. Unchanged prices produce
genuine zero. No total return, dividend cash, strategy return, execution costs,
benchmark-relative outcome, MFE/MAE or trade claim.

## 8. Terminal semantics and precedence

Reuse the four economic terminal states, with V0.2 source authentication:

| State | Positive replay condition |
|---|---|
| AVAILABLE | Exact horizon, original usable anchor, usable endpoint and whole-span comparability reproduce the exact raw price return; reason null |
| ANCHOR_UNAVAILABLE | Intact original capture facts establish Outcome anchor unusability; current supported reachable case/reason is defined in section 6 |
| DATA_UNAVAILABLE | Intact authoritative exact-endpoint facts establish unusability under the same conservative endpoint convention |
| BASIS_UNCERTAIN | Intact authoritative span facts positively establish an unsupported unit/action/source/currency convention |

Precedence is: unresolved session ordinal; original anchor usability; positive
terminal endpoint evidence; positive terminal basis evidence; incomplete retryable
endpoint/comparability evidence; otherwise AVAILABLE. Service/integrity failure
aborts assessment, without a factual terminal row. An unresolved cell is derived
only: PENDING, SESSION_UNAVAILABLE and UNRESOLVED are never persisted.

DATA_UNAVAILABLE reasons, in primary-reason priority order, are POST_DELISTING,
SUSPENDED_AT_HORIZON, NO_TRADE_AT_HORIZON,
ENDPOINT_CLASSIFICATION_UNSUPPORTED, SPECIAL_REGIME_UNSUPPORTED. Use exactly
V0.1's effective delisting predicate (delisting date **strictly before** endpoint),
positive authoritative suspension/no-trade covering the completed endpoint, or
positive unsupported security/currency/mechanism classification. V0.2 typed
Delisting, Suspension/TradingStatus and security/currency/rule facts can establish
the corresponding conditions under their frozen authority/scope gates.

The current V0.2 payload has no independent authoritative NO_TRADE fact.
NO_TRADE_AT_HORIZON cannot be emitted from zero volume, a missing bar or a T3
genuine-zero observation. Until a separately authorized typed source binding
exists, an otherwise unsupported no-trade assertion remains unresolved. A genuine
zero-volume forward bar alone also remains UNRESOLVED / ZERO_VOLUME_UNEXPLAINED
under this Outcome convention. No payload change is required by this contract.

BASIS_UNCERTAIN reason priority is UNIT_CHANGING_EVENT (SPLIT/REVERSE_SPLIT),
CAPITAL_ACTION_UNSUPPORTED (RIGHTS_OR_SHARE_EVENT/BONUS_OR_SHARE_DISTRIBUTION),
SECURITY_CONVERSION_UNSUPPORTED (CONVERSION/MERGER_OR_REORGANIZATION), then
PRICE_CONVENTION_UNSUPPORTED for positively established incompatible raw
source/currency/unit convention. Stock-dividend semantics stay in the already
bound bonus/share-distribution class; do not classify unsupported free text.
Use exact authenticated typed action events; no inferred ratios/adjustments.
Missing action coverage or UNPROVED convention means UNRESOLVED, not BASIS_UNCERTAIN.
Cash dividends remain price-only; no new cash-dividend interpretation/claim.

Unambiguous independent positive terminal evidence may decide despite unrelated
missing inputs; contradictory evidence for the deciding condition cannot.
Unavailable terminal returns are null, never zero; observed authenticated close
may remain factual. Persist one stable reason, at most 128 characters. No timeout,
retry count, missing-bar age or later current-state absence terminalizes a cell.

## 9. Exact retained manifest and verification chain

The normative replay chain is:

**observation → enrollment → candidate decision → technical capture → exact
retained anchor/proofs**, plus the observation's exact forward evidence set.

Enrollment manifest schema 1 binds section 3's source/chronology/anchor facts and
original cutoff. Observation manifest schema 1 binds enrollment ID/full identity/
bindingHash, policy/schema, horizon, outcomeKnownAt/evaluationCutoff, original
anchor link, selected endpoint or explicit absence, ordered calendar, exact
listing/status/identity/currency/convention/action selections and terminal reason.
Retain revision-series/correction/cancellation and exact premise references when
needed by the frozen selector. Exact forward records include the facts needed to
reproduce ties/conflicts; an absence is an original input fact, not a lookup today.

Validate required fields, policy/schema/identity/cutoff agreement, fixed evidence
kinds, nonempty IDs, hashes, lengths, unique reference IDs, valid dependency
links and deterministic ordering. Reference sets are ordered by lowercase UUID
`D` string ordinal; calendar by civil date; reason sets by the existing canonical
reason convention. Never omit an input to fit a bound or infer a missing premise.

Reuse strict V0.2 payload decoding and canonicalization. Exact referenced evidence
records must match their full retained provenance and payload hashes. Raw artifact
identity, source/hash/length and required retained archive bytes must authenticate
using existing raw-artifact/archive conventions. Validate SHA-256 and byte length;
missing/changed bytes fail closed. Source document URLs are provenance, never a
permission to fetch. An equivalent-looking archive or different evidence/revision
ID cannot replace the bound one. No new provider parser or archive subsystem.

Bounds: enrollment manifest and each observation manifest <=64 KiB UTF-8; existing
capture projection <=32 MiB; candidate projection <=8 KiB; <=512 distinct forward
evidence records including recursive premises, separate from the existing capture
bound; <=366 calendar entries. Reuse existing archive/parser bounds. Exceeding a
bound aborts without partial/truncated evidence. Commands 15 seconds, operation
60 seconds. No unbounded traversal or verification persistence.

Future V0.2 verification subject is one committed `(enrollmentId, horizonSessions)`
observation. Enrollment alone/unmaterialized horizon is not a verification subject.
Resolve exact Outcome/candidate/technical/capture schema bindings before evidence
interpretation. Never broaden the existing V0.1 verifier's allowlist; the additive
V0.2 path resolves this contract explicitly.

State precedence matches the integrity vocabulary: invalid request; missing
enrollment/committed observation; POLICY_VERSION_UNAVAILABLE for unsupported
stored policy/schema; INPUT_NOT_AVAILABLE for missing/malformed/corrupt exact
required inputs; DIFFERENT_RESULT after complete valid retained replay disagrees;
otherwise MATCH. Runtime/database/archive access/deadline failure preventing a
trustworthy attempt is a service failure. No new transport route is frozen here.

Compare typed policy/schema, observation/enrollment/source identity, subject,
original clocks/deadline eligibility, horizon/anchor dates and closes, terminal
state/reason, exact return and required retained identity sets. Full valid evidence
disagreeing with a stored typed price/result yields DIFFERENT_RESULT; missing or
corrupt evidence is INPUT_NOT_AVAILABLE. Current verifiedAt is execution time
only, excluded from historical selection/result comparison. Diagnostics have
deterministic field ordering, at most 100 structured field/stored/replayed/reason
differences and explicit truncation; no paths, secrets, raw payloads or SQL.

MATCH means reproducible under this exact frozen retained chain. It means neither
profitability nor prediction/trade/strategy validity. No Decision Verification or
technical re-execution prerequisite; authenticate the stored source facts without
borrowing a different verifier verdict as proof of the Outcome. Later evidence,
captures, candidates, Outcomes, portfolio, registry or policy changes do not alter
an intact MATCH. No repair, network/provider recovery or current-state fallback.

## 10. Future persistence and atomicity

Decision: **SEPARATE_V0_2_ENROLLMENT_AND_OUTCOME_TABLES**.

| Future logical table | Keys and immutable relationships |
|---|---|
| outcome_v02_enrollment | PK enrollmentId; UNIQUE enrollmentIdentity and (candidateDecisionId, outcomePolicyId, outcomeSchemaVersion); FK candidateDecisionId → screener_technical_candidate.decision_id and captureId → screener_technical_capture.capture_id, both ON DELETE RESTRICT; guards require the candidate's exact capture/source hashes and candidate=true; retain projection/bindingHash, source fields, anchor assessment, clocks/deadline and bounded manifest |
| outcome_v02_observation | PK (enrollmentId, horizonSessions), FK enrollmentId → outcome_v02_enrollment ON DELETE RESTRICT; fixed outcome-v0.2.0/schema 1; raw anchor date/close, proved horizon date/nullable close, state/reason, nullable decimal return, actual knowledge/recording clocks and bounded manifest |

No generic polymorphic framework or alteration of decision_snapshot tables is
required. Exact evidence UUIDs resolve to retained screener_evidence_record keys;
candidate/capture/source-pair and manifest references require transactional
authentication, not independent FKs that could accept mismatched parents. Reuse
existing immutable UPDATE/DELETE/TRUNCATE guards and archive retention conventions.
Anchor projection equality, clock/deadline, source/hash and terminal evidence
guards protect inserts. Do not add a mutable queue, enrollment status, verification
table/history or delete unresolved obligations.

New enrollment authenticates one bounded retained set in one REPEATABLE READ
transaction and atomically commits the enrollment. Identity conflicts/retries use
existing unique-key arbitration; re-read a new transaction on a concurrent retry,
never merge evidence views or reset original clocks. Authenticate an existing
matching enrollment before applying the new-enrollment deadline. A different
source/binding under that identity fails explicitly, with no retarget/overwrite.

Observation assessment is explicit and bounded to one enrollment/horizon. Reuse
V0.1 immutable terminal-row/idempotent subset semantics: return existing terminal
row without reevaluation; otherwise authenticate original source, select forward
evidence once, retain/authenticate required archives before INSERT, and commit
only a proved terminal row atomically. An unresolved assessment writes nothing.
Separate horizons may commit at different actual cutoffs. Retry returns the exact
original terminal row. No automatic terminalization on candidate/enrollment INSERT.

Verification uses one bounded REPEATABLE READ, READ ONLY view and one consistent
copy of required authenticated archive bytes. No writes, outcome mutation or
verification persistence. No migration is created or executed in this milestone.

## 11. Research and signal meaning

**Research V0.1 includes only its existing V0.1 population.** Bare V0.2 candidates,
enrollments and terminal V0.2 observations are excluded. Do not scan candidate
tables, union V0.2 rows into V0.1, change its denominators/hash/cohorts or use
verification as an inclusion/AVAILABLE-return gate. V0.2 analytical inclusion
requires a separately authorized Research V0.2/additive population contract.

This measures an enrolled technical signal's raw endpoint prices. It asserts no
trade, purchase, accepted recommendation, position size, portfolio action, realized
P&L or BUY/SELL recommendation. Enrollment timing/population remains selective;
this contract makes no independence or strategy-validity claim.

## 12. Frozen lifecycle and scenario cross-check

Lifecycle: commit technical capture → commit technical candidate → explicitly
enroll before the deadline → assess exact +1/+5/+10/+20 as retained evidence
becomes sufficient → commit terminal observations → independently verify their
retained chain. Candidate creation alone stops at the second step.

These are semantic acceptance checks, **not executed implementation tests**:

| Scenario | Required result |
|---|---|
| candidate=true, intact chain, immediate enrollment before deadline | One enrollment, four logical obligations, zero automatic terminal rows |
| candidate=false enrollment | Explicit rejection; no enrollment/observation writes |
| candidate=true never enrolled | Retained candidate only; no Outcome obligation |
| Duplicate enrollment retry, including after deadline | Return original intact enrollment/identity/clocks; no second enrollment |
| Later capture for same subject/date | Different source key; cannot alter earlier enrollment/anchor; new enrollment still subject to its own unchanged date deadline |
| Enrollment after +1 is knowable or at deadline | Reject even if +1 bar/completion is absent locally; no backdating |
| Required original anchor evidence missing/corrupt | Reject enrollment, or service/input failure on later assessment; verification INPUT_NOT_AVAILABLE; never fabricate terminal absence |
| Intact capture has authentic zero-volume/non-IDR Outcome anchor | Enroll if otherwise prospective; retain CAPTURED_PRICE_BASIS_UNVERIFIED; ANCHOR_UNAVAILABLE only after horizon proof |
| Anchor later corrected | Original revision/close/assessment remains; no repair or replacement |
| +1 AVAILABLE | Proved first completed future exchange date, usable original/raw endpoint and whole-span clearance; exact checked return |
| +5 DATA_UNAVAILABLE | Proved fifth exchange date plus positive authoritative endpoint suspension/delisting/unsupported classification; null return |
| Later status/action/price/session evidence | May resolve a previously unresolved cell under a new actual assessment cutoff; never rewrite committed rows or change intact old MATCH |
| Exact candidate/capture verification chain | Authenticate all exact retained sources and original clocks; MATCH only for exact terminal replay |
| Candidate row without enrollment | No persisted Outcome, no verification subject, no Research row |
| V0.2 terminal Outcome exists | Remains outside Research V0.1 |
| Same identity with altered candidate/capture/hash/cutoff | Explicit integrity conflict; no overwrite or silent retarget |
| Holiday/weekend immediately after E | Does not extend enrollment deadline; closures skip only horizon counting |

## 13. Design completion and implementation boundary

The source-binding blocker is resolved at the **contract level**. Subsequent
implementation is unblocked for the separate enrollment/observation path and
exact retained-source replay specified here. The positive NO_TRADE representation
limitation and conservative capacity/deadline are explicit, not open semantic
decisions or permission to invent facts. Existing stored candidates outside the
deadline cannot be enrolled retrospectively; fixtures belong only in disposable
storage, and genuine future candidates must be enrolled prospectively.

Recommended implementation order: additive persistence/enrollment with deadline
and source-integrity acceptance; deterministic observation evaluator/terminal
commit; exact V0.2 verification replay. Research population, public API/UI,
providers/acquisition/scheduling, ranking and portfolio work require separate
authorization. FullIdx remains disabled; soak and provider limits are unaffected.
