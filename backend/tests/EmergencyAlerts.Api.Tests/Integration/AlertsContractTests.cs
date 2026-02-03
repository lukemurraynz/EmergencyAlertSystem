using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EmergencyAlerts.Application.Dtos;

namespace EmergencyAlerts.Api.Tests.Integration;

/// <summary>
/// Integration tests for Alerts API contract validation.
/// Validates that endpoints return correct response schemas and status codes.
/// </summary>
public class AlertsContractTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public AlertsContractTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task POST_Alerts_Returns201Created_WithAlertDto()
    {
        // Create per-test client for rate limit isolation
        var client = _fixture.CreateAuthenticatedClient("operator", "operator");

        // Arrange
        var createDto = new CreateAlertDto
        {
            Headline = "Test Alert",
            Description = "Integration test alert",
            Severity = "Minor",
            ChannelType = "Sms",
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            LanguageCode = "en-GB",
            Areas = new List<AreaDto>
            {
                new AreaDto
                {
                    AreaDescription = "Test Area",
                    GeoJsonPolygon = "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}"
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/alerts", createDto);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Validate ETag header present
        Assert.True(response.Headers.Contains("ETag"));

        // Validate response body schema
        var alert = await response.Content.ReadFromJsonAsync<AlertDto>();
        Assert.NotNull(alert);
        Assert.NotEqual(Guid.Empty, alert.AlertId);
        Assert.Equal("Test Alert", alert.Headline);
        Assert.Equal("PendingApproval", alert.Status);
        Assert.Single(alert.Areas);
    }

    [Fact]
    public async Task GET_Alerts_ReturnsAlertList()
    {
        // Create per-test client for rate limit isolation
        var client = _fixture.CreateAuthenticatedClient("operator", "operator");

        // Act
        var response = await client.GetAsync("/api/v1/alerts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        // Validate list structure (even if empty)
        Assert.NotNull(content);
    }

    [Fact]
    public async Task GET_AlertById_Returns200_WithETag()
    {
        // Create per-test client for rate limit isolation
        var client = _fixture.CreateAuthenticatedClient("operator", "operator");

        // Arrange - Create an alert first
        var createDto = new CreateAlertDto
        {
            Headline = "Test Alert for GET",
            Description = "Test",
            Severity = "Minor",
            ChannelType = "Sms",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Areas = new List<AreaDto>
            {
                new AreaDto
                {
                    AreaDescription = "Test",
                    GeoJsonPolygon = "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}"
                }
            }
        };

        var createResponse = await client.PostAsJsonAsync("/api/v1/alerts", createDto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdAlert = await createResponse.Content.ReadFromJsonAsync<AlertDto>();

        // Act
        var response = await client.GetAsync($"/api/v1/alerts/{createdAlert!.AlertId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("ETag"));

        var alert = await response.Content.ReadFromJsonAsync<AlertDto>();
        Assert.NotNull(alert);
        Assert.Equal(createdAlert.AlertId, alert.AlertId);
    }

    [Fact]
    public async Task GET_AlertById_Returns404_WhenNotFound()
    {
        // Create per-test client for rate limit isolation
        var client = _fixture.CreateAuthenticatedClient("operator", "operator");

        // Act
        var response = await client.GetAsync($"/api/v1/alerts/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
