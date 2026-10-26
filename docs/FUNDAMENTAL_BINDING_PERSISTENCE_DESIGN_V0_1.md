# Fundamental Binding Persistence Design V0.1 — logical only

**Status: DESIGN PROPOSAL. No migration was authored, no SQL was executed, and no
database was created, connected to or mutated.** This document derives a logical
persistence design for the frozen
[Fundamental Evidence Binding V0.1 contract](FUNDAMENTAL_EVIDENCE_BINDING_V0_1_CONTRACT.md)
§18.3 (E01) and §22 (STORE01). It does **not** amend that contract: where this document
and the contract differ, the contract governs and the difference is a design question
listed in §7 rather than a reinterpretation.

Provisional relation name and migration slot: `fundamental_evidence_record`, `0012`.
Physical DDL, triggers, index definitions and disposable-database acceptance belong to
the separately authorized P2-I2 / backlog A9 and are not decided here. Deployment is a
further, separate authorization and is blocked on the `0007`–`0011` chain (backlog B6).

## 1. What this design is not

- Not a schema change, not SQL, not a deployment plan, not an operational migration.
- Not source admission, permission, or a claim that any fundamental source is usable.
- Not a second premise relation, entity registry, wide accounting model, or current-state
  table (§22).
- Not a decision about physical layout beyond what §22 already fixes.

## 2. Derivation rules

| # | Rule | Contract source |
|---|---|---|
| R1 | Exactly one new relation carries the whole envelope; filing, fact and convention identity live in it as immutable records | §22 |
| R2 | One column per E01 envelope field, with nullability mirroring the contract exactly | §18.3, §22 |
| R3 | Authoritative clock literals are `text`, not `timestamptz`; a timestamptz projection is a conservative prefilter only | §22 |
| R4 | Payload is retained canonical `text`, never `jsonb` as the sole byte representation; query projections cannot replace it | §22 |
| R5 | Amounts stay lexical strings; never the price table's `numeric(20,6)` | §22 |
| R6 | No FK into Screener tables | §22 |
| R7 | The database enforces local row shape; cross-record semantics are the trusted atomic writer's job | §22 |
| R8 | Existing foundation relations are reused unchanged: `source`, `ingestion_run`, `raw_artifact`, `raw_fetch_observation` | §22, migrations `0001`/`0002` |

R7 deserves emphasis because it bounds what DDL can honestly claim. A `CHECK` constraint
cannot see another row, so these contract invariants are **not** expressible as DDL and
must not be advertised as enforced:

- `knownAt ≥ receipt.retrievedAt` and `recordedAt ≥ knownAt`;
- `receipt.retrievedAt` equal to the retained `raw_fetch_observation.fetched_at`;
- the admission window containing both `reviewedAt` and the receipt instant (§21);
- premise visibility at `knownAt` (§19.3);
- artifact source/hash/length authentication.

Consequence: the deployed design must restrict `INSERT` on this relation to the trusted
writer role and grant no `UPDATE`/`DELETE`, because a shape-valid row written outside the
writer would be semantically unchecked. That is a deployment/privilege requirement, and
it is the main reason R7 is written down rather than implied.

## 3. Logical columns

Types are logical SQL types. "Null" mirrors E01; a nullable field keeps its column
nullable and no default is invented for it.

