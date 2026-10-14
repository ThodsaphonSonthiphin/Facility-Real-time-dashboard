# Admin Setup — Areas, Points, Round Windows and Sign Locations (Backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the Admin set up the places the pilot runs on without touching the seed or SQL: list, create and edit Areas (building, shift pattern, the one Cleaner per shift), deactivate and reactivate them, add and edit Service Points with their Round Windows (refusing windows that overlap, leave the shift, or end at 19:00/07:00), capture each sign's location and radius, and issue a new QR Token. Every change writes the audit log.

**Architecture:** Two pure helpers in `FacilityRealtime.Application`: `RoundWindowRules` (parse "HH:mm", validate one point's windows, ADR 0047 + its 2026-10-09 amendment) and `SignCodes` (Area code format, sign codes `AR01-IN` / `AR01-03`, random QR Tokens, ADR 0006). They are unit-tested. Two Infrastructure helpers handle the database-sensitive steps: `AreaCleaners` saves twice so the UNIQUE `cleaner_slot` never sees two Cleaners at once, and `RoundWindowRemoval` unlinks Scan Records before deleting a window. Three new endpoint files under `/api/admin` (`AdminAreaEndpoints`, `AdminPointEndpoints`, `AdminSignEndpoints`) share one read model, `AdminSetupView`. The Dashboard query starts hiding points of deactivated Areas. No schema change: every column already exists after migration `ScanRules`.

**Tech Stack:** .NET 10 Minimal API, EF Core 10 (`MySql.EntityFrameworkCore` 10.0.9 at runtime, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 in tests), xUnit 2.9.3.

**Spec:**
- `docs/adr/facility-0045-admin-manages-areas-and-captures-sign-location.md`: the Admin creates and edits Areas, points, Round Windows, sign locations and radius. A check-in sign is created with the Area. The location is captured at the sign, and a map pick is marked "ยังไม่ยืนยันหน้างาน".
- `docs/adr/facility-0047-cleaning-rounds-are-time-windows.md`, rule 1 and the **2026-10-09 amendment**: windows may not overlap and must lie inside the shift. A window may not end at 19:00 in the Day shift or at 07:00 in the Night shift.
- `docs/adr/facility-0021-points-admin-page.md`: rules 4–6 still apply. Deactivate instead of delete. No new QR on a deactivated point. Reprint after a new QR.
- `docs/adr/facility-0006-mysql-schema-and-qr-token.md`: the QR Token is a random UUID v4, and a new one makes the old sign stop working.
- `docs/adr/facility-0038-default-radius-50m-per-sign.md`: radius defaults to 50 m and is editable per sign.
- `docs/adr/facility-0040-area-is-the-unit-of-assignment.md`: one Cleaner per shift per Area, and a day-only Area has no night shift.
- `docs/adr/facility-0051-admin-corrections-and-audit-log.md`: every Admin change to an Area, point, Round Window, sign location, radius or QR is logged.
- `docs/adr/facility-0057-supervisor-building-page-and-audit-log-page.md`: the log never holds a phone number or password.
- `docs/adr/facility-0060-admin-gets-qr-token-from-points-page-only.md`: the Admin's points/print pages carry QR Tokens, and the Dashboard does not.
- Wireframes in `docs/design/wireframe/`: `AreaEdit.dc.html` (D10), `PointsAdmin.dc.html`, `CaptureLocation.dc.html` (M17), `PrintSigns.dc.html`.
- `docs/design/database.html`: tables `areas`, `service_points`, `signs`, `point_round_windows`, `audit_log`, and the rule "แอปตรวจ: ห้ามซ้อนกัน และต้องอยู่ในกะ".

## Global Constraints

- **Branch and merging:** work on branch `feat/admin-setup`, created from `master` at `598747a`. Do not merge it.
- **No migration.** Every column this plan needs already exists. If a task seems to need a schema change, stop and report instead of adding a migration.
- **Endpoint security:** every endpoint is under `/api/admin`, uses `RequireAuthorization(AuthSetup.AdminOnly)` (401 without login, 403 for Cleaner and Supervisor), and loads the actor with `CurrentUser.LoadAsync`.
- **Audit log:** every change writes one `audit_log` row in the same save or the same explicit transaction as the change (ADR 0051).
  - Saving something unchanged writes nothing.
  - Pass anonymous objects or records as `before`/`after`, never entities.
  - Never put a QR Token, phone number or password in the log.
- **Deactivate, never delete:** nothing is deleted except Round Windows. When a window is removed, its Scan Records keep their copied `round_start`/`round_end` and their `round_window_id` becomes NULL (database.html).
- **Error format:** errors answer with `ApiResults.Message(status, "<Thai text>")`, the JSON `{ "message": ... }` the phone and the Admin pages show as is.
- **Round Windows:**
  - They are Thai wall-clock, sent and returned as `"HH:mm"` strings, for example `"07:00"`.
  - Day shift: 07:00–19:00. Night shift: 19:00–07:00. Windows are measured from the shift start, so a night window may cross midnight.
  - A window must start inside its shift, end after its start, and end **before** the shift ends (ADR 0047 amendment).
  - Windows of one shift may touch (09:00 end, 09:00 start) but not overlap.
  - A `DayOnly` Area has no night windows.
- **Codes and tokens:**
  - The Area code is 2–20 characters A–Z or 0–9, upper-cased, and **cannot be changed** after creation because it is printed on every sign of the Area.
  - Sign codes are `{AreaCode}-IN` for the check-in sign and `{AreaCode}-{NN}` for points. The number is one past the highest ever used in that Area and is never reused.
  - QR Tokens are `Guid.NewGuid().ToString()`.
- **Runtime provider:** it is Oracle's `MySql.EntityFrameworkCore`, and SQLite tests cannot catch provider-only failures.
  - Task 3 adds a `[MySqlFact]` that runs when `FACILITY_MYSQL_TEST` is set.
  - MySQL 8.0 runs on this laptop as service `MySQL80`. Only use throwaway databases (`facility_*_test`), never `facility_dashboard`.
  - Get the root password from the developer and never write it into any committed file, this plan included.
- **Earlier rules stay:** keep every rule from PR #50 and #51 (auth, throttle, Admin-only Dashboard without QR Tokens, scan and attendance rules).

### Decisions made while planning (report them to the owner in the PR)

1. **Cleaners are assigned on the Area form**, as wireframe D10 shows.
   - A Cleaner can be put on an Area only when their regular shift matches the slot and they have no other Area (409 otherwise). The wireframe lists "only Cleaners without an Area in that shift".
   - Leaving a slot empty takes that Cleaner off the Area (`area_id` becomes NULL).
2. **Switching an Area to "Day only" deletes its points' night windows** and takes its night Cleaner off. Wireframe D10: "ช่องกะดึกและเวลารอบกะดึกหายไป". The audit row records how many windows went.
3. **Editing a point's windows keeps every window that is unchanged** (same shift, start and end), so the current round keeps its Scan Records. A changed window is a delete plus an add.
   - Editing the window of the round that is running right now restarts that point's card.
   - The Admin pages should advise editing between rounds. The frontend plan shows that hint.
4. **A map pick cannot overwrite a location captured at the sign** (409). It is the fallback for "not yet on site" (ADR 0045 rule 3), not a correction.
5. **Radius is 10–500 m.**
   - Below 10 m is finer than a phone's indoor fix, so every scan would be flagged. Above 500 m covers several buildings.
   - A new radius applies to later scans; stored verdicts stay as they were.
6. **No new QR for a sign whose point or Area is deactivated** (409), per ADR 0021 rule 4.
7. **Deactivating an Area hides its points from the Dashboard** and from the scan page's `by-token`, as wireframe D10 says. Scans and attendance were already refused. Cleaners stay assigned, so reactivating restores everything.
8. **`AuditTrail.Add` throws when given an entity** (a `FacilityRealtime.Domain` type) instead of a snapshot. This was deferred from plan 2, and a `User` carries a phone hash.

### Out of scope (later plans)

- **Plan 4, Admin attendance and alerts:** attendance board and corrections (D2, ADR 0051), the alert list with "assign cover" from a Blocked Scan (D3, `alert_reviews`), and the audit log page (D9).
- **Plan 5, Admin accounts and Excel export:** accounts page (D6, ADR 0020/0054) and Excel export (D8, ADR 0053).
- **Later:** buildings CRUD (seed only for the pilot), point re-ordering, SignalR push of setup changes (the Dashboard polls every 30 s, ADR 0061), the Supervisor inspection plan, and the frontend.

---

## File Structure

```
backend/
  src/FacilityRealtime.Application/
    Rounds/RoundWindowRules.cs         RoundWindowInput; parse "HH:mm"; validate one point's windows   (new)
    Signs/SignCodes.cs                 Area code format, sign codes, QR Tokens                          (new)
  src/FacilityRealtime.Infrastructure/
    Persistence/AreaCleaners.cs        puts Cleaners on an Area in two saves                            (new)
    Persistence/RoundWindowRemoval.cs  unlinks Scan Records, then deletes windows                       (new)
    Persistence/AuditTrail.cs          refuses entities as snapshots                                    (modified)
    Persistence/PointBoardQuery.cs     hides points of deactivated Areas                                (modified)
  src/FacilityRealtime.Api/
    DTOs/AdminSetupDtos.cs             requests and responses of the setup pages                        (new)
    Endpoints/AdminSetupView.cs        read model: Area list, Area detail, point, sign                  (new)
    Endpoints/AdminAreaEndpoints.cs    /api/admin/buildings, /cleaners, /areas                          (new)
    Endpoints/AdminPointEndpoints.cs   /api/admin/areas/{id}/points, /api/admin/points                  (new)
    Endpoints/AdminSignEndpoints.cs    /api/admin/signs/{id}/location, /radius, /regenerate-token       (new)
    Program.cs                         maps the three endpoint files                                    (modified)
  tests/FacilityRealtime.UnitTests/    RoundWindowRulesTests, SignCodesTests                            (new)
  tests/FacilityRealtime.ApiTests/
    Infrastructure/TestData.cs         + AddCleanerAsync                                                (modified)
    Admin/AdminSetupModels.cs, Admin/AdminSetupApi.cs                                                   (new)
    Admin/AreaAdminEndpointTests.cs, Admin/PointAdminEndpointTests.cs, Admin/SignAdminEndpointTests.cs  (new)
    Persistence/AuditTrailTests.cs, Persistence/MySqlAreaCleanerTests.cs                                (new)
docs/architecture.md, README.md                                                                         (modified, Task 5)
```

Run every command from the repository root. `dotnet test backend/FacilityRealtime.slnx` runs everything. If an API from this repo is running (for example for Swagger on port 5001), stop it before building, because Windows locks its DLLs.

**Endpoint map** (every route needs an Admin):

| Method and path | Task | Does |
|---|---|---|
| `GET /api/admin/buildings` | 2 | Active buildings, for the Area form |
| `GET /api/admin/cleaners` | 2 | Active Cleaners with shift and current Area, for the Cleaner pickers |
| `GET /api/admin/areas` | 2 | Every Area, active or not, with Cleaners and counts |
| `GET /api/admin/areas/{id}` | 2 | One Area: check-in sign, every point with its sign (QR Token, location, radius) and windows |
| `POST /api/admin/areas` | 3 | New Area and its check-in sign |
| `PUT /api/admin/areas/{id}` | 3 | Name, building, shift pattern, Cleaners |
| `POST /api/admin/areas/{id}/deactivate`, `/activate` | 3 | Turn an Area off or on |
| `POST /api/admin/areas/{areaId}/points` | 4 | New point, its sign and windows |
| `PUT /api/admin/points/{id}` | 4 | Name and windows |
| `POST /api/admin/points/{id}/deactivate`, `/activate` | 4 | Turn a point off or on |
| `PUT /api/admin/signs/{id}/location` | 5 | Location captured at the sign (`Site`) or picked on a map (`Map`) |
| `PUT /api/admin/signs/{id}/radius` | 5 | Radius in metres |
| `POST /api/admin/signs/{id}/regenerate-token` | 5 | New QR Token; the printed sign stops working |

---

### Task 1: Round Window rules and sign codes

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Rounds/RoundWindowRules.cs`
- Create: `backend/src/FacilityRealtime.Application/Signs/SignCodes.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/RoundWindowRulesTests.cs`, `backend/tests/FacilityRealtime.UnitTests/SignCodesTests.cs`

**Interfaces:**
- Consumes: `ShiftCalendar.DayStart` (07:00), `ShiftCalendar.NightStart` (19:00), `enum Shift { Day, Night }`, `enum ShiftPattern { DayAndNight, DayOnly }`.
- Produces (namespace `FacilityRealtime.Application.Rounds`):
  - `sealed record RoundWindowInput(Shift Shift, TimeOnly Start, TimeOnly End)`
  - `static class RoundWindowRules`:
    - `bool TryParseTime(string? text, out TimeOnly time)`
    - `string Format(TimeOnly time)`, which returns `"07:00"`
    - `int MinutesIntoShift(Shift shift, TimeOnly time)`, from 0 to 1439
    - `string Describe(RoundWindowInput window)`, which returns `"DAY 07:00-09:00"` for the audit log
    - `string? Validate(IReadOnlyList<RoundWindowInput> windows, ShiftPattern pattern)`, which returns null when valid, otherwise a Thai message
- Produces (namespace `FacilityRealtime.Application.Signs`) `static class SignCodes`:
  - `string? NormalizeAreaCode(string? code)`
  - `string CheckIn(string areaCode)`
  - `string NextPointCode(string areaCode, IEnumerable<string> existingPointCodes)`
  - `string NewQrToken()`

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.UnitTests/RoundWindowRulesTests.cs`:

```csharp
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0047 rule 1 and its 2026-10-09 amendment: what the Admin may save as a point's Round Windows.</summary>
public class RoundWindowRulesTests
{
    private static RoundWindowInput W(Shift shift, string start, string end)
    {
        Assert.True(RoundWindowRules.TryParseTime(start, out var s));
        Assert.True(RoundWindowRules.TryParseTime(end, out var e));
        return new RoundWindowInput(shift, s, e);
    }

    [Fact]
    public void The_seeded_restroom_rounds_are_valid()
    {
        var windows = new[]
        {
            W(Shift.Day, "07:00", "09:00"), W(Shift.Day, "16:00", "18:00"),
            W(Shift.Night, "20:00", "22:00"), W(Shift.Night, "03:00", "05:00"),
        };

        Assert.Null(RoundWindowRules.Validate(windows, ShiftPattern.DayAndNight));
    }

    [Fact]
    public void A_night_window_may_cross_midnight()
    {
        Assert.Null(RoundWindowRules.Validate([W(Shift.Night, "23:00", "01:00")], ShiftPattern.DayAndNight));
    }

    [Fact]
    public void Touching_windows_do_not_overlap()
    {
        Assert.Null(RoundWindowRules.Validate(
            [W(Shift.Day, "07:00", "09:00"), W(Shift.Day, "09:00", "11:00")], ShiftPattern.DayAndNight));
    }

    [Fact]
    public void No_windows_is_valid()
    {
        Assert.Null(RoundWindowRules.Validate([], ShiftPattern.DayOnly));
    }

    [Theory]
    [InlineData(Shift.Day, "18:00", "19:00", "เวลาเปลี่ยนกะ")]   // amendment 2026-10-09
    [InlineData(Shift.Night, "05:00", "07:00", "เวลาเปลี่ยนกะ")]
    [InlineData(Shift.Day, "06:00", "08:00", "ต้องอยู่ในกะเช้า")]
    [InlineData(Shift.Day, "17:00", "20:00", "ต้องอยู่ในกะเช้า")]
    [InlineData(Shift.Night, "18:00", "20:00", "ต้องอยู่ในกะดึก")]
    [InlineData(Shift.Night, "06:00", "08:00", "ต้องอยู่ในกะดึก")]
    [InlineData(Shift.Day, "10:00", "09:00", "หลังเวลาเริ่ม")]
    [InlineData(Shift.Day, "10:00", "10:00", "หลังเวลาเริ่ม")]
    [InlineData(Shift.Night, "02:00", "23:00", "หลังเวลาเริ่ม")]
    public void A_window_outside_its_shift_or_ending_at_the_shift_change_is_refused(Shift shift, string start, string end, string expected)
    {
        var error = RoundWindowRules.Validate([W(shift, start, end)], ShiftPattern.DayAndNight);

        Assert.NotNull(error);
        Assert.Contains(expected, error);
    }

    [Fact]
    public void A_day_only_area_has_no_night_windows()
    {
        var error = RoundWindowRules.Validate([W(Shift.Night, "20:00", "22:00")], ShiftPattern.DayOnly);

        Assert.NotNull(error);
        Assert.Contains("เฉพาะกะเช้า", error);
    }

    [Theory]
    [InlineData(Shift.Day, "07:00", "09:00", "08:00", "10:00")]
    [InlineData(Shift.Night, "23:00", "01:00", "00:30", "02:00")]
    [InlineData(Shift.Day, "07:00", "09:00", "07:00", "09:00")]
    public void Overlapping_windows_are_refused(Shift shift, string start1, string end1, string start2, string end2)
    {
        var error = RoundWindowRules.Validate([W(shift, start1, end1), W(shift, start2, end2)], ShiftPattern.DayAndNight);

        Assert.NotNull(error);
        Assert.Contains("ซ้อนกัน", error);
    }

    [Theory]
    [InlineData("07:00", 7, 0)]
    [InlineData("23:59", 23, 59)]
    [InlineData("00:00", 0, 0)]
    public void Times_are_read_as_two_digit_hours_and_minutes(string text, int hour, int minute)
    {
        Assert.True(RoundWindowRules.TryParseTime(text, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
        Assert.Equal(text, RoundWindowRules.Format(time));
    }

    [Theory]
    [InlineData("7:00")]
    [InlineData("07:00:00")]
    [InlineData("24:00")]
    [InlineData("ab")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_time_formats_are_refused(string? text)
    {
        Assert.False(RoundWindowRules.TryParseTime(text, out _));
    }
}
```

Create `backend/tests/FacilityRealtime.UnitTests/SignCodesTests.cs`:

```csharp
using FacilityRealtime.Application.Signs;

namespace FacilityRealtime.UnitTests;

/// <summary>The codes printed on the signs (facility-0040, 0045) and their QR Tokens (facility-0006).</summary>
public class SignCodesTests
{
    [Theory]
    [InlineData(" ar03 ", "AR03")]
    [InlineData("B12", "B12")]
    [InlineData("ABCDEFGHIJ0123456789", "ABCDEFGHIJ0123456789")]
    public void Area_codes_are_trimmed_and_upper_cased(string input, string expected)
    {
        Assert.Equal(expected, SignCodes.NormalizeAreaCode(input));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AR-03")]
    [InlineData("อาคาร1")]
    [InlineData("ABCDEFGHIJ01234567890")]
    [InlineData("")]
    [InlineData(null)]
    public void Area_codes_are_two_to_twenty_letters_or_digits(string? input)
    {
        Assert.Null(SignCodes.NormalizeAreaCode(input));
    }

    [Fact]
    public void The_check_in_sign_ends_in_IN()
    {
        Assert.Equal("AR01-IN", SignCodes.CheckIn("AR01"));
    }

    [Theory]
    [InlineData("", "AR01-01")]
    [InlineData("AR01-01,AR01-02", "AR01-03")]
    [InlineData("AR01-09,AR01-03", "AR01-10")]
    [InlineData("AR01-99", "AR01-100")]
    [InlineData("AR011-05,AR01-IN", "AR01-01")]
    public void A_new_point_takes_one_past_the_highest_number_ever_used(string existing, string expected)
    {
        var codes = existing.Split(',', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(expected, SignCodes.NextPointCode("AR01", codes));
    }

    [Fact]
    public void Qr_tokens_are_random_uuids()
    {
        var first = SignCodes.NewQrToken();
        var second = SignCodes.NewQrToken();

        Assert.True(Guid.TryParse(first, out _));
        Assert.NotEqual(first, second);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~RoundWindowRulesTests|FullyQualifiedName~SignCodesTests"`
Expected: build FAILS because `RoundWindowRules`, `RoundWindowInput` and `FacilityRealtime.Application.Signs` do not exist.

- [ ] **Step 3: Write `RoundWindowRules`**

Create `backend/src/FacilityRealtime.Application/Rounds/RoundWindowRules.cs`:

```csharp
using System.Globalization;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Rounds;

/// <summary>A Round Window as the Admin typed it, already parsed. Thai wall-clock.</summary>
public sealed record RoundWindowInput(Shift Shift, TimeOnly Start, TimeOnly End);

/// <summary>
/// ADR facility-0047 rule 1 and its 2026-10-09 amendment: what the Admin may save as a point's Round Windows.
/// Times are measured from the shift's start, so a night window may cross midnight; a window must end before the
/// shift does, because a late or rework submission after the shift change already belongs to the next shift.
/// </summary>
public static class RoundWindowRules
{
    private const int ShiftMinutes = 12 * 60;
    private const int DayMinutes = 24 * 60;

    /// <summary>"07:00" only: two-digit 24-hour time, whole minutes.</summary>
    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Minutes from the shift's start (07:00 or 19:00) to <paramref name="time"/>, wrapping past midnight: 0–1439.</summary>
    public static int MinutesIntoShift(Shift shift, TimeOnly time)
    {
        var shiftStart = shift == Shift.Day ? ShiftCalendar.DayStart : ShiftCalendar.NightStart;
        var minutes = time.Hour * 60 + time.Minute - (shiftStart.Hour * 60 + shiftStart.Minute);
        return (minutes + DayMinutes) % DayMinutes;
    }

    /// <summary>How the audit log shows a window, e.g. "DAY 07:00-09:00".</summary>
    public static string Describe(RoundWindowInput window) =>
        $"{(window.Shift == Shift.Day ? "DAY" : "NIGHT")} {Format(window.Start)}-{Format(window.End)}";

    /// <summary>Null when every window may be saved; otherwise the Thai message for the first problem found.</summary>
    public static string? Validate(IReadOnlyList<RoundWindowInput> windows, ShiftPattern pattern)
    {
        foreach (var window in windows)
        {
            if (window.Shift == Shift.Night && pattern == ShiftPattern.DayOnly)
            {
                return "Area นี้ทำเฉพาะกะเช้า ใส่ช่วงรอบกะดึกไม่ได้";
            }

            var start = MinutesIntoShift(window.Shift, window.Start);
            var end = MinutesIntoShift(window.Shift, window.End);
            if (start >= ShiftMinutes || end > ShiftMinutes)
            {
                return $"ช่วง {Text(window)} ต้องอยู่ในกะ{ShiftText(window.Shift)}";
            }

            if (end == ShiftMinutes)
            {
                return $"ช่วง {Text(window)} จบตรง {Format(window.End)} ซึ่งเป็นเวลาเปลี่ยนกะไม่ได้ ให้จบก่อนเวลานั้น";
            }

            if (end <= start)
            {
                return $"ช่วง {Text(window)}: เวลาสิ้นสุดต้องหลังเวลาเริ่ม";
            }
        }

        foreach (var shift in windows.GroupBy(w => w.Shift))
        {
            var ordered = shift.OrderBy(w => MinutesIntoShift(w.Shift, w.Start)).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (MinutesIntoShift(ordered[i].Shift, ordered[i].Start) < MinutesIntoShift(ordered[i - 1].Shift, ordered[i - 1].End))
                {
                    return $"ช่วง {Text(ordered[i - 1])} กับ {Text(ordered[i])} ซ้อนกัน";
                }
            }
        }

        return null;
    }

    private static string Text(RoundWindowInput window) => $"{Format(window.Start)}–{Format(window.End)}";

    private static string ShiftText(Shift shift) => shift == Shift.Day ? "เช้า (07:00–19:00)" : "ดึก (19:00–07:00)";
}
```

- [ ] **Step 4: Write `SignCodes`**

Create `backend/src/FacilityRealtime.Application/Signs/SignCodes.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace FacilityRealtime.Application.Signs;

/// <summary>The codes printed on the signs, e.g. AR01-IN and AR01-03 (facility-0040, 0045), and their QR Tokens (facility-0006).</summary>
public static partial class SignCodes
{
    /// <summary>Trimmed and upper-cased; null unless it is 2–20 letters A–Z or digits. It prefixes every sign code of the Area.</summary>
    public static string? NormalizeAreaCode(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        return normalized is not null && AreaCodePattern().IsMatch(normalized) ? normalized : null;
    }

    public static string CheckIn(string areaCode) => $"{areaCode}-IN";

    /// <summary>One past the highest number already used in the Area, so a deactivated point's number is never reused.</summary>
    public static string NextPointCode(string areaCode, IEnumerable<string> existingPointCodes)
    {
        var prefix = areaCode + "-";
        var highest = existingPointCodes
            .Where(code => code.StartsWith(prefix, StringComparison.Ordinal))
            .Select(code => int.TryParse(code[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{highest + 1:00}";
    }

    /// <summary>facility-0006: a random UUID v4. A new one makes the printed sign stop working.</summary>
    public static string NewQrToken() => Guid.NewGuid().ToString();

    [GeneratedRegex("^[A-Z0-9]{2,20}$")]
    private static partial Regex AreaCodePattern();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~RoundWindowRulesTests|FullyQualifiedName~SignCodesTests"`
Expected: PASS, 42 tests (26 + 16). Then `dotnet test backend/FacilityRealtime.slnx`: 0 failures.

- [ ] **Step 6: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Rounds/RoundWindowRules.cs backend/src/FacilityRealtime.Application/Signs/SignCodes.cs backend/tests/FacilityRealtime.UnitTests/RoundWindowRulesTests.cs backend/tests/FacilityRealtime.UnitTests/SignCodesTests.cs
git commit -m "feat(backend): round-window rules with the shift-change rule, sign codes and QR tokens (facility-0047, 0045, 0006)"
```

---

### Task 2: Admin read endpoints — buildings, Cleaners, Areas

**Files:**
- Create: `backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminSetupView.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs` (map the endpoints)
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AdminSetupModels.cs`, `Admin/AdminSetupApi.cs`, `Admin/AreaAdminEndpointTests.cs`

**Interfaces:**
- Consumes (Task 1): `RoundWindowRules.Format`, `RoundWindowRules.MinutesIntoShift`.
- Produces (namespace `FacilityRealtime.Api.DTOs`):
  - `BuildingDto(int Id, string Code, string Name)`
  - `CleanerOptionDto(int Id, string EmployeeId, string DisplayName, Shift? Shift, int? AreaId, string? AreaCode)`
  - `AssignedCleanerDto(int Id, string EmployeeId, string DisplayName)`
  - `AreaSummaryDto(int Id, string Code, string Name, int BuildingId, string BuildingCode, ShiftPattern ShiftPattern, bool IsActive, AssignedCleanerDto? DayCleaner, AssignedCleanerDto? NightCleaner, int ActivePointCount, int SignsNotConfirmedOnSite)`
  - `AdminSignDto(int Id, string Code, string QrToken, DateTime QrIssuedAt, decimal? Latitude, decimal? Longitude, short? LocationAccuracyM, LocationSource? LocationSource, DateTime? LocatedAt, short RadiusM)`
  - `AdminRoundWindowDto(int Id, Shift Shift, string Start, string End)`
  - `AdminPointDto(int Id, string Name, short SortOrder, bool IsActive, AdminSignDto Sign, IReadOnlyList<AdminRoundWindowDto> RoundWindows)`
  - `AreaDetailDto(AreaSummaryDto Area, AdminSignDto CheckInSign, IReadOnlyList<AdminPointDto> Points)`
- Produces (namespace `FacilityRealtime.Api.Endpoints`), `internal static class AdminSetupView`:
  - `Task<List<AreaSummaryDto>> LoadAreasAsync(AppDbContext db, int? onlyAreaId = null)`
  - `Task<AreaDetailDto?> LoadAreaAsync(AppDbContext db, int areaId)`
  - `AdminSignDto ToDto(Sign sign)`
  - `AdminPointDto ToDto(ServicePoint point, Sign sign, IEnumerable<PointRoundWindow> windows)`
- Produces: `public static class AdminAreaEndpoints { IEndpointRouteBuilder MapAdminAreaEndpoints(this IEndpointRouteBuilder app); }`, which later tasks add routes to.
- Produces (tests, namespace `FacilityRealtime.ApiTests.Admin`):
  - the models in `AdminSetupModels.cs`
  - `AdminSetupApi` with `AreaIdAsync`, `BuildingIdAsync`, `SignIdAsync`, `PointIdAsync`, `UserIdAsync`, `AuditAsync`, `ReadAsync<T>`, `MessageAsync`, `DashboardNamesAsync`

- [ ] **Step 1: Write the test models and helpers**

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AdminSetupModels.cs`:

```csharp
namespace FacilityRealtime.ApiTests.Admin;

public record MessageModel(string Message);

public record BuildingModel(int Id, string Code, string Name);

public record CleanerOptionModel(int Id, string EmployeeId, string DisplayName, string? Shift, int? AreaId, string? AreaCode);

public record AssignedCleanerModel(int Id, string EmployeeId, string DisplayName);

public record AreaSummaryModel(
    int Id, string Code, string Name, int BuildingId, string BuildingCode, string ShiftPattern, bool IsActive,
    AssignedCleanerModel? DayCleaner, AssignedCleanerModel? NightCleaner, int ActivePointCount, int SignsNotConfirmedOnSite);

public record AdminSignModel(
    int Id, string Code, string QrToken, DateTime QrIssuedAt, decimal? Latitude, decimal? Longitude,
    short? LocationAccuracyM, string? LocationSource, DateTime? LocatedAt, short RadiusM);

public record AdminRoundWindowModel(int Id, string Shift, string Start, string End);

public record AdminPointModel(int Id, string Name, short SortOrder, bool IsActive, AdminSignModel Sign, List<AdminRoundWindowModel> RoundWindows);

public record AreaDetailModel(AreaSummaryModel Area, AdminSignModel CheckInSign, List<AdminPointModel> Points);
```

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AdminSetupApi.cs`:

```csharp
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Points;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Finds seeded rows by the codes printed on signs, and reads back what the admin endpoints wrote.</summary>
public static class AdminSetupApi
{
    public static async Task<T> ReadAsync<T>(FacilityApiFactory factory, Func<AppDbContext, Task<T>> query)
    {
        T result = default!;
        await factory.WithDbAsync(async db => result = await query(db));
        return result;
    }

    public static Task<int> AreaIdAsync(FacilityApiFactory factory, string code) =>
        ReadAsync(factory, db => db.Areas.Where(a => a.Code == code).Select(a => a.Id).SingleAsync());

    public static Task<int> BuildingIdAsync(FacilityApiFactory factory, string code = "A") =>
        ReadAsync(factory, db => db.Buildings.Where(b => b.Code == code).Select(b => b.Id).SingleAsync());

    public static Task<int> SignIdAsync(FacilityApiFactory factory, string code) =>
        ReadAsync(factory, db => db.Signs.Where(s => s.Code == code).Select(s => s.Id).SingleAsync());

    /// <summary>The point whose sign is printed with <paramref name="signCode"/>, e.g. AR01-01.</summary>
    public static Task<int> PointIdAsync(FacilityApiFactory factory, string signCode) =>
        ReadAsync(factory, db => db.Signs.Where(s => s.Code == signCode).Select(s => s.ServicePointId!.Value).SingleAsync());

    public static Task<int> UserIdAsync(FacilityApiFactory factory, string employeeId) =>
        ReadAsync(factory, db => db.Users.Where(u => u.EmployeeId == employeeId).Select(u => u.Id).SingleAsync());

    public static Task<List<AuditEntry>> AuditAsync(FacilityApiFactory factory) =>
        ReadAsync(factory, db => db.AuditLog.AsNoTracking().OrderBy(a => a.Id).ToListAsync());

    public static async Task<string> MessageAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<MessageModel>())!.Message;

    /// <summary>The names on the Admin Dashboard right now.</summary>
    public static async Task<List<string>> DashboardNamesAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<PointModel>>("/api/service-points"))!.Select(p => p.Name).ToList();
}
```

- [ ] **Step 2: Write the failing tests**

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AreaAdminEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// facility-0045: the Admin's Area pages. Seed: building A; AR01 (Day and Night, cleaners E1001 day and E1002 night,
/// check-in AR01-IN and points AR01-01/AR01-02, all located on site); AR02 (Day only, cleaner E1003, AR02-IN and AR02-01, not located).
/// </summary>
public class AreaAdminEndpointTests
{
    private const string Areas = "/api/admin/areas";

    [Fact]
    public async Task Admin_pages_require_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().GetAsync(Areas);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/areas")]
    [InlineData("/api/admin/areas/1")]
    [InlineData("/api/admin/buildings")]
    [InlineData("/api/admin/cleaners")]
    public async Task Cleaners_cannot_open_admin_pages(string path)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Area_list_shows_cleaners_point_counts_and_unconfirmed_signs()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var areas = (await admin.GetFromJsonAsync<List<AreaSummaryModel>>(Areas))!;

        Assert.Equal(new[] { "AR01", "AR02" }, areas.Select(a => a.Code));
        var area1 = areas[0];
        Assert.Equal("DayAndNight", area1.ShiftPattern);
        Assert.Equal("A", area1.BuildingCode);
        Assert.True(area1.IsActive);
        Assert.Equal("E1001", area1.DayCleaner!.EmployeeId);
        Assert.Equal("E1002", area1.NightCleaner!.EmployeeId);
        Assert.Equal(2, area1.ActivePointCount);
        Assert.Equal(0, area1.SignsNotConfirmedOnSite);
        var area2 = areas[1];
        Assert.Equal("DayOnly", area2.ShiftPattern);
        Assert.Equal("E1003", area2.DayCleaner!.EmployeeId);
        Assert.Null(area2.NightCleaner);
        Assert.Equal(1, area2.ActivePointCount);
        Assert.Equal(2, area2.SignsNotConfirmedOnSite);
    }

    [Fact]
    public async Task Area_detail_lists_every_sign_with_its_qr_token_and_the_windows_in_shift_order()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var detail = (await admin.GetFromJsonAsync<AreaDetailModel>($"{Areas}/{area1}"))!;

        Assert.Equal("AR01", detail.Area.Code);
        Assert.Equal("AR01-IN", detail.CheckInSign.Code);
        Assert.Equal("token-checkin-ar01", detail.CheckInSign.QrToken); // facility-0060: the Admin's points page carries tokens
        Assert.Equal("Site", detail.CheckInSign.LocationSource);
        Assert.Equal(50, detail.CheckInSign.RadiusM);
        Assert.Equal(new[] { "AR01-01", "AR01-02" }, detail.Points.Select(p => p.Sign.Code));
        var men = detail.Points[0];
        Assert.Equal("ห้องน้ำชาย ชั้น 1", men.Name);
        Assert.True(men.IsActive);
        Assert.Equal(
            new[] { "Day 07:00-09:00", "Day 16:00-18:00", "Night 20:00-22:00", "Night 03:00-05:00" },
            men.RoundWindows.Select(w => $"{w.Shift} {w.Start}-{w.End}"));
    }

    [Fact]
    public async Task Unknown_area_is_not_found()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.GetAsync($"{Areas}/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Pickers_list_active_buildings_and_cleaners_only()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var buildings = (await admin.GetFromJsonAsync<List<BuildingModel>>("/api/admin/buildings"))!;
        var cleaners = (await admin.GetFromJsonAsync<List<CleanerOptionModel>>("/api/admin/cleaners"))!;

        Assert.Equal("A", Assert.Single(buildings).Code);
        Assert.Equal(new[] { "E1001", "E1002", "E1003" }, cleaners.Select(c => c.EmployeeId)); // no Supervisor, no Admin
        Assert.Equal(("Night", "AR01"), (cleaners[1].Shift, cleaners[1].AreaCode));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AreaAdminEndpointTests"`
Expected: FAIL. `Admin_pages_require_login` and the 403 theory get 404, because the routes do not exist yet, and the rest fail on 404.

- [ ] **Step 4: Write the DTOs**

Create `backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs`:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record BuildingDto(int Id, string Code, string Name);

/// <summary>For the Area form's Cleaner pickers (wireframe D10).</summary>
public record CleanerOptionDto(int Id, string EmployeeId, string DisplayName, Shift? Shift, int? AreaId, string? AreaCode);

public record AssignedCleanerDto(int Id, string EmployeeId, string DisplayName);

/// <summary>SignsNotConfirmedOnSite: the check-in sign and active points' signs with no location, or only a map pick (facility-0045).</summary>
public record AreaSummaryDto(
    int Id,
    string Code,
    string Name,
    int BuildingId,
    string BuildingCode,
    ShiftPattern ShiftPattern,
    bool IsActive,
    AssignedCleanerDto? DayCleaner,
    AssignedCleanerDto? NightCleaner,
    int ActivePointCount,
    int SignsNotConfirmedOnSite);

/// <summary>Carries the QR Token: the Admin's points and print pages are where it comes from (facility-0060).</summary>
public record AdminSignDto(
    int Id,
    string Code,
    string QrToken,
    DateTime QrIssuedAt,
    decimal? Latitude,
    decimal? Longitude,
    short? LocationAccuracyM,
    LocationSource? LocationSource,
    DateTime? LocatedAt,
    short RadiusM);

/// <summary>Start and End are Thai wall-clock "HH:mm", as the Admin types them.</summary>
public record AdminRoundWindowDto(int Id, Shift Shift, string Start, string End);

public record AdminPointDto(int Id, string Name, short SortOrder, bool IsActive, AdminSignDto Sign, IReadOnlyList<AdminRoundWindowDto> RoundWindows);

public record AreaDetailDto(AreaSummaryDto Area, AdminSignDto CheckInSign, IReadOnlyList<AdminPointDto> Points);
```

- [ ] **Step 5: Write the read model**

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminSetupView.cs`:

```csharp
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>What the Admin's Area, point and sign pages show, deactivated rows included.</summary>
internal static class AdminSetupView
{
    public static async Task<List<AreaSummaryDto>> LoadAreasAsync(AppDbContext db, int? onlyAreaId = null)
    {
        var areas = await db.Areas.AsNoTracking()
            .Include(a => a.Building)
            .Where(a => onlyAreaId == null || a.Id == onlyAreaId)
            .OrderBy(a => a.Code)
            .ToListAsync();
        var ids = areas.Select(a => a.Id).ToList();

        var cleaners = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.AreaId != null && ids.Contains(u.AreaId.Value))
            .ToListAsync();
        var activePointAreaIds = await db.ServicePoints.AsNoTracking()
            .Where(p => p.IsActive && ids.Contains(p.AreaId))
            .Select(p => p.AreaId)
            .ToListAsync();
        var signs = await db.Signs.AsNoTracking()
            .Where(s => ids.Contains(s.AreaId) && (s.ServicePointId == null || s.ServicePoint!.IsActive))
            .Select(s => new { s.AreaId, s.LocationSource })
            .ToListAsync();

        return areas.Select(a => new AreaSummaryDto(
                a.Id,
                a.Code,
                a.Name,
                a.BuildingId,
                a.Building!.Code,
                a.ShiftPattern,
                a.IsActive,
                CleanerOf(cleaners, a.Id, Shift.Day),
                CleanerOf(cleaners, a.Id, Shift.Night),
                activePointAreaIds.Count(id => id == a.Id),
                signs.Count(s => s.AreaId == a.Id && s.LocationSource != LocationSource.Site)))
            .ToList();
    }

    public static async Task<AreaDetailDto?> LoadAreaAsync(AppDbContext db, int areaId)
    {
        var summary = (await LoadAreasAsync(db, areaId)).SingleOrDefault();
        if (summary is null)
        {
            return null;
        }

        var signs = await db.Signs.AsNoTracking().Where(s => s.AreaId == areaId).ToListAsync();
        var points = await db.ServicePoints.AsNoTracking()
            .Where(p => p.AreaId == areaId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();
        var pointIds = points.Select(p => p.Id).ToList();
        var windows = await db.PointRoundWindows.AsNoTracking().Where(w => pointIds.Contains(w.ServicePointId)).ToListAsync();

        return new AreaDetailDto(
            summary,
            ToDto(signs.Single(s => s.ServicePointId == null)),
            points.Select(p => ToDto(p, signs.Single(s => s.ServicePointId == p.Id), windows)).ToList());
    }

    public static AdminSignDto ToDto(Sign sign) => new(
        sign.Id,
        sign.Code,
        sign.QrToken,
        Utc(sign.QrIssuedAt),
        sign.Latitude,
        sign.Longitude,
        sign.LocationAccuracyM,
        sign.LocationSource,
        sign.LocatedAt is { } locatedAt ? Utc(locatedAt) : (DateTime?)null,
        sign.RadiusM);

    /// <summary>Windows in the order the shift meets them: Day before Night, a night 03:00 after 20:00.</summary>
    public static AdminPointDto ToDto(ServicePoint point, Sign sign, IEnumerable<PointRoundWindow> windows) => new(
        point.Id,
        point.Name,
        point.SortOrder,
        point.IsActive,
        ToDto(sign),
        windows.Where(w => w.ServicePointId == point.Id)
            .OrderBy(w => w.Shift)
            .ThenBy(w => RoundWindowRules.MinutesIntoShift(w.Shift, w.StartTime))
            .Select(w => new AdminRoundWindowDto(w.Id, w.Shift, RoundWindowRules.Format(w.StartTime), RoundWindowRules.Format(w.EndTime)))
            .ToList());

    private static AssignedCleanerDto? CleanerOf(List<User> cleaners, int areaId, Shift shift) =>
        cleaners.Where(u => u.AreaId == areaId && u.Shift == shift)
            .Select(u => new AssignedCleanerDto(u.Id, u.EmployeeId ?? string.Empty, u.DisplayName))
            .FirstOrDefault();

    /// <summary>The database returns Unspecified; marking it UTC makes the JSON end in "Z".</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);
}
```

- [ ] **Step 6: Write the endpoints and map them**

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs`:

```csharp
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0045: the Admin sets up Areas, their shift pattern and their Cleaners. Every change is logged (facility-0051).</summary>
public static class AdminAreaEndpoints
{
    public static IEndpointRouteBuilder MapAdminAreaEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/buildings", ListBuildingsAsync);
        admin.MapGet("/cleaners", ListCleanersAsync);
        admin.MapGet("/areas", async (AppDbContext db) => Results.Ok(await AdminSetupView.LoadAreasAsync(db)));
        admin.MapGet("/areas/{id:int}", async (int id, AppDbContext db) =>
            await AdminSetupView.LoadAreaAsync(db, id) is { } area ? Results.Ok(area) : AreaNotFound());
        return app;
    }

    private static async Task<IResult> ListBuildingsAsync(AppDbContext db) =>
        Results.Ok(await db.Buildings.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Code)
            .Select(b => new BuildingDto(b.Id, b.Code, b.Name))
            .ToListAsync());

    /// <summary>Every active Cleaner with their regular shift and current Area; the form offers those without an Area.</summary>
    private static async Task<IResult> ListCleanersAsync(AppDbContext db) =>
        Results.Ok(await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive)
            .OrderBy(u => u.EmployeeId)
            .Select(u => new CleanerOptionDto(
                u.Id, u.EmployeeId ?? string.Empty, u.DisplayName, u.Shift, u.AreaId, u.Area == null ? null : u.Area.Code))
            .ToListAsync());

    private static IResult AreaNotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบ Area นี้");
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add after `app.MapMyWorkEndpoints();`:

