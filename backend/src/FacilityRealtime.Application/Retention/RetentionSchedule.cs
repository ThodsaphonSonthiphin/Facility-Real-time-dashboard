using FacilityRealtime.Application.Common;

namespace FacilityRealtime.Application.Retention;

public static class RetentionSchedule
{
    /// <summary>Strictly the next configured Bangkok hour; startup catch-up already handles today's due work.</summary>
    public static DateTime NextRunUtc(DateTime nowUtc, int hour)
    {
        var today = ThaiTime.FromUtc(nowUtc).Date.AddHours(hour);
        var next = ThaiTime.ToUtc(today);
        return next > nowUtc ? next : ThaiTime.ToUtc(today.AddDays(1));
    }
}
