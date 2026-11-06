namespace JanitorialManagementDashboard.Domain.Entities;

/// <summary>Anonymous completed point/month counts; the composite key is the archive completion marker.</summary>
public sealed class PointMonthlySummary
{
    public int ServicePointId { get; set; }
    public DateOnly Month { get; set; }
    public int RoundsTotal { get; set; }
    public int SubmittedOnTime { get; set; }
    public int SubmittedLate { get; set; }
    public int NotDone { get; set; }
    public int InspectedPass { get; set; }
    public int InspectedFail { get; set; }
    public int NotInspected { get; set; }
    public int IssuesReported { get; set; }
}
