using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;

/// <summary>
/// Spec §14 and ADR facility-0215: record the setup that is known today as the first effective-dated version. It never
/// invents a relief identity, a past day off, a break group or a Supervisor the current setup does not hold. The
/// migration itself stays schema-only; this explicit compatibility step runs once with the configured history start.
/// </summary>
public static class ScheduleBackfill
{
    /// <summary>
    /// Creates the open regular profile, regular weekday baseline and Supervisor building scope for every active person
    /// whose current Area/Building and Shift are known, effective from <paramref name="historyStartsAt"/>. Safe to run
    /// more than once: a person who already has any scheduling version is left alone.
    /// </summary>
    public static async Task EnsureAsync(AppDbContext db, DateOnly historyStartsAt, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var profiled = await db.CleanerProfilePeriods.Select(x => x.UserId).ToHashSetAsync(ct);
        var baselined = await db.ScheduleBaselines.Select(x => x.UserId).ToHashSetAsync(ct);
        var scoped = await db.SupervisorScopePeriods.Select(x => x.SupervisorId).ToHashSetAsync(ct);

        var cleaners = await db.Users.AsNoTracking()
            .Include(u => u.Area)
            .Where(u => u.Role == UserRole.Cleaner && u.IsActive && (u.AreaId != null || u.BuildingId != null))
            .ToListAsync(ct);
        foreach (var cleaner in cleaners)
        {
            var homeBuildingId = cleaner.Area?.BuildingId ?? cleaner.BuildingId;
            if (!profiled.Contains(cleaner.Id) && homeBuildingId is int homeId)
            {
                db.CleanerProfilePeriods.Add(new CleanerProfilePeriod
                {
                    UserId = cleaner.Id,
                    Kind = CleanerKind.Regular,
                    HomeBuildingId = homeId,
                    Shift = cleaner.Shift,
                    EffectiveFrom = historyStartsAt,
                    Revision = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            if (!baselined.Contains(cleaner.Id) && cleaner.AreaId is int areaId)
            {
                var baseline = new ScheduleBaseline
                {
                    UserId = cleaner.Id,
                    Kind = BaselineKind.Regular,
                    EffectiveFrom = historyStartsAt,
                    Revision = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                foreach (var weekday in Enum.GetValues<DayOfWeek>())
                {
                    baseline.Days.Add(new ScheduleBaselineDay
                    {
                        Weekday = weekday,
                        AreaId = areaId,
                        BreakGroup = null,
                        IsDayOff = false,
                    });
                }

                db.ScheduleBaselines.Add(baseline);
            }
        }

        var supervisors = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId != null && u.Shift != null)
            .ToListAsync(ct);
        foreach (var supervisor in supervisors.Where(s => !scoped.Contains(s.Id)))
        {
            db.SupervisorScopePeriods.Add(new SupervisorScopePeriod
            {
                SupervisorId = supervisor.Id,
                BuildingId = supervisor.BuildingId!.Value,
                Shift = supervisor.Shift!.Value,
                EffectiveFrom = historyStartsAt,
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
