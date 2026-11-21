using System.Text.Json;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using Microsoft.EntityFrameworkCore;

internal static class OverviewSeed
{
    // Program has checked the runner-owned marker and holds the dataset transaction.
    public static async Task CaptureAsync(AppDbContext db, DateTime baselineUtc, AttendanceSettings policy, CancellationToken ct)
    {
        var state = await OverviewMutation.LockStateAsync(db, ct);
        var head = await db.OverviewTargetRevisions.SingleAsync(ct);
        var startup = JsonSerializer.Deserialize<TargetImage>(head.Payload)!;
        if (head.Id != state.LatestRevisionId || startup.CaptureStartedAtUtc != head.EffectiveAt ||
            startup.Buildings.Count != 1 || startup.Buildings[0].Code != "A" ||
            !startup.Areas.Select(a => a.Code).SequenceEqual(["AR01", "AR02"]))
            throw new InvalidOperationException("Expected only the initial Development target baseline");

        state.LatestRevisionId = null;
        await db.SaveChangesAsync(ct);
        db.Remove(head);
        await db.SaveChangesAsync(ct);
        await new OverviewTargetRecorder(db, policy, new BaselineClock(baselineUtc)).CaptureAsync(ct);
    }

    private sealed class BaselineClock(DateTime instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(instant);
    }

    public static async Task<object> ManifestAsync(AppDbContext db, DateOnly anchor, int days)
    {
        var head = await db.OverviewTargetRevisions.AsNoTracking().SingleAsync();
        var state = await db.OverviewTargetStates.AsNoTracking().SingleAsync();
        if (head.Id != state.LatestRevisionId) throw new InvalidOperationException("Incomplete synthetic target baseline");

        // Evaluate the deterministic fixture recipe independently of the overview projection.
        var day = Math.Min(8, days - 1);
        var building = await db.Buildings.SingleAsync(b => b.Code == "E2EB001");
        var rows = await db.Areas.Where(a => a.BuildingId == building.Id && a.ShiftPattern == ShiftPattern.DayAndNight)
            .OrderBy(a => a.Code).Select(a => new { a.Id, a.Code,
                pointIds = db.ServicePoints.Where(p => p.AreaId == a.Id).Select(p => p.Id).ToArray() }).ToListAsync();
        var entered = rows.Where(a => (int.Parse(a.Code[4..]) - 1 + day) % 11 != 0).ToArray();
        var submitted = entered.Where(a => a.pointIds.All(p => (p + day) % 7 != 0)).ToArray();
        var inspected = submitted.Where(a => a.pointIds.All(p => (p + day) % 5 != 0)).ToArray();
        return new {
            revisionId = head.Id, imageHash = head.PayloadHash, captureStartedAtUtc = head.EffectiveAt,
            firstDate = anchor.AddDays(-days), lastDate = anchor.AddDays(-2),
            emptyDate = anchor.AddDays(-days - 1), preBaselineDate = anchor.AddDays(-days - 3),
            date = anchor.AddDays(-day), shift = "Night", buildingId = building.Id,
            entered = entered.Length, submitted = submitted.Length, inspected = inspected.Length,
            enteredAreas = entered.Select(a => new { id = a.Id, code = a.Code })
        };
    }
}
