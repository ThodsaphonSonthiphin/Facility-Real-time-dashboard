# Admin Attendance, Alerts and History (Backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Admin the three Dashboard pages that watch the shift: the attendance page (D2) with corrections that need a reason, the alert list (D3) with "คุยแล้ว ปิดเตือน" and "assign cover" straight from a Blocked Scan, and the read-only Admin history page (D9).

**Architecture:**
- **Pure rules in `FacilityRealtime.Application`, unit-tested:**
  - `AttendanceCalendar.PlaceTime` turns the Admin's typed "HH:mm" into the right moment of a shift.
  - `AttendanceRules.CheckTimes` and `MissingAtShiftEnd` check the order of events and find what a Cleaner forgot to scan.
  - `AreaAttendance.StateOf` gives an Area's state on the board.
  - `AlertKinds` and `AuditCategories` map alert kinds and audit actions.
- **One new table:** `alert_reviews`, with migration `AlertReviews`.
- **Database reads where MySQL could behave differently from SQLite:** date ranges, nullable flags and JSON columns. These live in Infrastructure as `AlertQuery` and `AuditLogQuery`, like `PointBoardQuery`, so a `[MySqlFact]` can run them on real MySQL.
- **Endpoint files:** three new ones under `/api/admin` (`AdminAttendanceEndpoints`, `AdminAlertEndpoints`, `AdminAuditLogEndpoints`), plus `CoverAssignmentEndpoints`, which gains `blockedScanId` and a candidates list.
- **Live updates:** the board gets a SignalR event `AttendanceRecorded` sent to Admins.

**Tech Stack:** .NET 10 Minimal API, EF Core 10 (`MySql.EntityFrameworkCore` 10.0.9 at runtime, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 in tests), SignalR, xUnit 2.9.3.

**Spec:**
- `docs/adr/facility-0050-check-in-confirms-area-presence-not-time-attendance.md`: attendance shows whether someone is in the Area. It never counts late or early, and Areas with nobody are listed first on D2.
- `docs/adr/facility-0051-admin-corrections-and-audit-log.md`:
  - The Admin adds or edits attendance with a required reason. The row carries "แก้โดย Admin" and the old value stays in the log.
  - A forgotten Shift-Out or Break-In stays empty and appears on D3 once the shift is over.
  - Every Admin change is logged, closing an alert included.
- `docs/adr/facility-0057-supervisor-building-page-and-audit-log-page.md`, rule 2: the "ประวัติ Admin" tab is Admin only and read-only. It filters by date range, actor and type, shows before and after with the reason, and never shows a phone number or password.
- `docs/adr/facility-0041-scan-only-own-area-with-cover-assignment.md`: Blocked Scans appear on D3, and the Admin assigns a per-shift cover from that list.
- `docs/adr/facility-0035-per-building-check-in-gps-flag-not-block.md` and `facility-0037-gps-for-every-scan-replaces-nfc.md`: a scan outside the radius is kept but flagged, for the Admin to talk to the person.
- `docs/adr/facility-0027-attendance-board-dashboard-tab-and-capacity.md`: Admin only, building filter, and rule 5 `AttendanceRecorded` over `/hubs/scan`. Rule 3 is amended by 0040: count per Area per shift.
- `docs/adr/facility-0031-four-stage-shift-attendance-lifecycle.md`: the four events and the states working, on break, done and not in.
- `docs/adr/facility-0069-attendance-scan-window-60-minutes-around-the-shift.md`: attendance belongs to the person's regular shift, inside 60 minutes before to 60 minutes after it.
- `docs/adr/facility-0058-dashboard-is-admin-only.md` and `facility-0061-dashboard-keeps-30s-poll-beside-push.md`: Admin only, and the page polls as well as listening.
- `docs/design/database.html`: tables `shift_attendances`, `blocked_scans`, `alert_reviews` (`UNIQUE (alert_type, record_id)`) and `audit_log`.
- Wireframes in `docs/design/wireframe/`: `BoardAttendance.dc.html` (D2), `BoardFlags.dc.html` (D3) and `AuditLog.dc.html` (D9).

## Global Constraints

- **Branch and merging:** work on branch `feat/attendance-alerts-history`, created from `master` at `7ff6739`. Do not merge it.
- **One migration only:** Task 2 adds `AlertReviews`, which creates table `alert_reviews`. If any other task seems to need a schema change, stop and report instead of adding a migration.
- **Endpoint security:** every new endpoint is under `/api/admin` and uses `RequireAuthorization(AuthSetup.AdminOnly)`, which gives 401 without login and 403 for Cleaner and Supervisor. Each one loads the actor with `CurrentUser.LoadAsync` and answers 401 when that returns null.
- **Audit log (facility-0051):**
  - Every change writes one `audit_log` row in the same save or the same explicit transaction as the change.
  - Saving something unchanged writes nothing.
  - Pass anonymous objects as `before`/`after`, never entities.
  - Never put a QR Token, phone number or password in the log, or in any response of these pages. Alerts show a sign's code (`AR01-IN`), never its token.
  - An attendance correction always carries its reason in `audit_log.reason`.
- **No late or early (facility-0050):** nothing in this plan computes, returns or names lateness for attendance.
- **Times:**
  - Stored DATETIMEs are UTC. Responses mark them UTC so the JSON ends in `Z`.
  - The Admin types attendance times as Thai wall-clock `"HH:mm"`.
  - `shiftDate` and the `from`/`to` query values are Thai calendar dates written `yyyy-MM-dd`.
  - Day shift is 07:00–19:00, Night is 19:00–07:00, and a night shift belongs to the date it started.
- **Error format:** errors answer with `ApiResults.Message(status, "<Thai text>")`, the JSON `{ "message": ... }` that the Admin pages show as is.
- **Runtime provider:** it is Oracle's `MySql.EntityFrameworkCore`, and SQLite tests cannot catch provider-only failures.
  - Task 7 adds a `[MySqlFact]` that runs when `FACILITY_MYSQL_TEST` is set.
  - MySQL 8.0 runs on this laptop as service `MySQL80`. Only use throwaway databases (`facility_*_test`), never `facility_dashboard`.
  - Ask the developer for the root password each time. Type it only into the command, and never write it into any file, this plan included, or into memory.
- **This machine (Windows):**
  - Write files with Thai text using the editor tools, not a shell heredoc, which breaks the Thai text here.
  - Stop any API from this repo before building, because Windows locks its DLLs.
- **Public repo:** test data uses made-up names and `00000000xx`-style phone numbers only.
- **Earlier rules stay:**
  - Keep every rule from PR #50, #51 and #52: auth, throttle, the Admin-only Dashboard without QR Tokens, scan and attendance rules, Admin setup.
  - Covers are only in the covering Cleaner's own regular shift.
  - Only Cleaners submit work.

### Decisions made while planning (report them to the owner in the PR)

1. **The board shows each Area's current regular Cleaner**, also when looking at a past shift. A Cleaner moved to another Area since then shows under the new Area.
2. **The board defaults to the shift on the clock**, as the status page (D1) does. At 06:30 that is still last night's shift. Any shift can be asked for with `shiftDate` and `shift`.
3. **Board counts:**
   - "มีคนแล้ว" counts Working, OnBreak and Done.
   - "มีคนทำแทน" counts Areas whose regular Cleaner has not checked in but have a cover.
   - Required = Present + Empty + Covered. Day-only Areas are left out of the night board, and deactivated Areas are left out of every board.
4. **One endpoint adds or moves an attendance event.**
   - It is allowed only in the Cleaner's regular shift.
   - The time must lie inside that shift's attendance window (ADR 0069) and not in the future. Events stay in order: a Break-In needs a Break-Out, Break-Out and Shift-Out need a Shift-In, and times are strictly increasing.
   - A moved scan keeps its sign and GPS verdict and gets the AdminEdit badge. An added event has no sign and no GPS (AdminAdd).
   - The Admin cannot delete an event.
5. **"ไม่ได้ลงเวลาเลิกงาน" appears when the shift ends (19:00 or 07:00)**, not when the attendance window closes an hour later. It also lists a forgotten Break-In.
   - It cannot be closed by "คุยแล้ว". It goes away when the Admin fills the time.
   - A Cleaner who never checked in is not on this list; the board shows the Area as Empty instead.
6. **The alert list covers a range of Thai dates**, by default yesterday and today, at most 31 days. The page itself splits it into "ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด" and by type.
7. **Assigning a cover from a Blocked Scan also closes that alert.** It writes two log rows, `COVER_ASSIGN` and `ALERT_REVIEW`, unless the alert was already closed.
8. **The note on "คุยแล้ว ปิดเตือน" is optional** and is stored as the log row's reason.
9. **An alert shows the sign's radius as it is now.** The verdict stored with the scan was made with the radius of that time.
10. **The cover candidates leave out Cleaners without an Area**, because they have no sign to check in at. They also leave out Cleaners who already cover that Area.
11. **The history page:**
    - Newest first, at most 500 rows a page (default 100). Older pages come with `beforeId`.
    - Default range is the last 7 days, at most 92. Opening the page is not logged.
    - Refusing UPDATE/DELETE on `audit_log` to the app's MySQL user is a deployment step (ADR 0057), not code.
12. **`AttendanceRecorded`** goes to Admins after every recorded scan and every correction, carrying `{ areaId, shiftDate, shift }`. The page then reloads that board, and it still polls every 30 s (ADR 0061).
13. **No `INSPECTION_GPS` alert yet:** Inspection Records have no GPS columns until the Supervisor plan.

### Out of scope (later plans)

- **Plan 5:** the accounts page (D6) and the Excel export (D8). Their actions join `AuditCategories` there.
- **Supervisor plan:** inspection, Supervisor block reasons (`OUTSIDE_SHIFT`, `OTHER_BUILDING`), Supervisor covers and `INSPECTION_GPS`.
- **Frontend:** every page.
- **Parked from plan 3, not touched here:**
  - A point can be added to a deactivated Area.
  - Two Admins editing one point at once get 500 instead of 409.
  - The 404 tests of the sign routes are missing.
  - Removing a Round Window loads its Scan Records into memory.

---

## File Structure

```
backend/
  src/FacilityRealtime.Domain/
    Enums/AlertType.cs                       AttendanceGps, SubmissionGps, BlockedScan                        (new, Task 2)
    Entities/AlertReview.cs                  one closed alert                                                 (new, Task 2)
  src/FacilityRealtime.Application/
    Attendance/AttendanceCalendar.cs         + PlaceTime                                                      (modified, Task 1)
    Attendance/AttendanceRules.cs            + CheckTimes, MissingAtShiftEnd                                  (modified, Task 1)
    Attendance/AreaAttendance.cs             AreaAttendanceState, StateOf                                     (new, Task 1)
    Audit/AuditCategories.cs                 AuditCategory, action → category                                 (new, Task 1)
    Alerts/AlertKind.cs                      AlertKind, AlertKinds.ReviewType                                 (new, Task 2)
  src/FacilityRealtime.Infrastructure/
    Persistence/AppDbContext.cs              + alert_reviews                                                  (modified, Task 2)
    Migrations/*_AlertReviews.cs             generated                                                        (new, Task 2)
    Persistence/AlertQuery.cs                AlertRow, LoadAsync                                              (new, Task 5)
    Persistence/AuditLogQuery.cs             AuditLogFilter, AuditLogRow, AuditActor                          (new, Task 7)
  src/FacilityRealtime.Api/
    DTOs/AdminAttendanceDtos.cs              board, correction, hub payload                                   (new, Tasks 3-4)
    DTOs/AdminAlertDtos.cs                   alert list and review                                            (new, Task 5)
    DTOs/CoverDtos.cs                        + BlockedScanId, CoverCandidateDto                               (modified, Task 6)
    DTOs/AuditLogDtos.cs                     history page                                                     (new, Task 7)
    Endpoints/AdminQuery.cs                  query-string dates, enums, shift, date range                     (new, Task 3; + Task 5)
    Endpoints/AttendanceBoard.cs             board read model and AttendanceRecorded                          (new, Task 3)
    Endpoints/ThaiLabels.cs                  Thai words for audit summaries                                   (new, Task 4; + Task 5)
    Endpoints/AdminAttendanceEndpoints.cs    GET/PUT /api/admin/attendance                                    (new, Task 3; replaced Task 4)
    Endpoints/AdminAlertEndpoints.cs         /api/admin/alerts, /review                                       (new, Task 5)
    Endpoints/CoverAssignmentEndpoints.cs    + blockedScanId, + /candidates                                   (replaced, Task 6)
    Endpoints/AdminAuditLogEndpoints.cs      /api/admin/audit-log, /actors                                    (new, Task 7)
    Endpoints/AttendanceEndpoints.cs         sends AttendanceRecorded                                         (modified, Task 3)
    Hubs/ScanHub.cs                          comment                                                          (modified, Task 3)
    Program.cs                               maps the three new endpoint files                                (modified, Tasks 3, 5, 7)
  tests/FacilityRealtime.UnitTests/          AttendanceCalendarTests, AttendanceRulesTests (+), AreaAttendanceTests,
                                             AuditCategoriesTests, AlertKindsTests                            (Tasks 1-2)
  tests/FacilityRealtime.ApiTests/
    Admin/AdminAttendanceModels.cs, Admin/AdminAttendanceApi.cs                                               (new, Task 3; + Tasks 4-7)
    Admin/AttendanceBoardEndpointTests.cs                                                                     (new, Task 3)
    Admin/AttendanceCorrectionEndpointTests.cs                                                                (new, Task 4)
    Admin/AlertEndpointTests.cs                                                                               (new, Task 5)
    Admin/CoverFromAlertTests.cs                                                                              (new, Task 6)
    Admin/AuditLogEndpointTests.cs                                                                            (new, Task 7)
    Persistence/SchemaRuleTests.cs           + alert reviewed once                                            (modified, Task 2)
    Persistence/MySqlAdminQueryTests.cs                                                                       (new, Task 7)
docs/architecture.md, README.md                                                                               (modified, Task 7)
```

Run every command from the repository root. `dotnet test backend/FacilityRealtime.slnx` runs everything.

**Endpoint map** (every route needs an Admin):

| Method and path | Task | Does |
|---|---|---|
| `GET /api/admin/attendance?shiftDate=&shift=&buildingId=` | 3 | Board of one shift; default is the shift on the clock |
| `PUT /api/admin/attendance` | 4 | Add or move one attendance event, with a reason |
| `GET /api/admin/alerts?from=&to=` | 5 | Alerts between two Thai dates (default yesterday and today) |
| `POST /api/admin/alerts/review` | 5 | "คุยแล้ว ปิดเตือน" |
| `GET /api/admin/cover-assignments/candidates?areaId=&shiftDate=&shift=` | 6 | Who can cover an Area in a shift |
| `POST /api/admin/cover-assignments` with `blockedScanId` | 6 | Assign a cover; from a Blocked Scan it also closes that alert |
| `GET /api/admin/audit-log?from=&to=&actorId=&category=&beforeId=&limit=` | 7 | History page, newest first |
| `GET /api/admin/audit-log/actors` | 7 | Everyone who appears in the history |
| SignalR `AttendanceRecorded` on `/hubs/scan` | 3, 4 | To Admins: `{ areaId, shiftDate, shift }` |

---

### Task 1: Attendance and audit rules

**Files:**
- Modify: `backend/src/FacilityRealtime.Application/Attendance/AttendanceCalendar.cs`
- Modify: `backend/src/FacilityRealtime.Application/Attendance/AttendanceRules.cs`
- Create: `backend/src/FacilityRealtime.Application/Attendance/AreaAttendance.cs`
- Create: `backend/src/FacilityRealtime.Application/Audit/AuditCategories.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/AttendanceCalendarTests.cs` (add), `AttendanceRulesTests.cs` (add), `AreaAttendanceTests.cs` (new), `AuditCategoriesTests.cs` (new)

**Interfaces:**
- Consumes: `ShiftSlot`, `ShiftCalendar.StartUtc/EndUtc`, `ThaiTime.ToUtc`, `AttendanceSettings`, `enum AttendanceEvent { ShiftIn, BreakOut, BreakIn, ShiftOut }`.
- Produces (namespace `FacilityRealtime.Application.Attendance`):
  - `AttendanceCalendar.PlaceTime(ShiftSlot slot, TimeOnly thaiTime, AttendanceSettings settings) : DateTime?` returns UTC, or null outside the slot's attendance window.
  - `AttendanceRules.CheckTimes(IReadOnlyDictionary<AttendanceEvent, DateTime> events) : string?` returns null when valid, otherwise a Thai message.
  - `AttendanceRules.MissingAtShiftEnd(IReadOnlyCollection<AttendanceEvent> recorded) : IReadOnlyList<AttendanceEvent>`.
  - `enum AreaAttendanceState { Empty, Working, OnBreak, Done, Covered }`.
  - `AreaAttendance.StateOf(IReadOnlyCollection<AttendanceEvent> regularCleanerEvents, bool hasCover) : AreaAttendanceState`.
- Produces (namespace `FacilityRealtime.Application.Audit`):
  - `enum AuditCategory { Attendance, Cover, AlertReview, Setup, QrToken }`.
  - `AuditCategories.ActionsOf(AuditCategory) : IReadOnlyList<string>`.
  - `AuditCategories.Of(string action) : AuditCategory?`.

- [ ] **Step 1: Write the failing tests**

In `backend/tests/FacilityRealtime.UnitTests/AttendanceCalendarTests.cs`, add these two tests inside the class, after the last test. The file already has `Settings`, `Thai(day, hour, minute)` (Thai wall-clock as UTC) and `Slot(day, shift)`:

```csharp
    [Theory]
    [InlineData(Shift.Day, 6, 30, 8)]      // before the shift starts
    [InlineData(Shift.Day, 19, 45, 8)]     // after it ends
    [InlineData(Shift.Night, 18, 15, 8)]
    [InlineData(Shift.Night, 23, 0, 8)]
    [InlineData(Shift.Night, 3, 0, 9)]     // after midnight: the next calendar day
    [InlineData(Shift.Night, 7, 50, 9)]
    public void An_admins_time_lands_inside_the_shifts_attendance_window(Shift shift, int hour, int minute, int expectedDay)
    {
        Assert.Equal((DateTime?)Thai(expectedDay, hour, minute), AttendanceCalendar.PlaceTime(Slot(8, shift)!, new TimeOnly(hour, minute), Settings));
    }

    [Theory]
    [InlineData(Shift.Day, 5, 59)]
    [InlineData(Shift.Day, 20, 1)]
    [InlineData(Shift.Night, 17, 59)]
    [InlineData(Shift.Night, 8, 1)]
    [InlineData(Shift.Night, 12, 0)]
    public void A_time_outside_the_window_has_no_place(Shift shift, int hour, int minute)
    {
        Assert.Null(AttendanceCalendar.PlaceTime(Slot(8, shift)!, new TimeOnly(hour, minute), Settings));
    }
```

