# Supervisor Inspection (Backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give Supervisors the whole inspection workflow, from scanning a point and recording สะอาด / ไม่สะอาด to blocks, covers, the "My Building" page and live updates. Give the Admin the inspection GPS alert, the inspection sheet of the Excel export, and the Inspection Summary tab.

**Architecture:**
- **One migration (Task 1):**
  - `inspections` gets the GPS column set and `cover_assignment_id`.
  - `cover_assignments` gets a nullable `building_id`, and its `area_id` becomes nullable. A CHECK keeps exactly one of the two, as `docs/design/database.html` draws it.
- **Pure rules in `FacilityRealtime.Application`, unit-tested:**
  - `Inspections/SupervisorScope` decides whether a Supervisor may inspect a building now, or why not (`OTHER_BUILDING`, `OUTSIDE_SHIFT`).
  - `Inspections/InspectionRules` finds the submission a Supervisor may judge: the latest one of the current round.
  - `Rounds/RoundOutcomes` classifies a finished round as ตรวจผ่าน / ไม่ได้ตรวจ / ไม่ได้แก้.
  - `Inspections/InspectionHistory` and `Inspections/InspectionSummary` build the export rows and the summary numbers from plain records.
- **Reads that MySQL could treat differently from SQLite** live in Infrastructure as `InspectionHistoryQuery`. Task 10's `[MySqlFact]` runs them on real MySQL.
- **New endpoints:**
  - The Supervisor's phone: `POST /api/inspections/scan` (the scan), `POST /api/inspections` (the verdict) and `GET /api/my-building`.
  - The Admin: `GET /api/admin/inspection-summary`. The existing cover, alert and export endpoints learn the Supervisor cases.
- **Live updates:** `ScanRecorded` and the new `InspectionRecorded` go to the Admins and to the Supervisors of that point's building. They are addressed by user id, through an `IUserIdProvider` that reads the token's `sub` claim.

**Tech Stack:** .NET 10 Minimal API, EF Core 10 (`MySql.EntityFrameworkCore` 10.0.9 at runtime, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 in tests), SignalR, ClosedXML 0.104.2, xUnit 2.9.3.

**Spec:**
- `docs/adr/facility-0029-supervisor-inspection-workflow.md` and its amendments:
  - The Supervisor scans the point's QR and chooses สะอาด (Passed) or ไม่สะอาด (Rework).
  - A Rework needs defect notes.
  - The Cleaner fixes the work and submits again, and the card returns to รอตรวจ.
- `docs/adr/facility-0047-cleaning-rounds-are-time-windows.md`:
  - Rule 5: a new round starts the card over. An old round is "ไม่ได้ตรวจ" (submitted, not inspected) or "ไม่ได้แก้" (rework asked, not fixed).
  - Rule 6: a Supervisor inspects any number of times.
  - Rule 7: only work submitted in the current round can be inspected. With none, the screen says "รอบนี้แม่บ้านยังไม่ส่งงาน" and has no inspect button.
- `docs/adr/facility-0048-supervisor-assigned-to-building.md` and its amendments:
  - A Supervisor inspects only their own building, and only in their own shift by the clock (Day 07:00–19:00, Night 19:00–07:00).
  - Another building or outside that shift is a Blocked Scan. The Admin may give a cover for one shift.
  - A Supervisor never checks in, but every inspection keeps its time and GPS.
  - Rule 5: the Admin-only Inspection Summary tab, defaulting to the last 7 days.
- `docs/adr/facility-0041-scan-only-own-area-with-cover-assignment.md`: Blocked Scans, covers per shift, and assigning straight from a Blocked Scan. Its amendment applies the block rule to Supervisors, counted by building.
- `docs/adr/facility-0037-gps-for-every-scan-replaces-nfc.md`: every inspection reads GPS and compares it with the sign. Outside the radius it is recorded and flagged, never blocked.
- `docs/adr/facility-0057-supervisor-building-page-and-audit-log-page.md`, rule 1, the "ตึกของฉัน" page:
  - Every point of the own building, none of other buildings.
  - รอตรวจ first by submit time, then รอแม่บ้านแก้.
  - Opens outside the shift but cannot inspect there.
  - Updates itself when a Cleaner submits.
- `docs/adr/facility-0058-dashboard-is-admin-only.md`, rule 3: the live feed goes to Admins. Supervisors get only their own building's changes.
- `docs/adr/facility-0053-excel-export-instead-of-report-pages.md`: the inspection set (หัวหน้า, จุด, รอบ, ผล, ข้อบกพร่อง, ธง GPS) with "ไม่ได้ตรวจ / ไม่ได้แก้" rows, and no raw coordinates.
- `docs/adr/facility-0035-*`, `facility-0051-*`: alerts the Admin closes with "คุยแล้ว ปิดเตือน", and the audit log of Admin actions.
- `docs/design/database.html`: the `inspections`, `cover_assignments`, `blocked_scans` and `alert_reviews` columns and enum values.
- Wireframes in `docs/design/wireframe/`:
  - `SupervisorHome.dc.html` (M15), `SupervisorInspect.dc.html` (M11), `InspectDone.dc.html`, `InspectNothing.dc.html` (M16).
  - `SupervisorSummary.dc.html`, `BoardFlags.dc.html` (D3), `ExportExcel.dc.html` (D8).
  - Where a wireframe and an ADR disagree, the ADR wins.

## Global Constraints

