using Microsoft.FeatureManagement;

namespace EmergencyAlerts.Api.Services;

/// <summary>
/// Service for checking feature flags configured in Azure App Configuration.
/// Provides centralized access to feature toggles for emergency response control.
/// </summary>
public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default);
    Task<Dictionary<string, bool>> GetAllFeaturesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of feature flag service using Microsoft.FeatureManagement.
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogger<FeatureFlagService> _logger;

    // Known feature flags for emergency alert system
    private static readonly string[] KnownFeatures = new[]
    {
        "EnableGeographicCorrelation",
        "EnableSLABreachDetection",
        "EnableApprovalTimeouts",
        "RequireDrasiHealthCheck",
        "EnableRegionalHotspotDetection",
        "EnableSeverityEscalation",
        "EnableExpiryWarnings",
        "EnableRateSpikeDetection"
    };

    public FeatureFlagService(
        IFeatureManager featureManager,
        ILogger<FeatureFlagService> logger)
    {
        _featureManager = featureManager;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a feature is enabled.
    /// </summary>
    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        try
        {
            var isEnabled = await _featureManager.IsEnabledAsync(featureName);
            _logger.LogDebug("Feature flag check: {FeatureName} = {IsEnabled}", featureName, isEnabled);
            return isEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check feature flag: {FeatureName}. Defaulting to false.", featureName);
            return false;
        }
    }

    /// <summary>
    /// Gets the status of all known feature flags.
    /// </summary>
    public async Task<Dictionary<string, bool>> GetAllFeaturesAsync(CancellationToken cancellationToken = default)
    {
        var features = new Dictionary<string, bool>();

        foreach (var feature in KnownFeatures)
        {
            features[feature] = await IsEnabledAsync(feature, cancellationToken);
        }

        return features;
    }
}
