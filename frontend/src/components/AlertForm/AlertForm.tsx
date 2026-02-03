import {
    Field,
    Input,
    Select,
    Button,
    Spinner,
    makeStyles,
    tokens,
    Badge,
    Card,
    CardHeader,
    Text,
} from '@fluentui/react-components'
import { useAlertForm } from '../../hooks/useAlertForm'
import type { Alert, Area, GeoPolygon, AlertSeverity, AlertChannelType } from '../../types'
import CharacterCounter from './CharacterCounter'
import MapEditor from './MapEditor'
import { useState } from 'react'
import {
    createSavedAreaTemplate,
    removeSavedAreaTemplate,
    upsertSavedAreaTemplate,
    type SavedAreaTemplate,
} from '../../utils/savedAreas'

const savedAreasStorageKey = 'emergency_alerts_saved_areas'

const toLocalDateTimeInput = (isoValue: string) => {
    const date = new Date(isoValue)
    if (Number.isNaN(date.getTime())) {
        return ''
    }
    const pad = (value: number) => value.toString().padStart(2, '0')
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

const toIsoFromLocalDateTimeInput = (value: string) => {
    if (!value) {
        return ''
    }
    const [datePart, timePart] = value.split('T')
    if (!datePart || !timePart) {
        return ''
    }
    const [yearText, monthText, dayText] = datePart.split('-')
    const [hourText, minuteText] = timePart.split(':')
    if (!yearText || !monthText || !dayText || !hourText || !minuteText) {
        return ''
    }
    const year = Number(yearText)
    const month = Number(monthText)
    const day = Number(dayText)
    const hour = Number(hourText)
    const minute = Number(minuteText)
    if ([year, month, day, hour, minute].some(value => Number.isNaN(value))) {
        return ''
    }
    const date = new Date(year, month - 1, day, hour, minute, 0, 0)
    return Number.isNaN(date.getTime()) ? '' : date.toISOString()
}

const useAlertFormStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXL,
        maxWidth: '800px',
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    fieldRow: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
        gap: tokens.spacingHorizontalL,
    },
    fieldFullWidth: {
        gridColumn: '1 / -1',
    },
    areaCard: {
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        marginBottom: tokens.spacingVerticalL,
    },
    distributionCard: {
        padding: tokens.spacingHorizontalL,
        borderRadius: tokens.borderRadiusLarge,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground1,
        boxShadow: '0 16px 30px rgba(20, 27, 76, 0.08)',
    },
    distributionGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: tokens.spacingHorizontalL,
        marginTop: tokens.spacingVerticalM,
    },
    distributionItem: {
        padding: tokens.spacingHorizontalM,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    areaHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalS,
    },
    actions: {
        display: 'flex',
        gap: tokens.spacingHorizontalL,
    },
    error: {
        color: tokens.colorStatusDangerForeground1,
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorStatusDangerBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
    success: {
        color: tokens.colorPaletteGreenForeground1,
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorStatusSuccessBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
})

interface AlertFormProps {
    onSuccess?: (alert: Alert) => void
}

