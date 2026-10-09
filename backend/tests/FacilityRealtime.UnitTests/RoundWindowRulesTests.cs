using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.UnitTests;

/// <summary>ADR facility-0047 rule 1 and its 2026-10-09 amendment: what the Admin may save as a point's Round Windows.</summary>
public class RoundWindowRulesTests
{
    private static RoundWindowInput W(Shift shift, string start, string end)
    {
        Assert.True(RoundWindowRules.TryParseTime(start, out var s));
        Assert.True(RoundWindowRules.TryParseTime(end, out var e));
        return new RoundWindowInput(shift, s, e);
    }

    [Fact]
    public void The_seeded_restroom_rounds_are_valid()
    {
        var windows = new[]
        {
            W(Shift.Day, "07:00", "09:00"), W(Shift.Day, "16:00", "18:00"),
            W(Shift.Night, "20:00", "22:00"), W(Shift.Night, "03:00", "05:00"),
        };

        Assert.Null(RoundWindowRules.Validate(windows, ShiftPattern.DayAndNight));
    }

    [Fact]
    public void A_night_window_may_cross_midnight()
    {
        Assert.Null(RoundWindowRules.Validate([W(Shift.Night, "23:00", "01:00")], ShiftPattern.DayAndNight));
    }

    [Fact]
    public void Touching_windows_do_not_overlap()
    {
        Assert.Null(RoundWindowRules.Validate(
            [W(Shift.Day, "07:00", "09:00"), W(Shift.Day, "09:00", "11:00")], ShiftPattern.DayAndNight));
    }

    [Fact]
    public void No_windows_is_valid()
    {
        Assert.Null(RoundWindowRules.Validate([], ShiftPattern.DayOnly));
    }

    [Theory]
    [InlineData(Shift.Day, "18:00", "19:00", "เวลาเปลี่ยนกะ")]   // amendment 2026-10-09
    [InlineData(Shift.Night, "05:00", "07:00", "เวลาเปลี่ยนกะ")]
    [InlineData(Shift.Day, "06:00", "08:00", "ต้องอยู่ในกะเช้า")]
    [InlineData(Shift.Day, "17:00", "20:00", "ต้องอยู่ในกะเช้า")]
    [InlineData(Shift.Night, "18:00", "20:00", "ต้องอยู่ในกะดึก")]
    [InlineData(Shift.Night, "06:00", "08:00", "ต้องอยู่ในกะดึก")]
    [InlineData(Shift.Day, "10:00", "09:00", "หลังเวลาเริ่ม")]
    [InlineData(Shift.Day, "10:00", "10:00", "หลังเวลาเริ่ม")]
    [InlineData(Shift.Night, "02:00", "23:00", "หลังเวลาเริ่ม")]
    public void A_window_outside_its_shift_or_ending_at_the_shift_change_is_refused(Shift shift, string start, string end, string expected)
    {
        var error = RoundWindowRules.Validate([W(shift, start, end)], ShiftPattern.DayAndNight);

        Assert.NotNull(error);
        Assert.Contains(expected, error);
    }

    [Fact]
    public void A_day_only_area_has_no_night_windows()
    {
        var error = RoundWindowRules.Validate([W(Shift.Night, "20:00", "22:00")], ShiftPattern.DayOnly);

        Assert.NotNull(error);
        Assert.Contains("เฉพาะกะเช้า", error);
    }

    [Theory]
    [InlineData(Shift.Day, "07:00", "09:00", "08:00", "10:00")]
    [InlineData(Shift.Night, "23:00", "01:00", "00:30", "02:00")]
    [InlineData(Shift.Day, "07:00", "09:00", "07:00", "09:00")]
    public void Overlapping_windows_are_refused(Shift shift, string start1, string end1, string start2, string end2)
    {
        var error = RoundWindowRules.Validate([W(shift, start1, end1), W(shift, start2, end2)], ShiftPattern.DayAndNight);

        Assert.NotNull(error);
        Assert.Contains("ซ้อนกัน", error);
    }

    [Theory]
    [InlineData("07:00", 7, 0)]
    [InlineData("23:59", 23, 59)]
    [InlineData("00:00", 0, 0)]
    public void Times_are_read_as_two_digit_hours_and_minutes(string text, int hour, int minute)
    {
        Assert.True(RoundWindowRules.TryParseTime(text, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
        Assert.Equal(text, RoundWindowRules.Format(time));
    }

    [Theory]
    [InlineData("7:00")]
    [InlineData("07:00:00")]
    [InlineData("24:00")]
    [InlineData("ab")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_time_formats_are_refused(string? text)
    {
        Assert.False(RoundWindowRules.TryParseTime(text, out _));
    }

    [Theory]
    [InlineData(Shift.Day, "07:00", "09:00", "DAY 07:00-09:00")]
    [InlineData(Shift.Night, "23:00", "01:00", "NIGHT 23:00-01:00")]
    public void Describe_names_the_shift_and_the_window_for_the_audit_log(Shift shift, string start, string end, string expected)
    {
        Assert.Equal(expected, RoundWindowRules.Describe(W(shift, start, end)));
    }

    [Theory]
    [InlineData(Shift.Day, "07:00", "18:59")]
    [InlineData(Shift.Night, "19:00", "06:59")]
    public void The_last_minute_before_the_shift_change_is_still_valid(Shift shift, string start, string end)
    {
        Assert.Null(RoundWindowRules.Validate([W(shift, start, end)], ShiftPattern.DayAndNight));
    }
}
