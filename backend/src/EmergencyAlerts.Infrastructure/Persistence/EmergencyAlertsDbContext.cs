using Microsoft.EntityFrameworkCore;
using EmergencyAlerts.Domain.Entities;
using NetTopologySuite.Geometries;

namespace EmergencyAlerts.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for Emergency Alerts with PostGIS support.
/// </summary>
public class EmergencyAlertsDbContext : DbContext
{
    public DbSet<Alert> Alerts { get; set; } = null!;
    public DbSet<Area> Areas { get; set; } = null!;
    public DbSet<ApprovalRecord> ApprovalRecords { get; set; } = null!;
    public DbSet<DeliveryAttempt> DeliveryAttempts { get; set; } = null!;
    public DbSet<Recipient> Recipients { get; set; } = null!;
    public DbSet<CorrelationEvent> CorrelationEvents { get; set; } = null!;
    public DbSet<AdminBoundary> AdminBoundaries { get; set; } = null!;

    public EmergencyAlertsDbContext(DbContextOptions<EmergencyAlertsDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations from separate files
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmergencyAlertsDbContext).Assembly);

        // Enable PostGIS extension (this will be in migrations)
        // CREATE EXTENSION IF NOT EXISTS postgis;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Enable sensitive data logging in development (can be configured externally)
        // optionsBuilder.EnableSensitiveDataLogging();
        // optionsBuilder.EnableDetailedErrors();
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Automatically update UpdatedAt timestamps on entities before saving.
    /// </summary>
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is Alert alert)
            {
                alert.GetType().GetProperty("UpdatedAt")?.SetValue(alert, DateTime.UtcNow);
            }
        }
    }
}
