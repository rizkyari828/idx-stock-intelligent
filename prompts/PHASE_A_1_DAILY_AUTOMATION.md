# Coding Agent Prompt — Phase A.1 daily automation

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash

Routing note: ai-local-infra classifies this as NORMAL/BALANCED and suggests
qwen3.6:27b-coding via Ollama. That model is NOT installed on this machine
(backlog C3: local_inference NOT_CONFIGURED_BY_MACHINE_POLICY), so Flash is the
cheapest *available* choice. Do not install Ollama for this task.
```

Why Flash is enough: the change is a date-selection helper plus a thin shell wrapper
around an existing, tested collector. The failure mode is visible in a diff and a
dry-run, and every date decision is covered by a unit test.

## Read first

`AGENTS.md`, `docs/AGENT_EXECUTION_RULES.md`, `docs/LOCAL_PILOT_WORKFLOW.md`
(the "Personal mode" section), `collectors/python/src/idx_stock_collector/pilot.py`
(`remaining`, `collection_eligibility`), `scripts/pilot-eod.sh`.

## Task

1. Create `pilot/holidays-2026.json`: the IDX trading-holiday list for 2026.
   - Every date MUST carry a source URL and `known_at`, exactly like
     `pilot/sessions.json` entries. Two independent public sources if possible.
   - If a date cannot be justified from a retained source, omit it and say so.
     Never invent a holiday, and never include a weekend (weekends are handled
     separately).
2. Create `scripts/pilot-auto.sh`:
   - Determine the most recent Jakarta trading day strictly before today,
     skipping Saturday/Sunday and any date in `pilot/holidays-2026.json`.
   - Invoke `bash scripts/pilot-eod.sh --from <date> --to <date> --ingest`.
   - Accept `--dry-run` (print the chosen date, call nothing) and `--date YYYY-MM-DD`
     (explicit override, still validated against weekend/holiday).
   - Do NOT route through `pilot-daily.sh --daily`: that path requires per-session
     independent proof and a current-day account counter (backlog D4). The direct
     date path already avoids both and is recorded as mode DAILY for soak.
3. Add unit tests for the date selector: normal weekday, Saturday, Sunday, a
   holiday-only gap, a holiday list that is empty, and `--date` override on a
   holiday (must refuse).

## Do not

- Do not scrape or call idx.co.id. The holiday list is entered from published
  schedules and cited; automation at runtime touches only the existing collector.
- Do not touch quota/`remaining` logic (that is Phase A.2).
- Do not change `pilot/sessions.json`, soak policy, or any frozen contract.

## Acceptance

- `PYTHONPATH=collectors/python/src python3.13 -m unittest discover -s collectors/python/tests`
  passes (currently 64 tests, 1 skipped).
- `bash scripts/pilot-auto.sh --dry-run` prints one weekday date and makes no
  provider request (`provider_requests: 0`).
- The script refuses to run twice for the same date without a `--date` override
  (report what prevents this from being a silent quota leak).
- `git diff --check` clean. No push.

## Report

State: the date chosen, the holiday-count with source URLs, tests run, provider
units consumed (**0** expected for dry-run), and anything you refused to automate.