```csharp
app.MapAdminAreaEndpoints();
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AreaAdminEndpointTests"`
Expected: PASS, 9 tests. Then `dotnet test backend/FacilityRealtime.slnx`: 0 failures.

- [ ] **Step 8: Commit**

```bash
git add backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs backend/src/FacilityRealtime.Api/Endpoints/AdminSetupView.cs backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs backend/src/FacilityRealtime.Api/Program.cs backend/tests/FacilityRealtime.ApiTests/Admin/AdminSetupModels.cs backend/tests/FacilityRealtime.ApiTests/Admin/AdminSetupApi.cs backend/tests/FacilityRealtime.ApiTests/Admin/AreaAdminEndpointTests.cs
git commit -m "feat(backend): Admin reads Areas with their signs, QR tokens and round windows (facility-0045, 0060)"
```

---

### Task 3: Create and edit Areas, assign Cleaners, deactivate

**Files:**
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/AreaCleaners.cs`
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/RoundWindowRemoval.cs`
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/AuditTrail.cs` (refuse entities)
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/PointBoardQuery.cs` (hide deactivated Areas)
- Modify: `backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs` (requests)
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs` (POST, PUT, deactivate, activate)
- Modify: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs` (`AddCleanerAsync`)
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/AreaAdminEndpointTests.cs` (append), `Persistence/AuditTrailTests.cs`, `Persistence/MySqlAreaCleanerTests.cs`

**Interfaces:**
- Consumes (Task 1): `SignCodes.NormalizeAreaCode`, `SignCodes.CheckIn`, `SignCodes.NewQrToken`.
- Consumes (Task 2): `AdminSetupView.LoadAreaAsync`, and the test helpers and models.
- Produces (namespace `FacilityRealtime.Infrastructure.Persistence`):
  - `static class AreaCleaners { Task ApplyAsync(AppDbContext db, int areaId, User? dayCleaner, User? nightCleaner); }`, called inside a transaction; it saves twice.
  - `static class RoundWindowRemoval { Task RemoveAsync(AppDbContext db, IReadOnlyCollection<PointRoundWindow> windows); }`, which unlinks Scan Records and marks the windows deleted. The caller saves.
- Produces (DTOs):
  - `CreateAreaRequest(string? Code, string? Name, int BuildingId, ShiftPattern? ShiftPattern, int? DayCleanerId, int? NightCleanerId)`
  - `UpdateAreaRequest(string? Name, int BuildingId, ShiftPattern? ShiftPattern, int? DayCleanerId, int? NightCleanerId)`
- Produces (tests): `TestData.AddCleanerAsync(FacilityApiFactory factory, string employeeId, Shift shift, string? areaCode = null) : Task<int>`.
- Audit actions: `AREA_CREATE`, `AREA_UPDATE`, `AREA_DEACTIVATE`, `AREA_ACTIVATE`. All use entity type `areas`.

- [ ] **Step 1: Add the test helper**

Add to `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs` (inside the class; the file already uses `Domain.Entities` and `Domain.Enums`):

```csharp
    /// <summary>An active Cleaner who never logs in, on <paramref name="shift"/>, in no Area unless one is given.</summary>
    public static async Task<int> AddCleanerAsync(FacilityApiFactory factory, string employeeId, Shift shift, string? areaCode = null)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var area = areaCode is null ? null : await db.Areas.SingleAsync(a => a.Code == areaCode);
            var cleaner = new User
            {
                Role = UserRole.Cleaner,
                EmployeeId = employeeId,
                DisplayName = $"แม่บ้านทดสอบ {employeeId}",
                SecretHash = "not-a-hash",
                AreaId = area?.Id,
                Shift = shift,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(cleaner);
            await db.SaveChangesAsync();
            id = cleaner.Id;
        });
        return id;
    }
