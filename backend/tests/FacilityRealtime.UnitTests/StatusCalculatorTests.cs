using System;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Xunit;

namespace FacilityRealtime.UnitTests;

/// <summary>
/// Every case is written in Thai wall-clock time (Asia/Bangkok, UTC+7) and converted to UTC
/// through <see cref="Thai"/>, so a test reads the way the rule in ADR facility-0005 reads.
/// Default scenario: Working Hours 08:00-17:00, Cleaning Interval 60 minutes, date 2026-09-14.
/// </summary>
public class StatusCalculatorTests
{
    private static readonly WorkingHours Hours = new() { Start = new TimeOnly(8, 0), End = new TimeOnly(17, 0) };

    private static ServicePoint Point(int intervalMinutes = 60) =>
        new() { Id = 1, Name = "Point A", CleaningIntervalMinutes = intervalMinutes, IsActive = true };

    private static ScanRecord Scan(ScanStatus status, DateTime scannedAtUtc) =>
        new() { ServicePointId = 1, Status = status, ScannedAt = scannedAtUtc };

    /// <summary>Thai wall-clock on 2026-09-14 (or an earlier day via <paramref name="dayOffset"/>) as a UTC instant.</summary>
    private static DateTime Thai(int hour, int minute, int dayOffset = 0) =>
        new DateTime(2026, 9, 14 + dayOffset, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);

    // ---- Rule 1: Issue beats everything -------------------------------------------------

    [Fact]
    public void Issue_scan_is_red_during_working_hours()
    {
        var status = StatusCalculator.CalculateStatus(Scan(ScanStatus.Issue, Thai(10, 0)), Point(), Thai(10, 30), Hours);
        Assert.Equal(PointStatus.Issue, status);
    }

    [Fact]
    public void Issue_scan_is_red_even_off_hours()
    {
        var status = StatusCalculator.CalculateStatus(Scan(ScanStatus.Issue, Thai(16, 50)), Point(), Thai(22, 0), Hours);
        Assert.Equal(PointStatus.Issue, status);
    }

    // ---- Rule 2: Off Hours beats a recent Normal scan (the reversal of a7f87c2) ---------

    [Fact]
    public void Normal_scan_minutes_before_closing_is_grey_after_closing()
    {
        // scanned 16:50, look at 17:20 -> Off Hours, not Normal-until-17:50
        var status = StatusCalculator.CalculateStatus(Scan(ScanStatus.Normal, Thai(16, 50)), Point(), Thai(17, 20), Hours);
        Assert.Equal(PointStatus.OffHours, status);
    }

    [Fact]
    public void Test_scan_at_night_is_grey_not_green()
    {
        var status = StatusCalculator.CalculateStatus(Scan(ScanStatus.Normal, Thai(20, 0)), Point(), Thai(20, 10), Hours);
        Assert.Equal(PointStatus.OffHours, status);
    }

    [Fact]
    public void Before_opening_is_grey()
    {
        var status = StatusCalculator.CalculateStatus(null, Point(), Thai(7, 30), Hours);
        Assert.Equal(PointStatus.OffHours, status);
    }

    [Fact]
    public void Closing_time_itself_is_off_hours()
    {
        // 17:00 sharp is outside [08:00, 17:00)
        var status = StatusCalculator.CalculateStatus(Scan(ScanStatus.Normal, Thai(16, 30)), Point(), Thai(17, 0), Hours);
        Assert.Equal(PointStatus.OffHours, status);
    }

    // ---- Rules 3/4: the first cycle counts from today's opening -------------------------

    [Fact]
    public void Scanned_yesterday_is_green_at_0830_today()
    {
        // cycle_start = max(yesterday 16:00, today 08:00) = 08:00 -> due 09:00
        var status = StatusCalculator.CalculateStatus(Scan(ScanStatus.Normal, Thai(16, 0, dayOffset: -1)), Point(), Thai(8, 30), Hours);
        Assert.Equal(PointStatus.Normal, status);
    }