- **Branch and merging:**
  - Work on branch `feat/supervisor-inspection`, created from `master` at `3eddfa6` (PR #54 merged).
  - Do not merge it.
- **One migration:**
  - Task 1 adds the only migration, named `SupervisorInspection`.
  - If a later task seems to need a schema change, stop and report instead of adding a migration.
- **No new package.** Everything needed is already referenced (ClosedXML is in Infrastructure since plan 5).
- **Endpoint security:**
  - Admin routes are under `/api/admin` with `RequireAuthorization(AuthSetup.AdminOnly)`. They load the actor with `CurrentUser.LoadAsync` and answer 401 when that returns null.
  - Supervisor routes use `RequireAuthorization()`, load the user the same way, and answer 403 with a Thai message to any other role.
- **Identity:** the inspector is the account in the access token, never a field in the body (facility-0017).
- **Audit log (facility-0051):**
  - Every Admin change (cover assign or cancel, alert review, export) writes one `audit_log` row in the same save or the same explicit transaction as the change.
  - Pass snapshots (anonymous objects), never entities.
  - Supervisor inspections are not Admin changes and are not logged there.
- **Never put a phone number, a password, a secret hash, a QR Token or raw coordinates in a response, the log or the Excel file.**
  - The scan answer may carry the distance to the sign and the in/out verdict.
- **Times:**
  - Stored DATETIMEs are UTC. Responses mark them UTC, so the JSON ends in `Z`.
  - The export prints Thai wall-clock `HH:mm` and Thai calendar dates `yyyy-MM-dd`.
  - A Supervisor's shift is the clock's: `ShiftCalendar.SlotAt(now)`. A Cleaner's is `AttendanceCalendar.SlotFor` (facility-0069).
- **Error format:** errors answer with `ApiResults.Message(status, "<Thai text>")`, the JSON `{ "message": ... }` that the phone and the Admin pages show as is.
- **Unused fields are left out.** A field that does not belong to the case (a building on a Cleaner's cover, a defect on สะอาด) gets 400 when sent. This is the plan 4–5 rule.
- **Runtime provider:** it is Oracle's `MySql.EntityFrameworkCore`, and SQLite tests cannot catch provider-only failures.
  - Task 10 adds a `[MySqlFact]` that runs when `FACILITY_MYSQL_TEST` is set.
  - Only use throwaway databases (`facility_*_test`), never `facility_dashboard`.
  - Ask the owner for the MySQL root password each time. Type it only into the command, and never write it into any file, this plan included, or into memory.
  - Subagents skip the MySQL steps; the controller runs them at the end.
- **This machine is the Mac:**
  - zsh, Homebrew MySQL, .NET 10 SDK.
  - Prefer the editor tools for files with Thai text, so nothing depends on shell quoting.
- **Public repo:** test data uses made-up names and `00000000xx`-style or seed-style phone numbers only.
- **Handoff after every task (owner's rule):**
  - The last step of every task adds one entry to a new section `## Task log (plan 6)` in `docs/handoff.md`, placed just above `## Pending work`. It is committed with the task.
  - The entry is in English and holds the date, the task, what changed, the test result and what comes next.
  - It holds no secret, phone number or real name.
- **Commits** end with the attribution trailer your session gives you.
- **Earlier rules stay:**
  - Keep every rule from PR #50–#54: auth, login throttle, the Admin-only Dashboard without QR Tokens, the Cleaner scan and attendance rules, Admin setup, attendance corrections, alerts, history, accounts and export.
  - Existing tests change only where a task says so.
- **Test baseline at `3eddfa6`:** 263 unit tests pass; API tests 424 pass and 4 are skipped (the MySQL tests, without `FACILITY_MYSQL_TEST`).

### Decisions made while planning (report them to the owner in the PR)

1. **A Supervisor's shift is the clock's.** The scan counts for `ShiftCalendar.SlotAt(now)`, with no 60-minute window around it, because Supervisors have no attendance (facility-0048 rule 3 and its amendment).
2. **Two calls: scan, then record.**
   - `POST /api/inspections/scan` is the scan. It may write a Blocked Scan, so it is a POST. It answers with the card, the submission to judge, the distance to the sign, and why there is no inspect button when there is none.
   - `POST /api/inspections` records the verdict. It repeats every check and also writes a Blocked Scan when blocked.
   - The record request carries the `scanRecordId` the phone was shown. If the Cleaner submitted again in between, the answer is 409 "scan again", so a Supervisor never judges work they did not see.
3. **The block checks the building first, then the shift.** Another building outside the shift is `OTHER_BUILDING`.
4. **What can be inspected (facility-0047 rules 2, 6, 7):**
   - The latest submission of the current round, which is the latest round of the clock's shift that has started. That stays true after the round's end time, until the next round starts.
   - Nothing submitted in that round: no button ("ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน").
   - Rework asked and nothing new submitted: no button ("รอแม่บ้านส่งงานแก้ ยังไม่มีงานใหม่ให้ตรวจ"), and 409 on record.
   - A round already ตรวจผ่าน can be inspected again, and the new verdict sets the card.
5. **Defect:**
   - Required for ไม่สะอาด: 1–1000 characters after trimming, matching the column.
   - Must be left out for สะอาด (400).
6. **Inspection GPS** is stored like a submission's (raw fix, distance, verdict).
   - Outside the radius it is the `INSPECTION_GPS` alert. It never blocks.
   - Without a location the inspection is refused (facility-0036).
7. **Supervisor covers share `cover_assignments`**, with `building_id` set and `area_id` null, as `database.html` draws it.
   - The same `POST /api/admin/cover-assignments` takes `buildingId` instead of `areaId`.
   - Any shift may be covered except the Supervisor's own building in their own shift, because the 0048 amendment uses covers for inspecting outside one's shift. This differs from Cleaners, who may cover only in their own shift.
   - A Supervisor cover lapses when the clock's shift ends.
   - No candidate list for buildings: the alert names the person to cover.
8. **`CoverAssignmentDto.CleanerName` becomes `PersonName`.** `AreaId`/`AreaCode` become nullable, and `BuildingId`/`BuildingCode` are added. No frontend reads it yet.
9. **A card in ต้องแก้ไข shows the defect** as `PointStatusDto.Rework { defect, requestedAt }` on the Dashboard, My Work and My Building. Wireframe InspectDone: "แม่บ้านเห็นข้อบกพร่องทันที".
10. **Live updates (facility-0057, 0058):** `ScanRecorded` and the new `InspectionRecorded` go to the Admins and to that building's Supervisors.
    - That means its active Supervisors of both shifts, plus anyone covering it in the clock's shift.
    - Cleaners still get no live feed, and My Work reloads on open. Parked, below.
11. **My Building** shows the clock's shift.
    - The own building comes first, then buildings covered this shift.
    - Order: รอตรวจ by submit time, then ต้องแก้ไข by the time it was asked, then the rest in card order.
    - `CanInspect` is false outside the own shift unless covered.
    - The wireframe's three counters are computed by the page from the statuses.
12. **M16's list of the day's rounds is left out.** The scan answer carries the card (status, current and next round) and the refusal text, which is what ADR 0047 rule 7 asks for.
13. **Round outcomes count only finished shifts.**
    - ไม่ได้ตรวจ / ไม่ได้แก้ are counted only for shifts whose attendance window has closed: `ClosesMinutesAfterShift` after the end, the same rule as "ไม่ได้ทำ" (plan 5 fix F1).
    - A round is identified by the copy on its submissions (point, shift date, shift, start, end), so a deleted window keeps its history.
14. **Export:**
    - New set `Inspections`, sheet "การตรวจงาน": one row per inspection, plus one row per finished round left ไม่ได้ตรวจ or ไม่ได้แก้.
    - ผล words: สะอาด / ต้องแก้ไข / ไม่ได้ตรวจ / ไม่ได้แก้.
    - A "ตรวจแทน" column.
    - The building filter is the point's building.
15. **Inspection Summary:** one row per active Supervisor with a building and shift. Their "beat" is their own building in their own shift.
    - จุดที่ตรวจแล้ว X of Y: the beat's active points (active Area that works that shift) this Supervisor inspected at least once, out of all of them.
    - งานที่แม่บ้านส่ง: the beat's submissions placed in a round. ตรวจแล้ว: those inspected by anyone.
    - ไม่ได้ตรวจ: the beat's finished rounds with that outcome.
    - ครั้งที่ตรวจ, ให้แก้ไข, ตรวจที่ติดธง GPS: this Supervisor's own inspections anywhere, covers included.
    - The range is by shift date: by default the 7 dates ending today, at most 31. The percentage is left to the page.
16. **No Supervisor action is written to `audit_log`.** It holds Admin changes (facility-0051), and inspections are records of their own.

### Out of scope (later plans)

- **Excel import of accounts** (ADR 0056, before rollout).
- **Frontend:** every page.
- **A live feed for Cleaners' My Work** (facility-0058 allows it for their own Area). The page reloads on open; add Area-addressed pushes with the frontend if needed.
- **The data-retention jobs** (facility-0055): clearing coordinates after 90 days now includes `inspections`, but no job exists yet.
- **Parked items from plans 3–5:** see `docs/handoff.md`. They are not touched here.

---

## File Structure

```
backend/
  src/FacilityRealtime.Domain/
    Entities/CoverAssignment.cs              AreaId nullable, + BuildingId, Building                          (modified, Task 1)
    Entities/InspectionRecord.cs             : IGpsStamped, + CoverAssignmentId                                (modified, Task 1)
    Enums/BlockReason.cs                     + OtherBuilding, OutsideShift                                    (modified, Task 2)
    Enums/AlertType.cs                       + InspectionGps                                                  (modified, Task 7)
  src/FacilityRealtime.Application/
    Inspections/SupervisorScope.cs           WithoutCover                                                     (new, Task 2)
    Inspections/InspectionRules.cs           Find, InspectionTarget, InspectionRefusal, MaxDefectLength       (new, Task 2)
    Rounds/RoundFacts.cs                     InspectionFact + Defect                                          (modified, Task 4)
    Rounds/PointStatusCalculator.cs          PointStatusResult + Rework                                       (modified, Task 4)
    Alerts/AlertKind.cs                      + InspectionGps                                                  (modified, Task 7)
    Rounds/RoundOutcomes.cs                  RoundOutcome, Of                                                 (new, Task 8)
    Inspections/InspectionHistory.cs         RoundSubmission, FinishedRound, FinishedRounds                   (new, Task 8)
    Export/ExportRules.cs                    ExportSet + Inspections                                          (modified, Task 8)
    Inspections/InspectionSummary.cs         Tally and its records                                            (new, Task 9)
  src/FacilityRealtime.Infrastructure/
    Persistence/AppDbContext.cs              cover CHECK, building FK, inspection GPS and cover FK            (modified, Task 1)
    Migrations/*_SupervisorInspection.cs     generated                                                        (new, Task 1)
    Persistence/RoundFactsQuery.cs           + Defect                                                         (modified, Task 4)
    Persistence/AlertQuery.cs                + inspection flags, block reason, Supervisor cover slot          (modified, Task 7)
    Persistence/InspectionHistoryQuery.cs    round submissions, inspections, point places                     (new, Task 8)
  src/FacilityRealtime.Api/
    Endpoints/AttendanceBoard.cs, MyWorkEndpoints.cs   area covers only                                       (modified, Task 1)
    Endpoints/BlockedScans.cs                reason + building                                                (modified, Task 3)
    Endpoints/AttendanceEndpoints.cs, ScanRecordEndpoints.cs   pass BlockReason.OtherArea                     (modified, Task 3; ScanRecord also Task 6)
    DTOs/AttendanceDtos.cs                   BlockedScanResponse + Reason, BuildingCode                       (modified, Task 3)
    DTOs/InspectionDtos.cs                   scan and record requests and answers                             (new, Task 3; + Task 4)
    Endpoints/InspectionEndpoints.cs         /api/inspections/scan, /api/inspections                         (new, Task 3; + Tasks 4, 6)
    Endpoints/ThaiLabels.cs                  + BlockedMessage, AlertName, InspectionResultName, RoundOutcomeName (modified, Tasks 3, 7, 8)
    DTOs/PointDtos.cs                        + ReworkDto, PointStatusDto.Rework; SplitTags public              (modified, Tasks 3, 4)
    DTOs/CoverDtos.cs                        building covers                                                  (modified, Task 5)
    Endpoints/CoverAssignmentEndpoints.cs    Supervisor covers                                                (modified, Task 5)
    Hubs/SubjectUserIdProvider.cs            Clients.User by "sub"                                            (new, Task 6)
    Hubs/ScanHub.cs                          doc comment                                                      (modified, Task 6)
    Endpoints/PointNotifications.cs          Admins + the building's Supervisors                              (new, Task 6)
    DTOs/MyBuildingDtos.cs, Endpoints/MyBuildingEndpoints.cs   /api/my-building                               (new, Task 6)
    DTOs/AdminAlertDtos.cs, Endpoints/AdminAlertEndpoints.cs   INSPECTION_GPS, reason, building cover     (modified, Task 7)
    Endpoints/ExportSheets.cs, AdminExportEndpoints.cs   "การตรวจงาน"                                         (modified, Task 8)
    DTOs/InspectionSummaryDtos.cs, Endpoints/AdminInspectionSummaryEndpoints.cs                               (new, Task 9)
    Program.cs                               map the new endpoint files, register the user id provider         (modified, Tasks 3, 6, 9)
  tests/FacilityRealtime.UnitTests/
    SupervisorScopeTests.cs, InspectionRulesTests.cs                                                          (new, Task 2)
    PointStatusCalculatorTests.cs            + rework carries its defect                                      (modified, Task 4)
    AlertKindsTests.cs                       + InspectionGps                                                  (modified, Task 7)
    RoundOutcomesTests.cs, InspectionHistoryTests.cs                                                          (new, Task 8)
    InspectionSummaryTests.cs                                                                                 (new, Task 9)
  tests/FacilityRealtime.ApiTests/
    Persistence/SchemaRuleTests.cs           + cover place CHECK, inspection GPS and cover                    (modified, Task 1)
    Infrastructure/TestData.cs               + AddPointAsync, AddSupervisorAsync, AddSupervisorCoverAsync;
                                               AddInspectionAsync options                                     (modified, Tasks 3, 8)
    Inspections/InspectionApi.cs             helpers and models                                               (new, Task 3; + Task 4)
    Inspections/InspectionScanEndpointTests.cs                                                                (new, Task 3)
    Inspections/InspectionEndpointTests.cs                                                                    (new, Task 4)
    Points/ServicePointEndpointTests.cs      PointModel + Rework                                              (modified, Task 4)
    Admin/CoverAssignmentEndpointTests.cs    CoverModel                                                       (modified, Task 5)
    Admin/SupervisorCoverTests.cs                                                                             (new, Task 5)
    Inspections/MyBuildingEndpointTests.cs, Inspections/SupervisorLiveTests.cs                                (new, Task 6)
    Admin/AdminAttendanceModels.cs, AlertEndpointTests.cs, CoverFromAlertTests.cs   model changes             (modified, Task 7)
    Admin/InspectionAlertTests.cs                                                                             (new, Task 7)
    Admin/InspectionExportTests.cs                                                                            (new, Task 8)
    Admin/InspectionSummaryEndpointTests.cs                                                                   (new, Task 9)
    Admin/DeactivatedAdminTests.cs           + the summary route                                              (modified, Task 9)
    Persistence/MySqlSupervisorTests.cs                                                                       (new, Task 10)
docs/handoff.md                              Task log entry                                                   (every task)
README.md, docs/architecture.md              new routes, schema and hub                                       (Task 10)
```

## Endpoint map

| Method | Route | Who | Body / query | Answer |
|---|---|---|---|---|
| POST | `/api/inspections/scan` | Supervisor | `{ qrToken, latitude, longitude, accuracyM }` | 200 `InspectionScanDto`; 403 `BlockedScanResponse` (Blocked Scan written) |
| POST | `/api/inspections` | Supervisor | `{ qrToken, scanRecordId, result, defect?, latitude, longitude, accuracyM }` | 201 `InspectionCreatedResponse`; 409 when there is nothing (new) to judge |
| GET | `/api/my-building` | Supervisor | — | 200 `MyBuildingDto` |
| POST | `/api/admin/cover-assignments` | Admin | Cleaner: `{ userId, areaId, shiftDate, shift, blockedScanId? }`; Supervisor: `{ userId, buildingId, shiftDate, shift, blockedScanId? }` | 201 `CoverAssignmentDto` |
| GET | `/api/admin/alerts` | Admin | unchanged | + `InspectionGps` lines, `blockReason`, building cover suggestions |
| POST | `/api/admin/export` | Admin | `sets` may include `Inspections` | + sheet "การตรวจงาน" |
| GET | `/api/admin/inspection-summary` | Admin | `from`, `to` (yyyy-MM-dd, both or neither), `buildingId?` | 200 `InspectionSummaryDto` |

Hub events (`/hubs/scan`), payload `PointStatusDto`:
- `ScanRecorded` goes to the Admins and the building's Supervisors.
- `InspectionRecorded` (new) goes to the same people.

---

### Task 1: Schema for Supervisor covers and inspection GPS

**Files:**
- Modify: `backend/src/FacilityRealtime.Domain/Entities/CoverAssignment.cs`
- Modify: `backend/src/FacilityRealtime.Domain/Entities/InspectionRecord.cs`
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/AppDbContext.cs` (the `InspectionRecord` and `CoverAssignment` blocks)
- Create (generated): `backend/src/FacilityRealtime.Infrastructure/Migrations/<timestamp>_SupervisorInspection.cs`, its `.Designer.cs`, and the updated `AppDbContextModelSnapshot.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AttendanceBoard.cs:34-38`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/MyWorkEndpoints.cs:36-39`
- Test: `backend/tests/FacilityRealtime.ApiTests/Persistence/SchemaRuleTests.cs` (append)

**Interfaces:**
- Produces:
  - `CoverAssignment.AreaId` is now `int?`.
  - New `CoverAssignment.BuildingId` (`int?`) and `CoverAssignment.Building` (`Building?`).
  - `InspectionRecord` now implements `IGpsStamped` (`Latitude`, `Longitude`, `AccuracyM`, `DistanceM`, `WithinRadius`). It gains `CoverAssignmentId` (`int?`) and `CoverAssignment` (`CoverAssignment?`).
  - The database refuses a cover with both or neither of `area_id` and `building_id`.

- [ ] **Step 0: Create the branch**

```bash
git checkout master
git pull --ff-only
git checkout -b feat/supervisor-inspection
```

Expected: `git log -1 --format=%h` prints `3eddfa6` before any commit of yours.

- [ ] **Step 1: Write the failing tests**

Append inside the `SchemaRuleTests` class in `backend/tests/FacilityRealtime.ApiTests/Persistence/SchemaRuleTests.cs` (its `using` lines already cover `ApiTests.Infrastructure`, `Domain.Entities`, `Domain.Enums` and EF Core):

```csharp
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task A_cover_is_for_an_area_or_a_building_never_both_or_neither(bool withArea, bool withBuilding)
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == "S2001");
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            db.CoverAssignments.Add(new CoverAssignment
            {
                UserId = supervisor.Id,
                AreaId = withArea ? (await db.Areas.FirstAsync()).Id : null,
                BuildingId = withBuilding ? (await db.Buildings.FirstAsync()).Id : null,
                ShiftDate = new DateOnly(2026, 10, 8),
                Shift = Shift.Night,
                AssignedById = admin.Id,
                AssignedAt = DateTime.UtcNow,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task A_supervisor_cover_names_a_building_and_an_inspection_keeps_its_gps_and_cover()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == "S2001");
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "E1001");
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            var building = await db.Buildings.SingleAsync(b => b.Code == "A");
            var sign = await db.Signs.SingleAsync(s => s.QrToken == "token-restroom-m1");
            var cover = new CoverAssignment
            {
                UserId = supervisor.Id,
                BuildingId = building.Id,
                ShiftDate = new DateOnly(2026, 10, 8),
                Shift = Shift.Night,
                AssignedById = admin.Id,
                AssignedAt = DateTime.UtcNow,
            };
            var scan = new ScanRecord
            {
                ServicePointId = sign.ServicePointId!.Value,
                SignId = sign.Id,
                UserId = cleaner.Id,
                ShiftDate = new DateOnly(2026, 10, 8),
                Shift = Shift.Night,
                Placement = Placement.OnTime,
                SubmittedAt = DateTime.UtcNow,
            };
            db.CoverAssignments.Add(cover);
            db.ScanRecords.Add(scan);
            await db.SaveChangesAsync();
            db.InspectionRecords.Add(new InspectionRecord
            {
                ScanRecordId = scan.Id,
                ServicePointId = scan.ServicePointId,
                SupervisorId = supervisor.Id,
                Result = InspectionResult.Passed,
                InspectedAt = DateTime.UtcNow,
                CoverAssignmentId = cover.Id,
                Latitude = 13.800000m,
                Longitude = 100.600000m,
                AccuracyM = 10,
                DistanceM = 12000,
                WithinRadius = false,
            });
            await db.SaveChangesAsync();
        });

        await factory.WithDbAsync(async db =>
        {
            var inspection = await db.InspectionRecords.Include(i => i.CoverAssignment).SingleAsync();
            Assert.False(inspection.WithinRadius);
            Assert.Equal((short)12000, inspection.DistanceM);
            Assert.Null(inspection.CoverAssignment!.AreaId);
            Assert.NotNull(inspection.CoverAssignment.BuildingId);
        });
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~SchemaRuleTests"`
Expected: build FAILS with `'CoverAssignment' does not contain a definition for 'BuildingId'` (and the same for `InspectionRecord.Latitude`).

- [ ] **Step 3: Change the two entities**

Replace `backend/src/FacilityRealtime.Domain/Entities/CoverAssignment.cs` with:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>
/// CONTEXT.md, Cover Assignment: an Admin lets a Cleaner work another Area for one shift (facility-0041), or a Supervisor
/// inspect a building outside their own building or shift (facility-0048). Exactly one of AreaId and BuildingId is set
/// (a CHECK enforces it). A Cleaner's cover lapses when their attendance window closes (facility-0069); a Supervisor's
/// when the clock's shift ends.
/// </summary>
public class CoverAssignment
{
    public int Id { get; set; }

    /// <summary>The Cleaner or Supervisor who covers.</summary>
    public int UserId { get; set; }

    /// <summary>The Area a Cleaner covers; null for a Supervisor's cover.</summary>
    public int? AreaId { get; set; }

    /// <summary>The building a Supervisor inspects; null for a Cleaner's cover.</summary>
    public int? BuildingId { get; set; }

    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public int AssignedById { get; set; }
    public DateTime AssignedAt { get; set; }
    public int? CancelledById { get; set; }
    public DateTime? CancelledAt { get; set; }

    public User? User { get; set; }
    public Area? Area { get; set; }
    public Building? Building { get; set; }
}
```

Replace `backend/src/FacilityRealtime.Domain/Entities/InspectionRecord.cs` with:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Inspection Record: a Supervisor's verdict on one Scan Record, with the GPS of the inspection (facility-0037).</summary>
public class InspectionRecord : IGpsStamped
{
    public long Id { get; set; }
    public long ScanRecordId { get; set; }

    /// <summary>Repeats ScanRecord.ServicePointId so a point's status query needs no join.</summary>
    public int ServicePointId { get; set; }

    public int SupervisorId { get; set; }
    public InspectionResult Result { get; set; }

    /// <summary>Required when Result is Rework (a CHECK constraint enforces it).</summary>
    public string? Defect { get; set; }

    public DateTime InspectedAt { get; set; }

    /// <summary>Set when the Supervisor inspected under a Cover Assignment: another building, or outside their shift (facility-0048).</summary>
    public int? CoverAssignmentId { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public short? AccuracyM { get; set; }
    public short? DistanceM { get; set; }
    public bool? WithinRadius { get; set; }

    public ScanRecord? ScanRecord { get; set; }
    public User? Supervisor { get; set; }
    public CoverAssignment? CoverAssignment { get; set; }
}
```

- [ ] **Step 4: Map them in `AppDbContext`**

In `backend/src/FacilityRealtime.Infrastructure/Persistence/AppDbContext.cs`, replace the whole `modelBuilder.Entity<InspectionRecord>(e => { ... });` block with:

```csharp
        modelBuilder.Entity<InspectionRecord>(e =>
        {
            e.ToTable("inspections", t => t.HasCheckConstraint(
                "ck_inspections_rework_has_defect", "result <> 'REWORK' OR defect IS NOT NULL"));
            e.Property(x => x.Result).HasConversion(new UpperSnakeEnumConverter<InspectionResult>()).HasMaxLength(10);
            e.Property(x => x.Defect).HasMaxLength(1000);
            MapGps(e);
            e.HasIndex(x => new { x.ServicePointId, x.InspectedAt });
            e.HasIndex(x => new { x.SupervisorId, x.InspectedAt });
            // The alert list reads flagged inspections by time, as it reads scan_records by submitted_at
            e.HasIndex(x => x.InspectedAt);
            e.HasOne(x => x.ScanRecord).WithMany().HasForeignKey(x => x.ScanRecordId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ServicePoint>().WithMany().HasForeignKey(x => x.ServicePointId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CoverAssignment).WithMany().HasForeignKey(x => x.CoverAssignmentId).OnDelete(DeleteBehavior.Restrict);
        });
```

Replace the whole `modelBuilder.Entity<CoverAssignment>(e => { ... });` block with:

```csharp
        modelBuilder.Entity<CoverAssignment>(e =>
        {
            // database.html: a Cleaner covers an Area, a Supervisor a building; never both, never neither
            e.ToTable("cover_assignments", t => t.HasCheckConstraint(
                "ck_cover_assignments_one_place",
                "(area_id IS NOT NULL AND building_id IS NULL) OR (area_id IS NULL AND building_id IS NOT NULL)"));
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.HasIndex(x => new { x.UserId, x.ShiftDate, x.Shift });
            e.HasIndex(x => new { x.AreaId, x.ShiftDate, x.Shift });
            e.HasIndex(x => new { x.BuildingId, x.ShiftDate, x.Shift });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledById).OnDelete(DeleteBehavior.Restrict);
        });
```

Also update the doc comment of `MapGps` if it lists tables; it does not today, so leave it.

- [ ] **Step 5: Fix the two Area-cover queries the nullable `AreaId` breaks**

In `backend/src/FacilityRealtime.Api/Endpoints/AttendanceBoard.cs`, the covers query: the attendance page shows Cleaners' Area covers only. Replace

```csharp
            .Where(c => c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null && areaIds.Contains(c.AreaId))
```

with

```csharp
            .Where(c => c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null
                && c.AreaId != null && areaIds.Contains(c.AreaId.Value))
```

In `backend/src/FacilityRealtime.Api/Endpoints/MyWorkEndpoints.cs`, replace

```csharp
        var coveredAreaIds = await db.CoverAssignments
            .Where(c => c.UserId == user.Id && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Select(c => c.AreaId)
            .ToListAsync();
```

with

```csharp
        var coveredAreaIds = await db.CoverAssignments
            .Where(c => c.UserId == user.Id && c.AreaId != null && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Select(c => c.AreaId!.Value)
            .ToListAsync();
```

Run `dotnet build backend/FacilityRealtime.slnx`. Expected: 0 errors. If another file fails to compile on `AreaId`, it compares `int?` with `int` in a way C# refuses. Fix it the same way (`c.AreaId != null && … c.AreaId.Value`) and list the file in your report.

- [ ] **Step 6: Add the migration**

Run from the repository root:

```bash
dotnet tool restore
dotnet ef migrations add SupervisorInspection --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api
```

Open the generated `<timestamp>_SupervisorInspection.cs` and check that `Up` does exactly this and nothing else:
- `inspections`: adds `cover_assignment_id`, `latitude`, `longitude`, `accuracy_m`, `distance_m` and `within_radius`, all nullable. It adds the indexes `IX_inspections_inspected_at` and `IX_inspections_cover_assignment_id`, and the FK to `cover_assignments`.
- `cover_assignments`: `area_id` becomes nullable. It adds `building_id` (nullable), `IX_cover_assignments_building_id_shift_date_shift`, the FK to `buildings` and the CHECK `ck_cover_assignments_one_place`.

If it drops or recreates any table, or touches another table, stop and report.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~SchemaRuleTests"`
Expected: PASS.

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS. Unit tests include `Migrations_match_the_model`. The API tests are the baseline plus 3, with 4 skipped.

- [ ] **Step 8: Update the handoff and commit**

In `docs/handoff.md`, add this row to the Status table:

```markdown
| 6 | Supervisor inspection, covers, My Building, alerts, export sheet, summary | in progress (branch `feat/supervisor-inspection`): `docs/superpowers/plans/2026-10-14-supervisor-inspection-backend.md` |
```

Then add the section `## Task log (plan 6)` just above `## Pending work`, with:

```markdown
- 2026-10-14 · Task 1 done: migration SupervisorInspection (inspection GPS and cover; Supervisor covers by building with a one-place CHECK); attendance board and My Work read Area covers only. All tests pass (MySQL skipped). Next: Task 2 (Supervisor rules).
```

Use the real date if it is not 2026-10-14.

```bash
git add backend/src backend/tests docs/handoff.md
git commit -m "feat(backend): schema for Supervisor covers by building and inspection GPS (facility-0037, 0048)"
```

---

### Task 2: Supervisor scope and inspection rules

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Inspections/SupervisorScope.cs`
- Create: `backend/src/FacilityRealtime.Application/Inspections/InspectionRules.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/SupervisorScopeTests.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/InspectionRulesTests.cs`
- Modify: `backend/src/FacilityRealtime.Domain/Enums/BlockReason.cs` (the two new values are needed here already)

**Interfaces:**
- Consumes:
  - `RoundRules.Current`, `SubmissionsOf`, `LatestInspection` and `NeedsRework` (internal, same assembly).
  - `RoundWindow`, `SubmissionFact`, `InspectionFact`.
- Produces:
  - `BlockReason.OtherBuilding` and `BlockReason.OutsideShift`.
  - `SupervisorScope.WithoutCover(int? ownBuildingId, Shift? ownShift, int pointBuildingId, Shift clockShift) : BlockReason?`. It returns null when no cover is needed.
  - `enum InspectionRefusal { NothingSubmitted, AwaitingRework }`
  - `sealed record InspectionTarget(SubmissionFact? Submission, InspectionRefusal? Refusal)`
  - `InspectionRules.Find(bool areaWorksThisShift, IReadOnlyList<RoundWindow> windows, IReadOnlyList<SubmissionFact> submissions, IReadOnlyList<InspectionFact> inspections, DateTime nowUtc) : InspectionTarget`
  - `InspectionRules.MaxDefectLength` (`const int`, 1000).

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.UnitTests/SupervisorScopeTests.cs`:

```csharp
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0048 and its 2026-10-07 amendment: own building, own shift by the clock; anything else needs a cover.</summary>
public class SupervisorScopeTests
{
    [Theory]
    [InlineData(1, Shift.Day, 1, Shift.Day, null)]
    [InlineData(1, Shift.Night, 1, Shift.Night, null)]
    [InlineData(1, Shift.Day, 2, Shift.Day, BlockReason.OtherBuilding)]
    [InlineData(1, Shift.Day, 1, Shift.Night, BlockReason.OutsideShift)]
    [InlineData(1, Shift.Day, 2, Shift.Night, BlockReason.OtherBuilding)]
    public void Own_building_in_own_shift_needs_no_cover(int ownBuilding, Shift ownShift, int pointBuilding, Shift clockShift, BlockReason? expected) =>
        Assert.Equal(expected, SupervisorScope.WithoutCover(ownBuilding, ownShift, pointBuilding, clockShift));

    [Fact]
    public void A_supervisor_without_a_building_is_in_no_building() =>
        Assert.Equal(BlockReason.OtherBuilding, SupervisorScope.WithoutCover(null, Shift.Day, 1, Shift.Day));
}
```

Create `backend/tests/FacilityRealtime.UnitTests/InspectionRulesTests.cs`:

```csharp
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0047 rules 2, 6 and 7 on 8 Oct with rounds 08:00-10:00 (id 1) and 16:00-18:00 (id 2).</summary>
public class InspectionRulesTests
{
    private static readonly ShiftSlot Day8 = new(new DateOnly(2026, 10, 8), Shift.Day);

    private static readonly IReadOnlyList<RoundWindow> Rounds =
    [
        RoundWindow.For(1, new TimeOnly(8, 0), new TimeOnly(10, 0), Day8),
        RoundWindow.For(2, new TimeOnly(16, 0), new TimeOnly(18, 0), Day8),
    ];

    private static DateTime Thai(int hour, int minute) =>
        new DateTime(2026, 10, 8, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);

    private static SubmissionFact Sent(long id, int? roundId, int hour, int minute) => new(id, roundId, Thai(hour, minute));

    private static InspectionFact Inspected(long submissionId, InspectionResult result, int hour, int minute) =>
        new(submissionId, result, Thai(hour, minute));

    private static InspectionTarget Find(
        DateTime now, SubmissionFact[]? submissions = null, InspectionFact[]? inspections = null, bool areaWorksThisShift = true) =>
        InspectionRules.Find(areaWorksThisShift, Rounds, submissions ?? [], inspections ?? [], now);

    [Fact]
    public void Nothing_to_inspect_before_the_first_round()
    {
        var target = Find(Thai(7, 30));

        Assert.Null(target.Submission);
        Assert.Equal(InspectionRefusal.NothingSubmitted, target.Refusal);
    }

    [Fact]
    public void Nothing_to_inspect_while_the_current_round_has_no_submission() =>
        Assert.Equal(InspectionRefusal.NothingSubmitted, Find(Thai(8, 30)).Refusal);

    [Fact]
    public void An_earlier_rounds_submission_is_not_inspected() =>
        Assert.Equal(InspectionRefusal.NothingSubmitted, Find(Thai(16, 30), [Sent(1, 1, 8, 10)]).Refusal);

    [Fact]
    public void An_off_round_submission_is_not_inspected() =>
        Assert.Equal(InspectionRefusal.NothingSubmitted, Find(Thai(8, 30), [Sent(1, null, 8, 10)]).Refusal);

    [Fact]
    public void An_area_without_this_shift_has_nothing_to_inspect() =>
        Assert.Equal(InspectionRefusal.NothingSubmitted, Find(Thai(8, 30), [Sent(1, 1, 8, 10)], areaWorksThisShift: false).Refusal);

    [Fact]
    public void The_latest_submission_of_the_current_round_is_inspected()
    {
        var target = Find(Thai(9, 0), [Sent(1, 1, 8, 10), Sent(2, 1, 8, 40)]);

        Assert.Equal(2, target.Submission!.Id);
        Assert.Null(target.Refusal);
    }

    [Fact]
    public void A_round_stays_inspectable_after_its_end_until_the_next_round_starts() =>
        Assert.Equal(1, Find(Thai(15, 59), [Sent(1, 1, 8, 10)]).Submission!.Id);

    [Fact]
    public void Rework_waits_for_the_cleaner()
    {
        var target = Find(Thai(9, 0), [Sent(1, 1, 8, 10)], [Inspected(1, InspectionResult.Rework, 8, 20)]);

        Assert.Null(target.Submission);
        Assert.Equal(InspectionRefusal.AwaitingRework, target.Refusal);
    }

    [Fact]
    public void The_resubmission_after_rework_is_inspected() =>
        Assert.Equal(
            2,
            Find(Thai(9, 0), [Sent(1, 1, 8, 10), Sent(2, 1, 8, 40)], [Inspected(1, InspectionResult.Rework, 8, 20)]).Submission!.Id);

    [Fact]
    public void A_passed_round_can_be_inspected_again()
    {
        var target = Find(Thai(9, 0), [Sent(1, 1, 8, 10)], [Inspected(1, InspectionResult.Passed, 8, 20)]);

        Assert.Equal(1, target.Submission!.Id);
        Assert.Null(target.Refusal);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~SupervisorScopeTests|FullyQualifiedName~InspectionRulesTests"`
Expected: build FAILS with `The type or namespace name 'Inspections' does not exist in the namespace 'FacilityRealtime.Application'`.

- [ ] **Step 3: Add the block reasons**

Replace `backend/src/FacilityRealtime.Domain/Enums/BlockReason.cs` with:

```csharp
namespace FacilityRealtime.Domain.Enums;

/// <summary>Why a scan was refused (CONTEXT.md, Blocked Scan; database.html blocked_scans.reason).</summary>
public enum BlockReason
{
    /// <summary>A Cleaner scanned a sign of another Area (facility-0041).</summary>
    OtherArea,

    /// <summary>A Supervisor scanned a point in another building (facility-0048 rule 2).</summary>
    OtherBuilding,

    /// <summary>A Supervisor scanned a point of their own building outside their own shift (facility-0048 amendment).</summary>
    OutsideShift,
}
```

The column is `VARCHAR(20)` and `OTHER_BUILDING` is 14 characters, so no migration is needed.

- [ ] **Step 4: Write the rules**

Create `backend/src/FacilityRealtime.Application/Inspections/SupervisorScope.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Inspections;

/// <summary>facility-0048 rule 2 and its 2026-10-07 amendment: where and when a Supervisor inspects without a cover.</summary>
public static class SupervisorScope
{
    /// <summary>
    /// Null when the point is in the Supervisor's own building and the clock is in their own shift. Otherwise the reason a
    /// scan without a Cover Assignment is blocked: the building is checked first, so another building at night is OtherBuilding.
    /// </summary>
    public static BlockReason? WithoutCover(int? ownBuildingId, Shift? ownShift, int pointBuildingId, Shift clockShift) =>
        ownBuildingId != pointBuildingId ? BlockReason.OtherBuilding
        : ownShift != clockShift ? BlockReason.OutsideShift
        : null;
}
```

Create `backend/src/FacilityRealtime.Application/Inspections/InspectionRules.cs`:

```csharp
using FacilityRealtime.Application.Rounds;

namespace FacilityRealtime.Application.Inspections;

/// <summary>Why the Supervisor's screen has no inspect button.</summary>
public enum InspectionRefusal
{
    /// <summary>The current round has no submission yet, or there is no current round (facility-0047 rule 7).</summary>
    NothingSubmitted,

    /// <summary>The latest inspection asked for rework and the Cleaner has not submitted since (facility-0029).</summary>
    AwaitingRework,
}

/// <summary>Exactly one of Submission and Refusal is set.</summary>
public sealed record InspectionTarget(SubmissionFact? Submission, InspectionRefusal? Refusal);

public static class InspectionRules
{
    /// <summary>inspections.defect is VARCHAR(1000).</summary>
    public const int MaxDefectLength = 1000;

    /// <summary>
    /// facility-0047 rules 2, 6 and 7: the Supervisor judges the latest submission of the current round — the latest round
    /// of the shift whose start has passed — and may do so any number of times, but not while a rework is still owed.
    /// </summary>
    public static InspectionTarget Find(
        bool areaWorksThisShift,
        IReadOnlyList<RoundWindow> windows,
        IReadOnlyList<SubmissionFact> submissions,
        IReadOnlyList<InspectionFact> inspections,
        DateTime nowUtc)
    {
        var round = areaWorksThisShift ? RoundRules.Current(windows, nowUtc) : null;
        var roundSubmissions = round is null ? new List<SubmissionFact>() : RoundRules.SubmissionsOf(round, submissions);
        if (roundSubmissions.Count == 0)
        {
            return new InspectionTarget(null, InspectionRefusal.NothingSubmitted);
        }

        if (RoundRules.NeedsRework(roundSubmissions, RoundRules.LatestInspection(roundSubmissions, inspections)))
        {
            return new InspectionTarget(null, InspectionRefusal.AwaitingRework);
        }

        return new InspectionTarget(roundSubmissions.OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id).Last(), null);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests`
Expected: PASS: 263 plus the 16 new cases (6 + 10).

- [ ] **Step 6: Update the handoff and commit**

Add under `## Task log (plan 6)`:

```markdown
- 2026-10-14 · Task 2 done: BlockReason OtherBuilding and OutsideShift; SupervisorScope.WithoutCover and InspectionRules.Find (latest submission of the current round, nothing while rework is owed). Unit tests pass. Next: Task 3 (the Supervisor's scan).
```

```bash
git add backend/src/FacilityRealtime.Domain backend/src/FacilityRealtime.Application backend/tests/FacilityRealtime.UnitTests docs/handoff.md
git commit -m "feat(backend): Supervisor scope and inspection target rules (facility-0047, 0048)"
```

---
### Task 3: The Supervisor's scan (`POST /api/inspections/scan`) and block reasons

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/BlockedScans.cs`
- Modify: `backend/src/FacilityRealtime.Api/DTOs/AttendanceDtos.cs` (`BlockedScanResponse`)
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AttendanceEndpoints.cs:68`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/ScanRecordEndpoints.cs:89`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs` (+ `BlockedMessage`)
- Modify: `backend/src/FacilityRealtime.Api/DTOs/PointDtos.cs` (`SplitTags` becomes public)
- Create: `backend/src/FacilityRealtime.Api/DTOs/InspectionDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/InspectionEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/Inspections/InspectionApi.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Inspections/InspectionScanEndpointTests.cs`

**Interfaces:**
- Consumes:
  - Task 2's `SupervisorScope.WithoutCover`, `InspectionRules.Find`, `InspectionRefusal` and `BlockReason.OtherBuilding` / `OutsideShift`.
  - Task 1's `CoverAssignment.BuildingId`.
  - Existing: `RoundFactsQuery.LoadAsync`, `PointBoardQuery.LoadAsync(db, nowUtc, servicePointId)`, `Geofence.Evaluate`, `GpsInput.TryRead` and `CurrentUser.LoadAsync`.
- Produces:
  - `BlockedScans.BlockAsync(AppDbContext db, User user, Sign sign, Area signArea, BlockReason reason, GpsReading gps, DateTime nowUtc) : Task<IResult>`. It answers 403 with `BlockedScanResponse(Message, AreaCode, AreaName, Reason, BuildingCode)`.
  - `ThaiLabels.BlockedMessage(BlockReason) : string`
  - `PointDtoMapper.SplitTags(string?) : IReadOnlyList<string>` (now public).
  - In `InspectionEndpoints`, the private building blocks that Tasks 4 and 6 extend:
    - `Visit(User Supervisor, Sign Sign, ServicePoint Point, ShiftSlot Slot, CoverAssignment? Cover, GpsReading Gps, DateTime Now)`
    - `VisitAsync(principal, db, clock, qrToken, latitude, longitude, accuracyM) : Task<(Visit? Visit, IResult? Stop)>`
    - `TargetAsync(db, visit) : Task<InspectionTarget>`
    - `RefusalText(InspectionRefusal?) : string?`
  - DTOs: `InspectionScanRequest(string? QrToken, double? Latitude, double? Longitude, double? AccuracyM)`, `InspectionSubmissionDto`, and `InspectionScanDto(PointStatusDto Point, InspectionSubmissionDto? Submission, string? Refusal, bool IsCover, int? DistanceM, bool? WithinRadius)`.
  - Test helpers:
    - `TestData.AddPointAsync(factory, areaCode, signCode, qrToken) : Task<int>`
    - `TestData.AddSupervisorAsync(factory, employeeId, phone, buildingCode, shift) : Task<int>`
    - `TestData.AddSupervisorCoverAsync(factory, employeeId, buildingCode, shiftDate, shift, cancelled = false) : Task<int>`
    - `InspectionApi.SupervisorAsync`, `ScanAsync`, `ScannedAsync` and `AddBuildingBAsync`, and the constants `MenRestroom` and `BuildingBPoint`.
    - Models: `InspectionScanModel`, `InspectionSubmissionModel` and `BlockedModel`.

- [ ] **Step 1: Add the test helpers**

In `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs`, add these `using` lines at the top:

```csharp
using FacilityRealtime.Application.Auth;
using Microsoft.Extensions.DependencyInjection;
```

and append inside the `TestData` class:

```csharp
    /// <summary>
    /// A point in <paramref name="areaCode"/> whose sign stands at TestGps.Near (radius 50 m), with rounds Day 08:00-10:00
    /// and Night 20:00-22:00, all created on 1 October so they existed on every test date.
    /// </summary>
    public static async Task<int> AddPointAsync(FacilityApiFactory factory, string areaCode, string signCode, string qrToken)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var area = await db.Areas.SingleAsync(a => a.Code == areaCode);
            var longAgo = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var point = new ServicePoint { AreaId = area.Id, Name = $"จุด {signCode}", SortOrder = 1, CreatedAt = longAgo };
            db.Signs.Add(new Sign
            {
                AreaId = area.Id,
                ServicePoint = point,
                Code = signCode,
                QrToken = qrToken,
                QrIssuedAt = longAgo,
                Latitude = 13.756300m,
                Longitude = 100.501800m,
                LocationAccuracyM = 10,
                LocationSource = LocationSource.Site,
                LocatedAt = longAgo,
            });
            db.PointRoundWindows.AddRange(
                new PointRoundWindow { ServicePoint = point, Shift = Shift.Day, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(10, 0), CreatedAt = longAgo },
                new PointRoundWindow { ServicePoint = point, Shift = Shift.Night, StartTime = new TimeOnly(20, 0), EndTime = new TimeOnly(22, 0), CreatedAt = longAgo });
            await db.SaveChangesAsync();
            id = point.Id;
        });
        return id;
    }

    /// <summary>An active Supervisor of a building and shift who can log in with <paramref name="phone"/>.</summary>
    public static async Task<int> AddSupervisorAsync(FacilityApiFactory factory, string employeeId, string phone, string buildingCode, Shift shift)
    {
        var hasher = factory.Services.GetRequiredService<IPasswordHasher>();
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var building = await db.Buildings.SingleAsync(b => b.Code == buildingCode);
            var supervisor = new User
            {
                Role = UserRole.Supervisor,
                EmployeeId = employeeId,
                DisplayName = $"หัวหน้าทดสอบ {employeeId}",
                SecretHash = hasher.Hash(PhoneNumber.Normalize(phone)),
                BuildingId = building.Id,
                Shift = shift,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(supervisor);
            await db.SaveChangesAsync();
            id = supervisor.Id;
        });
        return id;
    }

    /// <summary>A Supervisor's Cover Assignment for a building, written straight to the database, assigned by the seeded Admin.</summary>
    public static async Task<int> AddSupervisorCoverAsync(
        FacilityApiFactory factory, string employeeId, string buildingCode, DateOnly shiftDate, Shift shift, bool cancelled = false)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var building = await db.Buildings.SingleAsync(b => b.Code == buildingCode);
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            var cover = new CoverAssignment
            {
                UserId = supervisor.Id,
                BuildingId = building.Id,
                ShiftDate = shiftDate,
                Shift = shift,
                AssignedById = admin.Id,
                AssignedAt = DateTime.UtcNow,
                CancelledById = cancelled ? admin.Id : null,
                CancelledAt = cancelled ? DateTime.UtcNow : null,
            };
            db.CoverAssignments.Add(cover);
            await db.SaveChangesAsync();
            id = cover.Id;
        });
        return id;
    }
```

Create `backend/tests/FacilityRealtime.ApiTests/Inspections/InspectionApi.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Points;

namespace FacilityRealtime.ApiTests.Inspections;

public record InspectionSubmissionModel(
    long Id, DateTime SubmittedAt, string CleanerName, string Placement, string Status, List<string> IssueTags, string? Note);

public record InspectionScanModel(
    PointModel Point, InspectionSubmissionModel? Submission, string? Refusal, bool IsCover, int? DistanceM, bool? WithinRadius);

public record BlockedModel(string Message, string AreaCode, string AreaName, string Reason, string BuildingCode);

/// <summary>
/// The Supervisor's phone. Seeded S2001 (phone 0820000001) is the Day Supervisor of building A, which holds AR01 (both
/// restrooms) and AR02 (the meeting room). Log in before moving the clock.
/// </summary>
public static class InspectionApi
{
    public const string MenRestroom = "token-restroom-m1";
    public const string BuildingBPoint = "token-br01-01";

    public static async Task<HttpClient> SupervisorAsync(FacilityApiFactory factory, string employeeId = "S2001", string phone = "0820000001") =>
        (await AuthApi.LoggedInAsEmployeeAsync(factory, employeeId, phone)).Client;

    public static Task<HttpResponseMessage> ScanAsync(
        HttpClient client,
        string qrToken = MenRestroom,
        double latitude = TestGps.NearLatitude,
        double longitude = TestGps.NearLongitude,
        double accuracyM = 10) =>
        client.PostAsJsonAsync("/api/inspections/scan", new { qrToken, latitude, longitude, accuracyM });

    /// <summary>Scans and asserts 200.</summary>
    public static async Task<InspectionScanModel> ScannedAsync(HttpClient client, string qrToken = MenRestroom)
    {
        var response = await ScanAsync(client, qrToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InspectionScanModel>())!;
    }

    /// <summary>Building B with Area BR01 and one point: sign BR01-01 (token-br01-01) at TestGps.Near, rounds Day 08-10 and Night 20-22.</summary>
    public static async Task AddBuildingBAsync(FacilityApiFactory factory)
    {
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddAreaAsync(factory, "BR01", "B");
        await TestData.AddPointAsync(factory, "BR01", "BR01-01", BuildingBPoint);
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `backend/tests/FacilityRealtime.ApiTests/Inspections/InspectionScanEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Inspections;

/// <summary>
/// facility-0047 rule 7, 0048: the Supervisor's scan. Seeded men's restroom (AR01, building A): Day rounds 07:00-09:00 and
/// 16:00-18:00, Night 20:00-22:00 and 03:00-05:00. TestData.AddScanAsync writes a submission by Cleaner E1001.
/// </summary>
public class InspectionScanEndpointTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    private static async Task<List<BlockedScan>> BlockedAsync(FacilityApiFactory factory)
    {
        var rows = new List<BlockedScan>();
        await factory.WithDbAsync(async db => rows = await db.BlockedScans.ToListAsync());
        return rows;
    }

    [Fact]
    public async Task Scanning_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await InspectionApi.ScanAsync(factory.CreateApiClient());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("cleaner")]
    [InlineData("admin")]
    public async Task Only_supervisors_inspect(string who)
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = who == "admin" ? await AuthApi.LoggedInAdminAsync(factory) : await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.ScanAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await BlockedAsync(factory));
    }

    [Fact]
    public async Task Location_is_required()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);

        var response = await supervisor.PostAsJsonAsync("/api/inspections/scan", new { qrToken = InspectionApi.MenRestroom });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("no-such-token")]
    [InlineData("token-checkin-ar01")]
    public async Task Unknown_or_check_in_tokens_are_404(string token)
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);

        var response = await InspectionApi.ScanAsync(supervisor, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Nothing_submitted_in_this_round_means_no_inspect_button()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor);

        Assert.Null(scan.Submission);
        Assert.Equal("ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน", scan.Refusal);
        Assert.Equal("NotYetDone", scan.Point.Status);
        Assert.False(scan.IsCover);
        Assert.True(scan.WithinRadius);
        Assert.NotNull(scan.DistanceM);
        Assert.Empty(await BlockedAsync(factory));
    }

    [Fact]
    public async Task The_current_rounds_submission_is_offered()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(
            factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10), CleaningStatus.Issue, issueTags: "wet_floor", note: "ก๊อกรั่ว");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor);

        Assert.Null(scan.Refusal);
        Assert.Equal(scanId, scan.Submission!.Id);
        Assert.Equal("สมชาย ใจดี", scan.Submission.CleanerName);
        Assert.Equal("Issue", scan.Submission.Status);
        Assert.Equal(new[] { "wet_floor" }, scan.Submission.IssueTags);
        Assert.Equal("ก๊อกรั่ว", scan.Submission.Note);
        Assert.Equal("PendingInspection", scan.Point.Status);
    }

    [Fact]
    public async Task Rework_still_owed_means_no_inspect_button()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, scanId, InspectionResult.Rework, ThaiClock.At(8, 8, 20));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor);

        Assert.Null(scan.Submission);
        Assert.Equal("รอแม่บ้านส่งงานแก้ ยังไม่มีงานใหม่ให้ตรวจ", scan.Refusal);
        Assert.Equal("Rework", scan.Point.Status);
    }

    [Fact]
    public async Task An_earlier_rounds_work_is_not_offered()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 16, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor);

        Assert.Null(scan.Submission);
        Assert.Equal("ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน", scan.Refusal);
    }

    [Fact]
    public async Task A_point_in_another_building_is_a_blocked_scan()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BlockedModel>())!;
        Assert.Equal("จุดนี้ไม่ได้อยู่ในตึกของคุณ", body.Message);
        Assert.Equal("OtherBuilding", body.Reason);
        Assert.Equal("B", body.BuildingCode);
        Assert.Equal("BR01", body.AreaCode);
        var blocked = Assert.Single(await BlockedAsync(factory));
        Assert.Equal(BlockReason.OtherBuilding, blocked.Reason);
        Assert.Equal(ThaiClock.At(8, 8, 30).UtcDateTime, blocked.ScannedAt);
        Assert.True(blocked.WithinRadius); // the GPS is kept like on every scan (facility-0037)
    }

    [Fact]
    public async Task The_own_building_outside_the_own_shift_is_a_blocked_scan()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));

        var response = await InspectionApi.ScanAsync(supervisor);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("OutsideShift", (await response.Content.ReadFromJsonAsync<BlockedModel>())!.Reason);
        Assert.Equal(BlockReason.OutsideShift, Assert.Single(await BlockedAsync(factory)).Reason);
    }

    [Fact]
    public async Task A_cover_lets_the_supervisor_inspect_another_building()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorCoverAsync(factory, "S2001", "B", Oct8, Shift.Day);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor, InspectionApi.BuildingBPoint);

        Assert.True(scan.IsCover);
        Assert.Equal("B", scan.Point.BuildingCode);
        Assert.Empty(await BlockedAsync(factory));
    }

    [Fact]
    public async Task A_night_cover_of_the_own_building_lets_the_day_supervisor_inspect_at_night()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorCoverAsync(factory, "S2001", "A", Oct8, Shift.Night);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));

        var scan = await InspectionApi.ScannedAsync(supervisor);

        Assert.True(scan.IsCover);
    }

    [Theory]
    [InlineData(Shift.Night, false)]
    [InlineData(Shift.Day, true)]
    public async Task A_cover_of_another_shift_or_a_cancelled_one_does_not_count(Shift coverShift, bool cancelled)
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorCoverAsync(factory, "S2001", "B", Oct8, coverShift, cancelled);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~InspectionScanEndpointTests"`
Expected: FAIL. Every test gets 404 (the route does not exist) or fails its status assertion.

