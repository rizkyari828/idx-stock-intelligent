#!/usr/bin/env bash
set -eu
cd "$(dirname "$0")/.."
probe_local_only=false
for argument in "$@"; do
    case "$argument" in --dry-run|--help) probe_local_only=true;; esac
done
if test "$probe_local_only" = false; then
    git check-ignore -q .env || { printf '%s\n' 'STOP: .env must be ignored.' >&2; exit 1; }
    if git ls-files --error-unmatch .env >/dev/null 2>&1; then
        printf '%s\n' 'STOP: .env must be untracked.' >&2; exit 1
    fi
    set -a
    source .env >/dev/null 2>&1
    set +a
    test -n "${EODHD_API_TOKEN:-}" || { printf '%s\n' 'STOP: provider token absent.' >&2; exit 1; }
fi
probe_python="${IDX_PYTHON:-}"
if test -z "$probe_python"; then
    for candidate in python3.13 python3.12 python3; do
        if command -v "$candidate" >/dev/null 2>&1; then probe_python="$candidate"; break; fi
    done
fi
export PYTHONPATH=collectors/python/src
exec "$probe_python" -m idx_stock_collector.entitlement_probe "$@"
