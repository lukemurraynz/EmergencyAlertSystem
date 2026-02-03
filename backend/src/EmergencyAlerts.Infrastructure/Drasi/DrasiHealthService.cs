using EmergencyAlerts.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmergencyAlerts.Infrastructure.Drasi;

/// <summary>
/// Implementation of Drasi health checking service.
/// Monitors the health of the Drasi continuous query engine.
/// Configuration values dynamically loaded from Azure App Configuration.
/// </summary>
public class DrasiHealthService : IDrasiHealthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DrasiHealthService> _logger;
    private readonly IConfiguration _configuration;
    private DrasiHealthStatus? _cachedStatus;
    private DateTime _lastCheckTime = DateTime.MinValue;

    public DrasiHealthService(
        IHttpClientFactory httpClientFactory,
        ILogger<DrasiHealthService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the current health status of Drasi.
    /// Implements caching to avoid overwhelming the Drasi health endpoint.
    /// Cache expiration and fail-open mode are configurable via App Configuration.
    /// </summary>
    public async Task<DrasiHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        // Read configuration values (refreshed automatically by App Configuration middleware)
        var failOpen = _configuration.GetValue<bool>("Drasi:FailOpen", false);
        var cacheSeconds = _configuration.GetValue<int>("Drasi:HealthCheckCacheSeconds", 5);
        var cacheExpiration = TimeSpan.FromSeconds(cacheSeconds);

        if (failOpen)
        {
            // In development/test, allow operations to continue when Drasi is unavailable
            var now = DateTime.UtcNow;
            _cachedStatus = new DrasiHealthStatus(IsHealthy: true, ErrorMessage: null, LastCheckTime: now);
            _lastCheckTime = now;
            return _cachedStatus;
        }

        // Return cached status if still valid
        if (_cachedStatus != null && DateTime.UtcNow - _lastCheckTime < cacheExpiration)
        {
            return _cachedStatus;
        }

        try
        {
            var endpoint = _configuration["Drasi:HealthEndpoint"] ?? "http://drasi-api:8080/health";
            var timeoutSeconds = _configuration.GetValue<int>("Drasi:HealthCheckTimeoutSeconds", 2);

            var httpClient = _httpClientFactory.CreateClient("DrasiHealthClient");
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            var response = await httpClient.GetAsync(endpoint, cancellationToken);

            var isHealthy = response.IsSuccessStatusCode;
            var now = DateTime.UtcNow;

            _cachedStatus = new DrasiHealthStatus(
                IsHealthy: isHealthy,
                ErrorMessage: isHealthy ? null : $"Drasi health check returned status code {response.StatusCode}",
                LastCheckTime: now);

            _lastCheckTime = now;

            if (!isHealthy)
            {
                _logger.LogWarning("Drasi health check failed with status code: {StatusCode}", response.StatusCode);
            }

            return _cachedStatus;
        }
        catch (HttpRequestException ex)
        {
            var endpoint = _configuration["Drasi:HealthEndpoint"] ?? "http://drasi-api:8080/health";
            _logger.LogError(ex, "Failed to connect to Drasi health endpoint: {Endpoint}", endpoint);

            var now = DateTime.UtcNow;
            _cachedStatus = new DrasiHealthStatus(
                IsHealthy: false,
                ErrorMessage: $"Failed to connect to Drasi: {ex.Message}",
                LastCheckTime: now);

            _lastCheckTime = now;

            return _cachedStatus;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Drasi health check timed out");

            var now = DateTime.UtcNow;
            _cachedStatus = new DrasiHealthStatus(
                IsHealthy: false,
                ErrorMessage: "Drasi health check timed out",
                LastCheckTime: now);

            _lastCheckTime = now;

            return _cachedStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Drasi health check");

            var now = DateTime.UtcNow;
            _cachedStatus = new DrasiHealthStatus(
                IsHealthy: false,
                ErrorMessage: $"Unexpected error: {ex.Message}",
                LastCheckTime: now);

            _lastCheckTime = now;

            return _cachedStatus;
        }
    }
}
