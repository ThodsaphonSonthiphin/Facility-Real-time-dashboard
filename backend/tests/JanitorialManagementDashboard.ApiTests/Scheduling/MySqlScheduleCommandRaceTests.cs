using System.Globalization;
using System.Text.Json;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §11 and acceptance A02/A18: the command engine's exclusion is a property of the database, not of SQLite. These
/// tests use two real connections on the throwaway MySQL database, so a duplicate identity, a stale expected revision
/// and a first create into an empty slot are proved to end in exactly one committed business result and one audit row.
/// They only run when <c>FACILITY_MYSQL_TEST</c> names a throwaway <c>facility_*_test</c> database.
/// </summary>
[Collection("MySQL")]
public class MySqlScheduleCommandRaceTests
{
    private const string TestKey = "synthetic-scheduling-command-key-32-bytes-minimum";

    private static readonly DateOnly Week = new(2026, 11, 16);

    private static DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;

    [MySqlFact]
    public async Task Two_connections_submitting_the_same_identity_commit_one_business_result()
    {
        var (actorId, personId) = await FreshDatabaseAsync();
        var token = new ScheduleCommandTokenService(TestKey).Issue(actorId, TimeProvider.System.GetUtcNow());
        var envelope = new ScheduleCommandEnvelope(
            token.Value, token.Id,
            new Dictionary<string, string> { [RevisionKey(personId, Week)] = "0" },
            WeeklyDaysOffHandler.CommandKind, Payload(personId));

        // Both connections pass the receipt lookup before either commits: that is the duplicate race. The gate proves it.
        var gate = new TwoArrivals();
        await using var first = new AppDbContext(Options());
        await using var second = new AppDbContext(Options());
        var results = await Task.WhenAll(
            Service(first, new GatedWeeklyDaysOffHandler(gate)).ExecuteAsync(actorId, envelope),
            Service(second, new GatedWeeklyDaysOffHandler(gate)).ExecuteAsync(actorId, envelope));

        Assert.All(results, result => Assert.Equal(ScheduleCommandOutcome.Committed, result.Outcome));
        Assert.Equal(2, gate.Arrivals);
        await using var check = new AppDbContext(Options());
        Assert.Single(await check.WeeklyDayOffs.ToListAsync());
        Assert.Equal(1, (await check.WeeklyDayOffs.SingleAsync()).Revision);
        Assert.Single(await check.ScheduleCommandReceipts.ToListAsync());
        Assert.Single(await check.ScheduleChanges.ToListAsync());
    }

    [MySqlFact]
    public async Task Two_connections_claiming_the_same_expected_revision_keep_one_committed_change()
    {
        var (actorId, personId) = await FreshDatabaseAsync();
        var tokens = new ScheduleCommandTokenService(TestKey);
        var now = TimeProvider.System.GetUtcNow();
        var expected = new Dictionary<string, string> { [RevisionKey(personId, Week)] = "0" };
        var one = tokens.Issue(actorId, now);
        var two = tokens.Issue(actorId, now);

        await using var first = new AppDbContext(Options());
        await using var second = new AppDbContext(Options());
        var results = await Task.WhenAll(
            Service(first, new WeeklyDaysOffHandler()).ExecuteAsync(actorId,
                new ScheduleCommandEnvelope(one.Value, one.Id, expected, WeeklyDaysOffHandler.CommandKind, Payload(personId))),
            Service(second, new WeeklyDaysOffHandler()).ExecuteAsync(actorId,
                new ScheduleCommandEnvelope(two.Value, two.Id, expected, WeeklyDaysOffHandler.CommandKind, Payload(personId))));

        // A02/A18: the stale command neither applies nor leaves a receipt; nothing is merged or half-written.
        Assert.Single(results, result => result.Outcome == ScheduleCommandOutcome.Committed);
        var rejected = Assert.Single(results, result => result.Outcome == ScheduleCommandOutcome.Rejected);
        Assert.Equal(ScheduleCommandErrorCode.StaleRevision, Assert.Single(rejected.Problems).Code);
        await using var check = new AppDbContext(Options());
        Assert.Single(await check.WeeklyDayOffs.ToListAsync());
        Assert.Equal(1, (await check.WeeklyDayOffs.SingleAsync()).Revision);
        Assert.Single(await check.ScheduleCommandReceipts.ToListAsync());
        Assert.Single(await check.ScheduleChanges.ToListAsync());
    }

    [MySqlFact]
    public async Task Two_connections_locking_the_same_empty_slot_create_one_anchor()
    {
        var (_, personId) = await FreshDatabaseAsync();
        var now = DateTime.UtcNow;

        // The anchor is created by whichever transaction gets there first; the other must find it and wait instead of
        // inserting a second row (spec §11: unique-anchor create/retry for empty slots).
        await using var first = new AppDbContext(Options());
        await using var second = new AppDbContext(Options());
        await Task.WhenAll(LockAsync(first), LockAsync(second));

        await using var check = new AppDbContext(Options());
        var anchor = Assert.Single(await check.ScheduleAnchors.ToListAsync());
        Assert.Equal(personId, anchor.PersonId);
        Assert.Equal(Week, anchor.ShiftDate);
        Assert.Equal(Shift.Day, anchor.Shift);
        Assert.True(anchor.Revision >= 2, $"revision {anchor.Revision}");
        return;

        async Task LockAsync(AppDbContext db)
        {
            var store = new ScheduleStore(db, TimeProvider.System);
            await using var transaction = await store.BeginCommandTransactionAsync();
            await store.LockSlotAsync(personId, Week, Shift.Day, now);
            await transaction.CommitAsync();
        }
    }

