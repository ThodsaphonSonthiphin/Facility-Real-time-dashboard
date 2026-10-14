# Handoff: status and next steps

Read this first on a new machine or in a new session. It is updated after **every** task (owner's rule).

This repo is public: never put a password, phone number, real name or any secret in this file.

Last updated: 2026-10-14

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
| 5 | Accounts page (D6) + Excel export (D8) | **in progress** on branch `feat/accounts-export`: `docs/superpowers/plans/2026-10-14-accounts-and-excel-export-backend.md` |

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

## Pending work (ask the owner before starting)

- Supervisor plan: inspection, block reasons `OUTSIDE_SHIFT` / `OTHER_BUILDING`, Supervisor covers, the `INSPECTION_GPS` alert, and the inspection sheet of the Excel export.
- Excel import of accounts (ADR 0056; before rollout to every building).
- Frontend.

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

- A Cleaner who forgot Shift-Out and is later deactivated or moved to another shift: that alert can no longer be filled or closed. Normal date ranges do not show it.
- A wildly wrong date (e.g. year 9999) gives 500 instead of 400.
- Two Admins closing an alert or assigning a cover at the same moment: assigning may give 500 (nothing is left half-saved).
- The attendance window (opens/closes) is computed in 3 places; it should become `AttendanceCalendar.WindowOf`.
- `AuditLogQuery` runs 2 queries; one join would do.
- History paging: `NextBeforeId` offers an empty next page when the rows exactly fill the limit.
- No test yet that every timestamp in every response ends in `Z`.
- From plan 3: a point can be added to a deactivated Area; two Admins editing one point at once get 500 instead of 409; the 404 tests of the sign routes are missing; removing a round window loads its Scan Records into memory.

## Notes for the frontend plan

- Key alerts by kind **and** id: the "far from sign" and "no Shift-Out" alerts of one attendance share an id.
- Leave out unused filters entirely; an empty value gets 400. The same holds for unused account fields in plan 5.
- The "no Shift-Out" alert has no shift or check-in time yet for the D3 layout.
- The attendance page does not yet return the Admin's reason to show beside "แก้โดย Admin" (D2).

## Before plan 5

- At deployment: the app's MySQL user must not have UPDATE/DELETE on `audit_log` (ADR 0057).

## Open items to tell the owner

- The MySQL root password that was in the plan-2 file is scrubbed in the working copy but **still in git history**, and the repo is public. Change the root password on this laptop.
- GitGuardian once flagged `LoginTests.cs`. It is a made-up test credential; whoever manages GitGuardian should mark it Resolved as a test credential.
- The untracked file `docs/roadmap.html` is not part of any plan and has not been touched.

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
