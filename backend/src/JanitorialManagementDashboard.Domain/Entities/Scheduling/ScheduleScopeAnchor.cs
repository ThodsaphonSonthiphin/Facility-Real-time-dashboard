using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>
/// Spec §11 and ADRs facility-0140/0142: the stable building/shift row that serialises regular Supervisor period edits,
/// including overlapping future ranges claimed by different people.
/// </summary>
public class ScheduleScopeAnchor
{
    public int Id { get; set; }

    public int BuildingId { get; set; }
    public Shift Shift { get; set; }

    public long Revision { get; set; } = 1;
    public DateTime UpdatedAt { get; set; }
}
