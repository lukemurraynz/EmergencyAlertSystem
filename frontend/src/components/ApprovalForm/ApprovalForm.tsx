import { useState, useCallback } from 'react'
import {
    makeStyles,
    tokens,
    Button,
    Spinner,
    Textarea,
    Label,
} from '@fluentui/react-components'
import alertApi from '../../services/alertApi'
import type { ApiError } from '../../types'

const useApprovalFormStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    buttonGroup: {
        display: 'flex',
        gap: tokens.spacingHorizontalL,
        flexWrap: 'wrap',
    },
    error: {
        color: tokens.colorStatusDangerForeground1,
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorStatusDangerBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
    success: {
        color: tokens.colorPaletteGreenForeground1,
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorStatusSuccessBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
})

interface ApprovalFormProps {
    alertId: string
    onApproved?: () => void
    onRejected?: () => void
    etag?: string
}

export const ApprovalForm = ({ alertId, onApproved, onRejected, etag }: ApprovalFormProps) => {
    const styles = useApprovalFormStyles()
    const [isLoading, setIsLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [success, setSuccess] = useState<string | null>(null)
    const [rejectionReason, setRejectionReason] = useState('')
    const [showRejectForm, setShowRejectForm] = useState(false)

    const handleApprove = useCallback(async () => {
        setIsLoading(true)
        setError(null)
        setSuccess(null)
        try {
            await alertApi.approveAlert(alertId, etag)
            setSuccess('Alert approved successfully')
            onApproved?.()
        } catch (err) {
            const error = err as { detail?: string }
            setError(error?.detail || 'Failed to approve alert')
        } finally {
            setIsLoading(false)
        }
    }, [alertId, etag, onApproved])

    const handleReject = useCallback(async () => {
        setIsLoading(true)
        setError(null)
        setSuccess(null)
        const trimmedReason = rejectionReason.trim()
        if (!trimmedReason) {
            setError('Rejection reason is required')
            setIsLoading(false)
            return
        }
        try {
            await alertApi.rejectAlert(alertId, { rejectionReason: trimmedReason }, etag)
            setSuccess('Alert rejected successfully')
            setShowRejectForm(false)
            setRejectionReason('')
            onRejected?.()
        } catch (err) {
            const error = err as ApiError
            setError(error?.detail || 'Failed to reject alert')
        } finally {
            setIsLoading(false)
        }
    }, [alertId, rejectionReason, etag, onRejected])

    return (
        <div className={styles.root}>
            {error && <div className={styles.error}>{error}</div>}
            {success && <div className={styles.success}>{success}</div>}

            {!showRejectForm ? (
                <div className={styles.buttonGroup}>
                    <Button
                        appearance="primary"
                        disabled={isLoading}
                        onClick={handleApprove}
                    >
                        {isLoading ? <Spinner size="small" /> : 'Approve'}
                    </Button>
                    <Button
                        appearance="secondary"
                        disabled={isLoading}
                        onClick={() => setShowRejectForm(true)}
                    >
                        Reject
                    </Button>
                </div>
            ) : (
                <div className={styles.root}>
                    <div>
                        <Label htmlFor="rejection-reason">Rejection reason</Label>
                        <Textarea
                            id="rejection-reason"
                            value={rejectionReason}
                            onChange={(_, data) => setRejectionReason(data.value)}
                            placeholder="Provide a reason for rejecting this alert"
                            resize="vertical"
                        />
                    </div>
                    <div className={styles.buttonGroup}>
                        <Button
                            appearance="primary"
                            disabled={isLoading}
                            onClick={handleReject}
                        >
                            {isLoading ? <Spinner size="small" /> : 'Confirm Rejection'}
                        </Button>
                        <Button
                            appearance="secondary"
                            disabled={isLoading}
                            onClick={() => {
                                setShowRejectForm(false)
                                setRejectionReason('')
                            }}
                        >
                            Cancel
                        </Button>
                    </div>
                </div>
            )}
        </div>
    )
}

export default ApprovalForm
