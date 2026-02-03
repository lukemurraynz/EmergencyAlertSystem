namespace EmergencyAlerts.Application.Dtos;

/// <summary>
/// DTO for dashboard summary statistics.
/// </summary>
public class DashboardSummaryDto
{
    public AlertCountsDto Counts { get; set; } = new();
    public int DeliveryFailures { get; set; }
    public double? DeliverySuccessRatePercent { get; set; }
    public int DeliveryAttemptsLastHour { get; set; }
    public int DeliverySuccessCountLastHour { get; set; }
    public int DeliveryFailureCountLastHour { get; set; }
    public List<SLABreachDto> SLABreaches { get; set; } = new();
    public List<ApprovalTimeoutDto> ApprovalTimeouts { get; set; } = new();
    public List<CorrelationEventDto> RecentCorrelations { get; set; } = new();
}

/// <summary>
/// DTO for alert counts by status.
/// </summary>
public class AlertCountsDto
{
    public int PendingApproval { get; set; }
    public int Approved { get; set; }
    public int Delivered { get; set; }
    public int Rejected { get; set; }
    public int Cancelled { get; set; }
    public int Expired { get; set; }
    public int Total { get; set; }
}

/// <summary>
/// DTO for SLA breach notification.
/// </summary>
public class SLABreachDto
{
    public Guid AlertId { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime DetectionTimestamp { get; set; }
    public int ElapsedSeconds { get; set; }
}

/// <summary>
/// DTO for approval timeout notification.
/// </summary>
public class ApprovalTimeoutDto
{
    public Guid AlertId { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ElapsedMinutes { get; set; }
}

/// <summary>
/// DTO for correlation event.
/// </summary>
public class CorrelationEventDto
{
    public Guid EventId { get; set; }
    public string PatternType { get; set; } = string.Empty;
    public List<Guid> AlertIds { get; set; } = new();
    public DateTime DetectionTimestamp { get; set; }
    public string? ClusterSeverity { get; set; }
    public string? RegionCode { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
