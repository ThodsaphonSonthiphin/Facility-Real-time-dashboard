using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>
/// Spec §8 (ADR facility-0222): every dated slot carries one ordered action list — the legacy four or the numbered six —
/// and the server says which actions may be recorded next and which intermediate breaks an earlier final Shift-Out would
/// step over. The Cleaner confirms those gaps from the latest revision and sends skip markers, never guessed times:
/// <c>ส่ง skipped markers ไม่ใช่ guessed times ห้ามข้าม ShiftIn, ทำหลัง final ShiftOut, ซ้ำ event หรือย้อนลำดับ</c>.
/// The answers are pure data, so the endpoints and anything that later explains a stored row share one contract.
/// </summary>
public class AttendanceLifecycleTests
{
    private static readonly AttendanceSettings Settings = new() { OpensMinutesBeforeShift = 60, ClosesMinutesAfterShift = 60 };

    private static readonly AttendanceEvent[] Six =
    [
        AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break1In,
        AttendanceEvent.Break2Out, AttendanceEvent.Break2In, AttendanceEvent.ShiftOut,
    ];

    private static IReadOnlyList<AttendanceEvent> Offered(
        AttendanceLifecycleVersion version, AttendanceEvent[] recorded, AttendanceEvent[]? skipped = null) =>
        AttendanceLifecycle.NextActions(version, recorded, skipped ?? []).Select(choice => choice.Action).ToArray();

    private static AttendanceChoice ChoiceOf(
        AttendanceLifecycleVersion version, AttendanceEvent action, AttendanceEvent[] recorded, AttendanceEvent[]? skipped = null) =>
        AttendanceLifecycle.NextActions(version, recorded, skipped ?? []).Single(choice => choice.Action == action);

    private static ShiftSlot Slot(int day, Shift shift) => new(new DateOnly(2026, 11, day), shift);

    // ---- the ordered action list of each lifecycle version ----

    [Fact]
    public void A_legacy_slot_keeps_the_four_legacy_actions_in_order() =>
        Assert.Equal(
            new[] { AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut },
            AttendanceLifecycle.Order(AttendanceLifecycleVersion.Legacy4));

    [Fact]
    public void A_numbered_slot_has_the_six_numbered_actions_in_lifecycle_order() =>
        Assert.Equal(Six, AttendanceLifecycle.Order(AttendanceLifecycleVersion.Numbered6));

    [Fact]
    public void A_legacy_slot_is_never_relabelled_with_numbered_breaks()
    {
        var legacy = AttendanceLifecycle.Order(AttendanceLifecycleVersion.Legacy4);

        Assert.DoesNotContain(AttendanceEvent.Break1Out, legacy);
        Assert.DoesNotContain(AttendanceEvent.Break1In, legacy);
        Assert.DoesNotContain(AttendanceEvent.Break2Out, legacy);
        Assert.DoesNotContain(AttendanceEvent.Break2In, legacy);
    }

    [Fact]
    public void The_order_is_shared_and_read_only()
    {
        var order = AttendanceLifecycle.Order(AttendanceLifecycleVersion.Numbered6);

        Assert.Same(order, AttendanceLifecycle.Order(AttendanceLifecycleVersion.Numbered6));
        Assert.IsNotType<AttendanceEvent[]>(order);
        Assert.Throws<NotSupportedException>(() => ((IList<AttendanceEvent>)order).Add(AttendanceEvent.Break1Out));
        Assert.Throws<NotSupportedException>(() => ((IList<AttendanceEvent>)order)[0] = AttendanceEvent.Break1Out);
    }

    [Fact]
    public void A_slot_without_a_lifecycle_row_stays_legacy() =>
        Assert.Equal(AttendanceLifecycleVersion.Legacy4, AttendanceLifecycle.VersionFor(null));

    [Fact]
    public void A_stored_lifecycle_version_is_kept_as_stored() =>
        Assert.Equal(AttendanceLifecycleVersion.Numbered6, AttendanceLifecycle.VersionFor(AttendanceLifecycleVersion.Numbered6));

    [Fact]
    public void Shift_in_and_shift_out_are_never_skippable()
    {
        Assert.False(AttendanceLifecycle.IsSkippable(AttendanceEvent.ShiftIn));
        Assert.False(AttendanceLifecycle.IsSkippable(AttendanceEvent.ShiftOut));
    }

