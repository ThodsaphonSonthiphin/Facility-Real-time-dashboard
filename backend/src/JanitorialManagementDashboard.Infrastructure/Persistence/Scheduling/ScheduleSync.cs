using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;

/// <summary>
/// Idempotently synchronises a Cleaner's dated <see cref="CleanerProfilePeriod"/> and regular
/// <see cref="ScheduleBaseline"/> inside the caller's active transaction so Account and Area mutations never drift from
/// scheduling projections.
/// </summary>
public static class ScheduleSync
{
    public static async Task SyncCleanerAsync(
        AppDbContext db,
        ScheduleStore store,
        User cleaner,
        int? homeBuildingId,
        CleanerKind kind,
        int? actorId,
        DateOnly effectiveFrom,
        DateTime nowUtc,
        CancellationToken ct = default,
        int? clearFutureAreaId = null,
        bool preserveExistingShift = false)
    {
        if (cleaner.Role != UserRole.Cleaner)
        {
            return;
        }

        var anchor = await store.LockBaselineHeadAsync(cleaner.Id, nowUtc, ct, advanceRevision: false);

        var periods = await db.CleanerProfilePeriods
            .Where(p => p.UserId == cleaner.Id)
            .OrderBy(p => p.EffectiveFrom)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

        if (periods.Count == 0 && homeBuildingId is null && cleaner.AreaId is null && kind == CleanerKind.Regular)
        {
            return;
        }

        SyncProfile(db, periods, cleaner, homeBuildingId, kind, actorId, effectiveFrom, nowUtc, preserveExistingShift);

        if (kind == CleanerKind.Regular)
        {
            await SyncRegularBaselineAsync(db, anchor, cleaner, actorId, effectiveFrom, nowUtc, clearFutureAreaId, ct);
        }
        else if (kind == CleanerKind.Relief)
        {
            // Switching a Cleaner to Relief from effectiveFrom onwards must also clear any future regular baselines so
            // they cannot keep blocking Area deactivation or resurrect an Area if switched back to Regular later.
            await ClearRegularBaselinesAsync(db, anchor, cleaner, actorId, effectiveFrom, nowUtc,
                clearAllFuture: true, clearFutureAreaId: null, ct: ct);
        }
    }

    private static void SyncProfile(
        AppDbContext db,
        List<CleanerProfilePeriod> periods,
        User cleaner,
        int? homeBuildingId,
        CleanerKind kind,
        int? actorId,
        DateOnly effectiveFrom,
        DateTime nowUtc,
        bool preserveExistingShift)
    {
        var current = periods.LastOrDefault(p =>
            ScheduleResolver.Applies(p.EffectiveFrom, p.EffectiveUntil, effectiveFrom));
        var sameDay = periods.FirstOrDefault(p => p.EffectiveFrom == effectiveFrom);
        var targetShift = preserveExistingShift
            ? sameDay?.Shift ?? current?.Shift ?? cleaner.Shift
            : cleaner.Shift;

        if (sameDay is not null)
        {
            if (sameDay.Kind == kind && sameDay.HomeBuildingId == homeBuildingId && sameDay.Shift == targetShift)
            {
                return;
            }

            sameDay.Kind = kind;
            sameDay.HomeBuildingId = homeBuildingId;
            sameDay.Shift = targetShift;
            sameDay.Revision += 1;
            sameDay.UpdatedAt = nowUtc;
            sameDay.CreatedById ??= actorId;
            return;
        }

        if (current is not null
            && current.Kind == kind
            && current.HomeBuildingId == homeBuildingId
            && current.Shift == targetShift)
        {
            return;
        }

        if (current is not null && (current.EffectiveUntil is null || current.EffectiveUntil > effectiveFrom))
        {
            current.EffectiveUntil = effectiveFrom;
            current.Revision += 1;
            current.UpdatedAt = nowUtc;
        }

        db.CleanerProfilePeriods.Add(new CleanerProfilePeriod
        {
            UserId = cleaner.Id,
            Kind = kind,
            HomeBuildingId = homeBuildingId,
            Shift = targetShift,
            EffectiveFrom = effectiveFrom,
            EffectiveUntil = periods.Where(p => p.EffectiveFrom > effectiveFrom)
                .Select(p => (DateOnly?)p.EffectiveFrom)
                .Min(),
            Revision = 1,
            CreatedById = actorId,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
        });
    }

