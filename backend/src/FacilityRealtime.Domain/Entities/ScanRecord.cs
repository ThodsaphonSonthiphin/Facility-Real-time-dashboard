using System;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

public class ScanRecord
{
    public long Id { get; set; }
    public int ServicePointId { get; set; }
    public int UserId { get; set; }
    public ScanStatus Status { get; set; } = ScanStatus.Normal;
    public string? IssueTags { get; set; }
    public string? Notes { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    public ServicePoint? ServicePoint { get; set; }
    public User? User { get; set; }
}
