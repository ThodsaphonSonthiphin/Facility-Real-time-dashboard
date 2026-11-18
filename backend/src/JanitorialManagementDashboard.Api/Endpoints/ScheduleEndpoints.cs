using System.Globalization;
using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>
/// Spec §11 and acceptance A01: the five scheduling routes. Reads answer the authorised projection and writes go through the
/// one command engine, so a screen cannot change a plan without an identity, an expected revision and a dated authority.
/// <para>
/// Two properties are properties of this file and nothing below it. First, the whole group is absent — a plain 404 for
/// every actor — until an owner activates scheduling (<see cref="SchedulingSettings.Enabled"/>): the feature is not
/// "disabled" by refusing requests, it is not part of the route table at all. Second, every management route carries the
/// admin-or-Supervisor policy, which the authorization middleware applies <em>before</em> the request body is bound, so an
/// unauthorised caller with a wrong body is refused as unauthorised and never reaches a handler.
/// </para>
/// <para>
/// The actor is always read from the principal with <see cref="CurrentUser.LoadAsync"/>, never taken from the query or the
/// payload, and each handler then asks <see cref="ScheduleAuthority"/> about the specific building, shift and date it is
/// about to touch.
/// </para>
/// </summary>
public static class ScheduleEndpoints
{
    /// <summary>The route group every scheduling screen and the future consumers share.</summary>
    public const string Group = "/api/schedules";

    /// <summary>
    /// Spec §11 and the activation boundary: while scheduling is not activated, every <c>/api/schedules</c> request answers
    /// 404 — for every actor, for every method, and before authentication or body binding runs. It is registered before
    /// <c>UseAuthentication</c> so a disabled feature cannot even be probed for which accounts exist, and the routes below
    /// are not mapped at all in that state, so the answer is the same one an unknown path gives.
    /// </summary>
    public static IApplicationBuilder UseScheduleFeatureGate(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        // Resolved from the container, not from a setting captured in Program, so a host that replaces the settings (the
        // API tests do) decides the answer.
        var settings = app.ApplicationServices.GetRequiredService<SchedulingSettings>();
        return settings.Enabled
            ? app
            : app.Use((context, next) => context.Request.Path.StartsWithSegments(Group)
                ? AnswerNotFoundAsync(context)
                : next(context));
    }

    private static Task AnswerNotFoundAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.ServiceProvider.GetRequiredService<SchedulingSettings>();
        if (!settings.Enabled)
        {
            // Not activated: no route exists, so nothing below can be reached even if the gate were removed.
            return app;
        }

        // Even the group itself needs a signed-in account, so a route added later cannot be open by omission.
        var group = app.MapGroup(Group).RequireAuthorization();

        // Any signed-in account reads its own authorised week; a Cleaner sees only their own row (spec §3).
        group.MapGet("/me", ReadMineAsync);

