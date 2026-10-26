# Personal-first Master V1 — execution checkpoint

Objective: useful local personal research, configurable source rights, preserved
Verified evidence gates, and a future commercial path without SaaS infrastructure.
Started 2026-10-15 WIB on clean `main` at
`4c441d9441c1495bee4b03339d48330dc7e6c403` (5 ahead / 0 behind local `origin/main`).

## Execution mode and reuse

**NATIVE_CODEX_ORCHESTRATION (Mode B)**: native sequential execution. No inference
or orchestration through shared infrastructure, no fabricated model/effort telemetry.
Read-only discovery found the sibling `ai-local-infra` on clean
`feat/verified-result-handoff-v0.1` at
`a8ff68acae4b20d190b8ddcb9226512b1a5817d6`. Its Graphify graph identifies runtime,
context and acceptance components; actual docs corroborate them. `ORCHESTRATION.md`
describes plan-only orchestration, while `VERIFIED_RESULT_HANDOFF.md` describes the
newer opt-in sequential controller but explicitly says live handoff is unverified.
Its typed fact acceptance supports only `reverse_text_v1`, not this financial
program. Local code/tests do not establish live cross-model execution. No shared
infrastructure file, state, cache or configuration is modified; no model is called
through it. No old execution/cache acceptance is adopted for this project.

Existing architecture audit and policy design reuse
[the source policy proposal](SOURCE_USAGE_POLICY_AND_CAPABILITY_GATING_V0_1_PROPOSAL.md)
including its 14-feature matrix, five synthetic scenarios and eight repository-role
reconciliation. This master request authorizes the bounded source-independent
implementation below; it does not approve providers, frozen changes or Fundamental
freezing. Source registry, archive, Python/.NET ownership, as-of readers, existing
charts, ledger and committed-outcome Research remain intact.

## Bounded implementation decisions

- Add a pure Application usage gate, not a provider framework. It consumes explicit
  trusted source/scope/capability declarations and permission-decision references.
  Source scope is an exact reviewed route/dataset/window/convention identity;
  no wildcard matching, automatic switching or cross-provider history stitching.
- Four initial intents: retained price inspection, indicative technical diagnostics,
  Verified screening eligibility and price export. These are usage checks, not new
  calculators or readiness states. Other families may be declared, not activated.
- Strict bounded JSON configuration, unknown operations denied; absent configuration
  disables every sourced feature. No real provider grant is shipped. Unknown,
  prohibited, expired or future/unreviewed rights fail closed. Intervals are UTC
  `[validFrom, validUntil)`; both bounds are required for this conservative increment.
- Conditional permissions and unresolved restrictions always deny in this increment.
  Exact condition/limit enforcement needs a later reviewed integration; configuration
  cannot assert that an unenforced restriction has been satisfied.
- PERSONAL needs personal scope; SAAS needs commercial and multi-user scope but is
  still unsupported even with hypothetical grants. Rights-denial precedence is
  prohibition, expiry, unknown; SaaS serving is then denied before evidence use.
- Base retained operations require retention, internal use and display. Calculation
  requires derived processing, export requires separate export, and extra requested
  acquisition/history/recurring/redistribution operations require separate grants.
  History/recurring retrieval in this increment also requires unattended-acquisition
  permission; no manual acquisition path is implemented.
  No acquisition/export path is added. Ordinary display JSON is not export by encoding.
- Exact selected input provenance is supplied by trusted consuming code, not browser
  assertions. Verified use additionally requires the consuming service's existing
  complete evidence gate; a usage permit is never V0.2 admission. The diagnostic
  boundary will supply no positive evidence gate and perform no market analysis.
- Permission execution time is independent of historical financial cutoff. New
  policy snapshots are evaluated afresh; no decision cache, grandfathering, evidence
  rewrite, deletion, migration or operational source activation is introduced.

## Task status / acceptance

