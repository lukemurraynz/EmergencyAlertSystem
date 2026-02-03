import { useCallback, useEffect, useRef, useState } from 'react'
import * as atlas from 'azure-maps-control'
import { makeStyles, tokens, Button, Text, Badge, Input } from '@fluentui/react-components'
import type { GeoPolygon } from '../../types'
import { mapsConfigService, type MapsConfig } from '../../services/mapsConfigService'
import { normalizeRing } from '../../utils/geo'
import { DEFAULT_MAP_CENTER, MAP_STYLES, DEFAULT_MAP_HEIGHT } from '../../utils/mapConstants'

const useMapEditorStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    canvas: {
        border: `2px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground2,
        cursor: 'crosshair',
        minHeight: '400px',
        position: 'relative',
        overflow: 'hidden',
    },
    mapSurface: {
        height: DEFAULT_MAP_HEIGHT,
    },
    instructions: {
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorStatusWarningBackground3,
        borderRadius: tokens.borderRadiusMedium,
        borderLeft: `4px solid ${tokens.colorBrandForeground1}`,
    },
    coordinatesList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        maxHeight: '200px',
        overflowY: 'auto',
    },
    searchRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
    },
    searchInput: {
        flexGrow: 1,
        minWidth: '240px',
    },
    searchResults: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        padding: `${tokens.spacingVerticalXS} 0`,
    },
    searchResult: {
        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground2,
        cursor: 'pointer',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3,
        },
    },
    coordinateItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        fontSize: tokens.fontSizeBase100,
    },
    controls: {
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
})

interface MapEditorProps {
    onPolygonChange?: (polygon: GeoPolygon | null) => void
    initialPolygon?: GeoPolygon | null
}

type SearchResult = { id: string; label: string; position: atlas.data.Position }

type AzureMapsSearchAddressResult = {
    id?: string
    position?: {
        lat: number
        lon: number
    }
    address?: {
        freeformAddress?: string
        streetName?: string
        municipality?: string
    }
}

type AzureMapsSearchAddressResponse = {
    results?: AzureMapsSearchAddressResult[]
}

export const MapEditor = ({ onPolygonChange, initialPolygon }: MapEditorProps) => {
    const styles = useMapEditorStyles()
    const mapRef = useRef<HTMLDivElement | null>(null)
    const mapInstance = useRef<atlas.Map | null>(null)
    const dataSourceRef = useRef<atlas.source.DataSource | null>(null)
    const mapsConfigRef = useRef<MapsConfig | null>(null)
    const searchMarkerRef = useRef<atlas.HtmlMarker | null>(null)
    const coordsRef = useRef<atlas.data.Position[]>([])
    const [hasPolygon, setHasPolygon] = useState<boolean>(!!initialPolygon)
    const [error, setError] = useState<string | null>(null)
    const [coords, setCoords] = useState<atlas.data.Position[]>([])
    const [mapReady, setMapReady] = useState(false)
    const [searchQuery, setSearchQuery] = useState('')
    const [searchResults, setSearchResults] = useState<SearchResult[]>([])
    const [searching, setSearching] = useState(false)
    const [searchError, setSearchError] = useState<string | null>(null)

    const centerOnBrowserLocation = useCallback((map: atlas.Map) => {
        if (!navigator.geolocation) return
        navigator.geolocation.getCurrentPosition(
            position => {
                const center: atlas.data.Position = [
                    position.coords.longitude,
                    position.coords.latitude,
                ] as atlas.data.Position
                map.setCamera({ center, zoom: 12 })
            },
            () => {
                // Ignore location errors and keep default center.
            },
            { enableHighAccuracy: true, maximumAge: 60000, timeout: 8000 }
        )
    }, [])

    const handleSearch = useCallback(async () => {
        const query = searchQuery.trim()
        if (!query) return

        const config = mapsConfigRef.current
        if (!config?.aadClientId) {
            setSearchError('Map search is not ready yet.')
            return
        }

        setSearching(true)
        setSearchError(null)
        try {
            const tokenResponse = await mapsConfigService.getSasToken()
            const response = await fetch(
                `https://atlas.microsoft.com/search/address/json?api-version=1.0&query=${encodeURIComponent(query)}&limit=5`,
                {
                    headers: {
                        Authorization: `Bearer ${tokenResponse.token}`,
                        'x-ms-client-id': config.aadClientId,
                    },
                }
            )
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`)
            }
            const data = (await response.json()) as AzureMapsSearchAddressResponse
            const results = (data.results ?? [])
                .map(result => {
                    if (!result.position) return null
                    const position: atlas.data.Position = [
                        result.position.lon,
                        result.position.lat,
                    ] as atlas.data.Position
                    const label =
                        result.address?.freeformAddress ??
                        result.address?.streetName ??
                        result.address?.municipality ??
                        query
                    return {
                        id: result.id ?? `${result.position.lat}-${result.position.lon}`,
                        label,
                        position,
                    }
                })
                .filter((result): result is SearchResult => !!result)
            setSearchResults(results)
            if (!results.length) {
                setSearchError('No results found.')
            }
        } catch (err) {
            setSearchError(err instanceof Error ? err.message : 'Search failed.')
        } finally {
            setSearching(false)
        }
    }, [searchQuery])

    const selectSearchResult = useCallback((result: { position: atlas.data.Position }) => {
        const map = mapInstance.current
        if (!map) return

        if (searchMarkerRef.current) {
            map.markers.remove(searchMarkerRef.current)
        }

        const marker = new atlas.HtmlMarker({ position: result.position })
        map.markers.add(marker)
        searchMarkerRef.current = marker
        map.setCamera({ center: result.position, zoom: 13 })
    }, [])

    useEffect(() => {
        let isMounted = true
        const initializeMap = async () => {
            try {
                const config = await mapsConfigService.getConfig()
                mapsConfigRef.current = config
                
                if (!mapRef.current) return

                let authOptions: atlas.AuthenticationOptions

                if (config.authMode === 'sas' || config.authMode === 'anonymous') {
                    if (!config.aadClientId) {
                        if (isMounted) {
                            setError('Azure Maps client ID missing. Check backend /api/v1/config/maps')
                        }
                        return
                    }

                    authOptions = {
                        authType: 'anonymous',
                        clientId: config.aadClientId,
                        getToken: (resolve: (token: string) => void, reject: (reason?: unknown) => void) => {
                            mapsConfigService
                                .getSasToken()
                                .then(tokenResponse => resolve(tokenResponse.token))
                                .catch(reject)
                        },
                    } as atlas.AuthenticationOptions
                } else {
                    // Use Azure AD for authenticated access
                    if (!config.aadClientId || !config.aadAppId || !config.aadTenantId) {
                        if (isMounted) {
                            setError('Azure Maps configuration incomplete. Check backend /api/v1/config/maps')
                        }
                        return
                    }

                    authOptions = {
                        authType: 'aad',
                        clientId: config.aadClientId,
                        aadAppId: config.aadAppId,
                        aadTenant: config.aadTenantId,
                    } as atlas.AuthenticationOptions
                }

                const map = new atlas.Map(mapRef.current, {
                    center: DEFAULT_MAP_CENTER,
                    zoom: 10,
                    view: 'Auto',
                    authOptions,
                })
                mapInstance.current = map

                map.events.add('ready', () => {
                    const ds = new atlas.source.DataSource()
                    dataSourceRef.current = ds
                    map.sources.add(ds)
                    map.layers.add(new atlas.layer.PolygonLayer(ds, undefined, {
                        fillColor: '#0f6cbd',
                        fillOpacity: 0.35,
                    }))
                    map.layers.add(new atlas.layer.LineLayer(ds, undefined, {
                        strokeColor: '#0f6cbd',
                        strokeWidth: 2,
                    }))
                    map.controls.add(
                        new atlas.control.StyleControl({
                            mapStyles: MAP_STYLES,
                            layout: 'list',
                        }),
                        { position: atlas.ControlPosition.TopRight }
                    )
                    if (isMounted) {
                        setMapReady(true)
                    }

                    map.events.add('click', (e: atlas.MapMouseEvent) => {
                        if (!map) return
                        const pos = e.position as atlas.data.Position
                        setCoords(prev => {
                            const next = [...prev, pos]
                            coordsRef.current = next
                            ds.clear()
                            if (next.length > 1) {
                                ds.add(new atlas.data.LineString(next))
                            }
                            return next
                        })
                    })
                    centerOnBrowserLocation(map)
                })
            } catch (err) {
                if (isMounted) {
                    setError(`Failed to initialize map: ${err instanceof Error ? err.message : 'Unknown error'}`)
                }
            }
        }

        initializeMap()

        return () => {
            isMounted = false
            mapInstance.current?.dispose()
        }
    }, [centerOnBrowserLocation])

    useEffect(() => {
        if (!mapReady) return
        const ds = dataSourceRef.current
        if (!ds || !initialPolygon || !Array.isArray(initialPolygon.coordinates) || !initialPolygon.coordinates[0]) {
            return
        }

        try {
            const ringCoords = initialPolygon.coordinates[0]
            const positions: atlas.data.Position[] = ringCoords.map(([lng, lat]) => [lng, lat] as atlas.data.Position)
            if (positions.length) {
                ds.clear()
                ds.add(new atlas.data.Polygon([positions]))
                const bbox = atlas.data.BoundingBox.fromPositions(positions)
                if (bbox) {
                    mapInstance.current?.setCamera({ bounds: bbox, padding: 50 })
                }
            }
        } catch {
            /* ignore */
        }
    }, [initialPolygon, mapReady])

    const handleComplete = () => {
        const ds = dataSourceRef.current
        if (!ds) return
        const currentCoords = coordsRef.current
        if (currentCoords.length < 3) {
            setError('Polygon must have at least 3 points')
            return
        }
        const ring = currentCoords.map(([lng, lat]) => [lng, lat] as [number, number])
        const normalized = normalizeRing(ring)
        if (!normalized) {
            setError('Polygon contains invalid coordinates')
            return
        }
        const closedRing = normalized.map(([lng, lat]) => [lng, lat] as atlas.data.Position)
        ds.clear()
        ds.add(new atlas.data.Polygon([closedRing]))
        const polygon: GeoPolygon = {
            type: 'Polygon',
            coordinates: [normalized.map(([lng, lat]) => [lng, lat] as [number, number])],
        }
        onPolygonChange?.(polygon)
        setHasPolygon(true)
        setError(null)
    }

    const handleClear = () => {
        const ds = dataSourceRef.current
        ds?.clear()
        setCoords([])
        coordsRef.current = []
        setHasPolygon(false)
        onPolygonChange?.(null)
    }

    return (
        <div className={styles.root}>
            <div className={styles.instructions}>
                <Text weight="semibold">Instructions</Text>
                <Text size={200}>
                    Click on the map to add points. Complete to form a polygon or Reset to start over.
                </Text>
            </div>

            <div className={styles.searchRow}>
                <Input
                    className={styles.searchInput}
                    placeholder="Search for a location"
                    value={searchQuery}
                    onChange={(_, data) => setSearchQuery(data.value)}
                />
                <Button appearance="primary" onClick={handleSearch} disabled={searching}>
                    {searching ? 'Searching...' : 'Search'}
                </Button>
            </div>
            {searchError && <div className={styles.error}>{searchError}</div>}
            {!!searchResults.length && (
                <div className={styles.searchResults}>
                    {searchResults.map(result => (
                        <div
                            key={result.id}
                            className={styles.searchResult}
                            onClick={() => selectSearchResult(result)}
                            role="button"
                            tabIndex={0}
                            onKeyDown={event => {
                                if (event.key === 'Enter' || event.key === ' ') {
                                    selectSearchResult(result)
                                }
                            }}
                        >
                            <Text>{result.label}</Text>
                        </div>
                    ))}
                </div>
            )}

            <div className={styles.canvas}>
                <div ref={mapRef} className={styles.mapSurface} />
            </div>

            {error && <div className={styles.error}>{error}</div>}

            <div className={styles.controls}>
                <Button appearance="primary" disabled={coords.length < 3} onClick={handleComplete}>
                    Complete Polygon
                </Button>
                <Button appearance="secondary" onClick={handleClear}>
                    Reset
                </Button>
            </div>

            {hasPolygon && (
                <div>
                    <Badge appearance="filled" color="success">
                        Polygon Defined
                    </Badge>
                </div>
            )}
        </div>
    )
}

export default MapEditor
