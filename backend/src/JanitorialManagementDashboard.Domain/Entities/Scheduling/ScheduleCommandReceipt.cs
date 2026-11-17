namespace JanitorialManagementDashboard.Domain.Entities.Scheduling;

/// <summary>
/// ADRs facility-0165–0167, 0219 and 0221: the durable result of one committed schedule command. Same CommandId and
/// payload returns this row's result; same CommandId with a different payload is a conflict; an expired identity can
/// never apply a second time.
/// </summary>
public class ScheduleCommandReceipt
{
    public int Id { get; set; }

    /// <summary>The client/server-issued command identity; a UNIQUE index enforces one result per identity.</summary>
    public Guid CommandId { get; set; }

    public int ActorId { get; set; }

    public string Kind { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of the canonical operation, expected revisions and payload (Task 5).</summary>
    public string PayloadFingerprint { get; set; } = string.Empty;

    /// <summary>The expected-revision map the command was validated against, as JSON.</summary>
    public string ExpectedRevisionsJson { get; set; } = "{}";

    public string Outcome { get; set; } = string.Empty;

    /// <summary>The committed result payload, as JSON; retained for the configured detail window.</summary>
    public string ResultJson { get; set; } = "{}";

    /// <summary>When the business mutation and this receipt committed, in UTC.</summary>
    public DateTime CommittedAt { get; set; }
}
