using FacilityRealtime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>OccurredAt in [FromUtc, ToUtc); Actions null = every action; BeforeId gives the next, older page.</summary>
public sealed record AuditLogFilter(DateTime FromUtc, DateTime ToUtc, int? ActorId, IReadOnlyCollection<string>? Actions, long? BeforeId, int Limit);

public sealed record AuditLogRow(AuditEntry Entry, string ActorName);

public sealed record AuditActor(int Id, string DisplayName);

/// <summary>The Admin history page (wireframe D9, facility-0057): read-only, newest first.</summary>
public static class AuditLogQuery
{
    public static async Task<IReadOnlyList<AuditLogRow>> LoadAsync(AppDbContext db, AuditLogFilter filter)
    {
        var query = db.AuditLog.AsNoTracking().Where(a => a.OccurredAt >= filter.FromUtc && a.OccurredAt < filter.ToUtc);
        if (filter.ActorId is int actorId)
        {
            query = query.Where(a => a.ActorId == actorId);
        }

        if (filter.Actions is not null)
        {
            var actions = filter.Actions.ToList();
            query = query.Where(a => actions.Contains(a.Action));
        }

        if (filter.BeforeId is long beforeId)
        {
            query = query.Where(a => a.Id < beforeId);
        }

        // Ids grow with every row, so the newest row has the highest id
        var entries = await query.OrderByDescending(a => a.Id).Take(filter.Limit).ToListAsync();
        var actorIds = entries.Select(a => a.ActorId).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        return entries.Select(a => new AuditLogRow(a, names[a.ActorId])).ToList();
    }

    /// <summary>Everyone with a line in the log, deactivated accounts included, for the "ผู้ทำ" filter.</summary>
    public static async Task<IReadOnlyList<AuditActor>> ActorsAsync(AppDbContext db)
    {
        var ids = await db.AuditLog.Select(a => a.ActorId).Distinct().ToListAsync();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .Select(u => new AuditActor(u.Id, u.DisplayName))
            .ToListAsync();
    }
}
