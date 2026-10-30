using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Inspections;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

public record InspectionSummaryRowModel(
    int SupervisorId, string EmployeeId, string DisplayName, string BuildingCode, string Shift,
    int PointsInspected, int PointsTotal, int Submitted, int SubmittedInspected, int NotInspected, int Inspections, int Reworks, int GpsFlagged)
{
    public List<UninspectedPointModel> UninspectedPoints { get; init; } = [];
}

public record UninspectedPointModel(int PointId, string AreaCode, string PointName, string SignCode);

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
        Assert.Equal(new[] { "2000001", "S2002" }, summary.Rows.Select(r => r.EmployeeId));
        var day = summary.Rows[0];
        Assert.Equal(("A", "Day"), (day.BuildingCode, day.Shift));
        Assert.Equal(
            new[] { 2, 3, 3, 2, 1, 2, 1, 1 },
            new[] { day.PointsInspected, day.PointsTotal, day.Submitted, day.SubmittedInspected, day.NotInspected, day.Inspections, day.Reworks, day.GpsFlagged });
        Assert.Equal(["AR01-02"], day.UninspectedPoints.Select(p => p.SignCode));
        var night = summary.Rows[1];
        Assert.Equal(
            new[] { 0, 2, 0, 0, 0, 0, 0, 0 },
            new[] { night.PointsInspected, night.PointsTotal, night.Submitted, night.SubmittedInspected, night.NotInspected, night.Inspections, night.Reworks, night.GpsFlagged });
        Assert.Equal(["AR01-01", "AR01-02"], night.UninspectedPoints.Select(p => p.SignCode));
        Assert.All(summary.Rows, row => Assert.Equal(row.PointsTotal - row.PointsInspected, row.UninspectedPoints.Count));
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

        Assert.Contains("2000001", summary.Rows.Select(r => r.EmployeeId));
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

        Assert.Equal(0, one.Rows.Single(r => r.EmployeeId == "2000001").Inspections);
        Assert.Equal(1, two.Rows.Single(r => r.EmployeeId == "2000001").Inspections);
        Assert.Equal(3, one.Rows.Single(r => r.EmployeeId == "2000001").UninspectedPoints.Count);
        Assert.Equal(2, two.Rows.Single(r => r.EmployeeId == "2000001").UninspectedPoints.Count);
    }

    [Fact]
    public async Task A_cover_inspection_in_another_building_counts_under_a_building_filter()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await InspectionApi.AddBuildingBAsync(factory);
        var scan = await TestData.AddScanAsync(factory, InspectionApi.BuildingBPoint, ThaiClock.At(8, 8, 30));
        await TestData.AddInspectionAsync(factory, scan, InspectionResult.Passed, ThaiClock.At(8, 8, 40), supervisorEmployeeId: "2000001");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");

        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>($"{Path}?from=2026-10-08&to=2026-10-08&buildingId={buildingA}"))!;

        var row = summary.Rows.Single(r => r.EmployeeId == "2000001");
        Assert.Equal(1, row.Inspections);
        Assert.Equal(0, row.PointsInspected);
        Assert.Equal(3, row.UninspectedPoints.Count);
        Assert.All(row.UninspectedPoints, point => Assert.StartsWith("AR", point.AreaCode));
    }

    [Fact]
    public async Task Work_submitted_after_the_shift_end_is_left_out_of_the_beat()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory); // log in before the clock moves
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.BackdateSetupAsync(factory);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 19, 20));
        var submit = new { qrToken = InspectionApi.MenRestroom, status = "Normal", latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10 };
        var created = await cleaner.PostAsJsonAsync("/api/scan-records", submit);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        factory.Clock.SetUtcNow(ThaiClock.At(9, 12, 0));

        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>($"{Path}?from=2026-10-08&to=2026-10-08"))!;

        var row = summary.Rows.Single(r => r.EmployeeId == "2000001");
        Assert.Equal((0, 0, 0), (row.Submitted, row.SubmittedInspected, row.NotInspected));
    }

    [Fact]
    public async Task Point_lists_are_safe_sorted_and_exclude_only_own_inspections_in_the_own_shift()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddSupervisorAsync(factory, "S2002", "0820000002", "A", Shift.Night);
        await TestData.AddPointAsync(factory, "AR01", "AR01-09", "token-summary-extra-nine");
        await TestData.AddPointAsync(factory, "AR01", "AR01-00", "token-summary-extra-zero");
        var another = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, another, InspectionResult.Passed, ThaiClock.At(8, 8, 20), supervisorEmployeeId: "S2002");
        var ownWrongShift = await TestData.AddScanAsync(factory, "token-restroom-f1", ThaiClock.At(8, 20, 10));
        await TestData.AddInspectionAsync(factory, ownWrongShift, InspectionResult.Passed, ThaiClock.At(8, 20, 20));

        var response = await admin.GetAsync($"{Path}?from=2026-10-08&to=2026-10-08");
        var summary = (await response.Content.ReadFromJsonAsync<InspectionSummaryModel>())!;
        var day = summary.Rows.Single(r => r.EmployeeId == "2000001");
        Assert.Equal((0, 5, 1), (day.PointsInspected, day.PointsTotal, day.Inspections));
        Assert.Equal(["AR01-00", "AR01-01", "AR01-02", "AR01-09", "AR02-01"], day.UninspectedPoints.Select(p => p.SignCode));
        Assert.All(summary.Rows, row => Assert.Equal(row.PointsTotal - row.PointsInspected, row.UninspectedPoints.Count));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var row in json.RootElement.GetProperty("rows").EnumerateArray())
        {
            foreach (var point in row.GetProperty("uninspectedPoints").EnumerateArray())
            {
                Assert.Equal(["areaCode", "pointId", "pointName", "signCode"], point.EnumerateObject().Select(p => p.Name).Order());
                Assert.False(string.IsNullOrWhiteSpace(point.GetProperty("pointName").GetString()));
            }
        }
    }

    [Theory]
    [InlineData("point", 2)]
    [InlineData("area", 1)]
    [InlineData("all", 0)]
    public async Task Inactive_points_and_areas_leave_the_list_and_zero_point_beats_are_empty(string deactivate, int expected)
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await factory.WithDbAsync(async db =>
        {
            if (deactivate == "point")
            {
                (await db.Signs.Include(s => s.ServicePoint).SingleAsync(s => s.QrToken == "token-restroom-m1")).ServicePoint!.IsActive = false;
            }
            else
            {
                var areas = await db.Areas.Where(a => deactivate == "all" || a.Code == "AR01").ToListAsync();
                foreach (var area in areas) area.IsActive = false;
            }
            await db.SaveChangesAsync();
        });
        var summary = (await admin.GetFromJsonAsync<InspectionSummaryModel>(Path))!;
        var row = Assert.Single(summary.Rows);
        Assert.Equal((0, expected), (row.PointsInspected, row.PointsTotal));
        Assert.Equal(expected, row.UninspectedPoints.Count);
        Assert.Equal(row.PointsTotal - row.PointsInspected, row.UninspectedPoints.Count);
    }

    [Theory]
    [InlineData("cleaner")]
    [InlineData("supervisor")]
    public async Task Only_admins_see_the_summary(string who)
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = who == "cleaner"
            ? await AuthApi.LoggedInAsync(factory)
            : await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