```

- [ ] **Step 2: Write the failing tests**

Create `backend/tests/FacilityRealtime.ApiTests/Persistence/AuditTrailTests.cs`:

```csharp
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

public class AuditTrailTests
{
    /// <summary>facility-0057: no phone or password in the log. A User entity carries a phone hash, so only snapshots go in.</summary>
    [Fact]
    public async Task Entities_are_refused_as_audit_snapshots()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "E1001");

            Assert.Throws<ArgumentException>(() =>
                AuditTrail.Add(db, cleaner.Id, DateTime.UtcNow, "TEST", "users", cleaner.Id, "test", before: cleaner, after: null));
            Assert.Throws<ArgumentException>(() =>
                AuditTrail.Add(db, cleaner.Id, DateTime.UtcNow, "TEST", "users", cleaner.Id, "test", before: null, after: cleaner));
            Assert.Empty(db.AuditLog.Local);
        });
    }
}
```

Append to `backend/tests/FacilityRealtime.ApiTests/Admin/AreaAdminEndpointTests.cs`. Add `using System.Text.Json;`, `using FacilityRealtime.Domain.Enums;` and `using Microsoft.EntityFrameworkCore;` at the top, then add inside the class:

```csharp
    private static async Task<object> Ar01FormAsync(FacilityApiFactory factory, string? day, string? night, string pattern = "DayAndNight") => new
    {
        name = "Area 1 ชั้น 1",
        buildingId = await AdminSetupApi.BuildingIdAsync(factory),
        shiftPattern = pattern,
        dayCleanerId = day is null ? (int?)null : await AdminSetupApi.UserIdAsync(factory, day),
        nightCleanerId = night is null ? (int?)null : await AdminSetupApi.UserIdAsync(factory, night),
    };

    private static Task<int?> AreaOfAsync(FacilityApiFactory factory, string employeeId) =>
        AdminSetupApi.ReadAsync(factory, db => db.Users.Where(u => u.EmployeeId == employeeId).Select(u => u.AreaId).SingleAsync());

    [Fact]
    public async Task Admin_creates_an_area_with_its_check_in_sign_and_it_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Areas, new
        {
            code = " ar03 ",
            name = "Lobby ชั้น 1",
            buildingId = await AdminSetupApi.BuildingIdAsync(factory),
            shiftPattern = "DayOnly",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<AreaDetailModel>())!;
        Assert.Equal("AR03", detail.Area.Code);
        Assert.Equal("DayOnly", detail.Area.ShiftPattern);
        Assert.Equal("AR03-IN", detail.CheckInSign.Code);
        Assert.True(Guid.TryParse(detail.CheckInSign.QrToken, out _));
        Assert.Equal(50, detail.CheckInSign.RadiusM);
        Assert.Null(detail.CheckInSign.LocationSource);
        Assert.Empty(detail.Points);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("AREA_CREATE", entry.Action);
        Assert.Equal(adminAuth.User.Id, entry.ActorId);
        Assert.Equal("areas", entry.EntityType);
        Assert.Equal(detail.Area.Id, entry.EntityId);
        Assert.DoesNotContain(detail.CheckInSign.QrToken, entry.AfterJson);
    }

    [Fact]
    public async Task A_new_area_can_take_a_cleaner_without_an_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var newcomer = await TestData.AddCleanerAsync(factory, "E1009", Shift.Day);

        var response = await admin.PostAsJsonAsync(Areas, new
        {
            code = "AR03",
            name = "Lobby ชั้น 1",
            buildingId = await AdminSetupApi.BuildingIdAsync(factory),
            shiftPattern = "DayOnly",
            dayCleanerId = newcomer,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<AreaDetailModel>())!;
        Assert.Equal("E1009", detail.Area.DayCleaner!.EmployeeId);
        Assert.Equal(detail.Area.Id, await AreaOfAsync(factory, "E1009"));
    }

    [Theory]
    [InlineData("A", "Lobby", "DayOnly", false)]        // code too short
    [InlineData("AR-03", "Lobby", "DayOnly", false)]    // hyphen
    [InlineData("AR03", "  ", "DayOnly", false)]        // no name
    [InlineData("AR03", "Lobby", null, false)]          // no shift pattern
    [InlineData("AR03", "Lobby", "DayOnly", true)]      // unknown building
    public async Task Invalid_new_areas_are_refused(string code, string name, string? pattern, bool unknownBuilding)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Areas, new
        {
            code,
            name,
            buildingId = unknownBuilding ? 9999 : await AdminSetupApi.BuildingIdAsync(factory),
            shiftPattern = pattern,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(2, await AdminSetupApi.ReadAsync(factory, db => db.Areas.CountAsync()));
    }

    [Fact]
    public async Task Area_codes_are_unique()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Areas, new
        {
            code = "ar01",
            name = "อีก Area",
            buildingId = await AdminSetupApi.BuildingIdAsync(factory),
            shiftPattern = "DayOnly",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("day", "E1002", HttpStatusCode.BadRequest)]   // a Night Cleaner in the Day slot
    [InlineData("night", "E1009", HttpStatusCode.BadRequest)] // AR02 works days only
    [InlineData("day", "S2001", HttpStatusCode.BadRequest)]   // not a Cleaner
    [InlineData("day", "E1001", HttpStatusCode.Conflict)]     // already the Day Cleaner of AR01
    public async Task Cleaner_choices_are_checked(string slot, string employeeId, HttpStatusCode expected)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddCleanerAsync(factory, "E1009", Shift.Night);
        var chosen = await AdminSetupApi.UserIdAsync(factory, employeeId);
        var current = await AdminSetupApi.UserIdAsync(factory, "E1003");

        var response = await admin.PutAsJsonAsync($"{Areas}/{await AdminSetupApi.AreaIdAsync(factory, "AR02")}", new
        {
            name = "Office ชั้น 2",
            buildingId = await AdminSetupApi.BuildingIdAsync(factory),
            shiftPattern = "DayOnly",
            dayCleanerId = slot == "day" ? chosen : current,
            nightCleanerId = slot == "night" ? chosen : (int?)null,
        });

        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR02"), await AreaOfAsync(factory, "E1003"));
    }

    [Fact]
    public async Task Replacing_a_cleaner_takes_the_old_one_off_the_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var newcomer = await TestData.AddCleanerAsync(factory, "E1009", Shift.Day);
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await admin.PutAsJsonAsync($"{Areas}/{area1}", await Ar01FormAsync(factory, "E1009", "E1002"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<AreaDetailModel>())!;
        Assert.Equal("E1009", detail.Area.DayCleaner!.EmployeeId);
        Assert.Equal("E1002", detail.Area.NightCleaner!.EmployeeId);
        Assert.Null(await AreaOfAsync(factory, "E1001"));
        Assert.Equal(area1, await AreaOfAsync(factory, "E1009"));
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("AREA_UPDATE", entry.Action);
        var before = JsonDocument.Parse(entry.BeforeJson!).RootElement;
        var after = JsonDocument.Parse(entry.AfterJson!).RootElement;
        Assert.Equal(await AdminSetupApi.UserIdAsync(factory, "E1001"), before.GetProperty("DayCleanerId").GetInt32());
        Assert.Equal(newcomer, after.GetProperty("DayCleanerId").GetInt32());
    }

    [Fact]
    public async Task Leaving_a_shift_empty_takes_its_cleaner_off()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await admin.PutAsJsonAsync($"{Areas}/{area1}", await Ar01FormAsync(factory, "E1001", night: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.Content.ReadFromJsonAsync<AreaDetailModel>())!.Area.NightCleaner);
        Assert.Null(await AreaOfAsync(factory, "E1002"));
        Assert.Equal(area1, await AreaOfAsync(factory, "E1001"));
    }

    [Fact]
    public async Task Switching_to_day_only_removes_night_rounds_and_keeps_old_scans_round_times()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var nightScan = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 20, 30));
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await admin.PutAsJsonAsync($"{Areas}/{area1}", await Ar01FormAsync(factory, "E1001", null, "DayOnly"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<AreaDetailModel>())!;
        Assert.Equal("DayOnly", detail.Area.ShiftPattern);
        Assert.All(detail.Points, p => Assert.All(p.RoundWindows, w => Assert.Equal("Day", w.Shift)));
        Assert.Equal(4, detail.Points.Sum(p => p.RoundWindows.Count));
        var scan = await AdminSetupApi.ReadAsync(factory, db => db.ScanRecords.AsNoTracking().SingleAsync(s => s.Id == nightScan));
        Assert.Null(scan.RoundWindowId);
        Assert.Equal(new TimeOnly(20, 0), scan.RoundStart);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(4, JsonDocument.Parse(entry.AfterJson!).RootElement.GetProperty("RemovedNightWindows").GetInt32());
    }

    [Fact]
    public async Task Saving_an_unchanged_area_logs_nothing()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await admin.PutAsJsonAsync($"{Areas}/{area1}", await Ar01FormAsync(factory, "E1001", "E1002"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_deactivated_area_leaves_the_dashboard_and_the_scan_page_until_reactivated()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var area2 = await AdminSetupApi.AreaIdAsync(factory, "AR02");

        var off = await admin.PostAsync($"{Areas}/{area2}/deactivate", null);
        var offAgain = await admin.PostAsync($"{Areas}/{area2}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await off.Content.ReadFromJsonAsync<AreaDetailModel>())!.Area.IsActive);
        Assert.Equal(HttpStatusCode.OK, offAgain.StatusCode);
        Assert.DoesNotContain("ห้องประชุม", await AdminSetupApi.DashboardNamesAsync(admin));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/service-points/by-token/token-meeting-room")).StatusCode);

        var on = await admin.PostAsync($"{Areas}/{area2}/activate", null);

        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.Contains("ห้องประชุม", await AdminSetupApi.DashboardNamesAsync(admin));
        Assert.Equal(new[] { "AREA_DEACTIVATE", "AREA_ACTIVATE" }, (await AdminSetupApi.AuditAsync(factory)).Select(a => a.Action));
    }

    [Fact]
    public async Task Cleaners_cannot_create_areas()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.PostAsJsonAsync(Areas, new { code = "AR03", name = "Lobby", buildingId = 1, shiftPattern = "DayOnly" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
```

Create `backend/tests/FacilityRealtime.ApiTests/Persistence/MySqlAreaCleanerTests.cs`:

```csharp
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>users.cleaner_slot is a stored computed column with a UNIQUE index, checked row by row on MySQL.</summary>
public class MySqlAreaCleanerTests
{
    [MySqlFact]
    public async Task Replacing_an_areas_cleaner_passes_the_unique_slot_on_MySQL()
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
            int area1Id;
            await using (var write = new AppDbContext(options))
            {
                await DbInitializer.SeedAsync(write, new Pbkdf2PasswordHasher(iterations: 1_000));
                area1Id = (await write.Areas.SingleAsync(a => a.Code == "AR01")).Id;
                var newcomer = new User
                {
                    Role = UserRole.Cleaner,
                    EmployeeId = "E1009",
                    DisplayName = "แม่บ้านทดสอบ E1009",
                    SecretHash = "not-a-hash",
                    Shift = Shift.Day,
                    CreatedAt = DateTime.UtcNow,
                };
                write.Users.Add(newcomer);
                await write.SaveChangesAsync();

                // Why AreaCleaners saves twice: two active Day Cleaners on AR01, even for one statement, are refused
                newcomer.AreaId = area1Id;
                await Assert.ThrowsAsync<DbUpdateException>(() => write.SaveChangesAsync());
            }

            await using (var write = new AppDbContext(options))
            {
                var newcomer = await write.Users.SingleAsync(u => u.EmployeeId == "E1009");
                var night = await write.Users.SingleAsync(u => u.EmployeeId == "E1002");
                await using var transaction = await write.Database.BeginTransactionAsync();
                await AreaCleaners.ApplyAsync(write, area1Id, newcomer, night);
                await transaction.CommitAsync();
            }

            await using var read = new AppDbContext(options);
            Assert.Null((await read.Users.SingleAsync(u => u.EmployeeId == "E1001")).AreaId);
            Assert.Equal(area1Id, (await read.Users.SingleAsync(u => u.EmployeeId == "E1009")).AreaId);
            Assert.Equal(area1Id, (await read.Users.SingleAsync(u => u.EmployeeId == "E1002")).AreaId);
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AreaAdminEndpointTests|FullyQualifiedName~AuditTrailTests|FullyQualifiedName~MySqlAreaCleanerTests"`
Expected: build FAILS because `AreaCleaners` does not exist (the MySQL test references it). Once Steps 4–5 add it, the new Area tests still fail with 404/405 until Step 8 adds the routes.

- [ ] **Step 4: Refuse entities in `AuditTrail`**

Replace the body of `backend/src/FacilityRealtime.Infrastructure/Persistence/AuditTrail.cs` with:

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
            BeforeJson = Serialize(before, nameof(before)),
            AfterJson = Serialize(after, nameof(after)),
            Reason = reason,
        });

    /// <summary>facility-0057: no phone or password in the log. An entity can carry a hash, so only snapshots are accepted.</summary>
    private static string? Serialize(object? snapshot, string parameterName) =>
        snapshot is null ? null
        : snapshot.GetType().Assembly == typeof(AuditEntry).Assembly
            ? throw new ArgumentException($"Pass a snapshot, not the {snapshot.GetType().Name} entity.", parameterName)
            : JsonSerializer.Serialize(snapshot);
}
```

- [ ] **Step 5: Write `AreaCleaners` and `RoundWindowRemoval`**

Create `backend/src/FacilityRealtime.Infrastructure/Persistence/AreaCleaners.cs`:

```csharp
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>facility-0040: one Cleaner per shift per Area, set from the Admin's Area form (wireframe D10).</summary>
public static class AreaCleaners
{
    /// <summary>
    /// Puts the chosen Cleaners on the Area and takes every other active Cleaner off it (area_id NULL).
    /// Saves twice: users.cleaner_slot is UNIQUE and checked row by row, so the old Cleaner must leave the slot before
    /// the new one takes it. Call inside a transaction; the chosen users must be tracked by <paramref name="db"/>.
    /// </summary>
    public static async Task ApplyAsync(AppDbContext db, int areaId, User? dayCleaner, User? nightCleaner)
    {
        var chosenIds = new[] { dayCleaner?.Id, nightCleaner?.Id }.OfType<int>().ToHashSet();
        var current = await db.Users
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.AreaId == areaId)
            .ToListAsync();
        foreach (var leaving in current.Where(u => !chosenIds.Contains(u.Id)))
        {
            leaving.AreaId = null;
        }

        await db.SaveChangesAsync();

        foreach (var cleaner in new[] { dayCleaner, nightCleaner }.OfType<User>())
        {
            cleaner.AreaId = areaId;
        }

        await db.SaveChangesAsync();
    }
}
```

Create `backend/src/FacilityRealtime.Infrastructure/Persistence/RoundWindowRemoval.cs`:

```csharp
using FacilityRealtime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>database.html: a removed Round Window leaves its Scan Records with round_window_id NULL and their copied round times.</summary>
public static class RoundWindowRemoval
{
    /// <summary>
    /// Marks the windows deleted; the caller saves. Their Scan Records are loaded first so EF sets the link to NULL itself,
    /// which holds even where the database does not enforce the foreign key (SQLite in the API tests).
    /// </summary>
    public static async Task RemoveAsync(AppDbContext db, IReadOnlyCollection<PointRoundWindow> windows)
    {
        if (windows.Count == 0)
        {
            return;
        }

        var ids = windows.Select(w => w.Id).ToList();
        await db.ScanRecords.Where(s => s.RoundWindowId != null && ids.Contains(s.RoundWindowId.Value)).LoadAsync();
        db.PointRoundWindows.RemoveRange(windows);
    }
}
```

- [ ] **Step 6: Hide deactivated Areas from the Dashboard**

In `backend/src/FacilityRealtime.Infrastructure/Persistence/PointBoardQuery.cs`, change the first `LoadAsync` overload's summary and the filter in the second overload:

```csharp
    /// <summary>The Dashboard: every active point of an active Area, as seen from the shift running now.</summary>
```

```csharp
        var query = db.ServicePoints.AsNoTracking()
            .Include(p => p.Area!).ThenInclude(a => a.Building)
            .Where(p => p.IsActive && p.Area!.IsActive);
```

- [ ] **Step 7: Add the requests**

Append to `backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs`:

```csharp
/// <summary>The Area form (wireframe D10). The code is fixed once created: every sign of the Area prints it.</summary>
public record CreateAreaRequest(string? Code, string? Name, int BuildingId, ShiftPattern? ShiftPattern, int? DayCleanerId, int? NightCleanerId);

/// <summary>An empty Cleaner slot takes that shift's Cleaner off the Area.</summary>
public record UpdateAreaRequest(string? Name, int BuildingId, ShiftPattern? ShiftPattern, int? DayCleanerId, int? NightCleanerId);
```

- [ ] **Step 8: Write the Area write endpoints**

In `backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs`, add these usings at the top:

```csharp
using System.Security.Claims;
using FacilityRealtime.Application.Signs;
using FacilityRealtime.Domain.Entities;
```

Add these routes in `MapAdminAreaEndpoints`, before `return app;`:

```csharp
        admin.MapPost("/areas", CreateAsync);
        admin.MapPut("/areas/{id:int}", UpdateAsync);
        admin.MapPost("/areas/{id:int}/deactivate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, false, principal, db, clock));
        admin.MapPost("/areas/{id:int}/activate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, true, principal, db, clock));
```

Add these members to the class:

```csharp
    private sealed record AreaForm(string Name, Building Building, ShiftPattern Pattern, User? DayCleaner, User? NightCleaner);

    /// <summary>What the audit log keeps of an Area: no names of people, only ids.</summary>
    private sealed record AreaSnapshot(
        string Code, string Name, int BuildingId, string ShiftPattern, int? DayCleanerId, int? NightCleanerId, int? RemovedNightWindows = null);

    private static async Task<IResult> CreateAsync(CreateAreaRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (SignCodes.NormalizeAreaCode(request.Code) is not { } code)
        {
            return Bad("รหัส Area ต้องเป็นตัวอักษรอังกฤษหรือตัวเลข 2–20 ตัว เช่น AR03");
        }

        var (form, error) = await ReadFormAsync(
            db, areaId: null, request.Name, request.BuildingId, request.ShiftPattern, request.DayCleanerId, request.NightCleanerId);
        if (form is null)
        {
            return error!;
        }

        if (await db.Areas.AnyAsync(a => a.Code == code))
        {
            return Conflict("รหัส Area นี้มีแล้ว");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var area = new Area { BuildingId = form.Building.Id, Code = code, Name = form.Name, ShiftPattern = form.Pattern, CreatedAt = now };
        // facility-0045: the Area's one check-in sign comes with it
        db.Signs.Add(new Sign { Area = area, Code = SignCodes.CheckIn(code), QrToken = SignCodes.NewQrToken(), QrIssuedAt = now });

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.SaveChangesAsync();
            await AreaCleaners.ApplyAsync(db, area.Id, form.DayCleaner, form.NightCleaner);
            AuditTrail.Add(
                db, admin.Id, now, "AREA_CREATE", "areas", area.Id, $"เพิ่ม Area {code} {form.Name}",
                before: null, after: Snapshot(area, form.DayCleaner, form.NightCleaner));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("มีการบันทึกซ้อนกัน ลองใหม่อีกครั้ง");
        }

        return Results.Created($"/api/admin/areas/{area.Id}", await AdminSetupView.LoadAreaAsync(db, area.Id));
    }

    private static async Task<IResult> UpdateAsync(int id, UpdateAreaRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == id);
        if (area is null)
        {
            return AreaNotFound();
        }

        var (form, error) = await ReadFormAsync(
            db, area.Id, request.Name, request.BuildingId, request.ShiftPattern, request.DayCleanerId, request.NightCleanerId);
        if (form is null)
        {
            return error!;
        }

        var current = await db.Users.Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.AreaId == area.Id).ToListAsync();
        var before = Snapshot(area, current.FirstOrDefault(u => u.Shift == Shift.Day), current.FirstOrDefault(u => u.Shift == Shift.Night));

        area.Name = form.Name;
        area.BuildingId = form.Building.Id;
        area.ShiftPattern = form.Pattern;
        List<PointRoundWindow> nightWindows = form.Pattern == ShiftPattern.DayOnly
            ? await db.PointRoundWindows.Where(w => w.Shift == Shift.Night && w.ServicePoint!.AreaId == area.Id).ToListAsync()
            : [];
        // Wireframe D10: a day-only Area has no night rounds
        await RoundWindowRemoval.RemoveAsync(db, nightWindows);

        var now = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await AreaCleaners.ApplyAsync(db, area.Id, form.DayCleaner, form.NightCleaner);
            var after = Snapshot(area, form.DayCleaner, form.NightCleaner);
            if (after != before || nightWindows.Count > 0)
            {
                AuditTrail.Add(
                    db, admin.Id, now, "AREA_UPDATE", "areas", area.Id, $"แก้ Area {area.Code} {area.Name}",
                    before, after with { RemovedNightWindows = nightWindows.Count });
                await db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("มีการบันทึกซ้อนกัน ลองใหม่อีกครั้ง");
        }

        return Results.Ok(await AdminSetupView.LoadAreaAsync(db, area.Id));
    }

    /// <summary>Wireframe D10: a deactivated Area and its signs leave the Dashboard and cannot be scanned; history stays.</summary>
    private static async Task<IResult> SetActiveAsync(int id, bool active, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == id);
        if (area is null)
        {
            return AreaNotFound();
        }

        if (area.IsActive != active)
        {
            area.IsActive = active;
            AuditTrail.Add(
                db, admin.Id, clock.GetUtcNow().UtcDateTime, active ? "AREA_ACTIVATE" : "AREA_DEACTIVATE", "areas", area.Id,
                $"{(active ? "เปิด" : "ปิด")}ใช้งาน Area {area.Code}",
                before: new { IsActive = !active }, after: new { IsActive = active });
            await db.SaveChangesAsync();
        }

        return Results.Ok(await AdminSetupView.LoadAreaAsync(db, area.Id));
    }

    /// <summary>The fields Create and Update share; returns the form, or the answer to give instead.</summary>
    private static async Task<(AreaForm? Form, IResult? Error)> ReadFormAsync(
        AppDbContext db, int? areaId, string? name, int buildingId, ShiftPattern? pattern, int? dayCleanerId, int? nightCleanerId)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > 150)
        {
            return (null, Bad("ต้องใส่ชื่อ Area ไม่เกิน 150 ตัวอักษร"));
        }

        if (pattern is not { } shiftPattern || !Enum.IsDefined(shiftPattern))
        {
            return (null, Bad("ต้องเลือกรูปแบบกะ"));
        }

        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId && b.IsActive);
        if (building is null)
        {
            return (null, Bad("ไม่พบตึกนี้"));
        }

        if (shiftPattern == ShiftPattern.DayOnly && nightCleanerId is not null)
        {
            return (null, Bad("Area ที่ทำเฉพาะกะเช้าไม่มีแม่บ้านกะดึก"));
        }

        User? day = null;
        User? night = null;
        foreach (var (shift, cleanerId) in new[] { (Shift.Day, dayCleanerId), (Shift.Night, nightCleanerId) })
        {
            if (cleanerId is null)
            {
                continue;
            }

            var cleaner = await db.Users.Include(u => u.Area)
                .FirstOrDefaultAsync(u => u.Id == cleanerId && u.Role == UserRole.Cleaner && u.IsActive);
            if (cleaner is null)
            {
                return (null, Bad("ต้องเลือกแม่บ้านที่ใช้งานอยู่"));
            }

            if (cleaner.Shift != shift)
            {
                return (null, Bad($"{cleaner.DisplayName} ประจำกะ{ShiftName(cleaner.Shift)} ใส่ในช่องกะ{ShiftName(shift)}ไม่ได้"));
            }

            // Wireframe D10: the picker offers only Cleaners without an Area in that shift
            if (cleaner.AreaId is { } otherAreaId && otherAreaId != areaId)
            {
                return (null, Conflict($"{cleaner.DisplayName} ประจำ {cleaner.Area!.Code} อยู่แล้ว ย้ายออกจาก Area นั้นก่อน"));
            }

            if (shift == Shift.Day)
            {
                day = cleaner;
            }
            else
            {
                night = cleaner;
            }
        }

        return (new AreaForm(trimmed, building, shiftPattern, day, night), null);
    }

    private static AreaSnapshot Snapshot(Area area, User? day, User? night) =>
        new(area.Code, area.Name, area.BuildingId, area.ShiftPattern.ToString(), day?.Id, night?.Id);

    private static string ShiftName(Shift? shift) => shift switch
    {
        Shift.Day => "เช้า",
        Shift.Night => "ดึก",
        _ => "ที่ไม่ระบุ",
    };

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);

    private static IResult Conflict(string message) => ApiResults.Message(StatusCodes.Status409Conflict, message);
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~AreaAdminEndpointTests|FullyQualifiedName~AuditTrailTests"`
Expected: PASS, 28 tests (9 from Task 2, 18 new, 1 audit). Then `dotnet test backend/FacilityRealtime.slnx`: 0 failures. Earlier Dashboard, scan, attendance and cover tests must still pass.

