using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// facility-0054: "แก้เบอร์โทร" for Cleaners and Supervisors, "รีเซ็ตรหัสผ่าน" for Admins. Either one logs the person out on
/// every device, and neither the old nor the new value ever reaches the log (facility-0057).
/// </summary>
public class AccountSecretEndpointTests
{
    private static Task<HttpResponseMessage> PhoneAsync(HttpClient admin, int id, string? phone) =>
        admin.PutAsJsonAsync($"{AccountApi.Path}/{id}/phone", new { phone });

    private static Task<HttpResponseMessage> PasswordAsync(HttpClient admin, int id, string? password) =>
        admin.PutAsJsonAsync($"{AccountApi.Path}/{id}/password", new { password });

    [Fact]
    public async Task A_new_phone_replaces_the_old_one_and_logs_the_cleaner_out()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (_, _, cleanerRefresh) = await AuthApi.LoggedInAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var response = await PhoneAsync(admin, e1001, "000 000 0031");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.LoginAsync(factory.CreateApiClient(), "E1001", "0810000001")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(factory.CreateApiClient(), "E1001", "0000000031")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(factory.CreateApiClient(), cleanerRefresh)).StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("ACCOUNT_PHONE", (long?)e1001, (string?)null, (string?)null), (entry.Action, entry.EntityId, entry.BeforeJson, entry.AfterJson));
        Assert.Equal("แก้เบอร์โทรของ E1001 สมชาย ใจดี", entry.Summary);
    }

    [Fact]
    public async Task The_same_phone_again_changes_nothing()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (_, _, cleanerRefresh) = await AuthApi.LoggedInAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var response = await PhoneAsync(admin, e1001, "081-000-0001");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.RefreshAsync(factory.CreateApiClient(), cleanerRefresh)).StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Theory]
    [InlineData("๐๐๐๐๐๐๐๐๓๑")]
    [InlineData("000000003")]
    [InlineData(null)]
    public async Task An_incomplete_phone_is_refused(string? phone)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var response = await PhoneAsync(admin, e1001, phone);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_supervisor_phone_can_be_changed_too()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var s2001 = await AdminSetupApi.UserIdAsync(factory, "S2001");

        Assert.Equal(HttpStatusCode.NoContent, (await PhoneAsync(admin, s2001, "0000000041")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(factory.CreateApiClient(), "S2001", "0000000041")).StatusCode);
    }

    [Fact]
    public async Task An_admin_has_no_phone_and_a_cleaner_has_no_password()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");

        var phone = await PhoneAsync(admin, adminAuth.User.Id, "0000000051");
        var password = await PasswordAsync(admin, e1001, "new-password-1");

        Assert.Equal(HttpStatusCode.BadRequest, phone.StatusCode);
        Assert.Equal("บัญชีแอดมินไม่มีเบอร์โทร ใช้ปุ่มรีเซ็ตรหัสผ่าน", await AdminSetupApi.MessageAsync(phone));
        Assert.Equal(HttpStatusCode.BadRequest, password.StatusCode);
        Assert.Equal("แม่บ้านและหัวหน้าไม่มีรหัสผ่าน ใช้ปุ่มแก้เบอร์โทร", await AdminSetupApi.MessageAsync(password));
    }

    [Fact]
    public async Task A_reset_password_lets_the_other_admin_in_with_the_new_one_only()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var created = await AccountApi.ReadAsync(await AccountApi.CreateAdminAsync(admin, "admin2", "temporary-pass-1"));
        var other = await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin2", "temporary-pass-1");
        var otherRefresh = AuthApi.RefreshCookieValue(other);

        var response = await PasswordAsync(admin, created.Id, "temporary-pass-2");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin2", "temporary-pass-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.AdminLoginAsync(factory.CreateApiClient(), "admin2", "temporary-pass-2")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(factory.CreateApiClient(), otherRefresh)).StatusCode);
        var entry = (await AdminSetupApi.AuditAsync(factory)).Last();
        Assert.Equal(("ACCOUNT_PASSWORD", (string?)null, (string?)null), (entry.Action, entry.BeforeJson, entry.AfterJson));
    }

    [Theory]
    [InlineData("temporary-1234", "รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร")]
    [InlineData("1qaz2wsx3edc4rfv", "รหัสผ่านนี้คนใช้กันเยอะ เดาง่าย ตั้งรหัสอื่น")]
    public async Task A_short_or_common_password_is_refused_with_its_reason(string password, string message)
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await PasswordAsync(admin, adminAuth.User.Id, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task An_unknown_account_is_404()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await PhoneAsync(admin, 9999, "0000000061")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PasswordAsync(admin, 9999, "temporary-pass-9")).StatusCode);
    }

    /// <summary>The check docs/handoff.md asked for before account actions are logged: the history page never shows a secret.</summary>
    [Fact]
    public async Task The_history_page_never_shows_a_phone_a_password_a_hash_or_a_qr_token()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var e1001 = await AdminSetupApi.UserIdAsync(factory, "E1001");
        var created = await AccountApi.ReadAsync(await AccountApi.CreateCleanerAsync(admin, "E2001", "0000000071", areaId: null));
        await PhoneAsync(admin, e1001, "0000000072");
        var newAdmin = await AccountApi.ReadAsync(await AccountApi.CreateAdminAsync(admin, "admin2", "temporary-pass-71"));
        await PasswordAsync(admin, newAdmin.Id, "temporary-pass-72");
        await admin.PostAsync($"{AccountApi.Path}/{created.Id}/deactivate", null);
        var signId = await AdminSetupApi.SignIdAsync(factory, "AR01-01");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/signs/{signId}/regenerate-token", null)).StatusCode);
        var newToken = await AdminSetupApi.ReadAsync(factory, db => db.Signs.Where(s => s.Id == signId).Select(s => s.QrToken).SingleAsync());

        var raw = await admin.GetStringAsync("/api/admin/audit-log");

        Assert.Equal(6, (await AdminSetupApi.AuditAsync(factory)).Count);
        foreach (var secret in new[] { "0000000071", "0000000072", "0810000001", "temporary-pass-71", "temporary-pass-72", "pbkdf2", "token-", newToken })
        {
            Assert.DoesNotContain(secret, raw);
        }
    }
}