In `backend/tests/FacilityRealtime.UnitTests/AttendanceRulesTests.cs`, add inside the class, after the last test:

```csharp
    private static readonly DateTime Start = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

    private static Dictionary<AttendanceEvent, DateTime> Times(params (AttendanceEvent Event, int Minute)[] events) =>
        events.ToDictionary(e => e.Event, e => Start.AddMinutes(e.Minute));

    [Fact]
    public void A_full_shift_in_order_is_valid() =>
        Assert.Null(AttendanceRules.CheckTimes(Times(
            (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.BreakOut, 300), (AttendanceEvent.BreakIn, 360), (AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void A_forgotten_break_in_may_stay_empty() =>
        Assert.Null(AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 0), (AttendanceEvent.BreakOut, 300), (AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void No_events_is_valid() => Assert.Null(AttendanceRules.CheckTimes(Times()));

    [Fact]
    public void Every_event_needs_a_shift_in() =>
        Assert.Contains("เข้างานก่อน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftOut, 720))));

    [Fact]
    public void A_break_in_needs_a_break_out() =>
        Assert.Contains("ออกพักก่อน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 0), (AttendanceEvent.BreakIn, 360))));

    [Fact]
    public void Times_must_run_in_order() =>
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 60), (AttendanceEvent.BreakOut, 30))));

    [Fact]
    public void Two_events_at_the_same_minute_are_out_of_order() =>
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes(Times((AttendanceEvent.ShiftIn, 0), (AttendanceEvent.ShiftOut, 0))));

    [Fact]
    public void Shift_out_must_be_last() =>
        Assert.Contains("เรียงกัน", AttendanceRules.CheckTimes(Times(
            (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.BreakOut, 300), (AttendanceEvent.BreakIn, 360), (AttendanceEvent.ShiftOut, 330))));

    [Fact]
    public void Missing_events_once_the_shift_is_over()
    {
        Assert.Empty(AttendanceRules.MissingAtShiftEnd([]));
        Assert.Equal(new[] { AttendanceEvent.ShiftOut }, AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn]));
        Assert.Equal(
            new[] { AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut },
            AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut]));
        Assert.Equal(
            new[] { AttendanceEvent.BreakIn },
            AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.ShiftOut]));
        Assert.Empty(AttendanceRules.MissingAtShiftEnd([AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut]));
        Assert.Empty(AttendanceRules.MissingAtShiftEnd(
            [AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut]));
    }
```

Create `backend/tests/FacilityRealtime.UnitTests/AreaAttendanceTests.cs`:

```csharp
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>Wireframe D2 and facility-0050: an Area's state is whether someone is there, from its regular Cleaner's events.</summary>
public class AreaAttendanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_cleaner_who_came_decides_the_state_whatever_the_cover(bool hasCover)
    {
        Assert.Equal(AreaAttendanceState.Working, AreaAttendance.StateOf([AttendanceEvent.ShiftIn], hasCover));
        Assert.Equal(AreaAttendanceState.OnBreak, AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut], hasCover));
        Assert.Equal(
            AreaAttendanceState.Working,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn], hasCover));
        Assert.Equal(
            AreaAttendanceState.Done,
            AreaAttendance.StateOf([AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.ShiftOut], hasCover));
    }

    [Fact]
    public void Nobody_came_is_empty_unless_someone_covers()
    {
        Assert.Equal(AreaAttendanceState.Empty, AreaAttendance.StateOf([], hasCover: false));
        Assert.Equal(AreaAttendanceState.Covered, AreaAttendance.StateOf([], hasCover: true));
    }
}
```

Create `backend/tests/FacilityRealtime.UnitTests/AuditCategoriesTests.cs`:

```csharp
using FacilityRealtime.Application.Audit;

namespace FacilityRealtime.UnitTests;

/// <summary>Wireframe D9's "ประเภท" filter over every action the Admin endpoints write.</summary>
public class AuditCategoriesTests
{
    [Theory]
    [InlineData("ATTENDANCE_ADD", AuditCategory.Attendance)]
    [InlineData("ATTENDANCE_EDIT", AuditCategory.Attendance)]
    [InlineData("COVER_ASSIGN", AuditCategory.Cover)]
    [InlineData("COVER_CANCEL", AuditCategory.Cover)]
    [InlineData("ALERT_REVIEW", AuditCategory.AlertReview)]
    [InlineData("AREA_CREATE", AuditCategory.Setup)]
    [InlineData("AREA_UPDATE", AuditCategory.Setup)]
    [InlineData("AREA_ACTIVATE", AuditCategory.Setup)]
    [InlineData("AREA_DEACTIVATE", AuditCategory.Setup)]
    [InlineData("POINT_CREATE", AuditCategory.Setup)]
    [InlineData("POINT_UPDATE", AuditCategory.Setup)]
    [InlineData("POINT_ACTIVATE", AuditCategory.Setup)]
    [InlineData("POINT_DEACTIVATE", AuditCategory.Setup)]
    [InlineData("SIGN_LOCATION", AuditCategory.Setup)]
    [InlineData("SIGN_RADIUS", AuditCategory.Setup)]
    [InlineData("QR_REGENERATE", AuditCategory.QrToken)]
    public void Every_action_written_so_far_has_its_category(string action, AuditCategory category) =>
        Assert.Equal((AuditCategory?)category, AuditCategories.Of(action));

    [Fact]
    public void An_unknown_action_has_no_category() => Assert.Null(AuditCategories.Of("SOMETHING_NEW"));

    [Fact]
    public void Each_action_is_in_exactly_one_category()
    {
        var categories = Enum.GetValues<AuditCategory>();
        var actions = categories.SelectMany(c => AuditCategories.ActionsOf(c)).ToList();

        Assert.Equal(actions.Count, actions.Distinct().Count());
        Assert.All(categories, c => Assert.NotEmpty(AuditCategories.ActionsOf(c)));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests`
Expected: FAIL to compile (`PlaceTime`, `CheckTimes`, `MissingAtShiftEnd`, `AreaAttendance` and `AuditCategories` are not defined).

- [ ] **Step 3: Write the implementation**

In `backend/src/FacilityRealtime.Application/Attendance/AttendanceCalendar.cs`, add this method inside the class, after `SlotFor`:

```csharp
    /// <summary>
    /// facility-0051: the moment an Admin means by a Thai wall-clock <paramref name="thaiTime"/> typed for <paramref name="slot"/>,
    /// as UTC: the one inside the slot's attendance window, or null when there is none. A Day slot's 06:30 is before
    /// the shift starts; a Night slot's 07:20 is the next morning. The window opens at most 6 hours early (Program.cs),
    /// so the moment is on ShiftDate or the day after.
    /// </summary>
    public static DateTime? PlaceTime(ShiftSlot slot, TimeOnly thaiTime, AttendanceSettings settings)
    {
        var opens = ShiftCalendar.StartUtc(slot).AddMinutes(-settings.OpensMinutesBeforeShift);
        var closes = ShiftCalendar.EndUtc(slot).AddMinutes(settings.ClosesMinutesAfterShift);
        foreach (var date in new[] { slot.ShiftDate, slot.ShiftDate.AddDays(1) })
        {
            var utc = ThaiTime.ToUtc(date.ToDateTime(thaiTime));
            if (utc >= opens && utc <= closes)
            {
                return utc;
            }
        }

        return null;
    }
```

In `backend/src/FacilityRealtime.Application/Attendance/AttendanceRules.cs`, add inside the class, after `IsOnDuty`:

```csharp
    private static readonly AttendanceEvent[] InOrder =
        [AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut];

    /// <summary>
    /// facility-0051: what an Admin's correction may leave. Break-Out and Shift-Out need a Shift-In, a Break-In needs a
    /// Break-Out, and the times run Shift-In, Break-Out, Break-In, Shift-Out, each strictly later. Null when valid,
    /// otherwise the Thai message the Admin page shows.
    /// </summary>
    public static string? CheckTimes(IReadOnlyDictionary<AttendanceEvent, DateTime> events)
    {
        if (events.Count > 0 && !events.ContainsKey(AttendanceEvent.ShiftIn))
        {
            return "ต้องมีเวลาเข้างานก่อน";
        }

        if (events.ContainsKey(AttendanceEvent.BreakIn) && !events.ContainsKey(AttendanceEvent.BreakOut))
        {
            return "ต้องมีเวลาออกพักก่อนกลับจากพัก";
        }

        var times = InOrder.Where(e => events.ContainsKey(e)).Select(e => events[e]).ToList();
        return times.Zip(times.Skip(1)).Any(pair => pair.Second <= pair.First)
            ? "เวลาต้องเรียงกัน: เข้างาน → ออกพัก → กลับจากพัก → เลิกงาน"
            : null;
    }

    /// <summary>facility-0051 rule 2: what a Cleaner who came forgot to scan, once the shift is over. Nothing for one who never checked in.</summary>
    public static IReadOnlyList<AttendanceEvent> MissingAtShiftEnd(IReadOnlyCollection<AttendanceEvent> recorded)
    {
        if (!recorded.Contains(AttendanceEvent.ShiftIn))
        {
            return [];
        }

        var missing = new List<AttendanceEvent>();
        if (recorded.Contains(AttendanceEvent.BreakOut) && !recorded.Contains(AttendanceEvent.BreakIn))
        {
            missing.Add(AttendanceEvent.BreakIn);
        }

        if (!recorded.Contains(AttendanceEvent.ShiftOut))
        {
            missing.Add(AttendanceEvent.ShiftOut);
        }

        return missing;
    }
```

Create `backend/src/FacilityRealtime.Application/Attendance/AreaAttendance.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Attendance;

/// <summary>The status column of the attendance page (wireframe D2). facility-0050: whether someone is in the Area, never late or early.</summary>
public enum AreaAttendanceState
{
    /// <summary>The regular Cleaner has not checked in and nobody covers: the Admin needs to find a cover (facility-0041).</summary>
    Empty,

    Working,

    OnBreak,

    /// <summary>The regular Cleaner scanned Shift-Out.</summary>
    Done,

    /// <summary>The regular Cleaner has not checked in, and the Admin assigned a cover for this shift.</summary>
    Covered,
}

public static class AreaAttendance
{
    /// <summary>A regular Cleaner who came decides the state; otherwise a cover makes the Area Covered, else it is Empty.</summary>
    public static AreaAttendanceState StateOf(IReadOnlyCollection<AttendanceEvent> regularCleanerEvents, bool hasCover) =>
        regularCleanerEvents.Contains(AttendanceEvent.ShiftOut) ? AreaAttendanceState.Done
        : regularCleanerEvents.Contains(AttendanceEvent.BreakOut) && !regularCleanerEvents.Contains(AttendanceEvent.BreakIn) ? AreaAttendanceState.OnBreak
        : regularCleanerEvents.Contains(AttendanceEvent.ShiftIn) ? AreaAttendanceState.Working
        : hasCover ? AreaAttendanceState.Covered
        : AreaAttendanceState.Empty;
}
```

Create `backend/src/FacilityRealtime.Application/Audit/AuditCategories.cs`:

```csharp
namespace FacilityRealtime.Application.Audit;

/// <summary>The "ประเภท" filter of the Admin history page (wireframe D9, facility-0057). Plan 5 adds accounts and the Excel export.</summary>
public enum AuditCategory
{
    Attendance,
    Cover,
    AlertReview,
    Setup,
    QrToken,
}

public static class AuditCategories
{
    private static readonly Dictionary<AuditCategory, string[]> Actions = new()
    {
        [AuditCategory.Attendance] = ["ATTENDANCE_ADD", "ATTENDANCE_EDIT"],
        [AuditCategory.Cover] = ["COVER_ASSIGN", "COVER_CANCEL"],
        [AuditCategory.AlertReview] = ["ALERT_REVIEW"],
        [AuditCategory.Setup] =
        [
            "AREA_CREATE", "AREA_UPDATE", "AREA_ACTIVATE", "AREA_DEACTIVATE",
            "POINT_CREATE", "POINT_UPDATE", "POINT_ACTIVATE", "POINT_DEACTIVATE",
            "SIGN_LOCATION", "SIGN_RADIUS",
        ],
        [AuditCategory.QrToken] = ["QR_REGENERATE"],
    };

    public static IReadOnlyList<string> ActionsOf(AuditCategory category) => Actions[category];

    /// <summary>Null for an action no category lists yet; the page shows it under "ทุกประเภท" only.</summary>
    public static AuditCategory? Of(string action) =>
        Actions.Where(pair => pair.Value.Contains(action)).Select(pair => (AuditCategory?)pair.Key).FirstOrDefault();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests`
Expected: PASS, 0 failures.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Application backend/tests/FacilityRealtime.UnitTests
git commit -m "feat(backend): rules for Admin attendance corrections, missing events, Area state and audit categories (facility-0051, 0050, 0057)"
```

---

### Task 2: The `alert_reviews` table

**Files:**
- Create: `backend/src/FacilityRealtime.Domain/Enums/AlertType.cs`
- Create: `backend/src/FacilityRealtime.Domain/Entities/AlertReview.cs`
- Create: `backend/src/FacilityRealtime.Application/Alerts/AlertKind.cs`
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/AppDbContext.cs`
- Create (generated): `backend/src/FacilityRealtime.Infrastructure/Migrations/*_AlertReviews.cs`, `*_AlertReviews.Designer.cs`; modified `AppDbContextModelSnapshot.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/AlertKindsTests.cs` (new), `backend/tests/FacilityRealtime.ApiTests/Persistence/SchemaRuleTests.cs` (add)

**Interfaces:**
- Produces (Domain):
  - `enum AlertType { AttendanceGps, SubmissionGps, BlockedScan }`, stored as `ATTENDANCE_GPS`, `SUBMISSION_GPS`, `BLOCKED_SCAN`.
  - `class AlertReview { long Id; AlertType AlertType; long RecordId; int ReviewedById; DateTime ReviewedAt; }`.
- Produces (Application, namespace `FacilityRealtime.Application.Alerts`):
  - `enum AlertKind { BlockedScan, AttendanceGps, SubmissionGps, MissingAttendance }`.
  - `AlertKinds.ReviewType(AlertKind) : AlertType?`, which is null for `MissingAttendance`.
- Produces (Infrastructure): `AppDbContext.AlertReviews : DbSet<AlertReview>`, mapped to table `alert_reviews` with `UNIQUE (alert_type, record_id)`.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.UnitTests/AlertKindsTests.cs`:

```csharp
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>database.html alert_reviews: three kinds are closed by "คุยแล้ว"; a missing Shift-Out closes when it is filled (facility-0051).</summary>
public class AlertKindsTests
{
    [Theory]
    [InlineData(AlertKind.BlockedScan, AlertType.BlockedScan)]
    [InlineData(AlertKind.AttendanceGps, AlertType.AttendanceGps)]
    [InlineData(AlertKind.SubmissionGps, AlertType.SubmissionGps)]
    public void Reviewable_kinds_map_to_their_alert_reviews_type(AlertKind kind, AlertType type) =>
        Assert.Equal((AlertType?)type, AlertKinds.ReviewType(kind));

