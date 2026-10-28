# Safe Start, `reset-admin` Confirmation, Start Script and Deploy Guide: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A missing database setting stops the server instead of falling back to `facility_dashboard`. `reset-admin` names the database it is about to change and waits for `yes`. One script starts the system or runs `reset-admin` with every setting taken from one env file. The Thai deploy guide in `docs/handover.md` covers a Linux server.

**Architecture:**
- `Api/DatabaseSettings` is the one place that reads the connection string. It throws when the setting is missing, and it describes the target as `server:port / database`.
- `ResetAdminCommand.ConfirmDatabase` asks for `yes` before `Program.cs` migrates anything.
- `scripts/facility.sh` (bash, macOS and Linux) loads a `chmod 600` env file line by line and runs the API with those settings only.
- The guide documents both the script and a systemd unit that reads the same file.

**Tech Stack:** .NET 10 Minimal API, xUnit, bash 3.2+ (macOS) / bash 5 (Linux), `openssl`.

**Spec:** `docs/adr/facility-0093-safe-start-and-deploy-guide.md` (Thai). Related: facility-0011, 0049, 0092.

## Global Constraints

- Outside Development, a missing or empty `ConnectionStrings:DefaultConnection` stops startup and stops `reset-admin`. No code path falls back to `facility_dashboard`.
- The developer default `Server=localhost;Port=3306;Database=facility_dashboard;Uid=root;Pwd=;CharSet=utf8mb4;` lives only in `appsettings.Development.json`.
- `reset-admin` asks for `yes` **before** `MigrateAsync`. Any other answer, or end of input, exits with code 1 and touches nothing.
- The description of a database never contains its user name or password (`Uid`, `Pwd`).
- No password ever appears in a log line, an exception message or console output (facility-0057).
- The env file format is `KEY=VALUE`, one per line, no quotes, with `#` comments and blank lines allowed. It is the same as a systemd `EnvironmentFile`. The script never `source`s it.
- The script refuses an env file whose mode is not `600`.
- Before loading the file, the script unsets `ConnectionStrings__*`, `Jwt__*`, `FirstAdmin__*`, `ASPNETCORE_*` and `FACILITY_*`.
- The script must run on macOS bash 3.2: no associative arrays, no `mapfile`, no `${var,,}`.
- The deploy guide and every console text a person reads are in Thai. Startup exceptions are in English, like `AuthSetup`. Thai strings in this plan are copied verbatim.
- MySQL: only throwaway databases named `facility_*_test`; never `facility_dashboard`. Steps that need MySQL are run by the controller, never by a subagent. The owner types the MySQL root password; it is never written to a file.
- Backend tests: `dotnet test backend/FacilityRealtime.slnx`. Every commit passes the husky pre-commit.
- Never push, open a PR or merge without the owner's explicit OK.

## ADR decision → task

| ADR facility-0093 | Task |
|---|---|
| 1. Default connection string only in Development; elsewhere a missing one stops startup and `reset-admin` | 1 |
| 2. `reset-admin` prints server, port and database (no user or password), waits for `yes`, before migrate | 2 |
| 3. One script, env file `KEY=VALUE` with no quotes, read line by line, `chmod 600`, stale shell values cleared, Production by default, `init`, dedicated MySQL user | 3 (script), 4 (dedicated user in the guide) |
| 4. Deploy guide for Linux in `docs/handover.md`; Windows still open | 4 |
| 5. Thai-keyboard warning wherever a password is typed hidden | 3 (`init` prompt), 4 (guide) |
| Consequences: piped `reset-admin` sends `yes` first; `facility.test.sh` outside pre-commit | 2, 3, 5 |

## Decisions made while planning (the owner can overrule them)

1. **https through Caddy** in the guide. It gets and renews the certificate by itself, and its config is 3 lines. nginx + certbot is the usual alternative.
2. **A daily `mysqldump` backup** is in the guide. The facility-0092 guide already says "restore from backup", and nobody has written how to make one. 14 days are kept.
3. **`yes` is accepted in any case, with spaces around it** (`YES`, ` yes `). Anything else refuses.
4. **The env file may set `FACILITY_APP_DLL`.** On a server, the script then runs the published `FacilityRealtime.Api.dll`. Without it (the Mac trial), the script runs the API from the repo with `dotnet run --no-launch-profile`.
5. **`init` refuses `;`, `'` and `"` in the MySQL password.** Each one would break the connection string or the env file. Choose another password.

