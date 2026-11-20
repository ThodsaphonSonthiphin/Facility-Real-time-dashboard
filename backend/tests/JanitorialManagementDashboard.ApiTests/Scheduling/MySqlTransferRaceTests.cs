using JanitorialManagementDashboard.Api.Endpoints;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Geo;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §9 and ADRs facility-0213/0204 with acceptance A02 and A22: cancel-versus-confirm is one state transition decided by
/// the database, not by whichever request the process happened to schedule first, and a repeated confirmation is one arrival
/// however many phones send it. These tests use two independently scoped <see cref="AppDbContext"/>s on real MySQL
/// connections, so both writers really race.
/// <para>
/// They only run when <c>FACILITY_MYSQL_TEST</c> names a throwaway <c>facility_*_test</c> database; without it they are
/// skipped, and that skip is recorded rather than claimed as a pass.
/// </para>
/// </summary>
[Collection("MySQL")]
public class MySqlTransferRaceTests
{
    private static readonly DateOnly Date = new(2026, 11, 17);

    private static readonly DateTime EffectiveAt = new(2026, 11, 17, 6, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime ArrivalAt = new(2026, 11, 17, 6, 5, 0, DateTimeKind.Utc);

    private static DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>().UseMySQL(MySqlTestDatabase.ConnectionString()).Options;

    [MySqlFact]
    public async Task Cancel_against_confirm_leaves_exactly_one_winning_state_transition()
    {
        var seed = await FreshDatabaseAsync();

        // Both writers start from the same stored instruction: one cancels it, the other confirms arrival under it.
        await using var cancelling = new AppDbContext(Options());
        await using var confirming = new AppDbContext(Options());
        var gate = new TwoArrivals();
        var results = await Task.WhenAll(
            CancelAsync(cancelling, seed, gate),
            ConfirmAsync(confirming, seed, gate));

        var cancelled = results[0];
        var confirmed = results[1];

        // Exactly one of the two transitions happened: either the cancellation won and the arrival was refused, or the
        // arrival won and the cancellation was refused. Never both, and never neither.
        var winner = Assert.Single(results, result => result.Outcome != TransferState.Other);
        var loser = Assert.Single(results, result => result.Outcome == TransferState.Other);

        await using var check = new AppDbContext(Options());
        var instruction = Assert.Single(await check.AreaTransfers.ToListAsync());
        var confirmations = await check.AreaTransferConfirmations.ToListAsync();
        if (winner.Outcome == TransferState.Cancelled)
        {
            // The row is retained as cancelled history, and nobody arrived.
            Assert.True(instruction.IsCancelled);
            Assert.Equal(2, instruction.Revision);
            Assert.NotNull(instruction.CancelledById);
            Assert.Empty(confirmations);
            Assert.Equal(cancelled.Outcome, winner.Outcome);
            Assert.Equal("transfer-cancelled", loser.Revision);
        }
        else
        {
            // The arrival is evidence history keeps: the cancellation could not remove it, and the row was left alone.
            Assert.False(instruction.IsCancelled);
            Assert.Equal(1, instruction.Revision);
            var arrival = Assert.Single(confirmations);
            Assert.Equal(instruction.Id, arrival.TransferId);
            Assert.Equal(instruction.ToAreaId, arrival.AreaId);
            Assert.Equal(confirmed.Outcome, winner.Outcome);
        }
    }

    [MySqlFact]
    public async Task Duplicate_confirmations_store_one_row_and_answer_the_same_fact()
    {
        var seed = await FreshDatabaseAsync();

        await using var first = new AppDbContext(Options());
        await using var second = new AppDbContext(Options());
        var gate = new TwoArrivals();
        var results = await Task.WhenAll(
            ConfirmAsync(first, seed, gate),
            ConfirmAsync(second, seed, gate));

        // The unique index on transfer_id arbitrates it: one insert commits, the loser re-reads and answers the same fact.
        Assert.All(results, result => Assert.Equal(TransferState.Confirmed, result.Outcome));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(results[0].Revision, results[1].Revision);
        Assert.Equal(results[0].ConfirmedAt, results[1].ConfirmedAt);

        await using var check = new AppDbContext(Options());
        var instruction = Assert.Single(await check.AreaTransfers.ToListAsync());
        var confirmation = Assert.Single(await check.AreaTransferConfirmations.ToListAsync());
        Assert.Equal(instruction.Id, confirmation.TransferId);
        Assert.Equal(results[0].ConfirmedAt!.Value.UtcDateTime, confirmation.OccurredAt);
        // The break progress and the shift itself are untouched: no second Shift-In, no Shift-Out, no break event.
        Assert.Equal(1, instruction.Revision);
        Assert.False(instruction.IsCancelled);
        var facts = await check.ShiftAttendances.ToListAsync();
        Assert.Single(facts);
        Assert.Equal(AttendanceEvent.ShiftIn, facts[0].EventType);
    }

    private static async Task<TransferStateResult> CancelAsync(
        AppDbContext db, Seed seed, TwoArrivals gate)
    {
        var clock = new FixedClock(ArrivalAt);
        var service = new ScheduleCommandService(db, new ScheduleStore(db, clock), Tokens,
            [new CancelTransferHandler(clock)], new SilentNotifier(), clock);
        await gate.WaitAsync();
        var tokens = Tokens.Issue(seed.AdminId, new DateTimeOffset(ArrivalAt, TimeSpan.Zero));
        var result = await service.ExecuteAsync(seed.AdminId, new ScheduleCommandEnvelope(
            tokens.Value, tokens.Id,
            new Dictionary<string, string> { [ScheduleRevisionKeys.Transfer(seed.PersonId, Date, Shift.Day)] = "1" },
            ScheduleCommandKinds.CancelTransfer,
            System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                transferId = seed.TransferId,
                reason = "ยกเลิกเพราะย้ายกลับพื้นที่เดิม",
            })));

