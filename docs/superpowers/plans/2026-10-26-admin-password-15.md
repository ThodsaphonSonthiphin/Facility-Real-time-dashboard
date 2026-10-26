# Admin Password of 15 Characters Implementation Plan (#101)

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An Admin's temporary password must be 15–100 characters and not on a list of common passwords. The rule is checked when an Admin account is created and when a password is reset, never at login.

**Architecture:**
- **Server, Application layer.** A new `CommonPasswords` reads an embedded text file once and answers "is this password on the list" ignoring letter case.
- **`AccountRules`.** `MinPassword` becomes 15. A new `PasswordProblem(password)` returns the Thai message to show, or null. Both endpoints in `AdminAccountEndpoints` (create, reset) call it instead of repeating the message.
- **Frontend.** It checks only the length (the list is not sent to the browser, facility-0062). The server's message about a common password reaches the form through the existing `getErrorMessage`.
- **Not changed:** login, `LoginThrottle`, the Development seed (`DbInitializer`) and the test helper `AuthApi.AdminLoginAsync` that logs in with the seed's password (facility-0089, Consequences).

**Tech Stack:**
- Backend: .NET 10 Minimal API, xUnit (SQLite in memory).
- Frontend: React 19.2, TypeScript 6.0, Vitest 5.0 on jsdom, Testing Library.
- **No new package.** The list is a data file, not a dependency.

**Spec:** `docs/adr/facility-0089-admin-password-15-characters-and-blocklist.md`, issue #101.
- Decision 1: Admin passwords are 15–100 characters. Spaces count. Only spaces is refused.
- Decision 2: a common-password list is kept in the system and compared ignoring letter case. No external lookup. The list is not sent to the browser.
- Decision 3: no composition rules, no periodic change.
- Decision 4: the rule is checked on create and on reset only. Existing shorter passwords keep working.

## Global Constraints

- **Branch:** `feat/admin-password-15`, from `master` at `64f0c93`. It already holds the ADR commit `c92e1e9`. Do not merge it.
- **Machine:** the Mac, zsh. Backend tests: `dotnet test backend/FacilityRealtime.slnx`. Frontend: `cd frontend && npm run lint && npx tsc -b && npx vitest run`. The pre-commit hook runs the frontend lint and test suite.
- **Never commit** the modified `.gitignore` or the untracked root `package-lock.json`.
- **Messages (exact):**
  - Length: `รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร` (the dash is U+2013, as today).
  - Common: `รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น`
- **The list file (exact):** `backend/src/FacilityRealtime.Application/Accounts/common-passwords.txt`, 10,898 lines, ASCII only, SHA-256 `cbdc28a68ea4bc20d214755fb333d816c55ceada6cc3da3bd9b70ecea42aead0`. No header line, so the hash stays the list's.
- **Source of the list:** SecLists (MIT License, Daniel Miessler), `Passwords/Common-Credentials/xato-net-10-million-passwords-1000000.txt` at commit `c205c36a445bff37f8e58a9ec829105cd4975c58`. The downloaded file's SHA-256 is `424a3e03a17df0a2bc2b3ca749d81b04e79d59cb7aeec8876a5a3f308d0caf51`.
- **Test passwords:** a password a test expects to be accepted is `temporary-pass-<digits>` (16+ characters, checked not on the list). A password expected to be refused as common is `1qaz2wsx3edc4rfv` (on the list).
- **Commit messages** end with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  ```

---

### Task 1: The common-password list

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Accounts/common-passwords.txt`
- Create: `backend/src/FacilityRealtime.Application/Accounts/CommonPasswords.cs`
- Modify: `backend/src/FacilityRealtime.Application/FacilityRealtime.Application.csproj`
- Test: `backend/tests/FacilityRealtime.UnitTests/CommonPasswordsTests.cs`

**Interfaces:**
- Produces: `internal static class CommonPasswords` in `FacilityRealtime.Application.Accounts`, with `public static int Count { get; }` and `public static bool Contains(string password)`. The class is internal; `InternalsVisibleTo` already covers `FacilityRealtime.UnitTests` and `FacilityRealtime.ApiTests`.

- [ ] **Step 1: Build the list file**

The download is untrusted data: put it in a new empty folder outside the repo and only read it with text tools.

