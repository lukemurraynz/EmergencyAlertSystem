using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using EmergencyAlerts.Domain.Entities;
using EmergencyAlerts.Domain.Repositories;
using EmergencyAlerts.Infrastructure.Persistence;

namespace EmergencyAlerts.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IAlertRepository.
/// </summary>
public class AlertRepository : IAlertRepository
{
    private readonly EmergencyAlertsDbContext _context;

    public AlertRepository(EmergencyAlertsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Alert?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        return await _context.Alerts
            .Include(a => a.Areas)
            .Include(a => a.ApprovalRecord)
            .FirstOrDefaultAsync(a => a.AlertId == alertId, cancellationToken);
    }

    public async Task<IEnumerable<Alert>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Alerts
            .Include(a => a.Areas)
            .Include(a => a.ApprovalRecord)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Alert>> GetByStatusAsync(AlertStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Alerts
            .Include(a => a.Areas)
            .Include(a => a.ApprovalRecord)
            .Where(a => a.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Alert>> GetPendingDeliveryAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Alerts
            .Include(a => a.Areas)
            .Where(a => a.Status == AlertStatus.Approved && a.DeliveryStatus == DeliveryStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Alert>> GetExpiredAlertsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Alerts
            .Where(a => a.ExpiresAt <= DateTime.UtcNow &&
                       (a.Status == AlertStatus.Approved || a.Status == AlertStatus.Delivered))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Guid>> GetAlertIdsByRegionAsync(string regionCode, CancellationToken cancellationToken = default)
    {
        var targetStatuses = new[]
        {
            AlertStatus.Approved,
            AlertStatus.PendingApproval,
            AlertStatus.Delivered,
        };

        return await _context.Areas
            .Where(area => area.RegionCode == regionCode)
            .Join(_context.Alerts, area => area.AlertId, alert => alert.AlertId, (area, alert) => alert)
            .Where(alert => targetStatuses.Contains(alert.Status))
            .Select(alert => alert.AlertId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        await _context.Alerts.AddAsync(alert, cancellationToken);
    }

    public Task UpdateAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        _context.Alerts.Update(alert);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        _context.Alerts.Remove(alert);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// EF Core implementation of IAreaRepository.
/// </summary>
public class AreaRepository : IAreaRepository
{
    private readonly EmergencyAlertsDbContext _context;

    public AreaRepository(EmergencyAlertsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Area?> GetByIdAsync(Guid areaId, CancellationToken cancellationToken = default)
    {
        return await _context.Areas.FindAsync(new object[] { areaId }, cancellationToken);
    }

    public async Task<IEnumerable<Area>> GetByAlertIdAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        return await _context.Areas
            .Where(a => a.AlertId == alertId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Area area, CancellationToken cancellationToken = default)
    {
        await _context.Areas.AddAsync(area, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// EF Core implementation of IApprovalRecordRepository.
/// </summary>
public class ApprovalRecordRepository : IApprovalRecordRepository
{
    private readonly EmergencyAlertsDbContext _context;

    public ApprovalRecordRepository(EmergencyAlertsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ApprovalRecord?> GetByAlertIdAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        return await _context.ApprovalRecords
            .FirstOrDefaultAsync(ar => ar.AlertId == alertId, cancellationToken);
    }

    public async Task AddAsync(ApprovalRecord approvalRecord, CancellationToken cancellationToken = default)
    {
        await _context.ApprovalRecords.AddAsync(approvalRecord, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// EF Core implementation of IDeliveryAttemptRepository.
/// </summary>
public class DeliveryAttemptRepository : IDeliveryAttemptRepository
{
    private readonly EmergencyAlertsDbContext _context;

    public DeliveryAttemptRepository(EmergencyAlertsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<DeliveryAttempt?> GetByIdAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryAttempts.FindAsync(new object[] { attemptId }, cancellationToken);
    }

    public async Task<IEnumerable<DeliveryAttempt>> GetByAlertIdAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryAttempts
            .Where(da => da.AlertId == alertId)
            .OrderBy(da => da.AttemptedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DeliveryAttempt>> GetPendingAttemptsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryAttempts
            .Where(da => da.Status == AttemptStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DeliveryAttempt>> GetAttemptsSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryAttempts
            .Where(da => da.AttemptedAt >= sinceUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DeliveryAttempt attempt, CancellationToken cancellationToken = default)
    {
        await _context.DeliveryAttempts.AddAsync(attempt, cancellationToken);
    }

    public Task UpdateAsync(DeliveryAttempt attempt, CancellationToken cancellationToken = default)
    {
        _context.DeliveryAttempts.Update(attempt);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// EF Core implementation of IRecipientRepository.
/// </summary>
public class RecipientRepository : IRecipientRepository
{
    private readonly EmergencyAlertsDbContext _context;

    public RecipientRepository(EmergencyAlertsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Recipient?> GetByIdAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        return await _context.Recipients.FindAsync(new object[] { recipientId }, cancellationToken);
    }

    public async Task<Recipient?> GetByEmailAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        return await _context.Recipients
            .FirstOrDefaultAsync(r => r.EmailAddress == emailAddress.ToLowerInvariant(), cancellationToken);
    }

    public async Task<IEnumerable<Recipient>> GetActiveRecipientsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Recipients
            .Where(r => r.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Recipient recipient, CancellationToken cancellationToken = default)
    {
        await _context.Recipients.AddAsync(recipient, cancellationToken);
    }

    public Task UpdateAsync(Recipient recipient, CancellationToken cancellationToken = default)
    {
        _context.Recipients.Update(recipient);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// EF Core implementation of ICorrelationEventRepository.
/// </summary>
public class CorrelationEventRepository : ICorrelationEventRepository
{
    private readonly EmergencyAlertsDbContext _context;

    public CorrelationEventRepository(EmergencyAlertsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CorrelationEvent?> GetByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.CorrelationEvents.FindAsync(new object[] { eventId }, cancellationToken);
    }

    public async Task<IEnumerable<CorrelationEvent>> GetActiveEventsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CorrelationEvents
            .Where(ce => ce.ResolvedAt == null)
            .OrderByDescending(ce => ce.DetectionTimestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CorrelationEvent>> GetByPatternTypeAsync(PatternType patternType, CancellationToken cancellationToken = default)
    {
        return await _context.CorrelationEvents
            .Where(ce => ce.PatternType == patternType)
            .OrderByDescending(ce => ce.DetectionTimestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(CorrelationEvent correlationEvent, CancellationToken cancellationToken = default)
    {
        await _context.CorrelationEvents.AddAsync(correlationEvent, cancellationToken);
    }

    public Task UpdateAsync(CorrelationEvent correlationEvent, CancellationToken cancellationToken = default)
    {
        _context.CorrelationEvents.Update(correlationEvent);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// EF Core implementation of IAdminBoundaryRepository.
/// </summary>
public class AdminBoundaryRepository : IAdminBoundaryRepository
{
    private readonly EmergencyAlertsDbContext _context;

    public AdminBoundaryRepository(EmergencyAlertsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AdminBoundary?> GetByRegionCodeAsync(string regionCode, CancellationToken cancellationToken = default)
    {
        return await _context.AdminBoundaries
            .FirstOrDefaultAsync(ab => ab.RegionCode == regionCode, cancellationToken);
    }

    public async Task<IEnumerable<AdminBoundary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AdminBoundaries.ToListAsync(cancellationToken);
    }
}
