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

        var start = workStart ?? new TimeOnly(8, 0);
        var end = workEnd ?? new TimeOnly(17, 0);
        var currentLocalTime = TimeOnly.FromDateTime(currentTimeUtc);

        // Priority 2: Off Hours
        if (currentLocalTime < start || currentLocalTime >= end)
        {
            return PointStatus.OffHours;
        }

        // Priority 3 & 4: Working hours overdue vs normal
        DateTime todayStart = currentTimeUtc.Date.Add(start.ToTimeSpan());
        DateTime baseTime = todayStart;

        if (latestScan != null && latestScan.ScannedAt > todayStart)
        {
            baseTime = latestScan.ScannedAt;
        }

        var elapsed = currentTimeUtc - baseTime;
        if (elapsed > TimeSpan.FromMinutes(point.CleaningIntervalMinutes))
        {
            return PointStatus.Overdue;
        }

        return PointStatus.Normal;
    }
}
