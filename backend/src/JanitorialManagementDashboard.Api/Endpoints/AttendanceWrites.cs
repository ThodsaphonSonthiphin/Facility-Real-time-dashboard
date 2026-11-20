using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>
/// Spec §8 and §11: one actual fact, the skip markers recorded with it and the resolution of a stored gap for the very
/// action being recorded commit together or not at all. The scan route and the scoped missing-time correction both write
/// through here, so the two can never disagree about what a skip means or leave a half-written slot behind.
/// </summary>
internal static class AttendanceWrites
{
    /// <summary>What one attempted write did: whether it stored the fact, and the slot's facts afterwards.</summary>
    internal sealed record Written(bool Stored, IReadOnlyList<ShiftAttendance> Facts);

    /// <summary>
    /// Records <paramref name="fact"/> with the confirmed skip markers in one transaction. A stored gap for the recorded
    /// action becomes <see cref="AttendanceGapStatus.Resolved"/>, because the real time now answers it. The unique index on
    /// the actual facts keeps the first of two racing writers: the loser writes nothing at all — no skip marker, no
    /// resolution — and is answered with the facts as they stand.
    /// <para>
    /// <paramref name="also"/> runs inside the same transaction once the fact has its identity, which is where the scoped
    /// correction writes its <c>ATTENDANCE_ADD</c> audit row: the fact, the skip markers, the resolution and the audit entry
    /// are one commit.
    /// </para>
    /// </summary>
    public static async Task<Written> RecordAsync(
        AppDbContext db,
        ShiftAttendance fact,
        IReadOnlyList<AttendanceEvent> confirmedGaps,
        int? areaId,
        int? recordedById,
        DateTime nowUtc,
        Func<ShiftAttendance, Task>? also = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var slot = new ShiftSlot(fact.ShiftDate, fact.Shift);

        var gaps = await db.AttendanceGaps
            .Where(gap => gap.UserId == fact.UserId && gap.ShiftDate == fact.ShiftDate && gap.Shift == fact.Shift)
            .ToListAsync(ct);

        foreach (var resolved in gaps.Where(gap => gap.EventType == fact.EventType
                     && gap.Status == AttendanceGapStatus.Skipped))
        {
            resolved.Status = AttendanceGapStatus.Resolved;
            resolved.Revision += 1;
            resolved.RecordedById = null;
            resolved.RecordedAt = nowUtc;
        }

        foreach (var action in confirmedGaps)
        {
            var gap = gaps.FirstOrDefault(row => row.EventType == action);
            if (gap is null)
            {
                gap = new AttendanceGap
                {
                    UserId = fact.UserId,
                    ShiftDate = fact.ShiftDate,
                    Shift = fact.Shift,
                    EventType = action,
                    Revision = 1,
                    CreatedAt = nowUtc,
                };
                db.AttendanceGaps.Add(gap);
                gaps.Add(gap);
            }
            else
            {
                gap.Revision += 1;
            }

            gap.Status = AttendanceGapStatus.Skipped;
            gap.AreaId = areaId;
            gap.RecordedById = recordedById;
            gap.RecordedAt = nowUtc;
        }

        db.ShiftAttendances.Add(fact);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another writer stored this action first, which the unique index keeps. Anything else is not ours to explain.
            // See docs/adr/facility-0026-shift-check-in-two-shifts-strict-presence.md.
            db.ChangeTracker.Clear();

            var facts = await AttendanceEndpoints.LoadEventsAsync(db, fact.UserId, slot);
            if (!facts.Any(row => row.EventType == fact.EventType))
            {
                throw;
            }

            return new Written(false, facts);
        }

        if (also is not null)
        {
            await also(fact);
        }

        return new Written(true, await AttendanceEndpoints.LoadEventsAsync(db, fact.UserId, slot));
    }
}