        return result.Outcome == ScheduleCommandOutcome.Committed
            ? new TransferStateResult(TransferState.Cancelled, 0, null, "2")
            : new TransferStateResult(TransferState.Other, 0, null, "1");
    }

    private static async Task<TransferStateResult> ConfirmAsync(
        AppDbContext db, Seed seed, TwoArrivals gate)
    {
        var clock = new FixedClock(ArrivalAt);
        var service = new TransferCommandService(db, new ScheduleStore(db, clock), clock,
            new AttendanceSettings());
        await gate.WaitAsync();
        var (result, refusal, _) = await service.ConfirmAsync(
            seed.PersonId, seed.TransferId, "1", "token-checkin-ar03",
            new GpsInputDto(13.7563, 100.5018, 10));

        return result is not null
            ? new TransferStateResult(TransferState.Confirmed, result.Id, result.ConfirmedAt, result.Revision)
            : new TransferStateResult(TransferState.Other, 0, null, refusal!.Code);
    }

    private static readonly ScheduleCommandTokenService Tokens = new("synthetic-scheduling-command-key-32-bytes-minimum");

    private static async Task<Seed> FreshDatabaseAsync()
    {
        await using var db = new AppDbContext(Options());
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DbInitializer.SeedAsync(db, new Pbkdf2PasswordHasher(iterations: 1_000));

        var admin = await db.Users.SingleAsync(user => user.Username == "admin");
        var person = await db.Users.SingleAsync(user => user.EmployeeId == "1000001");

        // The destination Area with a captured sign, the origin as the person's own Area, and the arrived-and-not-departed
        // state an instruction requires.
        var building = await db.Buildings.SingleAsync(entry => entry.Code == "A");
        var destination = new Area
        {
            BuildingId = building.Id,
            Code = "AR03",
            Name = "Area 3",
            ShiftPattern = ShiftPattern.DayAndNight,
            CreatedAt = DateTime.UtcNow,
        };
        db.Areas.Add(destination);
        db.Signs.Add(new Sign
        {
            Area = destination,
            Code = "AR03-IN",
            QrToken = "token-checkin-ar03",
            QrIssuedAt = DateTime.UtcNow,
            Latitude = 13.7563m,
            Longitude = 100.5018m,
            LocationSource = LocationSource.Site,
            LocatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        db.ShiftAttendances.Add(new ShiftAttendance
        {
            UserId = person.Id,
            AreaId = person.AreaId!.Value,
            ShiftDate = Date,
            Shift = Shift.Day,
            EventType = AttendanceEvent.ShiftIn,
            OccurredAt = new DateTime(2026, 11, 17, 0, 5, 0, DateTimeKind.Utc),
            Source = AttendanceSource.Scan,
            CreatedAt = new DateTime(2026, 11, 17, 0, 5, 0, DateTimeKind.Utc),
        });
        var instruction = new AreaTransfer
        {
            PersonId = person.Id,
            ShiftDate = Date,
            Shift = Shift.Day,
            FromAreaId = person.AreaId!.Value,
            ToAreaId = destination.Id,
            EffectiveAt = EffectiveAt,
            Reason = "ย้ายไปช่วยพื้นที่ปลายทาง",
            CreatedById = admin.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.AreaTransfers.Add(instruction);
        await db.SaveChangesAsync();

        return new Seed(admin.Id, person.Id, instruction.Id);
    }

    private sealed record Seed(int AdminId, int PersonId, int TransferId);

    /// <summary>The two state transitions the race is about, plus "neither, and here is why".</summary>
    private enum TransferState
    {
        Confirmed,
        Cancelled,
        Other,
    }

    private sealed record TransferStateResult(TransferState Outcome, int Id, DateTimeOffset? ConfirmedAt, string Revision);

    /// <summary>A clock pinned to one instant, so both writers race on the same effective-time comparison.</summary>
    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero);
    }

    /// <summary>
    /// Holds both writers until each has read the instruction and is about to act, so the race is between two real
    /// transactions on two connections rather than between one request and the next.
    /// </summary>
    private sealed class TwoArrivals
    {
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public async Task WaitAsync()
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _both.TrySetResult();
            }

            await _both.Task;
        }
    }

    private sealed class SilentNotifier : IScheduleCommandNotifier
    {
        public Task NotifyCommittedAsync(CommandResult result, ScheduleChangeScope scope, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
