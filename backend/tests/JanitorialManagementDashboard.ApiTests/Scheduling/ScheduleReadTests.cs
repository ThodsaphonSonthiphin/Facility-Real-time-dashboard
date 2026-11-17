using System.Data.Common;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §3, §10 and §11 (acceptance A01, A04, A27): the week read is scoped by the actor, is built from the authorised
/// Users roster rather than from the presence of scheduling rows, says "unavailable" instead of zero before history
/// coverage, and reads every input of the week in one consistent snapshot.
/// </summary>
public class ScheduleReadTests
{
    private static readonly DateOnly Week = new(2026, 11, 16);
    private static readonly DateOnly NextWeek = new(2026, 11, 23);
    private static readonly DateOnly PreviousWeek = new(2026, 11, 9);
    private static readonly DateOnly HistoryStarts = new(2026, 11, 17);

    [Fact]
    public async Task A_cleaner_reads_only_their_own_row()
    {
        using var factory = new JanitorialApiFactory();
        var (_, auth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000001", "0810000001");
        var other = await AdminSetupApi.UserIdAsync(factory, "1000003");

        var projection = await ReadAsync(factory, auth.User.Id, Week);

        Assert.Equal(auth.User.Id, Assert.Single(projection.Rows).UserId);
        Assert.DoesNotContain(projection.Rows, row => row.UserId == other);
        Assert.Equal(ScheduleRules.WeekStart(Week), projection.WeekStart);
        Assert.Equal(factory.Clock.GetUtcNow(), projection.ReadAt);
    }

    [Fact]
    public async Task A_cleaner_forging_another_building_or_shift_reads_nothing()
    {
        // A01: a query parameter narrows; it never widens a Cleaner past their own row.
        using var factory = new JanitorialApiFactory();
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var (_, auth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000001", "0810000001");

        Assert.Empty((await ReadAsync(factory, auth.User.Id, Week, buildingId: buildingB)).Rows);
        Assert.Empty((await ReadAsync(factory, auth.User.Id, Week, shift: Shift.Night)).Rows);
        Assert.Single((await ReadAsync(factory, auth.User.Id, Week, shift: Shift.Day)).Rows);
    }

    [Fact]
    public async Task A_supervisor_reads_the_buildings_the_dated_scope_gives_them()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddAreaAsync(factory, "AR03", "B");
        await TestData.AddCleanerAsync(factory, "1000009", Shift.Day, areaCode: "AR03");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var supervisor = await AdminSetupApi.UserIdAsync(factory, "2000001");
        var dayA = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var dayA2 = await AdminSetupApi.UserIdAsync(factory, "1000003");
        var nightA = await AdminSetupApi.UserIdAsync(factory, "1000002");
        var dayB = await AdminSetupApi.UserIdAsync(factory, "1000009");

        var projection = await ReadAsync(factory, supervisor, Week);

        Assert.Equal(new[] { dayA, dayA2 }.OrderBy(x => x), projection.Rows.Select(r => r.UserId).OrderBy(x => x));
        Assert.DoesNotContain(projection.Rows, row => row.UserId == nightA);
        Assert.DoesNotContain(projection.Rows, row => row.UserId == dayB);
    }

    [Fact]
    public async Task A_supervisor_forging_a_building_or_shift_reads_nothing()
    {
        using var factory = new JanitorialApiFactory();
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var supervisor = await AdminSetupApi.UserIdAsync(factory, "2000001");

        // The forged building and the other shift are both narrower than the assigned scope, never a wider read.
        Assert.Empty((await ReadAsync(factory, supervisor, Week, buildingId: buildingB)).Rows);
        Assert.Empty((await ReadAsync(factory, supervisor, Week, shift: Shift.Night)).Rows);
        Assert.Equal(2, (await ReadAsync(factory, supervisor, Week, shift: Shift.Day)).Rows.Count);
    }

    [Fact]
    public async Task A_supervisor_without_any_dated_row_reads_the_projection_building()
    {
        // Spec §14: before the dated rows exist the compatibility projection is the only membership, never retroactively.
        using var factory = new JanitorialApiFactory();
        var supervisor = await AdminSetupApi.UserIdAsync(factory, "2000001");
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.FromHours(7)));

        var thisWeek = await ReadAsync(factory, supervisor, Week);
        var beforeToday = await ReadAsync(factory, supervisor, PreviousWeek);

