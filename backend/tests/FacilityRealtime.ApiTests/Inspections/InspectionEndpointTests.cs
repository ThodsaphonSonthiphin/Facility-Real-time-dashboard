using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Points;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Inspections;

/// <summary>facility-0029, 0037, 0047: the Supervisor records สะอาด or ไม่สะอาด on the current round's latest submission.</summary>
public class InspectionEndpointTests
{
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    private static async Task<InspectionRecord> StoredAsync(FacilityApiFactory factory, long id)
    {
        InspectionRecord? record = null;
        await factory.WithDbAsync(async db => record = await db.InspectionRecords.Include(i => i.Supervisor).SingleAsync(i => i.Id == id));
        return record!;
    }

    private static async Task<int> CountAsync(FacilityApiFactory factory)
    {
        var count = 0;
        await factory.WithDbAsync(async db => count = await db.InspectionRecords.CountAsync());
        return count;
    }

    [Fact]
    public async Task Passed_is_saved_with_its_gps_and_the_dashboard_shows_it()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var created = await InspectionApi.InspectedAsync(supervisor, scanId, "Passed");

        Assert.Equal("Passed", created.Result);
        Assert.Equal("Passed", created.NewPointStatus);
        Assert.Equal(scanId, created.ScanRecordId);
        Assert.True(created.WithinRadius);
        var stored = await StoredAsync(factory, created.InspectionId);
        Assert.Equal("2000001", stored.Supervisor!.EmployeeId);
        Assert.Null(stored.Defect);
        Assert.Null(stored.CoverAssignmentId);
        Assert.Equal(13.756300m, stored.Latitude);
        Assert.Equal(ThaiClock.At(8, 8, 30).UtcDateTime, stored.InspectedAt);
        var board = (await admin.GetFromJsonAsync<List<PointModel>>("/api/service-points"))!;
        Assert.Equal("Passed", board.Single(p => p.Name == "ห้องน้ำชาย ชั้น 1").Status);
    }

    [Fact]
    public async Task Rework_keeps_the_defect_and_the_cleaner_sees_it_on_my_work()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var created = await InspectionApi.InspectedAsync(supervisor, scanId, "Rework", "  พื้นใต้อ่างล้างมือยังเปียก  ");

        Assert.Equal("Rework", created.NewPointStatus);
        Assert.Equal("พื้นใต้อ่างล้างมือยังเปียก", (await StoredAsync(factory, created.InspectionId)).Defect);
        var work = (await cleaner.GetFromJsonAsync<MyWorkModel>("/api/my-work"))!;
        var point = work.Areas.Single().Points.Single(p => p.Name == "ห้องน้ำชาย ชั้น 1");
        Assert.Equal("Rework", point.Status);
        Assert.Equal("พื้นใต้อ่างล้างมือยังเปียก", point.Rework!.Defect);
        Assert.Equal(ThaiClock.At(8, 8, 30).UtcDateTime, point.Rework.RequestedAt.ToUniversalTime());
    }

    [Theory]
    [InlineData("Rework", null)]
    [InlineData("Rework", "   ")]
    [InlineData("Passed", "ไม่ควรมีข้อบกพร่อง")]
    public async Task Defect_is_required_for_rework_and_left_out_for_passed(string result, string? defect)
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, result, defect);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task A_defect_over_1000_characters_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, "Rework", new string('ก', 1001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_defect_of_exactly_1000_characters_is_accepted()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, "Rework", new string('ก', 1000));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_blank_defect_with_passed_counts_as_left_out()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, "Passed", "");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<InspectionCreatedModel>())!;
        Assert.Null((await StoredAsync(factory, created.InspectionId)).Defect);
    }

    [Fact]
    public async Task Result_and_submission_are_required()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var gps = new { latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10 };

        var noResult = await supervisor.PostAsJsonAsync(
            "/api/inspections", new { qrToken = InspectionApi.MenRestroom, scanRecordId = scanId, gps.latitude, gps.longitude, gps.accuracyM });
        var noSubmission = await InspectionApi.InspectAsync(supervisor, null, "Passed");

        Assert.Equal(HttpStatusCode.BadRequest, noResult.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noSubmission.StatusCode);
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task A_submission_the_supervisor_did_not_see_is_409()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var first = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 20));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, first, "Passed");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task Rework_still_owed_is_409()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, scanId, InspectionResult.Rework, ThaiClock.At(8, 8, 20));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, "Passed");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await CountAsync(factory));
    }

    [Fact]
    public async Task Nothing_submitted_is_409()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, 999, "Passed");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Rework_resubmission_and_second_inspection_go_round_the_cycle()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var submit = new { qrToken = InspectionApi.MenRestroom, status = "Normal", latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10 };
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));
        var first = (await (await cleaner.PostAsJsonAsync("/api/scan-records", submit)).Content.ReadFromJsonAsync<ScanCreatedModel>())!;
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 20));
        await InspectionApi.InspectedAsync(supervisor, first.ScanRecordId, "Rework", "พื้นยังเปียก");

        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 40));
        var fixedResponse = await cleaner.PostAsJsonAsync("/api/scan-records", submit);
        var fixedWork = (await fixedResponse.Content.ReadFromJsonAsync<ScanCreatedModel>())!;
        Assert.Equal("Rework", fixedWork.Placement);
        Assert.Equal("PendingInspection", fixedWork.NewPointStatus);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 50));
        Assert.Equal(fixedWork.ScanRecordId, (await InspectionApi.ScannedAsync(supervisor)).Submission!.Id);
        var passed = await InspectionApi.InspectedAsync(supervisor, fixedWork.ScanRecordId, "Passed");
        Assert.Equal("Passed", passed.NewPointStatus);
    }

    [Fact]
    public async Task Far_from_the_sign_is_recorded_and_flagged()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, "Passed", latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<InspectionCreatedModel>())!;
        Assert.False(created.WithinRadius);
        Assert.True(created.DistanceM > 1000);
        Assert.False((await StoredAsync(factory, created.InspectionId)).WithinRadius);
    }

    [Fact]
    public async Task An_inspection_under_a_cover_is_marked()
    {
        using var factory = new FacilityApiFactory();
        await InspectionApi.AddBuildingBAsync(factory);
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var coverId = await TestData.AddSupervisorCoverAsync(factory, "2000001", "B", Oct8, Shift.Day);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.BuildingBPoint, ThaiClock.At(8, 8, 30));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 40));

        var created = await InspectionApi.InspectedAsync(supervisor, scanId, "Passed", qrToken: InspectionApi.BuildingBPoint);

        Assert.Equal(coverId, (await StoredAsync(factory, created.InspectionId)).CoverAssignmentId);
    }

    [Fact]
    public async Task A_blocked_inspection_saves_only_the_blocked_scan()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 20, 30));

        var response = await InspectionApi.InspectAsync(supervisor, scanId, "Passed");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CountAsync(factory));
        var blocked = new List<BlockedScan>();
        await factory.WithDbAsync(async db => blocked = await db.BlockedScans.ToListAsync());
        Assert.Equal(BlockReason.OutsideShift, Assert.Single(blocked).Reason);
    }

    [Fact]
    public async Task Admins_get_the_inspection_live()
    {
        using var factory = new FacilityApiFactory();
        var supervisor = await InspectionApi.SupervisorAsync(factory);
        var (_, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        // facility-0058 as amended 2026-10-26: AR01's own Cleaners get it too (CleanerLiveTests); AR02's Cleaner does not
        var (_, cleanerAuth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000003", "0810000003");
        await using var adminHub = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        await using var cleanerHub = await HubClient.ConnectAsync(factory, cleanerAuth.AccessToken);
        var cleanerGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        cleanerHub.On<JsonElement>("InspectionRecorded", point => cleanerGot.TrySetResult(point));
        var adminGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        adminHub.On<JsonElement>("InspectionRecorded", point => adminGot.TrySetResult(point));
        await Task.Delay(200); // OnConnectedAsync joins the admin group just after the handshake
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.MenRestroom, ThaiClock.At(8, 8, 10));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        await InspectionApi.InspectedAsync(supervisor, scanId, "Rework", "พื้นยังเปียก");

        var payload = await adminGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Rework", payload.GetProperty("status").GetString());
        Assert.False(payload.TryGetProperty("qrToken", out _)); // facility-0060
        await Task.Delay(500);
        Assert.False(cleanerGot.Task.IsCompleted); // another Area's Cleaner
    }
}
