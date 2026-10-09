# Pilot Scan Rules (Backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn "any logged-in account may submit anything" into the pilot's rules: Cleaners record four Attendance Events at their Area's Check-In Sign, may submit Scan Records only between Shift-In and Shift-Out, only at their own Area (or an Area an Admin assigned them to cover this shift), every scan carries the phone's GPS fix and is flagged (never blocked) when outside the sign's radius, and scans of another Area are blocked and kept for the Admin. Cleaners get a "My Work" endpoint.

**Architecture:** Two new pure rules in `FacilityRealtime.Application` — `AttendanceCalendar`/`AttendanceRules` (which shift an attendance scan belongs to, ADR 0069; which event may come next, ADR 0031) and `Geofence` (distance and verdict, ADR 0035–0038) — unit-tested with fixed Thai times. Four new tables (`shift_attendances`, `blocked_scans`, `cover_assignments`, `audit_log`) plus GPS columns on `scan_records` and location columns on `signs`, in one new migration `ScanRules`. New endpoints: `POST /api/attendance`, `GET /api/attendance/me`, `GET /api/my-work`, `POST`/`DELETE /api/admin/cover-assignments`; `POST /api/scan-records` gains the rules. A Cleaner's attendance and submissions belong to **their own shift** (from `AttendanceCalendar`), while the Admin Dashboard keeps showing the shift running now (`ShiftCalendar.SlotAt`).

**Tech Stack:** .NET 10 Minimal API, EF Core 10 (`MySql.EntityFrameworkCore` 10.0.9 at runtime, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 in tests), `dotnet-ef` 10.0.12 local tool, xUnit 2.9.3.

**Spec:**
- `docs/adr/facility-0069-attendance-scan-window-60-minutes-around-the-shift.md` (new, 2026-10-09)
- `docs/adr/facility-0026-shift-check-in-two-shifts-strict-presence.md` (Shift-In before submitting; first press wins)
- `docs/adr/facility-0031-four-stage-shift-attendance-lifecycle.md` (four events, in order, 5-minute repeat guard)
- `docs/adr/facility-0050-check-in-confirms-area-presence-not-time-attendance.md` (no late rules)
- `docs/adr/facility-0051-admin-corrections-and-audit-log.md` (forgotten Break-In / Shift-Out stay empty; every Admin change is logged)
- `docs/adr/facility-0035-per-building-check-in-gps-flag-not-block.md`, `facility-0036-check-in-requires-location-permission.md`, `facility-0037-gps-for-every-scan-replaces-nfc.md`, `facility-0038-default-radius-50m-per-sign.md`
- `docs/adr/facility-0041-scan-only-own-area-with-cover-assignment.md` (block other Areas by QR Token; per-shift Cover Assignment; the cover checks in at their own sign)
- `docs/adr/facility-0052-cleaner-my-work-page-and-resend.md` (My Work lists own Area + covered Areas)
- `docs/design/database.html` (tables `shift_attendances`, `blocked_scans`, `cover_assignments`, `audit_log`, the GPS column set, `signs` location columns)
- `CONTEXT.md` (Attendance Event, Blocked Scan, Cover Assignment, Geofence Flag)

## Global Constraints

