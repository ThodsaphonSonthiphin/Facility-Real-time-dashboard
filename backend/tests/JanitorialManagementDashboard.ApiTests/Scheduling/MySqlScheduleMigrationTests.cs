using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// The migration is schema-only: a fresh database needs every scheduling table, and an existing database keeps its
/// legacy attendance facts and receives no guessed schedule history (ADR facility-0215).
/// </summary>
[Collection("MySQL")]
public class MySqlScheduleMigrationTests
{
    private const string PreviousMigration = "20261103080826_PointMonthlySummaries";

    private static DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;

    [MySqlFact]
    public async Task Fresh_database_migrates_with_all_scheduling_tables_and_no_pending_changes()
    {
        await using var db = new AppDbContext(Options());
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            await DbInitializer.SeedAsync(db, new Pbkdf2PasswordHasher(iterations: 1_000));

            // Legacy attendance still stores the exact four legacy names.
            var person = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var row = new ShiftAttendance
            {
                UserId = person.Id,
                AreaId = person.AreaId!.Value,
                ShiftDate = new DateOnly(2026, 11, 17),
                Shift = Shift.Day,
                EventType = AttendanceEvent.BreakOut,
                Source = AttendanceSource.AdminAdd,
                OccurredAt = new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc),
            };
            db.ShiftAttendances.Add(row);
            await db.SaveChangesAsync();
            var raw = await db.Database.SqlQueryRaw<string>(
                "SELECT event_type AS Value FROM shift_attendances WHERE id = {0}", row.Id).SingleAsync();
            Assert.Equal("BREAK_OUT", raw);

            // Weekly choices keep absent, confirmed-empty and selected weeks apart on MySQL dates.
            var week = new DateOnly(2026, 11, 16);
            var actor = await db.Users.SingleAsync(u => u.Role == UserRole.Supervisor);
            db.WeeklyDayOffs.Add(new WeeklyDayOff
            {
                UserId = person.Id,
                WeekStart = week,
                Revision = 1,
                CreatedById = actor.Id, // required provenance: MySQL enforces the created-by foreign key
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var empty = await db.WeeklyDayOffs.Include(x => x.Choices).SingleAsync();
            Assert.Equal(week, empty.WeekStart);
            Assert.Empty(empty.Choices);

            // The compatibility backfill reads the current setup only and is safe to run twice.
            await ScheduleBackfill.EnsureAsync(db, week, CancellationToken.None);
            await ScheduleBackfill.EnsureAsync(db, week, CancellationToken.None);
            Assert.Equal(3, await db.CleanerProfilePeriods.CountAsync());
            Assert.Equal(3, await db.ScheduleBaselines.CountAsync());
            Assert.Equal(21, await db.ScheduleBaselineDays.CountAsync());
            Assert.Single(await db.SupervisorScopePeriods.ToListAsync());

            // A second migration run has nothing to apply.
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [MySqlFact]
    public async Task Upgrade_preserves_legacy_attendance_and_creates_no_guessed_history()
    {
        await using var db = new AppDbContext(Options());
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await DbInitializer.SeedAsync(db, new Pbkdf2PasswordHasher(iterations: 1_000));

            var person = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var areaId = person.AreaId!.Value;
            var row = new ShiftAttendance
            {
                UserId = person.Id,
                AreaId = areaId,
                ShiftDate = new DateOnly(2026, 11, 17),
                Shift = Shift.Day,
                EventType = AttendanceEvent.BreakIn,
                Source = AttendanceSource.AdminEdit,
                OccurredAt = new DateTime(2026, 11, 17, 4, 30, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 11, 17, 4, 31, 0, DateTimeKind.Utc),
            };
            db.ShiftAttendances.Add(row);
            await db.SaveChangesAsync();

            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            db.ChangeTracker.Clear();

            var raw = await db.Database.SqlQueryRaw<string>(
                "SELECT event_type AS Value FROM shift_attendances WHERE id = {0}", row.Id).SingleAsync();
            Assert.Equal("BREAK_IN", raw);
            var stored = await db.ShiftAttendances.AsNoTracking().SingleAsync();
            Assert.Equal(AttendanceEvent.BreakIn, stored.EventType);
            Assert.Equal(AttendanceSource.AdminEdit, stored.Source);
            Assert.Equal(areaId, stored.AreaId);
            Assert.Equal(new DateOnly(2026, 11, 17), stored.ShiftDate);
            Assert.Equal(new DateTime(2026, 11, 17, 4, 30, 0), stored.OccurredAt);

            // The migration itself backfills nothing: no profile, baseline, scope or day-off history appears by guessing.
            Assert.Empty(await db.CleanerProfilePeriods.ToListAsync());
            Assert.Empty(await db.ScheduleBaselines.ToListAsync());
            Assert.Empty(await db.SupervisorScopePeriods.ToListAsync());
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());

            // The explicit compatibility backfill then records only the currently known regular setup.
            await ScheduleBackfill.EnsureAsync(db, new DateOnly(2026, 11, 17), CancellationToken.None);
            Assert.Equal(3, await db.CleanerProfilePeriods.CountAsync());
            Assert.Equal(3, await db.ScheduleBaselines.CountAsync());
            Assert.Equal(21, await db.ScheduleBaselineDays.CountAsync());
            Assert.Single(await db.SupervisorScopePeriods.ToListAsync());
            Assert.Empty(await db.ReliefAssignments.ToListAsync());
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());

            db.ChangeTracker.Clear();
            var afterBackfill = await db.ShiftAttendances.AsNoTracking().SingleAsync();
            Assert.Equal(AttendanceEvent.BreakIn, afterBackfill.EventType);
            Assert.Equal(areaId, afterBackfill.AreaId);
            Assert.Equal(new DateTime(2026, 11, 17, 4, 30, 0), afterBackfill.OccurredAt);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
