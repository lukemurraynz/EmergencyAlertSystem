using EmergencyAlerts.Application.Commands;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Application.Queries;

namespace EmergencyAlerts.Application.Ports;

/// <summary>
/// Application service for managing emergency alerts.
/// Orchestrates commands and queries across the domain.
/// </summary>
public interface IAlertService
{
    // Commands
    Task<CreateAlertResult> CreateAlertAsync(CreateAlertCommand command, CancellationToken cancellationToken = default);
    Task<ApproveAlertResult> ApproveAlertAsync(ApproveAlertCommand command, CancellationToken cancellationToken = default);
    Task<RejectAlertResult> RejectAlertAsync(RejectAlertCommand command, CancellationToken cancellationToken = default);
    Task<CancelAlertResult> CancelAlertAsync(CancelAlertCommand command, CancellationToken cancellationToken = default);

    // Queries
    Task<GetAlertResult> GetAlertAsync(GetAlertQuery query, CancellationToken cancellationToken = default);
    Task<ListAlertsResult> ListAlertsAsync(ListAlertsQuery query, CancellationToken cancellationToken = default);
    Task<GetDashboardSummaryResult> GetDashboardSummaryAsync(GetDashboardSummaryQuery query, CancellationToken cancellationToken = default);
}