        Assert.Contains(thisWeek.Rows, row => row.UserId == person);
        Assert.All(thisWeek.Rows, row => Assert.Null(row.Days[0].Area.Value)); // no history coverage yet
        Assert.Empty(beforeToday.Rows);                                        // and never backwards
    }

    [Fact]
    public async Task An_admin_reads_every_row()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddAreaAsync(factory, "AR03", "B");
        await TestData.AddCleanerAsync(factory, "1000009", Shift.Day, areaCode: "AR03");
        var (_, auth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var dayB = await AdminSetupApi.UserIdAsync(factory, "1000009");

        var all = await ReadAsync(factory, auth.User.Id, Week);
        var buildingAOnly = await ReadAsync(factory, auth.User.Id, Week, buildingId: buildingA);

        Assert.Equal(4, all.Rows.Count);
        Assert.Equal(3, buildingAOnly.Rows.Count);
        Assert.DoesNotContain(buildingAOnly.Rows, row => row.UserId == dayB);
    }

    [Fact]
    public async Task A_person_with_no_schedule_appears_with_unavailable_days()
    {
        // The roster is the authorised Users membership, so an empty schedule is visible as unavailable, never as a zero.
        using var factory = new JanitorialApiFactory();
        var (_, auth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var person = await AdminSetupApi.UserIdAsync(factory, "1000002");

        var row = Assert.Single((await ReadAsync(factory, auth.User.Id, Week)).Rows, r => r.UserId == person);

        Assert.Equal(7, row.Days.Count);
        Assert.All(row.Days, day =>
        {
            Assert.Equal("unavailable", day.Availability);
            Assert.Null(day.Area.Value);
            Assert.Equal(ScheduleResolver.SourceUnavailable, day.Area.SourceKind);
        });
    }

    [Fact]
    public async Task A_date_before_the_history_coverage_is_unavailable_and_not_the_current_Area()
    {
        // A27: before the covered dates the week says unavailable; it is not Area 0 and not the compatibility AreaId.
        using var factory = new JanitorialApiFactory();
        var compatibilityArea = await AdminSetupApi.AreaIdAsync(factory, "AR01"); // User.AreaId of 1000001
        var baselineArea = await AdminSetupApi.AreaIdAsync(factory, "AR02");      // what the stored baseline says
        await AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, HistoryStarts, buildingCode: "A");
        await AddBaselineAsync(factory, "1000001", BaselineKind.Regular, HistoryStarts, baselineArea);
        var (_, auth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");

        var covered = (await ReadAsync(factory, auth.User.Id, Week))
            .Rows.Single(r => r.UserId == person);
        var earlier = (await ReadAsync(factory, auth.User.Id, PreviousWeek));

        // Monday 16 Nov is before the baseline starts; Tuesday onwards is the stored Area, not User.AreaId.
        Assert.Equal("unavailable", covered.Days[0].Availability);
        Assert.Null(covered.Days[0].Area.Value);
        Assert.Equal(baselineArea, covered.Days[1].Area.Value);
        Assert.Equal(ScheduleResolver.SourceBaseline, covered.Days[1].Area.SourceKind);
        Assert.All(covered.Days.Skip(1), day => Assert.NotEqual(compatibilityArea, day.Area.Value));

        var before = earlier.Rows.Single(r => r.UserId == person);
        Assert.Equal("unavailable", earlier.Availability);
        Assert.All(before.Days, day =>
        {
            Assert.Equal("unavailable", day.Availability);
            Assert.Null(day.Area.Value);
            Assert.NotEqual(0, day.Area.Value);
            Assert.NotEqual(compatibilityArea, day.Area.Value);
        });
    }

    [Fact]
    public async Task A_dated_Area_override_wins_for_its_own_slot_in_the_projection()
    {
        using var factory = new JanitorialApiFactory();
        var compatibilityArea = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var baselineArea = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var overriddenArea = await TestData.AddAreaAsync(factory, "AR03");
        await AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, Week, buildingCode: "A");
        await AddBaselineAsync(factory, "1000001", BaselineKind.Regular, Week, baselineArea);
        await AddAreaExceptionAsync(factory, "1000001", Week.AddDays(2), Shift.Day, overriddenArea);
        await AddAreaExceptionAsync(factory, "1000001", Week.AddDays(3), Shift.Night, overriddenArea);
        var (_, auth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");

        var row = (await ReadAsync(factory, auth.User.Id, Week)).Rows.Single(r => r.UserId == person);

        Assert.Equal(overriddenArea, row.Days[2].Area.Value);
        Assert.Equal(ScheduleResolver.SourceDated, row.Days[2].Area.SourceKind);
        Assert.Equal(baselineArea, row.Days[3].Area.Value); // a Night override is another slot, not this day's shift
        Assert.Equal(compatibilityArea, await AdminSetupApi.AreaIdAsync(factory, "AR01"));
    }

    [Fact]
    public async Task A_day_off_choice_is_isolated_to_its_own_week()
    {
        // A04: a new week's day off differs from the previous week, and an unsaved week is unset rather than guessed.
        using var factory = new JanitorialApiFactory();
        var area = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        await AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, PreviousWeek, buildingCode: "A");
        await AddBaselineAsync(factory, "1000001", BaselineKind.Regular, PreviousWeek, area);
        await AddWeekAsync(factory, "1000001", Week, Week.AddDays(2));
        var (_, auth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");

        var saved = (await ReadAsync(factory, auth.User.Id, Week)).Rows.Single(r => r.UserId == person);
        var unsaved = (await ReadAsync(factory, auth.User.Id, NextWeek)).Rows.Single(r => r.UserId == person);

        Assert.True(saved.Days[2].IsDayOff);
        Assert.Equal("day-off", saved.Days[2].Availability);
        Assert.Equal(area, saved.Days[2].Area.Value); // the eligible Area stays visible for a record-first check-in
        Assert.False(unsaved.Days[2].IsDayOff);
        Assert.True(unsaved.Days[2].IsUnset);
        Assert.False(unsaved.Days[2].IsDayOff);
    }

    /// <summary>
    /// Spec §11: a read that races a multi-row write returns the whole before or the whole after projection. Monday's Area
    /// comes from the baseline version and Tuesday's from a dated override, and one command moves both — so a read that
    /// did not cover the week's statements with one snapshot could mix the two answers into a state that never existed.
    /// The write commits on its own connection while the read runs, and every command of one read is asserted to run in
    /// the same transaction, which is what makes MySQL's REPEATABLE READ snapshot cover all of them.
    /// </summary>
    [Fact]
    public async Task A_read_racing_a_multi_row_write_is_the_whole_before_or_the_whole_after()
    {
        // A file database, because the write has to commit on its own connection while the read is running.
        var path = Path.Combine(Path.GetTempPath(), $"facility-schedule-race-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"DataSource={path}").Options;
            int personId, firstAreaId, secondAreaId, tuesdayExceptionId;
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                await DbInitializer.SeedAsync(seed, new Pbkdf2PasswordHasher(iterations: 1_000));
                var admin = await seed.Users.SingleAsync(u => u.Username == "admin");
                var person = await seed.Users.SingleAsync(u => u.EmployeeId == "1000001");
                var building = await seed.Buildings.SingleAsync(b => b.Code == "A");
                firstAreaId = (await seed.Areas.SingleAsync(a => a.Code == "AR01")).Id;
                secondAreaId = (await seed.Areas.SingleAsync(a => a.Code == "AR02")).Id;
                personId = person.Id;
                seed.CleanerProfilePeriods.Add(new CleanerProfilePeriod
                {
                    UserId = person.Id, Kind = CleanerKind.Regular, HomeBuildingId = building.Id,
                    Shift = Shift.Day, EffectiveFrom = Week, Revision = 1, CreatedById = admin.Id,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                });
                var baseline = new ScheduleBaseline
                {
                    UserId = person.Id, Kind = BaselineKind.Regular, EffectiveFrom = Week, Revision = 1,
                    CreatedById = admin.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                };
                foreach (var weekday in Enum.GetValues<DayOfWeek>())
                {
                    baseline.Days.Add(new ScheduleBaselineDay { Weekday = weekday, AreaId = firstAreaId });
                }

                seed.ScheduleBaselines.Add(baseline);
                var tuesday = new ScheduleException
                {
                    UserId = person.Id, Kind = ScheduleExceptionKind.Area, ShiftDate = Week.AddDays(1),
                    Shift = Shift.Day, AreaId = firstAreaId, Revision = 1, CreatedById = admin.Id,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                };
                seed.ScheduleExceptions.Add(tuesday);
                await seed.SaveChangesAsync();
                tuesdayExceptionId = tuesday.Id;
            }

            var transactions = new List<DbTransaction?>();
            var readerOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"DataSource={path}")
                .AddInterceptors(new RecordTransaction(transactions))
                .Options;
            await using var reader = new AppDbContext(readerOptions);
            var store = new ScheduleStore(reader, TimeProvider.System);
            var (beforeArea, movedArea) = (firstAreaId, secondAreaId);

            // One read before the writer starts, so the before state is certainly observed.
            var reads = new List<(int Monday, int Tuesday)> { WeekRow(await store.ReadWeekAsync(personId, Week, null, null)) };

            var writer = Task.Run(async () =>
            {
                for (var step = 0; step < 12; step++)
                {
                    await using var write = new AppDbContext(options);
                    await using var transaction = await write.Database.BeginTransactionAsync();
                    var monday = await write.ScheduleBaselineDays
                        .SingleAsync(day => day.Baseline!.UserId == personId && day.Weekday == DayOfWeek.Monday);
                    var tuesday = await write.ScheduleExceptions.SingleAsync(row => row.Id == tuesdayExceptionId);
                    var area = step % 2 == 0 ? movedArea : beforeArea;
                    monday.AreaId = area;
                    tuesday.AreaId = area;
                    await write.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
            });

            while (!writer.IsCompleted && reads.Count < 40)
            {
                reads.Add(WeekRow(await store.ReadWeekAsync(personId, Week, null, null)));
            }

            await writer;
            reads.Add(WeekRow(await store.ReadWeekAsync(personId, Week, null, null)));

            Assert.All(reads, read => Assert.True(
                read == (beforeArea, beforeArea) || read == (movedArea, movedArea),
                $"a half projection was read: Monday {read.Monday}, Tuesday {read.Tuesday}"));
            Assert.Contains((beforeArea, beforeArea), reads);

            // Every statement of one read runs in the same transaction, so the snapshot covers all of them.
            transactions.Clear();
            var single = await store.ReadWeekAsync(personId, Week, null, null);
            Assert.NotEmpty(transactions);
            Assert.All(transactions, transaction => Assert.Same(transactions[0], transaction));
            Assert.NotNull(transactions[0]);
            Assert.NotEqual(WeekRow(single), default);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Monday's and Tuesday's resolved Areas of the single week row.</summary>
    private static (int Monday, int Tuesday) WeekRow(ScheduleProjection projection)
    {
        var days = Assert.Single(projection.Rows).Days;
        return (days[0].Area.Value!.Value, days[1].Area.Value!.Value);
    }

    private static async Task<ScheduleProjection> ReadAsync(
        JanitorialApiFactory factory, int actorId, DateOnly weekStart, int? buildingId = null, Shift? shift = null)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ScheduleStore>();
        return await store.ReadWeekAsync(actorId, weekStart, buildingId, shift, CancellationToken.None);
    }

    private static Task AddProfileAsync(
        JanitorialApiFactory factory, string employeeId, CleanerKind kind, Shift shift, DateOnly from,
        string? buildingCode = null) =>
        factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            var building = buildingCode is null ? null : await db.Buildings.SingleAsync(b => b.Code == buildingCode);
            db.CleanerProfilePeriods.Add(new CleanerProfilePeriod
            {
                UserId = person.Id, Kind = kind, HomeBuildingId = building?.Id, Shift = shift,
                EffectiveFrom = from, Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    private static Task AddBaselineAsync(
        JanitorialApiFactory factory, string employeeId, BaselineKind kind, DateOnly from, int? areaId) =>
        factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            var baseline = new ScheduleBaseline
            {
                UserId = person.Id, Kind = kind, EffectiveFrom = from, Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            foreach (var weekday in Enum.GetValues<DayOfWeek>())
            {
                baseline.Days.Add(new ScheduleBaselineDay { Weekday = weekday, AreaId = areaId });
            }

            db.ScheduleBaselines.Add(baseline);
            await db.SaveChangesAsync();
        });

    private static Task AddWeekAsync(
        JanitorialApiFactory factory, string employeeId, DateOnly weekStart, params DateOnly[] offDates) =>
        factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            var header = new WeeklyDayOff
            {
                UserId = person.Id, WeekStart = weekStart, Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            foreach (var offDate in offDates)
            {
                header.Choices.Add(new WeeklyDayOffChoice { OffDate = offDate });
            }

            db.WeeklyDayOffs.Add(header);
            await db.SaveChangesAsync();
        });

    private static Task AddAreaExceptionAsync(
        JanitorialApiFactory factory, string employeeId, DateOnly date, Shift shift, int areaId) =>
        factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            db.ScheduleExceptions.Add(new ScheduleException
            {
                UserId = person.Id, Kind = ScheduleExceptionKind.Area, ShiftDate = date, Shift = shift,
                AreaId = areaId, Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    /// <summary>Records the transaction each command of a read ran in, to prove one read is one snapshot.</summary>
    private sealed class RecordTransaction(List<DbTransaction?> transactions) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            transactions.Add(command.Transaction);
            return ValueTask.FromResult(result);
        }
    }
}

