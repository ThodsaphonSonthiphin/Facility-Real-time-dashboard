using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Admin;

/// <summary>Wireframe D6, facility-0020/0054/0056: the Admin lists accounts and adds them one by one.</summary>
public class AccountEndpointTests
{
    [Fact]
    public async Task The_list_shows_every_account_admins_first_and_never_a_secret()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.GetAsync(AccountApi.Path);
        var raw = await response.Content.ReadAsStringAsync();
        var accounts = JsonSerializer.Deserialize<List<AccountModel>>(raw, JsonSerializerOptions.Web)!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { "admin", "2000001", "1000001", "1000002", "1000003" }, accounts.Select(a => a.LoginName));
        Assert.Equal(new[] { "Admin", "Supervisor", "Cleaner", "Cleaner", "Cleaner" }, accounts.Select(a => a.Role));
        var e1001 = accounts.Single(a => a.LoginName == "1000001");
        Assert.Equal(("AR01", "Day", true), (e1001.AreaCode, e1001.Shift, e1001.IsActive));
        Assert.Equal("A", accounts.Single(a => a.LoginName == "2000001").BuildingCode);
        Assert.EndsWith("Z", JsonDocument.Parse(raw).RootElement[0].GetProperty("createdAt").GetString());
        Assert.DoesNotContain("0810000001", raw);
        Assert.DoesNotContain("pbkdf2", raw);
        Assert.DoesNotContain("secret", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_new_cleaner_logs_in_with_the_phone_at_once_and_the_log_has_no_phone()
    {
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
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
    [InlineData("1000001")]
    [InlineData("e9001")]
    [InlineData("E9001")]
    public async Task A_used_employee_id_is_refused_whatever_the_letter_case(string employeeId)
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        // the seed's IDs are digits only, like the factory's; a letter ID shows the check ignores case
        Assert.Equal(HttpStatusCode.Created, (await AccountApi.CreateCleanerAsync(admin, "E9001", "0000000013", areaId: null)).StatusCode);

        var response = await AccountApi.CreateCleanerAsync(admin, employeeId, "0000000014", areaId: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("รหัสพนักงานนี้มีแล้ว", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_used_username_is_refused_whatever_the_letter_case()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await AccountApi.CreateAdminAsync(admin, "ADMIN", "temporary-pass-1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ชื่อผู้ใช้นี้มีแล้ว", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_common_password_is_refused_with_its_reason_and_nothing_is_logged()
    {
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");   // seeded DayOnly

        var response = await AccountApi.CreateCleanerAsync(admin, "E2004", "0000000015", ar02, "Night");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("AR02 ทำเฉพาะกะเช้า เลือกกะดึกไม่ได้", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_deactivated_area_takes_no_cleaner()
    {
        using var factory = new JanitorialApiFactory();
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
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(AccountApi.Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.GetAsync(AccountApi.Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AccountApi.CreateCleanerAsync(cleaner, "E2006", "0000000017", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateApiClient().GetAsync(AccountApi.Path)).StatusCode);
    }

    [Fact]
    public async Task A_relief_cleaner_is_created_with_a_dated_home_building_from_today()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        Scheduling.ScheduleWriteApi.Today(factory);

        var response = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้านสแปร์", employeeId = "E3001", phone = "0893001000",
            shift = "Day", cleanerKind = "Relief", homeBuildingId = buildingB,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await AccountApi.ReadAsync(response);
        Assert.Equal((null, "Relief", (int?)buildingB, "B"), (
            created.AreaId, created.CleanerKind, created.HomeBuildingId, created.HomeBuildingCode));
        var profiles = await AdminSetupApi.ReadAsync(factory, db => db.CleanerProfilePeriods
            .Where(period => period.UserId == created.Id).ToListAsync());
        var profile = Assert.Single(profiles);
        // facility-0137/0194: the home building lives in the dated profile, and today is the first date it applies to.
        Assert.Equal(("Relief", (int?)buildingB, "Day", new DateOnly(2026, 11, 17)), (
            profile.Kind.ToString(), profile.HomeBuildingId, profile.Shift.ToString(), profile.EffectiveFrom));
        Assert.Null(profile.EffectiveUntil);
        Assert.Equal(1L, profile.Revision);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ACCOUNT_CREATE", entry.Action);
        Assert.DoesNotContain("0893001000", entry.AfterJson + entry.Summary);
    }

    [Fact]
    public async Task A_regular_cleaner_created_without_the_new_fields_writes_a_regular_identity_from_the_area()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar03 = await TestData.AddAreaAsync(factory, "AR03");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
        Scheduling.ScheduleWriteApi.Today(factory);

        // The form every existing caller sends: no cleanerKind, no home building.
        var response = await AccountApi.CreateCleanerAsync(admin, "E2001", "0000000011", ar03, "Day");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await AccountApi.ReadAsync(response);
        Assert.Equal(("AR03", "Cleaner", "Regular", "A"), (
            created.AreaCode, created.Role, created.CleanerKind, created.HomeBuildingCode));
        var profile = Assert.Single(await AdminSetupApi.ReadAsync(factory, db => db.CleanerProfilePeriods
            .Where(period => period.UserId == created.Id).ToListAsync()));
        Assert.Equal(("Regular", (int?)buildingA, "Day", new DateOnly(2026, 11, 17)), (
            profile.Kind.ToString(), profile.HomeBuildingId, profile.Shift.ToString(), profile.EffectiveFrom));
    }

    [Theory]
    [InlineData("Regular", null, false, false)]    // a regular Cleaner needs a home building or an Area
    [InlineData("Relief", "AR01", true, false)]    // a relief has no regular Area
    [InlineData("Relief", null, false, false)]     // a relief needs a home building
    [InlineData("Relief", null, true, true)]       // the home building must be one that exists
    [InlineData(5, null, false, false)]            // a number the enum does not define is not a kind to store
    [InlineData("5", null, false, false)]          // nor is its spelling as a string
    public async Task A_cleaner_identity_that_cannot_be_stored_is_refused(
        object? cleanerKind, string? areaCode, bool withHome, bool unknownHome)
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var areaId = areaCode is null ? (int?)null : await AdminSetupApi.AreaIdAsync(factory, areaCode);

        var response = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner",
            displayName = "แม่บ้านทดสอบ",
            employeeId = "E2007",
            phone = "0893007000",
            shift = "Day",
            areaId,
            cleanerKind,
            homeBuildingId = withHome ? (unknownHome ? buildingB + 1000 : buildingB) : (int?)null,
        });

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
            $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.Users.Where(user => user.EmployeeId == "E2007").ToListAsync());
            Assert.Empty(await db.CleanerProfilePeriods.ToListAsync());
        });
    }

    [Fact]
    public async Task A_regular_cleaner_can_be_created_with_home_building_only_and_null_shift()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
        Scheduling.ScheduleWriteApi.Today(factory);

        var response = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner",
            displayName = "แม่บ้านใหม่ ยังไม่จัดกะ",
            employeeId = "E3099",
            phone = "0893099000",
            cleanerKind = "Regular",
            homeBuildingId = buildingA,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await AccountApi.ReadAsync(response);
        Assert.Equal((null, (string?)null, "Regular", (int?)buildingA, "A"),
            (created.AreaId, created.Shift, created.CleanerKind, created.HomeBuildingId, created.HomeBuildingCode));

        var profile = await AdminSetupApi.ReadAsync(factory, db =>
            db.CleanerProfilePeriods.SingleAsync(p => p.UserId == created.Id));
        Assert.Equal((CleanerKind.Regular, (int?)buildingA, (Shift?)null, new DateOnly(2026, 11, 17)),
            (profile.Kind, profile.HomeBuildingId, profile.Shift, profile.EffectiveFrom));
    }

    [Fact]
    public async Task A_supervisor_cannot_be_given_a_cleaner_identity()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingB = await TestData.AddBuildingAsync(factory, "B");

        var response = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Supervisor", displayName = "หัวหน้าใหม่", employeeId = "E2010", phone = "0893010000",
            buildingId = buildingB, shift = "Day", cleanerKind = "Relief",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.WithDbAsync(async db => Assert.Empty(await db.CleanerProfilePeriods.ToListAsync()));
    }
}
