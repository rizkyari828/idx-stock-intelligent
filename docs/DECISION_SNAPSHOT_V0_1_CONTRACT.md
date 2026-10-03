# Prospective Decision Snapshot V0.1 — design freeze

Design reviewed: **2026-10-03, Asia/Jakarta**. Decision: **CONDITIONAL GO for a
separate implementation milestone after review**. This document freezes the
proposed implementation contract; it does not authorize implementation during
this design task. Storage schema version: **1**.

## 1. Purpose and boundary

Record an immutable, factual evaluation: what the existing PILOT Screener knew
and returned at an actual prospective capture time, including rejected and
blocked instruments. A capture can be COMPLETE, PARTIAL or BLOCKED and is useful
even when there are no candidates.

Reuse policy **screener-v0.1.0**, its features, episode rules, eligibility,
ordering, held union, reference selection and final inputHash. No recommendation,
order, prediction, automatic rule promotion, collection, backfill, outcome
calculation or separate episode state machine is included. Snapshot persistence
is an explicitly separate extension to the existing read-only Screener product.
It does not change `docs/SCREENER_V0_1_CONTRACT.md`.

Capture is manual through one local API write. No scheduling or UI is required
by the first implementation milestone. Existing Screener and Stocks GETs retain
their behavior and never create snapshots as a side effect. Storage is PostgreSQL
plus retained copies of the reference files; no new service or dependency.

## 2. Reviewed implementation and repository condition

Baseline: `738ca89` on `main`, four commits ahead of `origin/main`. The working
tree initially has one modification: a pasted Git command block before the
title of `docs/SCREENER_V0_1_CONTRACT.md`. Its policy body is unchanged. That
pre-existing edit is preserved, not executed or repaired by this task. The
committed contract at HEAD is the policy baseline. Resolve the unrelated edit
with its owner before starting implementation; no silent cleanup is authorized.

Source findings that govern this design:

| Existing component | Consequence for capture |
|---|---|
| `ScreenerService.ReadAsync` copies references, then owns a read-only repeatable-read transaction | Share its evaluation core; a capture needs its own read/write repeatable-read transaction, not a GET followed by a save |
| `ScreenerEvaluator` produces all configured rows plus positive held positions | Persist that complete result before presentation filters/paging |
| `ScreenerPresentation.Map` filters/pages discovery, then appends held rows | Its default response is not the capture population |
| `ScreenerEvidenceDatabase.ReadAsync` accepts a caller transaction and selects temporal visibility before latest revisions | Reuse this bounded read and retain the exact selected revision keys, including invalid selected revisions |
| `ScreenerReferences.SelectedDigest` and `ScreenerPresentation.InputHash` already hash evidence and portfolio chronology | Store their existing outputs unchanged; introduce no second semantic hash |
| `ScreenerReferenceFiles.LoadAsync` copies/parses fixed files, but only returns the parsed bundle | Retain the same copied bytes used for evaluation; do not reread the live files after evaluating |
| `RawArtifactArchiver` already writes content-addressed files | Reuse it for reference retention and verify the resulting file hash/length, including an already-existing destination |
| `PortfolioDatabase.ScreenerHistoryAsync` and `PortfolioLedger.Project` supply bounded cutoff-visible history and through-date economics | Obtain factual holding context in the same transaction; do not call a separate portfolio GET |
| Stocks history retains observed dates and filters all relevant knowledge/retrieval clocks | A decision's target/row market date must retain its actual date, not be relabelled as through |
| Migrations `0001`–`0004` and `reject_evidence_mutation` establish ordered, append-only PostgreSQL conventions | Use one additive migration and narrow table protections |
| Worker qualification and collector `soak_progress` count successful prospective DAILY collections and unique market dates | A decision capture is not a qualifying collection or a soak report |

Existing Screener evidence, response, evaluator, episode, database and stock
history tests provide reusable invariants. Their previous passing results are
not fresh verification of this design or a future implementation.

## 3. Granularity and identity

Use **one run header plus one row per evaluated instrument**. Universe, market
context, clocks, evidence manifest and portfolio input linkage belong to the
header once. Rows use stable instrument IDs and retain their own evaluation and
factual holding context. This permits future outcomes to reference a specific
row without duplicating the whole run's evidence for every instrument.

- `runId`: server-generated UUID, generated once for a new accepted capture.
- `requestId`: required caller-generated nonzero UUID, globally unique in the
  local snapshot store; retries reuse it. It identifies one capture intent.
- Row identity: **(runId, instrumentId)**; no additional row UUID.
- Policy identity: existing `screener-v0.1.0`, not a new setup policy.
- Episode identity: the existing policy/instrument/start-date identity,
  verbatim. Repeated captures of an episode do not create new episodes.

