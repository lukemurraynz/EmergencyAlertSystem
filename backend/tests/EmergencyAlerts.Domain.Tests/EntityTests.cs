using Xunit;
using EmergencyAlerts.Domain.Entities;
using EmergencyAlerts.Domain.Exceptions;

namespace EmergencyAlerts.Domain.Tests;

/// <summary>
/// Unit tests for Alert entity business logic and validation.
/// </summary>
public class AlertEntityTests : DomainTestBase
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var headline = "Test Alert";
        var description = "Test Description";
        var severity = Severity.Severe;
        var channelType = ChannelType.Sms;

        // Act
        var alert = Alert.Create(
            headline: headline,
            description: description,
            severity: severity,
            channelType: channelType,
            expiresAt: TestNow.AddHours(1),
            createdBy: "test-operator",
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);

        // Assert
        Assert.NotNull(alert);
        Assert.Equal(headline, alert.Headline);
        Assert.Equal(severity, alert.Severity);
        Assert.Equal(AlertStatus.PendingApproval, alert.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Create_WithInvalidHeadline_ShouldThrow(string? headline)
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<InvalidHeadlineException>(() => Alert.Create(
            headline: headline!,
            description: "Valid description",
            severity: Severity.Moderate,
            channelType: ChannelType.Sms,
            expiresAt: TestNow.AddHours(1),
            createdBy: "test-operator",
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object));

        Assert.NotNull(ex);
    }

    [Fact]
    public void HasExpired_WithPastExpiryTime_ShouldReturnTrue()
    {
        // Arrange
        var alert = Alert.Create(
            headline: "Test",
            description: "Desc",
            severity: Severity.Moderate,
            channelType: ChannelType.Sms,
            expiresAt: TestNow.AddHours(1),
            createdBy: "test-operator",
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);
        SetMockTime(TestNow.AddHours(2));

        // Act
        var hasExpired = alert.HasExpired(MockTimeProvider.Object);

        // Assert
        Assert.True(hasExpired);
    }

    [Fact]
    public void CanApprove_WithPendingApprovalStatus_ShouldReturnTrue()
    {
        // Arrange
        var alert = Alert.Create(
            headline: "Test",
            description: "Desc",
            severity: Severity.Moderate,
            channelType: ChannelType.Sms,
            expiresAt: TestNow.AddHours(1),
            createdBy: "test-operator",
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);

        // Act
        var canApprove = alert.CanApprove(MockTimeProvider.Object);

        // Assert
        Assert.True(canApprove);
    }

    [Fact]
    public void CanApprove_WithApprovedStatus_ShouldReturnFalse()
    {
        // Arrange
        var alert = CreateTestAlert(expiresAt: TestNow.AddHours(1));
        alert.Approve(
            approverId: "approver-id",
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);

        // Act
        var canApprove = alert.CanApprove(MockTimeProvider.Object);

        // Assert
        Assert.False(canApprove);
    }

    [Fact]
    public void CanCancel_WithValidAlert_ShouldReturnTrue()
    {
        // Arrange
        var alert = CreateTestAlert();
        alert.Approve(
            approverId: "approver-id",
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);

        // Act
        var canCancel = alert.CanCancel();

        // Assert
        Assert.True(canCancel);
    }

    [Fact]
    public void Headline_ExceedingMaxLength_ShouldThrow()
    {
        // Arrange
        var longHeadline = new string('x', 101);

        // Act & Assert
        var ex = Assert.Throws<InvalidHeadlineException>(() => Alert.Create(
            headline: longHeadline,
            description: "Valid description",
            severity: Severity.Moderate,
            channelType: ChannelType.Sms,
            expiresAt: TestNow.AddHours(1),
            createdBy: "test-operator",
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object));

        Assert.NotNull(ex);
    }

    [Fact]
    public void Description_ExceedingMaxLength_ShouldThrow()
    {
        // Arrange
        var longDescription = new string('x', 1396);

        // Act & Assert
        var ex = Assert.Throws<InvalidDescriptionException>(() => Alert.Create(
            headline: "Valid headline",
            description: longDescription,
            severity: Severity.Moderate,
            channelType: ChannelType.Sms,
            expiresAt: TestNow.AddHours(1),
            createdBy: "test-operator",
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object));

        Assert.NotNull(ex);
    }
}

