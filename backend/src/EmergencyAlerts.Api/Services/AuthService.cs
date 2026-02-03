using System.Security.Claims;
using EmergencyAlerts.Application.Ports;

namespace EmergencyAlerts.Api.Services;

public class AuthService : IAuthService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<UserInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null || !(user.Identity?.IsAuthenticated ?? false))
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var email = user.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        var roles = user.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();

        return Task.FromResult(new UserInfo(userId, email, roles));
    }

    public Task<bool> HasRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null || !(user.Identity?.IsAuthenticated ?? false))
        {
            return Task.FromResult(false);
        }

        var hasRole = user.FindAll(ClaimTypes.Role).Any(r => string.Equals(r.Value, role, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(hasRole);
    }

    public async Task ValidateRoleAsync(string requiredRole, CancellationToken cancellationToken = default)
    {
        var hasRole = await HasRoleAsync(requiredRole, cancellationToken);
        if (!hasRole)
        {
            throw new UnauthorizedAccessException($"User does not have required role '{requiredRole}'.");
        }
    }
}
