#!/usr/bin/env bash
# launchd entry point. Prepared only, NOT installed. The plist redirects
# stdout/stderr into logs/; a failed run also leaves a marker and a notification.
set -u
cd "$(dirname "$0")/.."
mkdir -p logs
bash scripts/pilot-auto.sh
status=$?
if [ "$status" -ne 0 ]; then
    printf '%s pilot-auto exit %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$status" >> logs/pilot-auto.failure.log
    /usr/bin/osascript -e "display notification \"pilot-auto failed (exit $status). See logs/pilot-auto.err.log\" with title \"IDX pilot\"" >/dev/null 2>&1 || true
fi
exit "$status"
