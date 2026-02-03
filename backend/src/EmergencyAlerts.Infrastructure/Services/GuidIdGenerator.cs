using EmergencyAlerts.Domain.Services;

namespace EmergencyAlerts.Infrastructure.Services;

/// <summary>
/// Production ID generator using Guid.NewGuid().
/// </summary>
internal sealed class GuidIdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.NewGuid();
}
