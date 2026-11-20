using JanitorialManagementDashboard.ApiTests.Scheduling;
using System.Net;
using System.Net.Http.Json;

namespace JanitorialManagementDashboard.ApiTests.Infrastructure;

public static class AttendanceApi
{
    public const string Area1CheckIn = "token-checkin-ar01";

    public static async Task<HttpResponseMessage> RecordAsync(
        HttpClient client,
        string eventType,
        string qrToken = Area1CheckIn,
        double latitude = TestGps.NearLatitude,
        double longitude = TestGps.NearLongitude,
        double accuracyM = 10)
    {
        using var read = await client.GetAsync("/api/attendance/me");
        var state = read.IsSuccessStatusCode ? await read.Content.ReadFromJsonAsync<AttendanceStateAnswer>() : null;
        return await client.PostAsJsonAsync("/api/attendance", new
        {
            qrToken, eventType, latitude, longitude, accuracyM,
            stateRevision = state?.StateRevision,
            confirmGaps = state?.NextActions?.FirstOrDefault(choice => choice.Action == eventType)?.RequiredGaps,
        });
    }

    /// <summary>Moves the clock to <paramref name="at"/> and records Shift-In; a repeat just returns the first time.</summary>
    public static async Task CheckInAsync(JanitorialApiFactory factory, HttpClient client, DateTimeOffset at, string qrToken = Area1CheckIn)
    {
        factory.Clock.SetUtcNow(at);
        var response = await RecordAsync(client, "ShiftIn", qrToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