- [ ] **Step 4: Give Blocked Scans their reason and building**

In `backend/src/FacilityRealtime.Api/DTOs/AttendanceDtos.cs`, replace the `BlockedScanResponse` record with:

```csharp
/// <summary>facility-0041, 0048: tells the phone why the scan was refused and where the scanned sign is.</summary>
public record BlockedScanResponse(string Message, string AreaCode, string AreaName, BlockReason Reason, string BuildingCode);
```

Replace `backend/src/FacilityRealtime.Api/Endpoints/BlockedScans.cs` with:

```csharp
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

internal static class BlockedScans
{
    /// <summary>facility-0041, 0048: nothing is recorded for the scan itself; the Admin sees the Blocked Scan instead.</summary>
    public static async Task<IResult> BlockAsync(
        AppDbContext db, User user, Sign sign, Area signArea, BlockReason reason, GpsReading gps, DateTime nowUtc)
    {
        var blocked = new BlockedScan { UserId = user.Id, SignId = sign.Id, Reason = reason, ScannedAt = nowUtc };
        blocked.StampGps(gps, sign);
        db.BlockedScans.Add(blocked);
        await db.SaveChangesAsync();

        var buildingCode = await db.Buildings.Where(b => b.Id == signArea.BuildingId).Select(b => b.Code).SingleAsync();
        return Results.Json(
            new BlockedScanResponse(ThaiLabels.BlockedMessage(reason), signArea.Code, signArea.Name, reason, buildingCode),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
```

In `AttendanceEndpoints.cs` replace `return await BlockedScans.BlockAsync(db, user, sign, area, gps, now);` with:

```csharp
            return await BlockedScans.BlockAsync(db, user, sign, area, BlockReason.OtherArea, gps, now);
```

In `ScanRecordEndpoints.cs` replace `return await BlockedScans.BlockAsync(db, user, sign, point.Area!, gps, now);` with:

```csharp
                return await BlockedScans.BlockAsync(db, user, sign, point.Area!, BlockReason.OtherArea, gps, now);
```

In `ThaiLabels.cs` add:

```csharp
    /// <summary>What the phone says about a Blocked Scan (facility-0041, 0048).</summary>
    public static string BlockedMessage(BlockReason reason) => reason switch
    {
        BlockReason.OtherBuilding => "จุดนี้ไม่ได้อยู่ในตึกของคุณ",
        BlockReason.OutsideShift => "ตอนนี้ไม่ใช่กะของคุณ ตรวจได้เฉพาะในกะของตัวเอง",
        _ => "ป้ายนี้ไม่ใช่ Area ของคุณ",
    };
```

In `PointDtos.cs` change `private static IReadOnlyList<string> SplitTags(string? tags)` to `public static IReadOnlyList<string> SplitTags(string? tags)`.

- [ ] **Step 5: Write the DTOs and the endpoint**

Create `backend/src/FacilityRealtime.Api/DTOs/InspectionDtos.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>The QR Token read from the point's sign and what the browser's Geolocation API reported (facility-0036, 0037).</summary>
public record InspectionScanRequest(string? QrToken, double? Latitude, double? Longitude, double? AccuracyM);

/// <summary>The submission a Supervisor would judge (wireframe M11 "ส่งงานล่าสุด").</summary>
public record InspectionSubmissionDto(
    long Id, DateTime SubmittedAt, string CleanerName, Placement Placement, CleaningStatus Status, IReadOnlyList<string> IssueTags, string? Note);

/// <summary>
/// Wireframes M11 and M16. Submission.Id is what POST /api/inspections must send as scanRecordId; when Submission is null,
/// Refusal says why there is no inspect button. IsCover: allowed here only by a Cover Assignment. DistanceM and
/// WithinRadius compare the phone with the sign (both null when the sign has no coordinates).
/// </summary>
public record InspectionScanDto(
    PointStatusDto Point, InspectionSubmissionDto? Submission, string? Refusal, bool IsCover, int? DistanceM, bool? WithinRadius);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/InspectionEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0029, 0047 rule 7, 0048: a Supervisor scans a point's sign, sees the current round's submission and records
/// สะอาด or ไม่สะอาด. Another building, or the own building outside the own shift by the clock, is a Blocked Scan unless an
/// Admin gave a cover for it.
/// </summary>
public static class InspectionEndpoints
{
    private const string SupervisorsOnly = "การตรวจงานใช้สำหรับหัวหน้าเท่านั้น";

    public static IEndpointRouteBuilder MapInspectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/inspections/scan", ScanAsync).RequireAuthorization();
        return app;
    }

    /// <summary>A Supervisor at a point's sign who may inspect there now; Cover is set when only a cover allows it.</summary>
    private sealed record Visit(User Supervisor, Sign Sign, ServicePoint Point, ShiftSlot Slot, CoverAssignment? Cover, GpsReading Gps, DateTime Now);

    /// <summary>The Visit, or the answer to stop with: 401, 403 (another role, or a Blocked Scan it has just written), 400 or 404.</summary>
    private static async Task<(Visit? Visit, IResult? Stop)> VisitAsync(
        ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, string? qrToken, double? latitude, double? longitude, double? accuracyM)
    {
        // facility-0017: the inspector is the account in the access token
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return (null, Results.Unauthorized());
        }

        if (user.Role != UserRole.Supervisor)
        {
            return (null, ApiResults.Message(StatusCodes.Status403Forbidden, SupervisorsOnly));
        }

        if (!GpsInput.TryRead(latitude, longitude, accuracyM, out var gps, out var gpsError))
        {
            return (null, ApiResults.Message(StatusCodes.Status400BadRequest, gpsError));
        }

        var sign = await db.Signs
            .Include(s => s.ServicePoint).ThenInclude(p => p!.Area)
            .FirstOrDefaultAsync(s => s.QrToken == qrToken && s.ServicePointId != null);
        if (sign?.ServicePoint is not { IsActive: true, Area.IsActive: true } point)
        {
            return (null, ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว"));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        // facility-0048 amendment: a Supervisor has no attendance, so the shift is the clock's
        var slot = ShiftCalendar.SlotAt(now);
        var buildingId = point.Area!.BuildingId;

        CoverAssignment? cover = null;
        if (SupervisorScope.WithoutCover(user.BuildingId, user.Shift, buildingId, slot.Shift) is { } reason)
        {
            cover = await db.CoverAssignments.FirstOrDefaultAsync(c =>
                c.UserId == user.Id && c.BuildingId == buildingId && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null);
            if (cover is null)
            {
                return (null, await BlockedScans.BlockAsync(db, user, sign, point.Area, reason, gps, now));
            }
        }

        return (new Visit(user, sign, point, slot, cover, gps, now), null);
    }

    /// <summary>The scan itself: nothing is stored unless it is blocked.</summary>
    private static async Task<IResult> ScanAsync(InspectionScanRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var (visit, stop) = await VisitAsync(principal, db, clock, request.QrToken, request.Latitude, request.Longitude, request.AccuracyM);
        if (visit is null)
        {
            return stop!;
        }

        var target = await TargetAsync(db, visit);
        var board = (await PointBoardQuery.LoadAsync(db, visit.Now, visit.Point.Id)).Single();
        var submission = target.Submission is { } fact
            ? await db.ScanRecords.AsNoTracking().Include(s => s.User).SingleAsync(s => s.Id == fact.Id)
            : null;
        var verdict = Geofence.Evaluate(visit.Gps, (double?)visit.Sign.Latitude, (double?)visit.Sign.Longitude, visit.Sign.RadiusM);

        return Results.Ok(new InspectionScanDto(
            PointDtoMapper.ToDto(board),
            submission is null
                ? null
                : new InspectionSubmissionDto(
                    submission.Id,
                    DateTime.SpecifyKind(submission.SubmittedAt, DateTimeKind.Utc),
                    submission.User?.DisplayName ?? string.Empty,
                    submission.Placement,
                    submission.Status,
                    PointDtoMapper.SplitTags(submission.IssueTags),
                    submission.Note),
            RefusalText(target.Refusal),
            visit.Cover is not null,
            verdict.DistanceM,
            verdict.WithinRadius));
    }

    /// <summary>facility-0047 rule 7, in the Supervisor's (the clock's) shift.</summary>
    private static async Task<InspectionTarget> TargetAsync(AppDbContext db, Visit visit)
    {
        var facts = (await RoundFactsQuery.LoadAsync(db, [visit.Point.Id], visit.Slot))[visit.Point.Id];
        return InspectionRules.Find(visit.Point.Area!.HasShift(visit.Slot.Shift), facts.Windows, facts.Submissions, facts.Inspections, visit.Now);
    }

    private static string? RefusalText(InspectionRefusal? refusal) => refusal switch
    {
        InspectionRefusal.NothingSubmitted => "ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน",
        InspectionRefusal.AwaitingRework => "รอแม่บ้านส่งงานแก้ ยังไม่มีงานใหม่ให้ตรวจ",
        _ => null,
    };
}
```

In `Program.cs` add `app.MapInspectionEndpoints();` on the line after `app.MapMyWorkEndpoints();`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~InspectionScanEndpointTests"`
Expected: PASS (16 cases).

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS. The existing Blocked Scan tests still read `AreaCode` and the same message.

- [ ] **Step 7: Update the handoff and commit**

Add under `## Task log (plan 6)`:

```markdown
- 2026-10-14 · Task 3 done: POST /api/inspections/scan (the current round's submission, or why there is nothing to inspect; another building or outside the shift is a Blocked Scan unless covered); Blocked Scan answers carry reason and building. All tests pass. Next: Task 4 (record the inspection).
```

```bash
git add backend/src backend/tests docs/handoff.md
git commit -m "feat(backend): Supervisor scans a point; other building or outside the shift is a Blocked Scan unless covered (facility-0047, 0048)"
```

---

### Task 4: Record the inspection (`POST /api/inspections`) and show the defect on the card

**Files:**
- Modify: `backend/src/FacilityRealtime.Application/Rounds/RoundFacts.cs` (`InspectionFact` + `Defect`)
- Modify: `backend/src/FacilityRealtime.Application/Rounds/PointStatusCalculator.cs` (`PointStatusResult` + `Rework`)
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/RoundFactsQuery.cs`
- Modify: `backend/src/FacilityRealtime.Api/DTOs/PointDtos.cs` (+ `ReworkDto`, `PointStatusDto.Rework`)
- Modify: `backend/src/FacilityRealtime.Api/DTOs/InspectionDtos.cs` (+ request and answer)
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/InspectionEndpoints.cs` (+ `CreateAsync`)
- Test: `backend/tests/FacilityRealtime.UnitTests/PointStatusCalculatorTests.cs` (append)
- Modify: `backend/tests/FacilityRealtime.ApiTests/Points/ServicePointEndpointTests.cs` (`PointModel` + `ReworkModel`)
- Modify: `backend/tests/FacilityRealtime.ApiTests/Inspections/InspectionApi.cs` (+ `InspectAsync`, `InspectedAsync`, `InspectionCreatedModel`)
- Test: `backend/tests/FacilityRealtime.ApiTests/Inspections/InspectionEndpointTests.cs`

**Interfaces:**
- Consumes: Task 3's `Visit`, `VisitAsync`, `TargetAsync` and `RefusalText`, and Task 2's `InspectionRules.MaxDefectLength`.
- Produces:
  - `InspectionFact(long SubmissionId, InspectionResult Result, DateTime InspectedAt, string? Defect = null)`
  - `PointStatusResult(PointStatus Status, RoundWindow? CurrentRound, RoundWindow? NextRound, InspectionFact? Rework = null)`. `Rework` is set only when `Status` is `Rework`.
  - `ReworkDto(string Defect, DateTime RequestedAt)` and `PointStatusDto.Rework` (`ReworkDto?`, the last positional member).
  - `CreateInspectionRequest(string? QrToken, long? ScanRecordId, InspectionResult? Result, string? Defect, double? Latitude, double? Longitude, double? AccuracyM)`
  - `InspectionCreatedResponse(long InspectionId, int ServicePointId, long ScanRecordId, InspectionResult Result, PointStatus NewPointStatus, int? DistanceM, bool? WithinRadius, DateTime InspectedAt)`
  - Hub event `InspectionRecorded` (payload `PointStatusDto`) goes to the Admins group. Task 6 widens it to the building's Supervisors.

- [ ] **Step 1: Write the failing unit tests**

Append inside `PointStatusCalculatorTests` in `backend/tests/FacilityRealtime.UnitTests/PointStatusCalculatorTests.cs`:

```csharp
    [Fact]
    public void Rework_carries_the_inspection_that_asked_for_it()
    {
        var result = Status(
            Thai(9, 0),
            [Sent(1, 1, 8, 10)],
            [new InspectionFact(1, InspectionResult.Rework, Thai(8, 20), "พื้นยังเปียก")]);

        Assert.Equal(PointStatus.Rework, result.Status);
        Assert.Equal("พื้นยังเปียก", result.Rework!.Defect);
        Assert.Equal(Thai(8, 20), result.Rework.InspectedAt);
    }

    [Fact]
    public void Only_a_card_in_rework_carries_one()
    {
        var result = Status(
            Thai(9, 0),
            [Sent(1, 1, 8, 10), Sent(2, 1, 8, 40)],
            [new InspectionFact(1, InspectionResult.Rework, Thai(8, 20), "พื้นยังเปียก")]);

        Assert.Equal(PointStatus.PendingInspection, result.Status);
        Assert.Null(result.Rework);
    }
```

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~PointStatusCalculatorTests"`
Expected: build FAILS: `InspectionFact` has no constructor taking 4 arguments.

- [ ] **Step 2: Carry the defect through the round facts**

In `backend/src/FacilityRealtime.Application/Rounds/RoundFacts.cs` replace the `InspectionFact` record with:

```csharp
/// <summary>An Inspection Record of one of those Scan Records. Defect is shown while the card is ต้องแก้ไข (facility-0029).</summary>
public sealed record InspectionFact(long SubmissionId, InspectionResult Result, DateTime InspectedAt, string? Defect = null);
```

In `PointStatusCalculator.cs` replace the `PointStatusResult` record with:

```csharp
/// <summary>Rework is the inspection that asked for it, set only when Status is Rework.</summary>
public sealed record PointStatusResult(PointStatus Status, RoundWindow? CurrentRound, RoundWindow? NextRound, InspectionFact? Rework = null);
```

and in `Calculate` replace everything from `var latest = RoundRules.LatestInspection(roundSubmissions, inspections);` to the end of the method with:

```csharp
        var latest = RoundRules.LatestInspection(roundSubmissions, inspections);
        var needsRework = RoundRules.NeedsRework(roundSubmissions, latest);

        var status =
            needsRework ? PointStatus.Rework
            : roundSubmissions.Count == 0 && nowUtc > current.EndUtc ? PointStatus.Overdue
            : roundSubmissions.Any(s => latest is null || s.SubmittedAt > latest.InspectedAt) ? PointStatus.PendingInspection
            : latest is { Result: InspectionResult.Passed } ? PointStatus.Passed
            : PointStatus.NotYetDone;

        return new PointStatusResult(status, current, next, needsRework ? latest : null);
    }
```

In `backend/src/FacilityRealtime.Infrastructure/Persistence/RoundFactsQuery.cs`, change the inspections projection to `.Select(i => new { i.ScanRecordId, i.ServicePointId, i.Result, i.InspectedAt, i.Defect })`, and the fact to `.Select(i => new InspectionFact(i.ScanRecordId, i.Result, i.InspectedAt, i.Defect))`.

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests`
Expected: PASS.

- [ ] **Step 3: Put the defect on the card**

In `backend/src/FacilityRealtime.Api/DTOs/PointDtos.cs`:

Add after `IssueDto`:

```csharp
/// <summary>facility-0029: what the Supervisor asked to fix, shown while the card is ต้องแก้ไข.</summary>
public record ReworkDto(string Defect, DateTime RequestedAt);
```

Add `ReworkDto? Rework` as the last member of `PointStatusDto` (after `IssueDto? Issue`), and add the matching last argument in `PointDtoMapper.ToDto`:

```csharp
            last is { Status: CleaningStatus.Issue } ? new IssueDto(SplitTags(last.IssueTags), last.Note, Utc(last.SubmittedAt)) : null,
            row.Status.Rework is { } rework ? new ReworkDto(rework.Defect ?? string.Empty, Utc(rework.InspectedAt)) : null);
```

In `backend/tests/FacilityRealtime.ApiTests/Points/ServicePointEndpointTests.cs` add:

```csharp
public record ReworkModel(string Defect, DateTime RequestedAt);
```

and add `ReworkModel? Rework` as the last member of `PointModel`. If any test constructs `PointModel` positionally (`grep -rn "new PointModel(" backend/tests`), give it `null` for the new member.

- [ ] **Step 4: Write the failing API tests**

Append to `backend/tests/FacilityRealtime.ApiTests/Inspections/InspectionApi.cs`, after `BlockedModel`:

```csharp
public record InspectionCreatedModel(
    long InspectionId, int ServicePointId, long ScanRecordId, string Result, string NewPointStatus, int? DistanceM, bool? WithinRadius, DateTime InspectedAt);
```

and inside the `InspectionApi` class:

```csharp
    public static Task<HttpResponseMessage> InspectAsync(
        HttpClient client,
        long? scanRecordId,
        string result,
        string? defect = null,
        string qrToken = MenRestroom,
        double latitude = TestGps.NearLatitude,
        double longitude = TestGps.NearLongitude,
        double accuracyM = 10) =>
        client.PostAsJsonAsync("/api/inspections", new { qrToken, scanRecordId, result, defect, latitude, longitude, accuracyM });

    /// <summary>Records a verdict and asserts 201.</summary>
    public static async Task<InspectionCreatedModel> InspectedAsync(
        HttpClient client, long scanRecordId, string result, string? defect = null, string qrToken = MenRestroom)
    {
        var response = await InspectAsync(client, scanRecordId, result, defect, qrToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InspectionCreatedModel>())!;
    }
```

Create `backend/tests/FacilityRealtime.ApiTests/Inspections/InspectionEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Points;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Inspections;