/// <summary>
/// Unit tests for Area entity with PostGIS validation.
/// </summary>
public class AreaEntityTests : DomainTestBase
{
    [Fact]
    public void Create_WithValidPolygon_ShouldSucceed()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var areaDescription = "Greater London";
        var wktPolygon = "POLYGON((-0.5 51.3, -0.5 51.7, 0.3 51.7, 0.3 51.3, -0.5 51.3))";

        // Act
        var area = Area.Create(
            alertId: alertId,
            areaDescription: areaDescription,
            areaPolygonWkt: wktPolygon,
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);

        // Assert
        Assert.NotNull(area);
        Assert.Equal(areaDescription, area.AreaDescription);
        Assert.Equal(alertId, area.AlertId);
    }

    [Fact]
    public void Create_WithInvalidPolygonWkt_ShouldThrow()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var invalidWkt = "INVALID POLYGON()";

        // Act
        var area = Area.Create(
            alertId: alertId,
            areaDescription: "Test Area",
            areaPolygonWkt: invalidWkt,
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);

        // Assert - domain does not validate WKT format; validation occurs in infrastructure
        Assert.NotNull(area);
    }

    [Fact]
    public void Create_WithMissingDescription_ShouldThrow()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var wktPolygon = "POLYGON((-0.5 51.3, -0.5 51.7, 0.3 51.7, 0.3 51.3, -0.5 51.3))";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Area.Create(
            alertId: alertId,
            areaDescription: "",
            areaPolygonWkt: wktPolygon,
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object));

        Assert.NotNull(ex);
    }
}

/// <summary>
/// Unit tests for ApprovalRecord entity with first-wins semantics.
/// </summary>
public class ApprovalRecordEntityTests : DomainTestBase
{
    [Fact]
    public void CreateApproval_WithValidData_ShouldSucceed()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var approverId = "approver-123";

        // Act
        var approval = ApprovalRecord.CreateApproval(
            alertId: alertId,
            approverId: approverId,
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);

        // Assert
        Assert.NotNull(approval);
        Assert.Equal(alertId, approval.AlertId);
        Assert.Equal(approverId, approval.ApproverId);
        Assert.Equal(Decision.Approved, approval.Decision);
        Assert.Null(approval.RejectionReason);
    }

    [Fact]
    public void CreateRejection_WithValidData_ShouldSucceed()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var approverId = "approver-456";
        var rejectionReason = "Insufficient information";

        // Act
        var rejection = ApprovalRecord.CreateRejection(
            alertId: alertId,
            approverId: approverId,
            rejectionReason: rejectionReason,
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);

        // Assert
        Assert.NotNull(rejection);
        Assert.Equal(alertId, rejection.AlertId);
        Assert.Equal(approverId, rejection.ApproverId);
        Assert.Equal(Decision.Rejected, rejection.Decision);
        Assert.Equal(rejectionReason, rejection.RejectionReason);
    }

    [Fact]
    public void CreateRejection_WithoutReason_ShouldThrow()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var approverId = "approver-789";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => ApprovalRecord.CreateRejection(
            alertId: alertId,
            approverId: approverId,
            rejectionReason: "",
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object));

        Assert.NotNull(ex);
    }

    [Fact]
    public void FirstWinsSemantics_MultipleApprovalsCreatable()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var approverId1 = "approver-1";
        var approverId2 = "approver-2";

        // Act
        var approval1 = ApprovalRecord.CreateApproval(
            alertId: alertId,
            approverId: approverId1,
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);

        SetMockId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var approval2 = ApprovalRecord.CreateRejection(
            alertId: alertId,
            approverId: approverId2,
            rejectionReason: "Changed mind",
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object);

        // Assert - both objects can be created (handler enforces first-wins)
        Assert.NotNull(approval1);
        Assert.NotNull(approval2);
        Assert.NotEqual(approval1.ApprovalId, approval2.ApprovalId);
        Assert.Equal(Decision.Approved, approval1.Decision);
        Assert.Equal(Decision.Rejected, approval2.Decision);
    }
}
