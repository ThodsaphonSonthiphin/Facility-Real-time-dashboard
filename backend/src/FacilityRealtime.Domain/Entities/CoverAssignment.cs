using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>
/// CONTEXT.md, Cover Assignment: an Admin lets a Cleaner work another Area for one shift (facility-0041), or a Supervisor
/// inspect a building outside their own building or shift (facility-0048). Exactly one of AreaId and BuildingId is set
/// (a CHECK enforces it). A Cleaner's cover lapses when their attendance window closes (facility-0069); a Supervisor's
/// when the clock's shift ends.
/// </summary>
public class CoverAssignment
{
    public int Id { get; set; }

    /// <summary>The Cleaner or Supervisor who covers.</summary>
    public int UserId { get; set; }

    /// <summary>The Area a Cleaner covers; null for a Supervisor's cover.</summary>
    public int? AreaId { get; set; }

    /// <summary>The building a Supervisor inspects; null for a Cleaner's cover.</summary>
    public int? BuildingId { get; set; }

    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public int AssignedById { get; set; }
    public DateTime AssignedAt { get; set; }
    public int? CancelledById { get; set; }
    public DateTime? CancelledAt { get; set; }

    public User? User { get; set; }
    public Area? Area { get; set; }
    public Building? Building { get; set; }
}