There is no uniqueness on market date, episode ID or inputHash. A genuinely new
request can capture changed knowledge/portfolio context on the same market day.
Reusing a requestId cannot overwrite or create a second copy of the same capture.

Population is all knowledge-visible configured members, union all positive held
positions at the same through/cutoff, deduplicated by instrumentId. Store known
exclusions, DATA_BLOCKED rows and outside-universe held rows. The benchmark is
run market context; it is not added to discovery rows merely because it is read.
Persist full ranked-candidate, all-view, shortlist and held ID arrays from the
existing result. Shortlist capacity remains presentation metadata, not a storage
filter. An unknown universe can produce an honest BLOCKED header with zero rows,
or held-only rows; configured counts remain null when unknown.

## 4. Prospective chronology

V0.1 writes only **PROSPECTIVE_CAPTURE**. Historical GET replay remains separate;
there is no persisted REPLAY writer in this milestone.

For a new capture, resolve one actual UTC instant from PostgreSQL
`clock_timestamp()` inside the capture transaction before reading evidence:

- `capturedAt = knowledgeCutoff = that instant`.
- `through` defaults to that instant's **Asia/Jakarta civil date**. If supplied,
  it must equal that date, and still satisfy the existing Screener horizon
  `2026-08-24` through `2027-08-24`. Older or future requested through dates are
  rejected. This deliberately keeps historical reconstruction in the GET path.
- `targetSession` is the existing evaluator's nullable, independently confirmed
  market session; each row's `marketDate` is the existing actual observation date.
  A weekend capture may legitimately target an older completed session. An
  unproven session/gap remains BLOCKED under the existing rules.
- `recordedAt` is PostgreSQL `clock_timestamp()` when the final header is
  inserted, after evaluation. It must be at least capturedAt. It is the recording
  timestamp, not a claim to be the exact commit timestamp. Success is returned
  only after commit.

Do not accept `cutoff`, `capturedAt`, `recordedAt`, `knownAt`, targetSession,
policyId or a precomputed result from HTTP. Never backdate clocks, manufacture a
completed session or label an old close with today's economic date. A capture
that crosses midnight retains its resolved through/cutoff, rather than resolving
them again. A new request after the policy horizon ends fails validation; an
idempotent retry of an existing capture remains readable.

The database view means **committed facts visible to this transaction**, further
restricted by the existing inclusive knowledge/source cutoffs and through date.
It does not claim to see another transaction's uncommitted import. The manifest
preserves this distinction if a later import has an older supplied knownAt.

## 5. Input hash and retained evidence

Store `inputHash` exactly from `ScreenerPresentation.InputHash`, and
`selectedDigest` exactly from `ScreenerReferences.SelectedDigest`. The final hash
already incorporates selected evidence, policy/request chronology, portfolio
header/history and relevant held thesis chronology. Do not add a separate
portfolio fingerprint, result hash or competing canonicalization algorithm.

Store one versioned, bounded **selection manifest**, not a copy of raw OHLC or
every evidence document. It identifies the inputs actually passed to the engine:

| Manifest content | Retained identity/check |
|---|---|
| Resolved request, benchmark and evaluated population | Through/cutoff/anchor, selected instrument IDs, benchmarkId, configured/held/evaluated ID sets |
| Each selected bar, including a selected invalid revision | (instrumentId, sessionDate, revisionNumber), knownAt, canonical content hash, rawArtifactId |
| Each selected listing assertion, including invalid/legacy assertions | (instrumentId, knownAt), existing listing evidence hash |
| Selected universe/instrument/reference/calendar/status records | Existing snapshot IDs/hashes or session/status keys and the same selection's identities |
| Portfolio, when supplied | Immutable portfolio ID and exact event IDs/read thesis version IDs needed to reconstruct the history passed to the engine; do not substitute only the currently active economic events |
| Absence | Explicit empty selections/missing-file markers, nullable target/universe identity and selected ID sets; no registry fallback or invented missing rows |
| Reference bundle copies | Fixed file kind, presence, archived byte length and existing archiver SHA-256 for each copied file |

Absence is reconstructed from the retained selection and bundle, not by querying
what happens to be absent today. Portfolio source records are retained in their
existing tables, with their original knownAt, event ordering and correction
links; the manifest stores identifiers, not private event notes or thesis text.
Listing and bar selection never fall back from an invalid selected revision.

Retain exact copied bytes of `pilot/screener-reference.json`, `pilot/sessions.json`
and `pilot/instrument-sessions.json` used by this evaluation, including revisions
not selected by cutoff. Reuse the application's parser/hash validation and
selection rules on those copies during verification. Missing files retain the
existing loader's empty/missing semantics. `pilot/universe.json` is operational
configuration, not a historical universe fallback or capture input.