- Work on branch `feat/pilot-scan-rules`, which is stacked on `feat/pilot-data-model` (PR #50). Do not merge either branch.
- Owner decisions of 2026-10-09 (binding): attendance window opens 60 minutes before the shift and closes 60 minutes after it, both in config (ADR 0069); only **Cleaner Accounts** submit Scan Records and record attendance; a Cleaner may submit from Shift-In until Shift-Out, breaks do not block; a Cover Assignment is only for the covering Cleaner's own regular shift; a sign without coordinates stores the phone's fix with **no** verdict (`distance_m` and `within_radius` NULL); login phone numbers are Arabic digits only (already true).
- A Cleaner's attendance and Scan Records use **the Cleaner's own shift slot** from `AttendanceCalendar.SlotFor(user.Shift, now, settings)`; the Admin Dashboard still uses `ShiftCalendar.SlotAt(now)`.
- Within radius ⇔ `distance_m <= radius_m` **and** `accuracy_m <= radius_m` (ADR 0035/0036/0037). Outside → stored and flagged, never refused. No location at all → 400 (ADR 0036, the one exception).
- The Area of a scan is decided by the sign's QR Token, never by GPS (ADR 0041). Another Area without an active cover for this shift → write a `blocked_scans` row (reason `OTHER_AREA`), save nothing else, answer 403 with the sign's Area code and name.
- Attendance order: none → Shift-In; Shift-In → Break-Out or Shift-Out; Break-Out → Break-In or Shift-Out (a forgotten Break-In stays empty, ADR 0051); Break-In → Shift-Out; nothing after Shift-Out. Pressing an already-recorded event returns the first time (ADR 0026). A different event within `RepeatIgnoreMinutes` (5) of the latest one is ignored, not recorded (ADR 0031).
- GPS columns everywhere: `latitude`, `longitude` DECIMAL(9,6); `accuracy_m`, `distance_m` SMALLINT (clamped to 32767); `within_radius` BOOLEAN (NULL = no verdict).
- Every Admin change writes an `audit_log` row in the same transaction (ADR 0051); never put phone numbers or passwords into `before_json`/`after_json`.
- The runtime provider is Oracle's `MySql.EntityFrameworkCore`; SQLite tests cannot catch provider-only failures. After the migration task, apply it to a throwaway MySQL database and run the `[MySqlFact]` test (`FACILITY_MYSQL_TEST`). MySQL 8.0.46 runs on this laptop as service `MySQL80`, user `root`, password `root`; only ever use throwaway databases (`facility_*_test`), never `facility_dashboard`, and never write the password into a committed file.
- Keep every existing rule from PR #50 (auth, throttle, Dashboard Admin-only, no QR Token in payloads).

### Deviations from database.html (decided while planning)

1. `blocked_scans.reason` holds only `OTHER_AREA` for now; the Supervisor plan adds `OTHER_BUILDING` and `OUTSIDE_SHIFT` (VARCHAR column, no migration needed).
2. `cover_assignments.area_id` is NOT NULL and there is no `building_id` yet; the Supervisor plan adds Supervisor covers.
3. `signs` gets `latitude`, `longitude`, `location_accuracy_m`, `location_source`, `located_at`, `radius_m` — not `distance_m`/`within_radius`, which only make sense on a scan.
4. `inspections` GPS columns come with the Supervisor inspection plan; `alert_reviews` comes with the Admin alerts page.

### Out of scope (later plans)

Supervisor inspection endpoint and page, Admin pages (capturing sign locations, correcting attendance, the alert list with "assign cover" from a Blocked Scan, round-window editing including the "no window may end at 19:00 or 07:00" rule), per-Area SignalR groups, the frontend, data-retention jobs.

---

## File Structure

```
backend/
  src/FacilityRealtime.Domain/
    Enums/      AttendanceEvent, AttendanceSource, BlockReason, LocationSource                     (new)
    Entities/   IGpsStamped, ShiftAttendance, BlockedScan, CoverAssignment, AuditEntry             (new)
                Sign (+location, radius), ScanRecord (+GPS, CoverAssignmentId)                     (modified)
  src/FacilityRealtime.Application/
    Shifts/ShiftCalendar.cs            + StartUtc, EndUtc                                          (modified)
    Attendance/AttendanceSettings.cs   the "Attendance" config section                             (new)
    Attendance/AttendanceCalendar.cs   which own-shift slot an attendance scan belongs to          (new)
    Attendance/AttendanceRules.cs      next allowed events, on duty                                (new)
    Geo/Geofence.cs                    GpsReading, GeofenceResult, distance + verdict              (new)
    Geo/GpsStamping.cs                 writes a fix + verdict onto an IGpsStamped row              (new)
  src/FacilityRealtime.Infrastructure/
    Persistence/AppDbContext.cs        4 tables, GPS + location columns                            (modified)
    Persistence/AuditTrail.cs          adds an audit_log row                                       (new)
    Persistence/PointBoardQuery.cs     + overload with a given slot and Area filter                (modified)
    Persistence/DbInitializer.cs       AR01 signs get coordinates                                  (modified)
    Migrations/<ts>_ScanRules.cs       (generated)
  src/FacilityRealtime.Api/
    Auth/CurrentUser.cs                the active account in the token                            (new)
    Endpoints/ApiResults.cs            JSON { message } with a status code                         (new)
    Endpoints/GpsInput.cs              validates the phone's fix                                   (new)
    Endpoints/BlockedScans.cs          records a Blocked Scan and answers 403                      (new)
    Endpoints/AttendanceEndpoints.cs   POST /api/attendance, GET /api/attendance/me                (new)
    Endpoints/ScanRecordEndpoints.cs   the scan rules                                             (modified)
    Endpoints/CoverAssignmentEndpoints.cs  POST/DELETE /api/admin/cover-assignments               (new)
    Endpoints/MyWorkEndpoints.cs       GET /api/my-work                                            (new)
    DTOs/AttendanceDtos.cs, DTOs/CoverDtos.cs, DTOs/MyWorkDtos.cs                                  (new)
    DTOs/PointDtos.cs                  scan request/response gain GPS fields                       (modified)
  tests/FacilityRealtime.UnitTests/    AttendanceCalendarTests, AttendanceRulesTests, GeofenceTests, ShiftCalendarTests (+)
  tests/FacilityRealtime.ApiTests/     Infrastructure/{TestGps, AttendanceApi, AuthApi(+), TestData(+)}
                                       Attendance/AttendanceEndpointTests, Points/ScanRecordEndpointTests (rewritten),
                                       Admin/CoverAssignmentEndpointTests, Points/MyWorkEndpointTests,
                                       Persistence/SchemaRuleTests (+), Persistence/MySqlReadTests (+)
```

Run every command from the repository root. `dotnet test backend/FacilityRealtime.slnx` runs everything. If an API is running from this repo (e.g. for Swagger on port 5001), stop it before building — Windows locks its DLLs.

---

### Task 1: Attendance calendar and rules

**Files:**
- Create: `backend/src/FacilityRealtime.Domain/Enums/AttendanceEvent.cs`
- Modify: `backend/src/FacilityRealtime.Application/Shifts/ShiftCalendar.cs`
- Create: `backend/src/FacilityRealtime.Application/Attendance/AttendanceSettings.cs`, `AttendanceCalendar.cs`, `AttendanceRules.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/AttendanceCalendarTests.cs`, `AttendanceRulesTests.cs`, and two tests added to `ShiftCalendarTests.cs`

**Interfaces:**
- Produces: `enum AttendanceEvent { ShiftIn, BreakOut, BreakIn, ShiftOut }` (Domain.Enums; stored as `SHIFT_IN` …).
- Produces: `ShiftCalendar.StartUtc(ShiftSlot) : DateTime`, `ShiftCalendar.EndUtc(ShiftSlot) : DateTime` (start + 12 h).
- Produces (namespace `FacilityRealtime.Application.Attendance`): `sealed class AttendanceSettings { const string SectionName = "Attendance"; int OpensMinutesBeforeShift = 60; int ClosesMinutesAfterShift = 60; int RepeatIgnoreMinutes = 5; }`; `static class AttendanceCalendar { ShiftSlot? SlotFor(Shift regularShift, DateTime nowUtc, AttendanceSettings settings); }`; `static class AttendanceRules { IReadOnlyList<AttendanceEvent> NextAllowed(IReadOnlyCollection<AttendanceEvent> recorded); bool IsOnDuty(IReadOnlyCollection<AttendanceEvent> recorded); }`.

- [ ] **Step 1: Write the failing tests**

Add to `backend/tests/FacilityRealtime.UnitTests/ShiftCalendarTests.cs` (inside the class; it already has the `Thai(day, hour, minute)` helper):

```csharp
    [Theory]
    [InlineData(Shift.Day, 8, 7, 8, 19)]
    [InlineData(Shift.Night, 8, 19, 9, 7)]
    public void Shift_starts_and_ends_twelve_hours_later(Shift shift, int startDay, int startHour, int endDay, int endHour)
    {
        var slot = new ShiftSlot(new DateOnly(2026, 10, 8), shift);

        Assert.Equal(Thai(startDay, startHour, 0), ShiftCalendar.StartUtc(slot));
        Assert.Equal(Thai(endDay, endHour, 0), ShiftCalendar.EndUtc(slot));
    }
```

Create `backend/tests/FacilityRealtime.UnitTests/AttendanceCalendarTests.cs`:

```csharp
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0069: attendance counts for the person's own shift from 60 minutes before it to 60 minutes after it.</summary>
public class AttendanceCalendarTests
{
    private static readonly AttendanceSettings Settings = new() { OpensMinutesBeforeShift = 60, ClosesMinutesAfterShift = 60 };

    private static DateTime Thai(int day, int hour, int minute) =>
        new DateTime(2026, 10, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);

    private static ShiftSlot? Slot(int? day, Shift shift) => day is int d ? new ShiftSlot(new DateOnly(2026, 10, d), shift) : null;

    [Theory]
    [InlineData(8, 5, 59, null)]
    [InlineData(8, 6, 0, 8)]
    [InlineData(8, 12, 0, 8)]
    [InlineData(8, 20, 0, 8)]
    [InlineData(8, 20, 1, null)]
    public void Day_shift_window_opens_an_hour_early_and_closes_an_hour_late(int day, int hour, int minute, int? shiftDay)
    {
        Assert.Equal(Slot(shiftDay, Shift.Day), AttendanceCalendar.SlotFor(Shift.Day, Thai(day, hour, minute), Settings));
    }

    [Theory]
    [InlineData(8, 17, 59, null)]
    [InlineData(8, 18, 0, 8)]
    [InlineData(9, 2, 0, 8)]
    [InlineData(9, 8, 0, 8)]
    [InlineData(9, 8, 1, null)]
    [InlineData(9, 18, 0, 9)]
    public void Night_shift_window_belongs_to_the_date_the_shift_started(int day, int hour, int minute, int? shiftDay)
    {
        Assert.Equal(Slot(shiftDay, Shift.Night), AttendanceCalendar.SlotFor(Shift.Night, Thai(day, hour, minute), Settings));
    }

    [Fact]
    public void Zero_minutes_means_inside_the_shift_only()
    {
        var strict = new AttendanceSettings { OpensMinutesBeforeShift = 0, ClosesMinutesAfterShift = 0 };

        Assert.Null(AttendanceCalendar.SlotFor(Shift.Day, Thai(8, 6, 59), strict));
        Assert.Equal(Slot(8, Shift.Day), AttendanceCalendar.SlotFor(Shift.Day, Thai(8, 7, 0), strict));
    }
}
```

Create `backend/tests/FacilityRealtime.UnitTests/AttendanceRulesTests.cs`:

```csharp
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0031 order; facility-0051: a forgotten Break-In stays empty, so Shift-Out may follow Break-Out.</summary>
public class AttendanceRulesTests
{
    private static IReadOnlyList<AttendanceEvent> Next(params AttendanceEvent[] recorded) => AttendanceRules.NextAllowed(recorded);

    [Fact]
    public void Nothing_recorded_allows_only_shift_in() =>
        Assert.Equal(new[] { AttendanceEvent.ShiftIn }, Next());

    [Fact]
    public void After_shift_in_take_a_break_or_leave() =>
        Assert.Equal(new[] { AttendanceEvent.BreakOut, AttendanceEvent.ShiftOut }, Next(AttendanceEvent.ShiftIn));

    [Fact]
    public void On_a_break_come_back_or_leave() =>
        Assert.Equal(new[] { AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut }, Next(AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut));

    [Fact]
    public void After_the_break_only_leave() =>
        Assert.Equal(new[] { AttendanceEvent.ShiftOut }, Next(AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn));

    [Fact]
    public void After_shift_out_nothing() =>
        Assert.Empty(Next(AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut));

    [Fact]
    public void On_duty_from_shift_in_until_shift_out_breaks_included()
    {
        Assert.False(AttendanceRules.IsOnDuty(Array.Empty<AttendanceEvent>()));
        Assert.True(AttendanceRules.IsOnDuty(new[] { AttendanceEvent.ShiftIn }));
        Assert.True(AttendanceRules.IsOnDuty(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut }));
        Assert.False(AttendanceRules.IsOnDuty(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut }));
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AttendanceCalendarTests|FullyQualifiedName~AttendanceRulesTests|FullyQualifiedName~ShiftCalendarTests"`
Expected: build FAILS — `FacilityRealtime.Application.Attendance`, `AttendanceEvent`, `StartUtc`, `EndUtc` do not exist.

- [ ] **Step 3: Write the implementation**

Create `backend/src/FacilityRealtime.Domain/Enums/AttendanceEvent.cs`:

```csharp
namespace FacilityRealtime.Domain.Enums;

/// <summary>CONTEXT.md, Attendance Event: the four scans of a shift (facility-0031).</summary>
public enum AttendanceEvent
{
    ShiftIn,
    BreakOut,
    BreakIn,
    ShiftOut,
}
```

In `backend/src/FacilityRealtime.Application/Shifts/ShiftCalendar.cs`, add inside `ShiftCalendar` after `SlotAt`:

```csharp
    /// <summary>When the shift starts, as UTC: 07:00 (Day) or 19:00 (Night) Thai on ShiftDate.</summary>
    public static DateTime StartUtc(ShiftSlot slot) => ToUtc(slot, slot.Shift == Shift.Day ? DayStart : NightStart);

    /// <summary>Every shift is 12 hours long.</summary>
    public static DateTime EndUtc(ShiftSlot slot) => StartUtc(slot).AddHours(12);
```

Create `backend/src/FacilityRealtime.Application/Attendance/AttendanceSettings.cs`:

```csharp
namespace FacilityRealtime.Application.Attendance;

/// <summary>The "Attendance" config section (facility-0069, facility-0031).</summary>
public sealed class AttendanceSettings
{
    public const string SectionName = "Attendance";

    public int OpensMinutesBeforeShift { get; set; } = 60;
    public int ClosesMinutesAfterShift { get; set; } = 60;

    /// <summary>A different attendance event within this many minutes of the latest one is ignored.</summary>
    public int RepeatIgnoreMinutes { get; set; } = 5;
}
```

Create `backend/src/FacilityRealtime.Application/Attendance/AttendanceCalendar.cs`:

```csharp
using FacilityRealtime.Application.Common;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Attendance;

/// <summary>facility-0069: attendance and Scan Records count for the person's own shift, not the shift the clock is in.</summary>
public static class AttendanceCalendar
{
    /// <summary>
    /// The slot of <paramref name="regularShift"/> whose window — from OpensMinutesBeforeShift before its start
    /// to ClosesMinutesAfterShift after its end, both inclusive — contains <paramref name="nowUtc"/>; null outside every window.
    /// </summary>
    public static ShiftSlot? SlotFor(Shift regularShift, DateTime nowUtc, AttendanceSettings settings)
    {
        var thaiDate = DateOnly.FromDateTime(ThaiTime.FromUtc(nowUtc));
        foreach (var date in new[] { thaiDate.AddDays(-1), thaiDate, thaiDate.AddDays(1) })
        {
            var slot = new ShiftSlot(date, regularShift);
            var opens = ShiftCalendar.StartUtc(slot).AddMinutes(-settings.OpensMinutesBeforeShift);
            var closes = ShiftCalendar.EndUtc(slot).AddMinutes(settings.ClosesMinutesAfterShift);
            if (nowUtc >= opens && nowUtc <= closes)
            {
                return slot;
            }
        }

        return null;
    }
}
```

Create `backend/src/FacilityRealtime.Application/Attendance/AttendanceRules.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Attendance;

/// <summary>facility-0031: the four events in order. facility-0051: a forgotten Break-In stays empty, so Shift-Out may follow Break-Out.</summary>
public static class AttendanceRules
{
    public static IReadOnlyList<AttendanceEvent> NextAllowed(IReadOnlyCollection<AttendanceEvent> recorded) =>
        recorded.Contains(AttendanceEvent.ShiftOut) ? []
        : recorded.Contains(AttendanceEvent.BreakIn) ? [AttendanceEvent.ShiftOut]
        : recorded.Contains(AttendanceEvent.BreakOut) ? [AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut]
        : recorded.Contains(AttendanceEvent.ShiftIn) ? [AttendanceEvent.BreakOut, AttendanceEvent.ShiftOut]
        : [AttendanceEvent.ShiftIn];

    /// <summary>Owner decision 2026-10-09: a Cleaner may submit from Shift-In until Shift-Out; a break does not stop it.</summary>
    public static bool IsOnDuty(IReadOnlyCollection<AttendanceEvent> recorded) =>
        recorded.Contains(AttendanceEvent.ShiftIn) && !recorded.Contains(AttendanceEvent.ShiftOut);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AttendanceCalendarTests|FullyQualifiedName~AttendanceRulesTests|FullyQualifiedName~ShiftCalendarTests"`
Expected: PASS (AttendanceCalendar 12, AttendanceRules 6, every ShiftCalendar test including the 2 new ones).

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: 0 failures.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Domain/Enums/AttendanceEvent.cs backend/src/FacilityRealtime.Application/Shifts/ShiftCalendar.cs backend/src/FacilityRealtime.Application/Attendance backend/tests/FacilityRealtime.UnitTests
git commit -m "feat(backend): attendance window around the own shift and the four-event order (facility-0069, 0031)"
```

---

### Task 2: Geofence

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Geo/Geofence.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/GeofenceTests.cs`

**Interfaces:**
- Produces (namespace `FacilityRealtime.Application.Geo`): `sealed record GpsReading(double Latitude, double Longitude, double AccuracyM)`; `sealed record GeofenceResult(int? DistanceM, bool? WithinRadius)`; `static class Geofence { double DistanceMeters(double lat1, double lon1, double lat2, double lon2); GeofenceResult Evaluate(GpsReading phone, double? signLatitude, double? signLongitude, int radiusM); }`.

- [ ] **Step 1: Write the failing test**

Create `backend/tests/FacilityRealtime.UnitTests/GeofenceTests.cs`:

```csharp
using FacilityRealtime.Application.Geo;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0035/0036/0037: outside the radius, or a fix less accurate than the radius, is flagged — never blocked.</summary>
public class GeofenceTests
{
    private const double SignLatitude = 13.7563;
    private const double SignLongitude = 100.5018;

    [Fact]
    public void One_thousandth_of_a_degree_of_latitude_is_about_111_metres()
    {
        Assert.InRange(Geofence.DistanceMeters(13.7563, 100.5018, 13.7573, 100.5018), 110.6, 111.8);
    }

    [Fact]
    public void Same_point_is_zero_metres()
    {
        Assert.Equal(0, Geofence.DistanceMeters(SignLatitude, SignLongitude, SignLatitude, SignLongitude), precision: 6);
    }

    [Fact]
    public void Phone_at_the_sign_is_within_the_radius()
    {
        Assert.Equal(new GeofenceResult(0, true), Geofence.Evaluate(new GpsReading(SignLatitude, SignLongitude, 10), SignLatitude, SignLongitude, 50));
    }

    [Fact]
    public void Phone_beyond_the_radius_is_flagged()
    {
        var result = Geofence.Evaluate(new GpsReading(13.7573, 100.5018, 10), SignLatitude, SignLongitude, 50);

        Assert.Equal(111, result.DistanceM);
        Assert.False(result.WithinRadius);
    }

    [Fact]
    public void Exactly_at_the_radius_is_within()
    {
        Assert.True(Geofence.Evaluate(new GpsReading(13.7573, 100.5018, 10), SignLatitude, SignLongitude, 111).WithinRadius);
    }

    [Fact]
    public void Inaccurate_fix_is_flagged_even_at_the_sign()
    {
        Assert.Equal(new GeofenceResult(0, false), Geofence.Evaluate(new GpsReading(SignLatitude, SignLongitude, 80), SignLatitude, SignLongitude, 50));
    }

    [Fact]
    public void Sign_without_coordinates_gives_no_verdict()
    {
        Assert.Equal(new GeofenceResult(null, null), Geofence.Evaluate(new GpsReading(SignLatitude, SignLongitude, 10), null, null, 50));
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~GeofenceTests"`
Expected: build FAILS — `FacilityRealtime.Application.Geo` does not exist.

- [ ] **Step 3: Write the implementation**

Create `backend/src/FacilityRealtime.Application/Geo/Geofence.cs`:

```csharp
namespace FacilityRealtime.Application.Geo;

/// <summary>What the browser's Geolocation API reported: position and its accuracy in metres.</summary>
public sealed record GpsReading(double Latitude, double Longitude, double AccuracyM);

/// <summary>Both null when the sign has no coordinates yet (owner decision 2026-10-09: no verdict, no flag).</summary>
public sealed record GeofenceResult(int? DistanceM, bool? WithinRadius);

public static class Geofence
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Great-circle (haversine) distance.</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>facility-0035/0036/0037: within only when both the distance and the fix's accuracy fit inside the radius.</summary>
    public static GeofenceResult Evaluate(GpsReading phone, double? signLatitude, double? signLongitude, int radiusM)
    {
        if (signLatitude is not { } latitude || signLongitude is not { } longitude)
        {
            return new GeofenceResult(null, null);
        }

        var distance = (int)Math.Round(DistanceMeters(phone.Latitude, phone.Longitude, latitude, longitude));
        return new GeofenceResult(distance, distance <= radiusM && phone.AccuracyM <= radiusM);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~GeofenceTests"`
Expected: PASS, 7 tests. Then `dotnet test backend/FacilityRealtime.slnx`: 0 failures.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Geo/Geofence.cs backend/tests/FacilityRealtime.UnitTests/GeofenceTests.cs
git commit -m "feat(backend): geofence distance and verdict, flag never block (facility-0035, 0036, 0037)"
```

---

### Task 3: Attendance, blocked-scan, cover and audit tables; GPS and sign-location columns

**Files:**
- Create: `backend/src/FacilityRealtime.Domain/Enums/AttendanceSource.cs`, `BlockReason.cs`, `LocationSource.cs`
- Create: `backend/src/FacilityRealtime.Domain/Entities/IGpsStamped.cs`, `ShiftAttendance.cs`, `BlockedScan.cs`, `CoverAssignment.cs`, `AuditEntry.cs`
- Modify: `backend/src/FacilityRealtime.Domain/Entities/Sign.cs`, `ScanRecord.cs`
- Create: `backend/src/FacilityRealtime.Application/Geo/GpsStamping.cs`
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/AppDbContext.cs`, `DbInitializer.cs`
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/AuditTrail.cs`
- Create: `backend/src/FacilityRealtime.Infrastructure/Migrations/<timestamp>_ScanRules.cs` (+ Designer; snapshot updated) — generated
- Test: `backend/tests/FacilityRealtime.ApiTests/Persistence/SchemaRuleTests.cs` (+2), `backend/tests/FacilityRealtime.ApiTests/Persistence/MySqlReadTests.cs` (+1 block)

**Interfaces:**
- Consumes: `GpsReading`, `Geofence.Evaluate` (Task 2); `AttendanceEvent` (Task 1).
- Produces (Domain.Enums): `AttendanceSource { Scan, AdminAdd, AdminEdit }`, `BlockReason { OtherArea }`, `LocationSource { Site, Map }`.
- Produces (Domain.Entities):
  - `interface IGpsStamped { decimal? Latitude; decimal? Longitude; short? AccuracyM; short? DistanceM; bool? WithinRadius; }` (all get/set)
  - `ShiftAttendance : IGpsStamped { long Id; int UserId; int AreaId; DateOnly ShiftDate; Shift Shift; AttendanceEvent EventType; DateTime OccurredAt; AttendanceSource Source; int? SignId; DateTime CreatedAt; User? User; … GPS }`
  - `BlockedScan : IGpsStamped { long Id; int UserId; int SignId; BlockReason Reason; DateTime ScannedAt; … GPS }`
  - `CoverAssignment { int Id; int UserId; int AreaId; DateOnly ShiftDate; Shift Shift; int AssignedById; DateTime AssignedAt; int? CancelledById; DateTime? CancelledAt; User? User; Area? Area }`
  - `AuditEntry { long Id; int ActorId; DateTime OccurredAt; string Action; string EntityType; long? EntityId; string Summary; string? BeforeJson; string? AfterJson; string? Reason }`
  - `Sign` gains `decimal? Latitude; decimal? Longitude; short? LocationAccuracyM; LocationSource? LocationSource; DateTime? LocatedAt; short RadiusM = 50`.
  - `ScanRecord : IGpsStamped` gains GPS properties and `int? CoverAssignmentId; CoverAssignment? CoverAssignment`.
- Produces (Application.Geo): `static class GpsStamping { void StampGps(this IGpsStamped target, GpsReading phone, Sign sign); }`.
- Produces (Infrastructure.Persistence): DbSets `ShiftAttendances`, `BlockedScans`, `CoverAssignments`, `AuditLog`; `static class AuditTrail { void Add(AppDbContext db, int actorId, DateTime occurredAt, string action, string entityType, long? entityId, string summary, object? before, object? after, string? reason = null); }`.
- Seed: the three AR01 signs get coordinates (`token-checkin-ar01` 13.756300, 100.501800; `token-restroom-m1` 13.756350, 100.501850; `token-restroom-f1` 13.756250, 100.501750), `LocationAccuracyM` 10, `LocationSource` Site, `LocatedAt` now. AR02 signs stay without coordinates. All signs `RadiusM` 50.

- [ ] **Step 1: Write the failing tests**

Add to `backend/tests/FacilityRealtime.ApiTests/Persistence/SchemaRuleTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task Each_attendance_event_is_recorded_once_per_shift()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "E1001");
            var sign = await db.Signs.SingleAsync(s => s.QrToken == "token-checkin-ar01");
            ShiftAttendance Entry() => new()
            {
                UserId = cleaner.Id,
                AreaId = cleaner.AreaId!.Value,
                ShiftDate = new DateOnly(2026, 10, 8),
                Shift = Shift.Day,
                EventType = AttendanceEvent.ShiftIn,
                OccurredAt = DateTime.UtcNow,
                Source = AttendanceSource.Scan,
                SignId = sign.Id,
                CreatedAt = DateTime.UtcNow,
            };

            db.ShiftAttendances.Add(Entry());
            await db.SaveChangesAsync();
            db.ShiftAttendances.Add(Entry());

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Seeded_area_1_signs_have_coordinates_and_area_2_signs_do_not()
    {
        using var factory = new FacilityApiFactory();
        var located = new List<string>();
        var radii = new List<short>();

        await factory.WithDbAsync(async db =>
        {
            located = await db.Signs.Where(s => s.Latitude != null).OrderBy(s => s.Code).Select(s => s.Code).ToListAsync();
            radii = await db.Signs.Select(s => s.RadiusM).ToListAsync();
        });

        Assert.Equal(new[] { "AR01-01", "AR01-02", "AR01-IN" }, located);
        Assert.All(radii, r => Assert.Equal(50, r));
    }
