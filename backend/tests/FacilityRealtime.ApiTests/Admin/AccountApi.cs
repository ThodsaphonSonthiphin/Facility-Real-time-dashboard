using System.Net.Http.Json;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Calls of the accounts page (wireframe D6). Fields a role does not use are left out, as the page will.</summary>
public static class AccountApi
{
    public const string Path = "/api/admin/accounts";

    public static Task<HttpResponseMessage> CreateCleanerAsync(
        HttpClient admin, string employeeId, string phone, int? areaId, string shift = "Day", string displayName = "แม่บ้านใหม่ ทดสอบ") =>
        admin.PostAsJsonAsync(Path, new { role = "Cleaner", displayName, employeeId, phone, areaId, shift });

    public static Task<HttpResponseMessage> CreateSupervisorAsync(
        HttpClient admin, string employeeId, string phone, int buildingId, string shift, string displayName = "หัวหน้าใหม่ ทดสอบ") =>
        admin.PostAsJsonAsync(Path, new { role = "Supervisor", displayName, employeeId, phone, buildingId, shift });

    public static Task<HttpResponseMessage> CreateAdminAsync(
        HttpClient admin, string username, string password, string displayName = "Admin ใหม่ ทดสอบ") =>
        admin.PostAsJsonAsync(Path, new { role = "Admin", displayName, username, password });

    public static async Task<AccountModel> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AccountModel>())!;

    public static async Task<List<AccountModel>> ListAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<AccountModel>>(Path))!;
}
