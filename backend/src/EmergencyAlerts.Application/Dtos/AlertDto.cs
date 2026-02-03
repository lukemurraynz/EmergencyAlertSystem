namespace EmergencyAlerts.Application.Dtos;

/// <summary>
/// DTO for alert response.
/// </summary>
public class AlertDto
{
    public Guid AlertId { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ChannelType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string DeliveryStatus { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public List<AreaDto> Areas { get; set; } = new();
    public ApprovalRecordDto? ApprovalRecord { get; set; }
}

/// <summary>
/// DTO for approval record.
/// </summary>
public class ApprovalRecordDto
{
    public Guid ApprovalId { get; set; }
    public string ApproverId { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime DecidedAt { get; set; }
}
