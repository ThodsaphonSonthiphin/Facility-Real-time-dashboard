# Accounts Page and Excel Export (Backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Admin the accounts page (D6) to add, edit, re-phone, reset and deactivate accounts one by one, and the Excel export (D8) of attendance and submissions for a date range. Every action is logged.

**Architecture:**
- **Pure rules in `FacilityRealtime.Application`, unit-tested:**
  - `AccountRules` checks what the Admin types: employee ID, username, display name, temporary password.
  - `PhoneNumber.IsComplete` checks a login phone.
  - `ExportRules.MissedRounds` finds the rounds nobody did.
  - `AuditCategories` gains `Account` and `Export`.
- **No schema change.** `users` already has every column, including the unique `cleaner_slot` / `supervisor_slot`.
- **Reads that MySQL could treat differently from SQLite** (DATE ranges, joins, `UPPER`) live in Infrastructure as `ExportQuery`. A `[MySqlFact]` runs them on real MySQL.
- **New endpoint files:**
  - `AdminAccountEndpoints` under `/api/admin/accounts`, with `AccountChecks` and `AccountView` beside it.
  - `AdminExportEndpoints` at `POST /api/admin/export`, with `ExportSheets`, which turns query rows into Thai sheet rows.
- **One new package:** ClosedXML writes the `.xlsx`, behind `Infrastructure/Export/ExcelWorkbook`, which takes plain text sheets.

**Tech Stack:** .NET 10 Minimal API, EF Core 10 (`MySql.EntityFrameworkCore` 10.0.9 at runtime, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 in tests), ClosedXML 0.104.2, xUnit 2.9.3.

**Spec:**
- `docs/adr/facility-0020-accounts-page-operations.md`, with its amendments:
  - Add, edit, reset password, deactivate/activate. There is no delete.
  - An Admin cannot deactivate their own account.
  - Every action is logged.
- `docs/adr/facility-0054-login-with-employee-id-and-phone.md`:
  - Cleaners and Supervisors log in with employee ID + phone. The phone is stored only as a PBKDF2 hash.
  - Changing the phone logs that person out everywhere.
  - "รีเซ็ตรหัสผ่าน" is for Admins only; others get "แก้เบอร์โทร".
  - Amendment of 2026-10-09: Arabic digits only. Spaces, dashes and +66 are removed; Thai digits are not converted.
- `docs/adr/facility-0056-accounts-created-one-by-one-excel-import-before-rollout.md`:
  - The pilot adds accounts one at a time: employee ID, name, phone, role, and Area (Cleaner) or building + shift (Supervisor).
  - Excel import comes before rollout, not in this plan.
  - A phone that is not 10 digits is refused.
- `docs/adr/facility-0053-excel-export-instead-of-report-pages.md`:
  - Export by date range: attendance (person, Area, shift, 4 times, source) and submissions (point, round, time, result, Issue, cover, GPS flag).
  - Missed rounds are included.
  - No raw coordinates.
  - Every export is logged.
- `docs/adr/facility-0057-supervisor-building-page-and-audit-log-page.md`, rule 2: the history never shows a phone number or password, before or after.
- `docs/adr/facility-0051-admin-corrections-and-audit-log.md`: every account action and every export writes `audit_log`.
- `docs/adr/facility-0014-*` and `facility-0012-*`: a password reset or a deactivation revokes every refresh token of that person.
- `docs/adr/facility-0047-round-windows-and-late-submissions.md`: result words ตรงเวลา / ส่งช้า / นอกรอบ, and "ไม่ได้ทำ" for a round with no submission.
- `docs/adr/facility-0055-data-retention-and-pdpa-before-production.md`: the export carries distance and in/out of radius only.
- Wireframes in `docs/design/wireframe/`: `Accounts.dc.html` (D6) and `ExportExcel.dc.html` (D8).

## Global Constraints

- **Branch and merging:**
  - Work on branch `feat/accounts-export`, created from `master` at `7cd8ba4` (PR #53 merged).
  - Do not merge it.
- **No migration:** nothing in this plan changes the schema. If a task seems to need a schema change, stop and report instead of adding a migration.
- **One new package:** add `ClosedXML` version `0.104.2` (MIT licence) to `FacilityRealtime.Infrastructure` only. Add no other package. If that version will not restore, stop and report.
- **Endpoint security:**
  - Every new endpoint is under `/api/admin` and uses `RequireAuthorization(AuthSetup.AdminOnly)`. That gives 401 without login and 403 to Cleaners and Supervisors.
  - Each one loads the actor with `CurrentUser.LoadAsync` and answers 401 when that returns null.
- **Audit log (facility-0051):**
  - Every change writes one `audit_log` row in the same save or the same explicit transaction as the change.
  - Saving something unchanged writes nothing.
  - Pass snapshots (records or anonymous objects) as `before`/`after`, never entities.
  - **Never put a phone number, a password, a secret hash or a QR Token in the log, in any response, or in the Excel file.** A phone or password change logs `before: null, after: null`.
- **Race answers:** a `catch (DbUpdateException)` answers 409 only after re-reading the database proves another save took the name or the slot. Otherwise it rethrows. This is the plan-4 ruling.
- **Times:**
  - Stored DATETIMEs are UTC. Responses mark them UTC, so the JSON ends in `Z`.
  - The export prints Thai wall-clock `HH:mm` and Thai calendar dates `yyyy-MM-dd`.
  - Day shift is 07:00–19:00, Night is 19:00–07:00, and a night shift belongs to the date it started.
- **Error format:** errors answer with `ApiResults.Message(status, "<Thai text>")`, the JSON `{ "message": ... }` that the Admin pages show as is.
- **Runtime provider:** it is Oracle's `MySql.EntityFrameworkCore`, and SQLite tests cannot catch provider-only failures.
  - Task 7 adds a `[MySqlFact]` that runs when `FACILITY_MYSQL_TEST` is set.
  - Only use throwaway databases (`facility_*_test`), never `facility_dashboard`.
  - Ask the developer for the root password each time. Type it only into the command, and never write it into any file, this plan included, or into memory.
  - Subagents skip the MySQL steps; the controller runs them at the end.
- **This machine (Windows):**
  - Write files with Thai text using the editor tools, not a shell heredoc, which breaks the Thai text here.
  - Stop any API from this repo before building, because Windows locks its DLLs.
- **Public repo:** test data uses made-up names and `00000000xx`-style phone numbers only.
- **Handoff after every task (owner's rule):**
  - The last step of every task adds one entry to the "Task log" section of `docs/handoff.md` and commits it with the task.
  - The entry is in English and holds the date, the task, what changed, the test result and what comes next.
  - It holds no secret, phone number or real name.
- **Earlier rules stay:**
  - Keep every rule from PR #50–#53: auth, login throttle, the Admin-only Dashboard without QR Tokens, scan and attendance rules, Admin setup, attendance corrections, alerts and history.
  - One active Cleaner per Area per shift, and one active Supervisor per building per shift, both enforced by the unique slots.

### Decisions made while planning (report them to the owner in the PR)

1. **Routes are `/api/admin/accounts`**, like every Admin page since plan 3. ADR 0020's `/api/users` predates that convention.
2. **A role, an employee ID and a username are fixed once created.** Admins log in differently from Cleaners and Supervisors, so a role change would need new login details. Make a new account instead. With no role change, ADR 0020's "cannot demote yourself" cannot happen.
3. **Deactivating a Cleaner takes them off their Area at once**, so the Area's slot opens in the Area form (D10). Activating brings them back with no Area.
   - A Supervisor keeps building and shift.
   - Activating answers 409 when another active account holds that slot.
4. **A deactivated Cleaner cannot be given an Area** (400). Activate first.
5. **Changing a phone or resetting a password logs that person out on every device:** all refresh tokens are revoked, and the access token dies within 5 minutes.
   - The log row has no before or after.
   - Typing the same phone or password again changes nothing and logs nothing.
6. **An Admin may reset their own password**, which logs them out too. They cannot deactivate themselves (400), so one active Admin always remains.
7. **On creation:**
   - A Cleaner's Area is optional, because the Area may not exist yet. The shift is required.
   - A Supervisor needs a building and a shift.
   - An Admin has neither.
8. **What the Admin may type:**
   - Employee ID: 1–20 English letters, digits or `-`, unique ignoring case.
   - Username: 3–100 of `A–Z a–z 0–9 . _ -`, unique ignoring case.
   - Temporary password: 8–100 characters, not only spaces.
   - Display name: 1–150 characters after trimming.
   - Phone: exactly 10 Arabic digits starting with 0, after `PhoneNumber.Normalize`.
9. **Fields that do not belong to the role must be left out (null).** An Admin with a phone, a Cleaner with a building, or a Supervisor with an Area gets 400. This matches plan 4: omit unused fields, do not send them empty.
10. **The export is `POST /api/admin/export` and returns the `.xlsx` file.**
    - It is POST because it writes a log row.
    - It covers 1–31 shift dates and filters by one building or all.
    - Sets are chosen from `Attendance` and `Submissions`.
11. **No inspection sheet yet.** No endpoint writes Inspection Records until the Supervisor plan, which will add that sheet with "ไม่ได้ตรวจ / ไม่ได้แก้".
12. **"ไม่ได้ทำ" rows are computed from today's setup**, using active points of active Areas, for shifts that have already ended.
    - A window counts only if it existed when its round started.
    - A point deactivated since, or a window deleted since, does not show its missed rounds.
    - A day-only Area has no night rows.
13. **The submissions sheet names the Cleaner** (employee ID + name). ADR 0053 lists no person column, but "ทำแทน" means little without one.
14. **Issue tags are exported as stored** (e.g. `wet_floor`). The backend has no Thai label for them yet.
15. **Every cell is text** (dates `yyyy-MM-dd`, times `HH:mm`), so a name typed as `=1+1` can never become a formula. Columns have a fixed width, because auto-fit needs fonts the server may not have.
16. **Each export writes `EXPORT_EXCEL`** with from, to, building, sets and row counts, and no row content.

### Out of scope (later plans)

- **Excel import of accounts** (ADR 0056, before rollout).
- **Supervisor plan:** inspection, the inspection sheet of the export, Supervisor block reasons, Supervisor covers and `INSPECTION_GPS`.
- **Frontend:** every page.
- **Parked items from plans 3–4:** see `docs/handoff.md`. They are not touched here.

---

## File Structure

```
backend/
  src/FacilityRealtime.Application/
    Accounts/AccountRules.cs                 employee ID, username, display name, password checks          (new, Task 1)
    Auth/PhoneNumber.cs                      + IsComplete                                                     (modified, Task 1)
    Audit/AuditCategories.cs                 + Account, Export                                                (modified, Task 1)
    Export/ExportRules.cs                    ExportSet, MaxDays, MissedRounds                                 (new, Task 5)
  src/FacilityRealtime.Infrastructure/
    FacilityRealtime.Infrastructure.csproj   + ClosedXML 0.104.2                                              (modified, Task 6)
    Persistence/ExportQuery.cs               attendance, submission and point-round rows                       (new, Task 5)
    Export/ExcelWorkbook.cs                  ExcelSheet, Write                                                (new, Task 6)
  src/FacilityRealtime.Api/
    DTOs/AccountDtos.cs                      AccountDto and requests                                          (new, Task 2)
    DTOs/ExportDtos.cs                       ExportRequest                                                    (new, Task 6)
    Endpoints/AccountView.cs                 list read model                                                  (new, Task 2)
    Endpoints/AccountChecks.cs               place, uniqueness, slot holder, race explanation                  (new, Task 2)
    Endpoints/AdminAccountEndpoints.cs       /api/admin/accounts...                                           (new, Task 2; + Tasks 3, 4)
    Endpoints/ExportSheets.cs                rows → Thai sheet rows                                           (new, Task 6)
    Endpoints/AdminExportEndpoints.cs        POST /api/admin/export                                           (new, Task 6)
    Endpoints/ThaiLabels.cs                  + RoleName, SubmissionResult, Gps                                (modified, Tasks 2, 6)
    Program.cs                               maps the two new endpoint files                                  (modified, Tasks 2, 6)
  tests/FacilityRealtime.UnitTests/
    AccountRulesTests.cs                                                                                      (new, Task 1)
    PhoneNumberTests.cs                      + IsComplete                                                     (modified, Task 1)
    AuditCategoriesTests.cs                  + new actions                                                    (modified, Task 1)
    ExportRulesTests.cs                                                                                       (new, Task 5)
  tests/FacilityRealtime.ApiTests/
    Infrastructure/TestData.cs               + AddAreaAsync, AddBuildingAsync, AddAttendanceAsync, BackdateSetupAsync (modified, Task 2, 5)
    Admin/AccountModels.cs, AccountApi.cs    test models and helpers                                          (new, Task 2)
    Admin/AccountEndpointTests.cs            list and create                                                  (new, Task 2)
    Admin/AccountEditEndpointTests.cs        edit, deactivate, activate                                       (new, Task 3)
    Admin/AccountSecretEndpointTests.cs      phone, password, history has no secrets                         (new, Task 4)
    Admin/DeactivatedAdminTests.cs           + the new routes                                                 (modified, Tasks 4, 6)
    Persistence/ExportQueryTests.cs                                                                           (new, Task 5)
    Admin/ExportEndpointTests.cs                                                                              (new, Task 6)
    Persistence/MySqlExportAndAccountTests.cs                                                                 (new, Task 7)
docs/handoff.md                              Task log entry                                                   (every task)
README.md, docs/architecture.md              new routes                                                       (Task 7)
```

## Endpoint map

| Method | Route | Body | Answer |
|---|---|---|---|
| GET | `/api/admin/accounts` | — | 200 `AccountDto[]` (Admins, Supervisors, Cleaners; each by login name) |
| POST | `/api/admin/accounts` | `CreateAccountRequest` | 201 `AccountDto` |
| PUT | `/api/admin/accounts/{id}` | `UpdateAccountRequest` | 200 `AccountDto` |
| PUT | `/api/admin/accounts/{id}/phone` | `{ phone }` | 204 |
| PUT | `/api/admin/accounts/{id}/password` | `{ password }` | 204 |
| POST | `/api/admin/accounts/{id}/deactivate` | — | 200 `AccountDto` |
| POST | `/api/admin/accounts/{id}/activate` | — | 200 `AccountDto` |
| POST | `/api/admin/export` | `ExportRequest` | 200 `.xlsx` file |

---

### Task 1: Account rules, login phone check, audit categories

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Accounts/AccountRules.cs`
- Modify: `backend/src/FacilityRealtime.Application/Auth/PhoneNumber.cs`
- Modify: `backend/src/FacilityRealtime.Application/Audit/AuditCategories.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/AccountRulesTests.cs` (new)
- Test: `backend/tests/FacilityRealtime.UnitTests/PhoneNumberTests.cs` (append)
- Test: `backend/tests/FacilityRealtime.UnitTests/AuditCategoriesTests.cs` (append rows)

**Interfaces:**
- Produces:
  - `AccountRules.NormalizeEmployeeId(string?) → string?`, `NormalizeUsername(string?) → string?`, `NormalizeDisplayName(string?) → string?`, `IsAllowedPassword(string?) → bool`, `MaxDisplayName = 150`, `MinPassword = 8`, `MaxPassword = 100`.
  - `PhoneNumber.IsComplete(string normalized) → bool`.
  - `AuditCategory.Account`, `AuditCategory.Export`.
  - Actions: `ACCOUNT_CREATE`, `ACCOUNT_UPDATE`, `ACCOUNT_PHONE`, `ACCOUNT_PASSWORD`, `ACCOUNT_ACTIVATE`, `ACCOUNT_DEACTIVATE`, `EXPORT_EXCEL`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/FacilityRealtime.UnitTests/AccountRulesTests.cs`:

```csharp
using FacilityRealtime.Application.Accounts;

namespace FacilityRealtime.UnitTests;

/// <summary>Wireframe D6: what the Admin may type when adding an account (facility-0020, 0054, 0056).</summary>
public class AccountRulesTests
{
    [Theory]
    [InlineData(" E1001 ", "E1001")]
    [InlineData("emp-0042", "emp-0042")]
    [InlineData("A", "A")]
    [InlineData("12345678901234567890", "12345678901234567890")]
    public void Employee_ids_are_trimmed(string typed, string expected) =>
        Assert.Equal(expected, AccountRules.NormalizeEmployeeId(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("E 1001")]
    [InlineData("E1001!")]
    [InlineData("รหัส1")]
    [InlineData("123456789012345678901")]
    public void Bad_employee_ids_are_refused(string? typed) => Assert.Null(AccountRules.NormalizeEmployeeId(typed));

    [Theory]
    [InlineData(" admin2 ", "admin2")]
    [InlineData("a.b_c-d", "a.b_c-d")]
    public void Usernames_are_trimmed(string typed, string expected) => Assert.Equal(expected, AccountRules.NormalizeUsername(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("ผู้ดูแล")]
    public void Bad_usernames_are_refused(string? typed) => Assert.Null(AccountRules.NormalizeUsername(typed));

    [Fact]
    public void A_username_is_at_most_100_characters()
    {
        Assert.NotNull(AccountRules.NormalizeUsername(new string('a', 100)));
        Assert.Null(AccountRules.NormalizeUsername(new string('a', 101)));
    }

    [Fact]
    public void Display_names_are_trimmed_and_at_most_150_characters()
    {
        Assert.Equal("สมใจ ทดสอบ", AccountRules.NormalizeDisplayName("  สมใจ ทดสอบ  "));
        Assert.NotNull(AccountRules.NormalizeDisplayName(new string('ก', 150)));
        Assert.Null(AccountRules.NormalizeDisplayName(new string('ก', 151)));
        Assert.Null(AccountRules.NormalizeDisplayName("   "));
        Assert.Null(AccountRules.NormalizeDisplayName(null));
    }

    [Theory]
    [InlineData("12345678", true)]
    [InlineData("temp pass 1", true)]
    [InlineData("1234567", false)]
    [InlineData("        ", false)]
    [InlineData(null, false)]
    public void A_temporary_password_is_8_to_100_characters(string? password, bool allowed) =>
        Assert.Equal(allowed, AccountRules.IsAllowedPassword(password));

    [Fact]
    public void A_password_longer_than_100_characters_is_refused() =>
        Assert.False(AccountRules.IsAllowedPassword(new string('x', 101)));
}
```

Append to the class in `backend/tests/FacilityRealtime.UnitTests/PhoneNumberTests.cs`:

```csharp
    [Theory]
    [InlineData("0810000001", true)]
    [InlineData("0000000011", true)]
    [InlineData("081000000", false)]
    [InlineData("08100000011", false)]
    [InlineData("1810000001", false)]
    [InlineData("", false)]
    public void A_login_phone_is_ten_digits_starting_with_zero(string normalized, bool complete) =>
        Assert.Equal(complete, PhoneNumber.IsComplete(normalized));

    [Fact]
    public void Thai_digits_never_make_a_complete_phone() =>
        Assert.False(PhoneNumber.IsComplete(PhoneNumber.Normalize("๐๘๑๐๐๐๐๐๐๑")));

    [Fact]
    public void A_typed_phone_with_dashes_or_country_code_is_complete_once_normalized()
    {
        Assert.True(PhoneNumber.IsComplete(PhoneNumber.Normalize("081-000-0001")));
        Assert.True(PhoneNumber.IsComplete(PhoneNumber.Normalize("+66 81 000 0001")));
    }
```

In `backend/tests/FacilityRealtime.UnitTests/AuditCategoriesTests.cs`, add these rows above `[InlineData("QR_REGENERATE", AuditCategory.QrToken)]`:

```csharp
    [InlineData("ACCOUNT_CREATE", AuditCategory.Account)]
    [InlineData("ACCOUNT_UPDATE", AuditCategory.Account)]
    [InlineData("ACCOUNT_PHONE", AuditCategory.Account)]
    [InlineData("ACCOUNT_PASSWORD", AuditCategory.Account)]
    [InlineData("ACCOUNT_ACTIVATE", AuditCategory.Account)]
    [InlineData("ACCOUNT_DEACTIVATE", AuditCategory.Account)]
    [InlineData("EXPORT_EXCEL", AuditCategory.Export)]
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AccountRulesTests|FullyQualifiedName~PhoneNumberTests|FullyQualifiedName~AuditCategoriesTests"`
Expected: build FAILS with `AccountRules`, `IsComplete`, `AuditCategory.Account` not defined.

- [ ] **Step 3: Write the rules**

`backend/src/FacilityRealtime.Application/Accounts/AccountRules.cs`:

```csharp
using System.Text.RegularExpressions;

namespace FacilityRealtime.Application.Accounts;

/// <summary>What the Admin may type on the accounts page (wireframe D6, facility-0020, 0054, 0056).</summary>
public static partial class AccountRules
{
    /// <summary>users.display_name is 150 wide.</summary>
    public const int MaxDisplayName = 150;

    public const int MinPassword = 8;
    public const int MaxPassword = 100;

    /// <summary>Trimmed; 1–20 English letters, digits or "-" (users.employee_id is 20 wide). Null when not allowed.</summary>
    public static string? NormalizeEmployeeId(string? typed)
    {
        var trimmed = typed?.Trim() ?? string.Empty;
        return EmployeeIdPattern().IsMatch(trimmed) ? trimmed : null;
    }

    /// <summary>The Admin's login: trimmed; 3–100 English letters, digits, ".", "_" or "-". Null when not allowed.</summary>
    public static string? NormalizeUsername(string? typed)
    {
        var trimmed = typed?.Trim() ?? string.Empty;
        return UsernamePattern().IsMatch(trimmed) ? trimmed : null;
    }

    public static string? NormalizeDisplayName(string? typed)
    {
        var trimmed = typed?.Trim() ?? string.Empty;
        return trimmed.Length is 0 or > MaxDisplayName ? null : trimmed;
    }

    /// <summary>The temporary password is kept exactly as typed, so spaces count, but it cannot be only spaces.</summary>
    public static bool IsAllowedPassword(string? password) =>
        password is { Length: >= MinPassword and <= MaxPassword } && !string.IsNullOrWhiteSpace(password);

    [GeneratedRegex(@"^[A-Za-z0-9-]{1,20}\z")]
    private static partial Regex EmployeeIdPattern();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{3,100}\z")]
    private static partial Regex UsernamePattern();
}
```

Add to `backend/src/FacilityRealtime.Application/Auth/PhoneNumber.cs`, inside the class after `Normalize`:

```csharp
    /// <summary>facility-0056: a login phone is 10 digits starting with 0. Pass the result of <see cref="Normalize"/>.</summary>
    public static bool IsComplete(string normalized) =>
        normalized.Length == 10 && normalized[0] == '0' && normalized.All(char.IsAsciiDigit);
```

In `backend/src/FacilityRealtime.Application/Audit/AuditCategories.cs`:
- Change the summary of `AuditCategory` to `/// <summary>The "ประเภท" filter of the Admin history page (wireframe D9, facility-0057).</summary>`.
- Add `Account,` and `Export,` after `QrToken,` in the enum.
- Add these entries to `Actions`, after the `QrToken` entry:

```csharp
        [AuditCategory.Account] =
        [
            "ACCOUNT_CREATE", "ACCOUNT_UPDATE", "ACCOUNT_PHONE", "ACCOUNT_PASSWORD", "ACCOUNT_ACTIVATE", "ACCOUNT_DEACTIVATE",
        ],
        [AuditCategory.Export] = ["EXPORT_EXCEL"],
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests`
Expected: PASS, all unit tests (215 before this task, plus the new ones).

- [ ] **Step 5: Run the API tests**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests`
Expected: PASS (the history page's category filter accepts `Account` and `Export` now; nothing else changes).

- [ ] **Step 6: Update the handoff and commit**

Add under `## Task log (plan 5)` in `docs/handoff.md` (create the section if it is missing, just above `## Pending work`):

```markdown
- 2026-10-14 · Task 1 done: AccountRules, PhoneNumber.IsComplete, audit categories Account and Export. Unit and API tests pass. Next: Task 2 (account list and create).
```

Use the real date if it is not 2026-10-14.

```bash
git add backend/src/FacilityRealtime.Application backend/tests/FacilityRealtime.UnitTests docs/handoff.md
git commit -m "feat(backend): account input rules, login phone check, Account and Export history categories"
```

---

### Task 2: Account list and create

**Files:**
- Create: `backend/src/FacilityRealtime.Api/DTOs/AccountDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AccountView.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AccountChecks.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminAccountEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs` (+ `RoleName`)
- Modify: `backend/src/FacilityRealtime.Api/Program.cs` (map the endpoints)
- Modify: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs` (+ `AddAreaAsync`, `AddBuildingAsync`)
- Create: `backend/tests/FacilityRealtime.ApiTests/Admin/AccountModels.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/Admin/AccountApi.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AccountEndpointTests.cs`

**Interfaces:**
- Consumes: Task 1's `AccountRules`, `PhoneNumber.IsComplete`; existing `IPasswordHasher.Hash`, `AuditTrail.Add`, `CurrentUser.LoadAsync`, `ApiResults.Message`, `ThaiLabels.ShiftName`.
- Produces, for Tasks 3–4:
  - `AccountDto`, `UpdateAccountRequest`, `AccountPhoneRequest`, `AccountPasswordRequest`.
  - `AccountView.LoadAsync(db, int? onlyUserId = null)`, `AccountView.LoadOneAsync(db, userId)`.
  - `AccountChecks.ReadPlaceAsync`, `HolderAsync`, `SlotTaken`, `ClaimOf`, `ExplainRaceAsync`, `Bad`, `Conflict`, `NotFound`.
  - `AdminAccountEndpoints.Snapshot(User)` and `SaveAsync(db, claim, afterSave)`.
  - Test helpers `AccountApi.*`, `TestData.AddAreaAsync`, `TestData.AddBuildingAsync`.

- [ ] **Step 1: Add the test helpers and models**

Append to `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs`, inside the class:

```csharp
    /// <summary>A new building with no Areas, e.g. "B".</summary>
    public static async Task<int> AddBuildingAsync(FacilityApiFactory factory, string code)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var building = new Building { Code = code, Name = $"ตึก {code}", CreatedAt = DateTime.UtcNow };
            db.Buildings.Add(building);
            await db.SaveChangesAsync();
            id = building.Id;
        });
        return id;
    }

    /// <summary>A new Area with its check-in sign and no Cleaner, e.g. "AR03" in building A.</summary>
    public static async Task<int> AddAreaAsync(
        FacilityApiFactory factory, string code, string buildingCode = "A", ShiftPattern pattern = ShiftPattern.DayAndNight)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var building = await db.Buildings.SingleAsync(b => b.Code == buildingCode);
            var area = new Area { BuildingId = building.Id, Code = code, Name = $"Area {code}", ShiftPattern = pattern, CreatedAt = DateTime.UtcNow };
            db.Signs.Add(new Sign { Area = area, Code = $"{code}-IN", QrToken = $"token-checkin-{code.ToLowerInvariant()}", QrIssuedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            id = area.Id;
        });
        return id;
    }
