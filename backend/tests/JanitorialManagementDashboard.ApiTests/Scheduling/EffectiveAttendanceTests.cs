using System.Net;
using System.Net.Http.Json;
using JanitorialManagementDashboard.Api.Endpoints;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Attendance;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §7 with ruling A: the Area a Cleaner may check in at is the slot's effective duty — a live relief assignment, the
/// dated plan, or — only when no dated identity exists at all — the account's compatibility Area. An absence record keeps
/// the underlying eligible Area visible and is shown as a conflict marker beside it, never as a reason to hide the Area.
/// </summary>
public class EffectiveAttendanceTests
{
    private static readonly DateOnly Day8 = EffectiveAttendanceApi.Day8;

    [Fact]
    public async Task A_dedicated_relief_scans_at_the_sign_of_their_live_assignment()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddAreaAsync(factory, "AR03", "A");
        await TestData.AddBuildingAsync(factory, "B");
        var relief = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR03");
        await ReliefApi.AddStoredReliefAsync(factory, relief, destination, Day8, Shift.Day,
            await AdminSetupApi.BuildingIdAsync(factory, "B"));
        var (client, auth, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E3001", ReliefApi.Phone("E3001"));

        var state = await EffectiveAttendanceApi.RecordAndReadAsync(factory, client, EffectiveAttendanceApi.Thai(8, 7, 2),
            "ShiftIn", "token-checkin-ar03");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(destination, state.DutyAreaId);
        Assert.Equal("relief", state.DutySource);
        var stored = Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Equal(destination, stored.AreaId);
        Assert.Equal(auth.User.Id, stored.UserId);
    }

