using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Inspections;

/// <summary>facility-0048 rule 2 and its 2026-10-07 amendment: where and when a Supervisor inspects without a cover.</summary>
public static class SupervisorScope
{
    /// <summary>
    /// Null when the point is in the Supervisor's own building and the clock is in their own shift. Otherwise the reason a
    /// scan without a Cover Assignment is blocked: the building is checked first, so another building at night is OtherBuilding.
    /// </summary>
    public static BlockReason? WithoutCover(int? ownBuildingId, Shift? ownShift, int pointBuildingId, Shift clockShift) =>
        ownBuildingId != pointBuildingId ? BlockReason.OtherBuilding
        : ownShift != clockShift ? BlockReason.OutsideShift
        : null;
}
