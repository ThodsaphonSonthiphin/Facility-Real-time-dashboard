# Building names on Admin pages, and an Area closes only once its Cleaners are moved — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix what the owner found testing `feat/admin-buildings` on 2026-10-27: Admin pages show a building's current name next to its code (so a rename shows), D8's picker is asked fresh on every open, and an Area cannot be turned off while active Cleaners still hold it.

**Architecture:** Backend: one new rule (`AreaRules.DeactivateProblem`) checked in the Area deactivate endpoint, and the Excel "ตึก" cell becomes "code · name" from one buildings lookup. Frontend: one shared module (`shared/api/buildingNames.ts`) gives a picker label from a building and a text from a code (via the cached `GET /api/admin/buildings/all`); every Admin page that prints a building uses it. Phones and printed signs keep the code.

**Tech Stack:** .NET 10 Minimal API, EF Core 10, xUnit, ClosedXML; React 19 + RTK Query, Vitest + Testing Library, Tailwind.

**Spec:** `docs/adr/facility-0090-admin-manages-buildings.md` (decision 3: "รายงานแสดงชื่อปัจจุบัน", never built) and the owner's rulings of 2026-10-27, written up as `docs/adr/facility-0091-building-names-and-area-close.md` in Task 6. The rulings:

1. Every Admin page shows a building as `<code> · <current name>` (pickers: `A · อาคารผลิต 1`; text: `ตึก A · อาคารผลิต 1`), the Excel export's "ตึก" cell too. The Cleaner and Supervisor phone pages and the printed signs keep the code alone (`ตึก A`), because the paper sign cannot follow a rename.
2. An Area cannot be turned off while an active Cleaner holds it (like the building rule of facility-0090 decision 4, and Workday's "move workers out before inactivating"). The refusal names them. The Admin takes them out in the Area form, or moves them in D6, first.

## Global Constraints

- Branch: keep working on `feat/admin-buildings` (not pushed, no PR). Do not push, open a PR or merge.
- Picker label format: `${code} · ${name}`; an off building in D8 adds ` (ปิดใช้งาน)`.
- Text format: `ตึก ${code} · ${name}`; while the names are loading, or for an unknown code, just `ตึก ${code}`.
- Area refusal text (server and page must match): `ปิดพื้นที่ <code> ไม่ได้ ยังมีแม่บ้านประจำ <names joined by ", "> ย้ายแม่บ้านออกก่อน`; the page's hint: `ยังมีแม่บ้านประจำ <names> เอาแม่บ้านออกจากกะในฟอร์มด้านบน หรือย้ายไปพื้นที่อื่นที่หน้าบัญชีผู้ใช้ก่อน จึงจะปิดพื้นที่ได้`.
- Do not touch `pages/print-signs`, `pages/scan`, `pages/inspect`, `pages/check-in`, `pages/my-work`, `pages/my-building`, `shared/components/BlockedScanScreen.tsx`.
- No new npm or NuGet package. No database migration.
- Comments and naming follow the surrounding code: short `/** */` and `///` lines that cite the ADR decision.
- Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. The husky pre-commit runs frontend lint, tsc and tests; never use `--no-verify`.
- Test commands: backend `dotnet test backend/tests/FacilityRealtime.UnitTests` and `dotnet test backend/tests/FacilityRealtime.ApiTests` (add `--filter "FullyQualifiedName~<Class>"` for one class); frontend `cd frontend && npx vitest run <path>`, whole suite `npm test`, plus `npx tsc -b` and `npm run lint`.

---

### Task 1: The server refuses to turn off an Area that active Cleaners hold

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Areas/AreaRules.cs`
- Create: `backend/tests/FacilityRealtime.UnitTests/AreaRulesTests.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs:173-205` (`SetActiveAsync`)
- Modify: `backend/tests/FacilityRealtime.ApiTests/Admin/AreaAdminEndpointTests.cs` (new tests; `A_deactivated_area_leaves_the_dashboard_and_the_scan_page_until_reactivated` at :403)
- Modify: `backend/tests/FacilityRealtime.ApiTests/Admin/AccountEditEndpointTests.cs:194-221`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Admin/SignAdminEndpointTests.cs:175-184`

**Interfaces:**
- Produces: `AreaRules.DeactivateProblem(string code, IReadOnlyList<string> cleanerNames) : string?` (null = may turn off). `POST /api/admin/areas/{id}/deactivate` answers 409 with that message.

- [ ] **Step 1: Write the failing unit test**

`backend/tests/FacilityRealtime.UnitTests/AreaRulesTests.cs`:

```csharp
using FacilityRealtime.Application.Areas;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0091 decision 2: an Area is turned off only once no active Cleaner holds it.</summary>
public class AreaRulesTests
{
    [Fact]
    public void An_area_nobody_holds_may_be_turned_off() => Assert.Null(AreaRules.DeactivateProblem("AR01", []));

    [Fact]
    public void An_area_its_cleaners_hold_names_them() =>
        Assert.Equal(
            "ปิดพื้นที่ AR01 ไม่ได้ ยังมีแม่บ้านประจำ สมชาย ใจดี, สมหญิง รักสะอาด ย้ายแม่บ้านออกก่อน",
            AreaRules.DeactivateProblem("AR01", ["สมชาย ใจดี", "สมหญิง รักสะอาด"]));
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AreaRulesTests"`
Expected: build FAIL, `The type or namespace name 'Areas' does not exist in the namespace 'FacilityRealtime.Application'`.

- [ ] **Step 3: Write the rule**

`backend/src/FacilityRealtime.Application/Areas/AreaRules.cs`:

```csharp
namespace FacilityRealtime.Application.Areas;

/// <summary>The answers when something is in the way of an Area change (facility-0091).</summary>
public static class AreaRules
{
    /// <summary>Decision 2: null when the Area may be turned off, else the active Cleaners who still hold it.</summary>
    public static string? DeactivateProblem(string code, IReadOnlyList<string> cleanerNames) =>
        cleanerNames.Count == 0
            ? null
            : $"ปิดพื้นที่ {code} ไม่ได้ ยังมีแม่บ้านประจำ {string.Join(", ", cleanerNames)} ย้ายแม่บ้านออกก่อน";
}
```

- [ ] **Step 4: Run the unit test to verify it passes**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AreaRulesTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Write the failing API tests**

In `backend/tests/FacilityRealtime.ApiTests/Admin/AreaAdminEndpointTests.cs`, after `A_deactivated_area_leaves_the_dashboard_and_the_scan_page_until_reactivated`, add:

```csharp
    [Fact]
    public async Task An_area_its_cleaners_hold_stays_on_until_they_are_taken_off()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var refused = await admin.PostAsync($"{Areas}/{area1}/deactivate", null);
        var emptied = await admin.PutAsJsonAsync($"{Areas}/{area1}", await Ar01FormAsync(factory, day: null, night: null));
        var off = await admin.PostAsync($"{Areas}/{area1}/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(
            "ปิดพื้นที่ AR01 ไม่ได้ ยังมีแม่บ้านประจำ สมชาย ใจดี, สมหญิง รักสะอาด ย้ายแม่บ้านออกก่อน",
            await AdminSetupApi.MessageAsync(refused));
        Assert.Equal(HttpStatusCode.OK, emptied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.Equal(new[] { "AREA_UPDATE", "AREA_DEACTIVATE" }, (await AdminSetupApi.AuditAsync(factory)).Select(a => a.Action));
    }

    [Fact]
    public async Task A_deactivated_cleaner_does_not_keep_an_area_on()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "E1003")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var off = await admin.PostAsync($"{Areas}/{await AdminSetupApi.AreaIdAsync(factory, "AR02")}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
    }
```

(Seed: AR01 is held by E1001 สมชาย ใจดี on Day and E1002 สมหญิง รักสะอาด on Night; AR02 by E1003 on Day.)

- [ ] **Step 6: Run them to verify the first fails**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AreaAdminEndpointTests"`
Expected: `An_area_its_cleaners_hold_stays_on_until_they_are_taken_off` FAILS (expected Conflict, got OK); `A_deactivated_cleaner_does_not_keep_an_area_on` passes.

- [ ] **Step 7: Check the rule in the endpoint**

In `backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs`, add `using FacilityRealtime.Application.Areas;` with the other usings, and in `SetActiveAsync` insert right after the facility-0090 off-building block (after its closing `}` at :192) and before `if (area.IsActive != active)`:

```csharp
        // facility-0091 decision 2: an Area is turned off only once no active Cleaner holds it
        if (!active && area.IsActive)
        {
            var holders = await db.Users
                .Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.AreaId == area.Id)
                .OrderBy(u => u.Shift)
                .Select(u => u.DisplayName)
                .ToListAsync();
            if (AreaRules.DeactivateProblem(area.Code, holders) is { } problem)
            {
                return Conflict(problem);
            }
        }
```

- [ ] **Step 8: Run the class to verify both pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AreaAdminEndpointTests"`
Expected: the two new tests PASS; `A_deactivated_area_leaves_the_dashboard_and_the_scan_page_until_reactivated` now FAILS (AR02 is held by E1003).

- [ ] **Step 9: Fix the tests that turned off a held Area through the endpoint**

a) `AreaAdminEndpointTests.A_deactivated_area_leaves_the_dashboard_and_the_scan_page_until_reactivated`: after `var area2 = ...;` add:

