import { useState } from 'react'
import {
    Dialog,
    DialogTrigger,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogActions,
    Button,
    Input,
    makeStyles,
    tokens,
} from '@fluentui/react-components'
import alertApi from '../../services/alertApi'

const useCancelDialogStyles = makeStyles({
    error: {
        color: tokens.colorPaletteRedForeground1,
        marginTop: tokens.spacingVerticalL,
    },
})

interface CancelDialogProps {
    alertId: string
    onCancelled?: () => void
    triggerButtonLabel?: string
}

export const CancelDialog = ({
    alertId,
    onCancelled,
    triggerButtonLabel = 'Cancel Alert',
}: CancelDialogProps) => {
    const styles = useCancelDialogStyles()
    const [reason, setReason] = useState('')
    const [isLoading, setIsLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [isOpen, setIsOpen] = useState(false)

    const handleCancel = async () => {
        setIsLoading(true)
        setError(null)
        try {
            await alertApi.cancelAlert(alertId)
            setIsOpen(false)
            setReason('')
            onCancelled?.()
        } catch (err) {
            const error = err as { detail?: string }
            setError(error?.detail || 'Failed to cancel alert')
        } finally {
            setIsLoading(false)
        }
    }

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => setIsOpen(data.open)}>
            <DialogTrigger disableButtonEnhancement>
                <Button appearance="secondary">{triggerButtonLabel}</Button>
            </DialogTrigger>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>Cancel Alert</DialogTitle>
                    <div style={{ marginBottom: tokens.spacingVerticalL }}>
                        <p>Are you sure you want to cancel this alert?</p>
                        <Input
                            placeholder="Enter reason (optional)"
                            value={reason}
                            onChange={(e) => setReason(e.target.value)}
                        />
                        {error && <div className={styles.error}>{error}</div>}
                    </div>
                    <DialogActions>
                        <Button appearance="secondary" onClick={() => setIsOpen(false)}>
                            Keep Alert
                        </Button>
                        <Button
                            appearance="primary"
                            onClick={handleCancel}
                            disabled={isLoading}
                        >
                            {isLoading ? 'Cancelling...' : 'Cancel Alert'}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    )
}

export default CancelDialog
