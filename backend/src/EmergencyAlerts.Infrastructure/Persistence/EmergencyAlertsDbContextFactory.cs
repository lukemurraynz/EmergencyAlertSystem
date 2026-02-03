using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EmergencyAlerts.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EmergencyAlertsDbContext to support EF Core migrations.
/// This is only used by dotnet ef tools, not at runtime.
/// </summary>
public class EmergencyAlertsDbContextFactory : IDesignTimeDbContextFactory<EmergencyAlertsDbContext>
{
    public EmergencyAlertsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EmergencyAlertsDbContext>();

        // Use a dummy connection string for design-time only
        // The actual connection string comes from Azure App Configuration at runtime
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=postgres;Username=postgres;Password=dummy",
            npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();
            });

        return new EmergencyAlertsDbContext(optionsBuilder.Options);
    }
}
