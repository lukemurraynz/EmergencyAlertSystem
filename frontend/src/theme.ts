import { webLightTheme } from '@fluentui/react-components'
import type { Theme } from '@fluentui/react-components'

export const appTheme: Theme = {
    ...webLightTheme,
    fontFamilyBase: '"Manrope", "Segoe UI", system-ui, -apple-system, sans-serif',
    colorBrandBackground: '#F06A7A',
    colorBrandBackgroundHover: '#E45A6C',
    colorBrandBackgroundPressed: '#CC4B5D',
    colorBrandForeground1: '#F06A7A',
    colorBrandForeground2: '#FF9AA8',
    colorBrandForegroundOnLight: '#1B1E3B',
    colorNeutralBackground1: '#F3F1F8',
    colorNeutralBackground2: '#FFFFFF',
    colorNeutralBackground3: '#EDE9F6',
    colorNeutralForeground1: '#181B36',
    colorNeutralForeground2: '#2B2F55',
    colorNeutralForeground3: '#6B6F89',
    colorNeutralStroke1: '#E0DCEE',
    colorNeutralStroke2: '#D4D0E4',
    colorNeutralStroke3: '#C6C2DA',
}

export default appTheme
