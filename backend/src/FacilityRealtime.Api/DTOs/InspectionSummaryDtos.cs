using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>One Supervisor's line (plan 6 decision 15). The page shows PointsInspected "of" PointsTotal and the percentage inspected.</summary>
public record InspectionSummaryRowDto(
    int SupervisorId,
    string EmployeeId,
    string DisplayName,
    string BuildingCode,
    Shift Shift,
    int PointsInspected,
    int PointsTotal,
    int Submitted,
    int SubmittedInspected,
    int NotInspected,
    int Inspections,
    int Reworks,
    int GpsFlagged);

/// <summary>facility-0048 rule 5: From and To are the Thai shift dates covered, both included.</summary>
public record InspectionSummaryDto(DateOnly From, DateOnly To, IReadOnlyList<InspectionSummaryRowDto> Rows);
