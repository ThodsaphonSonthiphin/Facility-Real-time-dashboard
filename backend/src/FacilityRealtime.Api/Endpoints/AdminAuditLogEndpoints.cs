using System.Security.Claims;
using System.Text.Json;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Audit;
using FacilityRealtime.Infrastructure.Persistence;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>The "ประวัติ Admin" tab (wireframe D9, facility-0057): Admin only and read-only. Opening it is not logged.</summary>
public static class AdminAuditLogEndpoints
{
    public const int DefaultLimit = 100;

    public const int MaxLimit = 500;

    public const int MaxSearchLength = 100;

    public static IEndpointRouteBuilder MapAdminAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/audit-log").RequireAuthorization(AuthSetup.AdminOnly);
        admin.MapGet("/", ListAsync);
        admin.MapGet("/actors", async (ClaimsPrincipal principal, AppDbContext db) =>
            await CurrentUser.LoadAsync(principal, db) is null
                ? Results.Unauthorized()
                : Results.Ok((await AuditLogQuery.ActorsAsync(db)).Select(a => new AuditActorDto(a.Id, a.DisplayName)).ToList()));
        return app;
    }

    private static async Task<IResult> ListAsync(
        string? from,
        string? to,
        int? actorId,
        string? category,
        long? beforeId,
        int? limit,
        string? q,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider clock)
    {
        if (await CurrentUser.LoadAsync(principal, db) is null)
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (AdminQuery.ReadDateRange(from, to, now, defaultDays: 7, maxDays: 92, out var fromUtc, out var toUtc) is { } rangeError)
        {
            return Bad(rangeError);
        }

        IReadOnlyList<string>? actions = null;
        if (category is not null)
        {
            if (!AdminQuery.TryReadEnum<AuditCategory>(category, out var parsed))
            {
                return Bad("ไม่มีประเภทนี้ในประวัติ");
            }

            actions = AuditCategories.ActionsOf(parsed);
        }

        var take = limit ?? DefaultLimit;
        if (take is < 1 or > MaxLimit)
        {
            return Bad($"limit ต้องอยู่ระหว่าง 1–{MaxLimit}");
        }

        var search = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (search?.Length > MaxSearchLength)
        {
            return Bad($"คำค้นยาวได้ไม่เกิน {MaxSearchLength} ตัวอักษร");
        }

        var rows = await AuditLogQuery.LoadAsync(db, new AuditLogFilter(fromUtc, toUtc, actorId, actions, beforeId, take, search));
        var entries = rows.Select(ToDto).ToList();
        return Results.Ok(new AuditLogPageDto(entries, entries.Count == take ? entries[^1].Id : (long?)null));
    }

    private static AuditLogEntryDto ToDto(AuditLogRow row) => new(
        row.Entry.Id,
        DateTime.SpecifyKind(row.Entry.OccurredAt, DateTimeKind.Utc),
        row.Entry.ActorId,
        row.ActorName,
        row.Entry.Action,
        AuditCategories.Of(row.Entry.Action),
        row.Entry.EntityType,
        row.Entry.EntityId,
        row.Entry.Summary,
        Json(row.Entry.BeforeJson),
        Json(row.Entry.AfterJson),
        row.Entry.Reason);

    /// <summary>The stored snapshot as a JSON value, so the page can show its fields.</summary>
    private static JsonElement? Json(string? stored)
    {
        if (stored is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(stored);
        return document.RootElement.Clone();
    }

    private static IResult Bad(string message) => ApiResults.Message(StatusCodes.Status400BadRequest, message);
}
