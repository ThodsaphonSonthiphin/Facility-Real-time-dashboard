#!/usr/bin/env bash
# facility-0093: checks scripts/facility.sh with a fake dotnet that prints what it was given. Run: bash scripts/facility.test.sh
set -u

script="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/facility.sh"
repo_root="$(cd "$(dirname "$script")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/bin"
cat > "$work/bin/dotnet" <<'EOT'
#!/bin/sh
printf 'args=%s\n' "$*"
printf 'conn=%s|\n' "${ConnectionStrings__DefaultConnection:-}"
printf 'env=%s\n' "${ASPNETCORE_ENVIRONMENT:-}"
printf 'stale=%s\n' "${FirstAdmin__Password:-none}"
EOT
chmod 755 "$work/bin/dotnet"
export PATH="$work/bin:$PATH"

failures=0
check() { # name, expected exit code, text the output must contain, command...
  local name="$1" code="$2" expected="$3" output actual
  shift 3
  output="$("$@" 2>&1)"
  actual=$?
  if [ "$actual" -ne "$code" ] || [[ "$output" != *"$expected"* ]]; then
    echo "FAIL: $name (exit $actual, wanted $code)"; echo "$output" | sed 's/^/    /'
    failures=$((failures + 1))
  else
    echo "ok:   $name"
  fi
}

good="$work/good.env"
printf '%s\n' '# test settings' '' \
  'ConnectionStrings__DefaultConnection=Server=localhost;Port=3306;Database=facility_x_test;Uid=app;Pwd=pw' \
  'Jwt__SigningKey=0123456789abcdef0123456789abcdef' > "$good"
chmod 600 "$good"

check "no command shows the usage" 2 "usage:" bash "$script"
check "a missing env file is refused" 2 "not found" bash "$script" start "$work/none.env"
cp "$good" "$work/open.env"; chmod 644 "$work/open.env"
check "an env file others can read is refused" 2 "chmod 600" bash "$script" start "$work/open.env"
check "start keeps the ; in the connection string" 0 "conn=Server=localhost;Port=3306;Database=facility_x_test;Uid=app;Pwd=pw|" bash "$script" start "$good"
check "start runs the repo's API without its launch profile" 0 "args=run --no-launch-profile --project $repo_root/backend/src/FacilityRealtime.Api --" bash "$script" start "$good"
check "start is Production unless the file says otherwise" 0 "env=Production" bash "$script" start "$good"
check "a value left in the shell does not count" 0 "stale=none" env FirstAdmin__Password=left-over bash "$script" start "$good"
check "reset-admin passes the username" 0 "-- reset-admin first.admin" bash "$script" reset-admin "$good" first.admin
check "reset-admin needs a username" 2 "usage:" bash "$script" reset-admin "$good"

cp "$good" "$work/dll.env"; echo 'FACILITY_APP_DLL=/opt/facility/app/FacilityRealtime.Api.dll' >> "$work/dll.env"
check "a published dll runs instead of the repo" 0 "args=/opt/facility/app/FacilityRealtime.Api.dll reset-admin first.admin" bash "$script" reset-admin "$work/dll.env" first.admin

printf 'Jwt__SigningKey=0123456789abcdef0123456789abcdef\n' > "$work/noconn.env"; chmod 600 "$work/noconn.env"
check "an env file without the database is refused" 2 "has no ConnectionStrings__DefaultConnection" env ConnectionStrings__DefaultConnection=Server=x bash "$script" start "$work/noconn.env"
printf 'ConnectionStrings__DefaultConnection="Server=x"\nJwt__SigningKey=k\n' > "$work/quoted.env"; chmod 600 "$work/quoted.env"
check "a quoted value is refused" 2 "without quotes" bash "$script" start "$work/quoted.env"
printf 'ConnectionStrings__DefaultConnection=Server=x;Database=facility_x_test\r\nJwt__SigningKey=0123456789abcdef0123456789abcdef\r\n' > "$work/crlf.env"; chmod 600 "$work/crlf.env"
check "Windows line ends are dropped" 0 "conn=Server=x;Database=facility_x_test|" bash "$script" start "$work/crlf.env"

# init: server, port, database, user, password, dll, admin username, display name, admin password twice
answers=$'\n\nfacility_x_test\nfacility_app\ndb-pass-2026\n\nfirst.admin\nแอดมินคนแรก\nadmin-pass-2026-long\nadmin-pass-2026-long\n'
check "init writes the env file" 0 "chmod 600" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "$answers" "$script" "$work/new/pilot.env"
check "init's file is readable by its owner only" 0 "600" bash -c 'if [ "$(uname)" = Darwin ]; then stat -f %Lp "$1"; else stat -c %a "$1"; fi' _ "$work/new/pilot.env"
check "init's file starts the API" 0 "conn=Server=localhost;Port=3306;Database=facility_x_test;Uid=facility_app;Pwd=db-pass-2026;CharSet=utf8mb4;|" bash "$script" start "$work/new/pilot.env"
check "init never overwrites a file" 2 "already exists" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "$answers" "$script" "$work/new/pilot.env"
check "init refuses a ; in the database password" 2 "; ' or \"" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "${answers/db-pass-2026/db;pass}" "$script" "$work/semi.env"

if [ "$failures" -gt 0 ]; then echo "$failures failed"; exit 1; fi
echo "all passed"
