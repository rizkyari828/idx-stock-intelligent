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
| A1 | Persisted binding + logical DDL design (STORE01 §22, provisional `0012`) | DONE 2026-10-26 | [design](FUNDAMENTAL_BINDING_PERSISTENCE_DESIGN_V0_1.md): logical only; 5 open decisions (D1 receipt, D2 artifact refs, D3 subject key, D4 discriminators, D5 lineage ambiguity) need an owner; no migration authored, no DB touched |
| A2 | T01–T44 implementation acceptance table | DONE 2026-10-26 | [table](FUNDAMENTAL_ACCEPTANCE_T01_T44.md): 16 implemented / 15 partial / 13 gap, mapped by reading assertions not names; focused run 50/50 pass; gaps trace to A5, A6, A7/A8, A1/A9 |
| A4 | Permanent golden-byte fixtures pinning the canonical writer | DONE 2026-10-26 | `tests/fixtures/fundamental-canonical.json` + `.sha256` pinned by `T36GoldenCanonicalBytesArePinned`; verified the pin fails on byte drift and passes when restored |
| A5 | E4 remainder: 53-week years, fiscal transitions, taxonomy bridge | DONE 2026-10-26 | T12 + T15 + T16 closed with tests; `ValidateCoverage` gap defect fixed (T11); `CompareAccounting`/identity now enforce audit and statement-family constraints; no contract reinterpretation |
| A3 | Reader/evaluator integration into readiness | NEEDS DECISION | product scope; contract already frozen |
| A6 | Parser XML/ZIP budget design | DEFERRED | belongs to a future source adapter |
| A7 | D07 formulas (FCF, debt, TTM) | BLOCKED | product approval per formula |
| A8 | D08 classification/valuation policy | BLOCKED | product approval |
| A9 | Migration `0012` authoring + disposable DB acceptance | NEEDS AUTHORIZATION | deployment stays separate |

### B. Sources / Level-1

