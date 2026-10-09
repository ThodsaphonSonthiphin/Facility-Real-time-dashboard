namespace FacilityRealtime.Domain.Entities;

/// <summary>
/// The GPS column set of database.html on every on-site record. Latitude, Longitude and AccuracyM are raw and are
/// cleared after 90 days (facility-0055); DistanceM and WithinRadius stay. WithinRadius false = Geofence Flag; null = no verdict.
/// </summary>
public interface IGpsStamped
{
    decimal? Latitude { get; set; }
    decimal? Longitude { get; set; }
    short? AccuracyM { get; set; }
    short? DistanceM { get; set; }
    bool? WithinRadius { get; set; }
}
