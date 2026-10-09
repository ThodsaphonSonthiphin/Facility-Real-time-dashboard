using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

public record CoverModel(int Id, int UserId, string CleanerName, int AreaId, string AreaCode, DateOnly ShiftDate, string Shift, DateTime AssignedAt);

/// <summary>facility-0041 + owner decision 2026-10-09: covers are per shift, only in the covering Cleaner's own regular shift.</summary>
public class CoverAssignmentEndpointTests
{
    private const string Path = "/api/admin/cover-assignments";

    private static async Task<int> UserIdAsync(FacilityApiFactory factory, string employeeId)
    {
        var id = 0;
        await factory.WithDbAsync(async db => id = (await db.Users.SingleAsync(u => u.EmployeeId == employeeId)).Id);
        return id;
    }

    private static async Task<int> AreaIdAsync(FacilityApiFactory factory, string code)
    {
        var id = 0;
        await factory.WithDbAsync(async db => id = (await db.Areas.SingleAsync(a => a.Code == code)).Id);
        return id;
    }

    private static async Task<HttpResponseMessage> AssignAsync(
        FacilityApiFactory factory, HttpClient admin, string employeeId, string areaCode, int day, string shift) =>
        await admin.PostAsJsonAsync(Path, new
        {
            userId = await UserIdAsync(factory, employeeId),
            areaId = await AreaIdAsync(factory, areaCode),
            shiftDate = new DateOnly(2026, 10, day),
            shift,
        });

    private static async Task<List<AuditEntry>> AuditAsync(FacilityApiFactory factory)
    {
        var rows = new List<AuditEntry>();
        await factory.WithDbAsync(async db => rows = await db.AuditLog.OrderBy(a => a.Id).ToListAsync());
        return rows;
    }

    [Fact]
    public async Task Admin_assigns_a_cover_and_it_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cover = (await response.Content.ReadFromJsonAsync<CoverModel>())!;
        Assert.Equal("สมชาย ใจดี", cover.CleanerName);
        Assert.Equal("AR02", cover.AreaCode);
        var entry = Assert.Single(await AuditAsync(factory));
        Assert.Equal("COVER_ASSIGN", entry.Action);
        Assert.Equal(adminAuth.User.Id, entry.ActorId);
        Assert.Equal("cover_assignments", entry.EntityType);
        Assert.Equal(cover.Id, entry.EntityId);
    }

    [Fact]
    public async Task Only_admins_assign()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await AssignAsync(factory, cleaner, "E1001", "AR02", 8, "Day");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("E1001", "AR01", 8, "Day")]     // the Cleaner's own Area
    [InlineData("E1001", "AR02", 8, "Night")]   // not the Cleaner's regular shift
    [InlineData("S2001", "AR02", 8, "Day")]     // not a Cleaner
    [InlineData("E1002", "AR02", 8, "Night")]   // AR02 works days only
    [InlineData("E1001", "AR02", 7, "Day")]     // that shift already ended
    [InlineData("E1002", "AR02", 8, "Day")]     // Night Cleaner, Day Area: refused only by the regular-shift rule
    public async Task Invalid_covers_are_refused(string employeeId, string areaCode, int day, string shift)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await AssignAsync(factory, admin, employeeId, areaCode, day, shift);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AuditAsync(factory));
    }

    [Fact]
    public async Task Duplicate_active_cover_is_a_conflict()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day");

        var again = await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Admin_cancels_a_cover_and_it_is_logged()
    {
        using var factory = new FacilityApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var cover = (await (await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day")).Content.ReadFromJsonAsync<CoverModel>())!;

        var cancel = await admin.DeleteAsync($"{Path}/{cover.Id}");
        var again = await admin.DeleteAsync($"{Path}/{cover.Id}");

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        CoverAssignment? stored = null;
        await factory.WithDbAsync(async db => stored = await db.CoverAssignments.SingleAsync());
        Assert.NotNull(stored!.CancelledAt);
        Assert.Equal(adminAuth.User.Id, stored.CancelledById);
        Assert.Equal(new[] { "COVER_ASSIGN", "COVER_CANCEL" }, (await AuditAsync(factory)).Select(a => a.Action));
    }

    [Fact]
    public async Task Missing_shift_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await admin.PostAsJsonAsync(Path, new
        {
            userId = await UserIdAsync(factory, "E1001"),
            areaId = await AreaIdAsync(factory, "AR02"),
            shiftDate = new DateOnly(2026, 10, 8),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AuditAsync(factory));
    }

    [Fact]
    public async Task Deactivated_cleaner_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "E1001")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AuditAsync(factory));
    }

    [Fact]
    public async Task Inactive_area_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Code == "AR02")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AuditAsync(factory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unknown_user_or_area_is_refused(bool unknownUser)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));

        var response = await admin.PostAsJsonAsync(Path, new
        {
            userId = unknownUser ? 99999 : await UserIdAsync(factory, "E1001"),
            areaId = unknownUser ? await AreaIdAsync(factory, "AR02") : 99999,
            shiftDate = new DateOnly(2026, 10, 8),
            shift = "Day",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await AuditAsync(factory));
    }

    [Fact]
    public async Task A_cancelled_cover_can_be_assigned_again()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var first = (await (await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day")).Content.ReadFromJsonAsync<CoverModel>())!;
        await admin.DeleteAsync($"{Path}/{first.Id}");

        var again = await AssignAsync(factory, admin, "E1001", "AR02", 8, "Day");

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Cancelling_closes_every_identical_active_cover()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 30));
        var first = await TestData.AddCoverAsync(factory, "E1001", "AR02", new DateOnly(2026, 10, 8), Shift.Day);
        await TestData.AddCoverAsync(factory, "E1001", "AR02", new DateOnly(2026, 10, 8), Shift.Day);

        var cancel = await admin.DeleteAsync($"{Path}/{first}");

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var covers = new List<CoverAssignment>();
        await factory.WithDbAsync(async db => covers = await db.CoverAssignments.ToListAsync());
        Assert.Equal(2, covers.Count);
        Assert.All(covers, c => Assert.NotNull(c.CancelledAt));
        Assert.Single((await AuditAsync(factory)).Where(a => a.Action == "COVER_CANCEL"));
    }
}
