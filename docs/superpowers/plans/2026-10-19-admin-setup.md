# Admin Setup Implementation Plan (plan 10)

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An Admin can set up the whole site from the browser, with no code and no SQL. They add and edit Areas, points and round windows, set each sign's radius and location (captured at the sign on a phone, or typed from a map), issue a new QR, print the signs, and manage every account.

**Architecture:**
- **Five Admin pages on the API that plans 3 and 5 built.** No file under `backend/` changes.
  - `pages/areas/` (D4) shows the Area list, the chosen Area's signs, the point editor and the sign settings.
  - `pages/area-form/` (D10) adds or edits an Area.
  - `pages/capture/` (M17) is the Admin's phone page. It reads the GPS for 30 s at the sign and saves the median.
  - `pages/print-signs/` (D5) prints A4 sheets of QR signs, with the QR drawn on the computer.
  - `pages/accounts/` (D6) holds the accounts table and the add, edit, phone, password and deactivate panels.
- **Shared pieces.**
  - `shared/api/adminSetupApi.ts` holds the reads that several Admin pages use (Areas, one Area, buildings, Cleaners) and the sign-location save, which the D4 page and M17 both use.
  - Every Admin query provides the one cache tag `AdminSetup`, and every Admin save invalidates it.
  - `shared/components/AdminNav.tsx` is the Admin menu. `AppLayout wide` shows it.
- **The QR is drawn as SVG in React** from `uqr`'s module matrix. It needs no web service (facility-0021) and no raw HTML (facility-0066).

**Tech Stack:** React 19.2, TypeScript 6.0, Vite 8.3, `@reduxjs/toolkit` 2.13.0, `react-router` 7.18.4, Vitest 5.0 on jsdom 30.1.2, Testing Library, eslint 9 with boundaries. **One new package: `uqr` 0.1.3** (MIT, no dependencies, about 8 kB gzip), for the QR modules.

**Spec:** this plan implements the Admin setup side of these ADRs, with wireframes D4, D5, D6, D10 and M17 for the screens. Where a wireframe and an ADR disagree, the ADR wins.
- `docs/adr/facility-0045-admin-manages-areas-and-captures-sign-location.md`:
  - The Admin creates Areas (name, building, shift pattern, one Cleaner per shift), points (name, round times per shift), and the location and radius of every sign.
  - A location is captured standing at the sign: 30 s of GPS, the median, with its accuracy, and a warning when the accuracy is worse than the radius.
  - A map pick is the fallback and shows "ยังไม่ยืนยันหน้างาน".
- `docs/adr/facility-0021-points-admin-page.md`: deactivate instead of delete; a new QR goes through one confirmation and then offers to print; signs are printed one at a time or all together, on A4 with 6 per sheet. Item 2 was replaced by 0042 and 0045.
- `docs/adr/facility-0020-accounts-page-operations.md`, with its amendments: add, edit, deactivate and activate (no delete); Cleaners and Supervisors log in with employee ID + phone, so "แก้เบอร์โทร" is for them and "รีเซ็ตรหัสผ่าน" is for Admins; no self-deactivation.
- `docs/adr/facility-0054-login-with-employee-id-and-phone.md`, `facility-0056-accounts-created-one-by-one-excel-import-before-rollout.md` (one by one for the pilot; about 180 rows filtered on the page, no paging).
- `docs/adr/facility-0038-default-radius-50m-per-sign.md`: the radius is editable per sign. `facility-0040`, `0042`, `0047`: shift patterns, round windows.
- `docs/adr/facility-0060-admin-gets-qr-token-from-points-page-only.md`: the QR Token reaches the browser only on the Admin's Area and print pages.
- `docs/adr/facility-0006-mysql-schema-and-qr-token.md`: a new QR makes the old sign useless at once; scan history is kept.
- `docs/adr/facility-0062-phone-bundle-budget-200kb-gzip.md`: one file, at most 200 kB gzip; Admin pages become lazy routes only if the build goes over.
- `docs/adr/facility-0025`, `0026`, `0063`, `0065`, `0066`, `0067`, `0068`, as in plans 7–9: tree, route table, CSS, lint boundaries, text rendering, one origin, failure minimum.
- Plans 3 and 5 (`docs/superpowers/plans/2026-10-09-admin-setup-backend.md`, `2026-10-14-accounts-and-excel-export-backend.md`) built the endpoints this plan calls. `docs/handoff.md` "Notes for the frontend plan" has the API quirks.
- Wireframes (`docs/design/wireframe/`): `PointsAdmin.dc.html` (D4), `PrintSigns.dc.html` (D5), `Accounts.dc.html` (D6), `AreaEdit.dc.html` (D10), `CaptureLocation.dc.html` (M17).