/// <summary>facility-0029, 0037, 0047: the Supervisor records สะอาด or ไม่สะอาด on the current round's latest submission.</summary>
public class InspectionEndpointTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    private static async Task<InspectionRecord> StoredAsync(FacilityApiFactory factory, long id)
    {
        InspectionRecord? record = null;
        await factory.WithDbAsync(async db => record = await db.InspectionRecords.Include(i => i.Supervisor).SingleAsync(i => i.Id == id));
        return record!;
    }

    private static async Task<int> CountAsync(FacilityApiFactory factory)
    {
        var count = 0;
        await factory.WithDbAsync(async db => count = await db.InspectionRecords.CountAsync());
        return count;
    }

    [Fact]
    public async Task Passed_is_saved_with_its_gps_and_the_dashboard_shows_it()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var created = await InspectionApi.InspectedAsync(supervisor, scanId, "Passed");

        Assert.Equal("Passed", created.Result);
        Assert.Equal("Passed", created.NewPointStatus);
        Assert.Equal(scanId, created.ScanRecordId);
        Assert.True(created.WithinRadius);
        var stored = await StoredAsync(factory, created.InspectionId);
        Assert.Equal("S2001", stored.Supervisor!.EmployeeId);
        Assert.Null(stored.Defect);
        Assert.Null(stored.CoverAssignmentId);
        Assert.Equal(13.756300m, stored.Latitude);
        Assert.Equal(ThaiClock.At(8, 8, 30).UtcDateTime, stored.InspectedAt);
        var board = (await admin.GetFromJsonAsync<List<PointModel>>("/api/service-points"))!;
        Assert.Equal("Passed", board.Single(p => p.Name == "ห้องน้ำชาย ชั้น 1").Status);
    }

    [Fact]
    public async Task Rework_keeps_the_defect_and_the_cleaner_sees_it_on_my_work()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var created = await InspectionApi.InspectedAsync(supervisor, scanId, "Rework", "  พื้นใต้อ่างล้างมือยังเปียก  ");

        Assert.Equal("Rework", created.NewPointStatus);
        Assert.Equal("พื้นใต้อ่างล้างมือยังเปียก", (await StoredAsync(factory, created.InspectionId)).Defect);
        var work = (await cleaner.GetFromJsonAsync<MyWorkModel>("/api/my-work"))!;
        var point = work.Areas.Single().Points.Single(p => p.Name == "ห้องน้ำชาย ชั้น 1");
        Assert.Equal("Rework", point.Status);
        Assert.Equal("พื้นใต้อ่างล้างมือยังเปียก", point.Rework!.Defect);
        Assert.Equal(ThaiClock.At(8, 8, 30).UtcDateTime, point.Rework.RequestedAt.ToUniversalTime());
    }

    [Theory]
    [InlineData("Rework", null)]
    [InlineData("Rework", "   ")]
    [InlineData("Passed", "ไม่ควรมีข้อบกพร่อง")]
    public async Task Defect_is_required_for_rework_and_left_out_for_passed(string result, string? defect)
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, result, defect);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task A_defect_over_1000_characters_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, "Rework", new string('ก', 1001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Result_and_submission_are_required()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var gps = new { latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10 };

        var noResult = await supervisor.PostAsJsonAsync(
            "/api/inspections", new { qrToken = InspectionApi.MenRestroom, scanRecordId = scanId, gps.latitude, gps.longitude, gps.accuracyM });
        var noSubmission = await InspectionApi.InspectAsync(supervisor, null, "Passed");

        Assert.Equal(HttpStatusCode.BadRequest, noResult.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noSubmission.StatusCode);
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task A_submission_the_supervisor_did_not_see_is_409()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var first = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 20));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, first, "Passed");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task Rework_still_owed_is_409()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, scanId, InspectionResult.Rework, ThaiClock.At(8, 8, 20));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, "Passed");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await CountAsync(factory));
    }

    [Fact]
    public async Task Nothing_submitted_is_409()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, 999, "Passed");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Rework_resubmission_and_second_inspection_go_round_the_cycle()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var submit = new { qrToken = InspectionApi.MenRestroom, status = "Normal", latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10 };
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));
        var first = (await (await cleaner.PostAsJsonAsync("/api/scan-records", submit)).Content.ReadFromJsonAsync<ScanCreatedModel>())!;
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 20));
        await InspectionApi.InspectedAsync(supervisor, first.ScanRecordId, "Rework", "พื้นยังเปียก");

        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 40));
        var fixedResponse = await cleaner.PostAsJsonAsync("/api/scan-records", submit);
        var fixedWork = (await fixedResponse.Content.ReadFromJsonAsync<ScanCreatedModel>())!;
        Assert.Equal("Rework", fixedWork.Placement);
        Assert.Equal("PendingInspection", fixedWork.NewPointStatus);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 50));
        Assert.Equal(fixedWork.ScanRecordId, (await InspectionApi.ScannedAsync(supervisor)).Submission!.Id);
        var passed = await InspectionApi.InspectedAsync(supervisor, fixedWork.ScanRecordId, "Passed");
        Assert.Equal("Passed", passed.NewPointStatus);
    }

    [Fact]
    public async Task Far_from_the_sign_is_recorded_and_flagged()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, "Passed", latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<InspectionCreatedModel>())!;
        Assert.False(created.WithinRadius);
        Assert.True(created.DistanceM > 1000);
    }

    [Fact]
    public async Task An_inspection_under_a_cover_is_marked()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var coverId = await TestData.AddSupervisorCoverAsync(factory, "S2001", "B", Oct8, Shift.Day);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.BuildingBPoint, ThaiClock.At(8, 8, 30));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 40));

        var created = await InspectionApi.InspectedAsync(supervisor, scanId, "Passed", qrToken: InspectionApi.BuildingBPoint);

        Assert.Equal(coverId, (await StoredAsync(factory, created.InspectionId)).CoverAssignmentId);
    }

    [Fact]
    public async Task A_blocked_inspection_saves_only_the_blocked_scan()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, "Passed");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CountAsync(factory));
        var blocked = new List<BlockedScan>();
        await factory.WithDbAsync(async db => blocked = await db.BlockedScans.ToListAsync());
        Assert.Equal(BlockReason.OutsideShift, Assert.Single(blocked).Reason);
    }

    [Fact]
    public async Task Admins_get_the_inspection_live()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (_, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await using var adminHub = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        var adminGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        adminHub.On<JsonElement>("InspectionRecorded", point => adminGot.TrySetResult(point));
        await Task.Delay(200); // OnConnectedAsync joins the admin group just after the handshake
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        await InspectionApi.InspectedAsync(supervisor, scanId, "Passed");

        var payload = await adminGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Passed", payload.GetProperty("status").GetString());
        Assert.False(payload.TryGetProperty("qrToken", out _)); // facility-0060
    }
}
```

`MyWorkModel`, `PointModel` and `ScanCreatedModel` are the public records in the `FacilityRealtime.ApiTests.Points` namespace, and `AttendanceApi` is in `ApiTests.Infrastructure`.

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~InspectionEndpointTests"`
Expected: FAIL. `POST /api/inspections` answers 404 or 405.

- [ ] **Step 5: Write the request, the answer and the endpoint**

Append to `backend/src/FacilityRealtime.Api/DTOs/InspectionDtos.cs` (add `using FacilityRealtime.Application.Rounds;` at the top):

```csharp
/// <summary>
/// facility-0029. ScanRecordId is the submission the scan showed (InspectionScanDto.Submission.Id). Result: Passed = สะอาด,
/// Rework = ไม่สะอาด. Defect is required with Rework and must be left out with Passed.
/// </summary>
public record CreateInspectionRequest(
    string? QrToken, long? ScanRecordId, InspectionResult? Result, string? Defect, double? Latitude, double? Longitude, double? AccuracyM);

/// <summary>WithinRadius false = Geofence Flag (the INSPECTION_GPS alert); null = the sign has no coordinates yet.</summary>
public record InspectionCreatedResponse(
    long InspectionId,
    int ServicePointId,
    long ScanRecordId,
    InspectionResult Result,
    PointStatus NewPointStatus,
    int? DistanceM,
    bool? WithinRadius,
    DateTime InspectedAt);
```

In `InspectionEndpoints.cs`:

Add these `using` lines:

```csharp
using FacilityRealtime.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
```

Map the route in `MapInspectionEndpoints`, after the scan route:

```csharp
        app.MapPost("/api/inspections", CreateAsync).RequireAuthorization();
```

Add the handler:

```csharp
    /// <summary>The verdict. Every check of the scan runs again, so a direct call is blocked the same way.</summary>
    private static async Task<IResult> CreateAsync(
        CreateInspectionRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, IHubContext<ScanHub> hub)
    {
        var (visit, stop) = await VisitAsync(principal, db, clock, request.QrToken, request.Latitude, request.Longitude, request.AccuracyM);
        if (visit is null)
        {
            return stop!;
        }

        if (request.Result is not { } result || !Enum.IsDefined(result))
        {
            return Bad("ต้องเลือกผลการตรวจ: สะอาด หรือ ไม่สะอาด");
        }

        var defect = string.IsNullOrWhiteSpace(request.Defect) ? null : request.Defect.Trim();
        if (result == InspectionResult.Rework && defect is null)
        {
            return Bad("เลือกไม่สะอาดต้องระบุข้อบกพร่อง");
        }

        if (result == InspectionResult.Passed && defect is not null)
        {
            return Bad("ข้อบกพร่องใส่ได้เฉพาะเมื่อเลือกไม่สะอาด");
        }

        if (defect is { Length: > InspectionRules.MaxDefectLength })
        {
            return Bad($"ข้อบกพร่องยาวได้ไม่เกิน {InspectionRules.MaxDefectLength} ตัวอักษร");
        }

        if (request.ScanRecordId is not { } scanRecordId)
        {
            return Bad("ต้องระบุงานที่ตรวจ (scanRecordId จากการสแกน)");
        }

        var target = await TargetAsync(db, visit);
        if (target.Refusal is { } refusal)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, RefusalText(refusal)!);
        }

        // Decision 2: never judge work the Supervisor was not shown
        if (target.Submission!.Id != scanRecordId)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "มีการส่งงานใหม่หลังจากที่คุณสแกน ให้สแกนป้ายอีกครั้ง");
        }

        var inspection = new InspectionRecord
        {
            ScanRecordId = scanRecordId,
            ServicePointId = visit.Point.Id,
            SupervisorId = visit.Supervisor.Id,
            Result = result,
            Defect = defect,
            CoverAssignmentId = visit.Cover?.Id,
            InspectedAt = visit.Now,
        };
        inspection.StampGps(visit.Gps, visit.Sign);
        db.InspectionRecords.Add(inspection);
        await db.SaveChangesAsync();

        // The Supervisor's shift is the clock's, which is also the Dashboard's
        var board = (await PointBoardQuery.LoadAsync(db, visit.Now, visit.Point.Id)).Single();
        await hub.Clients.Group(ScanHub.AdminGroup).SendAsync("InspectionRecorded", PointDtoMapper.ToDto(board));

        return Results.Created(
            $"/api/inspections/{inspection.Id}",
            new InspectionCreatedResponse(
                inspection.Id, visit.Point.Id, scanRecordId, result, board.Status.Status,
                inspection.DistanceM, inspection.WithinRadius, DateTime.SpecifyKind(visit.Now, DateTimeKind.Utc)));
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~InspectionEndpointTests|FullyQualifiedName~InspectionScanEndpointTests"`
Expected: PASS.

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS. A card that is not in rework sends no `rework` field (null is not written), so the existing card tests do not change.

- [ ] **Step 7: Update the handoff and commit**

Add under `## Task log (plan 6)`:

```markdown
- 2026-10-14 · Task 4 done: POST /api/inspections (สะอาด / ไม่สะอาด with defect, GPS and cover stamped, 409 when the scanned submission is stale or a rework is owed); cards in ต้องแก้ไข show the defect; Admins get InspectionRecorded live. All tests pass. Next: Task 5 (Supervisor covers).
```

```bash
git add backend/src backend/tests docs/handoff.md
git commit -m "feat(backend): Supervisor records สะอาด or ไม่สะอาด; the card shows the defect to fix (facility-0029, 0037)"
```

---
### Task 5: Supervisor covers by building

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/DTOs/CoverDtos.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/CoverAssignmentEndpoints.cs` (`AssignAsync`, `CancelAsync`; `ListCandidatesAsync` is unchanged)
- Modify: `backend/tests/FacilityRealtime.ApiTests/Admin/CoverAssignmentEndpointTests.cs` (`CoverModel`, one assert)
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/SupervisorCoverTests.cs`

**Interfaces:**
- Consumes:
  - Task 1's nullable `CoverAssignment.AreaId` and new `BuildingId` / `Building`.
  - Task 2's `SupervisorScope.WithoutCover`.
  - Task 3's `InspectionApi` and `TestData.AddSupervisorCoverAsync`.
  - Existing: `AdminAlertEndpoints.AddReview` and `AuditTrail.Add`.
- Produces:
  - `AssignCoverRequest(int UserId, int? AreaId, DateOnly ShiftDate, Shift? Shift, long? BlockedScanId = null, int? BuildingId = null)`
  - `CoverAssignmentDto(int Id, int UserId, string PersonName, int? AreaId, string? AreaCode, int? BuildingId, string? BuildingCode, DateOnly ShiftDate, Shift Shift, DateTime AssignedAt)`
  - `POST /api/admin/cover-assignments` accepts a Supervisor with `buildingId`. `DELETE /api/admin/cover-assignments/{id}` cancels either kind.

- [ ] **Step 1: Write the failing tests**

In `backend/tests/FacilityRealtime.ApiTests/Admin/CoverAssignmentEndpointTests.cs`, replace the `CoverModel` record with:

```csharp
public record CoverModel(
    int Id, int UserId, string PersonName, int? AreaId, string? AreaCode, int? BuildingId, string? BuildingCode, DateOnly ShiftDate, string Shift, DateTime AssignedAt);
```

and in `Admin_assigns_a_cover_and_it_is_logged` replace `cover.CleanerName` with `cover.PersonName`.

Create `backend/tests/FacilityRealtime.ApiTests/Admin/SupervisorCoverTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Inspections;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// facility-0048 rule 2 and its amendment: the Admin lets a Supervisor inspect another building, or their own building
/// outside their shift, for one shift. Seeded S2001 is the Day Supervisor of building A.
/// </summary>
public class SupervisorCoverTests
{
    private const string Path = "/api/admin/cover-assignments";
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    private static async Task<HttpResponseMessage> AssignAsync(
        FacilityApiFactory factory, HttpClient admin, string buildingCode, string shift, long? blockedScanId = null) =>
        await admin.PostAsJsonAsync(Path, new
        {
            userId = await AdminSetupApi.UserIdAsync(factory, "S2001"),
            buildingId = await AdminSetupApi.BuildingIdAsync(factory, buildingCode),
            shiftDate = Oct8,
            shift,
            blockedScanId,
        });

    [Fact]
    public async Task Admin_lets_a_supervisor_inspect_another_building_and_it_is_logged()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AssignAsync(factory, admin, "B", "Day");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cover = (await response.Content.ReadFromJsonAsync<CoverModel>())!;
        Assert.Equal("สมปอง ตรวจดี", cover.PersonName);
        Assert.Equal("B", cover.BuildingCode);
        Assert.Null(cover.AreaId);
        Assert.Null(cover.AreaCode);
        var audit = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("COVER_ASSIGN", audit.Action);
        Assert.Equal("มอบหมาย สมปอง ตรวจดี ตรวจแทนตึก B กะเช้า 2026-10-08", audit.Summary);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        Assert.True((await InspectionApi.ScannedAsync(supervisor, InspectionApi.BuildingBPoint)).IsCover);
    }

    [Fact]
    public async Task The_own_building_in_the_own_shift_needs_no_cover()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AssignAsync(factory, admin, "A", "Day");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_own_building_in_the_other_shift_can_be_covered()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AssignAsync(factory, admin, "A", "Night");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_supervisor_cover_names_a_building_and_no_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var userId = await AdminSetupApi.UserIdAsync(factory, "S2001");

        var noBuilding = await admin.PostAsJsonAsync(Path, new { userId, shiftDate = Oct8, shift = "Night" });
        var withArea = await admin.PostAsJsonAsync(Path, new
        {
            userId,
            areaId = await AdminSetupApi.AreaIdAsync(factory, "AR01"),
            buildingId = await AdminSetupApi.BuildingIdAsync(factory),
            shiftDate = Oct8,
            shift = "Night",
        });

        Assert.Equal(HttpStatusCode.BadRequest, noBuilding.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withArea.StatusCode);
    }

    [Fact]
    public async Task A_cleaner_cover_names_an_area_and_no_building()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var userId = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var withBuilding = await admin.PostAsJsonAsync(Path, new
        {
            userId,
            areaId = await AdminSetupApi.AreaIdAsync(factory, "AR02"),
            buildingId = await AdminSetupApi.BuildingIdAsync(factory),
            shiftDate = Oct8,
            shift = "Day",
        });
        var noArea = await admin.PostAsJsonAsync(Path, new { userId, shiftDate = Oct8, shift = "Day" });

        Assert.Equal(HttpStatusCode.BadRequest, withBuilding.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noArea.StatusCode);
    }

    [Fact]
    public async Task The_same_cover_twice_is_409()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        Assert.Equal(HttpStatusCode.Created, (await AssignAsync(factory, admin, "B", "Day")).StatusCode);

        var again = await AssignAsync(factory, admin, "B", "Day");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_shift_that_has_ended_is_refused()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 19, 0));

        var response = await AssignAsync(factory, admin, "B", "Day");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_ends_the_cover_and_is_logged()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var cover = (await (await AssignAsync(factory, admin, "B", "Day")).Content.ReadFromJsonAsync<CoverModel>())!;

        var response = await admin.DeleteAsync($"{Path}/{cover.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("ยกเลิกการตรวจแทนของ สมปอง ตรวจดี ที่ตึก B กะเช้า 2026-10-08", (await AdminSetupApi.AuditAsync(factory))[1].Summary);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        Assert.Equal(HttpStatusCode.Forbidden, (await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint)).StatusCode);
    }

    [Fact]
    public async Task Assigning_from_the_supervisors_blocked_scan_closes_its_alert()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        Assert.Equal(HttpStatusCode.Forbidden, (await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint)).StatusCode);
        var blockedId = await AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.Select(b => b.Id).SingleAsync());

        var response = await AssignAsync(factory, admin, "B", "Day", blockedId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var audit = await AdminSetupApi.AuditAsync(factory);
        Assert.Equal(new[] { "COVER_ASSIGN", "ALERT_REVIEW" }, audit.Select(a => a.Action));
        Assert.Equal("ปิดเตือนสแกนนอก Area ของ สมปอง ตรวจดี เวลา 08:30: มอบหมายตรวจแทนตึก B แล้ว", audit[1].Summary);
        Assert.Equal(HttpStatusCode.OK, (await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint)).StatusCode);
    }

    [Theory]
    [InlineData("A", "Night")]
    [InlineData("B", "Night")]
    public async Task A_blocked_scan_of_another_building_or_shift_does_not_fit(string buildingCode, string shift)
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint); // blocked at B in the Day shift
        var blockedId = await AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.Select(b => b.Id).SingleAsync());

        var response = await AssignAsync(factory, admin, buildingCode, shift, blockedId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.CoverAssignments.ToListAsync()));
    }

    [Fact]
    public async Task A_building_cover_stays_off_the_attendance_board()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        Assert.Equal(HttpStatusCode.Created, (await AssignAsync(factory, admin, "B", "Day")).StatusCode);

        var board = await AdminAttendanceApi.BoardAsync(admin, "?shiftDate=2026-10-08&shift=Day");

        Assert.DoesNotContain(board.Rows, r => r.State == "Covered");
        Assert.All(board.Rows, r => Assert.Empty(r.Covers));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~SupervisorCoverTests|FullyQualifiedName~CoverAssignmentEndpointTests"`
Expected: FAIL. Supervisor covers answer 400 ("ต้องเลือกแม่บ้านที่ใช้งานอยู่"), and `PersonName` is null in the Cleaner test.

- [ ] **Step 3: Change the DTOs**

In `backend/src/FacilityRealtime.Api/DTOs/CoverDtos.cs`, replace `AssignCoverRequest` and `CoverAssignmentDto` with:

```csharp
/// <summary>
/// A Cleaner's cover names AreaId (facility-0041); a Supervisor's names BuildingId (facility-0048). Leave the other out.
/// BlockedScanId: assigning from that Blocked Scan on the alert list (wireframe D3) also closes its alert.
/// </summary>
public record AssignCoverRequest(int UserId, int? AreaId, DateOnly ShiftDate, Shift? Shift, long? BlockedScanId = null, int? BuildingId = null);

/// <summary>Area fields are set for a Cleaner's cover, building fields for a Supervisor's.</summary>
public record CoverAssignmentDto(
    int Id,
    int UserId,
    string PersonName,
    int? AreaId,
    string? AreaCode,
    int? BuildingId,
    string? BuildingCode,
    DateOnly ShiftDate,
    Shift Shift,
    DateTime AssignedAt);
```

- [ ] **Step 4: Assign and cancel both kinds**

In `backend/src/FacilityRealtime.Api/Endpoints/CoverAssignmentEndpoints.cs`:

Add `using FacilityRealtime.Application.Inspections;`. Change the class summary to:

```csharp
/// <summary>
/// facility-0041: the Admin lets a Cleaner work another Area for one shift; facility-0048: a Supervisor inspect another
/// building, or their own outside their shift, for one shift. Every change is logged (facility-0051).
/// </summary>
```

Replace the whole `AssignAsync` method with these methods:

```csharp
    private static async Task<IResult> AssignAsync(AssignCoverRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.Shift is not { } shift || !Enum.IsDefined(shift))
        {
            return Bad("ต้องระบุกะ");
        }

        var person = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive && u.Role != UserRole.Admin);
        if (person is null)
        {
            return Bad("ต้องเลือกแม่บ้านหรือหัวหน้าที่ใช้งานอยู่");
        }

        var slot = new ShiftSlot(request.ShiftDate, shift);
        var now = clock.GetUtcNow().UtcDateTime;
        return person.Role == UserRole.Supervisor
            ? await AssignBuildingAsync(request, db, admin, person, slot, now)
            : await AssignAreaAsync(request, db, admin, person, slot, now, settings);
    }

    /// <summary>facility-0041: a Cleaner works another Area, only in their own regular shift (owner decision 2026-10-09).</summary>
    private static async Task<IResult> AssignAreaAsync(
        AssignCoverRequest request, AppDbContext db, User admin, User cleaner, ShiftSlot slot, DateTime now, AttendanceSettings settings)
    {
        if (request.BuildingId is not null)
        {
            return Bad("แม่บ้านทำแทนเป็น Area ไม่ต้องระบุตึก");
        }

        var area = request.AreaId is int areaId ? await db.Areas.FirstOrDefaultAsync(a => a.Id == areaId && a.IsActive) : null;
        if (area is null)
        {
            return Bad("ไม่พบ Area นี้ หรือปิดใช้งานแล้ว");
        }

        if (area.Id == cleaner.AreaId)
        {
            return Bad("Area นี้เป็น Area ประจำของแม่บ้านคนนี้อยู่แล้ว");
        }

        if (slot.Shift != cleaner.Shift)
        {
            return Bad("ทำแทนได้เฉพาะกะประจำของแม่บ้านคนนี้");
        }

        if (!area.HasShift(slot.Shift))
        {
            return Bad("Area นี้ไม่มีกะนี้");
        }

        if (ShiftCalendar.EndUtc(slot) <= now)
        {
            return Bad("กะนี้จบไปแล้ว");
        }

        if (await IsDuplicateAsync(db, cleaner.Id, area.Id, null, slot))
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "มอบหมายไว้แล้ว");
        }

        // Assigning from the alert list (wireframe D3): the Blocked Scan must be this Cleaner's, at a sign of this Area
        BlockedScan? blocked = null;
        if (request.BlockedScanId is long blockedScanId)
        {
            blocked = await db.BlockedScans.FirstOrDefaultAsync(b => b.Id == blockedScanId);
            if (blocked is null)
            {
                return Bad("ไม่พบการสแกนที่ถูกบล็อกนี้");
            }

            var signId = blocked.SignId;
            var signAreaId = await db.Signs.Where(s => s.Id == signId).Select(s => s.AreaId).SingleAsync();
            if (blocked.UserId != cleaner.Id || signAreaId != area.Id)
            {
                return Bad("การสแกนที่ถูกบล็อกนี้ไม่ใช่ของแม่บ้านคนนี้ที่ Area นี้");
            }

            // A scan from another shift is no reason to cover this one
            if (AttendanceCalendar.SlotFor(slot.Shift, blocked.ScannedAt, settings) != slot)
            {
                return Bad("การสแกนที่ถูกบล็อกนี้ไม่ได้อยู่ในกะนี้");
            }
        }

        var cover = new CoverAssignment
        {
            UserId = cleaner.Id,
            AreaId = area.Id,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            AssignedById = admin.Id,
            AssignedAt = now,
        };
        return await SaveAsync(
            db, admin, cleaner, cover, blocked, now,
            $"มอบหมาย {cleaner.DisplayName} ทำแทน {area.Code} กะ{ThaiLabels.ShiftName(slot.Shift)} {ThaiLabels.Date(slot.ShiftDate)}",
            $"มอบหมายทำแทน {area.Code} แล้ว",
            area,
            null);
    }

    /// <summary>
    /// facility-0048 rule 2 and its amendment: a Supervisor inspects another building, or their own outside their shift.
    /// Any shift may be covered except the one they already hold (decision 7). The shift is the clock's.
    /// </summary>
    private static async Task<IResult> AssignBuildingAsync(
        AssignCoverRequest request, AppDbContext db, User admin, User supervisor, ShiftSlot slot, DateTime now)
    {
        if (request.AreaId is not null)
        {
            return Bad("หัวหน้าตรวจแทนเป็นตึก ไม่ต้องระบุ Area");
        }

        var building = request.BuildingId is int buildingId ? await db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId && b.IsActive) : null;
        if (building is null)
        {
            return Bad("ไม่พบตึกนี้ หรือปิดใช้งานแล้ว");
        }

        if (SupervisorScope.WithoutCover(supervisor.BuildingId, supervisor.Shift, building.Id, slot.Shift) is null)
        {
            return Bad("ตึกและกะนี้เป็นของหัวหน้าคนนี้อยู่แล้ว");
        }

        if (ShiftCalendar.EndUtc(slot) <= now)
        {
            return Bad("กะนี้จบไปแล้ว");
        }

        if (await IsDuplicateAsync(db, supervisor.Id, null, building.Id, slot))
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "มอบหมายไว้แล้ว");
        }

        BlockedScan? blocked = null;
        if (request.BlockedScanId is long blockedScanId)
        {
            blocked = await db.BlockedScans.FirstOrDefaultAsync(b => b.Id == blockedScanId);
            if (blocked is null)
            {
                return Bad("ไม่พบการสแกนที่ถูกบล็อกนี้");
            }

            var signId = blocked.SignId;
            var signBuildingId = await db.Signs.Where(s => s.Id == signId).Select(s => s.Area!.BuildingId).SingleAsync();
            if (blocked.UserId != supervisor.Id || signBuildingId != building.Id)
            {
                return Bad("การสแกนที่ถูกบล็อกนี้ไม่ใช่ของหัวหน้าคนนี้ที่ตึกนี้");
            }

            if (ShiftCalendar.SlotAt(blocked.ScannedAt) != slot)
            {
                return Bad("การสแกนที่ถูกบล็อกนี้ไม่ได้อยู่ในกะนี้");
            }
        }

        var cover = new CoverAssignment
        {
            UserId = supervisor.Id,
            BuildingId = building.Id,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            AssignedById = admin.Id,
            AssignedAt = now,
        };
        return await SaveAsync(
            db, admin, supervisor, cover, blocked, now,
            $"มอบหมาย {supervisor.DisplayName} ตรวจแทนตึก {building.Code} กะ{ThaiLabels.ShiftName(slot.Shift)} {ThaiLabels.Date(slot.ShiftDate)}",
            $"มอบหมายตรวจแทนตึก {building.Code} แล้ว",
            null,
            building);
    }

    /// <summary>An active cover of the same person, place and shift. AreaId and BuildingId compare as SQL IS NULL when null.</summary>
    private static Task<bool> IsDuplicateAsync(AppDbContext db, int userId, int? areaId, int? buildingId, ShiftSlot slot) =>
        db.CoverAssignments.AnyAsync(c =>
            c.UserId == userId && c.AreaId == areaId && c.BuildingId == buildingId
            && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null);

    /// <summary>Saves the cover with its audit row and, from a Blocked Scan, closes that alert unless an Admin already did (facility-0041 rule 4).</summary>
    private static async Task<IResult> SaveAsync(
        AppDbContext db, User admin, User person, CoverAssignment cover, BlockedScan? blocked, DateTime now,
        string summary, string alertOutcome, Area? area, Building? building)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.CoverAssignments.Add(cover);
        await db.SaveChangesAsync();
        AuditTrail.Add(
            db, admin.Id, now, "COVER_ASSIGN", "cover_assignments", cover.Id, summary,
            before: null,
            after: new
            {
                cover.UserId,
                cover.AreaId,
                cover.BuildingId,
                ShiftDate = ThaiLabels.Date(cover.ShiftDate),
                Shift = cover.Shift.ToString(),
                BlockedScanId = blocked?.Id,
            });

        if (blocked is { } scan && !await db.AlertReviews.AnyAsync(r => r.AlertType == AlertType.BlockedScan && r.RecordId == scan.Id))
        {
            AdminAlertEndpoints.AddReview(
                db, AlertType.BlockedScan, scan.Id, "blocked_scans", admin.Id, now,
                $"ปิดเตือน{ThaiLabels.AlertName(AlertKind.BlockedScan)} ของ {person.DisplayName} เวลา {ThaiLabels.Clock(scan.ScannedAt)}: {alertOutcome}",
                note: null);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Created(
            $"/api/admin/cover-assignments/{cover.Id}",
            new CoverAssignmentDto(
                cover.Id, person.Id, person.DisplayName, area?.Id, area?.Code, building?.Id, building?.Code,
                cover.ShiftDate, cover.Shift, DateTime.SpecifyKind(now, DateTimeKind.Utc)));
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
```

