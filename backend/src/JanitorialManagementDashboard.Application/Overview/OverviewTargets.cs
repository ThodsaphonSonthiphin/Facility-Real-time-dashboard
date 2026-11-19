using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Overview;

public sealed record ReceivingPolicy(int OpensMinutesBeforeShift, int ClosesMinutesAfterShift);
public sealed record TargetBuilding(int Id, string Code, string Name, bool IsActive, DateTime CreatedAt);
public sealed record TargetArea(int Id, int BuildingId, string Code, string Name,
    bool IsActive, ShiftPattern ShiftPattern, DateTime CreatedAt);
public sealed record TargetPoint(int Id, int AreaId, string? Code, string Name, int SortOrder,
    bool IsActive, DateTime CreatedAt);
public sealed record TargetWindow(int Id, int PointId, Shift Shift,
    TimeOnly Start, TimeOnly End, DateTime CreatedAt);
public sealed record TargetImage(int SchemaVersion, ReceivingPolicy Policy,
    IReadOnlyList<TargetBuilding> Buildings, IReadOnlyList<TargetArea> Areas,
    IReadOnlyList<TargetPoint> Points, IReadOnlyList<TargetWindow> Windows,
    DateTime? CaptureStartedAtUtc = null, DateTime? CoverageGapFromUtc = null);
public sealed record TargetRevision(long Id, DateTime EffectiveAt, TargetImage Image);
public sealed record OverviewMoment(string Phase, DateTime EvaluationUtc,
    DateTime? CloseUtc, bool PolicyComplete);
public sealed record RoundObligation(int PointId, int AreaId, int BuildingId,
    string AreaCode, string AreaName, string BuildingCode, string BuildingName,
    int OriginId, TimeOnly Start, TimeOnly End, DateTime ActiveFromUtc,
    DateTime? FinalizedAtUtc, long ActivationRevisionId, string? FinalizedReason);
public sealed record TargetTimeline(TargetImage? Current, long? CurrentRevisionId,
    IReadOnlyList<RoundObligation> Rounds, bool CurrentComplete, bool HistoryComplete)
{
    // The slot is part of every opaque key, including rounds activated after scheduled end.
    internal ShiftSlot? Slot { get; init; }
    internal IReadOnlyList<TargetRevision> ReceivingHistory { get; init; } = [];
    internal IReadOnlyList<(DateTime From, DateTime To)> StructuralGaps { get; init; } = [];
}
