using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyAlerts.Domain.Services;

namespace EmergencyAlerts.Domain.Entities;

/// <summary>
/// Drasi-detected pattern event (geographic cluster, regional hotspot, etc.).
/// </summary>
public class CorrelationEvent
{
    public Guid EventId { get; private set; }
    public PatternType PatternType { get; private set; }
    private readonly List<Guid> _alertIds = new();
    public IReadOnlyCollection<Guid> AlertIds => _alertIds.AsReadOnly();
    public DateTime DetectionTimestamp { get; private set; }
    public Severity? ClusterSeverity { get; private set; }
    public string? RegionCode { get; private set; }
    public string? Metadata { get; private set; } // JSON
    public DateTime? ResolvedAt { get; private set; }

    // Private constructor for EF Core
    private CorrelationEvent() { }

    /// <summary>
    /// Creates a new correlation event.
    /// </summary>
    public static CorrelationEvent Create(
        PatternType patternType,
        IEnumerable<Guid> alertIds,
        ITimeProvider timeProvider,
        IIdGenerator idGenerator,
        Severity? clusterSeverity = null,
        string? regionCode = null,
        string? metadata = null)
    {
        var alertIdsList = alertIds.ToList();
        if (!alertIdsList.Any())
            throw new ArgumentException("At least one alert ID is required", nameof(alertIds));

        var correlationEvent = new CorrelationEvent
        {
            EventId = idGenerator.NewId(),
            PatternType = patternType,
            DetectionTimestamp = timeProvider.UtcNow,
            ClusterSeverity = clusterSeverity,
            RegionCode = regionCode,
            Metadata = metadata
        };

        // Add alert IDs to the internal list
        foreach (var alertId in alertIdsList)
        {
            correlationEvent._alertIds.Add(alertId);
        }

        return correlationEvent;
    }

    /// <summary>
    /// Marks the correlation event as resolved.
    /// </summary>
    public void Resolve(ITimeProvider timeProvider)
    {
        if (ResolvedAt.HasValue)
            throw new InvalidOperationException("Correlation event is already resolved");

        ResolvedAt = timeProvider.UtcNow;
    }

    /// <summary>
    /// Checks if the correlation event is active (not resolved).
    /// </summary>
    public bool IsActive() => !ResolvedAt.HasValue;

    /// <summary>
    /// Checks if the correlation involves a specific alert.
    /// </summary>
    public bool InvolvesAlert(Guid alertId) => _alertIds.Contains(alertId);
}

/// <summary>
/// Pattern types for Drasi correlation detection.
/// </summary>
public enum PatternType
{
    GeographicCluster,
    RegionalHotspot,
    SeverityEscalation,
    DuplicateSuppression,
    AreaExpansionSuggestion,
    RateSpike,
    ExpiryWarning,
    SlaBreach,
    ApprovalTimeout
}