Use the fixed internal archive root `data/raw/decision-reference`, under the
existing ignored `data/raw/**` directory, resolved by the existing
repository/content-root startup convention. Reuse
`RawArtifactArchiver` for content-addressed storage; hash/length verification is
required even when a destination already exists. Byte checksums are archive
integrity checks, not another Screener input hash. Do not create ingestion runs,
raw_artifact database rows, arbitrary path parameters or a document warehouse.

Copy/parse/archive/verify the bundle before starting the database evaluation;
pass precisely that parsed bundle into the transaction. The database and three
filesystem publications are not globally atomic. The contract is one retained
copied bundle plus one database snapshot, matching the existing loader's
semantics. Do not imply a simultaneous publication across reference files.
Every referenced archive must exist and pass integrity checks before commit.
A failed capture may leave harmless unreferenced content-addressed files; it
must not leave a persisted partial run. No automatic archive deletion in V0.1.
Backups/restores must include both PostgreSQL and these archives.

## 6. Minimal stored result

Store a deliberate version-1 analytical projection, not serialized application
objects or an unrestricted copy of every HTTP DTO. Use existing enum strings,
reason strings, numeric precision, units and availability semantics unchanged.
No rounding, threshold recomputation or conversion of unavailable values to zero.

**Run result:** overall status/reasons; the existing summary with nullable counts;
ordered rankedCandidateIds/allViewIds/shortlistIds/heldIds; benchmark context
containing instrumentId, marketDate, close, trend, volatility, EMA20/EMA50,
ATR14/ATR%, reasons, fieldStates and the bounded provenance described below.
Header columns supply policy, universe/snapshot, dates, hash and portfolio ID.

**Instrument result:**

- Stable instrumentId; nullable symbol as known by the evaluator at capture
  (display only); configured, held and nullable discoveryRank.
- Eligibility and its ordered reasons; setup, setupEvaluated and ordered setup
  reasons. Retain NONE, WATCH, CONFIRMED and FAILED exactly as produced.
- Nullable existing episode object: id, startDate, confirmationDate, endDate,
  endReason, ageSessions, confirmedAgeSessions, triggerPrice and watchThreshold.
  Copy all of these fields; do not create additional lifecycle dates.
- Actual nullable marketDate and close; priorHigh20, distanceToHighPercent,
  EMA20, EMA50, ATR14, ATR%, volumeRatio20, rs20Pp, rs60Pp and trend.
  Also retain monetaryLiquidity20Idr because it participates in candidate order.
- Existing stale, noTrade, tradingStatus, dataQuality, dataReasons, and
  fieldStates for the retained features.
- Bounded provenance: priceBasis, source, currency, volumeUnit, volumeBasis,
  marketSegment, canonicalQuality, observation revision/knownAt/retrievedAt/hash,
  priceSequenceStart, emaSeedStart, atrSeedStart, consecutiveSessions and selected
  referenceSnapshotIds. Retain the separate nullable currentEvidence link
  (sessionDate, revision, knownAt, retrievedAt, contentHash, canonicalQuality,
  source) unchanged: it may identify a current invalid revision while observation
  identifies an older usable price. Do not merge those meanings. The manifest
  holds the full selected revision linkage.
- Factual portfolio position context described in section 7.

Exclude raw OHLC arrays, raw provider payloads, full revision sequences,
issuer descriptions, UI pagination, transient messages, private thesis text,
changePercent, raw volume, dailyValueProxy, portfolio valuation/P&L and action
labels. Those are not required to reproduce this version's stored analytical
projection; retained canonical evidence remains the source for raw observations.

Preserve sparse `fieldStates` semantics: a populated retained feature with no
state entry is AVAILABLE; an unavailable feature is null with its explicit
availability/reason. WARMUP, UNKNOWN and UNAVAILABLE retain their distinctions.
Structural nulls such as no episode, no portfolio, no rank or no active mandate
are not missing technical features. Genuine numerical zero remains zero.

Outside-universe membership and setup state are separate. `setupEvaluated=false`
does not establish evaluated setup NONE, even if the existing setup enum is NONE.
For an unknown universe, `configured=false` means membership was not established;
the nullable universe snapshot/counts retain that uncertainty. Do not relabel it
as a proven outside-universe exclusion. An instrument absent from the captured
population is **not captured**, not NONE, INELIGIBLE or a fabricated row.

## 7. Portfolio context

Portfolio is optional. Without one: portfolioId, shares, investedCost,
averageCost and thesis metadata are null; held=false denotes no supplied holding
context, not proof about the user's other portfolios.

