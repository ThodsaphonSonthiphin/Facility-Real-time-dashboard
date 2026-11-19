using JanitorialManagementDashboard.Application.Shifts;

namespace JanitorialManagementDashboard.Application.Overview;

public static class OverviewTime
{
    public static OverviewMoment Evaluate(ShiftSlot slot, DateTime now, IReadOnlyList<TargetRevision> revisions)
    {
        var start = ShiftCalendar.StartUtc(slot);
        var end = ShiftCalendar.EndUtc(slot);
        var ordered = revisions.Where(r => r.EffectiveAt <= now)
            .OrderBy(r => r.EffectiveAt).ThenBy(r => r.Id).ToArray();
        var close = ReceivingClose(end, ordered);
        if (now < start) return new("notStarted", now, close, close.HasValue);
        if (now < end) return new("running", now, close, close.HasValue);
        // Without policy coverage at shift end, a historical closure cannot be proved.
        if (close is null) return new("receiving", now, null, false);
        return now <= close.Value
            ? new("receiving", now, close, true)
            : new("closed", close.Value, close, true);
    }

    private static DateTime? ReceivingClose(DateTime end, TargetRevision[] revisions)
    {
        var index = Array.FindLastIndex(revisions, r => r.EffectiveAt <= end);
        if (index < 0) return null;
        while (true)
        {
            var revision = revisions[index];
            var candidate = end.AddMinutes(revision.Image.Policy.ClosesMinutesAfterShift);
            if (candidate < revision.EffectiveAt) candidate = revision.EffectiveAt;

            index++;
            if (index == revisions.Length || candidate < revisions[index].EffectiveAt)
                return candidate;
        }
    }
}
