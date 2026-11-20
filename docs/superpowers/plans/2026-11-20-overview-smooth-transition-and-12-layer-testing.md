# Overview Smooth Transition & 12-Category Verification Suite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `sp-subagent-driven-development` (recommended) or `sp-executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate the full-page reload flash on `/admin/overview` when switching groups, pages, or filters, and deliver a complete, repeatable 12-category automated test matrix with strict pass/fail gates.

**Architecture:** Track 1 retains the last valid `OverviewAreaList` snapshot in `useOverviewPage` during in-flight context switches (`!query.error && !isUncanonicalSlot`) while tinting the `AreaResults` background (`aria-busy`, inline `"กำลังอัปเดต..."`) and strictly clearing stale rows on error. Track 2 runs the 7 existing test suites and adds 5 new automated suites (Contract, Visual Regression, Axe Accessibility, Performance/Load, and Chromium+WebKit Compatibility) using the existing repo stack (`.NET 10`, Vitest, Playwright, Node.js, plus `@axe-core/playwright`).

**Tech Stack:** React 19, TypeScript 6, RTK Query, Tailwind CSS 4 (`@theme` tokens only), Vitest 5, Playwright 1.64 (`chromium` + `webkit`), `@axe-core/playwright`, .NET 10 xUnit, MySQL 8 (isolated test databases only).

**Spec:** `docs/superpowers/specs/2026-11-20-overview-smooth-transition-and-12-layer-testing-design.md`

## Global Constraints

- **Stage: development (`AGENTS.md`):** No backward compatibility shims; update in place and delete unused code.
- **UI & Iconography (`AGENTS.md`, `MASTER.md`):** Zero emojis in UI or docs; `lucide-react` exclusively with `aria-hidden="true"`; Tailwind `@theme` tokens only (`frontend/src/index.css`); main JS gzip budget `<= 200 kB`.
- **Database safety:** Never touch the owner's `facility_dashboard` database; all Integration, E2E, Visual, A11y, Load, and Compatibility runs must use isolated throwaway databases (`scripts/e2e.sh` or `FACILITY_MYSQL_TEST`).
- **Session handoff:** Update `docs/handoff.md` after completion without secrets or real personnel data.
- **Confirmed UI Mockup:** `overview-transition-mockup.html` (Option A — charts + previous table stay mounted with `opacity-50` + `"กำลังอัปเดต..."`; error clears stale rows and shows Retry).

---

## Execution record

