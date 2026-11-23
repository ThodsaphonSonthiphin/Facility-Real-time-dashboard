using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>
/// Spec §3 (ADRs facility-0140, 0142 and 0215): the one authority rule every management, work and inspection path shares.
/// An Admin manages every building and shift; a Supervisor only the buildings a dated <see cref="ScopePeriod"/> gives
/// them for that shift. This is a pure rule over periods the caller has loaded: it never reads the mutable
/// <c>User.BuildingId</c>/<c>User.Shift</c> projection and it holds no activation state, so the scoped reader decides
/// whether an account is current before asking (spec §14 keeps those fields as compatibility projections only).
/// </summary>
public static class ScheduleAuthority
{
    /// <summary>True when the actor may manage this building and shift on <paramref name="date"/>. Admin never needs periods.</summary>
    public static bool CanManage(
        UserRole role,
        int actorId,
        int buildingId,
        Shift shift,
        DateOnly date,
        IReadOnlyCollection<ScopePeriod> periods) =>
        role == UserRole.Admin ||
        (role == UserRole.Supervisor && periods.Any(p =>
            p.SupervisorId == actorId && p.BuildingId == buildingId && p.Shift == shift &&
            p.From <= date && (p.Until is null || date < p.Until.Value)));

    /// <summary>
    /// Spec §3: a Supervisor's correction of a past date needs the responsibility of the edited date *and* the
    /// responsibility they still hold today, so a newly received building never reaches backwards. Admin bypasses the
    /// scope, never the caller's data-validity checks.
    /// </summary>
    public static bool CanCorrectHistory(
        UserRole role,
        int actorId,
        int buildingId,
        Shift shift,
        DateOnly editedDate,
        DateOnly businessDate,
        IReadOnlyCollection<ScopePeriod> periods) =>
        CanManage(role, actorId, buildingId, shift, editedDate, periods) &&
        CanManage(role, actorId, buildingId, shift, businessDate, periods);

    /// <summary>
    /// ADR facility-0240: scheduling authority covers both Day and Night shifts within the Supervisor's dated building(s).
    /// Inspection and /my-building continue to use <see cref="CanManage"/>.
    /// </summary>
    public static bool CanManageSchedule(
        UserRole role,
        int actorId,
        int buildingId,
        DateOnly date,
        IReadOnlyCollection<ScopePeriod> periods) =>
        role == UserRole.Admin ||
        (role == UserRole.Supervisor && periods.Any(p =>
            p.SupervisorId == actorId && p.BuildingId == buildingId &&
            p.From <= date && (p.Until is null || date < p.Until.Value)));

    public static bool CanCorrectScheduleHistory(
        UserRole role,
        int actorId,
        int buildingId,
        DateOnly editedDate,
        DateOnly businessDate,
        IReadOnlyCollection<ScopePeriod> periods) =>
        CanManageSchedule(role, actorId, buildingId, editedDate, periods) &&
        CanManageSchedule(role, actorId, buildingId, businessDate, periods);

    /// <summary>The period's own range: the first date included, the until date excluded (null stays open).</summary>
    public static bool Covers(ScopePeriod period, DateOnly date) =>
        period.From <= date && (period.Until is null || date < period.Until.Value);

    /// <summary>
    /// The date a membership write takes effect on: the Thai shift date of the business clock, the same date the
    /// endpoints already derive as <c>slot.ShiftDate</c>, so 01:00 on the 18th still belongs to the 17th's night shift.
    /// </summary>
    public static DateOnly BusinessDate(DateTime utc) => ShiftCalendar.SlotAt(utc).ShiftDate;
}
