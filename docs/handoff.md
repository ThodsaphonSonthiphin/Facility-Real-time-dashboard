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
| 10 | Admin setup: Areas, points, round windows, signs, sign location capture, print signs, accounts | in progress (branch `feat/admin-setup`): `docs/superpowers/plans/2026-10-19-admin-setup.md` |

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

## Pending work (ask the owner before starting)

- Excel import of accounts (ADR 0056; before rollout to every building).
- Frontend plans 10–11 (scopes in the plan-7 file): 10 Admin setup, 11 Admin live board.

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
- From plan 3: a point can be added to a deactivated Area; two Admins editing one point at once get 500 instead of 409; the 404 tests of the sign routes are missing; removing a round window loads its Scan Records into memory.

## Notes for the frontend plan

- Plan 7 built the base: add a page as `src/pages/<page>/` with `<page>Api.ts` (`baseApi.injectEndpoints`), a `.css` with the page prefix and an `index.ts`; add its route in `router.tsx` under the right `ProtectedRoute`; test with `renderApp` + `fakeServer`.
- QR Sign URLs (plan 8): a check-in sign prints `<origin>/check-in/<token>`, a point sign `<origin>/scan/<token>`. Plan 10 prints these.
- `/scan/:token` opens the Cleaner's ScanPage or the Supervisor's InspectPage by role (`shared/components/ByRole`, plan 9); an Admin gets no-permission. `/check-in/:token` is Cleaner-only.
- Shared for later pages: `shared/geo/readLocation.ts`, `shared/api/scanAnswers.ts` (`blockedScanOf`, `isNotSent`), `LocationHelp`, `BlockedScanScreen` (`sign` is `check-in`, `point` or `inspect`; `inspect` is the Supervisor's wording, plan 9), `StatusChip`, `test/fakeGps.ts`.
- Live updates (plan 9): open the hub with `followScanHub` (`shared/api/scanHub.ts`) in the query's `onCacheEntryAdded`; `onPoint` patches the cache, `onMissed` refetches (a tag). Plan 11's `getServicePoints` does the same, upserting cards, and reads its badge from `state.hub.status` (`store/slices/hubSlice.ts`). Tests drive the fake hub with `hub.latest()` from `src/test/fakeSignalR.ts`. The hub status is one global value written by every follower; before plan 11 follows more than one cache entry at a time (e.g. getServicePoints with changing arguments), make it per connection or ref-counted.
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