```

In `backend/tests/FacilityRealtime.ApiTests/Persistence/MySqlReadTests.cs`, inside the `write` block after the `ScanRecords.Add(...)`/`SaveChangesAsync()`, add an attendance row and a cover row:

```csharp
                var checkInSign = await write.Signs.SingleAsync(s => s.QrToken == "token-checkin-ar01");
                var cleaner = await write.Users.SingleAsync(u => u.EmployeeId == "E1001");
                var area2 = await write.Areas.SingleAsync(a => a.Code == "AR02");
                write.ShiftAttendances.Add(new ShiftAttendance
                {
                    UserId = cleaner.Id,
                    AreaId = cleaner.AreaId!.Value,
                    ShiftDate = new DateOnly(2026, 10, 8),
                    Shift = Shift.Day,
                    EventType = AttendanceEvent.ShiftIn,
                    OccurredAt = DateTime.UtcNow,
                    Source = AttendanceSource.Scan,
                    SignId = checkInSign.Id,
                    Latitude = 13.756300m,
                    Longitude = 100.501800m,
                    AccuracyM = 10,
                    DistanceM = 0,
                    WithinRadius = true,
                    CreatedAt = DateTime.UtcNow,
                });
                write.CoverAssignments.Add(new CoverAssignment
                {
                    UserId = cleaner.Id,
                    AreaId = area2.Id,
                    ShiftDate = new DateOnly(2026, 10, 8),
                    Shift = Shift.Day,
                    AssignedById = userId,
                    AssignedAt = DateTime.UtcNow,
                });
                await write.SaveChangesAsync();
```

and after the existing `scan` assertions (before `finally`), add:

```csharp
            var attendance = await read.ShiftAttendances.AsNoTracking().SingleAsync();
            Assert.Equal(new DateOnly(2026, 10, 8), attendance.ShiftDate);
            Assert.Equal(AttendanceEvent.ShiftIn, attendance.EventType);
            Assert.Equal(13.756300m, attendance.Latitude);
            Assert.True(attendance.WithinRadius);

            var cover = await read.CoverAssignments.AsNoTracking().SingleAsync();
            Assert.Equal(new DateOnly(2026, 10, 8), cover.ShiftDate);
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~SchemaRuleTests"`
Expected: build FAILS — `ShiftAttendance`, `AttendanceSource`, `Sign.Latitude`, `db.ShiftAttendances` do not exist.

- [ ] **Step 3: Add the domain types**

Create `backend/src/FacilityRealtime.Domain/Enums/AttendanceSource.cs`:

```csharp
namespace FacilityRealtime.Domain.Enums;

/// <summary>facility-0051: Admin additions and edits carry a badge; the original values live in audit_log.</summary>
public enum AttendanceSource
{
    Scan,
    AdminAdd,
    AdminEdit,
}
```

Create `backend/src/FacilityRealtime.Domain/Enums/BlockReason.cs`:

```csharp
namespace FacilityRealtime.Domain.Enums;

/// <summary>Why a scan was refused (CONTEXT.md, Blocked Scan). The Supervisor plan adds building and shift reasons.</summary>
public enum BlockReason
{
    OtherArea,
}
```

Create `backend/src/FacilityRealtime.Domain/Enums/LocationSource.cs`:

```csharp
namespace FacilityRealtime.Domain.Enums;

/// <summary>facility-0045: Site = captured standing at the sign; Map = picked on a map, not yet confirmed on site.</summary>
public enum LocationSource
{
    Site,
    Map,
}
```

Create `backend/src/FacilityRealtime.Domain/Entities/IGpsStamped.cs`:

```csharp
namespace FacilityRealtime.Domain.Entities;

/// <summary>
/// The GPS column set of database.html on every on-site record. Latitude, Longitude and AccuracyM are raw and are
/// cleared after 90 days (facility-0055); DistanceM and WithinRadius stay. WithinRadius false = Geofence Flag; null = no verdict.
/// </summary>
public interface IGpsStamped
{
    decimal? Latitude { get; set; }
    decimal? Longitude { get; set; }
    short? AccuracyM { get; set; }
    short? DistanceM { get; set; }
    bool? WithinRadius { get; set; }
}
```

Create `backend/src/FacilityRealtime.Domain/Entities/ShiftAttendance.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Attendance Event: one of a Cleaner's four scans in one shift. Unique per event per shift; the first wins.</summary>
public class ShiftAttendance : IGpsStamped
{
    public long Id { get; set; }
    public int UserId { get; set; }

    /// <summary>The Cleaner's own Area at the time of the scan.</summary>
    public int AreaId { get; set; }

    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public AttendanceEvent EventType { get; set; }
    public DateTime OccurredAt { get; set; }
    public AttendanceSource Source { get; set; }

    /// <summary>Null when an Admin added the event.</summary>
    public int? SignId { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public short? AccuracyM { get; set; }
    public short? DistanceM { get; set; }
    public bool? WithinRadius { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
```

Create `backend/src/FacilityRealtime.Domain/Entities/BlockedScan.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Blocked Scan: a scan the system refused (facility-0041), kept so the Admin sees it.</summary>
public class BlockedScan : IGpsStamped
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int SignId { get; set; }
    public BlockReason Reason { get; set; }
    public DateTime ScannedAt { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public short? AccuracyM { get; set; }
    public short? DistanceM { get; set; }
    public bool? WithinRadius { get; set; }
}
```

Create `backend/src/FacilityRealtime.Domain/Entities/CoverAssignment.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Cover Assignment: an Admin lets a Cleaner work another Area for one shift; it lapses when the shift ends.</summary>
public class CoverAssignment
{
    public int Id { get; set; }

    /// <summary>The Cleaner who covers.</summary>
    public int UserId { get; set; }

    /// <summary>The Area being covered.</summary>
    public int AreaId { get; set; }

    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public int AssignedById { get; set; }
    public DateTime AssignedAt { get; set; }
    public int? CancelledById { get; set; }
    public DateTime? CancelledAt { get; set; }

    public User? User { get; set; }
    public Area? Area { get; set; }
}
```

Create `backend/src/FacilityRealtime.Domain/Entities/AuditEntry.cs`:

```csharp
namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Audit Log: one Admin change (facility-0051). Read-only for the app once written.</summary>
public class AuditEntry
{
    public long Id { get; set; }
    public int ActorId { get; set; }
    public DateTime OccurredAt { get; set; }

    /// <summary>e.g. COVER_ASSIGN, COVER_CANCEL, ATTENDANCE_EDIT.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The table that changed, e.g. cover_assignments.</summary>
    public string EntityType { get; set; } = string.Empty;

    public long? EntityId { get; set; }

    /// <summary>The line shown on the Admin history page.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Never contains a phone number or a password.</summary>
    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    /// <summary>Required for attendance corrections.</summary>
    public string? Reason { get; set; }
}
```

In `backend/src/FacilityRealtime.Domain/Entities/Sign.cs`, add `using FacilityRealtime.Domain.Enums;` at the top and these properties after `QrIssuedAt`:

```csharp
    /// <summary>Captured by the Admin standing at the sign (facility-0045). Null until captured: scans then get no verdict.</summary>
    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    /// <summary>Accuracy the Admin's phone reported when capturing, e.g. 18.</summary>
    public short? LocationAccuracyM { get; set; }

    public LocationSource? LocationSource { get; set; }
    public DateTime? LocatedAt { get; set; }

    /// <summary>facility-0038: default 50 m, editable per sign.</summary>
    public short RadiusM { get; set; } = 50;
```

In `backend/src/FacilityRealtime.Domain/Entities/ScanRecord.cs`, change the class line to `public class ScanRecord : IGpsStamped` and add after `SubmittedAt`:

```csharp
    /// <summary>Set when the Cleaner submitted for an Area they were assigned to cover (facility-0041).</summary>
    public int? CoverAssignmentId { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public short? AccuracyM { get; set; }
    public short? DistanceM { get; set; }
    public bool? WithinRadius { get; set; }
```

and with the other navigations: `public CoverAssignment? CoverAssignment { get; set; }`.

Create `backend/src/FacilityRealtime.Application/Geo/GpsStamping.cs`:

```csharp
using FacilityRealtime.Domain.Entities;

namespace FacilityRealtime.Application.Geo;

public static class GpsStamping
{
    /// <summary>Stores the phone's fix and its verdict against <paramref name="sign"/> (facility-0035/0037: flagged, never blocked).</summary>
    public static void StampGps(this IGpsStamped target, GpsReading phone, Sign sign)
    {
        var verdict = Geofence.Evaluate(phone, (double?)sign.Latitude, (double?)sign.Longitude, sign.RadiusM);
        target.Latitude = Math.Round((decimal)phone.Latitude, 6);
        target.Longitude = Math.Round((decimal)phone.Longitude, 6);
        target.AccuracyM = ToSmallint(Math.Ceiling(phone.AccuracyM));
        target.DistanceM = verdict.DistanceM is int distance ? ToSmallint(distance) : null;
        target.WithinRadius = verdict.WithinRadius;
    }

    private static short ToSmallint(double value) => (short)Math.Min(value, short.MaxValue);
}
```

- [ ] **Step 4: Map the tables, write the audit helper and the seed**

In `backend/src/FacilityRealtime.Infrastructure/Persistence/AppDbContext.cs`:

1. Add `using Microsoft.EntityFrameworkCore.Metadata.Builders;` to the usings.
2. Add the DbSets after `InspectionRecords`:

