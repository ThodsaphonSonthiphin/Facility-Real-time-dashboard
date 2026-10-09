using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>Latitude, Longitude and AccuracyM are what the browser's Geolocation API reported.</summary>
public record RecordAttendanceRequest(string QrToken, AttendanceEvent? EventType, double? Latitude, double? Longitude, double? AccuracyM);

public record AttendanceEntryDto(AttendanceEvent EventType, DateTime OccurredAt, AttendanceSource Source, bool? WithinRadius);

public enum AttendanceOutcome
{
    Recorded,

    /// <summary>facility-0026: the event already exists; the first time is kept.</summary>
    AlreadyRecorded,

    /// <summary>facility-0031: a different event within RepeatIgnoreMinutes of the latest one is not counted.</summary>
    TooSoon,
}

/// <summary>ShiftDate and Shift are null, and the lists empty, outside every attendance window of the Cleaner's shift.</summary>
public record AttendanceStateDto(
    DateOnly? ShiftDate,
    Shift? Shift,
    IReadOnlyList<AttendanceEntryDto> Events,
    IReadOnlyList<AttendanceEvent> NextEvents,
    AttendanceOutcome? Outcome);

public record AttendanceRefusedDto(string Message, IReadOnlyList<AttendanceEvent> NextEvents);

/// <summary>facility-0041: tells the phone which Area the scanned sign belongs to.</summary>
public record BlockedScanResponse(string Message, string AreaCode, string AreaName);
