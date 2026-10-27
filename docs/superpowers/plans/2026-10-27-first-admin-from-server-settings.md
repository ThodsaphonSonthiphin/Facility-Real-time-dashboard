# First Admin from Server Settings and `reset-admin`: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A real server with an empty database gets its first Admin from the server's settings. A `reset-admin` command run on the server recovers Admin access. A Thai handover guide tells whoever takes the system on how to do both.

**Architecture:**
- The Admin history (`audit_log`) gets a nullable actor, which the API and D9 show as "ระบบ".
- `Infrastructure/Persistence/ServerAdminAccounts` creates, finds and resets Admin accounts with the system as the actor.
- `FirstAdmin.EnsureAsync` runs at startup after the migration and the seed.
- `Api/Commands/ResetAdminCommand` runs instead of the web server when the program starts with `reset-admin`.

**Tech Stack:** .NET 10 Minimal API, EF Core 10 (Oracle `MySql.EntityFrameworkCore`; SQLite in tests), xUnit, React + Vitest for the D9 labels.

**Spec:** `docs/adr/facility-0092-first-admin-from-server-settings.md` (Thai). Related: facility-0014, 0051, 0057, 0089.

## Global Constraints

- A password never appears in a log line, an exception message, console output, or an audit row (facility-0057). An error names the setting and the broken rule, never the value.
- The Admin username is checked with `AccountRules.NormalizeUsername`, the display name with `AccountRules.NormalizeDisplayName`, and the password with `AccountRules.PasswordProblem`: 15–100 characters, not on the common-password list (facility-0089). These are the same rules as the accounts page (D6).
- `FirstAdmin__Username`, `FirstAdmin__Password` and `FirstAdmin__DisplayName` are environment variables only. Never add a `FirstAdmin` section to `appsettings*.json`, and never commit them.
- The first Admin is created only while **no** user with `Role == Admin` exists, active or not. Otherwise the settings never create, re-password or turn on anyone.
- `reset-admin` never takes the password as a command-line argument.
- Rows the system writes have `ActorId == null`, and the API names their actor `ระบบ`.
- New audit actions: `ADMIN_FIRST_CREATE`, `ADMIN_RECOVER_CREATE`, `ADMIN_RECOVER_RESET`, all in category `Account`.
- MySQL: only throwaway databases named `facility_*_test`; never `facility_dashboard`. Steps that need MySQL are run by the controller, never by a subagent.
- Screen words follow facility-0085 (แอดมิน, เซิร์ฟเวอร์ …). Thai strings in this plan are copied verbatim.
- Every commit passes the husky pre-commit (frontend lint, tsc, tests). Backend tests: `dotnet test backend/FacilityRealtime.slnx`.
- Never push, open a PR or merge without the owner's explicit OK.

## ADR decision → task

| ADR facility-0092 | Task |
|---|---|
| 1. Settings `FirstAdmin__*` as environment variables, never in `appsettings.json` or git | 2 (settings class, no appsettings entry), 4 (guide) |
| 2. Create only while no Admin row exists at all; otherwise no effect | 2 |
| 3. Same rules as D6; a bad value stops the startup and names the setting, without the password | 2 |
| 4. No Admin and no settings: start, and warn with the variable names | 2 |
| 5. `reset-admin <username>`: same program and connection string, exits without serving; password twice, hidden, never an argument; three cases; tokens revoked; LoginThrottle note | 3 |
| 6. Audit both, no password, actor is the system | 1 (actor "ระบบ"), 2, 3 |
| 7. Remove `FirstAdmin__Password` afterwards; change the password on D6 | 2 (warning while it is still set), 3 (reminder printed), 4 (guide) |
| 8. Thai handover guide, 2 active Admins advised | 4 |
| Consequences: step after migrate in every environment but Testing; `reset-admin` needs no `Jwt:SigningKey`; the test list; the history takes a system actor; deploy-guide order | 1, 2, 3, 4, 5 |

## Decisions made while planning (the owner can overrule them)

1. **The actor is "ระบบ"** (owner, 2026-10-27, by popup). `audit_log.actor_id` becomes nullable (migration `SystemAuditActor`). D9 shows `ระบบ`. The "ผู้ทำ" filter does not list the system; the "บัญชี" category finds these rows.
2. **Settings only partly set** (for example a username but no password) count as a bad value: startup stops and names the first missing setting. This follows the ADR's "set wrong, know at once" (decision 3).
3. **An Admin exists but `FirstAdmin__Password` is still set:** startup logs a warning to remove it. This supports decision 7 and changes nothing.
4. **Startup log lines are in English**, like the existing startup errors (`AuthSetup`, the attendance check in `Program.cs`). The rule text inside them comes from `AccountRules`, so it is Thai. `reset-admin` talks to a person, so its output is Thai.

## File Structure

- `backend/src/FacilityRealtime.Domain/Entities/AuditEntry.cs`: `ActorId` becomes `int?`.
- `backend/src/FacilityRealtime.Infrastructure/Persistence/AuditTrail.cs`: `actorId` parameter becomes `int?`.
- `backend/src/FacilityRealtime.Infrastructure/Persistence/AuditLogQuery.cs`: a null actor reads as `ระบบ` and is left out of the actor list.
- `backend/src/FacilityRealtime.Infrastructure/Migrations/<timestamp>_SystemAuditActor.cs` (+ Designer, snapshot): generated.
- `backend/src/FacilityRealtime.Api/DTOs/AuditLogDtos.cs`: `ActorId` becomes `int?`.
- `backend/src/FacilityRealtime.Application/Audit/AuditCategories.cs`: the three new actions in `Account`.
- `frontend/src/pages/history/historyApi.ts`, `historyRules.ts`: optional `actorId`, Thai labels for the new actions.
- Create `backend/src/FacilityRealtime.Application/Accounts/FirstAdminSettings.cs`: the bound settings.
- Create `backend/src/FacilityRealtime.Infrastructure/Persistence/ServerAdminAccounts.cs`: create, find and reset with the system as the actor.
- Create `backend/src/FacilityRealtime.Infrastructure/Persistence/FirstAdmin.cs`: the startup step.
- Create `backend/src/FacilityRealtime.Api/Commands/AdminConsole.cs`: `IAdminConsole` and the real console with hidden typing.
- Create `backend/src/FacilityRealtime.Api/Commands/ResetAdminCommand.cs`: the command's dialogue.
- Modify `backend/src/FacilityRealtime.Api/Program.cs`: the `reset-admin` branch, the startup step, one shared connection-string function.
- Create `backend/tests/FacilityRealtime.ApiTests/Infrastructure/EmptyDatabase.cs` and `ListLogger.cs`: test helpers.
- Create `docs/handover.md`: the Thai guide. Modify `README.md`, `CONTEXT.md`, `docs/architecture.md`, the ADR (amendment) and `docs/handoff.md`.

---

### Task 0: Branch, ADR and plan (controller)

