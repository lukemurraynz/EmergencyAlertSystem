namespace EmergencyAlerts.Domain.Services;

/// <summary>
/// Abstraction for obtaining current time, enabling deterministic testing.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTime UtcNow { get; }
}
