# Handoff: status and next steps

Read this first on a new machine or in a new session. It is updated after **every** task (owner's rule).

This repo is public: never put a password, phone number, real name or any secret in this file.

Last updated: 2026-10-16

## Project

A factory cleaning-inspection system: 6 buildings, 70–72 Areas and about 170 housekeepers.

- Backend: .NET 10 Minimal API (Clean Architecture), EF Core 10, SignalR, xUnit.
- Runtime uses Oracle `MySql.EntityFrameworkCore` (MySQL 8.0); tests use SQLite in memory.
- The ADRs in `docs/adr/facility-NNNN-*.md` are the main spec. Where they conflict with a wireframe, the ADR wins.
- Plans live in `docs/superpowers/plans/`.

## Status

| Plan | Scope | Status |
|---|---|---|
| 1 | Data model | merged (PR #50) |
| 2 | Attendance + scan rules | merged (PR #51) |
| 3 | Admin setup: Areas, points, round windows, sign location, radius, new QR | merged (PR #52) |
| 4 | Attendance page (D2), alert list (D3), Admin history (D9) | merged (PR #53, `7cd8ba4`) |
| 5 | Accounts page (D6) + Excel export (D8) | done, merged (PR #54, `c530c84`) (branch `feat/accounts-export`): `docs/superpowers/plans/2026-10-14-accounts-and-excel-export-backend.md` |
| 6 | Supervisor inspection, covers, My Building, alerts, export sheet, summary | done, merged (PR #55, `d9b4374`) (branch `feat/supervisor-inspection`): `docs/superpowers/plans/2026-10-14-supervisor-inspection-backend.md` |
| 7 | Frontend foundation: tree, store, API layer, session, router and guard, login pages, served by the API | done, merged (PR #56, `43f10a3`) (branch `feat/frontend-foundation`): `docs/superpowers/plans/2026-10-15-frontend-foundation.md` |
| 8 | Cleaner phone: My Work, check-in sign page, point sign page with send failures and blocked scans | done, merged (PR #57, `96895d8`) (branch `feat/cleaner-phone`): `docs/superpowers/plans/2026-10-15-cleaner-phone.md` |
| 9 | Supervisor phone: My Building with live updates, inspection at the point's sign, nothing to inspect, blocked and send failures | done, merged (PR #58, `84890df`) (branch `feat/supervisor-phone`): `docs/superpowers/plans/2026-10-15-supervisor-phone.md` |
| 10 | Admin setup: Areas, points, round windows, signs, sign location capture, print signs, accounts | done, merged (PR #59, `a6dc170`) (branch `feat/admin-setup`): `docs/superpowers/plans/2026-10-19-admin-setup.md` |
| 11 | Admin live board: lazy Admin routes, point status with hub and 30 s poll (D1), attendance and corrections (D2), alerts and covers (D3) | done, merged (PR #60, `f897548`) (branch `feat/admin-live-board`): `docs/superpowers/plans/2026-10-19-admin-live-board.md` |
| 12 | Admin reports: inspection summary (D7), Excel export (D8), Admin history (D9) | in progress (branch `feat/admin-reports`): `docs/superpowers/plans/2026-10-21-admin-reports.md` |

Plan 4 test results at merge:
- Unit tests: 215 passed.
- API tests: 332 passed, 3 skipped (the MySQL tests need `FACILITY_MYSQL_TEST`).
- On real MySQL, all passed: migrations, the MySQL tests, and an 18-case API script.

## Task log (plan 5)

- 2026-10-14 · Owner approved: merge PR #53, scrub the MySQL password from the plan-2 file, delete the leftover pilot branches, write plan 5.
  - PR #53 merged (`7cd8ba4`); its branch was deleted locally and on GitHub.
  - The two pilot branches (`feat/pilot-data-model`, `feat/pilot-scan-rules`) no longer existed on GitHub; nothing to delete.
  - The password is replaced with `<local root password>` in `docs/superpowers/plans/2026-10-09-pilot-scan-rules-backend.md` (lines 33, 1030–1032).
  - The plan-5 file was written; this handoff was rewritten in English (the owner now prefers English).
  - Branch `feat/accounts-export` created from master `7cd8ba4`. The owner chose subagent-driven execution.
  - Next: plan 5 Task 1.
- 2026-10-14 · Task 1 done: AccountRules, PhoneNumber.IsComplete, audit categories Account and Export. Unit and API tests pass. Next: Task 2 (account list and create).
- 2026-10-14 · Task 2 done: GET/POST /api/admin/accounts (list; add Cleaner, Supervisor or Admin; slot and login-name checks; ACCOUNT_CREATE). All tests pass. Next: Task 3 (edit, deactivate, activate).
- 2026-10-14 · Task 3 done: PUT /api/admin/accounts/{id}, /deactivate, /activate (deactivation revokes sessions and frees the Cleaner's Area; no self-deactivation; slot check on activation). All tests pass. Next: Task 4 (phone and password).
- 2026-10-14 · Task 4 done: PUT /api/admin/accounts/{id}/phone and /password (sessions revoked, nothing secret logged); test that the history never shows a phone, password, hash or QR Token. All tests pass. Next: Task 5 (export rules and query).
- 2026-10-14 · Task 5 done: ExportRules.MissedRounds and ExportQuery (attendance, submissions, points with windows; date range and building filter). All tests pass. Next: Task 6 (the .xlsx endpoint).
- 2026-10-14 · Task 6 done: POST /api/admin/export returns .xlsx (การลงเวลา, การส่งงาน with ไม่ได้ทำ rows; text-only cells; EXPORT_EXCEL logged). New package ClosedXML 0.104.2. All tests pass. Next: Task 7 (MySQL check and docs).
- 2026-10-14 · Task 7 done: a MySQL test of the export queries, the case-insensitive employee ID rule and the deactivation transaction (user change plus session revocation); README and architecture docs. The MySQL test compiles and is skipped without `FACILITY_MYSQL_TEST`; the controller runs it on real MySQL. All other tests pass (421 passed, 4 skipped in API tests; 260 unit tests). Next: the MySQL run, the whole-branch review, then a PR for the owner.
- 2026-10-14 · Final review fixes: (F1) the export's "ไม่ได้ทำ" rows now wait until the attendance window closes (ClosesMinutesAfterShift after shift end, inclusive), because a Late Submission is accepted until then; (F2) the workbook is built before the EXPORT_EXCEL row is saved; (F3) editing a Cleaner whose Area was deactivated keeps the Area, moving one to a deactivated Area still gets 400; (F4) test pins that PUT without `areaId` clears the Area, documented in README and the frontend notes; (F5) README audit categories and this file's roadmap line corrected. All tests pass (MySQL ones skipped). Next: owner runs the MySQL tests on the Mac, then a PR.
- 2026-10-14 · MySQL check on the Mac (Homebrew MySQL 26.7, not 8.0): the owner ran the whole suite with `FACILITY_MYSQL_TEST` set; 691 passed, 0 skipped (263 unit, 428 API including the 4 MySQL tests). The throwaway database was dropped and `facility_dashboard` is untouched. Next: push the branch and open the PR, with the owner's OK.
- 2026-10-14 · PR #54 opened (owner approved the push). Not merged. Next: the owner reviews PR #54 and rules on the parked items listed in it; after the merge, ask which plan comes next (see Pending work).

## Task log (plan 6)

- 2026-10-14 · Task 1 done: migration SupervisorInspection (inspection GPS and cover; Supervisor covers by building with a one-place CHECK); attendance board and My Work read Area covers only. All tests pass (MySQL skipped). Next: Task 2 (Supervisor rules).

- 2026-10-14 · Task 2 done: BlockReason OtherBuilding and OutsideShift; SupervisorScope.WithoutCover and InspectionRules.Find (latest submission of the current round, nothing while rework is owed). Unit tests pass. Next: Task 3 (the Supervisor's scan).

- 2026-10-14 · Task 3 done: POST /api/inspections/scan (the current round's submission, or why there is nothing to inspect; another building or outside the shift is a Blocked Scan unless covered); Blocked Scan answers carry reason and building. All tests pass. Next: Task 4 (record the inspection).

- 2026-10-14 · Task 4 done: POST /api/inspections (สะอาด / ไม่สะอาด with defect, GPS and cover stamped, 409 when the scanned submission is stale or a rework is owed); cards in ต้องแก้ไข show the defect; Admins get InspectionRecorded live. All tests pass. Next: Task 5 (Supervisor covers).

- 2026-10-14 · Task 5 done: POST /api/admin/cover-assignments takes a Supervisor with buildingId (any shift but their own building-shift; from a Blocked Scan it closes that alert); cancel handles both kinds; CoverAssignmentDto.CleanerName renamed PersonName. All tests pass. Next: Task 6 (My Building and live updates).

- 2026-10-14 · Task 6 done: GET /api/my-building (own building then covered ones, รอตรวจ first, CanInspect outside the shift false); ScanRecorded and InspectionRecorded reach the building's Supervisors by account id (SubjectUserIdProvider). All tests pass. Next: Task 7 (alerts).

- 2026-10-14 · Task 7 done: alert list adds INSPECTION_GPS (closed with คุยแล้ว), shows each Blocked Scan's reason, and offers a building cover for a Supervisor's Blocked Scan. All tests pass. Next: Task 8 (inspection sheet of the export).

- 2026-10-14 · Task 8 done: RoundOutcomes and InspectionHistory (ไม่ได้ตรวจ / ไม่ได้แก้ for closed shifts); export set Inspections writes sheet "การตรวจงาน" (inspections, outcome rows, ตรวจแทน, GPS verdict only). All tests pass. Next: Task 9 (Inspection Summary).

- 2026-10-14 · Task 9 done: GET /api/admin/inspection-summary (per active Supervisor: beat points inspected of total, submissions and how many were inspected, ไม่ได้ตรวจ rounds, own inspections, reworks and GPS flags; 7 shift dates by default, at most 31). All tests pass. Next: Task 10 (MySQL check and docs).

- 2026-10-15 · Task 10 (part) done: MySqlSupervisorTests (compiles, skipped without FACILITY_MYSQL_TEST); README and architecture docs for inspections, My Building, Supervisor covers, alerts, export sheet and summary; frontend notes. Next: the controller's real-MySQL run (migration on a throwaway database, the whole suite with FACILITY_MYSQL_TEST, the smoke script), then the whole-branch review.

- 2026-10-15 · Final-review fix wave done: Inspection Summary leaves out post-shift work (decision 17); a failed live push is logged, not a 500; My Building IsCover follows the cover; docs updated. Unit 299, API 519 + 5 skipped. Next: the controller's MySQL run.

- 2026-10-15 · MySQL check on the Mac (Homebrew MySQL 26.7): the migration SupervisorInspection applied to an empty throwaway database (the one-place CHECK on the FK columns is accepted); the whole suite with `FACILITY_MYSQL_TEST` set passed, 823 passed, 0 skipped (299 unit, 524 API including the 5 MySQL tests); a smoke script against the real API on a throwaway database passed (check-in, submission with live push, My Building, scan, inspection with GPS, building cover create and cancel, alerts, summary, export) with no warnings in the API log. Both throwaway databases were dropped; `facility_dashboard` is untouched. Next: push the branch and open the PR, with the owner's OK.

- 2026-10-15 · Owner approved push and merge: PR #55 opened and merged (`d9b4374`). Plan 6 is done. Next: the owner chooses the next work (see Pending work).

## Decisions made while building plan 6 (report them to the owner in the PR)

- 17. Work a Cleaner submits after the clock's shift end (Late or a rework fix, inside the attendance window) cannot be inspected: Supervisors inspect only in their clock shift and a cover for an ended shift is refused. The Inspection Summary leaves it out of the beat's submitted / inspected / ไม่ได้ตรวจ numbers; the export still lists the round. Confirmed by the owner on 2026-10-15.
- 18. A blank defect sent with สะอาด counts as left out (201), like blank Notes on a submission.
- 19. The live feed goes only to active Supervisors, covering ones included; a live-update failure is logged and never fails the saved submission or inspection.
- 20. Migration SupervisorInspection is forward-only once a Supervisor (building) cover exists: Down() cannot make cover_assignments.area_id NOT NULL again.

## Task log (plan 7)

- 2026-10-15 · Task 1 done: old frontend deleted (it targeted the phase-1 API); toolchain is eslint 9 + boundaries, Vitest on jsdom with a fake fetch, husky pre-commit (frontend lint, tsc, tests); wireframe CSS tokens and IBM Plex Sans Thai bundled. Frontend tests 3 passed; boundary rules shown to catch page→page, shared→pages, unknown files and raw HTML. Next: Task 2 (store, API layer and session).
- 2026-10-15 · Task 2 done: authSlice (memory only), store, RTK Query baseApi with Bearer header and one shared refresh on 401 (never for /auth/ calls), page-load startSession, endSession (logout + cache reset), getErrorMessage. Frontend tests 23 passed; each guard shown to fail when its rule is removed. Next: Task 3 (router, guard and role homes).
- 2026-10-15 · Task 3 done: route table (createBrowserRouter), ProtectedRoute by role, role homes /my-work, /my-building, /dashboard (placeholders), not-found, no-permission, crash screen, session gate, bar with logout. Frontend tests 34 passed. Next: Task 4 (login pages M0 and D0).
- 2026-10-15 · Task 4 done: login pages /login (employee ID + phone, M0) and /login/admin (username + password, D0); wrong secret, throttle and server messages; return to the page opened before login; build commit shown. Frontend tests 44 passed; JS bundle 127.18 kB gzip. Next: Task 5 (the API serves the built frontend).
- 2026-10-15 · Task 5 done: the API serves the built frontend from wwwroot (npm run build:api), index.html for every page path but never for /api or /hubs (they keep 404/405), index.html no-cache and hashed assets immutable; CORS AllowAll removed; health check moved to /api/health. Unit 299, API 530 + 5 skipped. Next: Task 6 (docs, ADR amendments, final checks).
- 2026-10-15 · Task 6 done: ADR amendments (0004 frontend section #37, 0025 rebuild not move, 0026 routes, 0062 measured size, 0063 tokens, 0065 boundaries 7), plans 2B/2C marked superseded, README, frontend README, architecture diagram. Frontend 44 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
- 2026-10-15 · Final-review fix wave: a session that ends for any reason (logout or a refused refresh) empties the API cache (store listener); logout forgets the session at once, then waits for any refresh in flight and ignores its result, then logs out on the server; the refresh gives up after 15 s. Frontend tests 47 passed. Next: the controller's real run (MySQL), then the PR.

- 2026-10-15 · Task 6 Step 9 done (real run): the API on a throwaway database `facility_frontend_test` served the built app; curl checks passed (health, `/login`, `/scan/<x>` gets the app, unknown `/api` is 404); a script logged in as the seed Cleaner, Supervisor and Admin, with refresh and logout behaving as expected and a wrong phone refused. The owner checked the pages on a phone and reports everything works. Build output and the throwaway database were removed. Plan 7 is complete on branch `feat/frontend-foundation` (not pushed, no PR). Frontend tests 47 passed; unit 299; API 531 + 5 skipped. Next: the owner's OK for the push and PR, then frontend plan 8 (Cleaner phone).

## Task log (plan 8)

- 2026-10-15 · Task 1 done: My Work (M9, M14): own Area then covered Areas, most urgent first, summary tiles, rework defect, issue tag, no link to the scan page; asks the server on open and on "อัปเดต". Shared Cleaner types, GET /api/my-work query, Thai time helpers, status chip and colour tokens. Frontend tests 61 passed. Next: Task 2 (check-in page).
- 2026-10-15 · Task 2 done: check-in sign page /check-in/:token (M1, M3, M10): the shift's four events, buttons for what the server allows next, a fresh GPS reading per press, flag with the phone accuracy, already recorded and too-soon answers; Location off (M2), plain http, blocked sign (M8), outside the window, out of order (reloads the buttons), no signal. Shared readLocation, fakeGps, LocationHelp, BlockedScanScreen, scan answers. Frontend tests 82 passed. Next: Task 3 (scan page).
- 2026-10-15 · Task 3 done: point sign page /scan/:token (M4, M5, M11, M13): the point with its round hint and rework defect, เรียบร้อย chosen (two taps), พบปัญหา with tags and note (a tag or a note required), result by placement (on time, late with minutes, rework, off-round) with distance or flag; not checked in, checked out and outside the shift refused before the form; unknown sign; Location help (M2). Supervisors get no-permission until plan 9. Frontend tests 98 passed. Next: Task 4 (send failures and blocked point).
- 2026-10-15 · Task 4 done: the scan page's failures: no signal, timeout or 5xx show "ยังไม่ได้บันทึก" (M12) with the choices kept and "ส่งใหม่" that reads GPS again; another Area's point shows the blocked screen (M8) with the cover advice; a server refusal stays on the form; one send at a time. Frontend tests 103 passed. Next: Task 5 (docs, final checks, the controller's real run).
- 2026-10-15 · Task 5 done: ADR amendments (0025 shared Cleaner pieces, 0026 sign URLs, 0036 GPS reading and plain http, 0041 blocked on press, 0052 refresh and resend, 0062 measured size 135.53 kB gzip), README device notes, frontend README, architecture diagrams (and the plan-7 note above section 3), handoff notes for plans 9 and 10; plan 7 marked merged in the Status table. Frontend 103 tests (the plan expected 95; Tasks 3 and 4 added more); unit 299; API 531 + 5 skipped; no inline styles, web storage, ad-hoc time formatting or links to the sign pages; nothing under `backend/` changed. Next: the controller's real run, then the whole-branch review.
- 2026-10-15 · Task 5 Step 9 done (real run): the API on throwaway database `facility_cleaner_test` served the built app (JS 135.53 kB gzip). Server checks: `/check-in/<token>` and `/scan/<token>` return the app, unknown `/api` is 404, health OK. A script as the seed Cleaner: pre-shift-in refused, shift-in recorded, a clean point submission 201 (late by minutes, 8 m from the sign, within radius), My Work showed the point as รอตรวจ, another Area's point 403 with the Blocked Scan in the Admin alerts, an issue-tag submission 201, a check-in token on the point lookup 404; no warnings in the API log. The owner ran the plan's 8 Chrome steps on localhost (Sensors override, offline then resend, far-flag after 5 minutes, Location blocked) and reports no problems; the phone check on plain http was not run. Build output and the throwaway database were removed. Frontend 103 tests. Next: the whole-branch review, then the owner's OK for the push and PR.
- 2026-10-15 · Whole-branch review: no Critical; one Important, fixed: no test pinned the 20 s send timeout (removing it left every test green). `fakeServer` now ends a request when its signal aborts, as a browser does, and each sign page has a test where the server never answers: still sending at 19 s, "ยังไม่ได้บันทึก" / "ยังไม่ได้บันทึกเวลา" at 20 s. Both fail with no timeout, 10 s or 30 s. Minor, left as is: the scan page does not reload My Work after a 409 (the check-in page does); the server still refuses. Frontend 105 tests; unit 299; API 531 + 5 skipped. Next: the owner's OK for the push and PR.
- 2026-10-15 · Owner approved push and merge: PR #57 opened and merged (`96895d8`); GitGuardian passed. Plan 8 is done. Still open: the plain-http message on a real phone. Next: the owner chooses the next work (plan 9, Supervisor phone, see Pending work).

## Task log (plan 9)

- 2026-10-16 · Task 1 done: the server leaves out null fields, so outside the shift the attendance has no `shift`; My Work, the check-in page and the scan page now test it by truthiness (plan-8 bug: My Work printed "undefined", the scan page said "not checked in"). The `outsideShift` fixture matches the server. Frontend tests 105 passed. Next: Task 2 (hub follower and token).
- 2026-10-16 · Task 2 done: `followScanHub` (shared/api/scanHub.ts) opens one SignalR connection per cache entry after its first answer, hands ScanRecorded and InspectionRecorded cards to the page, asks again after every start and every reconnect (a restart every 3 s after the connection closes), and stops when the entry goes; `hubSlice` holds connecting / live / reconnecting; `getValidAccessToken` refreshes first when 30 s or less is left. Tests fake @microsoft/signalr through `test.alias` (src/test/fakeSignalR.ts). Frontend tests 116 passed. Next: Task 3 (My Building).
- 2026-10-16 · Task 3 done: My Building (M15, /my-building): the own building then covered ones, รอตรวจ by submit time, ต้องแก้ไข, then the rest; summary tiles; "not your shift" note; no links to the sign. Live: ScanRecorded and InspectionRecorded patch the card and it moves; a reconnect, "อัปเดต" and every visit ask the server; the footer says whether the hub is live; logout closes it. baseApi has tag `MyBuilding`. Frontend tests 130 passed (a push that lands during a GET asks again after it). Next: Task 4 (inspection page).
- 2026-10-16 · Task 4 done: /scan/:token opens by role (shared ByRole): the Cleaner's form, the Supervisor's inspection, no-permission for an Admin. Inspection (M6, M7, M16): opening reads a fresh fix and scans; the latest submission with distance; no verdict chosen; ไม่สะอาด needs a defect (sent trimmed, only with ไม่สะอาด); saved result with what the Cleaner sees; nothing to inspect or rework owed with the server's reason; 409 opens the newest work and clears the verdict; one send at a time. Frontend tests 146 passed (two pin the fresh scan on every visit and the splash during the rescan after a 409). Next: Task 5 (inspection failures).
- 2026-10-16 · Task 5 done: the inspection page's failures: Location off or plain http, on opening ("ตรวจงาน") and on the press ("บันทึกผลตรวจ"), with the retry repeating the same step; another building or the wrong shift shows the blocked screen in the Supervisor's wording (building cover advice, back to My Building), on opening or on the press; no signal, 20 s with no answer or a 5xx: "เปิดงานตรวจไม่ได้" with ลองอีกครั้ง on opening, "ยังไม่ได้บันทึกผลตรวจ" with the verdict kept and ส่งใหม่ (fresh fix) on the press. Frontend tests 157 passed. Next: Task 6 (docs, final checks, the controller's real run).
- 2026-10-16 · Task 6 done: ADR amendments (0023 token for SignalR, 0025 tree, 0026 sign URL by role, 0029 inspection page, 0048 blocked on opening, 0057 My Building live, 0062 measured size 154.83 kB gzip, 0064 hub follower and hub slice), README device notes, frontend README, architecture diagram, handoff notes for plans 10 and 11. Two behaviours the docs now state: `followScanHub` asks the server after every successful start (the first included) and every reconnect, and My Building asks once more after a GET that was in flight when a push landed. Frontend 157 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
- 2026-10-16 · Whole-branch review: no Critical or Important finding; StrictMode probes passed (one GPS reading and one scan per opening, one hub connection per page). One doc fix wave (hub client in architecture, BlockedScanScreen and hub-status notes here, the ไม่สะอาด resend risk in ADR 0029). Deferred to the start of plan 11: type the nullable PointCard fields as optional and drop the nulls from the `point()` fixture; make the hub status per connection before following more than one cache entry.
- 2026-10-16 · Task 6 Step 9 done (real run): the API on throwaway database `facility_supervisor_test` served the built app (JS 154.83 kB gzip). Server checks: `/my-building` and `/scan/<token>` return the app, unknown `/api` is 404, health OK, `/hubs/scan/negotiate` without a token is 401. A script with the real SignalR client as the seed Supervisor: the hub connected with the access token, a Cleaner's submission arrived as ScanRecorded, and the inspection scan offered that submission within the radius. The owner then ran the plan's 11 Chrome steps (Supervisor and Cleaner windows: live รอตรวจ without reload, ไม่สะอาด with defect seen on My Work, rework owed, nothing to inspect, 409 with newer work, offline then ส่งใหม่, hub drop and recovery, Admin no-permission) and reports everything works; no warnings in the API log. Build output and the throwaway database were removed. Next: the owner's OK for the push and PR.

## Task log (plan 10)

- 2026-10-19 · Task 1 done: Admin menu (สถานะจุด, จัดการ Area และป้าย, บัญชีผู้ใช้) shown by AppLayout wide and hidden in print; cache tag AdminSetup; shared/api/adminSetupApi.ts (Area, sign, building and Cleaner types and reads, setSignLocation); labels for shift pattern, sign location, rounds and coordinates; formatDate; shared form, table and confirm styles; test/adminFixtures.ts. Plan 9 marked merged in the Status table. Frontend tests 163 passed. Next: Task 2 (Areas page).
- 2026-10-19 · Task 2 done: Areas page D4 (/admin/areas/:areaId?): every Area with its meta line, the open Area (first one by default) with shifts, Cleaners and sign count, its check-in sign and points with rounds, location and radius, print links for active signs, links to edit, print and capture; deactivated Areas and points marked. Frontend tests 172 passed (fix round 1 added the switch and bad-id tests; the action cells stay table cells). Next: Task 3 (point editor).
- 2026-10-19 · Task 3 done: point editor on D4: name, Round Windows per shift (typed "HH:mm", checked before adding, Enter adds), the full list sent in shift order on save, day-only Areas have no night shift, the server's refusal shown with the draft kept, deactivate / activate a point; "+ เพิ่มจุดทำความสะอาดใน Area นี้" opens the same editor and a new point stays open for editing. Frontend tests 180 passed. Next: Task 4 (sign settings).
- 2026-10-19 · Task 4 done: sign settings on D4 for the check-in sign and every point: location (site capture link to M17, or coordinates typed from a map saved as Map and hidden once confirmed on site), radius 10–500 m, a new QR asked once in the page then "พิมพ์ป้ายใหม่ ›"; none for a deactivated point or Area. Frontend tests 188 passed. Next: Task 5 (Area form D10).
- 2026-10-19 · Task 5 done: Area form D10 (/admin/areas/new, /admin/areas/:areaId/edit): code (new only, fixed after), name, building, shift pattern, one Cleaner per shift from those without an Area, a warning that going day-only removes night rounds and the night Cleaner, the server's refusal shown, a save opens the Area on D4; the Area's signs and deactivate / activate on edit; one send at a time (the save is disabled while in flight). Frontend tests 194 passed. Next: Task 6 (print signs).
- 2026-10-19 · Task 6 done: print page D5 (/admin/print?area=<id>[&sign=<id>] or ?area=all): A4 sheets of 6 small or 2 A5 signs with dashed cut lines, each with type, name, Area and building, the QR drawn on the computer (uqr 0.1.3, SVG path, ECC M, quiet zone 4), rounds or attendance events, sign code, QR date and the full URL; deactivated Areas and points left out; the origin printed and a warning when it is not https; "พิมพ์" opens the browser's print with the bar, menu and controls hidden. JS bundle 167.62 kB gzip. Frontend tests 202 passed. Next: Task 7 (capture M17).
- 2026-10-19 · Task 7 done: capture page M17 (/admin/areas/:areaId/capture[?sign=<id>]), phone column: choose the sign you stand at, "ใช้ตำแหน่งตรงนี้" watches the GPS for 30 s (countdown, readings, latest accuracy), the median latitude, longitude and accuracy (rounded up) from at least 3 readings, the radius warning, "บันทึกพิกัด" only after 30 s, saved as Site; refused location, plain http and too few readings explained; choosing another sign or leaving stops the GPS; both buttons are disabled while a save is in flight. test/fakeGps.ts gained fakeGpsTrack. Frontend tests 220 passed. Next: Task 8 (accounts list and add).
- 2026-10-19 · Task 8 done: accounts page D6 (/admin/accounts): filters ทั้งหมด / แม่บ้าน / หัวหน้า / Admin / ปิดใช้งาน with counts, search by name or login name, the table with role, place and active; the add form sends only the chosen role's fields (Cleaner: employee ID, phone, optional Area, shift; Supervisor: building and shift; Admin: username and temporary password), a held shift or a night in a day-only Area cannot be picked, the phone and password are cleared once sent; the save is disabled while in flight. Frontend tests 233 passed. Next: Task 9 (account actions).
- 2026-10-19 · Task 9 done: account actions on D6: edit (name; a Cleaner's Area and shift, the current Area sent back to keep it, none for a deactivated Cleaner; a Supervisor's building and shift; an Admin's name only), แก้เบอร์โทร for Cleaners and Supervisors, รีเซ็ตรหัสผ่าน for Admins (own reset says this browser drops too), deactivate asks first and names the Area slot that goes, no self-deactivate button, activate in one press with the server's 409 shown; secrets are cleared once sent; one send at a time (the edit's save is disabled in flight, tested). Frontend tests 244 passed. Next: Task 10 (docs, final checks, real run).
- 2026-10-19 · Task 10 done: ADR amendments (0020 accounts page, 0021 new QR and print, 0025 tree, 0026 Admin routes, 0038 radius, 0045 capture and map fallback, 0060 where the token goes, 0062 measured size 173.53 kB gzip), README device notes, frontend README, architecture diagram, handoff notes for plan 11. Frontend 244 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
- 2026-10-19 · Final review fix wave: Admin reads ask the server on every open (`freshOnOpen` moved to shared/api/baseApi.ts) and "พิมพ์" refetches before printing; "บันทึกจุด" refuses times typed but not added; `normalizePhone` matches the server (+66 becomes 0, non-digits dropped, phone inputs allow 16 characters); phone and password help say re-entering the same secret changes nothing. Frontend tests 247 passed.

- 2026-10-19 · Owner's real run (plan 10), preliminary pass on the API at `facility_admin_test` and the Vite dev server: Areas, points, signs, print, accounts and the QR checks passed. An old check-in QR gets 404 "ไม่พบป้ายลงเวลานี้" and the new one records ShiftIn (tested with `POST /api/attendance`). The capture page (M17) on a real phone passed over https: the dev server runs with a mkcert certificate for the Mac's LAN IP (`FACILITY_DEV_CERT_DIR` in `frontend/vite.config.ts`, certificate kept outside the repo), the CA was installed on the phone, and the 30 s GPS capture saved. QR codes printed over plain http do not carry GPS, so signs are printed from the https origin.

- 2026-10-19 · PR #59 merged to master (merge commit `a6dc170`), branch `feat/admin-setup` deleted. Plan 10 is done. The GitGuardian check on #59 stayed red on six test-fixture values (fake UUIDs and `temporary-123`, not secrets); the incidents are still open in the GitGuardian dashboard to mark as false positives. Next: plan 11, after the owner's OK.

## Task log (plan 11)

- 2026-10-19 · Task 1 done: Admin pages are lazy routes (facility-0062 item 3, owner 2026-10-19); the root route shows "กำลังโหลด..." while the first page's file loads; renderApp waits for router.state.initialized. Main JS 156.21 kB gzip (was 173.53 kB in one file; a rebuild at the branch start measured 173.78 kB). Frontend tests 250 passed. Next: Task 2 (hub status per connection, AttendanceRecorded, optional PointCard fields).
- 2026-10-19 · Task 2 done: hubSlice keeps one status per connection key (selectHubStatus, hubFollowEnded), followScanHub takes a key and an optional onPoint and onAttendance (AttendanceRecorded), My Building uses key 'my-building'; PointCard's nullable fields are optional and point() leaves them out (both deferred from plan 9). Frontend tests 252 passed. Next: Task 3 (Dashboard cards with live updates).
- 2026-10-19 · Task 3 done: Dashboard D1 (/dashboard): every point by Area (server order), status chip, round, what happened by status, issue tag, no links; getServicePoints follows the hub (key 'service-points'): a pushed card replaces its point, an unknown point or a push during a GET asks again; livePoll (every open, 30 s, focus) with setupListeners in main.tsx; LiveBadge "SignalR Live" / "Connecting..." (facility-0064); tags ServicePoints, AttendanceBoard, Alerts. Main JS 156.36 kB gzip. Frontend tests 264 passed. Next: Task 4 (Dashboard filters).
- 2026-10-19 · Task 4 done: Dashboard filters in dashboardSlice (facility-0024): building (by code), one status from the legend buttons with counts inside the building, มีปัญหาที่แจ้ง; kept across pages, cleared when the session ends; "ไม่มีจุดตามตัวกรองนี้". Frontend tests 272 passed. Next: Task 5 (attendance page D2).
- 2026-10-19 · Task 5 done: attendance page D2 (/admin/attendance, lazy, in the menu as การเข้างาน): the clock's shift by default, ?date&shift&building in the address; summary tiles; table in the server's order with the regular Cleaner (or ไม่มีแม่บ้านประจำ), ยังไม่ลงเวลา, covers, state, the four times with "แก้โดย Admin", GPS flags; typed date dd/mm/yyyy checked before asking; search on Area, Cleaner or cover; shared/api/attendanceBoardApi.ts follows the hub per board (AttendanceRecorded invalidates tag AttendanceBoard:<date>:<shift>) with livePoll; formatShiftDate and parseShiftDate. Main JS 126.01 kB gzip. Frontend tests 285 passed. Next: Task 6 (corrections).
- 2026-10-19 · Task 6 done: corrections on D2 (facility-0051): "เพิ่มเวลา" / "แก้เวลา" on rows with a regular Cleaner; the form (event with each current time, time "HH:mm", reason required up to 500) checks before sending, shows the server's refusal and keeps the draft, one send at a time; the save invalidates AttendanceBoard and Alerts; ?fill=<userId>&event=<event> opens the form for an alert's link and closing it clears them. Frontend tests 295 passed. Next: Task 7 (alert list D3).
- 2026-10-19 · Task 7 done: alert list D3 (/admin/alerts, lazy): yesterday and today, ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด, type picker (ถูกบล็อก, ไกลจากป้าย for the three GPS kinds, ไม่ได้ลงเวลา), each alert keyed by kind and id with time, who ((หัวหน้า) marked), why a Blocked Scan was refused, sign, place, distance, radius, accuracy; "คุยแล้ว ปิดเตือน" with an optional note, refusals at the top of the page, one send at a time; a missing Shift-Out links to D2's form; the menu shows "รายการที่ขึ้นเตือน (n)", polled every 30 s (shared/api/alertListApi.ts); formatDayTime. Main JS 126.2 + 32.0 (preloaded chunk) = 158.1 kB gzip. Frontend tests 307 passed. Next: Task 8 (covers on D3).
- 2026-10-19 · Task 8 done: covers on D3: "Area ที่ยังไม่มีคน" from the clock's board with "มอบหมายคนทำแทน" (candidates best first, "ยืนยันมอบหมาย", the server's refusal in the form, nobody to cover said); a Blocked Scan with a cover gives it in one press (Cleaner: Area; Supervisor: building; the server closes the alert); "คนทำแทนกะนี้" with "ยกเลิกก่อนหมดกะ"; Supervisors' building covers are not listed (no endpoint). Covers invalidate AttendanceBoard and Alerts. Frontend tests 313 passed. Next: Task 9 (docs, final checks, the controller's real run).
- 2026-10-19 · Task 9 done: ADR amendments (0024 dashboardSlice as built, 0025 tree, 0026 routes and hydrate fallback, 0027 D2 as its own page with the shift in the URL, 0041 covers on D3, 0051 corrections on D2, 0061 livePoll, 0062 lazy Admin routes and all the JavaScript `index.html` loads 156.97 kB gzip (one file, no preloaded chunk in the final build), 0064 hub status per connection and the badge), architecture (section 1, the section 2 diagram, section 3 redrawn for pages/dashboard, section 4's client), README device notes, frontend README, handoff notes for plan 12. Frontend 313 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
- 2026-10-20 · Final-review fix wave: ยกเลิก is disabled while a send is in flight in the D2 correction, D3 review and D3 cover forms, and a finished send closes only its own form; an open D2 correction or D3 cover form closes when the board under it changes slot (D2 shows "กะที่แสดงเปลี่ยนแล้ว ฟอร์มแก้เวลาถูกปิด เปิดใหม่จากแถวของแม่บ้าน"); the cover candidates follow the per-shift board tag; the hub registers no-op handlers for events a follower does not take; test async waits 3 s. Frontend tests 319 passed. Next: the controller's real run (MySQL), then the owner's OK for the push and PR.
- 2026-10-20 · Controller's real run (plan 11), scripted part: the API on throwaway database `facility_live_test` (Homebrew MySQL, no root password on this Mac) served the built app (main JS 156.99 kB gzip). A Node script with the real SignalR client as the seed Admin passed 27/27: D1 list; D2 board by the clock's shift; a far Shift-In (200 m, radius 50 m) pushed as `AttendanceRecorded` and flagged; a submission with an issue tag pushed as `ScanRecorded`; a Blocked Scan refused 403 and listed on D3 with its cover suggestion; a cover from the Blocked Scan closes the alert, D2 shows "Covered", cancel clears it; a correction recorded as `AdminAdd`, pushed, and audited as `ATTENDANCE_ADD` with its reason; the far Shift-In alert closed with a note (open 2 → 0); a Cleaner gets 403 on the Admin endpoints; the hub negotiate without a token is 401; no `warn` or `fail` in the API log. Page paths return the app, unknown `/api` is 404.
- 2026-10-20 · Bug found by the real run and fixed (a53be69): `followScanHub` returned the page handler's value to SignalR, and D2's `onAttendance` returns the dispatched action, so the client logged an Error ("Result given for 'attendancerecorded' method but server is not expecting a result") on every D2 push. The handlers now have block bodies, and the fake hub's `push` throws when a handler returns a result, so this cannot come back (4 existing tests went red before the fix). Frontend 320 tests. Missing Shift-Out needs a shift to end, so it is covered by the tests only.
- 2026-10-20 · Still to do from the plan's 10 browser checks: the owner walks the Chrome steps (check-in reaching D2, submission reaching D1, filters across pages, hub drop and recovery, D2 correction, Blocked Scan and cover, GPS flag, phone size). The owner is running them on a second machine with a Cloudflare quick tunnel (see "Sharing a test build" below). The owner's results are in the entry below (checks 1–3 so far).
- 2026-10-20 · Mac setup for the walkthrough, done; no check walked yet. On the Mac: frontend 320 tests, backend 830 passed and 5 skipped (MySQL-only). `facility_live_test` exists and is seeded, `npm run build:api` has been run, and the signing key is in user-secrets. The API served on 5001 and a quick tunnel reached `/api/health` and the app, so a phone can use it. Both were stopped afterwards; every tunnel link is new. To resume: steps 3 and 4 of "Sharing a test build" (the database and the build are still there). Note: `dotnet run` without `--no-launch-profile` listened on 5225 even with `ASPNETCORE_URLS` set. The owner wants 7-digit employee ids (like 1000001) but the seed hard-codes `E1001`..`S2001`; held off. Changing it means `DbInitializer.cs`, about 35 test files, the README and docs, as its own commit with made-up numbers. For a test database only, renumbering by `ROW_NUMBER() OVER (ORDER BY id)` for non-Admin users inside a transaction works (the logins change, the passwords stay).
- 2026-10-20 · Owner's walkthrough (plan 11), checks 1–3 of 10 passed on `localhost:5001` (API on `facility_live_test`): (1) Admin login lands on `/dashboard` with the cards and the live badge; the JS the app loads is the main file plus the Dashboard chunk and two small shared chunks (`LiveBadge`, `adminSetupApi`), no other Admin page file; the other scripts in the Network list came from a browser extension, so use a Incognito window for the Network checks; (2) a Cleaner's check-in reaches D2 without a reload; (3) a submission reaches D1 as รอตรวจ and the Supervisor's inspection turns it to ตรวจผ่าน. Checks 4–10 still to walk: filters across pages, hub drop and recovery, D2 correction, Blocked Scan and cover, GPS flag, phone size, API log. While testing, the owner saw a Cleaner do Shift-Out at 15:57 on a day shift: this is by design (ADR 0050 no early-leave rule, ADR 0069 window from 60 minutes before to 60 minutes after the shift). The owner may take it back to the Supervisor; changing it would be its own plan, not part of PR #60.
- 2026-10-21 · Owner's walkthrough (plan 11), checks 4–10 reported passed. Check 5 (hub drop and recovery) was first walked with DevTools Offline, which does not close an open WebSocket: the badge stayed "SignalR Live" and only the 30 s poll failed (the "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้" message came and went), which is the designed fallback, not a bug. Walked again by stopping the API (Ctrl+C) and starting it again: the badge went to "Connecting..." and came back to "SignalR Live" without a reload; passed. API log (check 10) has no `warn` or `fail`. One finding, not yet detailed: Admin pages other than `/dashboard` overflow (the owner has not said which pages or which screen width); to be handled as its own item, not in PR #60 unless it happens on a normal desktop screen.
- 2026-10-21 · Fix for that finding (owner opened the pages on a phone): the wide tables of D2 (`/admin/attendance`) and D6 (`/admin/accounts`) sit in `.app-table-scroll` (shared.css), which scrolls sideways with a 720 px minimum table width; on D3 (`/admin/alerts`) the status chip is on its own line in every card (it wrapped only when the name was long). `/admin/areas` tables were not checked. Frontend 320 tests, lint and tsc clean. Checked by tests only, not by eye, until the owner looks. Next: the owner's OK to push and merge PR #60.
- 2026-10-21 · PR #60 merged to master (`f897548`), branch deleted. Plan 11 is done. Cleanup: no API or tunnel running, throwaway database `facility_live_test` dropped, `wwwroot` deleted. Next: plan 12 (Admin reports), after the owner's OK.

## Task log (plan 12)

- 2026-10-21 · Task 1 done: branch `feat/admin-reports` from `ca59320`; `thaiToday` and `shared/utils/dateRange.ts` (shortcuts วันนี้ / 7 วัน / เดือนนี้, `checkRange` with the server's limits, `readRange`, `readBuildingId`, `toRangeParams`, `exportLink`). Frontend tests 329 passed. Next: Task 2 (inspection summary D7).
- 2026-10-21 · Task 2 done: inspection summary D7 (`/admin/inspection-summary`, lazy, in the menu as สรุปการตรวจ): each active Supervisor with building and shift, points inspected of the beat ("ไม่ได้ตรวจ n จุด"), ตรวจทั้งหมด (facility-0048 rule 5), submitted, inspected, the share as a progress bar ("–" when nothing was submitted), ไม่ได้ตรวจ, ให้แก้ไข, GPS flags; no thresholds; range typed dd/mm/yyyy with the shortcuts, checked before asking (31 dates), building picker, both in the address. Shared `RangeFields` and `BuildingSelect`. Frontend tests 340 passed; main JS 157.13 kB gzip. Next: Task 3 (Excel export D8).

## Sharing a test build (colleagues on other networks)

Test data only. The Development seed has an Admin whose password is public in this repo (facility-0049), so a shared link must never point at a database with real data, and never at a long-lived server.

1. Clone the branch. `cd frontend && npm install && npm run build:api` (the build goes to the API's `wwwroot`, which git ignores).
2. Once per machine, set the signing key outside the repo: `dotnet user-secrets set "Jwt:SigningKey" <48 random bytes, base64> --project backend/src/FacilityRealtime.Api`. On Windows PowerShell 5.1 use `$rng = [Security.Cryptography.RandomNumberGenerator]::Create()` and `$rng.GetBytes($bytes)`; the static `GetBytes` is not there.
3. Create a throwaway database named `facility_*_test`. In the shell that runs the API, set `ConnectionStrings__DefaultConnection` (password typed there, never in a file), `ASPNETCORE_ENVIRONMENT=Development` and `ASPNETCORE_URLS=http://localhost:5001`, then `dotnet run --no-launch-profile --project backend/src/FacilityRealtime.Api`. Without `--no-launch-profile` the launch profile wins (port 5225) and the connection string can be missed, which would point the API at `facility_dashboard`.
4. `cloudflared tunnel --url http://localhost:5001` prints an `https://….trycloudflare.com` link with a real certificate, so phones need no CA and the browser allows GPS. Stop `cloudflared` and the link is dead. A named tunnel with Cloudflare Access (needs a domain) is the next step if colleagues should keep using it.
5. Same Wi-Fi only, no tunnel: the Vite dev server over https with a mkcert certificate for the machine's LAN IP (`FACILITY_DEV_CERT_DIR`, certificate kept outside the repo), and the mkcert CA installed on each phone. The IP changes with the network, so the certificate must be reissued.
6. Afterwards: stop the API and `cloudflared`, drop the database, delete `wwwroot`.

## Pending work (ask the owner before starting)

- Excel import of accounts (ADR 0056; before rollout to every building).
- UX fixes from the owner's real run, after plans 10 and 11 are done:
  - `/admin/accounts`, "แก้เบอร์โทร": show the current phone, then a field for the new phone.
  - `/admin/areas/:areaId`, round windows: choose the time from a list instead of typing "HH:mm". A window already added to the point cannot be picked again.

## Owner's rules (still in force)

- Never merge a PR or the younger brother's work without asking. PR #50–#53 were approved.
- MySQL: only throwaway databases `facility_*_test`; never touch `facility_dashboard`. The MySQL tests now refuse any other name.
- Ask the owner for the MySQL root password every time it is needed. Never write it into any file or into memory.
- Reply in English (switched from Thai on 2026-10-14), short and direct.
- Update this file after every task.

## System rules already decided (why the code is as it is)

- Attendance can be recorded from 60 minutes before the shift to 60 minutes after it (ADR 0069).
- Only Cleaners submit work, from Shift-In to Shift-Out; a break does not block it.
- A cover is only in that Cleaner's own regular shift.
- A round window cannot end exactly at 19:00 (Day) or 07:00 (Night).
- A deactivated Area leaves the Dashboard, the scan page and "my work".
- The attendance page never computes late or early (ADR 0050).

## Rulings made for the owner during plan 5

The 16 planning decisions are at the top of the plan-5 file (`docs/superpowers/plans/2026-10-14-accounts-and-excel-export-backend.md`), which has the detail. In short:

1. Routes are `/api/admin/accounts`, like every Admin page since plan 3.
2. Role, employee ID and username are fixed once created.
3. Deactivating a Cleaner frees their Area; activating brings them back with no Area; a Supervisor keeps building and shift; activating answers 409 if the slot is taken.
4. A deactivated Cleaner cannot be given an Area (400).
5. Changing a phone or resetting a password revokes all refresh tokens; the log row has no before or after; the same value again changes and logs nothing.
6. An Admin may reset their own password but cannot deactivate themselves.
7. On creation a Cleaner's Area is optional, a Supervisor needs building and shift, an Admin has neither.
8. Input limits: employee ID 1–20 characters, username 3–100, temporary password 8–100, display name 1–150, phone exactly 10 Arabic digits starting with 0.
9. Fields that do not belong to the role must be omitted (400 otherwise).
10. The export is `POST /api/admin/export`, returns the `.xlsx`, covers 1–31 shift dates, one building or all, sets `Attendance` and `Submissions`.
11. No inspection sheet yet; the Supervisor plan adds it.
12. "ไม่ได้ทำ" rows come from today's setup, for shifts that have ended; a window counts only if it existed when its round started.
13. The submissions sheet names the Cleaner.
14. Issue tags are exported as stored.
15. Every cell is text, with fixed column widths.
16. Each export writes `EXPORT_EXCEL` with range, building, sets and row counts, and no row content.

## Rulings made for the owner during plan 4

The 13 planning decisions are at the top of the plan-4 file and in the PR #53 description. These were made while building it:

1. Subagents skip the steps that need MySQL; the controller runs them at the end, so the password never goes to a subagent.
2. The one-line `Utc(DateTime)` helper is repeated in several files, following `AdminSetupView`.
3. A test checks that `AttendanceRecorded` reaches Admins only (ADR 0058).
4. On an attendance correction or closing an alert, a `DbUpdateException` answers 409 only if the racing row really exists; otherwise it is rethrown. Database errors are never swallowed.
5. Each correction rule has its own test, and a test checks that a correction sends the live event.
6. No cover is offered to a Cleaner without an Area, or for a deactivated Area.
7. Tests cover closing every alert type, a note over 500 characters, and the cover-candidate rules.
8. If the Admin picks a cover who is not the person who scanned the wrong Area, that alert is not closed automatically; "คุยแล้ว" must be pressed.
9. A Blocked Scan sent with a cover assignment must be in the same shift as the cover, or the answer is 400.
10. One test set checks that a deactivated Admin with a live token gets 401 on all 7 new routes.
11. Saving the same minute the Cleaner scanned is not a change (nothing logged, no "แก้โดย Admin" badge).
12. The MySQL tests refuse a database whose name is not `facility_*_test`.

## Parked; not needed before merging

- The AttendanceRecorded live pushes (check-in/out, Admin corrections) are not wrapped like PointNotifications: a hub failure there still answers 500 after the attendance row is saved (pre-existing since plan 4).
- A Cleaner who forgot Shift-Out and is later deactivated or moved to another shift: that alert can no longer be filled or closed. Normal date ranges do not show it.
- A wildly wrong date (e.g. year 9999) gives 500 instead of 400.
- Two Admins closing an alert or assigning a cover at the same moment: assigning may give 500 (nothing is left half-saved).
- The attendance window (opens/closes) is computed in 3 places; it should become `AttendanceCalendar.WindowOf`.
- `AuditLogQuery` runs 2 queries; one join would do.
- History paging: `NextBeforeId` offers an empty next page when the rows exactly fill the limit.
- No test yet that every timestamp in every response ends in `Z`.
- ThaiLabels.AlertName(BlockedScan) is still 'สแกนนอก Area', so the permanent audit summary for closing a Supervisor's Blocked Scan says 'สแกนนอก Area'; change the wording before rollout.
- Retrying POST /api/inspections writes a second Inspection Record (Passed) or gets 409 (Rework); the page must disable the button while posting.
- Supervisors' building covers (facility-0048) can be assigned from a Blocked Scan on D3, but no endpoint lists them, so the alert page cannot show or cancel them (plan 11 decision 10).
- From plan 3: a point can be added to a deactivated Area; two Admins editing one point at once get 500 instead of 409; the 404 tests of the sign routes are missing; removing a round window loads its Scan Records into memory.

## Notes for the frontend plan

- Plan 7 built the base: add a page as `src/pages/<page>/` with `<page>Api.ts` (`baseApi.injectEndpoints`), a `.css` with the page prefix and an `index.ts`; add its route in `router.tsx` under the right `ProtectedRoute`; test with `renderApp` + `fakeServer`.
- QR Sign URLs (plan 8): a check-in sign prints `<origin>/check-in/<token>`, a point sign `<origin>/scan/<token>`. Plan 10's print page (`/admin/print`) prints them with the printing page's origin, so print from the server's https address.
- `/scan/:token` opens the Cleaner's ScanPage or the Supervisor's InspectPage by role (`shared/components/ByRole`, plan 9); an Admin gets no-permission. `/check-in/:token` is Cleaner-only.
- Admin reads use `freshOnOpen` (ask the server on every open) and the print page refetches before printing: plan 10's final review overruled plan decision 13, because M17 runs on a second device.
- Shared for later pages: `shared/geo/readLocation.ts`, `shared/api/scanAnswers.ts` (`blockedScanOf`, `isNotSent`), `LocationHelp`, `BlockedScanScreen` (`sign` is `check-in`, `point` or `inspect`; `inspect` is the Supervisor's wording, plan 9), `StatusChip`, `test/fakeGps.ts`.
- Live updates (plans 9 and 11): open the hub with `followScanHub({ key, … })` (`shared/api/scanHub.ts`) in the query's `onCacheEntryAdded`. `onPoint` patches the cache (ScanRecorded, InspectionRecorded), `onAttendance` takes AttendanceRecorded (Admins only), and `onMissed` refetches by tag. Each connection's status is `selectHubStatus(key)` (`store/slices/hubSlice.ts`), and `LiveBadge` shows it. Admin live pages use `livePoll` (every open, 30 s, focus). Tests drive the fake hub with `hub.latest()` from `src/test/fakeSignalR.ts`.
- The server leaves out null fields in REST and hub JSON. Test them by truthiness; plan 9 Task 1 fixed the three `shift === null` checks of plan 8.
- Supervisor scan: call `POST /api/inspections/scan` when the page opens. Send `submission.id` back as `scanRecordId`, and on 409 scan again.
- `CoverAssignmentDto.cleanerName` is now `personName`. A cover has either `areaId`/`areaCode` or `buildingId`/`buildingCode`. `CoverSuggestionDto` likewise has `areaId` or `buildingId`.
- Alerts carry `blockReason` (`OtherArea`, `OtherBuilding`, `OutsideShift`) and the new kind `InspectionGps`.
- Cards in ต้องแก้ไข carry `rework { defect, requestedAt }`.
- Inspection Summary `notInspected` counts finished rounds left ไม่ได้ตรวจ in closed shifts, not submitted minus inspected; work a Cleaner submits after the shift end (inside the 60-minute window) cannot be inspected by anyone and is left out of the beat's numbers (plan 6 decision 17).
- Cleaners still get no hub events. My Work reloads on open.
- Disable the inspect button while POST /api/inspections is in flight: a retried Passed writes a second Inspection Record, and a retried Rework gets 409.
- A Blocked Scan alert of a Supervisor still says 'สแกนนอก Area' in the history summary; show the alert's `blockReason` on the page instead.
- Key alerts by kind **and** id: the "far from sign" and "no Shift-Out" alerts of one attendance share an id.
- Leave out unused filters entirely; an empty value gets 400. The same holds for unused account fields in plan 5.
- `PUT /api/admin/accounts/{id}` replaces every place field: leaving `areaId` out clears a Cleaner's Area, so the client sends the current `areaId` to keep it.
- The "no Shift-Out" alert has no shift or check-in time yet for the D3 layout.
- The attendance page does not yet return the Admin's reason to show beside "แก้โดย Admin" (D2).
- Admin pages (plans 10 and 11): add a plan-12 page as a `lazy` route under the Admin guard in `router.tsx`, its path to the lazy-route list in `router.test.tsx`, and its link to `shared/components/AdminNav.tsx` (plan 12: สรุปการตรวจ, ส่งออก Excel, ประวัติ Admin). Admin setup data uses the `AdminSetup` tag; the live board uses `ServicePoints`, `AttendanceBoard` (one per shift) and `Alerts`. `src/test/adminFixtures.ts` and `src/test/liveFixtures.ts` have made-up Admin answers.
- Dates for plan 12: `formatShiftDate`, `parseShiftDate` (typed dd/mm/yyyy, checked before sending) and `formatDayTime` are in `shared/utils/thaiTime.ts`. D1 and D2 have no "ส่งออก Excel" link yet; add it with D8.
- The print page's `@page` rule (A4, no margin) is in the one CSS bundle, so it applies to anything the browser prints.
- M17 on a real phone over https is still to be checked: a desktop browser with a fixed simulated location may report one reading only (the real run nudges it), while phones report about once a second.
- Bundle after plan 11: Admin pages are lazy routes (owner, 2026-10-19). All the JavaScript a phone loads from `index.html` is 156.97 kB gzip (one file; no preloaded chunk in the final build) of 200 kB. Plan 12's pages go into their own files.

## Before plan 5

- At deployment: the app's MySQL user must not have UPDATE/DELETE on `audit_log` (ADR 0057).

## Open items to tell the owner

- The MySQL root password that was in the plan-2 file is scrubbed in the working copy but **still in git history**, and the repo is public. Change the root password on this laptop.
- GitGuardian once flagged `LoginTests.cs`. It is a made-up test credential; whoever manages GitGuardian should mark it Resolved as a test credential.
- `docs/roadmap.html` was committed on this branch by the owner (`0e9ab37`); it is not part of any plan.
- Build warning NU1903: `Microsoft.AspNetCore.SignalR.Common` 10.0.12 has a known high-severity advisory. It comes in through `Microsoft.AspNetCore.SignalR.Client` in the ApiTests project (it existed before plan 5, and plan 5 did not cause it). Bump the package when a patched version exists.

## Working method that works

- Plan with the `sp-writing-plans` skill, then run it with `sp-subagent-driven-development`: one subagent per task, a review per task, and a whole-branch review at the end.
- Test against the real API: run the API in Development with `ConnectionStrings__DefaultConnection` pointing at a throwaway database, then drive it with a Python script (use `python -X utf8` when printing Thai).
- `dotnet test backend/FacilityRealtime.slnx` runs every test.
- New migration: `dotnet ef migrations add <Name> --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api` (run `dotnet tool restore` first).

### Quirks of the company Windows laptop

- `gh` is in `...\WinGet\Packages\GitHub.cli_Microsoft.Winget.Source_8wekyb3d8bbwe\bin`; add it to PATH yourself.
- Thai text in a shell heredoc breaks; write such files with the Write or Edit tool.
- Stop any running API before building, because Windows locks its DLLs.
- `TZ=Asia/Bangkok date` in Git Bash prints UTC, not Thai time.

### Setting up a new machine (e.g. the Mac)

Install the .NET 10 SDK, MySQL 8.0, the GitHub CLI and Claude Code. Then `git clone`, check out the branch, run `dotnet tool restore` and `dotnet test backend/FacilityRealtime.slnx`.
- 2026-10-14 · PR #54 merged by the owner (`c530c84`). Plan 5 is done. Next: the owner chooses the next plan (see Pending work).
