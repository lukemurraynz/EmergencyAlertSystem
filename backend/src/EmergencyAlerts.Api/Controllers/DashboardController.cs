using Microsoft.AspNetCore.Mvc;
using EmergencyAlerts.Application.Queries;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Application.Ports;

namespace EmergencyAlerts.Api.Controllers;

/// <summary>
/// Controller for dashboard summary and analytics.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IAlertService alertService,
        ILogger<DashboardController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    /// <summary>
    /// Gets dashboard summary with alert counts, SLA breaches, and correlations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dashboard summary data</returns>
    /// <response code="200">Dashboard summary retrieved</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryDto>> GetDashboardSummary(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting dashboard summary");

        var query = new GetDashboardSummaryQuery();
        var result = await _alertService.GetDashboardSummaryAsync(query, cancellationToken);

        return Ok(result.Summary);
    }

    /// <summary>
    /// Gets recent correlation events.
    /// </summary>
    /// <param name="limit">Maximum number of correlations to return (default: 10, max: 50)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of correlation events</returns>
    /// <response code="200">Correlations retrieved</response>
    [HttpGet("correlations")]
    [ProducesResponseType(typeof(List<CorrelationEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CorrelationEventDto>>> GetCorrelations(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting recent correlations - Limit: {Limit}", limit);

        // Validate limit
        if (limit < 1) limit = 10;
        if (limit > 50) limit = 50;

        var query = new GetDashboardSummaryQuery();
        var result = await _alertService.GetDashboardSummaryAsync(query, cancellationToken);

        var correlations = result.Summary.RecentCorrelations.Take(limit).ToList();

        return Ok(correlations);
    }
}
