import * as signalR from '@microsoft/signalr'
import { API_BASE_URL } from './apiBaseUrl'

export interface DashboardUpdate {
    eventType: string
    payload: Record<string, unknown>
}

export interface DashboardHubService {
    connect(): Promise<void>
    disconnect(): Promise<void>
    subscribe(handler: (update: DashboardUpdate) => void): void
    unsubscribe(): void
    isConnected(): boolean
}

let connection: signalR.HubConnection | null = null
let updateHandler: ((update: DashboardUpdate) => void) | null = null

export const dashboardHubService: DashboardHubService = {
    async connect(): Promise<void> {
        if (connection?.state === signalR.HubConnectionState.Connected) {
            return
        }

        // Hub URL uses API_BASE_URL so SignalR goes to same endpoint as REST API
        // This ensures it works with LoadBalancer deployments (not just ingress)
        const hubUrl = `${API_BASE_URL}/api/hubs/alerts`

        // LongPolling transport for multi-replica deployments without sticky sessions
        // WebSockets require sticky sessions which our LoadBalancer doesn't support
        connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: () => sessionStorage.getItem('authToken') || '',
                transport: signalR.HttpTransportType.LongPolling,
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .withServerTimeout(60000)
            .build()

        const emitUpdate = (eventType: string, data: Record<string, unknown>) => {
            if (updateHandler) {
                updateHandler({ eventType, payload: data })
            }
        }

        // Register all dashboard event handlers
        const dashboardEvents = [
            'AlertStatusChanged',
            'SLABreachDetected',
            'ApprovalTimeoutDetected',
            'CorrelationEventDetected',
            'DashboardSummaryUpdated',
            'SLACountdownUpdate',
            'DeliveryRetryStormDetected',
            'ApproverWorkloadAlert',
            'DeliverySuccessRateDegraded',
            'AlertDelivered',
        ] as const

        dashboardEvents.forEach(eventName => {
            connection!.on(eventName, (data: Record<string, unknown>) => {
                emitUpdate(eventName, data)
            })
        })

        connection.onreconnecting((error) => {
            console.debug('SignalR reconnecting...', error?.message)
        })

        connection.onreconnected((connectionId) => {
            console.debug('SignalR reconnected:', connectionId)
        })

        connection.onclose((error) => {
            console.debug('SignalR connection closed', error?.message)
        })

        try {
            await connection.start()
            await connection.invoke('SubscribeToDashboard')
        } catch (error) {
            console.error('SignalR connection failed:', error)
            throw error
        }
    },

    async disconnect(): Promise<void> {
        if (connection) {
            try {
                await connection.stop()
                connection = null
            } catch (error) {
                console.error('Error disconnecting SignalR:', error)
            }
        }
    },

    subscribe(handler: (update: DashboardUpdate) => void): void {
        updateHandler = handler
    },

    unsubscribe(): void {
        updateHandler = null
    },

    isConnected(): boolean {
        return connection?.state === signalR.HubConnectionState.Connected
    },
}

export default dashboardHubService
