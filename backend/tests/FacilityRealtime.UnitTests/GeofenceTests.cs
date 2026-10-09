using FacilityRealtime.Application.Geo;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0035/0036/0037: outside the radius, or a fix less accurate than the radius, is flagged — never blocked.</summary>
public class GeofenceTests
{
    private const double SignLatitude = 13.7563;
    private const double SignLongitude = 100.5018;

    [Fact]
    public void One_thousandth_of_a_degree_of_latitude_is_about_111_metres()
    {
        Assert.InRange(Geofence.DistanceMeters(13.7563, 100.5018, 13.7573, 100.5018), 110.6, 111.8);
    }

    [Fact]
    public void Same_point_is_zero_metres()
    {
        Assert.Equal(0, Geofence.DistanceMeters(SignLatitude, SignLongitude, SignLatitude, SignLongitude), precision: 6);
    }

    [Fact]
    public void Phone_at_the_sign_is_within_the_radius()
    {
        Assert.Equal(new GeofenceResult(0, true), Geofence.Evaluate(new GpsReading(SignLatitude, SignLongitude, 10), SignLatitude, SignLongitude, 50));
    }

    [Fact]
    public void Phone_beyond_the_radius_is_flagged()
    {
        var result = Geofence.Evaluate(new GpsReading(13.7573, 100.5018, 10), SignLatitude, SignLongitude, 50);

        Assert.Equal(111, result.DistanceM);
        Assert.False(result.WithinRadius);
    }

    [Fact]
    public void Exactly_at_the_radius_is_within()
    {
        Assert.True(Geofence.Evaluate(new GpsReading(13.7573, 100.5018, 10), SignLatitude, SignLongitude, 111).WithinRadius);
    }

    [Fact]
    public void Inaccurate_fix_is_flagged_even_at_the_sign()
    {
        Assert.Equal(new GeofenceResult(0, false), Geofence.Evaluate(new GpsReading(SignLatitude, SignLongitude, 80), SignLatitude, SignLongitude, 50));
    }

    [Fact]
    public void Sign_without_coordinates_gives_no_verdict()
    {
        Assert.Equal(new GeofenceResult(null, null), Geofence.Evaluate(new GpsReading(SignLatitude, SignLongitude, 10), null, null, 50));
    }
}
