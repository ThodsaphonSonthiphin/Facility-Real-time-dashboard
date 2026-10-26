# Buildings Managed by Admin Implementation Plan (facility-0090)

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Admin adds, renames and turns buildings on and off on a new page "ตึก" (`/admin/buildings`). A building is turned off only when nothing active is left in it, and an Area or a Supervisor account in a building that is off cannot be turned on. The D7 and D8 building pickers also list buildings that are off.

**Architecture:**
- **Server, Application layer.** A new `BuildingRules` holds:
  - the code rule (A–Z/0–9, 1–20 characters)
  - the name rule (1–100 characters after trimming)
  - every Thai message the building rules give
- **Server, Api layer.** A new `AdminBuildingEndpoints` serves:
  - the full list (`GET /api/admin/buildings/all`, off buildings and counts included)
  - add (`POST /api/admin/buildings`)
  - rename (`PUT /api/admin/buildings/{id}`)
  - turn off and on (`POST …/deactivate`, `POST …/activate`)
  - Every change is written with `AuditTrail.Add` as `BUILDING_*`.
  - The short list `GET /api/admin/buildings` (active buildings only, in `AdminAreaEndpoints`) stays as it is for the forms and the live pages.
- **Guards on the way back.**
  - `AdminAreaEndpoints.SetActiveAsync` refuses to turn on an Area whose building is off.
  - `AdminAccountEndpoints.SetActiveAsync` refuses to turn on a Supervisor whose building is off.
- **Frontend.**
  - A new lazy page folder `pages/buildings/` holds one table and a Drawer for add and edit.
  - `BuildingSelect` (D7, D8) reads the full list and marks a building that is off with "(ปิดใช้งาน)".
  - D9 learns the four new action names.

**Tech Stack:**
- Backend: .NET 10 Minimal API, EF Core, xUnit API tests on `FacilityApiFactory` (seeded by `DbInitializer`).
- Frontend: React 19.2, TypeScript 6.0, RTK Query, Tailwind 4, Vitest 5.0 on jsdom with Testing Library, and `fakeServer` in place of fetch.
- **No new package.**

**Spec:** `docs/adr/facility-0090-admin-manages-buildings.md` (approved by the owner 2026-10-26).
- Decision 1: a new Admin menu "ตึก" (`/admin/buildings`) beside "จัดการพื้นที่และป้าย", holding one table with the columns รหัส · ชื่อ · พื้นที่ที่เปิดอยู่ · สถานะ · แก้ไข. "+ เพิ่มตึก" sits at the table's top right. Add and edit open in the right-hand Drawer.
- Decision 2: the code is trimmed and upper-cased, A–Z or 0–9, 1–20 characters. It must be unique among all buildings, off ones included, and is set once.
- Decision 3: the name can change, must not be empty, is at most 100 characters, and may repeat another building's name.
- Decision 4:
  - A building is never deleted, only turned off. It can be turned off only with no active Area and no active Supervisor account assigned to it. The refusal says how many of each are left.
  - Turning a building back on has no condition and does not turn its Areas on.
  - While a building is off, an Area in it and a Supervisor account assigned to it cannot be turned on.
- Decision 5: the D7 and D8 building pickers list off buildings marked "(ปิดใช้งาน)". D1, D2, the Area form, the account form and cover assignment list only active buildings.
- Decision 6: add, rename and on/off go to the Admin history (`BUILDING_CREATE`, `BUILDING_UPDATE`, `BUILDING_ACTIVATE`, `BUILDING_DEACTIVATE`) with before and after values. A save that changes nothing writes nothing.

## Global Constraints

- **Branch:** `feat/admin-buildings`, from `master` at `3a6323b`.
  - Before Task 1 the controller commits the ADR, this plan and the handoff edits as one commit: `docs: ADR facility-0090 buildings managed by Admin, and its plan`.
  - Do not push or merge.
- **Machine:** the Mac, zsh.
  - Backend tests: `dotnet test backend/FacilityRealtime.slnx`. One class: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~<ClassName>"`.
  - Frontend: `cd frontend && npm run lint && npx tsc -b && npx vitest run`. One file: `cd frontend && npx vitest run <path>`.
  - The pre-commit hook runs the frontend lint and the test suite.
- **Never commit** the modified `.gitignore` or the untracked root `package-lock.json`.
- **This repo is public:** test data uses made-up names only.
- **Server messages (exact, the dash is U+2013):**
  - Code: `รหัสตึกต้องเป็นตัวอักษรอังกฤษหรือตัวเลข 1–20 ตัว เช่น B2`
  - Name: `ต้องใส่ชื่อตึก ไม่เกิน 100 ตัวอักษร`
  - Code taken: `รหัสตึกนี้มีแล้ว`
  - Not found: `ไม่พบตึกนี้`
  - Turn-off refused: `ปิดตึก {code} ไม่ได้ ยังมี{left} ปิดหรือย้ายออกก่อน`, where `{left}` is `พื้นที่ที่เปิดอยู่ {n} แห่ง`, `หัวหน้าประจำตึก {m} คน`, or both joined by ` และ `.
  - Area in an off building: `ตึก {code} ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายพื้นที่ไปตึกอื่น`
  - Supervisor in an off building: `ตึก {code} ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายหัวหน้าไปตึกอื่น`
- **Audit (exact):**
  - Entity type: `buildings`.
  - Summaries: `เพิ่มตึก {code} {name}`, `แก้ตึก {code} {name}` (the new name), `เปิดใช้งานตึก {code}`, `ปิดใช้งานตึก {code}`.
  - Snapshots: `{ Code, Name }` for add and rename, `{ IsActive }` for on/off.
- **Page words (exact):**
  - Menu and `h1`: `ตึก`. List heading `h2`: `ตึกทั้งหมด ({n})`. Button: `+ เพิ่มตึก`.
  - Drawer titles: `เพิ่มตึก`, `แก้ไขตึก · {code} {name}`.
  - Fields: `รหัสตึก`, with the help line `ตัวอักษรอังกฤษหรือตัวเลข 1–20 ตัว เช่น B2 · แก้ทีหลังไม่ได้`, and `ชื่อตึก`. On edit: `รหัสตึก {code} (แก้ไม่ได้)`.
  - Checks: `ใส่รหัสตึกก่อน`, `ใส่ชื่อตึกก่อน`.
  - Toasts: `เพิ่มตึก {code} {name} แล้ว`, `บันทึกตึก {code} {name} แล้ว`, `ปิดใช้งานตึก {code} แล้ว`, `เปิดใช้งานตึก {code} แล้ว`.
  - Picker suffix: ` (ปิดใช้งาน)`.
- **Frontend standards** (`docs/agents/frontend-standards.md`):
  - Tailwind tokens only. No new CSS file and no raw palette class.
  - Admin targets at least 36 px (`size="desk"`). The status is shown in words.
  - Tests find by role, label and text.
