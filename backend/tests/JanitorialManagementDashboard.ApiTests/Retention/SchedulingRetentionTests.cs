using System.Globalization;
using System.Text.Json;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.ApiTests.Scheduling;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Retention;

/// <summary>
/// Spec §12 and A27: the new scheduling detail joins the existing retention stream. The configured detail window removes
/// obsolete plan history, an arrival's GPS is cleared on the shorter GPS policy while its actual fact stays, and a purged
/// command receipt can never become a second application.
/// </summary>
public class SchedulingRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

    private static Task<RetentionResult> RunAsync(
        JanitorialApiFactory factory, AppDbContext db, RetentionSettings? settings = null) =>
        new RetentionRunner(db, factory.Clock, settings ?? new()).RunAsync(default);

    [Fact]
    public async Task Obsolete_baseline_versions_are_pruned_while_the_boundary_and_active_versions_survive()
    {
        using var factory = new JanitorialApiFactory();
        factory.Clock.SetUtcNow(Now);
        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(user => user.Username == "admin");
            var person = await db.Users.SingleAsync(user => user.EmployeeId == "1000001");
            var other = await db.Users.SingleAsync(user => user.EmployeeId == "1000002");

            var old = new ScheduleBaseline
            {
                UserId = person.Id, Kind = BaselineKind.Regular, EffectiveFrom = new(2023, 1, 1),
                EffectiveUntil = new(2024, 1, 1), Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            old.Days.Add(new ScheduleBaselineDay { Weekday = DayOfWeek.Monday, IsDayOff = true });
            var boundary = new ScheduleBaseline
            {
                UserId = person.Id, Kind = BaselineKind.Regular, EffectiveFrom = new(2024, 1, 1),
                EffectiveUntil = new(2026, 1, 1), Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            var active = new ScheduleBaseline
            {
                UserId = person.Id, Kind = BaselineKind.Regular, EffectiveFrom = new(2026, 1, 1),
                Revision = 1, CreatedById = admin.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            // A person whose only version ended long ago keeps it as the latest predecessor of the retained window.
            var predecessor = new ScheduleBaseline
            {
                UserId = other.Id, Kind = BaselineKind.Regular, EffectiveFrom = new(2023, 1, 1),
                EffectiveUntil = new(2024, 1, 1), Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            db.ScheduleBaselines.AddRange(old, boundary, active, predecessor);
            await db.SaveChangesAsync();

            await RunAsync(factory, db);
            db.ChangeTracker.Clear();

            var remaining = await db.ScheduleBaselines.ToListAsync();
            Assert.Equal(
                new[] { boundary.Id, predecessor.Id, active.Id }.OrderBy(id => id),
                remaining.Select(baseline => baseline.Id).OrderBy(id => id));
            Assert.Empty(await db.ScheduleBaselineDays.ToListAsync());
        });
    }

    [Fact]
    public async Task A_pruned_transfer_location_keeps_the_arrival_fact_and_its_destination()
    {
        using var factory = new JanitorialApiFactory();
        factory.Clock.SetUtcNow(Now);
        await factory.WithDbAsync(async db =>
        {
            var (person, admin, fromArea, toArea, sign) = await TransferSetupAsync(db);
            var transfer = new AreaTransfer
            {
                PersonId = person, ShiftDate = new(2026, 7, 26), Shift = Shift.Day,
                FromAreaId = fromArea, ToAreaId = toArea, EffectiveAt = new(2026, 7, 26, 3, 0, 0, DateTimeKind.Utc),
                Reason = "ย้ายพื้นที่ทดสอบ", CreatedById = admin,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            db.AreaTransfers.Add(transfer);
            await db.SaveChangesAsync();
            db.AreaTransferConfirmations.Add(new AreaTransferConfirmation
            {
                TransferId = transfer.Id, AreaId = toArea, SignId = sign, ConfirmedById = person,
                OccurredAt = Now.UtcDateTime.AddDays(-100), Source = AttendanceSource.Scan,
                Latitude = 13.75m, Longitude = 100.5m, AccuracyM = 10, DistanceM = 4, WithinRadius = true,
                CreatedAt = Now.UtcDateTime.AddDays(-100),
            });
            await db.SaveChangesAsync();

            var result = await RunAsync(factory, db);
            Assert.Equal(1, result.GpsCleared);
            db.ChangeTracker.Clear();

            var confirmation = Assert.Single(await db.AreaTransferConfirmations.ToListAsync());
            Assert.Equal((null, null, null), (confirmation.Latitude, confirmation.Longitude, confirmation.AccuracyM));
            Assert.Equal((toArea, AttendanceSource.Scan), (confirmation.AreaId, confirmation.Source));
            Assert.Equal(Now.UtcDateTime.AddDays(-100), confirmation.OccurredAt);
            Assert.Single(await db.AreaTransfers.ToListAsync());
        });
    }

    [Fact]
    public async Task Detail_older_than_the_window_is_removed_while_current_configuration_and_audit_remain()
    {
        using var factory = new JanitorialApiFactory();
        factory.Clock.SetUtcNow(Now);
        await factory.WithDbAsync(async db =>
        {
            var (person, admin, fromArea, toArea, sign) = await TransferSetupAsync(db);
            var oldDate = new DateOnly(2024, 1, 10);
            var currentDate = new DateOnly(2026, 11, 17);

            db.ScheduleExceptions.AddRange(
                DatedArea(person, admin, fromArea, oldDate), DatedArea(person, admin, fromArea, currentDate));
            db.ReliefAssignments.AddRange(
                Relief(person, admin, fromArea, oldDate), Relief(person, admin, fromArea, currentDate));
            db.WeeklyDayOffs.AddRange(
                Week(person, admin, new DateOnly(2024, 1, 8)), Week(person, admin, new DateOnly(2026, 11, 16)));
            db.AreaTransfers.AddRange(
                Transfer(person, admin, fromArea, toArea, oldDate), Transfer(person, admin, fromArea, toArea, currentDate));
            await db.SaveChangesAsync();

            foreach (var instruction in await db.AreaTransfers.ToListAsync())
            {
                db.AreaTransferConfirmations.Add(new AreaTransferConfirmation
                {
                    TransferId = instruction.Id, AreaId = toArea, SignId = sign, ConfirmedById = person,
                    OccurredAt = instruction.EffectiveAt, Source = AttendanceSource.Scan,
                    CreatedAt = instruction.EffectiveAt,
                });
            }

            var oldReceipt = Receipt(admin, Now.UtcDateTime.AddYears(-2));
            var currentReceipt = Receipt(admin, Now.UtcDateTime.AddDays(-1));
            db.ScheduleCommandReceipts.AddRange(oldReceipt, currentReceipt);
            await db.SaveChangesAsync();
            db.ScheduleChanges.AddRange(
                Change(oldReceipt.Id, Now.UtcDateTime.AddYears(-2)), Change(currentReceipt.Id, Now.UtcDateTime.AddDays(-1)));
            await db.SaveChangesAsync();

            await RunAsync(factory, db);
            db.ChangeTracker.Clear();

            Assert.Equal(currentDate, Assert.Single(await db.ScheduleExceptions.ToListAsync()).ShiftDate);
            Assert.Equal(currentDate, Assert.Single(await db.ReliefAssignments.ToListAsync()).ShiftDate);
            Assert.Equal(new DateOnly(2026, 11, 16), Assert.Single(await db.WeeklyDayOffs.ToListAsync()).WeekStart);
            Assert.Single(await db.WeeklyDayOffChoices.ToListAsync());
            var transfer = Assert.Single(await db.AreaTransfers.ToListAsync());
            Assert.Equal(currentDate, transfer.ShiftDate);
            Assert.Single(await db.AreaTransferConfirmations.ToListAsync());
            Assert.Equal(currentReceipt.CommandId, Assert.Single(await db.ScheduleCommandReceipts.ToListAsync()).CommandId);
            Assert.Single(await db.ScheduleChanges.ToListAsync());
        });
    }

    [Fact]
    public async Task A_purged_receipt_cannot_become_a_second_application_of_an_expired_identity()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        ScheduleWriteApi.Today(factory);
        var adminId = 0;
        int personId = 0;
        var issued = default(IssuedCommandToken);
        var envelope = default(ScheduleCommandEnvelope);
        await factory.WithDbAsync(async db =>
        {
            adminId = (await db.Users.SingleAsync(user => user.Username == "admin")).Id;
            personId = (await db.Users.SingleAsync(user => user.EmployeeId == "1000001")).Id;
            issued = new ScheduleCommandTokenService(ScheduleWriteApi.TestKey).Issue(adminId, factory.Clock.GetUtcNow().AddYears(-2));
            envelope = new ScheduleCommandEnvelope(
                issued.Value, issued.Id, new Dictionary<string, string>(), ScheduleWriteApi.SetWeeklyDaysOff,
                JsonSerializer.SerializeToElement(new
                {
                    weekStart = "2026-11-16",
                    rows = new[] { new { userId = personId, offDates = new[] { "2026-11-18" } } },
                }));
            var stored = new CommandResult(issued.Id, ScheduleCommandOutcome.Committed, new Dictionary<string, string>(), []);
            db.ScheduleCommandReceipts.Add(new ScheduleCommandReceipt
            {
                CommandId = issued.Id, ActorId = adminId, Kind = ScheduleWriteApi.SetWeeklyDaysOff,
                PayloadFingerprint = ScheduleCommandService.Fingerprint(envelope), ExpectedRevisionsJson = "{}",
                Outcome = ScheduleCommandOutcome.Committed, ResultJson = JsonSerializer.Serialize(stored),
                CommittedAt = factory.Clock.GetUtcNow().UtcDateTime.AddYears(-2),
            });
            await db.SaveChangesAsync();
        });

        // While the receipt is retained, the old identity still answers from it even though its start window has passed —
        // and writes nothing a second time.
        Assert.Equal(ScheduleCommandOutcome.Committed, (await ExecuteAsync(factory, adminId, envelope!)).Outcome);
        await factory.WithDbAsync(async db => Assert.Empty(await db.WeeklyDayOffs.ToListAsync()));

        await factory.WithDbAsync(async db =>
        {
            await RunAsync(factory, db);
            Assert.Empty(await db.ScheduleCommandReceipts.ToListAsync());
        });

        // With the receipt purged the identity is expired, so the same command is refused and nothing is applied.
        Assert.Equal(ScheduleCommandOutcome.Expired, (await ExecuteAsync(factory, adminId, envelope!)).Outcome);
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());
            Assert.Empty(await db.ScheduleChanges.ToListAsync());
        });
    }

    private static async Task<CommandResult> ExecuteAsync(
        JanitorialApiFactory factory, int actorId, ScheduleCommandEnvelope envelope)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ScheduleCommandService>()
            .ExecuteAsync(actorId, envelope, CancellationToken.None);
    }

    private static async Task<(int Person, int Admin, int FromArea, int ToArea, int Sign)> TransferSetupAsync(AppDbContext db)
    {
        var person = (await db.Users.SingleAsync(user => user.EmployeeId == "1000001")).Id;
        var admin = (await db.Users.SingleAsync(user => user.Username == "admin")).Id;
        var areas = await db.Areas.OrderBy(area => area.Id).Select(area => area.Id).Take(2).ToListAsync();
        var sign = await db.Signs.SingleAsync(entry => entry.AreaId == areas[1] && entry.ServicePointId == null);
        return (person, admin, areas[0], areas[1], sign.Id);
    }

    private static ScheduleException DatedArea(int person, int admin, int area, DateOnly date) => new()
    {
        UserId = person, Kind = ScheduleExceptionKind.Area, ShiftDate = date, Shift = Shift.Day,
        AreaId = area, CreatedById = admin, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private static ReliefAssignment Relief(int person, int admin, int area, DateOnly date) => new()
    {
        UserId = person, ShiftDate = date, Shift = Shift.Day, AreaId = area, SourceBuildingId = 1,
        AgreementReported = false, CreatedById = admin, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private static WeeklyDayOff Week(int person, int admin, DateOnly weekStart)
    {
        var week = new WeeklyDayOff
        {
            UserId = person, WeekStart = weekStart, Revision = 1, CreatedById = admin,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        week.Choices.Add(new WeeklyDayOffChoice { OffDate = weekStart.AddDays(6) });
        return week;
    }

    private static AreaTransfer Transfer(int person, int admin, int fromArea, int toArea, DateOnly date) => new()
    {
        PersonId = person, ShiftDate = date, Shift = Shift.Day, FromAreaId = fromArea, ToAreaId = toArea,
        EffectiveAt = date.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc), Reason = "ย้ายพื้นที่ทดสอบ",
        CreatedById = admin, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private static ScheduleCommandReceipt Receipt(int admin, DateTime committedAt) => new()
    {
        CommandId = Guid.NewGuid(), ActorId = admin, Kind = ScheduleWriteApi.SetWeeklyDaysOff,
        PayloadFingerprint = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), ExpectedRevisionsJson = "{}",
        Outcome = ScheduleCommandOutcome.Committed, ResultJson = "{}", CommittedAt = committedAt,
    };

    private static ScheduleChange Change(int receiptId, DateTime createdAt) => new()
    {
        ReceiptId = receiptId, EntityType = "WeeklyDayOff", Operation = "update", CreatedAt = createdAt,
    };
}
