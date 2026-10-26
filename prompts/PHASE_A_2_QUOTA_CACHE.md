# Coding Agent Prompt — Phase A.2 quota cache reuse

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash

Escalate only on a concrete failure:
Codex / Sol, thinking Medium
```

Why Flash is enough: the reuse rule already exists (`reusable_entries`) and only its
*precondition* is wrong for our case. Escalate if the fix would relax a quota or
provenance gate instead of keying reuse correctly.

## Problem

Re-running an already-collected session costs a full panel again. Observed
2026-10-26: `--from 2026-10-23 --to 2026-10-23 --dry-run` reported
`cached_symbols: []` even though 2026-10-23 was already ingested, because reuse
requires independent session proof for that date.

## Task

Make reuse key on **canonical state equality with intact evidence**, not on session
proof:

- For each symbol, reuse only when a retained canonical row exists for the requested
  date whose content matches the stored operation evidence (same content hash,
  artifact intact) — the same equality rule `daily_plan` already applies when it
  matches `worker["canonical_state"]` against current state.
- If any symbol's canonical row is missing, degraded differently, or its artifact
  evidence is not intact, fetch that symbol normally.
- Never treat cached state as proof of session completion, and never let reuse
  advance soak. Soak keeps counting only real SUCCEEDED runs.

## Do not

- Do not raise `daily_unit_ceiling`, remove the Free-account assertion, or change
  `remaining()` (backlog D4 stays untouched; the direct-date path calls it with
  `strict=False`).
- Do not weaken the "clean, completed canonical session" rule; implement a distinct
  state-based reuse and say why it is safe.

## Acceptance

- Second run for an already-complete date: `quota_units: 0`, zero new canonical rows,
  no provider fetch, and the command still exits with a clear status.
- A date whose stored row differs from canonical state still refetches (test it).
- A missing instrument in an otherwise complete date refetches only that instrument.
- Python discovery passes; `git diff --check` clean; no push.
- Report units consumed before/after and the exact rule you implemented.
