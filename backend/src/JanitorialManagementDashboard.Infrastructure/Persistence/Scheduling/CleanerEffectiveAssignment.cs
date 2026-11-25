using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;

/// <summary>
/// A Cleaner's effective dated identity and regular duty Area on a specific date, resolved from
/// <see cref="CleanerProfilePeriod"/> and <see cref="ScheduleBaseline"/> (and optionally dated Area overrides),
/// falling back to mutable <see cref="User"/> columns only for pre-backfill Cleaners with no dated profile at all
/// (spec §14).
/// </summary>
public sealed record CleanerEffectiveState(
    CleanerKind? Kind,
    int? HomeBuildingId,
    Shift? Shift,
    int? AreaId,
    bool HasDatedIdentity,
    bool IsDayOff = false,
    bool IsWholeShiftLeave = false);

/// <summary>
/// Single shared read helper for resolving active Cleaners' effective shift, kind, home building, and Area on a date,
/// preventing drift between dated scheduling records and legacy <c>users.AreaId</c>/<c>users.Shift</c> projections.
/// </summary>
public static class CleanerEffectiveAssignment
{
    /// <summary>
    /// Resolves effective state for each Cleaner in <paramref name="cleaners"/> on <paramref name="date"/>.
    /// When <paramref name="useFallbackWeekdayArea"/> is true (account/area setup views), if <paramref name="date"/>'s
    /// weekday is a day off in the baseline, the first non-day-off weekday's Area in that baseline is returned so a
    /// Cleaner's regular Area still appears on their day off. When false (operational duty views), only the Area for
    /// <paramref name="date"/>'s actual weekday (or dated override when <paramref name="includeDatedOverrides"/> is true)
    /// is returned.
    /// </summary>
    public static async Task<Dictionary<int, CleanerEffectiveState>> ResolveForDateAsync(
        AppDbContext db,
        IReadOnlyList<User> cleaners,
        DateOnly date,
        bool useFallbackWeekdayArea,
        bool includeDatedOverrides = false,
        CancellationToken ct = default)
    {
        var result = new Dictionary<int, CleanerEffectiveState>(cleaners.Count);
        var cleanerList = cleaners.Where(u => u.Role == UserRole.Cleaner).ToList();
        if (cleanerList.Count == 0)
        {
            return result;
        }

        var ids = cleanerList.Select(u => u.Id).Distinct().ToList();
        var profilesByUser = (await db.CleanerProfilePeriods.AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .OrderBy(p => p.EffectiveFrom).ThenBy(p => p.Id)
            .ToListAsync(ct))
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var baselinesByUser = (await db.ScheduleBaselines.AsNoTracking()
            .Include(b => b.Days)
            .Where(b => ids.Contains(b.UserId)
                && b.Kind == BaselineKind.Regular
                && b.EffectiveFrom <= date
                && (b.EffectiveUntil == null || date < b.EffectiveUntil.Value))
            .OrderBy(b => b.EffectiveFrom).ThenBy(b => b.Id)
            .ToListAsync(ct))
            .GroupBy(b => b.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<(int UserId, Shift Shift), int?>? datedAreas = null;
        if (includeDatedOverrides)
        {
            var overrides = await db.ScheduleExceptions.AsNoTracking()
                .Where(e => ids.Contains(e.UserId)
                    && e.ShiftDate == date
                    && e.Kind == ScheduleExceptionKind.Area
                    && !e.IsCancelled)
                .OrderBy(e => e.Id)
                .ToListAsync(ct);
            datedAreas = overrides
                .GroupBy(e => (e.UserId, e.Shift))
                .ToDictionary(g => g.Key, g => g.Last().AreaId);
        }

        var chosenOffUsers = (await db.WeeklyDayOffChoices.AsNoTracking()
            .Where(c => c.OffDate == date && ids.Contains(c.WeeklyDayOff!.UserId))
            .Select(c => c.WeeklyDayOff!.UserId)
            .Distinct()
            .ToListAsync(ct))
            .ToHashSet();

        var leavesByUser = (await db.ScheduleExceptions.AsNoTracking()
            .Where(e => ids.Contains(e.UserId)
                && e.ShiftDate == date
                && e.Kind == ScheduleExceptionKind.Leave
                && !e.IsCancelled
                && e.StartsAt != null
                && e.EndsAt != null)
            .Select(e => new { e.UserId, e.Shift, StartsAt = e.StartsAt!.Value, EndsAt = e.EndsAt!.Value })
            .ToListAsync(ct))
            .ToLookup(e => (e.UserId, e.Shift));

        bool HasWholeShiftLeave(int userId, Shift? shift)
        {
            if (shift is not { } s)
            {
                return false;
            }

            var slot = new ShiftSlot(date, s);
            var startUtc = ShiftCalendar.StartUtc(slot);
            var endUtc = ShiftCalendar.EndUtc(slot);
            return leavesByUser[(userId, s)].Any(l =>
                DateTime.SpecifyKind(l.StartsAt, DateTimeKind.Utc) <= startUtc
                && DateTime.SpecifyKind(l.EndsAt, DateTimeKind.Utc) >= endUtc);
        }

        var fallbackAreaIds = cleanerList
            .Where(u => u.AreaId is not null && u.Area is null)
            .Select(u => u.AreaId!.Value)
            .Distinct()
            .ToList();
        var areaBuildings = fallbackAreaIds.Count == 0
            ? new Dictionary<int, int>()
            : await db.Areas.AsNoTracking()
                .Where(a => fallbackAreaIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.BuildingId, ct);

        foreach (var user in cleanerList)
        {
            profilesByUser.TryGetValue(user.Id, out var userProfiles);
            var hasDatedIdentity = userProfiles is { Count: > 0 };
            var chosenOff = chosenOffUsers.Contains(user.Id);

            if (!hasDatedIdentity)
            {
                var compatHome = user.Area?.BuildingId
                    ?? (user.AreaId is int aId && areaBuildings.TryGetValue(aId, out var bId) ? bId : user.BuildingId);
                result[user.Id] = new CleanerEffectiveState(
                    user.AreaId is not null ? CleanerKind.Regular : null,
                    compatHome,
                    user.Shift,
                    user.AreaId,
                    HasDatedIdentity: false,
                    IsDayOff: chosenOff,
                    IsWholeShiftLeave: HasWholeShiftLeave(user.Id, user.Shift));
                continue;
            }

            var activeProfile = userProfiles!.LastOrDefault(p =>
                ScheduleResolver.Applies(p.EffectiveFrom, p.EffectiveUntil, date));
            var kind = activeProfile?.Kind;
            var shift = activeProfile?.Shift;
            var homeBuildingId = activeProfile?.HomeBuildingId;

            int? areaId = null;
            var hasDatedAreaOverride = false;
            var baselineOff = false;
            if (kind == CleanerKind.Regular)
            {
                if (datedAreas is not null && shift is Shift s && datedAreas.TryGetValue((user.Id, s), out var datedArea) && datedArea is int overrideAreaId)
                {
                    areaId = overrideAreaId;
                    hasDatedAreaOverride = true;
                }

                if (baselinesByUser.TryGetValue(user.Id, out var userBaselines) && userBaselines.Count > 0)
                {
                    var activeBaseline = userBaselines[^1];
                    var todayCell = activeBaseline.Days.FirstOrDefault(d => d.Weekday == date.DayOfWeek);
                    baselineOff = todayCell?.IsDayOff == true;
                    if (!hasDatedAreaOverride)
                    {
                        if (todayCell is { IsDayOff: false, AreaId: int todayArea })
                        {
                            areaId = todayArea;
                        }
                        else if (useFallbackWeekdayArea)
                        {
                            areaId = activeBaseline.Days
                                .OrderBy(d => d.Weekday)
                                .FirstOrDefault(d => !d.IsDayOff && d.AreaId != null)?.AreaId;
                        }
                    }
                }
            }

            var isDayOff = chosenOff || (baselineOff && !hasDatedAreaOverride);
            var isWholeShiftLeave = HasWholeShiftLeave(user.Id, shift);

            result[user.Id] = new CleanerEffectiveState(
                kind,
                homeBuildingId,
                shift,
                areaId,
                HasDatedIdentity: true,
                IsDayOff: isDayOff,
                IsWholeShiftLeave: isWholeShiftLeave);
        }

        return result;
    }

    public static async Task<CleanerEffectiveState> ResolveOneAsync(
        AppDbContext db,
        User cleaner,
        DateOnly date,
        bool useFallbackWeekdayArea,
        bool includeDatedOverrides = false,
        CancellationToken ct = default)
    {
        var map = await ResolveForDateAsync(db, [cleaner], date, useFallbackWeekdayArea, includeDatedOverrides, ct);
        return map.TryGetValue(cleaner.Id, out var state)
            ? state
            : new CleanerEffectiveState(null, null, cleaner.Shift, cleaner.AreaId, false);
    }
}
