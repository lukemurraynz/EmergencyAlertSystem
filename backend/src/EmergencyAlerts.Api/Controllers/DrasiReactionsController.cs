using System.Collections.Generic;
using EmergencyAlerts.Api.ReactionHandlers;
using EmergencyAlerts.Api.Services;
using EmergencyAlerts.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EmergencyAlerts.Api.Controllers;

[ApiController]
[Route("api/v1/drasi/reactions")]
public class DrasiReactionsController : ControllerBase
{
    private readonly DeliveryTriggerReactionHandler _deliveryTriggerReactionHandler;
    private readonly SLABreachReactionHandler _slaBreachReactionHandler;
    private readonly ApprovalTimeoutReactionHandler _approvalTimeoutReactionHandler;
    private readonly GeographicCorrelationReactionHandler _geographicCorrelationReactionHandler;
    private readonly RegionalHotspotReactionHandler _regionalHotspotReactionHandler;
    private readonly SeverityEscalationReactionHandler _severityEscalationReactionHandler;
    private readonly DuplicateSuppressionReactionHandler _duplicateSuppressionReactionHandler;
    private readonly AreaExpansionSuggestionReactionHandler _areaExpansionSuggestionReactionHandler;
    private readonly AllClearSuggestionReactionHandler _allClearSuggestionReactionHandler;
    private readonly ExpiryWarningReactionHandler _expiryWarningReactionHandler;
    private readonly RateSpikeDetectionReactionHandler _rateSpikeDetectionReactionHandler;
    private readonly SLACountdownReactionHandler _slaCountdownReactionHandler;
    private readonly DeliveryRetryStormReactionHandler _deliveryRetryStormReactionHandler;
    private readonly ApproverWorkloadReactionHandler _approverWorkloadReactionHandler;
    private readonly DeliverySuccessRateReactionHandler _deliverySuccessRateReactionHandler;
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<DrasiReactionsController> _logger;
    private readonly IDrasiReactionAuthenticator _reactionAuthenticator;

    public DrasiReactionsController(
        DeliveryTriggerReactionHandler deliveryTriggerReactionHandler,
        SLABreachReactionHandler slaBreachReactionHandler,
        ApprovalTimeoutReactionHandler approvalTimeoutReactionHandler,
        GeographicCorrelationReactionHandler geographicCorrelationReactionHandler,
        RegionalHotspotReactionHandler regionalHotspotReactionHandler,
        SeverityEscalationReactionHandler severityEscalationReactionHandler,
        DuplicateSuppressionReactionHandler duplicateSuppressionReactionHandler,
        AreaExpansionSuggestionReactionHandler areaExpansionSuggestionReactionHandler,
        AllClearSuggestionReactionHandler allClearSuggestionReactionHandler,
        ExpiryWarningReactionHandler expiryWarningReactionHandler,
        RateSpikeDetectionReactionHandler rateSpikeDetectionReactionHandler,
        SLACountdownReactionHandler slaCountdownReactionHandler,
        DeliveryRetryStormReactionHandler deliveryRetryStormReactionHandler,
        ApproverWorkloadReactionHandler approverWorkloadReactionHandler,
        DeliverySuccessRateReactionHandler deliverySuccessRateReactionHandler,
        IAlertRepository alertRepository,
        ILogger<DrasiReactionsController> logger,
        IDrasiReactionAuthenticator reactionAuthenticator)
    {
        _deliveryTriggerReactionHandler = deliveryTriggerReactionHandler;
        _slaBreachReactionHandler = slaBreachReactionHandler;
        _approvalTimeoutReactionHandler = approvalTimeoutReactionHandler;
        _geographicCorrelationReactionHandler = geographicCorrelationReactionHandler;
        _regionalHotspotReactionHandler = regionalHotspotReactionHandler;
        _severityEscalationReactionHandler = severityEscalationReactionHandler;
        _duplicateSuppressionReactionHandler = duplicateSuppressionReactionHandler;
        _areaExpansionSuggestionReactionHandler = areaExpansionSuggestionReactionHandler;
        _allClearSuggestionReactionHandler = allClearSuggestionReactionHandler;
        _expiryWarningReactionHandler = expiryWarningReactionHandler;
        _rateSpikeDetectionReactionHandler = rateSpikeDetectionReactionHandler;
        _slaCountdownReactionHandler = slaCountdownReactionHandler;
        _deliveryRetryStormReactionHandler = deliveryRetryStormReactionHandler;
        _approverWorkloadReactionHandler = approverWorkloadReactionHandler;
        _deliverySuccessRateReactionHandler = deliverySuccessRateReactionHandler;
        _alertRepository = alertRepository;
        _logger = logger;
        _reactionAuthenticator = reactionAuthenticator;
    }