    private static async Task SyncRegularBaselineAsync(
        AppDbContext db,
        ScheduleBaselineAnchor anchor,
        User cleaner,
        int? actorId,
        DateOnly effectiveFrom,
        DateTime nowUtc,
        int? clearFutureAreaId,
        CancellationToken ct)
    {
        var baselines = await db.ScheduleBaselines
            .Include(b => b.Days)
            .Where(b => b.UserId == cleaner.Id && b.Kind == BaselineKind.Regular)
            .OrderBy(b => b.EffectiveFrom)
            .ThenBy(b => b.Id)
            .ToListAsync(ct);

        var currentBaseline = baselines.LastOrDefault(b =>
            ScheduleResolver.Applies(b.EffectiveFrom, b.EffectiveUntil, effectiveFrom));
        var sameDayBaseline = baselines.FirstOrDefault(b => b.EffectiveFrom == effectiveFrom);

        if (cleaner.IsActive && cleaner.AreaId is int targetAreaId)
        {
            if (sameDayBaseline is not null)
            {
                var changed = false;
                foreach (var day in sameDayBaseline.Days.Where(d => !d.IsDayOff && d.AreaId != targetAreaId))
                {
                    day.AreaId = targetAreaId;
                    changed = true;
                }

                if (changed)
                {
                    sameDayBaseline.Revision += 1;
                    sameDayBaseline.UpdatedAt = nowUtc;
                    anchor.Revision += 1;
                    anchor.UpdatedAt = nowUtc;
                }

                return;
            }

            if (currentBaseline is null || currentBaseline.Days.Any(d => !d.IsDayOff && d.AreaId != targetAreaId))
            {
                if (currentBaseline is not null
                    && (currentBaseline.EffectiveUntil is null || currentBaseline.EffectiveUntil > effectiveFrom))
                {
                    currentBaseline.EffectiveUntil = effectiveFrom;
                    currentBaseline.Revision += 1;
                    currentBaseline.UpdatedAt = nowUtc;
                }

                var created = new ScheduleBaseline
                {
                    UserId = cleaner.Id,
                    Kind = BaselineKind.Regular,
                    EffectiveFrom = effectiveFrom,
                    EffectiveUntil = baselines.Where(b => b.EffectiveFrom > effectiveFrom)
                        .Select(b => (DateOnly?)b.EffectiveFrom)
                        .Min(),
                    Revision = 1,
                    CreatedById = actorId,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc,
                };

                foreach (var weekday in Enum.GetValues<DayOfWeek>())
                {
                    var prior = currentBaseline?.Days.FirstOrDefault(d => d.Weekday == weekday);
                    var isDayOff = prior?.IsDayOff ?? false;
                    created.Days.Add(new ScheduleBaselineDay
                    {
                        Weekday = weekday,
                        AreaId = isDayOff ? null : targetAreaId,
                        BreakGroup = isDayOff ? null : prior?.BreakGroup,
                        IsDayOff = isDayOff,
                    });
                }

                db.ScheduleBaselines.Add(created);
                anchor.Revision += 1;
                anchor.UpdatedAt = nowUtc;
            }

            return;
        }

        // Cleaner is inactive or has no regular Area: clear working AreaId/BreakGroup while keeping IsDayOff intact.
        // When deactivated, also clear all future regular baselines; when removed from a specific Area, clear only days
        // pointing to that Area in future baselines so an Area replacement cannot leave two active owners.
        await ClearRegularBaselinesAsync(db, anchor, cleaner, actorId, effectiveFrom, nowUtc,
            clearAllFuture: !cleaner.IsActive, clearFutureAreaId: clearFutureAreaId, ct: ct,
            loadedBaselines: baselines, currentBaseline: currentBaseline, sameDayBaseline: sameDayBaseline);
    }

