import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
    Badge,
    Button,
    Card,
    CardHeader,
    CardFooter,
    Dialog,
    DialogActions,
    DialogBody,
    DialogSurface,
    DialogTitle,
    Label,
    Spinner,
    Text,
    Textarea,
    Avatar,
    makeStyles,
    tokens,
} from '@fluentui/react-components'
import Layout from '../components/Layout'
import alertApi from '../services/alertApi'
import type { Alert } from '../types'

const useApprovalsStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXL,
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalL,
    },
    cardGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
        gap: tokens.spacingHorizontalL,
    },
    approvalCard: {
        padding: tokens.spacingHorizontalL,
        borderRadius: tokens.borderRadiusLarge,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        boxShadow: '0 16px 30px rgba(20, 27, 76, 0.08)',
        transition: 'transform 0.2s ease, box-shadow 0.2s ease',
        ':hover': {
            transform: 'translateY(-2px)',
            boxShadow: '0 22px 40px rgba(20, 27, 76, 0.12)',
        },
    },
    cardHeader: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    metaRow: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalS,
        marginTop: tokens.spacingVerticalS,
    },
    infoRow: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalM,
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase100,
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
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        marginTop: tokens.spacingVerticalS,
    },
    mutedText: {
        color: tokens.colorNeutralForeground3,
    },
})

