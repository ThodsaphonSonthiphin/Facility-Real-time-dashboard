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
/// choices distinguish "not chosen" from "chosen nothing", and numbered breaks use their canonical storage values.
/// </summary>
public class SchedulePersistenceTests
{
    [Fact]
    public async Task Schema_stores_numbered_attendance_values()
    {
        using var factory = new JanitorialApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var row = new ShiftAttendance {
                UserId = person.Id, AreaId = person.AreaId!.Value,
                ShiftDate = new DateOnly(2026, 11, 17), Shift = Shift.Day,
                EventType = AttendanceEvent.Break1Out, Source = AttendanceSource.AdminAdd,
                OccurredAt = new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc) };
            db.ShiftAttendances.Add(row);
            await db.SaveChangesAsync();
            var raw = await db.Database.SqlQueryRaw<string>(
                "SELECT event_type AS Value FROM shift_attendances WHERE id = {0}", row.Id).SingleAsync();
            Assert.Equal("BREAK1_OUT", raw);
            db.ChangeTracker.Clear();
            Assert.Equal(AttendanceEvent.Break1Out,
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
    public async Task Inverted_effective_range_is_rejected_by_the_store_and_the_database()
    {
        using var factory = new JanitorialApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == "2000001");
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var building = await db.Buildings.SingleAsync(b => b.Code == "A");
            var store = new ScheduleStore(db);
            var from = new DateOnly(2026, 3, 1);
            var until = new DateOnly(2026, 2, 1);

            // A range that ends before it starts covers no dates, so the only ownership guard must not answer "free".
            var inverted = await Assert.ThrowsAsync<ScheduleConflictException>(
                () => store.EnsureRegularScopeAvailableAsync(building.Id, Shift.Day, from, until));
            Assert.Equal("invalid-range", inverted.Code);
            await Assert.ThrowsAsync<ScheduleConflictException>(
                () => store.RegularScopeOverlapsAsync(building.Id, Shift.Day, from, until));

            // The end date is exclusive, so an empty range is inverted too.
            var empty = await Assert.ThrowsAsync<ScheduleConflictException>(
                () => store.EnsureRegularScopeAvailableAsync(building.Id, Shift.Day, from, from));
            Assert.Equal("invalid-range", empty.Code);

            // The same rule is a CHECK, so a caller that skips the store cannot persist it either.
            db.SupervisorScopePeriods.Add(new SupervisorScopePeriod
            {
                SupervisorId = supervisor.Id,
                BuildingId = building.Id,
                Shift = Shift.Day,
                EffectiveFrom = from,
                EffectiveUntil = until,
                Revision = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            db.CleanerProfilePeriods.Add(new CleanerProfilePeriod
            {
                UserId = cleaner.Id,
                Kind = CleanerKind.Regular,
                Shift = Shift.Day,
                EffectiveFrom = from,
                EffectiveUntil = until,
                Revision = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            db.ScheduleBaselines.Add(new ScheduleBaseline
            {
                UserId = cleaner.Id,
                Kind = BaselineKind.Regular,
                EffectiveFrom = from,
                EffectiveUntil = until,
                Revision = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            Assert.Empty(await db.SupervisorScopePeriods.ToListAsync());
            Assert.Empty(await db.CleanerProfilePeriods.ToListAsync());
            Assert.Empty(await db.ScheduleBaselines.ToListAsync());
        });
    }

    [Theory]
    [InlineData(ScheduleExceptionKind.Area)]
    [InlineData(ScheduleExceptionKind.BreakGroup)]
    public async Task A_slot_holds_one_live_override_of_each_kind(ScheduleExceptionKind kind)
    {
        using var factory = new JanitorialApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "1000002");
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == "2000001");
            var firstArea = await db.Areas.SingleAsync(a => a.Code == "AR01");
            var secondArea = await db.Areas.SingleAsync(a => a.Code == "AR02");
            var shiftDate = new DateOnly(2026, 11, 17);
            var isArea = kind == ScheduleExceptionKind.Area;

            db.ScheduleExceptions.Add(Override(
                cleaner.Id, supervisor.Id, kind, shiftDate, isArea ? firstArea.Id : null, isArea ? null : 1));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(1, (await db.ScheduleExceptions.SingleAsync()).ActiveKey);

            // A second live override would make "the effective Area" ambiguous, so the unique index refuses it.
            db.ScheduleExceptions.Add(Override(
                cleaner.Id, supervisor.Id, kind, shiftDate, isArea ? secondArea.Id : null, isArea ? null : 2));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            Assert.Single(await db.ScheduleExceptions.ToListAsync());

            // Cancelling keeps the history and frees the slot: the computed key is NULL, which a unique index ignores.
            var stored = await db.ScheduleExceptions.SingleAsync();
            stored.IsCancelled = true;
            stored.CancelledAt = DateTime.UtcNow;
            stored.CancelledById = supervisor.Id;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.Null((await db.ScheduleExceptions.SingleAsync()).ActiveKey);

            db.ScheduleExceptions.Add(Override(
                cleaner.Id, supervisor.Id, kind, shiftDate, isArea ? secondArea.Id : null, isArea ? null : 2));
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.ScheduleExceptions.CountAsync());
            Assert.Single(await db.ScheduleExceptions.Where(x => x.ActiveKey != null).ToListAsync());
        });
    }

    [Fact]
    public async Task Overlapping_leave_intervals_stay_storable()
    {
        using var factory = new JanitorialApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "1000003");
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == "2000001");
            var shiftDate = new DateOnly(2026, 11, 17);
            var morning = new DateTime(2026, 11, 17, 1, 0, 0, DateTimeKind.Utc);
            var noon = new DateTime(2026, 11, 17, 5, 0, 0, DateTimeKind.Utc);

            // Two leave spells on one shift are legitimate partial-shift facts, so leave rows carry a NULL active key.
            db.ScheduleExceptions.Add(Leave(cleaner.Id, supervisor.Id, shiftDate, morning, morning.AddHours(1)));
            db.ScheduleExceptions.Add(Leave(cleaner.Id, supervisor.Id, shiftDate, noon, noon.AddHours(1)));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            Assert.Equal(2, await db.ScheduleExceptions.CountAsync());
            Assert.All(await db.ScheduleExceptions.ToListAsync(), row => Assert.Null(row.ActiveKey));
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

    private static ScheduleException Override(
        int userId,
        int createdById,
        ScheduleExceptionKind kind,
        DateOnly shiftDate,
        int? areaId = null,
        int? breakGroup = null) => new()
    {
        UserId = userId,
        Kind = kind,
        ShiftDate = shiftDate,
        Shift = Shift.Day,
        AreaId = areaId,
        BreakGroup = breakGroup,
        Revision = 1,
        CreatedById = createdById,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static ScheduleException Leave(
        int userId, int createdById, DateOnly shiftDate, DateTime startsAt, DateTime endsAt) => new()
    {
        UserId = userId,
        Kind = ScheduleExceptionKind.Leave,
        ShiftDate = shiftDate,
        Shift = Shift.Day,
        StartsAt = startsAt,
        EndsAt = endsAt,
        Revision = 1,
        CreatedById = createdById,
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

    [Fact]
    public async Task Regular_cleaner_profile_period_allows_null_shift_when_unassigned()
    {
        using var factory = new JanitorialApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var building = await db.Buildings.SingleAsync(b => b.Code == "A");
            var cleaner = new User
            {
                Role = UserRole.Cleaner,
                EmployeeId = "E9901",
                DisplayName = "แม่บ้านรอจัดกะ",
                SecretHash = "hash",
                Shift = null,
                AreaId = null,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(cleaner);
            await db.SaveChangesAsync();

            db.CleanerProfilePeriods.Add(new CleanerProfilePeriod
            {
                UserId = cleaner.Id,
                Kind = CleanerKind.Regular,
                HomeBuildingId = building.Id,
                Shift = null,
                EffectiveFrom = new DateOnly(2026, 11, 21),
                Revision = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var stored = await db.CleanerProfilePeriods.SingleAsync(p => p.UserId == cleaner.Id);
            Assert.Null(stored.Shift);
            Assert.Equal(building.Id, stored.HomeBuildingId);
        });
    }

    [Fact]
    public async Task ScheduleBackfill_creates_profiles_and_baselines_idempotently_including_unassigned_shift_building_cleaners()
    {
        using var factory = new JanitorialApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var building = await db.Buildings.SingleAsync(b => b.Code == "A");
            var unassigned = new User
            {
                Role = UserRole.Cleaner,
                EmployeeId = "E9902",
                DisplayName = "แม่บ้านรอจัดกะตึก A",
                SecretHash = "hash",
                BuildingId = building.Id,
                Shift = null,
                AreaId = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(unassigned);
            await db.SaveChangesAsync();

            var historyStart = new DateOnly(2026, 11, 17);
            await ScheduleBackfill.EnsureAsync(db, historyStart, CancellationToken.None);
            db.ChangeTracker.Clear();

            // Seeded cleaner 1000001 has Area AR01 and Shift Day -> gets both profile and 7-day baseline
            var c1 = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var c1Profile = await db.CleanerProfilePeriods.SingleAsync(p => p.UserId == c1.Id);
            Assert.Equal((CleanerKind.Regular, (int?)building.Id, (Shift?)Shift.Day, historyStart),
                (c1Profile.Kind, c1Profile.HomeBuildingId, c1Profile.Shift, c1Profile.EffectiveFrom));
            var c1Baseline = await db.ScheduleBaselines.Include(b => b.Days).SingleAsync(b => b.UserId == c1.Id);
            Assert.Equal(7, c1Baseline.Days.Count);
            Assert.All(c1Baseline.Days, d => Assert.Equal(c1.AreaId, d.AreaId));

            // Unassigned shift/area cleaner with BuildingId -> gets profile with null Shift, and no baseline
            var uProfile = await db.CleanerProfilePeriods.SingleAsync(p => p.UserId == unassigned.Id);
            Assert.Equal((CleanerKind.Regular, (int?)building.Id, (Shift?)null, historyStart),
                (uProfile.Kind, uProfile.HomeBuildingId, uProfile.Shift, uProfile.EffectiveFrom));
            Assert.Empty(await db.ScheduleBaselines.Where(b => b.UserId == unassigned.Id).ToListAsync());

            // Idempotent second run creates no duplicates
            await ScheduleBackfill.EnsureAsync(db, historyStart, CancellationToken.None);
            Assert.Equal(1, await db.CleanerProfilePeriods.CountAsync(p => p.UserId == unassigned.Id));
            Assert.Equal(1, await db.ScheduleBaselines.CountAsync(b => b.UserId == c1.Id));
        });
    }

    [Fact]
    public async Task ScheduleSync_splits_current_profile_without_mutating_future_period()
    {
        using var factory = new JanitorialApiFactory();
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var buildingA = await db.Buildings.SingleAsync(b => b.Code == "A");
            var past = new DateOnly(2026, 10, 1);
            var today = new DateOnly(2026, 11, 17);
            var future = new DateOnly(2026, 12, 1);
            var now = DateTime.UtcNow;

            db.CleanerProfilePeriods.AddRange(
                new CleanerProfilePeriod
                {
                    UserId = cleaner.Id,
                    Kind = CleanerKind.Regular,
                    HomeBuildingId = buildingA.Id,
                    Shift = Shift.Day,
                    EffectiveFrom = past,
                    EffectiveUntil = future,
                    Revision = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new CleanerProfilePeriod
                {
                    UserId = cleaner.Id,
                    Kind = CleanerKind.Relief,
                    HomeBuildingId = buildingA.Id,
                    Shift = Shift.Day,
                    EffectiveFrom = future,
                    EffectiveUntil = null,
                    Revision = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            await db.SaveChangesAsync();

            var store = new ScheduleStore(db);
            await using var tx = await db.Database.BeginTransactionAsync();
            await ScheduleSync.SyncCleanerAsync(
                db, store, cleaner, buildingB, CleanerKind.Regular, actorId: null, today, now);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            db.ChangeTracker.Clear();

            var periods = await db.CleanerProfilePeriods
                .Where(p => p.UserId == cleaner.Id)
                .OrderBy(p => p.EffectiveFrom)
                .ToListAsync();
            Assert.Equal(3, periods.Count);
            Assert.Equal((past, (DateOnly?)today, buildingA.Id, CleanerKind.Regular),
                (periods[0].EffectiveFrom, periods[0].EffectiveUntil, periods[0].HomeBuildingId, periods[0].Kind));
            Assert.Equal((today, (DateOnly?)future, buildingB, CleanerKind.Regular),
                (periods[1].EffectiveFrom, periods[1].EffectiveUntil, periods[1].HomeBuildingId, periods[1].Kind));
            Assert.Equal((future, (DateOnly?)null, buildingA.Id, CleanerKind.Relief, 1L),
                (periods[2].EffectiveFrom, periods[2].EffectiveUntil, periods[2].HomeBuildingId, periods[2].Kind, periods[2].Revision));
        });
    }
}
