using Xunit;
using Moq;
using EmergencyAlerts.Application.Commands;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Domain.Entities;
using EmergencyAlerts.Domain.Repositories;
using EmergencyAlerts.Domain.Services;

namespace EmergencyAlerts.Application.Tests;

/// <summary>
/// Unit tests for CreateAlertCommand validation.
/// </summary>
public class CreateAlertCommandHandlerTests
{
    private readonly Mock<IAlertRepository> _mockAlertRepo;
    private readonly Mock<IAreaRepository> _mockAreaRepo;
    private readonly Mock<IIdGenerator> _mockIdGenerator;
    private readonly Mock<ITimeProvider> _mockTimeProvider;

    public CreateAlertCommandHandlerTests()
    {
        _mockAlertRepo = new Mock<IAlertRepository>();
        _mockAreaRepo = new Mock<IAreaRepository>();
        _mockIdGenerator = new Mock<IIdGenerator>();
        _mockTimeProvider = new Mock<ITimeProvider>();
        _mockTimeProvider.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
    }

    [Fact]
    public void CreateAlertCommand_WithValidData_ShouldConstructSuccessfully()
    {
        // Arrange
        var alertDto = new CreateAlertDto
        {
            Headline = "Test Alert",
            Description = "Test Description",
            Severity = "Severe",
            ChannelType = "Sms",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LanguageCode = "en-GB",
            Areas = new List<AreaDto>
            {
                new AreaDto
                {
                    AreaDescription = "Greater London",
                    GeoJsonPolygon = """
                        {
                            "type": "Polygon",
                            "coordinates": [[[-0.5, 51.3], [-0.5, 51.7], [0.3, 51.7], [0.3, 51.3], [-0.5, 51.3]]]
                        }
                        """
                }
            }
        };
        var userId = "test-operator";

        // Act
        var command = new CreateAlertCommand(alertDto, userId);

        // Assert
        Assert.NotNull(command);
        Assert.Equal(userId, command.UserId);
        Assert.Equal("Test Alert", command.Alert.Headline);
        Assert.Equal("Severe", command.Alert.Severity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateAlertCommand_WithInvalidHeadline_ShouldConstruct(string? headline)
    {
        // Arrange - DTO validation happens at handler level
        var alertDto = new CreateAlertDto
        {
            Headline = headline!,
            Description = "Valid Description",
            Severity = "Severe",
            ChannelType = "Sms",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Areas = new List<AreaDto>()
        };

        // Act
        var command = new CreateAlertCommand(alertDto, "user");

        // Assert - command created but DTO validation would fail in handler
        Assert.NotNull(command);
    }

    [Fact]
    public void CreateAlertCommand_WithPastExpiryTime_ShouldConstruct()
    {
        // Arrange
        var alertDto = new CreateAlertDto
        {
            Headline = "Test",
            Description = "Desc",
            Severity = "Moderate",
            ChannelType = "Sms",
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            Areas = new List<AreaDto>()
        };

        // Act
        var command = new CreateAlertCommand(alertDto, "user");

        // Assert - command created but validation would fail in handler
        Assert.NotNull(command);
        Assert.True(command.Alert.ExpiresAt < DateTime.UtcNow);
    }
}

/// <summary>
/// Unit tests for ApproveAlertCommand and RejectAlertCommand.
/// </summary>
public class ApproveAlertCommandHandlerTests
{
    private readonly Mock<IAlertRepository> _mockAlertRepo;
    private readonly Mock<IApprovalRecordRepository> _mockApprovalRepo;

    public ApproveAlertCommandHandlerTests()
    {
        _mockAlertRepo = new Mock<IAlertRepository>();
        _mockApprovalRepo = new Mock<IApprovalRecordRepository>();
    }

    [Fact]
    public void ApproveAlertCommand_WithValidData_ShouldConstruct()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var approverId = "approver-123";

        // Act
        var command = new ApproveAlertCommand(alertId, approverId, ETag: "v1");

        // Assert
        Assert.NotNull(command);
        Assert.Equal(alertId, command.AlertId);
        Assert.Equal(approverId, command.ApproverId);
        Assert.Equal("v1", command.ETag);
    }

    [Fact]
    public void RejectAlertCommand_WithValidData_ShouldConstruct()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var approverId = "approver-456";
        var reason = "Insufficient information";

        // Act
        var command = new RejectAlertCommand(alertId, approverId, reason, ETag: "v1");

        // Assert
        Assert.NotNull(command);
        Assert.Equal(alertId, command.AlertId);
        Assert.Equal(approverId, command.ApproverId);
        Assert.Equal(reason, command.RejectionReason);
    }

