using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
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
    /// <summary>
    /// A regular Cleaner who came decides the state; otherwise a cover makes the Area Covered, else it is Empty. Without a
    /// slot's lifecycle this is the legacy four-action answer, which is byte-identical to what the page has always shown.
    /// </summary>
    public static AreaAttendanceState StateOf(IReadOnlyCollection<AttendanceEvent> regularCleanerEvents, bool hasCover) =>
        StateOf(regularCleanerEvents, hasCover, AttendanceLifecycleVersion.Legacy4);

    /// <summary>
    /// Spec §8 with the Task 9 review ruling: the state is read against the slot's own lifecycle. A numbered slot is on break
    /// while a break has gone out and its own return has not come back — <c>Break1Out</c> without <c>Break1In</c>, or
    /// <c>Break2Out</c> without <c>Break2In</c> — exactly as the legacy <c>BreakOut</c> without <c>BreakIn</c> answers today,
    /// so a numbered break never reads as "Working". A Legacy4 slot keeps the legacy four-action behaviour unchanged.
    /// </summary>
    public static AreaAttendanceState StateOf(
        IReadOnlyCollection<AttendanceEvent> regularCleanerEvents, bool hasCover, AttendanceLifecycleVersion version) =>
        regularCleanerEvents.Contains(AttendanceEvent.ShiftOut) ? AreaAttendanceState.Done
        : IsOnBreak(regularCleanerEvents, version) ? AreaAttendanceState.OnBreak
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

    /// <summary>A break that went out without its matching return; each numbered break is answered by its own pair.</summary>
    private static bool IsOnBreak(IReadOnlyCollection<AttendanceEvent> events, AttendanceLifecycleVersion version) =>
        version == AttendanceLifecycleVersion.Numbered6
            ? (events.Contains(AttendanceEvent.Break1Out) && !events.Contains(AttendanceEvent.Break1In))
                || (events.Contains(AttendanceEvent.Break2Out) && !events.Contains(AttendanceEvent.Break2In))
            : events.Contains(AttendanceEvent.BreakOut) && !events.Contains(AttendanceEvent.BreakIn);
}
