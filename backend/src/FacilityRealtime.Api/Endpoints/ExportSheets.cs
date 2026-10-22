using FacilityRealtime.Application.Export;
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Export;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>The export's rows in the Thai words of wireframe D8 and facility-0053.</summary>
internal static class ExportSheets
{
    public const string AttendanceName = "การลงเวลา";
    public const string SubmissionsName = "การส่งงาน";
    public const string InspectionsName = "การตรวจงาน";

    private static readonly AttendanceEvent[] Events =
        [AttendanceEvent.ShiftIn, AttendanceEvent.BreakOut, AttendanceEvent.BreakIn, AttendanceEvent.ShiftOut];

    private sealed record SortedRow(DateOnly ShiftDate, Shift Shift, DateTime SortUtc, string Tie, IReadOnlyList<string> Cells);

    /// <summary>One row per Cleaner per shift: the four times, who recorded them, and the GPS verdicts.</summary>
    public static ExcelSheet Attendance(IReadOnlyList<AttendanceExportRow> events)
    {
        var rows = events
            .GroupBy(e => (e.ShiftDate, e.Shift, e.UserId))
            .Select(group =>
            {
                var first = group.MinBy(e => e.OccurredAtUtc)!;
                // shift_attendances is unique per event per shift, so each event appears at most once
                var byEvent = group.ToDictionary(e => e.EventType);
                var edited = Events.Where(t => byEvent.TryGetValue(t, out var e) && e.Source != AttendanceSource.Scan).ToList();
                var outside = Events.Where(t => byEvent.TryGetValue(t, out var e) && e.WithinRadius == false).ToList();
                IReadOnlyList<string> cells =
                [
                    ThaiLabels.Date(group.Key.ShiftDate),
                    ThaiLabels.ShiftName(group.Key.Shift),
                    first.BuildingCode,
                    first.AreaCode,
                    first.EmployeeId,
                    first.DisplayName,
                    .. Events.Select(t => byEvent.TryGetValue(t, out var e) ? ThaiLabels.Clock(e.OccurredAtUtc) : string.Empty),
                    edited.Count == 0 ? "สแกนเอง" : "แก้โดยแอดมิน: " + string.Join(", ", edited.Select(ThaiLabels.EventName)),
                    outside.Count > 0
                        ? "นอกรัศมี: " + string.Join(", ", outside.Select(t => $"{ThaiLabels.EventName(t)} {byEvent[t].DistanceM} ม."))
                        : group.Any(e => e.WithinRadius == true) ? "ในรัศมี" : "-",
                ];
                return new SortedRow(group.Key.ShiftDate, group.Key.Shift, first.OccurredAtUtc, first.AreaCode + "|" + first.EmployeeId, cells);
            });

        return new ExcelSheet(
            AttendanceName,
            ["วันที่กะ", "กะ", "ตึก", "พื้นที่", "รหัสพนักงาน", "ชื่อ", "เข้างาน", "ออกพัก", "กลับจากพัก", "เลิกงาน", "ที่มา", "ตำแหน่ง"],
            Sort(rows));
    }

    /// <summary>Every submission, plus a "ไม่ได้ทำ" row for each round of a closed shift that nobody did (decision 12).</summary>
    public static ExcelSheet Submissions(
        IReadOnlyList<SubmissionExportRow> submissions, IReadOnlyList<ExportPoint> points, DateOnly first, DateOnly last, DateTime nowUtc,
        int closesMinutesAfterShift)
    {
        var rows = submissions.Select(s => new SortedRow(
            s.ShiftDate,
            s.Shift,
            s.SubmittedAtUtc,
            s.AreaCode + "|" + s.SignCode,
            [
                ThaiLabels.Date(s.ShiftDate),
                ThaiLabels.ShiftName(s.Shift),
                s.BuildingCode,
                s.AreaCode,
                s.PointName,
                s.SignCode,
                Round(s.RoundStart, s.RoundEnd),
                ThaiLabels.Clock(s.SubmittedAtUtc),
                ThaiLabels.SubmissionResult(s.Placement, s.LateMinutes),
                s.Status == CleaningStatus.Issue ? s.IssueTags ?? "แจ้งปัญหา" : string.Empty,
                s.IsCover ? "ทำแทน" : string.Empty,
                s.EmployeeId,
                s.DisplayName,
                ThaiLabels.Gps(s.DistanceM, s.WithinRadius),
            ])).ToList();

        // A round counts as done once any OnTime, Late or Rework submission was placed in it
        var done = submissions
            .Where(s => s.RoundWindowId is not null && s.Placement != Placement.OffRound)
            .GroupBy(s => (s.ShiftDate, s.Shift))
            .ToDictionary(g => g.Key, g => (IReadOnlySet<int>)g.Select(s => s.RoundWindowId!.Value).ToHashSet());
        IReadOnlySet<int> none = new HashSet<int>();

        for (var date = first; date <= last; date = date.AddDays(1))
        {
            foreach (var shift in new[] { Shift.Day, Shift.Night })
            {
                var slot = new ShiftSlot(date, shift);
                var doneIds = done.GetValueOrDefault((date, shift), none);
                foreach (var point in points.Where(p => shift == Shift.Day || p.ShiftPattern == ShiftPattern.DayAndNight))
                {
                    var windows = point.Windows
                        .Where(w => w.Shift == shift)
                        .Select(w => (RoundWindow.For(w.Id, w.Start, w.End, slot), w.CreatedAtUtc));
                    foreach (var missed in ExportRules.MissedRounds(windows, doneIds, slot, nowUtc, closesMinutesAfterShift))
                    {
                        rows.Add(new SortedRow(date, shift, missed.StartUtc, point.AreaCode + "|" + point.SignCode,
                        [
                            ThaiLabels.Date(date),
                            ThaiLabels.ShiftName(shift),
                            point.BuildingCode,
                            point.AreaCode,
                            point.PointName,
                            point.SignCode,
                            Round(missed.Start, missed.End),
                            string.Empty,
                            "ไม่ได้ทำ",
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                        ]));
                    }
                }
            }
        }

        return new ExcelSheet(
            SubmissionsName,
            ["วันที่กะ", "กะ", "ตึก", "พื้นที่", "จุด", "ป้าย", "รอบ", "เวลาส่ง", "ผล", "แจ้งปัญหา", "ทำแทน", "รหัสพนักงาน", "ชื่อ", "ตำแหน่ง"],
            Sort(rows));
    }