## File Structure

- `backend/src/FacilityRealtime.Api/DatabaseSettings.cs` (new): `ConnectionString(IConfiguration)` and `Describe(string)`.
- `backend/src/FacilityRealtime.Api/appsettings.json`, `appsettings.Development.json`: the developer connection string moves.
- `backend/src/FacilityRealtime.Api/Program.cs`: uses `DatabaseSettings`, and the `reset-admin` path checks usage and confirms before migrating.
- `backend/src/FacilityRealtime.Api/Commands/ResetAdminCommand.cs`: `Usage` constant and `ConfirmDatabase`.
- `backend/tests/FacilityRealtime.ApiTests/Setup/DatabaseSettingsTests.cs` (new).
- `backend/tests/FacilityRealtime.ApiTests/Admin/ResetAdminCommandTests.cs`: tests for `ConfirmDatabase`.
- `scripts/facility.sh` (new), `scripts/facility.test.sh` (new).
- `docs/handover.md`: the deploy guide.
- `docs/handoff.md`: `## Sharing a test build` points at the script.

---

### Task 1: The connection string is required outside Development

**Files:**
- Create: `backend/src/FacilityRealtime.Api/DatabaseSettings.cs`
- Modify: `backend/src/FacilityRealtime.Api/appsettings.json` (remove the `ConnectionStrings` block)
- Modify: `backend/src/FacilityRealtime.Api/appsettings.Development.json` (add it)
- Modify: `backend/src/FacilityRealtime.Api/Program.cs:22,46,143-146`
- Test: `backend/tests/FacilityRealtime.ApiTests/Setup/DatabaseSettingsTests.cs`

**Interfaces:**
- Produces: `public static class DatabaseSettings` in namespace `FacilityRealtime.Api`, with
  - `public const string VariableName = "ConnectionStrings__DefaultConnection";`
  - `public static string ConnectionString(IConfiguration configuration)`: throws `InvalidOperationException` when the value is missing or empty.
  - `public static string Describe(string connectionString)`: returns `"<server>:<port> / <database>"`.

- [ ] **Step 1: Write the failing tests**

```csharp
using FacilityRealtime.Api;
using Microsoft.Extensions.Configuration;

namespace FacilityRealtime.ApiTests.Setup;

/// <summary>facility-0093 decision 1: only Development has a default database; elsewhere a missing one stops the server.</summary>
public class DatabaseSettingsTests
{
    private static IConfiguration Config(params string[] jsonFiles)
    {
        var builder = new ConfigurationBuilder();
        foreach (var file in jsonFiles)
        {
            builder.AddJsonFile(Path.Combine(AppContext.BaseDirectory, file), optional: false);
        }

        return builder.Build();
    }

    private static IConfiguration Config(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString })
            .Build();

    [Fact]
    public void Returns_the_connection_string_that_is_set()
    {
        Assert.Equal("Server=db;Database=facility_pilot", DatabaseSettings.ConnectionString(Config("Server=db;Database=facility_pilot")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_connection_string_stops_with_the_setting_name(string? value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseSettings.ConnectionString(Config(value)));

        Assert.Contains("ConnectionStrings__DefaultConnection", error.Message);
    }

    [Fact]
    public void Production_settings_alone_have_no_database()
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseSettings.ConnectionString(Config("appsettings.json")));
    }

    [Fact]
    public void Development_settings_keep_the_developer_database()
    {
        var value = DatabaseSettings.ConnectionString(Config("appsettings.json", "appsettings.Development.json"));

        Assert.Contains("Database=facility_dashboard", value);
    }

    [Theory]
    [InlineData("Server=db.example;Port=3307;Database=facility_pilot;Uid=app;Pwd=s3cret-pass", "db.example:3307 / facility_pilot")]
    [InlineData("Host=10.0.0.5;Initial Catalog=facility_pilot;User Id=app;Password=s3cret-pass", "10.0.0.5:3306 / facility_pilot")]
    [InlineData("Uid=app;Pwd=s3cret-pass", "(ไม่ได้ระบุ):3306 / (ไม่ได้ระบุ)")]
    public void Describes_server_port_and_database_without_the_login(string connectionString, string expected)
    {
        var described = DatabaseSettings.Describe(connectionString);

        Assert.Equal(expected, described);
        Assert.DoesNotContain("s3cret-pass", described);
        Assert.DoesNotContain("app", described.Replace("(ไม่ได้ระบุ)", ""));
    }
}
```


- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~DatabaseSettingsTests"`
Expected: build FAIL with "The name 'DatabaseSettings' does not exist".

- [ ] **Step 3: Write `DatabaseSettings`**

```csharp
using System.Data.Common;

namespace FacilityRealtime.Api;

/// <summary>
/// facility-0093 decision 1: the one place that reads the database setting, for serving and for reset-admin. Only
/// appsettings.Development.json has a default, so a server that forgot the setting stops instead of changing
/// facility_dashboard.
/// </summary>
public static class DatabaseSettings
{
    public const string VariableName = "ConnectionStrings__DefaultConnection";

    private const string NotGiven = "(ไม่ได้ระบุ)";

    public static string ConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("DefaultConnection") is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"{VariableName} is not set. Set it on the server, or run with scripts/facility.sh and its env file (docs/handover.md). Only Development has a default database.");

    /// <summary>"server:port / database" for a person to check; never the user name or password.</summary>
    public static string Describe(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var server = First(builder, "Server", "Host", "Data Source") ?? NotGiven;
        var port = First(builder, "Port") ?? "3306";
        var database = First(builder, "Database", "Initial Catalog") ?? NotGiven;
        return $"{server}:{port} / {database}";
    }

    private static string? First(DbConnectionStringBuilder builder, params string[] keys) =>
        keys.Select(key => builder.TryGetValue(key, out var value) ? value as string : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
```

- [ ] **Step 4: Move the developer connection string**

In `appsettings.json`, delete the whole block:

```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=facility_dashboard;Uid=root;Pwd=;CharSet=utf8mb4;"
  },
```

`appsettings.Development.json` becomes:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=facility_dashboard;Uid=root;Pwd=;CharSet=utf8mb4;"
  }
}
```

- [ ] **Step 5: Use it in `Program.cs`**

Replace `ConnectionString(builder.Configuration)` on lines 22 and 46 with `DatabaseSettings.ConnectionString(builder.Configuration)`. Delete lines 143–146 (the comment `// One database for serving and for reset-admin (facility-0092)` and the local `ConnectionString` function). Add `using FacilityRealtime.Api;` to `Program.cs`'s usings: its top-level statements are in the global namespace and do not see `FacilityRealtime.Api` otherwise.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS, with no other test broken. The Testing environment never calls `DatabaseSettings`.

- [ ] **Step 7: Commit**

```bash
git add backend/src/FacilityRealtime.Api/DatabaseSettings.cs backend/src/FacilityRealtime.Api/appsettings.json backend/src/FacilityRealtime.Api/appsettings.Development.json backend/src/FacilityRealtime.Api/Program.cs backend/tests/FacilityRealtime.ApiTests/Setup/DatabaseSettingsTests.cs
git commit -m "feat: the database setting is required outside Development (facility-0093)"
```

---

### Task 2: `reset-admin` names the database and waits for `yes`

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/Commands/ResetAdminCommand.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs:17-27`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/ResetAdminCommandTests.cs`

**Interfaces:**
- Consumes: `DatabaseSettings.ConnectionString(IConfiguration)` and `DatabaseSettings.Describe(string)` from Task 1.
- Produces: in `ResetAdminCommand`,
  - `public const string Usage = $"วิธีใช้: {Name} <ชื่อผู้ใช้แอดมิน>";`
  - `public static bool ConfirmDatabase(string connectionString, IAdminConsole console)`

- [ ] **Step 1: Write the failing tests**

Add to `ResetAdminCommandTests` (it already has `FakeConsole`):

