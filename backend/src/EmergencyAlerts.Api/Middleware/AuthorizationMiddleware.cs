using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmergencyAlerts.Api.Middleware;

/// <summary>
/// Middleware for Microsoft Entra ID (Azure AD) authentication and authorization.
/// Validates JWT tokens and ensures required claims are present.
/// </summary>
public class AuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthorizationMiddleware> _logger;

    public AuthorizationMiddleware(
        RequestDelegate next,
        ILogger<AuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var hostEnvironment = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();

        // Skip authentication for health check endpoints
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (path.StartsWith("/health") || path.StartsWith("/api/v1/health"))
        {
            _logger.LogDebug("Bypassing authorization for health endpoint: {Path}", context.Request.Path);
            await _next(context);
            return;
        }

        // Skip authentication for OpenAPI/Swagger
        if (path.StartsWith("/openapi") || path.StartsWith("/swagger"))
        {
            _logger.LogDebug("Bypassing authorization for OpenAPI endpoint: {Path}", context.Request.Path);
            await _next(context);
            return;
        }

        // Skip authentication for public configuration endpoints (maps config + token)
        if (path.StartsWith("/api/v1/config/maps"))
        {
            _logger.LogDebug("Bypassing authorization for Maps config endpoint: {Path}", context.Request.Path);
            await _next(context);
            return;
        }

        var allowAnonymous = ResolveAllowAnonymous(configuration);

        // Allow anonymous access when explicitly configured (demo/local scenarios)
        if (allowAnonymous)
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "demo-user-id"),
                    new Claim(ClaimTypes.Email, "demo@example.com"),
                    new Claim(ClaimTypes.Role, "operator"),
                    new Claim(ClaimTypes.Role, "approver"),
                    new Claim(ClaimTypes.Role, "coordinator")
                };

                var identity = new ClaimsIdentity(claims, "Anonymous");
                context.User = new ClaimsPrincipal(identity);
            }

            _logger.LogWarning("Bypassing authorization (Auth:AllowAnonymous=true) for {Path}", context.Request.Path);
            await _next(context);
            return;
        }

        // In development or test, create a mock user for testing if no auth is present
        if (hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Test"))
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                // Create mock authenticated user for development/test
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "dev-user-id"),
                    new Claim(ClaimTypes.Email, "dev@example.com"),
                    new Claim(ClaimTypes.Role, "operator"),
                    new Claim(ClaimTypes.Role, "approver"),
                    new Claim(ClaimTypes.Role, "coordinator")
                };

                var identity = new ClaimsIdentity(claims, "Development");
                context.User = new ClaimsPrincipal(identity);

                _logger.LogWarning("Using mock authentication for development. User: {UserId}", "dev-user-id");
            }
        }
        else
        {
            // Production: Require authenticated user
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                _logger.LogWarning("Unauthorized request to {Path}", context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.com/401",
                    title = "Unauthorized",
                    status = 401,
                    detail = "Authentication is required to access this resource."
                });
                return;
            }

            // Validate required claims
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Missing user identifier claim");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.com/403",
                    title = "Forbidden",
                    status = 403,
                    detail = "User identifier claim is missing."
                });
                return;
            }
        }

        await _next(context);
    }

    private static bool ResolveAllowAnonymous(IConfiguration configuration)
    {
        // Environment variable override so deployments can force behavior even if config providers disagree.
        var envValue = Environment.GetEnvironmentVariable("Auth__AllowAnonymous");
        if (!string.IsNullOrWhiteSpace(envValue) && bool.TryParse(envValue, out var envAllowAnonymous))
        {
            return envAllowAnonymous;
        }

        return configuration.GetValue<bool>("Auth:AllowAnonymous");
    }
}

/// <summary>
/// Extension method to register the authorization middleware.
/// </summary>
public static class AuthorizationMiddlewareExtensions
{
    public static IApplicationBuilder UseEntraIdAuthorization(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuthorizationMiddleware>();
    }
}