```

`backend/tests/FacilityRealtime.ApiTests/Admin/AccountModels.cs`:

```csharp
namespace FacilityRealtime.ApiTests.Admin;

public record AccountModel(
    int Id, string Role, string LoginName, string DisplayName, int? AreaId, string? AreaCode, int? BuildingId, string? BuildingCode,
    string? Shift, bool IsActive, DateTime CreatedAt);
```

`backend/tests/FacilityRealtime.ApiTests/Admin/AccountApi.cs`:

```csharp
using System.Net.Http.Json;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Calls of the accounts page (wireframe D6). Fields a role does not use are left out, as the page will.</summary>
public static class AccountApi
{
    public const string Path = "/api/admin/accounts";

    public static Task<HttpResponseMessage> CreateCleanerAsync(
        HttpClient admin, string employeeId, string phone, int? areaId, string shift = "Day", string displayName = "แม่บ้านใหม่ ทดสอบ") =>
        admin.PostAsJsonAsync(Path, new { role = "Cleaner", displayName, employeeId, phone, areaId, shift });

    public static Task<HttpResponseMessage> CreateSupervisorAsync(
        HttpClient admin, string employeeId, string phone, int buildingId, string shift, string displayName = "หัวหน้าใหม่ ทดสอบ") =>
        admin.PostAsJsonAsync(Path, new { role = "Supervisor", displayName, employeeId, phone, buildingId, shift });

    public static Task<HttpResponseMessage> CreateAdminAsync(
        HttpClient admin, string username, string password, string displayName = "Admin ใหม่ ทดสอบ") =>
        admin.PostAsJsonAsync(Path, new { role = "Admin", displayName, username, password });

