namespace FacilityRealtime.Domain.Enums;

/// <summary>
/// database.html alert_reviews.alert_type: the alerts an Admin closes with "คุยแล้ว ปิดเตือน" (facility-0035, 0041).
/// A missing Shift-Out is not one: it closes itself when the Admin fills the time (facility-0051). INSPECTION_GPS comes with the Supervisor plan.
/// </summary>
public enum AlertType
{
    AttendanceGps,
    SubmissionGps,
    BlockedScan,
}