    [Fact]
    public void A_missing_shift_out_is_filled_not_reviewed() => Assert.Null(AlertKinds.ReviewType(AlertKind.MissingAttendance));
}
```

In `backend/tests/FacilityRealtime.ApiTests/Persistence/SchemaRuleTests.cs`, add inside the class, after the last test:

```csharp
    [Fact]
    public async Task An_alert_is_reviewed_once()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            // The same id in two different tables is two different alerts
            db.AlertReviews.Add(new AlertReview { AlertType = AlertType.BlockedScan, RecordId = 7, ReviewedById = admin.Id, ReviewedAt = DateTime.UtcNow });
            db.AlertReviews.Add(new AlertReview { AlertType = AlertType.AttendanceGps, RecordId = 7, ReviewedById = admin.Id, ReviewedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            db.AlertReviews.Add(new AlertReview { AlertType = AlertType.BlockedScan, RecordId = 7, ReviewedById = admin.Id, ReviewedAt = DateTime.UtcNow });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: FAIL to compile (`AlertKind`, `AlertType`, `AlertReview` and `db.AlertReviews` are not defined).

- [ ] **Step 3: Write the implementation**

Create `backend/src/FacilityRealtime.Domain/Enums/AlertType.cs`:

```csharp
namespace FacilityRealtime.Domain.Enums;

/// <summary>
/// database.html alert_reviews.alert_type: the alerts an Admin closes with "คุยแล้ว ปิดเตือน" (facility-0035, 0041).
/// A missing Shift-Out is not one: it closes itself when the Admin fills the time (facility-0051). INSPECTION_GPS comes with the Supervisor plan.
/// </summary>
public enum AlertType
{
    AttendanceGps,
    SubmissionGps,
    BlockedScan,
}
```

Create `backend/src/FacilityRealtime.Domain/Entities/AlertReview.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>database.html alert_reviews: an alert the Admin closed ("คุยแล้ว ปิดเตือน"). One per alert.</summary>
public class AlertReview
{
    public long Id { get; set; }
    public AlertType AlertType { get; set; }

    /// <summary>The row in the alert's own table: shift_attendances, scan_records or blocked_scans.</summary>
    public long RecordId { get; set; }

    public int ReviewedById { get; set; }
    public DateTime ReviewedAt { get; set; }
}
```

Create `backend/src/FacilityRealtime.Application/Alerts/AlertKind.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Alerts;

/// <summary>The kinds of line on the alert list (wireframe D3).</summary>
public enum AlertKind
{
    /// <summary>A scan refused because the sign belongs to another Area (facility-0041).</summary>
    BlockedScan,

    /// <summary>An attendance scan outside its sign's radius: recorded and flagged (facility-0035).</summary>
    AttendanceGps,

    /// <summary>A submission outside its sign's radius: recorded and flagged (facility-0037).</summary>
    SubmissionGps,

    /// <summary>A Cleaner who checked in has no Shift-Out, or no Break-In after a Break-Out, once the shift ended (facility-0051).</summary>
    MissingAttendance,
}

public static class AlertKinds
{
    /// <summary>What alert_reviews stores when the Admin closes it; null for MissingAttendance, which closes once the Admin fills the time.</summary>
    public static AlertType? ReviewType(AlertKind kind) => kind switch
    {
        AlertKind.BlockedScan => AlertType.BlockedScan,
        AlertKind.AttendanceGps => AlertType.AttendanceGps,
        AlertKind.SubmissionGps => AlertType.SubmissionGps,
        _ => null,
    };
}
```

In `backend/src/FacilityRealtime.Infrastructure/Persistence/AppDbContext.cs`:

Add after `public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();`:

```csharp
    public DbSet<AlertReview> AlertReviews => Set<AlertReview>();
```

Add this block right after the `modelBuilder.Entity<AuditEntry>(e => { ... });` block, before `modelBuilder.UseSnakeCaseColumns();`:

```csharp
        modelBuilder.Entity<AlertReview>(e =>
        {
            e.ToTable("alert_reviews");
            e.Property(x => x.AlertType).HasConversion(new UpperSnakeEnumConverter<AlertType>()).HasMaxLength(20);
            // database.html: an alert is closed once
            e.HasIndex(x => new { x.AlertType, x.RecordId }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.Restrict);
        });
```

- [ ] **Step 4: Run the SQLite tests**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: 0 failures except `MigrationTests.Migrations_match_the_model`, which FAILS because the model changed. The next step adds the migration.

- [ ] **Step 5: Generate the migration**

```bash
dotnet tool restore
dotnet ef migrations add AlertReviews --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api
dotnet ef migrations script ScanRules AlertReviews --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api --output "$TMP/alert-reviews.sql"
grep -c "CREATE TABLE" "$TMP/alert-reviews.sql"
grep -n "alert_reviews" "$TMP/alert-reviews.sql"
```

Expected:
- `1` CREATE TABLE.
- Columns `id` bigint auto-increment, `alert_type` varchar(20), `record_id` bigint, `reviewed_by_id` int and `reviewed_at` datetime.
- A UNIQUE index on `(alert_type, record_id)` and a foreign key from `reviewed_by_id` to `users`.
- No change to any other table.

`$TMP` is any temp folder outside the repo, for example the session's scratchpad.

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: 0 failures (the MySQL tests are skipped).

- [ ] **Step 6: Verify the migration on real MySQL**

Ask the developer for the local root password. Type it only into these commands and never save it:

```bash
dotnet ef database update --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api --connection "Server=localhost;Port=3306;Database=facility_migration_test;Uid=root;Pwd=<local root password>;CharSet=utf8mb4;"
MYSQL_PWD='<local root password>' "/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -uroot -e "SHOW CREATE TABLE facility_migration_test.alert_reviews\G DROP DATABASE facility_migration_test;"
```

Expected:
- `database update` applies `PilotSchema`, `ScanRules` and `AlertReviews` with no error.
- `SHOW CREATE TABLE` shows the unique key on `(alert_type, record_id)`.
- The throwaway database is dropped.
- Never point these commands at `facility_dashboard`.

- [ ] **Step 7: Commit**

```bash
git add backend/src backend/tests
git status --short
git commit -m "feat(backend): alert_reviews table, one review per alert (database.html, facility-0035, 0041)"
```

---

### Task 3: Attendance board (D2) and the live `AttendanceRecorded` event

**Files:**
- Create: `backend/src/FacilityRealtime.Api/DTOs/AdminAttendanceDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminQuery.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AttendanceBoard.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminAttendanceEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AttendanceEndpoints.cs`, `backend/src/FacilityRealtime.Api/Hubs/ScanHub.cs`, `backend/src/FacilityRealtime.Api/Program.cs`
- Create (tests): `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceModels.cs`, `Admin/AdminAttendanceApi.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AttendanceBoardEndpointTests.cs`

**Interfaces:**
- Consumes:
  - `AreaAttendance.StateOf` and `AreaAttendanceState` (Task 1).
  - `AssignedCleanerDto(int Id, string EmployeeId, string DisplayName)` (plan 3).
  - `ShiftCalendar.SlotAt`/`EndUtc`, `ScanHub.AdminGroup`.
- Produces (Api.DTOs):
  - `BoardEventDto(long Id, AttendanceEvent EventType, DateTime OccurredAt, AttendanceSource Source, bool? WithinRadius, short? DistanceM)`.
  - `BoardCoverDto(int Id, int UserId, string EmployeeId, string DisplayName, string? OwnAreaCode, DateTime AssignedAt, DateTime? ShiftInAt)`.
  - `BoardRowDto(int AreaId, string AreaCode, string AreaName, string BuildingCode, AssignedCleanerDto? Cleaner, AreaAttendanceState State, IReadOnlyList<BoardEventDto> Events, IReadOnlyList<BoardCoverDto> Covers)`.
  - `BoardSummaryDto(int Required, int Present, int Empty, int Covered)`.
  - `AttendanceBoardDto(DateOnly ShiftDate, Shift Shift, bool ShiftEnded, BoardSummaryDto Summary, IReadOnlyList<BoardRowDto> Rows)`.
  - `AttendanceChangedDto(int AreaId, DateOnly ShiftDate, Shift Shift)`.
- Produces (Api.Endpoints, internal):
  - `AdminQuery.TryReadDate(string?, out DateOnly) : bool`.
  - `AdminQuery.TryReadEnum<T>(string?, out T) : bool`.
  - `AdminQuery.ReadSlot(string? shiftDate, string? shift, DateTime nowUtc, out ShiftSlot slot) : string?` (the error to show, or null).
  - `AttendanceBoard.LoadAsync(AppDbContext, ShiftSlot, int? buildingId, DateTime nowUtc) : Task<AttendanceBoardDto>`.
  - `AttendanceBoard.ToDto(ShiftAttendance) : BoardEventDto`.
  - `AttendanceBoard.NotifyAsync(IHubContext<ScanHub>, int areaId, ShiftSlot) : Task`.
- Produces (routes): `GET /api/admin/attendance`. Errors: 400 for a bad `shiftDate`/`shift` pair, 401, 403.
- Produces (tests): `AdminAttendanceApi.BoardPath` and `AdminAttendanceApi.BoardAsync(HttpClient admin, string query = "")`, plus the board models.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceModels.cs`:

```csharp
namespace FacilityRealtime.ApiTests.Admin;

public record BoardEventModel(long Id, string EventType, DateTime OccurredAt, string Source, bool? WithinRadius, short? DistanceM);

public record BoardCoverModel(int Id, int UserId, string EmployeeId, string DisplayName, string? OwnAreaCode, DateTime AssignedAt, DateTime? ShiftInAt);

public record BoardRowModel(
    int AreaId, string AreaCode, string AreaName, string BuildingCode, AssignedCleanerModel? Cleaner,
    string State, List<BoardEventModel> Events, List<BoardCoverModel> Covers);

public record BoardSummaryModel(int Required, int Present, int Empty, int Covered);

public record AttendanceBoardModel(DateOnly ShiftDate, string Shift, bool ShiftEnded, BoardSummaryModel Summary, List<BoardRowModel> Rows);
```

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceApi.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Calls the Admin's attendance, alert and history pages.</summary>
public static class AdminAttendanceApi
{
    public const string BoardPath = "/api/admin/attendance";

    public static async Task<AttendanceBoardModel> BoardAsync(HttpClient admin, string query = "")
    {
        var response = await admin.GetAsync(BoardPath + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AttendanceBoardModel>())!;
    }
}
```

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AttendanceBoardEndpointTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// Wireframe D2, facility-0027/0050. Seed: AR01 (Day and Night; E1001 Day, E1002 Night) and AR02 (Day only; E1003).
/// Log in before moving the clock.
/// </summary>
public class AttendanceBoardEndpointTests
{
    [Fact]
    public async Task The_board_needs_an_admin()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateApiClient().GetAsync(AdminAttendanceApi.BoardPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(AdminAttendanceApi.BoardPath)).StatusCode);
    }

    [Fact]
    public async Task Before_anyone_checks_in_every_area_is_empty()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 0));

        var board = await AdminAttendanceApi.BoardAsync(admin);

        Assert.Equal(new DateOnly(2026, 10, 8), board.ShiftDate);
        Assert.Equal("Day", board.Shift);
        Assert.False(board.ShiftEnded);
        Assert.Equal(new[] { "AR01", "AR02" }, board.Rows.Select(r => r.AreaCode));
        Assert.All(board.Rows, r => Assert.Equal("Empty", r.State));
        Assert.Equal("E1001", board.Rows[0].Cleaner!.EmployeeId);
        Assert.Equal("A", board.Rows[0].BuildingCode);
        Assert.Equal(new BoardSummaryModel(2, 0, 2, 0), board.Summary);
    }

    [Fact]
    public async Task An_area_follows_its_cleaners_scans_and_empty_areas_come_first()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var working = await AdminAttendanceApi.BoardAsync(admin);
        Assert.Equal(new[] { "AR02", "AR01" }, working.Rows.Select(r => r.AreaCode));
        var ar01 = working.Rows[1];
        Assert.Equal("Working", ar01.State);
        var shiftIn = Assert.Single(ar01.Events);
        Assert.Equal("ShiftIn", shiftIn.EventType);
        Assert.Equal("Scan", shiftIn.Source);
        Assert.Equal(ThaiClock.At(8, 6, 50).UtcDateTime, shiftIn.OccurredAt);
        Assert.True(shiftIn.WithinRadius);
        Assert.Equal(new BoardSummaryModel(2, 1, 1, 0), working.Summary);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 11, 0));
        Assert.Equal(HttpStatusCode.OK, (await AttendanceApi.RecordAsync(cleaner, "BreakOut")).StatusCode);
        Assert.Equal("OnBreak", (await AdminAttendanceApi.BoardAsync(admin)).Rows.Single(r => r.AreaCode == "AR01").State);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 19, 5));
        Assert.Equal(HttpStatusCode.OK, (await AttendanceApi.RecordAsync(cleaner, "ShiftOut")).StatusCode);
        // 19:05 is the Night shift on the clock, so ask for the Day shift
        var done = await AdminAttendanceApi.BoardAsync(admin, "?shiftDate=2026-10-08&shift=Day");
        Assert.Equal("Done", done.Rows.Single(r => r.AreaCode == "AR01").State);
        Assert.True(done.ShiftEnded);
        Assert.Equal(1, done.Summary.Present);
    }

    [Fact]
    public async Task The_night_board_leaves_out_day_only_areas()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 21, 0));

        var board = await AdminAttendanceApi.BoardAsync(admin);

        Assert.Equal("Night", board.Shift);
        var row = Assert.Single(board.Rows);
        Assert.Equal("AR01", row.AreaCode);
        Assert.Equal("E1002", row.Cleaner!.EmployeeId);
    }

    [Fact]
    public async Task A_cover_fills_an_area_whose_cleaner_did_not_come()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        await TestData.AddCoverAsync(factory, "E1001", "AR02", new DateOnly(2026, 10, 8), Shift.Day);
        await TestData.AddCoverAsync(factory, "E1001", "AR02", new DateOnly(2026, 10, 8), Shift.Day, cancelled: true);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var board = await AdminAttendanceApi.BoardAsync(admin);

        var ar02 = board.Rows.Single(r => r.AreaCode == "AR02");
        Assert.Equal("Covered", ar02.State);
        Assert.Equal("E1003", ar02.Cleaner!.EmployeeId);
        Assert.Empty(ar02.Events);
        var cover = Assert.Single(ar02.Covers);
        Assert.Equal("E1001", cover.EmployeeId);
        Assert.Equal("AR01", cover.OwnAreaCode);
        Assert.Equal(ThaiClock.At(8, 6, 50).UtcDateTime, cover.ShiftInAt);
        Assert.Equal(new BoardSummaryModel(2, 1, 0, 1), board.Summary);
    }

    [Fact]
    public async Task A_check_in_far_from_the_sign_carries_its_distance()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 6, 50));
        var far = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);
        Assert.Equal(HttpStatusCode.OK, far.StatusCode);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var shiftIn = Assert.Single((await AdminAttendanceApi.BoardAsync(admin)).Rows.Single(r => r.AreaCode == "AR01").Events);

        Assert.False(shiftIn.WithinRadius);
        Assert.True(shiftIn.DistanceM > 50);
    }

    [Fact]
    public async Task The_board_filters_by_building_and_leaves_out_deactivated_areas()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 0));
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);

        Assert.Equal(2, (await AdminAttendanceApi.BoardAsync(admin, $"?buildingId={buildingA}")).Rows.Count);
        Assert.Empty((await AdminAttendanceApi.BoardAsync(admin, $"?buildingId={buildingA + 100}")).Rows);

        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Code == "AR02")).IsActive = false;
            await db.SaveChangesAsync();
        });
        Assert.Equal(new[] { "AR01" }, (await AdminAttendanceApi.BoardAsync(admin)).Rows.Select(r => r.AreaCode));
    }

    [Theory]
    [InlineData("?shiftDate=2026-10-08")]
    [InlineData("?shift=Day")]
    [InlineData("?shiftDate=2026-10-08&shift=Evening")]
    [InlineData("?shiftDate=2026-10-08&shift=1")]
    [InlineData("?shiftDate=08/10/2026&shift=Day")]
    public async Task A_shift_needs_both_a_date_and_day_or_night(string query)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.GetAsync(AdminAttendanceApi.BoardPath + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_check_in_reaches_the_admins_live()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (_, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await using var adminHub = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        var got = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        adminHub.On<JsonElement>("AttendanceRecorded", payload => got.TrySetResult(payload));
        await Task.Delay(200);

        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));

        var payload = await got.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR01"), payload.GetProperty("areaId").GetInt32());
        Assert.Equal("2026-10-08", payload.GetProperty("shiftDate").GetString());
        Assert.Equal("Day", payload.GetProperty("shift").GetString());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AttendanceBoardEndpointTests"`
Expected: FAIL. The route does not exist yet, so the requests give 404 and the hub test times out.

- [ ] **Step 3: Write the implementation**

Create `backend/src/FacilityRealtime.Api/DTOs/AdminAttendanceDtos.cs`:

```csharp
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>One attendance event. Source AdminAdd/AdminEdit is the "แก้โดย Admin" badge (facility-0051); WithinRadius false is a flag (facility-0035).</summary>
public record BoardEventDto(long Id, AttendanceEvent EventType, DateTime OccurredAt, AttendanceSource Source, bool? WithinRadius, short? DistanceM);

/// <summary>A Cleaner assigned to cover this Area for the shift (facility-0041), with their own Area and their Shift-In.</summary>
public record BoardCoverDto(int Id, int UserId, string EmployeeId, string DisplayName, string? OwnAreaCode, DateTime AssignedAt, DateTime? ShiftInAt);

/// <summary>Cleaner is the Area's regular Cleaner for the shift (null when none is assigned); Events are theirs.</summary>
public record BoardRowDto(
    int AreaId,
    string AreaCode,
    string AreaName,
    string BuildingCode,
    AssignedCleanerDto? Cleaner,
    AreaAttendanceState State,
    IReadOnlyList<BoardEventDto> Events,
    IReadOnlyList<BoardCoverDto> Covers);

/// <summary>Required = Areas that work this shift; Present = Working, OnBreak or Done. Required = Present + Empty + Covered.</summary>
public record BoardSummaryDto(int Required, int Present, int Empty, int Covered);

public record AttendanceBoardDto(DateOnly ShiftDate, Shift Shift, bool ShiftEnded, BoardSummaryDto Summary, IReadOnlyList<BoardRowDto> Rows);

/// <summary>SignalR "AttendanceRecorded" to Admins (facility-0027 rule 5): the page reloads that shift's board.</summary>
public record AttendanceChangedDto(int AreaId, DateOnly ShiftDate, Shift Shift);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminQuery.cs`:

```csharp
using System.Globalization;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>Query-string values of the Admin pages. Dates are Thai calendar dates written yyyy-MM-dd.</summary>
internal static class AdminQuery
{
    public static bool TryReadDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>An enum by its name in any letter case, e.g. "day" or "Night". Numbers are refused.</summary>
    public static bool TryReadEnum<T>(string? text, out T value)
        where T : struct, Enum
    {
        var name = Enum.GetNames<T>().FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
        value = name is null ? default : Enum.Parse<T>(name);
        return name is not null;
    }

    /// <summary>Both values, or neither: then the shift on the clock, as the status page shows it. Returns the error to show, or null.</summary>
    public static string? ReadSlot(string? shiftDate, string? shift, DateTime nowUtc, out ShiftSlot slot)
    {
        slot = ShiftCalendar.SlotAt(nowUtc);
        if (shiftDate is null && shift is null)
        {
            return null;
        }

        if (!TryReadDate(shiftDate, out var date) || !TryReadEnum<Shift>(shift, out var parsed))
        {
            return "ต้องระบุวันที่ (yyyy-MM-dd) และกะ (Day หรือ Night) คู่กัน";
        }

        slot = new ShiftSlot(date, parsed);
        return null;
    }
}
```

Create `backend/src/FacilityRealtime.Api/Endpoints/AttendanceBoard.cs`:

```csharp
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// The attendance page (wireframe D2, facility-0027/0050): every active Area that works the shift, its regular
/// Cleaner's events and the covers assigned to it. Areas with nobody come first.
/// </summary>
internal static class AttendanceBoard
{
    public static async Task<AttendanceBoardDto> LoadAsync(AppDbContext db, ShiftSlot slot, int? buildingId, DateTime nowUtc)
    {
        // A day-only Area has no night shift, so it is not "ต้องมีคน" at night
        var areas = (await db.Areas.AsNoTracking()
                .Include(a => a.Building)
                .Where(a => a.IsActive && (buildingId == null || a.BuildingId == buildingId))
                .ToListAsync())
            .Where(a => a.HasShift(slot.Shift))
            .ToList();
        var areaIds = areas.Select(a => a.Id).ToList();

        var cleaners = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.Shift == slot.Shift
                && u.AreaId != null && areaIds.Contains(u.AreaId.Value))
            .ToListAsync();
        var covers = await db.CoverAssignments.AsNoTracking()
            .Include(c => c.User!).ThenInclude(u => u.Area)
            .Where(c => c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null && areaIds.Contains(c.AreaId))
            .OrderBy(c => c.AssignedAt)
            .ToListAsync();
        var userIds = cleaners.Select(u => u.Id).Concat(covers.Select(c => c.UserId)).Distinct().ToList();
        var events = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift && userIds.Contains(a.UserId))
            .OrderBy(a => a.OccurredAt)
            .ToListAsync();

        var rows = areas
            .Select(area => Row(area, cleaners.FirstOrDefault(u => u.AreaId == area.Id), covers.Where(c => c.AreaId == area.Id).ToList(), events))
            .OrderBy(r => r.State == AreaAttendanceState.Empty ? 0 : 1)
            .ThenBy(r => r.AreaCode)
            .ToList();

        return new AttendanceBoardDto(
            slot.ShiftDate,
            slot.Shift,
            nowUtc >= ShiftCalendar.EndUtc(slot),
            new BoardSummaryDto(
                rows.Count,
                rows.Count(r => r.State is AreaAttendanceState.Working or AreaAttendanceState.OnBreak or AreaAttendanceState.Done),
                rows.Count(r => r.State == AreaAttendanceState.Empty),
                rows.Count(r => r.State == AreaAttendanceState.Covered)),
            rows);
    }

    public static BoardEventDto ToDto(ShiftAttendance a) =>
        new(a.Id, a.EventType, Utc(a.OccurredAt), a.Source, a.WithinRadius, a.DistanceM);

    /// <summary>facility-0027 rule 5: the attendance page reloads that board; it also polls every 30 s (facility-0061).</summary>
    public static Task NotifyAsync(IHubContext<ScanHub> hub, int areaId, ShiftSlot slot) =>
        hub.Clients.Group(ScanHub.AdminGroup).SendAsync("AttendanceRecorded", new AttendanceChangedDto(areaId, slot.ShiftDate, slot.Shift));

    private static BoardRowDto Row(Area area, User? cleaner, List<CoverAssignment> covers, List<ShiftAttendance> events)
    {
        var own = cleaner is null ? new List<ShiftAttendance>() : events.Where(e => e.UserId == cleaner.Id).ToList();
        return new BoardRowDto(
            area.Id,
            area.Code,
            area.Name,
            area.Building!.Code,
            cleaner is null ? null : new AssignedCleanerDto(cleaner.Id, cleaner.EmployeeId ?? string.Empty, cleaner.DisplayName),
            AreaAttendance.StateOf(own.Select(e => e.EventType).ToList(), covers.Count > 0),
            own.Select(ToDto).ToList(),
            covers.Select(c => new BoardCoverDto(
                    c.Id,
                    c.UserId,
                    c.User!.EmployeeId ?? string.Empty,
                    c.User.DisplayName,
                    c.User.Area?.Code,
                    Utc(c.AssignedAt),
                    events.Where(e => e.UserId == c.UserId && e.EventType == AttendanceEvent.ShiftIn)
                        .Select(e => (DateTime?)Utc(e.OccurredAt))
                        .FirstOrDefault()))
                .ToList());
    }

    /// <summary>The database returns Unspecified; marking it UTC makes the JSON end in "Z".</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);
}
```

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminAttendanceEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>The attendance page (wireframe D2): who is in each Area this shift (facility-0027, 0050).</summary>
public static class AdminAttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAdminAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/attendance").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/", GetBoardAsync);
        return app;
    }

    private static async Task<IResult> GetBoardAsync(
        string? shiftDate, string? shift, int? buildingId, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        return AdminQuery.ReadSlot(shiftDate, shift, now, out var slot) is { } error
            ? ApiResults.Message(StatusCodes.Status400BadRequest, error)
            : Results.Ok(await AttendanceBoard.LoadAsync(db, slot, buildingId, now));
    }
}
```

In `backend/src/FacilityRealtime.Api/Endpoints/AttendanceEndpoints.cs`:

Add these two usings, keeping the list in order:

```csharp
using FacilityRealtime.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
```

Replace the `RecordAsync` signature

```csharp
    private static async Task<IResult> RecordAsync(
        RecordAttendanceRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
```

with

```csharp
    private static async Task<IResult> RecordAsync(
        RecordAttendanceRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider clock,
        AttendanceSettings settings,
        IHubContext<ScanHub> hub)
```

Replace the last line of `RecordAsync`

```csharp
        return Results.Ok(State(slot, [.. existing, entry], AttendanceOutcome.Recorded));
```

with

```csharp
        await AttendanceBoard.NotifyAsync(hub, area.Id, slot);
        return Results.Ok(State(slot, [.. existing, entry], AttendanceOutcome.Recorded));
```

In `backend/src/FacilityRealtime.Api/Hubs/ScanHub.cs`, replace the class comment

```csharp
/// <summary>Server-to-client only. "ScanRecorded" goes to Admin Accounts only (facility-0058).</summary>
```

with

```csharp
/// <summary>Server-to-client only. "ScanRecorded" and "AttendanceRecorded" go to Admin Accounts only (facility-0058, 0027).</summary>
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add after `app.MapAdminSignEndpoints();`:

```csharp
app.MapAdminAttendanceEndpoints();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AttendanceBoardEndpointTests"`
Expected: PASS, 13 tests.

Then run `dotnet test backend/FacilityRealtime.slnx`. Expected: 0 failures.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Api backend/tests/FacilityRealtime.ApiTests/Admin
git commit -m "feat(backend): Admin attendance board per Area and shift, live AttendanceRecorded (facility-0027, 0050)"
```

---

### Task 4: Admin adds or moves attendance times

**Files:**
- Create: `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs`
- Modify: `backend/src/FacilityRealtime.Api/DTOs/AdminAttendanceDtos.cs` (add the request)
- Replace: `backend/src/FacilityRealtime.Api/Endpoints/AdminAttendanceEndpoints.cs`
- Modify (tests): `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceApi.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AttendanceCorrectionEndpointTests.cs`

**Interfaces:**
- Consumes:
  - `AttendanceCalendar.PlaceTime`, `AttendanceRules.CheckTimes` (Task 1).
  - `AttendanceBoard.ToDto`, `AttendanceBoard.NotifyAsync`, `AdminQuery` (Task 3).
  - `RoundWindowRules.TryParseTime(string?, out TimeOnly)` (plan 3, `"HH:mm"`).
  - `AuditTrail.Add`.
- Produces (Api.DTOs): `CorrectAttendanceRequest(int UserId, DateOnly? ShiftDate, Shift? Shift, AttendanceEvent? EventType, string? Time, string? Reason)`.
- Produces (Api.Endpoints, internal) `ThaiLabels`:
  - `ShiftName(Shift) : string` returns "เช้า" or "ดึก".
  - `EventName(AttendanceEvent) : string`.
  - `Clock(DateTime utc) : string` returns Thai "HH:mm".
  - `Date(DateOnly) : string` returns "yyyy-MM-dd" in invariant culture.
- Produces (routes) `PUT /api/admin/attendance`:
  - 200 with `BoardEventDto`.
  - 400 for missing fields, no reason, a bad time, a non-Cleaner, the wrong shift, outside the window, the future, or the wrong order.
  - 409 when a racing scan already created the event.
  - Audit actions `ATTENDANCE_ADD` and `ATTENDANCE_EDIT` on `shift_attendances`, with the reason.
- Produces (tests): `AdminAttendanceApi.CorrectAsync(HttpClient admin, int userId, DateOnly shiftDate, string shift, string eventType, string? time, string? reason)`.

- [ ] **Step 1: Write the failing tests**

In `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceApi.cs`, add inside the class, after `BoardAsync`:

```csharp
    public static Task<HttpResponseMessage> CorrectAsync(
        HttpClient admin, int userId, DateOnly shiftDate, string shift, string eventType, string? time, string? reason) =>
        admin.PutAsJsonAsync(BoardPath, new { userId, shiftDate, shift, eventType, time, reason });
```

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AttendanceCorrectionEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0051: the Admin adds or moves a Cleaner's attendance time with a reason, and each change is logged.</summary>
public class AttendanceCorrectionEndpointTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    private static Task<List<ShiftAttendance>> EventsAsync(FacilityApiFactory factory, string employeeId) =>
        AdminSetupApi.ReadAsync(factory, db => db.ShiftAttendances.AsNoTracking()
            .Where(a => a.User!.EmployeeId == employeeId)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync());

    [Fact]
    public async Task Admin_adds_a_forgotten_check_in_with_a_reason()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var e1003 = await AdminSetupApi.UserIdAsync(factory, "E1003");

        var response = await AdminAttendanceApi.CorrectAsync(admin, e1003, Oct8, "Day", "ShiftIn", "06:58", "มือถือแบตหมด หัวหน้ายืนยันว่ามาทำงาน");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var added = (await response.Content.ReadFromJsonAsync<BoardEventModel>())!;
        Assert.Equal("AdminAdd", added.Source);
        Assert.Equal(ThaiClock.At(8, 6, 58).UtcDateTime, added.OccurredAt);
        var stored = Assert.Single(await EventsAsync(factory, "E1003"));
        Assert.Null(stored.SignId);
        Assert.Null(stored.WithinRadius);
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR02"), stored.AreaId);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ATTENDANCE_ADD", entry.Action);
        Assert.Equal(adminAuth.User.Id, entry.ActorId);
        Assert.Equal("shift_attendances", entry.EntityType);
        Assert.Equal(stored.Id, entry.EntityId);
        Assert.Equal("มือถือแบตหมด หัวหน้ายืนยันว่ามาทำงาน", entry.Reason);
        Assert.Null(entry.BeforeJson);
        Assert.Contains("06:58", entry.Summary);
        Assert.Equal("Working", (await AdminAttendanceApi.BoardAsync(admin)).Rows.Single(r => r.AreaCode == "AR02").State);
    }

    [Fact]
    public async Task Admin_moves_a_scanned_time_and_the_old_time_stays_in_the_log()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var response = await AdminAttendanceApi.CorrectAsync(admin, e1001, Oct8, "Day", "ShiftIn", "06:45", "เน็ตหลุด สแกนช้า");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = Assert.Single(await EventsAsync(factory, "E1001"));
        Assert.Equal(AttendanceSource.AdminEdit, stored.Source);
        Assert.Equal(ThaiClock.At(8, 6, 45).UtcDateTime, stored.OccurredAt);
        // A scanned event keeps its sign and GPS verdict
        Assert.NotNull(stored.SignId);
        Assert.True(stored.WithinRadius);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ATTENDANCE_EDIT", entry.Action);
        Assert.Contains("23:50:00Z", entry.BeforeJson);
        Assert.Contains("23:45:00Z", entry.AfterJson);
    }

    [Fact]
    public async Task A_missing_night_shift_out_is_filled_on_the_next_morning()
    {
        using var factory = new FacilityApiFactory();
        var (night, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E1002", "0810000002");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, night, ThaiClock.At(8, 18, 55));
        factory.Clock.SetUtcNow(ThaiClock.At(9, 9, 0));
        var e1002 = await AdminSetupApi.UserIdAsync(factory, "E1002");

        var response = await AdminAttendanceApi.CorrectAsync(admin, e1002, Oct8, "Night", "ShiftOut", "07:00", "ลืมสแกนเลิกงาน");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shiftOut = (await EventsAsync(factory, "E1002")).Single(a => a.EventType == AttendanceEvent.ShiftOut);
        Assert.Equal(ThaiClock.At(9, 7, 0).UtcDateTime, shiftOut.OccurredAt);
        Assert.Equal(AttendanceSource.AdminAdd, shiftOut.Source);
    }

    [Theory]
    [InlineData("Day", "ShiftOut", "05:00", "ลืมสแกน")]    // before the window opens at 06:00
    [InlineData("Day", "ShiftOut", "13:00", "ลืมสแกน")]    // still in the future at 12:00
    [InlineData("Night", "ShiftIn", "20:00", "ลืมสแกน")]   // not E1001's regular shift
    [InlineData("Day", "BreakIn", "10:00", "ลืมสแกน")]     // no Break-Out before it
    [InlineData("Day", "BreakOut", "06:30", "ลืมสแกน")]    // before the 06:50 Shift-In
    [InlineData("Day", "ShiftOut", "7:00", "ลืมสแกน")]     // not HH:mm
    [InlineData("Day", "ShiftOut", "11:00", "   ")]        // no reason
    [InlineData("Day", "ShiftOut", "11:00", null)]
    public async Task Invalid_corrections_are_refused_and_change_nothing(string shift, string eventType, string time, string? reason)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 12, 0));
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var response = await AdminAttendanceApi.CorrectAsync(admin, e1001, Oct8, shift, eventType, time, reason);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(await EventsAsync(factory, "E1001"));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Only_active_cleaners_have_attendance()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var supervisor = await AdminSetupApi.UserIdAsync(factory, "S2001");

        var response = await AdminAttendanceApi.CorrectAsync(admin, supervisor, Oct8, "Day", "ShiftIn", "06:58", "ลืมสแกน");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Saving_the_same_time_again_logs_nothing()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var e1003 = await AdminSetupApi.UserIdAsync(factory, "E1003");

        var first = await AdminAttendanceApi.CorrectAsync(admin, e1003, Oct8, "Day", "ShiftIn", "06:58", "ลืมสแกน");
        var again = await AdminAttendanceApi.CorrectAsync(admin, e1003, Oct8, "Day", "ShiftIn", "06:58", "ลืมสแกน");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Only_admins_correct_attendance()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);

        var response = await AdminAttendanceApi.CorrectAsync(cleaner, auth.User.Id, Oct8, "Day", "ShiftIn", "06:58", "แก้เอง");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AttendanceCorrectionEndpointTests"`
Expected: FAIL. The PUT route does not exist, so the endpoint answers 405 Method Not Allowed.

- [ ] **Step 3: Write the implementation**

Create `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs`:

```csharp
using System.Globalization;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>Words and times in an audit row's summary, which the Admin history page shows as is (facility-0057).</summary>
internal static class ThaiLabels
{
    public static string ShiftName(Shift shift) => shift == Shift.Day ? "เช้า" : "ดึก";

    public static string EventName(AttendanceEvent eventType) => eventType switch
    {
        AttendanceEvent.ShiftIn => "เข้างาน",
        AttendanceEvent.BreakOut => "ออกพัก",
        AttendanceEvent.BreakIn => "กลับจากพัก",
        _ => "เลิกงาน",
    };

    /// <summary>A stored UTC time as Thai wall-clock "HH:mm".</summary>
    public static string Clock(DateTime utc) => ThaiTime.FromUtc(utc).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Gregorian yyyy-MM-dd whatever the server's culture; th-TH would print the Buddhist year.</summary>
    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
```

In `backend/src/FacilityRealtime.Api/DTOs/AdminAttendanceDtos.cs`, add at the end of the file:

```csharp
/// <summary>facility-0051. Time is Thai wall-clock "HH:mm" inside the shift's attendance window (facility-0069); Reason is required.</summary>
public record CorrectAttendanceRequest(int UserId, DateOnly? ShiftDate, Shift? Shift, AttendanceEvent? EventType, string? Time, string? Reason);
```

Replace the whole of `backend/src/FacilityRealtime.Api/Endpoints/AdminAttendanceEndpoints.cs` with:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>The attendance page (wireframe D2): who is in each Area this shift (facility-0027, 0050), and the Admin's corrections (facility-0051).</summary>
public static class AdminAttendanceEndpoints
{
    public const int MaxReasonLength = 500;

    public static IEndpointRouteBuilder MapAdminAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/attendance").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/", GetBoardAsync);
        admin.MapPut("/", CorrectAsync);
        return app;
    }

    private static async Task<IResult> GetBoardAsync(
        string? shiftDate, string? shift, int? buildingId, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        return AdminQuery.ReadSlot(shiftDate, shift, now, out var slot) is { } error
            ? Bad(error)
            : Results.Ok(await AttendanceBoard.LoadAsync(db, slot, buildingId, now));
    }

    /// <summary>
    /// facility-0051: adds a missing event or moves an existing one, always with a reason. The row gets the "แก้โดย Admin" badge
    /// (AdminAdd or AdminEdit) and the old time stays in the audit log. A scanned event keeps its sign and GPS verdict.
    /// </summary>
    private static async Task<IResult> CorrectAsync(
        CorrectAttendanceRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider clock,
        AttendanceSettings settings,
        IHubContext<ScanHub> hub)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.ShiftDate is not { } shiftDate
            || request.Shift is not { } shift || !Enum.IsDefined(shift)
            || request.EventType is not { } eventType || !Enum.IsDefined(eventType))
        {
            return Bad("ต้องระบุวันที่ กะ และรายการที่จะเพิ่มหรือแก้");
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return Bad("ต้องกรอกเหตุผล");
        }

        if (reason.Length > MaxReasonLength)
        {
            return Bad($"เหตุผลยาวได้ไม่เกิน {MaxReasonLength} ตัวอักษร");
        }

        if (!RoundWindowRules.TryParseTime(request.Time, out var time))
        {
            return Bad("เวลาต้องเป็นแบบ HH:mm เช่น 06:58");
        }

        var cleaner = await db.Users.Include(u => u.Area)
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.Role == UserRole.Cleaner && u.IsActive);
        if (cleaner is null)
        {
            return Bad("ต้องเลือกแม่บ้านที่ใช้งานอยู่");
        }

        // facility-0069: attendance counts for the Cleaner's own regular shift
        if (cleaner.Shift != shift)
        {
            return Bad("แก้ได้เฉพาะกะประจำของแม่บ้านคนนี้");
        }

        var slot = new ShiftSlot(shiftDate, shift);
        if (AttendanceCalendar.PlaceTime(slot, time, settings) is not { } at)
        {
            return Bad($"เวลานี้อยู่นอกช่วงลงเวลาของกะนี้ ({settings.OpensMinutesBeforeShift} นาทีก่อนกะถึง {settings.ClosesMinutesAfterShift} นาทีหลังกะ)");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (at > now)
        {
            return Bad("เวลาที่ใส่ยังมาไม่ถึง");
        }

        var events = await db.ShiftAttendances
            .Where(a => a.UserId == cleaner.Id && a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift)
            .ToListAsync();
        var existing = events.FirstOrDefault(a => a.EventType == eventType);
        if (existing is null && cleaner.AreaId is null)
        {
            return Bad("แม่บ้านคนนี้ยังไม่มี Area");
        }

        var proposed = events.ToDictionary(a => a.EventType, a => a.OccurredAt);
        proposed[eventType] = at;
        if (AttendanceRules.CheckTimes(proposed) is { } orderError)
        {
            return Bad(orderError);
        }

        if (existing is not null && existing.OccurredAt == at)
        {
            // Saving the same time again changes nothing, so it logs nothing
            return Results.Ok(AttendanceBoard.ToDto(existing));
        }

        var summary = $"{(existing is null ? "เพิ่ม" : "แก้")}เวลา{ThaiLabels.EventName(eventType)}ของ {cleaner.DisplayName} "
            + $"{cleaner.Area?.Code} กะ{ThaiLabels.ShiftName(shift)} {ThaiLabels.Date(shiftDate)}: "
            + $"{(existing is null ? "–" : ThaiLabels.Clock(existing.OccurredAt))} → {ThaiLabels.Clock(at)}";
        ShiftAttendance row;
        if (existing is null)
        {
            row = new ShiftAttendance
            {
                UserId = cleaner.Id,
                AreaId = cleaner.AreaId!.Value,
                ShiftDate = slot.ShiftDate,
                Shift = slot.Shift,
                EventType = eventType,
                OccurredAt = at,
                Source = AttendanceSource.AdminAdd,
                CreatedAt = now,
            };
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.ShiftAttendances.Add(row);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The Cleaner scanned the same event a moment ago and the unique index kept theirs (facility-0026)
                return ApiResults.Message(StatusCodes.Status409Conflict, "มีการลงเวลารายการนี้แล้ว โหลดหน้าใหม่แล้วลองอีกครั้ง");
            }

            AuditTrail.Add(db, admin.Id, now, "ATTENDANCE_ADD", "shift_attendances", row.Id, summary, before: null, after: Snapshot(row), reason);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        else
        {
            var before = Snapshot(existing);
            existing.OccurredAt = at;
            if (existing.Source == AttendanceSource.Scan)
            {
                existing.Source = AttendanceSource.AdminEdit;
            }

            AuditTrail.Add(db, admin.Id, now, "ATTENDANCE_EDIT", "shift_attendances", existing.Id, summary, before, Snapshot(existing), reason);
            await db.SaveChangesAsync();
            row = existing;
        }

        await AttendanceBoard.NotifyAsync(hub, row.AreaId, slot);
        return Results.Ok(AttendanceBoard.ToDto(row));
    }

    private static object Snapshot(ShiftAttendance row) => new
    {
        EventType = row.EventType.ToString(),
        OccurredAt = DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc),
        Source = row.Source.ToString(),
    };

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AttendanceCorrectionEndpointTests|FullyQualifiedName~AttendanceBoardEndpointTests"`
Expected: PASS, 27 tests (14 correction tests and 13 board tests).

