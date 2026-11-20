using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Inspections;

/// <summary>facility-0048 rule 2 and its 2026-10-07 amendment: where and when a Supervisor inspects without a cover.</summary>
public static class SupervisorScope
{
    /// <summary>How much of a building's scope the Supervisor holds on the date, for the answer and the Blocked Scan reason.</summary>
    private enum Hold
    {
        None,
        OtherShiftOnly,
        ThisShift,
    }

    /// <summary>
    /// Null when the point is in one of the Supervisor's own buildings (spec §3: several are possible) and the clock is in
    /// the shift they hold it for. Otherwise the reason a scan without a Cover Assignment is blocked: the building is
    /// checked first, so another building at night is OtherBuilding.
    /// </summary>
    public static BlockReason? WithoutCover(
        IReadOnlyCollection<ScopePeriod> periods, int supervisorId, int pointBuildingId, Shift clockShift, DateOnly date) =>
        HoldOf(periods, supervisorId, pointBuildingId, clockShift, date) switch
        {
            Hold.ThisShift => null,
            Hold.OtherShiftOnly => BlockReason.OutsideShift,
            _ => BlockReason.OtherBuilding,
        };

    private static Hold HoldOf(
        IReadOnlyCollection<ScopePeriod> periods, int supervisorId, int buildingId, Shift clockShift, DateOnly date)
    {
        var held = periods
            .Where(p => p.SupervisorId == supervisorId && p.BuildingId == buildingId && ScheduleAuthority.Covers(p, date))
            .Select(p => p.Shift)
            .ToList();
        return held.Contains(clockShift) ? Hold.ThisShift
            : held.Count == 0 ? Hold.None
            : Hold.OtherShiftOnly;
    }

    /// <summary>
    /// The legacy answer from the single-building projection <c>User.BuildingId</c>/<c>User.Shift</c>. Kept for paths that
    /// still read that projection (the Blocked Scan alert list); every authority decision uses the dated overload above.
    /// </summary>
    public static BlockReason? WithoutCover(int? ownBuildingId, Shift? ownShift, int pointBuildingId, Shift clockShift) =>
        ownBuildingId != pointBuildingId ? BlockReason.OtherBuilding
        : ownShift != clockShift ? BlockReason.OutsideShift
        : null;
}