Completed on `codex/overview-transition-verification` on 2026-11-20 (Asia/Bangkok). See the [verified scorecard](../../handoff.md#overview-transitions-and-12-category-verification).

- Rechecked the red Overview case in an isolated source copy with snapshot retention disabled. The test failed on the full-page loading indicator, then passed on the implemented branch.
- Contract checks use the application's login response, seeded non-empty rows, Overview detail, and both response modes of the attendance sign endpoint.
- Visual checks cover 11 macOS Chromium baselines. A fixed browser clock and typed responses stabilize live views for screenshot comparison. Historical Overview and Attendance use the seeded medium API.
- The load npm script starts an isolated large dataset before running the concurrent Node benchmark. All thresholds remain as specified.
- Carried forward the in-progress attendance, dashboard, and seed prerequisites in a separate local commit. The six-action contract and browser attendance flows use that implementation.
- The follow-up uses a background tint for the loading state to preserve text and control contrast. It also waits for a fresh response on cached switches and keeps the label and links tied to the visible rows.

### Task 1: Smooth `/admin/overview` transition without full-page unmount (`facility-0233`)

**Files:**
- Modify: `frontend/src/pages/overview/AreaResults/type.ts:34-48`
- Modify: `frontend/src/pages/overview/AreaResults/AreaResults.tsx:25-90`
- Modify: `frontend/src/pages/overview/OverviewPage/useOverviewPage.ts:595-706,787-847`
- Modify: `frontend/src/pages/overview/OverviewPage/OverviewPage.tsx:103-156`
- Test: `frontend/src/pages/overview/OverviewPage.test.tsx`
- Test: `frontend/e2e/desktop/overview.spec.ts:70-92`

**Interfaces:**
- Consumes: `useGetOverviewAreasQuery` from `frontend/src/shared/overview/overviewApi.ts`
- Produces: `AreaResultsProps.updating?: boolean` consumed by `AreaResults.tsx` and populated by `buildResults` in `useOverviewPage.ts`.

- [x] **Step 1: Write the failing unit test in `frontend/src/pages/overview/OverviewPage.test.tsx`**

Add a test proving that switching groups keeps charts and previous rows mounted (no full-page `กำลังโหลดภาพรวม...`) and marks `AreaResults` with `กำลังอัปเดต...` and `aria-busy="true"` until the new group resolves:

```tsx
  it('keeps charts and previous rows mounted while switching groups instead of flashing full-page loading (facility-0233)', async () => {
    const base = overviewFixture();
    let resolveEntered!: (res: FakeResponse) => void;
    const pendingEntered = new Promise<FakeResponse>((resolve) => {
      resolveEntered = resolve;
    });

    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': (req) => {
        const params = new URLSearchParams(req.search);
        if (params.get('group') === 'entered') {
          return pendingEntered;
        }
        return { status: 200, body: base };
      },
    });

    const { user } = await renderApp('/admin/overview?date=2026-11-04&shift=Day&group=notEntered&page=1');
    await screen.findByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' });

    await user.click(screen.getByRole('button', { name: 'ลงเวลาแล้ว 90 พื้นที่' }));

    expect(screen.queryByText('กำลังโหลดภาพรวม...')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'สรุปผลของกะ' })).toBeVisible();
    const results = screen.getByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' });
    expect(within(results).getByText('กำลังอัปเดต...')).toBeVisible();
    expect(results.querySelector('[aria-busy="true"]')).not.toBeNull();

    await act(async () => {
      resolveEntered({
        status: 200,
        body: overviewFixture({
          group: 'entered',
          total: base.summary.groups.entered,
          rows: [makeAreaRow(301, { code: 'AR301', name: 'พื้นที่สลับกลุ่มสำเร็จ' })],
        }),
      });
    });

    const nextResults = await screen.findByRole('region', { name: 'พื้นที่ที่ลงเวลาแล้ว' });
    expect(within(nextResults).getByText(/AR301/)).toBeVisible();
    expect(within(nextResults).queryByText('กำลังอัปเดต...')).not.toBeInTheDocument();
  });
```

- [x] **Step 2: Run the targeted test to verify it fails**

Run: `NODE_ENV=test npm --prefix frontend test -- src/pages/overview/OverviewPage.test.tsx`
Expected: FAIL — `กำลังโหลดภาพรวม...` is rendered and `สรุปผลของกะ` unmounts during group switch.

- [x] **Step 3: Add `updating?: boolean` to `AreaResults/type.ts` and `AreaResults.tsx`**

In `frontend/src/pages/overview/AreaResults/type.ts`:
```ts
export interface AreaResultsProps {
  title: string;
  contextText: string;
  countText: string;
  availabilityWarning?: string;
  updating?: boolean;
  rows: readonly AreaResultRowView[];
  emptyState?: AreaResultsEmptyState;
  pagination: {
    page: number;
    hasPrevious: boolean;
    hasNext: boolean;
    disabled: boolean;
  };
  onPage: (page: number) => void;
}
```

In `frontend/src/pages/overview/AreaResults/AreaResults.tsx`:
- Accept `updating = false` in props.
- In header beside `{contextText}`, render:
```tsx
        <div className="flex flex-wrap items-center gap-2 text-xs text-text-2">
          {updating && (
            <span role="status" className="font-semibold text-accent">
              กำลังอัปเดต...
            </span>
          )}
          <span>{contextText}</span>
        </div>
```
- Wrap `<Table ...>` in `<div aria-busy={updating || undefined} className={updating ? 'rounded-md bg-surface-2 motion-safe:transition-colors duration-150' : 'motion-safe:transition-colors duration-150'}>`.

- [x] **Step 4: Retain last valid response during in-flight switches in `useOverviewPage.ts` and update `OverviewPage.tsx`**

In `frontend/src/pages/overview/OverviewPage/useOverviewPage.ts`:
- Track `retainedResponse` (`useState<OverviewAreaList | undefined>(undefined)`):
  - When `response` is defined, update `retainedResponse` to `response`.
  - When `!contextResult.ok || query.error || isUncanonicalSlot`, clear `retainedResponse` to `undefined` (so invalid URLs, failed new-context switches, and default-slot rollovers never show stale data).
- Compute `const displayResponse = response ?? (query.isFetching && !query.error && !isUncanonicalSlot ? retainedResponse : undefined);`
- Compute `const switching = displayResponse !== undefined && response === undefined && query.isFetching && !isCanonicalizing;`
- Set `loading = contextResult.ok && displayResponse === undefined && (query.isFetching || (awaitingFreshCache && !query.error));`
- Set `refreshing = contextResult.ok && response !== undefined && query.isFetching && !isCanonicalizing;` (same-context background refresh only).
- Build `panels`, `followUp`, `buildings`, `history`, `results` from `displayResponse`, using `selection.group` for the active selected highlight so the clicked bar/pill highlights immediately, and pass `updating: switching || refreshing` into `buildResults`.

In `frontend/src/pages/overview/OverviewPage/OverviewPage.tsx`:
- Replace `model.response` checks for rendering the body with `model.response` (returned as `displayResponse` from `useOverviewPage`), while keeping the `model.response && model.error` warning banner tied to actual same-context refresh errors (`!switching`).

- [x] **Step 5: Update stale copy assertions in `frontend/e2e/desktop/overview.spec.ts`**

In `frontend/e2e/desktop/overview.spec.ts`:
- Line 71: update `'ประวัติสำหรับอ่านข้อมูล'` -> `'ดูย้อนหลังเท่านั้น'`
- Line 90: update `'พื้นที่ที่มีรอบไม่ได้ทำ'` -> `'มีรอบไม่ได้ทำ'`

- [x] **Step 6: Run frontend lint, Overview tests, and full Vitest suite**

Run: `npm --prefix frontend run lint && NODE_ENV=test npm --prefix frontend test -- src/pages/overview src/shared/overview && npm --prefix frontend run build`
Expected: PASS (0 lint errors, all Overview + shared overview tests green, build <= 200 kB gzip).

- [x] **Step 7: Commit Task 1**

```bash
git add frontend/src/pages/overview frontend/e2e/desktop/overview.spec.ts
git commit -m "fix(overview): keep charts and table mounted during group and filter transitions (facility-0233)"
```

---

### Task 2: Category 5 — Backend-to-Frontend API Contract Tests (`facility-0234`)

**Files:**
- Create: `backend/tests/JanitorialManagementDashboard.ApiTests/Contract/ApiContractTests.cs`
- Create: `frontend/src/shared/api/contract.test.ts`

**Interfaces:**
- Consumes: `JanitorialApiFactory`, `AuthApi`, `ApiClient` from `backend/tests/JanitorialManagementDashboard.ApiTests/Infrastructure/`
- Consumes: `overviewFixture`, `overviewDetailFixture` from `frontend/src/test/overviewFixtures.ts`, `myWork`, `point` from `frontend/src/test/cleanerFixtures.ts`
- Produces: Automated xUnit + Vitest contract verification guarding exact camelCase JSON property names and enum strings across `/api/auth/me`, `/api/admin/overview/areas`, `/api/service-points`, `/api/my-work`.

- [x] **Step 1: Write `backend/tests/JanitorialManagementDashboard.ApiTests/Contract/ApiContractTests.cs`**

Verify the serialized JSON payload shapes returned by the real API endpoints contain every required camelCase property and valid enum string consumed by `frontend/src/shared/api/types.ts` and `frontend/src/shared/overview/types.ts`:

```csharp
using System.Net;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Infrastructure;

namespace JanitorialManagementDashboard.ApiTests.Contract;

public sealed class ApiContractTests
{
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    private static void AssertHasProperties(JsonElement obj, params string[] names)
    {
        Assert.Equal(JsonValueKind.Object, obj.ValueKind);
        foreach (var name in names)
        {
            Assert.True(obj.TryGetProperty(name, out _), $"Missing contract property '{name}' in {obj}");
        }
    }

    [Fact]
    public async Task Auth_me_matches_frontend_User_contract()
    {
        using var factory = new JanitorialApiFactory();
        var client = await AuthApi.AdminClientAsync(factory);

        var body = await ReadJsonAsync(await client.GetAsync("/api/auth/me"));

        AssertHasProperties(body, "id", "username", "fullName", "role");
        Assert.Contains(body.GetProperty("role").GetString(), new[] { "admin", "supervisor", "cleaner" });
    }

    [Fact]
    public async Task Overview_areas_matches_frontend_OverviewAreaList_contract()
    {
        using var factory = new JanitorialApiFactory();
        var client = await AuthApi.AdminClientAsync(factory);

        var body = await ReadJsonAsync(await client.GetAsync("/api/admin/overview/areas"));

        AssertHasProperties(body, "summary", "group", "page", "pageSize", "total", "rows");
        var summary = body.GetProperty("summary");
        AssertHasProperties(summary, "context", "availability", "entry", "submission", "inspection", "groups", "buildings");
        AssertHasProperties(summary.GetProperty("context"), "shiftDate", "shift", "phase", "readAtUtc", "evaluationUtc", "isClockCurrent", "range");
        AssertHasProperties(summary.GetProperty("entry"), "numerator", "denominator");
        AssertHasProperties(summary.GetProperty("groups"),
            "entered", "covered", "unassigned", "notEntered",
            "submitted", "incomplete", "inspected", "pendingInspection",
            "noSubmission", "rework", "issue", "overdue",
            "upcoming", "noRounds", "missedCleaning", "missedInspection");
    }

    [Fact]
    public async Task Service_points_matches_frontend_PointCard_contract()
    {
        using var factory = new JanitorialApiFactory();
        var client = await AuthApi.AdminClientAsync(factory);

        var body = await ReadJsonAsync(await client.GetAsync("/api/service-points"));

        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        if (body.GetArrayLength() > 0)
        {
            AssertHasProperties(body[0], "id", "code", "name", "areaId", "areaCode", "areaName", "buildingCode", "status");
        }
    }
}
```

- [x] **Step 2: Write `frontend/src/shared/api/contract.test.ts`**

Validate that frontend overview/work fixtures and runtime enum tables (`pointStatusLabel`, `eventLabel`, `shiftLabel`, `OVERVIEW_GROUPS`) stay 1:1 with the backend contract keys:

```ts
import { describe, expect, it } from 'vitest';
import { OVERVIEW_GROUPS } from '../overview/overviewContext';
import { attendanceEvents, eventLabel, pointStatusLabel, shiftLabel } from '../utils/labels';
import { overviewFixture } from '../../test/overviewFixtures';

describe('Frontend/Backend API contract invariants', () => {
  it('maps all 16 OverviewGroup keys in both fixture summary and URL parser', () => {
    const fixture = overviewFixture();
    const fixtureKeys = Object.keys(fixture.summary.groups).sort();
    expect(fixtureKeys).toEqual([...OVERVIEW_GROUPS].sort());
  });

  it('covers all six attendance events and both shifts', () => {
    expect(attendanceEvents).toEqual([
      'ShiftIn', 'Break1Out', 'Break1In', 'Break2Out', 'Break2In', 'ShiftOut',
    ]);
    for (const event of attendanceEvents) {
      expect(eventLabel[event]).toBeTruthy();
    }
    expect(Object.keys(shiftLabel).sort()).toEqual(['Day', 'Night']);
    expect(Object.keys(pointStatusLabel).sort()).toEqual([
      'BeforeFirstRound', 'NotYetDone', 'OffHours', 'Overdue', 'Passed', 'PendingInspection', 'Rework',
    ]);
  });
});
```

- [x] **Step 3: Run both contract test suites**

Run: `dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter "FullyQualifiedName~ApiContractTests" --verbosity minimal && NODE_ENV=test npm --prefix frontend test -- src/shared/api/contract.test.ts`
Expected: PASS (3/3 backend contract tests + 2/2 frontend contract tests).

- [x] **Step 4: Commit Task 2**

```bash
git add backend/tests/JanitorialManagementDashboard.ApiTests/Contract/ApiContractTests.cs frontend/src/shared/api/contract.test.ts
git commit -m "test(contract): add Backend-to-Frontend DTO contract verification (facility-0234)"
```

---

### Task 3: Category 9 — Automated Accessibility Audit with `@axe-core/playwright` (`facility-0234`)

**Files:**
- Modify: `frontend/package.json` (add `@axe-core/playwright` devDependency)
- Create: `frontend/e2e/desktop/accessibility.spec.ts`
- Create: `frontend/e2e/phone/accessibility.spec.ts`

**Interfaces:**
- Consumes: `loginAsAdmin`, `loginAsEmployee` from `frontend/e2e/support/login.ts`, `cleaner`, `supervisor` from `frontend/e2e/support/seed.ts`
- Produces: Strict zero-critical/serious WCAG 2.2 AA Playwright audit specs for desktop Admin pages and phone Cleaner/Supervisor pages.

- [x] **Step 1: Install `@axe-core/playwright` as a frontend devDependency**

Run: `npm --prefix frontend install --save-dev @axe-core/playwright`
Expected: `@axe-core/playwright` added to `frontend/package.json` and `frontend/package-lock.json`.

- [x] **Step 2: Create `frontend/e2e/desktop/accessibility.spec.ts`**

```ts
import AxeBuilder from '@axe-core/playwright';
import { loginAsAdmin } from '../support/login.ts';
import { expect, test } from '../support/test.ts';

async function expectNoSeriousViolations(page: import('@playwright/test').Page) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag22aa'])
    .analyze();
  const blocking = results.violations.filter((v) => v.impact === 'critical' || v.impact === 'serious');
  expect(blocking, JSON.stringify(blocking, null, 2)).toEqual([]);
}

test('admin login and core admin pages have zero critical or serious WCAG violations', async ({ page }) => {
  await page.goto('/login/admin');
  await expect(page.getByRole('heading', { name: 'แอดมินเข้าสู่ระบบ' })).toBeVisible();
  await expectNoSeriousViolations(page);

  await loginAsAdmin(page);
  for (const path of ['/admin/overview', '/admin/schedules', '/admin/attendance', '/admin/areas']) {
    await page.goto(path);
    await expect(page.getByRole('main')).toBeVisible();
    await expectNoSeriousViolations(page);
  }
});

test('admin navigation and drawers support Tab and Escape keyboard interaction', async ({ page }) => {
  await loginAsAdmin(page);
  await page.goto('/admin/buildings');
  await page.getByRole('button', { name: 'เพิ่มตึก' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
});
```

- [x] **Step 3: Create `frontend/e2e/phone/accessibility.spec.ts`**

```ts
import AxeBuilder from '@axe-core/playwright';
import { loginAsEmployee } from '../support/login.ts';
import { cleaner, supervisor } from '../support/seed.ts';
import { expect, test } from '../support/test.ts';

async function expectNoSeriousViolations(page: import('@playwright/test').Page) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag22aa'])
    .analyze();
  const blocking = results.violations.filter((v) => v.impact === 'critical' || v.impact === 'serious');
  expect(blocking, JSON.stringify(blocking, null, 2)).toEqual([]);
}

test('phone login, cleaner my-work, and supervisor my-building have zero critical or serious WCAG violations', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'เข้าสู่ระบบ' })).toBeVisible();
  await expectNoSeriousViolations(page);

  await loginAsEmployee(page, cleaner);
  await expect(page.getByRole('heading', { name: 'งานของฉัน' })).toBeVisible();
  await expectNoSeriousViolations(page);

  await page.goto('/my-account');
  await page.getByRole('button', { name: 'ออกจากระบบ' }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'ออกจากระบบ' }).click();

  await loginAsEmployee(page, supervisor);
  await expect(page.getByRole('heading', { name: 'ตึกของฉัน' })).toBeVisible();
  await expectNoSeriousViolations(page);
});
```

- [x] **Step 4: Run the accessibility specs via `scripts/e2e.sh` (and fix any violations found)**

Run: `E2E_HEADLESS=1 E2E_SLOW_MO=0 npm --prefix frontend run e2e -- --dataset medium accessibility.spec.ts`
Expected: PASS (0 critical/serious WCAG violations across all audited screens).

- [x] **Step 5: Commit Task 3**

```bash
git add frontend/package.json frontend/package-lock.json frontend/e2e/desktop/accessibility.spec.ts frontend/e2e/phone/accessibility.spec.ts
git commit -m "test(a11y): add axe-core WCAG 2.2 AA accessibility specs (facility-0234)"
```

---

### Task 4: Category 8 — Visual Regression Tests with Playwright `toHaveScreenshot()` (`facility-0235`)

**Files:**
- Create: `frontend/e2e/desktop/visual-regression.spec.ts`
- Create: `frontend/e2e/phone/visual-regression.spec.ts`
- Create: Baseline PNG snapshots generated under `frontend/e2e/desktop/visual-regression.spec.ts-snapshots/` and `frontend/e2e/phone/visual-regression.spec.ts-snapshots/`

**Interfaces:**
- Consumes: `loginAsAdmin`, `loginAsEmployee`, `overviewFixture`, `cleaner`, `supervisor`
- Produces: Deterministic visual snapshot tests with `animations: 'disabled'`, `maxDiffPixelRatio: 0.01`, and masked dynamic timestamps/dates.

- [x] **Step 1: Create `frontend/e2e/desktop/visual-regression.spec.ts`**

```ts
import { loginAsAdmin } from '../support/login.ts';
import { overviewFixture } from '../support/seed.ts';
import { expect, test } from '../support/test.ts';

test.skip(process.env.E2E_DATASET !== 'medium', 'Visual baselines use the deterministic medium dataset');

test('desktop key admin screens match visual baselines', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await loginAsAdmin(page);

  const { date, shift, buildingId } = overviewFixture();
  await page.goto(`/admin/overview?date=${date}&shift=${shift}&building=${buildingId}&group=entered&page=1`);
  await expect(page.getByRole('heading', { name: 'สรุปผลของกะ' })).toBeVisible();
  await expect(page).toHaveScreenshot('desktop-overview.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.locator('time'), page.locator('header p')],
  });

  await page.goto('/admin/schedules');
  await expect(page.getByRole('heading', { name: 'ตารางงาน' })).toBeVisible();
  await expect(page).toHaveScreenshot('desktop-schedules.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.locator('time')],
  });
});
```

- [x] **Step 2: Create `frontend/e2e/phone/visual-regression.spec.ts`**

```ts
import { loginAsEmployee } from '../support/login.ts';
import { cleaner, supervisor } from '../support/seed.ts';
import { expect, test } from '../support/test.ts';

test.skip(process.env.E2E_DATASET !== 'medium', 'Visual baselines use the deterministic medium dataset');

test('phone key cleaner and supervisor screens match visual baselines', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await loginAsEmployee(page, cleaner);
  await expect(page.getByRole('heading', { name: 'งานของฉัน' })).toBeVisible();
  await expect(page).toHaveScreenshot('phone-my-work.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.locator('time')],
  });

  await page.goto('/my-account');
  await page.getByRole('button', { name: 'ออกจากระบบ' }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'ออกจากระบบ' }).click();

  await loginAsEmployee(page, supervisor);
  await expect(page.getByRole('heading', { name: 'ตึกของฉัน' })).toBeVisible();
  await expect(page).toHaveScreenshot('phone-my-building.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.locator('time')],
  });
});
```

- [x] **Step 3: Generate initial baselines and re-run without `--update-snapshots` to verify strict pass**

Run:
```bash
E2E_HEADLESS=1 E2E_SLOW_MO=0 npm --prefix frontend run e2e -- --dataset medium visual-regression.spec.ts --update-snapshots
E2E_HEADLESS=1 E2E_SLOW_MO=0 npm --prefix frontend run e2e -- --dataset medium visual-regression.spec.ts
```
Expected: Second run PASSES with 0 pixel-diff failures.

- [x] **Step 4: Commit Task 4**

```bash
git add frontend/e2e/desktop/visual-regression.spec.ts frontend/e2e/phone/visual-regression.spec.ts frontend/e2e/desktop/visual-regression.spec.ts-snapshots frontend/e2e/phone/visual-regression.spec.ts-snapshots
git commit -m "test(visual): add Playwright visual regression snapshots (facility-0235)"
```

---

### Task 5: Category 10 — Performance / Load Test Benchmark (`facility-0235`)

**Files:**
- Create: `scripts/load-test.mjs`
- Modify: `frontend/package.json` (add `"test:load"` script)

**Interfaces:**
- Consumes: Running loopback API (`LOAD_BASE_URL`, default `http://127.0.0.1:5001`) seeded with `--dataset large` (70+ Areas) and synthetic admin/cleaner credentials via env (`LOAD_ADMIN_USER`, `LOAD_ADMIN_PASS`, `LOAD_CLEANER_ID`, `LOAD_CLEANER_PHONE`).
- Produces: Concurrent load benchmark asserting 0% HTTP error rate, `p95 <= 500ms` for `/api/admin/overview/areas`, and `p95 <= 200ms` for `/api/service-points` and `/api/my-work`.

- [x] **Step 1: Create `scripts/load-test.mjs`**

```js
#!/usr/bin/env node
import { performance } from 'node:perf_hooks';

const baseUrl = process.env.LOAD_BASE_URL ?? 'http://127.0.0.1:5001';
const adminUsername = process.env.LOAD_ADMIN_USER ?? 'admin';
const adminPassword = process.env.LOAD_ADMIN_PASS ?? 'Admin#12345';
const cleanerId = process.env.LOAD_CLEANER_ID ?? '650001';
const cleanerPhone = process.env.LOAD_CLEANER_PHONE ?? '0000000001';

async function login(path, body) {
  const res = await fetch(`${baseUrl}${path}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`Login ${path} failed: ${res.status}`);
  const data = await res.json();
  return data.accessToken;
}

