using System.Globalization;
using JanitorialManagementDashboard.Application.Attendance;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>
/// Spec §8, row "จังหวะพักหลัง final departure" (ADR facility-0222): an action whose expected instant falls at or after the
/// recorded departure and inside recorded applicable leave is derived not-applicable because of the leave —
/// <c>ให้ not-applicable เนื่องจากลา; ไม่สร้าง actual event</c> — while a scan forgotten before that departure stays missing,
/// <c>คง missing/skipped ไม่ถูกล้างเพราะมีลา</c>. The classification is derived on every read, so it never writes an event.
/// </summary>
public class AttendanceApplicabilityTests
{
    private static readonly DateTimeOffset Departure = DateTimeOffset.Parse("2026-11-17T06:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset LeaveEnd = Departure.AddHours(6);

    [Fact]
    public void Half_day_departure_exempts_only_future_actions_inside_leave()
    {
        var departure = DateTimeOffset.Parse("2026-11-17T06:00:00Z");
        var end = departure.AddHours(6);
        Assert.True(AttendanceApplicability.NotApplicable(departure.AddHours(3), departure, departure, end));
        Assert.False(AttendanceApplicability.NotApplicable(departure.AddHours(-2), departure, departure, end));
    }

    [Fact]
    public void The_departure_instant_itself_is_already_not_applicable() =>
        Assert.True(AttendanceApplicability.NotApplicable(Departure, Departure, Departure, LeaveEnd));

    [Fact]
    public void One_tick_before_the_departure_still_needs_the_scan() =>
        Assert.False(AttendanceApplicability.NotApplicable(Departure.AddTicks(-1), Departure, Departure, LeaveEnd));

    [Fact]
    public void An_action_exactly_at_the_leave_start_is_not_applicable() =>
        Assert.True(AttendanceApplicability.NotApplicable(Departure.AddHours(1), Departure, Departure.AddHours(1), LeaveEnd));

    [Fact]
    public void One_tick_before_the_leave_start_still_needs_the_scan() =>
        Assert.False(AttendanceApplicability.NotApplicable(Departure.AddHours(1), Departure, Departure.AddHours(1).AddTicks(1), LeaveEnd));

    [Fact]
    public void An_action_at_the_leave_end_still_needs_the_scan() =>
        Assert.False(AttendanceApplicability.NotApplicable(LeaveEnd, Departure, Departure, LeaveEnd));

    [Fact]
    public void A_scan_forgotten_before_the_departure_stays_missing_though_leave_exists() =>
        Assert.Equal(
            AttendanceApplicabilityStatus.Missing,
            AttendanceApplicability.Classify(Departure.AddHours(-2), Departure.AddHours(1), Departure, Departure, LeaveEnd));

    [Fact]
    public void A_scan_due_after_the_departure_inside_the_leave_is_not_applicable() =>
        Assert.Equal(
            AttendanceApplicabilityStatus.NotApplicable,
            AttendanceApplicability.Classify(Departure.AddHours(3), Departure.AddHours(4), Departure, Departure, LeaveEnd));

    [Fact]
    public void Nothing_is_missing_before_the_expected_instant() =>
        Assert.Equal(
            AttendanceApplicabilityStatus.Pending,
            AttendanceApplicability.Classify(Departure.AddHours(3), Departure.AddHours(2), null, null, null));

    [Fact]
    public void The_expected_instant_arrives_and_the_scan_is_missing() =>
        Assert.Equal(
            AttendanceApplicabilityStatus.Missing,
            AttendanceApplicability.Classify(Departure.AddHours(3), Departure.AddHours(3), null, null, null));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Without_both_a_departure_and_a_leave_interval_nothing_is_not_applicable(bool hasDeparture, bool hasLeave) =>
        Assert.Equal(
            AttendanceApplicabilityStatus.Missing,
            AttendanceApplicability.Classify(
                Departure.AddHours(3),
                Departure.AddHours(9),
                hasDeparture ? Departure : null,
                hasLeave ? Departure : null,
                hasLeave ? LeaveEnd : null));

    [Fact]
    public void An_empty_leave_interval_exempts_nothing() =>
        Assert.Equal(
            AttendanceApplicabilityStatus.Missing,
            AttendanceApplicability.Classify(Departure.AddHours(3), Departure.AddHours(9), Departure, LeaveEnd, LeaveEnd));

    [Fact]
    public void Not_applicable_is_a_status_of_its_own_and_not_a_missing_scan() =>
        Assert.NotEqual(AttendanceApplicabilityStatus.Missing, AttendanceApplicabilityStatus.NotApplicable);
}
