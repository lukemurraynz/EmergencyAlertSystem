using EmergencyAlerts.Application.Dtos;

namespace EmergencyAlerts.Application.Commands;

/// <summary>
/// Command to create a new alert.
/// </summary>
public record CreateAlertCommand(CreateAlertDto Alert, string UserId);

/// <summary>
/// Result of creating an alert.
/// </summary>
public record CreateAlertResult(Guid AlertId, AlertDto Alert);
