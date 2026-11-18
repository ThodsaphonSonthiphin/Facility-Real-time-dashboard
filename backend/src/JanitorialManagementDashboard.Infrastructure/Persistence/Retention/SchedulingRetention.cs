using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Retention;

/// <summary>
/// Spec §12 and A27: the new scheduling personal detail joins the existing retention stream. GPS is cleared on its own
/// (shorter) boundary by <see cref="RetentionRunner"/>; this class prunes the detail that the configured detail window no
/// longer needs, and it keeps every configuration record that is still active or future plus the one boundary version
/// that interprets the oldest retained date. Nothing here invents history: once an anchor is gone, reads answer
/// "unavailable" rather than reconstructing a plan from today's account.
/// </summary>
public sealed class SchedulingRetention(AppDbContext db)
{
    /// <summary>
    /// Removes the dated personal detail older than <see cref="RetentionSettings.RecordYears"/> and returns how many parent
    /// rows were deleted. Child rows (baseline days, week choices, transfer confirmations, change rows) go with their
    /// parent and are not counted, so the count means "stored records whose own facts ended".
    /// </summary>
    public async Task<int> PruneAsync(DateTime utcNow, RetentionSettings settings, CancellationToken ct)
    {
        var earliestRetained = DateOnly.FromDateTime(ThaiTime.FromUtc(utcNow.AddYears(-settings.RecordYears)));
        var cutoff = utcNow.AddYears(-settings.RecordYears);

        var deleted = await PruneBaselinesAsync(earliestRetained, ct);
        deleted += await PruneWeeklyDaysOffAsync(earliestRetained, ct);
        deleted += await PruneDatedDetailAsync(earliestRetained, ct);
        deleted += await PruneReceiptsAsync(cutoff, ct);
        return deleted;
    }

    /// <summary>
    /// Superseded baseline versions disappear once their whole effective range is older than the retained detail, but the
    /// latest predecessor at or before the boundary survives: it is the version that explains the state on the oldest
    /// retained date. An open or future version is never touched.
    /// </summary>
    private async Task<int> PruneBaselinesAsync(DateOnly earliestRetained, CancellationToken ct)
    {
        var versions = await db.ScheduleBaselines.AsNoTracking()
            .Select(baseline => new
            {
                baseline.Id,
                baseline.UserId,
                baseline.Kind,
                baseline.EffectiveFrom,
                baseline.EffectiveUntil,
            })
            .ToListAsync(ct);

        var obsolete = new List<int>();
        foreach (var group in versions.GroupBy(version => (version.UserId, version.Kind)))
        {
            var ordered = group.OrderBy(version => version.EffectiveFrom).ToList();
            var predecessor = ordered.LastOrDefault(version => version.EffectiveFrom <= earliestRetained);
            foreach (var version in ordered)
            {
                if (!SchedulingRetentionRules.KeepBaselineAnchor(
                        version.EffectiveFrom, version.EffectiveUntil, earliestRetained)
                    && version.Id != predecessor?.Id)
                {
                    obsolete.Add(version.Id);
                }
            }
        }

        if (obsolete.Count == 0)
        {
            return 0;
        }

        await db.ScheduleBaselineDays.Where(day => obsolete.Contains(day.BaselineId)).ExecuteDeleteAsync(ct);
        return await db.ScheduleBaselines.Where(baseline => obsolete.Contains(baseline.Id)).ExecuteDeleteAsync(ct);
    }

    /// <summary>Whole weeks whose seven dates are all older than the retained detail; a week that still touches it stays.</summary>
    private async Task<int> PruneWeeklyDaysOffAsync(DateOnly earliestRetained, CancellationToken ct)
    {
        var lastObsoleteWeekStart = earliestRetained.AddDays(-6);
        var obsolete = db.WeeklyDayOffs.Where(week => week.WeekStart < lastObsoleteWeekStart);
        await db.WeeklyDayOffChoices
            .Where(choice => obsolete.Any(week => week.Id == choice.WeeklyDayOffId))
            .ExecuteDeleteAsync(ct);
        return await obsolete.ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// The dated plan, relief and transfer facts older than the retained detail. A transfer's confirmation is deleted
    /// first because its instruction is what it belongs to; the arrival's own GPS was already cleared on the shorter
    /// policy, which is why losing it here means losing the retained fact, not the location.
    /// </summary>
    private async Task<int> PruneDatedDetailAsync(DateOnly earliestRetained, CancellationToken ct)
    {
        var deleted = await db.ScheduleExceptions
            .Where(exception => exception.ShiftDate < earliestRetained)
            .ExecuteDeleteAsync(ct);
        deleted += await db.ReliefAssignments
            .Where(assignment => assignment.ShiftDate < earliestRetained)
            .ExecuteDeleteAsync(ct);

        var obsoleteTransfers = db.AreaTransfers.Where(transfer => transfer.ShiftDate < earliestRetained);
        await db.AreaTransferConfirmations
            .Where(confirmation => obsoleteTransfers.Any(transfer => transfer.Id == confirmation.TransferId))
            .ExecuteDeleteAsync(ct);
        deleted += await obsoleteTransfers.ExecuteDeleteAsync(ct);
        return deleted;
    }

    /// <summary>
    /// The audit rows and retained command results. The receipt is what a client's identity is looked up in, so it is
    /// removed only after its token's 24-hour start window is long past; an expired identity is refused with or without a
    /// receipt, which is what keeps a purge from becoming a second application.
    /// </summary>
    private async Task<int> PruneReceiptsAsync(DateTime cutoff, CancellationToken ct)
    {
        // The audit rows are selected through their receipt, not by their own timestamp: a change row cannot outlive the
        // receipt that owns it, and deleting by the parent's age is what keeps the restricted foreign key satisfiable.
        var obsolete = db.ScheduleCommandReceipts.Where(receipt => receipt.CommittedAt < cutoff);
        await db.ScheduleChanges
            .Where(change => obsolete.Any(receipt => receipt.Id == change.ReceiptId))
            .ExecuteDeleteAsync(ct);
        return await db.ScheduleCommandReceipts
            .Where(receipt => receipt.CommittedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
