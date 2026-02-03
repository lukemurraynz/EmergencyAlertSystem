using Microsoft.AspNetCore.Mvc;
using EmergencyAlerts.Application.Commands;
using EmergencyAlerts.Application.Queries;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Application.Ports;
using EmergencyAlerts.Domain.Exceptions;

namespace EmergencyAlerts.Api.Controllers;

/// <summary>
/// Controller for managing emergency alerts.
/// </summary>
[ApiController]
[Route("api/v1/alerts")]
[Produces("application/json")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly IAuthService _authService;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        IAlertService alertService,
        IAuthService authService,
        ILogger<AlertsController> logger)
    {
        _alertService = alertService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new emergency alert.
    /// </summary>
    /// <param name="dto">Alert creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created alert with HTTP 201</returns>
    /// <response code="201">Alert created successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="403">User does not have operator role</response>
    /// <response code="503">Drasi service unavailable</response>
    [HttpPost]
    [ProducesResponseType(typeof(AlertDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AlertDto>> CreateAlert(
        [FromBody] CreateAlertDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new alert with headline: {Headline}", dto.Headline);

        // Validate role
        await _authService.ValidateRoleAsync("operator", cancellationToken);

        // Get current user
        var user = await _authService.GetCurrentUserAsync(cancellationToken);

        // Create command
        var command = new CreateAlertCommand(dto, user.UserId);

        try
        {
            var result = await _alertService.CreateAlertAsync(command, cancellationToken);

            _logger.LogInformation("Alert created successfully: {AlertId}", result.AlertId);

            // Add ETag header for optimistic concurrency on create
            var typedHeaders = Response.GetTypedHeaders();
            typedHeaders.ETag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{result.Alert.UpdatedAt.Ticks}\"");
            Response.Headers["ETag"] = $"\"{result.Alert.UpdatedAt.Ticks}\"";

            return CreatedAtAction(
                nameof(GetAlert),
                new { id = result.AlertId },
                result.Alert);
        }
        catch (InvalidPolygonException ex)
        {
            _logger.LogError(ex, "Invalid GeoJSON polygon in create request");
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid Polygon",
                Detail = ex.Message
            });
        }
        catch (DrasiUnavailableException ex)
        {
            _logger.LogError(ex, "Drasi unavailable during alert creation");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable",
                detail: ex.Message);
        }
        catch (Exception ex)
        {
            // Fallback: map unexpected polygon parsing errors to 422 if identifiable
            if (ex is InvalidPolygonException || ex.InnerException is Newtonsoft.Json.JsonReaderException)
            {
                _logger.LogError(ex, "Invalid GeoJSON polygon in create request (fallback)");
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Invalid Polygon",
                    Detail = ex.Message
                });
            }

            throw;
        }
    }

    /// <summary>
    /// Gets an alert by ID.
    /// </summary>
    /// <param name="id">Alert ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Alert details or HTTP 404</returns>
    /// <response code="200">Alert found</response>
    /// <response code="404">Alert not found</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertDto>> GetAlert(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting alert: {AlertId}", id);

        var query = new GetAlertQuery(id);
        var result = await _alertService.GetAlertAsync(query, cancellationToken);

        if (result.Alert == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Alert Not Found",
                Detail = $"Alert with ID {id} was not found."
            });
        }

        // Add ETag header for optimistic concurrency
        var typedHeaders = Response.GetTypedHeaders();
        typedHeaders.ETag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{result.Alert.UpdatedAt.Ticks}\"");
        Response.Headers["ETag"] = $"\"{result.Alert.UpdatedAt.Ticks}\"";

        return Ok(result.Alert);
    }

    /// <summary>
    /// Lists all alerts with optional filtering and pagination.
    /// </summary>
    /// <param name="status">Optional status filter (e.g., 'approved')</param>
    /// <param name="search">Optional search term for headline, description, or alert ID</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 50, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of alerts</returns>
    /// <response code="200">List of alerts</response>
    [HttpGet]
    [ProducesResponseType(typeof(ListAlertsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListAlertsResult>> ListAlerts(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing alerts - Status: {Status}, Search: {Search}, Page: {Page}, PageSize: {PageSize}",
            status, search, page, pageSize);

        // Validate pagination
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 100) pageSize = 100;

        var query = new ListAlertsQuery(status, search, page, pageSize);
        var result = await _alertService.ListAlertsAsync(query, cancellationToken);

        // Add pagination headers
        Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
        Response.Headers.Append("X-Page", result.Page.ToString());
        Response.Headers.Append("X-Page-Size", result.PageSize.ToString());

        return Ok(result);
    }
}
