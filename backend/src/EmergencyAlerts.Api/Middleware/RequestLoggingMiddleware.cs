using System.Diagnostics;
using System.Text;

namespace EmergencyAlerts.Api.Middleware;

/// <summary>
/// Middleware for structured request/response logging with correlation IDs.
/// Logs HTTP method, path, status code, duration, and correlation ID.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for health check endpoints to reduce noise
        if (context.Request.Path.StartsWithSegments("/api/v1/health/live"))
        {
            await _next(context);
            return;
        }

        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "N/A";
        var stopwatch = Stopwatch.StartNew();

        // Log incoming request
        _logger.LogInformation(
            "HTTP {Method} {Path} started. CorrelationId: {CorrelationId}, UserAgent: {UserAgent}",
            context.Request.Method,
            context.Request.Path,
            correlationId,
            context.Request.Headers.UserAgent.ToString());

        // Log request body for non-GET requests (in development only)
        if (context.Request.Method != HttpMethods.Get &&
            context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            await LogRequestBodyAsync(context, correlationId);
        }

        // Capture original response body stream
        var originalBodyStream = context.Response.Body;

        try
        {
            // Use a memory stream to capture response body for logging
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            stopwatch.Stop();

            // Log response
            LogResponse(context, correlationId, stopwatch.ElapsedMilliseconds);

            // Copy response body back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "HTTP {Method} {Path} failed. CorrelationId: {CorrelationId}, Duration: {DurationMs}ms",
                context.Request.Method,
                context.Request.Path,
                correlationId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private async Task LogRequestBodyAsync(HttpContext context, string correlationId)
    {
        context.Request.EnableBuffering();

        var body = await new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true)
            .ReadToEndAsync();

        context.Request.Body.Position = 0;

        if (!string.IsNullOrWhiteSpace(body))
        {
            _logger.LogDebug(
                "Request body for {CorrelationId}: {Body}",
                correlationId,
                body.Length > 1000 ? body[..1000] + "..." : body);
        }
    }

    private void LogResponse(HttpContext context, string correlationId, long durationMs)
    {
        var statusCode = context.Response.StatusCode;
        var logLevel = statusCode >= 500 ? LogLevel.Error :
                       statusCode >= 400 ? LogLevel.Warning :
                       LogLevel.Information;

        _logger.Log(
            logLevel,
            "HTTP {Method} {Path} completed. CorrelationId: {CorrelationId}, Status: {StatusCode}, Duration: {DurationMs}ms",
            context.Request.Method,
            context.Request.Path,
            correlationId,
            statusCode,
            durationMs);

        // Log slow requests
        if (durationMs > 1000)
        {
            _logger.LogWarning(
                "Slow request detected. CorrelationId: {CorrelationId}, Duration: {DurationMs}ms",
                correlationId,
                durationMs);
        }
    }
}

/// <summary>
/// Extension method to register the request logging middleware.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
