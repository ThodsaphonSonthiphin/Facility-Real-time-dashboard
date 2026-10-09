using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>Development and test data only. Every name and phone number is made up: this repo is public.</summary>
public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;

        var buildingA = new Building { Code = "A", Name = "ตึก A", CreatedAt = now };
        var area1 = new Area { Building = buildingA, Code = "AR01", Name = "Area 1 ชั้น 1", ShiftPattern = ShiftPattern.DayAndNight, CreatedAt = now };
        var area2 = new Area { Building = buildingA, Code = "AR02", Name = "Office ชั้น 2", ShiftPattern = ShiftPattern.DayOnly, CreatedAt = now };

        var menRestroom = new ServicePoint { Area = area1, Name = "ห้องน้ำชาย ชั้น 1", SortOrder = 1, CreatedAt = now };
        var womenRestroom = new ServicePoint { Area = area1, Name = "ห้องน้ำหญิง ชั้น 1", SortOrder = 2, CreatedAt = now };
        var meetingRoom = new ServicePoint { Area = area2, Name = "ห้องประชุม", SortOrder = 1, CreatedAt = now };

        db.Signs.AddRange(
            Located(new Sign { Area = area1, Code = "AR01-IN", QrToken = "token-checkin-ar01", QrIssuedAt = now }, 13.756300m, 100.501800m),
            Located(new Sign { Area = area1, ServicePoint = menRestroom, Code = "AR01-01", QrToken = "token-restroom-m1", QrIssuedAt = now }, 13.756350m, 100.501850m),
            Located(new Sign { Area = area1, ServicePoint = womenRestroom, Code = "AR01-02", QrToken = "token-restroom-f1", QrIssuedAt = now }, 13.756250m, 100.501750m),
            new Sign { Area = area2, Code = "AR02-IN", QrToken = "token-checkin-ar02", QrIssuedAt = now },
            new Sign { Area = area2, ServicePoint = meetingRoom, Code = "AR02-01", QrToken = "token-meeting-room", QrIssuedAt = now });

        foreach (var restroom in new[] { menRestroom, womenRestroom })
        {
            db.PointRoundWindows.AddRange(
                Window(restroom, Shift.Day, 7, 9),
                Window(restroom, Shift.Day, 16, 18),
                Window(restroom, Shift.Night, 20, 22),
                Window(restroom, Shift.Night, 3, 5));
        }

        db.PointRoundWindows.Add(Window(meetingRoom, Shift.Day, 8, 10));

        db.Users.AddRange(
            new User { Role = UserRole.Admin, Username = "admin", DisplayName = "ผู้ดูแลระบบ", SecretHash = hasher.Hash("admin1234"), CreatedAt = now },
            Employee(UserRole.Cleaner, "E1001", "0810000001", "สมชาย ใจดี", area1, null, Shift.Day),
            Employee(UserRole.Cleaner, "E1002", "0810000002", "สมหญิง รักสะอาด", area1, null, Shift.Night),
            Employee(UserRole.Cleaner, "E1003", "0810000003", "สมศรี มีสุข", area2, null, Shift.Day),
            Employee(UserRole.Supervisor, "S2001", "0820000001", "สมปอง ตรวจดี", null, buildingA, Shift.Day));

        await db.SaveChangesAsync();

        User Employee(UserRole role, string employeeId, string phone, string name, Area? area, Building? building, Shift shift) => new()
        {
            Role = role,
            EmployeeId = employeeId,
            SecretHash = hasher.Hash(PhoneNumber.Normalize(phone)),
            DisplayName = name,
            Area = area,
            Building = building,
            Shift = shift,
            CreatedAt = now,
        };

        // Made-up coordinates; AR02's signs are left uncaptured so both cases can be tried
        Sign Located(Sign sign, decimal latitude, decimal longitude)
        {
            sign.Latitude = latitude;
            sign.Longitude = longitude;
            sign.LocationAccuracyM = 10;
            sign.LocationSource = LocationSource.Site;
            sign.LocatedAt = now;
            return sign;
        }

        PointRoundWindow Window(ServicePoint point, Shift shift, int startHour, int endHour) => new()
        {
            ServicePoint = point,
            Shift = shift,
            StartTime = new TimeOnly(startHour, 0),
            EndTime = new TimeOnly(endHour, 0),
            CreatedAt = now,
        };
    }
}
