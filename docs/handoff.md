# Handoff: status and next steps

Read this first on a new machine or in a new session. It is updated after **every** task (owner's rule).

This repo is public: never put a password, phone number, real name or any secret in this file.

Last updated: 2026-10-15

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

## Pending work (ask the owner before starting)

- Excel import of accounts (ADR 0056; before rollout to every building).
- Frontend: plan 7 (foundation) is written, not started: `docs/superpowers/plans/2026-10-15-frontend-foundation.md`. Plans 8–11 follow it (scopes in that file). Branch `feat/frontend-foundation` is created by its Task 1.

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
