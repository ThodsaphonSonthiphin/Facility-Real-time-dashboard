#!/usr/bin/env bash
# facility-0094: the end-to-end run. Builds the app, starts it in Development (the seed) on the throwaway database
# facility_e2e_test, runs Playwright, then stops the API, drops the database and deletes wwwroot, pass or fail.
# See docs/handover.md section 6.
#
#   scripts/e2e.sh run [playwright args]   the whole run (npm run e2e; extra args go to `playwright test`)
#   scripts/e2e.sh clean                    remove what a killed run left behind (npm run e2e:clean)
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
api_dir="$repo_root/backend/src/FacilityRealtime.Api"
run_dir="$repo_root/.run"
pid_file="$run_dir/e2e-api.pid"
log_file="$run_dir/e2e-api.log"
port="${E2E_PORT:-5101}"
# The only database this script creates or drops. Never facility_dashboard (the trial run of 2026-10-27)
db_name="facility_e2e_test"

fail() { echo "e2e.sh: $*" >&2; exit 2; }
usage() { fail "วิธีใช้: scripts/e2e.sh run [ค่าที่ส่งต่อให้ playwright test] | clean"; }

sql() {
  if [ -n "${E2E_MYSQL_PASSWORD:-}" ]; then
    MYSQL_PWD="$E2E_MYSQL_PASSWORD" mysql -u root --batch --skip-column-names -e "$1"
  else
    mysql -u root --batch --skip-column-names -e "$1"
  fi
}

stop_api() {
  [ -f "$pid_file" ] || return 0
  local pid
  pid="$(cat "$pid_file")"
  kill "$pid" 2>/dev/null || true
  # Up to 10 s, so the next run finds the port free
  for _ in $(seq 1 20); do
    kill -0 "$pid" 2>/dev/null || break
    sleep 0.5
  done
  rm -f "$pid_file"
}

clean() {
  stop_api
  sql "DROP DATABASE IF EXISTS \`$db_name\`" ||
    echo "e2e.sh: ลบฐานข้อมูล $db_name ไม่ได้ ลบเองด้วย: mysql -u root -e 'DROP DATABASE $db_name'" >&2
  rm -rf "$api_dir/wwwroot"
}

wait_for_api() {
  for _ in $(seq 1 120); do
    curl -fs "http://localhost:$port/api/health" >/dev/null 2>&1 && return 0
    if ! kill -0 "$(cat "$pid_file")" 2>/dev/null; then
      tail -n 40 "$log_file" >&2
      fail "API หยุดก่อนพร้อม ดู log ทั้งหมดที่ $log_file"
    fi
    sleep 0.5
  done
  tail -n 40 "$log_file" >&2
  fail "API ไม่ตอบภายใน 60 วินาที ดู log ที่ $log_file"
}

run() {
  command -v mysql >/dev/null || fail "ไม่พบคำสั่ง mysql ติดตั้ง MySQL ก่อน (Mac: brew install mysql แล้ว brew services start mysql)"
  sql "SELECT 1" >/dev/null || fail "ต่อ MySQL ด้วย root ไม่ได้ ถ้า root มีรหัสผ่าน ตั้ง E2E_MYSQL_PASSWORD ก่อนรัน"
  if curl -fs "http://localhost:$port/api/health" >/dev/null 2>&1; then
    fail "มีระบบตอบอยู่ที่พอร์ต $port แล้ว หยุดตัวนั้นก่อน หรือเลือกพอร์ตอื่นด้วย E2E_PORT"
  fi

  mkdir -p "$run_dir"
  trap clean EXIT
  clean # what a killed run may have left

  (cd "$repo_root/frontend" && npm run build:api)
  dotnet build "$api_dir" -c Release --nologo -v quiet
  sql "CREATE DATABASE \`$db_name\` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci"

  local signing_key
  signing_key="$(openssl rand -base64 48)"
  (
    # ASP.NET Core takes its content root (wwwroot) from the working directory
    cd "$api_dir"
    export ASPNETCORE_ENVIRONMENT=Development
    export ASPNETCORE_URLS="http://localhost:$port"
    export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=$db_name;Uid=root;Pwd=${E2E_MYSQL_PASSWORD:-};CharSet=utf8mb4;"
    export Jwt__SigningKey="$signing_key"
    exec dotnet bin/Release/net10.0/FacilityRealtime.Api.dll
  ) >"$log_file" 2>&1 &
  echo $! >"$pid_file"
  wait_for_api

  cd "$repo_root/frontend"
  npx playwright install chromium
  # ${@+"$@"}: bash 3.2 with `set -u` treats a bare "$@" with no arguments as unbound
  E2E_BASE_URL="http://localhost:$port" npx playwright test ${@+"$@"}
}

case "${1:-}" in
  run) shift; run ${@+"$@"} ;;
  clean) clean ;;
  *) usage ;;
esac
