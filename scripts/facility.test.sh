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
printf 'pwd=%s\n' "$(pwd -P)"
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

# Made-up passwords, made at run time so no password-like text sits in the repo (secret scanners flag it).
db_pw="$(printf 'db-%05d%05d' "$RANDOM" "$RANDOM")"
nl=$'\n'
admin_pw="$(printf 'admin-%05d-%05d-%05d' "$RANDOM" "$RANDOM" "$RANDOM")"

good="$work/good.env"
printf '%s\n' '# test settings' '' \
  "ConnectionStrings__DefaultConnection=Server=localhost;Port=3306;Database=facility_x_test;Uid=app;Pwd=$db_pw" \
  'Jwt__SigningKey=0123456789abcdef0123456789abcdef' > "$good"
chmod 600 "$good"

check "no command shows the usage" 2 "วิธีใช้:" bash "$script"
check "a missing env file is refused" 2 "ไม่พบ" bash "$script" start "$work/none.env"
cp "$good" "$work/open.env"; chmod 644 "$work/open.env"
check "an env file others can read is refused" 2 "chmod 600" bash "$script" start "$work/open.env"
check "start keeps the ; in the connection string" 0 "conn=Server=localhost;Port=3306;Database=facility_x_test;Uid=app;Pwd=$db_pw|" bash "$script" start "$good"
check "start runs the repo's API without its launch profile" 0 "args=run --no-launch-profile --project $repo_root/backend/src/FacilityRealtime.Api --" bash "$script" start "$good"
check "start is Production unless the file says otherwise" 0 "env=Production" bash "$script" start "$good"
check "a value left in the shell does not count" 0 "stale=none" env FirstAdmin__Password="$admin_pw" bash "$script" start "$good"
check "reset-admin passes the username" 0 "-- reset-admin first.admin" bash "$script" reset-admin "$good" first.admin
check "reset-admin needs a username" 2 "วิธีใช้:" bash "$script" reset-admin "$good"

mkdir -p "$work/app" "$work/elsewhere"; : > "$work/app/FacilityRealtime.Api.dll"
cp "$good" "$work/dll.env"; echo "FACILITY_APP_DLL=$work/app/FacilityRealtime.Api.dll" >> "$work/dll.env"
check "a published dll runs instead of the repo" 0 "args=$work/app/FacilityRealtime.Api.dll reset-admin first.admin" bash "$script" reset-admin "$work/dll.env" first.admin
check "a published dll runs from its own folder" 0 "pwd=$(cd "$work/app" && pwd -P)" bash -c 'cd "$1" && bash "$2" start "$3"' _ "$work/elsewhere" "$script" "$work/dll.env"

printf 'Jwt__SigningKey=0123456789abcdef0123456789abcdef\n' > "$work/noconn.env"; chmod 600 "$work/noconn.env"
check "an env file without the database is refused" 2 "ไม่มีค่า ConnectionStrings__DefaultConnection" env ConnectionStrings__DefaultConnection=Server=x bash "$script" start "$work/noconn.env"
printf 'ConnectionStrings__DefaultConnection="Server=x"\nJwt__SigningKey=k\n' > "$work/quoted.env"; chmod 600 "$work/quoted.env"
check "a quoted value is refused" 2 "ไม่มีเครื่องหมายคำพูด" bash "$script" start "$work/quoted.env"
printf 'ConnectionStrings__DefaultConnection=Server=x;Database=facility_x_test\r\nJwt__SigningKey=0123456789abcdef0123456789abcdef\r\n' > "$work/crlf.env"; chmod 600 "$work/crlf.env"
check "Windows line ends are dropped" 0 "conn=Server=x;Database=facility_x_test|" bash "$script" start "$work/crlf.env"

# init: server, port, database, user, password, dll, admin username, display name, admin password twice
answers=$'\n\nfacility_x_test\nfacility_app\n'"$db_pw"$'\n\nfirst.admin\nแอดมินคนแรก\n'"$admin_pw"$'\n'"$admin_pw"$'\n'
check "init writes the env file" 0 "chmod 600" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "$answers" "$script" "$work/new/pilot.env"
check "init's file is readable by its owner only" 0 "600" bash -c 'if [ "$(uname)" = Darwin ]; then stat -f %Lp "$1"; else stat -c %a "$1"; fi' _ "$work/new/pilot.env"
check "init's file starts the API" 0 "conn=Server=localhost;Port=3306;Database=facility_x_test;Uid=facility_app;Pwd=$db_pw;CharSet=utf8mb4;|" bash "$script" start "$work/new/pilot.env"
check "init never overwrites a file" 2 "มีอยู่แล้ว" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "$answers" "$script" "$work/new/pilot.env"
check "init refuses a ; in the database password" 2 "ห้ามมี ;" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "${answers/$db_pw/db;pass}" "$script" "$work/semi.env"

check "a stale ASPNETCORE_ENVIRONMENT does not count" 0 "env=Production" env ASPNETCORE_ENVIRONMENT=Development bash "$script" start "$good"
printf 'ConnectionStrings__DefaultConnection=Server=x;Database=d\nJwt__SigningKey=\n' > "$work/emptykey.env"; chmod 600 "$work/emptykey.env"
check "an empty required value is refused" 2 "ไม่มีค่า Jwt__SigningKey" bash "$script" start "$work/emptykey.env"
ln -s "$good" "$work/link.env"
check "a symlink to a 600 env file works" 0 "env=Production" bash "$script" start "$work/link.env"

