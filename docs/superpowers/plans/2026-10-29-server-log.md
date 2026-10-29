# Server Log Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The server writes one journal line per security event (failed login, login refused while locked, reused refresh token), stops writing every SQL statement, and the deploy guide keeps those lines 90 days in the system's own journal namespace.

**Architecture:** A small `SecurityLog` service (one logger category, `FacilityRealtime.Security`) is called from `AuthEndpoints.LoginAsync` and `RefreshAsync`. One `appsettings.json` level silences EF Core's per-statement lines. The 90 days and the namespace are server settings, written into `docs/handover.md` §2.5; nothing in the app knows about them.

**Tech Stack:** .NET 10 Minimal API, `Microsoft.Extensions.Logging`, EF Core 10, xUnit with `WebApplicationFactory`, systemd-journald (deploy guide only).

**Spec:** `docs/superpowers/specs/2026-10-29-server-log-design.md` (ADRs facility-0095 to 0099; glossary term **Server Log** in `CONTEXT.md`).

## Global Constraints

- Logger category for every security line: `FacilityRealtime.Security`. Level: `Warning`.
- Exact line texts (IT greps for them; tests pin them):
  - `Login failed: employee {EmployeeId}, wrong secret` / `..., account turned off`; for an Admin `Login failed: admin {Username}, ...`
  - `Login failed: unknown account (employee login)` / `(admin login)`
  - `Login refused while locked: employee {EmployeeId}` (or `admin {Username}`, or `unknown account (employee login)` / `(admin login)`)
  - `Refresh token reused, session ended: employee {EmployeeId}` (or `admin {Username}`)
- Never in any log line: a phone number, a password, a token value (access, refresh, QR Token), a GPS coordinate, or the typed login text of an account that does not exist. `EnableSensitiveDataLogging` stays off everywhere.
- `appsettings.json` → `Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command` = `Warning`.
- Deploy guide: `LogNamespace=facility`; `/etc/systemd/journald@facility.conf` with `MaxRetentionSec=90day` and `MaxFileSec=1day`; read with `sudo journalctl --namespace=facility -u facility`; needs systemd 245 or later.
- No password literal in tests or docs (GitGuardian rule from `docs/handoff.md`): build a wrong password at run time.
- ADRs and `docs/handover.md` are written in Thai, like the files around them.
- Work on branch `feat/server-log`. Commit per task. Do **not** push, open a PR or merge without the owner's explicit OK.
- Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `backend/src/FacilityRealtime.Api/appsettings.json` | Modify | Silence EF Core's per-statement lines |
| `backend/src/FacilityRealtime.Api/Auth/SecurityLog.cs` | Create | The four security lines and the account-naming rule |
| `backend/src/FacilityRealtime.Api/Auth/AuthSetup.cs` | Modify | Register `SecurityLog` |
| `backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs` | Modify | Call `SecurityLog` at the 401s, the 429 and the reuse branch |
| `backend/tests/FacilityRealtime.ApiTests/Infrastructure/LogCapture.cs` | Create | Test logger provider that keeps every line |
| `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs` | Modify | Expose `Logs` (a `LogCapture`) on every factory |
| `backend/tests/FacilityRealtime.ApiTests/ServerLogTests.cs` | Create | SQL statements stay out of the log |
| `backend/tests/FacilityRealtime.ApiTests/Auth/SecurityLogTests.cs` | Create | Each security line, and what never appears |
| `docs/handover.md` | Modify | §2.5 namespace + 90 days + read commands; §3 read command |
| `docs/adr/facility-0055-…`, `docs/adr/facility-0093-…` | Modify | Amendment lines |
| `docs/code-map.md` | Modify | Where the security lines are written; refreshed line numbers |
| `docs/handoff.md` | Modify | Status after the work |

---

### Task 1: Keep SQL statements out of the log (plus the log capture the tests need)

