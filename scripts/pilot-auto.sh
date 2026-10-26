#!/usr/bin/env bash
# One unattended daily collection: pick the latest completed Jakarta trading
# day from cited calendars, then hand that exact date to the existing collector.
# Planning reads local files only; --dry-run makes no provider request. This
# deliberately does not use pilot-daily.sh, whose per-session proof and
# same-day counter are operator paths.
set -eu
cd "$(dirname "$0")/.."

dry_run=false
explicit=""
while test $# -gt 0; do
    case "$1" in
        --dry-run) dry_run=true; shift ;;
        --date)
            test $# -ge 2 || { printf '%s\n' 'STOP: --date needs YYYY-MM-DD.' >&2; exit 64; }
            explicit="$2"; shift 2 ;;
        --help|-h) sed -n '2,8p' "$0"; exit 0 ;;
        *) printf 'STOP: unknown argument %s\n' "$1" >&2; exit 64 ;;
    esac
done

pilot_python="${IDX_PYTHON:-}"
if test -z "$pilot_python"; then
    for candidate in python3.13 python3.12 python3; do
        if command -v "$candidate" >/dev/null 2>&1; then pilot_python="$candidate"; break; fi
    done
fi
test -n "$pilot_python" || { printf '%s\n' 'STOP: Python 3.12+ is required.' >&2; exit 1; }
export PYTHONPATH=collectors/python/src

plan_mode=RUN
if test "$dry_run" = true; then plan_mode=DRY_RUN; fi
if test -n "$explicit"; then
    plan="$("$pilot_python" -m idx_stock_collector.pilot_auto plan --mode "$plan_mode" --date "$explicit")"
else
    plan="$("$pilot_python" -m idx_stock_collector.pilot_auto plan --mode "$plan_mode")"
fi
read -r target refused <<<"$("$pilot_python" -c \
    'import json,sys; p=json.load(sys.stdin); print(p["date"], "true" if p["refused"] else "false")' <<<"$plan")"

if test "$dry_run" = true; then
    printf '%s\n' "$plan"
    exit 0
fi

if test "$refused" = true; then
    printf '%s\n' "$plan"
    printf '%s\n' "STOP: $target was already attempted by this service; it is not retried" \
        "automatically. Re-run once with --date $target only after a human review." >&2
    exit 3
fi

"$pilot_python" -m idx_stock_collector.pilot_auto record --date "$target" --status STARTED >/dev/null
status=0
bash scripts/pilot-eod.sh --from "$target" --to "$target" --ingest || status=$?
"$pilot_python" -m idx_stock_collector.pilot_auto record \
    --date "$target" --status FINISHED --exit-code "$status" >/dev/null
printf 'PILOT-AUTO: %s finished with exit status %s\n' "$target" "$status" >&2
exit "$status"