With a portfolio, use `ScreenerHistoryAsync` and `PortfolioLedger.Project` within
the capture transaction, at precisely the same cutoff and through. Positive
shares define held; future-economic trades and cutoff-invisible corrections are
excluded by the existing projection. Preserve its exact shares, investedCost and
averageCost. For a configured unheld row these are 0, 0 and null respectively.
Never recompute average cost using closes or treat average cost as support.

Retain the factual latest visible thesis version ID, version and active flag for
held rows, when present. Mandate is the evaluator's nullable active mandate;
an inactive version does not reinstate an old mandate. No thesis text or new
thesis/correction machinery. Non-held rows do not acquire a mandate from old
theses. Portfolio chronology is already covered by inputHash; this projection
is stored context, not an additional accounting engine.

## 8. Proposed PostgreSQL schema

Add only `decision_snapshot_run` and `decision_snapshot_row`. The following
types/constraints are the implementation contract, not an applied migration.
All timestamps are `timestamptz`, serialized as UTC. Numerics use PostgreSQL
`numeric` and the existing .NET decimal range without display rounding.

### decision_snapshot_run

| Column | Type and rule |
|---|---|
| run_id | uuid primary key, nonzero, server generated |
| request_id | uuid not null unique, nonzero |
| schema_version | smallint not null, CHECK = 1 |
| capture_kind | text not null, CHECK = 'PROSPECTIVE_CAPTURE' |
| captured_at, knowledge_cutoff | timestamptz not null, CHECK equality |
| recorded_at | timestamptz not null, database clock at insertion, CHECK >= captured_at |
| through, history_anchor | date not null; anchor 2026-08-24, through within frozen inclusive horizon |
| target_session | date nullable, when present anchor <= target_session <= through |
| policy_id, universe | text not null, CHECK 'screener-v0.1.0' and 'PILOT' |
| universe_snapshot_id | text nullable; unknown stays null |
| portfolio_id | uuid nullable, FK portfolio(portfolio_id), ON DELETE RESTRICT |
| input_hash, selected_digest | text not null, CHECK lowercase 64-character hexadecimal |
| status | text not null, CHECK COMPLETE/PARTIAL/BLOCKED |
| row_count | smallint not null, CHECK 0..210 |
| request_intent | jsonb not null, strict version-1 normalized request |
| result | jsonb not null object, section 6 run projection excluding header scalar columns |
| evidence_manifest | jsonb not null object, section 5 version-1 manifest |
| originating_xid | xid8 not null, database assigned current transaction ID; internal insert guard only |

`originating_xid` is not an economic date, replay identifier or retained MVCC
snapshot. The application cannot supply/override it. It exists solely to prevent
rows being attached after the creation transaction.

### decision_snapshot_row

| Column | Type and rule |
|---|---|
| run_id | uuid not null, FK decision_snapshot_run(run_id), ON DELETE RESTRICT |
| instrument_id | uuid not null, nonzero; composite primary key (run_id, instrument_id) |
| symbol | text nullable, captured display identity only |
| configured, held | boolean not null |
| discovery_rank | integer nullable, positive when present; unique (run_id, discovery_rank) for non-null ranks |
| eligibility | text not null, CHECK ELIGIBLE/INELIGIBLE/DATA_BLOCKED |
| setup | text not null, CHECK NONE/WATCH/CONFIRMED/FAILED |
| setup_evaluated | boolean not null |
| episode_id | text nullable, must agree with nullable stored episode object's id |
| market_date, close | date and numeric nullable; observed date <= run through; usable close positive |
| shares, invested_cost, average_cost | numeric nullable; section 7 null/zero/positive semantics |
| mandate | text nullable, existing active mandate enum only |
| thesis_version_id, thesis_version, thesis_active | uuid/integer/boolean nullable, existing held chronology |
| result | jsonb not null object, section 6 row projection excluding the scalar columns above |

No FK from instrument_id to the mutable instrument registry: a legitimate
configured stable ID with missing registry/evidence must still be capturable as
DATA_BLOCKED. Portfolio existence is validated and referenced only when supplied.
Thesis IDs and source-evidence keys are verified against the selected immutable
history and retained in the manifest, not new cross-table cascading relationships.
No alterations to portfolio, events, thesis, bars, raw artifacts or registry.

Indexes: unique request_id; primary keys; run `(captured_at DESC, run_id DESC)`;
run `(portfolio_id, captured_at DESC, run_id DESC)`; row `(instrument_id, run_id)`;
partial unique `(run_id, discovery_rank)` for ranked rows. Do not add a generic
JSON index or an outcome table yet. Instrument history joins runs for chronology;
measure this bounded personal query before adding duplicated timestamps/indexes.

