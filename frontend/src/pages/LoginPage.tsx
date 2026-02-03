import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
    FluentProvider,
    tokens,
    Button,
    Text,
    makeStyles
} from '@fluentui/react-components'
import { setAuthToken } from '../services/apiInterceptor'
import { appTheme } from '../theme'

const useLoginPageStyles = makeStyles({
    root: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '100vh',
        background: 'linear-gradient(135deg, #1A1E3A 0%, #2B2A5C 45%, #3A2C63 100%)',
        padding: 'clamp(16px, 3vw, 32px)',
    },
    container: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalXXL,
        padding: 'clamp(24px, 4vw, 48px)',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusLarge,
        boxShadow: '0 24px 60px rgba(12, 18, 55, 0.4)',
        maxWidth: '420px',
        width: '100%',
    },
    title: {
        fontSize: tokens.fontSizeBase600,
        fontWeight: tokens.fontWeightBold,
    },
    description: {
        textAlign: 'center',
        color: tokens.colorNeutralForeground3,
    },
    badge: {
        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
        borderRadius: tokens.borderRadiusCircular,
        backgroundColor: tokens.colorNeutralBackground3,
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase100,
        textTransform: 'uppercase',
        letterSpacing: '0.2em',
    },
})

export const LoginPage = () => {
    const styles = useLoginPageStyles()
    const navigate = useNavigate()
    const [isLoading, setIsLoading] = useState(false)

    const handleMockLogin = () => {
        setIsLoading(true)
        // Mock login - in production, this would use MSAL for Entra ID
        const mockToken = `mock-token-${Date.now()}`
        setAuthToken(mockToken)
        setTimeout(() => {
            navigate('/alerts')
        }, 500)
    }

    return (
        <FluentProvider theme={appTheme} dir="ltr">
            <div className={styles.root}>
                <div className={styles.container}>
                    <div className={styles.badge}>Secure access</div>
                    <h1 className={styles.title}>Emergency Alerts System</h1>
                    <Text className={styles.description} size={200}>
                        Sign in to manage emergency alerts
                    </Text>
                    <Button
                        appearance="primary"
                        size="large"
                        onClick={handleMockLogin}
                        disabled={isLoading}
                    >
                        {isLoading ? 'Signing in...' : 'Sign in with Entra ID'}
                    </Button>
                </div>
            </div>
        </FluentProvider>
    )
}

export default LoginPage
