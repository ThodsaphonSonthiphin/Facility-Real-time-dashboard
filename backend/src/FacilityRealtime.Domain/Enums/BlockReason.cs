namespace FacilityRealtime.Domain.Enums;

/// <summary>Why a scan was refused (CONTEXT.md, Blocked Scan; database.html blocked_scans.reason).</summary>
public enum BlockReason
{
    /// <summary>A Cleaner scanned a sign of another Area (facility-0041).</summary>
    OtherArea,

    /// <summary>A Supervisor scanned a point in another building (facility-0048 rule 2).</summary>
    OtherBuilding,

    /// <summary>A Supervisor scanned a point of their own building outside their own shift (facility-0048 amendment).</summary>
    OutsideShift,
}
