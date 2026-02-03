using System.ComponentModel.DataAnnotations;

namespace EmergencyAlerts.Application.Dtos;

/// <summary>
/// DTO for creating a new alert.
/// </summary>
public class CreateAlertDto
{
    [Required(ErrorMessage = "Headline is required")]
    [StringLength(100, ErrorMessage = "Headline cannot exceed 100 characters")]
    public string Headline { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [StringLength(1395, ErrorMessage = "Description cannot exceed 1395 characters (GSM limit)")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Severity is required")]
    public string Severity { get; set; } = string.Empty;

    [Required(ErrorMessage = "Channel type is required")]
    public string ChannelType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Expiry time is required")]
    public DateTime ExpiresAt { get; set; }

    [StringLength(5, ErrorMessage = "Language code cannot exceed 5 characters")]
    public string LanguageCode { get; set; } = "en-GB";

    [Required(ErrorMessage = "At least one area is required")]
    [MinLength(1, ErrorMessage = "At least one area is required")]
    public List<AreaDto> Areas { get; set; } = new();
}

/// <summary>
/// DTO for a geographic area.
/// </summary>
public class AreaDto
{
    [Required(ErrorMessage = "Area description is required")]
    [StringLength(255, ErrorMessage = "Area description cannot exceed 255 characters")]
    public string AreaDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "GeoJSON polygon is required")]
    public string GeoJsonPolygon { get; set; } = string.Empty;

    /// <summary>
    /// Region code for geographic correlation (e.g., "NZ-AKL").
    /// </summary>
    [StringLength(20, ErrorMessage = "Region code cannot exceed 20 characters")]
    public string? RegionCode { get; set; }
}
