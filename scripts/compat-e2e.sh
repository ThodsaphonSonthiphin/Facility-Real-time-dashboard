#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SPECS=(overview.spec.ts my-work.spec.ts check-in.spec.ts login.spec.ts)

base_port="${E2E_PORT:-5101}"
offset=0
status=0
for browser in chromium webkit; do
  echo "=== Compatibility run: $browser ==="
  E2E_PORT=$((base_port + offset)) E2E_BROWSER="$browser" E2E_HEADLESS=1 E2E_SLOW_MO=0 bash "$ROOT/scripts/e2e.sh" run --dataset medium "${SPECS[@]}" || status=1
  offset=$((offset + 1))
done
exit "$status"
