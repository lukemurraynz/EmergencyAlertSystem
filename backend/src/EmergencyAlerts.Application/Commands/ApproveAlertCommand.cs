using EmergencyAlerts.Application.Dtos;

namespace EmergencyAlerts.Application.Commands;

/// <summary>
/// Command to approve an alert.
/// </summary>
public record ApproveAlertCommand(Guid AlertId, string ApproverId, string? ETag);

/// <summary>
/// Result of approving an alert.
/// </summary>
public record ApproveAlertResult(AlertDto Alert);

/// <summary>
/// Command to reject an alert.
/// </summary>
public record RejectAlertCommand(Guid AlertId, string ApproverId, string RejectionReason, string? ETag);

/// <summary>
/// Result of rejecting an alert.
/// </summary>
public record RejectAlertResult(AlertDto Alert);

/// <summary>
/// Command to cancel an alert.
/// </summary>
public record CancelAlertCommand(Guid AlertId, string UserId);

/// <summary>
/// Result of cancelling an alert.
/// </summary>
public record CancelAlertResult(AlertDto Alert);