The Cleaner branch keeps every message and every check of the old method, in the same order.

In `CancelAsync`:

1. Add `.Include(c => c.Building)` after `.Include(c => c.Area)`.
2. Make the twins query also match the building: `&& c.AreaId == cover.AreaId && c.BuildingId == cover.BuildingId`.
3. Replace the `AuditTrail.Add(...)` summary argument with a text that names either place:

```csharp
        var what = cover.Area is { } area
            ? $"การทำแทนของ {cover.User!.DisplayName} ที่ {area.Code}"
            : $"การตรวจแทนของ {cover.User!.DisplayName} ที่ตึก {cover.Building!.Code}";
        AuditTrail.Add(
            db, admin.Id, now, "COVER_CANCEL", "cover_assignments", cover.Id,
            $"ยกเลิก{what} กะ{ThaiLabels.ShiftName(cover.Shift)} {ThaiLabels.Date(cover.ShiftDate)}",
            before: new { cancelled = false },
            after: new { cancelled = true, alsoCancelled = twins.Select(t => t.Id).ToList() });
```

The Cleaner text stays exactly as before ("ยกเลิกการทำแทนของ … ที่ AR02 กะเช้า …").

If the file already had its own `Bad` helper, keep one.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~Cover"`
Expected: PASS for `SupervisorCoverTests` (12 cases), `CoverAssignmentEndpointTests` and `CoverFromAlertTests`.

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS.

- [ ] **Step 6: Update the handoff and commit**

Add under `## Task log (plan 6)`:

```markdown
- 2026-10-14 · Task 5 done: POST /api/admin/cover-assignments takes a Supervisor with buildingId (any shift but their own building-shift; from a Blocked Scan it closes that alert); cancel handles both kinds; CoverAssignmentDto.CleanerName renamed PersonName. All tests pass. Next: Task 6 (My Building and live updates).
```

```bash
git add backend/src backend/tests docs/handoff.md
git commit -m "feat(backend): Admin assigns a Supervisor to inspect another building or shift (facility-0048)"
```

---

### Task 6: "My Building" page and live updates to the building's Supervisors

**Files:**
- Create: `backend/src/FacilityRealtime.Api/Hubs/SubjectUserIdProvider.cs`
- Modify: `backend/src/FacilityRealtime.Api/Hubs/ScanHub.cs` (summary only)
- Create: `backend/src/FacilityRealtime.Api/Endpoints/PointNotifications.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/ScanRecordEndpoints.cs:136`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/InspectionEndpoints.cs` (the `SendAsync` line of `CreateAsync`)
- Create: `backend/src/FacilityRealtime.Api/DTOs/MyBuildingDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/MyBuildingEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Inspections/MyBuildingEndpointTests.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Inspections/SupervisorLiveTests.cs`

**Interfaces:**
- Consumes:
  - Task 4's `PointStatusResult.Rework` and `PointStatusDto.Rework`, and Task 5's building covers.
  - Existing: `PointBoardQuery.LoadAsync(db, nowUtc, slot, areaIds)`, `PointBoardRow`, `ScanHub.AdminGroup` and `HubClient.ConnectAsync`.
- Produces:
  - `PointNotifications.SendAsync(IHubContext<ScanHub> hub, AppDbContext db, string eventName, PointBoardRow board, DateTime nowUtc) : Task`
  - `SubjectUserIdProvider : IUserIdProvider`
  - `MyBuildingDto(DateOnly ShiftDate, Shift Shift, IReadOnlyList<MyBuildingSectionDto> Buildings)`
  - `MyBuildingSectionDto(int BuildingId, string BuildingCode, string BuildingName, bool IsCover, bool CanInspect, IReadOnlyList<PointStatusDto> Points)`
  - `GET /api/my-building`

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.ApiTests/Inspections/MyBuildingEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Points;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.ApiTests.Inspections;

public record MyBuildingSectionModel(int BuildingId, string BuildingCode, string BuildingName, bool IsCover, bool CanInspect, List<PointModel> Points);

public record MyBuildingModel(DateOnly ShiftDate, string Shift, List<MyBuildingSectionModel> Buildings);

/// <summary>facility-0057 rule 1 (wireframe M15): every point of the Supervisor's building, waiting-for-inspection first.</summary>
public class MyBuildingEndpointTests
{
    private const string Path = "/api/my-building";

    [Fact]
    public async Task My_building_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().GetAsync(Path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("cleaner")]
    [InlineData("admin")]
    public async Task Only_supervisors_have_a_building_page(string who)
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = who == "admin" ? await AuthApi.LoggedInAdminAsync(factory) : await AuthApi.LoggedInAsync(factory);

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Every_point_of_the_own_building_with_waiting_work_first()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var womens = await TestData.AddScanAsync(factory, "token-restroom-f1", ThaiClock.At(8, 8, 5));
        await TestData.AddInspectionAsync(factory, womens, InspectionResult.Rework, ThaiClock.At(8, 8, 15));
        await TestData.AddScanAsync(factory, "token-meeting-room", ThaiClock.At(8, 8, 20));
        await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var page = (await supervisor.GetFromJsonAsync<MyBuildingModel>(Path))!;

        Assert.Equal(new DateOnly(2026, 10, 8), page.ShiftDate);
        Assert.Equal("Day", page.Shift);
        var building = Assert.Single(page.Buildings);
        Assert.Equal("A", building.BuildingCode);
        Assert.False(building.IsCover);
        Assert.True(building.CanInspect);
        Assert.Equal(new[] { "ห้องน้ำชาย ชั้น 1", "ห้องประชุม", "ห้องน้ำหญิง ชั้น 1" }, building.Points.Select(p => p.Name));
        Assert.Equal(new[] { "PendingInspection", "PendingInspection", "Rework" }, building.Points.Select(p => p.Status));
        Assert.Equal("พื้นยังเปียก", building.Points[2].Rework!.Defect);
    }

    [Fact]
    public async Task Other_buildings_are_not_shown()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var page = (await supervisor.GetFromJsonAsync<MyBuildingModel>(Path))!;

        Assert.Equal("A", Assert.Single(page.Buildings).BuildingCode);
        Assert.DoesNotContain(page.Buildings.SelectMany(b => b.Points), p => p.BuildingCode == "B");
    }

    [Fact]
    public async Task Outside_the_own_shift_the_page_opens_but_cannot_inspect()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));

        var page = (await supervisor.GetFromJsonAsync<MyBuildingModel>(Path))!;

        Assert.Equal("Night", page.Shift);
        Assert.False(Assert.Single(page.Buildings).CanInspect);
    }

    [Fact]
    public async Task A_covered_building_follows_the_own_one()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        await TestData.AddSupervisorCoverAsync(factory, "S2001", "B", new DateOnly(2026, 10, 8), Shift.Day);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var page = (await supervisor.GetFromJsonAsync<MyBuildingModel>(Path))!;

        Assert.Equal(new[] { "A", "B" }, page.Buildings.Select(b => b.BuildingCode));
        Assert.Equal(new[] { false, true }, page.Buildings.Select(b => b.IsCover));
        Assert.Equal(new[] { true, true }, page.Buildings.Select(b => b.CanInspect));
        Assert.Equal("จุด BR01-01", Assert.Single(page.Buildings[1].Points).Name);
    }

    [Fact]
    public async Task The_page_carries_no_qr_tokens()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var body = await supervisor.GetStringAsync(Path);

        Assert.DoesNotContain("token-", body); // facility-0059
    }
}
```

Create `backend/tests/FacilityRealtime.ApiTests/Inspections/SupervisorLiveTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;

namespace FacilityRealtime.ApiTests.Inspections;

/// <summary>facility-0057, 0058 rule 3: a point change reaches the Admins and that building's Supervisors, nobody else.</summary>
public class SupervisorLiveTests
{
    private static readonly object Submit = new
    {
        qrToken = InspectionApi.MenRestroom, status = "Normal", latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
    };

    private static TaskCompletionSource<JsonElement> Listen(HubConnection hub, string eventName)
    {
        var got = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<JsonElement>(eventName, point => got.TrySetResult(point));
        return got;
    }

    [Fact]
    public async Task The_buildings_supervisors_get_a_submission_live_and_others_do_not()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        await TestData.AddSupervisorAsync(factory, "S2003", "0820000003", "B", Shift.Day);
        var (_, dayA, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2001", "0820000001");
        var (_, nightA, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2002", "0820000002");
        var (_, dayB, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2003", "0820000003");
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await using var dayAHub = await HubClient.ConnectAsync(factory, dayA.AccessToken);
        await using var nightAHub = await HubClient.ConnectAsync(factory, nightA.AccessToken);
        await using var dayBHub = await HubClient.ConnectAsync(factory, dayB.AccessToken);
        var dayAGot = Listen(dayAHub, "ScanRecorded");
        var nightAGot = Listen(nightAHub, "ScanRecorded");
        var dayBGot = Listen(dayBHub, "ScanRecorded");
        await Task.Delay(200);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Submit);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await dayAGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("PendingInspection", payload.GetProperty("status").GetString());
        await nightAGot.Task.WaitAsync(TimeSpan.FromSeconds(5)); // both shifts' Supervisors of building A
        await Task.Delay(500);
        Assert.False(dayBGot.Task.IsCompleted);
    }

    [Fact]
    public async Task A_supervisor_covering_the_building_gets_it_live()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddSupervisorAsync(factory, "S2003", "0820000003", "B", Shift.Day);
        await TestData.AddSupervisorCoverAsync(factory, "S2003", "A", new DateOnly(2026, 10, 8), Shift.Day);
        var (_, dayB, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2003", "0820000003");
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await using var hub = await HubClient.ConnectAsync(factory, dayB.AccessToken);
        var got = Listen(hub, "ScanRecorded");
        await Task.Delay(200);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

        await cleaner.PostAsJsonAsync("/api/scan-records", Submit);

        await got.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task The_buildings_supervisors_get_an_inspection_live()
    {
        using var factory = new FacilityApiFactory();
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (_, nightA, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2002", "0820000002");
        await using var hub = await HubClient.ConnectAsync(factory, nightA.AccessToken);
        var got = Listen(hub, "InspectionRecorded");
        await Task.Delay(200);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        await InspectionApi.InspectedAsync(supervisor, scanId, "Passed");

        Assert.Equal("Passed", (await got.Task.WaitAsync(TimeSpan.FromSeconds(5))).GetProperty("status").GetString());
    }
}
```

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~MyBuildingEndpointTests|FullyQualifiedName~SupervisorLiveTests"`
Expected: FAIL. `/api/my-building` answers 404, and the live tests time out waiting (`TimeoutException`).

- [ ] **Step 2: Address hub clients by account id**

Create `backend/src/FacilityRealtime.Api/Hubs/SubjectUserIdProvider.cs`:

```csharp
using FacilityRealtime.Api.Auth;
using Microsoft.AspNetCore.SignalR;

namespace FacilityRealtime.Api.Hubs;

/// <summary>Clients.User(id) addresses the account id in the access token's "sub" claim (inbound claim mapping is off).</summary>
public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst(AuthClaims.UserId)?.Value;
}
```

In `ScanHub.cs` replace the class summary with:

```csharp
/// <summary>
/// Server-to-client only. "ScanRecorded", "InspectionRecorded" and "AttendanceRecorded" go to Admin Accounts (facility-0058,
/// 0027); the first two also go to the Supervisors of the point's building, addressed by account id (facility-0057,
/// PointNotifications). Cleaners get nothing live.
/// </summary>
```

In `Program.cs` add `using Microsoft.AspNetCore.SignalR;`, and register the provider right after the `builder.Services.AddSignalR()...;` statement:

```csharp
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
```

- [ ] **Step 3: Send point changes to the building's Supervisors**

Create `backend/src/FacilityRealtime.Api/Endpoints/PointNotifications.cs`:

```csharp
using System.Globalization;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0058 rule 3, 0057: a point change goes to the Admins and to the Supervisors of that point's building — its
/// active Supervisors of both shifts, and anyone covering it in the clock's shift. Nobody else.
/// </summary>
internal static class PointNotifications
{
    /// <summary><paramref name="board"/> is the point as the clock's shift sees it: the Dashboard's view and the Supervisor's.</summary>
    public static async Task SendAsync(IHubContext<ScanHub> hub, AppDbContext db, string eventName, PointBoardRow board, DateTime nowUtc)
    {
        var dto = PointDtoMapper.ToDto(board);
        await hub.Clients.Group(ScanHub.AdminGroup).SendAsync(eventName, dto);

        var buildingId = board.Point.Area!.BuildingId;
        var slot = ShiftCalendar.SlotAt(nowUtc);
        var own = await db.Users
            .Where(u => u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId == buildingId)
            .Select(u => u.Id)
            .ToListAsync();
        var covering = await db.CoverAssignments
            .Where(c => c.BuildingId == buildingId && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Select(c => c.UserId)
            .ToListAsync();
        var userIds = own.Concat(covering).Distinct().Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
        if (userIds.Count > 0)
        {
            await hub.Clients.Users(userIds).SendAsync(eventName, dto);
        }
    }
}
```

In `ScanRecordEndpoints.cs` replace

```csharp
        await hub.Clients.Group(ScanHub.AdminGroup).SendAsync("ScanRecorded", PointDtoMapper.ToDto(board));
```

with

```csharp
        await PointNotifications.SendAsync(hub, db, "ScanRecorded", board, now);
```

In `InspectionEndpoints.CreateAsync` replace

```csharp
        await hub.Clients.Group(ScanHub.AdminGroup).SendAsync("InspectionRecorded", PointDtoMapper.ToDto(board));
```

with

```csharp
        await PointNotifications.SendAsync(hub, db, "InspectionRecorded", board, visit.Now);
```

Remove `using` lines the compiler now reports as unused only if the build warns about them. Do not touch others.

- [ ] **Step 4: Write the page**

Create `backend/src/FacilityRealtime.Api/DTOs/MyBuildingDtos.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>One building on the page. CanInspect: the Supervisor may inspect there now (own building in own shift, or a cover).</summary>
public record MyBuildingSectionDto(
    int BuildingId, string BuildingCode, string BuildingName, bool IsCover, bool CanInspect, IReadOnlyList<PointStatusDto> Points);

/// <summary>facility-0057 rule 1, wireframe M15: the clock's shift; the own building first, then buildings covered in it.</summary>
public record MyBuildingDto(DateOnly ShiftDate, Shift Shift, IReadOnlyList<MyBuildingSectionDto> Buildings);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/MyBuildingEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0057 rule 1 (wireframe M15): where a Supervisor should go next. It only points the way: an inspection still
/// needs the scan at the point. It opens outside the shift too, with CanInspect false (facility-0048).
/// </summary>
public static class MyBuildingEndpoints
{
    public static IEndpointRouteBuilder MapMyBuildingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/my-building", GetAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.Role != UserRole.Supervisor)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, "หน้าตึกของฉันใช้สำหรับหัวหน้าเท่านั้น");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var slot = ShiftCalendar.SlotAt(now);
        var coveredIds = await db.CoverAssignments
            .Where(c => c.UserId == user.Id && c.BuildingId != null && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Select(c => c.BuildingId!.Value)
            .ToListAsync();
        var buildingIds = (user.BuildingId is int own ? coveredIds.Prepend(own) : coveredIds).Distinct().ToList();
        var buildings = await db.Buildings.AsNoTracking()
            .Where(b => buildingIds.Contains(b.Id) && b.IsActive)
            .ToDictionaryAsync(b => b.Id);
        var areaIds = await db.Areas.Where(a => a.IsActive && buildingIds.Contains(a.BuildingId)).Select(a => a.Id).ToListAsync();
        var rows = await PointBoardQuery.LoadAsync(db, now, slot, areaIds);

        var sections = buildingIds
            .Where(buildings.ContainsKey)
            .Select(id => new MyBuildingSectionDto(
                id,
                buildings[id].Code,
                buildings[id].Name,
                id != user.BuildingId,
                coveredIds.Contains(id) || (id == user.BuildingId && user.Shift == slot.Shift),
                Sorted(rows.Where(r => r.Point.Area!.BuildingId == id)).Select(PointDtoMapper.ToDto).ToList()))
            .ToList();
        return Results.Ok(new MyBuildingDto(slot.ShiftDate, slot.Shift, sections));
    }

    /// <summary>รอตรวจ by submit time, then ต้องแก้ไข by the time it was asked, then the rest in card order (Area code, then point).</summary>
    private static IEnumerable<PointBoardRow> Sorted(IEnumerable<PointBoardRow> rows) =>
        rows.Select((row, index) => (Row: row, Index: index))
            .OrderBy(x => x.Row.Status.Status switch { PointStatus.PendingInspection => 0, PointStatus.Rework => 1, _ => 2 })
            .ThenBy(x => x.Row.Status.Status switch
            {
                PointStatus.PendingInspection => x.Row.LastScan?.SubmittedAt ?? DateTime.MaxValue,
                PointStatus.Rework => x.Row.Status.Rework?.InspectedAt ?? DateTime.MaxValue,
                _ => DateTime.MaxValue,
            })
            .ThenBy(x => x.Index)
            .Select(x => x.Row);
}
```

In `Program.cs` add `app.MapMyBuildingEndpoints();` after `app.MapInspectionEndpoints();`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~MyBuildingEndpointTests|FullyQualifiedName~SupervisorLiveTests"`
Expected: PASS (8 + 3).

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS. The existing test that a Cleaner's hub gets no `ScanRecorded` still passes: Cleaners are never addressed.

- [ ] **Step 6: Update the handoff and commit**

Add under `## Task log (plan 6)`:

```markdown
- 2026-10-14 · Task 6 done: GET /api/my-building (own building then covered ones, รอตรวจ first, CanInspect outside the shift false); ScanRecorded and InspectionRecorded reach the building's Supervisors by account id (SubjectUserIdProvider). All tests pass. Next: Task 7 (alerts).
```

```bash
git add backend/src backend/tests docs/handoff.md
git commit -m "feat(backend): Supervisor's My Building page with live updates for that building only (facility-0057, 0058)"
```

---

### Task 7: Alerts: inspections far from the sign, and Supervisors' Blocked Scans

**Files:**
- Modify: `backend/src/FacilityRealtime.Domain/Enums/AlertType.cs`
- Modify: `backend/src/FacilityRealtime.Application/Alerts/AlertKind.cs`
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/AlertQuery.cs` (whole file below)
- Modify: `backend/src/FacilityRealtime.Api/DTOs/AdminAlertDtos.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminAlertEndpoints.cs` (`FindAsync`, `ToDto`)
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs` (`AlertName`)
- Test: `backend/tests/FacilityRealtime.UnitTests/AlertKindsTests.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceModels.cs`, `AlertEndpointTests.cs:67`, `CoverFromAlertTests.cs:84`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/InspectionAlertTests.cs`

**Interfaces:**
- Consumes:
  - Task 1's inspection GPS columns and Task 2's `SupervisorScope.WithoutCover`.
  - Task 4's `InspectionApi.InspectAsync` / `InspectionCreatedModel`, and Task 5's building covers.
- Produces:
  - `AlertType.InspectionGps` (`INSPECTION_GPS`) and `AlertKind.InspectionGps`.
  - `AlertRow(..., ShiftSlot? CoverSlot, BlockReason? BlockReason = null)`
  - `CoverSuggestionDto(int UserId, int? AreaId, int? BuildingId, DateOnly ShiftDate, Shift Shift)`
  - `AlertDto(..., CoverSuggestionDto? Cover, BlockReason? BlockReason)`

- [ ] **Step 1: Write the failing tests**

In `backend/tests/FacilityRealtime.UnitTests/AlertKindsTests.cs` add an `[InlineData(AlertKind.InspectionGps, AlertType.InspectionGps)]` line to `Reviewable_kinds_map_to_their_alert_reviews_type`.

In `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceModels.cs`:
- Replace `CoverSuggestionModel` with `public record CoverSuggestionModel(int UserId, int? AreaId, int? BuildingId, DateOnly ShiftDate, string Shift);`
- Add `string? BlockReason` as the last member of `AlertModel`.

In `AlertEndpointTests.cs` line 67, add `null` for `BuildingId`:

```csharp
            new CoverSuggestionModel(alert.Person.Id, await AdminSetupApi.AreaIdAsync(factory, "AR02"), null, Oct8, "Day"),
```

In `CoverFromAlertTests.cs` line 84 use `suggestion.AreaId!.Value`:

```csharp
        var response = await AssignAsync(admin, suggestion.UserId, suggestion.AreaId!.Value, blockedId);
```

Create `backend/tests/FacilityRealtime.ApiTests/Admin/InspectionAlertTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Inspections;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0037, 0041, 0048 on the alert list (wireframe D3): inspections far from the sign, and Supervisors' Blocked Scans.</summary>
public class InspectionAlertTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    [Fact]
    public async Task An_inspection_far_from_the_sign_is_an_alert_the_admin_closes()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var inspected = await InspectionApi.InspectAsync(supervisor, scanId, "Passed", latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);
        var inspectionId = (await inspected.Content.ReadFromJsonAsync<InspectionCreatedModel>())!.InspectionId;

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("InspectionGps", alert.Kind);
        Assert.Equal(inspectionId, alert.RecordId);
        Assert.Equal("S2001", alert.Person.EmployeeId);
        Assert.Equal("Supervisor", alert.Person.Role);
        Assert.Equal("AR01", alert.AreaCode);
        Assert.Equal("AR01-01", alert.SignCode);
        Assert.Equal("ห้องน้ำชาย ชั้น 1", alert.PointName);
        Assert.True(alert.DistanceM > 1000);
        Assert.Null(alert.Cover);
        Assert.Null(alert.BlockReason);
        Assert.False(alert.Reviewed);

        var review = await AdminAttendanceApi.ReviewAsync(admin, "InspectionGps", inspectionId, "GPS ในอาคารเพี้ยน");

        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        Assert.True(Assert.Single(await AdminAttendanceApi.AlertsAsync(admin)).Reviewed);
        var audit = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ALERT_REVIEW", audit.Action);
        Assert.Equal("inspections", audit.EntityType);
        Assert.Equal("ปิดเตือนตรวจงานไกลจากป้าย ของ สมปอง ตรวจดี เวลา 08:30", audit.Summary);
    }

    [Fact]
    public async Task An_inspection_inside_the_radius_is_no_alert_and_cannot_be_closed()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var inspected = await InspectionApi.InspectedAsync(supervisor, scanId, "Passed");

        Assert.Empty(await AdminAttendanceApi.AlertsAsync(admin));
        Assert.Equal(HttpStatusCode.NotFound, (await AdminAttendanceApi.ReviewAsync(admin, "InspectionGps", inspected.InspectionId)).StatusCode);
    }

    [Fact]
    public async Task A_supervisors_blocked_scan_says_why_and_offers_a_building_cover()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        Assert.Equal(HttpStatusCode.Forbidden, (await InspectionApi.ScanAsync(supervisor, InspectionApi.BuildingBPoint)).StatusCode);

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("BlockedScan", alert.Kind);
        Assert.Equal("OtherBuilding", alert.BlockReason);
        Assert.Equal("BR01", alert.AreaCode);
        Assert.Equal(
            new CoverSuggestionModel(alert.Person.Id, null, await AdminSetupApi.BuildingIdAsync(factory, "B"), Oct8, "Day"),
            alert.Cover);

        var assigned = await admin.PostAsJsonAsync("/api/admin/cover-assignments", new
        {
            userId = alert.Cover!.UserId,
            buildingId = alert.Cover.BuildingId,
            shiftDate = alert.Cover.ShiftDate,
            shift = alert.Cover.Shift,
            blockedScanId = alert.RecordId,
        });

        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        var after = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));
        Assert.True(after.Reviewed);
        Assert.Null(after.Cover);
    }

    [Fact]
    public async Task Outside_the_shift_it_offers_a_cover_of_the_own_building_for_that_shift()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));
        await InspectionApi.ScanAsync(supervisor);

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("OutsideShift", alert.BlockReason);
        Assert.Equal(new CoverSuggestionModel(alert.Person.Id, null, await AdminSetupApi.BuildingIdAsync(factory, "A"), Oct8, "Night"), alert.Cover);
    }

    [Fact]
    public async Task No_cover_is_offered_once_that_shift_has_ended()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));
        await InspectionApi.ScanAsync(supervisor);
        factory.Clock.SetUtcNow(ThaiClock.At(9, 7, 30));

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Null(alert.Cover);
    }
}
```

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AlertKindsTests"`
Expected: build FAILS: `AlertKind` has no `InspectionGps`.

- [ ] **Step 2: Add the alert kind**

Replace `backend/src/FacilityRealtime.Domain/Enums/AlertType.cs` with:

```csharp
namespace FacilityRealtime.Domain.Enums;

/// <summary>
/// database.html alert_reviews.alert_type: the alerts an Admin closes with "คุยแล้ว ปิดเตือน" (facility-0035, 0037, 0041).
/// A missing Shift-Out is not one: it closes itself when the Admin fills the time (facility-0051).
/// </summary>
public enum AlertType
{
    AttendanceGps,
    SubmissionGps,
    BlockedScan,
    InspectionGps,
}
```

In `backend/src/FacilityRealtime.Application/Alerts/AlertKind.cs` add, as the last member of `AlertKind`:

```csharp
    /// <summary>An inspection outside its sign's radius: recorded and flagged (facility-0037).</summary>
    InspectionGps,
```

and the case `AlertKind.InspectionGps => AlertType.InspectionGps,` in `AlertKinds.ReviewType`, before the `_ => null` arm. Update the `BlockedScan` member's summary to: `A scan refused for another Area (Cleaner) or another building or shift (Supervisor) (facility-0041, 0048).`

In `ThaiLabels.AlertName` add the arm `AlertKind.InspectionGps => "ตรวจงานไกลจากป้าย",` before the `_` arm.

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AlertKindsTests"`
Expected: PASS.

- [ ] **Step 3: Load the new alerts**

Replace `backend/src/FacilityRealtime.Infrastructure/Persistence/AlertQuery.cs` with:

```csharp
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>
/// One line of the alert list (wireframe D3). Area is where it happened: the scanned sign's Area, or for a missing
/// event the Area the Cleaner checked in to. CoverSlot is set when the Admin can still give this person a cover there —
/// that Area for a Cleaner, that Area's building for a Supervisor. BlockReason is set on a Blocked Scan.
/// </summary>
public sealed record AlertRow(
    AlertKind Kind,
    long RecordId,
    DateTime At,
    User Person,
    Area Area,
    Sign? Sign,
    short? DistanceM,
    short? AccuracyM,
    IReadOnlyList<AttendanceEvent> MissingEvents,
    AlertReview? Review,
    ShiftSlot? CoverSlot,
    BlockReason? BlockReason = null);

public static class AlertQuery
{
    /// <summary>
    /// Every alert whose time falls in [fromUtc, toUtc), newest first. Sign comes with its Area (and building) and point;
    /// Person with their Area.
    /// </summary>
    public static async Task<IReadOnlyList<AlertRow>> LoadAsync(
        AppDbContext db, DateTime fromUtc, DateTime toUtc, DateTime nowUtc, AttendanceSettings settings)
    {
        // facility-0035/0037: recorded but flagged, for the Admin to talk to the person
        var attendanceFlags = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.WithinRadius == false && a.SignId != null && a.OccurredAt >= fromUtc && a.OccurredAt < toUtc)
            .ToListAsync();
        var submissionFlags = await db.ScanRecords.AsNoTracking()
            .Where(s => s.WithinRadius == false && s.SubmittedAt >= fromUtc && s.SubmittedAt < toUtc)
            .ToListAsync();
        var inspectionFlags = await db.InspectionRecords.AsNoTracking()
            .Where(i => i.WithinRadius == false && i.InspectedAt >= fromUtc && i.InspectedAt < toUtc)
            .ToListAsync();
        var blocked = await db.BlockedScans.AsNoTracking()
            .Where(b => b.ScannedAt >= fromUtc && b.ScannedAt < toUtc)
            .ToListAsync();

        // facility-0051 rule 2: shifts that ended in the range, of Cleaners who checked in but did not finish scanning.
        // A night shift ends the morning after its ShiftDate, so look one day earlier.
        var firstDate = DateOnly.FromDateTime(ThaiTime.FromUtc(fromUtc)).AddDays(-1);
        var lastDate = DateOnly.FromDateTime(ThaiTime.FromUtc(toUtc));
        var shiftEvents = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.ShiftDate >= firstDate && a.ShiftDate <= lastDate)
            .ToListAsync();
        var missing = shiftEvents
            .GroupBy(a => (a.UserId, Slot: new ShiftSlot(a.ShiftDate, a.Shift)))
            .Select(g => (
                g.Key.UserId,
                End: ShiftCalendar.EndUtc(g.Key.Slot),
                ShiftIn: g.FirstOrDefault(a => a.EventType == AttendanceEvent.ShiftIn),
                Missing: AttendanceRules.MissingAtShiftEnd(g.Select(a => a.EventType).ToList())))
            .Where(s => s.End <= nowUtc && s.End >= fromUtc && s.End < toUtc && s.ShiftIn is not null && s.Missing.Count > 0)
            .ToList();

        // An inspection names its point, not its sign; each point has exactly one sign
        var flaggedPointIds = inspectionFlags.Select(i => i.ServicePointId).Distinct().ToList();
        var signIds = attendanceFlags.Select(a => a.SignId!.Value)
            .Concat(submissionFlags.Select(s => s.SignId))
            .Concat(blocked.Select(b => b.SignId))
            .Distinct()
            .ToList();
        var loadedSigns = await db.Signs.AsNoTracking()
            .Include(s => s.Area!).ThenInclude(a => a.Building)
            .Include(s => s.ServicePoint)
            .Where(s => signIds.Contains(s.Id) || (s.ServicePointId != null && flaggedPointIds.Contains(s.ServicePointId.Value)))
            .ToListAsync();
        var signs = loadedSigns.ToDictionary(s => s.Id);
        var signOfPoint = loadedSigns.Where(s => s.ServicePointId != null).ToDictionary(s => s.ServicePointId!.Value);

        var userIds = attendanceFlags.Select(a => a.UserId)
            .Concat(submissionFlags.Select(s => s.UserId))
            .Concat(inspectionFlags.Select(i => i.SupervisorId))
            .Concat(blocked.Select(b => b.UserId))
            .Concat(missing.Select(m => m.UserId))
            .Distinct()
            .ToList();
        var users = await db.Users.AsNoTracking()
            .Include(u => u.Area)
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);
        var missingAreaIds = missing.Select(m => m.ShiftIn!.AreaId).Distinct().ToList();
        var areas = await db.Areas.AsNoTracking().Where(a => missingAreaIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id);

        var recordIds = attendanceFlags.Select(a => a.Id)
            .Concat(submissionFlags.Select(s => s.Id))
            .Concat(inspectionFlags.Select(i => i.Id))
            .Concat(blocked.Select(b => b.Id))
            .Distinct()
            .ToList();
        var reviews = await db.AlertReviews.AsNoTracking().Where(r => recordIds.Contains(r.RecordId)).ToListAsync();
        var blockedUserIds = blocked.Select(b => b.UserId).Distinct().ToList();
        var activeCovers = await db.CoverAssignments.AsNoTracking()
            .Where(c => c.CancelledAt == null && c.ShiftDate >= firstDate && blockedUserIds.Contains(c.UserId))
            .ToListAsync();

        AlertReview? ReviewOf(AlertType type, long id) => reviews.FirstOrDefault(r => r.AlertType == type && r.RecordId == id);

        // facility-0041 rule 4, 0048 rule 2: offer a cover for the scan's own shift while that shift has not ended and none exists yet
        ShiftSlot? CoverSlotFor(BlockedScan scan)
        {
            var user = users[scan.UserId];
            var area = signs[scan.SignId].Area!;
            return user.Role == UserRole.Supervisor ? SupervisorCoverSlot(user, area, scan) : CleanerCoverSlot(user, area, scan);
        }

        // Not for a Cleaner without an Area (no sign to check in at, decision 10); HasShift is false for a deactivated Area.
        ShiftSlot? CleanerCoverSlot(User user, Area area, BlockedScan scan)
        {
            if (user is not { Role: UserRole.Cleaner, IsActive: true, Shift: { } shift } || user.AreaId is not int ownAreaId || ownAreaId == area.Id || !area.HasShift(shift))
            {
                return null;
            }

            var slot = AttendanceCalendar.SlotFor(shift, scan.ScannedAt, settings);
            return slot is not null
                && ShiftCalendar.EndUtc(slot) > nowUtc
                && !activeCovers.Any(c => c.UserId == user.Id && c.AreaId == area.Id && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift)
                    ? slot
                    : null;
        }

        // A Supervisor's shift is the clock's at the scan; the cover is for the sign's building (plan 6 decision 7)
        ShiftSlot? SupervisorCoverSlot(User user, Area area, BlockedScan scan)
        {
            if (!user.IsActive || area.Building is not { IsActive: true })
            {
                return null;
            }

            var slot = ShiftCalendar.SlotAt(scan.ScannedAt);
            return ShiftCalendar.EndUtc(slot) > nowUtc
                && SupervisorScope.WithoutCover(user.BuildingId, user.Shift, area.BuildingId, slot.Shift) is not null
                && !activeCovers.Any(c => c.UserId == user.Id && c.BuildingId == area.BuildingId && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift)
                    ? slot
                    : null;
        }

        var rows = new List<AlertRow>();
        rows.AddRange(attendanceFlags.Select(a => new AlertRow(
            AlertKind.AttendanceGps, a.Id, a.OccurredAt, users[a.UserId], signs[a.SignId!.Value].Area!, signs[a.SignId!.Value],
            a.DistanceM, a.AccuracyM, [], ReviewOf(AlertType.AttendanceGps, a.Id), null)));
        rows.AddRange(submissionFlags.Select(s => new AlertRow(
            AlertKind.SubmissionGps, s.Id, s.SubmittedAt, users[s.UserId], signs[s.SignId].Area!, signs[s.SignId],
            s.DistanceM, s.AccuracyM, [], ReviewOf(AlertType.SubmissionGps, s.Id), null)));
        rows.AddRange(inspectionFlags.Select(i => new AlertRow(
            AlertKind.InspectionGps, i.Id, i.InspectedAt, users[i.SupervisorId], signOfPoint[i.ServicePointId].Area!, signOfPoint[i.ServicePointId],
            i.DistanceM, i.AccuracyM, [], ReviewOf(AlertType.InspectionGps, i.Id), null)));
        rows.AddRange(blocked.Select(b => new AlertRow(
            AlertKind.BlockedScan, b.Id, b.ScannedAt, users[b.UserId], signs[b.SignId].Area!, signs[b.SignId],
            b.DistanceM, b.AccuracyM, [], ReviewOf(AlertType.BlockedScan, b.Id), CoverSlotFor(b), b.Reason)));
        rows.AddRange(missing.Select(m => new AlertRow(
            AlertKind.MissingAttendance, m.ShiftIn!.Id, m.End, users[m.UserId], areas[m.ShiftIn.AreaId], null,
            null, null, m.Missing, null, null)));

        return rows.OrderByDescending(r => r.At).ThenByDescending(r => r.RecordId).ToList();
    }
}
```

Compared with the old file, only these change: `inspectionFlags`, the sign load (it adds the building and the flagged points' signs), the Supervisor branch of `CoverSlotFor`, the inspection rows, and `b.Reason`. Everything else is as it was.

- [ ] **Step 4: Show and close them**

In `backend/src/FacilityRealtime.Api/DTOs/AdminAlertDtos.cs`:

```csharp
/// <summary>
/// What to send to POST /api/admin/cover-assignments, with the Blocked Scan's id: AreaId for a Cleaner's cover,
/// BuildingId for a Supervisor's (the other is null).
/// </summary>
public record CoverSuggestionDto(int UserId, int? AreaId, int? BuildingId, DateOnly ShiftDate, Shift Shift);
```

Add `BlockReason? BlockReason` as the last member of `AlertDto`. In its summary, change the `RecordId` sentence to "RecordId is the row in shift_attendances, scan_records, inspections or blocked_scans", and add "BlockReason is set on a Blocked Scan".

In `AdminAlertEndpoints.cs`:
- In `FindAsync`, add this arm before the `_ =>` arm:

```csharp
        AlertType.InspectionGps => await db.InspectionRecords
            .Where(i => i.Id == id && i.WithinRadius == false)
            .Select(i => new AlertTarget("inspections", i.Supervisor!.DisplayName, i.InspectedAt))
            .FirstOrDefaultAsync(),
```

- In `ToDto`, replace the last argument (the `CoverSuggestionDto` one) with:

```csharp
        row.CoverSlot is { } slot
            ? row.Person.Role == UserRole.Supervisor
                ? new CoverSuggestionDto(row.Person.Id, null, row.Area.BuildingId, slot.ShiftDate, slot.Shift)
                : new CoverSuggestionDto(row.Person.Id, row.Area.Id, null, slot.ShiftDate, slot.Shift)
            : null,
        row.BlockReason);
```

- Change the class summary to: `The alert list (wireframe D3): Blocked Scans, scans and inspections far from their sign, and missing Shift-Outs. Closing one is logged (facility-0051).`

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~InspectionAlertTests|FullyQualifiedName~AlertEndpointTests|FullyQualifiedName~CoverFromAlertTests"`
Expected: PASS.

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS.

- [ ] **Step 6: Update the handoff and commit**

Add under `## Task log (plan 6)`:

```markdown
- 2026-10-14 · Task 7 done: alert list adds INSPECTION_GPS (closed with คุยแล้ว), shows each Blocked Scan's reason, and offers a building cover for a Supervisor's Blocked Scan. All tests pass. Next: Task 8 (inspection sheet of the export).
```

```bash
git add backend/src backend/tests docs/handoff.md
git commit -m "feat(backend): alerts for inspections far from the sign and Supervisors' Blocked Scans with a building cover (facility-0037, 0048)"
```

---
### Task 8: Round outcomes and the export's inspection sheet

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Rounds/RoundOutcomes.cs`
- Create: `backend/src/FacilityRealtime.Application/Inspections/InspectionHistory.cs`
- Modify: `backend/src/FacilityRealtime.Application/Export/ExportRules.cs` (`ExportSet` + `Inspections`)
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/InspectionHistoryQuery.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs` (+ `InspectionResultName`, `RoundOutcomeName`)
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/ExportSheets.cs` (+ `Inspections`)
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminExportEndpoints.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs` (`AddInspectionAsync` options)
- Test: `backend/tests/FacilityRealtime.UnitTests/RoundOutcomesTests.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/InspectionHistoryTests.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/InspectionExportTests.cs`

**Interfaces:**
- Consumes:
  - `RoundRules.LatestInspection` / `NeedsRework` (internal).
  - `ShiftCalendar.EndUtc` / `ToUtc`, and `ExcelSheet` / `ExcelWorkbook`.
  - The existing `ExportSheets.SortedRow` / `Sort` / `Round` helpers.
  - Task 1's inspection GPS and cover.
- Produces:
  - `enum RoundOutcome { Passed, NotInspected, NotReworked }`
  - `RoundOutcomes.Of(IReadOnlyList<SubmissionFact> roundSubmissions, IReadOnlyList<InspectionFact> inspections) : RoundOutcome`
  - `RoundSubmission(long Id, int PointId, ShiftSlot Slot, TimeOnly RoundStart, TimeOnly RoundEnd, DateTime SubmittedAtUtc)`
  - `FinishedRound(int PointId, ShiftSlot Slot, TimeOnly RoundStart, TimeOnly RoundEnd, RoundOutcome Outcome)`
  - `InspectionHistory.FinishedRounds(IReadOnlyList<RoundSubmission>, IReadOnlyList<InspectionFact>, DateTime nowUtc, int closesMinutesAfterShift) : IReadOnlyList<FinishedRound>`
  - `InspectionExportRow(...)` and `PointPlace(int Id, int BuildingId, string BuildingCode, string AreaCode, string PointName, string SignCode, bool Counted, bool WorksNight)`
  - On `InspectionHistoryQuery`:
    - `LoadRoundSubmissionsAsync(db, first, last, buildingId) : Task<List<RoundSubmission>>`
    - `LoadInspectionsAsync(db, first, last, buildingId) : Task<List<InspectionExportRow>>`
    - `LoadPointPlacesAsync(db, buildingId) : Task<List<PointPlace>>`
  - `ExportSet.Inspections` and the sheet "การตรวจงาน".
  - `TestData.AddInspectionAsync(factory, scanRecordId, result, at, supervisorEmployeeId = "S2001", withinRadius = null, distanceM = null, coverAssignmentId = null)`

- [ ] **Step 1: Write the failing unit tests**

Create `backend/tests/FacilityRealtime.UnitTests/RoundOutcomesTests.cs`:

```csharp
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0047 rule 5: what a finished round with a submission left in the history.</summary>
public class RoundOutcomesTests
{
    private static DateTime Thai(int hour, int minute) => new DateTime(2026, 10, 8, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);

    private static SubmissionFact Sent(long id, int hour, int minute) => new(id, 1, Thai(hour, minute));

    private static InspectionFact Inspected(long submissionId, InspectionResult result, int hour, int minute) => new(submissionId, result, Thai(hour, minute));

    [Fact]
    public void Submitted_and_never_inspected_is_not_inspected() =>
        Assert.Equal(RoundOutcome.NotInspected, RoundOutcomes.Of([Sent(1, 8, 10)], []));

    [Fact]
    public void A_passed_inspection_is_passed() =>
        Assert.Equal(RoundOutcome.Passed, RoundOutcomes.Of([Sent(1, 8, 10)], [Inspected(1, InspectionResult.Passed, 8, 20)]));

    [Fact]
    public void Rework_never_fixed_is_not_reworked() =>
        Assert.Equal(RoundOutcome.NotReworked, RoundOutcomes.Of([Sent(1, 8, 10)], [Inspected(1, InspectionResult.Rework, 8, 20)]));

    [Fact]
    public void A_fix_nobody_inspected_is_not_inspected() =>
        Assert.Equal(
            RoundOutcome.NotInspected,
            RoundOutcomes.Of([Sent(1, 8, 10), Sent(2, 8, 40)], [Inspected(1, InspectionResult.Rework, 8, 20)]));

    [Fact]
    public void A_fix_that_passed_is_passed() =>
        Assert.Equal(
            RoundOutcome.Passed,
            RoundOutcomes.Of(
                [Sent(1, 8, 10), Sent(2, 8, 40)],
                [Inspected(1, InspectionResult.Rework, 8, 20), Inspected(2, InspectionResult.Passed, 8, 50)]));

    [Fact]
    public void Inspections_of_other_submissions_do_not_count() =>
        Assert.Equal(RoundOutcome.NotInspected, RoundOutcomes.Of([Sent(1, 8, 10)], [Inspected(9, InspectionResult.Passed, 8, 20)]));
}
```

Create `backend/tests/FacilityRealtime.UnitTests/InspectionHistoryTests.cs`:

```csharp
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>Plan 6 decision 13: outcomes of rounds with a submission, once the shift's attendance window has closed.</summary>
public class InspectionHistoryTests
{
    private const int Closes = 60;
    private static readonly ShiftSlot Day8 = new(new DateOnly(2026, 10, 8), Shift.Day);
    private static readonly ShiftSlot Night8 = new(new DateOnly(2026, 10, 8), Shift.Night);
    private static readonly DateTime AfterBoth = ShiftCalendar.EndUtc(Night8).AddMinutes(Closes + 1);

    /// <summary>A submission in the two-hour round starting at <paramref name="startHour"/>, <paramref name="minutesIn"/> minutes in.</summary>
    private static RoundSubmission Sent(long id, int point, ShiftSlot slot, int startHour, int minutesIn) =>
        new(id, point, slot, new TimeOnly(startHour, 0), new TimeOnly(startHour + 2, 0), ShiftCalendar.ToUtc(slot, new TimeOnly(startHour, minutesIn)));

    private static InspectionFact Inspected(long submissionId, InspectionResult result, ShiftSlot slot, int hour, int minute) =>
        new(submissionId, result, ShiftCalendar.ToUtc(slot, new TimeOnly(hour, minute)), result == InspectionResult.Rework ? "ยังสกปรก" : null);

    [Fact]
    public void Each_round_gets_its_outcome_in_the_order_of_the_shifts()
    {
        var rounds = InspectionHistory.FinishedRounds(
            [Sent(1, 10, Day8, 16, 10), Sent(2, 10, Day8, 7, 10), Sent(3, 11, Day8, 7, 20), Sent(4, 10, Night8, 20, 5)],
            [Inspected(2, InspectionResult.Rework, Day8, 7, 30), Inspected(3, InspectionResult.Passed, Day8, 7, 40)],
            AfterBoth,
            Closes);

        Assert.Equal(
            new[]
            {
                (10, Shift.Day, 7, RoundOutcome.NotReworked),
                (11, Shift.Day, 7, RoundOutcome.Passed),
                (10, Shift.Day, 16, RoundOutcome.NotInspected),
                (10, Shift.Night, 20, RoundOutcome.NotInspected),
            },
            rounds.Select(r => (r.PointId, r.Slot.Shift, r.RoundStart.Hour, r.Outcome)));
    }

    [Fact]
    public void Several_submissions_of_one_round_are_one_round()
    {
        var rounds = InspectionHistory.FinishedRounds(
            [Sent(1, 10, Day8, 7, 10), Sent(2, 10, Day8, 7, 40)],
            [Inspected(1, InspectionResult.Rework, Day8, 7, 20), Inspected(2, InspectionResult.Passed, Day8, 7, 50)],
            AfterBoth,
            Closes);

        Assert.Equal(RoundOutcome.Passed, Assert.Single(rounds).Outcome);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(Closes, false)]
    [InlineData(Closes + 1, true)]
    public void A_shift_still_taking_late_work_has_no_outcome_yet(int minutesAfterTheShift, bool counted)
    {
        var rounds = InspectionHistory.FinishedRounds(
            [Sent(1, 10, Day8, 7, 10)], [], ShiftCalendar.EndUtc(Day8).AddMinutes(minutesAfterTheShift), Closes);

        Assert.Equal(counted ? 1 : 0, rounds.Count);
    }
}
```

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~RoundOutcomesTests|FullyQualifiedName~InspectionHistoryTests"`
Expected: build FAILS: `RoundOutcomes` and `InspectionHistory` do not exist.

- [ ] **Step 2: Write the outcome rules**

Create `backend/src/FacilityRealtime.Application/Rounds/RoundOutcomes.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Rounds;

/// <summary>facility-0047 rule 5: what a finished round that had a submission left in the history.</summary>
public enum RoundOutcome
{
    /// <summary>ตรวจผ่าน: the latest inspection passed and nothing was submitted after it.</summary>
    Passed,

    /// <summary>ไม่ได้ตรวจ: submitted, and the latest submission was never inspected.</summary>
    NotInspected,

    /// <summary>ไม่ได้แก้: the Supervisor asked for rework and nothing was submitted after it.</summary>
    NotReworked,
}

public static class RoundOutcomes
{
    /// <summary><paramref name="roundSubmissions"/>: the round's OnTime, Late and Rework submissions, at least one.</summary>
    public static RoundOutcome Of(IReadOnlyList<SubmissionFact> roundSubmissions, IReadOnlyList<InspectionFact> inspections)
    {
        var latest = RoundRules.LatestInspection(roundSubmissions, inspections);
        return latest is null ? RoundOutcome.NotInspected
            : RoundRules.NeedsRework(roundSubmissions, latest) ? RoundOutcome.NotReworked
            : roundSubmissions.Any(s => s.SubmittedAt > latest.InspectedAt) ? RoundOutcome.NotInspected
            : RoundOutcome.Passed;
    }
}
```

Create `backend/src/FacilityRealtime.Application/Inspections/InspectionHistory.cs`:

```csharp
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;

namespace FacilityRealtime.Application.Inspections;

/// <summary>A Scan Record placed in a round (OnTime, Late or Rework), with the round's start and end as copied onto it when saved.</summary>
public sealed record RoundSubmission(long Id, int PointId, ShiftSlot Slot, TimeOnly RoundStart, TimeOnly RoundEnd, DateTime SubmittedAtUtc);

/// <summary>One round of one point in one shift that had a submission, and what it left in the history.</summary>
public sealed record FinishedRound(int PointId, ShiftSlot Slot, TimeOnly RoundStart, TimeOnly RoundEnd, RoundOutcome Outcome);

public static class InspectionHistory
{
    /// <summary>
    /// Plan 6 decision 13: the outcome of every round with a submission, only in shifts whose attendance window has closed —
    /// the same rule as "ไม่ได้ทำ" in ExportRules.MissedRounds — in the order the shifts met them. A round is its point,
    /// shift and the start and end copied onto its submissions, so a window deleted since keeps its history.
    /// </summary>
    public static IReadOnlyList<FinishedRound> FinishedRounds(
        IReadOnlyList<RoundSubmission> submissions, IReadOnlyList<InspectionFact> inspections, DateTime nowUtc, int closesMinutesAfterShift)
    {
        var bySubmission = inspections.ToLookup(i => i.SubmissionId);
        return submissions
            .Where(s => ShiftCalendar.EndUtc(s.Slot).AddMinutes(closesMinutesAfterShift) < nowUtc)
            .GroupBy(s => (s.PointId, s.Slot, s.RoundStart, s.RoundEnd))
            .Select(round =>
            {
                var facts = round.Select(s => new SubmissionFact(s.Id, null, s.SubmittedAtUtc)).ToList();
                var roundInspections = round.SelectMany(s => bySubmission[s.Id]).ToList();
                return new FinishedRound(
                    round.Key.PointId, round.Key.Slot, round.Key.RoundStart, round.Key.RoundEnd, RoundOutcomes.Of(facts, roundInspections));
            })
            .OrderBy(r => r.Slot.ShiftDate)
            .ThenBy(r => r.Slot.Shift)
            .ThenBy(r => ShiftCalendar.ToUtc(r.Slot, r.RoundStart))
            .ThenBy(r => r.PointId)
            .ToList();
    }
}
```

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests`
Expected: PASS.

- [ ] **Step 3: Write the failing API tests**

In `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs`, replace `AddInspectionAsync` with:

```csharp
    /// <summary>An Inspection Record, by Supervisor S2001 unless another is named. GPS and cover stay empty unless given.</summary>
    public static Task AddInspectionAsync(
        FacilityApiFactory factory,
        long scanRecordId,
        InspectionResult result,
        DateTimeOffset at,
        string supervisorEmployeeId = "S2001",
        bool? withinRadius = null,
        short? distanceM = null,
        int? coverAssignmentId = null) =>
        factory.WithDbAsync(async db =>
        {
            var scan = await db.ScanRecords.SingleAsync(s => s.Id == scanRecordId);
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == supervisorEmployeeId);
            db.InspectionRecords.Add(new InspectionRecord
            {
                ScanRecordId = scan.Id,
                ServicePointId = scan.ServicePointId,
                SupervisorId = supervisor.Id,
                Result = result,
                Defect = result == InspectionResult.Rework ? "พื้นยังเปียก" : null,
                InspectedAt = at.UtcDateTime,
                WithinRadius = withinRadius,
                DistanceM = distanceM,
                CoverAssignmentId = coverAssignmentId,
            });
            await db.SaveChangesAsync();
        });
```

Create `backend/tests/FacilityRealtime.ApiTests/Admin/InspectionExportTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Inspections;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0053, 0047 rule 5: the export's "การตรวจงาน" sheet.</summary>
public class InspectionExportTests
{
    private const string Path = "/api/admin/export";

    private static readonly string[] Header =
        ["วันที่กะ", "กะ", "ตึก", "Area", "จุด", "ป้าย", "รอบ", "เวลาตรวจ", "ผล", "ข้อบกพร่อง", "ตรวจแทน", "รหัสพนักงาน", "ชื่อ", "GPS"];

    private static async Task<(FacilityApiFactory Factory, HttpClient Admin)> StartAsync(DateTimeOffset now)
    {
        var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.BackdateSetupAsync(factory);
        factory.Clock.SetUtcNow(now);
        return (factory, admin);
    }

    private static Task<HttpResponseMessage> ExportAsync(HttpClient admin, string[] sets, int? buildingId = null) =>
        admin.PostAsJsonAsync(Path, new { from = "2026-10-08", to = "2026-10-08", buildingId, sets });

