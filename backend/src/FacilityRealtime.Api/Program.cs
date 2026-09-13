using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 0. Working Hours: one system-wide value from config (ADR 0005 / 0019), injected into every status calculation
var workingHours = builder.Configuration.GetSection(WorkingHours.SectionName).Get<WorkingHours>() ?? new WorkingHours();
if (workingHours.Start >= workingHours.End)
{
    throw new InvalidOperationException($"WorkingHours: Start ({workingHours.Start}) must be before End ({workingHours.End}).");
}
builder.Services.AddSingleton(workingHours);

// 1. Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=facility_dashboard;Uid=root;Pwd=;CharSet=utf8mb4;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(connectionString));

// 2. SignalR with string enum serialization
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 3. CORS (Allow local network devices & dev servers)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 4. JSON Serialization with String Enum Converter
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

app.UseCors("AllowAll");

// 5. Seed initial data on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.SeedAsync(db);
}

// 6. SignalR Hub Mapping
app.MapHub<ScanHub>("/hubs/scan");

// 7. Minimal API Endpoints

// Healthcheck
app.MapGet("/", () => Results.Ok(new { status = "healthy", service = "Facility Real-time Dashboard API" }));

// Auth: Login
app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db) =>
{
    var hash = DbInitializer.HashPassword(req.Password);
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.PasswordHash == hash);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var token = Guid.NewGuid().ToString("N");
    return Results.Ok(new LoginResponse(user.Id, user.Username, user.FullName, user.Role, token));
});

// Service Points: List for Dashboard
app.MapGet("/api/service-points", async (AppDbContext db, WorkingHours workingHours) =>
{
    var points = await db.ServicePoints.Where(p => p.IsActive).ToListAsync();
    var nowUtc = DateTime.UtcNow;

    var result = new List<ServicePointStatusDto>();

    foreach (var point in points)
    {
        var latestScan = await db.ScanRecords
            .Include(r => r.User)
            .Where(r => r.ServicePointId == point.Id)
            .OrderByDescending(r => r.ScannedAt)
            .FirstOrDefaultAsync();

        var status = StatusCalculator.CalculateStatus(latestScan, point, nowUtc, workingHours);
        var minutesSince = latestScan != null ? (int)(nowUtc - latestScan.ScannedAt).TotalMinutes : 999;
        var tags = !string.IsNullOrEmpty(latestScan?.IssueTags) 
            ? latestScan.IssueTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() 
            : null;

        result.Add(new ServicePointStatusDto(
            point.Id,
            point.Name,
            point.Location,
            point.CleaningIntervalMinutes,
            point.QrToken,
            status,
            latestScan?.ScannedAt,
            latestScan?.User?.FullName,
            latestScan?.Status,
            tags,
            latestScan?.Notes,
            minutesSince
        ));
    }

    return Results.Ok(result);
});

// Service Points: Lookup by QR Token for Mobile Scanner
app.MapGet("/api/service-points/by-token/{token}", async (string token, AppDbContext db, WorkingHours workingHours) =>
{
    var point = await db.ServicePoints.FirstOrDefaultAsync(p => p.QrToken == token && p.IsActive);
    if (point == null)
    {
        return Results.NotFound(new { message = $"Service point with QR token '{token}' not found." });
    }

    var nowUtc = DateTime.UtcNow;
    var latestScan = await db.ScanRecords
        .Include(r => r.User)
        .Where(r => r.ServicePointId == point.Id)
        .OrderByDescending(r => r.ScannedAt)
        .FirstOrDefaultAsync();

    var status = StatusCalculator.CalculateStatus(latestScan, point, nowUtc, workingHours);
    var minutesSince = latestScan != null ? (int)(nowUtc - latestScan.ScannedAt).TotalMinutes : 999;
    var tags = !string.IsNullOrEmpty(latestScan?.IssueTags) 
        ? latestScan.IssueTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() 
        : null;

    var dto = new ServicePointStatusDto(
        point.Id,
        point.Name,
        point.Location,
        point.CleaningIntervalMinutes,
        point.QrToken,
        status,
        latestScan?.ScannedAt,
        latestScan?.User?.FullName,
        latestScan?.Status,
        tags,
        latestScan?.Notes,
        minutesSince
    );

    return Results.Ok(dto);
});

// Scan Records: Submit Scan & Broadcast Real-Time Update
app.MapPost("/api/scan-records", async (
    CreateScanRecordRequest req, 
    AppDbContext db, 
    IHubContext<ScanHub> hubContext,
    WorkingHours workingHours) =>
{
    var point = await db.ServicePoints.FirstOrDefaultAsync(p => p.QrToken == req.QrToken && p.IsActive);
    if (point == null)
    {
        return Results.NotFound(new { message = $"Invalid QR token '{req.QrToken}'." });
    }

    var user = await db.Users.FindAsync(req.UserId);
    if (user == null)
    {
        return Results.BadRequest(new { message = $"User ID '{req.UserId}' not found." });
    }

    var nowUtc = DateTime.UtcNow;
    var scanRecord = new ScanRecord
    {
        ServicePointId = point.Id,
        UserId = user.Id,
        Status = req.Status,
        IssueTags = req.IssueTags != null && req.IssueTags.Count > 0 ? string.Join(",", req.IssueTags) : null,
        Notes = req.Notes,
        ScannedAt = nowUtc
    };

    db.ScanRecords.Add(scanRecord);
    await db.SaveChangesAsync();

    // Calculate new point status
    var newStatus = StatusCalculator.CalculateStatus(scanRecord, point, nowUtc, workingHours);

    // Build updated DTO for broadcast
    var updatedDto = new ServicePointStatusDto(
        point.Id,
        point.Name,
        point.Location,
        point.CleaningIntervalMinutes,
        point.QrToken,
        newStatus,
        scanRecord.ScannedAt,
        user.FullName,
        scanRecord.Status,
        req.IssueTags,
        scanRecord.Notes,
        0
    );

    // Broadcast to all connected SignalR clients (e.g. Dashboard)
    await hubContext.Clients.All.SendAsync("ScanRecorded", updatedDto);

    return Results.Created($"/api/scan-records/{scanRecord.Id}", new ScanRecordCreatedResponse(
        scanRecord.Id,
        point.Id,
        newStatus,
        scanRecord.ScannedAt
    ));
});

app.Run();
