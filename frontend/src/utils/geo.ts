const MAX_LAT = 85.05112878
const MIN_LAT = -85.05112878
const MIN_LON = -180
const MAX_LON = 180

export const normalizeRing = (
    ring: Array<[number, number]>
): Array<[number, number]> | null => {
    if (!Array.isArray(ring) || ring.length < 3) {
        return null
    }

    for (const [lng, lat] of ring) {
        if (!Number.isFinite(lng) || !Number.isFinite(lat)) {
            return null
        }
        if (lng < MIN_LON || lng > MAX_LON || lat < MIN_LAT || lat > MAX_LAT) {
            return null
        }
    }

    const normalized = [...ring]
    const first = normalized[0]
    const last = normalized[normalized.length - 1]
    if (!first || !last) {
        return null
    }
    const [firstLng, firstLat] = first
    const [lastLng, lastLat] = last
    if (firstLng !== lastLng || firstLat !== lastLat) {
        normalized.push([firstLng, firstLat])
    }

    return normalized
}