```csharp
    private const string PilotDatabase = "Server=db.example;Port=3307;Database=facility_pilot;Uid=app;Pwd=s3cret-pass";

    [Theory]
    [InlineData("yes")]
    [InlineData(" YES ")]
    public void Names_the_database_and_goes_on_after_yes(string answer)
    {
        var console = new FakeConsole(answer);

        Assert.True(ResetAdminCommand.ConfirmDatabase(PilotDatabase, console));
        Assert.Contains(console.Written, line => line.Contains("db.example:3307 / facility_pilot"));
        Assert.Single(console.ShownPrompts);
        Assert.Empty(console.HiddenPrompts);
        Assert.DoesNotContain(console.Written, line => line.Contains("s3cret-pass"));
    }

    [Theory]
    [InlineData("no")]
    [InlineData("")]
    [InlineData("y")]
    [InlineData(null)]
    public void Anything_but_yes_stops_before_the_database(string? answer)
    {
        var console = new FakeConsole(answer);

        Assert.False(ResetAdminCommand.ConfirmDatabase(PilotDatabase, console));
        Assert.Equal("ไม่ได้พิมพ์ yes ไม่ได้เปลี่ยนอะไร", console.Written[^1]);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~ResetAdminCommandTests"`
Expected: build FAIL with "'ResetAdminCommand' does not contain a definition for 'ConfirmDatabase'".

- [ ] **Step 3: Add `Usage` and `ConfirmDatabase`**

In `ResetAdminCommand`, below `public const string Name = "reset-admin";`:

```csharp
    public const string Usage = $"วิธีใช้: {Name} <ชื่อผู้ใช้แอดมิน>";

    /// <summary>
    /// facility-0093 decision 2: names the database before anything touches it, the migration included, and goes on
    /// only after "yes". Shows server, port and database, never the database's user name or password.
    /// </summary>
    public static bool ConfirmDatabase(string connectionString, IAdminConsole console)
    {
        console.WriteLine($"คำสั่งนี้จะเปลี่ยนข้อมูลในฐานข้อมูล {DatabaseSettings.Describe(connectionString)}");
        if (string.Equals(console.ReadLine("ถ้าถูกต้อง พิมพ์ yes แล้วกด Enter: ")?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        console.WriteLine("ไม่ได้พิมพ์ yes ไม่ได้เปลี่ยนอะไร");
        return false;
    }
```

In `RunAsync`, replace `console.WriteLine($"วิธีใช้: {Name} <ชื่อผู้ใช้แอดมิน>");` with `console.WriteLine(Usage);`. No new using: `FacilityRealtime.Api.Commands` already sees its parent namespace `FacilityRealtime.Api`.

- [ ] **Step 4: Confirm before migrating in `Program.cs`**

Replace the `reset-admin` block (lines 17–27) with:

```csharp
// facility-0092: `reset-admin <username>` recovers Admin access from the server's console, then exits without serving.
// It needs only the connection string, so it runs before the auth setup that asks for Jwt:SigningKey.
// facility-0093: it names the database and waits for "yes" before the migration touches anything.
if (args.Length > 0 && args[0] == ResetAdminCommand.Name)
{
    Console.OutputEncoding = Encoding.UTF8;
    var adminConsole = new SystemAdminConsole();
    if (args.Length != 2)
    {
        adminConsole.WriteLine(ResetAdminCommand.Usage);
        Environment.ExitCode = 2;
        return;
    }

    var adminConnection = DatabaseSettings.ConnectionString(builder.Configuration);
    if (!ResetAdminCommand.ConfirmDatabase(adminConnection, adminConsole))
    {
        Environment.ExitCode = 1;
        return;
    }

    var options = new DbContextOptionsBuilder<AppDbContext>().UseMySQL(adminConnection).Options;
    await using var adminDb = new AppDbContext(options);
    await adminDb.Database.MigrateAsync();
    Environment.ExitCode = await ResetAdminCommand.RunAsync(args[1..], adminDb, new Pbkdf2PasswordHasher(), TimeProvider.System, adminConsole);
    return;
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS. The existing `ResetAdminCommandTests` are unchanged and still pass, because `RunAsync` does not confirm.

- [ ] **Step 6: Commit**

```bash
git add backend/src/FacilityRealtime.Api/Commands/ResetAdminCommand.cs backend/src/FacilityRealtime.Api/Program.cs backend/tests/FacilityRealtime.ApiTests/Admin/ResetAdminCommandTests.cs
git commit -m "feat: reset-admin names the database and waits for yes (facility-0093)"
```

---

### Task 3: `scripts/facility.sh` with its test

**Files:**
- Create: `scripts/facility.sh` (mode 755)
- Create: `scripts/facility.test.sh` (mode 755)

**Interfaces:**
- Produces: the commands `scripts/facility.sh init <env-file>`, `scripts/facility.sh start <env-file>`, `scripts/facility.sh reset-admin <env-file> <username>`. Exit code 2 means a refusal by the script. Otherwise the code is the API's own, because the script `exec`s it.

- [ ] **Step 1: Write the failing test**

`scripts/facility.test.sh`:

```bash
#!/usr/bin/env bash
# facility-0093: checks scripts/facility.sh with a fake dotnet that prints what it was given. Run: bash scripts/facility.test.sh
set -u

