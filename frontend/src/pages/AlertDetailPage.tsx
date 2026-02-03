import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
    Button,
    Spinner,
    Badge,
    Card,
    CardHeader,
    CardFooter,
    makeStyles,
    tokens,
    Text,
} from '@fluentui/react-components'
import MapViewerAzure from '../components/MapViewerAzure'
import Layout from '../components/Layout'
import ApprovalForm from '../components/ApprovalForm/ApprovalForm'
import CancelDialog from '../components/ApprovalForm/CancelDialog'
import alertApi from '../services/alertApi'
import type { Alert } from '../types'

const useAlertDetailStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXL,
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'start',
        gap: tokens.spacingHorizontalXXL,
        flexWrap: 'wrap',
    },
    title: {
        fontSize: tokens.fontSizeBase600,
        fontWeight: tokens.fontWeightBold,
        margin: 0,
    },
    detailsCard: {
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        boxShadow: '0 16px 30px rgba(20, 27, 76, 0.08)',
    },
    detailsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))',
        gap: tokens.spacingHorizontalL,
    },
    detailItem: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    label: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase100,
        fontWeight: tokens.fontWeightSemibold,
    },
    value: {
        fontSize: tokens.fontSizeBase200,
    },
    loading: {
        display: 'flex',
        justifyContent: 'center',
        padding: tokens.spacingHorizontalXXL,
    },
    error: {
        color: tokens.colorStatusDangerForeground1,
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorStatusDangerBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
    actions: {
        display: 'flex',
        gap: tokens.spacingHorizontalL,
    },
    pillRow: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalS,
        marginTop: tokens.spacingVerticalS,
    },
    distributionGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: tokens.spacingHorizontalL,
        marginTop: tokens.spacingVerticalM,
    },
    distributionItem: {
        padding: tokens.spacingHorizontalM,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground1,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
})

