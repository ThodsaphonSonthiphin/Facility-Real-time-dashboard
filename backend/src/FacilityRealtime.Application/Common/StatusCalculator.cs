using System;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Application.Common;

public static class StatusCalculator
{
    public static PointStatus CalculateStatus(
        ScanRecord? latestScan,
        ServicePoint point,
        DateTime currentTimeUtc,
        TimeOnly? workStart = null,
        TimeOnly? workEnd = null)
    {
        // Priority 1: Issue has highest precedence (ADR 0005)
        if (latestScan != null && latestScan.Status == ScanStatus.Issue)
        {
            return PointStatus.Issue;
        }

        // Priority 2: If recently cleaned within its interval, it is Normal (Clean)
        if (latestScan != null && latestScan.Status == ScanStatus.Normal)
        {
            var elapsed = currentTimeUtc - latestScan.ScannedAt;
            if (elapsed <= TimeSpan.FromMinutes(point.CleaningIntervalMinutes))
            {
                return PointStatus.Normal;
            }
        }

        // Priority 3 & 4: When cleaning interval has expired (or point hasn't been cleaned)
        // Convert to Thailand Time (Asia/Bangkok, UTC+7) per ADR 0005
        var thaiTime = ToThaiTime(currentTimeUtc);
        var currentLocalTime = TimeOnly.FromDateTime(thaiTime);
        var start = workStart ?? new TimeOnly(8, 0);
        var end = workEnd ?? new TimeOnly(17, 0);

        bool isWorkingHours = currentLocalTime >= start && currentLocalTime < end;

        // Outside working hours without recent cleaning -> Off Hours (Gray)
        if (!isWorkingHours)
        {
            return PointStatus.OffHours;
        }

        // During working hours with expired cleaning -> Overdue (Orange)
        return PointStatus.Overdue;
    }

    public static DateTime ToThaiTime(DateTime utcTime)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
        }
        catch
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
            }
            catch
            {
                return utcTime.AddHours(7);
            }
        }
    }
}
