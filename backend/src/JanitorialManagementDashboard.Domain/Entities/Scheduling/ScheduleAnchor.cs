using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>
/// Spec §11: a stable per-person/per-slot row that a command locks before creating or changing an assignment. Two
/// writers that both see an empty slot still contend on this row, so both cannot commit a first create. It carries no
/// schedule fact itself.
/// </summary>
public class ScheduleAnchor
{
    public int Id { get; set; }

    public int PersonId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }

    public long Revision { get; set; } = 1;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Spec §11: the per-person head anchor that serialises empty-row creates and range edits of the person's baselines,
/// even when no baseline row exists yet.
/// </summary>
public class ScheduleBaselineAnchor
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    public long Revision { get; set; } = 1;
    public DateTime UpdatedAt { get; set; }
}
