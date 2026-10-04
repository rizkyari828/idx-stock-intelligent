# Outcome Verification V0.1 — frozen design contract

Design frozen: **2026-10-04, Asia/Jakarta**. Decision: **GO for subsequent
implementation; documentation only in this milestone**. Reviewed clean `main` at
`4c02f1d5a1854e8a7e6af7e6bb054b7adb0a6309`, **3 ahead / 0 behind origin/main**.
Exact replay binding: **`outcome-v0.1.0` + outcome storage/manifest schema 1**.
One committed immutable outcome row is the verification unit. **No migration is
required for the retained inputs consumed by the existing V0.1 evaluator.**

FROZEN requirements below describe a future verifier. No verifier, API, UI,
migration, persisted verification result or operational outcome is created here.
The frozen [Outcome Tracking](OUTCOME_TRACKING_V0_1_CONTRACT.md),
[Decision Snapshot](DECISION_SNAPSHOT_V0_1_CONTRACT.md),
[Research / Evaluation](RESEARCH_EVALUATION_V0_1_CONTRACT.md) and
[Screener](SCREENER_V0_1_CONTRACT.md) contracts remain unchanged.

## 1. Goal and reviewed foundations

Answer: **using only the exact retained inputs and frozen policy that produced
this committed terminal outcome, can its typed result be reproduced?**
Verification does not ask what an evaluation using today's evidence would produce.
It authenticates and replays an immutable artifact; it cannot refresh, repair,
replace or overwrite that artifact.

The review traced these existing foundations:

| Foundation | Source |
|---|---|
| Pure horizon/anchor/terminal/decimal evaluation and typed outcome manifest | `src/IdxStockIntelligence.Application/OutcomeTracking.cs` |
| Capture authentication, forward selection, archive copying and terminal-only persistence | `src/IdxStockIntelligence.Infrastructure/OutcomeTrackingService.cs` |
| Immutable composite key, original clocks, bounded manifest and evidence guards | `src/IdxStockIntelligence.Infrastructure/Migrations/0006_decision_snapshot_outcomes.sql` |
| Exact policy resolver, retained-key reader, archive checks and typed comparison patterns | `src/IdxStockIntelligence.Infrastructure/DecisionVerificationService.cs`, `src/IdxStockIntelligence.Application/DecisionVerification.cs` |
| Canonical raw numeric facts, exact revision selection and listing identity | `src/IdxStockIntelligence.Application/ScreenerEvidence.cs`, `src/IdxStockIntelligence.Infrastructure/ScreenerEvidenceDatabase.cs`, `src/IdxStockIntelligence.Application/InstrumentBoundaries.cs` |
| Knowledge-dated reference selection, exchange/instrument sessions and bounded archived parsing | `src/IdxStockIntelligence.Application/ScreenerReferences.cs`, `src/IdxStockIntelligence.Infrastructure/ScreenerReferenceFiles.cs`, `src/IdxStockIntelligence.Application/CompletedSessionPolicy.cs` |

The stored manifest retains the inputs actually consumed for this row: captured
anchor linkage, endpoint revision or explicit absence, listing linkage or absence,
ordered horizon calendar, selected instrument/status identities and exact copied
reference archives. The parent snapshot retains the original capture context and
its own evidence manifest/archives. Intermediate forward bars and other outcome
rows are not inputs to this row's terminal calculation. They need not be selected
again. Migration insertion guards support integrity but do not substitute for replay.

## 2. Verification subject and states

FROZEN subject: **`(runId, instrumentId, horizonSessions)`**, with horizons
**+1/+5/+10/+20**. The exact row must already be persisted in
`decision_snapshot_outcome`. Materialization is incremental; different rows may
have different original evaluation cutoffs. A run/horizon batch is therefore not
the V0.1 verification unit. Future bounded batches may compose individual results
without inventing an aggregate verification state.

