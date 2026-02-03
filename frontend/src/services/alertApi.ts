import { get, post, put, del } from './apiInterceptor'
import type { Alert, Area, GeoPolygon, PaginatedResult, DashboardSummary, AlertChannelType, AlertStatus, DeliveryStatus, ApprovalTimeout, SLABreach } from '../types'

export interface CreateAlertRequest {
    headline: string
    description: string
    severity: string
    channelType: string
    expiresAt: string
    languageCode: string
    areas: Array<{
        areaDescription: string
        geoJsonPolygon: string
    }>
}

export interface RejectAlertRequest {
    rejectionReason: string
}

export interface CancelAlertRequest {
    reason?: string
}

const emptyPolygon: GeoPolygon = { type: 'Polygon', coordinates: [] }

const normalizeStatus = (status?: string): AlertStatus => {
    if (!status) return 'draft'
    const normalized = status
        .replace(/([a-z])([A-Z])/g, '$1_$2')
        .replace(/[\s-]+/g, '_')
        .toLowerCase()

    switch (normalized) {
        case 'draft':
        case 'pending_approval':
        case 'approved':
        case 'rejected':
        case 'delivered':
        case 'cancelled':
        case 'expired':
            return normalized as AlertStatus
        default:
            return 'draft'
    }
}

const normalizeChannelType = (value?: string): AlertChannelType => {
    if (!value) return 'test'
    const normalized = value.toLowerCase()
    switch (normalized) {
        case 'test':
        case 'operator':
        case 'severe':
        case 'government':
        case 'sms':
            return normalized as AlertChannelType
        default:
            return 'test'
    }
}

const normalizeDeliveryStatus = (value?: string): DeliveryStatus => {
    if (!value) return 'pending'
    const normalized = value.toLowerCase()
    switch (normalized) {
        case 'pending':
        case 'delivered':
        case 'failed':
            return normalized as DeliveryStatus
        default:
            return 'pending'
    }
}

const parseGeoJsonPolygon = (value?: string): GeoPolygon | null => {
    if (!value) return null
    try {
        const parsed = JSON.parse(value) as GeoPolygon
        if (parsed?.type === 'Polygon' && Array.isArray(parsed.coordinates)) {
            return parsed
        }
    } catch {
        // Ignore parse errors and fall back to empty polygon.
    }
    return null
}

const mapArea = (area: Partial<Area> & { geoJsonPolygon?: string }): Area => {
    const polygon = area.polygon ?? parseGeoJsonPolygon(area.geoJsonPolygon) ?? emptyPolygon
    const mapped: Area = {
        areaDescription: area.areaDescription ?? '',
        polygon,
    }
    if (area.areaId !== undefined) {
        mapped.areaId = area.areaId
    }
    if (area.alertId !== undefined) {
        mapped.alertId = area.alertId
    }
    if (area.regionCode !== undefined) {
        mapped.regionCode = area.regionCode
    }
    return mapped
}

const isExpiredPendingApproval = (status: AlertStatus, expiresAt?: string): boolean => {
    if (status !== 'pending_approval' || !expiresAt) return false
    const expiresAtMs = Date.parse(expiresAt)
    return !Number.isNaN(expiresAtMs) && expiresAtMs <= Date.now()
}

const mapAlert = (alert: Alert & { areas?: Array<Partial<Area> & { geoJsonPolygon?: string }> }): Alert => {
    const normalizedStatus = normalizeStatus(alert.status as string)
    const status = isExpiredPendingApproval(normalizedStatus, alert.expiresAt)
        ? 'expired'
        : normalizedStatus

    return {
        ...alert,
        status,
        channelType: normalizeChannelType(alert.channelType as string),
        deliveryStatus: normalizeDeliveryStatus(alert.deliveryStatus as string),
        areas: (alert.areas ?? []).map(mapArea),
    }
}

