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
| C — Research vertical slice | READY after B | Source-independent diagnostics only; real-data rights not re-established |
| D — Fundamental review | READY | Current candidate remains PROPOSAL — NOT FROZEN |
| E — Fundamental implementation | CONDITIONAL | Freeze/build approval absent; prepare handoff only |

Current task: C. B's implementation is the commit introducing `SourceUsagePolicy.cs`
and this record (resolve with `git log -- src/IdxStockIntelligence.Application/SourceUsagePolicy.cs`).
Latest previously verified commit: initial baseline above. Next READY task: C.
Check actual HEAD/status and rerun applicable acceptance before adopting this record.
The initial full run predates the final grant/source binding refinement; the focused
32-case discovery checks that refinement. A final full regression is planned after C.
Sandboxed discovery initially stalled and was interrupted; normal discovery outside
the sandbox compiled and executed. Analyzer naming/options errors were corrected;
no failing test or compiler result is treated as acceptance.

## Approval and operational boundaries

Level-1 operational screening remains BLOCKED; Fundamental acquisition remains
BLOCKED / HOLD_FOR_EVIDENCE. No source research is reopened without new evidence.
No provider contact/activation, subscription, operational migration, protected
evidence mutation, public deployment, trading, push or shared-infrastructure edit
is authorized. Fundamental freeze and corresponding production build remain explicit
user decisions. These gates do not block independent source-policy/diagnostic work.
