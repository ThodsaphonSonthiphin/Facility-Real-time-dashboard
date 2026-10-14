using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Export;
using FacilityRealtime.Infrastructure.Export;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// Wireframe D8, facility-0053: the Admin downloads attendance and submissions for up to 31 shift dates as one .xlsx.
/// POST because each export is logged (facility-0051). The file holds no coordinates, phone, QR Token or password.
/// </summary>
public static class AdminExportEndpoints
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static IEndpointRouteBuilder MapAdminExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/export", ExportAsync).RequireAuthorization(AuthSetup.AdminOnly);
        return app;
    }

    private static async Task<IResult> ExportAsync(ExportRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (!AdminQuery.TryReadDate(request.From, out var first) || !AdminQuery.TryReadDate(request.To, out var last))
        {
            return Bad("ต้องระบุทั้งวันเริ่ม (from) และวันสุดท้าย (to) แบบ yyyy-MM-dd");
        }

        if (last < first)
        {
            return Bad("วันสุดท้ายต้องไม่ก่อนวันเริ่ม");
        }

        if (last.DayNumber - first.DayNumber + 1 > ExportRules.MaxDays)
        {
            return Bad($"เลือกได้ไม่เกิน {ExportRules.MaxDays} วัน");
        }

        var sets = (request.Sets ?? []).Distinct().ToList();
        if (sets.Count == 0 || sets.Any(s => !Enum.IsDefined(s)))
        {
            return Bad("ต้องเลือกข้อมูลอย่างน้อย 1 ชุด");
        }

        string? buildingCode = null;
        if (request.BuildingId is { } buildingId)
        {
            buildingCode = await db.Buildings.AsNoTracking().Where(b => b.Id == buildingId).Select(b => b.Code).FirstOrDefaultAsync();
            if (buildingCode is null)
            {
                return Bad("ไม่พบตึกนี้");
            }
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var sheets = new List<ExcelSheet>();
        if (sets.Contains(ExportSet.Attendance))
        {
            sheets.Add(ExportSheets.Attendance(await ExportQuery.LoadAttendanceAsync(db, first, last, request.BuildingId)));
        }

        if (sets.Contains(ExportSet.Submissions))
        {
            sheets.Add(ExportSheets.Submissions(
                await ExportQuery.LoadSubmissionsAsync(db, first, last, request.BuildingId),
                await ExportQuery.LoadPointsAsync(db, request.BuildingId),
                first,
                last,
                now));
        }

        var from = ThaiLabels.Date(first);
        var to = ThaiLabels.Date(last);
        AuditTrail.Add(
            db, admin.Id, now, "EXPORT_EXCEL", "export", null,
            $"ส่งออก Excel {string.Join(" + ", sheets.Select(s => s.Name))} วันที่ {from} ถึง {to} {(buildingCode is null ? "ทุกตึก" : "ตึก " + buildingCode)}",
            before: null,
            after: new
            {
                From = from,
                To = to,
                BuildingId = request.BuildingId,
                Sets = sheets.Select(s => s.Name == ExportSheets.AttendanceName ? nameof(ExportSet.Attendance) : nameof(ExportSet.Submissions)).ToList(),
                Rows = sheets.Select(s => s.Rows.Count).ToList(),
            });
        await db.SaveChangesAsync();

        return Results.File(ExcelWorkbook.Write(sheets), XlsxType, $"facility_{from}_{to}.xlsx");
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