script="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/facility.sh"
repo_root="$(cd "$(dirname "$script")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/bin"
cat > "$work/bin/dotnet" <<'EOF'
#!/bin/sh
printf 'args=%s\n' "$*"
printf 'conn=%s|\n' "${ConnectionStrings__DefaultConnection:-}"
printf 'env=%s\n' "${ASPNETCORE_ENVIRONMENT:-}"
printf 'stale=%s\n' "${FirstAdmin__Password:-none}"
EOF
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `bash scripts/facility.test.sh`
Expected: every check FAILs, because `scripts/facility.sh` does not exist yet ("No such file or directory"), and the run ends with "failed".

- [ ] **Step 3: Write `scripts/facility.sh`**

```bash
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
```

Then `chmod 755 scripts/facility.sh scripts/facility.test.sh`.

- [ ] **Step 4: Run the test to verify it passes**

Run: `bash scripts/facility.test.sh`
Expected: every line starts with `ok:` and the last line is `all passed`. Also run it with macOS's own bash: `/bin/bash scripts/facility.test.sh` (bash 3.2) → `all passed`.

- [ ] **Step 5: Commit**

```bash
git add scripts/facility.sh scripts/facility.test.sh
git commit -m "feat: one script starts the system or runs reset-admin from an env file (facility-0093)"
```

---

### Task 4: The deploy guide in `docs/handover.md`

**Files:**
- Modify: `docs/handover.md`
- Modify: `docs/handoff.md` (`## Sharing a test build`, step 3)

**Interfaces:**
- Consumes: the script commands from Task 3, the `reset-admin` confirmation text from Task 2, and the setting name `ConnectionStrings__DefaultConnection` from Task 1.

- [ ] **Step 1: Replace the opening note**

Replace the line `การติดตั้ง server, https และค่าความลับอื่น (...) จะเพิ่มในเอกสารนี้ในงานถัดไป (คู่มือ deploy)` with:

```markdown
ลำดับครั้งแรก: ข้อ 2 ติดตั้งระบบบน server → ข้อ 3 สร้างแอดมินคนแรก → ข้อ 5 ให้มีแอดมิน 2 คน ข้อ 4 ใช้เมื่อไม่มีแอดมินคนไหนเข้าระบบได้

**รหัสผ่านทุกจุดในเอกสารนี้พิมพ์แบบไม่แสดงบนจอ ตรวจว่าแป้นพิมพ์เป็นภาษาอังกฤษก่อนพิมพ์** ถ้าแป้นเป็นภาษาไทย รหัสที่ได้จะเป็นตัวไทยโดยมองไม่เห็น
```

- [ ] **Step 2: Update section 1**

Replace its last bullet with:

```markdown
- ค่าทั้งหมดอยู่ในไฟล์ค่าไฟล์เดียว (ข้อ 2.4) ทั้งระบบที่รันด้วย systemd และ `scripts/facility.sh` อ่านไฟล์นี้ จึงไม่ต้อง `export` ค่าเองในหน้าต่าง Terminal ค่าที่ `export` ไว้มีผลแค่หน้าต่างนั้น เคยทำให้คำสั่งไปทำกับฐานข้อมูลผิดตัว
- ถ้าไม่ได้ตั้ง `ConnectionStrings__DefaultConnection` ระบบไม่ยอมเริ่ม และ `reset-admin` ไม่ทำอะไร (facility-0093)
```

