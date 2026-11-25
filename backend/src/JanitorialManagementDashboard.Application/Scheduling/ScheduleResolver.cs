using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;

namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>
/// Spec §5, §7 and §10 (ADRs facility-0173, 0189, 0190, 0202 and 0214): the one pure resolution of a person's day. Each
/// field is answered on its own — a live dated explicit choice, else the applicable versioned baseline, else unset — so a
/// later Area never rewrites an earlier day, a dated break override survives a baseline change, and an absence state
/// keeps the underlying eligible Area the record-first check-in needs. It reads no database, no clock and no mutable User
/// field: the caller loads a <see cref="ScheduleInput"/> and asks about a date.
/// </summary>
public static class ScheduleResolver
{
    /// <summary>A live dated override supplied the value (an Area or break-group exception).</summary>
    public const string SourceDated = "dated";

    /// <summary>The applicable baseline version supplied the value.</summary>
    public const string SourceBaseline = "baseline";

    /// <summary>No record supplies the field, so it stays unset. Unset is not a confirmed working day.</summary>
    public const string SourceUnset = "unset";

    /// <summary>The date is before the person's history coverage, so nothing can be said about it.</summary>
    public const string SourceUnavailable = "unavailable";

    /// <summary>A known plan applies to the day.</summary>
    public const string AvailabilityAvailable = "available";

    /// <summary>The person chose this day off; the underlying eligible Area stays visible (spec §7).</summary>
    public const string AvailabilityDayOff = "day-off";

    /// <summary>An agreed leave covers the whole shift; the underlying eligible Area stays visible (spec §7).</summary>
    public const string AvailabilityLeave = "leave";

    /// <summary>Nobody has chosen this day's plan yet.</summary>
    public const string AvailabilityUnset = "unset";

    /// <summary>A27: the date is outside retained history coverage. Different from zero and from unset.</summary>
    public const string AvailabilityUnavailable = "unavailable";

    /// <summary>The revision of a field no stored row supplied.</summary>
    public const string NoRevision = "0";

    /// <summary>Spec §5: a still-applicable dated break-group override has no Area to belong to.</summary>
    public const string ProblemDatedGroupWithoutArea = "dated-group-without-area";

    /// <summary>Spec §5: a dated Area is recorded on a day the records mark as off.</summary>
    public const string ProblemDatedAreaOnDayOff = "dated-area-on-day-off";

    /// <summary>
    /// Spec §5 and ADR facility-0189: a live dated break-group override sits on a day off or a whole-shift leave, so the
    /// override still applies to a non-working day and someone has to correct it. It is never removed silently.
    /// </summary>
    public const string ProblemDatedGroupOnNonWorking = "dated-group-on-non-working";

    /// <summary>Spec §9 and A22: an instruction's source Area no longer matches the day's Area.</summary>
    public const string ProblemTransferOriginMismatch = "transfer-origin-mismatch";

    /// <summary>
    /// Spec §5 and §7: half-open range predicate — the start date is included, the until date is excluded, and an open
    /// range has no end. Explicit because every effective-dated record in the design uses this one boundary.
    /// </summary>
    public static bool Applies(DateOnly from, DateOnly? until, DateOnly date) =>
        from <= date && (until is null || date < until.Value);

