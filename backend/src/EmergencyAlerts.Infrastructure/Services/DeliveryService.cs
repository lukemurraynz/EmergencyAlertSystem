using EmergencyAlerts.Application.Ports;
using EmergencyAlerts.Domain.Entities;
using Azure;
using Azure.Communication.Email;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmergencyAlerts.Infrastructure.Services;

/// <summary>
/// Service for delivering alerts via email using Azure Communication Services.
/// Test recipients are dynamically loaded from Azure App Configuration.
/// </summary>
public class DeliveryService : IDeliveryService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DeliveryService> _logger;
    private List<string> _cachedRecipients = new();
    private readonly object _cacheLock = new();

    public DeliveryService(
        IConfiguration configuration,
        ILogger<DeliveryService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Load initial recipients from configuration
        LoadRecipientsFromConfiguration();
    }

    /// <summary>
    /// Delivers an alert to the configured test recipients via email.
    /// </summary>
    public async Task<EmailDeliveryResult> DeliverAlertAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        try
        {
            var recipients = await GetTestRecipientsAsync(cancellationToken);

            if (recipients.Count == 0)
            {
                _logger.LogWarning("No test recipients configured for alert delivery. Alert ID: {AlertId}", alert.AlertId);
                return new EmailDeliveryResult(false, "No test recipients configured");
            }

            var deliveryMode = (_configuration["Email:DeliveryMode"] ?? "Acs").Trim();
            if (deliveryMode.Equals("LogOnly", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Log-only delivery for alert {AlertId} to {RecipientCount} recipients: {Recipients}",
                    alert.AlertId,
                    recipients.Count,
                    string.Join(", ", recipients));

                return new EmailDeliveryResult(
                    Success: true,
                    ErrorMessage: null,
                    OperationId: "log-only");
            }

            var emailClient = CreateEmailClient();
            if (emailClient is null)
            {
                return new EmailDeliveryResult(false, "ACS credentials are not configured");
            }

            var senderAddress = _configuration["Email:SenderAddress"];
            if (string.IsNullOrWhiteSpace(senderAddress))
            {
                _logger.LogError("Email:SenderAddress is not configured.");
                return new EmailDeliveryResult(false, "Email sender address is not configured");
            }

            var emailContent = BuildEmailContent(alert);
            var toRecipients = recipients.Select(address => new EmailAddress(address)).ToList();
            var emailRecipients = new EmailRecipients(toRecipients);
            var emailMessage = new EmailMessage(senderAddress, emailRecipients, emailContent);

            _logger.LogInformation(
                "Sending alert {AlertId} via ACS email to {RecipientCount} recipients",
                alert.AlertId,
                recipients.Count);

            var operation = await emailClient.SendAsync(
                WaitUntil.Completed,
                emailMessage,
                cancellationToken);

            return new EmailDeliveryResult(
                Success: true,
                ErrorMessage: null,
                OperationId: operation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver alert {AlertId}", alert.AlertId);
            return new EmailDeliveryResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Gets the configured test recipient email addresses.
    /// Returns cached values; call RefreshTestRecipientsAsync to update from App Configuration.
    /// </summary>
    public Task<List<string>> GetTestRecipientsAsync(CancellationToken cancellationToken = default)
    {
        lock (_cacheLock)
        {
            return Task.FromResult(new List<string>(_cachedRecipients));
        }
    }

    /// <summary>
    /// Refreshes the test recipient list from Azure App Configuration.
    /// This is called automatically every 5 minutes via App Configuration refresh middleware,
    /// and can be manually triggered via the admin endpoint.
    /// </summary>
    public Task RefreshTestRecipientsAsync(CancellationToken cancellationToken = default)
    {
        LoadRecipientsFromConfiguration();

        _logger.LogInformation(
            "Refreshed test recipients from configuration. Count: {Count}",
            _cachedRecipients.Count);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads recipients from configuration (IConfiguration automatically refreshes from App Configuration).
    /// </summary>
    private void LoadRecipientsFromConfiguration()
    {
        var recipientsString = _configuration["Email:TestRecipients"];

        if (string.IsNullOrWhiteSpace(recipientsString))
        {
            lock (_cacheLock)
            {
                _cachedRecipients = new List<string>();
            }
            _logger.LogWarning("Email:TestRecipients configuration is empty");
            return;
        }

        var recipients = recipientsString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .ToList();

        lock (_cacheLock)
        {
            _cachedRecipients = recipients;
        }

        _logger.LogDebug(
            "Loaded {Count} test recipients from configuration",
            recipients.Count);
    }

    private EmailClient? CreateEmailClient()
    {
        var endpoint = _configuration["AzureCommunicationServices:Endpoint"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            TokenCredential credential = new DefaultAzureCredential();
            return new EmailClient(new Uri(endpoint), credential);
        }

        _logger.LogError("AzureCommunicationServices:Endpoint must be configured.");
        return null;
    }

    private static EmailContent BuildEmailContent(Alert alert)
    {
        var subject = $"[{alert.Severity}] {alert.Headline}";
        var plainText = $"""
{alert.Headline}

{alert.Description}

Severity: {alert.Severity}
Alert ID: {alert.AlertId}
Expires (UTC): {alert.ExpiresAt:O}
""";

        return new EmailContent(subject)
        {
            PlainText = plainText
        };
    }
}