    private static async Task ClearRegularBaselinesAsync(
        AppDbContext db,
        ScheduleBaselineAnchor anchor,
        User cleaner,
        int? actorId,
        DateOnly effectiveFrom,
        DateTime nowUtc,
        bool clearAllFuture,
        int? clearFutureAreaId,
        CancellationToken ct,
        List<ScheduleBaseline>? loadedBaselines = null,
        ScheduleBaseline? currentBaseline = null,
        ScheduleBaseline? sameDayBaseline = null)
    {
        var baselines = loadedBaselines ?? await db.ScheduleBaselines
            .Include(b => b.Days)
            .Where(b => b.UserId == cleaner.Id && b.Kind == BaselineKind.Regular)
            .OrderBy(b => b.EffectiveFrom)
            .ThenBy(b => b.Id)
            .ToListAsync(ct);

        currentBaseline ??= baselines.LastOrDefault(b =>
            ScheduleResolver.Applies(b.EffectiveFrom, b.EffectiveUntil, effectiveFrom));
        sameDayBaseline ??= baselines.FirstOrDefault(b => b.EffectiveFrom == effectiveFrom);

        var anchorDirty = false;
        if (sameDayBaseline is not null)
        {
            var changed = false;
            foreach (var day in sameDayBaseline.Days.Where(d => !d.IsDayOff && (d.AreaId != null || d.BreakGroup != null)))
            {
                day.AreaId = null;
                day.BreakGroup = null;
                changed = true;
            }

            if (changed)
            {
                sameDayBaseline.Revision += 1;
                sameDayBaseline.UpdatedAt = nowUtc;
                anchorDirty = true;
            }
        }
        else if (currentBaseline is not null && currentBaseline.Days.Any(d => !d.IsDayOff && d.AreaId != null))
        {
            if (currentBaseline.EffectiveUntil is null || currentBaseline.EffectiveUntil > effectiveFrom)
            {
                currentBaseline.EffectiveUntil = effectiveFrom;
                currentBaseline.Revision += 1;
                currentBaseline.UpdatedAt = nowUtc;
            }

            var cleared = new ScheduleBaseline
            {
                UserId = cleaner.Id,
                Kind = BaselineKind.Regular,
                EffectiveFrom = effectiveFrom,
                EffectiveUntil = baselines.Where(b => b.EffectiveFrom > effectiveFrom)
                    .Select(b => (DateOnly?)b.EffectiveFrom)
                    .Min(),
                Revision = 1,
                CreatedById = actorId,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc,
            };

            foreach (var weekday in Enum.GetValues<DayOfWeek>())
            {
                var prior = currentBaseline.Days.FirstOrDefault(d => d.Weekday == weekday);
                cleared.Days.Add(new ScheduleBaselineDay
                {
                    Weekday = weekday,
                    AreaId = null,
                    BreakGroup = null,
                    IsDayOff = prior?.IsDayOff ?? false,
                });
            }

            db.ScheduleBaselines.Add(cleared);
            anchorDirty = true;
        }

        if (clearAllFuture || clearFutureAreaId is not null)
        {
            foreach (var future in baselines.Where(b => b.EffectiveFrom > effectiveFrom))
            {
                var changed = false;
                foreach (var day in future.Days.Where(d => !d.IsDayOff
                    && (clearAllFuture
                        ? (d.AreaId != null || d.BreakGroup != null)
                        : d.AreaId == clearFutureAreaId)))
                {
                    day.AreaId = null;
                    day.BreakGroup = null;
                    changed = true;
                }

                if (changed)
                {
                    future.Revision += 1;
                    future.UpdatedAt = nowUtc;
                    anchorDirty = true;
                }
            }
        }

        if (anchorDirty)
        {
            anchor.Revision += 1;
            anchor.UpdatedAt = nowUtc;
        }
    }
}
