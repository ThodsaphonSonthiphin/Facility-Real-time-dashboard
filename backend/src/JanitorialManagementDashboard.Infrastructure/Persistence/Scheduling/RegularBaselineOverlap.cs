using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;

/// <summary>
/// Shared 1-Cleaner-per-(AreaId, Shift) overlap check used across scheduling commands, Account mutations, and Area
/// mutations so no two active Regular Cleaners can own the same Area and Shift on overlapping dates/weekdays.
/// </summary>
public static class RegularBaselineOverlap
{
    public sealed record OverlapConflict(int HolderUserId, string HolderName, int AreaId, string AreaCode, Shift Shift);

    /// <summary>
    /// Checks whether any other active Regular Cleaner holds an overlapping <c>(AreaId, effectiveShift)</c> on any
    /// weekday in <paramref name="workingDays"/> during <c>[effectiveFrom, nextFrom)</c>.
    /// </summary>
    public static async Task<OverlapConflict?> FindConflictAsync(
        AppDbContext db,
        int excludeUserId,
        DateOnly effectiveFrom,
        DateOnly? nextFrom,
        Shift effectiveShift,
        DateOnly businessDate,
        IReadOnlyCollection<(DayOfWeek Weekday, int AreaId)> workingDays,
        IReadOnlyDictionary<int, Area>? knownAreas = null,
        CancellationToken ct = default)
    {
        if (workingDays.Count == 0)
        {
            return null;
        }

        var targetAreaIds = workingDays.Select(w => w.AreaId).Distinct().ToList();
        var areaCodes = new Dictionary<int, string>();
        if (knownAreas is not null)
        {
            foreach (var pair in knownAreas)
            {
                areaCodes[pair.Key] = pair.Value.Code;
            }
        }

        var missingAreaIds = targetAreaIds.Where(id => !areaCodes.ContainsKey(id)).ToList();
        if (missingAreaIds.Count > 0)
        {
            var fetched = await db.Areas.AsNoTracking()
                .Where(a => missingAreaIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Code })
                .ToListAsync(ct);
            foreach (var item in fetched)
            {
                areaCodes[item.Id] = item.Code;
            }
        }

        var otherUsers = await db.Users.AsNoTracking()
            .Where(u => u.Id != excludeUserId && u.IsActive && u.Role == UserRole.Cleaner)
            .ToListAsync(ct);
        if (otherUsers.Count == 0)
        {
            return null;
        }

