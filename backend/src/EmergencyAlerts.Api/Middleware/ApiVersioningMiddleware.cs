using System;

namespace EmergencyAlerts.Api.Middleware;

/// <summary>
/// Middleware for API versioning via query parameter (?api-version=YYYY-MM-DD).
/// Per csharp.instructions.md: use query param versioning for non-breaking changes.
/// Defaults to latest stable if omitted.
/// </summary>
public class ApiVersioningMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiVersioningMiddleware> _logger;
    private const string CurrentVersion = "2026-01-25";
    private const string VersionQueryParam = "api-version";

    public ApiVersioningMiddleware(RequestDelegate next, ILogger<ApiVersioningMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract api-version from query string or header (prefer query)
        var requestedVersion = context.Request.Query[VersionQueryParam].ToString();
        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            requestedVersion = context.Request.Headers[VersionQueryParam].ToString();
        }

        var versionWasExplicitlyProvided = !string.IsNullOrWhiteSpace(requestedVersion);
        var version = versionWasExplicitlyProvided ? requestedVersion : CurrentVersion;

        // Validate version format only if explicitly provided
        if (versionWasExplicitlyProvided && !IsValidVersionFormat(version))
        {
            _logger.LogWarning("Invalid API version format requested: {Version}", version);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "InvalidVersion",
                    message = $"Invalid API version format. Expected YYYY-MM-DD, got '{version}'.",
                    target = VersionQueryParam
                }
            });
            return;
        }

        // Store version in HttpContext for use in controllers/responses
        context.Items["ApiVersion"] = version;

        // Add version header to response (use Append per ASP.NET analyzer)
        context.Response.Headers.Append("X-Api-Version", version);

        await _next(context);
    }

    private static bool IsValidVersionFormat(string version)
    {
        // Accept YYYY-MM-DD format or numeric YYYYMMDD
        return DateTime.TryParse(version, out _) || version.Length == 8 && int.TryParse(version, out _);
    }
}

/// <summary>
/// Extension method to register the API versioning middleware.
/// </summary>
public static class ApiVersioningMiddlewareExtensions
{
    public static IApplicationBuilder UseApiVersioning(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiVersioningMiddleware>();
    }
}
