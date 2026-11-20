using System.Text.Json.Serialization;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;

namespace JanitorialManagementDashboard.Api.DTOs;

public sealed record OverviewCount(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Value,
    int Observed,
    Availability Availability);

public sealed record OverviewContextDto(
    DateOnly ShiftDate,
    Shift Shift,
    int? BuildingId,
    string Phase,
    bool IsClockCurrent,
    DateTime EvaluationUtc,
    DateTime ReadAtUtc,
    DateTime? CloseUtc,
    long? TargetRevisionId,
    OverviewRange Range);

public sealed record OverviewMetricDto(OverviewCount Numerator, OverviewCount Denominator);

public sealed record OverviewBuildingDto(
    int Id,
    string Code,
    string Name,
    OverviewMetricDto Entry,
    OverviewMetricDto Submission,
    OverviewMetricDto Inspection,
    IReadOnlyDictionary<string, OverviewCount> Groups);

public sealed record OverviewSummaryDto(
    OverviewContextDto Context,
    SectionAvailability Availability,
    OverviewMetricDto Entry,
    OverviewMetricDto Submission,
    OverviewMetricDto Inspection,
    IReadOnlyDictionary<string, OverviewCount> Groups,
    IReadOnlyList<OverviewBuildingDto> Buildings);

public sealed record OverviewAreaRowDto(
    int AreaId,
    int BuildingId,
    string Code,
    string Name,
    string BuildingCode,
    string BuildingName,
    string Entry,
    AreaWork Work,
    string[] Reasons,
    string[] RoundKeys,
    bool OriginsIncomplete = false);

public sealed record OverviewAreaListDto(
    OverviewSummaryDto Summary,
    string Group,
    int Page,
    int PageSize,
    OverviewCount Total,
    IReadOnlyList<OverviewAreaRowDto> Rows);

public sealed record OverviewPointDetailDto(
    OverviewPoint Work,
    string? Code,
    string Name,
    int SortOrder,
    string? RoundKey,
    TimeOnly? Start,
    TimeOnly? End,
    int? WindowId = null);

public sealed record OverviewResponsibilityDto(
    int UserId,
    int AreaId,
    string State,
    int? LastConfirmedAreaId,
    DateTime? ConfirmedAt);

public sealed record OverviewUnresolvedSubmissionDto(
    long Id,
    int PointId,
    DateTime SubmittedAt,
    Placement Placement,
    TimeOnly? RoundStart,
    TimeOnly? RoundEnd);

public sealed record OverviewRoundResultDto(
    string RoundKey,
    RoundObligation Round,
    string Outcome,
    long? SubmissionId,
    long? InspectionId,
    bool IsPartial = false,
    IReadOnlyList<OverviewUnresolvedSubmissionDto>? UnresolvedSubmissions = null);

public sealed record OverviewAreaDetailDto(
    OverviewContextDto Context,
    SectionAvailability Availability,
    OverviewAreaRowDto Area,
    bool MatchesGroup,
    IReadOnlyList<AreaEntryEvidence> EntryEvidence,
    OverviewResponsibilityDto? CurrentResponsibility,
    OverviewFacts Facts,
    IReadOnlyList<OverviewPointDetailDto> Points,
    IReadOnlyList<OverviewRoundResultDto> Rounds,
    string? SelectedRoundKey);
