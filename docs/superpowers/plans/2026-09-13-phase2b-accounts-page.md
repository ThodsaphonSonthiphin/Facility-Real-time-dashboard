# Phase 2B: Accounts Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an Admin create cleaner and admin accounts, edit display names and roles, reset a temporary password, and deactivate or reactivate accounts from a web page, without touching code or SQL.

**Architecture:** Admin-only Minimal API routes under `/api/admin/users` validate with one shared rule class, hash through `IPasswordHasher`, and revoke refresh tokens on password reset and deactivation. The React side adds an Admin menu with an accounts tab: a table page, a side drawer for create and edit, and a reset-password dialog, each UI file paired with its hook.

**Tech Stack:** .NET 10 Minimal API, EF Core 10, xUnit 2.9.3 with the API test harness from plan 2A; React 19 + TypeScript + Vite 8, Vanilla CSS, `lucide-react`, Vitest 5.0.0.

**Spec:**
- `docs/adr/facility-0020-accounts-page-operations.md` (the decision this plan implements)
- `docs/adr/facility-0010-admin-gate-server-side.md`, `facility-0012-short-access-token-with-refresh.md`, `facility-0014-refresh-tokens-stored-server-side.md` (admin gate, 5-minute reach, token revocation)
- `docs/adr/facility-0002-scanner-authentication.md` (Admin creates accounts in advance)
- `docs/adr/facility-0009-delivery-plan-walking-skeleton.md` (Scope Defense item 1: seeding is the fallback only)
- `CONTEXT.md` (Cleaner Account, Admin Account, Deactivated Account)
- Decision map ticket: https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/issues/17

**Depends on:** `2026-09-13-phase2a-auth-jwt-refresh.md` merged first. This plan uses its `AuthSetup.AdminOnly` policy, `IPasswordHasher`, `RevokeUserSessionsAsync`, `User.IsActive`, the `FacilityApiFactory` and `AuthApi` test helpers, `apiFetch`, and `AuthContext.isAdmin`.

## Global Constraints

- The accounts page offers exactly: **create** (username unique and not editable afterwards, display name, temporary password, role `cleaner` or `admin`), **edit** (display name, role), **reset password** (new temporary password), **deactivate / reactivate**. **There is no delete** (facility-0020).
- An Admin **cannot deactivate or demote the account they are logged in with**; the server answers **400**, so at least one working Admin always remains (facility-0020).
- Cleaners **cannot change their own password**; a forgotten password is reset by an Admin (facility-0020).
- A role change or deactivation reaches that user's open sessions **within 5 minutes** (facility-0012). **Password reset and deactivation revoke every refresh token** of that user (facility-0014).
- A Deactivated Account gets **401 on login**, its refresh is refused, and its **scan history and name stay** on the Dashboard (facility-0020, CONTEXT.md).
- Every route is **admin-only: 401 when not logged in, 403 for a cleaner** (facility-0010).
- The temporary password is handed over **verbally or on paper**; the system stores only its hash, never sends it, and does **not force a change** at first login (facility-0020).
- Seeding stays only the **Scope Defense fallback** (facility-0009); nothing in this plan changes `DbInitializer`.
- Routes live under **`/api/admin/users`**, not the `/api/users` sketched in facility-0020's Consequences, so both admin pages share the `/api/admin` prefix with facility-0021's `/api/admin/service-points`. The admin-only requirement is unchanged.
- Field rules (plan-level choices, not fixed by an ADR; identical messages on client and server): username matches `^[a-z0-9._-]{3,50}$`; display name 1-150 characters after trimming; password at least 8 characters; role exactly `cleaner` or `admin`.
- Follow the established code shape: Minimal API handlers call `AppDbContext` directly; frontend uses Vanilla CSS with the tokens in `frontend/src/styles/index.css`, the manual router in `App.tsx`, and hooks in `features/<feature>/hooks` separate from UI in `features/<feature>/components`.
- Commit messages reference the decision ticket `(#17)`.

## File Structure

| Path | Status | Responsibility |
|---|---|---|
| `backend/src/FacilityRealtime.Application/Accounts/AccountRules.cs` | Create | Field rules and messages, used by every write route |
| `backend/tests/FacilityRealtime.UnitTests/AccountRulesTests.cs` | Create | Rule tests |
| `backend/src/FacilityRealtime.Api/DTOs/AdminUserDtos.cs` | Create | Request and row shapes |
| `backend/src/FacilityRealtime.Api/Endpoints/AdminUserEndpoints.cs` | Create | The six admin routes |
| `backend/src/FacilityRealtime.Api/Program.cs` | Modify | Map the routes |
| `backend/tests/FacilityRealtime.ApiTests/Admin/AdminUsersTests.cs` | Create | Route behaviour, gate, revocation |
| `frontend/src/types/index.ts` | Modify | `UserRole`, `AdminUser`, `CreateUserInput`, `UpdateUserInput` |
| `frontend/src/services/apiError.ts` (+ `.test.ts`) | Create | Error with the server's message |
| `frontend/src/services/adminUsersApi.ts` | Create | Calls to `/api/admin/users` |
| `frontend/src/features/admin/logic/accountValidation.ts` (+ `.test.ts`) | Create | Client copy of the field rules |
| `frontend/src/features/admin/admin.css` | Create | Styles shared by both admin pages |
| `frontend/src/features/admin/adminTabs.ts` (+ `.test.ts`) | Create | Admin menu tabs and path lookup |
| `frontend/src/features/admin/components/AdminLayout.tsx` | Create | Admin menu around a page |
| `frontend/src/features/admin/components/Toast.tsx`, `hooks/useToast.ts` | Create | One-line confirmations |
| `frontend/src/features/admin/components/AccountsPage.tsx`, `hooks/useAccountsPage.ts` | Create | Accounts table |
| `frontend/src/features/admin/components/AccountFormDrawer.tsx`, `hooks/useAccountForm.ts` | Create | Create and edit |
| `frontend/src/features/admin/components/ResetPasswordDialog.tsx`, `hooks/useResetPassword.ts` | Create | Reset password |
| `frontend/src/App.tsx` | Modify | Admin tab in the navbar, `/admin/*` route |

---

### Task 1: Account field rules

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Accounts/AccountRules.cs`
- Create: `backend/tests/FacilityRealtime.UnitTests/AccountRulesTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `AccountRules.FullNameMaxLength = 150`, `AccountRules.MinPasswordLength = 8`, `AccountRules.Roles` (`cleaner`, `admin`)
  - `string? UsernameError(string?)`, `string? FullNameError(string?)`, `string? PasswordError(string?)`, `string? RoleError(string?)` — `null` when valid, else the Thai message
  - `Dictionary<string, string[]> ValidateNew(username, fullName, password, role)`, `ValidateEdit(fullName, role)`, `ValidatePassword(password)` — keys `username`, `fullName`, `password`, `role`; empty when valid

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.UnitTests/AccountRulesTests.cs`:

```csharp
using FacilityRealtime.Application.Accounts;
using Xunit;

namespace FacilityRealtime.UnitTests;

public class AccountRulesTests
{
    [Fact]
    public void Complete_new_account_has_no_errors()
    {
        Assert.Empty(AccountRules.ValidateNew("mali.k", "แม่บ้าน มาลี", "temp-pass1", "cleaner"));
    }

    [Fact]
    public void New_account_reports_every_bad_field_at_once()
    {
        var errors = AccountRules.ValidateNew("A", " ", "short", "manager");

        Assert.Equal(new[] { "fullName", "password", "role", "username" }, errors.Keys.OrderBy(k => k));
    }

