using System.Text.Json.Serialization;

namespace EmergencyAlerts.Api.Tests.Integration.Dtos;

/// <summary>
/// Represents the Azure-style error response format.
/// Used by tests to validate error responses without conflicting with success response deserialization.
/// </summary>
public class ErrorResponseDto
{
    [JsonPropertyName("error")]
    public ErrorDetailDto? Error { get; set; }
}

public class ErrorDetailDto
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; set; }
}
