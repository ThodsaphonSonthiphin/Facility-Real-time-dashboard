namespace FacilityRealtime.Domain.Entities;

/// <summary>A building. Areas sit in one building; a Supervisor is assigned to one (facility-0048).</summary>
public class Building
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