    [Theory]
    [InlineData("cleaner01")]
    [InlineData("mali.k")]
    [InlineData("x_y-z")]
    [InlineData("abc")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Username_accepts_lowercase_letters_digits_dot_underscore_dash(string username)
    {
        Assert.Null(AccountRules.UsernameError(username));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Somchai")]
    [InlineData("som chai")]
    [InlineData("สมชาย")]
    [InlineData(" somchai ")]
    [InlineData("somchai\n")]
    [InlineData(null)]
    public void Username_rejects_anything_else(string? username)
    {
        Assert.Equal("ใช้ a-z, 0-9, จุด, ขีดล่าง หรือขีดกลาง ยาว 3–50 ตัว", AccountRules.UsernameError(username));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Full_name_is_required(string? fullName)
    {
        Assert.Equal("กรอกชื่อที่แสดง", AccountRules.FullNameError(fullName));
    }

    [Fact]
    public void Full_name_allows_150_characters_after_trimming_and_rejects_151()
    {
        Assert.Null(AccountRules.FullNameError($" {new string('ก', 150)} "));
        Assert.Equal("ชื่อที่แสดงยาวได้ไม่เกิน 150 ตัวอักษร", AccountRules.FullNameError(new string('ก', 151)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    public void Password_needs_at_least_8_characters(string? password)
    {
        Assert.Equal("รหัสผ่านอย่างน้อย 8 ตัวอักษร", AccountRules.PasswordError(password));
    }

    [Fact]
    public void Password_of_8_characters_is_accepted()
    {
        Assert.Null(AccountRules.PasswordError("12345678"));
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("Admin")]
    [InlineData("")]
    [InlineData(null)]
    public void Role_must_be_exactly_cleaner_or_admin(string? role)
    {
        Assert.Equal("เลือกสิทธิ์ cleaner หรือ admin", AccountRules.RoleError(role));
    }

    [Theory]
    [InlineData("cleaner")]
    [InlineData("admin")]
    public void Both_roles_are_accepted(string role)
    {
        Assert.Null(AccountRules.RoleError(role));
    }

    [Fact]
    public void Edit_checks_only_display_name_and_role()
    {
        Assert.Empty(AccountRules.ValidateEdit("ผู้ดูแลระบบ", "admin"));
        Assert.Equal(new[] { "fullName" }, AccountRules.ValidateEdit("", "admin").Keys);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --nologo`
Expected: FAIL at build with `The type or namespace name 'Accounts' does not exist in the namespace 'FacilityRealtime.Application'`.

- [ ] **Step 3: Write the rules**

Create `backend/src/FacilityRealtime.Application/Accounts/AccountRules.cs`:

```csharp
using System.Text.RegularExpressions;

namespace FacilityRealtime.Application.Accounts;

/// <summary>
/// Field rules for the accounts page (ADR facility-0020). The frontend's accountValidation.ts shows the same
/// messages before a round trip; this class is the authority.
/// </summary>
public static partial class AccountRules
{
    public const int FullNameMaxLength = 150;
    public const int MinPasswordLength = 8;
    public static readonly IReadOnlyList<string> Roles = ["cleaner", "admin"];

    // \z, not $: in .NET "$" also matches before a trailing newline
    [GeneratedRegex(@"^[a-z0-9._-]{3,50}\z")]
    private static partial Regex UsernamePattern();

    public static string? UsernameError(string? username) =>
        username is not null && UsernamePattern().IsMatch(username) ? null : "ใช้ a-z, 0-9, จุด, ขีดล่าง หรือขีดกลาง ยาว 3–50 ตัว";

    public static string? FullNameError(string? fullName)
    {
        var value = fullName?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return "กรอกชื่อที่แสดง";
        }

        return value.Length > FullNameMaxLength ? $"ชื่อที่แสดงยาวได้ไม่เกิน {FullNameMaxLength} ตัวอักษร" : null;
    }

    public static string? PasswordError(string? password) =>
        password is not null && password.Length >= MinPasswordLength ? null : $"รหัสผ่านอย่างน้อย {MinPasswordLength} ตัวอักษร";

    public static string? RoleError(string? role) =>
        role is not null && Roles.Contains(role) ? null : "เลือกสิทธิ์ cleaner หรือ admin";

    public static Dictionary<string, string[]> ValidateNew(string? username, string? fullName, string? password, string? role) =>
        Collect(("username", UsernameError(username)), ("fullName", FullNameError(fullName)), ("password", PasswordError(password)), ("role", RoleError(role)));

    public static Dictionary<string, string[]> ValidateEdit(string? fullName, string? role) =>
        Collect(("fullName", FullNameError(fullName)), ("role", RoleError(role)));

    public static Dictionary<string, string[]> ValidatePassword(string? password) =>
        Collect(("password", PasswordError(password)));

    private static Dictionary<string, string[]> Collect(params (string Field, string? Error)[] checks) =>
        checks.Where(c => c.Error is not null).ToDictionary(c => c.Field, c => new[] { c.Error! });
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --nologo`
Expected: PASS, `Passed: 69`.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Accounts backend/tests/FacilityRealtime.UnitTests/AccountRulesTests.cs
git commit -m "feat(accounts): add account field rules shared by every admin write (#17)"
```

---

### Task 2: Admin users API

**Files:**
- Create: `backend/src/FacilityRealtime.Api/DTOs/AdminUserDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminUserEndpoints.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/Admin/AdminUsersTests.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs` (map the routes)

**Interfaces:**
- Consumes: `AccountRules` (Task 1); `AuthSetup.AdminOnly`, `AuthClaims.UserId`, `IPasswordHasher`, `RevokeUserSessionsAsync`, `TimeProvider`, `FacilityApiFactory`, `AuthApi.LoggedInAsync` (plan 2A)
- Produces (all require the `admin` role: `401` anonymous, `403` cleaner):
  - `GET /api/admin/users` → `200 AdminUserDto[]` ordered by username
  - `POST /api/admin/users` `{ username, fullName, password, role }` → `201 AdminUserDto` with `Location`; `400 { message, errors }`; `409 { message }` when the username is taken
  - `PUT /api/admin/users/{id}` `{ fullName, role }` → `200 AdminUserDto`; `400` on bad fields or self-demotion; `404`
  - `POST /api/admin/users/{id}/reset-password` `{ password }` → `204`, revokes every refresh token of that user; `400`; `404`
  - `POST /api/admin/users/{id}/deactivate` → `204`, revokes every refresh token; `400` for your own account; `404`
  - `POST /api/admin/users/{id}/activate` → `204`; `404`
  - `record AdminUserDto(int Id, string Username, string FullName, string Role, bool IsActive, DateTime CreatedAt)`
  - `IEndpointRouteBuilder.MapAdminUserEndpoints()`

- [ ] **Step 1: Write the failing API tests**

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AdminUsersTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

public record AdminUserModel(int Id, string Username, string FullName, string Role, bool IsActive, DateTime CreatedAt);

public class AdminUsersTests
{
    public static TheoryData<string, string> EveryRoute => new()
    {
        { "GET", "/api/admin/users" },
        { "POST", "/api/admin/users" },
        { "PUT", "/api/admin/users/1" },
        { "POST", "/api/admin/users/1/reset-password" },
        { "POST", "/api/admin/users/1/deactivate" },
        { "POST", "/api/admin/users/1/activate" },
    };

    [Theory]
    [MemberData(nameof(EveryRoute))]
    public async Task Anonymous_requests_are_401(string method, string path)
    {
        using var factory = new FacilityApiFactory();

        var response = await SendAsync(factory.CreateApiClient(), method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(EveryRoute))]
    public async Task Cleaner_accounts_are_403(string method, string path)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await SendAsync(cleaner, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_returns_every_account_by_username_without_password_hashes()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);

        var response = await admin.GetAsync("/api/admin/users");
        var raw = await response.Content.ReadAsStringAsync();
        var users = JsonSerializer.Deserialize<List<AdminUserModel>>(raw, JsonSerializerOptions.Web)!;

        Assert.Equal(new[] { "admin", "somchai" }, users.Select(u => u.Username));
        Assert.All(users, u => Assert.True(u.IsActive));
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("cleaner")]
    [InlineData("admin")]
    public async Task Created_account_logs_in_with_its_temporary_password_and_role(string role)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);

        var created = await admin.PostAsJsonAsync("/api/admin/users", new { username = "mali.k", fullName = " แม่บ้าน มาลี ", password = "temp-pass1", role });
        var login = await AuthApi.LoginAsync(factory.CreateApiClient(), "mali.k", "temp-pass1");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = (await created.Content.ReadFromJsonAsync<AdminUserModel>())!;
        Assert.Equal("แม่บ้าน มาลี", body.FullName);
        Assert.Equal($"/api/admin/users/{body.Id}", created.Headers.Location!.OriginalString);
        Assert.Equal(role, (await AuthApi.ReadAuthAsync(login)).User.Role);
    }

    [Fact]
    public async Task Create_rejects_bad_fields_and_names_each_one()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/admin/users", new { username = "Bad Name", fullName = "", password = "123", role = "manager" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "fullName", "password", "role", "username" }, body.GetProperty("errors").EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        Assert.Contains("รหัสผ่านอย่างน้อย 8 ตัวอักษร", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Create_rejects_a_username_that_is_taken()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/admin/users", new { username = "somchai", fullName = "อีกคน", password = "temp-pass1", role = "cleaner" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Edit_changes_name_and_role_and_the_role_reaches_the_users_next_refresh()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);
        var (somchai, somchaiAuth, somchaiRefresh) = await AuthApi.LoggedInAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{somchaiAuth.User.Id}", new { fullName = "สมชาย หัวหน้าทีม", role = "admin" });
        var refreshed = await AuthApi.ReadAuthAsync(await AuthApi.RefreshAsync(somchai, somchaiRefresh));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AdminUserModel>())!;
        Assert.Equal("somchai", body.Username);
        Assert.Equal("สมชาย หัวหน้าทีม", body.FullName);
        Assert.Equal("admin", refreshed.User.Role);
    }

    [Fact]
    public async Task Admin_cannot_demote_their_own_account()
    {
        using var factory = new FacilityApiFactory();
        var (admin, auth, _) = await AdminAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{auth.User.Id}", new { fullName = "ผู้ดูแลระบบ", role = "cleaner" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var role = "";
        await factory.WithDbAsync(async db => role = (await db.Users.SingleAsync(u => u.Id == auth.User.Id)).Role);
        Assert.Equal("admin", role);
    }

    [Fact]
    public async Task Admin_can_rename_their_own_account()
    {
        using var factory = new FacilityApiFactory();
        var (admin, auth, _) = await AdminAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{auth.User.Id}", new { fullName = "ผู้ดูแล กะเช้า", role = "admin" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reset_password_swaps_the_password_and_logs_out_every_device()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);
        var (somchai, somchaiAuth, somchaiRefresh) = await AuthApi.LoggedInAsync(factory);

        var reset = await admin.PostAsJsonAsync($"/api/admin/users/{somchaiAuth.User.Id}/reset-password", new { password = "new-temp-99" });
        var oldPassword = await AuthApi.LoginAsync(factory.CreateApiClient(), "somchai", "password123");
        var newPassword = await AuthApi.LoginAsync(factory.CreateApiClient(), "somchai", "new-temp-99");
        var oldSession = await AuthApi.RefreshAsync(somchai, somchaiRefresh);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
    }

    [Fact]
    public async Task Reset_password_rejects_a_short_password()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/admin/users/1/reset-password", new { password = "1234567" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deactivation_blocks_login_and_refresh_but_keeps_scan_history()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);
        var (somchai, somchaiAuth, somchaiRefresh) = await AuthApi.LoggedInAsync(factory);
        await somchai.PostAsJsonAsync("/api/scan-records", new { qrToken = "token-restroom-m1", status = "Normal" });

        var deactivate = await admin.PostAsync($"/api/admin/users/{somchaiAuth.User.Id}/deactivate", null);
        var login = await AuthApi.LoginAsync(factory.CreateApiClient());
        var refresh = await AuthApi.RefreshAsync(somchai, somchaiRefresh);
        var listed = (await admin.GetFromJsonAsync<List<AdminUserModel>>("/api/admin/users"))!.Single(u => u.Username == "somchai");

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.False(listed.IsActive);
        var scans = 0;
        await factory.WithDbAsync(async db => scans = await db.ScanRecords.CountAsync(r => r.UserId == somchaiAuth.User.Id));
        Assert.Equal(1, scans);
    }

    [Fact]
    public async Task Reactivation_lets_the_account_log_in_again()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);
        await admin.PostAsync("/api/admin/users/1/deactivate", null);

        var activate = await admin.PostAsync("/api/admin/users/1/activate", null);
        var login = await AuthApi.LoginAsync(factory.CreateApiClient());

        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_deactivate_their_own_account()
    {
        using var factory = new FacilityApiFactory();
        var (admin, auth, _) = await AdminAsync(factory);

        var response = await admin.PostAsync($"/api/admin/users/{auth.User.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var isActive = false;
        await factory.WithDbAsync(async db => isActive = (await db.Users.SingleAsync(u => u.Id == auth.User.Id)).IsActive);
        Assert.True(isActive);
    }

    [Theory]
    [InlineData("PUT", "/api/admin/users/999", "{\"fullName\":\"ใครสักคน\",\"role\":\"cleaner\"}")]
    [InlineData("POST", "/api/admin/users/999/reset-password", "{\"password\":\"long-enough\"}")]
    [InlineData("POST", "/api/admin/users/999/activate", null)]
    public async Task Unknown_account_is_404(string method, string path, string? json)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);

        var response = await SendAsync(admin, method, path, json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task<(HttpClient Client, AuthResponseModel Auth, string RefreshToken)> AdminAsync(FacilityApiFactory factory) =>
        AuthApi.LoggedInAsync(factory, "admin", "admin1234");

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path, string? json = "{}")
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method != "GET" && json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --nologo --filter "FullyQualifiedName~AdminUsersTests"`
Expected: FAIL. With no routes mapped, the gate tests get `404 NotFound` instead of `401` and `403`, and every behaviour test fails the same way.

- [ ] **Step 3: Write the DTOs and the endpoints**

Create `backend/src/FacilityRealtime.Api/DTOs/AdminUserDtos.cs`:

```csharp
using System;

namespace FacilityRealtime.Api.DTOs;

/// <summary>One row on the accounts page. The password hash never leaves the server.</summary>
public record AdminUserDto(int Id, string Username, string FullName, string Role, bool IsActive, DateTime CreatedAt);

public record CreateUserRequest(string Username, string FullName, string Password, string Role);

/// <summary>ADR facility-0020: the username cannot change after creation, so it is not here.</summary>
public record UpdateUserRequest(string FullName, string Role);

public record ResetPasswordRequest(string Password);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminUserEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Accounts;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// ADR facility-0020: create (cleaner or admin), edit display name and role, reset a temporary password,
/// deactivate and reactivate. No delete. Every route is admin-only (facility-0010).
/// </summary>
public static class AdminUserEndpoints
{
    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/admin/users").RequireAuthorization(AuthSetup.AdminOnly);
        users.MapGet("", ListAsync);
        users.MapPost("", CreateAsync);
        users.MapPut("/{id:int}", UpdateAsync);
        users.MapPost("/{id:int}/reset-password", ResetPasswordAsync);
        users.MapPost("/{id:int}/deactivate", DeactivateAsync);
        users.MapPost("/{id:int}/activate", ActivateAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(AppDbContext db)
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
        return Results.Ok(users.Select(ToDto));
    }

    private static async Task<IResult> CreateAsync(CreateUserRequest request, AppDbContext db, IPasswordHasher hasher, TimeProvider clock)
    {
        var errors = AccountRules.ValidateNew(request.Username, request.FullName, request.Password, request.Role);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        if (await db.Users.AnyAsync(u => u.Username == request.Username))
        {
            return Results.Conflict(new { message = $"ชื่อผู้ใช้ '{request.Username}' มีอยู่แล้ว" });
        }

        var user = new User
        {
            Username = request.Username,
            FullName = request.FullName.Trim(),
            Role = request.Role,
            PasswordHash = hasher.Hash(request.Password),
            IsActive = true,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/users/{user.Id}", ToDto(user));
    }

    private static async Task<IResult> UpdateAsync(int id, UpdateUserRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var errors = AccountRules.ValidateEdit(request.FullName, request.Role);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return AccountNotFound(id);
        }

        // ADR facility-0020: an admin cannot demote the account they are using, so one working admin always remains
        if (IsSelf(principal, id) && request.Role != "admin")
        {
            return Results.BadRequest(new { message = "ลดสิทธิ์บัญชีที่ตัวเองใช้อยู่ไม่ได้" });
        }

        user.FullName = request.FullName.Trim();
        user.Role = request.Role; // reaches that user's sessions on their next refresh, within 5 minutes (facility-0012)
        await db.SaveChangesAsync();

        return Results.Ok(ToDto(user));
    }

    private static async Task<IResult> ResetPasswordAsync(int id, ResetPasswordRequest request, AppDbContext db, IPasswordHasher hasher, TimeProvider clock)
    {
        var errors = AccountRules.ValidatePassword(request.Password);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return AccountNotFound(id);
        }

        user.PasswordHash = hasher.Hash(request.Password);
        await db.SaveChangesAsync();
        await db.RevokeUserSessionsAsync(id, clock.GetUtcNow().UtcDateTime); // facility-0014: every device must log in again

        return Results.NoContent();
    }

    private static async Task<IResult> DeactivateAsync(int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        if (IsSelf(principal, id))
        {
            return Results.BadRequest(new { message = "ปิดใช้งานบัญชีที่ตัวเองใช้อยู่ไม่ได้" });
        }

        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return AccountNotFound(id);
        }

        user.IsActive = false;
        await db.SaveChangesAsync();
        await db.RevokeUserSessionsAsync(id, clock.GetUtcNow().UtcDateTime); // facility-0014: sessions end within 5 minutes

        return Results.NoContent();
    }

