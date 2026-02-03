using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EmergencyAlerts.Domain.Entities;
using NetTopologySuite.Geometries;

namespace EmergencyAlerts.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Area entity with PostGIS geometry support.
/// </summary>
public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("areas", schema: "emergency_alerts");

        builder.HasKey(a => a.AreaId);
        builder.Property(a => a.AreaId)
            .HasColumnName("area_id")
            .ValueGeneratedNever();

        builder.Property(a => a.AlertId)
            .HasColumnName("alert_id")
            .IsRequired();

        builder.Property(a => a.AreaDescription)
            .HasColumnName("area_description")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.RegionCode)
            .HasColumnName("region_code")
            .HasMaxLength(20);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // PostGIS geometry column - map from WKT string to PostGIS geometry
        // Note: We'll handle WKT conversion in the repository layer
        builder.Property(a => a.AreaPolygonWkt)
            .HasColumnName("area_polygon_wkt")
            .IsRequired();

        // Relationships
        builder.HasOne(a => a.Alert)
            .WithMany(a => a.Areas)
            .HasForeignKey(a => a.AlertId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(a => a.AlertId);
        // Note: GiST index on polygon will be created via migration SQL
        // CREATE INDEX idx_area_polygon ON areas USING GIST (area_polygon);
    }
}
