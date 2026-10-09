using FacilityRealtime.Application.Geo;
using FacilityRealtime.Domain.Entities;

namespace FacilityRealtime.UnitTests;

public class GpsStampingTests
{
    private static Sign LocatedSign() => new() { Latitude = 13.756300m, Longitude = 100.501800m, RadiusM = 50 };

    [Fact]
    public void A_sign_without_coordinates_stores_the_fix_and_no_verdict()
    {
        var scan = new ScanRecord();

        scan.StampGps(new GpsReading(13.7563, 100.5018, 10), new Sign { Latitude = null, Longitude = null, RadiusM = 50 });

        Assert.Equal(13.7563m, scan.Latitude);
        Assert.Equal(100.5018m, scan.Longitude);
        Assert.Equal((short)10, scan.AccuracyM);
        Assert.Null(scan.DistanceM);
        Assert.Null(scan.WithinRadius);
    }

    [Fact]
    public void A_distance_above_the_smallint_range_is_clamped_to_32767()
    {
        var scan = new ScanRecord();

        scan.StampGps(new GpsReading(14.2563, 100.5018, 10), LocatedSign());

        Assert.Equal((short)32767, scan.DistanceM);
        Assert.False(scan.WithinRadius);
    }

    [Fact]
    public void Accuracy_is_rounded_up_to_whole_metres()
    {
        var scan = new ScanRecord();

        scan.StampGps(new GpsReading(13.7563, 100.5018, 12.2), LocatedSign());

        Assert.Equal((short)13, scan.AccuracyM);
    }

    [Fact]
    public void A_fix_at_the_sign_is_within_the_radius()
    {
        var scan = new ScanRecord();

        scan.StampGps(new GpsReading(13.7563, 100.5018, 10), LocatedSign());

        Assert.True(scan.WithinRadius);
        Assert.Equal((short)0, scan.DistanceM);
    }
}