    [Theory]
    [InlineData(AttendanceEvent.BreakOut)]
    [InlineData(AttendanceEvent.BreakIn)]
    [InlineData(AttendanceEvent.Break1Out)]
    [InlineData(AttendanceEvent.Break1In)]
    [InlineData(AttendanceEvent.Break2Out)]
    [InlineData(AttendanceEvent.Break2In)]
    public void Only_the_intermediate_breaks_may_be_skipped(AttendanceEvent intermediate) =>
        Assert.True(AttendanceLifecycle.IsSkippable(intermediate));

    // ---- what may be recorded next ----

    [Theory]
    [InlineData(AttendanceLifecycleVersion.Legacy4)]
    [InlineData(AttendanceLifecycleVersion.Numbered6)]
    public void Before_shift_in_the_only_choice_is_shift_in_with_no_gaps(AttendanceLifecycleVersion version)
    {
        var choices = AttendanceLifecycle.NextActions(version, [], []);

        Assert.Equal(AttendanceEvent.ShiftIn, Assert.Single(choices).Action);
        Assert.Empty(choices[0].RequiredGaps);
    }

    [Fact]
    public void A_skip_marker_cannot_hide_shift_in() =>
        Assert.Equal(
            AttendanceEvent.ShiftIn,
            Assert.Single(AttendanceLifecycle.NextActions(AttendanceLifecycleVersion.Numbered6, [], [AttendanceEvent.ShiftIn])).Action);

    [Fact]
    public void After_shift_in_a_numbered_slot_offers_the_five_later_actions() =>
        Assert.Equal(Six.Skip(1), Offered(AttendanceLifecycleVersion.Numbered6, [AttendanceEvent.ShiftIn]));

    [Fact]
    public void A_legacy_slot_offers_its_own_four_actions_and_never_a_numbered_break()
    {
        // The lifecycle surface answers with the slot's four actions; the older short-chain answer that the existing
        // endpoints already use (Break-In only after a Break-Out) stays untouched in AttendanceRules.NextAllowed.
        var offered = Offered(AttendanceLifecycleVersion.Legacy4, [AttendanceEvent.ShiftIn]);

        Assert.Equal(
            new[] { AttendanceEvent.BreakOut, AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut },
            offered);
        Assert.DoesNotContain(AttendanceEvent.Break1Out, offered);
    }

    [Fact]
    public void The_first_break_needs_no_gap_because_shift_in_is_recorded() =>
        Assert.Empty(ChoiceOf(AttendanceLifecycleVersion.Numbered6, AttendanceEvent.Break1Out, [AttendanceEvent.ShiftIn]).RequiredGaps);

    [Fact]
    public void Each_later_action_lists_the_unrecorded_breaks_it_would_step_over()
    {
        AttendanceEvent[] recorded = [AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out];

        Assert.Equal(
            new[]
            {
                AttendanceEvent.Break1In, AttendanceEvent.Break2Out, AttendanceEvent.Break2In, AttendanceEvent.ShiftOut,
            },
            Offered(AttendanceLifecycleVersion.Numbered6, recorded));
        Assert.Empty(ChoiceOf(AttendanceLifecycleVersion.Numbered6, AttendanceEvent.Break1In, recorded).RequiredGaps);
        Assert.Equal(
            new[] { AttendanceEvent.Break1In },
            ChoiceOf(AttendanceLifecycleVersion.Numbered6, AttendanceEvent.Break2Out, recorded).RequiredGaps);
        Assert.Equal(
            new[] { AttendanceEvent.Break1In, AttendanceEvent.Break2Out },
            ChoiceOf(AttendanceLifecycleVersion.Numbered6, AttendanceEvent.Break2In, recorded).RequiredGaps);
        Assert.Equal(
            new[] { AttendanceEvent.Break1In, AttendanceEvent.Break2Out, AttendanceEvent.Break2In },
            ChoiceOf(AttendanceLifecycleVersion.Numbered6, AttendanceEvent.ShiftOut, recorded).RequiredGaps);
    }