    /// <summary>
    /// Every inspection, plus one row for each finished round left "ไม่ได้ตรวจ" or "ไม่ได้แก้" (facility-0047 rule 5,
    /// decision 13). A round that ended ตรวจผ่าน already shows as its inspections.
    /// </summary>
    public static ExcelSheet Inspections(
        IReadOnlyList<InspectionExportRow> inspections, IReadOnlyList<PointPlace> places, IReadOnlyList<FinishedRound> rounds)
    {
        var rows = inspections.Select(i => new SortedRow(
            i.ShiftDate,
            i.Shift,
            i.InspectedAtUtc,
            i.AreaCode + "|" + i.SignCode,
            [
                ThaiLabels.Date(i.ShiftDate),
                ThaiLabels.ShiftName(i.Shift),
                i.BuildingCode,
                i.AreaCode,
                i.PointName,
                i.SignCode,
                Round(i.RoundStart, i.RoundEnd),
                ThaiLabels.Clock(i.InspectedAtUtc),
                ThaiLabels.InspectionResultName(i.Result),
                i.Defect ?? string.Empty,
                i.IsCover ? "ตรวจแทน" : string.Empty,
                i.EmployeeId,
                i.DisplayName,
                ThaiLabels.Gps(i.DistanceM, i.WithinRadius),
            ])).ToList();

        var placeOf = places.ToDictionary(p => p.Id);
        foreach (var round in rounds.Where(r => r.Outcome != RoundOutcome.Passed))
        {
            var place = placeOf[round.PointId];
            rows.Add(new SortedRow(
                round.Slot.ShiftDate,
                round.Slot.Shift,
                ShiftCalendar.ToUtc(round.Slot, round.RoundEnd),
                place.AreaCode + "|" + place.SignCode,
                [
                    ThaiLabels.Date(round.Slot.ShiftDate),
                    ThaiLabels.ShiftName(round.Slot.Shift),
                    place.BuildingCode,
                    place.AreaCode,
                    place.PointName,
                    place.SignCode,
                    Round(round.RoundStart, round.RoundEnd),
                    string.Empty,
                    ThaiLabels.RoundOutcomeName(round.Outcome),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                ]));
        }

        return new ExcelSheet(
            InspectionsName,
            ["วันที่กะ", "กะ", "ตึก", "พื้นที่", "จุด", "ป้าย", "รอบ", "เวลาตรวจ", "ผล", "สิ่งที่ต้องแก้", "ตรวจแทน", "รหัสพนักงาน", "ชื่อ", "ตำแหน่ง"],
            Sort(rows));
    }

    private static string Round(TimeOnly? start, TimeOnly? end) =>
        start is { } s && end is { } e ? $"{RoundWindowRules.Format(s)}–{RoundWindowRules.Format(e)}" : string.Empty;

    /// <summary>By shift date, Day before Night, then time in the shift, then Area and sign or person.</summary>
    private static List<IReadOnlyList<string>> Sort(IEnumerable<SortedRow> rows) =>
        rows.OrderBy(r => r.ShiftDate)
            .ThenBy(r => r.Shift)
            .ThenBy(r => r.SortUtc)
            .ThenBy(r => r.Tie, StringComparer.Ordinal)
            .Select(r => r.Cells)
            .ToList();
}
