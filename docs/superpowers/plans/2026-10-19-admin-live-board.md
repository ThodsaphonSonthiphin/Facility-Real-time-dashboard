# Admin Live Board Implementation Plan (plan 11)

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Admin watches the site live in the browser. The Dashboard (D1) shows every point's status as it changes. The attendance page (D2) shows who is in each Area this shift and lets the Admin add or correct a time. The alert list (D3) is where the Admin closes alerts and assigns covers. Admin pages become lazy routes, so phones never download them.

**Architecture:**
- **Lazy Admin routes first (Task 1).** Each route under the Admin guard loads its page with React Router's `lazy`, so its code is a separate file the first time an Admin opens it. Phone pages stay in the main file. `renderApp` waits until the router has loaded the first page's file.
- **Live plumbing (Task 2).**
  - `hubSlice` keeps one status per hub connection, by key, instead of one global value (deferred from plan 9).
  - `followScanHub` takes that key and handles `AttendanceRecorded` as well as the point events.
  - `PointCard`'s nullable fields become optional (also deferred from plan 9).
- **Three Admin pages on the API that plans 4 and 6 built.** No file under `backend/` changes.
  - `pages/dashboard/` (D1): `GET /api/service-points`. Pushed cards patch the cache. It also polls every 30 s and refetches on window focus (facility-0061). Building, status and issue filters live in `dashboardSlice` (facility-0024).
  - `pages/attendance/` (D2): `GET /api/admin/attendance`, refetched when `AttendanceRecorded` names the shift on screen, with the same poll. The shift, date and building are in the URL. The correction form calls `PUT /api/admin/attendance`.
  - `pages/alerts/` (D3): the alert list with "คุยแล้ว ปิดเตือน", the Areas with nobody and their cover form, the cover button on a Blocked Scan, and this shift's covers with cancel.
- **Shared pieces.**
  - `shared/api/attendanceBoardApi.ts`: the board query, which D2 and D3 both use.
  - `shared/api/alertListApi.ts`: the alert query, which D3 and the menu's count both use.
  - `shared/components/LiveBadge.tsx`: the badge in the page's corner (facility-0064).
  - `store/slices/dashboardSlice.ts`: the Dashboard's filters.

**Tech Stack:** React 19.2, TypeScript 6.0, Vite 8.3, `@reduxjs/toolkit` 2.13.0 (RTK Query, `setupListeners`), `react-router` 7.18.4 (`lazy` routes), `@microsoft/signalr` 10, Vitest 5.0 on jsdom 30.1.2, Testing Library, eslint 9 with boundaries. **No new package.**

**Spec:** this plan implements the Admin live board side of these ADRs, with wireframes D1, D2 and D3 for the screens. Where a wireframe and an ADR disagree, the ADR wins.
- `docs/adr/facility-0061-dashboard-keeps-30s-poll-beside-push.md`: `pollingInterval: 30_000` and `refetchOnFocus` on the Dashboard's hook, `setupListeners(store.dispatch)`, and hub patches as the main path.
- `docs/adr/facility-0064-dashboard-hub-offline-keeps-todays-behaviour.md`, with its 2026-10-16 amendment:
  - The corner badge says "SignalR Live" in green, or "Connecting..." in orange, with the same words and colours as today.
  - It refetches once after every hub start and reconnect, and restarts every 3 s after a close.
- `docs/adr/facility-0062-phone-bundle-budget-200kb-gzip.md`: item 3, Admin pages as lazy routes. The owner chose on 2026-10-19 to do this now, before the budget runs out.
- `docs/adr/facility-0024-client-state-split.md`: server data in the RTK Query cache, Dashboard filters in `dashboardSlice`, and form drafts in `useState`.
- `docs/adr/facility-0027-attendance-board-dashboard-tab-and-capacity.md` with its amendments, and `facility-0050`:
  - Attendance is shown by Area. Nobody is ever late or early.
  - Areas with nobody come first, and a live update comes on `AttendanceRecorded`.
- `docs/adr/facility-0051-admin-corrections-and-audit-log.md`:
  - The Admin adds or moves a time, always with a reason, and the row then carries "แก้โดย Admin".
  - A missing Shift-Out shows up as an alert, to be filled in on D2.
- `docs/adr/facility-0041-scan-only-own-area-with-cover-assignment.md` and `facility-0048-supervisor-assigned-to-building.md`:
  - A cover lasts one shift: an Area cover for a Cleaner, a building cover for a Supervisor.
  - Assigning a cover from a Blocked Scan also closes that alert.
- `docs/adr/facility-0035`, `0037`: GPS flags are recorded, then shown as alerts for the Admin to talk through. `facility-0046`: an issue is a tag on the card, not a status. `facility-0047`: the point statuses.
- `docs/adr/facility-0058-dashboard-is-admin-only.md`, `facility-0059` (no link to a sign page), `facility-0025`, `0026`, `0063`, `0065`, `0066`, `0067`, `0068`, as in plans 7–10.
- Plans 4 and 6 (`docs/superpowers/plans/2026-10-14-admin-attendance-alerts-history-backend.md`, `2026-10-14-supervisor-inspection-backend.md`) built the endpoints. `docs/handoff.md` "Notes for the frontend plan" has the API quirks.
- Wireframes (`docs/design/wireframe/`): `BoardStatus.dc.html` (D1), `BoardAttendance.dc.html` (D2), `BoardFlags.dc.html` (D3).

