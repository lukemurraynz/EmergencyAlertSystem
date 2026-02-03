import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import {
    Text,
    Button,
    Spinner,
    makeStyles,
    tokens,
    Badge,
    Card,
    CardHeader,
    CardFooter,
    Avatar,
    Input,
    Dropdown,
    Option,
} from '@fluentui/react-components'
import { Search24Regular } from '@fluentui/react-icons'
import Layout from '../components/Layout'
import alertApi from '../services/alertApi'
import type { OptionOnSelectData, SelectionEvents } from '@fluentui/react-combobox'
import type { Alert } from '../types'

const useAlertsPageStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXL,
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalL,
    },
    filters: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        flexWrap: 'wrap',
        alignItems: 'center',
        padding: tokens.spacingVerticalL,
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
    searchBox: {
        minWidth: '280px',
        flexGrow: 1,
    },
    cardGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
        gap: tokens.spacingHorizontalL,
    },
    alertCard: {
        padding: tokens.spacingHorizontalL,
        borderRadius: tokens.borderRadiusLarge,
        boxShadow: '0 16px 30px rgba(20, 27, 76, 0.08)',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        transition: 'transform 0.2s ease, box-shadow 0.2s ease',
        ':hover': {
            transform: 'translateY(-2px)',
            boxShadow: '0 22px 40px rgba(20, 27, 76, 0.12)',
        },
    },
    cardHeader: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    metaRow: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalS,
        marginTop: tokens.spacingVerticalS,
    },
    mutedText: {
        color: tokens.colorNeutralForeground3,
    },
    dateRow: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalM,
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase100,
    },
    footer: {
        display: 'flex',
        justifyContent: 'space-between',
        marginTop: tokens.spacingVerticalM,
        alignItems: 'center',
    },
    loading: {
        display: 'flex',
        justifyContent: 'center',
        padding: tokens.spacingHorizontalXXL,
    },
    error: {
        color: tokens.colorStatusDangerForeground1,
        padding: tokens.spacingHorizontalL,
        backgroundColor: tokens.colorStatusDangerBackground3,
        borderRadius: tokens.borderRadiusMedium,
    },
})

