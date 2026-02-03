import { useCallback, useEffect, useRef, useState } from 'react'
import * as atlas from 'azure-maps-control'
import { makeStyles, tokens, Text, Button, Input } from '@fluentui/react-components'
import type { GeoPolygon } from '../types'
import { mapsConfigService, type MapsConfig } from '../services/mapsConfigService'
import { normalizeRing } from '../utils/geo'
import { DEFAULT_MAP_CENTER, MAP_STYLES, DEFAULT_MAP_HEIGHT } from '../utils/mapConstants'

const useMapViewerStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
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
    error: {
        color: tokens.colorStatusDangerForeground1,
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorStatusDangerBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
    mapFrame: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground2,
        overflow: 'hidden',
    },
    mapSurface: {
        height: DEFAULT_MAP_HEIGHT,
    },
})

interface MapViewerAzureProps {
    polygons: GeoPolygon[]
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

export const MapViewerAzure = ({ polygons }: MapViewerAzureProps) => {
    const styles = useMapViewerStyles()
    const mapRef = useRef<HTMLDivElement | null>(null)
    const mapInstance = useRef<atlas.Map | null>(null)
    const dataSourceRef = useRef<atlas.source.DataSource | null>(null)
    const mapsConfigRef = useRef<MapsConfig | null>(null)
    const searchMarkerRef = useRef<atlas.HtmlMarker | null>(null)
    const [mapReady, setMapReady] = useState(false)
    const [error, setError] = useState<string | null>(null)
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
                map.setCamera({ center, zoom: 10 })
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
        map.setCamera({ center: result.position, zoom: 12 })
    }, [])

    const renderPolygons = useCallback((items: GeoPolygon[]) => {
        const map = mapInstance.current
        const ds = dataSourceRef.current
        if (!map || !ds) return

        ds.clear()
        const allPositions: atlas.data.Position[] = []
        let invalidCount = 0
        items.forEach(p => {
            const ringCoords = p.coordinates[0]
            if (ringCoords && ringCoords.length) {
                const normalized = normalizeRing(ringCoords)
                if (!normalized) {
                    invalidCount += 1
                    return
                }
                const positions = normalized.map(([lng, lat]) => [lng, lat] as atlas.data.Position)
                ds.add(new atlas.data.Polygon([positions]))
                allPositions.push(...positions)
            }
        })
        if (allPositions.length) {
            const bbox = atlas.data.BoundingBox.fromPositions(allPositions)
            if (bbox) map.setCamera({ bounds: bbox, padding: 50 })
        }
        if (invalidCount > 0) {
            setError('Some polygons were skipped due to invalid coordinates.')
        }
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
                            setError('Azure Maps client ID missing')
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
                    if (!config.aadClientId || !config.aadAppId || !config.aadTenantId) {
                        if (isMounted) {
                            setError('Azure Maps configuration incomplete')
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
                    zoom: 5,
                    view: 'Auto',
                    authOptions,
                })
                mapInstance.current = map

                map.events.add('ready', () => {
                    const ds = new atlas.source.DataSource()
                    map.sources.add(ds)
                    dataSourceRef.current = ds
                    map.layers.add(new atlas.layer.PolygonLayer(ds, undefined, {
                        fillColor: '#fde7e9',
                        fillOpacity: 0.35,
                    }))
                    map.layers.add(new atlas.layer.LineLayer(ds, undefined, {
                        strokeColor: '#d13438',
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
                    centerOnBrowserLocation(map)
                })

                return () => {
                    map.dispose()
                }
            } catch (err) {
                if (isMounted) {
                    setError(`Failed to initialize map: ${err instanceof Error ? err.message : 'Unknown error'}`)
                }
            }
        }

        let cleanup: (() => void) | undefined
        initializeMap().then(c => { cleanup = c })

        return () => {
            isMounted = false
            cleanup?.()
        }
    }, [centerOnBrowserLocation])

    useEffect(() => {
        if (!mapReady) return
        renderPolygons(polygons)
    }, [mapReady, polygons, renderPolygons])

    return (
        <div className={styles.root}>
            {error && (
                <div className={styles.error}>
                    <Text>{error}</Text>
                </div>
            )}
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
            {searchError && (
                <div className={styles.error}>
                    <Text>{searchError}</Text>
                </div>
            )}
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
            <div className={styles.mapFrame}>
                <div className={styles.mapSurface} ref={mapRef} />
            </div>
        </div>
    )
}

export default MapViewerAzure
