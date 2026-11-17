using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>
/// ADRs facility-0203, 0204 and 0213: a Supervisor's instruction to move an already checked-in person from one Area to
/// another. The effective time is the plan, not measured departure; the actual arrival is a separate confirmation.
/// Cancelling keeps the instruction and marks it cancelled; a confirmed instruction stays as evidence.
/// </summary>
public class AreaTransfer
{
    public int Id { get; set; }

    public int PersonId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }

    public int FromAreaId { get; set; }
    public int ToAreaId { get; set; }

    /// <summary>When the instruction takes effect, in UTC. This is not a source departure scan.</summary>
    public DateTime EffectiveAt { get; set; }

    public long Revision { get; set; } = 1;

    public bool IsCancelled { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? CancelledById { get; set; }
    public string? CancellationReason { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
