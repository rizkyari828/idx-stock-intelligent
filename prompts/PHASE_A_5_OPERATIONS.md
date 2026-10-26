# Coding Agent Prompt — Phase A.5 operations

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash
```

## Task

1. **One command to run the app.** Add a documented command (script or `make`-style)
   that builds the frontend and starts `IdxStockIntelligence.Api` so the UI and API
   are served from one port. Print the exact URL.
2. **Backup/restore.** Document and script a Postgres backup using the tested restore
   path (`scripts/check_pilot_restore.py`); prove a restore into an owned disposable
   database. Backups must never include `.env` or credentials.
3. **Scheduler — DO NOT INSTALL WITHOUT EXPLICIT OWNER APPROVAL.** Prepare, but do not
   load, a `launchd` job that runs `scripts/pilot-auto.sh` at 07:00 Asia/Jakarta on
   weekdays, with a log path and a failure notification. State the exact `launchctl`
   commands the owner must run, and confirm nothing was installed.

## Read first

`docs/LOCAL_PILOT_WORKFLOW.md`, `scripts/check_pilot_restore.py`,
`compose.yaml`, `src/IdxStockIntelligence.Api/Program.cs` (static hosting).

## Do not

- Do not install, load or unload a launchd job; preparation only.
- Do not commit backups or any file containing secrets.
- Do not change collection, quota or soak behaviour.

## Acceptance

- The one-command start works from a clean shell and the UI loads against the local
  database; report the URL and the exact command.
- Restore drill passes into a disposable database; report the rows restored and that
  the protected database was untouched.
- The launchd plist exists in the repo with a comment saying it is not installed, plus
  the owner's exact install commands.
- `git diff --check` clean; no push.
