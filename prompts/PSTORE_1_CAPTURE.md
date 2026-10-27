# Personal store — phase 1: capture (Python)

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash

Routing note: ai-local-infra rates this CORRECTNESS_SENSITIVE/STRONG and resolves to
deepseek-flash. Phases 2–4 are rated NORMAL/BALANCED and resolve to qwen3.6 via Ollama,
which is NOT installed on this machine, so they also run on Flash.
```

## Goal

A session without independent completion proof can still be captured **for personal use**,
without ever entering canonical admission. Canonical ingestion is untouched: when a
session has no proof we simply do not call the worker at all.

## Read first

`collectors/python/src/idx_stock_collector/pilot.py` (`collection_eligibility`,
`collection_plan`, `collect`, `ingest_and_summarize`, `main`),
`docs/PERSONAL_UNPROVEN_INGESTION.md` (why three layers exist),
`docs/LOCAL_PILOT_WORKFLOW.md` (personal mode section).

## Task

1. **Re-add the explicit Python opt-in** (it was implemented, then reverted):
   - `collection_eligibility(..., allow_unproven=False)` → for a weekday with no proof,
     past the cutoff, not a future date: `session_proof="UNPROVEN_PERSONAL"`,
     `reason="ELIGIBLE_UNPROVEN_PERSONAL"`, `eligible=True`.
   - Everything else must keep refusing: future dates, `SAFE_EOD_CUTOFF_NOT_REACHED`,
     weekends (`CLOSED_BY_CALENDAR`), `CONFLICTING_SESSION_PROOF`, and any proof that
     existed but failed validation.
   - `collection_plan(..., allow_unproven=False)` passes it through.
2. **Refuse a cited holiday** even when the opt-in is on: load `pilot/holidays-2026.json`
   (via `pilot_auto.load_holidays`) and raise if the requested date is listed.
3. **Write the personal store** after a successful fetch, in `collect()` or immediately
   after it, only when `args.personal`:
   - path `data/personal/bars/<YYYY>/<YYYY-MM-DD>.json`;
   - one file per session; fields: `schema_version`, `session_date`, `captured_at`,
     `session_proof` (`UNPROVEN_PERSONAL` or `PROVEN`), `source_id`, and `rows[]` with
     `symbol`, `instrument_id`, `open|high|low|close|adjusted_close` as **exact decimal
     strings**, `volume` as integer, `content_sha256`, `artifact_sha256`, `fetched_at`,
     `quality_status`.
   - **Never overwrite.** If the file exists with identical bytes, no-op. If it differs,
     write a new capture at a revision-suffixed name and report it; do not replace history.
4. **Skip canonical ingestion** when the batch contains any `UNPROVEN_PERSONAL` date.
   Print an explicit line saying personal capture succeeded and canonical ingestion was
   skipped because the session is unproven. Do not attempt the worker and do not create a
   FAILED operation report.
5. `data/` is git-ignored: nothing captured may be committed.

## Do not

- Do not touch `CompletedSessionPolicy`, `ScreenerEvidence`, `PilotValidation`, the worker,
  or any migration. Canonical admission must stay exactly as it is.
- Do not add a database table.
- Do not call any provider in tests; use fixtures.

## Acceptance

- `PYTHONPATH=collectors/python/src python3.13 -m unittest discover -s collectors/python/tests`
  passes (baseline 88 tests, 1 skipped) with new tests for: opt-in eligible/refused matrix,
  holiday refusal, idempotent re-capture, and differing-capture revision naming.
- A dry-run proves 0 provider requests.
- `git diff --check` clean; commit scoped; no push.
- Report: exact file layout, the revision rule, and proof that canonical ingestion was
  skipped (command + output).
