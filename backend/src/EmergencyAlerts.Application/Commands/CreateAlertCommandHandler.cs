using EmergencyAlerts.Domain.Entities;
using EmergencyAlerts.Domain.Exceptions;
using EmergencyAlerts.Domain.Repositories;
using EmergencyAlerts.Domain.Services;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Application.Ports;
using NetTopologySuite.IO;

namespace EmergencyAlerts.Application.Commands;

/// <summary>
/// Handler for CreateAlertCommand.
/// </summary>
public class CreateAlertCommandHandler
{
    private readonly IAlertRepository _alertRepository;
    private readonly IAreaRepository _areaRepository;
    private readonly ITimeProvider _timeProvider;
    private readonly IIdGenerator _idGenerator;
    private readonly IDrasiHealthService _drasiHealthService;

    public CreateAlertCommandHandler(
        IAlertRepository alertRepository,
        IAreaRepository areaRepository,
        ITimeProvider timeProvider,
        IIdGenerator idGenerator,
        IDrasiHealthService drasiHealthService)
    {
        _alertRepository = alertRepository;
        _areaRepository = areaRepository;
        _timeProvider = timeProvider;
        _idGenerator = idGenerator;
        _drasiHealthService = drasiHealthService;
    }

    public async Task<CreateAlertResult> HandleAsync(CreateAlertCommand command, CancellationToken cancellationToken = default)
    {
        // FR-016: Fail-safe blocking - reject if Drasi is unavailable
        var drasiHealth = await _drasiHealthService.GetHealthStatusAsync(cancellationToken);
        if (!drasiHealth.IsHealthy)
        {
            throw new DrasiUnavailableException("Cannot create alert: Drasi is unavailable");
        }

        // Parse severity and channel type
        if (!Enum.TryParse<Severity>(command.Alert.Severity, ignoreCase: true, out var severity))
        {
            throw new ArgumentException($"Invalid severity: {command.Alert.Severity}", nameof(command.Alert.Severity));
        }

        if (!Enum.TryParse<ChannelType>(command.Alert.ChannelType, ignoreCase: true, out var channelType))
        {
            throw new ArgumentException($"Invalid channel type: {command.Alert.ChannelType}", nameof(command.Alert.ChannelType));
        }

        // Create alert entity
        var alert = Alert.Create(
            headline: command.Alert.Headline,
            description: command.Alert.Description,
            severity: severity,
            channelType: channelType,
            expiresAt: command.Alert.ExpiresAt,
            createdBy: command.UserId,
            timeProvider: _timeProvider,
            idGenerator: _idGenerator,
            languageCode: command.Alert.LanguageCode);

        // Parse and add geographic areas
        var geoJsonReader = new GeoJsonReader();
        var wktWriter = new NetTopologySuite.IO.WKTWriter();
        foreach (var areaDto in command.Alert.Areas)
        {
            try
            {
                var geometry = geoJsonReader.Read<NetTopologySuite.Geometries.Polygon>(areaDto.GeoJsonPolygon);
                var wkt = wktWriter.Write(geometry);

                var area = Area.Create(
                    alertId: alert.AlertId,
                    areaDescription: areaDto.AreaDescription,
                    areaPolygonWkt: wkt,
                    timeProvider: _timeProvider,
                    idGenerator: _idGenerator,
                    regionCode: areaDto.RegionCode);

                alert.AddArea(area);
                await _areaRepository.AddAsync(area, cancellationToken);
            }
            catch (Exception ex)
            {
                // Convert parsing errors to a domain-specific exception for proper ProblemDetails mapping
                throw new InvalidPolygonException("Invalid GeoJSON polygon", ex);
            }
        }

        // Persist alert
        await _alertRepository.AddAsync(alert, cancellationToken);
        await _alertRepository.SaveChangesAsync(cancellationToken);

        // Map to DTO
        var alertDto = MapToDto(alert);

        return new CreateAlertResult(alert.AlertId, alertDto);
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
                GeoJsonPolygon = ConvertWktToGeoJson(a.AreaPolygonWkt),
                RegionCode = a.RegionCode
            }).ToList()
        };
    }

    private static string ConvertWktToGeoJson(string wkt)
    {
        var wktReader = new NetTopologySuite.IO.WKTReader();
        var geometry = wktReader.Read(wkt);
        return new GeoJsonWriter().Write(geometry);
    }
}
