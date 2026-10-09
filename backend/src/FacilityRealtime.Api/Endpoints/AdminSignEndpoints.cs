using System.Security.Claims;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Signs;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0045, 0038, 0006: where a sign stands, how far a scan may be from it, and its QR Token. Every change is logged.</summary>
public static class AdminSignEndpoints
{
    /// <summary>Below 10 m is finer than a phone's indoor fix, so every scan would be flagged; 500 m covers several buildings.</summary>
    public const short MinRadiusM = 10;

    public const short MaxRadiusM = 500;

    public static IEndpointRouteBuilder MapAdminSignEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/signs").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapPut("/{id:int}/location", SetLocationAsync);
        admin.MapPut("/{id:int}/radius", SetRadiusAsync);
        admin.MapPost("/{id:int}/regenerate-token", RegenerateTokenAsync);
        return app;
    }

    private static async Task<IResult> SetLocationAsync(int id, SignLocationRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.Source is not { } source || !Enum.IsDefined(source))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, "ต้องระบุว่าพิกัดมาจากหน้างาน (Site) หรือแผนที่ (Map)");
        }

        // A map pick has no accuracy; only a capture at the sign has one
        var accuracy = source == LocationSource.Map ? 0 : request.AccuracyM;
        if (!GpsInput.TryRead(request.Latitude, request.Longitude, accuracy, out var gps, out var gpsError))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, gpsError);
        }

        var sign = await db.Signs.FirstOrDefaultAsync(s => s.Id == id);
        if (sign is null)
        {
            return SignNotFound();
        }

        // facility-0045 rule 3: the map is the fallback before going on site, not a correction of a site capture
        if (source == LocationSource.Map && sign.LocationSource == LocationSource.Site)
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "ป้ายนี้ยืนยันพิกัดที่หน้างานแล้ว ถ้าจะแก้ให้เก็บใหม่ที่หน้างาน");
        }

        var latitude = Math.Round((decimal)gps.Latitude, 6);
        var longitude = Math.Round((decimal)gps.Longitude, 6);
        short? accuracyM = source == LocationSource.Site ? (short)Math.Min(Math.Ceiling(gps.AccuracyM), short.MaxValue) : null;
        if (sign.Latitude == latitude && sign.Longitude == longitude && sign.LocationAccuracyM == accuracyM && sign.LocationSource == source)
        {
            // Saving the same location again changes nothing, so it logs nothing and keeps LocatedAt
            return Results.Ok(AdminSetupView.ToDto(sign));
        }

        var before = LocationSnapshot(sign);
        var now = clock.GetUtcNow().UtcDateTime;
        sign.Latitude = latitude;
        sign.Longitude = longitude;
        sign.LocationAccuracyM = accuracyM;
        sign.LocationSource = source;
        sign.LocatedAt = now;
        AuditTrail.Add(
            db, admin.Id, now, "SIGN_LOCATION", "signs", sign.Id,
            source == LocationSource.Site
                ? $"เก็บพิกัดป้าย {sign.Code} ที่หน้างาน ±{sign.LocationAccuracyM} ม."
                : $"ปักพิกัดป้าย {sign.Code} จากแผนที่ (ยังไม่ยืนยันหน้างาน)",
            before, after: LocationSnapshot(sign));
        await db.SaveChangesAsync();

        return Results.Ok(AdminSetupView.ToDto(sign));
    }

    /// <summary>facility-0038. Applies to later scans; verdicts already stored stay as they were.</summary>
    private static async Task<IResult> SetRadiusAsync(int id, SignRadiusRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        if (request.RadiusM is not { } radius || radius < MinRadiusM || radius > MaxRadiusM)
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest, $"รัศมีต้องอยู่ระหว่าง {MinRadiusM}–{MaxRadiusM} เมตร");
        }

        var sign = await db.Signs.FirstOrDefaultAsync(s => s.Id == id);
        if (sign is null)
        {
            return SignNotFound();
        }

        if (sign.RadiusM != radius)
        {
            var old = sign.RadiusM;
            sign.RadiusM = (short)radius;
            AuditTrail.Add(
                db, admin.Id, clock.GetUtcNow().UtcDateTime, "SIGN_RADIUS", "signs", sign.Id,
                $"เปลี่ยนรัศมีป้าย {sign.Code} {old} → {radius} ม.",
                before: new { RadiusM = old }, after: new { RadiusM = radius });
            await db.SaveChangesAsync();
        }

        return Results.Ok(AdminSetupView.ToDto(sign));
    }

    /// <summary>facility-0006/0021: the printed sign stops working at once; scan history is untouched.</summary>
    private static async Task<IResult> RegenerateTokenAsync(int id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var admin = await CurrentUser.LoadAsync(principal, db);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var sign = await db.Signs.Include(s => s.Area).Include(s => s.ServicePoint).FirstOrDefaultAsync(s => s.Id == id);
        if (sign is null)
        {
            return SignNotFound();
        }

        // facility-0021 rule 4: no new QR for a deactivated point (or Area)
        if (sign.Area is not { IsActive: true } || sign.ServicePoint is { IsActive: false })
        {
            return ApiResults.Message(StatusCodes.Status409Conflict, "ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงออก QR ใหม่ได้");
        }

        var issuedBefore = DateTime.SpecifyKind(sign.QrIssuedAt, DateTimeKind.Utc);
        var now = clock.GetUtcNow().UtcDateTime;
        sign.QrToken = SignCodes.NewQrToken();
        sign.QrIssuedAt = now;
        // The token itself never enters the log (facility-0060: tokens come only from the points and print pages)
        AuditTrail.Add(
            db, admin.Id, now, "QR_REGENERATE", "signs", sign.Id,
            $"ออก QR ใหม่ของป้าย {sign.Code} ป้ายเดิมสแกนไม่ได้แล้ว",
            before: new { QrIssuedAt = issuedBefore }, after: new { QrIssuedAt = now });
        await db.SaveChangesAsync();

        return Results.Ok(AdminSetupView.ToDto(sign));
    }

    private static object LocationSnapshot(Sign sign) => new
    {
        sign.Latitude,
        sign.Longitude,
        sign.LocationAccuracyM,
        LocationSource = sign.LocationSource?.ToString(),
    };

    private static IResult SignNotFound() => ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบป้ายนี้");
}