function percentile(sorted, p) {
  const idx = Math.min(sorted.length - 1, Math.ceil((p / 100) * sorted.length) - 1);
  return sorted[idx];
}

async function runScenario({ name, url, token, concurrency = 10, total = 50, p95BudgetMs }) {
  const latencies = [];
  let errors = 0;
  let next = 0;

  async function worker() {
    while (true) {
      const i = next++;
      if (i >= total) return;
      const start = performance.now();
      try {
        const res = await fetch(`${baseUrl}${url}`, {
          headers: { authorization: `Bearer ${token}` },
        });
        await res.arrayBuffer();
        if (!res.ok) errors++;
        else latencies.push(performance.now() - start);
      } catch {
        errors++;
      }
    }
  }

  await Promise.all(Array.from({ length: concurrency }, worker));
  latencies.sort((a, b) => a - b);
  const p50 = latencies.length ? percentile(latencies, 50) : NaN;
  const p95 = latencies.length ? percentile(latencies, 95) : NaN;
  const p99 = latencies.length ? percentile(latencies, 99) : NaN;
  const passed = errors === 0 && p95 <= p95BudgetMs;

  console.log(
    `${passed ? 'PASS' : 'FAIL'} ${name}: total=${total} conc=${concurrency} errors=${errors} p50=${p50.toFixed(1)}ms p95=${p95.toFixed(1)}ms (budget <=${p95BudgetMs}ms) p99=${p99.toFixed(1)}ms`,
  );
  return passed;
}

