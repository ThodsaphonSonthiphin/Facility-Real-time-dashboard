using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Inspections;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>facility-0053, 0047 rule 5: the export's "การตรวจงาน" sheet.</summary>
public class InspectionExportTests
{
    private const string Path = "/api/admin/export";

    private static readonly string[] Header =
        ["วันที่กะ", "กะ", "ตึก", "Area", "จุด", "ป้าย", "รอบ", "เวลาตรวจ", "ผล", "ข้อบกพร่อง", "ตรวจแทน", "รหัสพนักงาน", "ชื่อ", "GPS"];

    private static async Task<(FacilityApiFactory Factory, HttpClient Admin)> StartAsync(DateTimeOffset now)
    {
        var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.BackdateSetupAsync(factory);
        factory.Clock.SetUtcNow(now);
        return (factory, admin);
    }

    private static Task<HttpResponseMessage> ExportAsync(HttpClient admin, string[] sets, int? buildingId = null) =>
        admin.PostAsJsonAsync(Path, new { from = "2026-10-08", to = "2026-10-08", buildingId, sets });

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
    public async Task Inspections_and_the_rounds_left_uninspected_or_unfixed()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(9, 12, 0));
        using var _ = factory;
        var rework = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));
        await TestData.AddInspectionAsync(factory, rework, InspectionResult.Rework, ThaiClock.At(8, 8, 20));
        await TestData.AddScanAsync(factory, "token-restroom-f1", ThaiClock.At(8, 16, 10));
        var passed = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 16, 20));
        await TestData.AddInspectionAsync(factory, passed, InspectionResult.Passed, ThaiClock.At(8, 16, 30), withinRadius: false, distanceM: 120);

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Inspections"]));

        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("การตรวจงาน", sheet.Name);
        Assert.Equal(Header, sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        var rows = Rows(sheet);
        Assert.Equal(4, rows.Count);
        Assert.Equal(
            new[] { "2026-10-08", "เช้า", "A", "AR01", "ห้องน้ำชาย ชั้น 1", "AR01-01", "07:00–09:00", "08:20", "ต้องแก้ไข", "พื้นยังเปียก", "", "S2001", "สมปอง ตรวจดี", "-" },
            rows[0]);
        Assert.Equal(
            new[] { "2026-10-08", "เช้า", "A", "AR01", "ห้องน้ำชาย ชั้น 1", "AR01-01", "07:00–09:00", "", "ไม่ได้แก้", "", "", "", "", "" },
            rows[1]);
        Assert.Equal(
            new[] { "2026-10-08", "เช้า", "A", "AR01", "ห้องน้ำชาย ชั้น 1", "AR01-01", "16:00–18:00", "16:30", "สะอาด", "", "", "S2001", "สมปอง ตรวจดี", "นอกรัศมี 120 ม." },
            rows[2]);
        Assert.Equal(
            new[] { "2026-10-08", "เช้า", "A", "AR01", "ห้องน้ำหญิง ชั้น 1", "AR01-02", "16:00–18:00", "", "ไม่ได้ตรวจ", "", "", "", "", "" },
            rows[3]);
    }

    [Fact]
    public async Task An_inspection_under_a_cover_is_marked()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(9, 12, 0));
        using var _ = factory;
        await InspectionApi.AddBuildingBAsync(factory);
        var coverId = await TestData.AddSupervisorCoverAsync(factory, "S2001", "B", new DateOnly(2026, 10, 8), Shift.Day);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.BuildingBPoint, ThaiClock.At(8, 8, 30));
        await TestData.AddInspectionAsync(factory, scanId, InspectionResult.Passed, ThaiClock.At(8, 8, 40), coverAssignmentId: coverId);

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Inspections"]));

        var row = Assert.Single(Rows(workbook.Worksheet(1)));
        Assert.Equal(new[] { "B", "BR01", "สะอาด", "ตรวจแทน" }, new[] { row[2], row[3], row[8], row[10] });
    }

    [Fact]
    public async Task A_shift_still_running_has_no_outcome_rows()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(8, 12, 0));
        using var _ = factory;
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 8, 10));

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Inspections"]));

        Assert.Empty(Rows(workbook.Worksheet(1)));
    }

    [Fact]
    public async Task One_building_only()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(9, 12, 0));
        using var _ = factory;
        await InspectionApi.AddBuildingBAsync(factory);
        var scanId = await TestData.AddScanAsync(factory, InspectionApi.BuildingBPoint, ThaiClock.At(8, 8, 30));
        await TestData.AddInspectionAsync(factory, scanId, InspectionResult.Passed, ThaiClock.At(8, 8, 40));

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Inspections"], await AdminSetupApi.BuildingIdAsync(factory, "A")));

        Assert.Empty(Rows(workbook.Worksheet(1)));
    }

    [Fact]
    public async Task One_building_keeps_only_its_own_uninspected_rounds()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(9, 12, 0));
        using var _ = factory;
        await InspectionApi.AddBuildingBAsync(factory);
        await TestData.AddScanAsync(factory, InspectionApi.BuildingBPoint, ThaiClock.At(8, 8, 30));

        using var forA = await WorkbookAsync(await ExportAsync(admin, ["Inspections"], await AdminSetupApi.BuildingIdAsync(factory, "A")));
        using var forB = await WorkbookAsync(await ExportAsync(admin, ["Inspections"], await AdminSetupApi.BuildingIdAsync(factory, "B")));

        Assert.Empty(Rows(forA.Worksheet(1)));
        var row = Assert.Single(Rows(forB.Worksheet(1)));
        Assert.Equal(new[] { "ไม่ได้ตรวจ", "BR01" }, new[] { row[8], row[3] });
    }

    [Fact]
    public async Task A_deactivated_point_still_names_its_uninspected_round()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(9, 12, 0));
        using var _ = factory;
        await TestData.AddScanAsync(factory, "token-restroom-f1", ThaiClock.At(8, 8, 10));
        await factory.WithDbAsync(async db =>
        {
            var point = await db.ServicePoints.SingleAsync(p => p.Name == "ห้องน้ำหญิง ชั้น 1");
            point.IsActive = false;
            await db.SaveChangesAsync();
        });

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Inspections"]));

        var row = Assert.Single(Rows(workbook.Worksheet(1)));
        Assert.Equal(new[] { "ห้องน้ำหญิง ชั้น 1", "AR01-02", "ไม่ได้ตรวจ" }, new[] { row[4], row[5], row[8] });
    }

    [Fact]
    public async Task The_export_log_names_the_inspection_set()
    {
        var (factory, admin) = await StartAsync(ThaiClock.At(9, 12, 0));
        using var _ = factory;

        using var workbook = await WorkbookAsync(await ExportAsync(admin, ["Attendance", "Submissions", "Inspections"]));

        Assert.Equal(new[] { "การลงเวลา", "การส่งงาน", "การตรวจงาน" }, workbook.Worksheets.Select(s => s.Name));
        var audit = Assert.Single(await AdminSetupApi.AuditAsync(factory));
        using var after = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal(
            new[] { "Attendance", "Submissions", "Inspections" },
            after.RootElement.GetProperty("Sets").EnumerateArray().Select(s => s.GetString()));
        Assert.Contains("การตรวจงาน", audit.Summary);
    }
}