Bounds: existing 10 configured/200 held/211 selected IDs, 366 civil dates, 80,000
selected bars, 10,000 portfolio events, 2,000 theses, 4 MiB per reference file and
10,000 reference records still apply. At most 210 union rows, including held rows
outside discovery. Add capture serialization bounds: run result <=32 KiB,
each row result <=8 KiB, complete public run response <=2 MiB, selection manifest
<=32 MiB. Measure actual UTF-8 serialization; reject overflow, never truncate
reasons, evidence, held rows or arrays to fit. These are capture service bounds,
not altered Screener eligibility/feature requirements.

## 9. Immutability and atomic completeness

Reuse the existing append-only rejection function for BEFORE UPDATE OR DELETE
on both new tables; also reject TRUNCATE using BEFORE TRUNCATE statement triggers.
Do not grant new mutation privileges. A schema-owning administrator can bypass
database protections during controlled restoration; application APIs cannot.

Controlled owner restores load data before reinstalling/enabling guards, then
check every restored run's completeness before serving it. Preserve historical
originating_xid values; do not expose a restore/import bypass through HTTP.

UPDATE/DELETE rejection alone is insufficient: later INSERTs could change an old
run's population. A row BEFORE INSERT guard requires its parent run's
originating_xid to equal the database's current transaction ID. A deferred
constraint trigger on header INSERT at commit requires exact row_count and exact
instrument set equality with the manifest's evaluated population, and validates
cross-row/run
invariants (dates, episode linkage, held/rank arrays and portfolio null semantics).
It must work for zero-row BLOCKED runs too. JSON projection shape/version and
byte bounds are validated before insertion; database size/shape checks reinforce
the fixed envelopes.

Insert the final header and all final rows in one transaction. There is no
mutable PENDING/COMPLETE lifecycle, no later row filling, UPSERT, deletion,
correction or superseding endpoint. A later corrected evaluation is a new run
with its own actual capture clock; the older result remains intact.

## 10. API contract and idempotency

Proposed write: **POST /api/screener/decision-snapshots**.

Strict JSON object, only `requestId` (required UUID), `through` (optional date or
null) and `portfolioId` (optional UUID or null). Universe is fixed PILOT.
Unknown/duplicate fields, malformed UUIDs/dates, client clocks, cutoff, policy,
filters, offsets, limits, supplied results and alternate universes fail 400.
Use the existing JSON/body-size (64 KiB), same-origin and loopback protections.

No `expectedInputHash` in V0.1: the current hash includes cutoff. A previous
GET with a resolved Now cutoff cannot generally match a fresh prospective
cutoff, even if source evidence is unchanged. The POST captures its own fresh
evaluation and returns its stored result; it does not claim to save the exact
earlier displayed GET. A later UI must display/acknowledge the returned capture.
Designing another pin/session token is deferred.

Normalize original intent as `{through: date-or-null, portfolioId: UUID-or-null}`;
omitted and null are equivalent. Compare the stored normalized intent, not a
newly resolved current date/cutoff on a retry. Explicit through and omitted
through are distinct intents. No additional intent hashing system is needed.

| Outcome | HTTP / stable code |
|---|---|
| New committed run, including PARTIAL/BLOCKED | 201, Location run GET, full retained public run |
| Same requestId and same normalized intent | 200, same original run/result/clocks/hash; no reevaluation or new archive work |
| Same requestId with different intent | 409 REQUEST_ID_CONFLICT |
| Invalid body/context/date, including explicit past/future through | 400 SNAPSHOT_REQUEST_INVALID or PROSPECTIVE_THROUGH_REQUIRED |
| Valid portfolio UUID not found at capture cutoff | 404 PORTFOLIO_NOT_FOUND |
| Malformed/oversized/unreadable reference, database/archive/service failure or capture bound | 503, existing Screener code where applicable; otherwise SNAPSHOT_UNAVAILABLE, SNAPSHOT_ARCHIVE_UNAVAILABLE or SNAPSHOT_BOUND_EXCEEDED |

Existing transport protections retain their behavior: over-limit request bodies
return 413; non-JSON or cross-site POSTs return 415. Those middleware responses
need not have a snapshot-domain error body. Snapshot-domain errors above use a
sanitized `{code, error}` JSON object with no database/path internals.

An existing successful retry is served before enforcing today's through/horizon
or loading current reference files. Thus a lost-response retry on another day
returns the original run, even if the live files are now invalid. A unique
request_id constraint is the concurrency authority. If two transactions race,
one commits; the loser rolls back completely on uniqueness/serialization failure,
then uses a fresh read transaction to return that winner (200), or 409 for a
different intent. Never read an aborted/stale repeatable-read transaction or use
ON CONFLICT UPDATE. If no committed winner can be read within the deadline,
return retryable 503; retry the same requestId. Archive orphans are permissible.

