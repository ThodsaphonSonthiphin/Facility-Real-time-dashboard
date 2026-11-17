namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>ADRs facility-0151, 0152 and 0220: regular and relief patterns have different editing rules.</summary>
public enum BaselineKind
{
    Regular,
    Relief,
}

/// <summary>
/// ADRs facility-0151, 0173, 0187, 0189 and 0215: one immutable version of a person's repeating week. A later version
/// starts on a selected date; the earlier version keeps its own range, so past days never change retroactively.
/// </summary>
public class ScheduleBaseline
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public BaselineKind Kind { get; set; }

    /// <summary>The first date this version applies to (inclusive).</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>The first date it no longer applies to (exclusive); null for the latest version.</summary>
    public DateOnly? EffectiveUntil { get; set; }

    public long Revision { get; set; } = 1;

    /// <summary>Null only for the compatibility backfill, which has no human actor (spec §14).</summary>
    public int? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ScheduleBaselineDay> Days { get; set; } = [];
}

/// <summary>
/// One weekday cell of a baseline version. Null Area with IsDayOff false means "not chosen" (facility-0173); a working
/// relief row needs an Area, while a day-off cell has neither Area nor break group.
/// </summary>
public class ScheduleBaselineDay
{
    public int Id { get; set; }

    public int BaselineId { get; set; }
    public DayOfWeek Weekday { get; set; }

    /// <summary>Null for a day off or an unset cell.</summary>
    public int? AreaId { get; set; }

    /// <summary>The first-break group (1 or 2); null when no group has been confirmed for this cell.</summary>
    public int? BreakGroup { get; set; }

    public bool IsDayOff { get; set; }

    public ScheduleBaseline? Baseline { get; set; }
}