        // The management surface: an Admin anywhere, a Supervisor inside the dated periods it holds today.
        group.MapGet("", ReadManagedAsync).RequireAuthorization(AuthSetup.ScheduleManager);
        group.MapGet("/relief-candidates", ReadReliefCandidateAsync).RequireAuthorization(AuthSetup.ScheduleManager);
        group.MapPost("/command-tokens", IssueTokenAsync).RequireAuthorization(AuthSetup.ScheduleManager);
        group.MapPost("/commands", ExecuteAsync).RequireAuthorization(AuthSetup.ScheduleManager);
        group.MapGet("/commands/{id:guid}", ReadResultAsync).RequireAuthorization(AuthSetup.ScheduleManager);
        return app;
    }

    /// <summary>Spec §3 and §11: the caller's own authorised week, with no filter a caller could widen it with.</summary>
    private static async Task<IResult> ReadMineAsync(
        DateOnly weekStart, ClaimsPrincipal principal, AppDbContext db, ScheduleStore store, CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok((await store.ReadWeekAsync(user.Id, weekStart, null, null, ct)).ToDto());
    }

    /// <summary>
    /// Spec §3, §10 and A01: the scoped management week. A supplied building or shift narrows the read; the store decides
    /// the actor's scope from its dated periods, so a forged query returns nothing rather than another team's roster.
    /// </summary>
    private static async Task<IResult> ReadManagedAsync(
        DateOnly weekStart, int? buildingId, Shift? shift, ClaimsPrincipal principal, AppDbContext db,
        ScheduleStore store, CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok((await store.ReadWeekAsync(user.Id, weekStart, buildingId, shift, ct)).ToDto());
    }

    /// <summary>
    /// Spec §6 and A09: the lookup a recorder makes by employee id and source building before borrowing a relief. It carries
    /// the management policy like every other management route, binds the four parameters itself so a missing or unreadable
    /// one is a 400 rather than a 500, and answers only the minimum identity, home building, dated shift and eligibility
    /// status — never the person's schedule and never another team's rows. Somebody who is not an active Relief Cleaner of
    /// that source building is not described at all.
    /// </summary>
    private static async Task<IResult> ReadReliefCandidateAsync(
        string? employeeId, int? sourceBuildingId, string? date, string? shift, int? destinationAreaId,
        int? ownUserId, ClaimsPrincipal principal, AppDbContext db, ScheduleStore store, TimeProvider clock, CancellationToken ct)
    {
        var actor = await CurrentUser.LoadAsync(principal, db);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if ((ownUserId is null && string.IsNullOrWhiteSpace(employeeId)) || sourceBuildingId is null
            || !AdminQuery.TryReadDate(date, out var shiftDate)
            || !AdminQuery.TryReadEnum<Shift>(shift, out var parsedShift)
            || (ownUserId is not null && destinationAreaId is null))
        {
            return ApiResults.Message(StatusCodes.Status400BadRequest,
                "ต้องระบุรหัสพนักงาน ตึกต้นทาง วันที่ และกะเช้าหรือกะดึกให้ครบ");
        }

        if (destinationAreaId is int areaId)
        {
            var destination = await db.Areas.AsNoTracking().SingleOrDefaultAsync(area => area.Id == areaId && area.IsActive, ct);
            var periods = await SupervisorScopes.ReadAsync(db, actor, clock, ct);
            var today = ScheduleAuthority.BusinessDate(clock.GetUtcNow().UtcDateTime);
            bool CanChange(int building) => shiftDate < today
                ? ScheduleAuthority.CanCorrectHistory(actor.Role, actor.Id, building, parsedShift, shiftDate, today, periods)
                : ScheduleAuthority.CanManage(actor.Role, actor.Id, building, parsedShift, shiftDate, periods);
            if (destination is null || !destination.HasShift(parsedShift) || !CanChange(destination.BuildingId)
                || (ownUserId is not null && !CanChange(sourceBuildingId.Value)))
            {
                return Results.Forbid();
            }
        }

        var candidate = await store.ReadReliefCandidateAsync(employeeId?.Trim(), sourceBuildingId.Value,
            shiftDate, parsedShift, ct, ownUserId, requireSameShift: destinationAreaId is not null);
        if (candidate is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound,
                "ไม่พบพนักงานสแปร์ของตึกต้นทางนี้ในวันที่และกะที่เลือก");
        }

        var result = candidate.ToDto();
        if (destinationAreaId is not null)
        {
            var responsible = await store.ResponsibleSupervisorAsync(sourceBuildingId.Value, parsedShift, shiftDate, ct);
            var supervisorName = await db.Users.AsNoTracking().Where(user => user.Id == responsible)
                .Select(user => user.DisplayName).FirstOrDefaultAsync(ct);
            var live = await store.LiveReliefAsync(candidate.UserId, shiftDate, parsedShift, ct);
            result = result with
            {
                SourceSupervisorId = responsible,
                SourceSupervisorName = supervisorName,
                AgreementRequired = responsible != actor.Id,
                Revision = (live?.Revision ?? 0).ToString(CultureInfo.InvariantCulture),
            };
        }

        return Results.Ok(result);
    }

    /// <summary>
    /// Spec §11: the short-lived identity a mutation is submitted under. It is issued for the authenticated actor and
    /// nothing else — the client never chooses the id — and the engine refuses a command whose identity does not match.
    /// </summary>
    private static async Task<IResult> IssueTokenAsync(
        ClaimsPrincipal principal, AppDbContext db, ScheduleCommandTokenService tokens, TimeProvider clock)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var issued = tokens.Issue(user.Id, clock.GetUtcNow());
        return Results.Ok(new ScheduleCommandTokenDto(issued.Id, issued.Value, issued.ExpiresAt));
    }

    /// <summary>
    /// Spec §11 and A18: run one submitted batch. The engine decides whether the identity may start a command, whether a
    /// retained result answers instead, and whether every submitted row is still current; nothing is written when it is not.
    /// </summary>
    private static async Task<IResult> ExecuteAsync(
        ScheduleCommandEnvelope request, ClaimsPrincipal principal, AppDbContext db, ScheduleCommandService commands,
        CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        return user is null
            ? Results.Unauthorized()
            : Answer(await commands.ExecuteAsync(user.Id, request, ct));
    }

    /// <summary>Spec §11 and A19: the retained result of an uncertain write, only for the actor and scope that made it.</summary>
    private static async Task<IResult> ReadResultAsync(
        Guid id, ClaimsPrincipal principal, AppDbContext db, ScheduleCommandService commands, CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await commands.ReadResultAsync(user.Id, id, ct);
        return result is null ? Results.NotFound() : Answer(result);
    }

    /// <summary>
    /// Spec §11: the answer as an HTTP status a screen can branch on. Only <see cref="ScheduleCommandOutcome.Committed"/>
    /// means the facts changed; every other status says nothing was written, and a refused batch keeps its problems so the
    /// client can show which rows it refused without losing the input.
    /// </summary>
    private static IResult Answer(CommandResult result) => result.Outcome switch
    {
        ScheduleCommandOutcome.Committed => Results.Ok(result),
        ScheduleCommandOutcome.Rejected => Results.Json(result, statusCode: StatusCodes.Status409Conflict),
        ScheduleCommandOutcome.PayloadMismatch => Results.Json(result, statusCode: StatusCodes.Status409Conflict),
        ScheduleCommandOutcome.Expired => Results.Json(result, statusCode: StatusCodes.Status410Gone),
        ScheduleCommandOutcome.NotAuthorised => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Json(result, statusCode: StatusCodes.Status400BadRequest),
    };
}
