using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace EmergencyAlerts.Api.Middleware;

/// <summary>
/// Middleware for per-user rate limiting.
/// Implements sliding window rate limiting with different limits per role.
/// Rate limits are configurable via Azure App Configuration for operational flexibility.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly IHostEnvironment _env;

    // In-memory sliding window storage (user ID -> request timestamps)
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _requestHistory = new();
    private readonly TimeSpan _windowDuration;

    public RateLimitMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<RateLimitMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
        _env = env;

        // Read window duration from configuration (default: 1 minute)
        var windowMinutes = _configuration.GetValue<int>("RateLimit:WindowDurationMinutes", 1);
        _windowDuration = TimeSpan.FromMinutes(windowMinutes);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting in test mode or if test header is present
        if (_env.IsEnvironment("Test") || context.Request.Headers.ContainsKey("X-Bypass-RateLimit"))
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Skip rate limiting if user is not authenticated or is a health check
        if (string.IsNullOrWhiteSpace(userId) ||
            context.Request.Path.StartsWithSegments("/api/v1/health"))
        {
            await _next(context);
            return;
        }

        // Build endpoint key (METHOD:PATH)
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        // Normalize path for pattern matching (replace GUIDs with *)
        var normalizedPath = NormalizePath(path);
        var endpointKey = $"{method}:{normalizedPath}";

        // Get rate limit from configuration
        var rateLimit = GetRateLimitForEndpoint(endpointKey);
        if (rateLimit == null)
        {
            await _next(context);
            return;
        }

        // Get or create request history for this user
        var requestQueue = _requestHistory.GetOrAdd($"{userId}:{endpointKey}", _ => new ConcurrentQueue<DateTime>());

        // Clean up old requests outside the sliding window
        CleanupOldRequests(requestQueue);

        var now = DateTime.UtcNow;
        var requestsInWindow = requestQueue.Count;

        // Check if rate limit exceeded
        if (requestsInWindow >= rateLimit.Value.Limit)
        {
            var oldestRequest = requestQueue.TryPeek(out var oldest) ? oldest : now;
            var retryAfter = (int)Math.Ceiling((_windowDuration - (now - oldestRequest)).TotalSeconds);

            _logger.LogWarning(
                "Rate limit exceeded for user {UserId} on {Endpoint}. Limit: {Limit}, Current: {Current}",
                userId, endpointKey, rateLimit.Value.Limit, requestsInWindow);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.Append("Retry-After", retryAfter.ToString());
            context.Response.Headers.Append("X-RateLimit-Limit", rateLimit.Value.Limit.ToString());
            context.Response.Headers.Append("X-RateLimit-Remaining", "0");
            context.Response.Headers.Append("X-RateLimit-Reset", oldestRequest.AddMinutes(1).ToString("O"));

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.com/429",
                title = "Too Many Requests",
                status = 429,
                detail = $"Rate limit exceeded. Limit: {rateLimit.Value.Limit} requests per minute. Retry after {retryAfter} seconds.",
                retryAfter
            });

            return;
        }

        // Add current request to history
        requestQueue.Enqueue(now);

        // Add rate limit headers to response
        var remaining = rateLimit.Value.Limit - requestsInWindow - 1;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append("X-RateLimit-Limit", rateLimit.Value.Limit.ToString());
            context.Response.Headers.Append("X-RateLimit-Remaining", Math.Max(0, remaining).ToString());
            return Task.CompletedTask;
        });

        await _next(context);
    }

    /// <summary>
    /// Gets the rate limit configuration for a specific endpoint from App Configuration.
    /// Returns null if no rate limit is configured for this endpoint.
    /// </summary>
    private (int Limit, string RequiredRole)? GetRateLimitForEndpoint(string endpointKey)
    {
        // Map endpoint patterns to configuration keys
        var configKey = endpointKey switch
        {
            "POST:/api/v1/alerts" => "RateLimit:Operator:AlertCreation",
            "POST:/api/v1/alerts/*/approval" => "RateLimit:Approver:Approvals",
            "PUT:/api/v1/alerts/*/approval" => "RateLimit:Approver:Approvals",
            "DELETE:/api/v1/alerts/*/approval" => "RateLimit:Approver:Approvals",
            "POST:/api/v1/alerts/*/approvals" => "RateLimit:Approver:Approvals",
            "PUT:/api/v1/alerts/*/approvals" => "RateLimit:Approver:Approvals",
            "DELETE:/api/v1/alerts/*/approvals" => "RateLimit:Approver:Approvals",
            _ => null
        };

        if (configKey == null)
            return null;

        // Read limit from configuration (default values from FR-029)
        var limit = _configuration.GetValue<int>(configKey,
            configKey.Contains("AlertCreation") ? 10 : 30);

        var role = configKey.Contains("Operator") ? "operator" : "approver";

        return (limit, role);
    }

    private void CleanupOldRequests(ConcurrentQueue<DateTime> requestQueue)
    {
        var cutoff = DateTime.UtcNow.Subtract(_windowDuration);

        while (requestQueue.TryPeek(out var timestamp) && timestamp < cutoff)
        {
            requestQueue.TryDequeue(out _);
        }
    }

    private static string NormalizePath(string path)
    {
        // Replace GUIDs in path with * for pattern matching
        var segments = path.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            if (Guid.TryParse(segments[i], out _))
            {
                segments[i] = "*";
            }
        }
        return string.Join('/', segments);
    }
}

/// <summary>
/// Extension method to register the rate limit middleware.
/// </summary>
public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitMiddleware>();
    }
}
