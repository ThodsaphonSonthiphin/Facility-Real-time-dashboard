using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Blocked Scan: a scan the system refused (facility-0041), kept so the Admin sees it.</summary>
public class BlockedScan : IGpsStamped
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int SignId { get; set; }
    public BlockReason Reason { get; set; }
    public DateTime ScannedAt { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public short? AccuracyM { get; set; }
    public short? DistanceM { get; set; }
    public bool? WithinRadius { get; set; }
}
