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
            BeforeJson = Serialize(before, nameof(before)),
            AfterJson = Serialize(after, nameof(after)),
            Reason = reason,
        });

    /// <summary>facility-0057: no phone or password in the log. An entity can carry a hash, so only snapshots are accepted.</summary>
    private static string? Serialize(object? snapshot, string parameterName) =>
        snapshot is null ? null
        : snapshot.GetType().Assembly == typeof(AuditEntry).Assembly
            ? throw new ArgumentException($"Pass a snapshot, not the {snapshot.GetType().Name} entity.", parameterName)
            : JsonSerializer.Serialize(snapshot);
}
