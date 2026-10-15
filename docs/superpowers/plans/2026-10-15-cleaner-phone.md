# Cleaner Phone Implementation Plan (plan 8)

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Cleaner can do the whole shift on the phone: scan the Area's check-in sign and record the four attendance events, see My Work, scan a point's sign and submit it, and see what happened (late, off-round, flagged, blocked, not sent).

**Architecture:**
- **Three pages, all Cleaner-only, all on the plan-7 base** (facility-0025 tree, RTK Query, `ProtectedRoute`):
  - `pages/my-work/` at `/my-work` (M9, M14).
  - `pages/check-in/` at `/check-in/:token`, opened by an Area's check-in sign (M1, M2, M3, M8, M10).
  - `pages/scan/` at `/scan/:token`, opened by a point's sign (M2, M4, M5, M8, M11, M12, M13).
- **One shared query.** `GET /api/my-work` (`shared/api/myWorkApi.ts`) feeds all three pages: My Work's cards, the check-in page's attendance, and the scan page's "are you on duty" check. Every page asks again when it opens; Cleaners get no hub (facility-0052). An attendance answer patches that cached entry, so every page agrees at once.
- **The server decides; the phone shows.** Each press reads a fresh GPS fix (`shared/geo/readLocation.ts`), sends it with the sign's QR Token from the URL, and shows the answer: the round placement, the Geofence Flag, a Blocked Scan, or a refusal. Nothing is stored on the phone (facility-0052, 0068).
- **Shared screens** live in `shared/components/`, because two pages use them and a page may not import another page: `LocationHelp` (M2), `BlockedScanScreen` (M8), `StatusChip`.

**Tech Stack:** no new package. React 19.2, TypeScript 6.0, Vite 8.3, `@reduxjs/toolkit` 2.13.0, `react-router` 7.18.4, Vitest 5.0 on jsdom 30.1.2, Testing Library, eslint 9 with boundaries (all from plan 7).

**Spec:** this plan implements the Cleaner side of these ADRs, with wireframes M1–M5 and M8–M14 for the screens. Where a wireframe and an ADR disagree, the ADR wins.
- `docs/adr/facility-0007-scan-flow-native-camera.md`: the phone camera opens the sign's URL; a clean point is two taps (Normal already chosen).
- `docs/adr/facility-0031-four-stage-shift-attendance-lifecycle.md`: Shift-In, Break-Out, Break-In, Shift-Out; the page shows the next allowed buttons; a repeat within 5 minutes is not counted.
- `docs/adr/facility-0035-per-building-check-in-gps-flag-not-block.md`, `facility-0036-check-in-requires-location-permission.md`, `facility-0037-gps-for-every-scan-replaces-nfc.md`: GPS on every scan; outside the radius is flagged, never blocked; no location permission is the one block, and the page explains how to turn it on.
- `docs/adr/facility-0041-scan-only-own-area-with-cover-assignment.md`: another Area's sign is blocked, decided by the QR Token; the Admin sees the Blocked Scan and can assign a cover; a cover never changes where the Cleaner checks in.
- `docs/adr/facility-0046-issue-is-a-tag-not-a-card-status.md`: a reported issue is a red tag on the card, not a status.
- `docs/adr/facility-0047-cleaning-rounds-are-time-windows.md`: rounds are windows; the server places a submission On time, Late, Rework or Off-round; the card statuses and their order.
- `docs/adr/facility-0050-check-in-confirms-area-presence-not-time-attendance.md`: no late or early.
- `docs/adr/facility-0052-cleaner-my-work-page-and-resend.md`: My Work instead of push; a failed send says "ยังไม่ได้บันทึก" and is resent by hand; nothing is kept on the phone.
- `docs/adr/facility-0059-qr-token-only-from-the-sign.md`: My Work carries no QR Token and has no link to the scan page.
- `docs/adr/facility-0069-attendance-scan-window-60-minutes-around-the-shift.md`: attendance from 60 minutes before the shift to 60 minutes after it.
- `docs/adr/facility-0022`–`0026`, `0062`, `0063`, `0065`, `0066`, `0068` as in plan 7: API layer, session, state split, tree, router, bundle budget, CSS, lint boundaries, text rendering, failure minimum.
- Plan 7 (`docs/superpowers/plans/2026-10-15-frontend-foundation.md`) for the base this builds on, and `docs/handoff.md` "Notes for the frontend plan".
- Wireframes (`docs/design/wireframe/`): `Main.dc.html` (M1), `LocationOff.dc.html` (M2), `CheckinFlagged.dc.html` (M3), `CleanerSubmit.dc.html` (M4), `CleanerDone.dc.html` (M5), `ScanBlocked.dc.html` (M8), `MyWork.dc.html` (M9), `CheckinNext.dc.html` (M10), `NeedCheckin.dc.html` (M11), `SubmitFailed.dc.html` (M12), `CleanerLate.dc.html` (M13), `MyWorkCover.dc.html` (M14).