Only these committed terminal outcome states are subjects:
**AVAILABLE, ANCHOR_UNAVAILABLE, DATA_UNAVAILABLE, BASIS_UNCERTAIN**.
UNRESOLVED/PENDING/SESSION_UNAVAILABLE previews have no immutable outcome row.
An unmaterialized key returns **404**, never INPUT_NOT_AVAILABLE, and has no
verification state. Verification must not run live assessment to manufacture one.

Freeze exactly four verification states, conceptually aligned with Decision
Verification:

| State | Meaning |
|---|---|
| MATCH | Supported frozen replay with all required exact inputs intact reproduces the entire defined typed projection. |
| INPUT_NOT_AVAILABLE | Supported policy, but required exact retained inputs are missing, malformed, corrupt or cannot be authenticated. |
| POLICY_VERSION_UNAVAILABLE | The exact stored policy/schema binding has no supported frozen replay implementation. |
| DIFFERENT_RESULT | Required exact inputs are available and authenticated, replay completes, and the typed result differs. |

Exact precedence:

1. Validate route/body; invalid request is HTTP 400, outside the four states.
2. Resolve the exact committed subject and parent; absent subject/parent is HTTP 404.
3. Resolve the stored outcome policy/schema and required capture interpretation.
   Unsupported binding returns POLICY_VERSION_UNAVAILABLE before evidence decoding.
4. Validate and authenticate required retained inputs. Any such failure returns
   INPUT_NOT_AVAILABLE before result comparison, even if a stored scalar also differs.
5. Execute deterministic replay. A completed replay with any typed projection
   difference returns DIFFERENT_RESULT; otherwise return MATCH.

A service-level database/runtime/archive availability failure that prevents a
trustworthy attempt returns HTTP 503; it is not a fifth verification state. An
unexpected evaluator fault is a service failure, not a fabricated mismatch.
No verification results, diagnostics or `verifiedAt` are persisted.

## 3. Exact policy resolution and original chronology

Resolve a concrete frozen implementation for **`outcome-v0.1.0`, schema 1**.
Never fall back to current/latest policy, a compatible-looking policy or another
schema. The required parent interpretation is the existing **`screener-v0.1.0`,
capture schema 1, PROSPECTIVE_CAPTURE, PILOT** context. An unsupported capture
policy/schema cannot be interpreted through another version. Unsupported policy
binding takes precedence over an otherwise malformed evidence manifest. A malformed
manifest version under supported row/header bindings is INPUT_NOT_AVAILABLE.

Keep three distinct clocks:

| Clock | Frozen use |
|---|---|
| Original outcome `outcomeKnownAt` / manifest `evaluationCutoff` | The same original knowledge/evaluation boundary used for all forward replay. They must denote the same UTC instant. |
| Original outcome `recordedAt` | Immutable recording chronology; preserve it and validate its relationship to original evaluation/capture clocks. |
| Current `verifiedAt` | Server execution time for this attempt only; never an evidence-selection cutoff or compared result clock. |

Captured evidence uses the parent's original knowledge/capture cutoff, not the
later outcome cutoff. Forward evidence and horizon completion use original
`outcomeKnownAt`. Validate original recording >= evaluation >= capture chronology
and the frozen snapshot chronology. Never replace those boundaries with now.
Reference archives may contain later-known records; original cutoff selection
excludes them rather than rejecting the entire archive for containing them.

These clocks describe persisted application/domain knowledge and recording
chronology. `recordedAt` is not a PostgreSQL commit timestamp. Verification does
not reconstruct historical MVCC visibility at an arbitrary sub-transaction instant.
Exact retained identities control replay, including when an old-known fact was
imported after the original operation. Future commit-visibility tracking is outside V0.1.

## 4. Exact retained input selection

Use the committed outcome manifest and immutable parent snapshot manifest as a
closed input universe. The verifier must not select today's latest canonical
revision, current registry, live reference files, new sessions, basis clearance,
listing/status/action facts or another capture/outcome. It must not repair missing
exact evidence using another revision with equivalent-looking values.

