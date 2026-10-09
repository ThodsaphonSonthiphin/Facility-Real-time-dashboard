namespace FacilityRealtime.Api.Endpoints;

internal static class ApiResults
{
    /// <summary>A Thai message the phone shows as is.</summary>
    public static IResult Message(int statusCode, string message) => Results.Json(new { message }, statusCode: statusCode);
}