| ID | Item | Status | Notes |
|---|---|---|---|
| B1 | Close the two EODHD premises (genuine bar vs synthetic; `.JK` price/volume/currency/continuity) | DONE 2026-10-26 — NOT CLOSED | public docs exhausted, zero calls/cost: Family A adverse (VWAP/non-exchange notice), Family B narrowed to denomination binding; see [B1 record](LEVEL1_AUTOMATED_SOURCE_ARCHITECTURE.md#backlog-b1--eodhd-jk-premise-closure-attempt-2026-10-26-wib) |
| B2 | Re-run the Level-1 blocker matrix after B1 | DONE 2026-10-26 — no row flips | reconciliation only: row 9 resolution stricter, provider calendar coverage of JK not decidable, rows 1–5/12–20 explicitly not re-examined; see [B2 record](LEVEL1_AUTOMATED_SOURCE_ARCHITECTURE.md#backlog-b2--level-1-blocker-matrix-reconciliation-after-b1-2026-10-26-wib) |
| B5 | Exchange calendar + session-completion evidence | PARTIAL 2026-10-26 — permission-blocked | a per-session summary candidate exists (IDX "Trading Summary and Recapitulation") but returns an HTTP 403 bot challenge; no bypass attempted, and IDX terms already prohibit automation, so this is unavailable-not-absent; provider "73 exchanges" calendar still uncitable (B2) |
| B3 | KSEI corporate-action coverage/permission inquiry | NEEDS DECISION | requires owner authorization to contact |
| B4 | IDX control evidence (affirmative TradingStatus) | PARKED | revisit only if it becomes the sole blocker |
| B6 | Deploy migrations `0007`–`0011` (OPS-1 follow-up) | NEEDS AUTHORIZATION | preflight + owned disposable DB first |
| B7 | F15 archive-count discrepancy (2,078 vs >5,000) | DONE 2026-10-26 — no edit needed | verified no committed doc publishes a reconciled/authoritative count; contract §17 already retains the uncertainty, and the frozen text was left untouched |
| B8 | EODHD `.JK` price adapter (5A.3c) | BLOCKED | needs B1 + admission + both premises closed |
| B9 | Stockbit secondary-provider re-evaluation | PARKED | automation permission, auth lifecycle, provenance |
| B10 | External parser reuse (idx-bei/idxlens/exploreR) | CONDITIONAL | licensing + no bypass techniques |
| B11 | Onboard any new provider | READY | config + `SourceOnboardingTests`; see `docs/SOURCE_ONBOARDING.md` |

### C. Agent/infra integration

| ID | Item | Status | Notes |
|---|---|---|---|
| C2 | Register `opencode/deepseek-v4.1-flash-free` in `model-capabilities.yaml` | PREPARED 2026-10-26 — NOT APPLIED | blocked twice: the shared repo has an unrelated untracked `scripts/common/surface_outcome.py`, and the entry's routing placement is that repo owner's decision; candidate entry drafted below |
| C1 | Typed acceptance for CLI backends (capture layer) | NEEDS DECISION | safety-relevant; `_structured_capture` requires one complete assistant message |
| C3 | Real routing: Ollama absent (`local_inference: NOT_CONFIGURED_BY_MACHINE_POLICY`) | CONDITIONAL | install Ollama or change role mapping |
| C5 | Re-render MCP bundle if paths move; lifecycle doc | READY | `scripts/install-ai-infra-mcp-bundle.sh` |
| C4 | Import DSH sessions into infra telemetry | DEFERRED | no mapping defined |

### D. Operations / housekeeping

| ID | Item | Status |
|---|---|---|
| D1 | Pilot soak 1/10 → 9 real sessions | 2/10 as of 2026-10-26 | 2026-10-23 collected via `--personal` (11 bars, 12/20 free units); 8 sessions remain |
| D2 | Model-label discrepancy (v4-pro vs Flash) | DETERMINED 2026-10-26 — not repo-fixable | `v4-pro` occurs in no file in either repo; the label exists only in the harness runtime context, so any future model claim must cite that runtime label, not a repo artifact |
| D3 | Editorial annex for "proposal" wording inside the frozen contract | OPTIONAL |
| D4 | Daily gate demands a current-day account counter, so the first run of every new day fails closed | NEEDS DECISION | provider only advances `apiRequestsDate` on the first charged request; workaround = 1-unit priming request (used 2026-10-26); fix would relax a quota-safety gate, so owner call |

## Recommended order

1. **A2** — T01–T44 acceptance table. A1 landed 2026-10-26 as logical design with four
   owner decisions outstanding (D1–D4); B1/B2 closed without unblocking Level-1, so the
   blocker matrix is closed for re-derivation and no further source re-run is worth doing
   without new external input.
2. **A4 → A5** — harden tests, close E4 edges.
3. **C2** — cheap real runs; then decide **C1**.
4. **B5**, **B3**, **B6/A9**, **B4** — only with explicit authorization (B5 is READY
   research; B3/B6/A9/B4 need an owner decision first).

## Guardrails for the next session

- Do not reinterpret frozen contracts; semantic changes need an additive version.
- Never commit secrets or market datasets; `logs/` and state dirs stay untracked.
- No provider contact, subscription, operational migration, push of another agent's work,
  or shared-infra edit without an explicit owner decision.
- Documentation-only tasks: `git diff --check` plus content review; do not claim runtime
  tests were run.
- Runtime acceptance: `dotnet test` from the root (or `rtk proxy dotnet test`), never a
  test executable; focused classes via `--filter-class`.

## C2 prepared change — not applied

`ai-local-infra` was inspected read-only (HEAD `6a4412a`, untracked
`scripts/common/surface_outcome.py` present). No file there was edited, staged or
committed. Two independent reasons, either of which is sufficient:

1. **Unrelated dirty work.** An untracked file belongs to someone else; committing into
   that repo while it is dirty risks another agent's in-progress work.
2. **It is a routing decision, not a registration.** `config/model-capabilities.yaml` is
   JSON content behind a `.yaml` name. Its `failover` object is the ordered routing table
   per task class, so *where* a model is listed decides which model serves work. Adding a
   zero-cost model is therefore an owner choice about tier placement, not a mechanical
   config append.

Candidate entry for the owner to place (verified absent today: no `v4.1-flash` token
anywhere in that repo):

```json
"model_aliases": {"Astra": "gpt-6-astra", "FlashFree": "deepseek-v4.1-flash-free"}
```

plus exactly one failover entry, e.g. under `FAST`:

```json
{"backend": "opencode", "provider": "deepseek", "model": "deepseek-v4.1-flash-free", "native_variant": "low"}
```

Open questions the owner must answer before this is applied: is the free model acceptable
for `FAST`, or should it become a separate zero-cost tier; does it need its own
`effort_mappings` entry under `deepseek`; and does the DSH bundle or MCP registration need
re-rendering afterwards (backlog C5).
