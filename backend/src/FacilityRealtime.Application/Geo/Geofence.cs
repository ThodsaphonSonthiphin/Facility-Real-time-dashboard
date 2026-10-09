namespace FacilityRealtime.Application.Geo;

/// <summary>What the browser's Geolocation API reported: position and its accuracy in metres.</summary>
public sealed record GpsReading(double Latitude, double Longitude, double AccuracyM);

/// <summary>Both null when the sign has no coordinates yet (owner decision 2026-10-09: no verdict, no flag).</summary>
public sealed record GeofenceResult(int? DistanceM, bool? WithinRadius);

public static class Geofence
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Great-circle (haversine) distance.</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>facility-0035/0036/0037: within only when both the distance and the fix's accuracy fit inside the radius.</summary>
    public static GeofenceResult Evaluate(GpsReading phone, double? signLatitude, double? signLongitude, int radiusM)
    {
        if (signLatitude is not { } latitude || signLongitude is not { } longitude)
        {
            return new GeofenceResult(null, null);
        }

        var distance = (int)Math.Round(DistanceMeters(phone.Latitude, phone.Longitude, latitude, longitude));
        return new GeofenceResult(distance, distance <= radiusM && phone.AccuracyM <= radiusM);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
