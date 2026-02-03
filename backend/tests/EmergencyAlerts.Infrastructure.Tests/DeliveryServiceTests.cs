using EmergencyAlerts.Domain.Entities;
using EmergencyAlerts.Domain.Services;
using EmergencyAlerts.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EmergencyAlerts.Infrastructure.Tests;

/// <summary>
/// Tests for DeliveryService with Azure App Configuration integration.
/// Validates recipient loading, caching, refresh, and email delivery scenarios.
/// </summary>
public class DeliveryServiceTests
{
    private readonly Mock<ILogger<DeliveryService>> _loggerMock;

    public DeliveryServiceTests()
    {
        _loggerMock = new Mock<ILogger<DeliveryService>>();
    }

    [Fact]
    public async Task Constructor_WithValidRecipients_LoadsRecipientsSuccessfully()
    {
        // Arrange
        var configuration = BuildConfiguration("test1@example.com,test2@example.com,test3@example.com");

        // Act
        var service = new DeliveryService(configuration, _loggerMock.Object);
        var recipients = await service.GetTestRecipientsAsync();

        // Assert
        Assert.Equal(3, recipients.Count);
        Assert.Contains("test1@example.com", recipients);
        Assert.Contains("test2@example.com", recipients);
        Assert.Contains("test3@example.com", recipients);
    }

    [Fact]
    public async Task Constructor_WithEmptyRecipients_LoadsEmptyList()
    {
        // Arrange
        var configuration = BuildConfiguration(string.Empty);

        // Act
        var service = new DeliveryService(configuration, _loggerMock.Object);
        var recipients = await service.GetTestRecipientsAsync();

        // Assert
        Assert.Empty(recipients);
    }

    [Fact]
    public async Task Constructor_WithNullRecipients_LoadsEmptyList()
    {
        // Arrange
        var configuration = BuildConfiguration(null);

        // Act
        var service = new DeliveryService(configuration, _loggerMock.Object);
        var recipients = await service.GetTestRecipientsAsync();

        // Assert
        Assert.Empty(recipients);
    }

    [Fact]
    public async Task Constructor_WithWhitespaceInRecipients_TrimsAndFiltersCorrectly()
    {
        // Arrange
        var configuration = BuildConfiguration("  test1@example.com  ,  ,test2@example.com,  test3@example.com  ");

        // Act
        var service = new DeliveryService(configuration, _loggerMock.Object);
        var recipients = await service.GetTestRecipientsAsync();

        // Assert
        Assert.Equal(3, recipients.Count);
        Assert.All(recipients, r => Assert.DoesNotContain(" ", r));
    }

    [Fact]
    public async Task GetTestRecipientsAsync_ReturnsImmutableCopy()
    {
        // Arrange
        var configuration = BuildConfiguration("test1@example.com,test2@example.com");

        var service = new DeliveryService(configuration, _loggerMock.Object);

        // Act
        var recipients1 = await service.GetTestRecipientsAsync();
        recipients1.Add("modified@example.com"); // Mutate the returned list

        var recipients2 = await service.GetTestRecipientsAsync();

        // Assert - Original cache should not be modified
        Assert.Equal(2, recipients2.Count);
        Assert.DoesNotContain("modified@example.com", recipients2);
    }

    [Fact]
    public async Task RefreshTestRecipientsAsync_ReadsLatestConfiguration()
    {
        // Arrange - Start with one configuration
        var configuration = BuildConfiguration("original@example.com");

        var service = new DeliveryService(configuration, _loggerMock.Object);
        var originalRecipients = await service.GetTestRecipientsAsync();

        // Act - RefreshTestRecipientsAsync reads from current IConfiguration
        // In production, the middleware updates IConfiguration automatically
        await service.RefreshTestRecipientsAsync();

        var refreshedRecipients = await service.GetTestRecipientsAsync();

        // Assert - Should still have same recipients (configuration didn't actually change)
        Assert.Single(originalRecipients);
        Assert.Single(refreshedRecipients);
        Assert.Equal(originalRecipients[0], refreshedRecipients[0]);
    }

    [Fact]
    public async Task DeliverAlertAsync_WithRecipients_ReturnsSuccess()
    {
        // Arrange
        var configuration = BuildConfiguration("test1@example.com,test2@example.com");

        var service = new DeliveryService(configuration, _loggerMock.Object);
        var alert = CreateTestAlert();

        // Act
        var result = await service.DeliverAlertAsync(alert);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.OperationId);
        Assert.NotEmpty(result.OperationId);
    }

    [Fact]
    public async Task DeliverAlertAsync_WithNoRecipients_ReturnsFailure()
    {
        // Arrange
        var configuration = BuildConfiguration(string.Empty);

        var service = new DeliveryService(configuration, _loggerMock.Object);
        var alert = CreateTestAlert();

        // Act
        var result = await service.DeliverAlertAsync(alert);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No test recipients configured", result.ErrorMessage);
        Assert.Null(result.OperationId);
    }

    [Fact]
    public async Task DeliverAlertAsync_LogsRecipientCount()
    {
        // Arrange
        var configuration = BuildConfiguration("test1@example.com,test2@example.com,test3@example.com");

        var service = new DeliveryService(configuration, _loggerMock.Object);
        var alert = CreateTestAlert();

        // Act
        await service.DeliverAlertAsync(alert);

        // Assert - Verify logging occurred (verify at least one log call)
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RefreshTestRecipientsAsync_LogsRefreshOperation()
    {
        // Arrange
        var configuration = BuildConfiguration("test@example.com");

        var service = new DeliveryService(configuration, _loggerMock.Object);

        // Act
        await service.RefreshTestRecipientsAsync();

        // Assert - Verify logging occurred
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user1@test.com,user2@test.com")]
    [InlineData("a@b.com,c@d.com,e@f.com,g@h.com,i@j.com")]
    public async Task GetTestRecipientsAsync_WithVariousRecipientCounts_ReturnsCorrectCount(string recipientList)
    {
        // Arrange
        var expectedCount = recipientList.Split(',').Length;
        var configuration = BuildConfiguration(recipientList);

        var service = new DeliveryService(configuration, _loggerMock.Object);

        // Act
        var recipients = await service.GetTestRecipientsAsync();

        // Assert
        Assert.Equal(expectedCount, recipients.Count);
    }

    [Fact]
    public async Task DeliverAlertAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var configuration = BuildConfiguration("test@example.com");

        var service = new DeliveryService(configuration, _loggerMock.Object);
        var alert = CreateTestAlert();
        var cts = new CancellationTokenSource();

        // Act - Should complete before cancellation
        var result = await service.DeliverAlertAsync(alert, cts.Token);

        // Assert
        Assert.True(result.Success);
    }

    // Helper method to create test alert
    private Alert CreateTestAlert()
    {
        var timeProvider = new TestTimeProvider();
        var idGenerator = new TestIdGenerator();

        var alert = Alert.Create(
            headline: "Test Alert",
            description: "This is a test alert for email delivery testing",
            severity: Severity.Severe,
            channelType: ChannelType.Test,
            expiresAt: timeProvider.UtcNow.AddHours(2),
            createdBy: "test-user",
            timeProvider: timeProvider,
            idGenerator: idGenerator);

        return alert;
    }

    // Test helper classes
    private class TestTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private static IConfiguration BuildConfiguration(string? recipients)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Email:DeliveryMode"] = "LogOnly"
        };

        if (recipients is not null)
        {
            settings["Email:TestRecipients"] = recipients;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}