Then run `dotnet test backend/FacilityRealtime.slnx`. Expected: 0 failures.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Api backend/tests/FacilityRealtime.ApiTests/Admin
git commit -m "feat(backend): Admin adds or moves attendance times with a reason, logged with the old time (facility-0051)"
```

---

### Task 5: Alert list (D3) and "คุยแล้ว ปิดเตือน"

**Files:**
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/AlertQuery.cs`
- Create: `backend/src/FacilityRealtime.Api/DTOs/AdminAlertDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminAlertEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminQuery.cs` (add `ReadDateRange`), `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs` (add `AlertName`), `backend/src/FacilityRealtime.Api/Program.cs`
- Modify (tests): `Admin/AdminAttendanceModels.cs`, `Admin/AdminAttendanceApi.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AlertEndpointTests.cs`

**Interfaces:**
- Consumes:
  - `AlertKind`, `AlertKinds.ReviewType`, `AlertReview`, `AlertType` (Task 2).
  - `AttendanceRules.MissingAtShiftEnd` (Task 1), `AttendanceCalendar.SlotFor`, `ShiftCalendar.EndUtc`, `ThaiTime`.
- Produces (Infrastructure):
  - `sealed record AlertRow(AlertKind Kind, long RecordId, DateTime At, User Person, Area Area, Sign? Sign, short? DistanceM, short? AccuracyM, IReadOnlyList<AttendanceEvent> MissingEvents, AlertReview? Review, ShiftSlot? CoverSlot)`.
  - `AlertQuery.LoadAsync(AppDbContext db, DateTime fromUtc, DateTime toUtc, DateTime nowUtc, AttendanceSettings settings) : Task<IReadOnlyList<AlertRow>>`, newest first.
- Produces (Api.DTOs):
  - `AlertPersonDto(int Id, string EmployeeId, string DisplayName, UserRole Role, string? AreaCode)`.
  - `CoverSuggestionDto(int UserId, int AreaId, DateOnly ShiftDate, Shift Shift)`.
  - `AlertDto(AlertKind Kind, long RecordId, DateTime At, AlertPersonDto Person, string AreaCode, string AreaName, string? SignCode, string? PointName, short? DistanceM, short? RadiusM, short? AccuracyM, IReadOnlyList<AttendanceEvent> MissingEvents, bool Reviewed, DateTime? ReviewedAt, CoverSuggestionDto? Cover)`.
  - `ReviewAlertRequest(AlertKind? Kind, long RecordId, string? Note)`.
- Produces (Api.Endpoints, internal):
  - `AdminQuery.ReadDateRange(string? from, string? to, DateTime nowUtc, int defaultDays, int maxDays, out DateTime fromUtc, out DateTime toUtc) : string?`.
  - `ThaiLabels.AlertName(AlertKind) : string`.
  - `AdminAlertEndpoints.AddReview(AppDbContext db, AlertType type, long recordId, string table, int adminId, DateTime now, string summary, string? note)`, which adds the `alert_reviews` row and its `ALERT_REVIEW` audit row without saving.
- Produces (routes):
  - `GET /api/admin/alerts?from=&to=` returns 200 `List<AlertDto>`, or 400 for a bad range.
  - `POST /api/admin/alerts/review` returns 204. Errors: 400 without a kind, for MissingAttendance, or for a note longer than 500; 404 when it is not an alert; 409 when it was already closed.
- Produces (tests):
  - `AdminAttendanceApi.AlertsPath`.
  - `AlertsAsync(HttpClient admin, string query = "") : Task<List<AlertModel>>`.
  - `ReviewAsync(HttpClient admin, string kind, long recordId, string? note = null)`.
  - Models `AlertModel`, `AlertPersonModel`, `CoverSuggestionModel`.

- [ ] **Step 1: Write the failing tests**

In `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceModels.cs`, add at the end of the file:

```csharp
public record AlertPersonModel(int Id, string EmployeeId, string DisplayName, string Role, string? AreaCode);

public record CoverSuggestionModel(int UserId, int AreaId, DateOnly ShiftDate, string Shift);

public record AlertModel(
    string Kind, long RecordId, DateTime At, AlertPersonModel Person, string AreaCode, string AreaName,
    string? SignCode, string? PointName, short? DistanceM, short? RadiusM, short? AccuracyM,
    List<string> MissingEvents, bool Reviewed, DateTime? ReviewedAt, CoverSuggestionModel? Cover);
```

In `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceApi.cs`, add inside the class, after `CorrectAsync`:

```csharp
    public const string AlertsPath = "/api/admin/alerts";

    public static async Task<List<AlertModel>> AlertsAsync(HttpClient admin, string query = "")
    {
        var response = await admin.GetAsync(AlertsPath + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<AlertModel>>())!;
    }

    public static Task<HttpResponseMessage> ReviewAsync(HttpClient admin, string kind, long recordId, string? note = null) =>
        admin.PostAsJsonAsync(AlertsPath + "/review", new { kind, recordId, note });
```

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AlertEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// Wireframe D3: Blocked Scans (facility-0041), scans far from their sign (facility-0035/0037) and missing Shift-Outs
/// (facility-0051). The default range is yesterday and today, Thai time. Log in before moving the clock.
/// </summary>
public class AlertEndpointTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    /// <summary>E1001 (AR01, Day) checks in at 06:50, then scans AR02's check-in sign at 07:10 and is blocked.</summary>
    private static async Task BlockedAtAr02Async(FacilityApiFactory factory, HttpClient cleaner)
    {
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 10));
        Assert.Equal(HttpStatusCode.Forbidden, (await AttendanceApi.RecordAsync(cleaner, "ShiftIn", "token-checkin-ar02")).StatusCode);
    }

    private static async Task FarCheckInAsync(FacilityApiFactory factory, HttpClient cleaner)
    {
        factory.Clock.SetUtcNow(ThaiClock.At(8, 6, 50));
        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_blocked_scan_is_listed_with_the_cover_the_admin_can_assign()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await BlockedAtAr02Async(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("BlockedScan", alert.Kind);
        Assert.Equal(ThaiClock.At(8, 7, 10).UtcDateTime, alert.At);
        Assert.Equal("E1001", alert.Person.EmployeeId);
        Assert.Equal("Cleaner", alert.Person.Role);
        Assert.Equal("AR01", alert.Person.AreaCode);
        Assert.Equal("AR02", alert.AreaCode);
        Assert.Equal("AR02-IN", alert.SignCode);
        Assert.False(alert.Reviewed);
        Assert.Equal(
            new CoverSuggestionModel(alert.Person.Id, await AdminSetupApi.AreaIdAsync(factory, "AR02"), Oct8, "Day"),
            alert.Cover);
    }

    [Fact]
    public async Task The_cover_offer_goes_once_the_cover_exists()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await BlockedAtAr02Async(factory, cleaner);
        await TestData.AddCoverAsync(factory, "E1001", "AR02", Oct8, Shift.Day);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        Assert.Null(Assert.Single(await AdminAttendanceApi.AlertsAsync(admin)).Cover);
    }

    [Fact]
    public async Task The_cover_offer_goes_once_the_shift_is_over()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await BlockedAtAr02Async(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(9, 10, 0));

        var alerts = await AdminAttendanceApi.AlertsAsync(admin, "?from=2026-10-08&to=2026-10-09");

        Assert.Null(alerts.Single(a => a.Kind == "BlockedScan").Cover);
    }

    [Fact]
    public async Task A_check_in_far_from_its_sign_is_listed_with_distance_and_radius()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await FarCheckInAsync(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("AttendanceGps", alert.Kind);
        Assert.Equal("AR01-IN", alert.SignCode);
        Assert.Equal("AR01", alert.AreaCode);
        Assert.True(alert.DistanceM > 50);
        Assert.Equal((short)50, alert.RadiusM);
        Assert.Equal((short)10, alert.AccuracyM);
        Assert.Null(alert.Cover);
    }

    [Fact]
    public async Task A_submission_far_from_its_sign_is_listed()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 0));
        await factory.WithDbAsync(async db =>
        {
            var scan = await db.ScanRecords.SingleAsync(s => s.Id == scanId);
            scan.WithinRadius = false;
            scan.DistanceM = 120;
            await db.SaveChangesAsync();
        });
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 0));

        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        Assert.Equal("SubmissionGps", alert.Kind);
        Assert.Equal(scanId, alert.RecordId);
        Assert.Equal("AR01-01", alert.SignCode);
        Assert.Equal("ห้องน้ำชาย ชั้น 1", alert.PointName);
        Assert.Equal((short)120, alert.DistanceM);
    }

    [Fact]
    public async Task A_missing_shift_out_shows_when_the_shift_ends_and_goes_when_the_admin_fills_it()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 18, 0));
        Assert.Empty(await AdminAttendanceApi.AlertsAsync(admin));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 19, 30));
        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));
        Assert.Equal("MissingAttendance", alert.Kind);
        Assert.Equal(ThaiClock.At(8, 19, 0).UtcDateTime, alert.At);
        Assert.Equal(new[] { "ShiftOut" }, alert.MissingEvents);
        Assert.Equal("AR01", alert.AreaCode);
        Assert.Null(alert.SignCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AdminAttendanceApi.ReviewAsync(admin, "MissingAttendance", alert.RecordId)).StatusCode);

        var fill = await AdminAttendanceApi.CorrectAsync(admin, alert.Person.Id, Oct8, "Day", "ShiftOut", "18:58", "ลืมสแกนเลิกงาน");

        Assert.Equal(HttpStatusCode.OK, fill.StatusCode);
        Assert.Empty(await AdminAttendanceApi.AlertsAsync(admin));
    }

    [Fact]
    public async Task Reviewing_closes_the_alert_once_and_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await FarCheckInAsync(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));

        var review = await AdminAttendanceApi.ReviewAsync(admin, "AttendanceGps", alert.RecordId, "GPS ในอาคารเพี้ยน ยืนอยู่หน้าป้ายจริง");
        var again = await AdminAttendanceApi.ReviewAsync(admin, "AttendanceGps", alert.RecordId);

        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var reviewed = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));
        Assert.True(reviewed.Reviewed);
        Assert.Equal(ThaiClock.At(8, 7, 30).UtcDateTime, reviewed.ReviewedAt);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ALERT_REVIEW", entry.Action);
        Assert.Equal(adminAuth.User.Id, entry.ActorId);
        Assert.Equal("shift_attendances", entry.EntityType);
        Assert.Equal(alert.RecordId, entry.EntityId);
        Assert.Equal("GPS ในอาคารเพี้ยน ยืนอยู่หน้าป้ายจริง", entry.Reason);
    }

    [Theory]
    [InlineData("AttendanceGps")]   // the check-in was near its sign: not an alert
    [InlineData("BlockedScan")]     // no Blocked Scan has this id
    public async Task Only_real_alerts_can_be_reviewed(string kind)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        var attendanceId = await AdminSetupApi.ReadAsync(factory, db => db.ShiftAttendances.Select(a => a.Id).SingleAsync());

        var response = await AdminAttendanceApi.ReviewAsync(admin, kind, attendanceId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_review_needs_a_kind()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(AdminAttendanceApi.AlertsPath + "/review", new { recordId = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("?from=2026-10-08")]
    [InlineData("?from=2026-10-09&to=2026-10-08")]
    [InlineData("?from=2026-09-01&to=2026-10-08")]
    [InlineData("?from=8/10/2026&to=2026-10-08")]
    public async Task A_date_range_needs_two_dates_in_order_and_at_most_31_days(string query)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.GetAsync(AdminAttendanceApi.AlertsPath + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_admins_see_and_close_alerts()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(AdminAttendanceApi.AlertsPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AdminAttendanceApi.ReviewAsync(cleaner, "BlockedScan", 1)).StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AlertEndpointTests"`
Expected: FAIL. `/api/admin/alerts` does not exist, so the requests give 404.

- [ ] **Step 3: Write the implementation**

Create `backend/src/FacilityRealtime.Infrastructure/Persistence/AlertQuery.cs`:

```csharp
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>
/// One line of the alert list (wireframe D3). Area is where it happened: the scanned sign's Area, or for a missing
/// event the Area the Cleaner checked in to. CoverSlot is set when the Admin can still let this Cleaner cover that Area.
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
    ShiftSlot? CoverSlot);

public static class AlertQuery
{
    /// <summary>Every alert whose time falls in [fromUtc, toUtc), newest first. Sign comes with its Area and point; Person with their Area.</summary>
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

        var signIds = attendanceFlags.Select(a => a.SignId!.Value)
            .Concat(submissionFlags.Select(s => s.SignId))
            .Concat(blocked.Select(b => b.SignId))
            .Distinct()
            .ToList();
        var signs = await db.Signs.AsNoTracking()
            .Include(s => s.Area)
            .Include(s => s.ServicePoint)
            .Where(s => signIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);
        var userIds = attendanceFlags.Select(a => a.UserId)
            .Concat(submissionFlags.Select(s => s.UserId))
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
            .Concat(blocked.Select(b => b.Id))
            .Distinct()
            .ToList();
        var reviews = await db.AlertReviews.AsNoTracking().Where(r => recordIds.Contains(r.RecordId)).ToListAsync();
        var blockedUserIds = blocked.Select(b => b.UserId).Distinct().ToList();
        var activeCovers = await db.CoverAssignments.AsNoTracking()
            .Where(c => c.CancelledAt == null && c.ShiftDate >= firstDate && blockedUserIds.Contains(c.UserId))
            .ToListAsync();

        AlertReview? ReviewOf(AlertType type, long id) => reviews.FirstOrDefault(r => r.AlertType == type && r.RecordId == id);

        // facility-0041 rule 4: offer a cover for the scan's own shift while that shift has not ended and none exists yet
        ShiftSlot? CoverSlotFor(BlockedScan scan)
        {
            var user = users[scan.UserId];
            var area = signs[scan.SignId].Area!;
            if (user is not { Role: UserRole.Cleaner, IsActive: true, Shift: { } shift } || user.AreaId == area.Id || !area.HasShift(shift))
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

        var rows = new List<AlertRow>();
        rows.AddRange(attendanceFlags.Select(a => new AlertRow(
            AlertKind.AttendanceGps, a.Id, a.OccurredAt, users[a.UserId], signs[a.SignId!.Value].Area!, signs[a.SignId!.Value],
            a.DistanceM, a.AccuracyM, [], ReviewOf(AlertType.AttendanceGps, a.Id), null)));
        rows.AddRange(submissionFlags.Select(s => new AlertRow(
            AlertKind.SubmissionGps, s.Id, s.SubmittedAt, users[s.UserId], signs[s.SignId].Area!, signs[s.SignId],
            s.DistanceM, s.AccuracyM, [], ReviewOf(AlertType.SubmissionGps, s.Id), null)));
        rows.AddRange(blocked.Select(b => new AlertRow(
            AlertKind.BlockedScan, b.Id, b.ScannedAt, users[b.UserId], signs[b.SignId].Area!, signs[b.SignId],
            b.DistanceM, b.AccuracyM, [], ReviewOf(AlertType.BlockedScan, b.Id), CoverSlotFor(b))));
        rows.AddRange(missing.Select(m => new AlertRow(
            AlertKind.MissingAttendance, m.ShiftIn!.Id, m.End, users[m.UserId], areas[m.ShiftIn.AreaId], null,
            null, null, m.Missing, null, null)));

        return rows.OrderByDescending(r => r.At).ThenByDescending(r => r.RecordId).ToList();
    }
}
```

Create `backend/src/FacilityRealtime.Api/DTOs/AdminAlertDtos.cs`:

```csharp
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record AlertPersonDto(int Id, string EmployeeId, string DisplayName, UserRole Role, string? AreaCode);

/// <summary>What to send to POST /api/admin/cover-assignments, with the Blocked Scan's id, to let this Cleaner cover that Area.</summary>
public record CoverSuggestionDto(int UserId, int AreaId, DateOnly ShiftDate, Shift Shift);

/// <summary>
/// One line of the alert list (wireframe D3). RecordId is the row in shift_attendances, scan_records or blocked_scans;
/// for MissingAttendance it is the Shift-In. At is when it happened; for MissingAttendance, when the shift ended.
/// AreaCode is where it happened. RadiusM is the sign's radius now. Cover is set while a cover can still be assigned.
/// </summary>
public record AlertDto(
    AlertKind Kind,
    long RecordId,
    DateTime At,
    AlertPersonDto Person,
    string AreaCode,
    string AreaName,
    string? SignCode,
    string? PointName,
    short? DistanceM,
    short? RadiusM,
    short? AccuracyM,
    IReadOnlyList<AttendanceEvent> MissingEvents,
    bool Reviewed,
    DateTime? ReviewedAt,
    CoverSuggestionDto? Cover);

/// <summary>"คุยแล้ว ปิดเตือน". Note is optional and kept as the audit row's reason.</summary>
public record ReviewAlertRequest(AlertKind? Kind, long RecordId, string? Note);
```

In `backend/src/FacilityRealtime.Api/Endpoints/AdminQuery.cs`, add `using FacilityRealtime.Application.Common;` to the usings. Then add this method inside the class, after `ReadSlot`:

```csharp
    /// <summary>
    /// Thai dates from and to, both included, as the UTC range [from 00:00, the day after to 00:00) Thai time.
    /// Neither given: the <paramref name="defaultDays"/> days ending today. Returns the error to show, or null.
    /// </summary>
    public static string? ReadDateRange(
        string? from, string? to, DateTime nowUtc, int defaultDays, int maxDays, out DateTime fromUtc, out DateTime toUtc)
    {
        fromUtc = toUtc = default;
        DateOnly first, last;
        if (from is null && to is null)
        {
            last = DateOnly.FromDateTime(ThaiTime.FromUtc(nowUtc));
            first = last.AddDays(1 - defaultDays);
        }
        else if (!TryReadDate(from, out first) || !TryReadDate(to, out last))
        {
            return "ต้องระบุทั้งวันเริ่ม (from) และวันสุดท้าย (to) แบบ yyyy-MM-dd";
        }

        if (last < first)
        {
            return "วันสุดท้ายต้องไม่ก่อนวันเริ่ม";
        }

        if (last.DayNumber - first.DayNumber + 1 > maxDays)
        {
            return $"เลือกได้ไม่เกิน {maxDays} วัน";
        }

        fromUtc = ThaiTime.ToUtc(first.ToDateTime(TimeOnly.MinValue));
        toUtc = ThaiTime.ToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue));
        return null;
    }
```

In `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs`, add `using FacilityRealtime.Application.Alerts;` to the usings. Then add inside the class, after `Date`:

```csharp
    public static string AlertName(AlertKind kind) => kind switch
    {
        AlertKind.BlockedScan => "สแกนนอก Area",
        AlertKind.AttendanceGps => "ลงเวลาไกลจากป้าย",
        AlertKind.SubmissionGps => "ส่งงานไกลจากป้าย",
        _ => "ไม่ได้ลงเวลาเลิกงาน",
    };
```

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminAlertEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>The alert list (wireframe D3): Blocked Scans, scans far from their sign and missing Shift-Outs. Closing one is logged (facility-0051).</summary>
public static class AdminAlertEndpoints
{
    public const int MaxNoteLength = 500;

    public static IEndpointRouteBuilder MapAdminAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/alerts").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/", ListAsync);
        admin.MapPost("/review", ReviewAsync);
        return app;
    }

    /// <summary>Yesterday and today by default; the page splits it into "ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด" and by kind.</summary>
    private static async Task<IResult> ListAsync(
        string? from, string? to, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (AdminQuery.ReadDateRange(from, to, now, defaultDays: 2, maxDays: 31, out var fromUtc, out var toUtc) is { } error)
        {
            return Bad(error);
        }

        var rows = await AlertQuery.LoadAsync(db, fromUtc, toUtc, now, settings);
        return Results.Ok(rows.Select(ToDto).ToList());
    }

    /// <summary>"คุยแล้ว ปิดเตือน" (facility-0035, 0041). A missing Shift-Out is closed by filling the time instead (facility-0051).</summary>
    private static async Task<IResult> ReviewAsync(ReviewAlertRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.Kind is not { } kind || !Enum.IsDefined(kind))
        {
            return Bad("ต้องระบุประเภทของเตือน");
        }

        if (AlertKinds.ReviewType(kind) is not { } type)
        {
            return Bad("เตือนนี้ปิดเองเมื่อ Admin เติมเวลาที่ขาด");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            return Bad($"หมายเหตุยาวได้ไม่เกิน {MaxNoteLength} ตัวอักษร");
        }

        var target = await FindAsync(db, type, request.RecordId);
        if (target is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบรายการเตือนนี้");
        }

        if (await db.AlertReviews.AnyAsync(r => r.AlertType == type && r.RecordId == request.RecordId))
        {
            return AlreadyReviewed();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        AddReview(
            db, type, request.RecordId, target.Table, admin.Id, now,
            $"ปิดเตือน{ThaiLabels.AlertName(kind)} ของ {target.Name} เวลา {ThaiLabels.Clock(target.At)}",
            note);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another Admin closed it a moment ago; the unique index kept theirs
            return AlreadyReviewed();
        }

        return Results.NoContent();
    }

    /// <summary>Adds the alert_reviews row and its audit row; the caller saves them with its own change.</summary>
    internal static void AddReview(
        AppDbContext db, AlertType type, long recordId, string table, int adminId, DateTime now, string summary, string? note)
    {
        db.AlertReviews.Add(new AlertReview { AlertType = type, RecordId = recordId, ReviewedById = adminId, ReviewedAt = now });
        AuditTrail.Add(
            db, adminId, now, "ALERT_REVIEW", table, recordId, summary,
            before: new { Reviewed = false },
            after: new { Reviewed = true, AlertType = type.ToString() },
            reason: note);
    }

    private sealed record AlertTarget(string Table, string Name, DateTime At);

    /// <summary>The row behind the alert, only while it is one: a GPS flag must still be outside the radius.</summary>
    private static async Task<AlertTarget?> FindAsync(AppDbContext db, AlertType type, long id) => type switch
    {
        AlertType.AttendanceGps => await db.ShiftAttendances
            .Where(a => a.Id == id && a.WithinRadius == false)
            .Select(a => new AlertTarget("shift_attendances", a.User!.DisplayName, a.OccurredAt))
            .FirstOrDefaultAsync(),
        AlertType.SubmissionGps => await db.ScanRecords
            .Where(s => s.Id == id && s.WithinRadius == false)
            .Select(s => new AlertTarget("scan_records", s.User!.DisplayName, s.SubmittedAt))
            .FirstOrDefaultAsync(),
        _ => await db.BlockedScans
            .Where(b => b.Id == id)
            .Join(db.Users, b => b.UserId, u => u.Id, (b, u) => new AlertTarget("blocked_scans", u.DisplayName, b.ScannedAt))
            .FirstOrDefaultAsync(),
    };

    private static AlertDto ToDto(AlertRow row) => new(
        row.Kind,
        row.RecordId,
        Utc(row.At),
        new AlertPersonDto(row.Person.Id, row.Person.LoginName, row.Person.DisplayName, row.Person.Role, row.Person.Area?.Code),
        row.Area.Code,
        row.Area.Name,
        row.Sign?.Code,
        row.Sign?.ServicePoint?.Name,
        row.DistanceM,
        row.Sign?.RadiusM,
        row.AccuracyM,
        row.MissingEvents,
        row.Review is not null,
        row.Review is { } review ? Utc(review.ReviewedAt) : (DateTime?)null,
        row.CoverSlot is { } slot ? new CoverSuggestionDto(row.Person.Id, row.Area.Id, slot.ShiftDate, slot.Shift) : null);

    /// <summary>The database returns Unspecified; marking it UTC makes the JSON end in "Z".</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);

    private static IResult AlreadyReviewed() => ApiResults.Message(StatusCodes.Status409Conflict, "ปิดเตือนนี้ไปแล้ว");

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add after `app.MapAdminAttendanceEndpoints();`:

```csharp
app.MapAdminAlertEndpoints();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AlertEndpointTests"`
Expected: PASS, 15 tests.

Then run `dotnet test backend/FacilityRealtime.slnx`. Expected: 0 failures.

- [ ] **Step 5: Commit**

```bash
git add backend/src backend/tests/FacilityRealtime.ApiTests/Admin
git commit -m "feat(backend): Admin alert list with blocked scans, GPS flags and missing shift-outs; close an alert once, logged (facility-0041, 0035, 0051)"
```

---

### Task 6: Assign a cover from a Blocked Scan, and the candidates list

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/DTOs/CoverDtos.cs`
- Replace: `backend/src/FacilityRealtime.Api/Endpoints/CoverAssignmentEndpoints.cs`
- Modify (tests): `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceModels.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/CoverFromAlertTests.cs`

**Interfaces:**
- Consumes:
  - `AdminAlertEndpoints.AddReview` and `ThaiLabels.AlertName` (Task 5).
  - `ThaiLabels.ShiftName`, `Date` and `Clock` (Task 4).
  - `AdminQuery.TryReadDate` and `TryReadEnum` (Task 3).
  - `AttendanceSettings`.
- Produces (Api.DTOs):
  - `AssignCoverRequest(int UserId, int AreaId, DateOnly ShiftDate, Shift? Shift, long? BlockedScanId = null)`.
  - `CoverCandidateDto(int Id, string EmployeeId, string DisplayName, string AreaCode, DateTime? ShiftInAt, DateTime? ScannedThisAreaAt)`.
- Produces (routes):
  - `POST /api/admin/cover-assignments` with `blockedScanId`:
    - 400 when the Blocked Scan is unknown, or is not this Cleaner's at a sign of this Area.
    - On success it also writes `alert_reviews` and an `ALERT_REVIEW` row, unless the alert is already closed.
  - `GET /api/admin/cover-assignments/candidates?areaId=&shiftDate=&shift=`:
    - 200 `List<CoverCandidateDto>`: those who scanned that Area's signs in that shift come first (latest scan first), then those who checked in (earliest first), then the rest by employee ID.
    - 400 when a value is missing or the Area has no such shift; 404 when the Area is unknown or deactivated.

- [ ] **Step 1: Write the failing tests**

In `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceModels.cs`, add at the end of the file:

```csharp
public record CoverCandidateModel(int Id, string EmployeeId, string DisplayName, string AreaCode, DateTime? ShiftInAt, DateTime? ScannedThisAreaAt);
```

Create `backend/tests/FacilityRealtime.ApiTests/Admin/CoverFromAlertTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0041 rule 4: the Admin assigns a cover straight from a Blocked Scan, choosing among the shift's Cleaners.</summary>
public class CoverFromAlertTests
{
    private const string CandidatesPath = "/api/admin/cover-assignments/candidates";

    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    /// <summary>E1001 (AR01, Day) checks in at 06:50 and scans AR02's check-in sign at 09:10; returns that Blocked Scan's id.</summary>
    private static async Task<long> BlockedAtAr02Async(FacilityApiFactory factory, HttpClient cleaner)
    {
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 10));
        Assert.Equal(HttpStatusCode.Forbidden, (await AttendanceApi.RecordAsync(cleaner, "ShiftIn", "token-checkin-ar02")).StatusCode);
        return await AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.Select(b => b.Id).SingleAsync());
    }

    /// <summary>A third Area in building A with its check-in sign, so a third Day Cleaner can have an Area.</summary>
    private static Task AddAreaAsync(FacilityApiFactory factory, string code) =>
        factory.WithDbAsync(async db =>
        {
            var building = await db.Buildings.SingleAsync(b => b.Code == "A");
            var area = new Area { BuildingId = building.Id, Code = code, Name = $"Area {code}", CreatedAt = DateTime.UtcNow };
            db.Signs.Add(new Sign { Area = area, Code = $"{code}-IN", QrToken = $"token-checkin-{code.ToLowerInvariant()}", QrIssuedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

    private static Task AddShiftInAsync(FacilityApiFactory factory, string employeeId, DateTimeOffset at) =>
        factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            db.ShiftAttendances.Add(new ShiftAttendance
            {
                UserId = user.Id,
                AreaId = user.AreaId!.Value,
                ShiftDate = Oct8,
                Shift = Shift.Day,
                EventType = AttendanceEvent.ShiftIn,
                OccurredAt = at.UtcDateTime,
                Source = AttendanceSource.Scan,
                CreatedAt = at.UtcDateTime,
            });
            await db.SaveChangesAsync();
        });

    private static Task<HttpResponseMessage> AssignAsync(HttpClient admin, int userId, int areaId, long? blockedScanId) =>
        admin.PostAsJsonAsync("/api/admin/cover-assignments", new { userId, areaId, shiftDate = Oct8, shift = "Day", blockedScanId });

    private static async Task<List<CoverCandidateModel>> CandidatesAsync(HttpClient admin, string query)
    {
        var response = await admin.GetAsync(CandidatesPath + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<CoverCandidateModel>>())!;
    }

    [Fact]
    public async Task Assigning_from_a_blocked_scan_closes_that_alert()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var blockedId = await BlockedAtAr02Async(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 30));
        var suggestion = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin)).Cover!;

        var response = await AssignAsync(admin, suggestion.UserId, suggestion.AreaId, blockedId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var alert = Assert.Single(await AdminAttendanceApi.AlertsAsync(admin));
        Assert.True(alert.Reviewed);
        Assert.Null(alert.Cover);
        var audit = await AdminSetupApi.AuditAsync(factory);
        Assert.Equal(new[] { "COVER_ASSIGN", "ALERT_REVIEW" }, audit.Select(a => a.Action));
        Assert.Equal("blocked_scans", audit[1].EntityType);
        Assert.Equal(blockedId, audit[1].EntityId);
        Assert.Equal("Covered", (await AdminAttendanceApi.BoardAsync(admin)).Rows.Single(r => r.AreaCode == "AR02").State);
    }

    [Fact]
    public async Task An_alert_already_closed_is_not_closed_twice()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var blockedId = await BlockedAtAr02Async(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 30));
        Assert.Equal(HttpStatusCode.NoContent, (await AdminAttendanceApi.ReviewAsync(admin, "BlockedScan", blockedId)).StatusCode);

        var response = await AssignAsync(
            admin, await AdminSetupApi.UserIdAsync(factory, "E1001"), await AdminSetupApi.AreaIdAsync(factory, "AR02"), blockedId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(new[] { "ALERT_REVIEW", "COVER_ASSIGN" }, (await AdminSetupApi.AuditAsync(factory)).Select(a => a.Action));
    }

    [Fact]
    public async Task The_blocked_scan_must_be_this_cleaners_at_this_area()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddAreaAsync(factory, "AR03");
        var blockedId = await BlockedAtAr02Async(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 30));
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var otherArea = await AssignAsync(admin, e1001, await AdminSetupApi.AreaIdAsync(factory, "AR03"), blockedId);
        var unknown = await AssignAsync(admin, e1001, await AdminSetupApi.AreaIdAsync(factory, "AR02"), blockedId + 100);

        Assert.Equal(HttpStatusCode.BadRequest, otherArea.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.CoverAssignments.ToListAsync()));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Candidates_put_whoever_scanned_the_area_first_then_who_checked_in()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddAreaAsync(factory, "AR03");
        await TestData.AddCleanerAsync(factory, "E1009", Shift.Day, "AR03");
        await TestData.AddCleanerAsync(factory, "E1010", Shift.Day);   // no Area: no sign to check in at
        await AddShiftInAsync(factory, "E1009", ThaiClock.At(8, 6, 40));
        await BlockedAtAr02Async(factory, cleaner);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 30));
        var query = $"?areaId={await AdminSetupApi.AreaIdAsync(factory, "AR02")}&shiftDate=2026-10-08&shift=Day";

        var candidates = await CandidatesAsync(admin, query);

        // E1003 is AR02's own Cleaner and E1002 works nights: neither is offered
        Assert.Equal(new[] { "E1001", "E1009" }, candidates.Select(c => c.EmployeeId));
        Assert.Equal(ThaiClock.At(8, 9, 10).UtcDateTime, candidates[0].ScannedThisAreaAt);
        Assert.Equal(ThaiClock.At(8, 6, 50).UtcDateTime, candidates[0].ShiftInAt);
        Assert.Equal("AR01", candidates[0].AreaCode);
        Assert.Equal("AR03", candidates[1].AreaCode);
        Assert.Null(candidates[1].ScannedThisAreaAt);

        await TestData.AddCoverAsync(factory, "E1009", "AR02", Oct8, Shift.Day);
        Assert.Equal(new[] { "E1001" }, (await CandidatesAsync(admin, query)).Select(c => c.EmployeeId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?shiftDate=2026-10-08&shift=Day")]
    [InlineData("?areaId=1&shiftDate=2026-10-08")]
    [InlineData("?areaId=1&shift=Day")]
    public async Task Candidates_need_an_area_a_date_and_a_shift(string query)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(CandidatesPath + query)).StatusCode);
    }

    [Fact]
    public async Task Candidates_need_an_active_area_that_works_that_shift()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");

        var unknown = await admin.GetAsync($"{CandidatesPath}?areaId={ar02 + 100}&shiftDate=2026-10-08&shift=Day");
        var dayOnlyAtNight = await admin.GetAsync($"{CandidatesPath}?areaId={ar02}&shiftDate=2026-10-08&shift=Night");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, dayOnlyAtNight.StatusCode);
    }

    [Fact]
    public async Task Only_admins_see_candidates()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.GetAsync($"{CandidatesPath}?areaId=1&shiftDate=2026-10-08&shift=Day");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~CoverFromAlertTests"`
