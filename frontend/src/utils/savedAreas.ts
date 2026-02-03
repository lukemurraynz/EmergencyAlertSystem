import type { Area, GeoPolygon } from '../types'

export interface SavedAreaTemplate {
  id: string
  name: string
  polygon: GeoPolygon
  regionCode?: string
  createdAt: string
}

const normalizeName = (name: string, fallback: string): string => {
  const trimmed = name.trim()
  return trimmed.length > 0 ? trimmed : fallback
}

const generateId = (): string => {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID()
  }
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

export const createSavedAreaTemplate = (
  area: Area,
  options?: {
    id?: string
    now?: Date
    fallbackName?: string
  }
): SavedAreaTemplate => {
  const fallbackName = options?.fallbackName ?? 'Custom area'
  const name = normalizeName(area.areaDescription, fallbackName)
  const template: SavedAreaTemplate = {
    id: options?.id ?? generateId(),
    name,
    polygon: area.polygon,
    createdAt: (options?.now ?? new Date()).toISOString(),
  }
  if (area.regionCode && area.regionCode.trim().length > 0) {
    template.regionCode = area.regionCode
  }
  return template
}

export const upsertSavedAreaTemplate = (
  templates: SavedAreaTemplate[],
  next: SavedAreaTemplate
): SavedAreaTemplate[] => {
  const nameKey = next.name.trim().toLowerCase()
  const existingIndex = templates.findIndex(
    template => template.name.trim().toLowerCase() === nameKey
  )

  if (existingIndex >= 0) {
    const existing = templates[existingIndex]
    if (!existing) {
      return [next, ...templates]
    }
    const updated: SavedAreaTemplate = {
      ...next,
      id: existing.id,
      createdAt: existing.createdAt,
    }
    return [updated, ...templates.filter((_, index) => index !== existingIndex)]
  }

  return [next, ...templates]
}

export const removeSavedAreaTemplate = (
  templates: SavedAreaTemplate[],
  id: string
): SavedAreaTemplate[] => templates.filter(template => template.id !== id)
