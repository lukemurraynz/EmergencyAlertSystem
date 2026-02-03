using EmergencyAlerts.Domain.Exceptions;
using EmergencyAlerts.Domain.Repositories;
using EmergencyAlerts.Domain.Services;
using EmergencyAlerts.Application.Dtos;
using NetTopologySuite.IO;

namespace EmergencyAlerts.Application.Commands;

/// <summary>
/// Handler for CancelAlertCommand.
/// </summary>
public class CancelAlertCommandHandler
{
    private readonly IAlertRepository _alertRepository;
    private readonly ITimeProvider _timeProvider;

    public CancelAlertCommandHandler(
        IAlertRepository alertRepository,
        ITimeProvider timeProvider)
    {
        _alertRepository = alertRepository;
        _timeProvider = timeProvider;
    }

    public async Task<CancelAlertResult> HandleAsync(CancelAlertCommand command, CancellationToken cancellationToken = default)
    {
        // Retrieve alert
        var alert = await _alertRepository.GetByIdAsync(command.AlertId, cancellationToken)
            ?? throw new AlertNotFoundException(command.AlertId);

        // Cancel alert
        alert.Cancel(_timeProvider);

        // Persist changes
        await _alertRepository.UpdateAsync(alert, cancellationToken);
        await _alertRepository.SaveChangesAsync(cancellationToken);

        // Map to DTO
        var alertDto = new AlertDto
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
            }).ToList()
        };

        return new CancelAlertResult(alertDto);
    }

    private static string ConvertWktToGeoJson(string wkt)
    {
        var wktReader = new NetTopologySuite.IO.WKTReader();
        var geometry = wktReader.Read(wkt);
        return new GeoJsonWriter().Write(geometry);
    }
}
