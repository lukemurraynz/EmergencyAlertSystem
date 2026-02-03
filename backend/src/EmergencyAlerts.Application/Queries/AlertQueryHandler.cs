using System.Text.Json;
using EmergencyAlerts.Domain.Entities;
using EmergencyAlerts.Domain.Repositories;
using EmergencyAlerts.Domain.Services;
using EmergencyAlerts.Application.Dtos;
using NetTopologySuite.IO;

namespace EmergencyAlerts.Application.Queries;

/// <summary>
/// Handler for alert queries.
/// </summary>
public class AlertQueryHandler
{
    private readonly IAlertRepository _alertRepository;
    private readonly ICorrelationEventRepository _correlationEventRepository;
    private readonly IDeliveryAttemptRepository _deliveryAttemptRepository;
    private readonly ITimeProvider _timeProvider;

    public AlertQueryHandler(
        IAlertRepository alertRepository,
        ICorrelationEventRepository correlationEventRepository,
        IDeliveryAttemptRepository deliveryAttemptRepository,
        ITimeProvider timeProvider)
    {
        _alertRepository = alertRepository;
        _correlationEventRepository = correlationEventRepository;
        _deliveryAttemptRepository = deliveryAttemptRepository;
        _timeProvider = timeProvider;
    }

    public async Task<GetAlertResult> HandleAsync(GetAlertQuery query, CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.GetByIdAsync(query.AlertId, cancellationToken);

        if (alert == null)
        {
            return new GetAlertResult(null);
        }

        var alertDto = MapToDto(alert);
        return new GetAlertResult(alertDto);
    }

    public async Task<ListAlertsResult> HandleAsync(ListAlertsQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<Alert> alerts;

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<AlertStatus>(query.Status, ignoreCase: true, out var status))
        {
            alerts = await _alertRepository.GetByStatusAsync(status, cancellationToken);
        }
        else
        {
            alerts = await _alertRepository.GetAllAsync(cancellationToken);
        }

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim().ToLowerInvariant();
            alerts = alerts.Where(a =>
                (a.Headline?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                a.AlertId.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            );
        }

        var alertDtos = alerts
            .OrderByDescending(a => a.CreatedAt)
            .Select(MapToDto)
            .ToList();
        var totalCount = alertDtos.Count;