| Workstream | Status | Acceptance / commit |
|---|---|---|
| A — reuse audit/design | COMPLETE | Existing proposal retained; exact bounded decisions above |
| B — typed policy/catalog/evaluator | COMPLETE | 32 focused cases passed after exact grant/source/scope binding; initial full discovery 988 passed / 204 DB cases skipped |
| C — Research vertical slice | COMPLETE | Build 0 warnings/errors; 9 focused + 3 DB-free HTTP cases passed; final full regression 999 passed / 204 skipped / 0 failed |
| D — Fundamental review | COMPLETE | Internal review in the [freeze decision package](FUNDAMENTAL_FREEZE_DECISION_PACKAGE_V0_1.md); P1–P3 and P4 recorded APPROVED 2026-10-20, P5 not authorized; candidate frozen 2026-10-20 as [the contract](FUNDAMENTAL_EVIDENCE_BINDING_V0_1_CONTRACT.md) |
| E — Fundamental implementation | E1–E5 DONE | Binding/shape, exact numerics, canonical hash binding, typed scope/identity comparability and revision/PIT/premise fixtures; parser, DDL/deployment and source admission stay gated |

Current task: D (complete). B commit:
`143276343c9b6df3bd645418d50039a5a83eb9d4` (`feat: add fail-closed source usage policy`).
Latest previously committed milestone: C, the commit introducing
`ResearchSourceDiagnostics.cs` and updating this checkpoint. Next READY task: the
owner's freeze decisions P1–P5 in the decision package, then E1–E5 once separately
authorized. Check actual HEAD/status and rerun applicable acceptance before
adopting this record.
The initial full run predates the final grant/source binding refinement; the focused
32-case discovery checks that refinement. The final full regression after C covers
the latest code: 1,203 total, 999 passed, 204 opt-in DB cases skipped, zero failures.
Its TRX was retained outside the repository under `/private/tmp/idx-master-v1-tests`;
it is local execution evidence, not portable acceptance to trust without context.
Sandboxed discovery initially stalled and was interrupted; normal discovery outside
the sandbox compiled and executed. Analyzer naming/options errors were corrected;
no failing test or compiler result is treated as acceptance.

## Research diagnostic boundary and local use

`GET /api/research/source-diagnostics` (optional `?mode=RESEARCH` or
`?mode=VERIFIED`) reports four usage-intent decisions and explicit diagnostic-only
limitations. Provenance is **TRUSTED_LOCAL_CONFIGURATION_ONLY**, not market evidence.
It reads no market rows, archive files or financial facts, performs no calculation,
checks no positive evidence admission and calls no candidate promotion. Market-data
readiness is not assessed. No third-party data rights were newly demonstrated in
this run, so C3 uses the authorized source-independent diagnostic fallback.

The trusted server setting `IDX_SOURCE_USAGE_CONFIG` optionally contains bounded
JSON in the B schema. If absent, all sources/features are disabled. An explicit
invalid value returns HTTP 503 / SOURCE_USAGE_CONFIG_INVALID on this endpoint,
never a fallback grant. Query parameters cannot supply sources, rights, clocks or
admission; invalid/duplicate parameters return HTTP 400 with a safe code. Review
references, scope IDs, credentials and raw configuration are not returned.

Minimal explicit disabled configuration (no real-provider authorization):

```json
{"schemaVersion":1,"profile":"PERSONAL","enabledFeatures":[],"sources":[],"selections":[]}
```

Configuration enum tokens are uppercase snake case; the endpoint uses the existing
API enum serialization unchanged. Positive examples exist **only as authored test
fixtures**, with source/scope-bound synthetic decisions. A capability evidence ref
must identify reviewed route/field/version/history/unit/currency/adjustment/revision/
publication semantics; it is not source truth or an admission. A scope ID identifies
exact reviewed input coverage, not a pattern. Only one active scope per source and
one selected source per capability are supported in this increment.