export const AlertDetailPage = () => {
    const styles = useAlertDetailStyles()
    const { alertId } = useParams<{ alertId: string }>()
    const navigate = useNavigate()
    const [alert, setAlert] = useState<Alert | null>(null)
    const [isLoading, setIsLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        if (!alertId) {
            navigate('/alerts')
            return
        }

        const loadAlert = async () => {
            setIsLoading(true)
            setError(null)
            try {
                const data = await alertApi.getAlert(alertId)
                setAlert(data)
            } catch (err) {
                const error = err as { detail?: string }
                setError(error?.detail || 'Failed to load alert')
            } finally {
                setIsLoading(false)
            }
        }

        loadAlert()
    }, [alertId, navigate])

    const getStatusBadge = (status: string) => {
        const statusMap: Record<string, { color: 'subtle' | 'brand' | 'danger' | 'important' | 'informative' | 'severe' | 'success' | 'warning'; label: string }> = {
            draft: { color: 'subtle', label: 'Draft' },
            pending_approval: { color: 'warning', label: 'Pending' },
            approved: { color: 'success', label: 'Approved' },
            rejected: { color: 'danger', label: 'Rejected' },
            delivered: { color: 'success', label: 'Delivered' },
            cancelled: { color: 'subtle', label: 'Cancelled' },
            expired: { color: 'subtle', label: 'Expired' },
        }
        const config = statusMap[status] || { color: 'subtle' as const, label: status }
        return <Badge appearance="filled" color={config.color}>{config.label}</Badge>
    }

    const getDeliveryBadge = (status: string) => {
        const statusMap: Record<string, { color: 'subtle' | 'brand' | 'danger' | 'important' | 'informative' | 'severe' | 'success' | 'warning'; label: string }> = {
            pending: { color: 'informative', label: 'Delivery pending' },
            delivered: { color: 'success', label: 'Delivered' },
            failed: { color: 'danger', label: 'Failed' },
        }
        const config = statusMap[status] || { color: 'subtle' as const, label: status }
        return <Badge appearance="outline" color={config.color}>{config.label}</Badge>
    }

    const handleApprovalSuccess = () => {
        if (alertId) {
            alertApi.getAlert(alertId).then(setAlert)
        }
    }

    if (isLoading) {
        return (
            <Layout>
                <div className={styles.loading}>
                    <Spinner label="Loading alert..." />
                </div>
            </Layout>
        )
    }

    if (error || !alert) {
        return (
            <Layout>
                <div className={styles.root}>
                    <div className={styles.error}>{error || 'Alert not found'}</div>
                    <Button appearance="secondary" onClick={() => navigate('/alerts')}>
                        Back to Alerts
                    </Button>
                </div>
            </Layout>
        )
    }

    return (
        <Layout>
            <div className={styles.root}>
                <div className={styles.header}>
                    <div>
                        <h1 className={styles.title}>{alert.headline}</h1>
                        <div className={styles.pillRow}>
                            {getStatusBadge(alert.status)}
                            <Badge appearance="outline">{alert.severity}</Badge>
                            <Badge appearance="outline">{alert.channelType}</Badge>
                            {getDeliveryBadge(alert.deliveryStatus)}
                        </div>
                    </div>
                    <div className={styles.actions}>
                        <Button appearance="secondary" onClick={() => navigate('/alerts')}>
                            Back
                        </Button>
                    </div>
                </div>

                <Card className={styles.detailsCard}>
                    <CardHeader header={<Text weight="semibold">Alert details</Text>} />
                    <div className={styles.detailsGrid}>
                        <div className={styles.detailItem}>
                            <div className={styles.label}>Description</div>
                            <div className={styles.value}>{alert.description}</div>
                        </div>
                        <div className={styles.detailItem}>
                            <div className={styles.label}>Severity</div>
                            <div className={styles.value}>{alert.severity}</div>
                        </div>
                        <div className={styles.detailItem}>
                            <div className={styles.label}>Channel Type</div>
                            <div className={styles.value}>{alert.channelType}</div>
                        </div>
                        <div className={styles.detailItem}>
                            <div className={styles.label}>Created At</div>
                            <div className={styles.value}>{new Date(alert.createdAt || '').toLocaleString()}</div>
                        </div>
                        <div className={styles.detailItem}>
                            <div className={styles.label}>Expires At</div>
                            <div className={styles.value}>{new Date(alert.expiresAt).toLocaleString()}</div>
                        </div>
                        <div className={styles.detailItem}>
                            <div className={styles.label}>Number of Areas</div>
                            <div className={styles.value}>{alert.areas.length}</div>
                        </div>
                    </div>
                </Card>

                <Card className={styles.detailsCard}>
                    <CardHeader header={<Text weight="semibold">Automated distribution</Text>} />
                    <Text size={200}>
                        EAS broadcast is represented for operational visibility. Email delivery is the only active channel in this build.
                    </Text>
                    <div className={styles.distributionGrid}>
                        <div className={styles.distributionItem}>
                            <Text weight="semibold">EAS broadcast</Text>
                            <Badge appearance="outline">Simulated</Badge>
                            <Text size={200}>Displayed for situational awareness and decisioning.</Text>
                        </div>
                        <div className={styles.distributionItem}>
                            <Text weight="semibold">Email alerts</Text>
                            {getDeliveryBadge(alert.deliveryStatus)}
                            <Text size={200}>Delivered to configured recipients via ACS email.</Text>
                        </div>
                        <div className={styles.distributionItem}>
                            <Text weight="semibold">Push notifications</Text>
                            <Badge appearance="outline">Not configured</Badge>
                            <Text size={200}>Requires device registration and a push provider.</Text>
                        </div>
                    </div>
                    <CardFooter>
                        <Text size={200}>Delivery status updates flow into Drasi decisioning.</Text>
                    </CardFooter>
                </Card>

                {alert.areas.length > 0 && (
                    <Card className={styles.detailsCard}>
                        <CardHeader header={<Text weight="semibold">Geographic areas</Text>} />
                        <MapViewerAzure polygons={alert.areas.map(a => a.polygon)} />
                        {alert.areas.map((area, index) => (
                            <div key={index} className={styles.detailItem}>
                                <div className={styles.label}>{area.areaDescription}</div>
                                <div className={styles.value}>
                                    {area.regionCode ? `Region: ${area.regionCode}` : 'Custom area'}
                                </div>
                            </div>
                        ))}
                    </Card>
                )}

                {alert.status === 'pending_approval' && (
                    <Card className={styles.detailsCard}>
                        <CardHeader header={<Text weight="semibold">Approval required</Text>} />
                        <ApprovalForm
                            alertId={alertId!}
                            onApproved={handleApprovalSuccess}
                            onRejected={handleApprovalSuccess}
                        />
                    </Card>
                )}

                {(alert.status === 'pending_approval' || alert.status === 'approved') && (
                    <Card className={styles.detailsCard}>
                        <CardHeader header={<Text weight="semibold">Cancel alert</Text>} />
                        <CancelDialog alertId={alertId!} onCancelled={handleApprovalSuccess} />
                    </Card>
                )}
            </div>
        </Layout>
    )
}

export default AlertDetailPage
