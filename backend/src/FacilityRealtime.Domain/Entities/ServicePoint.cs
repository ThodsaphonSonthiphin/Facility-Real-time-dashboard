namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Service Point. Its QR Token now lives on its Sign.</summary>
public class ServicePoint
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Order on the card list and on the printed signs.</summary>
    public short SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Area? Area { get; set; }
}
