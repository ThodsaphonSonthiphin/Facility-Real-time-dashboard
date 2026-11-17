using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>
/// ADRs facility-0137–0139, 0144–0145, 0152, 0155, 0206, 0207 and 0211: a relief Cleaner's dated work in a destination
/// Area. Replacing a named regular Cleaner inherits their break group by default; a standalone assignment carries the
/// group the recorder chose. The recorded agreement is the destination actor's attestation, not a source signature.
/// </summary>
public class ReliefAssignment
{
    public int Id { get; set; }

    /// <summary>The relief Cleaner who works.</summary>
    public int UserId { get; set; }

    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }

    /// <summary>The destination Area (facility-0155).</summary>
    public int AreaId { get; set; }

    /// <summary>Null for standalone work until the recorder chooses group 1 or 2 (facility-0211).</summary>
    public int? BreakGroup { get; set; }

    /// <summary>The regular Cleaner whose duty is covered; null for standalone relief work.</summary>
    public int? ReplacedUserId { get; set; }

    /// <summary>The building the person comes from: the replaced person's building or the relief's home building.</summary>
    public int SourceBuildingId { get; set; }

    /// <summary>The responsible Supervisor at the source when a different person agreed to the loan.</summary>
    public int? SourceSupervisorId { get; set; }

    /// <summary>True when the destination actor recorded that the source agreed outside the web (facility-0207).</summary>
    public bool AgreementReported { get; set; }

    public bool IsCancelled { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? CancelledById { get; set; }
    public string? CancellationReason { get; set; }

    public long Revision { get; set; } = 1;
    public string? Reason { get; set; }

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