    private static async Task<IResult> ActivateAsync(int id, AppDbContext db)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return AccountNotFound(id);
        }

        user.IsActive = true;
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static bool IsSelf(ClaimsPrincipal principal, int id) =>
        principal.FindFirstValue(AuthClaims.UserId) == id.ToString();

    private static IResult AccountNotFound(int id) =>
        Results.NotFound(new { message = $"ไม่พบบัญชีหมายเลข {id}" });

    private static IResult ValidationFailed(Dictionary<string, string[]> errors) =>
        Results.BadRequest(new { message = string.Join(" · ", errors.Values.SelectMany(v => v)), errors });

    private static AdminUserDto ToDto(User user) =>
        new(user.Id, user.Username, user.FullName, user.Role, user.IsActive, user.CreatedAt);
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, replace:

```csharp
app.MapAuthEndpoints();
```

with:

```csharp
app.MapAuthEndpoints();

// Admin: accounts page (ADR facility-0020)
app.MapAdminUserEndpoints();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/FacilityRealtime.slnx --nologo`
Expected: PASS. Unit tests 69, API tests 65.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Api backend/tests/FacilityRealtime.ApiTests/Admin
git commit -m "feat(accounts): add admin-only routes to create, edit, reset and deactivate accounts (#17)"
```

---

### Task 3: Frontend data layer for accounts

**Files:**
- Modify: `frontend/src/types/index.ts` (append account types)
- Create: `frontend/src/services/apiError.ts`, `frontend/src/services/apiError.test.ts`
- Create: `frontend/src/services/adminUsersApi.ts`
- Create: `frontend/src/features/admin/logic/accountValidation.ts`, `frontend/src/features/admin/logic/accountValidation.test.ts`

**Interfaces:**
- Consumes: `apiFetch` (plan 2A); the routes and JSON shapes from Task 2
- Produces:
  - `type UserRole = 'cleaner' | 'admin'`; `AdminUser`, `CreateUserInput`, `UpdateUserInput` in `frontend/src/types/index.ts`
  - `toApiError(res: Response, fallback: string): Promise<Error>` — reused by plan 2C
  - `listUsersApi()`, `createUserApi(input)`, `updateUserApi(id, input)`, `resetPasswordApi(id, password)`, `setUserActiveApi(id, isActive)`
  - `validateNewAccount`, `validateAccountEdit`, `validateUsername`, `validateFullName`, `validatePassword`, `validateRole`, `hasErrors`, `MIN_PASSWORD_LENGTH`, types `NewAccountErrors`, `AccountEditErrors`

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/services/apiError.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { toApiError } from './apiError';

describe('toApiError', () => {
  it('uses the message the server sent', async () => {
    const res = new Response(JSON.stringify({ message: 'ชื่อผู้ใช้นี้มีอยู่แล้ว' }), { status: 409 });

    expect((await toApiError(res, 'สร้างบัญชีไม่สำเร็จ')).message).toBe('ชื่อผู้ใช้นี้มีอยู่แล้ว');
  });

  it.each([
    ['an empty body', null],
    ['a body that is not JSON', 'Internal Server Error'],
    ['JSON without a message', JSON.stringify({ title: 'Bad Request' })],
    ['a blank message', JSON.stringify({ message: '  ' })],
  ])('falls back when the response has %s', async (_label, body) => {
    const res = new Response(body, { status: 500 });

    expect((await toApiError(res, 'สร้างบัญชีไม่สำเร็จ')).message).toBe('สร้างบัญชีไม่สำเร็จ');
  });
});
```