- **Commit messages** end with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  ```

## Rulings made while planning (report them to the owner in the PR)

1. **The full list is `GET /api/admin/buildings/all`.** The ADR left the path to the plan. One list feeds both the page and the D7/D8 picker.
2. **The turn-off button is disabled while something is still inside**, and the Drawer says what is left. The server checks anyway (the frontend standard asks for a shown disabled state, and this avoids a surprise refusal).
3. **D9's "ประเภท" option `พื้นที่ · จุด · รอบ · ป้าย` becomes `ตึก · พื้นที่ · จุด · รอบ · ป้าย`.** The four `BUILDING_*` actions join the Setup category instead of a new one.
4. **The last column is headed `จัดการ` and holds the `แก้ไข` button**, as on D6. The ADR lists that column as "แก้ไข".
5. **A building that is off can still be renamed.** The ADR does not forbid it, and the history keeps the old name.
6. **Not guarded: an Area turned on at the same moment its building is turned off.** One Admin works at a time in the pilot. The cost if wrong is an active Area in an off building, which the Admin can turn off by hand.

---

### Task 1: Building rules and the buildings endpoints

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Buildings/BuildingRules.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminBuildingEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs` (after `BuildingDto`, line 5)
- Modify: `backend/src/FacilityRealtime.Api/Program.cs:107` (map the new endpoints)
- Modify: `backend/src/FacilityRealtime.Application/Audit/AuditCategories.cs` (Setup)
- Test: `backend/tests/FacilityRealtime.UnitTests/BuildingRulesTests.cs` (create)
- Test: `backend/tests/FacilityRealtime.UnitTests/AuditCategoriesTests.cs` (four rows)
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/BuildingAdminEndpointTests.cs` (create)
- Modify: `backend/tests/FacilityRealtime.ApiTests/Admin/AdminSetupModels.cs` (one record)

**Interfaces:**
- Produces: `public static partial class BuildingRules` in `FacilityRealtime.Application.Buildings`, with these members:
  - `MaxName` (100), `CodeProblem`, `NameProblem`, `CodeTaken`
  - `string? NormalizeCode(string?)`, `string? NormalizeName(string?)`
  - `string? DeactivateProblem(string code, int activeAreas, int activeSupervisors)`
  - `string OffForArea(string code)`, `string OffForSupervisor(string code)`
  
  Task 2 uses the last two.
- Produces: `ManagedBuildingDto(int Id, string Code, string Name, bool IsActive, int ActiveAreaCount, int ActiveSupervisorCount)` in JSON camelCase. Tasks 3 and 4 read it as `ManagedBuilding`.
- Produces routes, all Admin-only:
  - `GET /api/admin/buildings/all` returns 200 `ManagedBuildingDto[]` ordered by code.
  - `POST /api/admin/buildings` takes `{ code, name }` and returns 201 `ManagedBuildingDto`.
  - `PUT /api/admin/buildings/{id}` takes `{ name }` and returns 200 `ManagedBuildingDto`.
  - `POST /api/admin/buildings/{id}/deactivate` and `/activate` return 200 `ManagedBuildingDto`.
  - Errors are `{ message }` with 400, 404 or 409.

- [ ] **Step 1: Write the failing unit tests**

Create `backend/tests/FacilityRealtime.UnitTests/BuildingRulesTests.cs`:

```csharp
using FacilityRealtime.Application.Buildings;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0090: what the Admin may type on the buildings page, and the answers when a building is in the way.</summary>
public class BuildingRulesTests
{
    [Theory]
    [InlineData(" b2 ", "B2")]
    [InlineData("A", "A")]
    [InlineData("ABCDEFGHIJ0123456789", "ABCDEFGHIJ0123456789")]
    public void Codes_are_trimmed_and_upper_cased(string typed, string expected) =>
        Assert.Equal(expected, BuildingRules.NormalizeCode(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A-1")]
    [InlineData("BPI_B2")]
    [InlineData("อาคาร1")]
    [InlineData("ABCDEFGHIJ01234567890")]
    public void Other_codes_are_refused(string? typed) => Assert.Null(BuildingRules.NormalizeCode(typed));

    [Fact]
    public void Names_are_trimmed_and_100_characters_at_most()
    {
        Assert.Equal("ตึกผลิต", BuildingRules.NormalizeName("  ตึกผลิต "));
        Assert.Equal(new string('ก', 100), BuildingRules.NormalizeName(new string('ก', 100)));
        Assert.Null(BuildingRules.NormalizeName(new string('ก', 101)));
        Assert.Null(BuildingRules.NormalizeName("   "));
        Assert.Null(BuildingRules.NormalizeName(null));
    }

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(2, 0, "ปิดตึก A ไม่ได้ ยังมีพื้นที่ที่เปิดอยู่ 2 แห่ง ปิดหรือย้ายออกก่อน")]
    [InlineData(0, 1, "ปิดตึก A ไม่ได้ ยังมีหัวหน้าประจำตึก 1 คน ปิดหรือย้ายออกก่อน")]
    [InlineData(2, 1, "ปิดตึก A ไม่ได้ ยังมีพื้นที่ที่เปิดอยู่ 2 แห่ง และหัวหน้าประจำตึก 1 คน ปิดหรือย้ายออกก่อน")]
    public void A_building_turns_off_only_when_nothing_active_is_left(int areas, int supervisors, string? expected) =>
        Assert.Equal(expected, BuildingRules.DeactivateProblem("A", areas, supervisors));

    [Fact]
    public void A_building_that_is_off_names_itself_and_the_way_out()
    {
        Assert.Equal("ตึก B ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายพื้นที่ไปตึกอื่น", BuildingRules.OffForArea("B"));
        Assert.Equal("ตึก B ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายหัวหน้าไปตึกอื่น", BuildingRules.OffForSupervisor("B"));
    }
}
```

In `backend/tests/FacilityRealtime.UnitTests/AuditCategoriesTests.cs`, add these rows above `[InlineData("POINT_CREATE", AuditCategory.Setup)]`:

```csharp
    [InlineData("BUILDING_CREATE", AuditCategory.Setup)]
    [InlineData("BUILDING_UPDATE", AuditCategory.Setup)]
    [InlineData("BUILDING_ACTIVATE", AuditCategory.Setup)]
    [InlineData("BUILDING_DEACTIVATE", AuditCategory.Setup)]
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~BuildingRulesTests|FullyQualifiedName~AuditCategoriesTests"`
Expected: build error, because `FacilityRealtime.Application.Buildings` does not exist yet. Once Step 3 adds it, the four `BUILDING_*` rows still fail until Step 4.

- [ ] **Step 3: Write `BuildingRules`**

Create `backend/src/FacilityRealtime.Application/Buildings/BuildingRules.cs`:

```csharp
using System.Text.RegularExpressions;

namespace FacilityRealtime.Application.Buildings;

/// <summary>What the Admin may type on the buildings page, and the answers when a building is in the way (facility-0090).</summary>
public static partial class BuildingRules
{
    /// <summary>buildings.name is 100 wide.</summary>
    public const int MaxName = 100;

    public const string CodeProblem = "รหัสตึกต้องเป็นตัวอักษรอังกฤษหรือตัวเลข 1–20 ตัว เช่น B2";
    public const string NameProblem = "ต้องใส่ชื่อตึก ไม่เกิน 100 ตัวอักษร";
    public const string CodeTaken = "รหัสตึกนี้มีแล้ว";

    /// <summary>Decision 2: trimmed and upper-cased; null unless it is 1–20 letters A–Z or digits. It is set once.</summary>
    public static string? NormalizeCode(string? typed)
    {
        var normalized = typed?.Trim().ToUpperInvariant();
        return normalized is not null && CodePattern().IsMatch(normalized) ? normalized : null;
    }

    /// <summary>Decision 3: trimmed; null when empty or longer than <see cref="MaxName"/>. Another building may share it.</summary>
    public static string? NormalizeName(string? typed)
    {
        var trimmed = typed?.Trim() ?? string.Empty;
        return trimmed.Length is 0 or > MaxName ? null : trimmed;
    }

    /// <summary>Decision 4: null when the building may be turned off, else what is still active in it.</summary>
    public static string? DeactivateProblem(string code, int activeAreas, int activeSupervisors)
    {
        var left = new List<string>();
        if (activeAreas > 0)
        {
            left.Add($"พื้นที่ที่เปิดอยู่ {activeAreas} แห่ง");
        }

        if (activeSupervisors > 0)
        {
            left.Add($"หัวหน้าประจำตึก {activeSupervisors} คน");
        }

        return left.Count == 0 ? null : $"ปิดตึก {code} ไม่ได้ ยังมี{string.Join(" และ ", left)} ปิดหรือย้ายออกก่อน";
    }

    /// <summary>Decision 4: an Area of a building that is off stays off.</summary>
    public static string OffForArea(string code) => $"ตึก {code} ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายพื้นที่ไปตึกอื่น";

    /// <summary>Decision 4: a Supervisor of a building that is off stays off.</summary>
    public static string OffForSupervisor(string code) => $"ตึก {code} ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายหัวหน้าไปตึกอื่น";

    [GeneratedRegex("^[A-Z0-9]{1,20}$")]
    private static partial Regex CodePattern();
}
```

- [ ] **Step 4: Put the four actions in Setup**

In `backend/src/FacilityRealtime.Application/Audit/AuditCategories.cs`, the Setup list becomes:

```csharp
        [AuditCategory.Setup] =
        [
            "BUILDING_CREATE", "BUILDING_UPDATE", "BUILDING_ACTIVATE", "BUILDING_DEACTIVATE",
            "AREA_CREATE", "AREA_UPDATE", "AREA_ACTIVATE", "AREA_DEACTIVATE",
            "POINT_CREATE", "POINT_UPDATE", "POINT_ACTIVATE", "POINT_DEACTIVATE",
            "SIGN_LOCATION", "SIGN_RADIUS",
        ],
```

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~BuildingRulesTests|FullyQualifiedName~AuditCategoriesTests"`
Expected: PASS.

- [ ] **Step 5: Write the failing API tests**

Add to `backend/tests/FacilityRealtime.ApiTests/Admin/AdminSetupModels.cs`, after `BuildingModel`:

```csharp
public record ManagedBuildingModel(int Id, string Code, string Name, bool IsActive, int ActiveAreaCount, int ActiveSupervisorCount);
```

Create `backend/tests/FacilityRealtime.ApiTests/Admin/BuildingAdminEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// facility-0090: the buildings page. Seed: building A ("ตึก A") with the active Areas AR01 and AR02 and the active
/// Supervisor S2001 (day). <c>TestData.AddBuildingAsync</c> adds an empty building named "ตึก {code}".
/// </summary>
public class BuildingAdminEndpointTests
{
    private const string Buildings = "/api/admin/buildings";

    [Fact]
    public async Task The_full_list_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().GetAsync($"{Buildings}/all");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/all")]
    [InlineData("POST", "")]
    [InlineData("PUT", "/1")]
    [InlineData("POST", "/1/deactivate")]
    [InlineData("POST", "/1/activate")]
    public async Task Cleaners_cannot_manage_buildings(string method, string suffix)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var body = method == "GET" ? null : JsonContent.Create(new { code = "B", name = "ตึก B" });

        var response = await cleaner.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"{Buildings}{suffix}") { Content = body });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_full_list_has_off_buildings_and_counts_what_is_active_in_each()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var b = await TestData.AddBuildingAsync(factory, "B");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"{Buildings}/{b}/deactivate", null)).StatusCode);

        var all = (await admin.GetFromJsonAsync<List<ManagedBuildingModel>>($"{Buildings}/all"))!;
        var active = (await admin.GetFromJsonAsync<List<BuildingModel>>(Buildings))!;

        Assert.Equal(
            new[] { ("A", "ตึก A", true, 2, 1), ("B", "ตึก B", false, 0, 0) },
            all.Select(x => (x.Code, x.Name, x.IsActive, x.ActiveAreaCount, x.ActiveSupervisorCount)));
        // the forms' short list stays the active buildings only
        Assert.Equal(new[] { "A" }, active.Select(x => x.Code));
    }

    [Fact]
    public async Task An_admin_adds_a_building_and_the_history_keeps_it()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Buildings, new { code = " b2 ", name = "  ตึกผลิต " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ManagedBuildingModel>())!;
        Assert.Equal(("B2", "ตึกผลิต", true, 0, 0), (created.Code, created.Name, created.IsActive, created.ActiveAreaCount, created.ActiveSupervisorCount));
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("BUILDING_CREATE", "buildings", (long?)created.Id, "เพิ่มตึก B2 ตึกผลิต"), (entry.Action, entry.EntityType, entry.EntityId, entry.Summary));
        Assert.Null(entry.BeforeJson);
        // the log's JSON escapes Thai, so it is read back, not compared as text
        var after = JsonDocument.Parse(entry.AfterJson!).RootElement;
        Assert.Equal(("B2", "ตึกผลิต"), (after.GetProperty("Code").GetString(), after.GetProperty("Name").GetString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A-1")]
    [InlineData("อาคาร1")]
    [InlineData("ABCDEFGHIJ01234567890")]
    public async Task A_code_that_breaks_the_rule_is_refused(string code)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Buildings, new { code, name = "ตึกผลิต" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("รหัสตึกต้องเป็นตัวอักษรอังกฤษหรือตัวเลข 1–20 ตัว เช่น B2", await AdminSetupApi.MessageAsync(response));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task A_building_needs_a_name(string? name)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Buildings, new { code = "B", name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ต้องใส่ชื่อตึก ไม่เกิน 100 ตัวอักษร", await AdminSetupApi.MessageAsync(response));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    public async Task A_code_is_never_used_twice_even_by_an_off_building(string code)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var b = await TestData.AddBuildingAsync(factory, "B");
        await admin.PostAsync($"{Buildings}/{b}/deactivate", null);

        var response = await admin.PostAsJsonAsync(Buildings, new { code, name = "ตึกใหม่" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("รหัสตึกนี้มีแล้ว", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task Two_buildings_may_share_a_name()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Buildings, new { code = "B", name = "ตึก A" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_rename_changes_the_name_only_and_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var a = await AdminSetupApi.BuildingIdAsync(factory);

        var response = await admin.PutAsJsonAsync($"{Buildings}/{a}", new { code = "Z", name = " อาคารผลิต 1 " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ManagedBuildingModel>())!;
        Assert.Equal(("A", "อาคารผลิต 1"), (saved.Code, saved.Name));
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("BUILDING_UPDATE", "แก้ตึก A อาคารผลิต 1"), (entry.Action, entry.Summary));
        Assert.Equal("ตึก A", JsonDocument.Parse(entry.BeforeJson!).RootElement.GetProperty("Name").GetString());
        Assert.Equal("อาคารผลิต 1", JsonDocument.Parse(entry.AfterJson!).RootElement.GetProperty("Name").GetString());
    }

    [Fact]
    public async Task A_save_that_changes_nothing_writes_no_history()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var a = await AdminSetupApi.BuildingIdAsync(factory);

        var response = await admin.PutAsJsonAsync($"{Buildings}/{a}", new { name = "ตึก A" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_rename_needs_a_name()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var a = await AdminSetupApi.BuildingIdAsync(factory);

        var response = await admin.PutAsJsonAsync($"{Buildings}/{a}", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ต้องใส่ชื่อตึก ไม่เกิน 100 ตัวอักษร", await AdminSetupApi.MessageAsync(response));
    }

    [Theory]
    [InlineData("PUT", "")]
    [InlineData("POST", "/deactivate")]
    [InlineData("POST", "/activate")]
    public async Task Unknown_buildings_are_not_found(string method, string suffix)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var body = method == "PUT" ? JsonContent.Create(new { name = "ตึกใหม่" }) : null;

        var response = await admin.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"{Buildings}/9999{suffix}") { Content = body });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ไม่พบตึกนี้", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_building_with_active_areas_or_supervisors_cannot_be_turned_off()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var a = await AdminSetupApi.BuildingIdAsync(factory);

        var response = await admin.PostAsync($"{Buildings}/{a}/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "ปิดตึก A ไม่ได้ ยังมีพื้นที่ที่เปิดอยู่ 2 แห่ง และหัวหน้าประจำตึก 1 คน ปิดหรือย้ายออกก่อน",
            await AdminSetupApi.MessageAsync(response));
        Assert.Contains("A", (await admin.GetFromJsonAsync<List<BuildingModel>>(Buildings))!.Select(x => x.Code));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Off_areas_and_off_supervisors_do_not_keep_a_building_on()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var b = await TestData.AddBuildingAsync(factory, "B");
        var area = await TestData.AddAreaAsync(factory, "AR03", "B");
        var supervisor = await TestData.AddSupervisorAsync(factory, "S3001", "0830000001", "B", Shift.Day);

        var refused = await admin.PostAsync($"{Buildings}/{b}/deactivate", null);
        await admin.PostAsync($"/api/admin/areas/{area}/deactivate", null);
        await admin.PostAsync($"{AccountApi.Path}/{supervisor}/deactivate", null);
        var off = await admin.PostAsync($"{Buildings}/{b}/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(
            "ปิดตึก B ไม่ได้ ยังมีพื้นที่ที่เปิดอยู่ 1 แห่ง และหัวหน้าประจำตึก 1 คน ปิดหรือย้ายออกก่อน",
            await AdminSetupApi.MessageAsync(refused));
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await off.Content.ReadFromJsonAsync<ManagedBuildingModel>())!.IsActive);
    }

    [Fact]
    public async Task An_empty_building_turns_off_and_on_and_each_change_is_logged_once()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var b = await TestData.AddBuildingAsync(factory, "B");

        var off = await admin.PostAsync($"{Buildings}/{b}/deactivate", null);
        var offAgain = await admin.PostAsync($"{Buildings}/{b}/deactivate", null);
        var whileOff = (await admin.GetFromJsonAsync<List<BuildingModel>>(Buildings))!;
        var on = await admin.PostAsync($"{Buildings}/{b}/activate", null);
        var afterOn = (await admin.GetFromJsonAsync<List<BuildingModel>>(Buildings))!;

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.Equal(HttpStatusCode.OK, offAgain.StatusCode);
        Assert.DoesNotContain("B", whileOff.Select(x => x.Code));
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.True((await on.Content.ReadFromJsonAsync<ManagedBuildingModel>())!.IsActive);
        Assert.Contains("B", afterOn.Select(x => x.Code));
        var audit = await AdminSetupApi.AuditAsync(factory);
        Assert.Equal(new[] { "BUILDING_DEACTIVATE", "BUILDING_ACTIVATE" }, audit.Select(x => x.Action));
        Assert.Equal(new[] { "ปิดใช้งานตึก B", "เปิดใช้งานตึก B" }, audit.Select(x => x.Summary));
    }
}
```

Note for the implementer:
- `TestData.AddBuildingAsync`, `TestData.AddAreaAsync` and `TestData.AddSupervisorAsync` already exist in `Infrastructure/TestData.cs` and return the new row's id.
- `AuthApi.LoggedInAdminAsync` logs in as the seeded `admin`.

- [ ] **Step 6: Run them to see them fail**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~BuildingAdminEndpointTests"`
Expected: FAIL. The new routes do not exist yet, so every test that expects 200, 201, 400, 409, 403 or the "ไม่พบตึกนี้" message fails. Whatever an unmapped path answers (404, 405, or the frontend fallback), it is not what the tests expect.

- [ ] **Step 7: Add the DTOs**

In `backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs`, after `public record BuildingDto(int Id, string Code, string Name);` add:

```csharp

/// <summary>A row of the buildings page, off buildings too (facility-0090); also the D7 and D8 building pickers.</summary>
public record ManagedBuildingDto(int Id, string Code, string Name, bool IsActive, int ActiveAreaCount, int ActiveSupervisorCount);

public record CreateBuildingRequest(string? Code, string? Name);

/// <summary>The code is set once (facility-0090 decision 2), so only the name is sent.</summary>
public record UpdateBuildingRequest(string? Name);
```

- [ ] **Step 8: Write the endpoints**

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminBuildingEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Buildings;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0090: the Admin adds, renames and turns buildings on and off; a building is never deleted. The forms' list of
/// active buildings stays <c>GET /api/admin/buildings</c> in <see cref="AdminAreaEndpoints"/>. Every change is logged
/// (facility-0051).
/// </summary>
public static class AdminBuildingEndpoints
{
    public static IEndpointRouteBuilder MapAdminBuildingEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/buildings/all", async (ClaimsPrincipal principal, AppDbContext db) =>
            await CurrentUser.LoadAsync(principal, db) is null
                ? Results.Unauthorized()
                : Results.Ok(await Rows(db, db.Buildings.AsNoTracking().OrderBy(b => b.Code)).ToListAsync()));
        admin.MapPost("/buildings", CreateAsync);
        admin.MapPut("/buildings/{id:int}", RenameAsync);
        admin.MapPost("/buildings/{id:int}/deactivate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, false, principal, db, clock));
        admin.MapPost("/buildings/{id:int}/activate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, true, principal, db, clock));
        return app;
    }

    /// <summary>What the audit log keeps of a building on add and rename.</summary>
    private sealed record BuildingSnapshot(string Code, string Name);

    private static async Task<IResult> CreateAsync(CreateBuildingRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (BuildingRules.NormalizeCode(request.Code) is not { } code)
        {
            return Bad(BuildingRules.CodeProblem);
        }

        if (BuildingRules.NormalizeName(request.Name) is not { } name)
        {
            return Bad(BuildingRules.NameProblem);
        }

        // Decision 2: a code is never used twice, an off building's included
        if (await db.Buildings.AnyAsync(b => b.Code == code))
        {
            return Conflict(BuildingRules.CodeTaken);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var building = new Building { Code = code, Name = name, CreatedAt = now };
        db.Buildings.Add(building);
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.SaveChangesAsync();
            AuditTrail.Add(
                db, admin.Id, now, "BUILDING_CREATE", "buildings", building.Id, $"เพิ่มตึก {code} {name}",
                before: null, after: new BuildingSnapshot(code, name));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // The unique index on the code: another Admin added the same code at the same moment
            return Conflict(BuildingRules.CodeTaken);
        }

        return Results.Created($"/api/admin/buildings/{building.Id}", await LoadOneAsync(db, building.Id));
    }

    private static async Task<IResult> RenameAsync(int id, UpdateBuildingRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id);
        if (building is null)
        {
            return BuildingNotFound();
        }

        if (BuildingRules.NormalizeName(request.Name) is not { } name)
        {
            return Bad(BuildingRules.NameProblem);
        }

        if (building.Name != name)
        {
            var before = new BuildingSnapshot(building.Code, building.Name);
            building.Name = name;
            AuditTrail.Add(
                db, admin.Id, clock.GetUtcNow().UtcDateTime, "BUILDING_UPDATE", "buildings", building.Id,
                $"แก้ตึก {building.Code} {name}", before, after: new BuildingSnapshot(building.Code, name));
            await db.SaveChangesAsync();
        }

        return Results.Ok(await LoadOneAsync(db, building.Id));
    }

