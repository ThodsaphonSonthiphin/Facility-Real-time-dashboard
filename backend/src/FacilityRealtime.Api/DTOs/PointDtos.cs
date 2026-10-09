using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.DTOs;

/// <summary>Start and End are Thai wall-clock, as the Admin entered them.</summary>
public record RoundWindowDto(int Id, TimeOnly Start, TimeOnly End);

public record LastScanDto(DateTime SubmittedAt, string CleanerName, Placement Placement, CleaningStatus Status);

/// <summary>facility-0046: shown beside the status while the latest Scan Record is an Issue.</summary>
public record IssueDto(IReadOnlyList<string> Tags, string? Note, DateTime ReportedAt);

/// <summary>One Dashboard card. No QR Token (facility-0060).</summary>
public record PointStatusDto(
    int Id,
    string Name,
    int AreaId,
    string AreaCode,
    string AreaName,
    string BuildingCode,
    PointStatus Status,
    RoundWindowDto? CurrentRound,
    RoundWindowDto? NextRound,
    LastScanDto? LastScan,
    IssueDto? Issue);

/// <summary>facility-0017: no UserId, the scanner is the account in the access token. "Notes" keeps the phase-1 field name.</summary>
public record CreateScanRecordRequest(string QrToken, CleaningStatus? Status, List<string>? IssueTags, string? Notes);

public record ScanRecordCreatedResponse(
    long ScanRecordId,
    int ServicePointId,
    Placement Placement,
    int? LateMinutes,
    PointStatus NewPointStatus,
    DateTime SubmittedAt);

public static class PointDtoMapper
{
    public static PointStatusDto ToDto(PointBoardRow row)
    {
        var point = row.Point;
        var area = point.Area!;
        var last = row.LastScan;

        return new PointStatusDto(
            point.Id,
            point.Name,
            area.Id,
            area.Code,
            area.Name,
            area.Building!.Code,
            row.Status.Status,
            ToDto(row.Status.CurrentRound),
            ToDto(row.Status.NextRound),
            last is null ? null : new LastScanDto(Utc(last.SubmittedAt), last.User?.DisplayName ?? string.Empty, last.Placement, last.Status),
            last is { Status: CleaningStatus.Issue } ? new IssueDto(SplitTags(last.IssueTags), last.Note, Utc(last.SubmittedAt)) : null);
    }

    private static RoundWindowDto? ToDto(RoundWindow? round) => round is null ? null : new RoundWindowDto(round.Id, round.Start, round.End);

    private static IReadOnlyList<string> SplitTags(string? tags) =>
        string.IsNullOrEmpty(tags) ? [] : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The database returns Unspecified; marking it UTC makes the JSON end in "Z" so browsers convert it to Thai time.</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);
}