Create `frontend/src/features/admin/logic/accountValidation.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import type { UserRole } from '../../../types';
import {
  hasErrors,
  validateAccountEdit,
  validateFullName,
  validateNewAccount,
  validatePassword,
  validateUsername
} from './accountValidation';

const valid = { username: 'mali.k', fullName: 'แม่บ้าน มาลี', password: 'temp-pass1', role: 'cleaner' as UserRole };

describe('validateNewAccount', () => {
  it('accepts a complete cleaner account', () => {
    expect(validateNewAccount(valid)).toEqual({});
    expect(hasErrors(validateNewAccount(valid))).toBe(false);
  });

  it('reports every problem at once', () => {
    const errors = validateNewAccount({ username: 'A', fullName: ' ', password: 'short', role: 'manager' as UserRole });

    expect(Object.keys(errors).sort()).toEqual(['fullName', 'password', 'role', 'username']);
    expect(hasErrors(errors)).toBe(true);
  });
});

describe('validateUsername', () => {
  it.each(['cleaner01', 'mali.k', 'x_y-z', 'abc', 'a'.repeat(50)])('accepts %s', (username) => {
    expect(validateUsername(username)).toBeUndefined();
  });

  it.each([
    ['too short', 'ab'],
    ['too long', 'a'.repeat(51)],
    ['uppercase', 'Somchai'],
    ['a space', 'som chai'],
    ['Thai letters', 'สมชาย'],
    ['surrounding spaces', ' somchai ']
  ])('rejects %s', (_label, username) => {
    expect(validateUsername(username)).toBe('ใช้ a-z, 0-9, จุด, ขีดล่าง หรือขีดกลาง ยาว 3–50 ตัว');
  });
});

describe('validateFullName', () => {
  it('requires a name that is not just spaces', () => {
    expect(validateFullName('   ')).toBe('กรอกชื่อที่แสดง');
  });

  it('allows 150 characters after trimming and rejects 151', () => {
    expect(validateFullName(` ${'ก'.repeat(150)} `)).toBeUndefined();
    expect(validateFullName('ก'.repeat(151))).toBe('ชื่อที่แสดงยาวได้ไม่เกิน 150 ตัวอักษร');
  });
});

describe('validatePassword', () => {
  it('needs at least 8 characters', () => {
    expect(validatePassword('1234567')).toBe('รหัสผ่านอย่างน้อย 8 ตัวอักษร');
    expect(validatePassword('12345678')).toBeUndefined();
  });
});

describe('validateAccountEdit', () => {
  it('checks only the display name and role', () => {
    expect(validateAccountEdit({ fullName: 'ผู้ดูแลระบบ', role: 'admin' })).toEqual({});
    expect(validateAccountEdit({ fullName: '', role: 'admin' })).toEqual({ fullName: 'กรอกชื่อที่แสดง' });
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npm --prefix frontend test`
Expected: FAIL with `Failed to resolve import "./apiError"` and `Failed to resolve import "./accountValidation"`.

- [ ] **Step 3: Add the types, the error helper, the API client and the validation**

Append to `frontend/src/types/index.ts`:

```ts
/** Roles are stored lowercase (ADR facility-0020). */
export type UserRole = 'cleaner' | 'admin';

/** A row on the accounts page: GET /api/admin/users. */
export interface AdminUser {
  id: number;
  username: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface CreateUserInput {
  username: string;
  fullName: string;
  password: string;
  role: UserRole;
}

export interface UpdateUserInput {
  fullName: string;
  role: UserRole;
}
```

Create `frontend/src/services/apiError.ts`:

```ts
/** Turns a failed response into an Error that carries the server's message when it sent one. */
export async function toApiError(res: Response, fallback: string): Promise<Error> {
  const body = (await res.json().catch(() => null)) as { message?: unknown } | null;
  const message = typeof body?.message === 'string' && body.message.trim() ? body.message : fallback;
  return new Error(message);
}
```

Create `frontend/src/services/adminUsersApi.ts`:

```ts
import type { AdminUser, CreateUserInput, UpdateUserInput } from '../types';
import { apiFetch } from './authSession';
import { toApiError } from './apiError';

// ADR facility-0010 and facility-0020: every route here is admin-only on the server
const BASE_URL = '/api/admin/users';
const JSON_HEADERS = { 'Content-Type': 'application/json' };

export async function listUsersApi(): Promise<AdminUser[]> {
  const res = await apiFetch(BASE_URL);
  if (!res.ok) {
    throw await toApiError(res, 'โหลดรายชื่อบัญชีไม่สำเร็จ');
  }
  return res.json();
}

export async function createUserApi(input: CreateUserInput): Promise<AdminUser> {
  const res = await apiFetch(BASE_URL, { method: 'POST', headers: JSON_HEADERS, body: JSON.stringify(input) });
  if (!res.ok) {
    throw await toApiError(res, 'สร้างบัญชีไม่สำเร็จ');
  }
  return res.json();
}

export async function updateUserApi(id: number, input: UpdateUserInput): Promise<AdminUser> {
  const res = await apiFetch(`${BASE_URL}/${id}`, { method: 'PUT', headers: JSON_HEADERS, body: JSON.stringify(input) });
  if (!res.ok) {
    throw await toApiError(res, 'บันทึกบัญชีไม่สำเร็จ');
  }
  return res.json();
}

export async function resetPasswordApi(id: number, password: string): Promise<void> {
  const res = await apiFetch(`${BASE_URL}/${id}/reset-password`, {
    method: 'POST',
    headers: JSON_HEADERS,
    body: JSON.stringify({ password })
  });
  if (!res.ok) {
    throw await toApiError(res, 'รีเซ็ตรหัสผ่านไม่สำเร็จ');
  }
}

export async function setUserActiveApi(id: number, isActive: boolean): Promise<void> {
  const res = await apiFetch(`${BASE_URL}/${id}/${isActive ? 'activate' : 'deactivate'}`, { method: 'POST' });
  if (!res.ok) {
    throw await toApiError(res, isActive ? 'เปิดใช้งานบัญชีไม่สำเร็จ' : 'ปิดใช้งานบัญชีไม่สำเร็จ');
  }
}
```

Create `frontend/src/features/admin/logic/accountValidation.ts`:

```ts
import type { UserRole } from '../../../types';

// The same rules AccountRules.cs enforces on the server. The server stays the authority;
// these only let the drawer show the problem before a round trip.
export const USERNAME_PATTERN = /^[a-z0-9._-]{3,50}$/;
export const FULL_NAME_MAX_LENGTH = 150;
export const MIN_PASSWORD_LENGTH = 8;
export const ROLES: readonly UserRole[] = ['cleaner', 'admin'];

export interface NewAccountValues {
  username: string;
  fullName: string;
  password: string;
  role: UserRole;
}

export interface AccountEditValues {
  fullName: string;
  role: UserRole;
}

export type NewAccountErrors = Partial<Record<keyof NewAccountValues, string>>;
export type AccountEditErrors = Partial<Record<keyof AccountEditValues, string>>;

export function validateUsername(username: string): string | undefined {
  return USERNAME_PATTERN.test(username) ? undefined : 'ใช้ a-z, 0-9, จุด, ขีดล่าง หรือขีดกลาง ยาว 3–50 ตัว';
}

export function validateFullName(fullName: string): string | undefined {
  const value = fullName.trim();
  if (!value) {
    return 'กรอกชื่อที่แสดง';
  }
  if (value.length > FULL_NAME_MAX_LENGTH) {
    return `ชื่อที่แสดงยาวได้ไม่เกิน ${FULL_NAME_MAX_LENGTH} ตัวอักษร`;
  }
  return undefined;
}

export function validatePassword(password: string): string | undefined {
  return password.length >= MIN_PASSWORD_LENGTH ? undefined : `รหัสผ่านอย่างน้อย ${MIN_PASSWORD_LENGTH} ตัวอักษร`;
}

export function validateRole(role: string): string | undefined {
  return (ROLES as readonly string[]).includes(role) ? undefined : 'เลือกสิทธิ์ cleaner หรือ admin';
}

function compact<K extends string>(errors: Record<K, string | undefined>): Partial<Record<K, string>> {
  const result: Partial<Record<K, string>> = {};
  for (const key of Object.keys(errors) as K[]) {
    const message = errors[key];
    if (message) {
      result[key] = message;
    }
  }
  return result;
}

export function validateNewAccount(values: NewAccountValues): NewAccountErrors {
  return compact({
    username: validateUsername(values.username),
    fullName: validateFullName(values.fullName),
    password: validatePassword(values.password),
    role: validateRole(values.role)
  });
}

export function validateAccountEdit(values: AccountEditValues): AccountEditErrors {
  return compact({
    fullName: validateFullName(values.fullName),
    role: validateRole(values.role)
  });
}

export function hasErrors(errors: object): boolean {
  return Object.keys(errors).length > 0;
}
```

- [ ] **Step 4: Run the tests and the type check**

Run: `npm --prefix frontend test`
Expected: PASS, `Tests  30 passed (30)`.

Run: `npm --prefix frontend run build`
Expected: `built in`, no type errors.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/types/index.ts frontend/src/services/apiError.ts frontend/src/services/apiError.test.ts frontend/src/services/adminUsersApi.ts frontend/src/features/admin/logic
git commit -m "feat(accounts-web): add account API client and client-side field rules (#17)"
```

---

### Task 4: Admin menu, shared admin styles and toast

**Files:**
- Create: `frontend/src/features/admin/admin.css`
- Create: `frontend/src/features/admin/adminTabs.ts`, `frontend/src/features/admin/adminTabs.test.ts`
- Create: `frontend/src/features/admin/components/AdminLayout.tsx`
- Create: `frontend/src/features/admin/components/Toast.tsx`
- Create: `frontend/src/features/admin/hooks/useToast.ts`

**Interfaces:**
- Consumes: the CSS tokens in `frontend/src/styles/index.css`
- Produces:
  - `type AdminTabKey = 'accounts'`, `interface AdminTab { key; path; label }`, `ADMIN_TABS`, `adminTabForPath(path): AdminTab | undefined` — plan 2C adds `'points'`
  - `AdminLayout({ activeTab, onNavigate, children })`
  - `Toast({ toast, onDismiss })`, `interface ToastMessage { text; actionLabel?; onAction? }`
  - `useToast(durationMs = 6000)` → `{ toast, show(text, action?), dismiss }`
  - CSS classes `admin-page`, `admin-tabs`, `admin-tab`, `admin-title`, `admin-count`, `admin-toolbar`, `admin-search`, `admin-check`, `admin-spacer`, `admin-btn` (+ `-primary`, `-warning`, `-ghost`, `-sm`), `admin-table-wrap`, `admin-table` (`is-inactive`, `is-flash`), `admin-actions`, `admin-strong`, `admin-muted`, `admin-mono`, `admin-badge` (+ `-on`, `-off`, `-role`), `admin-empty`, `admin-error`, `admin-scrim`, `admin-drawer`, `admin-drawer-sub`, `admin-drawer-form`, `admin-drawer-footer`, `admin-field` (`is-invalid`), `admin-field-help`, `admin-field-error`, `admin-dialog-backdrop`, `admin-dialog`, `admin-warning`, `admin-dialog-footer`, `admin-toast`

- [ ] **Step 1: Write the failing test**

Create `frontend/src/features/admin/adminTabs.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { ADMIN_TABS, adminTabForPath } from './adminTabs';

