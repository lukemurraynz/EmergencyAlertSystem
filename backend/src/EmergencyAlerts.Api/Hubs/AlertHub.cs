using Microsoft.AspNetCore.SignalR;

namespace EmergencyAlerts.Api.Hubs;

/// <summary>
/// SignalR hub for real-time alert updates to dashboard clients.
/// Provides push notifications for alert status changes, SLA breaches, 
/// approval timeouts, and correlation events.
/// </summary>
public class AlertHub : Hub<IAlertHubClient>
{
    private readonly ILogger<AlertHub> _logger;

    public AlertHub(ILogger<AlertHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to dashboard updates.
    /// Clients call this method after connecting to receive real-time updates.
    /// </summary>
    public async Task SubscribeToDashboard()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
        _logger.LogInformation("Client {ConnectionId} subscribed to dashboard updates", Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribe from dashboard updates.
    /// </summary>
    public async Task UnsubscribeFromDashboard()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "dashboard");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from dashboard updates", Context.ConnectionId);
    }

    /// <summary>
    /// Subscribe to updates for a specific alert.
    /// </summary>
    /// <param name="alertId">Alert ID to subscribe to</param>
    public async Task SubscribeToAlert(string alertId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"alert-{alertId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to alert {AlertId}", Context.ConnectionId, alertId);
    }

    /// <summary>
    /// Unsubscribe from a specific alert.
    /// </summary>
    /// <param name="alertId">Alert ID to unsubscribe from</param>
    public async Task UnsubscribeFromAlert(string alertId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"alert-{alertId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from alert {AlertId}", Context.ConnectionId, alertId);
    }

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        }

        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called when a client connects.
    /// </summary>
    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected", Context.ConnectionId);
        return base.OnConnectedAsync();
    }
}

/// <summary>
/// Strongly-typed hub context for programmatic SignalR broadcasts.
/// Use this interface to send messages from Drasi reaction handlers.
/// </summary>
public interface IAlertHubClient
{
    /// <summary>
    /// Notifies clients when an alert status changes.
    /// </summary>
    Task AlertStatusChanged(object data);

    /// <summary>
    /// Notifies clients of a new SLA breach.
    /// </summary>
    Task SLABreachDetected(object data);

    /// <summary>
    /// Notifies clients of an approval timeout.
    /// </summary>
    Task ApprovalTimeoutDetected(object data);

    /// <summary>
    /// Notifies clients of a new correlation event.
    /// </summary>
    Task CorrelationEventDetected(object data);

    /// <summary>
    /// Notifies clients when an alert is delivered.
    /// </summary>
    Task AlertDelivered(object data);

    /// <summary>
    /// Notifies clients of dashboard summary updates.
    /// </summary>
    Task DashboardSummaryUpdated(object data);

    /// <summary>
    /// Notifies clients of SLA countdown updates (alerts approaching breach).
    /// </summary>
    Task SLACountdownUpdate(object data);

    /// <summary>
    /// Notifies clients of a delivery retry storm (3+ failed delivery attempts).
    /// </summary>
    Task DeliveryRetryStormDetected(object data);

    /// <summary>
    /// Notifies clients of approver workload imbalance (5+ decisions in 1 hour).
    /// </summary>
    Task ApproverWorkloadAlert(object data);

    /// <summary>
    /// Notifies clients when delivery success rate degrades below threshold.
    /// </summary>
    Task DeliverySuccessRateDegraded(object data);
}
