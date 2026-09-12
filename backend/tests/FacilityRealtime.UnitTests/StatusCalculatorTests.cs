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
    public void CalculateStatus_WhenOutsideWorkingHours_ReturnsOffHours_IfNoIssue()
    {
        var point = new ServicePoint { Id = 1, Name = "Point A", CleaningIntervalMinutes = 60, IsActive = true };
        var lastScan = new ScanRecord 
        { 
            ServicePointId = 1, 
            Status = ScanStatus.Normal, 
            ScannedAt = new DateTime(2026, 9, 13, 16, 30, 0, DateTimeKind.Utc) 
        };
        
        // Current time is 19:00 (After 17:00 WorkEnd)
        var eveningTime = new DateTime(2026, 9, 13, 19, 0, 0, DateTimeKind.Utc);
        var status = StatusCalculator.CalculateStatus(lastScan, point, eveningTime, WorkStart, WorkEnd);
        
        Assert.Equal(PointStatus.OffHours, status);
    }

    [Fact]
    public void CalculateStatus_WhenElapsedExceedsInterval_ReturnsOverdue()
    {
        var point = new ServicePoint { Id = 1, Name = "Point A", CleaningIntervalMinutes = 60, IsActive = true };
        // Cleaned at 09:00, current time is 10:30 (90 minutes > 60 interval)
        var lastScan = new ScanRecord 
        { 
            ServicePointId = 1, 
            Status = ScanStatus.Normal, 
            ScannedAt = new DateTime(2026, 9, 13, 9, 0, 0, DateTimeKind.Utc) 
        };
        
        var currentTime = new DateTime(2026, 9, 13, 10, 30, 0, DateTimeKind.Utc);
        var status = StatusCalculator.CalculateStatus(lastScan, point, currentTime, WorkStart, WorkEnd);
        
        Assert.Equal(PointStatus.Overdue, status);
    }

    [Fact]
    public void CalculateStatus_WhenCleanedWithinInterval_ReturnsNormal()
    {
        var point = new ServicePoint { Id = 1, Name = "Point A", CleaningIntervalMinutes = 60, IsActive = true };
        // Cleaned at 09:30, current time is 10:00 (30 minutes <= 60 interval)
        var lastScan = new ScanRecord 
        { 
            ServicePointId = 1, 
            Status = ScanStatus.Normal, 
            ScannedAt = new DateTime(2026, 9, 13, 9, 30, 0, DateTimeKind.Utc) 
        };
        
        var currentTime = new DateTime(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);
        var status = StatusCalculator.CalculateStatus(lastScan, point, currentTime, WorkStart, WorkEnd);
        
        Assert.Equal(PointStatus.Normal, status);
    }
}
