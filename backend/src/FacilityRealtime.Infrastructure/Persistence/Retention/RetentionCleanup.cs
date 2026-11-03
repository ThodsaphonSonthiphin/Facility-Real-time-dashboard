using FacilityRealtime.Application.Common;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence.Retention;

public sealed partial class RetentionRunner
{
    private async Task<int> DeleteAttendanceAsync(DateTime cutoff, CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var ids = await db.ShiftAttendances.Where(x => x.OccurredAt <= cutoff).OrderBy(x => x.Id)
                .Select(x => x.Id).Take(settings.BatchSize).ToListAsync(ct);
            if (ids.Count == 0) return total;
            var rows = db.ShiftAttendances.Where(x => ids.Contains(x.Id) && x.OccurredAt <= cutoff);
            await DeleteDependentsAsync(rows.Select(x => x.Id), AlertType.AttendanceGps, "shift_attendances", ct);
            total += await rows.ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }

    private async Task<int> DeleteBlockedAsync(DateTime cutoff, CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var ids = await db.BlockedScans.Where(x => x.ScannedAt <= cutoff).OrderBy(x => x.Id)
                .Select(x => x.Id).Take(settings.BatchSize).ToListAsync(ct);
            if (ids.Count == 0) return total;
            var rows = db.BlockedScans.Where(x => ids.Contains(x.Id) && x.ScannedAt <= cutoff);
            await DeleteDependentsAsync(rows.Select(x => x.Id), AlertType.BlockedScan, "blocked_scans", ct);
            total += await rows.ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }

    private async Task<int> DeleteCoversAsync(DateTime cutoff, CancellationToken ct)
    {
        var total = 0;
        var thai = ThaiTime.FromUtc(cutoff);
        var date = DateOnly.FromDateTime(thai);
        var lastDay = thai.TimeOfDay >= TimeSpan.FromHours(19) ? date : date.AddDays(-1);
        var lastNight = thai.TimeOfDay >= TimeSpan.FromHours(7) ? date.AddDays(-1) : date.AddDays(-2);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var ids = await db.CoverAssignments.Where(c => ((c.Shift == Shift.Day && c.ShiftDate <= lastDay) || (c.Shift == Shift.Night && c.ShiftDate <= lastNight))
                && c.AssignedAt <= cutoff
                && !db.ScanRecords.Any(s => s.CoverAssignmentId == c.Id) && !db.InspectionRecords.Any(i => i.CoverAssignmentId == c.Id))
                .OrderBy(c => c.Id).Select(c => c.Id).Take(settings.BatchSize).ToListAsync(ct);
            if (ids.Count == 0) return total;
            var auditIds = ids.Select(id => (long)id).ToList();
            await db.AuditLog.Where(a => a.EntityType == "cover_assignments" && a.EntityId != null && auditIds.Contains(a.EntityId.Value)).ExecuteDeleteAsync(ct);
            total += await db.CoverAssignments.Where(c => ids.Contains(c.Id)).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }

    private async Task<int> DeleteTokensAsync(DateTime cutoff, CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var ids = await db.RefreshTokens.Where(t => t.ExpiresAt <= cutoff).OrderBy(t => t.Id)
                .Select(t => t.Id).Take(settings.BatchSize).ToListAsync(ct);
            if (ids.Count == 0) return total;
            total += await db.RefreshTokens.Where(t => ids.Contains(t.Id) && t.ExpiresAt <= cutoff).ExecuteDeleteAsync(ct);
        }
    }
}
