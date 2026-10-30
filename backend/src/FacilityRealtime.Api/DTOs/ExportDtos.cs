using FacilityRealtime.Application.Export;

namespace FacilityRealtime.Api.DTOs;

/// <summary>Wireframe D8. From and To are Thai shift dates, yyyy-MM-dd, both included. Omitted building selectors = every building; explicit selectors are mutually exclusive.</summary>
public record ExportRequest(string? From, string? To, int? BuildingId, List<ExportSet>? Sets, List<int>? BuildingIds = null);
