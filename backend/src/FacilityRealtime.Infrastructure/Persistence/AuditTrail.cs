using System.Text.Json;
using FacilityRealtime.Domain.Entities;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>facility-0051: every Admin change is logged — who, when, what, before and after. Save it with the change itself.</summary>
public static class AuditTrail
{
    public static void Add(
        AppDbContext db,
        int actorId,
        DateTime occurredAt,
        string action,
        string entityType,
        long? entityId,
        string summary,
        object? before,
        object? after,
        string? reason = null) =>
        db.AuditLog.Add(new AuditEntry
        {
            ActorId = actorId,
            OccurredAt = occurredAt,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            Reason = reason,
        });
}
