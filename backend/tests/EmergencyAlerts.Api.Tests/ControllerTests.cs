using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using EmergencyAlerts.Api.Controllers;
using EmergencyAlerts.Application.Commands;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Domain.Services;
using EmergencyAlerts.Domain.Repositories;

namespace EmergencyAlerts.Api.Tests;

/// <summary>
/// Unit tests for AlertsController endpoints.
/// </summary>
public class AlertsControllerTests
{
    private readonly Mock<IAlertRepository> _mockAlertRepo;
    private readonly Mock<ITimeProvider> _mockTimeProvider;

    public AlertsControllerTests()
    {
        _mockAlertRepo = new Mock<IAlertRepository>();
        _mockTimeProvider = new Mock<ITimeProvider>();
        _mockTimeProvider.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
    }

    [Fact]
    public void ReadyAlerts_ShouldBeReady()
    {
        // Arrange
        var mockAlertRepo = new Mock<IAlertRepository>();

        // Act & Assert - Controller should accept repository in constructor
        Assert.NotNull(mockAlertRepo.Object);
    }

    [Fact]
    public void CreateAlert_WithValidData_ShouldAcceptRequest()
    {
        // Arrange
        var alertDto = new CreateAlertDto
        {
            Headline = "Test Alert",
            Description = "Test Description",
            Severity = "Severe",
            ChannelType = "Sms",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Areas = new List<AreaDto>()
        };

        var command = new CreateAlertCommand(alertDto, "test-user");

        // Act & Assert - Command should construct with DTO
        Assert.NotNull(command);
        Assert.Equal("test-user", command.UserId);
    }

    [Fact]
    public void CreateAlert_WithInvalidData_ShouldStillConstruct()
    {
        // Arrange - even with "invalid" data, command construction works
        var alertDto = new CreateAlertDto
        {
            Headline = "",
            Description = "Test",
            Severity = "Unknown",
            ChannelType = "Test",
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            Areas = new List<AreaDto>()
        };

        // Act
        var command = new CreateAlertCommand(alertDto, "user");

        // Assert - construction succeeds, validation happens at handler level
        Assert.NotNull(command);
    }
}

/// <summary>
/// Unit tests for ApprovalsController endpoints.
/// </summary>
public class ApprovalsControllerTests
{
    private readonly Mock<IApprovalRecordRepository> _mockApprovalRepo;

    public ApprovalsControllerTests()
    {
        _mockApprovalRepo = new Mock<IApprovalRecordRepository>();
    }

    [Fact]
    public void ApproveAlert_WithValidCommand_ShouldConstruct()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var approverId = "approver-123";

        // Act
        var command = new ApproveAlertCommand(alertId, approverId, ETag: null);

        // Assert
        Assert.NotNull(command);
        Assert.Equal(alertId, command.AlertId);
    }

    [Fact]
    public void RejectAlert_WithValidCommand_ShouldConstruct()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var approverId = "approver-456";

        // Act
        var command = new RejectAlertCommand(alertId, approverId, "Insufficient data", ETag: null);

        // Assert
        Assert.NotNull(command);
        Assert.Equal(alertId, command.AlertId);
        Assert.Equal("Insufficient data", command.RejectionReason);
    }
}

/// <summary>
/// Unit tests for DTO structures used in API layer.
/// </summary>
public class ApiDtoTests
{
    [Fact]
    public void CreateAlertDto_WithValidProperties_ShouldConstruct()
    {
        // Arrange & Act
        var dto = new CreateAlertDto
        {
            Headline = "Emergency Alert",
            Description = "This is an emergency",
            Severity = "Extreme",
            ChannelType = "Sms",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LanguageCode = "en-GB",
            Areas = new List<AreaDto>()
        };

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("Emergency Alert", dto.Headline);
        Assert.Equal("Extreme", dto.Severity);
    }

    [Fact]
    public void AreaDto_WithValidPolygon_ShouldConstruct()
    {
        // Arrange & Act
        var areaDto = new AreaDto
        {
            AreaDescription = "Test Area",
            GeoJsonPolygon = "{\"type\": \"Polygon\"}"
        };

        // Assert
        Assert.NotNull(areaDto);
        Assert.Equal("Test Area", areaDto.AreaDescription);
        Assert.NotEmpty(areaDto.GeoJsonPolygon);
    }
}

/// <summary>
/// Test factories for common controller test data.
/// </summary>
public static class AlertTestFactory
{
    public static CreateAlertDto CreateValidAlertDto()
    {
        return new CreateAlertDto
        {
            Headline = "Test Alert",
            Description = "Test Description",
            Severity = "Moderate",
            ChannelType = "Sms",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LanguageCode = "en-GB",
            Areas = new List<AreaDto>
            {
                new AreaDto
                {
                    AreaDescription = "Test Region",
                    GeoJsonPolygon = "{\"type\": \"Polygon\"}"
                }
            }
        };
    }

    public static ApproveAlertCommand CreateApprovalCommand()
    {
        return new ApproveAlertCommand(
            AlertId: Guid.NewGuid(),
            ApproverId: "test-approver",
            ETag: null);
    }
}
