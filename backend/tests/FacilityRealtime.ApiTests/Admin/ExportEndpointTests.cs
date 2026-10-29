using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Wireframe D8, facility-0053: the Admin downloads attendance and submissions for a date range as one .xlsx.</summary>
public class ExportEndpointTests
{
    private const string Path = "/api/admin/export";
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    /// <summary>9 Oct 12:00 Thai: the Day and Night shifts of 8 Oct are over, the Day shift of 9 Oct is running.</summary>
    private static async Task<(FacilityApiFactory Factory, HttpClient Admin)> StartAsync()
    {
        var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.BackdateSetupAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(9, 12, 0));
        return (factory, admin);
    }

    private static Task<HttpResponseMessage> ExportAsync(HttpClient admin, string from, string to, string[] sets, int? buildingId = null) =>
        admin.PostAsJsonAsync(Path, new { from, to, buildingId, sets });

    private static async Task<XLWorkbook> WorkbookAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }

    /// <summary>Every row under the header, as the text in each header column.</summary>
    private static List<string[]> Rows(IXLWorksheet sheet)
    {
        var width = sheet.Row(1).CellsUsed().Count();
        return sheet.RowsUsed().Skip(1)
            .Select(row => Enumerable.Range(1, width).Select(c => row.Cell(c).GetString()).ToArray())
            .ToList();
    }

    [Fact]
    public async Task Attendance_is_one_row_per_person_per_shift_with_source_and_gps()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddAttendanceAsync(factory, "1000001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        await TestData.AddAttendanceAsync(factory, "1000001", Oct8, Shift.Day, AttendanceEvent.ShiftOut, ThaiClock.At(8, 19, 2), AttendanceSource.AdminAdd);
        await TestData.AddAttendanceAsync(factory, "1000003", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 7, 5), distanceM: 900, withinRadius: false);

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Attendance"]));

        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("การลงเวลา", sheet.Name);
        Assert.Equal(
            new[] { "วันที่กะ", "กะ", "ตึก", "พื้นที่", "รหัสพนักงาน", "ชื่อ", "เข้างาน", "ออกพัก", "กลับจากพัก", "เลิกงาน", "ที่มา", "ตำแหน่ง" },
            sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        var rows = Rows(sheet);
        Assert.Equal(
            new[] { "2026-10-08", "เช้า", "A · ตึก A", "AR01", "1000001", "สมชาย ใจดี", "06:58", "", "", "19:02", "แก้โดยแอดมิน: เลิกงาน", "ในรัศมี" },
            rows[0]);
        Assert.Equal(new[] { "AR02", "1000003", "07:05", "สแกนเอง", "นอกรัศมี: เข้างาน 900 ม." }, new[] { rows[1][3], rows[1][4], rows[1][6], rows[1][10], rows[1][11] });
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task The_building_cell_shows_the_code_and_the_current_name()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddAttendanceAsync(factory, "1000001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        var a = await AdminSetupApi.BuildingIdAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/buildings/{a}", new { name = "อาคารผลิต 1" })).StatusCode);

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Attendance"]));

        Assert.Equal("A · อาคารผลิต 1", Rows(Assert.Single(workbook.Worksheets))[0][2]);
    }

    [Fact]
    public async Task Submissions_list_each_submission_and_every_round_nobody_did()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10), CleaningStatus.Issue, issueTags: "wet_floor");

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Submissions"]));

        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("การส่งงาน", sheet.Name);
        Assert.Equal(
            new[] { "วันที่กะ", "กะ", "ตึก", "พื้นที่", "จุด", "ป้าย", "รอบ", "เวลาส่ง", "ผล", "แจ้งปัญหา", "ทำแทน", "รหัสพนักงาน", "ชื่อ", "ตำแหน่ง" },
            sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        var rows = Rows(sheet);
        var menRestroom = rows.Where(r => r[5] == "AR01-01").Select(r => (r[1], r[6], r[7], r[8])).ToList();
        Assert.Equal(
            new[]
            {
                ("เช้า", "07:00–09:00", "08:10", "ตรงเวลา"),
                ("เช้า", "16:00–18:00", "", "ไม่ได้ทำ"),
                ("ดึก", "20:00–22:00", "", "ไม่ได้ทำ"),
                ("ดึก", "03:00–05:00", "", "ไม่ได้ทำ"),
            },
            menRestroom);
        var submitted = rows.Single(r => r[5] == "AR01-01" && r[8] == "ตรงเวลา");
        Assert.Equal(("wet_floor", "", "1000001", "สมชาย ใจดี"), (submitted[9], submitted[10], submitted[11], submitted[12]));
        Assert.Equal("A · ตึก A", submitted[2]);
        Assert.Equal("A · ตึก A", rows.First(r => r[5] == "AR01-01" && r[8] == "ไม่ได้ทำ")[2]);
        // AR02 works days only: one missed day round, no night rows
        Assert.Equal(new[] { ("เช้า", "ไม่ได้ทำ") }, rows.Where(r => r[5] == "AR02-01").Select(r => (r[1], r[8])));
    }

    /// <summary>A Scan Record written straight to the database, in the window of that sign's point that starts at <paramref name="windowStartHour"/>.</summary>
    private static Task AddRecordAsync(
        FacilityApiFactory factory,
        string qrToken,
        string employeeId,
        Placement placement,
        int? windowStartHour,
        DateTimeOffset at,
        int? lateMinutes = null,
        CleaningStatus status = CleaningStatus.Normal,
        string? issueTags = null,
        int? coverAssignmentId = null,
        short? distanceM = null,
        bool? withinRadius = null) =>
        factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.SingleAsync(s => s.QrToken == qrToken);
            var user = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var dayWindows = await db.PointRoundWindows.Where(w => w.ServicePointId == sign.ServicePointId && w.Shift == Shift.Day).ToListAsync();
            var window = windowStartHour is { } hour ? dayWindows.Single(w => w.StartTime.Hour == hour) : null;
            db.ScanRecords.Add(new ScanRecord
            {
                ServicePointId = sign.ServicePointId!.Value,
                SignId = sign.Id,
                UserId = user.Id,
                ShiftDate = Oct8,
                Shift = Shift.Day,
                RoundWindowId = window?.Id,
                RoundStart = window?.StartTime,
                RoundEnd = window?.EndTime,
                Placement = placement,
                LateMinutes = lateMinutes,
                Status = status,
                IssueTags = issueTags,
                SubmittedAt = at.UtcDateTime,
                CoverAssignmentId = coverAssignmentId,
                DistanceM = distanceM,
                WithinRadius = withinRadius,
            });
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task Each_kind_of_submission_prints_its_result_issue_and_gps_and_a_late_round_still_counts_as_done()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        const string women = "token-restroom-f1";
        await AddRecordAsync(factory, women, "1000001", Placement.Late, 7, ThaiClock.At(8, 9, 15), lateMinutes: 15,
            status: CleaningStatus.Issue, issueTags: null, distanceM: 12, withinRadius: true);
        await AddRecordAsync(factory, women, "1000001", Placement.OffRound, null, ThaiClock.At(8, 12, 0));
        await AddRecordAsync(factory, women, "1000001", Placement.Rework, 16, ThaiClock.At(8, 16, 30), distanceM: 900, withinRadius: false);

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Submissions"]));

        var rows = Rows(workbook.Worksheet(1)).Where(r => r[5] == "AR01-02").Select(r => (r[1], r[6], r[7], r[8], r[9], r[10], r[13])).ToList();
        Assert.Equal(
            new[]
            {
                // Late only: its round is done, so there is no "ไม่ได้ทำ" row for 07:00–09:00
                ("เช้า", "07:00–09:00", "09:15", "ส่งช้า 15 นาที", "แจ้งปัญหา", "", "ในรัศมี 12 ม."),
                ("เช้า", "", "12:00", "ส่งเพิ่ม (ไม่นับรอบ)", "", "", "-"),
                ("เช้า", "16:00–18:00", "16:30", "ส่งงานแก้", "", "", "นอกรัศมี 900 ม."),
                ("ดึก", "20:00–22:00", "", "ไม่ได้ทำ", "", "", ""),
                ("ดึก", "03:00–05:00", "", "ไม่ได้ทำ", "", "", ""),
            },
            rows);
    }

    [Fact]
    public async Task A_round_with_only_an_off_round_submission_is_still_not_done()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await AddRecordAsync(factory, "token-restroom-m1", "1000001", Placement.OffRound, null, ThaiClock.At(8, 12, 0));

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Submissions"]));

        var rows = Rows(workbook.Worksheet(1)).Where(r => r[5] == "AR01-01").Select(r => (r[1], r[6], r[8])).ToList();
        Assert.Equal(
            new[]
            {
                ("เช้า", "07:00–09:00", "ไม่ได้ทำ"),
                ("เช้า", "", "ส่งเพิ่ม (ไม่นับรอบ)"),
                ("เช้า", "16:00–18:00", "ไม่ได้ทำ"),
                ("ดึก", "20:00–22:00", "ไม่ได้ทำ"),
                ("ดึก", "03:00–05:00", "ไม่ได้ทำ"),
            },
            rows);
    }

    [Fact]
    public async Task A_cover_submission_is_marked_and_names_the_cleaner_who_covered()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddCleanerAsync(factory, "E9001", Shift.Day);
        var coverId = await TestData.AddCoverAsync(factory, "E9001", "AR02", Oct8, Shift.Day);
        await AddRecordAsync(factory, "token-meeting-room", "E9001", Placement.OnTime, 8, ThaiClock.At(8, 8, 30), coverAssignmentId: coverId);

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Submissions"]));

        var row = Assert.Single(Rows(workbook.Worksheet(1)), r => r[5] == "AR02-01");
        Assert.Equal(("ตรงเวลา", "ทำแทน", "E9001", "แม่บ้านทดสอบ E9001"), (row[8], row[10], row[11], row[12]));
    }

    [Fact]
    public async Task A_range_of_two_days_lists_the_missed_rounds_of_both()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-07", "2026-10-08", ["Submissions"]));

        var rows = Rows(workbook.Worksheet(1));
        // per date: 5 Day rounds (2 restrooms x 2, meeting room x 1) and 4 Night rounds
        Assert.Equal(18, rows.Count);
        Assert.All(rows, r => Assert.Equal("ไม่ได้ทำ", r[8]));
        Assert.Equal(new[] { "2026-10-07", "2026-10-08" }, rows.Select(r => r[0]).Distinct());
        Assert.Equal(new[] { "2026-10-07", "2026-10-08" }, rows.Where(r => r[5] == "AR02-01").Select(r => r[0]));
    }

    [Fact]
    public async Task A_shift_still_running_shows_no_missed_rounds()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-09", "2026-10-09", ["Submissions"]));

        Assert.Empty(Rows(workbook.Worksheet(1)));
    }

    [Fact]
    public async Task The_file_has_no_coordinates_and_its_name_carries_the_dates()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddAttendanceAsync(factory, "1000001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));

        var response = await ExportAsync(admin, "2026-10-08", "2026-10-09", ["Attendance", "Submissions"]);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("facility_2026-10-08_2026-10-09.xlsx", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        using var workbook = await WorkbookAsync(response);
        Assert.Equal(new[] { "การลงเวลา", "การส่งงาน" }, workbook.Worksheets.Select(w => w.Name));
        var text = string.Join("|", workbook.Worksheets.SelectMany(w => w.CellsUsed()).Select(c => c.GetString()));
        Assert.DoesNotContain("13.75", text);
        Assert.DoesNotContain("100.50", text);
        Assert.DoesNotContain("token-", text);
    }

    [Fact]
    public async Task Each_export_is_logged_with_its_range_and_row_counts()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddAttendanceAsync(factory, "1000001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));

        await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Attendance"]);

        var entry = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        Assert.Equal(("EXPORT_EXCEL", "export", (long?)null, (string?)null), (entry.Action, entry.EntityType, entry.EntityId, entry.BeforeJson));
        Assert.Equal("ส่งออก Excel การลงเวลา วันที่ 2026-10-08 ถึง 2026-10-08 ทุกตึก", entry.Summary);
        Assert.Contains("\"From\":\"2026-10-08\"", entry.AfterJson);
        Assert.Contains("\"Sets\":[\"Attendance\"]", entry.AfterJson);
        Assert.Contains("\"Rows\":[1]", entry.AfterJson);
        // Only the request and the counts: no row content. (A Thai name would be escaped as \uXXXX, so a text search cannot prove this.)
        using var after = JsonDocument.Parse(entry.AfterJson!);
        Assert.Equal(
            new[] { "From", "To", "BuildingId", "Sets", "Rows" },
            after.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Another_building_gives_headers_only()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await TestData.AddAttendanceAsync(factory, "1000001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));
        var buildingB = await TestData.AddBuildingAsync(factory, "B");

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Attendance", "Submissions"], buildingB));

        Assert.All(workbook.Worksheets, sheet => Assert.Empty(Rows(sheet)));
        Assert.EndsWith("ตึก B", Assert.Single(await AdminSetupApi.AuditAsync(factory)).Summary);
    }

    [Fact]
    public async Task Typed_text_stays_text_and_never_becomes_a_formula()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.EmployeeId == "1000001")).DisplayName = "=1+1";
            await db.SaveChangesAsync();
        });
        await TestData.AddAttendanceAsync(factory, "1000001", Oct8, Shift.Day, AttendanceEvent.ShiftIn, ThaiClock.At(8, 6, 58));

        using var workbook = await WorkbookAsync(await ExportAsync(admin, "2026-10-08", "2026-10-08", ["Attendance"]));

        var cell = workbook.Worksheet(1).Cell(2, 6);
        Assert.False(cell.HasFormula);
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal("=1+1", cell.GetString());
    }

    public static TheoryData<string> BadRequests => new()
    {
        """{ "from": "2026-10-01", "to": "2026-11-01", "sets": ["Attendance"] }""",
        """{ "from": "2026-10-09", "to": "2026-10-08", "sets": ["Attendance"] }""",
        """{ "from": "2026-10-08", "sets": ["Attendance"] }""",
        """{ "from": "08/10/2026", "to": "2026-10-08", "sets": ["Attendance"] }""",
        """{ "from": "2026-10-08", "to": "2026-10-08", "sets": [] }""",
        """{ "from": "2026-10-08", "to": "2026-10-08" }""",
        """{ "from": "2026-10-08", "to": "2026-10-08", "sets": ["Attendance"], "buildingId": 9999 }""",
        """{ "from": "2026-10-08", "to": "2026-10-08", "sets": [99] }""",
    };

    [Theory]
    [MemberData(nameof(BadRequests))]
    public async Task Bad_requests_are_refused_and_not_logged(string body)
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        var response = await admin.PostAsync(Path, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(await AdminSetupApi.MessageAsync(response)));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task Thirty_one_days_are_allowed()
    {
        var (factory, admin) = await StartAsync();
        using var _ = factory;

        Assert.Equal(HttpStatusCode.OK, (await ExportAsync(admin, "2026-09-01", "2026-10-01", ["Submissions"])).StatusCode);
    }

    [Fact]
    public async Task A_cleaner_cannot_export()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await ExportAsync(cleaner, "2026-10-08", "2026-10-08", ["Attendance"])).StatusCode);
    }
}
