using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>ADRs facility-0137, 0143 and 0215: a regular Cleaner has an Area, a relief Cleaner has a home building.</summary>
public enum CleanerKind
{
    Regular,
    Relief,
}

/// <summary>
/// ADRs facility-0137, 0143, 0194 and 0215: the dated regular/relief identity of a person. The mutable User.AreaId,
/// User.BuildingId and User.Shift columns remain compatibility projections for legacy paths; they are not history.
/// </summary>
public class CleanerProfilePeriod
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public CleanerKind Kind { get; set; }

    /// <summary>The relief Cleaner's home building (facility-0194); known for a regular Cleaner through their Area.</summary>
    public int? HomeBuildingId { get; set; }

    /// <summary>The Cleaner's shift, or null when a Regular Cleaner awaits Supervisor shift assignment.</summary>
    public Shift? Shift { get; set; }

    /// <summary>The first date the profile applies to (inclusive).</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>The first date it no longer applies to (exclusive); null while it stays open.</summary>
    public DateOnly? EffectiveUntil { get; set; }

    public long Revision { get; set; } = 1;

    /// <summary>Null only for the compatibility backfill, which has no human actor (spec §14).</summary>
    public int? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
