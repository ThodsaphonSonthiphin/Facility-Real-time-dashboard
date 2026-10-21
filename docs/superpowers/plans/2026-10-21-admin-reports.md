# Admin Reports Implementation Plan (plan 12)

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Admin reads the past in the browser. The inspection summary (D7) shows what each Supervisor inspected over a date range. The export page (D8) downloads attendance, submissions and inspections as one `.xlsx`. The Admin history (D9) lists every Admin change with before, after and reason.

**Architecture:**
- **Three lazy Admin pages on the API that plans 4, 5 and 6 built.** No file under `backend/` changes.
  - `pages/inspection-summary/` (D7): `GET /api/admin/inspection-summary`. The range and building are in the URL.
  - `pages/export/` (D8): `POST /api/admin/export`. The answer is a file. The endpoint's `queryFn` hands it to the browser to save, so the `Blob` never enters the Redux store (it is not serializable).
  - `pages/history/` (D9): `GET /api/admin/audit-log` as an RTK Query **infinite query** (older pages by `beforeId`), with `GET /api/admin/audit-log/actors` for the "ผู้ทำ" filter. The range, actor and type are in the URL. Before and after are rendered field by field from the stored snapshots.
- **Shared pieces** (each used by two or three of the pages, and a page never imports another page):
  - `shared/utils/dateRange.ts`: date range rules (shortcuts, checks before asking, reading and writing the address).
  - `shared/utils/thaiTime.ts`: `thaiToday`.
  - `shared/components/RangeFields.tsx`: "ตั้งแต่" / "ถึง" typed as dd/mm/yyyy, plus the shortcuts วันนี้ · 7 วัน · เดือนนี้.
  - `shared/components/BuildingSelect.tsx`: "ตึก" (ทุกตึก or one building).
- **Read-only pages:** every read uses `freshOnOpen`. There is no hub, no poll and no cache tag.

**Tech Stack:** React 19.2, TypeScript 6.0, Vite 8.3, `@reduxjs/toolkit` 2.13.0 (RTK Query, `build.infiniteQuery`), `react-router` 7.18.4 (`lazy` routes), Vitest 5.0 on jsdom 30.1.2, Testing Library, eslint 9 with boundaries. **No new package.**

**Spec:** this plan implements the Admin report side of these ADRs, with wireframes D7, D8 and D9 for the screens. Where a wireframe and an ADR disagree, the ADR wins.
- `docs/adr/facility-0048-supervisor-assigned-to-building.md` rule 5: the inspection summary is Admin only. It has a choosable range (7 days by default) and shows, for each Supervisor:
  - the building
  - how many times they inspected
  - the points never inspected in the range ("9 จาก 12 จุด")
  - work submitted but not inspected ("ไม่ได้ตรวจ", facility-0047)
  - how many reworks they asked for
  - inspections with a GPS flag
- `docs/adr/facility-0053-excel-export-instead-of-report-pages.md`: the Admin picks a date range and exports three sets (การลงเวลา, การส่งงาน, การตรวจงาน). There is no raw GPS in the file. Each export is logged (facility-0051).
- `docs/adr/facility-0057-supervisor-building-page-and-audit-log-page.md` rule 2: "ประวัติ Admin" is Admin only and read-only. It filters by date range, actor and type, and shows before and after with the reason. It never shows a phone number or password.
- `docs/adr/facility-0062-phone-bundle-budget-200kb-gzip.md` item 3: Admin pages are lazy routes (done in plan 11). The new pages follow it.
- `docs/adr/facility-0022`, `0024`, `0025`, `0026`, `0063`, `0065`, `0066`, `0067`, `0068`, as in plans 7–11.
- Plans 4, 5 and 6 built the endpoints:
  - `docs/superpowers/plans/2026-10-14-admin-attendance-alerts-history-backend.md`
  - `2026-10-14-accounts-and-excel-export-backend.md`
  - `2026-10-14-supervisor-inspection-backend.md`
  - `docs/handoff.md` "Notes for the frontend plan" has the API quirks.
- Wireframes (`docs/design/wireframe/`): `SupervisorSummary.dc.html` (D7), `ExportExcel.dc.html` (D8), `AuditLog.dc.html` (D9).

**The API this plan uses (unchanged; read the C# if in doubt):**

| Call | Answer | Source |
|---|---|---|
| `GET /api/admin/inspection-summary[?from=yyyy-MM-dd&to=yyyy-MM-dd][&buildingId=n]` | `InspectionSummaryDto { from, to, rows[] }`. A row is `{ supervisorId, employeeId, displayName, buildingCode, shift, pointsInspected, pointsTotal, submitted, submittedInspected, notInspected, inspections, reworks, gpsFlagged }`, ordered by building, shift, then employee ID. Both dates or neither (then the last 7 Thai dates); at most 31 dates; 400 `{ message }` otherwise | `Endpoints/AdminInspectionSummaryEndpoints.cs`, `DTOs/InspectionSummaryDtos.cs` |
| `POST /api/admin/export` `{ from, to, buildingId?, sets: ("Attendance" \| "Submissions" \| "Inspections")[] }` | 200 with the `.xlsx`: `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `Content-Disposition: attachment; filename=facility_<from>_<to>.xlsx; filename*=UTF-8''…`. 400 `{ message }` for a bad range (1–31 dates), no set, or an unknown building. Writes `EXPORT_EXCEL` to the audit log | `Endpoints/AdminExportEndpoints.cs`, `DTOs/ExportDtos.cs` |
| `GET /api/admin/audit-log[?from&to][&actorId=n][&category=…][&beforeId=n][&limit=n]` | `AuditLogPageDto { entries[], nextBeforeId? }`, newest first, 100 rows by default. An entry is `{ id, occurredAt, actorId, actorName, action, category?, entityType, entityId?, summary, before?, after?, reason? }`. `before` and `after` are the stored snapshots as JSON objects with **PascalCase** keys (except `COVER_CANCEL`'s `cancelled`, `alsoCancelled`), and keep `null` fields inside. Range: both or neither (the last 7 Thai dates), at most 92 dates | `Endpoints/AdminAuditLogEndpoints.cs`, `DTOs/AuditLogDtos.cs`, `Application/Audit/AuditCategories.cs` |
| `GET /api/admin/audit-log/actors` | `AuditActorDto[] { id, displayName }`: everyone who appears in the log, by name | same |
| `GET /api/admin/buildings` | `BuildingDto[] { id, code, name }` (plan 10's `useGetBuildingsQuery`) | `AdminAreaEndpoints.cs` |

JSON is camelCase and enums are strings. Times are UTC ISO strings ending in `Z`. A `DateOnly` such as `from` arrives as `"2026-10-15"`. **Null fields are left out** of the DTOs (but not from inside `before` / `after`, which the server passes through as stored).

`category` is one of `Attendance`, `Cover`, `AlertReview`, `Setup`, `QrToken`, `Account` or `Export`. The actions behind each are:

| Category | Actions |
|---|---|
| `Attendance` | `ATTENDANCE_ADD`, `ATTENDANCE_EDIT` |
| `Cover` | `COVER_ASSIGN`, `COVER_CANCEL` |
| `AlertReview` | `ALERT_REVIEW` |
| `Setup` | `AREA_CREATE`, `AREA_UPDATE`, `AREA_ACTIVATE`, `AREA_DEACTIVATE`, `POINT_CREATE`, `POINT_UPDATE`, `POINT_ACTIVATE`, `POINT_DEACTIVATE`, `SIGN_LOCATION`, `SIGN_RADIUS` |
| `QrToken` | `QR_REGENERATE` |
| `Account` | `ACCOUNT_CREATE`, `ACCOUNT_UPDATE`, `ACCOUNT_PHONE`, `ACCOUNT_PASSWORD`, `ACCOUNT_ACTIVATE`, `ACCOUNT_DEACTIVATE` |
| `Export` | `EXPORT_EXCEL` |

**The snapshots each action stores** (the C# `AuditTrail.Add` calls in `backend/src/FacilityRealtime.Api/Endpoints/`):

| Action | before → after |
|---|---|
| `ATTENDANCE_ADD` / `_EDIT` | `{ EventType, OccurredAt, Source }` (none before an add) |
| `COVER_ASSIGN` | after `{ UserId, AreaId, BuildingId, ShiftDate: "yyyy-MM-dd", Shift, BlockedScanId }` |
| `COVER_CANCEL` | `{ cancelled: false }` → `{ cancelled: true, alsoCancelled: [ids] }` |
| `ALERT_REVIEW` | `{ Reviewed: false }` → `{ Reviewed: true, AlertType }` |
| `AREA_CREATE` / `_UPDATE` | `{ Code, Name, BuildingId, ShiftPattern, DayCleanerId, NightCleanerId, RemovedNightWindows }` (ids only, no names, on purpose; `RemovedNightWindows` is `null` before and a count, often `0`, after) |
| `*_ACTIVATE` / `*_DEACTIVATE` | `{ IsActive }` (accounts: the whole account snapshot) |
| `POINT_CREATE` / `_UPDATE` | `{ Name, RoundWindows: ["DAY 07:00-09:00", …] }` |
| `SIGN_LOCATION` | `{ Latitude, Longitude, LocationAccuracyM, LocationSource }` |
| `SIGN_RADIUS` | `{ RadiusM }` |
| `QR_REGENERATE` | `{ QrIssuedAt }` (UTC, with `Z`) |
| `ACCOUNT_CREATE` / `_UPDATE` | `{ Role, LoginName, DisplayName, AreaId, BuildingId, Shift, IsActive }` |
| `ACCOUNT_PHONE` / `_PASSWORD` | none (facility-0057) |
| `EXPORT_EXCEL` | after `{ From, To, BuildingId, Sets: [...], Rows: [...] }` |

**Where this plan sits:** the last of the six frontend plans.

| Plan | Scope | Wireframes |
|---|---|---|
| 7–10 | Foundation, Cleaner phone, Supervisor phone, Admin setup (merged) | M0–M17, D0, D4–D6, D10 |
| 11 | Admin live board (merged, PR #60) | D1, D2, D3 |
| **12 (this one)** | Admin reports: inspection summary, Excel export, Admin history | D7, D8, D9 |

## Global Constraints

- **Branch:** `feat/admin-reports`, created from `master` at `ca59320` (PR #61 merged). Do not merge it.
- **Machine:** the Mac. zsh, Node v26.8.2, npm 11.19.1, .NET SDK 10.0.401.
  - Frontend commands run in `frontend/`. Backend commands run from the repo root.
  - In zsh, quote globs: `grep --include='*.tsx'`.
  - Write files that hold Thai text with the editor tools, not shell heredocs.
  - An untracked `package-lock.json` sits at the repo root. It is not part of this plan: never `git add` it (add files by name, never `git add -A` or `git add .`).
- **No new package.** `package.json` does not change.
- **API unchanged.** No file under `backend/` changes. Where a wireframe shows something the API does not send, leave it out (decision 9).
- **Bundle (facility-0062):** the main file is the one `dist/index.html` loads. Its JavaScript must stay ≤ 200 kB gzip. The three new pages are lazy routes, so they are separate files. Tasks 2, 3, 4 and 6 measure it. If it goes over, stop and report to the controller.
- **Styles:**
  - No `style={...}` anywhere in `src/` (facility-0063).
  - Page prefixes are `summary-`, `export-` and `history-`. Shared components use `app-`. Use the existing tokens in `src/index.css`. This plan adds no token.
- **Text people type and server text** (names, summaries, reasons, messages) are rendered as React text only. No `dangerouslySetInnerHTML`, and no `href` or `src` built from typed text (facility-0066). Links are built from server values, ids and dates that passed `parseShiftDate` only.
- **No link to a sign page** (`/scan/…`, `/check-in/…`) on any Admin page (facility-0059).
- **Nothing is stored on the device:** no `localStorage`, `sessionStorage` or IndexedDB (facility-0068). Saving the downloaded `.xlsx` is the browser's own download, which the Admin asked for.
- **Thai time:**
  - Every time and date on screen goes through `shared/utils/thaiTime.ts`: `formatShiftDate`, `formatDayTime`, `parseShiftDate`, and the new `thaiToday`.
  - Never call `toLocaleString`, `toLocaleTimeString` or `toLocaleDateString` elsewhere.
- **The browser computes no report number.** Counts are shown as the server sends them. The one derived figure is D7's share, `submittedInspected / submitted` rounded (decision 4).
- **Server text is shown as is.** `getErrorMessage` shows the API's `{ message }`. Pages add their own text only for checks made before sending (facility-0022 item 4).
- **Missing fields:** a field the server may send as null arrives missing.
  - Types mark such fields optional.
  - Code tests them with truthiness or `== null`, never `=== null`.
  - Test fixtures leave them out. Inside `before` / `after`, fixtures keep `null`, as the server does.
- **One send at a time:** while the export is in flight, its button is disabled.
- **Tests:**
  - Vitest on jsdom. Only browser seams are faked:
    - `fetch`, with `src/test/fakeServer.ts` (Task 3 teaches it to answer a file).
    - The SignalR package, through `test.alias` (`src/test/fakeSignalR.ts`), as before.
    - The browser's download: `URL.createObjectURL`, `URL.revokeObjectURL` and a link's `click()`, with the new `src/test/fakeDownload.ts` (Task 3).
  - No fake timers in this plan. Where a test needs today's date, it computes it with `thaiToday()`, which has its own unit test with fixed moments.
  - No MSW, no `vi.mock`, and no module mocks of our own code (regression-net #32).
  - Find elements by role, label or text the way a person would, never by CSS class.
  - The made-up Admin session is account 7, `ผู้ดูแลทดสอบ` (`adminSession` from `src/test/adminFixtures.ts`).
  - Every Admin page has the menu, and the menu has its own "ส่งออก Excel" link from Task 3 on. So a page test that looks for a page's own link scopes it with `within(screen.getByRole('main'))`.
- **Copy:** Thai UI text comes from the wireframes. Where this plan gives a string, use it exactly, because tests match it.
- **Public repo:** made-up names only (`ผู้ดูแลทดสอบ`, `ผู้ดูแลคนที่สอง`, `หัวหน้าทดสอบ`, `หัวหน้ากะดึก`, `หัวหน้าตึกบี`, `แม่บ้านทดสอบ`, `แม่บ้านที่อื่น`), employee IDs like `660001`. No secret in any file.
- **MySQL** is used only in Task 6's controller run.
  - Use only throwaway databases `facility_*_test`. Never touch `facility_dashboard`.
  - If the MySQL on the machine has a root password, ask the owner for it each time. Type it only into the command, never into a file or into memory.
  - Subagents skip that step.
- **Handoff after every task (owner's rule):**
  - The last step of every task adds one entry to a new section `## Task log (plan 12)` in `docs/handoff.md`. Task 1 creates the section just below the `## Task log (plan 11)` section, above `## Sharing a test build`. The entry is committed with the task.
  - The entry is in English: the date, the task, what changed, the test result, the bundle size where measured, and what comes next. No secret, phone number or real name.
- **Commits** end with the attribution trailer your session gives you. The husky pre-commit runs frontend lint, tsc and tests on every commit. If it fails, fix the cause. Never use `--no-verify`.
- **Baselines at `ca59320`:**
  - frontend: 320 tests pass
  - backend: 299 unit tests pass; API tests 531 pass and 5 are skipped (the MySQL ones)
  - main JS: 156.99 kB gzip (plan 11's real run)

### Decisions made while planning (report them to the owner in the PR)

1. **Routes:** D7 is `/admin/inspection-summary`, D8 is `/admin/export` and D9 is `/admin/history`.
   - All three are lazy routes under the Admin guard (facility-0062).
   - D7 takes `?from=yyyy-MM-dd&to=yyyy-MM-dd&building=<id>`.
   - D8 takes the same three to fill its form.
   - D9 takes `?from&to&actor=<id>&category=<AuditCategory>`.
2. **The menu** follows the wireframes' order and adds "ส่งออก Excel", which the wireframes reach only from page links. It reads: สถานะจุด, การเข้างาน, รายการที่ขึ้นเตือน, สรุปการตรวจ, ส่งออก Excel, จัดการ Area และป้าย, บัญชีผู้ใช้, ประวัติ Admin.
3. **Date ranges:** one way on all three pages.
   - Two fields, "ตั้งแต่" and "ถึง", typed as dd/mm/yyyy (like D2, plan 11 decision 13), and D8's shortcuts วันนี้ · 7 วัน · เดือนนี้. The shortcuts fill both fields with Thai dates.
   - Before asking, the page checks the format, the order and the length: 31 dates for D7 and D8, 92 for D9, the server's limits. The server's message is shown if it still refuses.
   - D7 and D9 ask when "แสดง" is pressed (or Enter). The building, actor and type pickers apply at once.
   - With no range in the address, D7 and D9 send none, so the server's "last 7 days" applies. The fields start on the 7 dates ending today. D7's heading shows the range the server answered with.
4. **D7 shows the server's numbers, and judges nothing** (the wireframe's note: "ระบบไม่ประเมินผลเอง และไม่มีขั้นต่ำ").
   - The wireframe's orange bar under 30 % and its bold GPS count at 5 or more are left out, because they are thresholds.
   - "ไม่ได้ตรวจ n จุด" after the points is a fact, not a threshold, so it stays, in the warning colour.
   - A column "ตรวจทั้งหมด" (`inspections`) is added: facility-0048 rule 5 lists "จำนวนครั้งที่ตรวจ", which the wireframe lacks.
   - The share is `submittedInspected / submitted`, rounded, drawn with a `<progress>` element (no inline width). It shows "–" when nothing was submitted.
   - "ไม่ได้ตรวจ" is the server's `notInspected` (finished rounds in closed shifts), not submitted minus inspected (plan 6 decision 17).
5. **D8 downloads through the API layer:**
   - The `exportExcel` mutation's `queryFn` posts through `baseQueryWithReauth`, so a 401 refreshes and retries once.
   - It reads a refusal as JSON, and an empty body as null, as `fetchBaseQuery` does, so the 401 still reaches the refresh.
   - It hands the `Blob` to `saveFile` (an object URL and a link with `download`), and keeps only the file name in the cache.
   - The file name comes from `Content-Disposition`. It falls back to `facility_<from>_<to>.xlsx`, the server's own pattern.
   - The wireframe's preview of row counts ("การลงเวลา 28 แถว") is left out, because the API gives none before the download. The page shows the file name and the sheets chosen instead.
   - The default sets are การลงเวลา and การส่งงาน, as in the wireframe.
6. **"ส่งออก Excel" links:**
   - D1 links to `/admin/export`.
   - D2 links to the export of the shift date and building on screen.
   - D7 links to the export of the range and building on screen.
7. **D9 pages with an RTK Query infinite query** (`build.infiniteQuery`, in RTK 2.13).
   - "โหลดรายการเก่ากว่านี้" asks for `beforeId = nextBeforeId`.
   - The server may offer one empty last page when the rows exactly fill a page (parked in the handoff). The page then shows nothing new and the button goes.
   - The search box ("ค้นหาชื่อคน จุด หรือรหัส") searches only the rows already loaded (summary, actor, reason, type), because the API has no search. The page says so.
   - Owner, 2026-10-21: keep this for plan 12. A server-side search on `/api/admin/audit-log` is its own piece of work after plan 12 (Task 6 puts it under Pending work in the handoff).
8. **D9's "ก่อน → หลัง"** is built field by field from the stored snapshots, one line per changed field: "label: before → after".
   - Fields equal on both sides are left out, and so are fields that show as "–" on both sides.
   - Labels and values are Thai where the field is known: events, sources, shifts, roles, Area shift patterns, alert types, sets, true/false.
   - Times go through `formatDayTime` and dates through `formatShiftDate`. Round windows read "เช้า 07:00-09:00".
   - Ids stay numbers ("(รหัสในระบบ)"), because the snapshots keep ids, not names, on purpose (`AreaSnapshot`). The summary column already names the people. Owner, 2026-10-21: keep it so. Looking names up on the page would show today's names, not the names at the time.
   - `ACCOUNT_PHONE` and `ACCOUNT_PASSWORD` read "เบอร์เดิม → เบอร์ใหม่ (ไม่แสดงเบอร์)" and "รหัสผ่านเดิม → รหัสผ่านใหม่ (ไม่แสดงรหัสผ่าน)".
   - An unknown field shows its key, so a future snapshot field is still visible.
   - An action shows a Thai name per action ("แก้เบอร์โทร", "เก็บพิกัดป้าย"), like the wireframe. An unknown action shows as stored.
9. **What the wireframes show that the API does not send is left out:**
   - D8's row counts before the download (decision 5).
   - D9's reason on actions that store none (QR, phone, export). They show "–".
   - D7's "[หัวหน้าตึก B–F อีก 8 คน]" placeholder row.
10. **Read-only:** every read uses `freshOnOpen`. No cache tag, no poll and no hub. The export's audit row shows on D9 the next time D9 opens.

---

## File structure after this plan

```
frontend/src/
├── router.tsx                        + admin/inspection-summary, admin/export, admin/history (lazy)
├── router.test.tsx                   lazy route list, menu
├── shared/
│   ├── utils/thaiTime.ts (+ test)    + thaiToday
│   ├── utils/dateRange.ts (+ test)   DateRange, RangeTexts, shortcuts, checkRange, readRange, readBuildingId,
│   │                                 toRangeParams, exportLink
│   ├── components/RangeFields.tsx    ตั้งแต่ / ถึง + วันนี้ · 7 วัน · เดือนนี้
│   ├── components/BuildingSelect.tsx ตึก: ทุกตึก or one building
│   ├── components/AdminNav.tsx       + สรุปการตรวจ, ส่งออก Excel, ประวัติ Admin
│   └── components/shared.css         + app-range
├── pages/
│   ├── inspection-summary/           InspectionSummaryPage (D7), SummaryTable, summaryApi, summaryRules (+ test),
│   │                                 inspectionSummary.css, index.ts, InspectionSummaryPage.test
│   ├── export/                       ExportPage (D8), exportApi, exportRules (+ test), saveFile, export.css, index.ts,
│   │                                 ExportPage.test
│   ├── history/                      HistoryPage (D9), HistoryControls, HistoryTable, historyApi, historyRules (+ test),
│   │                                 history.css, index.ts, HistoryPage.test
│   ├── dashboard/DashboardPage.tsx   + "ส่งออก Excel" link (+ test)
│   └── attendance/AttendancePage.tsx + "ส่งออก Excel" link for the shift on screen (+ test)
└── test/
    ├── fakeServer.ts (+ test)        + file answers
    ├── fakeDownload.ts               the browser's download, recorded
    └── reportFixtures.ts             inspection summary, audit log pages, actors
```

---

### Task 1: Date range rules

The rules all three pages share: today's Thai date, the shortcuts, the checks made before asking, and reading and writing a range in the address.

**Files:**
- Modify: `docs/handoff.md`
- Modify: `frontend/src/shared/utils/thaiTime.ts`
- Test: `frontend/src/shared/utils/thaiTime.test.ts`
- Create: `frontend/src/shared/utils/dateRange.ts`
- Test: `frontend/src/shared/utils/dateRange.test.ts`

**Interfaces:**
- Consumes: `formatDate`, `formatShiftDate`, `parseShiftDate` from `shared/utils/thaiTime.ts`.
- Produces:
  - `thaiToday(now?: Date): string`, today's Thai date as `"yyyy-MM-dd"`.
  - In `shared/utils/dateRange.ts`:
    - `interface DateRange { from: string; to: string }`
    - `interface RangeTexts { fromText: string; toText: string }`
    - `type RangePreset = 'today' | 'week' | 'month'`
    - `rangePresets: readonly RangePreset[]`
    - `presetLabel: Record<RangePreset, string>`
    - `addDays(date: string, days: number): string`
    - `rangeDays(range: DateRange): number`
    - `lastSevenDays(today: string): DateRange`
    - `presetRange(preset: RangePreset, today: string): DateRange`
    - `rangeTexts(range: DateRange): RangeTexts`
    - `checkRange(texts: RangeTexts, maxDays: number): DateRange | string` (the string is the message)
    - `readRange(params: URLSearchParams): DateRange | undefined`
    - `readBuildingId(params: URLSearchParams): number | undefined`
    - `toRangeParams(range: DateRange | undefined, buildingId: number | undefined): URLSearchParams`
    - `exportLink(range: DateRange | undefined, buildingId: number | undefined): string`

- [ ] **Step 1: Create the branch**

```bash
git checkout master && git pull --ff-only && git checkout -b feat/admin-reports
git log --oneline -1   # expect ca59320 (or a later docs-only commit)
```

- [ ] **Step 2: Bring the handoff up to date**

In `docs/handoff.md`, replace the plan 12 row of the Status table:

```markdown
| 12 | Admin reports: inspection summary (D7), Excel export (D8), Admin history (D9) | not started (the owner split plan 11 on 2026-10-19) |
```

with:

```markdown
| 12 | Admin reports: inspection summary (D7), Excel export (D8), Admin history (D9) | in progress (branch `feat/admin-reports`): `docs/superpowers/plans/2026-10-21-admin-reports.md` |
```

In `## Pending work (ask the owner before starting)`, delete the line:

```markdown
- Frontend plan 12: Admin reports, inspection summary (D7), Excel export (D8), Admin history (D9). Plan 11 was split in two on 2026-10-19.
```

Just above `## Sharing a test build (colleagues on other networks)`, add:

```markdown
## Task log (plan 12)

```

(The heading, then a blank line. Step 8 adds the first entry under it.)

- [ ] **Step 3: Write the failing tests**

In `frontend/src/shared/utils/thaiTime.test.ts`, add `thaiToday` to the import list from `./thaiTime` (keep the list alphabetical: after `parseShiftDate`), and add this test inside the `describe` block:

```ts
  it('thaiToday is the Thai calendar date, which turns over at 17:00 UTC', () => {
    expect(thaiToday(new Date('2026-10-15T16:59:00Z'))).toBe('2026-10-15');
    expect(thaiToday(new Date('2026-10-15T17:00:00Z'))).toBe('2026-10-16');
    expect(thaiToday(new Date('2026-12-31T17:30:00Z'))).toBe('2027-01-01');
  });
```

Create `frontend/src/shared/utils/dateRange.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import {
  addDays,
  checkRange,
  exportLink,
  lastSevenDays,
  presetRange,
  rangeDays,
  rangeTexts,
  readBuildingId,
  readRange,
  toRangeParams,
} from './dateRange';

describe('dateRange', () => {
  it('addDays moves across months and years', () => {
    expect(addDays('2026-10-15', -6)).toBe('2026-10-09');
    expect(addDays('2026-10-01', -1)).toBe('2026-09-30');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
  });

  it('rangeDays counts both ends', () => {
    expect(rangeDays({ from: '2026-10-15', to: '2026-10-15' })).toBe(1);
    expect(rangeDays({ from: '2026-09-01', to: '2026-10-01' })).toBe(31);
  });

  it('the shortcuts: today, the 7 dates ending today, and this month so far', () => {
    expect(presetRange('today', '2026-10-15')).toEqual({ from: '2026-10-15', to: '2026-10-15' });
    expect(presetRange('week', '2026-10-15')).toEqual({ from: '2026-10-09', to: '2026-10-15' });
    expect(presetRange('month', '2026-10-15')).toEqual({ from: '2026-10-01', to: '2026-10-15' });
    expect(lastSevenDays('2026-10-03')).toEqual({ from: '2026-09-27', to: '2026-10-03' });
  });

  it('rangeTexts writes a range the way the fields show it', () => {
    expect(rangeTexts({ from: '2026-10-01', to: '2026-10-07' })).toEqual({ fromText: '01/10/2026', toText: '07/10/2026' });
  });

  it('checkRange gives the range, or the message to show before asking', () => {
    expect(checkRange({ fromText: '01/10/2026', toText: ' 07/10/2026 ' }, 31)).toEqual({ from: '2026-10-01', to: '2026-10-07' });
    expect(checkRange({ fromText: '1/10/2026', toText: '07/10/2026' }, 31)).toBe('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
    expect(checkRange({ fromText: '01/10/2026', toText: '31/02/2026' }, 31)).toBe('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
    expect(checkRange({ fromText: '08/10/2026', toText: '07/10/2026' }, 31)).toBe('วันสุดท้ายต้องไม่ก่อนวันเริ่ม');
    expect(checkRange({ fromText: '01/09/2026', toText: '01/10/2026' }, 31)).toEqual({ from: '2026-09-01', to: '2026-10-01' });
    expect(checkRange({ fromText: '01/09/2026', toText: '02/10/2026' }, 31)).toBe('เลือกได้ไม่เกิน 31 วัน');
  });

  it('readRange takes both dates from the address or neither, because the server refuses one alone', () => {
    expect(readRange(new URLSearchParams('from=2026-10-01&to=2026-10-07'))).toEqual({ from: '2026-10-01', to: '2026-10-07' });
    expect(readRange(new URLSearchParams('from=2026-10-01'))).toBeUndefined();
    expect(readRange(new URLSearchParams('from=01/10/2026&to=2026-10-07'))).toBeUndefined();
    expect(readRange(new URLSearchParams(''))).toBeUndefined();
  });

  it('readBuildingId takes a positive whole number only', () => {
    expect(readBuildingId(new URLSearchParams('building=2'))).toBe(2);
    expect(readBuildingId(new URLSearchParams('building=0'))).toBeUndefined();
    expect(readBuildingId(new URLSearchParams('building=x'))).toBeUndefined();
    expect(readBuildingId(new URLSearchParams(''))).toBeUndefined();
  });

  it('toRangeParams and exportLink leave out what is not chosen', () => {
    expect(toRangeParams({ from: '2026-10-01', to: '2026-10-07' }, 2).toString()).toBe('from=2026-10-01&to=2026-10-07&building=2');
    expect(toRangeParams(undefined, undefined).toString()).toBe('');
    expect(exportLink({ from: '2026-10-15', to: '2026-10-15' }, undefined)).toBe('/admin/export?from=2026-10-15&to=2026-10-15');
    expect(exportLink(undefined, 1)).toBe('/admin/export?building=1');
    expect(exportLink(undefined, undefined)).toBe('/admin/export');
  });
});
```

- [ ] **Step 4: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/shared/utils/thaiTime.test.ts src/shared/utils/dateRange.test.ts`
Expected: FAIL. `thaiToday` is not exported, and `./dateRange` does not exist.

- [ ] **Step 5: Write the implementation**

In `frontend/src/shared/utils/thaiTime.ts`, below `formatShiftDate`, add:

```ts
/** Today's Thai calendar date as the server writes a shift date, "2026-10-15" (to fill a date range). */
export function thaiToday(now: Date = new Date()): string {
  const [day, month, year] = formatDate(now).split('/');
  return `${year}-${month}-${day}`;
}
```

Create `frontend/src/shared/utils/dateRange.ts`:

```ts
import { formatShiftDate, parseShiftDate } from './thaiTime';

/**
 * The date ranges of the Admin's report pages (D7, D8, D9). Dates are Thai shift dates "yyyy-MM-dd", both ends
 * included, as the report endpoints take them (from, to).
 */
export interface DateRange {
  from: string;
  to: string;
}

/** What the two typed fields hold, dd/mm/yyyy (plan 11 decision 13). */
export interface RangeTexts {
  fromText: string;
  toText: string;
}

/** Wireframe D8's shortcuts. */
export type RangePreset = 'today' | 'week' | 'month';

export const rangePresets: readonly RangePreset[] = ['today', 'week', 'month'];

export const presetLabel: Record<RangePreset, string> = { today: 'วันนี้', week: '7 วัน', month: 'เดือนนี้' };

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const DAY_MS = 86_400_000;

/** A "yyyy-MM-dd" date moved by whole days. */
export function addDays(date: string, days: number): string {
  return new Date(Date.parse(`${date}T00:00:00Z`) + days * DAY_MS).toISOString().slice(0, 10);
}

/** How many dates a range covers, both ends included. */
export function rangeDays({ from, to }: DateRange): number {
  return Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / DAY_MS) + 1;
}

