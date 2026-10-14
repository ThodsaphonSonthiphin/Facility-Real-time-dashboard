using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.ApiTests.Points;

namespace FacilityRealtime.ApiTests.Inspections;

public record InspectionSubmissionModel(
    long Id, DateTime SubmittedAt, string CleanerName, string Placement, string Status, List<string> IssueTags, string? Note);

public record InspectionScanModel(
    PointModel Point, InspectionSubmissionModel? Submission, string? Refusal, bool IsCover, int? DistanceM, bool? WithinRadius);

public record BlockedModel(string Message, string AreaCode, string AreaName, string Reason, string BuildingCode);

/// <summary>
/// The Supervisor's phone. Seeded S2001 (phone 0820000001) is the Day Supervisor of building A, which holds AR01 (both
/// restrooms) and AR02 (the meeting room). Log in before moving the clock.
/// </summary>
public static class InspectionApi
{
    public const string MenRestroom = "token-restroom-m1";
    public const string BuildingBPoint = "token-br01-01";

    public static async Task<HttpClient> SupervisorAsync(FacilityApiFactory factory, string employeeId = "S2001", string phone = "0820000001") =>
        (await AuthApi.LoggedInAsEmployeeAsync(factory, employeeId, phone)).Client;

    public static Task<HttpResponseMessage> ScanAsync(
        HttpClient client,
        string qrToken = MenRestroom,
        double latitude = TestGps.NearLatitude,
        double longitude = TestGps.NearLongitude,
        double accuracyM = 10) =>
        client.PostAsJsonAsync("/api/inspections/scan", new { qrToken, latitude, longitude, accuracyM });

    /// <summary>Scans and asserts 200.</summary>
    public static async Task<InspectionScanModel> ScannedAsync(HttpClient client, string qrToken = MenRestroom)
    {
        var response = await ScanAsync(client, qrToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InspectionScanModel>())!;
    }

    /// <summary>Building B with Area BR01 and one point: sign BR01-01 (token-br01-01) at TestGps.Near, rounds Day 08-10 and Night 20-22.</summary>
    public static async Task AddBuildingBAsync(FacilityApiFactory factory)
    {
        await TestData.AddBuildingAsync(factory, "B");
        await TestData.AddAreaAsync(factory, "BR01", "B");
        await TestData.AddPointAsync(factory, "BR01", "BR01-01", BuildingBPoint);
    }
}
