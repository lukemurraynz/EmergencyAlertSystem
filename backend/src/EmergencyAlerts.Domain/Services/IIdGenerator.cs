namespace EmergencyAlerts.Domain.Services;

/// <summary>
/// Abstraction for generating unique identifiers, enabling deterministic testing.
/// </summary>
public interface IIdGenerator
{
    /// <summary>
    /// Generates a new unique identifier.
    /// </summary>
    Guid NewId();
}