    /// <summary>Decision 4: off only with no active Area and no active Supervisor in it; on with no condition.</summary>
    private static async Task<IResult> SetActiveAsync(int id, bool active, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id);
        if (building is null)
        {
            return BuildingNotFound();
        }

        if (building.IsActive == active)
        {
            return Results.Ok(await LoadOneAsync(db, building.Id));
        }

        if (!active)
        {
            var activeAreas = await db.Areas.CountAsync(a => a.BuildingId == building.Id && a.IsActive);
            var activeSupervisors = await db.Users.CountAsync(
                u => u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId == building.Id);
            if (BuildingRules.DeactivateProblem(building.Code, activeAreas, activeSupervisors) is { } problem)
            {
                return Conflict(problem);
            }
        }

        building.IsActive = active;
        AuditTrail.Add(
            db, admin.Id, clock.GetUtcNow().UtcDateTime, active ? "BUILDING_ACTIVATE" : "BUILDING_DEACTIVATE", "buildings",
            building.Id, $"{(active ? "เปิด" : "ปิด")}ใช้งานตึก {building.Code}",
            before: new { IsActive = !active }, after: new { IsActive = active });
        await db.SaveChangesAsync();

        return Results.Ok(await LoadOneAsync(db, building.Id));
    }

    /// <summary>Filter and order <paramref name="buildings"/> before this: EF cannot order by the record it builds.</summary>
    private static IQueryable<ManagedBuildingDto> Rows(AppDbContext db, IQueryable<Building> buildings) =>
        buildings.Select(b => new ManagedBuildingDto(
            b.Id,
            b.Code,
            b.Name,
            b.IsActive,
            db.Areas.Count(a => a.BuildingId == b.Id && a.IsActive),
            db.Users.Count(u => u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId == b.Id)));

    private static Task<ManagedBuildingDto> LoadOneAsync(AppDbContext db, int id) =>
        Rows(db, db.Buildings.AsNoTracking().Where(b => b.Id == id)).SingleAsync();

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);

    private static IResult Conflict(string message) => ApiResults.Message(StatusCodes.Status409Conflict, message);

    private static IResult BuildingNotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบตึกนี้");
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, after `app.MapAdminAreaEndpoints();` add:

```csharp
app.MapAdminBuildingEndpoints();
```

- [ ] **Step 9: Run the tests to see them pass**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~BuildingAdminEndpointTests|FullyQualifiedName~BuildingRulesTests|FullyQualifiedName~AuditCategoriesTests|FullyQualifiedName~AreaAdminEndpointTests"`
Expected: PASS. `AreaAdminEndpointTests` is in the run to show that `GET /api/admin/buildings` still answers as before.

- [ ] **Step 10: Run the whole backend suite**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS. The MySQL-only tests are skipped, as before.

- [ ] **Step 11: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Buildings/BuildingRules.cs \
  backend/src/FacilityRealtime.Application/Audit/AuditCategories.cs \
  backend/src/FacilityRealtime.Api/Endpoints/AdminBuildingEndpoints.cs \
  backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs \
  backend/src/FacilityRealtime.Api/Program.cs \
  backend/tests/FacilityRealtime.UnitTests/BuildingRulesTests.cs \
  backend/tests/FacilityRealtime.UnitTests/AuditCategoriesTests.cs \
  backend/tests/FacilityRealtime.ApiTests/Admin/BuildingAdminEndpointTests.cs \
  backend/tests/FacilityRealtime.ApiTests/Admin/AdminSetupModels.cs
git commit -m "feat(buildings): Admin adds, renames and turns buildings on and off (facility-0090)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Nothing in an off building turns back on

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs:172-197` (`SetActiveAsync`)
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminAccountEndpoints.cs:203-232` (`SetActiveAsync`)
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/BuildingAdminEndpointTests.cs` (append two tests)

**Interfaces:**
- Consumes: `BuildingRules.OffForArea(string code)` and `BuildingRules.OffForSupervisor(string code)` from Task 1. Also consumes `POST /api/admin/buildings/{id}/deactivate` from Task 1, used by the tests.
- Produces: `POST /api/admin/areas/{id}/activate` and `POST /api/admin/accounts/{id}/activate` answer 409 with the messages above while the building is off. Nothing else changes.

- [ ] **Step 1: Write the failing tests**

Append inside `BuildingAdminEndpointTests` (before its closing brace):

```csharp
    [Fact]
    public async Task An_area_of_an_off_building_stays_off_until_it_moves_or_the_building_is_on()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var a = await AdminSetupApi.BuildingIdAsync(factory);
        var b = await TestData.AddBuildingAsync(factory, "B");
        var area = await TestData.AddAreaAsync(factory, "AR03", "B");
        await admin.PostAsync($"/api/admin/areas/{area}/deactivate", null);
        await admin.PostAsync($"{Buildings}/{b}/deactivate", null);

        var refused = await admin.PostAsync($"/api/admin/areas/{area}/activate", null);
        var moved = await admin.PutAsJsonAsync(
            $"/api/admin/areas/{area}",
            new { name = "Area AR03", buildingId = a, shiftPattern = "DayAndNight", dayCleanerId = (int?)null, nightCleanerId = (int?)null });
        var on = await admin.PostAsync($"/api/admin/areas/{area}/activate", null);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("ตึก B ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายพื้นที่ไปตึกอื่น", await AdminSetupApi.MessageAsync(refused));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.True((await on.Content.ReadFromJsonAsync<AreaDetailModel>())!.Area.IsActive);
        // the refused turn-on wrote nothing; the move is an AREA_UPDATE (its building changed)
        Assert.Equal(
            new[] { "AREA_DEACTIVATE", "BUILDING_DEACTIVATE", "AREA_UPDATE", "AREA_ACTIVATE" },
            (await AdminSetupApi.AuditAsync(factory)).Select(x => x.Action));
    }

    [Fact]
    public async Task A_supervisor_of_an_off_building_stays_off_until_the_building_is_on()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var b = await TestData.AddBuildingAsync(factory, "B");
        var supervisor = await TestData.AddSupervisorAsync(factory, "S3001", "0830000001", "B", Shift.Day);
        await admin.PostAsync($"{AccountApi.Path}/{supervisor}/deactivate", null);
        await admin.PostAsync($"{Buildings}/{b}/deactivate", null);

        var refused = await admin.PostAsync($"{AccountApi.Path}/{supervisor}/activate", null);
        await admin.PostAsync($"{Buildings}/{b}/activate", null);
        var on = await admin.PostAsync($"{AccountApi.Path}/{supervisor}/activate", null);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("ตึก B ปิดใช้งานอยู่ เปิดตึกก่อน หรือย้ายหัวหน้าไปตึกอื่น", await AdminSetupApi.MessageAsync(refused));
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.True((await AccountApi.ReadAsync(on)).IsActive);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~BuildingAdminEndpointTests"`
Expected: the two new tests FAIL, because `refused` is 200 OK.

- [ ] **Step 3: Guard the Area**

In `backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs`, add `using FacilityRealtime.Application.Buildings;` with the other `using` lines. In `SetActiveAsync`, between the `AreaNotFound()` check and `if (area.IsActive != active)`, add:

```csharp
        // facility-0090 decision 4: an Area of a building that is off stays off
        if (active && !area.IsActive
            && await db.Buildings.Where(b => b.Id == area.BuildingId && !b.IsActive).Select(b => b.Code).FirstOrDefaultAsync() is { } offCode)
        {
            return Conflict(BuildingRules.OffForArea(offCode));
        }

```

- [ ] **Step 4: Guard the Supervisor**

In `backend/src/FacilityRealtime.Api/Endpoints/AdminAccountEndpoints.cs`, add `using FacilityRealtime.Application.Buildings;` with the other `using` lines. In `SetActiveAsync`, just before `if (active && await HolderAsync(`, add:

```csharp
        // facility-0090 decision 4: a Supervisor of a building that is off stays off
        if (active && user.Role == UserRole.Supervisor && user.BuildingId is int buildingId
            && await db.Buildings.Where(b => b.Id == buildingId && !b.IsActive).Select(b => b.Code).FirstOrDefaultAsync() is { } offCode)
        {
            return Conflict(BuildingRules.OffForSupervisor(offCode));
        }

```

