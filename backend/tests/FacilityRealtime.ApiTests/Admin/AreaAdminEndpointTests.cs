using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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
        Assert.Equal("ไม่พบ Area นี้", await AdminSetupApi.MessageAsync(response));
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

    [Fact]
    public async Task Inactive_rows_are_skipped_and_map_picked_signs_count_as_unconfirmed()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var point2 = await db.Signs.Where(s => s.Code == "AR01-02").Select(s => s.ServicePoint!).SingleAsync();
            point2.IsActive = false;
            (await db.Signs.SingleAsync(s => s.Code == "AR01-01")).LocationSource = LocationSource.Map;
            (await db.Signs.SingleAsync(s => s.Code == "AR01-02")).LocationSource = LocationSource.Map; // would count, were its point not inactive
            (await db.Users.SingleAsync(u => u.EmployeeId == "E1002")).IsActive = false;
            db.Buildings.Add(new Building { Code = "Z", Name = "ตึกปิด", IsActive = false, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        var area1 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var summary = (await admin.GetFromJsonAsync<List<AreaSummaryModel>>(Areas))!.Single(a => a.Code == "AR01");
        var detail = (await admin.GetFromJsonAsync<AreaDetailModel>($"{Areas}/{area1}"))!;
        var buildings = (await admin.GetFromJsonAsync<List<BuildingModel>>("/api/admin/buildings"))!;
        var cleaners = (await admin.GetFromJsonAsync<List<CleanerOptionModel>>("/api/admin/cleaners"))!;

        Assert.Equal(1, summary.ActivePointCount);
        Assert.Equal(1, summary.SignsNotConfirmedOnSite); // AR01-01 is only a map pick; AR01-02 is a map pick too but belongs to an inactive point; AR01-IN is on site
        Assert.Null(summary.NightCleaner);
        Assert.Equal(new[] { true, false }, detail.Points.Select(p => p.IsActive));
        Assert.DoesNotContain(buildings, b => b.Code == "Z");
        Assert.DoesNotContain(cleaners, c => c.EmployeeId == "E1002");
    }

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

    [Theory]
    [InlineData("PUT", "")]
    [InlineData("POST", "/deactivate")]
    [InlineData("POST", "/activate")]
    public async Task Unknown_areas_are_not_found(string method, string suffix)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var body = method == "PUT" ? JsonContent.Create(await Ar01FormAsync(factory, null, null)) : null;

        var response = await admin.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"{Areas}/9999{suffix}") { Content = body });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Supervisors_cannot_create_areas()
    {
        using var factory = new FacilityApiFactory();
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2001", "0820000001");

        var response = await supervisor.PostAsJsonAsync(Areas, new { code = "AR03", name = "Lobby", buildingId = 1, shiftPattern = "DayOnly" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
