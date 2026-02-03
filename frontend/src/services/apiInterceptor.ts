import { API_BASE_URL } from './apiBaseUrl'

// API base URL configuration
const API_VERSION = '2026-01-25'

// Generate correlation ID for tracing
function generateCorrelationId(): string {
    return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`
}

// Get auth token from session storage
function getAuthToken(): string | null {
    return sessionStorage.getItem('authToken')
}

// Store auth token
export function setAuthToken(token: string): void {
    sessionStorage.setItem('authToken', token)
}

// API interceptor with error handling, correlation ID, and auth
export async function apiCall<T>(
    endpoint: string,
    options: RequestInit = {}
): Promise<T> {
    const correlationId = generateCorrelationId()
    const token = getAuthToken()

    // Build headers object, merging with any provided headers
    const headersInit: Record<string, string> = {
        'Content-Type': 'application/json',
        'X-Correlation-ID': correlationId,
        'api-version': API_VERSION,
    }

    // Merge custom headers if provided
    if (options.headers) {
        const providedHeaders = new Headers(options.headers)
        providedHeaders.forEach((value, key) => {
            headersInit[key] = value
        })
    }

    if (token) {
        headersInit['Authorization'] = `Bearer ${token}`
    }

    const headers = new Headers(headersInit)

    const url = `${API_BASE_URL}${endpoint}`

    try {
        const response = await fetch(url, {
            ...options,
            headers,
        })

        if (!response.ok) {
            const errorData: unknown = await response.json().catch(() => ({}))
            const apiError = {
                type: 'API_ERROR',
                title: 'Request Failed',
                status: response.status,
                correlationId,
                ...(typeof errorData === 'object' && errorData !== null ? errorData : {}),
            }
            throw apiError
        }

        if (response.status === 204) {
            return undefined as T
        }

        const data: T = await response.json()
        return data
    } catch (error) {
        if (error instanceof TypeError) {
            throw {
                type: 'NETWORK_ERROR',
                title: 'Network Error',
                status: 0,
                detail: error.message,
                correlationId,
            }
        }
        throw error
    }
}

// Export common HTTP method helpers
export async function get<T>(endpoint: string, options?: RequestInit): Promise<T> {
    return apiCall<T>(endpoint, { ...options, method: 'GET' })
}

export async function post<T>(endpoint: string, body?: unknown, options?: RequestInit): Promise<T> {
    return apiCall<T>(endpoint, {
        ...options,
        method: 'POST',
        ...(body !== undefined && { body: JSON.stringify(body) }),
    })
}

export async function put<T>(endpoint: string, body?: unknown, options?: RequestInit): Promise<T> {
    return apiCall<T>(endpoint, {
        ...options,
        method: 'PUT',
        ...(body !== undefined && { body: JSON.stringify(body) }),
    })
}

export async function patch<T>(endpoint: string, body?: unknown, options?: RequestInit): Promise<T> {
    return apiCall<T>(endpoint, {
        ...options,
        method: 'PATCH',
        ...(body !== undefined && { body: JSON.stringify(body) }),
    })
}

export async function del<T>(endpoint: string, body?: unknown, options?: RequestInit): Promise<T> {
    return apiCall<T>(endpoint, {
        ...options,
        method: 'DELETE',
        ...(body !== undefined && { body: JSON.stringify(body) }),
    })
}