// Alert endpoints
export const alertApi = {
    // List alerts with pagination and filters
    listAlerts: async (page: number = 1, pageSize: number = 20, filters?: Record<string, string>): Promise<PaginatedResult<Alert>> => {
        const params = new URLSearchParams({
            page: page.toString(),
            pageSize: pageSize.toString(),
            ...filters,
        })
        const response = await get<unknown>(`/api/v1/alerts?${params.toString()}`)

        if (Array.isArray(response)) {
            const items = response.map(item => mapAlert(item as Alert & { areas?: Array<Partial<Area> & { geoJsonPolygon?: string }> }))
            return {
                items,
                page,
                pageSize,
                total: items.length,
            }
        }

        const responseObj = (response ?? {}) as Partial<PaginatedResult<Alert>> & {
            alerts?: Array<Alert & { areas?: Array<Partial<Area> & { geoJsonPolygon?: string }> }>
            Alerts?: Array<Alert & { areas?: Array<Partial<Area> & { geoJsonPolygon?: string }> }>
            totalCount?: number
            TotalCount?: number
            page?: number
            Page?: number
            pageSize?: number
            PageSize?: number
        }

        const items = (responseObj.items ?? responseObj.alerts ?? responseObj.Alerts ?? []).map(mapAlert)

        return {
            items,
            page: responseObj.page ?? responseObj.Page ?? page,
            pageSize: responseObj.pageSize ?? responseObj.PageSize ?? pageSize,
            total: responseObj.total ?? responseObj.totalCount ?? responseObj.TotalCount ?? items.length,
        }
    },

    // Get single alert
    getAlert: async (alertId: string): Promise<Alert> => {
        const alert = await get<Alert & { areas?: Array<Partial<Area> & { geoJsonPolygon?: string }> }>(`/api/v1/alerts/${alertId}`)
        return mapAlert(alert)
    },

    // Create alert
    createAlert: async (data: CreateAlertRequest): Promise<Alert> => {
        const alert = await post<Alert & { areas?: Array<Partial<Area> & { geoJsonPolygon?: string }> }>('/api/v1/alerts', data)
        return mapAlert(alert)
    },

    // Approve alert with optimistic locking
    approveAlert: async (alertId: string, etag?: string): Promise<Alert> => {
        const options = etag ? { headers: { 'If-Match': etag } } : {}
        const alert = await put<Alert & { areas?: Array<Partial<Area> & { geoJsonPolygon?: string }> }>(
            `/api/v1/alerts/${alertId}/approval`,
            undefined,
            options
        )
        return mapAlert(alert)
    },

    // Reject alert with a reason
    rejectAlert: async (alertId: string, request: RejectAlertRequest, etag?: string): Promise<Alert> => {
        const options = etag ? { headers: { 'If-Match': etag } } : {}
        const alert = await del<Alert & { areas?: Array<Partial<Area> & { geoJsonPolygon?: string }> }>(
            `/api/v1/alerts/${alertId}/approval`,
            request,
            options
        )
        return mapAlert(alert)
    },

    // Cancel alert
    cancelAlert: async (alertId: string): Promise<Alert> => {
        return put<Alert>(`/api/v1/alerts/${alertId}/cancel`, {})
    },

    // Get dashboard summary
    getDashboardSummary: async (): Promise<DashboardSummary> => {
        const summary = await get<unknown>('/api/v1/dashboard/summary')
        if (summary && typeof summary === 'object') {
            const summaryObj = summary as Record<string, unknown>
            const counts = (summaryObj.counts ?? summaryObj.Counts) as Record<string, unknown> | undefined
            const pendingApproval =
                (counts?.pendingApproval ?? counts?.PendingApproval ?? summaryObj.pendingApprovals ?? summaryObj.PendingApprovals) as number | undefined
            const total =
                (counts?.total ?? counts?.Total ?? summaryObj.totalAlerts ?? summaryObj.TotalAlerts) as number | undefined
            const recentCorrelations =
                (summaryObj.recentCorrelations ?? summaryObj.RecentCorrelations) as unknown[] | undefined
            const deliveryFailures =
                (summaryObj.deliveryFailures ?? summaryObj.DeliveryFailures) as number | undefined
            const deliverySuccessRatePercent =
                (summaryObj.deliverySuccessRatePercent ?? summaryObj.DeliverySuccessRatePercent) as number | undefined
            const deliveryAttemptsLastHour =
                (summaryObj.deliveryAttemptsLastHour ?? summaryObj.DeliveryAttemptsLastHour) as number | undefined
            const deliverySuccessCountLastHour =
                (summaryObj.deliverySuccessCountLastHour ?? summaryObj.DeliverySuccessCountLastHour) as number | undefined
            const deliveryFailureCountLastHour =
                (summaryObj.deliveryFailureCountLastHour ?? summaryObj.DeliveryFailureCountLastHour) as number | undefined
            const slaBreach =
                (summaryObj.slaBreach ?? summaryObj.SLABreach) as number | undefined
            const correlatedAlerts =
                (summaryObj.correlatedAlerts ?? summaryObj.CorrelatedAlerts) as number | undefined
            const rawSlaBreaches =
                (summaryObj.slaBreaches ?? summaryObj.SLABreaches) as Array<Record<string, unknown>> | undefined
            const rawApprovalTimeouts =
                (summaryObj.approvalTimeouts ?? summaryObj.ApprovalTimeouts) as Array<Record<string, unknown>> | undefined

            const slaBreaches: SLABreach[] | undefined = rawSlaBreaches?.map(item => ({
                alertId: String(item.alertId ?? item.AlertId ?? ''),
                headline: String(item.headline ?? item.Headline ?? ''),
                severity: String(item.severity ?? item.Severity ?? 'Unknown') as SLABreach['severity'],
                detectionTimestamp: String(item.detectionTimestamp ?? item.DetectionTimestamp ?? ''),
                elapsedSeconds: Number(item.elapsedSeconds ?? item.ElapsedSeconds ?? 0),
            }))

            const approvalTimeouts: ApprovalTimeout[] | undefined = rawApprovalTimeouts?.map(item => ({
                alertId: String(item.alertId ?? item.AlertId ?? ''),
                headline: String(item.headline ?? item.Headline ?? ''),
                severity: String(item.severity ?? item.Severity ?? 'Unknown') as ApprovalTimeout['severity'],
                createdAt: String(item.createdAt ?? item.CreatedAt ?? ''),
                elapsedMinutes: Number(item.elapsedMinutes ?? item.ElapsedMinutes ?? 0),
            }))

            return {
                totalAlerts: total ?? 0,
                pendingApprovals: pendingApproval ?? 0,
                deliveryFailures: deliveryFailures ?? 0,
                ...(deliverySuccessRatePercent !== undefined ? { deliverySuccessRatePercent } : {}),
                ...(deliveryAttemptsLastHour !== undefined ? { deliveryAttemptsLastHour } : {}),
                ...(deliverySuccessCountLastHour !== undefined ? { deliverySuccessCountLastHour } : {}),
                ...(deliveryFailureCountLastHour !== undefined ? { deliveryFailureCountLastHour } : {}),
                slaBreach: slaBreach ?? (slaBreaches ? slaBreaches.length : 0),
                correlatedAlerts: correlatedAlerts ?? (recentCorrelations ? recentCorrelations.length : 0),
                slaBreaches: slaBreaches ?? [],
                approvalTimeouts: approvalTimeouts ?? [],
            }
        }
        return {
            totalAlerts: 0,
            pendingApprovals: 0,
            deliveryFailures: 0,
            slaBreach: 0,
            correlatedAlerts: 0,
            slaBreaches: [],
            approvalTimeouts: [],
        }
    },

    // Get correlated alerts
    getCorrelations: async (): Promise<Array<{ patternType: string; alertIds: string[]; regionCode?: string }>> => {
        const correlations = await get<Array<{ patternType: string; alertIds?: string[]; regionCode?: string }>>('/api/v1/dashboard/correlations')
        return correlations.map(correlation => ({
            ...correlation,
            alertIds: correlation.alertIds ?? [],
        }))
    },

    // Health check endpoints
    healthCheck: async (): Promise<{ status: string }> => {
        return get('/health')
    },

    healthLive: async (): Promise<{ status: string }> => {
        return get('/health/live')
    },

    healthReady: async (): Promise<{ status: string }> => {
        return get('/health/ready')
    },
}

export default alertApi
