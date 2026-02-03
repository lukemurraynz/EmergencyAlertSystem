using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmergencyAlerts.Api.Services;

public interface IDrasiReactionAuthenticator
{
    bool Validate(HttpRequest request, out string? reason);
}

public class DrasiReactionAuthenticator : IDrasiReactionAuthenticator
{
    private const string ReactionHeader = "X-Reaction-Token";
    private readonly string? _expectedToken;
    private readonly ILogger<DrasiReactionAuthenticator> _logger;
    private bool _missingTokenWarningLogged;

    public DrasiReactionAuthenticator(IConfiguration configuration, ILogger<DrasiReactionAuthenticator> logger)
    {
        _expectedToken = configuration["Drasi:ReactionAuthToken"];
        _logger = logger;
    }

    public bool Validate(HttpRequest request, out string? reason)
    {
        reason = null;

        if (string.IsNullOrWhiteSpace(_expectedToken))
        {
            if (!_missingTokenWarningLogged)
            {
                _logger.LogWarning("Drasi reaction auth token is not configured. Reaction endpoints are unprotected.");
                _missingTokenWarningLogged = true;
            }

            return true;
        }

        if (!request.Headers.TryGetValue(ReactionHeader, out var values) || values.Count == 0)
        {
            reason = "Missing reaction auth header";
            return false;
        }

        if (!string.Equals(values.First(), _expectedToken, StringComparison.Ordinal))
        {
            reason = "Invalid reaction auth token";
            return false;
        }

        return true;
    }
}
