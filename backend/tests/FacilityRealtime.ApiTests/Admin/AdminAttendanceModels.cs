namespace FacilityRealtime.ApiTests.Admin;

public record BoardEventModel(long Id, string EventType, DateTime OccurredAt, string Source, bool? WithinRadius, short? DistanceM);

public record BoardCoverModel(int Id, int UserId, string EmployeeId, string DisplayName, string? OwnAreaCode, DateTime AssignedAt, DateTime? ShiftInAt);

public record BoardRowModel(
    int AreaId, string AreaCode, string AreaName, string BuildingCode, AssignedCleanerModel? Cleaner,
    string State, List<BoardEventModel> Events, List<BoardCoverModel> Covers);

public record BoardSummaryModel(int Required, int Present, int Empty, int Covered);

public record AttendanceBoardModel(DateOnly ShiftDate, string Shift, bool ShiftEnded, BoardSummaryModel Summary, List<BoardRowModel> Rows);
