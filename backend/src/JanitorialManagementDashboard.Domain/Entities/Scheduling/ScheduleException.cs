using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>ADRs facility-0134, 0145, 0157, 0189, 0202 and 0214: dated Area, break-group and leave records.</summary>
public enum ScheduleExceptionKind
{
    Area,
    BreakGroup,
    Leave,
}

/// <summary>
/// ADRs facility-0134, 0145, 0157, 0189, 0202 and 0214: one dated override for a person's slot. An Area or break-group
/// row replaces the baseline for that date only; a leave row stores an agreed interval (possibly partial) and keeps
/// actual scans untouched. Cancelling keeps the row and its history instead of deleting it.
/// </summary>
public class ScheduleException
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public ScheduleExceptionKind Kind { get; set; }

    /// <summary>The Thai start date of the shift this record applies to (facility-0183).</summary>
    public DateOnly ShiftDate { get; set; }

    public Shift Shift { get; set; }

    /// <summary>Set for an Area override only; a CHECK enforces the kind's shape.</summary>
    public int? AreaId { get; set; }

    /// <summary>Set for a first-break override only; a CHECK enforces the kind's shape.</summary>
    public int? BreakGroup { get; set; }

    /// <summary>Set for leave only, in UTC. A partial-shift leave keeps the same ShiftDate.</summary>
    public DateTime? StartsAt { get; set; }

    /// <summary>Set for leave only, exclusive in UTC.</summary>
    public DateTime? EndsAt { get; set; }

    public bool IsCancelled { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? CancelledById { get; set; }

    public long Revision { get; set; } = 1;

    /// <summary>Required for a historical correction (facility-0214); optional on an original future record.</summary>
    public string? Reason { get; set; }

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
