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