describe('adminTabForPath', () => {
  it('finds the tab for its own path and anything below it', () => {
    expect(adminTabForPath('/admin/accounts')?.key).toBe('accounts');
    expect(adminTabForPath('/admin/accounts/42')?.key).toBe('accounts');
  });

  it('does not match a path that only starts with the same letters', () => {
    expect(adminTabForPath('/admin/accountsx')).toBeUndefined();
  });

  it('returns undefined for the bare admin path so the router can fall back to the first tab', () => {
    expect(adminTabForPath('/admin')).toBeUndefined();
    expect(ADMIN_TABS[0].path).toBe('/admin/accounts');
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm --prefix frontend test`
Expected: FAIL with `Failed to resolve import "./adminTabs"`.

- [ ] **Step 3: Write the tabs, the styles, the layout and the toast**

Create `frontend/src/features/admin/adminTabs.ts`:

```ts
export type AdminTabKey = 'accounts';

export interface AdminTab {
  key: AdminTabKey;
  path: string;
  label: string;
}

/** The Admin menu. ADR facility-0021 puts the service points page in this same menu; plan 2C adds its tab. */
export const ADMIN_TABS: readonly AdminTab[] = [
  { key: 'accounts', path: '/admin/accounts', label: 'บัญชีผู้ใช้' }
];

/** The tab a path belongs to, or undefined when the path is not one of the admin pages. */
export function adminTabForPath(path: string): AdminTab | undefined {
  return ADMIN_TABS.find((tab) => path === tab.path || path.startsWith(`${tab.path}/`));
}
```

Create `frontend/src/features/admin/admin.css`:

```css
/* Admin pages: accounts (ADR facility-0020) and service points (facility-0021).
   Colours, radii and shadows come from the tokens in styles/index.css. */

.admin-page {
  flex: 1;
  width: 100%;
  max-width: 1280px;
  margin: 0 auto;
  padding: 20px 32px 48px;
}

.admin-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-bottom: 20px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--border-subtle);
}

.admin-tab {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 14px;
  border: none;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--text-secondary);
  font: inherit;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
}

.admin-tab:hover { color: var(--text-primary); }
.admin-tab[aria-current='page'] { background: rgba(56, 189, 248, 0.15); color: var(--color-brand); }

.admin-title {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 12px;
  margin-bottom: 14px;
}

.admin-title h1 { font-size: 22px; font-weight: 700; letter-spacing: -0.3px; }
.admin-count { color: var(--text-muted); font-size: 14px; font-variant-numeric: tabular-nums; }

.admin-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
  margin-bottom: 14px;
}

.admin-search {
  flex: 1 1 220px;
  max-width: 360px;
  padding: 9px 12px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  background: var(--bg-card);
  color: var(--text-primary);
  font: inherit;
  font-size: 14px;
}

.admin-check { display: flex; align-items: center; gap: 8px; font-size: 13px; color: var(--text-secondary); cursor: pointer; }
.admin-check input { width: 16px; height: 16px; accent-color: var(--color-brand); }
.admin-spacer { flex: 1; }

.admin-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 9px 14px;
  border: 1px solid var(--border-highlight);
  border-radius: var(--radius-sm);
  background: var(--bg-card-elevated);
  color: var(--text-primary);
  font: inherit;
  font-size: 14px;
  font-weight: 600;
  white-space: nowrap;
  cursor: pointer;
}

.admin-btn:hover:not(:disabled) { border-color: var(--color-brand); }
.admin-btn:disabled { opacity: 0.45; cursor: not-allowed; }
.admin-btn:focus-visible, .admin-tab:focus-visible { outline: 2px solid var(--color-brand); outline-offset: 2px; }
.admin-btn-primary { background: var(--color-brand); border-color: var(--color-brand); color: #082f49; }
.admin-btn-warning { background: var(--color-overdue); border-color: var(--color-overdue); color: #431407; }
.admin-btn-ghost { background: transparent; border-color: transparent; color: var(--text-secondary); }
.admin-btn-ghost:hover:not(:disabled) { background: var(--bg-card); border-color: transparent; color: var(--text-primary); }
.admin-btn-sm { padding: 6px 10px; font-size: 13px; }

.admin-table-wrap {
  overflow-x: auto;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  background: var(--bg-card);
  box-shadow: var(--shadow-card);
}

.admin-table { width: 100%; min-width: 760px; border-collapse: collapse; }

.admin-table th,
.admin-table td {
  padding: 12px 14px;
  border-bottom: 1px solid var(--border-subtle);
  text-align: left;
  vertical-align: middle;
}

.admin-table th {
  background: rgba(15, 23, 42, 0.5);
  color: var(--text-muted);
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.admin-table tbody tr:last-child td { border-bottom: none; }
.admin-table tr.is-inactive td:not(.admin-actions) { opacity: 0.45; }
.admin-table tr.is-flash { animation: adminRowFlash 1.2s ease-out; }

@keyframes adminRowFlash {
  from { background: rgba(56, 189, 248, 0.15); }
  to { background: transparent; }
}

.admin-actions { text-align: right; white-space: nowrap; }
.admin-strong { font-weight: 600; }
.admin-muted { color: var(--text-secondary); font-size: 12px; }
.admin-mono { color: var(--text-secondary); font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; }

.admin-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 3px 10px;
  border-radius: var(--radius-full);
  font-size: 12px;
  font-weight: 600;
}

.admin-badge-on { background: var(--color-normal-bg); color: var(--color-normal); }
.admin-badge-off { background: var(--color-offhours-bg); color: var(--text-secondary); }
.admin-badge-role { background: rgba(56, 189, 248, 0.12); color: var(--color-brand); }

.admin-empty { padding: 32px; color: var(--text-secondary); text-align: center; }

.admin-error {
  margin-bottom: 14px;
  padding: 12px 14px;
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: var(--radius-md);
  background: var(--color-issue-bg);
  color: #fca5a5;
  font-size: 14px;
}

.admin-scrim { position: fixed; inset: 0; z-index: 40; background: rgba(2, 6, 23, 0.6); }

.admin-drawer {
  position: fixed;
  top: 0;
  right: 0;
  z-index: 50;
  display: flex;
  flex-direction: column;
  gap: 18px;
  width: min(440px, 100vw);
  height: 100vh;
  padding: 24px;
  overflow-y: auto;
  border-left: 1px solid var(--border-highlight);
  background: var(--bg-secondary);
  box-shadow: -20px 0 40px rgba(0, 0, 0, 0.4);
}

.admin-drawer h2, .admin-dialog h2 { font-size: 18px; font-weight: 700; }
.admin-drawer-sub { margin-top: 2px; color: var(--text-secondary); font-size: 13px; }

.admin-drawer-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: auto;
  padding-top: 12px;
  border-top: 1px solid var(--border-subtle);
}

.admin-field { display: flex; flex-direction: column; gap: 6px; }
.admin-field label { color: var(--text-secondary); font-size: 13px; font-weight: 600; }

.admin-field input,
.admin-field select {
  padding: 10px 12px;
  border: 1px solid var(--border-highlight);
  border-radius: var(--radius-sm);
  background: var(--bg-primary);
  color: var(--text-primary);
  font: inherit;
  font-size: 15px;
}

.admin-field input:disabled, .admin-field select:disabled { opacity: 0.6; }
.admin-field-help { color: var(--text-muted); font-size: 12px; }
.admin-field-error { color: var(--color-issue); font-size: 12px; }
.admin-field.is-invalid input, .admin-field.is-invalid select { border-color: var(--color-issue); }

.admin-dialog-backdrop {
  position: fixed;
  inset: 0;
  z-index: 60;
  display: grid;
  place-items: center;
  padding: 16px;
  background: rgba(2, 6, 23, 0.7);
}

.admin-dialog {
  width: min(480px, 100%);
  padding: 24px;
  border: 1px solid var(--border-highlight);
  border-radius: var(--radius-lg);
  background: var(--bg-secondary);
  box-shadow: var(--shadow-card);
}

.admin-warning {
  display: flex;
  gap: 10px;
  margin: 14px 0;
  padding: 12px;
  border: 1px solid rgba(249, 115, 22, 0.35);
  border-radius: var(--radius-sm);
  background: var(--color-overdue-bg);
  font-size: 14px;
}

.admin-dialog-footer { display: flex; justify-content: flex-end; gap: 8px; margin-top: 18px; }

.admin-toast {
  position: fixed;
  bottom: 24px;
  left: 50%;
  z-index: 80;
  display: flex;
  align-items: center;
  gap: 12px;
  max-width: calc(100vw - 32px);
  padding: 10px 16px;
  border: 1px solid var(--border-highlight);
  border-radius: var(--radius-full);
  background: var(--bg-card-elevated);
  box-shadow: var(--shadow-card);
  font-size: 14px;
  transform: translateX(-50%);
}

.admin-toast button { border: none; background: transparent; color: var(--color-brand); font: inherit; font-weight: 700; cursor: pointer; }
.admin-toast .admin-toast-close { color: var(--text-muted); font-size: 18px; line-height: 1; }

@media (max-width: 768px) {
  .admin-page { padding: 16px 14px 40px; }
}

@media (prefers-reduced-motion: reduce) {
  .admin-table tr.is-flash { animation: none; }
}

/* Lets a <form> sit inside the drawer without breaking its flex gap */
.admin-drawer-form { display: contents; }
```

Create `frontend/src/features/admin/components/AdminLayout.tsx`:

```tsx
import React from 'react';
import { ADMIN_TABS, type AdminTab } from '../adminTabs';
import '../admin.css';

interface AdminLayoutProps {
  activeTab: AdminTab;
  onNavigate: (path: string) => void;
  children: React.ReactNode;
}

export const AdminLayout: React.FC<AdminLayoutProps> = ({ activeTab, onNavigate, children }) => (
  <div className="admin-page">
    <nav className="admin-tabs" aria-label="เมนูผู้ดูแลระบบ">
      {ADMIN_TABS.map((tab) => (
        <button
          key={tab.key}
          type="button"
          className="admin-tab"
          aria-current={tab.key === activeTab.key ? 'page' : undefined}
          onClick={() => onNavigate(tab.path)}
        >
          {tab.label}
        </button>
      ))}
    </nav>
    {children}
  </div>
);
```

Create `frontend/src/features/admin/components/Toast.tsx`:

```tsx
import React from 'react';

export interface ToastMessage {
  text: string;
  actionLabel?: string;
  onAction?: () => void;
}

interface ToastProps {
  toast: ToastMessage | null;
  onDismiss: () => void;
}

export const Toast: React.FC<ToastProps> = ({ toast, onDismiss }) => {
  if (!toast) {
    return null;
  }

  return (
    <div className="admin-toast" role="status" aria-live="polite">
      <span>{toast.text}</span>
      {toast.actionLabel && toast.onAction && (
        <button
          type="button"
          onClick={() => {
            toast.onAction?.();
            onDismiss();
          }}
        >
          {toast.actionLabel}
        </button>
      )}
      <button type="button" className="admin-toast-close" onClick={onDismiss} aria-label="ปิดข้อความ">
        ×
      </button>
    </div>
  );
};
```

Create `frontend/src/features/admin/hooks/useToast.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ToastMessage } from '../components/Toast';