| Column | Type | Null | E01 field | Notes |
|---|---|---|---|---|
| `evidence_id` | uuid | no | evidenceId | Primary key (R) |
| `policy_id` | text | no | policyId | Exact literal; unsupported value rejected by writer |
| `schema_version` | int | no | schemaVersion | `= 1`, no fallback |
| `payload_schema_version` | int | no | payloadSchemaVersion | `= 1`, no fallback |
| `claim` | text | no | claim | `FinancialFiling \| FundamentalFact \| FundamentalConvention` (S) |
| `scope_kind` | text | no | scopeKind | `REPORTING_ENTITY \| SOURCE`; SOURCE only for conventions (S/E) |
| `subject` | text | no | subject | Closed canonical JSON value (S/E) |
| `subject_key` | text | no | — | **Derived projection**, not part of E01; see §7 D3 |
| `source_id` | text | no | sourceId | FK → `source(source_id)` (S/P) |
| `source_reference` | text | no | sourceReference | Never URL-derived (S) |
| `assertion_key` | text | no | assertionKey | S |
| `revision_series_id` | text | no | revisionSeriesId | 1–400 code units; exact `sourceId + U+001F + "fundamental-evidence-v0.1.0/" + claim + "/" + assertionKey` (S) |
| `revision_number` | bigint | no | revisionNumber | `> 0`, local append ordinal (R) |
| `supersedes_revision_number` | bigint | yes | supersedesRevisionNumber | `> 0`; null only at revision 1 (P) |
| `lineage_evidence_artifact_id` | uuid | yes | lineageEvidence | FK → `raw_artifact`; null only for revision 1 (P) |
| `authority_tier` | text | no | authorityTier | `T1..T5` (P) |
| `published_at` | text | yes | publishedAt | Exact Instant literal; null if unknown or date-only (P) |
| `publication_date` | date | yes | publicationDate | Never converted to midnight (P) |
| `publication_evidence_artifact_id` | uuid | yes | publicationEvidence | FK → `raw_artifact`; required if either publication field is non-null (P) |
| `receipt_ingestion_run_id` | uuid | yes | receipt | Composite FK part; null only for DERIVED (P) |
| `receipt_artifact_id` | uuid | yes | receipt | Composite FK part |
| `receipt_retrieved_at` | timestamptz | yes | receipt | Composite FK part; must equal the retained observation instant exactly |
| `known_at` | text | no | knownAt | Instant literal (P) |
| `recorded_at` | text | no | recordedAt | Instant literal, diagnostic only (P) |
| `admission` | text | no | admission | Closed canonical JSON value, eleven required fields (P) |
| `payload` | text | no | payload | Closed canonical JSON value, authoritative bytes (P) |
| `payload_operation` | text | no | payload.operation | **Derived discriminator**: `ASSERT \| CANCEL` |
| `payload_completeness` | text | no | payload.completeness | **Derived discriminator**: `FULL \| PARTIAL` |
| `payload_sha256` | char(64) | no | payloadSha256 | SHA-256 of `payload`; compared before typed reconstruction (P) |

`published_at`, `known_at` and `recorded_at` are `text` because timestamptz is
microsecond-precision and must not be the sole representation of the permitted seventh
fractional digit (§22). Reader/writer comparison is exact on the literal; any timestamptz
column added later is a prefilter and must never exclude a potentially decisive row by
rounding.

`receipt_retrieved_at` is the deliberate exception: §18.3 requires it to equal the
retained `raw_fetch_observation` instant exactly and forbids claiming finer precision than
that observation contains, so it must carry the foundation table's own type to support the
composite FK. That is a contract requirement, not a preference for rounded clocks.

## 4. Keys and constraints

| Kind | Definition | Enforces |
|---|---|---|
| Primary key | `evidence_id` | R: record identity; exact retries require all fields/content equal |
| Unique | `(revision_series_id, revision_number)` | One ordinal per series; a different `evidenceId` with the same series/revision is rejected even if the value matches (§22) |
| Self-FK | `(revision_series_id, supersedes_revision_number)` → `(revision_series_id, revision_number)` | Predecessor **exists in the same series**. With `revision_series_id` NOT NULL this FK is skipped only when `supersedes_revision_number` is NULL |
| FK | `source_id` → `source` | Exact existing source; no invented provider row |
| Composite FK | `(receipt_ingestion_run_id, receipt_artifact_id, receipt_retrieved_at)` → `raw_fetch_observation` | Receipt authenticates a real retained observation, not a fictitious UUID |
| FK | `lineage_evidence_artifact_id`, `publication_evidence_artifact_id` → `raw_artifact` | Artifact references resolve to retained bytes |
| Check | `revision_number > 0`; `supersedes_revision_number > 0` | Ordinal bounds |
| Check | `(revision_number = 1) = (supersedes_revision_number IS NULL)` | "Null only at revision 1" (§18.3) |
| Check | `claim IN (...)`; `scope_kind IN (...)`; `authority_tier IN (...)`; `payload_operation IN (...)`; `payload_completeness IN (...)` | Closed enumerations |
| Check | `schema_version = 1 AND payload_schema_version = 1` | No decoder fallback |
| Check | `char_length(revision_series_id) BETWEEN 1 AND 400` | §18.3 bound |
| Check | `payload_operation = 'CANCEL'` implies `revision_number > 1`, `payload_completeness = 'FULL'` | Cancellation is never revision 1 and never PARTIAL (§18.3) |
| Check | `payload_sha256 ~ '^[0-9a-f]{64}$'` | Hash form; authenticity still needs comparison |
| Check | receipt parts all-null or all-non-null | Nullability mirrors E01; DERIVED carries no receipt |
| Check | `published_at IS NULL OR publication_date IS NULL` | Date is used only when the instant is unknown, never alongside it (§18.3) |
| Check | `publication_evidence_artifact_id IS NOT NULL OR (published_at IS NULL AND publication_date IS NULL)` | Publication evidence is required whenever either publication field is set (§18.3) |
| Check | `revision_number > 1 OR lineage_evidence_artifact_id IS NULL` | Lineage proof is never attached to the first revision (§18.3) |
| Append-only | reuse `reject_evidence_mutation()` trigger on `UPDATE OR DELETE` | Historical immutability, same mechanism as `daily_bar_revision` |

