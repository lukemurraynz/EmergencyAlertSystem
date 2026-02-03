import type { DashboardUpdate } from '../services/dashboardHubService'

export type DecisionSeverity = 'critical' | 'high' | 'medium' | 'low' | 'info'

export interface DecisionInsight {
    id: string
    title: string
    detail: string
    severity: DecisionSeverity
    action?: string
    timestamp: string
    source: 'Drasi'
}

export interface BuildDecisionOptions {
    now?: Date
    idFactory?: () => string
}

const getString = (value: unknown): string | undefined => {
    if (typeof value === 'string') {
        return value
    }
    return undefined
}

const getNumber = (value: unknown): number | undefined => {
    if (typeof value === 'number' && !Number.isNaN(value)) {
        return value
    }
    if (typeof value === 'string') {
        const parsed = Number(value)
        return Number.isNaN(parsed) ? undefined : parsed
    }
    return undefined
}

const formatPattern = (value: string): string => {
    if (!value) return 'Correlation'
    return value.replace(/_/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2')
}

const normalizePatternKey = (value: string): string => {
    if (!value) return ''
    return value
        .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
        .replace(/[\s-]+/g, '_')
        .toLowerCase()
}

const defaultIdFactory = (): string => {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
        return crypto.randomUUID()
    }
    return `decision-${Math.random().toString(36).slice(2, 10)}`
}

