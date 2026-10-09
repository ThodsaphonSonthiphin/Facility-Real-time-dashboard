using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Inspection Record: a Supervisor's verdict on one Scan Record.</summary>
public class InspectionRecord
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

    public ScanRecord? ScanRecord { get; set; }
    public User? Supervisor { get; set; }
}