**The API this plan uses (unchanged; read the C# if in doubt):**

| Call | Answer | Source |
|---|---|---|
| `GET /api/my-work` | `MyWorkDto { attendance, areas[] }`; 403 for a non-Cleaner | `backend/src/FacilityRealtime.Api/Endpoints/MyWorkEndpoints.cs`, `DTOs/MyWorkDtos.cs` |
| `POST /api/attendance` `{ qrToken, eventType, latitude, longitude, accuracyM }` | 200 `AttendanceStateDto` with `outcome` (`Recorded`, `AlreadyRecorded`, `TooSoon`); 400 GPS or event; 403 `BlockedScanResponse`; 404 `{ message: "ไม่พบป้ายลงเวลานี้" }`; 409 `{ message }` outside the window, or `AttendanceRefusedDto { message, nextEvents }` out of order | `Endpoints/AttendanceEndpoints.cs`, `DTOs/AttendanceDtos.cs` |
| `GET /api/service-points/by-token/{token}` | `PointStatusDto`; 404 `{ message }` for an unknown, closed or check-in sign | `Endpoints/ServicePointEndpoints.cs`, `DTOs/PointDtos.cs` |
| `POST /api/scan-records` `{ qrToken, status, issueTags?, notes?, latitude, longitude, accuracyM }` | 201 `ScanRecordCreatedResponse`; 400; 403 `BlockedScanResponse`; 404 `{ message }`; 409 `{ message }` (not on duty) | `Endpoints/ScanRecordEndpoints.cs` |

JSON is camelCase; enums are strings with their C# names (`ShiftIn`, `PendingInspection`, `OffRound`); times are UTC ISO strings ending in `Z`; a Round Window's `start`/`end` is Thai wall-clock `"HH:mm:ss"`.

**Where this plan sits:** the frontend in five plans (table in plan 7). This is plan 8. Plan 9 (Supervisor phone) adds inspection to `/scan/:token`; plan 10 (Admin setup) prints the signs with the URLs this plan fixes.

## Global Constraints

- **Branch:** `feat/cleaner-phone`, created from `master` at `43f10a3` (PR #56 merged). Do not merge it.
- **Machine:** the Mac. zsh, Node v26.8.2, npm 11.19.1, .NET SDK 10.0.401.
  - Frontend commands run in `frontend/`. Backend commands run from the repo root.
  - Write files that hold Thai text with the editor tools, not shell heredocs.
- **No new package.** `package.json` and `package-lock.json` do not change. Base UI is still not needed (no Dialog or Toast).
- **API unchanged.** No file under `backend/` changes. Where the wireframes show something the API does not send, leave it out (decision 3).
- **Bundle:** the JavaScript of `npm run build` stays ≤ 200 kB gzip in one file (facility-0062). Plan 7 measured 127.18 kB.
- **Styles:**
  - No `style={...}` anywhere in `src/` (facility-0063).
  - Global tokens live in `src/index.css`. Page prefixes: `mywork-`, `checkin-`, `scan-`. Shared components use `app-`.
- **Text people type and server text** (defects, notes, names, Area names) are rendered as React text only. No `dangerouslySetInnerHTML`, and no `href` or `src` built from them (facility-0066).
- **QR Token (facility-0059):** a page reads a token only from its own URL (`useParams`). No page links to `/scan/...` or `/check-in/...`.
- **One send at a time:** while a press is being handled (GPS reading and request), its buttons are disabled, so a retried tap never writes a second record.
- **Nothing is stored on the phone:** no `localStorage`, `sessionStorage`, IndexedDB or cache of unsent work (facility-0052, 0068).
- **Thai time:** every time on screen goes through `shared/utils/thaiTime.ts` (Asia/Bangkok, 24-hour, Arabic digits). Never call `toLocaleString` or `toLocaleTimeString` elsewhere.
- **Server text is shown as is.** `getErrorMessage` shows the API's `{ message }`; pages add their own text only where the context differs (facility-0022 item 4).
- **Tests:**
  - Vitest on jsdom. Only browser APIs are faked: `fetch` with `src/test/fakeServer.ts` and `navigator.geolocation` with `src/test/fakeGps.ts` (new, Task 2). No MSW and no module mocks of our own code (regression-net #32).
  - Find elements by role, label or text the way a person would, never by CSS class.
  - Existing tests open `/my-work` and look for the heading "งานของฉัน" and the name "แม่บ้านทดสอบ" (in the bar). `MyWorkPage` must always render its `h1` "งานของฉัน", even while loading or after an error, and must never print the person's name (the bar already does; a second copy breaks `getByText`).
- **Copy:** Thai UI text comes from the wireframes. Where this plan gives a string, use it exactly, because tests match it.
- **Public repo:** made-up names only (`แม่บ้านทดสอบ`, `Area ทดลอง`, `Area ถัดไป`). No secret in any file.
- **MySQL** is used only in Task 5's controller run.
  - Use only throwaway databases `facility_*_test`. Never touch `facility_dashboard`.
  - Ask the owner for the root password each time. Type it only into the command, never into a file or into memory.
  - Subagents skip that step.
- **Handoff after every task (owner's rule):**
  - The last step of every task adds one entry to a new section `## Task log (plan 8)` in `docs/handoff.md`, placed just above `## Pending work`. It is committed with the task.
  - The entry is in English: the date, the task, what changed, the test result, and what comes next. No secret, phone number or real name.
- **Commits** end with the attribution trailer your session gives you. The husky pre-commit runs frontend lint, tsc and tests on every commit. If it fails, fix the cause. Never use `--no-verify`.
- **Baselines at `43f10a3`:** frontend 47 tests pass; backend 299 unit tests pass; API tests 531 pass and 5 are skipped (the MySQL ones).

### Decisions made while planning (report them to the owner in the PR)

1. **The check-in sign opens `/check-in/:token`; a point's sign keeps `/scan/:token`** (facility-0026).
   - The API already has two paths: `POST /api/attendance` takes only a check-in sign's token, and `by-token` answers 404 for a check-in sign. With two URLs the page knows the sign's kind without asking the server.
   - Plan 10 prints check-in signs as `<origin>/check-in/<token>` and point signs as `<origin>/scan/<token>`.
2. **Both sign routes are Cleaner-only in this plan.** A Supervisor who opens `/scan/<token>` sees "ไม่มีสิทธิ์เปิดหน้านี้" until plan 9 makes that route show the inspection page for Supervisors. A test pins this, so plan 9 changes it on purpose.
3. **No backend change**, so some wireframe details are left out:
   - **A Blocked Scan is recorded when the Cleaner presses** "ส่งงาน" or an attendance button, not when the page opens: only the two POSTs record one (facility-0041). Opening another Area's sign shows the form; the press shows M8.
   - **No distance before pressing** (M1, M4 show "ห่างป้าย 12 ม."): the phone does not know the sign's coordinates. After a submission the answer has the distance, so M5 shows it. An attendance answer has only "within radius or not", so M3 shows the flag and the phone's own accuracy, without a distance.
   - Not in any DTO, so not shown: the sign code (`AR01-01`), the Supervisor's name on the rework banner, and who and when a cover was assigned (M14 "แทนทองใบ · Admin มอบหมาย 07:40").
   - The attendance buttons say the event ("เข้างาน"), not the clock time (M1 "เข้างาน 06:52"); the recorded time appears after the press.
4. **The scan page checks duty first, from My Work's attendance.** Before Shift-In it shows M11 "ยังไม่ได้ลงเวลาเข้างาน"; after Shift-Out "ลงเวลาเลิกงานแล้ว"; outside the attendance window "ตอนนี้ไม่อยู่ในกะของคุณ". It sends nothing then. The rule copies `AttendanceRules.IsOnDuty`, and the server still decides on every submission (409).
5. **One `GET /api/my-work` cache entry for three pages, asked again whenever a page opens** (`refetchOnMountOrArgChange`), plus an "อัปเดต" button on My Work. No hub for Cleaners and no `refetchOnFocus` (plan 7 decision 9 leaves `setupListeners` to plan 11). An attendance answer patches the cached `attendance`.
6. **My Work lists what needs the Cleaner first:** ต้องแก้ไข, เลยรอบ, ยังไม่ทำ, then รอตรวจ, ตรวจผ่าน, ยังไม่ถึงรอบ, นอกเวลา. No card links to the scan page (facility-0059), although M9 links rework cards.
7. **The submit form:** "เรียบร้อย" is already chosen (facility-0007). "พบปัญหา" opens the four wireframe tags (ขยะล้น, น้ำยาหมด, พื้นเปียก, ของชำรุด, sent as those Thai words) and "หมายเหตุ" (at most 1000 characters, the server's limit). พบปัญหา needs at least one tag or a note; the server would accept neither, but a report with nothing in it helps no one. Tags and the note are sent only with พบปัญหา.
8. **"ยังไม่ได้บันทึก" (M12)** follows no answer, no answer in 20 s (`SEND_TIMEOUT_MS`), an answer that is not JSON, or a 5xx. The page keeps the choices and "ส่งใหม่" reads a fresh GPS fix and sends again. Accepted risk: if a request reached the server but its answer was lost, the resend writes a second Scan Record (placed by the normal rules, often Off-round). The same failure on the check-in page says "ยังไม่ได้บันทึกเวลา" and the buttons stay.
9. **GPS:** every press reads a fresh, high-accuracy fix (15 s timeout, no cached fix).
   - Permission refused → M2 with "เปิดแล้ว ลองอีกครั้ง", which repeats the same press.
   - No fix in time → "อ่านตำแหน่งไม่ได้" with "ลองอีกครั้ง".
   - A page on plain http (not https, not localhost) → "อ่านตำแหน่งไม่ได้" and "ต้องเปิดผ่าน https", with no retry: browsers never give a location there. Phones on `http://<Mac IP>` hit this until HTTPS exists (facility-0049).
10. **"กดซ้ำภายใน 5 นาทีไม่นับ"** is written as 5 minutes, the default of `Attendance:RepeatIgnoreMinutes`. If the owner changes that setting, change the two strings in `CheckInPage.tsx`.

---

## File structure after this plan

```
frontend/src/
├── index.css                         + status and warning colour tokens
├── router.tsx                        + /check-in/:token and /scan/:token under the Cleaner guard
├── shared/
│   ├── api/types.ts                  + Shift, AttendanceEvent, AttendanceState, PointStatus, PointCard, MyWork, BlockedScan …
│   ├── api/baseApi.ts                + SEND_TIMEOUT_MS
│   ├── api/myWorkApi.ts              GET /api/my-work, freshOnOpen (used by three pages)
│   ├── api/scanAnswers.ts            statusOf, blockedScanOf, isNotSent
│   ├── geo/readLocation.ts (+ test)  one fresh GPS reading per press
│   ├── utils/thaiTime.ts (+ test)    formatTime, formatClock, formatRound, formatMinutes
│   ├── utils/labels.ts               shift, event and point-status words
│   └── components/                   + StatusChip, LocationHelp (M2), BlockedScanScreen (M8); shared.css grows
├── pages/
│   ├── my-work/                      MyWorkPage (M9, M14), WorkCard, myWork.css, test
│   ├── check-in/                     CheckInPage (M1, M3, M10), AttendanceTimeline, checkInApi, checkIn.css, index.ts, test
│   └── scan/                         ScanPage, PointHeading, SubmitForm (M4), SubmitResult (M5, M13), OffDuty (M11),
│                                     SendFailed (M12), scanApi, scanRules, scan.css, index.ts, test
└── test/
    ├── setup.ts                      + removes the fake geolocation and isSecureContext after each test
    ├── fakeGps.ts                    fake navigator.geolocation
    └── cleanerFixtures.ts            made-up My Work, attendance, point and Blocked Scan answers
```

---

### Task 1: My Work page (M9, M14) and the shared Cleaner types

The Cleaner's home stops saying "หน้านี้กำลังสร้าง" and shows every point of the own Area and of covered Areas.

**Files:**
- Modify: `frontend/src/index.css`, `frontend/src/shared/api/types.ts`, `frontend/src/shared/components/shared.css`, `frontend/src/pages/my-work/MyWorkPage.tsx`
- Create: `frontend/src/shared/api/myWorkApi.ts`, `frontend/src/shared/utils/thaiTime.ts`, `frontend/src/shared/utils/labels.ts`, `frontend/src/shared/components/StatusChip.tsx`, `frontend/src/pages/my-work/WorkCard.tsx`, `frontend/src/pages/my-work/myWork.css`, `frontend/src/test/cleanerFixtures.ts`
- Test: `frontend/src/shared/utils/thaiTime.test.ts`, `frontend/src/pages/my-work/MyWorkPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes (plan 7): `baseApi` (`shared/api/baseApi.ts`), `getErrorMessage(error, fallback?)`, `fakeServer(routes)` and `authAnswer(role)` (`test/fakeServer.ts`), `renderApp(url)` (`test/renderApp.tsx`).
- Produces:
  - Types in `shared/api/types.ts`: `Shift`, `AttendanceEvent`, `AttendanceEntry`, `AttendanceState`, `PointStatus`, `CleaningStatus`, `Placement`, `RoundWindow`, `LastScan`, `ReportedIssue`, `Rework`, `PointCard`, `MyWorkArea`, `MyWork`, `BlockedScan` (exact shapes in Step 3).
  - `useGetMyWorkQuery(undefined, freshOnOpen)`, `myWorkApi` (for `util.updateQueryData('getMyWork', undefined, recipe)`), `freshOnOpen = { refetchOnMountOrArgChange: true }` from `shared/api/myWorkApi.ts`.
  - `formatTime(moment: string | Date): string`, `formatClock(time: string): string`, `formatRound(round: { start: string; end: string }): string`, `formatMinutes(minutes: number): string` from `shared/utils/thaiTime.ts`.
  - `shiftLabel: Record<Shift, string>`, `eventLabel: Record<AttendanceEvent, string>`, `attendanceEvents: readonly AttendanceEvent[]`, `pointStatusLabel: Record<PointStatus, string>` from `shared/utils/labels.ts`.
  - `<StatusChip status={PointStatus} />`.
  - CSS classes `app-note`, `app-note--info|warn|error`, `app-chip`, `app-panel`.
  - Test fixtures in `test/cleanerFixtures.ts`: `cleanerSession`, `point(overrides?)`, `entry(eventType, occurredAt, withinRadius?)`, `attendance(events, nextEvents, outcome?)`, `notCheckedIn`, `onDuty`, `checkedOut`, `outsideShift`, `area(points?, overrides?)`, `myWork(attendance?, areas?)`, `otherAreaBlocked`.

- [x] **Step 1: Create the branch**

```bash
git checkout master && git pull --ff-only && git checkout -b feat/cleaner-phone
git log --oneline -1   # expect 43f10a3 (or a later docs-only commit)
```

- [x] **Step 2: Colour tokens**

In `frontend/src/index.css`, inside `:root`, right after the line `--color-error-text: #8a1c12;`, add (colours from `MyWork.dc.html`):

```css
  --color-warn-bg: #fff4e5;
  --color-warn-border: #e3b776;
  --color-warn-text: #7a4300;
  --color-status-rework: #b42318;
  --color-status-overdue: #a85a00;
  --color-status-pending: #4a6378;
  --color-status-passed: #2e6e4a;
  --color-status-idle: #5f696b;
```

- [x] **Step 3: Server types**

Append to `frontend/src/shared/api/types.ts`:

```ts
/** facility-0040: Day 07:00–19:00, Night 19:00–07:00 (Asia/Bangkok). */
export type Shift = 'Day' | 'Night';

/** facility-0031: the four attendance scans of a shift. */
export type AttendanceEvent = 'ShiftIn' | 'BreakOut' | 'BreakIn' | 'ShiftOut';

/** AttendanceEntryDto. withinRadius false is a Geofence Flag; null means the sign has no coordinates yet. */
export interface AttendanceEntry {
  eventType: AttendanceEvent;
  occurredAt: string;
  source: 'Scan' | 'AdminAdd' | 'AdminEdit';
  withinRadius: boolean | null;
}

/**
 * AttendanceStateDto. shiftDate and shift are null, and the lists empty, outside every attendance window of the
 * Cleaner's own shift (facility-0069). outcome is set only on the answer of POST /api/attendance.
 */
export interface AttendanceState {
  shiftDate: string | null;
  shift: Shift | null;
  events: AttendanceEntry[];
  nextEvents: AttendanceEvent[];
  outcome: 'Recorded' | 'AlreadyRecorded' | 'TooSoon' | null;
}

/** facility-0047 rule 4 (PointStatusCalculator). */
export type PointStatus =
  | 'Rework'
  | 'Overdue'
  | 'PendingInspection'
  | 'Passed'
  | 'NotYetDone'
  | 'BeforeFirstRound'
  | 'OffHours';

/** facility-0046: Issue is a tag on the card, never a Point Status. */
export type CleaningStatus = 'Normal' | 'Issue';

/** facility-0047 rule 3: which round a submission counts for, decided once by the server. */
export type Placement = 'OnTime' | 'Late' | 'Rework' | 'OffRound';

/** RoundWindowDto. start and end are Thai wall-clock "HH:mm:ss", as the Admin entered them. */
export interface RoundWindow {
  id: number;
  start: string;
  end: string;
}

/** LastScanDto. */
export interface LastScan {
  submittedAt: string;
  cleanerName: string;
  placement: Placement;
  status: CleaningStatus;
}

/** IssueDto (facility-0046): shown while the latest submission reported an issue. */
export interface ReportedIssue {
  tags: string[];
  note: string | null;
  reportedAt: string;
}

/** ReworkDto (facility-0029): what the Supervisor asked to fix, while the card is ต้องแก้ไข. */
export interface Rework {
  defect: string;
  requestedAt: string;
}

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

/** MyWorkAreaDto. isCover: an Area the Cleaner covers this shift (facility-0041). */
export interface MyWorkArea {
  areaId: number;
  areaCode: string;
  areaName: string;
  isCover: boolean;
  points: PointCard[];
}

/** MyWorkDto (facility-0052): the attendance, then the own Area, then covered Areas. */
export interface MyWork {
  attendance: AttendanceState;
  areas: MyWorkArea[];
}

/** BlockedScanResponse (403): the scanned sign belongs elsewhere and nothing was recorded (facility-0041, 0048). */
export interface BlockedScan {
  message: string;
  areaCode: string;
  areaName: string;
  reason: 'OtherArea' | 'OtherBuilding' | 'OutsideShift';
  buildingCode: string;
}
```

- [x] **Step 4: Write the failing time test**

`frontend/src/shared/utils/thaiTime.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { formatMinutes, formatRound, formatTime } from './thaiTime';

describe('thaiTime', () => {
  it('shows a UTC moment from the server as Thai time on a 24-hour clock', () => {
    expect(formatTime('2026-10-14T23:52:00Z')).toBe('06:52');
    expect(formatTime('2026-10-15T17:05:00Z')).toBe('00:05');
  });

  it('shows a Date from the phone as Thai time', () => {
    expect(formatTime(new Date('2026-10-15T03:04:00Z'))).toBe('10:04');
  });

  it('drops the seconds of a round window', () => {
    expect(formatRound({ start: '07:00:00', end: '09:00:00' })).toBe('07:00–09:00');
  });

  it('says minutes as a Thai duration', () => {
    expect(formatMinutes(25)).toBe('25 นาที');
    expect(formatMinutes(85)).toBe('1 ชม. 25 นาที');
    expect(formatMinutes(120)).toBe('2 ชม.');
  });
});
```

Run: `npm test -- src/shared/utils/thaiTime.test.ts`
Expected: FAIL, `Failed to resolve import "./thaiTime"`.

- [x] **Step 5: Write `thaiTime.ts` and `labels.ts`**

`frontend/src/shared/utils/thaiTime.ts`:

```ts
/** Thai wall-clock "HH:mm". en-GB keeps Arabic digits and a 24-hour clock whatever language the phone uses. */
const thaiClock = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Bangkok',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/** A moment from the server (UTC ISO ending in "Z") or from the phone, as Thai time. */
export function formatTime(moment: string | Date): string {
  return thaiClock.format(typeof moment === 'string' ? new Date(moment) : moment);
}

/** A Round Window time ("HH:mm:ss") is already Thai wall-clock (RoundWindowDto); only the seconds go. */
export function formatClock(time: string): string {
  return time.slice(0, 5);
}

export function formatRound(round: { start: string; end: string }): string {
  return `${formatClock(round.start)}–${formatClock(round.end)}`;
}

export function formatMinutes(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) {
    return `${rest} นาที`;
  }
  return rest === 0 ? `${hours} ชม.` : `${hours} ชม. ${rest} นาที`;
}
```

`frontend/src/shared/utils/labels.ts`:

```ts
import type { AttendanceEvent, PointStatus, Shift } from '../api/types';

/** facility-0040. */
export const shiftLabel: Record<Shift, string> = {
  Day: 'กะเช้า 07:00–19:00',
  Night: 'กะดึก 19:00–07:00',
};

/** facility-0031, wireframes M1 and M10. */
export const eventLabel: Record<AttendanceEvent, string> = {
  ShiftIn: 'เข้างาน',
  BreakOut: 'ออกพัก',
  BreakIn: 'กลับจากพัก',
  ShiftOut: 'เลิกงาน',
};

/** The order the four events are shown in. */
export const attendanceEvents: readonly AttendanceEvent[] = ['ShiftIn', 'BreakOut', 'BreakIn', 'ShiftOut'];

/** facility-0047 rule 4: the card statuses as everyone sees them. */
export const pointStatusLabel: Record<PointStatus, string> = {
  Rework: 'ต้องแก้ไข',
  Overdue: 'เลยรอบ',
  PendingInspection: 'รอตรวจ',
  Passed: 'ตรวจผ่าน',
  NotYetDone: 'ยังไม่ทำ',
  BeforeFirstRound: 'ยังไม่ถึงรอบ',
  OffHours: 'นอกเวลา',
};
```

Run: `npm test -- src/shared/utils/thaiTime.test.ts`
Expected: PASS, 4 tests.

- [x] **Step 6: The shared query, the chip and the shared CSS**

`frontend/src/shared/api/myWorkApi.ts`:

```ts
import { baseApi } from './baseApi';
import type { MyWork } from './types';

/**
 * facility-0052: GET /api/my-work feeds My Work, the check-in page and the scan page. Cleaners get no live updates,
 * so every page that shows it passes `freshOnOpen` and asks the server again each time it opens.
 */
export const myWorkApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getMyWork: build.query<MyWork, void>({
      query: () => '/my-work',
    }),
  }),
});

export const { useGetMyWorkQuery } = myWorkApi;

/** Hook options for every Cleaner page: never act on an answer from an earlier visit. */
export const freshOnOpen = { refetchOnMountOrArgChange: true } as const;
```

`frontend/src/shared/components/StatusChip.tsx`:

```tsx
import type { PointStatus } from '../api/types';
import { pointStatusLabel } from '../utils/labels';
import './shared.css';

/** A point's status (facility-0047 rule 4) as the same coloured chip on every page. */
export function StatusChip({ status }: { status: PointStatus }) {
  return <span className={`app-chip app-chip--${status}`}>{pointStatusLabel[status]}</span>;
}
```

Append to `frontend/src/shared/components/shared.css`:

```css
.app-note {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 10px 14px;
  border-radius: var(--radius-md);
  font-size: 14px;
}

.app-note--info {
  background: var(--color-info-bg);
  border: 1px solid var(--color-info-border);
  color: var(--color-info-text);
}

.app-note--warn {
  background: var(--color-warn-bg);
  border: 1px solid var(--color-warn-border);
  color: var(--color-warn-text);
}

.app-note--error {
  background: var(--color-error-bg);
  border: 1px solid var(--color-error-border);
  color: var(--color-error-text);
}

.app-chip {
  display: inline-block;
  padding: 2px 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;
  white-space: nowrap;
  color: var(--color-on-primary);
  background: var(--color-status-idle);
}

.app-chip--Rework {
  background: var(--color-status-rework);
}

.app-chip--Overdue {
  background: var(--color-status-overdue);
}

.app-chip--PendingInspection {
  background: var(--color-status-pending);
}

.app-chip--Passed {
  background: var(--color-status-passed);
}

.app-panel {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 14px 16px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.app-panel h2 {
  font-size: 15px;
  font-weight: 600;
}

.app-panel ol {
  margin: 0;
  padding-left: 20px;
}
```

- [x] **Step 7: Test fixtures**

`frontend/src/test/cleanerFixtures.ts`:

```ts
import type {
  AttendanceEntry,
  AttendanceEvent,
  AttendanceState,
  BlockedScan,
  MyWork,
  MyWorkArea,
  PointCard,
} from '../shared/api/types';
import { authAnswer } from './fakeServer';

/**
 * Made-up answers for the Cleaner pages (public repo: no real names). The API sends UTC; the Thai time is in the
 * comments: 23:52Z the day before is 06:52, 03:05Z is 10:05.
 */
export const cleanerSession = { 'POST /api/auth/refresh': () => authAnswer('cleaner') };

export function point(overrides: Partial<PointCard> = {}): PointCard {
  return {
    id: 11,
    name: 'ห้องน้ำชาย',
    areaId: 1,
    areaCode: 'AR01',
    areaName: 'Area ทดลอง',
    buildingCode: 'A',
    status: 'NotYetDone',
    currentRound: { id: 101, start: '10:00:00', end: '12:00:00' },
    nextRound: { id: 102, start: '13:00:00', end: '15:00:00' },
    lastScan: null,
    issue: null,
    rework: null,
    ...overrides,
  };
}

export function entry(eventType: AttendanceEvent, occurredAt: string, withinRadius: boolean | null = true): AttendanceEntry {
  return { eventType, occurredAt, source: 'Scan', withinRadius };
}

export function attendance(
  events: AttendanceEntry[],
  nextEvents: AttendanceEvent[],
  outcome: AttendanceState['outcome'] = null,
): AttendanceState {
  return { shiftDate: '2026-10-15', shift: 'Day', events, nextEvents, outcome };
}

export const notCheckedIn = attendance([], ['ShiftIn']);
export const onDuty = attendance([entry('ShiftIn', '2026-10-14T23:52:00Z')], ['BreakOut', 'ShiftOut']); // in 06:52
export const checkedOut = attendance(
  [entry('ShiftIn', '2026-10-14T23:52:00Z'), entry('ShiftOut', '2026-10-15T11:55:00Z')], // 06:52, 18:55
  [],
);
export const outsideShift: AttendanceState = { shiftDate: null, shift: null, events: [], nextEvents: [], outcome: null };

export function area(points: PointCard[] = [point()], overrides: Partial<MyWorkArea> = {}): MyWorkArea {
  return { areaId: 1, areaCode: 'AR01', areaName: 'Area ทดลอง', isCover: false, points, ...overrides };
}

export function myWork(attendanceState: AttendanceState = onDuty, areas: MyWorkArea[] = [area()]): MyWork {
  return { attendance: attendanceState, areas };
}

export const otherAreaBlocked: BlockedScan = {
  message: 'ป้ายนี้ไม่ใช่ Area ของคุณ',
  areaCode: 'AR02',
  areaName: 'Area ถัดไป',
  reason: 'OtherArea',
  buildingCode: 'B',
};
```

- [x] **Step 8: Write the failing My Work tests**

`frontend/src/pages/my-work/MyWorkPage.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { area, cleanerSession, myWork, notCheckedIn, outsideShift, point } from '../../test/cleanerFixtures';
import { fakeServer } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serveMyWork(answer: unknown, status = 200) {
  return fakeServer({ ...cleanerSession, 'GET /api/my-work': () => ({ status, body: answer }) });
}

/** Wireframe M9's four points, in the server's order (not by urgency). */
const fourPoints = [
  point({
    id: 1,
    name: 'ห้องน้ำหญิง',
    status: 'Passed',
    lastScan: { submittedAt: '2026-10-15T03:10:00Z', cleanerName: 'แม่บ้านทดสอบ', placement: 'OnTime', status: 'Issue' }, // 10:10
    issue: { tags: ['ของชำรุด'], note: 'ก๊อกอ่างที่ 2 รั่ว', reportedAt: '2026-10-15T03:10:00Z' },
  }),
  point({
    id: 2,
    name: 'ห้องน้ำชาย',
    status: 'PendingInspection',
    lastScan: { submittedAt: '2026-10-15T03:05:00Z', cleanerName: 'แม่บ้านทดสอบ', placement: 'OnTime', status: 'Normal' }, // 10:05
  }),
  point({ id: 3, name: 'โถงทางเดิน', status: 'Overdue', currentRound: { id: 31, start: '07:30:00', end: '09:00:00' } }),
  point({
    id: 4,
    name: 'จุดทิ้งขยะ',
    status: 'Rework',
    currentRound: { id: 41, start: '08:00:00', end: '10:00:00' },
    rework: { defect: 'รอบถังขยะยังมีคราบ', requestedAt: '2026-10-15T02:40:00Z' }, // 09:40
  }),
];

describe('MyWorkPage (M9, M14, facility-0052)', () => {
  it('lists the own Area most urgent first, with the summary and the shift line', async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    const ownArea = await screen.findByRole('region', { name: 'Area ทดลอง (Area ของฉัน)' });
    expect(within(ownArea).getAllByRole('heading', { level: 3 }).map((h) => h.textContent)).toEqual([
      'จุดทิ้งขยะ',
      'โถงทางเดิน',
      'ห้องน้ำชาย',
      'ห้องน้ำหญิง',
    ]);
    const summary = screen.getByRole('list', { name: 'สรุปงาน' });
    expect(within(summary).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      '1 ต้องแก้',
      '1 เลยรอบ',
      '2 ส่งแล้ว',
    ]);
    expect(screen.getByText('กะเช้า 07:00–19:00 · เข้างาน 06:52')).toBeInTheDocument();
    expect(screen.getByText('ส่งงาน 10:05 · รอบถัดไป 13:00–15:00')).toBeInTheDocument();
  });

  it('a card sent back shows the defect and says to scan the point again, with no link to the scan page (facility-0059)', async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    expect(await screen.findByText('หัวหน้าให้แก้ 09:40: “รอบถังขยะยังมีคราบ”')).toBeInTheDocument();
    expect(screen.getByText('แก้แล้วสแกน QR ที่จุดนี้เพื่อส่งงานใหม่')).toBeInTheDocument();
    expect(within(screen.getByRole('main')).queryAllByRole('link')).toHaveLength(0);
  });

  it('shows the reported issue as a tag and what an overdue point still needs (facility-0046)', async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    expect(await screen.findByText('แจ้งปัญหา: ของชำรุด · ก๊อกอ่างที่ 2 รั่ว')).toBeInTheDocument();
    expect(screen.getByText('ยังไม่ส่งงาน · เลยเวลารอบแล้ว')).toBeInTheDocument();
    expect(screen.getByText('ตรวจผ่าน · ส่งงาน 10:10 · รอบถัดไป 13:00–15:00')).toBeInTheDocument();
  });

  it('covered Areas come after the own Area, marked ทำแทน (M14)', async () => {
    const covered = area([point({ id: 9, name: 'ห้องพักพนักงาน', areaId: 12, areaName: 'Area 12', buildingCode: 'B' })], {
      areaId: 12,
      areaCode: 'AR12',
      areaName: 'Area 12',
      isCover: true,
    });
    serveMyWork(myWork(undefined, [area(), covered]));
    await renderApp('/my-work');

    expect(await screen.findByText('วันนี้ทำแทน Area 12 ด้วย · สิทธิ์หมดเองเมื่อหมดกะ')).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent)).toEqual([
      'Area ทดลอง (Area ของฉัน)',
      'Area 12 · ตึก B (ทำแทน)',
    ]);
  });

  it('before Shift-In the shift line says so', async () => {
    serveMyWork(myWork(notCheckedIn));
    await renderApp('/my-work');

    expect(await screen.findByText('กะเช้า 07:00–19:00 · ยังไม่ได้ลงเวลาเข้างาน')).toBeInTheDocument();
    expect(screen.getByText('ยังไม่ส่ง · ส่งได้ถึง 12:00')).toBeInTheDocument();
  });

  it('outside the attendance window the shift line says so (facility-0069)', async () => {
    serveMyWork(myWork(outsideShift));
    await renderApp('/my-work');

    expect(await screen.findByText('ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ')).toBeInTheDocument();
  });

  it('asks the server again on "อัปเดต" (no push for Cleaners, facility-0052)', async () => {
    const server = serveMyWork(myWork());
    const { user } = await renderApp('/my-work');
    await screen.findByRole('region', { name: 'Area ทดลอง (Area ของฉัน)' });
    expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(1);

    await user.click(screen.getByRole('button', { name: 'อัปเดต' }));

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(2));
  });

  it('a Cleaner without an Area is told to ask the Admin', async () => {
    serveMyWork(myWork(undefined, []));
    await renderApp('/my-work');

    expect(
      await screen.findByText('ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับ Area หรือ Area ยังไม่มีจุด แจ้ง Admin'),
    ).toBeInTheDocument();
  });

  it('a failed load keeps the heading and shows the shared error wording', async () => {
    serveMyWork(undefined, 500);
    await renderApp('/my-work');

    expect(await screen.findByRole('alert')).toHaveTextContent('เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง');
    expect(screen.getByRole('heading', { name: 'งานของฉัน' })).toBeInTheDocument();
  });
});
```

Run: `npm test -- src/pages/my-work`
Expected: FAIL. The page still says "หน้านี้กำลังสร้าง", so `findByRole('region', ...)` times out.

- [x] **Step 9: Write the page**

`frontend/src/pages/my-work/WorkCard.tsx`:

```tsx
import type { PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatClock, formatRound, formatTime } from '../../shared/utils/thaiTime';

/** One point on My Work. Names, defects and notes are rendered as React text only (facility-0066). */
export function WorkCard({ point }: { point: PointCard }) {
  const round = point.currentRound
    ? `รอบ ${formatRound(point.currentRound)}`
    : point.nextRound
      ? `รอบถัดไป ${formatRound(point.nextRound)}`
      : null;

  return (
    <li className={`mywork-card mywork-card--${point.status}`}>
      <div className="mywork-card__top">
        <h3 className="mywork-card__name">{point.name}</h3>
        <StatusChip status={point.status} />
      </div>
      {round && <span className="mywork-card__round">{round}</span>}
      <span className="mywork-card__detail">{detailOf(point)}</span>
      {point.rework && <span className="mywork-card__todo">แก้แล้วสแกน QR ที่จุดนี้เพื่อส่งงานใหม่</span>}
      {point.issue && (
        <span className="mywork-card__issue">
          {`แจ้งปัญหา: ${[...point.issue.tags, point.issue.note].filter(Boolean).join(' · ')}`}
        </span>
      )}
    </li>
  );
}

function detailOf(point: PointCard): string {
  const sent = point.lastScan ? `ส่งงาน ${formatTime(point.lastScan.submittedAt)}` : null;
  const next = point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : null;
  switch (point.status) {
    case 'Rework':
      return point.rework ? `หัวหน้าให้แก้ ${formatTime(point.rework.requestedAt)}: “${point.rework.defect}”` : 'หัวหน้าให้แก้';
    case 'Overdue':
      return 'ยังไม่ส่งงาน · เลยเวลารอบแล้ว';
    case 'NotYetDone':
      return point.currentRound ? `ยังไม่ส่ง · ส่งได้ถึง ${formatClock(point.currentRound.end)}` : 'ยังไม่ส่ง';
    case 'PendingInspection':
      return [sent, next].filter(Boolean).join(' · ');
    case 'Passed':
      return ['ตรวจผ่าน', sent, next].filter(Boolean).join(' · ');
    case 'BeforeFirstRound':
      return 'รอบแรกของกะยังไม่เริ่ม';
    case 'OffHours':
      return 'จุดนี้ไม่มีรอบในกะนี้';
  }
}
```

Replace the whole of `frontend/src/pages/my-work/MyWorkPage.tsx` with:

```tsx
import { freshOnOpen, useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import type { AttendanceState, MyWork, PointStatus } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';
import { WorkCard } from './WorkCard';
import './myWork.css';

/** What needs the Cleaner comes first (plan 8 decision 6), then what is done or not due yet. */
const urgency: readonly PointStatus[] = [
  'Rework',
  'Overdue',
  'NotYetDone',
  'PendingInspection',
  'Passed',
  'BeforeFirstRound',
  'OffHours',
];

/**
 * facility-0052, wireframes M9 and M14: every point of the Cleaner's own Area, then of the Areas they cover this
 * shift. No push and no hub: the page asks the server each time it opens and on "อัปเดต". No card links to the scan
 * page; the only way there is the sign's QR (facility-0059). The name is not shown: the bar above already shows it.
 */
export function MyWorkPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyWorkQuery(undefined, freshOnOpen);

  return (
    <main className="mywork-page">
      <header className="mywork-heading">
        <h1>งานของฉัน</h1>
        {data && <p className="mywork-sub">{attendanceLine(data.attendance)}</p>}
      </header>
      {isLoading && <p className="mywork-muted">กำลังโหลดงาน...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {data && <WorkList work={data} />}
      <footer className="mywork-footer">
        <span>ไม่มีแจ้งเตือนเด้งขึ้น เปิดหน้านี้หรือกด "อัปเดต" เพื่อดูงานล่าสุด</span>
        <button type="button" className="app-button" disabled={isFetching} onClick={() => void refetch()}>
          {isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </button>
      </footer>
    </main>
  );
}

function attendanceLine(attendance: AttendanceState): string {
  if (attendance.shift === null) {
    return 'ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ';
  }
  const shiftIn = attendance.events.find((e) => e.eventType === 'ShiftIn');
  const shiftOut = attendance.events.find((e) => e.eventType === 'ShiftOut');
  return [
    shiftLabel[attendance.shift],
    shiftIn ? `เข้างาน ${formatTime(shiftIn.occurredAt)}` : 'ยังไม่ได้ลงเวลาเข้างาน',
    shiftOut ? `เลิกงาน ${formatTime(shiftOut.occurredAt)}` : null,
  ]
    .filter(Boolean)
    .join(' · ');
}

function WorkList({ work }: { work: MyWork }) {
  if (work.areas.length === 0) {
    return (
      <p className="app-note app-note--info">
        ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับ Area หรือ Area ยังไม่มีจุด แจ้ง Admin
      </p>
    );
  }
  const points = work.areas.flatMap((area) => area.points);
  const count = (...statuses: PointStatus[]) => points.filter((p) => statuses.includes(p.status)).length;
  const covered = work.areas.filter((area) => area.isCover);

  return (
    <>
      <ul className="mywork-summary" aria-label="สรุปงาน">
        <li className="mywork-summary__rework">
          <strong>{count('Rework')}</strong> ต้องแก้
        </li>
        <li className="mywork-summary__overdue">
          <strong>{count('Overdue')}</strong> เลยรอบ
        </li>
        <li>
          <strong>{count('PendingInspection', 'Passed')}</strong> ส่งแล้ว
        </li>
      </ul>
      {covered.length > 0 && (
        <p className="app-note app-note--info">
          {`วันนี้ทำแทน ${covered.map((area) => area.areaName).join(', ')} ด้วย · สิทธิ์หมดเองเมื่อหมดกะ`}
        </p>
      )}
      {work.areas.map((area) => {
        const headingId = `mywork-area-${area.areaId}`;
        const title = area.isCover
          ? `${area.areaName} · ตึก ${area.points[0]?.buildingCode ?? ''} (ทำแทน)`
          : `${area.areaName} (Area ของฉัน)`;
        const sorted = [...area.points].sort((a, b) => urgency.indexOf(a.status) - urgency.indexOf(b.status));
        return (
          <section key={area.areaId} className="mywork-area" aria-labelledby={headingId}>
            <h2 id={headingId}>{title}</h2>
            <ul className="mywork-cards">
              {sorted.map((point) => (
                <WorkCard key={point.id} point={point} />
              ))}
            </ul>
          </section>
        );
      })}
    </>
  );
}
```

Mind the JSX of the summary: `<strong>{count('Rework')}</strong> ต้องแก้` must keep the space before the word, because the test reads `"1 ต้องแก้"`.

`frontend/src/pages/my-work/myWork.css`:

```css
/* My Work, wireframes M9 and M14. Class prefix: mywork- */

.mywork-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.mywork-heading {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.mywork-heading h1 {
  font-size: 26px;
  font-weight: 700;
}

.mywork-sub,
.mywork-muted {
  font-size: 15px;
  color: var(--color-muted);
}

.mywork-summary {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin: 0;
  padding: 0;
  list-style: none;
  text-align: center;
  font-size: 13px;
}

.mywork-summary li {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 8px 4px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.mywork-summary strong {
  font-size: 24px;
  font-variant-numeric: tabular-nums;
}

.mywork-summary .mywork-summary__rework {
  background: var(--color-error-bg);
  border-color: var(--color-error-border);
  color: var(--color-error-text);
}

.mywork-summary .mywork-summary__overdue {
  background: var(--color-warn-bg);
  border-color: var(--color-warn-border);
  color: var(--color-warn-text);
}

.mywork-area {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.mywork-area h2 {
  font-size: 16px;
  font-weight: 600;
}

.mywork-cards {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.mywork-card {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 12px 14px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.mywork-card--Rework {
  padding: 11px 13px;
  border: 2px solid var(--color-status-rework);
}

.mywork-card__top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}

.mywork-card__name {
  font-size: 16px;
  font-weight: 600;
}

.mywork-card__round,
.mywork-card__detail {
  font-size: 14px;
  color: var(--color-text-soft);
}

.mywork-card__todo {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-status-rework);
}

.mywork-card__issue {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-error-text);
}

.mywork-footer {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--color-muted);
  text-align: center;
}
```

- [x] **Step 10: Run the tests, the typecheck and the lint**

```bash
npm test          # expect: 60 passed (47 + 4 time + 9 My Work), including the old router and login tests
npx tsc -b        # expect: no output
npm run lint      # expect: no output
```

If an old test now fails with "Found multiple elements with the text: แม่บ้านทดสอบ", the page prints the name; remove it (Global Constraints).

- [x] **Step 11: Handoff entry**

Add `## Task log (plan 8)` above `## Pending work` in `docs/handoff.md`, with this first entry (fill in the date):

```markdown
- <date> · Task 1 done: My Work (M9, M14): own Area then covered Areas, most urgent first, summary tiles, rework defect, issue tag, no link to the scan page; asks the server on open and on "อัปเดต". Shared Cleaner types, GET /api/my-work query, Thai time helpers, status chip and colour tokens. Frontend tests 60 passed. Next: Task 2 (check-in page).
```

- [x] **Step 12: Commit**

```bash
git add frontend/src docs/handoff.md docs/superpowers/plans/2026-10-15-cleaner-phone.md
git commit -m "feat(frontend): My Work page for Cleaners, most urgent first, covered Areas, no link to the scan page (facility-0052, 0059)"
```

---

### Task 2: Check-in sign page (M1, M2, M3, M8, M10)

The Area's check-in sign opens `/check-in/<token>`. The page shows the shift's four events and the buttons the server allows next; a press reads the GPS and records.

**Files:**
- Modify: `frontend/src/test/setup.ts`, `frontend/src/shared/api/baseApi.ts`, `frontend/src/shared/components/shared.css`, `frontend/src/router.tsx`
- Create: `frontend/src/test/fakeGps.ts`, `frontend/src/shared/geo/readLocation.ts`, `frontend/src/shared/api/scanAnswers.ts`, `frontend/src/shared/components/LocationHelp.tsx`, `frontend/src/shared/components/BlockedScanScreen.tsx`, `frontend/src/pages/check-in/checkInApi.ts`, `frontend/src/pages/check-in/AttendanceTimeline.tsx`, `frontend/src/pages/check-in/CheckInPage.tsx`, `frontend/src/pages/check-in/checkIn.css`, `frontend/src/pages/check-in/index.ts`
- Test: `frontend/src/shared/geo/readLocation.test.ts`, `frontend/src/pages/check-in/CheckInPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes (Task 1): `AttendanceEvent`, `AttendanceState`, `BlockedScan` types; `useGetMyWorkQuery`, `myWorkApi`, `freshOnOpen`; `formatTime`; `eventLabel`, `attendanceEvents`, `shiftLabel`; fixtures `cleanerSession`, `attendance`, `entry`, `myWork`, `notCheckedIn`, `onDuty`, `outsideShift`, `otherAreaBlocked`.
- Produces:
  - `readLocation(): Promise<LocationReading>`, with `GpsFix = { latitude: number; longitude: number; accuracyM: number }`, `LocationProblem = 'denied' | 'unavailable' | 'insecure'` and `LocationReading = { kind: 'fix'; fix: GpsFix } | { kind: LocationProblem }` (`shared/geo/readLocation.ts`).
  - `statusOf(error: unknown): unknown`, `blockedScanOf(error: unknown): BlockedScan | null`, `isNotSent(error: unknown): boolean` (`shared/api/scanAnswers.ts`).
  - `SEND_TIMEOUT_MS = 20_000` (`shared/api/baseApi.ts`).
  - `<LocationHelp problem action busy onRetry />` and `<BlockedScanScreen blocked sign ownAreaName? />`, with `sign: 'check-in' | 'point'`.
  - CSS classes `app-help`, `app-help__kicker`, `app-help__title`, `app-facts`.
  - Test helper `fakeGps(...answers): { calls: number }`, with `nearSign` and `farFromSign` (`test/fakeGps.ts`). `test/setup.ts` removes the fake after each test.
  - Route `/check-in/:token` (Cleaner only).

- [x] **Step 1: The fake GPS and the setup cleanup**

`frontend/src/test/fakeGps.ts`:

```ts
/** A reading next to the seed's AR01 signs (a made-up place), and one about 80 m away with a poor fix. */
export const nearSign = { latitude: 13.7563, longitude: 100.5018, accuracy: 12 };
export const farFromSign = { latitude: 13.757, longitude: 100.5018, accuracy: 40 };

export type FakeGpsAnswer = typeof nearSign | 'denied' | 'unavailable';

/**
 * Gives the page a navigator.geolocation (jsdom has none). Each getCurrentPosition takes the next answer; the last
 * one repeats. Like fetch, it is a browser API, not our code (regression-net #32). test/setup.ts removes it after
 * each test. `calls` counts the readings asked for.
 */
export function fakeGps(...answers: FakeGpsAnswer[]) {
  const state = { calls: 0 };
  const geolocation = {
    getCurrentPosition(onFix: PositionCallback, onError?: PositionErrorCallback | null) {
      const answer = answers[Math.min(state.calls, answers.length - 1)];
      state.calls += 1;
      setTimeout(() => {
        if (answer === 'denied' || answer === 'unavailable') {
          const code = answer === 'denied' ? 1 : 2;
          onError?.({ code, message: answer, PERMISSION_DENIED: 1, POSITION_UNAVAILABLE: 2, TIMEOUT: 3 } as unknown as GeolocationPositionError);
        } else {
          const coords = { ...answer, altitude: null, altitudeAccuracy: null, heading: null, speed: null };
          onFix({ coords, timestamp: Date.now() } as unknown as GeolocationPosition);
        }
      }, 0);
    },
  };
  Object.defineProperty(navigator, 'geolocation', { value: geolocation, configurable: true });
  return state;
}
```

In `frontend/src/test/setup.ts`, extend the `afterEach` so it reads:

```ts
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  // test/fakeGps.ts and the "plain http" tests define these on the browser objects
  Reflect.deleteProperty(navigator, 'geolocation');
  Reflect.deleteProperty(window, 'isSecureContext');
});
```

- [x] **Step 2: Write the failing GPS test**

`frontend/src/shared/geo/readLocation.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { fakeGps, nearSign } from '../../test/fakeGps';
import { readLocation } from './readLocation';

describe('readLocation (facility-0036, 0037)', () => {
  it('a fix carries the phone accuracy as accuracyM', async () => {
    fakeGps(nearSign);

    await expect(readLocation()).resolves.toEqual({
      kind: 'fix',
      fix: { latitude: 13.7563, longitude: 100.5018, accuracyM: 12 },
    });
  });

  it('a refused permission is "denied"; any other failure is "unavailable"', async () => {
    fakeGps('denied', 'unavailable');

    await expect(readLocation()).resolves.toEqual({ kind: 'denied' });
    await expect(readLocation()).resolves.toEqual({ kind: 'unavailable' });
  });

  it('on plain http it does not ask the phone, because browsers never answer there', async () => {
    const gps = fakeGps(nearSign);
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });

    await expect(readLocation()).resolves.toEqual({ kind: 'insecure' });
    expect(gps.calls).toBe(0);
  });
});
```

Run: `npm test -- src/shared/geo`
Expected: FAIL, `Failed to resolve import "./readLocation"`.

- [x] **Step 3: Write `readLocation.ts`**

`frontend/src/shared/geo/readLocation.ts`:

```ts
/** What the browser's Geolocation API reported; the server stores all three (facility-0036, 0037). */
export interface GpsFix {
  latitude: number;
  longitude: number;
  accuracyM: number;
}

/**
 * denied: no permission, the one case that blocks a scan (facility-0036). unavailable: no fix in time.
 * insecure: the page is on plain http, where browsers never give a location (facility-0028, 0049).
 */
export type LocationProblem = 'denied' | 'unavailable' | 'insecure';

export type LocationReading = { kind: 'fix'; fix: GpsFix } | { kind: LocationProblem };

const PERMISSION_DENIED = 1; // GeolocationPositionError.PERMISSION_DENIED

/** A fresh, high-accuracy reading for every press: a cached fix may be from before the person walked to the sign. */
export function readLocation(): Promise<LocationReading> {
  if (window.isSecureContext === false) {
    return Promise.resolve({ kind: 'insecure' });
  }
  if (!('geolocation' in navigator)) {
    return Promise.resolve({ kind: 'unavailable' });
  }
  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      (position) =>
        resolve({
          kind: 'fix',
          fix: {
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            accuracyM: position.coords.accuracy,
          },
        }),
      (error) => resolve({ kind: error.code === PERMISSION_DENIED ? 'denied' : 'unavailable' }),
      { enableHighAccuracy: true, timeout: 15_000, maximumAge: 0 },
    );
  });
}
```

Run: `npm test -- src/shared/geo`
Expected: PASS, 3 tests.

- [x] **Step 4: Answers shared by both scan pages, and the send timeout**

`frontend/src/shared/api/scanAnswers.ts`:

```ts
import type { BlockedScan } from './types';

