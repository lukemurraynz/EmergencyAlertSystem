using System;
using EmergencyAlerts.Domain.Services;

namespace EmergencyAlerts.Domain.Entities;

/// <summary>
/// Approval or rejection decision for an alert.
/// Only one approval record per alert (first-wins semantics).
/// </summary>
public class ApprovalRecord
{
    public Guid ApprovalId { get; private set; }
    public Guid AlertId { get; private set; }
    public string ApproverId { get; private set; } = string.Empty;
    public Decision Decision { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime DecidedAt { get; private set; }

    // Navigation property
    public Alert? Alert { get; private set; }

    // Private constructor for EF Core
    private ApprovalRecord() { }

    /// <summary>
    /// Creates an approval record with 'approved' decision.
    /// </summary>
    public static ApprovalRecord CreateApproval(Guid alertId, string approverId, ITimeProvider timeProvider, IIdGenerator idGenerator)
    {
        if (alertId == Guid.Empty)
            throw new ArgumentException("AlertId is required", nameof(alertId));
        if (string.IsNullOrWhiteSpace(approverId))
            throw new ArgumentException("ApproverId is required", nameof(approverId));

        return new ApprovalRecord
        {
            ApprovalId = idGenerator.NewId(),
            AlertId = alertId,
            ApproverId = approverId,
            Decision = Decision.Approved,
            RejectionReason = null,
            DecidedAt = timeProvider.UtcNow
        };
    }

    /// <summary>
    /// Creates an approval record with 'rejected' decision and reason.
    /// </summary>
    public static ApprovalRecord CreateRejection(Guid alertId, string approverId, string rejectionReason, ITimeProvider timeProvider, IIdGenerator idGenerator)
    {
        if (alertId == Guid.Empty)
            throw new ArgumentException("AlertId is required", nameof(alertId));
        if (string.IsNullOrWhiteSpace(approverId))
            throw new ArgumentException("ApproverId is required", nameof(approverId));
        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Rejection reason is required when decision is rejected", nameof(rejectionReason));

        return new ApprovalRecord
        {
            ApprovalId = idGenerator.NewId(),
            AlertId = alertId,
            ApproverId = approverId,
            Decision = Decision.Rejected,
            RejectionReason = rejectionReason,
            DecidedAt = timeProvider.UtcNow
        };
    }

    /// <summary>
    /// Checks if this is an approval (vs. rejection).
    /// </summary>
    public bool IsApproval() => Decision == Decision.Approved;

    /// <summary>
    /// Checks if this is a rejection (vs. approval).
    /// </summary>
    public bool IsRejection() => Decision == Decision.Rejected;
}

/// <summary>
/// Approval decision enumeration.
/// </summary>
public enum Decision
{
    Approved,
    Rejected
}

/// <summary>
/// Type alias for Decision enum (used in tests).
/// </summary>
public enum ApprovalDecision
{
    Approved,
    Rejected
}
