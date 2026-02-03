using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using EmergencyAlerts.Infrastructure.Persistence;
using EmergencyAlerts.Application.Ports;
using EmergencyAlerts.Domain.Services;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Net.Http.Headers;

namespace EmergencyAlerts.Api.Tests;

/// <summary>
/// Base test fixture for API integration tests.
/// Provides WebApplicationFactory for in-memory API hosting with shared InMemory DB per fixture.
/// </summary>
public class ApiTestFixture : IDisposable
{
    private static int _fixtureCounter = 0;
    private readonly string _databaseName;

    public WebApplicationFactory<Program> Factory { get; }
    public HttpClient Client { get; }

    public ApiTestFixture()
    {
        // Set environment to Test before creating the factory
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");

        // Generate unique database name per fixture instance to isolate test classes
        _databaseName = $"TestDb_{Interlocked.Increment(ref _fixtureCounter)}_{Guid.NewGuid()}";

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Drasi:FailOpen"] = "true"
                        });
                    })
                    .ConfigureServices(services =>
                    {
                        // Remove existing DbContext registration
                        var descriptor = services.SingleOrDefault(
                            d => d.ServiceType == typeof(DbContextOptions<EmergencyAlertsDbContext>));
                        if (descriptor != null)
                        {
                            services.Remove(descriptor);
                        }

                        // Register InMemory DbContext with consistent database name for this fixture
                        services.AddDbContext<EmergencyAlertsDbContext>(options =>
                        {
                            options.UseInMemoryDatabase(_databaseName);
                            options.EnableSensitiveDataLogging();
                        });

                        services.AddSingleton<IDrasiHealthService>(new TestDrasiHealthService());
                    });
            });

        Client = Factory.CreateClient();
    }

    /// <summary>
    /// Creates an HTTP client with authentication headers for testing.
    /// Uses unique user ID per client to avoid rate limit collisions across tests.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string userId = "", string role = "operator")
    {
        // Generate unique user ID or append uniqueness to provided ID to avoid rate limit interference
        if (string.IsNullOrEmpty(userId))
        {
            userId = $"test-user-{Guid.NewGuid()}";
        }
        else
        {
            // Append unique suffix to provided user ID to ensure rate limit isolation
            userId = $"{userId}-{Guid.NewGuid().ToString().Substring(0, 8)}";
        }

        var client = Factory.CreateClient();

        // Mock Entra ID claims via custom header (AuthorizationMiddleware mock mode)
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-User-Role", role);

        // Bypass rate limiting in tests
        client.DefaultRequestHeaders.Add("X-Bypass-RateLimit", "true");

        return client;
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    private sealed class TestDrasiHealthService : IDrasiHealthService
    {
        public Task<DrasiHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DrasiHealthStatus(true, null, DateTime.UtcNow));
        }
    }
}
