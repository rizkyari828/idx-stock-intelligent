# Local operations — 2026-10-26

One command to run the product, a Postgres backup with a restore drill, and a
prepared-but-not-installed weekday scheduler. Every command here spends **zero
EODHD units**; none of them contacts a provider.

## Run UI and API from one port

```sh
bash scripts/run-local.sh
# Serving UI and API at http://127.0.0.1:5080
```

The script requires an ignored, untracked `.env` with `POSTGRES_PASSWORD`, builds
`frontend/dist`, derives the loopback connection string, and starts
`IdxStockIntelligence.Api`. The SPA (`/`, `/stocks/...`, `/decisions/...`,
`/data-health`) and `/api/*` share that one port. `IDX_API_PORT` overrides the
port; `IDX_DATABASE_CONNECTION` overrides the derived connection string.

## Backup

```sh
bash scripts/pilot-backup.sh
# {"database":"idx_stock_intelligence","backup":"data/backups/...dump","bytes":...,"sha256":"..."}
```

`pg_dump -Fc` runs inside the running container, so the dump is database state
only. It never reads `.env` and never copies credentials; `data/backups/` is
ignored by git. Override the destination with `IDX_BACKUP_DIR`.

## Restore drill

```sh
bash scripts/pilot-restore-drill.sh data/backups/idx_stock_intelligence-<UTC>.dump
```

Creates an owned disposable database, restores with
`pg_restore --no-owner --no-privileges`, compares a canonical fingerprint (bar
revision count, instrument count, MD5 over revision identity + content hash)
with the source, prints the restored row count, then drops the disposable
database. The protected database is fingerprinted before and after and must be
unchanged. Observed on 2026-10-26: **97 rows restored**, fingerprint identical,
protected database untouched.

The older offline replay check is still available and creates/drops its own
database:

```sh
python3.13 scripts/check_pilot_restore.py
```

## Weekday scheduler — PREPARED, NOT INSTALLED

`scripts/launchd/com.idxintelligence.pilot-auto.plist` is a template for
07:00 local time, Monday–Friday. It runs `scripts/pilot-auto-notify.sh`, which
logs under `logs/`, appends `logs/pilot-auto.failure.log` and posts a macOS
notification when the run fails. `__REPO__`/`__HOME__` are substituted at
install time so no absolute developer path is committed.

Nothing was installed and `launchctl` was not called. The owner's exact commands,
from the repository root:

```sh
cd /Users/tovantest/Developer/Back-End/idx-stock-intelligent
mkdir -p "$HOME/Library/LaunchAgents" logs
sed -e "s|__REPO__|$PWD|g" -e "s|__HOME__|$HOME|g" \
    scripts/launchd/com.idxintelligence.pilot-auto.plist \
    > "$HOME/Library/LaunchAgents/com.idxintelligence.pilot-auto.plist"
launchctl bootstrap gui/"$(id -u)" "$HOME/Library/LaunchAgents/com.idxintelligence.pilot-auto.plist"
launchctl print gui/"$(id -u)"/com.idxintelligence.pilot-auto
```

To test the job body without loading it: `bash scripts/pilot-auto.sh --dry-run`.

Unload and remove:

```sh
launchctl bootout gui/"$(id -u)"/com.idxintelligence.pilot-auto
rm "$HOME/Library/LaunchAgents/com.idxintelligence.pilot-auto.plist"
```

Two honest limits: 07:00 is the Mac's local clock, so the machine must remain on
Asia/Jakarta (or `Hour` must be adjusted); and this path is proof-free by design —
it selects the newest weekday that is not a cited holiday and refuses only a date
already attempted (`--date` overrides). Because there is no independent session
proof, a closure missing from `pilot/holidays-2026.json` would be collected as if it
were a trading session; keeping that fixture correct is the price of unattended runs.