Two invariants are deliberately **not** claimed above: *consecutiveness with no gap*, and
*"names the immediately preceding retained revision"*. The unique key forbids duplicates
and the self-FK forbids dangling predecessors, but neither forbids a gap such as revision
3 without 2. Enforcing both requires the writer to compute `max(revision_number) + 1`
inside a serialized transaction (`SERIALIZABLE`, or an advisory lock keyed by
`revision_series_id`) and to reject any insert whose predecessor is not the current head.
Which of those two mechanisms is used is a physical choice deferred to A9; the invariant
itself is not deferred.

The lineage check is intentionally one-directional. §18.3 says lineage is "null only for
first revision" and that correction/withdrawal proof is "bound to new record"; the literal
text constrains null to the first revision but does not, by itself, force every later
revision to carry proof (a `PARTIAL` later revision is conceivable). The database
therefore forbids lineage on revision 1 only, and requiring proof on revision > 1 remains
a writer rule pending a contract-owner reading. This is exactly the kind of ambiguity that
must not be resolved by picking the stricter guess.

## 5. PIT selection contract the relation must support

The relation must answer "all potentially decisive candidates for this subject/economic
key at cutoff C", not "the latest row". Per §21 PIT02, a record is visible iff
`known_at ≤ C`, and its receipt instant `≤ C` when non-null; `published_at ≤ C` when
non-null. `recorded_at` does not gate replay.

The query path therefore needs, logically:

1. candidate selection by `subject_key` (+ `claim`, when the economic key is claim-scoped);
2. full revision history for the matched `revision_series_id` values, including visible
   predecessors, so a cancelled head cannot hide behind a `LIMIT`;
3. deterministic ordering by the exact literal clocks, never by a rounded timestamptz;
4. `BOUND_EXCEEDED` when the candidate set for one economic key exceeds 256 root records;
   no paging around the resolver, and no `LIMIT` that erases a conflict, cancelled head or
   high-authority failure (§22).

