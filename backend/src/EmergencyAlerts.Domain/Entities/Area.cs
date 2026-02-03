using System;
using EmergencyAlerts.Domain.Services;

namespace EmergencyAlerts.Domain.Entities;

/// <summary>
/// Geographic target zone for an alert.
/// An alert can have multiple areas.
/// </summary>
public class Area
{
    public Guid AreaId { get; private set; }
    public Guid AlertId { get; private set; }
    public string AreaDescription { get; private set; } = string.Empty;
    public string? RegionCode { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Geometry stored as WKT (Well-Known Text) for simplicity in domain model
    // EF Core will map this to PostGIS geometry type
    public string AreaPolygonWkt { get; private set; } = string.Empty;

    // Navigation property
    public Alert? Alert { get; private set; }

    // Private constructor for EF Core
    private Area() { }

    /// <summary>
    /// Creates a new geographic area for an alert.
    /// </summary>
    public static Area Create(
        Guid alertId,
        string areaDescription,
        string areaPolygonWkt,
        ITimeProvider timeProvider,
        IIdGenerator idGenerator,
        string? regionCode = null)
    {
        if (alertId == Guid.Empty)
            throw new ArgumentException("AlertId is required", nameof(alertId));
        if (string.IsNullOrWhiteSpace(areaDescription))
            throw new ArgumentException("Area description is required", nameof(areaDescription));
        if (areaDescription.Length > 255)
            throw new ArgumentException("Area description cannot exceed 255 characters", nameof(areaDescription));
        if (string.IsNullOrWhiteSpace(areaPolygonWkt))
            throw new ArgumentException("Area polygon is required", nameof(areaPolygonWkt));

        return new Area
        {
            AreaId = idGenerator.NewId(),
            AlertId = alertId,
            AreaDescription = areaDescription,
            AreaPolygonWkt = areaPolygonWkt,
            RegionCode = regionCode,
            CreatedAt = timeProvider.UtcNow
        };
    }

    /// <summary>
    /// Updates the region code (derived from centroid lookup).
    /// </summary>
    public void SetRegionCode(string regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
            throw new ArgumentException("Region code cannot be empty", nameof(regionCode));
        RegionCode = regionCode;
    }

    /// <summary>
    /// Validates PostGIS polygon constraints.
    /// This method stub is called by the domain service with actual PostGIS validation.
    /// </summary>
    public bool IsValidPolygon()
    {
        // Actual validation will be done via PostGIS in the infrastructure layer
        // This is a placeholder for domain-level logic if needed
        return !string.IsNullOrWhiteSpace(AreaPolygonWkt);
    }
}
