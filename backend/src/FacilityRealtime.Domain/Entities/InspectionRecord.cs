using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Inspection Record: a Supervisor's verdict on one Scan Record, with the GPS of the inspection (facility-0037).</summary>
public class InspectionRecord : IGpsStamped
{
    public long Id { get; set; }
    public long ScanRecordId { get; set; }

    /// <summary>Repeats ScanRecord.ServicePointId so a point's status query needs no join.</summary>
    public int ServicePointId { get; set; }

    public int SupervisorId { get; set; }
    public InspectionResult Result { get; set; }

    /// <summary>Required when Result is Rework (a CHECK constraint enforces it).</summary>
    public string? Defect { get; set; }

    public DateTime InspectedAt { get; set; }

    /// <summary>Set when the Supervisor inspected under a Cover Assignment: another building, or outside their shift (facility-0048).</summary>
    public int? CoverAssignmentId { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public short? AccuracyM { get; set; }
    public short? DistanceM { get; set; }
    public bool? WithinRadius { get; set; }

    public ScanRecord? ScanRecord { get; set; }
    public User? Supervisor { get; set; }
    public CoverAssignment? CoverAssignment { get; set; }
}
