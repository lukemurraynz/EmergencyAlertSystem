import { describe, it, expect } from 'vitest'
import { createSavedAreaTemplate, removeSavedAreaTemplate, upsertSavedAreaTemplate } from '../../src/utils/savedAreas'
import type { Area } from '../../src/types'

const polygon = {
  type: 'Polygon' as const,
  coordinates: [[[-1, 52], [0, 52], [0, 53], [-1, 53], [-1, 52]]],
}

describe('saved area templates', () => {
  it('creates a template with fallback name', () => {
    const area: Area = {
      areaDescription: '',
      polygon,
      regionCode: 'NZ',
    }
    const now = new Date('2026-02-02T00:00:00.000Z')

    const template = createSavedAreaTemplate(area, {
      id: 'template-1',
      now,
      fallbackName: 'Custom area',
    })

    expect(template.id).toBe('template-1')
    expect(template.name).toBe('Custom area')
    expect(template.regionCode).toBe('NZ')
    expect(template.createdAt).toBe(now.toISOString())
  })

  it('upserts by name case-insensitively', () => {
    const first = createSavedAreaTemplate(
      { areaDescription: 'North Zone', polygon },
      { id: 'a1', now: new Date('2026-02-01T00:00:00.000Z') }
    )
    const replacement = createSavedAreaTemplate(
      { areaDescription: 'north zone', polygon },
      { id: 'a2', now: new Date('2026-02-02T00:00:00.000Z') }
    )

    const result = upsertSavedAreaTemplate([first], replacement)

    expect(result).toHaveLength(1)
    expect(result[0].id).toBe(first.id)
    expect(result[0].name).toBe('north zone')
  })

  it('removes a template by id', () => {
    const first = createSavedAreaTemplate({ areaDescription: 'Area A', polygon }, { id: 'a1' })
    const second = createSavedAreaTemplate({ areaDescription: 'Area B', polygon }, { id: 'a2' })

    const result = removeSavedAreaTemplate([first, second], 'a1')

    expect(result).toHaveLength(1)
    expect(result[0].id).toBe('a2')
  })
})

