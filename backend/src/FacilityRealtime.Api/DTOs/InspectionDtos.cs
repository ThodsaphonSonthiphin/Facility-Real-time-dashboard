using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>The QR Token read from the point's sign and what the browser's Geolocation API reported (facility-0036, 0037).</summary>
public record InspectionScanRequest(string? QrToken, double? Latitude, double? Longitude, double? AccuracyM);

/// <summary>The submission a Supervisor would judge (wireframe M11 "ส่งงานล่าสุด").</summary>
public record InspectionSubmissionDto(
    long Id, DateTime SubmittedAt, string CleanerName, Placement Placement, CleaningStatus Status, IReadOnlyList<string> IssueTags, string? Note);

/// <summary>
/// Wireframes M11 and M16. Submission.Id is what POST /api/inspections must send as scanRecordId; when Submission is null,
/// Refusal says why there is no inspect button. IsCover: allowed here only by a Cover Assignment. DistanceM and
/// WithinRadius compare the phone with the sign (both null when the sign has no coordinates).
/// </summary>
public record InspectionScanDto(
    PointStatusDto Point, InspectionSubmissionDto? Submission, string? Refusal, bool IsCover, int? DistanceM, bool? WithinRadius);
