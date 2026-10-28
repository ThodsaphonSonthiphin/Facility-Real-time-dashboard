using FacilityRealtime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>
/// OccurredAt in [FromUtc, ToUtc); Actions null = every action; BeforeId gives the next, older page; Search null = no
/// search, else the rows whose summary, actor or reason holds it.
/// </summary>
public sealed record AuditLogFilter(
    DateTime FromUtc, DateTime ToUtc, int? ActorId, IReadOnlyCollection<string>? Actions, long? BeforeId, int Limit, string? Search = null);

public sealed record AuditLogRow(AuditEntry Entry, string ActorName);

public sealed record AuditActor(int Id, string DisplayName);

/// <summary>The Admin history page (wireframe D9, facility-0057): read-only, newest first.</summary>
public static class AuditLogQuery
{
    /// <summary>facility-0092: the actor of a row the server wrote by itself (the first Admin, reset-admin).</summary>
    public const string SystemActorName = "ระบบ";

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

        if (filter.Search is string search)
        {
            // Plan 12 decision 7: every row in the range, not only the rows loaded. % and _ are plain text; the escape is
            // ! because MySQL reads a backslash inside a string literal as its own escape.
            var pattern = "%" + search.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_") + "%";
            var actors = db.Users.Where(u => EF.Functions.Like(u.DisplayName, pattern, "!")).Select(u => u.Id);
            var systemMatches = SystemActorName.Contains(search, StringComparison.OrdinalIgnoreCase);
            query = query.Where(a =>
                EF.Functions.Like(a.Summary, pattern, "!")
                || (a.Reason != null && EF.Functions.Like(a.Reason, pattern, "!"))
                || (a.ActorId == null ? systemMatches : actors.Contains(a.ActorId.Value)));
        }

        if (filter.BeforeId is long beforeId)
        {
            query = query.Where(a => a.Id < beforeId);
        }

        // Ids grow with every row, so the newest row has the highest id
        var entries = await query.OrderByDescending(a => a.Id).Take(filter.Limit).ToListAsync();
        var actorIds = entries.Where(a => a.ActorId != null).Select(a => a.ActorId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        return entries.Select(a => new AuditLogRow(a, a.ActorId is int id ? names[id] : SystemActorName)).ToList();
    }

    /// <summary>Everyone with a line in the log, deactivated accounts included, for the "ผู้ทำ" filter.</summary>
    public static async Task<IReadOnlyList<AuditActor>> ActorsAsync(AppDbContext db)
    {
        // The system is no account, so the "ผู้ทำ" filter does not offer it; the "บัญชี" category finds its rows
        var ids = await db.AuditLog.Where(a => a.ActorId != null).Select(a => a.ActorId!.Value).Distinct().ToListAsync();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .Select(u => new AuditActor(u.Id, u.DisplayName))
            .ToListAsync();
    }
}
