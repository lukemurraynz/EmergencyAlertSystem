using System;

namespace EmergencyAlerts.Domain.Entities;

/// <summary>
/// Administrative boundary reference data for region code lookup.
/// </summary>
public class AdminBoundary
{
    public Guid BoundaryId { get; private set; }
    public string RegionCode { get; private set; } = string.Empty;
    public string RegionName { get; private set; } = string.Empty;
    public string BoundaryPolygonWkt { get; private set; } = string.Empty; // WKT for MultiPolygon
    public int AdminLevel { get; private set; }

    // Private constructor for EF Core
    private AdminBoundary() { }

    /// <summary>
    /// Creates a new administrative boundary.
    /// </summary>
    public static AdminBoundary Create(
        string regionCode,
        string regionName,
        string boundaryPolygonWkt,
        int adminLevel)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
            throw new ArgumentException("Region code is required", nameof(regionCode));
        if (regionCode.Length > 20)
            throw new ArgumentException("Region code cannot exceed 20 characters", nameof(regionCode));
        if (string.IsNullOrWhiteSpace(regionName))
            throw new ArgumentException("Region name is required", nameof(regionName));
        if (regionName.Length > 255)
            throw new ArgumentException("Region name cannot exceed 255 characters", nameof(regionName));
        if (string.IsNullOrWhiteSpace(boundaryPolygonWkt))
            throw new ArgumentException("Boundary polygon is required", nameof(boundaryPolygonWkt));
        if (adminLevel < 1)
            throw new ArgumentException("Admin level must be at least 1", nameof(adminLevel));

        return new AdminBoundary
        {
            BoundaryId = Guid.NewGuid(),
            RegionCode = regionCode,
            RegionName = regionName,
            BoundaryPolygonWkt = boundaryPolygonWkt,
            AdminLevel = adminLevel
        };
    }
}