    private static async Task<(int ActorId, int PersonId)> FreshDatabaseAsync()
    {
        await using var db = new AppDbContext(Options());
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DbInitializer.SeedAsync(db, new Pbkdf2PasswordHasher(iterations: 1_000));
        var actor = await db.Users.SingleAsync(user => user.Username == "admin");
        var person = await db.Users.SingleAsync(user => user.EmployeeId == "1000001");
        return (actor.Id, person.Id);
    }

    private static ScheduleCommandService Service(AppDbContext db, IScheduleCommandHandler handler) =>
        new(db, new ScheduleStore(db, TimeProvider.System), new ScheduleCommandTokenService(TestKey),
            [handler], new SilentNotifier(), TimeProvider.System);

    private static string RevisionKey(int personId, DateOnly weekStart) =>
        $"week#{personId}#{weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    private static JsonElement Payload(int personId) => JsonSerializer.SerializeToElement(new
    {
        personId,
        weekStart = Week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        offDates = new[] { "2026-11-17" },
    });

    /// <summary>Holds both commands inside their handler until both have passed the retained-result lookup.</summary>
    private sealed class TwoArrivals
    {
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public int Arrivals => _arrivals;

        public async Task WaitAsync()
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _both.TrySetResult();
            }

            await _both.Task;
        }
    }

    /// <summary>The weekly day-off kind without anchors: the duplicate identity race is about the receipt, not a row lock.</summary>
    private class WeeklyDaysOffHandler : IScheduleCommandHandler
    {
        public const string CommandKind = "test-set-weekly-days-off";

        public string Kind => CommandKind;

        public virtual Task<ScheduleCommandLockSet> PlanLocksAsync(
            ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default) =>
            Task.FromResult(new ScheduleCommandLockSet(
                [new ScheduleSlotKey(PersonId(request.Payload), Week, Shift.Day)], [PersonId(request.Payload)], []));

        public async Task<ScheduleCommandApplication> ValidateAndApplyAsync(
            ScheduleCommandRequest request, ScheduleCommandReceipt receipt, AppDbContext db, CancellationToken ct = default)
        {
            await BeforeApplyAsync();
            var personId = PersonId(request.Payload);
            var key = RevisionKey(personId, Week);
            var header = await db.WeeklyDayOffs.Include(week => week.Choices)
                .FirstOrDefaultAsync(week => week.UserId == personId && week.WeekStart == Week, ct);
            var current = (header?.Revision ?? 0).ToString(CultureInfo.InvariantCulture);
            var revisions = new Dictionary<string, string>(StringComparer.Ordinal) { [key] = current };
            if (request.ExpectedRevisions.TryGetValue(key, out var expected) && expected != current)
            {
                return new ScheduleCommandApplication(revisions,
                    [new RowProblem(key, ScheduleCommandErrorCode.StaleRevision, $"expected {expected}, current {current}")]);
            }

            var created = header is null;
            header ??= new WeeklyDayOff
            {
                UserId = personId,
                WeekStart = Week,
                CreatedById = request.ActorId,
                CreatedAt = request.Now.UtcDateTime,
                UpdatedAt = request.Now.UtcDateTime,
            };
            if (created)
            {
                db.WeeklyDayOffs.Add(header);
            }

            // A created header keeps its first version; only an update of an existing one bumps its revision (the same
            // revision coupling the SQLite harness applies). A new row that started at 2 would make the audit trail claim
            // an edit that never happened.
            if (!created)
            {
                header.Revision += 1;
            }

            header.UpdatedAt = request.Now.UtcDateTime;
            db.ScheduleChanges.Add(new ScheduleChange
            {
                Receipt = receipt,
                EntityType = "WeeklyDayOff",
                EntityId = header.Id == 0 ? null : header.Id,
                Operation = created ? "create" : "update",
                CreatedAt = request.Now.UtcDateTime,
            });
            revisions[key] = header.Revision.ToString(CultureInfo.InvariantCulture);
            return new ScheduleCommandApplication(revisions, []);
        }

        /// <summary>A hook the duplicate race uses to hold both commands inside their handler.</summary>
        protected virtual Task BeforeApplyAsync() => Task.CompletedTask;

        protected static int PersonId(JsonElement payload) => payload.GetProperty("personId").GetInt32();
    }

    private sealed class GatedWeeklyDaysOffHandler(TwoArrivals gate) : WeeklyDaysOffHandler
    {
        public override Task<ScheduleCommandLockSet> PlanLocksAsync(
            ScheduleCommandRequest request, AppDbContext db, CancellationToken ct = default) =>
            Task.FromResult(ScheduleCommandLockSet.None);

        protected override Task BeforeApplyAsync() => gate.WaitAsync();
    }

    private sealed class SilentNotifier : IScheduleCommandNotifier
    {
        public Task NotifyCommittedAsync(CommandResult result, CancellationToken ct = default) => Task.CompletedTask;
    }
}