Read canonical bars by exact `(instrumentId, sessionDate, revisionNumber)` and
authenticate their recorded `knownAt`, content hash and `rawArtifactId`. Read a
listing by exact `(instrumentId, knownAt)` and authenticate its content hash.
Resolve instrument snapshots by retained snapshot identity/knowledge/hash inside
the authenticated archive. Session/status proofs use their complete retained typed
identity and source/knowledge facts, not a current date lookup.

Reusing the frozen reference selector **inside the exact authenticated copied
archives at the original request/cutoff** is permitted and reproduces original
selection, including conflicts/ties. Compare selected reference identity sets to
the manifest. It is not permission to select from live files. Capture selection
uses the original captured request. Forward selection uses the frozen history
anchor and the originally evidenced horizon from the manifest calendar, not a
possibly altered stored output date. Restrict forward instrument/status selection
to the subject instrument as the writer did when recording its manifest.

Null anchor/endpoint/listing links and empty selected reference sets are deliberate
recorded absence when valid under the original selection/branch. Preserve those
absences; never query for a replacement, even one with an earlier `knownAt`.
An explicit archive `present=false` is also an input fact, not a missing file.
This distinguishes an authentic original absence from loss of a referenced input.

Authenticate the bounded original capture evidence using existing retained-key
patterns. No current population/portfolio reconstruction is needed. Outcomes depend
on the captured row and required anchor context, not a new full Screener evaluation.
Capture `inputHash` and `selectedDigest` cross-bind the parent header and manifests;
do not claim to recompute the portfolio-inclusive capture input hash without a
separate Decision Verification replay.

## 5. Capture and anchor authentication

Load the exact immutable parent run and row. Authenticate:

- Composite relationship to `runId` and `instrumentId`; parent row membership.
- Original target exchange session, requested through-date and capture clocks.
- Captured row market date, close, stale/field availability and provenance used
  by `OutcomeEvaluator.CaptureProblem`.
- Captured anchor revision identity/hash/raw artifact linkage, when present;
  an outcome anchor link must correspond to the exact retained capture selection.
- Capture policy/schema/inputHash/selectedDigest linkage and required copied
  capture reference/session/status/price-clearance evidence.

Use the captured row close/date as the original anchor projection. Never replace
it with corrected canonical data. Authenticate retained facts without imposing
AVAILABLE-anchor prerequisites on every subject: a valid original missing close,
stale observation, absent clearance or unsupported status can reproduce
ANCHOR_UNAVAILABLE. If a referenced capture revision/archive/link is lost or
corrupt, return INPUT_NOT_AVAILABLE; do not reinterpret that loss as original
anchor unavailability. A changed stored outcome anchor projection is compared
against the authenticated captured projection and yields DIFFERENT_RESULT.

## 6. Session horizon reproduction

Replay `OutcomeEvaluator.ResolveHorizon` under the original cutoff with exact
copied session evidence and the original capture context. Authenticate the ordered
manifest calendar against those proofs. Its first date is the captured target
session, tagged ANCHOR, with the original capture-time proof/absence. Subsequent
entries are contiguous civil dates through the originally evidenced endpoint.

Exclude the target session from the ordinal. Count only confirmed completed
exchange trading sessions. Preserve sourced announced/exceptional closures and
the frozen weekend/exceptional-open rules. Keep the frozen completion, conflict,
prospective chronology, Jakarta date and bounded PILOT rules. No bar can manufacture
a session; no missing/invalid endpoint can slide the horizon to another date.
The resolved endpoint is the Nth confirmed session for exactly 1/5/10/20.

Missing/corrupt required proof, archive or inconsistent retained proof identity:
INPUT_NOT_AVAILABLE. Authenticated complete evidence that deterministically yields
a horizon date different from the stored result: DIFFERENT_RESULT. Validate manifest
input structure independently of stored output scalars so an altered stored horizon
date cannot turn this comparison case into a spurious missing-input result.

An original cross-row alignment guard affects whether a row may be committed;
it is not an extra replay dependency. A successful immutable row retains its own
calendar. Later siblings or new captures cannot influence verification.