        // Apply pagination
        var pagedAlerts = alertDtos
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new ListAlertsResult(pagedAlerts, totalCount, query.Page, query.PageSize);
    }

    public async Task<GetDashboardSummaryResult> HandleAsync(GetDashboardSummaryQuery query, CancellationToken cancellationToken = default)
    {
        var alerts = (await _alertRepository.GetAllAsync(cancellationToken)).ToList();
        var correlations = await _correlationEventRepository.GetActiveEventsAsync(cancellationToken);
        var resolvedStatuses = alerts.Select(ResolveStatus).ToList();
        var now = _timeProvider.UtcNow;

        // Calculate approval timeouts: alerts pending > 5 minutes
        var approvalTimeoutThreshold = TimeSpan.FromMinutes(5);
        var approvalTimeouts = alerts
            .Where(a => a.Status == AlertStatus.PendingApproval && !a.HasExpired(_timeProvider))
            .Where(a => (now - a.CreatedAt) > approvalTimeoutThreshold)
            .OrderByDescending(a => now - a.CreatedAt)
            .Take(10)
            .Select(a => new ApprovalTimeoutDto
            {
                AlertId = a.AlertId,
                Headline = a.Headline,
                Severity = a.Severity.ToString(),
                CreatedAt = a.CreatedAt,
                ElapsedMinutes = (int)(now - a.CreatedAt).TotalMinutes
            })
            .ToList();

        // Calculate SLA breaches: approved alerts not delivered within 60 seconds
        var slaBreachThreshold = TimeSpan.FromSeconds(60);
        var slaBreaches = alerts
            .Where(a => a.Status == AlertStatus.Approved && a.DeliveryStatus == DeliveryStatus.Pending)
            .Where(a => a.SentAt.HasValue && (now - a.SentAt.Value) > slaBreachThreshold)
            .OrderByDescending(a => a.SentAt.HasValue ? now - a.SentAt.Value : TimeSpan.Zero)
            .Take(10)
            .Select(a => new SLABreachDto
            {
                AlertId = a.AlertId,
                Headline = a.Headline,
                Severity = a.Severity.ToString(),
                DetectionTimestamp = now,
                ElapsedSeconds = a.SentAt.HasValue ? (int)(now - a.SentAt.Value).TotalSeconds : 0
            })
            .ToList();

        var attemptsWindowStart = now.AddHours(-1);
        var deliveryAttempts = (await _deliveryAttemptRepository.GetAttemptsSinceAsync(attemptsWindowStart, cancellationToken)).ToList();
        var deliveryAttemptsTotal = deliveryAttempts.Count;
        var deliveryAttemptsSuccess = deliveryAttempts.Count(a => a.Status == AttemptStatus.Success);
        var deliveryAttemptsFailed = deliveryAttempts.Count(a => a.Status == AttemptStatus.Failed);
        var deliverySuccessRate = deliveryAttemptsTotal > 0
            ? (double)deliveryAttemptsSuccess / deliveryAttemptsTotal * 100
            : (double?)null;

        var summary = new DashboardSummaryDto
        {
            Counts = new AlertCountsDto
            {
                PendingApproval = resolvedStatuses.Count(status => status == AlertStatus.PendingApproval),
                Approved = resolvedStatuses.Count(status => status == AlertStatus.Approved),
                Delivered = resolvedStatuses.Count(status => status == AlertStatus.Delivered),
                Rejected = resolvedStatuses.Count(status => status == AlertStatus.Rejected),
                Cancelled = resolvedStatuses.Count(status => status == AlertStatus.Cancelled),
                Expired = resolvedStatuses.Count(status => status == AlertStatus.Expired),
                Total = resolvedStatuses.Count
            },
            DeliveryFailures = alerts.Count(a => a.DeliveryStatus == DeliveryStatus.Failed),
            DeliverySuccessRatePercent = deliverySuccessRate,
            DeliveryAttemptsLastHour = deliveryAttemptsTotal,
            DeliverySuccessCountLastHour = deliveryAttemptsSuccess,
            DeliveryFailureCountLastHour = deliveryAttemptsFailed,
            RecentCorrelations = correlations.Take(10).Select(c => new CorrelationEventDto
            {
                EventId = c.EventId,
                PatternType = c.PatternType.ToString(),
                AlertIds = c.AlertIds.ToList(),
                DetectionTimestamp = c.DetectionTimestamp,
                ClusterSeverity = c.ClusterSeverity?.ToString(),
                RegionCode = c.RegionCode,
                Metadata = ParseJsonMetadata(c.Metadata)
            }).ToList(),
            SLABreaches = slaBreaches,
            ApprovalTimeouts = approvalTimeouts
        };

        return new GetDashboardSummaryResult(summary);
    }

    private AlertDto MapToDto(Alert alert)
    {
        var resolvedStatus = ResolveStatus(alert);
        return new AlertDto
        {
            AlertId = alert.AlertId,
            Headline = alert.Headline,
            Description = alert.Description,
            Severity = alert.Severity.ToString(),
            ChannelType = alert.ChannelType.ToString(),
            Status = resolvedStatus.ToString(),
            LanguageCode = alert.LanguageCode,
            DeliveryStatus = alert.DeliveryStatus.ToString(),
            SentAt = alert.SentAt,
            ExpiresAt = alert.ExpiresAt,
            CreatedAt = alert.CreatedAt,
            UpdatedAt = alert.UpdatedAt,
            CreatedBy = alert.CreatedBy,
            Areas = alert.Areas.Select(a => new AreaDto
            {
                AreaDescription = a.AreaDescription,
                GeoJsonPolygon = ConvertWktToGeoJson(a.AreaPolygonWkt),
                RegionCode = a.RegionCode
            }).ToList(),
            ApprovalRecord = alert.ApprovalRecord != null ? new ApprovalRecordDto
            {
                ApprovalId = alert.ApprovalRecord.ApprovalId,
                ApproverId = alert.ApprovalRecord.ApproverId,
                Decision = alert.ApprovalRecord.Decision.ToString(),
                RejectionReason = alert.ApprovalRecord.RejectionReason,
                DecidedAt = alert.ApprovalRecord.DecidedAt
            } : null
        };
    }

    private AlertStatus ResolveStatus(Alert alert)
    {
        if (alert.Status is AlertStatus.PendingApproval or AlertStatus.Approved or AlertStatus.Delivered
            && alert.HasExpired(_timeProvider))
        {
            return AlertStatus.Expired;
        }

        return alert.Status;
    }

    private static string ConvertWktToGeoJson(string wkt)
    {
        var wktReader = new NetTopologySuite.IO.WKTReader();
        var geometry = wktReader.Read(wkt);
        return new GeoJsonWriter().Write(geometry);
    }

    private static Dictionary<string, object>? ParseJsonMetadata(string? jsonMetadata)
    {
        if (string.IsNullOrWhiteSpace(jsonMetadata))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonMetadata);
        }
        catch
        {
            return null;
        }
    }
}
