# Personal store — backfill (B.1b-1)

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash
```

## Why

Technical features are starved, not broken. The nine canonical sessions are sparse
(2026-08-24, 08-26, 09-23, 09-24, 09-25, 09-29, 09-30, 10-02, 10-23) and the frozen
Screener contract requires a **calendar-continuous** window, so the newest run has one
session and 12 of 13 features are WARMUP.

One EODHD range request returns a whole year for **1 unit**. So a single backfill of the
11-instrument panel (≈11 units, once) supplies ~240 sessions and makes EMA/ATR/RS usable
without waiting 50 trading days.

## Task

Add a personal **historical backfill** that fills `data/personal/bars/` from a date range.

1. New explicit flag combination accepted by the collector CLI: `--personal --backfill`
   together with `--from/--to`. Refuse `--backfill` without `--personal`, without both
   dates, with a range shorter than 2 days, with a future date, or with a range longer
   than the documented free-history bound already used by the collector (≈330 days).
2. Under this mode the per-date session-eligibility loop must NOT run — this is a
   personal historical capture, not a claimed completed session. Everything else stays:
   future dates refused, panel size fixed at 11, normal payload validation, exact
   decimal strings.
3. Reuse the phase-1 personal store writer, but write **one file per session date**
   found in the provider rows, in the same document shape
   (`data/personal/bars/<YYYY>/<YYYY-MM-DD>.json`), `session_proof: "UNPROVEN_PERSONAL"`,
   `quality_status: "UNPROVEN_PERSONAL"`, revision rule unchanged (never overwrite).
4. **Drop rows whose date is a cited exchange holiday** (`pilot/holidays-2026.json` via
   `pilot_auto.load_holidays`) and count them. Rationale: the provider is known to emit
   carry-forward rows on IDX closures (ANTM/GOTO on 2026-08-17 and 08-25); admitting them
   would corrupt continuity. Report the dropped count explicitly; never drop silently.
5. Canonical ingestion is always skipped in backfill mode: no worker call, no operation
   report, no soak effect. Print a summary: sessions written, rows written, rows dropped
   as cited holidays, units reserved.
6. Idempotency: re-running the same range writes nothing new (`UNCHANGED` per session) and
   still spends the units — say so in the summary so the cost is not a surprise.

## Do not

- Do not touch canonical admission, the Worker, migrations, quota ceilings or soak.
- Do not fetch a range that includes today or a future date.
- Do not run a live backfill in this task: quota is exhausted today and the owner will run
  it tomorrow morning. Verify with stubbed responses only.

## Acceptance

- Python discovery passes (baseline 95 tests, 1 skipped) with new tests: range accepted,
  holiday rows dropped and counted, one file per session, idempotent re-run, refusals for
  each invalid flag combination, and no canonical call (patch `subprocess.run` to fail).
- `bash scripts/pilot-auto.sh --dry-run` unchanged, `provider_requests: 0`.
- `git diff --check` clean; commit scoped; no push.
- Report: the exact command the owner should run tomorrow, expected unit count, and the
  literal test output.
