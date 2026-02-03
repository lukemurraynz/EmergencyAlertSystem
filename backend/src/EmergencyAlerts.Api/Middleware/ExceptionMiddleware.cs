using System.Text.Json;
using EmergencyAlerts.Domain.Exceptions;

namespace EmergencyAlerts.Api.Middleware;

/// <summary>
/// Middleware for handling exceptions and converting them to RFC 7807 Problem Details.
/// Includes nested error object for Azure-style error responses and correlation ID.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        _logger.LogError(exception, "Unhandled exception occurred. CorrelationId: {CorrelationId}", correlationId);

        var (statusCode, errorCode, message, target) = exception switch
        {
            AlertNotFoundException ex => (StatusCodes.Status404NotFound, "AlertNotFound", ex.Message, (string?)null),
            InvalidPolygonException ex => (StatusCodes.Status422UnprocessableEntity, "InvalidPolygon", ex.Message, "areas"),
            ConcurrentApprovalAttemptException ex => (StatusCodes.Status409Conflict, "ConcurrentApprovalAttempt", ex.Message, (string?)null),
            DrasiUnavailableException ex => (StatusCodes.Status503ServiceUnavailable, "DrasiUnavailable", ex.Message, (string?)null),
            ArgumentException ex => (StatusCodes.Status400BadRequest, "InvalidArgument", ex.Message, ex.ParamName),
            InvalidOperationException ex => (StatusCodes.Status400BadRequest, "InvalidOperation", ex.Message, (string?)null),
            UnauthorizedAccessException ex => (StatusCodes.Status403Forbidden, "Forbidden", ex.Message, (string?)null),
            _ => (StatusCodes.Status500InternalServerError, "InternalServerError", "An unexpected error occurred.", (string?)null)
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        // Azure-style error response per csharp.instructions.md
        var problemDetails = new
        {
            error = new
            {
                code = errorCode,
                message,
                target,
                correlationId,
                timestamp = DateTime.UtcNow,
                stackTrace = _environment.IsDevelopment() ? exception.StackTrace : null
            }
        };

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment(),
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Extension method to register the exception middleware.
/// </summary>
public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionMiddleware>();
    }
}