```csharp
    public DbSet<ShiftAttendance> ShiftAttendances => Set<ShiftAttendance>();
    public DbSet<BlockedScan> BlockedScans => Set<BlockedScan>();
    public DbSet<CoverAssignment> CoverAssignments => Set<CoverAssignment>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();
```

3. In the `Sign` block, add:

```csharp
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.LocationSource).HasConversion(new UpperSnakeEnumConverter<LocationSource>()).HasMaxLength(10);
```

4. In the `ScanRecord` block, add:

```csharp
            MapGps(e);
            e.HasOne(x => x.CoverAssignment).WithMany().HasForeignKey(x => x.CoverAssignmentId).OnDelete(DeleteBehavior.Restrict);
```

5. Before `modelBuilder.UseSnakeCaseColumns();`, add:

```csharp
        modelBuilder.Entity<ShiftAttendance>(e =>
        {
            e.ToTable("shift_attendances");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.Property(x => x.EventType).HasConversion(new UpperSnakeEnumConverter<AttendanceEvent>()).HasMaxLength(20);
            e.Property(x => x.Source).HasConversion(new UpperSnakeEnumConverter<AttendanceSource>()).HasMaxLength(20);
            MapGps(e);
            // facility-0026: each event once per shift, the first press wins
            e.HasIndex(x => new { x.UserId, x.ShiftDate, x.Shift, x.EventType }).IsUnique();
            e.HasIndex(x => new { x.ShiftDate, x.Shift, x.AreaId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Sign>().WithMany().HasForeignKey(x => x.SignId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BlockedScan>(e =>
        {
            e.ToTable("blocked_scans");
            e.Property(x => x.Reason).HasConversion(new UpperSnakeEnumConverter<BlockReason>()).HasMaxLength(20);
            MapGps(e);
            e.HasIndex(x => x.ScannedAt);
            e.HasIndex(x => new { x.SignId, x.ScannedAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Sign>().WithMany().HasForeignKey(x => x.SignId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CoverAssignment>(e =>
        {
            e.ToTable("cover_assignments");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.HasIndex(x => new { x.UserId, x.ShiftDate, x.Shift });
            e.HasIndex(x => new { x.AreaId, x.ShiftDate, x.Shift });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_log");
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(40).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(300).IsRequired();
            e.Property(x => x.BeforeJson).HasColumnType("json");
            e.Property(x => x.AfterJson).HasColumnType("json");
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => x.OccurredAt);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
```

6. Add this private helper method to the class (after `OnModelCreating`):

```csharp
    /// <summary>database.html GPS column set: DECIMAL(9,6) coordinates; SMALLINT accuracy and distance follow from short.</summary>
    private static void MapGps<T>(EntityTypeBuilder<T> e) where T : class, IGpsStamped
    {
        e.Property(x => x.Latitude).HasPrecision(9, 6);
        e.Property(x => x.Longitude).HasPrecision(9, 6);
    }
```

Create `backend/src/FacilityRealtime.Infrastructure/Persistence/AuditTrail.cs`:

```csharp
using System.Text.Json;
using FacilityRealtime.Domain.Entities;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>facility-0051: every Admin change is logged — who, when, what, before and after. Save it with the change itself.</summary>
public static class AuditTrail
{
    public static void Add(
        AppDbContext db,
        int actorId,
        DateTime occurredAt,
        string action,
        string entityType,
        long? entityId,
        string summary,
        object? before,
        object? after,
        string? reason = null) =>
        db.AuditLog.Add(new AuditEntry
        {
            ActorId = actorId,
            OccurredAt = occurredAt,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            Reason = reason,
        });
}
```

In `backend/src/FacilityRealtime.Infrastructure/Persistence/DbInitializer.cs`, replace the first three `new Sign { Area = area1, ... }` entries of `db.Signs.AddRange(...)` with:

```csharp
            Located(new Sign { Area = area1, Code = "AR01-IN", QrToken = "token-checkin-ar01", QrIssuedAt = now }, 13.756300m, 100.501800m),
            Located(new Sign { Area = area1, ServicePoint = menRestroom, Code = "AR01-01", QrToken = "token-restroom-m1", QrIssuedAt = now }, 13.756350m, 100.501850m),
            Located(new Sign { Area = area1, ServicePoint = womenRestroom, Code = "AR01-02", QrToken = "token-restroom-f1", QrIssuedAt = now }, 13.756250m, 100.501750m),
```

and add this local function next to `Window` at the end of `SeedAsync`:

```csharp
        // Made-up coordinates; AR02's signs are left uncaptured so both cases can be tried
        Sign Located(Sign sign, decimal latitude, decimal longitude)
        {
            sign.Latitude = latitude;
            sign.Longitude = longitude;
            sign.LocationAccuracyM = 10;
            sign.LocationSource = LocationSource.Site;
            sign.LocatedAt = now;
            return sign;
        }
```

- [ ] **Step 5: Run the SQLite tests**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: 0 failures except `MigrationTests.Migrations_match_the_model`, which FAILS (the model changed; the next step adds the migration).

- [ ] **Step 6: Generate the migration**

```bash
dotnet tool restore
dotnet ef migrations add ScanRules --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api
dotnet ef migrations script PilotSchema ScanRules --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api --output "$TMP/scan-rules.sql"
grep -c "CREATE TABLE" "$TMP/scan-rules.sql"
grep -c "longtext" "$TMP/scan-rules.sql"
grep -E "ALTER TABLE \`(signs|scan_records)\` ADD" "$TMP/scan-rules.sql" | wc -l
```

Expected: `4` (shift_attendances, blocked_scans, cover_assignments, audit_log), `0`, and `12` added columns (signs: latitude, longitude, location_accuracy_m, location_source, located_at, radius_m; scan_records: cover_assignment_id, latitude, longitude, accuracy_m, distance_m, within_radius). If the provider writes several columns in one `ALTER TABLE`, count the column names instead — every one must appear. Check by eye that `latitude`/`longitude` are `decimal(9,6)`, `accuracy_m`/`distance_m` are `smallint`, `within_radius` is `tinyint(1)`, `radius_m` is `smallint NOT NULL`, the attendance unique index is `(user_id, shift_date, shift, event_type)`, and `before_json`/`after_json` are `json`. `$TMP` is any temp folder outside the repo.

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: 0 failures (the MySQL test is skipped).

- [ ] **Step 7: Verify on real MySQL**

```bash
FACILITY_MYSQL_TEST="Server=localhost;Port=3306;Database=facility_scanrules_test;Uid=root;Pwd=root;CharSet=utf8mb4;" dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~MySqlReadTests"
dotnet ef database update --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api --connection "Server=localhost;Port=3306;Database=facility_migration_test;Uid=root;Pwd=root;CharSet=utf8mb4;"
"/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -uroot -proot -e "DROP DATABASE IF EXISTS facility_migration_test; DROP DATABASE IF EXISTS facility_scanrules_test;"
```

Expected: the MySQL test PASSES (1/1); `database update` applies `PilotSchema` then `ScanRules` with no error; both throwaway databases are dropped. Never point these commands at `facility_dashboard`.

- [ ] **Step 8: Commit**

```bash
git add backend/src backend/tests
git status --short
git commit -m "feat(backend): attendance, blocked-scan, cover and audit tables; GPS on scans; sign locations (database.html)"
```

---

### Task 4: Attendance endpoints

**Files:**
- Create: `backend/src/FacilityRealtime.Api/Auth/CurrentUser.cs`, `backend/src/FacilityRealtime.Api/Endpoints/ApiResults.cs`, `GpsInput.cs`, `BlockedScans.cs`, `AttendanceEndpoints.cs`, `backend/src/FacilityRealtime.Api/DTOs/AttendanceDtos.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`, `backend/src/FacilityRealtime.Api/appsettings.json`
- Create (tests): `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestGps.cs`, `AttendanceApi.cs`; modify `AuthApi.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Attendance/AttendanceEndpointTests.cs`

**Interfaces:**
- Consumes: `AttendanceSettings`, `AttendanceCalendar.SlotFor`, `AttendanceRules` (Task 1); `GpsReading` (Task 2); `ShiftAttendance`, `BlockedScan`, `GpsStamping.StampGps` (Task 3).
- Produces (Api.Auth): `static class CurrentUser { Task<User?> LoadAsync(ClaimsPrincipal principal, AppDbContext db); }` — null when missing or deactivated.
- Produces (Api.Endpoints): `ApiResults.Message(int statusCode, string message) : IResult` (`{ "message": … }`); `GpsInput.Required : string`, `GpsInput.Read(double? latitude, double? longitude, double? accuracyM) : GpsReading?`; `BlockedScans.BlockAsync(AppDbContext db, User user, Sign sign, Area signArea, GpsReading gps, DateTime nowUtc) : Task<IResult>` (saves the row, answers 403 `BlockedScanResponse`); `AttendanceEndpoints.LoadEventsAsync(AppDbContext db, int userId, ShiftSlot slot) : Task<List<ShiftAttendance>>` and `AttendanceEndpoints.StateForAsync(AppDbContext db, User user, DateTime nowUtc, AttendanceSettings settings) : Task<AttendanceStateDto>` (both `internal`, used by Tasks 5 and 7).
- Produces (Api.DTOs): `RecordAttendanceRequest(string QrToken, AttendanceEvent? EventType, double? Latitude, double? Longitude, double? AccuracyM)`; `AttendanceEntryDto(AttendanceEvent EventType, DateTime OccurredAt, AttendanceSource Source, bool? WithinRadius)`; `enum AttendanceOutcome { Recorded, AlreadyRecorded, TooSoon }`; `AttendanceStateDto(DateOnly? ShiftDate, Shift? Shift, IReadOnlyList<AttendanceEntryDto> Events, IReadOnlyList<AttendanceEvent> NextEvents, AttendanceOutcome? Outcome)`; `AttendanceRefusedDto(string Message, IReadOnlyList<AttendanceEvent> NextEvents)`; `BlockedScanResponse(string Message, string AreaCode, string AreaName)`.
- Produces (routes): `POST /api/attendance` → 200 `AttendanceStateDto` (Recorded / AlreadyRecorded / TooSoon); 400 missing event or location; 403 not a Cleaner, or another Area's sign (`BlockedScanResponse`, row in `blocked_scans`); 404 unknown token or a point sign; 409 outside the attendance window (`{message}`) or out of order (`AttendanceRefusedDto`). `GET /api/attendance/me` → 200 `AttendanceStateDto` (ShiftDate/Shift null and empty lists outside every window); 403 not a Cleaner.
- Produces (tests): `TestGps.NearLatitude/NearLongitude` (13.7563, 100.5018 — the AR01 signs), `TestGps.FarLatitude/FarLongitude` (13.8, 100.6 — about 12 km away); `AttendanceApi.RecordAsync(HttpClient, string eventType, string qrToken = "token-checkin-ar01", double latitude = Near, double longitude = Near, double accuracyM = 10)`; `AttendanceApi.CheckInAsync(FacilityApiFactory, HttpClient, DateTimeOffset at, string qrToken = "token-checkin-ar01")` (moves the clock, records Shift-In, asserts 200); `AuthApi.LoggedInAsEmployeeAsync(FacilityApiFactory, string employeeId, string phone)`.

- [ ] **Step 1: Add the test helpers**

Create `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestGps.cs`:

```csharp
namespace FacilityRealtime.ApiTests.Infrastructure;

public static class TestGps
{
    /// <summary>The seeded AR01 signs stand within about 10 m of this point (DbInitializer).</summary>
    public const double NearLatitude = 13.7563;

    public const double NearLongitude = 100.5018;

    /// <summary>About 12 km from the AR01 signs.</summary>
    public const double FarLatitude = 13.8;

    public const double FarLongitude = 100.6;
}
```

Create `backend/tests/FacilityRealtime.ApiTests/Infrastructure/AttendanceApi.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;

namespace FacilityRealtime.ApiTests.Infrastructure;

public static class AttendanceApi
{
    public const string Area1CheckIn = "token-checkin-ar01";

    public static Task<HttpResponseMessage> RecordAsync(
        HttpClient client,
        string eventType,
        string qrToken = Area1CheckIn,
        double latitude = TestGps.NearLatitude,
        double longitude = TestGps.NearLongitude,
        double accuracyM = 10) =>
        client.PostAsJsonAsync("/api/attendance", new { qrToken, eventType, latitude, longitude, accuracyM });

    /// <summary>Moves the clock to <paramref name="at"/> and records Shift-In; a repeat just returns the first time.</summary>
    public static async Task CheckInAsync(FacilityApiFactory factory, HttpClient client, DateTimeOffset at, string qrToken = Area1CheckIn)
    {
        factory.Clock.SetUtcNow(at);
        var response = await RecordAsync(client, "ShiftIn", qrToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

In `backend/tests/FacilityRealtime.ApiTests/Infrastructure/AuthApi.cs`, add next to `LoggedInAdminAsync`:

```csharp
    public static Task<(HttpClient Client, AuthResponseModel Auth, string RefreshToken)> LoggedInAsEmployeeAsync(
        FacilityApiFactory factory, string employeeId, string phone) =>
        LoggedInWithAsync(factory, client => LoginAsync(client, employeeId, phone));
```

- [ ] **Step 2: Write the failing test**

Create `backend/tests/FacilityRealtime.ApiTests/Attendance/AttendanceEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Attendance;

public record AttendanceEntryModel(string EventType, DateTime OccurredAt, string Source, bool? WithinRadius);