- [ ] **Step 3: Insert a new section 2 and renumber**

Insert after section 1. Rename the old sections: "2. สร้างแอดมินคนแรก" → "3.", "3. กู้คืน…" → "4.", "4. ให้มีแอดมิน…" → "5.". Update every "ข้อ 3" / "ข้อ 4" cross-reference inside them to the new numbers.

````markdown
## 2. ติดตั้งระบบบน server (Linux)

เขียนสำหรับ Ubuntu Server 24.04 LTS ยังไม่ได้เลือก server จริง (facility-0093) ถ้าเลือก Windows Server ต้องเขียนส่วนนี้เพิ่ม

### 2.1 สิ่งที่ต้องมี

- server Linux ที่เปิด port 80 และ 443 จากอินเทอร์เน็ตได้ (มือถือแม่บ้านใช้เน็ตของตัวเอง facility-0049)
- domain ที่ชี้มาที่ server เช่น `facility.example.com` ป้าย QR พิมพ์จาก domain นี้ เปลี่ยน domain = พิมพ์ป้ายใหม่ทั้งหมด
- ASP.NET Core Runtime 10 (`sudo apt install aspnetcore-runtime-10.0`)
- MySQL 8 ขึ้นไป
- Caddy สำหรับ https (`sudo apt install caddy`) ขอและต่ออายุใบรับรองให้เอง

### 2.2 ฐานข้อมูลและผู้ใช้ MySQL ของระบบ

ระบบใช้ผู้ใช้ MySQL ของตัวเอง ที่มีสิทธิ์แค่ฐานข้อมูลของระบบ ไม่ใช้ root เข้า MySQL ด้วย root แล้วรัน (เปลี่ยน `<รหัสผ่าน>` ห้ามมี `;` `'` หรือ `"`)

```sql
CREATE DATABASE facility CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER 'facility_app'@'localhost' IDENTIFIED BY '<รหัสผ่าน>';
GRANT ALL PRIVILEGES ON facility.* TO 'facility_app'@'localhost';
```

ตารางทั้งหมดสร้างเองตอนระบบเริ่มครั้งแรก (migrate)

### 2.3 สร้างไฟล์โปรแกรมแล้ววางบน server

บนเครื่องที่มีโค้ด (ต้องมี .NET SDK 10 และ Node.js)

```bash
npm --prefix frontend ci
npm --prefix frontend run build:api
dotnet publish backend/src/FacilityRealtime.Api -c Release -o publish
```

คัดลอกโฟลเดอร์ `publish` ไปที่ `/opt/facility/app` บน server และ `scripts/facility.sh` ไปที่ `/opt/facility/facility.sh` แล้วบน server

```bash
sudo useradd --system --no-create-home facility
sudo chmod 755 /opt/facility/facility.sh
```

### 2.4 ไฟล์ค่า

```bash
sudo /opt/facility/facility.sh init /etc/facility/facility.env
```

ตอบคำถามทีละข้อ ที่อยู่ `FacilityRealtime.Api.dll` คือ `/opt/facility/app/FacilityRealtime.Api.dll` สคริปต์สุ่ม `Jwt__SigningKey` ให้ ไฟล์ที่ได้อ่านได้เฉพาะ root

| ค่า | ความหมาย |
|---|---|
| `ConnectionStrings__DefaultConnection` | ที่อยู่ฐานข้อมูล ผู้ใช้ และรหัสผ่านจากข้อ 2.2 |
| `Jwt__SigningKey` | กุญแจเซ็น token อย่างน้อย 32 ไบต์ เปลี่ยนเมื่อไหร่ ทุกคนต้องเข้าระบบใหม่ |
| `Jwt__RefreshCookieSecure` | `true` เพราะเปิดผ่าน https |
| `ASPNETCORE_URLS` | `http://localhost:5001` ระบบรับเฉพาะจากในเครื่อง Caddy ส่งต่อให้ |
| `ASPNETCORE_ENVIRONMENT` | `Production` ห้ามตั้ง `Development` บน server เพราะ seed มีรหัสแอดมินที่ทุกคนเห็น (facility-0049) |
| `FirstAdmin__*` | แอดมินคนแรก ดูข้อ 3 |
| `FACILITY_APP_DLL` | ให้ `facility.sh` รันไฟล์ที่ publish แล้ว ระบบไม่ได้อ่านค่านี้ |

