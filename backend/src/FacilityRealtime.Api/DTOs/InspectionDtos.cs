using FacilityRealtime.Application.Rounds;
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

/// <summary>
/// facility-0029. ScanRecordId is the submission the scan showed (InspectionScanDto.Submission.Id). Result: Passed = สะอาด,
/// Rework = ไม่สะอาด. Defect is required with Rework and must be left out with Passed.
/// </summary>
public record CreateInspectionRequest(
    string? QrToken, long? ScanRecordId, InspectionResult? Result, string? Defect, double? Latitude, double? Longitude, double? AccuracyM);

/// <summary>WithinRadius false = Geofence Flag (the INSPECTION_GPS alert); null = the sign has no coordinates yet.</summary>
public record InspectionCreatedResponse(
    long InspectionId,
    int ServicePointId,
    long ScanRecordId,
    InspectionResult Result,
    PointStatus NewPointStatus,
    int? DistanceM,
    bool? WithinRadius,
    DateTime InspectedAt);
