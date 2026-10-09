namespace FacilityRealtime.ApiTests.Infrastructure;

public static class ThaiClock
{
    /// <summary>Asia/Bangkok wall-clock on <paramref name="day"/> October 2026.</summary>
    public static DateTimeOffset At(int day, int hour, int minute) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(7));
}
