using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Attendance;

public sealed record AttendanceChoice(AttendanceEvent Action, IReadOnlyList<AttendanceEvent> RequiredGaps);

public static class AttendanceLifecycle
{
    public static IReadOnlyList<AttendanceEvent> Order { get; } = Array.AsReadOnly<AttendanceEvent>(
        [AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break1In,
            AttendanceEvent.Break2Out, AttendanceEvent.Break2In, AttendanceEvent.ShiftOut]);

    public static bool IsSkippable(AttendanceEvent action) =>
        action is not (AttendanceEvent.ShiftIn or AttendanceEvent.ShiftOut);

    public static IReadOnlyList<AttendanceChoice> NextActions(
        IReadOnlyCollection<AttendanceEvent> recorded, IReadOnlyCollection<AttendanceEvent> skipped)
    {
        var confirmed = skipped.Where(IsSkippable).ToHashSet();

        var order = Order;
        if (recorded.Contains(AttendanceEvent.ShiftOut))
        {
            return [];
        }

        if (!recorded.Contains(AttendanceEvent.ShiftIn))
        {
            return [new AttendanceChoice(AttendanceEvent.ShiftIn, [])];
        }

        var choices = new List<AttendanceChoice>(order.Count - 1);
        var latestRecorded = order.Select((action, index) => recorded.Contains(action) ? index : 0).Max();
        for (var index = latestRecorded + 1; index < order.Count; index++)
        {
            var action = order[index];
            if (recorded.Contains(action) || confirmed.Contains(action))
            {
                continue;
            }

            var gaps = order.Take(index)
                .Where(preceding => !recorded.Contains(preceding) && !confirmed.Contains(preceding))
                .ToArray();
            choices.Add(new AttendanceChoice(action, Array.AsReadOnly(gaps)));
        }

        return choices;
    }
}
