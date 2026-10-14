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
    public async Task A_point_edit_without_the_window_list_is_refused_and_keeps_every_window()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var id = await AdminSetupApi.PointIdAsync(factory, "AR01-01");

        var response = await admin.PutAsJsonAsync($"/api/admin/points/{id}", new { name = "ห้องน้ำชาย ชั้น 1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ต้องส่งรายการช่วงรอบ", await AdminSetupApi.MessageAsync(response));
        Assert.Equal(4, await AdminSetupApi.ReadAsync(factory, db => db.PointRoundWindows.CountAsync(w => w.ServicePointId == id)));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
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