Read APIs, all returning retained content with no recomputation or provider access:

- `GET /api/screener/decision-snapshots/{runId}`: one header plus all rows, up to
  210; arrays retain original order, rows deterministic instrumentId order. Do
  not expose internal transaction IDs, filesystem paths or the large manifest.
- `GET /api/screener/decision-snapshots?limit=20&cursor=...&portfolioId=...`:
  recent brief run headers, default 20, max 100, newest capturedAt/runId first.
  Optional portfolioId selects that portfolio; omission includes discovery and
  portfolio captures. No mutable rank/status-based filter in V0.1.
- `GET /api/instruments/{instrumentId}/decision-snapshots?limit=20&cursor=...`:
  header chronology plus stored row, same order/default/max. Absence returns an
  empty history, not a synthesized setup result or registry-dependent 404.

Use strict keyset cursors containing capturedAt/runId and bound filter context,
with a maximum encoded length of 512 characters; reject malformed/mismatched
cursors/unknown or repeated query keys with 400 SNAPSHOT_QUERY_INVALID. No
unbounded read, offset scan, archive download or SQL/path parameter. Valid absent
run UUID returns 404 SNAPSHOT_NOT_FOUND; invalid UUID returns 400. Lists return
200 even when empty. Read service failures return sanitized 503
SNAPSHOT_UNAVAILABLE. Brief headers omit row ID arrays/manifest/market payload;
each public response must fit 2 MiB without truncating individual stored records.

## 11. Transaction flow

1. Validate/normalize the request; look up an already committed requestId and
   return it on a matching retry. Otherwise copy, parse, archive and verify the
   fixed reference bundle. Do not call providers.
2. Open **one read/write REPEATABLE READ transaction**. Resolve the database clock
   once, then through; recheck the requestId. The authoritative evaluation reads,
   portfolio projection, hash generation and inserts all share this transaction.
3. Run the existing service core with that transaction and copied bundle:
   select universe, read cutoff-visible portfolio chronology, project holdings,
   select bounded bars/listing/reference/session evidence, evaluate, compute
   selectedDigest and inputHash. No second connection or separate HTTP reads.
4. Build the version-1 projection and exact selection manifest from those same
   in-memory inputs/result. Validate bounds and invariants. Insert final header
   with database recordedAt/originating_xid, then all rows.
5. Commit, including deferred completeness constraints, and return 201. Any
   failure rolls back the whole run. Concurrent requestId conflict follows
   section 10 with a fresh read after rollback.

Keep existing 15-second command timeouts/cancellation and add a 60-second overall
capture deadline. Cancellation before commit must leave no partial run. If commit
succeeds but response delivery fails, retry recovers it by requestId. Do not
automatically reevaluate a failed attempt with a different cutoff and pretend
it is the original capture. A request that never committed may be attempted
again with an actual new server cutoff; there is no stored prior run to overwrite.

The only necessary evaluation refactor is a shared transaction-aware core that
can expose result plus selected inputs; the existing GET continues owning its
read-only transaction and presentation mapping. Do not make GET writable,
instantiate another indicator engine or loosen selected-evidence validation.

## 12. Replay and verification design (later milestone)

No verification endpoint or result storage is implemented in V0.1. A later
read-only verifier loads schema version 1, the retained bundle, exact manifest
keys and the allowlisted original policy implementation. It reconstructs the
captured selection/history, validates clocks/hashes and explicit absence, then
reuses the existing selectedDigest/inputHash and evaluator to compare the typed
stored projection, including ordered ID arrays and reasons.

Do not simply rerun today's latest-as-of query: a late import committed after
capture with an old knownAt could enter that query. Replay must use the exact
captured selected keys and population, without replacing an invalid revision or
adding previously absent evidence. No new result hash is necessary; compare
schema-defined values semantically, with exact decimals/UTC instants and ordered
arrays. The existing input hash canonicalizer sorts arrays and is not a result
ordering checksum.

| Verification state | Meaning |
|---|---|
| MATCH | Retained inputs, original policy, existing hashes and complete stored analytical projection reproduce |
| INPUT_NOT_AVAILABLE | Required selected records/archive bytes are missing, corrupt or cannot be authenticated; specify the missing linkage |
| POLICY_VERSION_UNAVAILABLE | The allowlisted implementation of the captured policy/storage projection cannot be executed |
| DIFFERENT_RESULT | Inputs and original implementation are available but an existing hash or typed result differs; report differences without rewriting |

MATCH can reproduce a BLOCKED evaluation. It establishes reproducibility of the
captured result, not independent certification of a provider's external claims,
evidence readiness or a trading recommendation.

