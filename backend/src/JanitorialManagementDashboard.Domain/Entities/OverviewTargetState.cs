namespace JanitorialManagementDashboard.Domain.Entities;

public class OverviewTargetState
{
    public int Id { get; set; } = 1;
    public long? LatestRevisionId { get; set; }
    public OverviewTargetRevision? LatestRevision { get; set; }
}