/** One toast at a time; a new message replaces the old one and restarts the timer. */
export function useToast(durationMs = 6000) {
  const [toast, setToast] = useState<ToastMessage | null>(null);
  const timer = useRef<number | undefined>(undefined);

  const dismiss = useCallback(() => {
    window.clearTimeout(timer.current);
    setToast(null);
  }, []);

  const show = useCallback(
    (text: string, action?: { label: string; run: () => void }) => {
      window.clearTimeout(timer.current);
      setToast({ text, actionLabel: action?.label, onAction: action?.run });
      timer.current = window.setTimeout(() => setToast(null), durationMs);
    },
    [durationMs]
  );

  useEffect(() => () => window.clearTimeout(timer.current), []);

  return { toast, show, dismiss };
}
```

- [ ] **Step 4: Run the tests, the type check and the linter**

Run: `npm --prefix frontend test`
Expected: PASS, `Tests  33 passed (33)`.

Run: `npm --prefix frontend run build` and `npm --prefix frontend run lint`
Expected: `built in`; no line of lint output reports an error.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/features/admin/admin.css frontend/src/features/admin/adminTabs.ts frontend/src/features/admin/adminTabs.test.ts frontend/src/features/admin/components frontend/src/features/admin/hooks
git commit -m "feat(accounts-web): add admin menu layout, shared admin styles and toast (#17)"
```

---

### Task 5: Accounts page and the `/admin` route

The repo has no React component test setup, so the compiler drives the red step and Task 6 checks the screens by hand against the table in ADR facility-0020.

**Files:**
- Create: `frontend/src/features/admin/hooks/useAccountsPage.ts`
- Create: `frontend/src/features/admin/hooks/useAccountForm.ts`
- Create: `frontend/src/features/admin/hooks/useResetPassword.ts`
- Create: `frontend/src/features/admin/components/AccountsPage.tsx`
- Create: `frontend/src/features/admin/components/AccountFormDrawer.tsx`
- Create: `frontend/src/features/admin/components/ResetPasswordDialog.tsx`
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Consumes: Task 3 API client and validation; Task 4 layout, styles, toast; `useAuth()` with `isAdmin` and `currentUser` (plan 2A)
- Produces:
  - `AccountsPage({ currentUserId })` rendered at `/admin/accounts` for admins; `/admin` falls back to the first tab
  - `useAccountsPage(currentUserId)`, `useAccountForm(initialUser, onSaved)`, `useResetPassword(user, onDone)`
  - Navbar button `จัดการระบบ`, shown only when `isAdmin`

- [ ] **Step 1: Wire the route and the navbar button first**

In `frontend/src/App.tsx`, replace:

```tsx
import { LayoutDashboard, Smartphone } from 'lucide-react';
```

with:

```tsx
import { LayoutDashboard, ShieldCheck, Smartphone } from 'lucide-react';
import { AdminLayout } from './features/admin/components/AdminLayout';
import { AccountsPage } from './features/admin/components/AccountsPage';
import { ADMIN_TABS, adminTabForPath } from './features/admin/adminTabs';
```

Replace:

```tsx
  const { currentUser, logout, isAuthenticated, isLoading } = useAuth();
```

with:

```tsx
  const { currentUser, logout, isAuthenticated, isLoading, isAdmin } = useAuth();
```

After the line that declares `tokenFromUrl`, add:

```tsx
  const isAdminRoute = currentPath === '/admin' || currentPath.startsWith('/admin/');
  const adminTab = adminTabForPath(currentPath) ?? ADMIN_TABS[0];
```

In the Dashboard tab button's style, change `background: !isScanRoute ?` to `background: !isScanRoute && !isAdminRoute ?` and `color: !isScanRoute ?` to `color: !isScanRoute && !isAdminRoute ?`, so the Dashboard tab is not highlighted on admin pages.

Directly after the closing `</button>` of the `หน้าสแกนบนมือถือ` tab, add:

```tsx

          {isAdmin && (
            <button
              onClick={() => navigateTo(ADMIN_TABS[0].path)}
              className="dev-navbar-tab"
              style={{
                ...styles.navTab,
                background: isAdminRoute ? 'rgba(56, 189, 248, 0.2)' : 'transparent',
                color: isAdminRoute ? '#38bdf8' : '#94a3b8'
              }}
            >
              <ShieldCheck size={16} />
              <span>จัดการระบบ</span>
            </button>
          )}
```

Replace the login redirect:

```tsx
            onSuccess={() => navigateTo(isScanRoute ? `/scan/${tokenFromUrl}` : '/dashboard')}
```

with:

```tsx
            onSuccess={() => navigateTo(isScanRoute ? `/scan/${tokenFromUrl}` : isAdminRoute ? adminTab.path : '/dashboard')}
```

Replace:

```tsx
        ) : isScanRoute ? (
          <ScanRecordPage
```

with:

```tsx
        ) : isAdminRoute ? (
          // ADR facility-0010: the server refuses non-admins anyway; this only avoids showing them a page of errors
          isAdmin ? (
            <AdminLayout activeTab={adminTab} onNavigate={navigateTo}>
              <AccountsPage currentUserId={currentUser!.id} />
            </AdminLayout>
          ) : (
            <div style={styles.sessionCheck}>หน้านี้สำหรับผู้ดูแลระบบเท่านั้น</div>
          )
        ) : isScanRoute ? (
          <ScanRecordPage
```

- [ ] **Step 2: Build to confirm the page is missing**

Run: `npm --prefix frontend run build`
Expected: FAIL with `Cannot find module './features/admin/components/AccountsPage'`.

- [ ] **Step 3: Write the three hooks**

Create `frontend/src/features/admin/hooks/useAccountsPage.ts`:

```ts
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { AdminUser } from '../../../types';
import { listUsersApi, setUserActiveApi } from '../../../services/adminUsersApi';
import { useToast } from './useToast';

export type AccountDrawerState = { mode: 'create' } | { mode: 'edit'; user: AdminUser } | null;

const byUsername = (a: AdminUser, b: AdminUser) => a.username.localeCompare(b.username);

export function useAccountsPage(currentUserId: number) {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [search, setSearch] = useState<string>('');
  const [drawer, setDrawer] = useState<AccountDrawerState>(null);
  const [resetTarget, setResetTarget] = useState<AdminUser | null>(null);
  const [flashId, setFlashId] = useState<number | null>(null);
  const flashTimer = useRef<number | undefined>(undefined);
  const { toast, show: showToast, dismiss: dismissToast } = useToast();

  const load = useCallback(async () => {
    try {
      const data = await listUsersApi();
      setUsers([...data].sort(byUsername));
      setLoadError(null);
    } catch (err: unknown) {
      setLoadError(err instanceof Error ? err.message : 'โหลดรายชื่อบัญชีไม่สำเร็จ');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
    return () => window.clearTimeout(flashTimer.current);
  }, [load]);

  const flash = useCallback((id: number) => {
    window.clearTimeout(flashTimer.current);
    setFlashId(id);
    flashTimer.current = window.setTimeout(() => setFlashId(null), 1500);
  }, []);

  const visibleUsers = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) {
      return users;
    }
    return users.filter((u) => u.username.includes(query) || u.fullName.toLowerCase().includes(query));
  }, [users, search]);

  const activeCount = useMemo(() => users.filter((u) => u.isActive).length, [users]);

  const handleSaved = useCallback(
    (saved: AdminUser, wasCreate: boolean) => {
      setUsers((prev) => [...prev.filter((u) => u.id !== saved.id), saved].sort(byUsername));
      setDrawer(null);
      flash(saved.id);
      showToast(
        wasCreate
          ? `สร้างบัญชี ${saved.username} แล้ว — แจ้งรหัสผ่านชั่วคราวให้เจ้าของบัญชีด้วยตัวเอง`
          : `บันทึก ${saved.username} แล้ว — สิทธิ์ใหม่มีผลกับเครื่องที่ login อยู่ภายใน 5 นาที`
      );
    },
    [flash, showToast]
  );

  const toggleActive = useCallback(
    async (user: AdminUser) => {
      const next = !user.isActive;
      try {
        await setUserActiveApi(user.id, next);
        setUsers((prev) => prev.map((u) => (u.id === user.id ? { ...u, isActive: next } : u)));
        flash(user.id);
        showToast(
          next
            ? `เปิดใช้งาน ${user.username} แล้ว`
            : `ปิดใช้งาน ${user.username} แล้ว — ทุกเครื่องจะออกจากระบบภายใน 5 นาที ประวัติสแกนยังอยู่`
        );
      } catch (err: unknown) {
        showToast(err instanceof Error ? err.message : 'เปลี่ยนสถานะบัญชีไม่สำเร็จ');
      }
    },
    [flash, showToast]
  );

  const handlePasswordReset = useCallback(
    (user: AdminUser) => {
      setResetTarget(null);
      showToast(`รีเซ็ตรหัสผ่านของ ${user.username} แล้ว — ทุกเครื่องต้อง login ใหม่`);
    },
    [showToast]
  );

  return {
    users: visibleUsers,
    totalCount: users.length,
    activeCount,
    isLoading,
    loadError,
    search,
    setSearch,
    drawer,
    openCreate: () => setDrawer({ mode: 'create' }),
    openEdit: (user: AdminUser) => setDrawer({ mode: 'edit', user }),
    closeDrawer: () => setDrawer(null),
    handleSaved,
    resetTarget,
    openReset: (user: AdminUser) => setResetTarget(user),
    closeReset: () => setResetTarget(null),
    handlePasswordReset,
    toggleActive,
    isSelf: (user: AdminUser) => user.id === currentUserId,
    flashId,
    toast,
    dismissToast
  };
}
```

