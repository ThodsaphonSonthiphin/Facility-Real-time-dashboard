using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

public class AttendanceLifecycleTests
{
    private static readonly AttendanceEvent[] Six =
    [
        AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break1In,
        AttendanceEvent.Break2Out, AttendanceEvent.Break2In, AttendanceEvent.ShiftOut,
    ];

    private static IReadOnlyList<AttendanceEvent> Offered(
        AttendanceEvent[] recorded, AttendanceEvent[]? skipped = null) =>
        AttendanceLifecycle.NextActions(recorded, skipped ?? []).Select(choice => choice.Action).ToArray();

    private static AttendanceChoice ChoiceOf(
        AttendanceEvent action, AttendanceEvent[] recorded, AttendanceEvent[]? skipped = null) =>
        AttendanceLifecycle.NextActions(recorded, skipped ?? []).Single(choice => choice.Action == action);



    [Fact]
    public void A_numbered_slot_has_the_six_numbered_actions_in_lifecycle_order() =>
        Assert.Equal(Six, AttendanceLifecycle.Order);

    [Fact]
    public void The_order_is_shared_and_read_only()
    {
        var order = AttendanceLifecycle.Order;

        Assert.Same(order, AttendanceLifecycle.Order);
        Assert.IsNotType<AttendanceEvent[]>(order);
        Assert.Throws<NotSupportedException>(() => ((IList<AttendanceEvent>)order).Add(AttendanceEvent.Break1Out));
        Assert.Throws<NotSupportedException>(() => ((IList<AttendanceEvent>)order)[0] = AttendanceEvent.Break1Out);
    }

    [Fact]
    public void Shift_in_and_shift_out_are_never_skippable()
    {
        Assert.False(AttendanceLifecycle.IsSkippable(AttendanceEvent.ShiftIn));
        Assert.False(AttendanceLifecycle.IsSkippable(AttendanceEvent.ShiftOut));
    }

    [Theory]
    [InlineData(AttendanceEvent.Break1Out)]
    [InlineData(AttendanceEvent.Break1In)]
    [InlineData(AttendanceEvent.Break2Out)]
    [InlineData(AttendanceEvent.Break2In)]
    public void Only_the_intermediate_breaks_may_be_skipped(AttendanceEvent intermediate) =>
        Assert.True(AttendanceLifecycle.IsSkippable(intermediate));

    // ---- what may be recorded next ----

    [Fact]
    public void Before_shift_in_the_only_choice_is_shift_in_with_no_gaps()
    {
        var choices = AttendanceLifecycle.NextActions([], []);

        Assert.Equal(AttendanceEvent.ShiftIn, Assert.Single(choices).Action);
        Assert.Empty(choices[0].RequiredGaps);
    }

    [Fact]
    public void A_skip_marker_cannot_hide_shift_in() =>
        Assert.Equal(
            AttendanceEvent.ShiftIn,
            Assert.Single(AttendanceLifecycle.NextActions([], [AttendanceEvent.ShiftIn])).Action);

    [Fact]
    public void After_shift_in_a_numbered_slot_offers_the_five_later_actions() =>
        Assert.Equal(Six.Skip(1), Offered([AttendanceEvent.ShiftIn]));

    [Fact]
    public void A_skip_marker_cannot_hide_the_final_shift_out()
    {
        // Spec §8 "ทำหลัง final ShiftOut": the departure is never skippable, so a stray marker for it can never deadlock
        // the shift — the final action is still offered, and the breaks before it are still the gaps to confirm.
        Assert.Equal(
            Six.Skip(1),
            Offered([AttendanceEvent.ShiftIn], [AttendanceEvent.ShiftOut]));
        Assert.Equal(
            new[]
            {
                AttendanceEvent.Break1Out, AttendanceEvent.Break1In, AttendanceEvent.Break2Out, AttendanceEvent.Break2In,
            },
            ChoiceOf(
                 AttendanceEvent.ShiftOut, [AttendanceEvent.ShiftIn], [AttendanceEvent.ShiftOut])
                .RequiredGaps);
    }

