using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Attendance;

/// <summary>
/// Spec §8 (ADR facility-0222): one action a Cleaner may record next, with the intermediate <see cref="RequiredGaps"/> that
/// reaching it would step over. The Cleaner confirms those gaps from the latest revision and this surface records skip
/// markers — <c>ส่ง skipped markers ไม่ใช่ guessed times</c> — so a skipped break never invents a paired time.
/// </summary>
public sealed record AttendanceChoice(AttendanceEvent Action, IReadOnlyList<AttendanceEvent> RequiredGaps);

/// <summary>
/// Spec §8 (ADR facility-0222): the ordered action list of one dated slot, what may be recorded next, and the precondition
/// for choosing a slot's version. Legacy = ShiftIn/BreakOut/BreakIn/ShiftOut; numbered =
/// ShiftIn/Break1Out/Break1In/Break2Out/Break2In/ShiftOut. A slot with no lifecycle row keeps the legacy four, so an ongoing
/// legacy shift or an old correction is never reinterpreted — <c>กะที่เริ่มเก่าอยู่ต่อจนจบ window; old corrections ยังตีความแบบเก่า</c>.
/// </summary>
public static class AttendanceLifecycle
{
    /// <summary>Refusal: the slot already holds an attendance fact, so it cannot become the first numbered slot.</summary>
    public const string SlotHasRecordedFacts = "slot-has-recorded-facts";

    /// <summary>Refusal: the receiving window has opened, so the version boundary is too late to choose.</summary>
    public const string WindowOpen = "window-open";

    private static readonly IReadOnlyList<AttendanceEvent> Legacy4Actions = Array.AsReadOnly<AttendanceEvent>(
        [AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut]);

    private static readonly IReadOnlyList<AttendanceEvent> Numbered6Actions = Array.AsReadOnly<AttendanceEvent>(
        [
            AttendanceEvent.ShiftIn, AttendanceEvent.Break1Out, AttendanceEvent.Break1In, AttendanceEvent.Break2Out,
            AttendanceEvent.Break2In, AttendanceEvent.ShiftOut,
        ]);

    /// <summary>
    /// Spec §8: the ordered actions of a slot with this version, as one shared read-only list. Callers read it — the
    /// lifecycle is fixed per slot, so they can neither reorder it nor append to it.
    /// </summary>
    public static IReadOnlyList<AttendanceEvent> Order(AttendanceLifecycleVersion version) =>
        version == AttendanceLifecycleVersion.Numbered6 ? Numbered6Actions : Legacy4Actions;

    /// <summary>
    /// Spec §8 Cutover: a slot without a lifecycle row is legacy. Only an explicit numbered row reinterprets a slot, so a
    /// legacy shift that is still running and every old correction keep their four actions and their stored strings.
    /// </summary>
    public static AttendanceLifecycleVersion VersionFor(AttendanceLifecycleVersion? stored) =>
        stored ?? AttendanceLifecycleVersion.Legacy4;

    /// <summary>
    /// Spec §8: <c>ห้ามข้าม ShiftIn, ทำหลัง final ShiftOut</c> — a shift needs a real check-in and a final departure, so only
    /// the intermediate breaks can be answered with a skip marker (which carries no time).
    /// </summary>
    public static bool IsSkippable(AttendanceEvent action) =>
        action is not (AttendanceEvent.ShiftIn or AttendanceEvent.ShiftOut);

    /// <summary>
    /// Spec §8: the permitted next actions of a slot, in lifecycle order, each with the gaps it would skip. Nothing before
    /// Shift-In but Shift-In itself (never skippable, nothing precedes it); nothing once the final Shift-Out is recorded.
    /// Afterwards every action that is neither recorded nor already skipped is offered, and its
    /// <see cref="AttendanceChoice.RequiredGaps"/> are the unrecorded, unskipped actions before it — already-skipped markers
    /// need no second confirmation. Actual times stay ordered by lifecycle ordinal because each recorded action closes the
    /// choices before it. For a legacy slot this is the four-action list; the shorter chain an existing endpoint already
    /// offered (a Break-In only after a Break-Out) is still answered by <see cref="AttendanceRules.NextAllowed(IReadOnlyCollection{AttendanceEvent})"/>.
    /// </summary>
    public static IReadOnlyList<AttendanceChoice> NextActions(
        AttendanceLifecycleVersion version,
        IReadOnlyCollection<AttendanceEvent> recorded,
        IReadOnlyCollection<AttendanceEvent> skipped)
    {
        var order = Order(version);
        if (recorded.Contains(AttendanceEvent.ShiftOut))
        {
            return [];
        }

        if (!recorded.Contains(AttendanceEvent.ShiftIn))
        {
            return [new AttendanceChoice(AttendanceEvent.ShiftIn, [])];
        }

        var choices = new List<AttendanceChoice>(order.Count - 1);
        for (var index = 1; index < order.Count; index++)
        {
            var action = order[index];
            if (recorded.Contains(action) || skipped.Contains(action))
            {
                continue;
            }

            var gaps = order.Take(index)
                .Where(preceding => !recorded.Contains(preceding) && !skipped.Contains(preceding))
                .ToArray();
            choices.Add(new AttendanceChoice(action, Array.AsReadOnly(gaps)));
        }

        return choices;
    }

    /// <summary>
    /// Spec §8 Cutover (ADR facility-0222): a slot's version is chosen strictly before its receiving window opens, and only
    /// while the slot holds no attendance fact — <c>Slot ที่เคยมี legacy event แล้วเลือกเป็น first new slot ไม่ได้</c>. Null when
    /// the activation is allowed, otherwise the English machine code the caller refuses with. The window edges come from
    /// <see cref="AttendanceCalendar.Window"/> and no rollout date is built in: an operator picks a slot the checks still
    /// allow, and a deployment decides the date outside this contract.
    /// </summary>
    public static string? CheckActivation(
        ShiftSlot slot, DateTime nowUtc, AttendanceSettings settings, bool hasRecordedFacts)
    {
        if (hasRecordedFacts)
        {
            return SlotHasRecordedFacts;
        }

        var (opens, _) = AttendanceCalendar.Window(slot, settings);
        return nowUtc >= opens ? WindowOpen : null;
    }
}
