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
usage() { fail "วิธีใช้: scripts/facility.sh init <ไฟล์ค่า> | start <ไฟล์ค่า> | reset-admin <ไฟล์ค่า> <ชื่อผู้ใช้>"; }

file_mode() {
  if [ "$(uname)" = "Darwin" ]; then stat -L -f %Lp "$1"; else stat -L -c %a "$1"; fi
}

load_env() {
  local file="$1" line key value lineno=0
  [ -f "$file" ] || fail "ไม่พบ $file สร้างด้วย: scripts/facility.sh init $file"
  [ "$(file_mode "$file")" = "600" ] || fail "$file ต้องอ่านได้เฉพาะเจ้าของ รัน: chmod 600 $file"

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
    [ "$key" != "$line" ] || fail "$file บรรทัด $lineno: ต้องเขียนเป็น ชื่อ=ค่า"
    [[ "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || fail "$file บรรทัด $lineno: ชื่อค่าไม่ถูกต้อง"
    case "$value" in *\"*|*\'*|*\\*) fail "$file บรรทัด $lineno: เขียน $key โดยไม่มีเครื่องหมายคำพูดและไม่มี \\" ;; esac
    case "$value" in [[:space:]]*|*[[:space:]]) fail "$file บรรทัด $lineno: ค่า $key ห้ามมีช่องว่างหน้าหรือท้าย" ;; esac
    export "$key=$value"
  done < "$file"

  for key in $required; do
    [ -n "${!key:-}" ] || fail "$file ไม่มีค่า $key"
  done
  export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
}

run_app() {
  if [ -n "${FACILITY_APP_DLL:-}" ]; then
    # ASP.NET Core takes its content root (the web pages) from the working directory
    cd "$(dirname "$FACILITY_APP_DLL")"
    exec dotnet "$FACILITY_APP_DLL" "$@"
  fi
  exec dotnet run --no-launch-profile --project "$repo_root/backend/src/FacilityRealtime.Api" -- "$@"
}

ask() { # prompt, default
  local answer
  IFS= read -r -p "$1" answer || true
  printf '%s\n' "${answer:-${2:-}}"
}

ask_secret() { # prompt
  local answer
  IFS= read -r -s -p "$1" answer || true
  echo >&2
  printf '%s\n' "$answer"
}

# Every value init writes must read the same to start and to systemd's EnvironmentFile.
check_value() { # label, value, "conn" if it goes into the connection string
  local label="$1" value="$2"
  [ -n "$value" ] || fail "ต้องตอบทุกข้อ ไม่ได้เขียนอะไร"
  case "$value" in
    *\"*|*\'*|*\\*|[[:space:]]*|*[[:space:]]) fail "$label ห้ามมีเครื่องหมายคำพูดหรือ \\ และห้ามมีช่องว่างหน้าหรือท้าย ไม่ได้เขียนอะไร" ;;
  esac
  if [ "${3:-}" = "conn" ]; then
    case "$value" in *\;*) fail "$label ห้ามมี ; ไม่ได้เขียนอะไร" ;; esac
  fi
  return 0
}

init_env() {
  local file="$1" host port database user password dll admin name admin_password again key
  [ ! -e "$file" ] || fail "$file มีอยู่แล้ว แก้ไฟล์นั้น หรือใช้ชื่ออื่น"
  echo "รหัสผ่านพิมพ์แบบไม่แสดงบนจอ ตรวจว่าแป้นพิมพ์เป็นภาษาอังกฤษก่อนพิมพ์" >&2
  host="$(ask "MySQL server [localhost]: " localhost)"; check_value "MySQL server" "$host" conn
  port="$(ask "MySQL port [3306]: " 3306)"; check_value "MySQL port" "$port" conn
  database="$(ask "ชื่อฐานข้อมูล: ")"; check_value "ชื่อฐานข้อมูล" "$database" conn
  user="$(ask "ผู้ใช้ MySQL ของระบบ (ไม่ใช่ root): ")"; check_value "ผู้ใช้ MySQL" "$user" conn
  password="$(ask_secret "รหัสผ่านของผู้ใช้นี้: ")"; check_value "รหัสผ่าน MySQL" "$password" conn
  dll="$(ask "ที่อยู่ FacilityRealtime.Api.dll (เว้นว่าง = รันจากโค้ดใน repo นี้): ")"
  if [ -n "$dll" ]; then
    check_value "ที่อยู่ dll" "$dll"
    case "$dll" in /*) [ -f "$dll" ] ;; *) false ;; esac || fail "ที่อยู่ dll ต้องเป็น path เต็มของไฟล์ที่มีอยู่ ไม่ได้เขียนอะไร"
  fi
  admin="$(ask "ชื่อผู้ใช้แอดมินคนแรก: ")"; check_value "ชื่อผู้ใช้แอดมิน" "$admin"
  name="$(ask "ชื่อที่แสดงของแอดมินคนแรก: ")"; check_value "ชื่อที่แสดง" "$name"
  admin_password="$(ask_secret "รหัสผ่านแอดมินคนแรก (15–100 ตัว): ")"; check_value "รหัสผ่านแอดมิน" "$admin_password"
  again="$(ask_secret "พิมพ์รหัสผ่านแอดมินอีกครั้ง: ")"
  [ "$admin_password" = "$again" ] || fail "รหัสผ่านแอดมินสองครั้งไม่ตรงกัน ไม่ได้เขียนอะไร"
  key="$(openssl rand -base64 48)" || fail "สร้าง Jwt__SigningKey ไม่ได้ (ต้องมี openssl) ไม่ได้เขียนอะไร"
  [ -n "$key" ] || fail "สร้าง Jwt__SigningKey ไม่ได้ (ต้องมี openssl) ไม่ได้เขียนอะไร"

  mkdir -p "$(dirname "$file")"
  tmp="$(umask 077; mktemp "$(dirname "$file")/.facility-env.XXXXXX")"
  trap 'rm -f "$tmp"' EXIT
  {
    echo "# facility-0093: settings for scripts/facility.sh and systemd. Readable by its owner only (chmod 600)."
    echo "# Remove the FirstAdmin__Password line once that Admin has logged in (docs/handover.md)."
    echo "ASPNETCORE_ENVIRONMENT=Production"
    echo "ASPNETCORE_URLS=http://localhost:5001"
    echo "ConnectionStrings__DefaultConnection=Server=$host;Port=$port;Database=$database;Uid=$user;Pwd=$password;CharSet=utf8mb4;"
    echo "Jwt__SigningKey=$key"
    echo "Jwt__RefreshCookieSecure=true"
    echo "FirstAdmin__Username=$admin"
    echo "FirstAdmin__DisplayName=$name"
    printf 'FirstAdmin__Password=%s\n' "$admin_password"
    if [ -n "$dll" ]; then echo "FACILITY_APP_DLL=$dll"; fi
  } > "$tmp"
  chmod 600 "$tmp"
  [ ! -e "$file" ] || fail "$file มีอยู่แล้ว แก้ไฟล์นั้น หรือใช้ชื่ออื่น"
  mv "$tmp" "$file"
  echo "เขียน $file แล้ว (chmod 600) ต่อไป: scripts/facility.sh start $file"
}

[ $# -ge 2 ] || usage
case "$1" in
  init) [ $# -eq 2 ] || usage; init_env "$2" ;;
  start) [ $# -eq 2 ] || usage; load_env "$2"; run_app ;;
  reset-admin) [ $# -eq 3 ] || usage; load_env "$2"; run_app reset-admin "$3" ;;
  *) usage ;;
esac
