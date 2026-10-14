using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Attendance;

/// <summary>The status column of the attendance page (wireframe D2). facility-0050: whether someone is in the Area, never late or early.</summary>
public enum AreaAttendanceState
{
    /// <summary>The regular Cleaner has not checked in and nobody covers: the Admin needs to find a cover (facility-0041).</summary>
    Empty,

    Working,

    OnBreak,

    /// <summary>The regular Cleaner scanned Shift-Out.</summary>
    Done,

    /// <summary>The regular Cleaner has not checked in, and the Admin assigned a cover for this shift.</summary>
    Covered,
}

public static class AreaAttendance
{
    /// <summary>A regular Cleaner who came decides the state; otherwise a cover makes the Area Covered, else it is Empty.</summary>
    public static AreaAttendanceState StateOf(IReadOnlyCollection<AttendanceEvent> regularCleanerEvents, bool hasCover) =>
        regularCleanerEvents.Contains(AttendanceEvent.ShiftOut) ? AreaAttendanceState.Done
        : regularCleanerEvents.Contains(AttendanceEvent.BreakOut) && !regularCleanerEvents.Contains(AttendanceEvent.BreakIn) ? AreaAttendanceState.OnBreak
        : regularCleanerEvents.Contains(AttendanceEvent.ShiftIn) ? AreaAttendanceState.Working
        : hasCover ? AreaAttendanceState.Covered
        : AreaAttendanceState.Empty;
}
