using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Inspections;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

public record InspectionSummaryRowModel(
    int SupervisorId, string EmployeeId, string DisplayName, string BuildingCode, string Shift,
    int PointsInspected, int PointsTotal, int Submitted, int SubmittedInspected, int NotInspected, int Inspections, int Reworks, int GpsFlagged);

public record InspectionSummaryModel(DateOnly From, DateOnly To, List<InspectionSummaryRowModel> Rows);

/// <summary>facility-0048 rule 5, wireframe SupervisorSummary: the Admin's Inspection Summary tab.</summary>
public class InspectionSummaryEndpointTests
{
    private const string Path = "/api/admin/inspection-summary";

    private static async Task<(FacilityApiFactory Factory, HttpClient Admin)> StartAsync()
    {
        var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.BackdateSetupAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(9, 12, 0));
        return (factory, admin);
    }

    [Fact]
    public async Task Counts_each_supervisors_beat_and_own_inspections()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        var mens = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, mens, InspectionResult.Passed, ThaiClock.At(8, 8, 20), withinRadius: true);
        await TestData.AddScanAsync(factory, "token-restroom-f1", ThaiClock.At(8, 8, 15));
        var meeting = await TestData.AddScanAsync(factory, "token-meeting-room", ThaiClock.At(8, 8, 30));
        await TestData.AddInspectionAsync(factory, meeting, InspectionResult.Rework, ThaiClock.At(8, 8, 40), withinRadius: false, distanceM: 120);

        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>($"{Path}?from=2026-10-08&to=2026-10-08"))!;

        Assert.Equal(new DateOnly(2026, 10, 8), summary.From);
        Assert.Equal(new[] { "S2001", "S2002" }, summary.Rows.Select(r => r.EmployeeId));
        var day = summary.Rows[0];
        Assert.Equal(("A", "Day"), (day.BuildingCode, day.Shift));
        Assert.Equal(
            new[] { 2, 3, 3, 2, 1, 2, 1, 1 },
            new[] { day.PointsInspected, day.PointsTotal, day.Submitted, day.SubmittedInspected, day.NotInspected, day.Inspections, day.Reworks, day.GpsFlagged });
        var night = summary.Rows[1];
        Assert.Equal(
            new[] { 0, 2, 0, 0, 0, 0, 0, 0 },
            new[] { night.PointsInspected, night.PointsTotal, night.Submitted, night.SubmittedInspected, night.NotInspected, night.Inspections, night.Reworks, night.GpsFlagged });
    }

    [Fact]
    public async Task The_default_range_is_the_last_seven_shift_dates()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>(Path))!;

        Assert.Equal((new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 9)), (summary.From, summary.To));
    }

    [Theory]
    [InlineData("?from=2026-10-08")]
    [InlineData("?from=2026-10-09&to=2026-10-08")]
    [InlineData("?from=2026-09-01&to=2026-10-08")]
    [InlineData("?from=08-10-2026&to=2026-10-08")]
    public async Task A_bad_range_is_400(string query)
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        var response = await admin.GetAsync(Path + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task One_building_lists_only_its_supervisors()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddSupervisorAsync(factory, "S2003", "0820000003", "B", Shift.Day);
        var buildingB = await AdminSetupApi.BuildingIdAsync(factory, "B");

        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>($"{Path}?buildingId={buildingB}"))!;

        var row = Assert.Single(summary.Rows);
        Assert.Equal(("S2003", "B", 1), (row.EmployeeId, row.BuildingCode, row.PointsTotal));
    }

    [Fact]
    public async Task A_deactivated_supervisor_is_not_listed()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "S2002")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>($"{Path}?from=2026-10-08&to=2026-10-08"))!;

        Assert.Contains("S2001", summary.Rows.Select(r => r.EmployeeId));
        Assert.DoesNotContain("S2002", summary.Rows.Select(r => r.EmployeeId));
    }

    [Fact]
    public async Task The_date_range_counts_only_its_own_shift_dates()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        var earlier = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(7, 8, 10));
        await TestData.AddInspectionAsync(factory, earlier, InspectionResult.Passed, ThaiClock.At(7, 8, 20), withinRadius: true);

        var one = (await admin.GetFromJsonAsync<InspectionSummaryModel>($"{Path}?from=2026-10-08&to=2026-10-08"))!;
        var two = (await admin.GetFromJsonAsync<InspectionSummaryModel>($"{Path}?from=2026-10-07&to=2026-10-08"))!;

        Assert.Equal(0, one.Rows.Single(r => r.EmployeeId == "S2001").Inspections);
        Assert.Equal(1, two.Rows.Single(r => r.EmployeeId == "S2001").Inspections);
    }

    [Fact]
    public async Task A_cover_inspection_in_another_building_counts_under_a_building_filter()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await InspectionApi.AddBuildingBAsync(factory);
        var scan = await TestData.AddScanAsync(factory, InspectionApi.BuildingBPoint, ThaiClock.At(8, 8, 30));
        await TestData.AddInspectionAsync(factory, scan, InspectionResult.Passed, ThaiClock.At(8, 8, 40), supervisorEmployeeId: "S2001");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");

        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>($"{Path}?from=2026-10-08&to=2026-10-08&buildingId={buildingA}"))!;

        Assert.Equal(1, summary.Rows.Single(r => r.EmployeeId == "S2001").Inspections);
    }

    [Theory]
    [InlineData("cleaner")]
    [InlineData("supervisor")]
    public async Task Only_admins_see_the_summary(string who)
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = who == "cleaner"
            ? await AuthApi.LoggedInAsync(factory)
            : await AuthApi.LoggedInAsEmployeeAsync(factory, "S2001", "0820000001");

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
