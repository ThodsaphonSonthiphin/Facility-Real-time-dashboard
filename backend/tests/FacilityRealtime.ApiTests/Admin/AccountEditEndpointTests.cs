using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Wireframe D6: edit, deactivate and activate. Deactivation logs the person out everywhere (facility-0014, 0020).</summary>
public class AccountEditEndpointTests
{
    private static Task<HttpResponseMessage> EditAsync(HttpClient admin, int id, object body) =>
        admin.PutAsJsonAsync($"{AccountApi.Path}/{id}", body);

    private static Task<HttpResponseMessage> DeactivateAsync(HttpClient admin, int id) =>
        admin.PostAsync($"{AccountApi.Path}/{id}/deactivate", null);

    private static Task<HttpResponseMessage> ActivateAsync(HttpClient admin, int id) =>
        admin.PostAsync($"{AccountApi.Path}/{id}/activate", null);

    [Fact]
    public async Task The_admin_renames_a_cleaner_and_moves_them_to_a_free_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1003 = await AdminSetupApi.UserIdAsync(factory, "E1003");
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03");

        var response = await EditAsync(admin, e1003, new { displayName = "สมศรี ย้ายที่", areaId = ar03, shift = "Day" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = await AccountApi.ReadAsync(response);
        Assert.Equal(("สมศรี ย้ายที่", "AR03", "E1003"), (edited.DisplayName, edited.AreaCode, edited.LoginName));
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("ACCOUNT_UPDATE", (long?)e1003), (entry.Action, entry.EntityId));
        Assert.Contains($"\"AreaId\":{ar02}", entry.BeforeJson);
        Assert.Contains($"\"AreaId\":{ar03}", entry.AfterJson);
    }

