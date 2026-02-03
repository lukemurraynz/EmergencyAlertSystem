using EmergencyAlerts.Domain.Entities;
using EmergencyAlerts.Domain.Exceptions;
using EmergencyAlerts.Domain.Repositories;
using EmergencyAlerts.Domain.Services;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Application.Ports;
using NetTopologySuite.IO;
using Microsoft.Extensions.Logging;

namespace EmergencyAlerts.Application.Commands;

/// <summary>
/// Handler for ApproveAlertCommand and RejectAlertCommand.
/// </summary>
public class ApproveAlertCommandHandler
{
    private readonly IAlertRepository _alertRepository;
    private readonly ITimeProvider _timeProvider;
    private readonly IIdGenerator _idGenerator;
    private readonly IDrasiHealthService _drasiHealthService;
    private readonly IDeliveryService _deliveryService;
    private readonly ILogger<ApproveAlertCommandHandler> _logger;

    public ApproveAlertCommandHandler(
        IAlertRepository alertRepository,
        ITimeProvider timeProvider,
        IIdGenerator idGenerator,
        IDrasiHealthService drasiHealthService,
        IDeliveryService deliveryService,
        ILogger<ApproveAlertCommandHandler> logger)
    {
        _alertRepository = alertRepository;
        _timeProvider = timeProvider;
        _idGenerator = idGenerator;
        _drasiHealthService = drasiHealthService;
        _deliveryService = deliveryService;
        _logger = logger;
    }

    public async Task<ApproveAlertResult> HandleAsync(ApproveAlertCommand command, CancellationToken cancellationToken = default)
    {
        // FR-016: Fail-safe blocking - reject if Drasi is unavailable
        var drasiHealth = await _drasiHealthService.GetHealthStatusAsync(cancellationToken);
        if (!drasiHealth.IsHealthy)
        {
            throw new DrasiUnavailableException("Cannot approve alert: Drasi is unavailable");
        }

        // Retrieve alert
        var alert = await _alertRepository.GetByIdAsync(command.AlertId, cancellationToken)
            ?? throw new AlertNotFoundException(command.AlertId);

        if (alert.Status == AlertStatus.PendingApproval && alert.HasExpired(_timeProvider))
        {
            throw new InvalidOperationException($"Alert expired at {alert.ExpiresAt:u} and can no longer be approved.");
        }

        // First-wins concurrency check (optimistic locking via ETag)
        if (alert.ApprovalRecord != null)
        {
            throw new ConcurrentApprovalAttemptException(command.AlertId, "Alert has already been approved or rejected");
        }

        // Approve alert
        alert.Approve(command.ApproverId, _timeProvider, _idGenerator);

        // Persist approval
        await _alertRepository.UpdateAsync(alert, cancellationToken);
        await _alertRepository.SaveChangesAsync(cancellationToken);

        if (alert.IsDeliverable(_timeProvider))
        {
            var deliveryResult = await _deliveryService.DeliverAlertAsync(alert, cancellationToken);

            if (deliveryResult.Success)
            {
                alert.UpdateDeliveryStatus(DeliveryStatus.Delivered, _timeProvider);
            }
            else
            {
                alert.UpdateDeliveryStatus(DeliveryStatus.Failed, _timeProvider);
                _logger.LogWarning(
                    "Alert delivery failed for {AlertId}. Error: {ErrorMessage}",
                    alert.AlertId,
                    deliveryResult.ErrorMessage ?? "Unknown error");
            }

            await _alertRepository.UpdateAsync(alert, cancellationToken);
            await _alertRepository.SaveChangesAsync(cancellationToken);
        }

        // Map to DTO
        var alertDto = MapToDto(alert);

        return new ApproveAlertResult(alertDto);
    }

    public async Task<RejectAlertResult> HandleRejectAsync(RejectAlertCommand command, CancellationToken cancellationToken = default)
    {
        // FR-016: Fail-safe blocking - reject if Drasi is unavailable
        var drasiHealth = await _drasiHealthService.GetHealthStatusAsync(cancellationToken);
        if (!drasiHealth.IsHealthy)
        {
            throw new DrasiUnavailableException("Cannot reject alert: Drasi is unavailable");
        }

        // Retrieve alert
        var alert = await _alertRepository.GetByIdAsync(command.AlertId, cancellationToken)
            ?? throw new AlertNotFoundException(command.AlertId);

        if (alert.Status == AlertStatus.PendingApproval && alert.HasExpired(_timeProvider))
        {
            throw new InvalidOperationException($"Alert expired at {alert.ExpiresAt:u} and can no longer be rejected.");
        }

        // First-wins concurrency check
        if (alert.ApprovalRecord != null)
        {
            throw new ConcurrentApprovalAttemptException(command.AlertId, "Alert has already been approved or rejected");
        }

        // Reject alert
        alert.Reject(command.ApproverId, command.RejectionReason, _timeProvider, _idGenerator);

        // Persist changes
        await _alertRepository.UpdateAsync(alert, cancellationToken);
        await _alertRepository.SaveChangesAsync(cancellationToken);

        // Map to DTO
        var alertDto = MapToDto(alert);

        return new RejectAlertResult(alertDto);
    }

    private static AlertDto MapToDto(Alert alert)
    {
        return new AlertDto
        {
            AlertId = alert.AlertId,
            Headline = alert.Headline,
            Description = alert.Description,
            Severity = alert.Severity.ToString(),
            ChannelType = alert.ChannelType.ToString(),
            Status = alert.Status.ToString(),
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
                GeoJsonPolygon = ConvertWktToGeoJson(a.AreaPolygonWkt)
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

    private static string ConvertWktToGeoJson(string wkt)
    {
        var wktReader = new NetTopologySuite.IO.WKTReader();
        var geometry = wktReader.Read(wkt);
        return new GeoJsonWriter().Write(geometry);
    }
}
