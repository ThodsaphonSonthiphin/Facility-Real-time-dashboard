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