## 7. Terminal replay and corporate-action safety

Use the existing frozen pure evaluator, its branch precedence and typed validators.
Pass the original outcome cutoff. Do not implement a second terminal classifier.
A replay that completes as an unresolved state differs from a stored terminal
result; unresolved replay is not a new verification subject or state.

**AVAILABLE:** reproduce anchor date/close, horizon date/close, instrument identity,
source/currency/raw price convention, whole-span continuity and revision-specific
hash-covered basis clearance. Both prices must meet frozen canonical validation.
Compute exactly:

`checked(100m * (horizonClose / anchorClose - 1m))`

Preserve .NET decimal division/checked arithmetic, overflow/precision handling,
genuine zero and negative returns. No tolerance, display rounding, binary float,
adjusted-price fallback or alternate formula. AVAILABLE reason remains null.

**ANCHOR_UNAVAILABLE:** reproduce the same frozen reason from original captured
context, including missing/stale/invalid close, unproved capture session/basis/status
or nonprospective session base. Original recorded absences may support MATCH.
Later anchor evidence cannot repair this terminal result.

**DATA_UNAVAILABLE:** reproduce positive authoritative endpoint evidence, such as
the exact verified delisting boundary, suspension/no-trade proof, unsupported
classification or special trading regime. Retain its exact reason and any nullable
endpoint close the evaluator produced. Missing bars alone never establish this
terminal state. Do not inspect today's presence or absence.

**BASIS_UNCERTAIN:** reproduce positive authoritative evidence of unsupported
comparability and its exact reason. Missing clearance, UNKNOWN/UNRESOLVED flags,
price jumps and free text never prove a terminal event. If required linked proof is
missing, INPUT_NOT_AVAILABLE; if all exact inputs are intact but do not reproduce
the stored terminal declaration, DIFFERENT_RESULT. Neither case silently accepts it.

Preserve the Outcome contract's safety for stock split/reverse split, rights,
bonus shares, stock dividend and conversion/merger regimes: no inferred factors,
entitlements, unit conversion or adjusted comparison. Cash dividends retain
price-only semantics, with no total return/reinvestment adjustment.

**Existing evidence ceiling:** the retained instrument model has continuity/event
coverage flags, not typed split/rights/merger event facts. The current frozen
evaluator's reproducible BASIS_UNCERTAIN branch is a positively cleared endpoint
whose source convention differs from the captured anchor (`PRICE_CONVENTION_UNSUPPORTED`).
The broader action reason names permitted by storage do not create event evidence
or executable branches. V0.1 verification must not add them to the frozen evaluator.
With intact existing inputs, an action-terminal declaration that the frozen
evaluator cannot reproduce yields DIFFERENT_RESULT. A required unavailable input
yields INPUT_NOT_AVAILABLE first. Missing typed action facts are an existing
Outcome readiness limit, not a migration required to replay its implemented branches.

## 8. Bounded manifest validation

Authenticate both retained manifests. The outcome manifest is schema 1 and has
all fields of the existing `OutcomeManifest`; nullable anchor/endpoint/listing/
terminalCondition fields must be represented according to schema. Nonnullable
collections may be empty but cannot be absent/null. Reject malformed types,
unknown schema fields, invalid IDs/dates/clocks/hashes, out-of-bound data and
duplicate retained identities. JSON object property order is not meaningful;
PostgreSQL jsonb does not retain original serialization or duplicate source keys.
Validate the stored typed representation, not invented original JSON bytes.

Check these relationships without inferring missing facts:

