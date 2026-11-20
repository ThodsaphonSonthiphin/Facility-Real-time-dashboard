namespace JanitorialManagementDashboard.Domain.Enums;

/// <summary>
/// facility-0051: Admin additions and edits carry a badge; the original values live in audit_log. Spec §8 adds
/// <see cref="SupervisorAdd"/> for a Supervisor's scoped missing-time entry — a confirmed fact with a reason, never an
/// overwrite of a scan — without renaming the three values already stored as strings.
/// </summary>
public enum AttendanceSource
{
    Scan,
    AdminAdd,
    AdminEdit,

    /// <summary>A Supervisor inside their dated scope filled a missing actual time with a reason (spec §8, §11).</summary>
    SupervisorAdd,
}
