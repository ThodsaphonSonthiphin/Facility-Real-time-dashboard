using System;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Common;

/// <summary>
/// Point Status rules, exactly as ADR facility-0005 states them (re-affirmed by facility-0019
/// after the code drifted). Checked in this order; the first match wins:
///   1. Issue     - the latest scan is ISSUE, shown red at any time of day
///   2. Off Hours - the current Asia/Bangkok time is outside Working Hours
///   3. Overdue   - now > due_at, where due_at = max(latest scan, today's opening) + Cleaning Interval
///   4. Normal    - otherwise
/// Working Hours are passed in (bound from config in Program.cs), never defaulted here, so a
/// call site cannot silently fall back to a hardcoded window again.
/// </summary>
public static class StatusCalculator
{
    private static readonly TimeZoneInfo ThaiZone = ResolveThaiZone();

    public static PointStatus CalculateStatus(
        ScanRecord? latestScan,
        ServicePoint point,
        DateTime currentTimeUtc,
        WorkingHours workingHours)
    {
        // 1. Issue beats everything, including Off Hours
        if (latestScan != null && latestScan.Status == ScanStatus.Issue)
        {
            return PointStatus.Issue;
        }

        var thaiNow = ToThaiTime(currentTimeUtc);
        var localTime = TimeOnly.FromDateTime(thaiNow);

        // 2. Off Hours beats Overdue and Normal: a scan minutes ago still shows grey after hours
        if (localTime < workingHours.Start || localTime >= workingHours.End)
        {
            return PointStatus.OffHours;
        }

        // 3 / 4. cycle_start = max(latest scan, today's opening); due_at = cycle_start + interval.
        // A point last scanned yesterday (or never) gets one full interval from opening, so the
        // board opens green at 08:00 and goes orange from 08:00 + interval if nobody has scanned.
        var todayOpeningUtc = ToUtc(thaiNow.Date.Add(workingHours.Start.ToTimeSpan()));
        var cycleStartUtc = latestScan != null && latestScan.ScannedAt > todayOpeningUtc
            ? latestScan.ScannedAt
            : todayOpeningUtc;

        var dueAtUtc = cycleStartUtc.AddMinutes(point.CleaningIntervalMinutes);

        // No grace period: strictly past due_at is Overdue; exactly at due_at is still Normal
        return currentTimeUtc > dueAtUtc ? PointStatus.Overdue : PointStatus.Normal;
    }

    public static DateTime ToThaiTime(DateTime utcTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), ThaiZone);
    }

    private static DateTime ToUtc(DateTime thaiLocalTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(thaiLocalTime, DateTimeKind.Unspecified), ThaiZone);
    }

    private static TimeZoneInfo ResolveThaiZone()
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
