#!/usr/bin/env bash
set -eu
exec bash "$(dirname "$0")/pilot-eod.sh" --daily "$@"
