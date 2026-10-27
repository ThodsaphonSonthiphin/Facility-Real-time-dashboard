using System.Text.Json;
using FacilityRealtime.Application.Audit;

namespace FacilityRealtime.Api.DTOs;

/// <summary>
/// One line of the Admin history page (wireframe D9). Before and After are the stored snapshots, which never hold a phone
/// number or password (facility-0057). Category is null for an action no category lists yet.
/// </summary>
public record AuditLogEntryDto(
    long Id,
    DateTime OccurredAt,
    int? ActorId,
    string ActorName,
    string Action,
    AuditCategory? Category,
    string EntityType,
    long? EntityId,
    string Summary,
    JsonElement? Before,
    JsonElement? After,
    string? Reason);

/// <summary>NextBeforeId: pass it as beforeId for the next, older page; null on the last page.</summary>
public record AuditLogPageDto(IReadOnlyList<AuditLogEntryDto> Entries, long? NextBeforeId);

public record AuditActorDto(int Id, string DisplayName);