    [HttpPost("delivery-trigger")]
    public async Task<IActionResult> DeliveryTrigger(
        [FromBody] DeliveryTriggerReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        await _deliveryTriggerReactionHandler.HandleAsync(request.AlertId, cancellationToken);
        return NoContent();
    }

    [HttpPost("delivery-sla-breach")]
    public async Task<IActionResult> SLABreach(
        [FromBody] SLABreachReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        await _slaBreachReactionHandler.HandleAsync(request.AlertId, request.Headline, request.Severity, request.ElapsedSeconds, cancellationToken);
        return NoContent();
    }

    [HttpPost("approval-timeout")]
    public async Task<IActionResult> ApprovalTimeout(
        [FromBody] ApprovalTimeoutReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        await _approvalTimeoutReactionHandler.HandleAsync(request.AlertId, request.ElapsedMinutes, cancellationToken);
        return NoContent();
    }

    [HttpPost("geographic-correlation")]
    public async Task<IActionResult> GeographicCorrelation(
        [FromBody] GeographicCorrelationReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        if (string.IsNullOrWhiteSpace(request.RegionCode))
        {
            _logger.LogWarning("Geographic correlation reaction received without region code");
            return BadRequest("regionCode is required");
        }

        // Fetch alert IDs for the correlated region
        var alertIds = await _alertRepository.GetAlertIdsByRegionAsync(request.RegionCode, cancellationToken);
        if (alertIds.Count < 2)
        {
            _logger.LogInformation("Geographic correlation for region {RegionCode} has fewer than 2 alerts, skipping", request.RegionCode);
            return NoContent();
        }

        await _geographicCorrelationReactionHandler.HandleAsync(alertIds, clusterSeverity: null, cancellationToken);
        return NoContent();
    }

    [HttpPost("regional-hotspot")]
    public async Task<IActionResult> RegionalHotspot(
        [FromBody] RegionalHotspotReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        var alertIds = await _alertRepository.GetAlertIdsByRegionAsync(request.RegionCode, cancellationToken);
        if (alertIds.Count == 0)
        {
            _logger.LogWarning("Regional hotspot reaction fired but no alerts found for region {RegionCode}", request.RegionCode);
            return NotFound("No alerts found for the requested region");
        }

        await _regionalHotspotReactionHandler.HandleAsync(request.RegionCode, alertIds, cancellationToken);
        return NoContent();
    }

    [HttpPost("severity-escalation")]
    public async Task<IActionResult> SeverityEscalation(
        [FromBody] SeverityEscalationReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        if (request.AlertIds is null || request.AlertIds.Count == 0)
        {
            _logger.LogWarning("Severity escalation reaction received without alert IDs");
            return BadRequest("alertIds must include at least one entry");
        }

        await _severityEscalationReactionHandler.HandleAsync(request.AlertIds, request.FromSeverity, request.ToSeverity, cancellationToken);
        return NoContent();
    }

