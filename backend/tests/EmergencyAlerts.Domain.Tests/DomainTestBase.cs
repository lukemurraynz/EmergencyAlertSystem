using Moq;
using EmergencyAlerts.Domain.Services;
using EmergencyAlerts.Domain.Entities;

namespace EmergencyAlerts.Domain.Tests;

/// <summary>
/// Base class for domain tests providing shared mock infrastructure and helper methods.
/// </summary>
public abstract class DomainTestBase
{
    protected Mock<ITimeProvider> MockTimeProvider { get; }
    protected Mock<IIdGenerator> MockIdGenerator { get; }
    protected DateTime TestNow { get; }
    protected Guid TestGuid { get; }

    protected DomainTestBase()
    {
        TestNow = new DateTime(2026, 1, 26, 12, 0, 0, DateTimeKind.Utc);
        TestGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

        MockTimeProvider = new Mock<ITimeProvider>();
        MockTimeProvider.Setup(x => x.UtcNow).Returns(TestNow);

        MockIdGenerator = new Mock<IIdGenerator>();
        MockIdGenerator.Setup(x => x.NewId()).Returns(TestGuid);
    }

    /// <summary>
    /// Creates a test alert with default or custom values.
    /// </summary>
    protected Alert CreateTestAlert(
        string headline = "Test Alert",
        string description = "Test Description",
        Severity severity = Severity.Severe,
        ChannelType channelType = ChannelType.Sms,
        DateTime? expiresAt = null,
        string createdBy = "test-operator",
        string languageCode = "en-GB")
    {
        return Alert.Create(
            headline: headline,
            description: description,
            severity: severity,
            channelType: channelType,
            expiresAt: expiresAt ?? TestNow.AddHours(1),
            createdBy: createdBy,
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object,
            languageCode: languageCode
        );
    }

    /// <summary>
    /// Creates a test area with default or custom values.
    /// </summary>
    protected Area CreateTestArea(
        Guid alertId,
        string areaDescription = "Test Area",
        string? areaPolygonWkt = null,
        string? regionCode = null)
    {
        // Default to a simple polygon WKT (London area approximation)
        areaPolygonWkt ??= "POLYGON((-0.5 51.3, -0.5 51.7, 0.3 51.7, 0.3 51.3, -0.5 51.3))";

        return Area.Create(
            alertId: alertId,
            areaDescription: areaDescription,
            areaPolygonWkt: areaPolygonWkt,
            timeProvider: MockTimeProvider.Object,
            idGenerator: MockIdGenerator.Object,
            regionCode: regionCode
        );
    }

    /// <summary>
    /// Creates a test approval record with default or custom values.
    /// </summary>
    protected ApprovalRecord CreateTestApprovalRecord(
        Guid alertId,
        Decision decision = Decision.Approved,
        string approverId = "test-approver",
        string? rejectionReason = null)
    {
        if (decision == Decision.Approved)
        {
            return ApprovalRecord.CreateApproval(
                alertId: alertId,
                approverId: approverId,
                timeProvider: MockTimeProvider.Object,
                idGenerator: MockIdGenerator.Object
            );
        }
        else
        {
            return ApprovalRecord.CreateRejection(
                alertId: alertId,
                approverId: approverId,
                rejectionReason: rejectionReason ?? "Test rejection reason",
                timeProvider: MockTimeProvider.Object,
                idGenerator: MockIdGenerator.Object
            );
        }
    }

    /// <summary>
    /// Configures mock time provider to return a specific time.
    /// </summary>
    protected void SetMockTime(DateTime utcTime)
    {
        MockTimeProvider.Setup(x => x.UtcNow).Returns(utcTime);
    }

    /// <summary>
    /// Configures mock ID generator to return a specific GUID.
    /// </summary>
    protected void SetMockId(Guid id)
    {
        MockIdGenerator.Setup(x => x.NewId()).Returns(id);
    }
}