    /// <summary>
    /// The resolved day for <paramref name="date"/>. Before <see cref="ScheduleInput.HistoryStartsAt"/> nothing is
    /// claimed (unavailable, no Area); afterwards a day off — the week's choice or the baseline cell stored as one — or a
    /// whole-shift leave is an absence state that keeps the underlying Area, an explicit choice wins over the baseline, and
    /// a conflict between the stored records is reported rather than resolved silently.
    /// </summary>
    public static ScheduleDay Resolve(ScheduleInput input, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (date < input.HistoryStartsAt)
        {
            return new ScheduleDay(date,
                new ResolvedValue<int?>(null, null, SourceUnavailable, NoRevision),
                new ResolvedValue<int?>(null, null, SourceUnavailable, NoRevision),
                IsDayOff: false, IsLeave: false, IsUnset: false,
                AvailabilityUnavailable, []);
        }

        // The dated rows of this date belong to one slot. The store passes only the slot the person works, and where a
        // caller passes more than one the lowest shift then lowest id answers, so the result is never order-dependent.
        var dated = input.Exceptions.Where(x => x.ShiftDate == date)
            .OrderBy(x => x.Shift).ThenBy(x => x.Id).ToList();
        var areaOverride = dated.Find(x => x.AreaId is not null);
        var groupOverride = dated.Find(x => x.BreakGroup is not null);

        // The baseline version whose half-open range covers the date, and that version's cell for this weekday.
        var cell = BaselineCell(input.Baselines, date);

        var area = areaOverride is not null
            ? new ResolvedValue<int?>(areaOverride.AreaId, areaOverride.Id, SourceDated, areaOverride.Revision)
            : cell?.AreaId is int baselineArea
                ? new ResolvedValue<int?>(baselineArea, cell.Id, SourceBaseline, cell.Revision)
                : new ResolvedValue<int?>(null, null, SourceUnset, NoRevision);

        // Field by field (spec §5): a dated group wins over the version's default, and the Area above is independent.
        var breakGroup = groupOverride is not null
            ? new ResolvedValue<int?>(groupOverride.BreakGroup, groupOverride.Id, SourceDated, groupOverride.Revision)
            : cell?.BreakGroup is int baselineGroup
                ? new ResolvedValue<int?>(baselineGroup, cell.Id, SourceBaseline, cell.Revision)
                : new ResolvedValue<int?>(null, null, SourceUnset, NoRevision);

        // facility-0190: the week's own choice (a null saved week is "nobody has been asked", which the row reports as
        // SchedulePersonWeek.HasWeeklyChoice rather than as a per-day state). A baseline cell stored as a day off is the
        // version's confirmed non-working weekday, so it resolves as "day-off" and never as an unconfirmed gap (spec §5:
        // unset is not a confirmed working day — and a confirmed day off is not an unconfirmed one either).
        var chosenOff = input.DaysOff?.Contains(date) == true;
        var baselineOff = cell?.IsDayOff == true;

        // A dated Area override is an explicit choice to work that date, so it wins the day over the baseline's day-off cell
        // and the clash is reported below instead of the off state being dropped quietly (spec §5, ADR facility-0189).
        var isDayOff = chosenOff || (baselineOff && areaOverride is null);

        var leaves = input.Leaves.Where(x => !x.Cancelled && x.ShiftDate == date)
            .OrderBy(x => x.Shift).ThenBy(x => x.Id).ToList();
        var isLeave = leaves.Count > 0;
        var wholeShiftLeave = leaves.Exists(x => CoversWholeShift(x));

        var problems = new List<ScheduleProblem>();
        if (groupOverride is not null && area.Value is null)
        {
            problems.Add(new ScheduleProblem(ProblemDatedGroupWithoutArea,
                "มีกลุ่มพักที่กำหนดเฉพาะวันนี้ แต่ไม่มี Area ที่ใช้ได้ในวันนี้ จึงต้องแก้ไขก่อนบันทึกการเข้างาน", date));
        }

        if (areaOverride is not null && (chosenOff || baselineOff))
        {
            problems.Add(new ScheduleProblem(ProblemDatedAreaOnDayOff,
                "มี Area ที่กำหนดเฉพาะวันนี้ในวันที่เป็นวันหยุด ต้องแก้ไขให้ตรงกัน", date));
        }

        // §5 and ADR facility-0189: a still-applicable dated break group on a day off or a whole-shift leave keeps its value
        // in BreakGroup and is shown here as a conflict to correct, rather than being silently kept or silently removed.
        if (groupOverride is not null && (isDayOff || wholeShiftLeave))
        {
            problems.Add(new ScheduleProblem(ProblemDatedGroupOnNonWorking,
                "มีกลุ่มพักที่กำหนดเฉพาะวันนี้ในวันที่ไม่ทำงาน (วันหยุดหรือลาทั้งกะ) ต้องแก้ไขให้ตรงกัน", date));
        }

        // A22: a transfer is a plan for later in the same shift, so it never rewrites the day's Area; an instruction whose
        // source Area no longer matches the plan (or the destination of a prior confirmed move in a chain) is shown for correction.
        var transfers = input.Transfers
            .Where(x => !x.Cancelled && x.ShiftDate == date)
            .OrderBy(x => x.Id)
            .ToList();
        if (area.Value is int plannedArea)
        {
            var expectedOrigin = plannedArea;
            var originMismatch = false;
            foreach (var transfer in transfers)
            {
                if (transfer.FromAreaId != expectedOrigin)
                {
                    originMismatch = true;
                    break;
                }

                if (transfer.ConfirmedAt is not null)
                {
                    expectedOrigin = transfer.ToAreaId;
                }
            }

            if (originMismatch)
            {
                problems.Add(new ScheduleProblem(ProblemTransferOriginMismatch,
                    "คำสั่งย้ายมี Area ต้นทางไม่ตรงกับ Area ของแผนในวันนี้ ต้องแก้ไขก่อนยืนยันการย้าย", date));
            }
        }

        var availability =
            isDayOff ? AvailabilityDayOff
            : wholeShiftLeave ? AvailabilityLeave
            : area.SourceKind == SourceUnset ? AvailabilityUnset
            : AvailabilityAvailable;

        // "No confirmed state of any kind": no Area, not a chosen or baseline day off, and not a whole-shift leave. Derived
        // from the availability above, so the flag and the word can never disagree — a known baseline Area stays a confirmed
        // working day even when no week was saved, and a confirmed day off is never reported as an unconfirmed gap.
        var isUnset = availability == AvailabilityUnset;

        return new ScheduleDay(date, area, breakGroup, isDayOff, isLeave, isUnset, availability, problems);
    }

    /// <summary>
    /// The cell the applicable baseline version holds for this weekday. The latest start wins, so a version that began
    /// later is the only one consulted for its own dates and an earlier day keeps the earlier version (ADR facility-0189).
    /// A fact with no weekday applies to every day of its version.
    /// </summary>
    private static BaselineFact? BaselineCell(IReadOnlyList<BaselineFact> baselines, DateOnly date)
    {
        var applicable = baselines.Where(x => Applies(x.From, x.Until, date)).ToList();
        if (applicable.Count == 0)
        {
            return null;
        }

        var latest = applicable.Max(x => x.From);
        var version = applicable.Where(x => x.From == latest).ToList();
        return version.Find(x => x.Weekday == date.DayOfWeek) ?? version.Find(x => x.Weekday is null);
    }

    /// <summary>True when the leave covers the whole window of its own slot (12 hours from its Thai start time).</summary>
    private static bool CoversWholeShift(LeaveFact leave)
    {
        var slot = new ShiftSlot(leave.ShiftDate, leave.Shift);
        var shiftStart = new DateTimeOffset(ShiftCalendar.StartUtc(slot), TimeSpan.Zero);
        var shiftEnd = new DateTimeOffset(ShiftCalendar.EndUtc(slot), TimeSpan.Zero);
        return leave.Start <= shiftStart && leave.End >= shiftEnd;
    }
}