**Files:**
- Create: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/LogCapture.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/ServerLogTests.cs`
- Modify: `backend/src/FacilityRealtime.Api/appsettings.json`

**Interfaces:**
- Produces: `LogCapture : ILoggerProvider` with `IReadOnlyList<LogLine> Lines`; `record LogLine(string Category, LogLevel Level, string Message)`; `FacilityApiFactory.Logs` (a `LogCapture`, one per factory). Task 2's tests read `factory.Logs.Lines`.

- [ ] **Step 1: Branch and commit the design documents**

The design documents from the grilling session are uncommitted on `master`. Put them on the feature branch first:

```bash
cd /Users/macbookair/Projects/Facility-Real-time-dashboard
git switch -c feat/server-log
git add CONTEXT.md docs/adr/facility-0095-logging-scope-security-events-and-errors.md docs/adr/facility-0096-server-log-read-from-journal-only.md docs/adr/facility-0097-server-log-names-account-by-employee-id-never-typed-text.md docs/adr/facility-0098-server-log-kept-90-days.md docs/adr/facility-0099-server-log-in-its-own-journal-namespace.md docs/superpowers/specs/2026-10-29-server-log-design.md docs/superpowers/plans/2026-10-29-server-log.md
git add docs/handoff.md
git commit -m "$(cat <<'EOF'
docs: Server Log design (facility-0095 to 0099), spec and plan

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

Do not add `backend/.idea/` (the owner's IDE folder).

- [ ] **Step 2: Create the test logger provider**

`backend/tests/FacilityRealtime.ApiTests/Infrastructure/LogCapture.cs`:

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>
/// Every line the API logs while a test runs. The app's level rules (appsettings.json) still apply, because the
/// logger factory filters before a provider sees a line, so a test reads what the server's journal would get.
/// </summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<LogLine> _lines = new();

    public IReadOnlyList<LogLine> Lines => _lines.ToArray();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<LogLine> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue(new LogLine(category, logLevel, exception is null ? formatter(state, exception) : $"{formatter(state, exception)} {exception}"));
    }
}

public sealed record LogLine(string Category, LogLevel Level, string Message);
```

- [ ] **Step 3: Give every factory a `Logs` capture**

In `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs`:

Add the using with the others:

```csharp
using Microsoft.Extensions.Logging;
```

Add the property under `Clock`:

```csharp
    /// <summary>Every line the API logged (Server Log, facility-0095).</summary>
    public LogCapture Logs { get; } = new();
```

Register it inside `builder.ConfigureServices(services => { ... })`, just before `ConfigureTestServices?.Invoke(services);`:

```csharp
            services.AddSingleton<ILoggerProvider>(Logs);
```

- [ ] **Step 4: Write the failing test**

`backend/tests/FacilityRealtime.ApiTests/ServerLogTests.cs`:

```csharp
using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests;

/// <summary>facility-0095: the Server Log holds security events and server errors, not every SQL statement.</summary>
public class ServerLogTests
{
    [Fact]
    public async Task A_login_writes_no_SQL_statement_to_the_log()
    {
        using var factory = new FacilityApiFactory();

        await AuthApi.LoginAsync(factory.CreateApiClient());

        Assert.DoesNotContain(factory.Logs.Lines, line => line.Category == "Microsoft.EntityFrameworkCore.Database.Command");
    }
}
```

- [ ] **Step 5: Run it and watch it fail**

Run: `cd backend && dotnet test tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~ServerLogTests"`
Expected: FAIL. The assertion lists `Microsoft.EntityFrameworkCore.Database.Command` lines (`Executed DbCommand …`), because the default level is `Information`.

- [ ] **Step 6: Silence the per-statement lines**

`backend/src/FacilityRealtime.Api/appsettings.json`, the `Logging` block becomes:

```json
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
```

`appsettings.Development.json` stays as it is: configuration merges by key, so Development inherits the new key.

- [ ] **Step 7: Run the test and the whole API suite**

