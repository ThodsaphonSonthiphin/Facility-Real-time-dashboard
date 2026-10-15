using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Alerts;

/// <summary>The kinds of line on the alert list (wireframe D3).</summary>
public enum AlertKind
{
    /// <summary>A scan refused for another Area (Cleaner) or another building or shift (Supervisor) (facility-0041, 0048).</summary>
    BlockedScan,

    /// <summary>An attendance scan outside its sign's radius: recorded and flagged (facility-0035).</summary>
    AttendanceGps,

    /// <summary>A submission outside its sign's radius: recorded and flagged (facility-0037).</summary>
    SubmissionGps,

    /// <summary>A Cleaner who checked in has no Shift-Out, or no Break-In after a Break-Out, once the shift ended (facility-0051).</summary>
    MissingAttendance,

    /// <summary>An inspection outside its sign's radius: recorded and flagged (facility-0037).</summary>
    InspectionGps,
}

public static class AlertKinds
{
    /// <summary>What alert_reviews stores when the Admin closes it; null for MissingAttendance, which closes once the Admin fills the time.</summary>
    public static AlertType? ReviewType(AlertKind kind) => kind switch
    {
        AlertKind.BlockedScan => AlertType.BlockedScan,
        AlertKind.AttendanceGps => AlertType.AttendanceGps,
        AlertKind.SubmissionGps => AlertType.SubmissionGps,
        AlertKind.InspectionGps => AlertType.InspectionGps,
        _ => null,
    };
}
