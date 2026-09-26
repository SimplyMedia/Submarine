import { createI18n } from 'vue-i18n'
import english from '../i18n/locales/en.json'

type LocaleMessage = string | { [key: string]: LocaleMessage }

const localeCatalogs = {
	en: {
		nameKey: 'settings.ui.localeNames.en',
	},
}

export const availableLocales = Object.entries(localeCatalogs).map(([code, entry]) => ({
	code,
	nameKey: entry.nameKey,
}))

export const i18n = createI18n({
	legacy: false,
	locale: 'en',
	fallbackLocale: 'en',
	messages: { en: english as Record<string, LocaleMessage> },
	globalInjection: true,
	missingWarn: false,
	fallbackWarn: false,
})
