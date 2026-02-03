import { describe, it, expect } from 'vitest'
import { renderHook, act } from '@testing-library/react'
import { useAlertForm } from '../../src/hooks/useAlertForm'

describe('useAlertForm', () => {
    describe('form initialization', () => {
        it('should initialize with default values', () => {
            const { result } = renderHook(() => useAlertForm())

            expect(result.current.formData.headline).toBe('')
            expect(result.current.formData.description).toBe('')
            expect(result.current.formData.severity).toBe('Moderate')
            expect(result.current.formData.channelType).toBe('test')
            expect(result.current.formData.areas).toEqual([])
            expect(result.current.errors).toEqual({})
        })

        it('should have isDirty as false initially', () => {
            const { result } = renderHook(() => useAlertForm())
            expect(result.current.isDirty).toBe(false)
        })
    })

    describe('form updates', () => {
        it('should update headline field', () => {
            const { result } = renderHook(() => useAlertForm())

            act(() => {
                result.current.updateField('headline', 'Test Alert')
            })

            expect(result.current.formData.headline).toBe('Test Alert')
            expect(result.current.isDirty).toBe(true)
        })

        it('should update description field', () => {
            const { result } = renderHook(() => useAlertForm())

            act(() => {
                result.current.updateField('description', 'Test Description')
            })

            expect(result.current.formData.description).toBe('Test Description')
        })

        it('should update severity field', () => {
            const { result } = renderHook(() => useAlertForm())

            act(() => {
                result.current.updateField('severity', 'Extreme')
            })

            expect(result.current.formData.severity).toBe('Extreme')
        })
    })

    describe('form validation', () => {
        it('should validate headline is required', () => {
            const { result } = renderHook(() => useAlertForm())

            act(() => {
                result.current.validateForm()
            })

            expect(result.current.errors.headline).toBeTruthy()
        })

        it('should validate description is required', () => {
            const { result } = renderHook(() => useAlertForm())

            act(() => {
                result.current.updateField('headline', 'Test')
                result.current.validateForm()
            })

            expect(result.current.errors.description).toBeTruthy()
        })

        it('should validate expiry time is in future', () => {
            const { result } = renderHook(() => useAlertForm())

            const pastDate = new Date(Date.now() - 3600000).toISOString()

            act(() => {
                result.current.updateField('headline', 'Test')
                result.current.updateField('description', 'Test')
                result.current.updateField('expiresAt', pastDate)
            })

            act(() => {
                result.current.validateForm()
            })

            expect(result.current.errors.expiresAt).toBeTruthy()
        })

        it('should validate at least one area is required', () => {
            const { result } = renderHook(() => useAlertForm())

            act(() => {
                result.current.updateField('headline', 'Test')
                result.current.updateField('description', 'Test')
                result.current.validateForm()
            })

            expect(result.current.errors.areas).toBeTruthy()
        })
    })

    describe('area management', () => {
        it('should add area', () => {
            const { result } = renderHook(() => useAlertForm())

            const testArea = {
                areaDescription: 'Test Area',
                polygon: {
                    type: 'Polygon' as const,
                    coordinates: [[[-1, 52], [0, 52], [0, 53], [-1, 53], [-1, 52]]],
                },
            }

            act(() => {
                result.current.addArea(testArea)
            })

            expect(result.current.formData.areas).toHaveLength(1)
            expect(result.current.formData.areas[0]).toEqual(testArea)
        })

        it('should remove area', () => {
            const { result } = renderHook(() => useAlertForm())

            const testArea = {
                areaDescription: 'Test Area',
                polygon: {
                    type: 'Polygon' as const,
                    coordinates: [[[-1, 52], [0, 52], [0, 53], [-1, 53], [-1, 52]]],
                },
            }

            act(() => {
                result.current.addArea(testArea)
                result.current.removeArea(0)
            })

            expect(result.current.formData.areas).toHaveLength(0)
        })

        it('should update area', () => {
            const { result } = renderHook(() => useAlertForm())

            const testArea = {
                areaDescription: 'Test Area',
                polygon: {
                    type: 'Polygon' as const,
                    coordinates: [[[-1, 52], [0, 52], [0, 53], [-1, 53], [-1, 52]]],
                },
            }

            act(() => {
                result.current.addArea(testArea)
            })

            const updatedArea = { ...testArea, areaDescription: 'Updated Area' }

            act(() => {
                result.current.updateArea(0, updatedArea)
            })

            expect(result.current.formData.areas[0].areaDescription).toBe('Updated Area')
        })
    })

    describe('form reset', () => {
        it('should reset form to initial state', () => {
            const { result } = renderHook(() => useAlertForm())

            act(() => {
                result.current.updateField('headline', 'Test')
                result.current.updateField('description', 'Test')
                result.current.resetForm()
            })

            expect(result.current.formData.headline).toBe('')
            expect(result.current.formData.description).toBe('')
            expect(result.current.errors).toEqual({})
        })
    })
})