export const ApprovalsPage = () => {
    const styles = useApprovalsStyles()
    const navigate = useNavigate()
    const [approvals, setApprovals] = useState<Alert[]>([])
    const [isLoading, setIsLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [page, setPage] = useState(1)

    const [isDialogOpen, setIsDialogOpen] = useState(false)
    const [selectedAlert, setSelectedAlert] = useState<Alert | null>(null)
    const [rejectionReason, setRejectionReason] = useState('')
    const [actionError, setActionError] = useState<string | null>(null)
    const [isSubmitting, setIsSubmitting] = useState(false)

    const pendingApprovals = approvals.filter(alert => alert.status === 'pending_approval')

    const loadApprovals = useCallback(async () => {
        setIsLoading(true)
        setError(null)
        try {
            const result = await alertApi.listAlerts(page, 20, { status: 'PendingApproval' })
            setApprovals(result.items)
        } catch (err) {
            const error = err as { detail?: string }
            setError(error?.detail || 'Failed to load approvals')
        } finally {
            setIsLoading(false)
        }
    }, [page])

    useEffect(() => {
        loadApprovals()
    }, [loadApprovals])

    const handleApprove = async (alertId?: string) => {
        if (!alertId) {
            return
        }
        setIsSubmitting(true)
        setActionError(null)
        try {
            await alertApi.approveAlert(alertId)
            await loadApprovals()
        } catch (err) {
            const error = err as { detail?: string }
            setActionError(error?.detail || 'Failed to approve alert')
        } finally {
            setIsSubmitting(false)
        }
    }

    const handleReject = (alert: Alert) => {
        setSelectedAlert(alert)
        setRejectionReason('')
        setActionError(null)
        setIsDialogOpen(true)
    }

    const confirmReject = async () => {
        if (!selectedAlert?.alertId) {
            return
        }
        const trimmedReason = rejectionReason.trim()
        if (!trimmedReason) {
            setActionError('Rejection reason is required')
            return
        }
        setIsSubmitting(true)
        setActionError(null)
        try {
            await alertApi.rejectAlert(selectedAlert.alertId, { rejectionReason: trimmedReason })
            setIsDialogOpen(false)
            setSelectedAlert(null)
            setRejectionReason('')
            await loadApprovals()
        } catch (err) {
            const error = err as { detail?: string }
            setActionError(error?.detail || 'Failed to reject alert')
        } finally {
            setIsSubmitting(false)
        }
    }

    const handleViewAlert = (alertId?: string) => {
        if (alertId) {
            navigate(`/alerts/${alertId}`)
        }
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

    return (
        <Layout>
            <div className={styles.root}>
                <div className={styles.header}>
                    <div>
                        <h2>Approvals</h2>
                        <Text size={200} as="p">Review and approve pending alerts</Text>
                    </div>
                    <Badge appearance="outline">{pendingApprovals.length} pending</Badge>
                </div>

                {error && <div className={styles.error}>{error}</div>}
                {actionError && <div className={styles.error}>{actionError}</div>}

                {isLoading ? (
                    <div className={styles.loading}>
                        <Spinner label="Loading approvals..." />
                    </div>
                ) : (
                    <div className={styles.cardGrid}>
                        {pendingApprovals.length === 0 ? (
                            <Card className={styles.approvalCard}>
                                <Text>No pending approvals</Text>
                            </Card>
                        ) : (
                            pendingApprovals.map(alert => (
                                <Card key={alert.alertId} className={styles.approvalCard}>
                                    <CardHeader
                                        image={<Avatar name={alert.headline} />}
                                        header={
                                            <div className={styles.cardHeader}>
                                                <Text weight="semibold">{alert.headline}</Text>
                                                <Text size={200} className={styles.mutedText}>
                                                    {alert.description?.slice(0, 96) || 'No description provided'}
                                                    {alert.description && alert.description.length > 96 ? '…' : ''}
                                                </Text>
                                            </div>
                                        }
                                        action={<Badge appearance="filled" color="warning">Pending</Badge>}
                                    />
                                    <div className={styles.metaRow}>
                                        <Badge appearance="outline">{alert.severity}</Badge>
                                        <Badge appearance="outline">{alert.channelType}</Badge>
                                        <Badge appearance="outline">EAS simulated</Badge>
                                        {getDeliveryBadge(alert.deliveryStatus)}
                                    </div>
                                    <div className={styles.infoRow}>
                                        <span>Created: {new Date(alert.createdAt || '').toLocaleString()}</span>
                                        <span>Expires: {new Date(alert.expiresAt).toLocaleString()}</span>
                                        <span>Distribution: EAS + Email</span>
                                    </div>
                                    <CardFooter>
                                        <div className={styles.actions}>
                                            <Button
                                                appearance="primary"
                                                size="small"
                                                disabled={isSubmitting}
                                                onClick={() => handleApprove(alert.alertId)}
                                            >
                                                Approve
                                            </Button>
                                            <Button
                                                appearance="secondary"
                                                size="small"
                                                disabled={isSubmitting}
                                                onClick={() => handleReject(alert)}
                                            >
                                                Reject
                                            </Button>
                                            <Button
                                                appearance="subtle"
                                                size="small"
                                                onClick={() => handleViewAlert(alert.alertId)}
                                            >
                                                View
                                            </Button>
                                        </div>
                                    </CardFooter>
                                </Card>
                            ))
                        )}
                    </div>
                )}

                <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: tokens.spacingVerticalXXL }}>
                    <Button
                        appearance="secondary"
                        onClick={() => setPage(p => Math.max(1, p - 1))}
                        disabled={page === 1}
                    >
                        Previous
                    </Button>
                    <Text>{`Page ${page}`}</Text>
                    <Button
                        appearance="secondary"
                        onClick={() => setPage(p => p + 1)}
                        disabled={approvals.length === 0}
                    >
                        Next
                    </Button>
                </div>
            </div>

            <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>Reject Alert</DialogTitle>
                        <div className={styles.dialogContent}>
                            <Text size={200}>Provide a reason for rejecting this alert.</Text>
                            <div>
                                <Label htmlFor="approval-reject-reason">Rejection reason</Label>
                                <Textarea
                                    id="approval-reject-reason"
                                    value={rejectionReason}
                                    onChange={(_, data) => setRejectionReason(data.value)}
                                    placeholder="Reason for rejection"
                                    resize="vertical"
                                />
                            </div>
                        </div>
                        <DialogActions>
                            <Button appearance="secondary" onClick={() => setIsDialogOpen(false)}>
                                Cancel
                            </Button>
                            <Button appearance="primary" disabled={isSubmitting} onClick={confirmReject}>
                                {isSubmitting ? 'Rejecting...' : 'Reject Alert'}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </Layout>
    )
}

export default ApprovalsPage
