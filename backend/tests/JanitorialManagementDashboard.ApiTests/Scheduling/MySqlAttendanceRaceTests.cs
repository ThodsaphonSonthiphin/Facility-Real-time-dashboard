using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using JanitorialManagementDashboard.Api.Endpoints;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
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
/// Spec §8 and §11 with acceptance A02: the attendance writer's exclusion is a property of the database, not of SQLite, and
/// a plan change (cancelling leave) never rewrites an actual fact or the revision a page holds. These tests use real MySQL
/// connections, so a second fill of the same missing action is proved to lose to the committed fact, and a read racing a
/// leave cancellation is proved to answer the same facts and the same token.
/// They only run when <c>FACILITY_MYSQL_TEST</c> names a throwaway <c>facility_*_test</c> database.
/// </summary>
[Collection("MySQL")]
public class MySqlAttendanceRaceTests
{
    private static readonly DateOnly Date = new(2026, 11, 17);
    private static readonly ShiftSlot Slot = new(Date, Shift.Day);

    private static DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;

    [MySqlFact]
    public async Task Two_different_numbered_scans_from_one_revision_commit_only_one_action()
    {
        await FreshDatabaseAsync();
        var gate = new RacingSlotLocks();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(MySqlTestDatabase.ConnectionString()).AddInterceptors(gate).Options;
        using var factory = new JanitorialApiFactory
        {
            ConfigureTestServices = services => services.AddScoped(_ => new AppDbContext(options)),
        };
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Date, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 17, 7, 5, 0, TimeSpan.FromHours(7)));
        var initial = await EffectiveAttendanceApi.StateAsync(cleaner);
        await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, factory.Clock.GetUtcNow(), "ShiftIn",
            lifecycleVersion: "Numbered6", stateRevision: initial.StateRevision);

        factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().AddHours(5));
        var working = await EffectiveAttendanceApi.StateAsync(cleaner);
        gate.Armed = true;
        var responses = await Task.WhenAll(Scan("Break1Out"), Scan("ShiftOut"));
        Assert.Equal(2, gate.Attempts);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var refused = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("refresh-required", (await refused.Content.ReadFromJsonAsync<RefusedAnswer>())!.Code);

        var final = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(2, final.Events.Count);
        var actual = final.Events.Select(row => row.EventType).ToList();
        Assert.DoesNotContain(final.Gaps!, gap => actual.Contains(gap.EventType) && gap.Status == "Skipped");
        return;

        Task<HttpResponseMessage> Scan(string action) => EffectiveAttendanceApi.RecordAsync(cleaner, action,
            lifecycleVersion: "Numbered6", stateRevision: working.StateRevision,
            confirmGaps: working.NextActions!.Single(choice => choice.Action == action).RequiredGaps);
    }

    // Wait for both independent request transactions immediately before either can obtain the slot.
    private sealed class RacingSlotLocks : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempts;
        public bool Armed { get; set; }
        public int Attempts => _attempts;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.Contains("FROM schedule_anchors", StringComparison.Ordinal)
                && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _attempts) == 2)
                {
                    _both.TrySetResult();
                }
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    [MySqlFact]
    public async Task Two_parallel_fills_of_the_same_missing_action_keep_the_first_fact()
    {
        var (personId, areaId) = await FreshDatabaseAsync();
        var at = new DateTime(2026, 11, 17, 3, 5, 0, DateTimeKind.Utc);

        await using var first = new AppDbContext(Options());
        await using var second = new AppDbContext(Options());
        var results = await Task.WhenAll(FillAsync(first), FillAsync(second));

        Assert.Equal(1, results.Count(result => result.Stored));
        var refused = results.Single(result => !result.Stored);
        Assert.Contains(refused.Facts, fact => fact.EventType == AttendanceEvent.BreakOut);

        await using var check = new AppDbContext(Options());
        var facts = await check.ShiftAttendances.AsNoTracking()
            .Where(fact => fact.UserId == personId && fact.ShiftDate == Date && fact.Shift == Shift.Day)
            .ToListAsync();
        var stored = Assert.Single(facts);
        Assert.Equal(AttendanceEvent.BreakOut, stored.EventType);
        Assert.Equal(AttendanceSource.SupervisorAdd, stored.Source);
        Assert.Equal(at, stored.OccurredAt);
        Assert.Empty(await check.AttendanceGaps.ToListAsync());
        return;

        Task<AttendanceWrites.Written> FillAsync(AppDbContext db) => AttendanceWrites.RecordAsync(db,
            new ShiftAttendance
            {
                UserId = personId,
                AreaId = areaId,
                ShiftDate = Date,
                Shift = Shift.Day,
                EventType = AttendanceEvent.BreakOut,
                OccurredAt = at,
                Source = AttendanceSource.SupervisorAdd,
                CreatedAt = at,
            },
            [], areaId, personId, at);
    }

    [MySqlFact]
    public async Task Cancelling_leave_while_reading_actual_times_never_changes_the_facts_or_the_revision()
    {
        var (personId, areaId) = await FreshDatabaseAsync();
        await using (var seed = new AppDbContext(Options()))
        {
            var admin = await seed.Users.SingleAsync(user => user.Username == "admin");
            seed.ShiftAttendances.AddRange(
                Fact(AttendanceEvent.ShiftIn, new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc)),
                Fact(AttendanceEvent.Break1Out, new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc)));
            seed.ScheduleExceptions.Add(new ScheduleException
            {
                UserId = personId,
                Kind = ScheduleExceptionKind.Leave,
                ShiftDate = Date,
                Shift = Shift.Day,
                StartsAt = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc),
                EndsAt = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc),
                Revision = 1,
                CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        ShiftAttendance Fact(AttendanceEvent action, DateTime occurredAt) => new()
        {
            UserId = personId,
            AreaId = areaId,
            ShiftDate = Date,
            Shift = Shift.Day,
            EventType = action,
            OccurredAt = occurredAt,
            Source = AttendanceSource.Scan,
            CreatedAt = occurredAt,
        };

        var revisionBefore = await RevisionAsync();
        var dutyWhileOnLeave = await DutyAsync();

        // A read of the actual facts runs while the leave is being cancelled on another connection.
        var reading = Task.Run(async () =>
        {
            await using var db = new AppDbContext(Options());
            return await db.ShiftAttendances.AsNoTracking()
                .Where(fact => fact.UserId == personId && fact.ShiftDate == Date && fact.Shift == Shift.Day)
                .ToListAsync();
        });
        await using (var cancel = new AppDbContext(Options()))
        {
            var leave = await cancel.ScheduleExceptions.SingleAsync();
            leave.IsCancelled = true;
            leave.Revision += 1;
            await cancel.SaveChangesAsync();
        }

        var factsReadMeanwhile = await reading;

        Assert.Equal(2, factsReadMeanwhile.Count);
        Assert.Equal(revisionBefore, AttendanceStateRevision.Of(personId, Slot, factsReadMeanwhile));
        Assert.Equal(revisionBefore, await RevisionAsync());
        // The plan moved, the actual facts and their token did not: the eligible Area stays visible either way.
        Assert.True(dutyWhileOnLeave.IsLeave);
        Assert.Equal(areaId, dutyWhileOnLeave.AreaId);
        var dutyAfterCancellation = await DutyAsync();
        Assert.False(dutyAfterCancellation.IsLeave);
        Assert.Equal(areaId, dutyAfterCancellation.AreaId);
        return;

        async Task<string> RevisionAsync()
        {
            await using var db = new AppDbContext(Options());
            var facts = await db.ShiftAttendances.AsNoTracking()
                .Where(fact => fact.UserId == personId && fact.ShiftDate == Date && fact.Shift == Shift.Day)
                .ToListAsync();
            return AttendanceStateRevision.Of(personId, Slot, facts);
        }

        async Task<EffectiveDuty> DutyAsync()
        {
            await using var db = new AppDbContext(Options());
            var store = new ScheduleStore(db, TimeProvider.System);
            var person = await db.Users.SingleAsync(user => user.Id == personId);
            return await EffectiveDutyReader.ReadAsync(db, store, person, Slot,
                new DateTimeOffset(2026, 11, 17, 3, 5, 0, TimeSpan.Zero));
        }
    }

    private static async Task<(int PersonId, int AreaId)> FreshDatabaseAsync()
    {
        await using var db = new AppDbContext(Options());
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DbInitializer.SeedAsync(db, new Pbkdf2PasswordHasher(iterations: 1_000));
        var person = await db.Users.SingleAsync(user => user.EmployeeId == "1000001");
        return (person.Id, person.AreaId!.Value);
    }
}
