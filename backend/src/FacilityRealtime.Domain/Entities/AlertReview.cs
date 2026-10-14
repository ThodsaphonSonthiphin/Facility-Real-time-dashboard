using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>database.html alert_reviews: an alert the Admin closed ("คุยแล้ว ปิดเตือน"). One per alert.</summary>
public class AlertReview
{
    public long Id { get; set; }
    public AlertType AlertType { get; set; }

    /// <summary>The row in the alert's own table: shift_attendances, scan_records or blocked_scans.</summary>
    public long RecordId { get; set; }

    public int ReviewedById { get; set; }
    public DateTime ReviewedAt { get; set; }
}
