import { describe, it, expect } from 'vitest'
import { buildDecisionFromUpdate } from '../../src/utils/drasiDecisioning'

describe('buildDecisionFromUpdate', () => {
    it('builds an SLA breach decision', () => {
        const decision = buildDecisionFromUpdate(
            {
                eventType: 'SLABreachDetected',
                payload: {
                    headline: 'Coastal Flood Warning',
                    elapsedSeconds: 92,
                    detectionTimestamp: '2026-02-01T10:00:00Z',
                    idempotencyKey: 'sla-1',
                },
            },
            { idFactory: () => 'test-id' }
        )

        expect(decision).not.toBeNull()
        expect(decision?.title).toContain('SLA')
        expect(decision?.detail).toContain('Coastal Flood Warning')
        expect(decision?.severity).toBe('high')
        expect(decision?.timestamp).toBe('2026-02-01T10:00:00Z')
    })

    it('builds an approval timeout decision', () => {
        const decision = buildDecisionFromUpdate(
            {
                eventType: 'ApprovalTimeoutDetected',
                payload: {
                    headline: 'Wildfire Advisory',
                    elapsedMinutes: 14,
                    detectionTimestamp: '2026-02-01T09:50:00Z',
                },
            },
            { idFactory: () => 'test-id' }
        )

        expect(decision?.title).toContain('Approval timeout')
        expect(decision?.detail).toContain('Wildfire Advisory')
        expect(decision?.severity).toBe('medium')
    })

    it('builds a correlation decision with EAS recommendation', () => {
        const decision = buildDecisionFromUpdate(
            {
                eventType: 'CorrelationEventDetected',
                payload: {
                    patternType: 'RegionalHotspot',
                    alertIds: ['a1', 'a2', 'a3', 'a4'],
                },
            },
            { idFactory: () => 'test-id' }
        )

        expect(decision?.title).toContain('Regional Hotspot')
        expect(decision?.severity).toBe('high')
        expect(decision?.action).toContain('EAS')
    })

    it('builds a duplicate suppression decision', () => {
        const decision = buildDecisionFromUpdate(
            {
                eventType: 'CorrelationEventDetected',
                payload: {
                    patternType: 'DuplicateSuppression',
                    headline: 'Storm Warning',
                    regionCode: 'NSW-North',
                    alertIds: ['a1', 'a2'],
                },
            },
            { idFactory: () => 'test-id' }
        )

        expect(decision?.title).toContain('duplicate')
        expect(decision?.detail).toContain('Storm Warning')
        expect(decision?.severity).toBe('medium')
    })

    it('builds an area expansion decision', () => {
        const decision = buildDecisionFromUpdate(
            {
                eventType: 'CorrelationEventDetected',
                payload: {
                    patternType: 'AreaExpansionSuggestion',
                    headline: 'Flood Watch',
                    regionCodes: ['QLD-North', 'QLD-Central'],
                    alertIds: ['a1', 'a2'],
                },
            },
            { idFactory: () => 'test-id' }
        )

        expect(decision?.title).toContain('Expand')
        expect(decision?.detail).toContain('Flood Watch')
        expect(decision?.severity).toBe('medium')
    })

    it('builds a rate spike decision', () => {
        const decision = buildDecisionFromUpdate(
            {
                eventType: 'DashboardSummaryUpdated',
                payload: {
                    eventType: 'RateSpikeDetected',
                    alertsInOneHourWindow: 54,
                    creationRatePerHour: 126.5,
                    severity: 'Critical',
                },
            },
            { idFactory: () => 'test-id' }
        )

        expect(decision?.title).toContain('rate spike')
        expect(decision?.severity).toBe('critical')
    })

    it('builds an all-clear suggestion decision', () => {
        const decision = buildDecisionFromUpdate(
            {
                eventType: 'AlertStatusChanged',
                payload: {
                    status: 'AllClearSuggested',
                    headline: 'Heat Advisory',
                },
            },
            { idFactory: () => 'test-id' }
        )

        expect(decision?.title).toContain('All-clear')
        expect(decision?.severity).toBe('low')
    })

    it('returns null for unrelated events', () => {
        const decision = buildDecisionFromUpdate(
            {
                eventType: 'UnknownEvent',
                payload: {},
            },
            { idFactory: () => 'test-id' }
        )

        expect(decision).toBeNull()
    })
})
