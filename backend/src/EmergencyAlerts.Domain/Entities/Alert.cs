using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyAlerts.Domain.Exceptions;
using EmergencyAlerts.Domain.Services;

namespace EmergencyAlerts.Domain.Entities;

/// <summary>
/// Alert aggregate root representing an emergency notification.
/// </summary>
public class Alert
{
    private const int MaxHeadlineLength = 100;
    private const int MaxDescriptionLength = 1395;
    public Guid AlertId { get; private set; }
    public string Headline { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Severity Severity { get; private set; }
    public ChannelType ChannelType { get; private set; }
    public AlertStatus Status { get; private set; }
    public string LanguageCode { get; private set; } = "en-GB";
    public DeliveryStatus DeliveryStatus { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;

    // Navigation properties
    private readonly List<Area> _areas = new();
    public IReadOnlyCollection<Area> Areas => _areas.AsReadOnly();
    public ApprovalRecord? ApprovalRecord { get; private set; }

    // Private constructor for EF Core
    private Alert() { }

    /// <summary>
    /// Creates a new alert in 'pending_approval' status.
    /// </summary>
    public static Alert Create(
        string headline,
        string description,
        Severity severity,
        ChannelType channelType,
        DateTime expiresAt,
        string createdBy,
        ITimeProvider timeProvider,
        IIdGenerator idGenerator,
        string languageCode = "en-GB")
    {
        // Validation
        if (string.IsNullOrWhiteSpace(headline))
            throw new InvalidHeadlineException("Headline is required");
        if (headline.Length > MaxHeadlineLength)
            throw new InvalidHeadlineException($"Headline cannot exceed {MaxHeadlineLength} characters");
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidDescriptionException("Description is required");
        if (description.Length > MaxDescriptionLength)
            throw new InvalidDescriptionException($"Description cannot exceed {MaxDescriptionLength} characters");
        if (expiresAt <= timeProvider.UtcNow)
            throw new ArgumentException("Expiry time must be in the future", nameof(expiresAt));
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy is required", nameof(createdBy));

        var now = timeProvider.UtcNow;
        return new Alert
        {
            AlertId = idGenerator.NewId(),
            Headline = headline,
            Description = description,
            Severity = severity,
            ChannelType = channelType,
            Status = AlertStatus.PendingApproval,
            LanguageCode = languageCode,
            DeliveryStatus = DeliveryStatus.Pending,
            ExpiresAt = expiresAt,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Adds a geographic area to the alert.
    /// </summary>
    public void AddArea(Area area)
    {
        if (area == null)
            throw new ArgumentNullException(nameof(area));
        _areas.Add(area);
    }

    /// <summary>
    /// Checks if the alert can be approved.
    /// </summary>
    public bool CanApprove(ITimeProvider timeProvider)
    {
        return Status == AlertStatus.PendingApproval && !HasExpired(timeProvider);
    }

    /// <summary>
    /// Approves the alert, transitioning it to 'approved' status.
    /// </summary>
    public void Approve(string approverId, ITimeProvider timeProvider, IIdGenerator idGenerator)
    {
        if (!CanApprove(timeProvider))
            throw new InvalidOperationException($"Alert cannot be approved from status {Status}");

        var now = timeProvider.UtcNow;
        Status = AlertStatus.Approved;
        SentAt = now;
        UpdatedAt = now;

        ApprovalRecord = ApprovalRecord.CreateApproval(AlertId, approverId, timeProvider, idGenerator);
    }

    /// <summary>
    /// Rejects the alert with a reason.
    /// </summary>
    public void Reject(string approverId, string rejectionReason, ITimeProvider timeProvider, IIdGenerator idGenerator)
    {
        if (!CanApprove(timeProvider))
            throw new InvalidOperationException($"Alert cannot be rejected from status {Status}");
        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Rejection reason is required", nameof(rejectionReason));

        Status = AlertStatus.Rejected;
        UpdatedAt = timeProvider.UtcNow;

        ApprovalRecord = ApprovalRecord.CreateRejection(AlertId, approverId, rejectionReason, timeProvider, idGenerator);
    }

    /// <summary>
    /// Checks if the alert can be cancelled.
    /// </summary>
    public bool CanCancel()
    {
        return Status is AlertStatus.Approved or AlertStatus.Delivered;
    }

    /// <summary>
    /// Cancels an active alert.
    /// </summary>
    public void Cancel(ITimeProvider timeProvider)
    {
        if (!CanCancel())
            throw new InvalidOperationException($"Alert cannot be cancelled from status {Status}");

        Status = AlertStatus.Cancelled;
        UpdatedAt = timeProvider.UtcNow;
    }

    /// <summary>
    /// Checks if the alert has expired.
    /// </summary>
    public bool HasExpired(ITimeProvider timeProvider)
    {
        return timeProvider.UtcNow > ExpiresAt;
    }

    /// <summary>
    /// Checks if the alert is deliverable (approved and not expired).
    /// </summary>
    public bool IsDeliverable(ITimeProvider timeProvider)
    {
        return Status == AlertStatus.Approved
            && DeliveryStatus == DeliveryStatus.Pending
            && !HasExpired(timeProvider);
    }

    /// <summary>
    /// Updates the delivery status.
    /// </summary>
    public void UpdateDeliveryStatus(DeliveryStatus deliveryStatus, ITimeProvider timeProvider)
    {
        DeliveryStatus = deliveryStatus;
        UpdatedAt = timeProvider.UtcNow;

        if (deliveryStatus == DeliveryStatus.Delivered)
        {
            Status = AlertStatus.Delivered;
        }
    }

    /// <summary>
    /// Marks the alert as expired if past expiry time.
    /// </summary>
    public void MarkExpiredIfNeeded(ITimeProvider timeProvider)
    {
        if (HasExpired(timeProvider) && Status is AlertStatus.Approved or AlertStatus.Delivered)
        {
            Status = AlertStatus.Expired;
            UpdatedAt = timeProvider.UtcNow;
        }
    }
}

/// <summary>
/// CAP severity levels for emergency alerts.
/// </summary>
public enum Severity
{
    Unknown,
    Minor,
    Moderate,
    Severe,
    Extreme
}

/// <summary>
/// Alert distribution channel types.
/// </summary>
public enum ChannelType
{
    Test,
    Operator,
    Severe,
    Government,
    Sms
}

/// <summary>
/// Alert lifecycle status.
/// </summary>
public enum AlertStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Delivered,
    Cancelled,
    Expired
}

/// <summary>
/// Delivery status for an alert.
/// </summary>
public enum DeliveryStatus
{
    Pending,
    Delivered,
    Failed
}

/// <summary>
/// Type alias for ChannelType (used in tests).
/// </summary>
public enum Channel
{
    Test,
    Operator,
    Severe,
    Government,
    Sms,
    Email
}