/** An RTK Query error's HTTP status, or its kind ('FETCH_ERROR', 'TIMEOUT_ERROR', 'PARSING_ERROR'). */
export function statusOf(error: unknown): unknown {
  return typeof error === 'object' && error !== null && 'status' in error ? error.status : undefined;
}

/** facility-0041, 0048: a 403 that names the scanned sign's Area is a Blocked Scan, not a missing permission. */
export function blockedScanOf(error: unknown): BlockedScan | null {
  if (statusOf(error) !== 403) {
    return null;
  }
  const data = (error as { data?: unknown }).data;
  return typeof data === 'object' && data !== null && 'reason' in data && 'areaCode' in data ? (data as BlockedScan) : null;
}

/**
 * facility-0052: no answer, no answer in time, an answer that is not ours, or a server fault. As far as the phone can
 * tell nothing was saved, so the page says "ยังไม่ได้บันทึก" and the person sends again.
 */
export function isNotSent(error: unknown): boolean {
  const status = statusOf(error);
  return (
    status === 'FETCH_ERROR' ||
    status === 'TIMEOUT_ERROR' ||
    status === 'PARSING_ERROR' ||
    (typeof status === 'number' && status >= 500)
  );
}
```

In `frontend/src/shared/api/baseApi.ts`, add after the imports:

```ts
/** facility-0052: a scan or attendance send with no answer in 20 s counts as not sent; the page offers to send again. */
export const SEND_TIMEOUT_MS = 20_000;
```

- [x] **Step 5: The two shared screens**

`frontend/src/shared/components/LocationHelp.tsx`:

```tsx
import type { LocationProblem } from '../geo/readLocation';
import './shared.css';

