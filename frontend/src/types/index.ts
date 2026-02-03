// Alert and related types
export type AlertSeverity = 'Extreme' | 'Severe' | 'Moderate' | 'Minor' | 'Unknown'
export type AlertChannelType = 'test' | 'operator' | 'severe' | 'government' | 'sms'
export type AlertStatus = 'draft' | 'pending_approval' | 'approved' | 'rejected' | 'delivered' | 'cancelled' | 'expired'
export type DeliveryStatus = 'pending' | 'delivered' | 'failed'
export type DecisionType = 'approved' | 'rejected'

export interface GeoPolygon {
    type: 'Polygon'
    coordinates: Array<Array<[number, number]>>
}

export interface Area {
    areaId?: string
    alertId?: string
    areaDescription: string
    polygon: GeoPolygon
    regionCode?: string
}

export interface Alert {
    alertId?: string
    headline: string
    description: string
    severity: AlertSeverity
    channelType: AlertChannelType
    status: AlertStatus
    deliveryStatus: DeliveryStatus
    languageCode: string
    expiresAt: string
    sentAt?: string
    createdAt?: string
    updatedAt?: string
    createdBy?: string
    areas: Area[]
}

export interface ApprovalRecord {
    approvalId?: string
    alertId: string
    approverId: string
    decision: DecisionType
    rejectionReason?: string
    decidedAt?: string
}

export interface DeliveryAttempt {
    attemptId?: string
    alertId: string
    recipientId: string
    attemptNumber: number
    status: 'pending' | 'success' | 'failed'
    failureReason?: string
    acsOperationId?: string
    attemptedAt?: string
}

export interface CorrelationEvent {
    eventId?: string
    patternType: string
    alertIds: string[]
    detectionTimestamp: string
    clusterSeverity?: AlertSeverity
    regionCode?: string
    metadata?: Record<string, unknown>
    resolvedAt?: string
}

export interface SLABreach {
    alertId: string
    headline: string
    severity: AlertSeverity
    detectionTimestamp: string
    elapsedSeconds: number
}

export interface SLACountdown {
    alertId: string
    headline: string
    severity: AlertSeverity
    secondsElapsed: number
    secondsRemaining: number
    breachAt: string
}

export interface ApprovalTimeout {
    alertId: string
    headline: string
    severity: AlertSeverity
    createdAt: string
    elapsedMinutes: number
}

export interface DeliveryRetryStorm {
    alertId: string
    headline: string
    severity: string
    failedAttemptCount: number
    lastFailureReason: string
    detectionTimestamp: string
}

export interface ApproverWorkloadAlert {
    approverId: string
    decisionsInHour: number
    approvedCount: number
    rejectedCount: number
    workloadLevel: string
    detectionTimestamp: string
}

export interface DeliverySuccessRateAlert {
    totalAttempts: number
    successCount: number
    failedCount: number
    successRatePercent: number
    detectionTimestamp: string
}

export interface DashboardSummary {
    totalAlerts: number
    pendingApprovals: number
    deliveryFailures: number
    deliverySuccessRatePercent?: number
    deliveryAttemptsLastHour?: number
    deliverySuccessCountLastHour?: number
    deliveryFailureCountLastHour?: number
    slaBreach: number
    correlatedAlerts: number
    slaBreaches?: SLABreach[]
    approvalTimeouts?: ApprovalTimeout[]
}

export interface PaginatedResult<T> {
    items: T[]
    page: number
    pageSize: number
    total: number
}

export interface ApiError {
    type: string
    title: string
    status: number
    detail?: string
    instance?: string
    correlationId?: string
    errors?: Record<string, string[]>
}

export interface AuthUser {
    id: string
    name: string
    email: string
    roles: string[]
}
