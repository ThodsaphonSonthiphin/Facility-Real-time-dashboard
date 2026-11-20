using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Attendance;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §9 and ADRs facility-0203, 0204, 0212 and 0213: a Supervisor records one authorised Area move for a person who has
/// already entered work, and that person confirms arrival once at the destination sign. The instruction is a plan time, not a
/// departure scan; the confirmation is an actual fact of its own — never a second Shift-In, never a Break-In, never a
/// Break-Out — so the initial Shift-In, the break progress and every earlier Area's facts stay exactly as they were.
/// <para>
/// Every scenario goes through HTTP because that is where the contract lives: the command envelope for instruction and
/// cancellation, and <c>POST /api/area-transfers/{id}/confirm</c> for the arrival.
/// </para>
/// </summary>
public class AreaTransferTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // The happy path: an instruction at 13:00, an arrival at 13:05, and nothing about the shift rewritten.
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transfer_receipt_requires_the_original_scope_on_lookup_and_replay(bool cancel)
    {
        using var factory = TransferApi.NewFactory();
        var originBuilding = await AdminSetupApi.BuildingIdAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var destination = await TestData.AddAreaAsync(factory, "AR03", "B");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new(2026, 10, 1));
        var (owner, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var at = TransferApi.Thai(13, 0);
        var expected = new Dictionary<string, string>();
        object payload = new { userId = auth.User.Id, fromAreaId = origin, toAreaId = destination,
            shiftDate = "2026-10-08", shift = "Day", effectiveAt = at, reason = "test move" };
        if (cancel)
        {
            var transferId = await TransferApi.InstructForIdAsync(factory, owner, auth.User.Id, origin, destination, at);
            expected[TransferApi.Key(auth.User.Id)] = "1";
            payload = new { transferId, reason = "test cancellation" };
        }
        var token = (await ScheduleWriteApi.IssueAsync(owner))!;
        var envelope = new { token = token.Token, commandId = token.CommandId, expectedRevisions = expected,
            kind = cancel ? TransferApi.Cancel : TransferApi.Instruct, payload };
        var status = $"/api/schedules/commands/{token.CommandId}";
        var committed = await owner.PostAsJsonAsync("/api/schedules/commands", envelope);
        Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var body = await committed.Content.ReadAsStringAsync();
        Assert.Equal(body, await owner.GetStringAsync(status));
        Assert.Equal(body, await (await owner.PostAsJsonAsync("/api/schedules/commands", envelope)).Content.ReadAsStringAsync());

        // A new building keeps the same account active as a Supervisor after its origin responsibility ends.
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new(2026, 10, 1));
        await factory.WithDbAsync(db => db.SupervisorScopePeriods.Where(row => row.BuildingId == originBuilding)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.EffectiveUntil, new DateOnly(2026, 10, 8))));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(status)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/schedules/commands", envelope)).StatusCode);

        // Old transfer receipts did not store the checked origin. Restoring the origin cannot recover that proof.
        await factory.WithDbAsync(async db =>
        {
            await db.SupervisorScopePeriods.Where(row => row.BuildingId == originBuilding)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.EffectiveUntil, (DateOnly?)null));
            await db.ScheduleCommandReceipts.Where(row => row.CommandId == token.CommandId)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.ScopeJson, "[]"));
        });
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(status)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/schedules/commands", envelope)).StatusCode);
        Assert.Equal(cancel, Assert.Single(await TransferApi.InstructionsAsync(factory)).IsCancelled);
    }

    [Fact]
    public async Task An_instruction_effective_after_final_out_cannot_create_arrival_evidence()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var id = await TransferApi.InstructForIdAsync(factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await EffectiveAttendanceApi.RecordAndReadAsync(factory, cleaner, TransferApi.Thai(12, 59), "ShiftOut");

        factory.Clock.SetUtcNow(TransferApi.Thai(13, 0));
        var check = await cleaner.GetFromJsonAsync<SignTransferCheck>(
            $"/api/attendance/signs/{TransferApi.Destination}?transferId={id}");
        Assert.False(check!.CanConfirmTransfer);
        var refused = await TransferApi.ConfirmAsync(factory, cleaner, id, "1");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Empty(await TransferApi.ConfirmationsAsync(factory));
        var facts = await EffectiveAttendanceApi.FactsAsync(factory);
        Assert.All(facts, fact => Assert.Equal(origin, fact.AreaId));
        Assert.Equal(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut }, facts.Select(fact => fact.EventType));
    }

    [Fact]
    public async Task An_instruction_and_a_destination_confirmation_keep_the_initial_check_in_and_the_origin_area()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.LocateAsync(factory);
        var shiftIn = TransferApi.Thai(7, 5);
        await TransferApi.CheckInAsync(factory, cleaner, shiftIn);

        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        var confirmed = await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var answer = await TransferApi.ReadConfirmAsync(confirmed);
        Assert.Equal(transferId, answer.Id);
        Assert.Equal("confirmed", answer.State);
        Assert.Equal(TransferApi.Thai(13, 5).UtcDateTime, answer.ConfirmedAt);
        Assert.Equal("1", answer.Revision);

        // The instruction is a plan time, not a measured departure: the row keeps its own 13:00 effective time.
        var instruction = Assert.Single(await TransferApi.InstructionsAsync(factory));
        Assert.Equal(origin, instruction.FromAreaId);
        Assert.Equal(destination, instruction.ToAreaId);
        Assert.Equal(TransferApi.Thai(13, 0).UtcDateTime, instruction.EffectiveAt);
        Assert.False(instruction.IsCancelled);
        Assert.Equal(1, instruction.Revision);

        // One confirmation, at the destination Area's own sign, with the person who arrived and server time.
        var confirmation = Assert.Single(await TransferApi.ConfirmationsAsync(factory));
        Assert.Equal(transferId, confirmation.TransferId);
        Assert.Equal(destination, confirmation.AreaId);
        Assert.Equal(auth.User.Id, confirmation.ConfirmedById);
        Assert.Equal(TransferApi.Thai(13, 5).UtcDateTime, confirmation.OccurredAt);
        Assert.Equal(AttendanceSource.Scan, confirmation.Source);
        Assert.True(confirmation.WithinRadius);

        // The original actual fact is untouched: the same Shift-In, at the same minute, in the same Area.
        var fact = Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Equal(AttendanceEvent.ShiftIn, fact.EventType);
        Assert.Equal(origin, fact.AreaId);
        Assert.Equal(shiftIn.UtcDateTime, fact.OccurredAt);

        // And the read now says the destination is the duty, from the transfer rather than from the account Area.
        var state = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(destination, state.DutyAreaId);
        Assert.Equal("transfer", state.DutySource);
        Assert.NotNull(state.Transfer);
        Assert.Equal(transferId, state.Transfer!.Id);
        Assert.Equal("confirmed", state.Transfer.State);
        Assert.Equal("1", state.Transfer.Revision);
        Assert.Equal(origin, state.Transfer.FromAreaId);
    }

    [Fact]
    public async Task Only_the_current_pending_transfer_at_its_destination_sign_can_be_confirmed()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.LocateAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        var destinationPath = $"/api/attendance/signs/{TransferApi.Destination}";
        var matchingPath = $"{destinationPath}?transferId={transferId}";
        factory.Clock.SetUtcNow(TransferApi.Thai(12, 59));
        Assert.False((await cleaner.GetFromJsonAsync<SignTransferCheck>(matchingPath))!.CanConfirmTransfer);

        factory.Clock.SetUtcNow(TransferApi.Thai(13, 5));
        Assert.Equal(HttpStatusCode.NoContent, (await cleaner.GetAsync(destinationPath)).StatusCode);
        Assert.True((await cleaner.GetFromJsonAsync<SignTransferCheck>(matchingPath))!.CanConfirmTransfer);
        Assert.False((await cleaner.GetFromJsonAsync<SignTransferCheck>(
            $"/api/attendance/signs/{TransferApi.Origin}?transferId={transferId}"))!.CanConfirmTransfer);
        Assert.False((await cleaner.GetFromJsonAsync<SignTransferCheck>(
            $"{destinationPath}?transferId={transferId + 1}"))!.CanConfirmTransfer);

        var confirmed = await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.False((await cleaner.GetFromJsonAsync<SignTransferCheck>(matchingPath))!.CanConfirmTransfer);
    }

    private sealed record SignTransferCheck(bool CanConfirmTransfer);

    [Fact]
    public async Task The_final_shift_out_happens_at_the_destination_without_a_second_shift_in_or_out()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));

        factory.Clock.SetUtcNow(TransferApi.Thai(19, 5));
        var shiftOut = await EffectiveAttendanceApi.RecordAsync(cleaner, "ShiftOut", TransferApi.Destination);
        Assert.Equal(HttpStatusCode.OK, shiftOut.StatusCode);

        var replay = await TransferApi.ReadConfirmAsync(await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1"));
        Assert.Equal(TransferApi.Thai(13, 5), replay.ConfirmedAt);
        var facts = await EffectiveAttendanceApi.FactsAsync(factory);
        Assert.Equal(new[] { AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut }, facts.Select(fact => fact.EventType));
        Assert.Equal(origin, facts[0].AreaId);
        // ADR facility-0203: the shift ends where responsibility has moved to, and it is still one shift.
        Assert.Equal(destination, facts[1].AreaId);
        Assert.Single(facts, fact => fact.EventType == AttendanceEvent.ShiftIn);
        Assert.Single(facts, fact => fact.EventType == AttendanceEvent.ShiftOut);
        Assert.Equal(4, (await EffectiveAttendanceApi.GapsAsync(factory)).Count);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Who may confirm, where, and when.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_sign_of_another_area_is_not_the_destination()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        var refused = await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1",
            token: "token-checkin-ar02", at: TransferApi.Thai(13, 5));
        var unknown = await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1",
            token: "token-that-does-not-exist", at: TransferApi.Thai(13, 5));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Empty(await TransferApi.ConfirmationsAsync(factory));
        Assert.Equal(1, Assert.Single(await TransferApi.InstructionsAsync(factory)).Revision);
    }

    [Fact]
    public async Task Another_person_cannot_confirm_the_arrival()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        // The other person logs in while the clock is still real time: a token issued against a test-dated clock would be
        // judged expired by the authentication middleware.
        var (other, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000003", "0810000003");
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        var refused = await TransferApi.ConfirmAsync(factory, other, transferId, "1", at: TransferApi.Thai(13, 5));

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Empty(await TransferApi.ConfirmationsAsync(factory));
    }

    [Fact]
    public async Task A_confirmation_before_the_effective_time_is_refused()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        // 12:55 is before the instruction takes effect, so the destination is not usable yet (spec §9).
        var refused = await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(12, 55));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Empty(await TransferApi.ConfirmationsAsync(factory));
        var state = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(origin, state.DutyAreaId);
        Assert.Equal("instructed", state.Transfer!.State);
    }

    [Fact]
    public async Task A_confirmation_outside_the_slot_window_is_refused()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        // 20:30 is past the Day shift's receiving window, so the person is not in this slot any more.
        var refused = await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(20, 30));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Empty(await TransferApi.ConfirmationsAsync(factory));
    }

    [Fact]
    public async Task A_confirmation_without_the_instruction_revision_is_refused()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        // Spec §9 binds the confirmation to the instruction's current revision ("ผูก transferId/current revision"), so a
        // client that never read the instruction cannot turn it into arrival evidence.
        var refused = await TransferApi.ConfirmAsync(factory, cleaner, transferId, expectedRevision: null,
            at: TransferApi.Thai(13, 5));

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Empty(await TransferApi.ConfirmationsAsync(factory));
        Assert.Equal(1, Assert.Single(await TransferApi.InstructionsAsync(factory)).Revision);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The existing GPS policy, unchanged: a missing location is refused, a coarse one is flagged but accepted.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_confirmation_without_a_location_is_refused()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        var refused = await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1",
            at: TransferApi.Thai(13, 5), latitude: null, longitude: null, accuracyM: null);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Empty(await TransferApi.ConfirmationsAsync(factory));
    }

    [Fact]
    public async Task An_out_of_radius_confirmation_is_flagged_but_accepted()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.LocateAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        var accepted = await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1",
            at: TransferApi.Thai(13, 5), latitude: TestGps.FarLatitude, longitude: TestGps.FarLongitude);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var stored = Assert.Single(await TransferApi.ConfirmationsAsync(factory));
        Assert.False(stored.WithinRadius);
        Assert.NotNull(stored.DistanceM);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Idempotence and the break cycle.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_repeated_confirmation_returns_the_same_fact_and_leaves_one_row()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        var first = await TransferApi.ReadConfirmAsync(
            await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5)));

        // The repeat is idempotent by the instruction's own identity: a later clock, another sign and no location at all
        // still answer the stored fact instead of writing a second arrival or refusing on a stale revision.
        var repeat = await TransferApi.ConfirmAsync(factory, cleaner, transferId, "99",
            token: "token-checkin-ar02", at: TransferApi.Thai(14, 30), latitude: null, longitude: null, accuracyM: null);

        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var again = await TransferApi.ReadConfirmAsync(repeat);
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.ConfirmedAt, again.ConfirmedAt);
        Assert.Equal(first.Revision, again.Revision);
        Assert.Equal(first.State, again.State);
        Assert.Single(await TransferApi.ConfirmationsAsync(factory));
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
    }

    [Fact]
    public async Task An_ongoing_break_is_unchanged_by_the_arrival()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        factory.Clock.SetUtcNow(TransferApi.Thai(10, 5));
        Assert.Equal(HttpStatusCode.OK, (await EffectiveAttendanceApi.RecordAsync(cleaner, "Break1Out")).StatusCode);
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));

        // ADR facility-0212: the arrival is not a Break-In, so the break the person is in stays open on the old facts and
        // the next actions are still the return from break and the end of the shift.
        var state = await EffectiveAttendanceApi.StateAsync(cleaner);
        var breakOut = state.Action("Break1Out");
        Assert.Equal("recorded", breakOut.State);
        Assert.Equal(TransferApi.Thai(10, 5).UtcDateTime, breakOut.OccurredAt!.Value);
        Assert.DoesNotContain(state.Events,
            entry => entry.EventType is "Break1In" or "ShiftOut");
        Assert.Contains("Break1In", state.NextEvents);
        var facts = await EffectiveAttendanceApi.FactsAsync(factory);
        Assert.Equal(origin, Assert.Single(facts, fact => fact.EventType == AttendanceEvent.Break1Out).AreaId);
        // The break's own plan and progress are untouched: no return-from-break was fabricated by the arrival.
        Assert.Equal(destination, state.DutyAreaId);
    }

    [Fact]
    public async Task An_arrival_keeps_the_planned_break_group_and_creates_no_break_action()
    {
        using var factory = TransferApi.NewFactory();
        await TestData.AddAreaAsync(factory, "AR03", "A");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01", breakGroup: 1);
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR03");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));

        // The plan's first-break group is the origin's group, and the transfer must not rewrite it: ADR facility-0212 keeps
        // the retained plan and its progress, and a group change may never create a past action.
        var before = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(1, before.DutyBreakGroup);

        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));

        var after = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(destination, after.DutyAreaId);
        Assert.Equal(1, after.DutyBreakGroup);
        // The arrival is not a break action of any kind: only the original Shift-In exists.
        var facts = await EffectiveAttendanceApi.FactsAsync(factory);
        Assert.Equal(AttendanceEvent.ShiftIn, Assert.Single(facts).EventType);
    }

    [Fact]
    public async Task Confirmation_does_not_enter_the_repeat_ignore_clock()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));

        // One minute after the arrival the next actual action is still accepted: the confirmation is not an attendance
        // event and never makes a correct press look like a repeat (spec §9).
        factory.Clock.SetUtcNow(TransferApi.Thai(13, 6));
        var next = await EffectiveAttendanceApi.RecordAsync(cleaner, "Break1Out", TransferApi.Destination);

        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        var state = (await next.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
        Assert.Equal("Recorded", state.Outcome);
        Assert.Equal(2, (await EffectiveAttendanceApi.FactsAsync(factory)).Count);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Responsibility: pending until confirmed, then the destination and never both.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_pending_instruction_blocks_both_areas_until_the_arrival_is_confirmed()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        // Past the effective time with no confirmation: neither Area may record an actual action, and nothing is written.
        factory.Clock.SetUtcNow(TransferApi.Thai(13, 1));
        var atOrigin = await EffectiveAttendanceApi.RecordAsync(cleaner, "Break1Out", TransferApi.Origin);
        var atDestination = await EffectiveAttendanceApi.RecordAsync(cleaner, "Break1Out", TransferApi.Destination);

        Assert.Equal(HttpStatusCode.Conflict, atOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, atDestination.StatusCode);
        var refusal = (await atOrigin.Content.ReadFromJsonAsync<RefusedAnswer>())!;
        Assert.Equal("transfer-pending", refusal.Code);
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
        Assert.Empty(await EffectiveAttendanceApi.BlockedAsync(factory));
        Assert.Empty(await EffectiveAttendanceApi.GapsAsync(factory));

        // The read says the same thing: the instruction is pending and there is no duty Area to act in.
        var pending = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Null(pending.DutyAreaId);
        Assert.Equal("transfer-pending", pending.DutySource);
        Assert.Equal("instructed", pending.Transfer!.State);

        // The destination becomes the duty only through the authorised confirmation.
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));
        factory.Clock.SetUtcNow(TransferApi.Thai(13, 6));
        Assert.Equal(HttpStatusCode.OK,
            (await EffectiveAttendanceApi.RecordAsync(cleaner, "Break1Out", TransferApi.Destination)).StatusCode);
    }

    [Fact]
    public async Task After_confirmation_the_destination_is_the_duty_and_the_origin_is_refused()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));

        factory.Clock.SetUtcNow(TransferApi.Thai(13, 6));
        var refused = await EffectiveAttendanceApi.RecordAsync(cleaner, "Break1Out", TransferApi.Origin);

        // Spec §9: no actual action at the source once responsibility has left it. The sign is reported as the Blocked Scan
        // policy always reports another Area's sign, and no actual fact is written.
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("AR01", (await refused.Content.ReadFromJsonAsync<BlockedScanModel>())!.AreaCode);
        Assert.Single(await EffectiveAttendanceApi.FactsAsync(factory));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Cancellation, forward moves and the one-instruction-per-slot rule.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Cancelling_before_arrival_succeeds_and_returns_the_origin_as_the_duty()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        factory.Clock.SetUtcNow(TransferApi.Thai(12, 30));
        var (response, answer) = await TransferApi.CancelAsync(admin, transferId,
            expected: new() { [TransferApi.Key(auth.User.Id)] = "1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer!.Outcome);
        Assert.Equal("2", answer.Revisions[TransferApi.Key(auth.User.Id)]);

        // The row is kept, marked cancelled, with its reason and the actor who cancelled it.
        var instruction = Assert.Single(await TransferApi.InstructionsAsync(factory));
        Assert.True(instruction.IsCancelled);
        Assert.Equal(TransferApi.Thai(12, 30).UtcDateTime, instruction.CancelledAt);
        Assert.Equal(adminAuth.User.Id, instruction.CancelledById);
        Assert.Equal("ยกเลิกคำสั่งย้าย", instruction.CancellationReason);
        Assert.Equal(2, instruction.Revision);

        var state = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(origin, state.DutyAreaId);
        Assert.Equal("cancelled", state.Transfer!.State);

        // A cancelled instruction can never be confirmed, and a second cancellation is refused.
        factory.Clock.SetUtcNow(TransferApi.Thai(13, 5));
        Assert.Equal(HttpStatusCode.Conflict,
            (await TransferApi.ConfirmAsync(factory, cleaner, transferId, "2")).StatusCode);
        var second = await TransferApi.CancelAsync(admin, transferId);
        Assert.Equal(HttpStatusCode.Conflict, second.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Rejected, second.Answer!.Outcome);
    }

    [Fact]
    public async Task Cancelling_after_a_confirmed_arrival_is_refused()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));

        factory.Clock.SetUtcNow(TransferApi.Thai(13, 30));
        var refused = await TransferApi.CancelAsync(admin, transferId);

        Assert.Equal(HttpStatusCode.Conflict, refused.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Rejected, refused.Answer!.Outcome);
        var instruction = Assert.Single(await TransferApi.InstructionsAsync(factory));
        Assert.False(instruction.IsCancelled);
        Assert.Single(await TransferApi.ConfirmationsAsync(factory));
    }

    [Fact]
    public async Task A_second_live_instruction_is_a_conflict()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var other = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        await TransferApi.InstructForIdAsync(factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        factory.Clock.SetUtcNow(TransferApi.Thai(12, 40));
        var (response, answer) = await TransferApi.InstructAsync(factory, admin, auth.User.Id, origin, other,
            TransferApi.Thai(14, 0), expected: new() { [TransferApi.Key(auth.User.Id)] = "1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Rejected, answer!.Outcome);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Problems).Code);
        var instruction = Assert.Single(await TransferApi.InstructionsAsync(factory));
        Assert.Equal(destination, instruction.ToAreaId);
    }

    [Fact]
    public async Task A_confirmed_instruction_allows_a_forward_move_from_the_destination()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var forward = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var first = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await TransferApi.ConfirmAsync(factory, cleaner, first, "1", at: TransferApi.Thai(13, 5));

        // ADR facility-0213: the confirmed arrival stays evidence, and the next change is a new forward record whose origin
        // is the Area responsibility has actually moved to.
        factory.Clock.SetUtcNow(TransferApi.Thai(15, 0));
        var (response, answer) = await TransferApi.InstructAsync(factory, admin, auth.User.Id, destination, forward,
            TransferApi.Thai(16, 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer!.Outcome);
        var instructions = await TransferApi.InstructionsAsync(factory);
        Assert.Equal(2, instructions.Count);
        Assert.Equal(destination, instructions[1].FromAreaId);
        Assert.Equal(forward, instructions[1].ToAreaId);
        Assert.Single(await TransferApi.ConfirmationsAsync(factory));

        factory.Clock.SetUtcNow(TransferApi.Thai(16, 1));
        var manager = await TeamAttendanceApi.StateAsync(admin, auth.User.Id);
        Assert.Equal(HttpStatusCode.OK, manager.StatusCode);
        var view = (await manager.Content.ReadFromJsonAsync<AttendanceStateAnswer>())!;
        Assert.Equal(instructions[1].Id, view.Transfer!.Id);
        Assert.Equal("instructed", view.Transfer.State);
        Assert.Empty(view.FillableActions!);
        var previous = Assert.Single(view.TransferHistory!);
        Assert.Equal(first, previous.Id);
        Assert.Equal("confirmed", previous.State);
        Assert.Equal(TransferApi.Thai(13, 5).UtcDateTime, previous.ConfirmedAt);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The chain after a confirmed arrival: a forward instruction builds on the destination, it does not revert the plan.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_forward_move_builds_on_the_confirmed_destination_until_it_takes_effect_or_is_cancelled()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var forward = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var first = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        await TransferApi.ConfirmAsync(factory, cleaner, first, "1", at: TransferApi.Thai(13, 5));

        // ADR facility-0213: the next change is a new forward record whose origin is the destination responsibility has
        // already moved to.
        var second = await TransferApi.InstructForIdAsync(
            factory, admin, auth.User.Id, destination, forward, TransferApi.Thai(16, 0));

        // Before the forward instruction's effective time the person is still where the confirmed arrival put them. Reading
        // only the newest row would answer the plan's original Area here — a person shown in an Area they already left, whose
        // sign would then accept work they are no longer responsible for.
        factory.Clock.SetUtcNow(TransferApi.Thai(15, 56));
        var beforeEffective = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(destination, beforeEffective.DutyAreaId);
        Assert.Equal("transfer", beforeEffective.DutySource);
        Assert.Equal(second, beforeEffective.Transfer!.Id);
        Assert.Equal("instructed", beforeEffective.Transfer.State);

        // Once it takes effect with no confirmation, no Area may record an actual action at all.
        factory.Clock.SetUtcNow(TransferApi.Thai(16, 1));
        var pending = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Null(pending.DutyAreaId);
        Assert.Equal("transfer-pending", pending.DutySource);

        // Cancelling the forward move returns responsibility to the destination that was actually reached, never to the plan.
        factory.Clock.SetUtcNow(TransferApi.Thai(16, 2));
        var cancelled = await TransferApi.CancelAsync(admin, second,
            expected: new() { [TransferApi.Key(auth.User.Id)] = "1" });
        Assert.Equal(HttpStatusCode.OK, cancelled.Response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, cancelled.Answer!.Outcome);

        factory.Clock.SetUtcNow(TransferApi.Thai(16, 3));
        var afterCancel = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(destination, afterCancel.DutyAreaId);
        Assert.Equal("transfer", afterCancel.DutySource);
        Assert.Equal("cancelled", afterCancel.Transfer!.State);
        Assert.Equal(2, (await TransferApi.InstructionsAsync(factory)).Count);
        Assert.Single(await TransferApi.ConfirmationsAsync(factory));
    }

    [Fact]
    public async Task An_instruction_after_a_cancellation_can_echo_the_revision_the_screen_read()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));

        factory.Clock.SetUtcNow(TransferApi.Thai(12, 55));
        var (_, created) = await TransferApi.InstructAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));
        var first = (await TransferApi.InstructionsAsync(factory))[^1];
        factory.Clock.SetUtcNow(TransferApi.Thai(12, 58));
        var cancelled = await TransferApi.CancelAsync(admin, first.Id,
            expected: new() { [TransferApi.Key(auth.User.Id)] = "1" });
        Assert.Equal(ScheduleCommandOutcome.Committed, cancelled.Answer!.Outcome);

        // The read reports the cancelled row and its new revision; a client that echoes exactly what it read must be able to
        // instruct again, rather than being told the slot is stale against a "0" it never saw.
        var state = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal("cancelled", state.Transfer!.State);
        Assert.Equal("2", state.Transfer.Revision);

        factory.Clock.SetUtcNow(TransferApi.Thai(14, 0));
        var (response, answer) = await TransferApi.InstructAsync(
            factory, admin, auth.User.Id, origin, destination, TransferApi.Thai(15, 0),
            expected: new() { [TransferApi.Key(auth.User.Id)] = state.Transfer.Revision });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer!.Outcome);
        Assert.Equal("1", answer.Revisions[TransferApi.Key(auth.User.Id)]);
        Assert.Equal(2, (await TransferApi.InstructionsAsync(factory)).Count);
    }

    [Fact]
    public async Task An_instruction_for_a_person_who_has_not_checked_in_is_refused()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var person = await AdminSetupApi.UserIdAsync(factory, "1000001");

        factory.Clock.SetUtcNow(TransferApi.Thai(12, 55));
        var (response, answer) = await TransferApi.InstructAsync(
            factory, admin, person, origin, destination, TransferApi.Thai(13, 0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Rejected, answer!.Outcome);
        Assert.Equal(ScheduleCommandErrorCode.RowInvalid, Assert.Single(answer.Problems).Code);
        Assert.Empty(await TransferApi.InstructionsAsync(factory));
    }

    [Fact]
    public async Task A_stale_origin_area_is_refused_instead_of_trusted()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var other = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));

        // The client claims the person is in AR02 while the duty is AR01: the server reads the duty itself.
        factory.Clock.SetUtcNow(TransferApi.Thai(12, 55));
        var (response, answer) = await TransferApi.InstructAsync(
            factory, admin, auth.User.Id, other, destination, TransferApi.Thai(13, 0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Rejected, answer!.Outcome);
        Assert.Empty(await TransferApi.InstructionsAsync(factory));
        // The person's own duty is untouched: the stale client value was refused, not written anywhere.
        var state = await EffectiveAttendanceApi.StateAsync(cleaner);
        Assert.Equal(origin, state.DutyAreaId);
    }

    [Fact]
    public async Task A_stale_instruction_revision_is_refused()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));

        factory.Clock.SetUtcNow(TransferApi.Thai(12, 50));
        var (response, answer) = await TransferApi.InstructAsync(factory, admin, auth.User.Id, origin, destination,
            TransferApi.Thai(13, 0), expected: new() { [TransferApi.Key(auth.User.Id)] = "7" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ScheduleCommandErrorCode.StaleRevision, Assert.Single(answer!.Problems).Code);
        Assert.Equal("0", answer.Revisions[TransferApi.Key(auth.User.Id)]);
        Assert.Empty(await TransferApi.InstructionsAsync(factory));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Authority: the from-Area's building decides for a Supervisor; an Admin has every building.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_in_scope_supervisor_records_the_instruction()
    {
        using var factory = TransferApi.NewFactory();
        await TestData.AddAreaAsync(factory, "AR03", "A");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var destination = await AdminSetupApi.AreaIdAsync(factory, "AR03");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));

        factory.Clock.SetUtcNow(TransferApi.Thai(12, 55));
        var (response, answer) = await TransferApi.InstructAsync(
            factory, supervisor, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer!.Outcome);
        Assert.Equal("1", answer.Revisions[TransferApi.Key(auth.User.Id)]);
    }

    [Fact]
    public async Task An_out_of_scope_supervisor_is_refused()
    {
        using var factory = TransferApi.NewFactory();
        await TestData.AddBuildingAsync(factory, "B");
        // The Supervisor holds building B only, so the origin Area (in building A) is not theirs to move anybody out of.
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 1));
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var (cleaner, auth, _) = await AuthApi.LoggedInAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));

        factory.Clock.SetUtcNow(TransferApi.Thai(12, 55));
        var (response, answer) = await TransferApi.InstructAsync(
            factory, supervisor, auth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Rejected, answer!.Outcome);
        Assert.Empty(await TransferApi.InstructionsAsync(factory));
    }

    [Fact]
    public async Task An_admin_manages_every_building()
    {
        using var factory = TransferApi.NewFactory();
        await TestData.AddBuildingAsync(factory, "B");
        // Origin and destination are both in building B, which no dated Supervisor period covers.
        var destination = await TestData.AddAreaAsync(factory, "BR01", "B");
        var origin = await TestData.AddAreaAsync(factory, "BR02", "B");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var person = await TestData.AddCleanerAsync(factory, "1000009", Shift.Day, areaCode: "BR02", phone: "0810000009");
        var (cleaner, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000009", "0810000009");
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5), "token-checkin-br02");

        factory.Clock.SetUtcNow(TransferApi.Thai(12, 55));
        var (response, answer) = await TransferApi.InstructAsync(
            factory, admin, person, origin, destination, TransferApi.Thai(13, 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer!.Outcome);
    }
}

