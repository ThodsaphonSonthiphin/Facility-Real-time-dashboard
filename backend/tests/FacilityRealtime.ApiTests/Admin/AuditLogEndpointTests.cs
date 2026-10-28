using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Wireframe D9, facility-0057: the Admin history page is read-only, newest first, filtered by date, actor and type.</summary>
public class AuditLogEndpointTests
{
    private const string Path = "/api/admin/audit-log";

    /// <summary>One row written by the seeded Admin at <paramref name="at"/>, as an Admin endpoint would write it.</summary>
    private static Task AddEntryAsync(FacilityApiFactory factory, string action, DateTimeOffset at, object? after = null) =>
        factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            AuditTrail.Add(db, admin.Id, at.UtcDateTime, action, "signs", 1, $"ทดสอบ {action}", before: null, after: after ?? new { Value = 1 });
            await db.SaveChangesAsync();
        });

    private static async Task<AuditLogPageModel> PageAsync(HttpClient admin, string query = "")
    {
        var response = await admin.GetAsync(Path + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuditLogPageModel>())!;
    }

    [Fact]
    public async Task The_log_is_newest_first_with_actor_category_and_snapshots()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddEntryAsync(factory, "SIGN_RADIUS", ThaiClock.At(8, 9, 0), new { RadiusM = 80 });
        await AddEntryAsync(factory, "COVER_ASSIGN", ThaiClock.At(8, 9, 5));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var page = await PageAsync(admin);

        Assert.Equal(new[] { "COVER_ASSIGN", "SIGN_RADIUS" }, page.Entries.Select(e => e.Action));
        Assert.Equal(new[] { "Cover", "Setup" }, page.Entries.Select(e => e.Category));
        Assert.All(page.Entries, e => Assert.Equal("ผู้ดูแลระบบ", e.ActorName));
        Assert.All(page.Entries, e => Assert.Equal(adminAuth.User.Id, e.ActorId));
        Assert.Equal(ThaiClock.At(8, 9, 0).UtcDateTime, page.Entries[1].OccurredAt);
        Assert.Equal(80, page.Entries[1].After!.Value.GetProperty("RadiusM").GetInt32());
        Assert.Null(page.Entries[1].Before);
        Assert.Null(page.NextBeforeId);
    }

    [Fact]
    public async Task The_log_filters_by_category_actor_and_dates()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddEntryAsync(factory, "SIGN_RADIUS", ThaiClock.At(8, 9, 0));
        await AddEntryAsync(factory, "COVER_ASSIGN", ThaiClock.At(8, 9, 5));
        await AddEntryAsync(factory, "QR_REGENERATE", ThaiClock.At(1, 9, 0));   // before the default 7 days
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "E1001");

        Assert.Equal(new[] { "COVER_ASSIGN", "SIGN_RADIUS" }, (await PageAsync(admin)).Entries.Select(e => e.Action));
        Assert.Equal(new[] { "SIGN_RADIUS" }, (await PageAsync(admin, "?category=setup")).Entries.Select(e => e.Action));
        Assert.Empty((await PageAsync(admin, "?category=Attendance")).Entries);
        Assert.Empty((await PageAsync(admin, $"?actorId={cleanerId}")).Entries);
        Assert.Empty((await PageAsync(admin, "?from=2026-10-07&to=2026-10-07")).Entries);
        Assert.Equal(new[] { "QR_REGENERATE" }, (await PageAsync(admin, "?from=2026-10-01&to=2026-10-01")).Entries.Select(e => e.Action));
    }

    /// <summary>Plan 12 decision 7, owner 2026-10-28: the search covers every row in the range, not only the rows loaded.</summary>
    [Fact]
    public async Task The_search_finds_summary_actor_and_reason_in_every_row()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            var adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;
            AuditTrail.Add(db, adminId, ThaiClock.At(8, 9, 0).UtcDateTime, "SIGN_RADIUS", "signs", 1, "รัศมีป้าย AR01-01", null, new { RadiusM = 80 });
            AuditTrail.Add(db, adminId, ThaiClock.At(8, 9, 1).UtcDateTime, "ATTENDANCE_CORRECT", "attendance", 2, "แก้เวลาเข้า", null, new { V = 1 }, reason: "ลืมสแกน 50%_off");
            AuditTrail.Add(db, actorId: null, ThaiClock.At(8, 9, 2).UtcDateTime, "ADMIN_FIRST_CREATE", "users", 3, "สร้างแอดมินคนแรก", null, new { V = 1 });
            await db.SaveChangesAsync();
        });
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        async Task<string[]> Found(string q) =>
            (await PageAsync(admin, "?q=" + Uri.EscapeDataString(q))).Entries.Select(e => e.Action).ToArray();

        Assert.Equal(new[] { "SIGN_RADIUS" }, await Found("ar01"));                      // summary, either case
        Assert.Equal(new[] { "ATTENDANCE_CORRECT" }, await Found("ลืมสแกน"));            // reason
        // the system as actor (ADMIN_FIRST_CREATE's summary lacks the word); the seed Admin is "ผู้ดูแลระบบ"
        Assert.Equal(new[] { "ADMIN_FIRST_CREATE", "ATTENDANCE_CORRECT", "SIGN_RADIUS" }, await Found("ระบบ"));
        Assert.Equal(new[] { "ATTENDANCE_CORRECT", "SIGN_RADIUS" }, await Found("ผู้ดูแล")); // the actor's name
        Assert.Equal(new[] { "ATTENDANCE_CORRECT" }, await Found("50%_"));               // % and _ are plain text
        Assert.Empty(await Found("50%off"));                                             // a wildcard % would match
        Assert.Equal(3, (await Found("  ")).Length);                                        // blank is no search
    }

    [Fact]
    public async Task Older_pages_of_a_search_come_with_before_id()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddEntryAsync(factory, "SIGN_RADIUS", ThaiClock.At(8, 9, 0));
        await AddEntryAsync(factory, "COVER_ASSIGN", ThaiClock.At(8, 9, 1));
        await AddEntryAsync(factory, "SIGN_LOCATION", ThaiClock.At(8, 9, 2));
        await AddEntryAsync(factory, "SIGN_RADIUS", ThaiClock.At(8, 9, 3));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var first = await PageAsync(admin, "?q=SIGN_R&limit=1");
        var second = await PageAsync(admin, $"?q=SIGN_R&limit=1&beforeId={first.NextBeforeId}");
        var third = await PageAsync(admin, $"?q=SIGN_R&limit=1&beforeId={second.NextBeforeId}");

        Assert.Equal(ThaiClock.At(8, 9, 3).UtcDateTime, Assert.Single(first.Entries).OccurredAt);
        Assert.Equal(ThaiClock.At(8, 9, 0).UtcDateTime, Assert.Single(second.Entries).OccurredAt);
        Assert.Empty(third.Entries);
    }

    [Fact]
    public async Task Older_pages_come_with_before_id()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddEntryAsync(factory, "SIGN_RADIUS", ThaiClock.At(8, 9, 0));
        await AddEntryAsync(factory, "SIGN_LOCATION", ThaiClock.At(8, 9, 1));
        await AddEntryAsync(factory, "COVER_ASSIGN", ThaiClock.At(8, 9, 2));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var first = await PageAsync(admin, "?limit=2");
        var second = await PageAsync(admin, $"?limit=2&beforeId={first.NextBeforeId}");

        Assert.Equal(new[] { "COVER_ASSIGN", "SIGN_LOCATION" }, first.Entries.Select(e => e.Action));
        Assert.Equal(first.Entries[1].Id, first.NextBeforeId);
        Assert.Equal(new[] { "SIGN_RADIUS" }, second.Entries.Select(e => e.Action));
        Assert.Null(second.NextBeforeId);
    }

    [Fact]
    public async Task An_action_no_category_lists_still_shows()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await AddEntryAsync(factory, "SOMETHING_NEW", ThaiClock.At(8, 9, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var entry = Assert.Single((await PageAsync(admin)).Entries);

        Assert.Equal("SOMETHING_NEW", entry.Action);
        Assert.Null(entry.Category);
    }

    [Fact]
    public async Task A_real_admin_change_appears_and_reading_the_log_is_not_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 0));
        var signId = await AdminSetupApi.SignIdAsync(factory, "AR01-01");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/signs/{signId}/radius", new { radiusM = 80 })).StatusCode);

        var entry = Assert.Single((await PageAsync(admin)).Entries);
        await PageAsync(admin);

        Assert.Equal("SIGN_RADIUS", entry.Action);
        Assert.Equal("Setup", entry.Category);
        Assert.Equal(50, entry.Before!.Value.GetProperty("RadiusM").GetInt32());
        Assert.Single(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Actors_are_everyone_who_has_a_line()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);

        Assert.Empty((await admin.GetFromJsonAsync<List<AuditActorModel>>(Path + "/actors"))!);

        await AddEntryAsync(factory, "SIGN_RADIUS", ThaiClock.At(8, 9, 0));

        Assert.Equal(
            new[] { new AuditActorModel(adminAuth.User.Id, "ผู้ดูแลระบบ") },
            await admin.GetFromJsonAsync<List<AuditActorModel>>(Path + "/actors"));
    }

    [Theory]
    [InlineData("?category=Nope")]
    [InlineData("?limit=0")]
    [InlineData("?limit=501")]
    [InlineData("?from=2026-10-01")]
    [InlineData("?from=2026-01-01&to=2026-10-08")]
    [InlineData("?q=" + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Bad_filters_are_refused(string query)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Path + query)).StatusCode);
    }

    /// <summary>facility-0092: the first Admin and reset-admin are written by the server itself, with no Admin logged in.</summary>
    [Fact]
    public async Task A_row_the_system_wrote_shows_the_system_as_its_actor()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            AuditTrail.Add(db, actorId: null, ThaiClock.At(8, 9, 0).UtcDateTime, "ADMIN_FIRST_CREATE", "users", 1,
                "สร้างแอดมินคนแรก admin ผู้ดูแลระบบ จากค่าในเซิร์ฟเวอร์", before: null, after: new { Role = "Admin" });
            await db.SaveChangesAsync();
        });
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));

        var entry = Assert.Single((await PageAsync(admin)).Entries);

        Assert.Equal(("ระบบ", (int?)null, "Account"), (entry.ActorName, entry.ActorId, entry.Category));
        Assert.Empty((await admin.GetFromJsonAsync<List<AuditActorModel>>(Path + "/actors"))!);
    }

    [Fact]
    public async Task Nobody_can_change_the_log()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.DeleteAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.PutAsJsonAsync(Path, new { })).StatusCode);
    }

    [Fact]
    public async Task Only_admins_read_the_log()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateApiClient().GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cleaner.GetAsync(Path + "/actors")).StatusCode);
    }
}