| Manifest component | Required validation |
|---|---|
| Versions and identity | Supported outcome binding; instrument/horizon equal the requested composite key; capture binding/hashes equal exact parent header/manifest. |
| Evaluation boundary | `evaluationCutoff` equals original `outcomeKnownAt`, with original chronology preserved. |
| Anchor/endpoint links | Exact subject ID, positive revision, valid date/knowledge/hash/artifact identity; anchor cross-links capture selection; endpoint belongs to the originally evidenced horizon. |
| Listing | Exact subject ID/knowledge/content-hash link, or explicit original absence. |
| Calendar | One first ANCHOR date; contiguous, strictly ordered unique dates; permitted frozen classifications; exactly requested trading-session count; required proof identities and endpoint sequence reproduce. |
| Instrument snapshots/status proofs | Subject scope, valid selected identities/knowledge/source facts and equality to original archive selection; no duplicate complete identities; tied distinct identities remain conflict evidence. |
| Archives | Exactly one each of `screener-reference`, `sessions`, `instrument-sessions`; fixed kind names and valid present/hash/length declarations. |
| Terminal condition | Nullable bounded original result assertion; compare to replay reason, not a substitute for positive terminal evidence. |

Canonical ordering: calendar order is semantic and must not be sorted to conceal
a difference. Identity sets use the existing normalized hashing/comparison rules;
the writer does not impose a new byte-serialization order on reference arrays.
Do not reject an otherwise authentic set merely for JSON/property order. Retain
the archived evidence order/tie semantics used by the frozen selector.

Input-shape/link/hash failures are INPUT_NOT_AVAILABLE. Stored state/reason/date/
close/return assertions belong to result comparison. Do not first require their
equality to the manifest as an input gate: synthetic changes to those assertions
must reach DIFFERENT_RESULT when inputs are intact. A shape-valid
`terminalCondition` that differs from replay is a result difference; a malformed
terminalCondition is a malformed input. Unsupported row policy/schema still wins
over malformed manifests.

## 9. Archive and canonical integrity

For each present reference archive, derive only the existing fixed internal
content-addressed identity from its retained SHA-256 and kind. Validate exact byte
length, exact bytes/EOF and content SHA-256 before parsing the in-memory copy.
Missing file, changed bytes, length/hash mismatch or invalid bounded parsing:
INPUT_NOT_AVAILABLE. `present=false` requires null hash/zero length and reproduces
the original empty-input semantics. Do not read live files for that kind.

No path is accepted from a caller or manifest. Do not fetch replacements or call
providers. A different archive with equivalent-looking records is not a replacement;
existing identical content-addressed SHA/length identity may share the same bytes.
Authenticate metadata for each link even when bytes are shared between bundles.

For canonical bars, exact revision must exist with matching retained identity and
raw-artifact metadata linkage. Recompute the existing canonical content hash and
validate original source knowledge/retrieval/session bounds. Listings retain exact
revision/knowledge/hash integrity under the original boundary. No latest-row SQL
or registry fallback. Missing exact revision/link or failed self-hash authentication
is INPUT_NOT_AVAILABLE. Authenticated facts that differ from the committed typed
outcome projection produce DIFFERENT_RESULT after replay. Later revisions do not
change either selection or result.

Reference archive validation does not imply replaying provider parsing or fetching
raw payloads. Preserve existing canonical identity/hash rules. If canonical numeric
data cannot be authenticated by the frozen exact validator, fail INPUT_NOT_AVAILABLE;
do not waive the check, coerce to float or quietly accept an invalid hash because
an outcome branch short-circuits price calculation.

## 10. Typed verification projection and MATCH

Freeze comparison of these schema-defined typed values:

- Exact outcome policy/schema and parent capture binding.
- `runId`, `instrumentId`, `horizonSessions` and authenticated parent relationship.
- Original captured target/session context and anchor market date/close.
- Recomputed horizon market date/close, terminal state/reason and priceReturnPct.
- Original evaluation-cutoff binding and recording/capture chronology.
- Required retained anchor/endpoint/listing/reference/session/archive identities,
  calendar and manifest terminalCondition, authenticated or compared as above.

Policy binding and evidence integrity are prerequisites; output assertions are
compared only after replay. Use exact typed nullable equality: null is distinct
from zero/empty; decimal equality is numeric .NET decimal equality, dates are exact
DateOnly values, timestamps compare UTC instants, enums/strings retain ordinal
contract spelling, ordered calendars retain order. No JSON whitespace, decimal
display scale, labels, localized formatting or current `verifiedAt` comparison.
`materialized`/`newlyMaterialized` presentation flags are not analytical results.
Do not manufacture a new `recordedAt`; only authenticate its retained chronology.

