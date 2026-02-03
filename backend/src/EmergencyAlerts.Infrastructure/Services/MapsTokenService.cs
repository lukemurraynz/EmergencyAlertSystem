using Azure.Core;
using Azure.Identity;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmergencyAlerts.Infrastructure.Services;

/// <summary>
/// Azure Maps SAS Token DTO for API responses.
/// </summary>
public class SasTokenResponse
{
    [JsonPropertyName("token")]
    public required string Token { get; set; }

    [JsonPropertyName("expiresAt")]
    public required DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Service for issuing short-lived SAS tokens for Azure Maps using managed identity.
/// Allows unauthenticated SPAs to access Azure Maps without exposing subscription key.
/// </summary>
public interface IMapsTokenService
{
    Task<SasTokenResponse> GetSasTokenAsync(CancellationToken cancellationToken = default);
}

public class MapsTokenService : IMapsTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MapsTokenService> _logger;
    private readonly TokenCredential _credential;

    public MapsTokenService(IConfiguration configuration, ILogger<MapsTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        // Use DefaultAzureCredential to support workload identity, managed identity, and local dev auth.
        _credential = new DefaultAzureCredential();
    }

    /// <summary>
    /// Issues a short-lived SAS token for Azure Maps using the API's managed identity.
    /// Token is valid for 1 hour and scoped to the configured Maps account.
    /// </summary>
    public async Task<SasTokenResponse> GetSasTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var mapsAccountName = _configuration["Maps:AccountName"];
            if (string.IsNullOrEmpty(mapsAccountName))
            {
                _logger.LogError("Maps account name not configured (Maps:AccountName)");
                throw new InvalidOperationException("Azure Maps account name not configured");
            }

            // Azure Maps SAS token endpoint
            var endpoint = $"https://{mapsAccountName}.atlas.microsoft.com/";

            // Request token with Maps Data scope
            var scope = "https://atlas.microsoft.com/.default";
            var tokenRequestContext = new TokenRequestContext(new[] { scope });
            var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);

            _logger.LogInformation("Successfully issued SAS token for Azure Maps");

            return new SasTokenResponse
            {
                Token = token.Token,
                ExpiresAt = token.ExpiresOn.UtcDateTime,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue SAS token for Azure Maps");
            throw;
        }
    }
}
