using System;

namespace FacilityRealtime.Domain.Entities;

public class ServicePoint
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int CleaningIntervalMinutes { get; set; } = 60;
    public string QrToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
