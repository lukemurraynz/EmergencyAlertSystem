using EmergencyAlerts.Application.Dtos;

namespace EmergencyAlerts.Application.Queries;

/// <summary>
/// Query to get an alert by ID.
/// </summary>
public record GetAlertQuery(Guid AlertId);

/// <summary>
/// Result of getting an alert.
/// </summary>
public record GetAlertResult(AlertDto? Alert);

/// <summary>
/// Query to list all alerts.
/// </summary>
public record ListAlertsQuery(string? Status = null, string? Search = null, int Page = 1, int PageSize = 50);

/// <summary>
/// Result of listing alerts.
/// </summary>
public record ListAlertsResult(List<AlertDto> Alerts, int TotalCount, int Page, int PageSize);

/// <summary>
/// Query to get dashboard summary.
/// </summary>
public record GetDashboardSummaryQuery();

/// <summary>
/// Result of getting dashboard summary.
/// </summary>
public record GetDashboardSummaryResult(DashboardSummaryDto Summary);