    public static async Task<AccountModel> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AccountModel>())!;

    public static async Task<List<AccountModel>> ListAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<AccountModel>>(Path))!;
}
```

- [ ] **Step 2: Write the failing tests**

`backend/tests/FacilityRealtime.ApiTests/Admin/AccountEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Wireframe D6, facility-0020/0054/0056: the Admin lists accounts and adds them one by one.</summary>
public class AccountEndpointTests
{
    [Fact]
    public async Task The_list_shows_every_account_admins_first_and_never_a_secret()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.GetAsync(AccountApi.Path);
        var raw = await response.Content.ReadAsStringAsync();
        var accounts = JsonSerializer.Deserialize<List<AccountModel>>(raw, JsonSerializerOptions.Web)!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { "admin", "S2001", "E1001", "E1002", "E1003" }, accounts.Select(a => a.LoginName));
        Assert.Equal(new[] { "Admin", "Supervisor", "Cleaner", "Cleaner", "Cleaner" }, accounts.Select(a => a.Role));
        var e1001 = accounts.Single(a => a.LoginName == "E1001");
        Assert.Equal(("AR01", "Day", true), (e1001.AreaCode, e1001.Shift, e1001.IsActive));
        Assert.Equal("A", accounts.Single(a => a.LoginName == "S2001").BuildingCode);
        Assert.EndsWith("Z", JsonDocument.Parse(raw).RootElement[0].GetProperty("createdAt").GetString());
        Assert.DoesNotContain("0810000001", raw);
        Assert.DoesNotContain("pbkdf2", raw);
        Assert.DoesNotContain("secret", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_new_cleaner_logs_in_with_the_phone_at_once_and_the_log_has_no_phone()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar03 = await TestData.AddAreaAsync(factory, "AR03");

        var response = await AccountApi.CreateCleanerAsync(admin, " E2001 ", "000-000-0011", ar03, "Night");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await AccountApi.ReadAsync(response);
        Assert.Equal(("Cleaner", "E2001", "AR03", "Night", true), (created.Role, created.LoginName, created.AreaCode, created.Shift, created.IsActive));
        Assert.Equal($"/api/admin/accounts/{created.Id}", response.Headers.Location!.OriginalString);

        var login = await AuthApi.LoginAsync(factory.CreateApiClient(), "E2001", "0000000011");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("ACCOUNT_CREATE", "users", (long?)created.Id, adminAuth.User.Id), (entry.Action, entry.EntityType, entry.EntityId, entry.ActorId));
        Assert.Contains("E2001", entry.Summary);
        Assert.Null(entry.BeforeJson);
        Assert.Contains("\"LoginName\":\"E2001\"", entry.AfterJson);
        Assert.DoesNotContain("0000000011", entry.AfterJson + entry.Summary);
    }

    [Fact]
    public async Task A_cleaner_may_be_added_without_an_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateCleanerAsync(admin, "E2002", "0000000012", areaId: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await AccountApi.ReadAsync(response);
        Assert.Null(created.AreaId);
        Assert.Equal("Day", created.Shift);
    }

    [Fact]
    public async Task A_taken_area_shift_is_refused_and_names_who_holds_it()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await AccountApi.CreateCleanerAsync(admin, "E2003", "0000000013", ar01, "Day");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของ AR01 มีแม่บ้านประจำแล้ว (สมชาย ใจดี)", await AdminSetupApi.MessageAsync(response));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_supervisor_takes_a_free_building_shift_only()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);

        var taken = await AccountApi.CreateSupervisorAsync(admin, "S2002", "0000000021", buildingA, "Day");
        var free = await AccountApi.CreateSupervisorAsync(admin, "S2003", "0000000022", buildingA, "Night");

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("กะเช้าของตึก A มีหัวหน้าประจำแล้ว (สมปอง ตรวจดี)", await AdminSetupApi.MessageAsync(taken));
        Assert.Equal(HttpStatusCode.Created, free.StatusCode);
        var created = await AccountApi.ReadAsync(free);
        Assert.Equal(("Supervisor", "A", "Night"), (created.Role, created.BuildingCode, created.Shift));
    }

    [Fact]
    public async Task A_new_admin_logs_in_with_the_temporary_password()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateAdminAsync(admin, "admin2", "temporary-1");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await AccountApi.ReadAsync(response);
        Assert.Equal(("Admin", "admin2", (string?)null), (created.Role, created.LoginName, created.Shift));
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin2", "temporary-1")).StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.DoesNotContain("temporary-1", entry.AfterJson + entry.Summary);
    }

    [Theory]
    [InlineData("e1001")]
    [InlineData("E1001")]
    public async Task A_used_employee_id_is_refused_whatever_the_letter_case(string employeeId)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateCleanerAsync(admin, employeeId, "0000000014", areaId: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("รหัสพนักงานนี้มีแล้ว", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_used_username_is_refused_whatever_the_letter_case()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateAdminAsync(admin, "ADMIN", "temporary-1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ชื่อผู้ใช้นี้มีแล้ว", await AdminSetupApi.MessageAsync(response));
    }

    public static TheoryData<string> BadRequests => new()
    {
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "๐๘๑๐๐๐๐๐๑๑", "shift": "Day" }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "081000001", "shift": "Day" }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E 2009", "phone": "0000000019", "shift": "Day" }""",
        """{ "role": "Cleaner", "displayName": "   ", "employeeId": "E2009", "phone": "0000000019", "shift": "Day" }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "0000000019" }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "0000000019", "shift": "Day", "buildingId": 1 }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "0000000019", "shift": "Day", "areaId": 9999 }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "0000000019", "shift": "Day", "password": "12345678" }""",
        """{ "role": "Supervisor", "displayName": "ก", "employeeId": "S2009", "phone": "0000000029", "shift": "Night" }""",
        """{ "role": "Supervisor", "displayName": "ก", "employeeId": "S2009", "phone": "0000000029", "shift": "Night", "buildingId": 1, "areaId": 1 }""",
        """{ "role": "Admin", "displayName": "ก", "username": "admin9", "password": "1234567" }""",
        """{ "role": "Admin", "displayName": "ก", "username": "admin9", "password": "12345678", "phone": "0000000019" }""",
        """{ "role": "Admin", "displayName": "ก", "username": "admin9", "password": "12345678", "shift": "Day" }""",
        """{ "displayName": "ก", "employeeId": "E2009", "phone": "0000000019", "shift": "Day" }""",
    };

    [Theory]
    [MemberData(nameof(BadRequests))]
    public async Task Bad_input_is_refused_and_nothing_is_saved(string body)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var before = (await AccountApi.ListAsync(admin)).Count;

        var response = await admin.PostAsync(AccountApi.Path, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(await AdminSetupApi.MessageAsync(response)));
        Assert.Equal(before, (await AccountApi.ListAsync(admin)).Count);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_day_only_area_takes_no_night_cleaner()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");   // seeded DayOnly

        var response = await AccountApi.CreateCleanerAsync(admin, "E2004", "0000000015", ar02, "Night");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("AR02 ทำเฉพาะกะเช้า เลือกกะดึกไม่ได้", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_deactivated_area_takes_no_cleaner()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar03 = await TestData.AddAreaAsync(factory, "AR03");
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Id == ar03)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await AccountApi.CreateCleanerAsync(admin, "E2005", "0000000016", ar03);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cleaners_and_supervisors_cannot_open_the_page()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2001", "0820000001");

        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(AccountApi.Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.GetAsync(AccountApi.Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AccountApi.CreateCleanerAsync(cleaner, "E2006", "0000000017", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateApiClient().GetAsync(AccountApi.Path)).StatusCode);
    }
}
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AccountEndpointTests"`
Expected: FAIL; the route does not exist (404 where 200/201/400/409 are expected).

- [ ] **Step 4: Write the DTOs**

`backend/src/FacilityRealtime.Api/DTOs/AccountDtos.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>
/// One row of the accounts page (wireframe D6). LoginName is the employee ID, or the username of an Admin.
/// No phone, password or hash ever leaves the server (facility-0054).
/// </summary>
public record AccountDto(
    int Id,
    UserRole Role,
    string LoginName,
    string DisplayName,
    int? AreaId,
    string? AreaCode,
    int? BuildingId,
    string? BuildingCode,
    Shift? Shift,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>
/// Cleaner: EmployeeId, Phone, Shift, optional AreaId. Supervisor: EmployeeId, Phone, BuildingId, Shift.
/// Admin: Username and a temporary Password. Leave out (null) every field the role does not use.
/// </summary>
public record CreateAccountRequest(
    UserRole? Role,
    string? DisplayName,
    string? EmployeeId,
    string? Phone,
    string? Username,
    string? Password,
    int? AreaId,
    int? BuildingId,
    Shift? Shift);

/// <summary>The role, employee ID and username are fixed. The place fields replace the current ones; null clears a Cleaner's Area.</summary>
public record UpdateAccountRequest(string? DisplayName, int? AreaId, int? BuildingId, Shift? Shift);

public record AccountPhoneRequest(string? Phone);

public record AccountPasswordRequest(string? Password);
```

- [ ] **Step 5: Write the read model and the checks**

Add to `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs`, inside the class:

```csharp
    public static string RoleName(UserRole role) => role switch
    {
        UserRole.Cleaner => "แม่บ้าน",
        UserRole.Supervisor => "หัวหน้า",
        _ => "Admin",
    };
```

`backend/src/FacilityRealtime.Api/Endpoints/AccountView.cs`:

```csharp
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>What the accounts page shows, deactivated accounts included.</summary>
internal static class AccountView
{
    /// <summary>Admins, then Supervisors, then Cleaners, each by login name. About 180 rows: the page filters them itself (facility-0056).</summary>
    public static async Task<List<AccountDto>> LoadAsync(AppDbContext db, int? onlyUserId = null)
    {
        var users = await db.Users.AsNoTracking()
            .Include(u => u.Area)
            .Include(u => u.Building)
            .Where(u => onlyUserId == null || u.Id == onlyUserId)
            .ToListAsync();
        return users
            .OrderBy(u => RoleOrder(u.Role))
            .ThenBy(u => u.LoginName, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList();
    }

    public static async Task<AccountDto> LoadOneAsync(AppDbContext db, int userId) => (await LoadAsync(db, userId)).Single();

    private static int RoleOrder(UserRole role) => role switch
    {
        UserRole.Admin => 0,
        UserRole.Supervisor => 1,
        _ => 2,
    };

    private static AccountDto ToDto(User user) => new(
        user.Id,
        user.Role,
        user.LoginName,
        user.DisplayName,
        user.AreaId,
        user.Area?.Code,
        user.BuildingId,
        user.Building?.Code,
        user.Shift,
        user.IsActive,
        DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc));
}
```

`backend/src/FacilityRealtime.Api/Endpoints/AccountChecks.cs`:

```csharp
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>Where an account works (Area or building + shift), and the checks that keep login names and slots unique.</summary>
internal static class AccountChecks
{
    public const string EmployeeIdTaken = "รหัสพนักงานนี้มีแล้ว";
    public const string UsernameTaken = "ชื่อผู้ใช้นี้มีแล้ว";

    /// <summary>The place a form asks for, checked against the role. Area and Building are tracked entities or null.</summary>
    public sealed record AccountPlace(Area? Area, Building? Building, Shift? Shift);

    /// <summary>What a save claims: its login name and its slot. A new account has UserId 0.</summary>
    public sealed record AccountClaim(int UserId, UserRole Role, string? Username, string? EmployeeId, int? AreaId, int? BuildingId, Shift? Shift);

    /// <summary>A deactivated account holds no slot (users.cleaner_slot and supervisor_slot are NULL for it).</summary>
    public static AccountClaim ClaimOf(User user) => new(
        user.Id,
        user.Role,
        user.Username,
        user.EmployeeId,
        user.IsActive ? user.AreaId : null,
        user.IsActive ? user.BuildingId : null,
        user.Shift);

    /// <summary>
    /// Cleaner: shift required, Area optional, no building. Supervisor: building and shift, no Area. Admin: none of them.
    /// <paramref name="userId"/> is the account being edited (0 for a new one), so it never collides with itself.
    /// </summary>
    public static async Task<(AccountPlace? Place, IResult? Error)> ReadPlaceAsync(
        AppDbContext db, UserRole role, int userId, int? areaId, int? buildingId, Shift? shift)
    {
        if (shift is { } given && !Enum.IsDefined(given))
        {
            return (null, Bad("กะต้องเป็น Day หรือ Night"));
        }

        switch (role)
        {
            case UserRole.Admin:
                return areaId is null && buildingId is null && shift is null
                    ? (new AccountPlace(null, null, null), null)
                    : (null, Bad("บัญชี Admin ไม่มี Area ตึก หรือกะ"));

            case UserRole.Cleaner:
            {
                if (buildingId is not null)
                {
                    return (null, Bad("แม่บ้านประจำ Area ไม่ได้ประจำตึก"));
                }

                if (shift is not { } cleanerShift)
                {
                    return (null, Bad("ต้องเลือกกะของแม่บ้าน"));
                }

                if (areaId is null)
                {
                    return (new AccountPlace(null, null, cleanerShift), null);
                }

                var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == areaId && a.IsActive);
                if (area is null)
                {
                    return (null, Bad("ต้องเลือก Area ที่ใช้งานอยู่"));
                }

                if (!area.HasShift(cleanerShift))
                {
                    return (null, Bad($"{area.Code} ทำเฉพาะกะเช้า เลือกกะดึกไม่ได้"));
                }

                return await HolderAsync(db, role, area.Id, null, cleanerShift, userId) is { } holder
                    ? (null, Conflict(SlotTaken(holder)))
                    : (new AccountPlace(area, null, cleanerShift), null);
            }

            default:
            {
                if (areaId is not null)
                {
                    return (null, Bad("หัวหน้าประจำตึก ไม่ได้ประจำ Area"));
                }

                if (buildingId is null || shift is not { } supervisorShift)
                {
                    return (null, Bad("ต้องเลือกตึกและกะของหัวหน้า"));
                }

                var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId && b.IsActive);
                if (building is null)
                {
                    return (null, Bad("ต้องเลือกตึกที่ใช้งานอยู่"));
                }

                return await HolderAsync(db, role, null, building.Id, supervisorShift, userId) is { } holder
                    ? (null, Conflict(SlotTaken(holder)))
                    : (new AccountPlace(null, building, supervisorShift), null);
            }
        }
    }

    /// <summary>The other active account in that Area-shift (Cleaner) or building-shift (Supervisor) slot, with its Area or Building loaded.</summary>
    public static Task<User?> HolderAsync(AppDbContext db, UserRole role, int? areaId, int? buildingId, Shift? shift, int exceptUserId) =>
        role switch
        {
            UserRole.Cleaner when areaId is not null && shift is not null => db.Users.AsNoTracking().Include(u => u.Area)
                .FirstOrDefaultAsync(u => u.Id != exceptUserId && u.Role == UserRole.Cleaner && u.IsActive && u.AreaId == areaId && u.Shift == shift),
            UserRole.Supervisor when buildingId is not null && shift is not null => db.Users.AsNoTracking().Include(u => u.Building)
                .FirstOrDefaultAsync(u => u.Id != exceptUserId && u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId == buildingId && u.Shift == shift),
            _ => Task.FromResult<User?>(null),
        };

    public static string SlotTaken(User holder) => holder.Role == UserRole.Cleaner
        ? $"กะ{ThaiLabels.ShiftName(holder.Shift!.Value)}ของ {holder.Area!.Code} มีแม่บ้านประจำแล้ว ({holder.DisplayName})"
        : $"กะ{ThaiLabels.ShiftName(holder.Shift!.Value)}ของตึก {holder.Building!.Code} มีหัวหน้าประจำแล้ว ({holder.DisplayName})";

    /// <summary>Unique ignoring case: MySQL's collation already treats "e1001" and "E1001" as one; SQLite would not.</summary>
    public static Task<bool> EmployeeIdTakenAsync(AppDbContext db, string employeeId, int exceptUserId)
    {
        var upper = employeeId.ToUpperInvariant();
        return db.Users.AnyAsync(u => u.Id != exceptUserId && u.EmployeeId != null && u.EmployeeId.ToUpper() == upper);
    }

    public static Task<bool> UsernameTakenAsync(AppDbContext db, string username, int exceptUserId)
    {
        var upper = username.ToUpperInvariant();
        return db.Users.AnyAsync(u => u.Id != exceptUserId && u.Username != null && u.Username.ToUpper() == upper);
    }

    /// <summary>
    /// After a DbUpdateException: the 409 to give when another save took the login name or the slot first.
    /// Null means the database failed for another reason, and the caller must rethrow.
    /// </summary>
    public static async Task<IResult?> ExplainRaceAsync(AppDbContext db, AccountClaim claim)
    {
        db.ChangeTracker.Clear();
        if (claim.Username is { } username && await UsernameTakenAsync(db, username, claim.UserId))
        {
            return Conflict(UsernameTaken);
        }

        if (claim.EmployeeId is { } employeeId && await EmployeeIdTakenAsync(db, employeeId, claim.UserId))
        {
            return Conflict(EmployeeIdTaken);
        }

        return await HolderAsync(db, claim.Role, claim.AreaId, claim.BuildingId, claim.Shift, claim.UserId) is { } holder
            ? Conflict(SlotTaken(holder))
            : null;
    }

    public static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);

    public static IResult Conflict(string message) => ApiResults.Message(StatusCodes.Status409Conflict, message);

    public static IResult NotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบบัญชีนี้");
}
```

- [ ] **Step 6: Write the endpoints**

