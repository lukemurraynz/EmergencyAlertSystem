import { useState, useEffect, useRef, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import {
    Text,
    makeStyles,
    tokens,
    Spinner,
    Badge,
    Button,
    Card,
    CardHeader,
    CardFooter,
} from '@fluentui/react-components'
import Layout from '../components/Layout'
import alertApi from '../services/alertApi'
import { post } from '../services/apiInterceptor'
import dashboardHubService, { type DashboardUpdate } from '../services/dashboardHubService'
import { buildDecisionFromUpdate } from '../utils/drasiDecisioning'
import type { DecisionInsight, DecisionSeverity } from '../utils/drasiDecisioning'
import type { DashboardSummary, ApprovalTimeout, SLABreach, SLACountdown, DeliveryRetryStorm, ApproverWorkloadAlert, DeliverySuccessRateAlert } from '../types'

const useDashboardStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXL,
    },
    title: {
        fontSize: tokens.fontSizeBase600,
        fontWeight: tokens.fontWeightBold,
        margin: 0,
    },
    summaryGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
        gap: tokens.spacingHorizontalL,
    },
    summaryCard: {
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    summaryValue: {
        fontSize: tokens.fontSizeBase600,
        fontWeight: tokens.fontWeightBold,
        color: tokens.colorNeutralForeground1,
    },
    summaryLabel: {
        color: tokens.colorNeutralForeground3,
        marginTop: tokens.spacingVerticalS,
    },
    correlationGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(250px, 1fr))',
        gap: tokens.spacingHorizontalL,
    },
    insightGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
        gap: tokens.spacingHorizontalL,
    },
    insightCard: {
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    insightList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        margin: 0,
        paddingLeft: tokens.spacingHorizontalL,
    },
    correlationCard: {
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    decisionHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalL,
        flexWrap: 'wrap',
    },
    decisionGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
        gap: tokens.spacingHorizontalL,
    },
    decisionCard: {
        padding: tokens.spacingHorizontalL,
        borderRadius: tokens.borderRadiusLarge,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        boxShadow: '0 16px 30px rgba(20, 27, 76, 0.08)',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    decisionMeta: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase100,
    },
    liveStatus: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    loading: {
        display: 'flex',
        justifyContent: 'center',
        padding: tokens.spacingHorizontalXXL,
    },
    error: {
        color: tokens.colorNeutralForegroundInverted,
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorStatusDangerBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
})