```bash
D=$(mktemp -d)
curl -sSfL -o "$D/source.txt" "https://raw.githubusercontent.com/danielmiessler/SecLists/c205c36a445bff37f8e58a9ec829105cd4975c58/Passwords/Common-Credentials/xato-net-10-million-passwords-1000000.txt"
shasum -a 256 "$D/source.txt"
# expect 424a3e03a17df0a2bc2b3ca749d81b04e79d59cb7aeec8876a5a3f308d0caf51
LC_ALL=C tr -d '\r' < "$D/source.txt" \
  | LC_ALL=C awk 'length($0) >= 15 && length($0) <= 100' \
  | LC_ALL=C tr 'A-Z' 'a-z' \
  | LC_ALL=C sort -u > backend/src/FacilityRealtime.Application/Accounts/common-passwords.txt
wc -l backend/src/FacilityRealtime.Application/Accounts/common-passwords.txt
# expect 10898
shasum -a 256 backend/src/FacilityRealtime.Application/Accounts/common-passwords.txt
# expect cbdc28a68ea4bc20d214755fb333d816c55ceada6cc3da3bd9b70ecea42aead0
rm -rf "$D"
```

If either hash differs, stop and report it. Do not use a different list.

- [ ] **Step 2: Embed the file**

In `FacilityRealtime.Application.csproj`, add an item group after the `InternalsVisibleTo` one:

```xml
  <ItemGroup>
    <EmbeddedResource Include="Accounts\common-passwords.txt" LogicalName="FacilityRealtime.Application.Accounts.common-passwords.txt" />
  </ItemGroup>
```

- [ ] **Step 3: Write the failing test**

`backend/tests/FacilityRealtime.UnitTests/CommonPasswordsTests.cs`:

```csharp
using FacilityRealtime.Application.Accounts;

namespace FacilityRealtime.UnitTests;

public class CommonPasswordsTests
{
    [Fact]
    public void The_whole_list_is_read() => Assert.Equal(10898, CommonPasswords.Count);

    [Theory]
    [InlineData("1qaz2wsx3edc4rfv", true)]
    [InlineData("1QAZ2WSX3EDC4RFV", true)]
    [InlineData("123456789qwerty", true)]
    [InlineData("temporary-pass-1", false)]
    [InlineData("ห้องน้ำชั้นสามสะอาดเสมอ", false)]
    public void A_password_on_the_list_is_found_whatever_the_letter_case(string password, bool common) =>
        Assert.Equal(common, CommonPasswords.Contains(password));
}
```

- [ ] **Step 4: Run it to see it fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~CommonPasswordsTests"`
Expected: build error, `CommonPasswords` does not exist.

- [ ] **Step 5: Write `CommonPasswords`**

`backend/src/FacilityRealtime.Application/Accounts/CommonPasswords.cs`:

```csharp
namespace FacilityRealtime.Application.Accounts;

/// <summary>
/// facility-0089: passwords too common to allow, compared ignoring letter case. common-passwords.txt is every entry of
/// 15–100 characters in SecLists' xato-net-10-million-passwords-1000000.txt (MIT License, commit c205c36), lowercased,
/// sorted and without duplicates, one per line; shorter entries are already refused by the length rule. Read once, on
/// the first check. It stays on the server: the browser checks only the length (facility-0062).
/// </summary>
internal static class CommonPasswords
{
    private const string ResourceName = "FacilityRealtime.Application.Accounts.common-passwords.txt";

    private static readonly Lazy<HashSet<string>> List = new(Read);

    public static int Count => List.Value.Count;

    public static bool Contains(string password) => List.Value.Contains(password.ToLowerInvariant());

    private static HashSet<string> Read()
    {
        using var stream = typeof(CommonPasswords).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"{ResourceName} is not embedded in the Application assembly");
        using var reader = new StreamReader(stream);
        var list = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                list.Add(line);
            }
        }

        return list;
    }
}
```

- [ ] **Step 6: Run it to see it pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~CommonPasswordsTests"`
Expected: 6 passed.

- [ ] **Step 7: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Accounts/common-passwords.txt \
  backend/src/FacilityRealtime.Application/Accounts/CommonPasswords.cs \
  backend/src/FacilityRealtime.Application/FacilityRealtime.Application.csproj \
  backend/tests/FacilityRealtime.UnitTests/CommonPasswordsTests.cs