The new setting affects **only this new diagnostic boundary**. It is not a global
switch or a retrofit of acquisition, legacy market readers, existing export or
Research V0.1. There is no source acquisition or live analytical execution even if
synthetic configuration produces USAGE_PERMITTED. New consumers must authenticate
actual input provenance, enforce all operational/source limits, check current
registry/rights and apply the existing complete evidence gates before using data.
Changing an environment setting requires operator restart; no hot-revocation,
background job/cache protocol or durable permission history is claimed.

Run against the existing local API; normal API startup still requires its existing
database connection configuration, but this endpoint never resolves a DB service.
HTTP acceptance deliberately uses a non-operational, unusable connection, owned
temporary working directory and synthetic configuration. It needs no PostgreSQL
server or schema/migration operations. Existing loopback-only/same-origin controls
are untouched. No UI, new package or orchestration service is needed.

Deterministic checks:

```text
rtk proxy dotnet test --filter-class IdxStockIntelligence.Tests.SourceUsagePolicyTests
rtk proxy dotnet test --filter-class IdxStockIntelligence.Tests.ResearchSourceDiagnosticsTests
rtk proxy python3 -m unittest discover -s scripts -p test_research_source_diagnostics.py -v
```

Use root standard discovery for full regression, not a test executable. The first
attempt at a combined wildcard class filter matched zero cases and was correctly
rejected by the minimum-expected-tests check; exact class filters then passed.

## Conditional Fundamental implementation handoff — E

**CONDITIONAL; no Fundamental production code is implemented.** The candidate is
now [FROZEN V0.1](FUNDAMENTAL_EVIDENCE_BINDING_V0_1_CONTRACT.md). Review
findings, the statement-only scope and the exact contract bundle are accepted by the
owner (P1–P3, 2026-10-20), and P4 authorized the freeze, which is now executed.

Owner decision recorded 2026-10-20 WIB: **P5 is granted for E1 only**, after the
owner directed that the next slice be executed in-session instead of through a
pasted prompt. E2–E5 remain unauthorized. E1 stays inside its row below: no parser,
table, migration, source adapter, registry row or financial formula; widening E1 or
starting E2 needs a new explicit decision.

| Future slice | Smallest authorized-after-approval work | Acceptance / boundary |
|---|---|---|
| E1 | Three exact typed bindings/envelope from §§18–19, required-null/closed shapes and family-specific validation | T01/T23/T24/T32/T36/T43; no parser, table or source adapter; reserved derived grammar cannot enable financial formulas |
| E2 | Reuse existing `EvidenceDecimal`; add family applicability/unit/scale validation, exact normalization/result checks | T03–T07/T28/T42, including max coefficient, negative/zero/null, inexact parse and repeated scale; no wider numeric domain or guessed FX |
| E3 | Reuse canonical writer after strict family decoding; bind exact payload bytes/hash and literal instants | T36/T44 and golden bytes; no JSONB-only canonical bytes, hidden property defaults or microsecond rounding |
| E4 | Pure fixture validation of identity, statement periods/dimensions, mapped concepts and scoped admission | T08–T16/T26/T41/T43; fixture-only permissions; no local registry insertion or approved accounting formula inferred from fixture |
| E5 | Pure revision/cancel/reinstatement/PIT and role-bound closure resolver fixtures | T17–T22/T29/T30/T35/T37–T40; no latest fallback, inactive-premise resurrection or retroactive knowledge |

T01–T44 remain future specifications; mapping them is not running them. Reconcile
all 44 in an implementation acceptance table rather than replacing them with the
new source-policy tests. Parser XML/ZIP budgets, financial formula rules, exact DB
concurrency/restore proof and provider coverage need their own later scopes.

### E4/E5 delivered (2026-10-26 WIB)