`Conflict` here is `AccountChecks.Conflict`, through the file's `using static`.

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test backend/FacilityRealtime.slnx --filter "FullyQualifiedName~BuildingAdminEndpointTests|FullyQualifiedName~AreaAdminEndpointTests|FullyQualifiedName~AccountEndpointTests|FullyQualifiedName~AccountEditEndpointTests"`
Expected: PASS.

- [ ] **Step 6: Run the whole backend suite**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs \
  backend/src/FacilityRealtime.Api/Endpoints/AdminAccountEndpoints.cs \
  backend/tests/FacilityRealtime.ApiTests/Admin/BuildingAdminEndpointTests.cs
git commit -m "feat(buildings): an Area or Supervisor of an off building cannot be turned on (facility-0090)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The D7 and D8 picker lists off buildings too

**Files:**
- Modify: `frontend/src/shared/api/adminSetupApi.ts` (a type, a query, an export)
- Modify: `frontend/src/shared/components/BuildingSelect.tsx`
- Modify: `frontend/src/test/adminFixtures.ts` (a fixture)
- Test: `frontend/src/pages/export/ExportPage.test.tsx`
- Test: `frontend/src/pages/inspection-summary/InspectionSummaryPage.test.tsx`

**Interfaces:**
- Consumes: `GET /api/admin/buildings/all` returns `ManagedBuildingDto[]` (Task 1).
- Produces:
  - `export interface ManagedBuilding extends Building { isActive: boolean; activeAreaCount: number; activeSupervisorCount: number }` and `useGetAllBuildingsQuery` from `shared/api/adminSetupApi.ts`.
  - `managedBuildings: ManagedBuilding[]` from `test/adminFixtures.ts`, containing `{ id: 1, code: 'A', name: 'ตึกทดลอง', isActive: true, activeAreaCount: 2, activeSupervisorCount: 1 }`, `{ id: 2, code: 'B', name: 'ตึกข้างเคียง', isActive: true, activeAreaCount: 0, activeSupervisorCount: 0 }` and `{ id: 3, code: 'C', name: 'ตึกเก่า', isActive: false, activeAreaCount: 0, activeSupervisorCount: 0 }`.
  - Task 4 uses all three.

- [ ] **Step 1: Add the fixture and point the two pages' fakes at the full list**

In `frontend/src/test/adminFixtures.ts`, add `ManagedBuilding` to the type import from `'../shared/api/adminSetupApi'` and add after `buildings`:

```ts
/** GET /api/admin/buildings/all (facility-0090): A holds two open Areas and a Supervisor, B is empty, C is off. */
export const managedBuildings: ManagedBuilding[] = [
  { id: 1, code: 'A', name: 'ตึกทดลอง', isActive: true, activeAreaCount: 2, activeSupervisorCount: 1 },
  { id: 2, code: 'B', name: 'ตึกข้างเคียง', isActive: true, activeAreaCount: 0, activeSupervisorCount: 0 },
  { id: 3, code: 'C', name: 'ตึกเก่า', isActive: false, activeAreaCount: 0, activeSupervisorCount: 0 },
];
```

In `frontend/src/pages/export/ExportPage.test.tsx` and in `frontend/src/pages/inspection-summary/InspectionSummaryPage.test.tsx`:
- Import `managedBuildings` instead of `buildings` from `'../../test/adminFixtures'`.
- In `serve`, replace the line `'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),` with `'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),`.

Append to the `describe` block of `ExportPage.test.tsx`:

```tsx
  it('the building picker lists every building, an off one marked (ปิดใช้งาน) (facility-0090)', async () => {
    serve();
    await renderApp('/admin/export');

    const picker = await screen.findByRole('combobox', { name: 'ตึก' });
    await waitFor(() =>
      expect(within(picker).getAllByRole('option').map((option) => option.textContent)).toEqual([
        'ทุกตึก',
        'A',
        'B',
        'C (ปิดใช้งาน)',
      ]),
    );
  });
```

Append to the `describe` block of `InspectionSummaryPage.test.tsx`:

```tsx
  it('an off building can still be chosen, for its old data (facility-0090)', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/inspection-summary');

    const picker = await screen.findByRole('combobox', { name: 'ตึก' });
    await waitFor(() => expect(within(picker).getByRole('option', { name: 'C (ปิดใช้งาน)' })).toBeInTheDocument());
    await user.selectOptions(picker, 'C (ปิดใช้งาน)');

    await waitFor(() => expect(query(summaryGets(server).at(-1)!.search)).toMatchObject({ buildingId: '3' }));
  });
```

`query` and `summaryGets` are already defined at the top of `InspectionSummaryPage.test.tsx`. The request's building parameter is `buildingId`, as the existing test around line 113 reads it.

- [ ] **Step 2: Run them to see them fail**

Run: `cd frontend && npx vitest run src/pages/export src/pages/inspection-summary`
Expected: FAIL. `BuildingSelect` still asks `GET /api/admin/buildings`, which now has no fake, so the picker shows only `ทุกตึก`. The existing tests that pick `B` fail too.

- [ ] **Step 3: Add the type and the query**

In `frontend/src/shared/api/adminSetupApi.ts`, after the `Building` interface, add:

```ts
/** ManagedBuildingDto (facility-0090): every building, off ones too, with what is active in it. */
export interface ManagedBuilding extends Building {
  isActive: boolean;
  activeAreaCount: number;
  activeSupervisorCount: number;
}
```

After the `getBuildings` endpoint, add:

```ts
    /** Every building, off ones too: the buildings page and the D7/D8 picker (facility-0090 decision 5). */
    getAllBuildings: build.query<ManagedBuilding[], void>({
      query: () => '/admin/buildings/all',
      providesTags: ['AdminSetup'],
    }),
```

Add `useGetAllBuildingsQuery,` after `useGetBuildingsQuery,` in the exported hooks.

- [ ] **Step 4: The picker reads the full list**

Replace `frontend/src/shared/components/BuildingSelect.tsx` with:

```tsx
import { useGetAllBuildingsQuery } from '../api/adminSetupApi';
import { Field, Select } from './Field';

interface BuildingSelectProps {
  value?: number;
  onChange: (buildingId: number | undefined) => void;
}

/**
 * "ตึก" on the report pages (D7, D8): every building, or one by its code. A building that is off is still listed,
 * marked, because its old data is still there (facility-0090 decision 5).
 */
export function BuildingSelect({ value, onChange }: BuildingSelectProps) {
  const { data: buildings } = useGetAllBuildingsQuery();
  return (
    <Field label="ตึก">
      <Select
        size="desk"
        value={value ?? ''}
        onChange={(event) => onChange(event.target.value === '' ? undefined : Number(event.target.value))}
      >
        <option value="">ทุกตึก</option>
        {(buildings ?? []).map((building) => (
          <option key={building.id} value={building.id}>
            {building.isActive ? building.code : `${building.code} (ปิดใช้งาน)`}
          </option>
        ))}
      </Select>
    </Field>
  );
}
```

- [ ] **Step 5: Run the tests to see them pass**

Run: `cd frontend && npx vitest run src/pages/export src/pages/inspection-summary`
Expected: PASS.

- [ ] **Step 6: Lint, types, and the whole frontend suite**

Run: `cd frontend && npm run lint && npx tsc -b && npx vitest run`
Expected: all pass. If another test fakes `GET /api/admin/buildings` for a page that uses `BuildingSelect`, point it at `/all` with `managedBuildings`, as Step 1 does. A page that uses `useGetBuildingsQuery` keeps its fake.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/shared/api/adminSetupApi.ts frontend/src/shared/components/BuildingSelect.tsx \
  frontend/src/test/adminFixtures.ts frontend/src/pages/export/ExportPage.test.tsx \
  frontend/src/pages/inspection-summary/InspectionSummaryPage.test.tsx
git commit -m "feat(reports): the D7 and D8 building picker lists off buildings, marked (facility-0090)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The buildings page

**Files:**
- Create: `frontend/src/pages/buildings/index.ts`
- Create: `frontend/src/pages/buildings/BuildingsPage.tsx`
- Create: `frontend/src/pages/buildings/BuildingTable.tsx`
- Create: `frontend/src/pages/buildings/BuildingDrawer.tsx`
- Create: `frontend/src/pages/buildings/buildingsApi.ts`
- Create: `frontend/src/pages/buildings/buildingRules.ts`
- Test: `frontend/src/pages/buildings/buildingRules.test.ts` (create)
- Test: `frontend/src/pages/buildings/BuildingsPage.test.tsx` (create)
- Modify: `frontend/src/router.tsx:94` (a lazy route after `admin/areas/:areaId/edit`)
- Modify: `frontend/src/shared/components/AdminNav.tsx:15` (a menu link)
- Modify: `frontend/src/router.test.tsx` (the menu list and the lazy route list)
- Modify: `frontend/src/pages/history/historyRules.ts` (four action names, the Setup label)
- Modify: `frontend/src/pages/history/historyRules.test.ts`, `frontend/src/pages/history/HistoryPage.test.tsx`

**Interfaces:**
- Consumes from Task 3: `ManagedBuilding`, `useGetAllBuildingsQuery` and `managedBuildings`. Consumes from Task 1: `POST /api/admin/buildings`, `PUT /api/admin/buildings/{id}`, `POST /api/admin/buildings/{id}/deactivate|activate`.
- Produces: the route `/admin/buildings` (lazy) and the menu link `ตึก`.

- [ ] **Step 1: Write the failing rule tests**

Create `frontend/src/pages/buildings/buildingRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { managedBuildings } from '../../test/adminFixtures';
import { draftProblem, statusText, stillInside } from './buildingRules';

const [withAreas, empty, off] = managedBuildings;

describe('buildingRules (facility-0090)', () => {
  it('draftProblem asks for the code of a new building, and a name always', () => {
    expect(draftProblem('', 'ตึกผลิต', true)).toBe('ใส่รหัสตึกก่อน');
    expect(draftProblem('  ', 'ตึกผลิต', true)).toBe('ใส่รหัสตึกก่อน');
    expect(draftProblem('B2', ' ', true)).toBe('ใส่ชื่อตึกก่อน');
    expect(draftProblem('', 'ตึกผลิต', false)).toBeNull();
    expect(draftProblem('B2', 'ตึกผลิต', true)).toBeNull();
  });

  it('stillInside says what keeps a building on, as the server counts it', () => {
    expect(stillInside(withAreas)).toBe('ยังมีพื้นที่ที่เปิดอยู่ 2 แห่ง และหัวหน้าประจำตึก 1 คน');
    expect(stillInside({ ...empty, activeAreaCount: 3 })).toBe('ยังมีพื้นที่ที่เปิดอยู่ 3 แห่ง');
    expect(stillInside({ ...empty, activeSupervisorCount: 2 })).toBe('ยังมีหัวหน้าประจำตึก 2 คน');
    expect(stillInside(empty)).toBeNull();
  });

  it('statusText is the status in words', () => {
    expect(statusText(empty)).toBe('ใช้งาน');
    expect(statusText(off)).toBe('ปิดใช้งาน');
  });
});
```

- [ ] **Step 2: Write the failing page tests**

Create `frontend/src/pages/buildings/BuildingsPage.test.tsx`:

```tsx
import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { adminSession, managedBuildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

const [a, b, c] = managedBuildings;

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),
    ...routes,
  });
}

