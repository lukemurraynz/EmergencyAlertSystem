import { useState, useCallback } from 'react'
import type { Alert, Area, AlertSeverity, AlertChannelType } from '../types'
import alertApi, { type CreateAlertRequest } from '../services/alertApi'

export interface AlertFormData {
    headline: string
    description: string
    severity: AlertSeverity
    channelType: AlertChannelType
    expiresAt: string
    languageCode: string
    areas: Area[]
}

const initialAlertFormData: AlertFormData = {
    headline: '',
    description: '',
    severity: 'Moderate',
    channelType: 'test',
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
    languageCode: 'en-GB',
    areas: [],
}

export interface UseAlertFormReturn {
    formData: AlertFormData
    errors: Record<string, string>
    isLoading: boolean
    isDirty: boolean
    updateField: <K extends keyof AlertFormData>(field: K, value: AlertFormData[K]) => void
    updateArea: (index: number, area: Area) => void
    addArea: (area: Area) => void
    removeArea: (index: number) => void
    resetForm: () => void
    submitForm: () => Promise<Alert | null>
    validateForm: () => boolean
}

export function useAlertForm(onSuccess?: (alert: Alert) => void): UseAlertFormReturn {
    const [formData, setFormData] = useState<AlertFormData>(initialAlertFormData)
    const [errors, setErrors] = useState<Record<string, string>>({})
    const [isLoading, setIsLoading] = useState(false)
    const [originalData, setOriginalData] = useState<AlertFormData>(initialAlertFormData)

    const isDirty = JSON.stringify(formData) !== JSON.stringify(originalData)

    const validateForm = useCallback((): boolean => {
        const newErrors: Record<string, string> = {}

        if (!formData.headline.trim()) {
            newErrors.headline = 'Headline is required'
        } else if (formData.headline.length > 100) {
            newErrors.headline = 'Headline must not exceed 100 characters'
        }

        if (!formData.description.trim()) {
            newErrors.description = 'Description is required'
        } else if (formData.description.length > 1395) {
            newErrors.description = 'Description exceeds maximum length'
        }

        if (new Date(formData.expiresAt) <= new Date()) {
            newErrors.expiresAt = 'Expiry time must be in the future'
        }

        if (formData.areas.length === 0) {
            newErrors.areas = 'At least one area is required'
        }

        setErrors(newErrors)
        return Object.keys(newErrors).length === 0
    }, [formData])

    const updateField = useCallback<UseAlertFormReturn['updateField']>((field, value) => {
        setFormData(prev => ({ ...prev, [field]: value }))
        if (errors[field]) {
            setErrors(prev => ({ ...prev, [field]: '' }))
        }
    }, [errors])

    const updateArea = useCallback((index: number, area: Area) => {
        setFormData(prev => {
            const newAreas = [...prev.areas]
            newAreas[index] = area
            return { ...prev, areas: newAreas }
        })
    }, [])

    const addArea = useCallback((area: Area) => {
        setFormData(prev => ({ ...prev, areas: [...prev.areas, area] }))
    }, [])

    const removeArea = useCallback((index: number) => {
        setFormData(prev => {
            const newAreas = prev.areas.filter((_, i) => i !== index)
            return { ...prev, areas: newAreas }
        })
    }, [])

    const resetForm = useCallback(() => {
        setFormData(initialAlertFormData)
        setOriginalData(initialAlertFormData)
        setErrors({})
    }, [])

    const submitForm = useCallback(async (): Promise<Alert | null> => {
        if (!validateForm()) {
            return null
        }

        setIsLoading(true)
        try {
            const request: CreateAlertRequest = {
                headline: formData.headline,
                description: formData.description,
                severity: formData.severity,
                channelType: formData.channelType,
                expiresAt: formData.expiresAt,
                languageCode: formData.languageCode,
                areas: formData.areas.map(area => ({
                    areaDescription: area.areaDescription,
                    geoJsonPolygon: JSON.stringify(area.polygon),
                })),
            }

            const result = await alertApi.createAlert(request)
            resetForm()
            onSuccess?.(result)
            return result
        } catch (error) {
            const apiError = error as { detail?: string }
            setErrors({
                submit: apiError?.detail || 'Failed to create alert',
            })
            return null
        } finally {
            setIsLoading(false)
        }
    }, [formData, validateForm, resetForm, onSuccess])

    return {
        formData,
        errors,
        isLoading,
        isDirty,
        updateField,
        updateArea,
        addArea,
        removeArea,
        resetForm,
        submitForm,
        validateForm,
    }
}