```csharp
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "E1003")).AreaId = null; // facility-0091: nobody may hold it
            await db.SaveChangesAsync();
        });
```

b) `AccountEditEndpointTests.A_cleaner_whose_area_was_deactivated_can_be_renamed_and_keeps_the_area` (:194): an Area turned off with its Cleaner still on it now only exists in data from before facility-0091, so write it straight to the database. Replace the line

```csharp
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/areas/{ar02}/deactivate", null)).StatusCode);
```

with

```csharp
        // only data from before facility-0091 has a Cleaner on an Area that is off
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Id == ar02)).IsActive = false;
            await db.SaveChangesAsync();
        });
```

c) `AccountEditEndpointTests.Moving_a_cleaner_to_a_deactivated_area_is_refused` (:210): replace `await admin.PostAsync($"/api/admin/areas/{ar02}/deactivate", null);` with the same `factory.WithDbAsync` block as b) but without the comment line.

d) `SignAdminEndpointTests.A_sign_in_a_deactivated_area_gets_no_new_qr_token` (:175): replace `Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/areas/{areaId}/deactivate", null)).StatusCode);` with:

```csharp
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Id == areaId)).IsActive = false;
            await db.SaveChangesAsync();
        });
```

Add `using Microsoft.EntityFrameworkCore;` to any of these files that lacks it (`SingleAsync`).

