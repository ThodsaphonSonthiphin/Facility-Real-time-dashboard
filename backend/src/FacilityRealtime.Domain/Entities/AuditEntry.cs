namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Audit Log: one Admin change (facility-0051). Read-only for the app once written.</summary>
public class AuditEntry
{
    public long Id { get; set; }
    public int ActorId { get; set; }
    public DateTime OccurredAt { get; set; }

    /// <summary>e.g. COVER_ASSIGN, COVER_CANCEL, ATTENDANCE_EDIT.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The table that changed, e.g. cover_assignments.</summary>
    public string EntityType { get; set; } = string.Empty;

    public long? EntityId { get; set; }

    /// <summary>The line shown on the Admin history page.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Never contains a phone number or a password.</summary>
    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    /// <summary>Required for attendance corrections.</summary>
    public string? Reason { get; set; }
}
