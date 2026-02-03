using EmergencyAlerts.Domain.Entities;

namespace EmergencyAlerts.Application.Ports;

/// <summary>
/// Result of an email delivery operation.
/// </summary>
public record EmailDeliveryResult(bool Success, string? ErrorMessage = null, string? OperationId = null);

/// <summary>
/// Service for delivering alerts via email using Azure Communication Services.
/// </summary>
public interface IDeliveryService
{
    /// <summary>
    /// Delivers an alert to the configured test recipients via email.
    /// </summary>
    Task<EmailDeliveryResult> DeliverAlertAsync(Alert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configured test recipient email addresses.
    /// </summary>
    Task<List<string>> GetTestRecipientsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the test recipient list from Azure App Configuration.
    /// </summary>
    Task RefreshTestRecipientsAsync(CancellationToken cancellationToken = default);
}