public record AttendanceStateModel(DateOnly? ShiftDate, string? Shift, List<AttendanceEntryModel> Events, List<string> NextEvents, string? Outcome);

public record AttendanceRefusedModel(string Message, List<string> NextEvents);

public record BlockedScanModel(string Message, string AreaCode, string AreaName);

/// <summary>E1001 is AR01's Day cleaner, E1002 its Night cleaner; AR01's check-in sign is at TestGps.Near. Log in before moving the clock.</summary>
public class AttendanceEndpointTests
{
    private static async Task<AttendanceStateModel> RecordAtAsync(FacilityApiFactory factory, HttpClient client, DateTimeOffset at, string eventType)
    {
        factory.Clock.SetUtcNow(at);
        var response = await AttendanceApi.RecordAsync(client, eventType);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AttendanceStateModel>())!;
    }

    private static async Task<List<T>> AllAsync<T>(FacilityApiFactory factory, Func<AppDbContext, IQueryable<T>> query)
    {
        var rows = new List<T>();
        await factory.WithDbAsync(async db => rows = await query(db).ToListAsync());
        return rows;
    }

    [Fact]
    public async Task Recording_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await AttendanceApi.RecordAsync(factory.CreateApiClient(), "ShiftIn");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cleaner_checks_in_at_the_own_area_sign()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var state = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 5), "ShiftIn");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(new DateOnly(2026, 10, 8), state.ShiftDate);
        Assert.Equal("Day", state.Shift);
        Assert.Equal("ShiftIn", Assert.Single(state.Events).EventType);
        Assert.True(state.Events[0].WithinRadius);
        Assert.Equal(new[] { "BreakOut", "ShiftOut" }, state.NextEvents);
        var stored = Assert.Single(await AllAsync(factory, db => db.ShiftAttendances));
        Assert.Equal(AttendanceSource.Scan, stored.Source);
        Assert.Equal((short)0, stored.DistanceM);
        Assert.Equal(13.756300m, stored.Latitude);
    }

    [Theory]
    [InlineData(6, 0, HttpStatusCode.OK)]
    [InlineData(5, 59, HttpStatusCode.Conflict)]
    public async Task Day_cleaner_may_check_in_from_one_hour_before_the_shift(int hour, int minute, HttpStatusCode expected)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, hour, minute));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Night_shift_out_next_morning_belongs_to_the_night_that_started_yesterday()
    {
        using var factory = new FacilityApiFactory();
        var (night, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E1002", "0810000002");
        await RecordAtAsync(factory, night, ThaiClock.At(8, 19, 0), "ShiftIn");

        var state = await RecordAtAsync(factory, night, ThaiClock.At(9, 7, 50), "ShiftOut");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(new DateOnly(2026, 10, 8), state.ShiftDate);
        Assert.Equal("Night", state.Shift);
    }

    [Fact]
    public async Task Night_attendance_closes_an_hour_after_the_shift()
    {
        using var factory = new FacilityApiFactory();
        var (night, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E1002", "0810000002");
        await RecordAtAsync(factory, night, ThaiClock.At(8, 19, 0), "ShiftIn");
        factory.Clock.SetUtcNow(ThaiClock.At(9, 8, 1));

        var response = await AttendanceApi.RecordAsync(night, "ShiftOut");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Pressing_again_keeps_the_first_time()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 0), "ShiftIn");

        var state = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 30), "ShiftIn");

        Assert.Equal("AlreadyRecorded", state.Outcome);
        Assert.Equal(ThaiClock.At(8, 7, 0).UtcDateTime, Assert.Single(state.Events).OccurredAt.ToUniversalTime());
    }

    [Fact]
    public async Task A_different_event_within_five_minutes_is_ignored()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 0), "ShiftIn");

        var tooSoon = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 3), "BreakOut");
        var later = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 5), "BreakOut");

        Assert.Equal("TooSoon", tooSoon.Outcome);
        Assert.Single(tooSoon.Events);
        Assert.Equal("Recorded", later.Outcome);
        Assert.Equal(2, later.Events.Count);
    }

    [Fact]
    public async Task Out_of_order_event_is_refused_with_the_next_allowed_ones()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 0), "ShiftIn");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "BreakIn");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var refused = (await response.Content.ReadFromJsonAsync<AttendanceRefusedModel>())!;
        Assert.Equal(new[] { "BreakOut", "ShiftOut" }, refused.NextEvents);
    }

    [Fact]
    public async Task Shift_out_may_follow_a_break_out_when_break_in_was_forgotten()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 7, 0), "ShiftIn");
        await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 12, 0), "BreakOut");

        var state = await RecordAtAsync(factory, cleaner, ThaiClock.At(8, 19, 0), "ShiftOut");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Empty(state.NextEvents);
    }

    [Fact]
    public async Task Check_in_at_another_areas_sign_is_blocked_and_kept_for_the_admin()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", qrToken: "token-checkin-ar02");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("AR02", (await response.Content.ReadFromJsonAsync<BlockedScanModel>())!.AreaCode);
        var blocked = Assert.Single(await AllAsync(factory, db => db.BlockedScans));
        Assert.Equal(BlockReason.OtherArea, blocked.Reason);
        Assert.Null(blocked.WithinRadius); // AR02's sign has no coordinates yet
        Assert.Empty(await AllAsync(factory, db => db.ShiftAttendances));
    }

    [Theory]
    [InlineData("token-restroom-m1")]
    [InlineData("no-such-token")]
    public async Task Point_signs_and_unknown_tokens_are_not_check_in_signs(string token)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", qrToken: token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Location_is_required()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await cleaner.PostAsJsonAsync("/api/attendance", new { qrToken = AttendanceApi.Area1CheckIn, eventType = "ShiftIn" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Far_check_in_is_saved_and_flagged()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(cleaner, "ShiftIn", latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = Assert.Single(await AllAsync(factory, db => db.ShiftAttendances));
        Assert.False(stored.WithinRadius);
        Assert.True(stored.DistanceM > 50);
    }

    [Fact]
    public async Task Only_cleaners_record_attendance()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await AttendanceApi.RecordAsync(admin, "ShiftIn");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task My_attendance_shows_what_can_be_pressed_next()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 6, 10));
        var before = (await cleaner.GetFromJsonAsync<AttendanceStateModel>("/api/attendance/me"))!;
        factory.Clock.SetUtcNow(ThaiClock.At(8, 5, 0));
        var outside = (await cleaner.GetFromJsonAsync<AttendanceStateModel>("/api/attendance/me"))!;

        Assert.Equal(new DateOnly(2026, 10, 8), before.ShiftDate);
        Assert.Empty(before.Events);
        Assert.Equal(new[] { "ShiftIn" }, before.NextEvents);
        Assert.Null(outside.ShiftDate);
        Assert.Empty(outside.NextEvents);
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AttendanceEndpointTests"`
Expected: the tests compile and FAIL — `/api/attendance` returns 404/405.

- [ ] **Step 4: Write the implementation**

Create `backend/src/FacilityRealtime.Api/Auth/CurrentUser.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Auth;

public static class CurrentUser
{
    /// <summary>facility-0017: the account in the access token, read fresh; null when it is missing or deactivated.</summary>
    public static async Task<User?> LoadAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var user = int.TryParse(principal.FindFirstValue(AuthClaims.UserId), out var id) ? await db.Users.FindAsync(id) : null;
        return user is { IsActive: true } ? user : null;
    }
}
```

Create `backend/src/FacilityRealtime.Api/Endpoints/ApiResults.cs`:

```csharp
namespace FacilityRealtime.Api.Endpoints;

internal static class ApiResults
{
    /// <summary>A Thai message the phone shows as is.</summary>
    public static IResult Message(int statusCode, string message) => Results.Json(new { message }, statusCode: statusCode);
}
```

Create `backend/src/FacilityRealtime.Api/Endpoints/GpsInput.cs`:

```csharp
using FacilityRealtime.Application.Geo;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0036: without a location the scan is refused — the one exception to "flag, never block".</summary>
internal static class GpsInput
{
    public const string Required = "ต้องเปิด Location และอนุญาตให้เว็บอ่านตำแหน่งก่อนสแกน";

    public static GpsReading? Read(double? latitude, double? longitude, double? accuracyM) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && accuracyM is >= 0 and <= 10_000
            ? new GpsReading(latitude.Value, longitude.Value, accuracyM.Value)
            : null;
}
```

Create `backend/src/FacilityRealtime.Api/DTOs/AttendanceDtos.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>Latitude, Longitude and AccuracyM are what the browser's Geolocation API reported.</summary>
public record RecordAttendanceRequest(string QrToken, AttendanceEvent? EventType, double? Latitude, double? Longitude, double? AccuracyM);

public record AttendanceEntryDto(AttendanceEvent EventType, DateTime OccurredAt, AttendanceSource Source, bool? WithinRadius);

public enum AttendanceOutcome
{
    Recorded,

    /// <summary>facility-0026: the event already exists; the first time is kept.</summary>
    AlreadyRecorded,

    /// <summary>facility-0031: a different event within RepeatIgnoreMinutes of the latest one is not counted.</summary>
    TooSoon,
}

/// <summary>ShiftDate and Shift are null, and the lists empty, outside every attendance window of the Cleaner's shift.</summary>
public record AttendanceStateDto(
    DateOnly? ShiftDate,
    Shift? Shift,
    IReadOnlyList<AttendanceEntryDto> Events,
    IReadOnlyList<AttendanceEvent> NextEvents,
    AttendanceOutcome? Outcome);

public record AttendanceRefusedDto(string Message, IReadOnlyList<AttendanceEvent> NextEvents);

/// <summary>facility-0041: tells the phone which Area the scanned sign belongs to.</summary>
public record BlockedScanResponse(string Message, string AreaCode, string AreaName);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/BlockedScans.cs`:

```csharp
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Endpoints;

internal static class BlockedScans
{
    /// <summary>facility-0041: nothing is recorded for the scan itself; the Admin sees the Blocked Scan instead.</summary>
    public static async Task<IResult> BlockAsync(AppDbContext db, User user, Sign sign, Area signArea, GpsReading gps, DateTime nowUtc)
    {
        var blocked = new BlockedScan { UserId = user.Id, SignId = sign.Id, Reason = BlockReason.OtherArea, ScannedAt = nowUtc };
        blocked.StampGps(gps, sign);
        db.BlockedScans.Add(blocked);
        await db.SaveChangesAsync();

        return Results.Json(
            new BlockedScanResponse("ป้ายนี้ไม่ใช่ Area ของคุณ", signArea.Code, signArea.Name),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
```

Create `backend/src/FacilityRealtime.Api/Endpoints/AttendanceEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

public static class AttendanceEndpoints
{
    private const string CleanersOnly = "การลงเวลาใช้สำหรับแม่บ้านเท่านั้น";

    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/attendance", RecordAsync).RequireAuthorization();
        app.MapGet("/api/attendance/me", GetMineAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> RecordAsync(
        RecordAttendanceRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.Role != UserRole.Cleaner)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, CleanersOnly);
        }

        if (request.EventType is not { } eventType || !Enum.IsDefined(eventType))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุประเภทการลงเวลา");
        }

        if (GpsInput.Read(request.Latitude, request.Longitude, request.AccuracyM) is not { } gps)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, GpsInput.Required);
        }

        var sign = await db.Signs.Include(s => s.Area)
            .FirstOrDefaultAsync(s => s.QrToken == request.QrToken && s.ServicePointId == null);
        if (sign?.Area is not { IsActive: true } area)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบป้ายลงเวลานี้");
        }

        var now = clock.GetUtcNow().UtcDateTime;

        // facility-0041: decided by the sign's QR Token; a Cleaner covering another Area still checks in at their own sign
        if (area.Id != user.AreaId)
        {
            return await BlockedScans.BlockAsync(db, user, sign, area, gps, now);
        }

        var slot = user.Shift is { } shift ? AttendanceCalendar.SlotFor(shift, now, settings) : null;
        if (slot is null || !area.HasShift(slot.Shift))
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "ตอนนี้ยังไม่อยู่ในช่วงลงเวลาของกะคุณ");
        }

        var existing = await LoadEventsAsync(db, user.Id, slot);
        var recorded = existing.Select(a => a.EventType).ToList();
        if (recorded.Contains(eventType))
        {
            return Results.Ok(State(slot, existing, AttendanceOutcome.AlreadyRecorded));
        }

        var latest = existing.MaxBy(a => a.OccurredAt);
        if (latest is not null && now - latest.OccurredAt < TimeSpan.FromMinutes(settings.RepeatIgnoreMinutes))
        {
            return Results.Ok(State(slot, existing, AttendanceOutcome.TooSoon));
        }

        var allowed = AttendanceRules.NextAllowed(recorded);
        if (!allowed.Contains(eventType))
        {
            return Results.Json(new AttendanceRefusedDto("ลงเวลาไม่ตรงลำดับ", allowed), statusCode: StatusCodes.Status409Conflict);
        }

        var entry = new ShiftAttendance
        {
            UserId = user.Id,
            AreaId = area.Id,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            EventType = eventType,
            OccurredAt = now,
            Source = AttendanceSource.Scan,
            SignId = sign.Id,
            CreatedAt = now,
        };
        entry.StampGps(gps, sign);
        db.ShiftAttendances.Add(entry);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // facility-0026: two presses raced and the unique index kept the first; anything else is a real error
            db.ChangeTracker.Clear();
            var recordedNow = await LoadEventsAsync(db, user.Id, slot);
            if (!recordedNow.Any(a => a.EventType == eventType))
            {
                throw;
            }

            return Results.Ok(State(slot, recordedNow, AttendanceOutcome.AlreadyRecorded));
        }

        return Results.Ok(State(slot, [.. existing, entry], AttendanceOutcome.Recorded));
    }

    private static async Task<IResult> GetMineAsync(ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        return user.Role != UserRole.Cleaner
            ? ApiResults.Message(StatusCodes.Status403Forbidden, CleanersOnly)
            : Results.Ok(await StateForAsync(db, user, clock.GetUtcNow().UtcDateTime, settings));
    }

    internal static async Task<AttendanceStateDto> StateForAsync(AppDbContext db, User user, DateTime nowUtc, AttendanceSettings settings)
    {
        var slot = user.Shift is { } shift ? AttendanceCalendar.SlotFor(shift, nowUtc, settings) : null;
        return slot is null
            ? new AttendanceStateDto(null, null, [], [], null)
            : State(slot, await LoadEventsAsync(db, user.Id, slot), null);
    }

    internal static Task<List<ShiftAttendance>> LoadEventsAsync(AppDbContext db, int userId, ShiftSlot slot) =>
        db.ShiftAttendances.AsNoTracking()
            .Where(a => a.UserId == userId && a.ShiftDate == slot.ShiftDate && a.Shift == slot.Shift)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync();

    private static AttendanceStateDto State(ShiftSlot slot, IReadOnlyList<ShiftAttendance> events, AttendanceOutcome? outcome) =>
        new(
            slot.ShiftDate,
            slot.Shift,
            events.OrderBy(a => a.OccurredAt)
                .Select(a => new AttendanceEntryDto(a.EventType, DateTime.SpecifyKind(a.OccurredAt, DateTimeKind.Utc), a.Source, a.WithinRadius))
                .ToList(),
            AttendanceRules.NextAllowed(events.Select(a => a.EventType).ToList()),
            outcome);
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add `using FacilityRealtime.Application.Attendance;` and, after the LoginThrottle registrations:

```csharp
var attendance = builder.Configuration.GetSection(AttendanceSettings.SectionName).Get<AttendanceSettings>() ?? new AttendanceSettings();
if (attendance.OpensMinutesBeforeShift is < 0 or > 360 || attendance.ClosesMinutesAfterShift is < 0 or > 360 || attendance.RepeatIgnoreMinutes < 0)
{
    throw new InvalidOperationException("Attendance: window minutes must be 0-360 and RepeatIgnoreMinutes 0 or more.");
}

