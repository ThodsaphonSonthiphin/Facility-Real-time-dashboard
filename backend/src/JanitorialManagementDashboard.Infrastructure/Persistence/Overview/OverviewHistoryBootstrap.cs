using JanitorialManagementDashboard.Application.Attendance;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Overview;

public static class OverviewHistoryBootstrap
{
    public static async Task<long> EnsureAsync(AppDbContext db, AttendanceSettings settings, TimeProvider clock, CancellationToken ct)
    {
        var recorder = new OverviewTargetRecorder(db, settings, clock);
        await using var change = await new OverviewMutation(db, recorder).BeginAsync(ct, capturePreMutation: false);
        var headId = db.OverviewTargetStates.Local.Single().LatestRevisionId;
        if (headId == null)
            return await change.CommitAsync(ct);

        var head = await db.OverviewTargetRevisions.SingleAsync(r => r.Id == headId, ct);
        var previous = OverviewTargetRecorder.Deserialize(head);
        var current = await recorder.ReadImageAsync(ct);
        // Offline configuration edits have no known effective time. Only structural differences open a gap.
        var sameStructure = OverviewTargetRecorder.SameTargets(current, previous with { Policy = current.Policy });
        return await change.CommitAsync(ct, sameStructure ? null : head.EffectiveAt, current);
    }
}