`backend/src/FacilityRealtime.Api/Endpoints/AdminAccountEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Accounts;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static FacilityRealtime.Api.Endpoints.AccountChecks;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// Wireframe D6, facility-0020/0054/0056: the Admin adds, edits, re-phones, resets and deactivates accounts one by one.
/// There is no delete. Every change is logged (facility-0051), never with a phone, a password or a hash (facility-0057).
/// </summary>
public static class AdminAccountEndpoints
{
    public static IEndpointRouteBuilder MapAdminAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/api/admin/accounts").RequireAuthorization(AuthSetup.AdminOnly);
        accounts.MapGet("", async (ClaimsPrincipal principal, AppDbContext db) =>
            await CurrentUser.LoadAsync(principal, db) is null ? Results.Unauthorized() : Results.Ok(await AccountView.LoadAsync(db)));
        accounts.MapPost("", CreateAsync);
        return app;
    }

    /// <summary>What the audit log keeps of an account. It has no secret on purpose: never add one.</summary>
    internal sealed record AccountSnapshot(string Role, string LoginName, string DisplayName, int? AreaId, int? BuildingId, string? Shift, bool IsActive);

    internal static AccountSnapshot Snapshot(User user) =>
        new(user.Role.ToString(), user.LoginName, user.DisplayName, user.AreaId, user.BuildingId, user.Shift?.ToString(), user.IsActive);

    private static async Task<IResult> CreateAsync(
        CreateAccountRequest request, ClaimsPrincipal principal, AppDbContext db, IPasswordHasher hasher, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.Role is not { } role || !Enum.IsDefined(role))
        {
            return Bad("ต้องเลือกบทบาท");
        }

        if (AccountRules.NormalizeDisplayName(request.DisplayName) is not { } displayName)
        {
            return Bad($"ต้องใส่ชื่อที่แสดง ไม่เกิน {AccountRules.MaxDisplayName} ตัวอักษร");
        }

        var user = new User { Role = role, DisplayName = displayName, CreatedAt = clock.GetUtcNow().UtcDateTime };
        string secret;
        if (role == UserRole.Admin)
        {
            if (request.EmployeeId is not null || request.Phone is not null)
            {
                return Bad("บัญชี Admin ใช้ชื่อผู้ใช้และรหัสผ่าน ไม่ใช้รหัสพนักงานหรือเบอร์โทร");
            }

            if (AccountRules.NormalizeUsername(request.Username) is not { } username)
            {
                return Bad("ชื่อผู้ใช้ต้องเป็นตัวอักษรอังกฤษ ตัวเลข . _ หรือ - ยาว 3–100 ตัว");
            }

            if (!AccountRules.IsAllowedPassword(request.Password))
            {
                return Bad($"รหัสผ่านชั่วคราวต้องยาว {AccountRules.MinPassword}–{AccountRules.MaxPassword} ตัวอักษร");
            }

            if (await UsernameTakenAsync(db, username, exceptUserId: 0))
            {
                return Conflict(UsernameTaken);
            }

            user.Username = username;
            secret = request.Password!;
        }
        else
        {
            if (request.Username is not null || request.Password is not null)
            {
                return Bad("แม่บ้านและหัวหน้าใช้รหัสพนักงานกับเบอร์โทร ไม่ใช้ชื่อผู้ใช้หรือรหัสผ่าน");
            }

            if (AccountRules.NormalizeEmployeeId(request.EmployeeId) is not { } employeeId)
            {
                return Bad("รหัสพนักงานต้องเป็นตัวอักษรอังกฤษ ตัวเลข หรือ - ไม่เกิน 20 ตัว");
            }

            var phone = PhoneNumber.Normalize(request.Phone);
            if (!PhoneNumber.IsComplete(phone))
            {
                return Bad("เบอร์โทรต้องเป็นเลขอารบิก 10 หลัก ขึ้นต้นด้วย 0");
            }

            if (await EmployeeIdTakenAsync(db, employeeId, exceptUserId: 0))
            {
                return Conflict(EmployeeIdTaken);
            }

            user.EmployeeId = employeeId;
            secret = phone;
        }

        var (place, error) = await ReadPlaceAsync(db, role, userId: 0, request.AreaId, request.BuildingId, request.Shift);
        if (place is null)
        {
            return error!;
        }

        user.AreaId = place.Area?.Id;
        user.BuildingId = place.Building?.Id;
        user.Shift = place.Shift;
        // facility-0054: the phone is stored only as a PBKDF2 hash, like a password
        user.SecretHash = hasher.Hash(secret);
        db.Users.Add(user);

        var claim = ClaimOf(user);
        var conflict = await SaveAsync(db, claim, async () =>
        {
            // The audit row needs the new id, so it is a second save inside the same transaction
            AuditTrail.Add(
                db, admin.Id, user.CreatedAt, "ACCOUNT_CREATE", "users", user.Id,
                $"เพิ่มบัญชี{ThaiLabels.RoleName(role)} {user.LoginName} {user.DisplayName}",
                before: null, after: Snapshot(user));
            await db.SaveChangesAsync();
        });
        if (conflict is not null)
        {
            return conflict;
        }

        return Results.Created($"/api/admin/accounts/{user.Id}", await AccountView.LoadOneAsync(db, user.Id));
    }

    /// <summary>
    /// Saves what is tracked, then runs <paramref name="afterSave"/>, all in one transaction. Another save that took the
    /// login name or the slot first becomes a 409; any other database error is rethrown.
    /// </summary>
    internal static async Task<IResult?> SaveAsync(AppDbContext db, AccountClaim claim, Func<Task>? afterSave = null)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.SaveChangesAsync();
            if (afterSave is not null)
            {
                await afterSave();
            }

            await transaction.CommitAsync();
            return null;
        }
        catch (DbUpdateException)
        {
            if (await ExplainRaceAsync(db, claim) is { } conflict)
            {
                return conflict;
            }

            throw;
        }
    }
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add after `app.MapAdminAuditLogEndpoints();`:

```csharp
app.MapAdminAccountEndpoints();
```

- [ ] **Step 7: Run the tests to see them pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AccountEndpointTests"`
Expected: PASS.

If `The_list_shows_every_account_admins_first_and_never_a_secret` fails on the word "secret", read the raw JSON. No property may be named after a secret, and the DTO above has none, so the failure means something leaks. Fix the leak; do not weaken the assertion.

- [ ] **Step 8: Run every test**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS (3 MySQL tests skipped).

- [ ] **Step 9: Update the handoff and commit**

Add to `## Task log (plan 5)` in `docs/handoff.md`:

```markdown
- 2026-10-14 · Task 2 done: GET/POST /api/admin/accounts (list; add Cleaner, Supervisor or Admin; slot and login-name checks; ACCOUNT_CREATE). All tests pass. Next: Task 3 (edit, deactivate, activate).
```

```bash
git add backend docs/handoff.md
git commit -m "feat(backend): Admin accounts page lists accounts and adds a Cleaner, Supervisor or Admin one by one (facility-0020, 0054, 0056)"
```

---

### Task 3: Edit, deactivate, activate

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminAccountEndpoints.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AccountEditEndpointTests.cs`

**Interfaces:**
- Consumes: Task 2's `AccountChecks.*`, `AccountView.LoadOneAsync`, `Snapshot`, `SaveAsync`, `UpdateAccountRequest`; the existing `RefreshTokenRevocation.RevokeUserSessionsAsync(db, userId, nowUtc)`.
- Produces: `PUT /api/admin/accounts/{id}`, `POST /api/admin/accounts/{id}/deactivate`, `POST /api/admin/accounts/{id}/activate`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/FacilityRealtime.ApiTests/Admin/AccountEditEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Wireframe D6: edit, deactivate and activate. Deactivation logs the person out everywhere (facility-0014, 0020).</summary>
public class AccountEditEndpointTests
{
    private static Task<HttpResponseMessage> EditAsync(HttpClient admin, int id, object body) =>
        admin.PutAsJsonAsync($"{AccountApi.Path}/{id}", body);

    private static Task<HttpResponseMessage> DeactivateAsync(HttpClient admin, int id) =>
        admin.PostAsync($"{AccountApi.Path}/{id}/deactivate", null);

    private static Task<HttpResponseMessage> ActivateAsync(HttpClient admin, int id) =>
        admin.PostAsync($"{AccountApi.Path}/{id}/activate", null);

    [Fact]
    public async Task The_admin_renames_a_cleaner_and_moves_them_to_a_free_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1003 = await AdminSetupApi.UserIdAsync(factory, "E1003");
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03");

        var response = await EditAsync(admin, e1003, new { displayName = "สมศรี ย้ายที่", areaId = ar03, shift = "Day" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = await AccountApi.ReadAsync(response);
        Assert.Equal(("สมศรี ย้ายที่", "AR03", "E1003"), (edited.DisplayName, edited.AreaCode, edited.LoginName));
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("ACCOUNT_UPDATE", (long?)e1003), (entry.Action, entry.EntityId));
        Assert.Contains($"\"AreaId\":{ar02}", entry.BeforeJson);
        Assert.Contains($"\"AreaId\":{ar03}", entry.AfterJson);
    }

    [Fact]
    public async Task Saving_the_same_values_logs_nothing()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await EditAsync(admin, e1001, new { displayName = " สมชาย ใจดี ", areaId = ar01, shift = "Day" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Moving_into_a_taken_slot_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1003 = await AdminSetupApi.UserIdAsync(factory, "E1003");
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await EditAsync(admin, e1003, new { displayName = "สมศรี มีสุข", areaId = ar01, shift = "Day" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของ AR01 มีแม่บ้านประจำแล้ว (สมชาย ใจดี)", await AdminSetupApi.MessageAsync(response));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_supervisor_is_moved_to_another_building_shift()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var s2001 = await AdminSetupApi.UserIdAsync(factory, "S2001");
        var buildingB = await TestData.AddBuildingAsync(factory, "B");

        var response = await EditAsync(admin, s2001, new { displayName = "สมปอง ตรวจดี", buildingId = buildingB, shift = "Night" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = await AccountApi.ReadAsync(response);
        Assert.Equal(("B", "Night"), (edited.BuildingCode, edited.Shift));
    }

    [Fact]
    public async Task Deactivating_a_cleaner_logs_them_out_and_frees_their_area_slot()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (_, _, cleanerRefresh) = await AuthApi.LoggedInAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await DeactivateAsync(admin, e1001);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await AccountApi.ReadAsync(response);
        Assert.Equal((false, (int?)null), (account.IsActive, account.AreaId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(factory.CreateApiClient(), cleanerRefresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.LoginAsync(factory.CreateApiClient())).StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ACCOUNT_DEACTIVATE", entry.Action);
        Assert.Contains($"\"AreaId\":{ar01}", entry.BeforeJson);
        Assert.Contains("\"IsActive\":false", entry.AfterJson);

        // AR01's day slot is open again
        Assert.Equal(HttpStatusCode.Created, (await AccountApi.CreateCleanerAsync(admin, "E2001", "0000000011", ar01, "Day")).StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_their_own_account()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await DeactivateAsync(admin, adminAuth.User.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ปิดบัญชีของตัวเองไม่ได้", await AdminSetupApi.MessageAsync(response));
        Assert.True(await AdminSetupApi.ReadAsync(factory, db => db.Users.Where(u => u.Id == adminAuth.User.Id).Select(u => u.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task Deactivating_twice_logs_once()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1002 = await AdminSetupApi.UserIdAsync(factory, "E1002");

        await DeactivateAsync(admin, e1002);
        var again = await DeactivateAsync(admin, e1002);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task An_activated_cleaner_logs_in_again_without_an_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        await DeactivateAsync(admin, e1001);

        var response = await ActivateAsync(admin, e1001);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await AccountApi.ReadAsync(response);
        Assert.Equal((true, (int?)null), (account.IsActive, account.AreaId));
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(factory.CreateApiClient())).StatusCode);
        Assert.Equal(new[] { "ACCOUNT_DEACTIVATE", "ACCOUNT_ACTIVATE" }, (await AdminSetupApi.AuditAsync(factory)).Select(e => e.Action));
    }

    [Fact]
    public async Task A_supervisor_whose_slot_was_filled_cannot_be_activated()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var s2001 = await AdminSetupApi.UserIdAsync(factory, "S2001");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        await DeactivateAsync(admin, s2001);
        Assert.Equal(HttpStatusCode.Created, (await AccountApi.CreateSupervisorAsync(admin, "S2002", "0000000021", buildingA, "Day", "หัวหน้าแทน ทดสอบ")).StatusCode);

        var response = await ActivateAsync(admin, s2001);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของตึก A มีหัวหน้าประจำแล้ว (หัวหน้าแทน ทดสอบ)", await AdminSetupApi.MessageAsync(response));
        Assert.False(await AdminSetupApi.ReadAsync(factory, db => db.Users.Where(u => u.Id == s2001).Select(u => u.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task A_deactivated_cleaner_gets_no_area_until_activated()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03");
        await DeactivateAsync(admin, e1001);

        var response = await EditAsync(admin, e1001, new { displayName = "สมชาย ใจดี", areaId = ar03, shift = "Day" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("เปิดใช้งานบัญชีก่อน แล้วค่อยเลือก Area", await AdminSetupApi.MessageAsync(response));
    }

    [Theory]
    [InlineData("PUT", "/api/admin/accounts/9999")]
    [InlineData("POST", "/api/admin/accounts/9999/deactivate")]
    [InlineData("POST", "/api/admin/accounts/9999/activate")]
    public async Task An_unknown_account_is_404(string method, string path)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "PUT" ? JsonContent.Create(new { displayName = "ก", shift = "Day" }) : null,
        };

        var response = await admin.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ไม่พบบัญชีนี้", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_cleaner_cannot_edit_accounts()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await DeactivateAsync(cleaner, auth.User.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await EditAsync(cleaner, auth.User.Id, new { displayName = "ก", shift = "Day" })).StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AccountEditEndpointTests"`
Expected: FAIL; the routes answer 404 or 405.

- [ ] **Step 3: Add the edit and activation endpoints**

In `AdminAccountEndpoints.MapAdminAccountEndpoints`, add after `accounts.MapPost("", CreateAsync);`:

```csharp
        accounts.MapPut("/{id:int}", UpdateAsync);
        accounts.MapPost("/{id:int}/deactivate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, false, principal, db, clock));
        accounts.MapPost("/{id:int}/activate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, true, principal, db, clock));
```

Add these methods to the class:

```csharp
    private static async Task<IResult> UpdateAsync(int id, UpdateAccountRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        if (AccountRules.NormalizeDisplayName(request.DisplayName) is not { } displayName)
        {
            return Bad($"ต้องใส่ชื่อที่แสดง ไม่เกิน {AccountRules.MaxDisplayName} ตัวอักษร");
        }

        // Decision 4: a deactivated Cleaner holds no Area; the Area's slot may already be someone else's
        if (!user.IsActive && user.Role == UserRole.Cleaner && request.AreaId is not null)
        {
            return Bad("เปิดใช้งานบัญชีก่อน แล้วค่อยเลือก Area");
        }

        var (place, error) = await ReadPlaceAsync(db, user.Role, user.Id, request.AreaId, request.BuildingId, request.Shift);
        if (place is null)
        {
            return error!;
        }

        var before = Snapshot(user);
        user.DisplayName = displayName;
        user.AreaId = place.Area?.Id;
        user.BuildingId = place.Building?.Id;
        user.Shift = place.Shift;
        var after = Snapshot(user);
        if (after == before)
        {
            return Results.Ok(await AccountView.LoadOneAsync(db, user.Id));
        }

        AuditTrail.Add(
            db, admin.Id, clock.GetUtcNow().UtcDateTime, "ACCOUNT_UPDATE", "users", user.Id,
            $"แก้บัญชี{ThaiLabels.RoleName(user.Role)} {user.LoginName} {user.DisplayName}", before, after);
        if (await SaveAsync(db, ClaimOf(user)) is { } conflict)
        {
            return conflict;
        }

        return Results.Ok(await AccountView.LoadOneAsync(db, user.Id));
    }

    /// <summary>
    /// facility-0020: no delete, only deactivate. A deactivated account cannot log in and every refresh token is revoked
    /// (facility-0014), so its devices drop within 5 minutes. History keeps its name.
    /// </summary>
    private static async Task<IResult> SetActiveAsync(int id, bool active, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        // facility-0020: so one active Admin always remains
        if (!active && user.Id == admin.Id)
        {
            return Bad("ปิดบัญชีของตัวเองไม่ได้");
        }

        if (user.IsActive == active)
        {
            return Results.Ok(await AccountView.LoadOneAsync(db, user.Id));
        }

        if (active && await HolderAsync(db, user.Role, user.AreaId, user.BuildingId, user.Shift, user.Id) is { } holder)
        {
            return Conflict(SlotTaken(holder));
        }

        var before = Snapshot(user);
        user.IsActive = active;
        if (!active && user.Role == UserRole.Cleaner)
        {
            // Decision 3: the Area's slot opens at once, so the Area form (D10) can offer it to someone else
            user.AreaId = null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        AuditTrail.Add(
            db, admin.Id, now, active ? "ACCOUNT_ACTIVATE" : "ACCOUNT_DEACTIVATE", "users", user.Id,
            $"{(active ? "เปิด" : "ปิด")}ใช้งานบัญชี{ThaiLabels.RoleName(user.Role)} {user.LoginName} {user.DisplayName}",
            before, Snapshot(user));
        Func<Task>? revokeSessions = active ? null : () => db.RevokeUserSessionsAsync(user.Id, now);
        var conflict = await SaveAsync(db, ClaimOf(user), revokeSessions);
        if (conflict is not null)
        {
            return conflict;
        }

        return Results.Ok(await AccountView.LoadOneAsync(db, user.Id));
    }
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AccountEditEndpointTests|FullyQualifiedName~AccountEndpointTests"`
Expected: PASS.

