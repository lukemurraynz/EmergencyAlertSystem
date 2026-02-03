using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EmergencyAlerts.Application.Commands;
using EmergencyAlerts.Application.Ports;
using EmergencyAlerts.Application.Dtos;
using EmergencyAlerts.Api.ReactionHandlers;
using EmergencyAlerts.Domain.Entities;
using EmergencyAlerts.Domain.Repositories;
using EmergencyAlerts.Domain.Services;
using System.Text.Json;

namespace EmergencyAlerts.Api.Controllers;

/// <summary>
/// Demo controller for generating sample alerts to showcase Drasi decisioning.
/// Generates realistic emergency scenarios across New Zealand regions to trigger:
/// - Regional hotspot detection
/// - Severity escalation patterns
/// - Rate spike detection
/// - SLA breach scenarios
/// - Delivery retry storm detection
/// - Approver workload monitoring
/// - Delivery success rate tracking
/// </summary>
[ApiController]
[Route("api/v1/demo")]
[Produces("application/json")]
public class DemoController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly IAuthService _authService;
    private readonly IAlertRepository _alertRepository;
    private readonly IDeliveryAttemptRepository _deliveryAttemptRepository;
    private readonly IRecipientRepository _recipientRepository;
    private readonly ITimeProvider _timeProvider;
    private readonly IIdGenerator _idGenerator;
    private readonly ILogger<DemoController> _logger;
    private readonly RegionalHotspotReactionHandler _regionalHotspotReactionHandler;
    private readonly GeographicCorrelationReactionHandler _geographicCorrelationReactionHandler;
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
    private readonly ApprovalTimeoutReactionHandler _approvalTimeoutReactionHandler;
    private readonly SLABreachReactionHandler _slaBreachReactionHandler;

    private static readonly Dictionary<string, (double Lat, double Lon, string RegionCode)> NewZealandRegions = new()
    {
        { "Auckland", (-36.8485, 174.7633, "NZ-AKL") },
        { "Wellington", (-41.2865, 174.7762, "NZ-WGN") },
        { "Christchurch", (-43.5320, 172.6306, "NZ-CHC") },
        { "Hamilton", (-37.7870, 175.2793, "NZ-HLZ") },
        { "Tauranga", (-37.6878, 176.1651, "NZ-TRG") }
    };

    private static readonly string[] DemoHeadlines = new[]
    {
        "Severe Weather Warning",
        "Traffic Incident Alert",
        "Public Health Notice",
        "Emergency Service Update",
        "Infrastructure Alert",
        "Utility Disruption Notice",
        "Community Safety Warning",
        "Transport Disruption"
    };

    private static readonly string[] DemoDescriptions = new[]
    {
        "A significant event has been detected affecting the region. Residents are advised to take precautionary measures.",
        "Emergency services are responding to an incident in the area. Updates to follow.",
        "A critical situation requires immediate public awareness. Please follow official guidance.",
        "Multiple reports received in quick succession. Authorities are coordinating response.",
        "System detected pattern of incidents across multiple areas. Regional coordination activated.",
        "High-priority incident with potential for escalation. Response teams mobilized.",
        "Alert threshold exceeded. Additional resources being deployed.",
        "Duplicate incident patterns detected. Enhanced monitoring in effect."
    };

    private static readonly string[] DemoApproverIds = new[]
    {
        "approver-alice@demo.nz",
        "approver-bob@demo.nz",
        "approver-carol@demo.nz"
    };

    private static readonly string[] DeliveryFailureReasons = new[]
    {
        "Connection timeout",
        "Recipient unavailable",
        "Rate limit exceeded",
        "Invalid phone number format",
        "Carrier rejected message",
        "Network congestion"
    };

    public DemoController(
        IAlertService alertService,
        IAuthService authService,
        IAlertRepository alertRepository,
        IDeliveryAttemptRepository deliveryAttemptRepository,
        IRecipientRepository recipientRepository,
        ITimeProvider timeProvider,
        IIdGenerator idGenerator,
        ILogger<DemoController> logger,
        RegionalHotspotReactionHandler regionalHotspotReactionHandler,
        GeographicCorrelationReactionHandler geographicCorrelationReactionHandler,
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
        ApprovalTimeoutReactionHandler approvalTimeoutReactionHandler,
        SLABreachReactionHandler slaBreachReactionHandler)
    {
        _alertService = alertService;
        _authService = authService;
        _alertRepository = alertRepository;
        _deliveryAttemptRepository = deliveryAttemptRepository;
        _recipientRepository = recipientRepository;
        _timeProvider = timeProvider;
        _idGenerator = idGenerator;
        _logger = logger;
        _regionalHotspotReactionHandler = regionalHotspotReactionHandler;
        _geographicCorrelationReactionHandler = geographicCorrelationReactionHandler;
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
        _approvalTimeoutReactionHandler = approvalTimeoutReactionHandler;
        _slaBreachReactionHandler = slaBreachReactionHandler;
    }

    /// <summary>
    /// Generates realistic demo alerts across New Zealand regions to showcase Drasi decisioning.
    /// Triggers regional hotspots, severity escalation, rate spikes, and SLA breach scenarios.
    /// </summary>
    /// <param name="count">Number of demo alerts to generate (default: 10, max: 50)</param>
    /// <param name="delay">Delay in milliseconds between alert creations (default: 500ms, enables rate spike demo)</param>
    /// <param name="autoApprove">Auto-approve alerts to trigger SLA breach detection (default: true)</param>
    /// <response code="200">Demo alerts generated successfully</response>
    /// <response code="400">Invalid parameters</response>
    /// <response code="403">User does not have operator role</response>
    [AllowAnonymous]
    [HttpPost("generate-alerts")]
    [ProducesResponseType(typeof(GenerateAlertsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateAlerts(
        [FromQuery] int count = 10,
        [FromQuery] int delay = 500,
        [FromQuery] bool autoApprove = true,
        CancellationToken cancellationToken = default)
    {
        // Validate parameters
        if (count < 1 || count > 50)
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid count parameter",
                Detail = "Count must be between 1 and 50",
                Status = StatusCodes.Status400BadRequest
            });

        if (delay < 0 || delay > 5000)
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid delay parameter",
                Detail = "Delay must be between 0 and 5000 milliseconds",
                Status = StatusCodes.Status400BadRequest
            });

        try
        {
            // Skip operator role validation for demo endpoint (anonymous access)
            // Demo alerts are non-destructive and for testing/showcase only
            string userId;
            try
            {
                var user = await _authService.GetCurrentUserAsync(cancellationToken);
                userId = user.UserId;
            }
            catch
            {
                // Fall back to demo user if auth unavailable
                userId = "demo-user";
            }

            _logger.LogInformation("Starting demo alert generation: count={Count}, delay={DelayMs}ms, userId={UserId}", count, delay, userId);

            var generatedIds = new List<string>();
            var random = new Random();
            var now = DateTime.UtcNow;

            // Generate alerts with varied patterns
            for (int i = 0; i < count; i++)
            {
                try
                {
                    // Select scenario: regional hotspot (cluster in same region), severity escalation, or rate spike
                    var scenario = i % 3;
                    string region;
                    var severity = SelectSeverity(scenario, i, count, random);

                    if (scenario == 0)
                    {
                        // Regional hotspot: cluster alerts in Auckland
                        region = "Auckland";
                    }
                    else if (scenario == 1)
                    {
                        // Severity escalation: Wellington alerts with escalating severity
                        region = "Wellington";
                    }
                    else
                    {
                        // Rate spike: varied regions
                        region = NewZealandRegions.Keys.ElementAt(random.Next(NewZealandRegions.Count));
                    }

                    var (lat, lon, regionCode) = NewZealandRegions[region];
                    var geoJsonPolygon = GenerateGeoJsonPolygon(lat, lon, region);

                    var createDto = new CreateAlertDto
                    {
                        Headline = $"{DemoHeadlines[random.Next(DemoHeadlines.Length)]} - {region}",
                        Description = DemoDescriptions[random.Next(DemoDescriptions.Length)],
                        Severity = severity,
                        ChannelType = "SMS",
                        LanguageCode = "en-GB",
                        ExpiresAt = now.AddMinutes(30),
                        Areas = new List<AreaDto>
                        {
                            new AreaDto
                            {
                                AreaDescription = $"{region} area - Demo scenario",
                                GeoJsonPolygon = geoJsonPolygon,
                                RegionCode = regionCode
                            }
                        }
                    };

                    var command = new CreateAlertCommand(createDto, userId);
                    var result = await _alertService.CreateAlertAsync(command, cancellationToken);
                    generatedIds.Add(result.AlertId.ToString());

                    _logger.LogInformation(
                        "Generated demo alert {Num}/{Total}: {AlertId} in {Region} with severity {Severity}",
                        i + 1, count, result.AlertId, region, severity);

                    // Apply delay between creations to allow Drasi queries to process
                    if (i < count - 1 && delay > 0)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating demo alert {Num}/{Total}", i + 1, count);
                    // Continue with next alert on error
                }
            }

            _logger.LogInformation("Demo alert generation completed: {GeneratedCount} alerts", generatedIds.Count);

            return Ok(new GenerateAlertsResponse
            {
                Success = true,
                Count = generatedIds.Count,
                AlertIds = generatedIds,
                Message = $"Successfully generated {generatedIds.Count} demo alerts. Watch the dashboard for Drasi decisioning:",
                ExpectedDecisions = new[]
                {
                        "🔴 Regional Hotspot - Auckland cluster (New Zealand)",
                        "📈 Severity Escalation - Wellington pattern (New Zealand)",
                        "⚡ Rate Spike - Rapid alert creation detected"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate demo alerts");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Demo generation failed",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    /// <summary>
    /// Simulates delivery failures to trigger delivery retry storm detection.
    /// Creates multiple failed delivery attempts for specified alerts.
    /// </summary>
    /// <param name="alertIds">Alert IDs to create failures for (comma-separated). If empty, uses recent pending alerts.</param>
    /// <param name="failuresPerAlert">Number of failed attempts per alert (default: 4, triggers storm at 3+)</param>
    /// <response code="200">Delivery failures simulated successfully</response>
    [AllowAnonymous]
    [HttpPost("simulate-delivery-failures")]
    [ProducesResponseType(typeof(SimulateDeliveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SimulateDeliveryFailures(
        [FromQuery] string? alertIds = null,
        [FromQuery] int failuresPerAlert = 4,
        CancellationToken cancellationToken = default)
    {
        if (failuresPerAlert < 1 || failuresPerAlert > 10)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid failuresPerAlert parameter",
                Detail = "Must be between 1 and 10",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            _logger.LogInformation("Starting delivery failure simulation: failuresPerAlert={FailuresPerAlert}", failuresPerAlert);

            var targetAlertIds = new List<Guid>();

            if (!string.IsNullOrWhiteSpace(alertIds))
            {
                foreach (var id in alertIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Guid.TryParse(id.Trim(), out var guid))
                    {
                        targetAlertIds.Add(guid);
                    }
                }
            }

            // If no IDs provided, get recent pending alerts
            if (targetAlertIds.Count == 0)
            {
                var pendingAlerts = await _alertRepository.GetByStatusAsync(AlertStatus.PendingApproval, cancellationToken);
                targetAlertIds.AddRange(pendingAlerts.Take(5).Select(a => a.AlertId));
            }

            if (targetAlertIds.Count == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "No alerts found",
                    Detail = "No alerts available to simulate failures. Generate some alerts first.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Ensure we have a demo recipient
            var demoRecipient = await GetOrCreateDemoRecipientAsync(cancellationToken);
            var random = new Random();
            var createdAttempts = new List<string>();

            foreach (var alertId in targetAlertIds)
            {
                for (int attemptNum = 1; attemptNum <= Math.Min(failuresPerAlert, 3); attemptNum++)
                {
                    var attempt = DeliveryAttempt.Create(
                        alertId,
                        demoRecipient.RecipientId,
                        _timeProvider,
                        _idGenerator,
                        attemptNum);

                    attempt.MarkFailure(DeliveryFailureReasons[random.Next(DeliveryFailureReasons.Length)]);
                    await _deliveryAttemptRepository.AddAsync(attempt, cancellationToken);
                    createdAttempts.Add($"{alertId}:attempt{attemptNum}");
                }

                await _deliveryAttemptRepository.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Created {Count} failed delivery attempts for alert {AlertId}", Math.Min(failuresPerAlert, 3), alertId);
            }

            return Ok(new SimulateDeliveryResponse
            {
                Success = true,
                AlertsAffected = targetAlertIds.Count,
                AttemptsCreated = createdAttempts.Count,
                Message = $"Created {createdAttempts.Count} failed delivery attempts across {targetAlertIds.Count} alerts.",
                ExpectedDecision = "🔄 Delivery retry storm detected - Alerts with 3+ failed attempts will trigger storm alerts"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate delivery failures");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Simulation failed",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    /// <summary>
    /// Simulates approver workload to trigger approver workload monitoring.
    /// Rapidly approves multiple alerts under a single approver ID.
    /// </summary>
    /// <param name="approverId">Approver ID to use (default: first demo approver)</param>
    /// <param name="decisionCount">Number of approval decisions to simulate (default: 6, triggers alert at 5+)</param>
    /// <response code="200">Approver workload simulated successfully</response>
    [AllowAnonymous]
    [HttpPost("simulate-approver-workload")]
    [ProducesResponseType(typeof(SimulateApproverWorkloadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SimulateApproverWorkload(
        [FromQuery] string? approverId = null,
        [FromQuery] int decisionCount = 6,
        CancellationToken cancellationToken = default)
    {
        if (decisionCount < 1 || decisionCount > 20)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid decisionCount parameter",
                Detail = "Must be between 1 and 20",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            var targetApproverId = approverId ?? DemoApproverIds[0];
            _logger.LogInformation("Starting approver workload simulation: approverId={ApproverId}, decisionCount={DecisionCount}",
                targetApproverId, decisionCount);

            // Get pending alerts to approve
            var pendingAlerts = (await _alertRepository.GetByStatusAsync(AlertStatus.PendingApproval, cancellationToken)).ToList();

            if (pendingAlerts.Count < decisionCount)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Not enough pending alerts",
                    Detail = $"Need {decisionCount} pending alerts but only {pendingAlerts.Count} available. Generate more alerts first.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var approvedCount = 0;
            var rejectedCount = 0;
            var random = new Random();

            for (int i = 0; i < decisionCount && i < pendingAlerts.Count; i++)
            {
                var alert = pendingAlerts[i];

                // Mix of approvals and rejections (80% approve, 20% reject)
                if (random.Next(100) < 80)
                {
                    var approveCommand = new ApproveAlertCommand(alert.AlertId, targetApproverId, null);
                    await _alertService.ApproveAlertAsync(approveCommand, cancellationToken);
                    approvedCount++;
                }
                else
                {
                    var rejectCommand = new RejectAlertCommand(alert.AlertId, targetApproverId, "Demo rejection for workload test", null);
                    await _alertService.RejectAlertAsync(rejectCommand, cancellationToken);
                    rejectedCount++;
                }

                _logger.LogInformation("Simulated decision {Num}/{Total} for alert {AlertId} by {ApproverId}",
                    i + 1, decisionCount, alert.AlertId, targetApproverId);

                // Small delay between decisions
                await Task.Delay(100, cancellationToken);
            }

            return Ok(new SimulateApproverWorkloadResponse
            {
                Success = true,
                ApproverId = targetApproverId,
                ApprovedCount = approvedCount,
                RejectedCount = rejectedCount,
                TotalDecisions = approvedCount + rejectedCount,
                Message = $"Approver '{targetApproverId}' made {approvedCount + rejectedCount} decisions ({approvedCount} approved, {rejectedCount} rejected).",
                ExpectedDecision = "👤 Approver workload imbalance - Triggers when approver makes 5+ decisions in 1 hour"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate approver workload");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Simulation failed",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    /// <summary>
    /// Simulates mixed delivery outcomes to trigger delivery success rate monitoring.
    /// Creates a mix of successful and failed delivery attempts.
    /// </summary>
    /// <param name="alertIds">Alert IDs to process (comma-separated). If empty, uses recent alerts.</param>
    /// <param name="successRate">Target success rate percentage (default: 60, below 80% triggers alert)</param>
    /// <response code="200">Delivery outcomes simulated successfully</response>
    [AllowAnonymous]
    [HttpPost("simulate-delivery-outcomes")]
    [ProducesResponseType(typeof(SimulateDeliveryOutcomesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SimulateDeliveryOutcomes(
        [FromQuery] string? alertIds = null,
        [FromQuery] int successRate = 60,
        CancellationToken cancellationToken = default)
    {
        if (successRate < 0 || successRate > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid successRate parameter",
                Detail = "Must be between 0 and 100",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            _logger.LogInformation("Starting delivery outcomes simulation: targetSuccessRate={SuccessRate}%", successRate);

            var targetAlertIds = new List<Guid>();

            if (!string.IsNullOrWhiteSpace(alertIds))
            {
                foreach (var id in alertIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Guid.TryParse(id.Trim(), out var guid))
                    {
                        targetAlertIds.Add(guid);
                    }
                }
            }

            // If no IDs provided, get recent alerts (any status)
            if (targetAlertIds.Count == 0)
            {
                var allAlerts = await _alertRepository.GetAllAsync(cancellationToken);
                targetAlertIds.AddRange(allAlerts.Take(10).Select(a => a.AlertId));
            }

            if (targetAlertIds.Count == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "No alerts found",
                    Detail = "No alerts available. Generate some alerts first.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var demoRecipient = await GetOrCreateDemoRecipientAsync(cancellationToken);
            var random = new Random();
            var successCount = 0;
            var failureCount = 0;

            foreach (var alertId in targetAlertIds)
            {
                var attempt = DeliveryAttempt.Create(
                    alertId,
                    demoRecipient.RecipientId,
                    _timeProvider,
                    _idGenerator,
                    1);

                // Apply target success rate
                if (random.Next(100) < successRate)
                {
                    attempt.MarkSuccess($"demo-acs-op-{_idGenerator.NewId()}");
                    successCount++;
                }
                else
                {
                    attempt.MarkFailure(DeliveryFailureReasons[random.Next(DeliveryFailureReasons.Length)]);
                    failureCount++;
                }

                await _deliveryAttemptRepository.AddAsync(attempt, cancellationToken);
            }

            await _deliveryAttemptRepository.SaveChangesAsync(cancellationToken);

            var actualRate = targetAlertIds.Count > 0 ? (successCount * 100) / targetAlertIds.Count : 0;

            return Ok(new SimulateDeliveryOutcomesResponse
            {
                Success = true,
                AlertsProcessed = targetAlertIds.Count,
                SuccessfulDeliveries = successCount,
                FailedDeliveries = failureCount,
                ActualSuccessRate = actualRate,
                Message = $"Created {targetAlertIds.Count} delivery attempts: {successCount} successful, {failureCount} failed ({actualRate}% success rate).",
                ExpectedDecision = actualRate < 80
                    ? "📉 Delivery success rate degraded - Success rate below 80% triggers system health alert"
                    : "System healthy - Success rate above threshold"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate delivery outcomes");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Simulation failed",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    /// <summary>
    /// Runs a comprehensive demo showcasing all Drasi decisioning features.
    /// Generates alerts, simulates delivery failures, approver workload, and mixed delivery outcomes.
    /// </summary>
    /// <param name="alertCount">Number of demo alerts to generate (default: 12)</param>
    /// <response code="200">Comprehensive demo completed successfully</response>
    [AllowAnonymous]
    [HttpPost("run-comprehensive")]
    [ProducesResponseType(typeof(ComprehensiveDemoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunComprehensiveDemo(
        [FromQuery] int alertCount = 12,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting comprehensive Drasi demo with {AlertCount} alerts", alertCount);

            var results = new List<string>();

            // Step 1: Generate alerts
            var generateResult = await GenerateAlerts(alertCount, 300, false, cancellationToken);
            if (generateResult is OkObjectResult okGenerate && okGenerate.Value is GenerateAlertsResponse genResponse)
            {
                results.Add($"✓ Generated {genResponse.Count} alerts");
            }
            else
            {
                results.Add("⚠ Alert generation had issues");
            }

            await Task.Delay(500, cancellationToken);

            // Step 2: Simulate approver workload (approve 6 alerts)
            var workloadResult = await SimulateApproverWorkload(DemoApproverIds[0], 6, cancellationToken);
            if (workloadResult is OkObjectResult okWorkload && okWorkload.Value is SimulateApproverWorkloadResponse workloadResponse)
            {
                results.Add($"✓ Approver workload: {workloadResponse.TotalDecisions} decisions");
            }
            else
            {
                results.Add("⚠ Approver workload simulation had issues");
            }

            await Task.Delay(500, cancellationToken);

            // Step 3: Simulate delivery failures
            var failureResult = await SimulateDeliveryFailures(null, 4, cancellationToken);
            if (failureResult is OkObjectResult okFailure && okFailure.Value is SimulateDeliveryResponse failureResponse)
            {
                results.Add($"✓ Delivery failures: {failureResponse.AttemptsCreated} failed attempts");
            }
            else
            {
                results.Add("⚠ Delivery failure simulation had issues");
            }

            await Task.Delay(500, cancellationToken);

            // Step 4: Simulate mixed delivery outcomes
            var outcomesResult = await SimulateDeliveryOutcomes(null, 65, cancellationToken);
            if (outcomesResult is OkObjectResult okOutcomes && okOutcomes.Value is SimulateDeliveryOutcomesResponse outcomesResponse)
            {
                results.Add($"✓ Delivery outcomes: {outcomesResponse.ActualSuccessRate}% success rate");
            }
            else
            {
                results.Add("⚠ Delivery outcomes simulation had issues");
            }

            return Ok(new ComprehensiveDemoResponse
            {
                Success = true,
                Message = "Comprehensive Drasi demo completed! Watch the dashboard for real-time decisioning:",
                Steps = results.ToArray(),
                ExpectedDecisions = new[]
                {
                    "🔴 Regional Hotspot - Auckland cluster detected",
                    "📈 Severity Escalation - Wellington pattern detected",
                    "⚡ Rate Spike - Rapid alert creation detected",
                    "👤 Approver Workload Alert - High decision volume",
                    "🔄 Delivery Retry Storm - Multiple failed delivery attempts",
                    "📉 Delivery Success Rate Degraded - Below 80% threshold"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run comprehensive demo");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Demo failed",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    /// <summary>
    /// Runs a full Drasi showcase to populate every dashboard element.
    /// Generates alerts plus synthetic Drasi reactions for all decision types.
    /// </summary>
    /// <response code="200">Showcase demo completed successfully</response>
    [AllowAnonymous]
    [HttpPost("run-showcase")]
    [ProducesResponseType(typeof(ComprehensiveDemoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunShowcase(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting Drasi showcase demo");

            var now = _timeProvider.UtcNow;

            var hotspotAlerts = await CreateDemoAlertsAsync("Auckland", 4, "Severe", cancellationToken);
            var escalationAlerts = await CreateDemoAlertsAsync("Wellington", 2, "Moderate", cancellationToken);
            var duplicateAlerts = await CreateDemoAlertsAsync("Hamilton", 2, "Moderate", cancellationToken, headlineOverride: "Utility Disruption Notice");
            var expansionAlerts = new List<DemoAlert>
            {
                await CreateDemoAlertAsync("Tauranga", "Moderate", cancellationToken, "Transport Disruption"),
                await CreateDemoAlertAsync("Christchurch", "Moderate", cancellationToken, "Transport Disruption"),
            };

            var slaAlert = await CreateDemoAlertAsync("Auckland", "Severe", cancellationToken, "SLA Breach Demo");
            var approvalAlert = await CreateDemoAlertAsync("Wellington", "Moderate", cancellationToken, "Approval Timeout Demo");

            await _regionalHotspotReactionHandler.HandleAsync(
                "NZ-AKL",
                hotspotAlerts.Select(a => a.AlertId).ToList(),
                cancellationToken);

            await _geographicCorrelationReactionHandler.HandleAsync(
                hotspotAlerts.Select(a => a.AlertId).ToList(),
                clusterSeverity: null,
                cancellationToken);

            await _severityEscalationReactionHandler.HandleAsync(
                escalationAlerts.Select(a => a.AlertId).ToList(),
                "Moderate",
                "Severe",
                cancellationToken);

            await _duplicateSuppressionReactionHandler.HandleAsync(
                duplicateAlerts[0].AlertId,
                duplicateAlerts[1].AlertId,
                duplicateAlerts[0].Headline,
                duplicateAlerts[0].RegionCode,
                cancellationToken);

            await _areaExpansionSuggestionReactionHandler.HandleAsync(
                expansionAlerts.Select(a => a.AlertId).ToList(),
                expansionAlerts.Select(a => a.RegionCode).ToList(),
                expansionAlerts[0].Headline,
                cancellationToken);

            await _approvalTimeoutReactionHandler.HandleAsync(approvalAlert.AlertId, 6, cancellationToken);

            await _slaBreachReactionHandler.HandleAsync(
                slaAlert.AlertId,
                slaAlert.Headline,
                slaAlert.Severity,
                90,
                cancellationToken);

            await _slaCountdownReactionHandler.HandleAsync(
                slaAlert.AlertId,
                slaAlert.Headline,
                slaAlert.Severity,
                20,
                40,
                now.AddSeconds(40),
                cancellationToken);

            await _deliveryRetryStormReactionHandler.HandleAsync(
                slaAlert.AlertId,
                slaAlert.Headline,
                slaAlert.Severity,
                4,
                "Connection timeout",
                cancellationToken);

            await _approverWorkloadReactionHandler.HandleAsync(
                "approver-alice@demo.nz",
                7,
                5,
                2,
                "high",
                cancellationToken);

            await _deliverySuccessRateReactionHandler.HandleAsync(20, 10, 10, 50, cancellationToken);

            await CreateDeliveryAttemptsAsync(
                slaAlert.AlertId,
                20,
                50,
                cancellationToken);

            await _rateSpikeDetectionReactionHandler.HandleAsync(54, 126.5, cancellationToken);

            await _expiryWarningReactionHandler.HandleAsync(
                slaAlert.AlertId,
                now.AddMinutes(10),
                cancellationToken);

            await _allClearSuggestionReactionHandler.HandleAsync(
                slaAlert.AlertId,
                slaAlert.Headline,
                now.AddMinutes(-30),
                now,
                cancellationToken);

            return Ok(new ComprehensiveDemoResponse
            {
                Success = true,
                Message = "Drasi showcase demo completed! Watch the dashboard for all decision types.",
                Steps = new[]
                {
                    "✓ Geographic correlation (Auckland cluster)",
                    "✓ Regional hotspot (Auckland)",
                    "✓ Severity escalation (Wellington)",
                    "✓ Duplicate suppression (Hamilton)",
                    "✓ Area expansion suggestion (Tauranga + Christchurch)",
                    "✓ Approval timeout + SLA breach + SLA countdown",
                    "✓ Delivery retry storm + success rate degradation",
                    "✓ Rate spike + expiry warning + all-clear suggestion",
                    "✓ Approver workload alert",
                },
                ExpectedDecisions = new[]
                {
                    "Geographic correlation detected",
                    "Regional Hotspot detected",
                    "Severity escalation detected",
                    "Duplicate suppression suggested",
                    "Area expansion suggested",
                    "Approval timeout detected",
                    "Delivery SLA breached",
                    "SLA countdown update",
                    "Delivery retry storm",
                    "Delivery success rate degraded",
                    "Alert rate spike detected",
                    "Expiry warning",
                    "All-clear suggested",
                    "Approver workload alert",
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run Drasi showcase demo");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Demo failed",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    private async Task<List<DemoAlert>> CreateDemoAlertsAsync(
        string region,
        int count,
        string severity,
        CancellationToken cancellationToken,
        string? headlineOverride = null)
    {
        var alerts = new List<DemoAlert>();
        for (var i = 0; i < count; i++)
        {
            var alert = await CreateDemoAlertAsync(region, severity, cancellationToken, headlineOverride);
            alerts.Add(alert);
        }

        return alerts;
    }

    private async Task<DemoAlert> CreateDemoAlertAsync(
        string region,
        string severity,
        CancellationToken cancellationToken,
        string? headlineOverride = null)
    {
        var now = _timeProvider.UtcNow;
        var (lat, lon, regionCode) = NewZealandRegions[region];
        var geoJsonPolygon = GenerateGeoJsonPolygon(lat, lon, region);
        var headline = headlineOverride ?? DemoHeadlines[new Random().Next(DemoHeadlines.Length)];

        var createDto = new CreateAlertDto
        {
            Headline = $"{headline} - {region}",
            Description = DemoDescriptions[new Random().Next(DemoDescriptions.Length)],
            Severity = severity,
            ChannelType = "SMS",
            LanguageCode = "en-GB",
            ExpiresAt = now.AddMinutes(45),
            Areas = new List<AreaDto>
            {
                new AreaDto
                {
                    AreaDescription = $"{region} area - Demo scenario",
                    GeoJsonPolygon = geoJsonPolygon,
                    RegionCode = regionCode
                }
            }
        };

        var command = new CreateAlertCommand(createDto, "demo-user");
        var result = await _alertService.CreateAlertAsync(command, cancellationToken);

        return new DemoAlert(result.AlertId, createDto.Headline, regionCode, severity);
    }

    private async Task CreateDeliveryAttemptsAsync(
        Guid alertId,
        int totalAttempts,
        int successRatePercent,
        CancellationToken cancellationToken)
    {
        var demoRecipient = await GetOrCreateDemoRecipientAsync(cancellationToken);
        var successTarget = (int)Math.Round(totalAttempts * (successRatePercent / 100.0));

        // AttemptNumber means "which retry is this" (1-3), not a loop counter
        // Demo creates multiple first-attempt records to simulate delivery metrics
        for (var i = 0; i < totalAttempts; i++)
        {
            var attempt = DeliveryAttempt.Create(
                alertId,
                demoRecipient.RecipientId,
                _timeProvider,
                _idGenerator,
                attemptNumber: 1);

            if (i < successTarget)
            {
                attempt.MarkSuccess($"demo-acs-op-{_idGenerator.NewId()}");
            }
            else
            {
                attempt.MarkFailure(DeliveryFailureReasons[i % DeliveryFailureReasons.Length]);
            }

            await _deliveryAttemptRepository.AddAsync(attempt, cancellationToken);
        }

        await _deliveryAttemptRepository.SaveChangesAsync(cancellationToken);
    }

    private record DemoAlert(Guid AlertId, string Headline, string RegionCode, string Severity);

    private async Task<Recipient> GetOrCreateDemoRecipientAsync(CancellationToken cancellationToken)
    {
        const string demoEmail = "demo-recipient@alerts.demo.nz";

        var existing = await _recipientRepository.GetByEmailAsync(demoEmail, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var recipient = Recipient.Create(demoEmail, _timeProvider, _idGenerator, "Demo Recipient");
        await _recipientRepository.AddAsync(recipient, cancellationToken);
        await _recipientRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created demo recipient: {Email}", demoEmail);
        return recipient;
    }

    /// <summary>
    /// Determines severity based on scenario to demonstrate different Drasi patterns.
    /// </summary>
    private string SelectSeverity(int scenario, int index, int totalCount, Random random)
    {
        return scenario switch
        {
            0 => "Severe", // Regional hotspot alerts
            1 => index switch // Severity escalation: Minor -> Moderate -> Severe -> Extreme
            {
                int i when i < totalCount / 3 => "Minor",
                int i when i < 2 * totalCount / 3 => "Moderate",
                int i when i < totalCount - 2 => "Severe",
                _ => "Extreme"
            },
            _ => new[] { "Minor", "Moderate", "Severe", "Extreme" }[random.Next(4)] // Rate spike: varied
        };
    }

    /// <summary>
    /// Generates a GeoJSON polygon centered on the region with small randomization.
    /// </summary>

    private string GenerateGeoJsonPolygon(double lat, double lon, string region)
    {
        // Create a small square around the center point (approximately 1km x 1km at equator)
        var offset = 0.01; // ~1.1km at equator

        var sw_lon = lon - offset;
        var sw_lat = lat - offset;
        var ne_lon = lon + offset;
        var ne_lat = lat + offset;

        // GeoJSON uses [longitude, latitude] order
        return $"{{\"type\":\"Polygon\",\"coordinates\":[[[{sw_lon},{sw_lat}],[{ne_lon},{sw_lat}],[{ne_lon},{ne_lat}],[{sw_lon},{ne_lat}],[{sw_lon},{sw_lat}]]]}}";
    }
}

/// <summary>
/// Response for demo alert generation.
/// </summary>
public record GenerateAlertsResponse
{
    public required bool Success { get; init; }
    public required int Count { get; init; }
    public required List<string> AlertIds { get; init; }
    public required string Message { get; init; }
    public required string[] ExpectedDecisions { get; init; }
}

/// <summary>
/// Response for delivery failure simulation.
/// </summary>
public record SimulateDeliveryResponse
{
    public required bool Success { get; init; }
    public required int AlertsAffected { get; init; }
    public required int AttemptsCreated { get; init; }
    public required string Message { get; init; }
    public required string ExpectedDecision { get; init; }
}

/// <summary>
/// Response for approver workload simulation.
/// </summary>
public record SimulateApproverWorkloadResponse
{
    public required bool Success { get; init; }
    public required string ApproverId { get; init; }
    public required int ApprovedCount { get; init; }
    public required int RejectedCount { get; init; }
    public required int TotalDecisions { get; init; }
    public required string Message { get; init; }
    public required string ExpectedDecision { get; init; }
}

/// <summary>
/// Response for delivery outcomes simulation.
/// </summary>
public record SimulateDeliveryOutcomesResponse
{
    public required bool Success { get; init; }
    public required int AlertsProcessed { get; init; }
    public required int SuccessfulDeliveries { get; init; }
    public required int FailedDeliveries { get; init; }
    public required int ActualSuccessRate { get; init; }
    public required string Message { get; init; }
    public required string ExpectedDecision { get; init; }
}

/// <summary>
/// Response for comprehensive demo.
/// </summary>
public record ComprehensiveDemoResponse
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public required string[] Steps { get; init; }
    public required string[] ExpectedDecisions { get; init; }
}
