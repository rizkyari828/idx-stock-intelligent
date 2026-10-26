# Coding Agent Prompt — Phase B.3 market context in the decision journal

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash
```

## Task

Show market context next to the existing versioned thesis journal, so a decision can be
read against the price that existed when it was taken. **Do not redesign the journal and
do not touch outcomes.**

1. In `frontend/src/ThesisJournal.tsx`, for the selected holding, display: the newest
   canonical bar date, close, `quality_status`, and (when `AVAILABLE` from the Phase B.1
   projection) `changePercent` and `distanceToHighPercent`.
2. State the bar's date explicitly next to the price. A stale price must look stale.
3. If the instrument has no canonical bar, show "no canonical bar recorded" — never `0`,
   never a blank that looks like zero.
4. Keep the existing versioned behaviour intact: creating/invalidating a thesis must not
   change, and earlier versions must remain immutable.

## Read first

`frontend/src/ThesisJournal.tsx`, `frontend/src/api.ts`, `frontend/src/view.ts`,
`frontend/src/dataHealth.tsx` (Phase A pattern), `src/IdxStockIntelligence.Api/Program.cs`
(portfolio/thesis endpoints), Phase B.1 output.

## Do not

- Do not write, promote or infer any Outcome, `decision_snapshot`, or verification state.
  This is display context only.
- Do not add a "recommended action", score, target price or advice text.
- Do not backfill a price onto a thesis whose date predates the bar; show the bar's own
  date and let the reader see the difference.
- No schema change, no migration, no provider call.

## Acceptance

- Frontend tests: bar present → date + close + quality shown; bar absent → explicit
  message; warmup feature → not rendered as a number.
- `cd frontend && npm run build && npm test` passes (baseline 105 tests).
- If any .NET endpoint changes, full root `dotnet test` passes and new totals are reported.
- `git diff --check` clean; no push. Report which files changed and confirm the thesis
  create/invalidate path is untouched (ideally by pointing at the unchanged tests).
