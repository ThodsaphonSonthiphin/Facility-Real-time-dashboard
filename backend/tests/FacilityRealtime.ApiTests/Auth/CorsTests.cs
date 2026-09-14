using System.Net.Http;
using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests.Auth;

public class CorsTests
{
    /// <summary>
    /// Final whole-branch review Finding 2: the "AllowAll" CORS policy allows any origin AND credentials.
    /// Nothing cross-origin needs the refresh cookie (the dashboard only ever calls same-origin, relative
    /// URLs), so a cross-site page must not be able to read back an Access-Control-Allow-Credentials: true
    /// for /api/auth/refresh.
    /// </summary>
    [Fact]
    public async Task Cross_origin_preflight_for_refresh_does_not_allow_credentials()
    {
        using var factory = new FacilityApiFactory();
        var client = factory.CreateApiClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/refresh");
        request.Headers.Add("Origin", "http://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        var allowsCredentials = response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var values)
            && values.Contains("true");
        Assert.False(allowsCredentials);
    }
}
