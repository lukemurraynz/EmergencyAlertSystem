using System.Collections.Generic;
using EmergencyAlerts.Domain.Entities;

namespace EmergencyAlerts.Domain.Repositories;

/// <summary>
/// Repository interface for Alert aggregate.
/// </summary>
public interface IAlertRepository
{
    Task<Alert?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Alert>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Alert>> GetByStatusAsync(AlertStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<Alert>> GetPendingDeliveryAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Alert>> GetExpiredAlertsAsync(CancellationToken cancellationToken = default);
    Task<List<Guid>> GetAlertIdsByRegionAsync(string regionCode, CancellationToken cancellationToken = default);
    Task AddAsync(Alert alert, CancellationToken cancellationToken = default);
    Task UpdateAsync(Alert alert, CancellationToken cancellationToken = default);
    Task DeleteAsync(Alert alert, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for Area entity.
/// </summary>
public interface IAreaRepository
{
    Task<Area?> GetByIdAsync(Guid areaId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Area>> GetByAlertIdAsync(Guid alertId, CancellationToken cancellationToken = default);
    Task AddAsync(Area area, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for ApprovalRecord entity.
/// </summary>
public interface IApprovalRecordRepository
{
    Task<ApprovalRecord?> GetByAlertIdAsync(Guid alertId, CancellationToken cancellationToken = default);
    Task AddAsync(ApprovalRecord approvalRecord, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for DeliveryAttempt entity.
/// </summary>
public interface IDeliveryAttemptRepository
{
    Task<DeliveryAttempt?> GetByIdAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DeliveryAttempt>> GetByAlertIdAsync(Guid alertId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DeliveryAttempt>> GetPendingAttemptsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<DeliveryAttempt>> GetAttemptsSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task AddAsync(DeliveryAttempt attempt, CancellationToken cancellationToken = default);
    Task UpdateAsync(DeliveryAttempt attempt, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for Recipient entity.
/// </summary>
public interface IRecipientRepository
{
    Task<Recipient?> GetByIdAsync(Guid recipientId, CancellationToken cancellationToken = default);
    Task<Recipient?> GetByEmailAsync(string emailAddress, CancellationToken cancellationToken = default);
    Task<IEnumerable<Recipient>> GetActiveRecipientsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Recipient recipient, CancellationToken cancellationToken = default);
    Task UpdateAsync(Recipient recipient, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for CorrelationEvent entity.
/// </summary>
public interface ICorrelationEventRepository
{
    Task<CorrelationEvent?> GetByIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CorrelationEvent>> GetActiveEventsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<CorrelationEvent>> GetByPatternTypeAsync(PatternType patternType, CancellationToken cancellationToken = default);
    Task AddAsync(CorrelationEvent correlationEvent, CancellationToken cancellationToken = default);
    Task UpdateAsync(CorrelationEvent correlationEvent, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for AdminBoundary entity (read-only reference data).
/// </summary>
public interface IAdminBoundaryRepository
{
    Task<AdminBoundary?> GetByRegionCodeAsync(string regionCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<AdminBoundary>> GetAllAsync(CancellationToken cancellationToken = default);
}
