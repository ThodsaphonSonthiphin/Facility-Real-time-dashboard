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
