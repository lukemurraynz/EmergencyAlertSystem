import { useState, useCallback } from 'react'
import type { GeoPolygon } from '../types'

export interface Coordinate {
    lat: number
    lng: number
}

export interface UsePolygonMapReturn {
    polygon: GeoPolygon | null
    coordinates: Coordinate[]
    isDrawing: boolean
    error: string | null
    setPolygon: (polygon: GeoPolygon | null) => void
    addCoordinate: (coord: Coordinate) => void
    removeCoordinate: (index: number) => void
    clearPolygon: () => void
    validatePolygon: () => boolean
    completePolygon: () => GeoPolygon | null
}

export function usePolygonMap(): UsePolygonMapReturn {
    const [polygon, setPolygon] = useState<GeoPolygon | null>(null)
    const [coordinates, setCoordinates] = useState<Coordinate[]>([])
    const [error, setError] = useState<string | null>(null)
    const [isDrawing, setIsDrawing] = useState(false)

    const validatePolygon = useCallback((coords: Coordinate[]): boolean => {
        if (coords.length < 3) {
            setError('Polygon must have at least 3 points')
            return false
        }

        // Check for duplicate consecutive coordinates
        for (let i = 0; i < coords.length - 1; i++) {
            const current = coords[i]
            const next = coords[i + 1]
            if (current && next && current.lat === next.lat && current.lng === next.lng) {
                setError('Duplicate consecutive coordinates are not allowed')
                return false
            }
        }

        // Basic self-intersection check (simplified)
        // In production, use a library like turf.js for comprehensive validation
        for (let i = 0; i < coords.length - 1; i++) {
            for (let j = i + 2; j < coords.length; j++) {
                if (j === coords.length - 1 && i === 0) continue // Skip closing line
                const c1 = coords[i]
                const c2 = coords[i + 1]
                const c3 = coords[j]
                const c4 = coords[(j + 1) % coords.length]
                if (!c1 || !c2 || !c3 || !c4) continue
                // Simplified intersection check
                const intersects = lineSegmentsIntersect(
                    { x: c1.lng, y: c1.lat },
                    { x: c2.lng, y: c2.lat },
                    { x: c3.lng, y: c3.lat },
                    { x: c4.lng, y: c4.lat }
                )
                if (intersects) {
                    setError('Polygon has self-intersections')
                    return false
                }
            }
        }

        setError(null)
        return true
    }, [])

    const addCoordinate = useCallback((coord: Coordinate) => {
        const newCoords = [...coordinates, coord]
        setCoordinates(newCoords)
        setIsDrawing(true)
    }, [coordinates])

    const removeCoordinate = useCallback((index: number) => {
        const newCoords = coordinates.filter((_, i) => i !== index)
        setCoordinates(newCoords)
        if (newCoords.length === 0) {
            setIsDrawing(false)
        }
    }, [coordinates])

    const clearPolygon = useCallback(() => {
        setPolygon(null)
        setCoordinates([])
        setError(null)
        setIsDrawing(false)
    }, [])

    const completePolygon = useCallback((): GeoPolygon | null => {
        if (!validatePolygon(coordinates)) {
            return null
        }

        // Create closed ring (first point = last point)
        const ring: [number, number][] = coordinates.map(c => [c.lng, c.lat])
        const firstCoord = coordinates[0]
        if (firstCoord) {
            ring.push([firstCoord.lng, firstCoord.lat])
        }

        const geoPolygon: GeoPolygon = {
            type: 'Polygon',
            coordinates: [ring],
        }

        setPolygon(geoPolygon)
        setIsDrawing(false)
        return geoPolygon
    }, [coordinates, validatePolygon])

    return {
        polygon,
        coordinates,
        isDrawing,
        error,
        setPolygon,
        addCoordinate,
        removeCoordinate,
        clearPolygon,
        validatePolygon: () => validatePolygon(coordinates),
        completePolygon,
    }
}

// Helper function for line segment intersection detection
function lineSegmentsIntersect(
    p1: { x: number; y: number },
    p2: { x: number; y: number },
    p3: { x: number; y: number },
    p4: { x: number; y: number }
): boolean {
    const ccw = (A: typeof p1, B: typeof p2, C: typeof p3) => {
        return (C.y - A.y) * (B.x - A.x) > (B.y - A.y) * (C.x - A.x)
    }

    return ccw(p1, p3, p4) !== ccw(p2, p3, p4) && ccw(p1, p2, p3) !== ccw(p1, p2, p4)
}