/** The server's default range on D7 and D9: the 7 dates ending today. */
export function lastSevenDays(today: string): DateRange {
  return { from: addDays(today, -6), to: today };
}

export function presetRange(preset: RangePreset, today: string): DateRange {
  if (preset === 'today') {
    return { from: today, to: today };
  }
  if (preset === 'week') {
    return lastSevenDays(today);
  }
  return { from: `${today.slice(0, 8)}01`, to: today };
}

export function rangeTexts({ from, to }: DateRange): RangeTexts {
  return { fromText: formatShiftDate(from), toText: formatShiftDate(to) };
}

/**
 * The typed range, checked before asking (facility-0022 item 4), or the message to show. The server makes the same
 * checks (31 dates for the summary and the export, 92 for the history), and its message is shown if it still refuses.
 */
export function checkRange({ fromText, toText }: RangeTexts, maxDays: number): DateRange | string {
  const from = parseShiftDate(fromText);
  const to = parseShiftDate(toText);
  if (!from || !to) {
    return 'วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026';
  }
  if (to < from) {
    return 'วันสุดท้ายต้องไม่ก่อนวันเริ่ม';
  }
  return rangeDays({ from, to }) > maxDays ? `เลือกได้ไม่เกิน ${maxDays} วัน` : { from, to };
}

/** ?from=yyyy-MM-dd&to=yyyy-MM-dd: both or neither, because the server refuses one alone. */
export function readRange(params: URLSearchParams): DateRange | undefined {
  const from = params.get('from');
  const to = params.get('to');
  return from && to && ISO_DATE.test(from) && ISO_DATE.test(to) ? { from, to } : undefined;
}

/** ?building=<id>. */
export function readBuildingId(params: URLSearchParams): number | undefined {
  const building = Number(params.get('building'));
  return Number.isInteger(building) && building > 0 ? building : undefined;
}

/** The address for a range and a building, in the words readRange and readBuildingId read. */
export function toRangeParams(range: DateRange | undefined, buildingId: number | undefined): URLSearchParams {
  const params = new URLSearchParams();
  if (range) {
    params.set('from', range.from);
    params.set('to', range.to);
  }
  if (buildingId) {
    params.set('building', String(buildingId));
  }
  return params;
}

/** The export page (D8) for this range and building: the "ส่งออก Excel" links of D2 and D7 (plan 12 decision 6). */
export function exportLink(range: DateRange | undefined, buildingId: number | undefined): string {
  const query = toRangeParams(range, buildingId).toString();
  return query ? `/admin/export?${query}` : '/admin/export';
}
```

- [ ] **Step 6: Run the tests to make sure they pass**

Run: `cd frontend && npx vitest run src/shared/utils/thaiTime.test.ts src/shared/utils/dateRange.test.ts`
Expected: PASS.

- [ ] **Step 7: Run the whole frontend check**

Run: `cd frontend && npm run lint && npx tsc -b && npm test`
Expected: lint and tsc clean; 329 tests pass (320 + 1 + 8).

- [ ] **Step 8: Handoff entry**

Under `## Task log (plan 12)` in `docs/handoff.md`, add (with today's date):

```markdown
- <yyyy-MM-dd> · Task 1 done: branch `feat/admin-reports` from `ca59320`; `thaiToday` and `shared/utils/dateRange.ts` (shortcuts วันนี้ / 7 วัน / เดือนนี้, `checkRange` with the server's limits, `readRange`, `readBuildingId`, `toRangeParams`, `exportLink`). Frontend tests <n> passed. Next: Task 2 (inspection summary D7).
```

- [ ] **Step 9: Commit**

```bash
git add docs/superpowers/plans/2026-10-21-admin-reports.md docs/handoff.md \
  frontend/src/shared/utils/thaiTime.ts frontend/src/shared/utils/thaiTime.test.ts \
  frontend/src/shared/utils/dateRange.ts frontend/src/shared/utils/dateRange.test.ts
git commit -m "feat(frontend): date range rules for the Admin reports"
```

---

### Task 2: Inspection summary (D7)

Each active Supervisor's inspections over a range, as the server counts them (facility-0048 rule 5).

**Files:**
- Create: `frontend/src/shared/components/RangeFields.tsx`
- Create: `frontend/src/shared/components/BuildingSelect.tsx`
- Modify: `frontend/src/shared/components/shared.css`
- Modify: `frontend/src/shared/components/AdminNav.tsx`
- Modify: `frontend/src/router.tsx`
- Modify: `frontend/src/router.test.tsx`
- Create: `frontend/src/pages/inspection-summary/summaryApi.ts`
- Create: `frontend/src/pages/inspection-summary/summaryRules.ts`
- Test: `frontend/src/pages/inspection-summary/summaryRules.test.ts`
- Create: `frontend/src/pages/inspection-summary/SummaryTable.tsx`
- Create: `frontend/src/pages/inspection-summary/InspectionSummaryPage.tsx`
- Create: `frontend/src/pages/inspection-summary/inspectionSummary.css`
- Create: `frontend/src/pages/inspection-summary/index.ts`
- Test: `frontend/src/pages/inspection-summary/InspectionSummaryPage.test.tsx`
- Create: `frontend/src/test/reportFixtures.ts`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: Task 1's `dateRange.ts` and `thaiToday`; `useGetBuildingsQuery` from `shared/api/adminSetupApi.ts`; `freshOnOpen` from `shared/api/baseApi.ts`; `shiftShortLabel` from `shared/utils/labels.ts`.
- Produces:
  - `RangeFields({ value: RangeTexts; onChange: (next: RangeTexts) => void; onEnter?: () => void })`. Inputs are labelled "ตั้งแต่" and "ถึง", and the shortcut buttons "วันนี้", "7 วัน" and "เดือนนี้" sit in a group labelled "ปุ่มลัดช่วงวัน".
  - `BuildingSelect({ value?: number; onChange: (buildingId: number | undefined) => void })`: a select labelled "ตึก" with "ทุกตึก" and each building's code.
  - `SummaryRow`, `InspectionSummary`, `SummaryArgs`, `useGetInspectionSummaryQuery` in `pages/inspection-summary/summaryApi.ts`.
  - `src/test/reportFixtures.ts` with `summaryRow()` and `summary`. Task 4 adds the audit log fixtures.
  - The route `admin/inspection-summary` and the menu link "สรุปการตรวจ".

- [ ] **Step 1: Write the made-up summary**

Create `frontend/src/test/reportFixtures.ts`:

```ts
import type { InspectionSummary, SummaryRow } from '../pages/inspection-summary/summaryApi';

/**
 * Made-up answers for the Admin report pages (public repo: no real names). The API sends UTC; the Thai time is in the
 * comments: 03:42Z is 10:42. Fields the server sends as null are left out, as it does.
 */

/** A Day Supervisor of building A who inspected every point of the beat at least once. */
export function summaryRow(overrides: Partial<SummaryRow> = {}): SummaryRow {
  return {
    supervisorId: 31,
    employeeId: '660001',
    displayName: 'หัวหน้าทดสอบ',
    buildingCode: 'A',
    shift: 'Day',
    pointsInspected: 12,
    pointsTotal: 12,
    submitted: 168,
    submittedInspected: 96,
    notInspected: 72,
    inspections: 101,
    reworks: 11,
    gpsFlagged: 3,
    ...overrides,
  };
}

/** The server's default range (the 7 dates to 15/10/2026), in its order: building, shift, employee ID. */
export const summary: InspectionSummary = {
  from: '2026-10-09',
  to: '2026-10-15',
  rows: [
    summaryRow(),
    summaryRow({
      supervisorId: 32,
      employeeId: '660002',
      displayName: 'หัวหน้ากะดึก',
      shift: 'Night',
      pointsInspected: 9,
      submitted: 150,
      submittedInspected: 40,
      notInspected: 110,
      inspections: 41,
      reworks: 2,
      gpsFlagged: 1,
    }),
    summaryRow({
      supervisorId: 33,
      employeeId: '660003',
      displayName: 'หัวหน้าตึกบี',
      buildingCode: 'B',
      pointsInspected: 0,
      pointsTotal: 10,
      submitted: 0,
      submittedInspected: 0,
      notInspected: 0,
      inspections: 0,
      reworks: 0,
      gpsFlagged: 0,
    }),
  ],
};
```

- [ ] **Step 2: Write the API and the rules**

Create `frontend/src/pages/inspection-summary/summaryApi.ts`:

```ts
import { baseApi } from '../../shared/api/baseApi';
import type { Shift } from '../../shared/api/types';

/**
 * InspectionSummaryRowDto (facility-0048 rule 5, plan 6 decision 15). notInspected counts finished rounds left
 * ไม่ได้ตรวจ in closed shifts; it is not submitted minus submittedInspected (plan 6 decision 17).
 */
export interface SummaryRow {
  supervisorId: number;
  employeeId: string;
  displayName: string;
  buildingCode: string;
  shift: Shift;
  pointsInspected: number;
  pointsTotal: number;
  submitted: number;
  submittedInspected: number;
  notInspected: number;
  inspections: number;
  reworks: number;
  gpsFlagged: number;
}

/** InspectionSummaryDto: from and to are the Thai shift dates the server counted, both included. */
export interface InspectionSummary {
  from: string;
  to: string;
  rows: SummaryRow[];
}

/** Both dates or neither (then the server's last 7 dates); no buildingId is every building. */
export interface SummaryArgs {
  from?: string;
  to?: string;
  buildingId?: number;
}

const summaryApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getInspectionSummary: build.query<InspectionSummary, SummaryArgs>({
      query: ({ from, to, buildingId }) => ({ url: '/admin/inspection-summary', params: { from, to, buildingId } }),
    }),
  }),
});

export const { useGetInspectionSummaryQuery } = summaryApi;
```

Create `frontend/src/pages/inspection-summary/summaryRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { summaryRow } from '../../test/reportFixtures';
import { inspectedShare, placeText, pointsText } from './summaryRules';

describe('summaryRules (D7)', () => {
  it('pointsText says how many of the beat were inspected, and how many were not', () => {
    expect(pointsText(summaryRow())).toBe('12 จาก 12 จุด');
    expect(pointsText(summaryRow({ pointsInspected: 9 }))).toBe('9 จาก 12 จุด · ไม่ได้ตรวจ 3 จุด');
  });

  it('inspectedShare is the rounded share of submitted work inspected, none when nothing was submitted', () => {
    expect(inspectedShare(summaryRow())).toBe(57);
    expect(inspectedShare(summaryRow({ submitted: 150, submittedInspected: 40 }))).toBe(27);
    expect(inspectedShare(summaryRow({ submitted: 0, submittedInspected: 0 }))).toBeNull();
  });

  it('placeText is the building and the shift', () => {
    expect(placeText(summaryRow())).toBe('A · กะเช้า');
    expect(placeText(summaryRow({ buildingCode: 'B', shift: 'Night' }))).toBe('B · กะดึก');
  });
});
```