git commit -m "feat(accounts): a list of common passwords of 15+ characters, embedded (facility-0089)"
```

---

### Task 2: The rule on create and on reset

**Files:**
- Modify: `backend/src/FacilityRealtime.Application/Accounts/AccountRules.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminAccountEndpoints.cs:75-78` (create) and `:308-311` (reset)
- Test: `backend/tests/FacilityRealtime.UnitTests/AccountRulesTests.cs:55-66`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AccountEndpointTests.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AccountSecretEndpointTests.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/DeactivatedAdminTests.cs:27`

**Interfaces:**
- Consumes: `CommonPasswords.Contains(string)` from Task 1.
- Produces, in `AccountRules`:
  - `public const int MinPassword = 15;` (`MaxPassword` stays 100)
  - `public static readonly string PasswordLengthProblem` = `รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร`
  - `public const string CommonPasswordProblem = "รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น";`
  - `public static string? PasswordProblem(string? password)`: the length message, else the common message, else null.
  - `IsAllowedPassword(string?)` stays and still checks only the length and "not only spaces".

- [ ] **Step 1: Write the failing unit tests**

In `AccountRulesTests.cs`, replace the theory `A_temporary_password_is_8_to_100_characters` (lines 55–62) with:

```csharp
    [Theory]
    [InlineData("temporary-pass-1", true)]
    [InlineData("temp pass phrase", true)]
    [InlineData("123456789012345", true)]
    [InlineData("12345678901234", false)]
    [InlineData("               ", false)]
    [InlineData(null, false)]
    public void A_temporary_password_is_15_to_100_characters(string? password, bool allowed) =>
        Assert.Equal(allowed, AccountRules.IsAllowedPassword(password));
```

Keep `A_password_longer_than_100_characters_is_refused`. Add after it:

```csharp
    [Theory]
    [InlineData("short", "รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร")]
    [InlineData(null, "รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร")]
    [InlineData("1QAZ2WSX3EDC4RFV", "รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น")]
    [InlineData("temporary-pass-1", null)]
    public void A_password_problem_says_the_length_first_then_whether_it_is_common(string? password, string? problem) =>
        Assert.Equal(problem, AccountRules.PasswordProblem(password));
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AccountRulesTests"`
Expected: build error, `PasswordProblem` does not exist.

- [ ] **Step 3: Change `AccountRules`**

Replace `public const int MinPassword = 8;` and the `IsAllowedPassword` member with:

```csharp
    /// <summary>facility-0089: 15 for a login by password alone (NIST SP 800-63B-4, 3.1.1.2).</summary>
    public const int MinPassword = 15;
    public const int MaxPassword = 100;

    public static readonly string PasswordLengthProblem = $"รหัสผ่านชั่วคราวต้องยาว {MinPassword}–{MaxPassword} ตัวอักษร";
    public const string CommonPasswordProblem = "รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น";
```

(delete the old `public const int MaxPassword = 100;` line so it is declared once), and:

```csharp
    /// <summary>The temporary password is kept exactly as typed, so spaces count, but it cannot be only spaces.</summary>
    public static bool IsAllowedPassword(string? password) =>
        password is { Length: >= MinPassword and <= MaxPassword } && !string.IsNullOrWhiteSpace(password);

    /// <summary>
    /// facility-0089: what is wrong with a new Admin password, or null. Checked when an Admin is created and when a
    /// password is reset, never at login, so a shorter password set before keeps working until it is reset.
    /// </summary>
    public static string? PasswordProblem(string? password) =>
        !IsAllowedPassword(password) ? PasswordLengthProblem
        : CommonPasswords.Contains(password!) ? CommonPasswordProblem
        : null;
```

- [ ] **Step 4: Run the unit tests to see them pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AccountRulesTests"`
Expected: all pass.

- [ ] **Step 5: Update the API tests' passwords and add the failing ones**

Accepted passwords in the API tests become `temporary-pass-<digits>`:

```bash
sed -i '' 's/"temporary-\([0-9][0-9]*\)"/"temporary-pass-\1"/g' \
  backend/tests/FacilityRealtime.ApiTests/Admin/AccountEndpointTests.cs \
  backend/tests/FacilityRealtime.ApiTests/Admin/AccountSecretEndpointTests.cs \
  backend/tests/FacilityRealtime.ApiTests/Admin/DeactivatedAdminTests.cs
grep -rn '"temporary-' backend/tests
```

Expected after the `grep`: only `temporary-pass-…` values.

