using FacilityRealtime.Application.Retention;

namespace FacilityRealtime.UnitTests;

public class RetentionRulesTests
{
    [Theory]
    [InlineData("2026-11-02T18:59:59Z", "2026-11-02T19:00:00Z")]
    [InlineData("2026-11-02T19:00:00Z", "2026-11-03T19:00:00Z")]
    [InlineData("2024-02-28T20:00:00Z", "2024-02-29T19:00:00Z")]
    [InlineData("2026-12-31T20:00:00Z", "2027-01-01T19:00:00Z")]
    public void Next_night_uses_Bangkok_wall_time_and_rolls_over_leap_day_and_year(string now, string next) =>
        Assert.Equal(DateTimeOffset.Parse(next).UtcDateTime, RetentionSchedule.NextRunUtc(DateTimeOffset.Parse(now).UtcDateTime, 2));

    [Fact]
    public void Defaults_are_valid_and_every_numeric_setting_rejects_unsafe_values()
    {
        new RetentionSettings().Validate();
        foreach (var property in typeof(RetentionSettings).GetProperties().Where(p => p.PropertyType == typeof(int)))
        {
            foreach (var invalid in new[] { -1, int.MaxValue })
            {
                var settings = new RetentionSettings(); property.SetValue(settings, invalid);
                Assert.Throws<InvalidOperationException>(settings.Validate);
            }
            if (property.Name != nameof(RetentionSettings.RunAtHour))
            {
                var settings = new RetentionSettings(); property.SetValue(settings, 0);
                Assert.Throws<InvalidOperationException>(settings.Validate);
            }
        }
    }
}