/// <summary>The transfer calls the Task 10 tests share, on top of the schedule write helpers.</summary>
internal static class TransferApi
{
    public const string Instruct = "instruct-transfer";
    public const string Cancel = "cancel-transfer";

    public const string Origin = "token-checkin-ar01";
    public const string Destination = "token-checkin-ar03";

    /// <summary>Asia/Bangkok wall-clock on 8 October 2026, the slot every transfer test happens in.</summary>
    public static DateTimeOffset Thai(int hour, int minute) => new(2026, 10, 8, hour, minute, 0, TimeSpan.FromHours(7));

    public static JanitorialApiFactory NewFactory() => ScheduleWriteApi.NewFactory();

    /// <summary>The revision key of the slot's transfer instruction, exactly as <c>ScheduleRevisionKeys</c> builds it.</summary>
    public static string Key(int personId) => $"transfer:{personId}:2026-10-08:Day";

    public static Task CheckInAsync(
        JanitorialApiFactory factory, HttpClient client, DateTimeOffset at, string token = Origin) =>
        AttendanceApi.CheckInAsync(factory, client, at, token);

    /// <summary>
    /// The destination sign's captured position, the way an Admin standing at it captures one: without it a scan of that
    /// sign has no geofence verdict at all, so a GPS assertion would be measuring nothing.
    /// </summary>
    public static Task LocateAsync(
        JanitorialApiFactory factory, string token = Destination, double latitude = TestGps.NearLatitude,
        double longitude = TestGps.NearLongitude) =>
        factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.SingleAsync(entry => entry.QrToken == token);
            sign.Latitude = (decimal)latitude;
            sign.Longitude = (decimal)longitude;
            sign.LocationAccuracyM = 10;
            sign.LocationSource = LocationSource.Site;
            sign.LocatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });

    /// <summary>
    /// Records one instruction at <paramref name="effectiveAt"/> through the command envelope, moves the clock to just before
    /// it, and answers the stored instruction's id.
    /// </summary>
    public static async Task<int> InstructForIdAsync(
        JanitorialApiFactory factory, HttpClient client, int personId, int fromAreaId, int toAreaId,
        DateTimeOffset effectiveAt)
    {
        factory.Clock.SetUtcNow(effectiveAt.AddMinutes(-5));
        var (response, answer) = await InstructAsync(factory, client, personId, fromAreaId, toAreaId, effectiveAt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ScheduleCommandOutcome.Committed, answer!.Outcome);
        return (await InstructionsAsync(factory))[^1].Id;
    }

    /// <summary>Submits one instruction, with the clock just before its effective time unless the caller names one.</summary>
    public static Task<(HttpResponseMessage Response, ScheduleWriteApi.CommandAnswer? Answer)> InstructAsync(
        JanitorialApiFactory factory, HttpClient client, int personId, int fromAreaId, int toAreaId,
        DateTimeOffset effectiveAt, DateTimeOffset? at = null, string? reason = "ย้ายพื้นที่ตามคำสั่งหน้างาน",
        Dictionary<string, string>? expected = null)
    {
        factory.Clock.SetUtcNow(at ?? effectiveAt.AddMinutes(-5));
        return ScheduleWriteApi.ExecuteAsync(client, Instruct, new
        {
            userId = personId,
            shiftDate = "2026-10-08",
            shift = "Day",
            fromAreaId,
            toAreaId,
            effectiveAt = effectiveAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            reason,
        }, expected);
    }

    public static Task<(HttpResponseMessage Response, ScheduleWriteApi.CommandAnswer? Answer)> CancelAsync(
        HttpClient client, int transferId, string? reason = "ยกเลิกคำสั่งย้าย", Dictionary<string, string>? expected = null) =>
        ScheduleWriteApi.ExecuteAsync(client, Cancel, new { transferId, reason }, expected);

    /// <summary>Moves the clock to the arrival instant (when one is named) and submits the confirmation.</summary>
    public static Task<HttpResponseMessage> ConfirmAsync(
        JanitorialApiFactory factory, HttpClient client, int transferId, string? expectedRevision,
        string token = Destination, DateTimeOffset? at = null, double? latitude = TestGps.NearLatitude,
        double? longitude = TestGps.NearLongitude, double? accuracyM = 10)
    {
        if (at is { } moment)
        {
            factory.Clock.SetUtcNow(moment);
        }

        return client.PostAsJsonAsync($"/api/area-transfers/{transferId}/confirm", new
        {
            expectedRevision,
            qrToken = token,
            latitude,
            longitude,
            accuracyM,
        });
    }

    public static async Task<TransferAnswer> ReadConfirmAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransferAnswer>())!;
    }

    public static Task<List<AreaTransfer>> InstructionsAsync(JanitorialApiFactory factory) =>
        AdminSetupApi.ReadAsync(factory, db => db.AreaTransfers.AsNoTracking().OrderBy(row => row.Id).ToListAsync());

    public static Task<List<AreaTransferConfirmation>> ConfirmationsAsync(JanitorialApiFactory factory) =>
        AdminSetupApi.ReadAsync(factory,
            db => db.AreaTransferConfirmations.AsNoTracking().OrderBy(row => row.Id).ToListAsync());
}

/// <summary>The confirmation answer as the phone receives it.</summary>
public record TransferAnswer(int Id, string State, DateTimeOffset? ConfirmedAt, string Revision);

/// <summary>The transfer member of the attendance read (spec §9): null when the slot has no instruction.</summary>
public record AttendanceTransferAnswer(
    int Id, int FromAreaId, int ToAreaId, DateTime EffectiveAt, string State, DateTime? ConfirmedAt, string Revision);
