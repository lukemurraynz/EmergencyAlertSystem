using EmergencyAlerts.Application.Ports;
using EmergencyAlerts.Domain.Services;
using EmergencyAlerts.Domain.Repositories;
using EmergencyAlerts.Infrastructure.Drasi;
using EmergencyAlerts.Infrastructure.Persistence;
using EmergencyAlerts.Infrastructure.Persistence.Repositories;
using EmergencyAlerts.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmergencyAlerts.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Core infrastructure services
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();

        // Drasi integration services
        services.AddSingleton<IDrasiHealthService, DrasiHealthService>();
        services.AddSingleton<DrasiQueryValidator>();

        // HTTP client for Drasi health checks
        services.AddHttpClient("DrasiHealthClient");

        // Database configuration: Use InMemory if no connection string provided; otherwise use PostgreSQL + NetTopologySuite
        services.AddDbContext<EmergencyAlertsDbContext>((provider, options) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString("EmergencyAlertsDb");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Default to InMemory for local/dev tests when no connection string is provided
                options.UseInMemoryDatabase("EmergencyAlertsDb");
            }
            else
            {
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.UseNetTopologySuite();
                });

                // Recommended performance options
                options.EnableDetailedErrors();
            }
        });

        // Repository registrations
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAreaRepository, AreaRepository>();
        services.AddScoped<IApprovalRecordRepository, ApprovalRecordRepository>();
        services.AddScoped<IDeliveryAttemptRepository, DeliveryAttemptRepository>();
        services.AddScoped<IRecipientRepository, RecipientRepository>();
        services.AddScoped<ICorrelationEventRepository, CorrelationEventRepository>();
        services.AddScoped<IAdminBoundaryRepository, AdminBoundaryRepository>();

        // Azure Maps services for SAS token generation
        services.AddScoped<IMapsTokenService, MapsTokenService>();

        return services;
    }
}
