#!/usr/bin/env bash
# launchd entry point. Prepared only, NOT installed. The plist redirects
# stdout/stderr into logs/; a failed run also leaves a marker and a notification.
# This is the personal capture path: the personal flag is passed to
# pilot-auto.sh, so a session that lacks independent completion proof is
# captured into data/personal/bars instead of being refused. Canonical
# ingestion is still skipped for that session, so the capture never becomes
# canonical and never affects soak. A proven session is ingested canonically
# as before, with the personal capture as an additional local copy.
set -u
cd "$(dirname "$0")/.."
mkdir -p logs
bash scripts/pilot-auto.sh --personal
status=$?
if [ "$status" -ne 0 ]; then
    printf '%s pilot-auto exit %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$status" >> logs/pilot-auto.failure.log
    /usr/bin/osascript -e "display notification \"pilot-auto failed (exit $status). See logs/pilot-auto.err.log\" with title \"IDX pilot\"" >/dev/null 2>&1 || true
fi
exit "$status"
