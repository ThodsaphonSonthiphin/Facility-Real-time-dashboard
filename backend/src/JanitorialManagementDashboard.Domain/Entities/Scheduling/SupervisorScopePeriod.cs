using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>
/// ADRs facility-0140, 0142 and 0215: one Supervisor's regular responsibility for a building and shift during a dated
/// range. A Supervisor may hold several buildings, but a building and shift has one regular owner at a time; the
/// Infrastructure store checks overlap under the building/shift anchor because MySQL and SQLite cannot express an
/// interval-exclusion constraint.
/// </summary>
public class SupervisorScopePeriod
{
    public int Id { get; set; }

    public int SupervisorId { get; set; }
    public int BuildingId { get; set; }
    public Shift Shift { get; set; }

    /// <summary>The first date the responsibility applies to (inclusive).</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>The first date it no longer applies to (exclusive); null while it stays open.</summary>
    public DateOnly? EffectiveUntil { get; set; }

    public long Revision { get; set; } = 1;

    /// <summary>Null only for the compatibility backfill, which has no human actor (spec §14).</summary>
    public int? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
