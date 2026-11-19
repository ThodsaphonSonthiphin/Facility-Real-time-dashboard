namespace JanitorialManagementDashboard.Domain.Entities;

public class OverviewTargetRevision
{
    public long Id { get; set; }
    public DateTime EffectiveAt { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public string PayloadHash { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}
