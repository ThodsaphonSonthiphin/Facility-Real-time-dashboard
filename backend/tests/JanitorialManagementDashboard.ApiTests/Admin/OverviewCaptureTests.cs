using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Admin;

public class OverviewCaptureTests
{
    [Fact]
    public async Task Setup_rename_and_revision_commit_together_without_duplicate_noop()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 6, 0));
        var id = await AdminSetupApi.BuildingIdAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 0));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(
            $"/api/admin/buildings/{id}", new { name = "อาคารใหม่" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(
            $"/api/admin/buildings/{id}", new { name = "อาคารใหม่" })).StatusCode);
        await factory.WithDbAsync(async db =>
        {
            var revisions = await db.OverviewTargetRevisions.OrderBy(r => r.Id).ToListAsync();
            Assert.Equal(2, revisions.Count);
            Assert.Equal(1, await db.AuditLog.CountAsync());
            Assert.Contains("อาคารใหม่", Image(revisions[1])
                .Buildings.Select(b => b.Name));
            Assert.Equal(revisions[1].Id, (await db.OverviewTargetStates.SingleAsync()).LatestRevisionId);
        });
    }

    [Fact]
    public async Task Point_creation_has_one_complete_image_with_the_printed_code_and_all_windows()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var start = ThaiClock.At(8, 6, 0);
        var areaId = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        await factory.WithDbAsync(async db =>
        {
            db.ServicePoints.Add(new()
            {
                AreaId = areaId, Name = "Unsigned point", IsActive = false, CreatedAt = start.UtcDateTime,
            });
            await db.SaveChangesAsync();
        });
        await OverviewTestData.StartHistoryAsync(factory, start);
        var created = ThaiClock.At(8, 9, 0);
        factory.Clock.SetUtcNow(created);
        var response = await admin.PostAsJsonAsync($"/api/admin/areas/{areaId}/points", new
        {
            name = "Store",
            roundWindows = new[] { new { shift = "Day", start = "10:00", end = "11:00" },
                new { shift = "Night", start = "21:00", end = "22:00" } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var revisions = await RevisionsAsync(factory);
        Assert.Equal(2, revisions.Count);
        var baseline = Image(revisions[0]);
        Assert.All(baseline.Windows, w => Assert.True(w.CreatedAt <= start.UtcDateTime));
        Assert.DoesNotContain(baseline.Points, p => p.Code == "AR01-03");
        var revision = revisions[1];
        var final = Image(revision);
        var unsigned = Assert.Single(final.Points, p => p.Name == "Unsigned point");
        Assert.Null(unsigned.Code);
        Assert.False(unsigned.IsActive);
        var point = Assert.Single(final.Points, p => p.Code == "AR01-03");
        Assert.Equal(areaId, point.AreaId);
        Assert.Equal("Store", point.Name);
        var windows = final.Windows.Where(w => w.PointId == point.Id).ToList();
        Assert.Equal(new[] { Shift.Day, Shift.Night }, windows.Select(w => w.Shift).Order());
        Assert.All(windows, w => Assert.Equal(created.UtcDateTime, w.CreatedAt));
        Assert.Equal(start.UtcDateTime, final.CaptureStartedAtUtc);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(revision.Payload))), revision.PayloadHash);
        using var document = JsonDocument.Parse(revision.Payload);
        AssertSafePropertyNames(document.RootElement);
    }

    [Fact]
    public async Task Staff_only_changes_keep_head_and_DayOnly_removes_all_night_windows_in_one_revision()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 6, 0));
        var areaId = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var buildingId = await AdminSetupApi.BuildingIdAsync(factory);
        var dayCleanerId = await AdminSetupApi.UserIdAsync(factory, "1000001");
        Task<HttpResponseMessage> Save(string shiftPattern) => admin.PutAsJsonAsync($"/api/admin/areas/{areaId}",
            new { name = "Area 1 ชั้น 1", buildingId, shiftPattern, dayCleanerId });
        Assert.Equal(HttpStatusCode.OK, (await Save("DayAndNight")).StatusCode);
        Assert.Single(await RevisionsAsync(factory));
        Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(HttpStatusCode.OK, (await Save("DayOnly")).StatusCode);
        var revisions = await RevisionsAsync(factory);
        Assert.Equal(2, revisions.Count);
        Assert.Equal(revisions[0].EffectiveAt, revisions[1].EffectiveAt);
        var before = Image(revisions[0]);
        var after = Image(revisions[1]);
        var points = before.Points.Where(p => p.AreaId == areaId).Select(p => p.Id).ToHashSet();
        var night = before.Windows.Where(w => points.Contains(w.PointId) && w.Shift == Shift.Night).ToHashSet();
        Assert.Equal(4, night.Count);
        Assert.Equal(before.Windows.Except(night), after.Windows);
        Assert.Equal(ShiftPattern.DayOnly, after.Areas.Single(a => a.Id == areaId).ShiftPattern);
        Assert.Null(after.CoverageGapFromUtc);
    }

    [Fact]
    public async Task Failed_revision_rolls_back_point_rename_replacement_windows_and_audit_and_retry_succeeds()
    {
        var fault = new RevisionFault();
        using var factory = new JanitorialApiFactory
        {
            ConfigureTestServices = services => services.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(fault)),
        };
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var head = await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 6, 0));
        var id = await AdminSetupApi.PointIdAsync(factory, "AR01-01");
        var baseline = Image(Assert.Single(await RevisionsAsync(factory)));
        var oldName = baseline.Points.Single(p => p.Id == id).Name;
        var oldWindowIds = baseline.Windows.Where(w => w.PointId == id).Select(w => w.Id);
        Task<HttpResponseMessage> Save() => admin.PutAsJsonAsync($"/api/admin/points/{id}", new
        {
            name = "New point name", roundWindows = new[] { new { shift = "Day", start = "10:00", end = "11:00" } },
        });
        fault.Armed = true;
        var failure = await Save();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
        Assert.Contains("ลองใหม่", await AdminSetupApi.MessageAsync(failure));
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(oldName, (await db.ServicePoints.SingleAsync(p => p.Id == id)).Name);
            Assert.Equal(oldWindowIds, await db.PointRoundWindows.Where(w => w.ServicePointId == id)
                .OrderBy(w => w.Id).Select(w => w.Id).ToListAsync());
            Assert.Empty(await db.AuditLog.ToListAsync());
            Assert.Equal(head, (await db.OverviewTargetStates.SingleAsync()).LatestRevisionId);
            Assert.Equal(head, (await db.OverviewTargetRevisions.SingleAsync()).Id);
        });
        fault.Armed = false;
        Assert.Equal(HttpStatusCode.OK, (await Save()).StatusCode);
        var revisions = await RevisionsAsync(factory);
        Assert.Equal(2, revisions.Count);
        var final = Image(revisions[1]);
        Assert.Equal("New point name", final.Points.Single(p => p.Id == id).Name);
        Assert.DoesNotContain(Assert.Single(final.Windows, w => w.PointId == id).Id, oldWindowIds);
        Assert.Single(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Bootstrap_marks_only_unobserved_structure_and_preserves_capture_start_across_restarts()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var start = ThaiClock.At(8, 6, 0);
        var first = await OverviewTestData.StartHistoryAsync(factory, start);
        await Assert.ThrowsAsync<InvalidOperationException>(() => OverviewTestData.StartHistoryAsync(factory, start));
        var settings = factory.Services.GetRequiredService<AttendanceSettings>();
        Task<long> RestartAsync() => AdminSetupApi.ReadAsync(factory,
            db => OverviewHistoryBootstrap.EnsureAsync(db, settings, factory.Clock, CancellationToken.None));
        Assert.Equal(first, await RestartAsync());
        var restart = ThaiClock.At(8, 8, 0).AddTicks(13);
        factory.Clock.SetUtcNow(restart);
        settings.ClosesMinutesAfterShift = 30;
        var policyId = await RestartAsync();
        Assert.True(policyId > first);
        Assert.Equal(policyId, await RestartAsync());

        var id = await AdminSetupApi.BuildingIdAsync(factory);
        await factory.WithDbAsync(db => db.Buildings.Where(b => b.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Name, "Edited offline")));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 0));
        var offlineId = await RestartAsync();
        Assert.True(offlineId > policyId);
        Assert.Equal(offlineId, await RestartAsync());
        var response = await admin.PutAsJsonAsync($"/api/admin/buildings/{id}", new { name = "Edited online" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var revisions = await RevisionsAsync(factory);
        Assert.Equal(4, revisions.Count);
        var policy = revisions[1];
        Assert.Equal(restart.UtcDateTime.AddTicks(-3), policy.EffectiveAt);
        Assert.Equal(30, Image(policy).Policy.ClosesMinutesAfterShift);
        Assert.Null(Image(policy).CoverageGapFromUtc);
        Assert.Equal(policy.EffectiveAt, Image(revisions[2]).CoverageGapFromUtc);
        Assert.Equal("Edited offline", Image(revisions[2]).Buildings.Single(b => b.Id == id).Name);
        Assert.Null(Image(revisions[3]).CoverageGapFromUtc);
        Assert.All(revisions, r => Assert.Equal(start.UtcDateTime, Image(r).CaptureStartedAtUtc));
    }

    [Fact]
    public async Task First_setup_write_records_pre_mutation_state_only_from_the_actual_fixture_instant()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var id = await AdminSetupApi.BuildingIdAsync(factory);
        Assert.Empty(await RevisionsAsync(factory));
        var now = ThaiClock.At(8, 10, 0);
        factory.Clock.SetUtcNow(now);
        var response = await admin.PutAsJsonAsync($"/api/admin/buildings/{id}", new { name = "Changed" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var revisions = await RevisionsAsync(factory);
        Assert.Equal(2, revisions.Count);
        Assert.All(revisions, r =>
        {
            Assert.Equal(now.UtcDateTime, r.EffectiveAt);
            Assert.Equal(now.UtcDateTime, Image(r).CaptureStartedAtUtc);
        });
        Assert.NotEqual("Changed", Image(revisions[0]).Buildings.Single(b => b.Id == id).Name);
        Assert.Equal("Changed", Image(revisions[1]).Buildings.Single(b => b.Id == id).Name);
    }

    private static Task<List<OverviewTargetRevision>> RevisionsAsync(JanitorialApiFactory factory) =>
        AdminSetupApi.ReadAsync(factory, db => db.OverviewTargetRevisions.OrderBy(r => r.Id).ToListAsync());

    private static TargetImage Image(OverviewTargetRevision revision) => JsonSerializer.Deserialize<TargetImage>(revision.Payload)!;

    private static void AssertSafePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                AssertSafePropertyNames(item);
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotContain(property.Name,
                    new[] { "User", "SecretHash", "QrToken", "Latitude", "Longitude", "DisplayName", "Phone" },
                    StringComparer.OrdinalIgnoreCase);
                AssertSafePropertyNames(property.Value);
            }
    }

    private sealed class RevisionFault : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<OverviewTargetRevision>().Any(r => r.State == EntityState.Added))
                throw new DbUpdateException("Synthetic revision write failure");
            return ValueTask.FromResult(result);
        }
    }
}