`FundamentalScope.cs` types the nested scope objects the E1 decoder only shape-checked:
`Period`, `Unit`, `ShareBasis`, `Accounting` and an exact `FundamentalIdentity`
comparison key. It adds period comparability (INSTANT vs DURATION, QUARTER vs YTD,
coverage duplicates/overlaps, ANNUAL fiscal span, YTD fiscal start), accounting
comparability (consolidation/group, attribution, operations, statement scope,
dimensions) and the rule that unknown terms block comparison instead of implying
equivalence.

`FundamentalRevisions.cs` adds the pure revision/PIT layer: consecutive ordinals,
mandatory predecessor links, cancellation never first and always premise-bound,
`VisibleAt` over knowledge/retrieval/publication clocks, governor selection without
latest-wins fallback, visible-cancel semantics, reinstatement visible only from its own
`knownAt`, equal-authority conflict reporting, correction-after-cutoff invisibility and
a bounded acyclic premise closure (256 nodes, 16 edges, unique roles).

Owner decision recorded 2026-10-26 WIB: **E4 and E5** authorized on the owner's
instruction to finish the series. No parser, table, migration, registry row, source
adapter or financial formula was added; evaluator/reader integration, persisted
binding, 53-week and taxonomy-bridge corner cases and DDL/deployment remain outside
these slices.

Acceptance: `FundamentalScopeRevisionTests` 14/14 passed (T08–T16, T26, T41, T43 and
T17–T22, T29, T30, T35, T37–T40); .NET build 0 warnings.

### E3 delivered (2026-10-26 WIB)

`FundamentalCanonical.cs` binds the payload to its bytes: it reuses the existing V0.2
canonical writer (ordinal property order, compact, no BOM/newline), hashes the
canonical payload and rejects a mismatch with `FUNDAMENTAL_INTEGRITY_FAILURE` during
decoding. Decimal and Instant literal spellings are preserved, so `1.0` and `1.00`
share a numeric value but keep distinct payload hashes, and a `.0000001Z` instant is
not rounded into `.0000000Z`.

Owner decision recorded 2026-10-26 WIB: **E3** was authorized on the owner's
instruction to continue after pushing. E4–E5 remain unauthorized.

Acceptance: `FundamentalCanonicalTests` 6/6 passed (T36, T44) and the E1 binding
suite was migrated to computed hashes (11/11); .NET build 0 warnings.

### E2 delivered (2026-10-26 WIB)

`FundamentalNumbers.cs` adds the exact numeric and applicability layer: power-of-ten
normalization `sourceLiteral × 10^scalePower × sign` with overflow, inexact-division
and >28-scale rejection, plus metric↔unit↔period applicability and the
DERIVED-only/REPORTED-only/excluded metric guards. `EvidenceDecimal` is reused
unchanged; no wider numeric domain, FX guess or rounding was introduced.

Owner decision recorded 2026-10-26 WIB: the E1-only authorization was extended to
**E2** on the owner's instruction to continue in the stock repository. E3–E5 remain
unauthorized.

Acceptance: `FundamentalNumbersTests` 19/19 passed (T03–T07, T28, T42, T43 numeric
guards); the .NET build stays at 0 warnings.

### E1 delivered (2026-10-26 WIB)

`FundamentalEvidence.cs` adds the strict decoder for `fundamental-evidence-v0.1.0`:
the E01 envelope, the payload operation/completeness rules, and the three typed
bindings FILING01/FACT01/CONV01 with closed objects, required-null semantics,
duplicate-property rejection, version binding, exact series identity, admission
shape/claim/tier checks, document/series revision rules, statement-only metric
guards, applicability/derivation exclusivity, record and array bounds, and strict
Instant/Date/Decimal lexemes. FAMILY diagnostics are emitted as
`CODE@/json/pointer` and admission is atomic — any error yields no evidence.

Deliberately deferred to E4 (per the slice table): deep period/dimension semantics,
statement-family and share-basis comparison constraints, mapped-concept and
definition agreement, canonical payload bytes/hash (E3), revision/PIT resolvers (E5)
and every formula. No parser, table, migration, source adapter, registry row or
financial formula is present. `EvidenceDecimal` is reused unchanged.