run_init() { # answers, file, then optional extra env (PATH=...)
  printf '%s' "$1" | bash "$script" init "$2"
}
nofile() { # name, directory that must hold nothing
  if [ -z "$(ls -A "$2" 2>/dev/null)" ]; then echo "ok:   $1"; else
    echo "FAIL: $1"; ls -A "$2" | sed 's/^/    /'; failures=$((failures + 1)); fi
}
out="$(run_init "$answers" "$work/out/o.env" 2>&1)"
if [[ "$out" != *"$db_pw"* && "$out" != *"$admin_pw"* ]]; then echo "ok:   init's console output shows no password"
else echo "FAIL: init's console output shows a password"; failures=$((failures + 1)); fi
key="$(sed -n 's/^Jwt__SigningKey=//p' "$work/out/o.env")"
if [ "${#key}" -ge 32 ] && grep -q '^FirstAdmin__Username=first.admin$' "$work/out/o.env" && grep -q '^FirstAdmin__DisplayName=แอดมินคนแรก$' "$work/out/o.env" && grep -qx "FirstAdmin__Password=$admin_pw" "$work/out/o.env"; then
  echo "ok:   init's file has a signing key and the three FirstAdmin lines"
else echo "FAIL: init's file lacks a signing key or a FirstAdmin line"; failures=$((failures + 1)); fi

mkdir -p "$work/badbin"; printf '#!/bin/sh\nexit 1\n' > "$work/badbin/openssl"; chmod 755 "$work/badbin/openssl"
check "init without a signing key is refused" 2 "สร้าง Jwt__SigningKey ไม่ได้" bash -c 'printf "%s" "$1" | PATH="$4:$PATH" bash "$2" init "$3"' _ "$answers" "$script" "$work/nokey/k.env" "$work/badbin"
nofile "init without a signing key leaves nothing" "$work/nokey"
check "two different admin passwords are refused" 2 "ไม่ตรงกัน" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "${answers/$admin_pw$nl$admin_pw/$admin_pw${nl}other-$admin_pw}" "$script" "$work/differ/d.env"
nofile "different passwords leave nothing" "$work/differ"
check "a display name starting with a quote is refused" 2 "ห้ามมีเครื่องหมายคำพูด" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "${answers/แอดมินคนแรก/\"x}" "$script" "$work/quote/q.env"
nofile "a refused display name leaves nothing" "$work/quote"
check "an empty answer is refused" 2 "ต้องตอบทุกข้อ" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "${answers/facility_app/}" "$script" "$work/empty/e.env"
nofile "an empty answer leaves nothing" "$work/empty"
check "a MySQL password of -n is written as typed" 0 "Pwd=-n;" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\" >/dev/null && cat \"\$3\"" _ "${answers/$db_pw/-n}" "$script" "$work/dash/n.env"

check "a quote in the middle of an answer is refused" 2 "ห้ามมีเครื่องหมายคำพูด" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "${answers/แอดมินคนแรก/Bob\'s team}" "$script" "$work/mid/m.env"
nofile "a quote in the middle leaves nothing" "$work/mid"
check "a backslash in the middle of an answer is refused" 2 "ห้ามมีเครื่องหมายคำพูดหรือ" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "${answers//$admin_pw/admin\\$admin_pw}" "$script" "$work/bs/b.env"
nofile "a backslash leaves nothing" "$work/bs"
printf "ConnectionStrings__DefaultConnection=Server=x\nJwt__SigningKey=ab'cd\n" > "$work/midq.env"; chmod 600 "$work/midq.env"
check "a quote in the middle of a value in the env file is refused" 2 "ไม่มีเครื่องหมายคำพูด" bash "$script" start "$work/midq.env"

check "a relative dll path is refused by init" 2 "path เต็ม" bash -c "printf '%s' \"\$1\" | bash \"\$2\" init \"\$3\"" _ "${answers/$'\n\nfirst.admin'/$'\nrelative/App.dll\nfirst.admin'}" "$script" "$work/reldll/r.env"
nofile "a relative dll path leaves nothing" "$work/reldll"
printf 'ConnectionStrings__DefaultConnection=Server=x\nJwt__SigningKey=0123456789abcdef0123456789abcdef \n' > "$work/trail.env"; chmod 600 "$work/trail.env"
check "a value with a space at the end is refused" 2 "ค่า Jwt__SigningKey ห้ามมีช่องว่างหน้าหรือท้าย" bash "$script" start "$work/trail.env"
printf 'ConnectionStrings__DefaultConnection=Server=x\nJwt__SigningKey=0123456789abcdef0123456789abcdef\nJwt__SigningKey=\n' > "$work/repeat.env"; chmod 600 "$work/repeat.env"
check "a repeated key with an empty last value is refused" 2 "ไม่มีค่า Jwt__SigningKey" bash "$script" start "$work/repeat.env"
printf 'my pass=word\nJwt__SigningKey=k\n' > "$work/badname.env"; chmod 600 "$work/badname.env"
out="$(bash "$script" start "$work/badname.env" 2>&1)"; code=$?
if [ "$code" -eq 2 ] && [[ "$out" == *"ชื่อค่าไม่ถูกต้อง"* && "$out" != *pass* ]]; then echo "ok:   a bad name is refused without echoing it"
else echo "FAIL: a bad name is refused without echoing it (exit $code)"; echo "$out" | sed 's/^/    /'; failures=$((failures + 1)); fi

if [ "$failures" -gt 0 ]; then echo "$failures failed"; exit 1; fi
echo "all passed"