Expected: FAIL.
- The candidates route does not exist, so it gives 405 for GET on that path or 404.
- `blockedScanId` is ignored, so no `ALERT_REVIEW` row is written and the mismatch is not refused.

- [ ] **Step 3: Write the implementation**

In `backend/src/FacilityRealtime.Api/DTOs/CoverDtos.cs`, replace

```csharp
public record AssignCoverRequest(int UserId, int AreaId, DateOnly ShiftDate, Shift? Shift);
```

with

```csharp
/// <summary>BlockedScanId: assigning from that Blocked Scan on the alert list (wireframe D3) also closes its alert.</summary>
public record AssignCoverRequest(int UserId, int AreaId, DateOnly ShiftDate, Shift? Shift, long? BlockedScanId = null);

/// <summary>Who could cover an Area in a shift (wireframe D3): their own Area, their Shift-In, and their latest scan at this Area's signs.</summary>
public record CoverCandidateDto(int Id, string EmployeeId, string DisplayName, string AreaCode, DateTime? ShiftInAt, DateTime? ScannedThisAreaAt);
```

Replace the whole of `backend/src/FacilityRealtime.Api/Endpoints/CoverAssignmentEndpoints.cs` with:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0041: the Admin lets a Cleaner work another Area for one shift. Every change is logged (facility-0051).</summary>
public static class CoverAssignmentEndpoints
{
    public static IEndpointRouteBuilder MapCoverAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/cover-assignments").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapPost("/", AssignAsync);
        admin.MapDelete("/{id:int}", CancelAsync);
        admin.MapGet("/candidates", ListCandidatesAsync);
        return app;
    }

    private static async Task<IResult> AssignAsync(AssignCoverRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.Shift is not { } shift || !Enum.IsDefined(shift))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุกะ");
        }

        var cleaner = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.Role == UserRole.Cleaner && u.IsActive);
        if (cleaner is null)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องเลือกแม่บ้านที่ใช้งานอยู่");
        }

        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == request.AreaId && a.IsActive);
        if (area is null)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ไม่พบ Area นี้ หรือปิดใช้งานแล้ว");
        }

        if (area.Id == cleaner.AreaId)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "Area นี้เป็น Area ประจำของแม่บ้านคนนี้อยู่แล้ว");
        }

        // Owner decision 2026-10-09: a cover only in the Cleaner's own regular shift
        if (shift != cleaner.Shift)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ทำแทนได้เฉพาะกะประจำของแม่บ้านคนนี้");
        }

        if (!area.HasShift(shift))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "Area นี้ไม่มีกะนี้");
        }

        var slot = new ShiftSlot(request.ShiftDate, shift);
        var now = clock.GetUtcNow().UtcDateTime;
        if (ShiftCalendar.EndUtc(slot) <= now)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "กะนี้จบไปแล้ว");
        }

        var duplicate = await db.CoverAssignments.AnyAsync(c =>
            c.UserId == cleaner.Id && c.AreaId == area.Id && c.ShiftDate == slot.ShiftDate && c.Shift == shift && c.CancelledAt == null);
        if (duplicate)
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
                return ApiResults.Message(StatusCodes.Status400BadRequest, "ไม่พบการสแกนที่ถูกบล็อกนี้");
            }

            var signId = blocked.SignId;
            var signAreaId = await db.Signs.Where(s => s.Id == signId).Select(s => s.AreaId).SingleAsync();
            if (blocked.UserId != cleaner.Id || signAreaId != area.Id)
            {
                return ApiResults.Message(StatusCodes.Status400BadRequest, "การสแกนที่ถูกบล็อกนี้ไม่ใช่ของแม่บ้านคนนี้ที่ Area นี้");
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var cover = new CoverAssignment
        {
            UserId = cleaner.Id,
            AreaId = area.Id,
            ShiftDate = slot.ShiftDate,
            Shift = shift,
            AssignedById = admin.Id,
            AssignedAt = now,
        };
        db.CoverAssignments.Add(cover);
        await db.SaveChangesAsync();
        AuditTrail.Add(
            db, admin.Id, now, "COVER_ASSIGN", "cover_assignments", cover.Id,
            $"มอบหมาย {cleaner.DisplayName} ทำแทน {area.Code} กะ{ThaiLabels.ShiftName(shift)} {ThaiLabels.Date(slot.ShiftDate)}",
            before: null,
            after: new
            {
                cover.UserId,
                cover.AreaId,
                ShiftDate = ThaiLabels.Date(cover.ShiftDate),
                Shift = shift.ToString(),
                BlockedScanId = blocked?.Id,
            });

        // The alert is handled: close it, unless an Admin already did (facility-0041 rule 4)
        if (blocked is { } scan && !await db.AlertReviews.AnyAsync(r => r.AlertType == AlertType.BlockedScan && r.RecordId == scan.Id))
        {
            AdminAlertEndpoints.AddReview(
                db, AlertType.BlockedScan, scan.Id, "blocked_scans", admin.Id, now,
                $"ปิดเตือน{ThaiLabels.AlertName(AlertKind.BlockedScan)} ของ {cleaner.DisplayName} เวลา {ThaiLabels.Clock(scan.ScannedAt)}: มอบหมายทำแทน {area.Code} แล้ว",
                note: null);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Created(
            $"/api/admin/cover-assignments/{cover.Id}",
            new CoverAssignmentDto(cover.Id, cleaner.Id, cleaner.DisplayName, area.Id, area.Code, cover.ShiftDate, shift, DateTime.SpecifyKind(now, DateTimeKind.Utc)));
    }

    private static async Task<IResult> CancelAsync(int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var cover = await db.CoverAssignments
            .Include(c => c.User)
            .Include(c => c.Area)
            .FirstOrDefaultAsync(c => c.Id == id && c.CancelledAt == null);
        if (cover is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบการมอบหมายนี้ หรือยกเลิกไปแล้ว");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        // No unique index guards against a racing duplicate, so close every identical active cover with this one.
        var twins = await db.CoverAssignments
            .Where(c => c.Id != cover.Id && c.UserId == cover.UserId && c.AreaId == cover.AreaId
                && c.ShiftDate == cover.ShiftDate && c.Shift == cover.Shift && c.CancelledAt == null)
            .ToListAsync();
        foreach (var twin in twins.Append(cover))
        {
            twin.CancelledById = admin.Id;
            twin.CancelledAt = now;
        }
        AuditTrail.Add(
            db, admin.Id, now, "COVER_CANCEL", "cover_assignments", cover.Id,
            $"ยกเลิกการทำแทนของ {cover.User!.DisplayName} ที่ {cover.Area!.Code} กะ{ThaiLabels.ShiftName(cover.Shift)} {ThaiLabels.Date(cover.ShiftDate)}",
            before: new { cancelled = false },
            after: new { cancelled = true, alsoCancelled = twins.Select(t => t.Id).ToList() });
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    /// <summary>
    /// Wireframe D3 "ให้ใครทำแทน": active Cleaners of that regular shift with an Area of their own (they check in at its sign),
    /// not already covering this Area. Whoever scanned this Area's signs in the shift comes first, then who checked in.
    /// </summary>
    private static async Task<IResult> ListCandidatesAsync(
        int? areaId, string? shiftDate, string? shift, ClaimsPrincipal principal, AppDbContext db, AttendanceSettings settings)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        if (areaId is null || !AdminQuery.TryReadDate(shiftDate, out var date) || !AdminQuery.TryReadEnum<Shift>(shift, out var parsedShift))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุ Area วันที่ (yyyy-MM-dd) และกะ (Day หรือ Night)");
        }

        var area = await db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId && a.IsActive);
        if (area is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบ Area นี้ หรือปิดใช้งานแล้ว");
        }

        if (!area.HasShift(parsedShift))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "Area นี้ไม่มีกะนี้");
        }

        var slot = new ShiftSlot(date, parsedShift);
        var cleaners = await db.Users.AsNoTracking()
            .Include(u => u.Area)
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.Shift == slot.Shift && u.AreaId != null && u.AreaId != area.Id)
            .ToListAsync();
        var ids = cleaners.Select(u => u.Id).ToList();
        var covering = await db.CoverAssignments.AsNoTracking()
            .Where(c => c.AreaId == area.Id && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Select(c => c.UserId)
            .ToListAsync();
        var shiftIns = await db.ShiftAttendances.AsNoTracking()
            .Where(a => a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift && a.EventType == AttendanceEvent.ShiftIn && ids.Contains(a.UserId))
            .ToListAsync();
        var opens = ShiftCalendar.StartUtc(slot).AddMinutes(-settings.OpensMinutesBeforeShift);
        var closes = ShiftCalendar.EndUtc(slot).AddMinutes(settings.ClosesMinutesAfterShift);
        var areaSignIds = await db.Signs.Where(s => s.AreaId == area.Id).Select(s => s.Id).ToListAsync();
        var scans = await db.BlockedScans.AsNoTracking()
            .Where(b => ids.Contains(b.UserId) && areaSignIds.Contains(b.SignId) && b.ScannedAt >= opens && b.ScannedAt <= closes)
            .ToListAsync();

        var candidates = cleaners
            .Where(u => !covering.Contains(u.Id))
            .Select(u => new CoverCandidateDto(
                u.Id,
                u.EmployeeId ?? string.Empty,
                u.DisplayName,
                u.Area!.Code,
                shiftIns.Where(a => a.UserId == u.Id).Select(a => (DateTime?)Utc(a.OccurredAt)).FirstOrDefault(),
                scans.Where(b => b.UserId == u.Id).Select(b => (DateTime?)Utc(b.ScannedAt)).Max()))
            .OrderBy(c => c.ScannedThisAreaAt is null ? 1 : 0)
            .ThenByDescending(c => c.ScannedThisAreaAt)
            .ThenBy(c => c.ShiftInAt is null ? 1 : 0)
            .ThenBy(c => c.ShiftInAt)
            .ThenBy(c => c.EmployeeId)
            .ToList();
        return Results.Ok(candidates);
    }

    /// <summary>The database returns Unspecified; marking it UTC makes the JSON end in "Z".</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~CoverFromAlertTests|FullyQualifiedName~CoverAssignmentEndpointTests"`
Expected: PASS. The earlier cover tests still pass unchanged.

Then run `dotnet test backend/FacilityRealtime.slnx`. Expected: 0 failures.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Api backend/tests/FacilityRealtime.ApiTests/Admin
git commit -m "feat(backend): assign a cover from a Blocked Scan, which closes its alert; cover candidates per Area and shift (facility-0041)"
```

---

### Task 7: Admin history page (D9), MySQL check and docs

**Files:**
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/AuditLogQuery.cs`
- Create: `backend/src/FacilityRealtime.Api/DTOs/AuditLogDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminAuditLogEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`, `docs/architecture.md`, `README.md`
- Modify (tests): `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceModels.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AuditLogEndpointTests.cs`, `backend/tests/FacilityRealtime.ApiTests/Persistence/MySqlAdminQueryTests.cs`

**Interfaces:**
- Consumes: `AuditCategories` (Task 1), `AdminQuery.ReadDateRange` and `TryReadEnum` (Tasks 3 and 5), `AlertQuery` (Task 5), `AlertReview` (Task 2).
- Produces (Infrastructure):
  - `sealed record AuditLogFilter(DateTime FromUtc, DateTime ToUtc, int? ActorId, IReadOnlyCollection<string>? Actions, long? BeforeId, int Limit)`.
  - `sealed record AuditLogRow(AuditEntry Entry, string ActorName)`.
  - `sealed record AuditActor(int Id, string DisplayName)`.
  - `AuditLogQuery.LoadAsync(AppDbContext, AuditLogFilter) : Task<IReadOnlyList<AuditLogRow>>`, newest first by id.
  - `AuditLogQuery.ActorsAsync(AppDbContext) : Task<IReadOnlyList<AuditActor>>`.
- Produces (Api.DTOs):
  - `AuditLogEntryDto(long Id, DateTime OccurredAt, int ActorId, string ActorName, string Action, AuditCategory? Category, string EntityType, long? EntityId, string Summary, JsonElement? Before, JsonElement? After, string? Reason)`.
  - `AuditLogPageDto(IReadOnlyList<AuditLogEntryDto> Entries, long? NextBeforeId)`.
  - `AuditActorDto(int Id, string DisplayName)`.
- Produces (routes):
  - `GET /api/admin/audit-log` returns 200 `AuditLogPageDto`. It gives 400 for a bad range (default the last 7 days, at most 92), an unknown category, or a limit outside 1–500 (default 100).
  - `GET /api/admin/audit-log/actors` returns 200 `List<AuditActorDto>`.
  - There is no route that changes the log.

- [ ] **Step 1: Write the failing tests**

In `backend/tests/FacilityRealtime.ApiTests/Admin/AdminAttendanceModels.cs`, add `using System.Text.Json;` at the top of the file. Then add at the end of the file:

```csharp
public record AuditLogEntryModel(
    long Id, DateTime OccurredAt, int ActorId, string ActorName, string Action, string? Category,
    string EntityType, long? EntityId, string Summary, JsonElement? Before, JsonElement? After, string? Reason);

public record AuditLogPageModel(List<AuditLogEntryModel> Entries, long? NextBeforeId);

public record AuditActorModel(int Id, string DisplayName);
```

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AuditLogEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Wireframe D9, facility-0057: the Admin history page is read-only, newest first, filtered by date, actor and type.</summary>
public class AuditLogEndpointTests
{
    private const string Path = "/api/admin/audit-log";