builder.Services.AddSingleton(attendance);
```

and `app.MapAttendanceEndpoints();` after `app.MapMeEndpoints();`.

In `backend/src/FacilityRealtime.Api/appsettings.json`, add after the `"LoginThrottle"` block (mind the comma):

```json
  "Attendance": {
    "OpensMinutesBeforeShift": 60,
    "ClosesMinutesAfterShift": 60,
    "RepeatIgnoreMinutes": 5
  }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AttendanceEndpointTests"`
Expected: PASS, 17 tests. Then `dotnet test backend/FacilityRealtime.slnx`: 0 failures.

- [ ] **Step 6: Commit**

```bash
git add backend/src/FacilityRealtime.Api backend/tests/FacilityRealtime.ApiTests
git commit -m "feat(backend): Cleaners record the four attendance events at their Area's sign (facility-0026, 0031, 0041, 0069)"
```

---

### Task 5: Scan submission rules

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/ScanRecordEndpoints.cs` (rewrite `CreateAsync`), `backend/src/FacilityRealtime.Api/DTOs/PointDtos.cs` (two records), `backend/src/FacilityRealtime.Infrastructure/Persistence/PointBoardQuery.cs` (overload)
- Modify: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs` (+ `AddCoverAsync`)
- Test: `backend/tests/FacilityRealtime.ApiTests/Points/ScanRecordEndpointTests.cs` (rewrite)

**Interfaces:**
- Consumes: `CurrentUser`, `ApiResults`, `GpsInput`, `BlockedScans`, `AttendanceEndpoints.LoadEventsAsync`, `AttendanceSettings` (Task 4); `AttendanceCalendar`, `AttendanceRules.IsOnDuty` (Task 1); `CoverAssignment`, `StampGps` (Task 3); test helpers `AttendanceApi`, `TestGps`, `AuthApi.LoggedInAsEmployeeAsync` (Task 4).
- Produces: `PointBoardQuery.LoadAsync(AppDbContext db, DateTime nowUtc, ShiftSlot slot, IReadOnlyCollection<int>? areaIds, int? servicePointId = null)` (the existing 3-argument overload delegates to it with `ShiftCalendar.SlotAt(nowUtc)` and no Area filter). The scan response's `NewPointStatus` is the point seen from the Cleaner's shift; the SignalR update stays the shift running now.
- Produces: `CreateScanRecordRequest(string QrToken, CleaningStatus? Status, List<string>? IssueTags, string? Notes, double? Latitude, double? Longitude, double? AccuracyM)`; `ScanRecordCreatedResponse(long ScanRecordId, int ServicePointId, Placement Placement, int? LateMinutes, PointStatus NewPointStatus, int? DistanceM, bool? WithinRadius, DateTime SubmittedAt)`.
- Produces (route rules for `POST /api/scan-records`, checked in this order): 401 no/deactivated account; 403 not a Cleaner; 400 tags/note too long, missing/undefined status, missing location; 404 unknown token, check-in sign, deactivated point or Area; 403 `BlockedScanResponse` + `blocked_scans` row when the point's Area is not the Cleaner's and no active cover exists for the Cleaner's current shift slot; 409 outside the Cleaner's attendance window, before Shift-In, or after Shift-Out; otherwise 201 with the record stored under the Cleaner's shift slot, placed against that slot's windows, with GPS and verdict, and `CoverAssignmentId` when covering.
- Produces (tests): `TestData.AddCoverAsync(FacilityApiFactory, string employeeId, string areaCode, DateOnly shiftDate, Shift shift, bool cancelled = false) : Task<int>`.

- [ ] **Step 1: Add the test helper**

Add to `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs` (inside the class):

```csharp
    /// <summary>A Cover Assignment written straight to the database, assigned by the seeded Admin.</summary>
    public static async Task<int> AddCoverAsync(
        FacilityApiFactory factory, string employeeId, string areaCode, DateOnly shiftDate, Shift shift, bool cancelled = false)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var area = await db.Areas.SingleAsync(a => a.Code == areaCode);
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            var cover = new CoverAssignment
            {
                UserId = cleaner.Id,
                AreaId = area.Id,
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

- [ ] **Step 2: Rewrite the test file (failing)**

Replace the whole of `backend/tests/FacilityRealtime.ApiTests/Points/ScanRecordEndpointTests.cs` with:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Attendance;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Points;

public record ScanCreatedModel(
    long ScanRecordId, int ServicePointId, string Placement, int? LateMinutes, string NewPointStatus, int? DistanceM, bool? WithinRadius, DateTime SubmittedAt);

/// <summary>
/// Seeded men's restroom (token-restroom-m1, AR01): Day rounds 07:00-09:00 and 16:00-18:00, Night 20:00-22:00 and 03:00-05:00.
/// E1001 = AR01 Day cleaner, E1002 = AR01 Night cleaner, E1003 = AR02 Day cleaner. Log in before moving the clock.
/// </summary>
public class ScanRecordEndpointTests
{
    private const string MenRestroomToken = "token-restroom-m1";
    private const string MeetingRoomToken = "token-meeting-room";

    private static object Body(
        string qrToken = MenRestroomToken,
        string status = "Normal",
        double latitude = TestGps.NearLatitude,
        double longitude = TestGps.NearLongitude,
        double accuracyM = 10) =>
        new { qrToken, status, latitude, longitude, accuracyM };

    /// <summary>Checks in at AR01's sign at <paramref name="at"/>, then submits <paramref name="body"/>; asserts 201.</summary>
    private static async Task<ScanCreatedModel> CheckInAndScanAtAsync(
        FacilityApiFactory factory, HttpClient client, DateTimeOffset at, object? body = null)
    {
        await AttendanceApi.CheckInAsync(factory, client, at);
        var response = await client.PostAsJsonAsync("/api/scan-records", body ?? Body());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ScanCreatedModel>())!;
    }

    private static async Task<ScanRecord> StoredAsync(FacilityApiFactory factory, long id)
    {
        ScanRecord? record = null;
        await factory.WithDbAsync(async db => record = await db.ScanRecords.SingleAsync(r => r.Id == id));
        return record!;
    }

    private static async Task<int> CountAsync<T>(FacilityApiFactory factory, Func<AppDbContext, IQueryable<T>> query)
    {
        var count = 0;
        await factory.WithDbAsync(async db => count = await query(db).CountAsync());
        return count;
    }

    [Fact]
    public async Task Scanning_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("supervisor")]
    public async Task Only_cleaners_submit(string who)
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = who == "admin"
            ? await AuthApi.LoggedInAdminAsync(factory)
            : await AuthApi.LoggedInAsEmployeeAsync(factory, "S2001", "0820000001");
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

        var response = await client.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("no-such-token")]
    [InlineData("token-checkin-ar01")]
    public async Task Unknown_or_check_in_tokens_are_404(string token)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body(qrToken: token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Location_is_required()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", new { qrToken = MenRestroomToken, status = "Normal" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Scan_before_checking_in_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountAsync(factory, db => db.ScanRecords));
    }

    [Fact]
    public async Task Scan_after_shift_out_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 8, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));
        Assert.Equal(HttpStatusCode.OK, (await AttendanceApi.RecordAsync(cleaner, "ShiftOut")).StatusCode);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 20));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Scan_during_a_break_is_allowed()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 0));
        Assert.Equal(HttpStatusCode.OK, (await AttendanceApi.RecordAsync(cleaner, "BreakOut")).StatusCode);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        Assert.Equal("OnTime", created.Placement);
    }

    [Fact]
    public async Task Scan_inside_the_round_is_on_time_with_gps_and_waits_for_inspection()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        Assert.Equal("OnTime", created.Placement);
        Assert.Equal("PendingInspection", created.NewPointStatus);
        Assert.True(created.WithinRadius);
        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(auth.User.Id, stored.UserId);
        Assert.Equal(new DateOnly(2026, 10, 8), stored.ShiftDate);
        Assert.Equal(Shift.Day, stored.Shift);
        Assert.Equal(new TimeOnly(7, 0), stored.RoundStart);
        Assert.Equal(13.756300m, stored.Latitude);
        Assert.Null(stored.CoverAssignmentId);
    }

    [Fact]
    public async Task Scan_after_an_empty_round_is_late_for_that_round()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 9, 40));

        Assert.Equal("Late", created.Placement);
        Assert.Equal(40, created.LateMinutes);
    }

    [Fact]
    public async Task Second_scan_after_the_round_is_off_round()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 9, 40));

        Assert.Equal("OffRound", created.Placement);
        Assert.Null((await StoredAsync(factory, created.ScanRecordId)).RoundWindowId);
    }

    [Fact]
    public async Task Scan_after_a_rework_inspection_is_the_rework()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var first = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, first.ScanRecordId, InspectionResult.Rework, ThaiClock.At(8, 8, 30));

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 10, 0));

        Assert.Equal("Rework", created.Placement);
    }

    [Fact]
    public async Task Early_check_in_and_scan_belong_to_the_day_shift_before_its_first_round()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 6, 45));

        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(new DateOnly(2026, 10, 8), stored.ShiftDate);
        Assert.Equal(Shift.Day, stored.Shift);
        Assert.Equal("OffRound", created.Placement);
    }

    [Fact]
    public async Task Night_cleaner_after_midnight_is_late_for_the_previous_evenings_round()
    {
        using var factory = new FacilityApiFactory();
        var (night, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E1002", "0810000002");

        var created = await CheckInAndScanAtAsync(factory, night, ThaiClock.At(9, 2, 0));

        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(new DateOnly(2026, 10, 8), stored.ShiftDate);
        Assert.Equal(Shift.Night, stored.Shift);
        Assert.Equal("Late", created.Placement);
        Assert.Equal(240, created.LateMinutes);
    }

    [Fact]
    public async Task Scan_of_another_areas_point_is_blocked_and_kept_for_the_admin()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 8, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body(qrToken: MeetingRoomToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("AR02", (await response.Content.ReadFromJsonAsync<BlockedScanModel>())!.AreaCode);
        Assert.Equal(1, await CountAsync(factory, db => db.BlockedScans.Where(b => b.Reason == BlockReason.OtherArea)));
        Assert.Equal(0, await CountAsync(factory, db => db.ScanRecords));
    }

    [Fact]
    public async Task Cover_assignment_lets_the_cleaner_work_the_other_area_this_shift()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var coverId = await TestData.AddCoverAsync(factory, "E1001", "AR02", new DateOnly(2026, 10, 8), Shift.Day);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 30), Body(qrToken: MeetingRoomToken));

        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(coverId, stored.CoverAssignmentId);
        Assert.Equal("OnTime", created.Placement);
        Assert.Null(created.WithinRadius); // AR02's signs have no coordinates yet: no verdict
        Assert.Null(created.DistanceM);
    }

    [Fact]
    public async Task Cancelled_cover_no_longer_lets_the_cleaner_in()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await TestData.AddCoverAsync(factory, "E1001", "AR02", new DateOnly(2026, 10, 8), Shift.Day, cancelled: true);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 8, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body(qrToken: MeetingRoomToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Far_scan_is_saved_and_flagged()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10),
            Body(latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude));

        Assert.False(created.WithinRadius);
        Assert.True(created.DistanceM > 50);
    }

    [Fact]
    public async Task Inaccurate_fix_is_flagged_even_at_the_sign()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10), Body(accuracyM: 80));

        Assert.False(created.WithinRadius);
    }

    [Fact]
    public async Task Scanner_is_the_logged_in_user_even_if_the_body_names_someone_else()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        var adminId = 0;
        await factory.WithDbAsync(async db => adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10), new
        {
            qrToken = MenRestroomToken, userId = adminId, status = "Normal",
            latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
        });

        Assert.Equal(auth.User.Id, (await StoredAsync(factory, created.ScanRecordId)).UserId);
    }

    [Fact]
    public async Task Issue_scan_stores_its_tags_and_note()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10), new
        {
            qrToken = MenRestroomToken, status = "Issue", issueTags = new[] { "wet_floor", "plumbing_issue" }, notes = " ก๊อกรั่ว ",
            latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
        });

        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(CleaningStatus.Issue, stored.Status);
        Assert.Equal("wet_floor,plumbing_issue", stored.IssueTags);
        Assert.Equal("ก๊อกรั่ว", stored.Note);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("undefined-number")]
    public async Task Missing_or_unknown_status_is_rejected(string kind)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        object body = kind == "missing"
            ? new { qrToken = MenRestroomToken, latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10 }
            : new { qrToken = MenRestroomToken, status = 7, latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10 };

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Overlong_note_is_rejected()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", new
        {
            qrToken = MenRestroomToken, status = "Normal", notes = new string('x', 1001),
            latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(66, 66, 66, HttpStatusCode.Created)]      // 66+66+66+2 commas = 200, at the limit
    [InlineData(67, 66, 66, HttpStatusCode.BadRequest)]   // 201
    public async Task Issue_tags_length_boundary(int a, int b, int c, HttpStatusCode expected)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", new
        {
            qrToken = MenRestroomToken, status = "Issue",
            issueTags = new[] { new string('a', a), new string('b', b), new string('c', c) },
            latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
        });

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_point_is_404()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.Include(s => s.ServicePoint).SingleAsync(s => s.QrToken == MenRestroomToken);
            sign.ServicePoint!.IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_area_is_404()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.Include(s => s.ServicePoint).ThenInclude(p => p!.Area).SingleAsync(s => s.QrToken == MenRestroomToken);
            sign.ServicePoint!.Area!.IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_is_401()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == auth.User.Id)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", Body());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Only_admins_receive_the_live_update()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, cleanerAuth, _) = await AuthApi.LoggedInAsync(factory);
        var (_, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await using var adminHub = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        await using var cleanerHub = await HubClient.ConnectAsync(factory, cleanerAuth.AccessToken);
        var adminGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanerGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        adminHub.On<JsonElement>("ScanRecorded", point => adminGot.TrySetResult(point));
        cleanerHub.On<JsonElement>("ScanRecorded", point => cleanerGot.TrySetResult(point));
        // StartAsync returns after the handshake; OnConnectedAsync, which joins the admin group, runs just after it
        await Task.Delay(200);

        await CheckInAndScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        var payload = await adminGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("PendingInspection", payload.GetProperty("status").GetString());
        Assert.False(payload.TryGetProperty("qrToken", out _)); // facility-0060
        await Task.Delay(500);
        Assert.False(cleanerGot.Task.IsCompleted); // facility-0058
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~ScanRecordEndpointTests"`
Expected: compiles (once `ScanCreatedModel` deserialises the new fields as null) and several tests FAIL — e.g. `Only_cleaners_submit` gets 201/409, `Scan_before_checking_in_is_refused` gets 201, `Scan_of_another_areas_point_is_blocked…` gets 201.

