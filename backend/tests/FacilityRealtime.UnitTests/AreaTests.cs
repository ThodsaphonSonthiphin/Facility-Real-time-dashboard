using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0040 rule 4 / 0042 rule 4: a day-only Area is Off Hours at night, and so is a deactivated Area.</summary>
public class AreaTests
{
    [Theory]
    [InlineData(ShiftPattern.DayAndNight, true, Shift.Day, true)]
    [InlineData(ShiftPattern.DayAndNight, true, Shift.Night, true)]
    [InlineData(ShiftPattern.DayOnly, true, Shift.Day, true)]
    [InlineData(ShiftPattern.DayOnly, true, Shift.Night, false)]
    [InlineData(ShiftPattern.DayAndNight, false, Shift.Day, false)]
    [InlineData(ShiftPattern.DayAndNight, false, Shift.Night, false)]
    [InlineData(ShiftPattern.DayOnly, false, Shift.Day, false)]
    [InlineData(ShiftPattern.DayOnly, false, Shift.Night, false)]
    public void Area_works_a_shift_only_when_active_and_its_pattern_includes_it(ShiftPattern pattern, bool isActive, Shift shift, bool expected)
    {
        var area = new Area { ShiftPattern = pattern, IsActive = isActive };

        Assert.Equal(expected, area.HasShift(shift));
    }
}