- [ ] **Step 1:** The branch is `feat/first-admin`, already created from master `04bbc14`. Commit the ADR and this plan, and nothing else (the owner's `.gitignore` change and the root `package-lock.json` stay uncommitted):

```bash
git add docs/adr/facility-0092-first-admin-from-server-settings.md docs/superpowers/plans/2026-10-27-first-admin-from-server-settings.md docs/handoff.md
git commit -m "docs: ADR facility-0092 and its plan — first Admin from server settings, reset-admin"
```

### Task 1: The Admin history shows "ระบบ" as an actor

**Files:**
- Modify: `backend/src/FacilityRealtime.Domain/Entities/AuditEntry.cs:7`
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/AuditTrail.cs:11`
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/AuditLogQuery.cs`
- Modify: `backend/src/FacilityRealtime.Api/DTOs/AuditLogDtos.cs:13`
- Modify: `backend/src/FacilityRealtime.Application/Audit/AuditCategories.cs`
- Create (generated): `backend/src/FacilityRealtime.Infrastructure/Migrations/*_SystemAuditActor.cs`, `*_SystemAuditActor.Designer.cs`; modify `AppDbContextModelSnapshot.cs`
- Modify: `frontend/src/pages/history/historyApi.ts`, `frontend/src/pages/history/historyRules.ts`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AuditLogEndpointTests.cs`, `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceModels.cs:29`, `backend/tests/FacilityRealtime.UnitTests/AuditCategoriesTests.cs`, `frontend/src/pages/history/historyRules.test.ts`

**Interfaces:**
- Produces:
  - `AuditEntry.ActorId : int?`.
  - `AuditTrail.Add(AppDbContext db, int? actorId, DateTime occurredAt, string action, string entityType, long? entityId, string summary, object? before, object? after, string? reason = null)`.
  - `AuditLogQuery.SystemActorName = "ระบบ"`.
  - Action names `ADMIN_FIRST_CREATE`, `ADMIN_RECOVER_CREATE`, `ADMIN_RECOVER_RESET`.

- [ ] **Step 1: Write the failing tests**

`AdminAttendanceModels.cs:28-29`: change `int ActorId` to `int? ActorId` in `AuditLogEntryModel`. The test reads JSON in which a system row has no `actorId`.

Add to `AuditLogEndpointTests` (inside the class):

```csharp
    /// <summary>facility-0092: the first Admin and reset-admin are written by the server itself, with no Admin logged in.</summary>
    [Fact]
    public async Task A_row_the_system_wrote_shows_the_system_as_its_actor()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            AuditTrail.Add(db, actorId: null, ThaiClock.At(8, 9, 0).UtcDateTime, "ADMIN_FIRST_CREATE", "users", 1,
                "สร้างแอดมินคนแรก admin ผู้ดูแลระบบ จากค่าในเซิร์ฟเวอร์", before: null, after: new { Role = "Admin" });
            await db.SaveChangesAsync();
        });
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var entry = Assert.Single((await PageAsync(admin)).Entries);

        Assert.Equal(("ระบบ", (int?)null, "Account"), (entry.ActorName, entry.ActorId, entry.Category));
        Assert.Empty((await admin.GetFromJsonAsync<List<AuditActorModel>>(Path + "/actors"))!);
    }
```

Add to `AuditCategoriesTests.Every_action_written_so_far_has_its_category`, after the `ACCOUNT_DEACTIVATE` line:

```csharp
    [InlineData("ADMIN_FIRST_CREATE", AuditCategory.Account)]
    [InlineData("ADMIN_RECOVER_CREATE", AuditCategory.Account)]
    [InlineData("ADMIN_RECOVER_RESET", AuditCategory.Account)]
```

Add to `frontend/src/pages/history/historyRules.test.ts` (inside the `describe`):

```ts
  it('names what the server did by itself, and hides the password it reset (facility-0092)', () => {
    expect(actionText('ADMIN_FIRST_CREATE')).toBe('สร้างแอดมินคนแรกจากค่าในเซิร์ฟเวอร์');
    expect(actionText('ADMIN_RECOVER_CREATE')).toBe('สร้างแอดมินด้วยคำสั่ง reset-admin');
    expect(actionText('ADMIN_RECOVER_RESET')).toBe('กู้คืนแอดมินด้วยคำสั่ง reset-admin');
    expect(changeLines({ action: 'ADMIN_RECOVER_RESET' })).toEqual(['รหัสผ่านเดิม → รหัสผ่านใหม่ (ไม่แสดงรหัสผ่าน)']);
  });
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~AuditLogEndpointTests|FullyQualifiedName~AuditCategoriesTests"`
Expected: compile error, because `AuditTrail.Add` does not take `null` for `actorId`. Then, once it compiles, the new cases fail.

Run: `cd frontend && npx vitest run src/pages/history/historyRules.test.ts`
Expected: the new `it` fails (`expected 'ADMIN_FIRST_CREATE' to be 'สร้างแอดมินคนแรกจากค่าในเซิร์ฟเวอร์'`).

- [ ] **Step 3: Make the actor nullable**

`AuditEntry.cs`: replace `public int ActorId { get; set; }` with

```csharp
    /// <summary>The Admin who made the change; null when the server did it itself, shown as "ระบบ" (facility-0092).</summary>
    public int? ActorId { get; set; }
```

`AuditTrail.cs`: change the parameter `int actorId` to `int? actorId`. The summary comment becomes `/// <summary>facility-0051: every Admin change is logged — who, when, what, before and after; who is null when the server did it (facility-0092). Save it with the change itself.</summary>`.

`AppDbContext.cs:220` stays as it is: `HasForeignKey(x => x.ActorId)` on an `int?` makes the relationship optional.

`AuditLogQuery.cs`: add the constant inside the class, and replace the name lookups:

```csharp
    /// <summary>facility-0092: the actor of a row the server wrote by itself (the first Admin, reset-admin).</summary>
    public const string SystemActorName = "ระบบ";
```

In `LoadAsync`, replace the last four lines (from `var actorIds` to the `return`) with

```csharp
        var actorIds = entries.Where(a => a.ActorId != null).Select(a => a.ActorId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        return entries.Select(a => new AuditLogRow(a, a.ActorId is int id ? names[id] : SystemActorName)).ToList();
```

In `ActorsAsync`, replace the first line with

```csharp
        // The system is no account, so the "ผู้ทำ" filter does not offer it; the "บัญชี" category finds its rows
        var ids = await db.AuditLog.Where(a => a.ActorId != null).Select(a => a.ActorId!.Value).Distinct().ToListAsync();
```

`AuditLogDtos.cs:13`: `int ActorId,` becomes `int? ActorId,`. The JSON leaves out a null, as it does for every field.

`AuditCategories.cs`: the `Account` list becomes

```csharp
        [AuditCategory.Account] =
        [
            "ACCOUNT_CREATE", "ACCOUNT_UPDATE", "ACCOUNT_PHONE", "ACCOUNT_PASSWORD", "ACCOUNT_ACTIVATE", "ACCOUNT_DEACTIVATE",
            "ADMIN_FIRST_CREATE", "ADMIN_RECOVER_CREATE", "ADMIN_RECOVER_RESET",
        ],
```

- [ ] **Step 4: Generate the migration**

From the repo root:

```bash
dotnet tool restore
dotnet ef migrations add SystemAuditActor --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api
```

Expected: a migration whose `Up` alters `audit_log.actor_id` to `nullable: true`, and whose `Down` alters it back. EF may drop and re-add the foreign key `FK_audit_log_users_actor_id` around the change. Read the generated file. It must touch only `audit_log.actor_id` and its foreign key; if it touches anything else, stop and report. Add this comment as the first line inside `Up`:

```csharp
            // facility-0092: a row the server writes by itself (the first Admin, reset-admin) has no Admin as its actor
```

- [ ] **Step 5: The D9 page**

`frontend/src/pages/history/historyApi.ts`: in `AuditEntry`, replace `actorId: number;` with

```ts
  /** Missing on a row the server wrote by itself; actorName is then ระบบ (facility-0092). */
  actorId?: number;
```

`frontend/src/pages/history/historyRules.ts`:
- In `actionLabel`, after `ACCOUNT_DEACTIVATE: 'ปิดใช้บัญชี',` add
  ```ts
    ADMIN_FIRST_CREATE: 'สร้างแอดมินคนแรกจากค่าในเซิร์ฟเวอร์',
    ADMIN_RECOVER_CREATE: 'สร้างแอดมินด้วยคำสั่ง reset-admin',
    ADMIN_RECOVER_RESET: 'กู้คืนแอดมินด้วยคำสั่ง reset-admin',
  ```
- In `secretChange`, after the `ACCOUNT_PASSWORD` line add
  ```ts
    ADMIN_RECOVER_RESET: 'รหัสผ่านเดิม → รหัสผ่านใหม่ (ไม่แสดงรหัสผ่าน)',
  ```

- [ ] **Step 6: Run the tests**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: all pass. `MigrationTests.Migrations_match_the_model` passes because the migration exists. Every existing `Assert.Equal(adminAuth.User.Id, e.ActorId)` still compiles (`int` widens to `int?`).

Run: `cd frontend && npx vitest run && npx tsc -b && npx eslint .`
Expected: all pass, clean.

- [ ] **Step 7: Commit**

```bash
git add backend/src backend/tests frontend/src/pages/history
git commit -m "feat: the Admin history shows ระบบ for rows the server writes by itself (facility-0092)"
```