export const buildDecisionFromUpdate = (
    update: DashboardUpdate,
    options: BuildDecisionOptions = {}
): DecisionInsight | null => {
    const payload = update.payload ?? {}
    const now = options.now ?? new Date()
    const idFactory = options.idFactory ?? defaultIdFactory
    const idempotencyKey = getString(payload.idempotencyKey)
    const id = idempotencyKey || idFactory()

    const timestamp =
        getString(payload.detectionTimestamp) ||
        getString(payload.timestamp) ||
        getString(payload.warningTimestamp) ||
        getString(payload.createdAt) ||
        now.toISOString()

    switch (update.eventType) {
        case 'SLABreachDetected': {
            const headline = getString(payload.headline) || 'Alert'
            const elapsedSeconds = getNumber(payload.elapsedSeconds)
            return {
                id,
                title: 'Delivery SLA breached',
                detail: `${headline} exceeded SLA${elapsedSeconds ? ` by ${elapsedSeconds}s` : ''}.`,
                severity: 'high',
                action: 'Escalate delivery investigation and notify on-call.',
                timestamp,
                source: 'Drasi',
            }
        }
        case 'ApprovalTimeoutDetected': {
            const headline = getString(payload.headline) || 'Alert'
            const elapsedMinutes = getNumber(payload.elapsedMinutes)
            return {
                id,
                title: 'Approval timeout detected',
                detail: `${headline} awaiting approval${elapsedMinutes ? ` for ${elapsedMinutes} min` : ''}.`,
                severity: 'medium',
                action: 'Escalate approval or re-route to backup approver.',
                timestamp,
                source: 'Drasi',
            }
        }
        case 'CorrelationEventDetected': {
            const patternType = getString(payload.patternType) || 'Correlation'
            const patternKey = normalizePatternKey(patternType)
            const alertIds = Array.isArray(payload.alertIds) ? payload.alertIds : []
            const alertCount = alertIds.length || getNumber(payload.alertCount) || 0
            const headline = getString(payload.headline) || 'Alert'
            const regionCode = getString(payload.regionCode) || getString(payload.region)
            const regionCodes = Array.isArray(payload.regionCodes)
                ? payload.regionCodes.filter((value): value is string => typeof value === 'string')
                : []

            if (patternKey === 'duplicate_suppression') {
                return {
                    id,
                    title: 'Possible duplicate alert',
                    detail: `${headline} appears multiple times${regionCode ? ` in ${regionCode}` : ''}.`,
                    severity: 'medium',
                    action: 'Suppress duplicate delivery or merge with the original alert.',
                    timestamp,
                    source: 'Drasi',
                }
            }

            if (patternKey === 'regional_hotspot') {
                const regionLabel = regionCode || 'multiple regions'
                return {
                    id,
                    title: 'Regional Hotspot detected',
                    detail: `${alertCount} alert${alertCount === 1 ? '' : 's'} active in ${regionLabel}.`,
                    severity: 'high',
                    action: 'Coordinate regional EAS coverage and monitor escalation.',
                    timestamp,
                    source: 'Drasi',
                }
            }

            if (patternKey === 'severity_escalation') {
                const escalation = getString(payload.escalation)
                const fromSeverity = getString(payload.fromSeverity)
                const toSeverity = getString(payload.toSeverity)
                const escalationLabel = escalation || (fromSeverity && toSeverity ? `${fromSeverity} → ${toSeverity}` : 'severity escalation')
                return {
                    id,
                    title: 'Severity escalation detected',
                    detail: `${headline} escalated: ${escalationLabel}.`,
                    severity: 'high',
                    action: 'Review escalation triggers and update response posture.',
                    timestamp,
                    source: 'Drasi',
                }
            }

            if (patternKey === 'area_expansion_suggestion') {
                const regionLabel = regionCodes.length > 0 ? regionCodes.join(', ') : 'multiple regions'
                return {
                    id,
                    title: 'Expand coverage area',
                    detail: `${headline} now spans ${regionLabel}.`,
                    severity: 'medium',
                    action: 'Consider expanding the alert boundary or issuing a broader advisory.',
                    timestamp,
                    source: 'Drasi',
                }
            }

            return {
                id,
                title: `${formatPattern(patternType)} detected`,
                detail: `${alertCount} alert${alertCount === 1 ? '' : 's'} correlated.`,
                severity: alertCount >= 4 ? 'high' : 'medium',
                action: 'Consider broader EAS coverage for affected regions.',
                timestamp,
                source: 'Drasi',
            }
        }
        case 'DashboardSummaryUpdated': {
            const eventType = getString(payload.eventType)
            if (eventType === 'RateSpikeDetected') {
                const rate = getNumber(payload.creationRatePerHour)
                const alertCount = getNumber(payload.alertsInOneHourWindow)
                const severity = getString(payload.severity) === 'Critical' ? 'critical' : 'high'
                return {
                    id,
                    title: 'Alert rate spike detected',
                    detail: `${alertCount ?? 'Multiple'} alerts in the last hour${rate ? ` · ${rate}/hr` : ''}.`,
                    severity,
                    action: 'Validate upstream sources and prepare EAS broadcast escalation.',
                    timestamp,
                    source: 'Drasi',
                }
            }
            return null
        }
        case 'AlertStatusChanged': {
            const status = getString(payload.status)
            if (status === 'ExpiryWarning') {
                const headline = getString(payload.headline) || 'Alert'
                const minutesRemaining = getNumber(payload.minutesRemaining)
                return {
                    id,
                    title: 'Expiry warning',
                    detail: `${headline} expires in ${minutesRemaining ?? 'upcoming'} minutes.`,
                    severity: 'low',
                    action: 'Review whether to extend or reissue.',
                    timestamp,
                    source: 'Drasi',
                }
            }
            if (status === 'AllClearSuggested') {
                const headline = getString(payload.headline) || 'Alert'
                return {
                    id,
                    title: 'All-clear suggested',
                    detail: `${headline} delivered 30 minutes ago.`,
                    severity: 'low',
                    action: 'Confirm conditions and send an all-clear update.',
                    timestamp,
                    source: 'Drasi',
                }
            }
            return null
        }
        case 'AlertDelivered': {
            const headline = getString(payload.headline) || 'Alert'
            return {
                id,
                title: 'Delivery confirmed',
                detail: `${headline} delivered to email recipients.`,
                severity: 'info',
                timestamp,
                source: 'Drasi',
            }
        }
        case 'SLACountdownUpdate': {
            const headline = getString(payload.headline) || 'Alert'
            const secondsRemaining = getNumber(payload.secondsRemaining)
            const severity = getString(payload.severity)
            const severityLevel: DecisionSeverity = secondsRemaining !== undefined && secondsRemaining <= 15 ? 'high' : 'medium'
            return {
                id,
                title: `⏱️ SLA countdown: ${secondsRemaining ?? '?'}s remaining`,
                detail: `${headline} (${severity}) approaching SLA breach.`,
                severity: severityLevel,
                action: secondsRemaining !== undefined && secondsRemaining <= 15 
                    ? 'Urgent: Expedite delivery or prepare escalation.'
                    : 'Monitor delivery progress.',
                timestamp,
                source: 'Drasi',
            }
        }
        case 'DeliveryRetryStormDetected': {
            const headline = getString(payload.headline) || 'Alert'
            const failedCount = getNumber(payload.failedAttemptCount) || 0
            const lastReason = getString(payload.lastFailureReason) || 'Unknown error'
            const severityLevel: DecisionSeverity = failedCount >= 5 ? 'critical' : 'high'
            return {
                id,
                title: '🔄 Delivery retry storm',
                detail: `${headline} failed ${failedCount} delivery attempts. Last error: ${lastReason}`,
                severity: severityLevel,
                action: 'Investigate delivery pipeline. Check ACS status and recipient validity.',
                timestamp,
                source: 'Drasi',
            }
        }
        case 'ApproverWorkloadAlert': {
            const approverId = getString(payload.approverId) || 'Unknown'
            const decisionsInHour = getNumber(payload.decisionsInHour) || 0
            const approvedCount = getNumber(payload.approvedCount) || 0
            const rejectedCount = getNumber(payload.rejectedCount) || 0
            const workloadLevel = getString(payload.workloadLevel) || 'medium'
            const severityLevel: DecisionSeverity = workloadLevel === 'critical' ? 'critical' : workloadLevel === 'high' ? 'high' : 'medium'
            return {
                id,
                title: '👤 Approver workload imbalance',
                detail: `${approverId} made ${decisionsInHour} decisions in 1 hour (${approvedCount} approved, ${rejectedCount} rejected).`,
                severity: severityLevel,
                action: 'Consider distributing approvals across team or adding backup approvers.',
                timestamp,
                source: 'Drasi',
            }
        }
        case 'DeliverySuccessRateDegraded': {
            const totalAttempts = getNumber(payload.totalAttempts) || 0
            const successCount = getNumber(payload.successCount) || 0
            const failedCount = getNumber(payload.failedCount) || 0
            const successRate = getNumber(payload.successRatePercent) || 0
            const severityLevel: DecisionSeverity = successRate < 50 ? 'critical' : successRate < 70 ? 'high' : 'medium'
            return {
                id,
                title: '📉 Delivery success rate degraded',
                detail: `Success rate: ${successRate.toFixed(1)}% (${successCount}/${totalAttempts} succeeded, ${failedCount} failed).`,
                severity: severityLevel,
                action: 'Investigate delivery infrastructure. Check ACS quotas and network connectivity.',
                timestamp,
                source: 'Drasi',
            }
        }
        default:
            return null
    }
}