    [Fact]
    public async Task A_relief_with_only_a_home_building_is_refused_and_writes_nothing()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddAreaAsync(factory, "AR03", "A");
        await TestData.AddBuildingAsync(factory, "B");
        // The person exists as a relief of building B with a dated identity, but nobody assigned them this slot: a home
        // building alone grants no duty Area (spec §7).
        await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var (client, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E3001", ReliefApi.Phone("E3001"));
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 2));

        var response = await EffectiveAttendanceApi.RecordAsync(client, "ShiftIn", "token-checkin-ar03");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("AR03", (await response.Content.ReadFromJsonAsync<BlockedScanModel>())!.AreaCode);
        Assert.Empty(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
        Assert.Single(await EffectiveAttendanceApi.BlockedAsync(factory));
    }

    [Fact]
    public async Task A_dated_plan_area_wins_over_a_stale_account_area()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddAreaAsync(factory, "AR03", "A");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR03");
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        // 1000001's account still names AR01; the plan for 8 October says AR03.
        var planned = EffectiveAttendanceApi.Thai(8, 7, 1);
        factory.Clock.SetUtcNow(planned);
        var accepted = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn", "token-checkin-ar03");
        var blocked = await EffectiveAttendanceApi.RecordAsync(cleaner, "BreakOut", "token-checkin-ar01");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var state = (await accepted.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR03"), state.DutyAreaId);
        Assert.Equal("baseline", state.DutySource);
        var stored = Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR03"), stored.AreaId);
    }

    [Fact]
    public async Task An_eligible_check_in_on_a_leave_day_is_accepted_with_a_visible_conflict()
    {
        using var factory = new JanitorialApiFactory();
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01");
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await EffectiveAttendanceApi.AddLeaveAsync(factory, "1000001", Day8, Shift.Day,
            new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var state = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 5), "ShiftIn");

        Assert.Equal("Recorded", state.Outcome);
        Assert.True(state.IsLeave);
        Assert.False(state.IsDayOff);
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR01"), state.DutyAreaId);
        var stored = Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Equal(cleanerId, stored.UserId);
        // Nothing about the plan moved: the leave row is still one agreed interval and no schedule row was rewritten.
        await factory.WithDbAsync(async db =>
        {
            var leave = Assert.Single(await db.ScheduleExceptions.ToListAsync());
            Assert.False(leave.IsCancelled);
            Assert.Equal(cleanerId, leave.UserId);
            Assert.Single(await db.CleanerProfilePeriods.ToListAsync());
        });
    }

    [Fact]
    public async Task A_leave_conflict_clears_only_after_a_saved_correction_and_keeps_the_original_checkin()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var baselineStart = new DateOnly(2026, 10, 1);
        await ScheduleWriteApi.AddWeekdayProfileAsync(factory, "1000001", baselineStart);
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", baselineStart, "AR01");
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var checkedInAt = EffectiveAttendanceApi.Thai(8, 7, 0);
        await EffectiveAttendanceApi.AddLeaveAsync(factory, "1000001", Day8, Shift.Day,
            checkedInAt, EffectiveAttendanceApi.Thai(8, 19, 0));
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var checkedIn = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, checkedInAt, "ShiftIn");
        Assert.True(checkedIn.IsLeave);
        Assert.Equal(checkedInAt.UtcDateTime, Assert.Single(checkedIn.Events).OccurredAt);

        var correctedAt = EffectiveAttendanceApi.Thai(8, 7, 10);
        factory.Clock.SetUtcNow(correctedAt);
        var leaveKey = ScheduleWriteApi.LeaveKey(person, Day8, "Day");
        var cancellation = new { userId = person, date = Day8, shift = "Day", reason = "กลับมาทำงาน" };
        var stale = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CancelLeave, cancellation,
            new Dictionary<string, string> { [leaveKey] = "0" });
        Assert.Equal(HttpStatusCode.Conflict, stale.Response.StatusCode);
        Assert.True((await EffectiveAttendanceApi.StateAsync(cleaner)).IsLeave);

        var saved = await ScheduleWriteApi.ExecuteAsync(admin, ScheduleWriteApi.CancelLeave, cancellation,
            new Dictionary<string, string> { [leaveKey] = "1" });
        Assert.Equal(HttpStatusCode.OK, saved.Response.StatusCode);
        var corrected = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.False(corrected.IsLeave);
        Assert.Equal(checkedIn.StateRevision, corrected.StateRevision);
        Assert.Equal(Assert.Single(checkedIn.Events), Assert.Single(corrected.Events));
        Assert.Equal(checkedInAt.UtcDateTime, Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory)).OccurredAt);
        await factory.WithDbAsync(async db =>
            Assert.Equal(correctedAt.UtcDateTime, (await db.ScheduleExceptions.SingleAsync()).CancelledAt));
    }

    [Fact]
    public async Task A_real_early_checkout_marks_later_leave_actions_inapplicable_and_preserves_earlier_gaps()
    {
        using var factory = new JanitorialApiFactory();
        var baselineStart = new DateOnly(2026, 10, 1);
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, baselineStart);
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", baselineStart, "AR01", breakGroup: 1);
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var checkout = EffectiveAttendanceApi.Thai(8, 13, 0);
        await EffectiveAttendanceApi.AddLeaveAsync(factory, "1000001", Day8, Shift.Day,
            checkout, EffectiveAttendanceApi.Thai(8, 19, 0));
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 5));
        var before = await EffectiveAttendanceApi.StateAsync(cleaner);
        var entered = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 5),
            "ShiftIn", lifecycleVersion: before.LifecycleVersion, stateRevision: before.StateRevision);
        var gaps = entered.NextActions!.Single(choice => choice.Action == "ShiftOut").RequiredGaps;

        var departed = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, checkout, "ShiftOut",
            lifecycleVersion: entered.LifecycleVersion, stateRevision: entered.StateRevision, confirmGaps: gaps);

        Assert.Equal("skipped", departed.Action("Break1Out").State);
        Assert.Equal("not-applicable", departed.Action("Break2Out").State);
        Assert.Equal("not-applicable", departed.Action("Break2In").State);
        Assert.Null(departed.Action("Break2In").OccurredAt);
        Assert.Equal(checkout.UtcDateTime, departed.Action("ShiftOut").OccurredAt);
        Assert.False(departed.DepartureLeaveMismatch);
        Assert.Equal(2, (await EffectiveAttendanceApi.FactsAsync(factory)).Count);
        Assert.Equal(4, (await EffectiveAttendanceApi.GapsAsync(factory)).Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(15)]
    public async Task Early_departure_without_applicable_leave_is_flagged_without_moving_actual_time(int? leaveStartsMinutesLater)
    {
        using var factory = new JanitorialApiFactory();
        var baselineStart = new DateOnly(2026, 10, 1);
        await ScheduleWriteApi.AddWeekdayProfileAsync(factory, "1000001", baselineStart);
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", baselineStart, "AR01", breakGroup: 1);
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var checkout = EffectiveAttendanceApi.Thai(8, 13, 0);
        if (leaveStartsMinutesLater is { } minutes)
        {
            await EffectiveAttendanceApi.AddLeaveAsync(factory, "1000001", Day8, Shift.Day,
                checkout.AddMinutes(minutes), EffectiveAttendanceApi.Thai(8, 19, 0));
        }

        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        var checkIn = EffectiveAttendanceApi.Thai(8, 7, 0);
        factory.Clock.SetUtcNow(checkIn);
        var initial = await EffectiveAttendanceApi.StateAsync(cleaner);
        var entered = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, checkIn, "ShiftIn",
            lifecycleVersion: initial.LifecycleVersion, stateRevision: initial.StateRevision);
        var gaps = entered.NextActions!.Single(choice => choice.Action == "ShiftOut").RequiredGaps;
        var departed = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, checkout, "ShiftOut",
            lifecycleVersion: entered.LifecycleVersion, stateRevision: entered.StateRevision, confirmGaps: gaps);

        Assert.Equal(leaveStartsMinutesLater.HasValue, departed.IsLeave);
        Assert.Equal(checkout.UtcDateTime, departed.Action("ShiftOut").OccurredAt);
        Assert.Equal(2, (await EffectiveAttendanceApi.FactsAsync(factory)).Count);
        await factory.WithDbAsync(async db =>
            Assert.Equal(leaveStartsMinutesLater.HasValue ? 1 : 0, await db.ScheduleExceptions.CountAsync()));
        Assert.True(departed.DepartureLeaveMismatch);
        Assert.True((await EffectiveAttendanceApi.StateAsync(cleaner)).DepartureLeaveMismatch);
    }

    [Fact]
    public async Task An_eligible_check_in_on_a_day_off_is_accepted_with_a_visible_conflict()
    {
        using var factory = new JanitorialApiFactory();
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01");
        await ScheduleWriteApi.AddStoredWeekAsync(factory, "1000001", new DateOnly(2026, 10, 5), Day8);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var state = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 5), "ShiftIn");

        Assert.Equal("Recorded", state.Outcome);
        Assert.True(state.IsDayOff);
        Assert.False(state.IsLeave);
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR01"), state.DutyAreaId);
        // The day off is still recorded exactly as the Supervisor saved it: the check-in adds an actual fact, not a plan edit.
        await factory.WithDbAsync(async db =>
        {
            var week = Assert.Single(await db.WeeklyDayOffs.Include(row => row.Choices).ToListAsync());
            Assert.Equal(Day8, Assert.Single(week.Choices).OffDate);
        });
    }

    [Fact]
    public async Task A_slot_before_the_dated_identity_is_unavailable_and_never_the_account_area()
    {
        using var factory = new JanitorialApiFactory();
        await TestData.AddAreaAsync(factory, "AR03", "A");
        // The dated identity starts on 1 October; 30 September is before the person's history coverage (spec §10).
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR03");
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 9, 30, 7, 2, 0, TimeSpan.FromHours(7)));

        // The resolver says "unavailable" for that date, so there is no duty Area — and the account's AR01 is not an answer
        // for somebody who has a dated identity at all (spec §14).
        var response = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn", "token-checkin-ar01");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));
        Assert.Single(await EffectiveAttendanceApi.BlockedAsync(factory));
    }

    [Fact]
    public async Task An_account_with_no_dated_identity_keeps_its_own_area_as_the_compatibility_projection()
    {
        using var factory = new JanitorialApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var state = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 5), "ShiftIn");

        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(await AdminSetupApi.AreaIdAsync(factory, "AR01"), state.DutyAreaId);
        Assert.Equal("compatibility", state.DutySource);
        Assert.Null(state.DutyBreakGroup);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Spec §9: a confirmed transfer moves the duty, a pending one removes it.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_duty_follows_the_confirmed_transfer_and_a_pending_one_refuses_every_area()
    {
        // The transfer tests' factory: the instruction is an ordinary schedule command, and /api/schedules — command token
        // included — only exists when scheduling is activated, which this factory's SchedulingSettings does.
        using var factory = TransferApi.NewFactory();
        await TestData.AddAreaAsync(factory, "AR03", "A");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR03");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, TransferApi.Thai(7, 5), "ShiftIn");

        // Before the instruction the duty is the plan's Area, with its own source word.
        var beforeInstruction = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(origin, beforeInstruction.DutyAreaId);
        Assert.Equal("baseline", beforeInstruction.DutySource);
        Assert.Null(beforeInstruction.Transfer);

        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        // The instruction has taken effect and nobody has arrived: there is no usable duty Area in either Area.
        factory.Clock.SetUtcNow(TransferApi.Thai(13, 1));
        var pending = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Null(pending.DutyAreaId);
        Assert.Equal("transfer-pending", pending.DutySource);
        Assert.Equal("instructed", pending.Transfer!.State);
        Assert.Null(pending.Transfer.ConfirmedAt);
        Assert.Equal(origin, pending.Transfer.FromAreaId);
        Assert.Equal(destination, pending.Transfer.ToAreaId);

        // Both Areas refuse an actual action with the pending-transfer conflict itself — not a window or ordering refusal —
        // and neither leaves a fact behind: an instruction alone is never entry evidence at B.
        var atOriginWhilePending = await EffectiveAttendanceApi.RecordAsync(cleaner, "BreakOut", "token-checkin-ar01");
        var atDestinationWhilePending = await EffectiveAttendanceApi.RecordAsync(cleaner, "BreakOut", "token-checkin-ar03");
        Assert.Equal(HttpStatusCode.Conflict, atOriginWhilePending.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, atDestinationWhilePending.StatusCode);
        Assert.Equal("transfer-pending",
            (await atOriginWhilePending.Content.ReadFromJsonAsync<RefusedAnswer>())!.Code);
        Assert.Equal("transfer-pending",
            (await atDestinationWhilePending.Content.ReadFromJsonAsync<RefusedAnswer>())!.Code);
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));

        // The confirmation is the only path to the destination, and afterwards A is refused while B is the duty.
        var confirmedAt = TransferApi.Thai(13, 5);
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: confirmedAt);
        factory.Clock.SetUtcNow(TransferApi.Thai(13, 6));
        var confirmed = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(destination, confirmed.DutyAreaId);
        Assert.Equal("transfer", confirmed.DutySource);
        Assert.Equal("confirmed", confirmed.Transfer!.State);
        Assert.Equal(confirmedAt.UtcDateTime, confirmed.Transfer.ConfirmedAt);

        var refusedAtOrigin = await EffectiveAttendanceApi.RecordAsync(cleaner, "BreakOut", "token-checkin-ar01");
        var acceptedAtDestination = await EffectiveAttendanceApi.RecordAsync(cleaner, "BreakOut", "token-checkin-ar03");
        Assert.Equal(HttpStatusCode.Forbidden, refusedAtOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, acceptedAtDestination.StatusCode);

        // ADR facility-0203: the stored facts keep the origin Area and the original minute on the one Shift-In and never add a
        // second one; only the new action is the destination's, and the arrival itself is its own row, not an attendance one.
        var facts = await EffectiveAttendanceApi.FactsAsync(factory);
        Assert.Equal(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut }, facts.Select(fact => fact.EventType));
        Assert.Single(facts, fact => fact.EventType == AttendanceEvent.ShiftIn);
        Assert.Equal(origin, facts[0].AreaId);
        Assert.Equal(TransferApi.Thai(7, 5).UtcDateTime, facts[0].OccurredAt);
        Assert.Equal(destination, facts[1].AreaId);
        var arrival = Assert.Single(await TransferApi.ConfirmationsAsync(factory));
        Assert.Equal(transferId, arrival.TransferId);
        Assert.Equal(destination, arrival.AreaId);
        Assert.Equal(confirmedAt.UtcDateTime, arrival.OccurredAt);
    }

    /// <summary>A pending transfer refuses a work submission too: a Service Point submission is never arrival evidence.</summary>
    [Fact]
    public async Task A_pending_transfer_refuses_a_work_submission_in_either_area()
    {
        using var factory = TransferApi.NewFactory();
        await TestData.AddAreaAsync(factory, "AR03", "A");
        // Both points are this test's own: the seeded fixture already owns AR01-01 and token-restroom-m1, and both the sign
        // code and the QR token are unique across every sign.
        var originPoint = await TestData.AddPointAsync(factory, "AR01", "AR01-91", "token-pending-origin-point");
        var destinationPoint = await TestData.AddPointAsync(factory, "AR03", "AR03-91", "token-pending-destination-point");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01");
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR03");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        // Spec §9: with the instruction in force and unconfirmed, a submission is refused in the source Area and in the
        // destination alike, and neither writes anything — a Service Point submission is work, never arrival evidence.
        factory.Clock.SetUtcNow(TransferApi.Thai(13, 1));
        var atOrigin = await SubmitAsync(cleaner, "token-pending-origin-point");
        var atDestination = await SubmitAsync(cleaner, "token-pending-destination-point");

        Assert.Equal(HttpStatusCode.Conflict, atOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, atDestination.StatusCode);
        // Both refusals are the transfer's own message, and they wrote nothing at all — no work record and no Blocked Scan:
        // the destination is refused as "not confirmed yet", never as another Area's sign.
        Assert.Equal(AttendanceEndpoints.TransferPendingMessage,
            (await atOrigin.Content.ReadFromJsonAsync<MessageModel>())!.Message);
        Assert.Equal(AttendanceEndpoints.TransferPendingMessage,
            (await atDestination.Content.ReadFromJsonAsync<MessageModel>())!.Message);
        Assert.Empty(await AdminSetupApi.ReadAsync(factory, db => db.ScanRecords.AsNoTracking().ToListAsync()));
        Assert.Empty(await EffectiveAttendanceApi.BlockedAsync(factory));

        // Only the authorised confirmation moves the work: afterwards the destination point accepts a submission, while the
        // source point is refused as another Area's sign and contributes no Scan Record either.
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));
        factory.Clock.SetUtcNow(TransferApi.Thai(13, 10));
        var refusedAtOrigin = await SubmitAsync(cleaner, "token-pending-origin-point");
        var acceptedAtDestination = await SubmitAsync(cleaner, "token-pending-destination-point");

        Assert.Equal(HttpStatusCode.Forbidden, refusedAtOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.Created, acceptedAtDestination.StatusCode);
        var stored = Assert.Single(await AdminSetupApi.ReadAsync(factory, db => db.ScanRecords.AsNoTracking().ToListAsync()));
        Assert.Equal(destinationPoint, stored.ServicePointId);
        Assert.NotEqual(originPoint, stored.ServicePointId);

        static Task<HttpResponseMessage> SubmitAsync(HttpClient client, string token) =>
            client.PostAsJsonAsync("/api/scan-records", new
            {
                qrToken = token,
                status = "Normal",
                latitude = TestGps.NearLatitude,
                longitude = TestGps.NearLongitude,
                accuracyM = 10,
            });
    }

    [Fact]
    public async Task A_repeat_numbered_action_replays_as_already_recorded_while_a_new_stale_action_is_refused()
    {
        using var factory = new JanitorialApiFactory();
        var baselineStart = new DateOnly(2026, 10, 1);
        await ScheduleWriteApi.AddWeekdayProfileAsync(factory, "1000001", baselineStart);
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", baselineStart, "AR01", breakGroup: 1);
        await EffectiveAttendanceApi.AddLifecycleAsync(factory, Day8, Shift.Day, AttendanceLifecycleVersion.Numbered6);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 5));
        var initial = await EffectiveAttendanceApi.StateAsync(cleaner);

        var first = await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, EffectiveAttendanceApi.Thai(8, 7, 5), "ShiftIn",
            lifecycleVersion: initial.LifecycleVersion, stateRevision: initial.StateRevision);
        Assert.Equal("Recorded", first.Outcome);

        // Replaying the same Numbered6 action with the pre-write revision answers AlreadyRecorded idempotently.
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 7, 6));
        var replay = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftIn",
            lifecycleVersion: initial.LifecycleVersion, stateRevision: initial.StateRevision);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = await replay.Content.ReadFromJsonAsync<AttendanceStateAnswer>();
        Assert.NotNull(replayed);
        Assert.Equal("AlreadyRecorded", replayed.Outcome);

        // A *new* action carrying that same stale revision is still refused with 409 refresh-required.
        var staleNew = await EffectiveAttendanceApi.RecordAsync(cleaner, "Break1Out",
            lifecycleVersion: initial.LifecycleVersion, stateRevision: initial.StateRevision);
        Assert.Equal(HttpStatusCode.Conflict, staleNew.StatusCode);
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
    }
}

