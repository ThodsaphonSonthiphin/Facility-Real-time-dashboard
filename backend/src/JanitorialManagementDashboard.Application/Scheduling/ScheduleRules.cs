namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>ADRs facility-0193 and facility-0210: the pure week calendar behind copying previous-week day-off choices.</summary>
public static class ScheduleRules
{
    /// <summary>ISO week start, Monday. ADR facility-0183: pass the person's dated slot, not the wall-clock date.</summary>
    public static DateOnly WeekStart(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>
    /// ADRs facility-0193 and facility-0210: each target person keeps a saved/local date; a null fills from that
    /// person's source weekday in the target week. Source-only people are never imported and a missing source stays null.
    /// </summary>
    public static IReadOnlyDictionary<int, DateOnly?> FillUnset(
        IReadOnlyDictionary<int, DateOnly?> target,
        IReadOnlyDictionary<int, DateOnly?> source, DateOnly targetWeek)
    {
        var monday = WeekStart(targetWeek);
        return target.ToDictionary(x => x.Key, x => x.Value ??
            (source.TryGetValue(x.Key, out var previous) && previous is { } day
                ? monday.AddDays(((int)day.DayOfWeek + 6) % 7) : (DateOnly?)null));
    }
}