- [ ] **Step 5: Run every test**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS (3 MySQL tests skipped).

- [ ] **Step 6: Update the handoff and commit**

Add to `## Task log (plan 5)` in `docs/handoff.md`:

```markdown
- 2026-10-14 · Task 3 done: PUT /api/admin/accounts/{id}, /deactivate, /activate (deactivation revokes sessions and frees the Cleaner's Area; no self-deactivation; slot check on activation). All tests pass. Next: Task 4 (phone and password).
```

```bash
git add backend docs/handoff.md
git commit -m "feat(backend): Admin edits, deactivates and activates accounts; deactivation logs the person out everywhere (facility-0014, 0020)"
```

---

### Task 4: Change phone, reset password, history never holds a secret

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminAccountEndpoints.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AccountSecretEndpointTests.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Admin/DeactivatedAdminTests.cs`

**Interfaces:**
- Consumes: Task 2's `AccountPhoneRequest`, `AccountPasswordRequest`, `AccountChecks.Bad/NotFound`; `IPasswordHasher.Hash/Verify`; `RevokeUserSessionsAsync`.
- Produces: `PUT /api/admin/accounts/{id}/phone` and `PUT /api/admin/accounts/{id}/password`, both 204.

- [ ] **Step 1: Write the failing tests**

`backend/tests/FacilityRealtime.ApiTests/Admin/AccountSecretEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// facility-0054: "แก้เบอร์โทร" for Cleaners and Supervisors, "รีเซ็ตรหัสผ่าน" for Admins. Either one logs the person out on
/// every device, and neither the old nor the new value ever reaches the log (facility-0057).
/// </summary>
public class AccountSecretEndpointTests
{
    private static Task<HttpResponseMessage> PhoneAsync(HttpClient admin, int id, string? phone) =>
        admin.PutAsJsonAsync($"{AccountApi.Path}/{id}/phone", new { phone });

    private static Task<HttpResponseMessage> PasswordAsync(HttpClient admin, int id, string? password) =>
        admin.PutAsJsonAsync($"{AccountApi.Path}/{id}/password", new { password });

    [Fact]
    public async Task A_new_phone_replaces_the_old_one_and_logs_the_cleaner_out()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (_, _, cleanerRefresh) = await AuthApi.LoggedInAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var response = await PhoneAsync(admin, e1001, "000 000 0031");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.LoginAsync(factory.CreateApiClient(), "E1001", "0810000001")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(factory.CreateApiClient(), "E1001", "0000000031")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(factory.CreateApiClient(), cleanerRefresh)).StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("ACCOUNT_PHONE", (long?)e1001, (string?)null, (string?)null), (entry.Action, entry.EntityId, entry.BeforeJson, entry.AfterJson));
        Assert.Equal("แก้เบอร์โทรของ E1001 สมชาย ใจดี", entry.Summary);
    }

    [Fact]
    public async Task The_same_phone_again_changes_nothing()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (_, _, cleanerRefresh) = await AuthApi.LoggedInAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var response = await PhoneAsync(admin, e1001, "081-000-0001");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.RefreshAsync(factory.CreateApiClient(), cleanerRefresh)).StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Theory]
    [InlineData("๐๘๑๐๐๐๐๐๓๑")]
    [InlineData("081000003")]
    [InlineData(null)]
    public async Task An_incomplete_phone_is_refused(string? phone)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var response = await PhoneAsync(admin, e1001, phone);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_supervisor_phone_can_be_changed_too()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var s2001 = await AdminSetupApi.UserIdAsync(factory, "S2001");

        Assert.Equal(HttpStatusCode.NoContent, (await PhoneAsync(admin, s2001, "0000000041")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(factory.CreateApiClient(), "S2001", "0000000041")).StatusCode);
    }

    [Fact]
    public async Task An_admin_has_no_phone_and_a_cleaner_has_no_password()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var phone = await PhoneAsync(admin, adminAuth.User.Id, "0000000051");
        var password = await PasswordAsync(admin, e1001, "new-password-1");

        Assert.Equal(HttpStatusCode.BadRequest, phone.StatusCode);
        Assert.Equal("บัญชี Admin ไม่มีเบอร์โทร ใช้ปุ่มรีเซ็ตรหัสผ่าน", await AdminSetupApi.MessageAsync(phone));
        Assert.Equal(HttpStatusCode.BadRequest, password.StatusCode);
        Assert.Equal("แม่บ้านและหัวหน้าไม่มีรหัสผ่าน ใช้ปุ่มแก้เบอร์โทร", await AdminSetupApi.MessageAsync(password));
    }

    [Fact]
    public async Task A_reset_password_lets_the_other_admin_in_with_the_new_one_only()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var created = await AccountApi.ReadAsync(await AccountApi.CreateAdminAsync(admin, "admin2", "temporary-1"));
        var other = await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin2", "temporary-1");
        var otherRefresh = AuthApi.RefreshCookieValue(other);

        var response = await PasswordAsync(admin, created.Id, "temporary-2");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin2", "temporary-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin2", "temporary-2")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(factory.CreateApiClient(), otherRefresh)).StatusCode);
        var entry = (await AdminSetupApi.AuditAsync(factory)).Last();
        Assert.Equal(("ACCOUNT_PASSWORD", (string?)null, (string?)null), (entry.Action, entry.BeforeJson, entry.AfterJson));
    }

    [Fact]
    public async Task A_too_short_password_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await PasswordAsync(admin, adminAuth.User.Id, "short")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_account_is_404()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await PhoneAsync(admin, 9999, "0000000061")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PasswordAsync(admin, 9999, "temporary-9")).StatusCode);
    }

    /// <summary>The check docs/handoff.md asked for before account actions are logged: the history page never shows a secret.</summary>
    [Fact]
    public async Task The_history_page_never_shows_a_phone_a_password_a_hash_or_a_qr_token()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        var created = await AccountApi.ReadAsync(await AccountApi.CreateCleanerAsync(admin, "E2001", "0000000071", areaId: null));
        await PhoneAsync(admin, e1001, "0000000072");
        var newAdmin = await AccountApi.ReadAsync(await AccountApi.CreateAdminAsync(admin, "admin2", "temporary-71"));
        await PasswordAsync(admin, newAdmin.Id, "temporary-72");
        await admin.PostAsync($"{AccountApi.Path}/{created.Id}/deactivate", null);
        var signId = await AdminSetupApi.SignIdAsync(factory, "AR01-01");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/signs/{signId}/regenerate-token", null)).StatusCode);
        var newToken = await AdminSetupApi.ReadAsync(factory, db => db.Signs.Where(s => s.Id == signId).Select(s => s.QrToken).SingleAsync());

        var raw = await admin.GetStringAsync("/api/admin/audit-log");

        Assert.Equal(6, (await AdminSetupApi.AuditAsync(factory)).Count);
        foreach (var secret in new[] { "0000000071", "0000000072", "0810000001", "temporary-71", "temporary-72", "pbkdf2", "token-", newToken })
        {
            Assert.DoesNotContain(secret, raw);
        }
    }
}
```

Add `using Microsoft.EntityFrameworkCore;` to the top of this test file for `SingleAsync`.

In `backend/tests/FacilityRealtime.ApiTests/Admin/DeactivatedAdminTests.cs`, add these rows above the `[InlineData("GET", "/api/admin/audit-log", null)]` row:

```csharp
    [InlineData("GET", "/api/admin/accounts", null)]
    [InlineData("POST", "/api/admin/accounts", """{ "role": "Cleaner", "displayName": "x", "employeeId": "E9", "phone": "0000000099", "shift": "Day" }""")]
    [InlineData("PUT", "/api/admin/accounts/2", """{ "displayName": "x", "shift": "Day" }""")]
    [InlineData("PUT", "/api/admin/accounts/2/phone", """{ "phone": "0000000099" }""")]
    [InlineData("PUT", "/api/admin/accounts/1/password", """{ "password": "temporary-9" }""")]
    [InlineData("POST", "/api/admin/accounts/2/deactivate", null)]
    [InlineData("POST", "/api/admin/accounts/2/activate", null)]
```

Also change the class summary's "every new Admin page" to "every Admin page".

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AccountSecretEndpointTests|FullyQualifiedName~DeactivatedAdminTests"`
Expected: FAIL, because the phone and password routes do not exist. The `DeactivatedAdminTests` rows for Tasks 2–3 already pass.

- [ ] **Step 3: Add the two endpoints**

In `AdminAccountEndpoints.MapAdminAccountEndpoints`, add after the `activate` mapping:

```csharp
        accounts.MapPut("/{id:int}/phone", ChangePhoneAsync);
        accounts.MapPut("/{id:int}/password", ResetPasswordAsync);
```

Add these methods:

```csharp
    /// <summary>facility-0054 rule 4: the Admin types the Cleaner's or Supervisor's new phone; every device of theirs must log in again.</summary>
    private static async Task<IResult> ChangePhoneAsync(
        int id, AccountPhoneRequest request, ClaimsPrincipal principal, AppDbContext db, IPasswordHasher hasher, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Role == UserRole.Admin)
        {
            return Bad("บัญชี Admin ไม่มีเบอร์โทร ใช้ปุ่มรีเซ็ตรหัสผ่าน");
        }

        var phone = PhoneNumber.Normalize(request.Phone);
        if (!PhoneNumber.IsComplete(phone))
        {
            return Bad("เบอร์โทรต้องเป็นเลขอารบิก 10 หลัก ขึ้นต้นด้วย 0");
        }

        await ReplaceSecretAsync(db, hasher, clock, admin, user, phone, "ACCOUNT_PHONE", $"แก้เบอร์โทรของ {user.LoginName} {user.DisplayName}");
        return Results.NoContent();
    }

    /// <summary>facility-0020: the Admin gives another Admin (or themselves) a new temporary password, told by voice or on paper.</summary>
    private static async Task<IResult> ResetPasswordAsync(
        int id, AccountPasswordRequest request, ClaimsPrincipal principal, AppDbContext db, IPasswordHasher hasher, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Role != UserRole.Admin)
        {
            return Bad("แม่บ้านและหัวหน้าไม่มีรหัสผ่าน ใช้ปุ่มแก้เบอร์โทร");
        }

        if (!AccountRules.IsAllowedPassword(request.Password))
        {
            return Bad($"รหัสผ่านชั่วคราวต้องยาว {AccountRules.MinPassword}–{AccountRules.MaxPassword} ตัวอักษร");
        }

        await ReplaceSecretAsync(db, hasher, clock, admin, user, request.Password!, "ACCOUNT_PASSWORD", $"รีเซ็ตรหัสผ่านของ {user.LoginName} {user.DisplayName}");
        return Results.NoContent();
    }

    /// <summary>
    /// The same secret again changes nothing and logs nothing. A new one is hashed, logged with no before or after
    /// (facility-0057), and every refresh token of the person is revoked (facility-0014).
    /// </summary>
    private static async Task ReplaceSecretAsync(
        AppDbContext db, IPasswordHasher hasher, TimeProvider clock, User admin, User user, string secret, string action, string summary)
    {
        if (hasher.Verify(secret, user.SecretHash))
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        user.SecretHash = hasher.Hash(secret);
        AuditTrail.Add(db, admin.Id, now, action, "users", user.Id, summary, before: null, after: null);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        await db.RevokeUserSessionsAsync(user.Id, now);
        await transaction.CommitAsync();
    }
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~Account|FullyQualifiedName~DeactivatedAdminTests"`
Expected: PASS.

- [ ] **Step 5: Run every test**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS (3 MySQL tests skipped).

- [ ] **Step 6: Update the handoff and commit**

In `docs/handoff.md`, delete the "Before plan 5" bullet that asks for the history test (this task adds it). Then add to `## Task log (plan 5)`:

```markdown
- 2026-10-14 · Task 4 done: PUT /api/admin/accounts/{id}/phone and /password (sessions revoked, nothing secret logged); test that the history never shows a phone, password, hash or QR Token. All tests pass. Next: Task 5 (export rules and query).
```

```bash
git add backend docs/handoff.md
git commit -m "feat(backend): Admin changes a phone or resets an Admin password; both log the person out and the history never holds a secret (facility-0054, 0057)"
```

---

### Task 5: Export rules and export query

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Export/ExportRules.cs`
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/ExportQuery.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs` (+ `AddAttendanceAsync`, `BackdateSetupAsync`)
- Test: `backend/tests/FacilityRealtime.UnitTests/ExportRulesTests.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Persistence/ExportQueryTests.cs`

**Interfaces:**
- Consumes: existing `RoundWindow.For`, `ShiftSlot`, `ShiftCalendar.EndUtc`.
- Produces, for Task 6:
  - `ExportSet { Attendance, Submissions }`, `ExportRules.MaxDays = 31`.
  - `ExportRules.MissedRounds(IEnumerable<(RoundWindow Window, DateTime CreatedAtUtc)> windows, IReadOnlySet<int> doneWindowIds, ShiftSlot slot, DateTime nowUtc) → IReadOnlyList<RoundWindow>`.
  - `ExportQuery.LoadAttendanceAsync(db, DateOnly first, DateOnly last, int? buildingId) → List<AttendanceExportRow>`.
  - `ExportQuery.LoadSubmissionsAsync(db, first, last, buildingId) → List<SubmissionExportRow>`.
  - `ExportQuery.LoadPointsAsync(db, buildingId) → List<ExportPoint>`.
  - Records `AttendanceExportRow`, `SubmissionExportRow`, `ExportPoint`, `ExportWindow`.
  - Test helpers `TestData.AddAttendanceAsync`, `TestData.BackdateSetupAsync`.

- [ ] **Step 1: Write the failing unit tests**

`backend/tests/FacilityRealtime.UnitTests/ExportRulesTests.cs`:

```csharp
using FacilityRealtime.Application.Export;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0047, 0053: the export lists every round of an ended shift that nobody did, as "ไม่ได้ทำ".</summary>
public class ExportRulesTests
{
    private static readonly ShiftSlot Day8 = new(new DateOnly(2026, 10, 8), Shift.Day);
    private static readonly DateTime LongAgo = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AfterTheShift = ShiftCalendar.EndUtc(Day8).AddMinutes(1);

    private static (RoundWindow Window, DateTime CreatedAtUtc) Window(int id, int startHour, int endHour, DateTime? createdAtUtc = null) =>
        (RoundWindow.For(id, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0), Day8), createdAtUtc ?? LongAgo);

    [Fact]
    public void Rounds_with_no_counted_submission_are_missed_in_the_order_of_the_shift()
    {
        var missed = ExportRules.MissedRounds([Window(1, 16, 18), Window(2, 7, 9)], new HashSet<int>(), Day8, AfterTheShift);

        Assert.Equal(new[] { 2, 1 }, missed.Select(w => w.Id));
    }

    [Fact]
    public void A_done_round_is_not_missed()
    {
        var missed = ExportRules.MissedRounds([Window(1, 7, 9), Window(2, 16, 18)], new HashSet<int> { 1 }, Day8, AfterTheShift);

        Assert.Equal(new[] { 2 }, missed.Select(w => w.Id));
    }

    [Fact]
    public void Nothing_is_missed_while_the_shift_is_still_running()
    {
        var missed = ExportRules.MissedRounds([Window(1, 7, 9)], new HashSet<int>(), Day8, ShiftCalendar.EndUtc(Day8).AddMinutes(-1));

        Assert.Empty(missed);
    }

    [Fact]
    public void A_window_added_after_its_round_started_does_not_count_for_that_shift()
    {
        var startUtc = RoundWindow.For(1, new TimeOnly(7, 0), new TimeOnly(9, 0), Day8).StartUtc;

        var missed = ExportRules.MissedRounds([Window(1, 7, 9, startUtc.AddMinutes(1))], new HashSet<int>(), Day8, AfterTheShift);

        Assert.Empty(missed);
    }

    [Fact]
    public void A_night_round_after_midnight_comes_after_the_evening_one()
    {
        var night8 = new ShiftSlot(new DateOnly(2026, 10, 8), Shift.Night);
        (RoundWindow, DateTime) Night(int id, int start, int end) => (RoundWindow.For(id, new TimeOnly(start, 0), new TimeOnly(end, 0), night8), LongAgo);

        var missed = ExportRules.MissedRounds([Night(1, 3, 5), Night(2, 20, 22)], new HashSet<int>(), night8, ShiftCalendar.EndUtc(night8));

        Assert.Equal(new[] { 2, 1 }, missed.Select(w => w.Id));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~ExportRulesTests"`
Expected: build FAILS; `ExportRules` is not defined.

- [ ] **Step 3: Write the rules**

`backend/src/FacilityRealtime.Application/Export/ExportRules.cs`:

```csharp
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;

namespace FacilityRealtime.Application.Export;

/// <summary>The sets the Admin ticks on wireframe D8. The inspection set comes with the Supervisor plan.</summary>
public enum ExportSet
{
    Attendance,
    Submissions,
}

/// <summary>facility-0053: Excel by date range instead of report pages.</summary>
public static class ExportRules
{
    /// <summary>The longest range one file covers, in shift dates.</summary>
    public const int MaxDays = 31;

    /// <summary>
    /// facility-0047 rule 5 "ไม่ได้ทำ": the rounds of one point in one shift with no OnTime, Late or Rework submission.
    /// Only once the shift has ended, because a Late Submission can still come until then. A window created after its
    /// round started did not exist for that round. Returned in the order the shift meets them.
    /// </summary>
    public static IReadOnlyList<RoundWindow> MissedRounds(
        IEnumerable<(RoundWindow Window, DateTime CreatedAtUtc)> windows, IReadOnlySet<int> doneWindowIds, ShiftSlot slot, DateTime nowUtc) =>
        ShiftCalendar.EndUtc(slot) > nowUtc
            ? []
            : windows
                .Where(w => w.CreatedAtUtc <= w.Window.StartUtc && !doneWindowIds.Contains(w.Window.Id))
                .Select(w => w.Window)
                .OrderBy(w => w.StartUtc)
                .ToList();
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~ExportRulesTests"`
Expected: PASS.

- [ ] **Step 5: Add the test helpers**

Append to `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs`, inside the class:

```csharp
    /// <summary>
    /// One attendance event of a Cleaner in their own Area, written straight to the database. A scan gets the Area's
    /// check-in sign and a GPS verdict; an Admin addition has neither (facility-0051).
    /// </summary>
    public static Task AddAttendanceAsync(
        FacilityApiFactory factory,
        string employeeId,
        DateOnly shiftDate,
        Shift shift,
        AttendanceEvent eventType,
        DateTimeOffset at,
        AttendanceSource source = AttendanceSource.Scan,
        short distanceM = 12,
        bool withinRadius = true) =>
        factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var sign = await db.Signs.SingleAsync(s => s.AreaId == user.AreaId && s.ServicePointId == null);
            var scanned = source != AttendanceSource.AdminAdd;
            db.ShiftAttendances.Add(new ShiftAttendance
            {
                UserId = user.Id,
                AreaId = user.AreaId!.Value,
                ShiftDate = shiftDate,
                Shift = shift,
                EventType = eventType,
                OccurredAt = at.UtcDateTime,
                Source = source,
                SignId = scanned ? sign.Id : null,
                Latitude = scanned ? 13.756300m : null,
                Longitude = scanned ? 100.501800m : null,
                AccuracyM = scanned ? (short)10 : null,
                DistanceM = scanned ? distanceM : null,
                WithinRadius = scanned ? withinRadius : null,
                CreatedAt = at.UtcDateTime,
            });
            await db.SaveChangesAsync();
        });

    /// <summary>
    /// The seed stamps its points and windows with the real clock, which is later than the October 2026 test dates. Move
    /// them back to 1 October so rounds on test dates existed when they started (export "ไม่ได้ทำ").
    /// </summary>
    public static Task BackdateSetupAsync(FacilityApiFactory factory) =>
        factory.WithDbAsync(async db =>
        {
            var longAgo = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.PointRoundWindows.ExecuteUpdateAsync(s => s.SetProperty(w => w.CreatedAt, longAgo));
            await db.ServicePoints.ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, longAgo));
        });
```

- [ ] **Step 6: Write the failing query tests**

`backend/tests/FacilityRealtime.ApiTests/Persistence/ExportQueryTests.cs`:

```csharp
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>facility-0053: the rows the Excel export reads, by shift date and building, with people and places joined.</summary>
public class ExportQueryTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);
    private static readonly DateOnly Oct9 = new(2026, 10, 9);

    private static async Task<T> QueryAsync<T>(FacilityApiFactory factory, Func<AppDbContext, Task<T>> query)
    {
        T result = default!;
        await factory.WithDbAsync(async db => result = await query(db));
        return result;
    }

    [Fact]
    public async Task Attendance_rows_carry_the_person_and_place_inside_the_date_range_only()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftOut, ThaiClock.At(8, 19, 2), AttendanceSource.AdminAdd);
        await TestData.AddAttendanceAsync(factory, "E1001", new DateOnly(2026, 10, 10), Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(10, 6, 59));

        var rows = await QueryAsync(factory, db => ExportQuery.LoadAttendanceAsync(db, Oct8, Oct9, buildingId: null));

        Assert.Equal(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut }, rows.Select(r => r.EventType));
        var shiftIn = rows[0];
        Assert.Equal(("E1001", "สมชาย ใจดี", "A", "AR01", Oct8, Shift.Day), (shiftIn.EmployeeId, shiftIn.DisplayName, shiftIn.BuildingCode, shiftIn.AreaCode, shiftIn.ShiftDate, shiftIn.Shift));
        Assert.Equal((AttendanceSource.Scan, (short?)12, (bool?)true), (shiftIn.Source, shiftIn.DistanceM, shiftIn.WithinRadius));
        Assert.Equal(ThaiClock.At(8, 6, 58).UtcDateTime, DateTime.SpecifyKind(shiftIn.OccurredAtUtc, DateTimeKind.Utc));
        Assert.Equal((AttendanceSource.AdminAdd, (bool?)null), (rows[1].Source, rows[1].WithinRadius));
    }

    [Fact]
    public async Task The_building_filter_leaves_out_other_buildings()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var buildingA = await QueryAsync(factory, db => db.Buildings.Where(b => b.Code == "A").Select(b => b.Id).SingleAsync());

        Assert.Empty(await QueryAsync(factory, db => ExportQuery.LoadAttendanceAsync(db, Oct8, Oct8, buildingB)));
        Assert.Empty(await QueryAsync(factory, db => ExportQuery.LoadSubmissionsAsync(db, Oct8, Oct8, buildingB)));
        Assert.Empty(await QueryAsync(factory, db => ExportQuery.LoadPointsAsync(db, buildingB)));
        Assert.Single(await QueryAsync(factory, db => ExportQuery.LoadAttendanceAsync(db, Oct8, Oct8, buildingA)));
        Assert.Single(await QueryAsync(factory, db => ExportQuery.LoadSubmissionsAsync(db, Oct8, Oct8, buildingA)));
    }

    [Fact]
    public async Task Submission_rows_carry_point_sign_round_and_person()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10), CleaningStatus.Issue, issueTags: "wet_floor");

        var row = Assert.Single(await QueryAsync(factory, db => ExportQuery.LoadSubmissionsAsync(db, Oct8, Oct8, null)));

        Assert.Equal(("A", "AR01", "AR01-01", Oct8, Shift.Day), (row.BuildingCode, row.AreaCode, row.SignCode, row.ShiftDate, row.Shift));
        Assert.Equal((new TimeOnly(7, 0), new TimeOnly(9, 0), Placement.OnTime), (row.RoundStart!.Value, row.RoundEnd!.Value, row.Placement));
        Assert.NotNull(row.RoundWindowId);
        Assert.Equal((CleaningStatus.Issue, "wet_floor", false), (row.Status, row.IssueTags, row.IsCover));
        Assert.Equal(("E1001", "สมชาย ใจดี"), (row.EmployeeId, row.DisplayName));
        Assert.False(string.IsNullOrEmpty(row.PointName));
    }

    [Fact]
    public async Task Points_come_with_their_windows_and_skip_deactivated_points_and_areas()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var womenRestroom = await db.Signs.Where(s => s.Code == "AR01-02").Select(s => s.ServicePoint!).SingleAsync();
            womenRestroom.IsActive = false;
            await db.SaveChangesAsync();
        });

        var points = await QueryAsync(factory, db => ExportQuery.LoadPointsAsync(db, null));

        Assert.Equal(new[] { "AR01-01", "AR02-01" }, points.Select(p => p.SignCode));
        Assert.Equal(4, points[0].Windows.Count);
        Assert.Equal((ShiftPattern.DayOnly, 1), (points[1].ShiftPattern, points[1].Windows.Count));

        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Code == "AR02")).IsActive = false;
            await db.SaveChangesAsync();
        });
        Assert.Equal(new[] { "AR01-01" }, (await QueryAsync(factory, db => ExportQuery.LoadPointsAsync(db, null))).Select(p => p.SignCode));
    }
}
```

The seed gives each AR01 restroom four windows (Day 07–09, 16–18; Night 20–22, 03–05) and AR02's meeting room one (Day 08–10).

- [ ] **Step 7: Run them to see them fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~ExportQueryTests"`
Expected: build FAILS; `ExportQuery` is not defined.

- [ ] **Step 8: Write the query**

`backend/src/FacilityRealtime.Infrastructure/Persistence/ExportQuery.cs`:

```csharp
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>One attendance event with its person and place. No coordinates: distance and verdict only (facility-0053).</summary>
public sealed record AttendanceExportRow(
    int UserId,
    string EmployeeId,
    string DisplayName,
    string BuildingCode,
    string AreaCode,
    DateOnly ShiftDate,
    Shift Shift,
    AttendanceEvent EventType,
    DateTime OccurredAtUtc,
    AttendanceSource Source,
    short? DistanceM,
    bool? WithinRadius);

/// <summary>One Scan Record with its point, sign, round and Cleaner. No coordinates.</summary>
public sealed record SubmissionExportRow(
    DateOnly ShiftDate,
    Shift Shift,
    string BuildingCode,
    string AreaCode,
    string PointName,
    string SignCode,
    int? RoundWindowId,
    TimeOnly? RoundStart,
    TimeOnly? RoundEnd,
    DateTime SubmittedAtUtc,
    Placement Placement,
    int? LateMinutes,
    CleaningStatus Status,
    string? IssueTags,
    bool IsCover,
    string EmployeeId,
    string DisplayName,
    short? DistanceM,
    bool? WithinRadius);

public sealed record ExportWindow(int Id, Shift Shift, TimeOnly Start, TimeOnly End, DateTime CreatedAtUtc);

/// <summary>An active point of an active Area with its Round Windows, for the "ไม่ได้ทำ" rows.</summary>
public sealed record ExportPoint(
    int Id, string BuildingCode, string AreaCode, ShiftPattern ShiftPattern, string PointName, string SignCode, IReadOnlyList<ExportWindow> Windows);

/// <summary>
/// facility-0053: what the Excel export reads, by Thai shift date (both ends included) and optionally one building.
/// In Infrastructure so a [MySqlFact] can run it on the Oracle provider.
/// </summary>
public static class ExportQuery
{
    public static Task<List<AttendanceExportRow>> LoadAttendanceAsync(AppDbContext db, DateOnly first, DateOnly last, int? buildingId) =>
        (from a in db.ShiftAttendances.AsNoTracking()
         join area in db.Areas on a.AreaId equals area.Id
         where a.ShiftDate >= first && a.ShiftDate <= last && (buildingId == null || area.BuildingId == buildingId)
         orderby a.ShiftDate, a.Shift, area.Code, a.UserId, a.OccurredAt
         select new AttendanceExportRow(
             a.UserId,
             a.User!.EmployeeId ?? string.Empty,
             a.User.DisplayName,
             area.Building!.Code,
             area.Code,
             a.ShiftDate,
             a.Shift,
             a.EventType,
             a.OccurredAt,
             a.Source,
             a.DistanceM,
             a.WithinRadius))
        .ToListAsync();

    public static Task<List<SubmissionExportRow>> LoadSubmissionsAsync(AppDbContext db, DateOnly first, DateOnly last, int? buildingId) =>
        db.ScanRecords.AsNoTracking()
            .Where(s => s.ShiftDate >= first && s.ShiftDate <= last
                && (buildingId == null || s.ServicePoint!.Area!.BuildingId == buildingId))
            .OrderBy(s => s.ShiftDate).ThenBy(s => s.Shift).ThenBy(s => s.SubmittedAt)
            .Select(s => new SubmissionExportRow(
                s.ShiftDate,
                s.Shift,
                s.ServicePoint!.Area!.Building!.Code,
                s.ServicePoint.Area.Code,
                s.ServicePoint.Name,
                s.Sign!.Code,
                s.RoundWindowId,
                s.RoundStart,
                s.RoundEnd,
                s.SubmittedAt,
                s.Placement,
                s.LateMinutes,
                s.Status,
                s.IssueTags,
                s.CoverAssignmentId != null,
                s.User!.EmployeeId ?? string.Empty,
                s.User.DisplayName,
                s.DistanceM,
                s.WithinRadius))
            .ToListAsync();

    /// <summary>Points as they are set up now (decision 12): active points of active Areas, by Area code then card order.</summary>
    public static async Task<List<ExportPoint>> LoadPointsAsync(AppDbContext db, int? buildingId)
    {
        var points = await db.ServicePoints.AsNoTracking()
            .Where(p => p.IsActive && p.Area!.IsActive && (buildingId == null || p.Area.BuildingId == buildingId))
            .Select(p => new { p.Id, BuildingCode = p.Area!.Building!.Code, AreaCode = p.Area.Code, p.Area.ShiftPattern, p.Name, p.SortOrder })
            .ToListAsync();
        var ids = points.Select(p => p.Id).ToList();
        var signs = await db.Signs.AsNoTracking()
            .Where(s => s.ServicePointId != null && ids.Contains(s.ServicePointId.Value))
            .Select(s => new { PointId = s.ServicePointId!.Value, s.Code })
            .ToListAsync();
        var windows = await db.PointRoundWindows.AsNoTracking().Where(w => ids.Contains(w.ServicePointId)).ToListAsync();

        return points
            .OrderBy(p => p.AreaCode, StringComparer.Ordinal).ThenBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new ExportPoint(
                p.Id,
                p.BuildingCode,
                p.AreaCode,
                p.ShiftPattern,
                p.Name,
                signs.Single(s => s.PointId == p.Id).Code,
                windows.Where(w => w.ServicePointId == p.Id)
                    .Select(w => new ExportWindow(w.Id, w.Shift, w.StartTime, w.EndTime, DateTime.SpecifyKind(w.CreatedAt, DateTimeKind.Utc)))
                    .ToList()))
            .ToList();
    }
}
```

