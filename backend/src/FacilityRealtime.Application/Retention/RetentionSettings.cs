namespace FacilityRealtime.Application.Retention;

public sealed class RetentionSettings
{
    public const string SectionName = "Retention";
    public bool Enabled { get; set; } = true;
    public int GpsDays { get; set; } = 90;
    public int RecordYears { get; set; } = 1;
    public int RunAtHour { get; set; } = 2;
    public int BatchSize { get; set; } = 1000;
    public int RetryMinutes { get; set; } = 15;
    public int RefreshTokenGraceDays { get; set; } = 30;
    public void Validate()
    {
        if (GpsDays is < 1 or > 36500 || RecordYears is < 1 or > 100 || RunAtHour is < 0 or > 23
            || BatchSize is < 1 or > 10000 || RetryMinutes is < 1 or > 1440 || RefreshTokenGraceDays is < 1 or > 36500)
            throw new InvalidOperationException("Retention settings outside allowed ranges: GpsDays/RefreshTokenGraceDays 1-36500, RecordYears 1-100, RunAtHour 0-23, BatchSize 1-10000, RetryMinutes 1-1440.");
    }
}
public sealed record RetentionResult(int GpsCleared = 0, int SummariesCreated = 0, int RecordsDeleted = 0);
public interface IRetentionRunner
{
    Task<RetentionResult> RunAsync(CancellationToken cancellationToken);
}