- [ ] **Step 4: Write the implementation**

In `backend/src/FacilityRealtime.Infrastructure/Persistence/PointBoardQuery.cs`, replace the signature line `public static async Task<IReadOnlyList<PointBoardRow>> LoadAsync(AppDbContext db, DateTime nowUtc, int? servicePointId = null)` and the following `var slot = ShiftCalendar.SlotAt(nowUtc);` line with:

```csharp
    /// <summary>The Dashboard: every active point as seen from the shift running now.</summary>
    public static Task<IReadOnlyList<PointBoardRow>> LoadAsync(AppDbContext db, DateTime nowUtc, int? servicePointId = null) =>
        LoadAsync(db, nowUtc, ShiftCalendar.SlotAt(nowUtc), areaIds: null, servicePointId);

    /// <summary>Points as seen from <paramref name="slot"/>; <paramref name="areaIds"/> narrows to those Areas (My Work).</summary>
    public static async Task<IReadOnlyList<PointBoardRow>> LoadAsync(
        AppDbContext db, DateTime nowUtc, ShiftSlot slot, IReadOnlyCollection<int>? areaIds, int? servicePointId = null)
    {
```

and, right after the `if (servicePointId is int onlyId) { … }` block, add:

```csharp
        if (areaIds is not null)
        {
            var onlyAreas = areaIds.ToList();
            query = query.Where(p => onlyAreas.Contains(p.AreaId));
        }
```

(The rest of the method body is unchanged; it already uses `slot`.)

In `backend/src/FacilityRealtime.Api/DTOs/PointDtos.cs`, replace the two scan records with:

```csharp
/// <summary>
/// facility-0017: no UserId, the scanner is the account in the access token. "Notes" keeps the phase-1 field name.
/// Latitude, Longitude and AccuracyM are what the browser's Geolocation API reported (facility-0036/0037).
/// </summary>
public record CreateScanRecordRequest(
    string QrToken,
    CleaningStatus? Status,
    List<string>? IssueTags,
    string? Notes,
    double? Latitude,
    double? Longitude,
    double? AccuracyM);

/// <summary>WithinRadius false = Geofence Flag; null = the sign has no coordinates yet.</summary>
public record ScanRecordCreatedResponse(
    long ScanRecordId,
    int ServicePointId,
    Placement Placement,
    int? LateMinutes,
    PointStatus NewPointStatus,
    int? DistanceM,
    bool? WithinRadius,
    DateTime SubmittedAt);
```

Replace the whole of `backend/src/FacilityRealtime.Api/Endpoints/ScanRecordEndpoints.cs` with:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

public static class ScanRecordEndpoints
{
    private const int MaxIssueTagsLength = 200;
    private const int MaxNoteLength = 1000;

    public static IEndpointRouteBuilder MapScanRecordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/scan-records", CreateAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateScanRecordRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IHubContext<ScanHub> hub,
        TimeProvider clock,
        AttendanceSettings settings)
    {
        // facility-0017: the scanner is the account in the access token, never a field in the body
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        // Owner decision 2026-10-09: only Cleaners submit; Supervisors inspect in their own flow
        if (user.Role != UserRole.Cleaner)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, "การส่งงานใช้สำหรับแม่บ้านเท่านั้น");
        }

        var issueTags = request.IssueTags is { Count: > 0 } tags ? string.Join(",", tags) : null;
        var note = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (issueTags?.Length > MaxIssueTagsLength || note?.Length > MaxNoteLength)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "แท็กปัญหาหรือหมายเหตุยาวเกินไป");
        }

        if (request.Status is not { } status || !Enum.IsDefined(status))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุสถานะการทำความสะอาด");
        }

        if (GpsInput.Read(request.Latitude, request.Longitude, request.AccuracyM) is not { } gps)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, GpsInput.Required);
        }

        var sign = await db.Signs
            .Include(s => s.ServicePoint).ThenInclude(p => p!.Area)
            .FirstOrDefaultAsync(s => s.QrToken == request.QrToken && s.ServicePointId != null);
        if (sign?.ServicePoint is not { IsActive: true, Area.IsActive: true } point)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว");
        }

        var now = clock.GetUtcNow().UtcDateTime;

        // facility-0069: the Cleaner's own shift, not the shift the clock is in
        var slot = user.Shift is { } shift ? AttendanceCalendar.SlotFor(shift, now, settings) : null;

        // facility-0041: the sign's QR Token decides the Area; another Area needs a Cover Assignment for this shift
        CoverAssignment? cover = null;
        if (point.AreaId != user.AreaId)
        {
            cover = slot is null
                ? null
                : await db.CoverAssignments.FirstOrDefaultAsync(c =>
                    c.UserId == user.Id && c.AreaId == point.AreaId && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null);
            if (cover is null)
            {
                return await BlockedScans.BlockAsync(db, user, sign, point.Area!, gps, now);
            }
        }

        // facility-0026/0050: submit from Shift-In until Shift-Out (owner decision 2026-10-09: breaks do not block)
        if (slot is null)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "ตอนนี้ไม่อยู่ในกะของคุณ");
        }

        var attendance = (await AttendanceEndpoints.LoadEventsAsync(db, user.Id, slot)).Select(a => a.EventType).ToList();
        if (!AttendanceRules.IsOnDuty(attendance))
        {
            return ApiResults.Message(
                StatusCodes.Status409Conflict,
                attendance.Contains(AttendanceEvent.ShiftOut) ? "ลงเวลาเลิกงานแล้ว ส่งงานไม่ได้" : "ลงเวลาเข้างานที่ป้ายของ Area คุณก่อน");
        }

        var facts = (await RoundFactsQuery.LoadAsync(db, [point.Id], slot))[point.Id];
        var placed = RoundPlacer.Place(facts.Windows, facts.Submissions, facts.Inspections, now);

        var record = new ScanRecord
        {
            ServicePointId = point.Id,
            SignId = sign.Id,
            UserId = user.Id,
            ShiftDate = slot.ShiftDate,
            Shift = slot.Shift,
            RoundWindowId = placed.Round?.Id,
            RoundStart = placed.Round?.Start,
            RoundEnd = placed.Round?.End,
            Placement = placed.Placement,
            LateMinutes = placed.LateMinutes,
            Status = status,
            IssueTags = issueTags,
            Note = note,
            CoverAssignmentId = cover?.Id,
            SubmittedAt = now,
        };
        record.StampGps(gps, sign);
        db.ScanRecords.Add(record);
        await db.SaveChangesAsync();

        // The Cleaner sees the point in their own shift; the Dashboard shows the shift running now (they differ only
        // in the hour before or after the Cleaner's shift)
        var mine = (await PointBoardQuery.LoadAsync(db, now, slot, areaIds: null, point.Id)).Single();
        var board = slot == ShiftCalendar.SlotAt(now) ? mine : (await PointBoardQuery.LoadAsync(db, now, point.Id)).Single();
        await hub.Clients.Group(ScanHub.AdminGroup).SendAsync("ScanRecorded", PointDtoMapper.ToDto(board));

        return Results.Created(
            $"/api/scan-records/{record.Id}",
            new ScanRecordCreatedResponse(
                record.Id, point.Id, placed.Placement, placed.LateMinutes, mine.Status.Status,
                record.DistanceM, record.WithinRadius, DateTime.SpecifyKind(now, DateTimeKind.Utc)));
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~ScanRecordEndpointTests"`
Expected: PASS, 31 tests. Then `dotnet test backend/FacilityRealtime.slnx`: 0 failures (the Dashboard tests use `TestData.AddScanAsync`, which writes directly, and the original `PointBoardQuery` overload).

- [ ] **Step 6: Commit**

```bash
git add backend/src backend/tests/FacilityRealtime.ApiTests
git commit -m "feat(backend): Cleaners submit only on duty, in their own or covered Area, with GPS flagged not blocked (facility-0026, 0037, 0041)"
```

---

### Task 6: Cover Assignment admin endpoints and the audit log

**Files:**
- Create: `backend/src/FacilityRealtime.Api/Endpoints/CoverAssignmentEndpoints.cs`, `backend/src/FacilityRealtime.Api/DTOs/CoverDtos.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs` (one line)
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/CoverAssignmentEndpointTests.cs`

**Interfaces:**
- Consumes: `CurrentUser`, `ApiResults` (Task 4); `CoverAssignment`, `AuditTrail` (Task 3); `ShiftCalendar.EndUtc` (Task 1); `AuthSetup.AdminOnly`.
- Produces: `AssignCoverRequest(int UserId, int AreaId, DateOnly ShiftDate, Shift? Shift)`; `CoverAssignmentDto(int Id, int UserId, string CleanerName, int AreaId, string AreaCode, DateOnly ShiftDate, Shift Shift, DateTime AssignedAt)`.
- Produces (routes, Admin only): `POST /api/admin/cover-assignments` → 201 `CoverAssignmentDto` + `audit_log` row `COVER_ASSIGN`; 400 when the shift is missing, the user is not an active Cleaner, the Area is inactive or is the Cleaner's own, the shift is not the Cleaner's regular shift (owner decision 2026-10-09), the Area does not work that shift, or the shift already ended; 409 for a duplicate active cover. `DELETE /api/admin/cover-assignments/{id}` → 204 + `audit_log` row `COVER_CANCEL`; 404 unknown or already cancelled.

- [ ] **Step 1: Write the failing test**

Create `backend/tests/FacilityRealtime.ApiTests/Admin/CoverAssignmentEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

public record CoverModel(int Id, int UserId, string CleanerName, int AreaId, string AreaCode, DateOnly ShiftDate, string Shift, DateTime AssignedAt);

/// <summary>facility-0041 + owner decision 2026-10-09: covers are per shift, only in the covering Cleaner's own regular shift.</summary>
public class CoverAssignmentEndpointTests
{
    private const string Path = "/api/admin/cover-assignments";

