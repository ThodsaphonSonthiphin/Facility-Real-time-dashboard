using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>ADR facility-0222: an explicit skip is stored; later filling the missing action resolves it.</summary>
public enum AttendanceGapStatus
{
    Skipped,
    Resolved,
}

/// <summary>
/// ADR facility-0222 and spec §8: the explicit, stored outcome for an expected action that has no actual event. A gap
/// is never a guessed time and never counts as scanned evidence. Skipping an intermediate break records it; a later
/// authorised correction stores the real event and resolves the gap.
/// </summary>
public class AttendanceGap
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }

    /// <summary>The expected action that is missing.</summary>
    public AttendanceEvent EventType { get; set; }

    public AttendanceGapStatus Status { get; set; } = AttendanceGapStatus.Skipped;

    /// <summary>The eligible underlying Area when the gap was recorded, so a later Area change cannot rewrite it.</summary>
    public int? AreaId { get; set; }

    public string? Reason { get; set; }

    /// <summary>Null when the system resolved the gap after a corrected actual event; otherwise the actor who recorded it.</summary>
    public int? RecordedById { get; set; }

    /// <summary>When the skip or resolution was recorded, in UTC.</summary>
    public DateTime? RecordedAt { get; set; }

    public long Revision { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
}
