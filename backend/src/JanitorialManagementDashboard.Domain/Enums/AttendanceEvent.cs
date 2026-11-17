namespace JanitorialManagementDashboard.Domain.Enums;

/// <summary>
/// CONTEXT.md, Attendance Event: the scans of a shift — the four legacy ones (facility-0031) and, for a numbered slot only,
/// the two explicit break cycles (ADR facility-0222). The numbered members are appended, so the four legacy names and
/// numeric values never shift and the four legacy stored strings keep their meaning.
/// </summary>
public enum AttendanceEvent
{
    ShiftIn,
    BreakOut,
    BreakIn,
    ShiftOut,

    // Spec §8: new = ShiftIn/Break1Out/Break1In/Break2Out/Break2In/ShiftOut, stored as BREAK1_OUT and so on.
    Break1Out,
    Break1In,
    Break2Out,
    Break2In,
}
