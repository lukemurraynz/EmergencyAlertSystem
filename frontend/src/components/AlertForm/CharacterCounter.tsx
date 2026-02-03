import { makeStyles, tokens } from '@fluentui/react-components'

const useCharacterCounterStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    counter: {
        fontSize: tokens.fontSizeBase100,
        display: 'flex',
        gap: tokens.spacingHorizontalS,
    },
    warning: {
        color: tokens.colorPaletteDarkOrangeForeground1,
    },
    error: {
        color: tokens.colorPaletteRedForeground1,
    },
    success: {
        color: tokens.colorPaletteGreenForeground1,
    },
})

interface CharacterCounterProps {
    text: string
    maxGsmLength?: number
    maxUcsLength?: number
}

// GSM 7-bit alphabet characters
const GSM_CHARSET = new Set([
    '@', '£', '$', '¥', 'è', 'é', 'ù', 'ì', 'ò', 'Ç', '\n', 'Ø', 'ø', '\r', 'Å', 'å',
    'Δ', '_', 'Φ', 'Γ', 'Λ', 'Ω', 'Π', 'Ψ', 'Σ', 'Θ', 'Ξ', '', '', '', '', '',
    ' ', '!', '"', '#', '¤', '%', '&', "'", '(', ')', '*', '+', ',', '-', '.', '/',
    '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ':', ';', '<', '=', '>', '?',
    '¡', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
    'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'Ä', 'Ö', 'Ñ', 'Ü', '§',
    '¿', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o',
    'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'ä', 'ö', 'ñ', 'ü', 'à',
    '^', '{', '}', '\\', '[', '~', ']', '|', '€',
])

export const CharacterCounter = ({ text, maxGsmLength = 1395, maxUcsLength = 615 }: CharacterCounterProps) => {
    const styles = useCharacterCounterStyles()

    // Detect if text is GSM 7-bit compatible
    const isGsm = Array.from(text).every(char => GSM_CHARSET.has(char))

    const encoding = isGsm ? 'GSM 7-bit' : 'UCS-2'
    const maxLength = isGsm ? maxGsmLength : maxUcsLength
    const currentLength = text.length
    const remaining = maxLength - currentLength
    const percentage = (currentLength / maxLength) * 100

    let statusClass = styles.success
    if (percentage > 80) {
        statusClass = styles.warning
    }
    if (currentLength > maxLength) {
        statusClass = styles.error
    }

    return (
        <div className={styles.root}>
            <div className={`${styles.counter} ${statusClass}`}>
                <span>{currentLength} / {maxLength} characters</span>
                <span>({encoding})</span>
                <span>{remaining >= 0 ? `${remaining} remaining` : `${Math.abs(remaining)} over`}</span>
            </div>
        </div>
    )
}

export default CharacterCounter
