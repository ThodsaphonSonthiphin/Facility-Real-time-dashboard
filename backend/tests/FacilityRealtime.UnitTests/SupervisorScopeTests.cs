using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

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