    [Fact]
    public void An_explicit_skip_is_not_asked_for_a_second_time()
    {
        // Spec §8: "Break1 --> Break2: Explicit skip missing Break1In" — the skipped marker already answers the gap.
        AttendanceEvent[] recorded = [AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out];
        AttendanceEvent[] skipped = [AttendanceEvent.Break1In];

        Assert.Equal(
            new[] { AttendanceEvent.Break2Out, AttendanceEvent.Break2In, AttendanceEvent.ShiftOut },
            Offered(AttendanceLifecycleVersion.Numbered6, recorded, skipped));
        Assert.Equal(
            new[] { AttendanceEvent.Break2Out, AttendanceEvent.Break2In },
            ChoiceOf(AttendanceLifecycleVersion.Numbered6, AttendanceEvent.ShiftOut, recorded, skipped).RequiredGaps);
    }

    [Fact]
    public void Shift_out_may_be_chosen_while_the_unresolved_breaks_are_confirmed_as_gaps() =>
        Assert.Equal(
            new[]
            {
                AttendanceEvent.Break1Out, AttendanceEvent.Break1In, AttendanceEvent.Break2Out, AttendanceEvent.Break2In,
            },
            ChoiceOf(AttendanceLifecycleVersion.Numbered6, AttendanceEvent.ShiftOut, [AttendanceEvent.ShiftIn]).RequiredGaps);

    [Fact]
    public void With_both_breaks_skipped_shift_out_needs_no_further_confirmation()
    {
        AttendanceEvent[] skipped = [AttendanceEvent.Break1Out, AttendanceEvent.Break1In, AttendanceEvent.Break2Out, AttendanceEvent.Break2In];

        Assert.Empty(ChoiceOf(
            AttendanceLifecycleVersion.Numbered6, AttendanceEvent.ShiftOut, [AttendanceEvent.ShiftIn], skipped).RequiredGaps);
    }

    [Fact]
    public void Nothing_is_offered_after_the_final_shift_out()
    {
        Assert.Empty(AttendanceLifecycle.NextActions(AttendanceLifecycleVersion.Numbered6, Six, []));
        Assert.Empty(AttendanceLifecycle.NextActions(
            AttendanceLifecycleVersion.Legacy4, [AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut], []));
    }

    // ---- activation: chosen strictly before the receiving window opens ----

    [Theory]
    [InlineData(Shift.Day, 18)]
    [InlineData(Shift.Night, 18)]
    [InlineData(Shift.Day, 25)]
    public void Activation_is_allowed_one_tick_before_the_window_opens(Shift shift, int day)
    {
        var slot = Slot(day, shift);
        var (opens, _) = AttendanceCalendar.Window(slot, Settings);

        Assert.Null(AttendanceLifecycle.CheckActivation(slot, opens.AddTicks(-1), Settings, hasRecordedFacts: false));
    }

    [Theory]
    [InlineData(Shift.Day, 18)]
    [InlineData(Shift.Night, 18)]
    [InlineData(Shift.Night, 25)]
    public void Activation_is_refused_at_exactly_the_window_open_and_later(Shift shift, int day)
    {
        // Spec §8 Cutover: the boundary is chosen before the receiving window opens; after it no new version is chosen.
        var slot = Slot(day, shift);
        var (opens, closes) = AttendanceCalendar.Window(slot, Settings);

        Assert.Equal("window-open", AttendanceLifecycle.CheckActivation(slot, opens, Settings, hasRecordedFacts: false));
        Assert.Equal("window-open", AttendanceLifecycle.CheckActivation(slot, opens.AddTicks(1), Settings, hasRecordedFacts: false));
        Assert.Equal("window-open", AttendanceLifecycle.CheckActivation(slot, closes, Settings, hasRecordedFacts: false));
        Assert.Equal("window-open", AttendanceLifecycle.CheckActivation(slot, closes.AddDays(1), Settings, hasRecordedFacts: false));
    }

    [Fact]
    public void A_slot_that_already_holds_attendance_facts_is_never_activated()
    {
        var slot = Slot(18, Shift.Day);
        var (opens, _) = AttendanceCalendar.Window(slot, Settings);

        Assert.Equal(
            "slot-has-recorded-facts",
            AttendanceLifecycle.CheckActivation(slot, opens.AddDays(-1), Settings, hasRecordedFacts: true));
        // The facts win over the window code: the slot is unusable as a first numbered slot either way.
        Assert.Equal(
            "slot-has-recorded-facts",
            AttendanceLifecycle.CheckActivation(slot, opens, Settings, hasRecordedFacts: true));
    }
}