Create `frontend/src/features/admin/hooks/useAccountForm.ts`:

```ts
import { useState } from 'react';
import type { FormEvent } from 'react';
import type { AdminUser, UserRole } from '../../../types';
import { createUserApi, updateUserApi } from '../../../services/adminUsersApi';
import { hasErrors, validateAccountEdit, validateNewAccount } from '../logic/accountValidation';
import type { NewAccountErrors } from '../logic/accountValidation';

/** Create when initialUser is null, otherwise edit that account (username stays fixed: ADR facility-0020). */
export function useAccountForm(initialUser: AdminUser | null, onSaved: (user: AdminUser, wasCreate: boolean) => void) {
  const [username, setUsername] = useState<string>(initialUser?.username ?? '');
  const [fullName, setFullName] = useState<string>(initialUser?.fullName ?? '');
  const [password, setPassword] = useState<string>('');
  const [role, setRole] = useState<UserRole>(initialUser?.role ?? 'cleaner');
  const [errors, setErrors] = useState<NewAccountErrors>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const nextErrors: NewAccountErrors = initialUser
      ? validateAccountEdit({ fullName, role })
      : validateNewAccount({ username, fullName, password, role });
    setErrors(nextErrors);
    if (hasErrors(nextErrors)) {
      return;
    }

    setIsSaving(true);
    setSubmitError(null);
    try {
      const saved = initialUser
        ? await updateUserApi(initialUser.id, { fullName: fullName.trim(), role })
        : await createUserApi({ username, fullName: fullName.trim(), password, role });
      onSaved(saved, initialUser === null);
    } catch (err: unknown) {
      setSubmitError(err instanceof Error ? err.message : 'บันทึกบัญชีไม่สำเร็จ');
    } finally {
      setIsSaving(false);
    }
  };

  return {
    isEdit: initialUser !== null,
    username,
    setUsername,
    fullName,
    setFullName,
    password,
    setPassword,
    role,
    setRole,
    errors,
    submitError,
    isSaving,
    submit
  };
}
```

Create `frontend/src/features/admin/hooks/useResetPassword.ts`:

```ts
import { useState } from 'react';
import type { FormEvent } from 'react';
import type { AdminUser } from '../../../types';
import { resetPasswordApi } from '../../../services/adminUsersApi';
import { validatePassword } from '../logic/accountValidation';

export function useResetPassword(user: AdminUser, onDone: (user: AdminUser) => void) {
  const [password, setPassword] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const problem = validatePassword(password);
    if (problem) {
      setError(problem);
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      await resetPasswordApi(user.id, password);
      onDone(user);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'รีเซ็ตรหัสผ่านไม่สำเร็จ');
    } finally {
      setIsSaving(false);
    }
  };

  return { password, setPassword, error, isSaving, submit };
}
```

- [ ] **Step 4: Write the three components**

Create `frontend/src/features/admin/components/AccountsPage.tsx`:

```tsx
import React from 'react';
import { KeyRound, Pencil, Power, UserPlus } from 'lucide-react';
import type { UserRole } from '../../../types';
import { useAccountsPage } from '../hooks/useAccountsPage';
import { AccountFormDrawer } from './AccountFormDrawer';
import { ResetPasswordDialog } from './ResetPasswordDialog';
import { Toast } from './Toast';

interface AccountsPageProps {
  currentUserId: number;
}

const ROLE_LABELS: Record<UserRole, string> = {
  cleaner: 'แม่บ้าน',
  admin: 'ผู้ดูแลระบบ'
};

export const AccountsPage: React.FC<AccountsPageProps> = ({ currentUserId }) => {
  const page = useAccountsPage(currentUserId);

  return (
    <>
      <div className="admin-title">
        <h1>บัญชีผู้ใช้</h1>
        <span className="admin-count">
          {page.activeCount} บัญชีเปิดใช้งาน · {page.totalCount - page.activeCount} ปิดใช้งาน
        </span>
      </div>

      <div className="admin-toolbar">
        <input
          id="account-search"
          className="admin-search"
          type="search"
          value={page.search}
          onChange={(e) => page.setSearch(e.target.value)}
          placeholder="ค้นหาชื่อผู้ใช้หรือชื่อที่แสดง"
          aria-label="ค้นหาบัญชี"
        />
        <div className="admin-spacer" />
        <button type="button" className="admin-btn admin-btn-primary" onClick={page.openCreate}>
          <UserPlus size={16} /> เพิ่มบัญชี
        </button>
      </div>

      {page.loadError && <div className="admin-error">{page.loadError}</div>}

      <div className="admin-table-wrap">
        <table className="admin-table">
          <thead>
            <tr>
              <th>ชื่อผู้ใช้</th>
              <th>ชื่อที่แสดง</th>
              <th>สิทธิ์</th>
              <th>สถานะ</th>
              <th aria-label="การทำงาน" />
            </tr>
          </thead>
          <tbody>
            {page.isLoading ? (
              <tr>
                <td colSpan={5} className="admin-empty">กำลังโหลดรายชื่อบัญชี...</td>
              </tr>
            ) : page.users.length === 0 ? (
              <tr>
                <td colSpan={5} className="admin-empty">
                  {page.totalCount === 0 ? 'ยังไม่มีบัญชี' : 'ไม่พบบัญชีที่ตรงกับคำค้น'}
                </td>
              </tr>
            ) : (
              page.users.map((user) => {
                const isSelf = page.isSelf(user);
                const rowClass = [user.isActive ? '' : 'is-inactive', page.flashId === user.id ? 'is-flash' : '']
                  .filter(Boolean)
                  .join(' ');
                return (
                  <tr key={user.id} className={rowClass}>
                    <td className="admin-mono">
                      {user.username}
                      {isSelf && <span className="admin-muted"> (คุณ)</span>}
                    </td>
                    <td className="admin-strong">{user.fullName}</td>
                    <td>
                      <span className="admin-badge admin-badge-role">{ROLE_LABELS[user.role]}</span>
                    </td>
                    <td>
                      {user.isActive ? (
                        <span className="admin-badge admin-badge-on">● เปิดใช้งาน</span>
                      ) : (
                        <span className="admin-badge admin-badge-off">○ ปิดใช้งาน</span>
                      )}
                    </td>
                    <td className="admin-actions">
                      <button type="button" className="admin-btn admin-btn-ghost admin-btn-sm" onClick={() => page.openEdit(user)}>
                        <Pencil size={14} /> แก้ไข
                      </button>
                      <button type="button" className="admin-btn admin-btn-ghost admin-btn-sm" onClick={() => page.openReset(user)}>
                        <KeyRound size={14} /> รีเซ็ตรหัสผ่าน
                      </button>
                      <button
                        type="button"
                        className="admin-btn admin-btn-ghost admin-btn-sm"
                        onClick={() => void page.toggleActive(user)}
                        disabled={isSelf}
                        title={isSelf ? 'ปิดใช้งานบัญชีที่ตัวเองใช้อยู่ไม่ได้' : undefined}
                      >
                        <Power size={14} /> {user.isActive ? 'ปิดใช้งาน' : 'เปิดใช้งาน'}
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {page.drawer && (
        <AccountFormDrawer
          key={page.drawer.mode === 'edit' ? page.drawer.user.id : 'create'}
          initialUser={page.drawer.mode === 'edit' ? page.drawer.user : null}
          isSelf={page.drawer.mode === 'edit' && page.isSelf(page.drawer.user)}
          onClose={page.closeDrawer}
          onSaved={page.handleSaved}
        />
      )}

      {page.resetTarget && (
        <ResetPasswordDialog user={page.resetTarget} onClose={page.closeReset} onDone={page.handlePasswordReset} />
      )}

      <Toast toast={page.toast} onDismiss={page.dismissToast} />
    </>
  );
};
```

Create `frontend/src/features/admin/components/AccountFormDrawer.tsx`:

```tsx
import React from 'react';
import type { AdminUser, UserRole } from '../../../types';
import { useAccountForm } from '../hooks/useAccountForm';
import { MIN_PASSWORD_LENGTH } from '../logic/accountValidation';

interface AccountFormDrawerProps {
  initialUser: AdminUser | null;
  isSelf: boolean;
  onClose: () => void;
  onSaved: (user: AdminUser, wasCreate: boolean) => void;
}

export const AccountFormDrawer: React.FC<AccountFormDrawerProps> = ({ initialUser, isSelf, onClose, onSaved }) => {
  const form = useAccountForm(initialUser, onSaved);

  return (
    <>
      <div className="admin-scrim" onClick={onClose} aria-hidden="true" />
      <aside className="admin-drawer" aria-label={form.isEdit ? 'แก้ไขบัญชี' : 'เพิ่มบัญชี'}>
        <form className="admin-drawer-form" onSubmit={form.submit} noValidate>
          <div>
            <h2>{form.isEdit ? `แก้ไข ${form.username}` : 'เพิ่มบัญชี'}</h2>
            <p className="admin-drawer-sub">
              {form.isEdit
                ? 'ชื่อผู้ใช้เปลี่ยนไม่ได้ สิทธิ์ใหม่มีผลกับเครื่องที่ login อยู่ภายใน 5 นาที'
                : 'แจ้งชื่อผู้ใช้และรหัสผ่านชั่วคราวให้เจ้าของบัญชีด้วยตัวเอง ระบบไม่ส่งให้'}
            </p>
          </div>

          <div className={`admin-field${form.errors.username ? ' is-invalid' : ''}`}>
            <label htmlFor="account-username">ชื่อผู้ใช้</label>
            <input
              id="account-username"
              value={form.username}
              onChange={(e) => form.setUsername(e.target.value)}
              disabled={form.isEdit}
              autoComplete="off"
              autoCapitalize="none"
              spellCheck={false}
              placeholder="เช่น mali.k"
            />
            {form.errors.username ? (
              <span className="admin-field-error">{form.errors.username}</span>
            ) : (
              <span className="admin-field-help">a-z, 0-9, จุด, ขีดล่าง หรือขีดกลาง ยาว 3–50 ตัว</span>
            )}
          </div>

          <div className={`admin-field${form.errors.fullName ? ' is-invalid' : ''}`}>
            <label htmlFor="account-full-name">ชื่อที่แสดง</label>
            <input
              id="account-full-name"
              value={form.fullName}
              onChange={(e) => form.setFullName(e.target.value)}
              autoComplete="off"
              placeholder="เช่น แม่บ้าน มาลี"
            />
            {form.errors.fullName ? (
              <span className="admin-field-error">{form.errors.fullName}</span>
            ) : (
              <span className="admin-field-help">ชื่อนี้ขึ้นบนการ์ด Dashboard ว่าใครสแกนล่าสุด</span>
            )}
          </div>

          {!form.isEdit && (
            <div className={`admin-field${form.errors.password ? ' is-invalid' : ''}`}>
              <label htmlFor="account-password">รหัสผ่านชั่วคราว</label>
              <input
                id="account-password"
                type="text"
                value={form.password}
                onChange={(e) => form.setPassword(e.target.value)}
                autoComplete="new-password"
                spellCheck={false}
              />
              {form.errors.password ? (
                <span className="admin-field-error">{form.errors.password}</span>
              ) : (
                <span className="admin-field-help">อย่างน้อย {MIN_PASSWORD_LENGTH} ตัวอักษร</span>
              )}
            </div>
          )}

          <div className={`admin-field${form.errors.role ? ' is-invalid' : ''}`}>
            <label htmlFor="account-role">สิทธิ์</label>
            <select
              id="account-role"
              value={form.role}
              onChange={(e) => form.setRole(e.target.value as UserRole)}
              disabled={isSelf}
            >
              <option value="cleaner">แม่บ้าน (cleaner) — สแกนและดู Dashboard</option>
              <option value="admin">ผู้ดูแลระบบ (admin) — จัดการจุดและบัญชีได้</option>
            </select>
            {form.errors.role ? (
              <span className="admin-field-error">{form.errors.role}</span>
            ) : isSelf ? (
              <span className="admin-field-help">ลดสิทธิ์บัญชีที่ตัวเองใช้อยู่ไม่ได้</span>
            ) : null}
          </div>

          {form.submitError && <div className="admin-error">{form.submitError}</div>}

          <div className="admin-drawer-footer">
            <button type="button" className="admin-btn admin-btn-ghost" onClick={onClose}>
              ยกเลิก
            </button>
            <button type="submit" className="admin-btn admin-btn-primary" disabled={form.isSaving}>
              {form.isSaving ? 'กำลังบันทึก...' : 'บันทึก'}
            </button>
          </div>
        </form>
      </aside>
    </>
  );
};
```

Create `frontend/src/features/admin/components/ResetPasswordDialog.tsx`:

```tsx
import React from 'react';
import type { AdminUser } from '../../../types';
import { useResetPassword } from '../hooks/useResetPassword';
import { MIN_PASSWORD_LENGTH } from '../logic/accountValidation';

interface ResetPasswordDialogProps {
  user: AdminUser;
  onClose: () => void;
  onDone: (user: AdminUser) => void;
}

export const ResetPasswordDialog: React.FC<ResetPasswordDialogProps> = ({ user, onClose, onDone }) => {
  const reset = useResetPassword(user, onDone);

  return (
    <div className="admin-dialog-backdrop">
      <form
        className="admin-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="reset-password-title"
        onSubmit={reset.submit}
        noValidate
      >
        <h2 id="reset-password-title">รีเซ็ตรหัสผ่านของ {user.fullName}</h2>

        <div className="admin-warning">
          <span aria-hidden="true">⚠️</span>
          <div>
            <b>ทุกเครื่องที่ {user.username} login อยู่จะต้อง login ใหม่</b> ด้วยรหัสผ่านชั่วคราวนี้
          </div>
        </div>

        <div className={`admin-field${reset.error ? ' is-invalid' : ''}`}>
          <label htmlFor="reset-password-input">รหัสผ่านชั่วคราวใหม่</label>
          <input
            id="reset-password-input"
            type="text"
            value={reset.password}
            onChange={(e) => reset.setPassword(e.target.value)}
            autoComplete="new-password"
            spellCheck={false}
          />
          {reset.error ? (
            <span className="admin-field-error">{reset.error}</span>
          ) : (
            <span className="admin-field-help">อย่างน้อย {MIN_PASSWORD_LENGTH} ตัวอักษร แจ้งให้เจ้าของบัญชีด้วยตัวเอง</span>
          )}
        </div>

        <div className="admin-dialog-footer">
          <button type="button" className="admin-btn admin-btn-ghost" onClick={onClose}>
            ยกเลิก
          </button>
          <button type="submit" className="admin-btn admin-btn-warning" disabled={reset.isSaving}>
            {reset.isSaving ? 'กำลังรีเซ็ต...' : 'รีเซ็ตรหัสผ่าน'}
          </button>
        </div>
      </form>
    </div>
  );
};
```

- [ ] **Step 5: Build, test and lint**

Run: `npm --prefix frontend run build`
Expected: PASS, `built in`.

Run: `npm --prefix frontend test`
Expected: PASS, `Tests  33 passed (33)`.

Run: `npm --prefix frontend run lint`
Expected: no line reports an error. A `react(set-state-in-effect)` warning on `useAccountsPage.ts` matches the pattern the phase 1 hooks already have.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/App.tsx frontend/src/features/admin
git commit -m "feat(accounts-web): add the accounts page with create, edit, reset and deactivate (#17)"
```

---

### Task 6: README note and the manual check on MySQL

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: Tasks 1-5
- Produces: a ticked checklist in this plan

- [ ] **Step 1: Point the README at the page**

In `README.md`, replace:

```markdown
- **ผู้ดูแลระบบ**: username: `admin` / password: `admin1234`
```

with:

```markdown
- **ผู้ดูแลระบบ**: username: `admin` / password: `admin1234`
- ผู้ดูแลระบบเพิ่มบัญชี แก้สิทธิ์ รีเซ็ตรหัสผ่าน และปิดใช้งานบัญชีได้ที่เมนู **จัดการระบบ** (`/admin/accounts`) ไม่มีการลบบัญชี
```

- [ ] **Step 2: Run every automated check**

```bash
dotnet test backend/FacilityRealtime.slnx --nologo
npm --prefix frontend test
npm --prefix frontend run build
npm --prefix frontend run lint
```

Expected: unit tests 69 and API tests 65 pass; Vitest `33 passed`; build succeeds; lint reports no errors.

- [ ] **Step 3: Walk the flow on a machine with MySQL**

Start the API and the frontend as the README describes. Tick a row only after seeing its result; if one fails, stop and debug with `debug-mantra`.

| # | Do | Expect |
|---|---|---|
| 1 | Log in as `somchai` on the computer | No `จัดการระบบ` button in the navbar |
| 2 | While logged in as `somchai`, open `/admin/accounts` | `หน้านี้สำหรับผู้ดูแลระบบเท่านั้น` |
| 3 | Log out, log in as `admin`, click `จัดการระบบ` | The accounts table lists `admin (คุณ)` and `somchai`; the `ปิดใช้งาน` button on your own row is disabled |
| 4 | `เพิ่มบัญชี` with username `Mali K`, empty name, password `123` | Red messages under username, display name and password; nothing is saved |
| 5 | `เพิ่มบัญชี` with `mali.k`, `แม่บ้าน มาลี`, `temp-pass1`, role แม่บ้าน | Row appears and flashes; toast says to hand over the temporary password |
| 6 | On a phone, log in as `mali.k` / `temp-pass1` and scan a point | Scan saves; the dashboard card shows `แม่บ้าน มาลี` |
| 7 | Edit `mali.k`: role ผู้ดูแลระบบ; wait 5 minutes; reload the phone | The phone's navbar now shows `จัดการระบบ` |
| 8 | Edit your own `admin` row | The role select is disabled with `ลดสิทธิ์บัญชีที่ตัวเองใช้อยู่ไม่ได้` |
| 9 | `รีเซ็ตรหัสผ่าน` on `mali.k` to `new-temp-99`; reload the phone after 5 minutes | Phone lands on the login page; `temp-pass1` fails, `new-temp-99` works |
| 10 | `ปิดใช้งาน` on `mali.k` | Row fades, badge `○ ปิดใช้งาน`; the dashboard card still shows `แม่บ้าน มาลี` as last cleaner |
| 11 | Log in as `mali.k` | `ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง` |
| 12 | `เปิดใช้งาน` on `mali.k`, log in again | Login works |
| 13 | `curl -i -X POST http://localhost:5001/api/admin/users -H "Authorization: Bearer <somchai access token from DevTools>" -H "Content-Type: application/json" -d '{}'` | `HTTP/1.1 403 Forbidden` |

- [ ] **Step 4: Commit**

```bash
git add README.md docs/superpowers/plans/2026-09-13-phase2b-accounts-page.md
git commit -m "docs(readme): point admins at the accounts page (#17)"
```

---

## Plan Complete

Plan 2B is done when all six tasks are ticked. Plan 2C (service points admin page, facility-0021) adds a `points` tab to `ADMIN_TABS` and reuses `admin.css`, `Toast`, `useToast` and `toApiError` from this plan.