Then run the MySQL test against a throwaway database. Ask the developer for the local root password and do not save it anywhere:

```bash
FACILITY_MYSQL_TEST="Server=localhost;Port=3306;Database=facility_adminsetup_test;Uid=root;Pwd=<local root password>;CharSet=utf8mb4;" dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~MySqlAreaCleanerTests|FullyQualifiedName~MySqlReadTests"
```

Expected: PASS, 2 tests, and the database is dropped afterwards.

- [ ] **Step 10: Commit**

```bash
git add backend/src/FacilityRealtime.Infrastructure/Persistence/AreaCleaners.cs backend/src/FacilityRealtime.Infrastructure/Persistence/RoundWindowRemoval.cs backend/src/FacilityRealtime.Infrastructure/Persistence/AuditTrail.cs backend/src/FacilityRealtime.Infrastructure/Persistence/PointBoardQuery.cs backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestData.cs backend/tests/FacilityRealtime.ApiTests/Admin/AreaAdminEndpointTests.cs backend/tests/FacilityRealtime.ApiTests/Persistence/AuditTrailTests.cs backend/tests/FacilityRealtime.ApiTests/Persistence/MySqlAreaCleanerTests.cs
git commit -m "feat(backend): Admin creates and edits Areas, assigns their Cleaners, deactivates them, all logged (facility-0045, 0040, 0051)"
```

