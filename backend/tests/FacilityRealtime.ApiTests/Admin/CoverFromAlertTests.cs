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

    /// <summary>A Blocked Scan at AR02's check-in sign written straight to the database, for a Cleaner who cannot log in.</summary>
    private static Task AddBlockedAtAr02Async(FacilityApiFactory factory, string employeeId, DateTimeOffset at) =>
        factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var sign = await db.Signs.SingleAsync(s => s.QrToken == "token-checkin-ar02");
            db.BlockedScans.Add(new BlockedScan { UserId = user.Id, SignId = sign.Id, Reason = BlockReason.OtherArea, ScannedAt = at.UtcDateTime });
            await db.SaveChangesAsync();
        });

    private static Task<HttpResponseMessage> AssignAsync(HttpClient admin, int userId, int areaId, long? blockedScanId, DateOnly? shiftDate = null) =>
        admin.PostAsJsonAsync("/api/admin/cover-assignments", new { userId, areaId, shiftDate = shiftDate ?? Oct8, shift = "Day", blockedScanId });

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

    [Fact]
    public async Task The_blocked_scan_must_belong_to_the_cleaner_being_assigned()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddAreaAsync(factory, "AR03");
        var e1009 = await TestData.AddCleanerAsync(factory, "E1009", Shift.Day, "AR03");
        var blockedId = await BlockedAtAr02Async(factory, cleaner);   // E1001's scan
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 30));

        var response = await AssignAsync(admin, e1009, await AdminSetupApi.AreaIdAsync(factory, "AR02"), blockedId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.CoverAssignments.ToListAsync()));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task The_blocked_scan_must_be_in_the_shift_of_the_cover()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var blockedId = await BlockedAtAr02Async(factory, cleaner);   // Oct 8 Day
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 30));

        var response = await AssignAsync(
            admin, await AdminSetupApi.UserIdAsync(factory, "E1001"), await AdminSetupApi.AreaIdAsync(factory, "AR02"), blockedId, new DateOnly(2026, 10, 9));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.CoverAssignments.ToListAsync()));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Candidates_leave_out_a_deactivated_cleaner()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddAreaAsync(factory, "AR03");
        await TestData.AddCleanerAsync(factory, "E1009", Shift.Day, "AR03");
        var query = $"?areaId={await AdminSetupApi.AreaIdAsync(factory, "AR02")}&shiftDate=2026-10-08&shift=Day";
        Assert.Contains("E1009", (await CandidatesAsync(admin, query)).Select(c => c.EmployeeId));

        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "E1009")).IsActive = false;
            await db.SaveChangesAsync();
        });

        Assert.DoesNotContain("E1009", (await CandidatesAsync(admin, query)).Select(c => c.EmployeeId));
    }

    [Fact]
    public async Task Candidates_answer_404_for_a_deactivated_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Code == "AR02")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await admin.GetAsync($"{CandidatesPath}?areaId={ar02}&shiftDate=2026-10-08&shift=Day");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_scan_from_another_shift_does_not_count_as_scanning_this_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddAreaAsync(factory, "AR03");
        await TestData.AddCleanerAsync(factory, "E1009", Shift.Day, "AR03");
        await AddBlockedAtAr02Async(factory, "E1009", ThaiClock.At(7, 10, 0));   // yesterday's Day shift
        var query = $"?areaId={await AdminSetupApi.AreaIdAsync(factory, "AR02")}&shiftDate=2026-10-08&shift=Day";

        var candidate = Assert.Single(await CandidatesAsync(admin, query), c => c.EmployeeId == "E1009");

        Assert.Null(candidate.ScannedThisAreaAt);
    }

    [Fact]
    public async Task Candidates_are_ordered_latest_scan_then_earliest_check_in_then_employee_id()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        foreach (var code in new[] { "AR03", "AR04", "AR05", "AR06", "AR07", "AR08" })
        {
            await AddAreaAsync(factory, code);
        }

        // Added in an order that differs from the expected one, so only the rules can produce it
        await TestData.AddCleanerAsync(factory, "E1011", Shift.Day, "AR03");   // scanned 08:00, checked in 06:30
        await TestData.AddCleanerAsync(factory, "E1012", Shift.Day, "AR04");   // scanned 09:00
        await TestData.AddCleanerAsync(factory, "E1013", Shift.Day, "AR05");   // checked in 06:58
        await TestData.AddCleanerAsync(factory, "E1014", Shift.Day, "AR06");   // checked in 06:40
        await TestData.AddCleanerAsync(factory, "E1016", Shift.Day, "AR07");   // neither
        await TestData.AddCleanerAsync(factory, "E1015", Shift.Day, "AR08");   // neither
        await AddBlockedAtAr02Async(factory, "E1011", ThaiClock.At(8, 8, 0));
        await AddBlockedAtAr02Async(factory, "E1012", ThaiClock.At(8, 9, 0));
        await AddShiftInAsync(factory, "E1011", ThaiClock.At(8, 6, 30));
        await AddShiftInAsync(factory, "E1013", ThaiClock.At(8, 6, 58));
        await AddShiftInAsync(factory, "E1014", ThaiClock.At(8, 6, 40));
        var query = $"?areaId={await AdminSetupApi.AreaIdAsync(factory, "AR02")}&shiftDate=2026-10-08&shift=Day";

        var candidates = await CandidatesAsync(admin, query);

        // E1001 is the seeded AR01 Cleaner with neither a scan nor a check-in
        Assert.Equal(
            new[] { "E1012", "E1011", "E1014", "E1013", "E1001", "E1015", "E1016" },
            candidates.Select(c => c.EmployeeId));
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
