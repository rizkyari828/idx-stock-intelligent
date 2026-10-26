# Phases C–E — gates, not build prompts

These phases are **deliberately not coding prompts yet**. Each is blocked by an owner
decision, an authorization, a permission, or money — and writing an implementation prompt
before the gate clears would produce speculative work. This file records exactly what
clears each gate, so the prompt can be written the moment it does.

## Phase C — housekeeping and decisions

| Item | What the owner must decide | Recommended default | Where it lives |
|---|---|---|---|
| D4 daily counter gate | accept the workaround, or fix the gate | **accept**: the per-date path calls `remaining(strict=False)`, so no priming is needed | `docs/LOCAL_PILOT_WORKFLOW.md` (personal mode section) |
| A1/D1 receipt representation | canonical JSON vs three typed columns + composite FK | **three columns**, canonical JSON reconstructed deterministically | `docs/FUNDAMENTAL_BINDING_PERSISTENCE_DESIGN_V0_1.md` §7 |
| A1/D2 artifact references | JSON vs `*_artifact_id` FK columns | **id columns**, writer verifies equality | same §7 |
| A1/D3 `subject_key` projection | allow a non-authoritative derived key? | **yes**, with a recomputation test | same §7 |
| A1/D4 payload discriminators | duplicate `operation`/`completeness` as columns? | **yes**, so ASSERT/CANCEL rules become real DDL | same §7 |
| A1/D5 lineage on revisions > 1 | is `lineageEvidence` mandatory after revision 1? | **owner reading required** — §18.3 is genuinely ambiguous | contract §18.3 |
| A9 migration `0012` | authorize authoring + disposable-DB acceptance? | authoring only, no deployment | backlog A9 |
| A3 reader into readiness | product scope decision | **cannot start before A9** — no fundamental table or reader exists | backlog A3 |
| C1 typed acceptance for CLI backends | reject partial capture (safe) or accept it? | **reject**; one complete assistant message | `ai-local-infra` `scripts/common/execution.py` |
| C2 model registration | `FAST` tier, or a separate zero-cost tier? | separate tier; does not displace existing routing | `ai-local-infra` `config/model-capabilities.yaml` |
| C5 MCP bundle lifecycle | none — bundle already checks out (`route ok`, `orchestrate ok`) | document only | this repo |

**Hard blocker for C1 and C2:** `ai-local-infra` carried **24 uncommitted changes** at the
last check (it had 1 untracked file earlier the same day). Nothing in that repository may
be edited until its owner commits or stashes their work. Confirm with
`mcp__ai-infra__ai_infra_check` before starting either item.

## Phase D — verified Level-1 stack (parked)

Requires paid licensing and/or written permission. Not obtainable by code.

| Item | Gate |
|---|---|
| B5 session-completion proof | a lawful automated completion source; the IDX page returns a bot challenge and IDX terms prohibit automation |
| B3 corporate-action coverage | written KSEI permission + a complete all-type window |
| B4 affirmative TradingStatus | IDX licensed route (parked as a sole-blocker retry) |
| B6 deploy migrations `0007`–`0011` | explicit operational authorization + preflight on an owned disposable database |

Consequence while parked: no `VERIFIED`, `MARKET_ELIGIBLE` or `DATA_READY` result may ever
be produced. Descriptive diagnostics and personal analysis remain fully usable.

## Phase E — fundamental data (parked)

Requires a data source with rights, and product decisions on formulas.

| Item | Gate |
|---|---|
| A6 parser XML/ZIP budgets | no admitted source yet |
| XBRL/XLSX parser | a permitted original-filing route; ideas from `idxlens`/`idx-mcp` are reusable, bypass techniques are not |
| A7 formulas (FCF, debt, TTM) | per-formula product approval |
| A8 classification/valuation policy | product approval |
| A9 deployment | separate authorization after authoring |

**A1's logical design and A5's scope/audit constraints are already done**; what is missing
is a lawful source and the formula approvals, not design.

## Rule for whoever picks these up

Do not convert a parked phase into code to show progress. A gate that clears is worth one
precise prompt; a gate that has not cleared is worth exactly this file.