**The API this plan uses (unchanged; read the C# if in doubt):**

| Call | Answer | Source |
|---|---|---|
| `GET /api/admin/areas` | `AreaSummaryDto[]` by code, deactivated included: `{ id, code, name, buildingId, buildingCode, shiftPattern, isActive, dayCleaner?, nightCleaner?, activePointCount, signsNotConfirmedOnSite }` | `Endpoints/AdminAreaEndpoints.cs`, `AdminSetupView.cs`, `DTOs/AdminSetupDtos.cs` |
| `GET /api/admin/areas/{id}` | `AreaDetailDto { area, checkInSign, points[] }`. A point is `{ id, name, sortOrder, isActive, sign, roundWindows[] }`, a window is `{ id, shift, start, end }` with `"HH:mm"`. A sign is `{ id, code, qrToken, qrIssuedAt, latitude?, longitude?, locationAccuracyM?, locationSource?, locatedAt?, radiusM }`. 404 `{ message }` | same |
| `GET /api/admin/buildings` | `BuildingDto[] { id, code, name }`, active only | same |
| `GET /api/admin/cleaners` | `CleanerOptionDto[] { id, employeeId, displayName, shift?, areaId?, areaCode? }`, active Cleaners | same |
| `POST /api/admin/areas` `{ code, name, buildingId, shiftPattern, dayCleanerId, nightCleanerId }` | 201 `AreaDetailDto`; 400 / 409 `{ message }` | same |
| `PUT /api/admin/areas/{id}` `{ name, buildingId, shiftPattern, dayCleanerId, nightCleanerId }` | 200 `AreaDetailDto`. A null Cleaner takes that shift's Cleaner off. Day-only removes the night windows | same |
| `POST /api/admin/areas/{id}/deactivate` · `/activate` | 200 `AreaDetailDto` | same |
| `POST /api/admin/areas/{areaId}/points` · `PUT /api/admin/points/{id}` `{ name, roundWindows: [{ shift, start, end }] }` | 201 / 200 `AdminPointDto`. The list is required (`[]` for none); the server checks overlap and shift bounds; 400 `{ message }` | `Endpoints/AdminPointEndpoints.cs` |
| `POST /api/admin/points/{id}/deactivate` · `/activate` | 200 `AdminPointDto` | same |
| `PUT /api/admin/signs/{id}/location` `{ latitude, longitude, accuracyM?, source: 'Site' \| 'Map' }` | 200 `AdminSignDto`; 400; 409 for a map pick on a sign already confirmed on site | `Endpoints/AdminSignEndpoints.cs` |
| `PUT /api/admin/signs/{id}/radius` `{ radiusM }` | 200 `AdminSignDto`; 400 outside 10–500 | same |
| `POST /api/admin/signs/{id}/regenerate-token` | 200 `AdminSignDto` with the new `qrToken`; 409 for a deactivated point or Area | same |
| `GET /api/admin/accounts` | `AccountDto[]`, Admins, then Supervisors, then Cleaners: `{ id, role, loginName, displayName, areaId?, areaCode?, buildingId?, buildingCode?, shift?, isActive, createdAt }` | `Endpoints/AdminAccountEndpoints.cs`, `AccountChecks.cs`, `DTOs/AccountDtos.cs` |
| `POST /api/admin/accounts` | 201 `AccountDto`. Cleaner `{ role, displayName, employeeId, phone, shift, areaId? }`; Supervisor `{ role, displayName, employeeId, phone, buildingId, shift }`; Admin `{ role, displayName, username, password }`. **A field of another role is a 400.** 409 for a taken login name or slot | same |
| `PUT /api/admin/accounts/{id}` `{ displayName, areaId?, buildingId?, shift? }` | 200 `AccountDto`. It replaces every place field: leaving `areaId` out clears a Cleaner's Area | same |
| `POST /api/admin/accounts/{id}/deactivate` · `/activate` | 200 `AccountDto`. Deactivating a Cleaner frees their Area; deactivating yourself is a 400; activating into a taken slot is a 409 | same |
| `PUT /api/admin/accounts/{id}/phone` `{ phone }` · `/password` `{ password }` | 204. Every session of that person is revoked | same |

JSON is camelCase. Enums are strings: `Cleaner`, `Supervisor`, `Admin`, `DayAndNight`, `DayOnly`, `Site`, `Map`, `Day`, `Night`. Times are UTC ISO strings ending in `Z`. **Null fields are left out.** For example, an Area with no night Cleaner has no `nightCleaner` key, and a sign with no location has no `latitude` key.

**Where this plan sits:** the frontend in five plans (table in plan 7). This is plan 10. Plan 11 (Admin live board) fills the Dashboard and adds its pages to the Admin menu.

## Global Constraints

- **Branch:** `feat/admin-setup`, created from `master` at `84890df` (PR #58 merged). Do not merge it.
- **Machine:** the Mac. zsh, Node v26.8.2, npm 11.19.1, .NET SDK 10.0.401.
  - Frontend commands run in `frontend/`. Backend commands run from the repo root.
  - Write files that hold Thai text with the editor tools, not shell heredocs.
- **One new package:** `uqr@0.1.3`, installed in Task 6 only. No other package; `package.json` changes nowhere else. Base UI is still not needed.
- **API unchanged.** No file under `backend/` changes. Where a wireframe shows something the API does not send (a sign's floor, the alert count in the menu), leave it out.
- **Bundle:** the JavaScript of `npm run build` stays ≤ 200 kB gzip in one file (facility-0062). Plan 9 measured 154.83 kB. Tasks 6 and 10 measure it. If it goes over, stop and report to the controller; do not split it on your own.
- **Styles:**
  - No `style={...}` anywhere in `src/` (facility-0063).
  - Page prefixes are `areas-`, `areaform-`, `capture-`, `print-` and `accounts-`. Shared components use `app-`.
- **Text people type and server text** (names, codes, messages) are rendered as React text only. No `dangerouslySetInnerHTML`, and no `href` or `src` built from typed text (facility-0066). Sign URLs are built from the server's `qrToken`, not from typed text.
- **QR Token (facility-0060):** it reaches the browser only through `GET /api/admin/areas/{id}` and the sign answers, on the Areas, capture and print pages. It is shown only inside the printed sign's URL, never as its own text and never in the console.
- **Secrets:** a phone or password the Admin types lives only in that form's `useState`. It is cleared after a successful send. It never goes into a URL, a notice, the console or the store.
- **Nothing is stored on the device:** no `localStorage`, `sessionStorage` or IndexedDB (facility-0068).
- **Thai time:** every time and date on screen goes through `shared/utils/thaiTime.ts` (`formatTime`, and the new `formatDate`). Never call `toLocaleString`, `toLocaleTimeString` or `toLocaleDateString` elsewhere.
- **Server text is shown as is.** `getErrorMessage` shows the API's `{ message }`. Pages add their own text only for checks made before sending (facility-0022 item 4).
- **Missing fields:** a field the server may send as null arrives missing.
  - Types mark such fields optional (`nightCleaner?: AssignedCleaner`).
  - Code tests them with truthiness or `== null`, never `=== null`.
  - Test fixtures leave them out, the way the server does.
- **One send at a time:** while a save is in flight, its button is disabled.
- **Tests:**
  - Vitest on jsdom. Only browser seams are faked:
    - `fetch`, with `src/test/fakeServer.ts`.
    - `navigator.geolocation`, with `src/test/fakeGps.ts`, which gains `fakeGpsTrack` for `watchPosition` in Task 7.
    - `window.print`, with `vi.spyOn(window, 'print')`.
    - The SignalR package, as before, through `test.alias`.
  - No MSW, no `vi.mock`, and no module mocks of our own code (regression-net #32).
  - Fake timers are used only in the capture tests (Task 7), and each such test file restores real timers in `afterEach`.
  - Find elements by role, label or text the way a person would, never by CSS class.
  - The made-up Admin session is account id 7, `ผู้ดูแลทดสอบ` (`authAnswer('admin')`). Admin pages do not print the Admin's name, because the bar already does and a second copy breaks `getByText('ผู้ดูแลทดสอบ')`. The accounts table is the exception, and its tests look inside rows.
- **Copy:** Thai UI text comes from the wireframes. Where this plan gives a string, use it exactly, because tests match it.
- **Public repo:** made-up names only (`ผู้ดูแลทดสอบ`, `แม่บ้านทดสอบ`, `แม่บ้านกะดึก`, `หัวหน้าทดสอบ`, `Area ทดลอง`, `ตึกทดลอง`), employee IDs like `650001`, and phones like `0810000009`. No secret in any file.
- **MySQL** is used only in Task 10's controller run.
  - Use only throwaway databases `facility_*_test`. Never touch `facility_dashboard`.
  - Ask the owner for the root password each time. Type it only into the command, never into a file or into memory.
  - Subagents skip that step.
- **Handoff after every task (owner's rule):**
  - The last step of every task adds one entry to a new section `## Task log (plan 10)` in `docs/handoff.md`, placed just above `## Pending work`. It is committed with the task.
  - The entry is in English: the date, the task, what changed, the test result, and what comes next. No secret, phone number or real name.
- **Commits** end with the attribution trailer your session gives you. The husky pre-commit runs frontend lint, tsc and tests on every commit. If it fails, fix the cause. Never use `--no-verify`.
- **Baselines at `84890df`:** frontend 157 tests pass (16 files); backend 299 unit tests pass; API tests 531 pass and 5 are skipped (the MySQL ones).

### Decisions made while planning (report them to the owner in the PR)

1. **Five pages and their routes**, all behind the Admin guard (facility-0026):
   - `/admin/areas` and `/admin/areas/:areaId` open D4. Without an id, the first Area is shown.
   - `/admin/areas/new` and `/admin/areas/:areaId/edit` open D10.
   - `/admin/areas/:areaId/capture` opens M17. It is the one Admin page in the phone column (`AppLayout` without `wide`), because the Admin holds a phone at the sign.
   - `/admin/print?area=<id>`, `?area=<id>&sign=<signId>` and `?area=all` open D5.
   - `/admin/accounts` opens D6.
   - The Dashboard stays at `/dashboard` (plan 7).
2. **The Admin menu** (`shared/components/AdminNav.tsx`) is shown by `AppLayout wide`. It lists only pages that exist: สถานะจุด, จัดการ Area และป้าย, บัญชีผู้ใช้. Plan 11 adds การเข้างาน, รายการที่ขึ้นเตือน (with its count), สรุปการตรวจ, ส่งออก Excel and ประวัติ Admin with their pages. The bar and the menu are hidden when printing.
3. **One cache tag, `AdminSetup`.** Areas, signs, Cleaners and accounts depend on each other: deactivating a Cleaner frees an Area slot, and an Area edit moves a Cleaner. So every Admin query provides the tag and every Admin save invalidates it. The data is small (about 72 Areas, about 180 accounts).
4. **QR drawn on the computer** with `uqr` 0.1.3. It runs at error correction M, with the standard 4-module quiet zone, as one SVG `<path>` written by React. There is no web service (facility-0021) and no raw HTML string (facility-0066). The sign URL is `<origin>/check-in/<token>` or `<origin>/scan/<token>` (plan 8). The origin is the printing page's own origin (facility-0067). The print page shows that origin and warns when it is not https, because a phone at the sign cannot read its location over plain http.
5. **The map fallback is a typed coordinate, not a map** (facility-0045 item 3). The Admin pastes "13.756300, 100.501800" copied from a map app, and it is saved as `source: 'Map'`, shown as "ยังไม่ยืนยันหน้างาน". This avoids a map library (about 40 kB gzip, over budget with the rest), and the browser asks no tile server about the factory. The field is hidden once the sign is confirmed on site, because the server refuses a map pick then (409).
6. **Sign settings are three small forms, each with its own button** (location, radius, QR), instead of D4's single "บันทึก". Each one maps to one endpoint, so a save can never be half done. The point's name and windows are one form (one endpoint).
7. **Confirmations:**
   - A new QR asks once in the page (`role="alertdialog"`, facility-0021 item 5). Then it offers "พิมพ์ป้ายใหม่ ›". No Base UI.
   - Deactivating an account also asks, because a Cleaner loses their Area and activation does not give it back (plan 5 decision 3).
   - Deactivating a point or an Area is one press, as D4 and D10 draw it. It is undone by "เปิดใช้งาน", and nothing is lost.
8. **Capture (M17, facility-0045 item 2):**
   - "ใช้ตำแหน่งตรงนี้" starts `watchPosition` (high accuracy, no cached fix) for 30 s, with a countdown, the number of readings and the latest accuracy.
   - The result is the median latitude, the median longitude and the median accuracy (rounded up to whole metres), and it needs at least 3 readings.
   - An accuracy worse than the sign's radius shows the ADR's warning ("ควรขยายรัศมีของป้ายนี้ หรือเก็บใหม่"), but it can still be saved.
   - "บันทึกพิกัด" stays disabled until the 30 s are up. Leaving the page or choosing another sign stops the GPS.
   - Risk: a browser that does not move may report fewer readings. Phones report about once a second while the GPS is on, but desktop Chrome with a fixed simulated location may report only once, so the real run (Task 10) nudges the simulated location. M17 on a real phone over https is checked once the server has https (facility-0049).
9. **Print (D5, facility-0021 item 6):**
   - One Area, one sign (after "พิมพ์" on a row, or after a new QR), or every active Area. "เล็ก (6 ต่อแผ่น)" puts 6 signs on an A4 sheet, and "A5 (2 ต่อแผ่น)" puts 2. Dashed cut lines. The wireframe's "A6 (6 ต่อแผ่น)" is renamed, because six signs on A4 are about 97 × 94 mm, not A6 (105 × 148 mm).
   - The page's `@page` rule (A4, no margin) is in the one CSS bundle, so it applies to any page the browser prints. Only this page is meant for paper.
   - Each sign shows its type, its name, the Area and building, "สแกนด้วยกล้องมือถือ", the rounds (points) or the four attendance events (check-in), the sign code, the QR date and the full URL in small print.
   - Deactivated points and Areas are not printed.
10. **Accounts (D6, facility-0020, 0056):**
    - Filters: ทั้งหมด, แม่บ้าน, หัวหน้า, Admin, ปิดใช้งาน, with counts. Search covers name and login name. No paging.
    - The add form shows and sends only the chosen role's fields, because the server refuses the others with a 400.
    - A shift already held in the chosen Area, or a building-shift already held by a Supervisor, cannot be picked, and the page says who holds it. A day-only Area offers no night shift.
    - Edit sends the Cleaner's current Area back, because the PUT replaces it.
    - "นำเข้าจาก Excel" is left out until its backend exists (ADR 0056, D11).
11. **D10 gains "รหัส Area"** on the add form. The server requires a code (2–20 letters or digits, printed on every sign) and fixes it once created. The edit form shows it read-only.
12. **Round times are typed as text "07:00"**, checked with `HH:mm` before sending, instead of `<input type="time">`. That input looks different in every browser and cannot be typed into reliably in jsdom. The server checks overlap and shift bounds, and its message is shown as it is.
13. **Admin pages use the cache with invalidation, and do not refetch every time they open.** Only Admins change this data, and every save invalidates it. The Dashboard's 30 s poll (facility-0061) is plan 11's.

---

## File structure after this plan

```
frontend/
├── package.json                      + uqr 0.1.3 (Task 6)
└── src/
    ├── router.tsx                    + the Admin setup routes
    ├── router.test.tsx               + the Admin menu
    ├── shared/
    │   ├── api/baseApi.ts            + tagTypes 'AdminSetup'
    │   ├── api/adminSetupApi.ts      Area, sign, building and Cleaner types; getAreas, getArea, getBuildings,
    │   │                             getCleaners, setSignLocation
    │   ├── components/AdminNav.tsx   the Admin menu; AppLayout wide shows it
    │   ├── components/AppLayout.tsx  + AdminNav when wide
    │   ├── components/shared.css     + menu, form field, table, warn button; bar and menu hidden in print
    │   ├── utils/labels.ts (+ test)  + shiftShortLabel, shiftPatternLabel, signPlaceLabel, roundsText, formatCoordinates
    │   └── utils/thaiTime.ts (+ test) + formatDate
    ├── pages/
    │   ├── areas/                    AreasPage (D4), AreaList, AreaDetailPanel, SignTable, PointEditor, SignPanel,
    │   │                             areasApi, areaText, pointRules (+ test), signRules (+ test), areas.css, test
    │   ├── area-form/                AreaFormPage (D10), AreaForm, AreaActiveSection, areaFormApi, areaFormRules,
    │   │                             areaForm.css, test
    │   ├── capture/                  CapturePage (M17), captureLocation (+ test), capture.css, test
    │   ├── print-signs/              PrintSignsPage (D5), SignCard, QrCode, qr (+ test), printRules, printSignsApi,
    │   │                             print.css, test
    │   └── accounts/                 AccountsPage (D6), AccountTable, CreateAccountForm, EditAccountForm, SecretForms,
    │                                 DeactivateConfirm, accountsApi, accountRules (+ test), accounts.css, tests
    └── test/
        ├── adminFixtures.ts          made-up Areas, signs, points, buildings, Cleaners, accounts
        └── fakeGps.ts                + fakeGpsTrack (watchPosition)
```

---

### Task 1: Admin foundation: menu, shared Admin reads, labels

The base every Admin page stands on: the menu, the cache tag, the Area and sign types with their reads, the labels the pages share, and the test fixtures.

**Files:**
- Modify: `docs/handoff.md`
- Modify: `frontend/src/shared/api/baseApi.ts`
- Create: `frontend/src/shared/api/adminSetupApi.ts`
- Modify: `frontend/src/shared/utils/labels.ts`, `frontend/src/shared/utils/thaiTime.ts`, `frontend/src/shared/utils/thaiTime.test.ts`
- Create: `frontend/src/shared/utils/labels.test.ts`
- Create: `frontend/src/shared/components/AdminNav.tsx`
- Modify: `frontend/src/shared/components/AppLayout.tsx`, `frontend/src/shared/components/shared.css`
- Create: `frontend/src/test/adminFixtures.ts`
- Test: `frontend/src/router.test.tsx`

**Interfaces:**
- Consumes: `baseApi` (`shared/api/baseApi.ts`), `Shift` (`shared/api/types.ts`).
- Produces:
  - `baseApi` tag `'AdminSetup'`.
  - In `shared/api/adminSetupApi.ts`:
    - Types `ShiftPattern = 'DayAndNight' | 'DayOnly'` and `LocationSource = 'Site' | 'Map'`.
    - Types `Building`, `CleanerOption`, `AssignedCleaner`, `AreaSummary`, `AdminSign`, `AdminRoundWindow`, `AdminPoint`, `AreaDetail` and `SignLocationRequest` (shapes in Step 4).
    - Hooks `useGetAreasQuery()`, `useGetAreaQuery(areaId: number)`, `useGetBuildingsQuery()`, `useGetCleanersQuery()` and `useSetSignLocationMutation()`.
  - In `shared/utils/labels.ts`:
    - `shiftShortLabel: Record<Shift, string>` and `shiftPatternLabel: Record<ShiftPattern, string>`.
    - `signPlaceLabel(sign: Pick<AdminSign, 'locationSource' | 'locationAccuracyM'>): string`.
    - `roundsText(windows: readonly Pick<AdminRoundWindow, 'shift' | 'start' | 'end'>[]): string`.
    - `formatCoordinates(latitude: number, longitude: number): string`.
  - `formatDate(moment: string | Date): string` in `shared/utils/thaiTime.ts`, giving `"16/10/2026"`.
  - `AdminNav`, shown by `AppLayout wide`.
  - CSS classes `app-nav`, `app-field`, `app-field__help`, `app-input`, `app-table`, `app-row--inactive`, `app-button--warn`, `app-link-button`, `app-muted`, `app-confirm`.
  - Fixtures in `src/test/adminFixtures.ts`: `adminSession`, `buildings`, `cleaners`, `sign()`, `unlocatedSign()`, `checkInSign()`, `adminPoint()`, `areaSummary()`, `officeSummary`, `areaDetail()`, `officeDetail()`.

- [ ] **Step 1: Create the branch**

```bash
git checkout master && git pull --ff-only && git checkout -b feat/admin-setup
git log --oneline -1   # expect 84890df (or a later docs-only commit)
```

- [ ] **Step 2: Bring the handoff's Status table up to date**

In `docs/handoff.md`, the plan 9 row still says "not pushed". Replace that whole row with:

```markdown
| 9 | Supervisor phone: My Building with live updates, inspection at the point's sign, nothing to inspect, blocked and send failures | done, merged (PR #58, `84890df`) (branch `feat/supervisor-phone`): `docs/superpowers/plans/2026-10-15-supervisor-phone.md` |
| 10 | Admin setup: Areas, points, round windows, signs, sign location capture, print signs, accounts | in progress (branch `feat/admin-setup`): `docs/superpowers/plans/2026-10-19-admin-setup.md` |
```

- [ ] **Step 3: Write the failing tests**

Add to `frontend/src/router.test.tsx`, inside the `describe` block, after the test `'a Cleaner opening the Dashboard sees the no-permission screen (facility-0058)'`:

```tsx
  it('an Admin page shows the Admin menu with the open page marked', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('admin') });
    await renderApp('/dashboard');

    const menu = screen.getByRole('navigation', { name: 'เมนู Admin' });
    expect(within(menu).getAllByRole('link').map((link) => link.textContent)).toEqual([
      'สถานะจุด',
      'จัดการ Area และป้าย',
      'บัญชีผู้ใช้',
    ]);
    expect(within(menu).getByRole('link', { name: 'สถานะจุด' })).toHaveAttribute('aria-current', 'page');
    expect(within(menu).getByRole('link', { name: 'จัดการ Area และป้าย' })).toHaveAttribute('href', '/admin/areas');
  });

  it('a phone page has no Admin menu', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('cleaner') });
    await renderApp('/my-work');

    expect(screen.queryByRole('navigation', { name: 'เมนู Admin' })).not.toBeInTheDocument();
  });
```

Change the first import line of `router.test.tsx` to:

```tsx
import { screen, within } from '@testing-library/react';
```

Create `frontend/src/shared/utils/labels.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { formatCoordinates, roundsText, signPlaceLabel } from './labels';

describe('Admin setup labels', () => {
  it('names where a sign location came from (facility-0045)', () => {
    expect(signPlaceLabel({ locationSource: 'Site', locationAccuracyM: 14 })).toBe('หน้างาน ±14 ม.');
    expect(signPlaceLabel({ locationSource: 'Map' })).toBe('ยังไม่ยืนยันหน้างาน (จากแผนที่)');
    expect(signPlaceLabel({})).toBe('ยังไม่มีพิกัด');
  });

  it('lists a point’s rounds by shift, in the order the server sends them', () => {
    expect(
      roundsText([
        { shift: 'Day', start: '07:00', end: '09:00' },
        { shift: 'Day', start: '16:00', end: '18:00' },
        { shift: 'Night', start: '20:00', end: '22:00' },
        { shift: 'Night', start: '03:00', end: '05:00' },
      ]),
    ).toBe('เช้า 07:00–09:00, 16:00–18:00 · ดึก 20:00–22:00, 03:00–05:00');
    expect(roundsText([{ shift: 'Night', start: '20:00', end: '22:00' }])).toBe('ดึก 20:00–22:00');
    expect(roundsText([])).toBe('ยังไม่มีรอบ');
  });

  it('writes coordinates with 6 decimals, as the server stores them', () => {
    expect(formatCoordinates(13.7563, 100.5018)).toBe('13.756300, 100.501800');
  });
});
```

In `frontend/src/shared/utils/thaiTime.test.ts`, add `formatDate` to the import from `'./thaiTime'`, and add this test inside the existing `describe` block:

```ts
  it('formatDate gives the Thai calendar date, which may be a day after the UTC date', () => {
    expect(formatDate('2026-10-15T18:30:00Z')).toBe('16/10/2026'); // 01:30 on the 16th in Thailand
    expect(formatDate('2026-10-16T02:00:00Z')).toBe('16/10/2026');
  });
```

- [ ] **Step 4: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/router.test.tsx src/shared/utils`
Expected: FAIL. `labels.test.ts` cannot import `formatCoordinates`, `roundsText` or `signPlaceLabel`. `thaiTime.test.ts` cannot import `formatDate`. The menu test finds no navigation named "เมนู Admin".

- [ ] **Step 5: The cache tag**

In `frontend/src/shared/api/baseApi.ts`, replace:

```ts
  // Refetched by tag only where a live push may have been missed (facility-0064 item 2)
  tagTypes: ['MyBuilding'],
```

with:

```ts
  // MyBuilding: refetched where a live push may have been missed (facility-0064 item 2).
  // AdminSetup: every Admin setup read; every Admin save invalidates it (plan 10 decision 3)
  tagTypes: ['MyBuilding', 'AdminSetup'],
```

- [ ] **Step 6: The shared Admin reads**

Create `frontend/src/shared/api/adminSetupApi.ts`:

```ts
import { baseApi } from './baseApi';
import type { Shift } from './types';

/**
 * The Admin's Area, point and sign data (facility-0045), read by the Areas, Area form, capture and print pages, and
 * the accounts page's pickers. Fields the server sends as null arrive missing, so they are optional here.
 */

/** facility-0040 rule 4. */
export type ShiftPattern = 'DayAndNight' | 'DayOnly';

/** facility-0045: Site = captured standing at the sign; Map = typed from a map, "ยังไม่ยืนยันหน้างาน". */
export type LocationSource = 'Site' | 'Map';

/** BuildingDto. */
export interface Building {
  id: number;
  code: string;
  name: string;
}

/** CleanerOptionDto: every active Cleaner, for the Area form's pickers (D10). */
export interface CleanerOption {
  id: number;
  employeeId: string;
  displayName: string;
  shift?: Shift;
  areaId?: number;
  areaCode?: string;
}

/** AssignedCleanerDto. */
export interface AssignedCleaner {
  id: number;
  employeeId: string;
  displayName: string;
}

/** AreaSummaryDto. signsNotConfirmedOnSite counts the check-in sign and active points' signs with no site capture. */
export interface AreaSummary {
  id: number;
  code: string;
  name: string;
  buildingId: number;
  buildingCode: string;
  shiftPattern: ShiftPattern;
  isActive: boolean;
  dayCleaner?: AssignedCleaner;
  nightCleaner?: AssignedCleaner;
  activePointCount: number;
  signsNotConfirmedOnSite: number;
}

/** AdminSignDto. It carries the QR Token: only the Admin's Area and print pages get it (facility-0060). */
export interface AdminSign {
  id: number;
  code: string;
  qrToken: string;
  qrIssuedAt: string;
  latitude?: number;
  longitude?: number;
  locationAccuracyM?: number;
  locationSource?: LocationSource;
  locatedAt?: string;
  radiusM: number;
}

/** AdminRoundWindowDto. start and end are Thai wall-clock "HH:mm", as the Admin typed them (facility-0047). */
export interface AdminRoundWindow {
  id: number;
  shift: Shift;
  start: string;
  end: string;
}

/** AdminPointDto. Windows come Day first, then by time into the shift (a night 03:00 after 20:00). */
export interface AdminPoint {
  id: number;
  name: string;
  sortOrder: number;
  isActive: boolean;
  sign: AdminSign;
  roundWindows: AdminRoundWindow[];
}

/** AreaDetailDto: the Area, its one check-in sign, and its points, deactivated ones included. */
export interface AreaDetail {
  area: AreaSummary;
  checkInSign: AdminSign;
  points: AdminPoint[];
}

/** SignLocationRequest. A map pick has no accuracy. */
export interface SignLocationRequest {
  signId: number;
  latitude: number;
  longitude: number;
  accuracyM?: number;
  source: LocationSource;
}

export const adminSetupApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getAreas: build.query<AreaSummary[], void>({
      query: () => '/admin/areas',
      providesTags: ['AdminSetup'],
    }),
    getArea: build.query<AreaDetail, number>({
      query: (areaId) => `/admin/areas/${areaId}`,
      providesTags: ['AdminSetup'],
    }),
    getBuildings: build.query<Building[], void>({
      query: () => '/admin/buildings',
      providesTags: ['AdminSetup'],
    }),
    getCleaners: build.query<CleanerOption[], void>({
      query: () => '/admin/cleaners',
      providesTags: ['AdminSetup'],
    }),
    /** Used by the D4 sign panel (a map pick) and M17 (a site capture). */
    setSignLocation: build.mutation<AdminSign, SignLocationRequest>({
      query: ({ signId, ...body }) => ({ url: `/admin/signs/${signId}/location`, method: 'PUT', body }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const {
  useGetAreasQuery,
  useGetAreaQuery,
  useGetBuildingsQuery,
  useGetCleanersQuery,
  useSetSignLocationMutation,
} = adminSetupApi;
```

- [ ] **Step 7: The labels and the date**

Replace the first line of `frontend/src/shared/utils/labels.ts`:

```ts
import type { AttendanceEvent, PointStatus, Shift } from '../api/types';
```

with:

```ts
import type { AdminRoundWindow, AdminSign, ShiftPattern } from '../api/adminSetupApi';
import type { AttendanceEvent, PointStatus, Shift } from '../api/types';
```

Append to the end of `labels.ts`:

```ts
/** Short shift names for tables and lists. */
export const shiftShortLabel: Record<Shift, string> = {
  Day: 'กะเช้า',
  Night: 'กะดึก',
};

/** facility-0040 rule 4, wireframe D10. */
export const shiftPatternLabel: Record<ShiftPattern, string> = {
  DayAndNight: '2 กะ (เช้า + ดึก)',
  DayOnly: 'เฉพาะกะเช้า',
};

/** facility-0045: where a sign's coordinates came from, as the Admin pages show it (D4, M17). */
export function signPlaceLabel(sign: Pick<AdminSign, 'locationSource' | 'locationAccuracyM'>): string {
  if (sign.locationSource === 'Site') {
    return `หน้างาน ±${sign.locationAccuracyM ?? 0} ม.`;
  }
  return sign.locationSource === 'Map' ? 'ยังไม่ยืนยันหน้างาน (จากแผนที่)' : 'ยังไม่มีพิกัด';
}

/** A point's Round Windows grouped by shift, in the order given: "เช้า 07:00–09:00, 16:00–18:00 · ดึก 20:00–22:00". */
export function roundsText(windows: readonly Pick<AdminRoundWindow, 'shift' | 'start' | 'end'>[]): string {
  const parts = (['Day', 'Night'] as const)
    .map((shift) => ({
      name: shift === 'Day' ? 'เช้า' : 'ดึก',
      times: windows.filter((window) => window.shift === shift).map((window) => `${window.start}–${window.end}`),
    }))
    .filter((part) => part.times.length > 0)
    .map((part) => `${part.name} ${part.times.join(', ')}`);
  return parts.length === 0 ? 'ยังไม่มีรอบ' : parts.join(' · ');
}

/** Coordinates with 6 decimals, as the server stores them (about 0.1 m). */
export function formatCoordinates(latitude: number, longitude: number): string {
  return `${latitude.toFixed(6)}, ${longitude.toFixed(6)}`;
}
```

In `frontend/src/shared/utils/thaiTime.ts`, below the `thaiClock` constant, add:

```ts
/** Day/month/year with Arabic digits and the Gregorian year, in Thai time. */
const thaiDate = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Bangkok',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});
```

and below `formatTime`, add:

```ts
/** A moment as the Thai calendar date "16/10/2026" (e.g. when a QR was issued, on the printed sign). */
export function formatDate(moment: string | Date): string {
  return thaiDate.format(typeof moment === 'string' ? new Date(moment) : moment);
}
```

- [ ] **Step 8: The Admin menu**

Create `frontend/src/shared/components/AdminNav.tsx`:

```tsx
import { NavLink } from 'react-router';
import './shared.css';

/**
 * The Admin menu of wireframes D1–D10, listing only the pages that exist (plan 10 decision 2). Plan 11 adds
 * การเข้างาน, รายการที่ขึ้นเตือน, สรุปการตรวจ, ส่งออก Excel and ประวัติ Admin with their pages.
 */
const adminLinks = [
  { to: '/dashboard', label: 'สถานะจุด' },
  { to: '/admin/areas', label: 'จัดการ Area และป้าย' },
  { to: '/admin/accounts', label: 'บัญชีผู้ใช้' },
] as const;

export function AdminNav() {
  return (
    <nav className="app-nav" aria-label="เมนู Admin">
      <ul>
        {adminLinks.map((link) => (
          <li key={link.to}>
            <NavLink
              to={link.to}
              className={({ isActive }) => (isActive ? 'app-nav__link app-nav__link--active' : 'app-nav__link')}
            >
              {link.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
```

In `frontend/src/shared/components/AppLayout.tsx`, add the import below the `roleLabel` import:

```tsx
import { AdminNav } from './AdminNav';
```

and replace:

```tsx
      </header>
      <Outlet />
```

with:

```tsx
      </header>
      {wide && <AdminNav />}
      <Outlet />
```

Also replace the comment above `export function AppLayout` with:

```tsx
/**
 * The bar above every logged-in page: who is logged in, and "ออกจากระบบ". `wide` is the Admin's desktop width
 * (wireframes D1–D11) and brings the Admin menu; without it the page is a phone column (M0–M17).
 */
```

- [ ] **Step 9: Shared styles**

Append to `frontend/src/shared/components/shared.css`:

```css
.app-nav ul {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin: 0;
  padding: 0;
  list-style: none;
  border-bottom: 1px solid var(--color-border);
}

.app-nav__link {
  display: inline-block;
  padding: 8px 12px;
  border-bottom: 3px solid transparent;
  color: var(--color-text-soft);
  text-decoration: none;
  font-size: 14px;
}

.app-nav__link--active {
  border-bottom-color: var(--color-primary);
  color: var(--color-primary);
  font-weight: 600;
}

.app-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 14px;
}

.app-field__help,
.app-muted {
  color: var(--color-muted);
  font-size: 13px;
}

.app-input {
  min-height: 40px;
  padding: 0 10px;
  border: 1px solid var(--color-input-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text);
  font-size: 15px;
}

.app-table {
  width: 100%;
  border-collapse: collapse;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  font-size: 14px;
}

.app-table th,
.app-table td {
  padding: 10px 12px;
  border-bottom: 1px solid var(--color-border);
  text-align: left;
  vertical-align: top;
}

.app-table thead th {
  color: var(--color-muted);
  font-size: 13px;
  font-weight: 600;
}

.app-row--inactive td,
.app-row--inactive th {
  color: var(--color-muted);
}

.app-button--warn {
  border-color: var(--color-status-overdue);
  background: var(--color-status-overdue);
  color: var(--color-on-primary);
  font-weight: 700;
}

.app-link-button {
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font-size: 14px;
  text-decoration: underline;
  cursor: pointer;
}

.app-confirm {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px 14px;
  border: 1px solid var(--color-warn-border);
  border-radius: var(--radius-md);
  background: var(--color-warn-bg);
  color: var(--color-warn-text);
}

.app-confirm__title {
  font-weight: 700;
}

.app-confirm__buttons {
  display: flex;
  gap: 8px;
}

@media print {
  .app-bar,
  .app-nav {
    display: none;
  }

  .app-shell,
  .app-shell--wide {
    max-width: none;
    padding: 0;
  }
}
```

- [ ] **Step 10: The Admin fixtures**

Create `frontend/src/test/adminFixtures.ts`:

```ts
import type {
  AdminPoint,
  AdminSign,
  AreaDetail,
  AreaSummary,
  Building,
  CleanerOption,
} from '../shared/api/adminSetupApi';
import { authAnswer } from './fakeServer';

/**
 * Made-up answers for the Admin setup pages (public repo: no real names). The API sends UTC; 02:00Z is 09:00 in
 * Thailand. Fields the server sends as null are left out, as it does (an `undefined` override is dropped by JSON).
 */
export const adminSession = { 'POST /api/auth/refresh': () => authAnswer('admin') };

export const buildings: Building[] = [
  { id: 1, code: 'A', name: 'ตึกทดลอง' },
  { id: 2, code: 'B', name: 'ตึกข้างเคียง' },
];

/** Active Cleaners: two hold AR01, two have no Area yet, one holds an Area of another building. */
export const cleaners: CleanerOption[] = [
  { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ', shift: 'Day', areaId: 1, areaCode: 'AR01' },
  { id: 22, employeeId: '650002', displayName: 'แม่บ้านกะดึก', shift: 'Night', areaId: 1, areaCode: 'AR01' },
  { id: 23, employeeId: '650003', displayName: 'แม่บ้านว่าง', shift: 'Day' },
  { id: 24, employeeId: '650004', displayName: 'แม่บ้านว่างกะดึก', shift: 'Night' },
  { id: 25, employeeId: '650005', displayName: 'แม่บ้านที่อื่น', shift: 'Day', areaId: 3, areaCode: 'AR03' },
];

/** A point's sign captured on site, ±14 m, radius 50 m; QR issued 09:00 on 16/10/2026. */
export function sign(overrides: Partial<AdminSign> = {}): AdminSign {
  return {
    id: 101,
    code: 'AR01-01',
    qrToken: '0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f',
    qrIssuedAt: '2026-10-16T02:00:00Z',
    latitude: 13.7563,
    longitude: 100.5018,
    locationAccuracyM: 14,
    locationSource: 'Site',
    locatedAt: '2026-10-16T02:30:00Z',
    radiusM: 50,
    ...overrides,
  };
}

/** A sign with no location yet: the server leaves the location fields out. */
export function unlocatedSign(overrides: Partial<AdminSign> = {}): AdminSign {
  return {
    id: 102,
    code: 'AR01-02',
    qrToken: '5d1c9a2e-8f3b-4e6a-b7c0-2d4e6f8a0b1c',
    qrIssuedAt: '2026-10-16T02:00:00Z',
    radiusM: 50,
    ...overrides,
  };
}

/** AR01's check-in sign, captured on site. */
export function checkInSign(overrides: Partial<AdminSign> = {}): AdminSign {
  return sign({ id: 100, code: 'AR01-IN', qrToken: '9a8b7c6d-5e4f-4a3b-9c2d-1e0f9a8b7c6d', ...overrides });
}

/** A point with two Day and two Night windows (the night 03:00 after 20:00, as the server orders them). */
export function adminPoint(overrides: Partial<AdminPoint> = {}): AdminPoint {
  return {
    id: 11,
    name: 'ห้องน้ำชาย',
    sortOrder: 1,
    isActive: true,
    sign: sign(),
    roundWindows: [
      { id: 1, shift: 'Day', start: '07:00', end: '09:00' },
      { id: 2, shift: 'Day', start: '16:00', end: '18:00' },
      { id: 3, shift: 'Night', start: '20:00', end: '22:00' },
      { id: 4, shift: 'Night', start: '03:00', end: '05:00' },
    ],
    ...overrides,
  };
}

/** AR01: two shifts, a Cleaner in each, two active points, one sign not confirmed on site. */
export function areaSummary(overrides: Partial<AreaSummary> = {}): AreaSummary {
  return {
    id: 1,
    code: 'AR01',
    name: 'Area ทดลอง',
    buildingId: 1,
    buildingCode: 'A',
    shiftPattern: 'DayAndNight',
    isActive: true,
    dayCleaner: { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ' },
    nightCleaner: { id: 22, employeeId: '650002', displayName: 'แม่บ้านกะดึก' },
    activePointCount: 2,
    signsNotConfirmedOnSite: 1,
    ...overrides,
  };
}

/** AR02: day only, no Cleaner, no point; its check-in sign has no location. */
export const officeSummary: AreaSummary = areaSummary({
  id: 2,
  code: 'AR02',
  name: 'Office ทดลอง',
  shiftPattern: 'DayOnly',
  dayCleaner: undefined,
  nightCleaner: undefined,
  activePointCount: 0,
  signsNotConfirmedOnSite: 1,
});

/** AR01 in full: the check-in sign, ห้องน้ำชาย (located) and ห้องน้ำหญิง (no location, one Day window). */
export function areaDetail(overrides: Partial<AreaDetail> = {}): AreaDetail {
  return {
    area: areaSummary(),
    checkInSign: checkInSign(),
    points: [
      adminPoint(),
      adminPoint({
        id: 12,
        name: 'ห้องน้ำหญิง',
        sortOrder: 2,
        sign: unlocatedSign(),
        roundWindows: [{ id: 5, shift: 'Day', start: '08:00', end: '10:00' }],
      }),
    ],
    ...overrides,
  };
}

/** AR02 in full: only its check-in sign. */
export function officeDetail(overrides: Partial<AreaDetail> = {}): AreaDetail {
  return {
    area: officeSummary,
    checkInSign: unlocatedSign({ id: 200, code: 'AR02-IN', qrToken: '1f2e3d4c-5b6a-4987-8a6b-5c4d3e2f1a0b' }),
    points: [],
    ...overrides,
  };
}
```

- [ ] **Step 11: Run the tests, the typecheck and the lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. Frontend tests: 163 (157 + 2 router + 3 labels + 1 thaiTime). Use the number Vitest prints in the handoff.

- [ ] **Step 12: Handoff entry**

Add a section `## Task log (plan 10)` to `docs/handoff.md`, just above `## Pending work`, with:

```markdown
- <date> · Task 1 done: Admin menu (สถานะจุด, จัดการ Area และป้าย, บัญชีผู้ใช้) shown by AppLayout wide and hidden in print; cache tag AdminSetup; shared/api/adminSetupApi.ts (Area, sign, building and Cleaner types and reads, setSignLocation); labels for shift pattern, sign location, rounds and coordinates; formatDate; shared form, table and confirm styles; test/adminFixtures.ts. Plan 9 marked merged in the Status table. Frontend tests <n> passed. Next: Task 2 (Areas page).
```

- [ ] **Step 13: Commit**

```bash
git add docs/handoff.md docs/superpowers/plans/2026-10-19-admin-setup.md frontend/src
git commit -m "feat(frontend): Admin menu, shared Admin setup reads and labels, Admin fixtures"
```

The plan file was written on `master` before the branch existed and is still untracked, so it goes into this first commit.

---
### Task 2: Areas page (D4): the Area list and the chosen Area's signs

The page shows the data only. Editing comes in Tasks 3 and 4.

**Files:**
- Create: `frontend/src/pages/areas/index.ts`, `AreasPage.tsx`, `AreaList.tsx`, `AreaDetailPanel.tsx`, `SignTable.tsx`, `areaText.ts`, `areas.css`
- Modify: `frontend/src/router.tsx`
- Test: `frontend/src/pages/areas/AreasPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `useGetAreasQuery`, `useGetAreaQuery`, `AreaSummary`, `AreaDetail`, `AdminSign` (Task 1); `roundsText`, `signPlaceLabel` (Task 1); fixtures `adminSession`, `areaSummary`, `officeSummary`, `areaDetail`, `officeDetail` (Task 1).
- Produces:
  - `AreasPage` at `/admin/areas/:areaId?`.
  - `AreaDetailPanel({ detail })`, keyed by Area id.
  - `SignTable({ detail })`. Task 3 adds `onEdit`.
  - `areaMeta(area: AreaSummary): string` and `areaLine(detail: AreaDetail): string`.

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/pages/areas/AreasPage.test.tsx`:

```tsx
import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { adminSession, areaDetail, areaSummary, officeDetail, officeSummary } from '../../test/adminFixtures';
import { authAnswer, fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }),
    'GET /api/admin/areas/2': () => ({ status: 200, body: officeDetail() }),
    ...routes,
  });
}

const row = (name: RegExp) => screen.getByRole('row', { name });

describe('AreasPage (D4, facility-0045)', () => {
  it('lists every Area and opens the first one with its signs', async () => {
    serve();
    await renderApp('/admin/areas');

    expect(await screen.findByRole('heading', { level: 2, name: 'AR01 Area ทดลอง' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'จัดการ Area จุด และพิกัดป้าย' })).toBeInTheDocument();
    const list = screen.getByRole('region', { name: 'Area ทั้งหมด (2)' });
    expect(within(list).getByText('ตึก A · 2 กะ · 2 จุด · ป้ายยังไม่ยืนยัน 1')).toBeInTheDocument();
    expect(within(list).getByText('ตึก A · เฉพาะกะเช้า · 0 จุด · ป้ายยังไม่ยืนยัน 1')).toBeInTheDocument();
    expect(within(list).getByRole('link', { name: /^AR01 Area ทดลอง/ })).toHaveAttribute('aria-current', 'true');
    expect(within(list).getByRole('link', { name: '+ เพิ่ม Area' })).toHaveAttribute('href', '/admin/areas/new');

    expect(screen.getByText('Area ที่เลือก · ตึก A')).toBeInTheDocument();
    expect(
      screen.getByText('ทำ 2 กะ (เช้า + ดึก) · แม่บ้านประจำ: เช้า แม่บ้านทดสอบ · ดึก แม่บ้านกะดึก · ป้าย 1 + 2'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'แก้ไข Area' })).toHaveAttribute('href', '/admin/areas/1/edit');
    expect(screen.getByRole('link', { name: 'พิมพ์ป้ายของ Area นี้ ›' })).toHaveAttribute('href', '/admin/print?area=1');
    expect(screen.getByRole('link', { name: 'เก็บพิกัดบนมือถือ ›' })).toHaveAttribute('href', '/admin/areas/1/capture');

    const checkIn = row(/AR01-IN/);
    expect(within(checkIn).getByText('หน้างาน ±14 ม. · รัศมี 50 ม.')).toBeInTheDocument();
    expect(within(checkIn).getByRole('link', { name: 'พิมพ์' })).toHaveAttribute('href', '/admin/print?area=1&sign=100');
    const men = row(/AR01-01/);
    expect(within(men).getByText('เช้า 07:00–09:00, 16:00–18:00 · ดึก 20:00–22:00, 03:00–05:00')).toBeInTheDocument();
    expect(within(men).getByRole('link', { name: 'พิมพ์' })).toHaveAttribute('href', '/admin/print?area=1&sign=101');
    const women = row(/AR01-02/);
    expect(within(women).getByText('เช้า 08:00–10:00')).toBeInTheDocument();
    expect(within(women).getByText('ยังไม่มีพิกัด · รัศมี 50 ม.')).toBeInTheDocument();
  });

  it('opens the Area in the address: a day-only Area with no Cleaner and no point', async () => {
    serve();
    await renderApp('/admin/areas/2');

    expect(await screen.findByRole('heading', { level: 2, name: 'AR02 Office ทดลอง' })).toBeInTheDocument();
    expect(screen.getByText('ทำเฉพาะกะเช้า · แม่บ้านประจำ: เช้า ยังไม่มี · ป้าย 1 + 0')).toBeInTheDocument();
    expect(screen.getByText('Area นี้ยังไม่มีจุดทำความสะอาด')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /^AR02 Office ทดลอง/ })).toHaveAttribute('aria-current', 'true');
    expect(screen.getByRole('link', { name: /^AR01 Area ทดลอง/ })).not.toHaveAttribute('aria-current');
  });

  it('a deactivated Area says so and offers nothing to print or capture', async () => {
    const detail = areaDetail({ area: areaSummary({ isActive: false }) });
    serve({
      'GET /api/admin/areas': () => ({ status: 200, body: [detail.area] }),
      'GET /api/admin/areas/1': () => ({ status: 200, body: detail }),
    });
    await renderApp('/admin/areas/1');

    expect(
      await screen.findByText('Area นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไข Area"'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'พิมพ์ป้ายของ Area นี้ ›' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'เก็บพิกัดบนมือถือ ›' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'พิมพ์' })).not.toBeInTheDocument();
    expect(screen.getByText('ตึก A · 2 กะ · 2 จุด · ป้ายยังไม่ยืนยัน 1 · ปิดใช้งาน')).toBeInTheDocument();
  });

  it('a deactivated point stays in the table, marked, with no print link', async () => {
    const detail = areaDetail();
    detail.points[1] = { ...detail.points[1], isActive: false };
    serve({ 'GET /api/admin/areas/1': () => ({ status: 200, body: detail }) });
    await renderApp('/admin/areas/1');

    const women = await screen.findByRole('row', { name: /AR01-02/ });
    expect(within(women).getByText('จุดทำความสะอาด · ปิดใช้งาน')).toBeInTheDocument();
    expect(within(women).queryByRole('link', { name: 'พิมพ์' })).not.toBeInTheDocument();
    expect(
      screen.getByText('ทำ 2 กะ (เช้า + ดึก) · แม่บ้านประจำ: เช้า แม่บ้านทดสอบ · ดึก แม่บ้านกะดึก · ป้าย 1 + 1'),
    ).toBeInTheDocument();
  });

  it('with no Area yet it says how to start', async () => {
    serve({ 'GET /api/admin/areas': () => ({ status: 200, body: [] }) });
    await renderApp('/admin/areas');

    expect(await screen.findByText('ยังไม่มี Area กด "+ เพิ่ม Area" เพื่อเริ่ม')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Area ทั้งหมด (0)' })).toBeInTheDocument();
  });

  it('an Area that does not exist shows the server’s message', async () => {
    serve({ 'GET /api/admin/areas/9': () => ({ status: 404, body: { message: 'ไม่พบ Area นี้' } }) });
    await renderApp('/admin/areas/9');

    expect(await screen.findByRole('alert')).toHaveTextContent('ไม่พบ Area นี้');
  });

  it('is for Admins only (facility-0010)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('supervisor') });
    await renderApp('/admin/areas');

    expect(screen.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/areas`
Expected: FAIL. `/admin/areas` is not a route, so every Admin test finds the not-found page.

- [ ] **Step 3: The texts**

Create `frontend/src/pages/areas/areaText.ts`:

```ts
import type { AreaDetail, AreaSummary } from '../../shared/api/adminSetupApi';

/** Wireframe D4's line under each Area in the list. */
export function areaMeta(area: AreaSummary): string {
  const parts = [
    `ตึก ${area.buildingCode}`,
    area.shiftPattern === 'DayOnly' ? 'เฉพาะกะเช้า' : '2 กะ',
    `${area.activePointCount} จุด`,
  ];
  if (area.signsNotConfirmedOnSite > 0) {
    parts.push(`ป้ายยังไม่ยืนยัน ${area.signsNotConfirmedOnSite}`);
  }
  if (!area.isActive) {
    parts.push('ปิดใช้งาน');
  }
  return parts.join(' · ');
}

/** Wireframe D4's line under the chosen Area's name: shifts, Cleaners, and signs (1 check-in + active points). */
export function areaLine(detail: AreaDetail): string {
  const { area } = detail;
  const day = `เช้า ${area.dayCleaner?.displayName ?? 'ยังไม่มี'}`;
  const cleaners =
    area.shiftPattern === 'DayOnly' ? day : `${day} · ดึก ${area.nightCleaner?.displayName ?? 'ยังไม่มี'}`;
  const pattern = area.shiftPattern === 'DayOnly' ? 'ทำเฉพาะกะเช้า' : 'ทำ 2 กะ (เช้า + ดึก)';
  const activePoints = detail.points.filter((point) => point.isActive).length;
  return `${pattern} · แม่บ้านประจำ: ${cleaners} · ป้าย 1 + ${activePoints}`;
}
```

- [ ] **Step 4: The pieces of the page**

Create `frontend/src/pages/areas/AreaList.tsx`:

```tsx
import { Link } from 'react-router';
import type { AreaSummary } from '../../shared/api/adminSetupApi';
import { areaMeta } from './areaText';

interface AreaListProps {
  areas: AreaSummary[] | undefined;
  selectedId: number | undefined;
  loading: boolean;
}

/** Wireframe D4, left: every Area, deactivated ones too; the open one is marked. */
export function AreaList({ areas, selectedId, loading }: AreaListProps) {
  return (
    <section className="areas-list" aria-labelledby="areas-list-title">
      <div className="areas-list__head">
        <h2 id="areas-list-title">{`Area ทั้งหมด (${areas?.length ?? 0})`}</h2>
        <Link className="app-button" to="/admin/areas/new">
          + เพิ่ม Area
        </Link>
      </div>
      {loading && <p className="app-muted">กำลังโหลด Area...</p>}
      <ul>
        {areas?.map((area) => {
          const selected = area.id === selectedId;
          const className = [
            'areas-list__item',
            selected && 'areas-list__item--selected',
            !area.isActive && 'areas-list__item--inactive',
          ]
            .filter(Boolean)
            .join(' ');
          return (
            <li key={area.id}>
              <Link to={`/admin/areas/${area.id}`} className={className} aria-current={selected ? 'true' : undefined}>
                <strong>{`${area.code} ${area.name}`}</strong>
                <span>{areaMeta(area)}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
```

Create `frontend/src/pages/areas/SignTable.tsx`:

```tsx
import { Link } from 'react-router';
import type { AdminSign, AreaDetail } from '../../shared/api/adminSetupApi';
import { roundsText, signPlaceLabel } from '../../shared/utils/labels';

const placeText = (sign: AdminSign) => `${signPlaceLabel(sign)} · รัศมี ${sign.radiusM} ม.`;

/**
 * Wireframe D4's table: the Area's check-in sign first, then its points in the server's order, deactivated ones
 * marked. Signs of a deactivated point or Area cannot be scanned, so they are not offered for printing.
 */
export function SignTable({ detail }: { detail: AreaDetail }) {
  const { area, checkInSign } = detail;
  const printLink = (sign: AdminSign) => `/admin/print?area=${area.id}&sign=${sign.id}`;

  return (
    <>
      <table className="app-table areas-signs" aria-label="ป้ายของ Area นี้">
        <thead>
          <tr>
            <th scope="col">ป้าย</th>
            <th scope="col">ช่วงเวลารอบ</th>
            <th scope="col">พิกัด · ความแม่นยำ</th>
            <th scope="col">จัดการ</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <th scope="row">
              <strong>{checkInSign.code}</strong> ป้ายลงเวลา
              <span className="app-muted areas-kind">ป้ายลงเวลา 1 ป้ายต่อ Area</span>
            </th>
            <td>เข้างาน ออกพัก กลับจากพัก เลิกงาน</td>
            <td>{placeText(checkInSign)}</td>
            <td className="areas-actions">{area.isActive && <Link to={printLink(checkInSign)}>พิมพ์</Link>}</td>
          </tr>
          {detail.points.map((point) => (
            <tr key={point.id} className={point.isActive ? undefined : 'app-row--inactive'}>
              <th scope="row">
                <strong>{point.sign.code}</strong> {point.name}
                <span className="app-muted areas-kind">
                  {point.isActive ? 'จุดทำความสะอาด' : 'จุดทำความสะอาด · ปิดใช้งาน'}
                </span>
              </th>
              <td>{roundsText(point.roundWindows)}</td>
              <td>{placeText(point.sign)}</td>
              <td className="areas-actions">
                {area.isActive && point.isActive && <Link to={printLink(point.sign)}>พิมพ์</Link>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {detail.points.length === 0 && <p className="app-muted">Area นี้ยังไม่มีจุดทำความสะอาด</p>}
    </>
  );
}
```

Create `frontend/src/pages/areas/AreaDetailPanel.tsx`:

```tsx
import { Link } from 'react-router';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { areaLine } from './areaText';
import { SignTable } from './SignTable';

/** Wireframe D4, right: the open Area and its signs. AreasPage keys it by Area id. */
export function AreaDetailPanel({ detail }: { detail: AreaDetail }) {
  const { area } = detail;

  return (
    <section className="areas-detail" aria-labelledby="areas-detail-title">
      <header className="areas-detail__head">
        <span className="app-muted">{`Area ที่เลือก · ตึก ${area.buildingCode}`}</span>
        <h2 id="areas-detail-title">{`${area.code} ${area.name}`}</h2>
        <p>{areaLine(detail)}</p>
        <div className="areas-detail__links">
          <Link className="app-button" to={`/admin/areas/${area.id}/edit`}>
            แก้ไข Area
          </Link>
          {area.isActive && <Link to={`/admin/print?area=${area.id}`}>พิมพ์ป้ายของ Area นี้ ›</Link>}
          {area.isActive && <Link to={`/admin/areas/${area.id}/capture`}>เก็บพิกัดบนมือถือ ›</Link>}
        </div>
      </header>
      {!area.isActive && (
        <p className="app-note app-note--warn">Area นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไข Area"</p>
      )}
      <SignTable detail={detail} />
    </section>
  );
}
```

- [ ] **Step 5: The page and its route**

Create `frontend/src/pages/areas/AreasPage.tsx`:

```tsx
import { useParams } from 'react-router';
import { useGetAreaQuery, useGetAreasQuery } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AreaDetailPanel } from './AreaDetailPanel';
import { AreaList } from './AreaList';
import './areas.css';

/**
 * facility-0045, wireframe D4: the Areas on the left, the open Area's signs on the right. Without an id in the
 * address, the first Area is open. `currentData` is the open Area's own answer, never the previous Area's.
 */
export function AreasPage() {
  const { areaId } = useParams();
  const areas = useGetAreasQuery();
  const parsed = areaId === undefined ? areas.data?.[0]?.id : Number(areaId);
  const selectedId = parsed !== undefined && Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
  const detail = useGetAreaQuery(selectedId ?? 0, { skip: selectedId === undefined });
  const error = areas.error ?? detail.error;

  return (
    <main className="areas-page">
      <h1>จัดการ Area จุด และพิกัดป้าย</h1>
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {areaId !== undefined && selectedId === undefined && (
        <p className="app-note app-note--error" role="alert">
          ไม่พบ Area นี้
        </p>
      )}
      <div className="areas-layout">
        <AreaList areas={areas.data} selectedId={selectedId} loading={areas.isLoading} />
        <div className="areas-main">
          {areas.data?.length === 0 && (
            <p className="app-note app-note--info">ยังไม่มี Area กด "+ เพิ่ม Area" เพื่อเริ่ม</p>
          )}
          {detail.isFetching && !detail.currentData && <p className="app-muted">กำลังโหลด Area...</p>}
          {detail.currentData && <AreaDetailPanel key={detail.currentData.area.id} detail={detail.currentData} />}
        </div>
      </div>
      <p className="app-muted">
        แม่บ้านสแกนได้เฉพาะป้ายของ Area ตัวเอง · ป้ายลงเวลา 1 ป้ายต่อ Area ระบบสร้างให้เองเมื่อเพิ่ม Area ·
        ทุกการแก้ในหน้านี้เก็บลงประวัติ Admin
      </p>
    </main>
  );
}
```

Create `frontend/src/pages/areas/index.ts`:

```ts
export { AreasPage } from './AreasPage';
```

In `frontend/src/router.tsx`, add the import below the `AdminLoginPage, LoginPage` import:

```tsx
import { AreasPage } from './pages/areas';
```

and replace the Admin block:

```tsx
      {
        element: <ProtectedRoute roles={['admin']} />,
        children: [{ element: <AppLayout wide />, children: [{ path: 'dashboard', element: <DashboardPage /> }] }],
      },
```

with:

```tsx
      {
        element: <ProtectedRoute roles={['admin']} />,
        children: [
          {
            element: <AppLayout wide />,
            children: [
              { path: 'dashboard', element: <DashboardPage /> },
              // facility-0045, wireframe D4; without an id the first Area is open
              { path: 'admin/areas/:areaId?', element: <AreasPage /> },
            ],
          },
        ],
      },
```

- [ ] **Step 6: The styles**

Create `frontend/src/pages/areas/areas.css`:

```css
/* Wireframe D4 (facility-0045). Class prefix: areas- */

.areas-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.areas-page h1 {
  font-size: 24px;
  font-weight: 700;
}

.areas-layout {
  display: grid;
  grid-template-columns: 300px minmax(0, 1fr);
  gap: var(--gap);
  align-items: start;
}

@media (max-width: 860px) {
  .areas-layout {
    grid-template-columns: minmax(0, 1fr);
  }
}

.areas-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 14px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.areas-list__head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}

.areas-list__head h2 {
  font-size: 15px;
  font-weight: 600;
}

.areas-list ul {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.areas-list__item {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  color: var(--color-text);
  text-decoration: none;
}

.areas-list__item span {
  color: var(--color-muted);
  font-size: 13px;
}

.areas-list__item--selected {
  background: var(--color-info-bg);
  outline: 1px solid var(--color-info-border);
}

.areas-list__item--inactive strong {
  color: var(--color-muted);
}

.areas-main,
.areas-detail {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-width: 0;
}

.areas-detail__head {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.areas-detail__head h2 {
  font-size: 20px;
  font-weight: 700;
}

.areas-detail__links {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  margin-top: 4px;
  font-size: 14px;
}

.areas-signs th[scope='row'] {
  font-weight: 400;
}

.areas-kind {
  display: block;
}

.areas-actions {
  display: flex;
  gap: 12px;
  white-space: nowrap;
}
```

- [ ] **Step 7: Run the tests, the typecheck and the lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. Frontend tests: 170 (163 + 7).

- [ ] **Step 8: Handoff entry**

```markdown
- <date> · Task 2 done: Areas page D4 (/admin/areas/:areaId?): every Area with its meta line, the open Area (first one by default) with shifts, Cleaners and sign count, its check-in sign and points with rounds, location and radius, print links for active signs, links to edit, print and capture; deactivated Areas and points marked. Frontend tests <n> passed. Next: Task 3 (point editor).
```

- [ ] **Step 9: Commit**

```bash
git add docs/handoff.md frontend/src
git commit -m "feat(frontend): Areas page D4 with the Area list and the open Area's signs"
```

---

### Task 3: Point editor: name, round windows, deactivate (D4)

**Files:**
- Create: `frontend/src/pages/areas/areasApi.ts`, `pointRules.ts`, `pointRules.test.ts`, `PointEditor.tsx`
- Modify: `frontend/src/pages/areas/SignTable.tsx`, `AreaDetailPanel.tsx`, `areas.css`
- Test: `frontend/src/pages/areas/PointEditor.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `AdminPoint`, `AreaSummary`, `AreaDetail` (Task 1); `shiftShortLabel` (Task 1); `SignTable`, `AreaDetailPanel` (Task 2); fixtures `adminPoint`, `unlocatedSign`, `officeDetail` (Task 1).
- Produces:
  - In `pages/areas/pointRules.ts`:
    - `WindowDraft = { shift: Shift; start: string; end: string }`.
    - `isClock(text: string): boolean`.
    - `minutesIntoShift(shift: Shift, clock: string): number`.
    - `sortWindows(windows: readonly WindowDraft[]): WindowDraft[]`.
    - `sameWindow(a: WindowDraft, b: WindowDraft): boolean`.
  - In `pages/areas/areasApi.ts`: `SavePointBody = { name: string; roundWindows: WindowDraft[] }`, and the hooks `useCreatePointMutation()` (`{ areaId, body }`), `useUpdatePointMutation()` (`{ pointId, body }`) and `useSetPointActiveMutation()` (`{ pointId, active }`). Task 4 adds two more.
  - `EditTarget = { kind: 'point'; pointId: number } | { kind: 'new-point' }`, exported from `SignTable.tsx`. Task 4 adds `{ kind: 'check-in' }`.
  - `SignTable({ detail, onEdit })`.
  - `PointEditor({ area, point?, onSaved(point, message), onClose })`.

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/pages/areas/pointRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { isClock, sortWindows } from './pointRules';

describe('point rules (facility-0047)', () => {
  it('accepts only a 24-hour "HH:mm", as the server does', () => {
    expect(isClock('07:00')).toBe(true);
    expect(isClock('23:59')).toBe(true);
    expect(isClock('7:00')).toBe(false);
    expect(isClock('24:00')).toBe(false);
    expect(isClock('07:60')).toBe(false);
    expect(isClock('0700')).toBe(false);
  });

  it('orders windows Day first, then by time into the shift (a night 03:00 after 20:00)', () => {
    expect(
      sortWindows([
        { shift: 'Night', start: '03:00', end: '05:00' },
        { shift: 'Day', start: '16:00', end: '18:00' },
        { shift: 'Night', start: '20:00', end: '22:00' },
        { shift: 'Day', start: '07:00', end: '09:00' },
      ]),
    ).toEqual([
      { shift: 'Day', start: '07:00', end: '09:00' },
      { shift: 'Day', start: '16:00', end: '18:00' },
      { shift: 'Night', start: '20:00', end: '22:00' },
      { shift: 'Night', start: '03:00', end: '05:00' },
    ]);
  });
});
```

Create `frontend/src/pages/areas/PointEditor.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  adminPoint,
  adminSession,
  areaDetail,
  areaSummary,
  officeDetail,
  officeSummary,
  unlocatedSign,
} from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }),
    'GET /api/admin/areas/2': () => ({ status: 200, body: officeDetail() }),
    ...routes,
  });
}

describe('PointEditor (D4, facility-0045, 0047)', () => {
  it('edits a point’s name and windows and sends the full list in shift order', async () => {
    let detailCalls = 0;
    const server = serve({
      'GET /api/admin/areas/1': () => {
        detailCalls += 1;
        return { status: 200, body: areaDetail() };
      },
      'PUT /api/admin/points/11': ({ body }) => ({
        status: 200,
        body: adminPoint({ name: (body as { name: string }).name }),
      }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));

    const editor = screen.getByRole('region', { name: 'แก้ไขจุด · AR01 Area ทดลอง' });
    expect(within(editor).getByLabelText('ชื่อจุด')).toHaveValue('ห้องน้ำชาย');
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });
    const night = within(editor).getByRole('group', { name: 'กะดึก' });
    expect(within(day).getByText('07:00–09:00')).toBeInTheDocument();
    expect(within(night).getByText('03:00–05:00')).toBeInTheDocument();

    await user.click(within(day).getByRole('button', { name: 'ลบช่วง 16:00–18:00' }));
    await user.type(within(night).getByLabelText('เริ่ม'), '23:00');
    await user.type(within(night).getByLabelText('ถึง'), '01:00');
    await user.click(within(night).getByRole('button', { name: 'เพิ่ม' }));
    expect(within(night).getByText('23:00–01:00')).toBeInTheDocument();
    await user.clear(within(editor).getByLabelText('ชื่อจุด'));
    await user.type(within(editor).getByLabelText('ชื่อจุด'), 'ห้องน้ำชาย ชั้น 1');
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));

    expect(await screen.findByText('บันทึกจุด ห้องน้ำชาย ชั้น 1 แล้ว')).toBeInTheDocument();
    expect(server.calls.find((call) => call.method === 'PUT')?.body).toEqual({
      name: 'ห้องน้ำชาย ชั้น 1',
      roundWindows: [
        { shift: 'Day', start: '07:00', end: '09:00' },
        { shift: 'Night', start: '20:00', end: '22:00' },
        { shift: 'Night', start: '23:00', end: '01:00' },
        { shift: 'Night', start: '03:00', end: '05:00' },
      ],
    });
    // The save invalidated AdminSetup, so the open Area was asked again
    await waitFor(() => expect(detailCalls).toBe(2));
  });

  it('adds a point to a day-only Area: no night shift, and a name is required', async () => {
    let created = false;
    const newPoint = adminPoint({
      id: 31,
      name: 'โถงทางเดิน',
      sign: unlocatedSign({ id: 301, code: 'AR02-01' }),
      roundWindows: [{ id: 9, shift: 'Day', start: '08:00', end: '10:00' }],
    });
    const server = serve({
      'GET /api/admin/areas/2': () => ({ status: 200, body: officeDetail(created ? { points: [newPoint] } : {}) }),
      'POST /api/admin/areas/2/points': () => {
        created = true;
        return { status: 201, body: newPoint };
      },
    });
    const { user } = await renderApp('/admin/areas/2');
    await user.click(await screen.findByRole('button', { name: '+ เพิ่มจุดทำความสะอาดใน Area นี้' }));

    const editor = screen.getByRole('region', { name: 'เพิ่มจุด · AR02 Office ทดลอง' });
    expect(within(editor).queryByRole('group', { name: 'กะดึก' })).not.toBeInTheDocument();
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));
    expect(within(editor).getByRole('alert')).toHaveTextContent('ใส่ชื่อจุดก่อน');
    expect(server.calls.some((call) => call.path === '/api/admin/areas/2/points')).toBe(false);

    await user.type(within(editor).getByLabelText('ชื่อจุด'), 'โถงทางเดิน');
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });
    await user.type(within(day).getByLabelText('เริ่ม'), '08:00');
    await user.type(within(day).getByLabelText('ถึง'), '10:00{Enter}');
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));

    expect(await screen.findByText('บันทึกจุด โถงทางเดิน แล้ว')).toBeInTheDocument();
    expect(server.calls.find((call) => call.path === '/api/admin/areas/2/points')?.body).toEqual({
      name: 'โถงทางเดิน',
      roundWindows: [{ shift: 'Day', start: '08:00', end: '10:00' }],
    });
    // The new point is now edited in place, and its row is in the table
    expect(await screen.findByRole('region', { name: 'แก้ไขจุด · AR02 Office ทดลอง' })).toBeInTheDocument();
    expect(screen.getByRole('row', { name: /AR02-01/ })).toBeInTheDocument();
  });

  it('checks the time format before adding a window, and refuses the same window twice', async () => {
    serve();
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));

    const day = screen.getByRole('group', { name: 'กะเช้า' });
    await user.type(within(day).getByLabelText('เริ่ม'), '7:00');
    await user.type(within(day).getByLabelText('ถึง'), '09:00');
    await user.click(within(day).getByRole('button', { name: 'เพิ่ม' }));
    expect(within(day).getByRole('alert')).toHaveTextContent('เวลาต้องเขียนแบบ 07:00');

    await user.clear(within(day).getByLabelText('เริ่ม'));
    await user.type(within(day).getByLabelText('เริ่ม'), '07:00');
    await user.click(within(day).getByRole('button', { name: 'เพิ่ม' }));
    expect(within(day).getByRole('alert')).toHaveTextContent('มีช่วงนี้แล้ว');
  });

  it('shows the server’s refusal and keeps the draft', async () => {
    serve({
      'PUT /api/admin/points/11': () => ({ status: 400, body: { message: 'ช่วง 18:00–20:00 ต้องอยู่ในกะเช้า' } }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));

    const editor = screen.getByRole('region', { name: 'แก้ไขจุด · AR01 Area ทดลอง' });
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });
    await user.type(within(day).getByLabelText('เริ่ม'), '18:00');
    await user.type(within(day).getByLabelText('ถึง'), '20:00');
    await user.click(within(day).getByRole('button', { name: 'เพิ่ม' }));
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));

    expect(await within(editor).findByRole('alert')).toHaveTextContent('ช่วง 18:00–20:00 ต้องอยู่ในกะเช้า');
    expect(within(day).getByText('18:00–20:00')).toBeInTheDocument();
  });

  it('deactivates a point and can bring it back (no delete, facility-0021)', async () => {
    let active = true;
    const detail = () => {
      const answer = areaDetail();
      answer.points[1] = { ...answer.points[1], isActive: active };
      return answer;
    };
    const server = serve({
      'GET /api/admin/areas/1': () => ({ status: 200, body: detail() }),
      'POST /api/admin/points/12/deactivate': () => {
        active = false;
        return { status: 200, body: detail().points[1] };
      },
      'POST /api/admin/points/12/activate': () => {
        active = true;
        return { status: 200, body: detail().points[1] };
      },
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-02' }));

    await user.click(screen.getByRole('button', { name: 'ปิดใช้งานจุดนี้' }));
    expect(await screen.findByText('ปิดใช้งานจุด ห้องน้ำหญิง แล้ว')).toBeInTheDocument();
    expect(await screen.findByText('จุดนี้ปิดใช้งานอยู่ ป้ายสแกนไม่ได้')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'เปิดใช้งานจุดนี้' }));
    expect(await screen.findByText('เปิดใช้งานจุด ห้องน้ำหญิง แล้ว')).toBeInTheDocument();
    expect(
      server.calls.filter((call) => call.path.startsWith('/api/admin/points/12/')).map((call) => call.path),
    ).toEqual(['/api/admin/points/12/deactivate', '/api/admin/points/12/activate']);
  });
});
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/areas`
Expected: FAIL. `pointRules` does not exist, and there is no "แก้ไข AR01-01" button.

- [ ] **Step 3: The rules**

Create `frontend/src/pages/areas/pointRules.ts`:

```ts
import type { Shift } from '../../shared/api/types';

/** One Round Window as the form holds it and the server takes it: Thai wall-clock "HH:mm" (facility-0047). */
export interface WindowDraft {
  shift: Shift;
  start: string;
  end: string;
}

/** "07:00" only: two-digit 24-hour time, as RoundWindowRules.TryParseTime on the server. */
export function isClock(text: string): boolean {
  return /^([01]\d|2[0-3]):[0-5]\d$/.test(text);
}

/** Minutes from the shift's start (07:00 or 19:00), wrapping past midnight: RoundWindowRules.MinutesIntoShift. */
export function minutesIntoShift(shift: Shift, clock: string): number {
  const [hours, minutes] = clock.split(':').map(Number);
  const shiftStart = shift === 'Day' ? 7 * 60 : 19 * 60;
  return (hours * 60 + minutes - shiftStart + 24 * 60) % (24 * 60);
}

/** Day before Night, then the order the shift meets them (a night 03:00 after 20:00), as the server lists them. */
export function sortWindows(windows: readonly WindowDraft[]): WindowDraft[] {
  return [...windows].sort((a, b) =>
    a.shift === b.shift
      ? minutesIntoShift(a.shift, a.start) - minutesIntoShift(b.shift, b.start)
      : a.shift === 'Day'
        ? -1
        : 1,
  );
}

export function sameWindow(a: WindowDraft, b: WindowDraft): boolean {
  return a.shift === b.shift && a.start === b.start && a.end === b.end;
}
```

- [ ] **Step 4: The API slice**

Create `frontend/src/pages/areas/areasApi.ts`:

```ts
import type { AdminPoint } from '../../shared/api/adminSetupApi';
import { baseApi } from '../../shared/api/baseApi';
import type { WindowDraft } from './pointRules';

/** SavePointRequest: the full list of windows, [] for none (the server refuses a missing list). */
export interface SavePointBody {
  name: string;
  roundWindows: WindowDraft[];
}

/** The D4 page's saves (facility-0045). Each one invalidates AdminSetup, so the open Area and the list refresh. */
const areasApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    createPoint: build.mutation<AdminPoint, { areaId: number; body: SavePointBody }>({
      query: ({ areaId, body }) => ({ url: `/admin/areas/${areaId}/points`, method: 'POST', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    updatePoint: build.mutation<AdminPoint, { pointId: number; body: SavePointBody }>({
      query: ({ pointId, body }) => ({ url: `/admin/points/${pointId}`, method: 'PUT', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    setPointActive: build.mutation<AdminPoint, { pointId: number; active: boolean }>({
      query: ({ pointId, active }) => ({
        url: `/admin/points/${pointId}/${active ? 'activate' : 'deactivate'}`,
        method: 'POST',
      }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const { useCreatePointMutation, useUpdatePointMutation, useSetPointActiveMutation } = areasApi;
```

- [ ] **Step 5: The editor**

Create `frontend/src/pages/areas/PointEditor.tsx`:

```tsx
import { useState, type FormEvent, type KeyboardEvent } from 'react';
import type { AdminPoint, AreaSummary } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { useCreatePointMutation, useSetPointActiveMutation, useUpdatePointMutation } from './areasApi';
import { isClock, sameWindow, sortWindows, type WindowDraft } from './pointRules';

interface PointEditorProps {
  area: AreaSummary;
  /** Missing: a new point. */
  point?: AdminPoint;
  /** After a save or an (de)activation: the point as the server now has it, and what to tell the Admin. */
  onSaved: (point: AdminPoint, message: string) => void;
  onClose: () => void;
}

/**
 * Wireframe D4's lower panel (facility-0045, 0047): the point's name and its Round Windows per shift. The whole list
 * is sent on every save; the server keeps unchanged windows and checks overlap and shift bounds.
 */
export function PointEditor({ area, point, onSaved, onClose }: PointEditorProps) {
  const [name, setName] = useState(point?.name ?? '');
  const [windows, setWindows] = useState<WindowDraft[]>(() =>
    (point?.roundWindows ?? []).map(({ shift, start, end }) => ({ shift, start, end })),
  );
  const [problem, setProblem] = useState<string | null>(null);
  const [createPoint, created] = useCreatePointMutation();
  const [updatePoint, updated] = useUpdatePointMutation();
  const [setPointActive, toggled] = useSetPointActiveMutation();
  const sending = created.isLoading || updated.isLoading || toggled.isLoading;
  const shifts: Shift[] = area.shiftPattern === 'DayOnly' ? ['Day'] : ['Day', 'Night'];
  const titleId = point ? `point-editor-${point.id}` : 'point-editor-new';

  const save = async (event: FormEvent) => {
    event.preventDefault();
    const trimmed = name.trim();
    if (trimmed === '') {
      setProblem('ใส่ชื่อจุดก่อน');
      return;
    }
    setProblem(null);
    // A day-only Area shows no night windows, so none are sent
    const body = { name: trimmed, roundWindows: sortWindows(windows.filter((w) => shifts.includes(w.shift))) };
    try {
      const saved = point
        ? await updatePoint({ pointId: point.id, body }).unwrap()
        : await createPoint({ areaId: area.id, body }).unwrap();
      onSaved(saved, `บันทึกจุด ${saved.name} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  const toggle = async (active: boolean) => {
    if (!point) {
      return;
    }
    setProblem(null);
    try {
      const saved = await setPointActive({ pointId: point.id, active }).unwrap();
      onSaved(saved, `${active ? 'เปิด' : 'ปิด'}ใช้งานจุด ${saved.name} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <section className="areas-editor app-panel" aria-labelledby={titleId}>
      <h3 id={titleId}>{`${point ? 'แก้ไขจุด' : 'เพิ่มจุด'} · ${area.code} ${area.name}`}</h3>
      <form className="areas-editor__form" onSubmit={(event) => void save(event)}>
        <label className="app-field">
          ชื่อจุด
          <input
            className="app-input"
            value={name}
            maxLength={150}
            disabled={sending}
            onChange={(event) => setName(event.target.value)}
          />
        </label>
        <div className="areas-editor__shifts">
          <span className="areas-editor__label">ช่วงเวลารอบทำความสะอาด</span>
          {shifts.map((shift) => (
            <ShiftWindows
              key={shift}
              shift={shift}
              windows={windows}
              disabled={sending}
              onAdd={(draft) => setWindows((all) => [...all, draft])}
              onRemove={(draft) => setWindows((all) => all.filter((w) => !sameWindow(w, draft)))}
            />
          ))}
          <p className="app-field__help">
            ช่วงห้ามซ้อนกันและต้องอยู่ในกะ · ส่งหลังช่วงจบนับเป็นส่งช้า · Area ที่ทำเฉพาะกะเช้าจะไม่มีช่องกะดึก
          </p>
        </div>
        {problem && (
          <p className="app-note app-note--error" role="alert">
            {problem}
          </p>
        )}
        <div className="areas-editor__buttons">
          <button type="submit" className="app-button app-button--primary" disabled={sending}>
            {sending ? 'กำลังบันทึก...' : 'บันทึกจุด'}
          </button>
          <button type="button" className="app-button" onClick={onClose}>
            ปิด
          </button>
        </div>
      </form>
      {point && (
        <div className="areas-editor__active">
          {point.isActive ? (
            <>
              <p className="app-field__help">ปิดใช้งานแล้วจุดและป้ายหายจาก Dashboard และสแกนไม่ได้ ประวัติยังอยู่ เปิดกลับได้</p>
              <button type="button" className="app-button" disabled={sending} onClick={() => void toggle(false)}>
                ปิดใช้งานจุดนี้
              </button>
            </>
          ) : (
            <>
              <p className="app-note app-note--warn">จุดนี้ปิดใช้งานอยู่ ป้ายสแกนไม่ได้</p>
              <button type="button" className="app-button" disabled={sending} onClick={() => void toggle(true)}>
                เปิดใช้งานจุดนี้
              </button>
            </>
          )}
        </div>
      )}
    </section>
  );
}

interface ShiftWindowsProps {
  shift: Shift;
  windows: WindowDraft[];
  disabled: boolean;
  onAdd: (draft: WindowDraft) => void;
  onRemove: (draft: WindowDraft) => void;
}

/** One shift's windows (D4: "07:00–09:00 ×", then เริ่ม / ถึง / เพิ่ม). Enter in the times adds, not saves. */
function ShiftWindows({ shift, windows, disabled, onAdd, onRemove }: ShiftWindowsProps) {
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const label = shiftShortLabel[shift];
  const mine = sortWindows(windows.filter((w) => w.shift === shift));

  const add = () => {
    const draft = { shift, start: start.trim(), end: end.trim() };
    if (!isClock(draft.start) || !isClock(draft.end)) {
      setProblem('เวลาต้องเขียนแบบ 07:00');
      return;
    }
    if (windows.some((w) => sameWindow(w, draft))) {
      setProblem('มีช่วงนี้แล้ว');
      return;
    }
    setProblem(null);
    onAdd(draft);
    setStart('');
    setEnd('');
  };

  const addOnEnter = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      add();
    }
  };

  return (
    <fieldset className="areas-shift" disabled={disabled}>
      <legend>{label}</legend>
      <ul className="areas-shift__windows" aria-label={`ช่วงรอบ${label}`}>
        {mine.length === 0 && <li className="app-muted">ยังไม่มีช่วงรอบ</li>}
        {mine.map((w) => (
          <li key={`${w.start}-${w.end}`} className="areas-shift__chip">
            <span>{`${w.start}–${w.end}`}</span>
            <button
              type="button"
              className="app-link-button"
              aria-label={`ลบช่วง ${w.start}–${w.end}`}
              onClick={() => onRemove(w)}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
      <div className="areas-shift__add">
        <label className="app-field">
          เริ่ม
          <input
            className="app-input areas-shift__time"
            placeholder="07:00"
            inputMode="numeric"
            maxLength={5}
            value={start}
            onChange={(event) => setStart(event.target.value)}
            onKeyDown={addOnEnter}
          />
        </label>
        <label className="app-field">
          ถึง
          <input
            className="app-input areas-shift__time"
            placeholder="09:00"
            inputMode="numeric"
            maxLength={5}
            value={end}
            onChange={(event) => setEnd(event.target.value)}
            onKeyDown={addOnEnter}
          />
        </label>
        <button type="button" className="app-button" onClick={add}>
          เพิ่ม
        </button>
      </div>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
    </fieldset>
  );
}
```

- [ ] **Step 6: Edit buttons in the table**

Replace the whole of `frontend/src/pages/areas/SignTable.tsx` with:

```tsx
import { Link } from 'react-router';
import type { AdminSign, AreaDetail } from '../../shared/api/adminSetupApi';
import { roundsText, signPlaceLabel } from '../../shared/utils/labels';

/** Which row's editor is open below the table. */
export type EditTarget = { kind: 'point'; pointId: number } | { kind: 'new-point' };

const placeText = (sign: AdminSign) => `${signPlaceLabel(sign)} · รัศมี ${sign.radiusM} ม.`;

/**
 * Wireframe D4's table: the Area's check-in sign first, then its points in the server's order, deactivated ones
 * marked. Signs of a deactivated point or Area cannot be scanned, so they are not offered for printing.
 */
export function SignTable({ detail, onEdit }: { detail: AreaDetail; onEdit: (target: EditTarget) => void }) {
  const { area, checkInSign } = detail;
  const printLink = (sign: AdminSign) => `/admin/print?area=${area.id}&sign=${sign.id}`;

  return (
    <>
      <table className="app-table areas-signs" aria-label="ป้ายของ Area นี้">
        <thead>
          <tr>
            <th scope="col">ป้าย</th>
            <th scope="col">ช่วงเวลารอบ</th>
            <th scope="col">พิกัด · ความแม่นยำ</th>
            <th scope="col">จัดการ</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <th scope="row">
              <strong>{checkInSign.code}</strong> ป้ายลงเวลา
              <span className="app-muted areas-kind">ป้ายลงเวลา 1 ป้ายต่อ Area</span>
            </th>
            <td>เข้างาน ออกพัก กลับจากพัก เลิกงาน</td>
            <td>{placeText(checkInSign)}</td>
            <td className="areas-actions">{area.isActive && <Link to={printLink(checkInSign)}>พิมพ์</Link>}</td>
          </tr>
          {detail.points.map((point) => (
            <tr key={point.id} className={point.isActive ? undefined : 'app-row--inactive'}>
              <th scope="row">
                <strong>{point.sign.code}</strong> {point.name}
                <span className="app-muted areas-kind">
                  {point.isActive ? 'จุดทำความสะอาด' : 'จุดทำความสะอาด · ปิดใช้งาน'}
                </span>
              </th>
              <td>{roundsText(point.roundWindows)}</td>
              <td>{placeText(point.sign)}</td>
              <td className="areas-actions">
                <button
                  type="button"
                  className="app-link-button"
                  aria-label={`แก้ไข ${point.sign.code}`}
                  onClick={() => onEdit({ kind: 'point', pointId: point.id })}
                >
                  แก้ไข
                </button>
                {area.isActive && point.isActive && <Link to={printLink(point.sign)}>พิมพ์</Link>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {detail.points.length === 0 && <p className="app-muted">Area นี้ยังไม่มีจุดทำความสะอาด</p>}
      {area.isActive && (
        <button type="button" className="app-button areas-add-point" onClick={() => onEdit({ kind: 'new-point' })}>
          + เพิ่มจุดทำความสะอาดใน Area นี้
        </button>
      )}
    </>
  );
}
```

- [ ] **Step 7: The editor below the table**

Replace the whole of `frontend/src/pages/areas/AreaDetailPanel.tsx` with:

```tsx
import { useState } from 'react';
import { Link } from 'react-router';
import type { AdminPoint, AreaDetail } from '../../shared/api/adminSetupApi';
import { areaLine } from './areaText';
import { PointEditor } from './PointEditor';
import { SignTable, type EditTarget } from './SignTable';

/**
 * Wireframe D4, right: the open Area, its signs, and the editor of the row being edited. AreasPage keys it by Area
 * id, so opening another Area closes the editor.
 */
export function AreaDetailPanel({ detail }: { detail: AreaDetail }) {
  const { area } = detail;
  const [editing, setEditing] = useState<EditTarget | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const open = (target: EditTarget) => {
    setEditing(target);
    setNotice(null);
  };
  // A new point becomes the point being edited; until the refreshed Area arrives, no editor shows
  const saved = (point: AdminPoint, message: string) => {
    setEditing({ kind: 'point', pointId: point.id });
    setNotice(message);
  };
  const editedPoint = editing?.kind === 'point' ? detail.points.find((point) => point.id === editing.pointId) : undefined;

  return (
    <section className="areas-detail" aria-labelledby="areas-detail-title">
      <header className="areas-detail__head">
        <span className="app-muted">{`Area ที่เลือก · ตึก ${area.buildingCode}`}</span>
        <h2 id="areas-detail-title">{`${area.code} ${area.name}`}</h2>
        <p>{areaLine(detail)}</p>
        <div className="areas-detail__links">
          <Link className="app-button" to={`/admin/areas/${area.id}/edit`}>
            แก้ไข Area
          </Link>
          {area.isActive && <Link to={`/admin/print?area=${area.id}`}>พิมพ์ป้ายของ Area นี้ ›</Link>}
          {area.isActive && <Link to={`/admin/areas/${area.id}/capture`}>เก็บพิกัดบนมือถือ ›</Link>}
        </div>
      </header>
      {!area.isActive && (
        <p className="app-note app-note--warn">Area นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไข Area"</p>
      )}
      <SignTable detail={detail} onEdit={open} />
      {notice && (
        <p className="app-note app-note--info" role="status">
          {notice}
        </p>
      )}
      {editing?.kind === 'new-point' && (
        <PointEditor key="new" area={area} onSaved={saved} onClose={() => setEditing(null)} />
      )}
      {editedPoint && (
        <PointEditor
          key={editedPoint.id}
          area={area}
          point={editedPoint}
          onSaved={saved}
          onClose={() => setEditing(null)}
        />
      )}
    </section>
  );
}
```

- [ ] **Step 8: The styles**

Append to `frontend/src/pages/areas/areas.css`:

```css
.areas-add-point {
  align-self: flex-start;
}

.areas-editor h3 {
  font-size: 16px;
  font-weight: 700;
}

.areas-editor__form,
.areas-editor__shifts {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.areas-editor__label {
  font-size: 14px;
  font-weight: 600;
}

.areas-editor__buttons,
.areas-editor__active {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.areas-shift {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0;
  padding: 10px 12px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
}

.areas-shift legend {
  padding: 0 4px;
  font-size: 14px;
  font-weight: 600;
}

.areas-shift__windows {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.areas-shift__chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 2px 10px;
  border: 1px solid var(--color-info-border);
  border-radius: 999px;
  background: var(--color-info-bg);
  font-size: 14px;
}

.areas-shift__add {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 8px;
}

.areas-shift__time {
  width: 90px;
}
```

- [ ] **Step 9: Run the tests, the typecheck and the lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. Frontend tests: 177 (170 + 2 rules + 5 editor).

- [ ] **Step 10: Handoff entry**

```markdown
- <date> · Task 3 done: point editor on D4: name, Round Windows per shift (typed "HH:mm", checked before adding, Enter adds), the full list sent in shift order on save, day-only Areas have no night shift, the server's refusal shown with the draft kept, deactivate / activate a point; "+ เพิ่มจุดทำความสะอาดใน Area นี้" opens the same editor and a new point stays open for editing. Frontend tests <n> passed. Next: Task 4 (sign settings).
```

- [ ] **Step 11: Commit**

```bash
git add docs/handoff.md frontend/src
git commit -m "feat(frontend): point editor on the Areas page: name, round windows, deactivate"
```

---

### Task 4: Sign settings: location, radius and a new QR (D4)

**Files:**
- Modify: `frontend/src/pages/areas/areasApi.ts`, `SignTable.tsx`, `AreaDetailPanel.tsx`, `PointEditor.tsx`, `areas.css`
- Create: `frontend/src/pages/areas/signRules.ts`, `signRules.test.ts`, `SignPanel.tsx`
- Test: `frontend/src/pages/areas/SignPanel.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `useSetSignLocationMutation`, `AdminSign` (Task 1); `formatCoordinates`, `signPlaceLabel`, `formatDate`, `formatTime` (Task 1 and existing); `PointEditor`, `SignTable`, `AreaDetailPanel` (Task 3).
- Produces:
  - In `pages/areas/signRules.ts`: `MIN_RADIUS_M = 10`, `MAX_RADIUS_M = 500`, `parseRadius(text: string): number | null` and `parseLatLng(text: string): { latitude: number; longitude: number } | null`.
  - In `areasApi.ts`: `useSetSignRadiusMutation()` (`{ signId, radiusM }`) and `useRegenerateQrMutation()` (`signId: number`).
  - `SignPanel({ areaId, sign, label, canIssueQr })`.
  - `EditTarget` gains `{ kind: 'check-in' }`.

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/pages/areas/signRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { parseLatLng, parseRadius } from './signRules';

describe('sign rules (facility-0038, 0045)', () => {
  it('accepts a whole radius from 10 to 500 m, the server’s limits', () => {
    expect(parseRadius('50')).toBe(50);
    expect(parseRadius(' 10 ')).toBe(10);
    expect(parseRadius('500')).toBe(500);
    expect(parseRadius('9')).toBeNull();
    expect(parseRadius('501')).toBeNull();
    expect(parseRadius('50.5')).toBeNull();
    expect(parseRadius('')).toBeNull();
  });

  it('reads coordinates pasted from a map app', () => {
    expect(parseLatLng('13.756300, 100.501800')).toEqual({ latitude: 13.7563, longitude: 100.5018 });
    expect(parseLatLng('13.7563 100.5018')).toEqual({ latitude: 13.7563, longitude: 100.5018 });
    expect(parseLatLng(' -33.8688,151.2093 ')).toEqual({ latitude: -33.8688, longitude: 151.2093 });
    expect(parseLatLng('13.7563')).toBeNull();
    expect(parseLatLng('91, 100')).toBeNull();
    expect(parseLatLng('13.7, 181')).toBeNull();
    expect(parseLatLng('ตึก A')).toBeNull();
  });
});
```

Create `frontend/src/pages/areas/SignPanel.test.tsx`:

```tsx
import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  adminSession,
  areaDetail,
  areaSummary,
  checkInSign,
  officeSummary,
  sign,
  unlocatedSign,
} from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }),
    ...routes,
  });
}

describe('SignPanel (D4, facility-0045, 0038, 0021)', () => {
  it('the check-in sign: its location, the capture link, and the radius', async () => {
    const server = serve({
      'PUT /api/admin/signs/100/radius': ({ body }) => ({
        status: 200,
        body: checkInSign({ radiusM: (body as { radiusM: number }).radiusM }),
      }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-IN' }));

    const editor = screen.getByRole('region', { name: 'ป้ายลงเวลา · AR01 Area ทดลอง' });
    const place = within(editor).getByRole('region', { name: 'พิกัดของป้าย' });
    expect(within(place).getByText('หน้างาน ±14 ม. · 13.756300, 100.501800')).toBeInTheDocument();
    expect(within(place).getByRole('link', { name: 'เก็บพิกัดที่ป้ายด้วยมือถือ (M17) ›' })).toHaveAttribute(
      'href',
      '/admin/areas/1/capture?sign=100',
    );
    // Confirmed on site: a map pick is not offered (the server would refuse it)
    expect(within(place).queryByLabelText('ยังไม่ได้ไปหน้างาน? ใส่พิกัดจากแผนที่ไปก่อน')).not.toBeInTheDocument();

    const radius = within(editor).getByRole('region', { name: 'รัศมีของป้าย' });
    const field = within(radius).getByLabelText('รัศมี (เมตร)');
    expect(field).toHaveValue('50');
    await user.clear(field);
    await user.type(field, '5');
    await user.click(within(radius).getByRole('button', { name: 'บันทึกรัศมี' }));
    expect(within(radius).getByRole('alert')).toHaveTextContent('รัศมีต้องอยู่ระหว่าง 10–500 เมตร');
    expect(server.calls.some((call) => call.method === 'PUT')).toBe(false);

    await user.clear(field);
    await user.type(field, '80');
    await user.click(within(radius).getByRole('button', { name: 'บันทึกรัศมี' }));
    expect(await within(radius).findByRole('status')).toHaveTextContent('บันทึกรัศมี 80 ม. แล้ว ใช้กับการสแกนครั้งต่อไป');
    expect(server.calls.find((call) => call.method === 'PUT')).toMatchObject({
      path: '/api/admin/signs/100/radius',
      body: { radiusM: 80 },
    });
  });

  it('a sign with no location takes coordinates typed from a map, as not confirmed on site', async () => {
    const server = serve({
      'PUT /api/admin/signs/102/location': () => ({
        status: 200,
        body: unlocatedSign({ latitude: 13.7563, longitude: 100.5018, locationSource: 'Map' }),
      }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-02' }));

    const editor = screen.getByRole('region', { name: 'แก้ไขจุด · AR01 Area ทดลอง' });
    const place = within(editor).getByRole('region', { name: 'พิกัดของป้าย' });
    expect(within(place).getByText('ยังไม่มีพิกัด')).toBeInTheDocument();
    const field = within(place).getByLabelText('ยังไม่ได้ไปหน้างาน? ใส่พิกัดจากแผนที่ไปก่อน');
    await user.type(field, 'ตึก A');
    await user.click(within(place).getByRole('button', { name: 'บันทึกพิกัดจากแผนที่' }));
    expect(within(place).getByRole('alert')).toHaveTextContent('ใส่พิกัดแบบ 13.756300, 100.501800');

    await user.clear(field);
    await user.type(field, '13.7563, 100.5018');
    await user.click(within(place).getByRole('button', { name: 'บันทึกพิกัดจากแผนที่' }));
    expect(await within(place).findByRole('status')).toHaveTextContent(
      'บันทึกพิกัดจากแผนที่แล้ว ป้ายนี้ยังไม่ยืนยันหน้างาน',
    );
    expect(server.calls.find((call) => call.method === 'PUT')?.body).toEqual({
      latitude: 13.7563,
      longitude: 100.5018,
      source: 'Map',
    });
  });

  it('a new QR asks once, then offers to print the new sign (facility-0021 item 5)', async () => {
    const server = serve({
      'POST /api/admin/signs/101/regenerate-token': () => ({
        status: 200,
        body: sign({ qrToken: '7c6b5a49-3827-4615-a4b3-c2d1e0f9a8b7', qrIssuedAt: '2026-10-19T03:00:00Z' }),
      }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));

    const qr = within(screen.getByRole('region', { name: 'แก้ไขจุด · AR01 Area ทดลอง' })).getByRole('region', {
      name: 'QR ของป้าย',
    });
    expect(within(qr).getByText('ออก QR ล่าสุด 16/10/2026 09:00')).toBeInTheDocument();

    await user.click(within(qr).getByRole('button', { name: 'ออก QR ใหม่' }));
    const ask = within(qr).getByRole('alertdialog', { name: 'ออก QR ใหม่ของป้าย AR01-01 ห้องน้ำชาย?' });
    expect(ask).toHaveAccessibleDescription(
      'ป้ายที่ติดอยู่ตอนนี้จะสแกนไม่ได้ทันที ต้องพิมพ์ป้ายใหม่ไปติดแทน ประวัติการสแกนไม่เปลี่ยน',
    );
    await user.click(within(ask).getByRole('button', { name: 'ยกเลิก' }));
    expect(server.calls.some((call) => call.path.endsWith('/regenerate-token'))).toBe(false);

    await user.click(within(qr).getByRole('button', { name: 'ออก QR ใหม่' }));
    await user.click(within(qr).getByRole('button', { name: 'ยืนยัน ออก QR ใหม่' }));
    expect(await within(qr).findByText('ออก QR ใหม่แล้ว ป้ายเดิมสแกนไม่ได้แล้ว')).toBeInTheDocument();
    expect(within(qr).getByRole('link', { name: 'พิมพ์ป้ายใหม่ ›' })).toHaveAttribute(
      'href',
      '/admin/print?area=1&sign=101',
    );
    expect(server.calls.filter((call) => call.path === '/api/admin/signs/101/regenerate-token')).toHaveLength(1);
  });

  it('a deactivated point gets no new QR (facility-0021 rule 4)', async () => {
    const detail = areaDetail();
    detail.points[0] = { ...detail.points[0], isActive: false };
    serve({ 'GET /api/admin/areas/1': () => ({ status: 200, body: detail }) });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));

    const qr = screen.getByRole('region', { name: 'QR ของป้าย' });
    expect(within(qr).queryByRole('button', { name: 'ออก QR ใหม่' })).not.toBeInTheDocument();
    expect(within(qr).getByText('ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงออก QR ใหม่ได้')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/areas`
Expected: FAIL. `signRules` does not exist, and there is no "แก้ไข AR01-IN" button or "พิกัดของป้าย" region.

- [ ] **Step 3: The rules**

Create `frontend/src/pages/areas/signRules.ts`:

```ts
/** AdminSignEndpoints.MinRadiusM / MaxRadiusM (facility-0038). */
export const MIN_RADIUS_M = 10;
export const MAX_RADIUS_M = 500;

/** A whole number of metres within the server's limits, or null. */
export function parseRadius(text: string): number | null {
  const radius = Number(text);
  return text.trim() !== '' && Number.isInteger(radius) && radius >= MIN_RADIUS_M && radius <= MAX_RADIUS_M
    ? radius
    : null;
}

/**
 * Plan 10 decision 5: the map fallback is a pair copied from a map app, "13.756300, 100.501800" (a comma or a space
 * between), within the world's bounds. Anything else is null.
 */
export function parseLatLng(text: string): { latitude: number; longitude: number } | null {
  const match = text.trim().match(/^(-?\d+(?:\.\d+)?)\s*[, ]\s*(-?\d+(?:\.\d+)?)$/);
  if (!match) {
    return null;
  }
  const latitude = Number(match[1]);
  const longitude = Number(match[2]);
  return Math.abs(latitude) <= 90 && Math.abs(longitude) <= 180 ? { latitude, longitude } : null;
}
```

- [ ] **Step 4: The two sign saves**

In `frontend/src/pages/areas/areasApi.ts`, replace the first import line:

```ts
import type { AdminPoint } from '../../shared/api/adminSetupApi';
```

with:

```ts
import type { AdminPoint, AdminSign } from '../../shared/api/adminSetupApi';
```

Then replace:

```ts
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const { useCreatePointMutation, useUpdatePointMutation, useSetPointActiveMutation } = areasApi;
```

with:

```ts
      invalidatesTags: ['AdminSetup'],
    }),
    /** facility-0038: applies to later scans; verdicts already stored stay. */
    setSignRadius: build.mutation<AdminSign, { signId: number; radiusM: number }>({
      query: ({ signId, radiusM }) => ({ url: `/admin/signs/${signId}/radius`, method: 'PUT', body: { radiusM } }),
      invalidatesTags: ['AdminSetup'],
    }),
    /** facility-0006: the printed sign stops working at once; scan history is untouched. */
    regenerateQr: build.mutation<AdminSign, number>({
      query: (signId) => ({ url: `/admin/signs/${signId}/regenerate-token`, method: 'POST' }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const {
  useCreatePointMutation,
  useUpdatePointMutation,
  useSetPointActiveMutation,
  useSetSignRadiusMutation,
  useRegenerateQrMutation,
} = areasApi;
```

- [ ] **Step 5: The sign panel**

Create `frontend/src/pages/areas/SignPanel.tsx`:

```tsx
import { useState } from 'react';
import { Link } from 'react-router';
import { useSetSignLocationMutation, type AdminSign } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { formatCoordinates, signPlaceLabel } from '../../shared/utils/labels';
import { formatDate, formatTime } from '../../shared/utils/thaiTime';
import { useRegenerateQrMutation, useSetSignRadiusMutation } from './areasApi';
import { MAX_RADIUS_M, MIN_RADIUS_M, parseLatLng, parseRadius } from './signRules';

interface SignPanelProps {
  areaId: number;
  sign: AdminSign;
  /** What the sign is for, in the new-QR question: the point's name, or "ป้ายลงเวลา". */
  label: string;
  /** facility-0021 rule 4: no new QR for a deactivated point or Area (the server answers 409). */
  canIssueQr: boolean;
}

type Note = { kind: 'ok' | 'error'; text: string } | null;

function NoteLine({ note }: { note: Note }) {
  if (!note) {
    return null;
  }
  return (
    <p
      className={note.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
      role={note.kind === 'ok' ? 'status' : 'alert'}
    >
      {note.text}
    </p>
  );
}

/**
 * Wireframe D4's sign settings (facility-0045, 0038, 0006): three small forms, each with its own save, so no save is
 * ever half done (plan 10 decision 6). The parent keys it by sign id.
 */
export function SignPanel({ areaId, sign, label, canIssueQr }: SignPanelProps) {
  return (
    <div className="areas-sign">
      <SignLocation areaId={areaId} sign={sign} />
      <SignRadius sign={sign} />
      <SignQr areaId={areaId} sign={sign} label={label} canIssueQr={canIssueQr} />
    </div>
  );
}

/** facility-0045 items 2 and 3: captured at the sign on a phone (M17), or typed from a map before going there. */
function SignLocation({ areaId, sign }: { areaId: number; sign: AdminSign }) {
  const [text, setText] = useState('');
  const [note, setNote] = useState<Note>(null);
  const [setLocation, { isLoading }] = useSetSignLocationMutation();
  const titleId = `sign-${sign.id}-place`;

  const saveMapPick = async () => {
    const place = parseLatLng(text);
    if (!place) {
      setNote({ kind: 'error', text: 'ใส่พิกัดแบบ 13.756300, 100.501800' });
      return;
    }
    try {
      await setLocation({ signId: sign.id, ...place, source: 'Map' }).unwrap();
      setText('');
      setNote({ kind: 'ok', text: 'บันทึกพิกัดจากแผนที่แล้ว ป้ายนี้ยังไม่ยืนยันหน้างาน' });
    } catch (error) {
      setNote({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <section className="areas-sign__part" aria-labelledby={titleId}>
      <h4 id={titleId}>พิกัดของป้าย</h4>
      <p>
        {sign.latitude != null && sign.longitude != null
          ? `${signPlaceLabel(sign)} · ${formatCoordinates(sign.latitude, sign.longitude)}`
          : signPlaceLabel(sign)}
      </p>
      <Link to={`/admin/areas/${areaId}/capture?sign=${sign.id}`}>เก็บพิกัดที่ป้ายด้วยมือถือ (M17) ›</Link>
      <p className="app-field__help">
        เปิดหน้านี้บนมือถือ ยืนตรงที่ติดป้าย แล้วกด "ใช้ตำแหน่งตรงนี้" ระบบอ่าน GPS ต่อเนื่อง 30 วินาที แล้วใช้ค่ากลาง
        ให้ยืนนิ่งจนครบเวลา
      </p>
      {sign.locationSource !== 'Site' && (
        <div className="areas-sign__row">
          <label className="app-field">
            ยังไม่ได้ไปหน้างาน? ใส่พิกัดจากแผนที่ไปก่อน
            <input
              className="app-input"
              placeholder="13.756300, 100.501800"
              value={text}
              disabled={isLoading}
              onChange={(event) => setText(event.target.value)}
            />
          </label>
          <button type="button" className="app-button" disabled={isLoading} onClick={() => void saveMapPick()}>
            บันทึกพิกัดจากแผนที่
          </button>
          <p className="app-field__help">
            คัดลอกพิกัดจากแอปแผนที่ พิกัดจากแผนที่จะขึ้นว่า "ยังไม่ยืนยันหน้างาน" จนกว่าจะมาเก็บที่ป้ายจริง
          </p>
        </div>
      )}
      <NoteLine note={note} />
    </section>
  );
}

/** facility-0038: 50 m by default, set per sign. */
function SignRadius({ sign }: { sign: AdminSign }) {
  const [text, setText] = useState(String(sign.radiusM));
  const [note, setNote] = useState<Note>(null);
  const [setRadius, { isLoading }] = useSetSignRadiusMutation();
  const titleId = `sign-${sign.id}-radius`;

  const save = async () => {
    const radiusM = parseRadius(text);
    if (radiusM === null) {
      setNote({ kind: 'error', text: `รัศมีต้องอยู่ระหว่าง ${MIN_RADIUS_M}–${MAX_RADIUS_M} เมตร` });
      return;
    }
    try {
      await setRadius({ signId: sign.id, radiusM }).unwrap();
      setNote({ kind: 'ok', text: `บันทึกรัศมี ${radiusM} ม. แล้ว ใช้กับการสแกนครั้งต่อไป` });
    } catch (error) {
      setNote({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <section className="areas-sign__part" aria-labelledby={titleId}>
      <h4 id={titleId}>รัศมีของป้าย</h4>
      <div className="areas-sign__row">
        <label className="app-field">
          รัศมี (เมตร)
          <input
            className="app-input areas-sign__radius"
            inputMode="numeric"
            value={text}
            disabled={isLoading}
            onChange={(event) => setText(event.target.value)}
          />
        </label>
        <button type="button" className="app-button" disabled={isLoading} onClick={() => void save()}>
          บันทึกรัศมี
        </button>
      </div>
      <p className="app-field__help">สแกนห่างจากป้ายเกินรัศมีจะติดธง ค่าเริ่ม 50 ม.</p>
      <NoteLine note={note} />
    </section>
  );
}

/** facility-0021 item 5, 0006: one question in the page, then the offer to print the new sign. */
function SignQr({ areaId, sign, label, canIssueQr }: SignPanelProps) {
  const [asking, setAsking] = useState(false);
  const [done, setDone] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [regenerate, { isLoading }] = useRegenerateQrMutation();
  const titleId = `sign-${sign.id}-qr`;

  const confirm = async () => {
    try {
      await regenerate(sign.id).unwrap();
      setAsking(false);
      setDone(true);
      setProblem(null);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <section className="areas-sign__part" aria-labelledby={titleId}>
      <h4 id={titleId}>QR ของป้าย</h4>
      <p>{`ออก QR ล่าสุด ${formatDate(sign.qrIssuedAt)} ${formatTime(sign.qrIssuedAt)}`}</p>
      {!canIssueQr && <p className="app-field__help">ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงออก QR ใหม่ได้</p>}
      {canIssueQr && !asking && (
        <button
          type="button"
          className="app-button areas-sign__qr"
          onClick={() => {
            setAsking(true);
            setDone(false);
          }}
        >
          ออก QR ใหม่
        </button>
      )}
      {asking && (
        <div
          className="app-confirm"
          role="alertdialog"
          aria-labelledby={`${titleId}-ask`}
          aria-describedby={`${titleId}-detail`}
        >
          <p id={`${titleId}-ask`} className="app-confirm__title">{`ออก QR ใหม่ของป้าย ${sign.code} ${label}?`}</p>
          <p id={`${titleId}-detail`}>ป้ายที่ติดอยู่ตอนนี้จะสแกนไม่ได้ทันที ต้องพิมพ์ป้ายใหม่ไปติดแทน ประวัติการสแกนไม่เปลี่ยน</p>
          <div className="app-confirm__buttons">
            <button
              type="button"
              className="app-button app-button--warn"
              disabled={isLoading}
              onClick={() => void confirm()}
            >
              {isLoading ? 'กำลังออก QR...' : 'ยืนยัน ออก QR ใหม่'}
            </button>
            {/* The safe choice has the focus */}
            <button type="button" className="app-button" autoFocus disabled={isLoading} onClick={() => setAsking(false)}>
              ยกเลิก
            </button>
          </div>
        </div>
      )}
      {done && (
        <p className="app-note app-note--info" role="status">
          <span>ออก QR ใหม่แล้ว ป้ายเดิมสแกนไม่ได้แล้ว</span>{' '}
          <Link to={`/admin/print?area=${areaId}&sign=${sign.id}`}>พิมพ์ป้ายใหม่ ›</Link>
        </p>
      )}
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
    </section>
  );
}
```

- [ ] **Step 6: Open the check-in sign, and show the sign panel under a point**

In `frontend/src/pages/areas/SignTable.tsx`, replace:

```tsx
export type EditTarget = { kind: 'point'; pointId: number } | { kind: 'new-point' };
```

with:

```tsx
export type EditTarget = { kind: 'point'; pointId: number } | { kind: 'new-point' } | { kind: 'check-in' };
```

and replace:

```tsx
            <td className="areas-actions">{area.isActive && <Link to={printLink(checkInSign)}>พิมพ์</Link>}</td>
```

with:

```tsx
            <td className="areas-actions">
              <button
                type="button"
                className="app-link-button"
                aria-label={`แก้ไข ${checkInSign.code}`}
                onClick={() => onEdit({ kind: 'check-in' })}
              >
                แก้ไข
              </button>
              {area.isActive && <Link to={printLink(checkInSign)}>พิมพ์</Link>}
            </td>
```

In `frontend/src/pages/areas/AreaDetailPanel.tsx`, add the import below the `PointEditor` import:

```tsx
import { SignPanel } from './SignPanel';
```

and replace:

```tsx
          onClose={() => setEditing(null)}
        />
      )}
    </section>
```

with:

```tsx
          onClose={() => setEditing(null)}
        />
      )}
      {editing?.kind === 'check-in' && (
        <section className="areas-editor app-panel" aria-labelledby="checkin-editor">
          <h3 id="checkin-editor">{`ป้ายลงเวลา · ${area.code} ${area.name}`}</h3>
          <SignPanel
            key={detail.checkInSign.id}
            areaId={area.id}
            sign={detail.checkInSign}
            label="ป้ายลงเวลา"
            canIssueQr={area.isActive}
          />
          <div className="areas-editor__buttons">
            <button type="button" className="app-button" onClick={() => setEditing(null)}>
              ปิด
            </button>
          </div>
        </section>
      )}
    </section>
```

In `frontend/src/pages/areas/PointEditor.tsx`, add the import below the `areasApi` import:

```tsx
import { SignPanel } from './SignPanel';
```

and replace (the end of `PointEditor`, the only place this text appears):

```tsx
          )}
        </div>
      )}
    </section>
  );
}
```

with:

```tsx
          )}
        </div>
      )}
      {point && (
        <SignPanel
          key={point.sign.id}
          areaId={area.id}
          sign={point.sign}
          label={point.name}
          canIssueQr={area.isActive && point.isActive}
        />
      )}
    </section>
  );
}
```

- [ ] **Step 7: The styles**

Append to `frontend/src/pages/areas/areas.css`:

```css
.areas-sign {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 12px;
  padding-top: 12px;
  border-top: 1px solid var(--color-border);
}

.areas-sign__part {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 14px;
}

.areas-sign__part h4 {
  font-size: 14px;
  font-weight: 700;
}

.areas-sign__row {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 8px;
}

.areas-sign__radius {
  width: 90px;
}

.areas-sign__qr {
  align-self: flex-start;
}
```

- [ ] **Step 8: Run the tests, the typecheck and the lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. Frontend tests: 183 (177 + 2 rules + 4 panel).

- [ ] **Step 9: Handoff entry**

```markdown
- <date> · Task 4 done: sign settings on D4 for the check-in sign and every point: location (site capture link to M17, or coordinates typed from a map saved as Map and hidden once confirmed on site), radius 10–500 m, a new QR asked once in the page then "พิมพ์ป้ายใหม่ ›"; none for a deactivated point or Area. Frontend tests <n> passed. Next: Task 5 (Area form D10).
```

- [ ] **Step 10: Commit**

```bash
git add docs/handoff.md frontend/src
git commit -m "feat(frontend): sign settings on the Areas page: location, radius, new QR"
```

---

### Task 5: Area form (D10): add or edit an Area, deactivate it

**Files:**
- Create: `frontend/src/pages/area-form/index.ts`, `AreaFormPage.tsx`, `AreaForm.tsx`, `AreaActiveSection.tsx`, `areaFormApi.ts`, `areaFormRules.ts`, `areaForm.css`
- Modify: `frontend/src/router.tsx`
- Test: `frontend/src/pages/area-form/AreaFormPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `useGetAreaQuery`, `useGetBuildingsQuery`, `useGetCleanersQuery`, `AreaDetail`, `Building`, `CleanerOption`, `ShiftPattern` (Task 1); `shiftPatternLabel`, `shiftShortLabel` (Task 1); fixtures `buildings`, `cleaners`, `areaDetail`, `areaSummary`, `officeSummary`, `unlocatedSign` (Task 1). The Areas page at `/admin/areas/:areaId` (Task 2) is where a save lands.
- Produces:
  - `AreaFormPage` at `/admin/areas/new` and `/admin/areas/:areaId/edit`.
  - In `areaFormApi.ts`: `AreaFormBody = { name: string; buildingId: number; shiftPattern: ShiftPattern; dayCleanerId: number | null; nightCleanerId: number | null }`, and the hooks `useCreateAreaMutation()` (`AreaFormBody & { code: string }`), `useUpdateAreaMutation()` (`{ areaId, body }`) and `useSetAreaActiveMutation()` (`{ areaId, active }`).

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/pages/area-form/AreaFormPage.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  adminSession,
  areaDetail,
  areaSummary,
  buildings,
  cleaners,
  officeSummary,
  unlocatedSign,
} from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'GET /api/admin/cleaners': () => ({ status: 200, body: cleaners }),
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }),
    ...routes,
  });
}

const optionTexts = (select: HTMLElement) => within(select).getAllByRole('option').map((option) => option.textContent);

describe('AreaFormPage (D10, facility-0045)', () => {
  it('adds an Area with its code, building, shift pattern and the free Cleaners of each shift', async () => {
    const created = areaDetail({
      area: areaSummary({
        id: 3,
        code: 'AR03',
        name: 'Area ใหม่',
        dayCleaner: { id: 23, employeeId: '650003', displayName: 'แม่บ้านว่าง' },
        nightCleaner: undefined,
        activePointCount: 0,
      }),
      checkInSign: unlocatedSign({ id: 300, code: 'AR03-IN' }),
      points: [],
    });
    const server = serve({
      'POST /api/admin/areas': () => ({ status: 201, body: created }),
      'GET /api/admin/areas/3': () => ({ status: 200, body: created }),
    });
    const { user, router } = await renderApp('/admin/areas/new');

    expect(await screen.findByRole('heading', { level: 1, name: 'เพิ่ม Area' })).toBeInTheDocument();
    const day = await screen.findByLabelText('แม่บ้านประจำกะเช้า');
    // D10: only the shift's Cleaners who have no Area yet
    expect(optionTexts(day)).toEqual(['— ยังไม่มี —', 'แม่บ้านว่าง (650003)']);
    expect(optionTexts(screen.getByLabelText('แม่บ้านประจำกะดึก'))).toEqual(['— ยังไม่มี —', 'แม่บ้านว่างกะดึก (650004)']);

    await user.type(screen.getByLabelText('รหัส Area'), 'AR03');
    await user.type(screen.getByLabelText('ชื่อ Area'), 'Area ใหม่');
    await user.selectOptions(screen.getByLabelText('ตึก'), 'ตึกทดลอง');
    await user.selectOptions(day, 'แม่บ้านว่าง (650003)');
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/areas/3'));
    expect(server.calls.find((call) => call.method === 'POST' && call.path === '/api/admin/areas')?.body).toEqual({
      code: 'AR03',
      name: 'Area ใหม่',
      buildingId: 1,
      shiftPattern: 'DayAndNight',
      dayCleanerId: 23,
      nightCleanerId: null,
    });
  });

  it('says what is missing before sending anything', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/areas/new');

    await user.click(await screen.findByRole('button', { name: 'บันทึก' }));
    expect(screen.getByRole('alert')).toHaveTextContent('ใส่รหัส Area ก่อน');
    await user.type(screen.getByLabelText('รหัส Area'), 'AR03');
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));
    expect(screen.getByRole('alert')).toHaveTextContent('ใส่ชื่อ Area ก่อน');
    await user.type(screen.getByLabelText('ชื่อ Area'), 'Area ใหม่');
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));
    expect(screen.getByRole('alert')).toHaveTextContent('เลือกตึกก่อน');
    expect(server.calls.some((call) => call.method === 'POST' && call.path === '/api/admin/areas')).toBe(false);
  });

  it('edits an Area; going day-only hides the night Cleaner and warns that night rounds go', async () => {
    const server = serve({
      'PUT /api/admin/areas/1': () => ({
        status: 200,
        body: areaDetail({ area: areaSummary({ shiftPattern: 'DayOnly', nightCleaner: undefined }) }),
      }),
    });
    const { user, router } = await renderApp('/admin/areas/1/edit');

    expect(await screen.findByRole('heading', { level: 1, name: 'แก้ไข Area · AR01 Area ทดลอง' })).toBeInTheDocument();
    expect(screen.getByText('รหัส Area AR01 (แก้ไม่ได้ พิมพ์อยู่บนทุกป้าย)')).toBeInTheDocument();
    expect(screen.getByLabelText('ชื่อ Area')).toHaveValue('Area ทดลอง');
    expect(screen.getByLabelText('ตึก')).toHaveValue('1');
    expect(screen.getByLabelText('แม่บ้านประจำกะเช้า')).toHaveValue('21');
    expect(screen.getByLabelText('แม่บ้านประจำกะดึก')).toHaveValue('22');

    await user.click(screen.getByRole('radio', { name: 'เฉพาะกะเช้า' }));
    expect(screen.queryByLabelText('แม่บ้านประจำกะดึก')).not.toBeInTheDocument();
    expect(
      screen.getByText('บันทึกแล้วเวลารอบกะดึกของทุกจุดใน Area นี้จะถูกลบ และแม่บ้านกะดึกออกจาก Area'),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/areas/1'));
    expect(server.calls.find((call) => call.method === 'PUT')?.body).toEqual({
      name: 'Area ทดลอง',
      buildingId: 1,
      shiftPattern: 'DayOnly',
      dayCleanerId: 21,
      nightCleanerId: null,
    });
  });

  it('shows the server’s refusal and stays on the form', async () => {
    serve({ 'POST /api/admin/areas': () => ({ status: 409, body: { message: 'รหัส Area นี้มีแล้ว' } }) });
    const { user, router } = await renderApp('/admin/areas/new');

    await user.type(await screen.findByLabelText('รหัส Area'), 'AR01');
    await user.type(screen.getByLabelText('ชื่อ Area'), 'Area ซ้ำ');
    await user.selectOptions(screen.getByLabelText('ตึก'), 'ตึกทดลอง');
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('รหัส Area นี้มีแล้ว');
    expect(router.state.location.pathname).toBe('/admin/areas/new');
  });

  it('deactivates the Area and can turn it back on (no delete)', async () => {
    let active = true;
    const server = serve({
      'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail({ area: areaSummary({ isActive: active }) }) }),
      'POST /api/admin/areas/1/deactivate': () => {
        active = false;
        return { status: 200, body: areaDetail({ area: areaSummary({ isActive: false }) }) };
      },
    });
    const { user } = await renderApp('/admin/areas/1/edit');

    expect(await screen.findByText('ป้ายลงเวลา AR01-IN ระบบสร้างให้เองตอนเพิ่ม Area')).toBeInTheDocument();
    expect(screen.getByText('จุดทำความสะอาด 2 จุด · เพิ่มหรือแก้ที่หน้า จัดการ Area และป้าย')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ปิดใช้งาน Area นี้' }));

    expect(await screen.findByText('ปิดใช้งาน AR01 แล้ว')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'เปิดใช้งาน Area นี้' })).toBeInTheDocument();
    expect(screen.getByText('Area นี้ปิดใช้งานอยู่ ป้ายทุกป้ายสแกนไม่ได้')).toBeInTheDocument();
    expect(server.calls.some((call) => call.path === '/api/admin/areas/1/deactivate')).toBe(true);
  });
});
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/area-form`
Expected: FAIL. `/admin/areas/new` opens the Areas page, which treats "new" as a bad id, and `/admin/areas/1/edit` is not found.

- [ ] **Step 3: The API slice and the rules**

Create `frontend/src/pages/area-form/areaFormApi.ts`:

```ts
import type { AreaDetail, ShiftPattern } from '../../shared/api/adminSetupApi';
import { baseApi } from '../../shared/api/baseApi';

/** CreateAreaRequest / UpdateAreaRequest. A null Cleaner leaves that shift empty and takes its Cleaner off. */
export interface AreaFormBody {
  name: string;
  buildingId: number;
  shiftPattern: ShiftPattern;
  dayCleanerId: number | null;
  nightCleanerId: number | null;
}

/** The D10 page's saves (facility-0045). The code goes only with a new Area: it is fixed once created. */
const areaFormApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    createArea: build.mutation<AreaDetail, AreaFormBody & { code: string }>({
      query: (body) => ({ url: '/admin/areas', method: 'POST', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    updateArea: build.mutation<AreaDetail, { areaId: number; body: AreaFormBody }>({
      query: ({ areaId, body }) => ({ url: `/admin/areas/${areaId}`, method: 'PUT', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    setAreaActive: build.mutation<AreaDetail, { areaId: number; active: boolean }>({
      query: ({ areaId, active }) => ({
        url: `/admin/areas/${areaId}/${active ? 'activate' : 'deactivate'}`,
        method: 'POST',
      }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const { useCreateAreaMutation, useUpdateAreaMutation, useSetAreaActiveMutation } = areaFormApi;
```

Create `frontend/src/pages/area-form/areaFormRules.ts`:

```ts
import type { AreaDetail, CleanerOption, ShiftPattern } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import type { AreaFormBody } from './areaFormApi';

/** The form as typed: selects hold ids as strings, '' for none. */
export interface AreaDraft {
  code: string;
  name: string;
  buildingId: string;
  shiftPattern: ShiftPattern;
  dayCleanerId: string;
  nightCleanerId: string;
}

/** Empty for "+ เพิ่ม Area", the Area's own values for "แก้ไข Area" (D10). */
export function draftOf(detail?: AreaDetail): AreaDraft {
  const area = detail?.area;
  return {
    code: '',
    name: area?.name ?? '',
    buildingId: area ? String(area.buildingId) : '',
    shiftPattern: area?.shiftPattern ?? 'DayAndNight',
    dayCleanerId: area?.dayCleaner ? String(area.dayCleaner.id) : '',
    nightCleanerId: area?.nightCleaner ? String(area.nightCleaner.id) : '',
  };
}

/** D10: "รายชื่อแสดงเฉพาะแม่บ้านที่ยังไม่มี Area ในกะนั้น", plus this Area's own Cleaner when editing. */
export function cleanerChoices(cleaners: CleanerOption[], shift: Shift, areaId: number | undefined): CleanerOption[] {
  return cleaners.filter((cleaner) => cleaner.shift === shift && (cleaner.areaId == null || cleaner.areaId === areaId));
}

/** What the page checks before sending; the server checks the rest (code format, slots) and its message is shown. */
export function draftProblem(draft: AreaDraft, isNew: boolean): string | null {
  if (isNew && draft.code.trim() === '') {
    return 'ใส่รหัส Area ก่อน';
  }
  if (draft.name.trim() === '') {
    return 'ใส่ชื่อ Area ก่อน';
  }
  if (draft.buildingId === '') {
    return 'เลือกตึกก่อน';
  }
  return null;
}

/** A day-only Area has no night Cleaner (the server refuses one). */
export function formBody(draft: AreaDraft): AreaFormBody {
  return {
    name: draft.name.trim(),
    buildingId: Number(draft.buildingId),
    shiftPattern: draft.shiftPattern,
    dayCleanerId: draft.dayCleanerId ? Number(draft.dayCleanerId) : null,
    nightCleanerId: draft.shiftPattern === 'DayOnly' || !draft.nightCleanerId ? null : Number(draft.nightCleanerId),
  };
}
```

- [ ] **Step 4: The form**

Create `frontend/src/pages/area-form/AreaForm.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import type { AreaDetail, Building, CleanerOption } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftPatternLabel, shiftShortLabel } from '../../shared/utils/labels';
import { useCreateAreaMutation, useUpdateAreaMutation } from './areaFormApi';
import { cleanerChoices, draftOf, draftProblem, formBody, type AreaDraft } from './areaFormRules';

interface AreaFormProps {
  /** Missing: a new Area. */
  detail?: AreaDetail;
  buildings: Building[];
  cleaners: CleanerOption[];
}

/** Wireframe D10's form. A save opens the Area on the D4 page. The page keys it by Area id. */
export function AreaForm({ detail, buildings, cleaners }: AreaFormProps) {
  const navigate = useNavigate();
  const isNew = !detail;
  const areaId = detail?.area.id;
  const [draft, setDraft] = useState<AreaDraft>(() => draftOf(detail));
  const [problem, setProblem] = useState<string | null>(null);
  const [createArea, created] = useCreateAreaMutation();
  const [updateArea, updated] = useUpdateAreaMutation();
  const sending = created.isLoading || updated.isLoading;
  const set = (patch: Partial<AreaDraft>) => setDraft((current) => ({ ...current, ...patch }));
  const losesNightRounds = detail?.area.shiftPattern === 'DayAndNight' && draft.shiftPattern === 'DayOnly';

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const found = draftProblem(draft, isNew);
    setProblem(found);
    if (found) {
      return;
    }
    try {
      const saved =
        areaId === undefined
          ? await createArea({ code: draft.code.trim(), ...formBody(draft) }).unwrap()
          : await updateArea({ areaId, body: formBody(draft) }).unwrap();
      navigate(`/admin/areas/${saved.area.id}`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className="areaform-form app-panel" onSubmit={(event) => void submit(event)}>
      {isNew ? (
        <div className="app-field">
          <label htmlFor="areaform-code">รหัส Area</label>
          <input
            id="areaform-code"
            className="app-input"
            value={draft.code}
            maxLength={20}
            disabled={sending}
            onChange={(event) => set({ code: event.target.value })}
          />
          {/* Outside the label, so the field's name stays "รหัส Area" */}
          <span className="app-field__help">ตัวอักษรอังกฤษหรือตัวเลข 2–20 ตัว เช่น AR03 · พิมพ์บนทุกป้ายของ Area แก้ทีหลังไม่ได้</span>
        </div>
      ) : (
        <p className="areaform-fixed">{`รหัส Area ${detail.area.code} (แก้ไม่ได้ พิมพ์อยู่บนทุกป้าย)`}</p>
      )}
      <label className="app-field">
        ชื่อ Area
        <input
          className="app-input"
          value={draft.name}
          maxLength={150}
          disabled={sending}
          onChange={(event) => set({ name: event.target.value })}
        />
      </label>
      <div className="app-field">
        <label htmlFor="areaform-building">ตึก</label>
        <select
          id="areaform-building"
          className="app-input"
          value={draft.buildingId}
          disabled={sending}
          onChange={(event) => set({ buildingId: event.target.value })}
        >
          <option value="">เลือกตึก</option>
          {buildings.map((building) => (
            <option key={building.id} value={String(building.id)}>
              {building.name}
            </option>
          ))}
        </select>
      </div>
      <fieldset className="areaform-choice" disabled={sending}>
        <legend>รูปแบบกะ</legend>
        {(['DayAndNight', 'DayOnly'] as const).map((pattern) => (
          <label key={pattern}>
            <input
              type="radio"
              name="areaform-pattern"
              checked={draft.shiftPattern === pattern}
              onChange={() => set({ shiftPattern: pattern })}
            />{' '}
            {shiftPatternLabel[pattern]}
          </label>
        ))}
      </fieldset>
      <CleanerSelect
        shift="Day"
        value={draft.dayCleanerId}
        choices={cleanerChoices(cleaners, 'Day', areaId)}
        disabled={sending}
        onChange={(dayCleanerId) => set({ dayCleanerId })}
      />
      {draft.shiftPattern === 'DayAndNight' && (
        <CleanerSelect
          shift="Night"
          value={draft.nightCleanerId}
          choices={cleanerChoices(cleaners, 'Night', areaId)}
          disabled={sending}
          onChange={(nightCleanerId) => set({ nightCleanerId })}
        />
      )}
      <p className="app-field__help">
        Area ละ 1 คนต่อกะ · รายชื่อแสดงเฉพาะแม่บ้านที่ยังไม่มี Area ในกะนั้น · เปลี่ยนเป็น "เฉพาะกะเช้า"
        แล้วช่องกะดึกและเวลารอบกะดึกหายไป
      </p>
      {losesNightRounds && (
        <p className="app-note app-note--warn">บันทึกแล้วเวลารอบกะดึกของทุกจุดใน Area นี้จะถูกลบ และแม่บ้านกะดึกออกจาก Area</p>
      )}
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="areaform-buttons">
        <button type="submit" className="app-button app-button--primary" disabled={sending}>
          {sending ? 'กำลังบันทึก...' : 'บันทึก'}
        </button>
        <Link className="app-button" to={areaId === undefined ? '/admin/areas' : `/admin/areas/${areaId}`}>
          ยกเลิก
        </Link>
      </div>
    </form>
  );
}

interface CleanerSelectProps {
  shift: Shift;
  value: string;
  choices: CleanerOption[];
  disabled: boolean;
  onChange: (value: string) => void;
}

/** D10: "แม่บ้านประจำกะเช้า สมศรี (650123)". */
function CleanerSelect({ shift, value, choices, disabled, onChange }: CleanerSelectProps) {
  const id = `areaform-cleaner-${shift}`;
  return (
    <div className="app-field">
      <label htmlFor={id}>{`แม่บ้านประจำ${shiftShortLabel[shift]}`}</label>
      <select
        id={id}
        className="app-input"
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">— ยังไม่มี —</option>
        {choices.map((cleaner) => (
          <option key={cleaner.id} value={String(cleaner.id)}>
            {`${cleaner.displayName} (${cleaner.employeeId})`}
          </option>
        ))}
      </select>
    </div>
  );
}
```

Create `frontend/src/pages/area-form/AreaActiveSection.tsx`:

```tsx
import { useState } from 'react';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useSetAreaActiveMutation } from './areaFormApi';

/** Wireframe D10's lower half: the Area's signs, and deactivation (no delete; history stays). */
export function AreaActiveSection({ detail }: { detail: AreaDetail }) {
  const { area } = detail;
  const [setAreaActive, { isLoading }] = useSetAreaActiveMutation();
  const [note, setNote] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null);
  const activePoints = detail.points.filter((point) => point.isActive).length;

  const toggle = async () => {
    try {
      await setAreaActive({ areaId: area.id, active: !area.isActive }).unwrap();
      setNote({ kind: 'ok', text: `${area.isActive ? 'ปิด' : 'เปิด'}ใช้งาน ${area.code} แล้ว` });
    } catch (error) {
      setNote({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <>
      <section className="app-panel" aria-labelledby="areaform-signs">
        <h2 id="areaform-signs">ป้ายของ Area นี้</h2>
        <p>{`ป้ายลงเวลา ${detail.checkInSign.code} ระบบสร้างให้เองตอนเพิ่ม Area`}</p>
        <p>{`จุดทำความสะอาด ${activePoints} จุด · เพิ่มหรือแก้ที่หน้า จัดการ Area และป้าย`}</p>
      </section>
      <section className="app-panel" aria-labelledby="areaform-active">
        <h2 id="areaform-active">{area.isActive ? 'ปิดใช้งาน Area' : 'เปิดใช้งาน Area'}</h2>
        {area.isActive ? (
          <p>Area และทุกป้ายหายจาก Dashboard และสแกนไม่ได้ ประวัติยังอยู่ เปิดกลับได้ ไม่มีปุ่มลบ</p>
        ) : (
          <p className="app-note app-note--warn">Area นี้ปิดใช้งานอยู่ ป้ายทุกป้ายสแกนไม่ได้</p>
        )}
        <button
          type="button"
          className={area.isActive ? 'app-button app-button--warn' : 'app-button'}
          disabled={isLoading}
          onClick={() => void toggle()}
        >
          {area.isActive ? 'ปิดใช้งาน Area นี้' : 'เปิดใช้งาน Area นี้'}
        </button>
        {note && (
          <p
            className={note.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
            role={note.kind === 'ok' ? 'status' : 'alert'}
          >
            {note.text}
          </p>
        )}
      </section>
    </>
  );
}
```

- [ ] **Step 5: The page and its routes**

Create `frontend/src/pages/area-form/AreaFormPage.tsx`:

```tsx
import { Link, useParams } from 'react-router';
import { useGetAreaQuery, useGetBuildingsQuery, useGetCleanersQuery } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AreaActiveSection } from './AreaActiveSection';
import { AreaForm } from './AreaForm';
import './areaForm.css';

/** facility-0045, wireframe D10: "+ เพิ่ม Area" opens the form empty, "แก้ไข Area" opens it filled. */
export function AreaFormPage() {
  const { areaId } = useParams();
  const id = areaId === undefined ? undefined : Number(areaId);
  const buildings = useGetBuildingsQuery();
  const cleaners = useGetCleanersQuery();
  const area = useGetAreaQuery(id ?? 0, { skip: id === undefined });
  const detail = id === undefined ? undefined : area.currentData;
  const error = buildings.error ?? cleaners.error ?? area.error;
  const title =
    id === undefined ? 'เพิ่ม Area' : detail ? `แก้ไข Area · ${detail.area.code} ${detail.area.name}` : 'แก้ไข Area';

  return (
    <main className="areaform-page">
      <Link to={id === undefined ? '/admin/areas' : `/admin/areas/${id}`}>‹ จัดการ Area และป้าย</Link>
      <h1>{title}</h1>
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {buildings.data && cleaners.data && (id === undefined || detail) ? (
        <AreaForm key={id ?? 'new'} detail={detail} buildings={buildings.data} cleaners={cleaners.data} />
      ) : (
        !error && <p className="app-muted">กำลังโหลด...</p>
      )}
      {detail && <AreaActiveSection detail={detail} />}
      <p className="app-muted">การบันทึกทุกครั้งเก็บลงประวัติ Admin</p>
    </main>
  );
}
```

Create `frontend/src/pages/area-form/index.ts`:

```ts
export { AreaFormPage } from './AreaFormPage';
```

Create `frontend/src/pages/area-form/areaForm.css`:

```css
/* Wireframe D10 (facility-0045). Class prefix: areaform- */

.areaform-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
  max-width: 720px;
}

.areaform-page h1 {
  font-size: 24px;
  font-weight: 700;
}

.areaform-form {
  gap: 12px;
}

.areaform-fixed {
  font-size: 14px;
}

.areaform-choice {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  margin: 0;
  padding: 10px 12px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  font-size: 14px;
}

.areaform-choice legend {
  padding: 0 4px;
  font-weight: 600;
}

.areaform-buttons {
  display: flex;
  gap: 8px;
}
```

In `frontend/src/router.tsx`, add the import below the `AreasPage` import:

```tsx
import { AreaFormPage } from './pages/area-form';
```

and replace:

```tsx
              { path: 'admin/areas/:areaId?', element: <AreasPage /> },
```

with:

```tsx
              { path: 'admin/areas/:areaId?', element: <AreasPage /> },
              // facility-0045, wireframe D10 ("new" outranks the id above)
              { path: 'admin/areas/new', element: <AreaFormPage /> },
              { path: 'admin/areas/:areaId/edit', element: <AreaFormPage /> },
```

- [ ] **Step 6: Run the tests, the typecheck and the lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. Frontend tests: 188 (183 + 5).

- [ ] **Step 7: Handoff entry**

```markdown
- <date> · Task 5 done: Area form D10 (/admin/areas/new, /admin/areas/:areaId/edit): code (new only, fixed after), name, building, shift pattern, one Cleaner per shift from those without an Area, a warning that going day-only removes night rounds and the night Cleaner, the server's refusal shown, a save opens the Area on D4; the Area's signs and deactivate / activate on edit. Frontend tests <n> passed. Next: Task 6 (print signs).
```

- [ ] **Step 8: Commit**

```bash
git add docs/handoff.md frontend/src
git commit -m "feat(frontend): Area form D10: add, edit, deactivate"
```

---

### Task 6: Print signs (D5), with the QR drawn on the computer

**Files:**
- Modify: `frontend/package.json`, `frontend/package-lock.json` (by npm)
- Create: `frontend/src/pages/print-signs/index.ts`, `PrintSignsPage.tsx`, `SignCard.tsx`, `QrCode.tsx`, `qr.ts`, `qr.test.ts`, `printRules.ts`, `printSignsApi.ts`, `print.css`
- Modify: `frontend/src/router.tsx`
- Test: `frontend/src/pages/print-signs/PrintSignsPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `useGetAreasQuery`, `AreaDetail`, `AreaSummary` (Task 1); `roundsText` (Task 1); `formatDate` (Task 1); fixtures (Task 1). Links from D4 (Tasks 2 and 4) open `/admin/print?area=<id>` and `?area=<id>&sign=<signId>`.
- Produces:
  - `PrintSignsPage` at `/admin/print`.
  - `qrPath(text: string): { size: number; d: string }`.
  - `QrCode({ text, label })`.
  - `printableSigns(details: AreaDetail[], signId?: number): PrintSign[]`.
  - `useGetPrintAreasQuery(which: string)`, where `which` is an Area id or `'all'`.

- [ ] **Step 1: Install the QR package**

From `frontend/`:

```bash
npm install uqr@0.1.3
```

`package.json` gains `"uqr": "^0.1.3"` under `dependencies`. Check that `node_modules/uqr/package.json` has no `dependencies` key.

- [ ] **Step 2: Write the failing tests**

Create `frontend/src/pages/print-signs/qr.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { qrPath } from './qr';

describe('qrPath (facility-0021: the QR is drawn on this computer)', () => {
  it('draws a sign URL as dark-module runs, with the standard quiet zone of 4 modules', () => {
    const { size, d } = qrPath('https://facility.example/scan/0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f');

    expect(size).toBe(45); // version 5 (37 modules) plus 4 on each side
    // The top rows of the three finder patterns: top-left, top-right, bottom-left
    expect(d.startsWith('M4 4h7v1h-7z')).toBe(true);
    expect(d).toContain('M34 4h7v1h-7z');
    expect(d).toContain('M4 34h7v1h-7z');
  });
});
```

Create `frontend/src/pages/print-signs/PrintSignsPage.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { adminSession, areaDetail, areaSummary, officeDetail, officeSummary } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

const closedArea = areaSummary({ id: 3, code: 'AR03', name: 'Area ปิด', isActive: false });

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary, closedArea] }),
    'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }),
    'GET /api/admin/areas/2': () => ({ status: 200, body: officeDetail() }),
    ...routes,
  });
}

const cardNames = () => screen.getAllByRole('article').map((card) => card.getAttribute('aria-label'));

describe('PrintSignsPage (D5, facility-0021 item 6)', () => {
  it('prints one Area: the check-in sign and the active points, each with its QR and URL', async () => {
    serve();
    await renderApp('/admin/print?area=1');

    expect(await screen.findByRole('heading', { level: 1, name: 'พิมพ์ป้าย QR · AR01 Area ทดลอง' })).toBeInTheDocument();
    expect(
      await screen.findByText(
        'ป้ายลงเวลา 1 + จุดทำความสะอาด 2 = 3 ป้าย · กระดาษ A4 แผ่นละ 6 ป้าย · 1 แผ่น · ตัดตามเส้นประ',
      ),
    ).toBeInTheDocument();
    expect(cardNames()).toEqual(['ป้าย AR01-IN', 'ป้าย AR01-01', 'ป้าย AR01-02']);

    const checkIn = screen.getByRole('article', { name: 'ป้าย AR01-IN' });
    expect(within(checkIn).getByText('ป้ายลงเวลา')).toBeInTheDocument();
    expect(within(checkIn).getByRole('heading', { name: 'Area ทดลอง' })).toBeInTheDocument();
    expect(within(checkIn).getByText('สแกนตอนเข้างาน ออกพัก กลับจากพัก และเลิกงาน')).toBeInTheDocument();
    expect(
      within(checkIn).getByText(`${window.location.origin}/check-in/9a8b7c6d-5e4f-4a3b-9c2d-1e0f9a8b7c6d`),
    ).toBeInTheDocument();
    expect(within(checkIn).getByRole('img', { name: 'QR AR01-IN' })).toBeInTheDocument();

    const men = screen.getByRole('article', { name: 'ป้าย AR01-01' });
    expect(within(men).getByText('จุดทำความสะอาด')).toBeInTheDocument();
    expect(within(men).getByRole('heading', { name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(within(men).getByText('Area AR01 · ตึก A')).toBeInTheDocument();
    expect(within(men).getByText('สแกนด้วยกล้องมือถือ')).toBeInTheDocument();
    expect(
      within(men).getByText('รอบ เช้า 07:00–09:00, 16:00–18:00 · ดึก 20:00–22:00, 03:00–05:00'),
    ).toBeInTheDocument();
    expect(within(men).getByText('รหัสป้าย AR01-01 · ออก QR 16/10/2026')).toBeInTheDocument();
    expect(
      within(men).getByText(`${window.location.origin}/scan/0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f`),
    ).toBeInTheDocument();
    // facility-0060: the token is shown only inside the sign's URL
    expect(screen.queryByText('0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f')).not.toBeInTheDocument();
    // The test page is plain http, where a phone at the sign cannot read its location
    expect(screen.getByText(`ที่อยู่บนป้าย: ${window.location.origin}`)).toBeInTheDocument();
    expect(
      screen.getByText('ที่อยู่นี้ไม่ใช่ https มือถือจะอ่านตำแหน่งไม่ได้ตอนสแกน พิมพ์จากที่อยู่ https ของเซิร์ฟเวอร์'),
    ).toBeInTheDocument();
  });

  it('A5 puts 2 signs on a sheet', async () => {
    serve();
    const { user } = await renderApp('/admin/print?area=1');
    await screen.findByRole('article', { name: 'ป้าย AR01-IN' });

    await user.click(screen.getByRole('radio', { name: 'A5 (2 ต่อแผ่น)' }));

    expect(
      screen.getByText('ป้ายลงเวลา 1 + จุดทำความสะอาด 2 = 3 ป้าย · กระดาษ A4 แผ่นละ 2 ป้าย · 2 แผ่น · ตัดตามเส้นประ'),
    ).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'แผ่นที่ 1' })).getAllByRole('article')).toHaveLength(2);
    expect(within(screen.getByRole('region', { name: 'แผ่นที่ 2' })).getAllByRole('article')).toHaveLength(1);
  });

  it('prints a single sign after "พิมพ์" on a row or a new QR', async () => {
    serve();
    await renderApp('/admin/print?area=1&sign=101');

    expect(await screen.findByRole('article', { name: 'ป้าย AR01-01' })).toBeInTheDocument();
    expect(cardNames()).toEqual(['ป้าย AR01-01']);
    expect(
      screen.getByText('ป้ายลงเวลา 0 + จุดทำความสะอาด 1 = 1 ป้าย · กระดาษ A4 แผ่นละ 6 ป้าย · 1 แผ่น · ตัดตามเส้นประ'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'พิมพ์ทั้ง Area ›' })).toHaveAttribute('href', '/admin/print?area=1');
  });

  it('prints every active Area, leaving out deactivated Areas and points', async () => {
    const detail = areaDetail();
    detail.points[1] = { ...detail.points[1], isActive: false };
    const server = serve({ 'GET /api/admin/areas/1': () => ({ status: 200, body: detail }) });
    await renderApp('/admin/print?area=all');

    expect(await screen.findByRole('heading', { level: 1, name: 'พิมพ์ป้าย QR · ทุก Area' })).toBeInTheDocument();
    await waitFor(() => expect(cardNames()).toEqual(['ป้าย AR01-IN', 'ป้าย AR01-01', 'ป้าย AR02-IN']));
    expect(
      screen.getByText('ป้ายลงเวลา 2 + จุดทำความสะอาด 1 = 3 ป้าย · กระดาษ A4 แผ่นละ 6 ป้าย · 1 แผ่น · ตัดตามเส้นประ'),
    ).toBeInTheDocument();
    expect(server.calls.some((call) => call.path === '/api/admin/areas/3')).toBe(false);
    expect(screen.getByRole('option', { name: 'ทุก Area (2)' })).toBeInTheDocument();
  });

  it('choosing an Area changes the address, and "พิมพ์" opens the browser’s print', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    serve();
    const { user, router } = await renderApp('/admin/print');

    expect(await screen.findByText('เลือก Area ที่จะพิมพ์')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'พิมพ์' })).toBeDisabled();
    await screen.findByRole('option', { name: 'AR02 Office ทดลอง' });
    await user.selectOptions(screen.getByLabelText('Area'), 'AR02 Office ทดลอง');

    expect(router.state.location.search).toBe('?area=2');
    expect(await screen.findByRole('article', { name: 'ป้าย AR02-IN' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'พิมพ์' }));
    expect(print).toHaveBeenCalledOnce();
  });

  it('a deactivated Area has nothing to print', async () => {
    serve({
      'GET /api/admin/areas/3': () => ({ status: 200, body: areaDetail({ area: closedArea }) }),
    });
    await renderApp('/admin/print?area=3');

    expect(await screen.findByText('Area นี้ปิดใช้งาน ป้ายสแกนไม่ได้ จึงไม่มีป้ายให้พิมพ์')).toBeInTheDocument();
    expect(screen.queryByRole('article')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/print-signs`
Expected: FAIL. `./qr` does not exist, and `/admin/print` is not a route.

- [ ] **Step 4: The QR**

Create `frontend/src/pages/print-signs/qr.ts`:

```ts
import { encode } from 'uqr';

/**
 * facility-0021: the QR is drawn on this computer; no web service sees the token. Error correction M survives a
 * scratched sign. The quiet zone of 4 modules (the QR standard's) is part of the drawing. Each row's runs of dark
 * modules become one rectangle of the SVG path, in module units: `size` is the viewBox side.
 */
export function qrPath(text: string): { size: number; d: string } {
  const { data } = encode(text, { ecc: 'M', border: 4 });
  let d = '';
  data.forEach((row, y) => {
    let x = 0;
    while (x < row.length) {
      if (!row[x]) {
        x += 1;
        continue;
      }
      const start = x;
      while (x < row.length && row[x]) {
        x += 1;
      }
      d += `M${start} ${y}h${x - start}v1h-${x - start}z`;
    }
  });
  return { size: data.length, d };
}
```

Create `frontend/src/pages/print-signs/QrCode.tsx`:

```tsx
import { useMemo } from 'react';
import { qrPath } from './qr';

/** The QR as SVG written by React: no HTML string, no image from elsewhere (facility-0066, 0021). */
export function QrCode({ text, label }: { text: string; label: string }) {
  const { size, d } = useMemo(() => qrPath(text), [text]);
  return (
    <svg className="print-qr" viewBox={`0 0 ${size} ${size}`} role="img" aria-label={label} shapeRendering="crispEdges">
      <rect width={size} height={size} fill="#fff" />
      <path d={d} fill="#000" />
    </svg>
  );
}
```

- [ ] **Step 5: The rules and the API slice**

Create `frontend/src/pages/print-signs/printRules.ts`:

```ts
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { roundsText } from '../../shared/utils/labels';

/** Six signs of about 97 × 94 mm on an A4 sheet (facility-0021 item 6), or two A5 signs for a wall that needs them big. */
export type SignSize = 'small' | 'A5';

export const perSheet: Record<SignSize, number> = { small: 6, A5: 2 };

/** One printed sign (wireframe D5). */
export interface PrintSign {
  key: string;
  kind: 'check-in' | 'point';
  type: string;
  name: string;
  place: string;
  code: string;
  hint: string;
  url: string;
  issuedAt: string;
}

/** Plan 8: a check-in sign opens /check-in/<token>, a point's sign /scan/<token>, on this page's own origin (facility-0067). */
export function signUrl(kind: PrintSign['kind'], token: string): string {
  return `${window.location.origin}/${kind === 'check-in' ? 'check-in' : 'scan'}/${token}`;
}

/**
 * Every sign that can be scanned: the check-in sign and the active points of each active Area, in the D4 order.
 * With `signId`, only that sign.
 */
export function printableSigns(details: AreaDetail[], signId?: number): PrintSign[] {
  const signs = details
    .filter((detail) => detail.area.isActive)
    .flatMap((detail): PrintSign[] => {
      const { area, checkInSign } = detail;
      const place = `Area ${area.code} · ตึก ${area.buildingCode}`;
      const checkIn: PrintSign = {
        key: `sign-${checkInSign.id}`,
        kind: 'check-in',
        type: 'ป้ายลงเวลา',
        name: area.name,
        place,
        code: checkInSign.code,
        hint: 'สแกนตอนเข้างาน ออกพัก กลับจากพัก และเลิกงาน',
        url: signUrl('check-in', checkInSign.qrToken),
        issuedAt: checkInSign.qrIssuedAt,
      };
      const points = detail.points
        .filter((point) => point.isActive)
        .map(
          (point): PrintSign => ({
            key: `sign-${point.sign.id}`,
            kind: 'point',
            type: 'จุดทำความสะอาด',
            name: point.name,
            place,
            code: point.sign.code,
            hint: point.roundWindows.length > 0 ? `รอบ ${roundsText(point.roundWindows)}` : 'สแกนหลังทำความสะอาดเสร็จ',
            url: signUrl('point', point.sign.qrToken),
            issuedAt: point.sign.qrIssuedAt,
          }),
        );
      return [checkIn, ...points];
    });
  return signId === undefined ? signs : signs.filter((sign) => sign.key === `sign-${signId}`);
}

export function sheetsOf<T>(items: T[], size: number): T[][] {
  const sheets: T[][] = [];
  for (let index = 0; index < items.length; index += size) {
    sheets.push(items.slice(index, index + size));
  }
  return sheets;
}

/** Wireframe D5: "ป้ายลงเวลา 1 + จุดทำความสะอาด 4 = 5 ป้าย · กระดาษ A4 แผ่นละ 6 ป้าย · 1 แผ่น · ตัดตามเส้นประ". */
export function summaryLine(signs: PrintSign[], size: SignSize): string {
  const checkIns = signs.filter((sign) => sign.kind === 'check-in').length;
  const sheets = Math.ceil(signs.length / perSheet[size]);
  return `ป้ายลงเวลา ${checkIns} + จุดทำความสะอาด ${signs.length - checkIns} = ${signs.length} ป้าย · กระดาษ A4 แผ่นละ ${perSheet[size]} ป้าย · ${sheets} แผ่น · ตัดตามเส้นประ`;
}

/** Why nothing is printed. */
export function nothingToPrint(details: AreaDetail[], signId: number | undefined): string {
  if (signId !== undefined) {
    return 'ป้ายนี้ปิดใช้งานหรือไม่มีแล้ว';
  }
  if (details.length === 1 && !details[0].area.isActive) {
    return 'Area นี้ปิดใช้งาน ป้ายสแกนไม่ได้ จึงไม่มีป้ายให้พิมพ์';
  }
  return 'ไม่มีป้ายให้พิมพ์';
}
```

Create `frontend/src/pages/print-signs/printSignsApi.ts`:

```ts
import type { AreaDetail, AreaSummary } from '../../shared/api/adminSetupApi';
import { baseApi } from '../../shared/api/baseApi';

const printSignsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /**
     * The Areas to print: one Area by id, or "all" (facility-0021 item 6: every active one, not only what is on
     * screen). The server has no all-signs endpoint, so "all" asks for each active Area's detail.
     */
    getPrintAreas: build.query<AreaDetail[], string>({
      async queryFn(which, _api, _extraOptions, baseQuery) {
        let ids: number[];
        if (which === 'all') {
          const list = await baseQuery('/admin/areas');
          if (list.error) {
            return { error: list.error };
          }
          ids = (list.data as AreaSummary[]).filter((area) => area.isActive).map((area) => area.id);
        } else {
          ids = [Number(which)];
        }
        const answers = await Promise.all(ids.map((id) => baseQuery(`/admin/areas/${id}`)));
        const failed = answers.find((answer) => answer.error);
        if (failed?.error) {
          return { error: failed.error };
        }
        return { data: answers.map((answer) => answer.data as AreaDetail) };
      },
      providesTags: ['AdminSetup'],
    }),
  }),
});

export const { useGetPrintAreasQuery } = printSignsApi;
```

- [ ] **Step 6: The sign and the page**

Create `frontend/src/pages/print-signs/SignCard.tsx`:

```tsx
import { formatDate } from '../../shared/utils/thaiTime';
import type { PrintSign } from './printRules';
import { QrCode } from './QrCode';

/** One sign of wireframe D5 (facility-0021 item 6): what it is, where, the QR, how to scan, and the full URL. */
export function SignCard({ sign }: { sign: PrintSign }) {
  return (
    <article className="print-sign" aria-label={`ป้าย ${sign.code}`}>
      <span className="print-sign__type">{sign.type}</span>
      <h2 className="print-sign__name">{sign.name}</h2>
      <span className="print-sign__place">{sign.place}</span>
      <QrCode text={sign.url} label={`QR ${sign.code}`} />
      <span className="print-sign__scan">สแกนด้วยกล้องมือถือ</span>
      <span className="print-sign__hint">{sign.hint}</span>
      <span className="print-sign__code">{`รหัสป้าย ${sign.code} · ออก QR ${formatDate(sign.issuedAt)}`}</span>
      <span className="print-sign__url">{sign.url}</span>
    </article>
  );
}
```

Create `frontend/src/pages/print-signs/PrintSignsPage.tsx`:

```tsx
import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useGetAreasQuery } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { nothingToPrint, perSheet, printableSigns, sheetsOf, summaryLine, type SignSize } from './printRules';
import { useGetPrintAreasQuery } from './printSignsApi';
import { SignCard } from './SignCard';
import './print.css';

/**
 * facility-0021 item 6, wireframe D5: A4 sheets of QR signs for one Area (?area=<id>), one sign (&sign=<id>) or every
 * active Area (?area=all), printed by the browser. The controls and the app bar are hidden on paper.
 */
export function PrintSignsPage() {
  const [params, setParams] = useSearchParams();
  const which = params.get('area') ?? '';
  const signParam = params.get('sign');
  const signId = signParam ? Number(signParam) : undefined;
  const [size, setSize] = useState<SignSize>('small');
  const areas = useGetAreasQuery();
  const print = useGetPrintAreasQuery(which, { skip: which === '' });
  const details = print.currentData;
  const signs = details ? printableSigns(details, signId) : [];
  const activeAreas = areas.data?.filter((area) => area.isActive) ?? [];
  const heading =
    which === 'all' ? 'ทุก Area' : details?.[0] ? `${details[0].area.code} ${details[0].area.name}` : undefined;

  return (
    <main className="print-page">
      <div className="print-controls">
        <Link to={which && which !== 'all' ? `/admin/areas/${which}` : '/admin/areas'}>
          ‹ กลับไปหน้าจัดการ Area และป้าย
        </Link>
        <h1>{heading ? `พิมพ์ป้าย QR · ${heading}` : 'พิมพ์ป้าย QR'}</h1>
        <div className="print-options">
          <div className="app-field">
            <label htmlFor="print-area">Area</label>
            <select
              id="print-area"
              className="app-input"
              value={which}
              onChange={(event) => setParams({ area: event.target.value })}
            >
              <option value="" disabled>
                เลือก Area
              </option>
              {activeAreas.map((area) => (
                <option key={area.id} value={String(area.id)}>
                  {`${area.code} ${area.name}`}
                </option>
              ))}
              <option value="all">{`ทุก Area (${activeAreas.length})`}</option>
            </select>
          </div>
          <fieldset className="print-size">
            <legend>ขนาดป้าย</legend>
            <label>
              <input type="radio" name="print-size" checked={size === 'small'} onChange={() => setSize('small')} />
              {' เล็ก (6 ต่อแผ่น)'}
            </label>
            <label>
              <input type="radio" name="print-size" checked={size === 'A5'} onChange={() => setSize('A5')} />
              {' A5 (2 ต่อแผ่น)'}
            </label>
          </fieldset>
          <button
            type="button"
            className="app-button app-button--primary"
            disabled={signs.length === 0}
            onClick={() => window.print()}
          >
            พิมพ์
          </button>
        </div>
        {signs.length > 0 && <p>{summaryLine(signs, size)}</p>}
        {signId !== undefined && which !== 'all' && (
          <p className="app-muted">
            พิมพ์ป้ายเดียว <Link to={`/admin/print?area=${which}`}>พิมพ์ทั้ง Area ›</Link>
          </p>
        )}
        <p className="app-muted">{`ที่อยู่บนป้าย: ${window.location.origin}`}</p>
        {window.location.protocol !== 'https:' && (
          <p className="app-note app-note--warn">
            ที่อยู่นี้ไม่ใช่ https มือถือจะอ่านตำแหน่งไม่ได้ตอนสแกน พิมพ์จากที่อยู่ https ของเซิร์ฟเวอร์
          </p>
        )}
        {print.error && (
          <p className="app-note app-note--error" role="alert">
            {getErrorMessage(print.error)}
          </p>
        )}
        {which === '' && <p className="app-note app-note--info">เลือก Area ที่จะพิมพ์</p>}
        {details && signs.length === 0 && <p className="app-note app-note--warn">{nothingToPrint(details, signId)}</p>}
      </div>
      {sheetsOf(signs, perSheet[size]).map((sheet, index) => (
        <section key={index} className={`print-sheet print-sheet--${size}`} aria-label={`แผ่นที่ ${index + 1}`}>
          {sheet.map((sign) => (
            <SignCard key={sign.key} sign={sign} />
          ))}
        </section>
      ))}
      <p className="app-muted print-controls">
        ระบบสร้าง QR ในเครื่องนี้เอง ไม่ส่งรหัสออกไปเว็บภายนอก · ติดเสร็จให้สแกนทดสอบ 1 ครั้ง หน้าจอต้องขึ้นชื่อจุดตรงกับป้าย
      </p>
    </main>
  );
}
```

Create `frontend/src/pages/print-signs/index.ts`:

```ts
export { PrintSignsPage } from './PrintSignsPage';
```

- [ ] **Step 7: The paper**

Create `frontend/src/pages/print-signs/print.css`:

```css
/* Wireframe D5 (facility-0021 item 6). Class prefix: print- */

.print-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.print-controls {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.print-controls h1 {
  font-size: 24px;
  font-weight: 700;
}

.print-options {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 16px;
}

.print-size {
  display: flex;
  gap: 12px;
  margin: 0;
  padding: 6px 10px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  font-size: 14px;
}

.print-size legend {
  padding: 0 4px;
  font-weight: 600;
}

/* One A4 sheet: 210 × 297 mm with an 8 mm margin; signs are cut along the dashed lines */
.print-sheet {
  display: grid;
  width: 210mm;
  height: 297mm;
  margin: 0 auto;
  padding: 8mm;
  background: #fff;
  box-shadow: 0 1px 4px rgb(0 0 0 / 15%);
}

.print-sheet--small {
  grid-template-columns: repeat(2, 1fr);
  grid-template-rows: repeat(3, 1fr);
}

.print-sheet--A5 {
  grid-template-columns: 1fr;
  grid-template-rows: repeat(2, 1fr);
}

.print-sign {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2mm;
  padding: 4mm;
  overflow: hidden;
  border: 1px dashed #9aa3a4;
  color: #000;
  text-align: center;
}

.print-sign__type,
.print-sign__scan {
  font-size: 11pt;
  font-weight: 600;
}

.print-sign__name {
  font-size: 16pt;
  font-weight: 700;
}

.print-sheet--A5 .print-sign__name {
  font-size: 24pt;
}

.print-sign__place,
.print-sign__hint,
.print-sign__code {
  font-size: 9pt;
}

.print-sign__url {
  font-size: 6.5pt;
  color: #333;
  word-break: break-all;
}

.print-qr {
  width: 42mm;
  height: 42mm;
}

.print-sheet--A5 .print-qr {
  width: 80mm;
  height: 80mm;
}

@page {
  size: A4 portrait;
  margin: 0;
}

@media print {
  .print-controls {
    display: none;
  }

  .print-page {
    gap: 0;
  }

  .print-sheet {
    margin: 0;
    box-shadow: none;
    break-after: page;
  }

  .print-sheet:last-of-type {
    break-after: auto;
  }
}
```

- [ ] **Step 8: The route**

In `frontend/src/router.tsx`, add the import below the `AreaFormPage` import:

```tsx
import { PrintSignsPage } from './pages/print-signs';
```

and replace:

```tsx
              { path: 'admin/areas/:areaId/edit', element: <AreaFormPage /> },
```

with:

```tsx
              { path: 'admin/areas/:areaId/edit', element: <AreaFormPage /> },
              // facility-0021 item 6, wireframe D5: ?area=<id>[&sign=<id>] or ?area=all
              { path: 'admin/print', element: <PrintSignsPage /> },
```

- [ ] **Step 9: Run the tests, the typecheck and the lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. Frontend tests: 195 (188 + 1 qr + 6 page).

- [ ] **Step 10: Measure the bundle**

```bash
cd frontend && npm run build && cd ..
```

Note the gzip size of `dist/assets/index-*.js` in Vite's output. It must be ≤ 200 kB (facility-0062). If it is over, stop and report the number to the controller. Do not split the bundle. Delete `frontend/dist` afterwards (git ignores it).

- [ ] **Step 11: Handoff entry**

```markdown
- <date> · Task 6 done: print page D5 (/admin/print?area=<id>[&sign=<id>] or ?area=all): A4 sheets of 6 small or 2 A5 signs with dashed cut lines, each with type, name, Area and building, the QR drawn on the computer (uqr 0.1.3, SVG path, ECC M, quiet zone 4), rounds or attendance events, sign code, QR date and the full URL; deactivated Areas and points left out; the origin printed and a warning when it is not https; "พิมพ์" opens the browser's print with the bar, menu and controls hidden. JS bundle <size> kB gzip. Frontend tests <n> passed. Next: Task 7 (capture M17).
```

- [ ] **Step 12: Commit**

```bash
git add docs/handoff.md frontend/package.json frontend/package-lock.json frontend/src
git commit -m "feat(frontend): print QR signs D5 with the QR drawn on the computer (uqr)"
```

---

### Task 7: Capture a sign's location at the sign (M17)

**Files:**
- Modify: `frontend/src/test/fakeGps.ts`
- Create: `frontend/src/pages/capture/index.ts`, `CapturePage.tsx`, `captureLocation.ts`, `captureLocation.test.ts`, `capture.css`
- Modify: `frontend/src/router.tsx`
- Test: `frontend/src/pages/capture/CapturePage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `useGetAreaQuery`, `useSetSignLocationMutation`, `AdminSign`, `AreaDetail` (Task 1); `formatCoordinates`, `signPlaceLabel` (Task 1); `GpsFix`, `LocationProblem` (`shared/geo/readLocation.ts`); fixtures (Task 1). D4 links here (Tasks 2 and 4).
- Produces:
  - `fakeGpsTrack(answers: FakeTrackAnswer[], everyMs = 1000): { watches: number; cleared: number }` in `src/test/fakeGps.ts`.
  - In `pages/capture/captureLocation.ts`:
    - `CAPTURE_MS = 30_000` and `MIN_READINGS = 3`.
    - `CaptureProgress = { seconds; readings; accuracyM: number | null }`.
    - `CaptureResult = { kind: 'fix'; fix; readings } | { kind: 'too-few'; readings } | { kind: LocationProblem }`.
    - `medianFix(fixes: readonly GpsFix[]): GpsFix`.
    - `accuracyVerdict(accuracyM: number, radiusM: number): { ok: boolean; text: string }`.
    - `startCapture(onProgress): { done: Promise<CaptureResult>; cancel(): void }`.
  - `CapturePage` at `/admin/areas/:areaId/capture`, with an optional `?sign=<signId>`.

- [ ] **Step 1: A fake GPS that keeps reading**

Append to `frontend/src/test/fakeGps.ts`:

```ts
export type FakeTrackAnswer = typeof nearSign | 'denied';

/**
 * Gives the page a navigator.geolocation whose watchPosition sends one answer every `everyMs` (the first after
 * `everyMs`), in order, then stays quiet. 'denied' fails the watch like a refused permission. `watches` counts
 * watchPosition calls; `cleared` counts clearWatch calls. Use with fake timers. test/setup.ts removes it after each test.
 */
export function fakeGpsTrack(answers: FakeTrackAnswer[], everyMs = 1_000) {
  const state = { watches: 0, cleared: 0 };
  const timers = new Map<number, ReturnType<typeof setTimeout>[]>();
  const geolocation = {
    watchPosition(onFix: PositionCallback, onError?: PositionErrorCallback | null) {
      state.watches += 1;
      const id = state.watches;
      timers.set(
        id,
        answers.map((answer, index) =>
          setTimeout(
            () => {
              if (answer === 'denied') {
                onError?.({ code: 1, message: 'denied', PERMISSION_DENIED: 1, POSITION_UNAVAILABLE: 2, TIMEOUT: 3 } as unknown as GeolocationPositionError);
              } else {
                const coords = { ...answer, altitude: null, altitudeAccuracy: null, heading: null, speed: null };
                onFix({ coords, timestamp: Date.now() } as unknown as GeolocationPosition);
              }
            },
            (index + 1) * everyMs,
          ),
        ),
      );
      return id;
    },
    clearWatch(id: number) {
      state.cleared += 1;
      timers.get(id)?.forEach(clearTimeout);
      timers.delete(id);
    },
  };
  Object.defineProperty(navigator, 'geolocation', { value: geolocation, configurable: true });
  return state;
}
```

- [ ] **Step 2: Write the failing tests**

Create `frontend/src/pages/capture/captureLocation.test.ts`:

```ts
import { afterEach, describe, expect, it, vi } from 'vitest';
import { fakeGpsTrack } from '../../test/fakeGps';
import { accuracyVerdict, medianFix, startCapture, type CaptureProgress } from './captureLocation';

afterEach(() => vi.useRealTimers());

const reading = (latitude: number, longitude: number, accuracy: number) => ({ latitude, longitude, accuracy });

describe('captureLocation (facility-0045 item 2)', () => {
  it('takes the median of each value on its own, so one wild reading moves none of them', () => {
    expect(
      medianFix([
        { latitude: 13.7563, longitude: 100.5018, accuracyM: 20 },
        { latitude: 13.757, longitude: 100.502, accuracyM: 12 },
        { latitude: 13.7564, longitude: 100.5019, accuracyM: 16 },
      ]),
    ).toEqual({ latitude: 13.7564, longitude: 100.5019, accuracyM: 16 });
  });

  it('with an even count it takes the middle two, and rounds the accuracy up to whole metres', () => {
    expect(
      medianFix([
        { latitude: 10, longitude: 100, accuracyM: 10 },
        { latitude: 20, longitude: 110, accuracyM: 13 },
        { latitude: 30, longitude: 120, accuracyM: 20 },
        { latitude: 40, longitude: 130, accuracyM: 30 },
      ]),
    ).toEqual({ latitude: 25, longitude: 115, accuracyM: 17 });
  });

  it('reads for 30 s, reporting the seconds, the readings and the latest accuracy', async () => {
    vi.useFakeTimers();
    const gps = fakeGpsTrack([reading(13.7563, 100.5018, 20), reading(13.757, 100.502, 12), reading(13.7564, 100.5019, 16)]);
    const progress: CaptureProgress[] = [];
    const capture = startCapture((step) => progress.push(step));

    await vi.advanceTimersByTimeAsync(30_000);

    await expect(capture.done).resolves.toEqual({
      kind: 'fix',
      readings: 3,
      fix: { latitude: 13.7564, longitude: 100.5019, accuracyM: 16 },
    });
    expect(progress.find((step) => step.seconds === 18)).toEqual({ seconds: 18, readings: 3, accuracyM: 16 });
    expect(gps.cleared).toBe(1);
  });

  it('gives too-few with fewer than 3 readings in 30 s', async () => {
    vi.useFakeTimers();
    fakeGpsTrack([reading(13.7563, 100.5018, 20), reading(13.7564, 100.5019, 16)]);
    const capture = startCapture(() => undefined);

    await vi.advanceTimersByTimeAsync(30_000);

    await expect(capture.done).resolves.toEqual({ kind: 'too-few', readings: 2 });
  });

  it('stops at once when the location is refused', async () => {
    vi.useFakeTimers();
    const gps = fakeGpsTrack(['denied']);
    const capture = startCapture(() => undefined);

    await vi.advanceTimersByTimeAsync(1_000);

    await expect(capture.done).resolves.toEqual({ kind: 'denied' });
    expect(gps.cleared).toBe(1);
  });

  it('on plain http it asks for no location', async () => {
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });
    const gps = fakeGpsTrack([reading(13.7563, 100.5018, 20)]);

    await expect(startCapture(() => undefined).done).resolves.toEqual({ kind: 'insecure' });
    expect(gps.watches).toBe(0);
  });

  it('cancel stops the GPS and the clock', async () => {
    vi.useFakeTimers();
    const gps = fakeGpsTrack([reading(13.7563, 100.5018, 20), reading(13.757, 100.502, 12), reading(13.7564, 100.5019, 16)]);
    const progress: CaptureProgress[] = [];
    const capture = startCapture((step) => progress.push(step));
    await vi.advanceTimersByTimeAsync(2_000);

    capture.cancel();
    const reported = progress.length;
    await vi.advanceTimersByTimeAsync(30_000);

    expect(progress).toHaveLength(reported);
    expect(gps.cleared).toBe(1);
  });

  it('compares the accuracy with the sign’s radius', () => {
    expect(accuracyVerdict(18, 50)).toEqual({ ok: true, text: 'ความแม่นยำ ±18 ม. · น้อยกว่ารัศมี ใช้ได้' });
    expect(accuracyVerdict(62, 50)).toEqual({
      ok: false,
      text: 'ความแม่นยำ ±62 ม. แย่กว่ารัศมี 50 ม. ควรขยายรัศมีของป้ายนี้ หรือเก็บใหม่',
    });
  });
});
```

Create `frontend/src/pages/capture/CapturePage.test.tsx`:

```tsx
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { adminSession, areaDetail, unlocatedSign } from '../../test/adminFixtures';
import { fakeGpsTrack } from '../../test/fakeGps';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

afterEach(() => vi.useRealTimers());

const reading = (latitude: number, longitude: number, accuracy: number) => ({ latitude, longitude, accuracy });
const atTheSign = [reading(13.7563, 100.5018, 20), reading(13.757, 100.502, 12), reading(13.7564, 100.5019, 16)];

function serve(routes: FakeRoutes = {}) {
  return fakeServer({ ...adminSession, 'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }), ...routes });
}

/**
 * Opens the page at a sign, then fakes the clock for the 30 s reading. Clicks after this use fireEvent, which needs
 * no timer.
 */
async function openAtSign(url: string, signHeading: string) {
  const app = await renderApp(url);
  await screen.findByRole('heading', { level: 2, name: signHeading });
  vi.useFakeTimers({ shouldAdvanceTime: true });
  return app;
}

const wait = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

describe('CapturePage (M17, facility-0045 item 2)', () => {
  it('reads the GPS for 30 s at the sign and saves the median as a site location', async () => {
    const gps = fakeGpsTrack(atTheSign);
    const server = serve({
      'PUT /api/admin/signs/102/location': () => ({
        status: 200,
        body: unlocatedSign({ latitude: 13.7564, longitude: 100.5019, locationAccuracyM: 16, locationSource: 'Site' }),
      }),
    });
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');
    expect(screen.getByText('Admin · เก็บพิกัดป้าย · AR01 Area ทดลอง')).toBeInTheDocument();
    expect(screen.getByText('รัศมี 50 ม. · ตอนนี้: ยังไม่มีพิกัด')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(18_000);
    expect(screen.getByText('กำลังอ่าน GPS 18 / 30 วินาที')).toBeInTheDocument();
    expect(screen.getByText('ยืนนิ่งตรงที่ติดป้าย · อ่านได้ 3 ครั้ง · ความแม่นยำตอนนี้ ±16 ม.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'บันทึกพิกัด (รอครบ 30 วินาที)' })).toBeDisabled();
    expect(within(screen.getByRole('list', { name: 'ป้ายของ Area นี้' })).getByText('กำลังเก็บ')).toBeInTheDocument();

    await wait(12_000);
    expect(screen.getByText('อ่านครบแล้ว 3 ครั้ง ใน 30 วินาที')).toBeInTheDocument();
    expect(screen.getByText('ค่ากลาง: 13.756400, 100.501900')).toBeInTheDocument();
    expect(screen.getByText('ความแม่นยำ ±16 ม. · น้อยกว่ารัศมี ใช้ได้')).toBeInTheDocument();
    expect(gps.cleared).toBe(1);

    fireEvent.click(screen.getByRole('button', { name: 'บันทึกพิกัด' }));
    expect(await screen.findByText('บันทึกพิกัดป้าย AR01-02 แล้ว หน้างาน ±16 ม.')).toBeInTheDocument();
    expect(server.calls.find((call) => call.method === 'PUT')).toMatchObject({
      path: '/api/admin/signs/102/location',
      body: { latitude: 13.7564, longitude: 100.5019, accuracyM: 16, source: 'Site' },
    });
  });

  it('warns when the accuracy is worse than the radius, but still lets it be saved', async () => {
    fakeGpsTrack([reading(13.7563, 100.5018, 70), reading(13.7563, 100.5018, 75), reading(13.7563, 100.5018, 62)]);
    serve();
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(30_000);

    expect(
      screen.getByText('ความแม่นยำ ±70 ม. แย่กว่ารัศมี 50 ม. ควรขยายรัศมีของป้ายนี้ หรือเก็บใหม่'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'บันทึกพิกัด' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'เก็บใหม่' })).toBeEnabled();
  });

  it('with fewer than 3 readings it saves nothing and offers to read again', async () => {
    fakeGpsTrack([reading(13.7563, 100.5018, 20), reading(13.7564, 100.5019, 16)]);
    serve();
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(30_000);

    expect(
      screen.getByText('อ่าน GPS ได้แค่ 2 ครั้งใน 30 วินาที ลองเดินไปใกล้หน้าต่างหรือที่โล่ง แล้วเก็บใหม่'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'บันทึกพิกัด' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'เก็บใหม่' })).toBeInTheDocument();
  });

  it('a refused location says how to allow it, then lets the Admin try again', async () => {
    fakeGpsTrack(['denied']);
    serve();
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(1_000);

    expect(screen.getByRole('alert')).toHaveTextContent(
      'เปิด Location และอนุญาตให้เว็บนี้อ่านตำแหน่ง แล้วกด "ใช้ตำแหน่งตรงนี้" อีกครั้ง',
    );
    expect(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' })).toBeInTheDocument();
  });

  it('on plain http it explains that https is needed and asks for no location', async () => {
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });
    const gps = fakeGpsTrack(atTheSign);
    serve();
    await renderApp('/admin/areas/1/capture?sign=102');
    await screen.findByRole('heading', { level: 2, name: 'AR01-02 ห้องน้ำหญิง' });

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง');
    expect(gps.watches).toBe(0);
    expect(screen.queryByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' })).not.toBeInTheDocument();
  });

  it('lists the Area’s signs to choose from; choosing another sign or leaving stops the GPS', async () => {
    const gps = fakeGpsTrack(atTheSign);
    serve();
    const { router } = await renderApp('/admin/areas/1/capture');

    expect(await screen.findByText('ยืนตรงที่ติดป้าย แล้วเลือกป้ายนั้นจากรายการข้างล่าง')).toBeInTheDocument();
    const list = screen.getByRole('list', { name: 'ป้ายของ Area นี้' });
    expect(within(list).getAllByRole('button').map((button) => button.textContent)).toEqual([
      'AR01-IN ป้ายลงเวลาหน้างาน ±14 ม.',
      'AR01-01 ห้องน้ำชายหน้างาน ±14 ม.',
      'AR01-02 ห้องน้ำหญิงยังไม่มีพิกัด',
    ]);
    // M17 is a phone page: no Admin menu
    expect(screen.queryByRole('navigation', { name: 'เมนู Admin' })).not.toBeInTheDocument();

    fireEvent.click(within(list).getByRole('button', { name: /^AR01-IN/ }));
    await waitFor(() => expect(router.state.location.search).toBe('?sign=100'));
    expect(await screen.findByRole('heading', { level: 2, name: 'AR01-IN ป้ายลงเวลา' })).toBeInTheDocument();

    vi.useFakeTimers({ shouldAdvanceTime: true });
    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(5_000);
    fireEvent.click(within(list).getByRole('button', { name: /^AR01-01/ }));
    expect(gps.cleared).toBe(1);
    expect(await screen.findByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(5_000);
    fireEvent.click(screen.getByRole('link', { name: '‹ กลับไปหน้าจัดการ Area และป้าย' }));
    await waitFor(() => expect(gps.cleared).toBe(2));
  });
});
```

- [ ] **Step 3: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/capture`
Expected: FAIL. `./captureLocation` does not exist, and the capture page is not a route.

- [ ] **Step 4: The reading**

Create `frontend/src/pages/capture/captureLocation.ts`:

```ts
import type { GpsFix, LocationProblem } from '../../shared/geo/readLocation';

/** facility-0045 item 2: read for 30 s standing still at the sign. */
export const CAPTURE_MS = 30_000;

/** Fewer readings than this in 30 s give no median worth keeping. */
export const MIN_READINGS = 3;

export interface CaptureProgress {
  seconds: number;
  readings: number;
  /** The latest reading's accuracy, null before the first. */
  accuracyM: number | null;
}

export type CaptureResult =
  | { kind: 'fix'; fix: GpsFix; readings: number }
  | { kind: 'too-few'; readings: number }
  | { kind: LocationProblem };

const PERMISSION_DENIED = 1; // GeolocationPositionError.PERMISSION_DENIED

function median(values: number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

/** The median latitude, longitude and accuracy, each on its own; the accuracy rounded up to whole metres. */
export function medianFix(fixes: readonly GpsFix[]): GpsFix {
  return {
    latitude: median(fixes.map((fix) => fix.latitude)),
    longitude: median(fixes.map((fix) => fix.longitude)),
    accuracyM: Math.ceil(median(fixes.map((fix) => fix.accuracyM))),
  };
}

/** facility-0045 item 2: an accuracy worse than the radius is worth a warning, not a refusal. */
export function accuracyVerdict(accuracyM: number, radiusM: number): { ok: boolean; text: string } {
  return accuracyM <= radiusM
    ? { ok: true, text: `ความแม่นยำ ±${accuracyM} ม. · น้อยกว่ารัศมี ใช้ได้` }
    : { ok: false, text: `ความแม่นยำ ±${accuracyM} ม. แย่กว่ารัศมี ${radiusM} ม. ควรขยายรัศมีของป้ายนี้ หรือเก็บใหม่` };
}

/**
 * Watches the GPS (high accuracy, no cached fix) for 30 s, reporting once a second and on every reading, then gives
 * the median. A refused permission ends it at once; other GPS errors only mean fewer readings. `cancel` stops the
 * watch and the clock, and the result then never comes.
 */
export function startCapture(onProgress: (progress: CaptureProgress) => void): {
  done: Promise<CaptureResult>;
  cancel: () => void;
} {
  let cancel: () => void = () => undefined;
  const done = new Promise<CaptureResult>((resolve) => {
    if (window.isSecureContext === false) {
      resolve({ kind: 'insecure' });
      return;
    }
    if (!('geolocation' in navigator)) {
      resolve({ kind: 'unavailable' });
      return;
    }
    const fixes: GpsFix[] = [];
    let seconds = 0;
    const report = () =>
      onProgress({ seconds, readings: fixes.length, accuracyM: fixes.length > 0 ? fixes[fixes.length - 1].accuracyM : null });

    const watchId = navigator.geolocation.watchPosition(
      (position) => {
        fixes.push({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracyM: position.coords.accuracy,
        });
        report();
      },
      (error) => {
        if (error.code === PERMISSION_DENIED) {
          stop();
          resolve({ kind: 'denied' });
        }
      },
      { enableHighAccuracy: true, maximumAge: 0 },
    );
    const timer = setInterval(() => {
      seconds += 1;
      report();
      if (seconds * 1000 >= CAPTURE_MS) {
        stop();
        resolve(
          fixes.length < MIN_READINGS
            ? { kind: 'too-few', readings: fixes.length }
            : { kind: 'fix', fix: medianFix(fixes), readings: fixes.length },
        );
      }
    }, 1000);

    function stop() {
      clearInterval(timer);
      navigator.geolocation.clearWatch(watchId);
    }
    cancel = stop;
  });
  return { done, cancel: () => cancel() };
}
```

- [ ] **Step 5: The page and its route**

Create `frontend/src/pages/capture/CapturePage.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import {
  useGetAreaQuery,
  useSetSignLocationMutation,
  type AdminSign,
  type AreaDetail,
} from '../../shared/api/adminSetupApi';
import type { GpsFix } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { formatCoordinates, signPlaceLabel } from '../../shared/utils/labels';
import {
  accuracyVerdict,
  CAPTURE_MS,
  startCapture,
  type CaptureProgress,
  type CaptureResult,
} from './captureLocation';
import './capture.css';

type Phase =
  | { kind: 'idle' }
  | { kind: 'reading'; progress: CaptureProgress }
  | { kind: 'result'; result: CaptureResult }
  | { kind: 'saved'; text: string };

interface CaptureSign {
  sign: AdminSign;
  title: string;
}

const SECONDS = CAPTURE_MS / 1000;

/** The Area's signs that can be scanned: the check-in sign and the active points (D4's order). */
function captureSigns(detail: AreaDetail): CaptureSign[] {
  return [
    { sign: detail.checkInSign, title: 'ป้ายลงเวลา' },
    ...detail.points.filter((point) => point.isActive).map((point) => ({ sign: point.sign, title: point.name })),
  ];
}

/**
 * facility-0045 item 2, wireframe M17: the Admin stands at a sign with a phone, reads the GPS for 30 s and saves the
 * median as a site location. A reading runs while `runId` is set: choosing another sign or leaving the page stops it.
 */
export function CapturePage() {
  const { areaId } = useParams();
  const [params, setParams] = useSearchParams();
  const area = useGetAreaQuery(Number(areaId));
  const [setLocation, saving] = useSetSignLocationMutation();
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });
  const [problem, setProblem] = useState<string | null>(null);
  const [runId, setRunId] = useState(0);

  useEffect(() => {
    if (runId === 0) {
      return undefined;
    }
    let live = true;
    const run = startCapture((progress) => {
      if (live) {
        setPhase({ kind: 'reading', progress });
      }
    });
    void run.done.then((result) => {
      if (live) {
        setPhase({ kind: 'result', result });
      }
    });
    return () => {
      live = false;
      run.cancel();
    };
  }, [runId]);

  const detail = area.currentData;
  const signs = detail ? captureSigns(detail) : [];
  const selected = signs.find((target) => String(target.sign.id) === params.get('sign'));

  const start = () => {
    setProblem(null);
    setPhase({ kind: 'reading', progress: { seconds: 0, readings: 0, accuracyM: null } });
    setRunId((id) => id + 1);
  };
  const choose = (signId: number) => {
    setRunId(0);
    setPhase({ kind: 'idle' });
    setProblem(null);
    setParams({ sign: String(signId) });
  };
  const save = async (sign: AdminSign, fix: GpsFix) => {
    setProblem(null);
    try {
      const saved = await setLocation({
        signId: sign.id,
        latitude: fix.latitude,
        longitude: fix.longitude,
        accuracyM: fix.accuracyM,
        source: 'Site',
      }).unwrap();
      setPhase({ kind: 'saved', text: `บันทึกพิกัดป้าย ${saved.code} แล้ว ${signPlaceLabel(saved)}` });
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <main className="capture-page">
      <Link to={`/admin/areas/${areaId}`}>‹ กลับไปหน้าจัดการ Area และป้าย</Link>
      {detail && <span className="capture-kicker">{`Admin · เก็บพิกัดป้าย · ${detail.area.code} ${detail.area.name}`}</span>}
      <h1>เก็บพิกัดป้าย</h1>
      {area.error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(area.error)}
        </p>
      )}
      {detail && !selected && <p className="app-note app-note--info">ยืนตรงที่ติดป้าย แล้วเลือกป้ายนั้นจากรายการข้างล่าง</p>}
      {selected && (
        <SignCapture
          target={selected}
          phase={phase}
          problem={problem}
          saving={saving.isLoading}
          onStart={start}
          onSave={(fix) => void save(selected.sign, fix)}
        />
      )}
      {detail && (
        <section className="capture-signs" aria-labelledby="capture-signs-title">
          <h2 id="capture-signs-title">ป้ายของ Area นี้</h2>
          <ul aria-label="ป้ายของ Area นี้">
            {signs.map((target) => {
              const isSelected = target.sign.id === selected?.sign.id;
              return (
                <li key={target.sign.id}>
                  <button
                    type="button"
                    className={isSelected ? 'capture-row capture-row--selected' : 'capture-row'}
                    aria-pressed={isSelected}
                    onClick={() => choose(target.sign.id)}
                  >
                    <strong>{`${target.sign.code} ${target.title}`}</strong>
                    <span>{isSelected && phase.kind === 'reading' ? 'กำลังเก็บ' : signPlaceLabel(target.sign)}</span>
                  </button>
                </li>
              );
            })}
          </ul>
        </section>
      )}
      <p className="app-muted">ต้องเปิดผ่าน HTTPS · การบันทึกเก็บลงประวัติ Admin</p>
    </main>
  );
}

interface SignCaptureProps {
  target: CaptureSign;
  phase: Phase;
  problem: string | null;
  saving: boolean;
  onStart: () => void;
  onSave: (fix: GpsFix) => void;
}

/** Wireframe M17's card: the sign, the 30 s reading, the median with its accuracy, and the save. */
function SignCapture({ target, phase, problem, saving, onStart, onSave }: SignCaptureProps) {
  const { sign, title } = target;
  const result = phase.kind === 'result' ? phase.result : null;
  const readAgain = phase.kind === 'saved' || result?.kind === 'too-few';
  const showStart = phase.kind !== 'reading' && result?.kind !== 'fix' && result?.kind !== 'insecure';

  return (
    <section className="app-panel capture-sign" aria-labelledby="capture-sign-title">
      <h2 id="capture-sign-title">{`${sign.code} ${title}`}</h2>
      <p className="app-muted">{`รัศมี ${sign.radiusM} ม. · ตอนนี้: ${signPlaceLabel(sign)}`}</p>
      {phase.kind === 'reading' && (
        <>
          <p className="capture-progress" role="status">
            {`กำลังอ่าน GPS ${phase.progress.seconds} / ${SECONDS} วินาที`}
          </p>
          <p>
            {`ยืนนิ่งตรงที่ติดป้าย · อ่านได้ ${phase.progress.readings} ครั้ง${
              phase.progress.accuracyM != null ? ` · ความแม่นยำตอนนี้ ±${Math.round(phase.progress.accuracyM)} ม.` : ''
            }`}
          </p>
          <button type="button" className="app-button app-button--primary" disabled>
            {`บันทึกพิกัด (รอครบ ${SECONDS} วินาที)`}
          </button>
        </>
      )}
      {result?.kind === 'fix' && (
        <>
          <p role="status">{`อ่านครบแล้ว ${result.readings} ครั้ง ใน ${SECONDS} วินาที`}</p>
          <p>{`ค่ากลาง: ${formatCoordinates(result.fix.latitude, result.fix.longitude)}`}</p>
          <p className={accuracyVerdict(result.fix.accuracyM, sign.radiusM).ok ? 'app-note app-note--info' : 'app-note app-note--warn'}>
            {accuracyVerdict(result.fix.accuracyM, sign.radiusM).text}
          </p>
          <div className="capture-buttons">
            <button type="button" className="app-button app-button--primary" disabled={saving} onClick={() => onSave(result.fix)}>
              {saving ? 'กำลังบันทึก...' : 'บันทึกพิกัด'}
            </button>
            <button type="button" className="app-button" disabled={saving} onClick={onStart}>
              เก็บใหม่
            </button>
          </div>
        </>
      )}
      {result?.kind === 'too-few' && (
        <p className="app-note app-note--warn">
          {`อ่าน GPS ได้แค่ ${result.readings} ครั้งใน ${SECONDS} วินาที ลองเดินไปใกล้หน้าต่างหรือที่โล่ง แล้วเก็บใหม่`}
        </p>
      )}
      {result?.kind === 'denied' && (
        <p className="app-note app-note--error" role="alert">
          เปิด Location และอนุญาตให้เว็บนี้อ่านตำแหน่ง แล้วกด "ใช้ตำแหน่งตรงนี้" อีกครั้ง
        </p>
      )}
      {result?.kind === 'unavailable' && (
        <p className="app-note app-note--error" role="alert">
          เครื่องนี้อ่านตำแหน่งไม่ได้ เปิดหน้านี้บนมือถือที่มี GPS
        </p>
      )}
      {result?.kind === 'insecure' && (
        <p className="app-note app-note--error" role="alert">
          หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง
        </p>
      )}
      {phase.kind === 'saved' && (
        <p className="app-note app-note--info" role="status">
          {phase.text}
        </p>
      )}
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      {showStart && (
        <button type="button" className="app-button app-button--primary" onClick={onStart}>
          {readAgain ? 'เก็บใหม่' : 'ใช้ตำแหน่งตรงนี้'}
        </button>
      )}
    </section>
  );
}
```

Create `frontend/src/pages/capture/index.ts`:

```ts
export { CapturePage } from './CapturePage';
```

Create `frontend/src/pages/capture/capture.css`:

```css
/* Wireframe M17 (facility-0045 item 2), the Admin's phone page. Class prefix: capture- */

.capture-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.capture-page h1 {
  font-size: 24px;
  font-weight: 700;
}

.capture-kicker {
  color: var(--color-muted);
  font-size: 13px;
}

.capture-sign h2 {
  font-size: 18px;
  font-weight: 700;
}

.capture-progress {
  font-size: 20px;
  font-weight: 700;
}

.capture-buttons {
  display: flex;
  gap: 8px;
}

.capture-signs {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.capture-signs h2 {
  font-size: 15px;
  font-weight: 600;
}

.capture-signs ul {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.capture-row {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text);
  font-size: 15px;
  text-align: left;
  cursor: pointer;
}

.capture-row span {
  color: var(--color-muted);
  font-size: 13px;
}

.capture-row--selected {
  border-color: var(--color-info-border);
  background: var(--color-info-bg);
}
```

In `frontend/src/router.tsx`, add the import below the `AreaFormPage` import:

```tsx
import { CapturePage } from './pages/capture';
```

and replace:

```tsx
              { path: 'admin/print', element: <PrintSignsPage /> },
            ],
          },
```

with:

```tsx
              { path: 'admin/print', element: <PrintSignsPage /> },
            ],
          },
          // M17: the Admin's phone page at the sign (facility-0045 item 2), in the phone column
          { element: <AppLayout />, children: [{ path: 'admin/areas/:areaId/capture', element: <CapturePage /> }] },
```

- [ ] **Step 6: Run the tests, the typecheck and the lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. Frontend tests: 209 (195 + 8 rules + 6 page).

- [ ] **Step 7: Handoff entry**

```markdown
- <date> · Task 7 done: capture page M17 (/admin/areas/:areaId/capture[?sign=<id>]), phone column: choose the sign you stand at, "ใช้ตำแหน่งตรงนี้" watches the GPS for 30 s (countdown, readings, latest accuracy), the median latitude, longitude and accuracy (rounded up) from at least 3 readings, the radius warning, "บันทึกพิกัด" only after 30 s, saved as Site; refused location, plain http and too few readings explained; choosing another sign or leaving stops the GPS. test/fakeGps.ts gained fakeGpsTrack. Frontend tests <n> passed. Next: Task 8 (accounts list and add).
```

- [ ] **Step 8: Commit**

```bash
git add docs/handoff.md frontend/src
git commit -m "feat(frontend): capture page M17: 30 s of GPS at the sign, median saved as a site location"
```

---

### Task 8: Accounts page (D6): the table, filters, search and adding an account

**Files:**
- Create: `frontend/src/pages/accounts/index.ts`, `AccountsPage.tsx`, `AccountTable.tsx`, `CreateAccountForm.tsx`, `ShiftChoice.tsx`, `accountsApi.ts`, `accountRules.ts`, `accountRules.test.ts`, `accounts.css`
- Modify: `frontend/src/test/adminFixtures.ts`, `frontend/src/router.tsx`
- Test: `frontend/src/pages/accounts/AccountsPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `useGetAreasQuery`, `useGetBuildingsQuery`, `AreaSummary`, `Building` (Task 1); `shiftLabel` (existing), `shiftShortLabel` (Task 1); fixtures (Task 1).
- Produces:
  - In `accountsApi.ts`:
    - `AccountRole = 'Cleaner' | 'Supervisor' | 'Admin'`.
    - `Account` (AccountDto, optional place fields).
    - `CreateAccountRequest`, a union by role.
    - Hooks `useGetAccountsQuery()` and `useCreateAccountMutation()`. Task 9 adds more.
  - In `accountRules.ts`:
    - `AccountFilter`, `accountFilters`, `filterLabel`, `accountRoleLabel`.
    - `filterAccounts(accounts, filter, search)` and `filterCounts(accounts)`.
    - `placeText(account)` and `areaOptionLabel(area, exceptUserId?)`.
    - `cleanerShiftBlock(area, shift, exceptUserId?)` and `supervisorShiftBlock(accounts, buildingId, shift, exceptUserId?)`.
    - `normalizePhone(text)`, plus `PHONE_PROBLEM`, `MIN_PASSWORD`, `MAX_PASSWORD`, `PASSWORD_PROBLEM`.
    - `CreateDraft`, `emptyDraft(role?)`, and `createRequest(draft): { request } | { problem }`.
  - `ShiftChoice({ name, value, blockOf, disabled, onChange })`.
  - `AccountTable({ accounts })`. Task 9 adds actions.
  - `AccountsPage` at `/admin/accounts`.
  - Fixtures `account()` and `accounts` in `src/test/adminFixtures.ts`.

- [ ] **Step 1: Account fixtures**

In `frontend/src/test/adminFixtures.ts`, add this import below the `adminSetupApi` import:

```ts
import type { Account } from '../pages/accounts/accountsApi';
```

and append:

```ts
/** A Cleaner of AR01, Day shift (AccountDto). */
export function account(overrides: Partial<Account> = {}): Account {
  return {
    id: 21,
    role: 'Cleaner',
    loginName: '650001',
    displayName: 'แม่บ้านทดสอบ',
    areaId: 1,
    areaCode: 'AR01',
    shift: 'Day',
    isActive: true,
    createdAt: '2026-10-01T02:00:00Z',
    ...overrides,
  };
}

const noPlace = { areaId: undefined, areaCode: undefined, shift: undefined };

/**
 * The server's order: Admins, Supervisors, Cleaners, each by login name. Account 7 is the logged-in Admin
 * (authAnswer('admin')).
 */
export const accounts: Account[] = [
  account({ id: 7, role: 'Admin', loginName: 'admin', displayName: 'ผู้ดูแลทดสอบ', ...noPlace }),
  account({ id: 8, role: 'Admin', loginName: 'admin2', displayName: 'ผู้ดูแลคนที่สอง', ...noPlace }),
  account({
    id: 31,
    role: 'Supervisor',
    loginName: '660001',
    displayName: 'หัวหน้าทดสอบ',
    areaId: undefined,
    areaCode: undefined,
    buildingId: 1,
    buildingCode: 'A',
  }),
  account(),
  account({ id: 22, loginName: '650002', displayName: 'แม่บ้านกะดึก', shift: 'Night' }),
  account({ id: 23, loginName: '650003', displayName: 'แม่บ้านว่าง', areaId: undefined, areaCode: undefined }),
  account({
    id: 26,
    loginName: '650006',
    displayName: 'แม่บ้านที่ลาออก',
    areaId: undefined,
    areaCode: undefined,
    isActive: false,
  }),
];
```

- [ ] **Step 2: Write the failing tests**

Create `frontend/src/pages/accounts/accountRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { account } from '../../test/adminFixtures';
import { createRequest, emptyDraft, normalizePhone, placeText } from './accountRules';

describe('account rules (facility-0020, 0054)', () => {
  it('builds a request with only the role’s own fields (the server refuses the others)', () => {
    const typed = {
      displayName: ' แม่บ้านใหม่ ',
      employeeId: '650010',
      phone: '081-000-0009',
      areaId: '2',
      buildingId: '1',
      shift: 'Day' as const,
      username: 'left-over',
      password: 'left-over-secret',
    };
    expect(createRequest({ ...typed, role: 'Cleaner' })).toEqual({
      request: { role: 'Cleaner', displayName: 'แม่บ้านใหม่', employeeId: '650010', phone: '0810000009', shift: 'Day', areaId: 2 },
    });
    expect(createRequest({ ...typed, role: 'Cleaner', areaId: '' })).toEqual({
      request: { role: 'Cleaner', displayName: 'แม่บ้านใหม่', employeeId: '650010', phone: '0810000009', shift: 'Day' },
    });
    expect(createRequest({ ...typed, role: 'Supervisor' })).toEqual({
      request: { role: 'Supervisor', displayName: 'แม่บ้านใหม่', employeeId: '650010', phone: '0810000009', buildingId: 1, shift: 'Day' },
    });
    expect(createRequest({ ...typed, role: 'Admin' })).toEqual({
      request: { role: 'Admin', displayName: 'แม่บ้านใหม่', username: 'left-over', password: 'left-over-secret' },
    });
    expect(createRequest(emptyDraft())).toEqual({ problem: 'ใส่ชื่อที่แสดงก่อน' });
  });

  it('accepts a phone of 10 Arabic digits starting with 0, ignoring spaces and dashes', () => {
    expect(normalizePhone('081-000 0009')).toBe('0810000009');
    expect(normalizePhone('810000009')).toBeNull();
    expect(normalizePhone('08100000099')).toBeNull();
    expect(normalizePhone('๐๘๑๐๐๐๐๐๐๙')).toBeNull();
  });

  it('writes where each role works', () => {
    expect(placeText(account())).toBe('AR01 · กะเช้า');
    expect(placeText(account({ areaId: undefined, areaCode: undefined, shift: 'Night' }))).toBe('ยังไม่มี Area · กะดึก');
    expect(placeText(account({ role: 'Supervisor', areaCode: undefined, buildingCode: 'B', shift: 'Night' }))).toBe(
      'ตึก B · กะดึก',
    );
    expect(placeText(account({ role: 'Admin', areaCode: undefined, shift: undefined }))).toBe('—');
  });
});
```

Create `frontend/src/pages/accounts/AccountsPage.test.tsx`:

```tsx
import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { account, accounts, adminSession, areaSummary, buildings, officeSummary } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/accounts': () => ({ status: 200, body: accounts }),
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    ...routes,
  });
}

/** A row by its name cell; the first four cells are login name, role, place, active. */
const rowOf = (name: string) => screen.getByRole('rowheader', { name }).closest('tr') as HTMLElement;
const cellsOf = (name: string) =>
  within(rowOf(name))
    .getAllByRole('cell')
    .slice(0, 4)
    .map((cell) => cell.textContent);
const names = () => screen.getAllByRole('rowheader').map((header) => header.textContent);
const created = (server: ReturnType<typeof serve>) =>
  server.calls.find((call) => call.method === 'POST' && call.path === '/api/admin/accounts')?.body;

describe('AccountsPage (D6, facility-0020, 0056)', () => {
  it('lists every account with role filters, counts and where each one works', async () => {
    serve();
    await renderApp('/admin/accounts');

    await screen.findByRole('table', { name: 'บัญชีผู้ใช้' });
    const filters = screen.getByRole('group', { name: 'กรองบัญชี' });
    expect(within(filters).getAllByRole('button').map((button) => button.textContent)).toEqual([
      'ทั้งหมด (7)',
      'แม่บ้าน (4)',
      'หัวหน้า (1)',
      'Admin (2)',
      'ปิดใช้งาน (1)',
    ]);
    expect(within(filters).getByRole('button', { name: 'ทั้งหมด (7)' })).toHaveAttribute('aria-pressed', 'true');
    expect(cellsOf('แม่บ้านทดสอบ')).toEqual(['650001', 'แม่บ้าน', 'AR01 · กะเช้า', 'ใช้งาน']);
    expect(cellsOf('แม่บ้านว่าง')).toEqual(['650003', 'แม่บ้าน', 'ยังไม่มี Area · กะเช้า', 'ใช้งาน']);
    expect(cellsOf('หัวหน้าทดสอบ')).toEqual(['660001', 'หัวหน้า', 'ตึก A · กะเช้า', 'ใช้งาน']);
    expect(cellsOf('ผู้ดูแลคนที่สอง')).toEqual(['admin2', 'Admin', '—', 'ใช้งาน']);
    expect(cellsOf('แม่บ้านที่ลาออก')).toEqual(['650006', 'แม่บ้าน', 'ยังไม่มี Area · กะเช้า', 'ปิดใช้งาน']);
  });

  it('filters by role or by deactivated, and searches names and login names', async () => {
    serve();
    const { user } = await renderApp('/admin/accounts');
    await screen.findByRole('table', { name: 'บัญชีผู้ใช้' });

    await user.click(screen.getByRole('button', { name: 'แม่บ้าน (4)' }));
    expect(names()).toEqual(['แม่บ้านทดสอบ', 'แม่บ้านกะดึก', 'แม่บ้านว่าง', 'แม่บ้านที่ลาออก']);
    await user.click(screen.getByRole('button', { name: 'ปิดใช้งาน (1)' }));
    expect(names()).toEqual(['แม่บ้านที่ลาออก']);

    await user.click(screen.getByRole('button', { name: 'ทั้งหมด (7)' }));
    await user.type(screen.getByLabelText('ค้นหา'), 'ADMIN2');
    expect(names()).toEqual(['ผู้ดูแลคนที่สอง']);
    await user.clear(screen.getByLabelText('ค้นหา'));
    await user.type(screen.getByLabelText('ค้นหา'), 'ไม่มีคนนี้');
    expect(screen.getByText('ไม่พบบัญชีที่ตรงกับที่ค้นหา')).toBeInTheDocument();
  });

  it('adds a Cleaner: only the Cleaner’s fields go, and a held shift or a night in a day-only Area cannot be picked', async () => {
    const server = serve({
      'POST /api/admin/accounts': ({ body }) => ({
        status: 201,
        body: account({
          id: 40,
          loginName: '650010',
          displayName: (body as { displayName: string }).displayName,
          areaId: 2,
          areaCode: 'AR02',
        }),
      }),
    });
    const { user } = await renderApp('/admin/accounts');

    const form = await screen.findByRole('form', { name: 'เพิ่มบัญชี' });
    expect(within(form).getByRole('radio', { name: 'แม่บ้าน' })).toBeChecked();
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '650010');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '081-000-0009');

    const areaSelect = await within(form).findByLabelText('Area ประจำ');
    await within(areaSelect).findByRole('option', { name: /^AR02/ });
    expect(within(areaSelect).getAllByRole('option').map((option) => option.textContent)).toEqual([
      '— ยังไม่กำหนด Area —',
      'AR01 Area ทดลอง · ตึก A · 2 กะ · กะเช้ามีคนแล้ว (แม่บ้านทดสอบ) · กะดึกมีคนแล้ว (แม่บ้านกะดึก)',
      'AR02 Office ทดลอง · ตึก A · กะเช้าอย่างเดียว',
    ]);
    await user.selectOptions(areaSelect, '1');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).toBeDisabled();
    expect(within(form).getByRole('radio', { name: /^กะดึก/ })).toBeDisabled();
    expect(within(form).getByText('(มีคนประจำแล้ว (แม่บ้านทดสอบ))')).toBeInTheDocument();

    await user.selectOptions(areaSelect, '2');
    expect(within(form).getByRole('radio', { name: /^กะดึก/ })).toBeDisabled();
    expect(within(form).getByText('(Area นี้ทำเฉพาะกะเช้า)')).toBeInTheDocument();
    await user.click(within(form).getByRole('radio', { name: /^กะเช้า/ }));
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี แม่บ้านใหม่ แล้ว');
    expect(created(server)).toEqual({
      role: 'Cleaner',
      displayName: 'แม่บ้านใหม่',
      employeeId: '650010',
      phone: '0810000009',
      shift: 'Day',
      areaId: 2,
    });
    // The phone leaves the form once it is sent
    expect(within(form).getByLabelText('เบอร์โทรศัพท์')).toHaveValue('');
  });

  it('adds a Supervisor with building and shift; a building-shift that has one cannot be picked', async () => {
    const server = serve({
      'POST /api/admin/accounts': () => ({
        status: 201,
        body: account({
          id: 41,
          role: 'Supervisor',
          loginName: '660002',
          displayName: 'หัวหน้าใหม่',
          areaId: undefined,
          areaCode: undefined,
          buildingId: 2,
          buildingCode: 'B',
          shift: 'Night',
        }),
      }),
    });
    const { user } = await renderApp('/admin/accounts');

    const form = await screen.findByRole('form', { name: 'เพิ่มบัญชี' });
    await user.click(within(form).getByRole('radio', { name: 'หัวหน้า' }));
    expect(within(form).queryByLabelText('Area ประจำ')).not.toBeInTheDocument();
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'หัวหน้าใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '660002');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '0820000002');
    await within(form).findByRole('option', { name: 'ตึกข้างเคียง' });
    await user.selectOptions(within(form).getByLabelText('ตึกที่ประจำ'), 'ตึกทดลอง');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).toBeDisabled();
    expect(within(form).getByText('(มีหัวหน้าแล้ว (หัวหน้าทดสอบ))')).toBeInTheDocument();

    await user.selectOptions(within(form).getByLabelText('ตึกที่ประจำ'), 'ตึกข้างเคียง');
    await user.click(within(form).getByRole('radio', { name: /^กะดึก/ }));
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี หัวหน้าใหม่ แล้ว');
    expect(created(server)).toEqual({
      role: 'Supervisor',
      displayName: 'หัวหน้าใหม่',
      employeeId: '660002',
      phone: '0820000002',
      buildingId: 2,
      shift: 'Night',
    });
  });

  it('adds an Admin with a username and a temporary password, and no phone', async () => {
    const server = serve({
      'POST /api/admin/accounts': () => ({
        status: 201,
        body: account({ id: 9, role: 'Admin', loginName: 'admin3', displayName: 'ผู้ดูแลคนที่สาม', areaId: undefined, areaCode: undefined, shift: undefined }),
      }),
    });
    const { user } = await renderApp('/admin/accounts');

    const form = await screen.findByRole('form', { name: 'เพิ่มบัญชี' });
    await user.click(within(form).getByRole('radio', { name: 'Admin' }));
    expect(within(form).queryByLabelText('เบอร์โทรศัพท์')).not.toBeInTheDocument();
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'ผู้ดูแลคนที่สาม');
    await user.type(within(form).getByLabelText('ชื่อผู้ใช้'), 'admin3');
    await user.type(within(form).getByLabelText('รหัสผ่านชั่วคราว'), 'short');
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));
    expect(within(form).getByRole('alert')).toHaveTextContent('รหัสผ่านชั่วคราวต้องยาว 8–100 ตัวอักษร');

    await user.clear(within(form).getByLabelText('รหัสผ่านชั่วคราว'));
    await user.type(within(form).getByLabelText('รหัสผ่านชั่วคราว'), 'temporary-123');
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี ผู้ดูแลคนที่สาม แล้ว');
    expect(created(server)).toEqual({ role: 'Admin', displayName: 'ผู้ดูแลคนที่สาม', username: 'admin3', password: 'temporary-123' });
    expect(within(form).getByLabelText('รหัสผ่านชั่วคราว')).toHaveValue('');
  });

  it('says what is missing first, and shows the server’s refusal with the form kept', async () => {
    const server = serve({
      'POST /api/admin/accounts': () => ({ status: 409, body: { message: 'รหัสพนักงานนี้มีแล้ว' } }),
    });
    const { user } = await renderApp('/admin/accounts');
    const form = await screen.findByRole('form', { name: 'เพิ่มบัญชี' });
    const send = () => user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    await send();
    expect(within(form).getByRole('alert')).toHaveTextContent('ใส่ชื่อที่แสดงก่อน');
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านซ้ำ');
    await send();
    expect(within(form).getByRole('alert')).toHaveTextContent('ใส่รหัสพนักงานก่อน');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '650001');
    await send();
    expect(within(form).getByRole('alert')).toHaveTextContent('เบอร์โทรต้องเป็นเลข 10 หลัก ขึ้นต้นด้วย 0');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '0810000009');
    await send();
    expect(within(form).getByRole('alert')).toHaveTextContent('เลือกกะก่อน');
    expect(created(server)).toBeUndefined();

    // No Area yet is allowed: the Admin chooses it later with "แก้ไข"
    await user.click(within(form).getByRole('radio', { name: /^กะเช้า/ }));
    await send();
    expect(await within(form).findByRole('alert')).toHaveTextContent('รหัสพนักงานนี้มีแล้ว');
    expect(within(form).getByLabelText('รหัสพนักงาน')).toHaveValue('650001');
  });
});
```

- [ ] **Step 3: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/accounts`
Expected: FAIL. `./accountsApi` and `./accountRules` do not exist, and `/admin/accounts` is not a route.

- [ ] **Step 4: The API slice and the rules**

Create `frontend/src/pages/accounts/accountsApi.ts`:

```ts
import { baseApi } from '../../shared/api/baseApi';
import type { Shift } from '../../shared/api/types';

/** UserRole as the accounts API sends it (the login answer's role is lower-case; this one is not). */
export type AccountRole = 'Cleaner' | 'Supervisor' | 'Admin';

/** AccountDto (D6). loginName is the employee ID, or an Admin's username. No phone, password or hash ever comes. */
export interface Account {
  id: number;
  role: AccountRole;
  loginName: string;
  displayName: string;
  areaId?: number;
  areaCode?: string;
  buildingId?: number;
  buildingCode?: string;
  shift?: Shift;
  isActive: boolean;
  createdAt: string;
}

/** CreateAccountRequest with only the role's own fields: the server refuses any other with 400 (plan 5 decision 9). */
export type CreateAccountRequest =
  | { role: 'Cleaner'; displayName: string; employeeId: string; phone: string; shift: Shift; areaId?: number }
  | { role: 'Supervisor'; displayName: string; employeeId: string; phone: string; buildingId: number; shift: Shift }
  | { role: 'Admin'; displayName: string; username: string; password: string };

/** facility-0020: the accounts page. Every save invalidates AdminSetup: accounts and Area slots depend on each other. */
const accountsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getAccounts: build.query<Account[], void>({
      query: () => '/admin/accounts',
      providesTags: ['AdminSetup'],
    }),
    createAccount: build.mutation<Account, CreateAccountRequest>({
      query: (body) => ({ url: '/admin/accounts', method: 'POST', body }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const { useGetAccountsQuery, useCreateAccountMutation } = accountsApi;
```

Create `frontend/src/pages/accounts/accountRules.ts`:

```ts
import type { AreaSummary } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { shiftShortLabel } from '../../shared/utils/labels';
import type { Account, AccountRole, CreateAccountRequest } from './accountsApi';

export type AccountFilter = 'all' | AccountRole | 'inactive';

export const accountFilters: readonly AccountFilter[] = ['all', 'Cleaner', 'Supervisor', 'Admin', 'inactive'];

/** Wireframe D6's tabs. */
export const filterLabel: Record<AccountFilter, string> = {
  all: 'ทั้งหมด',
  Cleaner: 'แม่บ้าน',
  Supervisor: 'หัวหน้า',
  Admin: 'Admin',
  inactive: 'ปิดใช้งาน',
};

export const accountRoleLabel: Record<AccountRole, string> = {
  Cleaner: 'แม่บ้าน',
  Supervisor: 'หัวหน้า',
  Admin: 'Admin',
};

function inFilter(account: Account, filter: AccountFilter): boolean {
  if (filter === 'all') {
    return true;
  }
  return filter === 'inactive' ? !account.isActive : account.role === filter;
}

/** facility-0056: about 180 rows, filtered on the page. The search covers the name and the login name. */
export function filterAccounts(accounts: Account[], filter: AccountFilter, search: string): Account[] {
  const needle = search.trim().toLowerCase();
  return accounts.filter(
    (account) =>
      inFilter(account, filter) &&
      (needle === '' ||
        account.displayName.toLowerCase().includes(needle) ||
        account.loginName.toLowerCase().includes(needle)),
  );
}

export function filterCounts(accounts: Account[]): Record<AccountFilter, number> {
  return Object.fromEntries(
    accountFilters.map((filter) => [filter, accounts.filter((account) => inFilter(account, filter)).length]),
  ) as Record<AccountFilter, number>;
}

/** D6's "ประจำที่" column. */
export function placeText(account: Account): string {
  if (account.role === 'Admin') {
    return '—';
  }
  const shift = account.shift ? shiftShortLabel[account.shift] : 'ไม่มีกะ';
  return account.role === 'Supervisor'
    ? `ตึก ${account.buildingCode ?? '-'} · ${shift}`
    : `${account.areaCode ?? 'ยังไม่มี Area'} · ${shift}`;
}

/** D6's Area option: "AR12 ชื่อ · ตึก B · 2 กะ · กะเช้ามีคนแล้ว (ทองใบ)". The account being edited is not "someone". */
export function areaOptionLabel(area: AreaSummary, exceptUserId?: number): string {
  const parts = [`${area.code} ${area.name}`, `ตึก ${area.buildingCode}`, area.shiftPattern === 'DayOnly' ? 'กะเช้าอย่างเดียว' : '2 กะ'];
  if (area.dayCleaner && area.dayCleaner.id !== exceptUserId) {
    parts.push(`กะเช้ามีคนแล้ว (${area.dayCleaner.displayName})`);
  }
  if (area.nightCleaner && area.nightCleaner.id !== exceptUserId) {
    parts.push(`กะดึกมีคนแล้ว (${area.nightCleaner.displayName})`);
  }
  if (!area.isActive) {
    parts.push('ปิดใช้งาน');
  }
  return parts.join(' · ');
}

/** Why a Cleaner cannot take this shift of this Area, or null (D6: "Area ละ 1 คนต่อกะ"; day-only has no night). */
export function cleanerShiftBlock(area: AreaSummary | undefined, shift: Shift, exceptUserId?: number): string | null {
  if (!area) {
    return null;
  }
  if (shift === 'Night' && area.shiftPattern === 'DayOnly') {
    return 'Area นี้ทำเฉพาะกะเช้า';
  }
  const holder = shift === 'Day' ? area.dayCleaner : area.nightCleaner;
  return holder && holder.id !== exceptUserId ? `มีคนประจำแล้ว (${holder.displayName})` : null;
}

/** Why a Supervisor cannot take this building-shift, or null (facility-0048: one per building per shift). */
export function supervisorShiftBlock(
  accounts: Account[],
  buildingId: number | undefined,
  shift: Shift,
  exceptUserId?: number,
): string | null {
  if (buildingId === undefined) {
    return null;
  }
  const holder = accounts.find(
    (other) =>
      other.role === 'Supervisor' &&
      other.isActive &&
      other.buildingId === buildingId &&
      other.shift === shift &&
      other.id !== exceptUserId,
  );
  return holder ? `มีหัวหน้าแล้ว (${holder.displayName})` : null;
}

/** PhoneNumber.Normalize + IsComplete: 10 Arabic digits starting with 0; spaces and dashes are ignored. */
export function normalizePhone(text: string): string | null {
  const digits = text.replace(/[\s-]/g, '');
  return /^0\d{9}$/.test(digits) ? digits : null;
}

export const PHONE_PROBLEM = 'เบอร์โทรต้องเป็นเลข 10 หลัก ขึ้นต้นด้วย 0';
export const MIN_PASSWORD = 8;
export const MAX_PASSWORD = 100;
export const PASSWORD_PROBLEM = 'รหัสผ่านชั่วคราวต้องยาว 8–100 ตัวอักษร';

/** The add form as typed; selects hold ids as strings, '' for none. */
export interface CreateDraft {
  role: AccountRole;
  displayName: string;
  employeeId: string;
  phone: string;
  areaId: string;
  buildingId: string;
  shift: Shift | '';
  username: string;
  password: string;
}

export function emptyDraft(role: AccountRole = 'Cleaner'): CreateDraft {
  return { role, displayName: '', employeeId: '', phone: '', areaId: '', buildingId: '', shift: '', username: '', password: '' };
}

/** The request with only the role's fields, or the first thing missing. The server checks the rest. */
export function createRequest(draft: CreateDraft): { request: CreateAccountRequest } | { problem: string } {
  const displayName = draft.displayName.trim();
  if (displayName === '') {
    return { problem: 'ใส่ชื่อที่แสดงก่อน' };
  }
  if (draft.role === 'Admin') {
    const username = draft.username.trim();
    if (username === '') {
      return { problem: 'ใส่ชื่อผู้ใช้ก่อน' };
    }
    if (draft.password.length < MIN_PASSWORD || draft.password.length > MAX_PASSWORD) {
      return { problem: PASSWORD_PROBLEM };
    }
    return { request: { role: 'Admin', displayName, username, password: draft.password } };
  }
  const employeeId = draft.employeeId.trim();
  if (employeeId === '') {
    return { problem: 'ใส่รหัสพนักงานก่อน' };
  }
  const phone = normalizePhone(draft.phone);
  if (!phone) {
    return { problem: PHONE_PROBLEM };
  }
  if (draft.role === 'Supervisor' && draft.buildingId === '') {
    return { problem: 'เลือกตึกก่อน' };
  }
  if (draft.shift === '') {
    return { problem: 'เลือกกะก่อน' };
  }
  if (draft.role === 'Supervisor') {
    return {
      request: { role: 'Supervisor', displayName, employeeId, phone, buildingId: Number(draft.buildingId), shift: draft.shift },
    };
  }
  return {
    request: {
      role: 'Cleaner',
      displayName,
      employeeId,
      phone,
      shift: draft.shift,
      ...(draft.areaId ? { areaId: Number(draft.areaId) } : {}),
    },
  };
}
```

- [ ] **Step 5: The table, the shift choice and the add form**

Create `frontend/src/pages/accounts/ShiftChoice.tsx`:

```tsx
import type { Shift } from '../../shared/api/types';
import { shiftLabel } from '../../shared/utils/labels';

interface ShiftChoiceProps {
  /** The radio group's name, unique per form. */
  name: string;
  value: Shift | '';
  /** Why a shift cannot be picked here, or null. */
  blockOf: (shift: Shift) => string | null;
  disabled: boolean;
  onChange: (shift: Shift) => void;
}

/** D6's "กะ": a held slot or a night in a day-only Area cannot be picked, and the page says why. */
export function ShiftChoice({ name, value, blockOf, disabled, onChange }: ShiftChoiceProps) {
  return (
    <fieldset className="accounts-choice" disabled={disabled}>
      <legend>กะ</legend>
      {(['Day', 'Night'] as const).map((shift) => {
        const block = blockOf(shift);
        return (
          <label key={shift}>
            <input
              type="radio"
              name={name}
              disabled={block !== null}
              checked={value === shift}
              onChange={() => onChange(shift)}
            />
            {` ${shiftLabel[shift]}`}
            {block && <span className="app-muted">{` (${block})`}</span>}
          </label>
        );
      })}
    </fieldset>
  );
}
```

Create `frontend/src/pages/accounts/AccountTable.tsx`:

```tsx
import { accountRoleLabel, placeText } from './accountRules';
import type { Account } from './accountsApi';

/** Wireframe D6's table, in the server's order (Admins, Supervisors, Cleaners); deactivated rows are faded. */
export function AccountTable({ accounts }: { accounts: Account[] }) {
  if (accounts.length === 0) {
    return <p className="app-note app-note--info">ไม่พบบัญชีที่ตรงกับที่ค้นหา</p>;
  }
  return (
    <table className="app-table" aria-label="บัญชีผู้ใช้">
      <thead>
        <tr>
          <th scope="col">ชื่อ</th>
          <th scope="col">รหัสพนักงาน / ชื่อผู้ใช้</th>
          <th scope="col">บทบาท</th>
          <th scope="col">ประจำที่</th>
          <th scope="col">ใช้งาน</th>
        </tr>
      </thead>
      <tbody>
        {accounts.map((account) => (
          <tr key={account.id} className={account.isActive ? undefined : 'app-row--inactive'}>
            <th scope="row">{account.displayName}</th>
            <td>{account.loginName}</td>
            <td>{accountRoleLabel[account.role]}</td>
            <td>{placeText(account)}</td>
            <td>{account.isActive ? 'ใช้งาน' : 'ปิดใช้งาน'}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
```

Create `frontend/src/pages/accounts/CreateAccountForm.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import type { AreaSummary, Building } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import {
  accountRoleLabel,
  areaOptionLabel,
  cleanerShiftBlock,
  createRequest,
  emptyDraft,
  supervisorShiftBlock,
  type CreateDraft,
} from './accountRules';
import { useCreateAccountMutation, type Account } from './accountsApi';
import { ShiftChoice } from './ShiftChoice';

interface CreateAccountFormProps {
  accounts: Account[];
  areas: AreaSummary[];
  buildings: Building[];
}

/**
 * Wireframe D6's "เพิ่มบัญชี" (facility-0020, 0054, 0056): the fields of the chosen role only. A phone or password lives
 * in this form's state until it is sent, then it is cleared.
 */
export function CreateAccountForm({ accounts, areas, buildings }: CreateAccountFormProps) {
  const [draft, setDraft] = useState<CreateDraft>(() => emptyDraft());
  const [problem, setProblem] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [createAccount, { isLoading }] = useCreateAccountMutation();
  const set = (patch: Partial<CreateDraft>) => setDraft((current) => ({ ...current, ...patch }));
  const activeAreas = areas.filter((area) => area.isActive);
  const area = activeAreas.find((option) => String(option.id) === draft.areaId);
  const buildingId = draft.buildingId ? Number(draft.buildingId) : undefined;
  const blockOf = (shift: Shift) =>
    draft.role === 'Cleaner' ? cleanerShiftBlock(area, shift) : supervisorShiftBlock(accounts, buildingId, shift);

  // A shift that the new Area or building does not allow is dropped
  const chooseArea = (areaId: string) => {
    const next = activeAreas.find((option) => String(option.id) === areaId);
    setDraft((current) => ({
      ...current,
      areaId,
      shift: current.shift && cleanerShiftBlock(next, current.shift) ? '' : current.shift,
    }));
  };
  const chooseBuilding = (value: string) => {
    const id = value ? Number(value) : undefined;
    setDraft((current) => ({
      ...current,
      buildingId: value,
      shift: current.shift && supervisorShiftBlock(accounts, id, current.shift) ? '' : current.shift,
    }));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setNotice(null);
    const built = createRequest(draft);
    if ('problem' in built) {
      setProblem(built.problem);
      return;
    }
    setProblem(null);
    try {
      const created = await createAccount(built.request).unwrap();
      setDraft(emptyDraft(draft.role));
      setNotice(`สร้างบัญชี ${created.displayName} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className="accounts-form app-panel" aria-labelledby="accounts-create-title" onSubmit={(event) => void submit(event)}>
      <h2 id="accounts-create-title">เพิ่มบัญชี</h2>
      <fieldset className="accounts-choice accounts-choice--row" disabled={isLoading}>
        <legend>บทบาท</legend>
        {(['Cleaner', 'Supervisor', 'Admin'] as const).map((role) => (
          <label key={role}>
            <input
              type="radio"
              name="accounts-create-role"
              checked={draft.role === role}
              onChange={() => setDraft({ ...emptyDraft(role), displayName: draft.displayName })}
            />
            {` ${accountRoleLabel[role]}`}
          </label>
        ))}
      </fieldset>
      <label className="app-field">
        ชื่อที่แสดง
        <input
          className="app-input"
          maxLength={150}
          value={draft.displayName}
          disabled={isLoading}
          onChange={(event) => set({ displayName: event.target.value })}
        />
      </label>
      {draft.role === 'Admin' ? (
        <>
          <label className="app-field">
            ชื่อผู้ใช้
            <input
              className="app-input"
              autoComplete="off"
              maxLength={100}
              value={draft.username}
              disabled={isLoading}
              onChange={(event) => set({ username: event.target.value })}
            />
          </label>
          <label className="app-field">
            รหัสผ่านชั่วคราว
            <input
              type="password"
              className="app-input"
              autoComplete="new-password"
              maxLength={100}
              value={draft.password}
              disabled={isLoading}
              onChange={(event) => set({ password: event.target.value })}
            />
          </label>
          <p className="app-field__help">Admin: ชื่อผู้ใช้ + รหัสผ่านชั่วคราว 8–100 ตัวอักษร (บอกเองด้วยวาจาหรือกระดาษ)</p>
        </>
      ) : (
        <>
          <div className="app-field">
            <label htmlFor="accounts-create-employee">รหัสพนักงาน</label>
            <input
              id="accounts-create-employee"
              className="app-input"
              autoComplete="off"
              maxLength={20}
              value={draft.employeeId}
              disabled={isLoading}
              onChange={(event) => set({ employeeId: event.target.value })}
            />
            <span className="app-field__help">ห้ามซ้ำ แก้ทีหลังไม่ได้</span>
          </div>
          <div className="app-field">
            <label htmlFor="accounts-create-phone">เบอร์โทรศัพท์</label>
            <input
              id="accounts-create-phone"
              type="tel"
              inputMode="numeric"
              className="app-input"
              autoComplete="off"
              maxLength={12}
              value={draft.phone}
              disabled={isLoading}
              onChange={(event) => set({ phone: event.target.value })}
            />
            <span className="app-field__help">
              ใช้แทนรหัสผ่าน ระบบเก็บเบอร์แบบที่เปิดดูไม่ได้ ใช้ตรวจตอน login อย่างเดียว · เปลี่ยนเบอร์ทีหลังได้ด้วยปุ่ม
              "แก้เบอร์โทร"
            </span>
          </div>
          {draft.role === 'Cleaner' ? (
            <div className="app-field">
              <label htmlFor="accounts-create-area">Area ประจำ</label>
              <select
                id="accounts-create-area"
                className="app-input"
                value={draft.areaId}
                disabled={isLoading}
                onChange={(event) => chooseArea(event.target.value)}
              >
                <option value="">— ยังไม่กำหนด Area —</option>
                {activeAreas.map((option) => (
                  <option key={option.id} value={String(option.id)}>
                    {areaOptionLabel(option)}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <div className="app-field">
              <label htmlFor="accounts-create-building">ตึกที่ประจำ</label>
              <select
                id="accounts-create-building"
                className="app-input"
                value={draft.buildingId}
                disabled={isLoading}
                onChange={(event) => chooseBuilding(event.target.value)}
              >
                <option value="">เลือกตึก</option>
                {buildings.map((building) => (
                  <option key={building.id} value={String(building.id)}>
                    {building.name}
                  </option>
                ))}
              </select>
            </div>
          )}
          <ShiftChoice
            name="accounts-create-shift"
            value={draft.shift}
            blockOf={blockOf}
            disabled={isLoading}
            onChange={(shift) => set({ shift })}
          />
          <p className="app-field__help">
            {draft.role === 'Cleaner'
              ? 'Area ละ 1 คนต่อกะ · ถ้ากะนี้มีคนประจำแล้วจะเลือกไม่ได้ · Area ที่ทำเฉพาะกะเช้าเลือกได้แค่กะเช้า · ยังไม่กำหนด Area ได้ เลือกทีหลังที่ปุ่ม "แก้ไข"'
              : 'หัวหน้าประจำตึกและกะ ตึกละ 1 คนต่อกะ · ตรวจได้เฉพาะในกะตัวเอง · ไม่ต้องลงเวลา'}
          </p>
        </>
      )}
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      {notice && (
        <p className="app-note app-note--info" role="status">
          {notice}
        </p>
      )}
      <button type="submit" className="app-button app-button--primary" disabled={isLoading}>
        {isLoading ? 'กำลังสร้าง...' : 'สร้างบัญชี'}
      </button>
    </form>
  );
}
```

- [ ] **Step 6: The page and its route**

Create `frontend/src/pages/accounts/AccountsPage.tsx`:

```tsx
import { useState } from 'react';
import { useGetAreasQuery, useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { accountFilters, filterAccounts, filterCounts, filterLabel, type AccountFilter } from './accountRules';
import { useGetAccountsQuery } from './accountsApi';
import { AccountTable } from './AccountTable';
import { CreateAccountForm } from './CreateAccountForm';
import './accounts.css';

/** facility-0020, 0056, wireframe D6: every account, filtered and searched on the page, and the add form beside it. */
export function AccountsPage() {
  const accounts = useGetAccountsQuery();
  const areas = useGetAreasQuery();
  const buildings = useGetBuildingsQuery();
  const [filter, setFilter] = useState<AccountFilter>('all');
  const [search, setSearch] = useState('');
  const list = accounts.data ?? [];
  const counts = filterCounts(list);
  const error = accounts.error ?? areas.error ?? buildings.error;

  return (
    <main className="accounts-page">
      <h1>บัญชีผู้ใช้</h1>
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      <div className="accounts-layout">
        <section className="accounts-list" aria-label="รายชื่อบัญชี">
          <div className="accounts-filters" role="group" aria-label="กรองบัญชี">
            {accountFilters.map((option) => (
              <button
                key={option}
                type="button"
                className={filter === option ? 'accounts-filter accounts-filter--on' : 'accounts-filter'}
                aria-pressed={filter === option}
                onClick={() => setFilter(option)}
              >
                {`${filterLabel[option]} (${counts[option]})`}
              </button>
            ))}
          </div>
          <label className="app-field accounts-search">
            ค้นหา
            <input
              type="search"
              className="app-input"
              placeholder="ชื่อ รหัสพนักงาน หรือชื่อผู้ใช้"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
          {accounts.isLoading && <p className="app-muted">กำลังโหลดบัญชี...</p>}
          {accounts.data && <AccountTable accounts={filterAccounts(list, filter, search)} />}
          <p className="app-muted">
            ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติการสแกนยังอยู่ · ปิดใช้งาน แก้เบอร์โทร หรือรีเซ็ตรหัสผ่านแล้ว
            ทุกเครื่องของคนนั้นหลุดภายใน 5 นาที · ปิดบัญชีของตัวเองไม่ได้ · ทุกการแก้ในหน้านี้เก็บลงประวัติของ Admin
          </p>
        </section>
        <aside className="accounts-panel" aria-label="แผงบัญชี">
          <CreateAccountForm accounts={list} areas={areas.data ?? []} buildings={buildings.data ?? []} />
        </aside>
      </div>
    </main>
  );
}
```

Create `frontend/src/pages/accounts/index.ts`:

```ts
export { AccountsPage } from './AccountsPage';
```

Create `frontend/src/pages/accounts/accounts.css`:

```css
/* Wireframe D6 (facility-0020, 0056). Class prefix: accounts- */

.accounts-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.accounts-page h1 {
  font-size: 24px;
  font-weight: 700;
}

.accounts-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 360px;
  gap: var(--gap);
  align-items: start;
}

@media (max-width: 960px) {
  .accounts-layout {
    grid-template-columns: minmax(0, 1fr);
  }
}

.accounts-list,
.accounts-panel {
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-width: 0;
}

.accounts-filters {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.accounts-filter {
  padding: 6px 12px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-surface);
  color: var(--color-text);
  font-size: 14px;
  cursor: pointer;
}

.accounts-filter--on {
  border-color: var(--color-primary);
  background: var(--color-info-bg);
  color: var(--color-primary-dark);
  font-weight: 600;
}

.accounts-search {
  max-width: 360px;
}

.accounts-form {
  gap: 10px;
}

.accounts-form h2 {
  font-size: 16px;
  font-weight: 700;
}

.accounts-choice {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin: 0;
  padding: 8px 12px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  font-size: 14px;
}

.accounts-choice--row {
  flex-direction: row;
  flex-wrap: wrap;
  gap: 16px;
}

.accounts-choice legend {
  padding: 0 4px;
  font-weight: 600;
}
```

In `frontend/src/router.tsx`, add the import below the `AdminLoginPage, LoginPage` import:

```tsx
import { AccountsPage } from './pages/accounts';
```

and replace:

```tsx
              { path: 'admin/print', element: <PrintSignsPage /> },
```

with:

```tsx
              { path: 'admin/print', element: <PrintSignsPage /> },
              // facility-0020, 0056, wireframe D6
              { path: 'admin/accounts', element: <AccountsPage /> },
```

- [ ] **Step 7: Run the tests, the typecheck and the lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. Frontend tests: 218 (209 + 3 rules + 6 page).

- [ ] **Step 8: Handoff entry**

```markdown
- <date> · Task 8 done: accounts page D6 (/admin/accounts): filters ทั้งหมด / แม่บ้าน / หัวหน้า / Admin / ปิดใช้งาน with counts, search by name or login name, the table with role, place and active; the add form sends only the chosen role's fields (Cleaner: employee ID, phone, optional Area, shift; Supervisor: building and shift; Admin: username and temporary password), a held shift or a night in a day-only Area cannot be picked, the phone and password are cleared once sent. Frontend tests <n> passed. Next: Task 9 (account actions).
```

- [ ] **Step 9: Commit**

```bash
git add docs/handoff.md frontend/src
git commit -m "feat(frontend): accounts page D6: table, filters, search, add an account"
```

---

### Task 9: Account actions: edit, phone, password, deactivate, activate (D6)

**Files:**
- Modify: `frontend/src/pages/accounts/accountsApi.ts`, `accountRules.ts`, `accountRules.test.ts`, `AccountTable.tsx`, `AccountsPage.tsx`, `accounts.css`
- Create: `frontend/src/pages/accounts/EditAccountForm.tsx`, `SecretForms.tsx`, `DeactivateConfirm.tsx`
- Test: `frontend/src/pages/accounts/AccountActions.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: everything from Task 8; `useAppSelector` (`store/hooks.ts`) for the logged-in account's id.
- Produces:
  - In `accountsApi.ts`: `UpdateAccountBody = { displayName: string; areaId?: number; buildingId?: number; shift?: Shift }`, and the hooks `useUpdateAccountMutation()` (`{ id, body }`), `useSetAccountActiveMutation()` (`{ id, active }`), `useChangePhoneMutation()` (`{ id, phone }`) and `useResetPasswordMutation()` (`{ id, password }`).
  - In `accountRules.ts`: `EditDraft`, `editDraftOf(account)`, and `updateRequest(account, draft): { body } | { problem }`.
  - `AccountAction = 'edit' | 'phone' | 'password' | 'deactivate' | 'activate'`, exported from `AccountTable.tsx`.
  - `AccountTable({ accounts, me, busy, onAction })`.

- [ ] **Step 1: Write the failing tests**

In `frontend/src/pages/accounts/accountRules.test.ts`, change the import from `'./accountRules'` to:

```ts
import { createRequest, editDraftOf, emptyDraft, normalizePhone, placeText, updateRequest } from './accountRules';
```

and add inside the `describe` block:

```ts
  it('an edit sends the role’s place fields, and a Cleaner’s current Area to keep it (the PUT replaces it)', () => {
    const cleaner = account();
    expect(updateRequest(cleaner, editDraftOf(cleaner))).toEqual({
      body: { displayName: 'แม่บ้านทดสอบ', shift: 'Day', areaId: 1 },
    });
    const away = account({ isActive: false, areaId: undefined, areaCode: undefined });
    expect(updateRequest(away, { ...editDraftOf(away), areaId: '2' })).toEqual({
      body: { displayName: 'แม่บ้านทดสอบ', shift: 'Day' },
    });
    const supervisor = account({ role: 'Supervisor', areaId: undefined, buildingId: 2, shift: 'Night' });
    expect(updateRequest(supervisor, editDraftOf(supervisor))).toEqual({
      body: { displayName: 'แม่บ้านทดสอบ', buildingId: 2, shift: 'Night' },
    });
    const admin = account({ role: 'Admin', areaId: undefined, shift: undefined });
    expect(updateRequest(admin, editDraftOf(admin))).toEqual({ body: { displayName: 'แม่บ้านทดสอบ' } });
    expect(updateRequest(cleaner, { ...editDraftOf(cleaner), displayName: ' ' })).toEqual({ problem: 'ใส่ชื่อที่แสดงก่อน' });
  });
```

Create `frontend/src/pages/accounts/AccountActions.test.tsx`:

```tsx
import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { account, accounts, adminSession, areaSummary, buildings, officeSummary } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/accounts': () => ({ status: 200, body: accounts }),
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    ...routes,
  });
}

/** Opens the page and presses a row's action button, e.g. "แก้ไข แม่บ้านว่าง". */
async function openAndPress(action: string) {
  const app = await renderApp('/admin/accounts');
  await screen.findByRole('table', { name: 'บัญชีผู้ใช้' });
  await app.user.click(screen.getByRole('button', { name: action }));
  return app;
}

const sent = (server: ReturnType<typeof serve>, method: string, path: string) =>
  server.calls.find((call) => call.method === method && call.path === path)?.body;

describe('account actions (D6, facility-0020, 0054)', () => {
  it('edits a Cleaner with no Area: name, Area and shift', async () => {
    const server = serve({
      'PUT /api/admin/accounts/23': () => ({
        status: 200,
        body: account({ id: 23, loginName: '650003', displayName: 'แม่บ้านว่าง ใหม่', areaId: 2, areaCode: 'AR02' }),
      }),
    });
    const { user } = await openAndPress('แก้ไข แม่บ้านว่าง');

    const form = screen.getByRole('form', { name: 'แก้ไขบัญชี · แม่บ้านว่าง' });
    expect(within(form).getByText('แม่บ้าน · 650003 (บทบาทและรหัสแก้ไม่ได้)')).toBeInTheDocument();
    await user.clear(within(form).getByLabelText('ชื่อที่แสดง'));
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านว่าง ใหม่');
    await user.selectOptions(within(form).getByLabelText('Area ประจำ'), '2');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).toBeChecked();
    await user.click(within(form).getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByText('บันทึกบัญชี แม่บ้านว่าง ใหม่ แล้ว')).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/23')).toEqual({
      displayName: 'แม่บ้านว่าง ใหม่',
      shift: 'Day',
      areaId: 2,
    });
    // The panel goes back to "เพิ่มบัญชี"
    expect(screen.getByRole('form', { name: 'เพิ่มบัญชี' })).toBeInTheDocument();
  });

  it('editing a Cleaner keeps their Area: their own slot is not "held"', async () => {
    const server = serve({ 'PUT /api/admin/accounts/21': () => ({ status: 200, body: account() }) });
    const { user } = await openAndPress('แก้ไข แม่บ้านทดสอบ');

    const form = screen.getByRole('form', { name: 'แก้ไขบัญชี · แม่บ้านทดสอบ' });
    expect(within(form).getByLabelText('Area ประจำ')).toHaveValue('1');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).toBeEnabled();
    expect(within(form).getByRole('radio', { name: /^กะดึก/ })).toBeDisabled();
    await user.click(within(form).getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByText('บันทึกบัญชี แม่บ้านทดสอบ แล้ว')).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/21')).toEqual({ displayName: 'แม่บ้านทดสอบ', shift: 'Day', areaId: 1 });
  });

  it('a Supervisor’s edit sends building and shift; an Admin’s only the name', async () => {
    const server = serve({
      'PUT /api/admin/accounts/31': () => ({ status: 200, body: accounts[2] }),
      'PUT /api/admin/accounts/8': () => ({ status: 200, body: accounts[1] }),
    });
    const { user } = await openAndPress('แก้ไข หัวหน้าทดสอบ');

    const supervisor = screen.getByRole('form', { name: 'แก้ไขบัญชี · หัวหน้าทดสอบ' });
    expect(within(supervisor).getByLabelText('ตึกที่ประจำ')).toHaveValue('1');
    await user.click(within(supervisor).getByRole('button', { name: 'บันทึก' }));
    expect(await screen.findByText('บันทึกบัญชี หัวหน้าทดสอบ แล้ว')).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/31')).toEqual({ displayName: 'หัวหน้าทดสอบ', buildingId: 1, shift: 'Day' });

    await user.click(screen.getByRole('button', { name: 'แก้ไข ผู้ดูแลคนที่สอง' }));
    const admin = screen.getByRole('form', { name: 'แก้ไขบัญชี · ผู้ดูแลคนที่สอง' });
    expect(within(admin).queryByRole('group', { name: 'กะ' })).not.toBeInTheDocument();
    await user.click(within(admin).getByRole('button', { name: 'บันทึก' }));
    expect(await screen.findByText('บันทึกบัญชี ผู้ดูแลคนที่สอง แล้ว')).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/8')).toEqual({ displayName: 'ผู้ดูแลคนที่สอง' });
  });

  it('a deactivated Cleaner gets no Area until activated (plan 5 decision 4)', async () => {
    serve();
    await openAndPress('แก้ไข แม่บ้านที่ลาออก');

    const form = screen.getByRole('form', { name: 'แก้ไขบัญชี · แม่บ้านที่ลาออก' });
    expect(within(form).getByLabelText('Area ประจำ')).toBeDisabled();
    expect(within(form).getByText('เปิดใช้งานบัญชีก่อน แล้วค่อยเลือก Area')).toBeInTheDocument();
  });

  it('changes a phone: checked first, sent once, and never kept on the page (facility-0054)', async () => {
    const server = serve({ 'PUT /api/admin/accounts/21/phone': () => ({ status: 204 }) });
    const { user } = await openAndPress('แก้เบอร์โทร แม่บ้านทดสอบ');
    expect(screen.queryByRole('button', { name: 'แก้เบอร์โทร ผู้ดูแลคนที่สอง' })).not.toBeInTheDocument();

    const form = screen.getByRole('form', { name: 'แก้เบอร์โทร · แม่บ้านทดสอบ' });
    await user.type(within(form).getByLabelText('เบอร์โทรใหม่'), '123');
    await user.click(within(form).getByRole('button', { name: 'บันทึกเบอร์ใหม่' }));
    expect(within(form).getByRole('alert')).toHaveTextContent('เบอร์โทรต้องเป็นเลข 10 หลัก ขึ้นต้นด้วย 0');

    await user.clear(within(form).getByLabelText('เบอร์โทรใหม่'));
    await user.type(within(form).getByLabelText('เบอร์โทรใหม่'), '0810000099');
    await user.click(within(form).getByRole('button', { name: 'บันทึกเบอร์ใหม่' }));

    expect(
      await screen.findByText('เปลี่ยนเบอร์โทรของ แม่บ้านทดสอบ แล้ว ทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่'),
    ).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/21/phone')).toEqual({ phone: '0810000099' });
    expect(screen.queryByDisplayValue('0810000099')).not.toBeInTheDocument();
  });

  it('resets an Admin’s password, and says when it was the Admin’s own', async () => {
    const server = serve({
      'PUT /api/admin/accounts/8/password': () => ({ status: 204 }),
      'PUT /api/admin/accounts/7/password': () => ({ status: 204 }),
    });
    const { user } = await openAndPress('รีเซ็ตรหัสผ่าน ผู้ดูแลคนที่สอง');
    expect(screen.queryByRole('button', { name: 'รีเซ็ตรหัสผ่าน แม่บ้านทดสอบ' })).not.toBeInTheDocument();

    const form = screen.getByRole('form', { name: 'รีเซ็ตรหัสผ่าน · ผู้ดูแลคนที่สอง' });
    await user.type(within(form).getByLabelText('รหัสผ่านชั่วคราวใหม่'), 'temporary-456');
    await user.click(within(form).getByRole('button', { name: 'บันทึกรหัสผ่านใหม่' }));
    expect(
      await screen.findByText(
        'รีเซ็ตรหัสผ่านของ ผู้ดูแลคนที่สอง แล้ว บอกรหัสใหม่ด้วยวาจาหรือกระดาษ ทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่',
      ),
    ).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/8/password')).toEqual({ password: 'temporary-456' });

    await user.click(screen.getByRole('button', { name: 'รีเซ็ตรหัสผ่าน ผู้ดูแลทดสอบ' }));
    const own = screen.getByRole('form', { name: 'รีเซ็ตรหัสผ่าน · ผู้ดูแลทดสอบ' });
    await user.type(within(own).getByLabelText('รหัสผ่านชั่วคราวใหม่'), 'temporary-789');
    await user.click(within(own).getByRole('button', { name: 'บันทึกรหัสผ่านใหม่' }));
    expect(
      await screen.findByText(
        'รีเซ็ตรหัสผ่านของ ผู้ดูแลทดสอบ แล้ว บอกรหัสใหม่ด้วยวาจาหรือกระดาษ ทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่ รวมเครื่องนี้ด้วย',
      ),
    ).toBeInTheDocument();
  });

  it('deactivating asks first and says the Cleaner’s Area slot goes; the Admin cannot deactivate themselves', async () => {
    const server = serve({
      'POST /api/admin/accounts/21/deactivate': () => ({
        status: 200,
        body: account({ isActive: false, areaId: undefined, areaCode: undefined }),
      }),
    });
    const { user } = await openAndPress('ปิดใช้งาน แม่บ้านทดสอบ');
    expect(screen.queryByRole('button', { name: 'ปิดใช้งาน ผู้ดูแลทดสอบ' })).not.toBeInTheDocument();

    const ask = screen.getByRole('alertdialog', { name: 'ปิดใช้งานบัญชี แม่บ้านทดสอบ?' });
    expect(ask).toHaveTextContent('แม่บ้านทดสอบ เข้าสู่ระบบไม่ได้ และทุกเครื่องหลุดภายใน 5 นาที ประวัติยังอยู่ เปิดใช้งานกลับได้');
    expect(ask).toHaveTextContent('AR01 กะเช้า จะว่าง เปิดใช้งานกลับแล้วต้องเลือก Area ใหม่');
    await user.click(within(ask).getByRole('button', { name: 'ยกเลิก' }));
    expect(server.calls.some((call) => call.path.endsWith('/deactivate'))).toBe(false);
    expect(screen.getByRole('form', { name: 'เพิ่มบัญชี' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ปิดใช้งาน แม่บ้านทดสอบ' }));
    await user.click(screen.getByRole('button', { name: 'ยืนยัน ปิดใช้งาน' }));
    expect(await screen.findByText('ปิดใช้งาน แม่บ้านทดสอบ แล้ว')).toBeInTheDocument();
    expect(server.calls.filter((call) => call.path === '/api/admin/accounts/21/deactivate')).toHaveLength(1);
  });

  it('activating is one press, and says a Cleaner comes back with no Area', async () => {
    const server = serve({
      'POST /api/admin/accounts/26/activate': () => ({
        status: 200,
        body: account({ id: 26, loginName: '650006', displayName: 'แม่บ้านที่ลาออก', areaId: undefined, areaCode: undefined }),
      }),
    });
    await openAndPress('เปิดใช้งาน แม่บ้านที่ลาออก');

    expect(
      await screen.findByText('เปิดใช้งาน แม่บ้านที่ลาออก แล้ว ยังไม่มี Area เลือก Area ได้ที่ "แก้ไข"'),
    ).toBeInTheDocument();
    expect(server.calls.filter((call) => call.path === '/api/admin/accounts/26/activate')).toHaveLength(1);
  });

  it('an activation the server refuses shows its message', async () => {
    serve({
      'POST /api/admin/accounts/26/activate': () => ({
        status: 409,
        body: { message: 'กะเช้าของ AR01 มีแม่บ้านประจำแล้ว (แม่บ้านทดสอบ)' },
      }),
    });
    await openAndPress('เปิดใช้งาน แม่บ้านที่ลาออก');

    expect(await screen.findByRole('alert')).toHaveTextContent('กะเช้าของ AR01 มีแม่บ้านประจำแล้ว (แม่บ้านทดสอบ)');
  });
});
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/accounts`
Expected: FAIL. `editDraftOf` and `updateRequest` do not exist, and there is no "แก้ไข แม่บ้านว่าง" button.

- [ ] **Step 3: The saves and the edit rules**

In `frontend/src/pages/accounts/accountsApi.ts`, replace:

```ts
    createAccount: build.mutation<Account, CreateAccountRequest>({
      query: (body) => ({ url: '/admin/accounts', method: 'POST', body }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const { useGetAccountsQuery, useCreateAccountMutation } = accountsApi;
```

with:

```ts
    createAccount: build.mutation<Account, CreateAccountRequest>({
      query: (body) => ({ url: '/admin/accounts', method: 'POST', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    updateAccount: build.mutation<Account, { id: number; body: UpdateAccountBody }>({
      query: ({ id, body }) => ({ url: `/admin/accounts/${id}`, method: 'PUT', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    /** facility-0020: no delete. Deactivation revokes every session and frees a Cleaner's Area (plan 5 decision 3). */
    setAccountActive: build.mutation<Account, { id: number; active: boolean }>({
      query: ({ id, active }) => ({ url: `/admin/accounts/${id}/${active ? 'activate' : 'deactivate'}`, method: 'POST' }),
      invalidatesTags: ['AdminSetup'],
    }),
    /** facility-0054 rule 4. 204; every session of the person is revoked. */
    changePhone: build.mutation<void, { id: number; phone: string }>({
      query: ({ id, phone }) => ({ url: `/admin/accounts/${id}/phone`, method: 'PUT', body: { phone } }),
    }),
    /** facility-0020: an Admin's temporary password, told by voice or on paper. 204. */
    resetPassword: build.mutation<void, { id: number; password: string }>({
      query: ({ id, password }) => ({ url: `/admin/accounts/${id}/password`, method: 'PUT', body: { password } }),
    }),
  }),
});

export const {
  useGetAccountsQuery,
  useCreateAccountMutation,
  useUpdateAccountMutation,
  useSetAccountActiveMutation,
  useChangePhoneMutation,
  useResetPasswordMutation,
} = accountsApi;
```

and, above `const accountsApi`, add:

```ts
/** UpdateAccountRequest: the role and login name are fixed; the place fields replace the current ones. */
export interface UpdateAccountBody {
  displayName: string;
  areaId?: number;
  buildingId?: number;
  shift?: Shift;
}
```

The phone and password saves invalidate nothing: no shown data changes.

Append to `frontend/src/pages/accounts/accountRules.ts` (and add `UpdateAccountBody` to its import from `'./accountsApi'`):

```ts
/** The edit form as typed. */
export interface EditDraft {
  displayName: string;
  areaId: string;
  buildingId: string;
  shift: Shift | '';
}

export function editDraftOf(account: Account): EditDraft {
  return {
    displayName: account.displayName,
    areaId: account.areaId ? String(account.areaId) : '',
    buildingId: account.buildingId ? String(account.buildingId) : '',
    shift: account.shift ?? '',
  };
}

/**
 * The PUT replaces every place field, so a Cleaner's current Area is sent back to keep it (handoff note). A
 * deactivated Cleaner holds no Area (plan 5 decision 4), so none is sent for one.
 */
export function updateRequest(account: Account, draft: EditDraft): { body: UpdateAccountBody } | { problem: string } {
  const displayName = draft.displayName.trim();
  if (displayName === '') {
    return { problem: 'ใส่ชื่อที่แสดงก่อน' };
  }
  if (account.role === 'Admin') {
    return { body: { displayName } };
  }
  if (account.role === 'Supervisor' && draft.buildingId === '') {
    return { problem: 'เลือกตึกก่อน' };
  }
  if (draft.shift === '') {
    return { problem: 'เลือกกะก่อน' };
  }
  if (account.role === 'Supervisor') {
    return { body: { displayName, buildingId: Number(draft.buildingId), shift: draft.shift } };
  }
  return {
    body: {
      displayName,
      shift: draft.shift,
      ...(account.isActive && draft.areaId ? { areaId: Number(draft.areaId) } : {}),
    },
  };
}
```

The import line at the top of `accountRules.ts` becomes:

```ts
import type { Account, AccountRole, CreateAccountRequest, UpdateAccountBody } from './accountsApi';
```

- [ ] **Step 4: The panels**

Create `frontend/src/pages/accounts/EditAccountForm.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import type { AreaSummary, Building } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import {
  accountRoleLabel,
  areaOptionLabel,
  cleanerShiftBlock,
  editDraftOf,
  supervisorShiftBlock,
  updateRequest,
  type EditDraft,
} from './accountRules';
import { useUpdateAccountMutation, type Account } from './accountsApi';
import { ShiftChoice } from './ShiftChoice';

interface EditAccountFormProps {
  account: Account;
  accounts: Account[];
  areas: AreaSummary[];
  buildings: Building[];
  onDone: (text: string) => void;
  onCancel: () => void;
}

/** D6's "แก้ไข": name and place; the role and the login name are fixed (plan 5 decision 2). Keyed by account id. */
export function EditAccountForm({ account, accounts, areas, buildings, onDone, onCancel }: EditAccountFormProps) {
  const [draft, setDraft] = useState<EditDraft>(() => editDraftOf(account));
  const [problem, setProblem] = useState<string | null>(null);
  const [updateAccount, { isLoading }] = useUpdateAccountMutation();
  const set = (patch: Partial<EditDraft>) => setDraft((current) => ({ ...current, ...patch }));
  // The current Area stays on offer even if it was deactivated since (plan 5 final-review F3)
  const areaChoices = areas.filter((area) => area.isActive || area.id === account.areaId);
  const area = areaChoices.find((option) => String(option.id) === draft.areaId);
  const buildingId = draft.buildingId ? Number(draft.buildingId) : undefined;
  const blockOf = (shift: Shift) =>
    account.role === 'Cleaner'
      ? cleanerShiftBlock(area, shift, account.id)
      : supervisorShiftBlock(accounts, buildingId, shift, account.id);
  const titleId = `accounts-edit-${account.id}`;

  const chooseArea = (areaId: string) => {
    const next = areaChoices.find((option) => String(option.id) === areaId);
    setDraft((current) => ({
      ...current,
      areaId,
      shift: current.shift && cleanerShiftBlock(next, current.shift, account.id) ? '' : current.shift,
    }));
  };
  const chooseBuilding = (value: string) => {
    const id = value ? Number(value) : undefined;
    setDraft((current) => ({
      ...current,
      buildingId: value,
      shift: current.shift && supervisorShiftBlock(accounts, id, current.shift, account.id) ? '' : current.shift,
    }));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const built = updateRequest(account, draft);
    if ('problem' in built) {
      setProblem(built.problem);
      return;
    }
    setProblem(null);
    try {
      const saved = await updateAccount({ id: account.id, body: built.body }).unwrap();
      onDone(`บันทึกบัญชี ${saved.displayName} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className="accounts-form app-panel" aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      <h2 id={titleId}>{`แก้ไขบัญชี · ${account.displayName}`}</h2>
      <p className="app-muted">{`${accountRoleLabel[account.role]} · ${account.loginName} (บทบาทและรหัสแก้ไม่ได้)`}</p>
      <label className="app-field">
        ชื่อที่แสดง
        <input
          className="app-input"
          maxLength={150}
          value={draft.displayName}
          disabled={isLoading}
          onChange={(event) => set({ displayName: event.target.value })}
        />
      </label>
      {account.role === 'Cleaner' && (
        <div className="app-field">
          <label htmlFor={`${titleId}-area`}>Area ประจำ</label>
          <select
            id={`${titleId}-area`}
            className="app-input"
            value={draft.areaId}
            disabled={isLoading || !account.isActive}
            onChange={(event) => chooseArea(event.target.value)}
          >
            <option value="">— ยังไม่กำหนด Area —</option>
            {areaChoices.map((option) => (
              <option key={option.id} value={String(option.id)}>
                {areaOptionLabel(option, account.id)}
              </option>
            ))}
          </select>
          {!account.isActive && <span className="app-field__help">เปิดใช้งานบัญชีก่อน แล้วค่อยเลือก Area</span>}
        </div>
      )}
      {account.role === 'Supervisor' && (
        <div className="app-field">
          <label htmlFor={`${titleId}-building`}>ตึกที่ประจำ</label>
          <select
            id={`${titleId}-building`}
            className="app-input"
            value={draft.buildingId}
            disabled={isLoading}
            onChange={(event) => chooseBuilding(event.target.value)}
          >
            <option value="">เลือกตึก</option>
            {buildings.map((building) => (
              <option key={building.id} value={String(building.id)}>
                {building.name}
              </option>
            ))}
          </select>
        </div>
      )}
      {account.role !== 'Admin' && (
        <ShiftChoice
          name={`${titleId}-shift`}
          value={draft.shift}
          blockOf={blockOf}
          disabled={isLoading}
          onChange={(shift) => set({ shift })}
        />
      )}
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="accounts-buttons">
        <button type="submit" className="app-button app-button--primary" disabled={isLoading}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึก'}
        </button>
        <button type="button" className="app-button" onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </form>
  );
}
```

Create `frontend/src/pages/accounts/SecretForms.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { MAX_PASSWORD, MIN_PASSWORD, normalizePhone, PASSWORD_PROBLEM, PHONE_PROBLEM } from './accountRules';
import { useChangePhoneMutation, useResetPasswordMutation, type Account } from './accountsApi';

interface SecretFormProps {
  account: Account;
  onDone: (text: string) => void;
  onCancel: () => void;
}

/**
 * facility-0054 rule 4: the Admin types a Cleaner's or Supervisor's new phone. It is sent once, then cleared; every
 * device of the person must log in again.
 */
export function PhoneForm({ account, onDone, onCancel }: SecretFormProps) {
  const [phone, setPhone] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [changePhone, { isLoading }] = useChangePhoneMutation();
  const titleId = `accounts-phone-${account.id}`;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const normalized = normalizePhone(phone);
    if (!normalized) {
      setProblem(PHONE_PROBLEM);
      return;
    }
    setProblem(null);
    try {
      await changePhone({ id: account.id, phone: normalized }).unwrap();
      setPhone('');
      onDone(`เปลี่ยนเบอร์โทรของ ${account.displayName} แล้ว ทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className="accounts-form app-panel" aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      <h2 id={titleId}>{`แก้เบอร์โทร · ${account.displayName}`}</h2>
      <div className="app-field">
        <label htmlFor={`${titleId}-input`}>เบอร์โทรใหม่</label>
        <input
          id={`${titleId}-input`}
          type="tel"
          inputMode="numeric"
          className="app-input"
          autoComplete="off"
          maxLength={12}
          value={phone}
          disabled={isLoading}
          onChange={(event) => setPhone(event.target.value)}
        />
        <span className="app-field__help">ใช้แทนรหัสผ่าน ระบบเก็บเบอร์แบบที่เปิดดูไม่ได้ · บันทึกแล้วทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่</span>
      </div>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="accounts-buttons">
        <button type="submit" className="app-button app-button--primary" disabled={isLoading}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึกเบอร์ใหม่'}
        </button>
        <button type="button" className="app-button" onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </form>
  );
}

/** facility-0020: a new temporary password for an Admin, told by voice or on paper; sent once, then cleared. */
export function PasswordForm({ account, isSelf, onDone, onCancel }: SecretFormProps & { isSelf: boolean }) {
  const [password, setPassword] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [resetPassword, { isLoading }] = useResetPasswordMutation();
  const titleId = `accounts-password-${account.id}`;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (password.length < MIN_PASSWORD || password.length > MAX_PASSWORD) {
      setProblem(PASSWORD_PROBLEM);
      return;
    }
    setProblem(null);
    try {
      await resetPassword({ id: account.id, password }).unwrap();
      setPassword('');
      // Resetting your own password revokes this browser's session too (facility-0014)
      onDone(
        `รีเซ็ตรหัสผ่านของ ${account.displayName} แล้ว บอกรหัสใหม่ด้วยวาจาหรือกระดาษ ทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่${isSelf ? ' รวมเครื่องนี้ด้วย' : ''}`,
      );
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className="accounts-form app-panel" aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      <h2 id={titleId}>{`รีเซ็ตรหัสผ่าน · ${account.displayName}`}</h2>
      <div className="app-field">
        <label htmlFor={`${titleId}-input`}>รหัสผ่านชั่วคราวใหม่</label>
        <input
          id={`${titleId}-input`}
          type="password"
          className="app-input"
          autoComplete="new-password"
          maxLength={MAX_PASSWORD}
          value={password}
          disabled={isLoading}
          onChange={(event) => setPassword(event.target.value)}
        />
        <span className="app-field__help">8–100 ตัวอักษร บอกเองด้วยวาจาหรือกระดาษ</span>
      </div>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="accounts-buttons">
        <button type="submit" className="app-button app-button--primary" disabled={isLoading}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึกรหัสผ่านใหม่'}
        </button>
        <button type="button" className="app-button" onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </form>
  );
}
```

Create `frontend/src/pages/accounts/DeactivateConfirm.tsx`:

```tsx
import { useState } from 'react';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { useSetAccountActiveMutation, type Account } from './accountsApi';

interface DeactivateConfirmProps {
  account: Account;
  onDone: (text: string) => void;
  onCancel: () => void;
}

/**
 * Plan 10 decision 7: deactivating asks once, because a Cleaner loses their Area and activation does not give it back
 * (plan 5 decision 3). The devices of the person drop within 5 minutes (facility-0012, 0014).
 */
export function DeactivateConfirm({ account, onDone, onCancel }: DeactivateConfirmProps) {
  const [setAccountActive, { isLoading }] = useSetAccountActiveMutation();
  const [problem, setProblem] = useState<string | null>(null);
  const titleId = `accounts-deactivate-${account.id}`;

  const confirm = async () => {
    try {
      await setAccountActive({ id: account.id, active: false }).unwrap();
      onDone(`ปิดใช้งาน ${account.displayName} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <div className="app-confirm" role="alertdialog" aria-labelledby={titleId} aria-describedby={`${titleId}-detail`}>
      <p id={titleId} className="app-confirm__title">{`ปิดใช้งานบัญชี ${account.displayName}?`}</p>
      <div id={`${titleId}-detail`}>
        <p>{`${account.displayName} เข้าสู่ระบบไม่ได้ และทุกเครื่องหลุดภายใน 5 นาที ประวัติยังอยู่ เปิดใช้งานกลับได้`}</p>
        {account.role === 'Cleaner' && account.areaCode && account.shift && (
          <p>{`${account.areaCode} ${shiftShortLabel[account.shift]} จะว่าง เปิดใช้งานกลับแล้วต้องเลือก Area ใหม่`}</p>
        )}
      </div>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="app-confirm__buttons">
        <button type="button" className="app-button app-button--warn" disabled={isLoading} onClick={() => void confirm()}>
          {isLoading ? 'กำลังปิดใช้งาน...' : 'ยืนยัน ปิดใช้งาน'}
        </button>
        {/* The safe choice has the focus */}
        <button type="button" className="app-button" autoFocus disabled={isLoading} onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Actions in the table, panels on the page**

Replace the whole of `frontend/src/pages/accounts/AccountTable.tsx` with:

```tsx
import { accountRoleLabel, placeText } from './accountRules';
import type { Account } from './accountsApi';

export type AccountAction = 'edit' | 'phone' | 'password' | 'deactivate' | 'activate';

interface AccountTableProps {
  accounts: Account[];
  /** The logged-in Admin's id: they cannot deactivate themselves (facility-0020). */
  me: number | undefined;
  /** An activation is in flight. */
  busy: boolean;
  onAction: (action: AccountAction, account: Account) => void;
}

/**
 * Wireframe D6's table, in the server's order (Admins, Supervisors, Cleaners); deactivated rows are faded. Each
 * button names its person, so a screen reader and the tests can tell the rows apart.
 */
export function AccountTable({ accounts, me, busy, onAction }: AccountTableProps) {
  if (accounts.length === 0) {
    return <p className="app-note app-note--info">ไม่พบบัญชีที่ตรงกับที่ค้นหา</p>;
  }
  return (
    <table className="app-table" aria-label="บัญชีผู้ใช้">
      <thead>
        <tr>
          <th scope="col">ชื่อ</th>
          <th scope="col">รหัสพนักงาน / ชื่อผู้ใช้</th>
          <th scope="col">บทบาท</th>
          <th scope="col">ประจำที่</th>
          <th scope="col">ใช้งาน</th>
          <th scope="col">จัดการ</th>
        </tr>
      </thead>
      <tbody>
        {accounts.map((account) => (
          <tr key={account.id} className={account.isActive ? undefined : 'app-row--inactive'}>
            <th scope="row">{account.displayName}</th>
            <td>{account.loginName}</td>
            <td>{accountRoleLabel[account.role]}</td>
            <td>{placeText(account)}</td>
            <td>{account.isActive ? 'ใช้งาน' : 'ปิดใช้งาน'}</td>
            <td className="accounts-actions">
              <button
                type="button"
                className="app-link-button"
                aria-label={`แก้ไข ${account.displayName}`}
                onClick={() => onAction('edit', account)}
              >
                แก้ไข
              </button>
              {account.role === 'Admin' ? (
                <button
                  type="button"
                  className="app-link-button"
                  aria-label={`รีเซ็ตรหัสผ่าน ${account.displayName}`}
                  onClick={() => onAction('password', account)}
                >
                  รีเซ็ตรหัสผ่าน
                </button>
              ) : (
                <button
                  type="button"
                  className="app-link-button"
                  aria-label={`แก้เบอร์โทร ${account.displayName}`}
                  onClick={() => onAction('phone', account)}
                >
                  แก้เบอร์โทร
                </button>
              )}
              {account.isActive
                ? account.id !== me && (
                    <button
                      type="button"
                      className="app-link-button"
                      aria-label={`ปิดใช้งาน ${account.displayName}`}
                      onClick={() => onAction('deactivate', account)}
                    >
                      ปิดใช้งาน
                    </button>
                  )
                : (
                    <button
                      type="button"
                      className="app-link-button"
                      aria-label={`เปิดใช้งาน ${account.displayName}`}
                      disabled={busy}
                      onClick={() => onAction('activate', account)}
                    >
                      เปิดใช้งาน
                    </button>
                  )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
```

Replace the whole of `frontend/src/pages/accounts/AccountsPage.tsx` with:

```tsx
import { useState } from 'react';
import { useGetAreasQuery, useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useAppSelector } from '../../store/hooks';
import { accountFilters, filterAccounts, filterCounts, filterLabel, type AccountFilter } from './accountRules';
import { useGetAccountsQuery, useSetAccountActiveMutation, type Account } from './accountsApi';
import { AccountTable, type AccountAction } from './AccountTable';
import { CreateAccountForm } from './CreateAccountForm';
import { DeactivateConfirm } from './DeactivateConfirm';
import { EditAccountForm } from './EditAccountForm';
import { PasswordForm, PhoneForm } from './SecretForms';
import './accounts.css';

type Panel = { kind: 'create' } | { kind: 'edit' | 'phone' | 'password' | 'deactivate'; id: number };

type Notice = { kind: 'ok' | 'error'; text: string } | null;

/**
 * facility-0020, 0056, wireframe D6: every account, filtered and searched on the page. The side panel adds an account,
 * or holds the action of the row pressed; when that action is done it goes back to "เพิ่มบัญชี".
 */
export function AccountsPage() {
  const accounts = useGetAccountsQuery();
  const areas = useGetAreasQuery();
  const buildings = useGetBuildingsQuery();
  const me = useAppSelector((state) => state.auth.currentUser?.id);
  const [filter, setFilter] = useState<AccountFilter>('all');
  const [search, setSearch] = useState('');
  const [panel, setPanel] = useState<Panel>({ kind: 'create' });
  const [notice, setNotice] = useState<Notice>(null);
  const [setAccountActive, activating] = useSetAccountActiveMutation();
  const list = accounts.data ?? [];
  const counts = filterCounts(list);
  const error = accounts.error ?? areas.error ?? buildings.error;
  const target = panel.kind === 'create' ? undefined : list.find((account) => account.id === panel.id);
  const backToCreate = () => setPanel({ kind: 'create' });
  const done = (text: string) => {
    setNotice({ kind: 'ok', text });
    backToCreate();
  };

  // facility-0020: activation is one press; a Cleaner comes back with no Area (plan 5 decision 3)
  const activate = async (account: Account) => {
    try {
      await setAccountActive({ id: account.id, active: true }).unwrap();
      setNotice({
        kind: 'ok',
        text:
          account.role === 'Cleaner'
            ? `เปิดใช้งาน ${account.displayName} แล้ว ยังไม่มี Area เลือก Area ได้ที่ "แก้ไข"`
            : `เปิดใช้งาน ${account.displayName} แล้ว`,
      });
    } catch (activationError) {
      setNotice({ kind: 'error', text: getErrorMessage(activationError) });
    }
  };

  const act = (action: AccountAction, account: Account) => {
    setNotice(null);
    if (action === 'activate') {
      void activate(account);
      return;
    }
    setPanel({ kind: action, id: account.id });
  };

  return (
    <main className="accounts-page">
      <h1>บัญชีผู้ใช้</h1>
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      <div className="accounts-layout">
        <section className="accounts-list" aria-label="รายชื่อบัญชี">
          <div className="accounts-filters" role="group" aria-label="กรองบัญชี">
            {accountFilters.map((option) => (
              <button
                key={option}
                type="button"
                className={filter === option ? 'accounts-filter accounts-filter--on' : 'accounts-filter'}
                aria-pressed={filter === option}
                onClick={() => setFilter(option)}
              >
                {`${filterLabel[option]} (${counts[option]})`}
              </button>
            ))}
          </div>
          <label className="app-field accounts-search">
            ค้นหา
            <input
              type="search"
              className="app-input"
              placeholder="ชื่อ รหัสพนักงาน หรือชื่อผู้ใช้"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
          {notice && (
            <p
              className={notice.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
              role={notice.kind === 'ok' ? 'status' : 'alert'}
            >
              {notice.text}
            </p>
          )}
          {accounts.isLoading && <p className="app-muted">กำลังโหลดบัญชี...</p>}
          {accounts.data && (
            <AccountTable
              accounts={filterAccounts(list, filter, search)}
              me={me}
              busy={activating.isLoading}
              onAction={act}
            />
          )}
          <p className="app-muted">
            ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติการสแกนยังอยู่ · ปิดใช้งาน แก้เบอร์โทร หรือรีเซ็ตรหัสผ่านแล้ว
            ทุกเครื่องของคนนั้นหลุดภายใน 5 นาที · ปิดบัญชีของตัวเองไม่ได้ · ทุกการแก้ในหน้านี้เก็บลงประวัติของ Admin
          </p>
        </section>
        <aside className="accounts-panel" aria-label="แผงบัญชี">
          {!target && <CreateAccountForm accounts={list} areas={areas.data ?? []} buildings={buildings.data ?? []} />}
          {target && panel.kind === 'edit' && (
            <EditAccountForm
              key={target.id}
              account={target}
              accounts={list}
              areas={areas.data ?? []}
              buildings={buildings.data ?? []}
              onDone={done}
              onCancel={backToCreate}
            />
          )}
          {target && panel.kind === 'phone' && (
            <PhoneForm key={target.id} account={target} onDone={done} onCancel={backToCreate} />
          )}
          {target && panel.kind === 'password' && (
            <PasswordForm key={target.id} account={target} isSelf={target.id === me} onDone={done} onCancel={backToCreate} />
          )}
          {target && panel.kind === 'deactivate' && (
            <DeactivateConfirm key={target.id} account={target} onDone={done} onCancel={backToCreate} />
          )}
        </aside>
      </div>
    </main>
  );
}
```

Append to `frontend/src/pages/accounts/accounts.css`:

```css
.accounts-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  white-space: nowrap;
}

.accounts-buttons {
  display: flex;
  gap: 8px;
}
```

- [ ] **Step 6: Run the tests, the typecheck and the lint**

Run: `cd frontend && npm test && npx tsc -b && npm run lint`
Expected: all pass. Frontend tests: 228 (218 + 1 rules + 9 actions).

- [ ] **Step 7: Handoff entry**

```markdown
- <date> · Task 9 done: account actions on D6: edit (name; a Cleaner's Area and shift, the current Area sent back to keep it, none for a deactivated Cleaner; a Supervisor's building and shift; an Admin's name only), แก้เบอร์โทร for Cleaners and Supervisors, รีเซ็ตรหัสผ่าน for Admins (own reset says this browser drops too), deactivate asks first and names the Area slot that goes, no self-deactivate button, activate in one press with the server's 409 shown; secrets are cleared once sent. Frontend tests <n> passed. Next: Task 10 (docs, final checks, real run).
```

- [ ] **Step 8: Commit**

```bash
git add docs/handoff.md frontend/src
git commit -m "feat(frontend): account actions on D6: edit, phone, password, deactivate, activate"
```

---

### Task 10: Docs, ADR amendments, final checks and the real run

**Files:**
- Modify (amendment appended at the end of each): `docs/adr/facility-0020-accounts-page-operations.md`, `facility-0021-points-admin-page.md`, `facility-0025-frontend-target-tree.md`, `facility-0026-frontend-router-shape.md`, `facility-0038-default-radius-50m-per-sign.md`, `facility-0045-admin-manages-areas-and-captures-sign-location.md`, `facility-0060-admin-gets-qr-token-from-points-page-only.md`, `facility-0062-phone-bundle-budget-200kb-gzip.md`
- Modify: `README.md`, `frontend/README.md`, `docs/architecture.md`, `docs/handoff.md`

**Interfaces:** none. Docs only, plus a full check.

- [ ] **Step 1: Measure the bundle**

```bash
cd frontend && npm run build && cd ..
```

Note the gzip size of `dist/assets/index-*.js` from Vite's output. It must be ≤ 200 kB. It goes into the 0062 amendment. If it is over, stop and report to the controller. Then delete `frontend/dist`.

- [ ] **Step 2: ADR amendments**

Append each block to the end of its ADR, after a blank line. The ADRs are in Thai, so the amendments are too. Use the editor tools. Write the date you commit in place of `<วันที่>` (format `2026-10-19`).

`facility-0020-accounts-page-operations.md`:

```markdown
> **แก้ไข <วันที่> (แผน 10):** หน้า `/admin/accounts` ตาม Wireframe D6: กรอง ทั้งหมด / แม่บ้าน / หัวหน้า / Admin / ปิดใช้งาน พร้อมจำนวน และค้นหาชื่อหรือรหัสบนหน้าเว็บ (ไม่แบ่งหน้า ตาม facility-0056) ฟอร์มเพิ่มบัญชีแสดงและส่งเฉพาะฟิลด์ของบทบาทที่เลือก (เซิร์ฟเวอร์ตอบ 400 ถ้ามีฟิลด์ของบทบาทอื่น) กะที่มีคนประจำแล้วหรือกะดึกของ Area เฉพาะกะเช้าเลือกไม่ได้และบอกว่าใครถืออยู่ แก้ไขส่ง Area เดิมของแม่บ้านกลับไปด้วยเพราะ PUT แทนที่ทั้งหมด "แก้เบอร์โทร" สำหรับแม่บ้านและหัวหน้า "รีเซ็ตรหัสผ่าน" สำหรับ Admin เบอร์และรหัสผ่านอยู่ในฟอร์มจนส่งแล้วล้างทิ้ง ปิดใช้งานมีขั้นยืนยันหนึ่งครั้งเพราะแม่บ้านเสีย Area และเปิดกลับแล้วไม่ได้ Area คืน เปิดใช้งานกดครั้งเดียว ไม่มีปุ่มปิดบัญชีตัวเอง ปุ่ม "นำเข้าจาก Excel" ยังไม่มีจนกว่างานนำเข้าฝั่งเซิร์ฟเวอร์ (facility-0056) จะเสร็จ
```

`facility-0021-points-admin-page.md`:

```markdown
> **แก้ไข <วันที่> (แผน 10):** หน้าจัดการเป็นระดับ Area ตาม facility-0045 (Wireframe D4 `/admin/areas`) จึงไม่มีตารางจุดแบบแบ่งหน้าและ toast เลิกทำของข้อ 1, 3, 4 ส่วนกติกาที่ยังใช้: ปิดใช้งานแทนลบ (จุดและ Area กดครั้งเดียว เปิดกลับได้) ออก QR ใหม่ถามครั้งเดียวในหน้า (`role="alertdialog"` ไม่ใช้ Base UI) แล้วเสนอ "พิมพ์ป้ายใหม่ ›" จุดหรือ Area ที่ปิดใช้งานออก QR ใหม่และพิมพ์ไม่ได้ หน้าพิมพ์ `/admin/print` (D5) พิมพ์ได้ทีละ Area ป้ายเดียว หรือทุก Area ที่เปิดใช้งาน แบบ "เล็ก (6 ต่อแผ่น)" หรือ "A5 (2 ต่อแผ่น)" บนกระดาษ A4 ตัดตามเส้นประ (D5 เขียนว่า A6 แต่ 6 ป้ายบน A4 ได้ประมาณ 97 × 94 มม. ไม่ใช่ A6) ป้ายมีชนิด ชื่อ Area และตึก "สแกนด้วยกล้องมือถือ" รอบหรือการลงเวลา รหัสป้าย วันที่ออก QR และ URL เต็ม QR วาดในเครื่องด้วย `uqr` 0.1.3 เป็น SVG ไม่ใช้บริการภายนอก ที่อยู่บนป้ายคือ origin ของหน้าที่สั่งพิมพ์ (facility-0067) และหน้าเตือนเมื่อไม่ใช่ https
```

`facility-0025-frontend-target-tree.md`:

```markdown
> **แก้ไข <วันที่> (แผน 10):** เพิ่มหน้า Admin `pages/areas/` (D4), `pages/area-form/` (D10), `pages/capture/` (M17), `pages/print-signs/` (D5) และ `pages/accounts/` (D6) ของที่ใช้ร่วมกันเพิ่มใน `shared/`: `shared/api/adminSetupApi.ts` (ชนิดข้อมูลและการอ่าน Area ป้าย ตึก แม่บ้าน และการบันทึกพิกัดป้าย ที่หลายหน้าใช้) และ `shared/components/AdminNav.tsx` (เมนู Admin ที่ `AppLayout wide` แสดง) ทุก query ของหน้า Admin ให้ tag `AdminSetup` และทุกการบันทึก invalidate tag นี้ ฝั่ง test เพิ่ม `src/test/adminFixtures.ts` และ `fakeGpsTrack` ใน `src/test/fakeGps.ts` (watchPosition ใช้คู่กับ fake timers)
```

`facility-0026-frontend-router-shape.md`:

```markdown
> **แก้ไข <วันที่> (แผน 10):** route ของหน้า Admin อยู่ใต้ `ProtectedRoute roles={['admin']}`: `/admin/areas/:areaId?` (D4 ไม่มี id เปิด Area แรก), `/admin/areas/new` และ `/admin/areas/:areaId/edit` (D10), `/admin/print?area=<id>[&sign=<id>]` หรือ `?area=all` (D5), `/admin/accounts` (D6) ใน `AppLayout wide` ที่มีเมนู Admin และ `/admin/areas/:areaId/capture[?sign=<id>]` (M17) ใน `AppLayout` คอลัมน์มือถือ เพราะ Admin ถือมือถือยืนที่ป้าย Dashboard ยังอยู่ที่ `/dashboard`
```

`facility-0038-default-radius-50m-per-sign.md`:

```markdown
> **แก้ไข <วันที่> (แผน 10):** Admin แก้รัศมีได้ทีละป้ายที่หน้า `/admin/areas` (D4) ทั้งป้ายลงเวลาและป้ายจุด ค่าต้องเป็นจำนวนเต็ม 10–500 เมตรตามที่เซิร์ฟเวอร์รับ ใช้กับการสแกนครั้งต่อไป ผลที่บันทึกไว้แล้วไม่เปลี่ยน
```

`facility-0045-admin-manages-areas-and-captures-sign-location.md`:

```markdown
> **แก้ไข <วันที่> (แผน 10):** หน้าเก็บพิกัด `/admin/areas/:areaId/capture` (M17) เลือกป้ายที่ยืนอยู่ กด "ใช้ตำแหน่งตรงนี้" แล้ว `watchPosition` (ความแม่นยำสูง ไม่ใช้ค่าเก่า) 30 วินาที แสดงวินาที จำนวนครั้ง และความแม่นยำล่าสุด ผลคือค่ากลางของละติจูด ลองจิจูด และความแม่นยำแยกกัน (ความแม่นยำปัดขึ้นเป็นเมตร) ต้องอ่านได้อย่างน้อย 3 ครั้ง ความแม่นยำแย่กว่ารัศมีขึ้นคำเตือนแต่ยังบันทึกได้ ปุ่มบันทึกกดได้เมื่อครบ 30 วินาที ออกจากหน้าหรือเลือกป้ายอื่นแล้ว GPS หยุด หน้านี้ต้องเปิดผ่าน https ทางสำรองข้อ 3 ทำเป็นช่องใส่พิกัดที่คัดลอกจากแอปแผนที่ (เช่น `13.756300, 100.501800`) แทนแผนที่ในหน้า ไม่ต้องเพิ่ม library แผนที่และไม่ส่งตำแหน่งของโรงงานไปขอภาพแผนที่จากเว็บภายนอก บันทึกเป็น Map ("ยังไม่ยืนยันหน้างาน") และช่องนี้หายเมื่อป้ายยืนยันหน้างานแล้ว
```

`facility-0060-admin-gets-qr-token-from-points-page-only.md`:

```markdown
> **แก้ไข <วันที่> (แผน 10):** QR Token มาถึงเบราว์เซอร์ทาง `GET /api/admin/areas/{id}` และคำตอบของการแก้ป้ายเท่านั้น ใช้ในหน้าจัดการ Area (D4) หน้าเก็บพิกัด (M17) และหน้าพิมพ์ป้าย (D5) แสดงเฉพาะใน URL ที่พิมพ์บนป้าย ไม่แสดงเป็นข้อความแยก และไม่ลง console
```

`facility-0062-phone-bundle-budget-200kb-gzip.md` (put in the size from Step 1):

```markdown
> **วัด <วันที่> (แผน 10):** JavaScript ไฟล์เดียว <ขนาดจาก Step 1> kB gzip หลังเพิ่มหน้า Admin ห้าหน้า (จัดการ Area แก้ Area เก็บพิกัด พิมพ์ป้าย บัญชีผู้ใช้) และ `uqr` 0.1.3 สำหรับวาด QR ยังอยู่ในงบ 200 kB จึงยังไม่แยกหน้า Admin เป็น lazy route
```

- [ ] **Step 3: `README.md`**

In the section `## 📱 การเข้าใช้งานระบบบนอุปกรณ์จริง`, replace the line:

```markdown
  - Admin เข้าที่ `/login/admin` ด้วยชื่อผู้ใช้ + รหัสผ่าน แล้วไปหน้า Dashboard (`/dashboard`)
```

with:

```markdown
  - Admin เข้าที่ `/login/admin` ด้วยชื่อผู้ใช้ + รหัสผ่าน แล้วไปหน้า Dashboard (`/dashboard`) เมนู Admin มี "จัดการ Area และป้าย" (`/admin/areas`: Area จุด เวลารอบ รัศมี พิกัดป้าย ออก QR ใหม่ และพิมพ์ป้ายที่ `/admin/print`) และ "บัญชีผู้ใช้" (`/admin/accounts`)
  - เก็บพิกัดป้ายบนมือถือที่ `/admin/areas/<id>/capture` ยืนที่ป้ายแล้วกด "ใช้ตำแหน่งตรงนี้" (30 วินาที ต้องเป็น https หรือ `localhost`) พิมพ์ป้ายจากที่อยู่ https ของเซิร์ฟเวอร์ เพราะที่อยู่บนป้ายคือที่อยู่ของหน้าที่สั่งพิมพ์
```

- [ ] **Step 4: `frontend/README.md`**

1. In the code block under `## Where things go (facility-0025)`:
   - Replace the line that starts `src/shared/api/` with:

     ```
     src/shared/api/                          baseApi (RTK Query), server types, myWorkApi, scanHub, adminSetupApi (Admin reads)
     ```

   - Replace the line that starts `src/shared/components/` with:

     ```
     src/shared/components/                   guard, layout, Admin menu, status and crash screens (class prefix app-)
     ```

2. Below the bullet that starts `- A page adds its endpoints with`, add:

```markdown
- Admin setup and accounts data provide the cache tag `AdminSetup`, and every Admin save invalidates it: Areas, signs, Cleaners and accounts depend on each other.
- A new Admin page goes under the Admin `ProtectedRoute` in `router.tsx`, and into `shared/components/AdminNav.tsx` if it belongs in the menu.
```

3. Under `## Tests`, append:

```markdown
Made-up Admin answers (Areas, signs, points, buildings, Cleaners, accounts) are in `src/test/adminFixtures.ts`; the logged-in Admin is account 7. The capture page reads the GPS for 30 s with `watchPosition`: `fakeGpsTrack([...])` sends one reading a second, and those tests use `vi.useFakeTimers({ shouldAdvanceTime: true })`, `fireEvent` for clicks, and restore real timers in `afterEach`. The print page's "พิมพ์" is checked with `vi.spyOn(window, 'print')`.
```

- [ ] **Step 5: `docs/architecture.md`**

In section 2:

1. In the `Pages[...]` node, replace `my-building (M15) · dashboard` with `my-building (M15) · dashboard · areas (D4) · area-form (D10) · capture (M17) · print-signs (D5) · accounts (D6)`.
2. In the `Guard[...]` node, replace `ProtectedRoute (role), AppLayout,` with `ProtectedRoute (role), AppLayout + AdminNav,`.

- [ ] **Step 6: `docs/handoff.md`**

1. In the Status table, replace the plan 10 row with:

```markdown
| 10 | Admin setup: Areas, points, round windows, signs, sign location capture, print signs, accounts | done (branch `feat/admin-setup`): `docs/superpowers/plans/2026-10-19-admin-setup.md` |
```

2. Under `## Pending work`, replace the line that starts `- Frontend plans 10–11` with:

```markdown
- Frontend plan 11 (scope in the plan-7 file): Admin live board.
```

3. In `## Notes for the frontend plan`, replace the line that starts `- QR Sign URLs (plan 8):` with:

```markdown
- QR Sign URLs (plan 8): a check-in sign prints `<origin>/check-in/<token>`, a point sign `<origin>/scan/<token>`. Plan 10's print page (`/admin/print`) prints them with the printing page's origin, so print from the server's https address.
```

   and add at the end of that section:

```markdown
- Admin pages (plan 10): add a plan-11 page under the Admin guard in `router.tsx` and its link to `shared/components/AdminNav.tsx` (the menu lists only pages that exist; รายการที่ขึ้นเตือน gets its count there). Admin data uses the `AdminSetup` tag; plan 11's live data keeps its own tags. `src/test/adminFixtures.ts` has made-up Admin answers.
- The print page's `@page` rule (A4, no margin) is in the one CSS bundle, so it applies to anything the browser prints.
- M17 on a real phone over https is still to be checked: a desktop browser with a fixed simulated location may report one reading only (the real run nudges it), while phones report about once a second.
```

- [ ] **Step 7: Final full check**

```bash
cd frontend && npm test && npx tsc -b && npm run lint && npm run build && cd ..
dotnet test backend/FacilityRealtime.slnx
grep -rn "style={" frontend/src || echo "no inline styles"
grep -rn "localStorage\|sessionStorage\|indexedDB" frontend/src || echo "no web storage"
grep -rn "toLocaleString\|toLocaleTimeString\|toLocaleDateString" frontend/src || echo "no ad-hoc time formatting"
grep -rn "dangerouslySetInnerHTML" frontend/src || echo "no raw HTML"
grep -rn "vi.mock" frontend/src || echo "no module mocks"
grep -rln "from 'uqr'" frontend/src                      # expect: only src/pages/print-signs/qr.ts
grep -rln "qrToken" frontend/src | grep -v "\.test\." | grep -v "src/test/"   # expect: shared/api/adminSetupApi.ts, pages/print-signs/printRules.ts
grep -rn "console\." frontend/src/pages | grep -v "\.test\." || echo "no console output in pages"
git diff --stat 84890df -- backend || true               # expect: nothing
rm -rf frontend/dist
```

Expected:
- Frontend: 228 tests pass, the typecheck and lint are clean, and the JS bundle is ≤ 200 kB gzip.
- Backend: 299 unit tests pass; API tests 531 pass and 5 are skipped (nothing in `backend/` changed).
- Each grep prints its "no ..." line or the expected files, and the `backend` diff is empty.

- [ ] **Step 8: Handoff entry and commit**

```markdown
- <date> · Task 10 done: ADR amendments (0020 accounts page, 0021 new QR and print, 0025 tree, 0026 Admin routes, 0038 radius, 0045 capture and map fallback, 0060 where the token goes, 0062 measured size <size> kB gzip), README device notes, frontend README, architecture diagram, handoff notes for plan 11. Frontend <n> tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
```

```bash
git add docs README.md frontend/README.md
git commit -m "docs: Admin setup in README, architecture and handoff; ADR amendments for Areas, signs, capture, print and accounts"
```

- [ ] **Step 9 (controller only, not a subagent): real run on a throwaway database**

Ask the owner for the MySQL root password. Use it only in the commands below, and never write it to a file.

```bash
mysql -u root -p -e "CREATE DATABASE facility_admin_test CHARACTER SET utf8mb4;"
npm --prefix frontend run build:api
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=facility_admin_test;Uid=root;Pwd=<typed by the owner>;CharSet=utf8mb4;" \
dotnet run --project backend/src/FacilityRealtime.Api --urls "http://0.0.0.0:5001"
```

In a second terminal:

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5001/admin/areas         # 200 (the app)
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5001/admin/print?area=1" # 200 (the app)
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5001/api/admin/areas     # 401 (no token)
```

In Chrome at `http://localhost:5001`, use a normal window for the Admin and an Incognito window for the Cleaner (separate cookies). Then check, in order:

1. Normal: log in at `/login/admin` as `admin` / `admin1234`. The Dashboard shows the menu สถานะจุด · จัดการ Area และป้าย · บัญชีผู้ใช้.
2. จัดการ Area และป้าย: AR01 is open. AR01-IN, AR01-01 and AR01-02 show "หน้างาน ±10 ม. · รัศมี 50 ม.". In the list, AR02 shows "ตึก A · เฉพาะกะเช้า · 1 จุด · ป้ายยังไม่ยืนยัน 2".
3. "+ เพิ่ม Area": code `AR03`, name `Area ทดลองใหม่`, ตึก A, เฉพาะกะเช้า, no Cleaner, then บันทึก. The page lands on AR03, with AR03-IN "ยังไม่มีพิกัด".
4. "+ เพิ่มจุดทำความสะอาดใน Area นี้": name `โถงทางเดิน`, กะเช้า 08:00–10:00, then บันทึกจุด. Row AR03-01 shows "เช้า 08:00–10:00".
5. In AR03-01's sign settings, type `13.7563, 100.5018` in the map field and save: "ยังไม่ยืนยันหน้างาน (จากแผนที่)". In AR03-IN's settings, set the radius to 80.
6. DevTools › More tools › Sensors › Location: latitude `13.7563`, longitude `100.5018`. Open "เก็บพิกัดที่ป้ายด้วยมือถือ (M17) ›" for AR03-01 and press "ใช้ตำแหน่งตรงนี้". During the 30 s, change the latitude's last digit three times (13.7564, 13.7565, 13.7563): a desktop browser may report a fixed location only once. After 30 s, press "บันทึกพิกัด": "บันทึกพิกัดป้าย AR03-01 แล้ว หน้างาน ±… ม.". Back on `/admin/areas/3`, AR03-01's map field is gone.
7. "พิมพ์ป้ายของ Area นี้ ›" on AR03: 2 signs on 1 sheet. Ctrl+P shows one A4 page with no bar, menu or controls. Scan AR03-01's on-screen QR with a phone camera: it reads `http://localhost:5001/scan/<token>`, the same as the small URL under it.
8. On AR01-01, "ออก QR ใหม่" → "ยืนยัน ออก QR ใหม่" → "ออก QR ใหม่แล้ว…" with "พิมพ์ป้ายใหม่ ›". Incognito: log in at `/login` as `E1001` / `0810000001`, then open `/scan/token-restroom-m1`. The old sign no longer opens the point (the server's not-found answer).
9. บัญชีผู้ใช้: the counts match the seed. Add a Cleaner `แม่บ้านทดลอง`, `650099`, phone `0810000099`, Area AR03, กะเช้า. Its row shows "AR03 · กะเช้า". In a new Incognito window, log in at `/login` as `650099` / `0810000099`. My Work shows AR03.
10. "แก้เบอร์โทร แม่บ้านทดลอง": `0810000098`. In a new Incognito window, logging in with `0810000099` fails ("รหัสพนักงานหรือเบอร์โทรไม่ถูกต้อง") and `0810000098` works.
11. "ปิดใช้งาน แม่บ้านทดลอง": the question says "AR03 กะเช้า จะว่าง…". Confirm. AR03's "แก้ไข Area" no longer lists them as a day Cleaner.
12. The row of `ผู้ดูแลระบบ` has no "ปิดใช้งาน" button.

Look in the API log for warnings: there must be none.

When done, stop the API, delete the build output and drop the database:

```bash
rm -r backend/src/FacilityRealtime.Api/wwwroot
mysql -u root -p -e "DROP DATABASE facility_admin_test;"
```

Record the result in the handoff (Task log), commit it, then run the whole-branch review.

---

## Self-review (done while writing)

- **Spec coverage:**
  - facility-0045 item 1 is covered by Areas (Task 5), points and round times (Task 3), the check-in sign shown and never created by hand (Tasks 2 and 5), and location and radius (Tasks 4 and 7).
  - facility-0045 item 2 (30 s, median, accuracy, warning) is Task 7. Item 3 (map fallback, "ยังไม่ยืนยันหน้างาน") is Task 4. The https requirement is Task 7, plus the print warning in Task 6.
  - facility-0021: deactivate instead of delete (Tasks 3 and 5), one QR confirmation then print (Task 4), print one or all on A4 with 6 per sheet (Task 6). Items 1, 3 and 4's paged table and undo toast are replaced by the Area level, as recorded in the 0021 amendment.
  - facility-0020, 0054 and 0056: add, edit, deactivate, activate, phone and password, no delete, no self-deactivation, filtered on the page (Tasks 8 and 9). Excel import is out of scope (D11).
  - facility-0038: the radius per sign (Task 4). facility-0060: the token only on Admin setup pages (Global Constraints, Task 6 test, Task 10 grep). facility-0062: measured in Tasks 6 and 10.
  - Wireframes: D4 (Tasks 2–4), D10 (Task 5), D5 (Task 6), M17 (Task 7), D6 (Tasks 8–9).
- **Names checked across tasks:**
  - `useGetAreaQuery`, `useSetSignLocationMutation`, `signPlaceLabel`, `roundsText`, `formatCoordinates`, `formatDate`.
  - `EditTarget` (Task 3, widened in Task 4), `SavePointBody`, `WindowDraft`.
  - `AreaFormBody`, `printableSigns`, `startCapture`, `accuracyVerdict`.
  - `AccountAction`, `CreateDraft`, `EditDraft`, `createRequest`, `updateRequest`.
- **Test counts:** 157 → 163 → 170 → 177 → 183 → 188 → 195 → 209 → 218 → 228.

