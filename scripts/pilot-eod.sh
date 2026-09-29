#!/usr/bin/env bash
set -eu
cd "$(dirname "$0")/.."
pilot_local_only=false
for argument in "$@"; do
    case "$argument" in --dry-run|--soak-report|--offline|--help) pilot_local_only=true;; esac
done
if test "$pilot_local_only" = false; then
    git check-ignore -q .env || { printf '%s\n' 'STOP: .env must be ignored.' >&2; exit 1; }
    if git ls-files --error-unmatch .env >/dev/null 2>&1; then
        printf '%s\n' 'STOP: .env must be untracked.' >&2; exit 1
    fi
    set -a
    source .env >/dev/null 2>&1
    set +a
    test -n "${EODHD_API_TOKEN:-}" || { printf '%s\n' 'STOP: provider token absent.' >&2; exit 1; }
fi
pilot_python="${IDX_PYTHON:-}"
if test -z "$pilot_python"; then
    for candidate in python3.13 python3.12 python3; do
        if command -v "$candidate" >/dev/null 2>&1; then pilot_python="$candidate"; break; fi
    done
fi
"$pilot_python" -c 'import sys; assert sys.version_info >= (3, 12), "Python 3.12+ required"'
export PYTHONPATH=collectors/python/src
exec "$pilot_python" -m idx_stock_collector.pilot "$@" --ingest
