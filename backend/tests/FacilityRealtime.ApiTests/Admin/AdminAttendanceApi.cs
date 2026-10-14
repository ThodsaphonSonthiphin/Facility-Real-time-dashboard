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
}
