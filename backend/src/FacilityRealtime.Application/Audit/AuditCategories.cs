namespace FacilityRealtime.Application.Audit;

/// <summary>The "ประเภท" filter of the Admin history page (wireframe D9, facility-0057).</summary>
public enum AuditCategory
{
    Attendance,
    Cover,
    AlertReview,
    Setup,
    QrToken,
    Account,
    Export,
}

public static class AuditCategories
{
    private static readonly Dictionary<AuditCategory, string[]> Actions = new()
    {
        [AuditCategory.Attendance] = ["ATTENDANCE_ADD", "ATTENDANCE_EDIT"],
        [AuditCategory.Cover] = ["COVER_ASSIGN", "COVER_CANCEL"],
        [AuditCategory.AlertReview] = ["ALERT_REVIEW"],
        [AuditCategory.Setup] =
        [
            "BUILDING_CREATE", "BUILDING_UPDATE", "BUILDING_ACTIVATE", "BUILDING_DEACTIVATE",
            "AREA_CREATE", "AREA_UPDATE", "AREA_ACTIVATE", "AREA_DEACTIVATE",
            "POINT_CREATE", "POINT_UPDATE", "POINT_ACTIVATE", "POINT_DEACTIVATE",
            "SIGN_LOCATION", "SIGN_RADIUS",
        ],
        [AuditCategory.QrToken] = ["QR_REGENERATE"],
        [AuditCategory.Account] =
        [
            "ACCOUNT_CREATE", "ACCOUNT_UPDATE", "ACCOUNT_PHONE", "ACCOUNT_PASSWORD", "ACCOUNT_ACTIVATE", "ACCOUNT_DEACTIVATE",
            "ADMIN_FIRST_CREATE", "ADMIN_RECOVER_CREATE", "ADMIN_RECOVER_RESET",
        ],
        [AuditCategory.Export] = ["EXPORT_EXCEL"],
    };

    public static IReadOnlyList<string> ActionsOf(AuditCategory category) => Actions[category];

    /// <summary>Null for an action no category lists yet; the page shows it under "ทุกประเภท" only.</summary>
    public static AuditCategory? Of(string action) =>
        Actions.Where(pair => pair.Value.Contains(action)).Select(pair => (AuditCategory?)pair.Key).FirstOrDefault();
}
