namespace FacilityRealtime.Application.Common;

/// <summary>Asia/Bangkok wall-clock conversions. Shifts and Round Windows are Thai time; every stored DATETIME is UTC.</summary>
public static class ThaiTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime ToUtc(DateTime thaiWallClock) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(thaiWallClock, DateTimeKind.Unspecified), Zone);

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Asia/Bangkok", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // Bangkok has no daylight saving, so a fixed +7 offset is an exact fallback
        return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");
    }
}