- [ ] **Step 10: Run both test projects**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests && dotnet test backend/tests/FacilityRealtime.ApiTests`
Expected: all pass (unit 340; API 578 passed, 5 skipped). If another test fails with 409 on `/deactivate` of an Area, it turned off a held Area: clear the Cleaner's `AreaId` first as in a), and note the test name in the task report.

- [ ] **Step 11: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Areas/AreaRules.cs backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs backend/tests
git commit -m "feat(areas): an Area is turned off only once no active Cleaner holds it (facility-0091)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The Area form says who still holds the Area, and its off button waits

**Files:**
- Modify: `frontend/src/pages/areas/areaText.ts`
- Modify: `frontend/src/pages/areas/AreaActiveSection.tsx`
- Test: `frontend/src/pages/areas/AreaFormPage.test.tsx:132-160`

**Interfaces:**
- Consumes: Task 1's refusal (shown by the existing error toast if it still happens, e.g. a Cleaner added from another tab).
- Produces: `cleanersLeft(area: AreaSummary): string | null` in `areaText.ts`.

- [ ] **Step 1: Write the failing test, and give the existing off/on test an Area nobody holds**

In `frontend/src/pages/areas/AreaFormPage.test.tsx`, in `it('deactivates the Area and can turn it back on (no delete)'`, change the three `areaSummary({ isActive: ... })` calls to also pass `dayCleaner: undefined, nightCleaner: undefined`, e.g.

```tsx
      'GET /api/admin/areas/1': () => ({
        status: 200,
        body: areaDetail({ area: areaSummary({ isActive: active, dayCleaner: undefined, nightCleaner: undefined }) }),
      }),
```

(and the same for the `deactivate` and `activate` answers). Then add after that test:

```tsx
  it('keeps the Area on while Cleaners hold it, and names them (facility-0091)', async () => {
    serve();
    await renderApp('/admin/areas/1/edit');

    const button = await screen.findByRole('button', { name: 'ปิดใช้งานพื้นที่นี้' });
    expect(button).toBeDisabled();
    expect(
      screen.getByText(
        'ยังมีแม่บ้านประจำ แม่บ้านทดสอบ, แม่บ้านกะดึก เอาแม่บ้านออกจากกะในฟอร์มด้านบน หรือย้ายไปพื้นที่อื่นที่หน้าบัญชีผู้ใช้ก่อน จึงจะปิดพื้นที่ได้',
      ),
    ).toBeInTheDocument();
  });
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd frontend && npx vitest run src/pages/areas/AreaFormPage.test.tsx`
Expected: the new test FAILS (button enabled, text not found); the others pass.

- [ ] **Step 3: Add the rule text**

In `frontend/src/pages/areas/areaText.ts`, add at the end:

```ts
/** facility-0091 decision 2, as the server's AreaRules.DeactivateProblem names them; null when no Cleaner holds the Area. */
export function cleanersLeft(area: AreaSummary): string | null {
  const names = [area.dayCleaner, area.nightCleaner].flatMap((cleaner) => (cleaner ? [cleaner.displayName] : []));
  return names.length === 0 ? null : `ยังมีแม่บ้านประจำ ${names.join(', ')}`;
}
```

(`AreaSummary` carries active Cleaners only: `AdminSetupView.LoadAreasAsync` filters `u.IsActive`.)

- [ ] **Step 4: Use it in the section**

In `frontend/src/pages/areas/AreaActiveSection.tsx`:
- import: `import { cleanersLeft } from './areaText';`
- after `const activePoints = ...;` add `const left = area.isActive ? cleanersLeft(area) : null;`
- replace the block

```tsx
        {area.isActive ? (
          <p>พื้นที่และทุกป้ายหายจากหน้าสถานะจุดและสแกนไม่ได้ ประวัติยังอยู่ เปิดกลับได้ ไม่มีปุ่มลบ</p>
        ) : (
          <Note tone="warn">พื้นที่นี้ปิดใช้งานอยู่ ป้ายทุกป้ายสแกนไม่ได้</Note>
        )}
```

with

```tsx
        {!area.isActive && <Note tone="warn">พื้นที่นี้ปิดใช้งานอยู่ ป้ายทุกป้ายสแกนไม่ได้</Note>}
        {left && (
          <Note tone="warn">{`${left} เอาแม่บ้านออกจากกะในฟอร์มด้านบน หรือย้ายไปพื้นที่อื่นที่หน้าบัญชีผู้ใช้ก่อน จึงจะปิดพื้นที่ได้`}</Note>
        )}
        {area.isActive && !left && (
          <p>พื้นที่และทุกป้ายหายจากหน้าสถานะจุดและสแกนไม่ได้ ประวัติยังอยู่ เปิดกลับได้ ไม่มีปุ่มลบ</p>
        )}
```

- and the button's `disabled={isLoading}` becomes `disabled={isLoading || left !== null}`.

- [ ] **Step 5: Run the file, then the whole suite**

Run: `cd frontend && npx vitest run src/pages/areas/AreaFormPage.test.tsx && npm test`
Expected: PASS. (The D4 drawer renders the same section; no D4 test presses this button.)

- [ ] **Step 6: Commit**

```bash
git add frontend/src/pages/areas
git commit -m "feat(areas): the Area form names the Cleaners who still hold the Area, and waits to turn it off (facility-0091)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: One building label, used by every Admin building picker; D7/D8 ask fresh on every open

**Files:**
- Create: `frontend/src/shared/api/buildingNames.ts`
- Create: `frontend/src/shared/api/buildingNames.test.ts`
- Modify: `frontend/src/shared/components/BuildingSelect.tsx` (D7, D8)
- Modify: `frontend/src/pages/areas/AreaForm.tsx:97-101` (D10)
- Modify: `frontend/src/pages/accounts/CreateAccountForm.tsx:193-197`, `frontend/src/pages/accounts/EditAccountForm.tsx:118-122` (D6)
- Modify: `frontend/src/pages/attendance/BoardControls.tsx:89-93` (D2)
- Modify: `frontend/src/pages/dashboard/Filters.tsx:32-36` (D1)
- Test: `frontend/src/pages/export/ExportPage.test.tsx`, `frontend/src/pages/inspection-summary/InspectionSummaryPage.test.tsx`, `frontend/src/pages/areas/AreaFormPage.test.tsx`, `frontend/src/pages/accounts/AccountsPage.test.tsx`, `frontend/src/pages/attendance/AttendancePage.test.tsx`

**Interfaces:**
- Produces (in `shared/api/buildingNames.ts`):
  - `type BuildingNames = ReadonlyMap<string, string>` (code → current name)
  - `buildingOptionText(building: Pick<Building, 'code' | 'name'>): string` → `"A · ตึกทดลอง"`
  - `buildingText(code: string, names?: BuildingNames): string` → `"A · ตึกทดลอง"`, or `"A"` when the name is not known
  - `useBuildingNames(): BuildingNames` (reads `GET /api/admin/buildings/all`; asks again on mount when the last answer is over 30 s old)

- [ ] **Step 1: Write the failing unit test**

`frontend/src/shared/api/buildingNames.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { buildingOptionText, buildingText } from './buildingNames';

describe('building labels (facility-0091 decision 1)', () => {
  it('a picker shows the code and the current name', () => {
    expect(buildingOptionText({ code: 'A', name: 'อาคารผลิต 1' })).toBe('A · อาคารผลิต 1');
  });

  it('a code becomes code · name, or stays the code while the names load or when it is unknown', () => {
    const names = new Map([['A', 'อาคารผลิต 1']]);
    expect(buildingText('A', names)).toBe('A · อาคารผลิต 1');
    expect(buildingText('Z', names)).toBe('Z');
    expect(buildingText('A')).toBe('A');
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd frontend && npx vitest run src/shared/api/buildingNames.test.ts`
Expected: FAIL, `Failed to resolve import "./buildingNames"`.

- [ ] **Step 3: Write the module**

`frontend/src/shared/api/buildingNames.ts`:

```ts
import { useMemo } from 'react';
import { useGetAllBuildingsQuery, type Building } from './adminSetupApi';

/**
 * facility-0091 decision 1: Admin pages show a building as its code (set once, on signs and in Excel) and its current
 * name (the one people use, which the Admin may change). Phones and printed signs keep the code alone.
 */

/** Building code → current name, off buildings included. */
export type BuildingNames = ReadonlyMap<string, string>;

/** Asked again on mount once the last answer is over 30 s old: a rename shows, without one request per row. */
const namesFreshness = { refetchOnMountOrArgChange: 30 } as const;

/** A building picker's option: "A · อาคารผลิต 1". */
export function buildingOptionText(building: Pick<Building, 'code' | 'name'>): string {
  return `${building.code} · ${building.name}`;
}

/** The same from a code alone; just the code while the names load or when the code is unknown. */
export function buildingText(code: string, names?: BuildingNames): string {
  const name = names?.get(code);
  return name === undefined ? code : `${code} · ${name}`;
}

/** Every building's current name, from the list the buildings page and the report pickers share. */
export function useBuildingNames(): BuildingNames {
  const { data } = useGetAllBuildingsQuery(undefined, namesFreshness);
  return useMemo(() => new Map((data ?? []).map((building) => [building.code, building.name] as const)), [data]);
}
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd frontend && npx vitest run src/shared/api/buildingNames.test.ts`
Expected: PASS (2 tests).

- [ ] **Step 5: Write the failing D8 tests**

In `frontend/src/pages/export/ExportPage.test.tsx`:
- change the expected list of `it('the building picker lists every building, an off one marked (ปิดใช้งาน) (facility-0090)'` to

```tsx
      expect(within(picker).getAllByRole('option').map((option) => option.textContent)).toEqual([
        'ทุกตึก',
        'A · ตึกทดลอง',
        'B · ตึกข้างเคียง',
        'C · ตึกเก่า (ปิดใช้งาน)',
      ]),
```

- in `it('sends the typed range, the building and the sets in the file’s order'` (:75) change `selectOptions(..., 'B')` to `selectOptions(..., 'B · ตึกข้างเคียง')`.
- add after the picker test:

```tsx
  it('asks for the buildings on every open, so a rename or a building turned off shows (facility-0091)', async () => {
    let list = managedBuildings;
    serve({ 'GET /api/admin/buildings/all': () => ({ status: 200, body: list }) });
    const { user } = await renderApp('/admin/export');
    const options = () =>
      within(screen.getByRole('combobox', { name: 'ตึก' }))
        .getAllByRole('option')
        .map((option) => option.textContent);
    await waitFor(() => expect(options()).toContain('B · ตึกข้างเคียง'));

    list = managedBuildings.map((building) => (building.code === 'B' ? { ...building, name: 'อาคารใหม่', isActive: false } : building));
    await user.click(screen.getByRole('link', { name: 'ตึก' }));
    await screen.findByRole('heading', { level: 1 });
    await user.click(screen.getByRole('link', { name: 'ส่งออก Excel' }));

    await waitFor(() => expect(options()).toContain('B · อาคารใหม่ (ปิดใช้งาน)'));
  });
```

(If the buildings page's `h1` has another level or role, wait instead on any text the buildings page shows, e.g. `await screen.findByRole('table', { name: 'ตึก' })`.)

- [ ] **Step 6: Run it to verify it fails**

Run: `cd frontend && npx vitest run src/pages/export/ExportPage.test.tsx`
Expected: the three changed/new tests FAIL (options are `A`, `B`, `C (ปิดใช้งาน)`).

- [ ] **Step 7: Use the label in BuildingSelect, asked fresh on open**

`frontend/src/shared/components/BuildingSelect.tsx`:
- imports become:

```tsx
import { useGetAllBuildingsQuery } from '../api/adminSetupApi';
import { freshOnOpen } from '../api/baseApi';
import { buildingOptionText } from '../api/buildingNames';
import { Field, Select } from './Field';
```

- `const { data } = useGetAllBuildingsQuery();` becomes `const { data } = useGetAllBuildingsQuery(undefined, freshOnOpen);`
- the option text becomes `{building.isActive ? buildingOptionText(building) : `${buildingOptionText(building)} (ปิดใช้งาน)`}`
- the doc comment's first line becomes: `"ตึก" on the report pages (D7, D8): every building, or one, as code · current name (facility-0091), asked on every open.`

- [ ] **Step 8: Use the label in the other pickers**

Replace the option text in each (value attributes stay as they are):
- `pages/areas/AreaForm.tsx`: `{building.name}` → `{buildingOptionText(building)}`
- `pages/accounts/CreateAccountForm.tsx`: `{building.name}` → `{buildingOptionText(building)}`
- `pages/accounts/EditAccountForm.tsx`: `{building.name}` → `{buildingOptionText(building)}`
- `pages/attendance/BoardControls.tsx`: `{building.code}` → `{buildingOptionText(building)}`
- `pages/dashboard/Filters.tsx`: `{building.code}` → `{buildingOptionText(building)}` (its `value={building.code}` stays: the filter is kept by code)

Each file gets `import { buildingOptionText } from '../../shared/api/buildingNames';`, placed in the file's import order.

- [ ] **Step 9: Update the picker tests**

- `pages/inspection-summary/InspectionSummaryPage.test.tsx`: :110 `'B'` → `'B · ตึกข้างเคียง'`; :153 `toHaveDisplayValue('A')` → `toHaveDisplayValue('A · ตึกทดลอง')`; :218 `['ทุกตึก', 'A', 'B']` → `['ทุกตึก', 'A · ตึกทดลอง', 'B · ตึกข้างเคียง']`.
- `pages/areas/AreaFormPage.test.tsx`: :56, :125, :180 `'ตึกทดลอง'` → `'A · ตึกทดลอง'`.
- `pages/accounts/AccountsPage.test.tsx`: :145 `{ name: 'ตึกข้างเคียง' }` → `{ name: 'B · ตึกข้างเคียง' }`; :146 and :274 `'ตึกทดลอง'` → `'A · ตึกทดลอง'`; :150 `'ตึกข้างเคียง'` → `'B · ตึกข้างเคียง'`.
- `pages/attendance/AttendancePage.test.tsx`: :111 `'B'` → `'B · ตึกข้างเคียง'`.
- `pages/dashboard/DashboardFilters.test.tsx:54` picks by value `'B'`, which still matches: leave it.

- [ ] **Step 10: Run the suite, tsc and lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. A remaining failure that picks or lists a building option by its old text is fixed the same way (code `·` name from `test/adminFixtures.ts`).

- [ ] **Step 11: Commit**

```bash
git add frontend/src
git commit -m "feat(frontend): building pickers on Admin pages show code · current name; D7 and D8 ask on every open (facility-0091)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Admin page text shows the building's current name

**Files:**
- Modify: `frontend/src/pages/areas/areaText.ts` (`areaMeta`), `frontend/src/pages/areas/AreaList.tsx`, `frontend/src/pages/areas/AreaDetailPanel.tsx:76` (D4)
- Modify: `frontend/src/pages/accounts/accountRules.ts` (`placeText`, `areaOptionLabel`), `frontend/src/pages/accounts/AccountTable.tsx`, `frontend/src/pages/accounts/SecretForms.tsx`, `frontend/src/pages/accounts/CreateAccountForm.tsx:179`, `frontend/src/pages/accounts/EditAccountForm.tsx:103` (D6)
- Modify: `frontend/src/pages/attendance/AttendanceTable.tsx:53` (D2)
- Modify: `frontend/src/pages/dashboard/DashboardPage.tsx:61` (D1)
- Modify: `frontend/src/pages/alerts/EmptyAreas.tsx:89`, `frontend/src/pages/alerts/alertRules.ts` (`coverNotice`), `frontend/src/pages/alerts/AlertItem.tsx:142`, `frontend/src/pages/alerts/CoverForm.tsx:46` (D3)
- Modify: `frontend/src/pages/inspection-summary/summaryRules.ts` (`placeText`), `frontend/src/pages/inspection-summary/SummaryTable.tsx:47` (D7)
- Test: `AreasPage.test.tsx`, `AccountsPage.test.tsx`, `AttendancePage.test.tsx`, `DashboardPage.test.tsx`, `AlertCovers.test.tsx`, `InspectionSummaryPage.test.tsx`, `alertRules.test.ts`, `summaryRules.test.ts`

**Interfaces:**
- Consumes: Task 3's `BuildingNames`, `buildingText`, `useBuildingNames`.
- Produces: optional last parameter `names?: BuildingNames` on `areaMeta(area, names?)`, `placeText(account, names?)` (accounts), `areaOptionLabel(area, exceptUserId?, names?)`, `coverNotice(cover, names?)`, `placeText(row, names?)` (summary). Without `names` each returns what it returns today.

- [ ] **Step 1: Write the failing tests**

a) Unit tests. In `pages/alerts/alertRules.test.ts`, next to the existing `coverNotice` expectation (:105), add:

```ts
  it('names the building of a Supervisor cover with its current name when it is known (facility-0091)', () => {
    expect(
      coverNotice({ ...supervisorCover, personName: 'หัวหน้าทดสอบ' }, new Map([['B', 'ตึกข้างเคียง']])),
    ).toBe('มอบหมาย หัวหน้าทดสอบ ตรวจแทนตึก B · ตึกข้างเคียง แล้ว');
  });
```

where `supervisorCover` is the cover object the existing :105 expectation passes (reuse that same value; if it is inline, lift it to a `const supervisorCover` above both tests). In `pages/inspection-summary/summaryRules.test.ts`, next to :18, add:

```ts
  it('a known building shows its current name (facility-0091)', () => {
    expect(placeText(summaryRow(), new Map([['A', 'ตึกทดลอง']]))).toBe('A · ตึกทดลอง · กะเช้า');
  });
```

b) Page tests: add `'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),` to the default routes of `serve` in `AreasPage.test.tsx`, `AccountsPage.test.tsx`, `AttendancePage.test.tsx`, `DashboardPage.test.tsx` and `AlertCovers.test.tsx` (import `managedBuildings` from `../../test/adminFixtures`), then change the expected texts:
- `AreasPage.test.tsx` :39 `'ตึก A · 2 กะ · 2 จุด · ป้ายยังไม่ยืนยัน 1'` → `'ตึก A · ตึกทดลอง · 2 กะ · 2 จุด · ป้ายยังไม่ยืนยัน 1'`; :40 `'ตึก A · เฉพาะกะเช้า · ...'` → `'ตึก A · ตึกทดลอง · เฉพาะกะเช้า · 0 จุด · ป้ายยังไม่ยืนยัน 1'`; :44 `'พื้นที่ที่เลือก · ตึก A'` → `'พื้นที่ที่เลือก · ตึก A · ตึกทดลอง'`; :161 `'ตึก A · 2 กะ · ...'` → `'ตึก A · ตึกทดลอง · 2 กะ · 2 จุด · ป้ายยังไม่ยืนยัน 1 · ปิดใช้งาน'`.
- `AccountsPage.test.tsx` :45 `'ตึก A · กะเช้า'` → `'ตึก A · ตึกทดลอง · กะเช้า'`; :93 and :94 `'· ตึก A ·'` → `'· ตึก A · ตึกทดลอง ·'`.
- `AttendancePage.test.tsx` :54–:59: in each row text, `ตึก A` → `ตึก A · ตึกทดลอง` and `ตึก B` → `ตึก B · ตึกข้างเคียง`.
- `DashboardPage.test.tsx` :39 `'ตึก A · 4 จุด'` → `'ตึก A · ตึกทดลอง · 4 จุด'`.
- `AlertCovers.test.tsx` :144 `'มอบหมาย หัวหน้าทดสอบ ตรวจแทนตึก B แล้ว'` → `'มอบหมาย หัวหน้าทดสอบ ตรวจแทนตึก B · ตึกข้างเคียง แล้ว'`; and in the first test, after `expect(within(empty).getByText('ไม่มีแม่บ้านประจำ')).toBeInTheDocument();`, add:

```tsx
    expect(
      within(empty).getByText((_, element) => element?.tagName === 'SPAN' && element.textContent === 'AR12 Area สิบสอง · ตึก B · ตึกข้างเคียง'),
    ).toBeInTheDocument();
```

- `InspectionSummaryPage.test.tsx` (it already serves the list): :54 `'A · กะเช้า'` → `'A · ตึกทดลอง · กะเช้า'`; :56 `'A · กะดึก'` → `'A · ตึกทดลอง · กะดึก'`.

Page texts appear once the names arrive, so a `getByText` that runs right after the first `findBy…` may need to become `await screen.findByText(…)`.

- [ ] **Step 2: Run them to verify they fail**

Run: `cd frontend && npx vitest run src/pages/areas src/pages/accounts src/pages/attendance src/pages/dashboard src/pages/alerts src/pages/inspection-summary`
Expected: the changed and new expectations FAIL (the old code-only text is shown); the rest pass.

- [ ] **Step 3: Rule functions take the names**

- `pages/areas/areaText.ts`: `areaMeta(area: AreaSummary, names?: BuildingNames)`, first part `` `ตึก ${buildingText(area.buildingCode, names)}` ``.
- `pages/accounts/accountRules.ts`: `placeText(account: Account, names?: BuildingNames)`, Supervisor branch `` `ตึก ${account.buildingCode ? buildingText(account.buildingCode, names) : '-'} · ${shift}` ``; `areaOptionLabel(area: AreaSummary, exceptUserId?: number, names?: BuildingNames)`, part `` `ตึก ${buildingText(area.buildingCode, names)}` ``.
- `pages/alerts/alertRules.ts`: `coverNotice(cover: CoverAssignment, names?: BuildingNames)`, Supervisor branch `` `มอบหมาย ${cover.personName} ตรวจแทนตึก ${cover.buildingCode ? buildingText(cover.buildingCode, names) : ''} แล้ว` ``.
- `pages/inspection-summary/summaryRules.ts`: `placeText({ buildingCode, shift }: SummaryRow, names?: BuildingNames)` returns `` `${buildingText(buildingCode, names)} · ${shiftShortLabel[shift]}` ``.

Each imports `import { buildingText, type BuildingNames } from '../../shared/api/buildingNames';`.

- [ ] **Step 4: Components read the names and pass them**

In each component below, add `const names = useBuildingNames();` at the top of the component body (import `useBuildingNames` from `../../shared/api/buildingNames`) and pass `names`:
- `AreaList.tsx`: `areaMeta(area, names)`
- `AreaDetailPanel.tsx`: `` `พื้นที่ที่เลือก · ตึก ${buildingText(area.buildingCode, names)}` `` (also import `buildingText`)
- `AccountTable.tsx`: `placeText(account, names)`
- `SecretForms.tsx`: `placeText(account, names)` in the component that builds the `value: placeText(account)` line (:73)
- `CreateAccountForm.tsx`: `areaOptionLabel(option, undefined, names)`
- `EditAccountForm.tsx`: `areaOptionLabel(option, account.id, names)`
- `AttendanceTable.tsx`: `` `ตึก ${buildingText(row.buildingCode, names)}` ``
- `DashboardPage.tsx`, in `AreaSection`: `` `ตึก ${buildingText(group.buildingCode, names)} · ${group.points.length} จุด` ``
- `EmptyAreas.tsx`: `` {` · ตึก ${buildingText(row.buildingCode, names)}`} ``
- `AlertItem.tsx`: `coverNotice(made, names)`; `CoverForm.tsx`: `coverNotice(cover, names)`
- `SummaryTable.tsx`: `placeText(row, names)`

The hook may run once per row (e.g. one `AlertItem` per alert): all calls share one cache entry, and `namesFreshness` asks again only when it is over 30 s old.

- [ ] **Step 5: Run the suite, tsc and lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. Tests that do not serve `GET /api/admin/buildings/all` still see `ตึก A` (the fake answers 404 and `buildingText` falls back to the code): that is the fallback working, leave them.

- [ ] **Step 6: Commit**

```bash
git add frontend/src
git commit -m "feat(frontend): Admin pages show a building's current name next to its code (facility-0091)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The Excel "ตึก" cell is code · current name

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/ExportSheets.cs` (`Attendance`, `Submissions`, `Inspections`)
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminExportEndpoints.cs:66-95`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/ExportEndpointTests.cs`, `backend/tests/FacilityRealtime.ApiTests/Admin/InspectionExportTests.cs`

**Interfaces:**
- Produces: each `ExportSheets` method takes a last parameter `IReadOnlyDictionary<string, string> buildingNames` (code → name).

- [ ] **Step 1: Write the failing test and update the expected cells**

In `ExportEndpointTests.cs`, add after `Attendance_is_one_row_per_person_per_shift_with_source_and_gps` (add `using System.Net.Http.Json;` if missing):

```csharp
    [Fact]
    public async Task The_building_cell_shows_the_code_and_the_current_name()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddAttendanceAsync(factory, "E1001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        var a = await AdminSetupApi.BuildingIdAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/buildings/{a}", new { name = "อาคารผลิต 1" })).StatusCode);

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Attendance"]));

        Assert.Equal("A · อาคารผลิต 1", Rows(Assert.Single(workbook.Worksheets))[0][2]);
    }
```

Update the expected building cells (seed building A is named `ตึก A`; `TestData.AddBuildingAsync` names `ตึก <code>`):
- `ExportEndpointTests.cs:64`: `"A"` → `"A · ตึก A"`.
- `InspectionExportTests.cs:66, :69, :72, :75`: `"A"` → `"A · ตึก A"`; `:92` `"B"` → `"B · ตึก B"`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~ExportEndpointTests|FullyQualifiedName~InspectionExportTests"`
Expected: the new test and the updated ones FAIL (cell is `A`).

- [ ] **Step 3: Write the cell from the lookup**

In `ExportSheets.cs`:
- add the helper above `Round`:

```csharp
    /// <summary>facility-0091 decision 1: "A · อาคารผลิต 1", the building's name now; the code alone if it has none.</summary>
    private static string Building(string code, IReadOnlyDictionary<string, string> names) =>
        names.TryGetValue(code, out var name) ? $"{code} · {name}" : code;
```

- `Attendance(IReadOnlyList<AttendanceExportRow> events, IReadOnlyDictionary<string, string> buildingNames)`; cell `first.BuildingCode` → `Building(first.BuildingCode, buildingNames)`.
- `Submissions(..., int closesMinutesAfterShift, IReadOnlyDictionary<string, string> buildingNames)`; cells `s.BuildingCode` → `Building(s.BuildingCode, buildingNames)` and `point.BuildingCode` → `Building(point.BuildingCode, buildingNames)`.
- `Inspections(..., IReadOnlyList<FinishedRound> rounds, IReadOnlyDictionary<string, string> buildingNames)`; cells `i.BuildingCode` → `Building(i.BuildingCode, buildingNames)` and `place.BuildingCode` → `Building(place.BuildingCode, buildingNames)`.

Headers stay as they are.

In `AdminExportEndpoints.cs`, after `var now = clock.GetUtcNow().UtcDateTime;` add:

```csharp
        var buildingNames = await db.Buildings.AsNoTracking().ToDictionaryAsync(b => b.Code, b => b.Name);
```

and pass `buildingNames` as the last argument of the three `ExportSheets` calls.

- [ ] **Step 4: Run both test projects**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests && dotnet test backend/tests/FacilityRealtime.ApiTests`
Expected: all pass (API 579 passed, 5 skipped).

- [ ] **Step 5: Commit**

```bash
git add backend
git commit -m "feat(export): the Excel building cell is code · current name (facility-0091)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Record the decisions, rebuild, and hand the owner the re-check

**Files:**
- Create: `docs/adr/facility-0091-building-names-and-area-close.md`
- Modify: `docs/adr/facility-0090-admin-manages-buildings.md` (one amendment line), `docs/adr/facility-0045-admin-manages-areas-and-captures-sign-location.md` (one amendment line)
- Modify: `CONTEXT.md` (the **Building** and **Area** entries), `docs/code-map.md`, `docs/handoff.md`

- [ ] **Step 1: Write ADR facility-0091** (Thai, in the shape of facility-0090: a mermaid choice graph, date, source, related, Context & Decision, Consequences)

Content to carry:
- Source: the owner's test of `feat/admin-buildings` on 2026-10-27: D4 kept "ตึก A" after a rename; D8 showed only codes; closing building C made the Admin close the Supervisor, but the Cleaner of AR03 stayed on an Area that was off.
- Why the code was shown: before facility-0090 buildings came only from the seed and never changed name, so every page printed `ตึก <code>`. facility-0090 decision 3 said reports show the current name, but its plan had no task for it.
- Decision 1 (chosen; rejected: name only — names may repeat (0090 decision 3), so two buildings could look the same; every page including phones — the printed sign cannot follow a rename): Admin pages and the Excel cell show `<code> · <current name>`; phones and printed signs keep the code.
- Decision 2 (chosen, like 0090 decision 4 and Workday's rule that workers move out before an organization is inactivated; rejected: free the Cleaners automatically — one press empties the shift slots and they are lost when the Area is turned on again; keep as is — a Cleaner keeps an Area that is off): an Area is turned off only once no active Cleaner holds it; the refusal names them; the Area form disables its button with the same names.
- Consequences: `AreaRules.DeactivateProblem`; `shared/api/buildingNames.ts`; D7/D8 picker asks on every open; data written before this ADR may still hold a Cleaner on an Area that is off (only test databases; no migration); Excel column count unchanged.

- [ ] **Step 2: Amendment lines and glossary**

- facility-0090, at the end: `> **แก้ไข 2026-10-27 (facility-0091):** ข้อ 3 ทำจริงแล้ว หน้า Admin และ Excel แสดงตึกเป็น "รหัส · ชื่อปัจจุบัน" หน้ามือถือและป้ายพิมพ์คงรหัส`
- facility-0045, at the end: `> **แก้ไข 2026-10-27 (facility-0091):** ปิดพื้นที่ได้เมื่อไม่มีแม่บ้านที่เปิดใช้งานประจำอยู่ ต้องเอาแม่บ้านออกจากกะหรือย้ายไปพื้นที่อื่นก่อน`
- `CONTEXT.md` **Building**: add `หน้า Admin แสดงเป็น รหัส · ชื่อปัจจุบัน หน้ามือถือและป้ายแสดงรหัส` and `0091` to its ADR list. **Area**: add `ปิดใช้งานได้เมื่อไม่มีแม่บ้านประจำที่เปิดใช้งานอยู่` and `0091`.

- [ ] **Step 3: Code map and handoff**

- `docs/code-map.md`: add `shared/api/buildingNames.ts` to the shared API rows, `Application/Areas/AreaRules.cs` to the Application rows, and note on the `POST /api/admin/areas/{id}/deactivate` row: `Refused while an active Cleaner holds the Area (facility-0091).` Fix any line references this branch moved.
- `docs/handoff.md`: in `## Next session: test the buildings branch`, record the owner's 2026-10-27 results (9, 6ง.7, 6ง.8 passed; 6ง.6 passes by design: the form does not offer an off building) and the fixes of this plan; update the test counts; replace "What the owner checks" with the re-check list of Step 5.

- [ ] **Step 4: Full check and rebuild**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests && dotnet test backend/tests/FacilityRealtime.ApiTests && cd frontend && npm test && npx tsc -b && npm run lint && npm run build:api`
Expected: all pass; the build writes `backend/src/FacilityRealtime.Api/wwwroot`. Restart the test API on `facility_buildings_test` (handoff `## Sharing a test build`, `--no-launch-profile`, port 5001) so the owner's tunnel serves the new build.

- [ ] **Step 5: Commit, then give the owner the re-check (in Thai, by popup for the go/no-go)**

```bash
git add docs CONTEXT.md
git commit -m "docs: building names on Admin pages and the Area close rule (facility-0091)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Re-check for the owner (desktop 1280 px):
1. Rename building A on "ตึก", then open "จัดการพื้นที่และป้าย": the list line and "พื้นที่ที่เลือก · ตึก A · <ชื่อใหม่>" show the new name.
2. D8 "ส่งออก Excel": the picker shows `A · <ชื่อ>` and the off building as `C · <ชื่อ> (ปิดใช้งาน)`; export one sheet and see `A · <ชื่อ>` in the "ตึก" column.
3. D7, D6, D2, D1: the building pickers and texts show `A · <ชื่อ>`.
4. Open the Area form of AR01 (it has Cleaners): "ปิดใช้งานพื้นที่นี้" is disabled and names the Cleaners. Take them out in the form, save, and the button works.

Do not push, open a PR or merge without the owner's explicit OK.