### Task 2: The first Admin at startup

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Accounts/FirstAdminSettings.cs`
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/ServerAdminAccounts.cs`
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/FirstAdmin.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs` (the migrate-and-seed block, "6.")
- Create: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/EmptyDatabase.cs`, `backend/tests/FacilityRealtime.ApiTests/Infrastructure/ListLogger.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Persistence/FirstAdminTests.cs`

**Interfaces:**
- Consumes: `AuditTrail.Add(..., int? actorId, ...)` and the action names from Task 1.
- Produces:
  - `FirstAdminSettings { const string SectionName = "FirstAdmin"; string? Username; string? Password; string? DisplayName; }` (namespace `FacilityRealtime.Application.Accounts`).
  - `ServerAdminAccounts.FirstCreateAction / RecoverCreateAction / RecoverResetAction` (string constants).
  - `ServerAdminAccounts.CreateAsync(AppDbContext db, IPasswordHasher hasher, TimeProvider clock, string username, string displayName, string password, string action, string summary) : Task<User>`.
  - `FirstAdmin.EnsureAsync(AppDbContext db, IPasswordHasher hasher, TimeProvider clock, FirstAdminSettings settings, ILogger logger) : Task<FirstAdminOutcome>`.
  - `enum FirstAdminOutcome { Created, AdminExists, NotConfigured }`.
  - Test helpers `EmptyDatabase` (`static Task<EmptyDatabase> CreateAsync()`, `AppDbContext Db`, `IAsyncDisposable`) and `ListLogger` (`List<(LogLevel Level, string Message)> Entries`).

- [ ] **Step 1: Write the test helpers**

`backend/tests/FacilityRealtime.ApiTests/Infrastructure/EmptyDatabase.cs`:

```csharp
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>An in-memory SQLite database with the schema and no seed: a server's first start (facility-0092).</summary>
public sealed class EmptyDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    private EmptyDatabase()
    {
        _connection.Open();
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
    }

    public AppDbContext Db { get; }

    public static async Task<EmptyDatabase> CreateAsync()
    {
        var empty = new EmptyDatabase();
        await empty.Db.Database.EnsureCreatedAsync();
        return empty;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
```

`backend/tests/FacilityRealtime.ApiTests/Infrastructure/ListLogger.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>Keeps every line logged, so a test can read what a server operator would see.</summary>
public sealed class ListLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
```

- [ ] **Step 2: Write the failing tests**

`backend/tests/FacilityRealtime.ApiTests/Persistence/FirstAdminTests.cs`:

```csharp
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Accounts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>facility-0092: the first Admin comes from the server's settings, only while no Admin exists at all.</summary>
public class FirstAdminTests
{
    private const string Password = "first-admin-pass-2026";
    private static readonly Pbkdf2PasswordHasher Hasher = new(iterations: 1_000);
    private static readonly TestTimeProvider Clock = new(ThaiClock.At(27, 9, 0));

    private static FirstAdminSettings Settings(string? username = "first.admin", string? password = Password, string? displayName = "แอดมินคนแรก") =>
        new() { Username = username, Password = password, DisplayName = displayName };

    [Fact]
    public async Task Creates_one_active_Admin_when_none_exists_and_logs_it_as_the_system()
    {
        await using var empty = await EmptyDatabase.CreateAsync();
        var logger = new ListLogger();

        var outcome = await FirstAdmin.EnsureAsync(empty.Db, Hasher, Clock, Settings(), logger);

        Assert.Equal(FirstAdminOutcome.Created, outcome);
        var admin = await empty.Db.Users.SingleAsync();
        Assert.Equal((UserRole.Admin, "first.admin", "แอดมินคนแรก", true), (admin.Role, admin.Username, admin.DisplayName, admin.IsActive));
        Assert.True(Hasher.Verify(Password, admin.SecretHash));
        var entry = await empty.Db.AuditLog.SingleAsync();
        Assert.Equal(("ADMIN_FIRST_CREATE", (int?)null, (long?)admin.Id), (entry.Action, entry.ActorId, entry.EntityId));
        Assert.DoesNotContain(Password, entry.Summary + entry.AfterJson);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains(Password));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("Created the first Admin 'first.admin'"));
    }

    [Fact]
    public async Task Does_nothing_while_any_Admin_exists_even_a_turned_off_one()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var seeded = await db.Users.SingleAsync(u => u.Username == "admin");
            seeded.IsActive = false;
            await db.SaveChangesAsync();
            var hashBefore = seeded.SecretHash;
            var logger = new ListLogger();

            var outcome = await FirstAdmin.EnsureAsync(db, Hasher, Clock, Settings(username: "admin"), logger);

            Assert.Equal(FirstAdminOutcome.AdminExists, outcome);
            var admin = await db.Users.SingleAsync(u => u.Role == UserRole.Admin);
            Assert.Equal((false, hashBefore), (admin.IsActive, admin.SecretHash));
            Assert.Empty(await db.AuditLog.ToListAsync());
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("FirstAdmin__Password is still set"));
        });
    }

    [Fact]
    public async Task With_no_Admin_and_no_settings_it_starts_and_warns_which_settings_to_set()
    {
        await using var empty = await EmptyDatabase.CreateAsync();
        var logger = new ListLogger();

        var outcome = await FirstAdmin.EnsureAsync(empty.Db, Hasher, Clock, new FirstAdminSettings(), logger);

        Assert.Equal(FirstAdminOutcome.NotConfigured, outcome);
        Assert.Empty(await empty.Db.Users.ToListAsync());
        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("FirstAdmin__Username, FirstAdmin__Password and FirstAdmin__DisplayName", warning.Message);
    }

    [Theory]
    [InlineData("short-pass")]
    [InlineData("1qaz2wsx3edc4rfv")]
    [InlineData(null)]
    public async Task A_password_against_the_rule_stops_the_startup_without_printing_it(string? password)
    {
        await using var empty = await EmptyDatabase.CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FirstAdmin.EnsureAsync(empty.Db, Hasher, Clock, Settings(password: password), new ListLogger()));

        Assert.StartsWith("FirstAdmin__Password", error.Message);
        if (password is not null)
        {
            Assert.DoesNotContain(password, error.Message);
        }

        Assert.Empty(await empty.Db.Users.ToListAsync());
    }

    [Theory]
    [InlineData(null, "แอดมินคนแรก", "FirstAdmin__Username")]
    [InlineData("a b", "แอดมินคนแรก", "FirstAdmin__Username")]
    [InlineData("first.admin", null, "FirstAdmin__DisplayName")]
    [InlineData("first.admin", "   ", "FirstAdmin__DisplayName")]
    public async Task A_missing_or_bad_username_or_display_name_stops_the_startup(string? username, string? displayName, string named)
    {
        await using var empty = await EmptyDatabase.CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FirstAdmin.EnsureAsync(empty.Db, Hasher, Clock, Settings(username: username, displayName: displayName), new ListLogger()));

        Assert.StartsWith(named, error.Message);
        Assert.DoesNotContain(Password, error.Message);
        Assert.Empty(await empty.Db.Users.ToListAsync());
    }
}
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~FirstAdminTests"`
Expected: compile error, because `FirstAdmin`, `FirstAdminSettings` and `FirstAdminOutcome` do not exist.

- [ ] **Step 4: Write the settings, the shared create and the startup step**

`backend/src/FacilityRealtime.Application/Accounts/FirstAdminSettings.cs`:

```csharp
namespace FacilityRealtime.Application.Accounts;

/// <summary>
/// facility-0092 decision 1: the server's environment variables FirstAdmin__Username, FirstAdmin__Password and
/// FirstAdmin__DisplayName. Never in appsettings.json or git; the password is removed once the Admin exists.
/// </summary>
public sealed class FirstAdminSettings
{
    public const string SectionName = "FirstAdmin";

    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? DisplayName { get; set; }
}
```

`backend/src/FacilityRealtime.Infrastructure/Persistence/ServerAdminAccounts.cs`:

```csharp
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>
/// facility-0092: Admin accounts made or recovered on the server itself, with no Admin logged in, so the audit row's
/// actor is null ("ระบบ"). Callers check the username, display name and password with AccountRules first.
/// </summary>
public static class ServerAdminAccounts
{
    public const string FirstCreateAction = "ADMIN_FIRST_CREATE";
    public const string RecoverCreateAction = "ADMIN_RECOVER_CREATE";
    public const string RecoverResetAction = "ADMIN_RECOVER_RESET";

    /// <summary>A new active Admin and its audit row, saved in one transaction. The row's snapshot has no password.</summary>
    public static async Task<User> CreateAsync(
        AppDbContext db, IPasswordHasher hasher, TimeProvider clock, string username, string displayName, string password, string action, string summary)
    {
        var admin = new User
        {
            Role = UserRole.Admin,
            Username = username,
            DisplayName = displayName,
            SecretHash = hasher.Hash(password),
            IsActive = true,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Users.Add(admin);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        // The audit row needs the new id, so it is a second save inside the same transaction
        AuditTrail.Add(
            db, actorId: null, admin.CreatedAt, action, "users", admin.Id, summary,
            before: null, after: new { Role = admin.Role.ToString(), admin.LoginName, admin.DisplayName, admin.IsActive });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return admin;
    }
}
```

`backend/src/FacilityRealtime.Infrastructure/Persistence/FirstAdmin.cs`:

```csharp
using FacilityRealtime.Application.Accounts;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FacilityRealtime.Infrastructure.Persistence;

public enum FirstAdminOutcome
{
    Created,
    AdminExists,
    NotConfigured,
}

/// <summary>
/// facility-0092 decisions 2–4, run at startup after the migration (and the seed, on a developer machine): the server's
/// FirstAdmin settings create one Admin, only while no Admin exists at all. A bad value stops the startup and names
/// the setting; the password itself never reaches the log or the error.
/// </summary>
public static class FirstAdmin
{
    public static async Task<FirstAdminOutcome> EnsureAsync(
        AppDbContext db, IPasswordHasher hasher, TimeProvider clock, FirstAdminSettings settings, ILogger logger)
    {
        // Any Admin row, turned off or not: the settings never create another, change a password or turn anyone on
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            if (!string.IsNullOrEmpty(settings.Password))
            {
                logger.LogWarning("FirstAdmin__Password is still set, but an Admin already exists, so it is ignored. Remove it from the server.");
            }

            return FirstAdminOutcome.AdminExists;
        }

        if (string.IsNullOrWhiteSpace(settings.Username) && string.IsNullOrEmpty(settings.Password) && string.IsNullOrWhiteSpace(settings.DisplayName))
        {
            logger.LogWarning(
                "No Admin account exists. Set FirstAdmin__Username, FirstAdmin__Password and FirstAdmin__DisplayName on the server, then restart (docs/handover.md).");
            return FirstAdminOutcome.NotConfigured;
        }

        var username = AccountRules.NormalizeUsername(settings.Username)
            ?? throw new InvalidOperationException("FirstAdmin__Username: 3–100 English letters, digits, '.', '_' or '-'.");
        var displayName = AccountRules.NormalizeDisplayName(settings.DisplayName)
            ?? throw new InvalidOperationException($"FirstAdmin__DisplayName: 1–{AccountRules.MaxDisplayName} characters.");
        if (AccountRules.PasswordProblem(settings.Password) is { } problem)
        {
            // The rule's words only, never the value
            throw new InvalidOperationException($"FirstAdmin__Password: {problem}");
        }

        await ServerAdminAccounts.CreateAsync(
            db, hasher, clock, username, displayName, settings.Password!, ServerAdminAccounts.FirstCreateAction,
            $"สร้างแอดมินคนแรก {username} {displayName} จากค่าในเซิร์ฟเวอร์");
        logger.LogInformation(
            "Created the first Admin '{Username}'. Remove FirstAdmin__Password from the server, log in and change the password on the accounts page.",
            username);
        return FirstAdminOutcome.Created;
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~FirstAdminTests"`
Expected: all PASS (10 cases).

- [ ] **Step 6: Wire it into startup**

`Program.cs`: add `using FacilityRealtime.Application.Accounts;` to the usings. In block "6.", after the `if (app.Environment.IsDevelopment()) { ... SeedAsync ... }` statement and still inside `if (!app.Environment.IsEnvironment("Testing")) { ... }`, add

```csharp
    // facility-0092: the first Admin comes from the server's settings (FirstAdmin__*), only while no Admin exists at all.
    // After the seed, so a developer machine keeps the seed's Admin and nothing happens there.
    var firstAdmin = app.Configuration.GetSection(FirstAdminSettings.SectionName).Get<FirstAdminSettings>() ?? new FirstAdminSettings();
    await FirstAdmin.EnsureAsync(
        db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), scope.ServiceProvider.GetRequiredService<TimeProvider>(), firstAdmin, app.Logger);
```

Change the block's comment line to `// 6. Migrate on startup. Seed only on a developer machine: the seed's known Admin password must never reach a server on the internet (facility-0049). Then the first Admin from the server's settings (facility-0092).`

No automated test runs this wiring: the API tests run in "Testing", which skips the block. Task 5's real run on MySQL covers it.

- [ ] **Step 7: Run everything and commit**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: all pass (MySQL tests skipped).

```bash
git add backend/src backend/tests
git commit -m "feat: the first Admin comes from the server's settings while no Admin exists (facility-0092)"
```

### Task 3: The `reset-admin` command

**Files:**
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/ServerAdminAccounts.cs` (add the find and reset methods and the `RecoveryTarget` types)
- Create: `backend/src/FacilityRealtime.Api/Commands/AdminConsole.cs`
- Create: `backend/src/FacilityRealtime.Api/Commands/ResetAdminCommand.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/ResetAdminCommandTests.cs`

**Interfaces:**
- Consumes: `ServerAdminAccounts.CreateAsync`, `ServerAdminAccounts.RecoverCreateAction`, `ServerAdminAccounts.RecoverResetAction` (Task 2); `AuditTrail.Add` with `actorId: null` (Task 1).
- Produces:
  - `RecoveryTarget` (abstract record) with nested `RecoveryTarget.Admin(User Account)`, `RecoveryTarget.Nobody()`, `RecoveryTarget.Staff(UserRole Role)`.
  - `ServerAdminAccounts.FindAsync(AppDbContext db, string username) : Task<RecoveryTarget>`.
  - `ServerAdminAccounts.ResetAsync(AppDbContext db, IPasswordHasher hasher, TimeProvider clock, User admin, string password) : Task`.
  - `IAdminConsole { string? ReadLine(string prompt); string? ReadSecret(string prompt); void WriteLine(string text); }` and `SystemAdminConsole : IAdminConsole`.
  - `ResetAdminCommand.Name = "reset-admin"`.
  - `ResetAdminCommand.RunAsync(string[] args, AppDbContext db, IPasswordHasher hasher, TimeProvider clock, IAdminConsole console) : Task<int>`. `args` is what follows `reset-admin`. Exit code 0 done, 1 refused, 2 wrong usage.

- [ ] **Step 1: Write the failing tests**

`backend/tests/FacilityRealtime.ApiTests/Admin/ResetAdminCommandTests.cs`:

```csharp
using System.Net;
using FacilityRealtime.Api.Commands;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0092 decision 5: reset-admin recovers Admin access from the server's console.</summary>
public class ResetAdminCommandTests
{
    private const string NewPassword = "recovered-pass-2026";

    /// <summary>Answers the prompts in order, and keeps what was written and which prompts hid the typing.</summary>
    private sealed class FakeConsole(params string?[] answers) : IAdminConsole
    {
        private readonly Queue<string?> _answers = new(answers);

        public List<string> Written { get; } = [];
        public List<string> ShownPrompts { get; } = [];
        public List<string> HiddenPrompts { get; } = [];

        public string? ReadLine(string prompt)
        {
            ShownPrompts.Add(prompt);
            return Next();
        }

        public string? ReadSecret(string prompt)
        {
            HiddenPrompts.Add(prompt);
            return Next();
        }

        public void WriteLine(string text) => Written.Add(text);

        private string? Next() => _answers.Count > 0 ? _answers.Dequeue() : null;
    }

    private static async Task<int> RunAsync(FacilityApiFactory factory, FakeConsole console, params string[] args)
    {
        var exitCode = -1;
        await factory.WithDbAsync(async db => exitCode = await ResetAdminCommand.RunAsync(
            args, db, factory.Services.GetRequiredService<IPasswordHasher>(), factory.Clock, console));
        return exitCode;
    }