export const AlertsPage = () => {
    const styles = useAlertsPageStyles()
    const navigate = useNavigate()
    const [alerts, setAlerts] = useState<Alert[]>([])
    const [isLoading, setIsLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(50)
    const [totalCount, setTotalCount] = useState(0)
    const [searchQuery, setSearchQuery] = useState('')
    const [statusFilter, setStatusFilter] = useState<string>('all')

    useEffect(() => {
        const loadAlerts = async () => {
            setIsLoading(true)
            setError(null)
            try {
                const filters: Record<string, string> = {}
                if (statusFilter !== 'all') {
                    filters.status = statusFilter
                }
                if (searchQuery.trim()) {
                    filters.search = searchQuery.trim()
                }
                
                const result = await alertApi.listAlerts(page, pageSize, filters)
                setAlerts(result.items)
                setTotalCount(result.total)
            } catch (err) {
                const error = err as { detail?: string }
                setError(error?.detail || 'Failed to load alerts')
            } finally {
                setIsLoading(false)
            }
        }

        loadAlerts()
    }, [page, pageSize, statusFilter, searchQuery])

    const getStatusBadge = (status: string) => {
        const statusMap: Record<string, { color: 'subtle' | 'brand' | 'danger' | 'important' | 'informative' | 'severe' | 'success' | 'warning'; label: string }> = {
            draft: { color: 'subtle', label: 'Draft' },
            pending_approval: { color: 'warning', label: 'Pending' },
            approved: { color: 'success', label: 'Approved' },
            rejected: { color: 'danger', label: 'Rejected' },
            delivered: { color: 'success', label: 'Delivered' },
            cancelled: { color: 'subtle', label: 'Cancelled' },
            expired: { color: 'subtle', label: 'Expired' },
        }
        const config = statusMap[status] || { color: 'subtle' as const, label: status }
        return <Badge appearance="filled" color={config.color}>{config.label}</Badge>
    }

    const getDeliveryBadge = (status: string) => {
        const statusMap: Record<string, { color: 'subtle' | 'brand' | 'danger' | 'important' | 'informative' | 'severe' | 'success' | 'warning'; label: string }> = {
            pending: { color: 'informative', label: 'Delivery pending' },
            delivered: { color: 'success', label: 'Delivered' },
            failed: { color: 'danger', label: 'Failed' },
        }
        const config = statusMap[status] || { color: 'subtle' as const, label: status }
        return <Badge appearance="outline" color={config.color}>{config.label}</Badge>
    }

    const handleCreateAlert = () => {
        navigate('/alerts/create')
    }

    const handleSelectAlert = (alertId: string | undefined) => {
        if (alertId) {
            navigate(`/alerts/${alertId}`)
        }
    }

    const totalPages = Math.ceil(totalCount / pageSize)
    
    const handleSearch = (value: string) => {
        setSearchQuery(value)
        setPage(1) // Reset to first page
    }

    const handleStatusFilterChange = (_event: SelectionEvents, data: OptionOnSelectData) => {
        setStatusFilter(data.optionValue ?? 'all')
        setPage(1) // Reset to first page
    }

    const handlePageSizeChange = (_event: SelectionEvents, data: OptionOnSelectData) => {
        setPageSize(Number(data.optionValue ?? '50') || 50)
        setPage(1) // Reset to first page
    }

    return (
        <Layout>
            <div className={styles.root}>
                <div className={styles.header}>
                    <div>
                        <h2>Alerts</h2>
                        <Text size={200} as="p">Manage emergency alerts ({totalCount} total)</Text>
                    </div>
                    <Button appearance="primary" onClick={handleCreateAlert}>
                        Create New Alert
                    </Button>
                </div>

                <div className={styles.filters}>
                    <Input
                        className={styles.searchBox}
                        contentBefore={<Search24Regular />}
                        placeholder="Search by headline or description..."
                        value={searchQuery}
                        onChange={(_, data) => handleSearch(data.value)}
                    />
                    <Dropdown
                        placeholder="Filter by status"
                        value={statusFilter}
                        selectedOptions={[statusFilter]}
                        onOptionSelect={handleStatusFilterChange}
                    >
                        <Option value="all">All statuses</Option>
                        <Option value="draft">Draft</Option>
                        <Option value="pending_approval">Pending Approval</Option>
                        <Option value="approved">Approved</Option>
                        <Option value="rejected">Rejected</Option>
                        <Option value="delivered">Delivered</Option>
                        <Option value="cancelled">Cancelled</Option>
                        <Option value="expired">Expired</Option>
                    </Dropdown>
                    <Dropdown
                        placeholder="Per page"
                        value={pageSize.toString()}
                        selectedOptions={[pageSize.toString()]}
                        onOptionSelect={handlePageSizeChange}
                    >
                        <Option value="20">20 per page</Option>
                        <Option value="50">50 per page</Option>
                        <Option value="100">100 per page</Option>
                    </Dropdown>
                </div>

                {error && <div className={styles.error}>{error}</div>}

                {isLoading ? (
                    <div className={styles.loading}>
                        <Spinner label="Loading alerts..." />
                    </div>
                ) : (
                    <div className={styles.cardGrid}>
                        {alerts.length === 0 ? (
                            <Card className={styles.alertCard}>
                                <Text>No alerts found</Text>
                            </Card>
                        ) : (
                            alerts.map(alert => (
                                <Card key={alert.alertId} className={styles.alertCard}>
                                    <CardHeader
                                        image={<Avatar name={alert.headline} />}
                                        header={
                                            <div className={styles.cardHeader}>
                                                <Text weight="semibold">{alert.headline}</Text>
                                                <Text size={200} className={styles.mutedText}>
                                                    {alert.description?.slice(0, 96) || 'No description provided'}
                                                    {alert.description && alert.description.length > 96 ? '…' : ''}
                                                </Text>
                                            </div>
                                        }
                                        action={getStatusBadge(alert.status)}
                                    />
                                    <div className={styles.metaRow}>
                                        <Badge appearance="outline">{alert.severity}</Badge>
                                        <Badge appearance="outline">{alert.channelType}</Badge>
                                        {getDeliveryBadge(alert.deliveryStatus)}
                                    </div>
                                    <div className={styles.dateRow}>
                                        <span>Created: {new Date(alert.createdAt || '').toLocaleString()}</span>
                                        <span>Expires: {new Date(alert.expiresAt).toLocaleString()}</span>
                                    </div>
                                    <CardFooter className={styles.footer}>
                                        <Text size={200}>Alert ID: {alert.alertId ?? 'Pending'}</Text>
                                        <Button
                                            appearance="primary"
                                            size="small"
                                            onClick={() => handleSelectAlert(alert.alertId)}
                                        >
                                            View details
                                        </Button>
                                    </CardFooter>
                                </Card>
                            ))
                        )}
                    </div>
                )}

                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: tokens.spacingVerticalXXL, gap: tokens.spacingHorizontalM }}>
                    <Button
                        appearance="secondary"
                        onClick={() => setPage(p => Math.max(1, p - 1))}
                        disabled={page === 1 || isLoading}
                    >
                        Previous
                    </Button>
                    <div style={{ display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'center' }}>
                        <Button
                            appearance="subtle"
                            onClick={() => setPage(1)}
                            disabled={page === 1 || isLoading}
                        >
                            First
                        </Button>
                        <Text>{`Page ${page} of ${totalPages || 1}`}</Text>
                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                            ({alerts.length} shown)
                        </Text>
                        <Button
                            appearance="subtle"
                            onClick={() => setPage(totalPages)}
                            disabled={page === totalPages || isLoading || totalPages === 0}
                        >
                            Last
                        </Button>
                    </div>
                    <Button
                        appearance="secondary"
                        onClick={() => setPage(p => p + 1)}
                        disabled={page >= totalPages || isLoading || alerts.length === 0}
                    >
                        Next
                    </Button>
                </div>
            </div>
        </Layout>
    )
}

export default AlertsPage
