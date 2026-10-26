# Next-session backlog — 2026-10-26

Handoff snapshot. Continue from here in a fresh session; do not re-derive the history.

## Baseline

| Repo | HEAD | State |
|---|---|---|
| `idx-stock-intelligent` | `064203c` | clean, `main` synced with `origin/main` |
| `ai-local-infra` | `6a4412a` | clean, synced (3 commits: typed acceptance rules + prompt) |

Verification at handoff: full root .NET discovery **1,258 total / 1,054 passed / 204
skipped / 0 failed**, build 0 warnings; Python adapter/MCP suites 32/32.

Read first: `AGENTS.md`, `docs/AGENT_EXECUTION_RULES.md`, this file,
`docs/PERSONAL_FIRST_MASTER_V1.md`, `docs/PRODUCT_SLICE_RUNBOOK.md`.

## Finished (context only)

- Source usage policy + research diagnostic boundary (A–C of Master V1).
- Fundamental Evidence Binding V0.1: review, freeze, decision record.
- E1 binding/shape decoder, E2 exact numerics/applicability, E3 canonical hash binding,
  E4 typed scope/identity, E5 revision/PIT/premise.
- Dev-time integration: read-only ai-local-infra facade, MCP server + DSH bundle,
  queue dispatch with fake backend, real hosted run reached the model.
- Config-only source onboarding, proven by `SourceOnboardingTests`.

## Backlog

Status: READY = executable now; NEEDS DECISION = owner call; BLOCKED = external
dependency; PARKED = deliberately deferred.

### A. Fundamental follow-through

| ID | Item | Status | Notes / acceptance |
|---|---|---|---|
| A1 | Persisted binding + logical DDL design (STORE01 §22, provisional `0012`) | READY | docs-only; no migration authored, no deployment |
| A2 | T01–T44 implementation acceptance table | READY | map every scenario to implemented tests or explicit gaps |
| A4 | Permanent golden-byte fixtures pinning the canonical writer | READY | E3 currently proves it in-code only |
| A5 | E4 remainder: 53-week years, fiscal transitions, taxonomy bridge | READY | additive; no contract reinterpretation |
| A3 | Reader/evaluator integration into readiness | NEEDS DECISION | product scope; contract already frozen |
| A6 | Parser XML/ZIP budget design | DEFERRED | belongs to a future source adapter |
| A7 | D07 formulas (FCF, debt, TTM) | BLOCKED | product approval per formula |
| A8 | D08 classification/valuation policy | BLOCKED | product approval |
| A9 | Migration `0012` authoring + disposable DB acceptance | NEEDS AUTHORIZATION | deployment stays separate |

### B. Sources / Level-1

| ID | Item | Status | Notes |
|---|---|---|---|
| B1 | Close the two EODHD premises (genuine bar vs synthetic; `.JK` price/volume/currency/continuity) | READY | public docs only; no contact, no cost |
| B2 | Re-run the Level-1 blocker matrix after B1 | READY after B1 | price, benchmark, corporate action, liquidity, board, status, calendar |
| B5 | Exchange calendar + session-completion evidence | READY | research; IDX "calendar" endpoint is an agenda feed |
| B3 | KSEI corporate-action coverage/permission inquiry | NEEDS DECISION | requires owner authorization to contact |
| B4 | IDX control evidence (affirmative TradingStatus) | PARKED | revisit only if it becomes the sole blocker |
| B6 | Deploy migrations `0007`–`0011` (OPS-1 follow-up) | NEEDS AUTHORIZATION | preflight + owned disposable DB first |
| B7 | F15 archive-count discrepancy (2,078 vs >5,000) | READY | editorial, non-blocking |
| B8 | EODHD `.JK` price adapter (5A.3c) | BLOCKED | needs B1 + admission + both premises closed |
| B9 | Stockbit secondary-provider re-evaluation | PARKED | automation permission, auth lifecycle, provenance |
| B10 | External parser reuse (idx-bei/idxlens/exploreR) | CONDITIONAL | licensing + no bypass techniques |
| B11 | Onboard any new provider | READY | config + `SourceOnboardingTests`; see `docs/SOURCE_ONBOARDING.md` |

### C. Agent/infra integration

| ID | Item | Status | Notes |
|---|---|---|---|
| C2 | Register `opencode/deepseek-v4.1-flash-free` in `model-capabilities.yaml` | READY | zero-cost real runs; shared-repo change |
| C1 | Typed acceptance for CLI backends (capture layer) | NEEDS DECISION | safety-relevant; `_structured_capture` requires one complete assistant message |
| C3 | Real routing: Ollama absent (`local_inference: NOT_CONFIGURED_BY_MACHINE_POLICY`) | CONDITIONAL | install Ollama or change role mapping |
| C5 | Re-render MCP bundle if paths move; lifecycle doc | READY | `scripts/install-ai-infra-mcp-bundle.sh` |
| C4 | Import DSH sessions into infra telemetry | DEFERRED | no mapping defined |

### D. Operations / housekeeping

| ID | Item | Status |
|---|---|---|
| D1 | Pilot soak 1/10 → 9 real sessions | BLOCKED (manual, operational) |
| D2 | Model-label discrepancy (v4-pro vs Flash) | READY |
| D3 | Editorial annex for "proposal" wording inside the frozen contract | OPTIONAL |

## Recommended order

1. **B1 → B2** — highest product value, no permission, no cost.
2. **A1 → A2** — persistence design and an honest acceptance table.
3. **A4 → A5** — harden tests, close E4 edges.
4. **C2** — cheap real runs; then decide **C1**.
5. **B3/B5**, **B6/A9**, **B4** — only with explicit authorization.

## Guardrails for the next session

- Do not reinterpret frozen contracts; semantic changes need an additive version.
- Never commit secrets or market datasets; `logs/` and state dirs stay untracked.
- No provider contact, subscription, operational migration, push of another agent's work,
  or shared-infra edit without an explicit owner decision.
- Documentation-only tasks: `git diff --check` plus content review; do not claim runtime
  tests were run.
- Runtime acceptance: `dotnet test` from the root (or `rtk proxy dotnet test`), never a
  test executable; focused classes via `--filter-class`.
