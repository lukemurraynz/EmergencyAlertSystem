const normalizeBaseUrl = (value: string) => value.replace(/\/$/, '')

const deriveApiBaseUrl = (): string => {
    // Priority 1: Use explicit environment variable (set at build time for production dev/test ops)
    const envValue = (import.meta.env.VITE_API_URL ?? '').toString().trim()
    if (envValue) {
        return normalizeBaseUrl(envValue)
    }

    // Priority 2: Use relative path for same-origin API (Kubernetes ingress pattern)
    // This works when API and frontend are behind the same ingress/host
    // In Kubernetes, ingress routes /api/* to backend and /* to frontend
    return ''
}

const deriveSignalRBaseUrl = (): string => {
    // Priority 1: Use explicit environment variable
    const envValue = (import.meta.env.VITE_SIGNALR_URL ?? '').toString().trim()
    if (envValue) {
        return normalizeBaseUrl(envValue)
    }

    // Priority 2: Use relative path (same-origin WebSocket)
    return ''
}

export const API_BASE_URL = deriveApiBaseUrl()
export const SIGNALR_BASE_URL = deriveSignalRBaseUrl()
