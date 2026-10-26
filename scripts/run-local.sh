#!/usr/bin/env bash
# One command: build the SPA, then serve UI and API from one loopback port.
set -eu
cd "$(dirname "$0")/.."
git check-ignore -q .env || { printf '%s\n' 'STOP: .env must be ignored.' >&2; exit 1; }
if git ls-files --error-unmatch .env >/dev/null 2>&1; then
    printf '%s\n' 'STOP: .env must be untracked.' >&2; exit 1
fi
set -a
source .env >/dev/null 2>&1
set +a
test -n "${POSTGRES_PASSWORD:-}" || { printf '%s\n' 'STOP: POSTGRES_PASSWORD absent.' >&2; exit 1; }
command -v npm >/dev/null 2>&1 || { printf '%s\n' 'STOP: npm is required to build the UI.' >&2; exit 1; }

npm --prefix frontend run build
url="http://127.0.0.1:${IDX_API_PORT:-5080}"
connection="${IDX_DATABASE_CONNECTION:-Host=127.0.0.1;Port=${POSTGRES_PORT:-5432};Database=${POSTGRES_DB:-idx_stock_intelligence};Username=${POSTGRES_USER:-idx_stock};Password=${POSTGRES_PASSWORD}}"
printf 'Serving UI and API at %s\n' "$url"
ASPNETCORE_URLS="$url" IDX_DATABASE_CONNECTION="$connection" exec dotnet run --project src/IdxStockIntelligence.Api -- "$@"
