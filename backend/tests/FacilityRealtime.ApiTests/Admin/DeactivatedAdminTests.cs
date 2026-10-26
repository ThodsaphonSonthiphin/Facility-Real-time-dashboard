using System.Net;
using System.Text;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// AdminOnly checks only the role claim, so every Admin route must also load the actor (CurrentUser.LoadAsync) and
/// answer 401 when the account is deactivated, even if its access token is still valid.
/// </summary>
public class DeactivatedAdminTests
{
    private const string PutAttendance =
        """{ "userId": 1, "shiftDate": "2026-10-08", "shift": "Day", "eventType": "ShiftIn", "time": "06:58", "reason": "x" }""";

    [Theory]
    [InlineData("GET", "/api/admin/attendance", null)]
    [InlineData("PUT", "/api/admin/attendance", PutAttendance)]
    [InlineData("GET", "/api/admin/alerts", null)]
    [InlineData("POST", "/api/admin/alerts/review", """{ "kind": "BlockedScan", "recordId": 1 }""")]
    [InlineData("GET", "/api/admin/cover-assignments/candidates?areaId=1&shiftDate=2026-10-08&shift=Day", null)]
    [InlineData("GET", "/api/admin/accounts", null)]
    [InlineData("POST", "/api/admin/accounts", """{ "role": "Cleaner", "displayName": "x", "employeeId": "E9", "phone": "0000000099", "shift": "Day" }""")]
    [InlineData("PUT", "/api/admin/accounts/2", """{ "displayName": "x", "shift": "Day" }""")]
    [InlineData("PUT", "/api/admin/accounts/2/phone", """{ "phone": "0000000099" }""")]
    [InlineData("PUT", "/api/admin/accounts/1/password", """{ "password": "temporary-pass-9" }""")]
    [InlineData("POST", "/api/admin/accounts/2/deactivate", null)]
    [InlineData("POST", "/api/admin/accounts/2/activate", null)]
    [InlineData("GET", "/api/admin/audit-log", null)]
    [InlineData("GET", "/api/admin/audit-log/actors", null)]
    [InlineData("POST", "/api/admin/export", """{ "from": "2026-10-08", "to": "2026-10-08", "sets": ["Attendance"] }""")]
    [InlineData("GET", "/api/admin/inspection-summary", null)]
    public async Task A_deactivated_admin_with_a_live_token_is_refused_and_nothing_is_logged(string method, string path, string? body)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Username == "admin");
            user.IsActive = false;
            await db.SaveChangesAsync();
        });
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        var response = await admin.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }
}
