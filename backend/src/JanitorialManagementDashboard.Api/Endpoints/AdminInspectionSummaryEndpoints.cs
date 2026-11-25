using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.DTOs;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Common;
using JanitorialManagementDashboard.Application.Export;
using JanitorialManagementDashboard.Application.Inspections;
using JanitorialManagementDashboard.Application.Rounds;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

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
            return Bad("ใส่วันเริ่มและวันสุดท้ายให้ครบและตรวจว่าทั้งสองวันที่ถูกต้อง");
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
            .Where(u => u.Role == UserRole.Supervisor && u.IsActive)
            .ToListAsync();
        var supervisorIds = supervisors.Select(u => u.Id).ToList();
        var datedPeriods = supervisorIds.Count == 0
            ? []
            : await db.SupervisorScopePeriods.AsNoTracking()
                .Where(p => supervisorIds.Contains(p.SupervisorId))
                .ToListAsync();
        var periodsBySupervisor = datedPeriods.ToLookup(p => p.SupervisorId);

        // Spec §3 and §14: read beats from dated SupervisorScopePeriods overlapping [first, last], falling back to
        // legacy User.BuildingId/Shift only for pre-backfill Supervisors with no dated period rows at all.
        var summaryBeats = new List<SummarySupervisor>();
        foreach (var supervisor in supervisors)
        {
            var ownPeriods = periodsBySupervisor[supervisor.Id].ToList();
            if (ownPeriods.Count > 0)
            {
                foreach (var beat in ownPeriods
                    .Where(p => p.EffectiveFrom <= last
                        && (p.EffectiveUntil == null || p.EffectiveUntil.Value > first)
                        && (buildingId == null || p.BuildingId == buildingId.Value))
                    .Select(p => (p.BuildingId, p.Shift))
                    .Distinct())
                {
                    summaryBeats.Add(new SummarySupervisor(supervisor.Id, beat.BuildingId, beat.Shift));
                }
            }
            else if (supervisor.BuildingId is int bId && supervisor.Shift is Shift s
                && (buildingId == null || bId == buildingId.Value))
            {
                summaryBeats.Add(new SummarySupervisor(supervisor.Id, bId, s));
            }
        }

        var beatBuildingIds = summaryBeats.Select(b => b.BuildingId).Distinct().ToList();
        var buildingCodes = beatBuildingIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Buildings.AsNoTracking()
                .Where(b => beatBuildingIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.Code);

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
            summaryBeats,
            places.Select(p => new SummaryPoint(p.Id, p.BuildingId, p.Counted, p.WorksNight)).ToList(),
            submissions,
            inspections.Select(i => new SummaryInspection(i.ScanRecordId, i.PointId, i.SupervisorId, new ShiftSlot(i.ShiftDate, i.Shift), i.Result, i.WithinRadius)).ToList(),
            rounds);

        var byId = supervisors.ToDictionary(u => u.Id);
        var pointById = places.ToDictionary(p => p.Id);
        var rows = summaryBeats.Zip(tallies, (beat, t) =>
            {
                var supervisor = byId[beat.Id];
                var buildingCode = buildingCodes.GetValueOrDefault(beat.BuildingId, supervisor.Building?.Code ?? string.Empty);
                return new InspectionSummaryRowDto(
                    supervisor.Id,
                    supervisor.EmployeeId ?? string.Empty,
                    supervisor.DisplayName,
                    buildingCode,
                    beat.Shift,
                    t.PointsInspected,
                    t.PointsTotal,
                    t.Submitted,
                    t.SubmittedInspected,
                    t.NotInspected,
                    t.Inspections,
                    t.Reworks,
                    t.GpsFlagged)
                {
                    UninspectedPoints = t.UninspectedPointIds.Select(id => pointById[id])
                        .OrderBy(p => p.AreaCode, StringComparer.Ordinal)
                        .ThenBy(p => p.SignCode, StringComparer.Ordinal)
                        .ThenBy(p => p.Id)
                        .Select(p => new UninspectedPointDto(p.Id, p.AreaCode, p.PointName, p.SignCode))
                        .ToList(),
                };
            })
            .OrderBy(r => r.BuildingCode, StringComparer.Ordinal)
            .ThenBy(r => r.Shift)
            .ThenBy(r => r.EmployeeId, StringComparer.Ordinal)
            .ToList();

        return Results.Ok(new InspectionSummaryDto(first, last, rows));
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
