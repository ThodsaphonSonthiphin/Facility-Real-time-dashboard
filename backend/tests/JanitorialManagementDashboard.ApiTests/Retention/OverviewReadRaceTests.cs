using System.Data;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JanitorialManagementDashboard.ApiTests.Retention;

[Collection("MySQL")]
public class OverviewReadRaceTests
{
    private static DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    private static OverviewReadQuery Query(AppDbContext db, DateTime nowUtc, RetentionSettings? retention = null) =>
        new(
            db,
            new ScheduleStore(db),
            retention ?? new RetentionSettings(),
            new FixedClock(nowUtc),
            NullLogger<OverviewReadQuery>.Instance);

    [MySqlFact]
    public async Task RepeatableRead_keeps_snapshot_consistent_while_retention_prunes_facts_and_targets()
    {
        await using var setup = new AppDbContext(Options());
        await setup.Database.EnsureDeletedAsync();
        try
        {
            await setup.Database.MigrateAsync();
            await DbInitializer.SeedAsync(setup, new Pbkdf2PasswordHasher(iterations: 1_000));

            var nowUtc = new DateTime(2026, 11, 19, 4, 0, 0, DateTimeKind.Utc);
            var settings = new RetentionSettings { RecordYears = 1, BatchSize = 10 };
            var expiredDate = new DateOnly(2025, 10, 15); // full month before 1-year retention cutoff
            var expiredSlot = new ShiftSlot(expiredDate, Shift.Day);
            var captureStart = ShiftCalendar.StartUtc(expiredSlot).AddDays(-2);

            await using (var lease = await new OverviewMutation(setup,
                new OverviewTargetRecorder(setup, new AttendanceSettings(), new FixedClock(captureStart))).BeginAsync(default))
            {
                await lease.CommitAsync(default);
            }

            var building = await setup.Buildings.FirstAsync();
            await using (var lease = await new OverviewMutation(setup,
                new OverviewTargetRecorder(setup, new AttendanceSettings(), new FixedClock(captureStart.AddHours(1)))).BeginAsync(default))
            {
                building.Name = "Renamed Before Lower";
                await setup.SaveChangesAsync();
                await lease.CommitAsync(default);
            }

            var cleaner = await setup.Users.Where(u => u.Role == UserRole.Cleaner).OrderBy(u => u.Id).FirstAsync();
            var point = await setup.ServicePoints.OrderBy(p => p.Id).FirstAsync();
            var sign = await setup.Signs.SingleAsync(s => s.ServicePointId == point.Id);
            var window = await setup.PointRoundWindows.Where(w => w.ServicePointId == point.Id && w.Shift == Shift.Day).FirstAsync();
            var scanAt = ShiftCalendar.StartUtc(expiredSlot).AddMinutes(15);

            setup.ShiftAttendances.Add(new ShiftAttendance
            {
                UserId = cleaner.Id,
                AreaId = sign.AreaId,
                ShiftDate = expiredDate,
                Shift = Shift.Day,
                EventType = AttendanceEvent.ShiftIn,
                Source = AttendanceSource.Scan,
                OccurredAt = scanAt,
                CreatedAt = scanAt,
            });
            setup.ScanRecords.Add(new ScanRecord
            {
                ServicePointId = point.Id,
                SignId = sign.Id,
                UserId = cleaner.Id,
                ShiftDate = expiredDate,
                Shift = Shift.Day,
                RoundWindowId = window.Id,
                RoundOriginId = window.Id,
                RoundStart = window.StartTime,
                RoundEnd = window.EndTime,
                Placement = Placement.OnTime,
                Status = CleaningStatus.Normal,
                SubmittedAt = scanAt.AddMinutes(5),
            });
            await setup.SaveChangesAsync();

            var readerReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var writerCommitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            int snapshotRevCount = 0, snapshotScanCount = 0, snapshotAttCount = 0;
            var readerTask = Task.Run(async () =>
            {
                await using var dbReader = new AppDbContext(Options());
                await using var tx = await dbReader.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, timeout.Token);
                snapshotRevCount = await dbReader.OverviewTargetRevisions.CountAsync(timeout.Token);
                readerReady.SetResult();
                await writerCommitted.Task.WaitAsync(timeout.Token);
                snapshotScanCount = await dbReader.ScanRecords.Where(s => s.ShiftDate == expiredDate).CountAsync(timeout.Token);
                snapshotAttCount = await dbReader.ShiftAttendances.Where(a => a.ShiftDate == expiredDate).CountAsync(timeout.Token);
                await tx.CommitAsync(timeout.Token);
            }, timeout.Token);

            await readerReady.Task.WaitAsync(timeout.Token);
            await using (var dbWriter = new AppDbContext(Options()))
            {
                var pruned = await new RetentionRunner(dbWriter, new FixedClock(nowUtc), settings).RunAsync(timeout.Token);
                Assert.True(pruned.RecordsDeleted > 0);
            }
            writerCommitted.SetResult();
            await readerTask;

            Assert.Equal(2, snapshotRevCount);
            Assert.Equal(1, snapshotScanCount);
            Assert.Equal(1, snapshotAttCount);

            await using var after = new AppDbContext(Options());
            Assert.Equal(0, await after.ScanRecords.Where(s => s.ShiftDate == expiredDate).CountAsync());
            var readAfter = await Query(after, nowUtc, settings).ReadAsync(expiredSlot, null, default);
            Assert.NotEqual("complete", readAfter.Snapshot.Availability.Entry.State);
            Assert.NotEqual("complete", readAfter.Snapshot.Availability.Work.State);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [MySqlFact]
    public async Task Retention_anchor_deletion_race_under_singleton_lock_keeps_head_fk_and_anchor()
    {
        await using var setup = new AppDbContext(Options());
        await setup.Database.EnsureDeletedAsync();
        try
        {
            await setup.Database.MigrateAsync();
            await DbInitializer.SeedAsync(setup, new Pbkdf2PasswordHasher(iterations: 1_000));

            var nowUtc = new DateTime(2026, 11, 19, 4, 0, 0, DateTimeKind.Utc);
            var settings = new RetentionSettings { BatchSize = 5 };
            var oldTime = nowUtc.AddYears(-2);

            await using (var l1 = await new OverviewMutation(setup,
                new OverviewTargetRecorder(setup, new AttendanceSettings(), new FixedClock(oldTime))).BeginAsync(default))
            {
                await l1.CommitAsync(default);
            }
            var b = await setup.Buildings.FirstAsync();
            await using (var l2 = await new OverviewMutation(setup,
                new OverviewTargetRecorder(setup, new AttendanceSettings(), new FixedClock(oldTime.AddDays(1)))).BeginAsync(default))
            {
                b.Name = "Anchor Candidate";
                await setup.SaveChangesAsync();
                await l2.CommitAsync(default);
            }

            var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pruneStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pruneDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var mutator = Task.Run(async () =>
            {
                await using var dbMut = new AppDbContext(Options());
                await using var lease = await new OverviewMutation(dbMut,
                    new OverviewTargetRecorder(dbMut, new AttendanceSettings(), new FixedClock(nowUtc))).BeginAsync(timeout.Token);
                b = await dbMut.Buildings.FirstAsync(timeout.Token);
                b.Name = "Current Head Name";
                await dbMut.SaveChangesAsync(timeout.Token);
                locked.SetResult();
                await release.Task.WaitAsync(timeout.Token);
                await lease.CommitAsync(timeout.Token);
            }, timeout.Token);

            var pruner = Task.Run(async () =>
            {
                await locked.Task.WaitAsync(timeout.Token);
                await using var dbPrune = new AppDbContext(Options());
                pruneStarted.SetResult();
                await OverviewTargetRetention.PruneAsync(dbPrune, nowUtc, settings, timeout.Token);
                pruneDone.SetResult();
            }, timeout.Token);

            await pruneStarted.Task.WaitAsync(timeout.Token);
            await Task.Delay(150, timeout.Token);
            Assert.False(pruneDone.Task.IsCompleted);

            release.SetResult();
            await Task.WhenAll(mutator, pruner);

            await using var verify = new AppDbContext(Options());
            var state = await verify.OverviewTargetStates.AsNoTracking().SingleAsync();
            var remainingIds = await verify.OverviewTargetRevisions.AsNoTracking().OrderBy(r => r.Id).Select(r => r.Id).ToListAsync();
            Assert.NotNull(state.LatestRevisionId);
            Assert.Contains(state.LatestRevisionId!.Value, remainingIds);
            Assert.Equal(2, remainingIds.Count);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [MySqlFact]
    public async Task Genuine_zero_events_with_full_history_versus_partial_annual_attendance_deletion()
    {
        await using var setup = new AppDbContext(Options());
        await setup.Database.EnsureDeletedAsync();
        try
        {
            await setup.Database.MigrateAsync();
            await DbInitializer.SeedAsync(setup, new Pbkdf2PasswordHasher(iterations: 1_000));

            var nowUtc = new DateTime(2026, 11, 19, 4, 0, 0, DateTimeKind.Utc);
            var settings = new RetentionSettings { RecordYears = 1 };
            var baselineStart = new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc);
            await using (var lease = await new OverviewMutation(setup,
                new OverviewTargetRecorder(setup, new AttendanceSettings(), new FixedClock(baselineStart))).BeginAsync(default))
            {
                await lease.CommitAsync(default);
            }

            var emptyDate = new DateOnly(2026, 11, 10);
            var emptyRead = await Query(setup, nowUtc, settings).ReadAsync(new ShiftSlot(emptyDate, Shift.Day), null, default);
            Assert.Equal("complete", emptyRead.Snapshot.Availability.Entry.State);
            Assert.Equal("complete", emptyRead.Snapshot.Availability.Work.State);
            Assert.Equal(0, emptyRead.Snapshot.Areas.Count(a => a.Entry == "entered"));

            var partialDate = new DateOnly(2025, 11, 19);
            var partialRead = await Query(setup, nowUtc, settings).ReadAsync(new ShiftSlot(partialDate, Shift.Day), null, default);
            Assert.NotEqual("complete", partialRead.Snapshot.Availability.Entry.State);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [MySqlFact]
    public async Task Boundary_cases_midnight_night_policy_change_and_issue_without_earlier_source()
    {
        await using var setup = new AppDbContext(Options());
        await setup.Database.EnsureDeletedAsync();
        try
        {
            await setup.Database.MigrateAsync();
            await DbInitializer.SeedAsync(setup, new Pbkdf2PasswordHasher(iterations: 1_000));

            var slotDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
            var nightSlot = new ShiftSlot(slotDate, Shift.Night);
            var startUtc = ShiftCalendar.StartUtc(nightSlot);

            var initialPolicy = new AttendanceSettings { OpensMinutesBeforeShift = 60, ClosesMinutesAfterShift = 60 };
            await using (var lease = await new OverviewMutation(setup,
                new OverviewTargetRecorder(setup, initialPolicy, new FixedClock(startUtc.AddHours(-3)))).BeginAsync(default))
            {
                await lease.CommitAsync(default);
            }

            var cleaner = await setup.Users.Where(u => u.Role == UserRole.Cleaner).OrderBy(u => u.Id).FirstAsync();
            var point = await setup.ServicePoints.OrderBy(p => p.Id).FirstAsync();
            var sign = await setup.Signs.SingleAsync(s => s.ServicePointId == point.Id);
            var nightWindow = await setup.PointRoundWindows
                .Where(w => w.ServicePointId == point.Id && w.Shift == Shift.Night && w.StartTime == new TimeOnly(3, 0))
                .FirstAsync();
            var midnightAt = startUtc.AddHours(8); // 20:00 UTC = 03:00 Bangkok next calendar day (inside 03:00-05:00 round)

            setup.ShiftAttendances.Add(new ShiftAttendance
            {
                UserId = cleaner.Id,
                AreaId = sign.AreaId,
                ShiftDate = slotDate,
                Shift = Shift.Night,
                EventType = AttendanceEvent.ShiftIn,
                Source = AttendanceSource.Scan,
                OccurredAt = midnightAt,
                CreatedAt = midnightAt,
            });
            setup.ScanRecords.Add(new ScanRecord
            {
                ServicePointId = point.Id,
                SignId = sign.Id,
                UserId = cleaner.Id,
                ShiftDate = slotDate,
                Shift = Shift.Night,
                RoundWindowId = null,
                RoundOriginId = nightWindow.Id,
                RoundStart = nightWindow.StartTime,
                RoundEnd = nightWindow.EndTime,
                Placement = Placement.OnTime,
                Status = CleaningStatus.Issue,
                IssueTags = "wet_floor",
                SubmittedAt = midnightAt.AddMinutes(2),
            });
            await setup.SaveChangesAsync();

            var laterUtc = startUtc.AddDays(2);
            var changedPolicy = new AttendanceSettings { OpensMinutesBeforeShift = 15, ClosesMinutesAfterShift = 15 };
            await using (var lease = await new OverviewMutation(setup,
                new OverviewTargetRecorder(setup, changedPolicy, new FixedClock(laterUtc))).BeginAsync(default))
            {
                await lease.CommitAsync(default);
            }

            var read = await Query(setup, laterUtc.AddHours(1)).ReadAsync(nightSlot, null, default);
            Assert.Equal(1, read.Snapshot.Areas.Count(a => a.Entry == "entered"));
            Assert.Equal(1, read.Snapshot.Areas.Count(a => a.Work.Issue));
            Assert.Equal(60, read.Snapshot.Targets.Current!.Policy.OpensMinutesBeforeShift);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }
}