In `AccountEndpointTests.BadRequests`, change the Admin rows:
- `"password": "1234567"` → `"password": "12345678901234"` (14 characters: too short).
- In the two rows that send an extra `phone` or `shift`, `"password": "12345678"` → `"password": "temporary-pass-1"`, so they are refused for the extra field and not for the length.
- Leave the Cleaner row with `"password": "12345678"` as it is.

Add to `AccountEndpointTests` (after `A_used_username_is_refused_whatever_the_letter_case`):

```csharp
    [Fact]
    public async Task A_common_password_is_refused_with_its_reason_and_nothing_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateAdminAsync(admin, "admin2", "1qaz2wsx3edc4rfv");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น", await AdminSetupApi.MessageAsync(response));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }
```

In `AccountSecretEndpointTests`, replace `A_too_short_password_is_refused` with:

```csharp
    [Theory]
    [InlineData("temporary-1234", "รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร")]
    [InlineData("1qaz2wsx3edc4rfv", "รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น")]
    public async Task A_short_or_common_password_is_refused_with_its_reason(string password, string message)
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await PasswordAsync(admin, adminAuth.User.Id, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, await AdminSetupApi.MessageAsync(response));
    }
```

- [ ] **Step 6: Run the API tests to see the new ones fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AccountEndpointTests|FullyQualifiedName~AccountSecretEndpointTests|FullyQualifiedName~DeactivatedAdminTests"`
Expected: `A_common_password_is_refused_with_its_reason_and_nothing_is_logged` fails with 201, and the `1qaz2wsx3edc4rfv` case of `A_short_or_common_password_is_refused_with_its_reason` fails with 204. The short case already passes: the endpoint builds its message from `MinPassword`, which Step 3 made 15. Every other test passes.

- [ ] **Step 7: Use `PasswordProblem` in both endpoints**

In `AdminAccountEndpoints.cs`, create (around line 75):

```csharp
            if (AccountRules.PasswordProblem(request.Password) is { } passwordProblem)
            {
                return Bad(passwordProblem);
            }
```

replacing:

```csharp
            if (!AccountRules.IsAllowedPassword(request.Password))
            {
                return Bad($"รหัสผ่านชั่วคราวต้องยาว {AccountRules.MinPassword}–{AccountRules.MaxPassword} ตัวอักษร");
            }
```

Reset (around line 308): the same replacement, with the same three lines.

- [ ] **Step 8: Run the backend suite**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: all pass (MySQL-only tests skipped as before).

- [ ] **Step 9: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Accounts/AccountRules.cs \
  backend/src/FacilityRealtime.Api/Endpoints/AdminAccountEndpoints.cs \
  backend/tests/FacilityRealtime.UnitTests/AccountRulesTests.cs \
  backend/tests/FacilityRealtime.ApiTests/Admin/AccountEndpointTests.cs \
  backend/tests/FacilityRealtime.ApiTests/Admin/AccountSecretEndpointTests.cs \
  backend/tests/FacilityRealtime.ApiTests/Admin/DeactivatedAdminTests.cs
git commit -m "feat(accounts): an Admin password is 15–100 characters and not a common one (facility-0089)"
```

---

### Task 3: The accounts page says 15–100

**Files:**
- Modify: `frontend/src/pages/accounts/accountRules.ts:120-122`
- Modify: `frontend/src/pages/accounts/CreateAccountForm.tsx:137`
- Modify: `frontend/src/pages/accounts/SecretForms.tsx:129`
- Test: `frontend/src/pages/accounts/AccountsPage.test.tsx:179-188`
- Test: `frontend/src/pages/accounts/AccountActions.test.tsx:138-149`

**Interfaces:**
- Consumes: the server's messages from Task 2. They are shown as they come, so the frontend needs no copy of the common-password message.
- Produces: `MIN_PASSWORD = 15`, `PASSWORD_PROBLEM = 'รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร'`.

- [ ] **Step 1: Write the failing test**

In `AccountsPage.test.tsx`, the Admin create test:
- Type `'temporary-1234'` (14 characters, accepted by the old rule) instead of `'short'`.
- Expect `'รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร'` in the alert.
- Type `'temporary-pass-123'` instead of `'temporary-123'`, and expect `password: 'temporary-pass-123'` in `created(server)`.

```tsx
    await user.type(within(form).getByLabelText('รหัสผ่านชั่วคราว'), 'temporary-1234');
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));
    expect(within(form).getByRole('alert')).toHaveTextContent('รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร');

    await user.clear(within(form).getByLabelText('รหัสผ่านชั่วคราว'));
    await user.type(within(form).getByLabelText('รหัสผ่านชั่วคราว'), 'temporary-pass-123');
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี ผู้ดูแลคนที่สาม แล้ว');
    expect(created(server)).toEqual({ role: 'Admin', displayName: 'ผู้ดูแลคนที่สาม', username: 'admin3', password: 'temporary-pass-123' });
