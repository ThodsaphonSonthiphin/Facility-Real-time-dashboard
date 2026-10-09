using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Scan Record: one submission at a Service Point, placed into a Round Window when saved (facility-0047).</summary>
public class ScanRecord
{
    public long Id { get; set; }
    public int ServicePointId { get; set; }
    public int SignId { get; set; }
    public int UserId { get; set; }

    /// <summary>The date the shift started on: a night-shift scan at 03:00 on 9 Oct belongs to 8 Oct.</summary>
    public DateOnly ShiftDate { get; set; }

    public Shift Shift { get; set; }

    /// <summary>Null for an Off-Round Submission, or when an Admin later deletes the window.</summary>
    public int? RoundWindowId { get; set; }

    /// <summary>Copied from the window at submission, because an Admin may edit the window later.</summary>
    public TimeOnly? RoundStart { get; set; }

    public TimeOnly? RoundEnd { get; set; }
    public Placement Placement { get; set; }

    /// <summary>Set only for a Late Submission.</summary>
    public int? LateMinutes { get; set; }

    public CleaningStatus Status { get; set; } = CleaningStatus.Normal;

    /// <summary>Comma-separated tag keys, e.g. "wet_floor,bad_odor".</summary>
    public string? IssueTags { get; set; }

    public string? Note { get; set; }

    /// <summary>Server time when the submission was saved (facility-0052).</summary>
    public DateTime SubmittedAt { get; set; }

    public ServicePoint? ServicePoint { get; set; }
    public Sign? Sign { get; set; }
    public User? User { get; set; }
    public PointRoundWindow? RoundWindow { get; set; }
}