Future-only appends, live file replacements and portfolio corrections must not
alter a stored record. A missing old implementation is not permission to use a
new policy under an old ID. Retain reviewed policy source/builds with ordinary
release records; policy behavior changes need their proper version, never a
user-supplied plugin. Verification unavailable is an honest result, not deletion
or correction of the captured evaluation.

## 13. Future outcomes and retention

Future append-only outcome records may FK **(runId, instrumentId)** and reference
the copied episodeId/confirmationDate, their own evaluation cutoff, policy and
selected outcome evidence. They do not modify snapshot rows. No outcomes/table
are created now. Horizons +1/+5/+10/+20 use actual subsequent confirmed exchange
sessions and the frozen outcome conventions, not calendar days, imputed trades
or tomorrow's unavailable close. Repeated captures of one episode are correlated
observations; episode analysis must deduplicate episodes explicitly. A captured
close is descriptive and never claimed executable.

Retain all committed captures and their linked evidence/archives in V0.1. No TTL,
silent pruning, compression subsystem or daily duplicate cleanup. Exports and
backups include the two tables, canonical/portfolio evidence and archive root;
restore must pass immutable/run completeness and retained-byte checks. Plan
storage monitoring/manual capacity review before collection expands.

Illustrative storage, not a measured benchmark: 252 captures/year; mean 126
retained sessions per run, benchmark included; 200 bytes per selected-bar manifest
entry, 4 KiB per analytical row and 4 KiB other header content. Immutable source
bars themselves are already stored and are not copied into snapshots.

| Population | Manifest/year | Rows + headers/year | Approximate logical total | Planning envelope with DB overhead |
|---|---:|---:|---:|---:|
| 10 stocks + benchmark | 70 MB | 11 MB | 81 MB | 0.1–0.25 GB |
| 100 stocks + benchmark | 641 MB | 104 MB | 745 MB | 1–2 GB |

Listing/portfolio manifest entries, held union and reference archives are extra;
measure actual captures before treating these estimates as capacity guarantees.
Byte-identical references deduplicate. A newly different 4 MiB reference copy
every capture would add about 1 GiB/year **per file**, so archives must be included
in storage review. No repeated full raw documents inside row payloads. The
100-stock calculation is hypothetical capacity planning, not authorization to
change the fixed 10-member PILOT or existing service bounds.

## 14. Safety and failure behavior

Use existing loopback/local-only API conventions and same-origin write checks.
No authentication redesign/public hosting assumption. Return sanitized codes;
never expose connection strings, archive paths, provider credentials, arbitrary
SQL or full private notes/thesis text. Archive kinds/roots are fixed server-side.
No caller-supplied policy code, arbitrary cutoff, path or result.

Missing ordinary history/reference coverage, unknown instrument facts and an
unconfirmed current session produce the existing factual BLOCKED/PARTIAL result
and may be captured successfully. Malformed evidence, exceeded service bounds,
archive integrity/I/O failure or database failure prevent persistence with 503.
No invalid evidence is admitted to avoid a failed capture. Storage/disk errors
never produce a successful partial run. Duplicate recovery does not require live
reference readiness after an original run already committed.

Capture writes only its two new tables and reference archive; no portfolio,
canonical market, operation ledger, session proof, collector configuration or
soak report writes. No provider fetches, new dependency or FullIdx activation.
Design and implementation may proceed at **soak 1/10**, with incomplete PILOT
evidence. A captured COMPLETE evaluation does not count toward soak; a captured
BLOCKED evaluation does not invalidate an unrelated qualified collection.

## 15. Required implementation test plan

Use standard `dotnet test` discovery and the existing owned disposable PostgreSQL
harness conventions. Tests must exercise real database constraints/concurrency,
not just mirror in-memory object construction. No provider traffic.

1. **Chronology:** controlled server/database clock resolution; recordedAt is
   actual insertion time >= capturedAt=cutoff; request timestamps/cutoff/policy
   are rejected. Through omitted/explicit today works; older/future dates fail.
   A weekend target retains its earlier session date; no confirmation proof
   remains BLOCKED. Midnight and timezone offset tests resolve once. Evidence
   dated after through or any applicable knowledge/retrieval cutoff is excluded.
2. **Population/states:** capture COMPLETE/PARTIAL/BLOCKED, DATA_BLOCKED,
   INELIGIBLE, WARMUP/UNKNOWN/unavailable features, genuine zero and evaluated
   NONE/WATCH/CONFIRMED/FAILED. Preserve eligibility versus optional features,
   setupEvaluated=false, episode expiry/interruption and exact trigger equality
   through the existing engine. Full population exceeds shortlist/page sizes
   without losing rows; unknown universe zero-row and held-only runs retain null
   counts; held outside-universe/blocked/excluded rows survive exactly once.
