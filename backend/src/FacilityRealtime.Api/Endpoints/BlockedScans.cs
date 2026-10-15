using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Geo;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

internal static class BlockedScans
{
    /// <summary>facility-0041, 0048: nothing is recorded for the scan itself; the Admin sees the Blocked Scan instead.</summary>
    public static async Task<IResult> BlockAsync(
        AppDbContext db, User user, Sign sign, Area signArea, BlockReason reason, GpsReading gps, DateTime nowUtc)
    {
        var blocked = new BlockedScan { UserId = user.Id, SignId = sign.Id, Reason = reason, ScannedAt = nowUtc };
        blocked.StampGps(gps, sign);
        db.BlockedScans.Add(blocked);
        await db.SaveChangesAsync();

        var buildingCode = await db.Buildings.Where(b => b.Id == signArea.BuildingId).Select(b => b.Code).SingleAsync();
        return Results.Json(
            new BlockedScanResponse(ThaiLabels.BlockedMessage(reason), signArea.Code, signArea.Name, reason, buildingCode),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