---

### Task 4: Points and their Round Windows

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs` (requests)
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AdminSetupView.cs` (`LoadPointAsync`)
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminPointEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/PointAdminEndpointTests.cs`

**Interfaces:**
- Consumes (Task 1): `RoundWindowInput`, `RoundWindowRules.TryParseTime`, `RoundWindowRules.Validate`, `RoundWindowRules.Describe`, `RoundWindowRules.MinutesIntoShift`, `SignCodes.NextPointCode`, `SignCodes.NewQrToken`.
- Consumes (Task 3): `RoundWindowRemoval.RemoveAsync`.
- Produces (DTOs):
  - `RoundWindowRequest(Shift? Shift, string? Start, string? End)`
  - `SavePointRequest(string? Name, List<RoundWindowRequest>? RoundWindows)`
- Produces: `AdminSetupView.LoadPointAsync(AppDbContext db, int pointId) : Task<AdminPointDto?>`.
- Produces: `public static class AdminPointEndpoints { IEndpointRouteBuilder MapAdminPointEndpoints(this IEndpointRouteBuilder app); }`.
- Audit actions: `POINT_CREATE`, `POINT_UPDATE`, `POINT_DEACTIVATE`, `POINT_ACTIVATE`. All use entity type `service_points`. Windows appear as `"DAY 07:00-09:00"` strings.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.ApiTests/Admin/PointAdminEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// facility-0045 + 0047: points and their Round Windows. Seed: the men's restroom (AR01-01) has Day 07-09, 16-18 and
/// Night 20-22, 03-05; AR02 works days only.
/// </summary>
public class PointAdminEndpointTests
{
    private static object Window(string shift, string start, string end) => new { shift, start, end };

    private static readonly object[] MenRestroomRounds =
    [
        Window("Day", "07:00", "09:00"), Window("Day", "16:00", "18:00"),
        Window("Night", "20:00", "22:00"), Window("Night", "03:00", "05:00"),
    ];

    private static async Task<HttpResponseMessage> CreateAsync(FacilityApiFactory factory, HttpClient admin, string areaCode, string name, params object[] rounds) =>
        await admin.PostAsJsonAsync($"/api/admin/areas/{await AdminSetupApi.AreaIdAsync(factory, areaCode)}/points", new { name, roundWindows = rounds });

    private static async Task<HttpResponseMessage> UpdateMenRestroomAsync(FacilityApiFactory factory, HttpClient admin, params object[] rounds) =>
        await admin.PutAsJsonAsync($"/api/admin/points/{await AdminSetupApi.PointIdAsync(factory, "AR01-01")}", new { name = "ห้องน้ำชาย ชั้น 1", roundWindows = rounds });

    [Fact]
    public async Task Admin_adds_a_point_with_its_sign_and_rounds_and_it_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await CreateAsync(factory, admin, "AR01", "ห้องเก็บของ",
            Window("Night", "03:00", "04:00"), Window("Day", "10:00", "11:00"), Window("Night", "23:00", "01:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var point = (await response.Content.ReadFromJsonAsync<AdminPointModel>())!;
        Assert.Equal("AR01-03", point.Sign.Code);
        Assert.True(Guid.TryParse(point.Sign.QrToken, out _));
        Assert.Equal(3, point.SortOrder);
        Assert.Equal(
            new[] { "Day 10:00-11:00", "Night 23:00-01:00", "Night 03:00-04:00" },
            point.RoundWindows.Select(w => $"{w.Shift} {w.Start}-{w.End}"));
        Assert.Contains("ห้องเก็บของ", await AdminSetupApi.DashboardNamesAsync(admin));
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("POINT_CREATE", entry.Action);
        Assert.Equal("service_points", entry.EntityType);
        Assert.Equal(point.Id, entry.EntityId);
        Assert.Contains("DAY 10:00-11:00", entry.AfterJson);
    }

    [Theory]
    [InlineData("AR02", "ห้องเก็บของ", "Night", "20:00", "22:00", "เฉพาะกะเช้า")]
    [InlineData("AR01", "ห้องเก็บของ", "Day", "17:00", "19:00", "เวลาเปลี่ยนกะ")]   // owner rule 2026-10-09
    [InlineData("AR01", "ห้องเก็บของ", "Night", "05:00", "07:00", "เวลาเปลี่ยนกะ")]
    [InlineData("AR01", "ห้องเก็บของ", "Day", "7:00", "09:00", "แบบ 07:00")]
    [InlineData("AR01", "ห้องเก็บของ", "Day", "10:00", "09:00", "หลังเวลาเริ่ม")]
    [InlineData("AR01", " ", "Day", "07:00", "09:00", "ชื่อจุด")]
    public async Task Invalid_points_are_refused(string areaCode, string name, string shift, string start, string end, string expected)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await CreateAsync(factory, admin, areaCode, name, Window(shift, start, end));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await AdminSetupApi.MessageAsync(response));
        Assert.Equal(3, await AdminSetupApi.ReadAsync(factory, db => db.ServicePoints.CountAsync()));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Overlapping_rounds_are_refused()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await CreateAsync(factory, admin, "AR01", "ห้องเก็บของ", Window("Day", "07:00", "09:00"), Window("Day", "08:00", "10:00"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ซ้อนกัน", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task Unknown_area_or_point_is_not_found()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var create = await admin.PostAsJsonAsync("/api/admin/areas/9999/points", new { name = "x", roundWindows = Array.Empty<object>() });
        var update = await admin.PutAsJsonAsync("/api/admin/points/9999", new { name = "x", roundWindows = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task Editing_keeps_unchanged_rounds_and_the_scans_linked_to_them()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var morningScan = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 7, 30));
        var before = (await (await admin.GetAsync($"/api/admin/areas/{await AdminSetupApi.AreaIdAsync(factory, "AR01")}"))
            .Content.ReadFromJsonAsync<AreaDetailModel>())!.Points[0].RoundWindows;

        var response = await UpdateMenRestroomAsync(factory, admin,
            Window("Day", "07:00", "09:00"), Window("Day", "15:00", "17:00"),
            Window("Night", "20:00", "22:00"), Window("Night", "03:00", "05:00"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = (await response.Content.ReadFromJsonAsync<AdminPointModel>())!.RoundWindows;
        Assert.Equal(before[0].Id, after[0].Id);                     // 07:00-09:00 kept
        Assert.Equal("15:00", after[1].Start);
        Assert.DoesNotContain(after, w => w.Id == before[1].Id);     // 16:00-18:00 replaced
        Assert.Equal(before[2].Id, after[2].Id);
        var scan = await AdminSetupApi.ReadAsync(factory, db => db.ScanRecords.AsNoTracking().SingleAsync(s => s.Id == morningScan));
        Assert.Equal(before[0].Id, scan.RoundWindowId);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("POINT_UPDATE", entry.Action);
        Assert.Contains("DAY 16:00-18:00", entry.BeforeJson);
        Assert.Contains("DAY 15:00-17:00", entry.AfterJson);
    }

    [Fact]
    public async Task Removing_a_round_keeps_old_scans_round_times()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var afternoonScan = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 16, 30));

        var response = await UpdateMenRestroomAsync(factory, admin,
            Window("Day", "07:00", "09:00"), Window("Night", "20:00", "22:00"), Window("Night", "03:00", "05:00"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scan = await AdminSetupApi.ReadAsync(factory, db => db.ScanRecords.AsNoTracking().SingleAsync(s => s.Id == afternoonScan));
        Assert.Null(scan.RoundWindowId);
        Assert.Equal(new TimeOnly(16, 0), scan.RoundStart);
        Assert.Equal(new TimeOnly(18, 0), scan.RoundEnd);
    }

    [Fact]
    public async Task Saving_an_unchanged_point_logs_nothing()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await UpdateMenRestroomAsync(factory, admin, MenRestroomRounds);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_deactivated_point_leaves_the_dashboard_until_reactivated()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var men = await AdminSetupApi.PointIdAsync(factory, "AR01-01");

        var off = await admin.PostAsync($"/api/admin/points/{men}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await off.Content.ReadFromJsonAsync<AdminPointModel>())!.IsActive);
        Assert.DoesNotContain("ห้องน้ำชาย ชั้น 1", await AdminSetupApi.DashboardNamesAsync(admin));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/service-points/by-token/token-restroom-m1")).StatusCode);

        var on = await admin.PostAsync($"/api/admin/points/{men}/activate", null);

        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.Contains("ห้องน้ำชาย ชั้น 1", await AdminSetupApi.DashboardNamesAsync(admin));
        Assert.Equal(new[] { "POINT_DEACTIVATE", "POINT_ACTIVATE" }, (await AdminSetupApi.AuditAsync(factory)).Select(a => a.Action));
    }

    [Fact]
    public async Task Cleaners_cannot_edit_points()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await UpdateMenRestroomAsync(factory, cleaner, MenRestroomRounds);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~PointAdminEndpointTests"`
