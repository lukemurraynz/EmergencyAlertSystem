using EmergencyAlerts.Domain.Services;

namespace EmergencyAlerts.Infrastructure.Services;

/// <summary>
/// Production time provider using system clock.
/// </summary>
internal sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
