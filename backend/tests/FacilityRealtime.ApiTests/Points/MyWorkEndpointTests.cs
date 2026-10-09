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
