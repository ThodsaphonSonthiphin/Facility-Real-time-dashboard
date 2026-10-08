using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Area: one Cleaner per shift, one Check-In Sign, N Service Points (facility-0040).</summary>
public class Area
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ShiftPattern ShiftPattern { get; set; } = ShiftPattern.DayAndNight;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Building? Building { get; set; }

    /// <summary>False for a deactivated Area, and for a day-only Area during the night shift: its points are Off Hours.</summary>
    public bool HasShift(Shift shift) => IsActive && (shift == Shift.Day || ShiftPattern == ShiftPattern.DayAndNight);
}