    private static async Task<int> UserIdAsync(FacilityApiFactory factory, string employeeId)
    {
        var id = 0;
        await factory.WithDbAsync(async db => id = (await db.Users.SingleAsync(u => u.EmployeeId == employeeId)).Id);
        return id;
    }

    private static async Task<int> AreaIdAsync(FacilityApiFactory factory, string code)
    {
        var id = 0;
        await factory.WithDbAsync(async db => id = (await db.Areas.SingleAsync(a => a.Code == code)).Id);
        return id;
    }

    private static async Task<HttpResponseMessage> AssignAsync(
        FacilityApiFactory factory, HttpClient admin, string employeeId, string areaCode, int day, string shift) =>
        await admin.PostAsJsonAsync(Path, new
        {
            userId = await UserIdAsync(factory, employeeId),
            areaId = await AreaIdAsync(factory, areaCode),
            shiftDate = new DateOnly(2026, 10, day),
            shift,
        });

    private static async Task<List<AuditEntry>> AuditAsync(FacilityApiFactory factory)
    {
        var rows = new List<AuditEntry>();
        await factory.WithDbAsync(async db => rows = await db.AuditLog.OrderBy(a => a.Id).ToListAsync());
        return rows;
    }

    [Fact]
    public async Task Admin_assigns_a_cover_and_it_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cover = (await response.Content.ReadFromJsonAsync<CoverModel>())!;
        Assert.Equal("สมชาย ใจดี", cover.CleanerName);
        Assert.Equal("AR02", cover.AreaCode);
        var entry = Assert.Single(await AuditAsync(factory));
        Assert.Equal("COVER_ASSIGN", entry.Action);
        Assert.Equal(adminAuth.User.Id, entry.ActorId);
        Assert.Equal("cover_assignments", entry.EntityType);
        Assert.Equal(cover.Id, entry.EntityId);
    }

    [Fact]
    public async Task Only_admins_assign()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await AssignAsync(factory, cleaner, "E1001", "AR02", 8, "Day");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("E1001", "AR01", 8, "Day")]     // the Cleaner's own Area
    [InlineData("E1001", "AR02", 8, "Night")]   // not the Cleaner's regular shift
    [InlineData("S2001", "AR02", 8, "Day")]     // not a Cleaner
    [InlineData("E1002", "AR02", 8, "Night")]   // AR02 works days only
    [InlineData("E1001", "AR02", 7, "Day")]     // that shift already ended
    public async Task Invalid_covers_are_refused(string employeeId, string areaCode, int day, string shift)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AssignAsync(factory, admin, employeeId, areaCode, day, shift);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AuditAsync(factory));
    }

    [Fact]
    public async Task Duplicate_active_cover_is_a_conflict()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day");

        var again = await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Admin_cancels_a_cover_and_it_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var cover = (await (await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day")).Content.ReadFromJsonAsync<CoverModel>())!;

        var cancel = await admin.DeleteAsync($"{Path}/{cover.Id}");
        var again = await admin.DeleteAsync($"{Path}/{cover.Id}");

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        CoverAssignment? stored = null;
        await factory.WithDbAsync(async db => stored = await db.CoverAssignments.SingleAsync());
        Assert.NotNull(stored!.CancelledAt);
        Assert.Equal(adminAuth.User.Id, stored.CancelledById);
        Assert.Equal(new[] { "COVER_ASSIGN", "COVER_CANCEL" }, (await AuditAsync(factory)).Select(a => a.Action));
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~CoverAssignmentEndpointTests"`
Expected: compiles and FAILS — the route returns 404/405.

- [ ] **Step 3: Write the implementation**

Create `backend/src/FacilityRealtime.Api/DTOs/CoverDtos.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record AssignCoverRequest(int UserId, int AreaId, DateOnly ShiftDate, Shift? Shift);

public record CoverAssignmentDto(
    int Id, int UserId, string CleanerName, int AreaId, string AreaCode, DateOnly ShiftDate, Shift Shift, DateTime AssignedAt);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/CoverAssignmentEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
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
            $"มอบหมาย {cleaner.DisplayName} ทำแทน {area.Code} กะ{ShiftName(shift)} {slot.ShiftDate:yyyy-MM-dd}",
            before: null,
            after: new { cover.UserId, cover.AreaId, ShiftDate = cover.ShiftDate.ToString("yyyy-MM-dd"), Shift = shift.ToString() });
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
        cover.CancelledById = admin.Id;
        cover.CancelledAt = now;
        AuditTrail.Add(
            db, admin.Id, now, "COVER_CANCEL", "cover_assignments", cover.Id,
            $"ยกเลิกการทำแทนของ {cover.User!.DisplayName} ที่ {cover.Area!.Code} กะ{ShiftName(cover.Shift)} {cover.ShiftDate:yyyy-MM-dd}",
            before: new { cancelled = false },
            after: new { cancelled = true });
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static string ShiftName(Shift shift) => shift == Shift.Day ? "เช้า" : "ดึก";
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add `app.MapCoverAssignmentEndpoints();` after `app.MapScanRecordEndpoints();`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~CoverAssignmentEndpointTests"`
Expected: PASS, 9 tests. Then `dotnet test backend/FacilityRealtime.slnx`: 0 failures.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Api backend/tests/FacilityRealtime.ApiTests
git commit -m "feat(backend): Admin assigns and cancels per-shift Cover Assignments, logged in audit_log (facility-0041, 0051)"
```

---

### Task 7: My Work

**Files:**
- Create: `backend/src/FacilityRealtime.Api/Endpoints/MyWorkEndpoints.cs`, `backend/src/FacilityRealtime.Api/DTOs/MyWorkDtos.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs` (one line), `docs/architecture.md` (component diagram line), `README.md` (test-accounts section)
- Test: `backend/tests/FacilityRealtime.ApiTests/Points/MyWorkEndpointTests.cs`

**Interfaces:**
- Consumes: `AttendanceEndpoints.StateForAsync` (Task 4); `AttendanceCalendar` (Task 1); `CoverAssignment` (Task 3); the `PointBoardQuery` slot overload (Task 5); `PointDtoMapper.ToDto`, `PointStatusDto` (PR #50).
- Produces: `MyWorkAreaDto(int AreaId, string AreaCode, string AreaName, bool IsCover, IReadOnlyList<PointStatusDto> Points)`; `MyWorkDto(AttendanceStateDto Attendance, IReadOnlyList<MyWorkAreaDto> Areas)`.
- Produces (route): `GET /api/my-work` (Cleaners only, else 403) → the Cleaner's own Area first, then Areas covered this shift, each with its points' status as seen from the Cleaner's shift slot (or the shift running now when outside every attendance window); no QR Token.

- [ ] **Step 1: Write the failing test**

Create `backend/tests/FacilityRealtime.ApiTests/Points/MyWorkEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Attendance;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.ApiTests.Points;

public record MyWorkAreaModel(int AreaId, string AreaCode, string AreaName, bool IsCover, List<PointModel> Points);

public record MyWorkModel(AttendanceStateModel Attendance, List<MyWorkAreaModel> Areas);

/// <summary>facility-0052: a Cleaner sees every point of their own Area and of Areas they cover this shift.</summary>
public class MyWorkEndpointTests
{
    [Fact]
    public async Task My_work_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().GetAsync("/api/my-work");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cleaner_sees_the_points_of_the_own_area()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var work = (await cleaner.GetFromJsonAsync<MyWorkModel>("/api/my-work"))!;

        Assert.Equal(new[] { "BreakOut", "ShiftOut" }, work.Attendance.NextEvents);
        var area = Assert.Single(work.Areas);
        Assert.Equal("AR01", area.AreaCode);
        Assert.False(area.IsCover);
        Assert.Equal(new[] { "ห้องน้ำชาย ชั้น 1", "ห้องน้ำหญิง ชั้น 1" }, area.Points.Select(p => p.Name));
        Assert.All(area.Points, p => Assert.Equal("NotYetDone", p.Status));
    }

    [Fact]
    public async Task Covered_area_follows_the_own_area()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await TestData.AddCoverAsync(factory, "E1001", "AR02", new DateOnly(2026, 10, 8), Shift.Day);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var work = (await cleaner.GetFromJsonAsync<MyWorkModel>("/api/my-work"))!;

        Assert.Equal(new[] { "AR01", "AR02" }, work.Areas.Select(a => a.AreaCode));
        Assert.Equal(new[] { false, true }, work.Areas.Select(a => a.IsCover));
        Assert.Equal("ห้องประชุม", Assert.Single(work.Areas[1].Points).Name);
    }

    [Fact]
    public async Task My_work_carries_no_qr_tokens()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var body = await cleaner.GetStringAsync("/api/my-work");

        Assert.DoesNotContain("token-", body); // facility-0059
    }

    [Fact]
    public async Task Only_cleaners_have_my_work()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.GetAsync("/api/my-work");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

(`PointModel` is the record declared in `ServicePointEndpointTests.cs`, same namespace.)

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~MyWorkEndpointTests"`
Expected: compiles and FAILS — `/api/my-work` returns 404.

- [ ] **Step 3: Write the implementation**

Create `backend/src/FacilityRealtime.Api/DTOs/MyWorkDtos.cs`:

```csharp
namespace FacilityRealtime.Api.DTOs;

public record MyWorkAreaDto(int AreaId, string AreaCode, string AreaName, bool IsCover, IReadOnlyList<PointStatusDto> Points);

/// <summary>facility-0052: the Cleaner's page — attendance buttons, then the own Area, then covered Areas.</summary>
public record MyWorkDto(AttendanceStateDto Attendance, IReadOnlyList<MyWorkAreaDto> Areas);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/MyWorkEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

public static class MyWorkEndpoints
{
    public static IEndpointRouteBuilder MapMyWorkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/my-work", GetAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.Role != UserRole.Cleaner)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, "หน้างานของฉันใช้สำหรับแม่บ้านเท่านั้น");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var slot = (user.Shift is { } shift ? AttendanceCalendar.SlotFor(shift, now, settings) : null) ?? ShiftCalendar.SlotAt(now);

        var coveredAreaIds = await db.CoverAssignments
            .Where(c => c.UserId == user.Id && c.ShiftDate == slot.ShiftDate && c.Shift == slot.Shift && c.CancelledAt == null)
            .Select(c => c.AreaId)
            .ToListAsync();
        var areaIds = (user.AreaId is int own ? coveredAreaIds.Prepend(own) : coveredAreaIds).Distinct().ToList();

        var rows = await PointBoardQuery.LoadAsync(db, now, slot, areaIds);
        var areas = areaIds
            .Select(id => rows.Where(r => r.Point.AreaId == id).ToList())
            .Where(points => points.Count > 0)
            .Select(points =>
            {
                var area = points[0].Point.Area!;
                return new MyWorkAreaDto(area.Id, area.Code, area.Name, area.Id != user.AreaId, points.Select(PointDtoMapper.ToDto).ToList());
            })
            .ToList();

        return Results.Ok(new MyWorkDto(await AttendanceEndpoints.StateForAsync(db, user, now, settings), areas));
    }
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add `app.MapMyWorkEndpoints();` after `app.MapCoverAssignmentEndpoints();`.

In `docs/architecture.md`, in the component diagram replace the line

```
            Ep["Endpoints/ServicePointEndpoints, ScanRecordEndpoints, MeEndpoints<br/>/api/service-points (Admin) | /api/scan-records | /api/me"]
```

with

```
            Ep["Endpoints/*<br/>/api/service-points (Admin) | /api/scan-records | /api/attendance | /api/my-work<br/>/api/admin/cover-assignments | /api/me"]
```

(If the line differs slightly on the branch, replace whichever line lists the endpoint files in the `ApiP` subgraph.)

In `README.md`, at the end of the "บัญชีผู้ใช้ทดสอบ" section, add:

```markdown
ลองใน Swagger (`/swagger`): แม่บ้านต้องลงเวลาเข้างานก่อนส่งงาน — `POST /api/attendance` ด้วย `qrToken` = `token-checkin-ar01`, `eventType` = `ShiftIn` และพิกัด เช่น `latitude` 13.7563, `longitude` 100.5018, `accuracyM` 10 (ป้ายของ AR01 มีพิกัดตัวอย่าง ป้ายของ AR02 ยังไม่มี) จากนั้นจึง `POST /api/scan-records` ได้ ลงเวลาได้ตั้งแต่ 60 นาทีก่อนกะถึง 60 นาทีหลังกะ (ADR 0069)
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~MyWorkEndpointTests"`
Expected: PASS, 5 tests. Then `dotnet test backend/FacilityRealtime.slnx`: 0 failures.

- [ ] **Step 5: Commit**

```bash
git add backend/src backend/tests docs/architecture.md README.md
git commit -m "feat(backend): My Work lists the Cleaner's own and covered Areas for their shift (facility-0052)"
```

---

## After the last task

- `dotnet test backend/FacilityRealtime.slnx` passes with 0 failures; the MySQL test passes with `FACILITY_MYSQL_TEST` set to a throwaway database.
- Run the API in Development against a throwaway MySQL database and try in Swagger: check in as E1001 at `token-checkin-ar01`, submit at `token-restroom-m1`, try `token-meeting-room` (blocked), assign a cover as `admin`, submit again (accepted, no verdict), open `/api/my-work`.
- Next plans: Supervisor inspection (inspections GPS, building/shift block reasons, Supervisor covers), Admin pages (sign location capture, attendance correction, alerts list with "assign cover" from a Blocked Scan, round-window editing with the 19:00/07:00 rule), then the frontend.
