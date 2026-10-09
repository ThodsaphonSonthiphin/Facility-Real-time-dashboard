using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>facility-0040: one Cleaner per shift per Area, set from the Admin's Area form (wireframe D10).</summary>
public static class AreaCleaners
{
    /// <summary>
    /// Puts the chosen Cleaners on the Area and takes every other active Cleaner off it (area_id NULL).
    /// Saves twice: users.cleaner_slot is UNIQUE and checked row by row, so the old Cleaner must leave the slot before
    /// the new one takes it. Call inside a transaction; the chosen users must be tracked by <paramref name="db"/>.
    /// </summary>
    public static async Task ApplyAsync(AppDbContext db, int areaId, User? dayCleaner, User? nightCleaner)
    {
        var chosenIds = new[] { dayCleaner?.Id, nightCleaner?.Id }.OfType<int>().ToHashSet();
        var current = await db.Users
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive && u.AreaId == areaId)
            .ToListAsync();
        foreach (var leaving in current.Where(u => !chosenIds.Contains(u.Id)))
        {
            leaving.AreaId = null;
        }

        await db.SaveChangesAsync();

        foreach (var cleaner in new[] { dayCleaner, nightCleaner }.OfType<User>())
        {
            cleaner.AreaId = areaId;
        }

        await db.SaveChangesAsync();
    }
}
