#!/bin/sh
# Render an installable DeepSeek Harness bundle that exposes scripts/ai-infra_mcp.py.
#
#   LAI_REPO=/abs/path/to/ai-local-infra scripts/install-ai-infra-mcp-bundle.sh [target]
#
# Default target: $DSH_HOME/local-bundles/ai-infra-mcp (DSH_HOME defaults to ~/.dsh).
# The rendered bundle contains absolute paths on purpose; the committed template in
# tools/dsh-mcp-ai-infra/ stays path-free. Installing it into a DSH profile is a
# separate, explicit step:
#
#   dsh plugin --profile desktop add <target>
#
# Nothing here writes to the sibling repository.
set -eu

[ $# -le 1 ] || { echo "usage: install-ai-infra-mcp-bundle.sh [target]" >&2; exit 64; }

repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd -P)
server="$repo_root/scripts/ai_infra_mcp.py"

[ -n "${LAI_REPO:-}" ] || { echo "LAI_REPO is unset; point it at the absolute local-ai-infra path" >&2; exit 2; }
case "$LAI_REPO" in /*) ;; *) echo "LAI_REPO must be absolute: $LAI_REPO" >&2; exit 2 ;; esac
[ -d "$LAI_REPO" ] || { echo "LAI_REPO is not a directory: $LAI_REPO" >&2; exit 2; }
[ -x "$LAI_REPO/scripts/orchestrate" ] || { echo "not a local-ai-infra checkout: $LAI_REPO" >&2; exit 2; }
[ -f "$server" ] || { echo "missing server: $server" >&2; exit 2; }
command -v python3 >/dev/null 2>&1 || { echo "python3 is required" >&2; exit 2; }

target=${1:-"${DSH_HOME:-$HOME/.dsh}/local-bundles/ai-infra-mcp"}
mkdir -p "$target"

python3 - "$repo_root" "$LAI_REPO" "$target" "$server" <<'PY'
import json
import pathlib
import sys

repo_root, lai_repo, target, server = sys.argv[1:5]
target_path = pathlib.Path(target)

manifest = {
    "name": "@local/dsh-ai-infra-mcp",
    "version": "0.1.0",
    "private": True,
    "type": "module",
    "description": "Read-only MCP bridge from DeepSeek Harness to the sibling local-ai-infra CLI.",
    "files": ["cordis.patch.yml"],
    "dsh": {"bundle": {"patch": "./cordis.patch.yml"}},
}
(target_path / "package.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

patch = f"""# Rendered by scripts/install-ai-infra-mcp-bundle.sh from {repo_root}
# Absolute paths are intentional here; the committed template stays path-free.
- insert:
    - id: ai-infra-mcp
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: ai-infra
        transport: stdio
        command: 'python3'
        args: ['{server}']
        env:
          LAI_REPO: '{lai_repo}'
        failOnStartupError: false
        toolCallTimeoutMs: 60000
"""
(target_path / "cordis.patch.yml").write_text(patch, encoding="utf-8")
print(f"bundle: {target_path}")
print(f"server: {server}")
print(f"lai_repo: {lai_repo}")
PY

cat <<EOF

Next, install it into a profile (explicit step):
  dsh plugin --profile desktop add "$target"
Then restart the desktop app and check that mcp__ai-infra__ai_infra_check appears.
EOF
