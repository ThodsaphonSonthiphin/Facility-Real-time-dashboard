namespace JanitorialManagementDashboard.Api;

/// <summary>
/// facility-0106: the browser's security headers, set by the app on every answer so tests keep them. The page loads
/// nothing from another site and runs no inline script, so the policy allows only this origin.
/// </summary>
public static class SecurityHeaders
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; " +
        "base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    /// <param name="swaggerWithoutCsp">True only in Development, the one place Swagger UI is mapped (facility-0049).</param>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool swaggerWithoutCsp) =>
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            // Swagger UI would break under this policy. Elsewhere a /swagger path is a page path that gets the app, so it
            // must keep the policy, or the path would be a way around it.
            if (!(swaggerWithoutCsp && context.Request.Path.StartsWithSegments("/swagger")))
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            }

            headers.XFrameOptions = "DENY"; // for browsers that do not know frame-ancestors
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer"; // a /scan/<token> link carries its QR Token in the path
            headers["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=()";
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                // Login and refresh answers carry an access token; the frontend's files keep their own caching (facility-0067)
                headers.CacheControl = "no-store";
            }

            return next(context);
        });
}