Create `frontend/src/pages/inspection-summary/summaryRules.ts`:

```ts
import { shiftShortLabel } from '../../shared/utils/labels';
import type { SummaryRow } from './summaryApi';

/** ExportRules.MaxDays, which the summary endpoint uses too. */
export const SUMMARY_MAX_DAYS = 31;

/** "จุดที่ตรวจแล้ว": points of the beat inspected at least once in the range, and how many were not. */
export function pointsText({ pointsInspected, pointsTotal }: SummaryRow): string {
  const missed = pointsTotal - pointsInspected;
  return `${pointsInspected} จาก ${pointsTotal} จุด${missed > 0 ? ` · ไม่ได้ตรวจ ${missed} จุด` : ''}`;
}

/** "สัดส่วนที่ตรวจ" in whole percent; null when nothing was submitted (plan 12 decision 4). */
export function inspectedShare({ submitted, submittedInspected }: SummaryRow): number | null {
  return submitted === 0 ? null : Math.round((submittedInspected / submitted) * 100);
}

export function placeText({ buildingCode, shift }: SummaryRow): string {
  return `${buildingCode} · ${shiftShortLabel[shift]}`;
}
```

Run: `cd frontend && npx vitest run src/pages/inspection-summary/summaryRules.test.ts`
Expected: PASS (3 tests).

- [ ] **Step 3: Write the failing page tests**

Create `frontend/src/pages/inspection-summary/InspectionSummaryPage.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { addDays } from '../../shared/utils/dateRange';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { summary } from '../../test/reportFixtures';
import { renderApp } from '../../test/renderApp';

const query = (search: string) => Object.fromEntries(new URLSearchParams(search));

/** The server answers the range it was asked for, or its default. */
function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/inspection-summary': (request) => {
      const asked = query(request.search);
      return { status: 200, body: { ...summary, from: asked.from ?? summary.from, to: asked.to ?? summary.to } };
    },
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    ...routes,
  });
}

const summaryGets = (server: ReturnType<typeof serve>) =>
  server.calls.filter((call) => call.path === '/api/admin/inspection-summary');

/** The cells after the Supervisor's name. */
function cellsOf(name: string): (string | null)[] {
  const row = screen
    .getAllByRole('row')
    .find((candidate) => within(candidate).queryByRole('rowheader')?.textContent?.startsWith(name));
  if (!row) {
    throw new Error(`no row for ${name}`);
  }
  return within(row).getAllByRole('cell').map((cell) => cell.textContent);
}

const fromField = () => screen.getByRole('textbox', { name: 'ตั้งแต่' });
const toField = () => screen.getByRole('textbox', { name: 'ถึง' });

describe('InspectionSummaryPage (D7, facility-0048 rule 5)', () => {
  it('shows each Supervisor of the last 7 days as the server counts them, judging nothing', async () => {
    const server = serve();
    await renderApp('/admin/inspection-summary');

    expect(await screen.findByText('Admin เท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)')).toBeInTheDocument();
    expect(query(summaryGets(server)[0].search)).toEqual({});
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual([
      'หัวหน้าทดสอบ660001',
      'หัวหน้ากะดึก660002',
      'หัวหน้าตึกบี660003',
    ]);
    expect(cellsOf('หัวหน้าทดสอบ')).toEqual(['A · กะเช้า', '12 จาก 12 จุด', '101', '168', '96', '57%', '72', '11', '3']);
    expect(cellsOf('หัวหน้ากะดึก')).toEqual([
      'A · กะดึก',
      '9 จาก 12 จุด · ไม่ได้ตรวจ 3 จุด',
      '41',
      '150',
      '40',
      '27%',
      '110',
      '2',
      '1',
    ]);
    expect(cellsOf('หัวหน้าตึกบี')).toEqual(['B · กะเช้า', '0 จาก 10 จุด · ไม่ได้ตรวจ 10 จุด', '0', '0', '0', '–', '0', '0', '0']);
    expect(screen.getByRole('progressbar', { name: 'ตรวจ 57% ของงานที่ส่ง' })).toHaveAttribute('value', '57');
    // facility-0059: no link to a sign page
    expect(within(screen.getByRole('table')).queryAllByRole('link')).toHaveLength(0);
  });

  it('the fields start on the 7 dates ending today, and the shortcuts fill both', async () => {
    serve();
    const { user } = await renderApp('/admin/inspection-summary');
    const today = thaiToday();

    expect(fromField()).toHaveValue(formatShiftDate(addDays(today, -6)));
    expect(toField()).toHaveValue(formatShiftDate(today));

    await user.click(screen.getByRole('button', { name: 'เดือนนี้' }));
    expect(fromField()).toHaveValue(formatShiftDate(`${today.slice(0, 8)}01`));
    expect(toField()).toHaveValue(formatShiftDate(today));

    await user.click(screen.getByRole('button', { name: 'วันนี้' }));
    expect(fromField()).toHaveValue(formatShiftDate(today));
  });

  it('shows a typed range; the address keeps it with the building', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/inspection-summary');
    await screen.findByText('Admin เท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)');

    await user.clear(fromField());
    await user.type(fromField(), '01/10/2026');
    await user.clear(toField());
    await user.type(toField(), '07/10/2026{Enter}');

    expect(await screen.findByText('Admin เท่านั้น · ช่วงที่เลือก (01/10/2026–07/10/2026)')).toBeInTheDocument();
    await screen.findByRole('option', { name: 'B' });
    await user.selectOptions(screen.getByRole('combobox', { name: 'ตึก' }), 'B');

    await waitFor(() => expect(router.state.location.search).toBe('?from=2026-10-01&to=2026-10-07&building=2'));
    await waitFor(() =>
      expect(query(summaryGets(server).at(-1)!.search)).toEqual({ from: '2026-10-01', to: '2026-10-07', buildingId: '2' }),
    );
  });

  it('a wrong, backwards or too long range is refused before asking', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/inspection-summary');
    await screen.findByText('Admin เท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)');
    const show = screen.getByRole('button', { name: 'แสดง' });

    await user.clear(fromField());
    await user.type(fromField(), '1/10/2026');
    await user.click(show);
    expect(screen.getByRole('alert')).toHaveTextContent('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');

    await user.clear(fromField());
    await user.type(fromField(), '08/10/2026');
    await user.clear(toField());
    await user.type(toField(), '01/10/2026');
    await user.click(show);
    expect(screen.getByRole('alert')).toHaveTextContent('วันสุดท้ายต้องไม่ก่อนวันเริ่ม');

    await user.clear(fromField());
    await user.type(fromField(), '01/09/2026');
    await user.clear(toField());
    await user.type(toField(), '02/10/2026');
    await user.click(show);
    expect(screen.getByRole('alert')).toHaveTextContent('เลือกได้ไม่เกิน 31 วัน');
    expect(summaryGets(server)).toHaveLength(1);
  });

  it('opens the range and building in the address, and the fields show them', async () => {
    const server = serve();
    await renderApp('/admin/inspection-summary?from=2026-10-01&to=2026-10-07&building=1');

    expect(await screen.findByText('Admin เท่านั้น · ช่วงที่เลือก (01/10/2026–07/10/2026)')).toBeInTheDocument();
    expect(query(summaryGets(server)[0].search)).toEqual({ from: '2026-10-01', to: '2026-10-07', buildingId: '1' });
    expect(fromField()).toHaveValue('01/10/2026');
    expect(toField()).toHaveValue('07/10/2026');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ตึก' })).toHaveDisplayValue('A'));
  });

  it("shows the server's refusal", async () => {
    serve({ 'GET /api/admin/inspection-summary': () => ({ status: 400, body: { message: 'เลือกได้ไม่เกิน 31 วัน' } }) });
    await renderApp('/admin/inspection-summary?from=2026-01-01&to=2026-10-07');
    expect(await screen.findByRole('alert')).toHaveTextContent('เลือกได้ไม่เกิน 31 วัน');
  });

  it('says so when no Supervisor is active in the building chosen', async () => {
    serve({ 'GET /api/admin/inspection-summary': () => ({ status: 200, body: { ...summary, rows: [] } }) });
    await renderApp('/admin/inspection-summary?building=2');
    expect(await screen.findByText('ไม่มีหัวหน้าที่เปิดใช้งานในตึกที่เลือก')).toBeInTheDocument();
  });

  it('is in the Admin menu as สรุปการตรวจ', async () => {
    serve();
    await renderApp('/admin/inspection-summary');
    const menu = screen.getByRole('navigation', { name: 'เมนู Admin' });
    expect(within(menu).getByRole('link', { name: 'สรุปการตรวจ' })).toHaveAttribute('aria-current', 'page');
  });
});
```

In `frontend/src/router.test.tsx`:
- In the test `'an Admin page shows the Admin menu with the open page marked'`, replace the expected list with:

```tsx
      'สถานะจุด',
      'การเข้างาน',
      'รายการที่ขึ้นเตือน',
      'สรุปการตรวจ',
      'จัดการ Area และป้าย',
      'บัญชีผู้ใช้',
```

- In the test `'every Admin page is a lazy route, …'`, add `'admin/inspection-summary',` right after `'admin/alerts',`.

- [ ] **Step 4: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/inspection-summary src/router.test.tsx`
Expected: FAIL. The page shows "ไม่พบหน้านี้", the menu has no "สรุปการตรวจ", and the lazy route list lacks the path.

- [ ] **Step 5: Write the shared fields**

Create `frontend/src/shared/components/RangeFields.tsx`:

```tsx
import type { KeyboardEvent } from 'react';
import { presetLabel, presetRange, rangePresets, rangeTexts, type RangeTexts } from '../utils/dateRange';
import { thaiToday } from '../utils/thaiTime';
import './shared.css';

interface RangeFieldsProps {
  value: RangeTexts;
  onChange: (next: RangeTexts) => void;
  /** Enter in either field: D7 and D9 show the range at once. */
  onEnter?: () => void;
}

/**
 * "ตั้งแต่" and "ถึง" typed as dd/mm/yyyy like D2's date (plan 11 decision 13), and wireframe D8's shortcuts
 * วันนี้ · 7 วัน · เดือนนี้, which fill both fields with Thai dates. The page checks the texts (checkRange) before asking.
 */
export function RangeFields({ value, onChange, onEnter }: RangeFieldsProps) {
  const enter = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      onEnter?.();
    }
  };
  return (
    <div className="app-range">
      <label className="app-field">
        ตั้งแต่
        <input
          className="app-input app-range__date"
          value={value.fromText}
          placeholder="วว/ดด/ปปปป"
          inputMode="numeric"
          onChange={(event) => onChange({ ...value, fromText: event.target.value })}
          onKeyDown={enter}
        />
      </label>
      <label className="app-field">
        ถึง
        <input
          className="app-input app-range__date"
          value={value.toText}
          placeholder="วว/ดด/ปปปป"
          inputMode="numeric"
          onChange={(event) => onChange({ ...value, toText: event.target.value })}
          onKeyDown={enter}
        />
      </label>
      <div className="app-range__presets" role="group" aria-label="ปุ่มลัดช่วงวัน">
        {rangePresets.map((preset) => (
          <button
            key={preset}
            type="button"
            className="app-button"
            onClick={() => onChange(rangeTexts(presetRange(preset, thaiToday())))}
          >
            {presetLabel[preset]}
          </button>
        ))}
      </div>
    </div>
  );
}
```

Create `frontend/src/shared/components/BuildingSelect.tsx`:

```tsx
import { useGetBuildingsQuery } from '../api/adminSetupApi';
import './shared.css';

interface BuildingSelectProps {
  value?: number;
  onChange: (buildingId: number | undefined) => void;
}

/** "ตึก" on the report pages (D7, D8): every building, or one by its code (BuildingDto). */
export function BuildingSelect({ value, onChange }: BuildingSelectProps) {
  const { data: buildings } = useGetBuildingsQuery();
  return (
    <label className="app-field">
      ตึก
      <select
        className="app-input"
        value={value ?? ''}
        onChange={(event) => onChange(event.target.value === '' ? undefined : Number(event.target.value))}
      >
        <option value="">ทุกตึก</option>
        {(buildings ?? []).map((building) => (
          <option key={building.id} value={building.id}>
            {building.code}
          </option>
        ))}
      </select>
    </label>
  );
}
```

In `frontend/src/shared/components/shared.css`, add just above the `@media print` block:

```css
/* RangeFields: two typed dates and the shortcuts (D7, D8, D9) */
.app-range {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 8px;
}

.app-range__date {
  width: 150px;
}

.app-range__presets {
  display: inline-flex;
  gap: 4px;
}
```

- [ ] **Step 6: Write the page**

Create `frontend/src/pages/inspection-summary/SummaryTable.tsx`:

```tsx
import type { SummaryRow } from './summaryApi';
import { inspectedShare, placeText, pointsText } from './summaryRules';