    [Fact]
    public async Task Resets_a_turned_off_Admin_turns_it_on_and_ends_every_session()
    {
        using var factory = new FacilityApiFactory();
        await AuthApi.LoggedInAdminAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Username == "admin")).IsActive = false;
            await db.SaveChangesAsync();
        });
        var console = new FakeConsole(NewPassword, NewPassword);

        var exitCode = await RunAsync(factory, console, "ADMIN");

        Assert.Equal(0, exitCode);
        Assert.Equal(2, console.HiddenPrompts.Count);
        Assert.Empty(console.ShownPrompts);
        Assert.DoesNotContain(console.Written, line => line.Contains(NewPassword));
        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            Assert.True(admin.IsActive);
            var tokens = await db.RefreshTokens.Where(t => t.UserId == admin.Id).ToListAsync();
            Assert.NotEmpty(tokens);
            Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
            var entry = await db.AuditLog.SingleAsync();
            Assert.Equal(("ADMIN_RECOVER_RESET", (int?)null, (long?)admin.Id), (entry.Action, entry.ActorId, entry.EntityId));
            Assert.Contains("และเปิดใช้บัญชี", entry.Summary);
            Assert.DoesNotContain(NewPassword, entry.Summary);
        });
        var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.AdminLoginAsync(client, "admin", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Creates_a_new_Admin_for_a_name_nobody_has()
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole("แอดมินสำรอง", NewPassword, NewPassword);

        var exitCode = await RunAsync(factory, console, "backup.admin");

        Assert.Equal(0, exitCode);
        Assert.Single(console.ShownPrompts);
        Assert.Equal(2, console.HiddenPrompts.Count);
        await factory.WithDbAsync(async db =>
        {
            var created = await db.Users.SingleAsync(u => u.Username == "backup.admin");
            Assert.Equal((UserRole.Admin, "แอดมินสำรอง", true), (created.Role, created.DisplayName, created.IsActive));
            var entry = await db.AuditLog.SingleAsync();
            Assert.Equal(("ADMIN_RECOVER_CREATE", (int?)null, (long?)created.Id), (entry.Action, entry.ActorId, entry.EntityId));
        });
    }

    [Fact]
    public async Task Refuses_a_Cleaners_employee_ID_and_changes_nothing()
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole(NewPassword, NewPassword);

        var exitCode = await RunAsync(factory, console, "e1001");

        Assert.Equal(1, exitCode);
        Assert.Empty(console.HiddenPrompts);
        Assert.Contains(console.Written, line => line.Contains("รหัสพนักงานของแม่บ้าน"));
        await factory.WithDbAsync(async db =>
        {
            Assert.False(await db.Users.AnyAsync(u => u.Username == "e1001"));
            Assert.Empty(await db.AuditLog.ToListAsync());
        });
    }

    [Fact]
    public async Task Two_different_passwords_change_nothing()
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole(NewPassword, NewPassword + "x");

        var exitCode = await RunAsync(factory, console, "admin");

        Assert.Equal(1, exitCode);
        Assert.Contains(console.Written, line => line.Contains("รหัสผ่านสองครั้งไม่ตรงกัน"));
        await AssertUnchangedAsync(factory);
    }

    [Theory]
    [InlineData("short-pass")]
    [InlineData("1qaz2wsx3edc4rfv")]
    public async Task A_password_against_the_rule_changes_nothing(string password)
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole(password, password);

        var exitCode = await RunAsync(factory, console, "admin");

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(console.Written, line => line.Contains(password));
        await AssertUnchangedAsync(factory);
    }

    [Fact]
    public async Task A_new_Admin_needs_a_display_name()
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole("   ", NewPassword, NewPassword);

        var exitCode = await RunAsync(factory, console, "backup.admin");

        Assert.Equal(1, exitCode);
        Assert.Empty(console.HiddenPrompts);
        await factory.WithDbAsync(async db => Assert.False(await db.Users.AnyAsync(u => u.Username == "backup.admin")));
    }

    public static TheoryData<int, string[]> WrongUsage => new()
    {
        { 2, [] },
        { 1, ["a b"] },
        { 2, ["admin", "extra"] },
    };

    [Theory]
    [MemberData(nameof(WrongUsage))]
    public async Task Wrong_usage_or_a_bad_username_changes_nothing(int expectedExitCode, string[] args)
    {
        using var factory = new FacilityApiFactory();
        var console = new FakeConsole(NewPassword, NewPassword);

        var exitCode = await RunAsync(factory, console, args);

        Assert.Equal(expectedExitCode, exitCode);
        Assert.Empty(console.HiddenPrompts);
        await AssertUnchangedAsync(factory);
    }

    /// <summary>The seeded Admin keeps the seed's password and nothing is logged.</summary>
    private static Task AssertUnchangedAsync(FacilityApiFactory factory) =>
        factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            Assert.True(factory.Services.GetRequiredService<IPasswordHasher>().Verify("admin1234", admin.SecretHash));
            Assert.Empty(await db.AuditLog.ToListAsync());
        });
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~ResetAdminCommandTests"`
Expected: compile error, because `FacilityRealtime.Api.Commands`, `IAdminConsole` and `ResetAdminCommand` do not exist.

- [ ] **Step 3: Find and reset**

Add to `ServerAdminAccounts.cs`. Add `using Microsoft.EntityFrameworkCore;` to its usings. Put the record type above the class:

```csharp
/// <summary>Who a username names for reset-admin (facility-0092 decision 5).</summary>
public abstract record RecoveryTarget
{
    /// <summary>An Admin with this username: reset its password and turn it on.</summary>
    public sealed record Admin(User Account) : RecoveryTarget;

    /// <summary>Nobody has it: create a new Admin.</summary>
    public sealed record Nobody : RecoveryTarget;

    /// <summary>A Cleaner's or Supervisor's employee ID: refuse, so their login name never doubles as an Admin's.</summary>
    public sealed record Staff(UserRole Role) : RecoveryTarget;
}
```

Add inside the class:

```csharp
    /// <summary>Only Admins have a username; employee IDs are checked too. Both in any letter case, like the accounts page.</summary>
    public static async Task<RecoveryTarget> FindAsync(AppDbContext db, string username)
    {
        var upper = username.ToUpperInvariant();
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Username != null && u.Username.ToUpper() == upper);
        if (admin is not null)
        {
            return new RecoveryTarget.Admin(admin);
        }

        var staff = await db.Users.FirstOrDefaultAsync(u => u.EmployeeId != null && u.EmployeeId.ToUpper() == upper);
        return staff is null ? new RecoveryTarget.Nobody() : new RecoveryTarget.Staff(staff.Role);
    }

    /// <summary>
    /// A new password, the account turned on, and every refresh token revoked (facility-0014), in one transaction. The
    /// audit row keeps no before or after, like a password reset on the accounts page (facility-0057).
    /// </summary>
    public static async Task ResetAsync(AppDbContext db, IPasswordHasher hasher, TimeProvider clock, User admin, string password)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var turnedOn = !admin.IsActive;
        admin.SecretHash = hasher.Hash(password);
        admin.IsActive = true;
        AuditTrail.Add(
            db, actorId: null, now, RecoverResetAction, "users", admin.Id,
            $"กู้คืนแอดมิน {admin.Username} {admin.DisplayName} ด้วยคำสั่ง reset-admin บนเซิร์ฟเวอร์: ตั้งรหัสผ่านใหม่{(turnedOn ? " และเปิดใช้บัญชี" : "")}",
            before: null, after: null);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        await db.RevokeUserSessionsAsync(admin.Id, now);
        await transaction.CommitAsync();
    }
```

- [ ] **Step 4: The console and the command**

`backend/src/FacilityRealtime.Api/Commands/AdminConsole.cs`:

```csharp
using System.Text;

namespace FacilityRealtime.Api.Commands;

/// <summary>What reset-admin asks and says; tests answer it without a terminal.</summary>
public interface IAdminConsole
{
    string? ReadLine(string prompt);

    /// <summary>Typing is not shown on the screen (facility-0092 decision 5).</summary>
    string? ReadSecret(string prompt);

    void WriteLine(string text);
}

/// <summary>The server's terminal. Piped input (a script) is read as lines; nothing is echoed either way.</summary>
public sealed class SystemAdminConsole : IAdminConsole
{
    public string? ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine();
    }

    public string? ReadSecret(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine();
        }

        var typed = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return typed.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                {
                    typed.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                typed.Append(key.KeyChar);
            }
        }
    }

    public void WriteLine(string text) => Console.WriteLine(text);
}
```

`backend/src/FacilityRealtime.Api/Commands/ResetAdminCommand.cs`:

```csharp
using FacilityRealtime.Api.Endpoints;
using FacilityRealtime.Application.Accounts;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Commands;

/// <summary>
/// facility-0092 decision 5: `reset-admin &lt;username&gt;`, run on the server with the API's own settings, recovers
/// Admin access when no Admin can log in. The password is asked twice with the typing hidden, never taken as an
/// argument, because a shell keeps its history. Exit code 0 done, 1 refused, 2 wrong usage.
/// </summary>
public static class ResetAdminCommand
{
    public const string Name = "reset-admin";

    /// <param name="args">What follows "reset-admin" on the command line.</param>
    public static async Task<int> RunAsync(string[] args, AppDbContext db, IPasswordHasher hasher, TimeProvider clock, IAdminConsole console)
    {
        if (args.Length != 1)
        {
            console.WriteLine($"วิธีใช้: {Name} <ชื่อผู้ใช้แอดมิน>");
            return 2;
        }

        if (AccountRules.NormalizeUsername(args[0]) is not { } username)
        {
            console.WriteLine("ชื่อผู้ใช้ต้องเป็นตัวอักษรอังกฤษ ตัวเลข . _ หรือ - ยาว 3–100 ตัว ไม่ได้เปลี่ยนอะไร");
            return 1;
        }

        var target = await ServerAdminAccounts.FindAsync(db, username);
        if (target is RecoveryTarget.Staff staff)
        {
            console.WriteLine($"{username} เป็นรหัสพนักงานของ{ThaiLabels.RoleName(staff.Role)} ไม่ใช่แอดมิน ไม่ได้เปลี่ยนอะไร");
            return 1;
        }

        string? displayName = null;
        if (target is RecoveryTarget.Nobody)
        {
            console.WriteLine($"ยังไม่มีแอดมินชื่อ {username} จะสร้างบัญชีใหม่");
            displayName = AccountRules.NormalizeDisplayName(console.ReadLine("ชื่อที่แสดง: "));
            if (displayName is null)
            {
                console.WriteLine($"ต้องใส่ชื่อที่แสดง ไม่เกิน {AccountRules.MaxDisplayName} ตัวอักษร ไม่ได้เปลี่ยนอะไร");
                return 1;
            }
        }

        var password = console.ReadSecret("รหัสผ่านใหม่ (ไม่แสดงบนจอ): ");
        if (console.ReadSecret("พิมพ์รหัสผ่านใหม่อีกครั้ง: ") != password)
        {
            console.WriteLine("รหัสผ่านสองครั้งไม่ตรงกัน ไม่ได้เปลี่ยนอะไร");
            return 1;
        }

        if (AccountRules.PasswordProblem(password) is { } problem)
        {
            console.WriteLine($"{problem} ไม่ได้เปลี่ยนอะไร");
            return 1;
        }

        if (target is RecoveryTarget.Admin existing)
        {
            await ServerAdminAccounts.ResetAsync(db, hasher, clock, existing.Account, password!);
            console.WriteLine($"ตั้งรหัสผ่านใหม่ให้ {existing.Account.Username} แล้ว บัญชีเปิดใช้งาน และทุกเครื่องที่เคยเข้าระบบด้วยบัญชีนี้ต้องเข้าใหม่");
        }
        else
        {
            await ServerAdminAccounts.CreateAsync(
                db, hasher, clock, username, displayName!, password!, ServerAdminAccounts.RecoverCreateAction,
                $"สร้างแอดมิน {username} {displayName} ด้วยคำสั่ง reset-admin บนเซิร์ฟเวอร์");
            console.WriteLine($"สร้างแอดมิน {username} แล้ว");
        }

        // LoginThrottle lives in the running API's memory; this process cannot clear it (facility-0092 decision 5)
        console.WriteLine("ถ้าบัญชีเพิ่งถูกล็อกเพราะใส่รหัสผิด 5 ครั้ง ให้รอ 15 นาที หรือรีสตาร์ตระบบ");
        console.WriteLine("เข้าระบบแล้วเปลี่ยนรหัสผ่านของตัวเองที่หน้าบัญชีผู้ใช้ทันที");
        return 0;
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~ResetAdminCommandTests"`
Expected: all PASS (10 cases).

- [ ] **Step 6: Wire it into `Program.cs`**

Add `using System.Text;`, `using FacilityRealtime.Api.Commands;` and `using FacilityRealtime.Infrastructure.Auth;` to the usings, if not already there (`FacilityRealtime.Infrastructure.Auth` already is).

Right after `var builder = WebApplication.CreateBuilder(args);` add

```csharp
// facility-0092: `reset-admin <username>` recovers Admin access from the server's console, then exits without serving.
// It needs only the connection string, so it runs before the auth setup that asks for Jwt:SigningKey.
if (args.Length > 0 && args[0] == ResetAdminCommand.Name)
{
    Console.OutputEncoding = Encoding.UTF8;
    var options = new DbContextOptionsBuilder<AppDbContext>().UseMySQL(ConnectionString(builder.Configuration)).Options;
    await using var adminDb = new AppDbContext(options);
    await adminDb.Database.MigrateAsync();
    Environment.ExitCode = await ResetAdminCommand.RunAsync(args[1..], adminDb, new Pbkdf2PasswordHasher(), TimeProvider.System, new SystemAdminConsole());
    return;
}
```

Replace the database registration block

```csharp
if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=localhost;Port=3306;Database=facility_dashboard;Uid=root;Pwd=;CharSet=utf8mb4;";

    builder.Services.AddDbContext<AppDbContext>(options => options.UseMySQL(connectionString));
}
```

with

```csharp
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseMySQL(ConnectionString(builder.Configuration)));
}
```

Right after `app.Run();` (before `public partial class Program;`) add

```csharp
// One database for serving and for reset-admin (facility-0092)
static string ConnectionString(IConfiguration configuration) =>
    configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=facility_dashboard;Uid=root;Pwd=;CharSet=utf8mb4;";
```

The command-line configuration provider ignores `reset-admin` and the username, because they carry no `--` and no `=`. No automated test runs this branch; Task 5's real run covers it.

- [ ] **Step 7: Run everything and commit**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: all pass (MySQL tests skipped).

```bash
git add backend/src backend/tests
git commit -m "feat: reset-admin recovers Admin access from the server's console (facility-0092)"
```

### Task 4: The Thai handover guide and the docs

