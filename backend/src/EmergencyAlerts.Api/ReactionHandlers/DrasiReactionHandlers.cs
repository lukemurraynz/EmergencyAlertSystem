using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using EmergencyAlerts.Api.Hubs;
using EmergencyAlerts.Domain.Repositories;
using EmergencyAlerts.Domain.Services;

namespace EmergencyAlerts.Api.ReactionHandlers;

/// <summary>
/// Base class for Drasi reaction handlers.
/// Implements idempotency key generation and common functionality.
/// </summary>
public abstract class DrasiReactionHandlerBase
{
    protected readonly ILogger Logger;
    protected readonly IHubContext<AlertHub, IAlertHubClient> HubContext;

    protected DrasiReactionHandlerBase(
        ILogger logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
    {
        Logger = logger;
        HubContext = hubContext;
    }

    /// <summary>
    /// Generates a deterministic idempotency key per SC-012 requirement.
    /// Format: ${queryId}:${entityId}:${windowStart}
    /// </summary>
    protected string GenerateIdempotencyKey(string queryId, Guid entityId, DateTime windowStart)
    {
        return $"{queryId}:{entityId}:{windowStart:yyyyMMddHHmmss}";
    }

    /// <summary>
    /// Generates a deterministic idempotency key for event-based reactions.
    /// </summary>
    protected string GenerateEventIdempotencyKey(string queryId, Guid eventId)
    {
        return $"{queryId}:{eventId}";
    }
}

/// <summary>
/// Reaction handler for newly approved alerts (delivery trigger).
/// Triggered by: delivery-trigger.cypher query
/// </summary>
public class DeliveryTriggerReactionHandler : DrasiReactionHandlerBase
{
    private readonly IAlertRepository _alertRepository;
    private readonly ITimeProvider _timeProvider;

    public DeliveryTriggerReactionHandler(
        IAlertRepository alertRepository,
        ITimeProvider timeProvider,
        ILogger<DeliveryTriggerReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _alertRepository = alertRepository;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = GenerateIdempotencyKey("delivery-trigger", alertId, _timeProvider.UtcNow);

        Logger.LogInformation(
            "Processing delivery trigger reaction. AlertId: {AlertId}, IdempotencyKey: {IdempotencyKey}",
            alertId, idempotencyKey);

        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert == null)
        {
            Logger.LogWarning("Alert not found for delivery trigger: {AlertId}", alertId);
            return;
        }

        // Notify dashboard subscribers
        await HubContext.Clients.Group("dashboard").AlertStatusChanged(new
        {
            alertId = alert.AlertId,
            status = alert.Status.ToString(),
            timestamp = _timeProvider.UtcNow,
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed delivery trigger for alert: {AlertId}", alertId);
    }
}

/// <summary>
/// Reaction handler for SLA breach detection.
/// Triggered by: delivery-sla-breach.cypher query (trueFor > 60s)
/// </summary>
public class SLABreachReactionHandler : DrasiReactionHandlerBase
{
    private readonly IAlertRepository _alertRepository;
    private readonly ITimeProvider _timeProvider;