/** Wireframe D7's table, in the server's order, plus "ตรวจทั้งหมด" (facility-0048 rule 5). Names are React text only. */
export function SummaryTable({ rows }: { rows: SummaryRow[] }) {
  if (rows.length === 0) {
    return <p className="app-note app-note--info">ไม่มีหัวหน้าที่เปิดใช้งานในตึกที่เลือก</p>;
  }
  return (
    <div className="app-table-scroll">
      <table className="app-table summary-table" aria-label="การตรวจของหัวหน้าแต่ละคน">
        <thead>
          <tr>
            <th scope="col">หัวหน้า</th>
            <th scope="col">ตึก · กะ</th>
            <th scope="col">จุดที่ตรวจแล้ว</th>
            <th scope="col">ตรวจทั้งหมด</th>
            <th scope="col">งานที่แม่บ้านส่ง</th>
            <th scope="col">ตรวจแล้ว</th>
            <th scope="col">สัดส่วนที่ตรวจ</th>
            <th scope="col">ไม่ได้ตรวจ</th>
            <th scope="col">ให้แก้ไข</th>
            <th scope="col">ตรวจที่ติดธง GPS</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.supervisorId}>
              <th scope="row">
                {row.displayName}
                <span className="summary-sub">{row.employeeId}</span>
              </th>
              <td>{placeText(row)}</td>
              <td className={row.pointsInspected < row.pointsTotal ? 'summary-missed' : undefined}>{pointsText(row)}</td>
              <td className="summary-number">{row.inspections}</td>
              <td className="summary-number">{row.submitted}</td>
              <td className="summary-number">{row.submittedInspected}</td>
              <td>
                <Share row={row} />
              </td>
              <td className="summary-number">{row.notInspected}</td>
              <td className="summary-number">{row.reworks}</td>
              <td className="summary-number">{row.gpsFlagged}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** A <progress> bar draws the share without an inline width (facility-0063). */
function Share({ row }: { row: SummaryRow }) {
  const share = inspectedShare(row);
  if (share == null) {
    return <>–</>;
  }
  return (
    <span className="summary-share">
      <progress max={100} value={share} aria-label={`ตรวจ ${share}% ของงานที่ส่ง`} />
      {`${share}%`}
    </span>
  );
}
```

Create `frontend/src/pages/inspection-summary/InspectionSummaryPage.tsx`:

```tsx
import { useState } from 'react';
import { useSearchParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import { BuildingSelect } from '../../shared/components/BuildingSelect';
import { RangeFields } from '../../shared/components/RangeFields';
import {
  checkRange,
  lastSevenDays,
  rangeTexts,
  readBuildingId,
  readRange,
  toRangeParams,
  type DateRange,
} from '../../shared/utils/dateRange';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { useGetInspectionSummaryQuery, type InspectionSummary } from './summaryApi';
import { SUMMARY_MAX_DAYS } from './summaryRules';
import { SummaryTable } from './SummaryTable';
import './inspectionSummary.css';

/**
 * facility-0048 rule 5, wireframe D7: each active Supervisor's inspections over a range, as the server counts them.
 * The page judges nothing (plan 12 decision 4). With no range in the address the server's last 7 dates apply; the
 * address keeps a chosen range and building.
 */
export function InspectionSummaryPage() {
  const [params, setParams] = useSearchParams();
  const asked = readRange(params);
  const buildingId = readBuildingId(params);
  const { currentData: summary, error, isFetching } = useGetInspectionSummaryQuery({ ...asked, buildingId }, freshOnOpen);
  const [texts, setTexts] = useState(() => rangeTexts(asked ?? lastSevenDays(thaiToday())));
  const [problem, setProblem] = useState<string | null>(null);
  const go = (range: DateRange | undefined, building: number | undefined) => setParams(toRangeParams(range, building));

  const show = () => {
    const range = checkRange(texts, SUMMARY_MAX_DAYS);
    if (typeof range === 'string') {
      setProblem(range);
      return;
    }
    setProblem(null);
    go(range, buildingId);
  };

  return (
    <main className="summary-page">
      <header>
        <span className="app-muted">{summary ? rangeLine(summary, Boolean(asked)) : 'Admin เท่านั้น'}</span>
        <h1>สรุปการตรวจของหัวหน้า</h1>
      </header>
      <div className="summary-controls">
        <RangeFields value={texts} onChange={setTexts} onEnter={show} />
        <button type="button" className="app-button" onClick={show}>
          แสดง
        </button>
        <BuildingSelect value={buildingId} onChange={(building) => go(asked, building)} />
      </div>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      {isFetching && !summary && <p className="app-muted">กำลังโหลด...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {summary && <SummaryTable rows={summary.rows} />}
      <SummaryNotes />
    </main>
  );
}

/** The range the server counted, which is the one asked for, or its default. */
function rangeLine(summary: InspectionSummary, asked: boolean): string {
  return `Admin เท่านั้น · ${asked ? 'ช่วงที่เลือก' : '7 วันล่าสุด'} (${formatShiftDate(summary.from)}–${formatShiftDate(summary.to)})`;
}

/** Wireframe D7's notes, with "ตรวจทั้งหมด" and plan 6 decision 17 added. */
function SummaryNotes() {
  return (
    <div className="summary-notes">
      <span>
        หน้านี้แสดงข้อมูลให้ Admin เห็น ระบบไม่ประเมินผลเอง และไม่มีขั้นต่ำว่าหัวหน้าต้องตรวจกี่ครั้ง · "จุดที่ตรวจแล้ว"
        นับจุดในตึกและกะของหัวหน้าที่ถูกตรวจอย่างน้อย 1 ครั้งในช่วงที่เลือก
      </span>
      <span>
        "ไม่ได้ตรวจ" คืองานที่แม่บ้านส่งแล้ว แต่รอบจบก่อนหัวหน้ามาตรวจ · "ตรวจที่ติดธง GPS"
        คือตรวจตอนที่มือถืออยู่นอกรัศมีของป้าย · "ตรวจทั้งหมด" นับทุกครั้งที่หัวหน้าคนนั้นตรวจ รวมตอนตรวจแทนตึกอื่น
      </span>
      <span>งานที่แม่บ้านส่งหลังจบกะไม่นับในตัวเลขของหัวหน้า เพราะไม่มีใครตรวจได้แล้ว</span>
      <span>ดูรายละเอียดรายครั้งได้จากไฟล์ Excel</span>
    </div>
  );
}
```

Create `frontend/src/pages/inspection-summary/inspectionSummary.css`:

```css
/* Inspection summary (D7, facility-0048 rule 5). Class prefix: summary- */

.summary-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.summary-page h1 {
  font-size: 26px;
  font-weight: 700;
}

.summary-controls {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 8px;
  font-size: 14px;
}

.summary-sub {
  display: block;
  color: var(--color-muted);
  font-size: 13px;
  font-weight: 400;
}

.summary-table .summary-number {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.summary-missed {
  color: var(--color-warn-text);
  font-weight: 600;
  white-space: nowrap;
}

.summary-share {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-variant-numeric: tabular-nums;
}

.summary-share progress {
  width: 120px;
  height: 8px;
  accent-color: var(--color-primary);
}

.summary-notes {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-width: 80ch;
  color: var(--color-text-soft);
  font-size: 14px;
}
```

Create `frontend/src/pages/inspection-summary/index.ts`:

```ts
export { InspectionSummaryPage } from './InspectionSummaryPage';
```

- [ ] **Step 7: Route and menu**

In `frontend/src/router.tsx`, right after the `admin/alerts` route, add:

```tsx
              // facility-0048 rule 5, wireframe D7: ?from=yyyy-MM-dd&to=yyyy-MM-dd&building=<id>
              {
                path: 'admin/inspection-summary',
                lazy: async () => ({ Component: (await import('./pages/inspection-summary')).InspectionSummaryPage }),
              },
```

In `frontend/src/shared/components/AdminNav.tsx`:
- Replace the doc comment's last sentence `Plan 12 adds สรุปการตรวจ, ส่งออก Excel and ประวัติ Admin with their pages.` with `The order follows the wireframes; plan 12 added ส่งออก Excel, which they reach only from page links.`
- Add this entry right after the `/admin/alerts` one:

```tsx
  { to: '/admin/inspection-summary', label: 'สรุปการตรวจ', counted: false },
```

- [ ] **Step 8: Run the tests to make sure they pass**

Run: `cd frontend && npx vitest run src/pages/inspection-summary src/router.test.tsx`
Expected: PASS.

If `currentData` is missing from the hook's result type, the RTK version is older than this plan assumes. Stop and report to the controller; do not use `data` silently, because `data` keeps the previous range on screen while a new range loads.

- [ ] **Step 9: Whole frontend check and bundle**

Run: `cd frontend && npm run lint && npx tsc -b && npm test`
Expected: clean; 340 tests pass (329 + 3 + 8).

Run: `cd frontend && npm run build && grep -o 'assets/index-[A-Za-z0-9_-]*\.js' dist/index.html && ls dist/assets/*.js && rm -rf dist`
Expected: the main file is about 157 kB gzip in the build report (≤ 200 kB), and the D7 page is its own `InspectionSummaryPage-*.js` file.

- [ ] **Step 10: Handoff entry and commit**

Add under `## Task log (plan 12)`:

```markdown
- <yyyy-MM-dd> · Task 2 done: inspection summary D7 (`/admin/inspection-summary`, lazy, in the menu as สรุปการตรวจ): each active Supervisor with building and shift, points inspected of the beat ("ไม่ได้ตรวจ n จุด"), ตรวจทั้งหมด (facility-0048 rule 5), submitted, inspected, the share as a progress bar ("–" when nothing was submitted), ไม่ได้ตรวจ, ให้แก้ไข, GPS flags; no thresholds; range typed dd/mm/yyyy with the shortcuts, checked before asking (31 dates), building picker, both in the address. Shared `RangeFields` and `BuildingSelect`. Frontend tests <n> passed; main JS <size> kB gzip. Next: Task 3 (Excel export D8).
```

```bash
git add docs/handoff.md frontend/src/router.tsx frontend/src/router.test.tsx \
  frontend/src/shared/components/RangeFields.tsx frontend/src/shared/components/BuildingSelect.tsx \
  frontend/src/shared/components/shared.css frontend/src/shared/components/AdminNav.tsx \
  frontend/src/pages/inspection-summary frontend/src/test/reportFixtures.ts
git commit -m "feat(frontend): inspection summary D7 with a date range and building (facility-0048)"
```

---

### Task 3: Excel export (D8) and its links

The Admin chooses the sets, the range and the building, then downloads one `.xlsx` (facility-0053).

**Files:**
- Modify: `frontend/src/test/fakeServer.ts`
- Test: `frontend/src/test/fakeServer.test.ts`
- Create: `frontend/src/test/fakeDownload.ts`
- Create: `frontend/src/pages/export/exportRules.ts`
- Test: `frontend/src/pages/export/exportRules.test.ts`
- Create: `frontend/src/pages/export/saveFile.ts`
- Create: `frontend/src/pages/export/exportApi.ts`
- Create: `frontend/src/pages/export/ExportPage.tsx`
- Create: `frontend/src/pages/export/export.css`
- Create: `frontend/src/pages/export/index.ts`
- Test: `frontend/src/pages/export/ExportPage.test.tsx`
- Modify: `frontend/src/router.tsx`, `frontend/src/router.test.tsx`, `frontend/src/shared/components/AdminNav.tsx`
- Modify: `frontend/src/pages/dashboard/DashboardPage.tsx`, `frontend/src/pages/dashboard/dashboard.css`
- Test: `frontend/src/pages/dashboard/DashboardPage.test.tsx`
- Modify: `frontend/src/pages/attendance/AttendancePage.tsx`
- Test: `frontend/src/pages/attendance/AttendancePage.test.tsx`
- Modify: `frontend/src/pages/inspection-summary/InspectionSummaryPage.tsx`, `inspectionSummary.css`
- Test: `frontend/src/pages/inspection-summary/InspectionSummaryPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: Task 1's `checkRange`, `lastSevenDays`, `rangeTexts`, `readRange`, `readBuildingId`, `exportLink`; Task 2's `RangeFields` and `BuildingSelect`.
- Produces:
  - `FakeResponse.file?: { content: string; type: string; disposition?: string }`.
  - `fakeDownload(): { saved: SavedFile[] }`, where `SavedFile` is `{ fileName: string; blob: Blob }`.
  - In `pages/export/exportRules.ts`: `ExportSet`, `EXPORT_MAX_DAYS`, `exportSets`, `setTitle`, `setDetail`, `fileNameFor(range)` and `fileNameOf(disposition, range)`.
  - `useExportExcelMutation` (arg `ExportRequest { from; to; buildingId?; sets }`, result `{ fileName: string }`).
  - The route `admin/export`, the menu link "ส่งออก Excel", and "ส่งออก Excel" links on D1, D2 and D7.

- [ ] **Step 1: Teach the fake server a file answer**

In `frontend/src/test/fakeServer.test.ts`, add inside the `describe` block:

```ts
  it('a file answer is sent as is, with its type and its name', async () => {
    fakeServer({
      'POST /api/files': () => ({
        status: 200,
        file: { content: 'file-bytes', type: 'application/octet-stream', disposition: 'attachment; filename=a.bin' },
      }),
    });

    const response = await fetch('/api/files', { method: 'POST' });

    expect(response.headers.get('Content-Type')).toBe('application/octet-stream');
    expect(response.headers.get('Content-Disposition')).toBe('attachment; filename=a.bin');
    expect(await response.text()).toBe('file-bytes');
  });
```

Run: `cd frontend && npx vitest run src/test/fakeServer.test.ts`
Expected: FAIL (a type error on `file`, or a JSON body instead of the bytes).

In `frontend/src/test/fakeServer.ts`, replace:

```ts
export interface FakeResponse {
  status: number;
  body?: unknown;
}
```

with:

```ts
export interface FakeResponse {
  status: number;
  body?: unknown;
  /** A file answer (the export, D8): sent as is with these headers instead of a JSON body. */
  file?: { content: string; type: string; disposition?: string };
}
```

and, inside `fetchMock`, replace:

```ts
    const hasBody = answer.body !== undefined && answer.status !== 204;
```

with:

```ts
    if (answer.file) {
      const headers: Record<string, string> = { 'Content-Type': answer.file.type };
      if (answer.file.disposition) {
        headers['Content-Disposition'] = answer.file.disposition;
      }
      return new Response(answer.file.content, { status: answer.status, headers });
    }
    const hasBody = answer.body !== undefined && answer.status !== 204;
```

Run: `cd frontend && npx vitest run src/test/fakeServer.test.ts`
Expected: PASS.

- [ ] **Step 2: The fake download**

Create `frontend/src/test/fakeDownload.ts`:

```ts
import { vi } from 'vitest';

export interface SavedFile {
  fileName: string;
  blob: Blob;
}

/**
 * jsdom has no object URLs and cannot save a file, so this stands in for the browser's download (a browser seam,
 * regression-net #32): every link the page clicks with a `download` name is recorded with the Blob behind it.
 * The click spy is restored after each test (vi.restoreAllMocks in setup.ts). The two URL functions stay defined,
 * so a revoke that runs after the test ends is harmless.
 */
export function fakeDownload() {
  const saved: SavedFile[] = [];
  const blobs = new Map<string, Blob>();
  let next = 0;
  Object.defineProperty(URL, 'createObjectURL', {
    configurable: true,
    writable: true,
    value: (blob: Blob) => {
      next += 1;
      const url = `blob:fake/${next}`;
      blobs.set(url, blob);
      return url;
    },
  });
  Object.defineProperty(URL, 'revokeObjectURL', {
    configurable: true,
    writable: true,
    value: (url: string) => {
      blobs.delete(url);
    },
  });
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    const blob = blobs.get(this.href);
    if (this.download && blob) {
      saved.push({ fileName: this.download, blob });
    }
  });
  return { saved };
}
```

- [ ] **Step 3: The export rules**

Create `frontend/src/pages/export/exportRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { fileNameFor, fileNameOf } from './exportRules';

const range = { from: '2026-10-01', to: '2026-10-07' };

describe('exportRules (D8)', () => {
  it("fileNameFor is the server's own name for the range", () => {
    expect(fileNameFor(range)).toBe('facility_2026-10-01_2026-10-07.xlsx');
  });

  it("fileNameOf takes ASP.NET's file name, and falls back to the server's pattern", () => {
    expect(
      fileNameOf("attachment; filename=facility_2026-10-01_2026-10-07.xlsx; filename*=UTF-8''facility_2026-10-01_2026-10-07.xlsx", range),
    ).toBe('facility_2026-10-01_2026-10-07.xlsx');
    expect(fileNameOf('attachment; filename="report.xlsx"', range)).toBe('report.xlsx');
    expect(fileNameOf("attachment; filename*=UTF-8''x.xlsx", range)).toBe('facility_2026-10-01_2026-10-07.xlsx');
    expect(fileNameOf(null, range)).toBe('facility_2026-10-01_2026-10-07.xlsx');
  });
});
```

Create `frontend/src/pages/export/exportRules.ts`:

```ts
import type { DateRange } from '../../shared/utils/dateRange';

/** ExportSet on the server (facility-0053). */
export type ExportSet = 'Attendance' | 'Submissions' | 'Inspections';

/** ExportRules.MaxDays. */
export const EXPORT_MAX_DAYS = 31;

/** The order of the sheets in the file and of the choices on the page. */
export const exportSets: readonly ExportSet[] = ['Attendance', 'Submissions', 'Inspections'];

/** Wireframe D8's choices, word for word. */
export const setTitle: Record<ExportSet, string> = {
  Attendance: 'การลงเวลา',
  Submissions: 'การส่งงาน',
  Inspections: 'การตรวจงาน',
};

export const setDetail: Record<ExportSet, string> = {
  Attendance: 'คน · Area · กะ · เวลา 4 ครั้ง · ที่มา (สแกนเอง / แก้โดย Admin) · ธง GPS',
  Submissions: 'จุด · รอบ · เวลา · ผล (ตรงเวลา / ส่งช้า / นอกรอบ) · Issue · ทำแทนหรือไม่ · ธง GPS · รอบที่ไม่ได้ทำ',
  Inspections: 'หัวหน้า · จุด · รอบ · ผล · ข้อบกพร่อง · ธง GPS · รอบที่ไม่ได้ตรวจ / ไม่ได้แก้',
};

/** AdminExportEndpoints names the file facility_<from>_<to>.xlsx. */
export function fileNameFor({ from, to }: DateRange): string {
  return `facility_${from}_${to}.xlsx`;
}

/** The name in "attachment; filename=…; filename*=UTF-8''…", or the server's pattern when there is none. */
export function fileNameOf(disposition: string | null, range: DateRange): string {
  const match = disposition ? /filename="?([^";]+)"?/.exec(disposition) : null;
  return match ? match[1] : fileNameFor(range);
}
```

Run: `cd frontend && npx vitest run src/pages/export/exportRules.test.ts`
Expected: PASS (2 tests).

- [ ] **Step 4: Write the failing page tests**

Create `frontend/src/pages/export/ExportPage.test.tsx`:

```tsx
import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { addDays } from '../../shared/utils/dateRange';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeDownload } from '../../test/fakeDownload';
import { fakeServer, type FakeResponse, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

const XLSX = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/** The server's answer: the bytes, with ASP.NET's Content-Disposition. */
function xlsx(name = 'facility_2026-10-09_2026-10-15.xlsx'): FakeResponse {
  return {
    status: 200,
    file: { content: 'xlsx-bytes', type: XLSX, disposition: `attachment; filename=${name}; filename*=UTF-8''${name}` },
  };
}

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'POST /api/admin/export': () => xlsx(),
    ...routes,
  });
}

const exportPosts = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/admin/export');
const download = () => screen.getByRole('button', { name: 'ดาวน์โหลด .xlsx' });
const fromField = () => screen.getByRole('textbox', { name: 'ตั้งแต่' });
const toField = () => screen.getByRole('textbox', { name: 'ถึง' });

