using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Application.Export;
using FacilityRealtime.Application.Inspections;
using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// facility-0048 rule 5 (wireframe SupervisorSummary): the Admin's tab of each Supervisor's inspections. Read-only, so
/// nothing is logged. By shift date, the last 7 by default, at most 31 like the export.
/// </summary>
public static class AdminInspectionSummaryEndpoints
{
    public const int DefaultDays = 7;

    public static IEndpointRouteBuilder MapAdminInspectionSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/inspection-summary", GetAsync).RequireAuthorization(AuthSetup.AdminOnly);
        return app;
    }

    private static async Task<IResult> GetAsync(
        string? from, string? to, int? buildingId, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        DateOnly first, last;
        if (from is null && to is null)
        {
            last = DateOnly.FromDateTime(ThaiTime.FromUtc(now));
            first = last.AddDays(1 - DefaultDays);
        }
        else if (!AdminQuery.TryReadDate(from, out first) || !AdminQuery.TryReadDate(to, out last))
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

        var supervisors = await db.Users.AsNoTracking()
            .Include(u => u.Building)
            .Where(u => u.Role == UserRole.Supervisor && u.IsActive && u.BuildingId != null && u.Shift != null
                && (buildingId == null || u.BuildingId == buildingId))
            .ToListAsync();
        var places = await InspectionHistoryQuery.LoadPointPlacesAsync(db, buildingId);
        var submissions = await InspectionHistoryQuery.LoadRoundSubmissionsAsync(db, first, last, buildingId);
        // A Supervisor's own inspections count wherever they were, covers included, so these are not filtered by building
        var inspections = await InspectionHistoryQuery.LoadInspectionsAsync(db, first, last, buildingId: null);
        var rounds = InspectionHistory.FinishedRounds(
            submissions,
            inspections.Select(i => new InspectionFact(i.ScanRecordId, i.Result, i.InspectedAtUtc)).ToList(),
            now,
            settings.ClosesMinutesAfterShift);

        var tallies = InspectionSummary.Tally(
            supervisors.Select(u => new SummarySupervisor(u.Id, u.BuildingId!.Value, u.Shift!.Value)).ToList(),
            places.Select(p => new SummaryPoint(p.Id, p.BuildingId, p.Counted, p.WorksNight)).ToList(),
            submissions,
            inspections.Select(i => new SummaryInspection(i.ScanRecordId, i.PointId, i.SupervisorId, new ShiftSlot(i.ShiftDate, i.Shift), i.Result, i.WithinRadius)).ToList(),
            rounds);

        var byId = supervisors.ToDictionary(u => u.Id);
        var rows = tallies
            .Select(t =>
            {
                var supervisor = byId[t.SupervisorId];
                return new InspectionSummaryRowDto(
                    supervisor.Id,
                    supervisor.EmployeeId ?? string.Empty,
                    supervisor.DisplayName,
                    supervisor.Building!.Code,
                    supervisor.Shift!.Value,
                    t.PointsInspected,
                    t.PointsTotal,
                    t.Submitted,
                    t.SubmittedInspected,
                    t.NotInspected,
                    t.Inspections,
                    t.Reworks,
                    t.GpsFlagged);
            })
            .OrderBy(r => r.BuildingCode, StringComparer.Ordinal)
            .ThenBy(r => r.Shift)
            .ThenBy(r => r.EmployeeId, StringComparer.Ordinal)
            .ToList();

        return Results.Ok(new InspectionSummaryDto(first, last, rows));
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