Run: `cd backend && dotnet test tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~ServerLogTests"`
Expected: PASS.

Run: `cd backend && dotnet test`
Expected: unit 343 passed; API 638 passed, 5 skipped (MySQL only).

- [ ] **Step 8: Commit**

```bash
git add backend/src/FacilityRealtime.Api/appsettings.json backend/tests/FacilityRealtime.ApiTests/Infrastructure/LogCapture.cs backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs backend/tests/FacilityRealtime.ApiTests/ServerLogTests.cs
git commit -m "$(cat <<'EOF'
feat(log): keep SQL statements out of the Server Log (facility-0095)

EF Core wrote every SQL statement at Information: 510 lines on a first
start, 36 for six failed logins. Database.Command is now Warning, so a
failed statement is still logged. Tests get a LogCapture on every factory.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Security lines for failed logins, locked logins and reused refresh tokens

**Files:**
- Create: `backend/src/FacilityRealtime.Api/Auth/SecurityLog.cs`
- Modify: `backend/src/FacilityRealtime.Api/Auth/AuthSetup.cs` (in `AddFacilityAuth`, beside `services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();`)
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs` (`LoginAsync` lines 35-83, `RefreshAsync` lines 85-131)
- Create: `backend/tests/FacilityRealtime.ApiTests/Auth/SecurityLogTests.cs`

**Interfaces:**
- Consumes: `FacilityApiFactory.Logs.Lines` (`LogLine(Category, Level, Message)`) from Task 1.
- Produces: `public sealed class SecurityLog(ILoggerFactory loggerFactory)` in `FacilityRealtime.Api.Auth` with `const string Category = "FacilityRealtime.Security"` and methods `LoginFailed(User user, bool secretMatched)`, `LoginFailedUnknownAccount(bool byEmployeeId)`, `LoginRefusedWhileLocked(User? user, bool byEmployeeId)`, `RefreshTokenReused(User user)`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/FacilityRealtime.ApiTests/Auth/SecurityLogTests.cs`:

```csharp
using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FacilityRealtime.ApiTests.Auth;

/// <summary>
/// facility-0095..0097: one Warning line per security event, in category FacilityRealtime.Security. An account is
/// named by employee ID (an Admin by username); what was typed for an account that does not exist never appears.
/// </summary>
public class SecurityLogTests
{
    private static List<string> SecurityLines(FacilityApiFactory factory) =>
        factory.Logs.Lines
            .Where(line => line.Category == "FacilityRealtime.Security")
            .Select(line => { Assert.Equal(LogLevel.Warning, line.Level); return line.Message; })
            .ToList();

    private static void NowhereInTheLog(FacilityApiFactory factory, string text) =>
        Assert.DoesNotContain(factory.Logs.Lines, line => line.Message.Contains(text, StringComparison.Ordinal));

    [Fact]
    public async Task A_wrong_phone_names_the_employee_and_never_the_phone()
    {
        using var factory = new FacilityApiFactory();

        await AuthApi.LoginAsync(factory.CreateApiClient(), "1000001", "0899999999");

        Assert.Equal(new[] { "Login failed: employee 1000001, wrong secret" }, SecurityLines(factory));
        NowhereInTheLog(factory, "0899999999");
    }

