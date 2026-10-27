using System.Text.Json;

namespace FacilityRealtime.ApiTests.Admin;

public record BoardEventModel(long Id, string EventType, DateTime OccurredAt, string Source, bool? WithinRadius, short? DistanceM);

public record BoardCoverModel(int Id, int UserId, string EmployeeId, string DisplayName, string? OwnAreaCode, DateTime AssignedAt, DateTime? ShiftInAt);

public record BoardRowModel(
    int AreaId, string AreaCode, string AreaName, string BuildingCode, AssignedCleanerModel? Cleaner,
    string State, List<BoardEventModel> Events, List<BoardCoverModel> Covers);

public record BoardSummaryModel(int Required, int Present, int Empty, int Covered);

public record AttendanceBoardModel(DateOnly ShiftDate, string Shift, bool ShiftEnded, BoardSummaryModel Summary, List<BoardRowModel> Rows);

public record AlertPersonModel(int Id, string EmployeeId, string DisplayName, string Role, string? AreaCode);

public record CoverSuggestionModel(int UserId, int? AreaId, int? BuildingId, DateOnly ShiftDate, string Shift);

public record AlertModel(
    string Kind, long RecordId, DateTime At, AlertPersonModel Person, string AreaCode, string AreaName,
    string? SignCode, string? PointName, short? DistanceM, short? RadiusM, short? AccuracyM,
    List<string> MissingEvents, bool Reviewed, DateTime? ReviewedAt, CoverSuggestionModel? Cover, string? BlockReason);

public record CoverCandidateModel(int Id, string EmployeeId, string DisplayName, string AreaCode, DateTime? ShiftInAt, DateTime? ScannedThisAreaAt);

public record AuditLogEntryModel(
    long Id, DateTime OccurredAt, int? ActorId, string ActorName, string Action, string? Category,
    string EntityType, long? EntityId, string Summary, JsonElement? Before, JsonElement? After, string? Reason);

public record AuditLogPageModel(List<AuditLogEntryModel> Entries, long? NextBeforeId);

public record AuditActorModel(int Id, string DisplayName);