MATCH requires every integrity prerequisite and complete exact typed reproduction.
It means **reproducible under the supported frozen policy and exact retained
evidence**. It does not mean profitable, a correct prediction, a successful trade,
complete market coverage or strategy validation. A reproduced terminal unavailable
outcome can be MATCH.

## 11. Diagnostics and future evidence isolation

Diagnostics are structured and bounded: **`field`, `storedValue`,
`recomputedValue`, `reason`**. Use stable reason codes and deterministic field
ordering; maximum **100** differences with `differencesTruncated`. Reuse Decision
Verification display bounds: field <=240 characters plus truncation marker, each
value <=160 plus marker; reason/detail codes <=128. Null remains null. Detecting
any difference prevents MATCH even when subsequent diagnostics are truncated.
Unavailable states provide a sanitized stable detail code; do not pretend that
replay completed or populate invented recomputed facts.

Never expose private filesystem paths, secrets, raw payload bodies, arbitrary SQL,
exception internals or unrestricted evidence dumps. A difference can identify
the bounded typed field, date/hash/identity and reason needed to investigate.

Regression guarantee: after commitment, adding later canonical revisions, unrelated
session proofs, newer basis/status/action/listing facts, new snapshots/outcomes,
portfolio changes, another Screener policy or current registry changes must leave
MATCH unchanged for intact exact inputs. This includes later imports with earlier
domain knowledge timestamps: they are outside the recorded input universe.
Removing/changing a required exact input is an integrity failure, not future-evidence
isolation. No archive or evidence recovery through network/provider calls.

## 12. Decision Verification and Research independence

Outcome Verification authenticates the snapshot context it consumes directly.
It must not use a current Decision Verification status as proof of its own result
or require the user to run Decision Verification first. Decision Verification MATCH
does not imply Outcome Verification MATCH; they reproduce different immutable
artifacts. Reuse authentication primitives, not another artifact's verdict.

Research V0.1 inclusion does **not** require an Outcome Verification state.
Verification is an integrity tool, not a selection filter. AVAILABLE returns and
all N/A/T/R/U denominators follow the unchanged Research contract. A future Research
UI may show verification facts separately; it cannot silently remove returns based
on verification or invent persisted verification history. FullIdx and soak are
outside this read-only integrity operation.

## 13. Narrow API and HTTP contract

FROZEN proposed route, consistent with existing snapshot verification POST and
row-specific outcome-read routes:

`POST /api/screener/decision-snapshots/{runId}/rows/{instrumentId}/outcomes/{horizonSessions}/verify`

Body: **`{}` only**, a present empty JSON object. Both UUIDs use the existing
nonempty `Guid.TryParseExact(..., "D", ...)` route semantics; horizon is exactly
1/5/10/20. Reject extra body properties, nonobject/empty/malformed body, invalid
UUID/horizon and any query parameters. No caller policy, cutoff, expected result, knownAt, recordedAt,
evidence identities or verification clocks. Server derives inputs from the row.

Response identifies the exact key, state, supported/stored policy and schema,
original outcomeKnownAt/recordedAt and current verifiedAt; includes bounded
differences/truncation and nullable sanitized detail. Do not expose manifests or
provider payloads. An unsupported policy response reports its stored binding
without pretending a supported implementation exists.

| HTTP | Semantics |
|---|---|
| 200 | Any of MATCH, INPUT_NOT_AVAILABLE, POLICY_VERSION_UNAVAILABLE, DIFFERENT_RESULT. |
| 404 | Snapshot/run or captured row missing, or no committed terminal outcome for the exact key, including unresolved/unmaterialized cells. |
| 400 | Invalid UUID/horizon/body or attempted caller overrides. |
| 503 | Database/runtime/archive service unavailable or deadline prevents a trustworthy verification attempt. |

