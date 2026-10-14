using FacilityRealtime.Application.Export;
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
                    edited.Count == 0 ? "สแกนเอง" : "แก้โดย Admin: " + string.Join(", ", edited.Select(ThaiLabels.EventName)),
                    outside.Count > 0
                        ? "นอกรัศมี: " + string.Join(", ", outside.Select(t => $"{ThaiLabels.EventName(t)} {byEvent[t].DistanceM} ม."))
                        : group.Any(e => e.WithinRadius == true) ? "ในรัศมี" : "-",
                ];
                return new SortedRow(group.Key.ShiftDate, group.Key.Shift, first.OccurredAtUtc, first.AreaCode + "|" + first.EmployeeId, cells);
            });

        return new ExcelSheet(
            AttendanceName,
            ["วันที่กะ", "กะ", "ตึก", "Area", "รหัสพนักงาน", "ชื่อ", "เข้างาน", "ออกพัก", "กลับจากพัก", "เลิกงาน", "ที่มา", "GPS"],
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
                s.Status == CleaningStatus.Issue ? s.IssueTags ?? "มี Issue" : string.Empty,
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
            ["วันที่กะ", "กะ", "ตึก", "Area", "จุด", "ป้าย", "รอบ", "เวลาส่ง", "ผล", "Issue", "ทำแทน", "รหัสพนักงาน", "ชื่อ", "GPS"],
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
