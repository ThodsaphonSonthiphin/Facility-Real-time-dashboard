using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Rounds;

/// <summary>A Scan Record of the shift being looked at. RoundWindowId null = Off-Round Submission.</summary>
public sealed record SubmissionFact(long Id, int? RoundWindowId, DateTime SubmittedAt);

/// <summary>An Inspection Record of one of those Scan Records. Defect is shown while the card is ต้องแก้ไข (facility-0029).</summary>
public sealed record InspectionFact(long SubmissionId, InspectionResult Result, DateTime InspectedAt, string? Defect = null);
