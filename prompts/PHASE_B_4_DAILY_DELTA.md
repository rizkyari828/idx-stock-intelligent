# Coding Agent Prompt — Phase B.4 daily delta summary

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash
```

## Task

Answer one question in the UI: **"what changed since the last session I saw?"** —
factually, with no interpretation.

1. Extend the existing read-only `/api/data-health` surface (or add a sibling endpoint)
   with a `delta` block comparing the two newest canonical sessions:
   - new session date(s) since the requested `since` date (default: previous session);
   - per instrument: close change in absolute and percent terms, or an explicit
     `MISSING` when either session lacks that instrument;
   - counts of flat (`open=high=low=close`) and zero-volume rows in the new session,
     labelled `observation` — never "no trade", "suspended" or "holiday";
   - any instrument whose panel coverage differs between the two sessions.
2. Frontend: a "Since last session" card on the landing view listing the above, with the
   existing DEGRADED badge when applicable.
3. Read-only. No new tables, no caching, no writes.

## Read first

`src/IdxStockIntelligence.Api/Program.cs` (`/api/data-health` added in Phase A),
`frontend/src/dataHealth.tsx`, `docs/LOCAL_PILOT_WORKFLOW.md` (personal mode limits).

## Do not

- Do not describe a price move as a cause, signal, breakout, or recommendation.
- Do not infer session status from bar shape or volume (the pilot has no confirmed
  trading-status evidence; say `observation` and stop).
- Do not treat a missing instrument as unchanged, and never divide by a missing close.
- No schema change, no provider call, no quota use.

## Acceptance

- Tests: two sessions with a normal change; an instrument present in only one session;
  a flat row; a zero-volume row; and an empty database (explicit empty state, no crash).
- Full root `dotnet test` passes; report totals. `cd frontend && npm run build && npm test`
  passes.
- `git diff --check` clean; no push. Report the exact `delta` JSON shape.
