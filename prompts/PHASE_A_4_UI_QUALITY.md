# Coding Agent Prompt — Phase A.4 UI quality surfaces

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash
```

Frontend-only, no schema and no API semantics invented here. Escalate only if the
existing UI has no fetch/state pattern to copy.

## Task

Depends on Phase A.3 (`GET /api/data-health`). If that endpoint does not exist, stop
and report — do not invent it.

1. **Degraded badge.** In the price/stock surfaces, show a compact `DEGRADED` badge
   wherever a value comes from rows whose `quality_status` is not `VALID`, with a
   tooltip stating it is personal, unverified data (not an exchange tape).
2. **Data Health page.** New view at the existing SPA route style showing: last
   session, coverage per session, gaps (with holiday marker), flat/zero-volume
   observations, and the `degraded` flag. Read-only; no actions.
3. **Today card.** On the landing view, one card: last session date, bars ingested,
   degraded flag, and a link to Data Health.

## Read first

`frontend/src/ui.tsx`, `frontend/src/main.tsx`, `frontend/src/StocksPage.tsx`,
`frontend/src/stocks.test.tsx` (test pattern), `frontend/package.json`.

## Do not

- Do not claim verification, accuracy or exchange provenance anywhere in text.
- Do not add a state library or dependency; reuse what exists.
- Do not hide `DEGRADED`; the badge is the point of this task.

## Acceptance

- `cd frontend && npm run build` passes (TypeScript + Vite).
- `cd frontend && npm test` passes, with at least: badge renders for non-VALID rows,
  badge absent for VALID, Data Health renders gaps/holidays, Today card links.
- The built assets are served by the existing `.NET` API static hosting; state the
  exact command you used to view it.
- `git diff --check` clean; no push. Report screenshots are not required; describe
  what each surface shows.
