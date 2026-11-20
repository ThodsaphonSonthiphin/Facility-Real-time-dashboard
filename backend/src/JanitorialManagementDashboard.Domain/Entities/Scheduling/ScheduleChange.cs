namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>
/// ADR facility-0214: the before/after audit of one row changed by a schedule command, written in the same transaction
/// as the mutation and its receipt. Actual attendance, scans and inspections never appear here.
/// </summary>
public class ScheduleChange
{
    public long Id { get; set; }

    public int ReceiptId { get; set; }

    /// <summary>The changed table's entity name, e.g. "WeeklyDayOff".</summary>
    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    /// <summary>"create", "update" or "cancel" as recorded by the command handler.</summary>
    public string Operation { get; set; } = string.Empty;

    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }

    /// <summary>Required for a historical correction (facility-0214).</summary>
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }

    public ScheduleCommandReceipt? Receipt { get; set; }
}
