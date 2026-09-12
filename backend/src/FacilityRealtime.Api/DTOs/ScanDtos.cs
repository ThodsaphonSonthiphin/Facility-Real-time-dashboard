using System;
using System.Collections.Generic;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(int Id, string Username, string FullName, string Role, string Token);

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

public record CreateScanRecordRequest(
    string QrToken,
    int UserId,
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
