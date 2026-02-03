using System;
using EmergencyAlerts.Domain.Services;

namespace EmergencyAlerts.Domain.Entities;

/// <summary>
/// Delivery target for alerts (email address for MVP).
/// </summary>
public class Recipient
{
    public Guid RecipientId { get; private set; }
    public string EmailAddress { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    // Private constructor for EF Core
    private Recipient() { }

    /// <summary>
    /// Creates a new recipient.
    /// </summary>
    public static Recipient Create(string emailAddress, ITimeProvider timeProvider, IIdGenerator idGenerator, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
            throw new ArgumentException("Email address is required", nameof(emailAddress));
        if (!IsValidEmail(emailAddress))
            throw new ArgumentException("Invalid email format", nameof(emailAddress));

        return new Recipient
        {
            RecipientId = idGenerator.NewId(),
            EmailAddress = emailAddress.ToLowerInvariant(),
            DisplayName = displayName,
            IsActive = true,
            CreatedAt = timeProvider.UtcNow
        };
    }

    /// <summary>
    /// Activates the recipient.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivates the recipient.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Updates the display name.
    /// </summary>
    public void UpdateDisplayName(string displayName)
    {
        DisplayName = displayName;
    }

    /// <summary>
    /// Basic email validation.
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        return !string.IsNullOrWhiteSpace(email)
            && email.Contains('@')
            && email.Length <= 255;
    }
}
