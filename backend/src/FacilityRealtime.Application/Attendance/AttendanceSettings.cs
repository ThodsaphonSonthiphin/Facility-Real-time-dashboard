namespace FacilityRealtime.Application.Attendance;

/// <summary>The "Attendance" config section (facility-0069, facility-0031).</summary>
public sealed class AttendanceSettings
{
    public const string SectionName = "Attendance";

    public int OpensMinutesBeforeShift { get; set; } = 60;
    public int ClosesMinutesAfterShift { get; set; } = 60;

    /// <summary>A different attendance event within this many minutes of the latest one is ignored.</summary>
    public int RepeatIgnoreMinutes { get; set; } = 5;
}
