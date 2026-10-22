using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Alerts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>The alert list (wireframe D3): Blocked Scans, scans and inspections far from their sign, and missing Shift-Outs. Closing one is logged (facility-0051).</summary>
public static class AdminAlertEndpoints
{
    public const int MaxNoteLength = 500;

    public static IEndpointRouteBuilder MapAdminAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/alerts").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/", ListAsync);
        admin.MapPost("/review", ReviewAsync);
        return app;
    }

    /// <summary>Yesterday and today by default; the page splits it into "ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด" and by kind.</summary>
    private static async Task<IResult> ListAsync(
        string? from, string? to, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, AttendanceSettings settings)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (AdminQuery.ReadDateRange(from, to, now, defaultDays: 2, maxDays: 31, out var fromUtc, out var toUtc) is { } error)
        {
            return Bad(error);
        }

        var rows = await AlertQuery.LoadAsync(db, fromUtc, toUtc, now, settings);
        return Results.Ok(rows.Select(ToDto).ToList());
    }

    /// <summary>"คุยแล้ว ปิดเตือน" (facility-0035, 0041). A missing Shift-Out is closed by filling the time instead (facility-0051).</summary>
    private static async Task<IResult> ReviewAsync(ReviewAlertRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.Kind is not { } kind || !Enum.IsDefined(kind))
        {
            return Bad("ต้องระบุประเภทของเตือน");
        }

        if (AlertKinds.ReviewType(kind) is not { } type)
        {
            return Bad("เตือนนี้ปิดเองเมื่อแอดมินเติมเวลาที่ขาด");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            return Bad($"หมายเหตุยาวได้ไม่เกิน {MaxNoteLength} ตัวอักษร");
        }

        var target = await FindAsync(db, type, request.RecordId);
        if (target is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบรายการเตือนนี้");
        }

        if (await db.AlertReviews.AnyAsync(r => r.AlertType == type && r.RecordId == request.RecordId))
        {
            return AlreadyReviewed();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        AddReview(
            db, type, request.RecordId, target.Table, admin.Id, now,
            $"ปิดเตือน{ThaiLabels.AlertName(kind, target.Reason)} ของ {target.Name} เวลา {ThaiLabels.Clock(target.At)}",
            note);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another Admin closed it a moment ago and the unique index kept theirs; anything else is a real error
            db.ChangeTracker.Clear();
            if (!await db.AlertReviews.AnyAsync(r => r.AlertType == type && r.RecordId == request.RecordId))
            {
                throw;
            }

            return AlreadyReviewed();
        }

        return Results.NoContent();
    }

    /// <summary>Adds the alert_reviews row and its audit row; the caller saves them with its own change.</summary>
    internal static void AddReview(
        AppDbContext db, AlertType type, long recordId, string table, int adminId, DateTime now, string summary, string? note)
    {
        db.AlertReviews.Add(new AlertReview { AlertType = type, RecordId = recordId, ReviewedById = adminId, ReviewedAt = now });
        AuditTrail.Add(
            db, adminId, now, "ALERT_REVIEW", table, recordId, summary,
            before: new { Reviewed = false },
            after: new { Reviewed = true, AlertType = type.ToString() },
            reason: note);
    }

    private sealed record AlertTarget(string Table, string Name, DateTime At, BlockReason? Reason = null);

    /// <summary>The row behind the alert, only while it is one: a GPS flag must still be outside the radius.</summary>
    private static async Task<AlertTarget?> FindAsync(AppDbContext db, AlertType type, long id) => type switch
    {
        AlertType.AttendanceGps => await db.ShiftAttendances
            .Where(a => a.Id == id && a.WithinRadius == false)
            .Select(a => new AlertTarget("shift_attendances", a.User!.DisplayName, a.OccurredAt))
            .FirstOrDefaultAsync(),
        AlertType.SubmissionGps => await db.ScanRecords
            .Where(s => s.Id == id && s.WithinRadius == false)
            .Select(s => new AlertTarget("scan_records", s.User!.DisplayName, s.SubmittedAt))
            .FirstOrDefaultAsync(),
        AlertType.InspectionGps => await db.InspectionRecords
            .Where(i => i.Id == id && i.WithinRadius == false)
            .Select(i => new AlertTarget("inspections", i.Supervisor!.DisplayName, i.InspectedAt))
            .FirstOrDefaultAsync(),
        _ => await db.BlockedScans
            .Where(b => b.Id == id)
            .Join(db.Users, b => b.UserId, u => u.Id, (b, u) => new AlertTarget("blocked_scans", u.DisplayName, b.ScannedAt, b.Reason))
            .FirstOrDefaultAsync(),
    };

    private static AlertDto ToDto(AlertRow row) => new(
        row.Kind,
        row.RecordId,
        Utc(row.At),
        new AlertPersonDto(row.Person.Id, row.Person.LoginName, row.Person.DisplayName, row.Person.Role, row.Person.Area?.Code),
        row.Area.Code,
        row.Area.Name,
        row.Sign?.Code,
        row.Sign?.ServicePoint?.Name,
        row.DistanceM,
        row.Sign?.RadiusM,
        row.AccuracyM,
        row.MissingEvents,
        row.Review is not null,
        row.Review is { } review ? Utc(review.ReviewedAt) : (DateTime?)null,
        row.CoverSlot is { } slot
            ? row.Person.Role == UserRole.Supervisor
                ? new CoverSuggestionDto(row.Person.Id, null, row.Area.BuildingId, slot.ShiftDate, slot.Shift)
                : new CoverSuggestionDto(row.Person.Id, row.Area.Id, null, slot.ShiftDate, slot.Shift)
            : null,
        row.BlockReason);

    /// <summary>The database returns Unspecified; marking it UTC makes the JSON end in "Z".</summary>
    private static DateTime Utc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);

    private static IResult AlreadyReviewed() => ApiResults.Message(StatusCodes.Status409Conflict, "ปิดเตือนนี้ไปแล้ว");

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
