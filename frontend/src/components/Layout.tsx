import type { ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
    FluentProvider,
    tokens,
    Button,
    Badge,
    Text,
    Menu,
    MenuTrigger,
    MenuPopover,
    MenuList,
    MenuItem,
    makeStyles,
    mergeClasses,
} from '@fluentui/react-components'
import { SignOut24Regular, Settings24Regular } from '@fluentui/react-icons'
import { appTheme } from '../theme'

const useLayoutStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        minHeight: '100vh',
        position: 'relative',
        background: 'linear-gradient(135deg, #E9E3F4 0%, #E5DEF1 35%, #F3F1F8 100%)',
        padding: 'clamp(16px, 2.4vw, 32px)',
        gap: tokens.spacingVerticalXL,
        overflowX: 'hidden',
        ':before': {
            content: '""',
            position: 'absolute',
            width: '420px',
            height: '420px',
            top: '-120px',
            right: '-120px',
            background: 'radial-gradient(circle, rgba(240,106,122,0.35) 0%, rgba(240,106,122,0) 70%)',
            filter: 'blur(2px)',
            pointerEvents: 'none',
        },
        ':after': {
            content: '""',
            position: 'absolute',
            width: '520px',
            height: '520px',
            bottom: '-220px',
            left: '-180px',
            background: 'radial-gradient(circle, rgba(37,42,98,0.3) 0%, rgba(37,42,98,0) 70%)',
            filter: 'blur(2px)',
            pointerEvents: 'none',
        },
    },
    shell: {
        width: 'min(1280px, 100%)',
        margin: '0 auto',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXL,
        position: 'relative',
        zIndex: 1,
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: 'clamp(16px, 2.2vw, 28px)',
        background: 'linear-gradient(135deg, #1A1E3A 0%, #2B2A5C 55%, #3A2C63 100%)',
        color: tokens.colorNeutralForegroundOnBrand,
        borderRadius: tokens.borderRadiusLarge,
        boxShadow: '0 24px 48px rgba(24, 27, 64, 0.25)',
        border: '1px solid rgba(255,255,255,0.08)',
    },
    headerLeft: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    headerEyebrow: {
        fontSize: tokens.fontSizeBase100,
        textTransform: 'uppercase',
        letterSpacing: '0.18em',
        color: 'rgba(255,255,255,0.72)',
        margin: 0,
    },
    headerTitle: {
        fontSize: tokens.fontSizeBase600,
        fontWeight: tokens.fontWeightBold,
        margin: 0,
    },
    headerNav: {
        display: 'flex',
        gap: tokens.spacingHorizontalL,
        alignItems: 'center',
    },
    navButton: {
        borderRadius: tokens.borderRadiusXLarge,
        backgroundColor: 'rgba(255,255,255,0.12)',
        color: tokens.colorNeutralForegroundOnBrand,
        border: '1px solid rgba(255,255,255,0.18)',
        ':hover': {
            backgroundColor: 'rgba(255,255,255,0.2)',
        },
    },
    content: {
        display: 'grid',
        gridTemplateColumns: '280px 1fr',
        gap: tokens.spacingHorizontalXXL,
        flex: 1,
        minHeight: 0,
        '@media (max-width: 1024px)': {
            gridTemplateColumns: '1fr',
        },
    },
    sidebar: {
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusLarge,
        padding: tokens.spacingHorizontalL,
        boxShadow: '0 18px 32px rgba(20, 27, 76, 0.12)',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    sidebarHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    sidebarNav: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        '@media (max-width: 1024px)': {
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
            gap: tokens.spacingHorizontalM,
        },
    },
    sidebarLink: {
        textDecoration: 'none',
        color: tokens.colorNeutralForeground2,
        display: 'block',
        padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
        borderRadius: tokens.borderRadiusMedium,
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground3,
        },
    },
    main: {
        flex: 1,
        minWidth: 0,
        padding: 'clamp(18px, 2.4vw, 32px)',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusLarge,
        boxShadow: '0 18px 32px rgba(20, 27, 76, 0.1)',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        animation: 'pageReveal 0.6s ease',
    },
    liveBadge: {
        backgroundColor: 'rgba(255,255,255,0.18)',
        color: tokens.colorNeutralForegroundOnBrand,
        border: '1px solid rgba(255,255,255,0.25)',
    },
})

interface LayoutProps {
    children: ReactNode
}

export const Layout = ({ children }: LayoutProps) => {
    const styles = useLayoutStyles()
    const navigate = useNavigate()

    const handleLogout = () => {
        sessionStorage.removeItem('authToken')
        navigate('/login')
    }

    return (
        <FluentProvider theme={appTheme} dir="ltr">
            <div className={styles.root}>
                <div className={styles.shell}>
                    <header className={styles.header}>
                        <div className={styles.headerLeft}>
                            <p className={styles.headerEyebrow}>EAS operations</p>
                            <h1 className={styles.headerTitle}>Emergency Alerts System</h1>
                        </div>
                        <nav className={styles.headerNav}>
                            <Badge appearance="outline" className={styles.liveBadge}>Live</Badge>
                            <Link to="/alerts" style={{ textDecoration: 'none', color: 'inherit' }}>
                                <Button appearance="subtle" size="medium" className={styles.navButton}>
                                    Alerts
                                </Button>
                            </Link>
                            <Link to="/approvals" style={{ textDecoration: 'none', color: 'inherit' }}>
                                <Button appearance="subtle" size="medium" className={styles.navButton}>
                                    Approvals
                                </Button>
                            </Link>
                            <Link to="/dashboard" style={{ textDecoration: 'none', color: 'inherit' }}>
                                <Button appearance="subtle" size="medium" className={styles.navButton}>
                                    Dashboard
                                </Button>
                            </Link>
                            <Menu>
                                <MenuTrigger disableButtonEnhancement>
                                    <Button appearance="subtle" icon={<Settings24Regular />} className={styles.navButton} />
                                </MenuTrigger>
                                <MenuPopover>
                                    <MenuList>
                                        <MenuItem onClick={handleLogout} icon={<SignOut24Regular />}>
                                            Sign Out
                                        </MenuItem>
                                    </MenuList>
                                </MenuPopover>
                            </Menu>
                        </nav>
                    </header>

                    <div className={styles.content}>
                        <aside className={styles.sidebar}>
                            <div className={styles.sidebarHeader}>
                                <Text weight="semibold">Quick actions</Text>
                            </div>
                            <nav className={styles.sidebarNav}>
                                <Link to="/alerts/create" className={mergeClasses(styles.sidebarLink)}>
                                    <Button appearance="secondary" style={{ width: '100%' }}>
                                        Create Alert
                                    </Button>
                                </Link>
                                <Link to="/alerts" className={styles.sidebarLink}>
                                    View All Alerts
                                </Link>
                                <Link to="/approvals" className={styles.sidebarLink}>
                                    Approvals
                                </Link>
                                <Link to="/dashboard" className={styles.sidebarLink}>
                                    Dashboard
                                </Link>
                            </nav>
                        </aside>

                        <main className={styles.main}>{children}</main>
                    </div>
                </div>
            </div>
        </FluentProvider>
    )
}

export default Layout