แก้ไฟล์ภายหลังด้วย `sudo nano /etc/facility/facility.env` บรรทัดละ `ชื่อ=ค่า` ห้ามใส่เครื่องหมายคำพูด แล้ว `sudo systemctl restart facility`

### 2.5 ให้ระบบรันเองตลอด (systemd)

สร้าง `/etc/systemd/system/facility.service`

```ini
[Unit]
Description=Facility Real-time Dashboard
After=network.target mysql.service

[Service]
User=facility
WorkingDirectory=/opt/facility/app
EnvironmentFile=/etc/facility/facility.env
ExecStart=/usr/bin/dotnet /opt/facility/app/FacilityRealtime.Api.dll
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now facility
sudo journalctl -u facility -f
```

log ต้องไม่มีข้อผิดพลาด ถ้าขึ้น `ConnectionStrings__DefaultConnection is not set` ให้ตรวจไฟล์ค่า

### 2.6 https (Caddy)

แก้ `/etc/caddy/Caddyfile` เป็น

```
facility.example.com {
    reverse_proxy localhost:5001
}
```

แล้ว `sudo systemctl reload caddy` เปิด `https://facility.example.com` ต้องเห็นหน้าเข้าระบบ ต่อไปทำข้อ 3

### 2.7 สำรองฐานข้อมูลทุกวัน

สร้าง `/etc/cron.daily/facility-backup` (`sudo chmod 755`)

```bash
#!/bin/sh
mkdir -p /var/backups/facility
mysqldump --single-transaction facility | gzip > /var/backups/facility/facility-$(date +%F).sql.gz
find /var/backups/facility -name '*.sql.gz' -mtime +14 -delete
```

cron รันด้วย root ซึ่งต้องเข้า MySQL ได้โดยไม่ต้องพิมพ์รหัส (Ubuntu ตั้ง root ของ MySQL แบบ `auth_socket` ให้อยู่แล้ว) ควรคัดลอกไฟล์ backup ออกไปเก็บนอก server ด้วย

กู้คืน: หยุดระบบ (`sudo systemctl stop facility`) แล้ว `gunzip < ไฟล์.sql.gz | sudo mysql facility` แล้วเริ่มระบบ

### 2.8 อัปเดตเป็นเวอร์ชันใหม่

1. ทำข้อ 2.3 บนเครื่องที่มีโค้ด
2. `sudo systemctl stop facility` แล้วสำรองฐานข้อมูลตามข้อ 2.7 ทันที
3. แทนที่ `/opt/facility/app` ด้วยโฟลเดอร์ `publish` ใหม่ แล้ว `sudo systemctl start facility` ระบบ migrate ฐานข้อมูลเองตอนเริ่ม

### 2.9 ทดลองบนเครื่องตัวเองก่อน (ไม่มี server)

```bash
scripts/facility.sh init ~/.facility/trial.env
scripts/facility.sh start ~/.facility/trial.env
cloudflared tunnel --url http://localhost:5001
```

ใช้ฐานข้อมูลทดลองและข้อมูลสมมติเท่านั้น ลิงก์ `trycloudflare.com` เปลี่ยนทุกครั้งที่เปิด ป้าย QR ที่พิมพ์จากลิงก์นี้ใช้ได้แค่ครั้งนั้น
````

- [ ] **Step 4: Update the old sections that are now 3 and 4**

In the new section 3 ("สร้างแอดมินคนแรก"), replace step 1's export example and its `read -rs` note with:

```markdown
1. ค่า 3 ตัวนี้อยู่ในไฟล์ค่าแล้ว ถ้าสร้างด้วย `facility.sh init` (ข้อ 2.4)
   - `FirstAdmin__Username` ชื่อผู้ใช้ ตัวอักษรอังกฤษ ตัวเลข `.` `_` หรือ `-` ยาว 3–100 ตัว
   - `FirstAdmin__Password` รหัสผ่าน ยาว 15–100 ตัว และไม่ใช่รหัสที่คนใช้กันเยอะ
   - `FirstAdmin__DisplayName` ชื่อที่แสดงในระบบ ยาว 1–150 ตัว

   ห้ามเขียนค่าเหล่านี้ลงไฟล์ `appsettings.json` หรือเก็บใน git
```

