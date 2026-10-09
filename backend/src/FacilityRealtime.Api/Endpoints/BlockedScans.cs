using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Endpoints;

internal static class BlockedScans
{
    /// <summary>facility-0041: nothing is recorded for the scan itself; the Admin sees the Blocked Scan instead.</summary>
    public static async Task<IResult> BlockAsync(AppDbContext db, User user, Sign sign, Area signArea, GpsReading gps, DateTime nowUtc)
    {
        var blocked = new BlockedScan { UserId = user.Id, SignId = sign.Id, Reason = BlockReason.OtherArea, ScannedAt = nowUtc };
        blocked.StampGps(gps, sign);
        db.BlockedScans.Add(blocked);
        await db.SaveChangesAsync();

        return Results.Json(
            new BlockedScanResponse("ป้ายนี้ไม่ใช่ Area ของคุณ", signArea.Code, signArea.Name),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
