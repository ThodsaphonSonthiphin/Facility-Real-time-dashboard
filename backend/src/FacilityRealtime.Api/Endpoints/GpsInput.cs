using FacilityRealtime.Application.Geo;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>facility-0036: without a location the scan is refused — the one exception to "flag, never block".</summary>
internal static class GpsInput
{
    public const string Required = "ต้องเปิด Location และอนุญาตให้เว็บอ่านตำแหน่งก่อนสแกน";

    public static GpsReading? Read(double? latitude, double? longitude, double? accuracyM) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && accuracyM is >= 0 and <= 10_000
            ? new GpsReading(latitude.Value, longitude.Value, accuracyM.Value)
            : null;
}
