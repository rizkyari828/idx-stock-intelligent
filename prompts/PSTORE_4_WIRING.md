# Personal store — phase 4: unattended wiring and docs

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash

Routing note: rated NORMAL/BALANCED (Ollama absent) → Flash.
```

Depends on phase 1 (capture) and ideally phase 3 (UI) for the docs screenshots/words.

## Task

1. **Wire the opt-in** through the unattended path:
   - `scripts/pilot-auto.sh` accepts `--personal` and passes it to `pilot-eod.sh`;
   - `scripts/pilot-auto-notify.sh` (the launchd entry) passes it, with a comment stating
     that this is the personal capture path;
   - a failed canonical run must not delete or block the personal capture that succeeded.
2. **Docs** — update, do not rewrite:
   - `docs/LOCAL_PILOT_WORKFLOW.md` personal section: the run now captures to
     `data/personal/` and skips canonical ingestion when the session is unproven; say
     plainly that such data is not canonical, cannot affect soak, and never becomes
     `VERIFIED`.
   - `docs/PERSONAL_UNPROVEN_INGESTION.md`: change status from OPEN to IMPLEMENTED-as-option-B
     and record what was actually built, keeping the three-layer finding intact.
   - `docs/NEXT_SESSION_BACKLOG.md`: close D5 with a one-line outcome and pointer.
3. **Checklist for the owner** in the docs: backup expectation for `data/personal/`
   (git-ignored, not in Postgres), and what to do when a session later gets real proof
   (canonical ingestion may then proceed; personal capture is not migrated silently).

## Do not

- Do not enable, install or load a launchd job; preparation only.
- Do not change quota logic, ceilings, soak rules or any canonical admission code.
- Do not claim the pipeline is unattended until the plist is actually installed by the
  owner; the docs must keep that distinction.

## Acceptance

- `bash -n scripts/pilot-auto.sh scripts/pilot-auto-notify.sh` passes.
- `bash scripts/pilot-auto.sh --dry-run` still prints a plan with `provider_requests: 0`
  and does not create personal files.
- Python suite and full `dotnet test` pass (report totals).
- `git diff --check` clean; commit scoped; no push. Report exactly what a daily run now does,
  step by step, and what remains manual.
