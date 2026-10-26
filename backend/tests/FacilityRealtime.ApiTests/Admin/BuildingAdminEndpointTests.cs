using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// facility-0090: the buildings page. Seed: building A ("ตึก A") with the active Areas AR01 and AR02 and the active
/// Supervisor S2001 (day). <c>TestData.AddBuildingAsync</c> adds an empty building named "ตึก {code}".
/// </summary>
public class BuildingAdminEndpointTests
{
    private const string Buildings = "/api/admin/buildings";

    [Fact]
    public async Task The_full_list_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().GetAsync($"{Buildings}/all");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/all")]
    [InlineData("POST", "")]
    [InlineData("PUT", "/1")]
    [InlineData("POST", "/1/deactivate")]
    [InlineData("POST", "/1/activate")]
    public async Task Cleaners_cannot_manage_buildings(string method, string suffix)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var body = method == "GET" ? null : JsonContent.Create(new { code = "B", name = "ตึก B" });

        var response = await cleaner.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"{Buildings}{suffix}") { Content = body });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_full_list_has_off_buildings_and_counts_what_is_active_in_each()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var b = await TestData.AddBuildingAsync(factory, "B");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"{Buildings}/{b}/deactivate", null)).StatusCode);

        var all = (await admin.GetFromJsonAsync<List<ManagedBuildingModel>>($"{Buildings}/all"))!;
        var active = (await admin.GetFromJsonAsync<List<BuildingModel>>(Buildings))!;

        Assert.Equal(
            new[] { ("A", "ตึก A", true, 2, 1), ("B", "ตึก B", false, 0, 0) },
            all.Select(x => (x.Code, x.Name, x.IsActive, x.ActiveAreaCount, x.ActiveSupervisorCount)));
        // the forms' short list stays the active buildings only
        Assert.Equal(new[] { "A" }, active.Select(x => x.Code));
    }

    [Fact]
    public async Task An_admin_adds_a_building_and_the_history_keeps_it()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Buildings, new { code = " b2 ", name = "  ตึกผลิต " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ManagedBuildingModel>())!;
        Assert.Equal(("B2", "ตึกผลิต", true, 0, 0), (created.Code, created.Name, created.IsActive, created.ActiveAreaCount, created.ActiveSupervisorCount));
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("BUILDING_CREATE", "buildings", (long?)created.Id, "เพิ่มตึก B2 ตึกผลิต"), (entry.Action, entry.EntityType, entry.EntityId, entry.Summary));
        Assert.Null(entry.BeforeJson);
        // the log's JSON escapes Thai, so it is read back, not compared as text
        var after = JsonDocument.Parse(entry.AfterJson!).RootElement;
        Assert.Equal(("B2", "ตึกผลิต"), (after.GetProperty("Code").GetString(), after.GetProperty("Name").GetString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A-1")]
    [InlineData("อาคาร1")]
    [InlineData("ABCDEFGHIJ01234567890")]
    public async Task A_code_that_breaks_the_rule_is_refused(string code)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Buildings, new { code, name = "ตึกผลิต" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("รหัสตึกต้องเป็นตัวอักษรอังกฤษหรือตัวเลข 1–20 ตัว เช่น B2", await AdminSetupApi.MessageAsync(response));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task A_building_needs_a_name(string? name)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Buildings, new { code = "B", name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ต้องใส่ชื่อตึก ไม่เกิน 100 ตัวอักษร", await AdminSetupApi.MessageAsync(response));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    public async Task A_code_is_never_used_twice_even_by_an_off_building(string code)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var b = await TestData.AddBuildingAsync(factory, "B");
        await admin.PostAsync($"{Buildings}/{b}/deactivate", null);

        var response = await admin.PostAsJsonAsync(Buildings, new { code, name = "ตึกใหม่" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("รหัสตึกนี้มีแล้ว", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task Two_buildings_may_share_a_name()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await admin.PostAsJsonAsync(Buildings, new { code = "B", name = "ตึก A" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_rename_changes_the_name_only_and_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var a = await AdminSetupApi.BuildingIdAsync(factory);

        var response = await admin.PutAsJsonAsync($"{Buildings}/{a}", new { code = "Z", name = " อาคารผลิต 1 " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ManagedBuildingModel>())!;
        Assert.Equal(("A", "อาคารผลิต 1"), (saved.Code, saved.Name));
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("BUILDING_UPDATE", "แก้ตึก A อาคารผลิต 1"), (entry.Action, entry.Summary));
        Assert.Equal("ตึก A", JsonDocument.Parse(entry.BeforeJson!).RootElement.GetProperty("Name").GetString());
        Assert.Equal("อาคารผลิต 1", JsonDocument.Parse(entry.AfterJson!).RootElement.GetProperty("Name").GetString());
    }

    [Fact]
    public async Task A_save_that_changes_nothing_writes_no_history()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var a = await AdminSetupApi.BuildingIdAsync(factory);

        var response = await admin.PutAsJsonAsync($"{Buildings}/{a}", new { name = "ตึก A" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_rename_needs_a_name()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var a = await AdminSetupApi.BuildingIdAsync(factory);

        var response = await admin.PutAsJsonAsync($"{Buildings}/{a}", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ต้องใส่ชื่อตึก ไม่เกิน 100 ตัวอักษร", await AdminSetupApi.MessageAsync(response));
    }

    [Theory]
    [InlineData("PUT", "")]
    [InlineData("POST", "/deactivate")]
    [InlineData("POST", "/activate")]
    public async Task Unknown_buildings_are_not_found(string method, string suffix)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var body = method == "PUT" ? JsonContent.Create(new { name = "ตึกใหม่" }) : null;

        var response = await admin.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"{Buildings}/9999{suffix}") { Content = body });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ไม่พบตึกนี้", await AdminSetupApi.MessageAsync(response));
    }

    [Fact]
    public async Task A_building_with_active_areas_or_supervisors_cannot_be_turned_off()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var a = await AdminSetupApi.BuildingIdAsync(factory);

        var response = await admin.PostAsync($"{Buildings}/{a}/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "ปิดตึก A ไม่ได้ ยังมีพื้นที่ที่เปิดอยู่ 2 แห่ง และหัวหน้าประจำตึก 1 คน ปิดหรือย้ายออกก่อน",
            await AdminSetupApi.MessageAsync(response));
        Assert.Contains("A", (await admin.GetFromJsonAsync<List<BuildingModel>>(Buildings))!.Select(x => x.Code));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Off_areas_and_off_supervisors_do_not_keep_a_building_on()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var b = await TestData.AddBuildingAsync(factory, "B");
        var area = await TestData.AddAreaAsync(factory, "AR03", "B");
        var supervisor = await TestData.AddSupervisorAsync(factory, "S3001", "0830000001", "B", Shift.Day);

        var refused = await admin.PostAsync($"{Buildings}/{b}/deactivate", null);
        await admin.PostAsync($"/api/admin/areas/{area}/deactivate", null);
        await admin.PostAsync($"{AccountApi.Path}/{supervisor}/deactivate", null);
        var off = await admin.PostAsync($"{Buildings}/{b}/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(
            "ปิดตึก B ไม่ได้ ยังมีพื้นที่ที่เปิดอยู่ 1 แห่ง และหัวหน้าประจำตึก 1 คน ปิดหรือย้ายออกก่อน",
            await AdminSetupApi.MessageAsync(refused));
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await off.Content.ReadFromJsonAsync<ManagedBuildingModel>())!.IsActive);
    }

    [Fact]
    public async Task An_empty_building_turns_off_and_on_and_each_change_is_logged_once()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var b = await TestData.AddBuildingAsync(factory, "B");

        var off = await admin.PostAsync($"{Buildings}/{b}/deactivate", null);
        var offAgain = await admin.PostAsync($"{Buildings}/{b}/deactivate", null);
        var whileOff = (await admin.GetFromJsonAsync<List<BuildingModel>>(Buildings))!;
        var on = await admin.PostAsync($"{Buildings}/{b}/activate", null);
        var afterOn = (await admin.GetFromJsonAsync<List<BuildingModel>>(Buildings))!;

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.Equal(HttpStatusCode.OK, offAgain.StatusCode);
        Assert.DoesNotContain("B", whileOff.Select(x => x.Code));
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.True((await on.Content.ReadFromJsonAsync<ManagedBuildingModel>())!.IsActive);
        Assert.Contains("B", afterOn.Select(x => x.Code));
        var audit = await AdminSetupApi.AuditAsync(factory);
        Assert.Equal(new[] { "BUILDING_DEACTIVATE", "BUILDING_ACTIVATE" }, audit.Select(x => x.Action));
        Assert.Equal(new[] { "ปิดใช้งานตึก B", "เปิดใช้งานตึก B" }, audit.Select(x => x.Summary));
    }
}