**Files:**
- Create: `docs/handover.md`
- Modify: `README.md` (append a section at the end)
- Modify: `CONTEXT.md` (the **Audit Log** entry)
- Modify: `docs/architecture.md` (one paragraph after the "Excel export" paragraph; the class diagram's actor line)
- Modify: `docs/adr/facility-0092-first-admin-from-server-settings.md` (append an amendment)

- [ ] **Step 1: Write `docs/handover.md`**

Write exactly this (Thai, for whoever takes the system on):

````markdown
# คู่มือส่งมอบระบบ

สำหรับคนที่รับช่วงดูแลระบบตรวจงานทำความสะอาดต่อจากเจ้าของโปรเจกต์ ทำตามทีละขั้นได้โดยไม่ต้องอ่านโค้ด แต่ต้องเข้า server ที่รันระบบได้ (พิมพ์คำสั่งใน terminal ของ server ได้)

การติดตั้ง server, https และค่าความลับอื่น (`Jwt:SigningKey`, `Jwt:RefreshCookieSecure`, ที่อยู่ฐานข้อมูล) จะเพิ่มในเอกสารนี้ในงานถัดไป (คู่มือ deploy)

## 1. ก่อนเริ่ม

- ระบบคือโปรแกรม `FacilityRealtime.Api` (ไฟล์ `FacilityRealtime.Api.dll` ในโฟลเดอร์ที่ deploy)
- ระบบอ่านค่าตั้งจาก environment variable ของ server ชื่อที่มี `__` (ขีดล่างสองตัว) เช่น `ConnectionStrings__DefaultConnection` คือที่อยู่ฐานข้อมูล
- ทุกคำสั่งในเอกสารนี้ต้องรันด้วยค่าชุดเดียวกับที่ระบบใช้จริง โดยเฉพาะ `ConnectionStrings__DefaultConnection` ไม่อย่างนั้นคำสั่งจะไปทำกับฐานข้อมูลอื่น

## 2. สร้างแอดมินคนแรก (ครั้งเดียว ตอน deploy ครั้งแรก)

ฐานข้อมูลใหม่ยังไม่มีบัญชีใดเลย และบัญชีทุกใบเพิ่มได้จากหน้า "บัญชีผู้ใช้" ซึ่งต้องเข้าระบบเป็นแอดมินก่อน แอดมินคนแรกจึงสร้างจากค่าใน server

1. ตั้ง environment variable 3 ตัวบน server
   - `FirstAdmin__Username` ชื่อผู้ใช้ ตัวอักษรอังกฤษ ตัวเลข `.` `_` หรือ `-` ยาว 3–100 ตัว
   - `FirstAdmin__Password` รหัสผ่าน ยาว 15–100 ตัว และไม่ใช่รหัสที่คนใช้กันเยอะ
   - `FirstAdmin__DisplayName` ชื่อที่แสดงในระบบ

   ห้ามเขียนค่าเหล่านี้ลงไฟล์ `appsettings.json` หรือเก็บใน git ตัวอย่างใน shell ของ Linux ก่อนเริ่มระบบ
   ```bash
   export FirstAdmin__Username='first.admin'
   export FirstAdmin__DisplayName='แอดมินคนแรก'
   read -rs FirstAdmin__Password && export FirstAdmin__Password
   ```
   `read -rs` ให้พิมพ์รหัสโดยไม่แสดงบนจอ และรหัสไม่ค้างใน history ของ shell
2. เริ่มระบบ log ต้องมีบรรทัด `Created the first Admin 'first.admin'`
3. เข้า `https://<ที่อยู่ระบบ>/login/admin` ด้วยชื่อผู้ใช้และรหัสผ่านนั้น
4. ลบ `FirstAdmin__Password` ออกจาก server แล้วรีสตาร์ตระบบ ถ้ายังตั้งค้างไว้ log จะเตือน `FirstAdmin__Password is still set`
5. ไปหน้า "บัญชีผู้ใช้" กด "รีเซ็ตรหัสผ่าน" ที่บัญชีของตัวเอง แล้วตั้งรหัสใหม่ที่มีแค่คุณรู้ เพราะรหัสเดิมเคยอยู่ในค่าตั้งของ server
6. เพิ่มแอดมินคนที่สองในหน้า "บัญชีผู้ใช้" (ดูข้อ 4)
7. เพิ่มตึกในหน้า "ตึก" แล้วจึงเพิ่มพื้นที่และบัญชีแม่บ้านกับหัวหน้า

ค่าทั้งสามมีผลเฉพาะตอนที่ยังไม่มีบัญชีแอดมินเลยสักคน นับคนที่ปิดใช้งานด้วย ถ้ามีแอดมินแล้ว ระบบไม่สร้างซ้ำ ไม่เปลี่ยนรหัส และไม่เปิดบัญชีที่ปิดไว้ ถ้าต้องการกู้คืน ใช้ข้อ 3

### ถ้าระบบไม่ยอมเริ่ม

ข้อความที่ขึ้นต้นด้วยชื่อค่าบอกว่าค่าไหนผิด ระบบไม่พิมพ์รหัสผ่านออกมา แก้ค่านั้นแล้วเริ่มใหม่

- `FirstAdmin__Username: ...` ชื่อผู้ใช้ผิดรูปแบบ หรือไม่ได้ตั้ง
- `FirstAdmin__DisplayName: ...` ไม่ได้ตั้งชื่อที่แสดง
- `FirstAdmin__Password: ...` รหัสสั้นหรือยาวเกิน เป็นรหัสที่คนใช้กันเยอะ หรือไม่ได้ตั้ง

ถ้ายังไม่มีแอดมินและไม่ได้ตั้งค่าทั้งสามเลย ระบบเริ่มได้ แต่ log เตือน `No Admin account exists` จนกว่าจะตั้งค่า

## 3. กู้คืนเมื่อไม่มีแอดมินคนไหนเข้าระบบได้ (`reset-admin`)

ใช้เมื่อแอดมินทุกคนลืมรหัส หรือถูกปิดใช้งานหมด ถ้ายังมีแอดมินอีกคนเข้าได้ ให้คนนั้นรีเซ็ตรหัสให้ในหน้า "บัญชีผู้ใช้" แทน

1. เข้า server ไปที่โฟลเดอร์ที่ deploy ระบบ แล้วตั้ง environment variable ชุดเดียวกับที่ระบบใช้ อย่างน้อย `ConnectionStrings__DefaultConnection`
2. รัน
   ```bash
   dotnet FacilityRealtime.Api.dll reset-admin <ชื่อผู้ใช้แอดมิน>
   ```
   ถ้ารันจากโค้ด ใช้ `dotnet run --project backend/src/FacilityRealtime.Api -- reset-admin <ชื่อผู้ใช้แอดมิน>`
3. คำสั่งถามรหัสผ่านใหม่สองครั้ง ตัวอักษรที่พิมพ์ไม่แสดงบนจอ คำสั่งไม่รับรหัสในบรรทัดคำสั่ง เพราะจะค้างใน history
   - ชื่อนั้นเป็นแอดมินอยู่แล้ว: ตั้งรหัสใหม่ เปิดบัญชีถ้าถูกปิด และทุกเครื่องที่เคยเข้าระบบด้วยบัญชีนี้ต้องเข้าใหม่
   - ยังไม่มีชื่อนี้: สร้างแอดมินใหม่ คำสั่งถามชื่อที่แสดงก่อน
   - ชื่อนี้เป็นรหัสพนักงานของแม่บ้านหรือหัวหน้า: ไม่ทำอะไร
4. คำสั่งจบด้วย exit code 0 เมื่อเสร็จ 1 เมื่อไม่ได้ทำ (ข้อความบอกเหตุผล) และ 2 เมื่อพิมพ์คำสั่งผิด
5. เข้าระบบแล้วเปลี่ยนรหัสของตัวเองที่หน้า "บัญชีผู้ใช้" ทันที

หมายเหตุ

- รันได้ขณะที่ระบบทำงานอยู่ ไม่ต้องหยุดระบบ
- ถ้าบัญชีเพิ่งถูกล็อกเพราะใส่รหัสผิด 5 ครั้ง การล็อกอยู่ในหน่วยความจำของระบบที่รันอยู่ ให้รอ 15 นาที หรือรีสตาร์ตระบบ
- หน้า "ประวัติการแก้ไข" มีแถวที่ผู้ทำคือ "ระบบ" ทุกครั้งที่สร้างหรือกู้คืนแอดมินด้วยวิธีในเอกสารนี้
- ใครเข้า server ได้ก็ใช้คำสั่งนี้ได้ จึงต้องจำกัดคนที่เข้า server ได้

## 4. ให้มีแอดมินที่เปิดใช้งานอย่างน้อย 2 คนเสมอ

ถ้าแอดมินคนหนึ่งลืมรหัสหรือลาออก อีกคนรีเซ็ตรหัสหรือปิดบัญชีให้ได้ในหน้า "บัญชีผู้ใช้" โดยไม่ต้องเข้า server คำสั่ง `reset-admin` จึงแทบไม่ต้องใช้
````

- [ ] **Step 2: README, CONTEXT, architecture**

`README.md`: append at the end of the file

```markdown

### คู่มือสำหรับ server จริง
- [docs/handover.md](docs/handover.md): สร้างแอดมินคนแรกจากค่าใน server และกู้คืนด้วยคำสั่ง `reset-admin` (ADR facility-0092)
```

`CONTEXT.md`: in the **Audit Log** entry, replace the line

`ประวัติทุกการเปลี่ยนแปลงที่ Admin ทำ: ใคร, เมื่อไร, เปลี่ยนอะไร, ค่าก่อนและหลัง เช่น แก้เวลา, มอบหมายทำแทน, แก้เวลารอบ, ออก QR ใหม่, จัดการบัญชี (facility-0051)`

with

`ประวัติทุกการเปลี่ยนแปลงที่ Admin ทำ: ใคร, เมื่อไร, เปลี่ยนอะไร, ค่าก่อนและหลัง เช่น แก้เวลา, มอบหมายทำแทน, แก้เวลารอบ, ออก QR ใหม่, จัดการบัญชี (facility-0051) รวมทั้งที่ server ทำเองตอนสร้างแอดมินคนแรกหรือกู้คืนแอดมิน ซึ่งผู้ทำคือ "ระบบ" (facility-0092)`

`docs/architecture.md`: after the paragraph that starts with `**Excel export (facility-0053).**`, add a blank line and

```markdown
**First Admin and reset-admin (facility-0092).** After the migration (and the Development seed), `FirstAdmin.EnsureAsync` creates one Admin from the server's `FirstAdmin__Username`, `FirstAdmin__Password` and `FirstAdmin__DisplayName`, only while no Admin row exists at all. A bad value stops the startup and names the setting, never the password. Started as `FacilityRealtime.Api reset-admin <username>`, `Program` migrates, runs `Commands/ResetAdminCommand` (password asked twice with the typing hidden), and exits without serving, so it needs only the connection string. Both go through `Persistence/ServerAdminAccounts` and write audit rows whose actor is null, which the history shows as "ระบบ". The handover guide is `docs/handover.md`.
```

In the class diagram, replace `User "1" <-- "0..*" AuditEntry : actor` with `User "0..1" <-- "0..*" AuditEntry : actor`.

- [ ] **Step 3: The ADR's amendment**

Append to `docs/adr/facility-0092-first-admin-from-server-settings.md`:

```markdown

## Amendments

- 2026-10-27, ตอนเขียนแผน (`docs/superpowers/plans/2026-10-27-first-admin-from-server-settings.md`):
  - ผู้กระทำในประวัติ: เจ้าของเลือกให้แสดงว่า "ระบบ" คอลัมน์ `audit_log.actor_id` ว่างได้ (migration `SystemAuditActor`) หน้า D9 แสดงคำว่า "ระบบ" ตัวกรอง "ผู้ทำ" ไม่มีระบบให้เลือก ใช้ประเภท "บัญชี" หาแถวเหล่านี้แทน
  - ชื่อรายการในประวัติ: `ADMIN_FIRST_CREATE` (ข้อ 2), `ADMIN_RECOVER_CREATE` และ `ADMIN_RECOVER_RESET` (ข้อ 5) อยู่ในประเภท "บัญชี"
  - ตั้งค่าไม่ครบ เช่น มีชื่อผู้ใช้แต่ไม่มีรหัสผ่าน นับเป็นค่าผิดตามข้อ 3 ระบบไม่ยอมเริ่ม และบอกค่าที่ขาด
  - ถ้ามีแอดมินแล้วแต่ยังตั้ง `FirstAdmin__Password` ค้างไว้ log เตือนให้ลบ (ช่วยข้อ 7) ไม่เปลี่ยนอะไร
  - คู่มือข้อ 8 อยู่ที่ `docs/handover.md` คู่มือ deploy จะเพิ่มในไฟล์เดียวกัน
```

- [ ] **Step 4: Commit**

```bash
git add docs/handover.md README.md CONTEXT.md docs/architecture.md docs/adr/facility-0092-first-admin-from-server-settings.md
git commit -m "docs: Thai handover guide for the first Admin and reset-admin (facility-0092)"
```

### Task 5: Real run on MySQL, handoff and the owner's test (controller)

The controller runs this task; MySQL never goes to a subagent. Use throwaway databases only. On this Mac, MySQL root has no password. Keep test passwords in shell variables, never in a file.

- [ ] **Step 1: The MySQL test suite**

```bash
mysql -u root -e "CREATE DATABASE facility_firstadmin_mysqltests_test CHARACTER SET utf8mb4;"
FACILITY_MYSQL_TEST="Server=localhost;Port=3306;Database=facility_firstadmin_mysqltests_test;Uid=root;Pwd=;" dotnet test backend/FacilityRealtime.slnx
```
Expected: everything passes with 0 skipped, including the MySQL tests, which migrate a fresh database with `SystemAuditActor`.

- [ ] **Step 2: First start on an empty database, Production**

```bash
mysql -u root -e "CREATE DATABASE facility_firstadmin_test CHARACTER SET utf8mb4;"
export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=facility_firstadmin_test;Uid=root;Pwd=;"
export ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5001
export Jwt__SigningKey="$(openssl rand -base64 48)"
export FirstAdmin__Username=first.admin FirstAdmin__DisplayName='แอดมินทดสอบ'
export FirstAdmin__Password="$(openssl rand -base64 24)"
dotnet run --no-launch-profile --project backend/src/FacilityRealtime.Api > "$SCRATCH/first-start.log" 2>&1 &
```
(`$SCRATCH` is the session's scratchpad directory; run the API in the background with its output captured there.) When `/api/health` answers, check:
- The log has `Created the first Admin 'first.admin'`, and no line contains `$FirstAdmin__Password` (`grep -F` on the captured output finds nothing).
- `POST /api/auth/login` with `{ "username": "first.admin", "password": "$FirstAdmin__Password" }` answers 200.
- `mysql -u root -e "SELECT action, actor_id FROM facility_firstadmin_test.audit_log"` shows `ADMIN_FIRST_CREATE` with `NULL`.
- With the access token, `GET /api/admin/audit-log` shows `"actorName":"ระบบ"` and no `actorId`.

- [ ] **Step 3: Restart, then a bad value**
- Restart with the same variables: no new user (`SELECT COUNT(*) FROM users` is still 1), and the log warns `FirstAdmin__Password is still set`.
- Drop and recreate `facility_firstadmin_test`, set `FirstAdmin__Password=short-pass`, and start: the process exits with `FirstAdmin__Password: รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร`, and the output does not contain `short-pass`. Then unset all three `FirstAdmin__*` and start: the log warns `No Admin account exists`, and the API answers `/api/health`.

- [ ] **Step 4: `reset-admin` with no `Jwt:SigningKey`**

Recreate the database and start once with valid `FirstAdmin__*` so `first.admin` exists, then stop the API. Unset `Jwt__SigningKey` and all `FirstAdmin__*`.

```bash
NEW="$(openssl rand -base64 24)"
printf '%s\n%s\n' "$NEW" "$NEW" | dotnet run --no-launch-profile --project backend/src/FacilityRealtime.Api -- reset-admin first.admin; echo "exit $?"
```
Expected: `exit 0`, the Thai success line, and no line containing `$NEW`. Start the API again (with `Jwt__SigningKey`): logging in with `$NEW` answers 200, and `audit_log` has `ADMIN_RECOVER_RESET` with a `NULL` actor. Then run `reset-admin backup.admin` with the piped input `แอดมินสำรอง`, `$NEW`, `$NEW`: exit 0, and a second Admin exists. (The Production database has no seed, so the employee-ID refusal is covered by the API tests only.)

- [ ] **Step 5: Handoff and the owner's test on their machine**

Update `docs/handoff.md`:
- Last updated: the branch is done, final review clean, not pushed, the owner's test is owed.
- A new top section `## Next session: test the first-Admin branch` with:
  - Steps 2–4 above, phrased for the owner to run in their own terminal. The owner types their own `FirstAdmin__Password` with `read -rs`.
  - Logging in at `http://localhost:5001/login/admin` (after `cd frontend && npm run build:api`).
  - D9 showing "ระบบ".
  - `reset-admin` typed by hand, to see the hidden typing.
- In `## Pending work`, the first-Admin item: done on the branch, owner's test owed.

Commit `docs/handoff.md` alone. Drop every `facility_firstadmin*_test` database the owner is not using, and delete `wwwroot` after the owner's test. Ask the owner by popup about push, PR and merge.