    [HttpPost("duplicate-suppression")]
    public async Task<IActionResult> DuplicateSuppression(
        [FromBody] DuplicateSuppressionReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        if (string.IsNullOrWhiteSpace(request.RegionCode))
        {
            _logger.LogWarning("Duplicate suppression reaction received without region code");
            return BadRequest("regionCode is required");
        }

        await _duplicateSuppressionReactionHandler.HandleAsync(
            request.AlertId,
            request.DuplicateAlertId,
            request.Headline,
            request.RegionCode,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("area-expansion-suggestion")]
    public async Task<IActionResult> AreaExpansionSuggestion(
        [FromBody] AreaExpansionSuggestionReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        if (request.AlertIds is null || request.AlertIds.Count < 2)
        {
            _logger.LogWarning("Area expansion reaction received without enough alert IDs");
            return BadRequest("alertIds must include at least two entries");
        }

        if (request.RegionCodes is null || request.RegionCodes.Count < 2)
        {
            _logger.LogWarning("Area expansion reaction received without enough region codes");
            return BadRequest("regionCodes must include at least two entries");
        }

        await _areaExpansionSuggestionReactionHandler.HandleAsync(
            request.AlertIds,
            request.RegionCodes,
            request.Headline,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("all-clear-suggestion")]
    public async Task<IActionResult> AllClearSuggestion(
        [FromBody] AllClearSuggestionReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        await _allClearSuggestionReactionHandler.HandleAsync(
            request.AlertId,
            request.Headline,
            request.DeliveredAt,
            request.SuggestedAt,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("expiry-warning")]
    public async Task<IActionResult> ExpiryWarning(
        [FromBody] ExpiryWarningReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        await _expiryWarningReactionHandler.HandleAsync(request.AlertId, request.ExpiresAt, cancellationToken);
        return NoContent();
    }

    [HttpPost("rate-spike-detection")]
    public async Task<IActionResult> RateSpikeDetection(
        [FromBody] RateSpikeDetectionReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        await _rateSpikeDetectionReactionHandler.HandleAsync(request.AlertsInWindow, request.CreationRatePerHour, cancellationToken);
        return NoContent();
    }

    [HttpPost("sla-countdown")]
    public async Task<IActionResult> SLACountdown(
        [FromBody] SLACountdownReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        await _slaCountdownReactionHandler.HandleAsync(
            request.AlertId,
            request.Headline,
            request.Severity,
            request.SecondsElapsed,
            request.SecondsRemaining,
            request.BreachAt,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("delivery-retry-storm")]
    public async Task<IActionResult> DeliveryRetryStorm(
        [FromBody] DeliveryRetryStormReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        await _deliveryRetryStormReactionHandler.HandleAsync(
            request.AlertId,
            request.Headline,
            request.Severity,
            request.FailedAttemptCount,
            request.LastFailureReason,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("approver-workload")]
    public async Task<IActionResult> ApproverWorkload(
        [FromBody] ApproverWorkloadReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        await _approverWorkloadReactionHandler.HandleAsync(
            request.ApproverId,
            request.DecisionsInHour,
            request.ApprovedCount,
            request.RejectedCount,
            request.WorkloadLevel,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("delivery-success-rate")]
    public async Task<IActionResult> DeliverySuccessRate(
        [FromBody] DeliverySuccessRateReactionRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = AuthorizeRequest();
        if (authResult is not null)
        {
            return authResult;
        }

        await _deliverySuccessRateReactionHandler.HandleAsync(
            request.TotalAttempts,
            request.SuccessCount,
            request.FailedCount,
            request.SuccessRatePercent,
            cancellationToken);
        return NoContent();
    }

    #region DTOs

    public record DeliveryTriggerReactionRequest(Guid AlertId);

    public record SLABreachReactionRequest(Guid AlertId, string? Headline, string? Severity, int ElapsedSeconds);

    public record ApprovalTimeoutReactionRequest(Guid AlertId, int ElapsedMinutes);

    public record GeographicCorrelationReactionRequest(string RegionCode, int AlertCount);

    public record RegionalHotspotReactionRequest(string RegionCode, int AlertCount);

    public record SeverityEscalationReactionRequest(List<Guid> AlertIds, string FromSeverity, string ToSeverity);

    public record DuplicateSuppressionReactionRequest(Guid AlertId, Guid DuplicateAlertId, string Headline, string RegionCode);

    public record AreaExpansionSuggestionReactionRequest(List<Guid> AlertIds, List<string> RegionCodes, string Headline);

    public record AllClearSuggestionReactionRequest(Guid AlertId, string? Headline, DateTime? DeliveredAt, DateTime? SuggestedAt);

    public record ExpiryWarningReactionRequest(Guid AlertId, DateTime ExpiresAt);

    public record RateSpikeDetectionReactionRequest(int AlertsInWindow, double CreationRatePerHour);

    public record SLACountdownReactionRequest(Guid AlertId, string Headline, string Severity, int SecondsElapsed, int SecondsRemaining, DateTime BreachAt);

    public record DeliveryRetryStormReactionRequest(Guid AlertId, string Headline, string Severity, int FailedAttemptCount, string? LastFailureReason);

    public record ApproverWorkloadReactionRequest(string ApproverId, int DecisionsInHour, int ApprovedCount, int RejectedCount, string WorkloadLevel);

    public record DeliverySuccessRateReactionRequest(int TotalAttempts, int SuccessCount, int FailedCount, double SuccessRatePercent);

    #endregion

    private IActionResult? AuthorizeRequest()
    {
        if (_reactionAuthenticator.Validate(Request, out var reason))
        {
            return null;
        }

        _logger.LogWarning("Unauthorized Drasi reaction call to {Path}. Reason: {Reason}", Request.Path, reason);
        return Unauthorized();
    }
}