const sent = (server: ReturnType<typeof serve>, method: string, path: string) =>
  server.calls.filter((call) => call.method === method && call.path === path);

/** The cells after the code, in a row found by its code. */
function cellsOf(code: string): (string | null)[] {
  const row = screen.getByRole('row', { name: new RegExp(`^${code} `) });
  return within(row).getAllByRole('cell').map((cell) => cell.textContent);
}

async function openEditor(user: Awaited<ReturnType<typeof renderApp>>['user'], building: ManagedBuilding) {
  await user.click(await screen.findByRole('button', { name: `แก้ไข ตึก ${building.code}` }));
  return screen.getByRole('dialog', { name: `แก้ไขตึก · ${building.code} ${building.name}` });
}

describe('BuildingsPage (facility-0090)', () => {
  it('lists every building, off ones too, with its open Areas and its status in words', async () => {
    serve();
    await renderApp('/admin/buildings');

    expect(await screen.findByRole('heading', { level: 1, name: 'ตึก' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { level: 2, name: 'ตึกทั้งหมด (3)' })).toBeInTheDocument();
    expect(cellsOf('A')).toEqual(['ตึกทดลอง', '2', 'ใช้งาน', 'แก้ไข']);
    expect(cellsOf('B')).toEqual(['ตึกข้างเคียง', '0', 'ใช้งาน', 'แก้ไข']);
    expect(cellsOf('C')).toEqual(['ตึกเก่า', '0', 'ปิดใช้งาน', 'แก้ไข']);
    const menu = screen.getByRole('navigation', { name: 'เมนูแอดมิน' });
    expect(within(menu).getByRole('link', { name: 'ตึก' })).toHaveAttribute('aria-current', 'page');
  });

  it('adds a building in the Drawer; the save closes it and floats its answer', async () => {
    const server = serve({
      'POST /api/admin/buildings': () => ({
        status: 201,
        body: { ...b, id: 4, code: 'B2', name: 'ตึกผลิต' },
      }),
    });
    const { user } = await renderApp('/admin/buildings');

    await user.click(await screen.findByRole('button', { name: '+ เพิ่มตึก' }));
    const drawer = screen.getByRole('dialog', { name: 'เพิ่มตึก' });
    expect(within(drawer).getByText('ตัวอักษรอังกฤษหรือตัวเลข 1–20 ตัว เช่น B2 · แก้ทีหลังไม่ได้')).toBeInTheDocument();
    await user.type(within(drawer).getByRole('textbox', { name: 'รหัสตึก' }), ' b2 ');
    await user.type(within(drawer).getByRole('textbox', { name: 'ชื่อตึก' }), 'ตึกผลิต');
    await user.click(within(drawer).getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByText('เพิ่มตึก B2 ตึกผลิต แล้ว')).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'เพิ่มตึก' })).not.toBeInTheDocument();
    expect(sent(server, 'POST', '/api/admin/buildings').map((call) => call.body)).toEqual([{ code: 'b2', name: 'ตึกผลิต' }]);
  });

  it('checks the code and the name before sending', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/buildings');

    await user.click(await screen.findByRole('button', { name: '+ เพิ่มตึก' }));
    const drawer = screen.getByRole('dialog', { name: 'เพิ่มตึก' });
    await user.click(within(drawer).getByRole('button', { name: 'บันทึก' }));
    expect(within(drawer).getByText('ใส่รหัสตึกก่อน')).toBeInTheDocument();
    await user.type(within(drawer).getByRole('textbox', { name: 'รหัสตึก' }), 'B2');
    await user.click(within(drawer).getByRole('button', { name: 'บันทึก' }));

    expect(within(drawer).getByText('ใส่ชื่อตึกก่อน')).toBeInTheDocument();
    expect(sent(server, 'POST', '/api/admin/buildings')).toEqual([]);
  });

  it("shows the server's answer when the code is taken, and keeps the Drawer open", async () => {
    serve({ 'POST /api/admin/buildings': () => ({ status: 409, body: { message: 'รหัสตึกนี้มีแล้ว' } }) });
    const { user } = await renderApp('/admin/buildings');

    await user.click(await screen.findByRole('button', { name: '+ เพิ่มตึก' }));
    const drawer = screen.getByRole('dialog', { name: 'เพิ่มตึก' });
    await user.type(within(drawer).getByRole('textbox', { name: 'รหัสตึก' }), 'C');
    await user.type(within(drawer).getByRole('textbox', { name: 'ชื่อตึก' }), 'ตึกใหม่');
    await user.click(within(drawer).getByRole('button', { name: 'บันทึก' }));

    expect(await within(drawer).findByText('รหัสตึกนี้มีแล้ว')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'เพิ่มตึก' })).toBeInTheDocument();
  });

  it('renames a building; its code is shown, not offered', async () => {
    const server = serve({ 'PUT /api/admin/buildings/1': () => ({ status: 200, body: { ...a, name: 'อาคารผลิต 1' } }) });
    const { user } = await renderApp('/admin/buildings');

    const drawer = await openEditor(user, a);
    expect(within(drawer).getByText('รหัสตึก A (แก้ไม่ได้)')).toBeInTheDocument();
    expect(within(drawer).queryByRole('textbox', { name: 'รหัสตึก' })).not.toBeInTheDocument();
    const name = within(drawer).getByRole('textbox', { name: 'ชื่อตึก' });
    expect(name).toHaveValue('ตึกทดลอง');
    await user.clear(name);
    await user.type(name, 'อาคารผลิต 1');
    await user.click(within(drawer).getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByText('บันทึกตึก A อาคารผลิต 1 แล้ว')).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/buildings/1').map((call) => call.body)).toEqual([{ name: 'อาคารผลิต 1' }]);
  });

  it('a building with open Areas or Supervisors cannot be turned off, and says what is left', async () => {
    serve();
    const { user } = await renderApp('/admin/buildings');

    const drawer = await openEditor(user, a);

    expect(
      within(drawer).getByText(
        'ยังมีพื้นที่ที่เปิดอยู่ 2 แห่ง และหัวหน้าประจำตึก 1 คน ปิดพื้นที่ และย้ายหรือปิดบัญชีหัวหน้าก่อน จึงจะปิดตึกได้',
      ),
    ).toBeInTheDocument();
    expect(within(drawer).getByRole('button', { name: 'ปิดใช้งานตึกนี้' })).toBeDisabled();
  });

  it('turns an empty building off, and an off one back on', async () => {
    const server = serve({
      'POST /api/admin/buildings/2/deactivate': () => ({ status: 200, body: { ...b, isActive: false } }),
      'POST /api/admin/buildings/3/activate': () => ({ status: 200, body: { ...c, isActive: true } }),
    });
    const { user } = await renderApp('/admin/buildings');

    await user.click(within(await openEditor(user, b)).getByRole('button', { name: 'ปิดใช้งานตึกนี้' }));
    expect(await screen.findByText('ปิดใช้งานตึก B แล้ว')).toBeInTheDocument();

    const offDrawer = await openEditor(user, c);
    expect(within(offDrawer).getByText('ตึกนี้ปิดใช้งานอยู่ เปิดพื้นที่หรือบัญชีหัวหน้าในตึกนี้ไม่ได้')).toBeInTheDocument();
    await user.click(within(offDrawer).getByRole('button', { name: 'เปิดใช้งานตึกนี้' }));

    expect(await screen.findByText('เปิดใช้งานตึก C แล้ว')).toBeInTheDocument();
    expect(sent(server, 'POST', '/api/admin/buildings/2/deactivate')).toHaveLength(1);
    expect(sent(server, 'POST', '/api/admin/buildings/3/activate')).toHaveLength(1);
  });
});
```

- [ ] **Step 3: Run them to see them fail**

Run: `cd frontend && npx vitest run src/pages/buildings`
Expected: FAIL. `./buildingRules` does not exist, and `/admin/buildings` is the 404 page.

- [ ] **Step 4: Write the rules and the API**

Create `frontend/src/pages/buildings/buildingRules.ts`:

```ts
import type { ManagedBuilding } from '../../shared/api/adminSetupApi';

/** What the page checks before sending; the server checks the code's format and that it is free, and its message is shown. */
export function draftProblem(code: string, name: string, isNew: boolean): string | null {
  if (isNew && code.trim() === '') {
    return 'ใส่รหัสตึกก่อน';
  }
  if (name.trim() === '') {
    return 'ใส่ชื่อตึกก่อน';
  }
  return null;
}

/** facility-0090 decision 4, counted as the server's BuildingRules.DeactivateProblem counts it; null when nothing is left. */
export function stillInside(building: ManagedBuilding): string | null {
  const left: string[] = [];
  if (building.activeAreaCount > 0) {
    left.push(`พื้นที่ที่เปิดอยู่ ${building.activeAreaCount} แห่ง`);
  }
  if (building.activeSupervisorCount > 0) {
    left.push(`หัวหน้าประจำตึก ${building.activeSupervisorCount} คน`);
  }
  return left.length === 0 ? null : `ยังมี${left.join(' และ ')}`;
}