interface LocationHelpProps {
  problem: LocationProblem;
  /** What waits for the location: "ลงเวลา" or "ส่งงาน". */
  action: string;
  busy: boolean;
  onRetry: () => void;
}

/**
 * Wireframe M2 (facility-0036): without a location nothing is recorded, so the page says how to turn it on and
 * repeats the same press. On plain http no browser gives a location, so there is nothing to retry.
 */
export function LocationHelp({ problem, action, busy, onRetry }: LocationHelpProps) {
  if (problem === 'insecure') {
    return (
      <main className="app-help">
        <h1 className="app-help__title">อ่านตำแหน่งไม่ได้</h1>
        <p className="app-note app-note--error">
          {`หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง ยังไม่ได้${action} แจ้ง Admin`}
        </p>
      </main>
    );
  }
  return (
    <main className="app-help">
      {problem === 'denied' ? (
        <>
          <h1 className="app-help__title">{`เปิด Location ก่อน${action}`}</h1>
          <p>ระบบต้องรู้ตำแหน่งตอนสแกน ถ้าปิดไว้จะยังไม่บันทึก</p>
          <section className="app-panel">
            <h2>Android</h2>
            <ol>
              <li>ปัดแถบด้านบนของจอลงมา</li>
              <li>แตะปุ่ม "ตำแหน่ง" ให้เปิด</li>
              <li>ถ้าเบราว์เซอร์ถาม ให้กด "อนุญาต"</li>
            </ol>
          </section>
          <section className="app-panel">
            <h2>iPhone</h2>
            <ol>
              <li>เปิด การตั้งค่า › ความเป็นส่วนตัว › บริการหาตำแหน่ง</li>
              <li>เปิดบริการหาตำแหน่ง แล้วเลือก Safari ให้ "ขณะใช้งาน"</li>
              <li>กลับมาหน้านี้</li>
            </ol>
          </section>
        </>
      ) : (
        <>
          <h1 className="app-help__title">อ่านตำแหน่งไม่ได้</h1>
          <p>{`มือถือยังหาตำแหน่งไม่เจอ ยังไม่ได้${action} ลองเดินไปใกล้หน้าต่างหรือที่โล่ง แล้วกดลองอีกครั้ง`}</p>
        </>
      )}
      <button type="button" className="app-button app-button--primary" disabled={busy} onClick={onRetry}>
        {problem === 'denied' ? 'เปิดแล้ว ลองอีกครั้ง' : 'ลองอีกครั้ง'}
      </button>
    </main>
  );
}
```

`frontend/src/shared/components/BlockedScanScreen.tsx`:

```tsx
import { Link } from 'react-router';
import type { BlockedScan } from '../api/types';
import './shared.css';

interface BlockedScanScreenProps {
  blocked: BlockedScan;
  /** A cover never lets a Cleaner check in at another Area's sign (facility-0041 item 4), so the advice differs. */
  sign: 'check-in' | 'point';
  ownAreaName?: string;
}

