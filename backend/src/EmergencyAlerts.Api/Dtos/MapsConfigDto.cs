namespace EmergencyAlerts.Api.Dtos;

/// <summary>
/// Public Azure Maps configuration (no secrets).
/// Safe to expose to browser clients for map initialization.
/// </summary>
public class MapsConfigDto
{
    /// <summary>
    /// Authentication type: 'aad' for Azure AD, 'subscriptionKey' for key-based (not recommended for SPA).
    /// </summary>
    public required string AuthMode { get; set; }

    /// <summary>
    /// Azure AD Client ID for SPA map control authentication.
    /// </summary>
    public required string? AadClientId { get; set; }

    /// <summary>
    /// Azure AD App ID (also called resource ID) for maps service.
    /// </summary>
    public required string? AadAppId { get; set; }

    /// <summary>
    /// Azure AD Tenant ID.
    /// </summary>
    public required string? AadTenantId { get; set; }

    /// <summary>
    /// Azure Maps account name (for reference).
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// Whether feature flag for map editor is enabled.
    /// </summary>
    public bool EnableMapEditor { get; set; } = true;
}
