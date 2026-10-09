using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Attendance;

/// <summary>facility-0031: the four events in order. facility-0051: a forgotten Break-In stays empty, so Shift-Out may follow Break-Out.</summary>
public static class AttendanceRules
{
    public static IReadOnlyList<AttendanceEvent> NextAllowed(IReadOnlyCollection<AttendanceEvent> recorded) =>
        recorded.Contains(AttendanceEvent.ShiftOut) ? []
        : recorded.Contains(AttendanceEvent.BreakIn) ? [AttendanceEvent.ShiftOut]
        : recorded.Contains(AttendanceEvent.BreakOut) ? [AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut]
        : recorded.Contains(AttendanceEvent.ShiftIn) ? [AttendanceEvent.BreakOut, AttendanceEvent.ShiftOut]
        : [AttendanceEvent.ShiftIn];

    /// <summary>Owner decision 2026-10-09: a Cleaner may submit from Shift-In until Shift-Out; a break does not stop it.</summary>
    public static bool IsOnDuty(IReadOnlyCollection<AttendanceEvent> recorded) =>
        recorded.Contains(AttendanceEvent.ShiftIn) && !recorded.Contains(AttendanceEvent.ShiftOut);
}
