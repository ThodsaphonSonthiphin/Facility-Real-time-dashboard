using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Attendance;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.ApiTests.Points;
using JanitorialManagementDashboard.ApiTests.Scheduling;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Admin;

public class OverviewEndpointTests
{
    private static readonly string[] ForbiddenJsonPropertyNames =
    [
        "qrToken", "secretHash", "password", "phone",
        "latitude", "longitude", "accuracyM", "distanceM", "withinRadius",
        "employeeId", "username",
    ];

    [Fact]
    public async Task Only_Admin_may_read_overview()
    {
        using var factory = new JanitorialApiFactory();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateApiClient().GetAsync("/api/admin/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateApiClient().GetAsync("/api/admin/overview/areas?group=entered")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateApiClient().GetAsync("/api/admin/overview/areas/1")).StatusCode);

        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cleaner.GetAsync("/api/admin/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cleaner.GetAsync("/api/admin/overview/areas?group=entered")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cleaner.GetAsync("/api/admin/overview/areas/1")).StatusCode);

        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await supervisor.GetAsync("/api/admin/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await supervisor.GetAsync("/api/admin/overview/areas?group=entered")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await supervisor.GetAsync("/api/admin/overview/areas/1")).StatusCode);
    }

    [Fact]
    public async Task Inactive_admin_with_live_token_is_refused_on_all_overview_routes()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));
        var areaId = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Username == "admin");
            user.IsActive = false;
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/admin/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/admin/overview/areas?group=entered")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync($"/api/admin/overview/areas/{areaId}")).StatusCode);
    }

    [Fact]
    public async Task A_date_without_shift_is_rejected_instead_of_reading_current_slot()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var result = await admin.GetAsync("/api/admin/overview?shiftDate=2026-11-01");
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/overview?shift=Day")]
    [InlineData("/api/admin/overview?shiftDate=2026/10/08&shift=Day")]
    [InlineData("/api/admin/overview?shiftDate=2026-02-30&shift=Day")]
    [InlineData("/api/admin/overview?shiftDate=&shift=Day")]
    [InlineData("/api/admin/overview?shiftDate=2026-10-08&shift=Evening")]
    [InlineData("/api/admin/overview?shiftDate=2026-10-08&shift=1")]
    [InlineData("/api/admin/overview?shiftDate=2026-10-08&shift=Day&buildingId=0")]
    [InlineData("/api/admin/overview?shiftDate=2026-10-08&shift=Day&buildingId=-2")]
    [InlineData("/api/admin/overview?shiftDate=2026-10-08&shift=Day&buildingId=abc")]
    [InlineData("/api/admin/overview?shiftDate=2026-10-08&shiftDate=2026-10-09&shift=Day")]
    [InlineData("/api/admin/overview?shiftDate=2026-10-08&shift=Day&shift=Night")]
    [InlineData("/api/admin/overview?shiftDate=2026-10-08&shift=Day&buildingId=1&buildingId=2")]
    [InlineData("/api/admin/overview/areas")]
    [InlineData("/api/admin/overview/areas?group=unknown")]
    [InlineData("/api/admin/overview/areas?group=")]
    [InlineData("/api/admin/overview/areas?group=entered&group=covered")]
    [InlineData("/api/admin/overview/areas?group=entered&page=0")]
    [InlineData("/api/admin/overview/areas?group=entered&page=-1")]
    [InlineData("/api/admin/overview/areas?group=entered&page=abc")]
    [InlineData("/api/admin/overview/areas?group=entered&page=1&page=2")]
    [InlineData("/api/admin/overview/areas?group=entered&pageSize=0")]
    [InlineData("/api/admin/overview/areas?group=entered&pageSize=51")]
    [InlineData("/api/admin/overview/areas?group=entered&pageSize=5&pageSize=10")]
    [InlineData("/api/admin/overview/areas/0")]
    [InlineData("/api/admin/overview/areas/-5")]
    [InlineData("/api/admin/overview/areas/1?group=notAGroup")]
    [InlineData("/api/admin/overview/areas/1?roundKey=malformed")]
    [InlineData("/api/admin/overview/areas/1?roundKey=")]
    [InlineData("/api/admin/overview/areas/1?roundKey=2026-10-08:Evening:1:1:1:0800-0900")]
    [InlineData("/api/admin/overview/areas/1?roundKey=2026-10-08:Day:0:1:1:0800-0900")]
    public async Task Invalid_and_duplicate_query_parameters_return_bad_request(string path)
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var response = await admin.GetAsync(path);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Future_Thai_date_is_rejected_while_later_shift_today_returns_notStarted()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        // 10:00 Thai on 2026-10-08: 2026-10-09 is future (400); 2026-10-08 Night is later today (notStarted).
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.GetAsync("/api/admin/overview?shiftDate=2026-10-09&shift=Day")).StatusCode);

        var nightResponse = await admin.GetAsync("/api/admin/overview?shiftDate=2026-10-08&shift=Night");
        Assert.Equal(HttpStatusCode.OK, nightResponse.StatusCode);
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/admin/overview/areas/{ar02}?shiftDate=2026-10-08&shift=Night")).StatusCode);
        var nightRaw = await nightResponse.Content.ReadAsStringAsync();
        AssertSafeJsonProperties(nightRaw);
        AssertExplicitNullValues(nightRaw);

        using var doc = JsonDocument.Parse(nightRaw);
        var root = doc.RootElement;
        Assert.Equal("notStarted", root.GetProperty("context").GetProperty("phase").GetString());
        Assert.False(root.GetProperty("context").GetProperty("isClockCurrent").GetBoolean());
        Assert.Equal("notStarted", root.GetProperty("availability").GetProperty("entry").GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("entry").GetProperty("numerator").GetProperty("value").ValueKind);
        Assert.Equal(0, root.GetProperty("entry").GetProperty("numerator").GetProperty("observed").GetInt32());

        // 02:30 Thai on 2026-10-09: clock slot is 2026-10-08 Night; 2026-10-09 Day is later today (notStarted).
        factory.Clock.SetUtcNow(ThaiClock.At(9, 2, 30));
        var defaultNight = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview");
        Assert.Equal("2026-10-08", defaultNight.GetProperty("context").GetProperty("shiftDate").GetString());
        Assert.Equal("Night", defaultNight.GetProperty("context").GetProperty("shift").GetString());
        Assert.True(defaultNight.GetProperty("context").GetProperty("isClockCurrent").GetBoolean());
        Assert.Equal("running", defaultNight.GetProperty("context").GetProperty("phase").GetString());

        var futureDayToday = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview?shiftDate=2026-10-09&shift=Day");
        Assert.Equal("notStarted", futureDayToday.GetProperty("context").GetProperty("phase").GetString());
        Assert.False(futureDayToday.GetProperty("context").GetProperty("isClockCurrent").GetBoolean());
    }

    [Fact]
    public async Task Unknown_building_returns_404_instead_of_being_ignored()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var areaId = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/admin/overview?shiftDate=2026-10-08&shift=Day&buildingId=9999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/admin/overview/areas?shiftDate=2026-10-08&shift=Day&buildingId=9999&group=entered")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/admin/overview/areas/{areaId}?shiftDate=2026-10-08&shift=Day&buildingId=9999")).StatusCode);

        // Building created after evaluation instant is unknown in an earlier closed slot's temporal context.
        factory.Clock.SetUtcNow(ThaiClock.At(10, 9, 0));
        var createdBuilding = await admin.PostAsJsonAsync("/api/admin/buildings", new { code = "LATE", name = "ตึกใหม่" });
        createdBuilding.EnsureSuccessStatusCode();
        var lateId = (await createdBuilding.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/admin/overview?shiftDate=2026-10-08&shift=Day&buildingId={lateId}")).StatusCode);
    }

    [Fact]
    public async Task All_sixteen_groups_match_summary_list_pages_and_detail_with_paging_and_clamping()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (cleaner1, _, _) = await AuthApi.LoggedInAsync(factory);
        var (cleaner3, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000003", "0810000003");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        // Setup 8 Areas in Building A + 1 Area in Building B before moving clock or starting history.
        await TestData.AddBuildingAsync(factory, "B");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A");
        var ar04 = await TestData.AddAreaAsync(factory, "AR04", "A");
        var ar05 = await TestData.AddAreaAsync(factory, "AR05", "A");
        var ar06 = await TestData.AddAreaAsync(factory, "AR06", "A");
        var ar07 = await TestData.AddAreaAsync(factory, "AR07", "A"); // upcoming
        var ar08 = await TestData.AddAreaAsync(factory, "AR08", "A"); // noRounds
        var ar09 = await TestData.AddAreaAsync(factory, "AR09", "B"); // Building B
        await TestData.AddCleanerAsync(factory, "1000004", Shift.Day, "AR03", "0810000004");
        await TestData.AddCleanerAsync(factory, "1000005", Shift.Day, "AR04", "0810000005");
        var (cleaner4, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000004", "0810000004");
        var (cleaner5, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000005", "0810000005");

        var p3 = await AddPointAsync(factory, "AR03", "AR03-01", "tok-ar03-1", 8, 9);
        await AddPointAsync(factory, "AR04", "AR04-01", "tok-ar04-1", 8, 9);
        await AddPointAsync(factory, "AR05", "AR05-01", "tok-ar05-1", 8, 9);
        await AddPointAsync(factory, "AR06", "AR06-01", "tok-ar06-1", 8, 9);
        await AddPointAsync(factory, "AR07", "AR07-01", "tok-ar07-1", 15, 16);
        await AddPointAsync(factory, "AR09", "AR09-01", "tok-ar09-1", 8, 9);

        // Make AR01 have an earlier finalized round (07:00-09:00 superseded at 10:00) -> missedCleaning & missedInspection setup
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        // 07:10: cleaner1 checks in at AR01, covers AR03, scans AR01-01 (submitted + pending inspection + issue), scans AR03-01 (inspected Passed).
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 10));
        await AttendanceApi.CheckInAsync(factory, cleaner1, ThaiClock.At(8, 7, 10));
        var coverRes = await admin.PostAsJsonAsync("/api/admin/cover-assignments", new
        {
            userId = await AdminSetupApi.UserIdAsync(factory, "1000001"),
            areaId = ar04,
            shiftDate = "2026-10-08",
            shift = "Day",
        });
        coverRes.EnsureSuccessStatusCode();

        // Submit on AR01-01 -> Normal, then inspect Rework on AR01-02? Let's make AR01-01 Passed (so AR01 has passed) and AR01-02 uninspected.
        var scan1 = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 7, 20), CleaningStatus.Issue, "wet_floor", "น้ำรั่ว");
        await TestData.AddInspectionAsync(factory, scan1, InspectionResult.Passed, ThaiClock.At(8, 7, 30));

        // AR02-01 (08:00-10:00): submitted at 08:10, inspected Rework at 08:20 -> Rework & Inspected
        var scan2 = await TestData.AddScanAsync(factory, "token-meeting-room", ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, scan2, InspectionResult.Rework, ThaiClock.At(8, 8, 20));

        // AR03-01 (08:00-09:00): submitted at 08:15, NOT inspected -> PendingInspection + Submitted
        await TestData.AddScanAsync(factory, "tok-ar03-1", ThaiClock.At(8, 8, 15));

        // 09:30: replace AR04-01's 08:00-09:00 window (never submitted -> missedCleaning!) and AR05-01's 08:00-09:00 window (submitted, never inspected -> missedInspection!)
        var scan5 = await TestData.AddScanAsync(factory, "tok-ar05-1", ThaiClock.At(8, 8, 15));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 30));
        var p4 = await AdminSetupApi.PointIdAsync(factory, "AR04-01");
        var p5 = await AdminSetupApi.PointIdAsync(factory, "AR05-01");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/points/{p4}", new
        {
            name = "จุด AR04-01",
            roundWindows = new[] { new { shift = "Day", start = "10:00", end = "11:00" } },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/points/{p5}", new
        {
            name = "จุด AR05-01",
            roundWindows = new[] { new { shift = "Day", start = "10:00", end = "11:00" } },
        })).StatusCode);
        _ = (p3, scan5, supervisor, ar06, ar07, ar08, ar09);

        // Evaluate at 10:30 Thai (running; AR06 08:00-09:00 is overdue).
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 30));
        var summaryRes = await admin.GetAsync("/api/admin/overview?shiftDate=2026-10-08&shift=Day");
        Assert.Equal(HttpStatusCode.OK, summaryRes.StatusCode);
        var summaryJson = await summaryRes.Content.ReadAsStringAsync();
        AssertSafeJsonProperties(summaryJson);

        using var summaryDoc = JsonDocument.Parse(summaryJson);
        var summaryGroups = summaryDoc.RootElement.GetProperty("groups");

        foreach (var group in OverviewGroupKeys.All)
        {
            var expectedCount = summaryGroups.GetProperty(group).GetProperty("value").GetInt32();
            var collectedIds = new List<int>();
            var page = 1;
            while (true)
            {
                var listRes = await admin.GetAsync($"/api/admin/overview/areas?shiftDate=2026-10-08&shift=Day&group={group}&page={page}&pageSize=5");
                Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
                var listJson = await listRes.Content.ReadAsStringAsync();
                AssertSafeJsonProperties(listJson);

                using var listDoc = JsonDocument.Parse(listJson);
                var listRoot = listDoc.RootElement;
                Assert.Equal(expectedCount, listRoot.GetProperty("total").GetProperty("value").GetInt32());
                Assert.Equal(expectedCount, listRoot.GetProperty("summary").GetProperty("groups").GetProperty(group).GetProperty("value").GetInt32());

                var rows = listRoot.GetProperty("rows").EnumerateArray().ToArray();
                if (expectedCount == 0)
                {
                    Assert.Empty(rows);
                    Assert.Equal(1, listRoot.GetProperty("page").GetInt32());
                    break;
                }

                foreach (var row in rows)
                {
                    var areaId = row.GetProperty("areaId").GetInt32();
                    collectedIds.Add(areaId);
                    var detailRes = await admin.GetAsync($"/api/admin/overview/areas/{areaId}?shiftDate=2026-10-08&shift=Day&group={group}");
                    Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
                    var detailJson = await detailRes.Content.ReadAsStringAsync();
                    AssertSafeJsonProperties(detailJson);
                    using var detailDoc = JsonDocument.Parse(detailJson);
                    Assert.Equal(areaId, detailDoc.RootElement.GetProperty("area").GetProperty("areaId").GetInt32());
                    Assert.True(detailDoc.RootElement.GetProperty("matchesGroup").GetBoolean());
                }

                if (collectedIds.Count >= expectedCount || rows.Length == 0)
                {
                    break;
                }

                page++;
            }

            Assert.Equal(expectedCount, collectedIds.Distinct().Count());
            Assert.Equal(expectedCount, collectedIds.Count);
        }

        // Verify page 2 and out-of-range page clamping on notEntered (8 areas: 5 on page 1, 3 on page 2).
        var page2 = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview/areas?shiftDate=2026-10-08&shift=Day&group=notEntered&page=2&pageSize=5");
        Assert.Equal(2, page2.GetProperty("page").GetInt32());
        Assert.Equal(8, page2.GetProperty("total").GetProperty("value").GetInt32());
        Assert.Equal(3, page2.GetProperty("rows").GetArrayLength());

        var clamped = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview/areas?shiftDate=2026-10-08&shift=Day&group=notEntered&page=99&pageSize=5");
        Assert.Equal(2, clamped.GetProperty("page").GetInt32());
        Assert.Equal(8, clamped.GetProperty("total").GetProperty("value").GetInt32());
        Assert.Equal(3, clamped.GetProperty("rows").GetArrayLength());

        // Mutate facts in a separate API call: cleaner3 checks in for AR02 via /api/attendance!
        await AttendanceApi.CheckInAsync(factory, cleaner3, ThaiClock.At(8, 10, 35), "token-checkin-ar02");

        var refreshed = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview/areas?shiftDate=2026-10-08&shift=Day&group=notEntered&page=2&pageSize=5");
        Assert.Equal(7, refreshed.GetProperty("total").GetProperty("value").GetInt32());
        Assert.Equal(7, refreshed.GetProperty("summary").GetProperty("groups").GetProperty("notEntered").GetProperty("value").GetInt32());
        Assert.Equal(2, refreshed.GetProperty("rows").GetArrayLength());

        // Shrink notEntered from 7 -> 5 (page 2 -> clamps back to page 1 with all 5 remaining rows).
        await AttendanceApi.CheckInAsync(factory, cleaner4, ThaiClock.At(8, 10, 40), "token-checkin-ar03");
        await AttendanceApi.CheckInAsync(factory, cleaner5, ThaiClock.At(8, 10, 41), "token-checkin-ar04");
        var shrunk = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview/areas?shiftDate=2026-10-08&shift=Day&group=notEntered&page=2&pageSize=5");
        Assert.Equal(1, shrunk.GetProperty("page").GetInt32());
        Assert.Equal(5, shrunk.GetProperty("total").GetProperty("value").GetInt32());
        Assert.Equal(5, shrunk.GetProperty("rows").GetArrayLength());

        // Building breakdown matches building-filtered summary.
        var buildingB = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var buildingBSummary = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/overview?shiftDate=2026-10-08&shift=Day&buildingId={buildingB}");
        var fromWhole = summaryDoc.RootElement.GetProperty("buildings").EnumerateArray().Single(b => b.GetProperty("id").GetInt32() == buildingB);
        var fromFiltered = Assert.Single(buildingBSummary.GetProperty("buildings").EnumerateArray());
        Assert.Equal(fromWhole.GetProperty("entry").GetProperty("denominator").GetProperty("value").GetInt32(),
            fromFiltered.GetProperty("entry").GetProperty("denominator").GetProperty("value").GetInt32());
        Assert.Equal(buildingBSummary.GetProperty("groups").GetProperty("incomplete").GetProperty("value").GetInt32(),
            fromWhole.GetProperty("groups").GetProperty("incomplete").GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task Receiving_window_and_policy_restart_preserve_closed_boundary_without_reopening()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");

        // 19:30 Thai (receiving window: End=19:00, Close=20:00). AR01's 07:00-09:00 is finalized (1),
        // while AR02's single final round (08:00-10:00) is still editable and NOT in History yet.
        factory.Clock.SetUtcNow(ThaiClock.At(8, 19, 30));
        var receiving = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview?shiftDate=2026-10-08&shift=Day");
        Assert.Equal("receiving", receiving.GetProperty("context").GetProperty("phase").GetString());
        Assert.Equal(1, receiving.GetProperty("groups").GetProperty("missedCleaning").GetProperty("value").GetInt32());
        var ar02Receiving = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/overview/areas/{ar02}?shiftDate=2026-10-08&shift=Day&group=missedCleaning");
        Assert.Empty(ar02Receiving.GetProperty("rounds").EnumerateArray());

        // 20:00 Thai (exact inclusive close boundary): still receiving!
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 0));
        var atBoundary = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview?shiftDate=2026-10-08&shift=Day");
        Assert.Equal("receiving", atBoundary.GetProperty("context").GetProperty("phase").GetString());
        Assert.Equal(1, atBoundary.GetProperty("groups").GetProperty("missedCleaning").GetProperty("value").GetInt32());

        // 20:05 Thai: closed! Now AR02's final round appears in History (slotClosed), bringing missedCleaning to 2.
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 5));
        var closed = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview?shiftDate=2026-10-08&shift=Day");
        Assert.Equal("closed", closed.GetProperty("context").GetProperty("phase").GetString());
        Assert.Equal(ThaiClock.At(8, 20, 0).UtcDateTime, closed.GetProperty("context").GetProperty("closeUtc").GetDateTime());
        Assert.Equal(2, closed.GetProperty("groups").GetProperty("missedCleaning").GetProperty("value").GetInt32());
        var ar02Closed = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/overview/areas/{ar02}?shiftDate=2026-10-08&shift=Day&group=missedCleaning");
        var closedRound = Assert.Single(ar02Closed.GetProperty("rounds").EnumerateArray());
        Assert.Equal("slotClosed", closedRound.GetProperty("round").GetProperty("finalizedReason").GetString());

        // Policy restart at 21:00 changing ClosesMinutesAfterShift from 60 to 180 MUST NOT reopen 2026-10-08 Day.
        factory.Clock.SetUtcNow(ThaiClock.At(8, 21, 0));
        var settings = factory.Services.GetRequiredService<AttendanceSettings>();
        var originalCloses = settings.ClosesMinutesAfterShift;
        try
        {
            settings.ClosesMinutesAfterShift = 180;
            await AdminSetupApi.ReadAsync(factory,
                db => OverviewHistoryBootstrap.EnsureAsync(db, settings, factory.Clock, CancellationToken.None));

            factory.Clock.SetUtcNow(ThaiClock.At(8, 21, 30));
            var afterRestart = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview?shiftDate=2026-10-08&shift=Day");
            Assert.Equal("closed", afterRestart.GetProperty("context").GetProperty("phase").GetString());
            Assert.Equal(ThaiClock.At(8, 20, 0).UtcDateTime, afterRestart.GetProperty("context").GetProperty("closeUtc").GetDateTime());
        }
        finally
        {
            settings.ClosesMinutesAfterShift = originalCloses;
        }
    }

    [Fact]
    public async Task Pre_baseline_observed_rows_return_partial_null_totals_and_never_prove_building_ownership()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        // Historical events on Oct 6 (before baseline started on Oct 8!)
        await TestData.AddAttendanceAsync(factory, "1000001", new DateOnly(2026, 10, 6), Shift.Day,
            AttendanceEvent.ShiftIn, ThaiClock.At(6, 7, 5));
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(6, 7, 30), CleaningStatus.Issue);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");

        // Factory-wide pre-baseline read: partial availability, explicit "value": null, Observed >= 1, real retained metadata.
        var listRes = await admin.GetAsync("/api/admin/overview/areas?shiftDate=2026-10-06&shift=Day&group=entered");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var raw = await listRes.Content.ReadAsStringAsync();
        AssertSafeJsonProperties(raw);
        AssertExplicitNullValues(raw);

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        Assert.Equal("partial", root.GetProperty("total").GetProperty("availability").GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("total").GetProperty("value").ValueKind);
        Assert.Equal(1, root.GetProperty("total").GetProperty("observed").GetInt32());
        var row = Assert.Single(root.GetProperty("rows").EnumerateArray());
        Assert.Equal("AR01", row.GetProperty("code").GetString());

        // Building-scoped pre-baseline read: must NOT silently treat current DB BuildingId as historical proof!
        var buildingScoped = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/admin/overview/areas?shiftDate=2026-10-06&shift=Day&buildingId={buildingA}&group=entered");
        Assert.Equal("partial", buildingScoped.GetProperty("total").GetProperty("availability").GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, buildingScoped.GetProperty("total").GetProperty("value").ValueKind);
        Assert.Equal(0, buildingScoped.GetProperty("total").GetProperty("observed").GetInt32());
        Assert.Empty(buildingScoped.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task Archived_unavailable_date_returns_no_invented_daily_reconstruction()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        // 2025-01-01 is > 1 year earlier than 2026-10-08: attendanceExpired + workArchived.
        var archived = await admin.GetFromJsonAsync<JsonElement>(
            "/api/admin/overview/areas?shiftDate=2025-01-01&shift=Day&group=notEntered");
        Assert.Equal("unavailable", archived.GetProperty("total").GetProperty("availability").GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, archived.GetProperty("total").GetProperty("value").ValueKind);
        Assert.Equal(0, archived.GetProperty("total").GetProperty("observed").GetInt32());
        Assert.Empty(archived.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task Existing_service_points_and_attendance_board_routes_keep_behavior_without_overview_context()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 0));

        var points = await admin.GetAsync("/api/service-points");
        Assert.Equal(HttpStatusCode.OK, points.StatusCode);
        var pointNames = await AdminSetupApi.DashboardNamesAsync(admin);
        Assert.Equal(3, pointNames.Count);

        var board = await AdminAttendanceApi.BoardAsync(admin);
        Assert.Equal(new DateOnly(2026, 10, 8), board.ShiftDate);
        Assert.Equal("Day", board.Shift);
        Assert.Equal(2, board.Rows.Count);
    }

    [Fact]
    public async Task Zero_row_complete_list_returns_page_one_empty_rows_and_zero_total()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var empty = await admin.GetFromJsonAsync<JsonElement>(
            "/api/admin/overview/areas?shiftDate=2026-10-08&shift=Day&group=entered&page=3&pageSize=5");
        Assert.Equal(1, empty.GetProperty("page").GetInt32());
        Assert.Empty(empty.GetProperty("rows").EnumerateArray());
        Assert.Equal("complete", empty.GetProperty("total").GetProperty("availability").GetProperty("state").GetString());
        Assert.Equal(0, empty.GetProperty("total").GetProperty("value").GetInt32());
        Assert.Equal(0, empty.GetProperty("total").GetProperty("observed").GetInt32());
    }

    private static async Task<int> AddPointAsync(
        JanitorialApiFactory factory, string areaCode, string signCode, string qrToken, int startHour, int endHour)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var area = await db.Areas.SingleAsync(a => a.Code == areaCode);
            var created = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var point = new Domain.Entities.ServicePoint { AreaId = area.Id, Name = $"จุด {signCode}", SortOrder = 1, CreatedAt = created };
            db.Signs.Add(new Sign
            {
                AreaId = area.Id,
                ServicePoint = point,
                Code = signCode,
                QrToken = qrToken,
                QrIssuedAt = created,
                Latitude = 13.756300m,
                Longitude = 100.501800m,
                LocationAccuracyM = 10,
                LocationSource = LocationSource.Site,
                LocatedAt = created,
            });
            db.PointRoundWindows.Add(new PointRoundWindow
            {
                ServicePoint = point,
                Shift = Shift.Day,
                StartTime = new TimeOnly(startHour, 0),
                EndTime = new TimeOnly(endHour, 0),
                CreatedAt = created,
            });
            await db.SaveChangesAsync();
            id = point.Id;
        });
        return id;
    }

    internal static void AssertSafeJsonProperties(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        Walk(doc.RootElement);

        static void Walk(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item);
                }
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    Assert.DoesNotContain(prop.Name, ForbiddenJsonPropertyNames, StringComparer.OrdinalIgnoreCase);
                    Walk(prop.Value);
                }
            }
        }
    }

    internal static void AssertExplicitNullValues(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var foundCountValue = false;
        Walk(doc.RootElement);
        Assert.True(foundCountValue, "Expected at least one OverviewCount with explicit null 'value' property.");

        void Walk(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item);
                }
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("observed", out _) && element.TryGetProperty("availability", out _))
                {
                    Assert.True(element.TryGetProperty("value", out var valueProp),
                        "OverviewCount must always emit 'value', even when null.");
                    if (valueProp.ValueKind == JsonValueKind.Null)
                    {
                        foundCountValue = true;
                    }
                }

                foreach (var prop in element.EnumerateObject())
                {
                    Walk(prop.Value);
                }
            }
        }
    }
}