export function statusText(building: ManagedBuilding): string {
  return building.isActive ? 'ใช้งาน' : 'ปิดใช้งาน';
}
```

Create `frontend/src/pages/buildings/buildingsApi.ts`:

```ts
import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { baseApi } from '../../shared/api/baseApi';

/** The buildings page's saves (facility-0090). The code goes only with a new building: it is fixed once created. */
const buildingsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    createBuilding: build.mutation<ManagedBuilding, { code: string; name: string }>({
      query: (body) => ({ url: '/admin/buildings', method: 'POST', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    renameBuilding: build.mutation<ManagedBuilding, { buildingId: number; name: string }>({
      query: ({ buildingId, name }) => ({ url: `/admin/buildings/${buildingId}`, method: 'PUT', body: { name } }),
      invalidatesTags: ['AdminSetup'],
    }),
    setBuildingActive: build.mutation<ManagedBuilding, { buildingId: number; active: boolean }>({
      query: ({ buildingId, active }) => ({
        url: `/admin/buildings/${buildingId}/${active ? 'activate' : 'deactivate'}`,
        method: 'POST',
      }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const { useCreateBuildingMutation, useRenameBuildingMutation, useSetBuildingActiveMutation } = buildingsApi;
```

- [ ] **Step 5: Write the table and the Drawer**

Create `frontend/src/pages/buildings/BuildingTable.tsx`:

```tsx
import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { statusText } from './buildingRules';

interface BuildingTableProps {
  buildings: ManagedBuilding[];
  onEdit: (building: ManagedBuilding) => void;
}

/**
 * facility-0090 decision 1: every building in the server's order (by code); an off one is faded and says so in words.
 * Each button names its building, so a screen reader and the tests can tell the rows apart.
 */
export function BuildingTable({ buildings, onEdit }: BuildingTableProps) {
  return (
    <Table label="ตึก">
      <thead>
        <tr>
          <th scope="col">รหัส</th>
          <th scope="col">ชื่อ</th>
          <th scope="col">พื้นที่ที่เปิดอยู่</th>
          <th scope="col">สถานะ</th>
          <th scope="col">จัดการ</th>
        </tr>
      </thead>
      <tbody>
        {buildings.map((building) => (
          <tr key={building.id} className={building.isActive ? undefined : 'text-muted'}>
            <th scope="row">{building.code}</th>
            <td>{building.name}</td>
            <td>{building.activeAreaCount}</td>
            <td>{statusText(building)}</td>
            <td>
              <Button variant="link" size="desk" aria-label={`แก้ไข ตึก ${building.code}`} onClick={() => onEdit(building)}>
                แก้ไข
              </Button>
            </td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
```

Create `frontend/src/pages/buildings/BuildingDrawer.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { Drawer } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { boxClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { draftProblem, stillInside } from './buildingRules';
import { useCreateBuildingMutation, useRenameBuildingMutation, useSetBuildingActiveMutation } from './buildingsApi';

interface BuildingDrawerProps {
  /** Missing: a new building. */
  building?: ManagedBuilding;
  onClose: () => void;
  /** A save or a switch closes the Drawer and floats its answer. */
  onDone: (message: string) => void;
}

/**
 * facility-0090 decisions 1–4 in the right-hand Drawer, as D4's Area: the code (only when new), the name, and for a
 * building that exists, turning it off or on. Off is offered only when nothing active is left in it; the server checks too.
 */
export function BuildingDrawer({ building, onClose, onDone }: BuildingDrawerProps) {
  const [code, setCode] = useState('');
  const [name, setName] = useState(building?.name ?? '');
  const [problem, setProblem] = useState<string | null>(null);
  const [switchProblem, setSwitchProblem] = useState<string | null>(null);
  const [createBuilding, created] = useCreateBuildingMutation();
  const [renameBuilding, renamed] = useRenameBuildingMutation();
  const [setBuildingActive, switching] = useSetBuildingActiveMutation();
  const sending = created.isLoading || renamed.isLoading || switching.isLoading;
  const left = building?.isActive ? stillInside(building) : null;
  const titleId = 'building-editor-title';

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const found = draftProblem(code, name, !building);
    setProblem(found);
    if (found) {
      return;
    }
    try {
      if (building) {
        const saved = await renameBuilding({ buildingId: building.id, name: name.trim() }).unwrap();
        onDone(`บันทึกตึก ${saved.code} ${saved.name} แล้ว`);
      } else {
        const saved = await createBuilding({ code: code.trim(), name: name.trim() }).unwrap();
        onDone(`เพิ่มตึก ${saved.code} ${saved.name} แล้ว`);
      }
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  const toggle = async (current: ManagedBuilding) => {
    setSwitchProblem(null);
    try {
      const saved = await setBuildingActive({ buildingId: current.id, active: !current.isActive }).unwrap();
      onDone(`${saved.isActive ? 'เปิด' : 'ปิด'}ใช้งานตึก ${saved.code} แล้ว`);
    } catch (error) {
      setSwitchProblem(getErrorMessage(error));
    }
  };

  return (
    <Drawer labelledBy={titleId} onClose={onClose}>
      <div className="flex flex-col gap-3">
        <div className="flex items-start justify-between gap-2">
          <h2 id={titleId} tabIndex={-1} className="text-lg font-semibold">
            {building ? `แก้ไขตึก · ${building.code} ${building.name}` : 'เพิ่มตึก'}
          </h2>
          <Button size="desk" onClick={onClose}>
            ปิด
          </Button>
        </div>
        <form className={`flex flex-col gap-3 ${boxClass}`} onSubmit={(event) => void submit(event)}>
          {building ? (
            <p className="text-muted">{`รหัสตึก ${building.code} (แก้ไม่ได้)`}</p>
          ) : (
            <Field label="รหัสตึก" help="ตัวอักษรอังกฤษหรือตัวเลข 1–20 ตัว เช่น B2 · แก้ทีหลังไม่ได้">
              <TextInput
                size="desk"
                value={code}
                maxLength={20}
                disabled={sending}
                onChange={(event) => setCode(event.target.value)}
              />
            </Field>
          )}
          <Field label="ชื่อตึก">
            <TextInput
              size="desk"
              value={name}
              maxLength={100}
              disabled={sending}
              onChange={(event) => setName(event.target.value)}
            />
          </Field>
          {problem && <Note tone="error">{problem}</Note>}
          <div className="flex flex-wrap gap-2">
            <Button type="submit" variant="primary" size="desk" disabled={sending}>
              บันทึก
            </Button>
            <Button size="desk" disabled={sending} onClick={onClose}>
              ยกเลิก
            </Button>
          </div>
        </form>
        {building && (
          <section className={`flex flex-col gap-2 ${boxClass}`} aria-labelledby="building-active-title">
            <h3 id="building-active-title" className="text-base font-semibold">
              {building.isActive ? 'ปิดใช้งานตึก' : 'เปิดใช้งานตึก'}
            </h3>
            {!building.isActive && <Note tone="warn">ตึกนี้ปิดใช้งานอยู่ เปิดพื้นที่หรือบัญชีหัวหน้าในตึกนี้ไม่ได้</Note>}
            {building.isActive && left && (
              <Note tone="warn">{`${left} ปิดพื้นที่ และย้ายหรือปิดบัญชีหัวหน้าก่อน จึงจะปิดตึกได้`}</Note>
            )}
            {building.isActive && !left && (
              <p>ตึกหายจากฟอร์มและหน้าข้อมูลสด ยังเลือกได้ในสรุปการตรวจและส่งออก Excel ประวัติยังอยู่ เปิดกลับได้ ไม่มีปุ่มลบ</p>
            )}
            {switchProblem && <Note tone="error">{switchProblem}</Note>}
            <div>
              {/* turning off is undone by the same button, so it is not `danger` (MASTER.md): the words carry it */}
              <Button size="desk" disabled={sending || left !== null} onClick={() => void toggle(building)}>
                {building.isActive ? 'ปิดใช้งานตึกนี้' : 'เปิดใช้งานตึกนี้'}
              </Button>
            </div>
          </section>
        )}
        <p className="text-muted">การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน</p>
      </div>
    </Drawer>
  );
}
```

- [ ] **Step 6: Write the page, its route and its menu link**

Create `frontend/src/pages/buildings/BuildingsPage.tsx`:

```tsx
import { useState } from 'react';
import { useGetAllBuildingsQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { Toast, type Notice } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { BuildingDrawer } from './BuildingDrawer';
import { BuildingTable } from './BuildingTable';

type Open = { kind: 'create' } | { kind: 'edit'; id: number } | null;

/**
 * facility-0090: every building, off ones too, in one table; a building is added and edited in the right-hand Drawer,
 * as D4's Area is. A save closes the Drawer and floats its answer. A building is never deleted, only turned off.
 */
export function BuildingsPage() {
  const buildings = useGetAllBuildingsQuery(undefined, freshOnOpen);
  const [open, setOpen] = useState<Open>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const list = buildings.data ?? [];
  const editing = open?.kind === 'edit' ? list.find((building) => building.id === open.id) : undefined;
  const close = () => setOpen(null);
  const done = (text: string) => {
    setOpen(null);
    setNotice({ kind: 'ok', text });
  };

  return (
    <main className="flex flex-col gap-4 text-sm text-text">
      <h1 className="text-xl font-bold">ตึก</h1>
      {buildings.error && <Note tone="error">{getErrorMessage(buildings.error)}</Note>}
      <section className="flex min-w-0 flex-col gap-2.5" aria-labelledby="buildings-list-title">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 id="buildings-list-title" className="text-base font-bold">{`ตึกทั้งหมด (${list.length})`}</h2>
          <Button size="desk" onClick={() => setOpen({ kind: 'create' })}>
            + เพิ่มตึก
          </Button>
        </div>
        {buildings.isLoading && <p className="text-muted">กำลังโหลดตึก...</p>}
        {buildings.data?.length === 0 && <Note tone="info">ยังไม่มีตึก กด "+ เพิ่มตึก" เพื่อเริ่ม</Note>}
        {list.length > 0 && <BuildingTable buildings={list} onEdit={(building) => setOpen({ kind: 'edit', id: building.id })} />}
      </section>
      <p className="text-muted">
        ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติยังอยู่ · ปิดตึกได้เมื่อไม่มีพื้นที่ที่เปิดอยู่และไม่มีหัวหน้าประจำตึก ·
        ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
      </p>
      {open?.kind === 'create' && <BuildingDrawer onClose={close} onDone={done} />}
      {editing && <BuildingDrawer key={editing.id} building={editing} onClose={close} onDone={done} />}
      {notice && <Toast notice={notice} onClose={() => setNotice(null)} />}
    </main>
  );
}
```

Create `frontend/src/pages/buildings/index.ts`:

```ts
export { BuildingsPage } from './BuildingsPage';
```

In `frontend/src/router.tsx`, after the route object whose `path` is `'admin/areas/:areaId/edit'` (it ends at line 94), add:

```tsx
              // facility-0090: the buildings, off ones too; add and edit in the Drawer
              { path: 'admin/buildings', lazy: async () => ({ Component: (await import('./pages/buildings')).BuildingsPage }) },
```

In `frontend/src/shared/components/AdminNav.tsx`, after the `/admin/areas` link, add:

```ts
  { to: '/admin/buildings', label: 'ตึก', counted: false },
```

In `frontend/src/router.test.tsx`:
- In the menu test, add `'ตึก',` after `'จัดการพื้นที่และป้าย',`.
- In the lazy-route test, add `'admin/buildings',` after `'admin/areas/:areaId/edit',`.

- [ ] **Step 7: The history names the four actions**

In `frontend/src/pages/history/historyRules.ts`:
- In `categoryLabel`, `Setup: 'พื้นที่ · จุด · รอบ · ป้าย',` becomes `Setup: 'ตึก · พื้นที่ · จุด · รอบ · ป้าย',` (plan ruling 3).
- In `actionLabel`, add before `AREA_CREATE: 'เพิ่มพื้นที่',`:

```ts
  BUILDING_CREATE: 'เพิ่มตึก',
  BUILDING_UPDATE: 'แก้ตึก',
  BUILDING_ACTIVATE: 'เปิดใช้ตึก',
  BUILDING_DEACTIVATE: 'ปิดใช้ตึก',
```

In `frontend/src/pages/history/HistoryPage.test.tsx`, in the "ประเภท" options list, `'พื้นที่ · จุด · รอบ · ป้าย'` becomes `'ตึก · พื้นที่ · จุด · รอบ · ป้าย'`.

In `frontend/src/pages/history/historyRules.test.ts`, in the test `actionText names each action in Thai, and shows an unknown one as stored`, add:

```ts
    expect(actionText('BUILDING_DEACTIVATE')).toBe('ปิดใช้ตึก');
```

- [ ] **Step 8: Run the tests to see them pass**

Run: `cd frontend && npx vitest run src/pages/buildings src/pages/history src/router.test.tsx`
Expected: PASS.

- [ ] **Step 9: Lint, types, the whole suite and the size**

Run: `cd frontend && npm run lint && npx tsc -b && npx vitest run && npm run build`
Expected:
- Everything passes.
- In the build output, the main JS stays within 200 kB gzip (facility-0062), and `BuildingsPage` is its own lazy chunk.
- Write the main JS and CSS gzip sizes into the report.

- [ ] **Step 10: Commit**

```bash
git add frontend/src/pages/buildings frontend/src/router.tsx frontend/src/router.test.tsx \
  frontend/src/shared/components/AdminNav.tsx frontend/src/pages/history/historyRules.ts \
  frontend/src/pages/history/historyRules.test.ts frontend/src/pages/history/HistoryPage.test.tsx
git commit -m "feat(buildings): the buildings page, add and edit in the Drawer (facility-0090)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The records

**Files:**
- Modify: `docs/adr/facility-0045-admin-manages-areas-and-captures-sign-location.md` (end: an amendment line)
- Modify: `docs/adr/facility-0048-supervisor-assigned-to-building.md` (end: an amendment line)
- Modify: `CONTEXT.md` (a **Building** entry between **Blocked Scan** and **Cover Assignment**)
- Modify: `docs/code-map.md` (the page table, the route table, the Admin setup endpoint table)
- Modify: `docs/handoff.md` (Last updated, the Pending-work bullet, a task-log line)

**Interfaces:**
- Consumes: the finished Tasks 1–4. This task reads the test counts and gzip sizes from their reports, or reruns the suites.
- Produces: nothing used by code.

- [ ] **Step 1: The ADR amendments**

Append to `docs/adr/facility-0045-admin-manages-areas-and-captures-sign-location.md`:

```markdown

> **แก้ไข 2026-10-26 (facility-0090):** ตึกมีหน้าจัดการของตัวเอง (`/admin/buildings`, เมนู "ตึก") พื้นที่ในตึกที่ปิดใช้งานอยู่เปิดใช้งานไม่ได้ ต้องเปิดตึกก่อน หรือย้ายพื้นที่ไปตึกที่เปิดอยู่ในฟอร์มพื้นที่
```

Append to `docs/adr/facility-0048-supervisor-assigned-to-building.md`:

```markdown

> **แก้ไข 2026-10-26 (facility-0090):** แอดมินเพิ่ม แก้ชื่อ และเปิดปิดตึกในเมนู "ตึก" ตึกปิดได้เมื่อไม่มีพื้นที่ที่เปิดอยู่และไม่มีบัญชีหัวหน้าที่เปิดใช้งานประจำตึกนั้น บัญชีหัวหน้าที่ประจำตึกที่ปิดอยู่เปิดใช้งานไม่ได้ ตัวเลือกตึกในสรุปการตรวจ (D7) และส่งออก Excel (D8) แสดงตึกที่ปิดด้วย ต่อท้าย "(ปิดใช้งาน)"
```

- [ ] **Step 2: The glossary**

In `CONTEXT.md`, between the **Blocked Scan** entry and `**Cover Assignment**:`, add:

```markdown
**Building**:
ตึกในโรงงาน มีรหัส (ตั้งครั้งเดียว A–Z/0–9 ยาว 1–20 ตัว) และชื่อ (แก้ได้ ซ้ำกับตึกอื่นได้) ทุก Area อยู่ในตึก 1 ตึก และหัวหน้าประจำตึก + กะ Admin เพิ่ม แก้ชื่อ และเปิดปิดตึกเอง ไม่ลบ ปิดได้เมื่อไม่มี Area ที่เปิดอยู่และไม่มีหัวหน้าประจำตึก (facility-0040, 0048, 0090)
_Avoid_: Site, Block, Facility
_บนหน้าเว็บ_: ตึก

```

- [ ] **Step 3: The code map**

In `docs/code-map.md`:
- In the pages table, after the `areas/` row, add:
  ```markdown
  | `buildings/` | Building setup: add, rename, on/off (facility-0090) | `BuildingsPage.tsx`, `BuildingDrawer.tsx`, `buildingRules.ts` (same counts as the server's `BuildingRules`) |
  ```
- In the route table, after the `/admin/areas/:areaId/edit` row, add:
  ```markdown
  | `/admin/buildings` | `pages/buildings/BuildingsPage.tsx` (lazy) | admin |
  ```
- In the Admin setup endpoint table, after the `GET /api/admin/buildings` row, add:
  ```markdown
  | GET | `/api/admin/buildings/all` | `AdminBuildingEndpoints.cs` | `adminSetupApi.ts` (`getAllBuildings`) | Every building, off ones too, with active Area and Supervisor counts: the buildings page and the D7/D8 picker. |
  | POST | `/api/admin/buildings` | `AdminBuildingEndpoints.cs` → `CreateAsync` | `pages/buildings/buildingsApi.ts` | Code set once, never reused (facility-0090). |
  | PUT | `/api/admin/buildings/{id}` | `AdminBuildingEndpoints.cs` → `RenameAsync` | `buildingsApi.ts` | Name only. |
  | POST | `/api/admin/buildings/{id}/deactivate` | `AdminBuildingEndpoints.cs` → `SetActiveAsync` | `buildingsApi.ts` | Refused while an active Area or Supervisor is left in it. |
  | POST | `/api/admin/buildings/{id}/activate` | `AdminBuildingEndpoints.cs` → `SetActiveAsync` | `buildingsApi.ts` | No condition; its Areas stay as they are. |
  ```
- If a row count such as "23 files" or the shared-components count changed because of this work, correct it. Otherwise leave it.

- [ ] **Step 4: The handoff**

In `docs/handoff.md`:
- **Last updated.** The first clause becomes: `**Buildings managed by Admin (facility-0090): done on branch \`feat/admin-buildings\`, committed, not pushed; the owner still checks it on the new page "ตึก", D7 and D8**`. Keep the earlier clauses after it.
- **Pending work.** The bullet `Buildings managed by Admin` starts with `**… done on branch \`feat/admin-buildings\` (facility-0090), committed, not pushed, not merged; the owner's check is owed.**`. Keep the list of decisions below it.
- **Task log.** Add a line in the `## Task log (docs)` style:
  - date and branch
  - the four commits
  - the backend unit and API counts (passed / skipped)
  - the frontend test count
  - main JS and CSS gzip
  - the six plan rulings in one line each

  Never write a password, phone number or real name.

- [ ] **Step 5: Commit**

```bash
git add docs/adr/facility-0045-admin-manages-areas-and-captures-sign-location.md \
  docs/adr/facility-0048-supervisor-assigned-to-building.md CONTEXT.md docs/code-map.md docs/handoff.md
git commit -m "docs: buildings managed by Admin in the ADRs, glossary, code map and handoff (facility-0090)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
