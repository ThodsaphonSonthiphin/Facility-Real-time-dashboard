using System;
using System.Collections.Generic;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record ServicePointStatusDto(
    int Id,
    string Name,
    string Location,
    int CleaningIntervalMinutes,
    string QrToken,
    PointStatus CurrentStatus,
    DateTime? LastScannedAt,
    string? LastCleanerName,
    ScanStatus? LastScanStatus,
    List<string>? LastIssueTags,
    string? LastNotes,
    int MinutesSinceLastScan
);

/// <summary>ADR facility-0017: no UserId. The scanner is the account in the access token.</summary>
public record CreateScanRecordRequest(
    string QrToken,
    ScanStatus Status,
    List<string>? IssueTags,
    string? Notes
);

public record ScanRecordCreatedResponse(
    long ScanRecordId,
    int ServicePointId,
    PointStatus NewPointStatus,
    DateTime ScannedAt
);