    [Fact]
    public async Task A_turned_off_account_with_the_right_phone_says_so()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "1000001")).IsActive = false;
            await db.SaveChangesAsync();
        });

        await AuthApi.LoginAsync(factory.CreateApiClient(), "1000001", "0810000001");

        Assert.Equal(new[] { "Login failed: employee 1000001, account turned off" }, SecurityLines(factory));
        NowhereInTheLog(factory, "0810000001");
    }

    [Fact]
    public async Task A_phone_typed_into_the_employee_id_field_is_never_written()
    {
        using var factory = new FacilityApiFactory();

        // both fields take digits only (facility-0054), so the fields get swapped
        await AuthApi.LoginAsync(factory.CreateApiClient(), "0810000001", "1000001");

        Assert.Equal(new[] { "Login failed: unknown account (employee login)" }, SecurityLines(factory));
        NowhereInTheLog(factory, "0810000001");
    }

    [Fact]
    public async Task A_login_name_longer_than_any_account_is_an_unknown_account()
    {
        using var factory = new FacilityApiFactory();

        await AuthApi.LoginAsync(factory.CreateApiClient(), new string('1', 101), "0810000001");

        Assert.Equal(new[] { "Login failed: unknown account (employee login)" }, SecurityLines(factory));
    }

    [Fact]
    public async Task An_admins_wrong_password_names_the_username_and_never_the_password()
    {
        using var factory = new FacilityApiFactory();
        var typed = $"wrong-{Guid.NewGuid():N}"; // built at run time: no password literal in the repo

        await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin", typed);

        Assert.Equal(new[] { "Login failed: admin admin, wrong secret" }, SecurityLines(factory));
        NowhereInTheLog(factory, typed);
    }

    [Fact]
    public async Task Each_attempt_refused_while_locked_is_written()
    {
        using var factory = new FacilityApiFactory();
        var client = factory.CreateApiClient();
        for (var i = 0; i < 5; i++)
        {
            await AuthApi.LoginAsync(client, "1000001", "0899999999");
        }

        var refused = await AuthApi.LoginAsync(client, "1000001", "0810000001");
        await AuthApi.LoginAsync(client, "1000001", "0810000001");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        var lines = SecurityLines(factory);
        Assert.Equal(Enumerable.Repeat("Login failed: employee 1000001, wrong secret", 5), lines.Take(5));
        Assert.Equal(new[] { "Login refused while locked: employee 1000001", "Login refused while locked: employee 1000001" }, lines.Skip(5));
    }

    [Fact]
    public async Task A_locked_unknown_account_is_written_without_what_was_typed()
    {
        using var factory = new FacilityApiFactory();
        var client = factory.CreateApiClient();
        for (var i = 0; i < 6; i++)
        {
            await AuthApi.LoginAsync(client, "9999999", "0812345678");
        }

        Assert.Equal("Login refused while locked: unknown account (employee login)", SecurityLines(factory).Last());
        NowhereInTheLog(factory, "9999999");
        NowhereInTheLog(factory, "0812345678");
    }

    [Fact]
    public async Task A_reused_refresh_token_names_the_account_whose_session_ended()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAsync(factory);
        await AuthApi.RefreshAsync(client, first);

        factory.Clock.Advance(TimeSpan.FromSeconds(31)); // past the 30 s grace of facility-0015
        var replay = await AuthApi.RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(new[] { "Refresh token reused, session ended: employee 1000001" }, SecurityLines(factory));
        NowhereInTheLog(factory, first);
    }

    [Fact]
    public async Task A_successful_login_writes_no_security_line()
    {
        using var factory = new FacilityApiFactory();

        await AuthApi.LoginAsync(factory.CreateApiClient());

        Assert.Empty(SecurityLines(factory));
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

Run: `cd backend && dotnet test tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~SecurityLogTests"`
Expected: 8 FAIL (no security line is written yet; the collections are empty). `A_successful_login_writes_no_security_line` passes already; it guards against logging every login.

- [ ] **Step 3: Create `SecurityLog`**

`backend/src/FacilityRealtime.Api/Auth/SecurityLog.cs`:

```csharp
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.Auth;

/// <summary>
/// facility-0095..0097: the Server Log's security lines, one Warning each, all in one category so
/// `journalctl --namespace=facility -u facility | grep Security` finds them. An account is named by employee ID, or
/// by username for an Admin. A login name that matches no account is never written (a phone typed into the wrong
/// field would land in the journal), and neither is any phone, password, token or GPS value.
/// </summary>
public sealed class SecurityLog(ILoggerFactory loggerFactory)
{
    public const string Category = "FacilityRealtime.Security";

    private readonly ILogger _logger = loggerFactory.CreateLogger(Category);

    /// <summary>The account exists: either the secret was wrong, or it was right and the account is turned off.</summary>
    public void LoginFailed(User user, bool secretMatched) =>
        _logger.LogWarning("Login failed: {Account}, {Reason}", Account(user), secretMatched ? "account turned off" : "wrong secret");

    public void LoginFailedUnknownAccount(bool byEmployeeId) =>
        _logger.LogWarning("Login failed: unknown account ({Kind} login)", Kind(byEmployeeId));

    /// <summary>One line per refused attempt: repeated lines show the guessing goes on.</summary>
    public void LoginRefusedWhileLocked(User? user, bool byEmployeeId)
    {
        if (user is null)
        {
            _logger.LogWarning("Login refused while locked: unknown account ({Kind} login)", Kind(byEmployeeId));
        }
        else
        {
            _logger.LogWarning("Login refused while locked: {Account}", Account(user));
        }
    }

    public void RefreshTokenReused(User user) =>
        _logger.LogWarning("Refresh token reused, session ended: {Account}", Account(user));

    private static string Account(User user) =>
        user.Role == UserRole.Admin ? $"admin {user.Username}" : $"employee {user.EmployeeId}";

    private static string Kind(bool byEmployeeId) => byEmployeeId ? "employee" : "admin";
}
```

- [ ] **Step 4: Register it**

`backend/src/FacilityRealtime.Api/Auth/AuthSetup.cs`, in `AddFacilityAuth`, right after `services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();`:

```csharp
        services.AddSingleton<SecurityLog>();
```

- [ ] **Step 5: Write the lines from `LoginAsync`**

`backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs`.

Add a parameter at the end of `LoginAsync`'s list, after `IOptions<JwtSettings> jwt`:

```csharp
        IOptions<JwtSettings> jwt,
        SecurityLog securityLog)
```

The too-long login name (currently):

```csharp
        if (loginName.Length > MaxLoginNameLength)
        {
            // Longer than any login column, so no account can match; nothing is counted or kept in memory
            return Results.Unauthorized();
        }
```

becomes:

```csharp
        if (loginName.Length > MaxLoginNameLength)
        {
            // Longer than any login column, so no account can match; nothing is counted or kept in memory
            securityLog.LoginFailedUnknownAccount(byEmployeeId);
            return Results.Unauthorized();
        }
```

The throttle and secret check (currently):

```csharp
        if (!throttle.TryBeginAttempt(throttleKey))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var secretMatches = hasher.Verify(secret, user?.SecretHash ?? DummyPasswordHash);
        if (user is null || !user.IsActive || !secretMatches)
        {
            return Results.Unauthorized();
        }
```

becomes:

```csharp
        if (!throttle.TryBeginAttempt(throttleKey))
        {
            securityLog.LoginRefusedWhileLocked(user, byEmployeeId);
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var secretMatches = hasher.Verify(secret, user?.SecretHash ?? DummyPasswordHash);
        if (user is null)
        {
            securityLog.LoginFailedUnknownAccount(byEmployeeId);
            return Results.Unauthorized();
        }

        if (!user.IsActive || !secretMatches)
        {
            // facility-0097: the journal tells the two apart; the phone still gets the same 401 for both
            securityLog.LoginFailed(user, secretMatches);
            return Results.Unauthorized();
        }
```

- [ ] **Step 6: Write the line from `RefreshAsync`**

Add the parameter at the end of `RefreshAsync`'s list:

```csharp
        IOptions<JwtSettings> jwt,
        SecurityLog securityLog)
```

The reuse branch (currently):

```csharp
            case RefreshDecision.ReuseDetected:
                // ADR facility-0015: two parties hold this token and the server cannot tell which is the owner
                await db.RevokeSessionAsync(stored.SessionId, now);
                return Unauthorized(http, jwt.Value);
```

becomes:

```csharp
            case RefreshDecision.ReuseDetected:
                // ADR facility-0015: two parties hold this token and the server cannot tell which is the owner
                await db.RevokeSessionAsync(stored.SessionId, now);
                securityLog.RefreshTokenReused(stored.User);
                return Unauthorized(http, jwt.Value);
```

`stored.User` is loaded by the `Include(t => t.User)` above and is known not to be null there (the `stored?.User is null` check returns earlier).

- [ ] **Step 7: Run the new tests, then everything**

Run: `cd backend && dotnet test tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~SecurityLogTests"`
Expected: 9 PASS.

Run: `cd backend && dotnet test`
Expected: unit 343 passed; API 647 passed, 5 skipped (MySQL only). If `LoginTests` or `RefreshTests` fail, the change altered a status code: the phone must still get exactly the 401/429 it got before.

- [ ] **Step 8: Commit**

```bash
git add backend/src/FacilityRealtime.Api/Auth/SecurityLog.cs backend/src/FacilityRealtime.Api/Auth/AuthSetup.cs backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs backend/tests/FacilityRealtime.ApiTests/Auth/SecurityLogTests.cs
git commit -m "$(cat <<'EOF'
feat(log): security lines for failed, locked and reused logins (facility-0095, 0097)

One Warning per event in category FacilityRealtime.Security. Accounts are
named by employee ID or Admin username; the typed text of an unknown
account, phones, passwords and tokens are never written.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Deploy guide, docs, and the measurement on the built API

**Files:**
- Modify: `docs/handover.md` (§2.5 lines 85-112, §3 step 2 line 185)
- Modify: `docs/adr/facility-0055-data-retention-and-pdpa-before-production.md` (end of file)
- Modify: `docs/adr/facility-0093-safe-start-and-deploy-guide.md` (end of file)
- Modify: `docs/code-map.md` (failure table around lines 360-375)
- Modify: `docs/handoff.md` (new first section and the `Last updated` line)

**Interfaces:**
- Consumes: the exact line texts from Task 2 and the config key from Task 1.

- [ ] **Step 1: Deploy guide §2.5**

In `docs/handover.md` §2.5, the service file gets `LogNamespace=facility` after `RestartSec=5`:

```ini
Restart=always
RestartSec=5
LogNamespace=facility
```

Replace the block from the line after the service file's closing fence down to and including `log ต้องไม่มีข้อผิดพลาด ถ้าขึ้น ...` with:

````markdown
สร้าง `/etc/systemd/journald@facility.conf` ให้ log ของระบบเก็บ 90 วันแล้วลบเอง (facility-0098, 0099) ค่านี้ใช้กับ log ของระบบนี้เท่านั้น log ของโปรแกรมอื่นบนเครื่องไม่ถูกแตะ ต้องใช้ systemd 245 ขึ้นไป (ดูด้วย `systemctl --version`)

```ini
[Journal]
MaxRetentionSec=90day
MaxFileSec=1day
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now facility
sudo journalctl --namespace=facility -u facility -f
```

log ต้องไม่มีข้อผิดพลาด ถ้าขึ้น `ConnectionStrings__DefaultConnection is not set.` ให้ตรวจไฟล์ค่า

ดูเฉพาะเหตุการณ์ด้านความปลอดภัย: `sudo journalctl --namespace=facility -u facility | grep Security`

- `Login failed: employee 1000001, wrong secret` ใส่เบอร์ผิด (`account turned off` คือบัญชีถูกปิดใช้งาน)
- `Login failed: unknown account (employee login)` รหัสพนักงานที่พิมพ์ไม่มีในระบบ ระบบไม่เขียนสิ่งที่พิมพ์ เพราะอาจเป็นเบอร์โทรที่พิมพ์สลับช่อง
- `Login refused while locked: ...` ผิด 5 ครั้งแล้ว ถูกล็อก 15 นาที ทุกครั้งที่ลองระหว่างล็อกขึ้นหนึ่งบรรทัด
- `Refresh token reused, session ended: ...` มีคนใช้ token เดิมซ้ำ ระบบให้บัญชีนั้นเข้าระบบใหม่ (facility-0015)

log ไม่มีเบอร์โทร รหัสผ่าน token หรือพิกัด (facility-0097)
````

- [ ] **Step 2: Deploy guide §3**

In §3 step 2, `(\`sudo journalctl -u facility\`)` becomes `(\`sudo journalctl --namespace=facility -u facility\`)`. Then check nothing else still uses the old command:

Run: `rg -n "journalctl" docs/handover.md`
Expected: every hit has `--namespace=facility`.

- [ ] **Step 3: ADR amendment lines**

Append to `docs/adr/facility-0055-data-retention-and-pdpa-before-production.md`:

```markdown

> **แก้ไข 2026-10-29 (facility-0098):** Server Log (journal ของ server) เก็บ 90 วันแล้วลบเอง มีรหัสพนักงานหรือ username ของแอดมิน ไม่มีเบอร์โทร รหัสผ่าน token หรือพิกัด (facility-0097)
```

Append to `docs/adr/facility-0093-safe-start-and-deploy-guide.md`:

```markdown

> **แก้ไข 2026-10-29 (facility-0099):** service เขียน log ลง journal namespace `facility` ที่เก็บ 90 วัน (`/etc/systemd/journald@facility.conf`) คำสั่งอ่าน log ในคู่มือเป็น `sudo journalctl --namespace=facility -u facility`
```

- [ ] **Step 4: Code map**

In `docs/code-map.md`, right after the failure table (the row starting `| Role changed |`), add:

```markdown

**Server Log lines** (facility-0095..0097): `Api/Auth/SecurityLog.cs`, called from `AuthEndpoints.LoginAsync` (failed, unknown account, refused while locked) and `RefreshAsync` (reused token). Category `FacilityRealtime.Security`, level Warning; the phone sees the same 401/429 as before. SQL statements stay out because `appsettings.json` sets `Microsoft.EntityFrameworkCore.Database.Command` to `Warning`.
```

Then refresh the `AuthEndpoints.cs:NN-NN` references, which Task 2 shifted:

Run: `rg -n "AuthEndpoints.cs:[0-9]" docs/code-map.md` and `rg -n "private static async Task<IResult>|TryBeginAttempt|ReuseDetected|RevokeUserSessionsAsync|if \(user is null\)" backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs`
Update each code-map reference to the new line numbers (`LoginAsync` range, the 401 branch, the reuse branch, the deactivated branch, the `RefreshAsync` range).

- [ ] **Step 5: Measure on the built API, as in the spec's section 2**

```bash
set -u
cd /Users/macbookair/Projects/Facility-Real-time-dashboard/backend/src/FacilityRealtime.Api
LOG="$(mktemp -d)/api.log"
mysql -u root -e "DROP DATABASE IF EXISTS facility_logcheck_test; CREATE DATABASE facility_logcheck_test CHARACTER SET utf8mb4;"
dotnet build -c Release --nologo -v quiet
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5102 \
ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=facility_logcheck_test;Uid=root;Pwd=;CharSet=utf8mb4;" \
Jwt__SigningKey="$(openssl rand -base64 48)" \
nohup dotnet bin/Release/net10.0/FacilityRealtime.Api.dll >"$LOG" 2>&1 &
PID=$!
curl -s -o /dev/null --retry 40 --retry-connrefused --retry-delay 1 http://localhost:5102/api/auth/me
echo "startup lines: $(wc -l < "$LOG")"
for i in 1 2 3 4 5 6; do curl -s -o /dev/null -w "%{http_code} " -H 'Content-Type: application/json' -d '{"employeeId":"1234567","phone":"0812345678"}' http://localhost:5102/api/auth/login; done; echo
grep -A1 "FacilityRealtime.Security" "$LOG"
mysql -u root -e "DROP DATABASE facility_logcheck_test;"
curl -s -o /dev/null -w "after drop: %{http_code}\n" -H 'Content-Type: application/json' -d '{"employeeId":"7654321","phone":"0899999999"}' http://localhost:5102/api/auth/login
grep -c "An unhandled exception was thrown" "$LOG"
grep -c "Executed DbCommand" "$LOG"
grep -c "0812345678\|1234567\|0899999999\|7654321" "$LOG"
kill $PID; wait $PID 2>/dev/null
mysql -u root -e "SHOW DATABASES LIKE 'facility_logcheck_test';"
```

Expected:
- startup lines: about 15 (lifetime, migrations, the `No Admin account exists` warning), not 510
- `401 401 401 401 401 429`, then five `Login failed: unknown account (employee login)` and one `Login refused while locked: unknown account (employee login)`
- `after drop: 500`, and the unhandled-exception count is `1`
- `Executed DbCommand` count `0`; typed values count `0`
- the last command prints nothing (database gone); the API is stopped

If the numbers differ, write the real ones into the handoff (next step), not these.

- [ ] **Step 6: Handoff**

In `docs/handoff.md`, replace the section `## Server Log (facility-0095 to 0099): designed 2026-10-29, not started` (written at the end of the design session) with the section below, and start the `Last updated:` line with `2026-10-29, later (**Server Log (facility-0095 to 0099) done on branch \`feat/server-log\`, not pushed; see the first section**); earlier: `:

```markdown
## Server Log (facility-0095 to 0099, done 2026-10-29; branch `feat/server-log`, not pushed)

The owner asked whether the project needs logging; grilled on 2026-10-29. Spec `docs/superpowers/specs/2026-10-29-server-log-design.md`, plan `docs/superpowers/plans/2026-10-29-server-log.md`. Glossary: **Server Log** (journal, read by whoever runs the server) is not the **Audit Log** (D9).
- The server now writes one Warning line per security event (failed login, refused while locked, reused refresh token), category `FacilityRealtime.Security`, naming the account by employee ID or Admin username. Nothing typed for an unknown account, no phone, password, token or GPS.
- SQL statements are out of the log (`Microsoft.EntityFrameworkCore.Database.Command` = Warning). Server errors were already logged (Kestrel's unhandled-exception line) and are unchanged.
- Deploy guide §2.5: `LogNamespace=facility`, `/etc/systemd/journald@facility.conf` (90 days), read with `sudo journalctl --namespace=facility -u facility`.
- Measured on the built API (Production, throwaway `facility_logcheck_test`, dropped): <startup line count> startup lines (was 510); six logins wrote 5 failed + 1 locked line; the 500 is still logged; 0 SQL lines; 0 typed values.
- Tests: backend <unit count> unit + <API count> API (5 MySQL-only skipped).
- **On the first Linux server:** check the namespace and the 90 days (cannot be tested on the Mac: no systemd).
- **Not decided, raise if needed:** IP address in security lines; a Windows server would reopen facility-0096, 0098, 0099.
- **Owner to do:** say whether to push, open the PR and merge.
```

Replace the three `<...>` values with what Step 5 and the last `dotnet test` printed before committing.

- [ ] **Step 7: Run the whole backend suite once more**

Run: `cd backend && dotnet test`
Expected: unit 343 passed; API 647 passed, 5 skipped.

- [ ] **Step 8: Commit**

```bash
git add docs/handover.md docs/adr/facility-0055-data-retention-and-pdpa-before-production.md docs/adr/facility-0093-safe-start-and-deploy-guide.md docs/code-map.md docs/handoff.md
git commit -m "$(cat <<'EOF'
docs: deploy guide keeps the Server Log 90 days in its own journal namespace (facility-0098, 0099)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

Then stop and ask the owner about push, PR and merge.
