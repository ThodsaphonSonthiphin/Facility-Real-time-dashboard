using System.Net;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Infrastructure;

namespace JanitorialManagementDashboard.ApiTests.Contract;

/// <summary>
/// Verifies that serialized camelCase JSON payloads and enum strings from live endpoints satisfy
/// the TypeScript contracts in frontend/src/shared/api/types.ts and frontend/src/shared/overview/types.ts.
/// </summary>
public sealed class ApiContractTests
{
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    private static void AssertHasProperties(JsonElement obj, params string[] names)
    {
        Assert.Equal(JsonValueKind.Object, obj.ValueKind);
        foreach (var name in names)
        {
            Assert.True(obj.TryGetProperty(name, out _), $"Missing contract property '{name}' in {obj}");
        }
    }

    [Fact]
    public async Task Login_response_matches_frontend_AuthResponse_contract()
    {
        using var factory = new JanitorialApiFactory();
        var client = factory.CreateApiClient();

        var body = await ReadJsonAsync(await AuthApi.AdminLoginAsync(client));

        AssertHasProperties(body, "accessToken", "expiresAt", "user");
        var user = body.GetProperty("user");
        AssertHasProperties(user, "id", "username", "fullName", "role");
        Assert.Contains(user.GetProperty("role").GetString(), new[] { "admin", "supervisor", "cleaner" });
    }

    [Fact]
    public async Task Overview_areas_matches_frontend_OverviewAreaList_contract()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await SetDayShiftAsync(factory);

        var body = await ReadJsonAsync(await admin.GetAsync("/api/admin/overview/areas?group=notEntered"));

        AssertHasProperties(body, "summary", "group", "page", "pageSize", "total", "rows");
        var summary = body.GetProperty("summary");
        AssertHasProperties(summary, "context", "availability", "entry", "submission", "inspection", "groups", "buildings");
        AssertHasProperties(summary.GetProperty("context"), "shiftDate", "shift", "phase", "readAtUtc", "evaluationUtc", "isClockCurrent", "range");
        Assert.Contains(summary.GetProperty("context").GetProperty("shift").GetString(), new[] { "Day", "Night" });
        Assert.Contains(summary.GetProperty("context").GetProperty("phase").GetString(), new[] { "running", "receiving", "closed", "notStarted" });
        AssertHasProperties(summary.GetProperty("entry"), "numerator", "denominator");
        Assert.Equal(16, summary.GetProperty("groups").EnumerateObject().Count());
        AssertHasProperties(summary.GetProperty("groups"),
            "entered", "covered", "unassigned", "notEntered",
            "submitted", "incomplete", "inspected", "pendingInspection",
            "noSubmission", "rework", "issue", "overdue",
            "upcoming", "noRounds", "missedCleaning", "missedInspection");

        var rows = body.GetProperty("rows").EnumerateArray().ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, AssertOverviewAreaRow);

        var areaId = rows[0].GetProperty("areaId").GetInt32();
        var detail = await ReadJsonAsync(await admin.GetAsync($"/api/admin/overview/areas/{areaId}?group=notEntered"));
        AssertHasProperties(detail, "context", "availability", "area", "matchesGroup", "entryEvidence", "facts", "points", "rounds");
        Assert.Equal(areaId, detail.GetProperty("area").GetProperty("areaId").GetInt32());
    }

    [Fact]
    public async Task Service_points_and_my_work_match_frontend_PointCard_and_MyWork_contracts()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await SetDayShiftAsync(factory);

        var points = await ReadJsonAsync(await admin.GetAsync("/api/service-points"));
        Assert.Equal(JsonValueKind.Array, points.ValueKind);
        Assert.NotEmpty(points.EnumerateArray());
        Assert.All(points.EnumerateArray(), AssertPointCard);

        var myWork = await ReadJsonAsync(await cleaner.GetAsync("/api/my-work"));
        AssertHasProperties(myWork, "attendance", "areas");
        var attendance = myWork.GetProperty("attendance");
        AssertHasProperties(attendance, "events", "actions");
        Assert.Equal(JsonValueKind.Array, attendance.GetProperty("events").ValueKind);
        var actions = attendance.GetProperty("actions").EnumerateArray().ToArray();
        Assert.Equal(
            new[] { "ShiftIn", "Break1Out", "Break1In", "Break2Out", "Break2In", "ShiftOut" },
            actions.Select(action => action.GetProperty("eventType").GetString()));
        Assert.All(actions, action =>
        {
            Assert.Contains(action.GetProperty("state").GetString(),
                new[] { "recorded", "skipped", "missing", "not-applicable", "pending" });
        });

        var area = myWork.GetProperty("areas").EnumerateArray().First();
        AssertHasProperties(area, "areaId", "areaCode", "areaName", "isCover", "points");
        Assert.NotEmpty(area.GetProperty("points").EnumerateArray());
        Assert.All(area.GetProperty("points").EnumerateArray(), AssertPointCard);
    }

    [Fact]
    public async Task Attendance_sign_matches_frontend_check_sign_contract()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        const string sign = "/api/attendance/signs/token-checkin-ar01";

        Assert.Equal(HttpStatusCode.NoContent, (await cleaner.GetAsync(sign)).StatusCode);
        var body = await ReadJsonAsync(await cleaner.GetAsync(sign + "?transferId=1"));
        Assert.False(body.GetProperty("canConfirmTransfer").GetBoolean());
        Assert.False(body.GetProperty("canShiftOutOrigin").GetBoolean());
    }

    private static void AssertOverviewAreaRow(JsonElement row)
    {
        AssertHasProperties(row, "areaId", "buildingId", "code", "name", "buildingCode", "buildingName",
            "entry", "work", "reasons", "roundKeys");
        Assert.Contains(row.GetProperty("entry").GetString(), new[] { "entered", "covered", "unassigned" });
        var work = row.GetProperty("work");
        foreach (var count in new[] { "duePoints", "submittedPoints", "inspectedPoints" })
        {
            Assert.True(work.GetProperty(count).TryGetInt32(out _), $"Expected integer work.{count}: {work}");
        }
        foreach (var flag in new[] { "submitted", "inspected", "incomplete", "pendingInspection", "noSubmission", "rework", "issue", "overdue" })
        {
            Assert.Contains(work.GetProperty(flag).ValueKind, new[] { JsonValueKind.True, JsonValueKind.False });
        }
    }

    private static void AssertPointCard(JsonElement point)
    {
        AssertHasProperties(point, "id", "name", "areaId", "areaCode", "areaName", "buildingCode", "status");
        Assert.Contains(point.GetProperty("status").GetString(),
            new[] { "BeforeFirstRound", "NotYetDone", "OffHours", "Overdue", "Passed", "PendingInspection", "Rework" });
    }

    private static async Task SetDayShiftAsync(JanitorialApiFactory factory)
    {
        var midnight = factory.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1);
        var baseline = new DateTimeOffset(midnight, TimeSpan.FromHours(7));
        await OverviewTestData.StartHistoryAsync(factory, baseline);
        factory.Clock.SetUtcNow(baseline.AddHours(8));
    }
}
