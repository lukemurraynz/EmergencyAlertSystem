using EmergencyAlerts.Application.Queries;
using EmergencyAlerts.Domain.Entities;
using EmergencyAlerts.Domain.Repositories;
using EmergencyAlerts.Domain.Services;
using Moq;
using Xunit;

namespace EmergencyAlerts.Application.Tests;

public class AlertQueryHandlerTests
{
    [Fact]
    public async Task GetDashboardSummaryAsync_ExcludesExpiredPendingApprovals()
    {
        var createdAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var createTimeProvider = new FixedTimeProvider(createdAt);
        var idGenerator = new FixedIdGenerator();

        var pendingExpired = Alert.Create(
            "Expired pending",
            "Expired pending description",
            Severity.Severe,
            ChannelType.Sms,
            createdAt.AddHours(1),
            "tester",
            createTimeProvider,
            idGenerator);

        var pendingActive = Alert.Create(
            "Active pending",
            "Active pending description",
            Severity.Moderate,
            ChannelType.Sms,
            createdAt.AddHours(3),
            "tester",
            createTimeProvider,
            idGenerator);

        var alertRepo = new Mock<IAlertRepository>();
        alertRepo
            .Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert> { pendingExpired, pendingActive });

        var correlationRepo = new Mock<ICorrelationEventRepository>();
        correlationRepo
            .Setup(repo => repo.GetActiveEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrelationEvent>());

        var deliveryAttemptRepo = new Mock<IDeliveryAttemptRepository>();
        deliveryAttemptRepo
            .Setup(repo => repo.GetAttemptsSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeliveryAttempt>());

        var queryTimeProvider = new FixedTimeProvider(createdAt.AddHours(2));
        var handler = new AlertQueryHandler(alertRepo.Object, correlationRepo.Object, deliveryAttemptRepo.Object, queryTimeProvider);

        var result = await handler.HandleAsync(new GetDashboardSummaryQuery(), CancellationToken.None);

        Assert.Equal(1, result.Summary.Counts.PendingApproval);
        Assert.Equal(1, result.Summary.Counts.Expired);
        Assert.Equal(2, result.Summary.Counts.Total);
    }

    [Fact]
    public async Task ListAlertsAsync_MapsExpiredPendingToExpiredStatus()
    {
        var createdAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var createTimeProvider = new FixedTimeProvider(createdAt);
        var idGenerator = new FixedIdGenerator();

        var pendingExpired = Alert.Create(
            "Expired pending",
            "Expired pending description",
            Severity.Severe,
            ChannelType.Sms,
            createdAt.AddHours(1),
            "tester",
            createTimeProvider,
            idGenerator);

        var pendingActive = Alert.Create(
            "Active pending",
            "Active pending description",
            Severity.Moderate,
            ChannelType.Sms,
            createdAt.AddHours(3),
            "tester",
            createTimeProvider,
            idGenerator);

        var alertRepo = new Mock<IAlertRepository>();
        alertRepo
            .Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert> { pendingExpired, pendingActive });

        var correlationRepo = new Mock<ICorrelationEventRepository>();
        correlationRepo
            .Setup(repo => repo.GetActiveEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrelationEvent>());

        var deliveryAttemptRepo = new Mock<IDeliveryAttemptRepository>();
        deliveryAttemptRepo
            .Setup(repo => repo.GetAttemptsSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeliveryAttempt>());

        var queryTimeProvider = new FixedTimeProvider(createdAt.AddHours(2));
        var handler = new AlertQueryHandler(alertRepo.Object, correlationRepo.Object, deliveryAttemptRepo.Object, queryTimeProvider);

        var result = await handler.HandleAsync(new ListAlertsQuery(), CancellationToken.None);

        var expiredDto = result.Alerts.Single(alert => alert.AlertId == pendingExpired.AlertId);
        var activeDto = result.Alerts.Single(alert => alert.AlertId == pendingActive.AlertId);

        Assert.Equal(AlertStatus.Expired.ToString(), expiredDto.Status);
        Assert.Equal(AlertStatus.PendingApproval.ToString(), activeDto.Status);
    }

    private sealed class FixedTimeProvider : ITimeProvider
    {
        public FixedTimeProvider(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class FixedIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