Reuse `SNAPSHOT_NOT_FOUND` / `SNAPSHOT_ROW_NOT_FOUND` where applicable; exact
unmaterialized subject uses `OUTCOME_NOT_FOUND`. A specific referenced archive
missing/corrupt after a functioning store lookup is INPUT_NOT_AVAILABLE/200.
General archive-store inaccessibility or interrupted I/O that prevents trustworthy
validation is service unavailability/503. Do not map a valid mismatch to HTTP failure.

## 14. Transaction sequence, deadlines and capacity

One bounded **REPEATABLE READ, READ ONLY** database transaction:

1. Validate request, start the overall deadline and read the exact outcome/parent
   key/header. Resolve exact frozen policy/schema before evidence interpretation.
2. Load/validate both manifests, original capture projection and exact referenced
   database facts within this same transaction; do not change snapshots mid-attempt.
3. Copy each required retained archive once into bounded memory, authenticate
   SHA/length/EOF, parse those bytes and validate original selected identities.
   Keep this transaction open within the deadline; do not reread changing live files.
4. Reproduce original horizon and terminal typed result solely from these captured
   in-memory facts, compare, then finish the read transaction and respond.

Database facts cannot mix two transaction states; each archive's hash authenticates
the exact bytes used even if files change afterward. A failed read cannot be
replaced mid-attempt. No advisory write lock, insert/update/delete, last-verified
stamp, live Outcome Tracking assessment/materialization call, provider/network
recovery or verification persistence.

Reuse limits: **15 seconds per database command**, **60 seconds overall**, caller
cancellation propagated, **100 diagnostics**, **64 KiB UTF-8 outcome manifest**,
**32 MiB capture manifest**, existing **80,000 captured canonical-bar** bound,
**210 capture rows**, and existing **10,000 reference records / 4 MiB copied bytes
per three-kind archive bundle**. At most the capture and outcome bundles are needed
(8 MiB total, six links before identical-hash reuse). Forward database evidence
for this unit is at most one exact endpoint and one exact listing; do not traverse
the original forward date-window bars or other outcomes. Horizon traversal retains
the frozen <=366 civil-date PILOT ceiling. Keep response within the existing
2 MiB public response convention. Reject bounds explicitly; never truncate inputs.

## 15. Persistence decision and real operational state

**No migration or new persistence.** Existing schema-1 outcome/capture manifests,
exact canonical revisions and copied reference archives suffice for deterministic
replay of the current evaluator's committed branches. Persist no verification
state, verifiedAt, history or diagnostic record. No migration 0007 is proposed.
If implementation discovers a genuinely required original input not retained by
these manifests, stop and report a design blocker rather than redesigning storage
or borrowing new evidence.

Reported real state: **one run, 10 captured rows, 40 unresolved horizon cells,
0 committed terminal outcome rows**. There is currently no real outcome subject
to verify; each requested unmaterialized key returns 404. This does not block future
implementation. Acceptance can use disposable synthetic committed outcomes in
isolated test databases/archives until genuine prospective outcomes exist. Do not
fabricate operational rows or claim a real Outcome Verification MATCH today.

The runbook/task report schema versions **2, 4, 5, 6**, FullIdx **DISABLED**, soak
**1/10**. These operational facts were not re-queried by this documentation review.
No provider calls, data writes or collection are required.

## 16. Frozen acceptance test plan

Future implementation must leave focused runnable checks through standard test
discovery and disposable service/API fixtures. No suites are required or claimed
for this design-only change.

1. **MATCH for all four stored states:** exact AVAILABLE decimal result (including
   zero/negative/native repeating division); authentic ANCHOR_UNAVAILABLE absence
   and stale/basis/status cases; positive DATA_UNAVAILABLE proofs; positive
   source-convention BASIS_UNCERTAIN. Unavailable MATCH is explicitly valid.
2. **Missing versus absent:** recorded null links/absent archives preserve original
   unavailable replay; loss of a required linked capture/endpoint/listing/reference
   input gives INPUT_NOT_AVAILABLE. Never query a replacement for explicit absence.