const adminToken = await login('/api/auth/admin/login', { username: adminUsername, password: adminPassword });
const cleanerToken = await login('/api/auth/login', { employeeId: cleanerId, phone: cleanerPhone });

const results = [
  await runScenario({ name: 'Admin Overview (/api/admin/overview/areas)', url: '/api/admin/overview/areas', token: adminToken, p95BudgetMs: 500 }),
  await runScenario({ name: 'Admin Service Points (/api/service-points)', url: '/api/service-points', token: adminToken, p95BudgetMs: 200 }),
  await runScenario({ name: 'Cleaner My Work (/api/my-work)', url: '/api/my-work', token: cleanerToken, p95BudgetMs: 200 }),
];

if (!results.every(Boolean)) {
  process.exit(1);
}
```

- [x] **Step 2: Verify `scripts/load-test.mjs` against an isolated large dataset**

Run against a throwaway API instance (or verify syntax with `node --check scripts/load-test.mjs` and run against isolated API).
Expected: All 3 scenarios print `PASS` with `errors=0` and p95 within budget.

- [x] **Step 3: Commit Task 5**

```bash
git add scripts/load-test.mjs
git commit -m "test(perf): add concurrent API load benchmark with strict p95 gates (facility-0235)"
```

---

### Task 6: Category 12 — Cross-Browser Compatibility Matrix (Chromium + WebKit) (`facility-0234`)

**Files:**
- Create: `scripts/compat-e2e.sh`
- Modify: `frontend/package.json` (add `"e2e:compat": "bash ../scripts/compat-e2e.sh"`)

**Interfaces:**
- Consumes: `scripts/e2e.sh` and `E2E_BROWSER` (`chromium` | `webkit`) in `frontend/e2e/support/browser.ts`
- Produces: Single-command cross-browser compatibility gate running core desktop & phone specs (`overview.spec.ts`, `my-work.spec.ts`, `check-in.spec.ts`, `login.spec.ts`) on both Chromium and WebKit (Safari engine).

- [x] **Step 1: Create `scripts/compat-e2e.sh` and make it executable**

```bash
#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SPECS=(overview.spec.ts my-work.spec.ts check-in.spec.ts login.spec.ts)

