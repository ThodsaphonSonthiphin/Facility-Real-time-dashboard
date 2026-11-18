using System.Security.Claims;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Infrastructure.Persistence;

namespace JanitorialManagementDashboard.Api.Endpoints;

/// <summary>
/// Spec §11 and ADR facility-0204: the one transfer route, <c>POST /api/area-transfers/{id}/confirm</c> — the transferred
/// person's arrival at the destination Area's Check-In Sign. The instruction itself is an ordinary schedule command
/// (<c>instruct-transfer</c>, <c>cancel-transfer</c>), so there is no second write path and no second audit shape; only the
/// arrival is its own route, because it is the Cleaner's own actual fact and not a management command.
/// <para>
/// The actor is always the principal, never a field in the body, and the instruction's own person is the only account the
/// service will answer: anybody else is a 403 that says nothing about the instruction. The body is the page's read revision,
/// the scanned token and the phone's location; the GPS policy itself is the existing one.
/// </para>
/// </summary>
public static class AreaTransferEndpoints
{
    public static IEndpointRouteBuilder MapAreaTransferEndpoints(this IEndpointRouteBuilder app)
    {
        // Any signed-in account may call it; whether this instruction is theirs is decided inside, from the principal.
        app.MapPost("/api/area-transfers/{id:int}/confirm", ConfirmAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> ConfirmAsync(
        int id,
        ConfirmTransferRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        TransferCommandService transfers,
        CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var (result, refusal) = await transfers.ConfirmAsync(
            user.Id, id, request.ExpectedRevision, request.QrToken,
            new GpsInputDto(request.Latitude, request.Longitude, request.AccuracyM), ct);

        return result is not null
            ? Results.Ok(result)
            : ApiResults.Message(StatusFor(refusal!.Code), refusal.Message);
    }

    /// <summary>
    /// The status a refusal deserves. Another person's instruction is 403 and says nothing about it; a token that is not a
    /// sign (or no longer an active Area's sign) is 404, exactly as the scan route answers an unknown sign; a missing
    /// location or a missing instruction revision is the client's own 400; and every state conflict is 409.
    /// </summary>
    private static int StatusFor(string code) => code switch
    {
        "transfer-not-yours" => StatusCodes.Status403Forbidden,
        "transfer-not-found" or "sign-not-found" => StatusCodes.Status404NotFound,
        "gps-required" or "revision-required" => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status409Conflict,
    };
}

/// <summary>
/// Spec §9: one arrival confirmation as the phone submits it — the instruction revision the page read, the scanned Check-In
/// Sign token, and the location every scan already sends. All of them are optional at the binding level so a missing one is
/// answered with a message rather than a binding failure; the GPS values are validated by the shared policy.
/// </summary>
public record ConfirmTransferRequest(
    string? ExpectedRevision,
    string? QrToken,
    double? Latitude,
    double? Longitude,
    double? AccuracyM);