3. **Integrity failures:** missing manifest fields, malformed/oversized manifest,
   duplicate identities, wrong archive kind/identity, hash/length/changed bytes,
   missing exact canonical revision/raw-artifact linkage, canonical self-hash
   failure, missing session/capture/basis/status/action evidence. No fallback.
4. **Policy precedence:** unsupported outcome policy or schema returns
   POLICY_VERSION_UNAVAILABLE even with missing/corrupt evidence; unsupported
   capture interpretation likewise cannot fall back. Supported binding with invalid
   manifest schema gives INPUT_NOT_AVAILABLE. Test precedence with result tampering.
5. **DIFFERENT_RESULT fixtures:** alter stored horizon date, return, terminal state,
   reason, anchor close/date or horizon close while required exact inputs remain
   intact. Compare terminalCondition assertion too. Completed unresolved replay
   against a stored terminal state differs; no new verification status appears.
   Use disposable fixtures; do not disable operational immutable guards.
6. **Horizon/chronology:** target excluded, exact +1/+5/+10/+20 ordinals, closures/
   weekends/exceptional opens, no bar-derived sessions or endpoint sliding, original
   capture and outcome cutoffs reused, verifiedAt later than original clocks,
   future-known archived records excluded. Proved different horizon is a mismatch;
   missing/corrupt proof is unavailable input.
7. **Basis/action safety:** hash-covered whole-span raw comparability; missing
   clearance remains unresolved; UNKNOWN/free text/jumps do not prove an action;
   stored unreproducible split/rights/merger declaration cannot MATCH. Preserve
   cash-dividend price-only treatment and checked overflow/precision rules.
8. **Future isolation:** add later canonical revision, session proof, basis/status/
   action/listing facts, capture/outcome rows, portfolio and registry changes;
   MATCH remains unchanged. Include later-imported earlier-known facts and live
   file replacement, plus verification without first running Decision Verification.
9. **Bounded diagnostics:** exact null/decimal/date/UTC comparisons, cosmetic
   formatting excluded, deterministic max-100 differences/truncation, no paths/
   secrets/raw bodies/internal SQL. Any detected difference prevents MATCH.
10. **API/service:** exact route and {} body, invalid UUID/horizon/override 400,
    absent/unresolved/unmaterialized exact subject 404, four states all 200, genuine
    service unavailability/deadline 503. Read-only single snapshot, no writes or
    verification persistence; immutable fingerprints unchanged under concurrent
    later evidence. Archive changes during read fail closed unless the copied
    bytes still authenticate the exact retained content identity.
11. **Research/operational safety:** verification never filters research inclusion
    or AVAILABLE denominators; reported real outcome rows remain zero absent a
    separately authorized genuine materialization. Synthetic rows stay disposable;
    provider calls 0, FullIdx disabled, soak unchanged.

## 17. Decisions and subsequent implementation sequence

One-row verification is accepted. No schema/migration blocker was found for the
existing evaluator; no owner choice remains open. Exact typed corporate-action
event evidence is currently unavailable and must not be invented. Real terminal
outcomes are also currently absent; this affects real-data acceptance, not the
ability to implement/test the verifier with disposable evidence.

Recommended order:

1. Outcome Verification typed policy resolver, exact retained-input validation,
   pure replay/projection comparison and focused fixtures.
2. Bounded read-only service/archive integration and the single-row POST route;
   disposable HTTP, integrity, future-isolation and no-write acceptance.
3. Read-only verification of genuine committed outcomes when they exist, without
   manufacturing operational data to satisfy acceptance.
4. Research typed query / deterministic aggregation, then Research read API /
   dataset identity / JSON export, then Research Dashboard, then explanatory AI.

This preserves **Decision Snapshot -> Decision Verification -> Outcome Tracking
-> Outcome Verification -> Research Evaluation**. It is implementation sequencing,
not a verification prerequisite for Research inclusion. No implementation, commit
or push is authorized by this milestone.
