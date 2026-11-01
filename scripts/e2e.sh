#!/usr/bin/env bash
# Compatible entry point; Python standard library owns the isolated run lifecycle.
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec python3 "$repo_root/scripts/e2e.py" "$@"
