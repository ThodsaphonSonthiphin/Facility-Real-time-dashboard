using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Points;

public record ScanCreatedModel(long ScanRecordId, int ServicePointId, string Placement, int? LateMinutes, string NewPointStatus, DateTime SubmittedAt);

/// <summary>Seeded men's restroom (token-restroom-m1): Day rounds 07:00-09:00 and 16:00-18:00. Log in before moving the clock.</summary>
public class ScanRecordEndpointTests
{
    private const string MenRestroomToken = "token-restroom-m1";

    private static async Task<ScanCreatedModel> ScanAtAsync(
        FacilityApiFactory factory, HttpClient client, DateTimeOffset at, object? body = null)
    {
        factory.Clock.SetUtcNow(at);
        var response = await client.PostAsJsonAsync("/api/scan-records", body ?? new { qrToken = MenRestroomToken, status = "Normal" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ScanCreatedModel>())!;
    }

    private static async Task<ScanRecord> StoredAsync(FacilityApiFactory factory, long id)
    {
        ScanRecord? record = null;
        await factory.WithDbAsync(async db => record = await db.ScanRecords.SingleAsync(r => r.Id == id));
        return record!;
    }

    [Fact]
    public async Task Scanning_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().PostAsJsonAsync("/api/scan-records", new { qrToken = MenRestroomToken, status = "Normal" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("no-such-token")]
    [InlineData("token-checkin-ar01")]
    public async Task Unknown_or_check_in_tokens_are_404(string token)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", new { qrToken = token, status = "Normal" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Scan_inside_the_round_is_on_time_and_waits_for_inspection()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);

        var created = await ScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        Assert.Equal("OnTime", created.Placement);
        Assert.Null(created.LateMinutes);
        Assert.Equal("PendingInspection", created.NewPointStatus);
        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(auth.User.Id, stored.UserId);
        Assert.Equal(new DateOnly(2026, 10, 8), stored.ShiftDate);
        Assert.Equal(Shift.Day, stored.Shift);
        Assert.Equal(new TimeOnly(7, 0), stored.RoundStart);
        Assert.Equal(new TimeOnly(9, 0), stored.RoundEnd);
        Assert.Equal(Placement.OnTime, stored.Placement);
    }

    [Fact]
    public async Task Scan_after_an_empty_round_is_late_for_that_round()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await ScanAtAsync(factory, cleaner, ThaiClock.At(8, 9, 40));

        Assert.Equal("Late", created.Placement);
        Assert.Equal(40, created.LateMinutes);
        Assert.Equal("PendingInspection", created.NewPointStatus);
    }

    [Fact]
    public async Task Second_scan_after_the_round_is_off_round()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await ScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        var created = await ScanAtAsync(factory, cleaner, ThaiClock.At(8, 9, 40));

        Assert.Equal("OffRound", created.Placement);
        Assert.Null((await StoredAsync(factory, created.ScanRecordId)).RoundWindowId);
    }

    [Fact]
    public async Task Scan_after_a_rework_inspection_is_the_rework()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var first = await ScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, first.ScanRecordId, InspectionResult.Rework, ThaiClock.At(8, 8, 30));

        var created = await ScanAtAsync(factory, cleaner, ThaiClock.At(8, 10, 0));

        Assert.Equal("Rework", created.Placement);
        Assert.Equal("PendingInspection", created.NewPointStatus);
    }

    [Fact]
    public async Task Scanner_is_the_logged_in_user_even_if_the_body_names_someone_else()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        var adminId = 0;
        await factory.WithDbAsync(async db => adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id);

        // The phase-1 contract accepted userId in the body; an old client or curl may still send it (facility-0017)
        var created = await ScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10),
            new { qrToken = MenRestroomToken, userId = adminId, status = "Normal" });

        Assert.Equal(auth.User.Id, (await StoredAsync(factory, created.ScanRecordId)).UserId);
    }

    [Fact]
    public async Task Issue_scan_stores_its_tags_and_note()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var created = await ScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10),
            new { qrToken = MenRestroomToken, status = "Issue", issueTags = new[] { "wet_floor", "plumbing_issue" }, notes = " ก๊อกรั่ว " });

        var stored = await StoredAsync(factory, created.ScanRecordId);
        Assert.Equal(CleaningStatus.Issue, stored.Status);
        Assert.Equal("wet_floor,plumbing_issue", stored.IssueTags);
        Assert.Equal("ก๊อกรั่ว", stored.Note);
    }

    [Fact]
    public async Task Overlong_note_is_rejected()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.PostAsJsonAsync("/api/scan-records",
            new { qrToken = MenRestroomToken, status = "Normal", notes = new string('x', 1001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_admins_receive_the_live_update()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, cleanerAuth, _) = await AuthApi.LoggedInAsync(factory);
        var (_, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await using var adminHub = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        await using var cleanerHub = await HubClient.ConnectAsync(factory, cleanerAuth.AccessToken);
        var adminGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanerGot = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        adminHub.On<JsonElement>("ScanRecorded", point => adminGot.TrySetResult(point));
        cleanerHub.On<JsonElement>("ScanRecorded", point => cleanerGot.TrySetResult(point));
        // StartAsync returns after the handshake; OnConnectedAsync, which joins the admin group, runs just after it
        await Task.Delay(200);

        await ScanAtAsync(factory, cleaner, ThaiClock.At(8, 8, 10));

        var payload = await adminGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("PendingInspection", payload.GetProperty("status").GetString());
        Assert.False(payload.TryGetProperty("qrToken", out _)); // facility-0060
        await Task.Delay(500);
        Assert.False(cleanerGot.Task.IsCompleted); // facility-0058
    }
}
