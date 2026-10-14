using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Points;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>Finds seeded rows by the codes printed on signs, and reads back what the admin endpoints wrote.</summary>
public static class AdminSetupApi
{
    public static async Task<T> ReadAsync<T>(FacilityApiFactory factory, Func<AppDbContext, Task<T>> query)
    {
        T result = default!;
        await factory.WithDbAsync(async db => result = await query(db));
        return result;
    }

    public static Task<int> AreaIdAsync(FacilityApiFactory factory, string code) =>
        ReadAsync(factory, db => db.Areas.Where(a => a.Code == code).Select(a => a.Id).SingleAsync());

    public static Task<int> BuildingIdAsync(FacilityApiFactory factory, string code = "A") =>
        ReadAsync(factory, db => db.Buildings.Where(b => b.Code == code).Select(b => b.Id).SingleAsync());

    public static Task<int> SignIdAsync(FacilityApiFactory factory, string code) =>
        ReadAsync(factory, db => db.Signs.Where(s => s.Code == code).Select(s => s.Id).SingleAsync());

    /// <summary>The point whose sign is printed with <paramref name="signCode"/>, e.g. AR01-01.</summary>
    public static Task<int> PointIdAsync(FacilityApiFactory factory, string signCode) =>
        ReadAsync(factory, db => db.Signs.Where(s => s.Code == signCode).Select(s => s.ServicePointId!.Value).SingleAsync());

    public static Task<int> UserIdAsync(FacilityApiFactory factory, string employeeId) =>
        ReadAsync(factory, db => db.Users.Where(u => u.EmployeeId == employeeId).Select(u => u.Id).SingleAsync());

    public static Task<List<AuditEntry>> AuditAsync(FacilityApiFactory factory) =>
        ReadAsync(factory, db => db.AuditLog.AsNoTracking().OrderBy(a => a.Id).ToListAsync());

    public static async Task<string> MessageAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<MessageModel>())!.Message;

    /// <summary>The names on the Admin Dashboard right now.</summary>
    public static async Task<List<string>> DashboardNamesAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<PointModel>>("/api/service-points"))!.Select(p => p.Name).ToList();
}
