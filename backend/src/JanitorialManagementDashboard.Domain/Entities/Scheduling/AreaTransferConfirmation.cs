using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>
/// ADRs facility-0204 and 0213: one actual destination-sign arrival per transfer instruction. It is not a ShiftIn and
/// never fabricates BreakIn/BreakOut; the unique TransferId makes a repeated confirmation return the same fact.
/// </summary>
public class AreaTransferConfirmation : IGpsStamped
{
    public int Id { get; set; }

    /// <summary>One confirmation per instruction; a UNIQUE index enforces it.</summary>
    public int TransferId { get; set; }

    /// <summary>The destination Area whose Check-In Sign was scanned.</summary>
    public int AreaId { get; set; }

    public int SignId { get; set; }

    /// <summary>The person who confirmed arrival.</summary>
    public int ConfirmedById { get; set; }

    /// <summary>The actual server time of the confirmation, in UTC.</summary>
    public DateTime OccurredAt { get; set; }

    public AttendanceSource Source { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public short? AccuracyM { get; set; }
    public short? DistanceM { get; set; }
    public bool? WithinRadius { get; set; }

    public DateTime CreatedAt { get; set; }
}
