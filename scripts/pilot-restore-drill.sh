#!/usr/bin/env bash
# Restore a pg_dump into an owned disposable database, compare canonical
# fingerprints, and drop it. The protected database is fingerprinted before and
# after and must not change. No provider access and no .env read.
set -euo pipefail
cd "$(dirname "$0")/.."
dump="${1:?usage: bash scripts/pilot-restore-drill.sh <data/backups/FILE.dump>}"
test -s "$dump" || { printf 'STOP: dump not found: %s\n' "$dump" >&2; exit 1; }
container="$(docker compose ps -q postgres)"
test -n "$container" || { printf '%s\n' 'STOP: local postgres container is not running.' >&2; exit 1; }
user="${POSTGRES_USER:-idx_stock}"
source_database="${POSTGRES_DB:-idx_stock_intelligence}"
target="idx_restore_check_$(uuidgen | tr -d '-' | cut -c1-12 | tr 'A-Z' 'a-z')"

sql() {
    docker compose exec -T postgres psql -X -q -A -t -v ON_ERROR_STOP=1 -U "$user" -d "$1" -c "$2"
}
fingerprint() {
    sql "$1" "SELECT (SELECT count(*) FROM daily_bar_revision) || ':' || (SELECT count(*) FROM instrument) || ':' ||
        coalesce((SELECT md5(string_agg(instrument_id::text || ':' || session_date || ':' || revision_number || ':' ||
        canonical_content_sha256, ',' ORDER BY instrument_id, session_date, revision_number))
        FROM daily_bar_revision), '-');" | tr -d '[:space:]'
}
cleanup() {
    sql "$source_database" "DROP DATABASE IF EXISTS $target;" >/dev/null 2>&1 || true
}
trap cleanup EXIT

before="$(fingerprint "$source_database")"
sql "$source_database" "CREATE DATABASE $target;"
docker compose exec -T postgres pg_restore -U "$user" -d "$target" --no-owner --no-privileges < "$dump"
after="$(fingerprint "$target")"
rows="$(sql "$target" "SELECT count(*) FROM daily_bar_revision;" | tr -d '[:space:]')"
protected="$(fingerprint "$source_database")"
[ "$after" = "$before" ] || { printf 'FAIL: restored fingerprint differs from source.\n' >&2; exit 1; }
[ "$protected" = "$before" ] || { printf 'FAIL: protected database changed during the drill.\n' >&2; exit 1; }
printf '{"status":"PASS","dump":"%s","disposable_database":"%s","rows_restored":%s,"fingerprint":"%s","protected_database_untouched":true}\n' \
    "$dump" "$target" "$rows" "$after"
