namespace JanitorialManagementDashboard.Application.Attendance;

/// <summary>
/// Spec §8, row "จังหวะพักหลัง final departure" (ADR facility-0222): what an action that has no recorded event is right now.
/// Not-applicable is its own state, different from a missing scan, and it is derived — no actual event is ever written for it.
/// </summary>
public enum AttendanceApplicabilityStatus
{
    /// <summary>The expected instant has not arrived, so nothing is missing yet.</summary>
    Pending,

    /// <summary>The expected instant has arrived and there is no scan and no applicable leave covering it.</summary>
    Missing,

    /// <summary>A recorded departure and recorded applicable leave make the action unnecessary.</summary>
    NotApplicable,
}

/// <summary>
/// Spec §8 with §7 (ADR facility-0222): the leave exemption for a scan that will never happen. A half-day departure exempts
/// only the actions it actually steps over — <c>ถ้า expected instant ของ action อยู่หลัง/เท่ากับ actual departure และอยู่ใน
/// recorded applicable leave ให้ not-applicable เนื่องจากลา; ไม่สร้าง actual event</c> — while a scan forgotten before that
/// departure stays missing, <c>คง missing/skipped ไม่ถูกล้างเพราะมีลา</c>.
/// </summary>
public static class AttendanceApplicability
{
    /// <summary>
    /// Spec §8: true when the action's expected instant is at or after the recorded departure and inside the recorded leave
    /// interval. Departure and leave are both required, so an early checkout with no leave never becomes an exemption.
    /// </summary>
    public static bool NotApplicable(DateTimeOffset expectedAt, DateTimeOffset actualOut,
        DateTimeOffset leaveStart, DateTimeOffset leaveEnd) =>
        expectedAt >= actualOut && expectedAt >= leaveStart && expectedAt < leaveEnd;

    /// <summary>
    /// Spec §8: classifies an action that has no recorded event. Not-applicable needs a departure and a leave interval and
    /// the predicate above; anything else is missing once <paramref name="nowUtc"/> has reached
    /// <paramref name="expectedAt"/>, and still pending before that. Deriving only — it never inserts an event.
    /// </summary>
    public static AttendanceApplicabilityStatus Classify(DateTimeOffset expectedAt, DateTimeOffset nowUtc,
        DateTimeOffset? actualOut, DateTimeOffset? leaveStart, DateTimeOffset? leaveEnd)
    {
        if (actualOut is { } departure && leaveStart is { } start && leaveEnd is { } end
            && NotApplicable(expectedAt, departure, start, end))
        {
            return AttendanceApplicabilityStatus.NotApplicable;
        }

        return nowUtc >= expectedAt ? AttendanceApplicabilityStatus.Missing : AttendanceApplicabilityStatus.Pending;
    }
}