Expected: FAIL because the routes return 404/405.

- [ ] **Step 3: Add the requests and `LoadPointAsync`**

Append to `backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs`:

```csharp
/// <summary>Start and End as "HH:mm" Thai wall-clock, e.g. "07:00" (facility-0047).</summary>
public record RoundWindowRequest(Shift? Shift, string? Start, string? End);

/// <summary>The full list of the point's windows: unchanged ones are kept, the rest replaced.</summary>
public record SavePointRequest(string? Name, List<RoundWindowRequest>? RoundWindows);
```

Add to `AdminSetupView` in `backend/src/FacilityRealtime.Api/Endpoints/AdminSetupView.cs`:

```csharp
    public static async Task<AdminPointDto?> LoadPointAsync(AppDbContext db, int pointId)
    {
        var point = await db.ServicePoints.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pointId);
        if (point is null)
        {
            return null;
        }

        var sign = await db.Signs.AsNoTracking().SingleAsync(s => s.ServicePointId == pointId);
        var windows = await db.PointRoundWindows.AsNoTracking().Where(w => w.ServicePointId == pointId).ToListAsync();
        return ToDto(point, sign, windows);
    }
```

- [ ] **Step 4: Write the point endpoints and map them**

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminPointEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Signs;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0045 + 0047: the Admin adds and edits Service Points and their Round Windows. Every change is logged (facility-0051).</summary>
public static class AdminPointEndpoints
{
    public static IEndpointRouteBuilder MapAdminPointEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapPost("/areas/{areaId:int}/points", CreateAsync);
        admin.MapPut("/points/{id:int}", UpdateAsync);
        admin.MapPost("/points/{id:int}/deactivate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, false, principal, db, clock));
        admin.MapPost("/points/{id:int}/activate", (int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock) =>
            SetActiveAsync(id, true, principal, db, clock));
        return app;
    }

