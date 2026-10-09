namespace FacilityRealtime.Domain.Enums;

/// <summary>Which round a Scan Record counts for, decided once when it is saved (ADR facility-0047 rule 3).</summary>
public enum Placement
{
    /// <summary>Inside its Round Window.</summary>
    OnTime,

    /// <summary>A Late Submission: after the window ended, for a round that had none yet.</summary>
    Late,

    /// <summary>The rework a Supervisor asked for on that round.</summary>
    Rework,

    /// <summary>An Off-Round Submission: kept, but counts for no round and does not change the Point Status.</summary>
    OffRound,
}
