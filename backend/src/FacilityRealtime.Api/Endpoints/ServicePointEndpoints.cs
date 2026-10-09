using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

public static class ServicePointEndpoints
{
    public static IEndpointRouteBuilder MapServicePointEndpoints(this IEndpointRouteBuilder app)
    {
        // facility-0058: the Dashboard is Admin only; facility-0060: it carries no QR Token
        app.MapGet("/api/service-points", async (AppDbContext db, TimeProvider clock) =>
        {
            var rows = await PointBoardQuery.LoadAsync(db, clock.GetUtcNow().UtcDateTime);
            return Results.Ok(rows.Select(PointDtoMapper.ToDto));
        }).RequireAuthorization(AuthSetup.AdminOnly);

        // The scan page: whoever holds a sign's QR Token may see that one point (facility-0059)
        app.MapGet("/api/service-points/by-token/{token}", async (string token, AppDbContext db, TimeProvider clock) =>
        {
            var pointId = await db.Signs
                .Where(s => s.QrToken == token && s.ServicePointId != null)
                .Select(s => s.ServicePointId)
                .FirstOrDefaultAsync();
            var row = pointId is null
                ? null
                : (await PointBoardQuery.LoadAsync(db, clock.GetUtcNow().UtcDateTime, pointId)).SingleOrDefault();

            return row is null
                ? Results.NotFound(new { message = "ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว" })
                : Results.Ok(PointDtoMapper.ToDto(row));
        }).RequireAuthorization();

        return app;
    }
}
