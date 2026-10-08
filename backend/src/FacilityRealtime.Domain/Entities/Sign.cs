namespace FacilityRealtime.Domain.Entities;

/// <summary>A printed QR Sign. ServicePointId null = the Area's Check-In Sign (facility-0040).</summary>
public class Sign
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public int? ServicePointId { get; set; }

    /// <summary>Printed on the sign, e.g. AR01-IN, AR01-03.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Regenerating it makes the old printed sign stop working (facility-0006).</summary>
    public string QrToken { get; set; } = string.Empty;

    public DateTime QrIssuedAt { get; set; }

    /// <summary>Computed by the database: AreaId for a Check-In Sign, else null. Unique, so an Area has one Check-In Sign.</summary>
    public int? CheckinAreaId { get; private set; }

    public Area? Area { get; set; }
    public ServicePoint? ServicePoint { get; set; }
}