Acceptance: focused class `FundamentalEvidenceBindingTests` 11/11 passed covering
T01/T23/T24/T32/T36/T43; full root discovery 1,214 total, 1,010 passed, 204 skipped,
0 failed, build 0 warnings. P5 remains E1-only; E2–E5 need a new decision.

Do not reuse `SourceUsagePolicy` as a Fundamental codec/admission. Its initial
finite half-open permission intervals are different from the Fundamental candidate's
explicitly evidenced no-expiry/inclusive Admission semantics. Any bridge needs exact
review; neither existing source enums nor V0.2 evidence fields may be reinterpreted.
Preserve exact economic/publication/retrieval/known/recorded clocks, entity/security
mapping and source/decision/current-use separation. Existing bounded ingestion is
Screener-specific; only its safe patterns are reusable, not an unchanged importer.

After an approved pure-binding slice, standard root .NET discovery and focused
deterministic fixture checks are required. Logical persistence/DDL design and
owned disposable DB acceptance would be a later explicit scope; operational
migration deployment, original-byte ingestion and live source activation remain
separately gated. No fundamental migration or source-independent production types
are added as a shortcut while freeze approval is missing.

## Fundamental freeze decision package — D (2026-10-20 WIB)

D1 is complete as a bounded **internal** review in the
[freeze decision package](FUNDAMENTAL_FREEZE_DECISION_PACKAGE_V0_1.md). D2's
independent-review condition is unmet: no verified second model route or
independent reviewer was used, so no independent approval is claimed. The
recommendation is `READY_FOR_USER_APPROVAL` for the **statement-only** candidate,
conditional on the owner's decisions P1–P5; it is not `NEEDS_TARGETED_REVISION`
and not `NOT_READY` because the open items are scope/deferred-capability decisions
and external evidence, not defects in the proposed text.

Corroborated in this pass: the exact numeric domain is the existing
`EvidenceDecimal` (200-character literal, decimal 96-bit coefficient path,
string-only converter); migrations `0007`–`0011` each guard their predecessor, so a
provisional `0012` must follow `0011`; frozen V0.2 §1/§3 defines market-evidence
dimensions only, so the fundamental family must be additive under its own
identity. Not performed and not certified: line-by-line re-derivation of §§18–20,
golden canonical bytes, DB concurrency/restore, parser budgets and any source check.

Acceptance evidence for this run: full root .NET discovery 1,203 total / 999
passed / 204 skipped / 0 failed; focused `SourceUsagePolicyTests` and
`ResearchSourceDiagnosticsTests` 41 passed; Python diagnostic acceptance 3 passed.
Those numbers re-verify the recorded baseline; they are not acceptance of the
fundamental candidate. Approval gates, frozen contracts and source dispositions are
unchanged by this review, and no freeze, build, migration or source action follows
from it.

Owner decisions recorded 2026-10-20 WIB (§9 of the package): P1 statement-only
scope, P2 FCF boundary and P3 exact contract bundle APPROVED; P4 freeze APPROVED
for one documentation action only; P5 build E1–E5 NOT AUTHORIZED. The freeze run is
specified in [the freeze prompt](../prompts/FUNDAMENTAL_FREEZE_V0_1.md) and has not
been executed here. Until it runs, this file's own status line still describes the
candidate as a proposal, and the frozen text does not exist.

## Approval and operational boundaries

Level-1 operational screening remains BLOCKED; Fundamental acquisition remains
BLOCKED / HOLD_FOR_EVIDENCE. No source research is reopened without new evidence.
No provider contact/activation, subscription, operational migration, protected
evidence mutation, public deployment, trading, push or shared-infrastructure edit
is authorized. Fundamental freeze and corresponding production build remain explicit
user decisions. These gates do not block independent source-policy/diagnostic work.