describe('ExportPage (D8, facility-0053)', () => {
  it('starts on the last 7 days with attendance and submissions, and downloads the file the server sends', async () => {
    const { saved } = fakeDownload();
    const server = serve();
    const { user } = await renderApp('/admin/export');
    const today = thaiToday();
    const weekAgo = addDays(today, -6);

    expect(fromField()).toHaveValue(formatShiftDate(weekAgo));
    expect(toField()).toHaveValue(formatShiftDate(today));
    expect(screen.getByRole('checkbox', { name: /^การลงเวลา/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /^การส่งงาน/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /^การตรวจงาน/ })).not.toBeChecked();
    expect(screen.getByText(`facility_${weekAgo}_${today}.xlsx`)).toBeInTheDocument();
    expect(screen.getByText('2 แผ่นงาน · การลงเวลา + การส่งงาน')).toBeInTheDocument();

    await user.click(download());

    expect(await screen.findByText('ดาวน์โหลดแล้ว: facility_2026-10-09_2026-10-15.xlsx')).toBeInTheDocument();
    // decision 1 of plan 5: no buildingId means every building, so it is left out
    expect(exportPosts(server).map((call) => call.body)).toEqual([
      { from: weekAgo, to: today, sets: ['Attendance', 'Submissions'] },
    ]);
    expect(saved).toHaveLength(1);
    expect(saved[0].fileName).toBe('facility_2026-10-09_2026-10-15.xlsx');
    expect(await saved[0].blob.text()).toBe('xlsx-bytes');
  });

  it('sends the typed range, the building and the sets in the file’s order', async () => {
    fakeDownload();
    const server = serve();
    const { user } = await renderApp('/admin/export');

    await user.click(screen.getByRole('checkbox', { name: /^การตรวจงาน/ }));
    await user.click(screen.getByRole('checkbox', { name: /^การลงเวลา/ }));
    await user.click(screen.getByRole('checkbox', { name: /^การลงเวลา/ }));
    await user.clear(fromField());
    await user.type(fromField(), '01/10/2026');
    await user.clear(toField());
    await user.type(toField(), '07/10/2026');
    await screen.findByRole('option', { name: 'B' });
    await user.selectOptions(screen.getByRole('combobox', { name: 'ตึก' }), 'B');
    await user.click(download());

    await screen.findByText(/^ดาวน์โหลดแล้ว/);
    expect(exportPosts(server)[0].body).toEqual({
      from: '2026-10-01',
      to: '2026-10-07',
      buildingId: 2,
      sets: ['Attendance', 'Submissions', 'Inspections'],
    });
  });

  it('refuses before sending when no set is chosen or the range is wrong or too long', async () => {
    const { saved } = fakeDownload();
    const server = serve();
    const { user } = await renderApp('/admin/export');

    await user.click(screen.getByRole('checkbox', { name: /^การลงเวลา/ }));
    await user.click(screen.getByRole('checkbox', { name: /^การส่งงาน/ }));
    await user.click(download());
    expect(screen.getByRole('alert')).toHaveTextContent('ต้องเลือกข้อมูลอย่างน้อย 1 ชุด');

    await user.click(screen.getByRole('checkbox', { name: /^การส่งงาน/ }));
    await user.clear(fromField());
    await user.type(fromField(), '2026-10-01');
    await user.click(download());
    expect(screen.getByRole('alert')).toHaveTextContent('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
    expect(screen.getByText('กรอกช่วงวันให้ถูกต้องก่อน')).toBeInTheDocument();

    await user.clear(fromField());
    await user.type(fromField(), '01/09/2026');
    await user.clear(toField());
    await user.type(toField(), '02/10/2026');
    await user.click(download());
    expect(screen.getByRole('alert')).toHaveTextContent('เลือกได้ไม่เกิน 31 วัน');

    expect(exportPosts(server)).toHaveLength(0);
    expect(saved).toHaveLength(0);
  });

  it("shows the server's refusal, and saves nothing", async () => {
    const { saved } = fakeDownload();
    serve({ 'POST /api/admin/export': () => ({ status: 400, body: { message: 'ไม่พบตึกนี้' } }) });
    const { user } = await renderApp('/admin/export?building=9');

    await user.click(download());

    expect(await screen.findByRole('alert')).toHaveTextContent('ไม่พบตึกนี้');
    expect(saved).toHaveLength(0);
  });

  it('sends once at a time: the button waits while the file is made', async () => {
    const { saved } = fakeDownload();
    let answer: (response: FakeResponse) => void = () => undefined;
    const server = serve({
      'POST /api/admin/export': () =>
        new Promise<FakeResponse>((resolve) => {
          answer = resolve;
        }),
    });
    const { user } = await renderApp('/admin/export');

    await user.click(download());
    const busy = await screen.findByRole('button', { name: 'กำลังสร้างไฟล์...' });
    expect(busy).toBeDisabled();
    await user.click(busy);
    expect(exportPosts(server)).toHaveLength(1);

    await act(async () => answer(xlsx()));

    expect(await screen.findByText(/^ดาวน์โหลดแล้ว/)).toBeInTheDocument();
    expect(saved).toHaveLength(1);
  });

  it('an expired access token is refreshed, and the file is saved once (facility-0023)', async () => {
    const { saved } = fakeDownload();
    let posts = 0;
    const server = serve({
      'POST /api/admin/export': () => {
        posts += 1;
        return posts === 1 ? { status: 401 } : xlsx();
      },
    });
    const { user } = await renderApp('/admin/export');

    await user.click(download());

    expect(await screen.findByText(/^ดาวน์โหลดแล้ว/)).toBeInTheDocument();
    expect(exportPosts(server)).toHaveLength(2);
    expect(server.calls.filter((call) => call.path === '/api/auth/refresh')).toHaveLength(2);
    expect(saved).toHaveLength(1);
  });

  it('opens with the range and building of the link that brought the Admin here', async () => {
    fakeDownload();
    const server = serve();
    const { user } = await renderApp('/admin/export?from=2026-10-15&to=2026-10-15&building=2');

    expect(fromField()).toHaveValue('15/10/2026');
    expect(toField()).toHaveValue('15/10/2026');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ตึก' })).toHaveDisplayValue('B'));
    await user.click(download());

    await screen.findByText(/^ดาวน์โหลดแล้ว/);
    expect(exportPosts(server)[0].body).toEqual({
      from: '2026-10-15',
      to: '2026-10-15',
      buildingId: 2,
      sets: ['Attendance', 'Submissions'],
    });
  });

  it('is in the Admin menu as ส่งออก Excel, and links back to the Dashboard', async () => {
    serve();
    await renderApp('/admin/export');
    const menu = screen.getByRole('navigation', { name: 'เมนู Admin' });
    expect(within(menu).getByRole('link', { name: 'ส่งออก Excel' })).toHaveAttribute('aria-current', 'page');
    expect(within(screen.getByRole('main')).getByRole('link', { name: '‹ กลับไป Dashboard' })).toHaveAttribute(
      'href',
      '/dashboard',
    );
  });
});
```

Add the link tests on the other pages.

In `frontend/src/pages/dashboard/DashboardPage.test.tsx`, inside the `describe` block, add:

```tsx
  it('links to the Excel export (D8)', async () => {
    const server = serve();
    const { store } = await renderApp('/dashboard');
    await settled(server, store);

    expect(within(screen.getByRole('main')).getByRole('link', { name: 'ส่งออก Excel' })).toHaveAttribute('href', '/admin/export');
  });
```

In `frontend/src/pages/attendance/AttendancePage.test.tsx`, inside the `describe` block, add:

```tsx
  it('"ส่งออก Excel" opens the export of the shift date and building on screen', async () => {
    serve();
    await renderApp('/admin/attendance?building=2');
    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');

    expect(within(screen.getByRole('main')).getByRole('link', { name: 'ส่งออก Excel' })).toHaveAttribute(
      'href',
      '/admin/export?from=2026-10-15&to=2026-10-15&building=2',
    );
  });
```

In `frontend/src/pages/inspection-summary/InspectionSummaryPage.test.tsx`, inside the `describe` block, add:

```tsx
  it('"ส่งออก Excel" opens the export of the range and building on screen', async () => {
    serve();
    await renderApp('/admin/inspection-summary?building=1');
    await screen.findByText('Admin เท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)');

    expect(within(screen.getByRole('main')).getByRole('link', { name: 'ส่งออก Excel' })).toHaveAttribute(
      'href',
      '/admin/export?from=2026-10-09&to=2026-10-15&building=1',
    );
  });
```

In `frontend/src/router.test.tsx`:
- Put `'ส่งออก Excel',` after `'สรุปการตรวจ',` in the menu list.
- Put `'admin/export',` after `'admin/inspection-summary',` in the lazy route list.

- [ ] **Step 5: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/export src/pages/dashboard/DashboardPage.test.tsx src/pages/attendance/AttendancePage.test.tsx src/pages/inspection-summary src/router.test.tsx`
Expected: FAIL. There is no export page, no menu link and no page links.

- [ ] **Step 6: Write saveFile and the API**

Create `frontend/src/pages/export/saveFile.ts`:

```ts
/**
 * Hands a file to the browser's own download, as a link with `download` would. The object URL is revoked a moment
 * later, once the browser has taken the file. Nothing is kept on the device by the page (facility-0068); the saved
 * file is the download the Admin asked for.
 */
export function saveFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1_000);
}
```

Create `frontend/src/pages/export/exportApi.ts`:

```ts
import { baseApi } from '../../shared/api/baseApi';
import { fileNameOf, type ExportSet } from './exportRules';
import { saveFile } from './saveFile';

/** ExportRequest: Thai shift dates, both included; buildingId left out for every building. */
export interface ExportRequest {
  from: string;
  to: string;
  buildingId?: number;
  sets: ExportSet[];
}

export interface ExportResult {
  fileName: string;
}

/** A downloaded file on its way from the answer to saveFile; it never reaches the store. */
interface DownloadedFile {
  blob: Blob;
  fileName: string;
}

const exportApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /**
     * facility-0053, plan 12 decision 5. POST because every export is logged (facility-0051). The answer is the .xlsx,
     * handed to the browser here: a Blob is not serializable, so only its name enters the cache. A refusal is JSON
     * { message }; an empty body (the 401 before a refresh) reads as null, as fetchBaseQuery's own handler does, so
     * baseQueryWithReauth still sees the 401, refreshes and asks once more.
     */
    exportExcel: build.mutation<ExportResult, ExportRequest>({
      async queryFn(request, _api, _extraOptions, baseQuery) {
        const answer = await baseQuery({
          url: '/admin/export',
          method: 'POST',
          body: request,
          responseHandler: async (response) => {
            if (!response.ok) {
              const text = await response.text();
              return text ? JSON.parse(text) : null;
            }
            return { blob: await response.blob(), fileName: fileNameOf(response.headers.get('Content-Disposition'), request) };
          },
        });
        if (answer.error) {
          return { error: answer.error };
        }
        const file = answer.data as DownloadedFile;
        saveFile(file.blob, file.fileName);
        return { data: { fileName: file.fileName } };
      },
    }),
  }),
});

export const { useExportExcelMutation } = exportApi;
```

- [ ] **Step 7: Write the page**

Create `frontend/src/pages/export/ExportPage.tsx`:

```tsx
import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { BuildingSelect } from '../../shared/components/BuildingSelect';
import { RangeFields } from '../../shared/components/RangeFields';
import { checkRange, lastSevenDays, rangeTexts, readBuildingId, readRange } from '../../shared/utils/dateRange';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { thaiToday } from '../../shared/utils/thaiTime';
import { useExportExcelMutation } from './exportApi';
import { EXPORT_MAX_DAYS, exportSets, fileNameFor, setDetail, setTitle, type ExportSet } from './exportRules';
import './export.css';

type Notice = { kind: 'ok' | 'error'; text: string } | null;

/**
 * facility-0053, wireframe D8: the Admin chooses the sets, the range (1–31 shift dates) and the building, then
 * downloads one .xlsx. The form starts on the last 7 days, or on the range and building of the link that brought the
 * Admin here (D2, D7). The export is logged by the server (facility-0051).
 */
export function ExportPage() {
  const [params] = useSearchParams();
  const [sets, setSets] = useState<ExportSet[]>(['Attendance', 'Submissions']);
  const [texts, setTexts] = useState(() => rangeTexts(readRange(params) ?? lastSevenDays(thaiToday())));
  const [buildingId, setBuildingId] = useState(() => readBuildingId(params));
  const [notice, setNotice] = useState<Notice>(null);
  const [exportExcel, { isLoading: sending }] = useExportExcelMutation();
  const range = checkRange(texts, EXPORT_MAX_DAYS);

  // The sets keep the file's order, whatever order they are ticked in
  const toggle = (set: ExportSet) =>
    setSets((current) => exportSets.filter((each) => (each === set ? !current.includes(each) : current.includes(each))));

  const download = async () => {
    if (sets.length === 0) {
      setNotice({ kind: 'error', text: 'ต้องเลือกข้อมูลอย่างน้อย 1 ชุด' });
      return;
    }
    if (typeof range === 'string') {
      setNotice({ kind: 'error', text: range });
      return;
    }
    setNotice(null);
    try {
      const { fileName } = await exportExcel({ ...range, buildingId, sets }).unwrap();
      setNotice({ kind: 'ok', text: `ดาวน์โหลดแล้ว: ${fileName}` });
    } catch (failure) {
      setNotice({ kind: 'error', text: getErrorMessage(failure) });
    }
  };

  return (
    <main className="export-page">
      <header>
        <span className="app-muted">Admin เท่านั้น · ช่วงทดลองใช้ไฟล์ Excel แทนหน้ารายงาน</span>
        <h1>ส่งออก Excel</h1>
      </header>
      <div className="export-layout">
        <section className="app-panel export-panel" aria-labelledby="export-choose">
          <h2 id="export-choose">1. เลือกข้อมูล</h2>
          {exportSets.map((set) => (
            <label key={set} className="export-set">
              <input type="checkbox" checked={sets.includes(set)} onChange={() => toggle(set)} />
              <span className="export-set__text">
                <strong>{setTitle[set]}</strong>
                <span className="app-muted">{setDetail[set]}</span>
              </span>
            </label>
          ))}
          <h2>2. ช่วงวันและตึก</h2>
          <div className="export-range">
            <RangeFields value={texts} onChange={setTexts} />
            <BuildingSelect value={buildingId} onChange={setBuildingId} />
          </div>
          <p className="app-muted">เลือกได้ 1–31 วัน นับตามวันที่ของกะ</p>
        </section>
        <section className="app-panel export-panel" aria-labelledby="export-download">
          <h2 id="export-download">3. ดาวน์โหลด</h2>
          {typeof range === 'string' ? (
            <p className="app-muted">กรอกช่วงวันให้ถูกต้องก่อน</p>
          ) : (
            <div className="export-file">
              <strong>{fileNameFor(range)}</strong>
              <span className="app-muted">
                {sets.length === 0
                  ? 'ยังไม่ได้เลือกข้อมูล'
                  : `${sets.length} แผ่นงาน · ${sets.map((set) => setTitle[set]).join(' + ')}`}
              </span>
            </div>
          )}
          <button
            type="button"
            className="app-button app-button--primary"
            disabled={sending}
            onClick={() => void download()}
          >
            {sending ? 'กำลังสร้างไฟล์...' : 'ดาวน์โหลด .xlsx'}
          </button>
          {notice && (
            <p
              className={notice.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
              role={notice.kind === 'ok' ? 'status' : 'alert'}
            >
              {notice.text}
            </p>
          )}
          <div className="export-notes">
            <span>ไม่มีพิกัด GPS ดิบ มีแค่ระยะห่างจากป้าย และผลในรัศมี / นอกรัศมี</span>
            <span>การส่งออกเก็บลงประวัติ Admin (ใคร เมื่อไร ชุดไหน ช่วงวันไหน)</span>
            <span>ไฟล์ที่ดาวน์โหลดแล้วอยู่นอกการลบตามระยะเวลาเก็บข้อมูล ห้ามส่งต่อนอกบริษัท (เรื่องคนเก็บไฟล์รอคุยกับ HR)</span>
          </div>
          <Link to="/dashboard">‹ กลับไป Dashboard</Link>
        </section>
      </div>
    </main>
  );
}
```

Create `frontend/src/pages/export/export.css`:

```css
/* Excel export (D8, facility-0053). Class prefix: export- */

.export-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.export-page h1 {
  font-size: 26px;
  font-weight: 700;
}

.export-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr);
  gap: 20px;
  align-items: start;
}

@media (max-width: 960px) {
  .export-layout {
    grid-template-columns: minmax(0, 1fr);
  }
}

.export-panel {
  gap: 14px;
}

.export-panel h2 {
  font-size: 18px;
}

.export-set {
  display: flex;
  gap: 12px;
  align-items: flex-start;
  padding: 12px 14px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  cursor: pointer;
}

.export-set input {
  width: 20px;
  height: 20px;
  margin: 2px 0 0;
  accent-color: var(--color-primary);
}

.export-set__text {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.export-range {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 12px;
}

.export-file {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 12px 14px;
  border-radius: var(--radius-md);
  background: var(--color-bg);
  font-size: 14px;
  overflow-wrap: anywhere;
}

.export-notes {
  display: flex;
  flex-direction: column;
  gap: 6px;
  color: var(--color-text-soft);
  font-size: 13px;
}
```

Create `frontend/src/pages/export/index.ts`:

```ts
export { ExportPage } from './ExportPage';
```

- [ ] **Step 8: Route, menu and links**

In `frontend/src/router.tsx`, right after the `admin/inspection-summary` route, add:

```tsx
              // facility-0053, wireframe D8: ?from=yyyy-MM-dd&to=yyyy-MM-dd&building=<id> fill the form
              { path: 'admin/export', lazy: async () => ({ Component: (await import('./pages/export')).ExportPage }) },
```

In `frontend/src/shared/components/AdminNav.tsx`, after the `/admin/inspection-summary` entry, add:

```tsx
  { to: '/admin/export', label: 'ส่งออก Excel', counted: false },
```

In `frontend/src/pages/dashboard/DashboardPage.tsx`:
- Add `import { Link } from 'react-router';` as the first import line.
- Replace:

```tsx
        <LiveBadge hubKey={DASHBOARD_HUB} />
```

with:

```tsx
        <div className="dashboard-heading__side">
          <Link className="app-button" to="/admin/export">
            ส่งออก Excel
          </Link>
          <LiveBadge hubKey={DASHBOARD_HUB} />
        </div>
```

In `frontend/src/pages/dashboard/dashboard.css`, below the `.dashboard-heading h1` rule, add:

```css
.dashboard-heading__side {
  display: flex;
  align-items: center;
  gap: 8px;
}
```

In `frontend/src/pages/attendance/AttendancePage.tsx`:
- Add `import { exportLink } from '../../shared/utils/dateRange';` beside the other `shared/utils` imports.
- Replace:

```tsx
        <LiveBadge hubKey={attendanceHubKey(args)} />
      </header>
```

with:

```tsx
        <div className="attendance-heading__side">
          {board && (
            <Link className="app-button" to={exportLink({ from: board.shiftDate, to: board.shiftDate }, args.buildingId)}>
              ส่งออก Excel
            </Link>
          )}
          <LiveBadge hubKey={attendanceHubKey(args)} />
        </div>
      </header>
```

In `frontend/src/pages/attendance/attendance.css`, below the `.attendance-heading h1` rule, add:

```css
.attendance-heading__side {
  display: flex;
  align-items: center;
  gap: 8px;
}
```

In `frontend/src/pages/inspection-summary/InspectionSummaryPage.tsx`:
- Add `Link` to the `react-router` import: `import { Link, useSearchParams } from 'react-router';`.
- Add `exportLink` to the `dateRange` import list (alphabetical: after `checkRange`).
- Right after the `BuildingSelect` line inside `summary-controls`, add:

```tsx
        <Link
          className="app-button summary-export"
          to={exportLink(summary ? { from: summary.from, to: summary.to } : asked, buildingId)}
        >
          ส่งออก Excel
        </Link>
```

In `frontend/src/pages/inspection-summary/inspectionSummary.css`, below `.summary-controls`, add:

```css
.summary-export {
  margin-left: auto;
}
```

- [ ] **Step 9: Run the tests to make sure they pass**

Run: `cd frontend && npx vitest run src/pages/export src/pages/dashboard/DashboardPage.test.tsx src/pages/attendance/AttendancePage.test.tsx src/pages/inspection-summary src/router.test.tsx src/test/fakeServer.test.ts`
Expected: PASS.

If the 401 test fails with the refresh called once, the response handler threw on the empty 401 body. Check that it reads `text()` first, as written.

- [ ] **Step 10: Whole frontend check and bundle**

Run: `cd frontend && npm run lint && npx tsc -b && npm test`
Expected: clean; 354 tests pass (340 + 1 + 2 + 8 + 3).

Run: `cd frontend && npm run build && grep -o 'assets/index-[A-Za-z0-9_-]*\.js' dist/index.html && ls dist/assets/*.js && rm -rf dist`
Expected: the main file ≤ 200 kB gzip (about 157 kB), and an `ExportPage-*.js` file.

- [ ] **Step 11: Handoff entry and commit**

Add under `## Task log (plan 12)`:

```markdown
- <yyyy-MM-dd> · Task 3 done: Excel export D8 (`/admin/export`, lazy, in the menu as ส่งออก Excel): the three sets (การลงเวลา and การส่งงาน ticked by default, sent in the file's order), range typed dd/mm/yyyy with the shortcuts (1–31 dates, checked before sending), building; the file name and sheets before the download; "ดาวน์โหลด .xlsx" posts through the refresh-on-401 base query, saves the server's file under its Content-Disposition name (`exportApi`'s `queryFn` hands the Blob to `saveFile`, so it never enters the store), and is disabled while in flight. "ส่งออก Excel" links on D1, on D2 (shift date and building) and on D7 (range and building); D8 reads `?from&to&building`. `fakeServer` answers files; `test/fakeDownload.ts` records saved files. Frontend tests <n> passed; main JS <size> kB gzip. Next: Task 4 (Admin history D9).
```

```bash
git add docs/handoff.md frontend/src/router.tsx frontend/src/router.test.tsx frontend/src/shared/components/AdminNav.tsx \
  frontend/src/test/fakeServer.ts frontend/src/test/fakeServer.test.ts frontend/src/test/fakeDownload.ts \
  frontend/src/pages/export \
  frontend/src/pages/dashboard/DashboardPage.tsx frontend/src/pages/dashboard/dashboard.css frontend/src/pages/dashboard/DashboardPage.test.tsx \
  frontend/src/pages/attendance/AttendancePage.tsx frontend/src/pages/attendance/attendance.css frontend/src/pages/attendance/AttendancePage.test.tsx \
  frontend/src/pages/inspection-summary/InspectionSummaryPage.tsx frontend/src/pages/inspection-summary/inspectionSummary.css \
  frontend/src/pages/inspection-summary/InspectionSummaryPage.test.tsx
git commit -m "feat(frontend): Excel export D8 with links from D1, D2 and D7 (facility-0053)"
```

---

### Task 4: Admin history (D9): list, filters, older pages

The read-only list of Admin changes, filtered by range, actor and type, newest first, with older pages on request (facility-0057 rule 2).

**Files:**
- Create: `frontend/src/pages/history/historyApi.ts`
- Create: `frontend/src/pages/history/historyRules.ts`
- Test: `frontend/src/pages/history/historyRules.test.ts`
- Create: `frontend/src/pages/history/HistoryControls.tsx`
- Create: `frontend/src/pages/history/HistoryTable.tsx`
- Create: `frontend/src/pages/history/HistoryPage.tsx`
- Create: `frontend/src/pages/history/history.css`
- Create: `frontend/src/pages/history/index.ts`
- Test: `frontend/src/pages/history/HistoryPage.test.tsx`
- Modify: `frontend/src/test/reportFixtures.ts`
- Modify: `frontend/src/router.tsx`, `frontend/src/router.test.tsx`, `frontend/src/shared/components/AdminNav.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: Task 1's `checkRange`, `lastSevenDays`, `rangeTexts`, `readRange`, `toRangeParams`, `RangeTexts`; Task 2's `RangeFields`; `formatDayTime`; `freshOnOpen`.
- Produces:
  - In `pages/history/historyApi.ts`: `AuditCategory`, `AuditEntry`, `AuditPage`, `AuditActor`, `HistoryArgs`, `useGetAuditLogInfiniteQuery`, `useGetAuditActorsQuery`.
  - In `historyRules.ts`: `HISTORY_MAX_DAYS`, `auditCategories`, `categoryLabel`, `actionLabel`, `actionText(action)`, `readHistoryArgs(params)`, `toHistoryParams(args)`.
  - `HistoryTable({ entries: AuditEntry[]; emptyText: string })`, with 5 columns: เวลา, ผู้ทำ, ประเภท, รายการที่ถูกแก้, เหตุผล. Task 5 adds "ก่อน → หลัง" before เหตุผล.
  - `HistoryControls` props `{ args, actors?, texts, onTexts, onShow, onArgs }`. Task 5 adds `search` and `onSearch`.
  - In `reportFixtures.ts`: `actors`, `auditEntry()`, `firstPage`, `olderPage`.
  - The route `admin/history` and the menu link "ประวัติ Admin".

- [ ] **Step 1: Write the API types and the made-up log**

Create `frontend/src/pages/history/historyApi.ts`:

```ts
import { baseApi } from '../../shared/api/baseApi';

/** AuditCategory on the server: the "ประเภท" filter (facility-0057). */
export type AuditCategory = 'Attendance' | 'Cover' | 'AlertReview' | 'Setup' | 'QrToken' | 'Account' | 'Export';

/**
 * AuditLogEntryDto. before and after are the stored snapshots as JSON objects (PascalCase keys, null fields kept);
 * they never hold a phone number or password (facility-0057). category is missing for an action no category lists.
 */
export interface AuditEntry {
  id: number;
  occurredAt: string;
  actorId: number;
  actorName: string;
  action: string;
  category?: AuditCategory;
  entityType: string;
  entityId?: number;
  summary: string;
  before?: unknown;
  after?: unknown;
  reason?: string;
}

/** AuditLogPageDto: nextBeforeId is sent while a page came back full. */
export interface AuditPage {
  entries: AuditEntry[];
  nextBeforeId?: number;
}

/** AuditActorDto: everyone who appears in the log. */
export interface AuditActor {
  id: number;
  displayName: string;
}

/** Both dates or neither (then the server's last 7 dates); missing filters are left out. */
export interface HistoryArgs {
  from?: string;
  to?: string;
  actorId?: number;
  category?: AuditCategory;
}

/** The first page asks for the newest rows: 0 stands for "no beforeId" (ids start at 1). */
const NEWEST = 0;

const historyApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    // plan 12 decision 7: older pages by beforeId, newest first
    getAuditLog: build.infiniteQuery<AuditPage, HistoryArgs, number>({
      infiniteQueryOptions: {
        initialPageParam: NEWEST,
        getNextPageParam: (lastPage) => lastPage.nextBeforeId,
      },
      query: ({ queryArg: { from, to, actorId, category }, pageParam }) => ({
        url: '/admin/audit-log',
        params: { from, to, actorId, category, beforeId: pageParam === NEWEST ? undefined : pageParam },
      }),
    }),
    getAuditActors: build.query<AuditActor[], void>({
      query: () => '/admin/audit-log/actors',
    }),
  }),
});

export const { useGetAuditLogInfiniteQuery, useGetAuditActorsQuery } = historyApi;
```

Append to `frontend/src/test/reportFixtures.ts`. Put the import at the top of the file, below the existing one:

```ts
import type { AuditActor, AuditEntry, AuditPage } from '../pages/history/historyApi';
```

and at the end of the file:

```ts
/** The log's actors, by name, as the server orders them. Account 7 is the logged-in Admin. */
export const actors: AuditActor[] = [
  { id: 8, displayName: 'ผู้ดูแลคนที่สอง' },
  { id: 7, displayName: 'ผู้ดูแลทดสอบ' },
];

/** A Shift-In the Admin added at 10:42 on 15/10 for 06:58, with the reason. Snapshots keep null fields, as stored. */
export function auditEntry(overrides: Partial<AuditEntry> = {}): AuditEntry {
  return {
    id: 50,
    occurredAt: '2026-10-15T03:42:00Z', // 10:42
    actorId: 7,
    actorName: 'ผู้ดูแลทดสอบ',
    action: 'ATTENDANCE_ADD',
    category: 'Attendance',
    entityType: 'shift_attendances',
    entityId: 900,
    summary: 'เพิ่มเวลาเข้างานของ 650001 แม่บ้านทดสอบ AR01 กะเช้า 15/10/2026',
    after: { EventType: 'ShiftIn', OccurredAt: '2026-10-14T23:58:00Z', Source: 'AdminAdd' }, // 06:58
    reason: 'มือถือแบตหมด หัวหน้ายืนยันว่ามาทำงาน',
    ...overrides,
  };
}

/** The newest page: five rows, then nextBeforeId for the older one. */
export const firstPage: AuditPage = {
  entries: [
    auditEntry(),
    auditEntry({
      id: 49,
      occurredAt: '2026-10-15T02:12:00Z', // 09:12
      actorId: 8,
      actorName: 'ผู้ดูแลคนที่สอง',
      action: 'COVER_ASSIGN',
      category: 'Cover',
      entityType: 'cover_assignments',
      entityId: 31,
      summary: 'มอบหมาย แม่บ้านที่อื่น ทำแทน AR01 กะเช้า 15/10/2026',
      after: { UserId: 25, AreaId: 1, BuildingId: null, ShiftDate: '2026-10-15', Shift: 'Day', BlockedScanId: null },
      reason: undefined,
    }),
    auditEntry({
      id: 48,
      occurredAt: '2026-10-15T02:05:00Z', // 09:05
      action: 'ALERT_REVIEW',
      category: 'AlertReview',
      entityType: 'shift_attendances',
      entityId: 401,
      summary: 'ปิดเตือนลงเวลาไกลจากป้ายของ แม่บ้านทดสอบ เวลา 06:52',
      before: { Reviewed: false },
      after: { Reviewed: true, AlertType: 'AttendanceGps' },
      reason: 'GPS ในอาคารเพี้ยน ยืนอยู่หน้าป้ายจริง',
    }),
    auditEntry({
      id: 47,
      occurredAt: '2026-10-14T04:20:00Z', // 14/10 11:20
      action: 'ACCOUNT_PHONE',
      category: 'Account',
      entityType: 'users',
      entityId: 21,
      summary: 'เปลี่ยนเบอร์โทรของ 650001 แม่บ้านทดสอบ',
      after: undefined,
      reason: undefined,
    }),
    auditEntry({
      id: 46,
      occurredAt: '2026-10-14T01:15:00Z', // 14/10 08:15
      action: 'EXPORT_EXCEL',
      category: 'Export',
      entityType: 'export',
      entityId: undefined,
      summary: 'ส่งออก Excel การลงเวลา + การส่งงาน วันที่ 2026-10-01 ถึง 2026-10-05 ทุกตึก',
      after: { From: '2026-10-01', To: '2026-10-05', BuildingId: null, Sets: ['Attendance', 'Submissions'], Rows: [28, 412] },
      reason: undefined,
    }),
  ],
  nextBeforeId: 46,
};

