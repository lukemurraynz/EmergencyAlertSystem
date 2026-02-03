using EmergencyAlerts.Application.Commands;
using EmergencyAlerts.Application.Queries;
using EmergencyAlerts.Application.Ports;

namespace EmergencyAlerts.Application.Services;

/// <summary>
/// Application service implementation for managing emergency alerts.
/// </summary>
public class AlertService : IAlertService
{
    private readonly CreateAlertCommandHandler _createHandler;
    private readonly ApproveAlertCommandHandler _approveHandler;
    private readonly CancelAlertCommandHandler _cancelHandler;
    private readonly AlertQueryHandler _queryHandler;

    public AlertService(
        CreateAlertCommandHandler createHandler,
        ApproveAlertCommandHandler approveHandler,
        CancelAlertCommandHandler cancelHandler,
        AlertQueryHandler queryHandler)
    {
        _createHandler = createHandler;
        _approveHandler = approveHandler;
        _cancelHandler = cancelHandler;
        _queryHandler = queryHandler;
    }

    public Task<CreateAlertResult> CreateAlertAsync(CreateAlertCommand command, CancellationToken cancellationToken = default)
    {
        return _createHandler.HandleAsync(command, cancellationToken);
    }

    public Task<ApproveAlertResult> ApproveAlertAsync(ApproveAlertCommand command, CancellationToken cancellationToken = default)
    {
        return _approveHandler.HandleAsync(command, cancellationToken);
    }

    public Task<RejectAlertResult> RejectAlertAsync(RejectAlertCommand command, CancellationToken cancellationToken = default)
    {
        return _approveHandler.HandleRejectAsync(command, cancellationToken);
    }

    public Task<CancelAlertResult> CancelAlertAsync(CancelAlertCommand command, CancellationToken cancellationToken = default)
    {
        return _cancelHandler.HandleAsync(command, cancellationToken);
    }

    public Task<GetAlertResult> GetAlertAsync(GetAlertQuery query, CancellationToken cancellationToken = default)
    {
        return _queryHandler.HandleAsync(query, cancellationToken);
    }

    public Task<ListAlertsResult> ListAlertsAsync(ListAlertsQuery query, CancellationToken cancellationToken = default)
    {
        return _queryHandler.HandleAsync(query, cancellationToken);
    }

    public Task<GetDashboardSummaryResult> GetDashboardSummaryAsync(GetDashboardSummaryQuery query, CancellationToken cancellationToken = default)
    {
        return _queryHandler.HandleAsync(query, cancellationToken);
    }
}
