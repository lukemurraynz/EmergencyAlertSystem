namespace EmergencyAlerts.Api.Middleware;

/// <summary>
/// Middleware for generating and propagating correlation IDs across requests.
/// Supports both generating new correlation IDs and accepting them from X-Correlation-ID headers.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check for existing correlation ID in request header
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();

        // Generate new correlation ID if not provided
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        // Store correlation ID in HttpContext.Items for use by other middleware/handlers
        context.Items["CorrelationId"] = correlationId;

        // Add correlation ID to response headers
        context.Response.Headers.Append(CorrelationIdHeader, correlationId);

        // Add correlation ID to logging scope
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }
}

/// <summary>
/// Extension method to register the correlation ID middleware.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