/// <summary>
/// Spec §11 on the database the design runs on: SQLite serialises the racing writer behind the reading transaction, so it
/// cannot show that MySQL's REPEATABLE READ snapshot is what keeps a week whole. Here the writer commits between two
/// statements of the same read, which is exactly the interleaving a projection without one snapshot would mix.
/// </summary>
[Collection("MySQL")]
public class MySqlScheduleReadRaceTests
{
    private static readonly DateOnly Week = new(2026, 11, 16);

    [MySqlFact]
    public async Task A_week_read_racing_a_multi_row_write_is_never_half_on_mysql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;
        await using var setup = new AppDbContext(options);
        await setup.Database.EnsureDeletedAsync();
        try
        {
            await setup.Database.MigrateAsync();
            await DbInitializer.SeedAsync(setup, new Pbkdf2PasswordHasher(iterations: 1_000));
            var admin = await setup.Users.SingleAsync(u => u.Username == "admin");
            var person = await setup.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var building = await setup.Buildings.SingleAsync(b => b.Code == "A");
            var beforeArea = (await setup.Areas.SingleAsync(a => a.Code == "AR01")).Id;
            var movedArea = (await setup.Areas.SingleAsync(a => a.Code == "AR02")).Id;
            setup.CleanerProfilePeriods.Add(new CleanerProfilePeriod
            {
                UserId = person.Id, Kind = CleanerKind.Regular, HomeBuildingId = building.Id, Shift = Shift.Day,
                EffectiveFrom = Week, Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            var baseline = new ScheduleBaseline
            {
                UserId = person.Id, Kind = BaselineKind.Regular, EffectiveFrom = Week, Revision = 1,
                CreatedById = admin.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            foreach (var weekday in Enum.GetValues<DayOfWeek>())
            {
                baseline.Days.Add(new ScheduleBaselineDay { Weekday = weekday, AreaId = beforeArea });
            }

            setup.ScheduleBaselines.Add(baseline);
            var tuesday = new ScheduleException
            {
                UserId = person.Id, Kind = ScheduleExceptionKind.Area, ShiftDate = Week.AddDays(1), Shift = Shift.Day,
                AreaId = beforeArea, Revision = 1, CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            setup.ScheduleExceptions.Add(tuesday);
            await setup.SaveChangesAsync();
            var personId = person.Id;
            var tuesdayId = tuesday.Id;

            var fired = 0;
            async Task MoveBothRowsOnceAsync()
            {
                if (Interlocked.Exchange(ref fired, 1) != 0)
                {
                    return;
                }

                await using var write = new AppDbContext(options);
                await using var transaction = await write.Database.BeginTransactionAsync();
                var monday = await write.ScheduleBaselineDays
                    .SingleAsync(day => day.Baseline!.UserId == personId && day.Weekday == DayOfWeek.Monday);
                monday.AreaId = movedArea;
                (await write.ScheduleExceptions.SingleAsync(row => row.Id == tuesdayId)).AreaId = movedArea;
                await write.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            await using var reader = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseMySQL(MySqlTestDatabase.ConnectionString())
                .AddInterceptors(new FireBeforeAsync(
                    sql => sql.Contains("schedule_exceptions", StringComparison.Ordinal), MoveBothRowsOnceAsync))
                .Options);
            var store = new ScheduleStore(reader, TimeProvider.System);

            var racing = await store.ReadWeekAsync(personId, Week, null, null);
            var after = await store.ReadWeekAsync(personId, Week, null, null);

            var racingDays = Assert.Single(racing.Rows).Days;
            Assert.Equal(beforeArea, racingDays[0].Area.Value); // the baseline statement ran before the commit
            Assert.Equal(beforeArea, racingDays[1].Area.Value); // and the override statement still saw its snapshot
            var afterDays = Assert.Single(after.Rows).Days;
            Assert.Equal(movedArea, afterDays[0].Area.Value);
            Assert.Equal(movedArea, afterDays[1].Area.Value);
            Assert.NotEqual(racing.Revision, after.Revision);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>Runs <paramref name="before"/> once, immediately before the first command whose text matches.</summary>
    private sealed class FireBeforeAsync(Func<string, bool> matches, Func<Task> before) : DbCommandInterceptor
    {
        private int _fired;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (matches(command.CommandText) && Interlocked.Exchange(ref _fired, 1) == 0)
            {
                await before();
            }

            return result;
        }
    }
}