Replace step 4 with:

```markdown
4. ลบบรรทัด `FirstAdmin__Password` ออกจากไฟล์ค่า แล้ว `sudo systemctl restart facility` ถ้ายังตั้งค้างไว้ log จะเตือน `FirstAdmin__Password is still set`
```

In the new section 4 ("กู้คืน … `reset-admin`"), replace steps 1 and 2 with:

````markdown
1. เข้า server แล้วรัน
   ```bash
   sudo /opt/facility/facility.sh reset-admin /etc/facility/facility.env <ชื่อผู้ใช้แอดมิน>
   ```
   สคริปต์ใช้ไฟล์ค่าเดียวกับระบบ จึงไปที่ฐานข้อมูลเดียวกันเสมอ ถ้าทดลองบนเครื่องตัวเอง ใช้ `scripts/facility.sh reset-admin ~/.facility/trial.env <ชื่อผู้ใช้แอดมิน>`
2. คำสั่งบอกก่อนว่าจะเปลี่ยนข้อมูลในฐานข้อมูลไหน เช่น `localhost:3306 / facility` ตรวจว่าถูกตัว แล้วพิมพ์ `yes` ถ้าพิมพ์อย่างอื่น คำสั่งจบโดยไม่แตะฐานข้อมูล
````

In step 5 of that section, replace "ให้ตรวจ `ConnectionStrings__DefaultConnection` ว่าตรงกับที่ระบบใช้" with "ให้ตรวจ `ConnectionStrings__DefaultConnection` ในไฟล์ค่า".

- [ ] **Step 5: Point the handoff's test-build steps at the script**

In `docs/handoff.md` `## Sharing a test build`, append to step 3: ` Simpler and safer since facility-0093: `scripts/facility.sh init` an env file outside the repo for the throwaway database, then `scripts/facility.sh start` it (Production, no seed: the Admin comes from `FirstAdmin__*`).`

- [ ] **Step 6: Read the guide top to bottom once**

Check: section numbers run 1–5; every "ข้อ N" points at the right section; no setting name is spelt two ways; the Thai-keyboard warning is in the opening note and in `facility.sh init`.

- [ ] **Step 7: Commit**

```bash
git add docs/handover.md docs/handoff.md
git commit -m "docs: Thai deploy guide for a Linux server (facility-0093)"
```

---

### Task 5: Real run on MySQL (controller only, never a subagent)

**Files:** none changed, unless a step fails.

- [ ] **Step 1:** The owner creates a throwaway database `facility_deploy_test` and a user `facility_deploy_app` with rights on it only (the root password is typed by the owner).
- [ ] **Step 2:** `npm --prefix frontend run build:api`, then `scripts/facility.sh init ~/.facility/deploy-test.env` (answers: `facility_deploy_test`, `facility_deploy_app`, made-up Admin).
- [ ] **Step 3:** `scripts/facility.sh start ~/.facility/deploy-test.env` → the log has `Created the first Admin`. Log in at `http://localhost:5001/login/admin`.
- [ ] **Step 4:** In another window with nothing exported: `ASPNETCORE_ENVIRONMENT=Production dotnet run --no-launch-profile --project backend/src/FacilityRealtime.Api` → stops with `ConnectionStrings__DefaultConnection is not set`. Then the same with `-- reset-admin admin` → the same message, and `facility_dashboard` is untouched.
- [ ] **Step 5:** `scripts/facility.sh reset-admin ~/.facility/deploy-test.env <admin>` → shows `localhost:3306 / facility_deploy_test`. Answer `no` → exit 1, no new row in D9. Run it again, answer `yes` → the password is reset and D9 has a "ระบบ" row.
- [ ] **Step 6:** `dotnet run --project backend/src/FacilityRealtime.Api` (launch profile, Development) still starts on `facility_dashboard`; stop it at once without logging in.
- [ ] **Step 7:** Clean up: stop the API, drop `facility_deploy_test` and the user `facility_deploy_app`, delete `~/.facility/deploy-test.env` and `wwwroot`. Write the result in `docs/handoff.md` and commit.
