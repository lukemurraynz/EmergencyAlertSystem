using System;

namespace EmergencyAlerts.Domain.Exceptions;

/// <summary>
/// Exception thrown when an alert is not found.
/// </summary>
public class AlertNotFoundException : Exception
{
    public Guid AlertId { get; }

    public AlertNotFoundException(Guid alertId)
        : base($"Alert with ID '{alertId}' was not found")
    {
        AlertId = alertId;
    }

    public AlertNotFoundException(Guid alertId, string message)
        : base(message)
    {
        AlertId = alertId;
    }

    public AlertNotFoundException(Guid alertId, string message, Exception innerException)
        : base(message, innerException)
    {
        AlertId = alertId;
    }
}

/// <summary>
/// Exception thrown when a polygon is invalid.
/// </summary>
public class InvalidPolygonException : Exception
{
    public string? PolygonWkt { get; }
    public string? ValidationError { get; }

    public InvalidPolygonException(string validationError)
        : base($"Invalid polygon: {validationError}")
    {
        ValidationError = validationError;
    }

    public InvalidPolygonException(string polygonWkt, string validationError)
        : base($"Invalid polygon: {validationError}")
    {
        PolygonWkt = polygonWkt;
        ValidationError = validationError;
    }

    public InvalidPolygonException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a concurrent approval attempt is detected.
/// </summary>
public class ConcurrentApprovalAttemptException : Exception
{
    public Guid AlertId { get; }
    public string ApproverId { get; }

    public ConcurrentApprovalAttemptException(Guid alertId, string approverId)
        : base($"Alert '{alertId}' has already been approved/rejected by another approver. First-wins conflict detected.")
    {
        AlertId = alertId;
        ApproverId = approverId;
    }

    public ConcurrentApprovalAttemptException(Guid alertId, string approverId, string message)
        : base(message)
    {
        AlertId = alertId;
        ApproverId = approverId;
    }

    public ConcurrentApprovalAttemptException(Guid alertId, string approverId, string message, Exception innerException)
        : base(message, innerException)
    {
        AlertId = alertId;
        ApproverId = approverId;
    }
}

/// <summary>
/// Exception thrown when Drasi is unavailable and fail-safe mode blocks operations.
/// </summary>
public class DrasiUnavailableException : Exception
{
    public string? QueryId { get; }

    public DrasiUnavailableException()
        : base("Drasi is currently unavailable. Alert creation and approval are blocked in fail-safe mode.")
    {
    }

    public DrasiUnavailableException(string queryId)
        : base($"Drasi query '{queryId}' is unavailable. Operations are blocked in fail-safe mode.")
    {
        QueryId = queryId;
    }

    public DrasiUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when alert status transition is invalid.
/// </summary>
public class InvalidAlertStatusTransitionException : Exception
{
    public Guid AlertId { get; }
    public string CurrentStatus { get; }
    public string AttemptedStatus { get; }

    public InvalidAlertStatusTransitionException(Guid alertId, string currentStatus, string attemptedStatus)
        : base($"Invalid status transition for alert '{alertId}': cannot transition from '{currentStatus}' to '{attemptedStatus}'")
    {
        AlertId = alertId;
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }
}

/// <summary>
/// Exception thrown when alert headline validation fails.
/// </summary>
public class InvalidHeadlineException : Exception
{
    public string? Headline { get; }

    public InvalidHeadlineException(string message)
        : base(message)
    {
    }

    public InvalidHeadlineException(string headline, string message)
        : base(message)
    {
        Headline = headline;
    }

    public InvalidHeadlineException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when alert description validation fails.
/// </summary>
public class InvalidDescriptionException : Exception
{
    public string? Description { get; }

    public InvalidDescriptionException(string message)
        : base(message)
    {
    }

    public InvalidDescriptionException(string description, string message)
        : base(message)
    {
        Description = description;
    }

    public InvalidDescriptionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
