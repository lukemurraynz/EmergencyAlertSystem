using AutoMapper;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Domain.Entities;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace EmergencyAlerts.Api.Mapping;

/// <summary>
/// AutoMapper profile for mapping between domain entities and DTOs.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Alert → AlertDto
        CreateMap<Alert, AlertDto>()
            .ForMember(dest => dest.Severity, opt => opt.MapFrom(src => src.Severity.ToString()))
            .ForMember(dest => dest.ChannelType, opt => opt.MapFrom(src => src.ChannelType.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.DeliveryStatus, opt => opt.MapFrom(src => src.DeliveryStatus.ToString()))
            .ForMember(dest => dest.Areas, opt => opt.MapFrom(src => src.Areas));

        // Area → AreaDto (WKT to GeoJSON conversion)
        CreateMap<Area, AreaDto>()
            .ForMember(dest => dest.GeoJsonPolygon, opt => opt.MapFrom(src => ConvertWktToGeoJson(src.AreaPolygonWkt)));

        // ApprovalRecord → ApprovalRecordDto
        CreateMap<ApprovalRecord, ApprovalRecordDto>()
            .ForMember(dest => dest.Decision, opt => opt.MapFrom(src => src.Decision.ToString()));
    }

    /// <summary>
    /// Converts WKT polygon to GeoJSON string.
    /// </summary>
    private static string ConvertWktToGeoJson(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
        {
            return string.Empty;
        }

        try
        {
            var reader = new WKTReader();
            var geometry = reader.Read(wkt);

            var writer = new GeoJsonWriter();
            var geoJson = writer.Write(geometry);

            return geoJson;
        }
        catch (Exception)
        {
            // If conversion fails, return empty string
            // In production, consider logging this error
            return string.Empty;
        }
    }

    /// <summary>
    /// Converts GeoJSON string to WKT polygon.
    /// </summary>
    public static string ConvertGeoJsonToWkt(string geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
        {
            return string.Empty;
        }

        try
        {
            var reader = new GeoJsonReader();
            var geometry = reader.Read<Geometry>(geoJson);

            var writer = new WKTWriter();
            var wkt = writer.Write(geometry);

            return wkt;
        }
        catch (Exception)
        {
            // If conversion fails, return empty string
            // In production, consider logging this error
            return string.Empty;
        }
    }
}
