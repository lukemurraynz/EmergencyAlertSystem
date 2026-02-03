using Microsoft.AspNetCore.Mvc;
using EmergencyAlerts.Application.Commands;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Application.Ports;
using EmergencyAlerts.Domain.Exceptions;

namespace EmergencyAlerts.Api.Controllers;

/// <summary>
/// Controller for cancelling active alerts.
/// </summary>
[ApiController]
[Route("api/v1/alerts/{alertId}")]
[Produces("application/json")]
public class CancelController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly IAuthService _authService;
    private readonly ILogger<CancelController> _logger;

    public CancelController(
        IAlertService alertService,
        IAuthService authService,
        ILogger<CancelController> logger)
    {
        _alertService = alertService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Cancels an active alert.
    /// </summary>
    /// <param name="alertId">Alert ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated alert with cancelled status</returns>
    /// <response code="200">Alert cancelled successfully</response>
    /// <response code="400">Alert cannot be cancelled from current status</response>
    /// <response code="403">User does not have operator role</response>
    /// <response code="404">Alert not found</response>
    [HttpPut("cancel")]
    [ProducesResponseType(typeof(AlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertDto>> CancelAlert(
        Guid alertId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cancelling alert: {AlertId}", alertId);

        // Validate role - operators can cancel their own alerts
        await _authService.ValidateRoleAsync("operator", cancellationToken);

        // Get current user
        var user = await _authService.GetCurrentUserAsync(cancellationToken);

        var command = new CancelAlertCommand(alertId, user.UserId);

        try
        {
            var result = await _alertService.CancelAlertAsync(command, cancellationToken);

            _logger.LogInformation("Alert cancelled successfully: {AlertId}", alertId);

            return Ok(result.Alert);
        }
        catch (AlertNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Alert Not Found",
                Detail = $"Alert with ID {alertId} was not found."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Operation",
                Detail = ex.Message
            });
        }
    }
}