    public SLABreachReactionHandler(
        IAlertRepository alertRepository,
        ITimeProvider timeProvider,
        ILogger<SLABreachReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _alertRepository = alertRepository;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        Guid alertId,
        string? headline,
        string? severity,
        int elapsedSeconds,
        CancellationToken cancellationToken = default)
    {
        var windowStart = _timeProvider.UtcNow.AddSeconds(-elapsedSeconds);
        var idempotencyKey = GenerateIdempotencyKey("delivery-sla-breach", alertId, windowStart);

        Logger.LogWarning(
            "SLA breach detected. AlertId: {AlertId}, Headline: {Headline}, ElapsedSeconds: {ElapsedSeconds}, IdempotencyKey: {IdempotencyKey}",
            alertId, headline ?? "(not provided)", elapsedSeconds, idempotencyKey);

        // Use provided headline/severity from Drasi query, fall back to DB lookup if needed
        var resolvedHeadline = headline;
        var resolvedSeverity = severity;

        if (string.IsNullOrEmpty(resolvedHeadline) || string.IsNullOrEmpty(resolvedSeverity))
        {
            var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
            if (alert != null)
            {
                resolvedHeadline ??= alert.Headline;
                resolvedSeverity ??= alert.Severity.ToString();
            }
            else
            {
                Logger.LogWarning("Alert not found for SLA breach and no headline/severity provided: {AlertId}", alertId);
                resolvedHeadline ??= "Unknown Alert";
                resolvedSeverity ??= "Unknown";
            }
        }

        // Notify dashboard subscribers of SLA breach
        await HubContext.Clients.Group("dashboard").SLABreachDetected(new
        {
            alertId,
            headline = resolvedHeadline,
            severity = resolvedSeverity,
            elapsedSeconds,
            detectionTimestamp = _timeProvider.UtcNow,
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed SLA breach notification for alert: {AlertId}", alertId);
    }
}

/// <summary>
/// Reaction handler for approval timeout detection.
/// Triggered by: approval-timeout.cypher query (trueLater @ 5min)
/// </summary>
public class ApprovalTimeoutReactionHandler : DrasiReactionHandlerBase
{
    private readonly IAlertRepository _alertRepository;
    private readonly ITimeProvider _timeProvider;

    public ApprovalTimeoutReactionHandler(
        IAlertRepository alertRepository,
        ITimeProvider timeProvider,
        ILogger<ApprovalTimeoutReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _alertRepository = alertRepository;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(Guid alertId, int elapsedMinutes, CancellationToken cancellationToken = default)
    {
        var windowStart = _timeProvider.UtcNow.AddMinutes(-elapsedMinutes);
        var idempotencyKey = GenerateIdempotencyKey("approval-timeout", alertId, windowStart);

        Logger.LogWarning(
            "Approval timeout detected. AlertId: {AlertId}, ElapsedMinutes: {ElapsedMinutes}, IdempotencyKey: {IdempotencyKey}",
            alertId, elapsedMinutes, idempotencyKey);

        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert == null)
        {
            Logger.LogWarning("Alert not found for approval timeout: {AlertId}", alertId);
            return;
        }

        // Notify dashboard subscribers of approval timeout
        await HubContext.Clients.Group("dashboard").ApprovalTimeoutDetected(new
        {
            alertId = alert.AlertId,
            headline = alert.Headline,
            severity = alert.Severity.ToString(),
            createdAt = alert.CreatedAt,
            elapsedMinutes,
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed approval timeout notification for alert: {AlertId}", alertId);
    }
}

/// <summary>
/// Reaction handler for geographic correlation events.
/// Triggered by: geographic-correlation.cypher query (3+ overlapping alerts in 24h)
/// </summary>
public class GeographicCorrelationReactionHandler : DrasiReactionHandlerBase
{
    private readonly ICorrelationEventRepository _correlationRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly ITimeProvider _timeProvider;

    public GeographicCorrelationReactionHandler(
        ICorrelationEventRepository correlationRepository,
        IIdGenerator idGenerator,
        ITimeProvider timeProvider,
        ILogger<GeographicCorrelationReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _correlationRepository = correlationRepository;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(List<Guid> alertIds, string? clusterSeverity, CancellationToken cancellationToken = default)
    {
        var eventId = _idGenerator.NewId();
        var idempotencyKey = GenerateEventIdempotencyKey("geographic-correlation", eventId);

        Logger.LogInformation(
            "Geographic correlation detected. AlertCount: {AlertCount}, IdempotencyKey: {IdempotencyKey}",
            alertIds.Count, idempotencyKey);

        // Create correlation event
        var correlationEvent = EmergencyAlerts.Domain.Entities.CorrelationEvent.Create(
            patternType: EmergencyAlerts.Domain.Entities.PatternType.GeographicCluster,
            alertIds: alertIds,
            timeProvider: _timeProvider,
            idGenerator: _idGenerator,
            clusterSeverity: string.IsNullOrWhiteSpace(clusterSeverity) ? null :
                Enum.Parse<EmergencyAlerts.Domain.Entities.Severity>(clusterSeverity, ignoreCase: true),
            metadata: $"{{\"idempotencyKey\":\"{idempotencyKey}\"}}");

        await _correlationRepository.AddAsync(correlationEvent, cancellationToken);
        await _correlationRepository.SaveChangesAsync(cancellationToken);

        // Notify dashboard subscribers
        await HubContext.Clients.Group("dashboard").CorrelationEventDetected(new
        {
            eventId = correlationEvent.EventId,
            patternType = "GeographicCluster",
            alertIds,
            clusterSeverity,
            detectionTimestamp = _timeProvider.UtcNow,
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed geographic correlation event: {EventId}", eventId);
    }
}
/// <summary>
/// Reaction handler for regional hotspot detection.
/// Triggered by: regional-hotspot.cypher query (4+ alerts in same region)
/// </summary>
public class RegionalHotspotReactionHandler : DrasiReactionHandlerBase
{
    private readonly ICorrelationEventRepository _correlationRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly ITimeProvider _timeProvider;

    public RegionalHotspotReactionHandler(
        ICorrelationEventRepository correlationRepository,
        IIdGenerator idGenerator,
        ITimeProvider timeProvider,
        ILogger<RegionalHotspotReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _correlationRepository = correlationRepository;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(string regionCode, List<Guid> alertIds, CancellationToken cancellationToken = default)
    {
        var eventId = _idGenerator.NewId();
        var idempotencyKey = GenerateEventIdempotencyKey("regional-hotspot", eventId);

        Logger.LogWarning(
            "Regional hotspot detected. Region: {Region}, AlertCount: {AlertCount}, IdempotencyKey: {IdempotencyKey}",
            regionCode, alertIds.Count, idempotencyKey);

        // Create regional hotspot correlation event
        var correlationEvent = EmergencyAlerts.Domain.Entities.CorrelationEvent.Create(
            patternType: EmergencyAlerts.Domain.Entities.PatternType.RegionalHotspot,
            alertIds: alertIds,
            timeProvider: _timeProvider,
            idGenerator: _idGenerator,
            regionCode: regionCode,
            metadata: $"{{\"idempotencyKey\":\"{idempotencyKey}\"}}");

        await _correlationRepository.AddAsync(correlationEvent, cancellationToken);
        await _correlationRepository.SaveChangesAsync(cancellationToken);

        // Notify dashboard subscribers
        await HubContext.Clients.Group("dashboard").CorrelationEventDetected(new
        {
            eventId = correlationEvent.EventId,
            patternType = "RegionalHotspot",
            region = regionCode,
            alertIds,
            alertCount = alertIds.Count,
            detectionTimestamp = _timeProvider.UtcNow,
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed regional hotspot event: {EventId}", eventId);
    }
}

/// <summary>
/// Reaction handler for severity escalation detection.
/// Triggered by: severity-escalation.cypher query (previousDistinctValue tracking)
/// </summary>
public class SeverityEscalationReactionHandler : DrasiReactionHandlerBase
{
    private readonly ICorrelationEventRepository _correlationRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly ITimeProvider _timeProvider;

    public SeverityEscalationReactionHandler(
        ICorrelationEventRepository correlationRepository,
        IIdGenerator idGenerator,
        ITimeProvider timeProvider,
        ILogger<SeverityEscalationReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _correlationRepository = correlationRepository;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(List<Guid> alertIds, string fromSeverity, string toSeverity, CancellationToken cancellationToken = default)
    {
        var eventId = _idGenerator.NewId();
        var idempotencyKey = GenerateEventIdempotencyKey("severity-escalation", eventId);

        Logger.LogWarning(
            "Severity escalation detected. AlertCount: {AlertCount}, Escalation: {FromSeverity}->{ToSeverity}, IdempotencyKey: {IdempotencyKey}",
            alertIds.Count, fromSeverity, toSeverity, idempotencyKey);

        // Create severity escalation correlation event
        var correlationEvent = EmergencyAlerts.Domain.Entities.CorrelationEvent.Create(
            patternType: EmergencyAlerts.Domain.Entities.PatternType.SeverityEscalation,
            alertIds: alertIds,
            timeProvider: _timeProvider,
            idGenerator: _idGenerator,
            metadata: $"{{\"fromSeverity\":\"{fromSeverity}\",\"toSeverity\":\"{toSeverity}\",\"idempotencyKey\":\"{idempotencyKey}\"}}");

        await _correlationRepository.AddAsync(correlationEvent, cancellationToken);
        await _correlationRepository.SaveChangesAsync(cancellationToken);

        // Notify dashboard subscribers
        await HubContext.Clients.Group("dashboard").CorrelationEventDetected(new
        {
            eventId = correlationEvent.EventId,
            patternType = "SeverityEscalation",
            alertIds,
            escalation = $"{fromSeverity} → {toSeverity}",
            detectionTimestamp = _timeProvider.UtcNow,
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed severity escalation event: {EventId}", eventId);
    }
}

/// <summary>
/// Reaction handler for expiry warnings.
/// Triggered by: expiry-warning.cypher query (trueLater @ 15min before expiry)
/// </summary>
public class ExpiryWarningReactionHandler : DrasiReactionHandlerBase
{
    private readonly IAlertRepository _alertRepository;
    private readonly ITimeProvider _timeProvider;

    public ExpiryWarningReactionHandler(
        IAlertRepository alertRepository,
        ITimeProvider timeProvider,
        ILogger<ExpiryWarningReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _alertRepository = alertRepository;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(Guid alertId, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        var windowStart = expiresAt.AddMinutes(-15);
        var idempotencyKey = GenerateIdempotencyKey("expiry-warning", alertId, windowStart);

        Logger.LogInformation(
            "Expiry warning triggered. AlertId: {AlertId}, ExpiresAt: {ExpiresAt}, IdempotencyKey: {IdempotencyKey}",
            alertId, expiresAt, idempotencyKey);

        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert == null)
        {
            Logger.LogWarning("Alert not found for expiry warning: {AlertId}", alertId);
            return;
        }

        // Notify dashboard subscribers of expiry warning
        await HubContext.Clients.Group("dashboard").AlertStatusChanged(new
        {
            alertId = alert.AlertId,
            headline = alert.Headline,
            severity = alert.Severity.ToString(),
            status = "ExpiryWarning",
            expiresAt,
            minutesRemaining = Math.Max(0, (int)(expiresAt - _timeProvider.UtcNow).TotalMinutes),
            warningTimestamp = _timeProvider.UtcNow,
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed expiry warning for alert: {AlertId}", alertId);
    }
}

/// <summary>
/// Reaction handler for alert creation rate spike detection.
/// Triggered by: rate-spike-detection.cypher query (linearGradient >50 alerts/hour)
/// </summary>
public class RateSpikeDetectionReactionHandler : DrasiReactionHandlerBase
{
    private readonly ITimeProvider _timeProvider;

    public RateSpikeDetectionReactionHandler(
        ITimeProvider timeProvider,
        ILogger<RateSpikeDetectionReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(int alertsInWindow, double creationRatePerHour, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = GenerateEventIdempotencyKey("rate-spike-detection", Guid.NewGuid());

        Logger.LogWarning(
            "Alert creation rate spike detected. Count: {AlertCount}, Rate: {CreationRate}/hr, IdempotencyKey: {IdempotencyKey}",
            alertsInWindow, creationRatePerHour, idempotencyKey);

        // Notify dashboard subscribers of rate spike
        await HubContext.Clients.Group("dashboard").DashboardSummaryUpdated(new
        {
            eventType = "RateSpikeDetected",
            alertsInOneHourWindow = alertsInWindow,
            creationRatePerHour = Math.Round(creationRatePerHour, 2),
            detectionTimestamp = _timeProvider.UtcNow,
            severity = creationRatePerHour > 100 ? "Critical" : "Warning",
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed rate spike detection event");
    }
}

/// <summary>
/// Reaction handler for duplicate suppression suggestions.
/// Triggered by: duplicate-suppression.cypher query (same headline + region within 15 minutes)
/// </summary>
public class DuplicateSuppressionReactionHandler : DrasiReactionHandlerBase
{
    private readonly ICorrelationEventRepository _correlationRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly ITimeProvider _timeProvider;

    public DuplicateSuppressionReactionHandler(
        ICorrelationEventRepository correlationRepository,
        IIdGenerator idGenerator,
        ITimeProvider timeProvider,
        ILogger<DuplicateSuppressionReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _correlationRepository = correlationRepository;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        Guid alertId,
        Guid duplicateAlertId,
        string headline,
        string regionCode,
        CancellationToken cancellationToken = default)
    {
        var eventId = _idGenerator.NewId();
        var idempotencyKey = GenerateEventIdempotencyKey("duplicate-suppression", eventId);

        Logger.LogWarning(
            "Duplicate suppression suggested. AlertId: {AlertId}, DuplicateAlertId: {DuplicateAlertId}, Region: {Region}, IdempotencyKey: {IdempotencyKey}",
            alertId, duplicateAlertId, regionCode, idempotencyKey);

        var metadata = JsonSerializer.Serialize(new
        {
            headline,
            regionCode,
            duplicateAlertId,
            windowMinutes = 15,
            idempotencyKey
        });

        var correlationEvent = EmergencyAlerts.Domain.Entities.CorrelationEvent.Create(
            patternType: EmergencyAlerts.Domain.Entities.PatternType.DuplicateSuppression,
            alertIds: new[] { alertId, duplicateAlertId },
            timeProvider: _timeProvider,
            idGenerator: _idGenerator,
            regionCode: regionCode,
            metadata: metadata);

        await _correlationRepository.AddAsync(correlationEvent, cancellationToken);
        await _correlationRepository.SaveChangesAsync(cancellationToken);

        await HubContext.Clients.Group("dashboard").CorrelationEventDetected(new
        {
            eventId = correlationEvent.EventId,
            patternType = "DuplicateSuppression",
            alertIds = new[] { alertId, duplicateAlertId },
            headline,
            regionCode,
            detectionTimestamp = _timeProvider.UtcNow,
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed duplicate suppression event: {EventId}", eventId);
    }
}

/// <summary>
/// Reaction handler for area expansion suggestions.
/// Triggered by: area-expansion-suggestion.cypher query (same headline across regions)
/// </summary>
public class AreaExpansionSuggestionReactionHandler : DrasiReactionHandlerBase
{
    private readonly ICorrelationEventRepository _correlationRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly ITimeProvider _timeProvider;

    public AreaExpansionSuggestionReactionHandler(
        ICorrelationEventRepository correlationRepository,
        IIdGenerator idGenerator,
        ITimeProvider timeProvider,
        ILogger<AreaExpansionSuggestionReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _correlationRepository = correlationRepository;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        List<Guid> alertIds,
        List<string> regionCodes,
        string headline,
        CancellationToken cancellationToken = default)
    {
        var distinctAlertIds = alertIds.Distinct().ToList();
        if (distinctAlertIds.Count < 2)
        {
            Logger.LogWarning("Area expansion suggestion received with insufficient alerts");
            return;
        }

        var eventId = _idGenerator.NewId();
        var idempotencyKey = GenerateEventIdempotencyKey("area-expansion-suggestion", eventId);

        Logger.LogWarning(
            "Area expansion suggested. Alerts: {AlertCount}, Regions: {RegionCount}, IdempotencyKey: {IdempotencyKey}",
            distinctAlertIds.Count, regionCodes.Count, idempotencyKey);

        var metadata = JsonSerializer.Serialize(new
        {
            headline,
            regionCodes,
            idempotencyKey
        });

        var correlationEvent = EmergencyAlerts.Domain.Entities.CorrelationEvent.Create(
            patternType: EmergencyAlerts.Domain.Entities.PatternType.AreaExpansionSuggestion,
            alertIds: distinctAlertIds,
            timeProvider: _timeProvider,
            idGenerator: _idGenerator,
            metadata: metadata);

        await _correlationRepository.AddAsync(correlationEvent, cancellationToken);
        await _correlationRepository.SaveChangesAsync(cancellationToken);

        await HubContext.Clients.Group("dashboard").CorrelationEventDetected(new
        {
            eventId = correlationEvent.EventId,
            patternType = "AreaExpansionSuggestion",
            alertIds = distinctAlertIds,
            headline,
            regionCodes,
            detectionTimestamp = _timeProvider.UtcNow,
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed area expansion suggestion event: {EventId}", eventId);
    }
}

/// <summary>
/// Reaction handler for SLA countdown updates.
/// Triggered by: sla-countdown.cypher query (alerts approaching SLA breach)
/// Showcases Drasi's predictive capabilities with live countdown.
/// </summary>
public class SLACountdownReactionHandler : DrasiReactionHandlerBase
{
    private readonly ITimeProvider _timeProvider;

    public SLACountdownReactionHandler(
        ITimeProvider timeProvider,
        ILogger<SLACountdownReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        Guid alertId,
        string headline,
        string severity,
        int secondsElapsed,
        int secondsRemaining,
        DateTime breachAt,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = GenerateIdempotencyKey("sla-countdown", alertId, _timeProvider.UtcNow);

        Logger.LogInformation(
            "SLA countdown update. AlertId: {AlertId}, SecondsRemaining: {SecondsRemaining}, BreachAt: {BreachAt}",
            alertId, secondsRemaining, breachAt);

        // Push countdown update to dashboard for live visualization
        await HubContext.Clients.Group("dashboard").SLACountdownUpdate(new
        {
            alertId,
            headline,
            severity,
            secondsElapsed,
            secondsRemaining,
            breachAt,
            timestamp = _timeProvider.UtcNow,
            idempotencyKey
        });
    }
}

/// <summary>
/// Reaction handler for all-clear suggestions.
/// Triggered by: all-clear-suggestion.cypher query (30 minutes after delivery)
/// </summary>
public class AllClearSuggestionReactionHandler : DrasiReactionHandlerBase
{
    private readonly IAlertRepository _alertRepository;
    private readonly ITimeProvider _timeProvider;

    public AllClearSuggestionReactionHandler(
        IAlertRepository alertRepository,
        ITimeProvider timeProvider,
        ILogger<AllClearSuggestionReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _alertRepository = alertRepository;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        Guid alertId,
        string? headline,
        DateTime? deliveredAt,
        DateTime? suggestedAt,
        CancellationToken cancellationToken = default)
    {
        var windowStart = deliveredAt ?? _timeProvider.UtcNow;
        var idempotencyKey = GenerateIdempotencyKey("all-clear-suggestion", alertId, windowStart);

        Logger.LogInformation(
            "All-clear suggestion triggered. AlertId: {AlertId}, IdempotencyKey: {IdempotencyKey}",
            alertId, idempotencyKey);

        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert == null)
        {
            Logger.LogWarning("Alert not found for all-clear suggestion: {AlertId}", alertId);
            return;
        }

        var resolvedHeadline = string.IsNullOrWhiteSpace(headline) ? alert.Headline : headline;
        var suggestionTimestamp = suggestedAt ?? _timeProvider.UtcNow;

        await HubContext.Clients.Group("dashboard").AlertStatusChanged(new
        {
            alertId = alert.AlertId,
            headline = resolvedHeadline,
            status = "AllClearSuggested",
            deliveredAt = deliveredAt ?? alert.UpdatedAt,
            suggestedAt = suggestionTimestamp,
            idempotencyKey
        });

        Logger.LogInformation("Successfully processed all-clear suggestion for alert: {AlertId}", alertId);
    }
}

/// <summary>
/// Reaction handler for delivery retry storm detection.
/// Triggered by: delivery-retry-storm.cypher query (3+ failed delivery attempts)
/// Showcases Drasi's cross-entity aggregation capabilities.
/// </summary>
public class DeliveryRetryStormReactionHandler : DrasiReactionHandlerBase
{
    private readonly ITimeProvider _timeProvider;

    public DeliveryRetryStormReactionHandler(
        ITimeProvider timeProvider,
        ILogger<DeliveryRetryStormReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        Guid alertId,
        string headline,
        string severity,
        int failedAttemptCount,
        string? lastFailureReason,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = GenerateIdempotencyKey("delivery-retry-storm", alertId, _timeProvider.UtcNow);

        Logger.LogWarning(
            "Delivery retry storm detected. AlertId: {AlertId}, FailedAttempts: {FailedAttempts}, LastReason: {LastReason}",
            alertId, failedAttemptCount, lastFailureReason ?? "unknown");

        await HubContext.Clients.Group("dashboard").DeliveryRetryStormDetected(new
        {
            alertId,
            headline,
            severity,
            failedAttemptCount,
            lastFailureReason = lastFailureReason ?? "Unknown error",
            detectionTimestamp = _timeProvider.UtcNow,
            idempotencyKey,
        });

        Logger.LogInformation("Successfully processed delivery retry storm for alert: {AlertId}", alertId);
    }
}

/// <summary>
/// Reaction handler for approver workload monitoring.
/// Triggered by: approver-workload-monitor.cypher query (5+ decisions in 1 hour)
/// Showcases Drasi's aggregation and threshold detection capabilities.
/// </summary>
public class ApproverWorkloadReactionHandler : DrasiReactionHandlerBase
{
    private readonly ITimeProvider _timeProvider;

    public ApproverWorkloadReactionHandler(
        ITimeProvider timeProvider,
        ILogger<ApproverWorkloadReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        string approverId,
        int decisionsInHour,
        int approvedCount,
        int rejectedCount,
        string workloadLevel,
        CancellationToken cancellationToken = default)
    {
        var windowStart = _timeProvider.UtcNow.AddHours(-1);
        var idempotencyKey = $"approver-workload:{approverId}:{windowStart:yyyyMMddHHmm}";

        Logger.LogWarning(
            "Approver workload alert. ApproverId: {ApproverId}, Decisions: {Decisions}, Level: {Level}",
            approverId, decisionsInHour, workloadLevel);

        await HubContext.Clients.Group("dashboard").ApproverWorkloadAlert(new
        {
            approverId,
            decisionsInHour,
            approvedCount,
            rejectedCount,
            workloadLevel,
            detectionTimestamp = _timeProvider.UtcNow,
            idempotencyKey,
        });

        Logger.LogInformation("Successfully processed approver workload alert for: {ApproverId}", approverId);
    }
}

/// <summary>
/// Reaction handler for delivery success rate monitoring.
/// Triggered by: delivery-success-rate.cypher query (success rate < 80%)
/// Showcases Drasi's real-time analytics capabilities.
/// </summary>
public class DeliverySuccessRateReactionHandler : DrasiReactionHandlerBase
{
    private readonly ITimeProvider _timeProvider;

    public DeliverySuccessRateReactionHandler(
        ITimeProvider timeProvider,
        ILogger<DeliverySuccessRateReactionHandler> logger,
        IHubContext<AlertHub, IAlertHubClient> hubContext)
        : base(logger, hubContext)
    {
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        int totalAttempts,
        int successCount,
        int failedCount,
        double successRatePercent,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = $"delivery-success-rate-{_timeProvider.UtcNow:yyyyMMddHH}";

        Logger.LogWarning(
            "Delivery success rate degraded. Total: {Total}, Success: {Success}, Failed: {Failed}, Rate: {Rate:F1}%",
            totalAttempts, successCount, failedCount, successRatePercent);

        await HubContext.Clients.Group("dashboard").DeliverySuccessRateDegraded(new
        {
            totalAttempts,
            successCount,
            failedCount,
            successRatePercent = Math.Round(successRatePercent, 1),
            detectionTimestamp = _timeProvider.UtcNow,
            idempotencyKey,
        });

        Logger.LogInformation("Successfully processed delivery success rate alert: {Rate:F1}%", successRatePercent);
    }
}
