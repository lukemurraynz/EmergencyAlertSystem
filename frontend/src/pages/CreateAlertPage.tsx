import { useNavigate } from 'react-router-dom'
import { Text, makeStyles, tokens } from '@fluentui/react-components'
import Layout from '../components/Layout'
import AlertForm from '../components/AlertForm/AlertForm'
import type { Alert } from '../types'

const useCreateAlertPageStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXL,
    },
    header: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    title: {
        fontSize: tokens.fontSizeBase600,
        fontWeight: tokens.fontWeightBold,
        margin: 0,
    },
})

export const CreateAlertPage = () => {
    const styles = useCreateAlertPageStyles()
    const navigate = useNavigate()

    const handleSuccess = (alert: Alert) => {
        navigate(`/alerts/${alert.alertId}`)
    }

    return (
        <Layout>
            <div className={styles.root}>
                <div className={styles.header}>
                    <h1 className={styles.title}>Create New Alert</h1>
                    <Text size={200}>Fill out the form below to create and submit a new emergency alert</Text>
                </div>

                <AlertForm onSuccess={handleSuccess} />
            </div>
        </Layout>
    )
}

export default CreateAlertPage