        var otherIds = otherUsers.Select(u => u.Id).ToList();
        var otherPeriods = (await db.CleanerProfilePeriods.AsNoTracking()
            .Where(p => otherIds.Contains(p.UserId))
            .OrderBy(p => p.EffectiveFrom).ThenBy(p => p.Id)
            .ToListAsync(ct))
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var otherBaselines = (await db.ScheduleBaselines.AsNoTracking()
            .Include(b => b.Days)
            .Where(b => otherIds.Contains(b.UserId)
                && b.Kind == BaselineKind.Regular
                && (b.EffectiveUntil == null || b.EffectiveUntil.Value > effectiveFrom)
                && (nextFrom == null || b.EffectiveFrom < nextFrom.Value))
            .OrderBy(b => b.EffectiveFrom).ThenBy(b => b.Id)
            .ToListAsync(ct))
            .GroupBy(b => b.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var other in otherUsers)
        {
            otherPeriods.TryGetValue(other.Id, out var periods);
            otherBaselines.TryGetValue(other.Id, out var baselines);

            if (baselines is not null)
            {
                foreach (var b in baselines)
                {
                    var overlapStart = b.EffectiveFrom > effectiveFrom ? b.EffectiveFrom : effectiveFrom;
                    var overlapEnd = (b.EffectiveUntil, nextFrom) switch
                    {
                        (DateOnly be, DateOnly nf) => be < nf ? be : nf,
                        (DateOnly be, null) => be,
                        (null, DateOnly nf) => nf,
                        (null, null) => (DateOnly?)null,
                    };
                    if (overlapEnd is DateOnly end && overlapStart >= end)
                    {
                        continue;
                    }

                    var isRegularSameShift = periods is { Count: > 0 }
                        ? periods.Any(p => p.EffectiveFrom < (overlapEnd ?? DateOnly.MaxValue)
                            && (p.EffectiveUntil == null || p.EffectiveUntil.Value > overlapStart)
                            && p.Kind == CleanerKind.Regular
                            && p.Shift == effectiveShift)
                        : other.Shift == effectiveShift;
                    if (!isRegularSameShift)
                    {
                        continue;
                    }

                    foreach (var cell in workingDays)
                    {
                        if (!WeekdayOccursInRange(cell.Weekday, overlapStart, overlapEnd))
                        {
                            continue;
                        }

                        if (b.Days.FirstOrDefault(d => d.Weekday == cell.Weekday) is { IsDayOff: false, AreaId: int otherAreaId }
                            && otherAreaId == cell.AreaId
                            && areaCodes.TryGetValue(otherAreaId, out var areaCode))
                        {
                            return new OverlapConflict(other.Id, other.DisplayName, otherAreaId, areaCode, effectiveShift);
                        }
                    }
                }
            }

            if (effectiveFrom >= businessDate
                && other.AreaId is int userAreaId
                && other.Shift == effectiveShift
                && workingDays.Any(d => d.AreaId == userAreaId))
            {
                var isRegularInRange = periods is not { Count: > 0 }
                    || periods.Any(p => p.EffectiveFrom < (nextFrom ?? DateOnly.MaxValue)
                        && (p.EffectiveUntil == null || p.EffectiveUntil.Value > effectiveFrom)
                        && p.Kind == CleanerKind.Regular
                        && p.Shift == effectiveShift);
                var hasBaselineInRange = baselines is { Count: > 0 };
                if (isRegularInRange && !hasBaselineInRange && areaCodes.TryGetValue(userAreaId, out var areaCode))
                {
                    return new OverlapConflict(other.Id, other.DisplayName, userAreaId, areaCode, effectiveShift);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Convenience check for Account and Area setup endpoints, which assign a Regular Cleaner to <paramref name="areaId"/>
    /// across all 7 weekdays starting at <paramref name="businessDate"/>.
    /// </summary>
    public static Task<OverlapConflict?> FindSlotConflictAsync(
        AppDbContext db,
        int excludeUserId,
        int areaId,
        Shift shift,
        DateOnly businessDate,
        DateOnly? effectiveUntil = null,
        CancellationToken ct = default)
    {
        var allWeekdays = Enum.GetValues<DayOfWeek>().Select(w => (w, areaId)).ToList();
        return FindConflictAsync(db, excludeUserId, businessDate, effectiveUntil, shift, businessDate, allWeekdays, ct: ct);
    }

    /// <summary>
    /// Display names of active Cleaners holding <paramref name="areaId"/> either via legacy <c>users.AreaId</c> or via any
    /// active/future <see cref="ScheduleBaseline"/> covering <paramref name="fromDate"/> onwards (used by Area deactivation).
    /// </summary>
    public static async Task<List<string>> ActiveAreaHoldersAsync(
        AppDbContext db,
        int areaId,
        DateOnly fromDate,
        CancellationToken ct = default)
    {
        var activeCleaners = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive)
            .OrderBy(u => u.Shift).ThenBy(u => u.Id)
            .ToListAsync(ct);
        if (activeCleaners.Count == 0)
        {
            return [];
        }

        var ids = activeCleaners.Select(u => u.Id).ToList();
        var periodsByUser = (await db.CleanerProfilePeriods.AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .OrderBy(p => p.EffectiveFrom).ThenBy(p => p.Id)
            .ToListAsync(ct))
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var matchingBaselines = await db.ScheduleBaselines.AsNoTracking()
            .Where(b => ids.Contains(b.UserId)
                && b.Kind == BaselineKind.Regular
                && (b.EffectiveUntil == null || b.EffectiveUntil.Value > fromDate)
                && b.Days.Any(d => !d.IsDayOff && d.AreaId == areaId))
            .Select(b => new { b.UserId, b.EffectiveFrom, b.EffectiveUntil })
            .ToListAsync(ct);

        var baselineHolders = new HashSet<int>();
        foreach (var b in matchingBaselines)
        {
            var overlapStart = b.EffectiveFrom > fromDate ? b.EffectiveFrom : fromDate;
            var overlapEnd = b.EffectiveUntil ?? DateOnly.MaxValue;
            if (!periodsByUser.TryGetValue(b.UserId, out var periods) || periods.Count == 0
                || periods.Any(p => p.Kind == CleanerKind.Regular
                    && p.EffectiveFrom < overlapEnd
                    && (p.EffectiveUntil == null || p.EffectiveUntil.Value > overlapStart)))
            {
                baselineHolders.Add(b.UserId);
            }
        }

        return activeCleaners
            .Where(u => baselineHolders.Contains(u.Id)
                || (!periodsByUser.ContainsKey(u.Id) && u.AreaId == areaId))
            .Select(u => u.DisplayName)
            .ToList();
    }

    private static bool WeekdayOccursInRange(DayOfWeek weekday, DateOnly start, DateOnly? end)
    {
        if (end is not DateOnly until)
        {
            return true;
        }

        var span = until.DayNumber - start.DayNumber;
        if (span >= 7)
        {
            return true;
        }

        var offset = ((int)weekday - (int)start.DayOfWeek + 7) % 7;
        return offset < span;
    }
}