- [ ] **Step 9: Run them to see them pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~ExportQueryTests"`
Expected: PASS.

If EF cannot translate the positional-record projection, project to an anonymous type inside the query and map to the record after `ToListAsync()`. Keep the record types as they are.

- [ ] **Step 10: Run every test**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS (3 MySQL tests skipped).

- [ ] **Step 11: Update the handoff and commit**

Add to `## Task log (plan 5)` in `docs/handoff.md`:

```markdown
- 2026-10-14 · Task 5 done: ExportRules.MissedRounds and ExportQuery (attendance, submissions, points with windows; date range and building filter). All tests pass. Next: Task 6 (the .xlsx endpoint).
```

```bash
git add backend docs/handoff.md
git commit -m "feat(backend): export rows for attendance and submissions, and the rounds nobody did (facility-0047, 0053)"
```

---

### Task 6: The Excel file and `POST /api/admin/export`

**Files:**
- Modify: `backend/src/FacilityRealtime.Infrastructure/FacilityRealtime.Infrastructure.csproj` (+ ClosedXML)
- Create: `backend/src/FacilityRealtime.Infrastructure/Export/ExcelWorkbook.cs`
- Create: `backend/src/FacilityRealtime.Api/DTOs/ExportDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/ExportSheets.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminExportEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs` (+ `SubmissionResult`, `Gps`)
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/ExportEndpointTests.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Admin/DeactivatedAdminTests.cs`

**Interfaces:**
- Consumes: Task 5's `ExportQuery`, `ExportRules`, `ExportSet` and rows; `AdminQuery.TryReadDate`; `ThaiLabels.Date/Clock/ShiftName/EventName`; `RoundWindowRules.Format(TimeOnly)`.
- Produces:
  - `ExcelSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)`, `ExcelWorkbook.Write(IReadOnlyList<ExcelSheet>) → byte[]`.
  - `ExportRequest(string? From, string? To, int? BuildingId, List<ExportSet>? Sets)`.
  - `POST /api/admin/export`.

- [ ] **Step 1: Add the package**

Run: `dotnet add backend/src/FacilityRealtime.Infrastructure package ClosedXML --version 0.104.2`
Expected: the csproj gains `<PackageReference Include="ClosedXML" Version="0.104.2" />` and restore succeeds. If restore fails, stop and report (Global Constraints).

- [ ] **Step 2: Write the failing tests**

`backend/tests/FacilityRealtime.ApiTests/Admin/ExportEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Wireframe D8, facility-0053: the Admin downloads attendance and submissions for a date range as one .xlsx.</summary>
public class ExportEndpointTests
{
    private const string Path = "/api/admin/export";
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    /// <summary>9 Oct 12:00 Thai: the Day and Night shifts of 8 Oct are over, the Day shift of 9 Oct is running.</summary>
    private static async Task<(FacilityApiFactory Factory, HttpClient Admin)> StartAsync()
    {
        var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.BackdateSetupAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(9, 12, 0));
        return (factory, admin);
    }

    private static Task<HttpResponseMessage> ExportAsync(HttpClient admin, string from, string to, string[] sets, int? buildingId = null) =>
        admin.PostAsJsonAsync(Path, new { from, to, buildingId, sets });

    private static async Task<XLWorkbook> WorkbookAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }

    /// <summary>Every row under the header, as the text in each header column.</summary>
    private static List<string[]> Rows(IXLWorksheet sheet)
    {
        var width = sheet.Row(1).CellsUsed().Count();
        return sheet.RowsUsed().Skip(1)
            .Select(row => Enumerable.Range(1, width).Select(c => row.Cell(c).GetString()).ToArray())
            .ToList();
    }

    [Fact]
    public async Task Attendance_is_one_row_per_person_per_shift_with_source_and_gps()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftOut, ThaiClock.At(8, 19, 2), AttendanceSource.AdminAdd);
        await TestData.AddAttendanceAsync(factory, "E1003", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 7, 5), distanceM: 900, withinRadius: false);

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Attendance"]));

        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("การลงเวลา", sheet.Name);
        Assert.Equal(
            new[] { "วันที่กะ", "กะ", "ตึก", "Area", "รหัสพนักงาน", "ชื่อ", "เข้างาน", "ออกพัก", "กลับจากพัก", "เลิกงาน", "ที่มา", "GPS" },
            sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        var rows = Rows(sheet);
        Assert.Equal(
            new[] { "2026-10-08", "เช้า", "A", "AR01", "E1001", "สมชาย ใจดี", "06:58", "", "", "19:02", "แก้โดย Admin: เลิกงาน", "ในรัศมี" },
            rows[0]);
        Assert.Equal(new[] { "AR02", "E1003", "07:05", "สแกนเอง", "นอกรัศมี: เข้างาน 900 ม." }, new[] { rows[1][3], rows[1][4], rows[1][6], rows[1][10], rows[1][11] });
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task Submissions_list_each_submission_and_every_round_nobody_did()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10), CleaningStatus.Issue, issueTags: "wet_floor");

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Submissions"]));

        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("การส่งงาน", sheet.Name);
        Assert.Equal(
            new[] { "วันที่กะ", "กะ", "ตึก", "Area", "จุด", "ป้าย", "รอบ", "เวลาส่ง", "ผล", "Issue", "ทำแทน", "รหัสพนักงาน", "ชื่อ", "GPS" },
            sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        var rows = Rows(sheet);
        var menRestroom = rows.Where(r => r[5] == "AR01-01").Select(r => (r[1], r[6], r[7], r[8])).ToList();
        Assert.Equal(
            new[]
            {
                ("เช้า", "07:00–09:00", "08:10", "ตรงเวลา"),
                ("เช้า", "16:00–18:00", "", "ไม่ได้ทำ"),
                ("ดึก", "20:00–22:00", "", "ไม่ได้ทำ"),
                ("ดึก", "03:00–05:00", "", "ไม่ได้ทำ"),
            },
            menRestroom);
        var submitted = rows.Single(r => r[5] == "AR01-01" && r[8] == "ตรงเวลา");
        Assert.Equal(("wet_floor", "", "E1001", "สมชาย ใจดี"), (submitted[9], submitted[10], submitted[11], submitted[12]));
        // AR02 works days only: one missed day round, no night rows
        Assert.Equal(new[] { ("เช้า", "ไม่ได้ทำ") }, rows.Where(r => r[5] == "AR02-01").Select(r => (r[1], r[8])));
    }

    [Fact]
    public async Task A_shift_still_running_shows_no_missed_rounds()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-09", "2026-10-09", ["Submissions"]));

        Assert.Empty(Rows(workbook.Worksheet(1)));
    }

    [Fact]
    public async Task The_file_has_no_coordinates_and_its_name_carries_the_dates()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));

        var response = await ExportAsync(admin, "2026-10-08", "2026-10-09", ["Attendance", "Submissions"]);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("facility_2026-10-08_2026-10-09.xlsx", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        using var workbook = await WorkbookAsync(response);
        Assert.Equal(new[] { "การลงเวลา", "การส่งงาน" }, workbook.Worksheets.Select(w => w.Name));
        var text = string.Join("|", workbook.Worksheets.SelectMany(w => w.CellsUsed()).Select(c => c.GetString()));
        Assert.DoesNotContain("13.75", text);
        Assert.DoesNotContain("100.50", text);
        Assert.DoesNotContain("token-", text);
    }

    [Fact]
    public async Task Each_export_is_logged_with_its_range_and_row_counts()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));

        await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Attendance"]);

        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("EXPORT_EXCEL", "export", (long?)null, (string?)null), (entry.Action, entry.EntityType, entry.EntityId, entry.BeforeJson));
        Assert.Equal("ส่งออก Excel การลงเวลา วันที่ 2026-10-08 ถึง 2026-10-08 ทุกตึก", entry.Summary);
        Assert.Contains("\"From\":\"2026-10-08\"", entry.AfterJson);
        Assert.Contains("\"Sets\":[\"Attendance\"]", entry.AfterJson);
        Assert.Contains("\"Rows\":[1]", entry.AfterJson);
        Assert.DoesNotContain("สมชาย", entry.AfterJson);
    }

    [Fact]
    public async Task Another_building_gives_headers_only()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        var buildingB = await TestData.AddBuildingAsync(factory, "B");

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Attendance", "Submissions"], buildingB));

        Assert.All(workbook.Worksheets, sheet => Assert.Empty(Rows(sheet)));
        Assert.EndsWith("ตึก B", Assert.Single(await AdminSetupApi.AuditAsync(factory)).Summary);
    }

    [Fact]
    public async Task Typed_text_stays_text_and_never_becomes_a_formula()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "E1001")).DisplayName = "=1+1";
            await db.SaveChangesAsync();
        });
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Attendance"]));

        var cell = workbook.Worksheet(1).Cell(2, 6);
        Assert.False(cell.HasFormula);
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal("=1+1", cell.GetString());
    }

    public static TheoryData<string> BadRequests => new()
    {
        """{ "from": "2026-10-01", "to": "2026-11-01", "sets": ["Attendance"] }""",
        """{ "from": "2026-10-09", "to": "2026-10-08", "sets": ["Attendance"] }""",
        """{ "from": "2026-10-08", "sets": ["Attendance"] }""",
        """{ "from": "08/10/2026", "to": "2026-10-08", "sets": ["Attendance"] }""",
        """{ "from": "2026-10-08", "to": "2026-10-08", "sets": [] }""",
        """{ "from": "2026-10-08", "to": "2026-10-08" }""",
        """{ "from": "2026-10-08", "to": "2026-10-08", "sets": ["Attendance"], "buildingId": 9999 }""",
    };

    [Theory]
    [MemberData(nameof(BadRequests))]
    public async Task Bad_requests_are_refused_and_not_logged(string body)
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        var response = await admin.PostAsync(Path, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(await AdminSetupApi.MessageAsync(response)));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Thirty_one_days_are_allowed()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        Assert.Equal(HttpStatusCode.OK, (await ExportAsync(admin, "2026-09-01", "2026-10-01", ["Submissions"])).StatusCode);
    }

    [Fact]
    public async Task A_cleaner_cannot_export()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await ExportAsync(cleaner, "2026-10-08", "2026-10-08", ["Attendance"])).StatusCode);
    }
}
```

In `backend/tests/FacilityRealtime.ApiTests/Admin/DeactivatedAdminTests.cs`, add:

```csharp
    [InlineData("POST", "/api/admin/export", """{ "from": "2026-10-08", "to": "2026-10-08", "sets": ["Attendance"] }""")]
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~ExportEndpointTests"`
Expected: FAIL; the route does not exist.

- [ ] **Step 4: Write the workbook writer**

`backend/src/FacilityRealtime.Infrastructure/Export/ExcelWorkbook.cs`:

```csharp
using ClosedXML.Excel;

namespace FacilityRealtime.Infrastructure.Export;

/// <summary>One worksheet: a bold, frozen header row, then rows of text.</summary>
public sealed record ExcelSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>Writes the .xlsx of wireframe D8.</summary>
public static class ExcelWorkbook
{
    private const double ColumnWidth = 16;

    /// <summary>
    /// Every cell is set as text, so a name or tag someone typed (e.g. "=1+1") can never run as a formula. Columns get a fixed
    /// width, because auto-fit measures text with system fonts the server may not have.
    /// </summary>
    public static byte[] Write(IReadOnlyList<ExcelSheet> sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var sheet in sheets)
        {
            var worksheet = workbook.Worksheets.Add(sheet.Name);
            for (var column = 0; column < sheet.Headers.Count; column++)
            {
                worksheet.Cell(1, column + 1).SetValue(sheet.Headers[column]);
            }

            worksheet.Row(1).Style.Font.Bold = true;
            worksheet.SheetView.FreezeRows(1);
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                for (var column = 0; column < sheet.Rows[row].Count; column++)
                {
                    worksheet.Cell(row + 2, column + 1).SetValue(sheet.Rows[row][column]);
                }
            }

            worksheet.Columns(1, sheet.Headers.Count).Width = ColumnWidth;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
```

- [ ] **Step 5: Write the labels, request, sheets and endpoint**

Add to `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs`, inside the class:

```csharp
    /// <summary>facility-0047: ผล of a submission on the export.</summary>
    public static string SubmissionResult(Placement placement, int? lateMinutes) => placement switch
    {
        Placement.OnTime => "ตรงเวลา",
        Placement.Late => $"ส่งช้า {lateMinutes ?? 0} นาที",
        Placement.Rework => "ส่งงานแก้",
        _ => "นอกรอบ",
    };

    /// <summary>facility-0053: distance and verdict only, never coordinates.</summary>
    public static string Gps(short? distanceM, bool? withinRadius) => withinRadius switch
    {
        true => $"ในรัศมี {distanceM} ม.",
        false => $"นอกรัศมี {distanceM} ม.",
        null => "-",
    };
```

`backend/src/FacilityRealtime.Api/DTOs/ExportDtos.cs`:

```csharp
using FacilityRealtime.Application.Export;

namespace FacilityRealtime.Api.DTOs;

/// <summary>Wireframe D8. From and To are Thai shift dates, yyyy-MM-dd, both included. BuildingId null = every building.</summary>
public record ExportRequest(string? From, string? To, int? BuildingId, List<ExportSet>? Sets);
```

`backend/src/FacilityRealtime.Api/Endpoints/ExportSheets.cs`:

