using System;
using EmergencyAlerts.Domain.Services;

namespace EmergencyAlerts.Domain.Entities;

/// <summary>
/// Records each delivery attempt for an alert to a recipient.
/// </summary>
public class DeliveryAttempt
{
    public Guid AttemptId { get; private set; }
    public Guid AlertId { get; private set; }
    public Guid RecipientId { get; private set; }
    public int AttemptNumber { get; private set; }
    public AttemptStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public string? AcsOperationId { get; private set; }
    public DateTime AttemptedAt { get; private set; }

    // Navigation properties
    public Alert? Alert { get; private set; }
    public Recipient? Recipient { get; private set; }

    // Private constructor for EF Core
    private DeliveryAttempt() { }

    /// <summary>
    /// Creates a new delivery attempt.
    /// </summary>
    public static DeliveryAttempt Create(
        Guid alertId,
        Guid recipientId,
        ITimeProvider timeProvider,
        IIdGenerator idGenerator,
        int attemptNumber = 1)
    {
        if (alertId == Guid.Empty)
            throw new ArgumentException("AlertId is required", nameof(alertId));
        if (recipientId == Guid.Empty)
            throw new ArgumentException("RecipientId is required", nameof(recipientId));
        if (attemptNumber < 1 || attemptNumber > 3)
            throw new ArgumentException("Attempt number must be between 1 and 3", nameof(attemptNumber));

        return new DeliveryAttempt
        {
            AttemptId = idGenerator.NewId(),
            AlertId = alertId,
            RecipientId = recipientId,
            AttemptNumber = attemptNumber,
            Status = AttemptStatus.Pending,
            AttemptedAt = timeProvider.UtcNow
        };
    }

    /// <summary>
    /// Marks the attempt as successful.
    /// </summary>
    public void MarkSuccess(string acsOperationId)
    {
        if (string.IsNullOrWhiteSpace(acsOperationId))
            throw new ArgumentException("ACS Operation ID is required for successful attempts", nameof(acsOperationId));

        Status = AttemptStatus.Success;
        AcsOperationId = acsOperationId;
        FailureReason = null;
    }

    /// <summary>
    /// Marks the attempt as failed with a reason.
    /// </summary>
    public void MarkFailure(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason is required", nameof(failureReason));

        Status = AttemptStatus.Failed;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Checks if this attempt can be retried.
    /// </summary>
    public bool CanRetry() => Status == AttemptStatus.Failed && AttemptNumber < 3;
}

/// <summary>
/// Delivery attempt status.
/// </summary>
public enum AttemptStatus
{
    Pending,
    Success,
    Failed
}