/** The older page (beforeId=46): the last one. */
export const olderPage: AuditPage = {
  entries: [
    auditEntry({
      id: 45,
      occurredAt: '2026-10-13T07:40:00Z', // 13/10 14:40
      action: 'QR_REGENERATE',
      category: 'QrToken',
      entityType: 'signs',
      entityId: 103,
      summary: 'ออก QR ใหม่ของป้าย AR01-03 ป้ายเดิมสแกนไม่ได้แล้ว',
      before: { QrIssuedAt: '2026-10-01T02:00:00Z' },
      after: { QrIssuedAt: '2026-10-13T07:40:00Z' },
      reason: undefined,
    }),
  ],
};
```

(An `undefined` override is dropped when the fake server turns the answer into JSON, so those fields arrive missing, as the server leaves them out.)

- [ ] **Step 2: The rules**

Create `frontend/src/pages/history/historyRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { actionText, readHistoryArgs, toHistoryParams } from './historyRules';

describe('historyRules (D9)', () => {
  it('actionText names each action in Thai, and shows an unknown one as stored', () => {
    expect(actionText('ATTENDANCE_EDIT')).toBe('แก้เวลาลงงาน');
    expect(actionText('ACCOUNT_PHONE')).toBe('แก้เบอร์โทร');
    expect(actionText('SIGN_LOCATION')).toBe('เก็บพิกัดป้าย');
    expect(actionText('SOMETHING_NEW')).toBe('SOMETHING_NEW');
    expect(actionText('constructor')).toBe('constructor');
  });

  it('readHistoryArgs keeps a range given in full, a real actor id and a known type', () => {
    expect(readHistoryArgs(new URLSearchParams('from=2026-10-01&to=2026-10-07&actor=8&category=Cover'))).toEqual({
      from: '2026-10-01',
      to: '2026-10-07',
      actorId: 8,
      category: 'Cover',
    });
    expect(readHistoryArgs(new URLSearchParams('from=2026-10-01&actor=x&category=Nope'))).toEqual({});
  });

  it('toHistoryParams writes what readHistoryArgs reads, leaving out what is not chosen', () => {
    expect(toHistoryParams({ from: '2026-10-01', to: '2026-10-07', actorId: 8, category: 'Cover' }).toString()).toBe(
      'from=2026-10-01&to=2026-10-07&actor=8&category=Cover',
    );
    expect(toHistoryParams({ category: 'Export' }).toString()).toBe('category=Export');
    expect(toHistoryParams({}).toString()).toBe('');
  });
});
```

Create `frontend/src/pages/history/historyRules.ts`:

```ts
import { readRange, toRangeParams } from '../../shared/utils/dateRange';
import type { AuditCategory, HistoryArgs } from './historyApi';

/** AdminAuditLogEndpoints: maxDays 92. */
export const HISTORY_MAX_DAYS = 92;

export const auditCategories: readonly AuditCategory[] = [
  'Attendance',
  'Cover',
  'AlertReview',
  'Setup',
  'QrToken',
  'Account',
  'Export',
];

/** Wireframe D9's "ประเภท" options, word for word. */
export const categoryLabel: Record<AuditCategory, string> = {
  Attendance: 'แก้การลงเวลา',
  Cover: 'มอบหมายทำแทน',
  AlertReview: 'ปิดเตือน',
  Setup: 'Area · จุด · รอบ · ป้าย',
  QrToken: 'ออก QR ใหม่',
  Account: 'บัญชี',
  Export: 'ส่งออก Excel',
};

/** Each action's Thai name for the "ประเภท" column (plan 12 decision 8); AuditCategories.cs lists the actions. */
export const actionLabel: Record<string, string> = {
  ATTENDANCE_ADD: 'เพิ่มเวลาลงงาน',
  ATTENDANCE_EDIT: 'แก้เวลาลงงาน',
  COVER_ASSIGN: 'มอบหมายทำแทน',
  COVER_CANCEL: 'ยกเลิกทำแทน',
  ALERT_REVIEW: 'ปิดเตือน',
  AREA_CREATE: 'เพิ่ม Area',
  AREA_UPDATE: 'แก้ Area',
  AREA_ACTIVATE: 'เปิดใช้ Area',
  AREA_DEACTIVATE: 'ปิดใช้ Area',
  POINT_CREATE: 'เพิ่มจุด',
  POINT_UPDATE: 'แก้จุดและช่วงรอบ',
  POINT_ACTIVATE: 'เปิดใช้จุด',
  POINT_DEACTIVATE: 'ปิดใช้จุด',
  SIGN_LOCATION: 'เก็บพิกัดป้าย',
  SIGN_RADIUS: 'แก้รัศมีป้าย',
  QR_REGENERATE: 'ออก QR ใหม่',
  ACCOUNT_CREATE: 'เพิ่มบัญชี',
  ACCOUNT_UPDATE: 'แก้บัญชี',
  ACCOUNT_PHONE: 'แก้เบอร์โทร',
  ACCOUNT_PASSWORD: 'รีเซ็ตรหัสผ่าน',
  ACCOUNT_ACTIVATE: 'เปิดใช้บัญชี',
  ACCOUNT_DEACTIVATE: 'ปิดใช้บัญชี',
  EXPORT_EXCEL: 'ส่งออก Excel',
};

/** An action no label lists yet shows as stored, so nothing is hidden. */
export function actionText(action: string): string {
  return Object.hasOwn(actionLabel, action) ? actionLabel[action] : action;
}

/** ?from&to&actor=<id>&category=<AuditCategory>; anything else is left out, because an empty filter gets 400. */
export function readHistoryArgs(params: URLSearchParams): HistoryArgs {
  const args: HistoryArgs = { ...readRange(params) };
  const actor = Number(params.get('actor'));
  if (Number.isInteger(actor) && actor > 0) {
    args.actorId = actor;
  }
  const category = auditCategories.find((each) => each === params.get('category'));
  if (category) {
    args.category = category;
  }
  return args;
}

export function toHistoryParams({ from, to, actorId, category }: HistoryArgs): URLSearchParams {
  const params = toRangeParams(from && to ? { from, to } : undefined, undefined);
  if (actorId) {
    params.set('actor', String(actorId));
  }
  if (category) {
    params.set('category', category);
  }
  return params;
}
```

Run: `cd frontend && npx vitest run src/pages/history/historyRules.test.ts`
Expected: PASS (3 tests).

- [ ] **Step 3: Write the failing page tests**

Create `frontend/src/pages/history/HistoryPage.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { addDays } from '../../shared/utils/dateRange';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { adminSession } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { actors, firstPage, olderPage } from '../../test/reportFixtures';
import { renderApp } from '../../test/renderApp';

const query = (search: string) => Object.fromEntries(new URLSearchParams(search));

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/audit-log': (request) => ({
      status: 200,
      body: query(request.search).beforeId === '46' ? olderPage : firstPage,
    }),
    'GET /api/admin/audit-log/actors': () => ({ status: 200, body: actors }),
    ...routes,
  });
}

const logGets = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/admin/audit-log');

/** Each row's cells. */
const rows = () =>
  within(screen.getByRole('table', { name: 'ประวัติของ Admin' }))
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getAllByRole('cell'));

/** Time, actor, type, what was changed, and the reason (the last cell). */
const mainCells = () => rows().map((cells) => [...cells.slice(0, 4), cells[cells.length - 1]].map((cell) => cell.textContent));

const fromField = () => screen.getByRole('textbox', { name: 'ตั้งแต่' });
const toField = () => screen.getByRole('textbox', { name: 'ถึง' });

describe('HistoryPage (D9, facility-0057)', () => {
  it('lists the last 7 days newest first: when, who, what kind, what changed, and why', async () => {
    const server = serve();
    await renderApp('/admin/history');

    await screen.findByRole('table', { name: 'ประวัติของ Admin' });
    expect(query(logGets(server)[0].search)).toEqual({});
    expect(mainCells()).toEqual([
      ['15/10 10:42', 'ผู้ดูแลทดสอบ', 'เพิ่มเวลาลงงาน', 'เพิ่มเวลาเข้างานของ 650001 แม่บ้านทดสอบ AR01 กะเช้า 15/10/2026', 'มือถือแบตหมด หัวหน้ายืนยันว่ามาทำงาน'],
      ['15/10 09:12', 'ผู้ดูแลคนที่สอง', 'มอบหมายทำแทน', 'มอบหมาย แม่บ้านที่อื่น ทำแทน AR01 กะเช้า 15/10/2026', '–'],
      ['15/10 09:05', 'ผู้ดูแลทดสอบ', 'ปิดเตือน', 'ปิดเตือนลงเวลาไกลจากป้ายของ แม่บ้านทดสอบ เวลา 06:52', 'GPS ในอาคารเพี้ยน ยืนอยู่หน้าป้ายจริง'],
      ['14/10 11:20', 'ผู้ดูแลทดสอบ', 'แก้เบอร์โทร', 'เปลี่ยนเบอร์โทรของ 650001 แม่บ้านทดสอบ', '–'],
      ['14/10 08:15', 'ผู้ดูแลทดสอบ', 'ส่งออก Excel', 'ส่งออก Excel การลงเวลา + การส่งงาน วันที่ 2026-10-01 ถึง 2026-10-05 ทุกตึก', '–'],
    ]);
    expect(screen.getByText('แสดง 5 รายการ')).toBeInTheDocument();
    expect(fromField()).toHaveValue(formatShiftDate(addDays(thaiToday(), -6)));
    expect(toField()).toHaveValue(formatShiftDate(thaiToday()));
    expect(
      screen.getByText(
        'ไม่มีใครแก้หรือลบรายการในหน้านี้ได้ · เก็บนานเท่ากับข้อมูลที่ถูกแก้ · เบอร์โทรไม่แสดงทั้งก่อนและหลัง · การเปิดดูหน้านี้ไม่ถูกบันทึก',
      ),
    ).toBeInTheDocument();
  });

  it('"โหลดรายการเก่ากว่านี้" adds the older page, and goes once the last page is in', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/history');
    await screen.findByText('แสดง 5 รายการ');

    await user.click(screen.getByRole('button', { name: 'โหลดรายการเก่ากว่านี้' }));

    expect(await screen.findByText('แสดง 6 รายการ')).toBeInTheDocument();
    expect(mainCells()[5]).toEqual(['13/10 14:40', 'ผู้ดูแลทดสอบ', 'ออก QR ใหม่', 'ออก QR ใหม่ของป้าย AR01-03 ป้ายเดิมสแกนไม่ได้แล้ว', '–']);
    expect(query(logGets(server)[1].search)).toEqual({ beforeId: '46' });
    expect(screen.queryByRole('button', { name: 'โหลดรายการเก่ากว่านี้' })).not.toBeInTheDocument();
  });

  it('an empty last page (rows exactly filled the page before) adds nothing and the button goes', async () => {
    serve({
      'GET /api/admin/audit-log': (request) => ({
        status: 200,
        body: query(request.search).beforeId === '46' ? { entries: [] } : firstPage,
      }),
    });
    const { user } = await renderApp('/admin/history');
    await screen.findByText('แสดง 5 รายการ');

    await user.click(screen.getByRole('button', { name: 'โหลดรายการเก่ากว่านี้' }));

    await waitFor(() => expect(screen.queryByRole('button', { name: 'โหลดรายการเก่ากว่านี้' })).not.toBeInTheDocument());
    expect(screen.getByText('แสดง 5 รายการ')).toBeInTheDocument();
  });

  it('the actor and type pickers ask at once, and the address keeps them', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/history');
    await screen.findByRole('option', { name: 'ผู้ดูแลคนที่สอง' });

    await user.selectOptions(screen.getByRole('combobox', { name: 'ประเภท' }), 'แก้การลงเวลา');
    await user.selectOptions(screen.getByRole('combobox', { name: 'ผู้ทำ' }), 'ผู้ดูแลคนที่สอง');

    await waitFor(() => expect(router.state.location.search).toBe('?actor=8&category=Attendance'));
    await waitFor(() => expect(query(logGets(server).at(-1)!.search)).toEqual({ actorId: '8', category: 'Attendance' }));
    expect(
      within(screen.getByRole('combobox', { name: 'ประเภท' }))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['ทุกประเภท', 'แก้การลงเวลา', 'มอบหมายทำแทน', 'ปิดเตือน', 'Area · จุด · รอบ · ป้าย', 'ออก QR ใหม่', 'บัญชี', 'ส่งออก Excel']);
  });

  it('a typed range is checked before asking (92 days at most), then kept in the address', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/history');
    await screen.findByText('แสดง 5 รายการ');

    await user.clear(fromField());
    await user.type(fromField(), '01/07/2026');
    await user.clear(toField());
    await user.type(toField(), '01/10/2026');
    await user.click(screen.getByRole('button', { name: 'แสดง' }));
    expect(screen.getByRole('alert')).toHaveTextContent('เลือกได้ไม่เกิน 92 วัน');
    expect(logGets(server)).toHaveLength(1);

    await user.clear(fromField());
    await user.type(fromField(), '02/07/2026{Enter}');

    await waitFor(() => expect(router.state.location.search).toBe('?from=2026-07-02&to=2026-10-01'));
    await waitFor(() => expect(query(logGets(server).at(-1)!.search)).toEqual({ from: '2026-07-02', to: '2026-10-01' }));
  });

  it('opens the filters in the address', async () => {
    const server = serve();
    await renderApp('/admin/history?from=2026-10-01&to=2026-10-07&actor=7&category=Export');

    await screen.findByText('แสดง 5 รายการ');
    expect(query(logGets(server)[0].search)).toEqual({ from: '2026-10-01', to: '2026-10-07', actorId: '7', category: 'Export' });
    expect(fromField()).toHaveValue('01/10/2026');
    expect(screen.getByRole('combobox', { name: 'ประเภท' })).toHaveDisplayValue('ส่งออก Excel');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ผู้ทำ' })).toHaveDisplayValue('ผู้ดูแลทดสอบ'));
  });

  it("shows the server's refusal", async () => {
    serve({ 'GET /api/admin/audit-log': () => ({ status: 400, body: { message: 'ไม่มีประเภทนี้ในประวัติ' } }) });
    await renderApp('/admin/history');
    expect(await screen.findByRole('alert')).toHaveTextContent('ไม่มีประเภทนี้ในประวัติ');
  });

  it('says so when nothing happened in the range', async () => {
    serve({ 'GET /api/admin/audit-log': () => ({ status: 200, body: { entries: [] } }) });
    await renderApp('/admin/history');
    expect(await screen.findByText('ไม่มีประวัติในช่วงนี้')).toBeInTheDocument();
  });

  it('is in the Admin menu as ประวัติ Admin', async () => {
    serve();
    await renderApp('/admin/history');
    const menu = screen.getByRole('navigation', { name: 'เมนู Admin' });
    expect(within(menu).getByRole('link', { name: 'ประวัติ Admin' })).toHaveAttribute('aria-current', 'page');
  });
});
```

In `frontend/src/router.test.tsx`:
- Put `'ประวัติ Admin',` last in the menu list (after `'บัญชีผู้ใช้',`).
- Put `'admin/history',` after `'admin/accounts',` in the lazy route list.

The menu list is now:

```tsx
      'สถานะจุด',
      'การเข้างาน',
      'รายการที่ขึ้นเตือน',
      'สรุปการตรวจ',
      'ส่งออก Excel',
      'จัดการ Area และป้าย',
      'บัญชีผู้ใช้',
      'ประวัติ Admin',
```

- [ ] **Step 4: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/history src/router.test.tsx`
Expected: the page tests and the router tests FAIL (no page, no menu link). The rules test still passes.

- [ ] **Step 5: Write the page**

Create `frontend/src/pages/history/HistoryControls.tsx`:

```tsx
import { RangeFields } from '../../shared/components/RangeFields';
import type { RangeTexts } from '../../shared/utils/dateRange';
import type { AuditActor, HistoryArgs } from './historyApi';
import { auditCategories, categoryLabel } from './historyRules';

interface HistoryControlsProps {
  args: HistoryArgs;
  actors?: AuditActor[];
  texts: RangeTexts;
  onTexts: (next: RangeTexts) => void;
  /** Check the typed range and show it. */
  onShow: () => void;
  /** The actor or type changed: ask at once. */
  onArgs: (next: HistoryArgs) => void;
}

/** Wireframe D9's filters: the range (typed), ผู้ทำ and ประเภท (facility-0057 rule 2). */
export function HistoryControls({ args, actors, texts, onTexts, onShow, onArgs }: HistoryControlsProps) {
  return (
    <div className="history-controls">
      <RangeFields value={texts} onChange={onTexts} onEnter={onShow} />
      <button type="button" className="app-button" onClick={onShow}>
        แสดง
      </button>
      <label className="app-field">
        ผู้ทำ
        <select
          className="app-input"
          value={args.actorId ?? ''}
          onChange={(event) =>
            onArgs({ ...args, actorId: event.target.value === '' ? undefined : Number(event.target.value) })
          }
        >
          <option value="">ทุกคน</option>
          {(actors ?? []).map((actor) => (
            <option key={actor.id} value={actor.id}>
              {actor.displayName}
            </option>
          ))}
        </select>
      </label>
      <label className="app-field">
        ประเภท
        <select
          className="app-input"
          value={args.category ?? ''}
          onChange={(event) => onArgs({ ...args, category: auditCategories.find((each) => each === event.target.value) })}
        >
          <option value="">ทุกประเภท</option>
          {auditCategories.map((category) => (
            <option key={category} value={category}>
              {categoryLabel[category]}
            </option>
          ))}
        </select>
      </label>
    </div>
  );
}
```

Create `frontend/src/pages/history/HistoryTable.tsx`:

```tsx
import { formatDayTime } from '../../shared/utils/thaiTime';
import type { AuditEntry } from './historyApi';
import { actionText } from './historyRules';

interface HistoryTableProps {
  entries: AuditEntry[];
  /** What to say when there is no row. */
  emptyText: string;
}

/** Wireframe D9's table, newest first. Summaries, names and reasons are React text only (facility-0066). */
export function HistoryTable({ entries, emptyText }: HistoryTableProps) {
  if (entries.length === 0) {
    return <p className="app-note app-note--info">{emptyText}</p>;
  }
  return (
    <div className="app-table-scroll">
      <table className="app-table history-table" aria-label="ประวัติของ Admin">
        <thead>
          <tr>
            <th scope="col">เวลา</th>
            <th scope="col">ผู้ทำ</th>
            <th scope="col">ประเภท</th>
            <th scope="col">รายการที่ถูกแก้</th>
            <th scope="col">เหตุผล</th>
          </tr>
        </thead>
        <tbody>
          {entries.map((entry) => (
            <tr key={entry.id}>
              <td className="history-time">{formatDayTime(entry.occurredAt)}</td>
              <td>{entry.actorName}</td>
              <td>
                <strong>{actionText(entry.action)}</strong>
              </td>
              <td>{entry.summary}</td>
              <td className="history-reason">{entry.reason ?? '–'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

Create `frontend/src/pages/history/HistoryPage.tsx`:

```tsx
import { useState } from 'react';
import { useSearchParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import { checkRange, lastSevenDays, rangeTexts } from '../../shared/utils/dateRange';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { thaiToday } from '../../shared/utils/thaiTime';
import { HistoryControls } from './HistoryControls';
import { useGetAuditActorsQuery, useGetAuditLogInfiniteQuery, type HistoryArgs } from './historyApi';
import { HISTORY_MAX_DAYS, readHistoryArgs, toHistoryParams } from './historyRules';
import { HistoryTable } from './HistoryTable';
import './history.css';

/**
 * facility-0057 rule 2, wireframe D9: every Admin change, newest first, read-only; opening it is not logged. The
 * range, actor and type are in the address; with no range the server's last 7 dates apply. Older pages come on
 * request (plan 12 decision 7).
 */
export function HistoryPage() {
  const [params, setParams] = useSearchParams();
  const args = readHistoryArgs(params);
  const log = useGetAuditLogInfiniteQuery(args, freshOnOpen);
  const { data: actors } = useGetAuditActorsQuery(undefined, freshOnOpen);
  const [texts, setTexts] = useState(() =>
    rangeTexts(args.from && args.to ? { from: args.from, to: args.to } : lastSevenDays(thaiToday())),
  );
  const [problem, setProblem] = useState<string | null>(null);
  const entries = log.currentData?.pages.flatMap((page) => page.entries) ?? [];
  const go = (next: HistoryArgs) => setParams(toHistoryParams(next));

  const show = () => {
    const range = checkRange(texts, HISTORY_MAX_DAYS);
    if (typeof range === 'string') {
      setProblem(range);
      return;
    }
    setProblem(null);
    go({ ...args, ...range });
  };

  return (
    <main className="history-page">
      <header>
        <span className="app-muted">Admin เท่านั้น · อ่านอย่างเดียว</span>
        <h1>ประวัติการเปลี่ยนแปลงของ Admin</h1>
      </header>
      <HistoryControls args={args} actors={actors} texts={texts} onTexts={setTexts} onShow={show} onArgs={go} />
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      {log.isFetching && !log.currentData && <p className="app-muted">กำลังโหลด...</p>}
      {log.error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(log.error)}
        </p>
      )}
      {log.currentData && (
        <>
          <HistoryTable entries={entries} emptyText="ไม่มีประวัติในช่วงนี้" />
          <div className="history-more">
            <span className="app-muted">{`แสดง ${entries.length} รายการ`}</span>
            {log.hasNextPage && (
              <button
                type="button"
                className="app-button"
                disabled={log.isFetchingNextPage}
                onClick={() => void log.fetchNextPage()}
              >
                {log.isFetchingNextPage ? 'กำลังโหลด...' : 'โหลดรายการเก่ากว่านี้'}
              </button>
            )}
          </div>
        </>
      )}
      <p className="history-footnote">
        ไม่มีใครแก้หรือลบรายการในหน้านี้ได้ · เก็บนานเท่ากับข้อมูลที่ถูกแก้ · เบอร์โทรไม่แสดงทั้งก่อนและหลัง · การเปิดดูหน้านี้ไม่ถูกบันทึก
      </p>
    </main>
  );
}
```

Create `frontend/src/pages/history/history.css`:

```css
/* Admin history (D9, facility-0057). Class prefix: history- */