```

In `AccountActions.test.tsx`, the reset test: `'temporary-456'` → `'temporary-pass-456'` (typed and in the `sent(…)` expectation), `'temporary-789'` → `'temporary-pass-789'`.

- [ ] **Step 2: Run them to see them fail**

Run: `cd frontend && npx vitest run src/pages/accounts`
Expected: the Admin create test fails (no alert for a 14-character password). The reset test passes already; it only keeps its passwords valid for the next step.

- [ ] **Step 3: Change the rule and the words**

`accountRules.ts`:

```ts
export const MIN_PASSWORD = 15;
export const MAX_PASSWORD = 100;
export const PASSWORD_PROBLEM = 'รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร';
```

`CreateAccountForm.tsx` line 137:

```tsx
          <p className="text-muted">แอดมิน: ชื่อผู้ใช้ + รหัสผ่านชั่วคราว 15–100 ตัวอักษร เป็นวลียาว ๆ ได้ (บอกเองด้วยวาจาหรือกระดาษ)</p>
```

`SecretForms.tsx` line 129:

```tsx
        help="15–100 ตัวอักษร เป็นวลียาว ๆ ได้ บอกเองด้วยวาจาหรือกระดาษ · ถ้าใส่รหัสเดิม ระบบไม่เปลี่ยนอะไร"
```

- [ ] **Step 4: Run them to see them pass, then the checks**

Run: `cd frontend && npx vitest run src/pages/accounts && npm run lint && npx tsc -b`
Expected: all pass, lint and tsc clean.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/accounts/accountRules.ts frontend/src/pages/accounts/CreateAccountForm.tsx \
  frontend/src/pages/accounts/SecretForms.tsx frontend/src/pages/accounts/AccountsPage.test.tsx \
  frontend/src/pages/accounts/AccountActions.test.tsx
git commit -m "feat(accounts): the accounts page asks for a 15–100 character Admin password (facility-0089)"
```

---

### Task 4: Docs and the final check

**Files:**
- Modify: `docs/adr/facility-0020-accounts-page-operations.md` (an amendment line at the end)
- Modify: `docs/handoff.md` (`Last updated`, the Admin password bullet under `## Pending work`, a task-log line)

- [ ] **Step 1: Amend facility-0020**

Append at the end of `facility-0020-accounts-page-operations.md`:

```markdown

> **แก้ไข 2026-10-26:** รหัสผ่านชั่วคราวของแอดมินยาว 15–100 ตัว และห้ามเป็นรหัสที่อยู่ในรายการรหัสยอดฮิต ตรวจตอนสร้างบัญชีและตอนรีเซ็ตเท่านั้น (facility-0089)
```

- [ ] **Step 2: The full checks**

```bash
dotnet test backend/FacilityRealtime.slnx
cd frontend && npm run lint && npx tsc -b && npx vitest run && npm run build
```

Expected: everything passes. Note the startup JS gzip size from the build output. It should be within a few bytes of 162.96 kB (only text changed), and well under 200 kB (facility-0062).

- [ ] **Step 3: Update the handoff**

In `docs/handoff.md`:
- `Last updated`: #101 done on `feat/admin-password-15`, not pushed.
- Replace the "Admin password of 15 characters…" bullet under `## Pending work` with a "done on the branch, not merged" line. Keep the accepted lockout risk in it.
- Add a task-log line with:
  - the backend and frontend test counts and the JS size
  - what the owner should check: on D6, creating an Admin with a 14-character password is refused, with a common one such as `1qaz2wsx3edc4rfv` too, and a reset is refused the same way

- [ ] **Step 4: Commit**

```bash
git add docs/adr/facility-0020-accounts-page-operations.md docs/handoff.md
git commit -m "docs: facility-0020 amended for the 15-character Admin password; handoff (#101)"
```
