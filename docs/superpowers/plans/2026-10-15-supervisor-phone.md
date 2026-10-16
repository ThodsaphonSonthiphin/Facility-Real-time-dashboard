# Supervisor Phone Implementation Plan (plan 9)

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Supervisor can work the whole shift on the phone. My Building shows what waits for them and updates live. Scanning a point's sign opens the inspection: the latest submission, สะอาด or ไม่สะอาด with a defect, and the saved result. The page also explains "nothing to inspect", a blocked building or shift, and a send that did not go through.

**Architecture:**
- **One sign URL, a page per role.** `/scan/:token` stays the URL printed on every point's sign (plan 8 decision 1).
  - A new `shared/components/ByRole` shows the Cleaner's `ScanPage` or the Supervisor's new `InspectPage` (facility-0029 item 1).
  - A page may not import another page, so the choice is made in the route table.
- **Live updates, first use of the hub (#26 hub-cache-bridge, facility-0064):**
  - `shared/api/scanHub.ts` opens one SignalR connection per RTK Query cache entry, in `onCacheEntryAdded`.
  - It hands each pushed card to the page, and after a reconnect or a restart it says that pushes may have been missed.
  - Its token comes from `getValidAccessToken`, which refreshes first when the token has 30 s or less left (facility-0023 item 3).
  - Whether the hub is live is not server data, so it lives in `store/slices/hubSlice.ts`.
- **My Building (`pages/my-building/`, M15).**
  - `GET /api/my-building`, patched in place by `ScanRecorded` and `InspectionRecorded`, and sorted on the phone with the server's rule.
  - A reconnect asks the server again; so do "อัปเดต" and opening the page.
- **Inspection (`pages/inspect/`, M6, M7, M16):**
  - Opening the page reads a fresh GPS fix and sends `POST /api/inspections/scan`. This is an RTK Query query, so React's StrictMode and a quick remount share one request.
  - "บันทึกผลตรวจ" reads another fresh fix and sends `POST /api/inspections`.
  - The server decides everything: what may be inspected, the flag, the block. The phone shows the answer.
- **Fix first.** The API leaves out every null field. Plan 8's three `attendance.shift === null` checks never fire outside the shift, so Task 1 fixes them before the new pages rely on the same rule.

**Tech Stack:** no new package. React 19.2, TypeScript 6.0, Vite 8.3, `@reduxjs/toolkit` 2.13.0, `react-router` 7.18.4, `@microsoft/signalr` 10.0.11 (installed since plan 7, imported for the first time here), Vitest 5.0 on jsdom 30.1.2, Testing Library, eslint 9 with boundaries.

**Spec:** this plan implements the Supervisor side of these ADRs, with wireframes M6, M7, M15 and M16 for the screens. Where a wireframe and an ADR disagree, the ADR wins.
- `docs/adr/facility-0029-supervisor-inspection-workflow.md`: the same QR opens the Supervisor's inspection form (สะอาด / ไม่สะอาด with a defect); ไม่สะอาด makes the card ต้องแก้ไข until the Cleaner sends again.
- `docs/adr/facility-0047-cleaning-rounds-are-time-windows.md` rule 7: only work sent in the current round can be inspected; with none, the page says so and has no inspect button.
- `docs/adr/facility-0048-supervisor-assigned-to-building.md`: a Supervisor inspects only their own building in their own shift by the clock. Anything else is a Blocked Scan unless an Admin gave a cover. They have no attendance.
- `docs/adr/facility-0057-supervisor-building-page-and-audit-log-page.md` item 1: My Building shows the own building only, รอตรวจ first by submit time, then the points waiting for rework. Inspecting needs the QR at the point. The page opens outside the shift but cannot inspect there, and it updates itself when a Cleaner submits.
- `docs/adr/facility-0023-auth-in-base-query-and-signalr.md` item 3: `getValidAccessToken` for SignalR's `accessTokenFactory`, 30 s margin.
- `docs/adr/facility-0064-dashboard-hub-offline-keeps-todays-behaviour.md`: automatic reconnect `[0, 2000, 5000, 10000, 30000]`, refetch once on reconnect, restart every 3 s after the connection closes, the connection state in a slice.
- `docs/adr/facility-0061-dashboard-keeps-30s-poll-beside-push.md`: the 30 s poll is for the Admin's screens only.
- GitHub #26 (hub-cache-bridge): open the hub in the query's `onCacheEntryAdded`, patch with `updateCachedData`, never one connection per component.
- GitHub #32 (regression-net): tests fake only `fetch` and the `@microsoft/signalr` package.
- `docs/adr/facility-0036`, `0037`, `0041`, `0052`, `0059`, `0062`, `0063`, `0065`, `0066`, `0068`, as in plan 8: Location help, GPS on every scan, blocked scans, resend by hand, QR Token only from the sign, bundle budget, CSS, lint boundaries, text rendering, failure minimum.
- Plan 8 (`docs/superpowers/plans/2026-10-15-cleaner-phone.md`) for the pieces this reuses, and `docs/handoff.md` "Notes for the frontend plan".
- Wireframes (`docs/design/wireframe/`): `SupervisorInspect.dc.html` (M6), `InspectDone.dc.html` (M7), `SupervisorHome.dc.html` (M15), `InspectNothing.dc.html` (M16), and for the failures `LocationOff.dc.html` (M2), `ScanBlocked.dc.html` (M8), `SubmitFailed.dc.html` (M12).

**The API this plan uses (unchanged; read the C# if in doubt):**

| Call | Answer | Source |
|---|---|---|
| `GET /api/my-building` | `MyBuildingDto { shiftDate, shift, buildings[] }`, each `{ buildingId, buildingCode, buildingName, isCover, canInspect, points[] }`; 403 for a non-Supervisor | `backend/src/FacilityRealtime.Api/Endpoints/MyBuildingEndpoints.cs`, `DTOs/MyBuildingDtos.cs` |
| `POST /api/inspections/scan` `{ qrToken, latitude, longitude, accuracyM }` | 200 `InspectionScanDto { point, submission?, refusal?, isCover, distanceM?, withinRadius? }`; 400 GPS; 403 `BlockedScanResponse` (`OtherBuilding`, `OutsideShift`, and **this call records the Blocked Scan**) or `{ message }` for another role; 404 `{ message }` | `Endpoints/InspectionEndpoints.cs`, `DTOs/InspectionDtos.cs` |
| `POST /api/inspections` `{ qrToken, scanRecordId, result, defect?, latitude, longitude, accuracyM }` | 201 `InspectionCreatedResponse`; 400 `{ message }`; 403 `BlockedScanResponse`; 404 `{ message }`; 409 `{ message }` (new work since the scan, nothing to inspect, or a rework still owed) | same |
| hub `/hubs/scan` (token in `?access_token=`) | `ScanRecorded` and `InspectionRecorded`, each a `PointStatusDto`, to the building's active Supervisors and to anyone covering it in the clock's shift | `Hubs/ScanHub.cs`, `Endpoints/PointNotifications.cs`, `Auth/AuthSetup.cs` |

JSON is camelCase in REST and on the hub. Enums are strings (`Passed`, `Rework`, `OtherBuilding`). Times are UTC ISO strings ending in `Z`. **Null fields are left out** (`JsonIgnoreCondition.WhenWritingNull` in `Program.cs`, for REST and the hub alike): an inspection scan with nothing to inspect has no `submission` key at all.

**Where this plan sits:** the frontend in five plans (table in plan 7). This is plan 9. Plan 10 (Admin setup) prints the signs. Plan 11 (Admin live board) reuses `followScanHub` and `hubSlice` for the Dashboard.

## Global Constraints

- **Branch:** `feat/supervisor-phone`, created from `master` at `55e3177` (PR #57 merged). Do not merge it.
- **Machine:** the Mac. zsh, Node v26.8.2, npm 11.19.1, .NET SDK 10.0.401.
  - Frontend commands run in `frontend/`. Backend commands run from the repo root.
  - Write files that hold Thai text with the editor tools, not shell heredocs.
- **No new package.** `package.json` and `package-lock.json` do not change. `@microsoft/signalr` 10.0.11 is already a dependency.
- **API unchanged.** No file under `backend/` changes. Where the wireframes show something the API does not send, leave it out (decision 7).
- **Bundle:** the JavaScript of `npm run build` stays ≤ 200 kB gzip in one file (facility-0062). Plan 8 measured 135.53 kB; the SignalR client adds roughly 15 kB.
- **Styles:**
  - No `style={...}` anywhere in `src/` (facility-0063).
  - Page prefixes: `building-` (My Building) and `inspect-` (inspection). Shared components use `app-`.
- **Text people type and server text** (defects, notes, names, refusals) are rendered as React text only. No `dangerouslySetInnerHTML`, and no `href` or `src` built from them (facility-0066).
- **QR Token (facility-0059):** the inspection page reads its token only from its URL (`useParams`). My Building has no link to `/scan/...`; the DTO has no token anyway.
- **One send at a time:** while "บันทึกผลตรวจ" or "ส่งใหม่" is being handled (GPS reading and request), the buttons and the verdict are disabled, so a second tap never writes a second Inspection Record.
- **Nothing is stored on the phone:** no `localStorage`, `sessionStorage`, IndexedDB or cache of unsent work (facility-0052, 0068).
- **Thai time:** every time on screen goes through `shared/utils/thaiTime.ts`. Never call `toLocaleString` or `toLocaleTimeString` elsewhere.
- **Server text is shown as is.** `getErrorMessage` shows the API's `{ message }`. Pages add their own text only where the context differs (facility-0022 item 4).
- **Missing fields:** a field the server may send as null arrives missing.
  - Types for new DTOs mark such fields optional (`submission?: InspectionSubmission`).
  - Code tests them with truthiness or `== null`, never `=== null`.
  - Test fixtures leave such fields out, the way the server does.
- **Tests:**
  - Vitest on jsdom. Only browser seams are faked:
    - `fetch`, with `src/test/fakeServer.ts`.
    - `navigator.geolocation`, with `src/test/fakeGps.ts`.
    - The `@microsoft/signalr` package, with `src/test/fakeSignalR.ts` through `test.alias` in `vite.config.ts` (Task 2).
  - No MSW, no `vi.mock`, and no module mocks of our own code (regression-net #32).
  - A hub push in a page test is wrapped in `act(...)`.
  - Find elements by role, label or text the way a person would, never by CSS class.
  - `router.test.tsx` opens `/` as a Supervisor and looks for the heading "ตึกของฉัน". `MyBuildingPage` must always render its `h1` "ตึกของฉัน", even while loading or after an error.
  - Neither Supervisor page prints the person's name: the bar already does, and a second copy breaks `getByText("หัวหน้าทดสอบ")`.
- **Copy:** Thai UI text comes from the wireframes. Where this plan gives a string, use it exactly, because tests match it.
- **Public repo:** made-up names only (`หัวหน้าทดสอบ`, `แม่บ้านทดสอบ`, `ตึกทดลอง`, `ตึกข้างเคียง`, `Area ตึกอื่น`). No secret in any file.
- **MySQL** is used only in Task 6's controller run.
  - Use only throwaway databases `facility_*_test`. Never touch `facility_dashboard`.
  - Ask the owner for the root password each time. Type it only into the command, never into a file or into memory.
  - Subagents skip that step.
- **Handoff after every task (owner's rule):**
  - The last step of every task adds one entry to a new section `## Task log (plan 9)` in `docs/handoff.md`, placed just above `## Pending work`. It is committed with the task.
  - The entry is in English: the date, the task, what changed, the test result, and what comes next. No secret, phone number or real name.
- **Commits** end with the attribution trailer your session gives you. The husky pre-commit runs frontend lint, tsc and tests on every commit. If it fails, fix the cause. Never use `--no-verify`.
- **Baselines at `55e3177`:** frontend 105 tests pass; backend 299 unit tests pass; API tests 531 pass and 5 are skipped (the MySQL ones).

### Decisions made while planning (report them to the owner in the PR)

1. **A plan-8 fix comes first.** Outside the attendance window the server leaves `shiftDate` and `shift` out of the JSON, so three `attendance.shift === null` checks never fire:
   - My Work printed "undefined · ยังไม่ได้ลงเวลาเข้างาน".
   - The scan page said "ยังไม่ได้ลงเวลาเข้างาน" instead of "ตอนนี้ไม่อยู่ในกะของคุณ".
   - The check-in page left out its outside-the-window line.
   - The tests passed because the fixture sent `null`. Task 1 makes the fixture match the server and the checks use truthiness.
2. **`/scan/:token` opens a page by role** (facility-0029 item 1): Cleaner → `ScanPage`, Supervisor → `InspectPage`, Admin → "ไม่มีสิทธิ์เปิดหน้านี้". Plan 8 pinned the Supervisor's no-permission screen on purpose; Task 4 replaces that test with the Admin's.
3. **The inspection scan runs when the page opens.**
   - The page reads a fresh, high-accuracy fix and sends `POST /api/inspections/scan`, as an RTK Query query whose `queryFn` reads the location first. StrictMode and a quick remount share one request, and every visit asks again (`refetchOnMountOrArgChange`).
   - Unlike the Cleaner's page (blocked on press, plan 8 decision 3), another building or the wrong shift is blocked as soon as the sign opens, because this endpoint is the one that records a Supervisor's Blocked Scan.
   - After the Admin assigns a cover, scanning the sign again opens the inspection.
4. **No verdict is chosen at first.**
   - "ไม่สะอาด" opens "ข้อบกพร่อง". It is required, at most 1000 characters (the server's limit), trimmed, and sent only with ไม่สะอาด, because the server refuses a defect with สะอาด.
   - "บันทึกผลตรวจ" says what is missing ("เลือกผลการตรวจก่อน", "ใส่ข้อบกพร่องก่อนบันทึก") and sends nothing.
   - Why not preselect: a preselected สะอาด would let a rushed tap pass dirty work. The Cleaner's preselected เรียบร้อย (facility-0007) is about their own work.
5. **A 409 on "บันทึกผลตรวจ" opens the newest state.**
   - It means new work was sent after the scan, or the point no longer has work to inspect.
   - The page scans again, clears the verdict and says "งานของจุดนี้เปลี่ยนไประหว่างที่คุณตรวจ ผลยังไม่ได้บันทึก หน้านี้เปิดงานล่าสุดให้แล้ว".
   - The Supervisor never judges work they were not shown (plan 6 decision 2).
6. **No answer (facility-0052's rule, for the Supervisor too).** This covers no signal, no answer in 20 s, an answer that is not JSON, and a 5xx.
   - On "บันทึกผลตรวจ": "ยังไม่ได้บันทึกผลตรวจ", with the verdict kept and "ส่งใหม่" reading a fresh fix.
   - When the page opens: "เปิดงานตรวจไม่ได้" with "ลองอีกครั้ง".
   - Accepted risk: if a request reached the server but its answer was lost, "ส่งใหม่" writes a second สะอาด Inspection Record (the Inspection Summary counts two). A second ไม่สะอาด gets 409 and the page opens "รอแม่บ้านส่งงานแก้".
7. **Not in any DTO, so not shown:**
   - M6's "หัวหน้า: วิไล · ประจำตึก A กะเช้า": the name is in the bar, and the account's building and shift are not in the login answer.
   - M16's table of the day's rounds: the point carries only the current and next round.
   - M7's "ดูหน้าที่แม่บ้านเห็น" link: that is the Cleaner's page.
   - Who asked for a rework: M15's "คุณให้แก้ 09:40" becomes "ให้แก้ 09:40".
8. **My Building is live, with no poll.**
   - The hub opens in `getMyBuilding`'s `onCacheEntryAdded` (#26) and listens to `ScanRecorded` and `InspectionRecorded`.
   - Each pushed card replaces the same point in the cache. The page sorts with the server's rule (`MyBuildingEndpoints.Sorted`): รอตรวจ by submit time, ต้องแก้ไข by when it was asked, then the rest by Area code in the order they came. So a new รอตรวจ moves up at once.
   - An automatic reconnect, or a restart after the connection closed, asks the server once (facility-0064). "อัปเดต" and every visit ask too.
   - No 30 s poll: facility-0061 keeps polling on the Admin's screens. Changes that come with time (a round starting, a point going เลยรอบ) show on the next visit, "อัปเดต", or push.
   - A pushed card for a point that is not on the page is ignored. A cover given after the page opened shows on the next visit, because the hub sends no cover event.
9. **The live line.** The footer says whether pushes arrive ("หน้านี้อัปเดตเองเมื่อแม่บ้านส่งงานหรือหัวหน้าตรวจ"), or that the connection dropped and "อัปเดต" shows the newest. The state is `store/slices/hubSlice.ts`, a shared slice rather than facility-0064's `dashboardSlice`, so plan 11's Dashboard badge reads the same one.
10. **`getValidAccessToken(getState, dispatch)` lives in `shared/auth/session.ts`.**
    - It uses the single-flight refresh, so a hub connect and a REST 401 never refresh twice (facility-0015).
    - With more than 30 s left it gives the token as it is. A refused refresh gives `''`: the session has ended and its cache entry is gone, so the hub stops.
11. **Tests fake the SignalR package** with `src/test/fakeSignalR.ts`, aliased in `vite.config.ts` for every test, the way `fakeServer` fakes `fetch`. Only `followScanHub` imports the package.

---

## File structure after this plan

```
frontend/
├── vite.config.ts                    + test.alias: @microsoft/signalr → src/test/fakeSignalR.ts
└── src/
    ├── router.tsx                    /scan/:token under a cleaner+supervisor guard, by role
    ├── store/
    │   ├── index.ts                  + hub reducer
    │   └── slices/hubSlice.ts        hub status: connecting, live, reconnecting
    ├── shared/
    │   ├── api/types.ts              AttendanceState fields the server may leave out are optional
    │   ├── api/baseApi.ts            + tagTypes ['MyBuilding']
    │   ├── api/scanHub.ts (+ test)   followScanHub: one connection per cache entry, reconnect, restart
    │   ├── auth/session.ts (+ test)  + getValidAccessToken
    │   └── components/               + ByRole; BlockedScanScreen gains sign="inspect"
    ├── pages/
    │   ├── my-building/              MyBuildingPage (M15), BuildingCard, myBuildingApi, buildingRules (+ test),
    │   │                             myBuilding.css, test
    │   └── inspect/                  InspectPage, InspectHeading, DistanceLine, InspectForm (M6), InspectResult (M7),
    │                                 NothingToInspect (M16), InspectSendFailed, inspectApi, inspectRules,
    │                                 inspect.css, index.ts, test
    └── test/
        ├── setup.ts                  + resetFakeHub after each test
        ├── fakeSignalR.ts            fake @microsoft/signalr: HubConnectionBuilder and the `hub` controls
        ├── cleanerFixtures.ts        outsideShift leaves out the null fields
        └── supervisorFixtures.ts     made-up My Building, inspection and Blocked Scan answers
```

---

### Task 1: Fields the server leaves out (plan-8 fix)

The server leaves out every null field. Outside the attendance window the attendance has no `shift` key, so the checks must test for a missing value.

**Files:**
- Modify: `frontend/src/test/cleanerFixtures.ts`, `frontend/src/shared/api/types.ts`, `frontend/src/pages/check-in/CheckInPage.tsx`, `frontend/src/pages/my-work/MyWorkPage.tsx`, `frontend/src/pages/scan/scanRules.ts`
- Test: the existing `CheckInPage.test.tsx`, `MyWorkPage.test.tsx` and `ScanPage.test.tsx` (their "outside the window" tests use `outsideShift`)
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: nothing new.
- Produces: `AttendanceState` with `shiftDate?: string | null`, `shift?: Shift | null` and `outcome?: 'Recorded' | 'AlreadyRecorded' | 'TooSoon' | null`. Code reads them by truthiness.

- [ ] **Step 1: Create the branch**

```bash
git checkout master && git pull --ff-only && git checkout -b feat/supervisor-phone
git log --oneline -1   # expect 55e3177 (or a later docs-only commit)
```

- [ ] **Step 2: Make the fixture match the server**

In `frontend/src/test/cleanerFixtures.ts`, replace:

```ts
export const outsideShift: AttendanceState = { shiftDate: null, shift: null, events: [], nextEvents: [], outcome: null };
```

with:

```ts
/** Outside the attendance window, as the server sends it: it leaves out the null shiftDate, shift and outcome. */
export const outsideShift: AttendanceState = { events: [], nextEvents: [] };
```

- [ ] **Step 3: Run the tests to see the bug**

Run: `cd frontend && npm test`
Expected: FAIL, 3 tests:
- `CheckInPage` "outside the attendance window there is no button" cannot find the line "ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ …".
- `MyWorkPage` "outside the attendance window the shift line says so" cannot find "ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ"; the page printed "undefined · ยังไม่ได้ลงเวลาเข้างาน".
- `ScanPage` "outside the own shift the point cannot be submitted" finds "ยังไม่ได้ลงเวลาเข้างาน" instead of "ตอนนี้ไม่อยู่ในกะของคุณ".

(`npx tsc -b` also fails now, because `AttendanceState` still requires `shift`. Step 4 fixes that.)

- [ ] **Step 4: Types and the three checks**

In `frontend/src/shared/api/types.ts`, replace the first comment block:

```ts
/**
 * Shapes the server sends and accepts (facility-0022). Enums arrive as strings (JsonStringEnumConverter),
 * times as UTC ISO strings ending in "Z". Types used by one page only live in that page's folder.
 */
```

with:

```ts
/**
 * Shapes the server sends and accepts (facility-0022). Enums arrive as strings (JsonStringEnumConverter),
 * times as UTC ISO strings ending in "Z". Types used by one page only live in that page's folder.
 * The server leaves out every null field (JsonIgnoreCondition.WhenWritingNull, REST and hub alike): a field typed
 * `| null` or `?` arrives missing, so code tests it with truthiness or `== null`, never `=== null`.
 */
```

In the same file, replace the whole `AttendanceState` block (its comment and interface) with:

```ts
/**
 * AttendanceStateDto. Outside every attendance window of the Cleaner's own shift (facility-0069) shiftDate and shift
 * are left out and the lists are empty. outcome comes only on the answer of POST /api/attendance.
 */
export interface AttendanceState {
  shiftDate?: string | null;
  shift?: Shift | null;
  events: AttendanceEntry[];
  nextEvents: AttendanceEvent[];
  outcome?: 'Recorded' | 'AlreadyRecorded' | 'TooSoon' | null;
}
```

In `frontend/src/pages/check-in/CheckInPage.tsx`, replace:

```tsx
      {attendance && attendance.shift === null && (
```

with:

```tsx
      {attendance && !attendance.shift && (
```

In `frontend/src/pages/my-work/MyWorkPage.tsx`, inside `attendanceLine`, replace:

```ts
  if (attendance.shift === null) {
```

with:

```ts
  if (!attendance.shift) {
```

In `frontend/src/pages/scan/scanRules.ts`, inside `offDutyReason`, replace:

```ts
  if (attendance.shift === null) {
```

with:

```ts
  if (!attendance.shift) {
```

- [ ] **Step 5: Run the tests, the typecheck and the lint**

```bash
npm test          # expect: 105 passed
npx tsc -b        # expect: no output
npm run lint      # expect: no output
grep -rnE "(===|!==) null" src --include='*.ts' --include='*.tsx' | grep -v '\.test\.'
```

The grep must list only checks of our own values: `getErrorMessage.ts` (2), `scanAnswers.ts` (2), and `CheckInPage.tsx` `pressed !== null` (2, page state). Nothing compares a server field with `null` any more.

- [ ] **Step 6: Handoff entry**

In `docs/handoff.md`, add a new section `## Task log (plan 9)` just above `## Pending work`, with:

```markdown
- <date> · Task 1 done: the server leaves out null fields, so outside the shift the attendance has no `shift`; My Work, the check-in page and the scan page now test it by truthiness (plan-8 bug: My Work printed "undefined", the scan page said "not checked in"). The `outsideShift` fixture matches the server. Frontend tests 105 passed. Next: Task 2 (hub follower and token).
```

- [ ] **Step 7: Commit**

```bash
cd ..
git add frontend/src docs/handoff.md
git commit -m "fix(frontend): outside the shift the server leaves out shift; test it by truthiness on My Work, check-in and scan (facility-0069)"
```

---

### Task 2: Hub follower, hub status and `getValidAccessToken`

The parts every live page needs: one SignalR connection per cache entry, its reconnect and restart, a fresh token, and a slice that says whether pushes arrive. Tests fake the SignalR package.

**Files:**
- Create: `frontend/src/test/fakeSignalR.ts`, `frontend/src/store/slices/hubSlice.ts`, `frontend/src/shared/api/scanHub.ts`
- Modify: `frontend/vite.config.ts`, `frontend/src/test/setup.ts`, `frontend/src/store/index.ts`, `frontend/src/shared/auth/session.ts`
- Test: `frontend/src/shared/api/scanHub.test.ts` (create), `frontend/src/shared/auth/session.test.ts` (add a `describe`)
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes (plans 7–8): `refreshSession(dispatch)` (`shared/auth/session.ts`), `setCredentials` (`store/slices/authSlice.ts`), `makeStore()`, `PointCard` (`shared/api/types.ts`), `point(overrides?)` (`test/cleanerFixtures.ts`), `fakeServer`, `authAnswer(role, token?)`.
- Produces:
  - `getValidAccessToken(getState: () => RootState, dispatch: Dispatch): Promise<string>` from `shared/auth/session.ts`.
  - `followScanHub(follow: ScanHubFollow): Promise<void>`, `HUB_RETRY_DELAYS_MS`, `HUB_RESTART_MS` from `shared/api/scanHub.ts`. `ScanHubFollow` is `{ cacheDataLoaded, cacheEntryRemoved, dispatch, accessToken: () => Promise<string>, onPoint: (point: PointCard) => void, onMissed: () => void }`.
  - `HubStatus = 'connecting' | 'live' | 'reconnecting'`, `hubStatusChanged(status)`, `hubReducer`; the store keeps it at `state.hub.status`.
  - Test controls in `test/fakeSignalR.ts`:
    - `hub.connections` and `hub.latest()`.
    - `hub.failNextStarts` (how many of the next `start()` calls fail).
    - `resetFakeHub()`.
    - `FakeHubConnection` with `url`, `retryDelays`, `tokens`, `starts`, `stops`, `push(name, payload)`, `reconnecting()`, `reconnected()` and `close()`.

- [ ] **Step 1: The fake SignalR package**

`frontend/src/test/fakeSignalR.ts`:

```ts
/**
 * Stands in for the @microsoft/signalr package in every test (vite.config.ts `test.alias`): regression-net #32 fakes
 * fetch and SignalR only, never our own code. It has just what shared/api/scanHub.ts uses. `hub` lets a test see the
 * connections the app built and play the server's part: push an event, drop the connection, bring it back.
 * test/setup.ts calls resetFakeHub after each test.
 */
type Listener = (...args: unknown[]) => void;

interface FakeConnectionOptions {
  accessTokenFactory?: () => string | Promise<string>;
}

export class FakeHubConnection {
  readonly url: string;
  readonly retryDelays: number[] | undefined;
  /** What accessTokenFactory gave on each start, in order. */
  readonly tokens: string[] = [];
  starts = 0;
  stops = 0;
  state: 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting' = 'Disconnected';
  private readonly options: FakeConnectionOptions;
  private readonly listeners = new Map<string, Listener[]>();
  private readonly closedListeners: Array<(error?: Error) => void> = [];
  private readonly reconnectingListeners: Array<(error?: Error) => void> = [];
  private readonly reconnectedListeners: Array<(connectionId?: string) => void> = [];

  constructor(url: string, options: FakeConnectionOptions, retryDelays: number[] | undefined) {
    this.url = url;
    this.options = options;
    this.retryDelays = retryDelays;
  }

  on(name: string, listener: Listener): void {
    this.listeners.set(name, [...(this.listeners.get(name) ?? []), listener]);
  }

  onclose(callback: (error?: Error) => void): void {
    this.closedListeners.push(callback);
  }

  onreconnecting(callback: (error?: Error) => void): void {
    this.reconnectingListeners.push(callback);
  }

  onreconnected(callback: (connectionId?: string) => void): void {
    this.reconnectedListeners.push(callback);
  }

  async start(): Promise<void> {
    this.starts += 1;
    this.state = 'Connecting';
    this.tokens.push(String(await this.options.accessTokenFactory?.()));
    if (hub.failNextStarts > 0) {
      hub.failNextStarts -= 1;
      this.state = 'Disconnected';
      throw new Error('fake hub: negotiation failed');
    }
    this.state = 'Connected';
  }

  /** Like the real client, stopping an open connection calls the onclose callbacks. */
  async stop(): Promise<void> {
    this.stops += 1;
    const wasOpen = this.state !== 'Disconnected';
    this.state = 'Disconnected';
    if (wasOpen) {
      this.closedListeners.forEach((callback) => callback());
    }
  }

  /** The server sends `name` with `payload` to this connection. */
  push(name: string, payload: unknown): void {
    (this.listeners.get(name) ?? []).forEach((listener) => listener(payload));
  }

  /** The socket dropped and the automatic reconnect began. */
  reconnecting(): void {
    this.state = 'Reconnecting';
    this.reconnectingListeners.forEach((callback) => callback(new Error('fake hub: connection lost')));
  }

  /** The automatic reconnect got through. */
  reconnected(): void {
    this.state = 'Connected';
    this.reconnectedListeners.forEach((callback) => callback('fake-connection'));
  }

  /** The connection ended for good: the automatic reconnect gave up, or the server closed it. */
  close(): void {
    this.state = 'Disconnected';
    this.closedListeners.forEach((callback) => callback(new Error('fake hub: closed')));
  }
}

export class HubConnectionBuilder {
  private url = '';
  private options: FakeConnectionOptions = {};
  private retryDelays: number[] | undefined;

  withUrl(url: string, options: FakeConnectionOptions = {}): this {
    this.url = url;
    this.options = options;
    return this;
  }

  withAutomaticReconnect(retryDelays?: number[]): this {
    this.retryDelays = retryDelays;
    return this;
  }

  configureLogging(): this {
    return this;
  }

  build(): FakeHubConnection {
    const connection = new FakeHubConnection(this.url, this.options, this.retryDelays);
    hub.connections.push(connection);
    return connection;
  }
}

/** What a test sees and drives. failNextStarts: how many of the next start() calls fail, like a negotiate with no network. */
export const hub = {
  connections: [] as FakeHubConnection[],
  failNextStarts: 0,
  /** The newest connection; throws when the app built none. */
  latest(): FakeHubConnection {
    const connection = hub.connections.at(-1);
    if (!connection) {
      throw new Error('the app built no hub connection');
    }
    return connection;
  },
};

export function resetFakeHub(): void {
  hub.connections = [];
  hub.failNextStarts = 0;
}
```

- [ ] **Step 2: Alias the package in tests, reset after each test**

Replace the whole of `frontend/vite.config.ts` with:

```ts
/// <reference types="vitest/config" />
import { execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

/** facility-0068 item 3: the short commit shown on the login pages; "dev" outside a git checkout. */
function buildCommit(): string {
  try {
    return execSync('git rev-parse --short HEAD', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim();
  } catch {
    return 'dev';
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  define: {
    __BUILD_COMMIT__: JSON.stringify(buildCommit()),
  },
  server: {
    host: '0.0.0.0', // Allow access from mobile devices on local WiFi
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5001',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5001',
        ws: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['src/test/setup.ts'],
    // regression-net #32: tests fake the SignalR package the way fakeServer fakes fetch
    alias: { '@microsoft/signalr': fileURLToPath(new URL('./src/test/fakeSignalR.ts', import.meta.url)) },
  },
});
```

Replace the whole of `frontend/src/test/setup.ts` with:

```ts
import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';
import { resetFakeHub } from './fakeSignalR';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  // test/fakeGps.ts and the "plain http" tests define these on the browser objects
  Reflect.deleteProperty(navigator, 'geolocation');
  Reflect.deleteProperty(window, 'isSecureContext');
  resetFakeHub();
});
```

- [ ] **Step 3: Write the failing tests for `getValidAccessToken`**

In `frontend/src/shared/auth/session.test.ts`, change the import line `import { refreshSession, startSession } from './session';` to:

```ts
import { getValidAccessToken, refreshSession, startSession } from './session';
```

and append at the end of the file:

```ts
describe('getValidAccessToken (facility-0023 item 3)', () => {
  function signedIn(token: string, msLeft: number) {
    const store = makeStore();
    const { body } = authAnswer('supervisor', token);
    store.dispatch(setCredentials({ ...body, expiresAt: new Date(Date.now() + msLeft).toISOString() }));
    return store;
  }

  it('a token with more than 30 s left is given as it is', async () => {
    const server = fakeServer({});
    const store = signedIn('token-live', 120_000);

    expect(await getValidAccessToken(store.getState, store.dispatch)).toBe('token-live');
    expect(server.calls).toHaveLength(0);
  });

  it('a token with 30 s or less left is refreshed first, once for every caller (facility-0015)', async () => {
    const server = fakeServer({ 'POST /api/auth/refresh': () => authAnswer('supervisor', 'token-new') });
    const store = signedIn('token-old', 20_000);

    const tokens = await Promise.all([
      getValidAccessToken(store.getState, store.dispatch),
      getValidAccessToken(store.getState, store.dispatch),
    ]);

    expect(tokens).toEqual(['token-new', 'token-new']);
    expect(server.calls.filter((c) => c.path === '/api/auth/refresh')).toHaveLength(1);
  });

  it('a refused refresh gives no token, and the session is over', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 401 }) });
    const store = signedIn('token-old', 1_000);

    expect(await getValidAccessToken(store.getState, store.dispatch)).toBe('');
    expect(store.getState().auth.currentUser).toBeNull();
  });
});
```

- [ ] **Step 4: Write the failing tests for `followScanHub`**

`frontend/src/shared/api/scanHub.test.ts`:

```ts
import { describe, expect, it, vi } from 'vitest';
import { makeStore } from '../../store';
import { point } from '../../test/cleanerFixtures';
import { hub } from '../../test/fakeSignalR';
import { followScanHub, HUB_RESTART_MS } from './scanHub';
import type { PointCard } from './types';

/** One cache entry's life, driven by hand: `loaded` is its first answer, `remove` its end. */
function follow() {
  const store = makeStore();
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
  const counts = { missed: 0 };
  const done = followScanHub({
    cacheDataLoaded,
    cacheEntryRemoved,
    dispatch: store.dispatch,
    accessToken: async () => 'token-supervisor',
    onPoint: (card) => points.push(card),
    onMissed: () => {
      counts.missed += 1;
    },
  });
  return { loaded, failLoad, remove, points, counts, done, status: () => store.getState().hub.status };
}

describe('followScanHub (#26 hub-cache-bridge, facility-0064)', () => {
  it('connects to /hubs/scan only after the first answer, with a token, and goes live', async () => {
    const f = follow();
    await Promise.resolve();
    expect(hub.connections).toHaveLength(0);

    f.loaded();

    await vi.waitFor(() => expect(f.status()).toBe('live'));
    const connection = hub.latest();
    expect(connection.url).toBe('/hubs/scan');
    expect(connection.retryDelays).toEqual([0, 2_000, 5_000, 10_000, 30_000]);
    expect(connection.tokens).toEqual(['token-supervisor']);
    expect(f.counts.missed).toBe(0);
  });

  it('hands every ScanRecorded and InspectionRecorded card to the page, and nothing else', async () => {
    const f = follow();
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));

    hub.latest().push('ScanRecorded', point({ id: 11, status: 'PendingInspection' }));
    hub.latest().push('InspectionRecorded', point({ id: 11, status: 'Passed' }));
    hub.latest().push('AttendanceRecorded', { id: 99 });

    expect(f.points.map((card) => card.status)).toEqual(['PendingInspection', 'Passed']);
  });

  it('after an automatic reconnect it asks once for what it missed', async () => {
    const f = follow();
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));

    hub.latest().reconnecting();
    expect(f.status()).toBe('reconnecting');
    hub.latest().reconnected();

    expect(f.status()).toBe('live');
    expect(f.counts.missed).toBe(1);
  });

  it('when the connection closes it starts again every 3 s, then asks for what it missed', async () => {
    const f = follow();
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      hub.failNextStarts = 1;
      hub.latest().close();
      expect(f.status()).toBe('reconnecting');

      await vi.advanceTimersByTimeAsync(HUB_RESTART_MS); // the first restart, which fails
      await vi.waitFor(() => {
        expect(hub.latest().starts).toBe(2);
        expect(vi.getTimerCount()).toBe(1); // the next restart is waiting
      });
      expect(f.status()).toBe('reconnecting');

      await vi.advanceTimersByTimeAsync(HUB_RESTART_MS); // the second restart gets through
      await vi.waitFor(() => expect(f.status()).toBe('live'));
      expect(hub.latest().starts).toBe(3);
      expect(f.counts.missed).toBe(1);
    } finally {
      vi.useRealTimers();
    }
  });

  it('a first start that fails is tried again in 3 s, and then asks for what it missed', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      hub.failNextStarts = 1;
      const f = follow();
      f.loaded();
      await vi.waitFor(() => {
        expect(f.status()).toBe('reconnecting');
        expect(vi.getTimerCount()).toBe(1);
      });
      expect(f.counts.missed).toBe(0);

      await vi.advanceTimersByTimeAsync(HUB_RESTART_MS);

      await vi.waitFor(() => expect(f.status()).toBe('live'));
      expect(hub.latest().starts).toBe(2);
      expect(f.counts.missed).toBe(1);
    } finally {
      vi.useRealTimers();
    }
  });

  it('when the entry goes, the connection stops, the status is connecting again, and nothing restarts', async () => {
    const f = follow();
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      f.remove();
      await f.done;

      expect(hub.latest().stops).toBe(1);
      expect(f.status()).toBe('connecting');
      expect(vi.getTimerCount()).toBe(0);
    } finally {
      vi.useRealTimers();
    }
  });

  it('an entry removed before its first answer never connects', async () => {
    const f = follow();
    f.failLoad(new Error('removed before the first answer'));
    await f.done;

    expect(hub.connections).toHaveLength(0);
  });
});
```

- [ ] **Step 5: Run the tests to make sure they fail**

Run: `cd frontend && npm test -- src/shared`
Expected: FAIL. `session.test.ts` reports that `getValidAccessToken` is not a function, and `scanHub.test.ts` cannot resolve `./scanHub`.

- [ ] **Step 6: The hub slice**

`frontend/src/store/slices/hubSlice.ts`:

```ts
import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

/**
 * facility-0064 item 1: whether the scan hub is connected is not server data, so it lives in a slice. connecting: not
 * live yet; live: pushes arrive; reconnecting: the connection dropped and is being tried again. Plan 11's Dashboard
 * badge reads this one too.
 */
export type HubStatus = 'connecting' | 'live' | 'reconnecting';

export interface HubState {
  status: HubStatus;
}

const initialState: HubState = { status: 'connecting' };

const hubSlice = createSlice({
  name: 'hub',
  initialState,
  reducers: {
    hubStatusChanged(state, action: PayloadAction<HubStatus>) {
      state.status = action.payload;
    },
  },
});

export const { hubStatusChanged } = hubSlice.actions;
export const hubReducer = hubSlice.reducer;
```

In `frontend/src/store/index.ts`, add the import below the `authSlice` import:

```ts
import { hubReducer } from './slices/hubSlice';
```

and replace:

```ts
const rootReducer = combineReducers({
  auth: authReducer,
  [baseApi.reducerPath]: baseApi.reducer,
});
```

with:

```ts
const rootReducer = combineReducers({
  auth: authReducer,
  hub: hubReducer,
  [baseApi.reducerPath]: baseApi.reducer,
});
```

- [ ] **Step 7: `getValidAccessToken`**

In `frontend/src/shared/auth/session.ts`, add below the existing imports:

```ts
import type { RootState } from '../../store';
```

and append at the end of the file:

```ts
/** facility-0023 item 3: a token with this little left is refreshed before SignalR uses it. */
const REFRESH_MARGIN_MS = 30_000;

/**
 * SignalR's accessTokenFactory, asked on every connect and reconnect. It shares the one refresh in flight
 * (facility-0015). A refused refresh ends the session: the answer is '' and the store listener empties the cache, which
 * stops the hub.
 */
export async function getValidAccessToken(getState: () => RootState, dispatch: Dispatch): Promise<string> {
  const { accessToken, expiresAt } = getState().auth;
  if (accessToken && expiresAt && Date.parse(expiresAt) - Date.now() > REFRESH_MARGIN_MS) {
    return accessToken;
  }
  await refreshSession(dispatch);
  return getState().auth.accessToken ?? '';
}
```

- [ ] **Step 8: `followScanHub`**

`frontend/src/shared/api/scanHub.ts`:

```ts
import { HubConnectionBuilder } from '@microsoft/signalr';
import type { Dispatch } from '@reduxjs/toolkit';
import { hubStatusChanged } from '../../store/slices/hubSlice';
import type { PointCard } from './types';

/** facility-0064: the automatic reconnect's waits; after it gives up, a fresh start every 3 s. */
export const HUB_RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];
export const HUB_RESTART_MS = 3_000;

/** ScanHub's point events (facility-0057, 0058): each carries the point's whole card as the clock's shift sees it. */
const POINT_EVENTS = ['ScanRecorded', 'InspectionRecorded'] as const;

export interface ScanHubFollow {
  /** From onCacheEntryAdded: connect once the first answer is in, stop when the entry goes. */
  cacheDataLoaded: Promise<unknown>;
  cacheEntryRemoved: Promise<unknown>;
  dispatch: Dispatch;
  /** Asked on every connect and reconnect (facility-0023 item 3). */
  accessToken: () => Promise<string>;
  onPoint: (point: PointCard) => void;
  /** Pushes may have been missed while the connection was down: ask the server again (facility-0064 item 2). */
  onMissed: () => void;
}

/**
 * #26 hub-cache-bridge: one connection per cache entry, opened in onCacheEntryAdded, so StrictMode and remounts never
 * open a second one. The hub sends a point's whole card, so the page patches its cache instead of asking again.
 */
export async function followScanHub({
  cacheDataLoaded,
  cacheEntryRemoved,
  dispatch,
  accessToken,
  onPoint,
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
  let attempts = 0;
  let restartTimer: ReturnType<typeof setTimeout> | undefined;

  const restartLater = () => {
    if (!removed) {
      dispatch(hubStatusChanged('reconnecting'));
      restartTimer = setTimeout(() => void start(), HUB_RESTART_MS);
    }
  };
  const start = async () => {
    attempts += 1;
    try {
      await connection.start();
    } catch {
      restartLater();
      return;
    }
    if (removed) {
      return;
    }
    dispatch(hubStatusChanged('live'));
    if (attempts > 1) {
      onMissed(); // a late first start or a restart: pushes before it were missed
    }
  };

  for (const event of POINT_EVENTS) {
    connection.on(event, (point: PointCard) => onPoint(point));
  }
  connection.onreconnecting(() => dispatch(hubStatusChanged('reconnecting')));
  connection.onreconnected(() => {
    dispatch(hubStatusChanged('live'));
    onMissed();
  });
  connection.onclose(restartLater);

  dispatch(hubStatusChanged('connecting'));
  void start();
  await cacheEntryRemoved;
  removed = true;
  clearTimeout(restartTimer);
  dispatch(hubStatusChanged('connecting'));
  await connection.stop();
}
```

- [ ] **Step 9: Run the tests, the typecheck and the lint**

```bash
npm test          # expect: 115 passed (105 + 3 + 7)
npx tsc -b        # expect: no output
npm run lint      # expect: no output
```

If `tsc` rejects `configureLogging('warn')`, check the overloads in `node_modules/@microsoft/signalr/dist/esm/HubConnectionBuilder.d.ts`: the string overload accepts `'warn'`.

- [ ] **Step 10: Handoff entry**

```markdown
- <date> · Task 2 done: `followScanHub` (shared/api/scanHub.ts) opens one SignalR connection per cache entry after its first answer, hands ScanRecorded and InspectionRecorded cards to the page, asks again after a reconnect or a restart (every 3 s after the connection closes), and stops when the entry goes; `hubSlice` holds connecting / live / reconnecting; `getValidAccessToken` refreshes first when 30 s or less is left. Tests fake @microsoft/signalr through `test.alias` (src/test/fakeSignalR.ts). Frontend tests 115 passed. Next: Task 3 (My Building).
```

- [ ] **Step 11: Commit**

```bash
cd ..
git add frontend/vite.config.ts frontend/src docs/handoff.md
git commit -m "feat(frontend): scan hub follower with reconnect and restart, hub status slice, getValidAccessToken; tests fake the SignalR package (facility-0023, 0064)"
```

---

### Task 3: My Building (M15), live

The Supervisor's home stops saying "หน้านี้กำลังสร้าง". It lists every point of the own building, and of buildings covered this shift, with what waits for the Supervisor first. It changes as soon as the hub pushes a card.

**Files:**
- Modify: `frontend/src/shared/api/baseApi.ts`, `frontend/src/pages/my-building/MyBuildingPage.tsx`
- Create: `frontend/src/pages/my-building/myBuildingApi.ts`, `frontend/src/pages/my-building/buildingRules.ts`, `frontend/src/pages/my-building/BuildingCard.tsx`, `frontend/src/pages/my-building/myBuilding.css`, `frontend/src/test/supervisorFixtures.ts`
- Test: `frontend/src/pages/my-building/buildingRules.test.ts`, `frontend/src/pages/my-building/MyBuildingPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes:
  - Task 2: `followScanHub`, `getValidAccessToken`, `state.hub.status`, `HubStatus`, and `hub` (`test/fakeSignalR.ts`).
  - Plan 8: `PointCard`, `LastScan`, `Shift`, `StatusChip`, `formatTime`, `formatClock`, `formatRound`, `shiftLabel`, `freshOnOpen` (`shared/api/myWorkApi.ts`), `getErrorMessage`, `point(overrides?)`.
- Produces:
  - `MyBuildingSection`, `MyBuilding`, `useGetMyBuildingQuery` from `pages/my-building/myBuildingApi.ts`.
  - `patchPoint(building: MyBuilding, card: PointCard): void` and `inspectionOrder(points: readonly PointCard[]): PointCard[]` from `buildingRules.ts`.
  - `baseApi` gets `tagTypes: ['MyBuilding']`.
  - Fixtures in `test/supervisorFixtures.ts`: `supervisorSession`, `lastScanAt(submittedAt, overrides?)`, `section(points?, overrides?)`, `myBuilding(buildings?)`.

- [ ] **Step 1: Supervisor fixtures**

`frontend/src/test/supervisorFixtures.ts`:

```ts
import type { MyBuilding, MyBuildingSection } from '../pages/my-building/myBuildingApi';
import type { LastScan, PointCard } from '../shared/api/types';
import { point } from './cleanerFixtures';
import { authAnswer } from './fakeServer';

/**
 * Made-up answers for the Supervisor pages (public repo: no real names). The API sends UTC; the Thai time is in the
 * comments: 03:05Z is 10:05. Fields the server sends as null are left out, as it does.
 */
export const supervisorSession = { 'POST /api/auth/refresh': () => authAnswer('supervisor') };

export function lastScanAt(submittedAt: string, overrides: Partial<LastScan> = {}): LastScan {
  return { submittedAt, cleanerName: 'แม่บ้านทดสอบ', placement: 'OnTime', status: 'Normal', ...overrides };
}

export function section(points: PointCard[] = [point()], overrides: Partial<MyBuildingSection> = {}): MyBuildingSection {
  return { buildingId: 1, buildingCode: 'A', buildingName: 'ตึกทดลอง', isCover: false, canInspect: true, points, ...overrides };
}

export function myBuilding(buildings: MyBuildingSection[] = [section()]): MyBuilding {
  return { shiftDate: '2026-10-15', shift: 'Day', buildings };
}
```

- [ ] **Step 2: Write the failing rule tests**

`frontend/src/pages/my-building/buildingRules.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { point } from '../../test/cleanerFixtures';
import { lastScanAt, myBuilding, section } from '../../test/supervisorFixtures';
import { inspectionOrder, patchPoint } from './buildingRules';

describe('inspectionOrder (facility-0057 item 1, MyBuildingEndpoints.Sorted)', () => {
  it('รอตรวจ by submit time, then ต้องแก้ไข by when it was asked, then the rest by Area in the order they came', () => {
    const points = [
      point({ id: 1, areaCode: 'AR02', status: 'NotYetDone' }),
      point({ id: 2, status: 'Rework', rework: { defect: 'ก', requestedAt: '2026-10-15T02:50:00Z' } }),
      point({ id: 3, status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:12:00Z') }),
      point({ id: 4, areaCode: 'AR01', status: 'Passed' }),
      point({ id: 5, status: 'Rework', rework: { defect: 'ข', requestedAt: '2026-10-15T02:40:00Z' } }),
      point({ id: 6, status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:05:00Z') }),
      point({ id: 7, areaCode: 'AR01', status: 'OffHours' }),
    ];

    expect(inspectionOrder(points).map((p) => p.id)).toEqual([6, 3, 5, 2, 4, 7, 1]);
  });
});

describe('patchPoint', () => {
  it('a pushed card replaces the same point where it is, in any building', () => {
    const answer = myBuilding([
      section([point({ id: 1 })]),
      section([point({ id: 2 }), point({ id: 3 })], { buildingId: 2, isCover: true }),
    ]);

    patchPoint(answer, point({ id: 3, status: 'PendingInspection' }));

    expect(answer.buildings[1].points.map((p) => [p.id, p.status])).toEqual([
      [2, 'NotYetDone'],
      [3, 'PendingInspection'],
    ]);
  });

  it('a card for a point not on the page changes nothing', () => {
    const answer = myBuilding([section([point({ id: 1 })])]);

    patchPoint(answer, point({ id: 99, status: 'Passed' }));

    expect(answer).toEqual(myBuilding([section([point({ id: 1 })])]));
  });
});
```

- [ ] **Step 3: Write the failing page tests**

`frontend/src/pages/my-building/MyBuildingPage.test.tsx`:

```tsx
import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { point } from '../../test/cleanerFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { renderApp } from '../../test/renderApp';
import { lastScanAt, myBuilding, section, supervisorSession } from '../../test/supervisorFixtures';

function serve(answer: unknown = myBuilding(), routes: FakeRoutes = {}) {
  return fakeServer({ ...supervisorSession, 'GET /api/my-building': () => ({ status: 200, body: answer }), ...routes });
}

const LIVE = 'หน้านี้อัปเดตเองเมื่อแม่บ้านส่งงานหรือหัวหน้าตรวจ';

/** Wireframe M15's points in the server's card order (not by what waits for the Supervisor). */
const fourPoints = [
  point({ id: 1, name: 'โถงทางเดิน', status: 'NotYetDone' }),
  point({
    id: 2,
    name: 'ห้องน้ำหญิง',
    areaId: 2,
    areaCode: 'AR02',
    areaName: 'Area ถัดไป',
    status: 'PendingInspection',
    currentRound: { id: 21, start: '09:00:00', end: '11:00:00' },
    lastScan: lastScanAt('2026-10-15T03:12:00Z'), // 10:12
  }),
  point({
    id: 3,
    name: 'จุดทิ้งขยะ',
    status: 'Rework',
    currentRound: { id: 31, start: '08:00:00', end: '10:00:00' },
    lastScan: lastScanAt('2026-10-15T02:20:00Z'),
    rework: { defect: 'รอบถังขยะยังมีคราบ', requestedAt: '2026-10-15T02:40:00Z' }, // asked 09:40
  }),
  point({ id: 4, name: 'ห้องน้ำชาย', status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:05:00Z') }), // 10:05
];

const cardNames = (region: HTMLElement) => within(region).getAllByRole('heading', { level: 3 }).map((h) => h.textContent);
const tiles = () =>
  within(screen.getByRole('list', { name: 'สรุปการตรวจ' }))
    .getAllByRole('listitem')
    .map((li) => li.textContent);

describe('MyBuildingPage (M15, facility-0057)', () => {
  it('lists what waits for the Supervisor first, with the summary and the clock shift', async () => {
    serve(myBuilding([section(fourPoints)]));
    await renderApp('/my-building');

    const own = await screen.findByRole('region', { name: 'ตึกทดลอง' });
    expect(cardNames(own)).toEqual(['ห้องน้ำชาย', 'ห้องน้ำหญิง', 'จุดทิ้งขยะ', 'โถงทางเดิน']);
    expect(screen.getByRole('heading', { level: 1, name: 'ตึกของฉัน' })).toBeInTheDocument();
    expect(screen.getByText('ตอนนี้ กะเช้า 07:00–19:00')).toBeInTheDocument();
    expect(screen.getByText('2 Area · 4 จุด')).toBeInTheDocument();
    expect(tiles()).toEqual(['2 รอตรวจ', '1 รอแม่บ้านแก้', '0 ตรวจแล้ว']);
    expect(screen.getByText('ส่ง 10:05 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(screen.getByText('Area ถัดไป · รอบ 09:00–11:00')).toBeInTheDocument();
    expect(screen.getByText('ให้แก้ 09:40: “รอบถังขยะยังมีคราบ” · รอแม่บ้านส่งใหม่')).toBeInTheDocument();
    expect(screen.getByText('ยังไม่ส่ง · ส่งได้ถึง 12:00')).toBeInTheDocument();
    expect(screen.getAllByText('ไปที่จุดนี้แล้วสแกน QR เพื่อตรวจ')).toHaveLength(2);
    // facility-0059: only the sign's QR opens the inspection
    expect(within(screen.getByRole('main')).queryAllByRole('link')).toHaveLength(0);
  });

  it('outside the own shift it says so and does not send anyone to scan (facility-0048)', async () => {
    serve(myBuilding([section(fourPoints, { canInspect: false })]));
    await renderApp('/my-building');

    expect(await screen.findByText('ตอนนี้ไม่ใช่กะของคุณ ดูได้ แต่สแกนตรวจไม่ได้')).toBeInTheDocument();
    expect(screen.queryByText('ไปที่จุดนี้แล้วสแกน QR เพื่อตรวจ')).not.toBeInTheDocument();
  });

  it('a building covered this shift has its own section after the own building (facility-0048)', async () => {
    serve(
      myBuilding([
        section([point()]),
        section([point({ id: 21, name: 'ห้องประชุม', areaId: 5, buildingCode: 'B' })], {
          buildingId: 2,
          buildingCode: 'B',
          buildingName: 'ตึกข้างเคียง',
          isCover: true,
        }),
      ]),
    );
    await renderApp('/my-building');

    const covered = await screen.findByRole('region', { name: 'ตึกข้างเคียง (ตรวจแทนกะนี้)' });
    expect(cardNames(covered)).toEqual(['ห้องประชุม']);
    expect(screen.getAllByRole('region').map((r) => r.getAttribute('aria-labelledby'))).toEqual(['building-1', 'building-2']);
  });

  it('an account with no building says to ask the Admin', async () => {
    serve(myBuilding([]));
    await renderApp('/my-building');

    expect(await screen.findByText('บัญชีนี้ยังไม่ได้ผูกกับตึก หรือตึกปิดใช้งาน แจ้ง Admin')).toBeInTheDocument();
  });

  it('a building with no points says so', async () => {
    serve(myBuilding([section([])]));
    await renderApp('/my-building');

    expect(await screen.findByText('ตึกนี้ยังไม่มีจุด')).toBeInTheDocument();
  });

  it('a pushed submission or inspection changes its card at once and moves it ("อัปเดตเอง")', async () => {
    serve(myBuilding([section(fourPoints)]));
    await renderApp('/my-building');
    const own = await screen.findByRole('region', { name: 'ตึกทดลอง' });
    expect(await screen.findByText(LIVE)).toBeInTheDocument();
    expect(hub.latest().tokens).toEqual(['token-supervisor']);

    act(() =>
      hub.latest().push('ScanRecorded', { ...fourPoints[0], status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:20:00Z') }),
    );

    expect(cardNames(own)).toEqual(['ห้องน้ำชาย', 'ห้องน้ำหญิง', 'โถงทางเดิน', 'จุดทิ้งขยะ']);
    expect(screen.getByText('ส่ง 10:20 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(tiles()).toEqual(['3 รอตรวจ', '1 รอแม่บ้านแก้', '0 ตรวจแล้ว']);

    act(() => hub.latest().push('InspectionRecorded', { ...fourPoints[3], status: 'Passed' }));

    expect(cardNames(own)).toEqual(['ห้องน้ำหญิง', 'โถงทางเดิน', 'จุดทิ้งขยะ', 'ห้องน้ำชาย']);
    expect(screen.getByText('ตรวจผ่าน · ส่ง 10:05 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(tiles()).toEqual(['2 รอตรวจ', '1 รอแม่บ้านแก้', '1 ตรวจแล้ว']);
  });

  it('a dropped hub says so, and coming back asks the server again (facility-0064)', async () => {
    const server = serve();
    await renderApp('/my-building');
    await screen.findByText(LIVE);

    act(() => hub.latest().reconnecting());
    expect(screen.getByText('การอัปเดตสดหลุด กำลังต่อใหม่ กด "อัปเดต" เพื่อดูงานล่าสุด')).toBeInTheDocument();

    act(() => hub.latest().reconnected());

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-building')).toHaveLength(2));
    expect(screen.getByText(LIVE)).toBeInTheDocument();
  });

  it('"อัปเดต" asks the server again', async () => {
    const server = serve();
    const { user } = await renderApp('/my-building');
    await screen.findByRole('region', { name: 'ตึกทดลอง' });

    await user.click(screen.getByRole('button', { name: 'อัปเดต' }));

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-building')).toHaveLength(2));
  });

  it('logging out closes the live connection', async () => {
    serve(undefined, { 'POST /api/auth/logout': () => ({ status: 204 }) });
    const { user } = await renderApp('/my-building');
    await screen.findByText(LIVE);

    await user.click(screen.getByRole('button', { name: 'ออกจากระบบ' }));

    await waitFor(() => expect(hub.latest().stops).toBe(1));
  });

  it('a failed load shows the error and keeps the heading', async () => {
    fakeServer({ ...supervisorSession, 'GET /api/my-building': () => ({ status: 503 }) });
    await renderApp('/my-building');

    expect(await screen.findByRole('alert')).toHaveTextContent('เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง');
    expect(screen.getByRole('heading', { level: 1, name: 'ตึกของฉัน' })).toBeInTheDocument();
  });
});
```

- [ ] **Step 4: Run the tests to make sure they fail**

Run: `npm test -- src/pages/my-building`
Expected: FAIL. `buildingRules.test.ts` cannot resolve `./buildingRules`. Every page test fails to find its content, because the page still says "หน้านี้กำลังสร้าง".

- [ ] **Step 5: The tag on `baseApi`**

In `frontend/src/shared/api/baseApi.ts`, replace:

```ts
export const baseApi = createApi({
  reducerPath: 'api',
  baseQuery: baseQueryWithReauth,
  endpoints: () => ({}),
});
```

with:

```ts
export const baseApi = createApi({
  reducerPath: 'api',
  baseQuery: baseQueryWithReauth,
  // Refetched by tag only where a live push may have been missed (facility-0064 item 2)
  tagTypes: ['MyBuilding'],
  endpoints: () => ({}),
});
```

- [ ] **Step 6: The rules**

`frontend/src/pages/my-building/buildingRules.ts`:

```ts
import type { PointCard, PointStatus } from '../../shared/api/types';
import type { MyBuilding } from './myBuildingApi';

/** A pushed card replaces the same point where it is; a point that is not on this page is ignored (plan 9 decision 8). */
export function patchPoint(building: MyBuilding, card: PointCard): void {
  for (const section of building.buildings) {
    const index = section.points.findIndex((p) => p.id === card.id);
    if (index >= 0) {
      section.points[index] = card;
      return;
    }
  }
}

function groupOf(status: PointStatus): number {
  if (status === 'PendingInspection') {
    return 0;
  }
  return status === 'Rework' ? 1 : 2;
}

/** When the point started waiting for the Supervisor; one without a time goes last in its group, as on the server. */
function waitingSince(point: PointCard): string {
  const since = point.status === 'PendingInspection' ? point.lastScan?.submittedAt : point.rework?.requestedAt;
  return since ?? '9999';
}

function ordinal(a: string, b: string): number {
  if (a === b) {
    return 0;
  }
  return a < b ? -1 : 1;
}

/**
 * facility-0057 item 1, the server's order (MyBuildingEndpoints.Sorted): รอตรวจ by submit time, then ต้องแก้ไข by when
 * it was asked, then the rest by Area code. Sorted again on the phone because a pushed card can change group. The sort
 * is stable, so within an Area the rest keep the order they came in.
 */
export function inspectionOrder(points: readonly PointCard[]): PointCard[] {
  return [...points].sort((a, b) => {
    const group = groupOf(a.status) - groupOf(b.status);
    if (group !== 0) {
      return group;
    }
    return groupOf(a.status) === 2 ? ordinal(a.areaCode, b.areaCode) : ordinal(waitingSince(a), waitingSince(b));
  });
}
```

- [ ] **Step 7: The API slice with the hub**

`frontend/src/pages/my-building/myBuildingApi.ts`:

```ts
import { baseApi } from '../../shared/api/baseApi';
import { followScanHub } from '../../shared/api/scanHub';
import type { PointCard, Shift } from '../../shared/api/types';
import { getValidAccessToken } from '../../shared/auth/session';
import type { RootState } from '../../store';
import { patchPoint } from './buildingRules';

/** MyBuildingSectionDto. isCover: shown because of a Cover Assignment this shift; canInspect: may inspect there now (facility-0048). */
export interface MyBuildingSection {
  buildingId: number;
  buildingCode: string;
  buildingName: string;
  isCover: boolean;
  canInspect: boolean;
  points: PointCard[];
}

/** MyBuildingDto (facility-0057 item 1): the clock's shift; the own building first, then buildings covered in it. */
export interface MyBuilding {
  shiftDate: string;
  shift: Shift;
  buildings: MyBuildingSection[];
}

const myBuildingApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getMyBuilding: build.query<MyBuilding, void>({
      query: () => '/my-building',
      providesTags: ['MyBuilding'],
      // facility-0057 item 1 "อัปเดตเอง": pushed cards patch this entry; a reconnect asks again (#26, facility-0064)
      async onCacheEntryAdded(_arg, { cacheDataLoaded, cacheEntryRemoved, updateCachedData, dispatch, getState }) {
        await followScanHub({
          cacheDataLoaded,
          cacheEntryRemoved,
          dispatch,
          accessToken: () => getValidAccessToken(() => getState() as RootState, dispatch),
          onPoint: (card) => updateCachedData((draft) => patchPoint(draft, card)),
          onMissed: () => dispatch(baseApi.util.invalidateTags(['MyBuilding'])),
        });
      },
    }),
  }),
});

export const { useGetMyBuildingQuery } = myBuildingApi;
```

- [ ] **Step 8: The card**

`frontend/src/pages/my-building/BuildingCard.tsx`:

```tsx
import type { PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatClock, formatRound, formatTime } from '../../shared/utils/thaiTime';

interface BuildingCardProps {
  point: PointCard;
  /** Own building in own shift, or a cover (facility-0048): only then does a รอตรวจ card send the Supervisor to scan. */
  canInspect: boolean;
}

/** One point on My Building. Names, defects and notes are rendered as React text only (facility-0066). */
export function BuildingCard({ point, canInspect }: BuildingCardProps) {
  const round = point.currentRound
    ? `รอบ ${formatRound(point.currentRound)}`
    : point.nextRound
      ? `รอบถัดไป ${formatRound(point.nextRound)}`
      : null;

  return (
    <li className={`building-card building-card--${point.status}`}>
      <div className="building-card__top">
        <h3 className="building-card__name">{point.name}</h3>
        <StatusChip status={point.status} />
      </div>
      <span className="building-card__where">{[point.areaName, round].filter(Boolean).join(' · ')}</span>
      <span className="building-card__detail">{detailOf(point)}</span>
      {canInspect && point.status === 'PendingInspection' && (
        <span className="building-card__todo">ไปที่จุดนี้แล้วสแกน QR เพื่อตรวจ</span>
      )}
      {point.issue && (
        <span className="building-card__issue">
          {`แจ้งปัญหา: ${[...point.issue.tags, point.issue.note].filter(Boolean).join(' · ')}`}
        </span>
      )}
    </li>
  );
}

/** Who asked for a rework is not in ReworkDto, so M15's "คุณให้แก้" is "ให้แก้" (plan 9 decision 7). */
function detailOf(point: PointCard): string {
  const sent = point.lastScan ? `ส่ง ${formatTime(point.lastScan.submittedAt)} · ${point.lastScan.cleanerName}` : null;
  switch (point.status) {
    case 'PendingInspection':
      return sent ?? 'ส่งแล้ว';
    case 'Rework':
      return point.rework
        ? `ให้แก้ ${formatTime(point.rework.requestedAt)}: “${point.rework.defect}” · รอแม่บ้านส่งใหม่`
        : 'รอแม่บ้านส่งใหม่';
    case 'Passed':
      return ['ตรวจผ่าน', sent].filter(Boolean).join(' · ');
    case 'Overdue':
      return 'ยังไม่ส่ง · เลยเวลารอบแล้ว';
    case 'NotYetDone':
      return point.currentRound ? `ยังไม่ส่ง · ส่งได้ถึง ${formatClock(point.currentRound.end)}` : 'ยังไม่ส่ง';
    case 'BeforeFirstRound':
      return 'รอบแรกของกะยังไม่เริ่ม';
    case 'OffHours':
      return 'จุดนี้ไม่มีรอบในกะนี้';
  }
}
```

- [ ] **Step 9: The page**

Replace the whole of `frontend/src/pages/my-building/MyBuildingPage.tsx` with:

```tsx
import { freshOnOpen } from '../../shared/api/myWorkApi';
import type { PointStatus } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { useAppSelector } from '../../store/hooks';
import type { HubStatus } from '../../store/slices/hubSlice';
import { BuildingCard } from './BuildingCard';
import { inspectionOrder } from './buildingRules';
import { useGetMyBuildingQuery, type MyBuilding } from './myBuildingApi';
import './myBuilding.css';

/** Wireframe M15's footnote "หน้านี้อัปเดตเองเมื่อแม่บ้านส่งงาน", by the hub's state (plan 9 decision 9). */
const liveLine: Record<HubStatus, string> = {
  connecting: 'กำลังเชื่อมต่อการอัปเดตสด...',
  live: 'หน้านี้อัปเดตเองเมื่อแม่บ้านส่งงานหรือหัวหน้าตรวจ',
  reconnecting: 'การอัปเดตสดหลุด กำลังต่อใหม่ กด "อัปเดต" เพื่อดูงานล่าสุด',
};

/**
 * facility-0057 item 1, wireframe M15: every point of the Supervisor's building, and of buildings they cover this
 * shift, what waits for them first. It only shows the way: inspecting needs the point's QR (facility-0059), so no card
 * is a link. The name is not shown: the bar above already shows it.
 */
export function MyBuildingPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyBuildingQuery(undefined, freshOnOpen);
  const hubStatus = useAppSelector((state) => state.hub.status);

  return (
    <main className="building-page">
      <header className="building-heading">
        {data && <span className="building-kicker">{`ตอนนี้ ${shiftLabel[data.shift]}`}</span>}
        <h1>ตึกของฉัน</h1>
        {data && <p className="building-sub">{countLine(data)}</p>}
      </header>
      {isLoading && <p className="building-muted">กำลังโหลดจุด...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {data && <BuildingList building={data} />}
      <footer className="building-footer">
        <span>{liveLine[hubStatus]}</span>
        <button type="button" className="app-button" disabled={isFetching} onClick={() => void refetch()}>
          {isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </button>
      </footer>
    </main>
  );
}

function countLine(building: MyBuilding): string {
  const points = building.buildings.flatMap((section) => section.points);
  const areas = new Set(points.map((point) => point.areaId));
  return `${areas.size} Area · ${points.length} จุด`;
}

function BuildingList({ building }: { building: MyBuilding }) {
  if (building.buildings.length === 0) {
    return <p className="app-note app-note--info">บัญชีนี้ยังไม่ได้ผูกกับตึก หรือตึกปิดใช้งาน แจ้ง Admin</p>;
  }
  const points = building.buildings.flatMap((section) => section.points);
  const count = (status: PointStatus) => points.filter((point) => point.status === status).length;

  return (
    <>
      <ul className="building-summary" aria-label="สรุปการตรวจ">
        <li className="building-summary__pending">
          <strong>{count('PendingInspection')}</strong> รอตรวจ
        </li>
        <li className="building-summary__rework">
          <strong>{count('Rework')}</strong> รอแม่บ้านแก้
        </li>
        <li>
          <strong>{count('Passed')}</strong> ตรวจแล้ว
        </li>
      </ul>
      {building.buildings.map((section) => {
        const headingId = `building-${section.buildingId}`;
        return (
          <section key={section.buildingId} className="building-section" aria-labelledby={headingId}>
            <h2 id={headingId}>{section.isCover ? `${section.buildingName} (ตรวจแทนกะนี้)` : section.buildingName}</h2>
            {!section.canInspect && (
              <p className="app-note app-note--warn">ตอนนี้ไม่ใช่กะของคุณ ดูได้ แต่สแกนตรวจไม่ได้</p>
            )}
            {section.points.length === 0 ? (
              <p className="building-muted">ตึกนี้ยังไม่มีจุด</p>
            ) : (
              <ul className="building-cards">
                {inspectionOrder(section.points).map((point) => (
                  <BuildingCard key={point.id} point={point} canInspect={section.canInspect} />
                ))}
              </ul>
            )}
          </section>
        );
      })}
    </>
  );
}
```

- [ ] **Step 10: The styles**

`frontend/src/pages/my-building/myBuilding.css` (colours from `SupervisorHome.dc.html`, through the tokens):

```css
/* My Building, wireframe M15. Class prefix: building- */

.building-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.building-heading {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.building-heading h1 {
  font-size: 26px;
  font-weight: 700;
}

.building-kicker {
  font-size: 13px;
  letter-spacing: 0.04em;
  color: var(--color-muted);
}

.building-sub,
.building-muted {
  font-size: 15px;
  color: var(--color-muted);
}

.building-summary {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin: 0;
  padding: 0;
  list-style: none;
  text-align: center;
  font-size: 12px;
}

.building-summary li {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 8px 4px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.building-summary strong {
  font-size: 24px;
  font-variant-numeric: tabular-nums;
}

.building-summary .building-summary__pending {
  background: var(--color-info-bg);
  border-color: var(--color-info-border);
  color: var(--color-status-pending);
}

.building-summary .building-summary__rework {
  background: var(--color-error-bg);
  border-color: var(--color-error-border);
  color: var(--color-error-text);
}

.building-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.building-section h2 {
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-soft);
}

.building-cards {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.building-card {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 12px 14px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.building-card--PendingInspection {
  padding: 11px 13px;
  border: 2px solid var(--color-primary);
}

.building-card--Rework {
  padding: 11px 13px;
  border: 2px solid var(--color-status-rework);
}

.building-card__top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 8px;
}

.building-card__name {
  font-size: 16px;
  font-weight: 600;
}

.building-card__where {
  font-size: 13px;
  color: var(--color-muted);
  font-variant-numeric: tabular-nums;
}

.building-card__detail {
  font-size: 14px;
}

.building-card__todo {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-primary);
}

.building-card__issue {
  font-size: 13px;
  color: var(--color-error-text);
}

.building-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  font-size: 13px;
  color: var(--color-text-soft);
}
```

- [ ] **Step 11: Run the tests, the typecheck and the lint**

```bash
npm test          # expect: 128 passed (115 + 3 + 10)
npx tsc -b        # expect: no output
npm run lint      # expect: no output
```

`router.test.tsx` "a live session opens the role home" still passes. It has no fake for `GET /api/my-building`, so the page shows the fake's 404 message under the heading "ตึกของฉัน", and the hub never connects because the first answer failed.

- [ ] **Step 12: Handoff entry**

```markdown
- <date> · Task 3 done: My Building (M15, /my-building): the own building then covered ones, รอตรวจ by submit time, ต้องแก้ไข, then the rest; summary tiles; "not your shift" note; no links to the sign. Live: ScanRecorded and InspectionRecorded patch the card and it moves; a reconnect, "อัปเดต" and every visit ask the server; the footer says whether the hub is live; logout closes it. baseApi has tag `MyBuilding`. Frontend tests 128 passed. Next: Task 4 (inspection page).
```

- [ ] **Step 13: Commit**

```bash
cd ..
git add frontend/src docs/handoff.md
git commit -m "feat(frontend): My Building page for Supervisors, live from the hub, what waits first (facility-0057, 0064)"
```

---

### Task 4: The inspection page at a point's sign (M6, M7, M16)

A Supervisor who scans a point's sign sees the latest submission of the current round, chooses สะอาด or ไม่สะอาด (with a defect), and sees the saved result. With nothing to inspect, the page says why and has no inspect button. The URL stays `/scan/:token`; the role picks the page.

**Files:**
- Create: `frontend/src/shared/components/ByRole.tsx`, `frontend/src/pages/inspect/index.ts`, `inspectApi.ts`, `inspectRules.ts`, `InspectHeading.tsx`, `DistanceLine.tsx`, `InspectForm.tsx`, `InspectResult.tsx`, `NothingToInspect.tsx`, `InspectPage.tsx`, `inspect.css` (all in `frontend/src/pages/inspect/` but the first)
- Modify: `frontend/src/router.tsx`, `frontend/src/shared/api/myWorkApi.ts` (one comment), `frontend/src/test/supervisorFixtures.ts`, `frontend/src/pages/scan/ScanPage.test.tsx` (the plan-8 pin)
- Test: `frontend/src/pages/inspect/InspectPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes:
  - Plans 7–8: `readLocation()`, `GpsFix` and `LocationProblem` (`shared/geo/readLocation.ts`); `statusOf(error)` (`shared/api/scanAnswers.ts`); `SEND_TIMEOUT_MS` and `baseApi`; `freshOnOpen`; `StatusScreen`, `StatusChip`, `ProtectedRoute` and `AppLayout`; `formatTime` and `formatRound`; `getErrorMessage`.
  - Test helpers: `fakeGps`, `nearSign` and `farFromSign` (`test/fakeGps.ts`); `point()`.
  - Task 3: `supervisorSession` and `lastScanAt`.
- Produces:
  - `<ByRole pages={Partial<Record<Role, ReactNode>>} />` from `shared/components/ByRole.tsx`.
  - From `pages/inspect/inspectApi.ts`:
    - Types: `InspectionSubmission`, `InspectionScan`, `InspectionVisit` (`InspectionScan & { accuracyM }`), `InspectionResult = 'Passed' | 'Rework'`, `RecordInspectionRequest` and `InspectionRecorded`.
    - Hooks: `useOpenInspectionQuery(token, options)` and `useRecordInspectionMutation()`.
    - A location problem on open is the error `{ status: 'CUSTOM_ERROR', error: LocationProblem }`.
  - From `inspectRules.ts`: `InspectDraft`, `emptyDraft`, `MAX_DEFECT_LENGTH`, `draftProblem(draft)`, `describeDraft(draft)`, `requestOf(qrToken, scanRecordId, draft, fix)`, `submissionLine(submission, point)` and `locationProblemOf(error)`.
  - Components: `<InspectHeading visit />` and `<DistanceLine visit />` (Task 5 reuses both).
  - Fixtures: `submission(overrides?)`, `inspectionScan(overrides?)`, `nothingToInspect(refusal?, overrides?)` and `recorded(overrides?)`.

- [ ] **Step 1: Inspection fixtures**

In `frontend/src/test/supervisorFixtures.ts`, add this import below the `myBuildingApi` import:

```ts
import type { InspectionRecorded, InspectionScan, InspectionSubmission } from '../pages/inspect/inspectApi';
```

and append at the end of the file:

```ts
/** The submission a scan offers for judging (InspectionSubmissionDto); 03:05Z is 10:05. */
export function submission(overrides: Partial<InspectionSubmission> = {}): InspectionSubmission {
  return {
    id: 501,
    submittedAt: '2026-10-15T03:05:00Z',
    cleanerName: 'แม่บ้านทดสอบ',
    placement: 'OnTime',
    status: 'Normal',
    issueTags: [],
    ...overrides,
  };
}

/** POST /api/inspections/scan with work to judge (M6): 4 m from the sign, within the radius. */
export function inspectionScan(overrides: Partial<InspectionScan> = {}): InspectionScan {
  return {
    point: point({ status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:05:00Z') }),
    submission: submission(),
    isCover: false,
    distanceM: 4,
    withinRadius: true,
    ...overrides,
  };
}

/** POST /api/inspections/scan with nothing to judge (M16): no submission key, as the server sends it. */
export function nothingToInspect(
  refusal = 'ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน',
  overrides: Partial<InspectionScan> = {},
): InspectionScan {
  return { point: point(), refusal, isCover: false, distanceM: 5, withinRadius: true, ...overrides };
}

/** A 201 answer of POST /api/inspections; 03:15Z is 10:15. */
export function recorded(overrides: Partial<InspectionRecorded> = {}): InspectionRecorded {
  return {
    inspectionId: 901,
    servicePointId: 11,
    scanRecordId: 501,
    result: 'Passed',
    newPointStatus: 'Passed',
    distanceM: 4,
    withinRadius: true,
    inspectedAt: '2026-10-15T03:15:00Z',
    ...overrides,
  };
}
```

- [ ] **Step 2: Write the failing page tests**

`frontend/src/pages/inspect/InspectPage.test.tsx`:

```tsx
import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { point } from '../../test/cleanerFixtures';
import { farFromSign, fakeGps, nearSign } from '../../test/fakeGps';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';
import {
  inspectionScan,
  nothingToInspect,
  recorded,
  submission,
  supervisorSession,
} from '../../test/supervisorFixtures';
import type { InspectionScan } from './inspectApi';

const SIGN = '/scan/token-restroom-m1';

function serve(routes: FakeRoutes = {}, scan: InspectionScan = inspectionScan()) {
  return fakeServer({
    ...supervisorSession,
    'POST /api/inspections/scan': () => ({ status: 200, body: scan }),
    ...routes,
  });
}

const nearBody = { latitude: 13.7563, longitude: 100.5018, accuracyM: 12 };

describe('InspectPage (M6, M7, M16, facility-0029)', () => {
  it('opening the sign reads a fresh location, scans, and shows the latest submission with no verdict chosen (M6)', async () => {
    const gps = fakeGps(nearSign);
    const server = serve();
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(server.calls.find((c) => c.path === '/api/inspections/scan')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      ...nearBody,
    });
    expect(gps.lastOptions).toMatchObject({ enableHighAccuracy: true, maximumAge: 0 });
    expect(screen.getByText('ตรวจงาน · Area ทดลอง · ตึก A')).toBeInTheDocument();
    expect(screen.getByText('10:05 · แม่บ้านทดสอบ · รอบ 10:00–12:00 · เรียบร้อย')).toBeInTheDocument();
    expect(screen.getByText('ห่างป้าย 4 ม. · ±12 ม.')).toBeInTheDocument();
    expect(screen.getByText('อยู่ในรัศมี')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'สะอาด' })).not.toBeChecked();
    expect(screen.getByRole('radio', { name: 'ไม่สะอาด' })).not.toBeChecked();
    expect(screen.queryByLabelText('ข้อบกพร่อง')).not.toBeInTheDocument();
    // The Supervisor's page never touches the Cleaner's endpoints
    expect(server.calls.some((c) => c.path.startsWith('/api/service-points') || c.path === '/api/scan-records')).toBe(false);
  });

  it('สะอาด records Passed with a fresh location and shows the saved result (M7)', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/inspections': () => ({ status: 201, body: recorded() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect(server.calls.find((c) => c.path === '/api/inspections')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      scanRecordId: 501,
      result: 'Passed',
      ...nearBody,
    });
    expect(screen.getByText('ผล: สะอาด')).toBeInTheDocument();
    expect(screen.getByText('ตรวจผ่าน')).toBeInTheDocument();
    expect(screen.getByText('ห้องน้ำชาย · รอบ 10:00–12:00')).toBeInTheDocument();
    expect(screen.getByText('ห่างป้าย 4 ม. · อยู่ในรัศมี')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับไปตึกของฉัน' })).toHaveAttribute('href', '/my-building');
  });

  it('ไม่สะอาด needs a defect, then sends it trimmed and shows what the Cleaner will see (M7)', async () => {
    fakeGps(nearSign);
    const server = serve({
      'POST /api/inspections': () => ({ status: 201, body: recorded({ result: 'Rework', newPointStatus: 'Rework' }) }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'ไม่สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(screen.getByRole('alert')).toHaveTextContent('ใส่ข้อบกพร่องก่อนบันทึก');
    expect(server.calls.some((c) => c.path === '/api/inspections')).toBe(false);

    await user.type(screen.getByLabelText('ข้อบกพร่อง'), '  พื้นใต้อ่างล้างมือยังเปียก ');
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect(server.calls.find((c) => c.path === '/api/inspections')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      scanRecordId: 501,
      result: 'Rework',
      defect: 'พื้นใต้อ่างล้างมือยังเปียก',
      ...nearBody,
    });
    expect(screen.getByText('ผล: ไม่สะอาด')).toBeInTheDocument();
    expect(screen.getByText('ต้องแก้ไข')).toBeInTheDocument();
    expect(screen.getByText('“พื้นใต้อ่างล้างมือยังเปียก”')).toBeInTheDocument();
    expect(
      screen.getByText('แม่บ้านทดสอบเห็นข้อบกพร่องนี้ทันทีในหน้างานของฉัน และเมื่อสแกนจุดนี้ แก้แล้วส่งงานใหม่ จากนั้นหัวหน้าตรวจอีกรอบ'),
    ).toBeInTheDocument();
    expect(
      screen.getByText('ถ้าไม่ได้แก้จนรอบ 13:00–15:00 เริ่ม การ์ดขึ้นรอบใหม่ และรอบนี้บันทึกว่า "ไม่ได้แก้"'),
    ).toBeInTheDocument();
  });

  it('with no verdict chosen it asks for one and sends nothing (plan 9 decision 4)', async () => {
    fakeGps(nearSign);
    const server = serve();
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(screen.getByRole('alert')).toHaveTextContent('เลือกผลการตรวจก่อน');
    expect(server.calls.some((c) => c.path === '/api/inspections')).toBe(false);
  });

  it('the defect goes only with ไม่สะอาด: switching back to สะอาด sends none', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/inspections': () => ({ status: 201, body: recorded() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'ไม่สะอาด' }));
    await user.type(screen.getByLabelText('ข้อบกพร่อง'), 'คราบน้ำ');
    await user.click(screen.getByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' });
    expect(server.calls.find((c) => c.path === '/api/inspections')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      scanRecordId: 501,
      result: 'Passed',
      ...nearBody,
    });
  });

  it('the form is locked while recording, so a second tap cannot write a second inspection', async () => {
    fakeGps(nearSign, 'pending');
    serve();
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('button', { name: 'กำลังบันทึก...' })).toBeDisabled();
    expect(screen.getByRole('radio', { name: 'สะอาด' })).toBeDisabled();
    expect(screen.getByRole('radio', { name: 'ไม่สะอาด' })).toBeDisabled();
  });

  it('a late submission that reported an issue says both', async () => {
    fakeGps(nearSign);
    serve(
      {},
      inspectionScan({
        submission: submission({ status: 'Issue', issueTags: ['น้ำยาหมด'], note: 'ก๊อกรั่ว', placement: 'Late' }),
      }),
    );
    await renderApp(SIGN);

    expect(
      await screen.findByText('10:05 · แม่บ้านทดสอบ · รอบ 10:00–12:00 · พบปัญหา: น้ำยาหมด · ก๊อกรั่ว · ส่งช้า'),
    ).toBeInTheDocument();
  });

  it('from outside the radius the page warns, and the saved result shows the flag (facility-0037)', async () => {
    fakeGps(farFromSign);
    serve(
      { 'POST /api/inspections': () => ({ status: 201, body: recorded({ distanceM: 80, withinRadius: false }) }) },
      inspectionScan({ distanceM: 80, withinRadius: false }),
    );
    const { user } = await renderApp(SIGN);

    expect(
      await screen.findByText('ห่างป้าย 80 ม. · ±40 ม. · นอกรัศมี ผลตรวจจากตรงนี้จะติดธงให้ Admin เห็น'),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByText('ติดธง: ห่างป้าย 80 ม. Admin จะเห็นรายการนี้')).toBeInTheDocument();
  });

  it('a covered building says the inspection is a cover (facility-0048)', async () => {
    fakeGps(nearSign);
    serve({}, inspectionScan({ isCover: true }));
    await renderApp(SIGN);

    expect(await screen.findByText('ตรวจแทนตึก A ในกะนี้ (Admin มอบหมาย)')).toBeInTheDocument();
  });

  it('no submission in the current round: the reason, the point, and no inspect button (M16, facility-0047 rule 7)', async () => {
    fakeGps(nearSign);
    serve({}, nothingToInspect());
    await renderApp(SIGN);

    expect(
      await screen.findByRole('heading', { name: 'ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(screen.getByText('รอบปัจจุบัน 10:00–12:00')).toBeInTheDocument();
    expect(screen.getByText('ห่างป้าย 5 ม. · ±12 ม.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'บันทึกผลตรวจ' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับไปตึกของฉัน' })).toHaveAttribute('href', '/my-building');
  });

  it('a rework still owed: the reason and the defect that was asked for (facility-0029)', async () => {
    fakeGps(nearSign);
    serve(
      {},
      nothingToInspect('รอแม่บ้านส่งงานแก้ ยังไม่มีงานใหม่ให้ตรวจ', {
        point: point({ status: 'Rework', rework: { defect: 'พื้นยังเปียก', requestedAt: '2026-10-15T02:40:00Z' } }),
      }),
    );
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'รอแม่บ้านส่งงานแก้ ยังไม่มีงานใหม่ให้ตรวจ' })).toBeInTheDocument();
    expect(screen.getByText('ให้แก้ 09:40: “พื้นยังเปียก”')).toBeInTheDocument();
  });

  it('new work since the scan (409): the page scans again, clears the verdict and says why (decision 5)', async () => {
    fakeGps(nearSign);
    let scans = 0;
    const newer = inspectionScan({ submission: submission({ id: 502, submittedAt: '2026-10-15T03:12:00Z' }) });
    const server = serve({
      'POST /api/inspections/scan': () => {
        scans += 1;
        return { status: 200, body: scans === 1 ? inspectionScan() : newer };
      },
      'POST /api/inspections': (request) =>
        (request.body as { scanRecordId: number }).scanRecordId === 501
          ? { status: 409, body: { message: 'มีการส่งงานใหม่หลังจากที่คุณสแกน ให้สแกนป้ายอีกครั้ง' } }
          : { status: 201, body: recorded({ scanRecordId: 502 }) },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByText('10:12 · แม่บ้านทดสอบ · รอบ 10:00–12:00 · เรียบร้อย')).toBeInTheDocument();
    expect(
      screen.getByText('งานของจุดนี้เปลี่ยนไประหว่างที่คุณตรวจ ผลยังไม่ได้บันทึก หน้านี้เปิดงานล่าสุดให้แล้ว'),
    ).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'สะอาด' })).not.toBeChecked();

    await user.click(screen.getByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect(
      server.calls
        .filter((c) => c.path === '/api/inspections')
        .map((c) => (c.body as { scanRecordId: number }).scanRecordId),
    ).toEqual([501, 502]);
  });

  it('a refusal (400) stays on the form with the server message', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/inspections': () => ({ status: 400, body: { message: 'ต้องเลือกผลการตรวจ: สะอาด หรือ ไม่สะอาด' } }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ต้องเลือกผลการตรวจ: สะอาด หรือ ไม่สะอาด');
    expect(screen.getByRole('button', { name: 'บันทึกผลตรวจ' })).toBeEnabled();
  });

  it('an unknown or closed sign shows the server message (404)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/inspections/scan': () => ({ status: 404, body: { message: 'ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว' } }) });
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับไปตึกของฉัน' })).toHaveAttribute('href', '/my-building');
  });
});
```

- [ ] **Step 3: Replace plan 8's pin with the Admin's**

In `frontend/src/pages/scan/ScanPage.test.tsx`, replace the whole test that starts with `it('a Supervisor sees no-permission until plan 9 adds inspection here (plan 8 decision 2)'` with:

```tsx
  it('an Admin opening a point sign sees no-permission; Cleaners and Supervisors use it (plan 9 decision 2)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('admin') });
    await renderApp(SIGN);

    expect(screen.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeInTheDocument();
    expect(screen.getByText('หน้านี้สำหรับแม่บ้าน หรือ หัวหน้างาน บัญชีนี้เป็นAdmin')).toBeInTheDocument();
  });
```

- [ ] **Step 4: Run the tests to make sure they fail**

Run: `npm test -- src/pages/inspect src/pages/scan`
Expected: FAIL.
- Every `InspectPage` test finds "ไม่มีสิทธิ์เปิดหน้านี้" instead of the inspection, because the route is still Cleaner-only. (The import of `./inspectApi` is a type import, so it does not stop the file.)
- The Admin test finds "หน้านี้สำหรับแม่บ้าน บัญชีนี้เป็นAdmin".
- Every other `ScanPage` test passes.

- [ ] **Step 5: `ByRole` and the route**

`frontend/src/shared/components/ByRole.tsx`:

```tsx
import type { ReactNode } from 'react';
import { useAppSelector } from '../../store/hooks';
import type { Role } from '../api/types';

/**
 * One URL, a page per role (facility-0029 item 1): a point's sign opens the Cleaner's submit form or the Supervisor's
 * inspection. It sits inside a ProtectedRoute that already let only these roles in. A page may not import another
 * page, so the route table names both and this picks one.
 */
export function ByRole({ pages }: { pages: Partial<Record<Role, ReactNode>> }) {
  const role = useAppSelector((state) => state.auth.currentUser?.role);
  return <>{role ? pages[role] : null}</>;
}
```

Replace the whole of `frontend/src/router.tsx` with:

```tsx
import type { RouteObject } from 'react-router';
import { AdminLoginPage, LoginPage } from './pages/auth';
import { CheckInPage } from './pages/check-in';
import { DashboardPage } from './pages/dashboard';
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
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    errorElement: <CrashScreen />,
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
        children: [{ element: <AppLayout wide />, children: [{ path: 'dashboard', element: <DashboardPage /> }] }],
      },
      {
        path: '*',
        element: <StatusScreen title="ไม่พบหน้านี้" detail="ที่อยู่นี้ไม่มีในระบบ" linkTo="/" linkText="กลับหน้าแรก" />,
      },
    ],
  },
];
```

In `frontend/src/shared/api/myWorkApi.ts`, replace the comment `/** Hook options for every Cleaner page: never act on an answer from an earlier visit. */` with:

```ts
/** Hook options for every phone page (Cleaner and Supervisor): never act on an answer from an earlier visit. */
```

- [ ] **Step 6: The API slice and the rules**

`frontend/src/pages/inspect/inspectApi.ts`:

```ts
import { baseApi, SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import type { CleaningStatus, Placement, PointCard, PointStatus } from '../../shared/api/types';
import { readLocation, type GpsFix } from '../../shared/geo/readLocation';

/** InspectionSubmissionDto: the submission the Supervisor judges (M6 "ส่งงานล่าสุด"). note is left out when empty. */
export interface InspectionSubmission {
  id: number;
  submittedAt: string;
  cleanerName: string;
  placement: Placement;
  status: CleaningStatus;
  issueTags: string[];
  note?: string;
}

/**
 * InspectionScanDto. Without a submission, refusal says why there is nothing to inspect (facility-0047 rule 7).
 * isCover: allowed here only by a Cover Assignment. distanceM and withinRadius are left out when the sign has no
 * coordinates.
 */
export interface InspectionScan {
  point: PointCard;
  submission?: InspectionSubmission;
  refusal?: string;
  isCover: boolean;
  distanceM?: number;
  withinRadius?: boolean;
}

/** What the page opened with: the scan's answer and the accuracy of the fix it sent (shown as ±). */
export interface InspectionVisit extends InspectionScan {
  accuracyM: number;
}

/** facility-0029: Passed = สะอาด, Rework = ไม่สะอาด. */
export type InspectionResult = 'Passed' | 'Rework';

/** CreateInspectionRequest. scanRecordId is the submission the scan showed; defect goes only with Rework. */
export interface RecordInspectionRequest extends GpsFix {
  qrToken: string;
  scanRecordId: number;
  result: InspectionResult;
  defect?: string;
}

/** InspectionCreatedResponse (201). withinRadius false is a Geofence Flag (the INSPECTION_GPS alert). */
export interface InspectionRecorded {
  inspectionId: number;
  servicePointId: number;
  scanRecordId: number;
  result: InspectionResult;
  newPointStatus: PointStatus;
  distanceM?: number;
  withinRadius?: boolean;
  inspectedAt: string;
}

const inspectApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /**
     * Plan 9 decision 3: the scan runs when the page opens, with a fresh fix (facility-0037). It is a query, so StrictMode
     * and a quick remount share one request. It records nothing unless the scan is blocked (facility-0048). A location
     * problem is a CUSTOM_ERROR whose `error` is the LocationProblem.
     */
    openInspection: build.query<InspectionVisit, string>({
      async queryFn(qrToken, _api, _extraOptions, baseQuery) {
        const reading = await readLocation();
        if (reading.kind !== 'fix') {
          return { error: { status: 'CUSTOM_ERROR' as const, error: reading.kind } };
        }
        const answer = await baseQuery({
          url: '/inspections/scan',
          method: 'POST',
          body: { qrToken, ...reading.fix },
          timeout: SEND_TIMEOUT_MS,
        });
        if (answer.error) {
          return { error: answer.error };
        }
        return { data: { ...(answer.data as InspectionScan), accuracyM: reading.fix.accuracyM } };
      },
    }),
    recordInspection: build.mutation<InspectionRecorded, RecordInspectionRequest>({
      query: (body) => ({ url: '/inspections', method: 'POST', body, timeout: SEND_TIMEOUT_MS }),
    }),
  }),
});

export const { useOpenInspectionQuery, useRecordInspectionMutation } = inspectApi;
```

`frontend/src/pages/inspect/inspectRules.ts`:

```ts
import { statusOf } from '../../shared/api/scanAnswers';
import type { PointCard } from '../../shared/api/types';
import type { GpsFix, LocationProblem } from '../../shared/geo/readLocation';
import { formatRound, formatTime } from '../../shared/utils/thaiTime';
import type { InspectionResult, InspectionSubmission, RecordInspectionRequest } from './inspectApi';

/** The verdict being made (facility-0024: a draft lives in useState). Nothing is chosen at first (plan 9 decision 4). */
export interface InspectDraft {
  result: InspectionResult | null;
  defect: string;
}

export const emptyDraft: InspectDraft = { result: null, defect: '' };

/** inspections.defect is VARCHAR(1000) (InspectionRules.MaxDefectLength). */
export const MAX_DEFECT_LENGTH = 1000;

/** Why บันทึกผลตรวจ cannot go yet; the server would refuse both (400). */
export function draftProblem(draft: InspectDraft): string | null {
  if (draft.result === null) {
    return 'เลือกผลการตรวจก่อน';
  }
  return draft.result === 'Rework' && draft.defect.trim() === '' ? 'ใส่ข้อบกพร่องก่อนบันทึก' : null;
}

export function describeDraft(draft: InspectDraft): string {
  return draft.result === 'Rework' ? `ไม่สะอาด: ${draft.defect.trim()}` : 'สะอาด';
}

/** The defect goes only with ไม่สะอาด: the server refuses one with สะอาด. */
export function requestOf(qrToken: string, scanRecordId: number, draft: InspectDraft, fix: GpsFix): RecordInspectionRequest {
  if (draft.result === 'Rework') {
    return { qrToken, scanRecordId, result: 'Rework', defect: draft.defect.trim(), ...fix };
  }
  return { qrToken, scanRecordId, result: 'Passed', ...fix };
}

/** M6 "ส่งงานล่าสุด": when, who, which round, what the Cleaner reported, and how the server placed it. */
export function submissionLine(submission: InspectionSubmission, point: PointCard): string {
  const reported = [...submission.issueTags, submission.note].filter(Boolean);
  const status =
    submission.status === 'Normal' ? 'เรียบร้อย' : reported.length > 0 ? `พบปัญหา: ${reported.join(' · ')}` : 'พบปัญหา';
  const placement = submission.placement === 'Late' ? 'ส่งช้า' : submission.placement === 'Rework' ? 'งานแก้' : null;
  return [
    formatTime(submission.submittedAt),
    submission.cleanerName,
    point.currentRound ? `รอบ ${formatRound(point.currentRound)}` : null,
    status,
    placement,
  ]
    .filter(Boolean)
    .join(' · ');
}

/** The open's own error for a location problem (inspectApi openInspection): a CUSTOM_ERROR naming it. */
export function locationProblemOf(error: unknown): LocationProblem | null {
  return statusOf(error) === 'CUSTOM_ERROR' ? (error as { error: LocationProblem }).error : null;
}
```

- [ ] **Step 7: The pieces of the page**

`frontend/src/pages/inspect/InspectHeading.tsx`:

```tsx
import type { InspectionVisit } from './inspectApi';

/** The top of M6, M16 and the failure screens: where the point is, its name, and whether this is a cover. */
export function InspectHeading({ visit }: { visit: InspectionVisit }) {
  const { point } = visit;
  return (
    <header className="inspect-heading">
      <span className="inspect-kicker">{`ตรวจงาน · ${point.areaName} · ตึก ${point.buildingCode}`}</span>
      <h1>{point.name}</h1>
      {visit.isCover && <p className="inspect-sub">{`ตรวจแทนตึก ${point.buildingCode} ในกะนี้ (Admin มอบหมาย)`}</p>}
    </header>
  );
}
```

`frontend/src/pages/inspect/DistanceLine.tsx`:

```tsx
import type { InspectionVisit } from './inspectApi';

/**
 * facility-0037: the server compared this phone's fix with the sign. Outside the radius the inspection is flagged,
 * never blocked. A sign without coordinates gives no distance, so nothing is shown.
 */
export function DistanceLine({ visit }: { visit: InspectionVisit }) {
  const accuracy = `±${Math.round(visit.accuracyM)} ม.`;
  if (visit.withinRadius === true) {
    return (
      <p className="inspect-distance">
        <span>{`ห่างป้าย ${visit.distanceM} ม. · ${accuracy}`}</span>
        <span className="inspect-radius">อยู่ในรัศมี</span>
      </p>
    );
  }
  if (visit.withinRadius === false) {
    return (
      <p className="app-note app-note--warn">
        {`ห่างป้าย ${visit.distanceM} ม. · ${accuracy} · นอกรัศมี ผลตรวจจากตรงนี้จะติดธงให้ Admin เห็น`}
      </p>
    );
  }
  return null;
}
```

`frontend/src/pages/inspect/InspectForm.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { DistanceLine } from './DistanceLine';
import { InspectHeading } from './InspectHeading';
import type { InspectionSubmission, InspectionVisit } from './inspectApi';
import { draftProblem, MAX_DEFECT_LENGTH, submissionLine, type InspectDraft } from './inspectRules';

interface InspectFormProps {
  visit: InspectionVisit;
  submission: InspectionSubmission;
  draft: InspectDraft;
  onChange: (draft: InspectDraft) => void;
  sending: boolean;
  /** Why the page opened again (a 409, decision 5). */
  notice: string | null;
  /** The server's refusal of the last press, shown as it is (facility-0022 item 4). */
  errorText: string | null;
  onSend: () => void;
}

/** Wireframe M6 (facility-0029): the latest submission of the current round, and the verdict. */
export function InspectForm({ visit, submission, draft, onChange, sending, notice, errorText, onSend }: InspectFormProps) {
  const [problem, setProblem] = useState<string | null>(null);

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const found = draftProblem(draft);
    setProblem(found);
    if (!found) {
      onSend();
    }
  };

  const alert = problem ?? errorText;
  return (
    <main className="inspect-page">
      <InspectHeading visit={visit} />
      {notice && (
        <p className="app-note app-note--warn" role="status">
          {notice}
        </p>
      )}
      <section className="app-panel">
        <h2>ส่งงานล่าสุด</h2>
        <p className="inspect-strong">{submissionLine(submission, visit.point)}</p>
      </section>
      <DistanceLine visit={visit} />
      <form className="inspect-form" onSubmit={onSubmit}>
        <fieldset className="inspect-choice">
          <legend>ผลการตรวจ</legend>
          <label>
            <input
              type="radio"
              name="inspect-result"
              disabled={sending}
              checked={draft.result === 'Passed'}
              onChange={() => onChange({ ...draft, result: 'Passed' })}
            />{' '}
            สะอาด
          </label>
          <label className="inspect-choice__rework">
            <input
              type="radio"
              name="inspect-result"
              disabled={sending}
              checked={draft.result === 'Rework'}
              onChange={() => onChange({ ...draft, result: 'Rework' })}
            />{' '}
            ไม่สะอาด
          </label>
        </fieldset>
        {draft.result === 'Rework' && (
          <div className="inspect-defect">
            <label htmlFor="inspect-defect">ข้อบกพร่อง</label>
            <textarea
              id="inspect-defect"
              rows={4}
              maxLength={MAX_DEFECT_LENGTH}
              disabled={sending}
              value={draft.defect}
              onChange={(event) => onChange({ ...draft, defect: event.target.value })}
            />
          </div>
        )}
        {alert && (
          <p className="app-note app-note--error" role="alert">
            {alert}
          </p>
        )}
        <button type="submit" className="inspect-submit" disabled={sending}>
          {sending ? 'กำลังบันทึก...' : 'บันทึกผลตรวจ'}
        </button>
      </form>
      <p className="inspect-muted">ไม่สะอาด = แม่บ้านเห็นข้อบกพร่องทันที แก้แล้วส่งงานใหม่ · ตรวจกี่ครั้งก็ได้</p>
    </main>
  );
}
```

`frontend/src/pages/inspect/InspectResult.tsx`:

```tsx
import { Link } from 'react-router';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatRound, formatTime } from '../../shared/utils/thaiTime';
import type { InspectionRecorded, InspectionVisit } from './inspectApi';
import type { InspectDraft } from './inspectRules';

interface InspectResultProps {
  visit: InspectionVisit;
  draft: InspectDraft;
  result: InspectionRecorded;
}

/** Wireframe M7 (facility-0029): the verdict is saved; ไม่สะอาด puts the defect on the Cleaner's card at once. */
export function InspectResult({ visit, draft, result }: InspectResultProps) {
  const { point } = visit;
  const cleaner = visit.submission?.cleanerName ?? 'แม่บ้าน';
  return (
    <main className="inspect-page">
      <header className="inspect-heading">
        <span className="inspect-kicker">{`ตรวจงาน · ${point.areaName} · ตึก ${point.buildingCode}`}</span>
        <h1>{`บันทึกผลตรวจแล้ว ${formatTime(result.inspectedAt)}`}</h1>
        <p className="inspect-sub">
          {[point.name, point.currentRound ? `รอบ ${formatRound(point.currentRound)}` : null].filter(Boolean).join(' · ')}
        </p>
      </header>
      {result.result === 'Passed' ? (
        <section className="app-note app-note--info">
          <p className="inspect-verdict">
            <strong>ผล: สะอาด</strong>
            <StatusChip status={result.newPointStatus} />
          </p>
        </section>
      ) : (
        <section className="app-note app-note--error">
          <p className="inspect-verdict">
            <strong>ผล: ไม่สะอาด</strong>
            <StatusChip status={result.newPointStatus} />
          </p>
          <span>{`“${draft.defect.trim()}”`}</span>
          <span>{`${cleaner}เห็นข้อบกพร่องนี้ทันทีในหน้างานของฉัน และเมื่อสแกนจุดนี้ แก้แล้วส่งงานใหม่ จากนั้นหัวหน้าตรวจอีกรอบ`}</span>
          {point.nextRound && (
            <span>{`ถ้าไม่ได้แก้จนรอบ ${formatRound(point.nextRound)} เริ่ม การ์ดขึ้นรอบใหม่ และรอบนี้บันทึกว่า "ไม่ได้แก้"`}</span>
          )}
        </section>
      )}
      {result.withinRadius === false && (
        <p className="app-note app-note--warn">{`ติดธง: ห่างป้าย ${result.distanceM} ม. Admin จะเห็นรายการนี้`}</p>
      )}
      {result.withinRadius === true && <p className="inspect-muted">{`ห่างป้าย ${result.distanceM} ม. · อยู่ในรัศมี`}</p>}
      <Link className="app-button app-button--primary" to="/my-building">
        กลับไปตึกของฉัน
      </Link>
    </main>
  );
}
```

`frontend/src/pages/inspect/NothingToInspect.tsx`:

```tsx
import { Link } from 'react-router';
import type { PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatRound, formatTime } from '../../shared/utils/thaiTime';
import { DistanceLine } from './DistanceLine';
import { InspectHeading } from './InspectHeading';
import type { InspectionVisit } from './inspectApi';

/**
 * Wireframe M16 (facility-0047 rule 7, facility-0029): no submission in the current round, or a rework still owed. The
 * server's reason is the heading; there is no inspect button. The day's round table is left out: the point carries
 * only its current and next round (plan 9 decision 7).
 */
export function NothingToInspect({ visit, notice }: { visit: InspectionVisit; notice: string | null }) {
  const { point } = visit;
  return (
    <main className="inspect-page">
      <InspectHeading visit={visit} />
      {notice && (
        <p className="app-note app-note--warn" role="status">
          {notice}
        </p>
      )}
      <section className="app-note app-note--info" aria-labelledby="inspect-nothing-title">
        <h2 id="inspect-nothing-title">{visit.refusal}</h2>
        <span>ตรวจได้เฉพาะงานที่ส่งในรอบปัจจุบัน</span>
      </section>
      <section className="app-panel">
        <h2>จุดนี้ตอนนี้</h2>
        <p className="inspect-status">
          <StatusChip status={point.status} />
          <span>{roundLine(point)}</span>
        </p>
        {point.rework && <p>{`ให้แก้ ${formatTime(point.rework.requestedAt)}: “${point.rework.defect}”`}</p>}
      </section>
      <DistanceLine visit={visit} />
      <Link className="app-button app-button--primary" to="/my-building">
        กลับไปตึกของฉัน
      </Link>
      <p className="inspect-muted">ไม่มีปุ่มตรวจจนกว่าแม่บ้านจะส่งงานในรอบนี้</p>
    </main>
  );
}

function roundLine(point: PointCard): string {
  if (point.currentRound) {
    return `รอบปัจจุบัน ${formatRound(point.currentRound)}`;
  }
  return point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : 'ตอนนี้จุดนี้ไม่มีรอบ';
}
```

- [ ] **Step 8: The page**

`frontend/src/pages/inspect/InspectPage.tsx` (Task 5 replaces it with the version that handles failures):

```tsx
import { useState } from 'react';
import { useParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/myWorkApi';
import { statusOf } from '../../shared/api/scanAnswers';
import { StatusScreen } from '../../shared/components/StatusScreen';
import { readLocation } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { InspectForm } from './InspectForm';
import { InspectResult } from './InspectResult';
import { NothingToInspect } from './NothingToInspect';
import { useOpenInspectionQuery, useRecordInspectionMutation, type InspectionRecorded, type InspectionVisit } from './inspectApi';
import { emptyDraft, requestOf, type InspectDraft } from './inspectRules';
import './inspect.css';

type Outcome =
  | { kind: 'recorded'; result: InspectionRecorded; visit: InspectionVisit }
  | { kind: 'refused'; message: string }
  | { kind: 'changed' };

/** Plan 9 decision 5: the work changed between the scan and the press, so the page opened the newest state instead. */
const CHANGED = 'งานของจุดนี้เปลี่ยนไประหว่างที่คุณตรวจ ผลยังไม่ได้บันทึก หน้านี้เปิดงานล่าสุดให้แล้ว';

/**
 * Wireframes M6, M7 and M16: the page a point's QR Sign opens for a Supervisor (/scan/<token>, facility-0029). Opening
 * scans the sign; บันทึกผลตรวจ records the verdict. The server decides what may be inspected, the flag and the block
 * (facility-0037, 0047 rule 7, 0048).
 */
export function InspectPage() {
  const { token = '' } = useParams();
  const visitQuery = useOpenInspectionQuery(token, freshOnOpen);
  const [record] = useRecordInspectionMutation();
  const [draft, setDraft] = useState<InspectDraft>(emptyDraft);
  const [sending, setSending] = useState(false);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const visit = visitQuery.data;

  const send = async () => {
    const submission = visit?.submission;
    if (!visit || !submission) {
      return;
    }
    setSending(true);
    try {
      const reading = await readLocation();
      if (reading.kind !== 'fix') {
        setOutcome({ kind: 'refused', message: 'อ่านตำแหน่งไม่ได้ ลองอีกครั้ง' });
        return;
      }
      const answer = await record(requestOf(token, submission.id, draft, reading.fix));
      if (answer.data) {
        setOutcome({ kind: 'recorded', result: answer.data, visit });
        return;
      }
      if (statusOf(answer.error) === 409) {
        // Never judge work the Supervisor was not shown: open the newest state instead
        setDraft(emptyDraft);
        setOutcome({ kind: 'changed' });
        void visitQuery.refetch();
        return;
      }
      setOutcome({ kind: 'refused', message: getErrorMessage(answer.error) });
    } finally {
      setSending(false);
    }
  };

  if (outcome?.kind === 'recorded') {
    return <InspectResult visit={outcome.visit} draft={draft} result={outcome.result} />;
  }
  // Decide only on a fresh answer: the cache may hold this sign's scan from an earlier visit
  if (visitQuery.isFetching) {
    return <p className="app-splash">กำลังอ่านตำแหน่งและเปิดงานตรวจ...</p>;
  }
  if (visitQuery.error) {
    return (
      <StatusScreen
        title={getErrorMessage(visitQuery.error)}
        detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้ง Admin"
        linkTo="/my-building"
        linkText="กลับไปตึกของฉัน"
      />
    );
  }
  if (!visit) {
    return <p className="app-splash">กำลังอ่านตำแหน่งและเปิดงานตรวจ...</p>;
  }
  const notice = outcome?.kind === 'changed' ? CHANGED : null;
  if (!visit.submission) {
    return <NothingToInspect visit={visit} notice={notice} />;
  }
  return (
    <InspectForm
      visit={visit}
      submission={visit.submission}
      draft={draft}
      onChange={setDraft}
      sending={sending}
      notice={notice}
      errorText={outcome?.kind === 'refused' ? outcome.message : null}
      onSend={() => void send()}
    />
  );
}
```

`frontend/src/pages/inspect/index.ts`:

```ts
export { InspectPage } from './InspectPage';
```

- [ ] **Step 9: The styles**

`frontend/src/pages/inspect/inspect.css` (colours from `SupervisorInspect.dc.html`, through the tokens):

```css
/* Inspection at a point's sign, wireframes M6, M7 and M16. Class prefix: inspect- */

.inspect-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.inspect-heading {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.inspect-heading h1 {
  font-size: 26px;
  font-weight: 700;
}

.inspect-kicker {
  font-size: 13px;
  letter-spacing: 0.04em;
  color: var(--color-muted);
}

.inspect-sub {
  font-size: 15px;
  color: var(--color-muted);
}

.inspect-strong {
  font-size: 16px;
  font-weight: 600;
}

.inspect-muted {
  font-size: 13px;
  color: var(--color-muted);
}

.inspect-distance {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  font-size: 14px;
  color: var(--color-muted);
}

.inspect-radius {
  padding: 2px 10px;
  border-radius: 999px;
  background: var(--color-status-passed);
  color: var(--color-on-primary);
  font-size: 13px;
  font-weight: 600;
  white-space: nowrap;
}

.inspect-form {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.inspect-choice {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
  margin: 0;
  padding: 0;
  border: 0;
}

.inspect-choice legend {
  margin-bottom: 8px;
  font-size: 15px;
  font-weight: 600;
}

.inspect-choice label {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 56px;
  background: var(--color-surface);
  border: 1px solid var(--color-input-border);
  border-radius: var(--radius-md);
  font-size: 17px;
  font-weight: 600;
}

.inspect-choice label:has(input:checked) {
  border: 2px solid var(--color-primary);
  background: var(--color-info-bg);
  color: var(--color-primary);
}

.inspect-choice .inspect-choice__rework:has(input:checked) {
  border-color: var(--color-status-rework);
  background: var(--color-error-bg);
  color: var(--color-error-text);
}

.inspect-defect {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.inspect-defect label {
  font-size: 14px;
  font-weight: 600;
}

.inspect-defect textarea {
  font: inherit;
  font-size: 15px;
  padding: 8px 10px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  resize: none;
}

.inspect-submit {
  min-height: 72px;
  border: 0;
  border-radius: var(--radius-lg);
  background: var(--color-primary);
  color: var(--color-on-primary);
  font: inherit;
  font-size: 22px;
  font-weight: 700;
  cursor: pointer;
}

.inspect-submit:disabled {
  opacity: 0.6;
  cursor: default;
}

.inspect-status,
.inspect-verdict {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}
```

- [ ] **Step 10: Run the tests, the typecheck and the lint**

```bash
npm test          # expect: 142 passed (128 + 14; the ScanPage pin is replaced, not added)
npx tsc -b        # expect: no output
npm run lint      # expect: no output
```

- [ ] **Step 11: Handoff entry**

```markdown
- <date> · Task 4 done: /scan/:token opens by role (shared ByRole): the Cleaner's form, the Supervisor's inspection, no-permission for an Admin. Inspection (M6, M7, M16): opening reads a fresh fix and scans; the latest submission with distance; no verdict chosen; ไม่สะอาด needs a defect (sent trimmed, only with ไม่สะอาด); saved result with what the Cleaner sees; nothing to inspect or rework owed with the server's reason; 409 opens the newest work and clears the verdict; one send at a time. Frontend tests 142 passed. Next: Task 5 (inspection failures).
```

- [ ] **Step 12: Commit**

```bash
cd ..
git add frontend/src docs/handoff.md
git commit -m "feat(frontend): a point's sign opens the inspection for Supervisors: latest submission, สะอาด / ไม่สะอาด with defect, nothing to inspect, new work since the scan (facility-0029, 0047)"
```

---

### Task 5: When the inspection cannot go ahead (M2, M8, M12 for the Supervisor)

Location off or plain http, another building or the wrong shift, and a scan or a verdict that does not reach the server.

**Files:**
- Modify: `frontend/src/shared/components/BlockedScanScreen.tsx`, `frontend/src/test/supervisorFixtures.ts`
- Create: `frontend/src/pages/inspect/InspectSendFailed.tsx`
- Modify (replace whole): `frontend/src/pages/inspect/InspectPage.tsx`
- Test: `frontend/src/pages/inspect/InspectPage.test.tsx` (add a `describe`)
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes:
  - Plan 8: `LocationHelp({ problem, action, busy, onRetry })`, `blockedScanOf(error)`, `isNotSent(error)`, `BlockedScan`, `SEND_TIMEOUT_MS`, `formatTime`.
  - Task 4: `InspectHeading`, `InspectionVisit`, `InspectDraft`, `describeDraft` and `locationProblemOf`.
- Produces:
  - `BlockedScanScreen` accepts `sign="inspect"`. Its advice is a building cover, and its link is "กลับไปตึกของฉัน" → `/my-building`.
  - `<InspectSendFailed visit draft triedAt sending onResend />`.
  - Fixtures `otherBuildingBlocked` and `outsideShiftBlocked`.

- [ ] **Step 1: Blocked fixtures**

In `frontend/src/test/supervisorFixtures.ts`, change the import `import type { LastScan, PointCard } from '../shared/api/types';` to:

```ts
import type { BlockedScan, LastScan, PointCard } from '../shared/api/types';
```

and append at the end of the file:

```ts
/** 403 from either inspection call: the sign is in another building (facility-0048). */
export const otherBuildingBlocked: BlockedScan = {
  message: 'จุดนี้ไม่ได้อยู่ในตึกของคุณ',
  areaCode: 'BR01',
  areaName: 'Area ตึกอื่น',
  reason: 'OtherBuilding',
  buildingCode: 'B',
};

/** 403 from either inspection call: the own building, but not the own shift by the clock (facility-0048 amendment). */
export const outsideShiftBlocked: BlockedScan = {
  message: 'ตอนนี้ไม่ใช่กะของคุณ ตรวจได้เฉพาะในกะของตัวเอง',
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  reason: 'OutsideShift',
  buildingCode: 'A',
};
```

- [ ] **Step 2: Write the failing tests**

In `frontend/src/pages/inspect/InspectPage.test.tsx`:

1. Change the vitest import to `import { describe, expect, it, vi } from 'vitest';`.
2. Add `import { SEND_TIMEOUT_MS } from '../../shared/api/baseApi';` below it.
3. Add `otherBuildingBlocked` and `outsideShiftBlocked` to the `supervisorFixtures` import.
4. Append at the end of the file:

```tsx
describe('InspectPage when something stands in the way (M2, M8, M12)', () => {
  it('Location off when the sign opens: the help, and the retry scans with a new fix (M2, facility-0036)', async () => {
    const gps = fakeGps('denied', nearSign);
    const server = serve();
    const { user } = await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'เปิด Location ก่อนตรวจงาน' })).toBeInTheDocument();
    expect(server.calls.some((c) => c.path === '/api/inspections/scan')).toBe(false);

    await user.click(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    expect(await screen.findByRole('heading', { name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(gps.calls).toBe(2);
    expect(server.calls.filter((c) => c.path === '/api/inspections/scan')).toHaveLength(1);
  });

  it('a page on plain http cannot read a location, and there is nothing to retry (facility-0049)', async () => {
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });
    serve();
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'อ่านตำแหน่งไม่ได้' })).toBeInTheDocument();
    expect(
      screen.getByText('หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง ยังไม่ได้ตรวจงาน แจ้ง Admin'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ลองอีกครั้ง' })).not.toBeInTheDocument();
  });

  it('another building is blocked as the sign opens, with the cover advice (M8, facility-0048)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/inspections/scan': () => ({ status: 403, body: otherBuildingBlocked }) });
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'จุดนี้ไม่ได้อยู่ในตึกของคุณ' })).toBeInTheDocument();
    expect(screen.getByText('ไม่ได้ตรวจงาน')).toBeInTheDocument();
    expect(screen.getByText('Area ตึกอื่น (BR01) · ตึก B')).toBeInTheDocument();
    expect(
      screen.getByText(
        'Admin เห็นการสแกนครั้งนี้แล้ว ให้แจ้ง Admin ว่าคุณตรวจแทนตึก B ในกะนี้ เมื่อ Admin กดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ',
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับไปตึกของฉัน' })).toHaveAttribute('href', '/my-building');
  });

  it('the own shift ending before the press blocks the press (M8)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/inspections': () => ({ status: 403, body: outsideShiftBlocked }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(
      await screen.findByRole('heading', { name: 'ตอนนี้ไม่ใช่กะของคุณ ตรวจได้เฉพาะในกะของตัวเอง' }),
    ).toBeInTheDocument();
    expect(screen.getByText('ไม่ได้ตรวจงาน')).toBeInTheDocument();
  });

  it('no signal when the sign opens: ลองอีกครั้ง scans again', async () => {
    fakeGps(nearSign);
    let scans = 0;
    serve({
      'POST /api/inspections/scan': () => {
        scans += 1;
        if (scans === 1) {
          throw new TypeError('Failed to fetch');
        }
        return { status: 200, body: inspectionScan() };
      },
    });
    const { user } = await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'เปิดงานตรวจไม่ได้' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));

    expect(await screen.findByRole('heading', { name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(scans).toBe(2);
  });

  it('no signal on บันทึกผลตรวจ keeps the verdict, and ส่งใหม่ sends it again with a fresh location (facility-0052)', async () => {
    const gps = fakeGps(nearSign);
    let tries = 0;
    const server = serve({
      'POST /api/inspections': () => {
        tries += 1;
        if (tries === 1) {
          throw new TypeError('Failed to fetch');
        }
        return { status: 201, body: recorded({ result: 'Rework', newPointStatus: 'Rework' }) };
      },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'ไม่สะอาด' }));
    await user.type(screen.getByLabelText('ข้อบกพร่อง'), 'พื้นเปียก');
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึกผลตรวจ' })).toBeInTheDocument();
    expect(screen.getByText('ไม่สะอาด: พื้นเปียก')).toBeInTheDocument();
    expect(gps.calls).toBe(2);

    await user.click(screen.getByRole('button', { name: 'ส่งใหม่' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect(gps.calls).toBe(3);
    const sends = server.calls.filter((c) => c.path === '/api/inspections');
    expect(sends).toHaveLength(2);
    expect(sends[1].body).toEqual(sends[0].body);
  });

  it('a server fault (503) is not saved either', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/inspections': () => ({ status: 503 }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึกผลตรวจ' })).toBeInTheDocument();
    expect(screen.getByText('สะอาด')).toBeInTheDocument();
  });

  it('a press with no answer ends at 20 s as not saved (facility-0052)', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/inspections': () => new Promise<never>(() => {}) });
    const { user } = await renderApp(SIGN);
    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    const send = screen.getByRole('button', { name: 'บันทึกผลตรวจ' });

    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      await user.click(send);
      await vi.waitFor(() => expect(server.calls.some((c) => c.path === '/api/inspections')).toBe(true));
      await vi.advanceTimersByTimeAsync(SEND_TIMEOUT_MS - 1_000);
      expect(screen.getByRole('button', { name: 'กำลังบันทึก...' })).toBeDisabled();

      await vi.advanceTimersByTimeAsync(1_000);

      expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึกผลตรวจ' })).toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
  });

  it('Location off on บันทึกผลตรวจ: the help, and the retry records the same verdict (M2)', async () => {
    fakeGps(nearSign, 'denied', nearSign);
    const server = serve({ 'POST /api/inspections': () => ({ status: 201, body: recorded() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'เปิด Location ก่อนบันทึกผลตรวจ' })).toBeInTheDocument();
    expect(server.calls.some((c) => c.path === '/api/inspections')).toBe(false);

    await user.click(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect((server.calls.find((c) => c.path === '/api/inspections')?.body as { result: string }).result).toBe('Passed');
  });
});
```

- [ ] **Step 3: Run the tests to make sure they fail**

Run: `npm test -- src/pages/inspect`
Expected: FAIL, the 9 new tests:
- Location problems and no signal on open show the generic StatusScreen.
- Blocked answers show a StatusScreen without "ไม่ได้ตรวจงาน".
- Press failures show the form's alert.

The 14 Task-4 tests still pass.

- [ ] **Step 4: The Supervisor's wording on the blocked screen**

Replace the whole of `frontend/src/shared/components/BlockedScanScreen.tsx` with:

```tsx
import { Link } from 'react-router';
import type { BlockedScan } from '../api/types';
import './shared.css';

interface BlockedScanScreenProps {
  blocked: BlockedScan;
  /**
   * check-in: a Cleaner at another Area's check-in sign; a cover never moves where they check in (facility-0041
   * item 4). point: a Cleaner at another Area's point. inspect: a Supervisor at a point of another building, or
   * outside their own shift (facility-0048).
   */
  sign: 'check-in' | 'point' | 'inspect';
  ownAreaName?: string;
}

const notSaved: Record<BlockedScanScreenProps['sign'], string> = {
  'check-in': 'ไม่ได้บันทึกเวลา',
  point: 'ไม่ได้บันทึกงาน',
  inspect: 'ไม่ได้ตรวจงาน',
};

/** Wireframe M8 (facility-0041, 0048): nothing was recorded; the Admin already sees this Blocked Scan and can assign a cover. */
export function BlockedScanScreen({ blocked, sign, ownAreaName }: BlockedScanScreenProps) {
  return (
    <main className="app-help">
      <span className="app-help__kicker">{sign === 'check-in' ? 'ป้ายลงเวลา' : 'ป้ายจุดทำความสะอาด'}</span>
      <h1 className="app-help__title">{blocked.message}</h1>
      <p className="app-note app-note--error">{notSaved[sign]}</p>
      <dl className="app-facts">
        <dt>ป้ายที่สแกน</dt>
        <dd>{`${blocked.areaName} (${blocked.areaCode}) · ตึก ${blocked.buildingCode}`}</dd>
        {ownAreaName && (
          <>
            <dt>Area ของคุณ</dt>
            <dd>{ownAreaName}</dd>
          </>
        )}
      </dl>
      {sign === 'check-in' && (
        <p>ลงเวลาที่ป้ายของ Area ตัวเองเท่านั้น วันที่ทำแทน Area อื่นก็ลงเวลาที่ป้ายของตัวเองตามปกติ Admin เห็นการสแกนครั้งนี้แล้ว</p>
      )}
      {sign === 'point' && (
        <section className="app-panel">
          <h2>ถ้าได้รับมอบหมายให้ทำแทน</h2>
          <p>
            {`Admin เห็นการสแกนครั้งนี้แล้ว ให้แจ้ง Admin ว่าคุณทำแทน ${blocked.areaName} เมื่อ Admin กดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ`}
          </p>
        </section>
      )}
      {sign === 'inspect' && (
        <section className="app-panel">
          <h2>ถ้าได้รับมอบหมายให้ตรวจแทน</h2>
          <p>
            {`Admin เห็นการสแกนครั้งนี้แล้ว ให้แจ้ง Admin ว่าคุณตรวจแทนตึก ${blocked.buildingCode} ในกะนี้ เมื่อ Admin กดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ`}
          </p>
        </section>
      )}
      {sign === 'inspect' ? (
        <Link className="app-button app-button--primary" to="/my-building">
          กลับไปตึกของฉัน
        </Link>
      ) : (
        <Link className="app-button app-button--primary" to="/my-work">
          กลับไปงานของฉัน
        </Link>
      )}
    </main>
  );
}
```

- [ ] **Step 5: The "not saved" screen**

`frontend/src/pages/inspect/InspectSendFailed.tsx`:

```tsx
import { formatTime } from '../../shared/utils/thaiTime';
import { InspectHeading } from './InspectHeading';
import type { InspectionVisit } from './inspectApi';
import { describeDraft, type InspectDraft } from './inspectRules';

interface InspectSendFailedProps {
  visit: InspectionVisit;
  draft: InspectDraft;
  triedAt: Date;
  sending: boolean;
  onResend: () => void;
}

/**
 * facility-0052's rule for the Supervisor too (wireframe M12's layout): nothing waits on the phone to send later. The
 * verdict stays on this page; the person walks to where there is signal and presses ส่งใหม่.
 */
export function InspectSendFailed({ visit, draft, triedAt, sending, onResend }: InspectSendFailedProps) {
  return (
    <main className="inspect-page">
      <InspectHeading visit={visit} />
      <section className="app-note app-note--error" aria-labelledby="inspect-failed-title">
        <h2 id="inspect-failed-title">ยังไม่ได้บันทึกผลตรวจ</h2>
        <span>{`บันทึกไม่สำเร็จ เพราะไม่มีสัญญาณหรือเซิร์ฟเวอร์ไม่ตอบ · ลองล่าสุด ${formatTime(triedAt)}`}</span>
      </section>
      <section className="app-panel">
        <h2>ผลที่คุณเลือกไว้ (ยังอยู่ในหน้านี้)</h2>
        <p>{describeDraft(draft)}</p>
      </section>
      <section className="app-panel">
        <h2>ทำอย่างไรต่อ</h2>
        <ol>
          <li>อย่าปิดหน้านี้</li>
          <li>เดินไปตรงที่มีสัญญาณ</li>
          <li>กด "ส่งใหม่"</li>
        </ol>
        <p className="inspect-muted">ระบบไม่เก็บผลตรวจไว้ในมือถือแล้วส่งเองทีหลัง</p>
      </section>
      <button type="button" className="inspect-submit" disabled={sending} onClick={onResend}>
        {sending ? 'กำลังส่ง...' : 'ส่งใหม่'}
      </button>
    </main>
  );
}
```

- [ ] **Step 6: The page with its failures**

Replace the whole of `frontend/src/pages/inspect/InspectPage.tsx` with:

```tsx
import { useState } from 'react';
import { useParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/myWorkApi';
import { blockedScanOf, isNotSent, statusOf } from '../../shared/api/scanAnswers';
import type { BlockedScan } from '../../shared/api/types';
import { BlockedScanScreen } from '../../shared/components/BlockedScanScreen';
import { LocationHelp } from '../../shared/components/LocationHelp';
import { StatusScreen } from '../../shared/components/StatusScreen';
import { readLocation, type LocationProblem } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { InspectForm } from './InspectForm';
import { InspectResult } from './InspectResult';
import { InspectSendFailed } from './InspectSendFailed';
import { NothingToInspect } from './NothingToInspect';
import { useOpenInspectionQuery, useRecordInspectionMutation, type InspectionRecorded, type InspectionVisit } from './inspectApi';
import { emptyDraft, locationProblemOf, requestOf, type InspectDraft } from './inspectRules';
import './inspect.css';

type Outcome =
  | { kind: 'recorded'; result: InspectionRecorded; visit: InspectionVisit }
  | { kind: 'refused'; message: string }
  | { kind: 'changed' }
  | { kind: 'location'; problem: LocationProblem }
  | { kind: 'blocked'; blocked: BlockedScan }
  | { kind: 'not-sent'; triedAt: Date };

/** Plan 9 decision 5: the work changed between the scan and the press, so the page opened the newest state instead. */
const CHANGED = 'งานของจุดนี้เปลี่ยนไประหว่างที่คุณตรวจ ผลยังไม่ได้บันทึก หน้านี้เปิดงานล่าสุดให้แล้ว';

/**
 * Wireframes M2, M6, M7, M8, M12 and M16: the page a point's QR Sign opens for a Supervisor (/scan/<token>,
 * facility-0029). Opening scans the sign; บันทึกผลตรวจ records the verdict. The server decides what may be inspected,
 * the flag and the block (facility-0037, 0047 rule 7, 0048). A press that does not go through keeps the verdict for
 * ส่งใหม่ (facility-0052).
 */
export function InspectPage() {
  const { token = '' } = useParams();
  const visitQuery = useOpenInspectionQuery(token, freshOnOpen);
  const [record] = useRecordInspectionMutation();
  const [draft, setDraft] = useState<InspectDraft>(emptyDraft);
  const [sending, setSending] = useState(false);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const visit = visitQuery.data;

  const send = async () => {
    const submission = visit?.submission;
    if (!visit || !submission) {
      return;
    }
    setSending(true);
    try {
      const reading = await readLocation();
      if (reading.kind !== 'fix') {
        setOutcome({ kind: 'location', problem: reading.kind });
        return;
      }
      const triedAt = new Date();
      const answer = await record(requestOf(token, submission.id, draft, reading.fix));
      if (statusOf(answer.error) === 409) {
        // Never judge work the Supervisor was not shown: open the newest state instead
        setDraft(emptyDraft);
        setOutcome({ kind: 'changed' });
        void visitQuery.refetch();
        return;
      }
      setOutcome(outcomeOf(answer, visit, triedAt));
    } finally {
      setSending(false);
    }
  };

  if (outcome?.kind === 'recorded') {
    return <InspectResult visit={outcome.visit} draft={draft} result={outcome.result} />;
  }
  if (outcome?.kind === 'blocked') {
    return <BlockedScanScreen blocked={outcome.blocked} sign="inspect" />;
  }
  if (outcome?.kind === 'location') {
    return <LocationHelp problem={outcome.problem} action="บันทึกผลตรวจ" busy={sending} onRetry={() => void send()} />;
  }
  if (outcome?.kind === 'not-sent' && visit) {
    return (
      <InspectSendFailed visit={visit} draft={draft} triedAt={outcome.triedAt} sending={sending} onResend={() => void send()} />
    );
  }
  // Decide only on a fresh answer: the cache may hold this sign's scan from an earlier visit
  if (visitQuery.isFetching || (!visit && !visitQuery.error)) {
    return <p className="app-splash">กำลังอ่านตำแหน่งและเปิดงานตรวจ...</p>;
  }
  if (visitQuery.error || !visit) {
    return <OpenFailed error={visitQuery.error} onRetry={() => void visitQuery.refetch()} />;
  }
  const notice = outcome?.kind === 'changed' ? CHANGED : null;
  if (!visit.submission) {
    return <NothingToInspect visit={visit} notice={notice} />;
  }
  return (
    <InspectForm
      visit={visit}
      submission={visit.submission}
      draft={draft}
      onChange={setDraft}
      sending={sending}
      notice={notice}
      errorText={outcome?.kind === 'refused' ? outcome.message : null}
      onSend={() => void send()}
    />
  );
}

/** Why the sign did not open: Location (M2), a Blocked Scan the server just recorded (M8), no answer, or a refusal. */
function OpenFailed({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const problem = locationProblemOf(error);
  if (problem) {
    return <LocationHelp problem={problem} action="ตรวจงาน" busy={false} onRetry={onRetry} />;
  }
  const blocked = blockedScanOf(error);
  if (blocked) {
    return <BlockedScanScreen blocked={blocked} sign="inspect" />;
  }
  if (isNotSent(error)) {
    return (
      <main className="app-help">
        <h1 className="app-help__title">เปิดงานตรวจไม่ได้</h1>
        <p className="app-note app-note--error">เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ยังไม่ได้ตรวจงาน เดินไปตรงที่มีสัญญาณ แล้วกดลองอีกครั้ง</p>
        <button type="button" className="app-button app-button--primary" onClick={onRetry}>
          ลองอีกครั้ง
        </button>
      </main>
    );
  }
  return (
    <StatusScreen
      title={getErrorMessage(error)}
      detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้ง Admin"
      linkTo="/my-building"
      linkText="กลับไปตึกของฉัน"
    />
  );
}

function outcomeOf(answer: { data?: InspectionRecorded; error?: unknown }, visit: InspectionVisit, triedAt: Date): Outcome {
  if (answer.data) {
    return { kind: 'recorded', result: answer.data, visit };
  }
  const blocked = blockedScanOf(answer.error);
  if (blocked) {
    return { kind: 'blocked', blocked };
  }
  if (isNotSent(answer.error)) {
    return { kind: 'not-sent', triedAt };
  }
  return { kind: 'refused', message: getErrorMessage(answer.error) };
}
```

- [ ] **Step 7: Run the tests, the typecheck and the lint**

```bash
npm test          # expect: 151 passed (142 + 9)
npx tsc -b        # expect: no output
npm run lint      # expect: no output
```

The plan-8 blocked-screen tests (check-in and point, "กลับไปงานของฉัน") must still pass unchanged.

- [ ] **Step 8: Handoff entry**

```markdown
- <date> · Task 5 done: the inspection page's failures: Location off or plain http, on opening ("ตรวจงาน") and on the press ("บันทึกผลตรวจ"), with the retry repeating the same step; another building or the wrong shift shows the blocked screen in the Supervisor's wording (building cover advice, back to My Building), on opening or on the press; no signal, 20 s with no answer or a 5xx: "เปิดงานตรวจไม่ได้" with ลองอีกครั้ง on opening, "ยังไม่ได้บันทึกผลตรวจ" with the verdict kept and ส่งใหม่ (fresh fix) on the press. Frontend tests 151 passed. Next: Task 6 (docs, final checks, the controller's real run).
```

- [ ] **Step 9: Commit**

```bash
cd ..
git add frontend/src docs/handoff.md
git commit -m "feat(frontend): inspection page failures: Location help, blocked building or shift, not saved with resend (facility-0036, 0048, 0052)"
```

---

### Task 6: Docs, ADR amendments and final checks

**Files:**
- Modify (amendment appended at the end of each): `docs/adr/facility-0023-auth-in-base-query-and-signalr.md`, `facility-0025-frontend-target-tree.md`, `facility-0026-frontend-router-shape.md`, `facility-0029-supervisor-inspection-workflow.md`, `facility-0048-supervisor-assigned-to-building.md`, `facility-0057-supervisor-building-page-and-audit-log-page.md`, `facility-0062-phone-bundle-budget-200kb-gzip.md`, `facility-0064-dashboard-hub-offline-keeps-todays-behaviour.md`
- Modify: `README.md`, `frontend/README.md`, `docs/architecture.md`, `docs/handoff.md`

**Interfaces:** none. Docs only, plus a full check.

- [ ] **Step 1: Measure the bundle**

```bash
cd frontend && npm run build && cd ..
```

Note the gzip size of `dist/assets/index-*.js` from Vite's output. It must be ≤ 200 kB. It goes into the 0062 amendment.

- [ ] **Step 2: ADR amendments**

Append each block to the end of its ADR, after a blank line. The ADRs are in Thai, so the amendments are too. Use the editor tools. Write the date you commit in place of `<วันที่>` (format `2026-10-15`).

`facility-0023-auth-in-base-query-and-signalr.md`:

```markdown
> **แก้ไข <วันที่> (แผน 9):** `getValidAccessToken(getState, dispatch)` อยู่ใน `shared/auth/session.ts` ใช้ refresh ตัวเดียวกับ REST (single-flight ตาม facility-0015) ถ้า token เหลือไม่เกิน 30 วินาทีจะ refresh ก่อนแล้วคืน token ใหม่ ถ้า refresh ถูกปฏิเสธจะคืนค่าว่าง session จบ cache ถูกล้าง และ hub หยุดเอง SignalR เรียกฟังก์ชันนี้ทุกครั้งที่ต่อและต่อใหม่ ผ่าน `followScanHub` ใน `shared/api/scanHub.ts`
```

`facility-0025-frontend-target-tree.md`:

```markdown
> **แก้ไข <วันที่> (แผน 9):** เพิ่มหน้า `pages/my-building/` (M15) และ `pages/inspect/` (M6, M7, M16) ของที่ใช้ร่วมกันเพิ่มใน `shared/`: `shared/api/scanHub.ts` (`followScanHub` เปิด SignalR หนึ่ง connection ต่อ cache entry ใน `onCacheEntryAdded` ตาม #26 ใช้ทั้งหน้าตึกของฉันและ Dashboard ของแผน 11) และ `shared/components/ByRole.tsx` (ที่อยู่เดียว หน้าตามบทบาท) state ของการเชื่อมต่อ hub อยู่ที่ `store/slices/hubSlice.ts` ฝั่ง test เพิ่ม `src/test/fakeSignalR.ts` ที่แทน package `@microsoft/signalr` ทุก test ผ่าน `test.alias` ใน `vite.config.ts` (ตาข่าย #32 fake แค่ fetch กับ SignalR) และ `src/test/supervisorFixtures.ts`
```

`facility-0026-frontend-router-shape.md`:

```markdown
> **แก้ไข <วันที่> (แผน 9):** `/scan/:token` เปิดได้ทั้งแม่บ้านและหัวหน้า `ByRole` เลือกหน้าตามบทบาท แม่บ้านได้หน้าส่งงาน หัวหน้าได้หน้าตรวจงาน (facility-0029 ข้อ 1) Admin เห็นจอไม่มีสิทธิ์ หน้าห้าม import หน้าอื่น ตาราง route จึงระบุทั้งสองหน้าและ `ByRole` เลือก `/check-in/:token` ยังเปิดได้เฉพาะแม่บ้าน
```

`facility-0029-supervisor-inspection-workflow.md`:

```markdown
> **แก้ไข <วันที่> (แผน 9):** หน้าตรวจงานบนมือถือ: เปิดป้ายแล้วอ่าน GPS ใหม่และสแกนทันที แสดงงานที่ส่งล่าสุดของรอบปัจจุบันพร้อมระยะจากป้าย ยังไม่เลือกผลไว้ก่อน (กันกดผ่านโดยไม่ได้ดู) เลือก "ไม่สะอาด" ต้องกรอกข้อบกพร่อง (ไม่เกิน 1000 ตัวอักษร) และส่งข้อบกพร่องเฉพาะเมื่อไม่สะอาด กด "บันทึกผลตรวจ" อ่าน GPS ใหม่อีกครั้ง ถ้าเซิร์ฟเวอร์ตอบ 409 (มีงานใหม่หลังสแกน หรือไม่มีงานให้ตรวจแล้ว) หน้าสแกนใหม่ ล้างผลที่เลือก และบอกเหตุผล ถ้าส่งไม่ถึงเซิร์ฟเวอร์ ขึ้น "ยังไม่ได้บันทึกผลตรวจ" พร้อมผลที่เลือกไว้และปุ่ม "ส่งใหม่" ยอมรับความเสี่ยงว่าถ้าคำขอถึงเซิร์ฟเวอร์แล้วแต่คำตอบหาย ส่งใหม่แบบสะอาดจะได้ Inspection Record ที่สอง
```

`facility-0048-supervisor-assigned-to-building.md`:

```markdown
> **แก้ไข <วันที่> (แผน 9):** หัวหน้าถูกบล็อกตั้งแต่เปิดป้าย ไม่ใช่ตอนกดบันทึก เพราะ `POST /api/inspections/scan` ที่หน้าเรียกตอนเปิดเป็นตัวที่บันทึก Blocked Scan ของหัวหน้า หน้า M8 ของหัวหน้าแนะนำให้แจ้ง Admin มอบหมายตรวจแทนตึกนั้นในกะนี้ แล้วสแกนป้ายใหม่ และพากลับหน้าตึกของฉัน ถ้ากะของหัวหน้าจบระหว่างที่หน้าเปิดอยู่ การกดบันทึกก็ถูกบล็อกแบบเดียวกัน
```

`facility-0057-supervisor-building-page-and-audit-log-page.md`:

```markdown
> **แก้ไข <วันที่> (แผน 9):** หน้าตึกของฉัน (`/my-building`) "อัปเดตเอง" ผ่าน hub: `ScanRecorded` และ `InspectionRecorded` ส่งการ์ดทั้งใบ หน้าแทนการ์ดใบนั้นใน cache แล้วเรียงใหม่บนมือถือตามกติกาเดียวกับเซิร์ฟเวอร์ (รอตรวจตามเวลาส่ง ต้องแก้ไขตามเวลาที่ให้แก้ ที่เหลือตาม Area) เมื่อ hub ต่อกลับได้ หน้าถามเซิร์ฟเวอร์ใหม่หนึ่งครั้ง ไม่มี poll 30 วินาที (facility-0061 ให้ poll เฉพาะจอ Admin) การเปลี่ยนตามเวลา เช่น รอบใหม่เริ่ม เห็นเมื่อเปิดหน้าใหม่หรือกด "อัปเดต" ท้ายหน้าบอกว่าอัปเดตสดอยู่หรือหลุด การ์ดไม่มีลิงก์ไปหน้าสแกน (facility-0059) ไม่มีข้อมูลว่าใครให้แก้ จึงเขียนว่า "ให้แก้" แทน "คุณให้แก้"
```

`facility-0062-phone-bundle-budget-200kb-gzip.md` (put in the size from Step 1):

```markdown
> **วัด <วันที่> (แผน 9):** JavaScript ไฟล์เดียว <ขนาดจาก Step 1> kB gzip หลังเพิ่มหน้าตึกของฉันและหน้าตรวจงานของหัวหน้า และ import SignalR ครั้งแรก (ยังไม่มี Base UI) ยังอยู่ในงบ 200 kB
```

`facility-0064-dashboard-hub-offline-keeps-todays-behaviour.md`:

```markdown
> **แก้ไข <วันที่> (แผน 9):** ข้อ 1 และ 2 ทำไว้ที่ `shared/api/scanHub.ts` (`followScanHub`) ให้ทุกหน้าที่ใช้ hub: `withAutomaticReconnect([0, 2000, 5000, 10000, 30000])`, ต่อกลับได้ (`onreconnected`) หรือเริ่มใหม่สำเร็จหลัง `onclose` แล้วหน้าถามเซิร์ฟเวอร์ใหม่หนึ่งครั้ง, `onclose` ลองเริ่มใหม่ทุก 3 วินาทีจนกว่า cache entry ถูกลบ state ของการเชื่อมต่อเก็บใน `store/slices/hubSlice.ts` (connecting, live, reconnecting) ที่ใช้ร่วมกัน ไม่ใช่ `dashboardSlice` หน้าตึกของฉันของหัวหน้าใช้ก่อน (แผน 9) ป้ายของ Dashboard ในแผน 11 อ่านจาก slice เดียวกัน
```

- [ ] **Step 3: `README.md`**

In the section `## 📱 การเข้าใช้งานระบบบนอุปกรณ์จริง`:

1. In the bullet that starts `` - `http://<MAC_LAN_IP>:5173/login` ``, replace the sentence ` หน้าตึกของฉันยังขึ้นว่า "หน้านี้กำลังสร้าง" จนกว่าแผน 9 จะเสร็จ` with ` หน้าตึกของฉันอัปเดตเองเมื่อแม่บ้านส่งงานหรือหัวหน้าตรวจ`.
2. In the bullet that starts `` - ป้ายลงเวลาของ Area เปิด `/check-in/<token>` ``, replace `(แม่บ้านเท่านั้น หัวหน้ามากับแผน 9)` with `(แม่บ้านเห็นหน้าส่งงาน หัวหน้าเห็นหน้าตรวจงาน)`.

- [ ] **Step 4: `frontend/README.md`**

1. In the code block under `## Where things go (facility-0025)`:
   - Replace the line that starts `src/store/` with:

     ```
     src/store/                               the store, authSlice, hubSlice (scan hub live or not), typed hooks
     ```

   - Replace the line that starts `src/shared/api/` with:

     ```
     src/shared/api/                          baseApi (RTK Query), server types, myWorkApi, scanHub (SignalR into the cache)
     ```

   - Replace the line that starts `src/test/` with:

     ```
     src/test/                                fakeServer (fake fetch), fakeGps, fakeSignalR, fixtures, renderApp
     ```

2. Under `## Session`, append:

```markdown
SignalR gets its token from `getValidAccessToken`, which refreshes first when 30 s or less is left, on every connect and reconnect. A page that wants live updates opens the hub with `followScanHub` inside its query's `onCacheEntryAdded` (one connection per cache entry; see `pages/my-building/myBuildingApi.ts`).
```

3. Under `## Tests`, append:

```markdown
The `@microsoft/signalr` package is replaced in every test by `src/test/fakeSignalR.ts` (`test.alias` in `vite.config.ts`). Use `hub.latest()` to see the connection the app built and to play the server: `push('ScanRecorded', card)`, `reconnecting()`, `reconnected()`, `close()`; wrap pushes in `act(...)`. Made-up Supervisor answers are in `src/test/supervisorFixtures.ts`. The server leaves out null fields, so fixtures leave them out too.
```

- [ ] **Step 5: `docs/architecture.md`**

1. In section 2, in the `Pages[...]` node, replace `scan (M4, M5, M8, M11–M13) · my-building · dashboard` with `scan (M4, M5, M8, M11–M13) · inspect (M6, M7, M16) · my-building (M15) · dashboard`.
2. In section 2, replace `Store["store/<br/>authSlice: user + access token in memory"]` with `Store["store/<br/>authSlice: user + access token in memory<br/>hubSlice: scan hub live or not"]`.
3. In section 2, below the `Guard[...]` node line, add:

```
        HubF["shared/api/scanHub.ts<br/>followScanHub: one SignalR connection per cache entry<br/>token from getValidAccessToken"]
```

4. In section 2, below the line `    BaseApi -->|"/api/auth/login and /api/*<br/>Authorization: Bearer"| Ep`, add:

```
    Pages -->|"my-building: onCacheEntryAdded"| HubF
    HubF --> Session
    HubF -->|"/hubs/scan?access_token=<br/>ScanRecorded, InspectionRecorded"| Hub
```

- [ ] **Step 6: `docs/handoff.md`**

1. Add a row to the Status table:

```markdown
| 9 | Supervisor phone: My Building with live updates, inspection at the point's sign, nothing to inspect, blocked and send failures | done (branch `feat/supervisor-phone`): `docs/superpowers/plans/2026-10-15-supervisor-phone.md` |
```

2. Under `## Pending work`, replace the line that starts `- Frontend plans 9–11` with:

```markdown
- Frontend plans 10–11 (scopes in the plan-7 file): 10 Admin setup, 11 Admin live board.
```

3. In `## Notes for the frontend plan`:
   - Replace the line that starts `` - `/scan/:token` is Cleaner-only after plan 8 `` with:

     ```markdown
     - `/scan/:token` opens the Cleaner's ScanPage or the Supervisor's InspectPage by role (`shared/components/ByRole`, plan 9); an Admin gets no-permission. `/check-in/:token` is Cleaner-only.
     ```

   - Below the line that starts `- Shared for later pages:`, add:

     ```markdown
     - Live updates (plan 9): open the hub with `followScanHub` (`shared/api/scanHub.ts`) in the query's `onCacheEntryAdded`; `onPoint` patches the cache, `onMissed` refetches (a tag). Plan 11's `getServicePoints` does the same, upserting cards, and reads its badge from `state.hub.status` (`store/slices/hubSlice.ts`). Tests drive the fake hub with `hub.latest()` from `src/test/fakeSignalR.ts`.
     - The server leaves out null fields in REST and hub JSON. Test them by truthiness; plan 9 Task 1 fixed the three `shift === null` checks of plan 8.
     ```

- [ ] **Step 7: Final full check**

```bash
cd frontend && npm test && npx tsc -b && npm run lint && npm run build && cd ..
dotnet test backend/FacilityRealtime.slnx
grep -rn "style={" frontend/src || echo "no inline styles"
grep -rn "localStorage\|sessionStorage\|indexedDB" frontend/src || echo "no web storage"
grep -rn "toLocaleString\|toLocaleTimeString" frontend/src || echo "no ad-hoc time formatting"
grep -rnE "to=\{?[\"'\`]/(scan|check-in)" frontend/src || echo "no links to the sign pages"
grep -rn "vi.mock" frontend/src || echo "no module mocks"
grep -rln "@microsoft/signalr" frontend/src   # expect: only src/shared/api/scanHub.ts (and the comment in src/test/fakeSignalR.ts)
git diff --stat 55e3177 -- backend || true   # expect: nothing
```

Expected:
- Frontend: 151 tests pass, the typecheck and lint are clean, and the JS bundle is ≤ 200 kB gzip.
- Backend: 299 unit tests pass; API tests 531 pass and 5 are skipped (nothing in `backend/` changed).
- Each grep prints its "no ..." line, `@microsoft/signalr` is imported only by `scanHub.ts`, and the `backend` diff is empty.

- [ ] **Step 8: Handoff entry and commit**

```markdown
- <date> · Task 6 done: ADR amendments (0023 token for SignalR, 0025 tree, 0026 sign URL by role, 0029 inspection page, 0048 blocked on opening, 0057 My Building live, 0062 measured size, 0064 hub follower and hub slice), README device notes, frontend README, architecture diagram, handoff notes for plans 10 and 11. Frontend 151 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
```

```bash
git add docs README.md frontend/README.md
git commit -m "docs: Supervisor phone in README, architecture and handoff; ADR amendments for the hub, sign by role, inspection and My Building"
```

- [ ] **Step 9 (controller only, not a subagent): real run on a throwaway database**

Ask the owner for the MySQL root password. Use it only in the commands below, and never write it to a file. Run this between 07:00 and 19:00: the seed Supervisor `S2001` is building A, Day shift.

```bash
mysql -u root -p -e "CREATE DATABASE facility_supervisor_test CHARACTER SET utf8mb4;"
npm --prefix frontend run build:api
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=facility_supervisor_test;Uid=root;Pwd=<typed by the owner>;CharSet=utf8mb4;" \
dotnet run --project backend/src/FacilityRealtime.Api --urls "http://0.0.0.0:5001"
```

In a second terminal:

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5001/my-building           # 200 (the app)
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5001/scan/token-restroom-m1 # 200 (the app)
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5001/hubs/scan/negotiate?negotiateVersion=1   # 401 (the hub needs a token)
```

In Chrome at `http://localhost:5001`, use two windows: a normal one for the Supervisor and an Incognito one for the Cleaner, because the two logins need separate cookies. In both, set DevTools › More tools › Sensors › Location to latitude `13.7563`, longitude `100.5018`. Then check, in order:

1. Incognito: log in at `/login` as `E1001`. Open `/check-in/token-checkin-ar01` and press "เข้างาน".
2. Normal window: log in at `/login` as `S2001`. `/my-building` shows "ตอนนี้ กะเช้า 07:00–19:00", the points of AR01 and AR02, and after a moment "หน้านี้อัปเดตเองเมื่อแม่บ้านส่งงานหรือหัวหน้าตรวจ".
3. Incognito: on `/scan/token-restroom-m1`, press "ส่งงาน". The Supervisor's `/my-building` shows "ห้องน้ำชาย ชั้น 1" as รอตรวจ at the top **without a reload**, and the รอตรวจ tile goes up by one.
4. Normal window: open `/scan/token-restroom-m1`. It shows "ส่งงานล่าสุด" with the Cleaner's time, and "ห่างป้าย … ม. · ±… ม." with "อยู่ในรัศมี". Choose "ไม่สะอาด", type a defect, and press "บันทึกผลตรวจ". "บันทึกผลตรวจแล้ว …" shows "ผล: ไม่สะอาด" and the defect.
5. Incognito: `/my-work` shows the point as ต้องแก้ไข with the defect.
6. Normal window: open `/scan/token-restroom-m1` again. It shows "รอแม่บ้านส่งงานแก้ ยังไม่มีงานใหม่ให้ตรวจ" and no "บันทึกผลตรวจ".
7. Normal window: open `/scan/token-meeting-room` (AR02, building A, nothing sent). It shows "ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน".
8. 409: the Normal window opens `/scan/token-restroom-f1`, but only after Incognito has sent "ส่งงาน" on it once. Then Incognito sends "ส่งงาน" on `/scan/token-restroom-f1` again. Now the Normal window chooses "สะอาด" and presses "บันทึกผลตรวจ": the page shows "งานของจุดนี้เปลี่ยนไประหว่างที่คุณตรวจ …" with the newer time.
9. Normal window, on the form: set DevTools › Network to Offline, choose "สะอาด" and press "บันทึกผลตรวจ"; "ยังไม่ได้บันทึกผลตรวจ" appears. Set it back to No throttling and press "ส่งใหม่"; the result page appears.
10. Normal window on `/my-building`: set Network to Offline for about 10 s, then back. The footer says "การอัปเดตสดหลุด …", then "หน้านี้อัปเดตเอง…" again, and the list reloads once (see the Network tab).
11. Log in as `admin` at `/login/admin`, then open `/scan/token-restroom-m1`: "ไม่มีสิทธิ์เปิดหน้านี้" with "หน้านี้สำหรับแม่บ้าน หรือ หัวหน้างาน บัญชีนี้เป็นAdmin".

If the run happens after 19:00 anyway, check instead that `/scan/token-restroom-m1` as `S2001` shows "ตอนนี้ไม่ใช่กะของคุณ ตรวจได้เฉพาะในกะของตัวเอง" with "ไม่ได้ตรวจงาน", and that `/my-building` shows "ตอนนี้ไม่ใช่กะของคุณ ดูได้ แต่สแกนตรวจไม่ได้".

Look in the API log for warnings: there must be no "Live update … failed".

When done, stop the API, delete the build output and drop the database:

```bash
rm -r backend/src/FacilityRealtime.Api/wwwroot
mysql -u root -p -e "DROP DATABASE facility_supervisor_test;"
```

Record the result in the handoff (Task log), commit it, then run the whole-branch review.
