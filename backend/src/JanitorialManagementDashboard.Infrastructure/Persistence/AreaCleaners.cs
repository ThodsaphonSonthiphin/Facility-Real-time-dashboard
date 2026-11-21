using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence;

/// <summary>facility-0040: one Cleaner per shift per Area, set from the Admin's Area form (wireframe D10).</summary>
public static class AreaCleaners
{
    /// <summary>
    /// Puts the chosen Cleaners on the Area, takes every other active Cleaner off it (area_id NULL), and synchronises
    /// each affected Cleaner's dated profile and regular baseline inside the caller's transaction.
    /// Saves twice for the user rows: users.cleaner_slot is UNIQUE and checked row by row, so the old Cleaner must leave
    /// the slot before the new one takes it. Call inside a transaction; the chosen users must be tracked by <paramref name="db"/>.
    /// </summary>
    public static async Task<IReadOnlyList<int>> ApplyAsync(
        AppDbContext db,
        ScheduleStore store,
        int areaId,
        int buildingId,
        User? dayCleaner,
        User? nightCleaner,
        int? actorId,
        DateOnly businessDate,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var chosenIds = new[] { dayCleaner?.Id, nightCleaner?.Id }.OfType<int>().ToHashSet();
        var current = await db.Users
            .Include(u => u.Area)
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.AreaId == areaId)
            .ToListAsync(ct);
        var leavingCleaners = current.Where(u => !chosenIds.Contains(u.Id)).ToList();
        var leavingAreaBuildings = leavingCleaners.ToDictionary(u => u.Id, u => u.Area?.BuildingId ?? buildingId);
        foreach (var leaving in leavingCleaners)
        {
            leaving.AreaId = null;
        }

        await db.SaveChangesAsync(ct);

        var assignedCleaners = new[] { dayCleaner, nightCleaner }.OfType<User>().ToList();
        foreach (var cleaner in assignedCleaners)
        {
            cleaner.AreaId = areaId;
        }

        await db.SaveChangesAsync(ct);

        foreach (var leaving in leavingCleaners)
        {
            var existingPeriods = await db.CleanerProfilePeriods.AsNoTracking()
                .Where(p => p.UserId == leaving.Id)
                .OrderBy(p => p.EffectiveFrom)
                .ThenBy(p => p.Id)
                .ToListAsync(ct);
            var existingHome = existingPeriods
                .LastOrDefault(p => ScheduleResolver.Applies(p.EffectiveFrom, p.EffectiveUntil, businessDate))
                ?.HomeBuildingId;
            await ScheduleSync.SyncCleanerAsync(
                db, store, leaving, existingHome ?? leavingAreaBuildings[leaving.Id], CleanerKind.Regular,
                actorId, businessDate, nowUtc, ct);
        }

        foreach (var cleaner in assignedCleaners)
        {
            await ScheduleSync.SyncCleanerAsync(
                db, store, cleaner, buildingId, CleanerKind.Regular,
                actorId, businessDate, nowUtc, ct);
        }

        await db.SaveChangesAsync(ct);

        return leavingCleaners.Select(u => u.Id)
            .Concat(assignedCleaners.Select(u => u.Id))
            .Distinct()
            .ToList();
    }
}
