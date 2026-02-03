import { get, post } from './apiInterceptor'

export interface MapsConfig {
    authMode: string
    aadClientId?: string
    aadAppId?: string
    aadTenantId?: string
    accountName?: string
    enableMapEditor: boolean
}

export interface SasTokenResponse {
    token: string
    expiresAt: string
}

export const mapsConfigService = {
    async getConfig(): Promise<MapsConfig> {
        return get<MapsConfig>('/api/v1/config/maps')
    },

    async getSasToken(): Promise<SasTokenResponse> {
        return post<SasTokenResponse>('/api/v1/config/maps/sas-token')
    },
}
