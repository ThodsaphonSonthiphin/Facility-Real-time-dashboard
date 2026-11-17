using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// ADRs facility-0215, 0221, 0222: scheduling facts are stored as dated records with explicit provenance, weekly
/// choices distinguish "not chosen" from "chosen nothing", and the legacy attendance strings stay readable.
/// </summary>
public class SchedulePersistenceTests
{
    [Fact]
    public async Task Schema_keeps_legacy_attendance_values_readable()
    {
        using var factory = new JanitorialApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var row = new ShiftAttendance {
                UserId = person.Id, AreaId = person.AreaId!.Value,
                ShiftDate = new DateOnly(2026, 11, 17), Shift = Shift.Day,
                EventType = AttendanceEvent.BreakOut, Source = AttendanceSource.AdminAdd,
                OccurredAt = new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc) };
            db.ShiftAttendances.Add(row);
            await db.SaveChangesAsync();
            var raw = await db.Database.SqlQueryRaw<string>(
                "SELECT event_type AS Value FROM shift_attendances WHERE id = {0}", row.Id).SingleAsync();
            Assert.Equal("BREAK_OUT", raw);
            db.ChangeTracker.Clear();
            Assert.Equal(AttendanceEvent.BreakOut,
                (await db.ShiftAttendances.SingleAsync(x => x.Id == row.Id)).EventType);
        });
    }

    [Fact]
    public async Task Weekly_day_off_distinguishes_unset_from_a_confirmed_empty_choice()
    {
        using var factory = new JanitorialApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var week = new DateOnly(2026, 11, 16);
            var monday = week;
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            var unset = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var confirmedEmpty = await db.Users.SingleAsync(u => u.EmployeeId == "1000002");
            var selectedMonday = await db.Users.SingleAsync(u => u.EmployeeId == "1000003");

            db.WeeklyDayOffs.Add(Header(confirmedEmpty.Id, week, admin.Id, choices: []));
            db.WeeklyDayOffs.Add(Header(selectedMonday.Id, week, admin.Id, choices: [monday]));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // No row at all means the person was never asked: it is not the same as a saved empty week.
            Assert.Null(await db.WeeklyDayOffs.SingleOrDefaultAsync(x => x.UserId == unset.Id && x.WeekStart == week));

            var empty = await db.WeeklyDayOffs.Include(x => x.Choices)
                .SingleAsync(x => x.UserId == confirmedEmpty.Id && x.WeekStart == week);
            Assert.Empty(empty.Choices);

            var chosen = await db.WeeklyDayOffs.Include(x => x.Choices)
                .SingleAsync(x => x.UserId == selectedMonday.Id && x.WeekStart == week);
            Assert.Equal(monday, Assert.Single(chosen.Choices).OffDate);
        });
    }

    [Fact]
    public async Task Supervisor_can_hold_two_buildings_in_one_shift()
    {
        using var factory = new JanitorialApiFactory();
        var secondBuildingId = await TestData.AddBuildingAsync(factory, "B");

        await factory.WithDbAsync(async db =>
        {
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == "2000001");
            var buildingA = await db.Buildings.SingleAsync(b => b.Code == "A");
            db.SupervisorScopePeriods.AddRange(
                Scope(supervisor.Id, buildingA.Id),
                Scope(supervisor.Id, secondBuildingId));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var buildingIds = await db.SupervisorScopePeriods
                .Where(x => x.SupervisorId == supervisor.Id)
                .Select(x => x.BuildingId)
                .OrderBy(x => x)
                .ToListAsync();
            Assert.Equal(new[] { buildingA.Id, secondBuildingId }.OrderBy(x => x), buildingIds);
        });
    }

    [Fact]
    public async Task Store_rejects_overlapping_regular_building_ownership()
    {
        using var factory = new JanitorialApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == "2000001");
            var building = await db.Buildings.SingleAsync(b => b.Code == "A");
            db.SupervisorScopePeriods.Add(new SupervisorScopePeriod
            {
                SupervisorId = supervisor.Id,
                BuildingId = building.Id,
                Shift = Shift.Day,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                EffectiveUntil = new DateOnly(2026, 2, 1),
                Revision = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            var store = new ScheduleStore(db);
            Assert.True(await store.RegularScopeOverlapsAsync(
                building.Id, Shift.Day, new DateOnly(2026, 1, 15), new DateOnly(2026, 3, 1)));
            await Assert.ThrowsAsync<ScheduleConflictException>(() => store.EnsureRegularScopeAvailableAsync(
                building.Id, Shift.Day, new DateOnly(2026, 1, 15), new DateOnly(2026, 3, 1)));

            // End-exclusive intervals: the next period may start on the previous period's end date.
            Assert.False(await store.RegularScopeOverlapsAsync(
                building.Id, Shift.Day, new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)));
        });
    }

    [Fact]
    public async Task Transfer_confirmation_is_unique_per_instruction()
    {
        using var factory = new JanitorialApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == "2000001");
            var from = await db.Areas.SingleAsync(a => a.Code == "AR01");
            var to = await db.Areas.SingleAsync(a => a.Code == "AR02");
            var sign = await db.Signs.SingleAsync(s => s.QrToken == "token-checkin-ar02");

            var transfer = Transfer(cleaner.Id, supervisor.Id, from.Id, to.Id);
            db.AreaTransfers.Add(transfer);
            await db.SaveChangesAsync();

            db.AreaTransferConfirmations.Add(Confirmation(transfer.Id, cleaner.Id, to.Id, sign.Id));
            await db.SaveChangesAsync();

            db.AreaTransferConfirmations.Add(Confirmation(transfer.Id, cleaner.Id, to.Id, sign.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Command_identity_applies_once()
    {
        using var factory = new JanitorialApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            var commandId = Guid.NewGuid();
            db.ScheduleCommandReceipts.Add(Receipt(commandId, admin.Id, "set-weekly-days-off"));
            await db.SaveChangesAsync();

            db.ScheduleCommandReceipts.Add(Receipt(commandId, admin.Id, "record-leave"));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Baseline_keeps_one_area_cell_per_weekday()
    {
        using var factory = new JanitorialApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var area = await db.Areas.SingleAsync(a => a.Code == "AR01");
            var baseline = new ScheduleBaseline
            {
                UserId = cleaner.Id,
                Kind = BaselineKind.Regular,
                EffectiveFrom = new DateOnly(2026, 11, 17),
                Revision = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            foreach (var weekday in Enum.GetValues<DayOfWeek>())
            {
                baseline.Days.Add(new ScheduleBaselineDay
                {
                    Weekday = weekday,
                    AreaId = area.Id,
                    IsDayOff = false,
                });
            }

            db.ScheduleBaselines.Add(baseline);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var stored = await db.ScheduleBaselines.Include(x => x.Days)
                .SingleAsync(x => x.UserId == cleaner.Id);
            Assert.Equal(7, stored.Days.Count);
            Assert.Equal(Enum.GetValues<DayOfWeek>().OrderBy(x => x), stored.Days.Select(x => x.Weekday).OrderBy(x => x));
            Assert.All(stored.Days, day =>
            {
                Assert.Equal(area.Id, day.AreaId);
                Assert.Null(day.BreakGroup);
                Assert.False(day.IsDayOff);
            });
        });
    }

    [Fact]
    public async Task Backfill_records_only_known_regular_facts_and_is_idempotent()
    {
        using var factory = new JanitorialApiFactory();
        var historyStartsAt = new DateOnly(2026, 11, 17);

        await factory.WithDbAsync(async db =>
        {
            await ScheduleBackfill.EnsureAsync(db, historyStartsAt, CancellationToken.None);
            await ScheduleBackfill.EnsureAsync(db, historyStartsAt, CancellationToken.None);

            var profiles = await db.CleanerProfilePeriods.ToListAsync();
            Assert.Equal(3, profiles.Count);
            Assert.All(profiles, profile =>
            {
                Assert.Equal(CleanerKind.Regular, profile.Kind);
                Assert.Equal(historyStartsAt, profile.EffectiveFrom);
                Assert.Null(profile.EffectiveUntil);
                Assert.NotNull(profile.HomeBuildingId);
            });

            var baselines = await db.ScheduleBaselines.Include(x => x.Days).ToListAsync();
            Assert.Equal(3, baselines.Count);
            Assert.All(baselines, baseline =>
            {
                Assert.Equal(BaselineKind.Regular, baseline.Kind);
                Assert.Equal(historyStartsAt, baseline.EffectiveFrom);
                Assert.Equal(7, baseline.Days.Count);
                Assert.All(baseline.Days, day =>
                {
                    Assert.NotNull(day.AreaId);
                    Assert.Null(day.BreakGroup);
                    Assert.False(day.IsDayOff);
                });
            });

            var scopes = await db.SupervisorScopePeriods.ToListAsync();
            Assert.Single(scopes);
            Assert.Equal(historyStartsAt, scopes[0].EffectiveFrom);
            Assert.Null(scopes[0].EffectiveUntil);

            // No past day-off choice, relief identity or break group is invented by the compatibility backfill.
            Assert.Empty(await db.WeeklyDayOffs.ToListAsync());
            Assert.Empty(await db.ReliefAssignments.ToListAsync());
        });
    }

    private static WeeklyDayOff Header(int userId, DateOnly weekStart, int createdById, IReadOnlyCollection<DateOnly> choices)
    {
        var header = new WeeklyDayOff
        {
            UserId = userId,
            WeekStart = weekStart,
            CreatedById = createdById,
            Revision = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        foreach (var date in choices)
        {
            header.Choices.Add(new WeeklyDayOffChoice { OffDate = date });
        }

        return header;
    }

    private static SupervisorScopePeriod Scope(int supervisorId, int buildingId) => new()
    {
        SupervisorId = supervisorId,
        BuildingId = buildingId,
        Shift = Shift.Day,
        EffectiveFrom = new DateOnly(2026, 11, 17),
        Revision = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static AreaTransfer Transfer(int personId, int supervisorId, int fromAreaId, int toAreaId) => new()
    {
        PersonId = personId,
        ShiftDate = new DateOnly(2026, 11, 17),
        Shift = Shift.Day,
        FromAreaId = fromAreaId,
        ToAreaId = toAreaId,
        EffectiveAt = new DateTime(2026, 11, 17, 6, 0, 0, DateTimeKind.Utc),
        Reason = "งานเร่งด่วน",
        Revision = 1,
        CreatedById = supervisorId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static AreaTransferConfirmation Confirmation(int transferId, int personId, int areaId, int signId) => new()
    {
        TransferId = transferId,
        AreaId = areaId,
        SignId = signId,
        ConfirmedById = personId,
        OccurredAt = new DateTime(2026, 11, 17, 6, 5, 0, DateTimeKind.Utc),
        Source = AttendanceSource.Scan,
        Latitude = 13.756300m,
        Longitude = 100.501800m,
        AccuracyM = 10,
        DistanceM = 2,
        WithinRadius = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static ScheduleCommandReceipt Receipt(Guid commandId, int actorId, string kind) => new()
    {
        CommandId = commandId,
        ActorId = actorId,
        Kind = kind,
        PayloadFingerprint = new string('A', 64),
        ExpectedRevisionsJson = "{}",
        Outcome = "committed",
        ResultJson = "{}",
        CommittedAt = DateTime.UtcNow,
    };
}
