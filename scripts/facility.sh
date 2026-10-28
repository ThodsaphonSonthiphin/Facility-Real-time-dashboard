#!/usr/bin/env bash
# facility-0093: starts the system or runs reset-admin with every setting from one env file, so nothing is typed
# with `export` and no setting can be missed. The file is KEY=VALUE per line, no quotes, the same as a systemd
# EnvironmentFile. It is read line by line, never sourced: the connection string holds ';'. See docs/handover.md.
#
#   scripts/facility.sh init <env-file>                    create the env file (asks; passwords hidden)
#   scripts/facility.sh start <env-file>                   run the system
#   scripts/facility.sh reset-admin <env-file> <username>  recover an Admin (facility-0092)
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
required="ConnectionStrings__DefaultConnection Jwt__SigningKey"

fail() { echo "facility.sh: $*" >&2; exit 2; }
usage() { fail "usage: scripts/facility.sh init <env-file> | start <env-file> | reset-admin <env-file> <username>"; }

file_mode() {
  if [ "$(uname)" = "Darwin" ]; then stat -f %Lp "$1"; else stat -c %a "$1"; fi
}

load_env() {
  local file="$1" line key value lineno=0 seen=" "
  [ -f "$file" ] || fail "$file not found. Create it with: scripts/facility.sh init $file"
  [ "$(file_mode "$file")" = "600" ] || fail "$file must be readable by its owner only. Run: chmod 600 $file"

  # Only the file's settings count: drop anything an earlier `export` left in this shell
  for key in $(compgen -e); do
    case "$key" in ConnectionStrings__*|Jwt__*|FirstAdmin__*|ASPNETCORE_*|FACILITY_*) unset "$key" ;; esac
  done

  while IFS= read -r line || [ -n "$line" ]; do
    lineno=$((lineno + 1))
    line="${line%$'\r'}"
    case "$line" in ''|'#'*) continue ;; esac
    key="${line%%=*}"
    value="${line#*=}"
    [ "$key" != "$line" ] || fail "$file line $lineno: expected KEY=VALUE"
    [[ "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || fail "$file line $lineno: '$key' is not a setting name"
    case "$value" in \"*|\'*) fail "$file line $lineno: write $key without quotes" ;; esac
    export "$key=$value"
    seen="$seen$key "
  done < "$file"

  for key in $required; do
    case "$seen" in *" $key "*) ;; *) fail "$file has no $key" ;; esac
  done
  export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
}

run_app() {
  if [ -n "${FACILITY_APP_DLL:-}" ]; then
    exec dotnet "$FACILITY_APP_DLL" "$@"
  fi
  exec dotnet run --no-launch-profile --project "$repo_root/backend/src/FacilityRealtime.Api" -- "$@"
}

ask() { # prompt, default
  local answer
  read -r -p "$1" answer || true
  echo "${answer:-${2:-}}"
}

ask_secret() { # prompt
  local answer
  read -r -s -p "$1" answer || true
  echo >&2
  echo "$answer"
}

init_env() {
  local file="$1" host port database user password dll admin name admin_password again
  [ ! -e "$file" ] || fail "$file already exists. Edit it, or choose another name"
  echo "รหัสผ่านพิมพ์แบบไม่แสดงบนจอ ตรวจว่าแป้นพิมพ์เป็นภาษาอังกฤษก่อนพิมพ์" >&2
  host="$(ask "MySQL server [localhost]: " localhost)"
  port="$(ask "MySQL port [3306]: " 3306)"
  database="$(ask "ชื่อฐานข้อมูล: ")"
  user="$(ask "ผู้ใช้ MySQL ของระบบ (ไม่ใช่ root): ")"
  password="$(ask_secret "รหัสผ่านของผู้ใช้นี้: ")"
  case "$password" in *\;*|*\'*|*\"*) fail "the MySQL password must not contain ; ' or \". Choose another one" ;; esac
  dll="$(ask "ที่อยู่ FacilityRealtime.Api.dll (เว้นว่าง = รันจากโค้ดใน repo นี้): ")"
  admin="$(ask "ชื่อผู้ใช้แอดมินคนแรก: ")"
  name="$(ask "ชื่อที่แสดงของแอดมินคนแรก: ")"
  admin_password="$(ask_secret "รหัสผ่านแอดมินคนแรก (15–100 ตัว): ")"
  again="$(ask_secret "พิมพ์รหัสผ่านแอดมินอีกครั้ง: ")"
  [ "$admin_password" = "$again" ] || fail "the two Admin passwords differ. Nothing was written"
  [ -n "$database" ] && [ -n "$user" ] && [ -n "$admin" ] && [ -n "$name" ] || fail "every question needs an answer. Nothing was written"

  mkdir -p "$(dirname "$file")"
  (
    umask 077
    {
      echo "# facility-0093: settings for scripts/facility.sh and systemd. Readable by its owner only (chmod 600)."
      echo "# Remove the FirstAdmin__Password line once that Admin has logged in (docs/handover.md)."
      echo "ASPNETCORE_ENVIRONMENT=Production"
      echo "ASPNETCORE_URLS=http://localhost:5001"
      echo "ConnectionStrings__DefaultConnection=Server=$host;Port=$port;Database=$database;Uid=$user;Pwd=$password;CharSet=utf8mb4;"
      echo "Jwt__SigningKey=$(openssl rand -base64 48)"
      echo "Jwt__RefreshCookieSecure=true"
      echo "FirstAdmin__Username=$admin"
      echo "FirstAdmin__DisplayName=$name"
      echo "FirstAdmin__Password=$admin_password"
      if [ -n "$dll" ]; then echo "FACILITY_APP_DLL=$dll"; fi
    } > "$file"
  )
  chmod 600 "$file"
  echo "wrote $file (chmod 600). Next: scripts/facility.sh start $file"
}

[ $# -ge 2 ] || usage
case "$1" in
  init) [ $# -eq 2 ] || usage; init_env "$2" ;;
  start) [ $# -eq 2 ] || usage; load_env "$2"; run_app ;;
  reset-admin) [ $# -eq 3 ] || usage; load_env "$2"; run_app reset-admin "$3" ;;
  *) usage ;;
esac