/** Wireframe M8 (facility-0041): nothing was recorded; the Admin already sees this Blocked Scan and can assign a cover. */
export function BlockedScanScreen({ blocked, sign, ownAreaName }: BlockedScanScreenProps) {
  return (
    <main className="app-help">
      <span className="app-help__kicker">{sign === 'check-in' ? 'ป้ายลงเวลา' : 'ป้ายจุดทำความสะอาด'}</span>
      <h1 className="app-help__title">{blocked.message}</h1>
      <p className="app-note app-note--error">{sign === 'check-in' ? 'ไม่ได้บันทึกเวลา' : 'ไม่ได้บันทึกงาน'}</p>
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
      {sign === 'point' ? (
        <section className="app-panel">
          <h2>ถ้าได้รับมอบหมายให้ทำแทน</h2>
          <p>
            {`Admin เห็นการสแกนครั้งนี้แล้ว ให้แจ้ง Admin ว่าคุณทำแทน ${blocked.areaName} เมื่อ Admin กดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ`}
          </p>
        </section>
      ) : (
        <p>ลงเวลาที่ป้ายของ Area ตัวเองเท่านั้น วันที่ทำแทน Area อื่นก็ลงเวลาที่ป้ายของตัวเองตามปกติ Admin เห็นการสแกนครั้งนี้แล้ว</p>
      )}
      <Link className="app-button app-button--primary" to="/my-work">
        กลับไปงานของฉัน
      </Link>
    </main>
  );
}
```

Append to `frontend/src/shared/components/shared.css`:

```css
.app-help {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.app-help__kicker {
  font-size: 13px;
  letter-spacing: 0.04em;
  color: var(--color-muted);
}

.app-help__title {
  font-size: 24px;
  font-weight: 700;
}

.app-facts {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 6px 12px;
  margin: 0;
  padding: 14px 16px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.app-facts dt {
  color: var(--color-muted);
}

.app-facts dd {
  margin: 0;
  font-weight: 600;
}
```

- [x] **Step 6: Write the failing check-in tests**

`frontend/src/pages/check-in/CheckInPage.test.tsx`:

```tsx
import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  attendance,
  cleanerSession,
  entry,
  myWork,
  notCheckedIn,
  onDuty,
  otherAreaBlocked,
  outsideShift,
} from '../../test/cleanerFixtures';
import { fakeGps, farFromSign, nearSign } from '../../test/fakeGps';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

const SIGN = '/check-in/token-checkin-ar01';
const shiftIn0652 = entry('ShiftIn', '2026-10-14T23:52:00Z');

function serve(routes: FakeRoutes, work = myWork(notCheckedIn)) {
  return fakeServer({ ...cleanerSession, 'GET /api/my-work': () => ({ status: 200, body: work }), ...routes });
}

/** The four tiles of "วันนี้", as a person reads them. */
function steps() {
  return within(screen.getByRole('list', { name: 'วันนี้' }))
    .getAllByRole('listitem')
    .map((li) => li.textContent);
}

describe('CheckInPage (M1, M2, M3, M8, M10)', () => {
  it('shows the shift, the own Area and the Shift-In button (M1)', async () => {
    serve({});
    await renderApp(SIGN);

    expect(await screen.findByRole('button', { name: 'เข้างาน' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'ลงเวลา' })).toBeInTheDocument();
    expect(screen.getByText('กะเช้า 07:00–19:00')).toBeInTheDocument();
    expect(screen.getByText('Area ของคุณ: Area ทดลอง · 1 จุด')).toBeInTheDocument();
    expect(steps()).toEqual(['เข้างาน ถัดไป', 'ออกพัก –', 'กลับจากพัก –', 'เลิกงาน –']);
  });

  it('เข้างาน sends the sign token, the event and the GPS, shows the time and offers the next events (M10)', async () => {
    fakeGps(nearSign);
    const server = serve({
      'POST /api/attendance': () => ({ status: 200, body: attendance([shiftIn0652], ['BreakOut', 'ShiftOut'], 'Recorded') }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานแล้ว 06:52')).toBeInTheDocument();
    expect(server.calls.find((c) => c.path === '/api/attendance')?.body).toEqual({
      qrToken: 'token-checkin-ar01',
      eventType: 'ShiftIn',
      latitude: 13.7563,
      longitude: 100.5018,
      accuracyM: 12,
    });
    expect(steps()).toEqual(['เข้างาน 06:52', 'ออกพัก ถัดไป', 'กลับจากพัก –', 'เลิกงาน ถัดไป']);
    expect(screen.getByRole('button', { name: 'ออกพัก' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'เลิกงาน' })).toBeInTheDocument();
    // the answer updated My Work's cached copy; nothing was asked again
    expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(1);
  });

  it('far from the sign: recorded but flagged, with the phone accuracy (M3, facility-0035)', async () => {
    fakeGps(farFromSign);
    serve({
      'POST /api/attendance': () => ({
        status: 200,
        body: attendance([entry('ShiftIn', '2026-10-14T23:52:00Z', false)], ['BreakOut', 'ShiftOut'], 'Recorded'),
      }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานแล้ว 06:52 แต่อยู่ไกลจากป้าย · ติดธง')).toBeInTheDocument();
    expect(screen.getByText('ความแม่นยำของตำแหน่ง ±40 ม.')).toBeInTheDocument();
    expect(steps()[0]).toBe('เข้างาน 06:52 ติดธง');
  });

  it('after the break only เลิกงาน is offered, and then the shift is complete (M10)', async () => {
    fakeGps(nearSign);
    const afterBreak = attendance(
      [shiftIn0652, entry('BreakOut', '2026-10-15T05:01:00Z'), entry('BreakIn', '2026-10-15T05:58:00Z')], // 12:01, 12:58
      ['ShiftOut'],
    );
    serve(
      {
        'POST /api/attendance': () => ({
          status: 200,
          body: attendance([...afterBreak.events, entry('ShiftOut', '2026-10-15T11:55:00Z')], [], 'Recorded'), // 18:55
        }),
      },
      myWork(afterBreak),
    );
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เลิกงาน' }));

    expect(await screen.findByText('บันทึกเวลาเลิกงานแล้ว 18:55')).toBeInTheDocument();
    expect(steps()).toEqual(['เข้างาน 06:52', 'ออกพัก 12:01', 'กลับจากพัก 12:58', 'เลิกงาน 18:55']);
    expect(screen.getByText('ลงเวลาครบแล้วสำหรับกะนี้')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เลิกงาน' })).not.toBeInTheDocument();
  });

  it('a press within 5 minutes of the last one is not counted (facility-0031)', async () => {
    fakeGps(nearSign);
    serve(
      { 'POST /api/attendance': () => ({ status: 200, body: attendance([shiftIn0652], ['BreakOut', 'ShiftOut'], 'TooSoon') }) },
      myWork(onDuty),
    );
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ออกพัก' }));

    expect(
      await screen.findByText('ยังไม่บันทึกออกพัก เพราะเพิ่งลงเวลาไปไม่ถึง 5 นาที กดซ้ำภายใน 5 นาทีไม่นับ'),
    ).toBeInTheDocument();
  });

  it('an event already recorded keeps its first time (facility-0026)', async () => {
    fakeGps(nearSign);
    serve({
      'POST /api/attendance': () => ({
        status: 200,
        body: attendance([shiftIn0652], ['BreakOut', 'ShiftOut'], 'AlreadyRecorded'),
      }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByText('เข้างาน: ลงเวลาไว้แล้วตอน 06:52 ระบบเก็บเวลาแรกไว้')).toBeInTheDocument();
  });

  it('Location off shows how to turn it on, and "เปิดแล้ว ลองอีกครั้ง" records (M2, facility-0036)', async () => {
    const gps = fakeGps('denied', nearSign);
    const server = serve({
      'POST /api/attendance': () => ({ status: 200, body: attendance([shiftIn0652], ['BreakOut', 'ShiftOut'], 'Recorded') }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'เปิด Location ก่อนลงเวลา' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Android' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'iPhone' })).toBeInTheDocument();
    expect(server.calls.some((c) => c.path === '/api/attendance')).toBe(false);

    await user.click(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานแล้ว 06:52')).toBeInTheDocument();
    expect(gps.calls).toBe(2);
  });

  it('on plain http the page says it needs https and offers no retry (facility-0049)', async () => {
    fakeGps(nearSign);
    serve({});
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'อ่านตำแหน่งไม่ได้' })).toBeInTheDocument();
    expect(screen.getByText(/ต้องเปิดผ่าน https/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ลองอีกครั้ง' })).not.toBeInTheDocument();
  });

  it('the check-in sign of another Area is blocked and nothing is recorded (M8, facility-0041)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/attendance': () => ({ status: 403, body: otherAreaBlocked }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'ป้ายนี้ไม่ใช่ Area ของคุณ' })).toBeInTheDocument();
    expect(screen.getByText('ไม่ได้บันทึกเวลา')).toBeInTheDocument();
    expect(screen.getByText('Area ถัดไป (AR02) · ตึก B')).toBeInTheDocument();
    expect(screen.getByText('Area ทดลอง')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับไปงานของฉัน' })).toHaveAttribute('href', '/my-work');
  });

  it('outside the attendance window there is no button (facility-0069)', async () => {
    serve({}, myWork(outsideShift));
    await renderApp(SIGN);

    expect(
      await screen.findByText('ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ ลงเวลาได้ตั้งแต่ 60 นาทีก่อนกะเริ่มจนถึง 60 นาทีหลังกะจบ'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
  });

  it('an out-of-order press shows the server message and asks for the allowed buttons again', async () => {
    fakeGps(nearSign);
    const server = serve(
      { 'POST /api/attendance': () => ({ status: 409, body: { message: 'ลงเวลาไม่ตรงลำดับ', nextEvents: ['ShiftOut'] } }) },
      myWork(onDuty),
    );
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ออกพัก' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ลงเวลาไม่ตรงลำดับ');
    await screen.findByRole('button', { name: 'ออกพัก' });
    expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(2);
  });

  it('no signal: says the time was not recorded and keeps the buttons (facility-0052)', async () => {
    fakeGps(nearSign);
    serve({
      'POST /api/attendance': () => {
        throw new TypeError('Failed to fetch');
      },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'ยังไม่ได้บันทึกเวลา เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่',
    );
    expect(screen.getByRole('button', { name: 'เข้างาน' })).toBeEnabled();
  });

  it('a logged-out Cleaner who scans the sign returns to it after login (facility-0026)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 401 }) });
    const { router } = await renderApp(SIGN);

    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toMatchObject({ from: { pathname: SIGN } });
  });
});
```

Run: `npm test -- src/pages/check-in`
Expected: FAIL, `Failed to resolve import` of the page through the route, or "ไม่พบหน้านี้" instead of the buttons (the route does not exist yet).

- [x] **Step 7: The attendance call**

`frontend/src/pages/check-in/checkInApi.ts`:

```ts
import { baseApi, SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import { myWorkApi } from '../../shared/api/myWorkApi';
import type { AttendanceEvent, AttendanceState } from '../../shared/api/types';
import type { GpsFix } from '../../shared/geo/readLocation';

/** RecordAttendanceRequest. The QR Token comes only from the URL the sign opened (facility-0059). */
export interface RecordAttendanceRequest extends GpsFix {
  qrToken: string;
  eventType: AttendanceEvent;
}

const checkInApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    recordAttendance: build.mutation<AttendanceState, RecordAttendanceRequest>({
      query: (body) => ({ url: '/attendance', method: 'POST', body, timeout: SEND_TIMEOUT_MS }),
      // The answer is the shift's attendance now: My Work's cached copy takes it, so every page agrees at once
      async onQueryStarted(_request, { dispatch, queryFulfilled }) {
        try {
          const { data } = await queryFulfilled;
          dispatch(
            myWorkApi.util.updateQueryData('getMyWork', undefined, (draft) => {
              draft.attendance = data;
            }),
          );
        } catch {
          // the page shows the failure
        }
      },
    }),
  }),
});

export const { useRecordAttendanceMutation } = checkInApi;
```

- [x] **Step 8: The page**

`frontend/src/pages/check-in/AttendanceTimeline.tsx`:

```tsx
import type { AttendanceState } from '../../shared/api/types';
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';

/** The "วันนี้" row of M1 and M10: each event's time, "ถัดไป" for what may come next, a flag mark (facility-0035). */
export function AttendanceTimeline({ attendance }: { attendance: AttendanceState }) {
  return (
    <ol className="checkin-timeline" aria-label="วันนี้">
      {attendanceEvents.map((eventType) => {
        const recorded = attendance.events.find((e) => e.eventType === eventType);
        const next = attendance.nextEvents.includes(eventType);
        const className = recorded ? 'checkin-step checkin-step--done' : next ? 'checkin-step checkin-step--next' : 'checkin-step';
        return (
          <li key={eventType} className={className}>
            <span>{eventLabel[eventType]}</span>{' '}
            <span>{recorded ? formatTime(recorded.occurredAt) : next ? 'ถัดไป' : '–'}</span>
            {recorded?.withinRadius === false && (
              <>
                {' '}
                <span className="checkin-flag">ติดธง</span>
              </>
            )}
          </li>
        );
      })}
    </ol>
  );
}
```

`frontend/src/pages/check-in/CheckInPage.tsx`:

```tsx
import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { freshOnOpen, useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import { blockedScanOf, isNotSent, statusOf } from '../../shared/api/scanAnswers';
import type { AttendanceEvent, AttendanceState, BlockedScan } from '../../shared/api/types';
import { BlockedScanScreen } from '../../shared/components/BlockedScanScreen';
import { LocationHelp } from '../../shared/components/LocationHelp';
import { readLocation, type LocationProblem } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { eventLabel, shiftLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';
import { AttendanceTimeline } from './AttendanceTimeline';
import { useRecordAttendanceMutation } from './checkInApi';
import './checkIn.css';

type Outcome =
  | { kind: 'answered'; eventType: AttendanceEvent; state: AttendanceState; accuracyM: number }
  | { kind: 'failed'; message: string }
  | { kind: 'blocked'; blocked: BlockedScan }
  | { kind: 'location'; problem: LocationProblem; eventType: AttendanceEvent };

/**
 * Wireframes M1, M2, M3, M8 and M10: the page an Area's check-in sign opens (/check-in/<token>, plan 8 decision 1).
 * The buttons are the events the server allows next (facility-0031). The server records, flags or blocks
 * (facility-0035, 0036, 0041) and never computes late or early (facility-0050).
 */
export function CheckInPage() {
  const { token = '' } = useParams();
  const workQuery = useGetMyWorkQuery(undefined, freshOnOpen);
  const [record] = useRecordAttendanceMutation();
  const [pressed, setPressed] = useState<AttendanceEvent | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const work = workQuery.data;
  const attendance = work?.attendance;
  const ownArea = work?.areas.find((area) => !area.isCover);
  // Buttons only from a fresh answer: a cached one may be from before another device recorded something
  const liveAttendance = workQuery.isFetching ? undefined : attendance;

  const press = async (eventType: AttendanceEvent) => {
    setPressed(eventType);
    try {
      const reading = await readLocation();
      if (reading.kind !== 'fix') {
        setOutcome({ kind: 'location', problem: reading.kind, eventType });
        return;
      }
      const answer = await record({ qrToken: token, eventType, ...reading.fix });
      if (answer.data) {
        setOutcome({ kind: 'answered', eventType, state: answer.data, accuracyM: reading.fix.accuracyM });
        return;
      }
      const blocked = blockedScanOf(answer.error);
      if (blocked) {
        setOutcome({ kind: 'blocked', blocked });
        return;
      }
      setOutcome({
        kind: 'failed',
        message: isNotSent(answer.error)
          ? `ยังไม่ได้บันทึกเวลา ${getErrorMessage(answer.error)}`
          : getErrorMessage(answer.error),
      });
      if (statusOf(answer.error) === 409) {
        void workQuery.refetch(); // out of order or outside the window: show what the server allows now
      }
    } finally {
      setPressed(null);
    }
  };

  if (outcome?.kind === 'location') {
    const { problem, eventType } = outcome;
    return <LocationHelp problem={problem} action="ลงเวลา" busy={pressed !== null} onRetry={() => void press(eventType)} />;
  }
  if (outcome?.kind === 'blocked') {
    return <BlockedScanScreen blocked={outcome.blocked} sign="check-in" ownAreaName={ownArea?.areaName} />;
  }

  return (
    <main className="checkin-page">
      <header className="checkin-heading">
        <span className="checkin-kicker">ป้ายลงเวลา</span>
        <h1>ลงเวลา</h1>
        {attendance?.shift && <p className="checkin-sub">{shiftLabel[attendance.shift]}</p>}
        {ownArea && <p className="checkin-area">{`Area ของคุณ: ${ownArea.areaName} · ${ownArea.points.length} จุด`}</p>}
      </header>
      {workQuery.isLoading && <p className="checkin-hint">กำลังโหลด...</p>}
      {workQuery.error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(workQuery.error)}
        </p>
      )}
      {attendance && attendance.shift === null && (
        <p className="app-note app-note--warn">
          ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ ลงเวลาได้ตั้งแต่ 60 นาทีก่อนกะเริ่มจนถึง 60 นาทีหลังกะจบ
        </p>
      )}
      {attendance?.shift && <AttendanceTimeline attendance={attendance} />}
      {outcome?.kind === 'answered' && (
        <AnsweredNote eventType={outcome.eventType} state={outcome.state} accuracyM={outcome.accuracyM} />
      )}
      {outcome?.kind === 'failed' && (
        <p className="app-note app-note--error" role="alert">
          {outcome.message}
        </p>
      )}
      {liveAttendance?.shift && liveAttendance.nextEvents.length === 0 && (
        <p className="app-note app-note--info">ลงเวลาครบแล้วสำหรับกะนี้</p>
      )}
      {liveAttendance && liveAttendance.nextEvents.length > 0 && (
        <div className="checkin-actions">
          {liveAttendance.nextEvents.map((eventType, index) => (
            <button
              key={eventType}
              type="button"
              className={index === 0 ? 'checkin-press checkin-press--main' : 'checkin-press'}
              disabled={pressed !== null}
              onClick={() => void press(eventType)}
            >
              {pressed === eventType ? 'กำลังบันทึก...' : eventLabel[eventType]}
            </button>
          ))}
        </div>
      )}
      <p className="checkin-hint">ระบบแสดงปุ่มถัดไปให้เองตามลำดับ · กดซ้ำภายใน 5 นาทีไม่นับ · ไม่คิดสายหรือกลับก่อน</p>
      <Link className="app-button" to="/my-work">
        ดูงานของฉัน
      </Link>
    </main>
  );
}

interface AnsweredNoteProps {
  eventType: AttendanceEvent;
  state: AttendanceState;
  accuracyM: number;
}

/** What the server did with the press: recorded (maybe flagged, M3), already recorded, or too soon (facility-0026, 0031). */
function AnsweredNote({ eventType, state, accuracyM }: AnsweredNoteProps) {
  const label = eventLabel[eventType];
  const recorded = state.events.find((e) => e.eventType === eventType);
  const time = recorded ? formatTime(recorded.occurredAt) : '';

  if (state.outcome === 'TooSoon') {
    return (
      <p className="app-note app-note--warn" role="status">
        {`ยังไม่บันทึก${label} เพราะเพิ่งลงเวลาไปไม่ถึง 5 นาที กดซ้ำภายใน 5 นาทีไม่นับ`}
      </p>
    );
  }
  if (state.outcome === 'AlreadyRecorded') {
    return (
      <p className="app-note app-note--info" role="status">
        {`${label}: ลงเวลาไว้แล้วตอน ${time} ระบบเก็บเวลาแรกไว้`}
      </p>
    );
  }
  if (recorded?.withinRadius === false) {
    return (
      <div className="app-note app-note--warn" role="status">
        <strong>{`บันทึกเวลา${label}แล้ว ${time} แต่อยู่ไกลจากป้าย · ติดธง`}</strong>
        <span>{`ความแม่นยำของตำแหน่ง ±${Math.round(accuracyM)} ม.`}</span>
        <span>Admin จะเห็นรายการนี้ ถ้าคุณยืนอยู่หน้าป้ายจริง แจ้ง Admin ได้เลย</span>
      </div>
    );
  }
  return (
    <p className="app-note app-note--info" role="status">
      {`บันทึกเวลา${label}แล้ว ${time}`}
    </p>
  );
}
```

`frontend/src/pages/check-in/checkIn.css`:

```css
/* Check-in sign page, wireframes M1, M3 and M10. Class prefix: checkin- */

.checkin-page {
  min-height: calc(100vh - 80px);
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.checkin-heading {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.checkin-heading h1 {
  font-size: 26px;
  font-weight: 700;
}

.checkin-kicker {
  font-size: 13px;
  letter-spacing: 0.04em;
  color: var(--color-muted);
}

.checkin-sub {
  font-size: 15px;
  color: var(--color-muted);
}

.checkin-area {
  font-size: 14px;
  color: var(--color-text-soft);
}

.checkin-timeline {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 6px;
  margin: 0;
  padding: 14px 16px;
  list-style: none;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  text-align: center;
  font-size: 12px;
}

.checkin-step {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 8px 2px;
  border: 1px dashed var(--color-input-border);
  border-radius: var(--radius-sm);
  color: var(--color-muted);
}

.checkin-step--done {
  border-style: solid;
  border-color: var(--color-border);
  color: var(--color-text);
  font-weight: 600;
}

.checkin-step--next {
  border: 2px solid var(--color-primary);
  color: var(--color-primary);
  font-weight: 600;
}

.checkin-flag {
  color: var(--color-warn-text);
  font-weight: 700;
}

.checkin-actions {
  margin-top: auto;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.checkin-press {
  min-height: 56px;
  border: 1px solid var(--color-primary);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-primary);
  font-size: 18px;
  font-weight: 700;
  cursor: pointer;
}

.checkin-press--main {
  min-height: 72px;
  background: var(--color-primary);
  color: var(--color-on-primary);
  font-size: 22px;
}

.checkin-press:disabled {
  opacity: 0.6;
  cursor: wait;
}

.checkin-hint {
  font-size: 13px;
  color: var(--color-muted);
  text-align: center;
}
```

`frontend/src/pages/check-in/index.ts`:

```ts
export { CheckInPage } from './CheckInPage';
```

- [x] **Step 9: The route**

In `frontend/src/router.tsx`, add the import below `import { AdminLoginPage, LoginPage } from './pages/auth';`:

```tsx
import { CheckInPage } from './pages/check-in';
```

and replace the Cleaner block:

```tsx
      {
        element: <ProtectedRoute roles={['cleaner']} />,
        children: [{ element: <AppLayout />, children: [{ path: 'my-work', element: <MyWorkPage /> }] }],
      },
```

with:

```tsx
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
```

- [x] **Step 10: Run the tests, the typecheck and the lint**

```bash
npm test          # expect: 76 passed (60 + 3 GPS + 13 check-in)
npx tsc -b        # expect: no output
npm run lint      # expect: no output
```

- [x] **Step 11: Handoff entry**

```markdown
- <date> · Task 2 done: check-in sign page /check-in/:token (M1, M3, M10): the shift's four events, buttons for what the server allows next, a fresh GPS reading per press, flag with the phone accuracy, already recorded and too-soon answers; Location off (M2), plain http, blocked sign (M8), outside the window, out of order (reloads the buttons), no signal. Shared readLocation, fakeGps, LocationHelp, BlockedScanScreen, scan answers. Frontend tests 76 passed. Next: Task 3 (scan page).
```

- [x] **Step 12: Commit**

```bash
git add frontend/src docs/handoff.md
git commit -m "feat(frontend): check-in sign page with the four attendance events, GPS on every press, flag, Location help and blocked sign (facility-0031, 0035, 0036, 0041)"
```

---

### Task 3: Point sign page: open, check duty, submit (M2, M4, M5, M11, M13)

A point's sign opens `/scan/<token>`. The page shows the point, refuses early when the Cleaner is not on duty, and sends the submission in two taps.

**Files:**
- Modify: `frontend/src/router.tsx`
- Create: `frontend/src/pages/scan/scanApi.ts`, `frontend/src/pages/scan/scanRules.ts`, `frontend/src/pages/scan/PointHeading.tsx`, `frontend/src/pages/scan/SubmitForm.tsx`, `frontend/src/pages/scan/SubmitResult.tsx`, `frontend/src/pages/scan/OffDuty.tsx`, `frontend/src/pages/scan/ScanPage.tsx`, `frontend/src/pages/scan/scan.css`, `frontend/src/pages/scan/index.ts`
- Test: `frontend/src/pages/scan/ScanPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes (Tasks 1–2): `PointCard`, `PointStatus`, `CleaningStatus`, `Placement`, `AttendanceState` types; `useGetMyWorkQuery`, `freshOnOpen`; `readLocation`, `GpsFix`, `LocationProblem`; `SEND_TIMEOUT_MS`; `LocationHelp`; `StatusChip`; `StatusScreen` (plan 7); `formatTime`, `formatClock`, `formatRound`, `formatMinutes`; `getErrorMessage`; fixtures and `fakeGps`.
- Produces (Task 4 relies on these names):
  - `useGetPointByTokenQuery(token, freshOnOpen)`, `useSubmitScanMutation()`, `SubmitScanRequest`, `ScanRecordCreated` (`pages/scan/scanApi.ts`).
  - `ScanDraft = { status: CleaningStatus; tags: string[]; note: string }`, `emptyDraft`, `issueTags`, `isComplete(draft)`, `describeDraft(draft): string`, `requestOf(qrToken, draft, fix): SubmitScanRequest`, `OffDutyReason`, `offDutyReason(attendance): OffDutyReason | null` (`pages/scan/scanRules.ts`).
  - `<PointHeading point />`, `<SubmitForm point draft onChange sending errorText onSend />`, `<SubmitResult point draft result />`, `<OffDuty reason point />`.
  - In `ScanPage.tsx`: `type Outcome` and `function outcomeOf(answer)` (Task 4 widens both).
  - Route `/scan/:token` (Cleaner only).
  - In `ScanPage.test.tsx`: `SIGN`, `serve(routes?, card?, work?)` and `saved(overrides?)` (Task 4 appends a `describe` that uses them).

- [x] **Step 1: Write the failing scan tests**

`frontend/src/pages/scan/ScanPage.test.tsx`:

```tsx
import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { checkedOut, cleanerSession, myWork, notCheckedIn, onDuty, outsideShift, point } from '../../test/cleanerFixtures';
import { fakeGps, nearSign } from '../../test/fakeGps';
import { authAnswer, fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';
import type { ScanRecordCreated } from './scanApi';

const SIGN = '/scan/token-restroom-m1';

function serve(routes: FakeRoutes = {}, card = point(), work = myWork(onDuty)) {
  return fakeServer({
    ...cleanerSession,
    'GET /api/service-points/by-token/token-restroom-m1': () => ({ status: 200, body: card }),
    'GET /api/my-work': () => ({ status: 200, body: work }),
    ...routes,
  });
}

/** A 201 answer of POST /api/scan-records; 03:05Z is 10:05 Thai time. */
function saved(overrides: Partial<ScanRecordCreated> = {}): ScanRecordCreated {
  return {
    scanRecordId: 501,
    servicePointId: 11,
    placement: 'OnTime',
    lateMinutes: null,
    newPointStatus: 'PendingInspection',
    distanceM: 6,
    withinRadius: true,
    submittedAt: '2026-10-15T03:05:00Z',
    ...overrides,
  };
}

describe('ScanPage (M2, M4, M5, M11, M13)', () => {
  it('opens the point with เรียบร้อย chosen and sends in two taps (M4 → M5, facility-0007)', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved() }) });
    const { user } = await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(screen.getByText('Area ทดลอง · ตึก A')).toBeInTheDocument();
    expect(screen.getByText('รอบ 10:00–12:00 · ส่งหลัง 12:00 นับเป็นส่งช้า')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'เรียบร้อย' })).toBeChecked();

    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' })).toBeInTheDocument();
    expect(server.calls.find((c) => c.path === '/api/scan-records')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      status: 'Normal',
      latitude: 13.7563,
      longitude: 100.5018,
      accuracyM: 12,
    });
    expect(screen.getByText('ห้องน้ำชาย · เรียบร้อย')).toBeInTheDocument();
    expect(screen.getByText('รอตรวจ')).toBeInTheDocument();
    expect(screen.getByText('ห่างป้าย 6 ม. · อยู่ในรัศมี')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'ดูงานของฉัน' })).toHaveAttribute('href', '/my-work');
  });

  it('พบปัญหา sends the chosen tags and the note (facility-0046)', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'พบปัญหา' }));
    await user.click(screen.getByRole('checkbox', { name: 'น้ำยาหมด' }));
    await user.click(screen.getByRole('checkbox', { name: 'ของชำรุด' }));
    await user.type(screen.getByLabelText('หมายเหตุ'), ' ก๊อกรั่ว ');
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' });
    expect(server.calls.find((c) => c.path === '/api/scan-records')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      status: 'Issue',
      issueTags: ['น้ำยาหมด', 'ของชำรุด'],
      notes: 'ก๊อกรั่ว',
      latitude: 13.7563,
      longitude: 100.5018,
      accuracyM: 12,
    });
    expect(screen.getByText('ห้องน้ำชาย · พบปัญหา: น้ำยาหมด · ของชำรุด · ก๊อกรั่ว')).toBeInTheDocument();
  });

  it('พบปัญหา needs a tag or a note before anything is sent', async () => {
    fakeGps(nearSign);
    const server = serve();
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'พบปัญหา' }));
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(screen.getByRole('alert')).toHaveTextContent('เลือกปัญหาที่พบอย่างน้อย 1 อย่าง หรือเขียนหมายเหตุ');
    expect(server.calls.some((c) => c.path === '/api/scan-records')).toBe(false);
  });

  it('a point the Supervisor sent back shows the defect, and the fix counts as rework (M4, facility-0029)', async () => {
    fakeGps(nearSign);
    serve(
      { 'POST /api/scan-records': () => ({ status: 201, body: saved({ placement: 'Rework', submittedAt: '2026-10-15T03:30:00Z' }) }) },
      point({ status: 'Rework', rework: { defect: 'พื้นใต้อ่างล้างมือยังเปียก', requestedAt: '2026-10-15T03:15:00Z' } }),
    );
    const { user } = await renderApp(SIGN);

    expect(await screen.findByText('หัวหน้าให้แก้ไข · 10:15')).toBeInTheDocument();
    expect(screen.getByText('“พื้นใต้อ่างล้างมือยังเปียก”')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ส่งงานแก้แล้ว 10:30' })).toBeInTheDocument();
    expect(screen.getByText('นับเป็นงานแก้ของรอบนี้ หัวหน้าจะตรวจอีกครั้ง')).toBeInTheDocument();
  });

  it('a late submission says how late (M13, facility-0047)', async () => {
    fakeGps(nearSign);
    serve(
      {
        'POST /api/scan-records': () => ({
          status: 201,
          body: saved({ placement: 'Late', lateMinutes: 25, submittedAt: '2026-10-15T05:25:00Z' }),
        }),
      },
      point({ status: 'Overdue' }),
    );
    const { user } = await renderApp(SIGN);

    expect(await screen.findByText('รอบ 10:00–12:00 · เลยเวลารอบแล้ว ส่งตอนนี้นับเป็นส่งช้า')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ส่งงานแล้ว 12:25' })).toBeInTheDocument();
    expect(screen.getByText('ส่งช้า 25 นาที')).toBeInTheDocument();
  });

  it('an off-round submission is kept but changes no card (M13, facility-0047)', async () => {
    fakeGps(nearSign);
    serve({
      'POST /api/scan-records': () => ({
        status: 201,
        body: saved({ placement: 'OffRound', newPointStatus: 'Passed', submittedAt: '2026-10-15T06:20:00Z' }),
      }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกเป็นงานนอกรอบ 13:20' })).toBeInTheDocument();
    expect(
      screen.getByText('ตอนนี้ไม่มีรอบไหนรับงานนี้ ระบบบันทึกไว้แล้ว แต่ไม่นับเป็นรอบใด และสถานะของจุดไม่เปลี่ยน'),
    ).toBeInTheDocument();
  });

  it('far from the sign: saved but flagged, with the distance (facility-0037)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved({ distanceM: 85, withinRadius: false }) }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));

    expect(
      await screen.findByText('ติดธง: ห่างป้าย 85 ม. Admin จะเห็นรายการนี้ ถ้าคุณอยู่ที่จุดนี้จริง แจ้ง Admin ได้เลย'),
    ).toBeInTheDocument();
  });

  it('before Shift-In the point cannot be submitted, and nothing is sent (M11)', async () => {
    const server = serve({}, point(), myWork(notCheckedIn));
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ยังไม่ได้ลงเวลาเข้างาน' })).toBeInTheDocument();
    expect(screen.getByText('ป้ายจุดทำความสะอาด · ห้องน้ำชาย · Area ทดลอง')).toBeInTheDocument();
    expect(screen.getByText('ไม่ได้บันทึกงาน')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ส่งงาน' })).not.toBeInTheDocument();
    expect(server.calls.some((c) => c.path === '/api/scan-records')).toBe(false);
  });

  it('after Shift-Out the point cannot be submitted', async () => {
    serve({}, point(), myWork(checkedOut));
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ลงเวลาเลิกงานแล้ว' })).toBeInTheDocument();
  });

  it('outside the own shift the point cannot be submitted (facility-0069)', async () => {
    serve({}, point(), myWork(outsideShift));
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ตอนนี้ไม่อยู่ในกะของคุณ' })).toBeInTheDocument();
  });

  it('an unknown or closed sign shows the server message', async () => {
    serve({
      'GET /api/service-points/by-token/token-restroom-m1': () => ({
        status: 404,
        body: { message: 'ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว' },
      }),
    });
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว' })).toBeInTheDocument();
  });

  it('Location off shows how to turn it on, then sends (M2, facility-0036)', async () => {
    fakeGps('denied', nearSign);
    serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));
    expect(await screen.findByRole('heading', { name: 'เปิด Location ก่อนส่งงาน' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    expect(await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' })).toBeInTheDocument();
  });

  it('a logged-out Cleaner who scans the sign returns to it after login (facility-0017, 0026)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 401 }) });
    const { router } = await renderApp(SIGN);

    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toMatchObject({ from: { pathname: SIGN } });
  });

  it('a Supervisor sees no-permission until plan 9 adds inspection here (plan 8 decision 2)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('supervisor') });
    await renderApp(SIGN);

    expect(screen.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeInTheDocument();
    expect(screen.getByText('หน้านี้สำหรับแม่บ้าน บัญชีนี้เป็นหัวหน้างาน')).toBeInTheDocument();
  });
});
```

Run: `npm test -- src/pages/scan`
Expected: FAIL, `Failed to resolve import "./scanApi"`.

- [x] **Step 2: The calls and the rules**

`frontend/src/pages/scan/scanApi.ts`:

```ts
import { baseApi, SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import type { CleaningStatus, Placement, PointCard, PointStatus } from '../../shared/api/types';
import type { GpsFix } from '../../shared/geo/readLocation';

/** CreateScanRecordRequest. "notes" keeps the phase-1 field name. Who scanned comes from the token (facility-0017). */
export interface SubmitScanRequest extends GpsFix {
  qrToken: string;
  status: CleaningStatus;
  issueTags?: string[];
  notes?: string;
}

/** ScanRecordCreatedResponse (201). withinRadius false is a Geofence Flag; null means the sign has no coordinates yet. */
export interface ScanRecordCreated {
  scanRecordId: number;
  servicePointId: number;
  placement: Placement;
  lateMinutes: number | null;
  newPointStatus: PointStatus;
  distanceM: number | null;
  withinRadius: boolean | null;
  submittedAt: string;
}

const scanApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    // facility-0059: the token comes only from the URL the sign's QR opened
    getPointByToken: build.query<PointCard, string>({
      query: (token) => `/service-points/by-token/${encodeURIComponent(token)}`,
    }),
    submitScan: build.mutation<ScanRecordCreated, SubmitScanRequest>({
      query: (body) => ({ url: '/scan-records', method: 'POST', body, timeout: SEND_TIMEOUT_MS }),
    }),
  }),
});

export const { useGetPointByTokenQuery, useSubmitScanMutation } = scanApi;
```

`frontend/src/pages/scan/scanRules.ts`:

```ts
import type { AttendanceState, CleaningStatus } from '../../shared/api/types';
import type { GpsFix } from '../../shared/geo/readLocation';
import type { SubmitScanRequest } from './scanApi';

/** The form's choices (facility-0024: a draft lives in useState). They stay through a failed send (facility-0052). */
export interface ScanDraft {
  status: CleaningStatus;
  tags: string[];
  note: string;
}

/** facility-0007: เรียบร้อย is already chosen, so a clean point is two taps. */
export const emptyDraft: ScanDraft = { status: 'Normal', tags: [], note: '' };

/** Wireframe M4's quick tags, stored as the words the Cleaner sees (facility-0046). */
export const issueTags: readonly string[] = ['ขยะล้น', 'น้ำยาหมด', 'พื้นเปียก', 'ของชำรุด'];

/** พบปัญหา needs something to report: a tag or a note (plan 8 decision 7). */
export function isComplete(draft: ScanDraft): boolean {
  return draft.status === 'Normal' || draft.tags.length > 0 || draft.note.trim() !== '';
}

export function describeDraft(draft: ScanDraft): string {
  if (draft.status === 'Normal') {
    return 'เรียบร้อย';
  }
  return `พบปัญหา: ${[...draft.tags, draft.note.trim()].filter((part) => part !== '').join(' · ')}`;
}

/** Tags and the note go only with พบปัญหา. */
export function requestOf(qrToken: string, draft: ScanDraft, fix: GpsFix): SubmitScanRequest {
  if (draft.status === 'Normal') {
    return { qrToken, status: 'Normal', ...fix };
  }
  const note = draft.note.trim();
  return { qrToken, status: 'Issue', issueTags: draft.tags, ...(note === '' ? {} : { notes: note }), ...fix };
}

export type OffDutyReason = 'outside-shift' | 'not-checked-in' | 'checked-out';

/**
 * The server's 409s, asked before the form (plan 8 decision 4): a Cleaner submits from Shift-In until Shift-Out in
 * their own shift's window; a break does not stop it (AttendanceRules.IsOnDuty, facility-0069).
 */
export function offDutyReason(attendance: AttendanceState): OffDutyReason | null {
  if (attendance.shift === null) {
    return 'outside-shift';
  }
  const recorded = attendance.events.map((entry) => entry.eventType);
  if (!recorded.includes('ShiftIn')) {
    return 'not-checked-in';
  }
  return recorded.includes('ShiftOut') ? 'checked-out' : null;
}
```

- [x] **Step 3: The four views**

`frontend/src/pages/scan/PointHeading.tsx`:

```tsx
import type { PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatClock, formatRound } from '../../shared/utils/thaiTime';

/** The top of M4 and M12: where the point is, its name, its status and its round. */
export function PointHeading({ point }: { point: PointCard }) {
  return (
    <header className="scan-heading">
      <span className="scan-kicker">{`${point.areaName} · ตึก ${point.buildingCode}`}</span>
      <h1>{point.name}</h1>
      <p className="scan-status">
        <StatusChip status={point.status} />
        <span>{roundLine(point)}</span>
      </p>
    </header>
  );
}

/** A hint only; the server decides the placement and the result page shows it (facility-0047 rule 3). */
function roundLine(point: PointCard): string {
  if (!point.currentRound) {
    return point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : 'ตอนนี้จุดนี้ไม่มีรอบ';
  }
  const round = `รอบ ${formatRound(point.currentRound)}`;
  if (point.status === 'NotYetDone') {
    return `${round} · ส่งหลัง ${formatClock(point.currentRound.end)} นับเป็นส่งช้า`;
  }
  if (point.status === 'Overdue') {
    return `${round} · เลยเวลารอบแล้ว ส่งตอนนี้นับเป็นส่งช้า`;
  }
  return round;
}
```

`frontend/src/pages/scan/SubmitForm.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import type { PointCard } from '../../shared/api/types';
import { formatTime } from '../../shared/utils/thaiTime';
import { PointHeading } from './PointHeading';
import { isComplete, issueTags, type ScanDraft } from './scanRules';

interface SubmitFormProps {
  point: PointCard;
  draft: ScanDraft;
  onChange: (draft: ScanDraft) => void;
  sending: boolean;
  /** The server's refusal of the last send, shown as it is (facility-0022 item 4). */
  errorText: string | null;
  onSend: () => void;
}

/** Wireframe M4: เรียบร้อย is chosen; พบปัญหา opens the tags and the note (facility-0007, 0046). */
export function SubmitForm({ point, draft, onChange, sending, errorText, onSend }: SubmitFormProps) {
  const [incomplete, setIncomplete] = useState(false);

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!isComplete(draft)) {
      setIncomplete(true);
      return;
    }
    setIncomplete(false);
    onSend();
  };

  const toggleTag = (tag: string) =>
    onChange({ ...draft, tags: draft.tags.includes(tag) ? draft.tags.filter((t) => t !== tag) : [...draft.tags, tag] });

  return (
    <main className="scan-page">
      <PointHeading point={point} />
      {point.rework && (
        <div className="app-note app-note--error">
          <strong>{`หัวหน้าให้แก้ไข · ${formatTime(point.rework.requestedAt)}`}</strong>
          <span>{`“${point.rework.defect}”`}</span>
        </div>
      )}
      <form className="scan-form" onSubmit={onSubmit}>
        <fieldset className="scan-choice">
          <legend>ผลการทำความสะอาด</legend>
          <label>
            <input
              type="radio"
              name="scan-status"
              checked={draft.status === 'Normal'}
              onChange={() => onChange({ ...draft, status: 'Normal' })}
            />{' '}
            เรียบร้อย
          </label>
          <label>
            <input
              type="radio"
              name="scan-status"
              checked={draft.status === 'Issue'}
              onChange={() => onChange({ ...draft, status: 'Issue' })}
            />{' '}
            พบปัญหา
          </label>
        </fieldset>
        {draft.status === 'Issue' && (
          <fieldset className="scan-issue">
            <legend>ปัญหาที่พบ</legend>
            <div className="scan-tags">
              {issueTags.map((tag) => (
                <label key={tag}>
                  <input type="checkbox" checked={draft.tags.includes(tag)} onChange={() => toggleTag(tag)} /> {tag}
                </label>
              ))}
            </div>
            <label htmlFor="scan-note">หมายเหตุ</label>
            <textarea
              id="scan-note"
              rows={3}
              maxLength={1000}
              value={draft.note}
              onChange={(event) => onChange({ ...draft, note: event.target.value })}
            />
          </fieldset>
        )}
        {incomplete && (
          <p className="app-note app-note--error" role="alert">
            เลือกปัญหาที่พบอย่างน้อย 1 อย่าง หรือเขียนหมายเหตุ
          </p>
        )}
        {errorText && (
          <p className="app-note app-note--error" role="alert">
            {errorText}
          </p>
        )}
        <button type="submit" className="scan-submit" disabled={sending}>
          {sending ? 'กำลังส่ง...' : 'ส่งงาน'}
        </button>
      </form>
      <p className="scan-hint">ถ้าไม่มีสัญญาณ หน้าจอจะขึ้น "ยังไม่ได้บันทึก" พร้อมปุ่มส่งใหม่ เวลาที่บันทึกคือตอนที่ส่งสำเร็จ</p>
    </main>
  );
}
```

`frontend/src/pages/scan/SubmitResult.tsx`:

```tsx
import { Link } from 'react-router';
import type { Placement, PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatMinutes, formatTime } from '../../shared/utils/thaiTime';
import type { ScanRecordCreated } from './scanApi';
import { describeDraft, type ScanDraft } from './scanRules';

const title: Record<Placement, string> = {
  OnTime: 'ส่งงานแล้ว',
  Late: 'ส่งงานแล้ว',
  Rework: 'ส่งงานแก้แล้ว',
  OffRound: 'บันทึกเป็นงานนอกรอบ',
};

interface SubmitResultProps {
  point: PointCard;
  draft: ScanDraft;
  result: ScanRecordCreated;
}

/** Wireframes M5 and M13: the server placed the submission (facility-0047 rule 3) and judged the distance (facility-0037). */
export function SubmitResult({ point, draft, result }: SubmitResultProps) {
  return (
    <main className="scan-page">
      <header className="scan-heading">
        <span className="scan-kicker">{`${point.areaName} · ตึก ${point.buildingCode}`}</span>
        <h1>{`${title[result.placement]} ${formatTime(result.submittedAt)}`}</h1>
        <p className="scan-sub">{`${point.name} · ${describeDraft(draft)}`}</p>
      </header>
      {result.placement === 'Late' && (
        <p className="app-note app-note--warn">
          <strong>{`ส่งช้า ${formatMinutes(result.lateMinutes ?? 0)}`}</strong>
          <span>รอบนี้จบแล้วและยังไม่มีใครส่ง จึงนับเป็นงานของรอบนี้ แต่ติดว่าส่งช้า</span>
        </p>
      )}
      {result.placement === 'Rework' && <p className="app-note app-note--info">นับเป็นงานแก้ของรอบนี้ หัวหน้าจะตรวจอีกครั้ง</p>}
      {result.placement === 'OffRound' && (
        <p className="app-note app-note--warn">
          ตอนนี้ไม่มีรอบไหนรับงานนี้ ระบบบันทึกไว้แล้ว แต่ไม่นับเป็นรอบใด และสถานะของจุดไม่เปลี่ยน
        </p>
      )}
      {result.withinRadius === false && (
        <p className="app-note app-note--warn">
          {`ติดธง: ห่างป้าย ${result.distanceM} ม. Admin จะเห็นรายการนี้ ถ้าคุณอยู่ที่จุดนี้จริง แจ้ง Admin ได้เลย`}
        </p>
      )}
      {result.withinRadius === true && <p className="scan-muted">{`ห่างป้าย ${result.distanceM} ม. · อยู่ในรัศมี`}</p>}
      <section className="app-panel">
        <h2>สถานะของจุดนี้</h2>
        <p>
          <StatusChip status={result.newPointStatus} />
        </p>
      </section>
      <Link className="app-button app-button--primary" to="/my-work">
        ดูงานของฉัน
      </Link>
    </main>
  );
}
```

`frontend/src/pages/scan/OffDuty.tsx`:

```tsx
import { Link } from 'react-router';
import type { PointCard } from '../../shared/api/types';
import type { OffDutyReason } from './scanRules';

const text: Record<OffDutyReason, { title: string; detail: string }> = {
  'not-checked-in': { title: 'ยังไม่ได้ลงเวลาเข้างาน', detail: 'ต้องสแกนป้ายลงเวลาของ Area ก่อน แล้วค่อยสแกนจุดนี้' },
  'checked-out': { title: 'ลงเวลาเลิกงานแล้ว', detail: 'ส่งงานได้ตั้งแต่ลงเวลาเข้างานจนถึงลงเวลาเลิกงาน' },
  'outside-shift': { title: 'ตอนนี้ไม่อยู่ในกะของคุณ', detail: 'ส่งงานได้เฉพาะในกะของตัวเอง หลังลงเวลาเข้างาน' },
};

/** Wireframe M11: nothing is sent, because the server would refuse it (409). No link to the check-in sign (facility-0059). */
export function OffDuty({ reason, point }: { reason: OffDutyReason; point: PointCard }) {
  return (
    <main className="scan-page">
      <header className="scan-heading">
        <span className="scan-kicker">{`ป้ายจุดทำความสะอาด · ${point.name} · ${point.areaName}`}</span>
        <h1>{text[reason].title}</h1>
      </header>
      <p className="app-note app-note--error">
        <strong>ไม่ได้บันทึกงาน</strong>
        <span>{text[reason].detail}</span>
      </p>
      {reason === 'not-checked-in' && (
        <>
          <p>เดินไปที่ป้ายลงเวลา สแกนด้วยกล้องมือถือ กด "เข้างาน" แล้วกลับมาสแกนจุดนี้ใหม่</p>
          <p className="scan-muted">
            ถ้าลงเวลาไม่ได้ (มือถือแบตหมด ไม่มีเน็ต) แจ้ง Admin ให้เพิ่มเวลาเข้างานให้ Admin ต้องใส่เหตุผลทุกครั้ง
          </p>
        </>
      )}
      <Link className="app-button app-button--primary" to="/my-work">
        ดูงานของฉัน
      </Link>
    </main>
  );
}
```

- [x] **Step 4: The page, its CSS, its index and its route**

`frontend/src/pages/scan/ScanPage.tsx`:

```tsx
import { useState } from 'react';
import { useParams } from 'react-router';
import { freshOnOpen, useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import { LocationHelp } from '../../shared/components/LocationHelp';
import { StatusScreen } from '../../shared/components/StatusScreen';
import { readLocation, type LocationProblem } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { OffDuty } from './OffDuty';
import { SubmitForm } from './SubmitForm';
import { SubmitResult } from './SubmitResult';
import { useGetPointByTokenQuery, useSubmitScanMutation, type ScanRecordCreated } from './scanApi';
import { emptyDraft, offDutyReason, requestOf, type ScanDraft } from './scanRules';
import './scan.css';

type Outcome =
  | { kind: 'saved'; result: ScanRecordCreated }
  | { kind: 'refused'; message: string }
  | { kind: 'location'; problem: LocationProblem };

/**
 * Wireframes M2, M4, M5, M11 and M13: the page a point's QR Sign opens (/scan/<token>, facility-0026). The Cleaner
 * presses ส่งงาน; the server decides the round, the flag and whether the Area is theirs (facility-0037, 0041, 0047).
 */
export function ScanPage() {
  const { token = '' } = useParams();
  const pointQuery = useGetPointByTokenQuery(token, freshOnOpen);
  const workQuery = useGetMyWorkQuery(undefined, freshOnOpen);
  const [submit] = useSubmitScanMutation();
  const [draft, setDraft] = useState<ScanDraft>(emptyDraft);
  const [sending, setSending] = useState(false);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const send = async () => {
    setSending(true);
    try {
      const reading = await readLocation();
      if (reading.kind !== 'fix') {
        setOutcome({ kind: 'location', problem: reading.kind });
        return;
      }
      setOutcome(outcomeOf(await submit(requestOf(token, draft, reading.fix))));
    } finally {
      setSending(false);
    }
  };

  if (pointQuery.error) {
    return (
      <StatusScreen
        title={getErrorMessage(pointQuery.error)}
        detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้ง Admin"
        linkTo="/my-work"
        linkText="ดูงานของฉัน"
      />
    );
  }
  if (workQuery.error && !workQuery.data) {
    return <StatusScreen title="เปิดหน้านี้ไม่ได้" detail={getErrorMessage(workQuery.error)} linkTo="/my-work" linkText="ดูงานของฉัน" />;
  }
  const point = pointQuery.data;
  const work = workQuery.data;
  // Decide only on fresh answers: the cache may hold My Work from before the Cleaner checked in
  if (!point || !work || pointQuery.isFetching || workQuery.isFetching) {
    return <p className="app-splash">กำลังเปิดป้าย...</p>;
  }

  if (outcome?.kind === 'saved') {
    return <SubmitResult point={point} draft={draft} result={outcome.result} />;
  }
  if (outcome?.kind === 'location') {
    return <LocationHelp problem={outcome.problem} action="ส่งงาน" busy={sending} onRetry={() => void send()} />;
  }
  const offDuty = offDutyReason(work.attendance);
  if (offDuty) {
    return <OffDuty reason={offDuty} point={point} />;
  }
  return (
    <SubmitForm
      point={point}
      draft={draft}
      onChange={setDraft}
      sending={sending}
      errorText={outcome?.kind === 'refused' ? outcome.message : null}
      onSend={() => void send()}
    />
  );
}

function outcomeOf(answer: { data?: ScanRecordCreated; error?: unknown }): Outcome {
  if (answer.data) {
    return { kind: 'saved', result: answer.data };
  }
  return { kind: 'refused', message: getErrorMessage(answer.error) };
}
```

`frontend/src/pages/scan/scan.css`:

```css
/* Point sign page, wireframes M4, M5, M11, M12 and M13. Class prefix: scan- */

.scan-page {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.scan-heading {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.scan-heading h1 {
  font-size: 26px;
  font-weight: 700;
}

.scan-kicker {
  font-size: 13px;
  letter-spacing: 0.04em;
  color: var(--color-muted);
}

.scan-sub {
  font-size: 15px;
  color: var(--color-muted);
}

.scan-status {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--color-text-soft);
}

.scan-form {
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.scan-choice {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
  margin: 0;
  padding: 0;
  border: 0;
}

.scan-choice legend {
  margin-bottom: 8px;
  font-size: 15px;
  font-weight: 600;
}

.scan-choice label {
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

.scan-choice label:has(input:checked) {
  border: 2px solid var(--color-primary);
  background: var(--color-info-bg);
  color: var(--color-primary);
}

.scan-issue {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0;
  padding: 14px 16px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.scan-issue legend {
  padding: 0 4px;
  font-size: 15px;
  font-weight: 600;
}

.scan-tags {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.scan-tags label {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 44px;
  padding: 0 10px;
  border: 1px solid var(--color-input-border);
  border-radius: var(--radius-sm);
}

.scan-issue textarea {
  padding: 8px 12px;
  font-size: 16px;
  border: 1px solid var(--color-input-border);
  border-radius: var(--radius-md);
  resize: vertical;
}

.scan-submit {
  min-height: 72px;
  border: 0;
  border-radius: var(--radius-lg);
  background: var(--color-primary);
  color: var(--color-on-primary);
  font-size: 22px;
  font-weight: 700;
  cursor: pointer;
}

.scan-submit:disabled {
  opacity: 0.6;
  cursor: wait;
}

.scan-hint,
.scan-muted {
  font-size: 13px;
  color: var(--color-muted);
}

.scan-hint {
  text-align: center;
}
```

`frontend/src/pages/scan/index.ts`:

```ts
export { ScanPage } from './ScanPage';
```

In `frontend/src/router.tsx`, add below the `CheckInPage` import:

```tsx
import { ScanPage } from './pages/scan';
```

and in the Cleaner block's `children`, after the `check-in/:token` line, add:

```tsx
              // Printed on each point's sign (facility-0026). Cleaner only until plan 9 adds the Supervisor's inspection
              { path: 'scan/:token', element: <ScanPage /> },
```

- [x] **Step 5: Run the tests, the typecheck and the lint**

```bash
npm test          # expect: 90 passed (76 + 14 scan)
npx tsc -b        # expect: no output
npm run lint      # expect: no output
```

- [x] **Step 6: Handoff entry**

```markdown
- <date> · Task 3 done: point sign page /scan/:token (M4, M5, M11, M13): the point with its round hint and rework defect, เรียบร้อย chosen (two taps), พบปัญหา with tags and note (a tag or a note required), result by placement (on time, late with minutes, rework, off-round) with distance or flag; not checked in, checked out and outside the shift refused before the form; unknown sign; Location help (M2). Supervisors get no-permission until plan 9. Frontend tests 90 passed. Next: Task 4 (send failures and blocked point).
```

- [x] **Step 7: Commit**

```bash
git add frontend/src docs/handoff.md
git commit -m "feat(frontend): point sign page: two-tap submission, issue tags, duty check before the form, result by placement and flag (facility-0007, 0046, 0047)"
```

---

### Task 4: When the send does not go through (M8, M12)

No signal or a server fault keeps the choices and offers "ส่งใหม่"; another Area's point shows the blocked screen; a server refusal stays on the form; a second tap never sends twice.

**Files:**
- Create: `frontend/src/pages/scan/SendFailed.tsx`
- Modify: `frontend/src/pages/scan/ScanPage.tsx` (whole file below), `frontend/src/pages/scan/ScanPage.test.tsx` (append), `docs/handoff.md`

**Interfaces:**
- Consumes: Task 3's `PointHeading`, `describeDraft`, `ScanDraft`, `outcomeOf`, `Outcome`, test helpers `SIGN`, `serve`, `saved`; Task 2's `blockedScanOf`, `isNotSent`, `BlockedScanScreen`; Task 1's `otherAreaBlocked` fixture.
- Produces: `<SendFailed point draft triedAt sending onResend />`; `Outcome` gains `{ kind: 'blocked'; blocked: BlockedScan }` and `{ kind: 'not-sent'; triedAt: Date }`; `outcomeOf(answer, triedAt)`.

- [x] **Step 1: Write the failing tests**

In `frontend/src/pages/scan/ScanPage.test.tsx`, add `otherAreaBlocked` to the import from `'../../test/cleanerFixtures'`, then append at the end of the file:

```tsx
describe('ScanPage when the send does not go through (M8, M12)', () => {
  it('no signal: ยังไม่ได้บันทึก keeps the choices, and ส่งใหม่ sends them again with a fresh GPS reading (M12, facility-0052)', async () => {
    const gps = fakeGps(nearSign);
    let tries = 0;
    const server = serve({
      'POST /api/scan-records': () => {
        tries += 1;
        if (tries === 1) {
          throw new TypeError('Failed to fetch');
        }
        return { status: 201, body: saved() };
      },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'พบปัญหา' }));
    await user.click(screen.getByRole('checkbox', { name: 'ขยะล้น' }));
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึก' })).toBeInTheDocument();
    expect(screen.getByText('พบปัญหา: ขยะล้น')).toBeInTheDocument();
    expect(screen.getByText(/^ส่งงานไม่สำเร็จ .* ลองล่าสุด \d\d:\d\d$/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ส่งใหม่' }));

    expect(await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' })).toBeInTheDocument();
    const sends = server.calls.filter((c) => c.path === '/api/scan-records');
    expect(sends).toHaveLength(2);
    expect(sends[1].body).toEqual(sends[0].body);
    expect(gps.calls).toBe(2);
  });

  it('a server fault is also ยังไม่ได้บันทึก', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/scan-records': () => ({ status: 503 }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึก' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ส่งใหม่' })).toBeEnabled();
  });

  it('a point of another Area is blocked, and the Admin sees it (M8, facility-0041)', async () => {
    fakeGps(nearSign);
    serve(
      { 'POST /api/scan-records': () => ({ status: 403, body: otherAreaBlocked }) },
      point({ id: 21, name: 'ห้องประชุม', areaId: 2, areaCode: 'AR02', areaName: 'Area ถัดไป', buildingCode: 'B' }),
    );
    const { user } = await renderApp(SIGN);

    await screen.findByRole('heading', { name: 'ห้องประชุม' });
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ป้ายนี้ไม่ใช่ Area ของคุณ' })).toBeInTheDocument();
    expect(screen.getByText('ไม่ได้บันทึกงาน')).toBeInTheDocument();
    expect(screen.getByText('Area ถัดไป (AR02) · ตึก B')).toBeInTheDocument();
    expect(screen.getByText('Area ทดลอง')).toBeInTheDocument();
    expect(
      screen.getByText(
        'Admin เห็นการสแกนครั้งนี้แล้ว ให้แจ้ง Admin ว่าคุณทำแทน Area ถัดไป เมื่อ Admin กดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ',
      ),
    ).toBeInTheDocument();
  });

  it('a refusal from the server stays on the form, word for word', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/scan-records': () => ({ status: 409, body: { message: 'ลงเวลาเข้างานที่ป้ายของ Area คุณก่อน' } }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ลงเวลาเข้างานที่ป้ายของ Area คุณก่อน');
    expect(screen.getByRole('button', { name: 'ส่งงาน' })).toBeEnabled();
  });

  it('ส่งงาน cannot be pressed again while a send is in flight', async () => {
    fakeGps(nearSign);
    let release: () => void = () => {};
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const server = serve({
      'POST /api/scan-records': async () => {
        await gate;
        return { status: 201, body: saved() };
      },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));
    const busy = await screen.findByRole('button', { name: 'กำลังส่ง...' });
    expect(busy).toBeDisabled();
    await user.click(busy);
    release();

    expect(await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' })).toBeInTheDocument();
    expect(server.calls.filter((c) => c.path === '/api/scan-records')).toHaveLength(1);
  });
});
```

Run: `npm test -- src/pages/scan`
Expected: FAIL. The no-signal and 503 tests find the form's alert instead of "ยังไม่ได้บันทึก"; the blocked test finds the form's alert "ป้ายนี้ไม่ใช่ Area ของคุณ" but no heading. The refusal and double-press tests already pass (Task 3 built them).

- [x] **Step 2: Write `SendFailed.tsx`**

`frontend/src/pages/scan/SendFailed.tsx`:

```tsx
import type { PointCard } from '../../shared/api/types';
import { formatTime } from '../../shared/utils/thaiTime';
import { PointHeading } from './PointHeading';
import { describeDraft, type ScanDraft } from './scanRules';

interface SendFailedProps {
  point: PointCard;
  draft: ScanDraft;
  triedAt: Date;
  sending: boolean;
  onResend: () => void;
}

/**
 * Wireframe M12 (facility-0052): nothing is kept on the phone to send later. The choices stay on this page; the person
 * walks to where there is signal and presses ส่งใหม่. The time recorded is when a send succeeds.
 */
export function SendFailed({ point, draft, triedAt, sending, onResend }: SendFailedProps) {
  return (
    <main className="scan-page">
      <PointHeading point={point} />
      <section className="app-note app-note--error" aria-labelledby="scan-failed-title">
        <h2 id="scan-failed-title">ยังไม่ได้บันทึก</h2>
        <span>{`ส่งงานไม่สำเร็จ เพราะไม่มีสัญญาณหรือเซิร์ฟเวอร์ไม่ตอบ · ลองล่าสุด ${formatTime(triedAt)}`}</span>
      </section>
      <section className="app-panel">
        <h2>สิ่งที่คุณเลือกไว้ (ยังอยู่ในหน้านี้)</h2>
        <p>{describeDraft(draft)}</p>
      </section>
      <section className="app-panel">
        <h2>ทำอย่างไรต่อ</h2>
        <ol>
          <li>อย่าปิดหน้านี้</li>
          <li>เดินไปตรงที่มีสัญญาณ</li>
          <li>กด "ส่งใหม่"</li>
        </ol>
        <p className="scan-muted">เวลาที่บันทึกคือเวลาที่ส่งสำเร็จ ถ้าพ้นเวลารอบจะนับเป็นส่งช้า ถ้าเดินออกไกลจากป้าย การส่งงานอาจติดธง</p>
        <p className="scan-muted">ระบบไม่เก็บงานไว้ในมือถือแล้วส่งเองทีหลัง</p>
      </section>
      <button type="button" className="scan-submit" disabled={sending} onClick={onResend}>
        {sending ? 'กำลังส่ง...' : 'ส่งใหม่'}
      </button>
    </main>
  );
}
```

- [x] **Step 3: Widen `ScanPage.tsx`**

Replace the whole of `frontend/src/pages/scan/ScanPage.tsx` with:

```tsx
import { useState } from 'react';
import { useParams } from 'react-router';
import { freshOnOpen, useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import { blockedScanOf, isNotSent } from '../../shared/api/scanAnswers';
import type { BlockedScan } from '../../shared/api/types';
import { BlockedScanScreen } from '../../shared/components/BlockedScanScreen';
import { LocationHelp } from '../../shared/components/LocationHelp';
import { StatusScreen } from '../../shared/components/StatusScreen';
import { readLocation, type LocationProblem } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { OffDuty } from './OffDuty';
import { SendFailed } from './SendFailed';
import { SubmitForm } from './SubmitForm';
import { SubmitResult } from './SubmitResult';
import { useGetPointByTokenQuery, useSubmitScanMutation, type ScanRecordCreated } from './scanApi';
import { emptyDraft, offDutyReason, requestOf, type ScanDraft } from './scanRules';
import './scan.css';

type Outcome =
  | { kind: 'saved'; result: ScanRecordCreated }
  | { kind: 'refused'; message: string }
  | { kind: 'location'; problem: LocationProblem }
  | { kind: 'blocked'; blocked: BlockedScan }
  | { kind: 'not-sent'; triedAt: Date };

/**
 * Wireframes M2, M4, M5, M8, M11, M12 and M13: the page a point's QR Sign opens (/scan/<token>, facility-0026). The
 * Cleaner presses ส่งงาน; the server decides the round, the flag and whether the Area is theirs (facility-0037,
 * 0041, 0047). A send that does not go through keeps the choices for ส่งใหม่ (facility-0052).
 */
export function ScanPage() {
  const { token = '' } = useParams();
  const pointQuery = useGetPointByTokenQuery(token, freshOnOpen);
  const workQuery = useGetMyWorkQuery(undefined, freshOnOpen);
  const [submit] = useSubmitScanMutation();
  const [draft, setDraft] = useState<ScanDraft>(emptyDraft);
  const [sending, setSending] = useState(false);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const send = async () => {
    setSending(true);
    try {
      const reading = await readLocation();
      if (reading.kind !== 'fix') {
        setOutcome({ kind: 'location', problem: reading.kind });
        return;
      }
      const triedAt = new Date();
      setOutcome(outcomeOf(await submit(requestOf(token, draft, reading.fix)), triedAt));
    } finally {
      setSending(false);
    }
  };

  if (pointQuery.error) {
    return (
      <StatusScreen
        title={getErrorMessage(pointQuery.error)}
        detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้ง Admin"
        linkTo="/my-work"
        linkText="ดูงานของฉัน"
      />
    );
  }
  if (workQuery.error && !workQuery.data) {
    return <StatusScreen title="เปิดหน้านี้ไม่ได้" detail={getErrorMessage(workQuery.error)} linkTo="/my-work" linkText="ดูงานของฉัน" />;
  }
  const point = pointQuery.data;
  const work = workQuery.data;
  // Decide only on fresh answers: the cache may hold My Work from before the Cleaner checked in
  if (!point || !work || pointQuery.isFetching || workQuery.isFetching) {
    return <p className="app-splash">กำลังเปิดป้าย...</p>;
  }

  if (outcome?.kind === 'saved') {
    return <SubmitResult point={point} draft={draft} result={outcome.result} />;
  }
  if (outcome?.kind === 'blocked') {
    const ownArea = work.areas.find((area) => !area.isCover);
    return <BlockedScanScreen blocked={outcome.blocked} sign="point" ownAreaName={ownArea?.areaName} />;
  }
  if (outcome?.kind === 'location') {
    return <LocationHelp problem={outcome.problem} action="ส่งงาน" busy={sending} onRetry={() => void send()} />;
  }
  if (outcome?.kind === 'not-sent') {
    return (
      <SendFailed point={point} draft={draft} triedAt={outcome.triedAt} sending={sending} onResend={() => void send()} />
    );
  }
  const offDuty = offDutyReason(work.attendance);
  if (offDuty) {
    return <OffDuty reason={offDuty} point={point} />;
  }
  return (
    <SubmitForm
      point={point}
      draft={draft}
      onChange={setDraft}
      sending={sending}
      errorText={outcome?.kind === 'refused' ? outcome.message : null}
      onSend={() => void send()}
    />
  );
}

function outcomeOf(answer: { data?: ScanRecordCreated; error?: unknown }, triedAt: Date): Outcome {
  if (answer.data) {
    return { kind: 'saved', result: answer.data };
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

- [x] **Step 4: Run the tests, the typecheck and the lint**

```bash
npm test          # expect: 95 passed (90 + 5)
npx tsc -b        # expect: no output
npm run lint      # expect: no output
```

- [x] **Step 5: Handoff entry**

```markdown
- <date> · Task 4 done: the scan page's failures: no signal, timeout or 5xx show "ยังไม่ได้บันทึก" (M12) with the choices kept and "ส่งใหม่" that reads GPS again; another Area's point shows the blocked screen (M8) with the cover advice; a server refusal stays on the form; one send at a time. Frontend tests 95 passed. Next: Task 5 (docs, final checks, the controller's real run).
```

- [x] **Step 6: Commit**

```bash
git add frontend/src docs/handoff.md
git commit -m "feat(frontend): send failed keeps the choices for resend, blocked point screen, one send at a time (facility-0041, 0052)"
```

---

### Task 5: Docs, ADR amendments and final checks

**Files:**
- Modify (amendment appended at the end of each): `docs/adr/facility-0025-frontend-target-tree.md`, `facility-0026-frontend-router-shape.md`, `facility-0036-check-in-requires-location-permission.md`, `facility-0041-scan-only-own-area-with-cover-assignment.md`, `facility-0052-cleaner-my-work-page-and-resend.md`, `facility-0062-phone-bundle-budget-200kb-gzip.md`
- Modify: `README.md`, `frontend/README.md`, `docs/architecture.md`, `docs/handoff.md`

**Interfaces:** none. Docs only, plus a full check.

- [x] **Step 1: Measure the bundle**

```bash
cd frontend && npm run build && cd ..
```

Note the gzip size of `dist/assets/index-*.js` from Vite's output. It must be ≤ 200 kB. You put it in the 0062 amendment.

- [x] **Step 2: ADR amendments**

Append each block to the end of its ADR, after a blank line. The ADRs are in Thai, so the amendments are too. Use the editor tools.

`facility-0025-frontend-target-tree.md`:

```markdown
> **แก้ไข 2026-10-15 (แผน 8):** เพิ่มหน้า `pages/check-in/` และ `pages/scan/` ของที่หลายหน้าใช้ร่วมกันอยู่ใน `shared/` เพราะหน้าห้าม import หน้าอื่น: `shared/api/myWorkApi.ts` (GET /api/my-work ใช้ทั้งหน้างานของฉัน หน้าลงเวลา และหน้าสแกน), `shared/api/scanAnswers.ts` (อ่านคำตอบ 403 ที่เป็น Blocked Scan และการส่งที่ไม่ถึงเซิร์ฟเวอร์), `shared/geo/readLocation.ts` (อ่าน GPS), `shared/utils/labels.ts` กับ `thaiTime.ts` และ component `StatusChip`, `LocationHelp` (M2), `BlockedScanScreen` (M8) ฝั่ง test เพิ่ม `src/test/fakeGps.ts` (fake `navigator.geolocation` แบบเดียวกับที่ fake `fetch`) และ `src/test/cleanerFixtures.ts`
```

`facility-0026-frontend-router-shape.md`:

```markdown
> **แก้ไข 2026-10-15 (แผน 8):** ป้ายลงเวลาของ Area ชี้ `/check-in/:token` ส่วนป้ายจุดยังชี้ `/scan/:token` ตามเดิม แยกสองที่อยู่เพราะ API แยกสองทางอยู่แล้ว (`POST /api/attendance` รับเฉพาะ QR Token ของป้ายลงเวลา และ `GET /api/service-points/by-token` ตอบ 404 กับป้ายลงเวลา) หน้าจึงรู้ชนิดป้ายจากที่อยู่โดยไม่ต้องถามเซิร์ฟเวอร์ ตอนนี้ทั้งสองที่อยู่เปิดได้เฉพาะแม่บ้าน หัวหน้าที่เปิด `/scan/:token` เห็นจอไม่มีสิทธิ์จนกว่าแผน 9 จะให้ที่อยู่นี้เปิดหน้าตรวจงานของหัวหน้า แผน 10 พิมพ์ป้ายตามสองที่อยู่นี้
```

`facility-0036-check-in-requires-location-permission.md`:

```markdown
> **แก้ไข 2026-10-15 (แผน 8):** หน้าเว็บอ่านพิกัดใหม่ทุกครั้งที่กด (ความแม่นยำสูง รอไม่เกิน 15 วินาที ไม่ใช้พิกัดเก่า) ไม่ได้รับสิทธิ์ → หน้าวิธีเปิด Location (M2) และปุ่ม "เปิดแล้ว ลองอีกครั้ง" ที่กดซ้ำรายการเดิม หาพิกัดไม่เจอ → บอกให้ลองอีกครั้ง หน้าที่เปิดผ่าน http (ไม่ใช่ https หรือ localhost) browser ไม่ให้อ่านพิกัดเลย หน้าเว็บจึงบอกว่าต้องเปิดผ่าน https และไม่มีปุ่มลองอีกครั้ง มือถือที่เปิด `http://<IP ของเครื่อง>` จะเจอแบบนี้จนกว่าจะมี HTTPS ตาม facility-0049
```

`facility-0041-scan-only-own-area-with-cover-assignment.md`:

```markdown
> **แก้ไข 2026-10-15 (แผน 8):** หน้าเว็บไม่ตัดสินเองว่าป้ายเป็นของ Area อื่น การบล็อกและการบันทึก Blocked Scan เกิดตอนแม่บ้านกด "ส่งงาน" หรือปุ่มลงเวลา เพราะ endpoint ที่บันทึก Blocked Scan มีแค่ `POST /api/scan-records` และ `POST /api/attendance` เปิดป้ายจุดของ Area อื่นจึงยังเห็นฟอร์ม พอกดส่งงานแล้วจึงขึ้นหน้า "ป้ายนี้ไม่ใช่ Area ของคุณ" (M8) และ Admin เห็นรายการ หน้า M8 ของป้ายลงเวลาไม่แนะนำเรื่องทำแทน เพราะคนทำแทนลงเวลาที่ป้ายของ Area ตัวเองตามข้อ 4
```

`facility-0052-cleaner-my-work-page-and-resend.md`:

```markdown
> **แก้ไข 2026-10-15 (แผน 8):** หน้างานของฉันถามเซิร์ฟเวอร์ใหม่ทุกครั้งที่เปิดหน้าและเมื่อกด "อัปเดต" ไม่มี hub สำหรับแม่บ้าน การ์ดไม่มีลิงก์ไปหน้าสแกน (facility-0059) แม้ wireframe M9 จะมี การส่งที่ไม่ถึงเซิร์ฟเวอร์ (ไม่มีสัญญาณ, ไม่ตอบใน 20 วินาที, คำตอบอ่านไม่ได้ หรือเซิร์ฟเวอร์ขัดข้อง 5xx) ขึ้น "ยังไม่ได้บันทึก" (M12) พร้อมสิ่งที่เลือกไว้และปุ่ม "ส่งใหม่" ที่อ่าน GPS ใหม่ทุกครั้ง ยอมรับความเสี่ยงว่าถ้าคำขอถึงเซิร์ฟเวอร์แล้วแต่คำตอบหาย การส่งใหม่จะได้ Scan Record ที่สอง ซึ่งระบบจัดรอบตามกติกาปกติ (มักเป็นงานนอกรอบ)
```

`facility-0062-phone-bundle-budget-200kb-gzip.md` (put in the size from Step 1):

```markdown
> **วัด 2026-10-15 (แผน 8):** JavaScript ไฟล์เดียว <N> kB gzip หลังเพิ่มหน้างานของฉัน หน้าลงเวลา และหน้าสแกนของแม่บ้าน (ยังไม่มี Base UI และยังไม่ได้ import SignalR) ยังอยู่ในงบ 200 kB
```

- [x] **Step 3: `README.md`**

In the section `## 📱 การเข้าใช้งานระบบบนอุปกรณ์จริง`:

1. In the bullet that starts `` - `http://<MAC_LAN_IP>:5173/login` ``, replace the sentence `ตอนนี้ทั้งสองหน้ายังขึ้นว่า "หน้านี้กำลังสร้าง" จนกว่าแผน 8 และ 9 จะเสร็จ` with `หน้าตึกของฉันยังขึ้นว่า "หน้านี้กำลังสร้าง" จนกว่าแผน 9 จะเสร็จ`.
2. Replace the bullet `` - หน้าสแกนป้าย (`/scan/<token>`) มากับแผน 8 ตอนนี้ที่อยู่นี้ยังขึ้น "ไม่พบหน้านี้" `` with:

```markdown
  - ป้ายลงเวลาของ Area เปิด `/check-in/<token>` และป้ายจุดเปิด `/scan/<token>` (แม่บ้านเท่านั้น หัวหน้ามากับแผน 9) ป้ายของ seed เช่น `/check-in/token-checkin-ar01` และ `/scan/token-restroom-m1`
  - ลงเวลาและส่งงานต้องอ่าน GPS ได้ browser ให้อ่านเฉพาะหน้า https หรือ `localhost` มือถือที่เปิด `http://<IP>` จะขึ้น "อ่านตำแหน่งไม่ได้" จนกว่าจะมี HTTPS (facility-0049) ระหว่างนี้ทดสอบบน Mac ที่ `http://localhost:5173` หรือ `:5001` แล้วตั้งตำแหน่งใน Chrome DevTools › More tools › Sensors › Location
```

- [x] **Step 4: `frontend/README.md`**

1. In the code block under `## Where things go (facility-0025)`, replace the line that starts `src/shared/api/` with these two lines, and add the `geo` line below the `src/shared/auth/` line:

```
src/shared/api/                          baseApi (RTK Query), server types, myWorkApi (shared by the Cleaner pages)
src/shared/geo/                          readLocation: one fresh GPS reading per press
```

2. Under `## Tests`, append:

```markdown
The Cleaner pages also need a location: `fakeGps(nearSign)` from `src/test/fakeGps.ts` gives jsdom a `navigator.geolocation` (`'denied'` and `'unavailable'` answers too). Made-up My Work, attendance and point answers are in `src/test/cleanerFixtures.ts`.
```

- [x] **Step 5: `docs/architecture.md`**

1. In section 2, in the `Pages[...]` node, replace `auth (M0, D0) · my-work · my-building · dashboard` with `auth (M0, D0) · my-work (M9, M14) · check-in (M1–M3, M10) · scan (M4, M5, M8, M11–M13) · my-building · dashboard`.
2. In section 3, replace `participant Phone as Phone browser<br/>(ScanRecordPage)` with `participant Phone as Phone browser<br/>(pages/scan ScanPage)`.
3. In section 3, replace the note `Before any scan the Cleaner records Shift-In: POST /api/attendance<br/>at the Area's check-in sign (the same QR flow, with the phone's GPS fix)` with `Before any scan the Cleaner records Shift-In at the Area's check-in sign:<br/>it opens /check-in/{qrToken}, and the press sends POST /api/attendance with a fresh GPS fix`.

- [x] **Step 6: `docs/handoff.md`**

1. Add a row to the Status table:

```markdown
| 8 | Cleaner phone: My Work, check-in sign page, point sign page with send failures and blocked scans | done (branch `feat/cleaner-phone`): `docs/superpowers/plans/2026-10-15-cleaner-phone.md` |
```

2. Under `## Pending work`, replace the line that starts `- Frontend plans 8–11` with:

```markdown
- Frontend plans 9–11 (scopes in the plan-7 file): 9 Supervisor phone, 10 Admin setup, 11 Admin live board.
```
3. In `## Notes for the frontend plan`, replace the line that starts `- The QR Sign URLs are not decided yet` with:

```markdown
- QR Sign URLs (plan 8): a check-in sign prints `<origin>/check-in/<token>`, a point sign `<origin>/scan/<token>`. Plan 10 prints these.
- `/scan/:token` is Cleaner-only after plan 8 (a test pins the Supervisor's no-permission screen). Plan 9 makes it show the inspection page for a Supervisor: a page may not import another page, so the role switch lives in `router.tsx` or a new app-level file classified in `eslint.config.js`.
- Shared for later pages: `shared/geo/readLocation.ts`, `shared/api/scanAnswers.ts` (`blockedScanOf`, `isNotSent`), `LocationHelp`, `BlockedScanScreen` (`sign="point"` text speaks to a Cleaner; plan 9 adds the Supervisor's wording), `StatusChip`, `test/fakeGps.ts`.
```

- [x] **Step 7: Final full check**

```bash
cd frontend && npm test && npx tsc -b && npm run lint && npm run build && cd ..
dotnet test backend/FacilityRealtime.slnx
grep -rn "style={" frontend/src || echo "no inline styles"
grep -rn "localStorage\|sessionStorage\|indexedDB" frontend/src || echo "no web storage"
grep -rn "toLocaleString\|toLocaleTimeString" frontend/src || echo "no ad-hoc time formatting"
grep -rnE "to=\{?[\"'\`]/(scan|check-in)" frontend/src || echo "no links to the sign pages"
git diff --stat 43f10a3 -- backend || true   # expect: nothing
```

Expected:
- Frontend: 95 tests pass, typecheck and lint are clean, and the JS bundle is ≤ 200 kB gzip.
- Backend: 299 unit tests pass; API 531 pass and 5 are skipped (nothing in `backend/` changed).
- Each grep prints its "no ..." line, and the `backend` diff is empty.

- [x] **Step 8: Handoff entry and commit**

```markdown
- <date> · Task 5 done: ADR amendments (0025 shared Cleaner pieces, 0026 sign URLs, 0036 GPS reading and plain http, 0041 blocked on press, 0052 refresh and resend, 0062 measured size), README device notes, frontend README, architecture diagrams, handoff notes for plans 9 and 10. Frontend 95 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
```

```bash
git add docs README.md frontend/README.md
git commit -m "docs: Cleaner phone in README, architecture and handoff; ADR amendments for sign URLs, GPS, blocked scans and resend"
```

- [ ] **Step 9 (controller only, not a subagent): real run on a throwaway database**

Ask the owner for the MySQL root password. Use it only in the commands below, and never write it to a file.

```bash
mysql -u root -p -e "CREATE DATABASE facility_cleaner_test CHARACTER SET utf8mb4;"
npm --prefix frontend run build:api
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=facility_cleaner_test;Uid=root;Pwd=<typed by the owner>;CharSet=utf8mb4;" \
dotnet run --project backend/src/FacilityRealtime.Api --urls "http://0.0.0.0:5001"
```

In a second terminal:

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5001/check-in/token-checkin-ar01   # 200 (the app)
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5001/scan/token-restroom-m1        # 200 (the app)
```

On the Mac, in Chrome at `http://localhost:5001` (localhost lets the page read a location), open DevTools › More tools › Sensors and set Location to latitude `13.7563`, longitude `100.5018`. Log in at `/login` as the seed Cleaner `E1001` (AR01, Day shift; between 20:00 and 06:00 use the Night-shift Cleaner `E1002` instead; both are in `README.md`). Then check, in order:

1. `/scan/token-restroom-m1` shows "ยังไม่ได้ลงเวลาเข้างาน".
2. `/check-in/token-checkin-ar01`: press "เข้างาน"; the time appears and "ออกพัก" and "เลิกงาน" are offered.
3. `/scan/token-restroom-m1`: press "ส่งงาน"; the result page shows the placement and "ห่างป้าย … ม. · อยู่ในรัศมี".
4. `/my-work` lists the AR01 points, with "ห้องน้ำชาย" sent.
5. `/scan/token-meeting-room` (AR02): press "ส่งงาน"; "ป้ายนี้ไม่ใช่ Area ของคุณ" appears. As `admin`, `GET /api/admin/alerts` in Swagger lists the Blocked Scan.
6. `/scan/token-restroom-f1`: set DevTools › Network to Offline, press "ส่งงาน"; "ยังไม่ได้บันทึก" appears. Set it back to No throttling and press "ส่งใหม่"; the result page appears.
7. In Sensors, set Location to `13.7600`, `100.5018` (about 400 m away); on `/check-in/token-checkin-ar01` press "ออกพัก"; the flagged message appears.
8. In Chrome's site settings for `localhost:5001`, block Location; press any button; "เปิด Location ก่อนลงเวลา" appears.

Ask the owner to open `http://<Mac IP>:5001/login` on a phone, log in as `E1001`, open `/my-work`, and check that the page reads well. On that plain-http address, pressing "เข้างาน" on `/check-in/token-checkin-ar01` must show "อ่านตำแหน่งไม่ได้" with the https message (decision 9); that is expected until HTTPS exists.

When done, stop the API, delete the build output and drop the database:

```bash
rm -r backend/src/FacilityRealtime.Api/wwwroot
mysql -u root -p -e "DROP DATABASE facility_cleaner_test;"
```

Record the result in the handoff (Task log), commit it, then run the whole-branch review.