export const DashboardPage = () => {
    const styles = useDashboardStyles()
    const navigate = useNavigate()
    const [summary, setSummary] = useState<DashboardSummary | null>(null)
    const [correlations, setCorrelations] = useState<Array<{ patternType: string; alertIds: string[]; regionCode?: string }>>([])
    const [isLoading, setIsLoading] = useState(true)
    const [isRefreshing, setIsRefreshing] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [decisionFeed, setDecisionFeed] = useState<DecisionInsight[]>([])
    const [liveApprovalTimeouts, setLiveApprovalTimeouts] = useState<ApprovalTimeout[]>([])
    const [liveSlaBreaches, setLiveSlaBreaches] = useState<SLABreach[]>([])
    const [liveSlaCountdowns, setLiveSlaCountdowns] = useState<SLACountdown[]>([])
    const [liveRetryStorms, setLiveRetryStorms] = useState<DeliveryRetryStorm[]>([])
    const [liveApproverWorkloads, setLiveApproverWorkloads] = useState<ApproverWorkloadAlert[]>([])
    const [liveDeliverySuccessRates, setLiveDeliverySuccessRates] = useState<DeliverySuccessRateAlert[]>([])
    const [liveStatus, setLiveStatus] = useState<'connecting' | 'connected' | 'error'>('connecting')
    const [liveError, setLiveError] = useState<string | null>(null)
    const [isDemoLoading, setIsDemoLoading] = useState(false)
    const hasLoadedRef = useRef(false)
    const signalRInitializedRef = useRef(false)

    const loadDashboard = useCallback(async () => {
        const isInitialLoad = !hasLoadedRef.current
        if (isInitialLoad) {
            setIsLoading(true)
        } else {
            setIsRefreshing(true)
        }
        setError(null)
        try {
            const [summaryData, correlationsData] = await Promise.all([
                alertApi.getDashboardSummary(),
                alertApi.getCorrelations(),
            ])
            setSummary(summaryData)
            setCorrelations(correlationsData)
            hasLoadedRef.current = true
        } catch (err) {
            const error = err as { detail?: string }
            setError(error?.detail || 'Failed to load dashboard')
        } finally {
            setIsLoading(false)
            setIsRefreshing(false)
        }
    }, [])

    useEffect(() => {
        loadDashboard()
        const interval = setInterval(loadDashboard, 10000)
        return () => clearInterval(interval)
    }, [loadDashboard])

    useEffect(() => {
        // Prevent double initialization in StrictMode
        if (signalRInitializedRef.current) {
            return
        }
        signalRInitializedRef.current = true

        let isActive = true
        const handleUpdate = (update: DashboardUpdate) => {
            if (!isActive) return
            
            const decision = buildDecisionFromUpdate(update)
            if (!decision) {
                // Still capture operational insights below even if the decision is ignored.
            }
            if (decision) {
                setDecisionFeed(prev => [decision, ...prev].slice(0, 6))
            }

            const payload = update.payload ?? {}
            const parseString = (value: unknown): string | undefined => (typeof value === 'string' ? value : undefined)
            const parseNumber = (value: unknown): number | undefined => {
                if (typeof value === 'number' && !Number.isNaN(value)) return value
                if (typeof value === 'string') {
                    const parsed = Number(value)
                    return Number.isNaN(parsed) ? undefined : parsed
                }
                return undefined
            }

            if (update.eventType === 'ApprovalTimeoutDetected') {
                const alertId = parseString(payload.alertId)
                if (!alertId) return
                const entry: ApprovalTimeout = {
                    alertId,
                    headline: parseString(payload.headline) || 'Alert',
                    severity: (parseString(payload.severity) as ApprovalTimeout['severity']) || 'Unknown',
                    createdAt: parseString(payload.createdAt) || new Date().toISOString(),
                    elapsedMinutes: parseNumber(payload.elapsedMinutes) ?? 0,
                }
                setLiveApprovalTimeouts(prev => [entry, ...prev.filter(item => item.alertId !== alertId)].slice(0, 5))
            }

            if (update.eventType === 'SLABreachDetected') {
                const alertId = parseString(payload.alertId)
                if (!alertId) return
                const entry: SLABreach = {
                    alertId,
                    headline: parseString(payload.headline) || 'Alert',
                    severity: (parseString(payload.severity) as SLABreach['severity']) || 'Unknown',
                    detectionTimestamp: parseString(payload.detectionTimestamp) || new Date().toISOString(),
                    elapsedSeconds: parseNumber(payload.elapsedSeconds) ?? 0,
                }
                setLiveSlaBreaches(prev => [entry, ...prev.filter(item => item.alertId !== alertId)].slice(0, 5))
                // Remove from countdown when breach occurs
                setLiveSlaCountdowns(prev => prev.filter(item => item.alertId !== alertId))
            }

            if (update.eventType === 'SLACountdownUpdate') {
                const alertId = parseString(payload.alertId)
                if (!alertId) return
                const entry: SLACountdown = {
                    alertId,
                    headline: parseString(payload.headline) || 'Alert',
                    severity: (parseString(payload.severity) as SLACountdown['severity']) || 'Unknown',
                    secondsElapsed: parseNumber(payload.secondsElapsed) ?? 0,
                    secondsRemaining: parseNumber(payload.secondsRemaining) ?? 0,
                    breachAt: parseString(payload.breachAt) || new Date().toISOString(),
                }
                setLiveSlaCountdowns(prev => {
                    const filtered = prev.filter(item => item.alertId !== alertId)
                    return [...filtered, entry].sort((a, b) => a.secondsRemaining - b.secondsRemaining).slice(0, 5)
                })
            }

            if (update.eventType === 'DeliveryRetryStormDetected') {
                const alertId = parseString(payload.alertId)
                if (!alertId) return
                const entry: DeliveryRetryStorm = {
                    alertId,
                    headline: parseString(payload.headline) || 'Alert',
                    severity: parseString(payload.severity) || 'Unknown',
                    failedAttemptCount: parseNumber(payload.failedAttemptCount) ?? 0,
                    lastFailureReason: parseString(payload.lastFailureReason) || 'Unknown error',
                    detectionTimestamp: parseString(payload.detectionTimestamp) || new Date().toISOString(),
                }
                setLiveRetryStorms(prev => [entry, ...prev.filter(item => item.alertId !== alertId)].slice(0, 5))
            }

            if (update.eventType === 'ApproverWorkloadAlert') {
                const approverId = parseString(payload.approverId)
                if (!approverId) return
                const entry: ApproverWorkloadAlert = {
                    approverId,
                    decisionsInHour: parseNumber(payload.decisionsInHour) ?? 0,
                    approvedCount: parseNumber(payload.approvedCount) ?? 0,
                    rejectedCount: parseNumber(payload.rejectedCount) ?? 0,
                    workloadLevel: parseString(payload.workloadLevel) || 'medium',
                    detectionTimestamp: parseString(payload.detectionTimestamp) || new Date().toISOString(),
                }
                setLiveApproverWorkloads(prev => [entry, ...prev.filter(item => item.approverId !== approverId)].slice(0, 5))
            }

            if (update.eventType === 'DeliverySuccessRateDegraded') {
                const entry: DeliverySuccessRateAlert = {
                    totalAttempts: parseNumber(payload.totalAttempts) ?? 0,
                    successCount: parseNumber(payload.successCount) ?? 0,
                    failedCount: parseNumber(payload.failedCount) ?? 0,
                    successRatePercent: parseNumber(payload.successRatePercent) ?? 0,
                    detectionTimestamp: parseString(payload.detectionTimestamp) || new Date().toISOString(),
                }
                setLiveDeliverySuccessRates(prev => [entry, ...prev].slice(0, 5))
            }
        }

        dashboardHubService.subscribe(handleUpdate)

        dashboardHubService.connect()
            .then(() => {
                if (isActive) {
                    setLiveStatus('connected')
                }
            })
            .catch(() => {
                if (isActive) {
                    setLiveStatus('error')
                    setLiveError('Live Drasi updates are unavailable.')
                }
            })

        return () => {
            isActive = false
            dashboardHubService.unsubscribe()
            dashboardHubService.disconnect()
            signalRInitializedRef.current = false
        }
    }, [])

    const getPatternColor = (pattern: string): 'subtle' | 'brand' | 'danger' | 'important' | 'informative' | 'severe' | 'success' | 'warning' => {
        const normalized = pattern
            .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
            .replace(/[\s-]+/g, '_')
            .toLowerCase()
        const colorMap: Record<string, 'subtle' | 'brand' | 'danger' | 'important' | 'informative' | 'severe' | 'success' | 'warning'> = {
            geographic_cluster: 'danger',
            regional_hotspot: 'severe',
            severity_escalation: 'warning',
            rate_spike: 'important',
            duplicate_suppression: 'informative',
            area_expansion_suggestion: 'brand',
        }
        return colorMap[normalized] ?? 'subtle'
    }

    const getDecisionColor = (severity: DecisionSeverity): 'subtle' | 'brand' | 'danger' | 'important' | 'informative' | 'severe' | 'success' | 'warning' => {
        const map: Record<DecisionSeverity, 'subtle' | 'brand' | 'danger' | 'important' | 'informative' | 'severe' | 'success' | 'warning'> = {
            critical: 'severe',
            high: 'danger',
            medium: 'warning',
            low: 'informative',
            info: 'subtle',
        }
        return map[severity]
    }

    const formatDecisionTime = (timestamp: string) => {
        const date = new Date(timestamp)
        return Number.isNaN(date.getTime()) ? 'Just now' : date.toLocaleTimeString()
    }

    const formatPatternLabel = (pattern: string) => {
        return pattern.replace(/_/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2')
    }

    const formatRegionName = (regionCode: string) => {
        const regionNames: Record<string, string> = {
            'NZ-AKL': 'Auckland',
            'NZ-WGN': 'Wellington',
            'NZ-CHC': 'Christchurch',
            'NZ-HLZ': 'Hamilton',
            'NZ-TRG': 'Tauranga',
        }
        return regionNames[regionCode] ?? regionCode
    }

    const handleGenerateDemo = async () => {
        setIsDemoLoading(true)
        try {
            await post('/api/v1/demo/run-showcase')
            // Reload dashboard to show new alerts and wait for Drasi decisions
            setTimeout(() => {
                loadDashboard()
            }, 1000)
        } catch (err) {
            const message = err instanceof Error ? err.message : 'Error generating demo alerts'
            setError(message)
        } finally {
            setIsDemoLoading(false)
        }
    }

    const approvalTimeouts = (summary?.approvalTimeouts?.length ? summary.approvalTimeouts : liveApprovalTimeouts) ?? []
    const slaBreaches = (summary?.slaBreaches?.length ? summary.slaBreaches : liveSlaBreaches) ?? []

    return (
        <Layout>
            <div className={styles.root}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: tokens.spacingHorizontalL }}>
                    <div>
                        <h1 className={styles.title}>Dashboard</h1>
                        <Text size={200} as="p">
                            System overview and key metrics
                            {isRefreshing ? ' · refreshing…' : ''}
                        </Text>
                    </div>
                    <Button
                        appearance="primary"
                        size="large"
                        onClick={handleGenerateDemo}
                        disabled={isDemoLoading || isLoading}
                        style={{
                            whiteSpace: 'nowrap',
                            marginTop: tokens.spacingVerticalL,
                        }}
                    >
                        {isDemoLoading ? '🎬 Generating...' : '🎬 DEMO'}
                    </Button>
                </div>

                {error && <div className={styles.error}>{error}</div>}

                {isLoading ? (
                    <div className={styles.loading}>
                        <Spinner label="Loading dashboard..." />
                    </div>
                ) : (
                    <>
                        {summary && (
                            <>
                                <div>
                                    <h2>Summary</h2>
                                    <div className={styles.summaryGrid}>
                                        <div className={styles.summaryCard}>
                                            <div className={styles.summaryValue}>{summary.totalAlerts}</div>
                                            <div className={styles.summaryLabel}>Total Alerts</div>
                                        </div>
                                        <div className={styles.summaryCard}>
                                            <div className={styles.summaryValue} style={{ color: tokens.colorPaletteDarkOrangeForeground1 }}>
                                                {summary.pendingApprovals}
                                            </div>
                                            <div className={styles.summaryLabel}>Pending Approvals</div>
                                        </div>
                                        <div className={styles.summaryCard}>
                                            <div className={styles.summaryValue} style={{ color: tokens.colorPaletteRedForeground1 }}>
                                                {summary.deliveryFailures}
                                            </div>
                                            <div className={styles.summaryLabel}>Delivery Failures</div>
                                        </div>
                                        <div className={styles.summaryCard}>
                                            <div className={styles.summaryValue} style={{ color: tokens.colorPaletteGreenForeground1 }}>
                                                {summary.deliverySuccessRatePercent !== undefined
                                                    ? `${summary.deliverySuccessRatePercent.toFixed(1)}%`
                                                    : 'N/A'}
                                            </div>
                                            <div className={styles.summaryLabel}>Delivery Success Rate (1h)</div>
                                        </div>
                                        <div className={styles.summaryCard}>
                                            <div className={styles.summaryValue} style={{ color: tokens.colorPaletteRedForeground1 }}>
                                                {summary.slaBreach}
                                            </div>
                                            <div className={styles.summaryLabel}>SLA Breaches</div>
                                        </div>
                                        <div className={styles.summaryCard}>
                                            <div className={styles.summaryValue}>{summary.correlatedAlerts}</div>
                                            <div className={styles.summaryLabel}>Correlated Alerts</div>
                                        </div>
                                    </div>
                                </div>

                                <div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                        <h2>Operational Insights</h2>
                                        {summary.pendingApprovals > 0 && (
                                            <Button appearance="primary" size="small" onClick={() => navigate('/approvals')}>
                                                Review approvals
                                            </Button>
                                        )}
                                    </div>
                                        <div className={styles.insightGrid}>
                                        <div className={styles.insightCard}>
                                            <Text weight="semibold">Approval timeouts</Text>
                                            {approvalTimeouts.length > 0 ? (
                                                <ul className={styles.insightList}>
                                                    {approvalTimeouts.slice(0, 5).map(timeout => (
                                                        <li key={timeout.alertId}>
                                                            <Text size={200}>
                                                                {timeout.headline} · {timeout.elapsedMinutes} min
                                                            </Text>
                                                        </li>
                                                    ))}
                                                </ul>
                                            ) : (
                                                <Text size={200}>No approval timeouts detected.</Text>
                                            )}
                                        </div>
                                        <div className={styles.insightCard}>
                                            <Text weight="semibold">Delivery retry storms</Text>
                                            {liveRetryStorms.length > 0 ? (
                                                <ul className={styles.insightList}>
                                                    {liveRetryStorms.slice(0, 5).map(storm => (
                                                        <li key={storm.alertId}>
                                                            <Text size={200}>
                                                                {storm.headline} · {storm.failedAttemptCount} failures
                                                            </Text>
                                                        </li>
                                                    ))}
                                                </ul>
                                            ) : (
                                                <Text size={200}>No retry storms detected.</Text>
                                            )}
                                        </div>
                                        <div className={styles.insightCard}>
                                            <Text weight="semibold">SLA breaches</Text>
                                            {slaBreaches.length > 0 ? (
                                                <ul className={styles.insightList}>
                                                    {slaBreaches.slice(0, 5).map(breach => (
                                                        <li key={breach.alertId}>
                                                            <Text size={200}>
                                                                {breach.headline} · {breach.elapsedSeconds} sec
                                                            </Text>
                                                        </li>
                                                    ))}
                                                </ul>
                                            ) : (
                                                <Text size={200}>No SLA breaches detected.</Text>
                                            )}
                                        </div>
                                        <div className={styles.insightCard}>
                                            <Text weight="semibold">Approver workload</Text>
                                            {liveApproverWorkloads.length > 0 ? (
                                                <ul className={styles.insightList}>
                                                    {liveApproverWorkloads.slice(0, 5).map(workload => (
                                                        <li key={workload.approverId}>
                                                            <Text size={200}>
                                                                {workload.approverId} · {workload.decisionsInHour} decisions
                                                            </Text>
                                                        </li>
                                                    ))}
                                                </ul>
                                            ) : (
                                                <Text size={200}>No workload alerts.</Text>
                                            )}
                                        </div>
                                        <div className={styles.insightCard} style={{ 
                                            borderLeft: liveSlaCountdowns.length > 0 ? `3px solid ${tokens.colorPaletteRedForeground1}` : undefined,
                                            background: liveSlaCountdowns.length > 0 ? tokens.colorPaletteRedBackground1 : undefined
                                        }}>
                                            <Text weight="semibold">⏱️ SLA countdowns (Drasi predictive)</Text>
                                            {liveSlaCountdowns.length > 0 ? (
                                                <ul className={styles.insightList} style={{ paddingLeft: 0, listStyle: 'none' }}>
                                                    {liveSlaCountdowns.slice(0, 5).map(countdown => (
                                                        <li key={countdown.alertId} style={{ marginBottom: tokens.spacingVerticalS }}>
                                                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                                                <Text size={200} style={{ flex: 1 }}>
                                                                    {countdown.headline}
                                                                </Text>
                                                                <Badge 
                                                                    appearance="filled" 
                                                                    color={countdown.secondsRemaining <= 15 ? 'danger' : countdown.secondsRemaining <= 30 ? 'warning' : 'informative'}
                                                                    style={{ fontFamily: 'monospace', fontSize: '14px', minWidth: '50px', textAlign: 'center' }}
                                                                >
                                                                    {countdown.secondsRemaining}s
                                                                </Badge>
                                                            </div>
                                                            <div style={{ 
                                                                height: '4px', 
                                                                background: tokens.colorNeutralBackground4,
                                                                borderRadius: '2px',
                                                                marginTop: '4px',
                                                                overflow: 'hidden'
                                                            }}>
                                                                <div style={{
                                                                    height: '100%',
                                                                    width: `${Math.max(0, Math.min(100, (countdown.secondsRemaining / 60) * 100))}%`,
                                                                    background: countdown.secondsRemaining <= 15 
                                                                        ? tokens.colorPaletteRedForeground1 
                                                                        : countdown.secondsRemaining <= 30 
                                                                            ? tokens.colorPaletteDarkOrangeForeground1 
                                                                            : tokens.colorPaletteGreenForeground1,
                                                                    transition: 'width 1s linear, background 0.3s'
                                                                }} />
                                                            </div>
                                                        </li>
                                                    ))}
                                                </ul>
                                            ) : (
                                                <Text size={200}>No alerts approaching SLA breach.</Text>
                                            )}
                                        </div>
                                        <div className={styles.insightCard}>
                                            <Text weight="semibold">Delivery success rate</Text>
                                            {liveDeliverySuccessRates.length > 0 ? (
                                                <ul className={styles.insightList}>
                                                    {liveDeliverySuccessRates.slice(0, 5).map((rate, index) => (
                                                        <li key={`${rate.detectionTimestamp}-${index}`}>
                                                            <Text size={200}>
                                                                {rate.successRatePercent.toFixed(1)}% success ({rate.successCount}/{rate.totalAttempts})
                                                            </Text>
                                                        </li>
                                                    ))}
                                                </ul>
                                            ) : (
                                                <Text size={200}>No delivery degradation detected.</Text>
                                            )}
                                        </div>
                                    </div>
                                </div>

                                <div>
                                    <div className={styles.decisionHeader}>
                                        <h2>Drasi decision feed</h2>
                                        <div className={styles.liveStatus}>
                                            <Badge
                                                appearance="outline"
                                                color={liveStatus === 'connected' ? 'success' : liveStatus === 'error' ? 'danger' : 'warning'}
                                            >
                                                {liveStatus === 'connected' ? 'Live' : liveStatus === 'error' ? 'Offline' : 'Connecting'}
                                            </Badge>
                                            <Text size={200}>
                                                {liveStatus === 'connected' ? 'Streaming signals' : liveStatus === 'error' ? 'Fallback mode' : 'Connecting'}
                                            </Text>
                                        </div>
                                    </div>
                                    {liveError && <div className={styles.error}>{liveError}</div>}
                                    <div className={styles.decisionGrid}>
                                        {decisionFeed.length === 0 ? (
                                            <Card className={styles.decisionCard}>
                                                <CardHeader header={<Text weight="semibold">No live decisions yet</Text>} />
                                                <Text size={200}>
                                                    Drasi will surface correlations, SLA breaches, and approval timeouts here.
                                                </Text>
                                            </Card>
                                        ) : (
                                            decisionFeed.map(decision => (
                                                <Card key={decision.id} className={styles.decisionCard}>
                                                    <div className={styles.decisionMeta}>
                                                        <Badge appearance="filled" color={getDecisionColor(decision.severity)}>
                                                            {decision.severity.toUpperCase()}
                                                        </Badge>
                                                        <span>{formatDecisionTime(decision.timestamp)}</span>
                                                    </div>
                                                    <Text weight="semibold">{decision.title}</Text>
                                                    <Text size={200}>{decision.detail}</Text>
                                                    {decision.action && (
                                                        <CardFooter>
                                                            <Text size={200}>Recommended: {decision.action}</Text>
                                                        </CardFooter>
                                                    )}
                                                </Card>
                                            ))
                                        )}
                                    </div>
                                </div>

                                {correlations.length > 0 && (
                                    <div>
                                        <h2>Detected Correlations</h2>
                                        <div className={styles.correlationGrid}>
                                            {correlations.map((correlation, index) => (
                                                <div key={index} className={styles.correlationCard}>
                                                    <div>
                                                        <Badge
                                                            appearance="filled"
                                                            color={getPatternColor(correlation.patternType)}
                                                        >
                                                            {formatPatternLabel(correlation.patternType)}
                                                        </Badge>
                                                    </div>
                                                    {correlation.regionCode && (
                                                        <Text size={200} weight="semibold" style={{ marginTop: tokens.spacingVerticalS }}>
                                                            {formatRegionName(correlation.regionCode)}
                                                        </Text>
                                                    )}
                                                    <Text size={200} style={{ marginTop: tokens.spacingVerticalXS }}>
                                            {(correlation.alertIds ?? []).length} alert{(correlation.alertIds ?? []).length !== 1 ? 's' : ''} involved
                                                    </Text>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                )}
                            </>
                        )}
                    </>
                )}
            </div>
        </Layout>
    )
}

export default DashboardPage
