using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using EmergencyAlerts.Api.Dtos;
using EmergencyAlerts.Infrastructure.Services;

namespace EmergencyAlerts.Api.Controllers;

/// <summary>
/// Configuration endpoints for serving public (non-secret) settings to clients.
/// </summary>
[ApiController]
[Route("api/v1/config")]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IMapsTokenService _mapsTokenService;

    public ConfigController(IConfiguration configuration, IMapsTokenService mapsTokenService)
    {
        _configuration = configuration;
        _mapsTokenService = mapsTokenService;
    }

    /// <summary>
    /// Returns public Azure Maps configuration for browser-based map initialization.
    /// Only non-secret values (AAD IDs, tenant, account name) are exposed.
    /// </summary>
    /// <returns>Maps authentication and account configuration.</returns>
    [HttpGet("maps")]
    public IActionResult GetMapsConfig()
    {
        var authMode = _configuration["Maps:AuthMode"] ?? "sas";

        var config = new MapsConfigDto
        {
            AuthMode = authMode,
            AadClientId = _configuration["Maps:AadClientId"],
            AadAppId = _configuration["Maps:AadAppId"],
            AadTenantId = _configuration["Maps:AadTenantId"],
            AccountName = _configuration["Maps:AccountName"],
            EnableMapEditor = _configuration.GetValue<bool>("FeatureFlags:EnableMapEditor", defaultValue: true),
        };

        return Ok(config);
    }

    /// <summary>
    /// Issues a short-lived SAS token for anonymous Azure Maps access.
    /// No authentication required: backend uses managed identity to mint token.
    /// </summary>
    /// <returns>SAS token with expiration time.</returns>
    [HttpPost("maps/sas-token")]
    public async Task<IActionResult> GetMapsSasToken(CancellationToken cancellationToken)
    {
        var response = await _mapsTokenService.GetSasTokenAsync(cancellationToken);
        return Ok(response);
    }
}