    private static async Task<XLWorkbook> WorkbookAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }

    /// <summary>Every row under the header, as the text in each header column.</summary>
    private static List<string[]> Rows(IXLWorksheet sheet)
    {
        var width = sheet.Row(1).CellsUsed().Count();
        return sheet.RowsUsed().Skip(1)
            .Select(row => Enumerable.Range(1, width).Select(c => row.Cell(c).GetString()).ToArray())
            .ToList();
    }

    [Fact]
    public async Task Inspections_and_the_rounds_left_uninspected_or_unfixed()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(9, 12, 0));
        using var _ = factory;
        var rework = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, rework, InspectionResult.Rework, ThaiClock.At(8, 8, 20));
        await TestData.AddScanAsync(factory, "token-restroom-f1", ThaiClock.At(8, 16, 10));
        var passed = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 16, 20));
        await TestData.AddInspectionAsync(factory, passed, InspectionResult.Passed, ThaiClock.At(8, 16, 30), withinRadius: false, distanceM: 120);

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Inspections"]));

        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("การตรวจงาน", sheet.Name);
        Assert.Equal(Header, sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        var rows = Rows(sheet);
        Assert.Equal(4, rows.Count);
        Assert.Equal(
            new[] { "2026-10-08", "เช้า", "A", "AR01", "ห้องน้ำชาย ชั้น 1", "AR01-01", "07:00–09:00", "08:20", "ต้องแก้ไข", "พื้นยังเปียก", "", "S2001", "สมปอง ตรวจดี", "-" },
            rows[0]);
        Assert.Equal(
            new[] { "2026-10-08", "เช้า", "A", "AR01", "ห้องน้ำชาย ชั้น 1", "AR01-01", "07:00–09:00", "", "ไม่ได้แก้", "", "", "", "", "" },
            rows[1]);
        Assert.Equal(
            new[] { "2026-10-08", "เช้า", "A", "AR01", "ห้องน้ำชาย ชั้น 1", "AR01-01", "16:00–18:00", "16:30", "สะอาด", "", "", "S2001", "สมปอง ตรวจดี", "นอกรัศมี 120 ม." },
            rows[2]);
        Assert.Equal(
            new[] { "2026-10-08", "เช้า", "A", "AR01", "ห้องน้ำหญิง ชั้น 1", "AR01-02", "16:00–18:00", "", "ไม่ได้ตรวจ", "", "", "", "", "" },
            rows[3]);
    }

    [Fact]
    public async Task An_inspection_under_a_cover_is_marked()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(9, 12, 0));
        using var _ = factory;
        await InspectionApi.AddBuildingBAsync(factory);
        var coverId = await TestData.AddSupervisorCoverAsync(factory, "S2001", "B", new DateOnly(2026, 10, 8), Shift.Day);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.BuildingBPoint, ThaiClock.At(8, 8, 30));
        await TestData.AddInspectionAsync(factory, scanId, InspectionResult.Passed, ThaiClock.At(8, 8, 40), coverAssignmentId: coverId);

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Inspections"]));

        var row = Assert.Single(Rows(workbook.Worksheet(1)));
        Assert.Equal(new[] { "B", "BR01", "สะอาด", "ตรวจแทน" }, new[] { row[2], row[3], row[8], row[10] });
    }

    [Fact]
    public async Task A_shift_still_running_has_no_outcome_rows()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(8, 12, 0));
        using var _ = factory;
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Inspections"]));

        Assert.Empty(Rows(workbook.Worksheet(1)));
    }

    [Fact]
    public async Task One_building_only()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(9, 12, 0));
        using var _ = factory;
        await InspectionApi.AddBuildingBAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.BuildingBPoint, ThaiClock.At(8, 8, 30));
        await TestData.AddInspectionAsync(factory, scanId, InspectionResult.Passed, ThaiClock.At(8, 8, 40));

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Inspections"], await AdminSetupApi.BuildingIdAsync(factory, "A")));

        Assert.Empty(Rows(workbook.Worksheet(1)));
    }

    [Fact]
    public async Task The_export_log_names_the_inspection_set()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(9, 12, 0));
        using var _ = factory;

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Attendance", "Submissions", "Inspections"]));

        Assert.Equal(new[] { "การลงเวลา", "การส่งงาน", "การตรวจงาน" }, workbook.Worksheets.Select(s => s.Name));
        var audit = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        using var after = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal(
            new[] { "Attendance", "Submissions", "Inspections" },
            after.RootElement.GetProperty("Sets").EnumerateArray().Select(s => s.GetString()));
        Assert.Contains("การตรวจงาน", audit.Summary);
    }
}
```

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~InspectionExportTests"`
Expected: FAIL with 400: `Inspections` is not an `ExportSet` value, so the body does not bind.

- [ ] **Step 4: Read the history**

In `backend/src/FacilityRealtime.Application/Export/ExportRules.cs`, replace the `ExportSet` enum with:

```csharp
/// <summary>The sets the Admin ticks on wireframe D8 (facility-0053).</summary>
public enum ExportSet
{
    Attendance,
    Submissions,
    Inspections,
}
```

Create `backend/src/FacilityRealtime.Infrastructure/Persistence/InspectionHistoryQuery.cs`:

```csharp
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>One Inspection Record with its point, round, Supervisor and GPS verdict. No coordinates (facility-0053).</summary>
public sealed record InspectionExportRow(
    long Id,
    long ScanRecordId,
    int PointId,
    DateOnly ShiftDate,
    Shift Shift,
    string BuildingCode,
    string AreaCode,
    string PointName,
    string SignCode,
    TimeOnly? RoundStart,
    TimeOnly? RoundEnd,
    DateTime InspectedAtUtc,
    InspectionResult Result,
    string? Defect,
    bool IsCover,
    int SupervisorId,
    string EmployeeId,
    string DisplayName,
    short? DistanceM,
    bool? WithinRadius);

/// <summary>A point, active or not, with where it is. Counted: an active point of an active Area. WorksNight: its Area has a night shift.</summary>
public sealed record PointPlace(
    int Id, int BuildingId, string BuildingCode, string AreaCode, string PointName, string SignCode, bool Counted, bool WorksNight);

/// <summary>
/// What the export's inspection sheet and the Inspection Summary read, by Thai shift date (both ends included) and
/// optionally the point's building. In Infrastructure so a [MySqlFact] can run it on the Oracle provider.
/// </summary>
public static class InspectionHistoryQuery
{
    /// <summary>Scan Records placed in a round (not Off-Round), with the round copied onto them.</summary>
    public static async Task<List<RoundSubmission>> LoadRoundSubmissionsAsync(AppDbContext db, DateOnly first, DateOnly last, int? buildingId)
    {
        var rows = await db.ScanRecords.AsNoTracking()
            .Where(s => s.ShiftDate >= first && s.ShiftDate <= last && s.Placement != Placement.OffRound
                && s.RoundStart != null && s.RoundEnd != null
                && (buildingId == null || s.ServicePoint!.Area!.BuildingId == buildingId))
            .Select(s => new { s.Id, s.ServicePointId, s.ShiftDate, s.Shift, s.RoundStart, s.RoundEnd, s.SubmittedAt })
            .ToListAsync();
        return rows
            .Select(s => new RoundSubmission(
                s.Id,
                s.ServicePointId,
                new ShiftSlot(s.ShiftDate, s.Shift),
                s.RoundStart!.Value,
                s.RoundEnd!.Value,
                DateTime.SpecifyKind(s.SubmittedAt, DateTimeKind.Utc)))
            .ToList();
    }

    /// <summary>Inspections of the Scan Records in the range, oldest first.</summary>
    public static Task<List<InspectionExportRow>> LoadInspectionsAsync(AppDbContext db, DateOnly first, DateOnly last, int? buildingId) =>
        db.InspectionRecords.AsNoTracking()
            .Where(i => i.ScanRecord!.ShiftDate >= first && i.ScanRecord.ShiftDate <= last
                && (buildingId == null || i.ScanRecord.ServicePoint!.Area!.BuildingId == buildingId))
            .OrderBy(i => i.InspectedAt).ThenBy(i => i.Id)
            .Select(i => new InspectionExportRow(
                i.Id,
                i.ScanRecordId,
                i.ServicePointId,
                i.ScanRecord!.ShiftDate,
                i.ScanRecord.Shift,
                i.ScanRecord.ServicePoint!.Area!.Building!.Code,
                i.ScanRecord.ServicePoint.Area.Code,
                i.ScanRecord.ServicePoint.Name,
                i.ScanRecord.Sign!.Code,
                i.ScanRecord.RoundStart,
                i.ScanRecord.RoundEnd,
                i.InspectedAt,
                i.Result,
                i.Defect,
                i.CoverAssignmentId != null,
                i.SupervisorId,
                i.Supervisor!.EmployeeId ?? string.Empty,
                i.Supervisor.DisplayName,
                i.DistanceM,
                i.WithinRadius))
            .ToListAsync();

    /// <summary>Every point of the building (or of all), active or not, so deactivated points still name their history.</summary>
    public static async Task<List<PointPlace>> LoadPointPlacesAsync(AppDbContext db, int? buildingId)
    {
        var points = await db.ServicePoints.AsNoTracking()
            .Where(p => buildingId == null || p.Area!.BuildingId == buildingId)
            .Select(p => new
            {
                p.Id,
                p.Area!.BuildingId,
                BuildingCode = p.Area.Building!.Code,
                AreaCode = p.Area.Code,
                p.Name,
                Counted = p.IsActive && p.Area.IsActive,
                p.Area.ShiftPattern,
            })
            .ToListAsync();
        var ids = points.Select(p => p.Id).ToList();
        var signCodes = await db.Signs.AsNoTracking()
            .Where(s => s.ServicePointId != null && ids.Contains(s.ServicePointId.Value))
            .ToDictionaryAsync(s => s.ServicePointId!.Value, s => s.Code);

        return points
            .Select(p => new PointPlace(
                p.Id, p.BuildingId, p.BuildingCode, p.AreaCode, p.Name, signCodes[p.Id], p.Counted, p.ShiftPattern == ShiftPattern.DayAndNight))
            .ToList();
    }
}
```

- [ ] **Step 5: Write the sheet and add it to the export**

In `ThaiLabels.cs` add `using FacilityRealtime.Application.Rounds;` and:

```csharp
    /// <summary>facility-0029: ผล of an inspection on the export, as the Supervisor's buttons say it.</summary>
    public static string InspectionResultName(InspectionResult result) => result == InspectionResult.Passed ? "สะอาด" : "ต้องแก้ไข";

    /// <summary>facility-0047 rule 5: a finished round's line on the inspection sheet.</summary>
    public static string RoundOutcomeName(RoundOutcome outcome) => outcome switch
    {
        RoundOutcome.NotInspected => "ไม่ได้ตรวจ",
        RoundOutcome.NotReworked => "ไม่ได้แก้",
        _ => "ตรวจผ่าน",
    };
```

In `ExportSheets.cs` add `using FacilityRealtime.Application.Inspections;`, the constant `public const string InspectionsName = "การตรวจงาน";` next to the other two names, and this method after `Submissions`:

```csharp
    /// <summary>
    /// Every inspection, plus one row for each finished round left "ไม่ได้ตรวจ" or "ไม่ได้แก้" (facility-0047 rule 5,
    /// decision 13). A round that ended ตรวจผ่าน already shows as its inspections.
    /// </summary>
    public static ExcelSheet Inspections(
        IReadOnlyList<InspectionExportRow> inspections, IReadOnlyList<PointPlace> places, IReadOnlyList<FinishedRound> rounds)
    {
        var rows = inspections.Select(i => new SortedRow(
            i.ShiftDate,
            i.Shift,
            i.InspectedAtUtc,
            i.AreaCode + "|" + i.SignCode,
            [
                ThaiLabels.Date(i.ShiftDate),
                ThaiLabels.ShiftName(i.Shift),
                i.BuildingCode,
                i.AreaCode,
                i.PointName,
                i.SignCode,
                Round(i.RoundStart, i.RoundEnd),
                ThaiLabels.Clock(i.InspectedAtUtc),
                ThaiLabels.InspectionResultName(i.Result),
                i.Defect ?? string.Empty,
                i.IsCover ? "ตรวจแทน" : string.Empty,
                i.EmployeeId,
                i.DisplayName,
                ThaiLabels.Gps(i.DistanceM, i.WithinRadius),
            ])).ToList();

        var placeOf = places.ToDictionary(p => p.Id);
        foreach (var round in rounds.Where(r => r.Outcome != RoundOutcome.Passed))
        {
            var place = placeOf[round.PointId];
            rows.Add(new SortedRow(
                round.Slot.ShiftDate,
                round.Slot.Shift,
                ShiftCalendar.ToUtc(round.Slot, round.RoundEnd),
                place.AreaCode + "|" + place.SignCode,
                [
                    ThaiLabels.Date(round.Slot.ShiftDate),
                    ThaiLabels.ShiftName(round.Slot.Shift),
                    place.BuildingCode,
                    place.AreaCode,
                    place.PointName,
                    place.SignCode,
                    Round(round.RoundStart, round.RoundEnd),
                    string.Empty,
                    ThaiLabels.RoundOutcomeName(round.Outcome),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                ]));
        }

        return new ExcelSheet(
            InspectionsName,
            ["วันที่กะ", "กะ", "ตึก", "Area", "จุด", "ป้าย", "รอบ", "เวลาตรวจ", "ผล", "ข้อบกพร่อง", "ตรวจแทน", "รหัสพนักงาน", "ชื่อ", "GPS"],
            Sort(rows));
    }
```

In `AdminExportEndpoints.cs`:
- Add `using FacilityRealtime.Application.Inspections;` and `using FacilityRealtime.Application.Rounds;`.
- Change the class summary's first sentence to say "attendance, submissions and inspections".
- Replace everything from `var now = clock.GetUtcNow().UtcDateTime;` to the line before `return Results.File(...)` with:

```csharp
        var now = clock.GetUtcNow().UtcDateTime;
        var chosen = new List<(ExportSet Set, ExcelSheet Sheet)>();
        if (sets.Contains(ExportSet.Attendance))
        {
            chosen.Add((ExportSet.Attendance, ExportSheets.Attendance(await ExportQuery.LoadAttendanceAsync(db, first, last, request.BuildingId))));
        }

        if (sets.Contains(ExportSet.Submissions))
        {
            chosen.Add((ExportSet.Submissions, ExportSheets.Submissions(
                await ExportQuery.LoadSubmissionsAsync(db, first, last, request.BuildingId),
                await ExportQuery.LoadPointsAsync(db, request.BuildingId),
                first,
                last,
                now,
                settings.ClosesMinutesAfterShift)));
        }

        if (sets.Contains(ExportSet.Inspections))
        {
            var inspections = await InspectionHistoryQuery.LoadInspectionsAsync(db, first, last, request.BuildingId);
            var rounds = InspectionHistory.FinishedRounds(
                await InspectionHistoryQuery.LoadRoundSubmissionsAsync(db, first, last, request.BuildingId),
                inspections.Select(i => new InspectionFact(i.ScanRecordId, i.Result, i.InspectedAtUtc)).ToList(),
                now,
                settings.ClosesMinutesAfterShift);
            chosen.Add((ExportSet.Inspections, ExportSheets.Inspections(
                inspections, await InspectionHistoryQuery.LoadPointPlacesAsync(db, request.BuildingId), rounds)));
        }

        var sheets = chosen.Select(c => c.Sheet).ToList();
        var file = ExcelWorkbook.Write(sheets);
        var from = ThaiLabels.Date(first);
        var to = ThaiLabels.Date(last);
        AuditTrail.Add(
            db, admin.Id, now, "EXPORT_EXCEL", "export", null,
            $"ส่งออก Excel {string.Join(" + ", sheets.Select(s => s.Name))} วันที่ {from} ถึง {to} {(buildingCode is null ? "ทุกตึก" : "ตึก " + buildingCode)}",
            before: null,
            after: new
            {
                From = from,
                To = to,
                BuildingId = request.BuildingId,
                Sets = chosen.Select(c => c.Set.ToString()).ToList(),
                Rows = sheets.Select(s => s.Rows.Count).ToList(),
            });
        await db.SaveChangesAsync();
```

The workbook is still built before the log row is saved (plan 5 fix F2).

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~InspectionExportTests|FullyQualifiedName~ExportEndpointTests"`
Expected: PASS. The plan-5 export tests are unchanged.

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS.

- [ ] **Step 7: Update the handoff and commit**

Add under `## Task log (plan 6)`:

```markdown
- 2026-10-14 · Task 8 done: RoundOutcomes and InspectionHistory (ไม่ได้ตรวจ / ไม่ได้แก้ for closed shifts); export set Inspections writes sheet "การตรวจงาน" (inspections, outcome rows, ตรวจแทน, GPS verdict only). All tests pass. Next: Task 9 (Inspection Summary).
```

```bash
git add backend/src backend/tests docs/handoff.md
git commit -m "feat(backend): Excel inspection sheet with ไม่ได้ตรวจ / ไม่ได้แก้ rows (facility-0047, 0053)"
```

---

### Task 9: Inspection Summary (`GET /api/admin/inspection-summary`)

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Inspections/InspectionSummary.cs`
- Create: `backend/src/FacilityRealtime.Api/DTOs/InspectionSummaryDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminInspectionSummaryEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/InspectionSummaryTests.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/InspectionSummaryEndpointTests.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Admin/DeactivatedAdminTests.cs`

**Interfaces:**
- Consumes:
  - Task 8's `RoundSubmission`, `FinishedRound`, `RoundOutcome`, `InspectionHistory.FinishedRounds` and `InspectionHistoryQuery.*`.
  - `ExportRules.MaxDays`, `AdminQuery.TryReadDate` and `ThaiTime.FromUtc`.
- Produces:
  - `SummarySupervisor(int Id, int BuildingId, Shift Shift)`
  - `SummaryPoint(int Id, int BuildingId, bool Counted, bool WorksNight)`
  - `SummaryInspection(long ScanRecordId, int PointId, int SupervisorId, ShiftSlot Slot, InspectionResult Result, bool? WithinRadius)`
  - `SupervisorTally(int SupervisorId, int PointsInspected, int PointsTotal, int Submitted, int SubmittedInspected, int NotInspected, int Inspections, int Reworks, int GpsFlagged)`
  - `InspectionSummary.Tally(supervisors, points, submissions, inspections, rounds) : IReadOnlyList<SupervisorTally>` (in the order of `supervisors`)
  - `InspectionSummaryDto(DateOnly From, DateOnly To, IReadOnlyList<InspectionSummaryRowDto> Rows)` and `InspectionSummaryRowDto`.

- [ ] **Step 1: Write the failing unit tests**

Create `backend/tests/FacilityRealtime.UnitTests/InspectionSummaryTests.cs`:

```csharp
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>
/// Plan 6 decision 15. Building 1 has points 10 (Area works nights), 11 (day-only Area) and 12 (deactivated); building 2
/// has point 20. Supervisor 1 = building 1 Day, 2 = building 1 Night, 3 = building 2 Day.
/// </summary>
public class InspectionSummaryTests
{
    private static readonly ShiftSlot Day8 = new(new DateOnly(2026, 10, 8), Shift.Day);
    private static readonly ShiftSlot Night8 = new(new DateOnly(2026, 10, 8), Shift.Night);

    private static readonly SummaryPoint[] Points =
    [
        new(10, 1, true, true),
        new(11, 1, true, false),
        new(12, 1, false, true),
        new(20, 2, true, true),
    ];

    private static readonly SummarySupervisor[] Supervisors = [new(1, 1, Shift.Day), new(2, 1, Shift.Night), new(3, 2, Shift.Day)];

    private static RoundSubmission Sent(long id, int point, ShiftSlot slot) =>
        new(id, point, slot, new TimeOnly(slot.Shift == Shift.Day ? 8 : 20, 0), new TimeOnly(slot.Shift == Shift.Day ? 10 : 22, 0),
            ShiftCalendar.StartUtc(slot).AddHours(1).AddMinutes(10));

    private static SummaryInspection Inspected(long scanId, int point, int supervisor, ShiftSlot slot, InspectionResult result, bool? withinRadius = true) =>
        new(scanId, point, supervisor, slot, result, withinRadius);

    private static FinishedRound Round(int point, ShiftSlot slot, RoundOutcome outcome) =>
        new(point, slot, new TimeOnly(slot.Shift == Shift.Day ? 8 : 20, 0), new TimeOnly(slot.Shift == Shift.Day ? 10 : 22, 0), outcome);

    [Fact]
    public void Each_supervisor_gets_their_beat_and_their_own_inspections()
    {
        var tallies = InspectionSummary.Tally(
            Supervisors,
            Points,
            [Sent(1, 10, Day8), Sent(2, 11, Day8), Sent(3, 20, Day8), Sent(4, 10, Night8)],
            [
                Inspected(1, 10, 1, Day8, InspectionResult.Passed),
                Inspected(3, 20, 1, Day8, InspectionResult.Rework, withinRadius: false), // supervisor 1 covering building 2
                Inspected(4, 10, 2, Night8, InspectionResult.Passed),
            ],
            [Round(10, Day8, RoundOutcome.Passed), Round(11, Day8, RoundOutcome.NotInspected), Round(20, Day8, RoundOutcome.NotReworked), Round(10, Night8, RoundOutcome.Passed)]);

        Assert.Equal(new SupervisorTally(1, 1, 2, 2, 1, 1, 2, 1, 1), tallies[0]);
        Assert.Equal(new SupervisorTally(2, 1, 1, 1, 1, 0, 1, 0, 0), tallies[1]);
        Assert.Equal(new SupervisorTally(3, 0, 1, 1, 1, 0, 0, 0, 0), tallies[2]);
    }

    [Fact]
    public void A_deactivated_point_is_neither_counted_nor_inspected_in_the_beat()
    {
        var tallies = InspectionSummary.Tally(
            Supervisors, Points, [Sent(1, 12, Day8)], [Inspected(1, 12, 1, Day8, InspectionResult.Passed)], []);

        Assert.Equal(0, tallies[0].PointsInspected);
        Assert.Equal(2, tallies[0].PointsTotal);
        Assert.Equal(1, tallies[0].Inspections); // still one of their inspections
        Assert.Equal(1, tallies[0].Submitted);   // its history still belongs to the beat
    }

    [Fact]
    public void An_inspection_at_a_point_of_another_building_is_not_in_the_beat()
    {
        var tallies = InspectionSummary.Tally(Supervisors, [Points[0]], [], [Inspected(9, 99, 1, Day8, InspectionResult.Passed)], []);

        Assert.Equal(0, tallies[0].PointsInspected);
        Assert.Equal(1, tallies[0].Inspections);
    }
}
```

The expected tallies read as follows:
- Supervisor 1's beat (building 1, Day) has points 10 and 11, so PointsTotal = 2.
  - They inspected point 10 there (PointsInspected = 1).
  - The beat had submissions 1 and 2 (Submitted = 2), of which 1 was inspected (SubmittedInspected = 1).
  - Point 11's round is ไม่ได้ตรวจ (NotInspected = 1).
  - They did 2 inspections, 1 Rework and 1 flagged.
- Supervisor 2's beat (building 1, Night) has only point 10.
- Supervisor 3's beat (building 2, Day) holds submission 3, inspected by supervisor 1. Its round is ไม่ได้แก้, which does not count as ไม่ได้ตรวจ.

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~InspectionSummaryTests"`
Expected: build FAILS: `InspectionSummary` does not exist.

- [ ] **Step 2: Write the tally**

Create `backend/src/FacilityRealtime.Application/Inspections/InspectionSummary.cs`:

```csharp
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Inspections;

/// <summary>An active Supervisor and their beat: their own building in their own shift.</summary>
public sealed record SummarySupervisor(int Id, int BuildingId, Shift Shift);

/// <summary>A point, active or not. Counted: an active point of an active Area. WorksNight: its Area has a night shift.</summary>
public sealed record SummaryPoint(int Id, int BuildingId, bool Counted, bool WorksNight);

/// <summary>One Inspection Record; Slot is the shift of the Scan Record it judged.</summary>
public sealed record SummaryInspection(long ScanRecordId, int PointId, int SupervisorId, ShiftSlot Slot, InspectionResult Result, bool? WithinRadius);

/// <summary>One row of the Inspection Summary (wireframe SupervisorSummary). Percentages are left to the page.</summary>
public sealed record SupervisorTally(
    int SupervisorId,
    int PointsInspected,
    int PointsTotal,
    int Submitted,
    int SubmittedInspected,
    int NotInspected,
    int Inspections,
    int Reworks,
    int GpsFlagged);

/// <summary>facility-0048 rule 5: the numbers the Admin weighs a Supervisor by. The system shows them; it does not judge.</summary>
public static class InspectionSummary
{
    /// <summary>
    /// Plan 6 decision 15. Beat numbers (points, submissions, ไม่ได้ตรวจ) are about the Supervisor's own building in their
    /// own shift, whoever inspected; the inspection counts are the Supervisor's own, anywhere, covers included.
    /// </summary>
    public static IReadOnlyList<SupervisorTally> Tally(
        IReadOnlyList<SummarySupervisor> supervisors,
        IReadOnlyList<SummaryPoint> points,
        IReadOnlyList<RoundSubmission> submissions,
        IReadOnlyList<SummaryInspection> inspections,
        IReadOnlyList<FinishedRound> rounds)
    {
        var pointOf = points.ToDictionary(p => p.Id);
        var inspectedIds = inspections.Select(i => i.ScanRecordId).ToHashSet();

        return supervisors.Select(supervisor =>
        {
            bool InBeat(int pointId, Shift shift) =>
                shift == supervisor.Shift && pointOf.TryGetValue(pointId, out var point) && point.BuildingId == supervisor.BuildingId;

            var beatPoints = points
                .Where(p => p.Counted && p.BuildingId == supervisor.BuildingId && (supervisor.Shift == Shift.Day || p.WorksNight))
                .Select(p => p.Id)
                .ToHashSet();
            var own = inspections.Where(i => i.SupervisorId == supervisor.Id).ToList();
            var beatSubmissions = submissions.Where(s => InBeat(s.PointId, s.Slot.Shift)).ToList();

            return new SupervisorTally(
                supervisor.Id,
                own.Where(i => i.Slot.Shift == supervisor.Shift && beatPoints.Contains(i.PointId)).Select(i => i.PointId).Distinct().Count(),
                beatPoints.Count,
                beatSubmissions.Count,
                beatSubmissions.Count(s => inspectedIds.Contains(s.Id)),
                rounds.Count(r => r.Outcome == RoundOutcome.NotInspected && InBeat(r.PointId, r.Slot.Shift)),
                own.Count,
                own.Count(i => i.Result == InspectionResult.Rework),
                own.Count(i => i.WithinRadius == false));
        }).ToList();
    }
}
```

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests`
Expected: PASS.

- [ ] **Step 3: Write the failing API tests**

Create `backend/tests/FacilityRealtime.ApiTests/Admin/InspectionSummaryEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Inspections;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.ApiTests.Admin;

public record InspectionSummaryRowModel(
    int SupervisorId, string EmployeeId, string DisplayName, string BuildingCode, string Shift,
    int PointsInspected, int PointsTotal, int Submitted, int SubmittedInspected, int NotInspected, int Inspections, int Reworks, int GpsFlagged);

public record InspectionSummaryModel(DateOnly From, DateOnly To, List<InspectionSummaryRowModel> Rows);

/// <summary>facility-0048 rule 5, wireframe SupervisorSummary: the Admin's Inspection Summary tab.</summary>
public class InspectionSummaryEndpointTests
{
    private const string Path = "/api/admin/inspection-summary";

