# Coding Agent Prompt — Phase A.3 data-health API

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash
```

Routing classified this CORRECTNESS_SENSITIVE/STRONG, whose resolved model is already
`deepseek-flash`. No escalation is needed for a read-only aggregate endpoint.

## Task

Add one read-only minimal-API endpoint `GET /api/data-health` returning, from the
existing database only:

- `lastSession`: newest `session_date` in `daily_bar_revision`;
- `sessions`: per date — bar count, distinct instruments, quality status counts;
- `expectedPanel`: 11 (10 equities + `JKSE.INDX`) and per-session coverage gap;
- `gaps`: weekdays between the first and last canonical session with no rows, and
  whether each is a known holiday (read `pilot/holidays-2026.json` if present);
- `anomalies`: rows that are flat (`open=high=low=close`) or zero-volume, per date,
  listed as observations — NOT as proof of no-trade/suspension;
- `quality`: `DEGRADED`/`VALID`/… counts and a single `degraded: true|false` flag;
- `generatedAt` (UTC).

## Do not

- Do not write, ingest, mutate or backfill anything. Read-only SQL only.
- Do not infer session status, tradability, suspension or "no trade" from bar shape
  or zero volume; describe them as observations only.
- Do not expose credentials, DSNs, artefact paths or raw payloads.
- No schema change, no migration.

## Read first

`src/IdxStockIntelligence.Api/Program.cs` (endpoint style), an existing read-only
Infrastructure reader, `src/IdxStockIntelligence.Infrastructure/Migrations/0001_phase0_foundation.sql`
(`daily_bar_revision` columns), `docs/LOCAL_PILOT_WORKFLOW.md` (personal mode).

## Acceptance

- Focused test class covering: empty database, one session, a date with missing panel
  members, flat/zero-volume rows, and a holiday in `gaps`.
- Full root `dotnet test` from the repo root passes (baseline 1,262 total / 1,058
  passed / 204 skipped / 0 failed); it will change by your new tests only.
- Endpoint works with the local Docker Postgres, or returns an explicit unavailable
  status instead of throwing.
- `git diff --check` clean; no push. Report the exact JSON shape you shipped.