    [Fact]
    public void Scanned_yesterday_is_orange_from_0901_today()
    {
        var status = StatusCalculator.CalculateStatus(Scan(ScanStatus.Normal, Thai(16, 0, dayOffset: -1)), Point(), Thai(9, 1), Hours);
        Assert.Equal(PointStatus.Overdue, status);
    }

    [Fact]
    public void Never_scanned_is_green_until_opening_plus_interval()
    {
        Assert.Equal(PointStatus.Normal, StatusCalculator.CalculateStatus(null, Point(), Thai(8, 30), Hours));
        Assert.Equal(PointStatus.Overdue, StatusCalculator.CalculateStatus(null, Point(), Thai(9, 1), Hours));
    }

    [Fact]
    public void Exactly_at_due_time_is_still_green()
    {
        // ADR: Overdue when now > due_at, no grace period, so 09:00 sharp is the last Normal instant
        var status = StatusCalculator.CalculateStatus(null, Point(), Thai(9, 0), Hours);
        Assert.Equal(PointStatus.Normal, status);
    }

    [Fact]
    public void Scan_before_opening_counts_from_opening()
    {
        // scanned 07:30 -> cycle_start = 08:00 -> due 09:00 (not 08:30)
        var scan = Scan(ScanStatus.Normal, Thai(7, 30));
        Assert.Equal(PointStatus.Normal, StatusCalculator.CalculateStatus(scan, Point(), Thai(8, 45), Hours));
        Assert.Equal(PointStatus.Overdue, StatusCalculator.CalculateStatus(scan, Point(), Thai(9, 1), Hours));
    }

    // ---- Rules 3/4: a scan during the day starts the next cycle (ADR 0005 worked example) --

    [Fact]
    public void Adr_example_scan_at_1005_is_due_at_1105()
    {
        var scan = Scan(ScanStatus.Normal, Thai(10, 5));
        Assert.Equal(PointStatus.Normal, StatusCalculator.CalculateStatus(scan, Point(), Thai(11, 0), Hours));
        Assert.Equal(PointStatus.Overdue, StatusCalculator.CalculateStatus(scan, Point(), Thai(11, 6), Hours));
    }

    [Fact]
    public void Interval_is_per_point()
    {
        var scan = Scan(ScanStatus.Normal, Thai(10, 0));
        Assert.Equal(PointStatus.Overdue, StatusCalculator.CalculateStatus(scan, Point(intervalMinutes: 30), Thai(10, 45), Hours));
        Assert.Equal(PointStatus.Normal, StatusCalculator.CalculateStatus(scan, Point(intervalMinutes: 120), Thai(10, 45), Hours));
    }

    // ---- Working Hours come from the caller (config), not from a default in the calculator --

    [Fact]
    public void Working_hours_from_config_are_respected()
    {
        var lateShift = new WorkingHours { Start = new TimeOnly(9, 0), End = new TimeOnly(18, 0) };
        Assert.Equal(PointStatus.OffHours, StatusCalculator.CalculateStatus(null, Point(), Thai(8, 30), lateShift));
        Assert.Equal(PointStatus.Normal, StatusCalculator.CalculateStatus(null, Point(), Thai(9, 30), lateShift));
        // 17:30 is inside the late shift (so not grey) but past the 10:00 due time, and grey under the default hours
        Assert.Equal(PointStatus.Overdue, StatusCalculator.CalculateStatus(null, Point(), Thai(17, 30), lateShift));
        Assert.Equal(PointStatus.OffHours, StatusCalculator.CalculateStatus(null, Point(), Thai(17, 30), Hours));
    }

    // ---- Timezone: the working-hours check is done on Asia/Bangkok time, not UTC ---------

    [Fact]
    public void Working_hours_are_evaluated_in_thai_time()
    {
        // 02:00 UTC is 09:00 in Bangkok: working hours, even though 02:00 is outside 08:00-17:00
        var twoAmUtc = new DateTime(2026, 9, 14, 2, 0, 0, DateTimeKind.Utc);
        Assert.Equal(PointStatus.Normal, StatusCalculator.CalculateStatus(null, Point(), twoAmUtc, Hours));
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(StatusCalculator.ToThaiTime(twoAmUtc)));
    }
}
