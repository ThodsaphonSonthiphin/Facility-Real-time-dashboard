using FacilityRealtime.Application.Shifts;

namespace FacilityRealtime.Application.Rounds;

/// <summary>A Round Window placed on one shift's timeline: Thai Start/End as entered, and the UTC instants they mean in that shift.</summary>
public sealed record RoundWindow(int Id, TimeOnly Start, TimeOnly End, DateTime StartUtc, DateTime EndUtc)
{
    public static RoundWindow For(int id, TimeOnly start, TimeOnly end, ShiftSlot slot) =>
        new(id, start, end, ShiftCalendar.ToUtc(slot, start), ShiftCalendar.ToUtc(slot, end));
}
