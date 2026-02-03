using System.ComponentModel.DataAnnotations;

namespace EmergencyAlerts.Application.Dtos;

/// <summary>
/// DTO for approving an alert.
/// </summary>
public class ApproveAlertDto
{
    [Required(ErrorMessage = "Alert ID is required")]
    public Guid AlertId { get; set; }

    /// <summary>
    /// ETag value for optimistic concurrency control (If-Match header).
    /// </summary>
    public string? ETag { get; set; }
}

/// <summary>
/// DTO for rejecting an alert.
/// </summary>
public class RejectAlertDto
{
    [Required(ErrorMessage = "Alert ID is required")]
    public Guid AlertId { get; set; }

    [Required(ErrorMessage = "Rejection reason is required")]
    [StringLength(500, ErrorMessage = "Rejection reason cannot exceed 500 characters")]
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>
    /// ETag value for optimistic concurrency control (If-Match header).
    /// </summary>
    public string? ETag { get; set; }
}

/// <summary>
/// DTO for cancelling an alert.
/// </summary>
public class CancelAlertDto
{
    [Required(ErrorMessage = "Alert ID is required")]
    public Guid AlertId { get; set; }
}
