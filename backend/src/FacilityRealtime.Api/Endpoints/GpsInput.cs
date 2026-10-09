using FacilityRealtime.Application.Geo;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0036: without a location the scan is refused — the one exception to "flag, never block".</summary>
internal static class GpsInput
{
    public const string Required = "ต้องเปิด Location และอนุญาตให้เว็บอ่านตำแหน่งก่อนสแกน";

    public const string Invalid = "ตำแหน่งที่ส่งมาไม่ถูกต้อง ลองสแกนใหม่อีกครั้ง";

    /// <summary>
    /// A missing value is "no location" (Required); a present but impossible one is Invalid. A coarse accuracy is still a
    /// location: it is stored and flagged by the geofence, never refused.
    /// </summary>
    public static bool TryRead(double? latitude, double? longitude, double? accuracyM, out GpsReading reading, out string error)
    {
        reading = default!;
        if (latitude is null || longitude is null || accuracyM is null)
        {
            error = Required;
            return false;
        }

        if (latitude is not (>= -90 and <= 90) || longitude is not (>= -180 and <= 180) || accuracyM is not >= 0)
        {
            error = Invalid;
            return false;
        }

        reading = new GpsReading(latitude.Value, longitude.Value, accuracyM.Value);
        error = "";
        return true;
    }
}
