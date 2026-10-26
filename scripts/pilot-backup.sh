#!/usr/bin/env bash
# Postgres-only backup of the local pilot database. Never reads or copies .env.
set -eu
cd "$(dirname "$0")/.."
container="$(docker compose ps -q postgres)"
test -n "$container" || { printf '%s\n' 'STOP: local postgres container is not running.' >&2; exit 1; }
user="${POSTGRES_USER:-idx_stock}"
database="${POSTGRES_DB:-idx_stock_intelligence}"
directory="${IDX_BACKUP_DIR:-data/backups}"
mkdir -p "$directory"
target="$directory/${database}-$(date -u +%Y%m%dT%H%M%SZ).dump"
temporary="$target.tmp"
trap 'rm -f "$temporary"' EXIT
docker exec "$container" pg_dump -U "$user" -d "$database" -Fc > "$temporary"
test -s "$temporary" || { printf '%s\n' 'STOP: pg_dump produced an empty backup.' >&2; exit 1; }
mv "$temporary" "$target"
bytes="$(wc -c < "$target" | tr -d ' ')"
hash="$(shasum -a 256 "$target" | awk '{print $1}')"
printf '{"database":"%s","backup":"%s","bytes":%s,"sha256":"%s","contains_env":false}\n' \
    "$database" "$target" "$bytes" "$hash"
