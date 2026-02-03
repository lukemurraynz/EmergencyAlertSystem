namespace EmergencyAlerts.Application.Ports;

/// <summary>
/// User information from authentication context.
/// </summary>
public record UserInfo(string UserId, string Email, List<string> Roles);

/// <summary>
/// Service for authentication and authorization operations.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Gets the current user information from the authentication context.
    /// </summary>
    Task<UserInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current user has a specific role.
    /// </summary>
    Task<bool> HasRoleAsync(string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the current user has the required role, throwing if not authorized.
    /// </summary>
    Task ValidateRoleAsync(string requiredRole, CancellationToken cancellationToken = default);
}