    private static async Task<IResult> CreateAsync(int areaId, SavePointRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == areaId);
        if (area is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบ Area นี้");
        }

        if (!TryReadForm(request, area.ShiftPattern, out var name, out var windows, out var error))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, error);
        }

        var usedCodes = await db.Signs.Where(s => s.AreaId == area.Id && s.ServicePointId != null).Select(s => s.Code).ToListAsync();
        var lastOrder = await db.ServicePoints.Where(p => p.AreaId == area.Id).MaxAsync(p => (int?)p.SortOrder) ?? 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var point = new ServicePoint { AreaId = area.Id, Name = name, SortOrder = (short)(lastOrder + 1), CreatedAt = now };
        var sign = new Sign
        {
            AreaId = area.Id,
            ServicePoint = point,
            Code = SignCodes.NextPointCode(area.Code, usedCodes),
            QrToken = SignCodes.NewQrToken(),
            QrIssuedAt = now,
        };
        db.Signs.Add(sign);
        db.PointRoundWindows.AddRange(windows.Select(w => NewWindow(point, w, now)));

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.SaveChangesAsync();
            AuditTrail.Add(
                db, admin.Id, now, "POINT_CREATE", "service_points", point.Id, $"เพิ่มจุด {sign.Code} {name}",
                before: null, after: Snapshot(name, windows));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "มีการบันทึกซ้อนกัน ลองใหม่อีกครั้ง");
        }

        return Results.Created($"/api/admin/points/{point.Id}", await AdminSetupView.LoadPointAsync(db, point.Id));
    }

    private static async Task<IResult> UpdateAsync(int id, SavePointRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var point = await db.ServicePoints.Include(p => p.Area).FirstOrDefaultAsync(p => p.Id == id);
        if (point is null)
        {
            return PointNotFound();
        }

        if (!TryReadForm(request, point.Area!.ShiftPattern, out var name, out var wanted, out var error))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, error);
        }

        var existing = await db.PointRoundWindows.Where(w => w.ServicePointId == id).ToListAsync();
        var before = Snapshot(point.Name, existing.Select(ToInput));

        // An unchanged window keeps its id, so the round running now keeps its Scan Records (facility-0047)
        var toAdd = wanted.ToList();
        var toRemove = new List<PointRoundWindow>();
        foreach (var row in existing)
        {
            var same = toAdd.FirstOrDefault(w => w == ToInput(row));
            if (same is null)
            {
                toRemove.Add(row);
            }
            else
            {
                toAdd.Remove(same);
            }
        }

        await RoundWindowRemoval.RemoveAsync(db, toRemove);
        var now = clock.GetUtcNow().UtcDateTime;
        db.PointRoundWindows.AddRange(toAdd.Select(w => NewWindow(point, w, now)));
        point.Name = name;

        if (db.ChangeTracker.HasChanges())
        {
            var code = await db.Signs.Where(s => s.ServicePointId == id).Select(s => s.Code).SingleAsync();
            AuditTrail.Add(
                db, admin.Id, now, "POINT_UPDATE", "service_points", point.Id, $"แก้จุด {code} {name}",
                before, after: Snapshot(name, wanted));
            await db.SaveChangesAsync();
        }

        return Results.Ok(await AdminSetupView.LoadPointAsync(db, point.Id));
    }

    /// <summary>facility-0021 rule 4: a deactivated point leaves the Dashboard and cannot be scanned; history stays.</summary>
    private static async Task<IResult> SetActiveAsync(int id, bool active, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var point = await db.ServicePoints.FirstOrDefaultAsync(p => p.Id == id);
        if (point is null)
        {
            return PointNotFound();
        }

        if (point.IsActive != active)
        {
            point.IsActive = active;
            var code = await db.Signs.Where(s => s.ServicePointId == id).Select(s => s.Code).SingleAsync();
            AuditTrail.Add(
                db, admin.Id, clock.GetUtcNow().UtcDateTime, active ? "POINT_ACTIVATE" : "POINT_DEACTIVATE", "service_points", point.Id,
                $"{(active ? "เปิด" : "ปิด")}ใช้งานจุด {code} {point.Name}",
                before: new { IsActive = !active }, after: new { IsActive = active });
            await db.SaveChangesAsync();
        }

        return Results.Ok(await AdminSetupView.LoadPointAsync(db, point.Id));
    }

    private static bool TryReadForm(
        SavePointRequest request, ShiftPattern pattern, out string name, out List<RoundWindowInput> windows, out string error)
    {
        name = request.Name?.Trim() ?? string.Empty;
        windows = [];
        if (name.Length is 0 or > 150)
        {
            error = "ต้องใส่ชื่อจุด ไม่เกิน 150 ตัวอักษร";
            return false;
        }

        foreach (var window in request.RoundWindows ?? [])
        {
            if (window.Shift is not { } shift || !Enum.IsDefined(shift))
            {
                error = "ต้องระบุกะของทุกช่วงรอบ";
                return false;
            }

            if (!RoundWindowRules.TryParseTime(window.Start, out var start) || !RoundWindowRules.TryParseTime(window.End, out var end))
            {
                error = "เวลาของช่วงรอบต้องเขียนแบบ 07:00";
                return false;
            }

            windows.Add(new RoundWindowInput(shift, start, end));
        }

        error = RoundWindowRules.Validate(windows, pattern) ?? string.Empty;
        return error.Length == 0;
    }

    private static RoundWindowInput ToInput(PointRoundWindow row) => new(row.Shift, row.StartTime, row.EndTime);

    private static PointRoundWindow NewWindow(ServicePoint point, RoundWindowInput window, DateTime now) => new()
    {
        ServicePoint = point,
        Shift = window.Shift,
        StartTime = window.Start,
        EndTime = window.End,
        CreatedAt = now,
    };

    /// <summary>What the audit log keeps of a point: its name and windows as "DAY 07:00-09:00".</summary>
    private static object Snapshot(string name, IEnumerable<RoundWindowInput> windows) => new
    {
        Name = name,
        RoundWindows = windows
            .OrderBy(w => w.Shift)
            .ThenBy(w => RoundWindowRules.MinutesIntoShift(w.Shift, w.Start))
            .Select(RoundWindowRules.Describe)
            .ToList(),
    };

    private static IResult PointNotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบจุดนี้");
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add after `app.MapAdminAreaEndpoints();`:

```csharp
app.MapAdminPointEndpoints();
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~PointAdminEndpointTests"`
Expected: PASS, 14 tests. Then `dotnet test backend/FacilityRealtime.slnx`: 0 failures.

- [ ] **Step 6: Commit**

```bash
git add backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs backend/src/FacilityRealtime.Api/Endpoints/AdminSetupView.cs backend/src/FacilityRealtime.Api/Endpoints/AdminPointEndpoints.cs backend/src/FacilityRealtime.Api/Program.cs backend/tests/FacilityRealtime.ApiTests/Admin/PointAdminEndpointTests.cs
git commit -m "feat(backend): Admin adds and edits points and their round windows; no window may end at the shift change (facility-0045, 0047)"
```

---

### Task 5: Sign location, radius and new QR Token; docs

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs` (requests)
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminSignEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`
- Modify: `docs/architecture.md` (component diagram endpoint list), `README.md` (Swagger recipe)
- Test: `backend/tests/FacilityRealtime.ApiTests/Admin/SignAdminEndpointTests.cs`

**Interfaces:**
- Consumes: `GpsInput.TryRead` (plan 2), `SignCodes.NewQrToken` (Task 1), `AdminSetupView.ToDto(Sign)` (Task 2), `AttendanceApi.RecordAsync` (test helper from plan 2).
- Produces (DTOs):
  - `SignLocationRequest(double? Latitude, double? Longitude, double? AccuracyM, LocationSource? Source)`
  - `SignRadiusRequest(int? RadiusM)`
- Produces: `public static class AdminSignEndpoints { const short MinRadiusM = 10; const short MaxRadiusM = 500; IEndpointRouteBuilder MapAdminSignEndpoints(this IEndpointRouteBuilder app); }`.
- Audit actions: `SIGN_LOCATION`, `SIGN_RADIUS`, `QR_REGENERATE`. All use entity type `signs`. The log never holds the QR Token.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.ApiTests/Admin/SignAdminEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0045 (capture at the sign, map as fallback), 0038 (radius), 0006/0021 (new QR Token).</summary>
public class SignAdminEndpointTests
{
    private static async Task<HttpResponseMessage> LocateAsync(
        FacilityApiFactory factory, HttpClient admin, string signCode, double? latitude, double? longitude, double? accuracyM, string? source) =>
        await admin.PutAsJsonAsync(
            $"/api/admin/signs/{await AdminSetupApi.SignIdAsync(factory, signCode)}/location",
            new { latitude, longitude, accuracyM, source });