    [Fact]
    public void A_skip_marker_for_a_non_skippable_action_hides_nothing_else_either()
    {
        // Filtered through IsSkippable before it is trusted: a marker on Shift-In or Shift-Out is not a skip at all, so it
        // is neither offered nor counted as a confirmed gap.
        AttendanceEvent[] straySkips = [AttendanceEvent.ShiftIn, AttendanceEvent.ShiftOut];

        Assert.Equal(Six.Skip(1), Offered([AttendanceEvent.ShiftIn], straySkips));
        Assert.Equal(
            new[] { AttendanceEvent.Break1Out, AttendanceEvent.Break1In },
            ChoiceOf(AttendanceEvent.Break2Out, [AttendanceEvent.ShiftIn], straySkips)
                .RequiredGaps);
    }

    [Fact]
    public void The_first_break_needs_no_gap_because_shift_in_is_recorded() =>
        Assert.Empty(ChoiceOf(AttendanceEvent.Break1Out, [AttendanceEvent.ShiftIn]).RequiredGaps);

    [Fact]
    public void Each_later_action_lists_the_unrecorded_breaks_it_would_step_over()
    {
        AttendanceEvent[] recorded = [AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out];

        Assert.Equal(
            new[]
            {
                AttendanceEvent.Break1In, AttendanceEvent.Break2Out, AttendanceEvent.Break2In, AttendanceEvent.ShiftOut,
            },
            Offered(recorded));
        Assert.Empty(ChoiceOf(AttendanceEvent.Break1In, recorded).RequiredGaps);
        Assert.Equal(
            new[] { AttendanceEvent.Break1In },
            ChoiceOf(AttendanceEvent.Break2Out, recorded).RequiredGaps);
        Assert.Equal(
            new[] { AttendanceEvent.Break1In, AttendanceEvent.Break2Out },
            ChoiceOf(AttendanceEvent.Break2In, recorded).RequiredGaps);
        Assert.Equal(
            new[] { AttendanceEvent.Break1In, AttendanceEvent.Break2Out, AttendanceEvent.Break2In },
            ChoiceOf(AttendanceEvent.ShiftOut, recorded).RequiredGaps);
    }

    [Fact]
    public void An_explicit_skip_is_not_asked_for_a_second_time()
    {
        // Spec §8: "Break1 --> Break2: Explicit skip missing Break1In" — the skipped marker already answers the gap.
        AttendanceEvent[] recorded = [AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out];
        AttendanceEvent[] skipped = [AttendanceEvent.Break1In];

        Assert.Equal(
            new[] { AttendanceEvent.Break2Out, AttendanceEvent.Break2In, AttendanceEvent.ShiftOut },
            Offered(recorded, skipped));
        Assert.Equal(
            new[] { AttendanceEvent.Break2Out, AttendanceEvent.Break2In },
            ChoiceOf(AttendanceEvent.ShiftOut, recorded, skipped).RequiredGaps);
    }

    [Fact]
    public void Shift_out_may_be_chosen_while_the_unresolved_breaks_are_confirmed_as_gaps() =>
        Assert.Equal(
            new[]
            {
                AttendanceEvent.Break1Out, AttendanceEvent.Break1In, AttendanceEvent.Break2Out, AttendanceEvent.Break2In,
            },
            ChoiceOf(AttendanceEvent.ShiftOut, [AttendanceEvent.ShiftIn]).RequiredGaps);

    [Fact]
    public void With_both_breaks_skipped_shift_out_needs_no_further_confirmation()
    {
        AttendanceEvent[] skipped = [AttendanceEvent.Break1Out, AttendanceEvent.Break1In, AttendanceEvent.Break2Out, AttendanceEvent.Break2In];

        Assert.Empty(ChoiceOf(
             AttendanceEvent.ShiftOut, [AttendanceEvent.ShiftIn], skipped).RequiredGaps);
    }

    [Fact]
    public void Nothing_is_offered_after_the_final_shift_out()
    {
        Assert.Empty(AttendanceLifecycle.NextActions(Six, []));
    }
}