    private static async Task<(FacilityApiFactory Factory, HttpClient Admin)> StartAsync()
    {
        var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.BackdateSetupAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(9, 12, 0));
        return (factory, admin);
    }

    [Fact]
    public async Task Counts_each_supervisors_beat_and_own_inspections()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        var mens = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, mens, InspectionResult.Passed, ThaiClock.At(8, 8, 20), withinRadius: true);
        await TestData.AddScanAsync(factory, "token-restroom-f1", ThaiClock.At(8, 8, 15));
        var meeting = await TestData.AddScanAsync(factory, "token-meeting-room", ThaiClock.At(8, 8, 30));
        await TestData.AddInspectionAsync(factory, meeting, InspectionResult.Rework, ThaiClock.At(8, 8, 40), withinRadius: false, distanceM: 120);

        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>($"{Path}?from=2026-10-08&to=2026-10-08"))!;

        Assert.Equal(new DateOnly(2026, 10, 8), summary.From);
        Assert.Equal(new[] { "S2001", "S2002" }, summary.Rows.Select(r => r.EmployeeId));
        var day = summary.Rows[0];
        Assert.Equal(("A", "Day"), (day.BuildingCode, day.Shift));
        Assert.Equal(
            new[] { 2, 3, 3, 2, 1, 2, 1, 1 },
            new[] { day.PointsInspected, day.PointsTotal, day.Submitted, day.SubmittedInspected, day.NotInspected, day.Inspections, day.Reworks, day.GpsFlagged });
        var night = summary.Rows[1];
        Assert.Equal(
            new[] { 0, 2, 0, 0, 0, 0, 0, 0 },
            new[] { night.PointsInspected, night.PointsTotal, night.Submitted, night.SubmittedInspected, night.NotInspected, night.Inspections, night.Reworks, night.GpsFlagged });
    }

    [Fact]
    public async Task The_default_range_is_the_last_seven_shift_dates()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>(Path))!;

        Assert.Equal((new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 9)), (summary.From, summary.To));
    }

    [Theory]
    [InlineData("?from=2026-10-08")]
    [InlineData("?from=2026-10-09&to=2026-10-08")]
    [InlineData("?from=2026-09-01&to=2026-10-08")]
    [InlineData("?from=08-10-2026&to=2026-10-08")]
    public async Task A_bad_range_is_400(string query)
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        var response = await admin.GetAsync(Path + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task One_building_lists_only_its_supervisors()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddSupervisorAsync(factory, "S2003", "0820000003", "B", Shift.Day);
        var buildingB = await AdminSetupApi.BuildingIdAsync(factory, "B");

        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>($"{Path}?buildingId={buildingB}"))!;

        var row = Assert.Single(summary.Rows);
        Assert.Equal(("S2003", "B", 1), (row.EmployeeId, row.BuildingCode, row.PointsTotal));
    }

    [Theory]
    [InlineData("cleaner")]
    [InlineData("supervisor")]
    public async Task Only_admins_see_the_summary(string who)
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = who == "cleaner"
            ? await AuthApi.LoggedInAsync(factory)
            : await AuthApi.LoggedInAsEmployeeAsync(factory, "S2001", "0820000001");

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

In `DeactivatedAdminTests.cs` add the line:

```csharp
    [InlineData("GET", "/api/admin/inspection-summary", null)]
```

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~InspectionSummaryEndpointTests|FullyQualifiedName~DeactivatedAdminTests"`
Expected: FAIL. The route answers 404.

- [ ] **Step 4: Write the endpoint**

Create `backend/src/FacilityRealtime.Api/DTOs/InspectionSummaryDtos.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>One Supervisor's line (plan 6 decision 15). The page shows PointsInspected "of" PointsTotal and the percentage inspected.</summary>
public record InspectionSummaryRowDto(
    int SupervisorId,
    string EmployeeId,
    string DisplayName,
    string BuildingCode,
    Shift Shift,
    int PointsInspected,
    int PointsTotal,
    int Submitted,
    int SubmittedInspected,
    int NotInspected,
    int Inspections,
    int Reworks,
    int GpsFlagged);

/// <summary>facility-0048 rule 5: From and To are the Thai shift dates covered, both included.</summary>
public record InspectionSummaryDto(DateOnly From, DateOnly To, IReadOnlyList<InspectionSummaryRowDto> Rows);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminInspectionSummaryEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Application.Export;
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0048 rule 5 (wireframe SupervisorSummary): the Admin's tab of each Supervisor's inspections. Read-only, so
/// nothing is logged. By shift date, the last 7 by default, at most 31 like the export.
/// </summary>
public static class AdminInspectionSummaryEndpoints
{
    public const int DefaultDays = 7;

    public static IEndpointRouteBuilder MapAdminInspectionSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/inspection-summary", GetAsync).RequireAuthorization(AuthSetup.AdminOnly);
        return app;
    }

    private static async Task<IResult> GetAsync(
        string? from, string? to, int? buildingId, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        DateOnly first, last;
        if (from is null && to is null)
        {
            last = DateOnly.FromDateTime(ThaiTime.FromUtc(now));
            first = last.AddDays(1 - DefaultDays);
        }
        else if (!AdminQuery.TryReadDate(from, out first) || !AdminQuery.TryReadDate(to, out last))
        {
            return Bad("ต้องระบุทั้งวันเริ่ม (from) และวันสุดท้าย (to) แบบ yyyy-MM-dd");
        }

        if (last < first)
        {
            return Bad("วันสุดท้ายต้องไม่ก่อนวันเริ่ม");
        }

        if (last.DayNumber - first.DayNumber + 1 > ExportRules.MaxDays)
        {
            return Bad($"เลือกได้ไม่เกิน {ExportRules.MaxDays} วัน");
        }

        var supervisors = await db.Users.AsNoTracking()
            .Include(u => u.Building)
            .Where(u => u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId != null && u.Shift != null
                && (buildingId == null || u.BuildingId == buildingId))
            .ToListAsync();
        var places = await InspectionHistoryQuery.LoadPointPlacesAsync(db, buildingId);
        var submissions = await InspectionHistoryQuery.LoadRoundSubmissionsAsync(db, first, last, buildingId);
        // A Supervisor's own inspections count wherever they were, covers included, so these are not filtered by building
        var inspections = await InspectionHistoryQuery.LoadInspectionsAsync(db, first, last, buildingId: null);
        var rounds = InspectionHistory.FinishedRounds(
            submissions,
            inspections.Select(i => new InspectionFact(i.ScanRecordId, i.Result, i.InspectedAtUtc)).ToList(),
            now,
            settings.ClosesMinutesAfterShift);

        var tallies = InspectionSummary.Tally(
            supervisors.Select(u => new SummarySupervisor(u.Id, u.BuildingId!.Value, u.Shift!.Value)).ToList(),
            places.Select(p => new SummaryPoint(p.Id, p.BuildingId, p.Counted, p.WorksNight)).ToList(),
            submissions,
            inspections.Select(i => new SummaryInspection(i.ScanRecordId, i.PointId, i.SupervisorId, new ShiftSlot(i.ShiftDate, i.Shift), i.Result, i.WithinRadius)).ToList(),
            rounds);

        var byId = supervisors.ToDictionary(u => u.Id);
        var rows = tallies
            .Select(t =>
            {
                var supervisor = byId[t.SupervisorId];
                return new InspectionSummaryRowDto(
                    supervisor.Id,
                    supervisor.EmployeeId ?? string.Empty,
                    supervisor.DisplayName,
                    supervisor.Building!.Code,
                    supervisor.Shift!.Value,
                    t.PointsInspected,
                    t.PointsTotal,
                    t.Submitted,
                    t.SubmittedInspected,
                    t.NotInspected,
                    t.Inspections,
                    t.Reworks,
                    t.GpsFlagged);
            })
            .OrderBy(r => r.BuildingCode, StringComparer.Ordinal)
            .ThenBy(r => r.Shift)
            .ToList();

        return Results.Ok(new InspectionSummaryDto(first, last, rows));
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
```

In `Program.cs` add `app.MapAdminInspectionSummaryEndpoints();` after `app.MapAdminExportEndpoints();`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~InspectionSummaryEndpointTests|FullyQualifiedName~DeactivatedAdminTests"`
Expected: PASS.

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: PASS.

- [ ] **Step 6: Update the handoff and commit**

Add under `## Task log (plan 6)`:

```markdown
- 2026-10-14 · Task 9 done: GET /api/admin/inspection-summary (per active Supervisor: beat points inspected of total, submissions and how many were inspected, ไม่ได้ตรวจ rounds, own inspections, reworks and GPS flags; 7 shift dates by default, at most 31). All tests pass. Next: Task 10 (MySQL check and docs).
```

```bash
git add backend/src backend/tests docs/handoff.md
git commit -m "feat(backend): Admin's Inspection Summary per Supervisor (facility-0048)"
```

---

### Task 10: MySQL check, docs, and the controller's real-database run

**Files:**
- Test: `backend/tests/FacilityRealtime.ApiTests/Persistence/MySqlSupervisorTests.cs`
- Modify: `README.md`
- Modify: `docs/architecture.md`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: Task 8's `InspectionHistoryQuery.*`, Task 7's `AlertQuery.LoadAsync`, Task 4's `RoundFactsQuery` defect, and Task 1's schema.

- [ ] **Step 1: Write the MySQL test**

Create `backend/tests/FacilityRealtime.ApiTests/Persistence/MySqlSupervisorTests.cs`:

```csharp
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>
/// Plan 6 on the Oracle MySQL provider: the one-place CHECK on cover_assignments, nullable area_id/building_id compared
/// with parameters (the duplicate-cover check), the inspection history joins, the alert list's inspection flags and a
/// Supervisor's Blocked Scan, and the defect read with the round facts. SQLite hides how MySQL treats these.
/// </summary>
[Collection("MySQL")]
public class MySqlSupervisorTests
{
    [MySqlFact]
    public async Task Supervisor_queries_and_rules_run_on_MySQL()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(MySqlTestDatabase.ConnectionString())
            .Options;

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.EnsureCreatedAsync();
        }

        try
        {
            var oct8 = new DateOnly(2026, 10, 8);
            var at = new DateTime(2026, 10, 8, 1, 10, 0, DateTimeKind.Utc); // 08:10 Thai
            int supervisorId, buildingAId, buildingBId, pointId;
            await using (var write = new AppDbContext(options))
            {
                await DbInitializer.SeedAsync(write, new Pbkdf2PasswordHasher(iterations: 1_000));
                var cleaner = await write.Users.SingleAsync(u => u.EmployeeId == "E1001");
                var supervisor = await write.Users.SingleAsync(u => u.EmployeeId == "S2001");
                var admin = await write.Users.SingleAsync(u => u.Username == "admin");
                var sign = await write.Signs.SingleAsync(s => s.Code == "AR01-01");
                var window = (await write.PointRoundWindows.Where(w => w.ServicePointId == sign.ServicePointId && w.Shift == Shift.Day).ToListAsync())
                    .Single(w => w.StartTime == new TimeOnly(7, 0));
                var buildingB = new Building { Code = "B", Name = "ตึก B", CreatedAt = at };
                write.Buildings.Add(buildingB);
                await write.SaveChangesAsync();

                var cover = new CoverAssignment
                {
                    UserId = supervisor.Id, BuildingId = buildingB.Id, ShiftDate = oct8, Shift = Shift.Day, AssignedById = admin.Id, AssignedAt = at,
                };
                var scan = new ScanRecord
                {
                    ServicePointId = sign.ServicePointId!.Value, SignId = sign.Id, UserId = cleaner.Id, ShiftDate = oct8, Shift = Shift.Day,
                    RoundWindowId = window.Id, RoundStart = window.StartTime, RoundEnd = window.EndTime, Placement = Placement.OnTime, SubmittedAt = at,
                };
                write.CoverAssignments.Add(cover);
                write.ScanRecords.Add(scan);
                await write.SaveChangesAsync();
                write.InspectionRecords.Add(new InspectionRecord
                {
                    ScanRecordId = scan.Id, ServicePointId = scan.ServicePointId, SupervisorId = supervisor.Id, Result = InspectionResult.Rework,
                    Defect = "พื้นยังเปียก", InspectedAt = at.AddMinutes(10), CoverAssignmentId = cover.Id,
                    Latitude = 13.800000m, Longitude = 100.600000m, AccuracyM = 10, DistanceM = 12000, WithinRadius = false,
                });
                write.BlockedScans.Add(new BlockedScan
                {
                    UserId = supervisor.Id, SignId = sign.Id, Reason = BlockReason.OutsideShift, ScannedAt = at.AddHours(12), DistanceM = 5, WithinRadius = true,
                });
                await write.SaveChangesAsync();

                // CHECK ck_cover_assignments_one_place: neither an Area nor a building is refused
                write.CoverAssignments.Add(new CoverAssignment
                {
                    UserId = supervisor.Id, ShiftDate = oct8, Shift = Shift.Night, AssignedById = admin.Id, AssignedAt = at,
                });
                await Assert.ThrowsAsync<DbUpdateException>(() => write.SaveChangesAsync());

                supervisorId = supervisor.Id;
                buildingAId = supervisor.BuildingId!.Value;
                buildingBId = buildingB.Id;
                pointId = scan.ServicePointId;
            }

            await using (var read = new AppDbContext(options))
            {
                // The duplicate-cover check compares nullable columns with parameters, as CoverAssignmentEndpoints does
                int? noArea = null;
                int? building = buildingBId;
                Assert.True(await read.CoverAssignments.AnyAsync(c =>
                    c.UserId == supervisorId && c.AreaId == noArea && c.BuildingId == building && c.ShiftDate == oct8 && c.Shift == Shift.Day && c.CancelledAt == null));

                var submissions = await InspectionHistoryQuery.LoadRoundSubmissionsAsync(read, oct8, oct8, buildingAId);
                Assert.Equal(new TimeOnly(7, 0), Assert.Single(submissions).RoundStart);

                var inspection = Assert.Single(await InspectionHistoryQuery.LoadInspectionsAsync(read, oct8, oct8, buildingAId));
                Assert.Equal(("S2001", true, false, "พื้นยังเปียก"), (inspection.EmployeeId, inspection.IsCover, inspection.WithinRadius!.Value, inspection.Defect));
                Assert.Equal("AR01-01", inspection.SignCode);
                Assert.Empty(await InspectionHistoryQuery.LoadInspectionsAsync(read, oct8, oct8, buildingBId));

                var places = await InspectionHistoryQuery.LoadPointPlacesAsync(read, buildingAId);
                Assert.Equal(3, places.Count);
                Assert.True(places.Single(p => p.SignCode == "AR01-01").WorksNight);

                var facts = (await RoundFactsQuery.LoadAsync(read, [pointId], new ShiftSlot(oct8, Shift.Day)))[pointId];
                Assert.Equal("พื้นยังเปียก", Assert.Single(facts.Inspections).Defect);

                var from = new DateTime(2026, 10, 7, 17, 0, 0, DateTimeKind.Utc); // 8 Oct 00:00 Thai
                var alerts = await AlertQuery.LoadAsync(read, from, from.AddDays(1), at.AddHours(13), new AttendanceSettings());
                Assert.Contains(alerts, a => a.Kind == AlertKind.InspectionGps && a.Person.EmployeeId == "S2001" && a.Sign!.Code == "AR01-01");
                var blocked = Assert.Single(alerts, a => a.Kind == AlertKind.BlockedScan);
                Assert.Equal(BlockReason.OutsideShift, blocked.BlockReason);
                Assert.Equal(new ShiftSlot(oct8, Shift.Night), blocked.CoverSlot);
            }
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
```

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~MySqlSupervisorTests"`
Expected: it compiles and is reported as **skipped**, because `FACILITY_MYSQL_TEST` is not set. A subagent stops here; the controller runs it in Step 4.

- [ ] **Step 2: Update the README**

In `README.md`, after the paragraph that starts `ลองใน Swagger (\`/swagger\`): แม่บ้านต้องลงเวลาเข้างานก่อนส่งงาน`, add:

```markdown
หัวหน้าตรวจงานใน Swagger (login ด้วย `S2001` / `0820000001` หัวหน้าตึก A กะเช้า, ADR facility-0029, 0047, 0048, 0057):

- `POST /api/inspections/scan` สแกนป้ายจุด เช่น `{ "qrToken": "token-restroom-m1", "latitude": 13.7563, "longitude": 100.5018, "accuracyM": 10 }` ได้การ์ดของจุด งานที่ส่งล่าสุดของรอบปัจจุบัน (`submission`) หรือเหตุผลที่ยังตรวจไม่ได้ (`refusal`) จุดในตึกอื่นหรือสแกนนอกกะของตัวเอง (ตามนาฬิกา เช้า 07:00–19:00 ดึก 19:00–07:00) ถูกบล็อกและลงรายการที่ขึ้นเตือน เว้นแต่ Admin มอบหมายตรวจแทนไว้
- `POST /api/inspections` บันทึกผล `{ "qrToken": …, "scanRecordId": <submission.id>, "result": "Passed" | "Rework", "defect": "…", "latitude": …, "longitude": …, "accuracyM": … }` เลือก `Rework` (ไม่สะอาด) ต้องมี `defect` เลือก `Passed` ห้ามส่ง `defect` ถ้าแม่บ้านส่งงานใหม่หลังสแกน ได้ 409 ให้สแกนใหม่
- `GET /api/my-building` ทุกจุดในตึกของตัวเอง (และตึกที่ได้รับมอบหมายกะนี้) จุดรอตรวจขึ้นก่อน เปิดนอกกะได้แต่ `canInspect` เป็น false
- Hub `/hubs/scan` ส่ง `ScanRecorded` และ `InspectionRecorded` ให้ Admin และหัวหน้าของตึกนั้น
```

In the Admin list (the bullet for `POST /api/admin/cover-assignments`), replace it with:

```markdown
- `POST /api/admin/cover-assignments` มอบหมายแม่บ้านทำแทน Area (`areaId`) หรือหัวหน้าตรวจแทนตึก (`buildingId`, กะไหนก็ได้ยกเว้นตึกและกะของตัวเอง) ใส่ `blockedScanId` เพื่อมอบหมายจากการสแกนที่ถูกบล็อก (ADR facility-0041, 0048)
```

In the accounts and export list, replace the `POST /api/admin/export` bullet with:

```markdown
- `POST /api/admin/export` ได้ไฟล์ `.xlsx` เช่น `{ "from": "2026-10-01", "to": "2026-10-07", "buildingId": 1, "sets": ["Attendance", "Submissions", "Inspections"] }` เลือกได้ 1–31 วัน ไม่ใส่ `buildingId` คือทุกตึก ชีต "การส่งงาน" มีแถว "ไม่ได้ทำ" ชีต "การตรวจงาน" มีแถว "ไม่ได้ตรวจ" และ "ไม่ได้แก้" ของกะที่ปิดแล้ว ทุกการส่งออกลงตาราง `audit_log`
- `GET /api/admin/inspection-summary?from=&to=&buildingId=` สรุปการตรวจของหัวหน้าแต่ละคน (ค่าเริ่ม 7 วันล่าสุด เลือกได้ไม่เกิน 31 วัน) อ่านอย่างเดียว (ADR facility-0048)
```

- [ ] **Step 3: Update the architecture document**

In `docs/architecture.md`:
1. In the component diagram (section 2):
   - In the `Ep[...]` label, add `/api/inspections/scan | /api/inspections | /api/my-building` and `/api/admin/inspection-summary`.
   - Change the `Hub[...]` label's last line to `ScanRecorded, InspectionRecorded → Admins + the building's Supervisors; AttendanceRecorded → Admins`.
   - Next to the `Ep -->|"Clients.Group(admins)"| Hub` edge, add `Ep -->|"Clients.Users(building Supervisors)"| Hub`.
2. In the note under the diagram that lists endpoint classes, add: "Plan 6 adds `InspectionEndpoints`, `MyBuildingEndpoints` and `AdminInspectionSummaryEndpoints`. `PointNotifications` sends point changes to the Admins and to the building's Supervisors, addressed by account id (`Hubs/SubjectUserIdProvider`)."
3. In the class diagram (section 5):
   - Add these lines to `InspectionRecord` after `+DateTime InspectedAt`: `+int? CoverAssignmentId`, `+decimal? Latitude`, `+decimal? Longitude`, `+short? AccuracyM`, `+short? DistanceM`, `+bool? WithinRadius`.
   - In `CoverAssignment`, change `+int AreaId` to `+int? AreaId «or BuildingId»` and add `+int? BuildingId «or AreaId»`.
   - Add the relation lines `Building "0..1" <-- "0..*" CoverAssignment : Supervisor cover` and `CoverAssignment "0..1" <-- "0..*" InspectionRecord : under cover`.
4. In the paragraph after the class diagram, add after the Cover Assignment sentence: "A Supervisor's Cover Assignment names a building instead of an Area, for one shift by the clock (facility-0048); a CHECK keeps exactly one of the two. An Inspection Record keeps the GPS of the inspection; outside the sign's radius it is the `INSPECTION_GPS` alert."

- [ ] **Step 4 (controller only): Run against real MySQL**

Ask the owner for the MySQL root password. Never write it into a file, this plan, the handoff or memory. Then, from the repository root, replacing `<pw>` only on the command line:

```bash
# 1. The migration applies to an empty throwaway database
dotnet ef database update --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api \
  --connection "Server=localhost;Port=3306;Database=facility_supervisor_test;Uid=root;Pwd=<pw>;CharSet=utf8mb4;"
mysql -uroot -p -e "SHOW CREATE TABLE facility_supervisor_test.cover_assignments\G" | grep -i "ck_cover_assignments_one_place"
mysql -uroot -p -e "DROP DATABASE facility_supervisor_test"

# 2. The whole suite, MySQL tests included
FACILITY_MYSQL_TEST="Server=localhost;Port=3306;Database=facility_supervisor_test;Uid=root;Pwd=<pw>;CharSet=utf8mb4;" dotnet test backend/FacilityRealtime.slnx
```

Expected:
1. `database update` ends with `Done.`, and the grep prints the CHECK line.
2. Every test passes, 0 skipped. The MySQL tests (now 5) drop their own database at the end.

Then a short run against the real API on a throwaway database:
- Run `ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=facility_smoke_test;Uid=root;Pwd=<pw>;CharSet=utf8mb4;" dotnet run --project backend/src/FacilityRealtime.Api --urls http://localhost:5225`. Development migrates the database and seeds it.
- In a second terminal, run the script below with `python3 -X utf8 <scratchpad>/smoke.py`. The script lives in your scratchpad, not in the repo.
- Afterwards stop the API and run `mysql -uroot -p -e "DROP DATABASE facility_smoke_test"`.

```python
import json, urllib.request, urllib.error

BASE = "http://localhost:5225"

def call(method, path, body=None, token=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    data = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request(BASE + path, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(request) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()

def login(body):
    status, raw = call("POST", "/api/auth/login", body)
    assert status == 200, (status, raw)
    return json.loads(raw)["accessToken"]

supervisor = login({"employeeId": "S2001", "phone": "0820000001"})
admin = login({"username": "admin", "password": "admin1234"})
gps = {"latitude": 13.7563, "longitude": 100.5018, "accuracyM": 10}

status, raw = call("GET", "/api/my-building", token=supervisor)
assert status == 200, (status, raw)
print("my-building", json.loads(raw)["shift"], [b["buildingCode"] for b in json.loads(raw)["buildings"]])

# S2001 is the Day Supervisor: by day the scan answers 200, at night it is a Blocked Scan (403). Both are correct.
status, raw = call("POST", "/api/inspections/scan", {"qrToken": "token-restroom-m1", **gps}, supervisor)
assert status in (200, 403), (status, raw)
print("scan", status, json.loads(raw).get("refusal") or json.loads(raw).get("reason"))

status, raw = call("GET", "/api/admin/alerts", token=admin)
assert status == 200, (status, raw)
print("alerts", [a["kind"] for a in json.loads(raw)])

buildings = [b["id"] for b in json.loads(call("GET", "/api/admin/buildings", token=admin)[1])]
users = json.loads(call("GET", "/api/admin/accounts", token=admin)[1])
s2001 = next(u["id"] for u in users if u["loginName"] == "S2001")
import datetime
today = (datetime.datetime.utcnow() + datetime.timedelta(hours=7)).date().isoformat()
status, raw = call("POST", "/api/admin/cover-assignments", {"userId": s2001, "buildingId": buildings[0], "shiftDate": today, "shift": "Night"}, admin)
assert status in (201, 400), (status, raw)  # 400 only when tonight's shift has already ended
print("cover", status)

status, raw = call("GET", "/api/admin/inspection-summary", token=admin)
assert status == 200, (status, raw)
print("summary", [(r["employeeId"], r["pointsTotal"]) for r in json.loads(raw)["rows"]])

status, raw = call("POST", "/api/admin/export", {"from": today, "to": today, "sets": ["Attendance", "Submissions", "Inspections"]}, admin)
assert status == 200 and raw[:2] == b"PK", (status, raw[:200])
print("export ok", len(raw), "bytes")
```

Expected: every assert holds and each line prints. If the script and the API disagree on a field name, fix the script, never the API.

- [ ] **Step 5: Update the handoff and commit**

In `docs/handoff.md`:
- Status table: change the status cell of plan 6's row to: done, not merged (branch `feat/supervisor-inspection`).
- Add under `## Task log (plan 6)`:

```markdown
- 2026-10-14 · Task 10 done: MySqlSupervisorTests (cover CHECK, nullable cover columns compared with parameters, inspection history joins, inspection and Supervisor alerts, defect in the round facts); README and architecture docs. On the Mac with MySQL: migration applied to a throwaway database, whole suite passed with 0 skipped, smoke script against the real API passed; throwaway databases dropped. Next: the whole-branch review, then a PR for the owner.
```

- Replace the "Pending work" bullet that starts "Supervisor plan:" with nothing (plan 6 covers it).
- Add to "Notes for the frontend plan":
  - "Supervisor scan: call `POST /api/inspections/scan` when the page opens. Send `submission.id` back as `scanRecordId`, and on 409 scan again."
  - "`CoverAssignmentDto.cleanerName` is now `personName`. A cover has either `areaId`/`areaCode` or `buildingId`/`buildingCode`. `CoverSuggestionDto` likewise has `areaId` or `buildingId`."
  - "Alerts carry `blockReason` (`OtherArea`, `OtherBuilding`, `OutsideShift`) and the new kind `InspectionGps`."
  - "Cards in ต้องแก้ไข carry `rework { defect, requestedAt }`."
  - "Cleaners still get no hub events. My Work reloads on open."

```bash
git add backend/tests README.md docs/architecture.md docs/handoff.md
git commit -m "test(backend): Supervisor queries on MySQL; docs for inspections, My Building, covers, alerts, export and summary"
```

---

## Notes for the executor

- Tasks 3–9 run `dotnet test backend/FacilityRealtime.slnx` before committing. If an earlier test breaks, fix the cause in this task's code. Change an old test only when this plan names it.
- The names that cross tasks are listed in each task's **Interfaces** block. If you need to rename one, stop and report: a later task's code depends on the exact name.
