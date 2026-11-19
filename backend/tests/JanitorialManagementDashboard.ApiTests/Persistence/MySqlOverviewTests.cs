using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace JanitorialManagementDashboard.ApiTests.Persistence;

[Collection("MySQL")]
public class MySqlOverviewTests
{
    private const string PreviousMigration = "20261117121357_AddScheduleCommandScope";
    private const string CurrentMigration = "20261119013534_OverviewTargetHistory";

    private static DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;

    private static OverviewTargetRecorder Recorder(AppDbContext db, DateTime utcNow) =>
        new(db, new AttendanceSettings(), new FixedClock(utcNow));

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    [MySqlFact]
    public async Task Upgrades_existing_records_without_inventing_unlinked_origins_and_survives_down_up()
    {
        await using var db = new AppDbContext(Options());
        await db.Database.EnsureDeletedAsync();
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await DbInitializer.SeedAsync(db, new Pbkdf2PasswordHasher(iterations: 1_000));

            var pointId = await db.ServicePoints.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
            var signId = await db.Signs.Where(s => s.ServicePointId == pointId).Select(s => s.Id).FirstAsync();
            var cleanerId = await db.Users.Where(u => u.Role == UserRole.Cleaner).OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
            var window = await db.PointRoundWindows.Where(w => w.ServicePointId == pointId).OrderBy(w => w.Id).FirstAsync();

            var shiftDate = new DateOnly(2026, 11, 10);
            var submittedAt = new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO scan_records
                    (service_point_id, sign_id, user_id, shift_date, shift, round_window_id, round_start, round_end, placement, status, submitted_at)
                VALUES
                    ({pointId}, {signId}, {cleanerId}, {shiftDate}, {"DAY"}, {window.Id}, {window.StartTime}, {window.EndTime}, {"ON_TIME"}, {"NORMAL"}, {submittedAt}),
                    ({pointId}, {signId}, {cleanerId}, {shiftDate}, {"DAY"}, {null}, {window.StartTime}, {window.EndTime}, {"LATE"}, {"NORMAL"}, {submittedAt.AddMinutes(5)}),
                    ({pointId}, {signId}, {cleanerId}, {shiftDate}, {"DAY"}, {null}, {null}, {null}, {"OFF_ROUND"}, {"NORMAL"}, {submittedAt.AddMinutes(10)});
                """);

            await migrator.MigrateAsync(CurrentMigration);
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(1, await db.OverviewTargetStates.CountAsync());
            Assert.Empty(await db.OverviewTargetRevisions.ToListAsync());

            db.ChangeTracker.Clear();
            var scans = await db.ScanRecords.OrderBy(s => s.Id).ToListAsync();
            Assert.Equal(3, scans.Count);
            Assert.Equal(window.Id, scans[0].RoundOriginId);
            Assert.Null(scans[1].RoundOriginId);
            Assert.Null(scans[2].RoundOriginId);
            Assert.Equal(Placement.OffRound, scans[2].Placement);

            await migrator.MigrateAsync(PreviousMigration);
            await migrator.MigrateAsync(CurrentMigration);
            db.ChangeTracker.Clear();
            Assert.Equal(3, await db.ScanRecords.CountAsync());
            Assert.Equal(1, await db.OverviewTargetStates.CountAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [MySqlFact]
    public async Task Concurrent_mutations_serialize_and_produce_coherent_revisions_in_id_order()
    {
        await using var setup = new AppDbContext(Options());
        await setup.Database.EnsureDeletedAsync();
        try
        {
            await setup.Database.MigrateAsync();
            await DbInitializer.SeedAsync(setup, new Pbkdf2PasswordHasher(iterations: 1_000));
            var t0 = new DateTime(2026, 11, 19, 1, 0, 0, DateTimeKind.Utc);
            await using (var init = await new OverviewMutation(setup, Recorder(setup, t0)).BeginAsync(default))
            {
                await init.CommitAsync(default);
            }

            var buildingId = await setup.Buildings.Select(b => b.Id).FirstAsync();
            var areaId = await setup.Areas.Select(a => a.Id).FirstAsync();

            var aEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var bStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var bAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var taskA = Task.Run(async () =>
            {
                await using var dbA = new AppDbContext(Options());
                await using var lease = await new OverviewMutation(dbA, Recorder(dbA, t0.AddMinutes(1))).BeginAsync(timeout.Token);
                var b = await dbA.Buildings.SingleAsync(x => x.Id == buildingId, timeout.Token);
                b.Name = "Building Renamed By A";
                await dbA.SaveChangesAsync(timeout.Token);
                aEntered.SetResult();
                await releaseA.Task.WaitAsync(timeout.Token);
                await lease.CommitAsync(timeout.Token);
            }, timeout.Token);

            var taskB = Task.Run(async () =>
            {
                await aEntered.Task.WaitAsync(timeout.Token);
                await using var dbB = new AppDbContext(Options());
                bStarted.SetResult();
                await using var lease = await new OverviewMutation(dbB, Recorder(dbB, t0.AddMinutes(2))).BeginAsync(timeout.Token);
                bAcquired.SetResult();
                var area = await dbB.Areas.SingleAsync(x => x.Id == areaId, timeout.Token);
                area.Name = "Area Renamed By B";
                await dbB.SaveChangesAsync(timeout.Token);
                await lease.CommitAsync(timeout.Token);
            }, timeout.Token);

            await bStarted.Task.WaitAsync(timeout.Token);
            await Task.Delay(150, timeout.Token);
            Assert.False(bAcquired.Task.IsCompleted);

            releaseA.SetResult();
            await Task.WhenAll(taskA, taskB);

            await using var verify = new AppDbContext(Options());
            var revisions = await verify.OverviewTargetRevisions.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
            var state = await verify.OverviewTargetStates.AsNoTracking().SingleAsync();
            Assert.Equal(3, revisions.Count);
            Assert.Equal(revisions[^1].Id, state.LatestRevisionId);

            var imgA = JsonSerializer.Deserialize<TargetImage>(revisions[1].Payload)!;
            var imgB = JsonSerializer.Deserialize<TargetImage>(revisions[2].Payload)!;
            Assert.Equal("Building Renamed By A", imgA.Buildings.Single(b => b.Id == buildingId).Name);
            Assert.NotEqual("Area Renamed By B", imgA.Areas.Single(a => a.Id == areaId).Name);
            Assert.Equal("Building Renamed By A", imgB.Buildings.Single(b => b.Id == buildingId).Name);
            Assert.Equal("Area Renamed By B", imgB.Areas.Single(a => a.Id == areaId).Name);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [MySqlFact]
    public async Task Rollback_discards_intermediate_changes_and_same_timestamp_revisions_sort_by_id()
    {
        await using var setup = new AppDbContext(Options());
        await setup.Database.EnsureDeletedAsync();
        try
        {
            await setup.Database.MigrateAsync();
            await DbInitializer.SeedAsync(setup, new Pbkdf2PasswordHasher(iterations: 1_000));
            var t0 = new DateTime(2026, 11, 19, 1, 0, 0, DateTimeKind.Utc);
            await using (var init = await new OverviewMutation(setup, Recorder(setup, t0)).BeginAsync(default))
            {
                await init.CommitAsync(default);
            }

            var area = await setup.Areas.FirstAsync();
            var originalPattern = area.ShiftPattern;
            var pointCountBefore = await setup.ServicePoints.CountAsync();
            var revisionCountBefore = await setup.OverviewTargetRevisions.CountAsync();

            // Writer A performs intermediate SaveChanges (DayOnly + window delete + new point) then throws before commit.
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await using var dbA = new AppDbContext(Options());
                await using var lease = await new OverviewMutation(dbA, Recorder(dbA, t0.AddMinutes(1))).BeginAsync(default);
                var targetArea = await dbA.Areas.SingleAsync(a => a.Id == area.Id);
                targetArea.ShiftPattern = ShiftPattern.DayOnly;
                var nightWindows = await dbA.PointRoundWindows
                    .Where(w => w.Shift == Shift.Night)
                    .ToListAsync();
                dbA.PointRoundWindows.RemoveRange(nightWindows);
                dbA.ServicePoints.Add(new ServicePoint
                {
                    AreaId = area.Id,
                    Name = "Rolled Back Point",
                    SortOrder = 99,
                    IsActive = true,
                    CreatedAt = t0.AddMinutes(1),
                });
                await dbA.SaveChangesAsync();
                throw new InvalidOperationException("Simulated failure before commit");
            });

            await using (var check = new AppDbContext(Options()))
            {
                Assert.Equal(revisionCountBefore, await check.OverviewTargetRevisions.CountAsync());
                Assert.Equal(pointCountBefore, await check.ServicePoints.CountAsync());
                var persistedArea = await check.Areas.SingleAsync(a => a.Id == area.Id);
                Assert.Equal(originalPattern, persistedArea.ShiftPattern);
            }

            // Two sequential commits with identical timestamp sort by ID.
            var sameInstant = t0.AddMinutes(5);
            await using (var dbB = new AppDbContext(Options()))
            await using (var lease = await new OverviewMutation(dbB, Recorder(dbB, sameInstant)).BeginAsync(default))
            {
                var targetArea = await dbB.Areas.SingleAsync(a => a.Id == area.Id);
                targetArea.Name = "First Same Timestamp";
                await dbB.SaveChangesAsync();
                await lease.CommitAsync(default);
            }

            await using (var dbC = new AppDbContext(Options()))
            await using (var lease = await new OverviewMutation(dbC, Recorder(dbC, sameInstant)).BeginAsync(default))
            {
                var targetArea = await dbC.Areas.SingleAsync(a => a.Id == area.Id);
                targetArea.Name = "Second Same Timestamp";
                await dbC.SaveChangesAsync();
                await lease.CommitAsync(default);
            }

            await using var verify = new AppDbContext(Options());
            var rows = await verify.OverviewTargetRevisions.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
            var (decoded, unreadable, gaps) = OverviewTargetRecorder.DecodeForRead(rows, NullLogger.Instance);
            Assert.Empty(unreadable);
            Assert.Empty(gaps);
            Assert.Equal("Second Same Timestamp", decoded[^1].Image.Areas.Single(a => a.Id == area.Id).Name);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }
}