**The API this plan uses (unchanged; read the C# if in doubt):**

| Call | Answer | Source |
|---|---|---|
| `GET /api/service-points` | `PointStatusDto[]`, every active point of every active Area, by Area code then the Area's order, as the clock's shift sees it: `{ id, name, areaId, areaCode, areaName, buildingCode, status, currentRound?, nextRound?, lastScan?, issue?, rework? }` | `Endpoints/ServicePointEndpoints.cs`, `DTOs/PointDtos.cs`, `Infrastructure/Persistence/PointBoardQuery.cs` |
| Hub `/hubs/scan` → Admins | `ScanRecorded`, `InspectionRecorded`: one `PointStatusDto`. `AttendanceRecorded`: `{ areaId, shiftDate, shift }` (Admins only) | `Hubs/ScanHub.cs`, `PointNotifications.cs`, `AttendanceBoard.cs` |
| `GET /api/admin/attendance[?shiftDate=yyyy-MM-dd&shift=Day\|Night][&buildingId=n]` | `AttendanceBoardDto { shiftDate, shift, shiftEnded, summary { required, present, empty, covered }, rows[] }`. A row is `{ areaId, areaCode, areaName, buildingCode, cleaner? { id, employeeId, displayName }, state, events[], covers[] }`. An event is `{ id, eventType, occurredAt, source, withinRadius?, distanceM? }`. A cover is `{ id, userId, employeeId, displayName, ownAreaCode?, assignedAt, shiftInAt? }`. Pass both date and shift, or neither (then the clock's shift); 400 otherwise | `Endpoints/AdminAttendanceEndpoints.cs`, `AttendanceBoard.cs`, `DTOs/AdminAttendanceDtos.cs` |
| `PUT /api/admin/attendance` `{ userId, shiftDate, shift, eventType, time: "HH:mm", reason }` | 200 `BoardEventDto`; 400 / 409 `{ message }`. The Cleaner's own shift only, inside the attendance window, in order, not in the future. Saving the same minute again changes nothing | same |
| `GET /api/admin/alerts` | `AlertDto[]` for yesterday and today, newest first: `{ kind, recordId, at, person { id, employeeId, displayName, role, areaCode? }, areaCode, areaName, signCode?, pointName?, distanceM?, radiusM?, accuracyM?, missingEvents[], reviewed, reviewedAt?, cover? { userId, areaId?, buildingId?, shiftDate, shift }, blockReason? }`. For `MissingAttendance`, `at` is the shift's end | `Endpoints/AdminAlertEndpoints.cs`, `DTOs/AdminAlertDtos.cs`, `Persistence/AlertQuery.cs` |
| `POST /api/admin/alerts/review` `{ kind, recordId, note? }` | 204; 400 for `MissingAttendance` (it closes once the time is filled); 404; 409 `{ message: "ปิดเตือนนี้ไปแล้ว" }` | same |
| `GET /api/admin/cover-assignments/candidates?areaId=n&shiftDate=yyyy-MM-dd&shift=Day` | `CoverCandidateDto[] { id, employeeId, displayName, areaCode, shiftInAt?, scannedThisAreaAt? }`, best first | `Endpoints/CoverAssignmentEndpoints.cs`, `DTOs/CoverDtos.cs` |
| `POST /api/admin/cover-assignments` `{ userId, areaId? \| buildingId?, shiftDate, shift, blockedScanId? }` | 201 `CoverAssignmentDto { id, userId, personName, areaId?, areaCode?, buildingId?, buildingCode?, shiftDate, shift, assignedAt }`; 400 / 409 `{ message }`. From a Blocked Scan it also closes that alert | same |
| `DELETE /api/admin/cover-assignments/{id}` | 204; 404 `{ message }` | same |
| `GET /api/admin/buildings` | `BuildingDto[] { id, code, name }` (plan 10's `useGetBuildingsQuery`) | `AdminAreaEndpoints.cs` |

JSON is camelCase and enums are strings. `AlertKind` is one of `BlockedScan`, `AttendanceGps`, `SubmissionGps`, `MissingAttendance` or `InspectionGps`. `AreaAttendanceState` is one of `Empty`, `Working`, `OnBreak`, `Done` or `Covered`. Times are UTC ISO strings ending in `Z`. A `DateOnly` such as `shiftDate` arrives as `"2026-10-15"`. **Null fields are left out.**

**Where this plan sits:** the frontend now comes in six plans. Plan 7's table had five; the owner split its plan 11 on 2026-10-19.

| Plan | Scope | Wireframes |
|---|---|---|
| 7–10 | Foundation, Cleaner phone, Supervisor phone, Admin setup (merged) | M0–M17, D0, D4–D6, D10 |
| **11 (this one)** | Admin live board: lazy Admin routes, point status with hub and 30 s poll, attendance and corrections, alerts and covers | D1, D2, D3 |
| 12 | Admin reports: inspection summary, Excel export, Admin history | D7, D8, D9 |

## Global Constraints

- **Branch:** `feat/admin-live-board`, created from `master` at `8e35d97` (PR #59 merged, then a handoff commit). Do not merge it.
- **Machine:** the Mac. zsh, Node v26.8.2, npm 11.19.1, .NET SDK 10.0.401.
  - Frontend commands run in `frontend/`. Backend commands run from the repo root.
  - In zsh, quote globs: `grep --include='*.tsx'`.
  - Write files that hold Thai text with the editor tools, not shell heredocs.
- **No new package.** `package.json` does not change.
- **API unchanged.** No file under `backend/` changes. Where a wireframe shows something the API does not send, leave it out (decision 8).
- **Bundle (facility-0062):**
  - From Task 1 on, the main file is the one `dist/index.html` loads, and its JavaScript must stay ≤ 200 kB gzip. Admin pages are separate files.
  - Tasks 1, 3, 5, 7 and 9 measure it. If it goes over, stop and report to the controller.
- **Styles:**
  - No `style={...}` anywhere in `src/` (facility-0063).
  - Page prefixes are `dashboard-`, `attendance-` and `alerts-`. Shared components use `app-`. New tokens go in `src/index.css`.
- **Text people type and server text** (names, codes, defects, notes, messages) are rendered as React text only. No `dangerouslySetInnerHTML`, and no `href` or `src` built from typed text (facility-0066). Links are built from server ids and enum values only.
- **No link to a sign page** (`/scan/…`, `/check-in/…`) on any Admin page (facility-0059).
- **Nothing is stored on the device:** no `localStorage`, `sessionStorage` or IndexedDB (facility-0068).
- **Thai time:**
  - Every time and date on screen goes through `shared/utils/thaiTime.ts`: `formatTime`, `formatDate`, `formatRound`, `formatClock`, and the new `formatShiftDate`, `parseShiftDate` and `formatDayTime`.
  - Never call `toLocaleString`, `toLocaleTimeString` or `toLocaleDateString` elsewhere.
- **The browser computes no point status** (facility-0019, 0061 option D). Cards show what the server sent.
- **Server text is shown as is.** `getErrorMessage` shows the API's `{ message }`. Pages add their own text only for checks made before sending (facility-0022 item 4).
- **Missing fields:** a field the server may send as null arrives missing.
  - Types mark such fields optional.
  - Code tests them with truthiness or `== null`, never `=== null`.
  - Test fixtures leave them out.
- **One send at a time:** while a save is in flight, its button is disabled.
- **Tests:**
  - Vitest on jsdom. Only browser seams are faked:
    - `fetch`, with `src/test/fakeServer.ts`.
    - The SignalR package, through `test.alias` (`src/test/fakeSignalR.ts`).
    - Timers, only in the 30 s poll tests (Tasks 3 and 5). They are started before `renderApp` with `vi.useFakeTimers({ shouldAdvanceTime: true })` and restored in `finally`.
    - The window's focus event, with `setupListeners(store.dispatch)` in the test itself.
  - No MSW, no `vi.mock`, and no module mocks of our own code (regression-net #32).
  - Find elements by role, label or text the way a person would, never by CSS class.
  - The made-up Admin session is account 7, `ผู้ดูแลทดสอบ` (`adminSession` from `src/test/adminFixtures.ts`). Admin pages do not print the Admin's name.
- **Copy:** Thai UI text comes from the wireframes. Where this plan gives a string, use it exactly, because tests match it.
- **Public repo:** made-up names only (`แม่บ้านทดสอบ`, `แม่บ้านลา`, `แม่บ้านที่อื่น`, `หัวหน้าทดสอบ`, `Area ทดลอง`), employee IDs like `650001`. No secret in any file.
- **MySQL** is used only in Task 9's controller run.
  - Use only throwaway databases `facility_*_test`. Never touch `facility_dashboard`.
  - Ask the owner for the root password each time. Type it only into the command, never into a file or into memory.
  - Subagents skip that step.
- **Handoff after every task (owner's rule):**
  - The last step of every task adds one entry to a new section `## Task log (plan 11)` in `docs/handoff.md`, placed just above `## Pending work`. It is committed with the task.
  - The entry is in English: the date, the task, what changed, the test result, the bundle size where measured, and what comes next. No secret, phone number or real name.
- **Commits** end with the attribution trailer your session gives you. The husky pre-commit runs frontend lint, tsc and tests on every commit. If it fails, fix the cause. Never use `--no-verify`.
- **Baselines at `8e35d97`:** frontend 247 tests pass (30 files); backend 299 unit tests pass; API tests 531 pass and 5 are skipped (the MySQL ones); JS 173.53 kB gzip in one file.

### Decisions made while planning (report them to the owner in the PR)

1. **Plan 11 is split (owner, 2026-10-19).** This plan covers the live pages (D1, D2, D3). Plan 12 covers the reports (D7, D8, D9). Each is its own branch and PR.
2. **Lazy Admin routes now (owner, 2026-10-19; facility-0062 item 3).**
   - Every route under the Admin guard, plan 10's pages included, uses `lazy`. Phone pages do not.
   - The root route gets `hydrateFallbackElement` ("กำลังโหลด...") for the moment the first page's file loads.
   - A file that fails to load (a new build replaced it while the page was open) falls to the root crash screen, which has "โหลดใหม่".
   - `renderApp` waits for `router.state.initialized`, so tests still see the page right after it.
3. **Routes:**
   - D1 stays at `/dashboard`.
   - D2 is `/admin/attendance`, with `?date=yyyy-MM-dd&shift=Day|Night&building=<id>&fill=<userId>&event=<AttendanceEvent>`.
   - D3 is `/admin/alerts`.
   - They are pages in the Admin menu, not facility-0027's sub-tabs: each page has its own address (facility-0026) and its own entry in plan 10's menu.
   - The menu now reads สถานะจุด, การเข้างาน, รายการที่ขึ้นเตือน, จัดการ Area และป้าย, บัญชีผู้ใช้. Plan 12 adds its three pages.
4. **One hub status per connection** (deferred from plan 9).
   - `hubSlice` holds `connections: Record<key, status>`. Each `followScanHub` call has a key, and the page reads its own key with `selectHubStatus(key)`.
   - When the cache entry goes, its key goes too.
   - Two boards on screen, or a board whose arguments just changed, never overwrite each other's status.
5. **Live sources:**
   - **D1:** `ScanRecorded` and `InspectionRecorded` replace that point's card. A card for a point the list does not have, or one that lands while a GET is in flight, asks again by tag.
   - **D2 and D3's board:** `AttendanceRecorded` invalidates the boards of that shift date and shift (tag `AttendanceBoard:<date>:<shift>`), whatever building they show.
   - **The rule they share:** each live page asks again after every hub start and reconnect (facility-0064). Each one uses `livePoll`: on every open, every 30 s, and on window focus (facility-0061).
   - **Alerts:** no hub event exists. The alert list is asked on every open, every 30 s, and after each action.
6. **The badge (facility-0064 item 1)** keeps today's words: "SignalR Live" (green) while live, "Connecting..." (orange) while connecting or reconnecting. It is on D1 and D2.
7. **Dashboard filters live in `dashboardSlice` (facility-0024):**
   - The building (by code, since the card has `buildingCode` only), one status, and "มีปัญหาที่แจ้ง" (issue only).
   - They stay set across pages and are cleared when the session ends.
   - The legend items are the status buttons, with counts. The counts follow the building filter.
   - facility-0024's `searchQuery` and `lastUpdatedPointId` are not built, because D1 has no search box or highlight.
8. **What the wireframes show that the API does not send is left out:**
   - D1's Area line ("ตึก A · 2 กะ · สมศรี เข้างาน 06:52"). The page shows "ตึก A · n จุด" instead.
   - D1's every-round list (the card has only the current and next round).
   - D1's "เลยมา / เหลือ x นาที" countdown, because the browser computes no time-based status.
   - D1's per-card GPS flag.
   - D3's "มีคนสแกนป้ายของ Area นี้" in the empty-Area list. The candidate list shows it.
   - The "ส่งออก Excel" links, which come with D8 in plan 12.
9. **Corrections (D2):**
   - The time is typed as "HH:mm" and checked before sending (plan 10 decision 12). The reason is required, up to 500 characters.
   - The event list shows each event's current time, and choosing an event fills in its time.
   - The form opens on the first event the Cleaner has not scanned. There is only a button where the Area has a regular Cleaner.
   - A missing Shift-Out on D3 links to D2 with that shift and the form open (`fill`, `event`). The shift comes from the alert time, which is the shift's end: 19:00 ends that date's Day shift, and 07:00 ends the Night shift of the date before.
10. **Covers (D3):**
    - **From an empty Area:** the server's candidates, best first, then "ยืนยันมอบหมาย".
    - **From a Blocked Scan with `cover`:** one press. A Cleaner's cover is an Area cover and a Supervisor's is a building cover. The server also closes the alert.
    - **Cancelling** a cover of this shift is one press, and assigning again undoes it.
    - **Not listed:** Supervisors' building covers, because no endpoint lists them. This is parked and the page says so.
11. **Closing an alert:** "คุยแล้ว ปิดเตือน" opens an optional note (up to 500 characters), then "ยืนยันปิดเตือน". A missing-attendance alert has no close button; it closes once the time is filled. A refusal is shown at the top of the page, because the alert may move tabs when the list is asked again.
12. **The menu's count** is the unreviewed alerts of the default range (yesterday and today). It shows as "รายการที่ขึ้นเตือน (n)" when n > 0, polled every 30 s on every wide Admin page. The alert query lives in `shared/api/` because the menu is shared.
13. **Dates are typed as "dd/mm/yyyy" text on D2**, for the same reason as round times in plan 10.
14. **`setupListeners(store.dispatch)` goes in `main.tsx`** (facility-0061 item 2). Tests call it themselves where they need focus.
15. **`PointCard`'s nullable fields are optional** (deferred from plan 9), and the `point()` fixture leaves them out.

---

## File structure after this plan

```
frontend/src/
├── main.tsx                          + setupListeners (Task 3)
├── router.tsx                        Admin routes lazy (Task 1); + admin/attendance, admin/alerts
├── router.test.tsx                   + lazy route table, menu
├── index.css                         + --color-status-break
├── store/
│   ├── index.ts                      + dashboard reducer
│   └── slices/hubSlice.ts            status per connection key, selectHubStatus
│       slices/dashboardSlice.ts      Dashboard filters (facility-0024)
├── shared/
│   ├── api/types.ts                  PointCard optional fields, AttendanceChanged
│   ├── api/baseApi.ts                + tags ServicePoints, AttendanceBoard, Alerts; livePoll
│   ├── api/scanHub.ts (+ test)       key, onPoint optional, onAttendance
│   ├── api/attendanceBoardApi.ts     board types, getAttendanceBoard (hub), boardTag, attendanceHubKey
│   ├── api/alertListApi.ts           alert types, getAlerts, openAlertCount
│   ├── components/LiveBadge.tsx      "SignalR Live" / "Connecting..."
│   ├── components/AdminNav.tsx       + การเข้างาน, รายการที่ขึ้นเตือน (n)
│   ├── components/shared.css         + app-live
│   └── utils/thaiTime.ts (+ test)    + formatShiftDate, parseShiftDate, formatDayTime
├── pages/
│   ├── dashboard/                    DashboardPage (D1), Filters, PointTile, dashboardApi, boardRules (+ test),
│   │                                 dashboard.css, DashboardPage.test, DashboardFilters.test
│   ├── attendance/                   AttendancePage (D2), BoardControls, AttendanceTable, CorrectionForm,
│   │                                 attendanceApi, attendanceRules (+ test), attendance.css, index.ts,
│   │                                 AttendancePage.test, AttendanceCorrection.test
│   ├── alerts/                       AlertsPage (D3), AlertItem, EmptyAreas, CoverForm, CoversThisShift,
│   │                                 alertsApi, alertRules (+ test), alerts.css, index.ts,
│   │                                 AlertsPage.test, AlertCovers.test
│   └── my-building/                  hub key
└── test/
    ├── renderApp.tsx                 waits for the router's lazy pages
    ├── cleanerFixtures.ts            point() without null fields
    └── liveFixtures.ts               Dashboard points, attendance board, alerts, cover candidates, apiIdle
```

---

### Task 1: Lazy Admin routes

Every Admin page becomes a lazy route, so a phone never downloads Admin code. Phone pages stay in the main file.

**Files:**
- Modify: `docs/handoff.md`
- Modify: `frontend/src/router.tsx`
- Modify: `frontend/src/test/renderApp.tsx`
- Test: `frontend/src/router.test.tsx`

**Interfaces:**
- Consumes: the page components exported by each `pages/<page>/index.ts`.
- Produces:
  - Admin routes with `lazy: async () => ({ Component })`. Later tasks add `admin/attendance` and `admin/alerts` the same way, and add their paths to the test's list.
  - `renderApp` resolves only after `router.state.initialized` is true.

- [ ] **Step 1: Create the branch**

```bash
git checkout master && git pull --ff-only && git checkout -b feat/admin-live-board
git log --oneline -1   # expect 8e35d97 (or a later docs-only commit)
```

- [ ] **Step 2: Bring the handoff up to date**

In `docs/handoff.md`, replace the plan 10 row of the Status table with these three rows:

```markdown
| 10 | Admin setup: Areas, points, round windows, signs, sign location capture, print signs, accounts | done, merged (PR #59, `a6dc170`) (branch `feat/admin-setup`): `docs/superpowers/plans/2026-10-19-admin-setup.md` |
| 11 | Admin live board: lazy Admin routes, point status with hub and 30 s poll (D1), attendance and corrections (D2), alerts and covers (D3) | in progress (branch `feat/admin-live-board`): `docs/superpowers/plans/2026-10-19-admin-live-board.md` |
| 12 | Admin reports: inspection summary (D7), Excel export (D8), Admin history (D9) | not started (the owner split plan 11 on 2026-10-19) |
```

In `## Pending work`, replace the line `- Frontend plan 11 (scope in the plan-7 file): Admin live board.` with:

```markdown
- Frontend plan 12: Admin reports, inspection summary (D7), Excel export (D8), Admin history (D9). Plan 11 was split in two on 2026-10-19.
```

- [ ] **Step 3: Measure the bundle before**

Run: `cd frontend && npm run build && ls dist/assets/*.js`
Expected: exactly one `.js` file. The build report gives it about 173.5 kB gzip.

- [ ] **Step 4: Write the failing tests**

In `frontend/src/router.test.tsx`, replace the first four import lines:

```tsx
import { screen, within } from '@testing-library/react';
import type { RouteObject } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { routes } from './router';
```

with:

```tsx
import { screen, within } from '@testing-library/react';
import type { ReactElement } from 'react';
import type { RouteObject } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { routes } from './router';
import type { Role } from './shared/api/types';
```

Below the `noSession` line, add:

```tsx
/** The page routes behind the guard of exactly this one role, through its layouts. */
function pagesOf(role: Role): RouteObject[] {
  const guard = routes[0].children?.find((route) => {
    const roles = (route.element as ReactElement<{ roles?: Role[] }> | undefined)?.props.roles;
    return roles?.length === 1 && roles[0] === role;
  });
  return (guard?.children ?? []).flatMap((layout) => layout.children ?? []);
}
```

Inside the `describe` block, after the test `'a phone page has no Admin menu'`, add:

```tsx
  it('every Admin page is a lazy route, so a phone never downloads Admin code (facility-0062 item 3)', () => {
    const pages = pagesOf('admin');

    expect(pages.map((page) => page.path)).toEqual([
      'dashboard',
      'admin/areas/:areaId?',
      'admin/areas/new',
      'admin/areas/:areaId/edit',
      'admin/print',
      'admin/accounts',
      'admin/areas/:areaId/capture',
    ]);
    expect(pages.every((page) => typeof page.lazy === 'function' && page.element === undefined)).toBe(true);
  });

  it('phone pages stay in the main file, so a scanned sign opens without a second download', () => {
    const phonePages = [...pagesOf('cleaner'), ...pagesOf('supervisor')];

    expect(phonePages.length).toBeGreaterThan(0);
    expect(phonePages.every((page) => page.lazy === undefined)).toBe(true);
  });

  it('an Admin page opens from the menu once its file has loaded', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('admin') });
    const { user } = await renderApp('/dashboard');

    await user.click(screen.getByRole('link', { name: 'บัญชีผู้ใช้' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'บัญชีผู้ใช้' })).toBeInTheDocument();
  });
```

- [ ] **Step 5: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/router.test.tsx`
Expected: FAIL. `'every Admin page is a lazy route …'` fails because the pages have `element` and no `lazy`. The other two new tests pass already. They guard the change.

- [ ] **Step 6: Make the Admin routes lazy**

Replace the whole of `frontend/src/router.tsx` with:

```tsx
import type { RouteObject } from 'react-router';
import { AdminLoginPage, LoginPage } from './pages/auth';
import { CheckInPage } from './pages/check-in';
import { InspectPage } from './pages/inspect';
import { MyBuildingPage } from './pages/my-building';
import { MyWorkPage } from './pages/my-work';
import { ScanPage } from './pages/scan';
import { AppLayout } from './shared/components/AppLayout';
import { ByRole } from './shared/components/ByRole';
import { CrashScreen } from './shared/components/CrashScreen';
import { HomeRedirect } from './shared/components/HomeRedirect';
import { ProtectedRoute } from './shared/components/ProtectedRoute';
import { StatusScreen } from './shared/components/StatusScreen';

/**
 * facility-0026: the whole route table as data. main.tsx feeds it to createBrowserRouter, tests to createMemoryRouter.
 * No loader or action: RTK Query owns data loading (facility-0022).
 *
 * facility-0062 item 3, done before the budget ran out (owner, 2026-10-19): every Admin page is its own file, loaded
 * the first time an Admin opens it (`lazy`), so a phone at a sign never downloads Admin code. Phone pages stay in the
 * main file, so a scanned sign opens without a second download.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    errorElement: <CrashScreen />,
    // While the first page's file loads. A file that cannot load (a new build replaced it) shows the crash screen
    hydrateFallbackElement: <p className="app-splash">กำลังโหลด...</p>,
    children: [
      { index: true, element: <HomeRedirect /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'login/admin', element: <AdminLoginPage /> },
      {
        element: <ProtectedRoute roles={['cleaner']} />,
        children: [
          {
            element: <AppLayout />,
            children: [
              { path: 'my-work', element: <MyWorkPage /> },
              // Printed on each Area's check-in sign (plan 8 decision 1); only the sign carries the token (facility-0059)
              { path: 'check-in/:token', element: <CheckInPage /> },
            ],
          },
        ],
      },
      {
        element: <ProtectedRoute roles={['cleaner', 'supervisor']} />,
        children: [
          {
            element: <AppLayout />,
            children: [
              // Printed on each point's sign (facility-0026): the Cleaner submits, the Supervisor inspects (facility-0029)
              {
                path: 'scan/:token',
                element: <ByRole pages={{ cleaner: <ScanPage />, supervisor: <InspectPage /> }} />,
              },
            ],
          },
        ],
      },
      {
        element: <ProtectedRoute roles={['supervisor']} />,
        children: [{ element: <AppLayout />, children: [{ path: 'my-building', element: <MyBuildingPage /> }] }],
      },
      {
        element: <ProtectedRoute roles={['admin']} />,
        children: [
          {
            element: <AppLayout wide />,
            children: [
              // facility-0058, wireframe D1
              { path: 'dashboard', lazy: async () => ({ Component: (await import('./pages/dashboard')).DashboardPage }) },
              // facility-0045, wireframe D4; without an id the first Area is open
              { path: 'admin/areas/:areaId?', lazy: async () => ({ Component: (await import('./pages/areas')).AreasPage }) },
              // facility-0045, wireframe D10 ("new" outranks the id above)
              { path: 'admin/areas/new', lazy: async () => ({ Component: (await import('./pages/area-form')).AreaFormPage }) },
              {
                path: 'admin/areas/:areaId/edit',
                lazy: async () => ({ Component: (await import('./pages/area-form')).AreaFormPage }),
              },
              // facility-0021 item 6, wireframe D5: ?area=<id>[&sign=<id>] or ?area=all
              { path: 'admin/print', lazy: async () => ({ Component: (await import('./pages/print-signs')).PrintSignsPage }) },
              // facility-0020, 0056, wireframe D6
              { path: 'admin/accounts', lazy: async () => ({ Component: (await import('./pages/accounts')).AccountsPage }) },
            ],
          },
          // M17: the Admin's phone page at the sign (facility-0045 item 2), in the phone column
          {
            element: <AppLayout />,
            children: [
              {
                path: 'admin/areas/:areaId/capture',
                lazy: async () => ({ Component: (await import('./pages/capture')).CapturePage }),
              },
            ],
          },
        ],
      },
      {
        path: '*',
        element: <StatusScreen title="ไม่พบหน้านี้" detail="ที่อยู่นี้ไม่มีในระบบ" linkTo="/" linkText="กลับหน้าแรก" />,
      },
    ],
  },
];
```

- [ ] **Step 7: Make `renderApp` wait for the lazy page**

Replace the whole of `frontend/src/test/renderApp.tsx` with:

```tsx
import { act, render, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, type RouteObject } from 'react-router';
import { App } from '../App';
import { routes as appRoutes } from '../router';
import { startSession } from '../shared/auth/session';
import { makeStore } from '../store';

/**
 * Renders the whole app at `url` the way main.tsx does: a fresh store, the page-load refresh, then the router
 * (regression-net #32: only fetch and SignalR are faked). Call fakeServer first; it must answer POST /api/auth/refresh.
 * Admin pages are lazy routes (facility-0062 item 3), so it also waits until the router has loaded the first page.
 */
export async function renderApp(url: string, routes: RouteObject[] = appRoutes) {
  const store = makeStore();
  const router = createMemoryRouter(routes, { initialEntries: [url] });
  const user = userEvent.setup();
  render(<App store={store} router={router} />);
  await act(() => startSession(store.dispatch));
  await waitFor(() => {
    if (!router.state.initialized) {
      throw new Error('the router is still loading the first page');
    }
  });
  return { store, router, user };
}
```

- [ ] **Step 8: Run the whole frontend suite**

Run: `cd frontend && npx vitest run`
Expected: PASS, 250 tests (247 + 3).

If an older test fails because a lazy page appears a moment later, change only that test's first lookup from `getBy…` to `await findBy…`, and name the test in the handoff entry. Do not change the app to make it pass.

- [ ] **Step 9: Measure the bundle after**

Run: `cd frontend && npm run build && grep -o 'assets/index-[A-Za-z0-9_-]*\.js' dist/index.html && ls dist/assets/*.js | wc -l`
Expected:
- Several `.js` files: the main file plus one for each Admin page. Shared Admin code may sit in a shared chunk.
- The main file (the one `dist/index.html` names) is smaller than 173.53 kB gzip in the build report.

Write down its gzip size for the handoff. Then run `rm -rf dist`.

- [ ] **Step 10: Lint and commit**

Add to `docs/handoff.md`, as a new section just above `## Pending work`:

```markdown
## Task log (plan 11)

- <date> · Task 1 done: Admin pages are lazy routes (facility-0062 item 3, owner 2026-10-19); the root route shows "กำลังโหลด..." while the first page's file loads; renderApp waits for router.state.initialized. Main JS <size> kB gzip (was 173.53 kB in one file). Frontend tests <n> passed. Next: Task 2 (hub status per connection, AttendanceRecorded, optional PointCard fields).
```

Then:

```bash
cd frontend && npm run lint
cd .. && git add docs/handoff.md frontend/src/router.tsx frontend/src/router.test.tsx frontend/src/test/renderApp.tsx
git commit -m "feat(frontend): Admin pages are lazy routes, so phones never download them"
```

---
### Task 2: Live plumbing: one hub status per connection, AttendanceRecorded, optional card fields

Plan 9 deferred two items to the start of plan 11. Both are done here, and the hub learns the Admin-only `AttendanceRecorded` event.

**Files:**
- Modify: `frontend/src/shared/api/types.ts`
- Modify: `frontend/src/test/cleanerFixtures.ts`
- Modify: `frontend/src/store/slices/hubSlice.ts`
- Modify: `frontend/src/shared/api/scanHub.ts`
- Modify: `frontend/src/pages/my-building/myBuildingApi.ts`, `frontend/src/pages/my-building/MyBuildingPage.tsx`
- Modify: `docs/handoff.md`
- Test: `frontend/src/shared/api/scanHub.test.ts`

**Interfaces:**
- Consumes: `followScanHub` and `hubSlice` from plan 9.
- Produces:
  - `PointCard` with optional `currentRound?`, `nextRound?`, `lastScan?`, `issue?` and `rework?`. `ReportedIssue.note?` is optional too.
  - `AttendanceChanged { areaId: number; shiftDate: string; shift: Shift }` in `shared/api/types.ts`.
  - In `store/slices/hubSlice.ts`:
    - `HubState { connections: Record<string, HubStatus> }`.
    - Actions `hubStatusChanged({ key, status })` and `hubFollowEnded(key)`.
    - `selectHubStatus(key: string): (state) => HubStatus`, which gives `'connecting'` for an unknown key.
  - `followScanHub({ key, cacheDataLoaded, cacheEntryRemoved, dispatch, accessToken, onPoint?, onAttendance?, onMissed })`.
  - `MY_BUILDING_HUB = 'my-building'` in `pages/my-building/myBuildingApi.ts`.

- [ ] **Step 1: Write the failing tests**

In `frontend/src/shared/api/scanHub.test.ts`, replace the import lines:

```ts
import { describe, expect, it, vi } from 'vitest';
import { makeStore } from '../../store';
import { point } from '../../test/cleanerFixtures';
import { hub } from '../../test/fakeSignalR';
import { followScanHub } from './scanHub';
import type { PointCard } from './types';
```

with:

```ts
import { describe, expect, it, vi } from 'vitest';
import { makeStore } from '../../store';
import { selectHubStatus } from '../../store/slices/hubSlice';
import { point } from '../../test/cleanerFixtures';
import { hub } from '../../test/fakeSignalR';
import { followScanHub } from './scanHub';
import type { AttendanceChanged, PointCard } from './types';
```

Replace the whole `follow` function, from its doc comment `/** One cache entry's life, driven by hand: …` to its closing brace, with:

```ts
interface FollowOptions {
  /** The connection's key in hubSlice. */
  key?: string;
  /** Whether the page takes point cards (ScanRecorded, InspectionRecorded). */
  points?: boolean;
  /** Whether the page takes AttendanceRecorded. */
  attendance?: boolean;
}

/** One cache entry's life, driven by hand: `loaded` is its first answer, `remove` its end. */
function follow({ key = 'test', points: withPoints = true, attendance: withAttendance = false }: FollowOptions = {}, store = makeStore()) {
  let loaded: (value?: unknown) => void = () => {};
  let failLoad: (reason: unknown) => void = () => {};
  let remove: () => void = () => {};
  const cacheDataLoaded = new Promise((resolve, reject) => {
    loaded = resolve;
    failLoad = reject;
  });
  const cacheEntryRemoved = new Promise<void>((resolve) => {
    remove = resolve;
  });
  const points: PointCard[] = [];
  const changes: AttendanceChanged[] = [];
  const counts = { missed: 0 };
  const done = followScanHub({
    key,
    cacheDataLoaded,
    cacheEntryRemoved,
    dispatch: store.dispatch,
    accessToken: async () => 'token-supervisor',
    onPoint: withPoints ? (card) => points.push(card) : undefined,
    onAttendance: withAttendance ? (change) => changes.push(change) : undefined,
    onMissed: () => {
      counts.missed += 1;
    },
  });
  return {
    loaded,
    failLoad,
    remove,
    points,
    changes,
    counts,
    done,
    status: () => selectHubStatus(key)(store.getState()),
    connections: () => store.getState().hub.connections,
  };
}
```

The tests `'when the entry goes, the connection stops, …'` and `'when the entry goes while a restart is waiting, …'` each contain this line:

```ts
      expect(f.status()).toBe('connecting');
```

Replace it in both tests with:

```ts
      expect(f.connections()).toEqual({}); // the ended connection leaves no status behind
```

Rename the first of those two tests to `'when the entry goes, the connection stops, its status goes, and nothing restarts'`.

After the test `'hands every ScanRecorded and InspectionRecorded card to the page, and nothing else'`, add:

```ts
  it('hands AttendanceRecorded to a page that asked for it, and no card to a page that did not (facility-0027 rule 5)', async () => {
    const f = follow({ points: false, attendance: true });
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));

    hub.latest().push('ScanRecorded', point());
    hub.latest().push('AttendanceRecorded', { areaId: 1, shiftDate: '2026-10-15', shift: 'Day' });

    expect(f.points).toEqual([]);
    expect(f.changes).toEqual([{ areaId: 1, shiftDate: '2026-10-15', shift: 'Day' }]);
  });

  it('each connection keeps its own status, and an ended one leaves no status behind (plan 11 decision 4)', async () => {
    const store = makeStore();
    const a = follow({ key: 'a' }, store);
    a.loaded();
    await vi.waitFor(() => expect(a.status()).toBe('live'));
    hub.failNextStarts = 1;
    const b = follow({ key: 'b' }, store);
    b.loaded();
    await vi.waitFor(() => expect(b.status()).toBe('reconnecting'));

    expect(a.status()).toBe('live');
    a.remove();
    await a.done;
    expect(store.getState().hub.connections).toEqual({ b: 'reconnecting' });

    b.remove();
    await b.done;
    expect(store.getState().hub.connections).toEqual({});
  });
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/shared/api/scanHub.test.ts`
Expected: FAIL. `store.getState().hub.connections` is undefined, and `AttendanceRecorded` reaches nobody.

- [ ] **Step 3: One status per connection**

Replace the whole of `frontend/src/store/slices/hubSlice.ts` with:

```ts
import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

/**
 * facility-0064 item 1: whether a scan hub connection is live is not server data, so it lives in a slice. Each page's
 * connection (one per cache entry, #26) has its own key, so two followers at once never overwrite each other's status
 * (plan 11 decision 4). connecting: not live yet; live: pushes arrive; reconnecting: the connection dropped and is being
 * tried again.
 */
export type HubStatus = 'connecting' | 'live' | 'reconnecting';

export interface HubState {
  connections: Record<string, HubStatus>;
}

const initialState: HubState = { connections: {} };

const hubSlice = createSlice({
  name: 'hub',
  initialState,
  reducers: {
    hubStatusChanged(state, action: PayloadAction<{ key: string; status: HubStatus }>) {
      state.connections[action.payload.key] = action.payload.status;
    },
    /** The cache entry went and its connection stopped. */
    hubFollowEnded(state, action: PayloadAction<string>) {
      delete state.connections[action.payload];
    },
  },
});

export const { hubStatusChanged, hubFollowEnded } = hubSlice.actions;
export const hubReducer = hubSlice.reducer;

/** A connection that has no status yet, or has ended, counts as connecting. */
export function selectHubStatus(key: string) {
  return (state: { hub: HubState }): HubStatus => state.hub.connections[key] ?? 'connecting';
}
```

- [ ] **Step 4: The follower takes a key and AttendanceRecorded**

Replace the whole of `frontend/src/shared/api/scanHub.ts` with:

```ts
import { HubConnectionBuilder } from '@microsoft/signalr';
import type { Dispatch } from '@reduxjs/toolkit';
import { hubFollowEnded, hubStatusChanged, type HubStatus } from '../../store/slices/hubSlice';
import type { AttendanceChanged, PointCard } from './types';

/** facility-0064: the automatic reconnect's waits; after it gives up, a fresh start every 3 s. */
export const HUB_RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];
export const HUB_RESTART_MS = 3_000;

/** ScanHub's point events (facility-0057, 0058): each carries the point's whole card as the clock's shift sees it. */
const POINT_EVENTS = ['ScanRecorded', 'InspectionRecorded'] as const;

export interface ScanHubFollow {
  /** This connection's name in hubSlice (plan 11 decision 4); the page's badge or footer reads the same key. */
  key: string;
  /** From onCacheEntryAdded: connect once the first answer is in, stop when the entry goes. */
  cacheDataLoaded: Promise<unknown>;
  cacheEntryRemoved: Promise<unknown>;
  dispatch: Dispatch;
  /** Asked on every connect and reconnect (facility-0023 item 3). */
  accessToken: () => Promise<string>;
  /** ScanRecorded and InspectionRecorded; left out by a page that shows no point cards. */
  onPoint?: (point: PointCard) => void;
  /** AttendanceRecorded, sent to Admins only (facility-0027 rule 5); left out by a page that shows no attendance. */
  onAttendance?: (change: AttendanceChanged) => void;
  /** Pushes may have been missed while the connection was down: ask the server again (facility-0064 item 2). */
  onMissed: () => void;
}

/**
 * #26 hub-cache-bridge: one connection per cache entry, opened in onCacheEntryAdded, so StrictMode and remounts never
 * open a second one. The hub sends a point's whole card, so the page patches its cache instead of asking again.
 */
export async function followScanHub({
  key,
  cacheDataLoaded,
  cacheEntryRemoved,
  dispatch,
  accessToken,
  onPoint,
  onAttendance,
  onMissed,
}: ScanHubFollow): Promise<void> {
  try {
    await cacheDataLoaded;
  } catch {
    return; // the entry went before its first answer
  }
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/scan', { accessTokenFactory: accessToken })
    .withAutomaticReconnect(HUB_RETRY_DELAYS_MS)
    .configureLogging('warn')
    .build();
  let removed = false;
  let restartTimer: ReturnType<typeof setTimeout> | undefined;
  const setStatus = (status: HubStatus) => dispatch(hubStatusChanged({ key, status }));

  const restartLater = () => {
    if (!removed) {
      setStatus('reconnecting');
      restartTimer = setTimeout(() => void start(), HUB_RESTART_MS);
    }
  };
  const start = async () => {
    if (removed) {
      return;
    }
    try {
      await connection.start();
    } catch {
      restartLater();
      return;
    }
    if (removed) {
      void connection.stop(); // the entry went while this start was in flight: do not leave the connection open
      return;
    }
    setStatus('live');
    onMissed(); // every start, the first included: pushes sent between the first answer and the connection going live were missed
  };

  if (onPoint) {
    for (const event of POINT_EVENTS) {
      connection.on(event, (point: PointCard) => onPoint(point));
    }
  }
  if (onAttendance) {
    connection.on('AttendanceRecorded', (change: AttendanceChanged) => onAttendance(change));
  }
  connection.onreconnecting(() => setStatus('reconnecting'));
  connection.onreconnected(() => {
    setStatus('live');
    onMissed();
  });
  connection.onclose(restartLater);

  setStatus('connecting');
  void start();
  await cacheEntryRemoved;
  removed = true;
  clearTimeout(restartTimer);
  dispatch(hubFollowEnded(key));
  await connection.stop();
}
```

- [ ] **Step 5: Types: the attendance event, and the card's missing fields**

In `frontend/src/shared/api/types.ts`, replace:

```ts
/** IssueDto (facility-0046): shown while the latest submission reported an issue. */
export interface ReportedIssue {
  tags: string[];
  note: string | null;
  reportedAt: string;
}
```

with:

```ts
/** IssueDto (facility-0046): shown while the latest submission reported an issue. note is missing when none was typed. */
export interface ReportedIssue {
  tags: string[];
  note?: string;
  reportedAt: string;
}
```

Replace:

```ts
/** PointStatusDto: one point's card. It never carries the QR Token (facility-0059, 0060). */
export interface PointCard {
  id: number;
  name: string;
  areaId: number;
  areaCode: string;
  areaName: string;
  buildingCode: string;
  status: PointStatus;
  currentRound: RoundWindow | null;
  nextRound: RoundWindow | null;
  lastScan: LastScan | null;
  issue: ReportedIssue | null;
  rework: Rework | null;
}
```

with:

```ts
/** PointStatusDto: one point's card. It never carries the QR Token (facility-0059, 0060). Fields the server sends as null arrive missing. */
export interface PointCard {
  id: number;
  name: string;
  areaId: number;
  areaCode: string;
  areaName: string;
  buildingCode: string;
  status: PointStatus;
  currentRound?: RoundWindow;
  nextRound?: RoundWindow;
  lastScan?: LastScan;
  issue?: ReportedIssue;
  rework?: Rework;
}
```

At the end of the file, add:

```ts
/** AttendanceChangedDto: ScanHub's "AttendanceRecorded", to Admins only (facility-0027 rule 5): that shift's board changed. */
export interface AttendanceChanged {
  areaId: number;
  shiftDate: string;
  shift: Shift;
}
```

In `frontend/src/test/cleanerFixtures.ts`, in `point()`, delete these three lines, so the fixture leaves the fields out the way the server does:

```ts
    lastScan: null,
    issue: null,
    rework: null,
```

- [ ] **Step 6: My Building uses its own key**

In `frontend/src/pages/my-building/myBuildingApi.ts`, after the `MyBuilding` interface, add:

```ts
/** My Building's hub connection in hubSlice (plan 11 decision 4); the page's footer reads the same key. */
export const MY_BUILDING_HUB = 'my-building';
```

In the same file, replace:

```ts
        await followScanHub({
          cacheDataLoaded,
```

with:

```ts
        await followScanHub({
          key: MY_BUILDING_HUB,
          cacheDataLoaded,
```

In `frontend/src/pages/my-building/MyBuildingPage.tsx`, replace:

```tsx
import type { HubStatus } from '../../store/slices/hubSlice';
```

with:

```tsx
import { selectHubStatus, type HubStatus } from '../../store/slices/hubSlice';
```

Replace:

```tsx
import { useGetMyBuildingQuery, type MyBuilding } from './myBuildingApi';
```

with:

```tsx
import { MY_BUILDING_HUB, useGetMyBuildingQuery, type MyBuilding } from './myBuildingApi';
```

Replace:

```tsx
  const hubStatus = useAppSelector((state) => state.hub.status);
```

with:

```tsx
  const hubStatus = useAppSelector(selectHubStatus(MY_BUILDING_HUB));
```

- [ ] **Step 7: Run the tests and the type check**

Run: `cd frontend && npx vitest run && npx tsc -b`
Expected:
- The tests PASS, 252 (250 + 2). The My Building tests pass unchanged.
- `tsc` reports nothing. If it finds code or a test that still sets one of the card fields to `null`, change that spot to leave the field out, or to test it by truthiness.

- [ ] **Step 8: Lint and commit**

Add to `## Task log (plan 11)` in `docs/handoff.md`:

```markdown
- <date> · Task 2 done: hubSlice keeps one status per connection key (selectHubStatus, hubFollowEnded), followScanHub takes a key and an optional onPoint and onAttendance (AttendanceRecorded), My Building uses key 'my-building'; PointCard's nullable fields are optional and point() leaves them out (both deferred from plan 9). Frontend tests <n> passed. Next: Task 3 (Dashboard cards with live updates).
```

Then:

```bash
cd frontend && npm run lint
cd .. && git add docs/handoff.md frontend/src/shared/api/types.ts frontend/src/test/cleanerFixtures.ts frontend/src/store/slices/hubSlice.ts frontend/src/shared/api/scanHub.ts frontend/src/shared/api/scanHub.test.ts frontend/src/pages/my-building/myBuildingApi.ts frontend/src/pages/my-building/MyBuildingPage.tsx
git commit -m "feat(frontend): one hub status per connection, AttendanceRecorded, optional card fields"
```

---
### Task 3: Dashboard (D1): every point by Area, live

The Admin's home shows every point's card, grouped by Area. Pushed cards change at once. The 30 s poll and a window focus catch what nobody pushes, such as a round running out.

**Files:**
- Modify: `frontend/src/shared/api/baseApi.ts`
- Modify: `frontend/src/main.tsx`
- Create: `frontend/src/shared/components/LiveBadge.tsx`
- Modify: `frontend/src/shared/components/shared.css`
- Create: `frontend/src/pages/dashboard/dashboardApi.ts`, `frontend/src/pages/dashboard/boardRules.ts`, `frontend/src/pages/dashboard/PointTile.tsx`, `frontend/src/pages/dashboard/dashboard.css`
- Modify: `frontend/src/pages/dashboard/DashboardPage.tsx`
- Create: `frontend/src/test/liveFixtures.ts`
- Modify: `docs/handoff.md`
- Test: `frontend/src/pages/dashboard/boardRules.test.ts`, `frontend/src/pages/dashboard/DashboardPage.test.tsx`

**Interfaces:**
- Consumes:
  - `followScanHub({ key, …, onPoint, onMissed })` and `selectHubStatus(key)` (Task 2).
  - `getValidAccessToken` (`shared/auth/session.ts`), `StatusChip`, and `formatTime`, `formatRound`, `formatClock`.
- Produces:
  - `baseApi` tags `'ServicePoints'`, `'AttendanceBoard'` and `'Alerts'`. Tasks 5–8 use the last two.
  - `livePoll = { refetchOnMountOrArgChange: true, pollingInterval: 30_000, refetchOnFocus: true }` in `shared/api/baseApi.ts`.
  - `LiveBadge({ hubKey }: { hubKey: string })` in `shared/components/LiveBadge.tsx`.
  - `DASHBOARD_HUB = 'service-points'` and `useGetServicePointsQuery()` in `pages/dashboard/dashboardApi.ts`.
  - In `pages/dashboard/boardRules.ts`:
    - `replacePoint(points: PointCard[], card: PointCard): boolean`.
    - `AreaGroup` and `groupByArea(points: readonly PointCard[]): AreaGroup[]`.
  - In `src/test/liveFixtures.ts`: `boardPoints: PointCard[]` and `apiIdle(store: AppStore): Promise<void>`.

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/test/liveFixtures.ts`:

```ts
import { waitFor } from '@testing-library/react';
import { expect } from 'vitest';
import type { PointCard } from '../shared/api/types';
import type { AppStore } from '../store';
import { point } from './cleanerFixtures';
import { lastScanAt } from './supervisorFixtures';

/**
 * Made-up answers for the Admin live board (public repo: no real names). The API sends UTC; the Thai time is in the
 * comments: 03:05Z is 10:05. Fields the server sends as null are left out, as it does.
 */

/** Waits until no request is on its way (no pending entry in the RTK Query cache), so a late answer cannot land after a check. */
export async function apiIdle(store: AppStore): Promise<void> {
  await waitFor(() =>
    expect(Object.values(store.getState().api.queries).some((query) => query?.status === 'pending')).toBe(false),
  );
}

/** Wireframe D1's points in the server's order: AR01 in building A, then AR02 in building B. */
export const boardPoints: PointCard[] = [
  point({ id: 1, name: 'ห้องน้ำชาย', status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:05:00Z') }), // 10:05
  point({
    id: 2,
    name: 'ห้องน้ำหญิง',
    status: 'Passed',
    lastScan: lastScanAt('2026-10-15T03:00:00Z'), // 10:00
    issue: { tags: ['ก๊อกน้ำรั่ว'], note: 'อ่างที่ 2', reportedAt: '2026-10-15T03:00:00Z' },
  }),
  point({
    id: 3,
    name: 'จุดทิ้งขยะ',
    status: 'Rework',
    currentRound: { id: 31, start: '08:00:00', end: '10:00:00' },
    rework: { defect: 'รอบถังขยะยังมีคราบ', requestedAt: '2026-10-15T02:40:00Z' }, // 09:40
  }),
  point({ id: 4, name: 'โถงทางเดิน', status: 'Overdue', currentRound: { id: 41, start: '07:30:00', end: '09:00:00' } }),
  point({
    id: 5,
    name: 'ห้องทำงาน',
    areaId: 2,
    areaCode: 'AR02',
    areaName: 'Office ทดลอง',
    buildingCode: 'B',
    status: 'BeforeFirstRound',
    currentRound: undefined,
    nextRound: { id: 51, start: '11:00:00', end: '12:00:00' },
  }),
  point({
    id: 6,
    name: 'ห้องน้ำ Office',
    areaId: 2,
    areaCode: 'AR02',
    areaName: 'Office ทดลอง',
    buildingCode: 'B',
    status: 'PendingInspection',
    currentRound: { id: 61, start: '08:00:00', end: '09:00:00' },
    lastScan: lastScanAt('2026-10-15T02:20:00Z', { placement: 'Late' }), // 09:20
  }),
  point({
    id: 7,
    name: 'ห้องเก็บของ',
    areaId: 2,
    areaCode: 'AR02',
    areaName: 'Office ทดลอง',
    buildingCode: 'B',
    status: 'OffHours',
    currentRound: undefined,
    nextRound: undefined,
  }),
];
```

Create `frontend/src/pages/dashboard/boardRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { point } from '../../test/cleanerFixtures';
import { boardPoints } from '../../test/liveFixtures';
import { groupByArea, replacePoint } from './boardRules';

describe('Dashboard rules (wireframe D1)', () => {
  it('groups the points by Area in the order the server sends them', () => {
    const groups = groupByArea(boardPoints);

    expect(groups.map((group) => [group.areaCode, group.areaName, group.buildingCode, group.points.length])).toEqual([
      ['AR01', 'Area ทดลอง', 'A', 4],
      ['AR02', 'Office ทดลอง', 'B', 3],
    ]);
    expect(groupByArea([])).toEqual([]);
  });

  it('a pushed card replaces its point in place; a point the list does not have is reported, not added', () => {
    const points = [point({ id: 1, status: 'NotYetDone' }), point({ id: 2 })];

    expect(replacePoint(points, point({ id: 1, status: 'PendingInspection' }))).toBe(true);
    expect(points.map((p) => [p.id, p.status])).toEqual([
      [1, 'PendingInspection'],
      [2, 'NotYetDone'],
    ]);
    expect(replacePoint(points, point({ id: 9 }))).toBe(false);
    expect(points).toHaveLength(2);
  });
});
```

Create `frontend/src/pages/dashboard/DashboardPage.test.tsx`:

```tsx
import { setupListeners } from '@reduxjs/toolkit/query';
import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { PointCard } from '../../shared/api/types';
import type { AppStore } from '../../store';
import { adminSession, buildings } from '../../test/adminFixtures';
import { point } from '../../test/cleanerFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { apiIdle, boardPoints } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';

function serve(answer: PointCard[] = boardPoints, routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/service-points': () => ({ status: 200, body: answer }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    ...routes,
  });
}

const pointGets = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/service-points');

/** The hub's first start asks again (facility-0064): wait for that answer, or it could land after a patch. */
async function settled(server: ReturnType<typeof serve>, store: AppStore) {
  await waitFor(() => expect(pointGets(server)).toHaveLength(2));
  await apiIdle(store);
}

const tileNames = (region: HTMLElement) => within(region).getAllByRole('heading', { level: 3 }).map((h) => h.textContent);

describe('DashboardPage (D1, facility-0058, 0061, 0064)', () => {
  it('shows every point by Area, with its status, round and what happened', async () => {
    serve();
    await renderApp('/dashboard');

    const ar01 = await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });
    expect(tileNames(ar01)).toEqual(['ห้องน้ำชาย', 'ห้องน้ำหญิง', 'จุดทิ้งขยะ', 'โถงทางเดิน']);
    expect(within(ar01).getByText('ตึก A · 4 จุด')).toBeInTheDocument();
    const ar02 = screen.getByRole('region', { name: 'AR02 Office ทดลอง' });
    expect(tileNames(ar02)).toEqual(['ห้องทำงาน', 'ห้องน้ำ Office', 'ห้องเก็บของ']);
    expect(within(ar02).getByText('ตึก B · 3 จุด')).toBeInTheDocument();

    expect(screen.getByText('ส่งงาน 10:05 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(screen.getByText('ตรวจผ่าน · ส่งงาน 10:00 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(screen.getByText('แจ้งปัญหา: ก๊อกน้ำรั่ว · อ่างที่ 2')).toBeInTheDocument();
    expect(screen.getByText('ไม่สะอาด 09:40: “รอบถังขยะยังมีคราบ” · รอแม่บ้านแก้')).toBeInTheDocument();
    expect(screen.getByText('ยังไม่ส่งงาน · เลยเวลารอบแล้ว')).toBeInTheDocument();
    expect(within(ar01).getByText('รอบ 07:30–09:00')).toBeInTheDocument();
    expect(within(ar02).getByText('รอบถัดไป 11:00–12:00')).toBeInTheDocument();
    expect(screen.getByText('รอบแรกของกะเริ่ม 11:00')).toBeInTheDocument();
    expect(screen.getByText('ส่งงาน 09:20 · แม่บ้านทดสอบ · ส่งช้า')).toBeInTheDocument();
    expect(screen.getByText('จุดนี้ไม่มีรอบในกะนี้')).toBeInTheDocument();
    // facility-0059: only the sign's QR opens a point
    expect(within(screen.getByRole('main')).queryAllByRole('link')).toHaveLength(0);
  });

  it('a pushed submission or inspection changes its card at once, without asking the server (#26)', async () => {
    const server = serve();
    const { store } = await renderApp('/dashboard');
    expect(await screen.findByText('SignalR Live')).toBeInTheDocument();
    await settled(server, store);
    expect(hub.latest().tokens).toEqual(['token-admin']);

    act(() => hub.latest().push('InspectionRecorded', { ...boardPoints[0], status: 'Passed' }));

    // RTK Query tells the page about a cache patch on the next frame, so the first look waits for it
    expect(await screen.findByText('ตรวจผ่าน · ส่งงาน 10:05 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(pointGets(server)).toHaveLength(2);
  });

  it('a card for a point the page does not have yet asks the server, for its place in the order', async () => {
    const server = serve();
    const { store } = await renderApp('/dashboard');
    await screen.findByText('SignalR Live');
    await settled(server, store);

    act(() => hub.latest().push('ScanRecorded', point({ id: 8, name: 'ห้องใหม่', status: 'PendingInspection' })));

    await waitFor(() => expect(pointGets(server)).toHaveLength(3));
  });

  it('asks the server every 30 s, for the changes nobody pushes (facility-0061)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      const server = serve();
      const { store } = await renderApp('/dashboard');
      await settled(server, store);

      await act(() => vi.advanceTimersByTimeAsync(30_000));

      await waitFor(() => expect(pointGets(server)).toHaveLength(3));
    } finally {
      vi.useRealTimers();
    }
  });

  it('asks the server again when the window gets focus (facility-0061)', async () => {
    const server = serve();
    const { store } = await renderApp('/dashboard');
    await settled(server, store);
    const stopListening = setupListeners(store.dispatch);
    try {
      act(() => {
        window.dispatchEvent(new Event('focus'));
      });

      await waitFor(() => expect(pointGets(server)).toHaveLength(3));
    } finally {
      stopListening();
    }
  });

  it('the badge says whether pushes arrive, and a reconnect asks the server again (facility-0064)', async () => {
    const server = serve();
    const { store } = await renderApp('/dashboard');
    expect(await screen.findByRole('status')).toHaveTextContent('SignalR Live');
    await settled(server, store);

    act(() => hub.latest().reconnecting());
    expect(screen.getByRole('status')).toHaveTextContent('Connecting...');
    act(() => hub.latest().reconnected());

    expect(screen.getByRole('status')).toHaveTextContent('SignalR Live');
    await waitFor(() => expect(pointGets(server)).toHaveLength(3));
  });

  it("shows the server's message when the list cannot be read", async () => {
    serve(boardPoints, {
      'GET /api/service-points': () => ({ status: 500, body: { message: 'เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง' } }),
    });
    await renderApp('/dashboard');

    expect(await screen.findByRole('alert')).toHaveTextContent('เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง');
  });

  it('an empty site says where to add points', async () => {
    serve([]);
    await renderApp('/dashboard');

    expect(await screen.findByText('ยังไม่มีจุดที่เปิดใช้งาน เพิ่มได้ที่หน้าจัดการ Area และป้าย')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/dashboard`
Expected: FAIL. `./boardRules` does not exist, and the Dashboard still says "หน้านี้กำลังสร้าง".

- [ ] **Step 3: Cache tags and the live poll**

In `frontend/src/shared/api/baseApi.ts`, replace:

```ts
  // MyBuilding: refetched where a live push may have been missed (facility-0064 item 2).
  // AdminSetup: every Admin setup read; every save that changes what they show invalidates it (plan 10 decision 3)
  tagTypes: ['MyBuilding', 'AdminSetup'],
```

with:

```ts
  // MyBuilding, ServicePoints: refetched where a live push may have been missed (facility-0064 item 2).
  // AdminSetup: every Admin setup read; every save that changes what they show invalidates it (plan 10 decision 3).
  // AttendanceBoard (per shift, plan 11 decision 5) and Alerts: the Admin's live board; corrections and covers invalidate them
  tagTypes: ['MyBuilding', 'AdminSetup', 'ServicePoints', 'AttendanceBoard', 'Alerts'],
```

At the end of the file, add:

```ts
/**
 * facility-0061: the Admin's live pages ask again every 30 s and when the window gets focus, beside the hub's pushes,
 * because time-based changes (a round running out) are never pushed. They also ask on every open, like freshOnOpen.
 */
export const livePoll = { refetchOnMountOrArgChange: true, pollingInterval: 30_000, refetchOnFocus: true } as const;
```

In `frontend/src/main.tsx`, after the line `import { createBrowserRouter } from 'react-router';`, add:

```tsx
import { setupListeners } from '@reduxjs/toolkit/query';
```

Replace:

```tsx
const store = makeStore();
```

with:

```tsx
const store = makeStore();
// facility-0061 item 2: refetchOnFocus needs the store to hear the window's focus
setupListeners(store.dispatch);
```

- [ ] **Step 4: The badge**

Create `frontend/src/shared/components/LiveBadge.tsx`:

```tsx
import { useAppSelector } from '../../store/hooks';
import { selectHubStatus } from '../../store/slices/hubSlice';
import './shared.css';

/**
 * facility-0064 item 1: the Admin pages' corner badge, with today's words and colours: "SignalR Live" in green while
 * pushes arrive, "Connecting..." in orange otherwise. Meanwhile the poll keeps the page at most 30 s old (facility-0061).
 */
export function LiveBadge({ hubKey }: { hubKey: string }) {
  const live = useAppSelector(selectHubStatus(hubKey)) === 'live';
  return (
    <span className={live ? 'app-live app-live--on' : 'app-live'} role="status">
      {live ? 'SignalR Live' : 'Connecting...'}
    </span>
  );
}
```

In `frontend/src/shared/components/shared.css`, insert before the line `@media print {`:

```css
.app-live {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border: 1px solid var(--color-warn-border);
  border-radius: 999px;
  background: var(--color-warn-bg);
  color: var(--color-warn-text);
  font-size: 13px;
  font-weight: 600;
  white-space: nowrap;
}

.app-live::before {
  content: '';
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--color-status-overdue);
}

.app-live--on {
  border-color: var(--color-info-border);
  background: var(--color-info-bg);
  color: var(--color-status-passed);
}

.app-live--on::before {
  background: var(--color-status-passed);
}

```

- [ ] **Step 5: The Dashboard's query and rules**

Create `frontend/src/pages/dashboard/boardRules.ts`:

```ts
import type { PointCard } from '../../shared/api/types';

/** A pushed card replaces the same point in place. False when the list does not have that point (#26). */
export function replacePoint(points: PointCard[], card: PointCard): boolean {
  const index = points.findIndex((point) => point.id === card.id);
  if (index < 0) {
    return false;
  }
  points[index] = card;
  return true;
}

/** One Area's section of wireframe D1. */
export interface AreaGroup {
  areaId: number;
  areaCode: string;
  areaName: string;
  buildingCode: string;
  points: PointCard[];
}

/** The server sends points by Area code, then by the Area's own order (PointBoardQuery); each Area becomes one section. */
export function groupByArea(points: readonly PointCard[]): AreaGroup[] {
  const groups: AreaGroup[] = [];
  for (const point of points) {
    const last = groups.at(-1);
    if (last && last.areaId === point.areaId) {
      last.points.push(point);
    } else {
      groups.push({
        areaId: point.areaId,
        areaCode: point.areaCode,
        areaName: point.areaName,
        buildingCode: point.buildingCode,
        points: [point],
      });
    }
  }
  return groups;
}
```

Create `frontend/src/pages/dashboard/dashboardApi.ts`:

```ts
import { baseApi } from '../../shared/api/baseApi';
import { followScanHub } from '../../shared/api/scanHub';
import type { PointCard } from '../../shared/api/types';
import { getValidAccessToken } from '../../shared/auth/session';
import type { RootState } from '../../store';
import { replacePoint } from './boardRules';

/** The Dashboard's hub connection in hubSlice (plan 11 decision 4); its LiveBadge reads the same key. */
export const DASHBOARD_HUB = 'service-points';

const dashboardApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /** facility-0058: every active point of every active Area, as the clock's shift sees it (Admin only, no QR Token). */
    getServicePoints: build.query<PointCard[], void>({
      query: () => '/service-points',
      providesTags: ['ServicePoints'],
      // #26: a pushed card replaces its point; facility-0064: every hub start and reconnect asks again
      async onCacheEntryAdded(_arg, { cacheDataLoaded, cacheEntryRemoved, updateCachedData, getCacheEntry, dispatch, getState }) {
        await followScanHub({
          key: DASHBOARD_HUB,
          cacheDataLoaded,
          cacheEntryRemoved,
          dispatch,
          accessToken: () => getValidAccessToken(() => getState() as RootState, dispatch),
          onPoint: (card) => {
            let found = false;
            updateCachedData((draft) => {
              found = replacePoint(draft, card);
            });
            // A point the list lacks (just activated) needs its place in the server's order. A GET still in flight read
            // the database before this push and would overwrite the patch when it lands (invalidation waits for it)
            if (!found || getCacheEntry().status === 'pending') {
              dispatch(baseApi.util.invalidateTags(['ServicePoints']));
            }
          },
          onMissed: () => dispatch(baseApi.util.invalidateTags(['ServicePoints'])),
        });
      },
    }),
  }),
});

export const { useGetServicePointsQuery } = dashboardApi;
```

- [ ] **Step 6: The card and the page**

Create `frontend/src/pages/dashboard/PointTile.tsx`:

```tsx
import type { Placement, PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatClock, formatRound, formatTime } from '../../shared/utils/thaiTime';

/**
 * One point on wireframe D1. It is not a link: only the sign's QR opens a point (facility-0059). Names, defects and
 * notes are rendered as React text only (facility-0066).
 */
export function PointTile({ point }: { point: PointCard }) {
  const round = roundText(point);
  return (
    <li className="dashboard-tile">
      <div className="dashboard-tile__top">
        <h3 className="dashboard-tile__name">{point.name}</h3>
        <StatusChip status={point.status} />
      </div>
      {round && <span className="dashboard-tile__round">{round}</span>}
      <span className="dashboard-tile__detail">{detailText(point)}</span>
      {point.issue && (
        <span className="dashboard-tile__issue">
          {`แจ้งปัญหา: ${[...point.issue.tags, point.issue.note].filter(Boolean).join(' · ')}`}
        </span>
      )}
    </li>
  );
}

function roundText(point: PointCard): string | null {
  if (point.currentRound) {
    return `รอบ ${formatRound(point.currentRound)}`;
  }
  return point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : null;
}

const placementNote: Record<Placement, string> = { OnTime: '', Late: ' · ส่งช้า', Rework: ' · งานแก้', OffRound: ' · นอกรอบ' };

/** What the card says under its round, by status. The browser computes no time-based status (facility-0061). */
function detailText(point: PointCard): string {
  const sent = point.lastScan
    ? `ส่งงาน ${formatTime(point.lastScan.submittedAt)} · ${point.lastScan.cleanerName}${placementNote[point.lastScan.placement]}`
    : null;
  switch (point.status) {
    case 'PendingInspection':
      return sent ?? 'ส่งงานแล้ว';
    case 'Passed':
      return ['ตรวจผ่าน', sent].filter(Boolean).join(' · ');
    case 'Rework':
      return point.rework
        ? `ไม่สะอาด ${formatTime(point.rework.requestedAt)}: “${point.rework.defect}” · รอแม่บ้านแก้`
        : 'รอแม่บ้านแก้';
    case 'Overdue':
      return 'ยังไม่ส่งงาน · เลยเวลารอบแล้ว';
    case 'NotYetDone':
      return point.currentRound ? `ยังไม่ส่งงาน · ส่งได้ถึง ${formatClock(point.currentRound.end)}` : 'ยังไม่ส่งงาน';
    case 'BeforeFirstRound':
      return point.nextRound ? `รอบแรกของกะเริ่ม ${formatClock(point.nextRound.start)}` : 'รอบแรกของกะยังไม่เริ่ม';
    case 'OffHours':
      return 'จุดนี้ไม่มีรอบในกะนี้';
  }
}
```

Replace the whole of `frontend/src/pages/dashboard/DashboardPage.tsx` with:

```tsx
import { livePoll } from '../../shared/api/baseApi';
import { LiveBadge } from '../../shared/components/LiveBadge';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { groupByArea, type AreaGroup } from './boardRules';
import { DASHBOARD_HUB, useGetServicePointsQuery } from './dashboardApi';
import { PointTile } from './PointTile';
import './dashboard.css';

/** Wireframe D1's grey line, word for word. */
const RULES_NOTE =
  'รอบเป็นช่วงเวลา พ้นเวลาสิ้นสุดแล้วยังไม่ส่ง = เลยรอบ · ส่งหลังช่วงจบนับเป็นรอบนั้นแต่ติดว่าส่งช้า · พอรอบใหม่เริ่ม การ์ดเริ่มใหม่ งานเก่าที่ไม่ได้ตรวจหรือไม่ได้แก้เก็บในประวัติ · ป้ายแดง "แจ้งปัญหา" คือของชำรุดที่แม่บ้านแจ้ง ไม่ใช่สถานะของการ์ด';

/**
 * facility-0058, wireframe D1: every point of every active Area, by Area, as the clock's shift sees it. Pushed cards
 * change at once (#26); every open, the 30 s poll and a window focus catch what nobody pushes (facility-0061). A failed
 * poll keeps the last cards on screen under the error.
 */
export function DashboardPage() {
  const { data, error, isLoading } = useGetServicePointsQuery(undefined, livePoll);

  return (
    <main className="dashboard-page">
      <header className="dashboard-heading">
        <h1>สถานะจุดทำความสะอาด</h1>
        <LiveBadge hubKey={DASHBOARD_HUB} />
      </header>
      <p className="app-muted">{RULES_NOTE}</p>
      {isLoading && <p className="app-muted">กำลังโหลดจุด...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {data && data.length === 0 && (
        <p className="app-note app-note--info">ยังไม่มีจุดที่เปิดใช้งาน เพิ่มได้ที่หน้าจัดการ Area และป้าย</p>
      )}
      {data && groupByArea(data).map((group) => <AreaSection key={group.areaId} group={group} />)}
    </main>
  );
}

function AreaSection({ group }: { group: AreaGroup }) {
  const headingId = `dashboard-area-${group.areaId}`;
  return (
    <section className="dashboard-area" aria-labelledby={headingId}>
      <div className="dashboard-area__head">
        <h2 id={headingId}>{`${group.areaCode} ${group.areaName}`}</h2>
        <span className="app-muted">{`ตึก ${group.buildingCode} · ${group.points.length} จุด`}</span>
      </div>
      <ul className="dashboard-tiles">
        {group.points.map((point) => (
          <PointTile key={point.id} point={point} />
        ))}
      </ul>
    </section>
  );
}
```

Create `frontend/src/pages/dashboard/dashboard.css`:

```css
/* Dashboard (D1). Class prefix: dashboard- */

.dashboard-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.dashboard-heading {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.dashboard-heading h1 {
  font-size: 26px;
  font-weight: 700;
}

.dashboard-area {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.dashboard-area__head {
  display: flex;
  align-items: baseline;
  gap: 12px;
}

.dashboard-area__head h2 {
  font-size: 18px;
  font-weight: 700;
}

.dashboard-tiles {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 12px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.dashboard-tile {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 14px 16px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.dashboard-tile__top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 8px;
}

.dashboard-tile__name {
  font-size: 16px;
  font-weight: 600;
}

.dashboard-tile__round {
  font-size: 13px;
  color: var(--color-muted);
}

.dashboard-tile__detail {
  font-size: 14px;
}

.dashboard-tile__issue {
  align-self: flex-start;
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  background: var(--color-error-bg);
  color: var(--color-error-text);
  font-size: 13px;
  font-weight: 600;
}
```

- [ ] **Step 7: Run the tests**

Run: `cd frontend && npx vitest run`
Expected: PASS, 262 tests (252 + 2 rules + 8 page tests).

- [ ] **Step 8: Measure, lint and commit**

Run: `cd frontend && npm run build && grep -o 'assets/index-[A-Za-z0-9_-]*\.js' dist/index.html`. Note the main file's gzip size from the build report; the Dashboard is in its own file. Then run `rm -rf dist`.

Add to `## Task log (plan 11)` in `docs/handoff.md`:

```markdown
- <date> · Task 3 done: Dashboard D1 (/dashboard): every point by Area (server order), status chip, round, what happened by status, issue tag, no links; getServicePoints follows the hub (key 'service-points'): a pushed card replaces its point, an unknown point or a push during a GET asks again; livePoll (every open, 30 s, focus) with setupListeners in main.tsx; LiveBadge "SignalR Live" / "Connecting..." (facility-0064); tags ServicePoints, AttendanceBoard, Alerts. Main JS <size> kB gzip. Frontend tests <n> passed. Next: Task 4 (Dashboard filters).
```

Then:

```bash
cd frontend && npm run lint
cd .. && git add docs/handoff.md frontend/src/main.tsx frontend/src/shared/api/baseApi.ts frontend/src/shared/components/LiveBadge.tsx frontend/src/shared/components/shared.css frontend/src/pages/dashboard frontend/src/test/liveFixtures.ts
git commit -m "feat(frontend): Dashboard D1 shows every point by Area, live (hub, 30 s poll, focus)"
```

---
### Task 4: Dashboard filters: building, status, issue (facility-0024)

The legend becomes the status filter, with counts. Next to it are a building picker and "มีปัญหาที่แจ้ง". The choices live in `dashboardSlice`, so they are still set when the Admin comes back from another page.

**Files:**
- Create: `frontend/src/store/slices/dashboardSlice.ts`
- Modify: `frontend/src/store/index.ts`
- Modify: `frontend/src/pages/dashboard/boardRules.ts`, `frontend/src/pages/dashboard/boardRules.test.ts`
- Create: `frontend/src/pages/dashboard/Filters.tsx`
- Modify: `frontend/src/pages/dashboard/DashboardPage.tsx`, `frontend/src/pages/dashboard/dashboard.css`
- Modify: `docs/handoff.md`
- Test: `frontend/src/pages/dashboard/DashboardFilters.test.tsx`

**Interfaces:**
- Consumes:
  - `useGetBuildingsQuery()` (`shared/api/adminSetupApi.ts`) and `pointStatusLabel` (`shared/utils/labels.ts`).
  - `sessionExpired` and `signedOut` (`store/slices/authSlice.ts`).
- Produces:
  - In `store/slices/dashboardSlice.ts`:
    - `DashboardState { buildingCode: string; status: PointStatus | 'all'; issueOnly: boolean }`, where `''` means every building.
    - Actions `buildingChosen(code)`, `statusToggled(status)` and `issueOnlyToggled()`.
    - `dashboardReducer`, at `state.dashboard`.
  - In `pages/dashboard/boardRules.ts`:
    - `legendOrder: readonly PointStatus[]`.
    - `inBuilding(points, buildingCode)` and `statusCounts(points): Record<PointStatus, number>`.
    - `filterPoints(points, filters: DashboardState): PointCard[]`.

- [ ] **Step 1: Write the failing tests**

In `frontend/src/pages/dashboard/boardRules.test.ts`, replace the import line:

```ts
import { groupByArea, replacePoint } from './boardRules';
```

with:

```ts
import { filterPoints, groupByArea, inBuilding, replacePoint, statusCounts } from './boardRules';
```

Inside the `describe` block, after the last test, add:

```ts
  it('counts each status, inside the chosen building', () => {
    expect(statusCounts(boardPoints)).toEqual({
      BeforeFirstRound: 1,
      NotYetDone: 0,
      PendingInspection: 2,
      Passed: 1,
      Rework: 1,
      Overdue: 1,
      OffHours: 1,
    });
    expect(statusCounts(inBuilding(boardPoints, 'B')).PendingInspection).toBe(1);
    expect(inBuilding(boardPoints, '')).toHaveLength(7);
  });

  it('keeps the cards of the building, the status and the issue tag together', () => {
    const names = (filters: Parameters<typeof filterPoints>[1]) => filterPoints(boardPoints, filters).map((p) => p.name);

    expect(names({ buildingCode: '', status: 'all', issueOnly: false })).toHaveLength(7);
    expect(names({ buildingCode: '', status: 'PendingInspection', issueOnly: false })).toEqual(['ห้องน้ำชาย', 'ห้องน้ำ Office']);
    expect(names({ buildingCode: 'B', status: 'PendingInspection', issueOnly: false })).toEqual(['ห้องน้ำ Office']);
    expect(names({ buildingCode: '', status: 'all', issueOnly: true })).toEqual(['ห้องน้ำหญิง']);
    expect(names({ buildingCode: 'B', status: 'all', issueOnly: true })).toEqual([]);
  });
```

Create `frontend/src/pages/dashboard/DashboardFilters.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeServer } from '../../test/fakeServer';
import { boardPoints } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';

function serve() {
  return fakeServer({
    ...adminSession,
    'GET /api/service-points': () => ({ status: 200, body: boardPoints }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'POST /api/auth/logout': () => ({ status: 204 }),
  });
}

const tileNames = () => within(screen.getByRole('main')).queryAllByRole('heading', { level: 3 }).map((h) => h.textContent);
const legend = () => within(screen.getByRole('list', { name: 'สถานะ' }));

describe('Dashboard filters (D1, facility-0024)', () => {
  it('the legend counts each status; choosing one shows only those cards, choosing it again shows all', async () => {
    serve();
    const { user } = await renderApp('/dashboard');
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });

    expect(legend().getAllByRole('button').map((button) => button.textContent)).toEqual([
      'ยังไม่ถึงรอบ 1',
      'ยังไม่ทำ 0',
      'รอตรวจ 2',
      'ตรวจผ่าน 1',
      'ต้องแก้ไข 1',
      'เลยรอบ 1',
      'นอกเวลา 1',
    ]);

    await user.click(legend().getByRole('button', { name: 'รอตรวจ 2' }));

    expect(tileNames()).toEqual(['ห้องน้ำชาย', 'ห้องน้ำ Office']);
    expect(legend().getByRole('button', { name: 'รอตรวจ 2' })).toHaveAttribute('aria-pressed', 'true');

    await user.click(legend().getByRole('button', { name: 'ต้องแก้ไข 1' }));
    expect(tileNames()).toEqual(['จุดทิ้งขยะ']);

    await user.click(legend().getByRole('button', { name: 'ต้องแก้ไข 1' }));
    expect(tileNames()).toHaveLength(7);
  });

  it('the building picker narrows the cards and the counts', async () => {
    serve();
    const { user } = await renderApp('/dashboard');
    await screen.findByRole('option', { name: 'B' });

    await user.selectOptions(screen.getByRole('combobox', { name: 'ตึก' }), 'B');

    expect(screen.queryByRole('region', { name: 'AR01 Area ทดลอง' })).not.toBeInTheDocument();
    expect(tileNames()).toEqual(['ห้องทำงาน', 'ห้องน้ำ Office', 'ห้องเก็บของ']);
    expect(legend().getByRole('button', { name: 'รอตรวจ 1' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'มีปัญหาที่แจ้ง 0' })).toBeInTheDocument();
  });

  it('มีปัญหาที่แจ้ง keeps the cards with an issue the Cleaner reported (facility-0046)', async () => {
    serve();
    const { user } = await renderApp('/dashboard');

    await user.click(await screen.findByRole('button', { name: 'มีปัญหาที่แจ้ง 1' }));

    expect(tileNames()).toEqual(['ห้องน้ำหญิง']);
    expect(screen.getByRole('button', { name: 'มีปัญหาที่แจ้ง 1' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('a filter that leaves nothing says so', async () => {
    serve();
    const { user } = await renderApp('/dashboard');
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });

    await user.click(legend().getByRole('button', { name: 'ยังไม่ทำ 0' }));

    expect(screen.getByText('ไม่มีจุดตามตัวกรองนี้')).toBeInTheDocument();
  });

  it('the filters are still set after another page, and cleared by logout (facility-0024)', async () => {
    serve();
    const { user, store } = await renderApp('/dashboard');
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });
    await user.click(legend().getByRole('button', { name: 'รอตรวจ 2' }));

    await user.click(screen.getByRole('link', { name: 'บัญชีผู้ใช้' }));
    await screen.findByRole('heading', { level: 1, name: 'บัญชีผู้ใช้' });
    await user.click(screen.getByRole('link', { name: 'สถานะจุด' }));
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });

    expect(tileNames()).toEqual(['ห้องน้ำชาย', 'ห้องน้ำ Office']);

    await user.click(screen.getByRole('button', { name: 'ออกจากระบบ' }));
    await waitFor(() => expect(store.getState().dashboard).toEqual({ buildingCode: '', status: 'all', issueOnly: false }));
  });
});
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/dashboard`
Expected: FAIL. `statusCounts`, `inBuilding` and `filterPoints` do not exist, there is no legend, and `state.dashboard` is undefined.

- [ ] **Step 3: The slice**

Create `frontend/src/store/slices/dashboardSlice.ts`:

```ts
import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { PointStatus } from '../../shared/api/types';
import { sessionExpired, signedOut } from './authSlice';

/**
 * facility-0024 item 2: the Dashboard's filters live in a slice, so they are still set when the Admin comes back from
 * another page. buildingCode '' is every building; status 'all' is every status; issueOnly keeps the cards with an issue
 * the Cleaner reported (facility-0046). A session that ends forgets them.
 */
export interface DashboardState {
  buildingCode: string;
  status: PointStatus | 'all';
  issueOnly: boolean;
}

const initialState: DashboardState = { buildingCode: '', status: 'all', issueOnly: false };

const dashboardSlice = createSlice({
  name: 'dashboard',
  initialState,
  reducers: {
    buildingChosen(state, action: PayloadAction<string>) {
      state.buildingCode = action.payload;
    },
    /** Pressing the chosen status again shows every status. */
    statusToggled(state, action: PayloadAction<PointStatus>) {
      state.status = state.status === action.payload ? 'all' : action.payload;
    },
    issueOnlyToggled(state) {
      state.issueOnly = !state.issueOnly;
    },
  },
  extraReducers: (builder) => {
    builder.addCase(sessionExpired, () => initialState).addCase(signedOut, () => initialState);
  },
});

export const { buildingChosen, statusToggled, issueOnlyToggled } = dashboardSlice.actions;
export const dashboardReducer = dashboardSlice.reducer;
```

In `frontend/src/store/index.ts`, after the line `import { authReducer, sessionExpired, signedOut } from './slices/authSlice';`, add:

```ts
import { dashboardReducer } from './slices/dashboardSlice';
```

Replace:

```ts
  hub: hubReducer,
```

with:

```ts
  hub: hubReducer,
  dashboard: dashboardReducer,
```

- [ ] **Step 4: The filter rules**

In `frontend/src/pages/dashboard/boardRules.ts`, replace the first line:

```ts
import type { PointCard } from '../../shared/api/types';
```

with:

```ts
import type { PointCard, PointStatus } from '../../shared/api/types';
import type { DashboardState } from '../../store/slices/dashboardSlice';
```

At the end of the file, add:

```ts
/** Wireframe D1's legend order: not started, to do, waiting, done, then the ones that need someone, then off hours. */
export const legendOrder: readonly PointStatus[] = [
  'BeforeFirstRound',
  'NotYetDone',
  'PendingInspection',
  'Passed',
  'Rework',
  'Overdue',
  'OffHours',
];

/** The cards of one building; '' is every building. */
export function inBuilding(points: readonly PointCard[], buildingCode: string): PointCard[] {
  return buildingCode === '' ? [...points] : points.filter((point) => point.buildingCode === buildingCode);
}

export function statusCounts(points: readonly PointCard[]): Record<PointStatus, number> {
  const counts = Object.fromEntries(legendOrder.map((status) => [status, 0])) as Record<PointStatus, number>;
  for (const point of points) {
    counts[point.status] += 1;
  }
  return counts;
}

/** The cards to show: the building, then the status and the issue tag (the legend counts inside the building). */
export function filterPoints(points: readonly PointCard[], filters: DashboardState): PointCard[] {
  return inBuilding(points, filters.buildingCode).filter(
    (point) => (filters.status === 'all' || point.status === filters.status) && (!filters.issueOnly || Boolean(point.issue)),
  );
}
```

- [ ] **Step 5: The filter bar and the page**

Create `frontend/src/pages/dashboard/Filters.tsx`:

```tsx
import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import type { PointCard } from '../../shared/api/types';
import { pointStatusLabel } from '../../shared/utils/labels';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { buildingChosen, issueOnlyToggled, statusToggled } from '../../store/slices/dashboardSlice';
import { inBuilding, legendOrder, statusCounts } from './boardRules';

/** Wireframe D1's filter row: the building, the legend as status buttons with counts, and "มีปัญหาที่แจ้ง" (facility-0024). */
export function Filters({ points }: { points: PointCard[] }) {
  const filters = useAppSelector((state) => state.dashboard);
  const dispatch = useAppDispatch();
  const { data: buildings } = useGetBuildingsQuery();
  const shown = inBuilding(points, filters.buildingCode);
  const counts = statusCounts(shown);
  const issues = shown.filter((point) => point.issue).length;

  return (
    <div className="dashboard-filters">
      <label className="dashboard-filter">
        ตึก
        <select
          className="app-input"
          value={filters.buildingCode}
          onChange={(event) => dispatch(buildingChosen(event.target.value))}
        >
          <option value="">ทุกตึก</option>
          {(buildings ?? []).map((building) => (
            <option key={building.id} value={building.code}>
              {building.code}
            </option>
          ))}
        </select>
      </label>
      <ul className="dashboard-legend" aria-label="สถานะ">
        {legendOrder.map((status) => (
          <li key={status}>
            <button
              type="button"
              className="dashboard-legend__item"
              aria-pressed={filters.status === status}
              onClick={() => dispatch(statusToggled(status))}
            >
              <span className={`dashboard-dot dashboard-dot--${status}`} />
              {`${pointStatusLabel[status]} ${counts[status]}`}
            </button>
          </li>
        ))}
      </ul>
      <button
        type="button"
        className="dashboard-issue"
        aria-pressed={filters.issueOnly}
        onClick={() => dispatch(issueOnlyToggled())}
      >
        {`มีปัญหาที่แจ้ง ${issues}`}
      </button>
    </div>
  );
}
```

In `frontend/src/pages/dashboard/DashboardPage.tsx`, replace:

```tsx
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { groupByArea, type AreaGroup } from './boardRules';
import { DASHBOARD_HUB, useGetServicePointsQuery } from './dashboardApi';
```

with:

```tsx
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useAppSelector } from '../../store/hooks';
import { filterPoints, groupByArea, type AreaGroup } from './boardRules';
import { DASHBOARD_HUB, useGetServicePointsQuery } from './dashboardApi';
import { Filters } from './Filters';
```

Replace:

```tsx
  const { data, error, isLoading } = useGetServicePointsQuery(undefined, livePoll);
```

with:

```tsx
  const { data, error, isLoading } = useGetServicePointsQuery(undefined, livePoll);
  const filters = useAppSelector((state) => state.dashboard);
  const shown = data ? filterPoints(data, filters) : [];
```

Replace:

```tsx
      <p className="app-muted">{RULES_NOTE}</p>
```

with:

```tsx
      {data && <Filters points={data} />}
      <p className="app-muted">{RULES_NOTE}</p>
```

Replace:

```tsx
      {data && groupByArea(data).map((group) => <AreaSection key={group.areaId} group={group} />)}
```

with:

```tsx
      {data && data.length > 0 && shown.length === 0 && <p className="app-note app-note--info">ไม่มีจุดตามตัวกรองนี้</p>}
      {groupByArea(shown).map((group) => (
        <AreaSection key={group.areaId} group={group} />
      ))}
```

Append to `frontend/src/pages/dashboard/dashboard.css`:

```css

.dashboard-filters {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  font-size: 14px;
}

.dashboard-filter {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.dashboard-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.dashboard-legend__item,
.dashboard-issue {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 32px;
  padding: 0 10px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-surface);
  color: var(--color-text);
  font-size: 13px;
  cursor: pointer;
}

.dashboard-issue {
  border-color: var(--color-error-border);
  color: var(--color-error-text);
}

.dashboard-legend__item[aria-pressed='true'],
.dashboard-issue[aria-pressed='true'] {
  border-color: var(--color-text);
  background: var(--color-text);
  color: var(--color-surface);
}

.dashboard-dot {
  width: 10px;
  height: 10px;
  box-sizing: border-box;
  border-radius: 50%;
  background: var(--color-status-idle);
}

.dashboard-dot--Rework {
  background: var(--color-status-rework);
}

.dashboard-dot--Overdue {
  background: var(--color-status-overdue);
}

.dashboard-dot--PendingInspection {
  background: var(--color-status-pending);
}

.dashboard-dot--Passed {
  background: var(--color-status-passed);
}

.dashboard-dot--BeforeFirstRound {
  border: 1px dashed var(--color-status-idle);
  background: transparent;
}

.dashboard-dot--OffHours {
  opacity: 0.5;
}
```

- [ ] **Step 6: Run the tests**

Run: `cd frontend && npx vitest run`
Expected: PASS, 269 tests (262 + 2 rules + 5 filter tests). The Task 3 page tests still pass, because no filter is set.

- [ ] **Step 7: Lint and commit**

Add to `## Task log (plan 11)` in `docs/handoff.md`:

```markdown
- <date> · Task 4 done: Dashboard filters in dashboardSlice (facility-0024): building (by code), one status from the legend buttons with counts inside the building, มีปัญหาที่แจ้ง; kept across pages, cleared when the session ends; "ไม่มีจุดตามตัวกรองนี้". Frontend tests <n> passed. Next: Task 5 (attendance page D2).
```

Then:

```bash
cd frontend && npm run lint
cd .. && git add docs/handoff.md frontend/src/store frontend/src/pages/dashboard
git commit -m "feat(frontend): Dashboard filters by building, status and reported issue, kept across pages"
```

---
### Task 5: Attendance page (D2): who is in each Area, live

The page shows every Area that works the shift, with the Areas that have nobody first. Each row has its regular Cleaner's four times, the covers and the GPS flags. The page follows `AttendanceRecorded` and polls every 30 s. The date, shift and building are in the URL.

**Files:**
- Modify: `frontend/src/shared/utils/thaiTime.ts`, `frontend/src/shared/utils/thaiTime.test.ts`
- Create: `frontend/src/shared/api/attendanceBoardApi.ts`
- Create: `frontend/src/pages/attendance/AttendancePage.tsx`, `frontend/src/pages/attendance/BoardControls.tsx`, `frontend/src/pages/attendance/AttendanceTable.tsx`, `frontend/src/pages/attendance/attendanceRules.ts`, `frontend/src/pages/attendance/attendance.css`, `frontend/src/pages/attendance/index.ts`
- Modify: `frontend/src/router.tsx`, `frontend/src/shared/components/AdminNav.tsx`, `frontend/src/index.css`
- Modify: `frontend/src/test/liveFixtures.ts`
- Modify: `docs/handoff.md`
- Test: `frontend/src/pages/attendance/attendanceRules.test.ts`, `frontend/src/pages/attendance/AttendancePage.test.tsx`, `frontend/src/router.test.tsx`

**Interfaces:**
- Consumes:
  - `followScanHub({ key, onAttendance, onMissed })` (Task 2), `livePoll` and the `AttendanceBoard` tag (Task 3), and `LiveBadge` (Task 3).
  - `AssignedCleaner` and `useGetBuildingsQuery` (`shared/api/adminSetupApi.ts`), and `eventLabel`, `attendanceEvents`, `shiftLabel` and `shiftShortLabel` (`shared/utils/labels.ts`).
- Produces:
  - In `shared/utils/thaiTime.ts`:
    - `formatShiftDate('2026-10-15')` gives `'15/10/2026'`.
    - `parseShiftDate('15/10/2026')` gives `'2026-10-15'`, or `null` when the text is not a real date in that form.
  - In `shared/api/attendanceBoardApi.ts`:
    - Types `AreaAttendanceState`, `BoardEvent`, `BoardCover`, `BoardRow`, `BoardSummary`, `AttendanceBoard` and `BoardArgs { shiftDate?: string; shift?: Shift; buildingId?: number }`.
    - `boardTag(shiftDate, shift)`, `attendanceHubKey(args)` and `useGetAttendanceBoardQuery(args)`.
  - In `pages/attendance/attendanceRules.ts`:
    - `areaStateLabel` and `readBoardArgs(params: URLSearchParams): BoardArgs`.
    - `toBoardParams(args: BoardArgs): URLSearchParams`, `searchRows(rows, text)`, `eventOf(row, type)`, `flagText(row)` and `coverLine(cover)`.
  - `AttendanceTable({ rows, onCorrect? })`. Task 6 passes `onCorrect`.
  - The route `/admin/attendance` (lazy) and the menu link "การเข้างาน".
  - Fixtures in `src/test/liveFixtures.ts`:
    - `boardEvent()`.
    - The rows `noCleanerRow`, `emptyRow`, `workingRow`, `coveredRow`, `correctedRow` and `breakRow`, and the list `boardRows`.
    - `attendanceBoard(rows?, overrides?)`.

- [ ] **Step 1: Write the failing tests**

In `frontend/src/shared/utils/thaiTime.test.ts`, replace:

```ts
import { formatDate, formatMinutes, formatRound, formatTime } from './thaiTime';
```

with:

```ts
import { formatDate, formatMinutes, formatRound, formatShiftDate, formatTime, parseShiftDate } from './thaiTime';
```

Inside the `describe('thaiTime', …)` block, add:

```ts
  it('formatShiftDate writes a shift date from the server (already Thai) as day/month/year', () => {
    expect(formatShiftDate('2026-10-05')).toBe('05/10/2026');
  });

  it('parseShiftDate reads a typed day/month/year, and refuses what is not a real date in that form', () => {
    expect(parseShiftDate('15/10/2026')).toBe('2026-10-15');
    expect(parseShiftDate(' 01/01/2027 ')).toBe('2027-01-01');
    expect(parseShiftDate('31/02/2026')).toBeNull();
    expect(parseShiftDate('5/10/2026')).toBeNull();
    expect(parseShiftDate('2026-10-15')).toBeNull();
    expect(parseShiftDate('')).toBeNull();
  });
```

Append to `frontend/src/test/liveFixtures.ts`:

```ts
/** One attendance scan (BoardEventDto): by default scanned within 6 m of the sign. */
export function boardEvent(
  id: number,
  eventType: AttendanceEvent,
  occurredAt: string,
  overrides: Partial<BoardEvent> = {},
): BoardEvent {
  return { id, eventType, occurredAt, source: 'Scan', withinRadius: true, distanceM: 6, ...overrides };
}

/** AR09 has no regular Cleaner for the shift (the server leaves `cleaner` out). */
export const noCleanerRow: BoardRow = {
  areaId: 9,
  areaCode: 'AR09',
  areaName: 'Area เก้า',
  buildingCode: 'A',
  state: 'Empty',
  events: [],
  covers: [],
};

/** AR12: its Cleaner has not come and nobody covers (wireframe D2's first row). */
export const emptyRow: BoardRow = {
  areaId: 12,
  areaCode: 'AR12',
  areaName: 'Area สิบสอง',
  buildingCode: 'B',
  cleaner: { id: 26, employeeId: '650006', displayName: 'แม่บ้านลา' },
  state: 'Empty',
  events: [],
  covers: [],
};

/** AR01: checked in 06:52, 85 m from the sign (a flag, facility-0035). */
export const workingRow: BoardRow = {
  areaId: 1,
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  buildingCode: 'A',
  cleaner: { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ' },
  state: 'Working',
  events: [boardEvent(1001, 'ShiftIn', '2026-10-14T23:52:00Z', { withinRadius: false, distanceM: 85 })],
  covers: [],
};

/** AR02: its Cleaner is away; แม่บ้านที่อื่น (AR03) covers since 07:10 and checked in at 06:50. */
export const coveredRow: BoardRow = {
  areaId: 2,
  areaCode: 'AR02',
  areaName: 'Office ทดลอง',
  buildingCode: 'B',
  cleaner: { id: 27, employeeId: '650007', displayName: 'แม่บ้านป่วย' },
  state: 'Covered',
  events: [],
  covers: [
    {
      id: 71,
      userId: 25,
      employeeId: '650005',
      displayName: 'แม่บ้านที่อื่น',
      ownAreaCode: 'AR03',
      assignedAt: '2026-10-15T00:10:00Z',
      shiftInAt: '2026-10-14T23:50:00Z',
    },
  ],
};

/** AR05: Shift-In 07:02 added by an Admin (facility-0051), so it has no GPS verdict. */
export const correctedRow: BoardRow = {
  areaId: 5,
  areaCode: 'AR05',
  areaName: 'Area ห้า',
  buildingCode: 'A',
  cleaner: { id: 29, employeeId: '650009', displayName: 'แม่บ้านแบตหมด' },
  state: 'Working',
  events: [boardEvent(1004, 'ShiftIn', '2026-10-15T00:02:00Z', { source: 'AdminAdd', withinRadius: undefined, distanceM: undefined })],
  covers: [],
};

/** AR07: in 06:55, on a break since 09:30. */
export const breakRow: BoardRow = {
  areaId: 7,
  areaCode: 'AR07',
  areaName: 'Area เจ็ด',
  buildingCode: 'A',
  cleaner: { id: 28, employeeId: '650008', displayName: 'แม่บ้านพัก' },
  state: 'OnBreak',
  events: [boardEvent(1002, 'ShiftIn', '2026-10-14T23:55:00Z'), boardEvent(1003, 'BreakOut', '2026-10-15T02:30:00Z')],
  covers: [],
};

/** In the server's order: Areas with nobody first, then by Area code. */
export const boardRows: BoardRow[] = [noCleanerRow, emptyRow, workingRow, coveredRow, correctedRow, breakRow];

/** The Day shift of 15/10/2026 as the clock sees it, with the summary the server would count from these rows. */
export function attendanceBoard(rows: BoardRow[] = boardRows, overrides: Partial<AttendanceBoard> = {}): AttendanceBoard {
  const count = (...states: AreaAttendanceState[]) => rows.filter((row) => states.includes(row.state)).length;
  return {
    shiftDate: '2026-10-15',
    shift: 'Day',
    shiftEnded: false,
    summary: {
      required: rows.length,
      present: count('Working', 'OnBreak', 'Done'),
      empty: count('Empty'),
      covered: count('Covered'),
    },
    rows,
    ...overrides,
  };
}
```

At the top of `frontend/src/test/liveFixtures.ts`, replace:

```ts
import type { PointCard } from '../shared/api/types';
```

with:

```ts
import type {
  AreaAttendanceState,
  AttendanceBoard,
  BoardEvent,
  BoardRow,
} from '../shared/api/attendanceBoardApi';
import type { AttendanceEvent, PointCard } from '../shared/api/types';
```

Create `frontend/src/pages/attendance/attendanceRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { boardRows, coveredRow, correctedRow, emptyRow, workingRow } from '../../test/liveFixtures';
import { coverLine, flagText, readBoardArgs, searchRows, toBoardParams } from './attendanceRules';

describe('attendance rules (wireframe D2)', () => {
  it('reads the shift from the URL only when the date and the shift come together, as the server wants', () => {
    expect(readBoardArgs(new URLSearchParams('date=2026-10-14&shift=Night&building=2'))).toEqual({
      shiftDate: '2026-10-14',
      shift: 'Night',
      buildingId: 2,
    });
    expect(readBoardArgs(new URLSearchParams('date=2026-10-14'))).toEqual({});
    expect(readBoardArgs(new URLSearchParams('date=14/10/2026&shift=Day'))).toEqual({});
    expect(readBoardArgs(new URLSearchParams('shift=Evening&date=2026-10-14&building=x'))).toEqual({});
    expect(readBoardArgs(new URLSearchParams(''))).toEqual({});
  });

  it('writes the URL back in the same words', () => {
    expect(toBoardParams({ shiftDate: '2026-10-14', shift: 'Night', buildingId: 2 }).toString()).toBe(
      'date=2026-10-14&shift=Night&building=2',
    );
    expect(toBoardParams({ buildingId: 1 }).toString()).toBe('building=1');
    expect(toBoardParams({}).toString()).toBe('');
  });

  it('search keeps the rows whose Area, Cleaner or cover matches, in any letter case', () => {
    expect(searchRows(boardRows, 'ar12').map((row) => row.areaCode)).toEqual(['AR12']);
    expect(searchRows(boardRows, 'ที่อื่น').map((row) => row.areaCode)).toEqual(['AR02']);
    expect(searchRows(boardRows, '  ')).toHaveLength(6);
  });

  it('names the scans outside the radius, and each cover with its own Area and check-in', () => {
    expect(flagText(workingRow)).toBe('เข้างาน ห่างป้าย 85 ม.');
    expect(flagText(emptyRow)).toBe('');
    expect(flagText(correctedRow)).toBe(''); // no GPS verdict is not a flag
    expect(coverLine(coveredRow.covers[0])).toBe('ทำแทน: แม่บ้านที่อื่น (AR03) · Admin มอบหมาย 07:10 · เข้างาน 06:50');
    expect(coverLine({ ...coveredRow.covers[0], ownAreaCode: undefined, shiftInAt: undefined })).toBe(
      'ทำแทน: แม่บ้านที่อื่น · Admin มอบหมาย 07:10 · ยังไม่ลงเวลา',
    );
  });
});
```

Create `frontend/src/pages/attendance/AttendancePage.test.tsx`:

```tsx
import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { apiIdle, attendanceBoard, workingRow } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/attendance': (request) => ({
      status: 200,
      body: request.search.includes('shift=Night') ? attendanceBoard([workingRow], { shift: 'Night' }) : attendanceBoard(),
    }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    ...routes,
  });
}

const boardCalls = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/admin/attendance');

/** The table row whose Area cell starts with this code. */
function rowOf(code: string): HTMLElement {
  const row = screen
    .getAllByRole('row')
    .find((candidate) => within(candidate).queryByRole('rowheader')?.textContent?.startsWith(`${code} `));
  if (!row) {
    throw new Error(`no row for ${code}`);
  }
  return row;
}

/** The row's cells after the Area: Cleaner, status, the four times, the flag. */
const cells = (code: string) => within(rowOf(code)).getAllByRole('cell').slice(0, 7).map((cell) => cell.textContent);

describe('AttendancePage (D2, facility-0027, 0050)', () => {
  it("shows the clock's shift: Areas with nobody first, each Cleaner's four times, covers and flags", async () => {
    serve();
    await renderApp('/admin/attendance');

    expect(await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00')).toBeInTheDocument();
    expect(
      within(screen.getByRole('list', { name: 'สรุปกะนี้' }))
        .getAllByRole('listitem')
        .map((tile) => tile.textContent),
    ).toEqual([
      'Area ที่ต้องมีคนกะนี้6ไม่รวม Area ที่ปิดกะนี้',
      'มีคนแล้ว3รวมพักเบรก',
      'ยังไม่มีคน2ต้องหาคนทำแทน',
      'มีคนทำแทน1Admin มอบหมายเฉพาะกะนี้',
    ]);
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual([
      'AR09 Area เก้าตึก A',
      'AR12 Area สิบสองตึก B',
      'AR01 Area ทดลองตึก A',
      'AR02 Office ทดลองตึก B',
      'AR05 Area ห้าตึก A',
      'AR07 Area เจ็ดตึก A',
    ]);
    expect(cells('AR09')[0]).toBe('ไม่มีแม่บ้านประจำ');
    expect(cells('AR12')).toEqual(['แม่บ้านลายังไม่ลงเวลา', 'ยังไม่มีคน', '–', '–', '–', '–', '']);
    expect(cells('AR01')).toEqual(['แม่บ้านทดสอบ', 'ทำงานอยู่', '06:52', '–', '–', '–', 'เข้างาน ห่างป้าย 85 ม.']);
    expect(within(rowOf('AR02')).getByText('ทำแทน: แม่บ้านที่อื่น (AR03) · Admin มอบหมาย 07:10 · เข้างาน 06:50')).toBeInTheDocument();
    expect(cells('AR02')[1]).toBe('มีคนทำแทน');
    expect(cells('AR05')[2]).toBe('07:02แก้โดย Admin');
    expect(cells('AR07')).toEqual(['แม่บ้านพัก', 'พักเบรก', '06:55', '09:30', '–', '–', '']);
    // facility-0059: no link to a sign page
    expect(within(screen.getByRole('table')).queryAllByRole('link')).toHaveLength(0);
  });

  it('กะดึก shows the night of the same date, and the address keeps it', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/attendance');
    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');

    await user.click(screen.getByRole('button', { name: 'กะดึก' }));

    expect(await screen.findByText('15/10/2026 · กะดึก 19:00–07:00')).toBeInTheDocument();
    expect(router.state.location.search).toBe('?date=2026-10-15&shift=Night');
    expect(boardCalls(server).at(-1)?.search).toBe('?shiftDate=2026-10-15&shift=Night');
    expect(screen.getByRole('button', { name: 'กะดึก' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('a typed date opens that date; a wrong one is refused before asking; กะปัจจุบัน goes back to the clock', async () => {
    serve();
    const { user, router } = await renderApp('/admin/attendance');
    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');
    const date = screen.getByRole('textbox', { name: 'วันที่' });

    await user.type(date, '31/02/2026');
    await user.click(screen.getByRole('button', { name: 'ดูวันที่นี้' }));
    expect(screen.getByRole('alert')).toHaveTextContent('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
    expect(router.state.location.search).toBe('');

    await user.clear(date);
    await user.type(date, '14/10/2026');
    await user.click(screen.getByRole('button', { name: 'ดูวันที่นี้' }));
    expect(router.state.location.search).toBe('?date=2026-10-14&shift=Day');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    await user.click(await screen.findByRole('button', { name: 'กะปัจจุบัน' }));
    expect(router.state.location.search).toBe('');
  });

  it('the building picker asks the server for that building', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/attendance');
    await screen.findByRole('option', { name: 'B' });

    await user.selectOptions(screen.getByRole('combobox', { name: 'ตึก' }), 'B');

    expect(router.state.location.search).toBe('?building=2');
    await waitFor(() => expect(boardCalls(server).at(-1)?.search).toBe('?buildingId=2'));
  });

  it('search keeps the Areas whose name, Cleaner or cover matches', async () => {
    serve();
    const { user } = await renderApp('/admin/attendance');
    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');

    await user.type(screen.getByRole('searchbox', { name: 'ค้นหา' }), 'ที่อื่น');
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual(['AR02 Office ทดลองตึก B']);

    await user.clear(screen.getByRole('searchbox', { name: 'ค้นหา' }));
    await user.type(screen.getByRole('searchbox', { name: 'ค้นหา' }), 'ไม่มีชื่อนี้');
    expect(screen.getByText('ไม่พบ Area ที่ตรงกับที่ค้นหา')).toBeInTheDocument();
  });

  it('a check-in pushed for the shift on screen asks again; one for another shift does not (facility-0027 rule 5)', async () => {
    const server = serve();
    const { store } = await renderApp('/admin/attendance');
    expect(await screen.findByText('SignalR Live')).toBeInTheDocument();
    await waitFor(() => expect(boardCalls(server)).toHaveLength(2)); // the hub's first start asks again
    await apiIdle(store);

    act(() => hub.latest().push('AttendanceRecorded', { areaId: 1, shiftDate: '2026-10-14', shift: 'Night' }));
    act(() => hub.latest().push('AttendanceRecorded', { areaId: 12, shiftDate: '2026-10-15', shift: 'Day' }));

    await waitFor(() => expect(boardCalls(server)).toHaveLength(3));
    await apiIdle(store);
    expect(boardCalls(server)).toHaveLength(3);
  });

  it('asks the server every 30 s (facility-0061)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      const server = serve();
      const { store } = await renderApp('/admin/attendance');
      await waitFor(() => expect(boardCalls(server)).toHaveLength(2));
      await apiIdle(store);

      await act(() => vi.advanceTimersByTimeAsync(30_000));

      await waitFor(() => expect(boardCalls(server)).toHaveLength(3));
    } finally {
      vi.useRealTimers();
    }
  });
});
```

In `frontend/src/router.test.tsx`, in the test `'an Admin page shows the Admin menu with the open page marked'`, replace:

```tsx
      'สถานะจุด',
      'จัดการ Area และป้าย',
      'บัญชีผู้ใช้',
    ]);
```

with:

```tsx
      'สถานะจุด',
      'การเข้างาน',
      'จัดการ Area และป้าย',
      'บัญชีผู้ใช้',
    ]);
```

In the test `'every Admin page is a lazy route, …'`, replace:

```tsx
      'dashboard',
      'admin/areas/:areaId?',
```

with:

```tsx
      'dashboard',
      'admin/attendance',
      'admin/areas/:areaId?',
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/shared/utils src/pages/attendance src/router.test.tsx`
Expected: FAIL. `formatShiftDate`, `parseShiftDate`, `attendanceBoardApi` and `./attendanceRules` do not exist, `/admin/attendance` is not found, and the menu has no "การเข้างาน".

- [ ] **Step 3: Shift dates**

In `frontend/src/shared/utils/thaiTime.ts`, after the `formatDate` function, add:

```ts
/** A shift date from the server ("2026-10-15", already a Thai calendar date) as "15/10/2026". */
export function formatShiftDate(date: string): string {
  const [year, month, day] = date.split('-');
  return `${day}/${month}/${year}`;
}

/** A date typed as "15/10/2026", as the server's "2026-10-15"; null when it is not a real date in that form. */
export function parseShiftDate(text: string): string | null {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text.trim());
  if (!match) {
    return null;
  }
  const [, day, month, year] = match;
  const iso = `${year}-${month}-${day}`;
  const date = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
  return date.toISOString().slice(0, 10) === iso ? iso : null;
}
```

- [ ] **Step 4: The shared board query**

Create `frontend/src/shared/api/attendanceBoardApi.ts`:

```ts
import type { RootState } from '../../store';
import { getValidAccessToken } from '../auth/session';
import type { AssignedCleaner } from './adminSetupApi';
import { baseApi } from './baseApi';
import { followScanHub } from './scanHub';
import type { AttendanceEvent, Shift } from './types';

/**
 * The attendance board (facility-0027, 0050), read by the attendance page (D2) and the alert page's "Areas with nobody"
 * (D3). Fields the server sends as null arrive missing, so they are optional here.
 */

/** Whether someone is in the Area this shift, never late or early (facility-0050). */
export type AreaAttendanceState = 'Empty' | 'Working' | 'OnBreak' | 'Done' | 'Covered';

/** BoardEventDto. source AdminAdd or AdminEdit is the "แก้โดย Admin" badge (facility-0051); withinRadius false is a flag. */
export interface BoardEvent {
  id: number;
  eventType: AttendanceEvent;
  occurredAt: string;
  source: 'Scan' | 'AdminAdd' | 'AdminEdit';
  withinRadius?: boolean;
  distanceM?: number;
}

/** BoardCoverDto: a Cleaner covering this Area for the shift (facility-0041), with their own Area and their Shift-In. */
export interface BoardCover {
  id: number;
  userId: number;
  employeeId: string;
  displayName: string;
  ownAreaCode?: string;
  assignedAt: string;
  shiftInAt?: string;
}

/** BoardRowDto. cleaner is the Area's regular Cleaner for the shift (missing when none); events are theirs. */
export interface BoardRow {
  areaId: number;
  areaCode: string;
  areaName: string;
  buildingCode: string;
  cleaner?: AssignedCleaner;
  state: AreaAttendanceState;
  events: BoardEvent[];
  covers: BoardCover[];
}

/** BoardSummaryDto: required = present + empty + covered. */
export interface BoardSummary {
  required: number;
  present: number;
  empty: number;
  covered: number;
}

/** AttendanceBoardDto: every active Area that works the shift, the Areas with nobody first. */
export interface AttendanceBoard {
  shiftDate: string;
  shift: Shift;
  shiftEnded: boolean;
  summary: BoardSummary;
  rows: BoardRow[];
}

/** Both shiftDate and shift, or neither (then the shift on the clock); buildingId narrows to one building. */
export interface BoardArgs {
  shiftDate?: string;
  shift?: Shift;
  buildingId?: number;
}

/** The tag of one shift's boards, whatever building they show: AttendanceRecorded names the shift (plan 11 decision 5). */
export function boardTag(shiftDate: string, shift: Shift) {
  return { type: 'AttendanceBoard' as const, id: `${shiftDate}:${shift}` };
}

/** One hub connection per board on screen (plan 11 decision 4); the page's LiveBadge reads the same key. */
export function attendanceHubKey({ shiftDate, shift, buildingId }: BoardArgs): string {
  return `attendance-board:${shiftDate ?? 'now'}:${shift ?? ''}:${buildingId ?? 'all'}`;
}

const attendanceBoardApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getAttendanceBoard: build.query<AttendanceBoard, BoardArgs>({
      query: ({ shiftDate, shift, buildingId }) => ({ url: '/admin/attendance', params: { shiftDate, shift, buildingId } }),
      providesTags: (board) => (board ? [boardTag(board.shiftDate, board.shift)] : ['AttendanceBoard']),
      // facility-0027 rule 5: a check-in, a break or a correction asks again for that shift's boards; facility-0064:
      // every hub start and reconnect asks again. Invalidation waits for a GET in flight, so no older answer wins
      async onCacheEntryAdded(args, { cacheDataLoaded, cacheEntryRemoved, dispatch, getState }) {
        await followScanHub({
          key: attendanceHubKey(args),
          cacheDataLoaded,
          cacheEntryRemoved,
          dispatch,
          accessToken: () => getValidAccessToken(() => getState() as RootState, dispatch),
          onAttendance: (change) => dispatch(baseApi.util.invalidateTags([boardTag(change.shiftDate, change.shift)])),
          onMissed: () => dispatch(baseApi.util.invalidateTags(['AttendanceBoard'])),
        });
      },
    }),
  }),
});

export const { useGetAttendanceBoardQuery } = attendanceBoardApi;
```

- [ ] **Step 5: The page's rules**

Create `frontend/src/pages/attendance/attendanceRules.ts`:

```ts
import type {
  AreaAttendanceState,
  BoardArgs,
  BoardCover,
  BoardEvent,
  BoardRow,
} from '../../shared/api/attendanceBoardApi';
import type { AttendanceEvent, Shift } from '../../shared/api/types';
import { eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';

/** Wireframe D2's status column (facility-0050). */
export const areaStateLabel: Record<AreaAttendanceState, string> = {
  Empty: 'ยังไม่มีคน',
  Working: 'ทำงานอยู่',
  OnBreak: 'พักเบรก',
  Done: 'เลิกงานแล้ว',
  Covered: 'มีคนทำแทน',
};

const SHIFTS: readonly string[] = ['Day', 'Night'];

/**
 * What the address asks for: ?date=2026-10-15&shift=Night&building=2. A date without a shift, or the reverse, is left
 * out, because the server refuses one without the other; then the board is the clock's shift.
 */
export function readBoardArgs(params: URLSearchParams): BoardArgs {
  const args: BoardArgs = {};
  const date = params.get('date');
  const shift = params.get('shift');
  if (date && /^\d{4}-\d{2}-\d{2}$/.test(date) && shift && SHIFTS.includes(shift)) {
    args.shiftDate = date;
    args.shift = shift as Shift;
  }
  const building = Number(params.get('building'));
  if (Number.isInteger(building) && building > 0) {
    args.buildingId = building;
  }
  return args;
}

/** The address for a board, in the words readBoardArgs reads. */
export function toBoardParams({ shiftDate, shift, buildingId }: BoardArgs): URLSearchParams {
  const params = new URLSearchParams();
  if (shiftDate && shift) {
    params.set('date', shiftDate);
    params.set('shift', shift);
  }
  if (buildingId) {
    params.set('building', String(buildingId));
  }
  return params;
}

/** The rows a search keeps: the Area's code or name, its regular Cleaner, or a cover, in any letter case. */
export function searchRows(rows: readonly BoardRow[], search: string): BoardRow[] {
  const text = search.trim().toLowerCase();
  if (text === '') {
    return [...rows];
  }
  return rows.filter((row) =>
    [row.areaCode, row.areaName, row.cleaner?.displayName, ...row.covers.map((cover) => cover.displayName)].some((value) =>
      value?.toLowerCase().includes(text),
    ),
  );
}

/** The regular Cleaner's scan of that event, if there is one. */
export function eventOf(row: BoardRow, type: AttendanceEvent): BoardEvent | undefined {
  return row.events.find((event) => event.eventType === type);
}

/** facility-0035: the scans recorded outside their sign's radius. No verdict (an Admin's time, a sign with no location) is no flag. */
export function flagText(row: BoardRow): string {
  return row.events
    .filter((event) => event.withinRadius === false)
    .map((event) => `${eventLabel[event.eventType]} ห่างป้าย ${event.distanceM ?? '?'} ม.`)
    .join(' · ');
}

/** A cover under the Area's Cleaner (wireframe D2): who, their own Area, when the Admin assigned them, and their Shift-In. */
export function coverLine(cover: BoardCover): string {
  return [
    `ทำแทน: ${cover.displayName}${cover.ownAreaCode ? ` (${cover.ownAreaCode})` : ''}`,
    `Admin มอบหมาย ${formatTime(cover.assignedAt)}`,
    cover.shiftInAt ? `เข้างาน ${formatTime(cover.shiftInAt)}` : 'ยังไม่ลงเวลา',
  ].join(' · ');
}
```

- [ ] **Step 6: The controls, the table and the page**

Create `frontend/src/pages/attendance/BoardControls.tsx`:

```tsx
import { useState } from 'react';
import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import type { AttendanceBoard, BoardArgs } from '../../shared/api/attendanceBoardApi';
import type { Shift } from '../../shared/api/types';
import { shiftShortLabel } from '../../shared/utils/labels';
import { parseShiftDate } from '../../shared/utils/thaiTime';

interface BoardControlsProps {
  board?: AttendanceBoard;
  args: BoardArgs;
  search: string;
  onSearch: (text: string) => void;
  /** Show this shift of this date. */
  onSlot: (shiftDate: string, shift: Shift) => void;
  /** Back to the shift on the clock. */
  onNow: () => void;
  onBuilding: (buildingId: number | undefined) => void;
}

/** Wireframe D2's controls: the shift, a typed date (plan 11 decision 13), the building and the search. */
export function BoardControls({ board, args, search, onSearch, onSlot, onNow, onBuilding }: BoardControlsProps) {
  const { data: buildings } = useGetBuildingsQuery();
  const [dateText, setDateText] = useState('');
  const [dateError, setDateError] = useState<string | null>(null);

  const showDate = () => {
    const date = parseShiftDate(dateText);
    if (!date) {
      setDateError('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
      return;
    }
    setDateError(null);
    onSlot(date, board?.shift ?? 'Day');
  };

  return (
    <div className="attendance-controls">
      <div className="attendance-shifts" role="group" aria-label="กะ">
        {(['Day', 'Night'] as const).map((shift) => (
          <button
            key={shift}
            type="button"
            className="attendance-shift"
            aria-pressed={board?.shift === shift}
            disabled={!board}
            onClick={() => board && onSlot(board.shiftDate, shift)}
          >
            {shiftShortLabel[shift]}
          </button>
        ))}
      </div>
      <label className="attendance-control">
        วันที่
        <input
          className="app-input"
          value={dateText}
          placeholder="วว/ดด/ปปปป"
          inputMode="numeric"
          onChange={(event) => setDateText(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              showDate();
            }
          }}
        />
      </label>
      <button type="button" className="app-button" onClick={showDate}>
        ดูวันที่นี้
      </button>
      {args.shiftDate && (
        <button type="button" className="app-button" onClick={onNow}>
          กะปัจจุบัน
        </button>
      )}
      <label className="attendance-control">
        ตึก
        <select
          className="app-input"
          value={args.buildingId ?? ''}
          onChange={(event) => onBuilding(event.target.value === '' ? undefined : Number(event.target.value))}
        >
          <option value="">ทุกตึก</option>
          {(buildings ?? []).map((building) => (
            <option key={building.id} value={building.id}>
              {building.code}
            </option>
          ))}
        </select>
      </label>
      <label className="attendance-control">
        ค้นหา
        <input
          className="app-input"
          type="search"
          value={search}
          placeholder="ชื่อ Area หรือแม่บ้าน"
          onChange={(event) => onSearch(event.target.value)}
        />
      </label>
      {dateError && (
        <p className="app-note app-note--error" role="alert">
          {dateError}
        </p>
      )}
    </div>
  );
}
```

Create `frontend/src/pages/attendance/AttendanceTable.tsx`:

```tsx
import type { BoardEvent, BoardRow } from '../../shared/api/attendanceBoardApi';
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';
import { areaStateLabel, coverLine, eventOf, flagText } from './attendanceRules';

interface AttendanceTableProps {
  rows: BoardRow[];
  /** "เพิ่มเวลา" / "แก้เวลา" on a row with a regular Cleaner (facility-0051). Without it the table has no action column. */
  onCorrect?: (row: BoardRow) => void;
}

/** Wireframe D2's table, in the server's order: Areas with nobody first (facility-0050). Names are React text only. */
export function AttendanceTable({ rows, onCorrect }: AttendanceTableProps) {
  if (rows.length === 0) {
    return <p className="app-note app-note--info">ไม่พบ Area ที่ตรงกับที่ค้นหา</p>;
  }
  return (
    <table className="app-table attendance-table" aria-label="การเข้างานราย Area">
      <thead>
        <tr>
          <th scope="col">Area</th>
          <th scope="col">แม่บ้าน</th>
          <th scope="col">สถานะ</th>
          {attendanceEvents.map((type) => (
            <th key={type} scope="col">
              {eventLabel[type]}
            </th>
          ))}
          <th scope="col">ธง</th>
          {onCorrect && <th scope="col">แก้ไข</th>}
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.areaId} className={row.state === 'Empty' ? 'attendance-row--empty' : undefined}>
            <th scope="row">
              {`${row.areaCode} ${row.areaName}`}
              <span className="attendance-sub">{`ตึก ${row.buildingCode}`}</span>
            </th>
            <td>
              <Who row={row} />
            </td>
            <td>
              <span className={`attendance-state attendance-state--${row.state}`}>{areaStateLabel[row.state]}</span>
            </td>
            {attendanceEvents.map((type) => (
              <td key={type}>
                <EventTime event={eventOf(row, type)} />
              </td>
            ))}
            <td>{flagText(row)}</td>
            {onCorrect && (
              <td>
                {row.cleaner && (
                  <button
                    type="button"
                    className="app-link-button"
                    aria-label={`${row.events.length === 0 ? 'เพิ่มเวลา' : 'แก้เวลา'} ${row.cleaner.displayName}`}
                    onClick={() => onCorrect(row)}
                  >
                    {row.events.length === 0 ? 'เพิ่มเวลา' : 'แก้เวลา'}
                  </button>
                )}
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function Who({ row }: { row: BoardRow }) {
  return (
    <div className="attendance-who">
      <span>{row.cleaner ? row.cleaner.displayName : 'ไม่มีแม่บ้านประจำ'}</span>
      {row.cleaner && !eventOf(row, 'ShiftIn') && <span className="attendance-sub">ยังไม่ลงเวลา</span>}
      {row.covers.map((cover) => (
        <span key={cover.id} className="attendance-sub">
          {coverLine(cover)}
        </span>
      ))}
    </div>
  );
}

function EventTime({ event }: { event?: BoardEvent }) {
  if (!event) {
    return <>–</>;
  }
  return (
    <>
      {formatTime(event.occurredAt)}
      {event.source !== 'Scan' && <span className="attendance-badge">แก้โดย Admin</span>}
    </>
  );
}
```

Create `frontend/src/pages/attendance/AttendancePage.tsx`:

```tsx
import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import {
  attendanceHubKey,
  useGetAttendanceBoardQuery,
  type AttendanceBoard,
  type BoardArgs,
  type BoardSummary,
} from '../../shared/api/attendanceBoardApi';
import { livePoll } from '../../shared/api/baseApi';
import { LiveBadge } from '../../shared/components/LiveBadge';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { AttendanceTable } from './AttendanceTable';
import { BoardControls } from './BoardControls';
import { readBoardArgs, searchRows, toBoardParams } from './attendanceRules';
import './attendance.css';

/**
 * facility-0027, 0050, wireframe D2: whether each Area that works the shift has someone in it, never late or early.
 * The shift on the clock by default; the address keeps another date, shift or building. Live on AttendanceRecorded,
 * every open, every 30 s and on focus (facility-0061).
 */
export function AttendancePage() {
  const [params, setParams] = useSearchParams();
  const args = readBoardArgs(params);
  const { data: board, error, isLoading } = useGetAttendanceBoardQuery(args, livePoll);
  const [search, setSearch] = useState('');
  const go = (next: BoardArgs) => setParams(toBoardParams(next));

  return (
    <main className="attendance-page">
      <header className="attendance-heading">
        <div>
          {board && <span className="app-muted">{slotLine(board)}</span>}
          <h1>การเข้างาน</h1>
        </div>
        <LiveBadge hubKey={attendanceHubKey(args)} />
      </header>
      <BoardControls
        board={board}
        args={args}
        search={search}
        onSearch={setSearch}
        onSlot={(shiftDate, shift) => go({ ...args, shiftDate, shift })}
        onNow={() => go({ buildingId: args.buildingId })}
        onBuilding={(buildingId) => go({ ...args, buildingId })}
      />
      {isLoading && <p className="app-muted">กำลังโหลด...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {board && (
        <>
          <Tiles summary={board.summary} />
          <AttendanceTable rows={searchRows(board.rows, search)} />
        </>
      )}
      <BoardNotes />
    </main>
  );
}

function slotLine(board: AttendanceBoard): string {
  return `${formatShiftDate(board.shiftDate)} · ${shiftLabel[board.shift]}${board.shiftEnded ? ' · จบกะแล้ว' : ''}`;
}

/** Wireframe D2's four counts (BoardSummaryDto). */
function Tiles({ summary }: { summary: BoardSummary }) {
  return (
    <ul className="attendance-tiles" aria-label="สรุปกะนี้">
      <li className="attendance-tile">
        <span>Area ที่ต้องมีคนกะนี้</span>
        <strong>{summary.required}</strong>
        <span className="app-muted">ไม่รวม Area ที่ปิดกะนี้</span>
      </li>
      <li className="attendance-tile">
        <span>มีคนแล้ว</span>
        <strong>{summary.present}</strong>
        <span className="app-muted">รวมพักเบรก</span>
      </li>
      <li className="attendance-tile attendance-tile--warn">
        <span>ยังไม่มีคน</span>
        <strong>{summary.empty}</strong>
        <span className="app-muted">ต้องหาคนทำแทน</span>
      </li>
      <li className="attendance-tile">
        <span>มีคนทำแทน</span>
        <strong>{summary.covered}</strong>
        <span className="app-muted">Admin มอบหมายเฉพาะกะนี้</span>
      </li>
    </ul>
  );
}

/** Wireframe D2's notes under the table. */
function BoardNotes() {
  return (
    <div className="attendance-notes">
      <span>หน้านี้ดูว่าแต่ละ Area มีคนอยู่แล้วหรือยัง ไม่ใช่ระบบลงเวลาทำงานของบริษัท จึงไม่คิดสายหรือกลับก่อน</span>
      <span>
        เรียง Area ที่ยังไม่มีคนขึ้นก่อน · Area ที่ทำเฉพาะกะเช้าไม่นับเป็น "ต้องมีคน" ในกะดึก · มอบหมายคนทำแทนได้ที่{' '}
        <Link to="/admin/alerts">รายการที่ขึ้นเตือน</Link>
      </span>
      <span>
        ลืมลงเวลาเลิกงาน: ช่องเลิกงานว่างไว้ หมดกะแล้วขึ้นใน <Link to="/admin/alerts">รายการที่ขึ้นเตือน</Link> ให้ Admin เติม
      </span>
    </div>
  );
}
```

Create `frontend/src/pages/attendance/index.ts`:

```ts
export { AttendancePage } from './AttendancePage';
```

Create `frontend/src/pages/attendance/attendance.css`:

```css
/* Attendance (D2). Class prefix: attendance- */

.attendance-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.attendance-heading {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
}

.attendance-heading h1 {
  font-size: 26px;
  font-weight: 700;
}

.attendance-controls {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 8px;
  font-size: 14px;
}

.attendance-control {
  display: inline-flex;
  flex-direction: column;
  gap: 4px;
}

.attendance-shifts {
  display: inline-flex;
  gap: 4px;
}

.attendance-shift {
  min-height: 40px;
  padding: 0 14px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text);
  font-size: 14px;
  cursor: pointer;
}

.attendance-shift[aria-pressed='true'] {
  border-color: var(--color-text);
  background: var(--color-text);
  color: var(--color-surface);
  font-weight: 600;
}

.attendance-tiles {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 12px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.attendance-tile {
  display: flex;
  flex-direction: column;
  padding: 14px 16px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  font-size: 14px;
}

.attendance-tile strong {
  font-size: 30px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.attendance-tile--warn {
  border: 2px solid var(--color-status-rework);
  background: var(--color-error-bg);
}

.attendance-tile--warn strong {
  color: var(--color-status-rework);
}

.attendance-row--empty th,
.attendance-row--empty td {
  background: var(--color-error-bg);
}

.attendance-sub {
  display: block;
  color: var(--color-muted);
  font-size: 13px;
  font-weight: 400;
}

.attendance-who {
  display: flex;
  flex-direction: column;
}

.attendance-state {
  display: inline-block;
  padding: 2px 10px;
  border-radius: 999px;
  background: var(--color-status-idle);
  color: var(--color-on-primary);
  font-size: 12px;
  font-weight: 600;
  white-space: nowrap;
}

.attendance-state--Empty {
  background: var(--color-status-rework);
}

.attendance-state--Working {
  background: var(--color-status-passed);
}

.attendance-state--Covered {
  background: var(--color-status-pending);
}

.attendance-state--OnBreak {
  background: var(--color-status-break);
}

.attendance-badge {
  margin-left: 6px;
  padding: 0 6px;
  border-radius: var(--radius-sm);
  background: var(--color-warn-bg);
  color: var(--color-warn-text);
  font-size: 12px;
}

.attendance-notes {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: var(--color-muted);
  font-size: 13px;
}
```

In `frontend/src/index.css`, after the line `  --color-status-idle: #5f696b;`, add:

```css
  --color-status-break: #6b5b95;
```

- [ ] **Step 7: The route and the menu**

In `frontend/src/router.tsx`, after the `dashboard` route line, add:

```tsx
              // facility-0027, 0050, 0051, wireframe D2: ?date=yyyy-MM-dd&shift=Day|Night&building=<id>
              {
                path: 'admin/attendance',
                lazy: async () => ({ Component: (await import('./pages/attendance')).AttendancePage }),
              },
```

In `frontend/src/shared/components/AdminNav.tsx`, replace:

```tsx
/**
 * The Admin menu of wireframes D1–D10, listing only the pages that exist (plan 10 decision 2). Plan 11 adds
 * การเข้างาน, รายการที่ขึ้นเตือน, สรุปการตรวจ, ส่งออก Excel and ประวัติ Admin with their pages.
 */
const adminLinks = [
  { to: '/dashboard', label: 'สถานะจุด' },
  { to: '/admin/areas', label: 'จัดการ Area และป้าย' },
```

with:

```tsx
/**
 * The Admin menu of wireframes D1–D10, listing only the pages that exist (plan 10 decision 2). Plan 11 adds
 * รายการที่ขึ้นเตือน; plan 12 adds สรุปการตรวจ, ส่งออก Excel and ประวัติ Admin with their pages.
 */
const adminLinks = [
  { to: '/dashboard', label: 'สถานะจุด' },
  { to: '/admin/attendance', label: 'การเข้างาน' },
  { to: '/admin/areas', label: 'จัดการ Area และป้าย' },
```

- [ ] **Step 8: Run the tests**

Run: `cd frontend && npx vitest run`
Expected: PASS, 282 tests (269 + 2 time + 4 rules + 7 page). The two router tests changed only their lists.

- [ ] **Step 9: Measure, lint and commit**

Run: `cd frontend && npm run build && grep -o 'assets/index-[A-Za-z0-9_-]*\.js' dist/index.html`. Note the main file's gzip size, then run `rm -rf dist`.

Add to `## Task log (plan 11)` in `docs/handoff.md`:

```markdown
- <date> · Task 5 done: attendance page D2 (/admin/attendance, lazy, in the menu as การเข้างาน): the clock's shift by default, ?date&shift&building in the address; summary tiles; table in the server's order with the regular Cleaner (or ไม่มีแม่บ้านประจำ), ยังไม่ลงเวลา, covers, state, the four times with "แก้โดย Admin", GPS flags; typed date dd/mm/yyyy checked before asking; search on Area, Cleaner or cover; shared/api/attendanceBoardApi.ts follows the hub per board (AttendanceRecorded invalidates tag AttendanceBoard:<date>:<shift>) with livePoll; formatShiftDate and parseShiftDate. Main JS <size> kB gzip. Frontend tests <n> passed. Next: Task 6 (corrections).
```

Then:

```bash
cd frontend && npm run lint
cd .. && git add docs/handoff.md frontend/src
git commit -m "feat(frontend): attendance page D2 shows who is in each Area this shift, live"
```

---
### Task 6: Attendance corrections (D2, facility-0051)

The Admin adds a missing time or moves one, always with a reason. A link from a missing Shift-Out alert opens the form on that Cleaner and event.

**Files:**
- Create: `frontend/src/pages/attendance/attendanceApi.ts`, `frontend/src/pages/attendance/CorrectionForm.tsx`
- Modify: `frontend/src/pages/attendance/attendanceRules.ts`, `frontend/src/pages/attendance/attendanceRules.test.ts`
- Modify: `frontend/src/pages/attendance/AttendancePage.tsx`, `frontend/src/pages/attendance/attendance.css`
- Modify: `docs/handoff.md`
- Test: `frontend/src/pages/attendance/AttendanceCorrection.test.tsx`

**Interfaces:**
- Consumes: `AttendanceTable({ rows, onCorrect })`, `eventOf`, `readBoardArgs` and `toBoardParams` (Task 5), and the `AttendanceBoard` and `Alerts` tags (Task 3).
- Produces:
  - `useCorrectAttendanceMutation()` in `pages/attendance/attendanceApi.ts`. It sends `CorrectionRequest { userId, shiftDate, shift, eventType, time, reason }` and invalidates `AttendanceBoard` and `Alerts`.
  - In `pages/attendance/attendanceRules.ts`:
    - `MAX_REASON = 500`.
    - `correctionProblem({ time, reason }): string | null` and `firstEvent(row, asked?)`.
    - `readFill(params): { userId: number; event?: AttendanceEvent } | undefined`.
  - The address `/admin/attendance?date=…&shift=…&fill=<userId>&event=<AttendanceEvent>` opens the form. Task 7 links to it.

- [ ] **Step 1: Write the failing tests**

In `frontend/src/pages/attendance/attendanceRules.test.ts`, replace:

```ts
import { coverLine, flagText, readBoardArgs, searchRows, toBoardParams } from './attendanceRules';
```

with:

```ts
import {
  correctionProblem,
  coverLine,
  firstEvent,
  flagText,
  readBoardArgs,
  readFill,
  searchRows,
  toBoardParams,
} from './attendanceRules';
```

Replace:

```ts
import { boardRows, coveredRow, correctedRow, emptyRow, workingRow } from '../../test/liveFixtures';
```

with:

```ts
import { boardRows, breakRow, coveredRow, correctedRow, emptyRow, workingRow } from '../../test/liveFixtures';
```

Inside the `describe` block, after the last test, add:

```ts
  it('checks a correction before sending: a time "HH:mm" and a reason (facility-0051)', () => {
    expect(correctionProblem({ time: '07:05', reason: 'มือถือแบตหมด' })).toBeNull();
    expect(correctionProblem({ time: ' 23:59 ', reason: 'ลืม' })).toBeNull();
    expect(correctionProblem({ time: '7:05', reason: 'ลืม' })).toBe('เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05');
    expect(correctionProblem({ time: '24:00', reason: 'ลืม' })).toBe('เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05');
    expect(correctionProblem({ time: '07:05', reason: '   ' })).toBe('ต้องกรอกเหตุผล');
    expect(correctionProblem({ time: '07:05', reason: 'ก'.repeat(501) })).toBe('เหตุผลยาวได้ไม่เกิน 500 ตัวอักษร');
  });

  it('the form opens on the event asked for, else the first one not scanned', () => {
    expect(firstEvent(emptyRow)).toBe('ShiftIn');
    expect(firstEvent(workingRow)).toBe('BreakOut');
    expect(firstEvent(breakRow)).toBe('BreakIn');
    expect(firstEvent(workingRow, 'ShiftIn')).toBe('ShiftIn');
  });

  it('reads the Cleaner and event an alert link asks to fill', () => {
    expect(readFill(new URLSearchParams('fill=22&event=ShiftOut'))).toEqual({ userId: 22, event: 'ShiftOut' });
    expect(readFill(new URLSearchParams('fill=22&event=Lunch'))).toEqual({ userId: 22, event: undefined });
    expect(readFill(new URLSearchParams('fill=x'))).toBeUndefined();
    expect(readFill(new URLSearchParams(''))).toBeUndefined();
  });
```

Create `frontend/src/pages/attendance/AttendanceCorrection.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { attendanceBoard, boardEvent } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}, board: AttendanceBoard = attendanceBoard()) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/attendance': () => ({ status: 200, body: board }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    ...routes,
  });
}

const savedEvent = { status: 200, body: boardEvent(1100, 'ShiftIn', '2026-10-15T00:05:00Z', { source: 'AdminAdd' }) };
const boardCalls = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/admin/attendance' && call.method === 'GET');
const puts = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.method === 'PUT');

/** Last night's shift of AR01: แม่บ้านกะดึก checked in 18:55 and has no Shift-Out. */
const nightRow: BoardRow = {
  areaId: 1,
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  buildingCode: 'A',
  cleaner: { id: 22, employeeId: '650002', displayName: 'แม่บ้านกะดึก' },
  state: 'Working',
  events: [boardEvent(1200, 'ShiftIn', '2026-10-14T11:55:00Z')],
  covers: [],
};
const nightBoard = attendanceBoard([nightRow], { shiftDate: '2026-10-14', shift: 'Night', shiftEnded: true });

describe('attendance corrections (D2, facility-0051)', () => {
  it('adds a missing Shift-In with a reason, then asks for the board again', async () => {
    const server = serve({ 'PUT /api/admin/attendance': () => savedEvent });
    const { user } = await renderApp('/admin/attendance');

    await user.click(await screen.findByRole('button', { name: 'เพิ่มเวลา แม่บ้านลา' }));
    const form = screen.getByRole('region', { name: 'เพิ่ม/แก้เวลา · แม่บ้านลา · AR12' });
    expect(within(form).getByText('กะเช้า 15/10/2026')).toBeInTheDocument();
    expect(within(form).getByRole('combobox', { name: 'รายการ' })).toHaveValue('ShiftIn');
    await user.type(within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' }), '07:05');
    await user.type(within(form).getByRole('textbox', { name: 'เหตุผล (ต้องกรอก)' }), 'มือถือแบตหมด หัวหน้ายืนยัน');
    const before = boardCalls(server).length;

    await user.click(within(form).getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานของ แม่บ้านลา แล้ว')).toBeInTheDocument();
    expect(puts(server).map((call) => call.body)).toEqual([
      { userId: 26, shiftDate: '2026-10-15', shift: 'Day', eventType: 'ShiftIn', time: '07:05', reason: 'มือถือแบตหมด หัวหน้ายืนยัน' },
    ]);
    expect(screen.queryByRole('region', { name: /^เพิ่ม\/แก้เวลา/ })).not.toBeInTheDocument();
    await waitFor(() => expect(boardCalls(server).length).toBeGreaterThan(before));
  });

  it('แก้เวลา opens on the first event not scanned, shows each current time, and a chosen event brings its time', async () => {
    serve();
    const { user } = await renderApp('/admin/attendance');

    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = screen.getByRole('region', { name: 'เพิ่ม/แก้เวลา · แม่บ้านทดสอบ · AR01' });
    const event = within(form).getByRole('combobox', { name: 'รายการ' });
    const time = within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' });

    expect(event).toHaveValue('BreakOut');
    expect(time).toHaveValue('');
    expect(within(event).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'เข้างาน (ตอนนี้ 06:52)',
      'ออกพัก (ยังไม่มี)',
      'กลับจากพัก (ยังไม่มี)',
      'เลิกงาน (ยังไม่มี)',
    ]);

    await user.selectOptions(event, 'ShiftIn');
    expect(time).toHaveValue('06:52');

    await user.click(within(form).getByRole('button', { name: 'ยกเลิก' }));
    expect(screen.queryByRole('region', { name: /^เพิ่ม\/แก้เวลา/ })).not.toBeInTheDocument();
  });

  it("a wrong time or no reason is refused before sending; the server's refusal keeps the draft", async () => {
    const server = serve({
      'PUT /api/admin/attendance': () => ({ status: 400, body: { message: 'เวลาที่ใส่ยังมาไม่ถึง' } }),
    });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'เพิ่มเวลา แม่บ้านลา' }));
    const form = screen.getByRole('region', { name: 'เพิ่ม/แก้เวลา · แม่บ้านลา · AR12' });
    const time = within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' });
    const reason = within(form).getByRole('textbox', { name: 'เหตุผล (ต้องกรอก)' });
    const save = within(form).getByRole('button', { name: 'บันทึก' });

    await user.type(time, '7.05');
    await user.click(save);
    expect(within(form).getByRole('alert')).toHaveTextContent('เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05');

    await user.clear(time);
    await user.type(time, '18:30');
    await user.click(save);
    expect(within(form).getByRole('alert')).toHaveTextContent('ต้องกรอกเหตุผล');
    expect(puts(server)).toHaveLength(0);

    await user.type(reason, 'ลืมสแกน');
    await user.click(save);

    expect(await within(form).findByText('เวลาที่ใส่ยังมาไม่ถึง')).toBeInTheDocument();
    expect(time).toHaveValue('18:30');
    expect(reason).toHaveValue('ลืมสแกน');
  });

  it('one save at a time: the button waits for the answer', async () => {
    const server = serve({ 'PUT /api/admin/attendance': () => new Promise(() => {}) });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'เพิ่มเวลา แม่บ้านลา' }));
    const form = screen.getByRole('region', { name: 'เพิ่ม/แก้เวลา · แม่บ้านลา · AR12' });
    await user.type(within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' }), '07:05');
    await user.type(within(form).getByRole('textbox', { name: 'เหตุผล (ต้องกรอก)' }), 'ลืมสแกน');

    await user.click(within(form).getByRole('button', { name: 'บันทึก' }));

    const waiting = await within(form).findByRole('button', { name: 'กำลังบันทึก...' });
    expect(waiting).toBeDisabled();
    await user.click(waiting);
    expect(puts(server)).toHaveLength(1);
  });

  it('no button for an Area with no regular Cleaner', async () => {
    serve();
    await renderApp('/admin/attendance');
    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');

    const row = screen.getAllByRole('row').find((r) => within(r).queryByRole('rowheader')?.textContent?.startsWith('AR09 '));
    expect(row).toBeDefined();
    expect(within(row as HTMLElement).queryByRole('button')).not.toBeInTheDocument();
  });

  it('a link from a missing Shift-Out alert opens the form on that Cleaner and event; closing it leaves the address clean', async () => {
    serve({}, nightBoard);
    const { user, router } = await renderApp('/admin/attendance?date=2026-10-14&shift=Night&fill=22&event=ShiftOut');

    const form = await screen.findByRole('region', { name: 'เพิ่ม/แก้เวลา · แม่บ้านกะดึก · AR01' });
    expect(within(form).getByText('กะดึก 14/10/2026')).toBeInTheDocument();
    expect(within(form).getByRole('combobox', { name: 'รายการ' })).toHaveValue('ShiftOut');

    await user.click(within(form).getByRole('button', { name: 'ยกเลิก' }));

    expect(router.state.location.search).toBe('?date=2026-10-14&shift=Night');
    expect(screen.queryByRole('region', { name: /^เพิ่ม\/แก้เวลา/ })).not.toBeInTheDocument();
  });

  it('a link for a Cleaner not on the board says so', async () => {
    serve({}, nightBoard);
    await renderApp('/admin/attendance?date=2026-10-14&shift=Night&fill=99&event=ShiftOut');

    expect(await screen.findByText('ไม่พบแม่บ้านคนนี้ในกะนี้ อาจย้าย Area หรือปิดใช้งานแล้ว')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/attendance`
Expected: FAIL. `correctionProblem`, `firstEvent` and `readFill` do not exist, and the table has no "เพิ่มเวลา" button.

- [ ] **Step 3: The rules and the mutation**

In `frontend/src/pages/attendance/attendanceRules.ts`, replace:

```ts
import { eventLabel } from '../../shared/utils/labels';
```

with:

```ts
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
```

At the end of the file, add:

```ts
/** facility-0051: AdminAttendanceEndpoints.MaxReasonLength. */
export const MAX_REASON = 500;

/** Checked before sending; the server checks the window, the order and the future, and its message is shown as is. */
export function correctionProblem({ time, reason }: { time: string; reason: string }): string | null {
  if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(time.trim())) {
    return 'เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05';
  }
  const text = reason.trim();
  if (text === '') {
    return 'ต้องกรอกเหตุผล';
  }
  return text.length > MAX_REASON ? `เหตุผลยาวได้ไม่เกิน ${MAX_REASON} ตัวอักษร` : null;
}

/** The event the form opens on: the one asked for, else the first one the Cleaner has not scanned, else Shift-In. */
export function firstEvent(row: BoardRow, asked?: AttendanceEvent): AttendanceEvent {
  return asked ?? attendanceEvents.find((type) => !eventOf(row, type)) ?? 'ShiftIn';
}

/** From an alert's "เติมเวลา…" link (plan 11 decision 9): ?fill=<Cleaner's account id>&event=<AttendanceEvent>. */
export function readFill(params: URLSearchParams): { userId: number; event?: AttendanceEvent } | undefined {
  const userId = Number(params.get('fill'));
  if (!Number.isInteger(userId) || userId <= 0) {
    return undefined;
  }
  const event = params.get('event');
  return { userId, event: attendanceEvents.find((type) => type === event) };
}
```

Create `frontend/src/pages/attendance/attendanceApi.ts`:

```ts
import type { BoardEvent } from '../../shared/api/attendanceBoardApi';
import { baseApi } from '../../shared/api/baseApi';
import type { AttendanceEvent, Shift } from '../../shared/api/types';

/** CorrectAttendanceRequest (facility-0051): time is Thai wall-clock "HH:mm" inside the shift's attendance window. */
export interface CorrectionRequest {
  userId: number;
  shiftDate: string;
  shift: Shift;
  eventType: AttendanceEvent;
  time: string;
  reason: string;
}

const attendanceApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /** Adds a missing time or moves one. The board changes, and a missing Shift-Out alert closes, so both are asked again. */
    correctAttendance: build.mutation<BoardEvent, CorrectionRequest>({
      query: (body) => ({ url: '/admin/attendance', method: 'PUT', body }),
      invalidatesTags: ['AttendanceBoard', 'Alerts'],
    }),
  }),
});

export const { useCorrectAttendanceMutation } = attendanceApi;
```

- [ ] **Step 4: The form**

Create `frontend/src/pages/attendance/CorrectionForm.tsx`:

```tsx
import { useState } from 'react';
import type { AssignedCleaner } from '../../shared/api/adminSetupApi';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import type { AttendanceEvent } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { attendanceEvents, eventLabel, shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate, formatTime } from '../../shared/utils/thaiTime';
import { useCorrectAttendanceMutation } from './attendanceApi';
import { correctionProblem, eventOf, firstEvent, MAX_REASON } from './attendanceRules';

interface CorrectionFormProps {
  board: AttendanceBoard;
  row: BoardRow;
  /** The row's regular Cleaner: only they can be corrected on this row (the server checks their shift). */
  cleaner: AssignedCleaner;
  initialEvent?: AttendanceEvent;
  onDone: (text: string) => void;
  onCancel: () => void;
}

/**
 * facility-0051, wireframe D2: the Admin adds a missing time or moves one, always with a reason. The row then carries
 * "แก้โดย Admin" and the old time stays in the history. The draft lives here only (facility-0024); one send at a time.
 */
export function CorrectionForm({ board, row, cleaner, initialEvent, onDone, onCancel }: CorrectionFormProps) {
  const start = firstEvent(row, initialEvent);
  const [eventType, setEventType] = useState<AttendanceEvent>(start);
  const [time, setTime] = useState(() => timeOf(row, start));
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [correct, { isLoading }] = useCorrectAttendanceMutation();
  const headingId = `attendance-correction-${row.areaId}`;

  const choose = (next: AttendanceEvent) => {
    setEventType(next);
    setTime(timeOf(row, next));
  };

  const save = async () => {
    const check = correctionProblem({ time, reason });
    if (check) {
      setProblem(check);
      return;
    }
    setProblem(null);
    try {
      await correct({
        userId: cleaner.id,
        shiftDate: board.shiftDate,
        shift: board.shift,
        eventType,
        time: time.trim(),
        reason: reason.trim(),
      }).unwrap();
      onDone(`บันทึกเวลา${eventLabel[eventType]}ของ ${cleaner.displayName} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <section className="app-panel attendance-form" aria-labelledby={headingId}>
      <h2 id={headingId}>{`เพิ่ม/แก้เวลา · ${cleaner.displayName} · ${row.areaCode}`}</h2>
      <span className="app-muted">{`${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)}`}</span>
      <div className="attendance-form__row">
        <label className="app-field">
          รายการ
          <select className="app-input" value={eventType} onChange={(event) => choose(event.target.value as AttendanceEvent)}>
            {attendanceEvents.map((type) => {
              const existing = eventOf(row, type);
              return (
                <option key={type} value={type}>
                  {`${eventLabel[type]} (${existing ? `ตอนนี้ ${formatTime(existing.occurredAt)}` : 'ยังไม่มี'})`}
                </option>
              );
            })}
          </select>
        </label>
        <label className="app-field">
          เวลา (ชม.:นาที)
          <input
            className="app-input"
            value={time}
            placeholder="07:05"
            inputMode="numeric"
            maxLength={5}
            onChange={(event) => setTime(event.target.value)}
          />
        </label>
      </div>
      <label className="app-field">
        เหตุผล (ต้องกรอก)
        <input
          className="app-input"
          value={reason}
          maxLength={MAX_REASON}
          onChange={(event) => setReason(event.target.value)}
        />
      </label>
      <span className="app-field__help">
        ระบบเก็บค่าเดิม ผู้แก้ และเวลาที่แก้ไว้ในประวัติ · รายการนี้จะมีป้าย "แก้โดย Admin" · แม่บ้านและหัวหน้าแก้เองไม่ได้
      </span>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="app-confirm__buttons">
        <button type="button" className="app-button app-button--primary" disabled={isLoading} onClick={() => void save()}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึก'}
        </button>
        <button type="button" className="app-button" onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </section>
  );
}

function timeOf(row: BoardRow, type: AttendanceEvent): string {
  const existing = eventOf(row, type);
  return existing ? formatTime(existing.occurredAt) : '';
}
```

Append to `frontend/src/pages/attendance/attendance.css`:

```css

.attendance-form {
  max-width: 640px;
}

.attendance-form__row {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}
```

- [ ] **Step 5: The page opens the form**

Replace the whole of `frontend/src/pages/attendance/AttendancePage.tsx` with:

```tsx
import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import {
  attendanceHubKey,
  useGetAttendanceBoardQuery,
  type AttendanceBoard,
  type BoardArgs,
  type BoardSummary,
} from '../../shared/api/attendanceBoardApi';
import { livePoll } from '../../shared/api/baseApi';
import type { AttendanceEvent } from '../../shared/api/types';
import { LiveBadge } from '../../shared/components/LiveBadge';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { AttendanceTable } from './AttendanceTable';
import { BoardControls } from './BoardControls';
import { CorrectionForm } from './CorrectionForm';
import { readBoardArgs, readFill, searchRows, toBoardParams } from './attendanceRules';
import './attendance.css';

/** The row whose form is open, and the event it opens on. */
type Editing = { areaId: number; event?: AttendanceEvent };

type Notice = { kind: 'ok' | 'error'; text: string } | null;

/**
 * facility-0027, 0050, wireframe D2: whether each Area that works the shift has someone in it, never late or early.
 * The shift on the clock by default; the address keeps another date, shift or building. Live on AttendanceRecorded,
 * every open, every 30 s and on focus (facility-0061). facility-0051: the Admin adds or moves a time with a reason.
 */
export function AttendancePage() {
  const [params, setParams] = useSearchParams();
  const args = readBoardArgs(params);
  const fill = readFill(params);
  const { data: board, error, isLoading } = useGetAttendanceBoardQuery(args, livePoll);
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Editing | null>(null);
  const [notice, setNotice] = useState<Notice>(null);
  const go = (next: BoardArgs) => setParams(toBoardParams(next));

  // From an alert's "เติมเวลา…" link (plan 11 decision 9): the form opens on that Cleaner once the board is in
  const fillRow = fill && board ? board.rows.find((row) => row.cleaner?.id === fill.userId) : undefined;
  const open: Editing | null = editing ?? (fillRow ? { areaId: fillRow.areaId, event: fill?.event } : null);
  const openRow = open && board ? board.rows.find((row) => row.areaId === open.areaId) : undefined;

  const closeForm = () => {
    setEditing(null);
    if (fill) {
      go(args); // the link's fill and event go, so the form does not open again
    }
  };

  return (
    <main className="attendance-page">
      <header className="attendance-heading">
        <div>
          {board && <span className="app-muted">{slotLine(board)}</span>}
          <h1>การเข้างาน</h1>
        </div>
        <LiveBadge hubKey={attendanceHubKey(args)} />
      </header>
      <BoardControls
        board={board}
        args={args}
        search={search}
        onSearch={setSearch}
        onSlot={(shiftDate, shift) => go({ ...args, shiftDate, shift })}
        onNow={() => go({ buildingId: args.buildingId })}
        onBuilding={(buildingId) => go({ ...args, buildingId })}
      />
      {notice && (
        <p
          className={notice.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
          role={notice.kind === 'ok' ? 'status' : 'alert'}
        >
          {notice.text}
        </p>
      )}
      {isLoading && <p className="app-muted">กำลังโหลด...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {board && fill && !fillRow && !editing && (
        <p className="app-note app-note--info">ไม่พบแม่บ้านคนนี้ในกะนี้ อาจย้าย Area หรือปิดใช้งานแล้ว</p>
      )}
      {board && (
        <>
          <Tiles summary={board.summary} />
          {openRow?.cleaner && (
            <CorrectionForm
              key={`${openRow.areaId}:${open?.event ?? ''}`}
              board={board}
              row={openRow}
              cleaner={openRow.cleaner}
              initialEvent={open?.event}
              onDone={(text) => {
                setNotice({ kind: 'ok', text });
                closeForm();
              }}
              onCancel={closeForm}
            />
          )}
          <AttendanceTable
            rows={searchRows(board.rows, search)}
            onCorrect={(row) => {
              setNotice(null);
              setEditing({ areaId: row.areaId });
            }}
          />
        </>
      )}
      <BoardNotes />
    </main>
  );
}

function slotLine(board: AttendanceBoard): string {
  return `${formatShiftDate(board.shiftDate)} · ${shiftLabel[board.shift]}${board.shiftEnded ? ' · จบกะแล้ว' : ''}`;
}

/** Wireframe D2's four counts (BoardSummaryDto). */
function Tiles({ summary }: { summary: BoardSummary }) {
  return (
    <ul className="attendance-tiles" aria-label="สรุปกะนี้">
      <li className="attendance-tile">
        <span>Area ที่ต้องมีคนกะนี้</span>
        <strong>{summary.required}</strong>
        <span className="app-muted">ไม่รวม Area ที่ปิดกะนี้</span>
      </li>
      <li className="attendance-tile">
        <span>มีคนแล้ว</span>
        <strong>{summary.present}</strong>
        <span className="app-muted">รวมพักเบรก</span>
      </li>
      <li className="attendance-tile attendance-tile--warn">
        <span>ยังไม่มีคน</span>
        <strong>{summary.empty}</strong>
        <span className="app-muted">ต้องหาคนทำแทน</span>
      </li>
      <li className="attendance-tile">
        <span>มีคนทำแทน</span>
        <strong>{summary.covered}</strong>
        <span className="app-muted">Admin มอบหมายเฉพาะกะนี้</span>
      </li>
    </ul>
  );
}

/** Wireframe D2's notes under the table. */
function BoardNotes() {
  return (
    <div className="attendance-notes">
      <span>หน้านี้ดูว่าแต่ละ Area มีคนอยู่แล้วหรือยัง ไม่ใช่ระบบลงเวลาทำงานของบริษัท จึงไม่คิดสายหรือกลับก่อน</span>
      <span>
        เรียง Area ที่ยังไม่มีคนขึ้นก่อน · Area ที่ทำเฉพาะกะเช้าไม่นับเป็น "ต้องมีคน" ในกะดึก · มอบหมายคนทำแทนได้ที่{' '}
        <Link to="/admin/alerts">รายการที่ขึ้นเตือน</Link>
      </span>
      <span>
        ลืมลงเวลาเลิกงาน: ช่องเลิกงานว่างไว้ หมดกะแล้วขึ้นใน <Link to="/admin/alerts">รายการที่ขึ้นเตือน</Link> ให้ Admin เติม
      </span>
    </div>
  );
}
```

- [ ] **Step 6: Run the tests**

Run: `cd frontend && npx vitest run`
Expected: PASS, 292 tests (282 + 3 rules + 7 correction tests). The Task 5 page tests still pass, because they look only at the first seven cells of a row.

- [ ] **Step 7: Lint and commit**

Add to `## Task log (plan 11)` in `docs/handoff.md`:

```markdown
- <date> · Task 6 done: corrections on D2 (facility-0051): "เพิ่มเวลา" / "แก้เวลา" on rows with a regular Cleaner; the form (event with each current time, time "HH:mm", reason required up to 500) checks before sending, shows the server's refusal and keeps the draft, one send at a time; the save invalidates AttendanceBoard and Alerts; ?fill=<userId>&event=<event> opens the form for an alert's link and closing it clears them. Frontend tests <n> passed. Next: Task 7 (alert list D3).
```

Then:

```bash
cd frontend && npm run lint
cd .. && git add docs/handoff.md frontend/src/pages/attendance
git commit -m "feat(frontend): the Admin adds or corrects an attendance time with a reason on D2"
```

---
### Task 7: Alert list (D3) and the menu's count

The page lists the alerts of yesterday and today, split into "ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด" and by type. The Admin closes an alert with an optional note. A missing Shift-Out links to the attendance page's form. The menu counts the open alerts on every Admin page.

**Files:**
- Modify: `frontend/src/shared/utils/thaiTime.ts`, `frontend/src/shared/utils/thaiTime.test.ts`
- Create: `frontend/src/shared/api/alertListApi.ts`
- Modify: `frontend/src/shared/components/AdminNav.tsx`
- Create: `frontend/src/pages/alerts/AlertsPage.tsx`, `frontend/src/pages/alerts/AlertItem.tsx`, `frontend/src/pages/alerts/alertsApi.ts`, `frontend/src/pages/alerts/alertRules.ts`, `frontend/src/pages/alerts/alerts.css`, `frontend/src/pages/alerts/index.ts`
- Modify: `frontend/src/router.tsx`, `frontend/src/test/liveFixtures.ts`
- Modify: `docs/handoff.md`
- Test: `frontend/src/pages/alerts/alertRules.test.ts`, `frontend/src/pages/alerts/AlertsPage.test.tsx`, `frontend/src/router.test.tsx`

**Interfaces:**
- Consumes: `livePoll` and the `Alerts` tag (Task 3), `formatShiftDate` (Task 5), and the address `/admin/attendance?date&shift&fill&event` (Task 6).
- Produces:
  - `formatDayTime(moment)` in `shared/utils/thaiTime.ts`, which gives `'15/10 09:10'`.
  - In `shared/api/alertListApi.ts`:
    - Types `AlertKind`, `AlertPerson`, `CoverSuggestion` and `Alert`.
    - `useGetAlertsQuery()` and `openAlertCount(alerts)`.
  - `useReviewAlertMutation()` in `pages/alerts/alertsApi.ts`. Task 8 adds the cover endpoints to the same file.
  - In `pages/alerts/alertRules.ts`:
    - `AlertGroup`, `alertGroupLabel`, `ReviewTab` and `filterAlerts(alerts, tab, group)`.
    - `alertTitle(alert)`, `alertWho(alert)` and `alertDetail(alert)`.
    - `slotOfShiftEnd(at)` and `fillLink(alert)`.
  - `AlertItem({ alert, onNotice })`. Task 8 adds the cover button to it.
  - `Notice = { kind: 'ok' | 'error'; text: string }`, exported from `pages/alerts/AlertItem.tsx`.
  - The route `/admin/alerts` (lazy), and the menu link "รายการที่ขึ้นเตือน (n)".
  - Fixtures in `src/test/liveFixtures.ts`: `blockedAlert`, `missingAlert`, `attendanceGpsAlert`, `inspectionGpsAlert`, `reviewedAlert` and `alerts`.

- [ ] **Step 1: Write the failing tests**

In `frontend/src/shared/utils/thaiTime.test.ts`, replace:

```ts
import { formatDate, formatMinutes, formatRound, formatShiftDate, formatTime, parseShiftDate } from './thaiTime';
```

with:

```ts
import {
  formatDate,
  formatDayTime,
  formatMinutes,
  formatRound,
  formatShiftDate,
  formatTime,
  parseShiftDate,
} from './thaiTime';
```

Inside the `describe('thaiTime', …)` block, add:

```ts
  it('formatDayTime gives the Thai day/month and time, for lists that cover more than one day', () => {
    expect(formatDayTime('2026-10-15T02:10:00Z')).toBe('15/10 09:10');
    expect(formatDayTime('2026-10-14T23:52:00Z')).toBe('15/10 06:52');
  });
```

At the top of `frontend/src/test/liveFixtures.ts`, after the `attendanceBoardApi` import, add:

```ts
import type { Alert } from '../shared/api/alertListApi';
```

Append to `frontend/src/test/liveFixtures.ts`:

```ts
/** 09:10: แม่บ้านที่อื่น (AR03) scanned a point of AR12 and was refused; a cover can still be given (facility-0041). */
export const blockedAlert: Alert = {
  kind: 'BlockedScan',
  recordId: 301,
  at: '2026-10-15T02:10:00Z',
  person: { id: 25, employeeId: '650005', displayName: 'แม่บ้านที่อื่น', role: 'Cleaner', areaCode: 'AR03' },
  areaCode: 'AR12',
  areaName: 'Area สิบสอง',
  signCode: 'AR12-02',
  pointName: 'ห้องน้ำหญิง',
  missingEvents: [],
  reviewed: false,
  cover: { userId: 25, areaId: 12, shiftDate: '2026-10-15', shift: 'Day' },
  blockReason: 'OtherArea',
};

/** 07:00 today, the end of last night's shift (14/10): แม่บ้านกะดึก checked in and has no Shift-Out (facility-0051). */
export const missingAlert: Alert = {
  kind: 'MissingAttendance',
  recordId: 401,
  at: '2026-10-15T00:00:00Z',
  person: { id: 22, employeeId: '650002', displayName: 'แม่บ้านกะดึก', role: 'Cleaner', areaCode: 'AR01' },
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  missingEvents: ['ShiftOut'],
  reviewed: false,
};

/** 06:52: checked in 85 m from AR01's check-in sign. Same record id as missingAlert: an alert is known by kind and id. */
export const attendanceGpsAlert: Alert = {
  kind: 'AttendanceGps',
  recordId: 401,
  at: '2026-10-14T23:52:00Z',
  person: { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ', role: 'Cleaner', areaCode: 'AR01' },
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  signCode: 'AR01-IN',
  distanceM: 85,
  radiusM: 50,
  accuracyM: 40,
  missingEvents: [],
  reviewed: false,
};

/** 10:15: the Supervisor inspected 62 m from the sign (facility-0037). */
export const inspectionGpsAlert: Alert = {
  kind: 'InspectionGps',
  recordId: 601,
  at: '2026-10-15T03:15:00Z',
  person: { id: 31, employeeId: '660001', displayName: 'หัวหน้าทดสอบ', role: 'Supervisor' },
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  signCode: 'AR01-03',
  pointName: 'จุดทิ้งขยะ',
  distanceM: 62,
  radiusM: 50,
  accuracyM: 25,
  missingEvents: [],
  reviewed: false,
};

/** Yesterday 15:00, a submission 70 m away; an Admin closed it at 16:30. */
export const reviewedAlert: Alert = {
  kind: 'SubmissionGps',
  recordId: 701,
  at: '2026-10-14T08:00:00Z',
  person: { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ', role: 'Cleaner', areaCode: 'AR01' },
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  signCode: 'AR01-01',
  pointName: 'ห้องน้ำชาย',
  distanceM: 70,
  radiusM: 50,
  accuracyM: 30,
  missingEvents: [],
  reviewed: true,
  reviewedAt: '2026-10-14T09:30:00Z',
};

/** Newest first, as the server sends them. */
export const alerts: Alert[] = [inspectionGpsAlert, blockedAlert, missingAlert, attendanceGpsAlert, reviewedAlert];
```

Create `frontend/src/pages/alerts/alertRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import {
  alerts,
  attendanceGpsAlert,
  blockedAlert,
  inspectionGpsAlert,
  missingAlert,
  reviewedAlert,
} from '../../test/liveFixtures';
import { alertDetail, alertTitle, alertWho, fillLink, filterAlerts, slotOfShiftEnd } from './alertRules';

describe('alert rules (wireframe D3)', () => {
  it('splits by talked-about or not, and puts the three GPS flags under one type', () => {
    expect(filterAlerts(alerts, 'open', 'all')).toHaveLength(4);
    expect(filterAlerts(alerts, 'reviewed', 'all')).toEqual([reviewedAlert]);
    expect(filterAlerts(alerts, 'all', 'far')).toEqual([inspectionGpsAlert, attendanceGpsAlert, reviewedAlert]);
    expect(filterAlerts(alerts, 'open', 'blocked')).toEqual([blockedAlert]);
    expect(filterAlerts(alerts, 'open', 'missing')).toEqual([missingAlert]);
  });

  it("names a Blocked Scan by why it was refused, never the old 'สแกนนอก Area' for a Supervisor", () => {
    expect(alertTitle(blockedAlert)).toBe('สแกนนอก Area (ถูกบล็อก)');
    expect(alertTitle({ ...blockedAlert, blockReason: 'OtherBuilding' })).toBe('สแกนนอกตึก (ถูกบล็อก)');
    expect(alertTitle({ ...blockedAlert, blockReason: 'OutsideShift' })).toBe('สแกนนอกกะ (ถูกบล็อก)');
    expect(alertTitle({ ...missingAlert, missingEvents: ['BreakIn', 'ShiftOut'] })).toBe('ไม่ได้ลงเวลากลับจากพัก และเลิกงาน');
    expect(alertWho(inspectionGpsAlert)).toBe('หัวหน้าทดสอบ (หัวหน้า)');
  });

  it('says where it happened, as far as the server knows', () => {
    expect(alertDetail(blockedAlert)).toBe(
      'ป้าย AR12-02 ห้องน้ำหญิง ของ AR12 Area สิบสอง · Area ของแม่บ้านที่อื่นคือ AR03 · ไม่ได้บันทึกงาน',
    );
    expect(alertDetail(attendanceGpsAlert)).toBe(
      'ป้ายลงเวลา AR01-IN · AR01 Area ทดลอง · ห่าง 85 ม. (รัศมี 50 ม.) · ความแม่นยำ ±40 ม.',
    );
    expect(alertDetail({ ...attendanceGpsAlert, accuracyM: undefined, radiusM: undefined })).toBe(
      'ป้ายลงเวลา AR01-IN · AR01 Area ทดลอง · ห่าง 85 ม.',
    );
    expect(alertDetail(missingAlert)).toBe('กะดึก 14/10/2026 · AR01 Area ทดลอง');
  });

  it("works out a missing-attendance alert's shift from its time, the shift's end (facility-0040)", () => {
    expect(slotOfShiftEnd('2026-10-15T12:00:00Z')).toEqual({ shiftDate: '2026-10-15', shift: 'Day' }); // 19:00
    expect(slotOfShiftEnd('2026-10-15T00:00:00Z')).toEqual({ shiftDate: '2026-10-14', shift: 'Night' }); // 07:00
    expect(slotOfShiftEnd('2026-11-01T00:00:00Z')).toEqual({ shiftDate: '2026-10-31', shift: 'Night' });
    expect(fillLink(missingAlert)).toBe('/admin/attendance?date=2026-10-14&shift=Night&fill=22&event=ShiftOut');
  });
});
```

Create `frontend/src/pages/alerts/AlertsPage.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { Alert } from '../../shared/api/alertListApi';
import { adminSession } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { alerts, attendanceGpsAlert } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}, list: () => Alert[] = () => alerts) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/alerts': () => ({ status: 200, body: list() }),
    ...routes,
  });
}

const alertGets = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/admin/alerts');
const items = () => screen.queryAllByRole('article').map((item) => item.getAttribute('aria-label'));

describe('AlertsPage (D3, facility-0035, 0041, 0051)', () => {
  it('lists the alerts nobody has talked about yet, newest first, with who, what and where', async () => {
    serve();
    await renderApp('/admin/alerts');

    await screen.findAllByRole('article');
    expect(items()).toEqual([
      'ตรวจงานไกลจากป้าย · หัวหน้าทดสอบ (หัวหน้า)',
      'สแกนนอก Area (ถูกบล็อก) · แม่บ้านที่อื่น',
      'ไม่ได้ลงเวลาเลิกงาน · แม่บ้านกะดึก',
      'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ',
    ]);
    const inspection = screen.getByRole('article', { name: 'ตรวจงานไกลจากป้าย · หัวหน้าทดสอบ (หัวหน้า)' });
    expect(within(inspection).getByText('15/10 10:15')).toBeInTheDocument();
    expect(
      within(inspection).getByText('ป้าย AR01-03 จุดทิ้งขยะ · AR01 Area ทดลอง · ห่าง 62 ม. (รัศมี 50 ม.) · ความแม่นยำ ±25 ม.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ยังไม่ได้คุย (4)' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('คุยแล้ว and ทั้งหมด; the type picker puts the three GPS flags together', async () => {
    serve();
    const { user } = await renderApp('/admin/alerts');
    await screen.findAllByRole('article');

    await user.click(screen.getByRole('button', { name: 'คุยแล้ว (1)' }));
    expect(items()).toEqual(['ส่งงานไกลจากป้าย · แม่บ้านทดสอบ']);
    expect(screen.getByText('คุยแล้ว 14/10 16:30')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ทั้งหมด' }));
    expect(items()).toHaveLength(5);

    await user.selectOptions(screen.getByRole('combobox', { name: 'ประเภท' }), 'ไกลจากป้าย');
    expect(items()).toEqual([
      'ตรวจงานไกลจากป้าย · หัวหน้าทดสอบ (หัวหน้า)',
      'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ',
      'ส่งงานไกลจากป้าย · แม่บ้านทดสอบ',
    ]);

    await user.selectOptions(screen.getByRole('combobox', { name: 'ประเภท' }), 'ไม่ได้ลงเวลา');
    await user.click(screen.getByRole('button', { name: 'คุยแล้ว (1)' }));
    expect(screen.getByText('ไม่มีรายการ')).toBeInTheDocument();
  });

  it('closing an alert sends the note, moves it to คุยแล้ว, and the menu count goes down', async () => {
    let closed = false;
    const server = serve(
      {
        'POST /api/admin/alerts/review': () => {
          closed = true;
          return { status: 204 };
        },
      },
      () => alerts.map((a) => (closed && a === attendanceGpsAlert ? { ...a, reviewed: true, reviewedAt: '2026-10-15T03:30:00Z' } : a)),
    );
    const { user } = await renderApp('/admin/alerts');
    const menu = screen.getByRole('navigation', { name: 'เมนู Admin' });
    expect(await within(menu).findByRole('link', { name: 'รายการที่ขึ้นเตือน (4)' })).toHaveAttribute('aria-current', 'page');
    const item = await screen.findByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' });

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.type(within(item).getByRole('textbox', { name: 'หมายเหตุ (ไม่บังคับ)' }), 'GPS ในอาคารเพี้ยน');
    await user.click(within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' }));

    expect(await screen.findByText('ปิดเตือนลงเวลาไกลจากป้ายของ แม่บ้านทดสอบ แล้ว')).toBeInTheDocument();
    expect(server.calls.find((call) => call.path === '/api/admin/alerts/review')?.body).toEqual({
      kind: 'AttendanceGps',
      recordId: 401,
      note: 'GPS ในอาคารเพี้ยน',
    });
    await waitFor(() => expect(screen.queryByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' })).not.toBeInTheDocument());
    expect(within(menu).getByRole('link', { name: 'รายการที่ขึ้นเตือน (3)' })).toBeInTheDocument();
  });

  it('a note left empty is not sent; ยกเลิก closes the note without sending', async () => {
    const server = serve({ 'POST /api/admin/alerts/review': () => ({ status: 204 }) });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' });

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(within(item).getByRole('button', { name: 'ยกเลิก' }));
    expect(within(item).queryByRole('textbox')).not.toBeInTheDocument();

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' }));

    await waitFor(() =>
      expect(server.calls.find((call) => call.path === '/api/admin/alerts/review')?.body).toEqual({ kind: 'AttendanceGps', recordId: 401 }),
    );
  });

  it('a missing Shift-Out has no close button; it links to the attendance page with that shift and the form open', async () => {
    serve();
    await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'ไม่ได้ลงเวลาเลิกงาน · แม่บ้านกะดึก' });

    expect(within(item).getByText('กะดึก 14/10/2026 · AR01 Area ทดลอง')).toBeInTheDocument();
    expect(within(item).queryByRole('button', { name: 'คุยแล้ว ปิดเตือน' })).not.toBeInTheDocument();
    expect(within(item).getByRole('link', { name: 'เติมเวลาเลิกงาน' })).toHaveAttribute(
      'href',
      '/admin/attendance?date=2026-10-14&shift=Night&fill=22&event=ShiftOut',
    );
  });

  it('a refusal (another Admin closed it first) is shown at the top, and the list is asked again', async () => {
    const server = serve({
      'POST /api/admin/alerts/review': () => ({ status: 409, body: { message: 'ปิดเตือนนี้ไปแล้ว' } }),
    });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' });
    const before = alertGets(server).length;

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ปิดเตือนนี้ไปแล้ว');
    await waitFor(() => expect(alertGets(server).length).toBeGreaterThan(before));
  });

  it('one close at a time: the button waits for the answer', async () => {
    const server = serve({ 'POST /api/admin/alerts/review': () => new Promise(() => {}) });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' });
    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    const confirm = within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' });

    await user.click(confirm);
    await waitFor(() => expect(confirm).toBeDisabled());
    await user.click(confirm);

    expect(server.calls.filter((call) => call.path === '/api/admin/alerts/review')).toHaveLength(1);
  });
});
```

In `frontend/src/router.test.tsx`, in the test `'an Admin page shows the Admin menu with the open page marked'`, replace:

```tsx
      'สถานะจุด',
      'การเข้างาน',
      'จัดการ Area และป้าย',
```

with:

```tsx
      'สถานะจุด',
      'การเข้างาน',
      'รายการที่ขึ้นเตือน',
      'จัดการ Area และป้าย',
```

In the test `'every Admin page is a lazy route, …'`, replace:

```tsx
      'admin/attendance',
      'admin/areas/:areaId?',
```

with:

```tsx
      'admin/attendance',
      'admin/alerts',
      'admin/areas/:areaId?',
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/shared/utils src/pages/alerts src/router.test.tsx`
Expected: FAIL. `formatDayTime`, `alertListApi` and `./alertRules` do not exist, `/admin/alerts` is not found, and the menu has no "รายการที่ขึ้นเตือน".

- [ ] **Step 3: Day and time**

In `frontend/src/shared/utils/thaiTime.ts`, after the `formatShiftDate` function, add:

```ts
/** A moment as Thai "15/10 09:10", for lists that cover more than one day (the alert list, wireframe D3). */
export function formatDayTime(moment: string | Date): string {
  return `${formatDate(moment).slice(0, 5)} ${formatTime(moment)}`;
}
```

- [ ] **Step 4: The shared alert query and the menu's count**

Create `frontend/src/shared/api/alertListApi.ts`:

```ts
import { baseApi } from './baseApi';
import type { AttendanceEvent, BlockedScan, Shift } from './types';

/**
 * The alert list (wireframe D3, facility-0035, 0037, 0041, 0051), read by the alert page and the Admin menu's count
 * (plan 11 decision 12). Fields the server sends as null arrive missing, so they are optional here.
 */

/** AlertKind on the server. */
export type AlertKind = 'BlockedScan' | 'AttendanceGps' | 'SubmissionGps' | 'MissingAttendance' | 'InspectionGps';

/** AlertPersonDto. employeeId is the login name. */
export interface AlertPerson {
  id: number;
  employeeId: string;
  displayName: string;
  role: 'Cleaner' | 'Supervisor' | 'Admin';
  areaCode?: string;
}

/** CoverSuggestionDto: what to send to assign the cover, while one can still be given. areaId for a Cleaner, buildingId for a Supervisor. */
export interface CoverSuggestion {
  userId: number;
  areaId?: number;
  buildingId?: number;
  shiftDate: string;
  shift: Shift;
}

/**
 * AlertDto. recordId is the row behind it; the same id may come with two kinds, so an alert is known by kind and id.
 * at is when it happened; for MissingAttendance it is the shift's end. areaCode is where it happened.
 */
export interface Alert {
  kind: AlertKind;
  recordId: number;
  at: string;
  person: AlertPerson;
  areaCode: string;
  areaName: string;
  signCode?: string;
  pointName?: string;
  distanceM?: number;
  radiusM?: number;
  accuracyM?: number;
  missingEvents: AttendanceEvent[];
  reviewed: boolean;
  reviewedAt?: string;
  cover?: CoverSuggestion;
  blockReason?: BlockedScan['reason'];
}

const alertListApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /** Yesterday and today, newest first (AlertQuery). */
    getAlerts: build.query<Alert[], void>({
      query: () => '/admin/alerts',
      providesTags: ['Alerts'],
    }),
  }),
});

export const { useGetAlertsQuery } = alertListApi;

/** The alerts no Admin has closed yet: the menu's count, wireframe D1 "รายการที่ขึ้นเตือน (4)". */
export function openAlertCount(alerts: readonly Alert[] | undefined): number {
  return alerts ? alerts.filter((alert) => !alert.reviewed).length : 0;
}
```

Replace the whole of `frontend/src/shared/components/AdminNav.tsx` with:

```tsx
import { NavLink } from 'react-router';
import { openAlertCount, useGetAlertsQuery } from '../api/alertListApi';
import { livePoll } from '../api/baseApi';
import './shared.css';

/**
 * The Admin menu of wireframes D1–D10, listing only the pages that exist (plan 10 decision 2). Plan 12 adds
 * สรุปการตรวจ, ส่งออก Excel and ประวัติ Admin with their pages.
 */
const adminLinks = [
  { to: '/dashboard', label: 'สถานะจุด', counted: false },
  { to: '/admin/attendance', label: 'การเข้างาน', counted: false },
  { to: '/admin/alerts', label: 'รายการที่ขึ้นเตือน', counted: true },
  { to: '/admin/areas', label: 'จัดการ Area และป้าย', counted: false },
  { to: '/admin/accounts', label: 'บัญชีผู้ใช้', counted: false },
] as const;

export function AdminNav() {
  // plan 11 decision 12: the open alerts, asked every 30 s on every Admin page; no count while unknown or none
  const { data: alerts } = useGetAlertsQuery(undefined, { pollingInterval: livePoll.pollingInterval });
  const open = openAlertCount(alerts);

  return (
    <nav className="app-nav" aria-label="เมนู Admin">
      <ul>
        {adminLinks.map((link) => (
          <li key={link.to}>
            <NavLink
              to={link.to}
              className={({ isActive }) => (isActive ? 'app-nav__link app-nav__link--active' : 'app-nav__link')}
            >
              {link.counted && open > 0 ? `${link.label} (${open})` : link.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
```

- [ ] **Step 5: The rules and the review mutation**

Create `frontend/src/pages/alerts/alertRules.ts`:

```ts
import type { Alert, AlertKind } from '../../shared/api/alertListApi';
import type { Shift } from '../../shared/api/types';
import { eventLabel, shiftShortLabel } from '../../shared/utils/labels';
import { formatDate, formatShiftDate, formatTime } from '../../shared/utils/thaiTime';

/** Wireframe D3's tabs: "ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด". */
export type ReviewTab = 'open' | 'reviewed' | 'all';

/** Wireframe D3's type picker: the three GPS flags are one choice. */
export type AlertGroup = 'all' | 'blocked' | 'far' | 'missing';

export const alertGroupLabel: Record<AlertGroup, string> = {
  all: 'ทุกประเภท',
  blocked: 'สแกนแล้วถูกบล็อก',
  far: 'ไกลจากป้าย',
  missing: 'ไม่ได้ลงเวลา',
};

const groupOf: Record<AlertKind, Exclude<AlertGroup, 'all'>> = {
  BlockedScan: 'blocked',
  AttendanceGps: 'far',
  SubmissionGps: 'far',
  InspectionGps: 'far',
  MissingAttendance: 'missing',
};

export function filterAlerts(alerts: readonly Alert[], tab: ReviewTab, group: AlertGroup): Alert[] {
  return alerts.filter(
    (alert) =>
      (tab === 'all' || alert.reviewed === (tab === 'reviewed')) && (group === 'all' || groupOf[alert.kind] === group),
  );
}

/** What happened. A Blocked Scan says why it was refused (blockReason), so a Supervisor's is never "สแกนนอก Area". */
export function alertTitle(alert: Alert): string {
  switch (alert.kind) {
    case 'BlockedScan':
      if (alert.blockReason === 'OtherBuilding') {
        return 'สแกนนอกตึก (ถูกบล็อก)';
      }
      return alert.blockReason === 'OutsideShift' ? 'สแกนนอกกะ (ถูกบล็อก)' : 'สแกนนอก Area (ถูกบล็อก)';
    case 'AttendanceGps':
      return 'ลงเวลาไกลจากป้าย';
    case 'SubmissionGps':
      return 'ส่งงานไกลจากป้าย';
    case 'InspectionGps':
      return 'ตรวจงานไกลจากป้าย';
    case 'MissingAttendance':
      return `ไม่ได้ลงเวลา${alert.missingEvents.map((event) => eventLabel[event]).join(' และ')}`;
  }
}

/** Who, with Supervisors marked as wireframe D3 does. */
export function alertWho(alert: Alert): string {
  return alert.person.role === 'Supervisor' ? `${alert.person.displayName} (หัวหน้า)` : alert.person.displayName;
}

/** The sign, the place and the distance, as far as the server knows them. */
export function alertDetail(alert: Alert): string {
  const place = `${alert.areaCode} ${alert.areaName}`;
  const sign = alert.signCode
    ? alert.pointName
      ? `ป้าย ${alert.signCode} ${alert.pointName}`
      : `ป้ายลงเวลา ${alert.signCode}`
    : null;
  if (alert.kind === 'BlockedScan') {
    const own =
      alert.person.role === 'Cleaner' && alert.person.areaCode
        ? `Area ของ${alert.person.displayName}คือ ${alert.person.areaCode}`
        : null;
    return [sign ? `${sign} ของ ${place}` : place, own, 'ไม่ได้บันทึกงาน'].filter(Boolean).join(' · ');
  }
  if (alert.kind === 'MissingAttendance') {
    const slot = slotOfShiftEnd(alert.at);
    return `${shiftShortLabel[slot.shift]} ${formatShiftDate(slot.shiftDate)} · ${place}`;
  }
  const distance =
    alert.distanceM == null ? null : `ห่าง ${alert.distanceM} ม.${alert.radiusM == null ? '' : ` (รัศมี ${alert.radiusM} ม.)`}`;
  const accuracy = alert.accuracyM == null ? null : `ความแม่นยำ ±${alert.accuracyM} ม.`;
  return [sign, place, distance, accuracy].filter(Boolean).join(' · ');
}

/**
 * A missing-attendance alert's time is its shift's end (AlertQuery): 19:00 ends that date's Day shift, 07:00 the Night
 * shift of the date before (facility-0040).
 */
export function slotOfShiftEnd(at: string): { shiftDate: string; shift: Shift } {
  const [day, month, year] = formatDate(at).split('/').map(Number);
  return formatTime(at) === '19:00'
    ? { shiftDate: isoDate(year, month, day), shift: 'Day' }
    : { shiftDate: isoDate(year, month, day - 1), shift: 'Night' };
}

function isoDate(year: number, month: number, day: number): string {
  return new Date(Date.UTC(year, month - 1, day)).toISOString().slice(0, 10);
}

/** D2 with that shift and the correction form open on the first missing event (plan 11 decision 9). Built from server ids and enums only. */
export function fillLink(alert: Alert): string {
  const slot = slotOfShiftEnd(alert.at);
  const params = new URLSearchParams({
    date: slot.shiftDate,
    shift: slot.shift,
    fill: String(alert.person.id),
    event: alert.missingEvents[0] ?? 'ShiftOut',
  });
  return `/admin/attendance?${params.toString()}`;
}
```

Note: the `alertTitle` join is `' และ'` with a space before only, so `['BreakIn', 'ShiftOut']` reads "ไม่ได้ลงเวลากลับจากพัก และเลิกงาน", as the test expects.

Create `frontend/src/pages/alerts/alertsApi.ts`:

```ts
import type { AlertKind } from '../../shared/api/alertListApi';
import { baseApi } from '../../shared/api/baseApi';

/** ReviewAlertRequest: "คุยแล้ว ปิดเตือน". note is optional and kept as the history row's reason. */
export interface ReviewRequest {
  kind: AlertKind;
  recordId: number;
  note?: string;
}

/** The alert page's actions. Each one asks for the list again, also after a refusal (another Admin may have closed it). */
const alertsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    reviewAlert: build.mutation<void, ReviewRequest>({
      query: (body) => ({ url: '/admin/alerts/review', method: 'POST', body }),
      invalidatesTags: ['Alerts'],
    }),
  }),
});

export const { useReviewAlertMutation } = alertsApi;
```

- [ ] **Step 6: The item and the page**

Create `frontend/src/pages/alerts/AlertItem.tsx`:

```tsx
import { useState } from 'react';
import { Link } from 'react-router';
import type { Alert } from '../../shared/api/alertListApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { eventLabel } from '../../shared/utils/labels';
import { formatDayTime } from '../../shared/utils/thaiTime';
import { alertDetail, alertTitle, alertWho, fillLink } from './alertRules';
import { useReviewAlertMutation } from './alertsApi';

/** A line at the top of the alert page; an action's result outlives the item, which may move tabs. */
export type Notice = { kind: 'ok' | 'error'; text: string };

interface AlertItemProps {
  alert: Alert;
  onNotice: (notice: Notice) => void;
}

/** One alert of wireframe D3: when, who, what, where, and what the Admin can do about it. Server text is React text only. */
export function AlertItem({ alert, onNotice }: AlertItemProps) {
  const [reviewing, setReviewing] = useState(false);
  const title = alertTitle(alert);

  return (
    <li className="alerts-item">
      <article aria-label={`${title} · ${alertWho(alert)}`}>
        <div className="alerts-item__top">
          <span className="alerts-item__time">{formatDayTime(alert.at)}</span>
          <strong>{alertWho(alert)}</strong>
          <span className={`alerts-chip alerts-chip--${alert.kind}`}>{title}</span>
        </div>
        <span className="alerts-item__detail">{alertDetail(alert)}</span>
        <div className="alerts-item__actions">
          {alert.reviewed && (
            <span className="app-muted">{alert.reviewedAt ? `คุยแล้ว ${formatDayTime(alert.reviewedAt)}` : 'คุยแล้ว'}</span>
          )}
          {alert.kind === 'MissingAttendance' && (
            // facility-0051: it closes once the time is filled, on the attendance page
            <Link className="app-button" to={fillLink(alert)}>
              {`เติมเวลา${eventLabel[alert.missingEvents[0] ?? 'ShiftOut']}`}
            </Link>
          )}
          {!alert.reviewed && alert.kind !== 'MissingAttendance' && !reviewing && (
            <button type="button" className="app-button" onClick={() => setReviewing(true)}>
              คุยแล้ว ปิดเตือน
            </button>
          )}
        </div>
        {reviewing && (
          <ReviewForm
            alert={alert}
            onDone={() => {
              setReviewing(false);
              onNotice({ kind: 'ok', text: `ปิดเตือน${title}ของ ${alert.person.displayName} แล้ว` });
            }}
            onError={(text) => onNotice({ kind: 'error', text })}
            onCancel={() => setReviewing(false)}
          />
        )}
      </article>
    </li>
  );
}

interface ReviewFormProps {
  alert: Alert;
  onDone: () => void;
  onError: (text: string) => void;
  onCancel: () => void;
}

/** "คุยแล้ว ปิดเตือน" with an optional note (plan 11 decision 11). One send at a time. */
function ReviewForm({ alert, onDone, onError, onCancel }: ReviewFormProps) {
  const [note, setNote] = useState('');
  const [review, { isLoading }] = useReviewAlertMutation();

  const send = async () => {
    try {
      await review({ kind: alert.kind, recordId: alert.recordId, note: note.trim() === '' ? undefined : note.trim() }).unwrap();
      onDone();
    } catch (error) {
      onError(getErrorMessage(error));
    }
  };

  return (
    <div className="alerts-review">
      <label className="app-field">
        หมายเหตุ (ไม่บังคับ)
        <input className="app-input" value={note} maxLength={500} onChange={(event) => setNote(event.target.value)} />
      </label>
      <div className="app-confirm__buttons">
        <button type="button" className="app-button app-button--primary" disabled={isLoading} onClick={() => void send()}>
          ยืนยันปิดเตือน
        </button>
        <button type="button" className="app-button" onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </div>
  );
}
```

Create `frontend/src/pages/alerts/AlertsPage.tsx`:

```tsx
import { useState } from 'react';
import { useGetAlertsQuery } from '../../shared/api/alertListApi';
import { livePoll } from '../../shared/api/baseApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AlertItem, type Notice } from './AlertItem';
import { alertGroupLabel, filterAlerts, type AlertGroup, type ReviewTab } from './alertRules';
import './alerts.css';

const GROUPS: readonly AlertGroup[] = ['all', 'blocked', 'far', 'missing'];

/**
 * Wireframe D3: the alerts of yesterday and today for the Admin to talk through (facility-0035, 0037, 0041, 0051).
 * No hub event carries alerts, so the list is asked on every open, every 30 s and after each action.
 */
export function AlertsPage() {
  const alerts = useGetAlertsQuery(undefined, livePoll);
  const [tab, setTab] = useState<ReviewTab>('open');
  const [group, setGroup] = useState<AlertGroup>('all');
  const [notice, setNotice] = useState<Notice | null>(null);
  const list = alerts.data ?? [];
  const openCount = list.filter((alert) => !alert.reviewed).length;
  const shown = filterAlerts(list, tab, group);
  const tabs: { tab: ReviewTab; label: string }[] = [
    { tab: 'open', label: `ยังไม่ได้คุย (${openCount})` },
    { tab: 'reviewed', label: `คุยแล้ว (${list.length - openCount})` },
    { tab: 'all', label: 'ทั้งหมด' },
  ];

  return (
    <main className="alerts-page">
      <header>
        <span className="app-muted">เมื่อวานและวันนี้ · Admin เท่านั้น</span>
        <h1>รายการที่ขึ้นเตือน</h1>
      </header>
      {notice && (
        <p
          className={notice.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
          role={notice.kind === 'ok' ? 'status' : 'alert'}
        >
          {notice.text}
        </p>
      )}
      <section className="alerts-section" aria-labelledby="alerts-list">
        <h2 id="alerts-list">เตือนจากการสแกน</h2>
        <div className="alerts-filters">
          <div className="alerts-tabs" role="group" aria-label="การคุย">
            {tabs.map((item) => (
              <button
                key={item.tab}
                type="button"
                className="alerts-tab"
                aria-pressed={tab === item.tab}
                onClick={() => setTab(item.tab)}
              >
                {item.label}
              </button>
            ))}
          </div>
          <label className="alerts-type">
            ประเภท
            <select className="app-input" value={group} onChange={(event) => setGroup(event.target.value as AlertGroup)}>
              {GROUPS.map((value) => (
                <option key={value} value={value}>
                  {alertGroupLabel[value]}
                </option>
              ))}
            </select>
          </label>
        </div>
        {alerts.isLoading && <p className="app-muted">กำลังโหลด...</p>}
        {alerts.error && (
          <p className="app-note app-note--error" role="alert">
            {getErrorMessage(alerts.error)}
          </p>
        )}
        {alerts.data && shown.length === 0 && <p className="app-note app-note--info">ไม่มีรายการ</p>}
        <ul className="alerts-list">
          {shown.map((alert) => (
            // The same record id can come with two kinds (a far Shift-In and its missing Shift-Out)
            <AlertItem key={`${alert.kind}-${alert.recordId}`} alert={alert} onNotice={setNotice} />
          ))}
        </ul>
        <p className="app-muted">
          ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติของ Admin
        </p>
      </section>
    </main>
  );
}
```

Create `frontend/src/pages/alerts/index.ts`:

```ts
export { AlertsPage } from './AlertsPage';
```

Create `frontend/src/pages/alerts/alerts.css`:

```css
/* Alerts (D3). Class prefix: alerts- */

.alerts-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.alerts-page h1 {
  font-size: 26px;
  font-weight: 700;
}

.alerts-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 16px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.alerts-section h2 {
  font-size: 17px;
  font-weight: 700;
}

.alerts-filters,
.alerts-tabs {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  font-size: 14px;
}

.alerts-tab {
  min-height: 36px;
  padding: 0 12px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-surface);
  color: var(--color-text);
  font-size: 14px;
  cursor: pointer;
}

.alerts-tab[aria-pressed='true'] {
  border-color: var(--color-text);
  background: var(--color-text);
  color: var(--color-surface);
  font-weight: 600;
}

.alerts-type {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.alerts-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.alerts-item article {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 12px 14px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.alerts-item__top,
.alerts-item__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}

.alerts-item__time {
  color: var(--color-muted);
  font-variant-numeric: tabular-nums;
}

.alerts-item__detail {
  color: var(--color-text-soft);
  font-size: 14px;
}

.alerts-chip {
  padding: 2px 10px;
  border-radius: 999px;
  background: var(--color-status-overdue);
  color: var(--color-on-primary);
  font-size: 12px;
  font-weight: 600;
}

.alerts-chip--BlockedScan {
  background: var(--color-status-rework);
}

.alerts-chip--MissingAttendance {
  background: var(--color-status-pending);
}

.alerts-review {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-width: 480px;
}
```

- [ ] **Step 7: The route**

In `frontend/src/router.tsx`, after the `admin/attendance` route, add:

```tsx
              // facility-0035, 0041, 0048, 0051, wireframe D3
              { path: 'admin/alerts', lazy: async () => ({ Component: (await import('./pages/alerts')).AlertsPage }) },
```

- [ ] **Step 8: Run the tests**

Run: `cd frontend && npx vitest run`
Expected: PASS, 304 tests (292 + 1 time + 4 rules + 7 page). The older Admin page tests still pass: their fake server answers the menu's `GET /api/admin/alerts` with 404, so the menu shows no count.

- [ ] **Step 9: Measure, lint and commit**

Run: `cd frontend && npm run build && grep -o 'assets/index-[A-Za-z0-9_-]*\.js' dist/index.html`. Note the main file's gzip size (the menu's alert query is in it), then run `rm -rf dist`.

Add to `## Task log (plan 11)` in `docs/handoff.md`:

```markdown
- <date> · Task 7 done: alert list D3 (/admin/alerts, lazy): yesterday and today, ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด, type picker (ถูกบล็อก, ไกลจากป้าย for the three GPS kinds, ไม่ได้ลงเวลา), each alert keyed by kind and id with time, who ((หัวหน้า) marked), why a Blocked Scan was refused, sign, place, distance, radius, accuracy; "คุยแล้ว ปิดเตือน" with an optional note, refusals at the top of the page, one send at a time; a missing Shift-Out links to D2's form; the menu shows "รายการที่ขึ้นเตือน (n)", polled every 30 s (shared/api/alertListApi.ts); formatDayTime. Main JS <size> kB gzip. Frontend tests <n> passed. Next: Task 8 (covers on D3).
```

Then:

```bash
cd frontend && npm run lint
cd .. && git add docs/handoff.md frontend/src
git commit -m "feat(frontend): alert list D3 with close-with-note, and the open count in the Admin menu"
```

---
### Task 8: Covers on D3: Areas with nobody, a Blocked Scan's cover, this shift's covers

The alert page also shows the Areas with nobody this shift, each with a cover form, and gives a Blocked Scan its cover in one press. It lists this shift's covers, and each one can be cancelled.

**Files:**
- Modify: `frontend/src/pages/alerts/alertsApi.ts`, `frontend/src/pages/alerts/alertRules.ts`, `frontend/src/pages/alerts/alertRules.test.ts`
- Modify: `frontend/src/pages/alerts/AlertItem.tsx`, `frontend/src/pages/alerts/AlertsPage.tsx`, `frontend/src/pages/alerts/alerts.css`
- Create: `frontend/src/pages/alerts/EmptyAreas.tsx`, `frontend/src/pages/alerts/CoverForm.tsx`, `frontend/src/pages/alerts/CoversThisShift.tsx`
- Modify: `frontend/src/test/liveFixtures.ts`, `frontend/src/pages/alerts/AlertsPage.test.tsx`
- Modify: `docs/handoff.md`
- Test: `frontend/src/pages/alerts/AlertCovers.test.tsx`

**Interfaces:**
- Consumes: `useGetAttendanceBoardQuery({})` and the board types (Task 5), `Alert` and `CoverSuggestion` (Task 7), and the `AttendanceBoard` and `Alerts` tags (Task 3).
- Produces:
  - In `pages/alerts/alertsApi.ts`:
    - Types `CoverCandidate`, `CoverAssignment` and `AssignCoverRequest`.
    - `useGetCoverCandidatesQuery({ areaId, shiftDate, shift })`, `useAssignCoverMutation()` and `useCancelCoverMutation()`. The last two invalidate `AttendanceBoard` and `Alerts`.
  - In `pages/alerts/alertRules.ts`: `candidateText(candidate)` and `coverNotice(cover)`.
  - Fixtures in `src/test/liveFixtures.ts`: `candidates` and `supervisorBlockedAlert`.

- [ ] **Step 1: Write the failing tests**

At the top of `frontend/src/test/liveFixtures.ts`, after the `alertListApi` import, add:

```ts
import type { CoverCandidate } from '../pages/alerts/alertsApi';
```

Append to `frontend/src/test/liveFixtures.ts`:

```ts
/** 11:00: the Supervisor scanned a point in building B; a building cover can still be given (facility-0048). */
export const supervisorBlockedAlert: Alert = {
  kind: 'BlockedScan',
  recordId: 302,
  at: '2026-10-15T04:00:00Z',
  person: { id: 31, employeeId: '660001', displayName: 'หัวหน้าทดสอบ', role: 'Supervisor' },
  areaCode: 'BR01',
  areaName: 'Area ตึกอื่น',
  signCode: 'BR01-01',
  pointName: 'ห้องประชุม',
  missingEvents: [],
  reviewed: false,
  cover: { userId: 31, buildingId: 2, shiftDate: '2026-10-15', shift: 'Day' },
  blockReason: 'OtherBuilding',
};

/** Who could cover AR12 this Day shift, best first as the server orders them: whoever scanned its signs, then by Shift-In. */
export const candidates: CoverCandidate[] = [
  {
    id: 25,
    employeeId: '650005',
    displayName: 'แม่บ้านที่อื่น',
    areaCode: 'AR03',
    shiftInAt: '2026-10-14T23:55:00Z',
    scannedThisAreaAt: '2026-10-15T02:10:00Z',
  },
  { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ', areaCode: 'AR01', shiftInAt: '2026-10-14T23:52:00Z' },
  { id: 30, employeeId: '650010', displayName: 'แม่บ้านมาสาย', areaCode: 'AR08' },
];
```

In `frontend/src/pages/alerts/alertRules.test.ts`, replace:

```ts
import { alertDetail, alertTitle, alertWho, fillLink, filterAlerts, slotOfShiftEnd } from './alertRules';
```

with:

```ts
import {
  alertDetail,
  alertTitle,
  alertWho,
  candidateText,
  coverNotice,
  fillLink,
  filterAlerts,
  slotOfShiftEnd,
} from './alertRules';
```

Replace:

```ts
  reviewedAlert,
} from '../../test/liveFixtures';
```

with:

```ts
  candidates,
  reviewedAlert,
} from '../../test/liveFixtures';
```

Inside the `describe` block, after the last test, add:

```ts
  it('describes a cover candidate and a made cover in words', () => {
    expect(candidates.map(candidateText)).toEqual([
      'แม่บ้านที่อื่น · AR03 · เข้างานแล้ว 06:55 · สแกนป้าย Area นี้เมื่อ 09:10',
      'แม่บ้านทดสอบ · AR01 · เข้างานแล้ว 06:52',
      'แม่บ้านมาสาย · AR08 · ยังไม่ลงเวลา',
    ]);
    const made = { id: 72, userId: 21, personName: 'แม่บ้านทดสอบ', shiftDate: '2026-10-15', shift: 'Day' as const, assignedAt: '2026-10-15T03:20:00Z' };
    expect(coverNotice({ ...made, areaId: 12, areaCode: 'AR12' })).toBe('มอบหมาย แม่บ้านทดสอบ ทำแทน AR12 แล้ว');
    expect(coverNotice({ ...made, personName: 'หัวหน้าทดสอบ', buildingId: 2, buildingCode: 'B' })).toBe(
      'มอบหมาย หัวหน้าทดสอบ ตรวจแทนตึก B แล้ว',
    );
  });
```

In `frontend/src/pages/alerts/AlertsPage.test.tsx`, replace:

```tsx
import { alerts, attendanceGpsAlert } from '../../test/liveFixtures';
```

with:

```tsx
import { alerts, attendanceBoard, attendanceGpsAlert } from '../../test/liveFixtures';
```

Replace:

```tsx
    'GET /api/admin/alerts': () => ({ status: 200, body: list() }),
    ...routes,
```

with:

```tsx
    'GET /api/admin/alerts': () => ({ status: 200, body: list() }),
    'GET /api/admin/attendance': () => ({ status: 200, body: attendanceBoard() }),
    ...routes,
```

Create `frontend/src/pages/alerts/AlertCovers.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { adminSession } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { alerts, attendanceBoard, candidates, supervisorBlockedAlert } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/alerts': () => ({ status: 200, body: [supervisorBlockedAlert, ...alerts] }),
    'GET /api/admin/attendance': () => ({ status: 200, body: attendanceBoard() }),
    'GET /api/admin/cover-assignments/candidates': () => ({ status: 200, body: candidates }),
    ...routes,
  });
}

const posts = (server: ReturnType<typeof serve>) =>
  server.calls.filter((call) => call.method === 'POST' && call.path === '/api/admin/cover-assignments');

describe('covers on the alert page (D3, facility-0041, 0048)', () => {
  it('lists the Areas with nobody this shift; มอบหมายคนทำแทน offers the candidates, best first, and assigns one', async () => {
    const server = serve({
      'POST /api/admin/cover-assignments': () => ({
        status: 201,
        body: { id: 72, userId: 21, personName: 'แม่บ้านทดสอบ', areaId: 12, areaCode: 'AR12', shiftDate: '2026-10-15', shift: 'Day', assignedAt: '2026-10-15T03:20:00Z' },
      }),
    });
    const { user } = await renderApp('/admin/alerts');

    const empty = await screen.findByRole('region', { name: 'Area ที่ยังไม่มีคน (2)' });
    expect(within(empty).getByText('แม่บ้านลา ยังไม่ลงเวลา')).toBeInTheDocument();
    expect(within(empty).getByText('ไม่มีแม่บ้านประจำ')).toBeInTheDocument();

    await user.click(within(empty).getByRole('button', { name: 'มอบหมายคนทำแทน AR12' }));
    const form = screen.getByRole('region', { name: 'มอบหมายคนทำแทน · AR12' });
    expect(within(form).getByText('แม่บ้านประจำ: แม่บ้านลา · ยังไม่ลงเวลา')).toBeInTheDocument();
    expect(within(form).getByText('มีผลเฉพาะกะเช้า 15/10/2026 · หมดสิทธิ์เองเมื่อจบกะ ไม่ต้องกดยกเลิก')).toBeInTheDocument();
    const who = await within(form).findByRole('combobox', { name: 'ให้ใครทำแทน' });
    expect(within(who).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'แม่บ้านที่อื่น · AR03 · เข้างานแล้ว 06:55 · สแกนป้าย Area นี้เมื่อ 09:10',
      'แม่บ้านทดสอบ · AR01 · เข้างานแล้ว 06:52',
      'แม่บ้านมาสาย · AR08 · ยังไม่ลงเวลา',
    ]);
    expect(server.calls.find((call) => call.path === '/api/admin/cover-assignments/candidates')?.search).toBe(
      '?areaId=12&shiftDate=2026-10-15&shift=Day',
    );

    await user.selectOptions(who, 'แม่บ้านทดสอบ · AR01 · เข้างานแล้ว 06:52');
    await user.click(within(form).getByRole('button', { name: 'ยืนยันมอบหมาย' }));

    expect(await screen.findByText('มอบหมาย แม่บ้านทดสอบ ทำแทน AR12 แล้ว')).toBeInTheDocument();
    expect(posts(server).map((call) => call.body)).toEqual([{ userId: 21, areaId: 12, shiftDate: '2026-10-15', shift: 'Day' }]);
    expect(screen.queryByRole('region', { name: 'มอบหมายคนทำแทน · AR12' })).not.toBeInTheDocument();
  });

  it('says when nobody can cover, shows a refusal in the form, and ยกเลิก closes it', async () => {
    serve({
      'GET /api/admin/cover-assignments/candidates': (request) => ({
        status: 200,
        body: request.search.includes('areaId=9') ? [] : candidates,
      }),
      'POST /api/admin/cover-assignments': () => ({ status: 400, body: { message: 'กะนี้จบไปแล้ว' } }),
    });
    const { user } = await renderApp('/admin/alerts');
    const empty = await screen.findByRole('region', { name: 'Area ที่ยังไม่มีคน (2)' });

    await user.click(within(empty).getByRole('button', { name: 'มอบหมายคนทำแทน AR09' }));
    const none = screen.getByRole('region', { name: 'มอบหมายคนทำแทน · AR09' });
    expect(await within(none).findByText('ไม่มีแม่บ้านกะนี้ที่ทำแทนได้')).toBeInTheDocument();
    expect(within(none).getByRole('button', { name: 'ยืนยันมอบหมาย' })).toBeDisabled();
    await user.click(within(none).getByRole('button', { name: 'ยกเลิก' }));
    expect(screen.queryByRole('region', { name: 'มอบหมายคนทำแทน · AR09' })).not.toBeInTheDocument();

    await user.click(within(empty).getByRole('button', { name: 'มอบหมายคนทำแทน AR12' }));
    const form = screen.getByRole('region', { name: 'มอบหมายคนทำแทน · AR12' });
    await within(form).findByRole('combobox', { name: 'ให้ใครทำแทน' });
    await user.click(within(form).getByRole('button', { name: 'ยืนยันมอบหมาย' }));

    expect(await within(form).findByRole('alert')).toHaveTextContent('กะนี้จบไปแล้ว');
  });

  it("a Cleaner's Blocked Scan gives its Area cover in one press, which also closes the alert (facility-0041 rule 4)", async () => {
    const server = serve({
      'POST /api/admin/cover-assignments': () => ({
        status: 201,
        body: { id: 73, userId: 25, personName: 'แม่บ้านที่อื่น', areaId: 12, areaCode: 'AR12', shiftDate: '2026-10-15', shift: 'Day', assignedAt: '2026-10-15T03:20:00Z' },
      }),
    });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'สแกนนอก Area (ถูกบล็อก) · แม่บ้านที่อื่น' });
    const alertGets = server.calls.filter((call) => call.path === '/api/admin/alerts').length;

    await user.click(within(item).getByRole('button', { name: 'ให้ทำแทน AR12 กะนี้' }));

    expect(await screen.findByText('มอบหมาย แม่บ้านที่อื่น ทำแทน AR12 แล้ว')).toBeInTheDocument();
    expect(posts(server).map((call) => call.body)).toEqual([
      { userId: 25, areaId: 12, shiftDate: '2026-10-15', shift: 'Day', blockedScanId: 301 },
    ]);
    await waitFor(() =>
      expect(server.calls.filter((call) => call.path === '/api/admin/alerts').length).toBeGreaterThan(alertGets),
    );
  });

  it("a Supervisor's Blocked Scan gives a building cover (facility-0048)", async () => {
    const server = serve({
      'POST /api/admin/cover-assignments': () => ({
        status: 201,
        body: { id: 74, userId: 31, personName: 'หัวหน้าทดสอบ', buildingId: 2, buildingCode: 'B', shiftDate: '2026-10-15', shift: 'Day', assignedAt: '2026-10-15T04:05:00Z' },
      }),
    });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'สแกนนอกตึก (ถูกบล็อก) · หัวหน้าทดสอบ (หัวหน้า)' });
    expect(within(item).getByText('ป้าย BR01-01 ห้องประชุม ของ BR01 Area ตึกอื่น · ไม่ได้บันทึกงาน')).toBeInTheDocument();

    await user.click(within(item).getByRole('button', { name: 'ให้ตรวจแทนตึกนี้กะนี้' }));

    expect(await screen.findByText('มอบหมาย หัวหน้าทดสอบ ตรวจแทนตึก B แล้ว')).toBeInTheDocument();
    expect(posts(server).map((call) => call.body)).toEqual([
      { userId: 31, buildingId: 2, shiftDate: '2026-10-15', shift: 'Day', blockedScanId: 302 },
    ]);
  });

  it("this shift's covers can be cancelled before the shift ends", async () => {
    const server = serve({ 'DELETE /api/admin/cover-assignments/71': () => ({ status: 204 }) });
    const { user } = await renderApp('/admin/alerts');

    const covers = await screen.findByRole('region', { name: 'คนทำแทนกะนี้ (1)' });
    expect(within(covers).getByRole('listitem')).toHaveTextContent(
      'แม่บ้านที่อื่น ทำแทน AR02 · แทน แม่บ้านป่วย · Admin มอบหมาย 07:10',
    );

    await user.click(within(covers).getByRole('button', { name: 'ยกเลิกการทำแทนของ แม่บ้านที่อื่น ที่ AR02' }));

    expect(await screen.findByText('ยกเลิกการทำแทนของ แม่บ้านที่อื่น ที่ AR02 แล้ว')).toBeInTheDocument();
    expect(server.calls.filter((call) => call.method === 'DELETE').map((call) => call.path)).toEqual([
      '/api/admin/cover-assignments/71',
    ]);
  });
});
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/alerts`
Expected: FAIL. `candidateText`, `coverNotice` and `CoverCandidate` do not exist, and the page has no "Area ที่ยังไม่มีคน" section and no cover buttons. The Task 7 page tests still pass.

- [ ] **Step 3: The cover endpoints and words**

Replace the whole of `frontend/src/pages/alerts/alertsApi.ts` with:

```ts
import type { AlertKind } from '../../shared/api/alertListApi';
import { baseApi } from '../../shared/api/baseApi';
import type { Shift } from '../../shared/api/types';

/** ReviewAlertRequest: "คุยแล้ว ปิดเตือน". note is optional and kept as the history row's reason. */
export interface ReviewRequest {
  kind: AlertKind;
  recordId: number;
  note?: string;
}

/** CoverCandidateDto (wireframe D3 "ให้ใครทำแทน"): their own Area, their Shift-In, and their latest scan at this Area's signs. */
export interface CoverCandidate {
  id: number;
  employeeId: string;
  displayName: string;
  areaCode: string;
  shiftInAt?: string;
  scannedThisAreaAt?: string;
}

/** CoverAssignmentDto: the Area fields for a Cleaner's cover, the building fields for a Supervisor's. */
export interface CoverAssignment {
  id: number;
  userId: number;
  personName: string;
  areaId?: number;
  areaCode?: string;
  buildingId?: number;
  buildingCode?: string;
  shiftDate: string;
  shift: Shift;
  assignedAt: string;
}

/** AssignCoverRequest: areaId for a Cleaner (facility-0041), buildingId for a Supervisor (facility-0048), never both. */
export interface AssignCoverRequest {
  userId: number;
  areaId?: number;
  buildingId?: number;
  shiftDate: string;
  shift: Shift;
  /** From a Blocked Scan on the list: the server also closes that alert (facility-0041 rule 4). */
  blockedScanId?: number;
}

/**
 * The alert page's actions. Each one asks again for what it changes, also after a refusal (another Admin may have been
 * first): a cover changes the board and may close an alert.
 */
const alertsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    reviewAlert: build.mutation<void, ReviewRequest>({
      query: (body) => ({ url: '/admin/alerts/review', method: 'POST', body }),
      invalidatesTags: ['Alerts'],
    }),
    /** Changes with every check-in and cover, so it follows the board's tag. */
    getCoverCandidates: build.query<CoverCandidate[], { areaId: number; shiftDate: string; shift: Shift }>({
      query: ({ areaId, shiftDate, shift }) => ({
        url: '/admin/cover-assignments/candidates',
        params: { areaId, shiftDate, shift },
      }),
      providesTags: ['AttendanceBoard'],
    }),
    assignCover: build.mutation<CoverAssignment, AssignCoverRequest>({
      query: (body) => ({ url: '/admin/cover-assignments', method: 'POST', body }),
      invalidatesTags: ['AttendanceBoard', 'Alerts'],
    }),
    cancelCover: build.mutation<void, number>({
      query: (id) => ({ url: `/admin/cover-assignments/${id}`, method: 'DELETE' }),
      invalidatesTags: ['AttendanceBoard', 'Alerts'],
    }),
  }),
});

export const { useReviewAlertMutation, useGetCoverCandidatesQuery, useAssignCoverMutation, useCancelCoverMutation } =
  alertsApi;
```

In `frontend/src/pages/alerts/alertRules.ts`, after the line `import { formatDate, formatShiftDate, formatTime } from '../../shared/utils/thaiTime';`, add:

```ts
import type { CoverAssignment, CoverCandidate } from './alertsApi';
```

At the end of the file, add:

```ts
/** One choice of "ให้ใครทำแทน": who, their own Area, their Shift-In, and whether they already scanned this Area's signs. */
export function candidateText(candidate: CoverCandidate): string {
  return [
    candidate.displayName,
    candidate.areaCode,
    candidate.shiftInAt ? `เข้างานแล้ว ${formatTime(candidate.shiftInAt)}` : 'ยังไม่ลงเวลา',
    candidate.scannedThisAreaAt ? `สแกนป้าย Area นี้เมื่อ ${formatTime(candidate.scannedThisAreaAt)}` : null,
  ]
    .filter(Boolean)
    .join(' · ');
}

/** What the page says once a cover is made: an Area for a Cleaner, a building for a Supervisor. */
export function coverNotice(cover: CoverAssignment): string {
  return cover.areaCode
    ? `มอบหมาย ${cover.personName} ทำแทน ${cover.areaCode} แล้ว`
    : `มอบหมาย ${cover.personName} ตรวจแทนตึก ${cover.buildingCode ?? ''} แล้ว`;
}
```

- [ ] **Step 4: The cover button on a Blocked Scan**

In `frontend/src/pages/alerts/AlertItem.tsx`, replace:

```tsx
import type { Alert } from '../../shared/api/alertListApi';
```

with:

```tsx
import type { Alert, CoverSuggestion } from '../../shared/api/alertListApi';
```

Replace:

```tsx
import { alertDetail, alertTitle, alertWho, fillLink } from './alertRules';
import { useReviewAlertMutation } from './alertsApi';
```

with:

```tsx
import { alertDetail, alertTitle, alertWho, coverNotice, fillLink } from './alertRules';
import { useAssignCoverMutation, useReviewAlertMutation } from './alertsApi';
```

Replace:

```tsx
        <div className="alerts-item__actions">
          {alert.reviewed && (
```

with:

```tsx
        <div className="alerts-item__actions">
          {alert.cover && !alert.reviewed && <CoverButton alert={alert} cover={alert.cover} onNotice={onNotice} />}
          {alert.reviewed && (
```

At the end of the file, add:

```tsx
interface CoverButtonProps {
  alert: Alert;
  cover: CoverSuggestion;
  onNotice: (notice: Notice) => void;
}

/**
 * facility-0041 rule 4, 0048: the person refused at a sign gets the cover the server suggests, for that shift only, in
 * one press (undone by cancelling it). The server closes this alert with it. One send at a time.
 */
function CoverButton({ alert, cover, onNotice }: CoverButtonProps) {
  const [assign, { isLoading }] = useAssignCoverMutation();

  const give = async () => {
    try {
      const made = await assign({
        userId: cover.userId,
        areaId: cover.areaId,
        buildingId: cover.buildingId,
        shiftDate: cover.shiftDate,
        shift: cover.shift,
        blockedScanId: alert.recordId,
      }).unwrap();
      onNotice({ kind: 'ok', text: coverNotice(made) });
    } catch (error) {
      onNotice({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <button type="button" className="app-button app-button--primary" disabled={isLoading} onClick={() => void give()}>
      {cover.areaId != null ? `ให้ทำแทน ${alert.areaCode} กะนี้` : 'ให้ตรวจแทนตึกนี้กะนี้'}
    </button>
  );
}
```

- [ ] **Step 5: Areas with nobody, the cover form, this shift's covers**

Create `frontend/src/pages/alerts/EmptyAreas.tsx`:

```tsx
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { AttendanceBoard } from '../../shared/api/attendanceBoardApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';

interface EmptyAreasProps {
  board?: AttendanceBoard;
  error?: unknown;
  /** The Area whose cover form is open. */
  chosen: number | null;
  onChoose: (areaId: number) => void;
  /** The cover form, shown under the list while an Area is chosen. */
  children?: ReactNode;
}

/** Wireframe D3's first box: the Areas with nobody this shift, from the same board as the attendance page (facility-0050). */
export function EmptyAreas({ board, error, chosen, onChoose, children }: EmptyAreasProps) {
  const empty = board ? board.rows.filter((row) => row.state === 'Empty') : [];
  return (
    <section className="alerts-section" aria-labelledby="alerts-empty">
      <h2 id="alerts-empty">{`Area ที่ยังไม่มีคน (${empty.length})`}</h2>
      <span className="app-muted">
        ข้อมูลเดียวกับ <Link to="/admin/attendance">การเข้างาน</Link>
        {board ? ` · ${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)}` : ''}
      </span>
      {error != null && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {board && empty.length === 0 && <p className="app-note app-note--info">ทุก Area มีคนแล้ว</p>}
      <ul className="alerts-rows">
        {empty.map((row) => (
          <li key={row.areaId} className="alerts-row">
            <span>
              <strong>{`${row.areaCode} ${row.areaName}`}</strong>
              {` · ตึก ${row.buildingCode}`}
            </span>
            <span className="app-muted">{row.cleaner ? `${row.cleaner.displayName} ยังไม่ลงเวลา` : 'ไม่มีแม่บ้านประจำ'}</span>
            <button
              type="button"
              className="app-button"
              aria-label={`มอบหมายคนทำแทน ${row.areaCode}`}
              disabled={chosen === row.areaId}
              onClick={() => onChoose(row.areaId)}
            >
              {chosen === row.areaId ? 'กำลังมอบหมาย ›' : 'มอบหมายคนทำแทน'}
            </button>
          </li>
        ))}
      </ul>
      {children}
    </section>
  );
}
```

Create `frontend/src/pages/alerts/CoverForm.tsx`:

```tsx
import { useState } from 'react';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { candidateText, coverNotice } from './alertRules';
import { useAssignCoverMutation, useGetCoverCandidatesQuery } from './alertsApi';

interface CoverFormProps {
  board: AttendanceBoard;
  row: BoardRow;
  onDone: (text: string) => void;
  onCancel: () => void;
}

/**
 * Wireframe D3 "มอบหมายคนทำแทน" (facility-0041): the server's candidates, best first, for this shift only. The choice
 * lives here only (facility-0024); one send at a time.
 */
export function CoverForm({ board, row, onDone, onCancel }: CoverFormProps) {
  const candidates = useGetCoverCandidatesQuery(
    { areaId: row.areaId, shiftDate: board.shiftDate, shift: board.shift },
    freshOnOpen,
  );
  const [chosen, setChosen] = useState<number | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [assign, { isLoading }] = useAssignCoverMutation();
  const list = candidates.data ?? [];
  const userId = chosen ?? list[0]?.id; // the server lists the best first
  const headingId = `alerts-cover-${row.areaId}`;

  const save = async () => {
    if (userId == null) {
      return;
    }
    setProblem(null);
    try {
      const cover = await assign({ userId, areaId: row.areaId, shiftDate: board.shiftDate, shift: board.shift }).unwrap();
      onDone(coverNotice(cover));
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <section className="app-panel alerts-cover" aria-labelledby={headingId}>
      <h2 id={headingId}>{`มอบหมายคนทำแทน · ${row.areaCode}`}</h2>
      <span>{row.cleaner ? `แม่บ้านประจำ: ${row.cleaner.displayName} · ยังไม่ลงเวลา` : 'Area นี้ไม่มีแม่บ้านประจำกะนี้'}</span>
      {candidates.isLoading && <p className="app-muted">กำลังโหลดรายชื่อ...</p>}
      {candidates.error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(candidates.error)}
        </p>
      )}
      {candidates.data && list.length === 0 && <p className="app-note app-note--info">ไม่มีแม่บ้านกะนี้ที่ทำแทนได้</p>}
      {list.length > 0 && (
        <label className="app-field">
          ให้ใครทำแทน
          <select className="app-input" value={userId} onChange={(event) => setChosen(Number(event.target.value))}>
            {list.map((candidate) => (
              <option key={candidate.id} value={candidate.id}>
                {candidateText(candidate)}
              </option>
            ))}
          </select>
        </label>
      )}
      <span className="app-field__help">
        {`มีผลเฉพาะ${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)} · หมดสิทธิ์เองเมื่อจบกะ ไม่ต้องกดยกเลิก`}
      </span>
      <span className="app-field__help">
        แม่บ้านที่ทำแทนลงเวลาที่ป้าย Area ของตัวเองตามปกติ แล้วสแกนจุดของ Area นี้ได้ · งานที่สแกนจะบันทึกว่า "ทำแทน"
      </span>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="app-confirm__buttons">
        <button
          type="button"
          className="app-button app-button--primary"
          disabled={userId == null || isLoading}
          onClick={() => void save()}
        >
          ยืนยันมอบหมาย
        </button>
        <button type="button" className="app-button" onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </section>
  );
}
```

Create `frontend/src/pages/alerts/CoversThisShift.tsx`:

```tsx
import type { AttendanceBoard } from '../../shared/api/attendanceBoardApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { formatTime } from '../../shared/utils/thaiTime';
import type { Notice } from './AlertItem';
import { useCancelCoverMutation } from './alertsApi';

interface CoversThisShiftProps {
  board: AttendanceBoard;
  onNotice: (notice: Notice) => void;
}

/**
 * Wireframe D3 "คนทำแทนกะนี้": every Area cover of the shift on the clock, from the board. Each ends with the shift by
 * itself, or is cancelled before it in one press. Supervisors' building covers have no list endpoint (plan 11 decision 10).
 */
export function CoversThisShift({ board, onNotice }: CoversThisShiftProps) {
  const [cancel, { isLoading }] = useCancelCoverMutation();
  const covers = board.rows.flatMap((row) => row.covers.map((cover) => ({ row, cover })));

  const cancelCover = async (id: number, text: string) => {
    try {
      await cancel(id).unwrap();
      onNotice({ kind: 'ok', text });
    } catch (error) {
      onNotice({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <section className="alerts-section" aria-labelledby="alerts-covers">
      <h2 id="alerts-covers">{`คนทำแทนกะนี้ (${covers.length})`}</h2>
      {covers.length === 0 && <p className="app-muted">ยังไม่มีคนทำแทนกะนี้</p>}
      <ul className="alerts-rows">
        {covers.map(({ row, cover }) => (
          <li key={cover.id} className="alerts-row">
            <span>
              <strong>{cover.displayName}</strong>
              {` ทำแทน ${row.areaCode}`}
              <span className="app-muted">
                {` · แทน ${row.cleaner ? row.cleaner.displayName : 'ไม่มีแม่บ้านประจำ'} · Admin มอบหมาย ${formatTime(cover.assignedAt)}`}
              </span>
            </span>
            <span className="app-muted">หมดสิทธิ์เมื่อจบกะ</span>
            <button
              type="button"
              className="app-link-button"
              aria-label={`ยกเลิกการทำแทนของ ${cover.displayName} ที่ ${row.areaCode}`}
              disabled={isLoading}
              onClick={() => void cancelCover(cover.id, `ยกเลิกการทำแทนของ ${cover.displayName} ที่ ${row.areaCode} แล้ว`)}
            >
              ยกเลิกก่อนหมดกะ
            </button>
          </li>
        ))}
      </ul>
      <p className="app-muted">การตรวจแทนตึกของหัวหน้ายังไม่แสดงในหน้านี้</p>
    </section>
  );
}
```

Replace the whole of `frontend/src/pages/alerts/AlertsPage.tsx` with:

```tsx
import { useState } from 'react';
import { useGetAlertsQuery } from '../../shared/api/alertListApi';
import { useGetAttendanceBoardQuery } from '../../shared/api/attendanceBoardApi';
import { livePoll } from '../../shared/api/baseApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AlertItem, type Notice } from './AlertItem';
import { alertGroupLabel, filterAlerts, type AlertGroup, type ReviewTab } from './alertRules';
import { CoverForm } from './CoverForm';
import { CoversThisShift } from './CoversThisShift';
import { EmptyAreas } from './EmptyAreas';
import './alerts.css';

const GROUPS: readonly AlertGroup[] = ['all', 'blocked', 'far', 'missing'];

/**
 * Wireframe D3: the Areas with nobody this shift and their covers (facility-0041, 0048), and the alerts of yesterday and
 * today for the Admin to talk through (facility-0035, 0037, 0051). The board follows the hub like the attendance page;
 * no hub event carries alerts, so they are asked on every open, every 30 s and after each action.
 */
export function AlertsPage() {
  const alerts = useGetAlertsQuery(undefined, livePoll);
  const board = useGetAttendanceBoardQuery({}, livePoll);
  const [tab, setTab] = useState<ReviewTab>('open');
  const [group, setGroup] = useState<AlertGroup>('all');
  const [notice, setNotice] = useState<Notice | null>(null);
  const [coverAreaId, setCoverAreaId] = useState<number | null>(null);
  const list = alerts.data ?? [];
  const openCount = list.filter((alert) => !alert.reviewed).length;
  const shown = filterAlerts(list, tab, group);
  // The form closes by itself once the Area has someone (a cover made here or elsewhere)
  const coverRow = board.data?.rows.find((row) => row.areaId === coverAreaId && row.state === 'Empty');
  const tabs: { tab: ReviewTab; label: string }[] = [
    { tab: 'open', label: `ยังไม่ได้คุย (${openCount})` },
    { tab: 'reviewed', label: `คุยแล้ว (${list.length - openCount})` },
    { tab: 'all', label: 'ทั้งหมด' },
  ];

  return (
    <main className="alerts-page">
      <header>
        <span className="app-muted">เมื่อวานและวันนี้ · Admin เท่านั้น</span>
        <h1>รายการที่ขึ้นเตือน</h1>
      </header>
      {notice && (
        <p
          className={notice.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
          role={notice.kind === 'ok' ? 'status' : 'alert'}
        >
          {notice.text}
        </p>
      )}
      <EmptyAreas
        board={board.data}
        error={board.error}
        chosen={coverRow ? coverRow.areaId : null}
        onChoose={(areaId) => {
          setNotice(null);
          setCoverAreaId(areaId);
        }}
      >
        {board.data && coverRow && (
          <CoverForm
            key={coverRow.areaId}
            board={board.data}
            row={coverRow}
            onDone={(text) => {
              setNotice({ kind: 'ok', text });
              setCoverAreaId(null);
            }}
            onCancel={() => setCoverAreaId(null)}
          />
        )}
      </EmptyAreas>
      <section className="alerts-section" aria-labelledby="alerts-list">
        <h2 id="alerts-list">เตือนจากการสแกน</h2>
        <div className="alerts-filters">
          <div className="alerts-tabs" role="group" aria-label="การคุย">
            {tabs.map((item) => (
              <button
                key={item.tab}
                type="button"
                className="alerts-tab"
                aria-pressed={tab === item.tab}
                onClick={() => setTab(item.tab)}
              >
                {item.label}
              </button>
            ))}
          </div>
          <label className="alerts-type">
            ประเภท
            <select className="app-input" value={group} onChange={(event) => setGroup(event.target.value as AlertGroup)}>
              {GROUPS.map((value) => (
                <option key={value} value={value}>
                  {alertGroupLabel[value]}
                </option>
              ))}
            </select>
          </label>
        </div>
        {alerts.isLoading && <p className="app-muted">กำลังโหลด...</p>}
        {alerts.error && (
          <p className="app-note app-note--error" role="alert">
            {getErrorMessage(alerts.error)}
          </p>
        )}
        {alerts.data && shown.length === 0 && <p className="app-note app-note--info">ไม่มีรายการ</p>}
        <ul className="alerts-list">
          {shown.map((alert) => (
            // The same record id can come with two kinds (a far Shift-In and its missing Shift-Out)
            <AlertItem key={`${alert.kind}-${alert.recordId}`} alert={alert} onNotice={setNotice} />
          ))}
        </ul>
        <p className="app-muted">
          ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติของ Admin
        </p>
      </section>
      {board.data && <CoversThisShift board={board.data} onNotice={setNotice} />}
    </main>
  );
}
```

Append to `frontend/src/pages/alerts/alerts.css`:

```css

.alerts-rows {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.alerts-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 10px 12px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.alerts-cover {
  max-width: 640px;
  border-color: var(--color-primary);
}
```

- [ ] **Step 6: Run the tests**

Run: `cd frontend && npx vitest run`
Expected: PASS, 310 tests (304 + 1 rules + 5 cover tests). The Task 7 page tests still pass, now with the board answered.

- [ ] **Step 7: Lint and commit**

Add to `## Task log (plan 11)` in `docs/handoff.md`:

```markdown
- <date> · Task 8 done: covers on D3: "Area ที่ยังไม่มีคน" from the clock's board with "มอบหมายคนทำแทน" (candidates best first, "ยืนยันมอบหมาย", the server's refusal in the form, nobody to cover said); a Blocked Scan with a cover gives it in one press (Cleaner: Area; Supervisor: building; the server closes the alert); "คนทำแทนกะนี้" with "ยกเลิกก่อนหมดกะ"; Supervisors' building covers are not listed (no endpoint). Covers invalidate AttendanceBoard and Alerts. Frontend tests <n> passed. Next: Task 9 (docs, final checks, the controller's real run).
```

Then:

```bash
cd frontend && npm run lint
cd .. && git add docs/handoff.md frontend/src
git commit -m "feat(frontend): covers on D3 for empty Areas and Blocked Scans, and this shift's covers"
```

---
### Task 9: Docs, ADR amendments, final checks, the controller's real run

**Files:**
- Modify: `docs/adr/facility-0024-client-state-split.md`, `facility-0025-frontend-target-tree.md`, `facility-0026-frontend-router-shape.md`, `facility-0027-attendance-board-dashboard-tab-and-capacity.md`, `facility-0041-scan-only-own-area-with-cover-assignment.md`, `facility-0051-admin-corrections-and-audit-log.md`, `facility-0061-dashboard-keeps-30s-poll-beside-push.md`, `facility-0062-phone-bundle-budget-200kb-gzip.md`, `facility-0064-dashboard-hub-offline-keeps-todays-behaviour.md`
- Modify: `docs/architecture.md`, `frontend/README.md`, `README.md`, `docs/handoff.md`

**Interfaces:**
- Consumes: everything built in Tasks 1–8.
- Produces: docs only. No code changes, unless a final check fails.

- [ ] **Step 1: Measure the main file**

Run: `cd frontend && npm run build && grep -o 'assets/index-[A-Za-z0-9_-]*\.js' dist/index.html && ls dist/assets/*.js`
Note the gzip size of the main file (the one `dist/index.html` names) from the build report, and the list of Admin page files. Then run `rm -rf dist`. The number goes into Step 2 (facility-0062) and Step 6 (handoff), where this plan writes `<size>`.

- [ ] **Step 2: ADR amendments**

Append one line at the end of each ADR below, as its own paragraph (a blank line before it). Use the editor tools, not a shell heredoc, because the text is Thai. Replace `<วันที่>` with the commit's date (yyyy-MM-dd), and `<size>` with the number from Step 1.

`docs/adr/facility-0062-phone-bundle-budget-200kb-gzip.md`:

```markdown
> **วัด <วันที่> (แผน 11):** เจ้าของโปรเจกต์ตัดสิน 2026-10-19 ให้ทำข้อ 3 ก่อนเกินงบ: หน้า Admin ทุกหน้า (ของแผน 10 และหน้าใหม่) เป็น `lazy` route ของ React Router แยกเป็นไฟล์ที่โหลดครั้งแรกที่ Admin เปิดหน้านั้น หน้ามือถือยังอยู่ในไฟล์หลัก งบ 200 kB gzip ใช้กับไฟล์หลักที่ `index.html` โหลด วัดได้ <size> kB gzip หลังเพิ่ม Dashboard หน้าการเข้างาน และหน้ารายการที่ขึ้นเตือน ถ้าไฟล์ของหน้า Admin โหลดไม่ได้ (เช่น มี build ใหม่ระหว่างเปิดหน้าค้างไว้) หน้าจะขึ้นจอเกิดข้อผิดพลาดที่มีปุ่ม "โหลดใหม่"
```

`docs/adr/facility-0061-dashboard-keeps-30s-poll-beside-push.md`:

```markdown
> **แก้ไข <วันที่> (แผน 11):** ทำตามข้อ 1–4 แล้ว: `livePoll` ใน `shared/api/baseApi.ts` คือถามเซิร์ฟเวอร์ทุกครั้งที่เปิดหน้า ทุก 30 วินาที และเมื่อหน้าต่างได้ focus ใช้กับ Dashboard (`/dashboard`) หน้าการเข้างาน (`/admin/attendance`) และหน้ารายการที่ขึ้นเตือน (`/admin/alerts`) ซึ่งเป็นจอ Admin ทั้งหมด `setupListeners(store.dispatch)` อยู่ใน `main.tsx` จำนวนเตือนในเมนู Admin ก็ถามทุก 30 วินาที
```

`docs/adr/facility-0064-dashboard-hub-offline-keeps-todays-behaviour.md`:

```markdown
> **แก้ไข <วันที่> (แผน 11):** สถานะการเชื่อมต่อใน `hubSlice` แยกตาม key ของแต่ละ connection (`selectHubStatus(key)`) ไม่ใช่ค่าเดียวทั้งแอป เพราะ Admin อาจเปิดหลายหน้าที่ตาม hub พร้อมกัน ป้ายมุมบน (`shared/components/LiveBadge.tsx`) บน Dashboard และหน้าการเข้างานใช้ข้อความและสีตามข้อ 1: "SignalR Live" สีเขียว หรือ "Connecting..." สีส้ม การ์ดที่ push มาแทนการ์ดเดิมใน cache ทันที จุดที่หน้ายังไม่มีทำให้ถามรายการใหม่ หน้าการเข้างานรับ `AttendanceRecorded` แล้วถามตารางของกะนั้นใหม่
```

`docs/adr/facility-0024-client-state-split.md`:

```markdown
> **แก้ไข <วันที่> (แผน 11):** `dashboardSlice` เก็บ `buildingCode` (ตึก), `status` (สถานะเดียวหรือทั้งหมด) และ `issueOnly` (มีปัญหาที่แจ้ง) ไม่มี `searchQuery` และ `lastUpdatedPointId` เพราะ D1 ไม่มีช่องค้นหาและไม่มีการไฮไลต์การ์ด ค่าถูกล้างเมื่อ session จบ ส่วนวันที่ กะ และตึกของหน้าการเข้างานอยู่ใน URL
```

`docs/adr/facility-0027-attendance-board-dashboard-tab-and-capacity.md`:

```markdown
> **แก้ไข <วันที่> (แผน 11):** หน้าการเข้างานเป็นหน้าแยก `/admin/attendance` ในเมนู Admin ไม่ใช่แท็บย่อยของ Dashboard เพื่อให้แต่ละหน้ามีที่อยู่ของตัวเอง (facility-0026) วันที่ กะ และตึกอยู่ใน URL (`?date=yyyy-MM-dd&shift=Day|Night&building=<id>`) ถ้าไม่ระบุคือกะตามนาฬิกา ข้อ 5 ทำแล้ว: `AttendanceRecorded` ทำให้ตารางของกะนั้นถามเซิร์ฟเวอร์ใหม่ และหน้าถามทุก 30 วินาทีด้วย
```

`docs/adr/facility-0051-admin-corrections-and-audit-log.md`:

```markdown
> **แก้ไข <วันที่> (แผน 11):** หน้าการเข้างาน (D2) มีปุ่ม "เพิ่มเวลา" / "แก้เวลา" ในแถวที่มีแม่บ้านประจำ ฟอร์มให้เลือกรายการ (บอกเวลาตอนนี้ของแต่ละรายการ) พิมพ์เวลาแบบ "HH:mm" และต้องกรอกเหตุผลไม่เกิน 500 ตัวอักษร เตือน "ไม่ได้ลงเวลาเลิกงาน" ในหน้ารายการที่ขึ้นเตือนมีลิงก์ "เติมเวลา…" ที่เปิดฟอร์มนี้ของกะนั้นให้เลย
```

`docs/adr/facility-0041-scan-only-own-area-with-cover-assignment.md`:

```markdown
> **แก้ไข <วันที่> (แผน 11):** หน้ารายการที่ขึ้นเตือน (`/admin/alerts`) มอบหมายทำแทนได้สองทาง: จาก "Area ที่ยังไม่มีคน" โดยเลือกจากรายชื่อที่เซิร์ฟเวอร์เรียงให้ และจากเตือนสแกนที่ถูกบล็อกด้วยการกดครั้งเดียว ซึ่งปิดเตือนนั้นด้วย การทำแทนของกะนี้ยกเลิกได้ก่อนหมดกะ การตรวจแทนตึกของหัวหน้า (facility-0048) มอบหมายได้จากเตือนที่ถูกบล็อก แต่ยังแสดงเป็นรายการไม่ได้ เพราะยังไม่มี endpoint ที่คืนรายการนั้น
```

`docs/adr/facility-0026-frontend-router-shape.md`:

```markdown
> **แก้ไข <วันที่> (แผน 11):** เพิ่ม `/admin/attendance` (D2) และ `/admin/alerts` (D3) ใต้ guard ของ Admin หน้า Admin ทุกหน้าเป็น `lazy` route (facility-0062) และ route ราก (`/`) มี `hydrateFallbackElement` "กำลังโหลด..." ระหว่างโหลดไฟล์ของหน้าแรก
```

`docs/adr/facility-0025-frontend-target-tree.md`:

```markdown
> **แก้ไข <วันที่> (แผน 11):** เพิ่ม `pages/dashboard` (D1), `pages/attendance` (D2), `pages/alerts` (D3), `shared/api/attendanceBoardApi.ts` (ตารางการเข้างาน ใช้ทั้ง D2 และ D3), `shared/api/alertListApi.ts` (รายการเตือน ใช้ทั้ง D3 และเมนู Admin), `shared/components/LiveBadge.tsx` และ `store/slices/dashboardSlice.ts`
```

- [ ] **Step 3: Architecture**

In `docs/architecture.md`, replace:

```markdown
The Supervisor's My Building page opens the SignalR hub (plan 9, `shared/api/scanHub.ts`); the Admin Dashboard adds it in plan 11.
```

with:

```markdown
The Supervisor's My Building page opens the SignalR hub (plan 9, `shared/api/scanHub.ts`); the Admin's Dashboard and attendance board open it too (plan 11). Admin pages are separate files, loaded the first time an Admin opens them (lazy routes, [facility-0062](adr/facility-0062-phone-bundle-budget-200kb-gzip.md)), so phones never download them.
```

Replace:

```text
auth (M0, D0) · my-work (M9, M14) · check-in (M1–M3, M10) · scan (M4, M5, M8, M11–M13) · inspect (M6, M7, M16) · my-building (M15) · dashboard · areas (D4)
```

with:

```text
auth (M0, D0) · my-work (M9, M14) · check-in (M1–M3, M10) · scan (M4, M5, M8, M11–M13) · inspect (M6, M7, M16) · my-building (M15) · dashboard (D1) · attendance (D2) · alerts (D3) · areas (D4)
```

Replace:

```text
each: Page.tsx + pageApi.ts (injectEndpoints) + page.css"]
```

with:

```text
each: Page.tsx + pageApi.ts (injectEndpoints) + page.css<br/>Admin pages are lazy routes (own files)"]
```

Replace:

```text
        Store["store/<br/>authSlice: user + access token in memory<br/>hubSlice: scan hub live or not"]
```

with:

```text
        Store["store/<br/>authSlice: user + access token in memory<br/>hubSlice: each hub connection live or not<br/>dashboardSlice: Dashboard filters"]
```

Replace:

```text
    Pages -->|"my-building: onCacheEntryAdded"| HubF
    HubF --> Session
    HubF -->|"/hubs/scan?access_token=<br/>ScanRecorded, InspectionRecorded"| Hub
```

with:

```text
    Pages -->|"my-building, dashboard, attendance board: onCacheEntryAdded"| HubF
    HubF --> Session
    HubF -->|"/hubs/scan?access_token=<br/>ScanRecorded, InspectionRecorded<br/>AttendanceRecorded (Admins)"| Hub
```

Replace the note under the heading `## 3. Sequence diagram — a scan reaches the dashboard in real time` (the paragraph that starts `> Note (plan 7, updated in plan 8):`) with:

```markdown
> Note (plan 11): the phone side is plan 8's `pages/scan`; the Admin side is `pages/dashboard`, whose `getServicePoints` cache entry follows the hub with `followScanHub`. Plan 9's Supervisor live updates and plan 11's attendance board are in the section 2 diagram.
```

Replace:

```text
    participant Dash as Dashboard browser<br/>(DashboardView)
```

with:

```text
    participant Dash as Admin browser<br/>(pages/dashboard, getServicePoints cache)
```

Replace:

```text
    Dash->>Dash: Update the card and KPI bar, no page reload
```

with:

```text
    Dash->>Dash: followScanHub hands the card to the cache entry, updateCachedData replaces that point's card, no page reload (a point not on the page: GET /api/service-points again)
    Note over Dash: Every open, every 30 s and on window focus the Dashboard asks GET /api/service-points again (facility-0061), and after every hub start or reconnect (facility-0064)
```

Replace:

```text
    participant FE as Browser<br/>(authSession.ts)
```

with:

```text
    participant FE as Browser<br/>(shared/auth/session.ts, baseApi)
```

- [ ] **Step 4: The READMEs**

In `frontend/README.md`, replace:

```text
src/store/                               the store, authSlice, hubSlice (scan hub live or not), typed hooks
src/shared/api/                          baseApi (RTK Query), server types, myWorkApi, scanHub (SignalR into the cache), adminSetupApi (Admin reads)
```

with:

```text
src/store/                               the store, authSlice, hubSlice (each hub connection live or not), dashboardSlice (Dashboard filters), typed hooks
src/shared/api/                          baseApi (RTK Query, livePoll), server types, myWorkApi, scanHub (SignalR into the cache), adminSetupApi (Admin reads),
                                         attendanceBoardApi (D2 and D3), alertListApi (alerts and the menu count)
```

Replace:

```markdown
- A new Admin page goes under the Admin `ProtectedRoute` in `router.tsx`, and into `shared/components/AdminNav.tsx` if it belongs in the menu.
```

with:

```markdown
- A new Admin page goes under the Admin `ProtectedRoute` in `router.tsx` as a lazy route (`lazy: async () => ({ Component: (await import('./pages/<page>')).<Page> })`), so phones never download it (facility-0062), and into the lazy-route list in `router.test.tsx`. It goes into `shared/components/AdminNav.tsx` if it belongs in the menu. Phone pages stay in the main file.
- The Admin's live board uses the tags `ServicePoints`, `AttendanceBoard` (one per shift, `boardTag`) and `Alerts`; corrections and covers invalidate the last two.
```

Replace:

```markdown
SignalR gets its token from `getValidAccessToken`, which refreshes first when 30 s or less is left, on every connect and reconnect. A page that wants live updates opens the hub with `followScanHub` inside its query's `onCacheEntryAdded` (one connection per cache entry; see `pages/my-building/myBuildingApi.ts`).
```

with:

```markdown
SignalR gets its token from `getValidAccessToken`, which refreshes first when 30 s or less is left, on every connect and reconnect. A page that wants live updates opens the hub with `followScanHub` inside its query's `onCacheEntryAdded`. There is one connection per cache entry, and each has its own key in `hubSlice`. See `pages/my-building/myBuildingApi.ts`, `pages/dashboard/dashboardApi.ts` and `shared/api/attendanceBoardApi.ts`. The Admin's live pages also use `livePoll` (every open, every 30 s, on focus; facility-0061) and show `LiveBadge` with the connection's key.
```

At the end of `frontend/README.md`, add:

```markdown

Made-up live board answers (Dashboard points, the attendance board, alerts, cover candidates) are in `src/test/liveFixtures.ts`, with `apiIdle(store)`, which waits until no request is on its way. The 30 s poll is tested with `vi.useFakeTimers({ shouldAdvanceTime: true })` started before `renderApp` and restored in `finally`, and a window focus with `setupListeners(store.dispatch)` in the test. `renderApp` waits until the router has loaded a lazy page, so the page is there when it returns.
```

In `README.md`, replace this line:

```markdown
  - Admin เข้าที่ `/login/admin` ด้วยชื่อผู้ใช้ + รหัสผ่าน แล้วไปหน้า Dashboard (`/dashboard`) เมนู Admin มี "จัดการ Area และป้าย" (`/admin/areas`: Area จุด เวลารอบ รัศมี พิกัดป้าย ออก QR ใหม่ และพิมพ์ป้ายที่ `/admin/print`) และ "บัญชีผู้ใช้" (`/admin/accounts`)
```

with (editor tools, Thai):

```markdown
  - Admin เข้าที่ `/login/admin` ด้วยชื่อผู้ใช้ + รหัสผ่าน แล้วไปหน้า Dashboard (`/dashboard`: สถานะทุกจุดตาม Area อัปเดตสด กรองตามตึก สถานะ และมีปัญหาที่แจ้ง) เมนู Admin มี "การเข้างาน" (`/admin/attendance`: แต่ละ Area มีคนหรือยัง เพิ่ม/แก้เวลาพร้อมเหตุผล), "รายการที่ขึ้นเตือน" (`/admin/alerts`: ปิดเตือน มอบหมายและยกเลิกคนทำแทน ตัวเลขในเมนูคือเตือนที่ยังไม่ได้คุย), "จัดการ Area และป้าย" (`/admin/areas`: Area จุด เวลารอบ รัศมี พิกัดป้าย ออก QR ใหม่ และพิมพ์ป้ายที่ `/admin/print`) และ "บัญชีผู้ใช้" (`/admin/accounts`) หน้า Admin โหลดแยกจากหน้ามือถือ มือถือจึงไม่ต้องโหลดโค้ดของ Admin
```

- [ ] **Step 5: Handoff notes for plan 12**

In `docs/handoff.md`, under `## Notes for the frontend plan`, replace the bullet that starts `- Live updates (plan 9): open the hub with` (the whole bullet) with:

```markdown
- Live updates (plans 9 and 11): open the hub with `followScanHub({ key, … })` (`shared/api/scanHub.ts`) in the query's `onCacheEntryAdded`. `onPoint` patches the cache (ScanRecorded, InspectionRecorded), `onAttendance` takes AttendanceRecorded (Admins only), and `onMissed` refetches by tag. Each connection's status is `selectHubStatus(key)` (`store/slices/hubSlice.ts`), and `LiveBadge` shows it. Admin live pages use `livePoll` (every open, 30 s, focus). Tests drive the fake hub with `hub.latest()` from `src/test/fakeSignalR.ts`.
```

Replace the bullet that starts `- Admin pages (plan 10): add a plan-11 page` (the whole bullet) with:

```markdown
- Admin pages (plans 10 and 11): add a plan-12 page as a `lazy` route under the Admin guard in `router.tsx`, its path to the lazy-route list in `router.test.tsx`, and its link to `shared/components/AdminNav.tsx` (plan 12: สรุปการตรวจ, ส่งออก Excel, ประวัติ Admin). Admin setup data uses the `AdminSetup` tag; the live board uses `ServicePoints`, `AttendanceBoard` (one per shift) and `Alerts`. `src/test/adminFixtures.ts` and `src/test/liveFixtures.ts` have made-up Admin answers.
- Dates for plan 12: `formatShiftDate`, `parseShiftDate` (typed dd/mm/yyyy, checked before sending) and `formatDayTime` are in `shared/utils/thaiTime.ts`. D1 and D2 have no "ส่งออก Excel" link yet; add it with D8.
```

Replace the bullet that starts `- Bundle headroom after plan 10:` (the whole bullet) with:

```markdown
- Bundle after plan 11: Admin pages are lazy routes (owner, 2026-10-19). The main file a phone loads is <size> kB gzip of 200 kB. Plan 12's pages go into their own files.
```

Under `## Parked; not needed before merging`, add:

```markdown
- Supervisors' building covers (facility-0048) can be assigned from a Blocked Scan on D3, but no endpoint lists them, so the alert page cannot show or cancel them (plan 11 decision 10).
```

In the Status table, change the plan 11 row's status to: `done (branch `feat/admin-live-board`): `docs/superpowers/plans/2026-10-19-admin-live-board.md``.

- [ ] **Step 6: Final checks**

Run each, from the repo root unless it says `frontend`:

```bash
cd frontend && npm run lint && npx tsc -b && npx vitest run
```
Expected: no lint or type error. About 310 tests pass. Write down the exact number.

```bash
cd frontend && grep -rn 'style={' src --include='*.tsx'
```
Expected: nothing.

```bash
cd frontend && grep -rnE 'localStorage|sessionStorage|indexedDB|dangerouslySetInnerHTML' src --include='*.ts' --include='*.tsx'
```
Expected: nothing, apart from the eslint rule's own text in `eslint.config.js`, which is outside `src`.

```bash
cd frontend && grep -rnE 'toLocale(String|TimeString|DateString)' src --include='*.ts' --include='*.tsx'
```
Expected: nothing.

```bash
cd frontend && grep -rnE "'/(scan|check-in)/" src/pages/dashboard src/pages/attendance src/pages/alerts
```
Expected: nothing (facility-0059).

```bash
git diff --stat master -- backend
```
Expected: empty (API unchanged).

```bash
dotnet test backend/FacilityRealtime.slnx
```
Expected: 299 unit tests pass; API tests 531 pass and 5 are skipped.

- [ ] **Step 7: Commit the docs**

Add to `## Task log (plan 11)` in `docs/handoff.md`:

```markdown
- <date> · Task 9 done: ADR amendments (0024 dashboardSlice as built, 0025 tree, 0026 routes and hydrate fallback, 0027 D2 as its own page with the shift in the URL, 0041 covers on D3, 0051 corrections on D2, 0061 livePoll, 0062 lazy Admin routes and the main file <size> kB gzip, 0064 hub status per connection and the badge), architecture (section 1, the section 2 diagram, section 3 redrawn for pages/dashboard, section 4's client), README device notes, frontend README, handoff notes for plan 12. Frontend <n> tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
```

Then:

```bash
git add docs README.md frontend/README.md
git commit -m "docs: plan 11 ADR amendments, architecture, READMEs and handoff notes for plan 12"
```

- [ ] **Step 8: The controller's real run (controller only; subagents skip this step)**

Ask the owner for the MySQL root password. Type it only into the commands, never into a file or into memory.

1. Create the throwaway database `facility_live_test` and run the API in Development with `ConnectionStrings__DefaultConnection` pointing at it. The seed fills it on start.
2. Run `cd frontend && npm run build:api`, and open the API's own address (`http://localhost:5001`) in Chrome.
3. Open a second Chrome profile (or a guest window) as the seed Cleaner, and a third as the seed Supervisor. Set their location in DevTools › More tools › Sensors.

Walk through these, writing down what was seen:

1. **Admin login.** The Admin lands on `/dashboard` and sees the cards by Area with "SignalR Live". In the Network tab, the first load downloads the main file and the Dashboard's file only. Opening "การเข้างาน" downloads its own file.
2. **Check-in reaches D2.** The Cleaner checks in at their Area's sign (`/check-in/<token>` from the seed). D2's row changes to ทำงานอยู่ with the time, without a reload.
3. **A submission reaches D1.** The Cleaner submits at a point. The card turns รอตรวจ without a reload. The Supervisor inspects it, and the card turns ตรวจผ่าน.
4. **Filters.** Choose a building, a status and มีปัญหาที่แจ้ง. Go to บัญชีผู้ใช้ and come back: the filters are still set. Log out and in: they are cleared.
5. **Hub drop.** Stop the API for about 10 s, then start it again. The badge turns "Connecting...", then "SignalR Live", and the cards are fresh.
6. **Correction.** On D2, "เพิ่มเวลา" for an Area whose Cleaner has not checked in, with a reason. The row shows the time and "แก้โดย Admin". `GET /api/admin/audit-log` (with the Admin's token) has an ATTENDANCE_ADD row with the reason.
7. **Blocked Scan and cover.** The Cleaner scans a point of another Area. Within 30 s D3 shows "สแกนนอก Area (ถูกบล็อก)", and the menu count goes up. "ให้ทำแทน … กะนี้" moves it to คุยแล้ว. The cover shows in "คนทำแทนกะนี้" and on D2's row. "ยกเลิกก่อนหมดกะ" removes it.
8. **GPS flag.** The Cleaner checks in with the simulated location about 200 m from the sign. D3 shows "ลงเวลาไกลจากป้าย". Close it with a note: the menu count goes down.
9. **Phone size.** In the Cleaner's profile, reload `/my-work` with the Network tab open: no Admin page file is downloaded.
10. **The API log** has no warnings.

A missing Shift-Out needs a shift to end, so it is covered by the tests only. Say so in the handoff entry.

Afterwards, drop `facility_live_test` and delete the build output in the API's `wwwroot`. Never touch `facility_dashboard`.

Add to `## Task log (plan 11)` in `docs/handoff.md` one entry with what was seen (no password, phone number or real name), and commit it:

```bash
git add docs/handoff.md
git commit -m "docs(handoff): plan 11 real run"
```

- [ ] **Step 9: Next**

The whole-branch review runs next (sp-subagent-driven-development). After it, ask the owner before any push or PR (owner's rule). The PR description lists the 15 planning decisions above.

---

## Self-review (done while planning)

- **Spec coverage:**
  - facility-0061 (poll, focus, `setupListeners`): Tasks 3, 5 and 7.
  - facility-0064 (badge, refetch after start and reconnect, per-connection status): Tasks 2 and 3.
  - facility-0062 item 3: Task 1.
  - facility-0024 (`dashboardSlice`): Task 4.
  - facility-0027 and 0050 (D2 board, Areas with nobody first, AttendanceRecorded): Task 5.
  - facility-0051 (corrections with a reason, the missing Shift-Out link): Tasks 6 and 7.
  - facility-0035 and 0037 (GPS alerts closed with a note): Task 7.
  - facility-0041 and 0048 (covers from empty Areas and Blocked Scans, cancel): Task 8.
  - facility-0046 (issue tag and filter): Tasks 3 and 4.
  - facility-0059 (no sign links): Tasks 3 and 5, and Task 9's grep.
  - The deferred plan 9 items: Task 2.
- **Not covered by design:** D7, D8 and D9, which are plan 12. Also Supervisors' building cover list, which needs an endpoint (parked).
- **Names used across tasks:**
  - `followScanHub({ key, onPoint?, onAttendance?, onMissed })` and `selectHubStatus(key)`.
  - `livePoll`, `LiveBadge({ hubKey })` and `DASHBOARD_HUB`.
  - `attendanceHubKey(args)`, `boardTag(date, shift)` and `useGetAttendanceBoardQuery(args)`.
  - `AttendanceTable({ rows, onCorrect? })`.
  - `readBoardArgs`, `toBoardParams`, `readFill`, `correctionProblem` and `firstEvent`.
  - `useGetAlertsQuery`, `openAlertCount`, `filterAlerts`, `alertTitle`, `alertDetail`, `slotOfShiftEnd`, `fillLink`, `candidateText` and `coverNotice`.
  - `Notice` (exported from `AlertItem.tsx`), `apiIdle` and `attendanceBoard()`.
