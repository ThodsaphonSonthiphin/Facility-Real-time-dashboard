using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests.Scheduling;

/// <summary>
/// Spec §3 and ADRs facility-0140/0142/0215: Admin manages every building and shift, a Supervisor only the buildings a
/// dated period gives them for that shift, and a historical correction needs both the edited date and today inside the
/// responsibility. Active-account checks belong to the scoped reader, not to this pure rule.
/// </summary>
public class ScheduleAuthorityTests
{
    private static readonly DateOnly Day = new(2026, 11, 17);

    [Fact]
    public void Same_supervisor_can_manage_two_buildings_but_not_other_shift()
    {
        var day = new DateOnly(2026, 11, 17);
        ScopePeriod[] scope = [new(10, 1, Shift.Day, day, null), new(10, 2, Shift.Day, day, null)];
        Assert.True(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 2, Shift.Day, day, scope));
        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 2, Shift.Night, day, scope));
    }

    [Fact]
    public void CanManageSchedule_authorizes_supervisor_across_both_shifts_in_assigned_buildings_only()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Day, Day, null)];

        Assert.True(ScheduleAuthority.CanManageSchedule(UserRole.Supervisor, 10, 1, Day, scope));
        Assert.False(ScheduleAuthority.CanManageSchedule(UserRole.Supervisor, 10, 2, Day, scope));
        // Inspection/MyBuilding authority remains single-shift:
        Assert.True(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day, scope));
        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Night, Day, scope));
    }

    [Fact]
    public void CanCorrectScheduleHistory_requires_building_scope_on_both_edited_and_business_dates()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Night, Day.AddDays(-7), Day), new(10, 2, Shift.Day, Day, null)];

        Assert.False(ScheduleAuthority.CanCorrectScheduleHistory(UserRole.Supervisor, 10, 1, Day.AddDays(-2), Day, scope));
        Assert.True(ScheduleAuthority.CanCorrectScheduleHistory(UserRole.Supervisor, 10, 2, Day, Day, scope));
    }

    [Fact]
    public void A_building_outside_the_assigned_scope_is_refused()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Day, Day, null), new(10, 2, Shift.Day, Day, null)];

        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 3, Shift.Day, Day, scope));
    }

    [Fact]
    public void Another_supervisors_period_is_not_authority()
    {
        ScopePeriod[] scope = [new(11, 1, Shift.Day, Day, null)];

        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day, scope));
    }

    [Fact]
    public void A_period_starts_on_its_effective_date_and_ends_before_its_until_date()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Day, Day, Day.AddDays(7))];

        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day.AddDays(-1), scope));
        Assert.True(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day, scope));
        Assert.True(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day.AddDays(6), scope));
        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day.AddDays(7), scope));
    }

    [Fact]
    public void An_expired_membership_grants_nothing_after_it_ends()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Day, Day.AddYears(-1), Day)];

        Assert.True(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day.AddDays(-1), scope));
        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day, scope));
        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day.AddMonths(1), scope));
    }

    [Fact]
    public void An_open_period_keeps_applying_until_it_is_closed()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Day, Day, null)];

        Assert.True(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day.AddYears(5), scope));
    }

    [Fact]
    public void No_periods_means_no_building()
    {
        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day, []));
    }

    [Theory]
    [InlineData(UserRole.Admin, 2, Shift.Night)]
    [InlineData(UserRole.Admin, 99, Shift.Day)]
    public void Admin_manages_every_building_and_shift(UserRole role, int buildingId, Shift shift) =>
        Assert.True(ScheduleAuthority.CanManage(role, 1, buildingId, shift, Day, []));

    [Fact]
    public void A_cleaner_is_never_authority_even_when_a_period_names_them()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Day, Day, null)];

        Assert.False(ScheduleAuthority.CanManage(UserRole.Cleaner, 10, 1, Shift.Day, Day, scope));
    }

    [Fact]
    public void Historical_membership_without_present_access_is_not_a_correction()
    {
        // The old building ended yesterday: the edited date is inside it, today is not
        ScopePeriod[] scope = [new(10, 1, Shift.Day, Day.AddYears(-1), Day), new(10, 2, Shift.Day, Day, null)];

        Assert.True(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day.AddDays(-10), scope));
        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day, scope));
        Assert.False(ScheduleAuthority.CanCorrectHistory(UserRole.Supervisor, 10, 1, Shift.Day, Day.AddDays(-10), Day, scope));
    }

    [Fact]
    public void A_current_holder_can_correct_a_date_inside_the_same_responsibility()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Day, Day.AddDays(-30), null)];

        Assert.True(ScheduleAuthority.CanCorrectHistory(UserRole.Supervisor, 10, 1, Shift.Day, Day, Day.AddDays(4), scope));
        Assert.True(ScheduleAuthority.CanCorrectHistory(UserRole.Supervisor, 10, 1, Shift.Day, Day.AddDays(-3), Day.AddDays(4), scope));
    }

    [Fact]
    public void A_date_before_the_responsibility_began_is_not_a_correction()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Day, Day.AddDays(-2), null)];

        Assert.False(ScheduleAuthority.CanCorrectHistory(UserRole.Supervisor, 10, 1, Shift.Day, Day.AddDays(-10), Day, scope));
    }

    [Fact]
    public void Admin_bypasses_the_scope_of_a_historical_correction_only()
    {
        // The bypass is the scope; every data-validity rule of the caller still applies to an Admin
        Assert.True(ScheduleAuthority.CanCorrectHistory(UserRole.Admin, 1, 7, Shift.Night, Day.AddYears(-1), Day, []));
    }

    [Fact]
    public void Covers_is_start_inclusive_and_end_exclusive()
    {
        var period = new ScopePeriod(10, 1, Shift.Day, Day, Day.AddDays(2));

        Assert.False(ScheduleAuthority.Covers(period, Day.AddDays(-1)));
        Assert.True(ScheduleAuthority.Covers(period, Day));
        Assert.True(ScheduleAuthority.Covers(period, Day.AddDays(1)));
        Assert.False(ScheduleAuthority.Covers(period, Day.AddDays(2)));
    }

    [Theory]
    // 2026-11-16 18:00 UTC is 01:00 Thai on the 17th: the night shift that started on the 16th is still running
    [InlineData(2026, 11, 16, 18, 0, 2026, 11, 16)]
    [InlineData(2026, 11, 17, 3, 0, 2026, 11, 17)]
    [InlineData(2026, 11, 17, 18, 59, 2026, 11, 17)]
    [InlineData(2026, 11, 17, 19, 0, 2026, 11, 17)]
    // 02:00 UTC on the 18th is 09:00 Thai, already the day shift that started on the 18th
    [InlineData(2026, 11, 18, 2, 0, 2026, 11, 18)]
    public void The_business_date_is_the_thai_shift_date(
        int year, int month, int day, int hour, int minute, int expectedYear, int expectedMonth, int expectedDay) =>
        Assert.Equal(
            new DateOnly(expectedYear, expectedMonth, expectedDay),
            ScheduleAuthority.BusinessDate(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc)));
}
