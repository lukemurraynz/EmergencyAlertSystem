import type * as atlas from 'azure-maps-control'

/** Default map center (London) */
export const DEFAULT_MAP_CENTER: atlas.data.Position = [-0.1278, 51.5074] as atlas.data.Position

/** Available Azure Maps style options */
export const MAP_STYLES: string[] = [
    'road',
    'road_shaded_relief',
    'grayscale_light',
    'grayscale_dark',
    'night',
    'satellite',
    'satellite_road_labels',
    'high_contrast_light',
    'high_contrast_dark',
]

/** Default map surface height in CSS */
export const DEFAULT_MAP_HEIGHT = '400px'