3. **Portfolio:** no portfolio gives null factual context; configured unheld
   gives zero shares/cost and null average cost; held shares/average cost/mandate
   and inactive/latest thesis version match the same projection. Historical
   corrections, supersedes links, future-economic trades and knowledge-visible
   future trade events preserve existing chronology and inputHash behavior.
4. **Atomic consistency:** persisted analytical projection equals the full
   evaluator result, selectedDigest/inputHash equal existing functions exactly.
   Pause capture while another connection appends a market revision/portfolio
   correction: evidence and held context remain in the one original transaction
   view. A later import with older knownAt does not alter the manifest or a
   verification reconstruction. Missing/invalid latest revision never falls back.
5. **Retries/concurrency:** sequential identical requestId returns original
   run/hash/clocks without reevaluation; changed normalized intent returns 409.
   Retry after midnight/live reference failure returns original. Two simultaneous
   matching captures yield one run and one row set (201/200); differing intents
   yield one winner/409. Lost response after commit recovers original; failures
   before commit leave no header/rows. Exercise rollback/fresh read on uniqueness
   and serialization failures.
6. **Immutability:** SQL UPDATE, DELETE and TRUNCATE on either table fail; row
   insert in a later transaction fails; wrong counts, duplicate instruments/ranks,
   manifest-set/episode/date mismatch and portfolio null inconsistencies fail
   atomically. Zero-row runs commit when valid. No UPSERT correction path exists.
7. **Archives/linkage:** copied bytes, not a reread, are retained even if live
   files change before DB persistence. Missing-file semantics survive; verify
   existing archive corruption, hash/length mismatch, disk error and oversized
   bundle/manifest/result failure. No source documents/notes leak into public
   responses. Database restore without required archives is detected honestly.
8. **Read/API:** strict JSON/query/UUID/cursor validation, bounds, pagination
   order and context binding; 201/200/400/404/409/503 cases; empty history for a
   valid uncaptured ID; immutable reads are independent of current universe and
   registry metadata. Same-origin/JSON/loopback behavior remains enforced.
9. **Future verification acceptance:** specify fixtures for MATCH,
   INPUT_NOT_AVAILABLE, POLICY_VERSION_UNAVAILABLE and DIFFERENT_RESULT in the
   later verifier milestone. For initial implementation, prove stored bytes and
   exact input linkage unchanged after future bars/references/portfolio appends;
   do not implement the deferred verification service merely to run these cases.
10. **Safety:** before/after fingerprints of all existing operational/portfolio
    tables and pilot/reference/operation files; only the new tables/archive may
    change in an explicit capture. Provider calls/units 0/0; FullIdx disabled;
    soak count/dates unchanged. GET remains read-only.

Implementation release verification: `dotnet build`, canonical root
`dotnet test`, owned disposable database constraint/concurrency and HTTP tests,
then a read-only comparison plus one explicitly requested prospective capture
against the real PILOT. No UI/browser suite is required unless a later milestone
adds UI. Do not migrate/capture operational data during design-only review.

## 16. Migration and implementation order

Propose **`0005_decision_snapshots.sql`** in the existing embedded migrations
directory. Follow the existing transactional migration/advisory lock and
pilot_schema_version conventions: add two tables, constraints, indexes and
immutability/completeness guards; record version 5 only on successful commit.
Test fresh install, upgrade from 4 and safe rerun in an owned disposable database.
No data backfill, seed decisions, replay imports or changes to accounting/source
tables. Deployment requires the normal reviewed implementation and controlled
migration step; do not automatically run an operational migration in this task.

Implementation sequence after review:

1. Resolve the pre-existing unrelated contract edit with its owner; retain the
   Screener policy baseline and review this design as the implementation contract.
2. Add schema and the minimal typed projection/request/manifest contracts;
   implement and test database atomic completeness/immutability first.
3. Expose the existing evaluation core within a caller transaction and retain the
   copied reference bundle; prove GET/hash behavior unchanged.
4. Add prospective capture, requestId recovery, archival integrity and bounded
   read endpoints, with the required chronology/concurrency/safety tests.
5. Verify migration/restore and a separately authorized real local capture.
   Defer UI, automation, replay writer, verifier implementation, outcomes,
   analytics, compression and capacity expansion to separate reviewed milestones.

No unresolved product architecture choice is delegated to implementers. Real
instrument/session/price-basis coverage remains incomplete independently of this
design, and storage estimates require measurement. Those are honest operational
limitations, not permission to weaken evidence or bypass FullIdx/soak gates.
