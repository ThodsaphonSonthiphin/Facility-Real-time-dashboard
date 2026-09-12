using System;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Xunit;

namespace FacilityRealtime.UnitTests;

public class StatusCalculatorTests
{
    private static readonly TimeOnly WorkStart = new(8, 0);
    private static readonly TimeOnly WorkEnd = new(17, 0);

    [Fact]
    public void CalculateStatus_WhenLatestScanIsIssue_ReturnsIssueRegardlessOfTime()
    {
        var point = new ServicePoint { Id = 1, Name = "Point A", CleaningIntervalMinutes = 60, IsActive = true };
        var lastScan = new ScanRecord 
        { 
            ServicePointId = 1, 
            Status = ScanStatus.Issue, 
            ScannedAt = DateTime.UtcNow.AddMinutes(-10) 
        };
        
        var status = StatusCalculator.CalculateStatus(lastScan, point, DateTime.UtcNow, WorkStart, WorkEnd);
        
        Assert.Equal(PointStatus.Issue, status);
    }

    [Fact]
    public void CalculateStatus_WhenCleanedWithinInterval_ReturnsNormal_EvenDuringOffHours()
    {
        var point = new ServicePoint { Id = 1, Name = "Point A", CleaningIntervalMinutes = 60, IsActive = true };
        // Cleaned 10 minutes ago at night (e.g. 02:00 AM)
        var lastScan = new ScanRecord 
        { 
            ServicePointId = 1, 
            Status = ScanStatus.Normal, 
            ScannedAt = DateTime.UtcNow.AddMinutes(-10) 
        };
        
        var status = StatusCalculator.CalculateStatus(lastScan, point, DateTime.UtcNow, WorkStart, WorkEnd);
        
        Assert.Equal(PointStatus.Normal, status);
    }

    [Fact]
    public void CalculateStatus_WhenElapsedExceedsInterval_DuringOffHours_ReturnsOffHours()
    {
        var point = new ServicePoint { Id = 1, Name = "Point A", CleaningIntervalMinutes = 60, IsActive = true };
        // 19:00 Thai time is 12:00 UTC
        // Last cleaned at 16:30 Thai time (09:30 UTC), elapsed = 150 mins > 60 interval
        var lastScan = new ScanRecord 
        { 
            ServicePointId = 1, 
            Status = ScanStatus.Normal, 
            ScannedAt = new DateTime(2026, 9, 13, 9, 30, 0, DateTimeKind.Utc) 
        };
        
        var eveningUtc = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc); // 19:00 Thai time
        var status = StatusCalculator.CalculateStatus(lastScan, point, eveningUtc, WorkStart, WorkEnd);
        
        Assert.Equal(PointStatus.OffHours, status);
    }

    [Fact]
    public void CalculateStatus_WhenElapsedExceedsInterval_DuringWorkingHours_ReturnsOverdue()
    {
        var point = new ServicePoint { Id = 1, Name = "Point A", CleaningIntervalMinutes = 60, IsActive = true };
        // 09:00 Thai time is 02:00 UTC
        var lastScan = new ScanRecord 
        { 
            ServicePointId = 1, 
            Status = ScanStatus.Normal, 
            ScannedAt = new DateTime(2026, 9, 13, 2, 0, 0, DateTimeKind.Utc) 
        };
        
        // 10:30 Thai time is 03:30 UTC (90 minutes > 60 interval)
        var currentUtc = new DateTime(2026, 9, 13, 3, 30, 0, DateTimeKind.Utc);
        var status = StatusCalculator.CalculateStatus(lastScan, point, currentUtc, WorkStart, WorkEnd);
        
        Assert.Equal(PointStatus.Overdue, status);
    }
}
