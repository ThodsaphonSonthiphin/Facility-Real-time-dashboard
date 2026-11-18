# Cleaner scheduling acceptance

The acceptance criteria are A01–A29 in the [approved design](../superpowers/specs/2026-11-17-cleaner-scheduling-design.md#15-เกณฑ์ตรวจรับ). Factory API tests use an adjustable clock for exact times and boundaries. The browser suite uses the current server slot and a synthetic roster in a runner-owned MySQL database. The real MySQL API suite separately checks migration, transactions, races and retention.

## Reproduce

Use the [owned E2E runner](e2e.md) and a desktop session. For the scheduling-only browser pass, the repeat guard is disabled within the owned run so several actions can be recorded in quick succession. `AttendanceEndpointTests.A_different_event_within_five_minutes_is_ignored` exercises the ordinary five-minute setting.

```bash
Attendance__RepeatIgnoreMinutes=0 npm --prefix frontend run e2e -- --dataset large --grep scheduling
npm --prefix frontend run e2e -- --dataset large
```

Select WebKit or Firefox via `E2E_BROWSER`. Reports, traces and screenshots are private under `.run/e2e/<run>/`. Use the run's own ownership manifest for cleanup after an interruption. The manifest, synthetic credentials and sign URLs belong with the local test artifacts.

The backend suite uses the usual unit and API projects. For MySQL-gated cases, set `FACILITY_MYSQL_TEST` privately to an explicitly owned `facility_*_test` database. The tests drop and recreate it. Apply this to an isolated test instance and verify the target before running. A run with the variable unset leaves the MySQL cases skipped.

```bash
dotnet test backend/tests/JanitorialManagementDashboard.UnitTests
dotnet test backend/tests/JanitorialManagementDashboard.ApiTests
npm --prefix frontend run lint
NODE_ENV=test npm --prefix frontend test
npm --prefix frontend run build
```

## Recorded verification

2026-11-18, Asia/Bangkok, on `codex/cleaner-scheduling`. The isolated backend target was a disposable database on a separate loopback MySQL instance. All 531 unit cases and 970 API cases passed, with 0 skipped. The API result includes all 19 MySQL-gated cases, including fresh and legacy-upgrade migrations, command/attendance/transfer races and scheduling retention. Raw test logs and TRX results are under `.run/scheduling-task17/`.

Frontend lint, TypeScript, production build and 644 frontend tests passed. The main JavaScript bundle was 131.60 kB gzip against the 200 kB budget.

| Owned browser run | Passed | Skipped | Failed | Evidence directory under `.run/e2e/` |
|---|---:|---:|---:|---|
| Chromium, full large profile | 41 | 0 | 0 | `20261118125747_63aa32f9` |
| Chromium, focused scheduling and per-run seed | 5 | 0 | 0 | `20261118130116_c5e4cfe7` |
| WebKit, full large profile | 39 | 2 | 0 | `20261118125941_a10250d3` |
| Firefox, full large profile, initial browser verification | 37 | 3 | 1 | `20261118130132_d0eb6114` |
| Firefox, focused scheduling after waiting for the reference week | 5 | 0 | 0 | `20261118131202_238dd04e` |
| Firefox, full large profile after fixes | 38 | 3 | 0 | `20261118131243_21948e72` |
| Chromium, focused scheduling after boundary and live-update fixes | 5 | 0 | 0 | `20261118131624_2c537b17` |

The full runs use the real repeat guard. The two WebKit skips are Chromium-only layout measurements. Firefox also skips the engine-specific location-permission retry. The initial Firefox failure was a previous-week read moving the checkbox between its scroll and click. The focused Chromium report contains thirteen other responsive screenshots and both destination-confirmation screenshots where the actual arrival status is visible.

## Acceptance evidence by criterion

Backend test classes below are in `backend/tests`. Frontend unit and component tests are in `frontend/src`. Browser cases are in `frontend/e2e/desktop/scheduling.spec.ts` and `frontend/e2e/phone/schedule-attendance.spec.ts`.

| ID | Evidence to inspect |
|---|---|
| A01 | `ScheduleScopeEndpointTests`, `ScheduleReadTests` and `ScheduleWriteTests` check two-building responsibility, another shift and forged filters, and Admin and own-Cleaner reads. The owned weekly edit and read also use those two staff roles. |
| A02 | `MySqlScheduleCommandRaceTests` races two authorised destination Supervisors through the real relief assignment handler for one Cleaner slot. Exactly one assignment, receipt and audit row survive. Other cases check an absent-row anchor and replay of one identity. The owned two-editor browser case exercises a competing stale draft. |
| A03 | `ScheduleWriteTests` checks that a one-date Area change leaves the rest of the week intact and refuses a baseline that would strand an override. `ScheduleHistoryCorrectionTests` verifies that a compatible dated break remains. |
| A04 | `ScheduleReadTests.A_day_off_choice_is_isolated_to_its_own_week`, `SchedulePersistenceTests` and the owned weekly edit and Cleaner read cover the distinction between an unset week and a committed choice. |
| A05 | `WeeklyOffEditor.test.tsx` covers stable identity, local Monday, an already-chosen day, source gaps and a departed person. It also verifies that a failed authorised refresh disables copying. |
| A06 | `WeeklyOffEditor.test.tsx` covers keeping the local edit when returning from review and restoring saved values on discard. |
| A07 | `ScheduleWriteTests` covers submitting a subset and leaving incomplete rows visible. `WeeklyOffEditor.test.tsx` and `ScheduleCommandTests` cover invalid-row reporting and atomic rollback. |
| A08 | `ReliefAssignmentTests` and `ReliefAssignmentRulesTests` check a required standalone group, the replaced person's dated group and an explicit override. |
| A09 | `ReliefLoanTests` checks the named source Supervisor, destination attestation and destination group. The owned loan case records the outside agreement through the destination Supervisor's browser. |
| A10 | `ReliefLoanTests` and `ReliefAssignmentTests` check another shift, occupied or on-leave candidates, unchanged existing plans and one Supervisor covering both buildings. |
| A11 | `AttendanceEndpointTests` checks both complete sequences and the stored legacy contract. `MySqlScheduleMigrationTests` upgrades an actual legacy database without adding guessed facts. |
| A12 | `AttendanceEndpointTests` requires exact gap confirmations and checks persisted skip markers with no synthetic times. `CheckInPage.test.tsx` checks the confirmation UI. |
| A13 | `AttendanceLifecycleTests`, `AttendanceEndpointTests` and `ScanRecordEndpointTests` cover ordered final departure, work during a break with no recorded return and refusal after leaving. |
| A14 | `MissingTeamAttendanceTests` checks provenance, scope, reason, time order and refusal to overwrite. `MySqlAttendanceRaceTests` verifies a competing fill preserves the first fact. `AttendanceCorrectionEndpointTests` covers the Admin correction path. |
| A15 | `EffectiveAttendanceTests.A_real_early_checkout_marks_later_leave_actions_inapplicable_and_preserves_earlier_gaps` records a final 13:00 departure with later leave-covered breaks. The owned browser uses a relative departure instead of changing the API clock. |
| A16 | `EffectiveAttendanceTests` checks actual 13:00 departure with absent leave and leave beginning 13:15. It checks that the read flags the mismatch and leaves the stored departure and leave facts intact. Check-In and the scoped team attendance drawer show the warning. |
| A17 | `EffectiveAttendanceTests` checks eligible check-in on leave and off days. Its correction case records actual 07:00, preserves the conflict after a refused edit and clears it only after the 07:10 plan cancellation commits. That cancellation leaves the actual record and its revision intact. `ScanRecordEndpointTests` checks that eligible work submission still succeeds. |
| A18 | `ScheduleCommandTests` and the MySQL command races check identical replay, changed-payload refusal and the current revision on a stale write. `ScheduleConflict.test.tsx` compares preserved input with authorised current values. |
| A19 | `ScheduleCommandTests`, `SchedulingRetentionTests` and `ScheduleReview.test.tsx` check lost responses, uncertain results, scoped lookup, original-envelope replay and refusal to restart a purged, expired identity. |
| A20 | `AttendanceCalendarTests` and `AttendanceEndpointTests` cover the receiving boundaries and a Night action after midnight. `scheduleRules.test.ts` and `SchedulePage.test.tsx` cover the Sunday-start week and Admin's initial clock shift. |
| A21 | `AttendanceLifecycleTests` checks exact pre-window eligibility and refusal for a slot with facts. `AttendanceEndpointTests` checks a stale client's response and absence of writes. This is contract and synthetic-slot evidence. Field cutover has its own prerequisites. |
| A22 | `AreaTransferTests` checks independent initial entry and destination confirmation times. The owned arrival case waits for a successful confirmation POST and verifies persisted facts through an independent read. |
| A23 | `AreaTransferTests` checks that confirmation preserves recorded break progress and group and does not create a break event. |
| A24 | `MySqlTransferRaceTests` checks cancel-versus-confirm and duplicate confirmation. `AreaTransferTests` checks same-fact replay, refusal after accepted arrival and a subsequent authorised forward move. |
| A25 | `ScheduleEntryProjectionTests` checks pending, confirmed and cancelled board states, occupied destinations and continued historical entry. The owned arrival case checks pre- and post-confirmation evidence and the origin work record. This is the board projection used by the separate overview workstream. |
| A26 | `ScheduleLiveTests` uses real hub connections to check an active same-building Supervisor on the other shift receives no event, and that a future baseline change reaches the affected Cleaner. It also covers deactivation, affected recipients and recovery by fresh read. `SchedulePage.test.tsx`, `ScheduleReview.test.tsx` and `ScheduleConflict.test.tsx` cover reconnect, read failure, uncertain results and keeping a draft. |
| A27 | `ScheduleReadTests` covers unavailable history. `MySqlSchedulingRetentionTests` and `SchedulingRetentionTests` cover the retention boundary, arrival after GPS pruning, preserved current configuration and command expiry after purge. |
| A28 | `ScheduleHistoryCorrectionTests` checks reasoned correction, original and corrected revisions, and unchanged actual events and work. Ordinary recurring edits are tested in `ScheduleWriteTests`. |
| A29 | Browser reports and viewport captures cover schedule, review, conflict, attendance and transfer. Component tests check labelled controls, editing and error states. |

## Field limits

The browser uses synthetic credentials, installed browser engines and controlled location coordinates. Real Safari on iPhone, camera scanning, phone GPS and permission retry, device toolbar and network behaviour still need a site trial. Choose the first eligible numbered slot and rehearse the backfill before field activation. [Deployment and rollback](../handover.md#210-เตรียมเปิดใช้ตารางงานและการลงเวลาแบบใหม่)