for browser in chromium webkit; do
  echo "=== Compatibility run: $browser ==="
  E2E_BROWSER="$browser" E2E_HEADLESS=1 E2E_SLOW_MO=0 bash "$ROOT/scripts/e2e.sh" run --dataset medium "${SPECS[@]}"
done
```

Run: `chmod +x scripts/compat-e2e.sh`

- [x] **Step 2: Add `"e2e:compat"` script to `frontend/package.json`**

Add `"e2e:compat": "bash ../scripts/compat-e2e.sh",` under `"scripts"` in `frontend/package.json`.

- [x] **Step 3: Run `npm --prefix frontend run e2e:compat`**

Run: `npm --prefix frontend run e2e:compat`
Expected: Both Chromium and WebKit runs PASS with 0 failures.

- [x] **Step 4: Commit Task 6**

```bash
git add scripts/compat-e2e.sh frontend/package.json
git commit -m "test(compat): add Chromium and WebKit cross-browser compatibility runner (facility-0234)"
```

---

### Task 7: Full 12-Category Verification Run & Handoff Scorecard

**Files:**
- Modify: `docs/handoff.md`

- [x] **Step 1: Execute all 7 existing suites + 5 new suites and record exact counts**

Run:
1. Unit + API + Integration + Security + Contract (Backend):
   - `dotnet test backend/tests/JanitorialManagementDashboard.UnitTests --verbosity minimal`
   - `dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --verbosity minimal` (with throwaway `FACILITY_MYSQL_TEST`)
2. Component + Contract + Regression (Frontend):
   - `npm --prefix frontend run lint && NODE_ENV=test npm --prefix frontend test && npm --prefix frontend run build`
3. E2E + Accessibility + Visual Regression + Compatibility:
   - `E2E_HEADLESS=1 E2E_SLOW_MO=0 npm --prefix frontend run e2e -- --dataset large`
   - `E2E_HEADLESS=1 E2E_SLOW_MO=0 npm --prefix frontend run e2e -- --dataset medium accessibility.spec.ts visual-regression.spec.ts`
   - `npm --prefix frontend run e2e:compat`
4. Performance / Load:
   - Run `scripts/load-test.mjs` against isolated `--dataset large` API.

- [x] **Step 2: Update `docs/handoff.md` with the 12-category pass/fail scorecard and commit**

```bash
git add docs/handoff.md
git commit -m "docs(handoff): record Overview smooth transition and 12-category test scorecard"
```
