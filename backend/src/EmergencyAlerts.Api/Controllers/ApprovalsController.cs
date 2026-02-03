using Microsoft.AspNetCore.Mvc;
using EmergencyAlerts.Application.Commands;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Application.Ports;
using EmergencyAlerts.Domain.Exceptions;

namespace EmergencyAlerts.Api.Controllers;

/// <summary>
/// Controller for alert approvals and rejections.
/// </summary>
[ApiController]
[Route("api/v1/alerts/{alertId}/approval")]
[Produces("application/json")]
public class ApprovalsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly IAuthService _authService;
    private readonly ILogger<ApprovalsController> _logger;

    public ApprovalsController(
        IAlertService alertService,
        IAuthService authService,
        ILogger<ApprovalsController> logger)
    {
        _alertService = alertService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Approves an alert (first-wins semantics with optimistic locking).
    /// </summary>
    /// <param name="alertId">Alert ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated alert with approval record</returns>
    /// <response code="200">Alert approved successfully</response>
    /// <response code="403">User does not have approver role</response>
    /// <response code="404">Alert not found</response>
    /// <response code="409">Concurrent approval attempt detected</response>
    /// <response code="412">ETag precondition failed</response>
    /// <response code="503">Drasi service unavailable</response>
    [HttpPost]
    [HttpPut] // Support both POST and PUT for approval
    [ProducesResponseType(typeof(AlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AlertDto>> ApproveAlert(
        Guid alertId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Approving alert: {AlertId}", alertId);

        // Validate role
        await _authService.ValidateRoleAsync("approver", cancellationToken);

        // Get current user
        var user = await _authService.GetCurrentUserAsync(cancellationToken);

        // Get ETag from If-Match header (optional but recommended)
        var etag = Request.Headers.IfMatch.FirstOrDefault()?.Trim('"');

        var command = new ApproveAlertCommand(alertId, user.UserId, etag);

        try
        {
            var result = await _alertService.ApproveAlertAsync(command, cancellationToken);

            _logger.LogInformation("Alert approved successfully: {AlertId}", alertId);

            // Add new ETag
            var typedHeaders = Response.GetTypedHeaders();
            typedHeaders.ETag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{result.Alert.UpdatedAt.Ticks}\"");
            Response.Headers["ETag"] = $"\"{result.Alert.UpdatedAt.Ticks}\"";

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
        catch (ConcurrentApprovalAttemptException ex)
        {
            _logger.LogWarning(ex, "Concurrent approval attempt for alert: {AlertId}", alertId);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Concurrent Approval Attempt",
                Detail = ex.Message
            });
        }
        catch (DrasiUnavailableException ex)
        {
            _logger.LogError(ex, "Drasi unavailable during approval");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable",
                detail: ex.Message);
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

    /// <summary>
    /// Rejects an alert with a reason.
    /// </summary>
    /// <param name="alertId">Alert ID</param>
    /// <param name="dto">Rejection data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated alert with rejection record</returns>
    /// <response code="200">Alert rejected successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="403">User does not have approver role</response>
    /// <response code="404">Alert not found</response>
    /// <response code="409">Concurrent approval attempt detected</response>
    /// <response code="503">Drasi service unavailable</response>
    [HttpDelete]
    [ProducesResponseType(typeof(AlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AlertDto>> RejectAlert(
        Guid alertId,
        [FromBody] RejectAlertDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rejecting alert: {AlertId}", alertId);

        // Validate role
        await _authService.ValidateRoleAsync("approver", cancellationToken);

        // Get current user
        var user = await _authService.GetCurrentUserAsync(cancellationToken);

        // Get ETag from If-Match header
        var etag = Request.Headers.IfMatch.FirstOrDefault()?.Trim('"');

        var command = new RejectAlertCommand(alertId, user.UserId, dto.RejectionReason, etag);

        try
        {
            var result = await _alertService.RejectAlertAsync(command, cancellationToken);

            _logger.LogInformation("Alert rejected successfully: {AlertId}", alertId);

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
        catch (ConcurrentApprovalAttemptException ex)
        {
            _logger.LogWarning(ex, "Concurrent approval attempt for alert: {AlertId}", alertId);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Concurrent Approval Attempt",
                Detail = ex.Message
            });
        }
        catch (DrasiUnavailableException ex)
        {
            _logger.LogError(ex, "Drasi unavailable during rejection");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable",
                detail: ex.Message);
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
