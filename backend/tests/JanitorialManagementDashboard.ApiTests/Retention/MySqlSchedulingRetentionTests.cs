using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Scheduling;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Retention;

/// <summary>
/// Spec §12 and A27 on the real provider: the scheduling detail rides the existing retention run, so the pruning deletes,
/// the half-open boundary selection, the integer-keyed GPS update on the transfer arrival and the receipt purge must all
/// work on MySQL and not only on SQLite.
/// </summary>
[Collection("MySQL")]
public class MySqlSchedulingRetentionTests
{
    [MySqlFact]
    public async Task Scheduling_detail_prunes_by_boundary_and_clears_arrival_gps_on_mysql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            await DbInitializer.SeedAsync(db, new Pbkdf2PasswordHasher(iterations: 1_000));

            var now = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
            var clock = new TestTimeProvider(now);
            var admin = await db.Users.SingleAsync(user => user.Username == "admin");
            var person = await db.Users.SingleAsync(user => user.EmployeeId == "1000001");
            var areas = await db.Areas.OrderBy(area => area.Id).Select(area => area.Id).Take(2).ToListAsync();
            var sign = await db.Signs.SingleAsync(entry => entry.AreaId == areas[1] && entry.ServicePointId == null);

            var obsolete = new ScheduleBaseline
            {
                UserId = person.Id, Kind = BaselineKind.Regular, EffectiveFrom = new(2023, 1, 1),
                EffectiveUntil = new(2024, 1, 1), Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            obsolete.Days.Add(new ScheduleBaselineDay { Weekday = DayOfWeek.Monday, IsDayOff = true });
            var boundary = new ScheduleBaseline
            {
                UserId = person.Id, Kind = BaselineKind.Regular, EffectiveFrom = new(2024, 1, 1),
                EffectiveUntil = new(2026, 1, 1), Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            var active = new ScheduleBaseline
            {
                UserId = person.Id, Kind = BaselineKind.Regular, EffectiveFrom = new(2026, 1, 1), Revision = 1,
                CreatedById = admin.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            db.ScheduleBaselines.AddRange(obsolete, boundary, active);

            db.ScheduleExceptions.AddRange(
                new ScheduleException
                {
                    UserId = person.Id, Kind = ScheduleExceptionKind.Area, ShiftDate = new(2024, 1, 10), Shift = Shift.Day,
                    AreaId = areas[0], CreatedById = admin.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                },
                new ScheduleException
                {
                    UserId = person.Id, Kind = ScheduleExceptionKind.Area, ShiftDate = new(2026, 11, 17), Shift = Shift.Day,
                    AreaId = areas[0], CreatedById = admin.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                });

            var transfer = new AreaTransfer
            {
                PersonId = person.Id, ShiftDate = new(2026, 7, 26), Shift = Shift.Day, FromAreaId = areas[0],
                ToAreaId = areas[1], EffectiveAt = new(2026, 7, 26, 3, 0, 0, DateTimeKind.Utc),
                Reason = "ย้ายพื้นที่ทดสอบ", CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            db.AreaTransfers.Add(transfer);
            var receipt = new ScheduleCommandReceipt
            {
                CommandId = Guid.NewGuid(), ActorId = admin.Id, Kind = ScheduleWriteApi.SetWeeklyDaysOff,
                PayloadFingerprint = "synthetic", ExpectedRevisionsJson = "{}", Outcome = ScheduleCommandOutcome.Committed,
                ResultJson = "{}", CommittedAt = now.UtcDateTime.AddYears(-2),
            };
            db.ScheduleCommandReceipts.Add(receipt);
            await db.SaveChangesAsync();

            db.AreaTransferConfirmations.Add(new AreaTransferConfirmation
            {
                TransferId = transfer.Id, AreaId = areas[1], SignId = sign.Id, ConfirmedById = person.Id,
                OccurredAt = now.UtcDateTime.AddDays(-100), Source = AttendanceSource.Scan,
                Latitude = 13.75m, Longitude = 100.5m, AccuracyM = 10, CreatedAt = now.UtcDateTime.AddDays(-100),
            });
            db.ScheduleChanges.Add(new ScheduleChange
            {
                ReceiptId = receipt.Id, EntityType = "WeeklyDayOff", Operation = "update",
                CreatedAt = now.UtcDateTime.AddYears(-2),
            });
            await db.SaveChangesAsync();

            var result = await new RetentionRunner(db, clock, new() { BatchSize = 1 }).RunAsync(default);
            Assert.Equal(1, result.GpsCleared);
            db.ChangeTracker.Clear();

            var baselines = await db.ScheduleBaselines.OrderBy(baseline => baseline.Id).ToListAsync();
            Assert.Equal(new[] { boundary.Id, active.Id }, baselines.Select(baseline => baseline.Id));
            Assert.Empty(await db.ScheduleBaselineDays.ToListAsync());
            Assert.Equal(new DateOnly(2026, 11, 17), Assert.Single(await db.ScheduleExceptions.ToListAsync()).ShiftDate);
            var confirmation = Assert.Single(await db.AreaTransferConfirmations.ToListAsync());
            Assert.Equal((null, null, null), (confirmation.Latitude, confirmation.Longitude, confirmation.AccuracyM));
            Assert.Single(await db.AreaTransfers.ToListAsync());
            Assert.Empty(await db.ScheduleCommandReceipts.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());

            // A second run is a no-op rather than a second deletion.
            Assert.Equal(0, (await new RetentionRunner(db, clock, new()).RunAsync(default)).RecordsDeleted);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
