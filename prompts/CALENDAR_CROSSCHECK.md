# Coding Agent Prompt — calendar cross-verification

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash
```

## Why this matters more than it looks

`pilot/holidays-2026.json` holds 22 IDX closures, all citing a **single** secondary
publisher (`mediaasuransinews.co.id`), with corroboration limited to trading-day counts.
Since 2026-10-26 the unattended path is **proof-free**: `scripts/pilot-auto.sh` selects
the newest weekday that is not a listed holiday. So this fixture is the only thing
standing between "holiday" and "collected as a trading session". A missing date produces
fabricated-looking bars; a wrong extra date silently skips a real session.

## Task

Independently verify every date in the fixture against **at least one additional source
that is not the existing publisher**.

1. Read `pilot/holidays-2026.json` first and list its 22 dates with names.
2. Find independent evidence per date. Preference order:
   a. official Indonesian government/IDX/KSEI published schedule (one-off reading);
   b. two independent news/finance outlets that list the exchange calendar.
3. For each date record the outcome: `CONFIRMED` (independent source agrees),
   `CONFLICT` (independent source disagrees), or `SINGLE_SOURCE` (nothing independent
   found).
4. Apply the findings to the fixture **conservatively**:
   - add a `second_reference` (URL) and `verified_at` where confirmed;
   - add `verification: "SINGLE_SOURCE"` where nothing independent was found — do **not**
     delete a date without conflicting evidence;
   - if an independent source names a closure that is missing, add it with both
     references; if a source contradicts a date, stop and report instead of guessing.
5. Update the fixture's top-level `sources` / `corroboration_method` fields to describe
   what was actually verified.

## Do not

- Do not scrape `idx.co.id` or call any provider API. If a page returns a bot challenge,
  record it and move on — never bypass it.
- Do not invent a date, a name, or a reference URL. If evidence is thin, say so.
- Do not touch `pilot/sessions.json`, collector code, quota logic or any frozen contract.
- No provider units: this task must consume **0** API calls.

## Acceptance

- `python3 -c "import json;json.load(open('pilot/holidays-2026.json'))"` succeeds and the
  date count is unchanged unless you explicitly report a justified addition/removal.
- `bash scripts/pilot-auto.sh --dry-run` still selects expected date(s) with
  `provider_requests: 0`.
- `PYTHONPATH=collectors/python/src python3.13 -m unittest discover -s collectors/python/tests`
  passes.
- `git diff --check` clean. Commit only the fixture (and a short docs note if you add
  one); **no push**.

## Report

A table of all 22 dates with: name, existing reference, independent reference found,
verdict (`CONFIRMED` / `CONFLICT` / `SINGLE_SOURCE`). Then state plainly what remains
unverified — that list is the real output, not the green checks.
