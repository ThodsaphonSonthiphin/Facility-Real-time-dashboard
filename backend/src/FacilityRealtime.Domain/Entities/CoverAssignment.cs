using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Cover Assignment: an Admin lets a Cleaner work another Area for one shift; it lapses when that Cleaner's attendance window for the shift closes (facility-0069).</summary>
public class CoverAssignment
{
    public int Id { get; set; }

    /// <summary>The Cleaner who covers.</summary>
    public int UserId { get; set; }

    /// <summary>The Area being covered.</summary>
    public int AreaId { get; set; }

    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public int AssignedById { get; set; }
    public DateTime AssignedAt { get; set; }
    public int? CancelledById { get; set; }
    public DateTime? CancelledAt { get; set; }

    public User? User { get; set; }
    public Area? Area { get; set; }
}
