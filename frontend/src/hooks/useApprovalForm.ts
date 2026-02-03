import { useState, useCallback } from 'react'
import alertApi from '../services/alertApi'

export interface ApprovalFormData {
    decision: 'approved' | 'rejected'
    rejectionReason?: string
}

export interface UseApprovalFormReturn {
    formData: ApprovalFormData
    errors: Record<string, string>
    isLoading: boolean
    updateDecision: (decision: 'approved' | 'rejected') => void
    updateReason: (reason: string) => void
    resetForm: () => void
    submitForm: (alertId: string, etag?: string) => Promise<boolean>
}

const initialFormData: ApprovalFormData = {
    decision: 'approved',
    rejectionReason: '',
}

export function useApprovalForm(): UseApprovalFormReturn {
    const [formData, setFormData] = useState<ApprovalFormData>(initialFormData)
    const [errors, setErrors] = useState<Record<string, string>>({})
    const [isLoading, setIsLoading] = useState(false)

    const updateDecision = useCallback((decision: 'approved' | 'rejected') => {
        setFormData(prev => ({ ...prev, decision }))
    }, [])

    const updateReason = useCallback((reason: string) => {
        setFormData(prev => ({ ...prev, rejectionReason: reason }))
    }, [])

    const resetForm = useCallback(() => {
        setFormData(initialFormData)
        setErrors({})
    }, [])

    const submitForm = useCallback(async (alertId: string, etag?: string): Promise<boolean> => {
        setIsLoading(true)
        const newErrors: Record<string, string> = {}

        if (formData.decision === 'rejected' && !formData.rejectionReason?.trim()) {
            newErrors.rejectionReason = 'Rejection reason is required'
        }

        setErrors(newErrors)

        if (Object.keys(newErrors).length > 0) {
            setIsLoading(false)
            return false
        }

        try {
            if (formData.decision === 'approved') {
                await alertApi.approveAlert(alertId, etag)
            } else {
                await alertApi.rejectAlert(
                    alertId,
                    { rejectionReason: formData.rejectionReason?.trim() ?? '' },
                    etag
                )
            }
            resetForm()
            return true
        } catch (error) {
            const err = error as { detail?: string }
            setErrors({
                submit: err?.detail || 'Failed to submit approval',
            })
            return false
        } finally {
            setIsLoading(false)
        }
    }, [formData, resetForm])

    return {
        formData,
        errors,
        isLoading,
        updateDecision,
        updateReason,
        resetForm,
        submitForm,
    }
}