.history-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.history-page h1 {
  font-size: 26px;
  font-weight: 700;
}

.history-controls {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 8px;
  font-size: 14px;
}

.history-time {
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
}

.history-reason {
  color: var(--color-text-soft);
}

.history-more {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
}

.history-footnote {
  padding: 8px 12px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  color: var(--color-text-soft);
  font-size: 13px;
}
```

Create `frontend/src/pages/history/index.ts`:

```ts
export { HistoryPage } from './HistoryPage';
```

- [ ] **Step 6: Route and menu**

In `frontend/src/router.tsx`, right after the `admin/accounts` route, add:

```tsx
              // facility-0057, wireframe D9: ?from&to&actor=<id>&category=<AuditCategory>
              { path: 'admin/history', lazy: async () => ({ Component: (await import('./pages/history')).HistoryPage }) },
```

In `frontend/src/shared/components/AdminNav.tsx`, add as the last entry:

```tsx
  { to: '/admin/history', label: 'ประวัติ Admin', counted: false },
```

- [ ] **Step 7: Run the tests to make sure they pass**

Run: `cd frontend && npx vitest run src/pages/history src/router.test.tsx`
Expected: PASS.

If `useGetAuditLogInfiniteQuery`'s result has no `currentData`, stop and report to the controller (see Task 2 Step 8).

- [ ] **Step 8: Whole frontend check and bundle**

Run: `cd frontend && npm run lint && npx tsc -b && npm test`
Expected: clean; 366 tests pass (354 + 3 + 9).

Run: `cd frontend && npm run build && grep -o 'assets/index-[A-Za-z0-9_-]*\.js' dist/index.html && ls dist/assets/*.js && rm -rf dist`
Expected: main file ≤ 200 kB gzip, and a `HistoryPage-*.js` file.

- [ ] **Step 9: Handoff entry and commit**

Add under `## Task log (plan 12)`:

```markdown
- <yyyy-MM-dd> · Task 4 done: Admin history D9 (`/admin/history`, lazy, last in the menu as ประวัติ Admin): newest first with time, actor, the action's Thai name, the summary and the reason ("–" when none); range typed dd/mm/yyyy (92 dates at most, checked before asking), ผู้ทำ from `/audit-log/actors` and ประเภท with the wireframe's seven labels, all in the address; older pages with "โหลดรายการเก่ากว่านี้" (RTK Query infinite query by `beforeId`; an empty last page adds nothing and the button goes). Frontend tests <n> passed; main JS <size> kB gzip. Next: Task 5 (before → after, search).
```

```bash
git add docs/handoff.md frontend/src/router.tsx frontend/src/router.test.tsx frontend/src/shared/components/AdminNav.tsx \
  frontend/src/pages/history frontend/src/test/reportFixtures.ts
git commit -m "feat(frontend): Admin history D9 with filters and older pages (facility-0057)"
```

---

### Task 5: D9's "ก่อน → หลัง" and search

Each row shows what changed, field by field, from the stored snapshots, with no phone or password (facility-0057). The search box narrows the rows already loaded.

**Files:**
- Modify: `frontend/src/pages/history/historyRules.ts`
- Test: `frontend/src/pages/history/historyRules.test.ts`
- Modify: `frontend/src/pages/history/HistoryTable.tsx`
- Modify: `frontend/src/pages/history/HistoryControls.tsx`
- Modify: `frontend/src/pages/history/HistoryPage.tsx`
- Modify: `frontend/src/pages/history/history.css`
- Test: `frontend/src/pages/history/HistoryPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: Task 4's `AuditEntry`, `actionText`, `HistoryTable`, `HistoryControls`; `eventLabel`, `shiftShortLabel`, `shiftPatternLabel` from `shared/utils/labels.ts`; `formatDayTime`, `formatShiftDate`.
- Produces:
  - `changeLines(entry: Pick<AuditEntry, 'action' | 'before' | 'after'>): string[]`
  - `searchEntries(entries: readonly AuditEntry[], search: string): AuditEntry[]`
  - The table's "ก่อน → หลัง" column (a list per row, or "–").
  - `HistoryControls`' `search` and `onSearch` props (a search box labelled "ค้นหา").

- [ ] **Step 1: Write the failing rule tests**

In `frontend/src/pages/history/historyRules.test.ts`:
- Change the import line to:

```ts
import { firstPage, olderPage } from '../../test/reportFixtures';
import { actionText, changeLines, readHistoryArgs, searchEntries, toHistoryParams } from './historyRules';
```

- Add inside the `describe` block:

```ts
  it('changeLines: one line per changed field, in Thai, from the stored snapshots (plan 12 decision 8)', () => {
    const [added, cover, review, phone, exported] = firstPage.entries;
    expect(changeLines(added)).toEqual(['รายการ: – → เข้างาน', 'เวลา: – → 15/10 06:58', 'ที่มา: – → Admin เพิ่ม']);
    // null fields (BuildingId, BlockedScanId) are "–" on both sides, so they are left out
    expect(changeLines(cover)).toEqual([
      'คนทำแทน (รหัสในระบบ): – → 25',
      'Area (รหัสในระบบ): – → 1',
      'วันที่ของกะ: – → 15/10/2026',
      'กะ: – → กะเช้า',
    ]);
    expect(changeLines(review)).toEqual(['คุยแล้ว: ไม่ใช่ → ใช่', 'ประเภทเตือน: – → ลงเวลาไกลจากป้าย']);
    expect(changeLines(phone)).toEqual(['เบอร์เดิม → เบอร์ใหม่ (ไม่แสดงเบอร์)']);
    expect(changeLines(exported)).toEqual([
      'ตั้งแต่: – → 01/10/2026',
      'ถึง: – → 05/10/2026',
      'ชุดข้อมูล: – → การลงเวลา, การส่งงาน',
      'จำนวนแถว: – → 28, 412',
    ]);
    expect(changeLines(olderPage.entries[0])).toEqual(['ออก QR: 01/10 09:00 → 13/10 14:40']);
  });

  it('changeLines leaves out fields that did not change, and shows round windows and Area shifts in Thai', () => {
    expect(
      changeLines({
        action: 'ATTENDANCE_EDIT',
        before: { EventType: 'ShiftOut', OccurredAt: '2026-10-15T11:58:00Z', Source: 'Scan' },
        after: { EventType: 'ShiftOut', OccurredAt: '2026-10-15T11:30:00Z', Source: 'AdminEdit' },
      }),
    ).toEqual(['เวลา: 15/10 18:58 → 15/10 18:30', 'ที่มา: สแกนเอง → แก้โดย Admin']);
    expect(
      changeLines({
        action: 'POINT_UPDATE',
        before: { Name: 'ห้องน้ำชาย', RoundWindows: ['DAY 13:00-15:00'] },
        after: { Name: 'ห้องน้ำชาย', RoundWindows: ['DAY 13:30-15:30', 'NIGHT 20:00-22:00'] },
      }),
    ).toEqual(['ช่วงรอบ: เช้า 13:00-15:00 → เช้า 13:30-15:30, ดึก 20:00-22:00']);
    const area = { Code: 'AR01', Name: 'Area ทดลอง', BuildingId: 1, ShiftPattern: 'DayAndNight', DayCleanerId: 21, NightCleanerId: 22, RemovedNightWindows: null };
    expect(
      changeLines({
        action: 'AREA_UPDATE',
        before: area,
        after: { ...area, ShiftPattern: 'DayOnly', NightCleanerId: null, RemovedNightWindows: 2 },
      }),
    ).toEqual(['กะของ Area: 2 กะ (เช้า + ดึก) → เฉพาะกะเช้า', 'แม่บ้านกะดึก (รหัสในระบบ): 22 → –', 'รอบกะดึกที่ถูกลบ: – → 2']);
    // the server always stores the count after an Area edit; none removed is no change
    expect(changeLines({ action: 'AREA_UPDATE', before: area, after: { ...area, Name: 'Area ใหม่', RemovedNightWindows: 0 } })).toEqual([
      'ชื่อ: Area ทดลอง → Area ใหม่',
    ]);
    expect(
      changeLines({ action: 'COVER_CANCEL', before: { cancelled: false }, after: { cancelled: true, alsoCancelled: [] } }),
    ).toEqual(['ยกเลิกแล้ว: ไม่ใช่ → ใช่']);
  });

  it('changeLines hides secrets and shows nothing when nothing was stored', () => {
    expect(changeLines({ action: 'ACCOUNT_PASSWORD' })).toEqual(['รหัสผ่านเดิม → รหัสผ่านใหม่ (ไม่แสดงรหัสผ่าน)']);
    expect(changeLines({ action: 'SOMETHING_NEW' })).toEqual([]);
    expect(changeLines({ action: 'SOMETHING_NEW', before: { Extra: 1 }, after: { Extra: 2 } })).toEqual(['Extra: 1 → 2']);
  });

  it('searchEntries keeps the rows whose summary, actor, reason or type matches, in any letter case', () => {
    const entries = firstPage.entries;
    expect(searchEntries(entries, '  ').map((entry) => entry.id)).toEqual([50, 49, 48, 47, 46]);
    expect(searchEntries(entries, 'แม่บ้านที่อื่น').map((entry) => entry.id)).toEqual([49]);
    expect(searchEntries(entries, 'ผู้ดูแลคนที่สอง').map((entry) => entry.id)).toEqual([49]);
    expect(searchEntries(entries, 'gps').map((entry) => entry.id)).toEqual([48]);
    expect(searchEntries(entries, 'แก้เบอร์').map((entry) => entry.id)).toEqual([47]);
  });
```

Run: `cd frontend && npx vitest run src/pages/history/historyRules.test.ts`
Expected: FAIL (`changeLines` and `searchEntries` are not exported).

- [ ] **Step 2: Write the rules**

In `frontend/src/pages/history/historyRules.ts`:
- Replace the two import lines with:

```ts
import { readRange, toRangeParams } from '../../shared/utils/dateRange';
import { eventLabel, shiftPatternLabel, shiftShortLabel } from '../../shared/utils/labels';
import { formatDayTime, formatShiftDate } from '../../shared/utils/thaiTime';
import type { AuditCategory, AuditEntry, HistoryArgs } from './historyApi';
```

- Append at the end of the file:

```ts
/** facility-0057: these actions store nothing about the secret; the line says what changed without it. */
const secretChange: Record<string, string> = {
  ACCOUNT_PHONE: 'เบอร์เดิม → เบอร์ใหม่ (ไม่แสดงเบอร์)',
  ACCOUNT_PASSWORD: 'รหัสผ่านเดิม → รหัสผ่านใหม่ (ไม่แสดงรหัสผ่าน)',
};

/** Snapshot fields in Thai (the AuditTrail.Add calls). Ids stay numbers: snapshots keep ids, not names, on purpose. */
const fieldLabel: Record<string, string> = {
  EventType: 'รายการ',
  OccurredAt: 'เวลา',
  Source: 'ที่มา',
  Reviewed: 'คุยแล้ว',
  AlertType: 'ประเภทเตือน',
  IsActive: 'เปิดใช้งาน',
  Name: 'ชื่อ',
  RoundWindows: 'ช่วงรอบ',
  RadiusM: 'รัศมี (ม.)',
  QrIssuedAt: 'ออก QR',
  Latitude: 'ละติจูด',
  Longitude: 'ลองจิจูด',
  LocationAccuracyM: 'ความแม่นยำ (ม.)',
  LocationSource: 'ที่มาของพิกัด',
  Role: 'บทบาท',
  LoginName: 'ชื่อเข้าระบบ',
  DisplayName: 'ชื่อที่แสดง',
  AreaId: 'Area (รหัสในระบบ)',
  BuildingId: 'ตึก (รหัสในระบบ)',
  Shift: 'กะ',
  Code: 'รหัส',
  ShiftPattern: 'กะของ Area',
  DayCleanerId: 'แม่บ้านกะเช้า (รหัสในระบบ)',
  NightCleanerId: 'แม่บ้านกะดึก (รหัสในระบบ)',
  RemovedNightWindows: 'รอบกะดึกที่ถูกลบ',
  UserId: 'คนทำแทน (รหัสในระบบ)',
  ShiftDate: 'วันที่ของกะ',
  BlockedScanId: 'สแกนที่ถูกบล็อก (รหัสในระบบ)',
  cancelled: 'ยกเลิกแล้ว',
  alsoCancelled: 'ยกเลิกพร้อมกัน (รหัสในระบบ)',
  From: 'ตั้งแต่',
  To: 'ถึง',
  Sets: 'ชุดข้อมูล',
  Rows: 'จำนวนแถว',
};

/** Enum values in Thai, by field. */
const valueLabel: Record<string, Record<string, string>> = {
  EventType: eventLabel,
  Source: { Scan: 'สแกนเอง', AdminAdd: 'Admin เพิ่ม', AdminEdit: 'แก้โดย Admin' },
  Shift: shiftShortLabel,
  Role: { Cleaner: 'แม่บ้าน', Supervisor: 'หัวหน้า', Admin: 'Admin' },
  LocationSource: { Site: 'หน้างาน', Map: 'แผนที่' },
  ShiftPattern: shiftPatternLabel,
  AlertType: {
    AttendanceGps: 'ลงเวลาไกลจากป้าย',
    SubmissionGps: 'ส่งงานไกลจากป้าย',
    BlockedScan: 'สแกนที่ถูกบล็อก',
    InspectionGps: 'ตรวจงานไกลจากป้าย',
  },
  Sets: { Attendance: 'การลงเวลา', Submissions: 'การส่งงาน', Inspections: 'การตรวจงาน' },
};

/** AREA_UPDATE always stores how many night windows went; 0 is "none", like the null before it. */
const ZERO_IS_NONE = new Set(['RemovedNightWindows']);

const MOMENT = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?Z$/;
const DATE = /^\d{4}-\d{2}-\d{2}$/;

function valueText(field: string, value: unknown): string {
  if (value == null) {
    return '–';
  }
  if (Array.isArray(value)) {
    return value.length === 0 ? '–' : value.map((item) => valueText(field, item)).join(', ');
  }
  if (typeof value === 'boolean') {
    return value ? 'ใช่' : 'ไม่ใช่';
  }
  if (typeof value === 'number') {
    return value === 0 && ZERO_IS_NONE.has(field) ? '–' : String(value);
  }
  if (typeof value === 'string') {
    const labels = valueLabel[field];
    if (labels && Object.hasOwn(labels, value)) {
      return labels[value];
    }
    if (field === 'RoundWindows') {
      return value.replace(/^DAY /, 'เช้า ').replace(/^NIGHT /, 'ดึก ');
    }
    if (MOMENT.test(value)) {
      return formatDayTime(value);
    }
    return DATE.test(value) ? formatShiftDate(value) : value;
  }
  return JSON.stringify(value);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Wireframe D9's "ก่อน → หลัง" (plan 12 decision 8): one "label: before → after" line per field that shows
 * differently on the two sides, in the order the snapshots list them. An unknown field shows its key.
 */
export function changeLines({ action, before, after }: Pick<AuditEntry, 'action' | 'before' | 'after'>): string[] {
  if (Object.hasOwn(secretChange, action)) {
    return [secretChange[action]];
  }
  if (before == null && after == null) {
    return [];
  }
  const was = before ?? {};
  const now = after ?? {};
  if (!isRecord(was) || !isRecord(now)) {
    return [`${valueText('', before)} → ${valueText('', after)}`];
  }
  const fields = [...new Set([...Object.keys(was), ...Object.keys(now)])];
  return fields
    .map((field) => ({ field, from: valueText(field, was[field]), to: valueText(field, now[field]) }))
    .filter((change) => change.from !== change.to)
    .map((change) => `${Object.hasOwn(fieldLabel, change.field) ? fieldLabel[change.field] : change.field}: ${change.from} → ${change.to}`);
}

/** The search box (plan 12 decision 7): the rows already loaded whose summary, actor, reason or type matches. */
export function searchEntries(entries: readonly AuditEntry[], search: string): AuditEntry[] {
  const text = search.trim().toLowerCase();
  if (text === '') {
    return [...entries];
  }
  return entries.filter((entry) =>
    [entry.summary, entry.actorName, entry.reason, actionText(entry.action)].some((value) =>
      value?.toLowerCase().includes(text),
    ),
  );
}
```

Run: `cd frontend && npx vitest run src/pages/history/historyRules.test.ts`
Expected: PASS (7 tests).

- [ ] **Step 3: Write the failing page tests**

In `frontend/src/pages/history/HistoryPage.test.tsx`, add inside the `describe` block:

```tsx
  it('"ก่อน → หลัง" lists each change, and never a phone number (facility-0057)', async () => {
    serve();
    await renderApp('/admin/history');
    await screen.findByText('แสดง 5 รายการ');

    expect(screen.getByRole('columnheader', { name: 'ก่อน → หลัง' })).toBeInTheDocument();
    const changes = rows().map((cells) => cells[4]);
    expect(within(changes[0]).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'รายการ: – → เข้างาน',
      'เวลา: – → 15/10 06:58',
      'ที่มา: – → Admin เพิ่ม',
    ]);
    expect(within(changes[2]).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'คุยแล้ว: ไม่ใช่ → ใช่',
      'ประเภทเตือน: – → ลงเวลาไกลจากป้าย',
    ]);
    expect(changes[3]).toHaveTextContent('เบอร์เดิม → เบอร์ใหม่ (ไม่แสดงเบอร์)');
  });

  it('search narrows the rows already loaded, and says when none match', async () => {
    serve();
    const { user } = await renderApp('/admin/history');
    await screen.findByText('แสดง 5 รายการ');
    const search = screen.getByRole('searchbox', { name: 'ค้นหา' });

    await user.type(search, 'แม่บ้านที่อื่น');
    expect(mainCells().map((cells) => cells[2])).toEqual(['มอบหมายทำแทน']);
    expect(screen.getByText('แสดง 1 จาก 5 รายการที่โหลดแล้ว')).toBeInTheDocument();

    await user.clear(search);
    await user.type(search, 'ไม่มีคำนี้');
    expect(screen.getByText('ไม่พบรายการที่ตรงกับที่ค้นหา')).toBeInTheDocument();
    expect(screen.getByText('ค้นหาเฉพาะรายการที่โหลดแล้ว')).toBeInTheDocument();
  });
```

Run: `cd frontend && npx vitest run src/pages/history/HistoryPage.test.tsx`
Expected: the two new tests FAIL. The earlier tests still pass: `mainCells` takes the reason from the last cell, so a new column before it changes nothing.

- [ ] **Step 4: The column, the search box and the page**

In `frontend/src/pages/history/HistoryTable.tsx`:
- Change the rules import to `import { actionText, changeLines } from './historyRules';`.
- Replace `<th scope="col">รายการที่ถูกแก้</th>` with:

```tsx
            <th scope="col">รายการที่ถูกแก้</th>
            <th scope="col">ก่อน → หลัง</th>
```

- Replace `<td>{entry.summary}</td>` with:

```tsx
              <td>{entry.summary}</td>
              <td>
                <Changes entry={entry} />
              </td>
```

- Add at the end of the file:

```tsx
function Changes({ entry }: { entry: AuditEntry }) {
  const lines = changeLines(entry);
  if (lines.length === 0) {
    return <>–</>;
  }
  return (
    <ul className="history-changes">
      {lines.map((line) => (
        <li key={line}>{line}</li>
      ))}
    </ul>
  );
}
```

In `frontend/src/pages/history/HistoryControls.tsx`:
- Add to `HistoryControlsProps`:

```tsx
  search: string;
  onSearch: (text: string) => void;