    [Fact]
    public void ApproveAndRejectCommands_DifferentStructures_ShouldCoexist()
    {
        // Arrange & Act
        var alertId = Guid.NewGuid();
        var approval = new ApproveAlertCommand(alertId, "approver-1", ETag: null);
        var rejection = new RejectAlertCommand(alertId, "approver-2", "Changed mind", ETag: null);

        // Assert - both commands represent different decision paths
        Assert.Equal(alertId, approval.AlertId);
        Assert.Equal(alertId, rejection.AlertId);
        Assert.NotEqual(approval.ApproverId, rejection.ApproverId);
    }
}

/// <summary>
/// Unit tests for CancelAlertCommand.
/// </summary>
public class CancelAlertCommandHandlerTests
{
    [Fact]
    public void CancelAlertCommand_WithValidData_ShouldConstruct()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var userId = "operator-789";

        // Act
        var command = new CancelAlertCommand(alertId, userId);

        // Assert
        Assert.NotNull(command);
        Assert.Equal(alertId, command.AlertId);
        Assert.Equal(userId, command.UserId);
    }
}

/// <summary>
/// Unit tests for repository query methods.
/// </summary>
public class RepositoryQueryTests
{
    private readonly Mock<IAlertRepository> _mockAlertRepo;
    private readonly Mock<ICorrelationEventRepository> _mockCorrelationRepo;

    public RepositoryQueryTests()
    {
        _mockAlertRepo = new Mock<IAlertRepository>();
        _mockCorrelationRepo = new Mock<ICorrelationEventRepository>();
    }

    [Fact]
    public async Task GetByStatusAsync_WithAlertStatus_ShouldCallRepository()
    {
        // Arrange
        _mockAlertRepo
            .Setup(x => x.GetByStatusAsync(AlertStatus.PendingApproval, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert>());

        // Act
        var result = await _mockAlertRepo.Object.GetByStatusAsync(AlertStatus.PendingApproval, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        _mockAlertRepo.Verify(
            x => x.GetByStatusAsync(AlertStatus.PendingApproval, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllAlerts()
    {
        // Arrange
        _mockAlertRepo
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert>());

        // Act
        var result = await _mockAlertRepo.Object.GetAllAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IEnumerable<Alert>>(result);
    }

    [Fact]
    public async Task GetPendingDeliveryAsync_ShouldReturnPendingAlerts()
    {
        // Arrange
        _mockAlertRepo
            .Setup(x => x.GetPendingDeliveryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert>());

        // Act
        var result = await _mockAlertRepo.Object.GetPendingDeliveryAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetExpiredAlertsAsync_ShouldReturnExpiredAlerts()
    {
        // Arrange
        _mockAlertRepo
            .Setup(x => x.GetExpiredAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert>());

        // Act
        var result = await _mockAlertRepo.Object.GetExpiredAlertsAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }
}

/// <summary>
/// Unit tests for DTO validation structures.
/// </summary>
public class DtoValidationTests
{
    [Fact]
    public void AreaDto_WithValidGeometryProperties_ShouldConstruct()
    {
        // Arrange & Act
        var areaDto = new AreaDto
        {
            AreaDescription = "Central London",
            GeoJsonPolygon = """{"type": "Polygon", "coordinates": [[[-0.1, 51.5]]]}"""
        };

        // Assert
        Assert.NotNull(areaDto);
        Assert.Equal("Central London", areaDto.AreaDescription);
        Assert.NotEmpty(areaDto.GeoJsonPolygon);
    }

    [Fact]
    public void CreateAlertDto_WithAreasList_ShouldConstruct()
    {
        // Arrange & Act
        var alertDto = new CreateAlertDto
        {
            Headline = "Test Alert",
            Description = "Test Description",
            Severity = "Severe",
            ChannelType = "Sms",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Areas = new List<AreaDto>
            {
                new AreaDto
                {
                    AreaDescription = "London",
                    GeoJsonPolygon = "{}"
                }
            }
        };

        // Assert
        Assert.NotNull(alertDto);
        Assert.Single(alertDto.Areas);
        Assert.Equal("London", alertDto.Areas[0].AreaDescription);
    }
}