    [Fact]
    public async Task A_location_captured_at_the_sign_is_stored_logged_and_used_by_the_next_scan()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (e1003, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E1003", "0810000003");

        var response = await LocateAsync(factory, admin, "AR02-IN", 13.757, 100.502, 17.2, "Site");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sign = (await response.Content.ReadFromJsonAsync<AdminSignModel>())!;
        Assert.Equal(13.757m, sign.Latitude);
        Assert.Equal(100.502m, sign.Longitude);
        Assert.Equal((short)18, sign.LocationAccuracyM);
        Assert.Equal("Site", sign.LocationSource);
        Assert.NotNull(sign.LocatedAt);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("SIGN_LOCATION", entry.Action);
        Assert.Equal("signs", entry.EntityType);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var checkIn = await AttendanceApi.RecordAsync(e1003, "ShiftIn", "token-checkin-ar02", 13.757, 100.502, 10);

        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        var attendance = await AdminSetupApi.ReadAsync(factory, db => db.ShiftAttendances.AsNoTracking().SingleAsync());
        Assert.True(attendance.WithinRadius); // before the capture AR02-IN gave no verdict
    }

    [Fact]
    public async Task A_map_pick_has_no_accuracy_and_stays_unconfirmed()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await LocateAsync(factory, admin, "AR02-01", 13.757, 100.502, 25, "Map");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sign = (await response.Content.ReadFromJsonAsync<AdminSignModel>())!;
        Assert.Equal("Map", sign.LocationSource);
        Assert.Null(sign.LocationAccuracyM);
    }

    [Fact]
    public async Task A_map_pick_cannot_replace_a_location_captured_on_site()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await LocateAsync(factory, admin, "AR01-IN", 13.757, 100.502, null, "Map");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Theory]
    [InlineData(13.757, 100.502, 10.0, null)]     // no source
    [InlineData(null, 100.502, 10.0, "Site")]     // no latitude
    [InlineData(13.757, 100.502, null, "Site")]   // a capture at the sign has an accuracy
    [InlineData(200.0, 100.502, 10.0, "Site")]    // impossible latitude
    public async Task Incomplete_locations_are_refused(double? latitude, double? longitude, double? accuracyM, string? source)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await LocateAsync(factory, admin, "AR02-IN", latitude, longitude, accuracyM, source);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Radius_changes_are_bounded_and_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var path = $"/api/admin/signs/{await AdminSetupApi.SignIdAsync(factory, "AR01-01")}/radius";

        var tooSmall = await admin.PutAsJsonAsync(path, new { radiusM = 9 });
        var tooLarge = await admin.PutAsJsonAsync(path, new { radiusM = 501 });
        var changed = await admin.PutAsJsonAsync(path, new { radiusM = 80 });
        var same = await admin.PutAsJsonAsync(path, new { radiusM = 80 });

        Assert.Equal(HttpStatusCode.BadRequest, tooSmall.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(80, (await changed.Content.ReadFromJsonAsync<AdminSignModel>())!.RadiusM);
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("SIGN_RADIUS", entry.Action);
        Assert.Contains("50", entry.BeforeJson);
        Assert.Contains("80", entry.AfterJson);
    }

    [Fact]
    public async Task A_new_qr_token_retires_the_printed_sign_and_the_log_never_holds_tokens()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));

        var response = await admin.PostAsync($"/api/admin/signs/{await AdminSetupApi.SignIdAsync(factory, "AR01-01")}/regenerate-token", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sign = (await response.Content.ReadFromJsonAsync<AdminSignModel>())!;
        Assert.True(Guid.TryParse(sign.QrToken, out _));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/service-points/by-token/token-restroom-m1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/service-points/by-token/{sign.QrToken}")).StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("QR_REGENERATE", entry.Action);
        Assert.DoesNotContain("token-restroom-m1", entry.BeforeJson);
        Assert.DoesNotContain(sign.QrToken, entry.AfterJson);
    }

    [Fact]
    public async Task A_deactivated_points_sign_gets_no_new_qr_token()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await admin.PostAsync($"/api/admin/points/{await AdminSetupApi.PointIdAsync(factory, "AR01-01")}/deactivate", null);

        var response = await admin.PostAsync($"/api/admin/signs/{await AdminSetupApi.SignIdAsync(factory, "AR01-01")}/regenerate-token", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cleaners_cannot_move_signs()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await LocateAsync(factory, cleaner, "AR01-01", 13.757, 100.502, 10, "Site");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~SignAdminEndpointTests"`
Expected: FAIL because the routes return 404/405.

- [ ] **Step 3: Add the requests**

Append to `backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs`:

```csharp
/// <summary>
/// facility-0045: Site = the median of 30 s of GPS readings taken standing at the sign (the page computes it) with its accuracy;
/// Map = picked on a map, no accuracy, shown as "ยังไม่ยืนยันหน้างาน".
/// </summary>
public record SignLocationRequest(double? Latitude, double? Longitude, double? AccuracyM, LocationSource? Source);

public record SignRadiusRequest(int? RadiusM);
```

- [ ] **Step 4: Write the sign endpoints and map them**

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminSignEndpoints.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Signs;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0045, 0038, 0006: where a sign stands, how far a scan may be from it, and its QR Token. Every change is logged.</summary>
public static class AdminSignEndpoints
{
    /// <summary>Below 10 m is finer than a phone's indoor fix, so every scan would be flagged; 500 m covers several buildings.</summary>
    public const short MinRadiusM = 10;

    public const short MaxRadiusM = 500;

    public static IEndpointRouteBuilder MapAdminSignEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/signs").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapPut("/{id:int}/location", SetLocationAsync);
        admin.MapPut("/{id:int}/radius", SetRadiusAsync);
        admin.MapPost("/{id:int}/regenerate-token", RegenerateTokenAsync);
        return app;
    }

    private static async Task<IResult> SetLocationAsync(int id, SignLocationRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.Source is not { } source || !Enum.IsDefined(source))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุว่าพิกัดมาจากหน้างาน (Site) หรือแผนที่ (Map)");
        }

        // A map pick has no accuracy; only a capture at the sign has one
        var accuracy = source == LocationSource.Map ? 0 : request.AccuracyM;
        if (!GpsInput.TryRead(request.Latitude, request.Longitude, accuracy, out var gps, out var gpsError))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, gpsError);
        }

        var sign = await db.Signs.FirstOrDefaultAsync(s => s.Id == id);
        if (sign is null)
        {
            return SignNotFound();
        }

        // facility-0045 rule 3: the map is the fallback before going on site, not a correction of a site capture
        if (source == LocationSource.Map && sign.LocationSource == LocationSource.Site)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "ป้ายนี้ยืนยันพิกัดที่หน้างานแล้ว ถ้าจะแก้ให้เก็บใหม่ที่หน้างาน");
        }

        var before = LocationSnapshot(sign);
        var now = clock.GetUtcNow().UtcDateTime;
        sign.Latitude = Math.Round((decimal)gps.Latitude, 6);
        sign.Longitude = Math.Round((decimal)gps.Longitude, 6);
        sign.LocationAccuracyM = source == LocationSource.Site ? (short)Math.Min(Math.Ceiling(gps.AccuracyM), short.MaxValue) : null;
        sign.LocationSource = source;
        sign.LocatedAt = now;
        AuditTrail.Add(
            db, admin.Id, now, "SIGN_LOCATION", "signs", sign.Id,
            source == LocationSource.Site
                ? $"เก็บพิกัดป้าย {sign.Code} ที่หน้างาน ±{sign.LocationAccuracyM} ม."
                : $"ปักพิกัดป้าย {sign.Code} จากแผนที่ (ยังไม่ยืนยันหน้างาน)",
            before, after: LocationSnapshot(sign));
        await db.SaveChangesAsync();

        return Results.Ok(AdminSetupView.ToDto(sign));
    }

    /// <summary>facility-0038. Applies to later scans; verdicts already stored stay as they were.</summary>
    private static async Task<IResult> SetRadiusAsync(int id, SignRadiusRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.RadiusM is not { } radius || radius < MinRadiusM || radius > MaxRadiusM)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, $"รัศมีต้องอยู่ระหว่าง {MinRadiusM}–{MaxRadiusM} เมตร");
        }

        var sign = await db.Signs.FirstOrDefaultAsync(s => s.Id == id);
        if (sign is null)
        {
            return SignNotFound();
        }

        if (sign.RadiusM != radius)
        {
            var old = sign.RadiusM;
            sign.RadiusM = (short)radius;
            AuditTrail.Add(
                db, admin.Id, clock.GetUtcNow().UtcDateTime, "SIGN_RADIUS", "signs", sign.Id,
                $"เปลี่ยนรัศมีป้าย {sign.Code} {old} → {radius} ม.",
                before: new { RadiusM = old }, after: new { RadiusM = radius });
            await db.SaveChangesAsync();
        }

        return Results.Ok(AdminSetupView.ToDto(sign));
    }

    /// <summary>facility-0006/0021: the printed sign stops working at once; scan history is untouched.</summary>
    private static async Task<IResult> RegenerateTokenAsync(int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var sign = await db.Signs.Include(s => s.Area).Include(s => s.ServicePoint).FirstOrDefaultAsync(s => s.Id == id);
        if (sign is null)
        {
            return SignNotFound();
        }

        // facility-0021 rule 4: no new QR for a deactivated point (or Area)
        if (sign.Area is not { IsActive: true } || sign.ServicePoint is { IsActive: false })
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงออก QR ใหม่ได้");
        }

        var issuedBefore = DateTime.SpecifyKind(sign.QrIssuedAt, DateTimeKind.Utc);
        var now = clock.GetUtcNow().UtcDateTime;
        sign.QrToken = SignCodes.NewQrToken();
        sign.QrIssuedAt = now;
        // The token itself never enters the log (facility-0060: tokens come only from the points and print pages)
        AuditTrail.Add(
            db, admin.Id, now, "QR_REGENERATE", "signs", sign.Id,
            $"ออก QR ใหม่ของป้าย {sign.Code} ป้ายเดิมสแกนไม่ได้แล้ว",
            before: new { QrIssuedAt = issuedBefore }, after: new { QrIssuedAt = now });
        await db.SaveChangesAsync();

        return Results.Ok(AdminSetupView.ToDto(sign));
    }

    private static object LocationSnapshot(Sign sign) => new
    {
        sign.Latitude,
        sign.Longitude,
        sign.LocationAccuracyM,
        LocationSource = sign.LocationSource?.ToString(),
    };

    private static IResult SignNotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบป้ายนี้");
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, add after `app.MapAdminPointEndpoints();`:

```csharp
app.MapAdminSignEndpoints();
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~SignAdminEndpointTests"`
Expected: PASS, 11 tests. Then `dotnet test backend/FacilityRealtime.slnx`: 0 failures.

- [ ] **Step 6: Update the docs**

In `docs/architecture.md`, in the component diagram, replace the `Ep` node line with:

```
            Ep["Endpoints/*<br/>/api/service-points (Admin) | /api/scan-records | /api/attendance | /api/my-work | /api/me<br/>/api/admin/cover-assignments | /api/admin/areas | points | signs | buildings | cleaners"]
```

In `README.md`, add this paragraph right after the Swagger paragraph that ends with "(ADR facility-0069)":

```markdown
ตั้งค่า Area ใน Swagger (login เป็น `admin`, ADR facility-0045):

- `GET /api/admin/areas/{id}` ดูป้ายทุกป้ายพร้อม QR Token, พิกัด และช่วงรอบ
- `POST /api/admin/areas` เพิ่ม Area ระบบสร้างป้ายลงเวลา `<รหัส>-IN` ให้เอง
- `POST /api/admin/areas/{areaId}/points` เพิ่มจุดพร้อมช่วงรอบ เช่น `{ "shift": "Day", "start": "07:00", "end": "09:00" }`
  - ช่วงรอบห้ามซ้อนกัน ต้องอยู่ในกะ และห้ามจบตรง 19:00 หรือ 07:00 (ADR facility-0047)
- `PUT /api/admin/signs/{id}/location` บันทึกพิกัดป้าย
- `POST /api/admin/signs/{id}/regenerate-token` ออก QR ใหม่
- ทุกการแก้ลงตาราง `audit_log`
```

- [ ] **Step 7: Commit**

```bash
git add backend/src/FacilityRealtime.Api/DTOs/AdminSetupDtos.cs backend/src/FacilityRealtime.Api/Endpoints/AdminSignEndpoints.cs backend/src/FacilityRealtime.Api/Program.cs backend/tests/FacilityRealtime.ApiTests/Admin/SignAdminEndpointTests.cs docs/architecture.md README.md
git commit -m "feat(backend): Admin captures sign locations, sets radius and issues new QR tokens, all logged (facility-0045, 0038, 0006)"
```

---

## After the last task

- `dotnet test backend/FacilityRealtime.slnx` passes with 0 failures. Both MySQL tests (`MySqlReadTests`, `MySqlAreaCleanerTests`) pass with `FACILITY_MYSQL_TEST` set to a throwaway database, which is dropped afterwards.
- Run the API in Development against a throwaway MySQL database and try this in Swagger as `admin`:
  1. Create Area `AR03` with an unassigned Cleaner. You need one: there is no accounts page yet, so add it in SQL on the throwaway database or skip the Cleaner.
  2. Add a point with windows `07:00–09:00` and `17:00–19:00`. The second must be refused because it ends at the shift change.
  3. Capture `AR02-IN`'s location.
  4. Issue a new QR for `AR01-02` and confirm that `token-restroom-f1` now gives 404.
  5. Deactivate `AR02` and confirm it disappears from `/api/service-points`.
- In the PR, list the 8 "Decisions made while planning" for the owner to confirm.
- **Next plans:**
  - Admin attendance and alerts: D2 board and corrections, D3 alerts with `alert_reviews` and "assign cover" from a Blocked Scan, D9 audit log page.
  - Admin accounts and Excel export: D6, D8.
  - Supervisor inspection.
  - The frontend.
