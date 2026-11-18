using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>
/// Spec §13 and ADR facility-0216 (Task 11): entry evidence is an actual arrival, so an initial Shift-In and a confirmed
/// transfer arrival both count, the same person can have entered two Areas in one slot without being in two places at once,
/// and nothing that is merely planned — an instruction, a loan, a skip marker or a pending replacement — is evidence.
/// </summary>
public class ScheduleEntryEvidenceTests
{
    private static readonly DateOnly Date = new(2026, 11, 17);
    private static readonly ScheduleSlot Slot = new(Date, Shift.Day);

    [Fact]
    public void Confirmed_arrival_adds_destination_without_removing_origin_entry()
    {
        AreaEntryEvidence[] facts = [
            new(1, Date, Shift.Day, DateTimeOffset.Parse("2026-11-17T00:00:00Z"), "shift-in", null),
            new(2, Date, Shift.Day, DateTimeOffset.Parse("2026-11-17T06:05:00Z"), "transfer-arrival", 7)];
        Assert.True(ScheduleEntryEvidence.EnteredAreas(Slot, facts).SetEquals([1, 2]));
    }

    [Fact]
    public void Evidence_from_another_slot_is_not_entry_for_this_one()
    {
        AreaEntryEvidence[] facts = [
            new(1, Date, Shift.Day, DateTimeOffset.Parse("2026-11-17T00:00:00Z"), "shift-in", null),
            new(2, Date.AddDays(1), Shift.Day, DateTimeOffset.Parse("2026-11-18T00:00:00Z"), "shift-in", null),
            new(3, Date, Shift.Night, DateTimeOffset.Parse("2026-11-17T12:00:00Z"), "shift-in", null)];
        Assert.True(ScheduleEntryEvidence.EnteredAreas(Slot, facts).SetEquals([1]));
    }

    [Theory]
    [InlineData("transfer-instruction")]
    [InlineData("relief-loan")]
    [InlineData("skipped")]
    [InlineData("pending-replacement")]
    [InlineData("")]
    public void A_planned_or_skipped_fact_is_never_entry_evidence(string kind)
    {
        AreaEntryEvidence[] facts = [new(1, Date, Shift.Day, DateTimeOffset.Parse("2026-11-17T00:00:00Z"), kind, null)];
        Assert.Empty(ScheduleEntryEvidence.EnteredAreas(Slot, facts));
    }

    [Fact]
    public void Two_facts_at_one_area_count_it_once()
    {
        AreaEntryEvidence[] facts = [
            new(1, Date, Shift.Day, DateTimeOffset.Parse("2026-11-17T00:00:00Z"), "shift-in", null),
            new(1, Date, Shift.Day, DateTimeOffset.Parse("2026-11-17T06:05:00Z"), "transfer-arrival", 7)];
        Assert.Single(ScheduleEntryEvidence.EnteredAreas(Slot, facts));
    }

    [Fact]
    public void No_evidence_enters_nothing()
    {
        Assert.Empty(ScheduleEntryEvidence.EnteredAreas(Slot, []));
    }

    [Theory]
    [InlineData(true, true, "entered")]
    [InlineData(true, false, "entered")]
    [InlineData(false, true, "covered")]
    [InlineData(false, false, "unassigned")]
    public void The_entry_category_puts_evidence_before_an_assignment(bool entered, bool assigned, string expected) =>
        Assert.Equal(expected, ScheduleEntryEvidence.EntryOf(entered, assigned));
}
