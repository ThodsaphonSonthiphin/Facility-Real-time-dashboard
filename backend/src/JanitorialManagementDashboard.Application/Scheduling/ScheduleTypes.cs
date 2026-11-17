using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>
/// ADR facility-0183: a shift is dated by the day it started, so a night shift still running after midnight keeps
/// its Sunday date.
/// </summary>
public sealed record ScheduleSlot(DateOnly ShiftDate, Shift Shift);

/// <summary>
/// ADRs facility-0173 and facility-0165: one dated cell of a person's week, unset when no choice exists yet, with
/// its revision for stale-write rejection.
/// </summary>
public sealed record ScheduleCell(int UserId, DateOnly Date, int? AreaId, int? BreakGroup,
    bool IsDayOff, bool IsLeave, bool IsUnset, string Revision);

/// <summary>
/// ADRs facility-0140 and facility-0142: one Supervisor's authority over a building and shift from a date until
/// cleared.
/// </summary>
public sealed record ScopePeriod(int SupervisorId, int BuildingId, Shift Shift, DateOnly From, DateOnly? Until);
