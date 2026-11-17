using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>
/// Spec §8 and §11: the revision a page sends back with a write, so a stale or incompatible page is refused before anything
/// is written. It is derived only from the slot's ordered actual facts — identity, action and instant — so it changes
/// exactly when a fact changes, and two pages that read the same facts in a different order agree.
/// </summary>
public class AttendanceStateRevisionTests
{
    private static readonly ShiftSlot Slot = new(new DateOnly(2026, 11, 17), Shift.Day);

    private static ShiftAttendance Fact(long id, AttendanceEvent action, string isoUtc) => new()
    {
        Id = id,
        UserId = 7,
        ShiftDate = Slot.ShiftDate,
        Shift = Slot.Shift,
        EventType = action,
        OccurredAt = DateTime.Parse(isoUtc, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal),
    };

    private static string Of(params ShiftAttendance[] facts) => AttendanceStateRevision.Of(7, Slot, facts);

    [Fact]
    public void The_same_facts_always_answer_the_same_revision()
    {
        var facts = new[] { Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:00Z") };

        Assert.Equal(Of(facts), Of(facts));
        Assert.Equal(Of(facts), Of(Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:00Z")));
    }

    [Fact]
    public void The_revision_is_a_stable_hex_token_and_not_the_fact_count()
    {
        var revision = Of(Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:00Z"));

        Assert.Matches("^[0-9a-f]{64}$", revision);
    }

    [Fact]
    public void An_added_fact_changes_the_revision()
    {
        var single = Of(Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:00Z"));

        var both = Of(
            Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:00Z"),
            Fact(2, AttendanceEvent.Break1Out, "2026-11-17T03:00:00Z"));

        Assert.NotEqual(single, both);
    }

    [Fact]
    public void An_instant_that_moved_changes_the_revision()
    {
        Assert.NotEqual(
            Of(Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:00Z")),
            Of(Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:01Z")));
    }

    [Fact]
    public void A_changed_action_changes_the_revision()
    {
        Assert.NotEqual(
            Of(Fact(1, AttendanceEvent.Break1Out, "2026-11-17T03:00:00Z")),
            Of(Fact(1, AttendanceEvent.Break2Out, "2026-11-17T03:00:00Z")));
    }

    [Fact]
    public void The_order_the_facts_arrive_in_does_not_matter()
    {
        var first = Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:00Z");
        var second = Fact(2, AttendanceEvent.Break1Out, "2026-11-17T03:00:00Z");

        Assert.Equal(Of(first, second), Of(second, first));
    }

    [Fact]
    public void Two_people_with_identical_facts_do_not_share_a_revision()
    {
        var fact = Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:00Z");

        Assert.NotEqual(AttendanceStateRevision.Of(7, Slot, [fact]), AttendanceStateRevision.Of(8, Slot, [fact]));
    }

    [Fact]
    public void Two_slots_of_the_same_person_do_not_share_a_revision()
    {
        var fact = Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:00Z");

        Assert.NotEqual(
            AttendanceStateRevision.Of(7, Slot, [fact]),
            AttendanceStateRevision.Of(7, new ShiftSlot(Slot.ShiftDate.AddDays(1), Slot.Shift), [fact]));
    }

    [Fact]
    public void A_slot_with_no_facts_has_its_own_revision_and_nothing_to_read()
    {
        var empty = AttendanceStateRevision.Of(7, Slot, []);

        Assert.Matches("^[0-9a-f]{64}$", empty);
        Assert.NotEqual(empty, Of(Fact(1, AttendanceEvent.ShiftIn, "2026-11-17T00:00:00Z")));
    }
}
