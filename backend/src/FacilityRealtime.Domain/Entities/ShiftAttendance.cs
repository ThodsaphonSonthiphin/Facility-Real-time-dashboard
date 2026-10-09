using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Attendance Event: one of a Cleaner's four scans in one shift. Unique per event per shift; the first wins.</summary>
public class ShiftAttendance : IGpsStamped
{
    public long Id { get; set; }
    public int UserId { get; set; }

    /// <summary>The Cleaner's own Area at the time of the scan.</summary>
    public int AreaId { get; set; }

    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public AttendanceEvent EventType { get; set; }
    public DateTime OccurredAt { get; set; }
    public AttendanceSource Source { get; set; }

    /// <summary>Null when an Admin added the event.</summary>
    public int? SignId { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public short? AccuracyM { get; set; }
    public short? DistanceM { get; set; }
    public bool? WithinRadius { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