```

- Change the function's first line to:

```tsx
export function HistoryControls({ args, actors, texts, onTexts, onShow, onArgs, search, onSearch }: HistoryControlsProps) {
```

- Right before the closing `</div>` of `history-controls`, add:

```tsx
      <label className="app-field">
        ค้นหา
        <input
          className="app-input history-search"
          type="search"
          value={search}
          placeholder="ค้นหาชื่อคน จุด หรือรหัส"
          onChange={(event) => onSearch(event.target.value)}
        />
        <span className="app-field__help">ค้นหาเฉพาะรายการที่โหลดแล้ว</span>
      </label>
```

In `frontend/src/pages/history/HistoryPage.tsx`:
- Change the rules import to `import { HISTORY_MAX_DAYS, readHistoryArgs, searchEntries, toHistoryParams } from './historyRules';`.
- Below the `problem` state, add `const [search, setSearch] = useState('');`.
- Below the `entries` line, add `const shown = searchEntries(entries, search);`.
- Replace the `HistoryControls` line with:

```tsx
      <HistoryControls
        args={args}
        actors={actors}
        texts={texts}
        onTexts={setTexts}
        onShow={show}
        onArgs={go}
        search={search}
        onSearch={setSearch}
      />
```

- Replace:

```tsx
          <HistoryTable entries={entries} emptyText="ไม่มีประวัติในช่วงนี้" />
          <div className="history-more">
            <span className="app-muted">{`แสดง ${entries.length} รายการ`}</span>
```

with:

```tsx
          <HistoryTable
            entries={shown}
            emptyText={entries.length > 0 ? 'ไม่พบรายการที่ตรงกับที่ค้นหา' : 'ไม่มีประวัติในช่วงนี้'}
          />
          <div className="history-more">
            <span className="app-muted">
              {shown.length === entries.length
                ? `แสดง ${entries.length} รายการ`
                : `แสดง ${shown.length} จาก ${entries.length} รายการที่โหลดแล้ว`}
            </span>
```

In `frontend/src/pages/history/history.css`, below `.history-reason`, add:

```css
.history-changes {
  margin: 0;
  padding-left: 18px;
}

.history-search {
  width: 240px;
}
```

- [ ] **Step 5: Run the tests to make sure they pass**

Run: `cd frontend && npx vitest run src/pages/history`
Expected: PASS.

- [ ] **Step 6: Whole frontend check**

Run: `cd frontend && npm run lint && npx tsc -b && npm test`
Expected: clean; 372 tests pass (366 + 4 + 2).

Run: `cd frontend && grep -rn "dangerouslySetInnerHTML\|style={" src/pages/history src/pages/export src/pages/inspection-summary src/shared/components/RangeFields.tsx src/shared/components/BuildingSelect.tsx`
Expected: no output.

- [ ] **Step 7: Handoff entry and commit**

Add under `## Task log (plan 12)`:

```markdown
- <yyyy-MM-dd> · Task 5 done: D9's "ก่อน → หลัง": one Thai line per changed field from the stored snapshots (events, sources, shifts, roles, Area shift patterns, alert types, sets, ใช่/ไม่ใช่; times via `formatDayTime`, dates via `formatShiftDate`, round windows "เช้า 07:00-09:00"; ids stay numbers; unknown fields show their key; `RemovedNightWindows` 0 counts as none); phone and password changes say so without the value. Search over the loaded rows (summary, actor, reason, type) with "แสดง n จาก m รายการที่โหลดแล้ว". Frontend tests <n> passed. Next: Task 6 (docs, final checks, the controller's real run).
```

```bash
git add docs/handoff.md frontend/src/pages/history
git commit -m "feat(frontend): D9 shows before and after per field, and searches the loaded rows (facility-0057)"
```

---

### Task 6: Docs, ADR amendments, final checks, the controller's real run

**Files:**
- Modify: `docs/adr/facility-0025-frontend-target-tree.md`, `facility-0026-frontend-router-shape.md`, `facility-0048-supervisor-assigned-to-building.md`, `facility-0053-excel-export-instead-of-report-pages.md`, `facility-0057-supervisor-building-page-and-audit-log-page.md`, `facility-0062-phone-bundle-budget-200kb-gzip.md`
- Modify: `docs/architecture.md`, `frontend/README.md`, `README.md`, `docs/handoff.md`

**Interfaces:**
- Consumes: everything built in Tasks 1–5.
- Produces: docs only. No code changes, unless a final check fails.

- [ ] **Step 1: Measure the main file**

Run: `cd frontend && npm run build && grep -o 'assets/index-[A-Za-z0-9_-]*\.js' dist/index.html && ls dist/assets/*.js`
Note the gzip size of the main file (the one `dist/index.html` names) from the build report, and the list of page files. Then run `rm -rf dist`. The number goes into Step 2 (facility-0062) and Step 6 (handoff), where this plan writes `<size>`.

- [ ] **Step 2: ADR amendments**

Append one line at the end of each ADR below, as its own paragraph (a blank line before it). Use the editor tools, not a shell heredoc, because the text is Thai. Replace `<วันที่>` with the commit's date (yyyy-MM-dd), and `<size>` with the number from Step 1.

`docs/adr/facility-0048-supervisor-assigned-to-building.md`:

```markdown
> **แก้ไข <วันที่> (แผน 12):** หน้าสรุปการตรวจ (ข้อ 5) อยู่ที่ `/admin/inspection-summary` ในเมนู Admin ("สรุปการตรวจ") แสดงตัวเลขตามที่เซิร์ฟเวอร์นับ ไม่มีเกณฑ์หรือสีตัดสิน (ตัดแถบสีส้มต่ำกว่า 30% และตัวหนาเมื่อธง GPS ถึง 5 ของ wireframe ออก) เพิ่มคอลัมน์ "ตรวจทั้งหมด" ตามข้อ 5 ที่ wireframe ไม่มี สัดส่วนที่ตรวจคืองานที่ตรวจแล้วหารงานที่ส่ง ปัดเป็นจำนวนเต็ม แสดง "–" เมื่อไม่มีงานส่ง ช่วงวันพิมพ์แบบ วว/ดด/ปปปป มีปุ่มลัด วันนี้ · 7 วัน · เดือนนี้ เลือกได้ไม่เกิน 31 วัน ช่วงวันและตึกอยู่ใน URL
```

`docs/adr/facility-0053-excel-export-instead-of-report-pages.md`:

```markdown
> **แก้ไข <วันที่> (แผน 12):** หน้าส่งออก Excel อยู่ที่ `/admin/export` ในเมนู Admin และมีลิงก์ "ส่งออก Excel" จาก Dashboard, หน้าการเข้างาน (วันที่ของกะและตึกที่แสดง) และหน้าสรุปการตรวจ (ช่วงวันและตึกที่แสดง) เลือกชุดข้อมูล (ค่าเริ่ม การลงเวลา + การส่งงาน) ช่วงวัน 1–31 วัน และตึก แล้วกด "ดาวน์โหลด .xlsx" เบราว์เซอร์บันทึกไฟล์ด้วยชื่อที่เซิร์ฟเวอร์ตั้ง (`facility_<from>_<to>.xlsx`) ไฟล์ไม่ผ่าน store ของหน้า หน้าไม่แสดงจำนวนแถวก่อนดาวน์โหลด เพราะ API ไม่ส่งมา
```

`docs/adr/facility-0057-supervisor-building-page-and-audit-log-page.md`:

```markdown
> **แก้ไข <วันที่> (แผน 12):** แท็บ "ประวัติ Admin" (ข้อ 2) อยู่ที่ `/admin/history` ในเมนู Admin กรองช่วงวัน (ไม่เกิน 92 วัน) ผู้ทำ และประเภท ค่าที่เลือกอยู่ใน URL เรียงใหม่ไปเก่า ครั้งละ 100 รายการ กด "โหลดรายการเก่ากว่านี้" เพื่อดูหน้าที่เก่ากว่า ช่อง "ก่อน → หลัง" สร้างจาก snapshot ที่เก็บไว้ บรรทัดละหนึ่งช่องที่เปลี่ยน เป็นภาษาไทยเมื่อรู้จักช่องนั้น รหัสในระบบ (เช่น Area, ตึก) แสดงเป็นตัวเลขเพราะ snapshot เก็บรหัส ไม่เก็บชื่อ การแก้เบอร์โทรและรีเซ็ตรหัสผ่านแสดงว่าเปลี่ยนโดยไม่แสดงค่า ช่องค้นหาค้นเฉพาะรายการที่โหลดแล้ว เพราะ API ยังไม่มีการค้นหา
```

`docs/adr/facility-0062-phone-bundle-budget-200kb-gzip.md`:

```markdown
> **วัด <วันที่> (แผน 12):** หน้าสรุปการตรวจ หน้าส่งออก Excel และหน้าประวัติ Admin เป็น `lazy` route แยกไฟล์ของตัวเอง ไฟล์หลักที่ `index.html` โหลดวัดได้ <size> kB gzip
```

`docs/adr/facility-0026-frontend-router-shape.md`:

```markdown
> **แก้ไข <วันที่> (แผน 12):** เพิ่ม `/admin/inspection-summary` (D7, `?from=yyyy-MM-dd&to=yyyy-MM-dd&building=<id>`), `/admin/export` (D8, รับ `?from&to&building` มาเติมฟอร์ม) และ `/admin/history` (D9, `?from&to&actor=<id>&category=<AuditCategory>`) ใต้ guard ของ Admin เป็น `lazy` route ทั้งสามหน้า
```

`docs/adr/facility-0025-frontend-target-tree.md`:

```markdown
> **แก้ไข <วันที่> (แผน 12):** เพิ่ม `pages/inspection-summary` (D7), `pages/export` (D8), `pages/history` (D9), `shared/utils/dateRange.ts` (ช่วงวันของหน้ารายงาน), `shared/components/RangeFields.tsx` (ช่องตั้งแต่ / ถึง และปุ่มลัด) และ `shared/components/BuildingSelect.tsx`
```

- [ ] **Step 3: Architecture**

In `docs/architecture.md`, line 113, replace:

```text
· print-signs (D5) · accounts (D6)<br/>
```

with:

```text
· print-signs (D5) · accounts (D6) · inspection-summary (D7) · export (D8) · history (D9)<br/>
```

(Only that fragment of the line changes.)

- [ ] **Step 4: Frontend README**

In `frontend/README.md`:
- Replace the line:

```text
src/shared/components/                   guard, layout, Admin menu, status and crash screens (class prefix app-)
```

with:

```text
src/shared/components/                   guard, layout, Admin menu, status and crash screens, RangeFields, BuildingSelect (class prefix app-)
src/shared/utils/                        Thai time, labels, error wording, dateRange (the report pages' date ranges)
```

- At the end of the `## Tests` section, add this paragraph:

```markdown
Made-up report answers (inspection summary, audit log pages, actors) are in `src/test/reportFixtures.ts`. A file answer is `{ status: 200, file: { content, type, disposition } }` in `fakeServer`. The export's download is checked with `fakeDownload()` from `src/test/fakeDownload.ts`, which records each file the page saves (`saved[i].fileName`, `saved[i].blob`). Pages that start on today's date are tested with `thaiToday()` rather than fake timers.
```

- [ ] **Step 5: README**

In `README.md`, in the line that starts `  - Admin เข้าที่ \`/login/admin\``, replace:

```text
และ "บัญชีผู้ใช้" (`/admin/accounts`) หน้า Admin โหลดแยกจากหน้ามือถือ
```

with:

```text
"บัญชีผู้ใช้" (`/admin/accounts`), "สรุปการตรวจ" (`/admin/inspection-summary`: การตรวจของหัวหน้าแต่ละคนตามช่วงวัน), "ส่งออก Excel" (`/admin/export`: ดาวน์โหลด .xlsx การลงเวลา การส่งงาน การตรวจงาน) และ "ประวัติ Admin" (`/admin/history`: ทุกการแก้ของ Admin พร้อมก่อน → หลังและเหตุผล อ่านอย่างเดียว) หน้า Admin โหลดแยกจากหน้ามือถือ
```

- [ ] **Step 6: Handoff**

In `docs/handoff.md`:
- In `## Notes for the frontend plan`, replace the line that starts `- Admin pages (plans 10 and 11): add a plan-12 page as a \`lazy\` route` with:

```markdown
- Admin pages (plans 10–12): add a page as a `lazy` route under the Admin guard in `router.tsx`, its path to the lazy-route list in `router.test.tsx`, and its link to `shared/components/AdminNav.tsx` if it belongs in the menu. Admin setup data uses the `AdminSetup` tag; the live board uses `ServicePoints`, `AttendanceBoard` (one per shift) and `Alerts`; the report pages (plan 12) use no tag and ask on every open. `src/test/adminFixtures.ts`, `src/test/liveFixtures.ts` and `src/test/reportFixtures.ts` have made-up Admin answers.
```

- Replace the line that starts `- Dates for plan 12:` with:

```markdown
- Date ranges (plan 12): `shared/utils/dateRange.ts` (`checkRange` with the server's limits, `readRange`, `toRangeParams`, `exportLink`) and `shared/components/RangeFields.tsx` (typed dd/mm/yyyy with the shortcuts). `thaiToday` is in `shared/utils/thaiTime.ts`.
```

- Replace the line that starts `- Bundle after plan 11:` with:

```markdown
- Bundle after plan 12: all the JavaScript a phone loads from `index.html` is <size> kB gzip of 200 kB. Admin pages, the three report pages included, are their own files.
```

- In `## Pending work (ask the owner before starting)`, add:

```markdown
- Server-side search for the Admin history (owner, 2026-10-21): a text filter on `/api/admin/audit-log` (summary, actor, reason), so D9's search box covers every row in the range, not only the rows loaded (plan 12 decision 7). Its own branch and PR, after plan 12.
```

- In `## Parked; not needed before merging`, add:

```markdown
- D8 shows no row counts before the download; the API would need a dry-run endpoint (plan 12 decision 5).
```

- Add the Task 6 entry under `## Task log (plan 12)`:

```markdown
- <yyyy-MM-dd> · Task 6 done: ADR amendments (0025 tree, 0026 routes, 0048 summary page, 0053 export page, 0057 history page, 0062 measured size <size> kB gzip), architecture, README, frontend README, handoff notes. Frontend <n> tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
```

- [ ] **Step 7: Final checks**

Run each, from the repo root unless it says otherwise:

```bash
cd frontend && npm run lint && npx tsc -b && npm test && cd ..
dotnet test backend/FacilityRealtime.slnx
git diff --stat ca59320 -- backend   # expect no output: no backend file changed
grep -rn "toLocale" frontend/src --include='*.ts' --include='*.tsx' | grep -v thaiTime.ts   # expect no output
grep -rn "localStorage\|sessionStorage\|indexedDB" frontend/src --include='*.ts' --include='*.tsx'   # expect no output
grep -rn "/scan/\|/check-in/" frontend/src/pages/inspection-summary frontend/src/pages/export frontend/src/pages/history   # expect no output
```

Expected: frontend 372 tests (if a task added more, say how many); unit 299; API 531 passed, 5 skipped.

- [ ] **Step 8: Commit the docs**

```bash
git add docs/adr/facility-0025-frontend-target-tree.md docs/adr/facility-0026-frontend-router-shape.md \
  docs/adr/facility-0048-supervisor-assigned-to-building.md docs/adr/facility-0053-excel-export-instead-of-report-pages.md \
  docs/adr/facility-0057-supervisor-building-page-and-audit-log-page.md docs/adr/facility-0062-phone-bundle-budget-200kb-gzip.md \
  docs/architecture.md frontend/README.md README.md docs/handoff.md
git commit -m "docs: plan 12 ADR amendments, architecture, READMEs and handoff"
```

- [ ] **Step 9: The controller's real run (subagents skip this step)**

This step needs MySQL, so the controller runs it, not a subagent.

1. Create the throwaway database `facility_reports_test` (Homebrew MySQL on this Mac; if root has a password, ask the owner for it and type it only into the command).
2. Run `npm run build:api` in `frontend/`. Note the main JS size.
3. In the shell that runs the API, set `ConnectionStrings__DefaultConnection` to `facility_reports_test`, `ASPNETCORE_ENVIRONMENT=Development` and `ASPNETCORE_URLS=http://localhost:5001`, then run `dotnet run --no-launch-profile --project backend/src/FacilityRealtime.Api`. The Development seed fills the database.
4. Run a script (Python with `-X utf8`, or Node) as the seed Admin:
   - `GET /api/admin/inspection-summary` with no query: 200, `from`/`to` are the 7 dates ending today, and every active seed Supervisor has a row.
   - The same with `from`/`to` 32 dates apart: 400 "เลือกได้ไม่เกิน 31 วัน".
   - `POST /api/admin/export` with today, all three sets:
     - 200
     - `Content-Disposition` names `facility_<today>_<today>.xlsx`
     - the body starts with `PK` (a zip)
   - `GET /api/admin/audit-log`: the first entry is `EXPORT_EXCEL` with category `Export`, and its `after` has `From`, `To`, `Sets`, `Rows`.
   - `GET /api/admin/audit-log?limit=1`: `nextBeforeId` is set. Then `?limit=1&beforeId=<it>` gives the next older entry.
   - `GET /api/admin/audit-log/actors` lists the seed Admin.
   - A Cleaner's token gets 403 on all four endpoints.
   - The API log has no `warn` or `fail`.
5. Write down the results in the handoff (an entry under `## Task log (plan 12)`), then give the owner these browser checks (Chrome, an Incognito window, `http://localhost:5001`, the seed Admin):
   1. The menu reads สถานะจุด, การเข้างาน, รายการที่ขึ้นเตือน, สรุปการตรวจ, ส่งออก Excel, จัดการ Area และป้าย, บัญชีผู้ใช้, ประวัติ Admin. Opening `/dashboard` loads no report page file (Network tab).
   2. สรุปการตรวจ: the heading shows the 7 days. "เดือนนี้" then "แสดง" changes the range and the address. The building picker narrows the rows. The "ส่งออก Excel" button opens D8 with the same range.
   3. ส่งออก Excel: download with all three sets for this week. The file opens in Excel with sheets การลงเวลา, การส่งงาน, การตรวจงาน. The page says "ดาวน์โหลดแล้ว: …".
   4. ประวัติ Admin: the export from check 3 is the first row, with ก่อน → หลัง listing ตั้งแต่, ถึง, ชุดข้อมูล, จำนวนแถว. Make one attendance correction on D2 with a reason, reopen ประวัติ Admin, and it shows with เวลา / ที่มา and the reason. The ประเภท and ผู้ทำ pickers narrow the list. Search narrows the loaded rows.
   5. D2's "ส่งออก Excel" opens D8 on that shift date.
   6. Phone width (DevTools, 390 px): the three pages scroll their tables sideways and nothing overflows the page.
6. Afterwards: stop the API, drop `facility_reports_test`, and delete `backend/src/FacilityRealtime.Api/wwwroot`.
7. Then run the whole-branch review (sp-subagent-driven-development). After it, ask the owner's OK to push and open the PR. The PR description lists the decisions at the top of this plan.

---

## Self-review (done while planning)

**Spec coverage:**
- facility-0048 rule 5: the building and shift, "จำนวนครั้งที่ตรวจ" (ตรวจทั้งหมด), points not inspected ("9 จาก 12 จุด · ไม่ได้ตรวจ 3 จุด"), ไม่ได้ตรวจ, reworks and GPS flags, with a choosable range defaulting to 7 days: Task 2.
- facility-0053: the three sets, the range, the building, no raw GPS (the server's file; the page's notes say so), and the export logged (server): Task 3. D9 shows the export row: Tasks 4 and 5.
- facility-0057 rule 2:
  - Admin only: Admin guard, lazy route.
  - Read-only: no action on the page.
  - Range, actor and type filters: Task 4.
  - Before and after with the reason: Tasks 4 and 5.
  - No phone or password: the server stores none, and Task 5 shows the secret line.
- facility-0062: lazy routes in Tasks 2–4; measured in Tasks 2, 3, 4 and 6.
- facility-0059, 0063, 0066, 0068: Global Constraints, checked in Task 5 Step 6 and Task 6 Step 7.
- The handoff's plan-12 notes:
  - menu links: Tasks 2–4
  - `formatShiftDate`, `parseShiftDate`, `formatDayTime`: used throughout
  - "ส่งออก Excel" links on D1 and D2: Task 3
  - `notInspected` is not submitted − inspected: Task 2, decision 4
  - the empty last page of history paging: Task 4

**Placeholders:** `<yyyy-MM-dd>`, `<n>`, `<size>` and `<วันที่>` in handoff and ADR lines are values the executor measures or dates at run time, and each step says where they come from. No step leaves code to invent.

**Type consistency:**
- `DateRange`, `RangeTexts` and `checkRange(texts, maxDays)` are used the same way in Tasks 1–5.
- `RangeFields({ value, onChange, onEnter? })` is used in D7, D8 and D9.
- `BuildingSelect({ value?, onChange })` is used in D7 and D8.
- `ExportSet` lives in `exportRules.ts`, and `exportApi.ts` imports it (no cycle).
- `AuditEntry`, `HistoryArgs` and `AuditCategory` live in `historyApi.ts`, and `historyRules.ts` imports their types.
- `HistoryTable({ entries, emptyText })` keeps its props in Task 5. `HistoryControls` gains `search` and `onSearch` in Task 5, and its only caller (`HistoryPage`) is updated in the same step.
- Test counts: 320 → 329 (T1) → 340 (T2) → 354 (T3) → 366 (T4) → 372 (T5).