    /// <summary>One row written by the seeded Admin at <paramref name="at"/>, as an Admin endpoint would write it.</summary>
    private static Task AddEntryAsync(FacilityApiFactory factory, string action, DateTimeOffset at, object? after = null) =>
        factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            AuditTrail.Add(db, admin.Id, at.UtcDateTime, action, "signs", 1, $"ทดสอบ {action}", before: null, after: after ?? new { Value = 1 });
            await db.SaveChangesAsync();
        });

    private static async Task<AuditLogPageModel> PageAsync(HttpClient admin, string query = "")
    {
        var response = await admin.GetAsync(Path + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuditLogPageModel>())!;
    }

    [Fact]
    public async Task The_log_is_newest_first_with_actor_category_and_snapshots()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddEntryAsync(factory, "SIGN_RADIUS", ThaiClock.At(8, 9, 0), new { RadiusM = 80 });
        await AddEntryAsync(factory, "COVER_ASSIGN", ThaiClock.At(8, 9, 5));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var page = await PageAsync(admin);

        Assert.Equal(new[] { "COVER_ASSIGN", "SIGN_RADIUS" }, page.Entries.Select(e => e.Action));
        Assert.Equal(new[] { "Cover", "Setup" }, page.Entries.Select(e => e.Category));
        Assert.All(page.Entries, e => Assert.Equal("ผู้ดูแลระบบ", e.ActorName));
        Assert.All(page.Entries, e => Assert.Equal(adminAuth.User.Id, e.ActorId));
        Assert.Equal(ThaiClock.At(8, 9, 0).UtcDateTime, page.Entries[1].OccurredAt);
        Assert.Equal(80, page.Entries[1].After!.Value.GetProperty("RadiusM").GetInt32());
        Assert.Null(page.Entries[1].Before);
        Assert.Null(page.NextBeforeId);
    }

    [Fact]
    public async Task The_log_filters_by_category_actor_and_dates()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddEntryAsync(factory, "SIGN_RADIUS", ThaiClock.At(8, 9, 0));
        await AddEntryAsync(factory, "COVER_ASSIGN", ThaiClock.At(8, 9, 5));
        await AddEntryAsync(factory, "QR_REGENERATE", ThaiClock.At(1, 9, 0));   // before the default 7 days
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "E1001");

        Assert.Equal(new[] { "COVER_ASSIGN", "SIGN_RADIUS" }, (await PageAsync(admin)).Entries.Select(e => e.Action));
        Assert.Equal(new[] { "SIGN_RADIUS" }, (await PageAsync(admin, "?category=setup")).Entries.Select(e => e.Action));
        Assert.Empty((await PageAsync(admin, "?category=Attendance")).Entries);
        Assert.Empty((await PageAsync(admin, $"?actorId={cleanerId}")).Entries);
        Assert.Empty((await PageAsync(admin, "?from=2026-10-07&to=2026-10-07")).Entries);
        Assert.Equal(new[] { "QR_REGENERATE" }, (await PageAsync(admin, "?from=2026-10-01&to=2026-10-01")).Entries.Select(e => e.Action));
    }

    [Fact]
    public async Task Older_pages_come_with_before_id()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddEntryAsync(factory, "SIGN_RADIUS", ThaiClock.At(8, 9, 0));
        await AddEntryAsync(factory, "SIGN_LOCATION", ThaiClock.At(8, 9, 1));
        await AddEntryAsync(factory, "COVER_ASSIGN", ThaiClock.At(8, 9, 2));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var first = await PageAsync(admin, "?limit=2");
        var second = await PageAsync(admin, $"?limit=2&beforeId={first.NextBeforeId}");

        Assert.Equal(new[] { "COVER_ASSIGN", "SIGN_LOCATION" }, first.Entries.Select(e => e.Action));
        Assert.Equal(first.Entries[1].Id, first.NextBeforeId);
        Assert.Equal(new[] { "SIGN_RADIUS" }, second.Entries.Select(e => e.Action));
        Assert.Null(second.NextBeforeId);
    }

    [Fact]
    public async Task An_action_no_category_lists_still_shows()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddEntryAsync(factory, "SOMETHING_NEW", ThaiClock.At(8, 9, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var entry = Assert.Single((await PageAsync(admin)).Entries);

        Assert.Equal("SOMETHING_NEW", entry.Action);
        Assert.Null(entry.Category);
    }

    [Fact]
    public async Task A_real_admin_change_appears_and_reading_the_log_is_not_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 0));
        var signId = await AdminSetupApi.SignIdAsync(factory, "AR01-01");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/signs/{signId}/radius", new { radiusM = 80 })).StatusCode);

        var entry = Assert.Single((await PageAsync(admin)).Entries);
        await PageAsync(admin);

        Assert.Equal("SIGN_RADIUS", entry.Action);
        Assert.Equal("Setup", entry.Category);
        Assert.Equal(50, entry.Before!.Value.GetProperty("RadiusM").GetInt32());
        Assert.Single(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Actors_are_everyone_who_has_a_line()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);

        Assert.Empty((await admin.GetFromJsonAsync<List<AuditActorModel>>(Path + "/actors"))!);

        await AddEntryAsync(factory, "SIGN_RADIUS", ThaiClock.At(8, 9, 0));

        Assert.Equal(
            new[] { new AuditActorModel(adminAuth.User.Id, "ผู้ดูแลระบบ") },
            await admin.GetFromJsonAsync<List<AuditActorModel>>(Path + "/actors"));
    }

    [Theory]
    [InlineData("?category=Nope")]
    [InlineData("?limit=0")]
    [InlineData("?limit=501")]
    [InlineData("?from=2026-10-01")]
    [InlineData("?from=2026-01-01&to=2026-10-08")]
    public async Task Bad_filters_are_refused(string query)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Path + query)).StatusCode);
    }

    [Fact]
    public async Task Nobody_can_change_the_log()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.DeleteAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.PutAsJsonAsync(Path, new { })).StatusCode);
    }

    [Fact]
    public async Task Only_admins_read_the_log()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateApiClient().GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(Path + "/actors")).StatusCode);
    }
}
```

Create `backend/tests/FacilityRealtime.ApiTests/Persistence/MySqlAdminQueryTests.cs`:

```csharp
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>
/// The alert and history queries use DATE ranges, nullable flags, IN lists and JSON columns. SQLite hides how the Oracle
/// MySQL provider treats them, so this runs both queries, and the alert_reviews unique key, on real MySQL.
/// </summary>
[Collection("MySQL")]
public class MySqlAdminQueryTests
{
    [MySqlFact]
    public async Task Alert_and_history_queries_run_on_MySQL()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(Environment.GetEnvironmentVariable("FACILITY_MYSQL_TEST")!)
            .Options;

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.EnsureCreatedAsync();
        }

        try
        {
            var shiftIn = new DateTime(2026, 10, 7, 23, 50, 0, DateTimeKind.Utc);   // 06:50 Thai on 8 Oct
            await using (var write = new AppDbContext(options))
            {
                await DbInitializer.SeedAsync(write, new Pbkdf2PasswordHasher(iterations: 1_000));
                var cleaner = await write.Users.SingleAsync(u => u.EmployeeId == "E1001");
                var admin = await write.Users.SingleAsync(u => u.Username == "admin");
                var checkIn = await write.Signs.SingleAsync(s => s.Code == "AR01-IN");
                var otherAreaSign = await write.Signs.SingleAsync(s => s.Code == "AR02-IN");
                var far = new ShiftAttendance
                {
                    UserId = cleaner.Id,
                    AreaId = cleaner.AreaId!.Value,
                    ShiftDate = new DateOnly(2026, 10, 8),
                    Shift = Shift.Day,
                    EventType = AttendanceEvent.ShiftIn,
                    OccurredAt = shiftIn,
                    Source = AttendanceSource.Scan,
                    SignId = checkIn.Id,
                    Latitude = 13.800000m,
                    Longitude = 100.600000m,
                    AccuracyM = 10,
                    DistanceM = 900,
                    WithinRadius = false,
                    CreatedAt = shiftIn,
                };
                var blocked = new BlockedScan { UserId = cleaner.Id, SignId = otherAreaSign.Id, Reason = BlockReason.OtherArea, ScannedAt = shiftIn.AddMinutes(20) };
                write.AddRange(far, blocked);
                await write.SaveChangesAsync();

                write.AlertReviews.Add(new AlertReview { AlertType = AlertType.BlockedScan, RecordId = blocked.Id, ReviewedById = admin.Id, ReviewedAt = shiftIn.AddMinutes(30) });
                AuditTrail.Add(write, admin.Id, shiftIn.AddMinutes(30), "ALERT_REVIEW", "blocked_scans", blocked.Id, "ปิดเตือน", new { Reviewed = false }, new { Reviewed = true });
                AuditTrail.Add(write, admin.Id, shiftIn.AddMinutes(31), "SIGN_RADIUS", "signs", checkIn.Id, "รัศมี", new { RadiusM = 50 }, new { RadiusM = 80 });
                await write.SaveChangesAsync();

                // alert_reviews: an alert is closed once
                write.AlertReviews.Add(new AlertReview { AlertType = AlertType.BlockedScan, RecordId = blocked.Id, ReviewedById = admin.Id, ReviewedAt = shiftIn });
                await Assert.ThrowsAsync<DbUpdateException>(() => write.SaveChangesAsync());
            }

            await using var read = new AppDbContext(options);
            var from = new DateTime(2026, 10, 6, 17, 0, 0, DateTimeKind.Utc);   // 7 Oct 00:00 Thai
            var to = new DateTime(2026, 10, 8, 17, 0, 0, DateTimeKind.Utc);     // 9 Oct 00:00 Thai
            var now = new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc);    // 20:00 Thai: the Day shift has ended

            var alerts = await AlertQuery.LoadAsync(read, from, to, now, new AttendanceSettings());

            Assert.Equal(new[] { AlertKind.MissingAttendance, AlertKind.BlockedScan, AlertKind.AttendanceGps }, alerts.Select(a => a.Kind));
            Assert.NotNull(alerts.Single(a => a.Kind == AlertKind.BlockedScan).Review);
            Assert.Null(alerts.Single(a => a.Kind == AlertKind.AttendanceGps).Review);
            Assert.Equal(new[] { AttendanceEvent.ShiftOut }, alerts.Single(a => a.Kind == AlertKind.MissingAttendance).MissingEvents);
            Assert.Equal("AR01", alerts.Single(a => a.Kind == AlertKind.MissingAttendance).Area.Code);

            var newest = Assert.Single(await AuditLogQuery.LoadAsync(
                read, new AuditLogFilter(from, to, ActorId: null, Actions: ["SIGN_RADIUS", "ALERT_REVIEW"], BeforeId: null, Limit: 1)));
            Assert.Equal("SIGN_RADIUS", newest.Entry.Action);
            Assert.Equal("ผู้ดูแลระบบ", newest.ActorName);
            Assert.Contains("80", newest.Entry.AfterJson);
            var older = await AuditLogQuery.LoadAsync(read, new AuditLogFilter(from, to, null, null, newest.Entry.Id, 10));
            Assert.Equal("ALERT_REVIEW", Assert.Single(older).Entry.Action);
            Assert.Single(await AuditLogQuery.ActorsAsync(read));
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AuditLogEndpointTests|FullyQualifiedName~MySqlAdminQueryTests"`
Expected: FAIL to compile (`AuditLogQuery` and `AuditLogFilter` are not defined).

- [ ] **Step 3: Write the implementation**

Create `backend/src/FacilityRealtime.Infrastructure/Persistence/AuditLogQuery.cs`:

```csharp
using FacilityRealtime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>OccurredAt in [FromUtc, ToUtc); Actions null = every action; BeforeId gives the next, older page.</summary>
public sealed record AuditLogFilter(DateTime FromUtc, DateTime ToUtc, int? ActorId, IReadOnlyCollection<string>? Actions, long? BeforeId, int Limit);

public sealed record AuditLogRow(AuditEntry Entry, string ActorName);

public sealed record AuditActor(int Id, string DisplayName);

/// <summary>The Admin history page (wireframe D9, facility-0057): read-only, newest first.</summary>
public static class AuditLogQuery
{
    public static async Task<IReadOnlyList<AuditLogRow>> LoadAsync(AppDbContext db, AuditLogFilter filter)
    {
        var query = db.AuditLog.AsNoTracking().Where(a => a.OccurredAt >= filter.FromUtc && a.OccurredAt < filter.ToUtc);
        if (filter.ActorId is int actorId)
        {
            query = query.Where(a => a.ActorId == actorId);
        }

        if (filter.Actions is not null)
        {
            var actions = filter.Actions.ToList();
            query = query.Where(a => actions.Contains(a.Action));
        }

        if (filter.BeforeId is long beforeId)
        {
            query = query.Where(a => a.Id < beforeId);
        }

        // Ids grow with every row, so the newest row has the highest id
        var entries = await query.OrderByDescending(a => a.Id).Take(filter.Limit).ToListAsync();
        var actorIds = entries.Select(a => a.ActorId).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        return entries.Select(a => new AuditLogRow(a, names[a.ActorId])).ToList();
    }

    /// <summary>Everyone with a line in the log, deactivated accounts included, for the "ผู้ทำ" filter.</summary>
    public static async Task<IReadOnlyList<AuditActor>> ActorsAsync(AppDbContext db)
    {
        var ids = await db.AuditLog.Select(a => a.ActorId).Distinct().ToListAsync();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .Select(u => new AuditActor(u.Id, u.DisplayName))
            .ToListAsync();
    }
}
```

Create `backend/src/FacilityRealtime.Api/DTOs/AuditLogDtos.cs`:

```csharp
using System.Text.Json;
using FacilityRealtime.Application.Audit;

namespace FacilityRealtime.Api.DTOs;

/// <summary>
/// One line of the Admin history page (wireframe D9). Before and After are the stored snapshots, which never hold a phone
/// number or password (facility-0057). Category is null for an action no category lists yet.
/// </summary>
public record AuditLogEntryDto(
    long Id,
    DateTime OccurredAt,
    int ActorId,
    string ActorName,
    string Action,
    AuditCategory? Category,
    string EntityType,
    long? EntityId,
    string Summary,
    JsonElement? Before,
    JsonElement? After,
    string? Reason);

/// <summary>NextBeforeId: pass it as beforeId for the next, older page; null on the last page.</summary>
public record AuditLogPageDto(IReadOnlyList<AuditLogEntryDto> Entries, long? NextBeforeId);

public record AuditActorDto(int Id, string DisplayName);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminAuditLogEndpoints.cs`:

```csharp
using System.Security.Claims;
using System.Text.Json;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Audit;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>The "ประวัติ Admin" tab (wireframe D9, facility-0057): Admin only and read-only. Opening it is not logged.</summary>
public static class AdminAuditLogEndpoints
{
    public const int DefaultLimit = 100;

    public const int MaxLimit = 500;

    public static IEndpointRouteBuilder MapAdminAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/audit-log").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/", ListAsync);
        admin.MapGet("/actors", async (ClaimsPrincipal principal, AppDbContext db) =>
            await CurrentUser.LoadAsync(principal, db) is null
                ? Results.Unauthorized()
                : Results.Ok((await AuditLogQuery.ActorsAsync(db)).Select(a => new AuditActorDto(a.Id, a.DisplayName)).ToList()));
        return app;
    }

    private static async Task<IResult> ListAsync(
        string? from,
        string? to,
        int? actorId,
        string? category,
        long? beforeId,
        int? limit,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider clock)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (AdminQuery.ReadDateRange(from, to, now, defaultDays: 7, maxDays: 92, out var fromUtc, out var toUtc) is { } rangeError)
        {
            return Bad(rangeError);
        }

        IReadOnlyList<string>? actions = null;
        if (category is not null)
        {
            if (!AdminQuery.TryReadEnum<AuditCategory>(category, out var parsed))
            {
                return Bad("ไม่มีประเภทนี้ในประวัติ");
            }

            actions = AuditCategories.ActionsOf(parsed);
        }

        var take = limit ?? DefaultLimit;
        if (take is < 1 or > MaxLimit)
        {
            return Bad($"limit ต้องอยู่ระหว่าง 1–{MaxLimit}");
        }

        var rows = await AuditLogQuery.LoadAsync(db, new AuditLogFilter(fromUtc, toUtc, actorId, actions, beforeId, take));
        var entries = rows.Select(ToDto).ToList();
        return Results.Ok(new AuditLogPageDto(entries, entries.Count == take ? entries[^1].Id : (long?)null));
    }

    private static AuditLogEntryDto ToDto(AuditLogRow row) => new(
        row.Entry.Id,
        DateTime.SpecifyKind(row.Entry.OccurredAt, DateTimeKind.Utc),
        row.Entry.ActorId,
        row.ActorName,
        row.Entry.Action,
        AuditCategories.Of(row.Entry.Action),
        row.Entry.EntityType,
        row.Entry.EntityId,
        row.Entry.Summary,
        Json(row.Entry.BeforeJson),
        Json(row.Entry.AfterJson),
        row.Entry.Reason);

    /// <summary>The stored snapshot as a JSON value, so the page can show its fields.</summary>
    private static JsonElement? Json(string? stored)
    {
        if (stored is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(stored);
        return document.RootElement.Clone();
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add after `app.MapAdminAlertEndpoints();`:

```csharp
app.MapAdminAuditLogEndpoints();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AuditLogEndpointTests"`
Expected: PASS, 13 tests.

Then run `dotnet test backend/FacilityRealtime.slnx`. Expected: 0 failures, with the MySQL tests skipped.

Then run the MySQL tests against a throwaway database. Ask the developer for the local root password and do not save it anywhere:

```bash
FACILITY_MYSQL_TEST="Server=localhost;Port=3306;Database=facility_adminqueries_test;Uid=root;Pwd=<local root password>;CharSet=utf8mb4;" dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~MySql"
```

Expected: PASS, 3 tests: `MySqlReadTests`, `MySqlAreaCleanerTests` and `MySqlAdminQueryTests`. Each one drops its database in `finally`.

- [ ] **Step 5: Update the docs**

In `docs/architecture.md`, in the component diagram, replace the `Ep` node line with:

```
            Ep["Endpoints/*<br/>/api/service-points (Admin) | /api/scan-records | /api/attendance | /api/my-work | /api/me<br/>/api/admin/cover-assignments | /api/admin/areas | points | signs | buildings | cleaners<br/>/api/admin/attendance | alerts | audit-log"]
```

Replace the `Hub` node line with:

```
            Hub["Hubs/ScanHub.cs<br/>/hubs/scan  [Authorize]<br/>ScanRecorded, AttendanceRecorded → Admins"]
```

Replace the `Ent` node line with:

```
            Ent["Entities: User, ServicePoint,<br/>ScanRecord, RefreshToken,<br/>ShiftAttendance, BlockedScan,<br/>CoverAssignment, AuditEntry, AlertReview"]
```

In the class diagram, add after the `class AuditEntry { ... }` block:

```
    class AlertReview {
        <<alert_reviews>>
        +long Id
        +AlertType AlertType
        +long RecordId
        +int ReviewedById
        +DateTime ReviewedAt
    }
```

Then add after the line `    User "1" <-- "0..*" AuditEntry : actor`:

```
    User "1" <-- "0..*" AlertReview : reviewed by
```

At the end of the paragraph under the class diagram, the one that ends with "(facility-0041, facility-0051).", add:

```
An Alert Review records that the Admin closed one alert ("คุยแล้ว ปิดเตือน") on a Blocked Scan or a scan outside its sign's radius, one per alert; a missing Shift-Out is never reviewed, it closes when the Admin fills the time (facility-0051).
```

In `README.md`, add this block after the line "- ทุกการแก้ลงตาราง `audit_log`":

```markdown

ดูการเข้างาน รายการที่ขึ้นเตือน และประวัติใน Swagger (login เป็น `admin`):

- `GET /api/admin/attendance` การเข้างานของกะที่กำลังเดินอยู่ หรือส่ง `shiftDate=2026-10-08&shift=Day` เพื่อดูกะอื่น (ADR facility-0050)
- `PUT /api/admin/attendance` เพิ่มหรือแก้เวลา เช่น `{ "userId": 3, "shiftDate": "2026-10-08", "shift": "Day", "eventType": "ShiftOut", "time": "18:58", "reason": "ลืมสแกน" }` ต้องมีเหตุผลทุกครั้ง (ADR facility-0051)
- `GET /api/admin/alerts` รายการที่ขึ้นเตือนของเมื่อวานและวันนี้ และ `POST /api/admin/alerts/review` ปิดเตือน
- `POST /api/admin/cover-assignments` ใส่ `blockedScanId` เพื่อมอบหมายทำแทนจากการสแกนที่ถูกบล็อก (ADR facility-0041)
- `GET /api/admin/audit-log` ประวัติ Admin อ่านอย่างเดียว (ADR facility-0057)
```

- [ ] **Step 6: Commit**

```bash
git add backend/src backend/tests docs/architecture.md README.md
git commit -m "feat(backend): read-only Admin history page with date, actor and category filters; MySQL check of the alert and history queries (facility-0057)"
```

---

## After the last task

- **Tests:** `dotnet test backend/FacilityRealtime.slnx` passes with 0 failures. The three MySQL tests pass with `FACILITY_MYSQL_TEST` set to a throwaway database, which is dropped afterwards.
- **Real API run:** start the API in Development against a throwaway MySQL database, with `ConnectionStrings__DefaultConnection` pointing at `facility_attendance_test`. Drive it with a Python script.
  - The API reads the real clock, so pick cases that fit the Thai time of the run. During the Day shift (06:00–20:00 Thai):
    1. `E1001` checks in at `token-checkin-ar01`. The board shows AR01 Working and AR02 Empty, with AR02 first.
    2. `E1001` sends `ShiftIn` with `token-checkin-ar02` and gets 403. The alert list shows a BlockedScan with a `cover`.
    3. Post that `cover` with `blockedScanId`. The board shows AR02 Covered, and the alert is reviewed.
    4. `PUT /api/admin/attendance` adds `E1003`'s `ShiftIn` with a reason. AR02 turns Working, and the history shows `ATTENDANCE_ADD`.
    5. A far check-in by `E1002`, run only during the Night window, or any far scan: it shows `AttendanceGps`, and `review` closes it once (a second try gives 409).
    6. `GET /api/admin/audit-log?category=Attendance` and `/actors` return the rows above, and the JSON has no phone number and no QR Token.
  - Drop the database afterwards. Never use `facility_dashboard`.
- **In the PR:**
  - List the 13 "Decisions made while planning" for the owner to confirm.
  - Note that refusing UPDATE/DELETE on `audit_log` to the app's MySQL user is a deployment step.
- **Next plans:**
  - Plan 5: accounts (D6) and the Excel export (D8). Their actions are added to `AuditCategories`.
  - The Supervisor plan.
  - The frontend: D2, D3 and D9 use the endpoints above.
