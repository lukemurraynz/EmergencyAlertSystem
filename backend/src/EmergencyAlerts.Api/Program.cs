using EmergencyAlerts.Application.Commands;
using EmergencyAlerts.Application.Queries;
using EmergencyAlerts.Application.Services;
using EmergencyAlerts.Application.Ports;
using EmergencyAlerts.Api.Hubs;
using EmergencyAlerts.Api.Middleware;
using EmergencyAlerts.Api.ReactionHandlers;
using EmergencyAlerts.Api.Services;
using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure App Configuration with managed identity and automatic refresh
// Made optional to allow app to start even if App Config is unavailable
var appConfigEndpoint = builder.Configuration["AppConfiguration:Endpoint"];
var appConfigEnabled = false;
if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
{
    try
    {
        builder.Configuration.AddAzureAppConfiguration(options =>
        {
            // Connect using managed identity (no credentials needed)
            options.Connect(new Uri(appConfigEndpoint), new ManagedIdentityCredential())
                // Select all application configuration keys
                .Select("*")
                // Configure feature flags for emergency response control
                .UseFeatureFlags(featureFlags =>
                {
                    featureFlags.SetRefreshInterval(TimeSpan.FromMinutes(5));
                })
                // Configure automatic refresh for operational settings
                .ConfigureRefresh(refresh =>
                {
                    // Email delivery configuration
                    refresh.Register("Email:TestRecipients", refreshAll: false)
                        .SetRefreshInterval(TimeSpan.FromMinutes(5));
                    refresh.Register("Email:SenderAddress", refreshAll: false)
                        .SetRefreshInterval(TimeSpan.FromMinutes(5));
                    refresh.Register("Email:DeliveryMode", refreshAll: false)
                        .SetRefreshInterval(TimeSpan.FromMinutes(5));

                    // Rate limiting configuration
                    refresh.Register("RateLimit:*", refreshAll: true)
                        .SetRefreshInterval(TimeSpan.FromMinutes(5));

                    // Drasi health check configuration
                    refresh.Register("Drasi:*", refreshAll: true)
                        .SetRefreshInterval(TimeSpan.FromMinutes(5));

                    // CORS configuration
                    refresh.Register("Cors:AllowedOrigins", refreshAll: false)
                        .SetRefreshInterval(TimeSpan.FromMinutes(5));

                    // Auth configuration
                    refresh.Register("Auth:AllowAnonymous", refreshAll: false)
                        .SetRefreshInterval(TimeSpan.FromMinutes(5));
                })
                // Prevent startup from blocking indefinitely on App Config load
                .ConfigureStartupOptions(startupOptions =>
                {
                    startupOptions.Timeout = TimeSpan.FromSeconds(15);
                });
        });
        appConfigEnabled = true;
        Console.WriteLine($"✓ Connected to Azure App Configuration: {appConfigEndpoint}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Failed to connect to Azure App Configuration: {ex.Message}");
        Console.WriteLine("Application will continue with local configuration only.");
    }
}

// Register infrastructure services (time provider, id generator, repositories, etc.)
EmergencyAlerts.Infrastructure.DependencyInjection.ServiceCollectionExtensions.AddInfrastructureServices(builder.Services);

// Register application layer services
builder.Services.AddScoped<CreateAlertCommandHandler>();
builder.Services.AddScoped<ApproveAlertCommandHandler>();
builder.Services.AddScoped<CancelAlertCommandHandler>();
builder.Services.AddScoped<AlertQueryHandler>();
builder.Services.AddScoped<IAlertService, AlertService>();

// Register delivery service for email alerts
builder.Services.AddScoped<IDeliveryService, EmergencyAlerts.Infrastructure.Services.DeliveryService>();

// Add Azure App Configuration services for refresh
builder.Services.AddAzureAppConfiguration();

// Add feature management for dynamic feature control
builder.Services.AddFeatureManagement();

// Register feature flag service
builder.Services.AddScoped<IFeatureFlagService, FeatureFlagService>();

// Register auth service based on HttpContext
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();

// Register Drasi reaction handlers
builder.Services.AddScoped<DeliveryTriggerReactionHandler>();
builder.Services.AddScoped<SLABreachReactionHandler>();
builder.Services.AddScoped<ApprovalTimeoutReactionHandler>();
builder.Services.AddScoped<GeographicCorrelationReactionHandler>();
builder.Services.AddScoped<RegionalHotspotReactionHandler>();
builder.Services.AddScoped<SeverityEscalationReactionHandler>();
builder.Services.AddScoped<DuplicateSuppressionReactionHandler>();
builder.Services.AddScoped<AreaExpansionSuggestionReactionHandler>();
builder.Services.AddScoped<AllClearSuggestionReactionHandler>();
builder.Services.AddScoped<ExpiryWarningReactionHandler>();
builder.Services.AddScoped<RateSpikeDetectionReactionHandler>();
builder.Services.AddScoped<SLACountdownReactionHandler>();
builder.Services.AddScoped<DeliveryRetryStormReactionHandler>();
builder.Services.AddScoped<ApproverWorkloadReactionHandler>();
builder.Services.AddScoped<DeliverySuccessRateReactionHandler>();
builder.Services.AddSingleton<IDrasiReactionAuthenticator, DrasiReactionAuthenticator>();

// Add controllers
builder.Services.AddControllers();

// Add AutoMapper for DTO mappings
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Add health checks
builder.Services.AddHealthChecks();

// Add OpenAPI/Swagger
builder.Services.AddOpenApi();

// Add CORS for frontend (origins from App Configuration)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"] ??
            "http://localhost:5173,http://localhost:3000";
        var origins = allowedOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// CRITICAL: Middleware order matters!

// 1. Correlation ID (first - generates/propagates correlation IDs)
app.UseCorrelationId();

// 2. Request Logging (early - logs all requests with correlation ID)
app.UseRequestLogging();

// 3. Azure App Configuration refresh middleware (enables dynamic config updates)
// Only use if App Configuration was successfully connected
if (appConfigEnabled)
{
    app.UseAzureAppConfiguration();
}

// 4. Exception Middleware (early - catches all unhandled exceptions)
app.UseExceptionMiddleware();

// 5. API Versioning (validates ?api-version query param and adds version header)
app.UseApiVersioning();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

// 6. HTTPS Redirection (skip health probes to avoid redirect loops)
var enableHttpsRedirection = builder.Configuration.GetValue<bool>("HttpsRedirection:Enabled");
if (enableHttpsRedirection)
{
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"), appBuilder =>
    {
        appBuilder.UseHttpsRedirection();
    });
}

// 7. Routing
app.UseRouting();

// 8. CORS (after routing, before auth)
app.UseCors();

// 9. Entra ID Authorization (after routing, before rate limiting)
app.UseEntraIdAuthorization();

// 10. Rate Limiting (after auth - uses user identity)
app.UseRateLimiting();

// 11. Map controllers
app.MapControllers();

// 12. Map SignalR hub (under /api/ prefix so ingress routes it to backend)
app.MapHub<AlertHub>("/api/hubs/alerts");

// 13. Map health check endpoints
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow
        });
        await context.Response.WriteAsync(json);
    }
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false  // No checks for liveness - just return 200 if app is running
});

app.Run();
