using FacilityRealtime.Application.Export;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>facility-0047, 0053: the export lists every round of an ended shift that nobody did, as "ไม่ได้ทำ".</summary>
public class ExportRulesTests
{
    private static readonly ShiftSlot Day8 = new(new DateOnly(2026, 10, 8), Shift.Day);
    private static readonly DateTime LongAgo = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AfterTheShift = ShiftCalendar.EndUtc(Day8).AddMinutes(1);

    private static (RoundWindow Window, DateTime CreatedAtUtc) Window(int id, int startHour, int endHour, DateTime? createdAtUtc = null) =>
        (RoundWindow.For(id, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0), Day8), createdAtUtc ?? LongAgo);

    [Fact]
    public void Rounds_with_no_counted_submission_are_missed_in_the_order_of_the_shift()
    {
        var missed = ExportRules.MissedRounds([Window(1, 16, 18), Window(2, 7, 9)], new HashSet<int>(), Day8, AfterTheShift);

        Assert.Equal(new[] { 2, 1 }, missed.Select(w => w.Id));
    }

    [Fact]
    public void A_done_round_is_not_missed()
    {
        var missed = ExportRules.MissedRounds([Window(1, 7, 9), Window(2, 16, 18)], new HashSet<int> { 1 }, Day8, AfterTheShift);

        Assert.Equal(new[] { 2 }, missed.Select(w => w.Id));
    }

    [Fact]
    public void Nothing_is_missed_while_the_shift_is_still_running()
    {
        var missed = ExportRules.MissedRounds([Window(1, 7, 9)], new HashSet<int>(), Day8, ShiftCalendar.EndUtc(Day8).AddMinutes(-1));

        Assert.Empty(missed);
    }

    [Fact]
    public void A_window_added_after_its_round_started_does_not_count_for_that_shift()
    {
        var startUtc = RoundWindow.For(1, new TimeOnly(7, 0), new TimeOnly(9, 0), Day8).StartUtc;

        var missed = ExportRules.MissedRounds([Window(1, 7, 9, startUtc.AddMinutes(1))], new HashSet<int>(), Day8, AfterTheShift);

        Assert.Empty(missed);
    }

    [Fact]
    public void A_night_round_after_midnight_comes_after_the_evening_one()
    {
        var night8 = new ShiftSlot(new DateOnly(2026, 10, 8), Shift.Night);
        (RoundWindow, DateTime) Night(int id, int start, int end) => (RoundWindow.For(id, new TimeOnly(start, 0), new TimeOnly(end, 0), night8), LongAgo);

        var missed = ExportRules.MissedRounds([Night(1, 3, 5), Night(2, 20, 22)], new HashSet<int>(), night8, ShiftCalendar.EndUtc(night8));

        Assert.Equal(new[] { 2, 1 }, missed.Select(w => w.Id));
    }
}