Proposed indexes (logical; exact definitions are A9's):

| Index | Purpose |
|---|---|
| `(subject_key, claim, known_at)` | Candidate selection for a factual economic key |
| `(revision_series_id, revision_number)` | Head and predecessor traversal; the unique key already supplies it |
| `(source_id, assertion_key)` | Provenance/diagnostic lookup |
| `(claim, known_at)` | Bounded scans for review tooling |

An index is a performance choice and never permission to weaken complete selection (§22).

## 6. Migration chain and preconditions (not executed)

`0012` follows `0007 → 0008 → 0009 → 0010 → 0011`, each requiring its predecessor, on top
of the `0001`/`0002` source/artifact/receipt foundation (§22). Two separate facts must not
be conflated:

- **Authoring** `0012` requires only the repository chain to be well-formed and the
  design above to be accepted.
- **Deploying** `0012` requires the predecessor migrations to be actually present in the
  target database. Backlog B6 (OPS-1) is unresolved, so this design assumes nothing about
  which migrations a live database has, and records no fresh DB observation.

Operating rules already fixed by §22 and restated for the implementer: no editing an
applied migration; no skipping operationally absent `0007`–`0011`; no deploying them as a
side effect of `0012`; preflight is checksum/version/backup/restore plus disposable
acceptance, separately authorized.

## 7. Open design decisions (need an owner, not an implementation choice)

These are places where §22's text and a real referential constraint pull in different
directions. D1–D4 are presentation/persistence questions rather than semantic ones, and
none may be settled by silently preferring the easier option. D5 is the exception: it is a
genuine gap in the frozen text and is flagged as a contract question, not an engineering
preference.

**D1 — `receipt` as JSON versus three columns.** §22 lists `receipt` among the fields held
as closed canonical JSON values, but also requires a "receipt composite reference to
`raw_fetch_observation`". A foreign key needs the three components as columns. Recommended:
store the three typed columns only, and reconstruct the canonical JSON deterministically
from them, so the receipt cannot disagree with its own FK. The alternative — canonical
JSON *plus* three extracted columns — creates a divergence risk that no `CHECK` can fully
close. Either way the semantic content is identical; only the byte presentation differs.

**D2 — artifact references as JSON versus id columns.** Same pattern for
`lineageEvidence` and `publicationEvidence`. Recommended: an `*_artifact_id` FK column,
with the writer verifying that the id matches the canonical JSON artifact object it
retains, and disposable acceptance recomputing that equality. §22 already permits nested
refs to be writer-checked rather than given a second relation.

**D3 — `subject_key` projection.** `subject` is JSON, but bounded PIT selection needs a
deterministic key. Recommended: a writer-supplied, explicitly non-authoritative
`subject_key` with a disposable-database test that recomputes it from the canonical
`subject` and rejects any mismatch. A generated column would be stronger, but only if the
key derivation is expressible deterministically in SQL; that choice is A9's.

**D4 — derived discriminator columns.** `payload_operation` and `payload_completeness` are
copies of fields inside the authoritative `payload` text, kept so that the
ASSERT/CANCEL constraints above are real DDL. Same rule as D3: writer-derived, verified
against the payload in acceptance, never authoritative over it.

**D5 — lineage on later revisions.** §18.3 states lineage is "null only for first revision"
and that correction/withdrawal proof is "bound to new record". Whether every revision > 1
must carry lineage proof (including a `PARTIAL` one) is not stated unambiguously. The DDL
above enforces only that revision 1 carries none. An owner reading is needed before
turning the writer rule into a constraint.

## 8. Acceptance criteria a future A9 must prove (disposable database only)

1. Schema applies onto an owned disposable database from the `0007`–`0011` chain, and
   re-applying is idempotent or fails closed — never partially.
2. `UPDATE` and `DELETE` on the relation are rejected by trigger.
3. A second insert with an existing `(revision_series_id, revision_number)` is rejected
   even when every value is byte-identical but `evidence_id` differs.
4. An identical `evidenceId` retry with an identical complete envelope succeeds and does
   not rewrite `recorded_at`; a retry differing in any field is rejected.
5. A `supersedes_revision_number` naming a nonexistent predecessor in the same series is
   rejected by the self-FK; a cross-series predecessor is rejected.
6. A receipt triple with no matching `raw_fetch_observation` is rejected; a matching one
   succeeds and its instant equals the retained observation exactly.
7. `CANCEL` at `revision_number = 1`, and `CANCEL` with `PARTIAL`, are rejected.
8. Derived columns disagreeing with the canonical JSON are rejected by the acceptance
   harness (D3/D4), proving the divergence is detectable.
9. Bounded selection returns `BOUND_EXCEEDED` at 257 root records rather than truncating,
   and a cancelled head is still returned for conflict reporting.
10. No new FK or write path touches any Screener table.

## 9. Validation and activity

Documentation only. No migration file, SQL execution, database connection, schema change,
source admission or permission change. No runtime test suite was run and none is claimed;
`dotnet test` is not applicable to a documentation-only design. `git diff --check` and
link review are the applicable checks. No push.
