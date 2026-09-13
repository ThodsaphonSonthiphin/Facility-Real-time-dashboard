using System;
using System.Linq;
using System.Threading.Tasks;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context, IPasswordHasher hasher)
    {
        // 1. Seed Users if empty
        if (!await context.Users.AnyAsync())
        {
            var cleaner = new User
            {
                Username = "somchai",
                PasswordHash = hasher.Hash("password123"),
                FullName = "สมชาย ใจดี",
                Role = "cleaner",
                CreatedAt = DateTime.UtcNow
            };

            var admin = new User
            {
                Username = "admin",
                PasswordHash = hasher.Hash("admin1234"),
                FullName = "ผู้ดูแลระบบ",
                Role = "admin",
                CreatedAt = DateTime.UtcNow
            };

            await context.Users.AddRangeAsync(cleaner, admin);
            await context.SaveChangesAsync();
        }

        // 2. Seed Service Points if empty
        if (!await context.ServicePoints.AnyAsync())
        {
            var points = new[]
            {
                new ServicePoint
                {
                    Name = "ห้องน้ำชาย ชั้น 1",
                    Location = "อาคาร A ชั้น 1 (ทิศเหนือ)",
                    CleaningIntervalMinutes = 60,
                    QrToken = "token-restroom-m1",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new ServicePoint
                {
                    Name = "ห้องน้ำหญิง ชั้น 1",
                    Location = "อาคาร A ชั้น 1 (ทิศใต้)",
                    CleaningIntervalMinutes = 60,
                    QrToken = "token-restroom-f1",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new ServicePoint
                {
                    Name = "จุดแยกขยะ โซน B",
                    Location = "อาคาร B โถงกลาง",
                    CleaningIntervalMinutes = 120,
                    QrToken = "token-waste-zone-b",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            await context.ServicePoints.AddRangeAsync(points);
            await context.SaveChangesAsync();
        }
    }
}
