using JanitorialManagementDashboard.Application.Inspections;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.UnitTests;

/// <summary>facility-0048 and its 2026-10-07 amendment: own building, own shift by the clock; anything else needs a cover.</summary>
public class SupervisorScopeTests
{
    [Theory]
    [InlineData(1, Shift.Day, 1, Shift.Day, null)]
    [InlineData(1, Shift.Night, 1, Shift.Night, null)]
    [InlineData(1, Shift.Day, 2, Shift.Day, BlockReason.OtherBuilding)]
    [InlineData(1, Shift.Day, 1, Shift.Night, BlockReason.OutsideShift)]
    [InlineData(1, Shift.Day, 2, Shift.Night, BlockReason.OtherBuilding)]
    public void Own_building_in_own_shift_needs_no_cover(int ownBuilding, Shift ownShift, int pointBuilding, Shift clockShift, BlockReason? expected) =>
        Assert.Equal(expected, SupervisorScope.WithoutCover(ownBuilding, ownShift, pointBuilding, clockShift));

    [Fact]
    public void A_supervisor_without_a_building_is_in_no_building() =>
        Assert.Equal(BlockReason.OtherBuilding, SupervisorScope.WithoutCover(null, Shift.Day, 1, Shift.Day));
}

/// <summary>
/// Spec §3: the dated scope decides where a Supervisor inspects without a cover. Every assigned building, in the clock's
/// shift only; the temporary cover and the clock-window rules stay exactly as they were (facility-0048).
/// </summary>
public class SupervisorScopePeriodTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);

    private static ScopePeriod[] DayScope() =>
        [new(20, 1, Shift.Day, Today.AddDays(-10), null), new(20, 2, Shift.Day, Today.AddDays(-10), null)];

    [Theory]
    [InlineData(1, Shift.Day, null)]
    [InlineData(2, Shift.Day, null)]
    [InlineData(1, Shift.Night, BlockReason.OutsideShift)]
    [InlineData(3, Shift.Day, BlockReason.OtherBuilding)]
    [InlineData(3, Shift.Night, BlockReason.OtherBuilding)]
    public void Every_assigned_building_in_the_clock_shift_needs_no_cover(int pointBuilding, Shift clockShift, BlockReason? expected) =>
        Assert.Equal(expected, SupervisorScope.WithoutCover(DayScope(), 20, pointBuilding, clockShift, Today));

    [Fact]
    public void A_building_read_off_the_projection_is_no_authority_when_dated_scope_exists()
    {
        // Building 9 is the account's current User.BuildingId, but the dated rows say 1 and 2
        Assert.Equal(BlockReason.OtherBuilding, SupervisorScope.WithoutCover(DayScope(), 20, 9, Shift.Day, Today));
    }

    [Fact]
    public void An_expired_membership_no_longer_needs_no_cover()
    {
        ScopePeriod[] scope = [new(20, 1, Shift.Day, Today.AddDays(-10), Today)];

        Assert.Equal(BlockReason.OtherBuilding, SupervisorScope.WithoutCover(scope, 20, 1, Shift.Day, Today));
        Assert.Null(SupervisorScope.WithoutCover(scope, 20, 1, Shift.Day, Today.AddDays(-1)));
    }

    [Fact]
    public void Another_supervisors_building_is_blocked() =>
        Assert.Equal(BlockReason.OtherBuilding, SupervisorScope.WithoutCover([new(21, 1, Shift.Day, Today, null)], 20, 1, Shift.Day, Today));

    [Fact]
    public void No_dated_scope_blocks_every_building() =>
        Assert.Equal(BlockReason.OtherBuilding, SupervisorScope.WithoutCover([], 20, 1, Shift.Day, Today));
}