/// <summary>The attendance calls and fixtures the Task 9 test files share: a slot lifecycle, a leave interval, and the state answer.</summary>
internal static class EffectiveAttendanceApi
{
    public static readonly DateOnly Day8 = new(2026, 10, 8);

    /// <summary>Asia/Bangkok wall-clock on 8 October 2026, when every Task 9 attendance test happens.</summary>
    public static DateTimeOffset Thai(int day, int hour, int minute) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(7));

    public static Task<HttpResponseMessage> RecordAsync(
        HttpClient client,
        string eventType,
        string qrToken = AttendanceApi.Area1CheckIn,
        string? lifecycleVersion = null,
        string? stateRevision = null,
        IReadOnlyList<string>? confirmGaps = null) =>
        client.PostAsJsonAsync("/api/attendance", new
        {
            qrToken,
            eventType,
            latitude = TestGps.NearLatitude,
            longitude = TestGps.NearLongitude,
            accuracyM = 10,
            lifecycleVersion,
            stateRevision,
            confirmGaps,
        });

    public static async Task<AttendanceStateAnswer> StateAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<AttendanceStateAnswer>("/api/attendance/me");
        Assert.NotNull(response);
        return response!;
    }

    /// <summary>Moves the clock to <paramref name="at"/> and records the action, asserting it was accepted.</summary>
    public static async Task<AttendanceStateAnswer> RecordAndReadAsync(
        JanitorialApiFactory factory,
        HttpClient client,
        DateTimeOffset at,
        string eventType,
        string qrToken = AttendanceApi.Area1CheckIn,
        string? lifecycleVersion = null,
        string? stateRevision = null,
        IReadOnlyList<string>? confirmGaps = null)
    {
        factory.Clock.SetUtcNow(at);
        var response = await RecordAsync(client, eventType, qrToken, lifecycleVersion, stateRevision, confirmGaps);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return (await response.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
    }

    /// <summary>An explicit lifecycle row for one dated slot: the slot the operator chose for the numbered contract.</summary>
    public static Task AddLifecycleAsync(
        JanitorialApiFactory factory, DateOnly shiftDate, Shift shift, AttendanceLifecycleVersion version) =>
        factory.WithDbAsync(async db =>
        {
            db.AttendanceSlotLifecycles.Add(new AttendanceSlotLifecycle
            {
                ShiftDate = shiftDate,
                Shift = shift,
                Version = version,
                ActivatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    /// <summary>An agreed leave interval of one slot, as the leave command stores it: UTC, half-open.</summary>
    public static Task AddLeaveAsync(
        JanitorialApiFactory factory, string employeeId, DateOnly shiftDate, Shift shift, DateTimeOffset from,
        DateTimeOffset to) =>
        factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(user => user.EmployeeId == employeeId);
            var admin = await db.Users.SingleAsync(user => user.Username == "admin");
            db.ScheduleExceptions.Add(new ScheduleException
            {
                UserId = person.Id,
                Kind = ScheduleExceptionKind.Leave,
                ShiftDate = shiftDate,
                Shift = shift,
                StartsAt = from.UtcDateTime,
                EndsAt = to.UtcDateTime,
                Revision = 1,
                CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    public static Task<List<Domain.Entities.ShiftAttendance>> FactsAsync(JanitorialApiFactory factory) =>
        AdminSetupApi.ReadAsync(factory, db => db.ShiftAttendances.AsNoTracking().OrderBy(row => row.Id).ToListAsync());

    public static Task<List<AttendanceGap>> GapsAsync(JanitorialApiFactory factory) =>
        AdminSetupApi.ReadAsync(factory, db => db.AttendanceGaps.AsNoTracking().OrderBy(row => row.Id).ToListAsync());

    public static Task<List<Domain.Entities.BlockedScan>> BlockedAsync(JanitorialApiFactory factory) =>
        AdminSetupApi.ReadAsync(factory, db => db.BlockedScans.AsNoTracking().ToListAsync());
}

/// <summary>One action of the read, as the page receives it.</summary>
public record ActionAnswer(string EventType, string State, DateTime? OccurredAt, string? Source, DateTime? ExpectedAt);

public record GapAnswer(string EventType, string Status, string? Reason, DateTime? RecordedAt);

public record ChoiceAnswer(string Action, List<string> RequiredGaps);

/// <summary>The whole attendance answer, including the members Task 9 and Task 10 added (absent from an older answer).</summary>
public record AttendanceStateAnswer(
    DateOnly? ShiftDate,
    string? Shift,
    List<AttendanceEntryModel> Events,
    List<string> NextEvents,
    string? Outcome,
    string? LifecycleVersion = null,
    string? StateRevision = null,
    List<ActionAnswer>? Actions = null,
    List<GapAnswer>? Gaps = null,
    List<ChoiceAnswer>? NextActions = null,
    int? DutyAreaId = null,
    int? DutyBreakGroup = null,
    string? DutySource = null,
    bool IsDayOff = false,
    bool IsLeave = false,
    AttendanceTransferAnswer? Transfer = null,
    List<AttendanceTransferAnswer>? TransferHistory = null,
    List<string>? FillableActions = null,
    bool DepartureLeaveMismatch = false)
{
    public ActionAnswer Action(string eventType) =>
        Actions!.Single(action => action.EventType == eventType);
}

/// <summary>A refused write: the machine word plus the old list of actions that are allowed now.</summary>
public record RefusedAnswer(string Message, List<string> NextEvents, string? Code = null, List<string>? RequiredGaps = null);

/// <summary>A stale-revision refusal, which carries the state the caller has to re-read.</summary>
public record StateConflictAnswer(string Message, string Code, AttendanceStateAnswer State);