```csharp
using FacilityRealtime.Application.Export;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Export;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>The export's rows in the Thai words of wireframe D8 and facility-0053.</summary>
internal static class ExportSheets
{
    public const string AttendanceName = "การลงเวลา";
    public const string SubmissionsName = "การส่งงาน";

    private static readonly AttendanceEvent[] Events =
        [AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut];

    private sealed record SortedRow(DateOnly ShiftDate, Shift Shift, DateTime SortUtc, string Tie, IReadOnlyList<string> Cells);

    /// <summary>One row per Cleaner per shift: the four times, who recorded them, and the GPS verdicts.</summary>
    public static ExcelSheet Attendance(IReadOnlyList<AttendanceExportRow> events)
    {
        var rows = events
            .GroupBy(e => (e.ShiftDate, e.Shift, e.UserId))
            .Select(group =>
            {
                var first = group.MinBy(e => e.OccurredAtUtc)!;
                // shift_attendances is unique per event per shift, so each event appears at most once
                var byEvent = group.ToDictionary(e => e.EventType);
                var edited = Events.Where(t => byEvent.TryGetValue(t, out var e) && e.Source != AttendanceSource.Scan).ToList();
                var outside = Events.Where(t => byEvent.TryGetValue(t, out var e) && e.WithinRadius == false).ToList();
                IReadOnlyList<string> cells =
                [
                    ThaiLabels.Date(group.Key.ShiftDate),
                    ThaiLabels.ShiftName(group.Key.Shift),
                    first.BuildingCode,
                    first.AreaCode,
                    first.EmployeeId,
                    first.DisplayName,
                    .. Events.Select(t => byEvent.TryGetValue(t, out var e) ? ThaiLabels.Clock(e.OccurredAtUtc) : string.Empty),
                    edited.Count == 0 ? "สแกนเอง" : "แก้โดย Admin: " + string.Join(", ", edited.Select(ThaiLabels.EventName)),
                    outside.Count > 0
                        ? "นอกรัศมี: " + string.Join(", ", outside.Select(t => $"{ThaiLabels.EventName(t)} {byEvent[t].DistanceM} ม."))
                        : group.Any(e => e.WithinRadius == true) ? "ในรัศมี" : "-",
                ];
                return new SortedRow(group.Key.ShiftDate, group.Key.Shift, first.OccurredAtUtc, first.AreaCode + "|" + first.EmployeeId, cells);
            });

        return new ExcelSheet(
            AttendanceName,
            ["วันที่กะ", "กะ", "ตึก", "Area", "รหัสพนักงาน", "ชื่อ", "เข้างาน", "ออกพัก", "กลับจากพัก", "เลิกงาน", "ที่มา", "GPS"],
            Sort(rows));
    }

    /// <summary>Every submission, plus a "ไม่ได้ทำ" row for each round of an ended shift that nobody did (decision 12).</summary>
    public static ExcelSheet Submissions(
        IReadOnlyList<SubmissionExportRow> submissions, IReadOnlyList<ExportPoint> points, DateOnly first, DateOnly last, DateTime nowUtc)
    {
        var rows = submissions.Select(s => new SortedRow(
            s.ShiftDate,
            s.Shift,
            s.SubmittedAtUtc,
            s.AreaCode + "|" + s.SignCode,
            [
                ThaiLabels.Date(s.ShiftDate),
                ThaiLabels.ShiftName(s.Shift),
                s.BuildingCode,
                s.AreaCode,
                s.PointName,
                s.SignCode,
                Round(s.RoundStart, s.RoundEnd),
                ThaiLabels.Clock(s.SubmittedAtUtc),
                ThaiLabels.SubmissionResult(s.Placement, s.LateMinutes),
                s.Status == CleaningStatus.Issue ? s.IssueTags ?? "มี Issue" : string.Empty,
                s.IsCover ? "ทำแทน" : string.Empty,
                s.EmployeeId,
                s.DisplayName,
                ThaiLabels.Gps(s.DistanceM, s.WithinRadius),
            ])).ToList();

        // A round counts as done once any OnTime, Late or Rework submission was placed in it
        var done = submissions
            .Where(s => s.RoundWindowId is not null && s.Placement != Placement.OffRound)
            .GroupBy(s => (s.ShiftDate, s.Shift))
            .ToDictionary(g => g.Key, g => (IReadOnlySet<int>)g.Select(s => s.RoundWindowId!.Value).ToHashSet());
        IReadOnlySet<int> none = new HashSet<int>();

        for (var date = first; date <= last; date = date.AddDays(1))
        {
            foreach (var shift in new[] { Shift.Day, Shift.Night })
            {
                var slot = new ShiftSlot(date, shift);
                var doneIds = done.GetValueOrDefault((date, shift), none);
                foreach (var point in points.Where(p => shift == Shift.Day || p.ShiftPattern == ShiftPattern.DayAndNight))
                {
                    var windows = point.Windows
                        .Where(w => w.Shift == shift)
                        .Select(w => (RoundWindow.For(w.Id, w.Start, w.End, slot), w.CreatedAtUtc));
                    foreach (var missed in ExportRules.MissedRounds(windows, doneIds, slot, nowUtc))
                    {
                        rows.Add(new SortedRow(date, shift, missed.StartUtc, point.AreaCode + "|" + point.SignCode,
                        [
                            ThaiLabels.Date(date),
                            ThaiLabels.ShiftName(shift),
                            point.BuildingCode,
                            point.AreaCode,
                            point.PointName,
                            point.SignCode,
                            Round(missed.Start, missed.End),
                            string.Empty,
                            "ไม่ได้ทำ",
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                        ]));
                    }
                }
            }
        }

        return new ExcelSheet(
            SubmissionsName,
            ["วันที่กะ", "กะ", "ตึก", "Area", "จุด", "ป้าย", "รอบ", "เวลาส่ง", "ผล", "Issue", "ทำแทน", "รหัสพนักงาน", "ชื่อ", "GPS"],
            Sort(rows));
    }

    private static string Round(TimeOnly? start, TimeOnly? end) =>
        start is { } s && end is { } e ? $"{RoundWindowRules.Format(s)}–{RoundWindowRules.Format(e)}" : string.Empty;

    /// <summary>By shift date, Day before Night, then time in the shift, then Area and sign or person.</summary>
    private static List<IReadOnlyList<string>> Sort(IEnumerable<SortedRow> rows) =>
        rows.OrderBy(r => r.ShiftDate)
            .ThenBy(r => r.Shift)
            .ThenBy(r => r.SortUtc)
            .ThenBy(r => r.Tie, StringComparer.Ordinal)
            .Select(r => r.Cells)
            .ToList();
}
```

`backend/src/FacilityRealtime.Api/Endpoints/AdminExportEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Export;
using FacilityRealtime.Infrastructure.Export;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// Wireframe D8, facility-0053: the Admin downloads attendance and submissions for up to 31 shift dates as one .xlsx.
/// POST because each export is logged (facility-0051). The file holds no coordinates, phone, QR Token or password.
/// </summary>
public static class AdminExportEndpoints
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static IEndpointRouteBuilder MapAdminExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/export", ExportAsync).RequireAuthorization(AuthSetup.AdminOnly);
        return app;
    }

    private static async Task<IResult> ExportAsync(ExportRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (!AdminQuery.TryReadDate(request.From, out var first) || !AdminQuery.TryReadDate(request.To, out var last))
        {
            return Bad("ต้องระบุทั้งวันเริ่ม (from) และวันสุดท้าย (to) แบบ yyyy-MM-dd");
        }

        if (last < first)
        {
            return Bad("วันสุดท้ายต้องไม่ก่อนวันเริ่ม");
        }

        if (last.DayNumber - first.DayNumber + 1 > ExportRules.MaxDays)
        {
            return Bad($"เลือกได้ไม่เกิน {ExportRules.MaxDays} วัน");
        }

        var sets = (request.Sets ?? []).Distinct().ToList();
        if (sets.Count == 0 || sets.Any(s => !Enum.IsDefined(s)))
        {
            return Bad("ต้องเลือกข้อมูลอย่างน้อย 1 ชุด");
        }

        string? buildingCode = null;
        if (request.BuildingId is { } buildingId)
        {
            buildingCode = await db.Buildings.AsNoTracking().Where(b => b.Id == buildingId).Select(b => b.Code).FirstOrDefaultAsync();
            if (buildingCode is null)
            {
                return Bad("ไม่พบตึกนี้");
            }
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var sheets = new List<ExcelSheet>();
        if (sets.Contains(ExportSet.Attendance))
        {
            sheets.Add(ExportSheets.Attendance(await ExportQuery.LoadAttendanceAsync(db, first, last, request.BuildingId)));
        }

        if (sets.Contains(ExportSet.Submissions))
        {
            sheets.Add(ExportSheets.Submissions(
                await ExportQuery.LoadSubmissionsAsync(db, first, last, request.BuildingId),
                await ExportQuery.LoadPointsAsync(db, request.BuildingId),
                first,
                last,
                now));
        }

        var from = ThaiLabels.Date(first);
        var to = ThaiLabels.Date(last);
        AuditTrail.Add(
            db, admin.Id, now, "EXPORT_EXCEL", "export", null,
            $"ส่งออก Excel {string.Join(" + ", sheets.Select(s => s.Name))} วันที่ {from} ถึง {to} {(buildingCode is null ? "ทุกตึก" : "ตึก " + buildingCode)}",
            before: null,
            after: new
            {
                From = from,
                To = to,
                BuildingId = request.BuildingId,
                Sets = sheets.Select(s => s.Name == ExportSheets.AttendanceName ? nameof(ExportSet.Attendance) : nameof(ExportSet.Submissions)).ToList(),
                Rows = sheets.Select(s => s.Rows.Count).ToList(),
            });
        await db.SaveChangesAsync();

        return Results.File(ExcelWorkbook.Write(sheets), XlsxType, $"facility_{from}_{to}.xlsx");
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add after `app.MapAdminAccountEndpoints();`:

```csharp
app.MapAdminExportEndpoints();
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~ExportEndpointTests|FullyQualifiedName~DeactivatedAdminTests"`
Expected: PASS.

If ClosedXML 0.104.2's API differs from the calls used here (`SetValue(string)`, `Columns(int, int).Width`, `SheetView.FreezeRows`, `GetString()`, `DataType`, `HasFormula`), use the equivalent in that version. Keep the test's meaning: every cell is text.

- [ ] **Step 7: Run every test**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS (3 MySQL tests skipped).

- [ ] **Step 8: Update the handoff and commit**

Add to `## Task log (plan 5)` in `docs/handoff.md`:

```markdown
- 2026-10-14 · Task 6 done: POST /api/admin/export returns .xlsx (การลงเวลา, การส่งงาน with ไม่ได้ทำ rows; text-only cells; EXPORT_EXCEL logged). New package ClosedXML 0.104.2. All tests pass. Next: Task 7 (MySQL check and docs).
```

```bash
git add backend docs/handoff.md
git commit -m "feat(backend): Admin downloads attendance and submissions as Excel for up to 31 days, missed rounds included (facility-0053)"
```

---

### Task 7: MySQL check, docs, final handoff

**Files:**
- Create: `backend/tests/FacilityRealtime.ApiTests/Persistence/MySqlExportAndAccountTests.cs`
- Modify: `README.md` (routes)
- Modify: `docs/architecture.md` (routes, ClosedXML)
- Modify: `docs/handoff.md` (status)

**Interfaces:**
- Consumes: Task 5's `ExportQuery`; existing `MySqlTestDatabase.ConnectionString()`, `[MySqlFact]`, `[Collection("MySQL")]`.

- [ ] **Step 1: Write the MySQL test**

`backend/tests/FacilityRealtime.ApiTests/Persistence/MySqlExportAndAccountTests.cs`:

```csharp
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>
/// The export reads DATE ranges, joins and nullable flags, and the accounts page compares UPPER(employee_id). SQLite hides
/// how the Oracle MySQL provider treats them, so this runs them on real MySQL.
/// </summary>
[Collection("MySQL")]
public class MySqlExportAndAccountTests
{
    [MySqlFact]
    public async Task Export_and_account_queries_run_on_MySQL()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(MySqlTestDatabase.ConnectionString())
            .Options;

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.EnsureCreatedAsync();
        }

        try
        {
            var oct8 = new DateOnly(2026, 10, 8);
            var shiftIn = new DateTime(2026, 10, 7, 23, 58, 0, DateTimeKind.Utc);   // 06:58 Thai on 8 Oct
            await using (var write = new AppDbContext(options))
            {
                await DbInitializer.SeedAsync(write, new Pbkdf2PasswordHasher(iterations: 1_000));
                var cleaner = await write.Users.SingleAsync(u => u.EmployeeId == "E1001");
                var restroom = await write.Signs.SingleAsync(s => s.Code == "AR01-01");
                var window = await write.PointRoundWindows.FirstAsync(w => w.ServicePointId == restroom.ServicePointId && w.Shift == Shift.Day);
                write.ShiftAttendances.Add(new ShiftAttendance
                {
                    UserId = cleaner.Id, AreaId = cleaner.AreaId!.Value, ShiftDate = oct8, Shift = Shift.Day,
                    EventType = AttendanceEvent.ShiftIn, OccurredAt = shiftIn, Source = AttendanceSource.AdminAdd, CreatedAt = shiftIn,
                });
                write.ScanRecords.Add(new ScanRecord
                {
                    ServicePointId = restroom.ServicePointId!.Value, SignId = restroom.Id, UserId = cleaner.Id, ShiftDate = oct8, Shift = Shift.Day,
                    RoundWindowId = window.Id, RoundStart = window.StartTime, RoundEnd = window.EndTime, Placement = Placement.OnTime,
                    SubmittedAt = shiftIn.AddHours(1), DistanceM = 12, WithinRadius = true,
                });
                await write.SaveChangesAsync();

                // users.employee_id is unique ignoring case on MySQL, as AccountChecks assumes
                write.Users.Add(new User { Role = UserRole.Cleaner, EmployeeId = "e1001", DisplayName = "ซ้ำ", SecretHash = "x", Shift = Shift.Night, CreatedAt = shiftIn });
                await Assert.ThrowsAsync<DbUpdateException>(() => write.SaveChangesAsync());
            }

            await using var read = new AppDbContext(options);
            var buildingA = await read.Buildings.Where(b => b.Code == "A").Select(b => b.Id).SingleAsync();

            var attendance = await ExportQuery.LoadAttendanceAsync(read, oct8, oct8, buildingA);
            var submissions = await ExportQuery.LoadSubmissionsAsync(read, oct8, oct8.AddDays(1), null);
            var points = await ExportQuery.LoadPointsAsync(read, buildingA);
            var upperMatches = await read.Users.AnyAsync(u => u.EmployeeId != null && u.EmployeeId.ToUpper() == "E1001");

            var shiftInRow = Assert.Single(attendance);
            Assert.Equal(("E1001", "AR01", AttendanceSource.AdminAdd, (bool?)null), (shiftInRow.EmployeeId, shiftInRow.AreaCode, shiftInRow.Source, shiftInRow.WithinRadius));
            var submission = Assert.Single(submissions);
            Assert.Equal(("AR01-01", Placement.OnTime, false, (bool?)true), (submission.SignCode, submission.Placement, submission.IsCover, submission.WithinRadius));
            Assert.Contains(points, p => p.SignCode == "AR01-01" && p.Windows.Count > 0);
            Assert.True(upperMatches);
            Assert.Empty(await ExportQuery.LoadAttendanceAsync(read, oct8.AddDays(1), oct8.AddDays(2), null));
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
```

- [ ] **Step 2: Run without MySQL**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS, now with 4 MySQL tests skipped.

- [ ] **Step 3: Run on real MySQL (controller only, not a subagent)**

Ask the owner for the MySQL root password. Then, in Git Bash, typing the password only into the command:

```bash
FACILITY_MYSQL_TEST="Server=localhost;Port=3306;Database=facility_accounts_test;Uid=root;Pwd=<local root password>;CharSet=utf8mb4;" dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~MySql"
```

Expected: every MySQL test passes. Afterwards, check that the throwaway database is gone:

```bash
MYSQL_PWD='<local root password>' "/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -uroot -e "SHOW DATABASES LIKE 'facility\_%';"
```

Expected: no `facility_accounts_test`, and `facility_dashboard` untouched.

- [ ] **Step 4: Update the docs**

- In `README.md`, add the routes of the endpoint map above to the Admin route list, next to the plan-4 routes.
- In `docs/architecture.md`:
  - Add `AdminAccountEndpoints` and `AdminExportEndpoints` to the endpoint list.
  - Add `ExportQuery` to the Infrastructure reads.
  - Add `Infrastructure/Export/ExcelWorkbook` and the ClosedXML dependency, and say why every cell is text.

- [ ] **Step 5: Update the handoff and commit**

In `docs/handoff.md`:
- Set plan 5 in the status table to "done, PR open".
- Add the 16 planning decisions as a short list under "Rulings", pointing to this plan for detail.
- Add to `## Task log (plan 5)`:

```markdown
- 2026-10-14 · Task 7 done: MySQL check of the export and account queries; README and architecture docs. Next: final whole-branch review, then a PR for the owner.
```

```bash
git add backend README.md docs
git commit -m "test(backend): export and account queries on MySQL; docs for the accounts page and the Excel export"
```

---

## Self-review notes (planning)

- **Spec coverage:**
  - ADR 0020: add, edit, reset password, deactivate/activate, no delete, no self-deactivation, logged. Tasks 2–4.
  - ADR 0054: employee ID + phone, hashed, phone change logs out, Arabic digits only. Tasks 1, 2, 4.
  - ADR 0056: one by one, 10-digit phone check. Import is out of scope.
  - ADR 0053: two of the three sets, missed rounds, no coordinates, logged. Tasks 5–6. The inspection set is deferred (decision 11).
  - ADR 0057: no secret in history. Task 4 test.
  - Handoff item "test the history has no phone or QR Token": Task 4.
- **Types used across tasks:** `AccountDto`, `AccountChecks.AccountClaim`, `SaveAsync(db, claim, afterSave)`, `ExportSet`, `ExportQuery.LoadPointsAsync`, `ExcelSheet`. Each is named the same where it is made and where it is used.
