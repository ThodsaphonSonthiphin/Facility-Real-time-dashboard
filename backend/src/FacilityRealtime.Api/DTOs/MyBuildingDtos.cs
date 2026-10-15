using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

/// <summary>One building on the page. IsCover: shown because of a Cover Assignment in the clock's shift, even for the own building. CanInspect: the Supervisor may inspect there now (own building in own shift, or a cover).</summary>
public record MyBuildingSectionDto(
    int BuildingId, string BuildingCode, string BuildingName, bool IsCover, bool CanInspect, IReadOnlyList<PointStatusDto> Points);

/// <summary>facility-0057 rule 1, wireframe M15: the clock's shift; the own building first, then buildings covered in it.</summary>
public record MyBuildingDto(DateOnly ShiftDate, Shift Shift, IReadOnlyList<MyBuildingSectionDto> Buildings);
