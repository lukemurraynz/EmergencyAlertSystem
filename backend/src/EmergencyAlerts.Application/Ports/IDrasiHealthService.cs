namespace EmergencyAlerts.Application.Ports;

/// <summary>
/// Health status for Drasi continuous query engine.
/// </summary>
public record DrasiHealthStatus(bool IsHealthy, string? ErrorMessage = null, DateTime? LastCheckTime = null);

/// <summary>
/// Service for checking Drasi health status.
/// </summary>
public interface IDrasiHealthService
{
    /// <summary>
    /// Gets the current health status of the Drasi service.
    /// </summary>
    Task<DrasiHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default);
}
