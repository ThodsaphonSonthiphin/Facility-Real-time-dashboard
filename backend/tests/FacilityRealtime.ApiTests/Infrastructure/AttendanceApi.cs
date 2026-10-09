using System.Net;
using System.Net.Http.Json;

namespace FacilityRealtime.ApiTests.Infrastructure;

public static class AttendanceApi
{
    public const string Area1CheckIn = "token-checkin-ar01";

    public static Task<HttpResponseMessage> RecordAsync(
        HttpClient client,
        string eventType,
        string qrToken = Area1CheckIn,
        double latitude = TestGps.NearLatitude,
        double longitude = TestGps.NearLongitude,
        double accuracyM = 10) =>
        client.PostAsJsonAsync("/api/attendance", new { qrToken, eventType, latitude, longitude, accuracyM });

    /// <summary>Moves the clock to <paramref name="at"/> and records Shift-In; a repeat just returns the first time.</summary>
    public static async Task CheckInAsync(FacilityApiFactory factory, HttpClient client, DateTimeOffset at, string qrToken = Area1CheckIn)
    {
        factory.Clock.SetUtcNow(at);
        var response = await RecordAsync(client, "ShiftIn", qrToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