    [Fact]
    public async Task Saving_the_same_values_logs_nothing()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await EditAsync(admin, e1001, new { displayName = " สมชาย ใจดี ", areaId = ar01, shift = "Day" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Moving_into_a_taken_slot_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1003 = await AdminSetupApi.UserIdAsync(factory, "E1003");
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await EditAsync(admin, e1003, new { displayName = "สมศรี มีสุข", areaId = ar01, shift = "Day" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของ AR01 มีแม่บ้านประจำแล้ว (สมชาย ใจดี)", await AdminSetupApi.MessageAsync(response));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_supervisor_is_moved_to_another_building_shift()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var s2001 = await AdminSetupApi.UserIdAsync(factory, "S2001");
        var buildingB = await TestData.AddBuildingAsync(factory, "B");

        var response = await EditAsync(admin, s2001, new { displayName = "สมปอง ตรวจดี", buildingId = buildingB, shift = "Night" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = await AccountApi.ReadAsync(response);
        Assert.Equal(("B", "Night"), (edited.BuildingCode, edited.Shift));
    }

    [Fact]
    public async Task Deactivating_a_cleaner_logs_them_out_and_frees_their_area_slot()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (_, _, cleanerRefresh) = await AuthApi.LoggedInAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await DeactivateAsync(admin, e1001);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await AccountApi.ReadAsync(response);
        Assert.Equal((false, (int?)null), (account.IsActive, account.AreaId));
        // The endpoint itself revokes the sessions; Refresh would answer 401 for an inactive account even without that
        var tokens = await AdminSetupApi.ReadAsync(factory, db => db.RefreshTokens.Where(t => t.UserId == e1001).ToListAsync());
        Assert.NotEmpty(tokens);
        Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(factory.CreateApiClient(), cleanerRefresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.LoginAsync(factory.CreateApiClient())).StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("ACCOUNT_DEACTIVATE", entry.Action);
        Assert.Contains($"\"AreaId\":{ar01}", entry.BeforeJson);
        Assert.Contains("\"IsActive\":false", entry.AfterJson);

        // AR01's day slot is open again
        Assert.Equal(HttpStatusCode.Created, (await AccountApi.CreateCleanerAsync(admin, "E2001", "0000000011", ar01, "Day")).StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_their_own_account()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await DeactivateAsync(admin, adminAuth.User.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ปิดบัญชีของตัวเองไม่ได้", await AdminSetupApi.MessageAsync(response));
        Assert.True(await AdminSetupApi.ReadAsync(factory, db => db.Users.Where(u => u.Id == adminAuth.User.Id).Select(u => u.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task Deactivating_twice_logs_once()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1002 = await AdminSetupApi.UserIdAsync(factory, "E1002");

        await DeactivateAsync(admin, e1002);
        var again = await DeactivateAsync(admin, e1002);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task An_activated_cleaner_logs_in_again_without_an_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        await DeactivateAsync(admin, e1001);

        var response = await ActivateAsync(admin, e1001);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await AccountApi.ReadAsync(response);
        Assert.Equal((true, (int?)null), (account.IsActive, account.AreaId));
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(factory.CreateApiClient())).StatusCode);
        Assert.Equal(new[] { "ACCOUNT_DEACTIVATE", "ACCOUNT_ACTIVATE" }, (await AdminSetupApi.AuditAsync(factory)).Select(e => e.Action));
    }

    [Fact]
    public async Task A_supervisor_whose_slot_was_filled_cannot_be_activated()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var s2001 = await AdminSetupApi.UserIdAsync(factory, "S2001");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        await DeactivateAsync(admin, s2001);
        Assert.Equal(HttpStatusCode.Created, (await AccountApi.CreateSupervisorAsync(admin, "S2002", "0000000021", buildingA, "Day", "หัวหน้าแทน ทดสอบ")).StatusCode);

        var response = await ActivateAsync(admin, s2001);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของตึก A มีหัวหน้าประจำแล้ว (หัวหน้าแทน ทดสอบ)", await AdminSetupApi.MessageAsync(response));
        Assert.False(await AdminSetupApi.ReadAsync(factory, db => db.Users.Where(u => u.Id == s2001).Select(u => u.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task A_deactivated_supervisor_whose_slot_was_filled_can_still_be_renamed()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var s2001 = await AdminSetupApi.UserIdAsync(factory, "S2001");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        await DeactivateAsync(admin, s2001);
        Assert.Equal(HttpStatusCode.Created, (await AccountApi.CreateSupervisorAsync(admin, "S2002", "0000000021", buildingA, "Day", "หัวหน้าแทน ทดสอบ")).StatusCode);

        var response = await EditAsync(admin, s2001, new { displayName = "สมปอง เปลี่ยนชื่อ", buildingId = buildingA, shift = "Day" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("สมปอง เปลี่ยนชื่อ", (await AccountApi.ReadAsync(response)).DisplayName);
        var updates = (await AdminSetupApi.AuditAsync(factory)).Where(e => e.Action == "ACCOUNT_UPDATE").ToList();
        Assert.Equal((long?)s2001, Assert.Single(updates).EntityId);
        Assert.Equal(HttpStatusCode.Conflict, (await ActivateAsync(admin, s2001)).StatusCode);
    }

    [Fact]
    public async Task A_cleaner_whose_area_was_deactivated_can_be_renamed_and_keeps_the_area()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1003 = await AdminSetupApi.UserIdAsync(factory, "E1003");
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        // only data from before facility-0091 has a Cleaner on an Area that is off
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Id == ar02)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await EditAsync(admin, e1003, new { displayName = "สมศรี เปลี่ยนชื่อ", areaId = ar02, shift = "Day" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = await AccountApi.ReadAsync(response);
        Assert.Equal(("สมศรี เปลี่ยนชื่อ", ar02), (edited.DisplayName, edited.AreaId));
    }

    [Fact]
    public async Task Moving_a_cleaner_to_a_deactivated_area_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Id == ar02)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await EditAsync(admin, e1001, new { displayName = "สมชาย ใจดี", areaId = ar02, shift = "Day" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ต้องเลือกพื้นที่ที่ใช้งานอยู่", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task Leaving_the_area_out_of_an_edit_clears_it_and_the_history_shows_it()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        var response = await EditAsync(admin, e1001, new { displayName = "สมชาย ใจดี", shift = "Day" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await AccountApi.ReadAsync(response)).AreaId);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("ACCOUNT_UPDATE", (long?)e1001), (entry.Action, entry.EntityId));
        Assert.Contains($"\"AreaId\":{ar01}", entry.BeforeJson);
        Assert.Contains("\"AreaId\":null", entry.AfterJson);
    }

    [Fact]
    public async Task A_deactivated_cleaner_gets_no_area_until_activated()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03");
        await DeactivateAsync(admin, e1001);

        var response = await EditAsync(admin, e1001, new { displayName = "สมชาย ใจดี", areaId = ar03, shift = "Day" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("เปิดใช้งานบัญชีก่อน แล้วค่อยเลือกพื้นที่", await AdminSetupApi.MessageAsync(response));
    }

    [Theory]
    [InlineData("PUT", "/api/admin/accounts/9999")]
    [InlineData("POST", "/api/admin/accounts/9999/deactivate")]
    [InlineData("POST", "/api/admin/accounts/9999/activate")]
    public async Task An_unknown_account_is_404(string method, string path)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "PUT" ? JsonContent.Create(new { displayName = "ก", shift = "Day" }) : null,
        };

        var response = await admin.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ไม่พบบัญชีนี้", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_cleaner_cannot_edit_accounts()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await DeactivateAsync(cleaner, auth.User.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await EditAsync(cleaner, auth.User.Id, new { displayName = "ก", shift = "Day" })).StatusCode);
    }
}
