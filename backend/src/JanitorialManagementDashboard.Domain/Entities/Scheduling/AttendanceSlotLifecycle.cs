using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>ADR facility-0222: the four legacy actions keep their meaning; a new slot uses the numbered six.</summary>
public enum AttendanceLifecycleVersion
{
    Legacy4,
    Numbered6,
}

/// <summary>
/// ADR facility-0222: the chosen attendance action set for one dated slot. The row must be activated before the
/// receiving window opens (Task 8 checks that precondition); a slot with recorded legacy facts is never reinterpreted.
/// </summary>
public class AttendanceSlotLifecycle
{
    public int Id { get; set; }

    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }

    public AttendanceLifecycleVersion Version { get; set; }

    public DateTime ActivatedAt { get; set; }

    /// <summary>Null when a deployment/operator setting chose the boundary rather than a signed-in person.</summary>
    public int? ActivatedById { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }
}