export const AlertForm = ({ onSuccess }: AlertFormProps) => {
    const styles = useAlertFormStyles()
    const form = useAlertForm(onSuccess)
    const [selectedAreaIndex, setSelectedAreaIndex] = useState<number | null>(null)
    const [savedAreas, setSavedAreas] = useState<SavedAreaTemplate[]>(() => {
        if (typeof window === 'undefined') {
            return []
        }
        try {
            const raw = window.localStorage.getItem(savedAreasStorageKey)
            if (!raw) return []
            const parsed = JSON.parse(raw) as SavedAreaTemplate[]
            if (!Array.isArray(parsed)) return []
            return parsed.filter(item => !!item && typeof item.id === 'string' && typeof item.name === 'string')
        } catch {
            return []
        }
    })
    const [selectedSavedAreaId, setSelectedSavedAreaId] = useState('')
    const [savedAreaMessage, setSavedAreaMessage] = useState<string | null>(null)
    const [savedAreaError, setSavedAreaError] = useState<string | null>(null)

    const persistSavedAreas = (next: SavedAreaTemplate[]) => {
        setSavedAreas(next)
        if (typeof window === 'undefined') return
        try {
            window.localStorage.setItem(savedAreasStorageKey, JSON.stringify(next))
        } catch {
            // Ignore storage errors and keep in-memory state.
        }
    }

    const handleDescriptionChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
        form.updateField('description', e.target.value)
    }

    const handleHeadlineChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        form.updateField('headline', e.target.value)
    }

    const handleSeverityChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
        form.updateField('severity', e.target.value as AlertSeverity)
    }

    const handleChannelTypeChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
        form.updateField('channelType', e.target.value as AlertChannelType)
    }

    const handleAddArea = (description: string) => {
        const newArea: Area = {
            areaDescription: description,
            polygon: {
                type: 'Polygon',
                coordinates: [[
                    [-1.5, 52.0],
                    [-1.0, 52.0],
                    [-1.0, 52.5],
                    [-1.5, 52.5],
                    [-1.5, 52.0],
                ]],
            },
        }
        form.addArea(newArea)
        setSelectedAreaIndex(form.formData.areas.length)
    }

    const handleAreaNameChange = (index: number, value: string) => {
        const current = form.formData.areas[index]
        if (!current) return
        form.updateArea(index, { ...current, areaDescription: value })
    }

    const handleSaveAreaTemplate = (index: number) => {
        const area = form.formData.areas[index]
        if (!area?.polygon) {
            setSavedAreaError('Area polygon is incomplete. Define the area before saving.')
            setSavedAreaMessage(null)
            return
        }

        const template = createSavedAreaTemplate(area, {
            fallbackName: `Custom area ${index + 1}`,
        })
        const next = upsertSavedAreaTemplate(savedAreas, template)
        const saved = next[0] ?? template
        persistSavedAreas(next)
        setSelectedSavedAreaId(saved.id)
        setSavedAreaMessage(`Saved "${saved.name}" for reuse.`)
        setSavedAreaError(null)
    }

    const handleUseSavedArea = () => {
        const selected = savedAreas.find(area => area.id === selectedSavedAreaId)
        if (!selected) {
            setSavedAreaError('Select a saved area to add.')
            setSavedAreaMessage(null)
            return
        }

        const newArea: Area = {
            areaDescription: selected.name,
            polygon: selected.polygon,
            ...(selected.regionCode ? { regionCode: selected.regionCode } : {}),
        }
        form.addArea(newArea)
        setSelectedAreaIndex(form.formData.areas.length)
        setSavedAreaMessage(`Added "${selected.name}" to this alert.`)
        setSavedAreaError(null)
    }

    const handleRemoveSavedArea = () => {
        if (!selectedSavedAreaId) return
        const next = removeSavedAreaTemplate(savedAreas, selectedSavedAreaId)
        persistSavedAreas(next)
        setSelectedSavedAreaId('')
        setSavedAreaMessage('Saved area removed.')
        setSavedAreaError(null)
    }

    const handlePolygonChange = (index: number, polygon: GeoPolygon) => {
        if (polygon) {
            const currentArea = form.formData.areas[index]
            if (currentArea) {
                const updatedArea: Area = {
                    ...currentArea,
                    polygon,
                }
                form.updateArea(index, updatedArea)
            }
        }
    }

    const handleRemoveArea = (index: number) => {
        form.removeArea(index)
        setSelectedAreaIndex(null)
    }

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault()
        const result = await form.submitForm()
        if (result) {
            // Success handled by onSuccess callback
        }
    }

    return (
        <form onSubmit={handleSubmit} className={styles.root}>
            {form.errors.submit && (
                <div className={styles.error}>{form.errors.submit}</div>
            )}

            <div className={styles.section}>
                <h2>Alert Details</h2>
                <div className={styles.fieldRow}>
                    <Field
                        label="Headline"
                        required
                        {...(form.errors.headline && { validationMessage: form.errors.headline })}
                        className={styles.fieldFullWidth}
                    >
                        <Input
                            value={form.formData.headline}
                            onChange={handleHeadlineChange}
                            maxLength={100}
                            placeholder="Enter alert headline"
                        />
                    </Field>

                    <Field
                        label="Description"
                        required
                        {...(form.errors.description && { validationMessage: form.errors.description })}
                        className={styles.fieldFullWidth}
                    >
                        <textarea
                            value={form.formData.description}
                            onChange={handleDescriptionChange}
                            placeholder="Enter alert description"
                            style={{
                                padding: tokens.spacingHorizontalL,
                                borderRadius: tokens.borderRadiusMedium,
                                border: `1px solid ${tokens.colorNeutralStroke2}`,
                                fontFamily: 'inherit',
                                fontSize: 'inherit',
                                minHeight: '120px',
                                resize: 'vertical',
                            }}
                        />
                        <CharacterCounter text={form.formData.description} />
                    </Field>

                    <Field label="Severity" required>
                        <Select value={form.formData.severity} onChange={handleSeverityChange}>
                            <option value="Extreme">Extreme</option>
                            <option value="Severe">Severe</option>
                            <option value="Moderate">Moderate</option>
                            <option value="Minor">Minor</option>
                            <option value="Unknown">Unknown</option>
                        </Select>
                    </Field>

                    <Field label="Channel Type" required>
                        <Select value={form.formData.channelType} onChange={handleChannelTypeChange}>
                            <option value="test">Test</option>
                            <option value="operator">Operator</option>
                            <option value="severe">Severe</option>
                            <option value="government">Government</option>
                        </Select>
                    </Field>

                    <Field label="Expires At" required {...(form.errors.expiresAt && { validationMessage: form.errors.expiresAt })}>
                        <Input
                            type="datetime-local"
                            value={toLocalDateTimeInput(form.formData.expiresAt)}
                            onChange={(e) => {
                                const dt = toIsoFromLocalDateTimeInput(e.target.value)
                                if (dt) {
                                    form.updateField('expiresAt', dt)
                                }
                            }}
                        />
                    </Field>
                </div>
            </div>

            <div className={styles.section}>
                <h2>Automated distribution</h2>
                <Card className={styles.distributionCard}>
                    <CardHeader header={<Text weight="semibold">EAS + Email distribution plan</Text>} />
                    <Text size={200}>
                        EAS broadcast is represented for operational visibility. Email delivery is the only active channel in this build.
                    </Text>
                    <div className={styles.distributionGrid}>
                        <div className={styles.distributionItem}>
                            <Text weight="semibold">EAS broadcast</Text>
                            <Badge appearance="outline">Simulated</Badge>
                            <Text size={200}>Used for escalation and regional decisioning.</Text>
                        </div>
                        <div className={styles.distributionItem}>
                            <Text weight="semibold">Email alerts</Text>
                            <Badge appearance="outline" color="success">Active</Badge>
                            <Text size={200}>Delivered to configured test recipients.</Text>
                        </div>
                        <div className={styles.distributionItem}>
                            <Text weight="semibold">Push notifications</Text>
                            <Badge appearance="outline">Not configured</Badge>
                            <Text size={200}>Requires device registration and a push provider.</Text>
                        </div>
                    </div>
                </Card>
            </div>

            <div className={styles.section}>
                <h2>Geographic Areas</h2>
                <p>{form.formData.areas.length} area(s) defined</p>

                <Card className={styles.areaCard}>
                    <CardHeader header={<Text weight="semibold">Saved areas</Text>} />
                    {savedAreas.length === 0 ? (
                        <Text size={200}>Save a custom area to reuse it in future alerts.</Text>
                    ) : (
                        <div className={styles.fieldRow}>
                            <Field label="Saved area">
                                <Select
                                    value={selectedSavedAreaId}
                                    onChange={(event) => setSelectedSavedAreaId(event.target.value)}
                                >
                                    <option value="">Select an area</option>
                                    {savedAreas.map(area => (
                                        <option key={area.id} value={area.id}>
                                            {area.name}
                                        </option>
                                    ))}
                                </Select>
                            </Field>
                            <div className={styles.actions}>
                                <Button appearance="primary" size="small" onClick={handleUseSavedArea} disabled={!selectedSavedAreaId}>
                                    Add to alert
                                </Button>
                                <Button appearance="subtle" size="small" onClick={handleRemoveSavedArea} disabled={!selectedSavedAreaId}>
                                    Remove
                                </Button>
                            </div>
                        </div>
                    )}
                    {savedAreaMessage && <div className={styles.success}>{savedAreaMessage}</div>}
                    {savedAreaError && <div className={styles.error}>{savedAreaError}</div>}
                </Card>

                {form.formData.areas.length > 0 && (
                    <div>
                        {form.formData.areas.map((area, index) => (
                            <div key={index} className={styles.areaCard}>
                                <div className={styles.areaHeader}>
                                    <h3>{area.areaDescription || 'Custom area'}</h3>
                                    <Badge appearance="filled" color="subtle">
                                        {area.regionCode || 'Custom'}
                                    </Badge>
                                </div>
                                <Field label="Area name" className={styles.fieldFullWidth}>
                                    <Input
                                        value={area.areaDescription}
                                        onChange={(_, data) => handleAreaNameChange(index, data.value)}
                                        placeholder="Give this area a name"
                                    />
                                </Field>
                                <div className={styles.actions}>
                                    <Button
                                        appearance={selectedAreaIndex === index ? 'primary' : 'secondary'}
                                        size="small"
                                        onClick={() => setSelectedAreaIndex(index)}
                                    >
                                        {selectedAreaIndex === index ? 'Editing' : 'Edit'}
                                    </Button>
                                    <Button
                                        appearance="subtle"
                                        size="small"
                                        onClick={() => handleRemoveArea(index)}
                                    >
                                        Remove
                                    </Button>
                                    <Button
                                        appearance="subtle"
                                        size="small"
                                        onClick={() => handleSaveAreaTemplate(index)}
                                    >
                                        Save
                                    </Button>
                                </div>
                            </div>
                        ))}
                    </div>
                )}

                {selectedAreaIndex !== null && (
                    <div className={styles.section}>
                        <h3>
                            Edit {form.formData.areas[selectedAreaIndex]?.areaDescription || `Area ${selectedAreaIndex + 1}`}
                        </h3>
                        <MapEditor
                            onPolygonChange={(polygon) => {
                                if (polygon && selectedAreaIndex !== null) {
                                    handlePolygonChange(selectedAreaIndex, polygon)
                                }
                            }}
                            {...(form.formData.areas[selectedAreaIndex]?.polygon && { initialPolygon: form.formData.areas[selectedAreaIndex].polygon })}
                        />
                    </div>
                )}

                <Button
                    appearance="secondary"
                    onClick={() => handleAddArea('')}
                >
                    Add Area
                </Button>
            </div>

            {form.errors.areas && (
                <div className={styles.error}>{form.errors.areas}</div>
            )}

            <div className={styles.actions}>
                <Button appearance="primary" type="submit" disabled={form.isLoading}>
                    {form.isLoading ? <Spinner size="small" /> : 'Create Alert'}
                </Button>
                <Button appearance="secondary" onClick={() => form.resetForm()}>
                    Reset
                </Button>
            </div>
        </form>
    )
}

export default AlertForm
