namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>
/// ADRs facility-0190, 0192, 0193 and 0217: one person's day-off choice for one ISO week. A missing header means the
/// person was never asked (unset); a header with no choice rows is a confirmed empty choice. A null date never stands
/// for "no choice", so a later copy can fill only genuinely unset people.
/// </summary>
public class WeeklyDayOff
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>The Monday of the chosen week.</summary>
    public DateOnly WeekStart { get; set; }

    public long Revision { get; set; } = 1;

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<WeeklyDayOffChoice> Choices { get; set; } = [];
}

/// <summary>One selected day off inside a saved week; the row's existence is the selection.</summary>
public class WeeklyDayOffChoice
{
    public int Id { get; set; }

    public int WeeklyDayOffId { get; set; }

    /// <summary>A date inside the header's week, stored instead of a weekday so a different weekday can be chosen each week.</summary>
    public DateOnly OffDate { get; set; }

    public WeeklyDayOff? WeeklyDayOff { get; set; }
}
