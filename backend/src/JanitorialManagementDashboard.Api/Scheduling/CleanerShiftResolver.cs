using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Resolves a Cleaner's effective <see cref="Shift"/> or <see cref="ShiftSlot"/> for a date or UTC instant from their
/// dated <see cref="Domain.Entities.Scheduling.CleanerProfilePeriod"/> rows, falling back to <see cref="User.Shift"/>
/// only when no profile period covers that date.
/// </summary>
public static class CleanerShiftResolver
{
    public static async Task<Shift?> ResolveShiftAsync(
        AppDbContext db, User user, DateOnly slotDate, CancellationToken ct = default)
    {
        var period = await db.CleanerProfilePeriods.AsNoTracking()
            .Where(p => p.UserId == user.Id
                && p.EffectiveFrom <= slotDate
                && (p.EffectiveUntil == null || slotDate < p.EffectiveUntil.Value))
            .OrderBy(p => p.EffectiveFrom).ThenBy(p => p.Id)
            .LastOrDefaultAsync(ct);

        return period is not null ? period.Shift : user.Shift;
    }

    public static async Task<ShiftSlot?> ResolveSlotAsync(
        AppDbContext db, User user, DateTime nowUtc, AttendanceSettings settings, CancellationToken ct = default)
    {
        var thaiDate = DateOnly.FromDateTime(ThaiTime.FromUtc(nowUtc));
        var windowStart = thaiDate.AddDays(-1);
        var windowEnd = thaiDate.AddDays(1);
        var periods = await db.CleanerProfilePeriods.AsNoTracking()
            .Where(p => p.UserId == user.Id
                && p.EffectiveFrom <= windowEnd
                && (p.EffectiveUntil == null || p.EffectiveUntil.Value > windowStart))
            .OrderBy(p => p.EffectiveFrom).ThenBy(p => p.Id)
            .ToListAsync(ct);

        foreach (var date in new[] { windowStart, thaiDate, windowEnd })
        {
            var period = periods.LastOrDefault(p => ScheduleResolver.Applies(p.EffectiveFrom, p.EffectiveUntil, date));
            var shift = period is not null ? period.Shift : user.Shift;
            if (shift is not { } s)
            {
                continue;
            }

            var slot = new ShiftSlot(date, s);
            var (opens, closes) = AttendanceCalendar.Window(slot, settings);
            if (nowUtc >= opens && nowUtc <= closes)
            {
                return slot;
            }
        }

        return null;
    }
}
