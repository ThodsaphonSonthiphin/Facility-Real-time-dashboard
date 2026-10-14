using System.Net;
using System.Net.Http.Json;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Calls the Admin's attendance, alert and history pages.</summary>
public static class AdminAttendanceApi
{
    public const string BoardPath = "/api/admin/attendance";

    public static async Task<AttendanceBoardModel> BoardAsync(HttpClient admin, string query = "")
    {
        var response = await admin.GetAsync(BoardPath + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AttendanceBoardModel>())!;
    }

    public static Task<HttpResponseMessage> CorrectAsync(
        HttpClient admin, int userId, DateOnly shiftDate, string shift, string eventType, string? time, string? reason) =>
        admin.PutAsJsonAsync(BoardPath, new { userId, shiftDate, shift, eventType, time, reason });

    public const string AlertsPath = "/api/admin/alerts";

    public static async Task<List<AlertModel>> AlertsAsync(HttpClient admin, string query = "")
    {
        var response = await admin.GetAsync(AlertsPath + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<AlertModel>>())!;
    }

    public static Task<HttpResponseMessage> ReviewAsync(HttpClient admin, string kind, long recordId, string? note = null) =>
        admin.PostAsJsonAsync(AlertsPath + "/review", new { kind, recordId, note });
}
