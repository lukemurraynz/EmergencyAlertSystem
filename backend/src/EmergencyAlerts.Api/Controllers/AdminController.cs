using EmergencyAlerts.Application.Ports;
using EmergencyAlerts.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace EmergencyAlerts.Api.Controllers;

/// <summary>
/// Administrative endpoints for configuration management and system operations.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;
    private readonly IConfigurationRefresherProvider? _refresherProvider;
    private readonly IFeatureFlagService? _featureFlagService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IDeliveryService deliveryService,
        ILogger<AdminController> logger,
        IConfigurationRefresherProvider? refresherProvider = null,
        IFeatureFlagService? featureFlagService = null)
    {
        _deliveryService = deliveryService;
        _logger = logger;
        _refresherProvider = refresherProvider;
        _featureFlagService = featureFlagService;
    }

    /// <summary>
    /// Manually refreshes configuration from Azure App Configuration.
    /// This forces an immediate refresh of all registered keys (Email:TestRecipients, etc.)
    /// without waiting for the 5-minute automatic refresh interval.
    /// </summary>
    /// <response code="200">Configuration refreshed successfully</response>
    /// <response code="503">App Configuration is not configured or unavailable</response>
    [HttpPost("config/refresh")]
    [ProducesResponseType(typeof(ConfigRefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RefreshConfiguration(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if App Configuration is configured
            if (_refresherProvider == null)
            {
                _logger.LogWarning("App Configuration refresh requested but provider is not configured");
                return StatusCode(503, new ProblemDetails
                {
                    Title = "App Configuration Not Configured",
                    Detail = "Azure App Configuration is not configured for this instance",
                    Status = 503
                });
            }

            // Get all refreshers and trigger refresh
            var refreshers = _refresherProvider.Refreshers;
            var refreshTasks = refreshers.Select(r => r.TryRefreshAsync(cancellationToken));
            var results = await Task.WhenAll(refreshTasks);

            // Refresh the delivery service cache
            await _deliveryService.RefreshTestRecipientsAsync(cancellationToken);

            var successCount = results.Count(r => r);

            _logger.LogInformation(
                "Manual configuration refresh completed. Refreshed {SuccessCount}/{TotalCount} refreshers",
                successCount,
                refreshers.Count());

            return Ok(new ConfigRefreshResponse
            {
                Success = true,
                RefreshedCount = successCount,
                TotalRefreshers = refreshers.Count(),
                Message = $"Successfully refreshed {successCount} configuration sources"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh configuration");
            return StatusCode(503, new ProblemDetails
            {
                Title = "Configuration Refresh Failed",
                Detail = ex.Message,
                Status = 503
            });
        }
    }

    /// <summary>
    /// Gets the currently configured test recipients for email delivery.
    /// </summary>
    /// <response code="200">Returns the current test recipients</response>
    [HttpGet("config/test-recipients")]
    [ProducesResponseType(typeof(TestRecipientsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTestRecipients(CancellationToken cancellationToken = default)
    {
        var recipients = await _deliveryService.GetTestRecipientsAsync(cancellationToken);

        return Ok(new TestRecipientsResponse
        {
            Recipients = recipients,
            Count = recipients.Count
        });
    }

    /// <summary>
    /// Gets the status of all feature flags.
    /// Feature flags control emergency response system capabilities and can be toggled
    /// in Azure App Configuration for operational control during incidents.
    /// </summary>
    /// <response code="200">Returns the status of all feature flags</response>
    /// <response code="503">Feature management is not configured</response>
    [HttpGet("features")]
    [ProducesResponseType(typeof(FeatureFlagsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetFeatureFlags(CancellationToken cancellationToken = default)
    {
        if (_featureFlagService == null)
        {
            _logger.LogWarning("Feature flags requested but feature management is not configured");
            return StatusCode(503, new ProblemDetails
            {
                Title = "Feature Management Not Configured",
                Detail = "Feature flags are not configured for this instance",
                Status = 503
            });
        }

        var features = await _featureFlagService.GetAllFeaturesAsync(cancellationToken);

        return Ok(new FeatureFlagsResponse
        {
            Features = features,
            Count = features.Count
        });
    }

    /// <summary>
    /// Checks if a specific feature is enabled.
    /// </summary>
    /// <param name="featureName">The name of the feature to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Returns the feature status</response>
    /// <response code="503">Feature management is not configured</response>
    [HttpGet("features/{featureName}")]
    [ProducesResponseType(typeof(FeatureStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetFeatureStatus(string featureName, CancellationToken cancellationToken = default)
    {
        if (_featureFlagService == null)
        {
            _logger.LogWarning("Feature flag check requested but feature management is not configured");
            return StatusCode(503, new ProblemDetails
            {
                Title = "Feature Management Not Configured",
                Detail = "Feature flags are not configured for this instance",
                Status = 503
            });
        }

        var isEnabled = await _featureFlagService.IsEnabledAsync(featureName, cancellationToken);

        return Ok(new FeatureStatusResponse
        {
            FeatureName = featureName,
            IsEnabled = isEnabled
        });
    }
}

/// <summary>
/// Response for configuration refresh operation.
/// </summary>
public record ConfigRefreshResponse
{
    public required bool Success { get; init; }
    public required int RefreshedCount { get; init; }
    public required int TotalRefreshers { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Response for test recipients query.
/// </summary>
public record TestRecipientsResponse
{
    public required List<string> Recipients { get; init; }
    public required int Count { get; init; }
}

/// <summary>
/// Response for feature flags query.
/// </summary>
public record FeatureFlagsResponse
{
    public required Dictionary<string, bool> Features { get; init; }
    public required int Count { get; init; }
}

/// <summary>
/// Response for individual feature status.
/// </summary>
public record FeatureStatusResponse
{
    public required string FeatureName { get; init; }
    public required bool IsEnabled { get; init; }
}
