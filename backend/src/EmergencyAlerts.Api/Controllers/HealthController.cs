using Microsoft.AspNetCore.Mvc;
using EmergencyAlerts.Application.Ports;

namespace EmergencyAlerts.Api.Controllers;

/// <summary>
/// Controller for health checks and service status.
/// </summary>
[ApiController]
[Route("api/v1/health")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly IDrasiHealthService _drasiHealthService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IDrasiHealthService drasiHealthService,
        ILogger<HealthController> logger)
    {
        _drasiHealthService = drasiHealthService;
        _logger = logger;
    }

    /// <summary>
    /// Comprehensive health check including Drasi status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health status</returns>
    /// <response code="200">Service is healthy</response>
    /// <response code="503">Service is unhealthy</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthCheckResponse>> GetHealth(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Health check requested");

        var drasiHealth = await _drasiHealthService.GetHealthStatusAsync(cancellationToken);

        var response = new HealthCheckResponse
        {
            Status = drasiHealth.IsHealthy ? "Healthy" : "Unhealthy",
            Timestamp = DateTime.UtcNow,
            Checks = new Dictionary<string, ComponentHealth>
            {
                ["drasi"] = new ComponentHealth
                {
                    Status = drasiHealth.IsHealthy ? "Healthy" : "Unhealthy",
                    ErrorMessage = drasiHealth.ErrorMessage,
                    LastCheckTime = drasiHealth.LastCheckTime
                },
                ["api"] = new ComponentHealth
                {
                    Status = "Healthy",
                    LastCheckTime = DateTime.UtcNow
                }
            }
        };

        if (!drasiHealth.IsHealthy)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Liveness probe - checks if the API is running.
    /// </summary>
    /// <returns>HTTP 200 if alive</returns>
    /// <response code="200">API is alive</response>
    [HttpGet("live")]
    [ProducesResponseType(typeof(LivenessResponse), StatusCodes.Status200OK)]
    public ActionResult<LivenessResponse> GetLiveness()
    {
        return Ok(new LivenessResponse
        {
            Status = "Alive",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Readiness probe - checks if the API is ready to accept traffic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP 200 if ready, HTTP 503 if not ready</returns>
    /// <response code="200">API is ready</response>
    /// <response code="503">API is not ready</response>
    [HttpGet("ready")]
    [ProducesResponseType(typeof(ReadinessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ReadinessResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ReadinessResponse>> GetReadiness(
        CancellationToken cancellationToken)
    {
        var drasiHealth = await _drasiHealthService.GetHealthStatusAsync(cancellationToken);

        var response = new ReadinessResponse
        {
            Status = drasiHealth.IsHealthy ? "Ready" : "NotReady",
            Timestamp = DateTime.UtcNow,
            DrasiHealthy = drasiHealth.IsHealthy
        };

        if (!drasiHealth.IsHealthy)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        return Ok(response);
    }
}

/// <summary>
/// Health check response model.
/// </summary>
public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, ComponentHealth> Checks { get; set; } = new();
}

/// <summary>
/// Component health details.
/// </summary>
public class ComponentHealth
{
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime? LastCheckTime { get; set; }
}

/// <summary>
/// Liveness probe response.
/// </summary>
public class LivenessResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Readiness probe response.
/// </summary>
public class ReadinessResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool DrasiHealthy { get; set; }
}
