using System;

namespace FacilityRealtime.Application.Common;

/// <summary>
/// Working Hours: one value for the whole system (ADR facility-0005), bound from the
/// "WorkingHours" section of appsettings.json. Times are Asia/Bangkok wall-clock.
/// Outside this window every point shows Off Hours unless its latest scan is an Issue.
/// </summary>
public sealed class WorkingHours
{
    public const string SectionName = "WorkingHours";

    public TimeOnly Start { get; set; } = new(8, 0);
    public TimeOnly End { get; set; } = new(17, 0);
}
