using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Overview;

/// <summary>One retained submission fact, already narrowed by the read to the selected slot plus Issue evidence.</summary>
public sealed record OverviewSubmission(long Id, int PointId, DateOnly ShiftDate, Shift Shift, int? OriginId,
    int? ExistingWindowId, TimeOnly? RoundStart, TimeOnly? RoundEnd,
    Placement Placement, CleaningStatus Status, DateTime SubmittedAt,
    int UserId, string? IssueTags, string? Note);

/// <summary>One Supervisor verdict on a submission.</summary>
public sealed record OverviewInspection(long Id, long SubmissionId,
    InspectionResult Result, DateTime InspectedAt, int SupervisorId, string? Defect);

/// <summary>An attendance fact. It supports Entry but never proves it (spec §13, ADR facility-0216).</summary>
public sealed record OverviewAttendance(long Id, int AreaId, int UserId,
    AttendanceEvent Event, DateTime OccurredAt, bool Edited, AttendanceSource? Source = null,
    string? Reason = null, DateTime? CorrectedAt = null,
    OverviewAttendanceChange? Addition = null, OverviewAttendanceChange? Edit = null);

public sealed record OverviewAttendanceChange(string? Reason, DateTime RecordedAt);

/// <summary>An explicitly skipped action, without an attendance time.</summary>
public sealed record OverviewAttendanceGap(int Id, int AreaId, int UserId,
    AttendanceEvent Event, string? Reason, DateTime? RecordedAt);

/// <summary>Stored leave for a referenced person in the slot, separate from actual attendance.</summary>
public sealed record OverviewAttendanceLeave(int Id, int UserId, DateTime StartsAt, DateTime EndsAt);

/// <summary>A scheduling explanation about a person's action, without a recorded attendance time.</summary>
public sealed record OverviewAttendanceLeaveEffect(int UserId, AttendanceEvent Event, string Kind);

/// <summary>A live cover assignment. It assigns an Area but is never entry evidence.</summary>
public sealed record OverviewCover(int Id, int AreaId, int UserId,
    DateTime AssignedAt, DateTime? CancelledAt);

/// <summary>A display name for the people facts of a detail view.</summary>
public sealed record OverviewPerson(int Id, string DisplayName);

/// <summary>
/// The immutable facts one projection reads. <see cref="Submissions"/> carries the selected slot's work plus each
/// relevant point's last retained submission at/before evaluation for Issue; the ShiftDate/Shift fields decide which
/// slot a fact belongs to, never the wall-clock time of <c>SubmittedAt</c>.
/// </summary>
public sealed record OverviewFacts(IReadOnlyList<OverviewSubmission> Submissions,
    IReadOnlyList<OverviewInspection> Inspections,
    IReadOnlyList<OverviewAttendance> Attendance, IReadOnlyList<OverviewCover> Covers,
    IReadOnlyList<OverviewPerson> People, IReadOnlyList<AreaEntryEvidence> EntryEvidence,
    IReadOnlySet<int> AssignedAreas, bool OriginsComplete, bool IssueStateKnown,
    IReadOnlyList<OverviewAttendanceGap>? AttendanceGaps = null,
    IReadOnlyList<OverviewAttendanceLeave>? AttendanceLeaves = null,
    IReadOnlyList<OverviewAttendanceLeaveEffect>? AttendanceLeaveEffects = null);

/// <summary>One point's current round. <see cref="IsDue"/> decides whether Work counts it.</summary>
public sealed record OverviewPoint(int PointId, bool IsDue, bool IsSubmitted,
    bool IsInspected, bool NeedsRework, bool IsIssue, bool IsOverdue,
    bool IsUpcoming, long? SubmissionId, long? InspectionId);

/// <summary>The denominator-backed completion of one Area. Every whole-area flag is derived from due points.</summary>
public sealed record AreaWork(int DuePoints, int SubmittedPoints, int InspectedPoints,
    bool Submitted, bool Inspected, bool Incomplete, bool PendingInspection,
    bool NoSubmission, bool Rework, bool Issue, bool Overdue);

/// <summary>
/// One finalized round's outcome, keyed by the opaque round key Task 6 returns. A round with an ambiguous legacy origin
/// is partial: its outcome is <see cref="OverviewOutcomes.Unresolved"/> when no submission could be verified, and the
/// candidate scans stay inspectable in <paramref name="UnresolvedSubmissions"/> instead of being deleted.
/// </summary>
public sealed record OverviewRoundResult(string RoundKey, RoundObligation Round,
    string Outcome, long? SubmissionId, long? InspectionId,
    bool IsPartial = false, IReadOnlyList<OverviewSubmission>? UnresolvedSubmissions = null);

/// <summary>One target Area under one historical owner. Groups drive every cardinality.</summary>
public sealed record OverviewArea(int Id, int BuildingId, string Code, string Name,
    string BuildingCode, string BuildingName, string Entry, AreaWork Work,
    IReadOnlyList<OverviewPoint> Points, IReadOnlyList<OverviewRoundResult> History,
    IReadOnlySet<string> Groups, bool OriginsIncomplete = false);

/// <summary>The single projection Task 6 pages, filters and counts from.</summary>
public sealed record OverviewSnapshot(ShiftSlot Slot, OverviewMoment Moment,
    TargetTimeline Targets, SectionAvailability Availability,
    IReadOnlyList<OverviewArea> Areas);

/// <summary>
/// The locked history outcomes. A later pass never removes an earlier failure. <see cref="Unresolved"/> is used only
/// for an ambiguous legacy origin: the round cannot be called done or missed because a real scan might belong to it.
/// </summary>
public static class OverviewOutcomes
{
    public const string NotDone = "notDone";
    public const string NotInspected = "notInspected";
    public const string NotReworked = "notReworked";
    public const string Passed = "passed";
    public const string Unresolved = "unresolved";
}
