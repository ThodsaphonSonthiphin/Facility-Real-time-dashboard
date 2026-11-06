using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.ApiTests.Inspections;
using Microsoft.AspNetCore.SignalR.Client;

namespace JanitorialManagementDashboard.ApiTests.Admin;

/// <summary>
/// facility-0105: an Admin's hub connection was put in a group once, at connect, and kept getting every push after the
/// account was deactivated. Pushes to Admins now go to the active Admins read at send time, so a deactivated Admin whose
/// page is still open gets nothing from the next push on.
/// </summary>
public class DeactivatedAdminLiveTests
{
    private const string SecondAdminPassword = "temporary-pass-2";

    private static TaskCompletionSource<JsonElement> Listen(HubConnection hub, string eventName)
    {
        var got = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<JsonElement>(eventName, payload => got.TrySetResult(payload));
        return got;
    }

    /// <summary>Two Admins on the hub; the second is then deactivated through the accounts page.</summary>
    private static async Task<(HubConnection Active, HubConnection Deactivated)> TwoAdminsOneDeactivatedAsync(JanitorialApiFactory factory)
    {
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var second = await AccountApi.ReadAsync(await AccountApi.CreateAdminAsync(admin, "admin2", SecondAdminPassword));
        var login = await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin2", SecondAdminPassword);
        login.EnsureSuccessStatusCode();
        var secondAuth = await AuthApi.ReadAuthAsync(login);
        var active = await HubClient.ConnectAsync(factory, adminAuth.AccessToken);
        var deactivated = await HubClient.ConnectAsync(factory, secondAuth.AccessToken);

        var response = await admin.PostAsync($"{AccountApi.Path}/{second.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (active, deactivated);
    }

    [Fact]
    public async Task A_deactivated_admin_with_the_page_open_gets_no_scan_record()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var hubs = await TwoAdminsOneDeactivatedAsync(factory);
        await using var activeHub = hubs.Active;
        await using var deactivatedHub = hubs.Deactivated;
        var activeGot = Listen(activeHub, "ScanRecorded");
        var deactivatedGot = Listen(deactivatedHub, "ScanRecorded");
        await Task.Delay(200);
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 7, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 10));

        var response = await cleaner.PostAsJsonAsync("/api/scan-records", new
        {
            qrToken = InspectionApi.MenRestroom, status = "Normal", latitude = TestGps.NearLatitude, longitude = TestGps.NearLongitude, accuracyM = 10,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await activeGot.Task.WaitAsync(TimeSpan.FromSeconds(5)); // the send works
        await Task.Delay(500);
        Assert.False(deactivatedGot.Task.IsCompleted);
    }

    [Fact]
    public async Task A_deactivated_admin_with_the_page_open_gets_no_shift_check_in()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var hubs = await TwoAdminsOneDeactivatedAsync(factory);
        await using var activeHub = hubs.Active;
        await using var deactivatedHub = hubs.Deactivated;
        var activeGot = Listen(activeHub, "AttendanceRecorded");
        var deactivatedGot = Listen(deactivatedHub, "AttendanceRecorded");
        await Task.Delay(200);

        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 6, 50));

        await activeGot.Task.WaitAsync(TimeSpan.FromSeconds(5)); // the send works
        await Task.Delay(500);
        Assert.False(deactivatedGot.Task.IsCompleted);
    }
}
