using FacilityRealtime.Domain.Entities;

namespace FacilityRealtime.Application.Geo;

public static class GpsStamping
{
    /// <summary>Stores the phone's fix and its verdict against <paramref name="sign"/> (facility-0035/0037: flagged, never blocked).</summary>
    public static void StampGps(this IGpsStamped target, GpsReading phone, Sign sign)
    {
        var verdict = Geofence.Evaluate(phone, (double?)sign.Latitude, (double?)sign.Longitude, sign.RadiusM);
        target.Latitude = Math.Round((decimal)phone.Latitude, 6);
        target.Longitude = Math.Round((decimal)phone.Longitude, 6);
        target.AccuracyM = ToSmallint(Math.Ceiling(phone.AccuracyM));
        target.DistanceM = verdict.DistanceM is int distance ? ToSmallint(distance) : null;
        target.WithinRadius = verdict.WithinRadius;
    }

    private static short ToSmallint(double value) => (short)Math.Min(value, short.MaxValue);
}
