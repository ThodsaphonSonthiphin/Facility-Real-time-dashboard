using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Attendance;

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
    public static AreaAttendanceState StateOf(
        IReadOnlyCollection<AttendanceEvent> regularCleanerEvents, bool hasCover) =>
        regularCleanerEvents.Contains(AttendanceEvent.ShiftOut) ? AreaAttendanceState.Done
        : IsOnBreak(regularCleanerEvents) ? AreaAttendanceState.OnBreak
        : regularCleanerEvents.Contains(AttendanceEvent.ShiftIn) ? AreaAttendanceState.Working
        : hasCover ? AreaAttendanceState.Covered
        : AreaAttendanceState.Empty;

    /// <summary>
    /// Spec §13 and ADR facility-0216 (Task 11): the entry category of one Area, from the shared actual-entry evidence and
    /// whether anybody is assigned to it. The three categories keep the target denominator, and evidence always beats an
    /// assignment, so a live cover or a transfer instruction cannot hide that somebody really entered.
    /// </summary>
    public static string EntryOf(bool hasEntryEvidence, bool isAssigned) =>
        ScheduleEntryEvidence.EntryOf(hasEntryEvidence, isAssigned);

    private static bool IsOnBreak(IReadOnlyCollection<AttendanceEvent> events) =>
        AttendanceLifecycle.Order.LastOrDefault(action => AttendanceLifecycle.IsSkippable(action) && events.Contains(action))
            is AttendanceEvent.Break1Out or AttendanceEvent.Break2Out;
}
