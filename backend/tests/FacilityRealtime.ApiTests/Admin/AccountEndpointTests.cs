using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Wireframe D6, facility-0020/0054/0056: the Admin lists accounts and adds them one by one.</summary>
public class AccountEndpointTests
{
    [Fact]
    public async Task The_list_shows_every_account_admins_first_and_never_a_secret()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.GetAsync(AccountApi.Path);
        var raw = await response.Content.ReadAsStringAsync();
        var accounts = JsonSerializer.Deserialize<List<AccountModel>>(raw, JsonSerializerOptions.Web)!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { "admin", "S2001", "E1001", "E1002", "E1003" }, accounts.Select(a => a.LoginName));
        Assert.Equal(new[] { "Admin", "Supervisor", "Cleaner", "Cleaner", "Cleaner" }, accounts.Select(a => a.Role));
        var e1001 = accounts.Single(a => a.LoginName == "E1001");
        Assert.Equal(("AR01", "Day", true), (e1001.AreaCode, e1001.Shift, e1001.IsActive));
        Assert.Equal("A", accounts.Single(a => a.LoginName == "S2001").BuildingCode);
        Assert.EndsWith("Z", JsonDocument.Parse(raw).RootElement[0].GetProperty("createdAt").GetString());
        Assert.DoesNotContain("0810000001", raw);
        Assert.DoesNotContain("pbkdf2", raw);
        Assert.DoesNotContain("secret", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_new_cleaner_logs_in_with_the_phone_at_once_and_the_log_has_no_phone()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar03 = await TestData.AddAreaAsync(factory, "AR03");

        var response = await AccountApi.CreateCleanerAsync(admin, " E2001 ", "000-000-0011", ar03, "Night");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await AccountApi.ReadAsync(response);
        Assert.Equal(("Cleaner", "E2001", "AR03", "Night", true), (created.Role, created.LoginName, created.AreaCode, created.Shift, created.IsActive));
        Assert.Equal($"/api/admin/accounts/{created.Id}", response.Headers.Location!.OriginalString);

        var login = await AuthApi.LoginAsync(factory.CreateApiClient(), "E2001", "0000000011");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("ACCOUNT_CREATE", "users", (long?)created.Id, adminAuth.User.Id), (entry.Action, entry.EntityType, entry.EntityId, entry.ActorId));
        Assert.Contains("E2001", entry.Summary);
        Assert.Null(entry.BeforeJson);
        Assert.Contains("\"LoginName\":\"E2001\"", entry.AfterJson);
        Assert.DoesNotContain("0000000011", entry.AfterJson + entry.Summary);
    }

    [Fact]
    public async Task A_cleaner_may_be_added_without_an_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateCleanerAsync(admin, "E2002", "0000000012", areaId: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await AccountApi.ReadAsync(response);
        Assert.Null(created.AreaId);
        Assert.Equal("Day", created.Shift);
    }

    [Fact]
    public async Task A_taken_area_shift_is_refused_and_names_who_holds_it()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await AccountApi.CreateCleanerAsync(admin, "E2003", "0000000013", ar01, "Day");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของ AR01 มีแม่บ้านประจำแล้ว (สมชาย ใจดี)", await AdminSetupApi.MessageAsync(response));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_supervisor_takes_a_free_building_shift_only()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);

        var taken = await AccountApi.CreateSupervisorAsync(admin, "S2002", "0000000021", buildingA, "Day");
        var free = await AccountApi.CreateSupervisorAsync(admin, "S2003", "0000000022", buildingA, "Night");

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("กะเช้าของตึก A มีหัวหน้าประจำแล้ว (สมปอง ตรวจดี)", await AdminSetupApi.MessageAsync(taken));
        Assert.Equal(HttpStatusCode.Created, free.StatusCode);
        var created = await AccountApi.ReadAsync(free);
        Assert.Equal(("Supervisor", "A", "Night"), (created.Role, created.BuildingCode, created.Shift));
    }

    [Fact]
    public async Task A_new_admin_logs_in_with_the_temporary_password()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateAdminAsync(admin, "admin2", "temporary-pass-1");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await AccountApi.ReadAsync(response);
        Assert.Equal(("Admin", "admin2", (string?)null), (created.Role, created.LoginName, created.Shift));
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin2", "temporary-pass-1")).StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.DoesNotContain("temporary-pass-1", entry.AfterJson + entry.Summary);
    }

    [Theory]
    [InlineData("e1001")]
    [InlineData("E1001")]
    public async Task A_used_employee_id_is_refused_whatever_the_letter_case(string employeeId)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateCleanerAsync(admin, employeeId, "0000000014", areaId: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("รหัสพนักงานนี้มีแล้ว", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_used_username_is_refused_whatever_the_letter_case()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateAdminAsync(admin, "ADMIN", "temporary-pass-1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ชื่อผู้ใช้นี้มีแล้ว", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_common_password_is_refused_with_its_reason_and_nothing_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateAdminAsync(admin, "admin2", "1qaz2wsx3edc4rfv");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น", await AdminSetupApi.MessageAsync(response));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    public static TheoryData<string> BadRequests => new()
    {
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "๐๐๐๐๐๐๐๐๑๙", "shift": "Day" }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "000000019", "shift": "Day" }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E 2009", "phone": "0000000019", "shift": "Day" }""",
        """{ "role": "Cleaner", "displayName": "   ", "employeeId": "E2009", "phone": "0000000019", "shift": "Day" }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "0000000019" }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "0000000019", "shift": "Day", "buildingId": 1 }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "0000000019", "shift": "Day", "areaId": 9999 }""",
        """{ "role": "Cleaner", "displayName": "ก", "employeeId": "E2009", "phone": "0000000019", "shift": "Day", "password": "12345678" }""",
        """{ "role": "Supervisor", "displayName": "ก", "employeeId": "S2009", "phone": "0000000029", "shift": "Night" }""",
        """{ "role": "Supervisor", "displayName": "ก", "employeeId": "S2009", "phone": "0000000029", "shift": "Night", "buildingId": 1, "areaId": 1 }""",
        """{ "role": "Admin", "displayName": "ก", "username": "admin9", "password": "12345678901234" }""",
        """{ "role": "Admin", "displayName": "ก", "username": "admin9", "password": "temporary-pass-1", "phone": "0000000019" }""",
        """{ "role": "Admin", "displayName": "ก", "username": "admin9", "password": "temporary-pass-1", "shift": "Day" }""",
        """{ "displayName": "ก", "employeeId": "E2009", "phone": "0000000019", "shift": "Day" }""",
    };

    [Theory]
    [MemberData(nameof(BadRequests))]
    public async Task Bad_input_is_refused_and_nothing_is_saved(string body)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var before = (await AccountApi.ListAsync(admin)).Count;

        var response = await admin.PostAsync(AccountApi.Path, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(await AdminSetupApi.MessageAsync(response)));
        Assert.Equal(before, (await AccountApi.ListAsync(admin)).Count);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_day_only_area_takes_no_night_cleaner()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");   // seeded DayOnly

        var response = await AccountApi.CreateCleanerAsync(admin, "E2004", "0000000015", ar02, "Night");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("AR02 ทำเฉพาะกะเช้า เลือกกะดึกไม่ได้", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_deactivated_area_takes_no_cleaner()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar03 = await TestData.AddAreaAsync(factory, "AR03");
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Id == ar03)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await AccountApi.CreateCleanerAsync(admin, "E2005", "0000000016", ar03);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cleaners_and_supervisors_cannot_open_the_page()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2001", "0820000001");

        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(AccountApi.Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.GetAsync(AccountApi.Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AccountApi.CreateCleanerAsync(cleaner, "E2006", "0000000017", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateApiClient().GetAsync(AccountApi.Path)).StatusCode);
    }
}
