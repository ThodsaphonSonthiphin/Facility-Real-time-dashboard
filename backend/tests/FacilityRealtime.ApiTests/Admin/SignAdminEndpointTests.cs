using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0045 (capture at the sign, map as fallback), 0038 (radius), 0006/0021 (new QR Token).</summary>
public class SignAdminEndpointTests
{
    private static async Task<HttpResponseMessage> LocateAsync(
        FacilityApiFactory factory, HttpClient admin, string signCode, double? latitude, double? longitude, double? accuracyM, string? source) =>
        await admin.PutAsJsonAsync(
            $"/api/admin/signs/{await AdminSetupApi.SignIdAsync(factory, signCode)}/location",
            new { latitude, longitude, accuracyM, source });

    [Fact]
    public async Task A_location_captured_at_the_sign_is_stored_logged_and_used_by_the_next_scan()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (e1003, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E1003", "0810000003");

        var response = await LocateAsync(factory, admin, "AR02-IN", 13.757, 100.502, 17.2, "Site");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sign = (await response.Content.ReadFromJsonAsync<AdminSignModel>())!;
        Assert.Equal(13.757m, sign.Latitude);
        Assert.Equal(100.502m, sign.Longitude);
        Assert.Equal((short)18, sign.LocationAccuracyM);
        Assert.Equal("Site", sign.LocationSource);
        Assert.NotNull(sign.LocatedAt);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("SIGN_LOCATION", entry.Action);
        Assert.Equal("signs", entry.EntityType);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var checkIn = await AttendanceApi.RecordAsync(e1003, "ShiftIn", "token-checkin-ar02", 13.757, 100.502, 10);

        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        var attendance = await AdminSetupApi.ReadAsync(factory, db => db.ShiftAttendances.AsNoTracking().SingleAsync());
        Assert.True(attendance.WithinRadius); // before the capture AR02-IN gave no verdict
    }

    [Fact]
    public async Task A_map_pick_has_no_accuracy_and_stays_unconfirmed()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await LocateAsync(factory, admin, "AR02-01", 13.757, 100.502, 25, "Map");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sign = (await response.Content.ReadFromJsonAsync<AdminSignModel>())!;
        Assert.Equal("Map", sign.LocationSource);
        Assert.Null(sign.LocationAccuracyM);
    }

    [Fact]
    public async Task A_map_pick_cannot_replace_a_location_captured_on_site()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await LocateAsync(factory, admin, "AR01-IN", 13.757, 100.502, null, "Map");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Theory]
    [InlineData(13.757, 100.502, 10.0, null)]     // no source
    [InlineData(null, 100.502, 10.0, "Site")]     // no latitude
    [InlineData(13.757, 100.502, null, "Site")]   // a capture at the sign has an accuracy
    [InlineData(200.0, 100.502, 10.0, "Site")]    // impossible latitude
    public async Task Incomplete_locations_are_refused(double? latitude, double? longitude, double? accuracyM, string? source)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await LocateAsync(factory, admin, "AR02-IN", latitude, longitude, accuracyM, source);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Refused_locations_are_worded_for_the_admin_not_for_scanning()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var missing = await LocateAsync(factory, admin, "AR02-IN", null, 100.502, 10.0, "Site");
        var impossible = await LocateAsync(factory, admin, "AR02-IN", 200.0, 100.502, 10.0, "Site");

        Assert.Equal("ต้องส่งพิกัด (ละติจูด ลองจิจูด) และความแม่นยำเมื่อเก็บที่หน้างาน", await AdminSetupApi.MessageAsync(missing));
        Assert.Equal("พิกัดที่ส่งมาไม่ถูกต้อง ลองเก็บพิกัดใหม่อีกครั้ง", await AdminSetupApi.MessageAsync(impossible));
    }

    [Fact]
    public async Task Radius_changes_are_bounded_and_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var path = $"/api/admin/signs/{await AdminSetupApi.SignIdAsync(factory, "AR01-01")}/radius";

        var tooSmall = await admin.PutAsJsonAsync(path, new { radiusM = 9 });
        var tooLarge = await admin.PutAsJsonAsync(path, new { radiusM = 501 });
        var changed = await admin.PutAsJsonAsync(path, new { radiusM = 80 });
        var same = await admin.PutAsJsonAsync(path, new { radiusM = 80 });

        Assert.Equal(HttpStatusCode.BadRequest, tooSmall.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(80, (await changed.Content.ReadFromJsonAsync<AdminSignModel>())!.RadiusM);
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("SIGN_RADIUS", entry.Action);
        Assert.Equal(50, JsonDocument.Parse(entry.BeforeJson!).RootElement.GetProperty("RadiusM").GetInt32());
        Assert.Equal(80, JsonDocument.Parse(entry.AfterJson!).RootElement.GetProperty("RadiusM").GetInt32());
    }

    [Fact]
    public async Task A_new_qr_token_retires_the_printed_sign_and_the_log_never_holds_tokens()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));

        var response = await admin.PostAsync($"/api/admin/signs/{await AdminSetupApi.SignIdAsync(factory, "AR01-01")}/regenerate-token", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sign = (await response.Content.ReadFromJsonAsync<AdminSignModel>())!;
        Assert.True(Guid.TryParse(sign.QrToken, out _));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/service-points/by-token/token-restroom-m1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/service-points/by-token/{sign.QrToken}")).StatusCode);
        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal("QR_REGENERATE", entry.Action);
        Assert.DoesNotContain("token-restroom-m1", entry.BeforeJson);
        Assert.DoesNotContain(sign.QrToken, entry.AfterJson);
    }

    [Fact]
    public async Task A_deactivated_points_sign_gets_no_new_qr_token()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await admin.PostAsync($"/api/admin/points/{await AdminSetupApi.PointIdAsync(factory, "AR01-01")}/deactivate", null);

        var response = await admin.PostAsync($"/api/admin/signs/{await AdminSetupApi.SignIdAsync(factory, "AR01-01")}/regenerate-token", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Saving_the_same_location_again_changes_and_logs_nothing()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var first = await LocateAsync(factory, admin, "AR02-01", 13.757, 100.502, null, "Map");
        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await LocateAsync(factory, admin, "AR02-01", 13.757, 100.502, null, "Map");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstSign = (await first.Content.ReadFromJsonAsync<AdminSignModel>())!;
        var secondSign = (await second.Content.ReadFromJsonAsync<AdminSignModel>())!;
        Assert.Equal(firstSign.LocatedAt, secondSign.LocatedAt);
        Assert.Single(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_sign_in_a_deactivated_area_gets_no_new_qr_token()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var areaId = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/areas/{areaId}/deactivate", null)).StatusCode);

        var response = await admin.PostAsync($"/api/admin/signs/{await AdminSetupApi.SignIdAsync(factory, "AR02-IN")}/regenerate-token", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_huge_accuracy_is_clamped_to_the_column_range()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var response = await LocateAsync(factory, admin, "AR02-IN", 13.757, 100.502, 40000, "Site");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(short.MaxValue, (await response.Content.ReadFromJsonAsync<AdminSignModel>())!.LocationAccuracyM);
    }

    [Fact]
    public async Task Cleaners_cannot_move_signs()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await LocateAsync(factory, cleaner, "AR01-01", 13.757, 100.502, 10, "Site");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
